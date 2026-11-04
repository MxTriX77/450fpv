using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fpv450.Feed
{
    /// <summary>
    /// Where a signal's value comes from: a labelled stub, the noclip camera's own measured motion (the stand-in
    /// for the physics' camera until the physics flies it, p4-feed design 4), or the physics.
    /// </summary>
    public enum SignalSource { Stub, Noclip, Physics }

    /// <summary>The fiber link's state (K§11.8).</summary>
    public enum LinkState { Intact, Broken }

    /// <summary>
    /// Every signal of K§11.8 (`docs/knowledge.md`, the signals the feed needs from the simulation), one id each, so
    /// the review overlay and the log can name each one with its source (FEED-03).
    /// </summary>
    public enum FeedSignal
    {
        /// Motor electrical frequency per motor, Hz, field mean: N3 spacing and roll.
        MotorElectricalHz,
        /// Motor current per motor and total, A, field mean and max: N3, N4, N1/N2 (weak), the N12 term.
        MotorCurrent,
        /// The total current's step, A/s, field max: N4 onset and the N12 term (weak).
        MotorCurrentStep,
        /// Pack voltage under load, V: the brownout cut, N8, the OSD.
        PackVolts,
        /// The camera's angular velocity, rad/s: C4 blur, P5 crawl.
        CameraAngularVelocity,
        /// The camera's vibration, RMS in three bands, m/s²: the throttle-punch shake (PR-1), N2/N3 hooks.
        CameraVibration,
        /// Impact events, peak m/s² and N·s: loss on a hard impact, the jolt, lens dirt.
        Impact,
        /// Fiber tension at the spool exit, N: N2, N8, N12, the N5–N7 variant.
        FiberTension,
        /// The fiber's minimum bend radius, m.
        FiberMinBendRadius,
        /// The fiber link's state.
        LinkState,
        /// The fiber link's optical margin, dB.
        LinkMargin,
        /// Scene light level, cd/m²: AE → grain, saturation, exposure, the N13 gate.
        SceneLight,
        /// Sun direction and visibility: O3, N11.
        Sun,
        /// Time of day, h: the sky and the base light.
        TimeOfDay,
        /// Rain rate, mm/h: the lens drops (FEED-13).
        RainRate,
        /// Air velocity at the camera, m/s: the lens drops (FEED-13).
        AirVelocityAtCamera,
        /// The OSD state: 30 × 16 characters, blinking cells already toggled.
        OsdState,
    }

    /// <summary>
    /// The feed's causes (FEED-01, FEED-03, D-008): the K§11.8 signals, read once per field unless a signal says
    /// otherwise. The physics' <c>FeedSignals</c> record (<c>p6-flight-core</c> design 1) fills them as its mechanisms
    /// land; until then each one is a stub, and <see cref="SourceOf"/> says which.
    /// </summary>
    public interface IFeedSignals
    {
        /// Whether `signal` comes from the physics, the noclip camera or a stub.
        SignalSource SourceOf(FeedSignal signal);

        /// Motor electrical frequency of `motor` (0–3), Hz, field mean.
        float MotorElectricalHz(int motor);
        /// Current of `motor` (0–3), A, field mean.
        float MotorCurrentA(int motor);
        /// Total motor current, A, field mean.
        float MotorCurrentTotalA { get; }
        /// Total motor current, A, field max.
        float MotorCurrentTotalMaxA { get; }
        /// The total current's step, A/s, field max.
        float MotorCurrentStepAPerS { get; }
        /// Pack voltage under load, V.
        float PackVolts { get; }
        /// The camera's angular velocity in its own axes, rad/s, per rendered frame.
        Vector3 CameraAngularVelocity { get; }
        /// The camera mount's acceleration RMS in the bands below 50 Hz, 50–200 Hz and above 200 Hz (X, Y, Z), m/s².
        Vector3 CameraVibration { get; }
        /// How many impacts there have been; each new one adds 1, with its peak and impulse below.
        int ImpactCount { get; }
        /// The latest impact's peak acceleration, m/s².
        float ImpactPeakAccel { get; }
        /// The latest impact's rigid-body impulse, N·s.
        float ImpactImpulse { get; }
        /// Fiber tension at the spool exit, N.
        float FiberTensionN { get; }
        /// The fiber's minimum bend radius along the paid-out length, m.
        float FiberMinBendRadiusM { get; }
        /// The fiber link's state.
        LinkState LinkState { get; }
        /// The fiber link's optical margin above the receiver threshold, dB.
        float LinkMarginDb { get; }
        /// Scene light level of the rendered frame before exposure, cd/m², per rendered frame.
        float SceneLuminance { get; }
        /// The sun's direction in the camera's axes (a unit vector), per rendered frame.
        Vector3 SunDirection { get; }
        /// How much of the sun is visible, 0–1, per rendered frame.
        float SunVisibility { get; }
        /// Time of day, local solar hours.
        float TimeOfDayHours { get; }
        /// Rain rate, mm/h.
        float RainRateMmPerHour { get; }
        /// Air velocity at the camera, m/s, per rendered frame.
        Vector3 AirVelocityAtCamera { get; }
        /// The OSD character at `row` (0–15) and `col` (0–29), blinking already applied.
        char OsdCell(int row, int col);
    }

    /// <summary>The log line D-008 asks for: which signals are stubbed, which the noclip camera measures and which
    /// come from the physics (the noclip list only when it has any).</summary>
    public static class FeedSignalLog
    {
        public static string Describe(IFeedSignals signals)
        {
            var stub = new StringBuilder();
            var noclip = new StringBuilder();
            var physics = new StringBuilder();
            foreach (FeedSignal s in (FeedSignal[])System.Enum.GetValues(typeof(FeedSignal)))
            {
                SignalSource source = signals.SourceOf(s);
                StringBuilder list = source == SignalSource.Physics ? physics : source == SignalSource.Noclip ? noclip : stub;
                list.Append(list.Length > 0 ? ", " : "").Append(s);
            }
            return noclip.Length > 0
                ? $"Feed signals: stub [{stub}]; noclip [{noclip}]; physics [{physics}]"
                : $"Feed signals: stub [{stub}]; physics [{physics}]";
        }

        /// One signal's value now, in its K§11.8 units, for the review overlay, which prints it beside its source
        /// (the "stub is visible as a stub" scenario). An unmodelled infinity reads "inf".
        public static string Value(IFeedSignals s, FeedSignal signal)
        {
            switch (signal)
            {
                case FeedSignal.MotorElectricalHz: return $"{PerMotor(s.MotorElectricalHz, "0")} Hz";
                case FeedSignal.MotorCurrent:
                    return $"{N(s.MotorCurrentTotalA, "0.0")} A, max {N(s.MotorCurrentTotalMaxA, "0.0")} A; {PerMotor(s.MotorCurrentA, "0.0")} A";
                case FeedSignal.MotorCurrentStep: return $"{N(s.MotorCurrentStepAPerS, "0")} A/s";
                case FeedSignal.PackVolts: return $"{N(s.PackVolts, "0.00")} V";
                case FeedSignal.CameraAngularVelocity: return $"{V(s.CameraAngularVelocity, "0.00")} rad/s";
                case FeedSignal.CameraVibration: return $"{V(s.CameraVibration, "0.0")} m/s² RMS";
                case FeedSignal.Impact:
                    return $"{N(s.ImpactCount, "0")}, last {N(s.ImpactPeakAccel, "0")} m/s², {N(s.ImpactImpulse, "0.00")} N·s";
                case FeedSignal.FiberTension: return $"{N(s.FiberTensionN, "0.0")} N";
                case FeedSignal.FiberMinBendRadius: return $"{N(s.FiberMinBendRadiusM, "0.000")} m";
                case FeedSignal.LinkState: return s.LinkState.ToString();
                case FeedSignal.LinkMargin: return $"{N(s.LinkMarginDb, "0.0")} dB";
                case FeedSignal.SceneLight: return $"{N(s.SceneLuminance, "0.0")} cd/m²";
                case FeedSignal.Sun: return $"{V(s.SunDirection, "0.00")}, {N(s.SunVisibility, "0.00")} visible";
                case FeedSignal.TimeOfDay: return $"{N(s.TimeOfDayHours, "0.00")} h";
                case FeedSignal.RainRate: return $"{N(s.RainRateMmPerHour, "0.0")} mm/h";
                case FeedSignal.AirVelocityAtCamera: return $"{V(s.AirVelocityAtCamera, "0.0")} m/s";
                case FeedSignal.OsdState:
                    int lit = 0;
                    for (int r = 0; r < StubFeedSignals.OsdRows; r++)
                        for (int c = 0; c < StubFeedSignals.OsdCols; c++)
                            if (s.OsdCell(r, c) != ' ')
                                lit++;
                    return lit == 0 ? "blank" : $"{N(lit, "0")} of {N(StubFeedSignals.OsdRows * StubFeedSignals.OsdCols, "0")} cells";
                default: throw new System.ArgumentOutOfRangeException(nameof(signal), signal, null);
            }
        }

        static string N(float v, string format) =>
            float.IsInfinity(v) ? (v > 0f ? "inf" : "-inf") : v.ToString(format, CultureInfo.InvariantCulture);

        static string V(Vector3 v, string format) => $"({N(v.X, format)}, {N(v.Y, format)}, {N(v.Z, format)})";

        static string PerMotor(System.Func<int, float> value, string format)
        {
            var text = new StringBuilder();
            for (int m = 0; m < StubFeedSignals.Motors; m++)
                text.Append(m > 0 ? " " : "").Append(N(value(m), format));
            return text.ToString();
        }
    }
}
