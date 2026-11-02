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
    /// The camera's micro-motion near the ground and in a low cruise (D-064; docs/reference-notes/ground-micro-motion.md §6),
    /// read from the sim the way motion_bands.py reads the footage:
    /// - **The flight:** the repository's drone with its whole airframe (MicroMotion.Airframe) over the floor world, flown by
    ///   K§8.5's pilot (HarnessPilot: frames at 29.917 Hz, 0.1 s late, its measured τ, its stick noise from `pilot.motor`),
    ///   holding a spot at a camera height (Hover) or clip S's low cruise at a height and an airspeed (Cruise).
    /// - **The frames:** the recorder's 2-2-1 field steps (§2.3): frame k shows field ⌊k·q⌋, q = 50/29.917, at the field's
    ///   middle (assumed: the registration fits the whole picture); each frame's turn is the camera's rate (the rigid body's
    ///   and the frame's elastic turn at the lens) summed between those instants, in its axes (roll about the optical axis,
    ///   pitch, yaw), then retimed to the uniform clock by q over its step, as motion_bands.py's `retime` undoes them.
    /// - **The numbers:** motion_bands.py's per axis: the rate's spread (°/s); the angle's RMS below 1 Hz (0.4–1), at 1–4 Hz
    ///   and at 4–15 Hz from a Welch spectrum of the summed turn (Hann, 64 frames, half overlap, each window less its line);
    ///   the wobble about the 0.6 s trend (wind_stats.residual), its spread and its 1/e time.
    /// The sim's numbers have no tracker noise, parallax or glitches, so they stand beside the footage's *real* bounds.
    /// </summary>
    public static class GroundMicroMotion
    {
        const double TwoPi = 6.283185307179586, RadToDeg = 57.29577951308232, DegToRad = 0.017453292519943295;

        public const double Fps = HarnessPilot.CameraHz;

        /// <summary>motion_bands.BANDS, Hz.</summary>
        public static readonly double[] Lo = { 0.4, 1.0, 4.0 }, Hi = { 1.0, 4.0, 15.5 };

        public const double Discard = 5, Seconds = 40;

        public enum Phase { Hover, Cruise }

        /// <summary>One flight's frame turns (deg, retimed) per axis (roll, pitch, yaw), and its means.</summary>
        public sealed class Run
        {
            public double[][] Turn = new double[3][];
            public double MeanRotorHeightOverR, MeanSpeed, MeanThrottle;
        }

        /// <summary>motion_bands.py's numbers per axis.</summary>
        public sealed class Grade
        {
            public double[] RateSd = new double[3], Wobble = new double[3], Acf = new double[3];
            public double[][] Angle = { new double[3], new double[3], new double[3] };
            public double RotorHeightOverR, Speed;
        }

        /// <summary>
        /// The known-wrong of D-064, a perfectly steady rotor: no own wake and no re-ingested wash, the motors' resistance,
        /// friction and inertia undrawn (their Kv, ESC lag and unbalance as before).
        /// </summary>
        public static void SteadyRotor(FlightSetup s)
        {
            s.RotorWake = null;
            if (s.GroundEffect != null) s.GroundEffect.RecirculationSigma = 0;
            if (s.Asymmetry != null) s.Asymmetry.ResistanceSigma = s.Asymmetry.FrictionSigma = s.Asymmetry.InertiaSigma = 0;
        }

        /// <summary>
        /// Flies one probe flight: a hover with the camera `height` m over the floor, or a cruise at `height` m and `airspeed`
        /// m/s; `remnant` gives the pilot its stick noise; `edit` changes the setup (a source off, the known-wrong).
        /// </summary>
        public static Run Fly(DroneSpec spec, Phase phase, double height, WindLevel level, ulong seed, Action<FlightSetup> edit = null,
            bool remnant = true, double airspeed = 0, double seconds = Seconds, double descent = 0)
        {
            FlightSetup setup = CrosswindScenario.Setup(spec, WindSetup.Of(level), seed);
            MicroMotion.Airframe(spec, setup);
            double origin = height - spec.CameraPosition.Y;
            setup.StartPosition = new Double3(0, origin, 0);
            double pitchDeg = 0;
            if (phase == Phase.Cruise)
            {
                pitchDeg = -CrosswindScenario.CruisePitch(spec, airspeed); // nose down: the tracker's pitch is negative
                setup.StartVelocity = new Double3(0, 0, airspeed);
                DetMath.SinCos(-pitchDeg * DegToRad / 2, out double sp, out double cp);
                setup.StartAttitude = new Quat(sp, 0, 0, cp);
            }
            edit?.Invoke(setup);

            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            long release = core.State.Step;
            var pilot = new HarnessPilot(core.Controller) { Course = 0, Height = origin, Hover = phase == Phase.Hover, PitchDeg = pitchDeg, Learning = false };
            if (remnant) pilot.UseRemnant(seed);

            int steps = (int)(seconds * 1000), from = (int)(Discard * 1000);
            double q = 50 / Fps;
            var turns = new List<double>[3] { new List<double>(), new List<double>(), new List<double>() };
            var run = new Run();
            Double3 sum = default;
            int frame = 0, lastField = -1, n = 0, f = 0;
            while (FrameStep(f, q, out _) < from) f++;
            long next = FrameStep(f, q, out int field);
            double radius = setup.Propulsion.Props.Diameter / 2;
            for (int k = 0; k < steps; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (k == 3000) pilot.Learning = true; // its noise learns from the settled hover or cruise
                if (k >= from) pilot.Height = origin - descent * (k - from) * FlightCore.Dt; // a glide down (A), from the measured start
                if (k * Fps >= frame * 1000.0)
                {
                    pilot.See(HarnessPilot.Take(s, k, 0));
                    frame++;
                }
                if (k == next)
                {
                    // A frame's instant: the turn since the last one, retimed by q over its field step.
                    if (lastField >= 0)
                    {
                        double scale = q / (field - lastField) * RadToDeg;
                        turns[0].Add(sum.Z * scale); // roll about the optical axis (body z)
                        turns[1].Add(sum.X * scale); // pitch about body x
                        turns[2].Add(sum.Y * scale); // yaw about body y
                    }
                    sum = default;
                    lastField = field;
                    next = FrameStep(++f, q, out field);
                }
                if (k >= from)
                {
                    Double3 rate = s.BodyRate;
                    if (core.Modes != null) rate = rate + core.Modes.FrameRate(s.ModeRate);
                    sum = sum + rate * FlightCore.Dt;
                    double z = 0;
                    for (int i = 0; i < 4; i++) z += core.Trace.GroundDistance[i] / 4;
                    run.MeanRotorHeightOverR += z / radius;
                    run.MeanSpeed += Math.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z);
                    run.MeanThrottle += core.Trace.RotorCommand[0];
                    n++;
                }
                long us = (release + k) * FlightCore.StepUs;
                core.Advance(pilot.Sticks(k, us));
            }
            core.Wind.Dispose();
            for (int a = 0; a < 3; a++) run.Turn[a] = turns[a].ToArray();
            run.MeanRotorHeightOverR /= n;
            run.MeanSpeed /= n;
            run.MeanThrottle /= n;
            return run;
        }

        // Frame f's step: it shows field ⌊f·q⌋, read at the field's middle.
        static long FrameStep(int f, double q, out int field)
        {
            field = (int)Math.Floor(f * q);
            return (long)Math.Round((field * 0.020 + 0.010) * 1000);
        }

        /// <summary>motion_bands.py's numbers for a run.</summary>
        public static Grade Analyse(Run run)
        {
            var g = new Grade { RotorHeightOverR = run.MeanRotorHeightOverR, Speed = run.MeanSpeed };
            for (int a = 0; a < 3; a++)
            {
                double[] d = run.Turn[a];
                double mean = 0, var = 0;
                foreach (double x in d) mean += x * Fps / d.Length;
                foreach (double x in d) var += (x * Fps - mean) * (x * Fps - mean) / d.Length;
                g.RateSd[a] = Math.Sqrt(var);
                var angle = new double[d.Length];
                double acc = 0;
                for (int i = 0; i < d.Length; i++) angle[i] = acc += d[i];
                g.Angle[a] = AngleBands(angle, 64);
                double[] res = MicroMotion.Residual(angle, Fps);
                double rs = 0;
                foreach (double x in res) rs += x * x / res.Length;
                g.Wobble[a] = Math.Sqrt(rs);
                g.Acf[a] = AcfTime(res, Fps);
            }
            return g;
        }

        /// <summary>motion_bands.spectra's angle bands: Welch over Hann windows of n frames, half overlap, each less its line;
        /// the PSD per cycle a frame; each band's RMS over its bins.</summary>
        public static double[] AngleBands(double[] x, int n)
        {
            var win = new double[n];
            double norm = 0;
            for (int i = 0; i < n; i++) norm += (win[i] = 0.5 - 0.5 * Math.Cos(TwoPi * i / n)) * win[i];
            var psd = new double[n / 2 + 1];
            int count = 0;
            var re = new double[n];
            var im = new double[n];
            for (int s = 0; s + n <= x.Length; s += n / 2)
            {
                // Less the window's least-squares line.
                double mt = (n - 1) / 2.0, mx = 0, sxt = 0, stt = 0;
                for (int i = 0; i < n; i++) mx += x[s + i] / n;
                for (int i = 0; i < n; i++)
                {
                    sxt += (i - mt) * (x[s + i] - mx);
                    stt += (i - mt) * (i - mt);
                }
                double slope = sxt / stt;
                for (int k = 0; k <= n / 2; k++)
                {
                    double r = 0, m = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double v = (x[s + i] - mx - slope * (i - mt)) * win[i];
                        r += v * Math.Cos(TwoPi * k * i / n);
                        m -= v * Math.Sin(TwoPi * k * i / n);
                    }
                    double p = (r * r + m * m) / norm;
                    if (k > 0 && k < n / 2) p *= 2;
                    psd[k] += p;
                }
                count++;
            }
            var bands = new double[Lo.Length];
            double df = Fps / n;
            for (int b = 0; b < Lo.Length; b++)
            {
                double sum = 0;
                for (int k = 1; k <= n / 2; k++)
                    if (Lo[b] <= k * df && k * df < Hi[b]) sum += psd[k] / count / n;
                bands[b] = Math.Sqrt(sum);
            }
            return bands;
        }

        /// <summary>wind_stats.acf_time: the lag where the autocorrelation first falls below 1/e, interpolated, s.</summary>
        public static double AcfTime(double[] r, double fps)
        {
            double c0 = 0;
            foreach (double x in r) c0 += x * x;
            double prev = 1;
            for (int lag = 1; lag < r.Length / 2; lag++)
            {
                double c = 0;
                for (int i = 0; i + lag < r.Length; i++) c += r[i] * r[i + lag];
                c /= c0;
                if (c < 1 / Math.E) return (lag - 1 + (prev - 1 / Math.E) / (prev - c)) / fps;
                prev = c;
            }
            return double.NaN;
        }

        /// <summary>The power mean of several grades (the RMS over seeds), the 1/e times averaged.</summary>
        public static Grade Mean(List<Grade> all)
        {
            var m = new Grade();
            foreach (Grade g in all)
            {
                m.RotorHeightOverR += g.RotorHeightOverR / all.Count;
                m.Speed += g.Speed / all.Count;
                for (int a = 0; a < 3; a++)
                {
                    m.RateSd[a] += g.RateSd[a] * g.RateSd[a] / all.Count;
                    m.Wobble[a] += g.Wobble[a] * g.Wobble[a] / all.Count;
                    m.Acf[a] += g.Acf[a] / all.Count;
                    for (int b = 0; b < 3; b++) m.Angle[a][b] += g.Angle[a][b] * g.Angle[a][b] / all.Count;
                }
            }
            for (int a = 0; a < 3; a++)
            {
                m.RateSd[a] = Math.Sqrt(m.RateSd[a]);
                m.Wobble[a] = Math.Sqrt(m.Wobble[a]);
                for (int b = 0; b < 3; b++) m.Angle[a][b] = Math.Sqrt(m.Angle[a][b]);
            }
            return m;
        }

        public static Grade Mean(DroneSpec spec, Phase phase, double height, WindLevel level, ulong first, int seeds,
            Action<FlightSetup> edit = null, bool remnant = true, double airspeed = 0, double seconds = Seconds, double descent = 0)
        {
            var all = new List<Grade>();
            for (ulong seed = first; seed < first + (ulong)seeds; seed++)
                all.Add(Analyse(Fly(spec, phase, height, level, seed, edit, remnant, airspeed, seconds, descent)));
            return Mean(all);
        }

        /// <summary>A grade as the note's rows: per axis the rate spread, the three angle bands, the wobble and its 1/e time.</summary>
        public static string Rows(string name, Grade g)
        {
            var b = new StringBuilder();
            string[] axes = { "roll", "pitch", "yaw" };
            b.Append(name).Append(string.Format(CultureInfo.InvariantCulture, " (rotors {0:F1} R, {1:F1} m/s)", g.RotorHeightOverR, g.Speed));
            for (int a = 0; a < 3; a++)
                b.Append(string.Format(CultureInfo.InvariantCulture, "\n  {0,-5} rate {1:F2} deg/s; angle {2:F4} / {3:F4} / {4:F4} deg; wobble {5:F3} deg, 1/e {6:F2} s",
                    axes[a], g.RateSd[a], g.Angle[a][0], g.Angle[a][1], g.Angle[a][2], g.Wobble[a], g.Acf[a]));
            return b.ToString();
        }
    }

    /// <summary>D-064's probe: the near-ground hover and the low cruise against the footage (Explicit tables, and the checks).</summary>
    public class GroundMicroMotionProbe
    {
        // The own wake against its published form: a rotor held at a hover speed in still air wanders with the thrust σ it was
        // given (Stack et al. 2004's 2.9 %, through the props' static slope, within 10 %), its correlation time the corner's
        // R/(2π·0.2·v_h) (within 15 %), and Glauert's fade; the known-wrong steady rotor does not wander at all.
        [Test]
        public void TheOwnWakeIsThePublishedThrustUnsteadiness()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            RotorWakeSpec wake = spec.BuildRotorWake();
            foreach (bool on in new[] { true, false })
            {
                var p = new Propulsion(spec.BuildPropulsion(), null, null, default, null, default, on ? wake : null, new FlightRandom(7).Open("rotor.wake"));
                var s = new FlightState();
                for (int i = 0; i < 4; i++) s.RotorSpeed[i] = 920;
                int n = 120000;
                var t = new double[n];
                double mean = 0, vh = 0;
                for (int k = 0; k < n; k++)
                {
                    p.BeginStep(s);
                    t[k] = p.Thrust(0);
                    mean += t[k] / n;
                    p.AdvanceWake(ref s, FlightCore.Dt);
                    s.Step++;
                }
                double var = 0;
                foreach (double x in t) var += (x - mean) * (x - mean) / n;
                double sigma = Math.Sqrt(var) / mean;
                vh = Math.Sqrt(mean / (2 * wake.AirDensity * Math.PI * Math.Pow(spec.Props.Diameter / 2, 2)));
                if (!on)
                {
                    Assert.That(sigma, Is.LessThan(1e-9), "a perfectly steady rotor (the mean's rounding aside)");
                    continue;
                }
                double tau = spec.Props.Diameter / 2 / (2 * Math.PI * wake.CornerFrequency * vh), c0 = 0, c1 = 0;
                int lag = (int)Math.Round(tau / FlightCore.Dt);
                for (int k = 0; k + lag < n; k++)
                {
                    c0 += (t[k] - mean) * (t[k] - mean);
                    c1 += (t[k] - mean) * (t[k + lag] - mean);
                }
                TestContext.WriteLine($"thrust {mean:F2} N, v_h {vh:F2} m/s: σ {sigma:P2} of it (given {wake.ThrustSigma:P1}); at τ {tau * 1000:F1} ms the autocorrelation {c1 / c0:F3} (1/e 0.368)");
                Assert.That(sigma, Is.EqualTo(wake.ThrustSigma).Within(0.1 * wake.ThrustSigma));
                Assert.That(c1 / c0, Is.EqualTo(1 / Math.E).Within(0.15 / Math.E));
            }
            Assert.That(RotorWakeSpec.Fade(0), Is.EqualTo(1));
            Assert.That(RotorWakeSpec.Fade(1), Is.EqualTo(Math.Sqrt((Math.Sqrt(5) - 1) / 2)).Within(1e-15), "Glauert's v_i/v_h at μ 1");
            Assert.That(Math.Pow(RotorWakeSpec.Fade(3), 4) + 9 * Math.Pow(RotorWakeSpec.Fade(3), 2), Is.EqualTo(1).Within(1e-12));
        }

        // D-064's grade where it holds (the note's table has the rest): the near-ground hover is never still with the sources
        // on, and the known-wrong steady rotor is, below every clip's 4–15 Hz and rate; and lower is livelier, the 1–4 Hz roll
        // and pitch at 0.3 m (the rotors 2.8 R up, in the re-ingested wash) at least 1.2× those at 1.25 m. Still air, seeds 1–3.
        [Test]
        public void NearTheGroundTheHoverIsNeverStill()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            GroundMicroMotion.Grade high = GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, 1.25, WindLevel.Still, 1, 3);
            GroundMicroMotion.Grade low = GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, 0.3, WindLevel.Still, 1, 3);
            GroundMicroMotion.Grade still = GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, 1.25, WindLevel.Still, 1, 3, GroundMicroMotion.SteadyRotor);
            TestContext.WriteLine(GroundMicroMotion.Rows("1.25 m", high));
            TestContext.WriteLine(GroundMicroMotion.Rows("0.3 m", low));
            TestContext.WriteLine(GroundMicroMotion.Rows("1.25 m, steady rotor", still));
            for (int a = 0; a < 2; a++)
            {
                Assert.That(high.RateSd[a], Is.GreaterThan(2.0), "the rate, as C, D and E's 2.3–6.2 °/s");
                Assert.That(high.Angle[a][2], Is.GreaterThan(0.006), "above 4 Hz, as every clip's lowest");
                Assert.That(still.RateSd[a], Is.LessThan(0.1), "the steady rotor is still");
                Assert.That(still.Angle[a][2], Is.LessThan(0.001), "the steady rotor is still above 4 Hz");
                Assert.That(low.Angle[a][1], Is.GreaterThan(1.2 * high.Angle[a][1]), "lower is livelier at 1–4 Hz");
            }
        }

        // The note's grade (Explicit: about 2 s a flight).
        [Test, Explicit]
        public void Table()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (WindLevel level in new[] { WindLevel.Still, WindLevel.Calm })
                foreach (double h in new[] { 1.25, 0.75, 0.45, 0.3 })
                {
                    TestContext.WriteLine(GroundMicroMotion.Rows($"hover {h} m, {level}, as built", GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, h, level, 1, 3)));
                    TestContext.WriteLine(GroundMicroMotion.Rows($"hover {h} m, {level}, steady rotor", GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, h, level, 1, 3, GroundMicroMotion.SteadyRotor)));
                }
            // A's last metres: a glide down at 0.5 m/s from 6.5 m to 3.5 m at 8 m/s (about 4°, inside A's 2–6°), over 6 s.
            TestContext.WriteLine(GroundMicroMotion.Rows("descent 6.5 to 3.5 m, 8 m/s, Calm, as built",
                GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Cruise, 6.5, WindLevel.Calm, 1, 3, airspeed: 8, seconds: 11, descent: 0.5)));
            TestContext.WriteLine(GroundMicroMotion.Rows("descent 6.5 to 3.5 m, 8 m/s, Calm, steady rotor",
                GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Cruise, 6.5, WindLevel.Calm, 1, 3, GroundMicroMotion.SteadyRotor, airspeed: 8, seconds: 11, descent: 0.5)));
            foreach (WindLevel level in new[] { WindLevel.Calm, WindLevel.Windy })
            {
                TestContext.WriteLine(GroundMicroMotion.Rows($"cruise 7.5 m 11 m/s, {level}, as built", GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Cruise, 7.5, level, 1, 3, airspeed: 11)));
                TestContext.WriteLine(GroundMicroMotion.Rows($"cruise 7.5 m 11 m/s, {level}, steady rotor", GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Cruise, 7.5, level, 1, 3, GroundMicroMotion.SteadyRotor, airspeed: 11)));
            }
        }

        /// <summary>Each source switched against the as-built hover at 1.25 m in still air (seeds 1–3).</summary>
        public static readonly (string Name, Action<FlightSetup> Edit, bool Remnant)[] Sources =
        {
            ("as built", null, true),
            ("noiseless pilot", null, false),
            ("steady rotor (known-wrong)", GroundMicroMotion.SteadyRotor, true),
            ("own wake only", s => { s.GroundEffect.RecirculationSigma = 0; s.Asymmetry.ResistanceSigma = s.Asymmetry.FrictionSigma = s.Asymmetry.InertiaSigma = 0; }, true),
            ("motors' dynamics only", s => { s.RotorWake = null; s.GroundEffect.RecirculationSigma = 0; }, true),
            ("own wake's corner 0.1 (sensitivity)", s => s.RotorWake.CornerFrequency = 0.1, true),
            ("own wake's corner 0.05 (sensitivity)", s => s.RotorWake.CornerFrequency = 0.05, true),
        };

        [Test, Explicit]
        public void SourcesTable()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (var source in Sources)
                TestContext.WriteLine(GroundMicroMotion.Rows("hover 1.25 m, Still, " + source.Name,
                    GroundMicroMotion.Mean(spec, GroundMicroMotion.Phase.Hover, 1.25, WindLevel.Still, 1, 3, source.Edit, source.Remnant)));
        }
    }
}
