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

        // ---- Exp, Ln and Atan2 (p7d-tether 1.1): fdlibm 5.3's algorithms and constants (Sun Microsystems, 1993,
        // published), on the basic operations only: no bit access, the binary exponent moved by exact multiplications by
        // powers of two. Each constant's bits match fdlibm's hex comment; the splits of ln 2, π/2, π and atan(½, 1, 3/2)
        // into a head and a tail are re-derived exactly (the 1.1 as-built note). ----

        // ln 2 in two parts: Ln2Hi has 32 significant bits, so k·Ln2Hi is exact for every |k| ≤ 1075.
        const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10, InvLn2 = 1.44269504088896338700e+00;

        // e^x's rational form on |r| ≤ ln2/2: r·(e^r + 1)/(e^r − 1) ≈ 2 + P1·r² + … + P5·r^10 (Remez, |error| < 2^-59).
        const double ExpP1 = 1.66666666666666019037e-01, ExpP2 = -2.77777777770155933842e-03, ExpP3 = 6.61375632143793436117e-05,
            ExpP4 = -1.65339022054652515390e-06, ExpP5 = 4.13813679705723846039e-08;

        // ln(1 + f) = 2s + s·R(s²), s = f/(2 + f), R ≈ Lg1·s² + … + Lg7·s^14 on √2/2 ≤ 1 + f ≤ √2 (Remez, |error| < 2^-58.45).
        const double Lg1 = 6.666666666666735130e-01, Lg2 = 3.999999999940941908e-01, Lg3 = 2.857142874366239149e-01,
            Lg4 = 2.222219843214978396e-01, Lg5 = 1.818357216161805012e-01, Lg6 = 1.531383769920937332e-01, Lg7 = 1.479819860511658591e-01;

        // atan x ≈ x − x·(aT0·x² + … + aT10·x^22) on |x| ≤ 7/16 (Remez), and atan of the reduction points ½, 1, 3/2 and ∞
        // as a head and a tail.
        static readonly double[] AtanHi = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 };
        static readonly double[] AtanLo = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 };
        const double AT0 = 3.33333333333329318027e-01, AT1 = -1.99999999998764832476e-01, AT2 = 1.42857142725034663711e-01,
            AT3 = -1.11111104054623557880e-01, AT4 = 9.09088713343650656196e-02, AT5 = -7.69187620504482999495e-02,
            AT6 = 6.66107313738753120669e-02, AT7 = -5.83357013379057348645e-02, AT8 = 4.97687799461593236017e-02,
            AT9 = -3.65315727442169155270e-02, AT10 = 1.62858201153657823623e-02;

        // π in two parts and π/2.
        const double Pi = 3.1415926535897931160e+00, PiLo = 1.2246467991473531772e-16, HalfPi = 1.57079632679489655800e+00;

        const double MinNormal = 2.2250738585072014e-308, TwoTo54 = 18014398509481984.0, Sqrt2 = 1.4142135623730951;

        // 2^(2^i) and 2^-(2^i) for i = 9 … 0 (2^512 … 2), built by exact squaring.
        static readonly double[] Up = SquaredPowers(2.0), Down = SquaredPowers(0.5);

        static double[] SquaredPowers(double b)
        {
            var p = new double[10];
            for (int i = 9; i >= 0; i--) { p[i] = b; b *= b; }
            return p;
        }

        /// <summary>
        /// e^x by the basic operations only, so every runtime gives the same bits (System.Math.Exp is not correctly
        /// rounded and differs between C libraries). fdlibm's method: x = k·ln2 + r with |r| ≤ ln2/2 (Cody–Waite, ln 2 in
        /// two parts), e^r from a degree-5 Remez form in r², then 2^k put on by exact multiplications, the last of them
        /// rounding once when the result is subnormal. Within 1 ulp of e^x (fdlibm's bound; the tests measure it against
        /// the runtime's over the whole range). e^NaN is NaN; above 709.78 it is +∞, below −745.13 it is 0.
        /// </summary>
        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return x;
            if (x > 7.09782712893383973096e+02) return double.PositiveInfinity;
            if (x < -7.45133219101941108420e+02) return 0;
            double k = System.Math.Round(x * InvLn2); // exact: rounding to an integer is one correctly rounded op
            double hi = x - k * Ln2Hi, lo = k * Ln2Lo, r = hi - lo, t = r * r;
            double c = r - t * (ExpP1 + t * (ExpP2 + t * (ExpP3 + t * (ExpP4 + t * ExpP5))));
            double y = 1 - ((lo - r * c / (2 - c)) - hi);
            int n = (int)k;
            if (n > 1023) return y * TwoToThe(1023) * 2;          // y < 1 there, so the result is finite
            if (n < -1021) return y * TwoToThe(n + 1000) * TwoToThe(-1000); // exact, then one rounding into the subnormals
            return y * TwoToThe(n);
        }

        /// <summary>
        /// The natural logarithm by the basic operations only, so every runtime gives the same bits (System.Math.Log is
        /// not correctly rounded). fdlibm's method: x = 2^k·(1 + f) with √2/2 &lt; 1 + f ≤ √2, the exponent found by exact
        /// multiplications by powers of two (a subnormal x first scaled by 2^54), then ln(1 + f) from s = f/(2 + f) and a
        /// degree-7 Remez form in s², in the order that keeps its error under 1 ulp, plus k·ln2 in two parts. ln 1 = +0,
        /// ln 0 = −∞, ln +∞ = +∞, and a negative x or NaN gives NaN.
        /// </summary>
        public static double Ln(double x)
        {
            if (!(x > 0)) return x == 0 ? double.NegativeInfinity : double.NaN;
            if (x > double.MaxValue) return x;
            int k = 0;
            double m = x;
            if (m < MinNormal) { m *= TwoTo54; k = -54; }
            if (m >= 2)
            {
                for (int i = 0, e = 512; i < 10; i++, e >>= 1)
                    if (m >= Up[i]) { m *= Down[i]; k += e; }
            }
            else if (m < 1)
            {
                for (int i = 0, e = 512; i < 10; i++, e >>= 1)
                    if (m * Up[i] < 2) { m *= Up[i]; k -= e; }
            }
            if (m > Sqrt2) { m *= 0.5; k++; }
            double f = m - 1; // exact: m lies within a factor of two of 1
            double s = f / (2 + f), z = s * s, w = z * z;
            double t1 = w * (Lg2 + w * (Lg4 + w * Lg6)), t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            double r = t2 + t1, hfsq = 0.5 * f * f, dk = k;
            return dk * Ln2Hi - ((hfsq - (s * (hfsq + r) + dk * Ln2Lo)) - f);
        }

        /// <summary>
        /// The angle of the point (x, y) from the +x axis, in (−π, π], as System.Math.Atan2(y, x) defines it (the signs of
        /// zero and the infinities included), by the basic operations only so every runtime gives the same bits. fdlibm's
        /// method: atan |y/x| with |y/x| reduced about ½, 1, 3/2 or ∞ to |t| ≤ 7/16 and a degree-11 Remez form in t², then
        /// placed in its quadrant with π's tail. NaN in either gives NaN.
        /// </summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return x + y;
            bool yNegative = y < 0 || (y == 0 && 1 / y < 0), xNegative = x < 0 || (x == 0 && 1 / x < 0);
            double ay = yNegative ? -y : y, ax = xNegative ? -x : x, angle; // angle: the turn from +x, before y's sign
            if (ay == 0) angle = xNegative ? Pi : 0;
            else if (ax == 0) angle = HalfPi;
            else if (ax > double.MaxValue) angle = ay > double.MaxValue ? (xNegative ? 3 * AtanHi[1] : AtanHi[1]) : (xNegative ? Pi : 0);
            else if (ay > double.MaxValue) angle = HalfPi;
            else
            {
                double z = AtanNonNegative(ay / ax); // |y/x| may overflow to ∞ (π/2) or underflow to 0: both still right
                angle = xNegative ? Pi - (z - PiLo) : z;
            }
            return yNegative ? -angle : angle;
        }

        // atan t for t ≥ 0 (fdlibm's atan): t ≥ 2^66 is π/2; else t is reduced about 0, ½, 1, 3/2 or ∞ to |u| ≤ 7/16.
        static double AtanNonNegative(double t)
        {
            if (t >= 7.378697629483820646e+19) return AtanHi[3] + AtanLo[3];
            int id;
            if (t < 0.4375) id = -1;
            else if (t < 0.6875) { id = 0; t = (2 * t - 1) / (2 + t); }
            else if (t < 1.1875) { id = 1; t = (t - 1) / (t + 1); }
            else if (t < 2.4375) { id = 2; t = (t - 1.5) / (1 + 1.5 * t); }
            else { id = 3; t = -1 / t; }
            double z = t * t, w = z * z;
            double s1 = z * (AT0 + w * (AT2 + w * (AT4 + w * (AT6 + w * (AT8 + w * AT10)))));
            double s2 = w * (AT1 + w * (AT3 + w * (AT5 + w * (AT7 + w * AT9))));
            if (id < 0) return t - t * (s1 + s2);
            return AtanHi[id] - ((t * (s1 + s2) - AtanLo[id]) - t);
        }

        // 2^n for −1022 ≤ n ≤ 1023 by exact squaring: every factor and partial product is a normal power of two.
        static double TwoToThe(int n)
        {
            double b = n < 0 ? 0.5 : 2.0, p = 1;
            for (int m = n < 0 ? -n : n; m != 0; m >>= 1)
            {
                if ((m & 1) != 0) p *= b;
                b *= b;
            }
            return p;
        }
    }
}
