namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// The wind's mean at a plant (w16-plants-in-the-air 1.3): Respond's first half, which the drawn side computes on a coarse grid
    /// at a low rate and the GPU interpolates (design.md §7.1, "a coarse CPU texture of the mean lean and the means"). The air at
    /// the load point (the field's mean, plus any steady air), the mean lean in the plane of its horizontal way, the fluctuation's
    /// covariance there and the fluctuation's families at their shares.
    /// </summary>
    public struct PlantMean
    {
        /// <summary>The mean air at the load point, world m/s.</summary>
        public Double3 Air;

        /// <summary>The mean lean, rad, toward (WayX, WayZ): the mean air's horizontal way (+X in still air).</summary>
        public double Lean, WayX, WayZ;

        /// <summary>The load point's world height, m.</summary>
        public double LoadY;

        /// <summary>The fluctuation's covariance at the load point, (m/s)², from the modes (Σ aᵢaⱼ/2).</summary>
        public double XX, XZ, ZZ, YY, XY, ZY;

        internal PlantParts Parts;

        /// <summary>The open modes' share, and the profile's log at the load point (their scale's).</summary>
        public readonly double Open => Parts.Open;
        public readonly double Lambda => Parts.Lambda;

        /// <summary>The strip's eddies' share (PlantAir.StripOf names the strip).</summary>
        public readonly double StripShare => Parts.StripShare;

        /// <summary>The wake eddies' share and 1 over the obstacle's height, 1/m.</summary>
        public readonly double Wake => Parts.Wake;
        public readonly double WakeInverse => Parts.WakeInverse;
    }

    /// <summary>One fluctuation mode as a table holds it: its wave vector (rad/m; a wake eddy's unit direction), its amplitude at
    /// share 1 (world m/s), its phase (rad) and a wake eddy's 2π over its size in obstacle heights (1 for the others).</summary>
    public struct PlantMode
    {
        public Double3 Wave, Amplitude;
        public double Phase, Scale;
    }

    public sealed partial class PlantAir
    {
        /// <summary>The frame's reference mean wind (world m/s, at the reference height) and the field's drift since the start (world
        /// m), as Begin fixed them: a mode's phase at a point p is κ·p + φ − κ·drift and its rate κ·mean (a strip's eddies take
        /// their convection's share of both; a wake eddy's its scale over the obstacle's height).</summary>
        public Double3 FrameMean => _mean;
        public Double3 FrameDrift => _drift;

        /// <summary>The wind's mean at a plant at the frame (no wash): its base (world m) over the ground under it (m).</summary>
        public void Mean(in PlantMechanics m, in Double3 basePoint, double ground, out PlantMean mean) =>
            Mean(m, basePoint, ground, default, out mean);

        /// <summary>The strip a mean's eddies are read from: its index in the field's strips (StripModes), −1 for none.</summary>
        public int StripOf(in PlantMean mean) => _wind.PlantStripIndex(mean.Parts.Strip);

        /// <summary>The open modes' count, and the modes at a load point whose profile's log is lambda (PlantMean.Lambda), at share
        /// 1. Their wave vectors, phases and amplitudes are the field's for the whole flight.</summary>
        public int OpenModeCount => _wind.PlantOpenCount;

        public void OpenModes(double lambda, PlantMode[] into) => _wind.PlantOpenModes(lambda, into);

        /// <summary>The wooded strips' count, a strip's eddies' count, and its eddies at share 1; returns their convection over the
        /// field's drift.</summary>
        public int StripCount => _wind.PlantStripCount;

        public int StripModeCount(int strip) => _wind.PlantStrip(strip).PlantModeCount;

        public double StripModes(int strip, PlantMode[] into) => _wind.PlantStrip(strip).PlantModeTable(into);

        /// <summary>The wake eddies' count and the eddies (unit directions and amplitudes; Scale over a point's obstacle height is its
        /// wave number).</summary>
        public int WakeModeCount => _wind.PlantWakeCount;

        public void WakeModes(PlantMode[] into) => _wind.PlantWakeModes(into);
    }
}
