// Double3: the sim core's 3-vector in double precision, in D-019's world frame
// (+X east, +Y up, +Z north, left-handed). Cross follows DetMath's convention.
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// refs/godot/main:game/src/world/WorldQuery.cs, struct Double3. Its Godot-side
// Vector3 conversion is dropped (DetMath: no System.Numerics vectors), and Length
// goes through DetMath.Sqrt.
//
// The operators, Dot, Cross and Length are marked AggressiveInlining (w9 step budget, second pass): Unity's Mono
// JIT inlines only a method of a few IL bytes without it, so each operator was a call with its structs copied, and
// the player's step spent a quarter of its time there. Inlined, the operations and their order are the same.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace Fpv450.Sim
{
    public struct Double3
    {
        public double X, Y, Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Double3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double3 operator +(Double3 a, Double3 b) => new Double3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double3 operator -(Double3 a, Double3 b) => new Double3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double3 operator -(Double3 a) => new Double3(-a.X, -a.Y, -a.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double3 operator *(Double3 a, double s) => new Double3(a.X * s, a.Y * s, a.Z * s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>a x b = (ay*bz - az*by, az*bx - ax*bz, ax*by - ay*bx), as Unity's Vector3.Cross: X x Y = Z.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double3 Cross(Double3 a, Double3 b) =>
            new Double3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Length() => DetMath.Sqrt(X * X + Y * Y + Z * Z);

        public override readonly string ToString() =>
            "(" + X.ToString("R", CultureInfo.InvariantCulture) + ", " + Y.ToString("R", CultureInfo.InvariantCulture)
            + ", " + Z.ToString("R", CultureInfo.InvariantCulture) + ")";
    }
}
