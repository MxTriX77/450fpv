namespace Fpv450.Sim.Control
{
    /// <summary>
    /// One radio report after calibration, as the input layer writes it (INP-04, p6-input-app). Directions are the
    /// pilot's (DetMath.cs, "Pilot axes"). In Betaflight's terms an axis is (rcData − 1500) / 500 and the throttle
    /// (rcData − 1000) / 1000; the core's controller does the deadband, the rates and the throttle curve, so the
    /// input layer maps the raw report through the calibration only, once.
    ///
    /// The timing contract: the input thread stamps each report with HostClock.NowUs() on arrival and writes it to the
    /// StickInbox, with non-decreasing stamps. The sample in force at a step is the latest whose HostUs is at or
    /// before the step's host time (FlightScheduler.StepHostUs), so a step never sees a report from its future and
    /// the render rate never enters. The scheduler logs the sample in force at every step, and replay reads that log.
    /// </summary>
    public struct StickSample
    {
        /// <summary>Arrival time on HostClock, µs.</summary>
        public long HostUs;

        /// <summary>−1 to +1: +1 is full right stick (roll right).</summary>
        public double Roll;

        /// <summary>−1 to +1: +1 is full forward stick (nose down).</summary>
        public double Pitch;

        /// <summary>−1 to +1: +1 is full right stick (yaw right, the heading grows).</summary>
        public double Yaw;

        /// <summary>0 to 1: 0 is the stick fully down.</summary>
        public double Throttle;

        /// <summary>The arm switch is on.</summary>
        public bool Arm;

        /// <summary>The sample in force before the first report arrives: centred, throttle down, disarmed.</summary>
        public static readonly StickSample Neutral = default;
    }
}
