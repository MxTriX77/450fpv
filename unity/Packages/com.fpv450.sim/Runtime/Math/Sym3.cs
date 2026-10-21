namespace Fpv450.Sim
{
    /// <summary>
    /// A symmetric 3 × 3 matrix of doubles: an inertia tensor in body axes (kg·m²), with its products of inertia as
    /// the off-diagonal entries (XY = −Σ m·x·y, and so on). Basic IEEE operations only.
    /// </summary>
    public struct Sym3
    {
        public double XX, YY, ZZ, XY, XZ, YZ;

        public Sym3(double xx, double yy, double zz, double xy, double xz, double yz)
        {
            XX = xx;
            YY = yy;
            ZZ = zz;
            XY = xy;
            XZ = xz;
            YZ = yz;
        }

        public static Sym3 operator +(Sym3 a, Sym3 b) =>
            new Sym3(a.XX + b.XX, a.YY + b.YY, a.ZZ + b.ZZ, a.XY + b.XY, a.XZ + b.XZ, a.YZ + b.YZ);

        public static Sym3 operator *(Sym3 a, double s) =>
            new Sym3(a.XX * s, a.YY * s, a.ZZ * s, a.XY * s, a.XZ * s, a.YZ * s);

        public static Double3 operator *(Sym3 a, Double3 v) => new Double3(
            a.XX * v.X + a.XY * v.Y + a.XZ * v.Z,
            a.XY * v.X + a.YY * v.Y + a.YZ * v.Z,
            a.XZ * v.X + a.YZ * v.Y + a.ZZ * v.Z);

        /// <summary>The parallel-axis term of a unit point mass at offset d: |d|²·1 − d·dᵀ.</summary>
        public static Sym3 PointMass(Double3 d)
        {
            double xx = d.X * d.X, yy = d.Y * d.Y, zz = d.Z * d.Z;
            return new Sym3(yy + zz, xx + zz, xx + yy, -(d.X * d.Y), -(d.X * d.Z), -(d.Y * d.Z));
        }

        /// <summary>The inverse, by the adjugate over the determinant. The caller keeps the matrix positive definite.</summary>
        public readonly Sym3 Inverse()
        {
            double cxx = YY * ZZ - YZ * YZ;
            double cxy = XZ * YZ - XY * ZZ;
            double cxz = XY * YZ - XZ * YY;
            double det = XX * cxx + XY * cxy + XZ * cxz;
            double s = 1.0 / det;
            return new Sym3(
                cxx * s,
                (XX * ZZ - XZ * XZ) * s,
                (XX * YY - XY * XY) * s,
                cxy * s,
                cxz * s,
                (XY * XZ - XX * YZ) * s);
        }

        public override readonly string ToString()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "(xx " + XX.ToString("R", c) + ", yy " + YY.ToString("R", c) + ", zz " + ZZ.ToString("R", c)
                + ", xy " + XY.ToString("R", c) + ", xz " + XZ.ToString("R", c) + ", yz " + YZ.ToString("R", c) + ")";
        }
    }
}
