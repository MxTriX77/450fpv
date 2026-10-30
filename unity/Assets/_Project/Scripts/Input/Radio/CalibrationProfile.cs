using System;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>One stick's calibration, in its HID axis's raw values (p6-input-app design 4).</summary>
    public sealed class StickCalibration
    {
        /// <summary>The report's axis that carries the stick (a TX12's channel − 1); −1 for none.</summary>
        public int Axis = -1;

        /// <summary>The raw value falls as the stick goes right, forward or up.</summary>
        public bool Reversed;

        /// <summary>The endpoints, Min below Max, and the rest (unused for the throttle).</summary>
        public int Min, Centre, Max;

        /// <summary>Raw counts either side of Centre that read as centre: the stick's measured rest noise.</summary>
        public int Deadband;

        public StickCalibration Clone() => (StickCalibration)MemberwiseClone();
    }

    public enum ArmControl { None, Axis, Button }

    /// <summary>The arm switch: an axis above or below a threshold, or a button pressed or released.</summary>
    public sealed class ArmCalibration
    {
        public ArmControl Control;

        /// <summary>The axis or the button (bit) index.</summary>
        public int Index;

        /// <summary>An axis's raw value halfway between off and on.</summary>
        public int Threshold;

        /// <summary>On above the threshold (an axis), or on while pressed (a button).</summary>
        public bool OnHigh = true;

        public ArmCalibration Clone() => (ArmCalibration)MemberwiseClone();
    }

    /// <summary>
    /// A named calibration profile (INP-02): for each stick, the axis, reversal, endpoints, centre and deadband, and the
    /// arm switch. CalibrationStore keeps profiles as JSON in the user's profile folder.
    /// </summary>
    public sealed class CalibrationProfile
    {
        public string Name = "", Device = "";
        public StickCalibration Roll = new StickCalibration(), Pitch = new StickCalibration();
        public StickCalibration Yaw = new StickCalibration(), Throttle = new StickCalibration();
        public ArmCalibration Arm = new ArmCalibration();

        /// <summary>The stick channels in report order, Betaflight's letters: "AETR", "TAER" or any other.</summary>
        public string Order
        {
            get
            {
                var letters = new[] { 'A', 'E', 'T', 'R' };
                var axes = new[] { Roll.Axis, Pitch.Axis, Throttle.Axis, Yaw.Axis };
                Array.Sort(axes, letters);
                return new string(letters);
            }
        }

        public CalibrationProfile Clone() => new CalibrationProfile
        {
            Name = Name, Device = Device,
            Roll = Roll.Clone(), Pitch = Pitch.Clone(), Yaw = Yaw.Clone(), Throttle = Throttle.Clone(),
            Arm = Arm.Clone(),
        };
    }

    /// <summary>
    /// The calibrated map (in place of RawStickMap once the pilot has a profile): roll, pitch and yaw −1 to +1 from
    /// the centre to each endpoint, piecewise, so an off-centre rest still reads 0 and each end reads ±1; inside the
    /// deadband 0, outside it linear to the ends, so there is no step. The throttle is 0 to 1 between its endpoints.
    /// The core's controller still applies Betaflight's own deadband, rates and throttle curve (StickSample). It keeps
    /// a copy of the profile, so it never changes in use, and allocates nothing.
    /// </summary>
    public sealed class CalibratedStickMap : IStickMap
    {
        readonly CalibrationProfile _p;

        public CalibratedStickMap(CalibrationProfile profile) => _p = (profile ?? throw new ArgumentNullException(nameof(profile))).Clone();

        public void Map(RadioReport report, RadioAxis[] axes, out StickSample s)
        {
            s = StickSample.Neutral;
            s.HostUs = report.HostUs;
            s.Roll = Centred(report, _p.Roll);
            s.Pitch = Centred(report, _p.Pitch);
            s.Yaw = Centred(report, _p.Yaw);
            s.Throttle = Throttle(report, _p.Throttle);
            s.Arm = Armed(report, _p.Arm);
        }

        /// <summary>−1 to +1 (+1 right, forward); 0 for an axis the report lacks.</summary>
        public static double Centred(RadioReport report, StickCalibration c)
        {
            if (c.Axis < 0 || c.Axis >= report.Axes.Length) return 0;
            int d = report.Axes[c.Axis] - c.Centre;
            double v;
            if (d > c.Deadband) v = (d - c.Deadband) / (double)Math.Max(1, c.Max - c.Centre - c.Deadband);
            else if (d < -c.Deadband) v = (d + c.Deadband) / (double)Math.Max(1, c.Centre - c.Min - c.Deadband);
            else v = 0;
            if (c.Reversed) v = -v;
            return v < -1 ? -1 : v > 1 ? 1 : v;
        }

        /// <summary>0 (down) to 1 (up); 0 for an axis the report lacks.</summary>
        public static double Throttle(RadioReport report, StickCalibration c)
        {
            if (c.Axis < 0 || c.Axis >= report.Axes.Length || c.Max <= c.Min) return 0;
            double v = (report.Axes[c.Axis] - c.Min) / (double)(c.Max - c.Min);
            if (c.Reversed) v = 1 - v;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        public static bool Armed(RadioReport report, ArmCalibration a)
        {
            switch (a.Control)
            {
                case ArmControl.Axis:
                    return a.Index >= 0 && a.Index < report.Axes.Length && (report.Axes[a.Index] > a.Threshold) == a.OnHigh;
                case ArmControl.Button:
                    return a.Index >= 0 && a.Index < 64 && (((report.Buttons >> a.Index) & 1) != 0) == a.OnHigh;
                default:
                    return false;
            }
        }
    }
}
