using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pitch gap's budget (p7c-wind 1.5, the physics ledger's gap 3): at clip P's cruise trim (the harness pilot holding
    /// 22.7° nose down in still air), each mechanism's pitching moment about the CoM per unit of a uniform gust or of a gust
    /// gradient, open loop, at the rotors' trimmed speeds (the controller has not answered yet). Explicit: it prints the
    /// design's table.
    /// </summary>
    public sealed class WindLevelsProbe
    {
        const double DegToRad = 0.017453292519943295;

        // The trim: the harness pilot holds clip P's pitch on a course north at 35 m in still air for 20 s.
        static FlightCore Trim(DroneSpec spec, out FlightSetup setup)
        {
            setup = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still));
            setup.VortexRing = spec.BuildVortexRing();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.StartPosition = new Double3(0, 35, 0);
            setup.StartVelocity = new Double3(0, 0, WindHarness.ReleaseSpeed);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(sp, 0, 0, cp);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var p = new HarnessPilot(core.Controller) { Course = 0, Height = 35 };
            long release = core.State.Step;
            int frame = 0;
            for (int k = 0; k < 20000; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0) { p.See(HarnessPilot.Take(core.State, k, 0)); frame++; }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
            }
            return core;
        }

        /// <summary>The pitching moment's parts about body +x (N·m) at the state for a wind field (world m/s at a world
        /// point): the rotors' thrust about the CoM, their hub moments, their H-forces, the body's drag, the coil's, the hull's
        /// moment (p7c-wind design 12).</summary>
        public static double[] Parts(FlightCore core, FlightSetup setup, FlightState s, Func<Double3, Double3> wind)
        {
            Propulsion pr = core.Propulsion;
            Double3 com = core.Body.CenterOfMass;
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = wind(s.Position + s.Attitude.Rotate(pr.Hub(i)));
            pr.BeginStep(s, hubs);
            double thrust = 0, hub = 0, h = 0;
            for (int i = 0; i < 4; i++)
            {
                Double3 r = pr.Hub(i) - com;
                double t = pr.Thrust(i), w = s.RotorSpeed[i];
                thrust -= r.Z * t;
                h += Double3.Cross(r, pr.HForce(i, w)).X;
                hub += pr.HubMoment(i, w, t).X;
            }
            Double3 v = s.Attitude.InverseRotate(s.Velocity);
            Double3 Air(Double3 point) => v + Double3.Cross(s.BodyRate, point) - s.Attitude.InverseRotate(wind(s.Position + s.Attitude.Rotate(point)));
            Double3 cpt = core.Aero.Spec.BodyCenterOfPressure;
            double body = Double3.Cross(cpt - com, core.Aero.Body(Air(cpt))).X;
            ref readonly DronePart coil = ref setup.Mass.Part(core.Aero.Spec.CoilPart);
            double coilM = Double3.Cross(coil.Position - com, core.Aero.Coil(Air(coil.Position), coil)).X;
            double hull = new Aero(GoldenFlights.WithTheHull(core.Aero.Spec)).Hull(Air(cpt)).X; // off in flight (D-107): what it would add
            return new[] { thrust, hub, h, body, coilM, hull };
        }

        [Test, Explicit]
        public void TheBudgetAtTheCruise()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightCore core = Trim(spec, out FlightSetup setup);
            FlightState s = core.State;
            TrackerAngles.Of(s.Attitude, out _, out double pitch, out _);
            Double3 com = core.Body.CenterOfMass;
            TestContext.WriteLine($"trim: {s.Velocity.Length():F2} m/s, pitch {pitch / DegToRad:F2}°, CoM {com.X:F4} {com.Y:F4} {com.Z:F4}, Ixx {core.Body.Inertia.XX:F4}");
            for (int i = 0; i < 4; i++)
            {
                Double3 r = core.Propulsion.Hub(i) - com;
                TestContext.WriteLine($"  hub {i}: r {r.X:F3} {r.Y:F3} {r.Z:F3}, {s.RotorSpeed[i]:F0} rad/s");
            }
            string[] names = { "thrust", "hub", "H-force", "body drag", "coil drag", "hull" };
            Double3 at = s.Position;
            Double3 forward = s.Attitude.Rotate(new Double3(0, 0, 1));
            forward = new Double3(forward.X, 0, forward.Z) * (1 / Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z));
            void Row(string label, Func<double, Func<Double3, Double3>> field, double d)
            {
                double[] p = Parts(core, setup, s, field(d)), m = Parts(core, setup, s, field(-d));
                string line = label + ":";
                double sum = 0;
                for (int k = 0; k < p.Length; k++)
                {
                    double g = (p[k] - m[k]) / (2 * d);
                    sum += g;
                    line += $" {names[k]} {g:+0.0000;-0.0000}";
                }
                TestContext.WriteLine(line + $"; sum {sum:+0.0000;-0.0000} N·m per unit");
            }
            Row("updraft w (per m/s)", d => x => new Double3(0, d, 0), 0.5);
            Row("headwind u (per m/s)", d => x => forward * -d, 0.5);
            Row("updraft growing forward dw/dx (per 1/s)", d => x => new Double3(0, d * Double3.Dot(x - at, forward), 0), 0.5);
            Row("headwind growing upward du/dz (per 1/s)", d => x => forward * (-d * (x.Y - at.Y)), 0.5);
            Row("headwind growing forward du/dx (per 1/s)", d => x => forward * (-d * Double3.Dot(x - at, forward)), 0.5);
        }

        /// <summary>
        /// The field's gusts off the forecast's direction (D-086, P33): along the harness's course (the drawn wind abeam from
        /// the left, at the cruise's ground speed) at 35 m over the open field, and held at 0.75 H in the middle of the harness's wooded strip,
        /// Severe, 40 seeds × 10 min sampled at 10 Hz: the share of time the horizontal wind at the drone blows from more than
        /// 45° and 90° off the drawn direction, the gusts (runs of 0.2–10 s) beyond 45°, per minute, and the share of time
        /// the crosswind is from the right (reversed). Explicit.
        /// </summary>
        [Test, Explicit]
        public void GustsOffTheForecast()
        {
            foreach (bool canopy in new[] { false, true })
            {
                double over45 = 0, over90 = 0, reversed = 0, gusts = 0, samples = 0, minutes = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var drawn = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
                    double from = drawn.FromDeg, course = (from + 90) % 360;
                    drawn.Dispose();
                    WindSetup setup = WindSetup.Of(WindLevel.Severe);
                    CanopyStrip strip = WindHarness.Strip(course);
                    if (canopy) setup.Canopies = new[] { strip };
                    var wind = new WindField(setup, new FlightRandom(seed));
                    DetMath.SinCos(course * DegToRad, out double sin, out double cos);
                    DetMath.SinCos(from * DegToRad, out double fs, out double fc);
                    double height = canopy ? 0.75 * WindHarness.StripHeight : 35, run = 0;
                    for (long step = 0; step < 600_000; step += 100)
                    {
                        // Over the field the drone flies the course; in the strip it holds its middle (the eddies blow past).
                        double x = WindHarness.GroundSpeed * step * 0.001;
                        Double3 p = canopy ? new Double3(strip.CentreX, height, strip.CentreZ) : new Double3(sin * x, height, cos * x);
                        wind.Begin(step, p, 0);
                        Double3 w = wind.At(p, 0);
                        // The wind blows toward −(sin, cos) of the bearing it comes from; its own bearing from: atan2 of −w.
                        double speed = Math.Sqrt(w.X * w.X + w.Z * w.Z);
                        double off = speed > 0 ? Math.Acos(Math.Max(-1, Math.Min(1, -(w.X * fs + w.Z * fc) / speed))) / DegToRad : 0;
                        samples++;
                        if (off > 45) { over45++; run++; }
                        else { if (run >= 2 && run <= 100) gusts++; run = 0; }
                        if (off > 90) over90++;
                        // The crosswind from the left of the course: the wind toward the course's right, (cos, −sin).
                        if (w.X * cos - w.Z * sin < 0) reversed++;
                    }
                    minutes += 10;
                    wind.Dispose();
                }
                TestContext.WriteLine($"{(canopy ? "in the strip at 0.75 H" : "open field at 35 m")}: off > 45° {100 * over45 / samples:F2} % of the time, > 90° {100 * over90 / samples:F3} %, "
                    + $"gusts beyond 45° (0.2–10 s) {gusts / minutes:F2} per min, the crosswind reversed {100 * reversed / samples:F3} %");
            }
        }

        /// <summary>
        /// The closed loop's pitch answer to the air at the cruise (p7c-wind 1.5c, design 14's hand calculation): the
        /// harness's whole airframe flown by the noiseless human pilot at clip P's trim (35 m, the course north, still air),
        /// and at 20 s a uniform step of 0.5 m/s in one component of the air (a GustFront evaluated at the body's origin:
        /// updraft, headwind, or from the left); the step's pitch less the same flight's without it (the same seed, so the
        /// rotors' own wake cancels), and its transfer |H(f)| = |jω·∫Δθ(t)·e^(−jωt) dt| per m/s at the band's frequencies,
        /// the last 5 s Hann-tapered. Explicit: it prints the table design 14 reads.
        /// </summary>
        [Test, Explicit]
        public void TheGustTransfer()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const double step = 20, after = 20, size = 0.5;
            double[] f = { 0.23, 0.3, 0.4, 0.5, 0.7, 1.0, 1.5, 2.0, 3.0, 4.0 };
            double[] baseline = StepPitch(spec, default, step, after);
            foreach (var (name, delta) in new[] { ("updraft", new Double3(0, size, 0)), ("headwind", new Double3(0, 0, -size)), ("from the left", new Double3(size, 0, 0)) })
            {
                double[] with = StepPitch(spec, delta, step, after);
                int n = with.Length, taper = 5000;
                double peak = 0, at = 0, line = 0;
                var d = new double[n];
                for (int k = 0; k < n; k++)
                {
                    d[k] = (with[k] - baseline[k]) / size;
                    if (Math.Abs(d[k]) > Math.Abs(peak)) { peak = d[k]; at = k * 0.001; }
                    if (k >= n - taper) d[k] *= 0.5 + 0.5 * Math.Cos(Math.PI * (k - (n - taper)) / taper);
                }
                for (int k = n - 1000; k < n; k++) line += (with[k] - baseline[k]) / size / 1000;
                string row = $"{name}: step response peak {peak:+0.0000;-0.0000}° per m/s at +{at:F3} s, last 1 s {line:+0.0000;-0.0000}°; |H| °/(m/s):";
                foreach (double hz in f)
                {
                    double w = 2 * Math.PI * hz, re = 0, im = 0;
                    for (int k = 0; k < n; k++)
                    {
                        re += d[k] * Math.Cos(w * k * 0.001) * 0.001;
                        im -= d[k] * Math.Sin(w * k * 0.001) * 0.001;
                    }
                    row += $" {hz:G3} Hz {w * Math.Sqrt(re * re + im * im):F4}";
                }
                TestContext.WriteLine(row);
            }
        }

        /// <summary>
        /// The field's air in the drone's bands (p7c-wind 1.5c, design 14): Severe over the open field, along the harness's
        /// course (the drawn wind abeam from the left, at the cruise's ground speed, so the drone meets the frozen field at
        /// about the cruise's 20 m/s through the air) at each height, 20 seeds × 120 s sampled at the camera's 29.917 Hz:
        /// the RMS of the updraft w, of the headwind along the course and of the crosswind, and of the updraft's difference
        /// between two points 0.32 m apart along the course (the front and rear hubs), in the bands 0.23–0.5, 0.5–1, 1–2 and
        /// 2–4 Hz (Welch, Hann windows of 128 samples, half overlap, each less its line, as wind_stats.py's spectra), and
        /// Kaimal et al. (1972)'s neutral w spectrum in the same bands at that height, from the field's own u* (the log law's
        /// at the drawn strength) and its encounter speed. Explicit.
        /// </summary>
        [Test, Explicit]
        public void TheFieldsVerticalBands()
        {
            double[] lo = { 0.23, 0.5, 1, 2 }, hi = { 0.5, 1, 2, 4 };
            const double fps = HarnessPilot.CameraHz, seconds = 120, spacing = 0.32;
            WindPreset preset = WindPreset.Of(WindLevel.Severe);
            // The open field at each height, then the harness's wooded strip crossed at 0.5 and 0.75 H (its canopy's eddies;
            // Kaimal's row there is the open field's at that height, for scale). p7c-wind design 16: the one-scale field (model
            // 98's, the known-wrong) first, then the field by height, on the same seeds.
            foreach (bool oneScale in new[] { true, false })
            foreach (double height in new[] { 2.0, 5.0, 10.0, 20.0, 35.0, 50.0, -0.5, -0.75 })
            {
                bool strip = height < 0;
                double z = strip ? -height * WindHarness.StripHeight : height;
                var w = new double[4]; var head = new double[4]; var cross = new double[4]; var dw = new double[4]; var kaimal = new double[4];
                double ustar = 0, sigmaW = 0, speed = 0;
                const int seeds = 20;
                for (ulong seed = 1; seed <= seeds; seed++)
                {
                    var drawn = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
                    double from = drawn.FromDeg, course = (from + 90) % 360;
                    drawn.Dispose();
                    WindSetup setup = WindSetup.Of(WindLevel.Severe);
                    setup.OneScale = oneScale;
                    if (strip) setup.Canopies = new[] { WindHarness.Strip(course) };
                    var field = new WindField(setup, new FlightRandom(seed));
                    DetMath.SinCos(course * DegToRad, out double sin, out double cos);
                    var along = new Double3(sin, 0, cos);
                    var right = new Double3(cos, 0, -sin);
                    // In the strip: its crossing a canopy height inside either edge (the layer full), else 120 s of open field.
                    WindHarness.BeltStretch(out double near, out _);
                    double x0 = strip ? near + (WindHarness.ApproachHeights + 1) * WindHarness.StripHeight : 0;
                    double span = strip ? (WindHarness.StripWidthHeights - 2) * WindHarness.StripHeight / WindHarness.GroundSpeed : seconds;
                    int n = (int)(span * fps);
                    double[] sw = new double[n], sh = new double[n], sc = new double[n], sd = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        long step = (long)(i * 1000 / fps);
                        Double3 p = along * (x0 + WindHarness.GroundSpeed * step * 0.001) + new Double3(0, z, 0);
                        field.Begin(step, p, 0);
                        Double3 a = field.At(p, 0), b = field.At(p - along * spacing, 0);
                        sw[i] = a.Y;
                        sh[i] = -Double3.Dot(a, along);
                        sc[i] = Double3.Dot(a, right);
                        sd[i] = a.Y - b.Y;
                    }
                    double[] bw = Bands(sw, fps, lo, hi), bh = Bands(sh, fps, lo, hi), bc = Bands(sc, fps, lo, hi), bd = Bands(sd, fps, lo, hi);
                    // Kaimal's neutral w: nS_w/u*² = 2.1·f/(1 + 5.3·f^(5/3)), f = n·z/V at the drone's encounter speed V.
                    double us = WindPreset.Karman * field.MeanSpeed / Math.Log((preset.ReferenceHeight - preset.Displacement) / preset.Roughness);
                    double profile = Math.Log((z - preset.Displacement) / preset.Roughness) / Math.Log((preset.ReferenceHeight - preset.Displacement) / preset.Roughness);
                    double v = (along * WindHarness.GroundSpeed - field.Mean * profile).Length(); // the mean at the run's end (the last Begin's)
                    for (int k = 0; k < 4; k++)
                    {
                        w[k] += bw[k] * bw[k] / seeds; head[k] += bh[k] * bh[k] / seeds; cross[k] += bc[k] * bc[k] / seeds; dw[k] += bd[k] * bd[k] / seeds;
                        double sum = 0;
                        const int m = 200;
                        for (int j = 0; j < m; j++)
                        {
                            double hz = lo[k] * Math.Exp(Math.Log(hi[k] / lo[k]) * (j + 0.5) / m), f = hz * (z - preset.Displacement) / v;
                            sum += 2.1 * f / (1 + 5.3 * Math.Pow(f, 5.0 / 3)) * Math.Log(hi[k] / lo[k]) / m;
                        }
                        kaimal[k] += sum * us * us / seeds;
                    }
                    ustar += us / seeds;
                    sigmaW += field.SigmaV / seeds;
                    speed += v / seeds;
                    field.Dispose();
                }
                string Row(double[] x) => string.Join(" / ", Array.ConvertAll(x, y => Math.Sqrt(y).ToString("F3")));
                TestContext.WriteLine($"{(oneScale ? "one-scale" : "by height")}, {(strip ? $"in the strip at {-height} H, {z:F1} m" : $"{z} m")} (u* {ustar:F3} m/s, the field's σ_w {sigmaW:F3}, Kaimal's 1.25·u* {1.25 * ustar:F3}; met at {speed:F1} m/s), "
                    + $"RMS in 0.23–0.5 / 0.5–1 / 1–2 / 2–4 Hz, m/s: w {Row(w)}; Kaimal's w {Row(kaimal)}; headwind {Row(head)}; crosswind {Row(cross)}; "
                    + $"w over {spacing} m {Row(dw)}");
            }
        }

        // Band RMS of a series sampled at fps: Welch over Hann windows of 128 samples, half overlap, each less its line.
        internal static double[] Bands(double[] x, double fps, double[] lo, double[] hi)
        {
            const int n = 128;
            var psd = new double[n / 2 + 1];
            double norm = 0;
            var win = new double[n];
            for (int i = 0; i < n; i++) norm += (win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n)) * win[i];
            int count = 0;
            for (int s = 0; s + n <= x.Length; s += n / 2)
            {
                double mt = (n - 1) / 2.0, mx = 0, sxt = 0, stt = 0;
                for (int i = 0; i < n; i++) mx += x[s + i] / n;
                for (int i = 0; i < n; i++) { sxt += (i - mt) * (x[s + i] - mx); stt += (i - mt) * (i - mt); }
                double slope = sxt / stt;
                for (int k = 1; k < n / 2; k++)
                {
                    double re = 0, im = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double v = (x[s + i] - mx - slope * (i - mt)) * win[i];
                        re += v * Math.Cos(2 * Math.PI * k * i / n);
                        im -= v * Math.Sin(2 * Math.PI * k * i / n);
                    }
                    psd[k] += 2 * (re * re + im * im) / norm;
                }
                count++;
            }
            var bands = new double[lo.Length];
            for (int b = 0; b < lo.Length; b++)
            {
                double sum = 0;
                for (int k = 1; k < n / 2; k++)
                    if (lo[b] <= k * fps / n && k * fps / n < hi[b]) sum += psd[k] / count / n;
                bands[b] = Math.Sqrt(sum);
            }
            return bands;
        }

        // The noiseless human's pitch (the tracker's, degrees) at every step from `step` s for `after` s, at clip P's trim in
        // still air with a uniform step of `delta` (world m/s) in the air at `step` s (none when zero).
        static double[] StepPitch(DroneSpec spec, Double3 delta, double step, double after)
        {
            WindSetup wind = WindSetup.Of(WindLevel.Still);
            if (delta.Length() > 0)
                wind.Front = new GustFront { Start = new Double3(-step, 0, 0), Normal = new Double3(1, 0, 0), Speed = 1, Width = 0.002, Delta = delta, Uniform = true };
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, 1);
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.Modes = spec.BuildModes();
            setup.StartPosition = new Double3(0, 35, 0);
            setup.StartVelocity = new Double3(0, 0, WindHarness.ReleaseSpeed);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(sp, 0, 0, cp);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var p = new HarnessPilot(core.Controller) { Course = 0, Height = 35 };
            p.Human();
            long release = core.State.Step;
            int from = (int)(step * 1000) - (int)release, steps = from + (int)(after * 1000), frame = 0;
            var pitch = new double[(int)(after * 1000)];
            for (int k = 0; k < steps; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0) { p.See(HarnessPilot.Take(core.State, k, 0)); frame++; }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
                if (k >= from)
                {
                    TrackerAngles.Of(core.State.Attitude, out _, out double th, out _);
                    pitch[k - from] = th / DegToRad;
                }
            }
            core.Wind.Dispose();
            return pitch;
        }

        // The airframes the attribution flies: the whole one, then each source of pitch taken out in turn.
        static readonly (string Name, bool Steady, Action<FlightSetup> Change)[] Variants =
        {
            ("whole", false, null),
            ("steady", true, null),
            ("noownwake", false, s => s.RotorWake = null),
            ("noasymmetry", false, s => s.Asymmetry = null),
            ("nointerference", false, s => s.Interference = null),
            ("noflapping", false, s => s.Flapping = null),
            ("steady-noownwake", true, s => s.RotorWake = null),
            ("steady-noownwake-noasym", true, s => { s.RotorWake = null; s.Asymmetry = null; }),
            ("hull", false, s => s.Aero = GoldenFlights.WithTheHull(s.Aero)), // p7c-wind design 12, off by default (D-107)
            ("steady-hull", true, s => s.Aero = GoldenFlights.WithTheHull(s.Aero)),
            ("potential", false, s => { s.Aero = GoldenFlights.WithTheHull(s.Aero); s.Aero.HullPotential = true; }), // its known-wrong
        };

        /// <summary>
        /// Where the harness's pitch comes from: Severe's nominal runs on seeds 1–N (PITCH_PROBE_SEEDS, default 5) with each
        /// source taken out, written as the harness's CSVs under PITCH_PROBE_OUT/&lt;variant&gt;/ for
        /// tools/wind/pitch_probe.py. The harness's default pilot flies them, the human (D-109). Explicit.
        /// </summary>
        [Test, Explicit]
        public void TheAttribution()
        {
            string root = Environment.GetEnvironmentVariable("PITCH_PROBE_OUT");
            Assume.That(root, Is.Not.Null.And.Not.Empty, "set PITCH_PROBE_OUT");
            int n = int.TryParse(Environment.GetEnvironmentVariable("PITCH_PROBE_SEEDS"), out int k) ? k : 5;
            DroneSpec spec = RepositoryDrone.Spec();
            var jobs = new System.Collections.Generic.List<(string, int)>();
            foreach (var v in Variants) for (int i = 0; i < n; i++) jobs.Add((v.Name, i));
            System.Threading.Tasks.Parallel.ForEach(jobs, job =>
            {
                var v = Array.Find(Variants, x => x.Name == job.Item1);
                WindHarness.Run r = WindHarness.Fly(spec, WindLevel.Severe, (ulong)(job.Item2 + 1), job.Item2, n, WindHarness.HumanPilots[0],
                    belt: !v.Steady, steady: v.Steady, airframe: v.Change);
                string dir = System.IO.Path.Combine(root, v.Name);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"seed-{r.Seed}.csv"), WindHarness.Csv(r));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"seed-{r.Seed}.segment"), r.Segment ?? "");
            });
        }
    }
}
