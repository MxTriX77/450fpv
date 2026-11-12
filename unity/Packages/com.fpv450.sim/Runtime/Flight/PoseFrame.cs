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

        /// <summary>Fiber paid out from the coil this flight, m (p7d-tether 1.6; p5-drone draws the coil, its pack shrinking).</summary>
        public double FiberPaidOut;

        /// <summary>The fiber's active nodes, world m, from the touchdown (node 0, the laid trail's last point) to the exit,
        /// and their count (p7d-tether 1.6, DRN-05: the line drawn through them).</summary>
        public FiberNodes FiberNodes;
        public int FiberNodeCount;

        /// <summary>The laid trail's points published with this pose (FlightCore.Tether.Trail, read up to this count; append-only
        /// between lifts).</summary>
        public int FiberTrailCount;

        /// <summary>After a break (p7d-tether 1.8): the trail's points before the far piece (the laid trail ends there and the far
        /// piece runs on to FiberTrailCount; the nodes, the near piece hanging from the exit, are not joined to it); 0 while
        /// whole. FiberNodeCount is 0 once nothing hangs from the exit.</summary>
        public int FiberBreakTrail;

        /// <summary>The frame's elastic turn at the camera, body rad (small angles about x, y, z; AirframeModes.FrameTurn),
        /// 0 for a rigid airframe: the camera on the flexing frame (p7e-camera-feel, FieldCamera.Attitude). Render-side.</summary>
        public Double3 CameraTurn;

        /// <summary>
        /// The legs as the app draws them (p7g-legs 1.3): each leg's state (Contact.LegFlags) and each corner's
        /// (Contact.CornerState), a byte a corner, rear right in the low byte; each leg's set (body rad: x and z its bend at the
        /// clamp's edge, the foot's way times the angle; y its twist in the clamp; LegDamage.SetRotation turns it about the
        /// clamp's edge), kept as it was when the leg left; and each hanging skid half's fold about the other leg's bore (body
        /// rotation vector). Intact legs are all 0.
        /// </summary>
        public uint LegState, CornerState;
        public Corners3 LegSet, SkidFold;

        /// <summary>Each leg's frame sway in its set (LegDamage.Sway, rad; p7g-legs, model 54): the part of the set's z its skid
        /// half does not turn by (LegDamage.HalfRotation). 0 for a cantilever's set. Render-side only.</summary>
        public Corners LegSway;

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
                // b's: a split or a merge between the two steps changes which material each node is.
                FiberNodes = b.FiberNodes,
                FiberNodeCount = b.FiberNodeCount,
                FiberTrailCount = b.FiberTrailCount,
                FiberBreakTrail = b.FiberBreakTrail,
                CameraTurn = a.CameraTurn + (b.CameraTurn - a.CameraTurn) * t,
                LegState = b.LegState,
                CornerState = b.CornerState,
                LegSet = b.LegSet,
                SkidFold = b.SkidFold,
                LegSway = b.LegSway,
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


    /// <summary>The fiber's active nodes in a pose, inline (a value type, so a published pose never shares them with the
    /// flight thread): up to Capacity, the table's span.max_nodes and a terrain's crests (FiberLine.CrestNodes).</summary>
    public struct FiberNodes
    {
        public const int Capacity = 48;

        public Double3 N0, N1, N2, N3, N4, N5, N6, N7, N8, N9, N10, N11, N12, N13, N14, N15, N16, N17, N18, N19, N20, N21, N22, N23, N24, N25, N26, N27, N28, N29, N30, N31, N32, N33, N34, N35, N36, N37, N38, N39, N40, N41, N42, N43, N44, N45, N46, N47;

        public Double3 this[int i]
        {
            readonly get
            {
                switch (i)
                {
                    case 0: return N0;
                    case 1: return N1;
                    case 2: return N2;
                    case 3: return N3;
                    case 4: return N4;
                    case 5: return N5;
                    case 6: return N6;
                    case 7: return N7;
                    case 8: return N8;
                    case 9: return N9;
                    case 10: return N10;
                    case 11: return N11;
                    case 12: return N12;
                    case 13: return N13;
                    case 14: return N14;
                    case 15: return N15;
                    case 16: return N16;
                    case 17: return N17;
                    case 18: return N18;
                    case 19: return N19;
                    case 20: return N20;
                    case 21: return N21;
                    case 22: return N22;
                    case 23: return N23;
                    case 24: return N24;
                    case 25: return N25;
                    case 26: return N26;
                    case 27: return N27;
                    case 28: return N28;
                    case 29: return N29;
                    case 30: return N30;
                    case 31: return N31;
                    case 32: return N32;
                    case 33: return N33;
                    case 34: return N34;
                    case 35: return N35;
                    case 36: return N36;
                    case 37: return N37;
                    case 38: return N38;
                    case 39: return N39;
                    case 40: return N40;
                    case 41: return N41;
                    case 42: return N42;
                    case 43: return N43;
                    case 44: return N44;
                    case 45: return N45;
                    case 46: return N46;
                    case 47: return N47;
                    default: throw new System.ArgumentOutOfRangeException(nameof(i));
                }
            }
            set
            {
                switch (i)
                {
                    case 0: N0 = value; break;
                    case 1: N1 = value; break;
                    case 2: N2 = value; break;
                    case 3: N3 = value; break;
                    case 4: N4 = value; break;
                    case 5: N5 = value; break;
                    case 6: N6 = value; break;
                    case 7: N7 = value; break;
                    case 8: N8 = value; break;
                    case 9: N9 = value; break;
                    case 10: N10 = value; break;
                    case 11: N11 = value; break;
                    case 12: N12 = value; break;
                    case 13: N13 = value; break;
                    case 14: N14 = value; break;
                    case 15: N15 = value; break;
                    case 16: N16 = value; break;
                    case 17: N17 = value; break;
                    case 18: N18 = value; break;
                    case 19: N19 = value; break;
                    case 20: N20 = value; break;
                    case 21: N21 = value; break;
                    case 22: N22 = value; break;
                    case 23: N23 = value; break;
                    case 24: N24 = value; break;
                    case 25: N25 = value; break;
                    case 26: N26 = value; break;
                    case 27: N27 = value; break;
                    case 28: N28 = value; break;
                    case 29: N29 = value; break;
                    case 30: N30 = value; break;
                    case 31: N31 = value; break;
                    case 32: N32 = value; break;
                    case 33: N33 = value; break;
                    case 34: N34 = value; break;
                    case 35: N35 = value; break;
                    case 36: N36 = value; break;
                    case 37: N37 = value; break;
                    case 38: N38 = value; break;
                    case 39: N39 = value; break;
                    case 40: N40 = value; break;
                    case 41: N41 = value; break;
                    case 42: N42 = value; break;
                    case 43: N43 = value; break;
                    case 44: N44 = value; break;
                    case 45: N45 = value; break;
                    case 46: N46 = value; break;
                    case 47: N47 = value; break;
                    default: throw new System.ArgumentOutOfRangeException(nameof(i));
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

        /// <summary>HostClock µs of step 0: step k is nominally at StartUs + k × FlightCore.StepUs. The scheduler's time
        /// base, so a drop (FlightScheduler.MaxOwedUs) moves it later.</summary>
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
