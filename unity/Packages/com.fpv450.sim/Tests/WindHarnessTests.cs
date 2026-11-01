using System;
using System.Globalization;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.2, K§8.5's acceptance harness (WindHarness, HarnessPilot): the tracker's angles are the ZYX angles;
    // the simulated pilot's test flight, at the cruise clip P's pitch flies (p7c-wind 1.6), reproduces the crosswind
    // lean p6-flight-core 1.5's scenario gives there, and a drone without drag (a known-wrong model) fails it; the camera log is track_attitude.py's CSV at 29.917 Hz
    // as wind_stats.py's reader takes it, each frame at the recorder's field pick; the start rule finds a stretch with the
    // wind on the left. tools/flightbench
    // --wind-harness flies the same code and writes the files.
    public sealed class WindHarnessTests
    {
        const double DegToRad = Math.PI / 180;

        static Quat Zyx(double yaw, double pitch, double roll)
        {
            // Yaw about +Y (the heading grows), pitch about body +x (positive puts the nose down, so −pitch), roll about
            // body +z (positive lifts the right side, so −roll).
            Quat Axis(Double3 a, double angle) => new Quat(a.X * Math.Sin(angle / 2), a.Y * Math.Sin(angle / 2), a.Z * Math.Sin(angle / 2), Math.Cos(angle / 2));
            return Axis(new Double3(0, 1, 0), yaw) * Axis(new Double3(1, 0, 0), -pitch) * Axis(new Double3(0, 0, 1), -roll);
        }

        [Test]
        public void TheTrackersAnglesAreTheZyxAngles()
        {
            foreach (double yaw in new[] { -170.0, -90, -20, 0, 35, 179 })
                foreach (double pitch in new[] { -60.0, -22.7, 0, 15 })
                    foreach (double roll in new[] { -45.0, -9.1, 0, 3, 120 })
                    {
                        TrackerAngles.Of(Zyx(yaw * DegToRad, pitch * DegToRad, roll * DegToRad), out double r, out double p, out double h);
                        Assert.That(r, Is.EqualTo(roll * DegToRad).Within(1e-12), "roll");
                        Assert.That(p, Is.EqualTo(pitch * DegToRad).Within(1e-12), "pitch");
                        Assert.That(TrackerAngles.Wrap(h - yaw * DegToRad), Is.EqualTo(0).Within(1e-12), "heading");
                    }
            for (int i = -2000; i <= 2000; i++)
            {
                double y = Math.Sin(i * 0.0021) * (7.5 + i % 7), x = Math.Cos(i * 0.0021) * (5.5 + i % 5);
                Assert.That(TrackerAngles.Atan2(y, x), Is.EqualTo(Math.Atan2(y, x)).Within(4e-16 * Math.PI), "atan2");
            }
            // Signs as the tracker's (wind.md §1.2): rolled right, looking up and turning right are positive.
            TrackerAngles.Of(Zyx(0, 0, 0.1), out double right, out _, out _);
            TrackerAngles.Of(Zyx(0, 0.1, 0), out _, out double up, out _);
            TrackerAngles.Of(Zyx(0.1, 0, 0), out _, out _, out double east);
            Assert.That(right > 0 && up > 0 && east > 0, Is.True);
        }

        [Test]
        public void TheHarnessReproducesTheStandInLean()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double reference = WindHarness.CruiseRollPerMs(spec, WindPreset.SevereCentre);
            // At the cruise (p7c-wind 1.6) the lean per m/s is the airspeed's (wind.md §5.3: the tracker's roll, 23.4°
            // over it); T0 grades what it gives in Severe, not K§6.5's band at 13 m/s. The relation is the small crosswind's
            // slope, so it is checked at 2 m/s: the quadratic drag steepens it with the crosswind (+16 % at 7.2 m/s on
            // D-050's silhouette at its 20.6 m/s cruise, +12.5 % on D-043's at 18.8).
            double relation = 23.4 / WindHarness.ReleaseSpeed, slope = WindHarness.CruiseRollPerMs(spec, 2);
            TestContext.Out.WriteLine($"1.5's cruise lean at {WindHarness.ReleaseSpeed:F1} m/s as the tracker's roll: {slope:F3}° per m/s at 2 m/s "
                + $"(wind.md §5.3's relation {relation:F3}), {reference:F3} at Severe's {WindPreset.SevereCentre:F1} m/s abeam: "
                + $"{reference * WindPreset.SevereCentre:F1}° (T0: 9 ± 3)");
            Assert.That(slope, Is.EqualTo(relation).Within(0.15 * relation));
            WindHarness.Pilot nominal = WindHarness.Pilots[0];
            for (int i = 0; i < 5; i++)
            {
                foreach (bool belt in new[] { false, true })
                {
                    WindHarness.Run r = WindHarness.Fly(spec, WindLevel.Severe, (ulong)i + 1, i, 5, nominal, belt);
                    double beltSeconds = (r.BeltLast - r.BeltFirst + 1) / HarnessPilot.CameraHz;
                    double own = WindHarness.CruiseRollPerMs(spec, r.MeanCrosswind); // 1.5's at the run's own crosswind
                    TestContext.Out.WriteLine($"seed {r.Seed}{(belt ? ", belt" : "")}: {r.HeightRef:F0} m, {r.Mass:F3} kg, airspeed {r.MeanAirspeed:F2} m/s, "
                        + $"pitch {r.MeanPitchDeg:F2}°, roll {r.MeanRollDeg:F2}° against {r.MeanCrosswind:F2} m/s of {r.MeanSpeed:F2} = {r.LeanPerMs:F3}° per m/s "
                        + $"(1.5's there {own:F3}); belt {r.Segment} ({beltSeconds:F2} s)");
                    Assert.That(WindHarness.Reproduces(r.LeanPerMs, own, belt ? 0.2 : 0.15), Is.True, "the lean per m/s");
                    Assert.That(r.MeanRollDeg, Is.LessThan(0), "into the wind from the left");
                    Assert.That(r.MeanPitchDeg, Is.EqualTo(HarnessPilot.ClipPPitchDeg).Within(2), "θ_ref ± 2°");
                    int usable = Array.FindAll(r.Samples, x => x.Usable).Length;
                    Assert.That(usable, Is.InRange(1016, 1018), "34 s at 29.917 Hz, without the climb between the stretches");
                    Assert.That(r.Samples.Length - usable, Is.EqualTo((int)(r.ClimbSeconds * HarnessPilot.CameraHz)).Within(1), "the climb's frames");
                    Assert.That(Array.TrueForAll(r.Samples, x => !x.InBelt || x.Usable), Is.True, "the belt is measured");
                    Assert.That(r.BeltHeightRef, Is.InRange(0.5 * WindHarness.StripHeight, WindHarness.StripHeight), "the belt at 0.5–1.0 H");
                    // P's 7.3 H at the run's ground speed along the course: the cruise through the air plus the wind's
                    // component along it, which the meander and the shifts move within the start rule's 90°.
                    Assert.That(beltSeconds, Is.InRange(4, 12), "7 s at the cruise's ground speed, give or take the wind along the course");
                    Assert.That(r.MeanAirspeed / WindHarness.ReleaseSpeed, Is.InRange(0.95, 1.08), "the course is flown at the cruise");
                    Assert.That(r.MeanCrosswind, Is.GreaterThan(0.25 * r.MeanSpeed), "the wind from the left");
                }
            }
            WindHarness.Run wrong = WindHarness.Fly(spec, WindLevel.Severe, 1, 0, 5, nominal, belt: false, drag: false);
            TestContext.Out.WriteLine($"known-wrong, no drag: {wrong.LeanPerMs:F3}° per m/s");
            Assert.That(WindHarness.Reproduces(wrong.LeanPerMs, reference, 0.1), Is.False, "no drag fails");
        }

        [Test]
        public void TheLogIsTheTrackersCsv()
        {
            WindHarness.Run r = WindHarness.Fly(RepositoryDrone.Spec(), WindLevel.Windy, 3, 1, 5, WindHarness.Pilots[0]);
            string[] lines = WindHarness.Csv(r).Split('\n');
            Assert.That(lines[lines.Length - 1], Is.Empty, "ends with a line end");
            Assert.That(lines[0], Is.EqualTo("frame,time_s,roll_deg,pitch_deg,yaw_rate_dps,conf_roll,conf_pitch,conf_yaw,dup,n_cols,inlier_frac,"
                + "contrast,span,resid_px,edge_px,yaw_peak,yaw_unique,yaw_del_deg,flag"), "track_attitude.py's COLUMNS");
            int n = lines.Length - 2;
            Assert.That(n, Is.EqualTo(r.Samples.Length));
            double first = 0, last = 0, heading = 0;
            for (int i = 1; i <= n; i++)
            {
                string[] f = lines[i].Split(',');
                Assert.That(f.Length, Is.EqualTo(19));
                Assert.That(int.Parse(f[0], CultureInfo.InvariantCulture), Is.EqualTo(i));
                double t = double.Parse(f[1], CultureInfo.InvariantCulture);
                Assert.That(t, Is.EqualTo((i - 1) / HarnessPilot.CameraHz).Within(1e-6));
                if (i == 1) first = t;
                last = t;
                Assert.That(double.Parse(f[2], CultureInfo.InvariantCulture), Is.EqualTo(r.Samples[i - 1].RollDeg).Within(5e-5));
                Assert.That(double.Parse(f[3], CultureInfo.InvariantCulture), Is.EqualTo(r.Samples[i - 1].PitchDeg).Within(5e-5));
                heading += double.Parse(f[4], CultureInfo.InvariantCulture) / HarnessPilot.CameraHz;
                Assert.That(f[5] + f[6] + f[7] + f[8], Is.EqualTo(r.Samples[i - 1].Usable ? "1110" : "0000"), "conf 1 (0 in the climb), dup 0");
                for (int k = 9; k < 18; k++) Assert.That(f[k], Is.Empty, "a simulation has no tracker diagnostics");
                Assert.That(f[18], Is.EqualTo("ok"));
            }
            // wind_stats.py's frame rate, (n − 1) / (t_last − t_first), and the heading its yaw run integrates: the
            // course's (the run's mean crab is zero, the split's swings average out), within the CSV's rounding.
            Assert.That((n - 1) / (last - first), Is.EqualTo(HarnessPilot.CameraHz).Within(1e-4));
            Assert.That(Math.Abs(heading), Is.LessThan(2), "the heading's net change over the run, degrees");
            Assert.That(r.Segment, Does.Match(@"^\d+-\d+$"));
        }

        [Test]
        public void TheStartKeepsTheWindOnTheLeft()
        {
            var steady = new double[200];
            for (int i = 0; i < steady.Length; i++) steady[i] = 250 + 30 * Math.Sin(i * 0.1);
            Assert.That(WindHarness.StartSecond(steady, 250, 39), Is.EqualTo(0));
            var swung = (double[])steady.Clone();
            for (int i = 20; i < 60; i++) swung[i] = 250 + 120; // 120° off from 20 s to 59 s
            Assert.That(WindHarness.StartSecond(swung, 250, 39), Is.EqualTo(60));
            Assert.That(WindHarness.StartSecond(steady, 70, 39), Is.EqualTo(-1), "never within 90° of 70°");
            Assert.That(WindHarness.StartSecond(swung, 10, 39), Is.EqualTo(20), "only the swing's 40 s are within 90° of 10°");
            Assert.That(WindHarness.StartSecond(new[] { 359.0, 1, 2, 358 }, 0.5, 3), Is.EqualTo(0), "across north");
            // The field's history moves (p7c-wind 1.4): the start is the first second the rule allows, and the flight
            // started there meets the history's direction from that second on.
            double[] h = WindHarness.History(WindLevel.Severe, 9, 120, out double drawn);
            Assert.That(h.Length, Is.EqualTo(7201));
            Assert.That(Array.TrueForAll(h, d => d == drawn), Is.False, "the direction moves");
            int start = WindHarness.StartSecond(h, drawn, WindHarness.Seconds);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            for (int s = start; s <= start + 39; s++)
                Assert.That(Math.Abs(((h[s] - drawn) % 360 + 540) % 360 - 180), Is.LessThan(90), "second " + s);
            var late = new WindField(new WindSetup { Level = WindLevel.Severe, StartSecond = start }, new FlightRandom(9));
            Assert.That(late.FromDegAt(0), Is.EqualTo(h[start]));
        }

        // D-060: the harness pilot's stick noise is the optimal-control model's motor noise. On a command of known variance
        // (a 1 Hz sinusoid, σ_c² = A²/2) its variance is π·ρ_u·σ_c² through the neuromotor lag, (1 − a)/(1 + a)·π·ρ_u·σ_c²/Δt
        // at the 250 Hz reports (a = e^(−Δt/τ_n)), with ρ_u at −25 dB, and its lag-1 correlation is a. The observation
        // noise's −20 dB in its place (3.2× the variance) fails; without UseRemnant the pilot is noiseless (the grade's
        // known-wrong family).
        [Test]
        public void TheStickNoiseIsTheModelsMotorNoise()
        {
            double Variance(double db, out double lag1)
            {
                var pilot = new HarnessPilot(null) { MotorNoiseDb = db };
                pilot.UseRemnant(7);
                const double amplitude = 40; // °/s
                double sum = 0, sum2 = 0, cross = 0, last = 0;
                int n = 0;
                for (long k = 0; k < 250 * 300; k++)
                {
                    double noise = pilot.Motor(1, amplitude * Math.Sin(2 * Math.PI * k / 250.0), k);
                    if (k >= 250 * 30)
                    {
                        sum += noise;
                        sum2 += noise * noise;
                        cross += noise * last;
                        n++;
                    }
                    last = noise;
                }
                double mean = sum / n, v = sum2 / n - mean * mean;
                lag1 = (cross / n - mean * mean) / v;
                return v / (amplitude * amplitude / 2);
            }
            double dt = 0.004, a = Math.Exp(-dt / 0.1);
            double expected = (1 - a) / (1 + a) * Math.PI * Math.Pow(10, -2.5) / dt;
            double got = Variance(-25, out double lag1), wrong = Variance(-20, out _);
            TestContext.Out.WriteLine($"stick noise over the command's variance: {got:F4} (the model's {expected:F4}); lag-1 correlation {lag1:F4} ({a:F4}); "
                + $"at −20 dB {wrong:F4}");
            Assert.That(got / expected, Is.EqualTo(1).Within(0.1));
            Assert.That(lag1, Is.EqualTo(a).Within(0.01));
            Assert.That(Math.Abs(wrong / expected - 1), Is.GreaterThan(0.1), "−20 dB is not the motor noise's ratio");
        }

        // D-060, hover-micro-motion.md §2.3: each logged frame shows the newest whole PAL field at its horizon row, as the
        // recorder writes it at its 29.917 Hz ticks, so the frames' instants jitter by a field (σ 20/√12 = 5.8 ms) against
        // the tracker's uniform clock. The uniform clock (the harness before D-060, the known-wrong) has no such spread.
        [Test]
        public void TheFramesShowTheRecordersFieldPick()
        {
            const int frames = 1800; // a minute
            var lag = new double[frames];
            var uniform = new double[frames];
            for (int f = 1; f < frames; f++)
            {
                double tick = f * 1000 / HarnessPilot.CameraHz; // ms after the release
                long pick = WindHarness.FieldStep(f);
                Assert.That((pick - 5) % 20, Is.EqualTo(0), "a field's horizon row, frame " + f);
                Assert.That(pick - 5 + 20, Is.LessThanOrEqualTo(tick), "a whole field by the tick, frame " + f);
                Assert.That(pick - 5 + 40, Is.GreaterThan(tick), "the newest, frame " + f);
                lag[f] = tick - pick;
                uniform[f] = Math.Ceiling(tick - 1e-9) - tick;
            }
            static double Spread(double[] x)
            {
                double m = 0, v = 0;
                for (int i = 1; i < x.Length; i++) m += x[i] / (x.Length - 1);
                for (int i = 1; i < x.Length; i++) v += (x[i] - m) * (x[i] - m) / (x.Length - 2);
                return Math.Sqrt(v);
            }
            bool AField(double sigma) => Math.Abs(sigma / (20 / Math.Sqrt(12)) - 1) <= 0.1;
            double picked = Spread(lag), clock = Spread(uniform);
            TestContext.Out.WriteLine($"the frames' instants jitter by σ {picked:F2} ms (a field's 5.77); the uniform clock's {clock:F2} ms");
            Assert.That(AField(picked), Is.True);
            Assert.That(AField(clock), Is.False, "the uniform clock (the known-wrong) has no field's jitter");
        }
    }
}
