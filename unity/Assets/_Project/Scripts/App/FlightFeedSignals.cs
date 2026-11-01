using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Num = System.Numerics;
using SimSignals = Fpv450.Sim.Flight.FeedSignals;

namespace Fpv450.App
{
    /// <summary>
    /// The feed's signals in a flight (FEED-03, D-008): every signal the physics publishes (PosePair.Signals, its Live
    /// bits) from the physics, labelled so, narrowed to float; every other one the feed's labelled stub. The OSD is the
    /// feed's stand-in layout (StubOsd, "Svinoriz 10 Opto" and "! SAFE !") with the pack and the current from these
    /// signals and the altitude, the distance and the timer from the flight, until the OSD content model (p4-feed 1.5).
    /// </summary>
    public sealed class FlightFeedSignals : IFeedSignals, IFieldOsd
    {
        public readonly StubFeedSignals Stub = new StubFeedSignals();
        SimSignals _s;

        /// <summary>Main thread, once a frame: the latest step's signals, and the OSD's flight values (m, m, s).</summary>
        public void Update(in SimSignals signals, double altitudeM, double distanceM, double flightSeconds)
        {
            _s = signals;
            StubOsd osd = Stub.Osd;
            osd.AltitudeM = (float)altitudeM;
            osd.DistanceM = (float)distanceM;
            osd.ElapsedSeconds = flightSeconds;
        }

        bool Live(FeedSignal signal) => ((uint)_s.Live & (1u << (int)signal)) != 0;

        public SignalSource SourceOf(FeedSignal signal) => Live(signal) ? SignalSource.Physics : Stub.SourceOf(signal);

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
        public char OsdCell(int row, int col) => Stub.OsdCell(row, col);
        public void BuildOsd(int field) => Stub.Osd.Build(this, field);

        static Num.Vector3 V(in Double3 v) => new Num.Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
