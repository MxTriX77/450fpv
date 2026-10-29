using System;

namespace Fpv450.FlightBench
{
    // Flight-model golden files and benchmarks (sim-core-build): `--golden` checks every case against its pinned
    // states, `--bench` times them and prints the power state next to the numbers (PLT-07), `--scenarios` flies the
    // aero and wind scenarios (p6-flight-core 1.5) with their known-wrong models, `--contacts` the ground-contact scenarios
    // on the stub (1.6). `--probe` runs the world-query contact
    // probe (p6-world-query-gallery 1.2); `--probe --break <piece>` runs it with a known-wrong piece, which must fail.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--golden")
            {
                Console.WriteLine("flightbench: 3 golden cases registered");
                bool ok = FreeBodyCase.Golden();
                ok &= PoweredFlightCase.Golden();
                return ok ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--bench")
            {
                Console.WriteLine("flightbench: 6 bench cases registered; " + Power.Describe());
                bool ok = FreeBodyCase.Bench();
                ok &= MassRebuildCase.Bench();
                ok &= PoweredFlightCase.Bench();
                ok &= LoggedStepCase.Bench();
                ok &= ContactProbeCase.Bench();
                ok &= ContactsCase.Bench();
                if (!Power.OnAc) Console.WriteLine("flightbench: not on AC power, so these numbers are not evidence (PLT-07)");
                return ok ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--scenarios")
            {
                Console.WriteLine("flightbench: the aero and wind scenarios (p6-flight-core 1.5)");
                return ScenariosCase.Run() ? 0 : 1;
            }
            if (args.Length == 1 && args[0] == "--contacts")
            {
                Console.WriteLine("flightbench: the ground-contact scenarios on the stub (p6-flight-core 1.6)");
                return ContactsCase.Run() ? 0 : 1;
            }
            if (args.Length >= 1 && args[0] == "--probe")
            {
                var piece = ContactProbeCase.Break.None;
                if (args.Length == 1 || args.Length == 3 && args[1] == "--break" && Enum.TryParse(args[2], true, out piece))
                    return ContactProbeCase.Run(piece) ? 0 : 1;
            }
            Console.Error.WriteLine("usage: flightbench --golden | --bench | --scenarios | --contacts | --probe [--break latch|margin|refresh|spheres|disc]");
            return 2;
        }
    }
}
