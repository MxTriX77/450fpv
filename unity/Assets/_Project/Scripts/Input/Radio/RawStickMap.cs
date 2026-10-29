using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The uncalibrated map from a raw report to a StickSample, until the calibration wizard (p6-input-app 1.2) detects
    /// the channel order, reversal, endpoints and centres: AETR on the first four axes and the arm switch on the fifth
    /// (a TX12's channels 1–5: EdgeTX's default AETR order, the arm on AUX1), each axis linear over its HID logical
    /// range. A device with fewer than five axes arms on its first button. Raw values only: the core's controller does
    /// the deadband, the rates and the throttle curve (StickSample).
    /// </summary>
    public sealed class RawStickMap : IStickMap
    {
        public int Roll = 0, Pitch = 1, Throttle = 2, Yaw = 3, Arm = 4;

        public void Map(RadioReport report, RadioAxis[] axes, out StickSample s)
        {
            s = StickSample.Neutral;
            s.HostUs = report.HostUs;
            s.Roll = Centred(report, axes, Roll);
            s.Pitch = Centred(report, axes, Pitch);
            s.Yaw = Centred(report, axes, Yaw);
            s.Throttle = Throttle >= 0 && Throttle < axes.Length ? (Centred(report, axes, Throttle) + 1) / 2 : 0;
            s.Arm = Arm < axes.Length ? Centred(report, axes, Arm) > 0 : (report.Buttons & 1) != 0;
        }

        // −1 to +1 over the axis's logical range, clamped; 0 for an axis the device lacks.
        static double Centred(RadioReport report, RadioAxis[] axes, int i)
        {
            if (i < 0 || i >= axes.Length || axes[i].Max <= axes[i].Min) return 0;
            double v = (2.0 * report.Axes[i] - axes[i].Min - axes[i].Max) / ((double)axes[i].Max - axes[i].Min);
            return v < -1 ? -1 : v > 1 ? 1 : v;
        }
    }
}
