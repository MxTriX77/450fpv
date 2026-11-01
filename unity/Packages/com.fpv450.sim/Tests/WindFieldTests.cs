using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;
using static Fpv450.Sim.Tests.WindTargets;

namespace Fpv450.Sim.Tests
{
    // wind-turbulence (p7c-wind 1.4), the wind field in open air: "Calm is never perfectly still", "Same seed, same
    // wind" (at every rotor), and the level's preset as the targets design 3 gives it: the strength inside its band and
    // the mean unbiased, σ/U, the energy at 2–4 Hz at the encounter frequency, the direction uniform over the compass,
    // every draw named and bounded, the log-law profile, the direction's history from the seed. Each statistical target
    // is also run on a known-wrong model, which must fail it (K§8.6: a target no wrong model fails is no target). The
    // targets and the known-wrong models are WindTargets'.
    public sealed class WindFieldTests
    {
        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        // ---- the scenarios ----

        [Test]
        public void CalmIsNeverPerfectlyStill()
        {
            var runs = new List<History>();
            for (ulong seed = 1; seed <= 5; seed++) runs.Add(FlightHistory(WindLevel.Calm, seed, 120, 10));
            bool still = NeverStill(runs, out string detail);
            bool band = MeanInBand(runs, WindPreset.Of(WindLevel.Calm), out string bandDetail);
            TestContext.Out.WriteLine("Calm, 5 flights of 120 s: " + detail + "; " + bandDetail);
            Assert.That(still, Is.True, detail);
            Assert.That(band, Is.True, bandDetail);

            // The known-wrong: still air, and a fixed vector at the drawn mean.
            var stillAir = new List<History>();
            var fixedVector = new List<History>();
            for (ulong seed = 1; seed <= 5; seed++)
            {
                stillAir.Add(Gusts(WindLevel.Calm, seed, 120, 10, Still));
                fixedVector.Add(Gusts(WindLevel.Calm, seed, 120, 10, Fixed));
            }
            Assert.That(NeverStill(stillAir, out string d1), Is.False, "still air fails: " + d1);
            Assert.That(NeverStill(fixedVector, out string d2), Is.False, "a fixed vector fails: " + d2);
            TestContext.Out.WriteLine("known-wrong: still air: " + d1 + "; a fixed vector: " + d2);

            // The header names the model.
            FlightSetup calm = BenchFlight(WindLevel.Calm, 3);
            Log.LogHeader header = Log.LogHeader.For(calm, new FlightCore(calm));
            Assert.That(header.WindModel, Is.EqualTo("field"));
            Assert.That(header.Wind.Level, Is.EqualTo(WindLevel.Calm));
        }

        [Test]
        public void TheSameSeedTheSameWind()
        {
            // Two powered flights on one seed and level, flown alike: the wind at every rotor and at the body bit for bit;
            // a third flown otherwise goes elsewhere and meets other air there (the field is a function of place).
            DroneSpec spec = RepositoryDrone.Spec();
            FlightCore Fly(ulong seed) => new FlightCore(new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(spec.Nominal), Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(), Wind = WindSetup.Of(WindLevel.Windy), StartPosition = new Double3(0, 300, 0),
            });
            FlightCore a = Fly(0x450F), b = Fly(0x450F);
            FlightCore c = Fly(0x450F);
            for (int k = 0; k < 5000; k++)
            {
                var stick = new StickSample { HostUs = k * 1000L, Arm = k >= 20, Throttle = k > 40 ? 0.5 : 0 };
                a.Advance(stick);
                b.Advance(stick);
                c.Advance(new StickSample { HostUs = k * 1000L, Arm = k >= 20, Throttle = k > 40 ? 0.6 : 0, Roll = 0.3 });
                for (int i = 0; i < 4; i++) Assert.That(a.Trace.RotorWind[i], Is.EqualTo(b.Trace.RotorWind[i]), "step " + k + ", rotor " + i);
                Assert.That(a.Trace.Wind, Is.EqualTo(b.Trace.Wind), "step " + k);
            }
            Assert.That(a.State.Position, Is.Not.EqualTo(c.State.Position), "a flight flown otherwise goes elsewhere");
            Assert.That(a.Trace.RotorWind[0], Is.Not.EqualTo(c.Trace.RotorWind[0]), "and meets other air there");

            // A different seed, a different prevailing direction: 200 seeds, every bearing its own, uniform.
            var bearings = new double[200];
            for (int i = 0; i < bearings.Length; i++)
                bearings[i] = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom((ulong)i + 1)).FromDeg;
            var distinct = new HashSet<double>(bearings);
            Assert.That(distinct.Count, Is.EqualTo(bearings.Length), "every seed its own direction");
            bool uniform = Uniform(bearings, out string detail);
            TestContext.Out.WriteLine("Windy over 200 seeds: " + detail);
            Assert.That(uniform, Is.True, detail);

            // The known-wrong: one direction for the level ("mostly from the left" as a rule) fails uniformity; an
            // unseeded turbulence fails "the same seed, the same wind".
            var left = new double[200];
            for (int i = 0; i < left.Length; i++) left[i] = 225 + 90 * ((i * 0.618034) % 1);
            Assert.That(Uniform(left, out string d1), Is.False, "a direction from the left only fails: " + d1);
            History u1 = Gusts(WindLevel.Windy, 0x450F, 5, 1, Unseeded), u2 = Gusts(WindLevel.Windy, 0x450F, 5, 1, Unseeded);
            int same = 0;
            for (int i = 0; i < u1.Samples.Length; i++)
                if (u1.Samples[i].X == u2.Samples[i].X && u1.Samples[i].Z == u2.Samples[i].Z) same++;
            Assert.That(same, Is.LessThan(u1.Samples.Length), "an unseeded turbulence is not the same wind on the same seed");
            TestContext.Out.WriteLine($"known-wrong: from the left only: {d1}; an unseeded turbulence matched {same} of {u1.Samples.Length} samples");
        }

        [Test]
        public void TheLevelsHoldTheirIntensityAndSpectrum()
        {
            foreach (WindLevel level in new[] { WindLevel.Windy, WindLevel.Severe })
            {
                var runs = new List<History>();
                for (ulong seed = 1; seed <= 20; seed++) runs.Add(Gusts(level, seed, 1200, 50));
                WindPreset preset = WindPreset.Of(level);
                bool intensity = Intensity(runs, preset, out string di, out _);
                bool band = MeanInBand(runs, preset, out string db);
                var met = new List<History>();
                for (ulong seed = 1; seed <= 5; seed++) met.Add(Encounter(level, seed, 33, WindHarness.ReleaseSpeed));
                bool spectrum = Spectrum(met, preset, WindHarness.ReleaseSpeed, 2, 4, out string ds, out _);
                TestContext.Out.WriteLine($"{level}, 20 seeds of 1200 s: {di}; {db}; 5 seeds at the {WindHarness.ReleaseSpeed:F1} m/s cruise: {ds}");
                Assert.That(intensity, Is.True, di);
                Assert.That(band, Is.True, db);
                Assert.That(spectrum, Is.True, ds);

                // The known-wrong: a field that stops below 1 Hz at the cruise and white noise fail the spectrum, a σ that
                // ignores the wind fails the intensity, a mean 5 % off its drawn strength fails the unbiased mean.
                WindPreset cut = preset;
                cut.ShortestWavelength = WindHarness.ReleaseSpeed;
                var low = new List<History>();
                var white = new List<History>();
                var flat = new List<History>();
                var biased = new List<History>();
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    low.Add(Encounter(level, seed, 33, WindHarness.ReleaseSpeed, cut));
                    white.Add(Encounter(level, seed, 33, WindHarness.ReleaseSpeed, null, White));
                    flat.Add(Gusts(level, seed, 300, 50, FlatSigma));
                }
                foreach (History h in runs) biased.Add(Biased(h));
                Assert.That(Spectrum(low, preset, WindHarness.ReleaseSpeed, 2, 4, out string w0, out _), Is.False, "a field below 1 Hz fails the spectrum: " + w0);
                Assert.That(Spectrum(white, preset, WindHarness.ReleaseSpeed, 2, 4, out string w1, out _), Is.False, "white noise fails the spectrum: " + w1);
                Assert.That(Intensity(flat, preset, out string w2, out _), Is.False, "a flat σ fails the intensity: " + w2);
                Assert.That(MeanInBand(biased, preset, out string w3), Is.False, "a biased mean fails: " + w3);
                TestContext.Out.WriteLine($"  known-wrong: below 1 Hz: {w0}; white noise: {w1}; a flat 0.2 m/s σ: {w2}; a mean 5 % strong: {w3}");
            }
        }

        // flight-randomness's "Every draw is named and bounded" on a windy flight: every per-flight draw names its
        // stream and parameter and lies inside its bound, the counter-based streams are the named ones, and the
        // turbulence never leaves its stated bound.
        [Test]
        public void EveryWindDrawIsNamedAndBounded()
        {
            foreach (WindLevel level in new[] { WindLevel.Calm, WindLevel.Windy, WindLevel.Severe })
            {
                var random = new FlightRandom(0x450F);
                var wind = new WindField(WindSetup.Of(level), random);
                Assert.That(random.Opened, Is.EqualTo(new[] { "wind.direction", "wind.strength", "wind.meander", "wind.shift", "wind.turbulence" }));
                Assert.That(random.Draws.Count, Is.EqualTo(2));
                foreach (ParameterDraw d in random.Draws)
                {
                    Assert.That(d.Stream, Does.StartWith("wind."), "a wind stream");
                    Assert.That(d.Parameter, Does.StartWith("wind."), "a wind parameter");
                    Assert.That(d.Value, Is.InRange(d.Lo, d.Hi), d.Parameter);
                }
                double worst = 0;
                var at = new Double3(3, 35, -7);
                for (long k = 0; k < 600000; k += 7)
                {
                    wind.Begin(k, at, 0);
                    Double3 g = wind.Turbulence(at);
                    worst = Math.Max(worst, Math.Max(Math.Abs(g.X) / wind.Bound.X, Math.Max(Math.Abs(g.Y) / wind.Bound.Y, Math.Abs(g.Z) / wind.Bound.Z)));
                }
                Assert.That(worst, Is.LessThanOrEqualTo(1));
                TestContext.Out.WriteLine($"{level}: from {F(wind.FromDeg, "0.0")}° at {F(wind.MeanSpeed)} m/s, σ {F(wind.SigmaH)} / {F(wind.SigmaV)} m/s, "
                    + $"bound {F(wind.Bound.X)} / {F(wind.Bound.Y)} / {F(wind.Bound.Z)} m/s; 10 min reached {F(worst, "0.00")} of it");
            }
        }

        // The mean profile (K§6.7 layer 1): the log law over the cover from the drawn strength at the reference height,
        // 0 in the cover, held above the top; and the ground under the body is the world's (a raised floor lifts it).
        [Test]
        public void TheMeanFollowsTheLogLaw()
        {
            var wind = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(5));
            WindPreset p = wind.Preset;
            double z0 = p.Roughness, d = p.Displacement;
            wind.Begin(0, default, 0);
            Double3 m = wind.Mean;
            foreach (double z in new[] { 1.5, 2, 10, 35, 50, 100 })
            {
                double expected = Math.Log((z - d) / z0) / Math.Log((p.ReferenceHeight - d) / z0);
                var at = new Double3(9.7, z, 3.1);
                Double3 mean = wind.At(at) - wind.Turbulence(at);
                double got = Double3.Dot(mean, m) / Double3.Dot(m, m);
                Assert.That(got, Is.EqualTo(expected).Within(1e-12), "height " + z);
            }
            Assert.That(wind.At(new Double3(0, 0.1, 0)), Is.EqualTo(default(Double3)), "inside the cover the field is still");
            Double3 top = wind.At(new Double3(1, 100, 2)) - wind.Turbulence(new Double3(1, 100, 2));
            Double3 above = wind.At(new Double3(1, 400, 2)) - wind.Turbulence(new Double3(1, 400, 2));
            Assert.That((above - top).Length(), Is.LessThan(1e-12 * top.Length()), "held above the top");
            wind.Begin(0, default, 10);
            Assert.That(wind.At(new Double3(0, 10.1, 0)), Is.EqualTo(default(Double3)), "the ground under the body is the world's");
            wind.Begin(0, default, double.NegativeInfinity);
            Assert.That(wind.At(new Double3(0, 30, 0)), Is.EqualTo(default(Double3)), "a hole is sheltered until 1.3's cavity");
        }

        // The direction (K§8.3): computable from the seed alone, moving (meander and shifts), and a late start is the
        // same history from that second.
        [Test]
        public void TheDirectionHistoryIsTheSeeds()
        {
            var a = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom(9));
            var b = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom(9));
            var late = new WindField(new WindSetup { Level = WindLevel.Windy, StartSecond = 1234 }, new FlightRandom(9));
            double lo = double.MaxValue, hi = double.MinValue;
            for (int s = 0; s <= 7200; s++)
            {
                Assert.That(a.FromDegAt(s), Is.EqualTo(b.FromDegAt(s)));
                lo = Math.Min(lo, a.FromDegAt(s));
                hi = Math.Max(hi, a.FromDegAt(s));
                if (s + 1234 <= 7200) Assert.That(late.FromDegAt(s), Is.EqualTo(a.FromDegAt(s + 1234)), "second " + s);
            }
            TestContext.Out.WriteLine($"seed 9: drawn {F(a.FromDeg, "0.0")}°, over 120 min {F(lo, "0.0")}–{F(hi, "0.0")}°");
            Assert.That(hi - lo, Is.GreaterThan(20), "the direction moves");
        }

        // The levels' bands never overlap (K§8.6).
        [Test]
        public void TheLevelsDoNotOverlap()
        {
            WindPreset calm = WindPreset.Of(WindLevel.Calm), windy = WindPreset.Of(WindLevel.Windy), severe = WindPreset.Of(WindLevel.Severe);
            Assert.That(calm.MeanHi, Is.LessThan(windy.MeanLo));
            Assert.That(windy.MeanHi, Is.LessThan(severe.MeanLo));
            double[] bands = { calm.MeanLo, calm.MeanHi, windy.MeanLo, windy.MeanHi, severe.MeanLo, severe.MeanHi };
            Assert.That(bands, Is.EqualTo(new[] { 0.81, 1.35, 2.7, 4.5, 5.4, 9.0 }).Within(1e-12), "centre ± 25 % (K§8.6)");
        }
    }
}
