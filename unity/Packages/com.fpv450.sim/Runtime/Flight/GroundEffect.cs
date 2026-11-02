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
    ///   step and the corner), bounded. It enters the thrust's and the torque's advance ratio as the rotor's own wake does
    ///   (RotorWakeSpec; D-064). Its σ and bound are thrust shares, which Propulsion turns into an inflow over v_h through
    ///   the props' static slope. Never on the pose.
    ///   Published (Dekker, Baars, Scarano, Tuinstra, Ragni, "Unsteady flow behaviour of multi-rotors in ground proximity",
    ///   Flow 3, E30, 2023, TU Delft; two 6-inch rotors one diameter apart, PIV): at H/R 2 the fountain rises above the discs
    ///   and is re-ingested, at H/R 4 it reaches 3 R and is not; the re-ingestion's time scale is the recirculation path's,
    ///   T_conv ≈ 0.1 s at H/R 2 (f_conv ≈ 10 Hz, spectral peaks at multiples of about 7 Hz), larger in magnitude the lower
    ///   the rotors. Healy, McCauley, Gandhi, Sahni and Mistry (77th VFS Forum 2021, computed; as Dekker et al. quote it):
    ///   when the fountain is near a rotor its thrust drops suddenly, the vibrations up to 10 %.
    /// </summary>
    public sealed class GroundEffectSpec
    {
        /// <summary>The rays' length over R: 8 (derived: Cheeseman and Bennett's gain is 0.1 % there, R/4z = 1/32).</summary>
        public double Reach = 8;

        /// <summary>The height over R below which both forms are held: 0.5 (assumed: the image model diverges at 0.25; the
        /// ground's gain there is 1.333, the ceiling's 1.11).</summary>
        public double LowestHeight = 0.5;

        /// <summary>The ceiling model's α₀: 1.60 (published: Hsiao and Chirarattananon's reduced model fitted to their
        /// measurements; on this prop, assumed).</summary>
        public double CeilingAlpha = 1.60;

        /// <summary>The height over R below which the wash re-ingests: 4.0 (published: Dekker et al. 2023, the fountain reaches
        /// 3 R at H/R 4, below the discs, and rises above them at H/R 2).</summary>
        public double RecirculationOnset = 4.0;

        /// <summary>The re-ingested wash's thrust σ, a share of the thrust, at RecirculationReference: 0.033 (derived from
        /// Healy et al. 2021's "up to 10 %" read as 3σ, assumed), rising linearly as the height falls from RecirculationOnset
        /// (0) and held below LowestHeight (assumed: the fountain's larger magnitude the lower the rotors, Dekker et al.).</summary>
        public double RecirculationSigma = 0.033;

        /// <summary>The height over R RecirculationSigma holds at: 2 (published: Dekker et al.'s and Healy et al.'s H/R 2).</summary>
        public double RecirculationReference = 2.0;

        /// <summary>Its bound, a share of the thrust: 0.17 (assumed: 3σ at LowestHeight, 3 × 0.033 × 3.5/2).</summary>
        public double RecirculationBound = 0.17;

        /// <summary>The wash's loop over (z + R), down to the ground, out, up and back in: 3.0 (derived from published: Dekker
        /// et al.'s T_conv 0.1 s at H/R 2 for their rotor, 2.14 N on R 76.2 mm, so v_h 6.92 m/s by momentum theory; 0.1·v_h/R
        /// is 9.1 over (z + R) = 3 R).</summary>
        public double RecirculationLoop = 3.0;

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

        /// <summary>The re-ingested wash's thrust σ, a share of the thrust, at height z over R.</summary>
        public double Sigma(double zOverR)
        {
            if (!(zOverR < RecirculationOnset)) return 0;
            double z = zOverR > LowestHeight ? zOverR : LowestHeight;
            return RecirculationSigma * (RecirculationOnset - z) / (RecirculationOnset - RecirculationReference);
        }
    }
}
