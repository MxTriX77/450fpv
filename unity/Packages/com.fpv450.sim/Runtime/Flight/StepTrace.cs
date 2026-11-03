using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// What a step computed on its way to the new state that is not itself integrated state, for the log (design 10,
    /// LOG-01): the quantities a crash is explained by. FlightCore fills it every step; a body without propulsion
    /// leaves the rotor and pack slots zero. The net force and moment are the sums the rigid body integrated.
    /// </summary>
    public struct StepTrace
    {
        /// <summary>The ESC duty the controller's last loop of the step commanded per corner, after the mixer's
        /// saturation and the idle, 0–1 of full (DShot 48–2047); 0 while disarmed.</summary>
        public Corners RotorCommand;

        /// <summary>Each pack's terminal voltage at the step's last sub-step, V (the packs share the ESCs' bus).</summary>
        public double PackVolts0, PackVolts1;

        /// <summary>The current drawn from each pack at the step's last sub-step, A (negative while the ESCs brake).</summary>
        public double PackAmps0, PackAmps1;

        /// <summary>The net force on the body, its weight included, world, N.</summary>
        public Double3 NetForce;

        /// <summary>The net moment about the centre of mass, body axes, N·m.</summary>
        public Double3 NetMoment;

        /// <summary>The air's share of the net force: the body's, the coil's and the rotors' in-plane drag, world, N.</summary>
        public Double3 AeroForce;

        /// <summary>The air's share of the net moment, about the centre of mass, body axes, N·m.</summary>
        public Double3 AeroMoment;

        /// <summary>The contacts' share of the net force (normal and friction, every touching pair), world, N (1.6).</summary>
        public Double3 ContactForce;

        /// <summary>The contacts' share of the net moment, about the centre of mass, body axes, N·m.</summary>
        public Double3 ContactMoment;

        /// <summary>Each leg's spring force, N, and its compression (its deepest point's depth), m. Since p7g-legs 1.2 the force
        /// is along the leg, compression positive (with LegSideStiffness 0, along the contact's normal, as before).</summary>
        public Corners LegLoad, LegCompression;

        /// <summary>The touching (shape, object, shape) pairs whose force the step took, the legs' points among them.</summary>
        public int ContactsTouching;

        /// <summary>The wind field's velocity at the body's origin during the step, world, m/s (p7c-wind 1.4).</summary>
        public Double3 Wind;

        /// <summary>The wind at each rotor's hub during the step, world, m/s (the rotors' inflow and in-plane drag take
        /// it); zero for a body without propulsion.</summary>
        public Corners3 RotorWind;

        /// <summary>The rain's force on the airframe during the step, world, N (p7c-wind 1.7; inside AeroForce too); zero
        /// when Sunny.</summary>
        public Double3 RainForce;

        /// <summary>The rain's water film on the airframe after the step, kg (the mass model's "rain_film"); zero when
        /// Sunny.</summary>
        public double FilmMass;

        /// <summary>The air's velocity at the camera at the step's start, camera (body) axes, m/s: the feed's
        /// AirVelocityAtCamera (FEED-13's lens drops).</summary>
        public Double3 CameraAir;
        /// <summary>Each rotor's distance along its axis to the surface below it and above it at the step's start, m (its rays
        /// and SampleGround, GroundEffectSpec): +∞ for none within the reach, 0 without the ground effect.</summary>
        public Corners GroundDistance, CeilingDistance;
        /// <summary>Each rotor's added inflow through its disc from the other rotors' wakes this step, m/s, positive down
        /// through it (RotorInterferenceSpec): a downwash behind a rotor, an upwash beside and ahead. 0 without the wake.</summary>
        public Corners InterferenceInflow;

        /// <summary>Each leg's state after the step (Contact.LegFlags), a byte a corner, rear right in the low byte (p7g-legs
        /// 1.2); 0 for intact legs, and without the legs' law.</summary>
        public uint LegState;

        /// <summary>Each leg's set after the step, body axes, rad: x and z its bend at the clamp (the way its foot moved,
        /// times the set angle), y its twist in the clamp.</summary>
        public Corners3 LegSet;

        /// <summary>Each leg's stiffness over an intact one's by its state (the cracked factor, 0 broken or off; 1.3 flies
        /// it); 1 without the legs' law.</summary>
        public Corners LegStiffnessFactor;

        /// <summary>The moment at each leg's clamp's edge the legs' law judged this step, N·m; 0 without it.</summary>
        public Corners LegMoment;

        /// <summary>Each corner's state after the step (Contact.CornerState), a byte a corner, rear right in the low byte (p7g-legs
        /// 1.3): what carries its skid half, what is left of its tube.</summary>
        public uint CornerState;

        /// <summary>Each leg's side give this step with the anchor in series, N/m: the tube's by its crack less the axial load's
        /// softening (P·Δ), or a hanging half's cantilever (p7g-legs 1.3).</summary>
        public Corners LegSideGive;

        /// <summary>Each leg's jaws' turn at the foot, body m (the side spring's rest moved by their friction slipping).</summary>
        public Corners3 LegJaw;

        /// <summary>Each hanging skid half's fold about the other leg's bore, body rotation vector, rad.</summary>
        public Corners3 SkidFold;

        /// <summary>The flight's loose pieces (p7g-legs 1.3, design 4), or null: logged in the side record at 50 Hz
        /// (LogFormat.LooseBlock), never in the step record.</summary>
        public Contact.LoosePieces Loose;
    }

    /// <summary>
    /// The kinds of a step's events (design 10, LOG-01). Fixed: a kind is never renumbered, and a new mechanism appends
    /// its kinds (phase 7: hooks, pitfalls, VRS, payout, the tether's break). The producers arrive with their mechanisms:
    /// mixer saturation (FlightCore, 1.4); the ESCs' current limit (1.5); contacts, prop strikes, crashes, tunnels,
    /// impacts and lost contacts (ContactModel, 1.6); the scheduler and the log
    /// write the hitches. FlightEvent says what each field holds per kind; a field a kind does not use keeps its "none"
    /// value.
    /// </summary>
    public enum EventKind : ushort
    {
        None = 0,

        /// <summary>Part touches (Object, Shape) of Material or Surface at Point (Index: the contact shape, which the header
        /// lists). A: the closing speed along the normal, m/s.</summary>
        ContactMade = 1,

        /// <summary>Part leaves (Object, Shape) (Index: the contact shape). A: how long the contact lasted, s; B: its normal
        /// impulse, N·s; C: its peak normal force, N.</summary>
        ContactBroken = 2,

        /// <summary>Index's prop disc is entered by (Object, Shape) at Point. A: the speed of the entry along the normal, m/s.</summary>
        PropStrike = 3,

        /// <summary>
        /// Index's axis (MixerAxis) began to lose authority in the mixer (A: the largest share of its demand lost in the
        /// step, 0–1; for the collective the throttle taken off, 0–1), or regained it (A: 0). Roll, pitch and yaw lose
        /// to the PID-sum limits and to the mix's scaling; the collective to airmode's desaturation.
        /// </summary>
        MixerSaturation = 4,

        /// <summary>A crash of Part on (Object, Shape) at Point, which ends the flight (Index: CrashCause). A: the closing speed
        /// at its first touch, m/s; B: the impulse of its impact so far, N·s; C: that impact's peak acceleration so far,
        /// m/s². The impact's whole figures follow in its Impact event.</summary>
        Crash = 5,

        /// <summary>Part ended the step through (Object, Shape) at Point, its deepest core end: a physics bug, never pilot
        /// error. A: the depth past the latched plane, m; B: the part's speed, m/s.</summary>
        Tunnel = 6,

        /// <summary>A late step (Index: HitchCause). A: the steps run to catch up, or the waits for the log; B: how late, µs.</summary>
        Hitch = 7,

        /// <summary>A step made more events than StepEvents holds: a bug in a producer. A: how many were lost.</summary>
        EventsLost = 8,

        /// <summary>
        /// Index's ESC began to hold its motor at its current limit (A: the largest share of its duty the limit took in
        /// the step, 0–1; B: the limit, A), or let it go (A: 0). The maximum thrust and the spin-up are the limit's.
        /// </summary>
        CurrentLimit = 9,

        /// <summary>
        /// An impact of Part on (Object, Shape) at Point (its first touch or strike faster than the contact spec's
        /// impact speed), made when its window closes (Index: the contact shape, or the disc's corner for a strike). A:
        /// the peak acceleration of the body from all its contacts over the window, m/s²; B: their impulse over it, N·s;
        /// C: the closing speed at the first touch, m/s.
        /// </summary>
        Impact = 10,

        /// <summary>A contact shape met more pairs than its table holds, or a query more than its buffer: a bug (Index: the
        /// contact shape). A: how many were dropped.</summary>
        ContactsLost = 11,

        /// <summary>Index's rotor entered its vortex ring (VortexRingSpec, p7b-airframe 1.2). A: the descent along its axis
        /// over its induced velocity, η; B: its in-plane airspeed over it, μ; C: that induced velocity v_h, m/s.</summary>
        VortexRingEntered = 12,

        /// <summary>Index's rotor left its vortex ring. A, B, C as VortexRingEntered's.</summary>
        VortexRingLeft = 13,

        // 14–22: other lines' (D-074).

        /// <summary>Part's leg began to yield, a set that stays (Index: Contact.LegMode, Bending, Buckling or ClampTwist), loaded
        /// by (Object, Shape) at Point (p7g-legs 1.2). A: the moment at its clamp's edge, or the twist on its jaws, N·m; B: the
        /// capacity it passed, N·m; C: that contact's closing speed at its touch, m/s. The set's angle is in the step record.</summary>
        LegSet = 23,

        /// <summary>Part's leg cracked at its clamp's edge, the edge's first yield (Index: Bending or Buckling). A: the moment
        /// there, N·m; B: the edge's first-yield moment, N·m; C as LegSet's.</summary>
        LegCracked = 24,

        /// <summary>Part's leg parted at its clamp's edge, the strain there past the elongation at break (Index: Bending or
        /// Buckling). A: the moment there, N·m; B: the moment the edge's strain parts it at, N·m; C as LegSet's.</summary>
        LegBroken = 25,

        /// <summary>Part's leg slid out of its clamp (Index: ClampSlip). A: the pull on the jaws, N; B: what they held with the
        /// twist they carried, N; C as LegSet's.</summary>
        LegDetached = 26,

        /// <summary>Part's T-joint let its skid go (Index: TJointSlip, the skid slid in its bore, or TJointFracture, the socket
        /// broke). A: the force along the skid, N, or the socket's moment, N·m; B: its capacity; C as LegSet's.</summary>
        SkidLoose = 27,

        /// <summary>A piece left the drone and flies loose (p7g-legs 1.3, design 4): Part its leg's (or its skid's), Index its
        /// slot in the side record (LogFormat.LooseBlock), Point its centre of mass. A: its mass, kg; B: its centre's speed,
        /// m/s; C: its parts (the LegChange mask). The build is rebuilt without it in the same step.</summary>
        PieceDropped = 28,
    }

    /// <summary>MixerSaturation's Index.</summary>
    public enum MixerAxis : byte { Roll = 0, Pitch = 1, Yaw = 2, Collective = 3 }

    /// <summary>Hitch's Index.</summary>
    public enum HitchCause : byte
    {
        /// <summary>The scheduler found two or more steps due (FlightScheduler, PLT-04).</summary>
        Late = 1,

        /// <summary>The log's ring was full, so the flight thread waited for the flush worker (FlightRecorder).</summary>
        LogFull = 2,

        /// <summary>The app paused the flight (p6-input-app): no step ran for B µs, and every later step's host time is
        /// that much later, so a stick's age adds every pause before it. A: 0.</summary>
        Pause = 3,
    }

    /// <summary>
    /// One event of a step (design 10): fixed fields, whose meaning per kind EventKind gives. The log stamps it with the
    /// step that made it, so a producer sets everything but the step.
    /// </summary>
    public struct FlightEvent
    {
        public const ushort NoPart = 0xFFFF;
        public const byte NoIndex = 0xFF;

        public EventKind Kind;

        /// <summary>The drone part, an index into the mass model's parts (the log header lists them); NoPart when none.</summary>
        public ushort Part;

        /// <summary>A corner, an axis or a cause, by kind; NoIndex when none.</summary>
        public byte Index;

        /// <summary>The world object (WorldIds.Ground for the ground) and its shape, as StaticContacts names them.</summary>
        public int Object, Shape;

        /// <summary>The world's material and surface ids (the log header names them).</summary>
        public ushort Material;
        public byte Surface;

        /// <summary>World, m.</summary>
        public Double3 Point;

        /// <summary>The kind's numbers, in the units EventKind gives.</summary>
        public double A, B, C;

        /// <summary>An event of this kind with every other field at its "none" value.</summary>
        public static FlightEvent Of(EventKind kind) => new FlightEvent { Kind = kind, Part = NoPart, Index = NoIndex };
    }

    /// <summary>
    /// The events one step made, in the order made: preallocated, cleared at each step's start. A step that makes more
    /// than the capacity keeps the first ones and counts the rest in Lost, which the log records as an event.
    /// </summary>
    public sealed class StepEvents
    {
        readonly FlightEvent[] _items;

        public StepEvents(int capacity = 64)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new FlightEvent[capacity];
        }

        public int Count { get; private set; }

        /// <summary>Events refused this step because the buffer was full.</summary>
        public int Lost { get; private set; }

        public ref readonly FlightEvent this[int i]
        {
            get
            {
                if ((uint)i >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(i));
                return ref _items[i];
            }
        }

        public void Add(in FlightEvent e)
        {
            if (Count == _items.Length)
            {
                Lost++;
                return;
            }
            _items[Count++] = e;
        }

        public void Clear()
        {
            Count = 0;
            Lost = 0;
        }
    }
}
