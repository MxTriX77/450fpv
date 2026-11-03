// DetMath: the one home of the sim core's deterministic math rules.
//
// Determinism (PLT-04, W-13, docs/knowledge.md section 6.10)
// - The sim core computes in double precision with the correctly rounded IEEE 754
//   basic operations only: + - * / and sqrt. They give the same bits on every x64
//   machine and runtime (CoreCLR in the .NET tools, Mono and IL2CPP in Unity).
// - System.Math is called only inside this class.
//   - Its transcendental functions (Sin, Cos, Tan, Asin, Acos, Atan, Atan2, Exp,
//     Log, Log10, Pow, Cbrt, the hyperbolics) are not correctly rounded and differ
//     between runtimes and C libraries. DetMath provides them as polynomial or
//     rational approximations built from the basic operations (ported from the
//     first attempt in phase 3).
//   - Min and Max have differed between .NET runtimes on signed zero, so they come
//     through DetMath too once needed.
// - No MathF, no Math.FusedMultiplyAdd, no System.Numerics vectors and no hardware
//   intrinsics: they fuse a*b+c into one rounding, change the precision or depend
//   on the CPU. An FMA is never implied; if one is ever wanted it is explicit here.
// - The headless replay (phase 6) is the test that these rules hold.
//
// Frames and units (D-019)
// - SI units throughout: metres, kilograms, seconds, newtons, radians.
// - World frame = Unity's: +X east, +Y up, +Z north. It is left-handed.
// - Body frame = Unity's: +x right, +y up, +z forward; origin at the arm-plane centre.
// - Cross product: the usual component formula, the same as Unity's Vector3.Cross:
//       a x b = (ay*bz - az*by, az*bx - ax*bz, ax*by - ay*bx)
//   so X x Y = Z (east x up = north). In this left-handed frame its direction
//   follows the left-hand rule, and so do rotations: a positive angle about an axis
//   turns clockwise when seen from the axis tip looking back at the origin. Hence a
//   positive rotation about +Y turns north towards east (the compass heading grows),
//   about body +x it puts the nose down, and about body +z it lifts the right side.
//   omega x r, r x F and quaternion products all use this one convention; nothing in
//   the core is right-handed.
// - Attitude (Quat): the unit quaternion that takes body vectors to world vectors,
//   v_world = q v_body q*, with the Hamilton product. Its (X, Y, Z, W) are Unity's
//   Quaternion (x, y, z, w) one to one, so a pose crosses to Unity with no conversion.
// - Body rate (omega): body axes, rad/s, positive by the rule above; q' = q (0, omega) / 2.
// - Pilot axes, for the controller, the sticks and the logs (aerospace p, q, r):
//       p, roll right (right side down)  = -omega.z
//       q, pitch up (nose up)            = -omega.x
//       r, yaw right (heading grows)     = +omega.y
//   The sticks (StickSample) use the pilot's directions: roll +1 = stick right, pitch
//   +1 = stick forward (nose down, so -q), yaw +1 = stick right.
// - Euler angles exist only for logging and the simulated pilot: ZYX yaw-pitch-roll,
//   with the wind study's tracker definitions (reference-notes/wind.md section 6.2).

namespace Fpv450.Sim
{
    public static class DetMath
    {
        /// <summary>
        /// Square root. IEEE 754 requires sqrt to be correctly rounded, so this gives
        /// the same bits on every runtime.
        /// </summary>
        public static double Sqrt(double x) => System.Math.Sqrt(x);

        /// <summary>|x|: exact (the sign bit cleared) on every runtime.</summary>
        public static double Abs(double x) => System.Math.Abs(x);

        /// <summary>
        /// The smaller of a and b: a unless b &lt; a. Plain comparisons, so a NaN or a
        /// signed zero gives the same bits on every runtime (System.Math.Min has not).
        /// </summary>
        public static double Min(double a, double b) => b < a ? b : a;

        /// <summary>The larger of a and b: a unless b &gt; a (the rule of Min).</summary>
        public static double Max(double a, double b) => b > a ? b : a;

        /// <summary>
        /// The cube root, by Newton's iteration on the basic operations, so every runtime gives the same bits
        /// (System.Math.Cbrt is not). x = m·8^k with m in [1, 8) by exact steps of 8; eight Newton steps from 1.5
        /// take m's root to within an ulp, and the root's 2^k goes back on exactly. 0, NaN and ±∞ are their own roots.
        /// </summary>
        public static double Cbrt(double x)
        {
            if (x < 0) return -Cbrt(-x);
            if (!(x > 0) || x > double.MaxValue) return x;
            double m = x, scale = 1;
            while (m >= 8) { m *= 0.125; scale *= 2; }
            while (m < 1) { m *= 8; scale *= 0.5; }
            double y = 1.5;
            for (int i = 0; i < 8; i++) y += (m / (y * y) - y) / 3;
            return y * scale;
        }

        // π/2 in two parts (fdlibm's pio2_1 and pio2_1t, published): the first has 33 bits, so k·PiOver2Hi is exact
        // for |k| < 2^20 and the reduction loses nothing there.
        const double PiOver2Hi = 1.57079632673412561417e+00, PiOver2Lo = 6.07710050650619224932e-11;
        const double TwoOverPi = 6.36619772367581382433e-01;

        /// <summary>
        /// sin x and cos x, x in radians, |x| below about 1.6e6, by the basic operations only, so every runtime gives
        /// the same bits (System.Math's are not correctly rounded). x = k·π/2 + r with |r| ≤ π/4 (Cody–Waite, exact
        /// for that range), then the Taylor series of both to r^17 and r^16, whose next terms are below 1e-19, and
        /// the quadrant k picks and signs them.
        /// </summary>
        public static void SinCos(double x, out double sin, out double cos)
        {
            double k = System.Math.Round(x * TwoOverPi); // exact: rounding to an integer is one correctly rounded op
            double r = x - k * PiOver2Hi - k * PiOver2Lo, r2 = r * r;
            double s = r * (1 + r2 * (-1.0 / 6 + r2 * (1.0 / 120 + r2 * (-1.0 / 5040 + r2 * (1.0 / 362880
                + r2 * (-1.0 / 39916800 + r2 * (1.0 / 6227020800 + r2 * (-1.0 / 1307674368000 + r2 * (1.0 / 355687428096000)))))))));
            double c = 1 + r2 * (-0.5 + r2 * (1.0 / 24 + r2 * (-1.0 / 720 + r2 * (1.0 / 40320 + r2 * (-1.0 / 3628800
                + r2 * (1.0 / 479001600 + r2 * (-1.0 / 87178291200 + r2 * (1.0 / 20922789888000))))))));
            long q = (long)k & 3;
            sin = q == 0 ? s : q == 1 ? c : q == 2 ? -s : -c;
            cos = q == 0 ? c : q == 1 ? -s : q == 2 ? -c : s;
        }

        // ln 2 in two parts (fdlibm's ln2_hi and ln2_lo, published): the first has 32 bits, so k·Ln2Hi is exact for
        // |k| < 2^21.
        const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10;
        const double Sqrt2 = 1.4142135623730951;

        /// <summary>
        /// The natural logarithm, by the basic operations only (System.Math.Log is not correctly rounded). x = m·2^e
        /// with m in [√½, √2), e read from the bits exactly (a subnormal is first scaled by 2^54, exactly); then
        /// ln m = 2·atanh(s), s = (m − 1)/(m + 1), |s| ≤ 0.172, by its series to s^25 (the next term below 2e-21), and
        /// e·ln 2 in two parts. 0 gives −∞, a negative or NaN gives NaN, +∞ itself.
        /// </summary>
        public static double Log(double x)
        {
            if (!(x > 0)) return x == 0 ? double.NegativeInfinity : double.NaN;
            if (x > double.MaxValue) return x;
            long e = 0;
            if (x < 2.2250738585072014e-308) { x *= 18014398509481984.0; e = -54; } // 2^54
            long bits = System.BitConverter.DoubleToInt64Bits(x);
            e += ((bits >> 52) & 0x7FF) - 1023;
            double m = System.BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            if (m >= Sqrt2) { m *= 0.5; e++; }
            double s = (m - 1) / (m + 1), s2 = s * s;
            double series = s2 * (1.0 / 3 + s2 * (1.0 / 5 + s2 * (1.0 / 7 + s2 * (1.0 / 9 + s2 * (1.0 / 11 + s2 * (1.0 / 13
                + s2 * (1.0 / 15 + s2 * (1.0 / 17 + s2 * (1.0 / 19 + s2 * (1.0 / 21 + s2 * (1.0 / 23 + s2 * (1.0 / 25))))))))))));
            return e * Ln2Hi + (2 * s + (2 * s * series + e * Ln2Lo));
        }

        /// <summary>
        /// e^x, by the basic operations only (System.Math.Exp is not correctly rounded). x = k·ln 2 + r with |r| ≤ ln 2 / 2
        /// (ln 2 in two parts), e^r by its Taylor series to r^17 (the next term below 1e-23), then 2^k exactly, in two
        /// steps where k leaves the normal range. Overflow gives +∞, underflow 0, NaN itself.
        /// </summary>
        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return x;
            if (x > 709.8) return double.PositiveInfinity;
            if (x < -745.2) return 0;
            double k = System.Math.Round(x * 1.4426950408889634); // 1/ln 2; rounding to an integer is exact
            double r = x - k * Ln2Hi - k * Ln2Lo;
            double p = 1 + r * (1 + r * (1.0 / 2 + r * (1.0 / 6 + r * (1.0 / 24 + r * (1.0 / 120 + r * (1.0 / 720 + r * (1.0 / 5040
                + r * (1.0 / 40320 + r * (1.0 / 362880 + r * (1.0 / 3628800 + r * (1.0 / 39916800 + r * (1.0 / 479001600
                + r * (1.0 / 6227020800 + r * (1.0 / 87178291200 + r * (1.0 / 1307674368000 + r * (1.0 / 20922789888000
                + r * (1.0 / 355687428096000)))))))))))))))));
            long n = (long)k;
            if (n > 1023) { p *= 8.98846567431158e307; n -= 1023; } // 2^1023
            if (n < -1022) { p *= 2.2250738585072014e-308; n += 1022; } // 2^-1022
            return p * System.BitConverter.Int64BitsToDouble((n + 1023) << 52);
        }

        const double Pi = 3.141592653589793, HalfPi = 1.5707963267948966, SixthPi = 0.5235987755982988;
        const double Sqrt3 = 1.7320508075688772, TanTwelfthPi = 0.2679491924311228; // 2 − √3

        /// <summary>
        /// The angle of (x, y) from +x towards +y, radians in [−π, π], by the basic operations only (System.Math.Atan2
        /// is not correctly rounded). t = min(|x|, |y|)/max(|x|, |y|) in [0, 1]; above tan(π/12) it is turned back by
        /// π/6, t' = (√3·t − 1)/(t + √3), so |t'| ≤ tan(π/12); atan t' by its series to t'^29 (the next term below 4e-18
        /// of it); then the octant and the signs. (0, 0) gives 0; NaN gives NaN.
        /// </summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            double ax = Abs(x), ay = Abs(y);
            if (ax == 0 && ay == 0) return 0;
            bool steep = ay > ax;
            double t = steep ? ax / ay : ay / ax, offset = 0;
            if (t > TanTwelfthPi)
            {
                t = (Sqrt3 * t - 1) / (t + Sqrt3);
                offset = SixthPi;
            }
            double t2 = t * t;
            double a = t * (1 + t2 * (-1.0 / 3 + t2 * (1.0 / 5 + t2 * (-1.0 / 7 + t2 * (1.0 / 9 + t2 * (-1.0 / 11 + t2 * (1.0 / 13
                + t2 * (-1.0 / 15 + t2 * (1.0 / 17 + t2 * (-1.0 / 19 + t2 * (1.0 / 21 + t2 * (-1.0 / 23 + t2 * (1.0 / 25
                + t2 * (-1.0 / 27 + t2 * (1.0 / 29)))))))))))))));
            double r = offset + a;
            if (steep) r = HalfPi - r;
            if (x < 0) r = Pi - r;
            return y < 0 ? -r : r;
        }
    }
}
