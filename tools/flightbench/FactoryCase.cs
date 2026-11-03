using System;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // p7f-damage 1.2, prop-motor-damage's "The baseline shake is bounded away from imperceptible" (design 3, D-059): the
    // drone hovers 10 s in still air (DamageScenario) on each seed's draws, again with only the props' mass unbalance (the
    // blades' pitch draws off), with only the blades' pitch (the unbalance off) and with both factory draws off; the camera's
    // 50–200 Hz band's RMS over the hover's last 5 s.
    // - The scenario's claim as written: the band's 0.5th percentile over the seeds above the draws-off hovers' (seeds 1–5, the
    //   largest).
    // - Paired by seed (the same draws but the two factory ones, the same rotor-wake stream): the factory imbalance's own share
    //   of the band, P = band² − band_off², whose root is its 1/rev band; the mass and aerodynamic shares of a seed are its two
    //   single-source powers over their sum, at the median each at least a fifth.
    // The band's 99.5th percentile is the factory band other scenarios compare with (DamageScenario.FactoryBandHigh). Known-wrongs:
    // perfect props (both draws off) are the floor itself, so they fail "bounded away"; a mass-only imbalance (before 1.2) has
    // no aerodynamic share.
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
            double floor = 0, perfect = double.MaxValue;
            for (int n = 0; n < Math.Min(5, seeds); n++)
            {
                floor = Math.Max(floor, none[n]);
                perfect = Math.Min(perfect, none[n]);
            }
            var line = new double[seeds];
            var massShare = new double[seeds];
            var aeroShare = new double[seeds];
            int above = 0;
            for (int n = 0; n < seeds; n++)
            {
                double off = none[n] * none[n];
                line[n] = Math.Sqrt(Math.Max(both[n] * both[n] - off, 0));
                double pm = Math.Max(mass[n] * mass[n] - off, 0), pa = Math.Max(aero[n] * aero[n] - off, 0);
                massShare[n] = pm / (pm + pa);
                aeroShare[n] = pa / (pm + pa);
                if (both[n] > none[n]) above++;
            }
            double[] sorted = Sorted(both), lines = Sorted(line), offs = Sorted(none);
            Array.Sort(massShare);
            Array.Sort(aeroShare);
            int lo = (int)(0.005 * seeds), hi = seeds - 1 - lo, mid = seeds / 2;
            Console.WriteLine($"  {4 * seeds} hovers of {DamageScenario.Seconds:F0} s in {clock.Elapsed.TotalSeconds:F0} s; seeds {first}–{first + seeds - 1}");
            Console.WriteLine($"  the factory band (both draws), 0.5 / 50 / 99.5 %: {sorted[lo]:E3} / {sorted[mid]:E3} / {sorted[hi]:E3} m/s² (min {sorted[0]:E3}, max {sorted[seeds - 1]:E3})");
            Console.WriteLine($"  both factory draws off: 0.5 / 50 / 99.5 %: {offs[lo]:E3} / {offs[mid]:E3} / {offs[hi]:E3}; seeds {first}–{first + Math.Min(5, seeds) - 1}'s largest {floor:E3}");
            Console.WriteLine($"  the factory imbalance's own 1/rev band (paired, √(band² − band_off²)), 0.5 / 50 / 99.5 %: {lines[lo]:E3} / {lines[mid]:E3} / {lines[hi]:E3}; "
                + $"the band above its own draws-off hover on {above} of {seeds} seeds");
            Console.WriteLine($"  the shares at the median: mass {massShare[mid]:F3}, aerodynamic {aeroShare[mid]:F3} (each at least 0.2)");
            bool away = sorted[lo] > floor, shares = massShare[mid] >= 0.2 && aeroShare[mid] >= 0.2;
            bool wrongAway = perfect > floor, wrongShare = 0.0 >= 0.2;
            Console.WriteLine($"  bounded away from imperceptible (the band's 0.5th percentile above the draws-off hover's): {(away ? "PASS" : "FAIL")}; "
                + $"the known-wrong perfect props: {(wrongAway ? "PASSES" : "fails, as it must")}");
            if (!away)
                Console.WriteLine("  (the draws-off hovers' band is each rotor's own wake, D-064, at the arms' mode near 60 Hz, and it varies across the seeds "
                    + "more than the factory imbalance adds: the paired 1/rev band above is the imbalance's own)");
            Console.WriteLine($"  the mass and aerodynamic shares each at least a fifth at the median: {(shares ? "PASS" : "FAIL")}; the known-wrong mass-only "
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
