using System;
using System.Numerics;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    // fpv-camera (p7e-camera-feel 1.3): the rolling-shutter read and the exposure's blur, from the camera's rotations alone.
    public class RollingShutterTests
    {
        const double PixelsPerRad = 960 * 1.005; // the render at the centre: 960 px a half-width × f (design 5)
        const double DegToRad = Math.PI / 180;

        // The camera turning about y at rate rad/s, against the field's reference instant, at the history's samples.
        static Quaternion[] Turning(double rate)
        {
            var q = new Quaternion[RollingShutter.Samples];
            for (int i = 0; i < q.Length; i++)
            {
                double t = (i - RollingShutter.SampleLead) * RollingShutter.SampleUs - RollingShutter.ReferenceUs;
                q[i] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(rate * t * 1e-6));
            }
            return q;
        }

        [Test]
        public void TheRowsAreReadAtPalsLines()
        {
            Assert.That(RollingShutter.RowReadUs(0), Is.EqualTo(1632));
            Assert.That(RollingShutter.ReferenceUs, Is.EqualTo(10784));
            Assert.That(RollingShutter.RowReadUs(RollingShutter.Rows - 1), Is.EqualTo(19936));
        }

        // fpv-camera's "A turn skews the field": 300 °/s in yaw turns the first line's read against the last's by 5.49°.
        [Test]
        public void ATurnSkewsTheField()
        {
            var shutter = new RollingShutter();
            shutter.Build(Turning(300 * DegToRad), RollingShutter.MinExposureS, PixelsPerRad);
            double skew = RollingShutter.Angle(shutter.TapOf(0, 0), shutter.TapOf(RollingShutter.Rows - 1, 0)) / DegToRad;
            TestContext.WriteLine($"first to last line: {skew:F3}°");
            Assert.That(skew, Is.EqualTo(300 * 286 * 64e-6).Within(0.01 * 300 * 286 * 64e-6));
            double middle = RollingShutter.Angle(shutter.TapOf(RollingShutter.Rows / 2, 0), Quaternion.Identity);
            Assert.That(middle, Is.LessThan(300 * DegToRad * RollingShutter.MinExposureS), "the middle line is read at the reference, less half its exposure");
            Assert.That(shutter.Turning, Is.True);
        }

        // fpv-camera's "Daylight stays crisp": the unmeasured light and the daylight's are 1/8192 s, one tap at 300 °/s.
        [Test]
        public void DaylightStaysCrisp()
        {
            Assert.That(RollingShutter.ExposureS(0), Is.EqualTo(1.0 / 8192).Within(1e-12));
            Assert.That(RollingShutter.ExposureS(4096), Is.EqualTo(1.0 / 8192).Within(1e-12));
            var shutter = new RollingShutter();
            shutter.Build(Turning(300 * DegToRad), RollingShutter.ExposureS(0), PixelsPerRad);
            for (int r = 0; r < RollingShutter.Rows; r++) Assert.That(shutter.TapsOf(r), Is.EqualTo(1), "row " + r);
        }

        // fpv-camera's "Night smears": 8 cd/m² or less is 1/50 s, and 100 °/s spreads 8 taps over each row's 20 ms.
        [Test]
        public void NightSmears()
        {
            Assert.That(RollingShutter.ExposureS(8), Is.EqualTo(RollingShutter.MaxExposureS));
            Assert.That(RollingShutter.ExposureS(1), Is.EqualTo(RollingShutter.MaxExposureS));
            Assert.That(RollingShutter.ExposureS(1e9f), Is.EqualTo(RollingShutter.MinExposureS));
            double rate = 100 * DegToRad;
            var shutter = new RollingShutter();
            shutter.Build(Turning(rate), RollingShutter.ExposureS(8), PixelsPerRad);
            for (int r = 0; r < RollingShutter.Rows; r += 13)
            {
                Assert.That(shutter.TapsOf(r), Is.EqualTo(RollingShutter.Taps), "row " + r);
                double spread = RollingShutter.Angle(shutter.TapOf(r, 0), shutter.TapOf(r, RollingShutter.Taps - 1));
                Assert.That(spread, Is.EqualTo(rate * 0.02 * 7 / 8).Within(1e-4), "the first and last taps 17.5 ms apart");
            }
        }

        // fpv-camera's "A still drone shows a still picture".
        [Test]
        public void AStillCameraReadsTheReference()
        {
            var shutter = new RollingShutter();
            shutter.Build(Turning(0), RollingShutter.MaxExposureS, PixelsPerRad);
            Assert.That(shutter.Turning, Is.False);
            for (int r = 0; r < RollingShutter.Rows; r++)
            {
                Assert.That(shutter.TapsOf(r), Is.EqualTo(1));
                Assert.That(RollingShutter.Angle(shutter.TapOf(r, 0), Quaternion.Identity), Is.LessThan(1e-9));
            }
            shutter.Clear();
            Assert.That(shutter.TapsOf(0), Is.EqualTo(1));
            Assert.That(shutter.TapOf(0, 0), Is.EqualTo(Quaternion.Identity));
        }
    }
}
