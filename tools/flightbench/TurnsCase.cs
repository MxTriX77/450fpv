using System;
using System.Collections.Generic;
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
    // Turning routes low over the farmland's bushes (w3-farmland-ground 2.4; the game developer's hills routes, which were not
    // kept, flew so and broke their lines on bushes inside the turns): each route climbs from rest at its first point to
    // Height over the ground, flies its legs at Speed with the nose along them (the scripted pilot, SimPilot), turning 73–125°
    // at each corner, and hovers Rest seconds over its end. Every route keeps 17 m from every tree, ruin and piece of debris
    // (measured from the placements' positions: 19.8–28.6 m) and passes within 15 m of 43–66 bushes; "hills" turns round
    // LeafyBush_P2 1412 23 m inside its second corner, where one of those routes broke at 90.65 s. The loadout is the hills
    // flight's (1 kg, legs on, Sunny), in the wind given (`--turns [calm | windy | severe]`, Windy the hills flight's).
    // Printed for each: the line whole or where it broke (and on what), the drone, the line paid out, the chain at the end (its
    // nodes and the line it spans from the trail's end to the exit) and the trail's points (the laid line frozen into it), how
    // far the laid line moved after it touched the ground (5 m marks of line, laid at least 10 s before the end), the speeds of
    // the chain's laid nodes (more than 30 m of line from the exit and within 0.1 m of the ground; a node at rest leaves the
    // chain for the trail), the step's time, the section table's use and the world bugs. It fails if a line breaks, a drone
    // crashes, a step allocates, or after the hover more than Live metres of line are still in the chain (the hanging span at
    // Height is about 30 m): the laid line must rest in the trail. How far a sharp turn drags the line's last corner is printed,
    // not judged: the spool's peel tension sets it (w3-farmland-ground design, 2.4).
    internal static class TurnsCase
    {
        internal const double Height = 5, Speed = 6.5, Climb = 8, Rest = 20, Live = 60, Mark = 5;
        const ulong Seed = 1;

        internal static readonly (string Name, XZ[] Points)[] Routes =
        {
            ("hills", new[] { new XZ(511, 459), new XZ(466, 327), new XZ(378, 369), new XZ(326, 312), new XZ(267, 369), new XZ(182, 284) }),
            ("west", new[] { new XZ(237, 278), new XZ(165, 380), new XZ(270, 466), new XZ(331, 359), new XZ(212, 296), new XZ(258, 183) }),
            ("east", new[] { new XZ(932, 603), new XZ(945, 516), new XZ(840, 473), new XZ(865, 584), new XZ(778, 567), new XZ(739, 653) }),
            ("north", new[] { new XZ(437, 867), new XZ(511, 838), new XZ(501, 932), new XZ(417, 896), new XZ(462, 769), new XZ(362, 698) }),
        };

        public static bool Run(WindLevel wind)
        {
            MapWorld map = MapWorld.Load(RangeScenarios.Root, "farmland");
            Console.WriteLine($"--turns: {Routes.Length} routes at {Height:F0} m over the farmland's ground and {Speed} m/s, 1 kg, legs on, {wind} wind, Sunny; "
                + Power.Describe());
            bool ok = true;
            foreach ((string name, XZ[] points) in Routes) ok &= Fly(map, name, points, wind);
            Console.WriteLine($"--turns: {(ok ? "PASS" : "FAIL")} (every line whole, every drone up, 0 B, and after the hover the line at rest in the trail but for at most {Live:F0} m of it)");
            return ok;
        }

        static bool Fly(MapWorld map, string name, XZ[] route, WindLevel wind)
        {
            double length = 0;
            for (int i = 1; i < route.Length; i++) length += Distance(route[i - 1], route[i]);
            int steps = (int)((Climb + length / Speed + Rest) * 1000), first = (int)(Climb * 1000);
            FlightSetup setup = Setup(map, route, wind, out Double3 foot, out double stand);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, foot + new Double3(0, stand, 0)) { Period = 1, Heading = Bearing(route[0], route[1]) };
            var ground = new GroundSample[1];
            var times = new long[steps];
            long allocated = 0, crashed = -1;
            FiberLine line = core.Tether;
            int marks = (int)(line.Chain[line.Chain.Count - 1].Material / Mark) + 4000;
            var laidAt = new Double3[marks];
            var laidWhen = new double[marks];
            for (int k = 0; k < marks; k++) laidWhen[k] = double.NaN;
            var speeds = new List<double>();
            int trailMost = 0;
            var bugs = new int[16];
            for (int k = 0; k < steps; k++)
            {
                StickSample stick = Sticks(pilot, core, map, ground, route, foot, stand, k * 0.001, core.State.Step * FlightCore.StepUs);
                long a = AllocProbe.Before(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                times[k] = Stopwatch.GetTimestamp() - t0;
                if (k >= 1) allocated += AllocProbe.After(a);
                if (crashed < 0 && core.State.Crashed) crashed = core.State.Step;
                for (int e = 0; e < core.Events.Count; e++)
                    if (core.Events[e].Kind == EventKind.WorldBug && core.Events[e].Index < bugs.Length) bugs[core.Events[e].Index]++;
                trailMost = Math.Max(trailMost, line.TrailCount);
                if (k % 10 == 9 && !line.Broken) Sample(map, line, speeds);
                if (k % 100 == 99) Lay(map, line, k * 0.001, laidAt, laidWhen);
            }
            core.Wind.Dispose();

            // How far each 5 m mark of line laid at least 10 s before the end moved from where it first lay.
            double end = steps * 0.001, most = 0, mostAt = 0;
            var moved = new List<double>();
            for (int k = 0; k < marks; k++)
            {
                if (!(laidWhen[k] <= end - 10) || !Where(line, k * Mark, out Double3 x)) continue;
                double d = Math.Sqrt((x.X - laidAt[k].X) * (x.X - laidAt[k].X) + (x.Z - laidAt[k].Z) * (x.Z - laidAt[k].Z));
                moved.Add(d);
                if (d > most)
                {
                    most = d;
                    mostAt = k * Mark;
                }
            }
            moved.Sort();
            speeds.Sort();
            double us = 1e6 / Stopwatch.Frequency;
            var flight = new long[steps - first];
            Array.Copy(times, first, flight, 0, flight.Length);
            Array.Sort(flight);
            string breakText = "whole";
            if (line.Broken)
            {
                FiberBreak b = line.LastBreak;
                var kind = (FiberMember)b.Kind;
                string on = kind == FiberMember.Shape || kind == FiberMember.Snag || kind == FiberMember.Layer
                    ? " on " + (map.ObjectName((int)(uint)(b.Member >> 32)) ?? "?") : "";
                breakText = $"broken at {line.BrokenAt * 0.001:F2} s, {b.Mechanism} at a {kind}{on}, {b.Tension:F3} N, R_eff {b.Radius * 100:F2} cm, "
                    + $"{b.Material - line.Chain[0].Material:F0} m of line from the chain's end";
            }
            // At rest at the end: the line in the chain (from its node 0, the trail's end, to the exit) no more than Live metres.
            double live = line.Chain[line.Chain.Count - 1].Material - line.Chain[0].Material;
            bool whole = !line.Broken && crashed < 0, still = live <= Live && allocated == 0;

            int far = speeds.Count, resting = 0;
            foreach (double v in speeds) if (v < 0.005) resting++;
            Console.WriteLine($"  {name}: {length:F0} m of route; the line {breakText}{(crashed >= 0 ? $", the drone crashed at {crashed * 0.001:F2} s" : ", the drone up")}; "
                + $"{line.PaidOut:F0} m paid out; at the end {line.Chain.Count} nodes over {live:F1} m of line and {line.TrailCount} trail points (at most {trailMost}); "
                + $"the laid line's {moved.Count} marks moved {(moved.Count > 0 ? $"median {moved[moved.Count / 2]:F2} m, p90 {moved[moved.Count * 9 / 10]:F2} m, most {most:F2} m (at {mostAt:F0} m of line)" : "-")}; "
                + $"the laid nodes' speeds ({far} samples) {(far > 0 ? $"median {speeds[far / 2]:F4} m/s, p99 {speeds[far * 99 / 100]:F3} m/s, max {speeds[far - 1]:F3} m/s, {100.0 * resting / far:F0} % under 5 mm/s" : "-")}; "
                + $"the step after the climb p50 {flight[flight.Length / 2] * us:F1} µs, p99 {flight[flight.Length * 99 / 100] * us:F1} µs; "
                + $"{line.Cuts.Sections} sections in the table; world bugs: {FarmlandLineCase.Bugs(bugs)}; "
                + $"allocated after the first step {allocated} B: {(whole && still ? "PASS" : "FAIL")}");
            return whole && still;
        }

        // The laid nodes' speeds: more than 30 m of line from the exit and within 0.1 m of the ground under them.
        static void Sample(MapWorld map, FiberLine line, List<double> speeds)
        {
            CableChain c = line.Chain;
            int n = c.Count;
            double exit = c[n - 1].Material;
            var at = new XZ[1];
            var g = new GroundSample[1];
            for (int i = 1; i < n - 1; i++)
            {
                if (!(exit - c[i].Material > 30)) continue;
                at[0] = new XZ(c[i].Position.X, c[i].Position.Z);
                map.SampleGround(at, g);
                if (c[i].Position.Y - g[0].Height < 0.1) speeds.Add(c[i].Velocity.Length());
            }
        }

        // Each 5 m mark of line the first time it lies within 0.1 m of the ground, more than 3 m of line from the exit.
        static void Lay(MapWorld map, FiberLine line, double t, Double3[] laidAt, double[] laidWhen)
        {
            if (line.Broken) return;
            CableChain c = line.Chain;
            double exit = c[c.Count - 1].Material;
            var at = new XZ[1];
            var g = new GroundSample[1];
            for (int k = 0; k < laidAt.Length && k * Mark < exit - 3; k++)
            {
                if (!double.IsNaN(laidWhen[k]) || !Where(line, k * Mark, out Double3 x)) continue;
                at[0] = new XZ(x.X, x.Z);
                map.SampleGround(at, g);
                if (x.Y - g[0].Height < 0.1)
                {
                    laidAt[k] = x;
                    laidWhen[k] = t;
                }
            }
        }

        // The point of the line at material s: between the trail's points, or between the chain's nodes.
        static bool Where(FiberLine line, double s, out Double3 x)
        {
            x = default;
            int t = line.TrailCount;
            for (int i = 0; i + 1 < t; i++)
                if (line.TrailMaterial(i) <= s && s <= line.TrailMaterial(i + 1))
                {
                    x = Lerp(line.Trail[i], line.Trail[i + 1], line.TrailMaterial(i), line.TrailMaterial(i + 1), s);
                    return true;
                }
            CableChain c = line.Chain;
            for (int i = 0; i + 1 < c.Count; i++)
                if (c[i].Material <= s && s <= c[i + 1].Material)
                {
                    x = Lerp(c[i].Position, c[i + 1].Position, c[i].Material, c[i + 1].Material, s);
                    return true;
                }
            return false;
        }

        static Double3 Lerp(Double3 a, Double3 b, double sa, double sb, double s) => sb > sa ? a + (b - a) * ((s - sa) / (sb - sa)) : a;

        internal static FlightSetup Setup(MapWorld map, XZ[] route, WindLevel wind, out Double3 foot, out double stand)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(Path.Combine(RangeScenarios.Root, SurfaceTable.RelativePath)));
            var loadout = new Loadout { Payload = 1.0, Legs = true, CoilPaidOut = 0 };
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash, surfaces);
            stand = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support) stand = Math.Max(stand, s.Radius - Math.Min(s.A.Y, s.B.Y));
            var ground = new GroundSample[1];
            map.SampleGround(new[] { route[0] }, ground);
            foot = new Double3(route[0].X, ground[0].Height, route[0].Z);
            Double3 nose = Bearing(route[0], route[1]);
            double heading = Math.Atan2(nose.X, nose.Z); // D-019: positive yaw turns north towards east
            Double3 up = ground[0].Normal; // set down on its facet, as the farmland line's case (w3-farmland-ground 2.2)
            return new FlightSetup
            {
                Seed = Seed,
                World = new FlightWorld(map),
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.Of(wind),
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

        // The sticks at t s: armed on the ground, the climb over the start, the route's point at that time (its height over the
        // ground ahead of it, as the hills pilot looks 1 s and 2.5 s ahead) with its velocity along the leg and the nose along
        // it, then the hover over the route's end.
        internal static StickSample Sticks(SimPilot p, FlightCore core, MapWorld map, GroundSample[] ground, XZ[] route, Double3 foot, double stand, double t, long hostUs)
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
                while (leg < route.Length - 1 && s > Distance(route[leg - 1], route[leg]))
                {
                    s -= Distance(route[leg - 1], route[leg]);
                    leg++;
                }
                XZ a = route[leg - 1], b = route[leg];
                double l = Distance(a, b), f = Math.Min(1, s / l);
                var at = new XZ(a.X + (b.X - a.X) * f, a.Z + (b.Z - a.Z) * f);
                Double3 along = Bearing(a, b);
                double lift = double.NegativeInfinity;
                for (int i = 0; i < 3; i++)
                {
                    double ahead = f < 1 ? i * 1.25 * Speed : 0;
                    Here[0] = new XZ(at.X + along.X * ahead, at.Z + along.Z * ahead);
                    map.SampleGround(Here, ground);
                    lift = Math.Max(lift, ground[0].Height);
                }
                p.Target = new Double3(at.X, lift + Height, at.Z);
                ref readonly FlightState state = ref core.State;
                p.Heading = along;
                if (f < 1)
                {
                    p.HoldVelocity = true;
                    p.TargetVelocity = new Double3(along.X * Speed + 0.5 * (at.X - state.Position.X), 0, along.Z * Speed + 0.5 * (at.Z - state.Position.Z));
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

        internal static Double3 Bearing(XZ a, XZ b)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, d = Math.Sqrt(dx * dx + dz * dz);
            return new Double3(dx / d, 0, dz / d);
        }

        static double Distance(XZ a, XZ b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
    }
}
