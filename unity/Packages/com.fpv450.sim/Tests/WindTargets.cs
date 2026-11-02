using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The wind field's open-air targets (p7c-wind 1.4, design 3) as functions of wind histories that pass or fail, and
    /// the known-wrong models each must fail (K§8.6): the suite asserts them (WindFieldTests), tools/flightbench
    /// --scenarios prints them. The histories come from the field through a flight's own step (FlightHistory) or from the
    /// field alone (Gusts, Encounter), with or without a known-wrong model in its place. Every history samples the
    /// reference height over the floor, where the profile is 1.
    /// </summary>
    public static class WindTargets
    {
        public const int StepsPerSecond = 1000;

        // A wind history: the wind's velocity (world, m/s) every `every` steps, from a model stepped at 1 kHz.
        public delegate Double3 WindModel(long step);

        public sealed class History
        {
            public double Mean;          // the drawn strength, m/s
            public double FromDeg;       // the drawn bearing
            public Double3[] Samples;    // every Every steps
            public Double3[] Means;      // the field's mean wind at each sample's time
            public Double3 Across;       // Encounter: the horizontal unit vector across the path
            public int Every;
        }

        static Double3 Reference(WindField w) => new Double3(0, w.Preset.ReferenceHeight, 0);

        // The field through a flight's own step: a bench body (no propulsion), the wind at the reference height.
        public static History FlightHistory(WindLevel level, ulong seed, double seconds, int every)
        {
            var core = new FlightCore(BenchFlight(level, seed));
            var h = new History { Mean = core.Wind.MeanSpeed, FromDeg = core.Wind.FromDeg, Every = every };
            int n = (int)(seconds * StepsPerSecond / every);
            h.Samples = new Double3[n];
            h.Means = new Double3[n];
            var stick = new StickSample();
            Double3 at = Reference(core.Wind);
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < every; k++) core.Advance(stick);
                h.Samples[i] = core.Wind.At(at);
                h.Means[i] = core.Wind.Mean;
            }
            return h;
        }

        // The field alone at the reference height over the floor, or a known-wrong model in its place.
        public static History Gusts(WindLevel level, ulong seed, double seconds, int every, Func<WindField, WindModel> wrong = null)
        {
            var wind = new WindField(WindSetup.Of(level), new FlightRandom(seed));
            var h = new History { Mean = wind.MeanSpeed, FromDeg = wind.FromDeg, Every = every };
            int n = (int)(seconds * StepsPerSecond / every);
            h.Samples = new Double3[n];
            h.Means = new Double3[n];
            WindModel model = wrong?.Invoke(wind);
            Double3 at = Reference(wind);
            long step = 0;
            Double3 last = default;
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < every; k++)
                {
                    if (model != null) last = model(step);
                    step++;
                }
                wind.Begin(step, at, 0);
                h.Samples[i] = model == null ? wind.At(at) : last;
                h.Means[i] = wind.Mean;
            }
            return h;
        }

        /// <summary>
        /// The field met at `airspeed` through the air (frozen and advected: the path moves with the drift plus airspeed
        /// along the prevailing wind's left, at the reference height), every step for `seconds`; or a known-wrong model.
        /// </summary>
        public static History Encounter(WindLevel level, ulong seed, double seconds, double airspeed, WindPreset? preset = null,
            Func<WindField, WindModel> wrong = null)
        {
            var random = new FlightRandom(seed);
            WindField wind = preset == null ? new WindField(WindSetup.Of(level), random) : new WindField(WindSetup.Of(level), random, preset.Value);
            Double3 down = WindField.Toward(wind.FromDeg, 1);
            var path = new Double3(-down.Z, 0, down.X); // the wind abeam: across the prevailing wind
            var h = new History { Mean = wind.MeanSpeed, FromDeg = wind.FromDeg, Every = 1, Across = down };
            int n = (int)(seconds * StepsPerSecond);
            h.Samples = new Double3[n];
            h.Means = new Double3[n];
            WindModel model = wrong?.Invoke(wind);
            for (int i = 0; i < n; i++)
            {
                wind.Begin(i, default, 0);
                Double3 at = wind.Drift + path * (airspeed * i / (double)StepsPerSecond) + Reference(wind);
                h.Samples[i] = model == null ? wind.At(at) : model(i);
                h.Means[i] = wind.Mean;
            }
            return h;
        }

        static DronePart Cube() => new DronePart
        {
            Name = "cube", Kind = PartKind.Box, Material = "carbon", Mass = 4.8, Size = new Double3(0.25, 0.25, 0.25),
        };

        // ---- the known-wrong models ----

        // A fixed vector: the drawn mean, no turbulence, no meander (K§8.3: never a fixed vector).
        public static WindModel Fixed(WindField w) => step => WindField.Toward(w.FromDeg, w.MeanSpeed);

        // Still air.
        public static WindModel Still(WindField w) => step => default;

        // White noise of the right σ about the field's mean: uniform draws every step, no memory.
        public static WindModel White(WindField w)
        {
            var r = new Random(7);
            double a = w.SigmaH * Math.Sqrt(3), b = w.SigmaV * Math.Sqrt(3);
            return step =>
            {
                w.Begin(step, default, 0);
                return w.Mean + new Double3(a * (2 * r.NextDouble() - 1), b * (2 * r.NextDouble() - 1), a * (2 * r.NextDouble() - 1));
            };
        }

        // The field with a σ that does not grow with the wind: 0.2 m/s at every level.
        public static WindModel FlatSigma(WindField w)
        {
            Double3 at = Reference(w);
            return step =>
            {
                w.Begin(step, at, 0);
                return w.Mean + (w.At(at) - w.Mean) * (0.2 / w.SigmaH);
            };
        }

        // The field with its mean 5 % strong (the drawn strength is not the wind's): the field's own history with 5 % of
        // its mean added, which is what that model would fly on the same draws.
        public static History Biased(History h)
        {
            var b = new History { Mean = h.Mean, FromDeg = h.FromDeg, Every = h.Every, Samples = new Double3[h.Samples.Length], Means = h.Means };
            for (int i = 0; i < b.Samples.Length; i++) b.Samples[i] = h.Samples[i] + h.Means[i] * 0.05;
            return b;
        }

        // The field drawn from an unseeded generator: two flights on one seed meet different air.
        public static WindModel Unseeded(WindField w)
        {
            var other = new WindField(WindSetup.Of(w.Preset.Level), new FlightRandom((ulong)new Random().Next()));
            Double3 at = Reference(w);
            return step =>
            {
                other.Begin(step, at, 0);
                return other.At(at);
            };
        }

        // ---- the targets, each a function of histories that passes or fails ----

        // Never perfectly still: in every 10 s window the wind is not zero, and it moves (its range over the window
        // passes 1 mm/s in some component).
        public static bool NeverStill(List<History> runs, out string detail)
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

        // The mean: each drawn strength inside the level's band, and the wind's component along the field's own mean
        // unbiased about it (the pooled offset within 3 standard errors, the error from the record's length in L_u/U).
        public static bool MeanInBand(List<History> runs, WindPreset preset, out string detail)
        {
            bool inBand = true;
            double sum = 0, variance = 0;
            foreach (History h in runs)
            {
                inBand &= h.Mean >= preset.MeanLo && h.Mean <= preset.MeanHi;
                double along = 0;
                for (int i = 0; i < h.Samples.Length; i++)
                {
                    Double3 m = h.Means[i];
                    along += Double3.Dot(h.Samples[i] - m, m * (1 / m.Length()));
                }
                along /= h.Samples.Length;
                double sigma = preset.Intensity * h.Mean, tau = preset.HorizontalScale / h.Mean;
                double seconds = h.Samples.Length * h.Every / (double)StepsPerSecond;
                sum += along;
                variance += sigma * sigma * 2 * tau / seconds; // the mean of a record of that length in integral times
            }
            double offset = sum / runs.Count, error = Math.Sqrt(variance) / runs.Count;
            detail = $"drawn strengths {(inBand ? "inside" : "OUTSIDE")} [{F(preset.MeanLo)}, {F(preset.MeanHi)}] m/s; the along-mean wind {F(offset, "+0.0000;-0.0000")} m/s "
                + $"off the field's mean (standard error {F(error, "0.0000")})";
            return inBand && Math.Abs(offset) <= 3 * error;
        }

        // σ/U: the horizontal turbulence's standard deviation over the drawn strength, pooled, inside K§6.7's 0.12–0.15;
        // the vertical's over the horizontal within 10 % of the preset's.
        public static bool Intensity(List<History> runs, WindPreset preset, out string detail, out double ratio)
        {
            double h2 = 0, v2 = 0, u2 = 0;
            foreach (History h in runs)
            {
                for (int i = 0; i < h.Samples.Length; i++)
                {
                    Double3 g = h.Samples[i] - h.Means[i];
                    h2 += (g.X * g.X + g.Z * g.Z) / 2;
                    v2 += g.Y * g.Y;
                }
                u2 += h.Mean * h.Mean * h.Samples.Length;
            }
            ratio = Math.Sqrt(h2 / u2);
            double vertical = Math.Sqrt(v2 / h2);
            detail = $"σ_u/U {F(ratio, "0.0000")} (K§6.7: 0.12–0.15), σ_w/σ_u {F(vertical, "0.000")} ({F(preset.VerticalRatio)})";
            return ratio >= 0.12 && ratio <= 0.15 && Math.Abs(vertical / preset.VerticalRatio - 1) <= 0.1;
        }

        /// <summary>
        /// The energy at the encounter frequency (design 3, K§6.7: "energy must reach 2–4 Hz"): the across-path gust's
        /// variance between `lo` and `hi` Hz on Encounter histories at `airspeed`, from each run's periodogram (a radix-2
        /// FFT of its first 2^n samples less their mean), pooled, over the von Kármán transverse spectrum's own in that band
        /// (MIL-HDBK-1797: Φ_v(Ω) = σ²·(L/π)·(1 + (8/3)·(1.339·L·Ω)²) / (1 + (1.339·L·Ω)²)^(11/6), Ω = 2π·f / airspeed). It
        /// passes within 0.7–1.4 of it.
        /// </summary>
        public static bool Spectrum(List<History> runs, WindPreset preset, double airspeed, double lo, double hi, out string detail, out double ratio)
        {
            double measured = 0, expected = 0;
            foreach (History h in runs)
            {
                int n = 1;
                while (n * 2 <= h.Samples.Length) n *= 2;
                var re = new double[n];
                var im = new double[n];
                double mean = 0;
                for (int i = 0; i < n; i++) mean += Double3.Dot(h.Samples[i] - h.Means[i], h.Across) / n;
                for (int i = 0; i < n; i++) re[i] = Double3.Dot(h.Samples[i] - h.Means[i], h.Across) - mean;
                Fft(re, im);
                double rate = StepsPerSecond / (double)h.Every, df = rate / n, band = 0;
                for (int j = 1; j < n / 2; j++)
                {
                    double f = j * df;
                    if (f >= lo && f < hi) band += 2 * (re[j] * re[j] + im[j] * im[j]) / ((double)n * n);
                }
                measured += band;
                double sigma = preset.Intensity * h.Mean, length = preset.HorizontalScale;
                double o1 = 2 * Math.PI * lo / airspeed, o2 = 2 * Math.PI * hi / airspeed, sum = 0;
                const int steps = 2000;
                for (int i = 0; i < steps; i++)
                {
                    double o = o1 + (o2 - o1) * (i + 0.5) / steps, y = 1.339 * length * o;
                    sum += sigma * sigma * (length / Math.PI) * (1 + 8.0 / 3 * y * y) / Math.Pow(1 + y * y, 11.0 / 6) * (o2 - o1) / steps;
                }
                expected += sum;
            }
            ratio = measured / expected;
            detail = $"the across-path gust's {F(lo, "0")}–{F(hi, "0")} Hz at {F(airspeed, "0")} m/s: {F(Math.Sqrt(measured / runs.Count), "0.0000")} m/s RMS against "
                + $"von Kármán's {F(Math.Sqrt(expected / runs.Count), "0.0000")} (variance ratio {F(ratio, "0.00")}; 0.7–1.4)";
            return ratio >= 0.7 && ratio <= 1.4;
        }

        // An in-place radix-2 FFT (test-only).
        static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double a = -2 * Math.PI / len, wr = Math.Cos(a), wi = Math.Sin(a);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int j = 0; j < len / 2; j++)
                    {
                        int p = i + j, q = p + len / 2;
                        double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr;
                        re[q] = re[p] - tr;
                        im[q] = im[p] - ti;
                        re[p] += tr;
                        im[p] += ti;
                        double nr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = nr;
                    }
                }
            }
        }

        // The direction: uniform over the compass across seeds (χ² over 8 sectors under its 0.1 % point, 24.3).
        public static bool Uniform(double[] bearings, out string detail)
        {
            var sector = new int[8];
            foreach (double b in bearings) sector[(int)(((b % 360) + 360) % 360 / 45) % 8]++;
            double expected = bearings.Length / 8.0, chi2 = 0;
            foreach (int c in sector) chi2 += (c - expected) * (c - expected) / expected;
            detail = $"{bearings.Length} bearings over 8 sectors: χ² {F(chi2, "0.0")} (uniform under 24.3)";
            return chi2 < 24.3;
        }

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>A bench body's flight in a wind, for the header and the never-still scenario (a body without
        /// propulsion falls; the histories sample the field at the reference height, not at the body).</summary>
        public static FlightSetup BenchFlight(WindLevel level, ulong seed) => new FlightSetup
        {
            Seed = seed, World = FlightWorld.Floor(), Mass = new MassModel(new[] { Cube() }), Wind = WindSetup.Of(level),
            StartPosition = new Double3(0, 1e6, 0),
        };
    }
}
