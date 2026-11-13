using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using static Fpv450.Sim.Tests.SweepScenarios;

namespace Fpv450.FlightBench
{
    // The map-wide landing sweep (w9-village-physics 3.0): `--sweep <map> <points> <seed>` draws the points (SweepScenarios.Draw),
    // flies the four flights at each (Land with its Liftoff, Cut, Drop, Slide) on --threads threads, each flight its own core, and
    // prints the counts per invariant by map, class, slope band, stem stratum and manoeuvre, then every failing row with the command
    // that replays it. `--point <i>` flies point i alone and prints its rows in full (`--from <i>` the points from i on); `--out <file>` writes every row (TSV);
    // `--wind <level>` flies another level (Calm by default); `--no-replay` leaves the replays out. The rows' SHA-256 is printed: the
    // same seed gives the same rows on any thread count.
    internal static class SweepCase
    {
        public static int Run(string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[2], out int count) || count < 1
                || !ulong.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
                return Usage();
            string mapId = args[1], outFile = null;
            int threads = Math.Max(1, Environment.ProcessorCount / 2), only = -1, from = 0;
            WindLevel level = WindLevel.Calm;
            bool replay = true;
            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--threads" && i + 1 < args.Length && int.TryParse(args[++i], out threads) && threads >= 1) continue;
                if (args[i] == "--point" && i + 1 < args.Length && int.TryParse(args[++i], out only) && only >= 0) continue;
                if (args[i] == "--from" && i + 1 < args.Length && int.TryParse(args[++i], out from) && from >= 0) continue;
                if (args[i] == "--out" && i + 1 < args.Length) { outFile = args[++i]; continue; }
                if (args[i] == "--wind" && i + 1 < args.Length && Enum.TryParse(args[++i], true, out level)) continue;
                if (args[i] == "--no-replay") { replay = false; continue; }
                return Usage();
            }

            var clock = Stopwatch.StartNew();
            MapWorld map = MapWorld.Load(RangeScenarios.Root, mapId);
            var context = new Context(map, PoweredFlightCase.Drone(), level);
            Point[] points = Draw(map, count, seed, out long candidates);
            double drawn = clock.Elapsed.TotalSeconds;
            int quota = 0;
            foreach (Point p in points) if (p.Quota) quota++;
            Console.WriteLine($"flightbench: the landing sweep on {mapId}, seed {seed}, {level} air, L1 (2 kg, legs on), every model but the line; "
                + $"{points.Length} points ({count} in proportion to area, {quota} more to reach {MinPerClass} a class, band and stratum) from {candidates} candidates "
                + $"in {drawn:F1} s; {threads} threads{(replay ? "" : ", no replays")}; {Power.Describe()}");

            var jobs = new List<(int Point, Manoeuvre M)>();
            foreach (Point p in points)
                if (only < 0 ? p.Index >= from : p.Index == only)
                    foreach (Manoeuvre m in new[] { Manoeuvre.Land, Manoeuvre.Cut, Manoeuvre.Drop, Manoeuvre.Slide })
                        jobs.Add((p.Index, m));
            if (jobs.Count == 0)
            {
                Console.Error.WriteLine($"flightbench: no point {only} among the {points.Length} drawn");
                return 2;
            }
            var results = new Row[jobs.Count][];
            int done = 0;
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, j =>
            {
                results[j] = Fly(context, points[jobs[j].Point], jobs[j].M, seed, replay: replay);
                int d = System.Threading.Interlocked.Increment(ref done);
                if (d % 100 == 0) Console.Error.WriteLine($"  {d} of {jobs.Count} flights, {clock.Elapsed.TotalMinutes:F1} min");
            });
            double flown = clock.Elapsed.TotalSeconds - drawn;

            var rows = new List<Row>();
            foreach (Row[] r in results) rows.AddRange(r);
            var text = new StringBuilder(Row.Header()).Append('\n');
            var physics = new StringBuilder();
            foreach (Row r in rows)
            {
                text.Append(r.Line()).Append('\n');
                physics.Append(r.Line(alloc: false)).Append('\n');
            }
            string hash; // the allocation column left out (Row.Line)
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(physics.ToString()))).Replace("-", "").ToLowerInvariant();
            if (outFile != null) File.WriteAllText(outFile, text.ToString());

            if (only >= 0)
                foreach (Row r in rows) Console.WriteLine("  " + r.Line().Replace('\t', ' ') + "\n    events: " + r.Events);

            Tables(mapId, rows);
            int failing = 0;
            string command = $"dotnet run -c Release --project tools/flightbench -- --sweep {mapId} {count} {seed}" + (level != WindLevel.Calm ? $" --wind {level}" : "");
            Console.WriteLine("  failing rows (point, manoeuvre, x, z, surface, stems, slope, heading, seed: each failed check at its first step; replay):");
            foreach (Row r in rows)
            {
                if (r.Skipped || r.Pass) continue;
                failing++;
                var s = new StringBuilder();
                for (int i = 0; i < Checks; i++) if (r.Failed[i]) s.Append(s.Length > 0 ? ", " : "").Append((Check)i).Append(" @").Append(r.First[i]);
                Console.WriteLine($"    #{r.Point.Index} {r.Manoeuvre} ({r.Point.X:F2}, {r.Point.Z:F2}) {r.Point.Surface}/{r.Point.Stems ?? "-"} {r.Point.Slope:F2}° "
                    + $"heading {r.Heading:F1}° seed {r.Seed:X16}: {s}; {r.Detail}; `{command} --point {r.Point.Index}`");
            }
            Console.WriteLine($"flightbench: sweep {rows.Count} rows, {failing} failing; drawing {drawn:F1} s, flying {flown:F1} s "
                + $"({flown / Math.Max(1, jobs.Count) * threads:F2} s a flight a thread); rows SHA-256 without the allocation column {hash}");
            if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so the times are not evidence (PLT-07)");
            return failing == 0 ? 0 : 1;
        }

        // The counts: rows judged and failed per check, by manoeuvre, surface class, slope band and stem stratum.
        static void Tables(string mapId, List<Row> rows)
        {
            void Table(string title, Func<Row, string> key)
            {
                var keys = new SortedDictionary<string, (int Rows, int[] Judged, int[] Failed)>(StringComparer.Ordinal);
                foreach (Row r in rows)
                {
                    if (r.Skipped) continue;
                    string k = key(r);
                    if (k == null) continue;
                    if (!keys.TryGetValue(k, out var e)) e = (0, new int[Checks], new int[Checks]);
                    e.Rows++;
                    for (int i = 0; i < Checks; i++)
                    {
                        if (r.Judged[i]) e.Judged[i]++;
                        if (r.Failed[i]) e.Failed[i]++;
                    }
                    keys[k] = e;
                }
                var head = new StringBuilder($"  {title,-22} rows");
                for (int i = 0; i < Checks; i++) head.Append($" {(Check)i,10}");
                Console.WriteLine(head);
                foreach (var kv in keys)
                {
                    var line = new StringBuilder($"    {kv.Key,-20} {kv.Value.Rows,4}");
                    for (int i = 0; i < Checks; i++) line.Append($" {(kv.Value.Judged[i] == 0 ? "-" : kv.Value.Failed[i] + "/" + kv.Value.Judged[i]),10}");
                    Console.WriteLine(line);
                }
            }
            int skipped = 0;
            foreach (Row r in rows) if (r.Skipped) skipped++;
            Console.WriteLine($"  failed/judged per check ({skipped} rows skipped):");
            Table("map", r => mapId);
            Table("manoeuvre", r => r.Manoeuvre.ToString());
            Table("surface", r => r.Point.Surface);
            Table("slope band", r => BandName(r.Point.Band));
            if (mapId == "farmland") Table("stems", r => r.Point.Stems);
        }

        static int Usage()
        {
            Console.Error.WriteLine("usage: flightbench --sweep <farmland | test-range> <points> <seed> [--threads N] [--point I | --from I] [--out FILE] [--wind LEVEL] [--no-replay]");
            return 2;
        }
    }
}
