using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The stand-in wind's targets (wind-field, design 8) as functions of wind histories that pass or fail, and the
    /// known-wrong models each must fail (K§8.6): the suite asserts them (WindFieldTests), tools/flightbench
    /// --scenarios prints them. The histories come from the wind model through a flight's own step (FlightHistory) or from the
    /// wind class alone (Gusts), with or without a known-wrong model in its place.
    /// </summary>
    public static class WindTargets
    {
        public const int StepsPerSecond = 1000;

        // A wind history: the wind's velocity (world, m/s) every `every` steps, from a model stepped at 1 kHz.
        public delegate Double3 WindModel(long step);

        public sealed class History
        {
            public double Mean;          // the drawn mean speed, m/s
            public double FromDeg;       // the drawn bearing
            public Double3[] Samples;    // every Every steps
            public int Every;
        }

        // The stand-in itself, through a flight's own step: a bench body (no propulsion), so the wind is all it computes.
        public static History FlightHistory(WindLevel level, ulong seed, double seconds, int every)
        {
            var core = new FlightCore(BenchFlight(level, seed));
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
        public static History Gusts(WindLevel level, ulong seed, double seconds, int every, Func<StandInWind, WindModel> wrong = null)
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
        public static WindModel Fixed(StandInWind w) => step => w.Mean;

        // Still air.
        public static WindModel Still(StandInWind w) => step => default;

        // White noise of the right σ: uniform draws every step, no memory.
        public static WindModel White(StandInWind w)
        {
            var r = new Random(7);
            double a = w.SigmaH * Math.Sqrt(3), b = w.SigmaV * Math.Sqrt(3);
            return step => w.Mean + new Double3(a * (2 * r.NextDouble() - 1), b * (2 * r.NextDouble() - 1), a * (2 * r.NextDouble() - 1));
        }

        // The right process with a σ that does not grow with the wind: 0.2 m/s at every level.
        public static WindModel FlatSigma(StandInWind w)
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
        public static History Biased(History h)
        {
            var b = new History { Mean = h.Mean, FromDeg = h.FromDeg, Every = h.Every, Samples = new Double3[h.Samples.Length] };
            Double3 extra = StandInWind.Toward(h.FromDeg, 0.05 * h.Mean);
            for (int i = 0; i < b.Samples.Length; i++) b.Samples[i] = h.Samples[i] + extra;
            return b;
        }

        // The right process drawn from an unseeded generator: two flights on one seed meet different air.
        public static WindModel Unseeded(StandInWind w)
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

        // The mean: each drawn mean inside the level's band, and the wind's along-mean component unbiased about it
        // (the pooled offset within 3 standard errors, the error from each run's own τ).
        public static bool MeanInBand(List<History> runs, WindPreset preset, out string detail)
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
        public static bool Intensity(List<History> runs, out string detail, out double ratio)
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
        public static bool TimeConstant(List<History> runs, out string detail)
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
        /// <summary>A bench body's flight in a wind, for the header and the never-still scenario.</summary>
        public static FlightSetup BenchFlight(WindLevel level, ulong seed) => new FlightSetup
        {
            Seed = seed, World = FlightWorld.Floor(), Mass = new MassModel(new[] { Cube() }), Wind = WindSetup.Of(level),
            StartPosition = new Double3(0, 1e6, 0),
        };

    }
}
