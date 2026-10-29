using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Fpv450.Replay
{
    // This host's identity for the log header: the build and the ISA flags (the sim core finds the runtime itself).
    // Sim data only (LOG-05): no machine name, user or path.
    internal static class Host
    {
        public static readonly string Build = "tools/replay " + (typeof(Host).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");

        public static readonly string Isa = DetectIsa();

        static string DetectIsa()
        {
            var flags = new List<string> { RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant() };
            if (Sse2.IsSupported) flags.Add("sse2");
            if (Sse41.IsSupported) flags.Add("sse4.1");
            if (Avx.IsSupported) flags.Add("avx");
            if (Avx2.IsSupported) flags.Add("avx2");
            if (Fma.IsSupported) flags.Add("fma");
            if (Avx512F.IsSupported) flags.Add("avx512f");
            if (AdvSimd.IsSupported) flags.Add("advsimd");
            return string.Join(" ", flags);
        }

        [DllImport("winmm.dll")]
        static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        static extern uint timeEndPeriod(uint period);

        // The 1 ms timer period a game asks Windows for, so the flight thread's 1 ms sleeps last about 1 ms.
        public static IDisposable FineTimer() => OperatingSystem.IsWindows() ? new Period() : null;

        sealed class Period : IDisposable
        {
            public Period() => timeBeginPeriod(1);

            public void Dispose() => timeEndPeriod(1);
        }
    }
}
