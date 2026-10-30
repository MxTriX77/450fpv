using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// flight-dynamics' "A crosswind leans the drone" (p6-flight-core 1.5): the repository's drone at its nominal
    /// loadout, armed by Betaflight's rules and held at a point by the closed-loop pilot (SimPilot) in a wind, its lean
    /// and drift measured over the flight's last 10 s of 40. The suite asserts it (AeroTests); tools/flightbench --scenarios
    /// prints it. Also the cruise trim: the pitch that holds an airspeed in still air (clip P's pitch flown the other
    /// way round is WindHarness.HeldPitchAirspeed). Basic operations only in what the core is fed, so the flights give
    /// the same bits on every runtime.
    /// </summary>
    public static class CrosswindScenario
    {
        public const double Seconds = 40, MeasuredSeconds = 10;

        /// <summary>
        /// The lean per m/s a hover in a steady 5 m/s needs, degrees (derived, p7c-wind 1.6): the drag's bands over the
        /// weight at the nominal loadout, the rotors' in-plane drag 0.033–0.049 N per m/s (blade-element theory) and the
        /// silhouettes' C_d 0.8–1.05 (Hoerner), from a wind on the side (the smallest silhouette) to one on the diagonal
        /// (the side's and the front's at 45°, the largest). K§6.5's 0.95–2.6 is the lean at clip P's cruise, not in a hover.
        /// </summary>
        public const double HoverLeanLo = 0.32, HoverLeanHi = 0.56;

        /// <summary>The top of a turbulent field's hover lean per m/s of its mean (assumed: the gusts and the pilot's own
        /// motion raise the quadratic drag's mean up to 25 % above a steady wind's).</summary>
        public const double GustLeanHi = 1.25 * HoverLeanHi;

        /// <summary>The top of a turbulent field's hover lean per m/s at a mean of `speed` m/s (derived, p7c-wind 1.5): the
        /// silhouettes' drag is quadratic, so its lean per m/s grows with the speed, at most in proportion above the bands'
        /// 5 m/s (the rotors' linear in-plane drag keeps the bottom).</summary>
        public static double GustLeanHiAt(double speed) => GustLeanHi * System.Math.Max(1, speed / 5);
        const double RadToDeg = 57.29577951308232;

        public struct Result
        {
            /// <summary>The mean wind at the body over the measured window, world m/s, and its speed.</summary>
            public Double3 Wind;
            public double WindSpeed;

            /// <summary>The body's up axis from the vertical, degrees, mean over the window; per m/s of the wind.</summary>
            public double LeanDeg, LeanPerMs;

            /// <summary>The lean's bearing against the wind's source: 0 leans straight into the wind, degrees.</summary>
            public double LeanOffWindDeg;

            /// <summary>The position's mean and largest horizontal error over the window, m, and the mean drift, m/s.</summary>
            public double MeanError, MaxError, Drift;

            /// <summary>The air's horizontal force over the weight, mean over the window (tan of the lean it needs).</summary>
            public double AirOverWeight;
        }

        /// <summary>The flight's setup: the drone with its drag (or without, for a known-wrong model) in a wind.</summary>
        public static FlightSetup Setup(DroneSpec spec, WindSetup wind, ulong seed = 0x450F, bool drag = true, bool rotorDrag = true)
        {
            PropulsionSpec propulsion = spec.BuildPropulsion();
            if (!rotorDrag) propulsion.Props.HForce = 0;
            return new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(spec.Nominal), Propulsion = propulsion,
                Aero = drag ? spec.BuildAero() : null, Wind = wind, StartPosition = new Double3(0, 300, 0), StartPackCharge = 0.9,
            };
        }

        /// <summary>Arms by Betaflight's rules (the switch off, then on with the throttle low), 16 steps.</summary>
        public static void Arm(FlightCore core)
        {
            for (int k = 0; k < 16; k++)
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 });
        }

        /// <summary>Holds the start point for Seconds and measures the last MeasuredSeconds.</summary>
        public static Result Hold(FlightSetup setup)
        {
            var core = new FlightCore(setup);
            Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition) { PositionIntegral = 0.1 };
            double weight = core.Body.Mass * 9.80665;
            var r = new Result();
            Double3 up = default, wind = default, drift = default;
            double air = 0;
            int n = 0, steps = (int)(Seconds * 1000), from = (int)((Seconds - MeasuredSeconds) * 1000);
            for (int k = 0; k < steps; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                if (k < from) continue;
                ref readonly FlightState s = ref core.State;
                up = up + s.Attitude.Rotate(new Double3(0, 1, 0));
                wind = wind + core.Wind.At(s.Position);
                drift = drift + new Double3(s.Velocity.X, 0, s.Velocity.Z);
                Double3 a = core.Trace.AeroForce;
                air += DetMath.Sqrt(a.X * a.X + a.Z * a.Z) / weight;
                double ex = s.Position.X - setup.StartPosition.X, ez = s.Position.Z - setup.StartPosition.Z;
                double e = DetMath.Sqrt(ex * ex + ez * ez);
                r.MeanError += e;
                r.MaxError = DetMath.Max(r.MaxError, e);
                n++;
            }
            up = up * (1.0 / n);
            r.Wind = wind * (1.0 / n);
            r.WindSpeed = DetMath.Sqrt(r.Wind.X * r.Wind.X + r.Wind.Z * r.Wind.Z);
            r.MeanError /= n;
            drift = drift * (1.0 / n);
            r.Drift = DetMath.Sqrt(drift.X * drift.X + drift.Z * drift.Z);
            r.AirOverWeight = air / n;
            double horizontal = DetMath.Sqrt(up.X * up.X + up.Z * up.Z);
            r.LeanDeg = System.Math.Atan2(horizontal, up.Y) * RadToDeg;
            r.LeanPerMs = r.WindSpeed > 0 ? r.LeanDeg / r.WindSpeed : 0;
            // Into the wind: the up axis leans toward where the wind comes from, −W.
            if (horizontal > 0 && r.WindSpeed > 0)
            {
                double cos = -(up.X * r.Wind.X + up.Z * r.Wind.Z) / (horizontal * r.WindSpeed);
                r.LeanOffWindDeg = System.Math.Acos(System.Math.Min(1, System.Math.Max(-1, cos))) * RadToDeg;
            }
            return r;
        }

        /// <summary>The pitch, degrees, that holds an airspeed north in still air, mean over the last MeasuredSeconds.</summary>
        public static double CruisePitch(DroneSpec spec, double airspeed) => Cruise(spec, airspeed, 0, out _);

        /// <summary>
        /// The sideways lean, degrees, that holds a track north at a ground speed in a steady crosswind from the west,
        /// the nose kept on the track ("roll and keep direction", wind.md §5.5): wind.md §5.3 derives 23.4° over the
        /// airspeed per m/s, 1.8 at its 13 m/s and 1.24 at the cruise clip P's pitch flies (p7c-wind 1.6).
        /// </summary>
        public static double CruiseLean(DroneSpec spec, double speed, double crosswind)
        {
            Cruise(spec, speed, crosswind, out double lean);
            return lean;
        }

        // The pitch and the sideways lean (toward the west, into a wind from there), degrees, holding a ground velocity
        // north in a steady wind from the west.
        static double Cruise(DroneSpec spec, double airspeed, double crosswind, out double lean)
        {
            FlightSetup setup = Setup(spec, crosswind > 0 ? WindSetup.SteadyFrom(270, crosswind) : WindSetup.Of(WindLevel.Still));
            var core = new FlightCore(setup);
            Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition)
            {
                HoldVelocity = true, TargetVelocity = new Double3(0, 0, airspeed), MaxTiltSin = 0.8,
            };
            double pitch = 0;
            lean = 0;
            int n = 0, steps = (int)(Seconds * 1000), from = (int)((Seconds - MeasuredSeconds) * 1000);
            for (int k = 0; k < steps; k++)
            {
                StickSample sticks = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                core.Advance(sticks);
                // The height held too: the pilot's target follows the drone's path.
                pilot.Target = new Double3(core.State.Position.X, setup.StartPosition.Y, core.State.Position.Z);
                if (k < from) continue;
                Double3 up = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                pitch += System.Math.Atan2(up.Z, up.Y) * RadToDeg;
                lean += System.Math.Atan2(-up.X, up.Y) * RadToDeg;
                n++;
            }
            lean /= n;
            return pitch / n;
        }
    }
}
