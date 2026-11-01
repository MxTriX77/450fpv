using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The powered flight (tasks 1.4 and 1.5): the repository's drone with the Betaflight 4.5 controller at 4 kHz and the
    // ESCs, motors, props and packs at its loops, its drag and the Windy wind (the field, p7c-wind 1.4), flown by GoldenFlights' scripted pilot (the same file the suite compiles,
    // so the suite on .NET and on Unity's Mono and this Release build pin one hash). The bench times the powered step.
    internal static class PoweredFlightCase
    {
        public const string Name = "powered-flight";

        // The tumble's chain over main's layout flies model 14's fit and model 13's file (D-043's layout, a fixture since D-050).
        static DroneSpec M13() => DroneSpec.Parse(Repository(GoldenFlights.Model13DronePath));
        static DroneSpec M14() => GoldenFlights.WithModel14Fit(Drone());

        public static DroneSpec Drone() => DroneSpec.Parse(DroneText());

        /// <summary>The repository's drone.json, found from the working directory up.</summary>
        public static string DroneText() => Repository("assets-src/drone/drone.json");

        static string Repository(string relative)
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string path = Path.Combine(dir, relative);
                if (File.Exists(path)) return File.ReadAllText(path);
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
            ulong content = Fpv450.Sim.Tests.RangeScenarios.World.ContentHash;
            ulong range = GoldenFlights.FlyRange(Drone(), out FlightCore g);
            bool rangeOk = content == GoldenFlights.RangeContent && range == GoldenFlights.RangeHash && g.Contacts.ImpactCount >= 1 && !g.State.Crashed;
            Console.WriteLine($"range-flight: the test range 0x{content:X16} ({(content == GoldenFlights.RangeContent ? "as recorded" : $"recorded on 0x{GoldenFlights.RangeContent:X16}: the map moved")}), "
                + $"{GoldenFlights.RangeSteps} steps on S04's straw, hash 0x{range:X16} ({(range == GoldenFlights.RangeHash ? "matches" : $"golden 0x{GoldenFlights.RangeHash:X16}")}), "
                + $"{g.Contacts.ImpactCount} impacts, ends {g.State.Position.Y:F4} m up on {g.Trace.ContactsTouching} touching pairs: {(rangeOk ? "PASS" : "FAIL")}");
            ulong rain = GoldenFlights.FlyPowered(Drone(), out FlightCore r, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain);
            bool rok = rain == GoldenFlights.RainPoweredHash && r.Rain.FilmMass > 0;
            Console.WriteLine($"rain-flight: {GoldenFlights.PoweredSteps} steps in Calm and Severe rain, hash 0x{rain:X16} "
                + $"({(rain == GoldenFlights.RainPoweredHash ? "matches" : $"golden 0x{GoldenFlights.RainPoweredHash:X16}")}), "
                + $"{r.Rain.RateMmPerHour:F2} mm/h, film {r.Rain.FilmMass * 1000:F2} of {r.Rain.FilmCapacity * 1000:F2} g: {(rok ? "PASS" : "FAIL")}");
            ulong tumble = GoldenFlights.FlyTumble(Drone(), out FlightCore t, out GoldenFlights.TumbleChecks tc);
            bool tok = tumble == GoldenFlights.TumbleHash && tc.Momentum < 1e-10 && tc.Energy < 1e-10 && tc.Parabola < 1e-9 && tc.Wander > 0.5;
            Console.WriteLine($"tumble: {GoldenFlights.TumbleSteps} steps of the L4 body off its principal axes, hash 0x{tumble:X16} "
                + $"({(tumble == GoldenFlights.TumbleHash ? "matches" : $"golden 0x{GoldenFlights.TumbleHash:X16}")}), momentum within {tc.Momentum:E2} (< 1e-10), "
                + $"energy within {tc.Energy:E2} (< 1e-10), CoM {tc.Parabola:E2} m from its parabola (< 1e-9), body rate wandering {tc.Wander:F3} rad/s (> 0.5): "
                + $"{(tok ? "PASS" : "FAIL")}");
            // The merge's audit against unity/main (model 17): on drone.json with main's tuned rotor drag (0.25) in still air,
            // over main's 1656-byte layout (the record less p7c-wind's 152 bytes at 1296), each golden is main's model 15 flight
            // in still air, and the tumble main's pin. Main's kept audits of models 6–14 flew the stand-in's air, which is gone.
            DroneSpec main = GoldenFlights.WithRotorDrag(DroneText(), GoldenFlights.TunedHForce);
            WindSetup still = WindSetup.Of(WindLevel.Still);
            ulong p15 = GoldenFlights.FlyPowered(main, out _, 1296, 152, wind: still);
            ulong c15 = GoldenFlights.FlyContact(main, out _, 1296, 152, wind: still);
            ulong t15 = GoldenFlights.FlyTumble(main, out _, out _, 1296, 152);
            bool mok = p15 == GoldenFlights.Model15StillPoweredHash && c15 == GoldenFlights.Model15StillContactHash && t15 == GoldenFlights.Model15TumbleHash;
            Console.WriteLine($"main's rotor drag in still air, over main's layout: powered 0x{p15:X16}, contact 0x{c15:X16}, tumble 0x{t15:X16} "
                + $"({(mok ? "model 15's, bit for bit" : "NOT model 15's")}): {(mok ? "PASS" : "FAIL")}");
            // The tumble's chain over main's layout: model 14's fit and model 13's file fly their pins.
            ulong t14 = GoldenFlights.FlyTumble(M14(), out _, out _, 1296, 152);
            ulong t13 = GoldenFlights.FlyTumble(M13(), out _, out _, 1296, 152);
            bool eok = t14 == 0x26CDDB5EDB155896UL && t13 == 0xB371E06FF28075D8UL;
            Console.WriteLine($"the tumble over main's layout: model 14's fit 0x{t14:X16}, model 13's file 0x{t13:X16} "
                + $"({(eok ? "theirs, bit for bit" : "NOT theirs")}): {(eok ? "PASS" : "FAIL")}");
            // The merge's audit against the range branch (model 19): on main's tuned rotor drag in still air, over the range
            // branch's 1656-byte layout, the range golden is model 16's range flight in still air.
            ulong r16 = GoldenFlights.FlyRange(main, out _, 1296, 152, wind: still);
            bool r16ok = r16 == GoldenFlights.Model16StillRangeHash;
            Console.WriteLine($"main's rotor drag in still air, over the range branch's layout: range 0x{r16:X16} "
                + $"({(r16ok ? "model 16's, bit for bit" : "NOT model 16's")}): {(r16ok ? "PASS" : "FAIL")}");
            return ok && cok && rangeOk && rok && tok && mok && eok && r16ok;
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
