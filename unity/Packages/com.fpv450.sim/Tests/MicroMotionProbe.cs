using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The camera's micro-motion probe (p7-micro-motion; D-059, PHY-25, PHY-13; docs/reference-notes/hover-micro-motion.md).
    /// It flies the repository's drone with its whole airframe in a hover (SimPilot holding a point) or in clip P's cruise
    /// (HarnessPilot over open field, wind abeam from the left, no strip), and logs the camera's attitude every step: the
    /// rigid body's turned by the frame's elastic turn at the lens (AirframeModes), in the tracker's roll, pitch and
    /// heading (TrackerAngles, wind.md §1.2). From that it gives:
    /// - **True bands:** the 1 kHz attitude's Welch PSD (Hann, 8192 steps, half overlap, linear detrend) as RMS over K§8.6's
    ///   bands (wind_stats.BANDS) and above the frames' 15 Hz Nyquist.
    /// - **Frame bands:** the attitude point-sampled at 29.917 Hz, as the harness and clip P's recorder take it, through
    ///   wind_stats.py's Welch (128 frames): what lies above 15 Hz folds into these (aliasing), as it would in the footage.
    ///   With FieldPick the frames take the newest whole field's horizon instant instead of a uniform clock (the
    ///   recorder's 50 → 29.917 Hz pick, video-feed.md §0; the measurement chain, not the drone).
    /// - **Spreads:** each axis's residual about wind_stats.py's σ = 0.6 s Gaussian trend (K§8.6's T1, T2, T5 over field).
    /// - **p7e's figure:** CameraFeelProbe's turn above 5 Hz (a first-order 5 Hz high-pass of the camera's rate, integrated),
    ///   so this probe's hover rows can be set beside p7e-camera-feel's design §1.
    /// </summary>
    public static class MicroMotion
    {
        const double RadToDeg = 57.29577951308232, DegToRad = 0.017453292519943295, TwoPi = 6.283185307179586;

        /// <summary>The camera's frame rate, Hz (measured, clip P's recording, wind.md §1.1).</summary>
        public const double Fps = HarnessPilot.CameraHz;

        /// <summary>The band edges, Hz: K§8.6's six (to the frames' Nyquist), then 15–50, 50–150 and 150–500.</summary>
        public static readonly double[] Edges = { 0.23, 0.5, 1, 2, 4, 8, 15, 50, 150, 500 };

        /// <summary>How many of the bands the frames resolve (to 15 Hz).</summary>
        public const int FrameBands = 6;

        /// <summary>The measured start (after arming, spin-up and the release's transient), s, and the default length.</summary>
        public const double Discard = 5, Seconds = 40;

        /// <summary>The hover's and the cruise's height, m (assumed: inside K§8.5's 20–50 m, at the field's reference height).</summary>
        public const double Height = 35;

        public enum Kind { Hover, Cruise }

        /// <summary>One flight's camera, from the measured start: the attitude every step (rad), the camera's rate (body
        /// rad/s), and the means beside it.</summary>
        public sealed class Run
        {
            public Kind Kind;
            public WindLevel Level;
            public ulong Seed;
            public double[] Roll, Pitch, Heading;
            public Double3[] Rate;
            public double MeanAirspeed, MeanWind, MeanRotorHz;
        }

        /// <summary>A flight's analysis: per axis (roll, pitch, heading) the true and frame band RMS (deg), the residual
        /// spread (deg); and p7e's turn above 5 Hz (deg, the three axes together).</summary>
        public sealed class Bands
        {
            public double[][] True = new double[3][], Frame = new double[3][], Picked = new double[3][];
            public double[] Spread = new double[3];
            public double Above5;

            /// <summary>The roll's share of the thrust-tilt residual's variance, cov(roll, e)/var(e), e = roll + k·heading with
            /// k = −sin(mean pitch) (wind_stats.py's airframe split; K§8.4 measured 0.80 over P's field, 0.64 over the belt).</summary>
            public double RollShare;

            /// <summary>A band set's RMS from band lo to band hi (indices into Edges), deg.</summary>
            public static double Over(double[] b, int lo, int hi)
            {
                double s = 0;
                for (int i = lo; i < hi && i < b.Length; i++) s += b[i] * b[i];
                return Math.Sqrt(s);
            }
        }

        /// <summary>The whole airframe a pilot's flight has (as WindHarness flies): asymmetry, rings, ground effect,
        /// flapping, the rotors' wakes (p7b-airframe 1.3d) and modes.</summary>
        public static void Airframe(DroneSpec spec, FlightSetup setup)
        {
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.Flapping = spec.BuildFlapping();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Modes = spec.BuildModes();
        }

        /// <summary>
        /// Flies one probe flight. `edit` changes the setup before the flight (a source switched or scaled, a known-wrong
        /// model); `steady` flies the seed's drawn mean wind as one fixed vector (no turbulence: the known-wrong field);
        /// `split` is the cruise pilot's share of the tilt on the horizon (K§8.5's 0.64; K§8.4 measured 0.80 over the field).
        /// </summary>
        public static Run Fly(DroneSpec spec, Kind kind, WindLevel level, ulong seed, Action<FlightSetup> edit = null,
            bool steady = false, double seconds = Seconds, double split = 0.64)
        {
            FlightSetup setup;
            double course = 0;
            if (kind == Kind.Cruise)
            {
                double[] history = WindHarness.History(level, seed, 120, out double drawn);
                int start = WindHarness.StartSecond(history, drawn, seconds);
                if (start < 0) throw new InvalidOperationException("no start for seed " + seed);
                setup = WindHarness.Setup(spec, level, seed, Height, false, true, out double courseDeg, out _, steady ? 0 : start, steady);
                course = courseDeg * DegToRad;
            }
            else
            {
                WindSetup wind = WindSetup.Of(level);
                if (steady && level != WindLevel.Still)
                {
                    var drawn = new WindField(WindSetup.Of(level), new FlightRandom(seed));
                    wind = WindSetup.SteadyFrom(drawn.FromDeg, drawn.MeanSpeed);
                    drawn.Dispose();
                }
                setup = CrosswindScenario.Setup(spec, wind, seed);
                Airframe(spec, setup);
                setup.StartPosition = new Double3(0, Height, 0);
            }
            edit?.Invoke(setup);

            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            long release = core.State.Step;
            HarnessPilot harness = null;
            SimPilot hover = null;
            if (kind == Kind.Cruise) harness = new HarnessPilot(core.Controller) { Course = course, Height = Height, Split = split };
            else hover = new SimPilot(core.Controller, setup.StartPosition) { PositionIntegral = 0.1 };

            int steps = (int)(seconds * 1000), from = (int)(Discard * 1000), n = steps - from, frame = 0;
            var run = new Run
            {
                Kind = kind, Level = level, Seed = seed,
                Roll = new double[n], Pitch = new double[n], Heading = new double[n], Rate = new Double3[n],
            };
            double lastHeading = 0, turns = 0;
            for (int k = 0; k < steps; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (harness != null && k * Fps >= frame * 1000.0)
                {
                    harness.See(HarnessPilot.Take(s, k, course));
                    frame++;
                }
                if (k >= from)
                {
                    int j = k - from;
                    Quat camera = s.Attitude;
                    Double3 rate = s.BodyRate;
                    if (core.Modes != null)
                    {
                        // The frame's elastic turn at the lens: FrameRate is linear in the modes, so at their coordinates it
                        // is the turn (rad), at their rates the turn's rate.
                        Double3 turn = core.Modes.FrameRate(s.ModeShape);
                        camera = (s.Attitude * new Quat(turn.X / 2, turn.Y / 2, turn.Z / 2, 1)).Normalized();
                        rate = rate + core.Modes.FrameRate(s.ModeRate);
                    }
                    TrackerAngles.Of(camera, out double roll, out double pitch, out double heading);
                    if (j > 0)
                    {
                        double step = heading - lastHeading;
                        if (step > Math.PI) turns -= TwoPi;
                        else if (step < -Math.PI) turns += TwoPi;
                    }
                    lastHeading = heading;
                    run.Roll[j] = roll;
                    run.Pitch[j] = pitch;
                    run.Heading[j] = heading + turns;
                    run.Rate[j] = rate;
                    Double3 air = s.Velocity - core.Wind.At(s.Position);
                    run.MeanAirspeed += air.Length() / n;
                    run.MeanWind += core.Wind.At(s.Position).Length() / n;
                    double hz = 0;
                    for (int i = 0; i < 4; i++) hz += s.RotorSpeed[i] / TwoPi / 4;
                    run.MeanRotorHz += hz / n;
                }
                long us = (release + k) * FlightCore.StepUs;
                core.Advance(harness != null ? harness.Sticks(k, us) : hover.Sticks(core, us));
            }
            core.Wind.Dispose();
            return run;
        }

        /// <summary>A flight's bands, frames and spreads.</summary>
        public static Bands Analyse(Run run)
        {
            var b = new Bands();
            double[][] axes = { run.Roll, run.Pitch, run.Heading };
            var res = new double[3][];
            for (int a = 0; a < 3; a++)
            {
                double[] deg = Scale(axes[a], RadToDeg);
                b.True[a] = BandRms(Welch(deg, 1000, 8192, out double df), df, Edges.Length - 1);
                double[] frames = Frames(deg, false), picked = Frames(deg, true);
                b.Frame[a] = BandRms(Welch(frames, Fps, 128, out double dff), dff, FrameBands);
                b.Picked[a] = BandRms(Welch(picked, Fps, 128, out double dfp), dfp, FrameBands);
                res[a] = Residual(frames, Fps);
                b.Spread[a] = Std(res[a]);
            }
            double meanPitch = 0;
            foreach (double x in run.Pitch) meanPitch += x / run.Pitch.Length;
            double k = -Math.Sin(meanPitch), cre = 0, cee = 0;
            for (int i = 0; i < res[0].Length; i++)
            {
                double e = res[0][i] + k * res[2][i];
                cre += res[0][i] * e;
                cee += e * e;
            }
            b.RollShare = cre / cee;
            b.Above5 = Above5(run.Rate);
            return b;
        }

        static double[] Scale(double[] x, double k)
        {
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] * k;
            return y;
        }

        /// <summary>
        /// A 1 kHz series sampled as the camera's frames: at frame f, the step at or after f/29.917 s (the harness's clock);
        /// or, picked, at the newest field whose last line was read by then, at its horizon's row (the field's start plus
        /// (25 + 60)·64 µs: the horizon in the picture's top fifth, wind.md §1.1; PAL timing, published), as a recorder
        /// that shows the newest whole field at its tick does (video-feed.md §0; assumed).
        /// </summary>
        public static double[] Frames(double[] x, bool picked)
        {
            int n = (int)((x.Length - 1) * Fps / 1000.0) - 1;
            var y = new double[n];
            for (int f = 0; f < n; f++)
            {
                double t = (f + 1) / Fps;
                int step;
                if (picked)
                {
                    long field = (long)(t / 0.02) - 1;
                    step = (int)(field * 20 + 5.44);
                }
                else step = (int)Math.Ceiling(t * 1000 - 1e-9);
                y[f] = x[Math.Min(Math.Max(step, 0), x.Length - 1)];
            }
            return y;
        }

        /// <summary>Welch's one-sided PSD (unit²/Hz): Hann windows of n, half overlap, each detrended linearly (wind_stats.welch).</summary>
        public static double[] Welch(double[] x, double fs, int n, out double df)
        {
            var win = new double[n];
            double norm = 0;
            for (int k = 0; k < n; k++)
            {
                win[k] = 0.5 - 0.5 * Math.Cos(TwoPi * k / (n - 1));
                norm += win[k] * win[k];
            }
            norm *= fs;
            var psd = new double[n / 2 + 1];
            var re = new double[n];
            var im = new double[n];
            int count = 0;
            double tm = (n - 1) / 2.0, stt = 0;
            for (int k = 0; k < n; k++) stt += (k - tm) * (k - tm);
            for (int s = 0; s + n <= x.Length; s += n / 2)
            {
                double mean = 0, sty = 0;
                for (int k = 0; k < n; k++) mean += x[s + k];
                mean /= n;
                for (int k = 0; k < n; k++) sty += (k - tm) * x[s + k];
                double slope = sty / stt;
                for (int k = 0; k < n; k++)
                {
                    re[k] = (x[s + k] - mean - slope * (k - tm)) * win[k];
                    im[k] = 0;
                }
                Fft(re, im);
                for (int k = 0; k <= n / 2; k++)
                {
                    double p = (re[k] * re[k] + im[k] * im[k]) / norm;
                    psd[k] += k == 0 || k == n / 2 ? p : 2 * p;
                }
                count++;
            }
            for (int k = 0; k < psd.Length; k++) psd[k] /= Math.Max(count, 1);
            df = fs / n;
            return psd;
        }

        /// <summary>RMS over each band [Edges[i], Edges[i + 1]) of a PSD, for the first `bands` bands.</summary>
        public static double[] BandRms(double[] psd, double df, int bands)
        {
            var r = new double[bands];
            for (int i = 0; i < bands; i++)
            {
                double s = 0;
                for (int k = 0; k < psd.Length; k++)
                {
                    double f = k * df;
                    if (f >= Edges[i] && f < Edges[i + 1]) s += psd[k];
                }
                r[i] = Math.Sqrt(s * df);
            }
            return r;
        }

        /// <summary>The spread about wind_stats.py's trend (Residual's std).</summary>
        public static double ResidualStd(double[] frames, double fps) => Std(Residual(frames, fps));

        static double Std(double[] r)
        {
            double sum = 0, sum2 = 0;
            foreach (double x in r)
            {
                sum += x;
                sum2 += x * x;
            }
            double m = sum / r.Length;
            return Math.Sqrt(Math.Max(0, sum2 / r.Length - m * m));
        }

        /// <summary>The residual about wind_stats.py's trend: the frames less their σ = 0.6 s Gaussian mean, where its ±3σ
        /// window is whole (inside a run the local-linear trend is that mean).</summary>
        public static double[] Residual(double[] frames, double fps)
        {
            double sigma = 0.6 * fps;
            int h = (int)(3 * sigma);
            var w = new double[2 * h + 1];
            double sw = 0;
            for (int t = -h; t <= h; t++) sw += w[t + h] = Math.Exp(-0.5 * (t / sigma) * (t / sigma));
            var r = new double[Math.Max(0, frames.Length - 2 * h)];
            for (int i = h; i < frames.Length - h; i++)
            {
                double trend = 0;
                for (int t = -h; t <= h; t++) trend += w[t + h] * frames[i + t];
                r[i - h] = frames[i] - trend / sw;
            }
            return r;
        }

        // CameraFeelProbe's figure (p7e-camera-feel 1.1): the camera's rate less its first-order 5 Hz low-pass, integrated
        // through the same filter, RMS over the three axes, degrees.
        static double Above5(Double3[] rate)
        {
            double cut = 1 - TwoPi * 5 * FlightCore.Dt, sum = 0;
            Double3 low = default, ang = default;
            for (int k = 0; k < rate.Length; k++)
            {
                low = low * cut + rate[k] * (1 - cut);
                ang = ang * cut + (rate[k] - low) * FlightCore.Dt;
                if (k >= 1000) sum += ang.X * ang.X + ang.Y * ang.Y + ang.Z * ang.Z; // after the filter's own start
            }
            return Math.Sqrt(sum / Math.Max(1, rate.Length - 1000)) * RadToDeg;
        }

        // In-place radix-2 FFT (n a power of two).
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
                double ang = -TwoPi / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, c = a + len / 2;
                        double tr = re[c] * cr - im[c] * ci, ti = re[c] * ci + im[c] * cr;
                        re[c] = re[a] - tr;
                        im[c] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                        double nr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = nr;
                    }
                }
            }
        }

        /// <summary>The mean of several flights' analyses, band by band (RMS-averaged: the bands' power is averaged).</summary>
        public static Bands Mean(List<Bands> all)
        {
            var m = new Bands();
            for (int a = 0; a < 3; a++)
            {
                m.True[a] = PowerMean(all, x => x.True[a]);
                m.Frame[a] = PowerMean(all, x => x.Frame[a]);
                m.Picked[a] = PowerMean(all, x => x.Picked[a]);
                double s = 0;
                foreach (Bands x in all) s += x.Spread[a] * x.Spread[a];
                m.Spread[a] = Math.Sqrt(s / all.Count);
            }
            double h = 0;
            foreach (Bands x in all)
            {
                h += x.Above5 * x.Above5;
                m.RollShare += x.RollShare / all.Count;
            }
            m.Above5 = Math.Sqrt(h / all.Count);
            return m;
        }

        static double[] PowerMean(List<Bands> all, Func<Bands, double[]> of)
        {
            var r = new double[of(all[0]).Length];
            foreach (Bands x in all)
                for (int i = 0; i < r.Length; i++) r[i] += of(x)[i] * of(x)[i] / all.Count;
            for (int i = 0; i < r.Length; i++) r[i] = Math.Sqrt(r[i]);
            return r;
        }

        /// <summary>One row of the probe's table: the frame bands above 4 Hz (T6d's), the true bands above 4 Hz and above
        /// 15 Hz, the spreads, p7e's figure; roll, pitch, heading.</summary>
        public static string Row(string name, Bands b)
        {
            var s = new StringBuilder(name.PadRight(30));
            string[] axis = { "R", "P", "Y" };
            for (int a = 0; a < 3; a++)
                s.Append(' ').Append(axis[a]).Append(" fr>4 ").Append(F(Bands.Over(b.Frame[a], 4, 6)))
                    .Append(" pk>4 ").Append(F(Bands.Over(b.Picked[a], 4, 6)))
                    .Append(" tr4-15 ").Append(F(Bands.Over(b.True[a], 4, 6)))
                    .Append(" tr>15 ").Append(F(Bands.Over(b.True[a], 6, 9)))
                    .Append(" 1-2 ").Append(F(b.Frame[a][2]))
                    .Append(" 2-4 ").Append(F(b.Frame[a][3]))
                    .Append(" sd ").Append(F(b.Spread[a])).Append(" |");
            s.Append(" >5Hz(p7e) ").Append(F(b.Above5)).Append(" roll share ").Append(b.RollShare.ToString("0.00", CultureInfo.InvariantCulture));
            return s.ToString();
        }

        static string F(double x) => x.ToString(x < 0.001 ? "0.0E+0" : "0.0000", CultureInfo.InvariantCulture);
    }

    /// <summary>The probe's checks (p7-micro-motion) and its tables (Explicit: they print the note's numbers).</summary>
    public class MicroMotionProbe
    {
        const double RadToDeg = 57.29577951308232, TwoPi = 6.283185307179586;

        // The measurement round-trips: sinusoids land in their bands at their RMS, a 100 Hz one folds into the frames' 8–15 Hz
        // band (|100 − 3·29.917| = 10.2 Hz), and the known-wrong 0.15° of white noise on the pose reads as clip P's floor.
        [Test]
        public void TheMeasurementRoundTrips()
        {
            int n = 35000;
            var x = new double[n];
            double[] hz = { 0.7, 3, 6, 11, 100 };
            for (int k = 0; k < n; k++)
                foreach (double f in hz) x[k] += 0.1 * Math.Sin(TwoPi * f * k / 1000.0 + f);
            double[] tr = MicroMotion.BandRms(MicroMotion.Welch(x, 1000, 8192, out double df), df, MicroMotion.Edges.Length - 1);
            Assert.That(tr[1], Is.EqualTo(0.0707).Within(0.004), "0.7 Hz in 0.5–1");
            Assert.That(tr[3], Is.EqualTo(0.0707).Within(0.004), "3 Hz in 2–4");
            Assert.That(tr[4], Is.EqualTo(0.0707).Within(0.004), "6 Hz in 4–8");
            Assert.That(tr[5], Is.EqualTo(0.0707).Within(0.004), "11 Hz in 8–15");
            Assert.That(tr[7], Is.EqualTo(0.0707).Within(0.004), "100 Hz in 50–150");
            double[] frames = MicroMotion.Frames(x, false);
            double[] fr = MicroMotion.BandRms(MicroMotion.Welch(frames, MicroMotion.Fps, 128, out double dff), dff, MicroMotion.FrameBands);
            Assert.That(fr[5], Is.EqualTo(0.1).Within(0.01), "11 Hz and the folded 100 Hz in the frames' 8–15");
            var six = new double[n];
            for (int k = 0; k < n; k++) six[k] = 0.1 * Math.Sin(TwoPi * 6 * k / 1000.0);
            Assert.That(MicroMotion.ResidualStd(MicroMotion.Frames(six, false), MicroMotion.Fps), Is.EqualTo(0.0707).Within(0.003),
                "the 0.6 s trend passes 6 Hz whole");

            // The known-wrong: white noise of 0.15° a frame (the harness's T6d wrong model) reads about P's roll floor.
            var rng = new Random(1);
            var noise = new double[1200];
            for (int i = 0; i < noise.Length; i++)
            {
                double u = 0;
                for (int j = 0; j < 12; j++) u += rng.NextDouble();
                noise[i] = 0.15 * (u - 6);
            }
            double[] nb = MicroMotion.BandRms(MicroMotion.Welch(noise, MicroMotion.Fps, 128, out double dfn), dfn, MicroMotion.FrameBands);
            Assert.That(MicroMotion.Bands.Over(nb, 4, 6), Is.EqualTo(0.15 * Math.Sqrt(11 / 14.96)).Within(0.015));
        }

        // The turbulence is the camera's floor above 4 Hz in clip P's cruise, and the props' G 6.3 unbalance is not: with the
        // known-wrong fixed vector (no turbulence) the frames' 4–15 Hz roll falls several times; with the asymmetry off it
        // moves by less than a thousandth of a degree. Severe, seed 1.
        [Test]
        public void TurbulenceIsTheCruiseFloorAndTheUnbalanceIsNot()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MicroMotion.Bands built = MicroMotion.Analyse(MicroMotion.Fly(spec, MicroMotion.Kind.Cruise, WindLevel.Severe, 1, seconds: 25));
            MicroMotion.Bands steady = MicroMotion.Analyse(MicroMotion.Fly(spec, MicroMotion.Kind.Cruise, WindLevel.Severe, 1, steady: true, seconds: 25));
            MicroMotion.Bands even = MicroMotion.Analyse(MicroMotion.Fly(spec, MicroMotion.Kind.Cruise, WindLevel.Severe, 1,
                s => s.Asymmetry = null, seconds: 25));
            double b = MicroMotion.Bands.Over(built.Frame[0], 4, 6), w = MicroMotion.Bands.Over(steady.Frame[0], 4, 6);
            double e = MicroMotion.Bands.Over(even.Frame[0], 4, 6);
            TestContext.WriteLine($"roll 4–15 Hz at the frames: built {b:F4}°, fixed vector {w:F4}°, no asymmetry {e:F4}°");
            Assert.That(b, Is.GreaterThan(3 * w), "the turbulence carries the floor above 4 Hz");
            Assert.That(Math.Abs(b - e), Is.LessThan(1e-3), "the G 6.3 unbalance does not move the camera's angle measurably");
        }

        // The note's tables (hover-micro-motion.md §2–§4). Explicit: about 2 s a flight.
        [Test, Explicit]
        public void Levels()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (MicroMotion.Kind kind in new[] { MicroMotion.Kind.Hover, MicroMotion.Kind.Cruise })
                foreach (WindLevel level in new[] { WindLevel.Still, WindLevel.Calm, WindLevel.Windy, WindLevel.Severe })
                {
                    if (kind == MicroMotion.Kind.Cruise && level == WindLevel.Still) continue; // the harness needs a level's course
                    var all = new List<MicroMotion.Bands>();
                    double air = 0, rotor = 0;
                    for (ulong seed = 1; seed <= 5; seed++)
                    {
                        MicroMotion.Run run = MicroMotion.Fly(spec, kind, level, seed);
                        all.Add(MicroMotion.Analyse(run));
                        air += run.MeanAirspeed / 5;
                        rotor += run.MeanRotorHz / 5;
                    }
                    TestContext.WriteLine(MicroMotion.Row($"{kind} {level} ({air:F1} m/s, {rotor:F0} Hz)", MicroMotion.Mean(all)));
                }
        }

        /// <summary>The sources the runtime carries, each switched or scaled against the as-built flight (Severe, seeds 1–3).</summary>
        public static readonly (string Name, Action<FlightSetup> Edit, bool Steady)[] Sources =
        {
            ("as built", null, false),
            ("fixed vector (no turbulence)", null, true),
            ("no asymmetry", s => s.Asymmetry = null, false),
            ("Kv and ESC only (no unbalance)", s => s.Asymmetry.BalanceGrade = 0, false),
            ("unbalance G 16", s => s.Asymmetry.BalanceGrade = 16e-3, false),
            ("unbalance G 40", s => s.Asymmetry.BalanceGrade = 40e-3, false),
            ("no flapping", s => s.Flapping = null, false),
            ("rigid airframe (no modes)", s => s.Modes = null, false),
            ("no vortex ring, no ground", s => { s.VortexRing = null; s.GroundEffect = null; }, false),
            ("H-force 0.25 (p7b 1.7's)", s => s.Propulsion.Props.HForce = 0.25, false),
        };

        [Test, Explicit]
        public void SourcesTable()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (MicroMotion.Kind kind in new[] { MicroMotion.Kind.Hover, MicroMotion.Kind.Cruise })
                foreach (var source in Sources)
                {
                    var all = new List<MicroMotion.Bands>();
                    for (ulong seed = 1; seed <= 3; seed++)
                        all.Add(MicroMotion.Analyse(MicroMotion.Fly(spec, kind, WindLevel.Severe, seed, source.Edit, source.Steady)));
                    TestContext.WriteLine(MicroMotion.Row(kind + " " + source.Name, MicroMotion.Mean(all)));
                }
        }
    }
}
