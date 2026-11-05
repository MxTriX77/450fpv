using System;
using System.Linq;
using System.Threading.Tasks;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // p7f-damage 1.3, `--damage [seeds]`: the acceptance harness's first cells (design 14; 1.7 completes them). SimPilot flies the
    // drone along H01_D's south wall (masonry) at 1, 2 and 4 m/s with the lead prop's rim asked 1, 3 and 5 mm into it for
    // 0.3 s from its first strike, backs off and hovers 10 s (StrikeScenarios.Graze), on each seed's draws (the motors' asymmetry,
    // the factory pitch and unbalance, the blades' strength, each rotor's own wake; the wall does not change), and again with the
    // strikes off (ContactSpec.BladeStrikes, the same draws). Each cell prints its medians and spreads: the lead rotor's hover
    // command and the hover's total current, damaged and not; the camera's 50–200 Hz band; the lead prop's blades' lengths and
    // their difference; passes, damage events, jams and crashes. 1.7's claims ("Worse after a scratch": on every seed the
    // command and the current higher, the band past the factory's 99.5th percentile on 95 % of the 3 and 5 mm cells) are
    // printed as rates here, not asserted; the run fails only on a tunnel (a physics bug) or a cell with no strike. A crash is
    // printed with its cause: the scripted pilot holds the drone to the wall, so a deep press can end a flight. The range's
    // ground is surfaces.json's soil (since model 43). Then the pilot's landings tipped over onto the dirt track and S04's soil,
    // Sunny and in Rain, the throttle cut or disarmed at the touch, with surfaces.json, with model 43's carve (ContactSpec.TipSlide
    // off: the slide p7f-damage 1.3 found) and without surfaces.json (the Glance): their blades' passes, carves, stones, jams and
    // damage; a landing must stop (StrikeScenarios.TipStops). Then props at rest (p7f-damage 1.3b): the drone set down on one
    // prop's blade, and D-082's case on R01_A's roof rack, disarmed and then armed at idle (RestScenarios).
    internal static class DamageCase
    {
        public static bool Run(int seeds)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double[] speeds = { 1, 2, 4 }, depths = { 0.001, 0.003, 0.005 };
            bool ok = true;
            Console.WriteLine($"  {speeds.Length * depths.Length} cells of {seeds} seeds, damage on and off; the factory band's 99.5th percentile "
                + $"{DamageScenario.FactoryBandHigh:F4} m/s² (DamageScenario.FactoryBandHigh)");
            foreach (double v in speeds)
            {
                foreach (double d in depths)
                {
                    var on = new StrikeScenarios.GrazeResult[seeds];
                    var off = new StrikeScenarios.GrazeResult[seeds];
                    Parallel.For(0, seeds, n =>
                    {
                        ulong seed = (ulong)(n + 1);
                        on[n] = StrikeScenarios.Graze(spec, v, d, 0.3, 10, seed, true, StrikeScenarios.Airframe(spec));
                        off[n] = StrikeScenarios.Graze(spec, v, d, 0.3, 10, seed, false, StrikeScenarios.Airframe(spec));
                    });
                    int higherCommand = 0, higherAmps = 0, pastBand = 0, crashes = 0, tunnels = 0, untouched = 0;
                    for (int n = 0; n < seeds; n++)
                    {
                        if (on[n].HoverCommand > off[n].HoverCommand) higherCommand++;
                        if (on[n].HoverAmps > off[n].HoverAmps) higherAmps++;
                        if (on[n].Band > DamageScenario.FactoryBandHigh) pastBand++;
                        crashes += on[n].Crashes + off[n].Crashes;
                        tunnels += on[n].Tunnels + off[n].Tunnels;
                        if (on[n].Passes == 0) untouched++;
                    }
                    ok &= tunnels == 0 && untouched < seeds;
                    Console.WriteLine($"  {v} m/s, {d * 1e3} mm: passes {Median(on, r => r.Passes):F0}, damage events {Median(on, r => r.Damaged):F0}, jams "
                        + $"{on.Sum(r => r.Jams)}, crashes {crashes}, tunnels {tunnels}, untouched {untouched}");
                    for (int n = 0; n < seeds; n++)
                        if (on[n].Crashes + on[n].Tunnels > 0) Console.WriteLine($"    seed {n + 1}: " + on[n].Describe());
                    Console.WriteLine($"    fl's blades {Median(on, r => r.TipA * 127):F2} / {Median(on, r => r.TipB * 127):F2} mm (spread {Spread(on, r => r.TipA * 127)}), "
                        + $"their difference {Median(on, r => Math.Abs(r.TipA - r.TipB) * 127):F2} mm (max {on.Max(r => Math.Abs(r.TipA - r.TipB) * 127):F2})");
                    Console.WriteLine($"    fl's hover command {Median(on, r => r.HoverCommand):F4} against {Median(off, r => r.HoverCommand):F4} undamaged, higher on "
                        + $"{higherCommand} of {seeds}; the current {Median(on, r => r.HoverAmps):F2} A against {Median(off, r => r.HoverAmps):F2}, higher on {higherAmps}");
                    Console.WriteLine($"    the camera's band {Median(on, r => r.Band):F4} m/s² against {Median(off, r => r.Band):F4} (spread {Spread(on, r => r.Band)}), "
                        + $"past the factory's 99.5th percentile on {pastBand} of {seeds}");
                }
            }
            Console.WriteLine($"  {2 * seeds * speeds.Length * depths.Length} flights in {clock.Elapsed.TotalSeconds:F0} s: {(ok ? "PASS" : "FAIL")} "
                + "(no tunnel, every cell struck; 1.7 asserts the account)");

            // The pilot's landing tipped over (from a hover, 0.5 m/s down and 2.5 m/s across the skids, the throttle cut or the drone
            // disarmed at the touch) onto the dirt track and S04's soil, Sunny and in Rain, with surfaces.json and without it (the
            // known-wrong Glance); it fails on a tunnel, or where the soil's flight carves nothing or never stops a prop. Since model
            // 48 the legs fly rigid across (StrikeScenarios.TippedLanding) and a disarmed drone's ESCs brake (drone.json's BLHeli_S):
            // its props are stopped before they reach the soil, so a disarmed landing must tip, stop and break no blade, and the
            // ESCs freewheeling (model 38's coast, the known-wrong) are printed beside it.
            Console.WriteLine("  tipped landings (the props in the soil; p7f-damage 1.3's soil carve):");
            bool tipped = true;
            foreach (bool disarm in new[] { false, true })
            {
                foreach (string site in new[] { "the dirt track", "S04's soil" })
                {
                    foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain })
                    {
                        StrikeScenarios.TipResult r = StrikeScenarios.TippedLanding(spec, World(site, out Double3 at), at, rain, SoilScenarios.Table(), disarm);
                        StrikeScenarios.TipResult wrong = StrikeScenarios.TippedLanding(spec, World(site, out at), at, rain, null, disarm);
                        StrikeScenarios.TipResult slid = StrikeScenarios.TippedLanding(spec, World(site, out at), at, rain, SoilScenarios.Table(), disarm, tipSlide: false);
                        tipped &= r.Tunnels == 0 && StrikeScenarios.TipStops(r) && r.MaxTilt > 45
                            && (disarm ? r.Broken == 0 : r.InSoil > 0 && r.Jams + r.Broken > 0); // since model 64 a prop chewed to a stump on the stones stops it too
                        Console.WriteLine($"    {site}, {rain}, {(disarm ? "disarmed" : "the throttle cut")}: " + r.Describe());
                        Console.WriteLine("      model 43's carve (ContactSpec.TipSlide off): " + slid.Describe());
                        Console.WriteLine("      without surfaces.json: " + wrong.Describe());
                        if (!disarm) continue;
                        StrikeScenarios.TipResult free = StrikeScenarios.TippedLanding(StrikeScenarios.Freewheeling(PoweredFlightCase.Drone()), World(site, out at), at, rain,
                            SoilScenarios.Table(), disarm);
                        Console.WriteLine("      the ESCs freewheeling (EscDisarm.Freewheel): " + free.Describe());
                    }
                }
            }
            Console.WriteLine($"  tipped landings: {(tipped ? "PASS" : "FAIL")} (each tips and stops on the ground, StrikeScenarios.TipStops, with no tunnel; armed, it "
                + "carves and stops a prop; disarmed, its braked props break no blade)");
            bool rest = Rest(spec);
            return ok && tipped & rest & Bells(spec) & Shafts(spec) & Heat(spec);
        }

        // p7f-damage 1.6b (design 17, D-083): the motor's heat and dirt, HeatTests' scenarios printed. A jammed rotor at full throttle
        // under a 40 A limit against the two-node closed form, under the class's ESC, and without heat (the known-wrong); the same
        // current turning and stalled; a turning bell scraping the dirt track; and the pilot's case at three throttles, each motor's
        // failure time against its load (the known-wrong a fixed timer). It fails where a scenario's claim fails or on a tunnel.
        static bool Heat(DroneSpec spec)
        {
            Console.WriteLine("  the motor's heat and dirt (p7f-damage 1.6b, D-083):");
            HeatScenarios.Stall limited = HeatScenarios.Jammed(spec, 40, seconds: 300), free = HeatScenarios.Jammed(spec, double.PositiveInfinity, seconds: 5),
                cold = HeatScenarios.Jammed(spec, double.PositiveInfinity, heat: false, seconds: 5);
            foreach (HeatScenarios.Stall s in new[] { limited, free, cold }) Console.WriteLine("    " + s.Describe());
            bool stall = limited.ClosedFormError < 0.01 && !double.IsNaN(free.FailAt) && free.FailAt > free.ShortAt && free.FailAt < limited.FailAt && double.IsNaN(cold.FailAt);
            Console.WriteLine($"    a stalled motor heats and fails: {(stall ? "PASS" : "FAIL")} (the closed form within {limited.ClosedFormError * 100:F3} %; failed at "
                + $"{free.FailAt:F3} s at full throttle, {limited.FailAt:F1} s under 40 A; the known-wrong never)");
            HeatScenarios.Cooling c = HeatScenarios.Cools(spec);
            Console.WriteLine("    " + c.Describe());
            bool cools = c.SteadySpinWinding < c.SteadyStallWinding && c.SteadySpinStator < c.SteadyStallStator;
            Console.WriteLine($"    a spinning motor cools by its own air: {(cools ? "PASS" : "FAIL")}");
            double hover = StrikeScenarios.HoverDuty(spec);
            HeatScenarios.Fouled f = HeatScenarios.Scraped(spec, hover, seconds: 5), clean = HeatScenarios.Scraped(spec, hover, heat: false, seconds: 5);
            Console.WriteLine("    " + f.Describe());
            Console.WriteLine("    " + clean.Describe());
            bool dirt = !double.IsNaN(f.PackedAt) && f.BoundAt >= f.PackedAt && clean.Bound == 0;
            Console.WriteLine($"    dirt in the gap drags, then binds: {(dirt ? "PASS" : "FAIL")}");
            double[] throttles = { 0.3, 0.45, 0.6 };
            var runs = throttles.Select(t => HeatScenarios.Upside(spec, t)).ToArray();
            foreach (HeatScenarios.Upturned u in runs) Console.WriteLine("    " + u.Describe());
            double First(HeatScenarios.Upturned u) => u.FailAt.Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.PositiveInfinity).Min();
            bool sooner = true;
            for (int i = 1; i < runs.Length; i++) sooner &= First(runs[i]) <= First(runs[i - 1]);
            double[] failed = runs[runs.Length - 1].FailAt.Where(x => !double.IsNaN(x)).ToArray();
            bool apart = failed.Length >= 2 && failed.Max() - failed.Min() > 0.1 && runs.All(u => u.Tunnels == 0);
            double timer = failed.Length > 0 ? failed.OrderBy(x => x).ElementAt(failed.Length / 2) : double.NaN;
            Console.WriteLine($"    upside down with throttle, the motors fail one after another: {(sooner && apart ? "PASS" : "FAIL")} (the first failure at "
                + string.Join(", ", runs.Select(u => $"{u.Throttle:F2}: {(double.IsInfinity(First(u)) ? "none" : First(u).ToString("F3") + " s")}"))
                + $"; the known-wrong fixed timer fails all four at {timer:F3} s, together)");
            return stall && cools && dirt && sooner && apart;
        }

        // p7f-damage 1.5 (design 8, 10): the motor's bell. Its rear-right bell pressed at 5 and 20 N on masonry under the prop, the
        // drone holding that rotor's thrust (the scrape: μ_k·F_n·R on the rotor, the current rising with the push), and side hits
        // at 1, 3 and 6 m/s with the rotor at hover speed on concrete at the stator's height (the stator stops the ring short of
        // its collapse load: no dent there, the magnets pressed onto the teeth while it lasts). It fails where the scrape is not
        // its closed form, the current does not rise with the push, a hit dents the bell at the stator's height, or on a strike or
        // a tunnel.
        static bool Bells(DroneSpec spec)
        {
            Console.WriteLine("  the motor's bell (p7f-damage 1.5):");
            BellScenarios.Press free = BellScenarios.Pressed(spec, 0), p5 = BellScenarios.Pressed(spec, 5), p20 = BellScenarios.Pressed(spec, 20);
            bool s = true;
            foreach (BellScenarios.Press p in new[] { free, p5, p20 })
            {
                Console.WriteLine("    " + p.Describe());
                s &= p.Strikes + p.Others == 0 && Math.Abs(p.Scrape - p.Expected) <= 1e-9 * p.Expected;
            }
            s &= p20.Amps - free.Amps > 2 * (p5.Amps - free.Amps) && p5.Amps > free.Amps;
            Console.WriteLine($"    a scraped bell draws more current: {(s ? "PASS" : "FAIL")} (rr +{p5.Amps - free.Amps:F3} A at 5 N, +{p20.Amps - free.Amps:F3} A at 20 N)");
            bool h = true;
            foreach (double v in new[] { 1.0, 3.0, 6.0 })
            {
                BellScenarios.Hit wall = BellScenarios.Struck(spec, v, false), rod = BellScenarios.Struck(spec, v, true);
                Console.WriteLine("    " + wall.Describe());
                Console.WriteLine("    " + rod.Describe());
                h &= wall.DentGap == 0 && wall.Strikes + wall.Tunnels + rod.Strikes + rod.Tunnels == 0;
            }
            Console.WriteLine($"    a side hit at the stator's height leaves the gap: {(h ? "PASS" : "FAIL")}");
            return s && h;
        }

        // p7f-damage 1.6 (design 9, 11): the shaft and the bearings, ShaftTests' scenarios printed. A bent shaft: rr's hub struck through
        // the load law at 0.8, 1.2 and 2 M_y, then the 10 s hover (the known-wrong a flat Kt loss); a brinelled bearing: the law at
        // 2·C0r; a lost blade at its root in the hover, the sticks centred (the known-wrong a scripted loss with no 1/rev); a root
        // break's BladeLost. It fails where a form misses, the bent shaft does not shake past the known-wrong or the events are not
        // logged or the lost blade's drone does not hold its attitude (its tilt at 1 s printed, under 45° through the second).
        static bool Shafts(DroneSpec spec)
        {
            Console.WriteLine("  the shaft and the bearings (p7f-damage 1.6):");
            ShaftScenarios.Bent straight = ShaftScenarios.Hover(spec, 1, 0), flat = ShaftScenarios.Hover(spec, 1, 0, flat: true);
            ShaftScenarios.Bent b08 = ShaftScenarios.Hover(spec, 1, 0.8), b12 = ShaftScenarios.Hover(spec, 1, 1.2), b20 = ShaftScenarios.Hover(spec, 1, 2.0);
            foreach (ShaftScenarios.Bent b in new[] { straight, flat, b08, b12, b20 }) Console.WriteLine("    " + b.Describe());
            bool bent = b08.Bend == 0 && b12.Bends == 1 && b20.Bends == 1 && b12.Band > 2 * straight.Band && b20.Bound
                && Math.Abs(flat.Band - straight.Band) < 0.1 * (b12.Band - straight.Band);
            Console.WriteLine($"    a bent shaft shakes: {(bent ? "PASS" : "FAIL")} (the band {straight.Band:F4} → {b12.Band:F4} m/s² at 1.2 M_y; the flat loss {flat.Band:F4}; "
                + "2 M_y's run-out binds the motor)");
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            MotorShaft s = core.Contacts.Shafts;
            MotorInternals m = spec.Internals[0];
            double l = s.Lever(0), span = s.Spacing(0), c0 = m.BearingStaticRating, force = 2 * c0 / (1 + l / span);
            var events = new StepEvents(16);
            ShaftScenarios.HubHit(core, 0, force * l / s.YieldMoment(0), events);
            double dent = 1e-4 * m.BallDiameter * 4;
            bool brinell = Math.Abs(s.DentUpper(0) - dent) <= 1e-9 * dent && s.PeakUpper(0) > c0;
            Console.WriteLine($"    a brinelled bearing: {(brinell ? "PASS" : "FAIL")} (2 C0r: the upper dent {s.DentUpper(0) * 1e9:F3} nm, the form {dent * 1e9:F3}; the lower "
                + $"{s.DentLower(0) * 1e9:F3} nm at {s.PeakLower(0):F1} N)");
            ShaftScenarios.Lost lost = ShaftScenarios.LoseBlade(spec, 1), wrong = ShaftScenarios.LoseBlade(spec, 1, wrong: true);
            Console.WriteLine("    " + lost.Describe());
            Console.WriteLine("    " + wrong.Describe());
            StrikeScenarios.Rig rod = ShaftScenarios.Rod(spec, 0.112, 0.008);
            bool blade = lost.Band > 5 * wrong.Band && rod.Count(EventKind.BladeLost) == 2 && double.IsNaN(lost.TiltAt);
            Console.WriteLine($"    a lost blade is flown: {(blade ? "PASS" : "FAIL")} (its band {lost.Band:F2} against the scripted loss's {wrong.Band:F4} m/s²; "
                + $"{rod.Count(EventKind.BladeLost)} BladeLost at a root break; the tilt at 1 s {lost.TiltAfterOne:F2}°, {(double.IsNaN(lost.TiltAt) ? "never 45°" : $"45° at {lost.TiltAt * 1e3:F0} ms")})");
            return bent && brinell && blade;
        }

        // p7f-damage 1.3b (design 16, D-082): props are parts at rest. The drone set down on one prop's blade (a steel block
        // under it), and D-082's case on R01_A's roof (rolled onto its left skid, the front-left blade on the rack's side rail),
        // disarmed, then armed with the sticks centred; each against its known-wrong, the prop passing through at rest (model
        // 50's). It fails where a resting blade bears nothing, the known-wrong does not settle lower, the armed idle rotor does not
        // strike the rack, chip its blade and move the drone, or on a tunnel or a crash.
        static bool Rest(DroneSpec spec)
        {
            Console.WriteLine("  props at rest (p7f-damage 1.3b, D-082):");
            RestScenarios.Result block = RestScenarios.Block(spec), through = RestScenarios.Block(spec, rest: false);
            // Since model 60 judged over the set-downs (RestScenarios.BlockDrops) and the rack's seeds (RackSeeds), as RestTests:
            // where the drone comes to rest on a blade turns on how it lands, within a millimetre (RestScenarios' notes).
            Console.WriteLine("    set down on one prop: " + block.Describe());
            Console.WriteLine("      the prop passing through (model 50): " + through.Describe());
            RestScenarios.BlockSweep bw = RestScenarios.Blocks(spec);
            bool b = bw.Bears == bw.N && bw.WrongOnItsSkid == bw.N && bw.Over >= bw.Holds && bw.Holds > 0; // since model 64 over the known-wrong wherever the blade holds the corner
            Console.WriteLine($"    a resting prop bears load: {(b ? "PASS" : "FAIL")} (over {bw.N} set-downs the blade bears the drone on {bw.Bears}, holds its corner "
                + $"off the floor on {bw.Holds}, {bw.LeastOver * 1e3:F2}–{bw.MostOver * 1e3:F2} mm over the known-wrong's, which rests on that skid on {bw.WrongOnItsSkid}; "
                + $"this set-down {(block.HubHeight - through.HubHeight) * 1e3:F1} mm)");
            RestScenarios.RackPose(spec, out Double3 p, out _, out double roll);
            RestScenarios.Result rack = RestScenarios.Rack(spec), wrong = RestScenarios.Rack(spec, rest: false, arm: false);
            RestScenarios.RackSweep rw = RestScenarios.Racks(spec);
            int half = rw.N / 2;
            // Since model 64 the canister bears the rolled drone too: the blade over 1 N on some seeds, not most (RestTests).
            bool r = rw.Rests > 0 && rw.Strikes > half && rw.Chips > half && rw.Moves > half && rw.Over > half && rw.Notches > 0 && rw.OnTheRail > 0
                && rw.WrongBearsNothing == rw.N && rw.Tunnels == 0;
            Console.WriteLine($"    on R01_A's roof rack (set down rolled {roll * 57.29577951308232:F1}° at ({p.X:F3}, {p.Y:F3}, {p.Z:F3}), 3 s disarmed, then 3 s armed at idle): "
                + rack.Describe());
            Console.WriteLine("      the prop passing through (model 50), disarmed: " + wrong.Describe());
            Console.WriteLine($"    arming with a prop on the rack moves the drone: {(r ? "PASS" : "FAIL")} (over {rw.N} seeds: rests on the blade {rw.Rests}, "
                + $"strikes {rw.Strikes}, chips {rw.Chips}, notches {rw.Notches}, the rail first {rw.OnTheRail}, moves {rw.Moves}, over the known-wrong {rw.Over}, "
                + $"the known-wrong bears nothing {rw.WrongBearsNothing}, {rw.Crashes} crashes (driven off the roof), {rw.Tunnels} tunnels)");
            // Information: a blade that carries a real share of the drone holds an idle motor by its friction (μ·N·r against the
            // idle's Kt·d·V/R less Kt·I0), so arming stalls it: the front-left blade's root pressed in the rail's crotch with the
            // motor can (RestScenarios' scan, 25 mm from the hub, 24° off the rail).
            RestScenarios.Result stall = RestScenarios.Rack(spec, yaw: -66, radius: 0.025, corner: RestScenarios.FrontLeft);
            Console.WriteLine("    (information) the front-left blade's root on the rail beside its motor can, bearing its share: " + stall.Describe());
            return b && r;
        }

        static FlightWorld World(string site, out Double3 at) =>
            site == "the dirt track" ? StrikeScenarios.TrackWorld(out at) : StrikeScenarios.BeltWorld(out at);

        static double Median(StrikeScenarios.GrazeResult[] r, Func<StrikeScenarios.GrazeResult, double> f)
        {
            double[] x = r.Select(f).OrderBy(v => v).ToArray();
            return x[x.Length / 2];
        }

        static string Spread(StrikeScenarios.GrazeResult[] r, Func<StrikeScenarios.GrazeResult, double> f)
        {
            double[] x = r.Select(f).OrderBy(v => v).ToArray();
            return $"{x[0]:F4}–{x[x.Length - 1]:F4}";
        }
    }
}
