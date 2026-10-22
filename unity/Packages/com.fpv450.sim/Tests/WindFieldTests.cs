using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // wind-field (PHB-06), the stand-in wind: "Calm is never perfectly still" and "The same seed, the same wind", and
    // the level's preset as the targets design 8 gives it: the mean inside its band and unbiased, the gust's σ/U and
    // its time constant L/U, the direction uniform over the compass, every value inside its bound. Each statistical
    // target is also run on a known-wrong model, which must fail it (K§8.6: a target no wrong model fails is no target).
    public sealed class WindFieldTests
    {
        const int StepsPerSecond = 1000;

        // A wind history: the wind's velocity (world, m/s) every `every` steps, from a model stepped at 1 kHz.
        delegate Double3 WindModel(long step);

        sealed class History
        {
            public double Mean;          // the drawn mean speed, m/s
            public double FromDeg;       // the drawn bearing
            public Double3[] Samples;    // every Every steps
            public int Every;
        }

        // The stand-in itself, through a flight's own step: a bench body (no propulsion), so the wind is all it computes.
        static History Flight(WindLevel level, ulong seed, double seconds, int every)
        {
            var core = new FlightCore(new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = new MassModel(new[] { Cube() }), Wind = WindSetup.Of(level),
                StartPosition = new Double3(0, 1e6, 0),
            });
            var h = new History { Mean = core.Wind.MeanSpeed, FromDeg = core.Wind.FromDeg, Every = every };
            int n = (int)(seconds * StepsPerSecond / every);
            h.Samples = new Double3[n];
            var stick = new StickSample();
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < every; k++) core.Advance(stick);
                h.Samples[i] = core.Wind.At(core.State.Gust, core.State.Position);
            }
            return h;
        }

        // The stand-in's gust alone (the same class the flight steps), for the long statistics.
        static History Gusts(WindLevel level, ulong seed, double seconds, int every, Func<StandInWind, WindModel> wrong = null)
        {
            var wind = new StandInWind(WindSetup.Of(level), new FlightRandom(seed));
            var h = new History { Mean = wind.MeanSpeed, FromDeg = wind.FromDeg, Every = every };
            int n = (int)(seconds * StepsPerSecond / every);
            h.Samples = new Double3[n];
            Double3 gust = wind.StartGust;
            WindModel model = wrong?.Invoke(wind);
            long step = 0;
            Double3 last = default;
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < every; k++)
                {
                    if (model == null) wind.Advance(ref gust, step);
                    else last = model(step);
                    step++;
                }
                h.Samples[i] = model == null ? wind.At(gust, default) : last;
            }
            return h;
        }

        static DronePart Cube() => new DronePart
        {
            Name = "cube", Kind = PartKind.Box, Material = "carbon", Mass = 4.8, Size = new Double3(0.25, 0.25, 0.25),
        };

        // ---- the known-wrong models ----

        // A fixed vector: the drawn mean, no turbulence (K§8.3: never a fixed vector).
        static WindModel Fixed(StandInWind w) => step => w.Mean;

        // Still air.
        static WindModel Still(StandInWind w) => step => default;

        // White noise of the right σ: uniform draws every step, no memory.
        static WindModel White(StandInWind w)
        {
            var r = new Random(7);
            double a = w.SigmaH * Math.Sqrt(3), b = w.SigmaV * Math.Sqrt(3);
            return step => w.Mean + new Double3(a * (2 * r.NextDouble() - 1), b * (2 * r.NextDouble() - 1), a * (2 * r.NextDouble() - 1));
        }

        // The right process with a σ that does not grow with the wind: 0.2 m/s at every level.
        static WindModel FlatSigma(StandInWind w)
        {
            double tau = w.TauH, a = tau / (tau + 0.001), c = 0.2 * Math.Sqrt(3 * (1 - a * a));
            var r = new Random(11);
            Double3 g = w.StartGust * (0.2 / w.SigmaH);
            return step =>
            {
                g = new Double3(a * g.X + c * (2 * r.NextDouble() - 1), a * g.Y + c * 0.556 * (2 * r.NextDouble() - 1), a * g.Z + c * (2 * r.NextDouble() - 1));
                return w.Mean + g;
            };
        }

        // The right process with its mean 5 % strong (the drawn strength is not the wind's): the stand-in's own
        // history with 5 % of its mean added, which is what that model would fly on the same draws.
        static History Biased(History h)
        {
            var b = new History { Mean = h.Mean, FromDeg = h.FromDeg, Every = h.Every, Samples = new Double3[h.Samples.Length] };
            Double3 extra = StandInWind.Toward(h.FromDeg, 0.05 * h.Mean);
            for (int i = 0; i < b.Samples.Length; i++) b.Samples[i] = h.Samples[i] + extra;
            return b;
        }

        // The right process drawn from an unseeded generator: two flights on one seed meet different air.
        static WindModel Unseeded(StandInWind w)
        {
            double a = w.TauH / (w.TauH + 0.001), c = w.SigmaH * Math.Sqrt(3 * (1 - a * a));
            var r = new Random();
            Double3 g = w.StartGust;
            return step =>
            {
                g = new Double3(a * g.X + c * (2 * r.NextDouble() - 1), g.Y, a * g.Z + c * (2 * r.NextDouble() - 1));
                return w.Mean + g;
            };
        }

        // ---- the targets, each a function of histories that passes or fails ----

        // Never perfectly still: in every 10 s window the wind is not zero, and it moves (its range over the window
        // passes 1 mm/s in some component).
        static bool NeverStill(List<History> runs, out string detail)
        {
            double smallestSpeed = double.MaxValue, smallestRange = double.MaxValue;
            foreach (History h in runs)
            {
                int window = 10 * StepsPerSecond / h.Every;
                for (int start = 0; start + window <= h.Samples.Length; start += window)
                {
                    double lo0 = double.MaxValue, hi0 = double.MinValue, lo1 = lo0, hi1 = hi0, lo2 = lo0, hi2 = hi0;
                    for (int i = start; i < start + window; i++)
                    {
                        Double3 v = h.Samples[i];
                        smallestSpeed = Math.Min(smallestSpeed, v.Length());
                        lo0 = Math.Min(lo0, v.X); hi0 = Math.Max(hi0, v.X);
                        lo1 = Math.Min(lo1, v.Y); hi1 = Math.Max(hi1, v.Y);
                        lo2 = Math.Min(lo2, v.Z); hi2 = Math.Max(hi2, v.Z);
                    }
                    smallestRange = Math.Min(smallestRange, Math.Max(hi0 - lo0, Math.Max(hi1 - lo1, hi2 - lo2)));
                }
            }
            detail = $"the slowest sample {F(smallestSpeed, "0.000")} m/s, the stillest 10 s window moves {F(1000 * smallestRange, "0.0")} mm/s";
            return smallestSpeed > 0 && smallestRange > 0.001;
        }

        // The mean: each drawn mean inside the level's band, and the wind's along-mean component unbiased about it
        // (the pooled offset within 3 standard errors, the error from each run's own τ).
        static bool MeanInBand(List<History> runs, WindPreset preset, out string detail)
        {
            bool inBand = true;
            double sum = 0, variance = 0;
            foreach (History h in runs)
            {
                inBand &= h.Mean >= preset.MeanLo && h.Mean <= preset.MeanHi;
                Double3 dir = StandInWind.Toward(h.FromDeg, 1);
                double along = 0;
                foreach (Double3 v in h.Samples) along += Double3.Dot(v, dir);
                along /= h.Samples.Length;
                double sigma = WindPreset.Intensity * h.Mean, tau = WindPreset.HorizontalScale / h.Mean;
                double seconds = h.Samples.Length * h.Every / (double)StepsPerSecond;
                sum += along - h.Mean;
                variance += sigma * sigma * 2 * tau / seconds; // the mean of a Gauss–Markov record of that length
            }
            double offset = sum / runs.Count, error = Math.Sqrt(variance) / runs.Count;
            detail = $"drawn means {(inBand ? "inside" : "OUTSIDE")} [{F(preset.MeanLo)}, {F(preset.MeanHi)}] m/s; the along-mean wind {F(offset, "+0.0000;-0.0000")} m/s "
                + $"off its drawn mean (standard error {F(error, "0.0000")})";
            return inBand && Math.Abs(offset) <= 3 * error;
        }

        // σ/U: the horizontal gust's standard deviation over the drawn mean, pooled, inside K§6.7's 0.12–0.15; the
        // vertical's over the horizontal within 10 % of MIL-F-8785C's 0.556.
        static bool Intensity(List<History> runs, out string detail, out double ratio)
        {
            double h2 = 0, v2 = 0, u2 = 0;
            long n = 0;
            foreach (History h in runs)
            {
                foreach (Double3 s in h.Samples)
                {
                    Double3 g = s - StandInWind.Toward(h.FromDeg, h.Mean);
                    h2 += (g.X * g.X + g.Z * g.Z) / 2;
                    v2 += g.Y * g.Y;
                    n++;
                }
                u2 += h.Mean * h.Mean * h.Samples.Length;
            }
            ratio = Math.Sqrt(h2 / u2);
            double vertical = Math.Sqrt(v2 / h2);
            detail = $"σ_u/U {F(ratio, "0.0000")} (K§6.7: 0.12–0.15), σ_w/σ_u {F(vertical, "0.000")} (0.556)";
            return ratio >= 0.12 && ratio <= 0.15 && Math.Abs(vertical / 0.556 - 1) <= 0.1;
        }

        // τ: the gust's time constant, horizontal and vertical, each within 20 % of L/U at the drawn mean. Estimated as
        // the Gauss–Markov process's maximum likelihood does, from the lag-one regression of the samples,
        // a = Σ x_t·x_{t+Δ} / Σ x_t², τ = −Δ / ln a, per run in units of its own τ (the runs draw different means),
        // pooled by each run's length in τ (its information).
        static bool TimeConstant(List<History> runs, out string detail)
        {
            double h = Scale(runs, false), v = Scale(runs, true);
            detail = $"the gust's time constant {F(h, "0.00")} of L_u/U horizontally and {F(v, "0.00")} of L_w/U vertically (1)";
            return Math.Abs(h - 1) <= 0.2 && Math.Abs(v - 1) <= 0.2;
        }

        static double Scale(List<History> runs, bool vertical)
        {
            double sum = 0, weight = 0;
            foreach (History h in runs)
            {
                double tau = (vertical ? WindPreset.VerticalScale : WindPreset.HorizontalScale) / h.Mean;
                double dt = h.Every / (double)StepsPerSecond;
                Double3 mean = StandInWind.Toward(h.FromDeg, h.Mean);
                double xy = 0, xx = 0;
                for (int axis = 0; axis < (vertical ? 1 : 2); axis++)
                {
                    for (int i = 0; i + 1 < h.Samples.Length; i++)
                    {
                        double x0 = vertical ? h.Samples[i].Y : axis == 0 ? h.Samples[i].X - mean.X : h.Samples[i].Z - mean.Z;
                        double x1 = vertical ? h.Samples[i + 1].Y : axis == 0 ? h.Samples[i + 1].X - mean.X : h.Samples[i + 1].Z - mean.Z;
                        xy += x0 * x1;
                        xx += x0 * x0;
                    }
                }
                double a = xy / xx;
                double estimate = a > 0 && a < 1 ? -dt / Math.Log(a) : a >= 1 ? double.PositiveInfinity : 0;
                double length = h.Samples.Length * dt / tau;
                sum += estimate / tau * length;
                weight += length;
            }
            return sum / weight;
        }

        // The direction: uniform over the compass across seeds (χ² over 8 sectors under its 0.1 % point, 24.3).
        static bool Uniform(double[] bearings, out string detail)
        {
            var sector = new int[8];
            foreach (double b in bearings) sector[(int)(((b % 360) + 360) % 360 / 45) % 8]++;
            double expected = bearings.Length / 8.0, chi2 = 0;
            foreach (int c in sector) chi2 += (c - expected) * (c - expected) / expected;
            detail = $"{bearings.Length} bearings over 8 sectors: χ² {F(chi2, "0.0")} (uniform under 24.3)";
            return chi2 < 24.3;
        }

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        // ---- the scenarios ----

        [Test]
        public void CalmIsNeverPerfectlyStill()
        {
            var runs = new List<History>();
            for (ulong seed = 1; seed <= 5; seed++) runs.Add(Flight(WindLevel.Calm, seed, 120, 10));
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
            var core = new FlightCore(new FlightSetup { Seed = 3, World = FlightWorld.Floor(), Mass = new MassModel(new[] { Cube() }), Wind = WindSetup.Of(WindLevel.Calm) });
            Log.LogHeader header = Log.LogHeader.For(new FlightSetup { Seed = 3, World = FlightWorld.Floor(), Mass = new MassModel(new[] { Cube() }), Wind = WindSetup.Of(WindLevel.Calm) }, core);
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
