using System;
using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The aero and wind scenarios (p6-flight-core 1.5), from the same files the suite compiles (CrosswindScenario,
    // WindTargets, SimPilot): the crosswind lean under the closed-loop pilot, the cruise trim, and the stand-in wind's
    // targets, each statistical target also run on a known-wrong model that must fail it. PASS needs every target met
    // and every known-wrong model failed.
    internal static class ScenariosCase
    {
        static bool _ok = true;

        static void Row(string name, bool pass, string detail, bool knownWrong = false)
        {
            bool good = knownWrong ? !pass : pass;
            _ok &= good;
            string verdict = knownWrong ? (pass ? "PASSES: the target cannot tell it from the model (FAIL)" : "fails, as it must") : pass ? "PASS" : "FAIL";
            Console.WriteLine($"  {name}: {detail}: {verdict}");
        }

        static string Lean(CrosswindScenario.Result r) =>
            $"lean {r.LeanDeg:F2}° = {r.LeanPerMs:F3}° per m/s of {r.WindSpeed:F2} m/s, {r.LeanOffWindDeg:F1}° off the wind's bearing, "
            + $"position error {r.MeanError:F3} m mean / {r.MaxError:F3} m max, drift {r.Drift:F4} m/s";

        static bool LeanTarget(CrosswindScenario.Result r) => r.LeanPerMs >= 0.95 && r.LeanPerMs <= 2.6 && r.LeanOffWindDeg < 5;

        public static bool Run()
        {
            DroneSpec spec = PoweredFlightCase.Drone();

            Console.WriteLine("crosswind lean (flight-dynamics, K§6.5: 0.95–2.6° per m/s, nominal 1.8, into the wind), the drone holding its point "
                + $"under the closed-loop pilot, the last {CrosswindScenario.MeasuredSeconds:F0} s of {CrosswindScenario.Seconds:F0}:");
            foreach (double from in new[] { 270.0, 0, 90, 180, 45, 135 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(from, 5)));
                Row($"steady 5 m/s from {from:F0}°", LeanTarget(r) && r.Drift < 0.05, Lean(r));
            }
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Severe), seed));
                Row($"Severe stand-in, seed {seed}", r.LeanPerMs >= 0.95 && r.LeanPerMs <= 2.6 && r.LeanOffWindDeg < 10, Lean(r));
            }
            CrosswindScenario.Result none = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(270, 5), drag: false, rotorDrag: false));
            Row("known-wrong: no drag", LeanTarget(none), Lean(none), knownWrong: true);
            CrosswindScenario.Result body = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(270, 5), rotorDrag: false));
            Row("known-wrong: the body's drag alone, no rotor in-plane drag", LeanTarget(body), Lean(body), knownWrong: true);

            Console.Write("  cruise trim in still air, the pitch that holds an airspeed (clip P: 22.7° at wind.md §5.3's nominal 13 m/s; the published cruise 17–22 m/s):");
            foreach (double v in new[] { 13.0, 17, 22 }) Console.Write($" {v:F0} m/s {CrosswindScenario.CruisePitch(spec, v):F1}°;");
            Console.WriteLine();
            double pitch = CrosswindScenario.CruisePitch(spec, 13);
            double lean0 = CrosswindScenario.CruiseLean(spec, 13, 0), lean2 = CrosswindScenario.CruiseLean(spec, 13, 2);
            double perMs = (lean2 - lean0) / 2;
            Row("at cruise, 13 m/s north, a 2 m/s crosswind from the west, the nose on the track",
                pitch >= 22.4 && pitch <= 23.0 && perMs >= 0.95 && perMs <= 2.6,
                $"pitch {pitch:F2}° (P: 22.7°), {lean2 - lean0:F2}° more lean = {perMs:F3}° per m/s (wind.md §5.3: 1.8 nominal)");

            Console.WriteLine("stand-in wind (wind-field, design 8):");
            var calm = new List<WindTargets.History>();
            var still = new List<WindTargets.History>();
            var vector = new List<WindTargets.History>();
            for (ulong seed = 1; seed <= 5; seed++)
            {
                calm.Add(WindTargets.FlightHistory(WindLevel.Calm, seed, 120, 10));
                still.Add(WindTargets.Gusts(WindLevel.Calm, seed, 120, 10, WindTargets.Still));
                vector.Add(WindTargets.Gusts(WindLevel.Calm, seed, 120, 10, WindTargets.Fixed));
            }
            Row("Calm is never perfectly still, 5 flights of 120 s", WindTargets.NeverStill(calm, out string d), d);
            Row("Calm's mean in its band", WindTargets.MeanInBand(calm, WindPreset.Of(WindLevel.Calm), out d), d);
            Row("known-wrong: still air", WindTargets.NeverStill(still, out d), d, knownWrong: true);
            Row("known-wrong: a fixed vector", WindTargets.NeverStill(vector, out d), d, knownWrong: true);

            // The same seed, the same wind: two bench flights on one seed, flown differently.
            FlightSetup a = WindTargets.BenchFlight(WindLevel.Windy, 0x450F), b = WindTargets.BenchFlight(WindLevel.Windy, 0x450F);
            b.StartVelocity = new Double3(3, 1, -2);
            FlightCore ca = new FlightCore(a), cb = new FlightCore(b);
            int same = 0;
            for (int k = 0; k < 60000; k++)
            {
                ca.Advance(default);
                cb.Advance(default);
                if (ca.State.Gust.X == cb.State.Gust.X && ca.State.Gust.Y == cb.State.Gust.Y && ca.State.Gust.Z == cb.State.Gust.Z) same++;
            }
            Row("the same seed, the same wind", same == 60000, $"{same} of 60000 steps' gusts bit-identical on two differently flown flights");
            WindTargets.History u1 = WindTargets.Gusts(WindLevel.Windy, 0x450F, 5, 1, WindTargets.Unseeded);
            WindTargets.History u2 = WindTargets.Gusts(WindLevel.Windy, 0x450F, 5, 1, WindTargets.Unseeded);
            int sameWrong = 0;
            for (int i = 0; i < u1.Samples.Length; i++)
                if (u1.Samples[i].X == u2.Samples[i].X && u1.Samples[i].Z == u2.Samples[i].Z) sameWrong++;
            Row("known-wrong: an unseeded turbulence", sameWrong == u1.Samples.Length, $"{sameWrong} of {u1.Samples.Length} samples alike", knownWrong: true);

            var bearings = new double[400];
            for (int i = 0; i < bearings.Length; i++) bearings[i] = new StandInWind(WindSetup.Of(WindLevel.Windy), new FlightRandom((ulong)i + 1)).FromDeg;
            Row("a different seed, a different direction, uniform over the compass", WindTargets.Uniform(bearings, out d) && new HashSet<double>(bearings).Count == 400, d);
            var left = new double[400];
            for (int i = 0; i < left.Length; i++) left[i] = 225 + 90 * ((i * 0.618034) % 1);
            Row("known-wrong: always from the left", WindTargets.Uniform(left, out d), d, knownWrong: true);

            foreach (WindLevel level in new[] { WindLevel.Windy, WindLevel.Severe })
            {
                var runs = new List<WindTargets.History>();
                var white = new List<WindTargets.History>();
                var flat = new List<WindTargets.History>();
                var biased = new List<WindTargets.History>();
                for (ulong seed = 1; seed <= 20; seed++) runs.Add(WindTargets.Gusts(level, seed, 1200, 50));
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    white.Add(WindTargets.Gusts(level, seed, 300, 50, WindTargets.White));
                    flat.Add(WindTargets.Gusts(level, seed, 300, 50, WindTargets.FlatSigma));
                }
                foreach (WindTargets.History h in runs) biased.Add(WindTargets.Biased(h));
                Row($"{level}: σ/U, 20 seeds of 1200 s", WindTargets.Intensity(runs, out d, out _), d);
                Row($"{level}: the time constant L/U", WindTargets.TimeConstant(runs, out d), d);
                Row($"{level}: the mean in its band and unbiased", WindTargets.MeanInBand(runs, WindPreset.Of(level), out d), d);
                Row($"{level} known-wrong: white noise", WindTargets.TimeConstant(white, out d), d, knownWrong: true);
                Row($"{level} known-wrong: a flat 0.2 m/s σ", WindTargets.Intensity(flat, out d, out _), d, knownWrong: true);
                Row($"{level} known-wrong: a mean 5 % strong", WindTargets.MeanInBand(biased, WindPreset.Of(level), out d), d, knownWrong: true);
            }
            Console.WriteLine($"scenarios: {(_ok ? "PASS" : "FAIL")}");
            return _ok;
        }
    }
}
