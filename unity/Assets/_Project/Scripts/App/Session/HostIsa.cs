using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Fpv450.App.Session
{
    /// <summary>
    /// This host's ISA flags for the flight log's header (LOG-04), in tools/replay's <c>Host.Isa</c> form: the process
    /// architecture, then sse2, sse4.1, avx, avx2, fma and avx512f, so a replay on the same machine notes no ISA
    /// difference. Unity's Mono has no System.Runtime.Intrinsics, so the flags are Windows' IsProcessorFeaturePresent
    /// (which accounts for the OS saving the wider registers, as .NET's IsSupported does). Windows has no flag for FMA3;
    /// every x86 CPU with AVX2 has it (Intel since Haswell, AMD since Excavator), so fma follows avx2 (assumed). Sim data
    /// only (LOG-05): no machine name, user or path.
    /// </summary>
    public static class HostIsa
    {
        public static readonly string Flags = Detect();

        // winnt.h's PF_* features.
        const uint Sse2 = 10, Sse41 = 37, Avx = 39, Avx2 = 40, Avx512F = 41;

        static string Detect()
        {
            var flags = new List<string> { RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant() };
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return flags[0];
            try
            {
                if (IsProcessorFeaturePresent(Sse2)) flags.Add("sse2");
                if (IsProcessorFeaturePresent(Sse41)) flags.Add("sse4.1");
                if (IsProcessorFeaturePresent(Avx)) flags.Add("avx");
                if (IsProcessorFeaturePresent(Avx2))
                {
                    flags.Add("avx2");
                    flags.Add("fma");
                }
                if (IsProcessorFeaturePresent(Avx512F)) flags.Add("avx512f");
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
            }
            return string.Join(" ", flags);
        }

        [DllImport("kernel32.dll")]
        static extern bool IsProcessorFeaturePresent(uint feature);
    }
}
