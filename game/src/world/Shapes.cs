using System;

/// Support points of the primitives, for the wind grid's footprints. Pure C#, only correctly rounded IEEE operations
/// (W-13), no allocation.
public sealed partial class WorldQuery
{
    static double Sign(double v) => v < 0 ? -1 : 1;

    /// The point of `s` farthest along the unit direction `dir` (on a tie, the + side of each axis).
    static Double3 Support(in Prim s, Double3 dir)
    {
        switch (s.Kind)
        {
            case ShapeKind.Box:
                return s.C + s.R.X * (Sign(Double3.Dot(dir, s.R.X)) * s.Half.X) + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y)
                    + s.R.Z * (Sign(Double3.Dot(dir, s.R.Z)) * s.Half.Z);
            case ShapeKind.Sphere:
                return s.C + dir * s.Radius;
            case ShapeKind.Capsule:
                return s.C + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y) + dir * s.Radius;
            default:
            {
                Double3 end = s.C + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y), across = dir - s.R.Y * Double3.Dot(dir, s.R.Y);
                double length = across.Length();
                return length > 0 ? end + across * (s.Radius / length) : end;
            }
        }
    }
}
