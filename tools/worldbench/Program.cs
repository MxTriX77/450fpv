using System;

namespace Fpv450.WorldBench
{
    // World-query golden files and benchmarks. No cases are registered yet; the
    // entry points exist so the verification commands in CLAUDE.md are real.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--golden")
            {
                Console.WriteLine("worldbench: 0 golden cases registered");
                return 0;
            }
            if (args.Length == 1 && args[0] == "--bench")
            {
                Console.WriteLine("worldbench: 0 bench cases registered");
                return 0;
            }
            Console.Error.WriteLine("usage: worldbench --golden | --bench");
            return 2;
        }
    }
}
