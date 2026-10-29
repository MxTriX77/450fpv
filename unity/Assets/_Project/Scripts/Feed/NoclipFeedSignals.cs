using UnityEngine;
using Num = System.Numerics;

namespace Fpv450.Feed
{
    /// <summary>
    /// The feed's signals on a noclip camera, until the physics thread exists (p4-feed design 4): the camera's angular
    /// rate is measured off the camera's own motion and labelled <see cref="SignalSource.Noclip"/>; every other signal
    /// is <see cref="Stub"/>'s labelled stub. With no drone that is no motor current and an intact link, so only the
    /// Godot timers' quality drops and recovering dropouts run.
    /// </summary>
    public sealed class NoclipFeedSignals : IFeedSignals, IFieldOsd
    {
        /// Every signal but the camera's rate, settable as the stubs are.
        public readonly StubFeedSignals Stub = new StubFeedSignals();

        readonly Transform camera;
        Quaternion last;
        bool sampled;

        public NoclipFeedSignals(Transform camera) => this.camera = camera;

        /// Measures the rate over the last rendered frame, <paramref name="dt"/> seconds: the rotation since the last
        /// sample, in the camera's own axes (x right, y up, z forward, Unity's left-handed sense), over the time. The
        /// angle comes from the rotation's vector part, not its acos: a frame's turn at a few hundred fps is below
        /// float resolution in the scalar part.
        public void Sample(float dt)
        {
            Quaternion now = camera.rotation;
            if (sampled && dt > 0f)
            {
                Quaternion d = Quaternion.Inverse(last) * now;
                float sign = d.w < 0f ? -1f : 1f; // the shorter way round
                var v = new Vector3(d.x, d.y, d.z) * sign;
                float s = v.magnitude;
                Vector3 w = s > 0f ? v * (2f * Mathf.Atan2(s, d.w * sign) / (s * dt)) : Vector3.zero;
                CameraAngularVelocity = float.IsFinite(w.x + w.y + w.z) ? new Num.Vector3(w.x, w.y, w.z) : Num.Vector3.Zero;
            }
            last = now;
            sampled = true;
        }

        public SignalSource SourceOf(FeedSignal signal) =>
            signal == FeedSignal.CameraAngularVelocity ? SignalSource.Noclip : Stub.SourceOf(signal);

        public Num.Vector3 CameraAngularVelocity { get; private set; }

        public float MotorElectricalHz(int motor) => Stub.MotorElectricalHz(motor);
        public float MotorCurrentA(int motor) => Stub.MotorCurrentA(motor);
        public float MotorCurrentTotalA => Stub.MotorCurrentTotalA;
        public float MotorCurrentTotalMaxA => Stub.MotorCurrentTotalMaxA;
        public float MotorCurrentStepAPerS => Stub.MotorCurrentStepAPerS;
        public float PackVolts => Stub.PackVolts;
        public Num.Vector3 CameraVibration => Stub.CameraVibration;
        public int ImpactCount => Stub.ImpactCount;
        public float ImpactPeakAccel => Stub.ImpactPeakAccel;
        public float ImpactImpulse => Stub.ImpactImpulse;
        public float FiberTensionN => Stub.FiberTensionN;
        public float FiberMinBendRadiusM => Stub.FiberMinBendRadiusM;
        public LinkState LinkState => Stub.LinkState;
        public float LinkMarginDb => Stub.LinkMarginDb;
        public float SceneLuminance => Stub.SceneLuminance;
        public Num.Vector3 SunDirection => Stub.SunDirection;
        public float SunVisibility => Stub.SunVisibility;
        public float TimeOfDayHours => Stub.TimeOfDayHours;
        public float RainRateMmPerHour => Stub.RainRateMmPerHour;
        public Num.Vector3 AirVelocityAtCamera => Stub.AirVelocityAtCamera;
        public char OsdCell(int row, int col) => Stub.OsdCell(row, col);
        public void BuildOsd(int field) => Stub.BuildOsd(field);
    }
}
