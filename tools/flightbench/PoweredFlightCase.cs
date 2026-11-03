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
        static DroneSpec M14() => GoldenFlights.WithModel14Fit(M22());

        // D-066's audit (model 23): model 22's drone, the 40 A limit, the 1.0 A no-load current and its pack and electronics masses;
        // since model 29 without props.blade (p7f-damage 1.2), as every older kept audit.
        static DroneSpec M22() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(Drone()));

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
            // D-064 (model 23): over the record's first 1808 bytes, without the rotor wakes and the motors' new spreads; the
            // contact flight's re-ingested wash moved to published numbers, so its audit was a temporary edit (GoldenFlights).
            // D-066's audit (model 23 on p7b-airframe--power), kept: on model 22's drone every golden is model 22's there and the
            // landing's merge's model 25 here (the merge at model 26), every record and event.
            // Since model 35 every kept audit hashes over model 29's 1904 bytes (the record less p7f-damage 1.3's blade strikes).
            const int m29 = GoldenFlights.Model29Layout;
            ulong p22 = GoldenFlights.FlyPowered(M22(), out _, layout: m29), c22 = GoldenFlights.FlyContact(M22(), out _, layout: m29),
                g22 = GoldenFlights.FlyRange(M22(), out _, layout: m29);
            ulong w22 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m29);
            ulong t22 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m29);
            bool m22 = p22 == GoldenFlights.Model25PoweredHash && c22 == GoldenFlights.Model25ContactHash && g22 == GoldenFlights.Model25RangeHash
                && w22 == GoldenFlights.Model25RainPoweredHash && t22 == GoldenFlights.Model25TumbleHash;
            Console.WriteLine($"model 22's drone (the 40 A limit, 1.0 A no-load, 0.92 kg packs, 0.25 kg electronics): powered 0x{p22:X16}, contact 0x{c22:X16}, "
                + $"range 0x{g22:X16}, rain 0x{w22:X16}, tumble 0x{t22:X16} ({(m22 ? "model 25's, bit for bit" : "NOT model 25's")}): {(m22 ? "PASS" : "FAIL")}");
            DroneSpec main = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(DroneText(), GoldenFlights.TunedHForce)));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int m19 = GoldenFlights.Model19Layout;
            ulong p15 = GoldenFlights.FlyPowered(main, out _, 1296, 152, wind: still, interference: false, wake: false, motors: false, layout: m19);
            ulong t15 = GoldenFlights.FlyTumble(main, out _, out _, 1296, 152, m19);
            bool mok = p15 == GoldenFlights.Model15StillPoweredHash && t15 == GoldenFlights.Model15TumbleHash;
            Console.WriteLine($"main's rotor drag in still air, over main's layout: powered 0x{p15:X16}, tumble 0x{t15:X16} "
                + $"({(mok ? "model 15's, bit for bit" : "NOT model 15's")}): {(mok ? "PASS" : "FAIL")}");
            // The tumble's chain over main's layout: model 14's fit and model 13's file fly their pins.
            ulong t14 = GoldenFlights.FlyTumble(M14(), out _, out _, 1296, 152, m19);
            ulong t13 = GoldenFlights.FlyTumble(M13(), out _, out _, 1296, 152, m19);
            bool eok = t14 == 0x26CDDB5EDB155896UL && t13 == 0xB371E06FF28075D8UL;
            Console.WriteLine($"the tumble over main's layout: model 14's fit 0x{t14:X16}, model 13's file 0x{t13:X16} "
                + $"({(eok ? "theirs, bit for bit" : "NOT theirs")}): {(eok ? "PASS" : "FAIL")}");
            // p7b-airframe 1.3d's and D-064's audits (model 24): without the interference and D-064's sources, over model 19's
            // 1808 bytes, the powered, rain and tumble goldens are model 19's (the contact and range flights' wash has no switch).
            ulong p19 = GoldenFlights.FlyPowered(M22(), out _, interference: false, wake: false, motors: false, layout: m19);
            ulong r19 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19);
            ulong t19 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m19);
            bool aok = p19 == GoldenFlights.Model19PoweredHash && r19 == GoldenFlights.Model19RainHash && t19 == GoldenFlights.Model19TumbleHash;
            Console.WriteLine($"without the interference and D-064's sources, over model 19's 1808 bytes: powered 0x{p19:X16}, rain 0x{r19:X16}, "
                + $"tumble 0x{t19:X16} ({(aok ? "model 19's, bit for bit" : "NOT model 19's")}): {(aok ? "PASS" : "FAIL")}");
            // The integration's audit against p7b-airframe--micro (model 23): without the interference, over that branch's 1840
            // bytes (the record less the interference's 64 at 1808), each golden is its pin; since model 25 the contact flight's
            // is with 1.6c's stick spring (GoldenFlights.MicroContactHash).
            const int at = GoldenFlights.Model19Layout, skip = 64;
            ulong mp = GoldenFlights.FlyPowered(M22(), out _, at, skip, interference: false, layout: m29);
            ulong mc = GoldenFlights.FlyContact(M22(), out _, at, skip, interference: false, layout: m29);
            ulong mr = GoldenFlights.FlyPowered(M22(), out _, at, skip, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, layout: m29);
            ulong mt = GoldenFlights.FlyTumble(M22(), out _, out _, at, skip, m29);
            bool uok = mp == GoldenFlights.MicroPoweredHash && mc == GoldenFlights.MicroContactHash && mr == GoldenFlights.MicroRainHash
                && mt == GoldenFlights.MicroTumbleHash;
            Console.WriteLine($"without the interference, over the micro branch's 1840 bytes: powered 0x{mp:X16}, contact 0x{mc:X16}, rain 0x{mr:X16}, "
                + $"tumble 0x{mt:X16} ({(uok ? "its model 23's, bit for bit" : "NOT its model 23's")}): {(uok ? "PASS" : "FAIL")}");
            // The merge's audit against p7b-airframe--power (its model 24): on drone.json without the interference and D-064's
            // sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are its pins.
            DroneSpec bare = GoldenFlights.WithoutTheBlade(Drone());
            ulong pp = GoldenFlights.FlyPowered(bare, out _, interference: false, wake: false, motors: false, layout: m19);
            ulong pr = GoldenFlights.FlyPowered(bare, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19);
            ulong pt = GoldenFlights.FlyTumble(bare, out _, out _, layout: m19);
            bool pok = pp == GoldenFlights.PowerPoweredHash && pr == GoldenFlights.PowerRainHash && pt == GoldenFlights.PowerTumbleHash;
            Console.WriteLine($"without the interference and D-064's sources, over 1808 bytes: powered 0x{pp:X16}, rain 0x{pr:X16}, tumble 0x{pt:X16} "
                + $"({(pok ? "the power branch's, bit for bit" : "NOT the power branch's")}): {(pok ? "PASS" : "FAIL")}");
            // p7f-damage 1.2's audit (model 29), kept: with the blades' pitch draws off and the flapping's inertia at the rod, every
            // golden is model 26's; each switch alone gives the share of the move it makes, printed (GoldenFlights' notes).
            DroneSpec d = Drone();
            Action<FlightSetup> rod = GoldenFlights.WithTheRodsInertia(d), nopitch = GoldenFlights.WithoutThePitch, both = s => { nopitch(s); rod(s); };
            ulong a26 = GoldenFlights.FlyPowered(d, out _, edit: both, layout: m29), b26 = GoldenFlights.FlyContact(d, out _, edit: both, layout: m29),
                c26 = GoldenFlights.FlyRange(d, out _, edit: both, layout: m29);
            ulong r26 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: both, layout: m29);
            bool sok = a26 == GoldenFlights.Model26PoweredHash && b26 == GoldenFlights.Model26ContactHash && c26 == GoldenFlights.Model26RangeHash
                && r26 == GoldenFlights.Model26RainPoweredHash;
            Console.WriteLine($"the blades' pitch off and the rod's inertia: powered 0x{a26:X16}, contact 0x{b26:X16}, range 0x{c26:X16}, rain 0x{r26:X16} "
                + $"({(sok ? "model 26's, bit for bit" : "NOT model 26's")}): {(sok ? "PASS" : "FAIL")}");
            ulong ip = GoldenFlights.FlyPowered(d, out _, edit: nopitch, layout: m29), ic = GoldenFlights.FlyContact(d, out _, edit: nopitch, layout: m29),
                ig = GoldenFlights.FlyRange(d, out _, edit: nopitch, layout: m29);
            ulong ir = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: nopitch, layout: m29);
            ulong qp = GoldenFlights.FlyPowered(d, out _, edit: rod, layout: m29), qc = GoldenFlights.FlyContact(d, out _, edit: rod, layout: m29),
                qg = GoldenFlights.FlyRange(d, out _, edit: rod, layout: m29);
            ulong qr = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: rod, layout: m29);
            Console.WriteLine($"  the flapping's blade inertia alone (pitch off): powered 0x{ip:X16}, contact 0x{ic:X16}, range 0x{ig:X16}, rain 0x{ir:X16}");
            Console.WriteLine($"  the blades' pitch alone (the rod's inertia): powered 0x{qp:X16}, contact 0x{qc:X16}, range 0x{qg:X16}, rain 0x{qr:X16}");
            // p7f-damage 1.3's audit (model 35), kept: the goldens' discs cut nothing, so over model 29's 1904 bytes every golden is
            // model 29's pin, every record and event; with the strikes off (ContactSpec.BladeStrikes) the whole record is model 35's.
            ulong p29 = GoldenFlights.FlyPowered(d, out FlightCore pc, layout: m29), c29 = GoldenFlights.FlyContact(d, out FlightCore cc, layout: m29);
            ulong g29 = GoldenFlights.FlyRange(d, out FlightCore gc, layout: m29), t29 = GoldenFlights.FlyTumble(d, out _, out _, layout: m29);
            ulong r29 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m29);
            Action<FlightSetup> off = s => { if (s.Contacts != null) s.Contacts.BladeStrikes = false; };
            ulong coff = GoldenFlights.FlyContact(d, out _, edit: off), goff = GoldenFlights.FlyRange(d, out _, edit: off);
            bool zok = p29 == GoldenFlights.Model29PoweredHash && c29 == GoldenFlights.Model29ContactHash && g29 == GoldenFlights.Model29RangeHash
                && t29 == GoldenFlights.Model29TumbleHash && r29 == GoldenFlights.Model29RainPoweredHash && coff == GoldenFlights.ContactHash && goff == GoldenFlights.RangeHash
                && cc.Strikes != null && cc.Strikes.Passes == 0 && gc.Strikes.Passes == 0;
            Console.WriteLine($"over model 29's 1904 bytes: powered 0x{p29:X16}, contact 0x{c29:X16}, range 0x{g29:X16}, rain 0x{r29:X16}, tumble 0x{t29:X16} "
                + $"({(zok ? "model 29's, bit for bit" : "NOT model 29's")}), {cc.Strikes?.Passes} and {gc.Strikes?.Passes} blade passes in the contact and range flights; "
                + $"the strikes off: contact 0x{coff:X16}, range 0x{goff:X16} (the pins): {(zok ? "PASS" : "FAIL")}");
            return ok && cok && rangeOk && rok && tok && m22 && mok && eok && aok && uok && pok && sok && zok;
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
