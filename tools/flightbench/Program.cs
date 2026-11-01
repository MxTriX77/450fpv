using System;

namespace Fpv450.FlightBench
{
    // Flight-model golden files and benchmarks (sim-core-build): `--golden` checks every case against its pinned
    // states, `--bench` times them and prints the power state next to the numbers (PLT-07), `--scenarios` flies the
    // aero and wind scenarios (p6-flight-core 1.5) with their known-wrong models, `--contacts` the ground-contact scenarios
    // on the stub (1.6), `--wind-harness` K§8.5's acceptance harness (p7c-wind 1.2), `--shake [first seed]` the
    // throttle-punch shake's distribution on 40 seeds a cell (1–40 by default) and PHY-10's claims on them, failing if one
    // misses. `--probe` runs the world-query contact probe (p6-world-query-gallery 1.2); `--probe --break <piece>` runs it
    // with a known-wrong piece, which must fail.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--golden")
            {
                Console.WriteLine("flightbench: 4 golden cases registered");
                bool ok = FreeBodyCase.Golden();
                ok &= PoweredFlightCase.Golden();
                return ok ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--bench")
            {
                Console.WriteLine("flightbench: 9 bench cases registered; " + Power.Describe());
                bool ok = FreeBodyCase.Bench();
                ok &= MassRebuildCase.Bench();
                ok &= PoweredFlightCase.Bench();
                ok &= LoggedStepCase.Bench();
                ok &= ContactProbeCase.Bench();
                ok &= ContactsCase.Bench();
                ok &= CanopyStepCase.Bench();
                ok &= PosadkaStepCase.Bench();
                ok &= AirframeCase.Bench();
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
                Console.WriteLine("flightbench: the ground-contact scenarios on the stub (p6-flight-core 1.6)");
                return ContactsCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args[0] == "--wind-harness")
                return WindHarnessCase.Run(args);
            if (args.Length >= 1 && args[0] == "--probe")
            {
                var piece = ContactProbeCase.Break.None;
                if (args.Length == 1 || args.Length == 3 && args[1] == "--break" && Enum.TryParse(args[2], true, out piece))
                    return ContactProbeCase.Run(piece) ? 0 : 1;
            }
            Console.Error.WriteLine("usage: flightbench --golden | --bench | --scenarios | --shake [first seed] | --contacts | --probe [--break latch|margin|refresh|spheres|disc] | --wind-harness <level> [...]");
            return 2;
        }
    }
}
