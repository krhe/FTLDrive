﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Drive")]
    public class FTLDriveModule : PartModule
    {
        private enum DriveState
        {
            IDLE,
            STARTING,
            SPINNING,
            JUMPING
        }

        private DriveState state = DriveState.IDLE;
        private double activationTime = 0;
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
                generatedForceStr = String.Format("{0:0.0}iN", value);
                generatedForce = value;
            }
        }

        private double generatedForce = 0;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Generated force", isPersistant = false)]
        private string generatedForceStr = "0.0iN";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Force required", isPersistant = false)]
        private string requiredForce = "Inf";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Success probability", isPersistant = false)]
        private string successProb = "?";

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

            str.AppendFormat("Maximal force: {0:0.0}iN\n", maxGeneratorForce);
            str.AppendFormat("Maximal charge time: {0:0.0}s\n\n", maxChargeTime);
            str.AppendFormat("Requires\n");
            str.AppendFormat("- Electric charge: {0:0.00}/s\n\n", requiredElectricalCharge);
            str.Append("Navigational computer\n");
            str.Append("- Required force\n");
            str.Append("- Success probability\n");

            return str.ToString();
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Jump")]
        public void Jump()
        {
            if (navCom.JumpPossible)
            {
                if (state == DriveState.IDLE)
                {
                    ScreenMessages.PostScreenMessage("Spinning up FTL drive...", (float)maxChargeTime, ScreenMessageStyle.UPPER_CENTER);
                    state = DriveState.STARTING;
                    driveSound.audio.Play();
                }
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
            if (state == DriveState.IDLE)
            {
                navCom.Destination = BeaconSelector.Next(navCom.Destination, FlightGlobals.ActiveVessel);
                UpdateJumpStatistics();
            }
        }

        [KSPAction("Next beacon")]
        public void NextAction(KSPActionParam p)
        {
            if (state == DriveState.IDLE)
            {
                NextBeacon();

                if (navCom.JumpPossible)
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

        }

        private void UpdateJumpStatistics()
        {
            if (navCom.JumpPossible)
            {
                beaconName = navCom.Destination.vesselName;
                requiredForce = String.Format("{0:0.0}iN", navCom.GetRequiredForce());
                successProb = String.Format("{0:0.0}%", navCom.GetSuccesProbability(maxGeneratorForce)*100);
            }
            else
            {
                beaconName = BeaconSelector.NO_TARGET;
                requiredForce = "Inf";
                successProb = "?";
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            SoundManager.LoadSound("FTLDrive/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            try
            {
                this.state = DriveState.IDLE;

                if (state != StartState.Editor)
                    navCom.Source = FlightGlobals.ActiveVessel;
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

        private double lastUpdateTime = -1.0f;

        private double LastUpdateTime
        {
            get
            {
                if (lastUpdateTime < 0)
                {
                    lastUpdateTime = Planetarium.GetUniversalTime();
                }

                return lastUpdateTime;
            }
            set
            {
                lastUpdateTime = value;
            }
        }

        public void FixedUpdate()
        {
            if (IsVesselReady())
            {
                var deltaT = GetElapsedTime();
                LastUpdateTime += deltaT;
                activationTime += deltaT;

                switch (state)
                {
                    case DriveState.IDLE:
                        break;
                    case DriveState.STARTING:
                        state = DriveState.SPINNING;
                        activationTime = 0;
                        break;
                    case DriveState.SPINNING:
                        SpinningUpDrive(deltaT);
                        break;
                    case DriveState.JUMPING:
                        ExecuteJump();
                        break;
                }
            }

            base.OnFixedUpdate();
        }

        /**
         * \brief Check if the vessel is ready
         * \return true if the vessel is ready, otherwise false.
         */
        private static bool IsVesselReady()
        {
            return (Time.timeSinceLevelLoad > 1.0f) && FlightGlobals.ready;
        }

        /**
         * \brief Return the elapsed time since last update.
         * \return elapsed time
         */
        private double GetElapsedTime()
        {
            return Planetarium.GetUniversalTime() - LastUpdateTime;
        }

        public override void OnUpdate()
        {
            UpdateJumpStatistics();
            base.OnUpdate();
        }

        private void SpinningUpDrive(double deltaT)
        {
            var spinRate = maxGeneratorForce / maxChargeTime;

            if (activationTime >= maxChargeTime)
            {
                Force += PowerDrive((maxChargeTime - (activationTime - deltaT)) * spinRate, deltaT);
                state = DriveState.JUMPING;
            }
            else
            {
                Force += PowerDrive(deltaT * spinRate, deltaT);

                if (Force > navCom.GetRequiredForce())
                {
                    state = DriveState.JUMPING;
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
            Force = 0;
            state = DriveState.IDLE;
        }
    }
}
