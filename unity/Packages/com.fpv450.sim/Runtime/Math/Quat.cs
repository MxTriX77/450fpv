namespace Fpv450.Sim
{
    /// <summary>
    /// A rotation quaternion of doubles: the attitude, body to world (DetMath.cs, "Frames and units"). The components
    /// are Unity's Quaternion (x, y, z, w) one to one, so the renderer copies them with no conversion. Basic IEEE
    /// operations only.
    /// </summary>
    public struct Quat
    {
        public double X, Y, Z, W;

        public Quat(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Quat Identity = new Quat(0, 0, 0, 1);

        /// <summary>The Hamilton product a·b: rotate by b, then by a (the order of Unity's a * b).</summary>
        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public readonly Quat Conjugate() => new Quat(-X, -Y, -Z, W);

        public static double Dot(Quat a, Quat b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

        public readonly double Norm() => DetMath.Sqrt(X * X + Y * Y + Z * Z + W * W);

        public readonly Quat Normalized()
        {
            double s = 1.0 / Norm();
            return new Quat(X * s, Y * s, Z * s, W * s);
        }

        /// <summary>v rotated by this unit quaternion: body to world for the attitude. v + 2w(u × v) + 2u × (u × v).</summary>
        public readonly Double3 Rotate(Double3 v)
        {
            var u = new Double3(X, Y, Z);
            Double3 t = Double3.Cross(u, v) * 2.0;
            return v + t * W + Double3.Cross(u, t);
        }

        /// <summary>v rotated by the inverse: world to body for the attitude.</summary>
        public readonly Double3 InverseRotate(Double3 v) => Conjugate().Rotate(v);

        /// <summary>
        /// One step of q̇ = ½·q·(0, ω) for ω in body axes (rad/s), then renormalised: q + ½·dt·q·(0, ω), divided by
        /// its norm. First order; per step the angle is 2·atan(|ω|·dt/2) for |ω|·dt, a relative error of (|ω|·dt)²/12.
        /// </summary>
        public static Quat Integrate(Quat q, Double3 bodyRate, double dt)
        {
            double h = 0.5 * dt;
            double x = bodyRate.X * h, y = bodyRate.Y * h, z = bodyRate.Z * h;
            var dq = new Quat(
                q.W * x + q.Y * z - q.Z * y,
                q.W * y - q.X * z + q.Z * x,
                q.W * z + q.X * y - q.Y * x,
                -(q.X * x + q.Y * y + q.Z * z));
            return new Quat(q.X + dq.X, q.Y + dq.Y, q.Z + dq.Z, q.W + dq.W).Normalized();
        }

        public override readonly string ToString()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "(" + X.ToString("R", c) + ", " + Y.ToString("R", c) + ", " + Z.ToString("R", c) + ", " + W.ToString("R", c) + ")";
        }
    }
}
