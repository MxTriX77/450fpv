namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The camera mount's vibration for the feed (FeedSignals.CameraVibration, K§11.8; p7b-airframe 1.4): each step's
    /// acceleration of the mount, body m/s² (the rigid body's at the camera and the frame's elastic share), split into
    /// three bands by second-order Butterworth sections (bilinear, prewarped; published forms) at the 1 kHz step: X from
    /// LowCorner to 50 Hz, Y 50–200 Hz, Z above 200 Hz to the step's 500 Hz; each band's RMS over the feed's 20 ms field,
    /// the vector's magnitude. X starts at LowCorner, 5 Hz (assumed): below it the camera's motion is the flight's own,
    /// which the pose carries. An output only: nothing in the flight reads it, so its filters are not the flight's state.
    /// </summary>
    public sealed class VibrationBands
    {
        public const double LowCorner = 5, MidCorner = 50, HighCorner = 200;

        // Each section's coefficients: b0, b1, b2, a1, a2.
        readonly double[] _xHigh, _xLow, _yHigh, _yLow, _zHigh;
        // Each section's two past inputs and outputs, per axis: [section][axis * 4 + k].
        readonly double[][] _past = new double[5][];
        double _sumX, _sumY, _sumZ;
        int _count;

        public VibrationBands(double dt)
        {
            _xHigh = Section(LowCorner, dt, true);
            _xLow = Section(MidCorner, dt, false);
            _yHigh = Section(MidCorner, dt, true);
            _yLow = Section(HighCorner, dt, false);
            _zHigh = Section(HighCorner, dt, true);
            for (int i = 0; i < 5; i++) _past[i] = new double[12];
        }

        /// <summary>The last whole field's RMS per band, m/s² (X, Y, Z as FeedSignals.CameraVibration).</summary>
        public Double3 Field { get; private set; }

        /// <summary>The last step's band outputs' magnitudes, m/s² (X, Y, Z), for a scenario's look at them.</summary>
        public Double3 Last { get; private set; }

        /// <summary>One step's acceleration of the mount, body m/s²; a whole field of fieldSteps publishes the RMS.</summary>
        public void Add(in Double3 a, int fieldSteps)
        {
            double x2 = 0, y2 = 0, z2 = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                double v = axis == 0 ? a.X : axis == 1 ? a.Y : a.Z;
                double x = Run(_xLow, 1, axis, Run(_xHigh, 0, axis, v));
                double y = Run(_yLow, 3, axis, Run(_yHigh, 2, axis, v));
                double z = Run(_zHigh, 4, axis, v);
                x2 += x * x;
                y2 += y * y;
                z2 += z * z;
            }
            Last = new Double3(DetMath.Sqrt(x2), DetMath.Sqrt(y2), DetMath.Sqrt(z2));
            _sumX += x2;
            _sumY += y2;
            _sumZ += z2;
            if (++_count < fieldSteps) return;
            Field = new Double3(DetMath.Sqrt(_sumX / _count), DetMath.Sqrt(_sumY / _count), DetMath.Sqrt(_sumZ / _count));
            _sumX = _sumY = _sumZ = 0;
            _count = 0;
        }

        // One section on one axis, direct form I.
        double Run(double[] c, int section, int axis, double x)
        {
            double[] p = _past[section];
            int o = axis * 4;
            double y = c[0] * x + c[1] * p[o] + c[2] * p[o + 1] - c[3] * p[o + 2] - c[4] * p[o + 3];
            p[o + 1] = p[o];
            p[o] = x;
            p[o + 3] = p[o + 2];
            p[o + 2] = y;
            return y;
        }

        // A second-order Butterworth low- or high-pass at f Hz for the step dt: K = tan(π·f·dt).
        static double[] Section(double f, double dt, bool high)
        {
            DetMath.SinCos(3.141592653589793 * f * dt, out double sin, out double cos);
            double k = sin / cos, k2 = k * k, r2 = 1.4142135623730951 * k;
            double norm = 1 / (1 + r2 + k2);
            double a1 = 2 * (k2 - 1) * norm, a2 = (1 - r2 + k2) * norm;
            return high
                ? new[] { norm, -2 * norm, norm, a1, a2 }
                : new[] { k2 * norm, 2 * k2 * norm, k2 * norm, a1, a2 };
        }
    }
}
