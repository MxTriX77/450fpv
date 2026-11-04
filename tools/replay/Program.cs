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
            + "  replay <log> [--drone <drone.json>] [--materials <physics_materials.json>] [--surfaces <surfaces.json>] [--tether <tether.json>]\n"
            + "                                         replay the flight; compare every record bit for bit, on the world\n"
            + "                                         the log names: 'floor', or the repository's maps/<id>\n"
            + "  replay --summary <log>                 the flight's summary; a cut log reads to its last whole record\n"
            + "  replay --record <log> [--seconds S] [--realtime] [--wind still|calm|windy|severe] [--rain sunny|rain|severerain] [--wakes | --canopy] [--map <id> [--foot x,y,z,heading]] [--drone <drone.json>] [--line | --tether <tether.json>] [--disarm-at-touch] [--descent M/S]\n"
            + "  replay --record <log> --graze [--drone <drone.json>]\n"
            + "                                         fly the test range's wall graze (p7f-damage 1.3: a blade strike\n"
            + "                                         that chips the front-left blades, the drone flying on) and log it\n"
            + "  replay --record <log> --tip [--disarm] [--rain sunny|rain|severerain] [--drone <drone.json>]\n"
            + "                                         fly a landing tipped over onto the test range's S04 soil (p7f-damage\n"
            + "                                         1.3's soil carve: the lower props dig into it) and log it\n"
            + "                                         fly the scripted flight (120 s, Windy) and log it, on the stub's\n"
            + "                                         floor or from the legs-on start of the repository's maps/<id>\n"
            + "                                         (or the foot and heading --foot gives) in the map's own wind\n"
            + "                                         (its wakes, its belt's canopy and its cuts' cavity flow); --line\n"
            + "                                         flies it on the fiber (the repository's tether.json, or the file given);\n"
            + "                                         --disarm-at-touch disarms as the legs first touch, not 2.5 s after;\n"
            + "                                         --descent: its last metres' descent (0.5 m/s; 1.9 lands hard\n"
            + "                                         enough to bend a leg, p7g-legs 1.2)\n"
            + "  replay --record <log> --legs [--drone <drone.json>]\n"
            + "                                         p7g-legs 1.3's damage flight: the free body up into a bar that\n"
            + "                                         slides leg_rr out, a fabric cushion over it, down onto a straw\n"
            + "                                         pad: one leg lost, the other three bent and broken (on the\n"
            + "                                         legs' lever since model 49), their pieces loose\n"
            + "exit: 0 match, 1 diverged, 2 usage, 3 refused, 4 unreadable, 5 matched a cut log\n"
            + "drone.json, physics_materials.json and surfaces.json default to the repository's assets-src/drone/drone.json,\n"
            + "assets-src/physics_materials.json and assets-src/surfaces.json, and tether.json, for a log with the line, to its\n"
            + "assets-src/tether.json";

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
            ulong materialsHash = 0;
            string materials = Option(args, "--materials") ?? (h.Contacts != null && h.Contacts.MaterialsHash != 0 ? ScriptedFlight.RepositoryMaterials() : null);
            if (materials != null)
            {
                materialsHash = ScriptedFlight.MaterialsHash(materials, out string problem);
                if (problem != null)
                {
                    Console.WriteLine("refused: " + problem);
                    return (int)ReplayOutcome.Refused;
                }
                Console.WriteLine($"  physics_materials.json {materials}: 0x{materialsHash:X16}");
            }
            ulong surfacesHash = 0;
            string surfaces = Option(args, "--surfaces") ?? (h.Contacts?.Surfaces != null ? ScriptedFlight.RepositorySurfaces() : null);
            if (surfaces != null)
            {
                surfacesHash = ScriptedFlight.SurfacesHash(surfaces, out string problem);
                if (problem != null)
                {
                    Console.WriteLine("refused: " + problem);
                    return (int)ReplayOutcome.Refused;
                }
                Console.WriteLine($"  surfaces.json {surfaces}: 0x{surfacesHash:X16}");
            }
            ulong tetherHash = 0;
            string tether = Option(args, "--tether") ?? (h.Tether != null ? ScriptedFlight.RepositoryTether() : null);
            if (tether != null)
            {
                tetherHash = ScriptedFlight.TetherHash(tether, out string problem);
                if (problem != null)
                {
                    Console.WriteLine("refused: " + problem);
                    return (int)ReplayOutcome.Refused;
                }
                Console.WriteLine($"  tether.json {tether}: 0x{tetherHash:X16}");
            }
            FlightWorld world = ScriptedFlight.OpenWorld(h.WorldId, out string where);
            if (world == null)
            {
                Console.WriteLine("refused: this replay cannot open the world '" + h.WorldId + "': " + where);
                return (int)ReplayOutcome.Refused;
            }
            Console.WriteLine($"  world '{h.WorldId}' from {where}: 0x{world.ContentHash:X16}");

            var clock = Stopwatch.StartNew();
            ReplayResult r = FlightReplay.Run(reader, world, droneHash, Host.Isa, surfacesHash, tetherHash, materialsHash);
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
            if (Array.IndexOf(args, "--legs") >= 0)
            {
                string legsDrone = Option(args, "--drone") ?? ScriptedFlight.RepositoryDrone()
                    ?? throw new ArgumentException("no drone.json found; pass --drone");
                ScriptedFlight.Recorded d = ScriptedFlight.FlyLegs(path, legsDrone);
                Console.WriteLine($"record {Path.GetFileName(path)}: the legs' damage flight on '{ScriptedFlight.PadsWorld}', {d.Counts.Steps} steps, "
                    + $"{d.Counts.Events} events, {d.Bytes} bytes; seed 0x{d.Seed:X16}, drone.json 0x{d.DroneHash:X16}");
                Console.WriteLine("  " + d.Landing);
                return d.Fault == null ? 0 : 1;
            }
            string seconds = Option(args, "--seconds");
            double duration = seconds == null ? 120 : double.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
            if (!(duration > 0 && duration <= 3600)) throw new ArgumentException("--seconds must be in (0, 3600]");
            string drone = Option(args, "--drone") ?? ScriptedFlight.RepositoryDrone()
                ?? throw new ArgumentException("no drone.json found; pass --drone");
            bool realtime = Array.IndexOf(args, "--realtime") >= 0;
            string level = Option(args, "--wind") ?? "windy";
            if (!Enum.TryParse(level, true, out WindLevel wind) || wind == WindLevel.Steady)
                throw new ArgumentException("--wind is still, calm, windy or severe");
            if (!Enum.TryParse(Option(args, "--rain") ?? "sunny", true, out RainLevel rain) || !Enum.IsDefined(typeof(RainLevel), rain))
                throw new ArgumentException("--rain is sunny, rain or severerain");

            string map = Option(args, "--map"), footText = Option(args, "--foot");
            double[] foot = null;
            if (footText != null)
            {
                if (map == null) throw new ArgumentException("--foot needs --map");
                string[] parts = footText.Split(',');
                if (parts.Length != 4) throw new ArgumentException("--foot is x,y,z,heading");
                foot = Array.ConvertAll(parts, p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture));
            }
            string tether = Option(args, "--tether") ?? (Array.IndexOf(args, "--line") >= 0
                ? ScriptedFlight.RepositoryTether() ?? throw new ArgumentException("no tether.json found; pass --tether") : null);
            if (Array.IndexOf(args, "--graze") >= 0)
            {
                ScriptedFlight.Recorded g = ScriptedFlight.FlyGraze(path, drone, out string outcome);
                Console.WriteLine($"record {Path.GetFileName(path)}: the graze along H01_D's south wall on the test range, {g.Counts.Steps} steps, "
                    + $"{g.Counts.Events} events, {g.Bytes} bytes; {g.WallSeconds:F2} s of wall time");
                Console.WriteLine("  " + outcome);
                if (g.Fault == null) return 0;
                Console.WriteLine("  the log failed: " + g.Fault.Message);
                return 1;
            }
            if (Array.IndexOf(args, "--tip") >= 0)
            {
                ScriptedFlight.Recorded t = ScriptedFlight.FlyTip(path, drone, rain, Array.IndexOf(args, "--disarm") >= 0, out string outcome);
                Console.WriteLine($"record {Path.GetFileName(path)}: the tipped landing onto S04's soil on the test range, {rain}, {t.Counts.Steps} steps, "
                    + $"{t.Counts.Events} events, {t.Bytes} bytes; {t.WallSeconds:F2} s of wall time");
                Console.WriteLine("  " + outcome);
                if (t.Fault == null) return 0;
                Console.WriteLine("  the log failed: " + t.Fault.Message);
                return 1;
            }
            string descent = Option(args, "--descent");
            double last = descent == null ? 0.5 : double.Parse(descent, System.Globalization.CultureInfo.InvariantCulture);
            if (!(last > 0 && last <= 2)) throw new ArgumentException("--descent must be in (0, 2] m/s");
            ScriptedFlight.Recorded r = ScriptedFlight.Fly(path, drone, (long)(duration * 1000), realtime, wind, Array.IndexOf(args, "--wakes") >= 0, rain,
                Array.IndexOf(args, "--canopy") >= 0, map, foot, tether, Array.IndexOf(args, "--disarm-at-touch") >= 0, last);
            LogEnd c = r.Counts;
            double perStep = (double)(r.Bytes - r.HeaderBytes) / c.Steps;
            Console.WriteLine($"record {Path.GetFileName(path)}: {(realtime ? "real time on the flight thread" : "virtual clock, the scheduler and the flush worker")}, {map ?? "floor"}, {wind} wind, {rain}, "
                + $"{c.Steps} steps ({c.Steps / 1000.0:F3} s), {c.Events} events, {c.Hitches} scheduler hitches, {c.LogWaits} log waits, "
                + $"{c.SticksDropped} stick reports dropped");
            Console.WriteLine($"  {r.Bytes} bytes: header {r.HeaderBytes} B, then {perStep:F1} B a step = {perStep * 60000 / 1e6:F2} MB a minute; "
                + $"seed 0x{r.Seed:X16}, drone.json 0x{r.DroneHash:X16}; {r.WallSeconds:F2} s of wall time");
            Console.WriteLine("  landing: " + r.Landing);
            if (r.Line != null) Console.WriteLine("  the line: " + r.Line);
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
