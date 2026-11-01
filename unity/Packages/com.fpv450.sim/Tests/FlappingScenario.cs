using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>A hover in still air, then a steady wind steps in (WindSetup.SteadyOnsetSeconds): the airframe's roll and pitch
    /// response with the flapping on and off, the pilot's sticks centred (the throttle the sim pilot's), no asymmetry.</summary>
    public static class FlappingScenario
    {
        public const double Onset = 3, Gust = 4, Window = 0.5;
        const double RadToDeg = 57.29577951308232;

        public static readonly double[] AdvanceRatios = { 0.05, 0.10, 0.20 };

        // A constant-chord, linearly twisted blade from the hub to the tip, the blade Johnson's closed forms are written for.
        public const double Chord = 0.02, Theta0 = 0.30, ThetaTw = -0.10, Radius = 0.127, Inertia = 4.3e-5, Rho = 1.225, Slope = 5.7;
        public const double Speed = 942, Thrust = 12;

        public static RotorFlappingSpec Johnsons(double bendingHz, double southwell) => new RotorFlappingSpec
        {
            LiftSlope = Slope, AirDensity = Rho, Radius = Radius, Blades = 2, BladeInertia = Inertia, BendingFrequency = bendingHz,
            Southwell = southwell, Stations = new[] { 0.0, 1.0 }, Chords = new[] { Chord, Chord }, Angles = new[] { Theta0, Theta0 + ThetaTw },
        };

        /// <summary>
        /// The published forms (Johnson, Helicopter Theory, 1980, §5.2–5.4: the flap equation's harmonics in forward flight on
        /// a spring-restrained centre hinge), written out for the constant-chord blade: γ = ρ·a·c·R⁴/I_b,
        /// ν²·β₀ = γ[θ₀(1 + μ²)/8 + θ_tw(1 + 5μ²/6)/10 − λ/6],
        /// (ν² − 1)·β₁c = −(γ/8)[λ_x + 4μβ₀/3 + (1 + μ²/2)·β₁s], (ν² − 1)·β₁s = (γ/8)[μ(8θ₀/3 + 2θ_tw − 2λ) + (1 − μ²/2)·β₁c],
        /// solved by Cramer's rule; the hub moment's harmonics (N_b/2)·I_b·Ω²·(ν² − 1)·β₁ per blade pair. At ν = 1 the
        /// articulated forms, β₁c = −μ(8θ₀/3 + 2θ_tw − 2λ)/(1 − μ²/2), β₁s = −(4μβ₀/3 + λ_x)/(1 + μ²/2).
        /// </summary>
        public static void Published(double nu2, double mu, double lambda, double lx, out double b0, out double b1c, out double b1s)
        {
            double gamma = Rho * Slope * Chord * Radius * Radius * Radius * Radius / Inertia, g8 = gamma / 8, k = nu2 - 1;
            b0 = gamma * (Theta0 * (1 + mu * mu) / 8 + ThetaTw * (1 + 5 * mu * mu / 6) / 10 - lambda / 6) / nu2;
            double fc = -g8 * (lx + 4 * mu * b0 / 3), fs = g8 * mu * (8 * Theta0 / 3 + 2 * ThetaTw - 2 * lambda);
            double dc = g8 * (1 + mu * mu / 2), ds = g8 * (1 - mu * mu / 2);
            // k·β₁c + dc·β₁s = fc; −ds·β₁c + k·β₁s = fs.
            double det = k * k + dc * ds;
            b1c = (k * fc - dc * fs) / det;
            b1s = (k * fs + ds * fc) / det;
        }

        // The inflow the solver used at μ (its Glauert induced velocity and Pitt and Peters' skew), so the forms see the same λ.
        public static void Inflow(RotorFlappingSpec f, double mu, out double lambda, out double lx)
        {
            double vt = Speed * Radius, vi = f.InducedVelocity(mu * vt, 0, Thrust);
            lambda = vi / vt;
            double root = System.Math.Sqrt(mu * mu + lambda * lambda) + lambda;
            lx = f.SkewGain * (mu / root) * lambda;
        }

        /// <summary>The worst relative error of the solver's hub moments (roll and pitch) against Johnson's forms over the three
        /// advance ratios on the hingeless (150 Hz) blade; with zero, of a rotor without flapping (model 10's: none).</summary>
        public static double WorstError(bool zero)
        {
            RotorFlappingSpec f = Johnsons(150, 1.193);
            double w0 = 2 * System.Math.PI * 150, ib2 = Inertia * Speed * Speed, nu2 = 1 + Inertia * (w0 * w0 + 0.193 * Speed * Speed) / ib2;
            double worst = 0;
            foreach (double mu in AdvanceRatios)
            {
                Inflow(f, mu, out double lambda, out double lx);
                Published(nu2, mu, lambda, lx, out _, out double b1c, out double b1s);
                f.Solve(Speed, mu * Speed * Radius, 0, Thrust, out double m1c, out double m1s, out _, out _, out _);
                if (zero) m1c = m1s = 0;
                double kb = ib2 * (nu2 - 1);
                worst = System.Math.Max(worst, System.Math.Abs(m1c - kb * b1c) / System.Math.Abs(kb * b1c));
                worst = System.Math.Max(worst, System.Math.Abs(m1s - kb * b1s) / System.Math.Abs(kb * b1s));
            }
            return worst;
        }


        public struct Response
        {
            /// <summary>The peak roll rate (about the nose) and pitch rate (about the right side) after the step, °/s, the
            /// times they came, s after it, and the bank and pitch angles' change over the window, °.</summary>
            public double RollPeak, PitchPeak, RollAt, PitchAt, RollDeg, PitchDeg;
        }

        public static Response GustStep(DroneSpec spec, double fromDeg, bool flapping)
        {
            var wind = new WindSetup { Level = WindLevel.Steady, SteadySpeed = Gust, SteadyFromDeg = fromDeg, SteadyOnsetSeconds = Onset };
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, 0x450F);
            if (flapping) setup.Flapping = spec.BuildFlapping();
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            var r = new Response();
            double roll0 = 0, pitch0 = 0;
            int onset = (int)(Onset * 1000), end = onset + (int)(Window * 1000);
            while (core.State.Step < end)
            {
                StickSample sticks = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                sticks.Roll = sticks.Pitch = sticks.Yaw = 0;
                core.Advance(sticks);
                ref readonly FlightState s = ref core.State;
                Angles(s, out double roll, out double pitch);
                if (s.Step == onset)
                {
                    roll0 = roll;
                    pitch0 = pitch;
                }
                if (s.Step <= onset) continue;
                double t = (s.Step - onset) * FlightCore.Dt;
                double rr = System.Math.Abs(s.BodyRate.Z) * RadToDeg, pr = System.Math.Abs(s.BodyRate.X) * RadToDeg;
                if (rr > r.RollPeak) { r.RollPeak = rr; r.RollAt = t; }
                if (pr > r.PitchPeak) { r.PitchPeak = pr; r.PitchAt = t; }
                r.RollDeg = roll - roll0;
                r.PitchDeg = pitch - pitch0;
            }
            return r;
        }

        static void Angles(in FlightState s, out double roll, out double pitch)
        {
            Double3 right = s.Attitude.Rotate(new Double3(1, 0, 0)), nose = s.Attitude.Rotate(new Double3(0, 0, 1));
            roll = System.Math.Asin(right.Y) * RadToDeg;
            pitch = System.Math.Asin(nose.Y) * RadToDeg;
        }

        public static string Describe(double fromDeg, in Response on, in Response off) =>
            $"a {Gust} m/s gust from {fromDeg:F0} deg at hover: with flapping roll {on.RollPeak:F2} deg/s at +{on.RollAt * 1000:F0} ms, pitch "
            + $"{on.PitchPeak:F2} deg/s at +{on.PitchAt * 1000:F0} ms, over {Window} s bank {on.RollDeg:F3} deg, pitch {on.PitchDeg:F3} deg; without roll "
            + $"{off.RollPeak:F2} deg/s at +{off.RollAt * 1000:F0} ms, pitch {off.PitchPeak:F2} deg/s at +{off.PitchAt * 1000:F0} ms, bank "
            + $"{off.RollDeg:F3} deg, pitch {off.PitchDeg:F3} deg";
    }
}
