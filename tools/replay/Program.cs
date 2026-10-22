using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Replay
{
    // tools/replay (design 10, LOG-02): the headless bit-exact replay of a flight log, its summary, and the scripted
    // flight that exercises both. The exit code is the replay's ReplayOutcome.
    internal static class Program
    {
        const string Usage =
            "usage:\n"
            + "  replay <log> [--drone <drone.json>]   replay the flight; compare every record bit for bit\n"
            + "  replay --summary <log>                 the flight's summary; a cut log reads to its last whole record\n"
            + "  replay --record <log> [--seconds S] [--realtime] [--wind still|calm|windy|severe] [--drone <drone.json>]\n"
            + "                                         fly the scripted flight (120 s, Windy) and log it\n"
            + "exit: 0 match, 1 diverged, 2 usage, 3 refused, 4 unreadable, 5 matched a cut log\n"
            + "drone.json defaults to the repository's assets-src/drone/drone.json";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2 && args[0] == "--summary") return Summary(args[1]);
                if (args.Length >= 2 && args[0] == "--record") return Record(args);
                if (args.Length >= 1 && !args[0].StartsWith("--", StringComparison.Ordinal)) return Replay(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine("replay: " + e.Message);
            }
            Console.Error.WriteLine(Usage);
            return 2;
        }

        static int Replay(string[] args)
        {
            string path = args[0];
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            FlightLogReader reader;
            try
            {
                reader = new FlightLogReader(file);
            }
            catch (FlightLogException e)
            {
                Console.WriteLine("replay " + Path.GetFileName(path) + ": unreadable: " + e.Message);
                return (int)ReplayOutcome.Unreadable;
            }
            LogHeader h = reader.Header;
            Console.WriteLine($"replay {Path.GetFileName(path)}: seed 0x{h.Seed:X16}, model {h.ModelVersion}, world '{h.WorldId}' 0x{h.WorldHash:X16}, "
                + $"drone.json 0x{h.DroneHash:X16}; recorded by {h.Build} on {h.Runtime}, isa {h.Isa}");

            ulong droneHash = 0;
            string drone = Option(args, "--drone") ?? (h.DroneHash != 0 ? ScriptedFlight.RepositoryDrone() : null);
            if (drone != null)
            {
                droneHash = ScriptedFlight.DroneHash(drone, out string problem);
                if (problem != null)
                {
                    Console.WriteLine("refused: " + problem);
                    return (int)ReplayOutcome.Refused;
                }
                Console.WriteLine($"  drone.json {drone}: 0x{droneHash:X16}");
            }
            FlightWorld world = ScriptedFlight.OpenWorld(h.WorldId);
            if (world == null)
            {
                Console.WriteLine("refused: this replay cannot open the world '" + h.WorldId + "'");
                return (int)ReplayOutcome.Refused;
            }

            var clock = Stopwatch.StartNew();
            ReplayResult r = FlightReplay.Run(reader, world, droneHash, Host.Isa);
            Console.WriteLine("  " + r.Message);
            foreach (string note in r.Notes) Console.WriteLine("  note: " + note);
            Console.WriteLine($"  replayed in {clock.Elapsed.TotalSeconds:F2} s on {LogHeader.CurrentRuntime()}, isa {Host.Isa}; exit {(int)r.Outcome} ({r.Outcome})");
            return (int)r.Outcome;
        }

        static int Summary(string path)
        {
            FlightSummary s = FlightSummary.Read(path);
            s.Print(Console.Out);
            return s.Header == null ? (int)ReplayOutcome.Unreadable : s.Complete ? 0 : (int)ReplayOutcome.MatchCut;
        }

        static int Record(string[] args)
        {
            string path = args[1];
            string seconds = Option(args, "--seconds");
            double duration = seconds == null ? 120 : double.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
            if (!(duration > 0 && duration <= 3600)) throw new ArgumentException("--seconds must be in (0, 3600]");
            string drone = Option(args, "--drone") ?? ScriptedFlight.RepositoryDrone()
                ?? throw new ArgumentException("no drone.json found; pass --drone");
            bool realtime = Array.IndexOf(args, "--realtime") >= 0;
            string level = Option(args, "--wind") ?? "windy";
            if (!Enum.TryParse(level, true, out WindLevel wind) || wind == WindLevel.Steady)
                throw new ArgumentException("--wind is still, calm, windy or severe");

            ScriptedFlight.Recorded r = ScriptedFlight.Fly(path, drone, (long)(duration * 1000), realtime, wind);
            LogEnd c = r.Counts;
            double perStep = (double)(r.Bytes - r.HeaderBytes) / c.Steps;
            Console.WriteLine($"record {Path.GetFileName(path)}: {(realtime ? "real time on the flight thread" : "virtual clock, the scheduler and the flush worker")}, {wind} wind, "
                + $"{c.Steps} steps ({c.Steps / 1000.0:F3} s), {c.Events} events, {c.Hitches} scheduler hitches, {c.LogWaits} log waits, "
                + $"{c.SticksDropped} stick reports dropped");
            Console.WriteLine($"  {r.Bytes} bytes: header {r.HeaderBytes} B, then {perStep:F1} B a step = {perStep * 60000 / 1e6:F2} MB a minute; "
                + $"seed 0x{r.Seed:X16}, drone.json 0x{r.DroneHash:X16}; {r.WallSeconds:F2} s of wall time");
            if (r.Fault != null)
            {
                Console.WriteLine("  the log failed: " + r.Fault.Message);
                return 1;
            }
            return 0;
        }

        static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0) return null;
            if (i + 1 >= args.Length) throw new ArgumentException(name + " needs a value");
            return args[i + 1];
        }
    }
}
