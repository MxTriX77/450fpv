namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// What the scene reads of one step's state (design 1), in the frames of DetMath.cs: Position and Attitude copy to
    /// a Unity transform component for component. It is the integrated state itself, never smoothed, and nothing
    /// the renderer does feeds back into the flight.
    /// </summary>
    public struct PoseFrame
    {
        /// <summary>The state's step: sim time Step × FlightCore.Dt since the flight started.</summary>
        public long Step;

        /// <summary>World, m: the body origin (the arm-plane centre).</summary>
        public Double3 Position;

        /// <summary>Body to world.</summary>
        public Quat Attitude;

        /// <summary>World, m/s.</summary>
        public Double3 Velocity;

        /// <summary>ω in body axes, rad/s.</summary>
        public Double3 BodyRate;

        /// <summary>Rotor speed per corner, rad/s (0 until 1.4).</summary>
        public Corners RotorSpeed;

        public bool Armed;

        /// <summary>Leg compression per corner, m, positive when compressed (0 until 1.6; p5-drone draws the flex).</summary>
        public Corners LegCompression;

        /// <summary>Fiber paid out from the spool, m (0 until 7d; p5-drone draws the coil).</summary>
        public double FiberPaidOut;

        public readonly double TimeS => Step * FlightCore.Dt;

        /// <summary>
        /// The pose a fraction t (0–1) of the way from a to b: linear in everything, normalised-linear in the attitude
        /// (along the shorter arc). Render-side only: no step calls it. The discrete fields (Step, Armed) are b's.
        /// </summary>
        public static void Lerp(in PoseFrame a, in PoseFrame b, double t, out PoseFrame result)
        {
            Quat qa = a.Attitude, qb = b.Attitude;
            if (Quat.Dot(qa, qb) < 0)
                qb = new Quat(-qb.X, -qb.Y, -qb.Z, -qb.W);
            result = new PoseFrame
            {
                Step = b.Step,
                Position = a.Position + (b.Position - a.Position) * t,
                Attitude = new Quat(
                    qa.X + (qb.X - qa.X) * t,
                    qa.Y + (qb.Y - qa.Y) * t,
                    qa.Z + (qb.Z - qa.Z) * t,
                    qa.W + (qb.W - qa.W) * t).Normalized(),
                Velocity = a.Velocity + (b.Velocity - a.Velocity) * t,
                BodyRate = a.BodyRate + (b.BodyRate - a.BodyRate) * t,
                RotorSpeed = Corners.Lerp(a.RotorSpeed, b.RotorSpeed, t),
                Armed = b.Armed,
                LegCompression = Corners.Lerp(a.LegCompression, b.LegCompression, t),
                FiberPaidOut = a.FiberPaidOut + (b.FiberPaidOut - a.FiberPaidOut) * t,
            };
        }
    }

    /// <summary>
    /// One published unit of the PoseBuffer: the last two steps' poses, the feed signals of the latest step, and the
    /// host time the flight started at, which maps the renderer's clock onto sim time.
    /// </summary>
    public struct PosePair
    {
        public PoseFrame Previous, Current;
        public FeedSignals Signals;

        /// <summary>HostClock µs of step 0: step k is nominally at StartUs + k × FlightCore.StepUs.</summary>
        public long StartUs;

        /// <summary>
        /// The interpolation the renderer calls with its own clock (HostClock µs): the pose at hostUs between Previous
        /// and Current, held at Current past it (a late flight thread shows as a hold, never as an extrapolation) and
        /// at Previous before it.
        /// </summary>
        public readonly void Interpolate(long hostUs, out PoseFrame pose)
        {
            long from = StartUs + Previous.Step * FlightCore.StepUs;
            long span = (Current.Step - Previous.Step) * FlightCore.StepUs;
            double t = span <= 0 ? 1.0 : (double)(hostUs - from) / span;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            PoseFrame.Lerp(Previous, Current, t, out pose);
        }
    }
}
