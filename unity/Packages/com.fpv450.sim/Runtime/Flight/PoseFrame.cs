using System;

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

        /// <summary>
        /// The legs as the app draws them (p7g-legs 1.3): each leg's state (Contact.LegFlags) and each corner's
        /// (Contact.CornerState), a byte a corner, rear right in the low byte; each leg's set (body rad: x and z its bend at the
        /// clamp's edge, the foot's way times the angle; y its twist in the clamp; LegDamage.SetRotation turns it about the
        /// clamp's edge), kept as it was when the leg left; and each hanging skid half's fold about the other leg's bore (body
        /// rotation vector). Intact legs are all 0.
        /// </summary>
        public uint LegState, CornerState;
        public Corners3 LegSet, SkidFold;

        /// <summary>The pieces the legs dropped, where each one is now.</summary>
        public LoosePoses Loose;

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
                LegState = b.LegState,
                CornerState = b.CornerState,
                LegSet = b.LegSet,
                SkidFold = b.SkidFold,
                Loose = b.Loose,
            };
            for (int i = 0; i < LoosePoses.Slots; i++)
            {
                LoosePose pa = a.Loose[i], pb = b.Loose[i];
                if (pa.Parts == 0 || pa.Parts != pb.Parts) continue;
                Quat la = pa.Attitude, lb = pb.Attitude;
                if (Quat.Dot(la, lb) < 0) lb = new Quat(-lb.X, -lb.Y, -lb.Z, -lb.W);
                pb.Position = pa.Position + (pb.Position - pa.Position) * t;
                pb.Attitude = new Quat(la.X + (lb.X - la.X) * t, la.Y + (lb.Y - la.Y) * t, la.Z + (lb.Z - la.Z) * t, la.W + (lb.W - la.W) * t).Normalized();
                result.Loose[i] = pb;
            }
        }
    }

    /// <summary>One dropped piece as the app draws it (p7g-legs 1.3): its parts (Contact.LegChange's mask: bit c a leg's whole
    /// tube, 4 + c its part below the break, 8 + c a T-joint, 12 the right skid, 13 the left; 0 for an empty slot) and its frame,
    /// the drone's body frame as it left (each part as the drone drew it then, turned by its leg's set).</summary>
    public struct LoosePose
    {
        public int Parts;
        public Double3 Position;
        public Quat Attitude;
        public bool AtRest;
    }

    /// <summary>The dropped pieces' slots (Contact.LoosePieces.Slots), a value type so a pose stays a snapshot.</summary>
    public struct LoosePoses
    {
        public const int Slots = 6;

        public LoosePose P0, P1, P2, P3, P4, P5;

        public LoosePose this[int i]
        {
            readonly get
            {
                switch (i)
                {
                    case 0: return P0;
                    case 1: return P1;
                    case 2: return P2;
                    case 3: return P3;
                    case 4: return P4;
                    case 5: return P5;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
            set
            {
                switch (i)
                {
                    case 0: P0 = value; break;
                    case 1: P1 = value; break;
                    case 2: P2 = value; break;
                    case 3: P3 = value; break;
                    case 4: P4 = value; break;
                    case 5: P5 = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
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
