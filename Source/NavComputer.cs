using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScienceFoundry.FTL
{
    public class NavComputer
    {
        private HyperspaceTunnel tunnel = new HyperspaceTunnel();

        public Vessel Beacon 
        { 
            get
            {
                return tunnel.Destination;
            }
            set
            {
                tunnel.Destination = value;
            }
        }

        public Vessel ActiveVessel 
        { 
            get
            {
                return tunnel.Source;
            }
            set
            {
                tunnel.Source = value;
            }
        }

        public bool IsJumpPossible()
        {
            bool retValue = false;

            if ((Beacon != null) && (ActiveVessel != null))
            {
                if ((ActiveVessel.situation == Vessel.Situations.ORBITING) || 
                    (ActiveVessel.situation == Vessel.Situations.ESCAPING))
                {
                    if ((Beacon.situation == Vessel.Situations.ORBITING) ||
                        (Beacon.situation == Vessel.Situations.ESCAPING))
                    {
                        if (tunnel.Possible)
                        {
                            retValue = true;
                        }
                    }
                }
            }

            return retValue;
        }

        public bool Jump(double force)
        {
            bool retValue = false;

            if (IsJumpPossible())
            {
                var rnd = new System.Random();

                if (rnd.NextDouble() < tunnel.SuccesProbability(force))
                {
                    Rendezvous();
                    retValue = true;
                }
                else
                    KillShip();
            }

            return retValue;
        }

        private void KillShip()
        {
            var parts = ActiveVessel.Parts.ToArray();
            
            foreach (var p in ActiveVessel.Parts)
            {
                p.explode();
            }
        }

        private void Rendezvous(double leadTime = 2)
        {
            if (IsJumpPossible())
            {
                var o = Beacon.orbit;
                var newOrbit = CreateOrbit(o.inclination,
                                           o.eccentricity,
                                           o.semiMajorAxis,
                                           o.LAN,
                                           o.argumentOfPeriapsis,
                                           o.meanAnomalyAtEpoch,
                                           o.epoch - leadTime,
                                           o.referenceBody);

                SetOrbit(ActiveVessel, newOrbit);
            }
        }

        public double GetSuccessProbability(double force)
        {
            return tunnel.SuccesProbability(force);
        }

        public double GetRequiredForce()
        {
            return tunnel.GetForceRequired();
        }

        private void SetOrbit(Vessel vessel, Orbit newOrbit)
        {
            if (newOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()).magnitude > newOrbit.referenceBody.sphereOfInfluence)
            {
                UnityEngine.Debug.Log("Destination position was above the sphere of influence");
                return;
            }
                
            try
            {
                OrbitPhysicsManager.HoldVesselUnpack(60);
            }
            catch (NullReferenceException)
            {
                UnityEngine.Debug.Log("OrbitPhysicsManager.HoldVesselUnpack threw NullReferenceException");
            }

            vessel.GoOnRails();

            var oldBody = vessel.orbitDriver.orbit.referenceBody;

            UpdateOrbit(vessel.orbitDriver, newOrbit);

            vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
            vessel.orbitDriver.vel = vessel.orbit.vel;

            var newBody = vessel.orbitDriver.orbit.referenceBody;

            if (newBody != oldBody)
            {
                var evnt = new GameEvents.HostedFromToAction<Vessel, CelestialBody>(vessel, oldBody, newBody);
                GameEvents.onVesselSOIChanged.Fire(evnt);
            }
        }

        private void UpdateOrbit(OrbitDriver orbitDriver, Orbit newOrbit)
        {
            var orbit = orbitDriver.orbit;

            orbit.inclination = newOrbit.inclination;
            orbit.eccentricity = newOrbit.eccentricity;
            orbit.semiMajorAxis = newOrbit.semiMajorAxis;
            orbit.LAN = newOrbit.LAN;
            orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
            orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
            orbit.epoch = newOrbit.epoch;
            orbit.referenceBody = newOrbit.referenceBody;
            orbit.Init();
            orbit.UpdateFromUT(Planetarium.GetUniversalTime());

            if (orbit.referenceBody != newOrbit.referenceBody)
            {
                if (orbitDriver.OnReferenceBodyChange != null)
                    orbitDriver.OnReferenceBodyChange(newOrbit.referenceBody);
            }
        }
        
        private Orbit CreateOrbit(double inc, double e, double sma, double lan, double w, double mEp, double epoch, CelestialBody body)
        {
            if (double.IsNaN(inc))
                inc = 0;
            if (double.IsNaN(e))
                e = 0;
            if (double.IsNaN(sma))
                sma = body.Radius + body.maxAtmosphereAltitude + 10000;
            if (double.IsNaN(lan))
                lan = 0;
            if (double.IsNaN(w))
                w = 0;
            if (double.IsNaN(mEp))
                mEp = 0;
            if (double.IsNaN(epoch))
                mEp = Planetarium.GetUniversalTime();

            if (Math.Sign(e - 1) == Math.Sign(sma))
                sma = -sma;

            if (Math.Sign(sma) >= 0)
            {
                while (mEp < 0)
                    mEp += Math.PI * 2;
                while (mEp > Math.PI * 2)
                    mEp -= Math.PI * 2;
            }

            return new Orbit(inc, e, sma, lan, w, mEp, epoch, body);
        }
    }
}
