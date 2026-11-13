using System;
using System.Globalization;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // Flight-model golden files and benchmarks (sim-core-build): `--golden` checks every case against its pinned
    // states, `--bench` times them and prints the power state next to the numbers (PLT-07), `--scenarios` flies the
    // aero and wind scenarios (p6-flight-core 1.5) with their known-wrong models, `--contacts` the ground-contact scenarios
    // on the stub (1.6) and on the test range (1.6b), `--wind-harness` K§8.5's acceptance harness (p7c-wind 1.2), `--shake
    // [first seed]` the throttle-punch shake's distribution on 40 seeds a cell (1–40 by default) and PHY-10's claims on
    // them, failing if one misses. `--landing` holds the touchdowns' toppling and the coil's rock against rigid-body
    // mechanics (1.6c, D-067). `--factory [seeds] [first]` flies the factory imbalance's baseline shake on 20 000 seeds by
    // default (p7f-damage 1.2). `--damage [seeds]` flies the damage harness's first cells along H01_D's wall, 8 seeds a cell by
    // default (p7f-damage 1.3). `--legs [seeds]` flies the legs' outcomes over touchdown speed, roll and drift, 40 seeds a
    // cell by default, with the clamp's preload and the tube's yield held at their bounds and the known-wrong models
    // (p7g-legs 1.4). `--probe` runs the world-query contact probe (p6-world-query-gallery 1.2); `--probe
    // --break <piece>` runs it with a known-wrong piece, which must fail. `--rate-tracking` flies stick steps on the session's
    // drone and prints the setpoint against the achieved rate and what limits it; `--rate-log <log>...` reads flight logs so.
    // `--turns [calm | windy | severe]` flies turning routes low over the farmland's bushes and judges the laid line's rest
    // (w3-farmland-ground 2.4); `--bench farmland-line-step [calm | windy | severe]` flies the line case in that wind.
    // `--tether [seeds]` flies the fiber's acceptance harness, the pilot's four causes on 20 seeds a row by default, and judges
    // design 16's targets and the known-wrong models (p7d-tether 1.11).
    // `--replay-step <log> [--from S] [--to S]` replays a flight log bit for bit and times its steps and their world queries
    // in that window of flight seconds against PLT-04 (w3-farmland-ground 2.1). `--ground [seeds] [--nominal-legs]` flies the liftoff harness on
    // Village's and House's grounds with its known-wrong models and Village's ground rows (w9-village-physics 2.9).
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--golden")
            {
                Console.WriteLine("flightbench: 6 golden cases registered");
                bool ok = FreeBodyCase.Golden();
                ok &= PoweredFlightCase.Golden();
                return ok ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--bench")
            {
                Console.WriteLine("flightbench: 14 bench cases registered; " + Power.Describe());
                bool ok = FreeBodyCase.Bench();
                ok &= MassRebuildCase.Bench();
                ok &= PoweredFlightCase.Bench();
                ok &= LoggedStepCase.Bench();
                ok &= ContactProbeCase.Bench();
                ok &= ContactsCase.Bench();
                ok &= CanopyStepCase.Bench();
                ok &= PosadkaStepCase.Bench();
                ok &= PosadkaStepCase.BenchMap();
                ok &= PosadkaStepCase.BenchCut();
                ok &= AirframeCase.Bench();
                ok &= HousesStepCase.Bench();
                ok &= HousesWindowCase.Bench();
                ok &= FarmlandLineCase.Bench(WindLevel.Severe);
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--bench" && args[1] == HousesWindowCase.Name)
            {
                Console.WriteLine("flightbench: " + HousesWindowCase.Name + " alone; " + Power.Describe());
                bool ok = HousesWindowCase.Bench();
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--bench" && args[1] == HousesStepCase.Name)
            {
                Console.WriteLine("flightbench: " + HousesStepCase.Name + " alone; " + Power.Describe());
                bool ok = HousesStepCase.Bench();
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length >= 2 && args.Length <= 3 && args[0] == "--bench" && args[1] == FarmlandLineCase.Name)
            {
                WindLevel level = WindLevel.Severe;
                if (args.Length == 3 && !Enum.TryParse(args[2], true, out level))
                {
                    Console.Error.WriteLine("usage: flightbench --bench " + FarmlandLineCase.Name + " [calm | windy | severe]");
                    return 2;
                }
                Console.WriteLine("flightbench: " + FarmlandLineCase.Name + " alone; " + Power.Describe());
                bool ok = FarmlandLineCase.Bench(level);
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--bench" && args[1] == BuildCase.Name)
            {
                Console.WriteLine("flightbench: " + BuildCase.Name + " alone; " + Power.Describe());
                bool ok = BuildCase.Bench();
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--turns")
            {
                WindLevel level = WindLevel.Windy;
                if (args.Length == 2 && !Enum.TryParse(args[1], true, out level))
                {
                    Console.Error.WriteLine("usage: flightbench --turns [calm | windy | severe]");
                    return 2;
                }
                bool ok = TurnsCase.Run(level);
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--scenarios")
            {
                Console.WriteLine("flightbench: the aero and wind scenarios (p6-flight-core 1.5)");
                return ScenariosCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--shake")
            {
                int first = 1;
                if (args.Length == 1 || int.TryParse(args[1], out first) && first >= 1)
                {
                    Console.WriteLine("flightbench: the throttle-punch shake's distribution and PHY-10's claims (p7b-airframe)");
                    return ShakeCase.Run(first) ? 0 : 1;
                }
            }
            if (args.Length == 1 && args[0] == "--contacts")
            {
                Console.WriteLine("flightbench: the ground-contact scenarios on the stub (p6-flight-core 1.6) and the test range (1.6b)");
                return ContactsCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--slopes")
            {
                Console.WriteLine("flightbench: the legs, a crash and a prop strike on slopes of 0–38° and at the farmland's ledge (w3-farmland-ground 2.2)");
                return SlopesCase.Run(args.Length == 2 ? args[1] : null) ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--landing")
            {
                Console.WriteLine("flightbench: landing stability against rigid-body mechanics (p6-flight-core 1.6c, D-067)");
                return LandingCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args.Length <= 3 && args[0] == "--factory")
            {
                int seeds = 20000, first = 1;
                if ((args.Length < 2 || int.TryParse(args[1], out seeds) && seeds >= 10) && (args.Length < 3 || int.TryParse(args[2], out first) && first >= 1))
                {
                    Console.WriteLine("flightbench: the factory imbalance's baseline shake (p7f-damage 1.2), " + seeds + " seeds' hovers");
                    return FactoryCase.Run(seeds, first) ? 0 : 1;
                }
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--damage")
            {
                int seeds = 8;
                if (args.Length < 2 || int.TryParse(args[1], out seeds) && seeds >= 1)
                {
                    Console.WriteLine("flightbench: blade strikes on H01_D's wall, the damage harness's first cells (p7f-damage 1.3), " + seeds + " seeds a cell");
                    return DamageCase.Run(seeds) ? 0 : 1;
                }
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--legs")
            {
                int seeds = 40;
                if (args.Length < 2 || int.TryParse(args[1], out seeds) && seeds >= 1)
                {
                    Console.WriteLine("flightbench: the legs' outcomes over touchdown speed, roll and drift (p7g-legs 1.4), " + seeds + " seeds a cell");
                    return LegsCase.Run(seeds) ? 0 : 1;
                }
            }
            if (args.Length >= 1 && args.Length <= 3 && args[0] == "--ground")
            {
                int seeds = 10;
                bool nominal = args[args.Length - 1] == "--nominal-legs";
                int counted = nominal ? args.Length - 1 : args.Length;
                if (counted == 1 || counted == 2 && int.TryParse(args[1], out seeds) && seeds >= 2)
                {
                    Console.WriteLine("flightbench: the liftoff harness and Village's ground rows (w9-village-physics 2.9, p7a-ground design 13), " + seeds + " seeds a cell");
                    return GroundCase.Run(seeds, nominal) ? 0 : 1;
                }
            }
            if (args.Length >= 1 && args[0] == "--wind-harness")
                return WindHarnessCase.Run(args);
            if (args.Length == 1 && args[0] == "--rate-tracking")
            {
                Console.WriteLine("flightbench: how the drone tracks its controller's setpoint (reference-notes/rate-tracking.md)");
                return RateTrackingCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--tether")
            {
                int seeds = 20;
                if (args.Length == 1 || int.TryParse(args[1], out seeds) && seeds >= 1)
                {
                    Console.WriteLine("flightbench: the fiber's acceptance harness, the pilot's four causes (p7d-tether 1.11)");
                    return TetherCase.Run(seeds) ? 0 : 1;
                }
            }
            if (args.Length == 1 && args[0] == "--pitch-back")
            {
                Console.WriteLine("flightbench: the climb after a pitch-back at a fixed throttle (D-087)");
                return PitchBackCase.Run() ? 0 : 1;
            }
            if (args.Length >= 2 && args[0] == "--rate-log")
                return RateTrackingCase.RunLogs(args[1..]) ? 0 : 1;
            if (args.Length >= 2 && args[0] == "--replay-step")
            {
                double from = 0, to = double.MaxValue;
                bool good = true;
                for (int i = 2; i + 1 < args.Length && good; i += 2)
                    if (args[i] == "--from") good = double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out from);
                    else if (args[i] == "--to") good = double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out to);
                    else good = false;
                if (good && args.Length % 2 == 0)
                    return ReplayStepCase.Run(args[1], from, to) ? 0 : 1;
            }
            if (args.Length >= 1 && args[0] == "--probe")
            {
                var piece = ContactProbeCase.Break.None;
                if (args.Length == 1 || args.Length == 3 && args[1] == "--break" && Enum.TryParse(args[2], true, out piece))
                    return ContactProbeCase.Run(piece) ? 0 : 1;
            }
            Console.Error.WriteLine("usage: flightbench --golden | --bench [houses-line-step | houses-window-step | farmland-line-step [calm | windy | severe]] | --turns [calm | windy | severe] | --scenarios | --shake [first seed] | --contacts | --slopes | --landing | --factory [seeds] [first seed] | --damage [seeds] | --legs [seeds] | --probe [--break latch|margin|refresh|spheres|disc] | --wind-harness <level> [...] | --rate-tracking | --pitch-back | --tether [seeds] | --rate-log <log>... | --replay-step <log> [--from S] [--to S]");
            return 2;
        }
    }
}
