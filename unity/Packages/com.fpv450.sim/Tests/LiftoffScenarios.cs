using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The liftoff harness's flight (w9-village-physics 2.9, p7a-ground design 13): the session's airframe on a spot, its
    /// skids just over the ground and falling with the throttle cut (a vertical touch or the pilot's glide), 2 s at rest
    /// armed, then the pilot's gentle liftoff, read for a held leg. flightbench --ground flies it over Village's and House's
    /// grounds; the suite asserts single flights (LiftoffTests).
    /// </summary>
    public static class LiftoffScenarios
    {
        const double RadToDeg = 57.29577951308232;

        /// <summary>The ramp to the hover + RampAbove over RampSeconds, then the climb on at Climb of throttle a second until the
        /// drone is off, where the throttle stays (assumed protocol, design 2.9 §3: the pilot's own caution, "very sharp lift off"
        /// being a cause of a cut fiber, B 11).</summary>
        public const double RampAbove = 0.1, RampSeconds = 0.5, Climb = 0.2;

        /// <summary>Design 13's held leg: a corner over HeldLoad either way at least HeldTime after the others left, the body
        /// turning at least HeldTurn toward it (assumed, design 13: 0.5° is about six of the feed's luma samples).</summary>
        public const double HeldLoad = 1, HeldTime = 0.02, HeldTurn = 0.5;

        /// <summary>The descents of the two touches (design 13): vertical at 0.3 m/s; the glide 0.5 m/s down and 0.3 forward.</summary>
        public static readonly Double3 Vertical = new Double3(0, -0.3, 0), Glide = new Double3(0, -0.5, 0.3);

        public struct Liftoff
        {
            /// <summary>When each corner last carried more than HeldLoad either way (s from the liftoff's start; NaN never).</summary>
            public double[] Left;

            /// <summary>Each corner's deepest sink at rest before the liftoff, m.</summary>
            public double[] Sink;

            /// <summary>Design 13's lone held corner (−1 none): the hold past the third (s), the turn toward it (°), its largest
            /// load either way during the hold (N).</summary>
            public int Lone;
            public double LoneHold, LoneTurn, LonePeak;

            /// <summary>The held group (the pilot's "one or many of them"): how many corners left at least HeldTime after the
            /// rest with the body turning HeldTurn toward them (0 none), that largest gap (s) and the turn (°).</summary>
            public int Group;
            public double GroupHold, GroupTurn;

            /// <summary>When the drone was off (s from the liftoff's start; NaN if never) and the throttle then; the largest
            /// pull toward the ground on any corner (N); crashes and tunnels over the flight.</summary>
            public double Off, OffThrottle, Pull;
            public int Crashes, Tunnels;
        }

        /// <summary>The hover throttle at a spot (SoilScenarios.HoverThrottle's pilot, 2 s of a 1 m hover in still air).</summary>
        public static double Hover(DroneSpec spec, FlightWorld world, Double3 foot) => SoilScenarios.HoverThrottle(spec, world, foot);

        /// <summary>
        /// One flight at foot (on world's ground), its nose at yawDeg (0 north), the flight seed drawing the build's balance,
        /// the motors and the legs; the touch vertical or the glide (along the nose). table replaces surfaces.json (a known-wrong's
        /// soil); rigid flies without it, the plain-friction ground (μ 0.5/0.4); edit changes the setup (a comparison's).
        /// </summary>
        public static Liftoff Fly(DroneSpec spec, FlightWorld world, Double3 foot, double hover, RainLevel rain, ulong seed, bool glide,
            double yawDeg = 0, SurfaceTable table = null, bool rigid = false, Action<FlightSetup> edit = null)
        {
            double yaw = yawDeg / RadToDeg;
            var q = new Quat(0, Math.Sin(yaw / 2), 0, Math.Cos(yaw / 2));
            FlightSetup setup = SoilScenarios.Setup(spec, world, foot, rain, seed, table: table, rigid: rigid);
            FiberCutScenarios.SessionAirframe(spec, setup);
            setup.StartAttitude = q;
            edit?.Invoke(setup);
            setup.StartPosition = Over(setup, world, foot, q);
            Double3 v = glide ? Glide : Vertical;
            setup.StartVelocity = new Double3(Math.Sin(yaw) * v.Z, v.Y, Math.Cos(yaw) * v.Z);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var r = new Liftoff { Left = new double[4], Sink = new double[4], Lone = -1, Off = double.NaN };
            for (int k = 0; k < 2000; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true });
                Count(core, ref r);
            }
            Double3[] corner = new Double3[4];
            foreach (ContactShape s in core.Contacts.Spec.Shapes)
                if (s.Role == ContactRole.Skid) corner[s.Corner] = s.A;
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                r.Sink[c] = Math.Max(sk.Max0, Math.Max(sk.Max1, sk.Max2));
                r.Left[c] = double.NaN;
            }
            double rest = core.State.Position.Y;
            const int Steps = 4000;
            var lean = new double[Steps, 4];
            var load = new double[Steps, 4];
            int end = Steps;
            double top = hover + RampAbove, held = double.NaN;
            for (int k = 0; k < Steps; k++)
            {
                double t = k * FlightCore.Dt;
                double th = !double.IsNaN(held) ? held : t < RampSeconds ? t / RampSeconds * top : Math.Min(1, top + (t - RampSeconds) * Climb);
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = th });
                Count(core, ref r);
                bool any = false;
                for (int c = 0; c < 4; c++)
                {
                    lean[k, c] = Lean(core.State, corner[c]);
                    load[k, c] = core.Trace.LegLoad[c];
                    if (Math.Abs(load[k, c]) > HeldLoad)
                    {
                        r.Left[c] = (k + 1) * FlightCore.Dt;
                        any = true;
                    }
                    if (-load[k, c] > r.Pull) r.Pull = -load[k, c];
                }
                if (double.IsNaN(held) && !any && core.State.Position.Y - rest > 0.01)
                {
                    held = th;
                    r.Off = (k + 1) * FlightCore.Dt;
                    r.OffThrottle = th;
                    end = Math.Min(Steps, k + 300);
                }
                if (k + 1 >= end) break;
            }
            Judge(ref r, lean, load);
            return r;
        }

        // The body's start with the skids' lowest point 2 mm over the ground beneath it, so the touch keeps the descent the
        // pilot flew it at with the throttle cut.
        static Double3 Over(FlightSetup setup, FlightWorld world, Double3 foot, Quat q)
        {
            double lift = double.NegativeInfinity;
            foreach (ContactShape s in setup.Contacts.Shapes)
            {
                if (s.Role != ContactRole.Skid && s.Role != ContactRole.Support) continue;
                foreach (Double3 p in new[] { s.A, s.B })
                {
                    Double3 w = q.Rotate(p);
                    double ground = SlopeScenarios.Ground(world.Query, foot.X + w.X, foot.Z + w.Z).Height;
                    lift = Math.Max(lift, ground + s.Radius - w.Y - foot.Y);
                }
            }
            return foot + new Double3(0, lift + 0.002, 0);
        }

        /// <summary>
        /// The soft-soil landing (audit §1.2, 2.9's row): the drone level, its skids 2 mm over the ground at foot and coming down at
        /// descent m/s with the throttle cut (armed at idle), flown seconds; no draws (the nominal build). The deepest any node of
        /// a half-skid sank (m) and the deepest a node lies below the surface at the end (m).
        /// </summary>
        public static (double Peak, double After, int Tunnels) Drop(DroneSpec spec, FlightWorld world, Double3 foot, double descent, RainLevel rain, double seconds = 3)
        {
            FlightSetup setup = SoilScenarios.Setup(spec, world, foot, rain);
            setup.StartPosition = Over(setup, world, foot, Quat.Identity);
            setup.StartVelocity = new Double3(0, -descent, 0);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            double peak = 0;
            var r = new Liftoff();
            int steps = (int)(seconds / FlightCore.Dt + 0.5);
            for (int k = 0; k < steps; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true });
                Count(core, ref r);
                for (int c = 0; c < 4; c++)
                {
                    ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                    peak = Math.Max(peak, Math.Max(sk.Max0, Math.Max(sk.Max1, sk.Max2)));
                }
            }
            double rest = 0;
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                rest = Math.Max(rest, Math.Max(sk.Sink0, Math.Max(sk.Sink1, sk.Sink2)));
            }
            return (peak, rest, r.Tunnels);
        }

        static void Count(FlightCore core, ref Liftoff r)
        {
            for (int i = 0; i < core.Events.Count; i++)
            {
                if (core.Events[i].Kind == EventKind.Crash) r.Crashes++;
                if (core.Events[i].Kind == EventKind.Tunnel) r.Tunnels++;
            }
        }

        // The lone corner (design 13) and the group (the largest gap between the corners' last loads).
        static void Judge(ref Liftoff r, double[,] lean, double[,] load)
        {
            var order = new[] { 0, 1, 2, 3 };
            if (double.IsNaN(r.Off)) return; // never off: no reading
            var left = new double[4];
            for (int c = 0; c < 4; c++) left[c] = double.IsNaN(r.Left[c]) ? FlightCore.Dt : r.Left[c]; // carried nothing: off at once
            Array.Sort(order, (a, b) => left[a].CompareTo(left[b]));
            int Step(double t) => (int)(t / FlightCore.Dt + 0.5) - 1;
            // Lone: the last against the third.
            int last = order[3];
            int from = Step(left[order[2]]), to = Step(left[last]);
            double turn = 0, peak = 0;
            for (int k = from; k <= to; k++)
            {
                turn = Math.Max(turn, lean[k, last] - lean[from, last]);
                peak = Math.Max(peak, Math.Abs(load[k, last]));
            }
            r.LoneHold = left[last] - left[order[2]];
            r.LoneTurn = turn;
            r.LonePeak = peak;
            if (r.LoneHold >= HeldTime && r.LoneTurn >= HeldTurn) r.Lone = last;
            // Group: the largest gap; the late corners' mean lean toward them.
            int split = 0;
            double gap = -1;
            for (int i = 0; i < 3; i++)
                if (left[order[i + 1]] - left[order[i]] > gap)
                {
                    gap = left[order[i + 1]] - left[order[i]];
                    split = i;
                }
            from = Step(left[order[split]]);
            to = Step(left[order[split + 1]]);
            double gturn = 0;
            for (int k = from; k <= to; k++)
            {
                double m = 0;
                for (int i = split + 1; i < 4; i++) m += lean[k, order[i]] - lean[from, order[i]];
                gturn = Math.Max(gturn, m / (3 - split));
            }
            r.GroupHold = gap;
            r.GroupTurn = gturn;
            if (gap >= HeldTime && gturn >= HeldTurn) r.Group = 3 - split;
        }

        // How far the body's up leans toward a corner's side, deg: its world tilt along the corner's horizontal bearing.
        static double Lean(in FlightState s, Double3 corner)
        {
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0)), w = s.Attitude.Rotate(new Double3(corner.X, 0, corner.Z));
            w = new Double3(w.X, 0, w.Z);
            return Math.Asin(Double3.Dot(up, w) / w.Length()) * RadToDeg;
        }

        /// <summary>surfaces.json with every surface's dry set in rain too (the known-wrong "the dry rows in rain").</summary>
        public static SurfaceTable DryEverywhere()
        {
            SurfaceTable t = SoilScenarios.Table().Clone();
            for (int i = 0; i < t.Surfaces.Length; i++)
            {
                t.Surfaces[i].Wet = t.Surfaces[i].Dry;
                t.Surfaces[i].Saturated = t.Surfaces[i].Dry;
            }
            return t;
        }
    }
}
