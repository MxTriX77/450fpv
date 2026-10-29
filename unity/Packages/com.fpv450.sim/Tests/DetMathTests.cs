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
    }
}
