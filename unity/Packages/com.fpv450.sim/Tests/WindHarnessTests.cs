using System;
using System.Globalization;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.2, K§8.5's acceptance harness (WindHarness, HarnessPilot): the tracker's angles are the ZYX angles;
    // the simulated pilot's test flight reproduces p6-flight-core 1.5's stand-in crosswind lean inside K§6.5's band,
    // and a drone without drag (a known-wrong model) fails it; the camera log is track_attitude.py's CSV at 29.917 Hz
    // as wind_stats.py's reader takes it; the start rule finds a stretch with the wind on the left. tools/flightbench
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
            TestContext.Out.WriteLine($"1.5's cruise lean as the tracker's roll: {reference:F3}° per m/s");
            Assert.That(reference, Is.InRange(0.95, 2.6));
            WindHarness.Pilot nominal = WindHarness.Pilots[0];
            for (int i = 0; i < 5; i++)
            {
                foreach (bool belt in new[] { false, true })
                {
                    WindHarness.Run r = WindHarness.Fly(spec, WindLevel.Severe, (ulong)i + 1, i, 5, nominal, belt);
                    double beltSeconds = (r.BeltLast - r.BeltFirst + 1) / HarnessPilot.CameraHz;
                    TestContext.Out.WriteLine($"seed {r.Seed}{(belt ? ", belt" : "")}: {r.HeightRef:F0} m, {r.Mass:F3} kg, airspeed {r.MeanAirspeed:F2} m/s, "
                        + $"pitch {r.MeanPitchDeg:F2}°, roll {r.MeanRollDeg:F2}° against {r.MeanCrosswind:F2} m/s = {r.LeanPerMs:F3}° per m/s; belt {r.Segment} ({beltSeconds:F2} s)");
                    Assert.That(WindHarness.Reproduces(r.LeanPerMs, reference, belt ? 0.2 : 0.1), Is.True, "the lean per m/s");
                    Assert.That(r.MeanRollDeg, Is.LessThan(0), "into the wind from the left");
                    Assert.That(r.MeanPitchDeg, Is.EqualTo(HarnessPilot.ClipPPitchDeg).Within(2), "θ_ref ± 2°");
                    Assert.That(r.Samples.Length, Is.EqualTo(1017), "34 s at 29.917 Hz");
                    // The belt's 90 m at the run's ground speed along the course: 13 m/s through the air plus the wind's
                    // component along it, which the meander and the shifts move within the start rule's 90°.
                    Assert.That(beltSeconds, Is.InRange(4, 12), "90 m at 13 m/s through the air, give or take the wind along the course");
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
                Assert.That(f[5] + f[6] + f[7] + f[8], Is.EqualTo("1110"), "conf 1, dup 0");
                for (int k = 9; k < 18; k++) Assert.That(f[k], Is.Empty, "a simulation has no tracker diagnostics");
                Assert.That(f[18], Is.EqualTo("ok"));
            }
            // wind_stats.py's frame rate, (n − 1) / (t_last − t_first), and the heading its yaw run integrates: the
            // course's (the run's mean crab is zero, the split's swings average out), within the CSV's rounding.
            Assert.That((n - 1) / (last - first), Is.EqualTo(HarnessPilot.CameraHz).Within(1e-4));
            Assert.That(Math.Abs(heading), Is.LessThan(2), "the heading's net change over 34 s, degrees");
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
    }
}
