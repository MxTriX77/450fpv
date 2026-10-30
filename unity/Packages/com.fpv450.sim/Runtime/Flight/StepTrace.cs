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

        /// <summary>Each leg's spring force, N, and its compression (its deepest point's depth), m.</summary>
        public Corners LegLoad, LegCompression;

        /// <summary>The touching (shape, object, shape) pairs whose force the step took, the legs' points among them.</summary>
        public int ContactsTouching;
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
