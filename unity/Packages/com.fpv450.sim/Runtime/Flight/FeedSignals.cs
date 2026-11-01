using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The physics' signals, as bits of FeedSignals.Live. Each bit is 1 &lt;&lt; the index of the same name in p4-feed's
    /// FeedSignal enum, so the feed's SourceOf(signal) is (Live &amp; 1 &lt;&lt; signal) != 0. Bits 11–13 and 16 are the
    /// feed's signals the physics never publishes (scene light, sun, time of day, OSD state).
    /// </summary>
    [Flags]
    public enum FeedSignalSet : uint
    {
        None = 0,
        MotorElectricalHz = 1u << 0,
        MotorCurrent = 1u << 1,
        MotorCurrentStep = 1u << 2,
        PackVolts = 1u << 3,
        CameraAngularVelocity = 1u << 4,
        CameraVibration = 1u << 5,
        Impact = 1u << 6,
        FiberTension = 1u << 7,
        FiberMinBendRadius = 1u << 8,
        LinkState = 1u << 9,
        LinkMargin = 1u << 10,
        RainRate = 1u << 14,
        AirVelocityAtCamera = 1u << 15,
    }

    /// <summary>The fiber link's state; the values of p4-feed's LinkState.</summary>
    public enum LinkState : byte { Intact, Broken }

    /// <summary>
    /// The causes the analog feed reads from the physics (K§11.8, D-008), published with the pose after every step
    /// (PosePair.Signals). The names and SI units are p4-feed's IFeedSignals, so its adapter copies field for field
    /// (narrowing to float on the Unity side; the core stays in double). Live says which signals the physics
    /// computes; a signal whose bit is clear is a stub: its fields stay zero and the feed shows its labelled stand-in.
    ///
    /// Who fills each signal:
    /// - CameraAngularVelocity: filled (1.1)
    /// - MotorElectricalHz, MotorCurrent (each ESC's draw from the packs), MotorCurrentStep, PackVolts: filled (1.4),
    ///   for a body with propulsion · Impact and Crashed: 1.6 (design 1's list)
    /// - AirVelocityAtCamera: filled (1.5), from p7c-wind's field at the setup's CameraPosition (1.7)
    /// - RainRate: filled (p7c-wind 1.7), the setup's rain: the flight's drawn rate, 0 when Sunny
    /// - CameraVibration: filled (p7b-airframe 1.4, PR-1's throttle-punch shake) with the airframe's modes (VibrationBands);
    ///   with them CameraAngularVelocity gains the frame's elastic turn
    /// - FiberTension, FiberMinBendRadius, LinkState, LinkMargin: stub; 7d
    /// K§11.8's other rows are not the physics': scene light, sun and time of day (the renderer), the OSD state (the
    /// app).
    ///
    /// "Field mean" and "field max" are over the latest whole 20 ms window of sim time (steps 20k to 20k + 19): the
    /// PAL field rate, aligned to the flight, never to the render clock.
    /// </summary>
    public struct FeedSignals
    {
        /// <summary>The signals the physics computes; the rest are stubs.</summary>
        public FeedSignalSet Live;

        /// <summary>The step these values belong to.</summary>
        public long Step;

        /// <summary>Motor electrical frequency per corner, Hz (rpm / 60 × pole pairs), field mean.</summary>
        public Corners MotorElectricalHz;

        /// <summary>Motor current per corner, A, field mean.</summary>
        public Corners MotorCurrentA;

        /// <summary>Total motor current, A: field mean and field max.</summary>
        public double MotorCurrentTotalA, MotorCurrentTotalMaxA;

        /// <summary>The total current's step-to-step change, A/s, field max.</summary>
        public double MotorCurrentStepAPerS;

        /// <summary>The packs' loaded voltage at the ESCs, V, field mean.</summary>
        public double PackVolts;

        /// <summary>The OSD's low-voltage warning (PHY-11, p7b-airframe 1.5), part of the PackVolts signal: the field's
        /// loaded voltage below LowCellVolts a cell. The raw comparison; Betaflight's display filter and hysteresis are the
        /// OSD's.</summary>
        public bool PackLow;

        /// <summary>The low-voltage warning's threshold, V a cell (pilot: the FC profile's vbat_warning_cell_voltage,
        /// 3.00 V, Li-ion; reference-notes/fc-profiles.md).</summary>
        public const double LowCellVolts = 3.00;

        /// <summary>The camera's angular velocity in its own axes (the body axes: 0° uptilt), rad/s, at this step.</summary>
        public Double3 CameraAngularVelocity;

        /// <summary>The camera mount's acceleration RMS over the field, m/s²: X below 50 Hz, Y 50–200 Hz, Z above 200 Hz.</summary>
        public Double3 CameraVibration;

        /// <summary>Impacts so far this flight; each new one adds 1, with its peak and impulse below.</summary>
        public int ImpactCount;

        /// <summary>The latest impact's peak acceleration, m/s², and rigid-body impulse, N·s.</summary>
        public double ImpactPeakAccel, ImpactImpulse;

        /// <summary>The flight has ended in a crash (1.6).</summary>
        public bool Crashed;

        /// <summary>Fiber tension at the spool exit, N.</summary>
        public double FiberTensionN;

        /// <summary>The fiber's minimum bend radius along the paid-out length, m.</summary>
        public double FiberMinBendRadiusM;

        public LinkState LinkState;

        /// <summary>The link's optical margin above the receiver threshold, dB.</summary>
        public double LinkMarginDb;

        /// <summary>The rain's rate, mm/h (FEED-13's lens drops); 0 when Sunny.</summary>
        public double RainRateMmPerHour;

        /// <summary>The air's velocity at the camera, m/s, in the camera's axes.</summary>
        public Double3 AirVelocityAtCamera;
    }
}
