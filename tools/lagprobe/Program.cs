using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.World;

namespace Fpv450.LagProbe
{
    // tools/lagprobe (L1, the model-79 lag): what a flight log says about the scheduler falling behind real time, and a
    // headless replay that times each phase of each step. The replay's numbers are .NET's (RyuJIT), faster than the
    // player's Mono; the timeline's player cost comes from the player's own hitches, so the two can be set side by side.
    internal static class Program
    {
        const string Usage =
            "usage:\n"
            + "  lagprobe hitches <log>...                 one line a flight: its hitches, how far behind it fell, its dropped sticks\n"
            + "  lagprobe timeline <log> [--bin S]         per bin of flight time (default 5 s): lateness, the player's step cost,\n"
            + "                                            the drone, the line, the events\n"
            + "  lagprobe window <log> --wall S [--span S] the events and state within span (default 10 s) of wall time S\n"
            + "  lagprobe phases <log> [--bin S] [--from S] [--to S] [--dump S,S...]\n"
            + "                                            the timed replay (from the start; bins printed within --from/--to,\n"
            + "                                            flight seconds), every step record checked bit for bit; --dump prints\n"
            + "                                            the line's segments at those flight seconds";

        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2 && args[0] == "hitches") return Hitches(args.Skip(1).ToArray());
                if (args.Length >= 2 && args[0] == "timeline") return Timeline(args[1], Number(args, "--bin", 5));
                if (args.Length >= 2 && args[0] == "window") return Window(args[1], Number(args, "--wall", double.NaN), Number(args, "--span", 10));
                if (args.Length >= 2 && args[0] == "phases")
                    return Phases(args[1], Number(args, "--bin", 5), Number(args, "--from", 0), Number(args, "--to", double.MaxValue), Steps(args, "--dump"));
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine("lagprobe: " + e.Message);
            }
            Console.Error.WriteLine(Usage);
            return 2;
        }

        // ---- the log's own record of the scheduler ----

        // A late pass (HitchCause.Late), stamped with the first late step's state: Stamp − 1 was due Late µs before the
        // pass began, and the pass ran Steps steps.
        struct Pass
        {
            public long Stamp, Steps, LateUs;
        }

        sealed class Scan
        {
            public LogHeader Header;
            public long Steps, LogFull, SticksDropped, Hitches;
            public bool Complete;
            public readonly List<Pass> Passes = new List<Pass>();
            public readonly List<(long Step, long Us)> Pauses = new List<(long, long)>();

            // The pass in force at a step (−1 for none): the last one stamped at or before it, if its steps reach it.
            public int PassAt(long step)
            {
                int lo = 0, hi = Passes.Count - 1, found = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    if (Passes[mid].Stamp <= step) { found = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
                return found >= 0 && step < Passes[found].Stamp + Passes[found].Steps ? found : -1;
            }

            // How late the step ran, µs: within a pass, the pass's lateness carried along to the next pass's (back to back)
            // or to none by the pass's end (it caught up); 0 outside every pass.
            public double LateUs(long step)
            {
                int i = PassAt(step);
                if (i < 0) return 0;
                Pass p = Passes[i];
                double f = (double)(step - p.Stamp) / p.Steps;
                double next = i + 1 < Passes.Count && Passes[i + 1].Stamp == p.Stamp + p.Steps ? Passes[i + 1].LateUs : 0;
                return p.LateUs + (next - p.LateUs) * f;
            }

            public double PausedUs(long step)
            {
                double us = 0;
                foreach (var (s, u) in Pauses)
                    if (s <= step) us += u;
                return us;
            }

            // The wall time a step ran, s from the flight's start: its due time, the pauses before it, its lateness.
            public double Wall(long step) => step * 1e-3 + (PausedUs(step) + LateUs(step)) * 1e-6;

            // The step that ran at a wall time: the first whose Wall reaches it.
            public long StepAtWall(double wall)
            {
                long lo = 0, hi = Steps;
                while (lo < hi)
                {
                    long mid = (lo + hi) / 2;
                    if (Wall(mid) < wall) lo = mid + 1;
                    else hi = mid;
                }
                return lo;
            }

            // The player's flight-thread time a step from back-to-back passes (pass i ran Steps steps, from its start to the
            // next pass's start), µs; false when the next pass is not back to back, or when pass i is too short to be sure the
            // thread never waited between the two. A pass of n steps ends still behind (so the next starts at once, with no
            // yield or sleep between) when n·c exceeds the 1 ms to the next step's due time: n ≥ MinSteps does it for any step
            // dearer than 0.2 ms, below the .NET replay's cheapest (about 150 µs on the ground) times the player's Mono factor.
            public const int MinSteps = 5;

            public bool PassCost(int i, out double us)
            {
                us = 0;
                if (i + 1 >= Passes.Count || Passes[i + 1].Stamp != Passes[i].Stamp + Passes[i].Steps || Passes[i].Steps < MinSteps) return false;
                us = (Passes[i].Steps * 1000.0 + Passes[i + 1].LateUs - Passes[i].LateUs) / Passes[i].Steps;
                return true;
            }
        }

        delegate void OnStep(in LoggedStep step);
        delegate void OnEvent(in LoggedEvent e);

        static Scan Read(string path, OnStep onStep = null, OnEvent onEvent = null)
        {
            var scan = new Scan();
            using FlightLogReader r = FlightLogReader.Open(path);
            scan.Header = r.Header;
            while (r.Next())
            {
                if (r.Kind == LogFormat.StepBlock)
                {
                    scan.Steps++;
                    if (onStep != null)
                    {
                        r.ReadStep(out LoggedStep s);
                        onStep(s);
                    }
                }
                else if (r.Kind == LogFormat.EventBlock)
                {
                    r.ReadEvent(out LoggedEvent e);
                    if (e.Event.Kind == EventKind.Hitch)
                    {
                        if (e.Event.Index == (byte)HitchCause.Late)
                        {
                            scan.Hitches++;
                            scan.Passes.Add(new Pass { Stamp = e.Step, Steps = (long)e.Event.A, LateUs = (long)e.Event.B });
                        }
                        else if (e.Event.Index == (byte)HitchCause.LogFull) scan.LogFull++;
                        else if (e.Event.Index == (byte)HitchCause.Pause) scan.Pauses.Add((e.Step, (long)e.Event.B));
                    }
                    onEvent?.Invoke(e);
                }
            }
            scan.Complete = r.Complete;
            if (r.Complete) scan.SticksDropped = r.End.SticksDropped;
            return scan;
        }

        // ---- hitches: one line a flight ----

        static int Hitches(string[] logs)
        {
            Console.WriteLine("flight seed          | s flown | passes | steps caught up | max late ms | behind >50 ms: s, first at | behind >1 s: s | player us/step in passes p50/p90 | pauses | log waits | sticks dropped");
            foreach (string path in logs)
            {
                Scan s;
                try
                {
                    s = Read(path);
                }
                catch (FlightLogException e)
                {
                    Console.WriteLine(Path.GetFileName(path) + ": unreadable: " + e.Message);
                    continue;
                }
                long caught = 0, maxLate = 0;
                double over50 = 0, over1000 = 0, first50 = -1;
                var costs = new List<double>();
                for (int i = 0; i < s.Passes.Count; i++)
                {
                    Pass p = s.Passes[i];
                    caught += p.Steps;
                    maxLate = Math.Max(maxLate, p.LateUs);
                    if (s.PassCost(i, out double us))
                    {
                        costs.Add(us);
                        double span = p.Steps * us * 1e-6;
                        if (p.LateUs > 50000) over50 += span;
                        if (p.LateUs > 1000000) over1000 += span;
                    }
                    if (p.LateUs > 50000 && first50 < 0) first50 = s.Wall(p.Stamp);
                }
                costs.Sort();
                Console.WriteLine(string.Format(C, "0x{0:X16} | {1,7:F1} | {2,6} | {3,15} | {4,11:F1} | {5,7:F1}, {6,9} | {7,14:F1} | {8,11} | {9,6} | {10,9} | {11}",
                    s.Header.Seed, s.Steps * 1e-3, s.Passes.Count, caught, maxLate * 1e-3, over50, first50 < 0 ? "-" : first50.ToString("F1", C) + " s",
                    over1000, costs.Count == 0 ? "-" : Pct(costs, 50).ToString("F0", C) + " / " + Pct(costs, 90).ToString("F0", C), s.Pauses.Count, s.LogFull,
                    s.Complete ? s.SticksDropped.ToString(C) : "cut"));
            }
            return 0;
        }

        // ---- the timeline ----

        // Where a point lies on the test range (maps/test-range): S04's straw strips (twelve-metre patches either side of
        // the belt, z 7..19 and −19..−7, x −140..160), the belt itself (|z| < 7), the yard and the houses south-west of
        // the track start, or open ground.
        static string Region(Double3 p)
        {
            double az = Math.Abs(p.Z);
            if (p.X >= -140 && p.X <= 160 && az >= 7 && az <= 19) return "straw";
            if (p.X >= -142 && p.X <= 162 && az < 7) return "belt";
            if (p.X >= -225 && p.X <= -115 && p.Z >= -66 && p.Z <= -44) return "yard";
            return "open";
        }

        static readonly string[] Regions = { "straw", "belt", "yard", "open" };

        sealed class Bin
        {
            public long First = -1, Last;
            public LoggedStep End;
            public int MaxNodes, MaxTouching, MaxTrail;
            public double MaxDirt, MaxWinding, MaxMotorState, WindSum, SpeedMax;
            public long WindN;
            public readonly Dictionary<EventKind, int> Events = new Dictionary<EventKind, int>();
            public readonly Dictionary<string, int> FiberAt = new Dictionary<string, int>(); // FiberContact points by region
        }

        static Bin[] Bins(string path, double binSeconds, out Scan scan)
        {
            var bins = new List<Bin>();
            long per = (long)Math.Round(binSeconds * 1000);
            Bin At(long step)
            {
                int i = (int)(Math.Max(0, step - 1) / per);
                while (bins.Count <= i) bins.Add(new Bin());
                return bins[i];
            }
            scan = Read(path, (in LoggedStep st) =>
            {
                Bin b = At(st.State.Step);
                if (b.First < 0) b.First = st.State.Step;
                b.Last = st.State.Step;
                b.End = st;
                b.MaxNodes = Math.Max(b.MaxNodes, st.Trace.Tether.Nodes);
                b.MaxTrail = Math.Max(b.MaxTrail, st.Trace.Tether.Trail);
                b.MaxTouching = Math.Max(b.MaxTouching, st.Trace.ContactsTouching);
                b.SpeedMax = Math.Max(b.SpeedMax, st.State.Velocity.Length());
                b.WindSum += st.Trace.Wind.Length();
                b.WindN++;
                for (int i = 0; i < 4; i++)
                {
                    b.MaxDirt = Math.Max(b.MaxDirt, st.Trace.DirtMass[i]);
                    b.MaxWinding = Math.Max(b.MaxWinding, st.Trace.WindingTemperature[i]);
                    b.MaxMotorState = Math.Max(b.MaxMotorState, st.Trace.MotorState[i]);
                }
            }, (in LoggedEvent e) =>
            {
                Bin b = At(e.Step);
                b.Events[e.Event.Kind] = b.Events.TryGetValue(e.Event.Kind, out int n) ? n + 1 : 1;
                if (e.Event.Kind == EventKind.FiberContact)
                {
                    string r = Region(e.Event.Point);
                    b.FiberAt[r] = b.FiberAt.TryGetValue(r, out int m) ? m + 1 : 1;
                }
            });
            return bins.ToArray();
        }

        static int Timeline(string path, double binSeconds)
        {
            Bin[] bins = Bins(path, binSeconds, out Scan scan);
            Console.WriteLine($"flight 0x{scan.Header.Seed:X16}: {scan.Steps} steps, {scan.Passes.Count} late passes, {scan.Pauses.Count} pauses, "
                + $"{scan.LogFull} log waits, {(scan.Complete ? scan.SticksDropped + " stick reports dropped" : "cut")}");
            Console.WriteLine("t sim s | wall s | passes | late ms max/end | player us/step | drone x, y, z (m) region | v max | nodes | trail | touch | wind | dirt g | wind C | motor | fiber contacts straw/belt/yard/open | events");
            long per = (long)Math.Round(binSeconds * 1000);
            for (int b = 0; b < bins.Length; b++)
            {
                Bin bin = bins[b];
                if (bin.First < 0) continue;
                int passes = 0;
                double maxLate = 0, sumUs = 0, sumN = 0;
                for (int i = 0; i < scan.Passes.Count; i++)
                {
                    Pass p = scan.Passes[i];
                    if (p.Stamp < bin.First || p.Stamp > bin.Last) continue;
                    passes++;
                    maxLate = Math.Max(maxLate, p.LateUs);
                    if (scan.PassCost(i, out double us))
                    {
                        sumUs += us * p.Steps;
                        sumN += p.Steps;
                    }
                }
                ref readonly FlightState s = ref bin.End.State;
                string events = string.Join(", ", bin.Events.Where(kv => kv.Key != EventKind.Hitch).OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value));
                Console.WriteLine(string.Format(C,
                    "{0,7:F0} | {1,6:F1} | {2,6} | {3,7:F0} / {4,5:F0} | {5,14} | {6,7:F1}, {7,5:F1}, {8,6:F1} {9,-5} | {10,5:F1} | {11,5} | {12,5} | {13,5} | {14,4:F1} | {15,6:F2} | {16,6:F0} | {17,5:F0} | {18,4} / {19,4} / {20,4} / {21,4} | {22}",
                    b * per * 1e-3, scan.Wall(bin.Last), passes, maxLate * 1e-3, scan.LateUs(bin.Last) * 1e-3, sumN > 0 ? (sumUs / sumN).ToString("F0", C) : "-",
                    s.Position.X, s.Position.Y, s.Position.Z, Region(s.Position), bin.SpeedMax, bin.MaxNodes, bin.MaxTrail, bin.MaxTouching,
                    bin.WindN > 0 ? bin.WindSum / bin.WindN : 0, bin.MaxDirt * 1000, bin.MaxWinding, bin.MaxMotorState,
                    Get(bin.FiberAt, "straw"), Get(bin.FiberAt, "belt"), Get(bin.FiberAt, "yard"), Get(bin.FiberAt, "open"), events));
            }
            return 0;
        }

        // ---- a window around a wall time ----

        static int Window(string path, double wall, double span)
        {
            if (double.IsNaN(wall)) throw new ArgumentException("window needs --wall S");
            // First pass: the scheduler's record, to map the wall time to steps.
            Scan scan = Read(path);
            long from = scan.StepAtWall(wall - span), to = scan.StepAtWall(wall + span), at = scan.StepAtWall(wall);
            Console.WriteLine(string.Format(C, "flight 0x{0:X16}: wall {1:F1} s is step {2} (flight time {3:F3} s, {4:F0} ms late there); the window is steps {5}..{6}",
                scan.Header.Seed, wall, at, at * 1e-3, scan.LateUs(at) * 1e-3, from, to));
            var quiet = new HashSet<EventKind> { EventKind.Hitch, EventKind.FiberContact, EventKind.FiberSlip, EventKind.FiberSection };
            var perSecond = new SortedDictionary<long, Dictionary<string, int>>();
            var lines = new List<string>();
            var hitches = new List<string>();
            var names = new FlightSummary { Header = scan.Header }; // only for Describe's names
            string root = RepositoryWith(Path.Combine("maps", scan.Header.WorldId, "placements.json"));
            IWorldQuery world = root != null ? MapWorld.Load(root, scan.Header.WorldId) : null;
            var members = new Dictionary<string, int>(); // the line's contacts and slips by member, object and material
            Read(path, (in LoggedStep st) =>
            {
                long k = st.State.Step;
                if (k < from || k > to || (k - from) % 1000 != 0) return;
                ref readonly FlightState s = ref st.State;
                ref readonly StepTrace t = ref st.Trace;
                ref readonly StickSample k2 = ref st.Stick;
                lines.Add(string.Format(C,
                    "  step {0} (wall {1:F1} s, {2:F0} ms late): drone ({3:F1}, {4:F1}, {5:F1}) m {6}, {7:F1} m/s; line {8} nodes, trail {9}, paid {10:F1} m, exit {11:F2} N; touching {12}; wind {13:F1} m/s; "
                    + "motors {14}, dirt {15:F2} g, windings {16:F0} C; rotors {17:F0} {18:F0} {19:F0} {20:F0} rad/s; sticks r {21:F2} p {22:F2} y {23:F2} t {24:F2}{25}",
                    k, scan.Wall(k), scan.LateUs(k) * 1e-3, s.Position.X, s.Position.Y, s.Position.Z, Region(s.Position), s.Velocity.Length(),
                    t.Tether.Nodes, t.Tether.Trail, t.Tether.PaidOut, t.Tether.ExitTension, t.ContactsTouching, t.Wind.Length(),
                    Max4(t.MotorState), Max4(t.DirtMass) * 1000, Max4(t.WindingTemperature), s.RotorSpeed[0], s.RotorSpeed[1], s.RotorSpeed[2], s.RotorSpeed[3],
                    k2.Roll, k2.Pitch, k2.Yaw, k2.Throttle, s.Crashed ? ", CRASHED" : ""));
            }, (in LoggedEvent e) =>
            {
                if (e.Step < from || e.Step > to) return;
                if (e.Event.Kind == EventKind.Hitch)
                {
                    if (hitches.Count < 400) hitches.Add(string.Format(C, "{0}:{1}x{2:F0}ms", e.Step, e.Event.A, e.Event.B * 1e-3));
                }
                long sec = (e.Step - from) / 1000;
                if (!perSecond.TryGetValue(sec, out var d)) perSecond[sec] = d = new Dictionary<string, int>();
                string key = e.Event.Kind == EventKind.FiberContact || e.Event.Kind == EventKind.FiberSlip
                    ? e.Event.Kind + "." + (FiberMember)(e.Event.Index & 0x0F) + "@" + Region(e.Event.Point) : e.Event.Kind.ToString();
                d[key] = d.TryGetValue(key, out int n) ? n + 1 : 1;
                if (e.Event.Kind == EventKind.FiberContact || e.Event.Kind == EventKind.FiberSlip)
                {
                    string what = e.Event.Object == FiberContacts.DroneObject ? "the drone's part " + e.Event.Part
                        : (world?.ObjectName(e.Event.Object) ?? "object " + e.Event.Object) + ", " + (world?.MaterialName(e.Event.Material) ?? "material " + e.Event.Material);
                    string m = e.Event.Kind + " " + (FiberMember)(e.Event.Index & 0x0F) + " on " + what;
                    members[m] = members.TryGetValue(m, out int c) ? c + 1 : 1;
                }
                if (!quiet.Contains(e.Event.Kind))
                    lines.Add(string.Format(C, "    step {0} (wall {1:F2} s): {2}", e.Step, scan.Wall(e.Step), names.Describe(e.Event)));
            });
            Console.WriteLine("state each second, and the events other than hitches and the fiber's contacts, slips and sections:");
            foreach (string l in lines) Console.WriteLine(l);
            Console.WriteLine("events by second of the window (from step " + from + "):");
            foreach (var kv in perSecond)
                Console.WriteLine("  +" + kv.Key + " s: " + string.Join(", ", kv.Value.OrderBy(x => x.Key).Select(x => x.Key + " " + x.Value)));
            Console.WriteLine("the line's contacts and slips in the window, by member: " + string.Join("; ",
                members.OrderByDescending(x => x.Value).Take(12).Select(x => x.Key + " x" + x.Value)));
            Console.WriteLine("late passes in the window (stamp:steps x late): " + hitches.Count + (hitches.Count > 0 ? "; " + string.Join(" ", hitches.Take(60)) + (hitches.Count > 60 ? " ..." : "") : ""));
            return 0;
        }

        // ---- the timed replay ----

        static int Phases(string path, double binSeconds, double fromS, double toS, long[] dumps)
        {
            using FlightLogReader log = FlightLogReader.Open(path);
            LogHeader h = log.Header;
            if (h.ModelVersion != LogFormat.ModelVersion)
                throw new ArgumentException("the log is of model " + h.ModelVersion + ", this probe's core is model " + LogFormat.ModelVersion);
            string root = RepositoryWith(Path.Combine("maps", h.WorldId, "placements.json"))
                ?? throw new ArgumentException("no maps/" + h.WorldId + " above " + Directory.GetCurrentDirectory());
            var world = new FlightWorld(MapWorld.Load(root, h.WorldId));
            if (world.ContentHash != h.WorldHash) throw new ArgumentException("the world differs from the log's");
            var problems = new List<string>();
            FlightSetup setup = h.ToSetup(world, problems);
            if (problems.Count > 0) throw new ArgumentException("the settings differ: " + problems[0]);
            long build = Stopwatch.GetTimestamp();
            var core = new FlightCore(setup);
            using Fpv450.Sim.Wind.WindField air = core.Wind;
            Console.WriteLine(string.Format(C, "flight 0x{0:X16}, model {1}, world '{2}': the core built in {3:F0} ms on {4} (the player's Mono is slower; the timeline's "
                + "player us/step is its own)", h.Seed, h.ModelVersion, h.WorldId, (Stopwatch.GetTimestamp() - build) * Probe.Us * 1e-3, LogHeader.CurrentRuntime()));

            long per = (long)Math.Round(binSeconds * 1000);
            var replayed = new byte[LogFormat.StepSize];
            var times = new List<double>((int)per);
            double extras = 0; // the scheduler's own work a step besides Advance (FlightScheduler.RunDue): pose, signals, the record
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame pose0);
            core.GetSignals(out FeedSignals signals0);
            poses.Begin(0, pose0, signals0);
            var start = new long[(int)Phase.Count];
            var qStart = new long[(int)Phase.Count];
            var cStart = new long[(int)Phase.Count];
            var kStart = new long[(int)Query.Count];
            var kcStart = new long[(int)Query.Count];
            long binFirst = 1, checkedSteps = 0;
            double nodes = 0, touching = 0, segQueries = 0, members = 0, samples = 0;
            var nodesAt = new double[Regions.Length];
            var heldBy = new double[16];
            bool header = false;

            void Flush(long last)
            {
                double t0 = (binFirst - 1) * 1e-3;
                if (times.Count > 0 && t0 + binSeconds > fromS && t0 < toS)
                {
                    if (!header)
                    {
                        header = true;
                        Console.WriteLine("t s   | mean us | p50 | p99 | max  | +sched | wind | flight | contacts (q) | strikes | body+ | tether: total (q) prepare ground rotors loads assign solve finish cut rest "
                            + "| q calls/step | nodes, by region straw/belt/yard/open | node contacts by member | touching | seg queries | cache members "
                            + "| world calls by kind: us (calls) a step");
                    }
                    times.Sort();
                    double n = times.Count;
                    double P(Phase p) => (Probe.Ticks[(int)p] - start[(int)p]) * Probe.Us / n;
                    double Q(Phase p) => (Probe.QueryTicks[(int)p] - qStart[(int)p]) * Probe.Us / n;
                    Phase[] tether = { Phase.Tether, Phase.TetherPrepare, Phase.TetherGround, Phase.TetherRotors, Phase.TetherLoads, Phase.TetherAssign,
                        Phase.TetherSolve, Phase.TetherFinish, Phase.TetherCut, Phase.TetherRest };
                    double tq = tether.Sum(Q), calls = 0;
                    for (int i = 0; i < (int)Phase.Count; i++) calls += Probe.QueryCalls[i] - cStart[i];
                    string members2 = string.Join(" ", Enumerable.Range(0, 16).Where(i => heldBy[i] > 0)
                        .Select(i => (FiberMember)i + " " + (heldBy[i] / Math.Max(1, samples)).ToString("F1", C)));
                    string kinds = string.Join(", ", Enumerable.Range(0, (int)Query.Count).Where(i => Probe.KindCalls[i] > kcStart[i])
                        .Select(i => (Query)i + " " + ((Probe.KindTicks[i] - kStart[i]) * Probe.Us / n).ToString("F0", C) + " ("
                            + ((Probe.KindCalls[i] - kcStart[i]) / n).ToString("F1", C) + ")"));
                    Console.WriteLine(string.Format(C,
                        "{0,5:F0} | {1,7:F0} | {2,3:F0} | {3,3:F0} | {4,4:F0} | {32,6:F0} | {5,4:F0} | {6,6:F0} | {7,4:F0} ({8,3:F0}) | {9,7:F0} | {10,5:F0} | {11,5:F0} ({12,3:F0}) {13,4:F0} {14,4:F0} {15,4:F0} {16,4:F0} {17,4:F0} {18,4:F0} {19,4:F0} {20,4:F0} {21,4:F0} "
                        + "| {22,6:F0} | {23,5:F1}, {24:F1}/{25:F1}/{26:F1}/{27:F1} | {28} | {29,5:F1} | {30,5:F1} | {31,6:F0} | " + kinds,
                        t0, times.Average(), Pct(times, 50), Pct(times, 99), times[times.Count - 1],
                        P(Phase.Wind) + P(Phase.WindTrace), P(Phase.Flight), P(Phase.ContactForces) + P(Phase.ContactSense), Q(Phase.ContactForces) + Q(Phase.ContactSense),
                        P(Phase.Strikes), P(Phase.Start) + P(Phase.Body) + P(Phase.Loose) + P(Phase.Rain),
                        tether.Sum(P), tq, P(Phase.TetherPrepare), P(Phase.TetherGround), P(Phase.TetherRotors), P(Phase.TetherLoads), P(Phase.TetherAssign),
                        P(Phase.TetherSolve), P(Phase.TetherFinish), P(Phase.TetherCut), P(Phase.TetherRest) + P(Phase.Tether),
                        calls / n, nodes / Math.Max(1, samples), nodesAt[0] / Math.Max(1, samples), nodesAt[1] / Math.Max(1, samples), nodesAt[2] / Math.Max(1, samples),
                        nodesAt[3] / Math.Max(1, samples), members2.Length == 0 ? "-" : members2, touching / Math.Max(1, samples), segQueries / Math.Max(1, samples),
                        members / Math.Max(1, samples), extras / n));
                }
                Array.Copy(Probe.Ticks, start, start.Length);
                Array.Copy(Probe.QueryTicks, qStart, qStart.Length);
                Array.Copy(Probe.QueryCalls, cStart, cStart.Length);
                Array.Copy(Probe.KindTicks, kStart, kStart.Length);
                Array.Copy(Probe.KindCalls, kcStart, kcStart.Length);
                times.Clear();
                extras = 0;
                nodes = touching = segQueries = members = samples = 0;
                Array.Clear(nodesAt);
                Array.Clear(heldBy);
                binFirst = last + 1;
            }

            while (log.Next())
            {
                if (log.Kind != LogFormat.StepBlock) continue;
                ReadOnlySpan<byte> payload = log.Payload;
                LogFormat.DecodeStick(payload, out StickSample stick);
                Probe.StepBegin();
                long t = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t1 = Stopwatch.GetTimestamp(), dt = t1 - t;
                Probe.StepEnd();
                long step = core.State.Step;
                if (LogFormat.StepOf(payload) != step) throw new InvalidOperationException("the log skips to step " + LogFormat.StepOf(payload) + " after " + (step - 1));
                t1 = Stopwatch.GetTimestamp();
                LogFormat.EncodeStep(replayed, core.State, stick, core.Trace);
                core.GetPose(out PoseFrame pose);
                core.GetSignals(out FeedSignals signals);
                poses.Publish(pose, signals);
                extras += (Stopwatch.GetTimestamp() - t1) * Probe.Us;
                if (!payload.SequenceEqual(replayed))
                {
                    Console.WriteLine("DIVERGED at step " + step + ": the timed core's record differs from the log's; its timings stop being this flight's");
                    return 1;
                }
                checkedSteps++;
                times.Add(dt * Probe.Us);
                if (step % 50 == 0 && core.Tether != null && core.Tether.Attached)
                {
                    CableChain chain = core.Tether.Chain;
                    samples++;
                    nodes += chain.Count;
                    for (int i = 0; i < chain.Count; i++)
                    {
                        ref CableNode node = ref chain[i];
                        nodesAt[Array.IndexOf(Regions, Region(node.Position))]++;
                        if (node.Contact0.Active) heldBy[node.Contact0.MemberKind & 15]++;
                        if (node.Contact1.Active) heldBy[node.Contact1.MemberKind & 15]++;
                    }
                    FiberContacts fc = core.Tether.Contacts;
                    if (fc != null)
                    {
                        touching += fc.Touching;
                        segQueries += fc.Queries;
                        for (int c = 0; c < fc.CacheCount; c++)
                            if (fc.Cache(c).Valid) members += fc.Cache(c).MemberCount;
                    }
                }
                if (step % per == 0) Flush(step);
                if (Array.IndexOf(dumps, step) >= 0) Dump(core, world, setup.Tether.Number("contact.clearance_margin_m"));
            }
            Flush(core.State.Step);
            Console.WriteLine($"checked {checkedSteps} step records bit for bit against the log: all equal");
            Console.WriteLine("world calls on the flight thread over the flight: " + string.Join(", ", Enumerable.Range(0, (int)Query.Count)
                .Where(i => Probe.KindCalls[i] > 0)
                .Select(i => (Query)i + " " + Probe.KindCalls[i] + " calls, " + (Probe.KindTicks[i] * Probe.Us / Math.Max(1, checkedSteps)).ToString("F1", C) + " us/step")));
            return 0;
        }

        // The line at one step: each segment, where it lies, what its first node holds, whether its clearance certificate
        // holds (a certified segment is not queried), and the fiber's own query on it (FiberContacts.ShapeHits: its capsule
        // at the clearance margin, wires left out) timed here between steps. The world's answers change no state.
        static void Dump(FlightCore core, FlightWorld world, double margin)
        {
            if (core.Tether == null || !core.Tether.Attached) return;
            CableChain chain = core.Tether.Chain;
            double radius = core.Tether.Diameter / 2;
            var shapes = new StaticContact[256];
            FlightState s = core.State;
            Console.WriteLine(string.Format(C, "  the line at step {0}: drone ({1:F1}, {2:F1}, {3:F1}) m; {4} nodes, from the laid end to the exit", s.Step,
                s.Position.X, s.Position.Y, s.Position.Z, chain.Count));
            Console.WriteLine("    seg | from (x, y, z) m        | length m | reach x by z m | region | certified | node holds        | query: found, us");
            for (int j = 0; j + 1 < chain.Count; j++)
            {
                ref CableNode na = ref chain[j];
                Double3 a = na.Position, b = chain[j + 1].Position;
                var cap = new Capsule(a, b, radius);
                long t0 = Stopwatch.GetTimestamp();
                int found = 0;
                for (int r = 0; r < 5; r++) found = world.Query.StaticContacts(cap, cap, margin, shapes, QueryFilter.NoWires);
                double us = (Stopwatch.GetTimestamp() - t0) * Probe.Us / 5;
                string holds = (na.Contact0.Active ? Held(na.Contact0, world) : "-") + (na.Contact1.Active ? " + " + Held(na.Contact1, world) : "");
                Console.WriteLine(string.Format(C, "    {0,3} | ({1,7:F1}, {2,5:F2}, {3,7:F1}) | {4,8:F1} | {5,6:F1} x {6,5:F1} | {7,-6} | {8,-9} | {9,-17} | {10,4}, {11:F1}",
                    j, a.X, a.Y, a.Z, (b - a).Length(), Math.Abs(b.X - a.X) + 2 * (radius + margin), Math.Abs(b.Z - a.Z) + 2 * (radius + margin),
                    Region((a + b) * 0.5), na.Cleared ? "yes" : "no", holds, found, us));
            }
        }

        static string Held(in CableContact c, FlightWorld world)
        {
            var kind = (FiberMember)(c.MemberKind & 15);
            int obj = (int)(c.Member >> 32);
            if (kind == FiberMember.Ground) return "ground";
            if (kind == FiberMember.Part) return "drone part";
            return kind + " " + (world.Query.ObjectName(obj) ?? "#" + obj);
        }

        // ---- helpers ----

        static string RepositoryWith(string relative)
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
                if (File.Exists(Path.Combine(dir, relative))) return dir;
            return null;
        }

        static double Number(string[] args, string name, double fallback)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0) return fallback;
            if (i + 1 >= args.Length) throw new ArgumentException(name + " needs a value");
            return double.Parse(args[i + 1], C);
        }

        // Flight seconds, comma separated, as the steps they end.
        static long[] Steps(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0) return Array.Empty<long>();
            if (i + 1 >= args.Length) throw new ArgumentException(name + " needs a value");
            return args[i + 1].Split(',').Select(x => (long)Math.Round(double.Parse(x, C) * 1000)).ToArray();
        }

        static double Pct(List<double> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)(p / 100 * sorted.Count))];

        static int Get(Dictionary<string, int> d, string key) => d.TryGetValue(key, out int n) ? n : 0;

        static double Max4(in Corners c) => Math.Max(Math.Max(c[0], c[1]), Math.Max(c[2], c[3]));
    }
}
