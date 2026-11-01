using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.UI;
using Num = System.Numerics;
using SimSignals = Fpv450.Sim.Flight.FeedSignals;

namespace Fpv450.App
{
    /// <summary>
    /// The feed's signals in a flight (FEED-03, D-008): every signal the physics publishes (PosePair.Signals, its Live
    /// bits) from the physics, labelled so, narrowed to float; every other one the feed's labelled stub. The OSD is the
    /// content model (FlightOsd, p4-feed 1.5): the arm state, the altitude, the distance and the timer from the flight,
    /// the pack and the current from these signals; its source is the pack's.
    /// </summary>
    public sealed class FlightFeedSignals : IFeedSignals, IFieldOsd
    {
        public readonly StubFeedSignals Stub = new StubFeedSignals();
        public readonly FlightOsd Osd = new FlightOsd();
        SimSignals _s;

        /// <summary>
        /// Main thread, once a frame: the latest step's signals, and the OSD's flight values (the arm state; m, m, s). The
        /// OSD's battery samples the physics' field means from its first whole field (step FlightCore.FieldSteps: before
        /// it they are 0 V and 0 A), or the stub's steady pack.
        /// </summary>
        public void Update(in SimSignals signals, bool armed, double altitudeM, double distanceM, double flightSeconds)
        {
            _s = signals;
            Osd.Flight(armed, altitudeM, distanceM, flightSeconds);
            if (!Live(FeedSignal.PackVolts)) Osd.Sample(0, PackVolts, MotorCurrentTotalA);
            else if (signals.Step >= FlightCore.FieldSteps) Osd.Sample(signals.Step / FlightCore.FieldSteps, PackVolts, MotorCurrentTotalA);
        }

        bool Live(FeedSignal signal) => ((uint)_s.Live & (1u << (int)signal)) != 0;

        public SignalSource SourceOf(FeedSignal signal) =>
            Live(signal == FeedSignal.OsdState ? FeedSignal.PackVolts : signal) ? SignalSource.Physics : Stub.SourceOf(signal);

        public float MotorElectricalHz(int motor) => Live(FeedSignal.MotorElectricalHz) ? (float)_s.MotorElectricalHz[motor] : Stub.MotorElectricalHz(motor);
        public float MotorCurrentA(int motor) => Live(FeedSignal.MotorCurrent) ? (float)_s.MotorCurrentA[motor] : Stub.MotorCurrentA(motor);
        public float MotorCurrentTotalA => Live(FeedSignal.MotorCurrent) ? (float)_s.MotorCurrentTotalA : Stub.MotorCurrentTotalA;
        public float MotorCurrentTotalMaxA => Live(FeedSignal.MotorCurrent) ? (float)_s.MotorCurrentTotalMaxA : Stub.MotorCurrentTotalMaxA;
        public float MotorCurrentStepAPerS => Live(FeedSignal.MotorCurrentStep) ? (float)_s.MotorCurrentStepAPerS : Stub.MotorCurrentStepAPerS;
        public float PackVolts => Live(FeedSignal.PackVolts) ? (float)_s.PackVolts : Stub.PackVolts;
        public Num.Vector3 CameraAngularVelocity => Live(FeedSignal.CameraAngularVelocity) ? V(_s.CameraAngularVelocity) : Stub.CameraAngularVelocity;
        public Num.Vector3 CameraVibration => Live(FeedSignal.CameraVibration) ? V(_s.CameraVibration) : Stub.CameraVibration;
        public int ImpactCount => Live(FeedSignal.Impact) ? _s.ImpactCount : Stub.ImpactCount;
        public float ImpactPeakAccel => Live(FeedSignal.Impact) ? (float)_s.ImpactPeakAccel : Stub.ImpactPeakAccel;
        public float ImpactImpulse => Live(FeedSignal.Impact) ? (float)_s.ImpactImpulse : Stub.ImpactImpulse;
        public float FiberTensionN => Live(FeedSignal.FiberTension) ? (float)_s.FiberTensionN : Stub.FiberTensionN;
        public float FiberMinBendRadiusM => Live(FeedSignal.FiberMinBendRadius) ? (float)_s.FiberMinBendRadiusM : Stub.FiberMinBendRadiusM;
        public Feed.LinkState LinkState => Live(FeedSignal.LinkState) ? (Feed.LinkState)(byte)_s.LinkState : Stub.LinkState;
        public float LinkMarginDb => Live(FeedSignal.LinkMargin) ? (float)_s.LinkMarginDb : Stub.LinkMarginDb;
        public float SceneLuminance => Stub.SceneLuminance;
        public Num.Vector3 SunDirection => Stub.SunDirection;
        public float SunVisibility => Stub.SunVisibility;
        public float TimeOfDayHours => Stub.TimeOfDayHours;
        public float RainRateMmPerHour => Stub.RainRateMmPerHour;
        public Num.Vector3 AirVelocityAtCamera => Live(FeedSignal.AirVelocityAtCamera) ? V(_s.AirVelocityAtCamera) : Stub.AirVelocityAtCamera;
        public char OsdCell(int row, int col) => Osd.Grid[row, col];
        public void BuildOsd(int field) => Osd.Build(field, LinkMarginDb);

        static Num.Vector3 V(in Double3 v) => new Num.Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
