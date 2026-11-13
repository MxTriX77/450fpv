namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Each rotor's own wake unsteadiness in free air (D-064, PHY-07/08's "never perfectly still"; p7b-airframe design 3e),
    /// inside the inflow seam (Propulsion.RotorInflow): a hovering rotor's wake is unstable (its tip vortices pair, merge and
    /// break down within a few revolutions), so the inflow it induces through its own disc wanders and the thrust and torque
    /// with it, below the shaft rate. Each rotor's wake inflow through its disc, x·v_h, is a first-order process about 0, its
    /// unit from the named stream "rotor.wake" (counter-based on the step and the corner, so the four wander apart), bounded,
    /// entering the thrust's and the torque's advance ratio as the re-ingested wash does. Never on the pose.
    /// - **The level** (ThrustSigma): the thrust's σ in hover, a share of the thrust, converted to an inflow over v_h through
    ///   the props' static slope (Propulsion: σ_x = σ_T·(1 − r)/(2r)). The four rotors wander independently (assumed: no
    ///   cross-correlation of neighbouring rotors' wakes is published).
    /// - **Not carried:** Zhou, Ning and Hu's (ISPIV 2021) neighbour gain, the thrust's σ 2.4× at a 0.05 D tip gap and the
    ///   single rotor's from 0.5 D (this drone's gap is 0.26 D). It is the ratio of the whole thrust σ over 0–1500 Hz, which
    ///   the paper ties to the blades' loading at the blade passage (its tonal noise) and the near-blade turbulence; it does
    ///   not show the wake's wander below the rev rising, so it is not applied to it.
    /// - **The flight state** (p7b-airframe design 3f): the wander the disc feels is its near wake's, a sheet of vorticity
    ///   γ = 2·v_i whose filaments wander further the older they are (Leishman 1998, Karpatne et al. 2013: the tip vortex's
    ///   wander grows with its wake age; linear growth assumed). The wake leaves the disc at its transport velocity
    ///   V_t = √(V_∥² + (V_a + v_i)²), so a filament a distance s off the disc is s/V_t old, and the inflow it induces at the
    ///   disc wanders by ∫ γ·(s/V_t)/s² ds ∝ v_i/V_t (derived). By Glauert's momentum theory (published) v_i·V_t = v_h², so
    ///   in v_h units the inflow's σ is the hover's times (v_i/v_h)², and the wake's eddies pass at V_t, so its corner is
    ///   f·V_t/R. Hover: 1 and 1. Forward flight: the wake swept back, 0.236 and 2.06× at μ 2 (the cruise). A climb: the
    ///   wake leaves faster, 0.74 and 1.16× at V_a 0.3 v_h. A descent: the wake comes back toward the disc, momentum theory
    ///   held to its limit DescentHold (Johnson's VzD), 1.22 and 0.905× there; deeper descents are the vortex ring's
    ///   (VortexRingSpec, whose σ, 0 at VzD, adds to this one), so nothing is counted twice. Near the ground the wake's
    ///   return is the re-ingested wash's (GroundEffectSpec); this term takes the hub's own air only.
    /// - **Its band** (CornerFrequency): a first-order corner at f·V_t/R.
    /// - **The bound**: Bound times the state's σ.
    /// </summary>
    public sealed class RotorWakeSpec
    {
        /// <summary>The thrust's σ in hover, a share of the thrust: 0.029 (derived from published: Stack, Caradonna and Savaş
        /// 2004, "Flow visualizations and extended thrust time histories of rotor vortex wakes in descent", AHS 4th Decennial
        /// Specialist's Conference on Aeromechanics, NASA Ames: a 25.4 cm three-bladed rotor in hover in a 60 m tow tank,
        /// its thrust filtered below the rev over 90 revolutions, fluctuating 12 % of the mean peak to peak; read as about
        /// 4.2σ, the expected range of 30–50 independent samples of a Gaussian, so σ 2.7–3.2 %). Bounded above by Zhou, Ning
        /// and Hu 2021 (ISPIV 2021, Iowa State: a single 240 mm rotor at 4860 rpm, 3.0 N, σ 0.21 N = 7 % of the thrust over
        /// 0–1500 Hz on a JR3 load cell, the vibration included).</summary>
        public double ThrustSigma = 0.029;

        /// <summary>The first-order corner, f·R/v_h: 0.2 (published: the low band of a rotor's own wake unsteadiness near hover,
        /// Johnson 2005, NASA/TP-2005-213477, Table 2, as the vortex ring's fluctuation takes it; for the hover's wake assumed).
        /// At the hover's v_h, about 9.7 m/s, it is 15 Hz.</summary>
        public double CornerFrequency = 0.2;

        /// <summary>Its bound, over its σ: 3 (assumed, as the vortex ring's ±3σ).</summary>
        public double Bound = 3;

        /// <summary>The air's density, kg/m³ (drone.json's aero air_density_kg_m3, 1.225, published ISA).</summary>
        public double AirDensity = 1.225;

        /// <summary>The descent along the axis, over v_h, beyond which the own wake is held: 0.2 (published: Johnson 2005,
        /// NASA/TP-2005-213477, Table 4's VzD, where the measured inflow leaves momentum theory and the vortex ring's own
        /// increment begins; VortexRingSpec.ExitRate is the same point).</summary>
        public const double DescentHold = 0.2;

        public RotorWakeSpec Clone() => (RotorWakeSpec)MemberwiseClone();

        /// <summary>
        /// The wake's own induced velocity over v_h, v, at μ = V_∥/v_h in the disc's plane and the axial inflow a = V_a/v_h
        /// through it (positive as in a climb), a held at −DescentHold: Glauert's v·√(μ² + (a + v)²) = 1 (published). The
        /// wake's transport velocity is 1/v (v_h units). Newton's method from the root at the hold, the largest the law
        /// has, down the increasing, convex side: eight steps, converged to the last bit from six over μ 0–30 and a −3 to 10.
        /// </summary>
        public static double Induced(double mu, double axial)
        {
            double a = axial > -DescentHold ? axial : -DescentHold;
            double v = 0.5 * DescentHold + DetMath.Sqrt(0.25 * DescentHold * DescentHold + 1);
            for (int n = 0; n < 8; n++)
            {
                double u = a + v, s = DetMath.Sqrt(mu * mu + u * u);
                v -= (v * s - 1) / (s + v * u / s);
            }
            return v;
        }

        /// <summary>Model 90's fade (the kept audit's, Propulsion.HoverWakeLaw): Glauert's v_i/v_h at μ in the disc's plane,
        /// the axial flow aside.</summary>
        public static double Fade(double mu)
        {
            double m2 = mu * mu;
            return DetMath.Sqrt(0.5 * (DetMath.Sqrt(m2 * m2 + 4) - m2));
        }
    }
}
