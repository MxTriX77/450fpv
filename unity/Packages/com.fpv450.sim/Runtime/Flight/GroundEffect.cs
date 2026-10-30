namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Ground effect, ceiling effect and recirculation per rotor (p7b-airframe design 3, PHY-08), inside the inflow seam
    /// (Propulsion.RotorInflow), from each rotor's distances along its axis to the surface below and above it
    /// (FlightCore's rays down and up the axis, the one below combined with SampleGround's ground; none over a hole).
    /// - The ground: Cheeseman and Bennett's image model, T/T∞ = 1/(1 − (R/4z)²·v_h²/(v_h² + V²)), V the hub's airspeed in
    ///   the disc's plane and v_h = √(T/2ρA) at the rotor's present thrust (published: Cheeseman, I. C., Bennett, W. E.,
    ///   "The Effect of the Ground on a Helicopter Rotor in Forward Flight", ARC R&amp;M 3021, 1955, its hover and forward
    ///   flight forms; v_h for its v_i, assumed). Their ratio is at constant power; the rotor's torque at a speed is kept
    ///   (as the vortex ring keeps it), so at a fixed speed the power is fixed and the ratio is the thrust's gain there.
    /// - The ceiling: Hsiao and Chirarattananon's momentum model, γ = ½ + ½·√(1 + α₀·δ²/8), δ = R/z, the thrust at constant
    ///   power γ^(2/3) of its free value (published: Hsiao, Y. H., Chirarattananon, P., "Ceiling Effects for Hybrid
    ///   Aerial-Surface Locomotion of Small Rotorcraft", IEEE/ASME Trans. Mechatronics 24(5), 2019, arXiv:1905.04632: α₀ 1.60
    ///   for their props, measured at fixed drive, the rotor's speed nearly unchanged). Its use on a 10-inch prop, and the
    ///   forward-speed fade of the ground's form applied to it, are assumed.
    /// - Both fade to exactly 1 at Reach (each less its value there), so a rotor rising out of either never steps.
    /// - Recirculation: in a close hover the wash spreads on the ground and the multirotor's wall jets meet between the
    ///   rotors in a fountain, which the discs re-ingest; its mean is inside the measured ground effect above, its
    ///   unsteadiness is not. Each rotor's re-ingested inflow through its disc, x·v_h, is a first-order process about 0
    ///   (the fountain's upwash and the re-ingested downwash), with σ rising as the height falls, its time constant the
    ///   wash's loop, RecirculationLoop·(z + R)/v_h, its unit from the named stream "ground.recirc" (counter-based on the
    ///   step and the corner), bounded. It enters the thrust's advance ratio as the ring's inflow does. Never on the pose.
    /// </summary>
    public sealed class GroundEffectSpec
    {
        /// <summary>The rays' length over R: 8 (derived: Cheeseman and Bennett's gain is 0.1 % there, R/4z = 1/32).</summary>
        public double Reach = 8;

        /// <summary>The height over R below which both forms are held: 0.5 (assumed: the image model diverges at 0.25; the
        /// ground's gain there is 1.333, the ceiling's 1.07).</summary>
        public double LowestHeight = 0.5;

        /// <summary>The ceiling model's α₀: 1.60 (published: Hsiao and Chirarattananon's reduced model fitted to their
        /// measurements; on this prop, assumed).</summary>
        public double CeilingAlpha = 1.60;

        /// <summary>The height over R below which the wash re-ingests: 4.0 (assumed: two diameters, where the mean gain is
        /// down to 0.4 %; the wall jet and the fountain between the rotors reach the discs from about there).</summary>
        public double RecirculationOnset = 4.0;

        /// <summary>The re-ingested inflow's σ over v_h at LowestHeight, falling linearly to 0 at RecirculationOnset: 0.05
        /// (assumed: about 3.4 % of the thrust, a third of the vortex ring's measured σ).</summary>
        public double RecirculationSigma = 0.05;

        /// <summary>Its bound over v_h: 0.15 (assumed: 3σ at LowestHeight).</summary>
        public double RecirculationBound = 0.15;

        /// <summary>The wash's loop over (z + R), down to the ground, out, up and back in: 2 (assumed).</summary>
        public double RecirculationLoop = 2;

        /// <summary>The air's density, kg/m³ (drone.json's aero air_density_kg_m3, 1.225, published ISA).</summary>
        public double AirDensity = 1.225;

        public GroundEffectSpec Clone() => (GroundEffectSpec)MemberwiseClone();

        /// <summary>The ground's gain at height z over R with the forward fade f = v_h²/(v_h² + V²): Cheeseman and Bennett's,
        /// less its value at Reach; 1 at and beyond Reach (and for z +∞, no ground).</summary>
        public double GroundGain(double zOverR, double fade)
        {
            if (!(zOverR < Reach)) return 1;
            double z = zOverR > LowestHeight ? zOverR : LowestHeight;
            double a = 1 / (4 * z), r = 1 / (4 * Reach);
            return 1 / (1 - (a * a - r * r) * fade);
        }

        /// <summary>The ceiling's gain at height z over R with the forward fade f: Hsiao and Chirarattananon's γ^(2/3), γ less
        /// its value at Reach; 1 at and beyond Reach.</summary>
        public double CeilingGain(double zOverR, double fade)
        {
            if (!(zOverR < Reach)) return 1;
            double z = zOverR > LowestHeight ? zOverR : LowestHeight;
            double g = 1 + (Gamma(1 / z) - Gamma(1 / Reach)) * fade;
            return DetMath.Cbrt(g * g);
        }

        /// <summary>Hsiao and Chirarattananon's ceiling coefficient γ at δ = R/z.</summary>
        public double Gamma(double delta) => 0.5 + 0.5 * DetMath.Sqrt(1 + CeilingAlpha * delta * delta / 8);

        /// <summary>The re-ingested inflow's σ over v_h at height z over R.</summary>
        public double Sigma(double zOverR)
        {
            if (!(zOverR < RecirculationOnset)) return 0;
            double z = zOverR > LowestHeight ? zOverR : LowestHeight;
            return RecirculationSigma * (RecirculationOnset - z) / (RecirculationOnset - LowestHeight);
        }
    }
}
