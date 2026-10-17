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
    }
}
