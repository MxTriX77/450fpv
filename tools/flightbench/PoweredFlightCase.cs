using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Contact;
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
        static DroneSpec M22() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(Old()));

        // p7g-legs 1.1's audit (model 37): model 26's legs, the CFRP legs' masses, the assumed 10 kN/m spring and its damper;
        // since model 46 without props.blade, as p7g-legs' audits fly without the line's mechanisms.
        static DroneSpec M26() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(Old()));

        // p7g-legs 1.2's audit (model 39): model 37's leg law, without the legs' side give and their failure law; since model 46
        // without props.blade.
        static DroneSpec M37() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel37Legs(Old()));

        public static DroneSpec Drone() => DroneSpec.Parse(DroneText());

        // Model 74 (the nozzle bent to the pilot's 27.5°): every kept audit of models 64–70 flies model 70's drone.json (a fixture), whose
        // line FlyContact lets out of its 15° nozzle over the 4 mm lip with the old peel (GoldenFlights.Model70Tether).
        static DroneSpec M70() => DroneSpec.Parse(Repository(GoldenFlights.Model70DronePath));

        // Model 64 (D01's canister): every kept audit of models up to 63 flies model 63's drone.json, the old coil beneath the
        // frame (a fixture), whose line FlyContact lets out at the old rear cap (GoldenFlights.Model63Tether).
        static DroneSpec Old() => DroneSpec.Parse(Repository(GoldenFlights.Model63DronePath));

        // The line's audits since model 40 (the physics integration 2 with p7f-damage): drone.json without props.blade; since
        // model 46 (with p7g-legs) on model 26's legs.
        static DroneSpec Bare() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(Old()));

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
            // The record less the contacts' 120 bytes and, since model 39, the legs' state (zero without contacts; since model 46
            // after the line's 2392 bytes).
            const int legs = GoldenFlights.Model44Layout;
            ulong with = GoldenFlights.FlyPowered(Drone(), out _, 992, 120, layout: legs);
            ulong without = GoldenFlights.FlyPowered(Drone(), out _, 992, 120, false, layout: legs);
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
            // p7a-ground 1.10's audit, restated at the ground merge (model 27) and the physics integration 2 (model 34): without
            // surfaces.json the contact flight is p7d-tether's model 31 flight over the whole record, and the physics line's model
            // 26 flight on the rigid floor over model 28's 1904 bytes.
            const int m28 = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block
            const int m38 = GoldenFlights.Model38Layout; // since model 40 the blade strikes follow it; the line's audits fly without props.blade
            const int m29 = GoldenFlights.Model29Layout; // p7f-damage's model 29 record: the line's model 28 record, 1904 bytes
            const int m44 = GoldenFlights.Model44Layout; // since model 46 the legs' block follows the strikes
            const int m46 = GoldenFlights.Model46Layout; // since model 49 the legs' lean follows the legs' block
            ulong rigid = GoldenFlights.FlyContact(Bare(), out _, soil: false, yawI: false, layout: m38, airmode: true),
                rigid28 = GoldenFlights.FlyContact(Bare(), out _, soil: false, layout: m28, yawI: false, airmode: true);
            bool sok = rigid == GoldenFlights.Model31ContactHash && rigid28 == GoldenFlights.Model26ContactHash;
            cok &= sok;
            Console.WriteLine($"contact-flight without surfaces.json: hash 0x{rigid:X16}, over 1904 bytes 0x{rigid28:X16} "
                + $"({(sok ? "model 31's and model 26's, bit for bit" : "NOT model 31's and 26's")}): {(sok ? "PASS" : "FAIL")}");
            ulong content = Fpv450.Sim.Tests.RangeScenarios.World.ContentHash;
            ulong range = GoldenFlights.FlyRange(Drone(), out FlightCore g);
            bool rangeOk = content == GoldenFlights.RangeContent && range == GoldenFlights.RangeHash && g.Contacts.ImpactCount >= 1 && !g.State.Crashed;
            Console.WriteLine($"range-flight: the test range 0x{content:X16} ({(content == GoldenFlights.RangeContent ? "as recorded" : $"recorded on 0x{GoldenFlights.RangeContent:X16}: the map moved")}), "
                + $"{GoldenFlights.RangeSteps} steps on S04's straw, hash 0x{range:X16} ({(range == GoldenFlights.RangeHash ? "matches" : $"golden 0x{GoldenFlights.RangeHash:X16}")}), "
                + $"{g.Contacts.ImpactCount} impacts, ends {g.State.Position.Y:F4} m up on {g.Trace.ContactsTouching} touching pairs: {(rangeOk ? "PASS" : "FAIL")}");
            // w7-farmland-wind 1.2's audit (model 88): with the wake grid's version 1 the range flight is model 76's pin, every
            // record and event (GoldenFlightTests.WithVersion1WakesTheRangeGoldenIsModel76s); the edgewise audits below fly version 1 too.
            ulong v1 = GoldenFlights.FlyRange(Drone(), out _, wakes2: false);
            bool v1ok = v1 == GoldenFlights.Model76RangeHash;
            rangeOk &= v1ok;
            Console.WriteLine($"range-flight with the wake grid's version 1: 0x{v1:X16} ({(v1ok ? "model 76's, bit for bit" : "NOT model 76's")}): {(v1ok ? "PASS" : "FAIL")}");
            // p7a-ground 1.12's audit (model 33), restated at the physics integration 2 (model 34): without the layers' area laws
            // the range flight is p7d-tether's model 31 flight over the whole record, and model 32's over model 28's 1904 bytes,
            // dry and in Rain.
            ulong r31 = GoldenFlights.FlyRange(Bare(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, layout: m38, airmode: true);
            ulong r32 = GoldenFlights.FlyRange(Bare(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: m28, yawI: false, airmode: true);
            ulong w32 = GoldenFlights.FlyRange(Bare(), out _, rain: RainLevel.Rain, table: Fpv450.Sim.Tests.SoilScenarios.Table(), materials: GoldenFlights.WithoutTheAreaLaws(),
                layout: m28, yawI: false, airmode: true);
            bool a32 = r31 == GoldenFlights.Model31RangeHash && r32 == GoldenFlights.Model32RangeHash && w32 == GoldenFlights.Model32RainRangeHash;
            rangeOk &= a32;
            Console.WriteLine($"range-flight without the layers' area laws: 0x{r31:X16}, over 1904 bytes 0x{r32:X16}, in Rain with surfaces.json 0x{w32:X16} "
                + $"({(a32 ? "model 31's and model 32's, bit for bit" : "NOT model 31's and 32's")}): {(a32 ? "PASS" : "FAIL")}");
            // The physics integration 2's audit against p7c-wind's cavity (model 34): with the map's cuts dropped from
            // WindSetup.ForMap the range flight is the same bits (it never enters a cut's opening).
            WindSetup uncut = WindSetup.ForMap(Fpv450.Sim.Tests.RangeScenarios.World, WindLevel.Calm);
            int cuts = uncut.Cuts?.Length ?? 0;
            uncut.Cuts = null;
            ulong rc = GoldenFlights.FlyRange(Drone(), out _, wind: uncut);
            bool cutOk = cuts > 0 && rc == GoldenFlights.RangeHash;
            rangeOk &= cutOk;
            Console.WriteLine($"range-flight without its map's {cuts} cuts: 0x{rc:X16} ({(rc == GoldenFlights.RangeHash ? "the same bits" : "DIFFERENT")}): {(cutOk ? "PASS" : "FAIL")}");
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
            ulong p22 = GoldenFlights.FlyPowered(M22(), out _, layout: m28, yawI: false, airmode: true), c22 = GoldenFlights.FlyContact(M22(), out _, soil: false, layout: m28, yawI: false, airmode: true),
                g22 = GoldenFlights.FlyRange(M22(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: m28, yawI: false, airmode: true);
            ulong w22 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, yawI: false, airmode: true);
            ulong t22 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m28);
            bool m22 = p22 == GoldenFlights.Model25PoweredHash && c22 == GoldenFlights.Model25ContactHash && g22 == GoldenFlights.Model25RangeHash
                && w22 == GoldenFlights.Model25RainPoweredHash && t22 == GoldenFlights.Model25TumbleHash;
            Console.WriteLine($"model 22's drone (the 40 A limit, 1.0 A no-load, 0.92 kg packs, 0.25 kg electronics): powered 0x{p22:X16}, contact 0x{c22:X16}, "
                + $"range 0x{g22:X16}, rain 0x{w22:X16}, tumble 0x{t22:X16} ({(m22 ? "model 25's, bit for bit" : "NOT model 25's")}): {(m22 ? "PASS" : "FAIL")}");
            DroneSpec main = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(Repository(GoldenFlights.Model63DronePath), GoldenFlights.TunedHForce)));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int m19 = GoldenFlights.Model19Layout;
            ulong p15 = GoldenFlights.FlyPowered(main, out _, 1296, 152, wind: still, interference: false, wake: false, motors: false, layout: m19, yawI: false, airmode: true);
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
            ulong p19 = GoldenFlights.FlyPowered(M22(), out _, interference: false, wake: false, motors: false, layout: m19, yawI: false, airmode: true);
            ulong r19 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19, yawI: false, airmode: true);
            ulong t19 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m19);
            bool aok = p19 == GoldenFlights.Model19PoweredHash && r19 == GoldenFlights.Model19RainHash && t19 == GoldenFlights.Model19TumbleHash;
            Console.WriteLine($"without the interference and D-064's sources, over model 19's 1808 bytes: powered 0x{p19:X16}, rain 0x{r19:X16}, "
                + $"tumble 0x{t19:X16} ({(aok ? "model 19's, bit for bit" : "NOT model 19's")}): {(aok ? "PASS" : "FAIL")}");
            // The integration's audit against p7b-airframe--micro (model 23): without the interference, over that branch's 1840
            // bytes (the record less the interference's 64 at 1808), each golden is its pin; since model 25 the contact flight's
            // is with 1.6c's stick spring (GoldenFlights.MicroContactHash).
            const int at = GoldenFlights.Model19Layout, skip = 64;
            ulong mp = GoldenFlights.FlyPowered(M22(), out _, at, skip, interference: false, layout: m28, yawI: false, airmode: true);
            ulong mc = GoldenFlights.FlyContact(M22(), out _, at, skip, interference: false, soil: false, layout: m28, yawI: false, airmode: true);
            ulong mr = GoldenFlights.FlyPowered(M22(), out _, at, skip, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, layout: m28,
                yawI: false, airmode: true);
            ulong mt = GoldenFlights.FlyTumble(M22(), out _, out _, at, skip, m28);
            bool uok = mp == GoldenFlights.MicroPoweredHash && mc == GoldenFlights.MicroContactHash && mr == GoldenFlights.MicroRainHash
                && mt == GoldenFlights.MicroTumbleHash;
            Console.WriteLine($"without the interference, over the micro branch's 1840 bytes: powered 0x{mp:X16}, contact 0x{mc:X16}, rain 0x{mr:X16}, "
                + $"tumble 0x{mt:X16} ({(uok ? "its model 23's, bit for bit" : "NOT its model 23's")}): {(uok ? "PASS" : "FAIL")}");
            // The merge's audit against p7b-airframe--power (its model 24): on drone.json without the interference and D-064's
            // sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are its pins.
            DroneSpec bare = Bare(); // since model 46 on model 26's legs
            ulong pp = GoldenFlights.FlyPowered(bare, out _, interference: false, wake: false, motors: false, layout: m19, yawI: false, airmode: true);
            ulong pr = GoldenFlights.FlyPowered(bare, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19, yawI: false, airmode: true);
            ulong pt = GoldenFlights.FlyTumble(bare, out _, out _, layout: m19);
            bool pok = pp == GoldenFlights.PowerPoweredHash && pr == GoldenFlights.PowerRainHash && pt == GoldenFlights.PowerTumbleHash;
            Console.WriteLine($"without the interference and D-064's sources, over 1808 bytes: powered 0x{pp:X16}, rain 0x{pr:X16}, tumble 0x{pt:X16} "
                + $"({(pok ? "the power branch's, bit for bit" : "NOT the power branch's")}): {(pok ? "PASS" : "FAIL")}");
            // p7d-tether 1.6's audit (model 31): every golden without the line is model 28's over its 1904 bytes, the fiber's block
            // zero on every record; since model 34 the contact and range flights without the soil and the area laws, and with them
            // p7a-ground's model 33 pins (the physics integration 2's audit against the ground).
            int tail = 0;
            void Tail(byte[] record)
            {
                for (int i = m28; i < m38; i++) if (record[i] != 0) tail++;
            }
            ulong a28 = GoldenFlights.FlyPowered(Bare(), out _, layout: m28, onRecord: Tail, yawI: false, airmode: true);
            ulong c28 = GoldenFlights.FlyContact(Bare(), out _, layout: m28, onRecord: Tail, soil: false, yawI: false, airmode: true);
            ulong g28 = GoldenFlights.FlyRange(Bare(), out _, layout: m28, onRecord: Tail, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true);
            ulong c33 = GoldenFlights.FlyContact(Bare(), out _, layout: m28, onRecord: Tail, yawI: false, airmode: true);
            ulong g33 = GoldenFlights.FlyRange(Bare(), out _, layout: m28, onRecord: Tail, yawI: false, airmode: true);
            ulong r28 = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, onRecord: Tail, yawI: false, airmode: true);
            ulong t28 = GoldenFlights.FlyTumble(Bare(), out _, out _, layout: m28);
            bool k28 = a28 == GoldenFlights.Model28PoweredHash && c28 == GoldenFlights.Model28ContactHash && g28 == GoldenFlights.Model28RangeHash
                && r28 == GoldenFlights.Model28RainHash && t28 == GoldenFlights.Model28TumbleHash && tail == 0;
            Console.WriteLine($"without the line, over model 28's 1904 bytes: powered 0x{a28:X16}, contact (no soil) 0x{c28:X16}, range (no area laws) 0x{g28:X16}, "
                + $"rain 0x{r28:X16}, tumble 0x{t28:X16} ({(k28 ? "model 28's, bit for bit" : "NOT model 28's")}), {tail} non-zero bytes in the fiber's block: {(k28 ? "PASS" : "FAIL")}");
            bool k33 = c33 == GoldenFlights.Model33ContactHash && g33 == GoldenFlights.Model33RangeOn38Hash && tail == 0;
            k28 &= k33;
            Console.WriteLine($"without the line, over model 28's 1904 bytes, with the soil and the area laws: contact 0x{c33:X16}, range 0x{g33:X16} "
                + $"({(k33 ? "p7a-ground's model 33, bit for bit; the range with model 38's buried face" : "NOT model 33's")}): {(k33 ? "PASS" : "FAIL")}");
            // p7d-tether 1.6: the contact flight with the line.
            ulong line = GoldenFlights.FlyContact(Drone(), out FlightCore l, tether: true);
            bool lok = line == GoldenFlights.TetherHash && l.Tether.PaidOut > 0 && l.Tether.Chain.Wedges == 0 && !l.State.Crashed;
            Console.WriteLine($"tether-flight: {GoldenFlights.ContactSteps} steps with the line, hash 0x{line:X16} "
                + $"({(line == GoldenFlights.TetherHash ? "matches" : $"golden 0x{GoldenFlights.TetherHash:X16}")}), {l.Tether.PaidOut:F3} m paid out, "
                + $"{l.Tether.Chain.Count} nodes, trail {l.Tether.TrailCount}, {l.Tether.Chain.Clips} clips, {l.Tether.Chain.SafeSteps} safe steps, "
                + $"{l.Tether.Chain.Wedges} wedges, {l.Tether.Chain.NewtonSolves} Newton solves ({l.Tether.Chain.NewtonUnconverged} steps past their budget), "
                + $"ends {l.State.Position.Y:F4} m up: {(lok ? "PASS" : "FAIL")}");
            // w3-farmland-ground 2.4 (the laid line rests and freezes), its audit against model 79's pin, which 80–84 kept: with the
            // known-wrong sliding trail (FlightSetup.TetherSlidingTrail) the tether golden is 79's, every record and event.
            ulong line79 = GoldenFlights.FlyContact(Drone(), out FlightCore l79, tether: true, slidingTrail: true);
            bool k79 = line79 == GoldenFlights.Model79TetherHash;
            lok &= k79;
            Console.WriteLine($"tether-flight with the sliding trail: 0x{line79:X16} ({(k79 ? "model 79's, bit for bit" : "NOT model 79's")}), "
                + $"{l79.Tether.PaidOut:F3} m paid out, trail {l79.Tether.TrailCount}: {(k79 ? "PASS" : "FAIL")}");
            // Model 77 (1.8's finding 2: the segments' Newton solves), its audit against its merge (74 onto 73): with
            // the known-wrong linear step (FlightSetup.TetherLinearStep) the tether golden is the merge's, every record and event, and with airmode
            // on too model 74's (the audit with airmode below), so the chain is the whole of 77's move against the merge. Since model 78
            // over model 73's 3360 bytes (77's record), the clean sensors.
            ulong one77 = GoldenFlights.FlyContact(Drone(), out FlightCore l77, tether: true, linearStep: true, layout: GoldenFlights.Model73Layout);
            bool k77 = one77 == GoldenFlights.Model77MergeTetherHash && l77.Tether.Chain.NewtonSolves == 0;
            lok &= k77;
            Console.WriteLine($"tether-flight with the linear step: 0x{one77:X16} ({(k77 ? "the merge's, bit for bit" : "NOT the merge's")}), "
                + $"{l77.Tether.PaidOut:F3} m paid out, {l77.Tether.Chain.Clips} clips, {l77.Tether.Chain.SafeSteps} safe steps, {l77.Tether.Chain.Corrections} corrections "
                + $"(with the Newton solves {l.Tether.Chain.Corrections}): {(k77 ? "PASS" : "FAIL")}");
            // Model 64's audit against model 63 (D01's canister; since model 65 with the 0-force hold): on model 63's drone.json (the old coil, Old(); its line leaves the
            // old rear cap, GoldenFlights.Model63Tether) with the fountain through the body every golden is model 63's pin, every
            // record and event; with 61's blocking the tether golden is the merge's on the old coil. Every audit below flies Old().
            // Model 73 (the physics integration 2 with D-086's controller: 69 merged onto 70), its audits. Against model 70: with
            // Betaflight's airmode on (every model before 69) each golden is its model 70 pin over the whole record, every record and
            // event, so the controller is the whole move. Against model 69: without airmode, the motors' heat off and over model 63's
            // 3040 bytes (the shafts' and the heat's blocks zero) each golden is its model 69 pin. Every older audit below flies
            // airmode on (GoldenFlights' airmode); with it model 65's pins over model 63's 3040 bytes are 69's own audit (the shafts' row).
            const int m73a = GoldenFlights.Model73Layout; // since model 75 over model 73's 3360 bytes, the clean sensors
            ulong a70p = GoldenFlights.FlyPowered(Drone(), out FlightCore ap, airmode: true, layout: m73a),
                a70r = GoldenFlights.FlyPowered(Drone(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, airmode: true, layout: m73a),
                a70c = GoldenFlights.FlyContact(Drone(), out FlightCore ac, airmode: true, layout: m73a), a70g = GoldenFlights.FlyRange(Drone(), out FlightCore ag, airmode: true, layout: m73a),
                a70l = GoldenFlights.FlyContact(Drone(), out FlightCore al, tether: true, airmode: true, layout: m73a);
            // Since model 77 (74's bent nozzle on the line) the drone.json is 74's, so with airmode on each golden is its model 74 pin
            // (74 kept 70's pins but the tether's, Model74TetherHash).
            bool k70 = a70p == GoldenFlights.Model70PoweredHash && a70r == GoldenFlights.Model70RainPoweredHash && a70c == GoldenFlights.Model70ContactHash
                && a70g == GoldenFlights.Model70RangeHash && a70l == GoldenFlights.Model74TetherHash;
            Console.WriteLine($"with airmode (model 74's controller): powered 0x{a70p:X16}, rain 0x{a70r:X16}, contact 0x{a70c:X16}, range 0x{a70g:X16}, tether 0x{a70l:X16} "
                + $"({(k70 ? "model 74's, bit for bit" : "NOT model 74's")}); without it the powered flight ends {(core.State.Position - ap.State.Position).Length():F3} m from it, "
                + $"the contact {(c.State.Position - ac.State.Position).Length() * 1e3:F1} mm ({c.Contacts.ImpactCount} impacts against {ac.Contacts.ImpactCount}), the range "
                + $"{(g.State.Position - ag.State.Position).Length() * 1e3:F1} mm ({g.Contacts.ImpactCount} against {ag.Contacts.ImpactCount}), the tether "
                + $"{(l.State.Position - al.State.Position).Length() * 1e3:F1} mm: {(k70 ? "PASS" : "FAIL")}");
            const int m63h = GoldenFlights.Model63Layout;
            ulong h69p = GoldenFlights.FlyPowered(Drone(), out _, layout: m63h),
                h69r = GoldenFlights.FlyPowered(Drone(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m63h),
                h69c = GoldenFlights.FlyContact(Drone(), out _, layout: m63h), h69g = GoldenFlights.FlyRange(Drone(), out _, layout: m63h),
                h69l = GoldenFlights.FlyContact(M70(), out _, tether: true, layout: m63h); // since model 77 the straight nozzle (Model70Drone)
            bool k69 = h69p == GoldenFlights.Model69PoweredHash && h69r == GoldenFlights.Model69RainPoweredHash && h69c == GoldenFlights.Model69ContactHash
                && h69g == GoldenFlights.Model69RangeHash && h69l == GoldenFlights.Model69TetherHash;
            Console.WriteLine($"without airmode, the motors' heat and over model 63's 3040 bytes: powered 0x{h69p:X16}, rain 0x{h69r:X16}, contact 0x{h69c:X16}, "
                + $"range 0x{h69g:X16}, tether 0x{h69l:X16} ({(k69 ? "model 69's, bit for bit" : "NOT model 69's")}): {(k69 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the fiber's chain fixes (model 65, 62 merged onto 64), its audit against model 64: with
            // the known-wrong 0-force hold (FlightSetup.TetherZeroForceHold: 64's chain, its hold and its safe pass) the line's flight
            // is model 64's, every record and event. Every older audit below flies with it too (they audit 64's chain and its parents).
            // Since model 68 (the shafts' block after 3040 bytes) the audits of models 63, 64 and 65 fly over model 63's 3040 bytes.
            const int m63 = GoldenFlights.Model63Layout;
            // Since model 74 the audits of models 64–70 on the canister fly model 70's drone.json (Model70Drone, its nozzle straight),
            // whose line FlyContact lets out of its 15° nozzle over the 4 mm lip with the old peel (GoldenFlights.Model70Tether).
            ulong line64 = GoldenFlights.FlyContact(M70(), out FlightCore l64, tether: true, coulomb: false, layout: m63, airmode: true);
            bool k64 = line64 == GoldenFlights.Model64TetherHash && l64.Tether.Chain.ZeroForceHold && !l.Tether.Chain.ZeroForceHold;
            lok &= k64;
            Console.WriteLine($"tether-flight with the 0-force hold: 0x{line64:X16} ({(k64 ? "model 64's, bit for bit" : "NOT model 64's")}); its line "
                + (l64.Tether.Broken ? $"breaks at step {l64.Tether.BrokenAt} by {l64.Tether.LastBreak.Mechanism}" : $"holds, {l64.Tether.PaidOut:F3} m paid out") + $", {l64.Tether.Chain.SafeSteps} safe steps, {l64.Tether.Chain.Wedges} wedges; with the rules the line "
                + (l.Tether.Broken ? $"breaks at step {l.Tether.BrokenAt} by {l.Tether.LastBreak.Mechanism} at {(Fpv450.Sim.Tether.FiberMember)l.Tether.LastBreak.Kind} {l.Tether.LastBreak.Member}" : $"holds, {l.Tether.PaidOut:F3} m paid out")
                + $", {l.Tether.Chain.SafeSteps} safe steps, {l.Tether.Chain.Corrections} corrections (64's {l64.Tether.Chain.Corrections}): {(k64 ? "PASS" : "FAIL")}");
            ulong o63p = GoldenFlights.FlyPowered(Old(), out _, layout: m63, airmode: true), o63r = GoldenFlights.FlyPowered(Old(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m63, airmode: true),
                o63c = GoldenFlights.FlyContact(Old(), out _, layout: m63, airmode: true), o63g = GoldenFlights.FlyRange(Old(), out _, layout: m63, airmode: true), o63t = GoldenFlights.FlyTumble(Old(), out _, out _, layout: m63),
                o63l = GoldenFlights.FlyContact(Old(), out FlightCore ol, tether: true, coulomb: false, body: false, layout: m63, airmode: true),
                o63b = GoldenFlights.FlyContact(Old(), out FlightCore ob, tether: true, coulomb: false, layout: m63, airmode: true);
            bool k63 = o63p == GoldenFlights.Model63PoweredHash && o63r == GoldenFlights.Model63RainPoweredHash && o63c == GoldenFlights.Model63ContactHash
                && o63g == GoldenFlights.Model63RangeHash && o63t == GoldenFlights.Model63TumbleHash && o63l == GoldenFlights.Model63TetherHash
                && o63b == GoldenFlights.Model63BodyTetherHash;
            lok &= k63;
            string Outcome(FlightCore x) => x.Tether.Broken ? $"broken at step {x.Tether.BrokenAt} by {x.Tether.LastBreak.Mechanism}" : $"whole, {x.Tether.PaidOut:F3} m paid out";
            Console.WriteLine($"on the old coil (model 63's drone.json), the fountain through the body: powered 0x{o63p:X16}, rain 0x{o63r:X16}, contact 0x{o63c:X16}, "
                + $"range 0x{o63g:X16}, tumble 0x{o63t:X16}, tether 0x{o63l:X16} ({(k63 ? "model 63's, bit for bit" : "NOT model 63's")}), the line {Outcome(ol)}; "
                + $"with 61's blocking 0x{o63b:X16} (the merge's), the line {Outcome(ob)}; on the canister the line {Outcome(l64)}: {(k63 ? "PASS" : "FAIL")}");
            // Since model 60 (the skid's frame on the line) every audit below against models 50-59 flies without the frame
            // (GoldenFlights.WithoutTheFrame); since model 63 (the bells on the line) each against a 2944-byte pin over model 57's
            // 2944 bytes (GoldenFlights.Model57Layout).
            // Model 61's audit against model 59 (since model 64 without the skid's frame, over model 57's 2944 bytes): with the fountain through the drone's body (FlightSetup.TetherFountainThroughBody)
            // the line's flight is model 59's, every record and event.
            ulong line59 = GoldenFlights.FlyContact(Old(), out FlightCore l59, tether: true, coulomb: false, body: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout, airmode: true);
            bool k61 = line59 == GoldenFlights.Model59TetherHash && !l59.Tether.Rotors.BodyBlocks && l.Tether.Rotors.BodyBlocks;
            lok &= k61;
            Console.WriteLine($"tether-flight with the fountain through the body: 0x{line59:X16} ({(line59 == GoldenFlights.Model59TetherHash ? "model 59's, bit for bit" : "NOT model 59's")}), "
                + $"the line {(l59.Tether.Broken ? $"broken at step {l59.Tether.BrokenAt} by {l59.Tether.LastBreak.Mechanism}" : "whole")}; with the body's blocking "
                + $"{(l.Tether.Broken ? $"broken at step {l.Tether.BrokenAt} by {l.Tether.LastBreak.Mechanism}" : "whole")}: {(k61 ? "PASS" : "FAIL")}");
            // Model 62's audit against model 59 (the line held between the drone's parts; since model 65 on the old coil, through the
            // body, without the skid's frame, over model 57's 2944 bytes): with the 0-force hold the line's flight is model 59's (above,
            // line59), with the rules model 62's, every record and event.
            ulong line62 = GoldenFlights.FlyContact(Old(), out FlightCore l62, tether: true, body: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout, airmode: true);
            bool k62 = line62 == GoldenFlights.Model62TetherHash && l59.Tether.Chain.ZeroForceHold && !l62.Tether.Chain.ZeroForceHold;
            lok &= k62;
            Console.WriteLine($"tether-flight on the old coil through the body without the frame, with the rules: 0x{line62:X16} ({(k62 ? "model 62's, bit for bit" : "NOT model 62's")}), "
                + $"the line {(l62.Tether.Broken ? $"broken at step {l62.Tether.BrokenAt} by {l62.Tether.LastBreak.Mechanism}" : "whole")}, {l62.Tether.Chain.SafeSteps} safe steps; "
                + $"with the 0-force hold {(l59.Tether.Broken ? $"broken at step {l59.Tether.BrokenAt}" : "whole")}, {l59.Tether.Chain.SafeSteps} safe steps: {(k62 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the cuts (model 59), its audit against the props at rest (model 57), and p7d-tether
            // 1.8's against 55: without the cuts and the link (FlightSetup.TetherWithoutCuts) the line's flight is model 57's, every
            // record and event; without a resting prop too it is model 55's over model 50's 2816 bytes (1.8's own audit).
            ulong line57 = GoldenFlights.FlyContact(Old(), out FlightCore l57, tether: true, coulomb: false, cuts: false, layout: GoldenFlights.Model57Layout, edit: GoldenFlights.WithoutTheFrame, airmode: true),
                line55cut = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, cuts: false, layout: GoldenFlights.Model50Layout, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, airmode: true);
            bool k57 = line57 == GoldenFlights.Model57TetherHash && l57.Tether.Cuts == null && line55cut == GoldenFlights.Model55TetherHash;
            lok &= k57;
            Console.WriteLine($"tether-flight without the cuts: 0x{line57:X16} ({(line57 == GoldenFlights.Model57TetherHash ? "model 57's, bit for bit" : "NOT model 57's")}), "
                + $"without a resting prop too over 2816 bytes 0x{line55cut:X16} ({(line55cut == GoldenFlights.Model55TetherHash ? "model 55's" : "NOT model 55's")}); with them the line "
                + (l.Tether.Broken ? $"breaks at step {l.Tether.BrokenAt} by {l.Tether.LastBreak.Mechanism} at {(Fpv450.Sim.Tether.FiberMember)l.Tether.LastBreak.Kind} {l.Tether.LastBreak.Member} "
                    + $"(the landing's disc: 1.8's known risk, the blade at {l.Tether.LastBreak.Radius:F2} m/s), the failsafe at {l.Failsafe?.Stage}" : "holds")
                + $", {l.Tether.Link?.Dips} dips, {l.Tether.Cuts?.Sections} sections read: {(k57 ? "PASS" : "FAIL")}");
            // Its audit against p7d-tether 1.8 (model 56): without a resting prop (ContactSpec.BladeRest off) the line's flight with
            // the cuts is model 56's over model 50's 2816 bytes, every record and event.
            ulong line56 = GoldenFlights.FlyContact(Old(), out FlightCore l56, tether: true, coulomb: false, layout: GoldenFlights.Model50Layout, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, body: false, airmode: true);
            bool k56 = line56 == GoldenFlights.Model56TetherHash;
            lok &= k56;
            Console.WriteLine($"tether-flight without a resting prop, over model 50's 2816 bytes: 0x{line56:X16} ({(k56 ? "model 56's, bit for bit" : "NOT model 56's")}), "
                + $"the line {(l56.Tether.Broken ? $"broken at step {l56.Tether.BrokenAt}" : "whole")}: {(k56 ? "PASS" : "FAIL")}");
            // p7d-tether 1.7's audit: without the fiber's contacts (FlightSetup.TetherWithoutContacts) the line's flight is model
            // 50's, every record and event (since model 57 over model 50's 2816 bytes).
            ulong line50 = GoldenFlights.FlyContact(Old(), out FlightCore l50, tether: true, coulomb: false, fiberContacts: false, layout: GoldenFlights.Model50Layout,
                edit: GoldenFlights.WithoutTheFrame, airmode: true);
            bool k50 = line50 == GoldenFlights.Model50TetherHash && l50.Tether.Contacts == null;
            lok &= k50;
            Console.WriteLine($"tether-flight without the fiber's contacts: 0x{line50:X16} ({(k50 ? "model 50's, bit for bit" : "NOT model 50's")}); "
                + $"with them {l.Tether.Contacts?.Inserted} contact nodes put in, {l.Tether.Contacts?.QueriesTotal} shape queries: {(k50 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the fiber's contacts (model 55), its audit against p7d-tether 1.7 (model 53): with the
            // hanging half's give and the stub's spring off (model 53's legs) the line's flight is 1.7's pin, every record and
            // event (no golden parts a leg); without the fiber's contacts it is model 51's, which kept model 50's (above). Since
            // model 57 over model 50's 2816 bytes; since model 59 without the cuts.
            ulong line53 = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, cuts: false, layout: GoldenFlights.Model50Layout, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheHangingGive, airmode: true);
            bool k53 = line53 == GoldenFlights.Model53TetherHash;
            lok &= k53;
            Console.WriteLine($"tether-flight without the hanging half's give: 0x{line53:X16} ({(k53 ? "model 53's, bit for bit" : "NOT model 53's")}): {(k53 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the props at rest (model 57), its audits: without a resting prop (ContactSpec.BladeRest
            // off) the line's flight with the fiber's contacts is model 55's over its 2816 bytes, every record and event, as it is
            // with the rest on; without the fiber's contacts and the hanging give it is p7f-damage 1.3b's model 52 pin over the
            // whole record. Since model 59 without the cuts (model 57's line).
            ulong line55 = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, cuts: false, layout: GoldenFlights.Model50Layout, edit: GoldenFlights.WithoutTheFrame, airmode: true),
                line55off = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, cuts: false, layout: GoldenFlights.Model50Layout, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, airmode: true),
                line52 = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, fiberContacts: false, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheHangingGive,
                    layout: GoldenFlights.Model57Layout, airmode: true);
            bool k55 = line55 == GoldenFlights.Model53TetherHash && line55off == GoldenFlights.Model53TetherHash, k52 = line52 == GoldenFlights.Model52TetherHash;
            lok &= k55 && k52;
            Console.WriteLine($"tether-flight over model 55's 2816 bytes: 0x{line55:X16}, without a resting prop 0x{line55off:X16} "
                + $"({(k55 ? "model 55's, bit for bit" : "NOT model 55's")}); without the fiber's contacts and the hanging give 0x{line52:X16} "
                + $"({(k52 ? "model 52's, bit for bit" : "NOT model 52's")}): {(k55 && k52 ? "PASS" : "FAIL")}");
            // p7f-damage 1.5 (model 58, the motors' bells): with the bells off every golden over model 57's 2944 bytes is model 57's pin
            // (the kept test WithoutTheBellsEachGoldenIsModel57s also flies them with the bells on, and checks the block zero). Since
            // model 63 (the bells on the line) without the skid's frame and the cuts.
            const int m57 = GoldenFlights.Model57Layout;
            Action<FlightSetup> bellless = GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheBells;
            ulong b57p = GoldenFlights.FlyPowered(Old(), out _, layout: m57, edit: bellless, airmode: true),
                b57c = GoldenFlights.FlyContact(Old(), out _, layout: m57, edit: bellless, airmode: true),
                b57g = GoldenFlights.FlyRange(Old(), out _, layout: m57, edit: bellless, airmode: true),
                b57l = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, cuts: false, layout: m57, edit: bellless, airmode: true);
            bool kb57 = b57p == GoldenFlights.Model57PoweredHash && b57c == GoldenFlights.Model57ContactHash && b57g == GoldenFlights.Model57RangeHash
                && b57l == GoldenFlights.Model57TetherHash;
            lok &= kb57;
            Console.WriteLine($"without the bells, the frame and the cuts, over model 57's 2944 bytes: powered 0x{b57p:X16}, contact 0x{b57c:X16}, range 0x{b57g:X16}, tether 0x{b57l:X16} "
                + $"({(kb57 ? "model 57's, bit for bit" : "NOT model 57's")}): {(kb57 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the tip slide and the fountain (model 48), its audit against the line (model 46): on
            // design 4's field (no fountain, no ground vortex; p7d-tether 1.6b's audit against 44 when it was alone) the line's
            // flight is model 46's, every record and event; no golden's disc cuts the soil, so with the tip slide off (model 43's
            // carve) the contact, range and tether goldens are their pins. Since model 50 (the lever on the line) design 4's air
            // gives p7g-legs 1.4's model 49 tether pin over the whole record (over model 50's 2816 bytes since model 52), and with the lever
            // off too model 46's over 2784 bytes; since model 55 without the fiber's contacts; since model 60 without the skid's frame
            // (WithoutTheFrame; WithoutTheLever carries it too).
            ulong line46 = GoldenFlights.FlyContact(Old(), out FlightCore l46, tether: true, coulomb: false, fiberContacts: false, recirculation: false, layout: GoldenFlights.Model50Layout,
                    edit: GoldenFlights.WithoutTheFrame, airmode: true),
                line46off = GoldenFlights.FlyContact(Old(), out _, tether: true, coulomb: false, fiberContacts: false, layout: m46, recirculation: false, edit: GoldenFlights.WithoutTheLever, airmode: true);
            Action<FlightSetup> slideOff = s => s.Contacts.TipSlide = false;
            ulong cso = GoldenFlights.FlyContact(Drone(), out FlightCore cs, edit: slideOff, airmode: true, layout: GoldenFlights.Model73Layout),
                gso = GoldenFlights.FlyRange(Drone(), out FlightCore gs, edit: slideOff, airmode: true, layout: GoldenFlights.Model73Layout),
                lso = GoldenFlights.FlyContact(Drone(), out FlightCore ls, tether: true, edit: slideOff, airmode: true, layout: GoldenFlights.Model73Layout);
            long carved = l46.Strikes.Passes + cs.Strikes.Passes + gs.Strikes.Passes + ls.Strikes.Passes;
            bool l44 = line46 == GoldenFlights.Model49TetherHash && line46off == GoldenFlights.Model46TetherHash && cso == GoldenFlights.Model70ContactHash && gso == GoldenFlights.Model70RangeHash
                && lso == GoldenFlights.Model74TetherHash && carved == 0; // since model 77 the bent nozzle's: model 74's tether pin
            lok &= l44;
            Console.WriteLine($"tether-flight on design 4's air: 0x{line46:X16} ({(line46 == GoldenFlights.Model49TetherHash ? "model 49's, bit for bit" : "NOT model 49's")}), "
                + $"the lever off too 0x{line46off:X16} ({(line46off == GoldenFlights.Model46TetherHash ? "model 46's" : "NOT model 46's")}); "
                + $"the tip slide off: contact 0x{cso:X16}, range 0x{gso:X16}, tether 0x{lso:X16} (model 74's pins, airmode on, over model 73's 3360 bytes), {carved} blade passes: {(l44 ? "PASS" : "FAIL")}");
            // The physics integration 2's audit against p7d-tether (model 34): on the rigid floor the line's flight is model 31's.
            ulong line31 = GoldenFlights.FlyContact(Bare(), out _, tether: true, fiberContacts: false, coulomb: false, soil: false, yawI: false, layout: m38, recirculation: false, airmode: true);
            bool l31 = line31 == GoldenFlights.Model31TetherHash;
            lok &= l31;
            Console.WriteLine($"tether-flight without surfaces.json: 0x{line31:X16} ({(l31 ? "model 31's, bit for bit" : "NOT model 31's")}): {(l31 ? "PASS" : "FAIL")}");
            // Betaflight's yaw I's audit (model 36): on model 34's yaw I (i_yaw just under 12, its Ki model 34's bit for bit)
            // every golden is model 34's pin, every record and event, so the yaw Ki is the whole move. Every audit above against
            // an earlier model flies it too; the tumble has no rotors and keeps its hash.
            ulong y34p = GoldenFlights.FlyPowered(Bare(), out _, yawI: false, layout: m38, airmode: true), y34c = GoldenFlights.FlyContact(Bare(), out _, yawI: false, layout: m38, airmode: true),
                y34g = GoldenFlights.FlyRange(Bare(), out _, yawI: false, layout: m38, airmode: true),
                y34r = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false, layout: m38, airmode: true),
                y34l = GoldenFlights.FlyContact(Bare(), out _, tether: true, fiberContacts: false, coulomb: false, yawI: false, layout: m38, recirculation: false, airmode: true);
            bool y34 = y34p == GoldenFlights.Model34PoweredHash && y34c == GoldenFlights.Model34ContactHash && y34g == GoldenFlights.Model34RangeOn38Hash
                && y34r == GoldenFlights.Model34RainPoweredHash && y34l == GoldenFlights.Model34TetherHash;
            Console.WriteLine($"on model 34's yaw I (i_yaw {GoldenFlights.Model34YawI:R}): powered 0x{y34p:X16}, contact 0x{y34c:X16}, range 0x{y34g:X16}, "
                + $"rain 0x{y34r:X16}, tether 0x{y34l:X16} ({(y34 ? "model 34's, bit for bit; the range with model 38's buried face" : "NOT model 34's")}): {(y34 ? "PASS" : "FAIL")}");
            // p7f-damage 1.2's audit (model 29), kept: with the blades' pitch draws off and the flapping's inertia at the rod, every
            // golden is model 26's (since model 40 without the line's mechanisms: no soil, no area laws, model 34's yaw I); each switch alone gives the share of the move it makes, printed (GoldenFlights' notes).
            DroneSpec d = GoldenFlights.WithModel26Legs(Old()); // since model 46 on model 26's legs
            Action<FlightSetup> rod = GoldenFlights.WithTheRodsInertia(d), nopitch = GoldenFlights.WithoutThePitch, both = s => { nopitch(s); rod(s); };
            MaterialRow[] layerless = GoldenFlights.WithoutTheAreaLaws();
            ulong a26 = GoldenFlights.FlyPowered(d, out _, edit: both, layout: m29, yawI: false, airmode: true),
                b26 = GoldenFlights.FlyContact(d, out _, edit: both, layout: m29, soil: false, yawI: false, airmode: true),
                c26 = GoldenFlights.FlyRange(d, out _, edit: both, layout: m29, materials: layerless, yawI: false, airmode: true);
            ulong r26 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: both, layout: m29, yawI: false, airmode: true);
            bool sok26 = a26 == GoldenFlights.Model26PoweredHash && b26 == GoldenFlights.Model26ContactHash && c26 == GoldenFlights.Model26RangeHash
                && r26 == GoldenFlights.Model26RainPoweredHash;
            Console.WriteLine($"the blades' pitch off and the rod's inertia: powered 0x{a26:X16}, contact 0x{b26:X16}, range 0x{c26:X16}, rain 0x{r26:X16} "
                + $"({(sok26 ? "model 26's, bit for bit" : "NOT model 26's")}): {(sok26 ? "PASS" : "FAIL")}");
            ulong ip = GoldenFlights.FlyPowered(d, out _, edit: nopitch, layout: m29, yawI: false, airmode: true),
                ic = GoldenFlights.FlyContact(d, out _, edit: nopitch, layout: m29, soil: false, yawI: false, airmode: true),
                ig = GoldenFlights.FlyRange(d, out _, edit: nopitch, layout: m29, materials: layerless, yawI: false, airmode: true);
            ulong ir = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: nopitch, layout: m29, yawI: false, airmode: true);
            ulong qp = GoldenFlights.FlyPowered(d, out _, edit: rod, layout: m29, yawI: false, airmode: true),
                qc = GoldenFlights.FlyContact(d, out _, edit: rod, layout: m29, soil: false, yawI: false, airmode: true),
                qg = GoldenFlights.FlyRange(d, out _, edit: rod, layout: m29, materials: layerless, yawI: false, airmode: true);
            ulong qr = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: rod, layout: m29, yawI: false, airmode: true);
            Console.WriteLine($"  the flapping's blade inertia alone (pitch off): powered 0x{ip:X16}, contact 0x{ic:X16}, range 0x{ig:X16}, rain 0x{ir:X16}");
            Console.WriteLine($"  the blades' pitch alone (the rod's inertia): powered 0x{qp:X16}, contact 0x{qc:X16}, range 0x{qg:X16}, rain 0x{qr:X16}");
            // p7f-damage 1.3's audit (model 35), kept: the goldens' discs cut nothing, so over model 29's 1904 bytes every golden is
            // model 29's pin, every record and event (since model 40 without the line's mechanisms); with the strikes off
            // (ContactSpec.BladeStrikes) the whole record is the pins'.
            ulong p29 = GoldenFlights.FlyPowered(d, out FlightCore pc, layout: m29, yawI: false, airmode: true),
                c29 = GoldenFlights.FlyContact(d, out FlightCore cc, layout: m29, soil: false, yawI: false, airmode: true);
            ulong g29 = GoldenFlights.FlyRange(d, out FlightCore gc, layout: m29, materials: layerless, yawI: false, airmode: true),
                t29 = GoldenFlights.FlyTumble(d, out _, out _, layout: m29);
            ulong r29 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m29, yawI: false, airmode: true);
            Action<FlightSetup> off = s => { if (s.Contacts != null) s.Contacts.BladeStrikes = false; };
            ulong coff = GoldenFlights.FlyContact(Drone(), out _, edit: off, airmode: true, layout: GoldenFlights.Model73Layout),
                goff = GoldenFlights.FlyRange(Drone(), out _, edit: off, airmode: true, layout: GoldenFlights.Model73Layout),
                loff = GoldenFlights.FlyContact(Drone(), out _, tether: true, edit: off, airmode: true, layout: GoldenFlights.Model73Layout);
            bool zok = p29 == GoldenFlights.Model29PoweredHash && c29 == GoldenFlights.Model29ContactHash && g29 == GoldenFlights.Model29RangeHash
                && t29 == GoldenFlights.Model29TumbleHash && r29 == GoldenFlights.Model29RainPoweredHash && coff == GoldenFlights.Model70ContactHash && goff == GoldenFlights.Model70RangeHash
                && loff == GoldenFlights.Model74TetherHash // since model 77 the bent nozzle's: model 74's tether pin
                && cc.Strikes != null && cc.Strikes.Passes == 0 && gc.Strikes.Passes == 0;
            Console.WriteLine($"over model 29's 1904 bytes: powered 0x{p29:X16}, contact 0x{c29:X16}, range 0x{g29:X16}, rain 0x{r29:X16}, tumble 0x{t29:X16} "
                + $"({(zok ? "model 29's, bit for bit" : "NOT model 29's")}), {cc.Strikes?.Passes} and {gc.Strikes?.Passes} blade passes in the contact and range flights; "
                + $"the strikes off: contact 0x{coff:X16}, range 0x{goff:X16}, tether 0x{loff:X16} (the pins): {(zok ? "PASS" : "FAIL")}");
            // The physics integration 2 with p7f-damage (model 40): on a drone.json without props.blade every golden is the line's
            // model 38 pin over its 2008 bytes, the strikes' block zero; without the line's mechanisms (no soil, no area laws, no
            // line, model 34's yaw I) every golden is p7f-damage's model 35 pin over its record (the record less the fiber's 104
            // bytes at 1904), the fiber's block zero. So each parent's mechanisms are the whole of each move against the other.
            int strikesTail = 0, fiber = 0;
            void Strikes(byte[] record)
            {
                for (int i = m38; i < m44; i++) if (record[i] != 0) strikesTail++;
            }
            void Fiber(byte[] record)
            {
                for (int i = m28; i < m38; i++) if (record[i] != 0) fiber++;
            }
            ulong b38p = GoldenFlights.FlyPowered(Bare(), out _, layout: m38, onRecord: Strikes, airmode: true),
                b38r = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m38, onRecord: Strikes, airmode: true),
                b38c = GoldenFlights.FlyContact(Bare(), out _, layout: m38, onRecord: Strikes, airmode: true), b38g = GoldenFlights.FlyRange(Bare(), out _, layout: m38, onRecord: Strikes, airmode: true),
                b38l = GoldenFlights.FlyContact(Bare(), out _, tether: true, fiberContacts: false, coulomb: false, layout: m38, onRecord: Strikes, recirculation: false, airmode: true),
                b38t = GoldenFlights.FlyTumble(Bare(), out _, out _, layout: m38);
            bool k38 = b38p == GoldenFlights.Model38PoweredHash && b38r == GoldenFlights.Model38RainPoweredHash && b38c == GoldenFlights.Model38ContactHash
                && b38g == GoldenFlights.Model38RangeHash && b38l == GoldenFlights.Model38TetherHash && b38t == GoldenFlights.Model38TumbleHash && strikesTail == 0;
            Console.WriteLine($"without props.blade, over the line's 2008 bytes: powered 0x{b38p:X16}, rain 0x{b38r:X16}, contact 0x{b38c:X16}, range 0x{b38g:X16}, "
                + $"tether 0x{b38l:X16}, tumble 0x{b38t:X16} ({(k38 ? "the line's model 38, bit for bit" : "NOT model 38's")}), {strikesTail} non-zero bytes in the "
                + $"strikes' block: {(k38 ? "PASS" : "FAIL")}");
            const int fiberBytes = GoldenFlights.Model35Skip;
            ulong l35p = GoldenFlights.FlyPowered(d, out _, m28, fiberBytes, layout: m44, onRecord: Fiber, yawI: false, airmode: true),
                l35r = GoldenFlights.FlyPowered(d, out _, m28, fiberBytes, layout: m44, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Fiber, yawI: false, airmode: true),
                l35c = GoldenFlights.FlyContact(d, out _, m28, fiberBytes, layout: m44, onRecord: Fiber, soil: false, yawI: false, airmode: true),
                l35g = GoldenFlights.FlyRange(d, out _, m28, fiberBytes, layout: m44, onRecord: Fiber, materials: layerless, yawI: false, airmode: true),
                l35t = GoldenFlights.FlyTumble(d, out _, out _, m28, fiberBytes, m44);
            bool k35 = l35p == GoldenFlights.Model35PoweredHash && l35r == GoldenFlights.Model35RainPoweredHash && l35c == GoldenFlights.Model35ContactHash
                && l35g == GoldenFlights.Model35RangeHash && l35t == GoldenFlights.Model35TumbleHash && fiber == 0;
            Console.WriteLine($"without the line's mechanisms, over p7f-damage's record: powered 0x{l35p:X16}, rain 0x{l35r:X16}, contact 0x{l35c:X16}, "
                + $"range 0x{l35g:X16}, tumble 0x{l35t:X16} ({(k35 ? "p7f-damage's model 35, bit for bit" : "NOT model 35's")}), {fiber} non-zero bytes in the fiber's "
                + $"block: {(k35 ? "PASS" : "FAIL")}");
            // p7g-legs' audits, kept (since model 46 each without the line's mechanisms: no soil, no area laws, no props.blade,
            // model 34's yaw I; no line, no cut). 1.1's (model 37): on model 26's legs every golden is model 26's over 1904 bytes.
            ulong q26 = GoldenFlights.FlyPowered(M26(), out _, layout: m28, yawI: false, airmode: true), x26 = GoldenFlights.FlyContact(M26(), out _, layout: m28, soil: false, yawI: false, airmode: true),
                h26 = GoldenFlights.FlyRange(M26(), out _, layout: m28, materials: layerless, yawI: false, airmode: true);
            ulong v26 = GoldenFlights.FlyPowered(M26(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, yawI: false, airmode: true);
            ulong u26 = GoldenFlights.FlyTumble(M26(), out _, out _, layout: m28);
            bool m26 = q26 == GoldenFlights.Model26PoweredHash && x26 == GoldenFlights.Model26ContactHash && h26 == GoldenFlights.Model26RangeHash
                && v26 == GoldenFlights.Model26RainPoweredHash && u26 == GoldenFlights.Model26TumbleHash;
            Console.WriteLine($"model 26's legs (CFRP 11 and 20 g, the assumed 10 kN/m and 45 N.s/m, the leg shapes 6600 N/m): powered 0x{q26:X16}, contact 0x{x26:X16}, "
                + $"range 0x{h26:X16}, rain 0x{v26:X16}, tumble 0x{u26:X16} ({(m26 ? "model 26's, bit for bit" : "NOT model 26's")}): {(m26 ? "PASS" : "FAIL")}");
            // 1.2's (model 39): without the legs' side give and their law, over model 37's 1904 bytes, every golden is model 37's.
            ulong p37 = GoldenFlights.FlyPowered(M37(), out _, layout: m28, yawI: false, airmode: true), c37 = GoldenFlights.FlyContact(M37(), out _, layout: m28, soil: false, yawI: false, airmode: true),
                g37 = GoldenFlights.FlyRange(M37(), out _, layout: m28, materials: layerless, yawI: false, airmode: true);
            ulong w37 = GoldenFlights.FlyPowered(M37(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, yawI: false, airmode: true);
            ulong t37 = GoldenFlights.FlyTumble(M37(), out _, out _, layout: m28);
            bool m37 = p37 == GoldenFlights.Model37PoweredHash && c37 == GoldenFlights.Model37ContactHash && g37 == GoldenFlights.Model37RangeHash
                && w37 == GoldenFlights.Model37RainPoweredHash && t37 == GoldenFlights.Model37TumbleHash;
            Console.WriteLine($"model 37's leg law (no side give, no failure law): powered 0x{p37:X16}, contact 0x{c37:X16}, range 0x{g37:X16}, rain 0x{w37:X16}, "
                + $"tumble 0x{t37:X16} ({(m37 ? "model 37's, bit for bit" : "NOT model 37's")}): {(m37 ? "PASS" : "FAIL")}");
            // 1.3's (model 41): the flights that touch nothing are model 39's over its 2072 bytes (the first 1904, then the legs' state).
            DroneSpec legsNow = GoldenFlights.WithoutTheBlade(Old());
            const int legSkip = GoldenFlights.Model41Skip, m39 = GoldenFlights.Model39Layout;
            ulong p39 = GoldenFlights.FlyPowered(legsNow, out _, m28, legSkip, layout: m39, yawI: false, airmode: true),
                w39 = GoldenFlights.FlyPowered(legsNow, out _, m28, legSkip, layout: m39, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false, airmode: true),
                t39 = GoldenFlights.FlyTumble(legsNow, out _, out _, m28, legSkip, m39);
            bool k39 = p39 == GoldenFlights.Model39PoweredHash && w39 == GoldenFlights.Model39RainPoweredHash && t39 == GoldenFlights.Model39TumbleHash;
            Console.WriteLine($"over model 39's record, the flights that touch nothing: powered 0x{p39:X16}, rain 0x{w39:X16}, tumble 0x{t39:X16} "
                + $"({(k39 ? "model 39's, bit for bit" : "NOT model 39's")}): {(k39 ? "PASS" : "FAIL")}");
            // The physics integration 2 with p7g-legs (model 46): on model 26's legs every golden is the line's model 44 pin over its
            // 2392 bytes, every leg intact; without the line's mechanisms every golden is p7g-legs' model 41 pin over its record (the
            // record less the fiber's and the strikes' 488 bytes at 1904), both blocks zero. So each parent's mechanisms are the
            // whole of each move against the other.
            uint legStates = 0;
            void LegStates(byte[] record)
            {
                for (int i = m44; i < m44 + 8; i++) legStates |= record[i];
            }
            DroneSpec l26 = GoldenFlights.WithModel26Legs(Old());
            ulong e44p = GoldenFlights.FlyPowered(l26, out _, layout: m44, onRecord: LegStates, airmode: true),
                e44r = GoldenFlights.FlyPowered(l26, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m44, onRecord: LegStates, airmode: true),
                e44c = GoldenFlights.FlyContact(l26, out _, layout: m44, onRecord: LegStates, airmode: true), e44g = GoldenFlights.FlyRange(l26, out _, layout: m44, onRecord: LegStates, airmode: true),
                e44l = GoldenFlights.FlyContact(l26, out _, tether: true, fiberContacts: false, coulomb: false, layout: m44, onRecord: LegStates, recirculation: false, airmode: true),
                e47l = GoldenFlights.FlyContact(l26, out _, tether: true, fiberContacts: false, coulomb: false, layout: m44, onRecord: LegStates, airmode: true),
                e44t = GoldenFlights.FlyTumble(l26, out _, out _, layout: m44);
            bool k44 = e44p == GoldenFlights.Model44PoweredHash && e44r == GoldenFlights.Model44RainPoweredHash && e44c == GoldenFlights.Model44ContactHash
                && e44g == GoldenFlights.Model44RangeHash && e44l == GoldenFlights.Model44TetherHash && e44t == GoldenFlights.Model44TumbleHash && legStates == 0;
            Console.WriteLine($"on model 26's legs, over the line's 2392 bytes: powered 0x{e44p:X16}, rain 0x{e44r:X16}, contact 0x{e44c:X16}, range 0x{e44g:X16}, "
                + $"tether on design 4's air 0x{e44l:X16}, tumble 0x{e44t:X16} ({(k44 ? "the line's model 44, bit for bit, and so the tip slide's 45" : "NOT model 44's")}), "
                + $"leg states 0x{legStates:X2}: {(k44 ? "PASS" : "FAIL")}");
            // Model 48's audit against p7d-tether 1.6b (model 47, the fountain on 44): on model 26's legs the tether golden with the
            // fountain is model 47's pin over 2392 bytes (the other goldens fly no line: model 47's are model 44's).
            bool k47 = e47l == GoldenFlights.Model47TetherHash && legStates == 0;
            k44 &= k47;
            Console.WriteLine($"on model 26's legs, over the line's 2392 bytes, the tether with the fountain: 0x{e47l:X16} "
                + $"({(k47 ? "p7d-tether 1.6b's model 47, bit for bit" : "NOT model 47's")}): {(k47 ? "PASS" : "FAIL")}");
            int lineBlocks = 0;
            void LineBlocks(byte[] record)
            {
                for (int i = m28; i < m44; i++) if (record[i] != 0) lineBlocks++;
            }
            // Since model 49 over the record before the legs' lean, and the lever off (1.3's softening, model 41's).
            Action<FlightSetup> noLever = GoldenFlights.WithoutTheLever;
            ulong l41p = GoldenFlights.FlyPowered(legsNow, out _, m28, legSkip, layout: m46, onRecord: LineBlocks, yawI: false, airmode: true),
                l41r = GoldenFlights.FlyPowered(legsNow, out _, m28, legSkip, layout: m46, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: LineBlocks, yawI: false, airmode: true),
                l41c = GoldenFlights.FlyContact(legsNow, out _, m28, legSkip, layout: m46, onRecord: LineBlocks, soil: false, yawI: false, edit: noLever, airmode: true),
                l41g = GoldenFlights.FlyRange(legsNow, out _, m28, legSkip, layout: m46, onRecord: LineBlocks, materials: layerless, yawI: false, edit: noLever, airmode: true),
                l41t = GoldenFlights.FlyTumble(legsNow, out _, out _, m28, legSkip, m46);
            bool k41 = l41p == GoldenFlights.Model41PoweredHash && l41r == GoldenFlights.Model41RainPoweredHash && l41c == GoldenFlights.Model41ContactOnTheSectionsYieldHash
                && l41g == GoldenFlights.Model41RangeHash && l41t == GoldenFlights.Model41TumbleHash && lineBlocks == 0;
            Console.WriteLine($"without the line's mechanisms, over p7g-legs' record: powered 0x{l41p:X16}, rain 0x{l41r:X16}, contact 0x{l41c:X16}, "
                + $"range 0x{l41g:X16}, tumble 0x{l41t:X16} ({(k41 ? "p7g-legs' model 41, bit for bit" : "NOT model 41's")}), {lineBlocks} non-zero bytes in the fiber's "
                + $"and the strikes' blocks: {(k41 ? "PASS" : "FAIL")}");
            // p7g-legs 1.4 (model 49): with the lever off (1.3's linear softening) every golden is model 46's pin over its 2784 bytes,
            // the lean zero; with it on, the flights that touch nothing are model 46's too. So the lever is the whole move. Since
            // model 50 (the lever on the line) the pins are model 48's: the tether flight's is the fountain's (Model48TetherHash).
            int lean = 0;
            void Lean(byte[] record)
            {
                for (int i = m46; i < LogFormat.StepSize; i++) if (record[i] != 0) lean++;
            }
            DroneSpec now = Old();
            ulong n46c = GoldenFlights.FlyContact(now, out _, layout: m46, onRecord: Lean, edit: noLever, airmode: true),
                n46g = GoldenFlights.FlyRange(now, out _, layout: m46, onRecord: Lean, edit: noLever, airmode: true),
                n46l = GoldenFlights.FlyContact(now, out _, tether: true, fiberContacts: false, coulomb: false, layout: m46, onRecord: Lean, edit: noLever, airmode: true),
                n46p = GoldenFlights.FlyPowered(now, out _, layout: m46, onRecord: Lean, airmode: true),
                n46r = GoldenFlights.FlyPowered(now, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m46, onRecord: Lean, airmode: true),
                n46t = GoldenFlights.FlyTumble(now, out _, out _, layout: m46);
            bool k46 = n46c == GoldenFlights.Model46ContactHash && n46g == GoldenFlights.Model46RangeHash && n46l == GoldenFlights.Model48TetherHash
                && n46p == GoldenFlights.Model46PoweredHash && n46r == GoldenFlights.Model46RainPoweredHash && n46t == GoldenFlights.Model46TumbleHash && lean == 0;
            Console.WriteLine($"without the lever (1.3's softening), over model 46's 2784 bytes: contact 0x{n46c:X16}, range 0x{n46g:X16}, tether 0x{n46l:X16}; on it, "
                + $"powered 0x{n46p:X16}, rain 0x{n46r:X16}, tumble 0x{n46t:X16} ({(k46 ? "model 48's, bit for bit" : "NOT model 48's")}), {lean} non-zero bytes "
                + $"in the lean: {(k46 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the skid's frame (model 60), its audit against model 59: without the frame (one value
            // both ways) the contact, range and tether goldens are model 59's pins over the whole record, every record and event;
            // with it the pins (above). So the frame is the whole move. Its audit against the frame alone (model 54): without the
            // cuts, the fiber's contacts and the props at rest, over model 50's 2816 bytes, they are p7g-legs' model 54 pins.
            Action<FlightSetup> frameless = GoldenFlights.WithoutTheFrame;
            ulong f59c = GoldenFlights.FlyContact(now, out _, layout: m57, edit: frameless, airmode: true), f59g = GoldenFlights.FlyRange(now, out _, layout: m57, edit: frameless, airmode: true),
                f59l = GoldenFlights.FlyContact(now, out FlightCore fl, tether: true, coulomb: false, layout: m57, edit: frameless, body: false, airmode: true);
            bool k59 = f59c == GoldenFlights.Model59ContactHash && f59g == GoldenFlights.Model59RangeHash && f59l == GoldenFlights.Model59TetherHash;
            Console.WriteLine($"without the skid's frame (one value both ways), over model 57's 2944 bytes: contact 0x{f59c:X16}, range 0x{f59g:X16}, tether 0x{f59l:X16} "
                + $"({(k59 ? "model 59's, bit for bit" : "NOT model 59's")}), the line {(fl.Tether.Broken ? $"broken at step {fl.Tether.BrokenAt}" : "whole")}: {(k59 ? "PASS" : "FAIL")}");
            const int m50 = GoldenFlights.Model50Layout;
            Action<FlightSetup> passing = GoldenFlights.WithoutTheRest;
            ulong f54c = GoldenFlights.FlyContact(now, out _, layout: m50, edit: passing, airmode: true), f54g = GoldenFlights.FlyRange(now, out _, layout: m50, edit: passing, airmode: true),
                f54l = GoldenFlights.FlyContact(now, out _, tether: true, coulomb: false, fiberContacts: false, layout: m50, edit: passing, airmode: true);
            bool k54 = f54c == GoldenFlights.Model54ContactHash && f54g == GoldenFlights.Model54RangeHash && f54l == GoldenFlights.Model54TetherHash;
            Console.WriteLine($"without the cuts, the fiber's contacts and the props at rest, over model 50's 2816 bytes: contact 0x{f54c:X16}, range 0x{f54g:X16}, "
                + $"tether 0x{f54l:X16} ({(k54 ? "the frame's model 54, bit for bit" : "NOT model 54's")}): {(k54 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the bells (model 63), its audits: with the bells off every golden over model 57's 2944
            // bytes is model 60's pin (the kept test WithoutTheBellsEachGoldenIsModel60s flies them on too and checks the block zero);
            // without the skid's frame and the cuts every golden is p7f-damage 1.5's model 58 pin over the whole record
            // (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
            Action<FlightSetup> bellsOff = GoldenFlights.WithoutTheBells;
            ulong b60p = GoldenFlights.FlyPowered(now, out _, layout: m57, edit: bellsOff, airmode: true), b60c = GoldenFlights.FlyContact(now, out _, layout: m57, edit: bellsOff, airmode: true),
                b60g = GoldenFlights.FlyRange(now, out _, layout: m57, edit: bellsOff, airmode: true), b60l = GoldenFlights.FlyContact(now, out _, tether: true, coulomb: false, layout: m57, edit: bellsOff, body: false, airmode: true);
            bool k60 = b60p == GoldenFlights.Model60PoweredHash && b60c == GoldenFlights.Model60ContactHash && b60g == GoldenFlights.Model60RangeHash
                && b60l == GoldenFlights.Model60TetherHash;
            Console.WriteLine($"without the bells, over model 57's 2944 bytes: powered 0x{b60p:X16}, contact 0x{b60c:X16}, range 0x{b60g:X16}, tether 0x{b60l:X16} "
                + $"({(k60 ? "model 60's, bit for bit" : "NOT model 60's")}): {(k60 ? "PASS" : "FAIL")}");
            ulong f58p = GoldenFlights.FlyPowered(now, out _, edit: frameless, layout: m63, airmode: true), f58c = GoldenFlights.FlyContact(now, out _, edit: frameless, layout: m63, airmode: true),
                f58g = GoldenFlights.FlyRange(now, out _, edit: frameless, layout: m63, airmode: true),
                f58l = GoldenFlights.FlyContact(now, out _, tether: true, coulomb: false, cuts: false, edit: frameless, layout: m63, airmode: true);
            bool k58 = f58p == GoldenFlights.Model58PoweredHash && f58c == GoldenFlights.Model58ContactHash && f58g == GoldenFlights.Model58RangeHash
                && f58l == GoldenFlights.Model58TetherHash;
            Console.WriteLine($"without the skid's frame and the cuts, over model 63's 3040 bytes: powered 0x{f58p:X16}, contact 0x{f58c:X16}, range 0x{f58g:X16}, "
                + $"tether 0x{f58l:X16} ({(k58 ? "the bells' model 58, bit for bit" : "NOT model 58's")}): {(k58 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the shafts (model 68, 66 merged onto 65), its audits. Against model 65: with the shafts
            // and bearings off every golden on the canister over model 63's 3040 bytes is model 65's pin (the kept test
            // WithoutTheShaftsEachGoldenIsModel65s flies them on too and checks the block zero). Against model 66: on model 63's
            // drone.json, the fountain through the body, the 0-force hold and no blade root, every golden is model 66's pin over the
            // whole record (OnModel63sDroneEachGoldenIsModel66s).
            Action<FlightSetup> shaftsOff = GoldenFlights.WithoutTheShafts;
            DroneSpec canister = M70(); // since model 74 a fixture (the nozzle straight on the 15° canister)
            ulong s65p = GoldenFlights.FlyPowered(canister, out _, layout: m63, edit: shaftsOff, airmode: true), s65c = GoldenFlights.FlyContact(canister, out _, layout: m63, edit: shaftsOff, airmode: true),
                s65g = GoldenFlights.FlyRange(canister, out _, layout: m63, edit: shaftsOff, airmode: true), s65l = GoldenFlights.FlyContact(canister, out _, tether: true, layout: m63, edit: shaftsOff, airmode: true);
            bool k65 = s65p == GoldenFlights.Model65PoweredHash && s65c == GoldenFlights.Model65ContactHash && s65g == GoldenFlights.Model65RangeHash
                && s65l == GoldenFlights.Model65TetherHash;
            Console.WriteLine($"without the shafts and the bearings, over model 63's 3040 bytes: powered 0x{s65p:X16}, contact 0x{s65c:X16}, range 0x{s65g:X16}, "
                + $"tether 0x{s65l:X16} ({(k65 ? "model 65's, bit for bit" : "NOT model 65's")}): {(k65 ? "PASS" : "FAIL")}");
            Action<FlightSetup> rootless = GoldenFlights.WithoutTheBladeRoot;
            const int m68 = GoldenFlights.Model68Layout; // since model 70 over model 68's 3168 bytes, the motors' heat off
            ulong s66p = GoldenFlights.FlyPowered(now, out _, edit: rootless, layout: m68, airmode: true),
                s66r = GoldenFlights.FlyPowered(now, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: rootless, layout: m68, airmode: true),
                s66c = GoldenFlights.FlyContact(now, out _, edit: rootless, layout: m68, airmode: true), s66g = GoldenFlights.FlyRange(now, out _, edit: rootless, layout: m68, airmode: true),
                s66l = GoldenFlights.FlyContact(now, out FlightCore l66, tether: true, coulomb: false, body: false, edit: rootless, layout: m68, airmode: true),
                s66t = GoldenFlights.FlyTumble(now, out _, out _, layout: m68);
            bool k66 = s66p == GoldenFlights.Model66PoweredHash && s66r == GoldenFlights.Model66RainPoweredHash && s66c == GoldenFlights.Model66ContactHash
                && s66g == GoldenFlights.Model66RangeHash && s66l == GoldenFlights.Model66TetherHash && s66t == GoldenFlights.Model66TumbleHash;
            Console.WriteLine($"on the old coil through the body, with the 0-force hold and no blade root: powered 0x{s66p:X16}, rain 0x{s66r:X16}, contact 0x{s66c:X16}, "
                + $"range 0x{s66g:X16}, tether 0x{s66l:X16}, tumble 0x{s66t:X16} ({(k66 ? "model 66's, bit for bit" : "NOT model 66's")}), the line "
                + $"{(l66.Tether.Broken ? $"broken at step {l66.Tether.BrokenAt} by {l66.Tether.LastBreak.Mechanism}" : "whole")}: {(k66 ? "PASS" : "FAIL")}");
            // p7f-damage 1.6b (model 70), its audit against model 68: without the motors' heat and dirt every golden on the canister over
            // model 68's 3168 bytes is model 68's pin (the kept test WithoutTheHeatEachGoldenIsModel68s also checks the block zero).
            ulong h68p = GoldenFlights.FlyPowered(canister, out _, layout: m68, airmode: true),
                h68r = GoldenFlights.FlyPowered(canister, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m68, airmode: true),
                h68c = GoldenFlights.FlyContact(canister, out _, layout: m68, airmode: true), h68g = GoldenFlights.FlyRange(canister, out _, layout: m68, airmode: true),
                h68l = GoldenFlights.FlyContact(canister, out _, tether: true, layout: m68, airmode: true), h68t = GoldenFlights.FlyTumble(canister, out _, out _, layout: m68);
            bool k68 = h68p == GoldenFlights.Model68PoweredHash && h68r == GoldenFlights.Model68RainPoweredHash && h68c == GoldenFlights.Model68ContactHash
                && h68g == GoldenFlights.Model68RangeHash && h68l == GoldenFlights.Model68TetherHash && h68t == GoldenFlights.Model68TumbleHash;
            Console.WriteLine($"without the motors' heat and dirt, over model 68's 3168 bytes: powered 0x{h68p:X16}, rain 0x{h68r:X16}, contact 0x{h68c:X16}, "
                + $"range 0x{h68g:X16}, tether 0x{h68l:X16}, tumble 0x{h68t:X16} ({(k68 ? "model 68's, bit for bit" : "NOT model 68's")}): {(k68 ? "PASS" : "FAIL")}");
            // The physics integration 2 with the losses (model 75, 72 merged onto 73), its audits. Against model 73: with the clean
            // sensors (the arms stiff in their plane, no gyro range; every flight over a layout short of the record) every golden over
            // model 73's 3360 bytes is model 73's pin (WithTheCleanSensorsEachGoldenIsModel77s also checks the arms' block zero).
            // Against model 72: with airmode on and the motors' heat off, over model 72's bytes (the record's first 3168 and its last
            // 176, the heat's block skipped), every golden is model 72's pin (WithAirmodeAndWithoutTheHeatEachGoldenIsModel72s).
            const int m73 = GoldenFlights.Model73Layout;
            ulong c73p = GoldenFlights.FlyPowered(canister, out _, layout: m73),
                c73r = GoldenFlights.FlyPowered(canister, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m73),
                c73c = GoldenFlights.FlyContact(canister, out _, layout: m73), c73g = GoldenFlights.FlyRange(canister, out _, layout: m73),
                c73l = GoldenFlights.FlyContact(canister, out _, tether: true, layout: m73), c73t = GoldenFlights.FlyTumble(canister, out _, out _, layout: m73);
            bool k73 = c73p == GoldenFlights.Model73PoweredHash && c73r == GoldenFlights.Model73RainPoweredHash && c73c == GoldenFlights.Model73ContactHash
                && c73g == GoldenFlights.Model73RangeHash && c73l == GoldenFlights.Model73TetherHash && c73t == GoldenFlights.Model73TumbleHash;
            Console.WriteLine($"with the clean sensors, over model 73's 3360 bytes: powered 0x{c73p:X16}, rain 0x{c73r:X16}, contact 0x{c73c:X16}, range 0x{c73g:X16}, "
                + $"tether 0x{c73l:X16}, tumble 0x{c73t:X16} ({(k73 ? "model 73's, bit for bit" : "NOT model 73's")}): {(k73 ? "PASS" : "FAIL")}");
            const int at72 = GoldenFlights.Model68Layout, skip72 = GoldenFlights.HeatSkip;
            ulong a72p = GoldenFlights.FlyPowered(canister, out _, at72, skip72, heat: false, airmode: true),
                a72r = GoldenFlights.FlyPowered(canister, out _, at72, skip72, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, heat: false, airmode: true),
                a72c = GoldenFlights.FlyContact(canister, out _, at72, skip72, heat: false, airmode: true),
                a72g = GoldenFlights.FlyRange(canister, out _, at72, skip72, heat: false, airmode: true),
                a72l = GoldenFlights.FlyContact(canister, out _, at72, skip72, tether: true, heat: false, airmode: true),
                a72t = GoldenFlights.FlyTumble(canister, out _, out _, at72, skip72);
            bool k72 = a72p == GoldenFlights.Model72PoweredHash && a72r == GoldenFlights.Model72RainPoweredHash && a72c == GoldenFlights.Model72ContactHash
                && a72g == GoldenFlights.Model72RangeHash && a72l == GoldenFlights.Model72TetherHash && a72t == GoldenFlights.Model72TumbleHash;
            Console.WriteLine($"with airmode on and without the motors' heat, over model 72's bytes (the heat's 192 skipped): powered 0x{a72p:X16}, rain 0x{a72r:X16}, "
                + $"contact 0x{a72c:X16}, range 0x{a72g:X16}, tether 0x{a72l:X16}, tumble 0x{a72t:X16} ({(k72 ? "model 72's, bit for bit" : "NOT model 72's")}): {(k72 ? "PASS" : "FAIL")}");
            // The climb after a pitch-back (model 76, D-087), its audit against model 75: without the edgewise flow's thrust and torque
            // (RotorFlappingSpec.EdgewiseThrust off) every golden is model 75's pin (WithoutTheEdgewiseFlowEachGoldenIsModel75s), the range
            // on the wake grid's version 1, which 75 flew.
            // Since model 79 (76 merged onto 78) the tether flies 75's line: model 70's drone.json with its exit, lip and peel and the
            // linear step (with the edgewise flow, model 76's tether pin).
            DroneSpec d76 = Drone();
            ulong e75p = GoldenFlights.FlyPowered(d76, out _, edgewise: false),
                e75r = GoldenFlights.FlyPowered(d76, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edgewise: false),
                e75c = GoldenFlights.FlyContact(d76, out _, edgewise: false), e75g = GoldenFlights.FlyRange(d76, out _, edgewise: false, wakes2: false),
                e75l = GoldenFlights.FlyContact(M70(), out _, tether: true, linearStep: true, edgewise: false);
            bool k75 = e75p == GoldenFlights.Model75PoweredHash && e75r == GoldenFlights.Model75RainPoweredHash && e75c == GoldenFlights.Model75ContactHash
                && e75g == GoldenFlights.Model75RangeHash && e75l == GoldenFlights.Model75TetherHash;
            Console.WriteLine($"without the edgewise flow: powered 0x{e75p:X16}, rain 0x{e75r:X16}, contact 0x{e75c:X16}, range 0x{e75g:X16}, tether 0x{e75l:X16} "
                + $"({(k75 ? "model 75's, bit for bit" : "NOT model 75's")}): {(k75 ? "PASS" : "FAIL")}");
            // p7d-tether 1.8 (model 74), its audit against model 70: on model 70's drone.json (its nozzle straight on the 15° canister) with model 70's
            // exit, lip and peel (Model70Tether) every golden is model 70's pin (the kept test WithTheOldExitAndPeelEachGoldenIsModel70s).
            // Since model 77 (73's controller on the line) flown with airmode on, as model 70 flew; since model 78 over model 73's
            // 3360 bytes (model 70's record), the clean sensors.
            ulong e70p = GoldenFlights.FlyPowered(canister, out _, airmode: true, layout: m73),
                e70r = GoldenFlights.FlyPowered(canister, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, airmode: true, layout: m73),
                e70c = GoldenFlights.FlyContact(canister, out _, airmode: true, layout: m73), e70g = GoldenFlights.FlyRange(canister, out _, airmode: true, layout: m73),
                e70l = GoldenFlights.FlyContact(canister, out FlightCore l70, tether: true, airmode: true, layout: m73), e70t = GoldenFlights.FlyTumble(canister, out _, out _, layout: m73);
            bool k70e = e70p == GoldenFlights.Model70PoweredHash && e70r == GoldenFlights.Model70RainPoweredHash && e70c == GoldenFlights.Model70ContactHash
                && e70g == GoldenFlights.Model70RangeHash && e70l == GoldenFlights.Model70TetherHash && e70t == GoldenFlights.Model70TumbleHash;
            Console.WriteLine($"with the straight 15° nozzle, the 4 mm lip and the old peel on model 70's drone.json: powered 0x{e70p:X16}, rain 0x{e70r:X16}, contact 0x{e70c:X16}, "
                + $"range 0x{e70g:X16}, tether 0x{e70l:X16}, tumble 0x{e70t:X16} ({(k70e ? "model 70's, bit for bit" : "NOT model 70's")}), the line "
                + $"{(l70.Tether.Broken ? $"broken at step {l70.Tether.BrokenAt} by {l70.Tether.LastBreak.Mechanism}" : "whole")}: {(k70e ? "PASS" : "FAIL")}");
            // The physics integration 2 with the fiber's Newton step and its cut criteria (model 78, 77 merged onto 75), its audits.
            // Against model 77: with the clean sensors (every flight over model 73's 3360 bytes, 77's record) and the Newton step every
            // golden is model 77's pin, every record and event (77 kept 73's pins but the tether's). Against model 75: with the known-wrong
            // linear step on model 70's drone.json (its straight nozzle) with model 70's exit, lip and peel (Model70Tether) the tether
            // golden is model 75's (75's line); every golden without the line is 75's own pin (above). So 77's line is the whole of 78's
            // move against 75 and 75's arms and gyro range the whole against 77 (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel77s,
            // WithTheLinearStepAndTheOldExitTheTetherGoldenIsModel75s).
            ulong n77p = GoldenFlights.FlyPowered(Drone(), out _, layout: m73),
                n77r = GoldenFlights.FlyPowered(Drone(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m73),
                n77c = GoldenFlights.FlyContact(Drone(), out _, layout: m73), n77g = GoldenFlights.FlyRange(Drone(), out _, layout: m73),
                n77l = GoldenFlights.FlyContact(Drone(), out FlightCore l77n, tether: true, layout: m73, linearStep: false),
                n77t = GoldenFlights.FlyTumble(Drone(), out _, out _, layout: m73);
            bool k77n = n77p == GoldenFlights.Model73PoweredHash && n77r == GoldenFlights.Model73RainPoweredHash && n77c == GoldenFlights.Model73ContactHash
                && n77g == GoldenFlights.Model73RangeHash && n77l == GoldenFlights.Model77TetherHash && n77t == GoldenFlights.Model73TumbleHash;
            Console.WriteLine($"with the clean sensors and the Newton step, over model 73's 3360 bytes: powered 0x{n77p:X16}, rain 0x{n77r:X16}, contact 0x{n77c:X16}, "
                + $"range 0x{n77g:X16}, tether 0x{n77l:X16}, tumble 0x{n77t:X16} ({(k77n ? "model 77's, bit for bit" : "NOT model 77's")}), "
                + $"{l77n.Tether.Chain.NewtonSolves} Newton solves: {(k77n ? "PASS" : "FAIL")}");
            // Since model 79 (76's edgewise thrust on the line) without the edgewise thrust.
            ulong o75l = GoldenFlights.FlyContact(canister, out FlightCore l75o, tether: true, linearStep: true, edgewise: false);
            bool k75o = o75l == GoldenFlights.Model75TetherHash && l75o.Tether.Chain.NewtonSolves == 0;
            Console.WriteLine($"with the linear step, the straight 15° nozzle, the 4 mm lip and the old peel on model 70's drone.json: tether 0x{o75l:X16} "
                + $"({(k75o ? "model 75's, bit for bit" : "NOT model 75's")}), the line {(l75o.Tether.Broken ? $"broken at step {l75o.Tether.BrokenAt}" : $"whole, {l75o.Tether.PaidOut:F3} m paid out")}: "
                + $"{(k75o ? "PASS" : "FAIL")}");
            // The physics integration 2 with the climb's edgewise thrust and the fiber's Newton step (model 79, 76 merged onto 78), its
            // audits. Against model 78: without the edgewise flow's thrust and torque every golden is model 78's pin over the whole
            // record, every record and event (78 kept 75's pins but the tether's). Against model 76: with the known-wrong linear step
            // on model 70's drone.json with model 70's exit, lip and peel the tether golden is model 76's; every golden without the line
            // is 76's own pin, this model's. So the edgewise flow is the whole of 79's move against 78 and 78's line the whole against 76
            // (GoldenFlightTests.WithoutTheEdgewiseThrustEachGoldenIsModel78s, WithTheLinearStepAndTheOldExitEachGoldenIsModel76s).
            DroneSpec d79 = Drone();
            ulong w78p = GoldenFlights.FlyPowered(d79, out _, edgewise: false),
                w78r = GoldenFlights.FlyPowered(d79, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edgewise: false),
                w78c = GoldenFlights.FlyContact(d79, out _, edgewise: false), w78g = GoldenFlights.FlyRange(d79, out _, edgewise: false, wakes2: false),
                w78l = GoldenFlights.FlyContact(d79, out FlightCore l78w, tether: true, edgewise: false);
            bool k78w = w78p == GoldenFlights.Model75PoweredHash && w78r == GoldenFlights.Model75RainPoweredHash && w78c == GoldenFlights.Model75ContactHash
                && w78g == GoldenFlights.Model75RangeHash && w78l == GoldenFlights.Model78TetherHash;
            Console.WriteLine($"without the edgewise thrust: powered 0x{w78p:X16}, rain 0x{w78r:X16}, contact 0x{w78c:X16}, range 0x{w78g:X16}, tether 0x{w78l:X16} "
                + $"({(k78w ? "model 78's, bit for bit" : "NOT model 78's")}), {l78w.Tether.Chain.NewtonSolves} Newton solves: {(k78w ? "PASS" : "FAIL")}");
            ulong o76l = GoldenFlights.FlyContact(canister, out FlightCore l76o, tether: true, linearStep: true);
            bool k76o = o76l == GoldenFlights.Model76TetherHash && l76o.Tether.Chain.NewtonSolves == 0;
            Console.WriteLine($"with the linear step, the straight 15° nozzle, the 4 mm lip and the old peel on model 70's drone.json, the edgewise thrust on: tether 0x{o76l:X16} "
                + $"({(k76o ? "model 76's, bit for bit" : "NOT model 76's")}), the line {(l76o.Tether.Broken ? $"broken at step {l76o.Tether.BrokenAt}" : $"whole, {l76o.Tether.PaidOut:F3} m paid out")}: "
                + $"{(k76o ? "PASS" : "FAIL")}");
            return ok && cok && rangeOk && rok && tok && m22 && mok && eok && aok && uok && pok && k28 && lok && y34 && sok26 && zok && k38 && k35
                && m26 && m37 && k39 && k44 && k41 && k46 && k59 && k54 && k60 && k58 && k65 && k66 && k68 && k69 && k70 && k72 && k73 && k75 && k70e && k77n && k75o
                && k78w && k76o;
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
