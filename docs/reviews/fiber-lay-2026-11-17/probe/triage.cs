using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.App.Session;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

// QA triage of the pilot's flights on the model 103 build (p7d-tether 1.11h). Reads the logs only (no replay).
// Args: <repo root> <out dir> <flights dir> [count]. Takes the newest <count> logs (30), oldest first, and names each
// flight by its index here and its seed: never its file name, date or time (OPSEC). Writes per flight fNN.diag.txt:
// the header's settings, the player's summary lines (the log's path dropped), the incidents, the fiber's contacts with
// world members, every drop of the trail's count (a lift or a thaw), and a trace every 0.5 s.
static class Program
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.Idle; } catch { }
        string root = args[0], outDir = args[1], dir = args[2];
        int count = args.Length > 3 ? int.Parse(args[3], C) : 30;
        Directory.CreateDirectory(outDir);
        FlightWorldChoice fw = FlightWorlds.Farmland(root);
        FlightWorld fworld = fw.Open();
        IWorldQuery world = fworld.Query;
        var at = new XZ[1];
        var under = new GroundSample[1];
        double Ground(double x, double z) { at[0] = new XZ(x, z); world.SampleGround(at, under); return under[0].Height; }
        var logs = new DirectoryInfo(dir).GetFiles("*.flightlog").OrderBy(f => f.LastWriteTimeUtc).ToList();
        logs = logs.Skip(Math.Max(0, logs.Count - count)).ToList();
        using var index = new StreamWriter(Path.Combine(outDir, "index.txt"));
        for (int fi = 0; fi < logs.Count; fi++)
        {
            string log = logs[fi].FullName;
            string tag = $"f{fi + 1:00}";
            LogHeader h;
            using (FlightLogReader r = FlightLogReader.Open(log)) h = r.Header;
            using var w = new StreamWriter(Path.Combine(outDir, tag + ".diag.txt"));
            string head = $"{tag} seed 0x{h.Seed:X16} model {h.ModelVersion} world {h.WorldId} wind {h.Wind.Level} rain {h.Rain} payload {h.Loadout.Payload:0.###} kg legs {h.Loadout.Legs} line {(h.Tether != null ? "yes" : "no")}";
            w.WriteLine(head);
            PostFlight post = PostFlight.Read(log, id => id == fw.Id ? world : null);
            string brk = "-";
            foreach (string line in post.Lines())
            {
                if (line.StartsWith("log ", StringComparison.Ordinal) || line.Contains(":\\") || line.Contains(".flightlog")) continue;
                w.WriteLine("summary: " + line);
                if (line.StartsWith("fiber", StringComparison.Ordinal)) brk = line;
                if (line.StartsWith("time ", StringComparison.Ordinal)) head += " | " + line;
            }
            FlightSummary sum = FlightSummary.Read(log);
            int hitches = 0, fiberContacts = 0;
            var members = new Dictionary<string, int>();
            foreach (LoggedEvent e in sum.Events)
            {
                EventKind k = e.Event.Kind;
                if (k == EventKind.Hitch) { hitches++; continue; }
                if (k == EventKind.FiberContact || k == EventKind.FiberSlip)
                {
                    byte mk = (byte)(e.Event.Index & 0x7F);
                    if (mk == 0 || e.Event.Object < 0) continue;
                    string name = e.Event.Object >= 0 ? ((world as MapWorld)?.ObjectName(e.Event.Object) ?? "?") : "-";
                    string key = $"{(Fpv450.Sim.Tether.FiberMember)mk} obj {e.Event.Object} '{name}'";
                    members[key] = members.TryGetValue(key, out int m) ? m + 1 : 1;
                    if (k == EventKind.FiberContact) fiberContacts++;
                    w.WriteLine($"fiber t={e.Step * 0.001:0.000} {k} {(Fpv450.Sim.Tether.FiberMember)mk} obj {e.Event.Object} '{name}' shape {e.Event.Shape} at ({e.Event.Point.X:0.00},{e.Event.Point.Y:0.00},{e.Event.Point.Z:0.00}) agl {e.Event.Point.Y - Ground(e.Event.Point.X, e.Event.Point.Z):0.00} A {e.Event.A:G4} B {e.Event.B:G4} C {e.Event.C:G4}");
                    continue;
                }
                if (k == EventKind.StemEpisode || k == EventKind.MixerSaturation || k == EventKind.BladeStrike || k == EventKind.ContactMade
                    || k == EventKind.ContactBroken || k == EventKind.Payout || k == EventKind.VortexRingEntered || k == EventKind.VortexRingLeft) continue;
                w.WriteLine($"ev t={e.Step * 0.001:0.000} {sum.Describe(e.Event)}");
            }
            w.WriteLine($"hitches {hitches}");
            foreach (var kv in members.OrderByDescending(x => x.Value)) w.WriteLine($"member {kv.Value,6} {kv.Key}");
            int lastTrail = -1, drops = 0, dropped = 0, maxTrail = 0;
            long steps = 0;
            double paid = 0;
            using (FlightLogReader r = FlightLogReader.Open(log))
                while (r.Next())
                {
                    if (r.Kind != LogFormat.StepBlock) continue;
                    r.ReadStep(out LoggedStep s);
                    long k = s.State.Step;
                    steps = k;
                    int trail = s.Trace.Tether.Trail;
                    if (trail > maxTrail) maxTrail = trail;
                    Double3 p = s.State.Position, v = s.State.Velocity, wind = s.Trace.Wind;
                    if (lastTrail >= 0 && trail < lastTrail)
                    {
                        drops++;
                        dropped += lastTrail - trail;
                        w.WriteLine($"  trail-drop t={k * 0.001:0.000} {lastTrail}->{trail} drone ({p.X:0.0},{p.Y:0.0},{p.Z:0.0}) agl {p.Y - Ground(p.X, p.Z):0.0} |v| {v.Length():0.0} exitT {s.Trace.Tether.ExitTension:0.000} nodes {s.Trace.Tether.Nodes}");
                    }
                    lastTrail = trail;
                    paid = s.Trace.Tether.PaidOut;
                    if (k % 500 != 0) continue;
                    double up = s.State.Attitude.Rotate(new Double3(0, 1, 0)).Y;
                    w.WriteLine($"  t={k * 0.001:0.000} p=({p.X:0.0},{p.Y:0.0},{p.Z:0.0}) agl={p.Y - Ground(p.X, p.Z):0.00} |v|={v.Length():0.0} up={up:0.00} thr={s.Stick.Throttle:0.00} wind=({wind.X:0.0},{wind.Y:0.0},{wind.Z:0.0}) T={s.Trace.Tether.ExitTension:0.000} paid={s.Trace.Tether.PaidOut:0.0} nodes={s.Trace.Tether.Nodes} trail={trail}");
                }
            string row = $"{head} | steps {steps} | paid {paid:0.0} m | trail max {maxTrail} last {lastTrail} drops {drops} ({dropped} points) | fiber contacts with world members {fiberContacts} | {brk}";
            index.WriteLine(row);
            Console.WriteLine(row);
        }
        return 0;
    }
}
