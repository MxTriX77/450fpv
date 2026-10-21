using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fpv450.FlightBench
{
    // The power source, plan, mode and CPU clock, printed next to every number (PLT-07): numbers count only on AC.
    // The same readings as tools/hygiene/bench.py.
    internal static class Power
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SystemPowerStatus
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ProcessorPowerInformation
        {
            public uint Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdleState, CurrentIdleState;
        }

        [DllImport("kernel32.dll")]
        static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

        [DllImport("powrprof.dll")]
        static extern uint PowerGetEffectiveOverlayScheme(out Guid scheme);

        [DllImport("powrprof.dll")]
        static extern uint CallNtPowerInformation(int level, IntPtr input, uint inputLength,
            [Out] ProcessorPowerInformation[] output, uint outputLength);

        public static bool OnAc { get; private set; }

        public static string Describe()
        {
            if (!OperatingSystem.IsWindows()) return "power state unknown";
            string source = "unknown source";
            if (GetSystemPowerStatus(out SystemPowerStatus status))
            {
                OnAc = status.ACLineStatus == 1;
                source = status.ACLineStatus switch { 0 => "battery, not evidence (PLT-07)", 1 => "AC", _ => source };
            }
            return $"{source}, {Plan()} plan, {Mode()} mode, {Clock()}";
        }

        static string Plan()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("powercfg", "/getactivescheme")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                string text = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                int open = text.IndexOf('('), close = text.LastIndexOf(')');
                return open >= 0 && close > open ? text.Substring(open + 1, close - open - 1) : "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        static string Mode()
        {
            if (PowerGetEffectiveOverlayScheme(out Guid g) != 0) return "unknown";
            switch (g.ToString())
            {
                case "961cc777-2547-4f9d-8174-7d86181b8a7a": return "best power efficiency";
                case "00000000-0000-0000-0000-000000000000": return "balanced";
                case "3af9b8d9-7c97-431d-ad78-34a8bfea439f": return "better performance";
                case "ded574b5-45a0-4f42-8737-46345c09c238": return "best performance";
                default: return "unknown";
            }
        }

        static string Clock()
        {
            var info = new ProcessorPowerInformation[Environment.ProcessorCount];
            uint size = (uint)(Marshal.SizeOf<ProcessorPowerInformation>() * info.Length);
            if (CallNtPowerInformation(11, IntPtr.Zero, 0, info, size) != 0) return "CPU clock unknown";
            uint current = 0, max = 0;
            foreach (ProcessorPowerInformation p in info)
            {
                current = Math.Max(current, p.CurrentMhz);
                max = Math.Max(max, p.MaxMhz);
            }
            return $"CPU {current}/{max} MHz";
        }
    }
}
