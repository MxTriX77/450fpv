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
    // wind-field (PHB-06), the stand-in wind: "Calm is never perfectly still" and "The same seed, the same wind", and
    // the level's preset as the targets design 8 gives it: the mean inside its band and unbiased, the gust's σ/U and
    // its time constant L/U, the direction uniform over the compass, every value inside its bound. Each statistical
    // target is also run on a known-wrong model, which must fail it (K§8.6: a target no wrong model fails is no target).
    // The targets and the known-wrong models are WindTargets'.
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
            Assert.That(header.WindModel, Is.EqualTo("stand-in"));
            Assert.That(header.Wind.Level, Is.EqualTo(WindLevel.Calm));
        }

        [Test]
        public void TheSameSeedTheSameWind()
        {
            // Two powered flights on one seed and level, flown with different sticks: the wind at the body bit for bit.
            DroneSpec spec = RepositoryDrone.Spec();
            FlightCore Fly(ulong seed) => new FlightCore(new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(spec.Nominal), Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(), Wind = WindSetup.Of(WindLevel.Windy), StartPosition = new Double3(0, 300, 0),
            });
            FlightCore a = Fly(0x450F), b = Fly(0x450F);
            for (int k = 0; k < 5000; k++)
            {
                a.Advance(new StickSample { HostUs = k * 1000L, Arm = k >= 20, Throttle = k > 40 ? 0.5 : 0 });
                b.Advance(new StickSample { HostUs = k * 1000L, Arm = k >= 20, Throttle = k > 40 ? 0.6 : 0, Roll = 0.3 });
                Assert.That(a.State.Gust, Is.EqualTo(b.State.Gust), "step " + k);
            }
            Assert.That(a.State.Position, Is.Not.EqualTo(b.State.Position), "the flights themselves differ");

            // A different seed, a different prevailing direction: 200 seeds, every bearing its own, uniform.
            var bearings = new double[200];
            for (int i = 0; i < bearings.Length; i++)
                bearings[i] = new StandInWind(WindSetup.Of(WindLevel.Windy), new FlightRandom((ulong)i + 1)).FromDeg;
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
        public void TheLevelsHoldTheirIntensityAndScale()
        {
            foreach (WindLevel level in new[] { WindLevel.Windy, WindLevel.Severe })
            {
                var runs = new List<History>();
                for (ulong seed = 1; seed <= 10; seed++) runs.Add(Gusts(level, seed, 1200, 50));
                WindPreset preset = WindPreset.Of(level);
                bool intensity = Intensity(runs, out string di, out _);
                bool scale = TimeConstant(runs, out string ds);
                bool band = MeanInBand(runs, preset, out string db);
                TestContext.Out.WriteLine($"{level}, 10 seeds of 1200 s: {di}; {ds}; {db}");
                Assert.That(intensity, Is.True, di);
                Assert.That(scale, Is.True, ds);
                Assert.That(band, Is.True, db);

                // The known-wrong: white noise fails the scale, a σ that ignores the wind fails the intensity, a mean
                // 5 % off its drawn strength fails the unbiased mean.
                var white = new List<History>();
                var flat = new List<History>();
                var biased = new List<History>();
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    white.Add(Gusts(level, seed, 300, 50, White));
                    flat.Add(Gusts(level, seed, 300, 50, FlatSigma));
                }
                foreach (History h in runs) biased.Add(Biased(h));
                Assert.That(TimeConstant(white, out string w1), Is.False, "white noise fails the scale: " + w1);
                Assert.That(Intensity(flat, out string w2, out _), Is.False, "a flat σ fails the intensity: " + w2);
                Assert.That(MeanInBand(biased, preset, out string w3), Is.False, "a biased mean fails: " + w3);
                TestContext.Out.WriteLine($"  known-wrong: white noise: {w1}; a flat 0.2 m/s σ: {w2}; a mean 5 % strong: {w3}");
            }
        }

        // flight-randomness's "Every draw is named and bounded" on a windy flight: every per-flight draw names its
        // stream and parameter and lies inside its bound, and the evolving gust never leaves its own.
        [Test]
        public void EveryWindDrawIsNamedAndBounded()
        {
            foreach (WindLevel level in new[] { WindLevel.Calm, WindLevel.Windy, WindLevel.Severe })
            {
                var random = new FlightRandom(0x450F);
                var wind = new StandInWind(WindSetup.Of(level), random);
                Assert.That(random.Opened, Is.EqualTo(new[] { "wind.direction", "wind.strength", "wind.gust0", "wind.turbulence" }));
                Assert.That(random.Draws.Count, Is.EqualTo(5));
                foreach (ParameterDraw d in random.Draws)
                {
                    Assert.That(d.Stream, Does.StartWith("wind."), "a wind stream");
                    Assert.That(d.Parameter, Does.StartWith("wind."), "a wind parameter");
                    Assert.That(d.Value, Is.InRange(d.Lo, d.Hi), d.Parameter);
                }
                Double3 gust = wind.StartGust;
                double worstH = 0, worstV = 0;
                for (long k = 0; k < 600000; k++)
                {
                    wind.Advance(ref gust, k);
                    worstH = Math.Max(worstH, Math.Max(Math.Abs(gust.X), Math.Abs(gust.Z)) / wind.BoundH);
                    worstV = Math.Max(worstV, Math.Abs(gust.Y) / wind.BoundV);
                }
                Assert.That(worstH, Is.LessThanOrEqualTo(1));
                Assert.That(worstV, Is.LessThanOrEqualTo(1));
                TestContext.Out.WriteLine($"{level}: from {F(wind.FromDeg, "0.0")}° at {F(wind.MeanSpeed)} m/s, σ {F(wind.SigmaH)} / {F(wind.SigmaV)} m/s, "
                    + $"τ {F(wind.TauH, "0.0")} / {F(wind.TauV, "0.0")} s; 10 min reached {F(worstH, "0.00")} and {F(worstV, "0.00")} of the 4σ bound");
            }
        }

        // The levels' bands never overlap (K§8.6).
        [Test]
        public void TheLevelsDoNotOverlap()
        {
            WindPreset calm = WindPreset.Of(WindLevel.Calm), windy = WindPreset.Of(WindLevel.Windy), severe = WindPreset.Of(WindLevel.Severe);
            Assert.That(calm.MeanHi, Is.LessThan(windy.MeanLo));
            Assert.That(windy.MeanHi, Is.LessThan(severe.MeanLo));
            double[] bands = { calm.MeanLo, calm.MeanHi, windy.MeanLo, windy.MeanHi, severe.MeanLo, severe.MeanHi };
            Assert.That(bands, Is.EqualTo(new[] { 0.6, 1.0, 1.875, 3.125, 3.75, 6.25 }).Within(1e-12), "centre ± 25 % (K§8.6)");
        }
    }
}
