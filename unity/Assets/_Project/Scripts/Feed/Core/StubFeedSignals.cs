using System.Numerics;

namespace Fpv450.Feed
{
    /// <summary>
    /// The labelled stand-in for every K§11.8 signal (FEED-03, D-008): each one reports <see cref="SignalSource.Stub"/>
    /// until the physics publishes it, and <see cref="FeedSignalLog.Describe"/> says so in the log. It replaces the
    /// Godot feed's <c>FeedSignals</c> stand-in (<c>docs/legacy/video-feed-godot/src/FeedSignals.cs</c>).
    /// </summary>
    /// <remarks>
    /// The values are settable, so the engine side can drive them in phase 4 (the noclip camera's rates, a scripted
    /// link margin, the debug panel's triggers). The defaults are a parked drone with no fiber: nothing turns, nothing
    /// pulls, and the link can't fail. Only the pack voltage has a Godot value.
    /// </remarks>
    public sealed class StubFeedSignals : IFeedSignals, IFieldOsd
    {
        /// The Svinoriz 10 Opto is a quad.
        public const int Motors = 4;
        public const int OsdRows = 16, OsdCols = 30;

        public readonly float[] MotorHz = new float[Motors];
        public readonly float[] MotorAmps = new float[Motors];
        /// The Godot OSD's layout with invented values (task 1.4), laid out per field by <see cref="BuildOsd"/>
        /// until the OSD content model replaces it (`p4-feed` design 5, task 1.5). Blank until the first field.
        public readonly StubOsd Osd = new StubOsd();

        public SignalSource SourceOf(FeedSignal signal) => SignalSource.Stub;

        public float MotorElectricalHz(int motor) => MotorHz[motor];
        public float MotorCurrentA(int motor) => MotorAmps[motor];
        public float MotorCurrentTotalA { get; set; }
        public float MotorCurrentTotalMaxA { get; set; }
        public float MotorCurrentStepAPerS { get; set; }
        /// A full 6S pack, the Godot stand-in's (FeedSignals.cs:292); phase 5 picks the real battery.
        public float PackVolts { get; set; } = 25.2f;
        public Vector3 CameraAngularVelocity { get; set; }
        public Vector3 CameraVibration { get; set; }
        public int ImpactCount { get; set; }
        public float ImpactPeakAccel { get; set; }
        public float ImpactImpulse { get; set; }
        /// No fiber is modelled before phase 7d (D-042): no tension, a straight fiber, a margin that can't run out.
        public float FiberTensionN { get; set; }
        public float FiberMinBendRadiusM { get; set; } = float.PositiveInfinity;
        public LinkState LinkState { get; set; } = LinkState.Intact;
        public float LinkMarginDb { get; set; } = float.PositiveInfinity;
        /// Unmeasured: the AE loop measures the render (`p4-feed` task 1.7).
        public float SceneLuminance { get; set; }
        /// No sun: zero direction, nothing visible.
        public Vector3 SunDirection { get; set; }
        public float SunVisibility { get; set; }
        /// Noon (assumed); the look presets set the real one.
        public float TimeOfDayHours { get; set; } = 12f;
        /// Sunny, and still air.
        public float RainRateMmPerHour { get; set; }
        public Vector3 AirVelocityAtCamera { get; set; }

        public char OsdCell(int row, int col) => Osd.Grid[row, col];

        public void BuildOsd(int field) => Osd.Build(this, field);
    }
}
