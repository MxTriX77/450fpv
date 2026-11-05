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
        /// <summary>The fiber's step (p7d-tether 1.6): its payout, its tensions, its pull on the drone, its nodes and trail;
        /// all 0 for a flight without the line.</summary>
        public Tether.TetherTrace Tether;

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
        /// softening (P·Δ), or a hanging half's cantilever (p7g-legs 1.3); on the lever (1.4) its points' secants, negative past
        /// the sway load.</summary>
        public Corners LegSideGive;

        /// <summary>Each leg's lean on its lever this step (p7g-legs 1.4): its foot's offset across the tube from its rest, its
        /// points' shares of it, m; 0 off the lever or off the ground.</summary>
        public Corners LegLean;

        /// <summary>Each leg's jaws' turn at the foot, body m (the side spring's rest moved by their friction slipping).</summary>
        public Corners3 LegJaw;

        /// <summary>Each hanging skid half's fold about the other leg's bore, body rotation vector, rad.</summary>
        public Corners3 SkidFold;

        /// <summary>Each resting prop's blades' load this step (p7f-damage 1.3b): the contact force on blade 0 and blade 1, N, and
        /// each blade's own deflection at its strongest contact, m (its bending's share of the series, along the contact's
        /// normal); 0 for a turning prop's.</summary>
        public Corners BladeLoad0, BladeLoad1, BladeBend0, BladeBend1;

        /// <summary>Each motor's bell this step (p7f-damage 1.5, Contact.MotorBell): its scrape's moment against the turning rotor (or,
        /// at rest, its static friction's hold), N·m; the magnets' rub on the stator's teeth, N·m; and its Kt over the drawn one less 1 (exact: 0 for a gap as new). All
        /// 0 without the bells.</summary>
        public Corners BellScrape, BellRub, BellKtChange;

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

        /// <summary>The fiber's coil passed a rebuild mark (p7d-tether 1.6: each span.coil_rebuild_m of line paid out this
        /// flight) and the mass model was rebuilt. Point: the exit. A: the line paid out at the mark, m; B: the coil's mass
        /// now, kg; C: the pack's resistance T_r (payout_tension_n), N.</summary>
        Payout = 14,

        /// <summary>The fiber's chain gained energy its guards could not take back, or went non-finite (CableChain, design
        /// 13): a physics bug, never pilot error; the step undone and the line stopped. Index: the first non-finite node, or
        /// none for an energy breach; Point: that node, or the exit. A: the rise over the budget, J; B: the linearisation's
        /// deficit, J; C: what the line's relative motion held, J.</summary>
        TetherWedge = 15,

        /// <summary>A preallocated table of the world's or the line's was full: a bug in a producer or a world beyond its
        /// budget (Index: WorldBugCause). A: the table's size.</summary>
        WorldBug = 16,

        /// <summary>
        /// A blade's pass across a shape its disc cuts (p7f-damage design 4–6): Part's prop, Index the blade (low 4 bits) and the
        /// pass's StrikeMode (high 4 bits), on (Object, Shape) of Material or Surface, Point where the blade meets it. A: the
        /// pass's work, the energy it took from the rotor, J; B: the meeting radius r_m, m; C: the blade's speed into the face
        /// at its entry, u_n, m/s.
        /// </summary>
        BladeStrike = 17,

        /// <summary>
        /// A pass changed a blade (p7f-damage design 1, 5): Part's prop, Index the blade (low 4 bits) and the StrikeMode (high 4
        /// bits: crush, break, bend, wear), on (Object, Shape) of Material at Point. A: the blade's tip radius after it, r/R; B:
        /// the mass it lost, kg; C: the inner edge of the changed stations, r/R (the new tip for a crush or a break, the bent
        /// station for a set, the meeting radius for wear).
        /// </summary>
        BladeDamaged = 18,

        /// <summary>
        /// Index's rotor stopped on a shape its disc cuts (p7f-damage design 6): a pass needed more than the rotor's energy, so
        /// it ended on the face, its damage cut short. Part's prop, on (Object, Shape) of Material at Point. A: the pass's work,
        /// J; B: the rotor's energy before it, ½Jω², J; C: the share of the pass's work done, 0–1.
        /// </summary>
        RotorJammed = 19,

        /// <summary>
        /// A soil carve's pass met a stone (p7f-damage 1.3's tip slide: surfaces.json's stone fraction, the "soil.stones" draw):
        /// Part's prop, Index the blade (low 4 bits) and what the stone did (high 4 bits, StrikeMode: Break, Crush for a notch or
        /// a sever, Glance for an elastic push), on (Object, Shape) of Surface at Point, the stone's place on the leading edge.
        /// A: the energy it took from the rotor, J; B: the stone's diameter, m; C: the blade's speed into it, m/s.
        /// </summary>
        StoneStrike = 20,

        /// <summary>
        /// A motor's bell dented (p7f-damage 1.5, design 8): its ring collapsed under a contact past its collapse load. Part the
        /// motor, Index its corner (low 4 bits) and the band (high 4 bits: 0 the stator's, where the dent narrows the gap; 1 above
        /// or below it, toward the windings' end turns), by (Object, Shape) of Material or Surface at Point. A: the band's dent
        /// after it, m; B: the ring's load, N; C: the collapse load it passed, N.
        /// </summary>
        MotorDamaged = 21,

        /// <summary>
        /// Index's motor was held at rest by its drag under drive, the ESC's drive into the stalled winding at its duty within the
        /// motor's added drag (a bind, design 8, D-073), or let go (A 0). A: the added drag, N·m; B: the drive's torque Kt·i, N·m;
        /// C: what BLHeli_S's low-rpm protection would let the ESC drive there, its floor Pwm_Limit_Beg (25 of 255 at the default
        /// startup power) as Kt·d·V/R, N·m (derived, not flown).
        /// </summary>
        MotorBound = 22,

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

        /// <summary>
        /// The fiber took a member as a contact (p7d-tether 1.7, design 6): Index its FiberMember, on (Object, Shape) for a
        /// shape, a conductor (its wire shape) or a snag record (Shape the record's index), on Object (the instance) and Shape
        /// (the element's index) for an element, or Part and Shape (the drone's contact shape) for the drone's own; Material the
        /// member's, Point where it touched, after the step it was taken. A: the line's turn there, rad; B: the larger tension
        /// beside it, N; C: the bend's radius R_eff there, m (∞ for a member the line does not press).
        /// </summary>
        FiberContact = 30,

        /// <summary>
        /// Line slid through a capstan member it had held at, or a member let it go (p7d-tether 1.7): Index its FiberMember, the
        /// member named as FiberContact's, Point the node. A capstan's slide: A the line's turn, rad; B and C the tensions on its
        /// high and low sides, N. A hooked straw or a standing stem letting go: A 0; B the force it held, N; C what it holds to
        /// (its release force or Euler's load, N), or its slide-off press, m, negated.
        /// </summary>
        FiberSlip = 31,

        /// <summary>
        /// The line broke (p7d-tether 1.8, design 7): Index its TetherMechanism (low 4 bits) and the place's FiberMember (high 4
        /// bits), the place named as FiberContact names a member (a shape, an element, a record, a conductor, the drone's part), or
        /// the drone (Object −2) and the coil's Part for the exit lip and the peel front, the rotor's disc shape and part for the
        /// prop, Object −1 for the free span; Point the break. A: the line's turn there, rad (the prop: the crossing's radius, m);
        /// B: the tension, N; C: the bend's radius R_eff, m (∞ for tension; the prop: the blade's speed there, m/s). The strength it
        /// met follows it in the same step as FiberSection with Index 0x80 (none for the prop and the wedge). The link is Broken
        /// and the margin −∞ from this step.
        /// </summary>
        TetherBreak = 32,

        /// <summary>
        /// The link's margin crossed the receiver's threshold (p7d-tether 1.8, design 8). At a dip's start: Index the FiberMember
        /// of the bend with the largest loss, named as TetherBreak's place, A the margin, dB, B that loss, dB, C 0. At its end
        /// (Index | 0x80): the member with the largest loss at the dip's least, A the dip's length, s, B its least margin, dB, C
        /// that loss, dB.
        /// </summary>
        LinkDip = 33,

        /// <summary>
        /// A section's strength read (p7d-tether 1.8, design 7, 14): Index its SectionBand (low 2 bits: intact, flaw, abraded,
        /// span), 0x40 when it wore through its coating now (its abraded draw), 0x80 when it is the strength a break met; the
        /// member it was read at named as TetherBreak's place. A: the section's start s, m (the span's: the break's section);
        /// B: its strength, Pa; C: the stress it met, Pa (a wear-through: the wear's depth, m). Logged at a section's first read
        /// at a contact; at the lip and the peel when its stress first passes half its strength, and at a break.
        /// </summary>
        FiberSection = 34,

        /// <summary>
        /// The flight controller's failsafe moved on a stage (Betaflight 4.5's, p7d-tether 1.8, design 8): Index its
        /// Control.FailsafeStage (0 the receiver declared the signal lost, 1 stage 1's channel fallbacks, 2 stage 2's
        /// failsafe_procedure, 3 disarmed). A: the time since the last good frame, s; B: failsafe_procedure (0 DROP, 1 AUTO_LAND);
        /// C: the throttle stick the stage holds, 0–1.
        /// </summary>
        Failsafe = 35,
    }

    /// <summary>A blade pass's mode (BladeStrike's and BladeDamaged's Index, high 4 bits; p7f-damage design 5).</summary>
    public enum StrikeMode : byte
    {
        /// <summary>The face takes no blade damage here and the pass no work: the ground and the soil until p7a-ground's
        /// surfaces, a thin compliant row (straw, foliage, twig shells, fabric, mesh: p7a's and p7d's), a row without what a
        /// blade meets.</summary>
        Glance = 0,

        /// <summary>A harder face crushed the blade back to the meeting radius.</summary>
        Crush = 1,

        /// <summary>A section broke where its bending first passed its strength: everything beyond is lost.</summary>
        Break = 2,

        /// <summary>A tilted harder face bent the blade over it: an elastic slap, a flap set past the proportional limit.</summary>
        Bend = 3,

        /// <summary>The blade slid over a tilted harder face and wore its leading edge (Archard).</summary>
        Wear = 4,

        /// <summary>The blade carved a softer face: its crushing work and the flung mass.</summary>
        Carve = 5,
    }

    /// <summary>WorldBug's Index.</summary>
    public enum WorldBugCause : byte
    {
        /// <summary>The fiber's laid trail was full (trail.capacity): its oldest pair was merged.</summary>
        TrailFull = 1,

        /// <summary>A fiber cache's elements (contact.element_capacity), snag segments (contact.snag_capacity) or conductors
        /// (contact.wire_capacity) were more than it holds: the canonical prefix is used (p7d-tether 1.7). A: the true count.</summary>
        FiberElementsFull = 2, FiberSnagsFull = 3, FiberWiresFull = 4,

        /// <summary>A fiber cache's grid held fewer cell entries than its members meet (numerical): those past it are not met.</summary>
        FiberGridFull = 5,

        /// <summary>A segment's query met more shapes than the fiber's buffer holds: it was asked again at the insertion gap.</summary>
        FiberShapesFull = 6,

        /// <summary>The fiber's section table (FiberCuts.Capacity) was full: sections past it are not logged or worn (p7d-tether 1.8).</summary>
        FiberSectionsFull = 7,
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
