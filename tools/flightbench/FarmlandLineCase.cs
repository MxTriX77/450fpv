using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The fiber over the farmland (w3-farmland-ground 2.1, PLT-04): a seeded flight lays its line along 1.6 km of route over
    // the farmland's hills (about 2 km paid out with the hanging span and the Severe wind's drift), then hovers 60 s, and the
    // step is timed against PLT-04's 300 µs at p99 with 0 B after its first step; a second flight of the same seed gives the
    // world queries' time in each step (TimedQuery) against the 60 µs line.
    // - The start: the village's track 60 m south-east of its centre, (552, 556), on dirt_track 23 m from any placement
    //   (assumed: the package holds no start point yet, D-092 only puts it at the village).
    // - The route, at 30 m over the ground (assumed: over the tallest crowns, so the drone itself never meets a tree): through
    //   the village's centre and west edge, north across the F6|F7 tree belt and west along it, south past F6 and over the
    //   F3|F5 and F4|F10 belts, then south-east into F11: 1.6 km at 12 m/s, the line laid behind the drone.
    // - The pilot's sessions' loadout (L1: 2 kg, legs on, Severe wind, Sunny) in the field's own Severe wind (the farmland has
    //   no wind obstacles until W7), the coil full, the scripted pilot (SimPilot) holding the route's speed and its height.
    // Its world is MapWorld.Load(root, "farmland"): once 1.2 loads the farmland's trees, bushes and ruins, the same flight's
    // line lies through them and this case times that (task 2.1's re-run after 1.2). Unpaced: the wind has no worker here.
    internal static class FarmlandLineCase
    {
        public const string Name = "farmland-line-step";

        const ulong Seed = 1;
        const double Height = 30, Speed = 12, Climb = 12, Hover = 60;

        static readonly XZ[] Route =
        {
            new XZ(552, 556), new XZ(512, 600), new XZ(450, 640), new XZ(430, 770), new XZ(250, 770), new XZ(130, 640),
            new XZ(130, 400), new XZ(300, 300), new XZ(600, 300), new XZ(700, 250), new XZ(720, 120),
        };

        public static bool Bench()
        {
            MapWorld map = MapWorld.Load(RangeScenarios.Root, "farmland");
            double length = 0;
            for (int i = 1; i < Route.Length; i++) length += Distance(Route[i - 1], Route[i]);
            double seconds = Climb + length / Speed + Hover;
            int steps = (int)(seconds * 1000);

            // The warm-up: the flight's first 10 s, so the timed flights' first steps run compiled code (the tiered JIT).
            Fly(map, 10000, false, out _, out _, out _, out _);

            long[] times = Fly(map, steps, false, out long allocated, out string flown, out bool whole, out _);
            long[] queries = Fly(map, steps, true, out _, out _, out _, out double calls);
            double us = 1e6 / Stopwatch.Frequency;
            int first = (int)(Climb * 1000), routeEnd = (int)((Climb + length / Speed) * 1000);
            string P(long[] x, int from, int to)
            {
                var part = new long[to - from];
                Array.Copy(x, from, part, 0, part.Length);
                Array.Sort(part);
                int n = part.Length;
                return $"p50 {part[n / 2] * us:F1} µs, p99 {part[n * 99 / 100] * us:F1} µs, max {part[n - 1] * us:F1} µs";
            }
            var all = new long[steps - 1];
            Array.Copy(times, 1, all, 0, all.Length);
            Array.Sort(all);
            double p99 = all[all.Length * 99 / 100] * us;
            var q = new long[steps - 1];
            Array.Copy(queries, 1, q, 0, q.Length);
            Array.Sort(q);
            double q99 = q[q.Length * 99 / 100] * us;
            bool ok = p99 <= 300 && allocated == 0 && whole;
            Console.WriteLine($"{Name}: the farmland ({map.PlacedObjects} placed objects), from the village's track along {length:F0} m of route at {Height:F0} m over "
                + $"the ground and {Speed:F0} m/s, then a {Hover:F0} s hover; 2.0 kg, legs on, Severe wind; {flown}. "
                + $"{steps} steps unpaced: all after the first p99 {p99:F1} µs; the climb {P(times, 1, first)}; the route {P(times, first, routeEnd)}; "
                + $"the hover {P(times, routeEnd, steps)}; world queries a step (the second flight) on the ground and in the climb {P(queries, 1, first)}, "
                + $"in all p50 {q[q.Length / 2] * us:F1} µs, p99 {q99:F1} µs, "
                + $"max {q[q.Length - 1] * us:F1} µs, {calls:F1} calls a step ({(q99 < 60 ? "under" : "over")} the 60 µs line); "
                + $"allocation after the first step {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B, the line whole and 1.5 km out, the drone up)");
            return ok;
        }

        // One flight of `steps`: each step's time (or, timed, its world queries' time), what the steps after the first
        // allocated, what the line and the drone did, whether the line is whole and 1.5 km out with the drone up, the world
        // calls a step.
        static long[] Fly(MapWorld map, int steps, bool timed, out long allocated, out string flown, out bool whole, out double calls)
        {
            FlightSetup setup = Setup(map, out Double3 foot, out double stand, out Double3 nose);
            TimedQuery clock = timed ? new TimedQuery(map) : null;
            if (timed) setup.World = new FlightWorld(clock);
            var core = new FlightCore(setup);
            long crashed = -1;
            var pilot = new SimPilot(core.Controller, foot + new Double3(0, stand, 0)) { Period = 1, Heading = nose };
            var times = new long[steps];
            var ground = new GroundSample[1];
            allocated = 0;
            for (int k = 0; k < steps; k++)
            {
                StickSample stick = Sticks(pilot, core, map, ground, foot, stand, k * 0.001, core.State.Step * FlightCore.StepUs);
                long a = GC.GetAllocatedBytesForCurrentThread(), q = timed ? clock.Ticks : 0, t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long dt = Stopwatch.GetTimestamp() - t0;
                if (k >= 1) allocated += GC.GetAllocatedBytesForCurrentThread() - a;
                times[k] = timed ? clock.Ticks - q : dt;
                if (crashed < 0 && core.State.Crashed) crashed = core.State.Step;
            }
            calls = timed ? (double)clock.Calls / steps : 0;
            FiberLine line = core.Tether;
            whole = !line.Broken && crashed < 0 && line.PaidOut >= 1500;
            flown = $"the line {line.PaidOut:F1} m paid out, {line.Chain.Count} nodes, {line.TrailCount} trail points"
                + (line.Broken ? $", broken at step {line.BrokenAt} ({line.LastBreak.Mechanism})" : "")
                + (crashed >= 0 ? $", the drone crashed at step {crashed}" : "")
                + $"; its whole shape queries {line.Contacts.QueriesTotal}, its one-shape checks {line.Contacts.ChecksTotal}"
                + Buried(map, line.Chain);
            core.Wind.Dispose();
            return times;
        }

        // The chain's segments over the terrain at the end (w3-farmland-ground 2.2): a node takes the ground from SampleGround and
        // a segment's query leaves it out (2.1), so a straight segment between two laid nodes may cut through a rise between them.
        // The deepest any point of a segment (every 0.5 m) lies under the ground, and how many segments do so by more than 1 cm.
        static string Buried(MapWorld map, CableChain chain)
        {
            var at = new XZ[1];
            var g = new GroundSample[1];
            double deepest = 0, longest = 0;
            int buried = 0;
            for (int j = 0; j + 1 < chain.Count; j++)
            {
                Double3 a = chain[j].Position, b = chain[j + 1].Position;
                double length = (b - a).Length(), deep = 0;
                longest = Math.Max(longest, length);
                int n = Math.Max(2, (int)(length / 0.5));
                for (int i = 1; i < n; i++)
                {
                    Double3 x = a + (b - a) * ((double)i / n);
                    at[0] = new XZ(x.X, x.Z);
                    map.SampleGround(at, g);
                    deep = Math.Max(deep, g[0].Height - x.Y);
                }
                if (deep > 0.01) buried++;
                deepest = Math.Max(deepest, deep);
            }
            return $"; at the end {buried} of its {chain.Count - 1} segments (the longest {longest:F0} m) cut more than 1 cm under the ground, the deepest {deepest:F2} m";
        }

        static FlightSetup Setup(MapWorld map, out Double3 foot, out double stand, out Double3 nose)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(Path.Combine(RangeScenarios.Root, SurfaceTable.RelativePath)));
            var loadout = new Loadout { Payload = 2.0, Legs = true, CoilPaidOut = 0 };
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash, surfaces);
            stand = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support) stand = Math.Max(stand, s.Radius - Math.Min(s.A.Y, s.B.Y));
            var ground = new GroundSample[1];
            map.SampleGround(new[] { Route[0] }, ground);
            foot = new Double3(Route[0].X, ground[0].Height, Route[0].Z);
            double dx = Route[1].X - Route[0].X, dz = Route[1].Z - Route[0].Z, d = Math.Sqrt(dx * dx + dz * dz);
            nose = new Double3(dx / d, 0, dz / d);
            double heading = Math.Atan2(dx, dz); // D-019: positive yaw turns north towards east
            // Set down on the track's facet (5.4° there), its skids on it and 10 µm in (w3-farmland-ground 2.2): set level, its
            // supports stood up to 23 mm in the ground, two legs broke at the first step and two by 0.45 s, and the line flew
            // without legs while their four pieces rolled on the terrain, the climb's world-query cost.
            Double3 up = ground[0].Normal;
            return new FlightSetup
            {
                Seed = Seed,
                World = new FlightWorld(map),
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.Of(WindLevel.Severe),
                Rain = RainLevel.Sunny,
                CameraPosition = spec.CameraPosition,
                Contacts = contacts,
                Asymmetry = spec.BuildAsymmetry(),
                Placement = spec.BuildPlacement(),
                VortexRing = spec.BuildVortexRing(),
                GroundEffect = spec.BuildGroundEffect(),
                RotorWake = spec.BuildRotorWake(),
                Flapping = spec.BuildFlapping(),
                Interference = spec.BuildInterference(),
                Modes = spec.BuildModes(),
                StartPosition = foot + up * (stand - 1e-5),
                StartAttitude = SlopeScenarios.Aligned(up, heading * 180 / Math.PI),
                Tether = TetherTable.Parse(File.ReadAllText(Path.Combine(RangeScenarios.Root, TetherTable.RelativePath))),
                TetherCoilPart = spec.Coil.Part,
                TetherSpoolMass = spec.Coil.SpoolMass,
            };
        }

        // The sticks at t s: armed on the ground, the climb over the start, the route at its speed (the route's point at
        // that time pulled back onto, its height over the ground there), then the hover over the route's end.
        static StickSample Sticks(SimPilot p, FlightCore core, MapWorld map, GroundSample[] ground, Double3 foot, double stand, double t, long hostUs)
        {
            if (t < 0.5) return new StickSample { HostUs = hostUs, Arm = t >= 0.2 };
            if (t < Climb)
            {
                p.HoldVelocity = false;
                p.Target = foot + new Double3(0, stand + Math.Min(Height, 3 * (t - 0.5)), 0);
            }
            else
            {
                double s = (t - Climb) * Speed;
                int leg = 1;
                while (leg < Route.Length - 1 && s > Distance(Route[leg - 1], Route[leg]))
                {
                    s -= Distance(Route[leg - 1], Route[leg]);
                    leg++;
                }
                XZ a = Route[leg - 1], b = Route[leg];
                double l = Distance(a, b), f = Math.Min(1, s / l);
                var at = new XZ(a.X + (b.X - a.X) * f, a.Z + (b.Z - a.Z) * f);
                Here[0] = at;
                map.SampleGround(Here, ground);
                p.Target = new Double3(at.X, ground[0].Height + Height, at.Z);
                ref readonly FlightState state = ref core.State;
                if (f < 1)
                {
                    // The route's speed along the leg, with a pull back onto it (0.5 /s).
                    p.HoldVelocity = true;
                    p.TargetVelocity = new Double3((b.X - a.X) / l * Speed + 0.5 * (at.X - state.Position.X), 0,
                        (b.Z - a.Z) / l * Speed + 0.5 * (at.Z - state.Position.Z));
                }
                else p.HoldVelocity = false;
            }
            StickSample k = p.Sticks(core, hostUs);
            k.Roll = Axis(k.Roll);
            k.Pitch = Axis(k.Pitch);
            k.Yaw = Axis(k.Yaw);
            k.Throttle = Math.Round(Math.Clamp(k.Throttle, 0, 1) * 1000) / 1000;
            return k;
        }

        static readonly XZ[] Here = new XZ[1];

        static double Axis(double v) => Math.Round(Math.Clamp(v, -1, 1) * 500) / 500;

        static double Distance(XZ a, XZ b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
    }
}
