using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "The rotors in each other's wake" (p7b-airframe 1.3d): the harness pilot holds a pitch, a heading
    // north and 35 m in still air with the whole airframe, released at a speed (a hover: SimPilot holds the point); over the
    // run's last MeasuredSeconds the
    // scenario reads each rotor's added inflow from the others' wakes, its share of the thrust that inflow takes at the
    // rotor's own speed (the CFD's "against an isolated rotor at the same rpm"), the rotors' speeds, the wake's skew and
    // the airspeed.
    public static class RotorWakeScenario
    {
        const double Height = 35, Seconds = 30, MeasuredSeconds = 10;
        const double DegToRad = 0.017453292519943295, RadToDeg = 57.29577951308232;

        public struct Result
        {
            public double Airspeed, PitchDeg;

            /// <summary>Per corner (rear right, front right, rear left, front left): the added inflow, m/s; the thrust it takes
            /// at the rotor's speed, a share; the rotor's speed, rad/s; the induced velocity, m/s; the wake's skew, degrees.</summary>
            public Corners Inflow, Loss, Speed, Induced, SkewDeg;

            /// <summary>The rear pair's and the front pair's mean thrust loss at their speeds, shares.</summary>
            public double RearLoss => 0.5 * (Loss[0] + Loss[2]);
            public double FrontLoss => 0.5 * (Loss[1] + Loss[3]);

            /// <summary>How far the rear pair's thrust at its speed falls under the front pair's, a share (Nguyen, Liu and
            /// Mori's measure for tandem rotors: the rear rotor's C_T against the front's).</summary>
            public double RearUnderFront => 1 - (1 - RearLoss) / (1 - FrontLoss);
        }

        /// <summary>The published order of the rear rotors' loss in forward flight at 5–15 m/s, other frames (RotorWakeSpec's
        /// sources: 11 % Ye et al., 19 % Misiorowski et al., at most 11 % rear under front Nguyen et al.): 2–20 %.</summary>
        public const double LeastLoss = 0.02, MostLoss = 0.20;

        /// <summary>The flights it is checked at: 10 m/s at 6° nose down and 15 m/s at 12° (each the still-air trim, derived
        /// from the cruise's drag) and the slow flight of 2 m/s at 1°.</summary>
        public static readonly (double Pitch, double Speed)[] Forward = { (-6, 10), (-12, 15) };
        public const double SlowPitch = -1, SlowSpeed = 2;

        /// <summary>One line of a reading.</summary>
        public static string Describe(in Result r) =>
            $"{r.Airspeed:F1} m/s at {r.PitchDeg:F1}°: rear {100 * r.RearUnderFront:F1} % under front (rear {100 * r.RearLoss:F1} %, front "
            + $"{100 * r.FrontLoss:F1} % of an isolated rotor's at its speed), added inflow rear {0.5 * (r.Inflow[0] + r.Inflow[2]):F2}, front "
            + $"{0.5 * (r.Inflow[1] + r.Inflow[3]):F2} m/s, v_i {r.Induced[1]:F2} m/s, skew {r.SkewDeg[1]:F1}°";

        /// <summary>Flies the held pitch (degrees, nose down negative) released at a speed (m/s), with or without the wake.</summary>
        public static Result Cruise(DroneSpec spec, double pitchDeg, double releaseSpeed, bool wake = true)
        {
            FlightSetup setup = WindHarness.Setup(spec, WindLevel.Still, 1, Height, belt: false, drag: true, out _, out _, wake: wake);
            setup.StartVelocity = new Double3(0, 0, releaseSpeed);
            DetMath.SinCos(pitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(sp, 0, 0, cp);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var p = new HarnessPilot(core.Controller) { Course = 0, Height = Height, PitchDeg = pitchDeg };
            long release = core.State.Step;
            int steps = (int)(Seconds * 1000), from = (int)((Seconds - MeasuredSeconds) * 1000), frame = 0, n = 0, frames = 0;
            var r = new Result();
            for (int k = 0; k < steps; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0)
                {
                    HarnessPilot.Frame f = HarnessPilot.Take(core.State, k, 0);
                    p.See(f);
                    frame++;
                    if (k >= from)
                    {
                        r.PitchDeg += f.Pitch * RadToDeg;
                        frames++;
                    }
                }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
                if (k >= from) Add(core, ref r, ref n);
            }
            r.PitchDeg /= frames;
            return Mean(r, n);
        }

        /// <summary>Holds a hover at 35 m in still air with SimPilot (the harness pilot flies a course, not a point), with or
        /// without the wake.</summary>
        public static Result Hover(DroneSpec spec, bool wake = true)
        {
            FlightSetup setup = WindHarness.Setup(spec, WindLevel.Still, 1, Height, belt: false, drag: true, out _, out _, wake: wake);
            setup.StartVelocity = default;
            setup.StartAttitude = Quat.Identity;
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition) { PositionIntegral = 0.1 };
            int steps = (int)(Seconds * 1000), from = (int)((Seconds - MeasuredSeconds) * 1000), n = 0;
            var r = new Result();
            for (int k = 0; k < steps; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                if (k >= from) Add(core, ref r, ref n);
            }
            return Mean(r, n);
        }

        static void Add(FlightCore core, ref Result r, ref int n)
        {
            Propulsion prop = core.Propulsion;
            for (int i = 0; i < 4; i++)
            {
                double w = core.State.RotorSpeed[i], added = core.Trace.WakeInflow[i];
                prop.Aero(w, prop.Inflow[i] - added, out double free, out _);
                prop.Aero(w, prop.Inflow[i], out double taken, out _);
                r.Inflow[i] += added;
                r.Loss[i] += free > 0 ? 1 - taken / free : 0;
                r.Speed[i] += w;
                r.Induced[i] += prop.WakeInduced(i);
                r.SkewDeg[i] += 2 * Atan(prop.WakeSkew(i)) * RadToDeg;
            }
            r.Airspeed += core.State.Velocity.Length();
            n++;
        }

        static Result Mean(Result r, int n)
        {
            for (int i = 0; i < 4; i++)
            {
                r.Inflow[i] /= n;
                r.Loss[i] /= n;
                r.Speed[i] /= n;
                r.Induced[i] /= n;
                r.SkewDeg[i] /= n;
            }
            r.Airspeed /= n;
            return r;
        }

        // atan for |x| ≤ 1 by its series after three half-angle passes (a reading only, not the step).
        static double Atan(double x)
        {
            for (int n = 0; n < 3; n++) x = x / (1 + DetMath.Sqrt(1 + x * x));
            double x2 = x * x, s = 0, p = x;
            for (int n = 0; n < 12; n++)
            {
                s += (n % 2 == 0 ? p : -p) / (2 * n + 1);
                p *= x2;
            }
            return 8 * s;
        }
    }
}
