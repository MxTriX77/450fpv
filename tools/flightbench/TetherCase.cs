using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The fiber's acceptance harness (p7d-tether 1.11, design 16 and 17): the pilot's four causes and the flights that must keep
    // the line (PHY-19, P35–P38), each on seeds 1–N (20 by default) on the session's drone, every flight its own core so the
    // order of the parallel runs never moves a bit. Each row prints its cuts by mechanism and place and the hole's edge's most
    // stress while whole; the targets are judged, the known-wrong models must each fail one the model meets, and the bench
    // fails on any target not met. The sensitivity rows print the reverse and the backward move under the assumed spool numbers.
    internal static class TetherCase
    {
        enum Target { Never, MayOrMayNot, Some, None }

        sealed class Row
        {
            public string Name;
            public Func<ulong, FiberCutScenarios.Outcome> Fly;
            public Target Target;
            public FiberCutScenarios.Outcome[] O;
        }

        public static bool Run(int seeds)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            // The two-way spool takes line back at its hold, Lip·G₀: a reel's take-up, flown at models 74–88's 2–10 g so that the
            // known-wrong is a reel that keeps its line short (design 18: on the light pack's 0.01–2 g its take-up is too weak to).
            Action<FlightSetup> twoWay = s =>
                {
                    s.TetherTwoWay = true;
                    s.Tether = FiberCutScenarios.WithBand(s.Tether, "spool.peel_energy_j_m", 0.0196, 0.0981);
                },
                bare = s => s.TetherWithoutContacts = true,
                air4 = s => s.TetherDesign4Air = true,
                flared = s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_radius_m", 0.004),
                peel2 = s => s.Tether = FiberCutScenarios.Scaled(s.Tether, "spool.peel_energy_j_m", 2),
                muLow = s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 0.36),
                muHigh = s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 1.0),
                muOld = s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 0.3);
            Row R(string name, Target t, Func<ulong, FiberCutScenarios.Outcome> fly) => new Row { Name = name, Target = t, Fly = fly };
            var rows = new List<Row>
            {
                R("normal liftoff, Calm", Target.Never, s => FiberCutScenarios.Liftoff(spec, false, s)),
                R("normal liftoff, Windy", Target.Never, s => FiberCutScenarios.Liftoff(spec, false, s, null, WindLevel.Windy)),
                R("very sharp liftoff", Target.Some, s => FiberCutScenarios.Liftoff(spec, true, s)),
                R("backward after liftoff", Target.Some, s => FiberCutScenarios.Backward(spec, s)),
                // Design 18's reading of "after lift off": the legs just off, inside the 4 R where the wake meets the ground. Judged
                // since the exit's segment keeps the spool's law (CableChain.SpoolAfterSlide; before, the legs' slides stretched it to
                // 1.4–2.9 N against the law's 0.26 N).
                R("backward just after liftoff, at 0.4 m (design 18)", Target.Some, s => FiberCutScenarios.Backward(spec, s, null, 3, 0.4)),
                R("sharp reverse from 54 km/h, Calm", Target.MayOrMayNot, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s)),
                R("sharp reverse from 54 km/h, Windy", Target.MayOrMayNot, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Windy, s)),
                R("pitch-back from 90 km/h, the cruise's throttle held (D-087)", Target.None, s => FiberCutScenarios.PitchBack(spec, WindLevel.Calm, s, null, 25)),
                R("orbit of 20 m at 54 km/h", Target.Never, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 43, 360, null, 21).O),
                R("gentle 180° turn of 15 m at 54 km/h", Target.Never, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 57, 180).O),
                R("cruise at 100 km/h, Calm, its height held", Target.Never, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 27.8, 0, heightFirst: true).O),
                R("cruise at 100 km/h, Windy, its height held", Target.Never, s => FiberCutScenarios.Turn(spec, WindLevel.Windy, s, 27.8, 0, heightFirst: true).O),
                R("tree snag", Target.Some, s => FiberCutScenarios.TreeSnag(spec, WindLevel.Calm, s)),
                R("known-wrong: sharp reverse, Calm, the two-way spool", Target.None, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, twoWay)),
                R("known-wrong: tree snag without the fiber's contacts", Target.None, s => FiberCutScenarios.TreeSnag(spec, WindLevel.Calm, s, bare)),
                R("known-wrong: very sharp liftoff, design 4's air", Target.None, s => FiberCutScenarios.Liftoff(spec, true, s, air4)),
                R("known-wrong: sharp reverse, Calm, the flared 4 mm lip", Target.None, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, flared)),
                R("sensitivity: sharp reverse, Calm, G0 doubled", Target.None, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, peel2)),
                R("sensitivity: sharp reverse, Calm, lip mu 0.36 (the band's floor)", Target.None, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, muLow)),
                R("sensitivity: sharp reverse, Calm, lip mu 1.0 (the band's top)", Target.None, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, muHigh)),
                R("sensitivity: backward after liftoff, lip mu 1.0", Target.None, s => FiberCutScenarios.Backward(spec, s, muHigh)),
                R("sensitivity: very sharp liftoff, lip mu 1.0", Target.None, s => FiberCutScenarios.Liftoff(spec, true, s, muHigh)),
                R("sensitivity: orbit of 20 m at 54 km/h, lip mu 1.0", Target.None, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 43, 360, muHigh, 21).O),
                R("sensitivity: cruise at 100 km/h, Windy, its height held, lip mu 1.0", Target.None, s => FiberCutScenarios.Turn(spec, WindLevel.Windy, s, 27.8, 0, 90, muHigh, heightFirst: true).O),
                R("sensitivity: backward after liftoff, models to 88's lip mu 0.3", Target.None, s => FiberCutScenarios.Backward(spec, s, muOld)),
            };
            Console.WriteLine($"--tether: the pilot's four causes (PHY-19, p7d-tether 1.11), {rows.Count} rows of {seeds} seeds on the session's drone; "
                + Power.Describe());
            FiberCutScenarios.SessionDrone = true;
            try
            {
                foreach (Row r in rows) r.O = new FiberCutScenarios.Outcome[seeds];
                Parallel.For(0, rows.Count * seeds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) },
                    i => rows[i / seeds].O[i % seeds] = rows[i / seeds].Fly((ulong)(i % seeds + 1)));
            }
            finally
            {
                FiberCutScenarios.SessionDrone = false;
            }

            foreach (Row r in rows)
            {
                for (int s = 0; s < seeds; s++) Console.WriteLine($"  {r.Name} seed {s + 1}: {r.O[s]}; the edge's most σ {r.O[s].EdgeMost / 1e9:0.000} GPa (θ {r.O[s].EdgeTurn * 180 / Math.PI:0}°, T {r.O[s].EdgeTension:0.000} N)");
                Console.WriteLine($"== {r.Name}: {Counts(r)}; the edge's most σ while whole: mean {r.O.Average(o => o.EdgeMost) / 1e9:0.000} GPa, most {r.O.Max(o => o.EdgeMost) / 1e9:0.000}");
            }

            Row Find(string name) => rows.First(r => r.Name == name);
            int Cuts(Row r) => r.O.Count(o => o.Broken);
            int Where(Row r, Func<FiberCutScenarios.Outcome, bool> p) => r.O.Count(o => o.Broken && p(o));
            bool Disc(FiberCutScenarios.Outcome o) => o.Mechanism == TetherMechanism.Prop;
            bool Lip(FiberCutScenarios.Outcome o) => o.Place == FiberMember.Lip;
            bool Crown(FiberCutScenarios.Outcome o) => o.Place == FiberMember.Shape || o.Place == FiberMember.Twig || o.Place == FiberMember.Snag;
            int margin = Math.Max(1, seeds / 20); // design 16's 2 and 38 of 40, at this many seeds

            bool ok = true;
            void Judge(string what, bool met)
            {
                ok &= met;
                Console.WriteLine($"  {(met ? "PASS" : "FAIL")}: {what}");
            }
            Console.WriteLine("--tether: the targets");
            foreach (Row r in rows)
            {
                int c = Cuts(r);
                if (r.Target == Target.Never) Judge($"{r.Name} never cuts ({c} of {seeds})", c == 0);
                else if (r.Target == Target.MayOrMayNot) Judge($"{r.Name} may or may not cut ({c} of {seeds}, within {margin}–{seeds - margin})", c >= margin && c <= seeds - margin);
                else if (r.Target == Target.Some) Judge($"{r.Name} cuts on some ({c} of {seeds})", c >= margin);
            }
            Row calm = Find("sharp reverse from 54 km/h, Calm"), windy = Find("sharp reverse from 54 km/h, Windy");
            int lip = Where(calm, Lip) + Where(windy, Lip);
            Judge($"a sharp reverse tears at the hole's edge on some (D-087: {lip} of {2 * seeds}; by a rear disc {Where(calm, Disc) + Where(windy, Disc)})", lip >= margin);
            Row snag = Find("tree snag");
            Judge($"a tree snag cuts at the crown on some ({Where(snag, Crown)} of {seeds}; by a disc {Where(snag, Disc)})", Where(snag, Crown) >= margin);

            Console.WriteLine("--tether: the known-wrong models, each failing a target the model meets");
            Row twoWayRow = Find("known-wrong: sharp reverse, Calm, the two-way spool");
            bool slack = Where(calm, Disc) >= margin;
            Judge($"the reverse cuts by its slack at a rear disc: the model {Where(calm, Disc)} of {seeds}, the two-way spool {Where(twoWayRow, Disc)} (it must fail)",
                slack && Where(twoWayRow, Disc) < margin);
            Row bareRow = Find("known-wrong: tree snag without the fiber's contacts");
            Judge($"a tree snag cuts at the crown: the model {Where(snag, Crown)} of {seeds}, without the fiber's contacts {Where(bareRow, Crown)} (it must fail)",
                Where(snag, Crown) >= margin && Where(bareRow, Crown) < margin);
            Console.WriteLine($"  not judged: design 4's air ({Cuts(Find("known-wrong: very sharp liftoff, design 4's air"))} of {seeds} cut) and the flared lip "
                + $"({Where(Find("known-wrong: sharp reverse, Calm, the flared 4 mm lip"), Lip)} at the edge) fail the very sharp liftoff's and the edge's targets, which the model fails too");

            Console.WriteLine($"--tether: {(ok ? "PASS" : "FAIL")} (design 16's targets on {seeds} seeds; design 17 says which the physics does not meet and why)");
            return ok;
        }

        static string Counts(Row r)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (FiberCutScenarios.Outcome o in r.O)
            {
                string key = !o.Broken ? "whole" : o.Place == FiberMember.Lip ? $"{o.Mechanism} at {o.Place} ({o.Band})" : $"{o.Mechanism} at {o.Place}";
                counts[key] = counts.TryGetValue(key, out int k) ? k + 1 : 1;
            }
            return string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")) + $" of {r.O.Length}";
        }
    }
}
