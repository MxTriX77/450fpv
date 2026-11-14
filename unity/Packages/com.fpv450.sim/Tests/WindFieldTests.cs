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

        // D-060: the open field's z0 is the landscape's class (Davenport–Wieringa class 4, 0.10 m, published), and σ_u/U
        // follows from it (derived: 2.4·κ/ln((35 − 0.2)/0.10) = 0.164), not the stubble's 0.03 m (0.136).
        [Test]
        public void TheOpenFieldsRoughnessIsTheLandscapesClass()
        {
            foreach (WindLevel level in new[] { WindLevel.Calm, WindLevel.Windy, WindLevel.Severe })
            {
                WindPreset p = WindPreset.Of(level);
                Assert.That(p.Roughness, Is.EqualTo(0.10), "class 4's z0");
                Assert.That(p.Displacement, Is.EqualTo(0.67 * 0.3).Within(1e-15), "the stubble's d");
                Assert.That(p.Intensity, Is.EqualTo(2.4 * 0.4 / Math.Log((35 - 0.201) / 0.10)).Within(1e-12));
                Assert.That(p.Intensity, Is.EqualTo(0.164).Within(0.0005));
                Assert.That(Stubble(level).Intensity, Is.EqualTo(0.136).Within(0.0005), "the stubble's z0 0.03 m");
            }
        }

        // D-060: the turbulence reaches the discs' 0.5 m (2D), so the inertial range spans the rotors' spacing. Met at the
        // cruise, the field's 20–30 Hz (its 0.7–1 m eddies) holds von Kármán's energy; the 1 m cut, the field before D-060,
        // leaves almost none there (only the projection of its 1 m modes on the path).
        [Test]
        public void TheTurbulenceReachesTheDiscsScale()
        {
            WindPreset preset = WindPreset.Of(WindLevel.Severe);
            Assert.That(preset.ShortestWavelength, Is.EqualTo(2 * 0.254).Within(0.01), "twice the 10-inch disc");
            WindPreset cut = preset;
            cut.ShortestWavelength = 1;
            cut.Modes = 128;
            var met = new List<History>();
            var coarse = new List<History>();
            for (ulong seed = 1; seed <= 5; seed++)
            {
                met.Add(Encounter(WindLevel.Severe, seed, 33, WindHarness.FieldCheckSpeed));
                coarse.Add(Encounter(WindLevel.Severe, seed, 33, WindHarness.FieldCheckSpeed, cut));
            }
            bool reaches = Spectrum(met, preset, WindHarness.FieldCheckSpeed, 20, 30, out string d, out _);
            bool wrong = Spectrum(coarse, preset, WindHarness.FieldCheckSpeed, 20, 30, out string w, out _);
            Spectrum(met, preset, WindHarness.FieldCheckSpeed, 4, 15, out string d4, out _);
            Spectrum(coarse, preset, WindHarness.FieldCheckSpeed, 4, 15, out string w4, out _);
            TestContext.Out.WriteLine($"to 0.5 m: {d}; over the camera's band, {d4}");
            TestContext.Out.WriteLine($"known-wrong, the 1 m cut: {w}; over the camera's band, {w4}");
            Assert.That(reaches, Is.True, d);
            Assert.That(wrong, Is.False, "the 1 m cut fails: " + w);
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
                // ignores the wind and the stubble's roughness (z0 0.03 m, D-060's known-wrong) fail the intensity, a mean
                // 5 % off its drawn strength fails the unbiased mean.
                WindPreset cut = preset;
                cut.ShortestWavelength = WindHarness.ReleaseSpeed;
                var low = new List<History>();
                var white = new List<History>();
                var flat = new List<History>();
                var stubble = new List<History>();
                var biased = new List<History>();
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    low.Add(Encounter(level, seed, 33, WindHarness.ReleaseSpeed, cut));
                    white.Add(Encounter(level, seed, 33, WindHarness.ReleaseSpeed, null, White));
                    flat.Add(Gusts(level, seed, 300, 50, FlatSigma));
                }
                for (ulong seed = 1; seed <= 20; seed++) stubble.Add(Gusts(level, seed, 1200, 50, null, Stubble(level)));
                foreach (History h in runs) biased.Add(Biased(h));
                Assert.That(Spectrum(low, preset, WindHarness.ReleaseSpeed, 2, 4, out string w0, out _), Is.False, "a field below 1 Hz fails the spectrum: " + w0);
                Assert.That(Spectrum(white, preset, WindHarness.ReleaseSpeed, 2, 4, out string w1, out _), Is.False, "white noise fails the spectrum: " + w1);
                Assert.That(Intensity(flat, preset, out string w2, out _), Is.False, "a flat σ fails the intensity: " + w2);
                Assert.That(Intensity(stubble, preset, out string w4, out _), Is.False, "the stubble's roughness fails the class's intensity: " + w4);
                Assert.That(MeanInBand(biased, preset, out string w3), Is.False, "a biased mean fails: " + w3);
                TestContext.Out.WriteLine($"  known-wrong: below 1 Hz: {w0}; white noise: {w1}; a flat 0.2 m/s σ: {w2}; the stubble's z0: {w4}; a mean 5 % strong: {w3}");
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

        // p7c-wind design 16: the field's w along the harness's course (Severe, the wind abeam from the left at the cruise's
        // ground speed) at 2, 5, 10, 20 and 35 m, 10 seeds × 120 s at the camera's rate, band by band (Welch, WindLevelsProbe.
        // Bands) against Kaimal et al. (1972)'s neutral w at the speed the path crosses the frozen field and the field's u*:
        // within 0.8–1.25 at 0.5–4 Hz and 0.8–1.6 at 0.23–0.5 Hz (the windows' leakage from below; the bounds are design 16's,
        // set before the build). The known-wrong, the one-scale field (model 98's), fails at every height below 35 m, where it
        // is the handbook's own and passes.
        [Test]
        public void TheVerticalGustsScaleWithHeight()
        {
            double[] lo = { 0.23, 0.5, 1, 2 }, hi = { 0.5, 1, 2, 4 }, min = { 0.8, 0.8, 0.8, 0.8 }, max = { 1.6, 1.25, 1.25, 1.25 };
            foreach (double z in new[] { 2.0, 5, 10, 20, 35 })
            {
                double[] law = VerticalBandsOverKaimal(z, false, lo, hi), wrong = VerticalBandsOverKaimal(z, true, lo, hi);
                bool Passes(double[] r)
                {
                    for (int k = 0; k < r.Length; k++) if (!(r[k] >= min[k] && r[k] <= max[k])) return false;
                    return true;
                }
                string Row(double[] r) => string.Join(" / ", Array.ConvertAll(r, x => F(x, "0.00")));
                TestContext.Out.WriteLine($"{z} m, w over Kaimal's in 0.23–0.5 / 0.5–1 / 1–2 / 2–4 Hz: the law {Row(law)}; the one-scale field {Row(wrong)}");
                Assert.That(Passes(law), Is.True, $"{z} m: {Row(law)}");
                if (z < 35) Assert.That(Passes(wrong), Is.False, $"the one-scale field fails at {z} m: {Row(wrong)}");
                else Assert.That(Passes(wrong), Is.True, $"at 35 m the one-scale field is the handbook's own: {Row(wrong)}");
            }
        }

        static double[] VerticalBandsOverKaimal(double z, bool oneScale, double[] lo, double[] hi)
        {
            const double fps = HarnessPilot.CameraHz, seconds = 120, degToRad = 0.017453292519943295;
            const int seeds = 10;
            WindPreset p = WindPreset.Of(WindLevel.Severe);
            var field = new double[lo.Length];
            var kaimal = new double[lo.Length];
            for (ulong seed = 1; seed <= seeds; seed++)
            {
                var wind = new WindField(new WindSetup { Level = WindLevel.Severe, OneScale = oneScale }, new FlightRandom(seed));
                DetMath.SinCos((wind.FromDeg + 90) % 360 * degToRad, out double sin, out double cos);
                var along = new Double3(sin, 0, cos);
                int n = (int)(seconds * fps);
                var w = new double[n];
                double speed = 0;
                for (int i = 0; i < n; i++)
                {
                    long step = (long)(i * 1000 / fps);
                    Double3 at = along * (WindHarness.GroundSpeed * step * 0.001) + new Double3(0, z, 0);
                    wind.Begin(step, at, 0);
                    w[i] = wind.At(at, 0).Y;
                    speed += (along * WindHarness.GroundSpeed - wind.Mean).Length() / n; // the path through the frozen field
                }
                double[] b = WindLevelsProbe.Bands(w, fps, lo, hi);
                double ustar = WindPreset.Karman * wind.MeanSpeed / Math.Log((p.ReferenceHeight - p.Displacement) / p.Roughness);
                for (int k = 0; k < lo.Length; k++)
                {
                    field[k] += b[k] * b[k] / seeds;
                    double sum = 0;
                    const int m = 200;
                    for (int j = 0; j < m; j++)
                    {
                        double hz = lo[k] * Math.Exp(Math.Log(hi[k] / lo[k]) * (j + 0.5) / m), f = hz * (z - p.Displacement) / speed;
                        sum += 2.1 * f / (1 + 5.3 * Math.Pow(f, 5.0 / 3)) * Math.Log(hi[k] / lo[k]) / m;
                    }
                    kaimal[k] += sum * ustar * ustar / seeds;
                }
            }
            var r = new double[lo.Length];
            for (int k = 0; k < r.Length; k++) r[k] = Math.Sqrt(field[k] / kaimal[k]);
            return r;
        }

        // p7c-wind design 16: the law's numbers. The coefficients are von Kármán's scales for the surface layer's σ and Kaimal's
        // inertial range (L ∝ σ³/ε); the scales hold above the profile's top and the canopy top's floor them in the cover's
        // roughness sublayer; σ_w/σ_u is the surface layer's; the taper is the sublayer's depth; the one-scale preset is
        // model 98's.
        [Test]
        public void TheScalesFollowTheSurfaceLayer()
        {
            double Coefficient(double sigma, double a, double form) => Math.Pow(a / (sigma * sigma * form * Math.Pow(1.339, -5.0 / 3)), -1.5) / (2 * Math.PI);
            WindPreset p = WindPreset.Of(WindLevel.Windy);
            Assert.That(p.HeightScaled, Is.True);
            Assert.That(p.HorizontalScaleOverHeight, Is.EqualTo(Coefficient(2.4, 105 / Math.Pow(33, 5.0 / 3), 2 / Math.PI)).Within(1e-3), "u's: Kaimal's 0.309");
            Assert.That(p.VerticalScaleOverHeight, Is.EqualTo(Coefficient(1.25, 2.1 / 5.3, 8 / (3 * Math.PI))).Within(1e-3), "w's: Kaimal's 0.396");
            Assert.That(p.VerticalRatio, Is.EqualTo(1.25 / 2.4).Within(1e-15));
            Assert.That(p.TaperHeight, Is.EqualTo(2 * (0.3 - 0.201)).Within(1e-12), "Raupach's roughness sublayer over the cover");
            double d = p.Displacement;
            foreach (double z in new[] { 0.3, 0.35, 0.5, 0.58, 2, 10, 35, 100 })
            {
                Assert.That(p.HorizontalScaleAt(z), Is.EqualTo(Math.Max(3.132 * (z - d), 0.3)).Within(1e-12), "L_h at " + z);
                Assert.That(p.VerticalScaleAt(z), Is.EqualTo(Math.Max(0.470 * (z - d), 0.18)).Within(1e-12), "L_w at " + z);
            }
            Assert.That(p.HorizontalScaleAt(400), Is.EqualTo(p.HorizontalScaleAt(100)), "held above the top");
            Assert.That(p.VerticalScaleAt(35), Is.EqualTo(16.35).Within(0.01));
            WindPreset old = p.OneScale();
            Assert.That((old.HeightScaled, old.VerticalRatio, old.TaperHeight, old.HorizontalScale, old.VerticalScale), Is.EqualTo((false, 0.594, 1.0, 167.0, 35.0)));
            Assert.That((old.HorizontalScaleAt(2), old.VerticalScaleAt(2)), Is.EqualTo((167.0, 35.0)), "one scale at every height");
        }

        // p7c-wind design 16: the table is the band energy. At each height the field by height is, mode for mode, the one-scale
        // field built at that height's own scales (the same draws and wave vectors: its longest wavenumber held by the
        // longest-scale fraction), within the table's linear interpolation; above the profile's top it holds.
        [Test]
        public void TheScaledModesAreTheBandEnergyAtEachHeight()
        {
            foreach (double z in new[] { 0.35, 0.7, 1.25, 3, 10, 35, 80, 150 })
            {
                var scaled = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom(21));
                WindPreset p = scaled.Preset, exact = p.OneScale();
                exact.VerticalRatio = p.VerticalRatio;
                exact.HorizontalScale = p.HorizontalScaleAt(z);
                exact.VerticalScale = p.VerticalScaleAt(z);
                exact.LongestFraction = p.LongestFraction * exact.HorizontalScale / p.HorizontalScale;
                var one = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom(21), exact);
                double worst = 0, rms = 0;
                const int n = 400;
                for (int i = 0; i < n; i++)
                {
                    var at = new Double3(1.7 * i, z, -0.9 * i);
                    scaled.Begin(i * 37L, at, 0);
                    one.Begin(i * 37L, at, 0);
                    Double3 a = scaled.Turbulence(at), b = one.Turbulence(at);
                    worst = Math.Max(worst, (a - b).Length());
                    rms += b.Length() * b.Length() / n;
                }
                rms = Math.Sqrt(rms);
                TestContext.Out.WriteLine($"{z} m: scales {F(p.HorizontalScaleAt(z), "0.00")} / {F(p.VerticalScaleAt(z), "0.000")} m, at most {F(worst / rms, "0.00000")} of the RMS apart");
                Assert.That(worst, Is.LessThan(0.005 * rms), z + " m");
            }
        }

        // p7c-wind design 16's cost (Explicit; a timing, so on AC power only): the open field's At per sample, the one-scale field
        // and the field by height, at 1.25 and 35 m, best of five passes of 200 000 samples.
        [Test, Explicit]
        public void TheFieldsCostPerSample()
        {
            foreach (bool oneScale in new[] { true, false })
                foreach (double z in new[] { 1.25, 35 })
                {
                    var wind = new WindField(new WindSetup { Level = WindLevel.Severe, OneScale = oneScale }, new FlightRandom(3));
                    double best = double.MaxValue, sink = 0;
                    for (int pass = 0; pass < 5; pass++)
                    {
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 200000; i++)
                        {
                            var at = new Double3(0.01 * i, z, 0.02 * i);
                            if (i % 4 == 0) wind.Begin(i / 4, at, 0);
                            sink += wind.At(at, 0).Y;
                        }
                        best = Math.Min(best, clock.Elapsed.TotalMilliseconds * 1e6 / 200000);
                    }
                    TestContext.Out.WriteLine($"{(oneScale ? "one-scale" : "by height")} at {z} m: {F(best, "0")} ns a sample (checksum {F(sink, "0.0")})");
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
            Assert.That(bands, Is.EqualTo(new[] { 1.06875, 1.78125, 3.5625, 5.9375, 7.125, 11.875 }).Within(1e-12), "centre ± 25 % (K§8.6)");
        }
    }
}
