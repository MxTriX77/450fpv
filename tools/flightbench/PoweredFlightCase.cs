using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The powered flight (tasks 1.4 and 1.5): the repository's drone with the Betaflight 4.5 controller at 4 kHz and the
    // ESCs, motors, props and packs at its loops, its drag and the Windy stand-in wind, flown by GoldenFlights' scripted pilot (the same file the suite compiles,
    // so the suite on .NET and on Unity's Mono and this Release build pin one hash). The bench times the powered step.
    internal static class PoweredFlightCase
    {
        public const string Name = "powered-flight";

        // The kept audits of models 7–12 fly model 13's drone.json (D-043's layout, a fixture since D-050), those of
        // models 7–11 on model 11's pack table.
        static DroneSpec M13() => Repository(GoldenFlights.Model13DronePath);
        static DroneSpec M11() => GoldenFlights.WithModel11Packs(M13());

        public static DroneSpec Drone() => Repository("assets-src/drone/drone.json");

        static DroneSpec Repository(string relative)
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string path = Path.Combine(dir, relative);
                if (File.Exists(path)) return DroneSpec.Parse(File.ReadAllText(path));
            }
            throw new FileNotFoundException(relative + " was not found from " + Directory.GetCurrentDirectory());
        }

        public static bool Golden()
        {
            ulong hash = GoldenFlights.FlyPowered(Drone(), out FlightCore core);
            ulong with = GoldenFlights.FlyPowered(Drone(), out _, 992, 120);
            ulong without = GoldenFlights.FlyPowered(Drone(), out _, 992, 120, false);
            bool ok = hash == GoldenFlights.PoweredHash && with == without && core.State.Armed;
            Console.WriteLine($"{Name}: {GoldenFlights.PoweredSteps} steps, hash 0x{hash:X16} "
                + $"({(hash == GoldenFlights.PoweredHash ? "matches" : $"golden 0x{GoldenFlights.PoweredHash:X16}")}), armed {core.State.Armed}, "
                + $"packs at {core.State.PackCharge0:F4} of 0.9; over the record less the contacts' 120 bytes 0x{with:X16} with the contacts, "
                + $"0x{without:X16} without ({(with == without ? "the same" : "DIFFERENT")}): {(ok ? "PASS" : "FAIL")}");
            ulong contact = GoldenFlights.FlyContact(Drone(), out FlightCore c);
            bool cok = contact == GoldenFlights.ContactHash && c.Contacts.ImpactCount >= 2 && !c.State.Crashed;
            Console.WriteLine($"contact-flight: {GoldenFlights.ContactSteps} steps, hash 0x{contact:X16} "
                + $"({(contact == GoldenFlights.ContactHash ? "matches" : $"golden 0x{GoldenFlights.ContactHash:X16}")}), {c.Contacts.ImpactCount} impacts, "
                + $"ends {c.State.Position.Y:F4} m up on {c.Trace.ContactsTouching} touching pairs: {(cok ? "PASS" : "FAIL")}");
            ulong tumble = GoldenFlights.FlyTumble(Drone(), out FlightCore t, out GoldenFlights.TumbleChecks tc);
            bool tok = tumble == GoldenFlights.TumbleHash && tc.Momentum < 1e-10 && tc.Energy < 1e-10 && tc.Parabola < 1e-9 && tc.Wander > 0.5;
            Console.WriteLine($"tumble: {GoldenFlights.TumbleSteps} steps of the L4 body off its principal axes, hash 0x{tumble:X16} "
                + $"({(tumble == GoldenFlights.TumbleHash ? "matches" : $"golden 0x{GoldenFlights.TumbleHash:X16}")}), momentum within {tc.Momentum:E2} (< 1e-10), "
                + $"energy within {tc.Energy:E2} (< 1e-10), CoM {tc.Parabola:E2} m from its parabola (< 1e-9), body rate wandering {tc.Wander:F3} rad/s (> 0.5): "
                + $"{(tok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.1's audit: with the motors' asymmetry off (and 1.2's rings, 1.3's ground effect), over model 7's
            // 1296-byte layout, each golden is model 7's.
            ulong p7 = GoldenFlights.FlyPowered(M11(), out _, 1296, 360, asymmetry: false, ring: false, ground: false, flapping: false, modes: false);
            ulong c7 = GoldenFlights.FlyContact(M11(), out _, 1296, 360, false, false, ground: false, flapping: false, modes: false);
            ulong t7 = GoldenFlights.FlyTumble(M11(), out _, out _, 1296, 360);
            bool aok = p7 == 0xA65FBFBCBCFC5B96UL && c7 == 0x3AF33645FF8F1919UL && t7 == 0xE3178EE7FF322698UL;
            Console.WriteLine($"asymmetry off, over model 7's layout: powered 0x{p7:X16}, contact 0x{c7:X16}, tumble 0x{t7:X16} "
                + $"({(aok ? "model 7's, bit for bit" : "NOT model 7's")}): {(aok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.2's audit: with the vortex rings off (and 1.3's ground effect), over model 8's 1328-byte layout, each
            // golden is model 8's.
            ulong p8 = GoldenFlights.FlyPowered(M11(), out _, 1328, 328, ring: false, ground: false, flapping: false, modes: false);
            ulong c8 = GoldenFlights.FlyContact(M11(), out _, 1328, 328, ring: false, ground: false, flapping: false, modes: false);
            ulong t8 = GoldenFlights.FlyTumble(M11(), out _, out _, 1328, 328);
            bool rok = p8 == 0x08FBAF9331110BE9UL && c8 == 0x5D45B356ECD6B940UL && t8 == 0xF7E0F587B45FA098UL;
            Console.WriteLine($"rings off, over model 8's layout: powered 0x{p8:X16}, contact 0x{c8:X16}, tumble 0x{t8:X16} "
                + $"({(rok ? "model 8's, bit for bit" : "NOT model 8's")}): {(rok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.3's audit: with the ground effect off, over model 9's 1400-byte layout, each golden is model 9's.
            ulong p9 = GoldenFlights.FlyPowered(M11(), out _, 1400, 256, ground: false, flapping: false, modes: false);
            ulong c9 = GoldenFlights.FlyContact(M11(), out _, 1400, 256, ground: false, flapping: false, modes: false);
            ulong t9 = GoldenFlights.FlyTumble(M11(), out _, out _, 1400, 256);
            bool gok = p9 == 0x5AF86BCB6FD0B3A2UL && c9 == 0x8D965FFD8708C6C3UL && t9 == 0x50541A5726F695D8UL;
            Console.WriteLine($"ground effect off, over model 9's layout: powered 0x{p9:X16}, contact 0x{c9:X16}, tumble 0x{t9:X16} "
                + $"({(gok ? "model 9's, bit for bit" : "NOT model 9's")}): {(gok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.3c's audit: with the flapping off (the record's layout unchanged), each golden is model 10's.
            ulong p10 = GoldenFlights.FlyPowered(M11(), out _, 1496, 160, flapping: false, modes: false);
            ulong c10 = GoldenFlights.FlyContact(M11(), out _, 1496, 160, flapping: false, modes: false);
            ulong t10 = GoldenFlights.FlyTumble(M11(), out _, out _, 1496, 160);
            bool fok = p10 == 0x0B91602E467653F4UL && c10 == 0x05EDD1881425872EUL && t10 == 0x58EF4F6AE8D111D8UL;
            Console.WriteLine($"flapping off: powered 0x{p10:X16}, contact 0x{c10:X16}, tumble 0x{t10:X16} "
                + $"({(fok ? "model 10's, bit for bit" : "NOT model 10's")}): {(fok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.5's audit: on model 11's pack table (the record's layout unchanged), each golden is model 11's;
            // the audits above fly on it too.
            ulong p11 = GoldenFlights.FlyPowered(M11(), out _, 1496, 160, modes: false);
            ulong c11 = GoldenFlights.FlyContact(M11(), out _, 1496, 160, modes: false);
            ulong t11 = GoldenFlights.FlyTumble(M11(), out _, out _, 1496, 160);
            bool pok = p11 == 0x9057D18CF741A255UL && c11 == 0xF6E32F2FDC831268UL && t11 == 0x58EF4F6AE8D111D8UL;
            Console.WriteLine($"model 11's pack table: powered 0x{p11:X16}, contact 0x{c11:X16}, tumble 0x{t11:X16} "
                + $"({(pok ? "model 11's, bit for bit" : "NOT model 11's")}): {(pok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.4's audit: with the airframe's modes off, over model 12's 1496-byte layout, each golden is model 12's.
            ulong p12 = GoldenFlights.FlyPowered(M13(), out _, 1496, 160, modes: false);
            ulong c12 = GoldenFlights.FlyContact(M13(), out _, 1496, 160, modes: false);
            ulong t12 = GoldenFlights.FlyTumble(M13(), out _, out _, 1496, 160);
            bool mok = p12 == 0x63D668DD01A1E587UL && c12 == 0x8B50CC1BD8EBC8FFUL && t12 == 0x58EF4F6AE8D111D8UL;
            Console.WriteLine($"modes off, over model 12's layout: powered 0x{p12:X16}, contact 0x{c12:X16}, tumble 0x{t12:X16} "
                + $"({(mok ? "model 12's, bit for bit" : "NOT model 12's")}): {(mok ? "PASS" : "FAIL")}");
            return ok && cok && tok && aok && rok && gok && fok && pok && mok;
        }

        public static bool Bench()
        {
            const int warmup = 2000, steps = 60000; // 60 s of flight: the scripted pilot's 10 s six times
            var core = new FlightCore(GoldenFlights.PoweredSetup(Drone()));
            var sticks = new StickSample[GoldenFlights.PoweredSteps];
            for (int k = 0; k < sticks.Length; k++) sticks[k] = GoldenFlights.PoweredStick(k);
            var times = new long[steps];
            for (int k = 0; k < warmup; k++) core.Advance(sticks[k]);

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < steps; k++)
            {
                StickSample stick = sticks[(warmup + k) % sticks.Length];
                if (stick.HostUs < 20 * FlightCore.StepUs) stick.Arm = true; // the loop's restart keeps the switch on
                long t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                times[k] = Stopwatch.GetTimestamp() - t0;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, max = times[steps - 1] * us;
            bool ok = p99 <= 300 && allocated == 0 && core.State.Armed;
            string share = TimedQuery.Share(GoldenFlights.PoweredSetup(Drone()), (c, k) =>
            {
                StickSample s = sticks[k % sticks.Length];
                if (k >= sticks.Length && s.HostUs < 20 * FlightCore.StepUs) s.Arm = true;
                return s;
            }, warmup, steps);
            Console.WriteLine($"{Name}: {steps} steps of {FlightController.LoopsPerStep} controller loops and propulsion sub-steps, "
                + $"p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs, steady-state allocation {allocated} B, {share}: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }
    }
}
