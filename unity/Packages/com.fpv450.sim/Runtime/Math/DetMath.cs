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

namespace Fpv450.Sim
{
    public static class DetMath
    {
        /// <summary>
        /// Square root. IEEE 754 requires sqrt to be correctly rounded, so this gives
        /// the same bits on every runtime.
        /// </summary>
        public static double Sqrt(double x) => System.Math.Sqrt(x);
    }
}
