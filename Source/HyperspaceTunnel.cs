using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScienceFoundry.FTL
{
    public class HyperspaceTunnel
    {
        public Vessel Source { get; set; }
        public Vessel Destination { get; set; }

        public HyperspaceTunnel()
        {
            Source = null;
            Destination = null;
        }

        private double CalculateGravitation(CelestialBody body, double altitude)
        {
            double retValue = body.gravParameter/(altitude*altitude);
            var orbit = body.GetOrbit();
            
            if (orbit != null)
            {
                if (orbit.referenceBody != null)
                {
                    retValue += CalculateGravitation(orbit.referenceBody, orbit.altitude + orbit.referenceBody.Radius);
                }
            }
            
            return retValue;
        }

        private double PunchRequirement(Vessel vessel)
        {
            var orbit = vessel.GetOrbitDriver().orbit;
            return CalculateGravitation(orbit.referenceBody, 
                                        orbit.altitude + orbit.referenceBody.Radius);
        }

        public double GetForceRequired()
        {
            return Possible ? (PunchRequirement(Source) + PunchRequirement(Destination)) * Source.GetTotalMass() * 1e3 : Double.PositiveInfinity;
        }

        public double SuccesProbability(double generatedPunchForce)
        {
            double retValue = 0;

            if (Possible)
            {
                var forceRequired = GetForceRequired();

                if (forceRequired > generatedPunchForce)
                {
                    retValue = generatedPunchForce / forceRequired;
                }
                else
                {
                    retValue = 1;
                }
            }

            return retValue;
        }

        public bool Possible
        {
            get
            {
                if (Destination == null)
                    return false;
                if (Source == null)
                    return false;
                if (Source.GetOrbitDriver() == null)
                    return false;
                if (Destination.GetOrbitDriver() == null)
                    return false;
                if (Source.GetOrbitDriver().orbit.referenceBody == null)
                    return false;
                if (Destination.GetOrbitDriver().referenceBody == null)
                    return false;

                return true;
            }
        }
    }
}
