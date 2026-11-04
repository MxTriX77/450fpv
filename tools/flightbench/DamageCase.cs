using System;
using System.Linq;
using System.Threading.Tasks;
using Fpv450.Sim;
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
                            && (disarm ? r.Broken == 0 : r.InSoil > 0 && r.Jams > 0);
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
            return ok && tipped & Rest(spec);
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
            bool b = block.BladeTouches && !block.SupportTouches && block.Load > 2 && through.Load == 0 && through.SupportTouches
                && block.HubHeight - through.HubHeight > 0.003 && block.Crashes + block.Tunnels == 0;
            Console.WriteLine("    set down on one prop: " + block.Describe());
            Console.WriteLine("      the prop passing through (model 50): " + through.Describe());
            Console.WriteLine($"    a resting prop bears load: {(b ? "PASS" : "FAIL")} (the blade holds its corner up, {(block.HubHeight - through.HubHeight) * 1e3:F1} mm "
                + "over the known-wrong's, which rests on that skid)");
            RestScenarios.RackPose(spec, out Double3 p, out _, out double roll);
            RestScenarios.Result rack = RestScenarios.Rack(spec), wrong = RestScenarios.Rack(spec, rest: false, arm: false);
            bool r = rack.Load > 1 && rack.Strikes > 0 && rack.FirstStrikeOn == "R01_A (steel)" && rack.Damaged > 0 && rack.Lost > 0 && rack.Travel > 0.001
                && rack.Crashes + rack.Tunnels == 0 && wrong.Load == 0 && rack.HubAtArming - wrong.HubHeight > 0.003 && rack.Notches > 0;
            Console.WriteLine($"    on R01_A's roof rack (set down rolled {roll * 57.29577951308232:F1}° at ({p.X:F3}, {p.Y:F3}, {p.Z:F3}), 3 s disarmed, then 3 s armed at idle): "
                + rack.Describe());
            Console.WriteLine("      the prop passing through (model 50), disarmed: " + wrong.Describe());
            Console.WriteLine($"    arming with a prop on the rack moves the drone: {(r ? "PASS" : "FAIL")}");
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
