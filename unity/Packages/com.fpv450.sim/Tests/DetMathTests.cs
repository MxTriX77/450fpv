using System;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    public sealed class DetMathTests
    {
        // Expected bits are the correctly rounded doubles of the exact roots (derived).
        // A runtime whose sqrt is not correctly rounded fails here.
        [TestCase(4.0, 0x4000000000000000L)] // 2
        [TestCase(2.0, 0x3FF6A09E667F3BCDL)] // 1.4142135623730951
        [TestCase(3.0, 0x3FFBB67AE8584CAAL)] // 1.7320508075688772
        public void Sqrt_IsCorrectlyRounded(double x, long expectedBits)
        {
            Assert.That(BitConverter.DoubleToInt64Bits(DetMath.Sqrt(x)), Is.EqualTo(expectedBits));
        }

        // The cube root scales the payload (drone.json): exact on exact cubes, so the file's own mass leaves the part
        // as written, and close to the root elsewhere: within 2 ulp of System.Math.Cbrt (a test-only reference, itself
        // up to an ulp from the true root).
        [TestCase(1.0, 1.0)]
        [TestCase(8.0, 2.0)]
        [TestCase(0.125, 0.5)]
        [TestCase(27.0, 3.0)]
        [TestCase(1073741824.0, 1024.0)]
        [TestCase(-8.0, -2.0)]
        [TestCase(0.0, 0.0)]
        public void Cbrt_IsExactOnExactCubes(double x, double root)
        {
            Assert.That(DetMath.Cbrt(x), Is.EqualTo(root));
        }

        [Test]
        public void Cbrt_IsWithinTwoUlpOfTheRoot()
        {
            double worst = 0;
            for (int i = 1; i <= 20000; i++)
            {
                double x = i * 1e-4 * (i % 7 + 1);
                double y = DetMath.Cbrt(x), reference = Math.Cbrt(x);
                double ulp = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(reference) + 1) - reference;
                worst = Math.Max(worst, Math.Abs(y - reference) / ulp);
            }
            TestContext.WriteLine($"worst {worst:F2} ulp");
            Assert.That(worst, Is.LessThanOrEqualTo(2));
            Assert.That(double.IsNaN(DetMath.Cbrt(double.NaN)), Is.True);
            Assert.That(DetMath.Cbrt(double.PositiveInfinity), Is.EqualTo(double.PositiveInfinity));
        }

        // SinCos against System.Math over ±10 turns, and its exact points; its bits are pinned at two angles so a
        // runtime that rounded one of its basic operations differently would show here (the wind's bearing uses it).
        [Test]
        public void SinCos_AgreesWithTheRuntimesToHalfAnUlpOfOne()
        {
            double worst = 0;
            for (int i = -200000; i <= 200000; i++)
            {
                double x = i * 3.1415e-4;
                DetMath.SinCos(x, out double s, out double c);
                worst = Math.Max(worst, Math.Max(Math.Abs(s - Math.Sin(x)), Math.Abs(c - Math.Cos(x))));
            }
            TestContext.WriteLine($"worst {worst:E2} against System.Math");
            Assert.That(worst, Is.LessThanOrEqualTo(1.2e-16));
            DetMath.SinCos(0, out double s0, out double c0);
            Assert.That((s0, c0), Is.EqualTo((0.0, 1.0)));
            DetMath.SinCos(-Math.PI / 2, out double s1, out double c1);
            Assert.That(s1, Is.EqualTo(-1.0));
            Assert.That(Math.Abs(c1), Is.LessThan(1e-16));
            DetMath.SinCos(1.0, out double s2, out double c2);
            DetMath.SinCos(250 * Math.PI / 180, out double s3, out double c3);
            Assert.That((BitConverter.DoubleToInt64Bits(s2), BitConverter.DoubleToInt64Bits(c2)), Is.EqualTo((0x3FEAED548F090CEEL, 0x3FE14A280FB5068CL)));
            Assert.That((BitConverter.DoubleToInt64Bits(s3), BitConverter.DoubleToInt64Bits(c3)), Is.EqualTo((PinnedS3, PinnedC3)));
        }

        // Log and Exp (the wind field's profile and draws, p7c-wind 1.4) against System.Math over their working ranges, a
        // few ulp, and their exact points.
        [Test]
        public void LogAndExp_AgreeWithTheRuntimesToAFewUlp()
        {
            double worstLog = 0, worstExp = 0;
            for (int i = -2000; i <= 2000; i++)
            {
                double x = Math.Pow(10, i * 0.0137) * 1.0001;
                double l = DetMath.Log(x), rl = Math.Log(x);
                worstLog = Math.Max(worstLog, Math.Abs(l - rl) / Math.Max(1e-300, Math.Abs(rl) < 1 ? 1 : Math.Abs(rl)));
                double y = i * 0.35;
                double ex = DetMath.Exp(y), re = Math.Exp(y);
                worstExp = Math.Max(worstExp, Math.Abs(ex - re) / re);
            }
            TestContext.WriteLine($"worst log {worstLog:E2}, exp {worstExp:E2} (relative, against System.Math)");
            Assert.That(worstLog, Is.LessThanOrEqualTo(4.5e-16));
            Assert.That(worstExp, Is.LessThanOrEqualTo(4.5e-16));
            Assert.That(DetMath.Log(1), Is.EqualTo(0.0));
            Assert.That(DetMath.Exp(0), Is.EqualTo(1.0));
            Assert.That(DetMath.Log(0), Is.EqualTo(double.NegativeInfinity));
            Assert.That(double.IsNaN(DetMath.Log(-1)), Is.True);
            Assert.That(Math.Abs(DetMath.Log(4.9e-324) - Math.Log(4.9e-324)), Is.LessThan(1e-12));
            Assert.That(DetMath.Exp(800), Is.EqualTo(double.PositiveInfinity));
            Assert.That(DetMath.Exp(-800), Is.EqualTo(0.0));
        }

        // p7c-wind (the map's wind): the angle of a belt's axis and of a wind volume turned about the vertical.
        [Test]
        public void Atan2_AgreesWithTheRuntimesToAFewUlpOfPi()
        {
            double worst = 0;
            for (int i = 0; i < 20000; i++)
            {
                DetMath.SinCos(i * 0.000314159 * 1.0007 - 3.14, out double s, out double c);
                double r = 0.3 + i % 13;
                double a = DetMath.Atan2(s * r, c * r), reference = Math.Atan2(s * r, c * r);
                worst = Math.Max(worst, Math.Abs(a - reference));
            }
            TestContext.WriteLine($"worst atan2 {worst:E2} (absolute, against System.Math)");
            Assert.That(worst, Is.LessThanOrEqualTo(1.4e-15));
            Assert.That(DetMath.Atan2(0, 1), Is.EqualTo(0.0));
            Assert.That(DetMath.Atan2(1, 0), Is.EqualTo(Math.PI / 2));
            Assert.That(DetMath.Atan2(0, -1), Is.EqualTo(Math.PI));
            Assert.That(DetMath.Atan2(-1, -1), Is.EqualTo(-3 * Math.PI / 4).Within(4.5e-16));
            Assert.That(DetMath.Atan2(0, 0), Is.EqualTo(0.0));
            Assert.That(double.IsNaN(DetMath.Atan2(double.NaN, 1)), Is.True);
        }

        // sin and cos of 250° (−0.9396926207859082, −0.3420201433256694), recorded on .NET 10 and Mono alike.
        const long PinnedS3 = unchecked((long)0xBFEE11F642522D1AUL), PinnedC3 = unchecked((long)0xBFD5E3A8748A0C01UL);
    }
}
