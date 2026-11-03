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
        static DroneSpec M22() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(Drone()));

        public static DroneSpec Drone() => DroneSpec.Parse(DroneText());

        // The line's audits since model 40 (the physics integration 2 with p7f-damage): drone.json without props.blade.
        static DroneSpec Bare() => GoldenFlights.WithoutTheBlade(Drone());

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
            // p7a-ground 1.10's audit, restated at the ground merge (model 27) and the physics integration 2 (model 34): without
            // surfaces.json the contact flight is p7d-tether's model 31 flight over the whole record, and the physics line's model
            // 26 flight on the rigid floor over model 28's 1904 bytes.
            const int m28 = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block
            const int m38 = GoldenFlights.Model38Layout; // since model 40 the blade strikes follow it; the line's audits fly without props.blade
            const int m29 = GoldenFlights.Model29Layout; // p7f-damage's model 29 record: the line's model 28 record, 1904 bytes
            ulong rigid = GoldenFlights.FlyContact(Bare(), out _, soil: false, yawI: false, layout: m38),
                rigid28 = GoldenFlights.FlyContact(Bare(), out _, soil: false, layout: m28, yawI: false);
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
            // p7a-ground 1.12's audit (model 33), restated at the physics integration 2 (model 34): without the layers' area laws
            // the range flight is p7d-tether's model 31 flight over the whole record, and model 32's over model 28's 1904 bytes,
            // dry and in Rain.
            ulong r31 = GoldenFlights.FlyRange(Bare(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, layout: m38);
            ulong r32 = GoldenFlights.FlyRange(Bare(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: m28, yawI: false);
            ulong w32 = GoldenFlights.FlyRange(Bare(), out _, rain: RainLevel.Rain, table: Fpv450.Sim.Tests.SoilScenarios.Table(), materials: GoldenFlights.WithoutTheAreaLaws(),
                layout: m28, yawI: false);
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
            ulong p22 = GoldenFlights.FlyPowered(M22(), out _, layout: m28, yawI: false), c22 = GoldenFlights.FlyContact(M22(), out _, soil: false, layout: m28, yawI: false),
                g22 = GoldenFlights.FlyRange(M22(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: m28, yawI: false);
            ulong w22 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, yawI: false);
            ulong t22 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m28);
            bool m22 = p22 == GoldenFlights.Model25PoweredHash && c22 == GoldenFlights.Model25ContactHash && g22 == GoldenFlights.Model25RangeHash
                && w22 == GoldenFlights.Model25RainPoweredHash && t22 == GoldenFlights.Model25TumbleHash;
            Console.WriteLine($"model 22's drone (the 40 A limit, 1.0 A no-load, 0.92 kg packs, 0.25 kg electronics): powered 0x{p22:X16}, contact 0x{c22:X16}, "
                + $"range 0x{g22:X16}, rain 0x{w22:X16}, tumble 0x{t22:X16} ({(m22 ? "model 25's, bit for bit" : "NOT model 25's")}): {(m22 ? "PASS" : "FAIL")}");
            DroneSpec main = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(DroneText(), GoldenFlights.TunedHForce)));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int m19 = GoldenFlights.Model19Layout;
            ulong p15 = GoldenFlights.FlyPowered(main, out _, 1296, 152, wind: still, interference: false, wake: false, motors: false, layout: m19, yawI: false);
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
            ulong p19 = GoldenFlights.FlyPowered(M22(), out _, interference: false, wake: false, motors: false, layout: m19, yawI: false);
            ulong r19 = GoldenFlights.FlyPowered(M22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19, yawI: false);
            ulong t19 = GoldenFlights.FlyTumble(M22(), out _, out _, layout: m19);
            bool aok = p19 == GoldenFlights.Model19PoweredHash && r19 == GoldenFlights.Model19RainHash && t19 == GoldenFlights.Model19TumbleHash;
            Console.WriteLine($"without the interference and D-064's sources, over model 19's 1808 bytes: powered 0x{p19:X16}, rain 0x{r19:X16}, "
                + $"tumble 0x{t19:X16} ({(aok ? "model 19's, bit for bit" : "NOT model 19's")}): {(aok ? "PASS" : "FAIL")}");
            // The integration's audit against p7b-airframe--micro (model 23): without the interference, over that branch's 1840
            // bytes (the record less the interference's 64 at 1808), each golden is its pin; since model 25 the contact flight's
            // is with 1.6c's stick spring (GoldenFlights.MicroContactHash).
            const int at = GoldenFlights.Model19Layout, skip = 64;
            ulong mp = GoldenFlights.FlyPowered(M22(), out _, at, skip, interference: false, layout: m28, yawI: false);
            ulong mc = GoldenFlights.FlyContact(M22(), out _, at, skip, interference: false, soil: false, layout: m28, yawI: false);
            ulong mr = GoldenFlights.FlyPowered(M22(), out _, at, skip, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, layout: m28,
                yawI: false);
            ulong mt = GoldenFlights.FlyTumble(M22(), out _, out _, at, skip, m28);
            bool uok = mp == GoldenFlights.MicroPoweredHash && mc == GoldenFlights.MicroContactHash && mr == GoldenFlights.MicroRainHash
                && mt == GoldenFlights.MicroTumbleHash;
            Console.WriteLine($"without the interference, over the micro branch's 1840 bytes: powered 0x{mp:X16}, contact 0x{mc:X16}, rain 0x{mr:X16}, "
                + $"tumble 0x{mt:X16} ({(uok ? "its model 23's, bit for bit" : "NOT its model 23's")}): {(uok ? "PASS" : "FAIL")}");
            // The merge's audit against p7b-airframe--power (its model 24): on drone.json without the interference and D-064's
            // sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are its pins.
            DroneSpec bare = GoldenFlights.WithoutTheBlade(Drone());
            ulong pp = GoldenFlights.FlyPowered(bare, out _, interference: false, wake: false, motors: false, layout: m19, yawI: false);
            ulong pr = GoldenFlights.FlyPowered(bare, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: m19, yawI: false);
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
            ulong a28 = GoldenFlights.FlyPowered(Bare(), out _, layout: m28, onRecord: Tail, yawI: false);
            ulong c28 = GoldenFlights.FlyContact(Bare(), out _, layout: m28, onRecord: Tail, soil: false, yawI: false);
            ulong g28 = GoldenFlights.FlyRange(Bare(), out _, layout: m28, onRecord: Tail, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false);
            ulong c33 = GoldenFlights.FlyContact(Bare(), out _, layout: m28, onRecord: Tail, yawI: false);
            ulong g33 = GoldenFlights.FlyRange(Bare(), out _, layout: m28, onRecord: Tail, yawI: false);
            ulong r28 = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m28, onRecord: Tail, yawI: false);
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
                + $"{l.Tether.Chain.Wedges} wedges, ends {l.State.Position.Y:F4} m up: {(lok ? "PASS" : "FAIL")}");
            // The physics integration 2's audit against p7d-tether (model 34): on the rigid floor the line's flight is model 31's.
            ulong line31 = GoldenFlights.FlyContact(Bare(), out _, tether: true, soil: false, yawI: false, layout: m38);
            bool l31 = line31 == GoldenFlights.Model31TetherHash;
            lok &= l31;
            Console.WriteLine($"tether-flight without surfaces.json: 0x{line31:X16} ({(l31 ? "model 31's, bit for bit" : "NOT model 31's")}): {(l31 ? "PASS" : "FAIL")}");
            // Betaflight's yaw I's audit (model 36): on model 34's yaw I (i_yaw just under 12, its Ki model 34's bit for bit)
            // every golden is model 34's pin, every record and event, so the yaw Ki is the whole move. Every audit above against
            // an earlier model flies it too; the tumble has no rotors and keeps its hash.
            ulong y34p = GoldenFlights.FlyPowered(Bare(), out _, yawI: false, layout: m38), y34c = GoldenFlights.FlyContact(Bare(), out _, yawI: false, layout: m38),
                y34g = GoldenFlights.FlyRange(Bare(), out _, yawI: false, layout: m38),
                y34r = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false, layout: m38),
                y34l = GoldenFlights.FlyContact(Bare(), out _, tether: true, yawI: false, layout: m38);
            bool y34 = y34p == GoldenFlights.Model34PoweredHash && y34c == GoldenFlights.Model34ContactHash && y34g == GoldenFlights.Model34RangeOn38Hash
                && y34r == GoldenFlights.Model34RainPoweredHash && y34l == GoldenFlights.Model34TetherHash;
            Console.WriteLine($"on model 34's yaw I (i_yaw {GoldenFlights.Model34YawI:R}): powered 0x{y34p:X16}, contact 0x{y34c:X16}, range 0x{y34g:X16}, "
                + $"rain 0x{y34r:X16}, tether 0x{y34l:X16} ({(y34 ? "model 34's, bit for bit; the range with model 38's buried face" : "NOT model 34's")}): {(y34 ? "PASS" : "FAIL")}");
            // p7f-damage 1.2's audit (model 29), kept: with the blades' pitch draws off and the flapping's inertia at the rod, every
            // golden is model 26's (since model 40 without the line's mechanisms: no soil, no area laws, model 34's yaw I); each switch alone gives the share of the move it makes, printed (GoldenFlights' notes).
            DroneSpec d = Drone();
            Action<FlightSetup> rod = GoldenFlights.WithTheRodsInertia(d), nopitch = GoldenFlights.WithoutThePitch, both = s => { nopitch(s); rod(s); };
            MaterialRow[] layerless = GoldenFlights.WithoutTheAreaLaws();
            ulong a26 = GoldenFlights.FlyPowered(d, out _, edit: both, layout: m29, yawI: false),
                b26 = GoldenFlights.FlyContact(d, out _, edit: both, layout: m29, soil: false, yawI: false),
                c26 = GoldenFlights.FlyRange(d, out _, edit: both, layout: m29, materials: layerless, yawI: false);
            ulong r26 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: both, layout: m29, yawI: false);
            bool sok26 = a26 == GoldenFlights.Model26PoweredHash && b26 == GoldenFlights.Model26ContactHash && c26 == GoldenFlights.Model26RangeHash
                && r26 == GoldenFlights.Model26RainPoweredHash;
            Console.WriteLine($"the blades' pitch off and the rod's inertia: powered 0x{a26:X16}, contact 0x{b26:X16}, range 0x{c26:X16}, rain 0x{r26:X16} "
                + $"({(sok26 ? "model 26's, bit for bit" : "NOT model 26's")}): {(sok26 ? "PASS" : "FAIL")}");
            ulong ip = GoldenFlights.FlyPowered(d, out _, edit: nopitch, layout: m29, yawI: false),
                ic = GoldenFlights.FlyContact(d, out _, edit: nopitch, layout: m29, soil: false, yawI: false),
                ig = GoldenFlights.FlyRange(d, out _, edit: nopitch, layout: m29, materials: layerless, yawI: false);
            ulong ir = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: nopitch, layout: m29, yawI: false);
            ulong qp = GoldenFlights.FlyPowered(d, out _, edit: rod, layout: m29, yawI: false),
                qc = GoldenFlights.FlyContact(d, out _, edit: rod, layout: m29, soil: false, yawI: false),
                qg = GoldenFlights.FlyRange(d, out _, edit: rod, layout: m29, materials: layerless, yawI: false);
            ulong qr = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: rod, layout: m29, yawI: false);
            Console.WriteLine($"  the flapping's blade inertia alone (pitch off): powered 0x{ip:X16}, contact 0x{ic:X16}, range 0x{ig:X16}, rain 0x{ir:X16}");
            Console.WriteLine($"  the blades' pitch alone (the rod's inertia): powered 0x{qp:X16}, contact 0x{qc:X16}, range 0x{qg:X16}, rain 0x{qr:X16}");
            // p7f-damage 1.3's audit (model 35), kept: the goldens' discs cut nothing, so over model 29's 1904 bytes every golden is
            // model 29's pin, every record and event (since model 40 without the line's mechanisms); with the strikes off
            // (ContactSpec.BladeStrikes) the whole record is the pins'.
            ulong p29 = GoldenFlights.FlyPowered(d, out FlightCore pc, layout: m29, yawI: false),
                c29 = GoldenFlights.FlyContact(d, out FlightCore cc, layout: m29, soil: false, yawI: false);
            ulong g29 = GoldenFlights.FlyRange(d, out FlightCore gc, layout: m29, materials: layerless, yawI: false),
                t29 = GoldenFlights.FlyTumble(d, out _, out _, layout: m29);
            ulong r29 = GoldenFlights.FlyPowered(d, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m29, yawI: false);
            Action<FlightSetup> off = s => { if (s.Contacts != null) s.Contacts.BladeStrikes = false; };
            ulong coff = GoldenFlights.FlyContact(d, out _, edit: off), goff = GoldenFlights.FlyRange(d, out _, edit: off),
                loff = GoldenFlights.FlyContact(d, out _, tether: true, edit: off);
            bool zok = p29 == GoldenFlights.Model29PoweredHash && c29 == GoldenFlights.Model29ContactHash && g29 == GoldenFlights.Model29RangeHash
                && t29 == GoldenFlights.Model29TumbleHash && r29 == GoldenFlights.Model29RainPoweredHash && coff == GoldenFlights.ContactHash && goff == GoldenFlights.RangeHash
                && loff == GoldenFlights.TetherHash
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
                for (int i = m38; i < Fpv450.Sim.Log.LogFormat.StepSize; i++) if (record[i] != 0) strikesTail++;
            }
            void Fiber(byte[] record)
            {
                for (int i = m28; i < m38; i++) if (record[i] != 0) fiber++;
            }
            ulong b38p = GoldenFlights.FlyPowered(Bare(), out _, layout: m38, onRecord: Strikes),
                b38r = GoldenFlights.FlyPowered(Bare(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m38, onRecord: Strikes),
                b38c = GoldenFlights.FlyContact(Bare(), out _, layout: m38, onRecord: Strikes), b38g = GoldenFlights.FlyRange(Bare(), out _, layout: m38, onRecord: Strikes),
                b38l = GoldenFlights.FlyContact(Bare(), out _, tether: true, layout: m38, onRecord: Strikes),
                b38t = GoldenFlights.FlyTumble(Bare(), out _, out _, layout: m38);
            bool k38 = b38p == GoldenFlights.Model38PoweredHash && b38r == GoldenFlights.Model38RainPoweredHash && b38c == GoldenFlights.Model38ContactHash
                && b38g == GoldenFlights.Model38RangeHash && b38l == GoldenFlights.Model38TetherHash && b38t == GoldenFlights.Model38TumbleHash && strikesTail == 0;
            Console.WriteLine($"without props.blade, over the line's 2008 bytes: powered 0x{b38p:X16}, rain 0x{b38r:X16}, contact 0x{b38c:X16}, range 0x{b38g:X16}, "
                + $"tether 0x{b38l:X16}, tumble 0x{b38t:X16} ({(k38 ? "the line's model 38, bit for bit" : "NOT model 38's")}), {strikesTail} non-zero bytes in the "
                + $"strikes' block: {(k38 ? "PASS" : "FAIL")}");
            const int fiberBytes = GoldenFlights.Model35Skip;
            ulong l35p = GoldenFlights.FlyPowered(d, out _, m28, fiberBytes, onRecord: Fiber, yawI: false),
                l35r = GoldenFlights.FlyPowered(d, out _, m28, fiberBytes, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Fiber, yawI: false),
                l35c = GoldenFlights.FlyContact(d, out _, m28, fiberBytes, onRecord: Fiber, soil: false, yawI: false),
                l35g = GoldenFlights.FlyRange(d, out _, m28, fiberBytes, onRecord: Fiber, materials: layerless, yawI: false),
                l35t = GoldenFlights.FlyTumble(d, out _, out _, m28, fiberBytes);
            bool k35 = l35p == GoldenFlights.Model35PoweredHash && l35r == GoldenFlights.Model35RainPoweredHash && l35c == GoldenFlights.Model35ContactHash
                && l35g == GoldenFlights.Model35RangeHash && l35t == GoldenFlights.Model35TumbleHash && fiber == 0;
            Console.WriteLine($"without the line's mechanisms, over p7f-damage's record: powered 0x{l35p:X16}, rain 0x{l35r:X16}, contact 0x{l35c:X16}, "
                + $"range 0x{l35g:X16}, tumble 0x{l35t:X16} ({(k35 ? "p7f-damage's model 35, bit for bit" : "NOT model 35's")}), {fiber} non-zero bytes in the fiber's "
                + $"block: {(k35 ? "PASS" : "FAIL")}");
            return ok && cok && rangeOk && rok && tok && m22 && mok && eok && aok && uok && pok && k28 && lok && y34 && sok26 && zok && k38 && k35;
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
