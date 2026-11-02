namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Each rotor's own wake unsteadiness in free air (D-064, PHY-07/08's "never perfectly still"; p7b-airframe design 3d),
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
    /// - **Its band** (CornerFrequency): a first-order corner at f·R/v_h.
    /// - **Forward flight** fades it with the wake's own induced velocity, v_i/v_h from Glauert's momentum theory at
    ///   μ = V/v_h (published), v_i⁴ + μ²·v_i² = 1 in v_h units: the wake that makes it is swept off the disc.
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

        public RotorWakeSpec Clone() => (RotorWakeSpec)MemberwiseClone();

        /// <summary>The wake's own induced velocity over v_h at μ = V/v_h in the disc's plane (Glauert, axial flow aside).</summary>
        public static double Fade(double mu)
        {
            double m2 = mu * mu;
            return DetMath.Sqrt(0.5 * (DetMath.Sqrt(m2 * m2 + 4) - m2));
        }
    }
}
