namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// One prop blade's state (p7f-damage design 1, PHY-26) along props.blade's stations (PropBlade): its tip radius, each
    /// station's chord share left (a leading-edge chip removes from the front), pitch set and flap set, and its factory
    /// pitch offset (prop.blade_pitch, design 3). A new blade is whole: the tip at 1, every share 1, every set 0. Part of the
    /// flight's state like the contact latches: preallocated with the flight, changed only by a damaging step (p7f-damage
    /// 1.3 on) or a test fixture, and read by its rotor (DamagedRotor.Rebuild).
    /// The material: each station is a uniform piece of its span; the share of a station inside the tip and its chord share
    /// both scale its mass (assumed: a chip takes a section's area in proportion to its chord), at the piece's own centroid.
    /// </summary>
    public sealed class BladeState
    {
        /// <summary>The tip radius, r/R: 1 new.</summary>
        public double TipRadius = 1;

        /// <summary>Per station: the chord share left (1 new), the pitch set (rad, 0 new) and the flap set, the station's
        /// permanent displacement along the shaft (m, toward the thrust; 0 new).</summary>
        public readonly double[] ChordShare, PitchSet, FlapSet;

        /// <summary>The blade's factory pitch offset, rad, over the whole blade (prop.blade_pitch's draw; 0 without it).</summary>
        public double FactoryPitch;

        public BladeState(int stations)
        {
            ChordShare = new double[stations];
            PitchSet = new double[stations];
            FlapSet = new double[stations];
            Reset(0);
        }

        /// <summary>A new blade with its factory pitch offset (rad).</summary>
        public void Reset(double factoryPitch)
        {
            TipRadius = 1;
            for (int k = 0; k < ChordShare.Length; k++)
            {
                ChordShare[k] = 1;
                PitchSet[k] = 0;
                FlapSet[k] = 0;
            }
            FactoryPitch = factoryPitch;
        }

        /// <summary>Whole: the tip at 1, every chord share 1 and every set 0 (the factory pitch aside).</summary>
        public bool Whole
        {
            get
            {
                if (TipRadius != 1) return false;
                for (int k = 0; k < ChordShare.Length; k++)
                    if (ChordShare[k] != 1 || PitchSet[k] != 0 || FlapSet[k] != 0) return false;
                return true;
            }
        }

        /// <summary>The blade cut back to a tip radius (r/R): what lies beyond it is gone (a trim; never grows back).</summary>
        public void Trim(double tipRadius)
        {
            if (tipRadius < TipRadius) TipRadius = tipRadius < 0 ? 0 : tipRadius;
        }

        /// <summary>The share of station k's span (centred at x, span h, r/R) inside the tip, 0–1.</summary>
        public double Inside(double x, double h)
        {
            double f = (TipRadius - (x - 0.5 * h)) / h;
            return f > 1 ? 1 : f < 0 ? 0 : f;
        }
    }
}
