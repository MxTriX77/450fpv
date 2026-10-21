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
    }
}
