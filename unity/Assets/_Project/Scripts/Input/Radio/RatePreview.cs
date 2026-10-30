using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The Calibrate screen's curves (INP-03, design 5): sampled from the flight controller built on the settings
    /// being edited, through its own functions (FlightController.SetpointOf and ThrottleCommandOf), so the preview is
    /// the curve the flight flies, never a copy of Betaflight's formulas. Each curve runs over the stick's upper half,
    /// 0 to 1 in Points points; the lower half mirrors it.
    /// </summary>
    public sealed class RatePreview
    {
        public const int Points = 101;

        // The mixer's spins, which the flight controller requires and its RC path never reads.
        internal static readonly bool[] AnySpins = { true, false, false, true };

        /// <summary>The setpoint, °/s, per axis (roll, pitch, yaw) at Stick(i).</summary>
        public readonly double[][] Rate = { new double[Points], new double[Points], new double[Points] };

        /// <summary>The throttle command at Stick(i), 0–1 of its range (rcCommand 1000–2000 µs).</summary>
        public readonly double[] Throttle = new double[Points];

        public static double Stick(int i) => i / (double)(Points - 1);

        /// <summary>Samples the curves of settings (which the flight controller must accept).</summary>
        public void From(in FcSettings settings)
        {
            var fc = new FlightController(settings, AnySpins);
            for (int i = 0; i < Points; i++)
            {
                double x = Stick(i);
                for (int a = 0; a < 3; a++) Rate[a][i] = fc.SetpointOf(a, x);
                Throttle[i] = (fc.ThrottleCommandOf(x) - 1000) / 1000;
            }
        }
    }
}
