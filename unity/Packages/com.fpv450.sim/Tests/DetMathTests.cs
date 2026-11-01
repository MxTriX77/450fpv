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

        // sin and cos of 250° (−0.9396926207859082, −0.3420201433256694), recorded on .NET 10 and Mono alike.
        const long PinnedS3 = unchecked((long)0xBFEE11F642522D1AUL), PinnedC3 = unchecked((long)0xBFD5E3A8748A0C01UL);

        // ---- Exp, Ln and Atan2 (p7d-tether 1.1) against System.Math, a test-only reference itself within about an ulp
        // of the truth. Each sweep covers the cable solver's domain densely (design 1, 2, 6, 7, 8: the capstan e^(±μθ),
        // the macrobend law, the peel's rate law, the drag's Re^(−2/3), every bend's and swing's angle) and then the
        // function's whole range; the worst ulp and relative error go to the test's output for the as-built note. ----

        // The distance in ulps between two doubles of the same sign, through zero for opposite signs; 0 when equal.
        static long UlpsBetween(double a, double b)
        {
            if (a == b) return 0;
            long x = BitConverter.DoubleToInt64Bits(a), y = BitConverter.DoubleToInt64Bits(b);
            if ((x < 0) != (y < 0)) return (x & long.MaxValue) + (y & long.MaxValue);
            return Math.Abs(x - y);
        }

        sealed class Worst
        {
            readonly string _name;
            public long Ulps = -1;
            public double At, Relative;
            public int Count;

            public Worst(string name) => _name = name;

            public void Add(double x, double got, double reference)
            {
                Count++;
                long u = UlpsBetween(got, reference);
                if (u > Ulps) { Ulps = u; At = x; }
                if (reference != 0 && !double.IsInfinity(reference) && Math.Abs(reference) >= 2.2250738585072014e-308)
                    Relative = Math.Max(Relative, Math.Abs(got - reference) / Math.Abs(reference));
            }

            public void Report()
            {
                TestContext.WriteLine($"{_name}: {Count} points, worst {Ulps} ulp at {At:R}, worst relative {Relative:E2}");
                Assert.That(Ulps, Is.LessThanOrEqualTo(2), _name + " at " + At.ToString("R"));
            }
        }

        [Test]
        public void Exp_IsWithinTwoUlpOfTheRuntimes()
        {
            // The solver's domain: the capstan's e^(±μθ) for μ up to 1 over several turns, the macrobend's e^(−decay·(R − R_a))
            // from +4.8 down, the peel's (ṡ/v_ref)^n and the drag's Re^(−2/3) through Exp(a·Ln x).
            var solver = new Worst("Exp on [-50, 50]");
            for (int i = 0; i <= 1000000; i++)
            {
                double x = -50 + i * 1e-4;
                solver.Add(x, DetMath.Exp(x), Math.Exp(x));
            }
            solver.Report();
            var whole = new Worst("Exp on [-745.13, 709.78] (the subnormal results included)");
            for (int i = 0; i <= 200000; i++)
            {
                double x = -745.13 + i * (1454.91 / 200000);
                whole.Add(x, DetMath.Exp(x), Math.Exp(x));
            }
            for (int k = 1; k <= 60; k++)
                foreach (double x in new[] { 1.3 * Math.Pow(2, -k), -1.3 * Math.Pow(2, -k) })
                    whole.Add(x, DetMath.Exp(x), Math.Exp(x));
            whole.Report();

            Assert.That(DetMath.Exp(0), Is.EqualTo(1.0));
            Assert.That(double.IsNaN(DetMath.Exp(double.NaN)), Is.True);
            Assert.That(DetMath.Exp(double.PositiveInfinity), Is.EqualTo(double.PositiveInfinity));
            Assert.That(DetMath.Exp(710), Is.EqualTo(double.PositiveInfinity));
            Assert.That(BitConverter.DoubleToInt64Bits(DetMath.Exp(double.NegativeInfinity)), Is.EqualTo(0L));
            Assert.That(BitConverter.DoubleToInt64Bits(DetMath.Exp(-746)), Is.EqualTo(0L));
        }

        [Test]
        public void Ln_IsWithinTwoUlpOfTheRuntimes()
        {
            // The solver's domain: the peel's rate ratio and the line's Reynolds number, 1e-6 … 1e6, and close about 1.
            var solver = new Worst("Ln on [1e-6, 1e6] and [0.5, 1.5]");
            for (int i = 0; i <= 1000000; i++)
            {
                double x = Math.Pow(10, -6 + 12.0 * i / 1000000);
                solver.Add(x, DetMath.Ln(x), Math.Log(x));
                double y = 1 + (i - 500000) * 1e-6;
                solver.Add(y, DetMath.Ln(y), Math.Log(y));
            }
            solver.Report();
            var whole = new Worst("Ln on every binade, the subnormals included");
            for (int e = -1074; e <= 1023; e++)
                for (int j = 0; j < 7; j++)
                {
                    double x = Math.Pow(2, e) * (1 + j / 7.0);
                    if (x > 0 && !double.IsInfinity(x)) whole.Add(x, DetMath.Ln(x), Math.Log(x));
                }
            foreach (double x in new[] { double.Epsilon, 1e-310, 2.2250738585072014e-308, double.MaxValue })
                whole.Add(x, DetMath.Ln(x), Math.Log(x));
            whole.Report();

            Assert.That(BitConverter.DoubleToInt64Bits(DetMath.Ln(1)), Is.EqualTo(0L), "ln 1 is +0");
            Assert.That(DetMath.Ln(0), Is.EqualTo(double.NegativeInfinity));
            Assert.That(DetMath.Ln(-0.0), Is.EqualTo(double.NegativeInfinity));
            Assert.That(DetMath.Ln(double.PositiveInfinity), Is.EqualTo(double.PositiveInfinity));
            Assert.That(double.IsNaN(DetMath.Ln(-1)), Is.True);
            Assert.That(double.IsNaN(DetMath.Ln(double.NaN)), Is.True);
        }

        [Test]
        public void Atan2_IsWithinTwoUlpOfTheRuntimes()
        {
            // The solver's domain: every direction (a bend's turn is Atan2(|a × b|, a · b), a wire's swing any quadrant), at
            // lengths from 1e-300 to 1e300.
            var circle = new Worst("Atan2 round the circle at radii 1e-300 … 1e300");
            foreach (double r in new[] { 1e-300, 1e-6, 1, 1e6, 1e300 })
                for (int i = 0; i <= 200000; i++)
                {
                    double theta = -Math.PI + 2 * Math.PI * i / 200000;
                    double x = r * Math.Cos(theta), y = r * Math.Sin(theta);
                    circle.Add(theta, DetMath.Atan2(y, x), Math.Atan2(y, x));
                }
            circle.Report();
            var ratios = new Worst("Atan2 at |y/x| from 1e-320 to 1e320 in each quadrant");
            for (int k = -320; k <= 308; k++)
            {
                double t = Math.Pow(10, k);
                foreach ((double y, double x) in new[] { (1.0, t), (t, 1.0), (-1.0, t), (t, -1.0), (-1.0, -t), (-t, -1.0) })
                    ratios.Add(y / x, DetMath.Atan2(y, x), Math.Atan2(y, x));
            }
            ratios.Report();

            // The signs of zero and the infinities as the runtime's: within an ulp, with the same sign.
            double[] specials = { 0.0, -0.0, 1, -1, double.PositiveInfinity, double.NegativeInfinity, double.NaN };
            foreach (double y in specials)
                foreach (double x in specials)
                {
                    double got = DetMath.Atan2(y, x), reference = Math.Atan2(y, x);
                    string at = "Atan2(" + y.ToString("R") + ", " + x.ToString("R") + ") = " + got.ToString("R") + " against " + reference.ToString("R");
                    if (double.IsNaN(reference))
                    {
                        Assert.That(double.IsNaN(got), Is.True, at);
                        continue;
                    }
                    Assert.That(UlpsBetween(got, reference), Is.LessThanOrEqualTo(1), at);
                    Assert.That(BitConverter.DoubleToInt64Bits(got) < 0, Is.EqualTo(BitConverter.DoubleToInt64Bits(reference) < 0), at);
                }
        }

        // Determinism: each function's bits over a fixed grid, folded into one FNV-1a hash per function and pinned, so a
        // runtime that rounded one of its basic operations differently (Unity's Mono against .NET) shows here. The grids
        // cross every branch: Exp's normal and subnormal results and both scalings, Ln's subnormal pre-scale and both
        // exponent walks, Atan2's five reductions in all four quadrants. The inputs come from the basic operations and
        // DetMath.SinCos only, so they are the same bits on every runtime too.
        [Test]
        public void Exp_Ln_Atan2_GiveTheSameBitsOnEveryRuntime()
        {
            ulong exp = Fnv.Offset, ln = Fnv.Offset, atan2 = Fnv.Offset;
            double y = 1e-320; // × 1.0739 a point: from the subnormals to 2e299
            for (int i = 0; i <= 20000; i++, y *= 1.0739)
            {
                double x = -745.13 + i * (1454.91 / 20000);
                exp = Fnv.Add(exp, DetMath.Exp(x));
                ln = Fnv.Add(ln, DetMath.Ln(y));
                DetMath.SinCos(-3.141592653589793 + 6.283185307179586 * i / 20000, out double sin, out double cos);
                atan2 = Fnv.Add(atan2, DetMath.Atan2(sin * 3.7, cos));
            }
            TestContext.WriteLine($"Exp 0x{exp:X16}, Ln 0x{ln:X16}, Atan2 0x{atan2:X16}");
            Assert.That((exp, ln, atan2), Is.EqualTo((PinnedExpGrid, PinnedLnGrid, PinnedAtan2Grid)),
                "recorded on .NET 10; a difference on Mono is a determinism bug, never a reason to re-pin");
        }

        // Recorded on .NET 10 at the first run (p7d-tether 1.1's as-built note); Unity's EditMode (Mono) must give the same.
        const ulong PinnedExpGrid = 0xCEF8CD9A2013DD53UL, PinnedLnGrid = 0x68E8C52373B86791UL, PinnedAtan2Grid = 0x86637FD7C7A86484UL;

        // FNV-1a over a double's eight bytes, little-endian.
        static class Fnv
        {
            public const ulong Offset = 0xCBF29CE484222325UL;

            public static ulong Add(ulong h, double x)
            {
                ulong bits = (ulong)BitConverter.DoubleToInt64Bits(x);
                for (int i = 0; i < 8; i++)
                {
                    h ^= (bits >> (8 * i)) & 0xFF;
                    h *= 0x100000001B3UL;
                }
                return h;
            }
        }
    }
}
