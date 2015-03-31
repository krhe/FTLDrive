using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public class FTLDriveModule : PartModule
    {
        //FXGroup alarmSound;

        private float lastUpdate = 0.0f;
        private float updateInterval = 1f;
        private float activationTime = 0;
        private bool startSpin = false;
        private FXGroup driveSound;

        /**
         * \brief Jump beacon name (displayed in the GUI)
         * This is the name of the currently selected jump beacon. It is updated from the Next function,
         * which will go to the next active beacon on the list.
         * \note this variable is not actually used by the mod, it is only for the GUI
         */
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Beacon", isPersistant = false)]
        private string beaconName = BeaconSelector.NO_TARGET;

        private double Force
        {
            get
            {
                return generatedForce;
            }
            set
            {
                generatedForce = value;
            }
        }

        private double generatedForce = 0;


        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Force required", isPersistant = false)]
        private string requiredForce = "Inf";

        /**
         * \brief Is the ship spinning up its drive.
         */
        private bool driveActivated = false;

        [KSPField(guiActive=false, guiActiveEditor=false, isPersistant=true)]
        public double maxGeneratorForce = 2000;

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        public double maxChargeTime = 10;

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        public double requiredElectricalCharge = 100;

        /**
         * \brief Currently selected beacon.
         */
        private NavComputer navCom = new NavComputer();

        public override string GetInfo()
        {
            var str = new StringBuilder();

            str.AppendFormat("Maximal force: {0:0.0N}\n", maxGeneratorForce);
            str.AppendFormat("Maximal charge time: {0:0.0}s\n\n", maxChargeTime);
            str.AppendFormat("Requires\n");
            str.AppendFormat("- Electric charge: {0:0.00}/s\n\n", requiredElectricalCharge);
            str.Append("Navigational computer\n");
            str.Append("- Required force\n");

            return str.ToString();
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Jump")]
        public void Jump()
        {
            if (!driveActivated)
            {
                ScreenMessages.PostScreenMessage("Spinning up FTL drive...", (float)maxChargeTime, ScreenMessageStyle.UPPER_CENTER);
                startSpin = true;
                driveSound.audio.Play();
            }
        }

        [KSPAction("Activate drive")]
        public void JumpAction(KSPActionParam p)
        {
            Jump();
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Next Beacon")]
        public void NextBeacon()
        {
            navCom.Beacon = BeaconSelector.Next(navCom.Beacon, FlightGlobals.ActiveVessel);
            UpdateJumpStatistics();
        }

        [KSPAction("Next beacon")]
        public void NextAction(KSPActionParam p)
        {
            NextBeacon();

            if (navCom.IsJumpPossible())
            {
                ScreenMessages.PostScreenMessage(String.Format("Beacon {0} selected", beaconName),
                                                 4f,
                                                 ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                ScreenMessages.PostScreenMessage("NAVCOM Unavailable", 4f, ScreenMessageStyle.UPPER_CENTER);
            }

        }

        private void UpdateJumpStatistics()
        {
            if (navCom.IsJumpPossible())
            {
                beaconName = navCom.Beacon.vesselName;
                requiredForce = String.Format("{0:0.0}N / {1:0.0}N", navCom.GetRequiredForce(), maxGeneratorForce);
            }
            else
            {
                beaconName = BeaconSelector.NO_TARGET;
                requiredForce = "Inf";
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            SoundManager.LoadSound("FTLDrive/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            try
            {
                driveActivated = false;                

                if (state != StartState.Editor)
                    navCom.ActiveVessel = FlightGlobals.ActiveVessel;
            }
            catch (Exception ex)
            {
                print(String.Format("[FTLDrive] Error in OnStart - {0}", ex.ToString()));
            }

            base.OnStart(state);
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                navCom = new NavComputer();
                maxGeneratorForce = Convert.ToDouble(node.GetValue("maxGeneratorForce"));
                maxChargeTime = Convert.ToDouble(node.GetValue("maxChargeTime"));
                requiredElectricalCharge = Convert.ToDouble(node.GetValue("requiredElectricalCharge"));
            }
            catch (Exception ex)
            {
                print(String.Format("[FTLDrive] Error in OnLoad - {0}", ex.ToString()));
            }

            base.OnLoad(node);
        }

        public override void OnAwake()
        {
            base.OnAwake();
        }

        public override void OnUpdate()
        {
            var currentTime = Time.time;
            var deltaT = currentTime - lastUpdate;

            if (deltaT > updateInterval)
            {
                lastUpdate = currentTime;
                activationTime += deltaT;

                if (startSpin)
                {
                    StartDrive();
                }

                if (driveActivated)
                {
                    SpinningUpDrive(deltaT);
                }

                UpdateJumpStatistics();
            }


            base.OnUpdate();
        }

        private void SpinningUpDrive(double deltaT)
        {
            var spinRate = maxGeneratorForce / maxChargeTime;

            if (activationTime >= maxChargeTime)
            {
                Force += PowerDrive((maxChargeTime - (activationTime - deltaT)) * spinRate, deltaT);
                ExecuteJump();
            }
            else
            {
                Force += PowerDrive(deltaT * spinRate, deltaT);

                if (Force > navCom.GetRequiredForce())
                {
                    ExecuteJump();
                }
            }
        }

        private double PowerDrive(double deltaF, double deltaT)
        {
            var demand = deltaT * requiredElectricalCharge;
            var delivered = part.RequestResource("ElectricCharge", demand);
            return deltaF * (delivered/demand);
        }

        private void ExecuteJump()
        {
            if (navCom.Jump(Force))
            {
                ScreenMessages.PostScreenMessage("Jump Completed!", 2f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                ScreenMessages.PostScreenMessage("Jump failed!", 2f, ScreenMessageStyle.UPPER_CENTER);
            }

            driveSound.audio.Stop();
            driveActivated = false;
            updateInterval = 1f;
            Force = 0;
        }

        private void StartDrive()
        {
            lastUpdate = Time.time;
            startSpin = false;
            driveActivated = true;
            activationTime = 0;
            updateInterval = 0.1f;
        }
    }
}
