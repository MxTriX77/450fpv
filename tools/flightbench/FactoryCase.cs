using System;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // p7f-damage 1.2, prop-motor-damage's "The baseline shake is bounded away from imperceptible" (design 3, D-059): the
    // drone hovers 10 s in still air (DamageScenario) on each seed's draws, again with only the props' mass unbalance (the
    // blades' pitch draws off), with only the blades' pitch (the unbalance off) and with both factory draws off; the camera's
    // 50–200 Hz band's RMS over the hover's last 5 s, printed over the seeds with and without the draws.
    // - Paired by seed (the same draws but the two factory ones, the same rotor-wake stream; DamageScenario.BoundedAway, the
    //   suite's rule): the draws raise the band on at least 99.5 % of the seeds, and their own 1/rev band √(band² − band_off²)
    //   is above zero at its 0.5th percentile. The absolute band is not compared across seeds: each rotor's own wake (D-064)
    //   excites the arms' mode near 62 Hz and varies between seeds more than the draws add.
    // - The mass and aerodynamic shares of a seed are its two single-source powers over their sum, at the median each at
    //   least a fifth until model 68; since model 72 (the arms in their plane, ArmFlex) the arm's bend across takes most of
    //   the mass pull's lateral part off the body, so the aerodynamic share is at least a fifth and the mass share is printed.
    // The band's 99.5th percentile is the factory band other scenarios compare with (DamageScenario.FactoryBandHigh). Known-wrongs:
    // perfect props (both draws off) raise the band on no seed, so they fail "bounded away"; a mass-only imbalance (before 1.2)
    // has no aerodynamic share.
    internal static class FactoryCase
    {
        public static bool Run(int seeds, int first)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var both = new double[seeds];
            var mass = new double[seeds];
            var aero = new double[seeds];
            var none = new double[seeds];
            Parallel.For(0, seeds, n =>
            {
                ulong seed = (ulong)(first + n);
                both[n] = DamageScenario.Hover(DamageScenario.Setup(spec, seed)).Band;
                mass[n] = DamageScenario.Hover(DamageScenario.Setup(spec, seed, true, false)).Band;
                aero[n] = DamageScenario.Hover(DamageScenario.Setup(spec, seed, false, true)).Band;
                none[n] = DamageScenario.Hover(DamageScenario.Setup(spec, seed, false, false)).Band;
            });
            var massShare = new double[seeds];
            var aeroShare = new double[seeds];
            for (int n = 0; n < seeds; n++)
            {
                double off = none[n] * none[n];
                double pm = Math.Max(mass[n] * mass[n] - off, 0), pa = Math.Max(aero[n] * aero[n] - off, 0);
                massShare[n] = pm / (pm + pa);
                aeroShare[n] = pa / (pm + pa);
            }
            bool away = DamageScenario.BoundedAway(both, none, out int above, out double[] lines);
            bool wrongAway = DamageScenario.BoundedAway(none, none, out int wrongAbove, out _);
            double[] sorted = Sorted(both), offs = Sorted(none);
            Array.Sort(massShare);
            Array.Sort(aeroShare);
            int lo = (int)(0.005 * seeds), hi = seeds - 1 - lo, mid = seeds / 2;
            Console.WriteLine($"  {4 * seeds} hovers of {DamageScenario.Seconds:F0} s in {clock.Elapsed.TotalSeconds:F0} s; seeds {first}–{first + seeds - 1}");
            Console.WriteLine($"  the factory band (both draws), 0.5 / 50 / 99.5 %: {sorted[lo]:E3} / {sorted[mid]:E3} / {sorted[hi]:E3} m/s² (min {sorted[0]:E3}, max {sorted[seeds - 1]:E3})");
            Console.WriteLine($"  both factory draws off: 0.5 / 50 / 99.5 %: {offs[lo]:E3} / {offs[mid]:E3} / {offs[hi]:E3} (not compared across seeds: each rotor's own wake, D-064)");
            Console.WriteLine($"  the factory imbalance's own 1/rev band (paired, √(band² − band_off²)), 0.5 / 50 / 99.5 %: {lines[lo]:E3} / {lines[mid]:E3} / {lines[hi]:E3}; "
                + $"the band above its own draws-off hover on {above} of {seeds} seeds");
            Console.WriteLine($"  the shares at the median: mass {massShare[mid]:F3}, aerodynamic {aeroShare[mid]:F3} (the aerodynamic at least 0.2; the mass printed)");
            bool shares = massShare[mid] > 0 && aeroShare[mid] >= 0.2;
            bool wrongShare = 0.0 >= 0.2;
            Console.WriteLine($"  bounded away from imperceptible, paired by seed (raised on at least {Math.Ceiling(0.995 * seeds):F0} of {seeds} seeds, the own band's "
                + $"0.5th percentile {lines[lo]:E3} above zero): {(away ? "PASS" : "FAIL")}; the known-wrong perfect props raise it on {wrongAbove}: "
                + $"{(wrongAway ? "PASSES" : "fails, as it must")}");
            Console.WriteLine($"  the aerodynamic share at least a fifth at the median, the mass share above zero: {(shares ? "PASS" : "FAIL")}; the known-wrong mass-only "
                + $"imbalance's aerodynamic share 0: {(wrongShare ? "PASSES" : "fails, as it must")}");
            Console.WriteLine($"  the factory band's 99.5th percentile: {sorted[hi]:R} m/s² (DamageScenario.FactoryBandHigh: {DamageScenario.FactoryBandHigh:R})");
            return away && shares && !wrongAway && !wrongShare;
        }

        static double[] Sorted(double[] x)
        {
            var s = (double[])x.Clone();
            Array.Sort(s);
            return s;
        }
    }
}
