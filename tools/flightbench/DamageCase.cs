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
    // Sunny and in Rain, the throttle cut or disarmed at the touch, with surfaces.json and without it (the Glance, the
    // known-wrong): their blades' passes, carves, jams and damage.
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
            // known-wrong Glance); it fails on a tunnel, or where the soil's flight carves nothing or never stops a prop.
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
                        tipped &= r.Tunnels == 0 && r.InSoil > 0 && r.Jams > 0;
                        Console.WriteLine($"    {site}, {rain}, {(disarm ? "disarmed" : "the throttle cut")}: " + r.Describe());
                        Console.WriteLine("      without surfaces.json: " + wrong.Describe());
                    }
                }
            }
            Console.WriteLine($"  tipped landings: {(tipped ? "PASS" : "FAIL")} (each carves and stops a prop, no tunnel)");
            return ok && tipped;
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
