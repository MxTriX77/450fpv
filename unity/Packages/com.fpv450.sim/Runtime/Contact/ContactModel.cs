using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>The cause of a crash, as its event's Index.</summary>
    public enum CrashCause : byte
    {
        /// <summary>The contacts' acceleration of the body passed ContactSpec.CrashAccel.</summary>
        Acceleration = 1,

        /// <summary>Retired at model 35 (p7f-damage 1.3): a prop disc entering a rigid shape or the ground faster than 3 m/s ended
        /// the flight. A strike now does its own mechanics (PropStrikes) and never ends the flight; old logs keep the value.</summary>
        PropStrike = 2,
    }

    /// <summary>What carries a corner's half of its skid and what is left of its leg's tube (p7g-legs 1.3, design 8): the step
    /// record's corner_state, a byte a corner.</summary>
    [Flags]
    public enum CornerState : byte
    {
        /// <summary>Its leg holds its half of the skid (the build's).</summary>
        None = 0,

        /// <summary>Its half hangs from the other T-joint on its skid (its own leg or T-joint let go): the skid's cantilever
        /// from that bore carries it.</summary>
        Hanging = 1,

        /// <summary>Its half of the skid left the drone (the skid dropped).</summary>
        SkidGone = 2,

        /// <summary>Its leg broke at the clamp's edge: the stub in the clamp is its tube.</summary>
        Stub = 4,

        /// <summary>Its leg's tube left the drone (it slid out of the clamp).</summary>
        TubeGone = 8,

        /// <summary>Its hanging half folded at the bore (past the skid's first yield there).</summary>
        Folded = 16,
    }

    /// <summary>
    /// What one change of the legs' structure takes off the drone (ContactModel.Restructure, p7g-legs 1.3), filled in place:
    /// the parts that leave or change, and the pieces they leave as (each a rigid assembly of its parts, with its shapes).
    /// </summary>
    public sealed class LegChange
    {
        /// <summary>The most pieces one step can leave as: each leg's (4) and each skid alone (2), as many as LoosePieces' slots
        /// (p7g-legs 1.4: 1.3's 3 overflowed when a level hard landing parted all four legs in one step).</summary>
        public const int MaxPieces = 6;

        /// <summary>The broken legs whose tubes are now their stubs in the clamps, bit 1 &lt;&lt; corner.</summary>
        public int Stubs;

        /// <summary>The pieces: each one's parts (a mask: bit c a leg's whole tube, 4 + c its part below a break, 8 + c a T-joint,
        /// 12 the right skid, 13 the left), its contact shapes (indices into the drone's) and their cores as they left (body).</summary>
        public int Pieces;
        public readonly int[] Parts = new int[MaxPieces];
        public readonly int[][] Shapes = { new int[16], new int[16], new int[16], new int[16], new int[16], new int[16] };
        public readonly ContactShape[][] Geometry =
            { new ContactShape[16], new ContactShape[16], new ContactShape[16], new ContactShape[16], new ContactShape[16], new ContactShape[16] };
        public readonly int[] ShapeCount = new int[MaxPieces];

        public void Clear()
        {
            Stubs = 0;
            Pieces = 0;
            for (int i = 0; i < MaxPieces; i++)
            {
                Parts[i] = 0;
                ShapeCount[i] = 0;
            }
        }
    }

    /// <summary>
    /// The drone's contacts with the world (p6-flight-core design 7, PHB-05, K§7.1 and K§7.5), engine-free and
    /// allocation-free in a step. Two calls a step, around the rigid body's move:
    /// - Forces, at the step's start: every touching pair's normal force and friction, from the planes and ground samples
    ///   Sense kept at the same pose, and the body's velocity there; the crash by acceleration; the impact window.
    /// - Sense, after the move: each shape swept from the last pose to the new one through StaticContacts (and the
    ///   supports' ground from SampleGround), the latches updated, and the contact, strike and tunnel events.
    ///
    /// The latch (K§7.5 with review-physics P1): per (shape, object, shape) pair, a first touch (Time &lt; 1) latches its
    /// point and normal, and the depth is measured against that plane: δ = r − n·(c − P), c the core's point nearest P. A
    /// later Time = 1 contact with a same-side normal refreshes the plane, but only while the part's core is outside the
    /// shape (Distance &gt; −r): past that the closest point's normal is no longer the contact's. A flipped normal or another
    /// Time &lt; 1 keeps it. A pair goes when its plane distance passes the margin or it leaves the result; leaving while
    /// its plane still shows penetration, after a flip or with the part's deepest core end past the shape's centre along
    /// the normal it came in by, is a Tunnel event: a physics bug, never pilot error.
    ///
    /// A buried face is no contact (Buried, physics-integration-2's side-face latch): a layer's contact whose point lies
    /// inside another shape of the same object and the same material is a face between two pieces of one layer (S04's
    /// overlapping straw tiles: a tile's side within its neighbour), not the layer's surface. The mass runs on across it,
    /// so it carries no normal stress of its own; the piece around it meets the part by its own face.
    ///
    /// The laws:
    /// - A leg (K§7.1 step 8): one spring k and damper c per corner in series with what its foot touches. Its points are
    ///   its support spheres on SampleGround's ground and on objects (a sphere's contacts on coplanar pieces of one face are
    ///   one, Seam), and its skid capsule's object contacts that are not its supports' (P3, SameSupport). The leg carries
    ///   F = k·δ_max − c·v̄ (at least 0), δ_max the deepest point's depth, split over the points by depth, w_i = δ_i/Σδ; v̄
    ///   is the depth-weighted closing speed.
    /// - A body shape: each touching pair is its own spring and damper, the shape's stiffness along the normal in series
    ///   with the material's (a rigid material drops out: the shape's alone, never 1/(1/k + 1/∞), P7), c = 2ζ·√(k·m·share).
    /// - Friction (K§7.1 step 5): a stick anchor per pair, tangential stiffness ratio × the normal's, holding to μs·Fn and
    ///   sliding at μk·Fn; μ from physics_materials.json's row, or the ground's pair. The spring stretches with the touching
    ///   material's slip, not with the contact point's move, so a rounded part rolls (1.6c: the coil rocks about its contact).
    /// - A prop disc: its bounding sphere each step; when that meets anything, a chord capsule over each ChordSegments
    ///   of the rim's segments, sized to hold them (1.7: the stand's posts sit inside the sphere, far from the rim); and
    ///   when a chord meets anything, its segments of the ring of capsules round the rim (P4). Both gates only skip
    ///   capsules that touch nothing, so the ring finds what it found without them. A shape the bounding sphere meets
    ///   that cuts the disc's slab inside the rim is the disc's too (BladeCut), and a pair lives while its shape cuts the
    ///   slab within the rim, so a rod that passes the rim into the disc is still met (p7f-damage design 4). A shape's
    ///   entry is a PropStrike; the blades' passes across it are PropStrikes' (model 35: never a crash).
    /// - A motor's bell (p7f-damage 1.5, MotorBell, after AttachBells): a motor sphere's pair on its turning band is a body pair
    ///   whose material moves with the rotor, so while the rotor turns it slides at μk·Fn against the bell's slip (no anchor), and
    ///   its moment, the magnets' rub on the stator and the ring's dent go to the bell (MotorDamaged).
    /// No restitution coefficient anywhere: a bounce is the stored energy of the compliance and the thrust.
    ///
    /// The soil (p7a-ground 1.10, design 2 and 3), with surfaces.json attached (ContactSpec.Surfaces) at the flight's
    /// moisture: each corner's half-skid, from the skid's middle through its T-joint to its outer end, meets the ground's
    /// soil, the plane where SampleGround has it and the deepest soil shape (Surface != 0) the corner's parts touch where it
    /// reports a hole, by Winkler's elasto-plastic law (Soil) at three nodes: the middle (its own sample), the joint and the
    /// end (the supports' samples), integrated by the three-point rule exact for quadratics on those arcs. The deepest node
    /// sets the surface. The leg is in series with it at equal force: k·x = F_soil(δ_i − x) (+ the corner's other points),
    /// by four safeguarded Newton steps from the last step's x, then the soil's damping c_s·S in series with the leg's on
    /// the closing speed at the load centroid, where the force acts. Its stick anchor holds to μs·Fn + c_a·S_m + P(θ) and
    /// slides at μk·Fn + c_a·S_m + P(θ), P the groove's passive resistance toward the stretch. A ring of the last grooves
    /// lets a half-skid landing on the same soil within half the groove's width across and 10° start from their sinks. A
    /// rigid surface (asphalt, a slab) keeps the leg alone on the supports, as without the table.
    ///
    /// Wet ground (p7a-ground 1.13, design 7), with the table in Rain or Severe rain: a groove that has loaded wet soil
    /// holds its half-skid as it lifts by its cohesive pull (Soil.Cohesion over each node's wetted width), a tension in the
    /// leg's series solve; a material row's wet block stands in for the row (wet straw: softer, gripping harder), and a
    /// corner lifting off a wet layer it pressed clings by the same law (Cling); the cover kinds read their wet sets (Cover).
    ///
    /// Compliant layers (p7a-ground 1.12, design 6): a layer row (one with an area law: straw, fabric, a sandbag's fill) meets
    /// each touching part by its footprint (Layered, Layer), the leg by its half-skid; it compresses at most Layer.StrokeShare
    /// of its thickness, past which it is solid, and either carries the half-skid onto its own object's soil beneath (Carry)
    /// or stops the leg, or a body shape's own spring, at its stroke.
    ///
    /// A damaged leg changes the drone (p7g-legs 1.3, design 8): a set turns the leg's tube and its skid half about its clamp's
    /// edge (MoveShapes; a held foot's anchor moves with its point, so the ground keeps it where it is); a crack's factor
    /// scales the tube's stiffnesses; the side give is the tube's 3EI/L³ less the axial load's geometric stiffness (P·Δ, never
    /// below 0) with the jaws held; a broken leg keeps its stub; a parted or detached piece leaves the contact set
    /// (Restructure); and a skid half whose leg let go hangs from the other T-joint on its skid, its cantilever's give a turn
    /// of its shapes about that bore (Hang), folding past the skid's first yield there.
    /// </summary>
    public sealed class ContactModel
    {
        /// <summary>A corner's half-skid in soil (ContactModel.SoilOf): its leg's deflection, each node's sink below the
        /// soil's surface (middle, T-joint, outer end) and deepest sink, the soil's static force, the force with its damping,
        /// its stiffness, the load centroid's arc from the middle, the footprint now and at the deepest sinks, the anchor's
        /// force, and the series solve's residual k·x − F; in wet soil (1.13) the cohesive pull (N, toward the soil) and the
        /// wetted footprint still holding (m²).</summary>
        public struct SkidSoil
        {
            public bool Engaged, Sliding;
            public byte Surface;
            public int Object, Shape;
            public double Deflection, Sink0, Sink1, Sink2, Max0, Max1, Max2, Static, Force, Stiffness, Centroid, Footprint, Wetted, Residual;
            public Double3 Friction;
            public double Pull, Clinging;
        }

        /// <summary>The (object, shape) pairs one shape holds at once; more is a ContactsLost event. Measured (w3-farmland-ground
        /// 2.7): a part lodged in a ruin's window frame holds up to 14 of its boxes at once, and a prop's disc 7–10 crown pieces
        /// where a crown's shell is densest (w4a-farmland-vegetation); 24 holds both with room (Buffer, the query's, is 32).</summary>
        public const int PairsPerShape = 24;

        /// <summary>The rim's segments under one chord of the disc's second gate (1.7): 3 of 12 make four chords.</summary>
        public const int ChordSegments = 3;

        // The chord capsules' pad over the farthest point they hold, m: far above the rounding of the body-to-world
        // transform and the query's tests, far below anything physical; a larger gate only costs time.
        const double ChordPad = 1e-6;

        // How far a shallower contact's point may lie off a deeper one's plane and still be that face's seam, m (assumed: a
        // micron, far above the rounding of placed boxes' faces, far below a step a 5 mm foot can feel).
        const double SeamTolerance = 1e-6;

        // Two depths within this are one face's, m (assumed: a nanometre; one plane through two boxes' transforms differs by
        // about 1e-15 m), so the earlier result keeps the contact and rounding never trades it between the pieces.
        const double SeamTie = 1e-9;

        // Two contacts' normals with a dot product at or below this face away from each other (assumed: more than 120° apart;
        // a layer's underside and its soil's top, or two abutting pieces' sides, are 180° apart, a face's pieces and its edges
        // within 90°), so neither is the other's seam (1.12).
        const double FacingAway = -0.5;

        const int Buffer = 32;

        struct Pair
        {
            public int Object, Shape;
            public ushort Material;
            public byte Surface;
            public Double3 P, N, Entry; // the plane, and the normal the part came in by
            public bool Latched, Flipped, Seen, Touching, Suppressed;
            public double Depth;        // at the pose Sense last saw, m
            public double Slack;        // a blade's overlap its spring does not read: what it came back to rest in, as it shrinks since, m
            public Double3 Point;       // the contact point on the plane under the core there, world
            public Double3 Stretch;     // the friction's stick spring, world: the touching material's slip since it stuck
            public bool Anchored, Sliding;
            public long Since;
            public double Impulse, PeakForce, MadeSpeed, Time;
            public Double3 Force;       // a leg point's force this step, world (the side give's law, before its cone)
            public double Share;        // its share of its leg, by depth
            public Double3 Drift;       // a leg point's push across the tube this step, world (its spring's rest moves taken off)
            public double Overlap;      // a leg point's depth its spring reads: its depth, or a hanging half's without its elastic turn
        }

        struct Row
        {
            public bool Known, Rigid;
            public double StaticFriction, KineticFriction, Stiffness, Damping;
            public double Adhesion, AdhesionRelease; // a wet layer's cling σ_a (Pa) and its release δ_a (m), 1.13; 0 when dry
            public AreaLaw Law;                      // a layer's area law and its reference depth (m), 1.12; None for any other row
            public double LawDepth;
            public CrownRow Crown;                   // a crown's twig shell (w9-village-physics 2.3): its twigs' law; null for any other row
        }

        /// <summary>The footprint ring's grooves (design 3, assumed: two a corner).</summary>
        public const int PrintRing = 8;

        // A print's axis within this of a landing half-skid's makes it the same groove: cos 10° (design 3, assumed).
        const double PrintAngleCos = 0.984807753012208;

        // A corner's half-skid on soil: its geometry, the middle node's ground, and its groove.
        struct Skid
        {
            public bool Known;
            public int End, Joint;               // the support shapes at its outer end and at its T-joint
            public int Capsule;                  // its skid capsule (A the outer end, B the middle), which a leg's set moves
            public Double3 Middle;               // body m: the skid's middle, the half-skid's inner end
            public double Radius, Length, Arc;   // m: the tube's radius, the middle to the end, the middle to the joint
            public double W0, W1, W2;            // the nodes' weights, m (exact for a quadratic over 0, Arc, Length)
            public Pair Ground;                  // the middle node's ground (Sense), as a support's
            public bool Engaged;                 // some node lies below the soil's surface
            public int Object, Shape;            // the soil's identity: the ground (its surface) or a soil shape
            public byte Surface;
            public double Max0, Max1, Max2;      // each node's deepest sink, m
            public double Wet0, Wet1, Wet2;      // each node's wetted width since it last let go, m (1.13; 0 in dry soil)
            public double X;                     // the leg's deflection, m (below 0 stretched by the cohesive pull)
            public Double3 Anchor, AnchorBody;   // the stick anchor (world) and the material point stuck there (body)
            public bool Anchored, Sliding;
            public SkidSoil Last;                // what the last Forces found, for the log and the tests
        }

        struct Print
        {
            public bool Used;
            public int Object, Shape;
            public byte Surface;
            public Double3 P0, P2;               // the nodes' core points, middle and outer end, world m
            public double Arc;                   // the joint node's share of the length
            public double Max0, Max1, Max2;
        }

        readonly ContactSpec _spec;
        readonly FlightWorld _world;
        readonly ContactShape[] _shapes;
        readonly int[] _order;          // spheres, then capsules, then discs: a capsule's dedupe reads its part's spheres
        readonly int[] _supports;       // the support shapes, in shape order
        readonly Pair[] _pairs;         // shape i's pairs at i·PairsPerShape
        readonly int[] _count;
        readonly Pair[] _ground;        // a support's ground (index: shape)
        readonly bool _terrain;         // the ground is a terrain, not one plane (Geometry(Ground, 0) false: w3-farmland-ground 2.2)
        readonly Row[] _byMaterial;     // world material id → its row
        readonly Row _groundRow;
        double _massShare;              // m·share, kg, for the damping ratios (a loose piece's own, Reuse)
        readonly double _dt;            // the step it is called at: the flight's 1 ms, a loose piece's sub-step
        readonly StaticContact[] _buf = new StaticContact[Buffer];
        readonly XZ[] _at;
        readonly GroundSample[] _samples;
        readonly Double3[] _ring;       // the rim's unit directions in the disc's plane (body xz)
        readonly double[] _chordReach;  // each chord's farthest rim vertex from it, on the unit ring
        readonly StaticContact[] _gate = new StaticContact[Buffer]; // a disc's bounding sphere's contacts, while the ring queries

        // The soil (surfaces.json at the flight's moisture), or null without the table.
        readonly Skid[] _skids;         // by corner
        readonly SoilSet[] _soil;       // world surface id → its set
        readonly Row[] _surfaceRow;     // world surface id → its friction as a rigid ground
        readonly Print[] _prints;
        int _printNext;

        // Wet ground (1.13): each corner's footprint clinging to a wet layer (a material row's cling, straw's) since it last
        // let go, m² (null when nothing clings); the cover kinds' sets at the flight's moisture, for the elements (1.14, 1.15).
        readonly double[] _cling;
        readonly CoverSet[] _cover;

        // Compliant layers (1.12): each corner's half-skid (its skid capsule, the part a corner meets a layer with; −1 without
        // one), and the share of a layer's thickness it compresses before it is solid.
        readonly int[] _halfSkid = { -1, -1, -1, -1 };
        readonly double _stroke;

        // The legs' side give and their failure law (p7g-legs 1.2): each corner's springs this step, whether its points
        // touch, and its tube shapes' loads; the law, or null.
        readonly LegDamage _legs;
        readonly int[] _legOf;          // a leg tube shape's corner (a body shape on a leg's part), else −1
        readonly double[] _axial = new double[4], _side = new double[4];
        readonly bool[] _legTouching = new bool[4];
        readonly Double3[] _tubeForce = new Double3[4], _tubeMoment = new Double3[4];
        readonly int[] _tubeShape = new int[4], _tubePair = new int[4];
        readonly double[] _tubeStrongest = new double[4];

        // A damaged leg changes the drone (p7g-legs 1.3, design 8). The shapes as the legs' sets, breaks and folds have moved
        // them (_shapes; the build's are _base), where the last Sense left each one (its sweep's start), and those that left
        // the contact set. Each corner's state, the set and fold its shapes last moved by, each leg's axial load the step
        // before (its side give's softening), and a hanging half's force and couple onto the leg that holds it.
        readonly ContactShape[] _base;
        readonly Double3[] _wasA, _wasB;
        readonly bool[] _off;
        readonly CornerState[] _corner = new CornerState[4];
        readonly Double3[] _fold = new Double3[4], _setApplied = new Double3[4], _foldApplied = new Double3[4];
        readonly double[] _lastAxial = new double[4], _tubeSide = new double[4];
        readonly double[] _lean = new double[4];  // each leg's lean on its lever this step, its points' shares of δ (p7g-legs 1.4), m

        // The skid's frame (p7g-legs, model 54): whether each corner's leg sways as its skid's frame along the skid this step
        // (LegGive), the frame's way across the leg (world: the skid's axis square to the leg's), the tube's give and its damper
        // that way, and the leg spring's give that way with the anchor in series and the tube's own (as _side and _tubeSide are
        // the other way, across the skid).
        readonly bool[] _frame = new bool[4];
        readonly Double3[] _frameAxis = new Double3[4];
        readonly double[] _frameGive = new double[4], _frameDamp = new double[4], _sideF = new double[4], _tubeSideF = new double[4];
        readonly Double3[] _hangForce = new Double3[4], _hangCouple = new Double3[4];
        readonly bool[] _hangTouching = new bool[4];
        readonly int[] _hangShape = { -1, -1, -1, -1 }, _hangPair = new int[4]; // a hanging half's strongest contact
        readonly int[] _legTube = { -1, -1, -1, -1 };  // each leg's tube shape
        readonly LegFlags[] _followed = new LegFlags[4]; // the legs' flags the structure last followed
        readonly LegChange _change = new LegChange();
        bool _restructure;

        // The hanging skid's cantilever: its tube's EI, and the lever from a bore to the other leg's (derived, 1.1's section).
        readonly double _skidBending, _skidLever, _skidContact;
        readonly Double3[] _elastic = new Double3[4];   // a hanging half's elastic turn about its holder's bore (body rotation vector)
        // A hanging half's elastic move along its deepest point's normal past what its turn gives (body m, ContactSpec.HangingGive):
        // the rest of its chain's give, the holding leg's across its tube and the stick anchor's in series with them.
        readonly Double3[] _hangShift = new Double3[4], _shiftApplied = new Double3[4];

        // Props are parts at rest (p7f-damage 1.3b): the rotors and their blades (null until AttachRotors), each blade shape's
        // slot (corner × blades + blade; −1 for any other shape), segment and edge; the hubs (body) and spin signs; the
        // stations' bending sums for a contact's compliance; which rotors rest now and which a carve holds (PropStrikes);
        // each resting rotor's angle at its blades' placement and its turn rate since the last; the rotors whose turning blades'
        // passes the strikes judged since they last rested, and the blade shapes those placed back this step; and the step's loads.
        Propulsion _rotors;
        PropBlade _blade;
        double _bladeR;
        int _bladesPer;
        int[] _bladeSlot, _bladeSeg, _bladeEdge;
        bool[] _appeared;
        int _passed;
        readonly Double3[] _hub = new Double3[4];
        readonly double[] _spinSign = new double[4], _restAngle = new double[4], _restRate = new double[4];
        double[] _flat0, _flat1, _flat2, _edge0, _edge1, _edge2, _axial0;
        int _resting, _carveHeld;
        RestLoad[] _restLoads;
        double[] _restWeight, _restStrongest;
        readonly double[] _shaftTorque = new double[4], _shaftStiffness = new double[4], _shaftDamping = new double[4];

        // The motors' bells (p7f-damage 1.5): null until AttachBells; each shape's corner when it is a motor's sphere, else −1.
        MotorBell _bells;
        MotorShaft _shafts; // the motors' shafts and bearings (p7f-damage 1.6), with the bells and props.blade; null without
        MotorHealth _health; // the motors' heat and dirt (p7f-damage 1.6b), with the bells; null without
        int[] _bellOf;

        /// <summary>A resting blade's load in the last Forces (p7f-damage 1.3b): the contact force on it (world, N) and its
        /// components across its section at the strongest pair's station (flatwise, edgewise, N), the force-weighted radius of
        /// its pairs (m), the blade's own deflection along the strongest pair's normal (m), and that pair's world identity and
        /// point.</summary>
        public struct RestLoad
        {
            public bool Loaded;
            public Double3 Force, Point;
            public double Flat, Edge, Radius, Deflection, Normal;
            public int Object, Shape;
            public ushort Material;
            public byte Surface;
        }

        // The impact window.
        int _impactLeft, _impacts;
        double _impactPeak, _lastPeak, _lastImpulse;
        Double3 _impactImpulse;
        FlightEvent _impact;

        /// <summary>The model for a drone of the given mass in world; moisture picks surfaces.json's sets (D-021: Sunny is
        /// Dry, Rain Wet, Severe rain Saturated), unread without the table. stroke is the layers' stroke share, Layer.StrokeShare
        /// in every flight; a scenario's known-wrong model without the stroke limit passes +∞. random draws the legs' strengths
        /// when the spec has their law (a flight's FlightRandom, so its header lists them); dt is the step it is called at, the
        /// flight's 1 ms or a loose piece's sub-step.</summary>
        public ContactModel(ContactSpec spec, FlightWorld world, double mass, Moisture moisture = Moisture.Dry, double stroke = Layer.StrokeShare,
            FlightRandom random = null, double dt = FlightCore.Dt)
        {
            _stroke = stroke;
            _dt = dt;
            _spec = (spec ?? throw new ArgumentNullException(nameof(spec))).Clone();
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _terrain = !_world.Geometry(WorldIds.Ground, 0, out _);
            _shapes = _spec.Shapes;
            int n = _shapes.Length;
            if (n > 255) throw new ArgumentException("the contact spec holds more than 255 shapes", nameof(spec));
            _pairs = new Pair[n * PairsPerShape];
            _count = new int[n];
            _ground = new Pair[n];
            _order = new int[n];
            int at = 0, supports = 0;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    ContactShape s = _shapes[i];
                    bool disc = s.Role == ContactRole.Disc, sphere = !disc && Same(s.A, s.B);
                    if (pass == 0 && sphere || pass == 1 && !sphere && !disc || pass == 2 && disc) _order[at++] = i;
                    if (pass == 0 && s.Role == ContactRole.Support) supports++;
                }
            }
            _supports = new int[supports];
            for (int i = 0, k = 0; i < n; i++)
                if (_shapes[i].Role == ContactRole.Support) _supports[k++] = i;
            for (int i = 0; i < n; i++)
                if (_shapes[i].Role == ContactRole.Skid && _shapes[i].Corner >= 0 && _shapes[i].Corner < 4) _halfSkid[_shapes[i].Corner] = i;
            SurfaceTable table = _spec.Surfaces;
            if (table != null) _skids = Skids();
            int middles = _skids != null ? 4 : 0; // the supports' samples, then the skids' middles
            _at = new XZ[supports + middles];
            _samples = new GroundSample[supports + middles];
            _massShare = mass * _spec.DampingMassShare;

            // The world's materials by id, from the table by name; one the table lacks is refused. With surfaces.json in
            // Rain or Severe rain a row's wet block stands in (1.13: wet straw is softer, grips harder and clings); a flight
            // without the table reads the dry rows, as model 27's.
            bool wet = table != null && moisture != Moisture.Dry;
            IWorldQuery q = world.Query;
            int top = 0;
            for (int id = 1; id < ushort.MaxValue; id++)
                if (q.MaterialName((ushort)id) != null) top = id;
            _byMaterial = new Row[top + 1];
            bool clings = false;
            for (int id = 1; id <= top; id++)
            {
                string name = q.MaterialName((ushort)id);
                if (name == null) continue;
                if (!_spec.Row(name, out MaterialRow r))
                    throw new ArgumentException("the world's material '" + name + "' has no row in physics_materials.json", nameof(spec));
                if (wet && r.HasWet)
                {
                    double k = r.Rigid ? 0 : r.WetStiffness;
                    _byMaterial[id] = new Row
                    {
                        Known = true, Rigid = r.Rigid, StaticFriction = r.WetStaticFriction, KineticFriction = r.WetKineticFriction, Stiffness = k,
                        Damping = r.Rigid ? 0 : 2 * r.DampingRatio * DetMath.Sqrt(k * _massShare),
                        Adhesion = r.Rigid ? 0 : r.WetAdhesion, AdhesionRelease = r.Rigid ? 0 : r.WetAdhesionRelease,
                        Law = r.Rigid ? AreaLaw.None : r.AreaLaw, LawDepth = r.Rigid ? 0 : r.AreaReferenceDepth,
                        Crown = r.Rigid ? null : r.Crown,
                    };
                    clings |= _byMaterial[id].Adhesion > 0;
                    continue;
                }
                _byMaterial[id] = new Row
                {
                    Known = true, Rigid = r.Rigid, StaticFriction = r.StaticFriction, KineticFriction = r.KineticFriction, Stiffness = r.Stiffness,
                    Damping = r.Rigid ? 0 : 2 * r.DampingRatio * DetMath.Sqrt(r.Stiffness * _massShare),
                    Law = r.Rigid ? AreaLaw.None : r.AreaLaw, LawDepth = r.Rigid ? 0 : r.AreaReferenceDepth,
                    Crown = r.Rigid ? null : r.Crown,
                };
            }
            if (clings && _skids != null) _cling = new double[4];
            _groundRow = new Row { Known = true, Rigid = true, StaticFriction = _spec.GroundStaticFriction, KineticFriction = _spec.GroundKineticFriction };

            // The world's surfaces by id, from the table by name at the flight's moisture; one the table lacks is refused.
            if (table != null)
            {
                _soil = new SoilSet[256];
                _surfaceRow = new Row[256];
                _prints = new Print[PrintRing];
                for (int id = 1; id < 256; id++)
                {
                    string name = q.SurfaceName((byte)id);
                    if (name == null) continue;
                    int k = 0;
                    while (k < table.Surfaces.Length && !string.Equals(table.Surfaces[k].Name, name, StringComparison.Ordinal)) k++;
                    if (k == table.Surfaces.Length)
                        throw new ArgumentException("the world's surface '" + name + "' has no row in surfaces.json", nameof(spec));
                    SoilSet set = table.Surfaces[k].Set(moisture);
                    _soil[id] = set;
                    _surfaceRow[id] = new Row { Known = true, Rigid = true, StaticFriction = set.StaticFriction, KineticFriction = set.KineticFriction };
                    // The cohesive pull's rise on the soil's unload line ends at σ_a, which must come before δ_a (1.13).
                    for (int c = 0; set.Adhesion > 0 && c < 4; c++)
                    {
                        if (!_skids[c].Known) continue;
                        double ku = set.UnloadRatio * Soil.Modulus(set, Soil.Width(SoilPart.Tube, _skids[c].Radius));
                        if (!(set.Adhesion / ku < set.AdhesionRelease))
                            throw new ArgumentException("surfaces.json's " + name + " at " + moisture + ": σ_a over the unload modulus, " + set.Adhesion / ku
                                + " m, is not below adhesion_release_m " + set.AdhesionRelease, nameof(spec));
                    }
                }
                _cover = new CoverSet[table.Cover.Length];
                for (int i = 0; i < _cover.Length; i++) _cover[i] = table.Cover[i].Set(moisture);
            }
            for (int c = 0; _cling != null && c < 4; c++)
            {
                if (!_skids[c].Known) continue;
                for (int id = 1; id < _byMaterial.Length; id++)
                {
                    Row w = _byMaterial[id];
                    if (!(w.Adhesion > 0)) continue;
                    // The cling's rise on the layer's own stiffness ends at σ_a·A before δ_a, and its fall is gentler than the
                    // leg (Cling's closed form), at the largest footprint, the half-skid's whole width.
                    double a = w.Adhesion * 2 * _skids[c].Radius * _skids[c].Length, rise = a / w.Stiffness;
                    if (!(rise < w.AdhesionRelease) || !(a / (w.AdhesionRelease - rise) < _spec.LegStiffness))
                        throw new ArgumentException("physics_materials.json's " + q.MaterialName((ushort)id) + " wet block: its cling cannot let go under the leg", nameof(spec));
                }
            }

            _ring = new Double3[_spec.RingSegments + 1];
            for (int s = 0; s <= _spec.RingSegments; s++)
            {
                DetMath.SinCos(2 * 3.141592653589793 * s / _spec.RingSegments, out double sin, out double cos);
                _ring[s] = new Double3(cos, 0, sin);
            }

            // The chords: a straight rim segment is as far from a chord as its farther end, so the farthest vertex
            // between the chord's ends bounds them all.
            int ring = _spec.RingSegments;
            _chordReach = new double[(ring + ChordSegments - 1) / ChordSegments];
            for (int c = 0; c < _chordReach.Length; c++)
            {
                int from = c * ChordSegments, to = from + ChordSegments < ring ? from + ChordSegments : ring;
                for (int v = from + 1; v < to; v++)
                    _chordReach[c] = DetMath.Max(_chordReach[c], SegmentDistance(_ring[v], _ring[from], _ring[to]));
            }

            _base = (ContactShape[])_shapes.Clone();
            _wasA = new Double3[n];
            _wasB = new Double3[n];
            _off = new bool[n];
            for (int i = 0; i < n; i++) _off[i] = _shapes[i].Role == ContactRole.Blade; // until AttachRotors places them
            for (int i = 0; i < n; i++)
            {
                _wasA[i] = _shapes[i].A;
                _wasB[i] = _shapes[i].B;
            }
            _legOf = new int[n];
            for (int i = 0; i < n; i++) _legOf[i] = -1;
            if (_spec.Legs != null)
            {
                if (!(_spec.LegSideStiffness > 0)) throw new ArgumentException("the legs' law needs their side give (LegSideStiffness)", nameof(spec));
                if (random == null) throw new ArgumentNullException(nameof(random), "the legs' law draws its strengths from the flight's randomness");
                _legs = new LegDamage(_spec.Legs, random, _spec.LegJawSideStiffness);
                for (int i = 0; i < n; i++)
                    for (int c = 0; c < 4; c++)
                        if (_shapes[i].Role == ContactRole.Body && _shapes[i].Part == _spec.Legs.Parts[c])
                        {
                            _legOf[i] = c;
                            _legTube[c] = i;
                        }
                LegDamageSpec l = _spec.Legs;
                _skidBending = l.Modulus * l.SecondMoment;
                Double3 span = l.Bore[0] - l.Bore[l.Sibling[0]];
                _skidLever = span.Length();
                _skidContact = _legTube[0] >= 0 ? _shapes[_legTube[0]].Stiffness : 0;
            }
        }

        /// <summary>The legs' failure law, or null without it.</summary>
        public LegDamage Legs => _legs;

        /// <summary>A corner's state now (p7g-legs 1.3): what carries its skid half, what is left of its tube.</summary>
        public CornerState Corner(int corner) => _corner[corner];

        /// <summary>The skid's frame at a corner in the last step (p7g-legs, model 54): whether its leg swayed as its skid's frame
        /// along the skid, and its leg spring's give across at the foot that way and across the skid, N/m (the anchor in series;
        /// the same both ways off the frame).</summary>
        public bool Framed(int corner) => _frame[corner];
        public double SideGive(int corner) => _side[corner];
        public double FrameSideGive(int corner) => _frame[corner] ? _sideF[corner] : _side[corner];

        /// <summary>A hanging skid half's fold about its sibling's bore (body rotation vector, rad).</summary>
        public Double3 Fold(int corner) => _fold[corner];

        /// <summary>A shape as it is now (moved by its leg's set, its skid's fold, or a break), and whether it left the contact
        /// set.</summary>
        public ContactShape Shape(int i) => _shapes[i];

        /// <summary>A shape as the spec built it.</summary>
        public ContactShape Base(int i) => _base[i];
        public bool ShapeOff(int i) => _off[i];
        public int ShapeCount => _shapes.Length;

        /// <summary>A leg's or T-joint's state changed in the last Forces, so the drone's structure changes (Restructure).</summary>
        public bool RestructurePending => _restructure;

        /// <summary>The hanging skid's cantilever at the other leg's bore, 3EI/a³ (N/m), a the bores' distance (derived).</summary>
        public double HangingStiffness => _skidLever > 0 ? 3 * _skidBending / (_skidLever * _skidLever * _skidLever) : 0;

        /// <summary>
        /// A leg bent before the first step (the scenarios' damaged builds; a flight never calls it): the law's set, and its
        /// shapes turned about its clamp's edge by it; or, move false (1.3's known-wrong), the law's set with the shapes left
        /// where the build put them (a later set turns them by its own share only).
        /// </summary>
        public void PresetLeg(int leg, Double3 set, in FlightState s, bool move = true)
        {
            _legs.Preset(leg, set);
            if (!move) _setIgnored[leg] = _legs.SetRotation(leg);
            MoveShapes(s);
            for (int i = 0; i < _shapes.Length; i++)
            {
                _wasA[i] = _shapes[i].A;
                _wasB[i] = _shapes[i].B;
            }
        }

        /// <summary>
        /// A loose piece's contacts used again for another piece (LoosePieces, p7g-legs 1.3): every pair cleared, the shapes
        /// given (the rest left out of the set), and the damping taken on the piece's mass. Allocates nothing.
        /// </summary>
        public void Reuse(double mass, int[] shapes, int count, ContactShape[] geometry)
        {
            _massShare = mass * _spec.DampingMassShare;
            for (int id = 1; id < _byMaterial.Length; id++)
            {
                if (!_byMaterial[id].Known) continue;
                MaterialRow r;
                _spec.Row(_world.Query.MaterialName((ushort)id), out r);
                // The row's own stiffness, its wet block's in Rain (the constructor's), at the piece's mass.
                _byMaterial[id].Damping = r.Rigid ? 0 : 2 * r.DampingRatio * DetMath.Sqrt(_byMaterial[id].Stiffness * _massShare);
            }
            for (int i = 0; i < _shapes.Length; i++)
            {
                _off[i] = true;
                _count[i] = 0;
                _ground[i] = default;
            }
            for (int k = 0; k < count; k++)
            {
                int i = shapes[k];
                _off[i] = false;
                _shapes[i].A = _wasA[i] = geometry[k].A;
                _shapes[i].B = _wasB[i] = geometry[k].B;
            }
            _impactLeft = 0;
            _impacts = 0;
            _lastPeak = _lastImpulse = 0;
        }

        // Each corner's half-skid: its skid capsule (A its outer end, B the skid's middle) and its two supports, the one at
        // the capsule's A and the T-joint's between; a corner without them keeps the supports alone.
        Skid[] Skids()
        {
            var skids = new Skid[4];
            for (int c = 0; c < 4; c++)
            {
                int capsule = -1, end = -1, joint = -1;
                for (int i = 0; i < _shapes.Length; i++)
                    if (_shapes[i].Corner == c && _shapes[i].Role == ContactRole.Skid) capsule = i;
                if (capsule < 0) continue;
                ContactShape sk = _shapes[capsule];
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactShape s = _shapes[i];
                    if (s.Corner != c || s.Role != ContactRole.Support) continue;
                    if (Same(s.A, sk.A)) end = i;
                    else joint = i;
                }
                if (end < 0 || joint < 0) continue;
                Double3 axis = sk.A - sk.B;
                double length = axis.Length(), arc = Double3.Dot(_shapes[joint].A - sk.B, axis) / length;
                if (!(arc > 0 && arc < length)) continue;
                // The rule exact for 1, s and s² on the nodes 0, a and L: w1 = L³/(6a(L − a)), w2 = L/2 − L²/(6(L − a)).
                double w1 = length * length * length / (6 * arc * (length - arc)), w2 = length / 2 - length * length / (6 * (length - arc));
                skids[c] = new Skid
                {
                    Known = true, End = end, Joint = joint, Capsule = capsule, Middle = sk.B, Radius = sk.Radius, Length = length, Arc = arc,
                    W0 = length - w1 - w2, W1 = w1, W2 = w2,
                };
            }
            return skids;
        }

        static double SegmentDistance(Double3 p, Double3 a, Double3 b)
        {
            Double3 ab = b - a;
            double t = DetMath.Min(1, DetMath.Max(0, Double3.Dot(p - a, ab) / Double3.Dot(ab, ab)));
            return (p - (a + ab * t)).Length();
        }

        public ContactSpec Spec => _spec;

        /// <summary>A prop disc's pairs now (0 for any other shape): the shapes its slab cuts or its ring touched, in the order
        /// they came (PropStrikes reads them after each Sense).</summary>
        public int DiscPairs(int shape) => _shapes[shape].Role == ContactRole.Disc ? _count[shape] : 0;

        /// <summary>A disc's pair k: the world object and its shape, their material and surface.</summary>
        public void DiscPair(int shape, int k, out int obj, out int objShape, out ushort material, out byte surface)
        {
            ref Pair p = ref _pairs[shape * PairsPerShape + k];
            obj = p.Object;
            objShape = p.Shape;
            material = p.Material;
            surface = p.Surface;
        }

        /// <summary>A world surface's set at the flight's moisture (surfaces.json's, as the soil reads it); false without the table
        /// or for an id the world does not name.</summary>
        public bool SurfaceSet(byte surface, out SoilSet set)
        {
            bool known = _soil != null && _surfaceRow[surface].Known;
            set = known ? _soil[surface] : default;
            return known;
        }

        /// <summary>Impacts so far, and the latest one's peak acceleration (m/s²) and impulse (N·s): the feed's Impact.</summary>
        public int ImpactCount => _impacts;
        public double ImpactPeakAccel => _lastPeak;
        public double ImpactImpulse => _lastImpulse;

        // ---- Props are parts at rest (p7f-damage 1.3b, design 16, D-082) ---------------------------------------------------

        /// <summary>
        /// The rotors whose blades are this model's Blade shapes (FlightCore, before the first Sense; with ContactSpec.BladeRest
        /// and props.blade): from then on each Sense places a resting rotor's blades at its angle from their state (DamagedRotor)
        /// and each Forces gives the rotor its blades' moment about the shaft (Propulsion.SetShaftLoad). The stations' sums
        /// for a contact's compliance, per station j up to k: Σ Δx/EI, Σ x·Δx/EI, Σ x²·Δx/EI flatwise and edgewise and Σ
        /// Δx/EA, so a cantilever's compliance at r is r²·S0 − 2r·S1 + S2 over the stations inside r (unit load, derived).
        /// </summary>
        public void AttachRotors(Propulsion propulsion, PropulsionSpec spec)
        {
            _rotors = propulsion ?? throw new ArgumentNullException(nameof(propulsion));
            _blade = spec.Rotor.Blade;
            _bladeR = spec.Props.Diameter / 2;
            _bladesPer = spec.Props.Blades;
            int n = _shapes.Length;
            _bladeSlot = new int[n];
            _bladeSeg = new int[n];
            _bladeEdge = new int[n];
            _appeared = new bool[n];
            var seen = new int[4];
            int segments = ContactSpec.BladeSegmentEnds.Length;
            for (int i = 0; i < n; i++)
            {
                _bladeSlot[i] = -1;
                if (_shapes[i].Role != ContactRole.Blade) continue;
                int c = _shapes[i].Corner, k = seen[c]++;
                _bladeSlot[i] = c * _bladesPer + k / (2 * segments);
                _bladeSeg[i] = k / 2 % segments;
                _bladeEdge[i] = k % 2;
            }
            for (int i = 0; i < n; i++)
                if (_shapes[i].Role == ContactRole.Disc) _hub[_shapes[i].Corner] = _shapes[i].A;
            for (int c = 0; c < 4; c++) _spinSign[c] = spec.Motors[c].Clockwise ? -1 : 1;
            PropBlade b = _blade;
            int m = b.Stations.Length;
            _flat0 = new double[m];
            _flat1 = new double[m];
            _flat2 = new double[m];
            _edge0 = new double[m];
            _edge1 = new double[m];
            _edge2 = new double[m];
            _axial0 = new double[m];
            double dx = b.Span * _bladeR, f0 = 0, f1 = 0, f2 = 0, e0 = 0, e1 = 0, e2 = 0, a0 = 0;
            for (int k = 0; k < m; k++)
            {
                double x = b.Stations[k] * _bladeR, ch = b.Chord[k], t = b.Thickness[k];
                double eiF = _spec.SectionForm * b.FlexuralModulus * ch * t * t * t / 12, eiE = _spec.SectionForm * b.FlexuralModulus * t * ch * ch * ch / 12;
                f0 += dx / eiF;
                f1 += x * dx / eiF;
                f2 += x * x * dx / eiF;
                e0 += dx / eiE;
                e1 += x * dx / eiE;
                e2 += x * x * dx / eiE;
                a0 += dx / (b.FlexuralModulus * b.Fill[k] * ch * t);
                _flat0[k] = f0;
                _flat1[k] = f1;
                _flat2[k] = f2;
                _edge0[k] = e0;
                _edge1[k] = e1;
                _edge2[k] = e2;
                _axial0[k] = a0;
            }
            _restLoads = new RestLoad[4 * _bladesPer];
            _restWeight = new double[4 * _bladesPer];
            _restStrongest = new double[4 * _bladesPer];
        }

        /// <summary>
        /// The motors' bells (p7f-damage 1.5, MotorBell; FlightCore, with ContactSpec.MotorBell and Motors): from then on a contact
        /// on a motor sphere's turning band slides on the bell's surface while its rotor turns, its moment and the magnets' rub go
        /// to the rotor (Propulsion.SetMotor) and its ring dents past its collapse load (MotorDamaged).
        /// </summary>
        public void AttachBells(Propulsion propulsion, PropulsionSpec spec)
        {
            if (!_spec.MotorBell || _spec.Motors == null) return;
            _bells = new MotorBell(_spec, _shapes, propulsion, spec);
            _bellOf = new int[_shapes.Length];
            for (int i = 0; i < _bellOf.Length; i++) _bellOf[i] = -1;
            for (int c = 0; c < 4; c++) _bellOf[_bells.Shape(c)] = c;
            // The motors' heat and dirt (p7f-damage 1.6b): stepped by the bells' Finish, fed by their scrapes and the strikes' carves.
            if (_spec.MotorHeat) _health = _bells.Health = new MotorHealth(_spec, propulsion, _bells);
            // The shafts and bearings (p7f-damage 1.6): the bells' and the resting blades' loads, the strikes' (PropStrikes).
            if (!_spec.MotorShaft || spec.Rotor == null) return;
            _shafts = new MotorShaft(_spec, propulsion, spec, _bells);
            _bells.Shaft = _shafts;
        }

        /// <summary>The motors' bells, or null without them.</summary>
        public MotorBell Bells => _bells;

        /// <summary>The motors' heat and dirt (p7f-damage 1.6b), or null without them (no bells, or ContactSpec.MotorHeat off).</summary>
        public MotorHealth Health => _health;

        /// <summary>The motors' shafts and bearings (p7f-damage 1.6), or null without them (no bells, no props.blade, or
        /// ContactSpec.MotorShaft off).</summary>
        public MotorShaft Shafts => _shafts;

        // A pair as a load's cause (MotorShaft's events).
        static FlightEvent Cause(in Pair p) =>
            new FlightEvent { Object = p.Object, Shape = p.Shape, Material = p.Material, Surface = p.Surface, Point = p.Point };

        /// <summary>Whether rotor c's blades are its contact shapes now (it turns slower than BladeRestSpeed, no carve holds it).</summary>
        public bool Resting(int corner) => (_resting & (1 << corner)) != 0;

        /// <summary>The rotors a jammed carve holds (PropStrikes, bit 1 &lt;&lt; corner): its grind is that blade's contact, so
        /// their blades are no shapes until it ends.</summary>
        public void HoldBlades(int mask) => _carveHeld = mask;

        /// <summary>The rotors whose blades' passes the strikes judged this step (PropStrikes, bit 1 &lt;&lt; corner): their blades
        /// come back to rest unloaded in what they are inside (PlaceBlades).</summary>
        public void Passes(int mask) => _passed |= mask;

        /// <summary>Whether shape i touches anything now: a support's ground, or one of its pairs not carried by another.</summary>
        public bool Touching(int i)
        {
            if (_ground[i].Touching) return true;
            for (int k = 0; k < _count[i]; k++)
                if (_pairs[i * PairsPerShape + k].Touching && !_pairs[i * PairsPerShape + k].Suppressed) return true;
            return false;
        }

        /// <summary>The pairs touching a world material now, not held by another (forensics: what a drone at rest in a crown rests
        /// on, w9-village-physics 2.3).</summary>
        public int TouchingOn(ushort material)
        {
            int n = 0;
            for (int i = 0; i < _shapes.Length; i++)
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (p.Touching && !p.Suppressed && p.Surface == WorldIds.NoSurface && p.Material == material) n++;
                }
            return n;
        }

        /// <summary>A blade's load in the last Forces (slot corner × blades + blade).</summary>
        public RestLoad RestLoadOf(int slot) => _restLoads != null ? _restLoads[slot] : default;

        // Each rotor's blades to its angle now: a resting one's from its blades' state (tip, chord share, pitch and flap sets),
        // each segment's trailing and leading edge a chord apart less a radius each, at the section's pitch; a rotor turning
        // faster, or held by a carve, takes them off (their touching pairs broken). A blade shape coming back appears where its
        // blade is (no sweep from where it was a turn ago). After the strikes judged its rotor's passes (PropStrikes: a glance
        // through a ground the strikes do not know, a bend over a face, a crush, a carve) what it comes back inside is theirs: no
        // motion pressed its spring, so each pair it makes in this Sense keeps that overlap as its slack (Update, Forces). A rotor
        // about BladeRestSpeed back before any pass presses as its sweep took it, any part's first touch; at the flight's start the
        // blades stand where the setup sets the drone down, as every part does. A resting rotor's turn since the last placement is
        // its blades' slip, for their friction and damping (Forces).
        void PlaceBlades(in FlightState was, in FlightState now, StepEvents events)
        {
            int resting = 0;
            for (int c = 0; c < 4; c++)
                if (_spec.BladeRest && now.RotorSpeed[c] <= _spec.BladeRestSpeed && (_carveHeld & (1 << c)) == 0) resting |= 1 << c;
            for (int c = 0; c < 4; c++)
            {
                if ((resting & (1 << c)) == 0) continue;
                double turn = now.RotorAngle[c] - _restAngle[c];
                while (turn > 3.141592653589793) turn -= 6.283185307179586;
                while (turn <= -3.141592653589793) turn += 6.283185307179586;
                _restRate[c] = (_resting & (1 << c)) != 0 ? turn / _dt : 0;
                _restAngle[c] = now.RotorAngle[c];
            }
            for (int i = 0; i < _shapes.Length; i++)
            {
                int slot = _bladeSlot[i];
                if (slot < 0) continue;
                int c = slot / _bladesPer, b = slot % _bladesPer;
                if ((resting & (1 << c)) == 0 || !BladeEdge(i, c, b, now.RotorAngle[c], out Double3 a, out Double3 e))
                {
                    if (!_off[i]) Off(i, now.Step, events);
                    continue;
                }
                _shapes[i].A = a;
                _shapes[i].B = e;
                _appeared[i] = _off[i] && (_passed & (1 << c)) != 0;
                if (_off[i])
                {
                    _off[i] = false;
                    _wasA[i] = a;
                    _wasB[i] = e;
                }
            }
            _resting = resting;
            _passed &= ~resting;
        }

        // A world surface that is a soil (surfaces.json's set, not rigid): it yields to a blade at its bearing pressure, softer than
        // the blade (the hardness rule), so a blade meets it by the strikes' carve, never as a rigid face; at rest a blade in soil
        // bears nothing here (assumed: its bearing on the soil is left to a soil law for the blade, open). Without the table the
        // ground is rigid and a resting blade meets it as any body part does.
        bool YieldsToBlade(byte surface) => surface != WorldIds.NoSurface && _soil != null && _surfaceRow[surface].Known && !_soil[surface].Rigid;

        // A blade shape leaves the set: its touching pairs broken.
        void Off(int i, long step, StepEvents events)
        {
            _off[i] = true;
            for (int k = 0; k < _count[i]; k++)
            {
                ref Pair p = ref _pairs[i * PairsPerShape + k];
                if (p.Touching) Broken(ref p, i, step, events);
            }
            _count[i] = 0;
        }

        // Blade shape i's core at the rotor's angle (body): its segment's ends at their stations' chord, pitch and flap set, the
        // edge's offset along the chord from the mid-chord on the blade's radial line (assumed: the chord's middle on it), the
        // leading edge back by what a chip took. False when the segment lies past the blade's tip.
        bool BladeEdge(int i, int c, int b, double angle, out Double3 a, out Double3 e)
        {
            int g = _bladeSeg[i];
            double[] ends = ContactSpec.BladeSegmentEnds;
            BladeState state = _rotors.Rotor(c).Blade(b);
            double from = g == 0 ? _blade.Stations[0] - _blade.Span / 2 : ends[g - 1], to = DetMath.Min(ends[g], state.TipRadius);
            a = e = default;
            if (!(to > from)) return false;
            DetMath.SinCos(angle + 6.283185307179586 * b / _bladesPer, out double sp, out double cp);
            double s = _spinSign[c], rad = _shapes[i].Radius;
            var radial = new Double3(cp, 0, s * sp);
            var motion = new Double3(-sp, 0, s * cp);
            a = EdgePoint(c, state, radial, motion, from, _bladeEdge[i], rad);
            e = EdgePoint(c, state, radial, motion, to, _bladeEdge[i], rad);
            return true;
        }

        Double3 EdgePoint(int c, BladeState state, Double3 radial, Double3 motion, double x, int edge, double rad)
        {
            int k = BladeStation(x);
            double chord = _blade.Chord[k], theta = _blade.SectionAngle[k] + state.FactoryPitch + state.PitchSet[k];
            double off = edge == 0 ? -chord / 2 + rad : -chord / 2 + DetMath.Max(chord * state.ChordShare[k] - rad, rad);
            DetMath.SinCos(theta, out double st, out double ct);
            return _hub[c] + radial * (x * _bladeR) + new Double3(0, state.FlapSet[k], 0) + motion * (off * ct) + new Double3(0, off * st, 0);
        }

        int BladeStation(double x)
        {
            int k = (int)((x - (_blade.Stations[0] - 0.5 * _blade.Span)) / _blade.Span);
            return k < 0 ? 0 : k >= _blade.Stations.Length ? _blade.Stations.Length - 1 : k;
        }

        // A resting blade pair's normal stiffness (N/m) and the blade's share of its compliance: the blade a cantilever from
        // the hub, bent at the contact's radius r across its section at the station there (flatwise across the chord, edgewise
        // along it, the section's principal axes at its pitch; axially along the span), in series with its mount, the motor's
        // can (its shape's Stiffness across body y, StiffnessUp along it, as a body shape's). Also the contact's radius, the
        // blade's direction of motion there and the section's axes (body).
        double BladeStiffness(int i, in Pair p, in FlightState s, out double share, out double r, out Double3 motion, out Double3 flat, out Double3 along)
        {
            int slot = _bladeSlot[i], c = slot / _bladesPer, b = slot % _bladesPer;
            ContactShape shape = _shapes[i];
            Double3 at = s.Attitude.InverseRotate(p.Point - s.Position) - _hub[c];
            var plane = new Double3(at.X, 0, at.Z);
            r = plane.Length();
            Double3 radial = r > 0 ? plane * (1 / r) : new Double3(1, 0, 0);
            motion = new Double3(-radial.Z, 0, radial.X) * _spinSign[c];
            int k = BladeStation(r / _bladeR);
            BladeState state = _rotors.Rotor(c).Blade(b);
            DetMath.SinCos(_blade.SectionAngle[k] + state.FactoryPitch + state.PitchSet[k], out double st, out double ct);
            flat = motion * -st + new Double3(0, ct, 0);
            along = motion * ct + new Double3(0, st, 0);
            Double3 nb = s.Attitude.InverseRotate(p.N);
            double nf = Double3.Dot(nb, flat), ne = Double3.Dot(nb, along), nr = Double3.Dot(nb, radial);
            int j = k - 1; // the stations wholly inside r
            double cf = 0, ce = 0, ca = 0;
            if (j >= 0)
            {
                cf = r * r * _flat0[j] - 2 * r * _flat1[j] + _flat2[j];
                ce = r * r * _edge0[j] - 2 * r * _edge1[j] + _edge2[j];
                ca = _axial0[j];
            }
            double blade = nf * nf * cf + ne * ne * ce + nr * nr * ca;
            double up = nb.Y * nb.Y, mount = up / shape.StiffnessUp + (1 - up) / shape.Stiffness;
            share = blade / (blade + mount);
            return 1 / (blade + mount);
        }

        void ClearRest()
        {
            for (int j = 0; j < _restLoads.Length; j++)
            {
                _restLoads[j] = default;
                _restWeight[j] = _restStrongest[j] = 0;
            }
            for (int c = 0; c < 4; c++) _shaftTorque[c] = _shaftStiffness[c] = _shaftDamping[c] = 0;
        }

        // A resting blade pair's load (world fw, its normal force fn on the blade's series stiffness ks, kn with a layer's): into
        // its blade's (the force, its components across the section, the force-weighted radius, the strongest pair's deflection
        // of the blade itself, fn·c_blade, and its identity), and its rotor's moment about the shaft, r·(f·ê_t), with that
        // moment's stiffness against a turn, r²·(kn·(n·ê_t)² + k_t·(1 − (n·ê_t)²)), the stick spring's k_t while it holds
        // (derived: the turn moves the blade's material along ê_t into the face and along it).
        void Rest(int i, in Pair p, in FlightState s, Double3 fw, double fn, double ks, double kn, double cn, in Row row, Double3 v, double share, double r,
            Double3 motion, Double3 flat, Double3 along)
        {
            int slot = _bladeSlot[i], c = slot / _bladesPer;
            ref RestLoad l = ref _restLoads[slot];
            Double3 fb = s.Attitude.InverseRotate(fw);
            double size = fw.Length();
            l.Loaded = true;
            l.Force = l.Force + fw;
            l.Flat += Double3.Dot(fb, flat);
            l.Edge += Double3.Dot(fb, along);
            l.Radius += size * r;
            _restWeight[slot] += size;
            if (size > _restStrongest[slot])
            {
                _restStrongest[slot] = size;
                l.Deflection = fn * share / ks;
                l.Normal = fn;
                l.Object = p.Object;
                l.Shape = p.Shape;
                l.Material = p.Material;
                l.Surface = p.Surface;
                l.Point = p.Point;
            }
            _shaftTorque[c] += r * Double3.Dot(fb, motion);
            double nt = Double3.Dot(s.Attitude.InverseRotate(p.N), motion), across = 1 - nt * nt;
            bool stuck = p.Anchored && !p.Sliding;
            double kt = stuck ? _spec.FrictionStiffnessRatio * kn : 0;
            _shaftStiffness[c] += r * r * (kn * nt * nt + kt * across);
            // The damping against a turn: the normal's, cn·(n·ê_t)², and the friction's: the stick's damper while it holds, else
            // the kinetic friction's slope along ê_t, μ_k·F_n/|v_t| across its slip (its slip at least SlipFloor).
            Double3 n = p.N, vt = v - n * Double3.Dot(n, v), et = s.Attitude.Rotate(motion);
            double slip = vt.Length(), aligned = slip > 0 ? Double3.Dot(vt, et) / slip : 0;
            double friction = stuck ? DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cn * across
                : row.KineticFriction * fn / DetMath.Max(slip, SlipFloor) * (1 - aligned * aligned);
            _shaftDamping[c] += r * r * (cn * nt * nt + friction);
        }

        // A sliding blade's slip under which its friction's slope is taken, m/s (assumed: a micron a millisecond, far below any
        // slide the log can tell from a stick).
        const double SlipFloor = 1e-3;

        // The step's resting blades: each load's radius, the trace's loads and deflections, and each rotor's shaft load for the
        // next step's propulsion (none for a rotor that does not rest).
        void RestTrace(ref StepTrace trace)
        {
            for (int j = 0; j < _restLoads.Length; j++)
                if (_restWeight[j] > 0) _restLoads[j].Radius /= _restWeight[j];
            for (int c = 0; c < 4; c++)
            {
                RestLoad b0 = _restLoads[c * _bladesPer], b1 = _bladesPer > 1 ? _restLoads[c * _bladesPer + 1] : default;
                trace.BladeLoad0[c] = b0.Force.Length();
                trace.BladeLoad1[c] = b1.Force.Length();
                trace.BladeBend0[c] = b0.Deflection;
                trace.BladeBend1[c] = b1.Deflection;
                // The moment at rest: the one found at the blades' slip rate, less its damping's share of that rate. A prop lost in this
                // step (its shaft broke in MotorShaft.Finish, after these loads were summed) has no blades to load its shaft.
                if ((_resting & (1 << c)) != 0 && (_rotors.Lost & (1 << c)) == 0)
                    _rotors.SetShaftLoad(c, _shaftTorque[c] + _shaftDamping[c] * _restRate[c], _shaftStiffness[c], _shaftDamping[c], _restAngle[c]);
                else _rotors.SetShaftLoad(c, 0, 0, 0, 0);
            }
        }

        // ---- Forces: at the step's start ----------------------------------------------------------------------------------

        /// <summary>
        /// The contacts' force (world, N) and moment about the centre of mass (body, N·m) on the state's pose, added to
        /// force and moment only when some pair touches (so a flight in the air keeps its bits); the trace's contact
        /// fields; a crash by acceleration; the impact window.
        /// </summary>
        public void Forces(ref FlightState s, in MassProperties body, ref Double3 force, ref Double3 moment, ref StepTrace trace, StepEvents events)
        {
            Double3 f = default, m = default;
            Corners load = default, compression = default;
            int touching = 0, strongest = -1, strongestPair = -1;
            double strongestForce = 0;
            Double3 com = body.CenterOfMass;
            bool series = _spec.LegSideStiffness > 0;
            Double3 axis = s.Attitude.Rotate(new Double3(0, 1, 0)); // the legs', world

            // The legs, corner by corner: with their side give, each point's force now and the sums after the body shapes
            // (whose leg tubes load the legs); else model 37's one spring along the normal.
            if (series) LegTrials(s, axis, com, ref f, ref m, ref load, ref compression, ref touching, ref strongest, ref strongestPair, ref strongestForce);
            else for (int c = 0; c < 4; c++)
            {
                if (_skids != null && _skids[c].Known
                    && SoilCorner(c, s, com, ref f, ref m, ref load, ref compression, ref touching, ref strongest, ref strongestPair, ref strongestForce))
                {
                    if (_cling != null) _cling[c] = 0;
                    continue;
                }
                double deepest = 0, sum = 0, closing = 0;
                int deepShape = -1, deepPair = -1;
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    if (role == ContactRole.Support && _ground[i].Touching) Gather(ref _ground[i], i, -1, s, ref deepest, ref sum, ref closing, ref deepShape, ref deepPair);
                    for (int k = 0; k < _count[i]; k++)
                    {
                        ref Pair p = ref _pairs[i * PairsPerShape + k];
                        if (p.Touching && !p.Suppressed) Gather(ref p, i, k, s, ref deepest, ref sum, ref closing, ref deepShape, ref deepPair);
                    }
                }
                if (!(sum > 0))
                {
                    if (_cling != null && _cling[c] > 0) Cling(c, s, com, ref f, ref m, ref load, ref compression, ref touching);
                    continue;
                }
                closing /= sum;
                double k0 = _spec.LegStiffness, c0 = _spec.LegDamping;
                ref Pair deep = ref deepPair < 0 ? ref _ground[deepShape] : ref _pairs[deepShape * PairsPerShape + deepPair];
                Row row = RowOf(deep);
                // A layer meets the corner's half-skid by its footprint and compresses to its stroke, past which it is solid
                // and the leg alone takes the further sink (1.12, design 6: nothing beneath it here that SoilCorner carries).
                double kl = row.Stiffness, cl = row.Damping, stroke = double.PositiveInfinity;
                if (!row.Rigid) Layered(deep, row, _halfSkid[c] >= 0 ? _halfSkid[c] : deepShape, _halfSkid[c] >= 0, s, out kl, out cl, out stroke);
                double kn = row.Rigid ? k0 : Series(k0, kl), cn = row.Rigid ? c0 : Series(c0, cl);
                bool solid = !row.Rigid && kn * deepest >= kl * stroke;
                if (solid)
                {
                    kn = k0;
                    cn = c0;
                }
                double fn = solid ? k0 * (deepest - stroke) - c0 * closing : kn * deepest - cn * closing;
                if (fn < 0) fn = 0;
                load[c] = fn;
                compression[c] = deepest;
                if (_cling != null)
                {
                    // A wet layer's footprint (1.13): the half-skid lying along it, its chord at the layer's own compression
                    // (the static load over the layer's stiffness, at most its stroke) times its length; the widest since it
                    // last let go.
                    double area = 0;
                    if (row.Adhesion > 0)
                    {
                        Soil.Displaced(SoilPart.Tube, _skids[c].Radius, solid ? stroke : kn * deepest / kl, out _, out double chord);
                        area = chord * _skids[c].Length;
                    }
                    _cling[c] = row.Adhesion > 0 ? DetMath.Max(_cling[c], area) : 0;
                }
                // Each point's share, its friction, and the moment it makes.
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    if (role == ContactRole.Support && _ground[i].Touching)
                        Apply(ref _ground[i], i, -1, fn, kn, cn, sum, row, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                    for (int k = 0; k < _count[i]; k++)
                    {
                        ref Pair p = ref _pairs[i * PairsPerShape + k];
                        if (p.Touching && !p.Suppressed)
                            Apply(ref p, i, k, fn, kn, cn, sum, RowOf(p), s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                    }
                }
            }

            // The body shapes, pair by pair; then a resting prop's blades (p7f-damage 1.3b), as parts of the body whose material
            // moves with its rotor's turn, their moment about the shaft the rotor's in the next step's propulsion.
            if (_restLoads != null) ClearRest();
            _bells?.Begin(s);
            _shafts?.Begin();
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (role != ContactRole.Body && role != ContactRole.Blade || _off[i]) continue;
                ContactShape shape = _shapes[i];
                bool blade = role == ContactRole.Blade;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (!p.Touching || p.Suppressed) continue;
                    Row row = RowOf(p);
                    double ks, bladeShare = 0, r = 0;
                    Double3 motion = default, flat = default, along = default;
                    if (blade) ks = BladeStiffness(i, p, s, out bladeShare, out r, out motion, out flat, out along);
                    else
                    {
                        Double3 nb = s.Attitude.InverseRotate(p.N);
                        double up = nb.Y * nb.Y;
                        ks = 1 / (up / shape.StiffnessUp + (1 - up) / shape.Stiffness);
                    }
                    double cs = 2 * shape.DampingRatio * DetMath.Sqrt(ks * _massShare);
                    // A layer by this part's footprint, solid past its stroke: then the part's own spring alone (1.12, P7). The
                    // spring reads the depth past a blade's slack (PlaceBlades; 0 for every other pair).
                    double kl = row.Stiffness, cl = row.Damping, stroke = double.PositiveInfinity;
                    if (!row.Rigid) Layered(p, row, i, false, s, out kl, out cl, out stroke);
                    double kn = row.Rigid ? ks : Series(ks, kl), cn = row.Rigid ? cs : Series(cs, cl), depth = p.Depth - p.Slack;
                    if (!row.Rigid && kn * depth >= kl * stroke)
                    {
                        kn = ks;
                        cn = cs;
                        depth -= stroke;
                    }
                    if (blade)
                    {
                        int c = _shapes[i].Corner;
                        Double3 v = PointVelocity(s, p.Point) + s.Attitude.Rotate(motion * (_restRate[c] * r));
                        double fb = kn * depth - cn * Double3.Dot(v, p.N);
                        if (fb < 0) fb = 0;
                        Double3 fwb = Apply(ref p, i, k, fb, kn, cn, p.Depth, row, s, com, v, ref f, ref m, ref touching, ref strongest, ref strongestPair,
                            ref strongestForce);
                        Rest(i, p, s, fwb, fb, ks, kn, cn, row, v, bladeShare, r, motion, flat, along);
                        // Its load on the hub (p7f-damage 1.6): through the shaft to the bearings, its own bending capped at the root.
                        if (_shafts != null && fb > 0)
                            _shafts.Add(c, s.Attitude.InverseRotate(p.Point - s.Position), s.Attitude.InverseRotate(fwb), true, s.RotorSpeed[c], Cause(p));
                        continue;
                    }
                    double fn = kn * depth - cn * Double3.Dot(PointVelocity(s, p.Point), p.N);
                    if (fn < 0) fn = 0;
                    int bell = _bellOf != null ? _bellOf[i] : -1;
                    if (bell >= 0 && _bells.OnBand(bell, s.Attitude.InverseRotate(p.Point - s.Position), out Double3 surface, out bool stator))
                    {
                        // A motor's turning bell (p7f-damage 1.5): its material moves at ω·R along its turn, so a turning bell slides
                        // on what it touches (no anchor) and a stopped one holds by its anchor; its load goes to its rotor and ring.
                        Double3 vb = PointVelocity(s, p.Point) + s.Attitude.Rotate(surface * (s.RotorSpeed[bell] * _bells.Radius(bell)));
                        Double3 fwb = Apply(ref p, i, k, fn, kn, cn, p.Depth, row, s, com, vb, ref f, ref m, ref touching, ref strongest, ref strongestPair,
                            ref strongestForce, _bells.Turning(bell));
                        bool dented = _bells.Pair(bell, fn, s.Attitude.InverseRotate(fwb - p.N * fn), surface, stator, kn, row.StaticFriction,
                            out double dent, out double ringLoad, out double collapse);
                        // A turning bell on a soil scrapes it into its gap (p7f-damage 1.6b), by the contact's distance to its openings.
                        if (_health != null && fn > 0 && _bells.Turning(bell) && YieldsToBlade(p.Surface))
                        {
                            double y = s.Attitude.InverseRotate(p.Point - s.Position).Y;
                            _health.Scrape(bell, fn, s.RotorSpeed[bell] * _bells.Radius(bell), DetMath.Min(y - _bells.Lip(bell), _bells.Top(bell) - y),
                                _soil[p.Surface], _dt);
                        }
                        // The ring's share of the pair's load goes to the shaft (p7f-damage 1.6); past the gap the teeth carry the rest.
                        if (_shafts != null && fn > 0 && ringLoad > 0)
                            _shafts.Add(bell, s.Attitude.InverseRotate(p.Point - s.Position), s.Attitude.InverseRotate(fwb) * (ringLoad / fn), false, 0, Cause(p));
                        if (dented)
                        {
                            FlightEvent e = FlightEvent.Of(EventKind.MotorDamaged);
                            e.Part = (ushort)_bells.Part(bell);
                            e.Index = (byte)(bell | (stator ? 0 : 1) << 4);
                            e.Object = p.Object;
                            e.Shape = p.Shape;
                            e.Material = p.Material;
                            e.Surface = p.Surface;
                            e.Point = p.Point;
                            e.A = dent;
                            e.B = ringLoad;
                            e.C = collapse;
                            events.Add(e);
                        }
                        continue;
                    }
                    Double3 fw = Apply(ref p, i, k, fn, kn, cn, p.Depth, row, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                    int leg = _legOf[i];
                    if (leg < 0 || !(p.Depth > 0) || (_corner[leg] & (CornerState.Stub | CornerState.TubeGone)) != 0) continue;
                    // A leg tube's touch loads its leg: along it, and at the clamp's edge by its height under it.
                    double height = Double3.Dot(axis, World(s, _spec.Legs.Clamp[leg]) - p.Point);
                    _tubeForce[leg] = _tubeForce[leg] + fw;
                    _tubeMoment[leg] = _tubeMoment[leg] + (fw - axis * Double3.Dot(axis, fw)) * height;
                    double size = fw.Length();
                    if (size > _tubeStrongest[leg])
                    {
                        _tubeStrongest[leg] = size;
                        _tubeShape[leg] = i;
                        _tubePair[leg] = k;
                    }
                }
            }

            _shafts?.Finish(s, events);
            _bells?.Finish(s, _dt, ref trace, events);
            if (series) LegSums(s, axis, com, ref f, ref m, ref load, ref touching, ref strongest, ref strongestPair, ref strongestForce, events);
            LegTrace(ref trace);
            if (_restLoads != null) RestTrace(ref trace);

            trace.ContactForce = f;
            trace.ContactMoment = m;
            trace.LegLoad = load;
            trace.LegCompression = compression;
            trace.ContactsTouching = touching;
            if (touching > 0)
            {
                force = force + f;
                moment = moment + m;
            }

            // The crash by acceleration, and the impact window.
            double accel = f.Length() * body.InverseMass;
            if (_impactLeft > 0)
            {
                _impactPeak = DetMath.Max(_impactPeak, accel);
                _impactImpulse = _impactImpulse + f * _dt;
                _lastPeak = _impactPeak;
                _lastImpulse = _impactImpulse.Length();
            }
            if (!s.Crashed && accel > _spec.CrashAccel && strongest >= 0)
            {
                ref Pair p = ref strongestPair < 0 ? ref _ground[strongest] : ref _pairs[strongest * PairsPerShape + strongestPair];
                Crash(ref s, events, strongest, p, CrashCause.Acceleration, p.MadeSpeed);
            }
            if (_impactLeft > 0 && --_impactLeft == 0)
            {
                _impact.A = _impactPeak;
                _impact.B = _impactImpulse.Length();
                events.Add(_impact);
            }
        }

        // One touching point of a leg: its depth into the corner's deepest and sum, and its closing speed weighted by depth.
        void Gather(ref Pair p, int shape, int pair, in FlightState s, ref double deepest, ref double sum, ref double closing, ref int deepShape, ref int deepPair)
        {
            double depth = _spec.LegSideStiffness > 0 ? p.Overlap : p.Depth;
            if (!(depth > 0)) return;
            if (depth > deepest)
            {
                deepest = depth;
                deepShape = shape;
                deepPair = pair;
            }
            sum += depth;
            closing += depth * Double3.Dot(PointVelocity(s, p.Point), p.N);
        }

        // A hanging half's point's move along its normal by the half's elastic turn about the holding leg's bore (body), and by its
        // elastic move past the turn (HangingGive), m: what its overlap without them adds to its depth.
        double Unturned(int c, in Pair p, in FlightState s)
        {
            if (Same(_elastic[c], default) && Same(_shiftApplied[c], default)) return 0;
            int h = _spec.Legs.Sibling[c];
            Double3 bore = Rotated(_spec.Legs.Bore[h], h), at = s.Attitude.InverseRotate(p.Point - s.Position) - bore;
            if (!_spec.HangingGive) return Double3.Dot(p.N, s.Attitude.Rotate(at - Turn(_elastic[c]).Conjugate().Rotate(at)));
            at = at - _shiftApplied[c];
            Double3 moved = at - Turn(_elastic[c]).Conjugate().Rotate(at) + _shiftApplied[c];
            return Double3.Dot(p.N, s.Attitude.Rotate(moved));
        }

        // ---- The legs' side give (p7g-legs 1.2, design 3) -------------------------------------------------------------------

        // Each leg point's force before its friction cone. A leg is a spring along its tube (LegStiffness, in series with the
        // ground's material, as before) and across it at the foot (its side give in series with the stick anchor, the ratio
        // of the first): the point's push into it, d = δ_max·n − the anchor's stretch, splits along and across the tube, so a
        // gripping foot loads the leg through the tube's bending (262 N/m, never the anchor's 26 kN/m), and a tilted leg pushes
        // its foot along its tube, a strut. Its dampers act on the point's velocity the same way. The legs' law moves the
        // springs' rests (LegDamage's offsets) once a leg yields, bows, slips or turns. On the line (model 46) a leg's half-skid
        // in soil is the soil's series solve with this leg (SoilCorner, SoilTrial), a layer meets it by its footprint to its
        // stroke (Layered), and a corner lifting off a wet layer clings (Cling), as for the line's one spring.
        void LegTrials(in FlightState s, Double3 up, Double3 com, ref Double3 f, ref Double3 m, ref Corners load, ref Corners compression,
            ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce)
        {
            // The legs first, then the hanging halves, whose give is in series with the leg that holds them.
            for (int pass = 0; pass < 2; pass++)
            for (int c = 0; c < 4; c++)
            {
                CornerState state = _corner[c];
                bool hanging = (state & CornerState.Hanging) != 0;
                if ((pass == 1) != hanging) continue;
                _legTouching[c] = false;
                _lean[c] = 0;
                _frame[c] = false;
                _soilPoint[c].On = false;
                // A half-skid no longer on its own leg leaves its soil, its grooves into the ring; a hanging half meets the soil
                // as its points on its rigid surface (the soil's law is a leg's half-skid's).
                if (_skids != null && _skids[c].Engaged && (state & (CornerState.Hanging | CornerState.SkidGone)) != 0)
                    Leave(ref _skids[c], World(s, _skids[c].Middle), World(s, _shapes[_skids[c].End].A), default);
                if ((state & CornerState.SkidGone) != 0) continue;
                if (!hanging && _skids != null && _skids[c].Known)
                {
                    // The soil's pairs are its load centroid's (one point); the other points read their depth.
                    for (int i = 0; i < _shapes.Length; i++)
                    {
                        ContactRole role = _shapes[i].Role;
                        if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                        _ground[i].Overlap = 0;
                        for (int k = 0; k < _count[i]; k++)
                        {
                            ref Pair p = ref _pairs[i * PairsPerShape + k];
                            p.Overlap = p.Surface == WorldIds.NoSurface ? p.Depth : 0;
                        }
                    }
                    if (SoilCorner(c, s, com, ref f, ref m, ref load, ref compression, ref touching, ref strongest, ref strongestPair, ref strongestForce, true))
                    {
                        if (_cling != null) _cling[c] = 0;
                        continue;
                    }
                    _soilPoint[c].On = false;
                }
                double deepest = 0, sum = 0, closing = 0;
                int deepShape = -1, deepPair = -1;
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    for (int k = -1; k < _count[i]; k++)
                    {
                        ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                        if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                        p.Overlap = hanging ? p.Depth + Unturned(c, p, s) : p.Depth;
                        Gather(ref p, i, k, s, ref deepest, ref sum, ref closing, ref deepShape, ref deepPair);
                    }
                }
                double k0, c0, ks, cs, ratio = _spec.FrictionStiffnessRatio;
                if (hanging)
                {
                    // The skid's cantilever from the other bore (3EI/a³, the same along and across: a round tube) in series
                    // with the tube stock's own contact spring (its shapes' penetration spring), on each point's overlap of
                    // the half without its elastic turn: the massless half turns by its cantilever's give (Hang), so its
                    // shapes stand out of what it touches by the contact spring's share; damped at the tube's own ζ;
                    // across it, in series with the holding leg's side give.
                    k0 = Series(_skidContact, HangingStiffness);
                    c0 = 2 * _spec.LegSideDampingRatio * DetMath.Sqrt(k0 * _massShare);
                    ks = Series(k0, _side[_spec.Legs.Sibling[c]]);
                    cs = c0;
                }
                else
                {
                    LegGive(c, out k0, out c0, out ks, out cs);
                }
                _legTouching[c] = sum > 0;
                if (!(sum > 0))
                {
                    _axial[c] = k0;
                    _side[c] = Series(ks, ratio * k0);
                    if (_frame[c]) _sideF[c] = Series(_frameGive[c], ratio * k0);
                    if (!hanging && _cling != null && _cling[c] > 0) Cling(c, s, com, ref f, ref m, ref load, ref compression, ref touching);
                    continue;
                }
                // A hanging half that lies deepest in a crown's twig shell while it also meets something solid (a branch's bark, the
                // ground) is held by the solid (w9 2.6): the corner's one chain takes that point's row and overlap. On the twigs' row
                // the chain was the twigs' near-nothing in series, every point of the half carried nothing, and the half passed the
                // branch inside the shell (Pine002_M, 151 mm through its bark at 7.5 m/s).
                if (hanging && RowOf(deepPair < 0 ? _ground[deepShape] : _pairs[deepShape * PairsPerShape + deepPair]).Crown != null)
                {
                    double held = 0;
                    for (int i = 0; i < _shapes.Length; i++)
                    {
                        ContactRole role = _shapes[i].Role;
                        if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                        for (int k = -1; k < _count[i]; k++)
                        {
                            ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                            if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                            if (!(p.Overlap > held) || RowOf(p).Crown != null) continue;
                            held = p.Overlap;
                            deepShape = i;
                            deepPair = k;
                        }
                    }
                    if (held > 0) deepest = held;
                }
                compression[c] = deepest;
                ref Pair deep = ref deepPair < 0 ? ref _ground[deepShape] : ref _pairs[deepShape * PairsPerShape + deepPair];
                Row row = RowOf(deep);
                // A layer meets the corner's half-skid by its footprint and compresses to its stroke, past which it is solid and
                // the leg alone takes the further push (p7a-ground 1.12, as the line's one spring).
                double kr = row.Stiffness, cr = row.Damping, stroke = double.PositiveInfinity, push = deepest;
                if (!row.Rigid) Layered(deep, row, _halfSkid[c] >= 0 ? _halfSkid[c] : deepShape, _halfSkid[c] >= 0, s, out kr, out cr, out stroke);
                double ka = row.Rigid ? k0 : Series(k0, kr), ca = row.Rigid ? c0 : Series(c0, cr);
                bool solid = !row.Rigid && ka * deepest >= kr * stroke;
                if (solid)
                {
                    ka = k0;
                    ca = c0;
                    push = deepest - stroke;
                }
                if (!hanging && _cling != null)
                {
                    // A wet layer's footprint (1.13): the half-skid along it at the layer's own compression; the widest since it
                    // last let go.
                    double area = 0;
                    if (row.Adhesion > 0)
                    {
                        Soil.Displaced(SoilPart.Tube, _skids[c].Radius, solid ? stroke : ka * deepest / kr, out _, out double chord);
                        area = chord * _skids[c].Length;
                    }
                    _cling[c] = row.Adhesion > 0 ? DetMath.Max(_cling[c], area) : 0;
                }
                double kl = ks > 0 ? Series(ks, ratio * ka) : 0;
                double cl = cs > 0 ? Series(cs, DetMath.Sqrt(ratio) * ca) : 0;
                _axial[c] = ka;
                _side[c] = kl;
                _tubeSide[c] = hanging ? 0 : ks;
                // The skid's frame along the skid (model 54): its give and damper with the anchor's in series, as across.
                bool frame = _frame[c];
                double klf = 0, clf = 0;
                if (frame)
                {
                    double kf = _frameGive[c], cf = _frameDamp[c];
                    klf = kf > 0 ? Series(kf, ratio * ka) : 0;
                    clf = cf > 0 ? Series(cf, DetMath.Sqrt(ratio) * ca) : 0;
                    _sideF[c] = klf;
                    _tubeSideF[c] = kf;
                }
                bool moved = !hanging && _legs != null && _legs.Moved(c);
                double along = 0;
                Double3 across = default;
                if (moved)
                {
                    along = _legs.AxialOffset(c);
                    across = s.Attitude.Rotate(_legs.LateralOffset(c));
                }
                if (!hanging && _spec.LegLeverLength > 0 && ks > 0)
                {
                    // The lever (1.4): each point's force by the leg's statics at its own push; the leg's give its points' shares.
                    double anchor = ratio * ka, cap = SwayCap(c), tube = 0, side = 0, lean = 0, tubeF = 0, sideF = 0;
                    Double3 axis = LeverAxis(c, s, up);
                    if (frame) _frameAxis[c] = FrameAxis(s, axis, up);
                    for (int i = 0; i < _shapes.Length; i++)
                    {
                        ContactRole role = _shapes[i].Role;
                        if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                        for (int k = -1; k < _count[i]; k++)
                        {
                            ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                            if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                            if (frame)
                                FrameLeverTrial(ref p, s, axis, _frameAxis[c], sum, push, ka, ca, ks, _spec.LegLeverLength, _frameGive[c], _spec.LegFrameLeverLength,
                                    cap, anchor, cl, clf, moved, along, across, _dt, ref tube, ref side, ref tubeF, ref sideF, ref lean);
                            else
                                LeverTrial(ref p, s, axis, sum, push, ka, ca, ks, _spec.LegLeverLength, cap, anchor, cl, moved, along, across, _dt,
                                    ref tube, ref side, ref lean);
                        }
                    }
                    _side[c] = side;
                    _tubeSide[c] = tube;
                    _lean[c] = lean;
                    if (frame)
                    {
                        _sideF[c] = sideF;
                        _tubeSideF[c] = tubeF;
                    }
                    CapCrown(c, s);
                    continue;
                }
                if (frame) _frameAxis[c] = FrameAxis(s, up, up);
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    if (role == ContactRole.Support && _ground[i].Touching)
                        Trial(ref _ground[i], s, up, sum, push, ka, ca, kl, cl, moved, along, across, _dt, frame, klf, clf, _frameAxis[c]);
                    for (int k = 0; k < _count[i]; k++)
                    {
                        ref Pair p = ref _pairs[i * PairsPerShape + k];
                        if (p.Touching && !p.Suppressed) Trial(ref p, s, up, sum, push, ka, ca, kl, cl, moved, along, across, _dt, frame, klf, clf, _frameAxis[c]);
                    }
                }
                CapCrown(c, s);
            }
        }

        // A leg on a crown's twigs (2.3): what its points push on them is at most what the twigs it bears on carry, the leg one part
        // (its half-skid's silhouette) at the twigs' reach. The lever's statics (p7g-legs 1.4) turn a held foot's sideways stretch
        // into an axial force no twig carries (45 N a point, measured, a foot held 0.16 m off as the body fell through): past the
        // twigs' hold, or with its anchor past their reach, the twigs under it have broken, so its force is scaled to the hold and
        // its anchor lets go.
        void CapCrown(int c, in FlightState s)
        {
            double sum = 0, hold = 0;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    CrownRow crown = RowOf(p).Crown;
                    if (!p.Touching || p.Suppressed || crown == null) continue;
                    double fn = Double3.Dot(p.N, p.Force);
                    if (fn > 0) sum += fn;
                    hold = DetMath.Max(hold, CrownStiffness(crown, _halfSkid[c] >= 0 ? _halfSkid[c] : i, p.N, s) * crown.TwigReach);
                    if (Double3.Dot(p.Stretch, p.Stretch) > crown.TwigReach * crown.TwigReach) p.Stretch = default;
                }
            }
            if (!(sum > hold)) return;
            double scale = hold / sum;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (!p.Touching || p.Suppressed || RowOf(p).Crown == null) continue;
                    p.Force = p.Force * scale;
                    p.Stretch = default;
                }
            }
        }

        // The lever's axis (world): the leg's own, turned by its set about its clamp's edge (the set turns its shapes, so its
        // foot's rest lies on that axis and the lean is measured from it); the body's for an unbent leg, exactly.
        Double3 LeverAxis(int c, in FlightState s, Double3 up)
        {
            Quat q = _setTurn[c];
            return q.X == 0 && q.Y == 0 && q.Z == 0 ? up : s.Attitude.Rotate(q.Rotate(new Double3(0, 1, 0)));
        }

        // One leg point's force on the lever (p7g-legs 1.4): its push into the leg as Trial's, along the leg the lever's statics
        // at it (the foot's rise taken off, the axial force along the leaned link), across it the lever's secant in series with
        // the stick anchor, the dampers as Trial's; its shares of the leg's secant, its give with the anchor and its lean summed.
        static void LeverTrial(ref Pair p, in FlightState s, Double3 up, double sum, double deepest, double ka, double ca, double kt, double l0,
            double cap, double anchor, double cl, bool moved, double along, Double3 across, double dt, ref double tube, ref double side, ref double lean)
        {
            if (!(p.Overlap > 0)) return;
            double w = p.Overlap / sum;
            Double3 n = p.N, v = PointVelocity(s, p.Point);
            Double3 vt = v - n * Double3.Dot(n, v);
            if (!p.Anchored)
            {
                p.Stretch = default;
                p.Anchored = true;
                p.Sliding = false;
            }
            else
            {
                p.Stretch = p.Stretch + vt * dt;
            }
            p.Stretch = p.Stretch - n * Double3.Dot(n, p.Stretch);
            Double3 d = n * deepest - p.Stretch;
            double a = Double3.Dot(up, d);
            Double3 lat = d - up * a;
            if (moved)
            {
                a -= along;
                lat = lat - across;
            }
            double va = Double3.Dot(up, v), delta = lat.Length();
            double force = Lever(a, delta, ka, -ca * va, kt, l0, cap, out double secant, out _);
            double kl = OnAnchor(secant, anchor);
            p.Force = (up * force + lat * kl - (v - up * va) * cl) * w;
            p.Share = w;
            p.Drift = lat;
            tube += secant * w;
            side += kl * w;
            lean += delta * w;
        }

        // A leg's springs this step (p7g-legs 1.3): along it, k0 with its damper c0 (the tube's EA/L share scaled by its
        // crack's factor, the damper with it at the same ζ), and across it at the foot, ks with its damper cs: the tube's
        // 3EI/L³ by the crack's factor less the axial load's geometric stiffness, k_t·P/P_sway, never below 0; the jaws held
        // (their bed turns by their friction, LegDamage). Without the parts (1.2's chain), LegSideStiffness as it was.
        // The lever's buckling cap at leg c (p7g-legs 1.4): its bow factor on the sway load π²EI/(4L²), which the leg's drawn
        // modulus scales (leg.stiffness, w9-village-physics 2.9; 1 without the legs' law).
        double SwayCap(int c) => BowOverSway * _spec.LegSwayLoad * (_legs != null ? _legs.Modulus(c) : 1);

        void LegGive(int c, out double k0, out double c0, out double ks, out double cs)
        {
            k0 = _spec.LegStiffness;
            c0 = _spec.LegDamping;
            ks = _spec.LegSideStiffness;
            cs = 2 * _spec.LegSideDampingRatio * DetMath.Sqrt(ks * _massShare);
            _frame[c] = false;
            if (!(_spec.LegTubeSideStiffness > 0)) return;
            double f = _legs != null ? _legs.StiffnessFactor(c) : 1;
            if (f != 1 && _spec.LegTubeAxialStiffness > 0)
            {
                double k = 1 / (1 / k0 - 1 / _spec.LegTubeAxialStiffness + 1 / (f * _spec.LegTubeAxialStiffness));
                c0 *= DetMath.Sqrt(k / k0);
                k0 = k;
            }
            double kt = f * _spec.LegTubeSideStiffness;
            cs = 2 * _spec.LegSideDampingRatio * DetMath.Sqrt(kt * _massShare);
            // The skid's frame along the skid (p7g-legs, model 54): the frame leg's give and its damper at the same ζ, by the
            // crack's factor, while both legs on the skid hold it (LegDamage.Frame; every leg without the law).
            if (_spec.LegFrameSideStiffness > 0 && (_legs == null || _legs.Frame(c)))
            {
                double kf = f * _spec.LegFrameSideStiffness;
                _frame[c] = true;
                _frameDamp[c] = 2 * _spec.LegSideDampingRatio * DetMath.Sqrt(kf * _massShare);
                if (_spec.LegLeverLength > 0) _frameGive[c] = kf;
                else
                {
                    _frameGive[c] = kf - _spec.LegFrameSideStiffness * _lastAxial[c] / _spec.LegFrameSwayLoad;
                    if (!(_frameGive[c] > 0)) _frameGive[c] = 0;
                }
            }
            if (_spec.LegLeverLength > 0)
            {
                ks = kt; // the lever's bending at the foot, K/(γL)²: its axial load's statics are each point's (Lever)
                return;
            }
            ks = kt - _spec.LegTubeSideStiffness * _lastAxial[c] / _spec.LegSwayLoad;
            if (!(ks > 0)) ks = 0;
        }

        // The frame's way across leg c (world, p7g-legs model 54): the skid's axis (body z) square to the leg's axis there; the
        // body's z itself for an unbent leg on the body's axis, exactly.
        Double3 FrameAxis(in FlightState s, Double3 axis, Double3 up)
        {
            Double3 z = s.Attitude.Rotate(new Double3(0, 0, 1));
            if (axis.X == up.X && axis.Y == up.Y && axis.Z == up.Z) return z;
            Double3 e = z - axis * Double3.Dot(axis, z);
            return e * (1 / e.Length());
        }

        // A force across the leg by its two gives (p7g-legs, model 54): k on the push across, the frame's k_f on its share along
        // the frame's way; k alone off the frame, exactly.
        static Double3 Across(Double3 lat, double k, double kf, bool frame, Double3 axis) =>
            frame ? lat * k + axis * ((kf - k) * Double3.Dot(lat, axis)) : lat * k;

        /// <summary>The fixed–pinned column's load over the fixed–free one's, 4β²/π² = 8.183 (derived: LegDamageSpec.FixedPinned
        /// over π²/4): past it the tube bows (the legs' law, 1.2), so the lever's axial load is held there in its statics.</summary>
        public const double BowOverSway = LegDamageSpec.FixedPinned / 2.4674011002723395;

        // The leg as a lever across its tube (p7g-legs 1.4, ContactSpec.LegLeverLength): the foot pushed a along the leg and δ
        // across it from its rest, the link of length l0 from the pivot turned by θ = atan2(δ, l0 − a) and shortened to
        // l = √(δ² + (l0 − a)²), so its axial spring carries P = ka·(a − (l − (l0 − a))) + damp, the foot's rise on the circle
        // taken off; its bending K·θ (K = kt·l0²) across the link. The force on the body: along the leg P·cos θ + (Kθ/l)·sin θ,
        // across it toward the foot's offset (Kθ/l)·cos θ − P·sin θ, returned as its secant over δ (the axial load held at cap
        // in it, the bow's). To first order in δ, kt − P/l0 = kt·(1 − P/P_sway), 1.3's softening; at δ = 0 the force along is
        // ka·a + damp bit for bit.
        static double Lever(double a, double delta, double ka, double damp, double kt, double l0, double cap, out double secant, out double rise)
        {
            double h = l0 - a, l2 = delta * delta + h * h, l = DetMath.Sqrt(l2);
            rise = delta * delta / (l + h);
            double p = ka * (a - rise) + damp, theta = DetMath.Atan2(delta, h), k = kt * l0 * l0;
            double perDelta = delta > 0 ? theta / delta : 1 / h, held = p < cap ? p : cap;
            secant = k * perDelta * (h / l2) - held / l;
            return p * (h / l) + k * theta * delta / l2;
        }

        // A point's lever secant in series with its stick anchor; past half the anchor's stiffness the series nears its pole (the
        // chain compressed by more than 44 mm along it), held at its value there (assumed bound, never reached in a landing).
        static double OnAnchor(double secant, double anchor) => secant > -0.5 * anchor ? Series(secant, anchor) : -anchor;

        // One leg point's force: its share of its leg's push along and across the tube, less the dampers; the stick anchor
        // stretched by the touching material's slip, as Friction does.
        static void Trial(ref Pair p, in FlightState s, Double3 up, double sum, double deepest, double ka, double ca, double kl, double cl,
            bool moved, double along, Double3 across, double dt, bool frame = false, double klf = 0, double clf = 0, Double3 frameAxis = default)
        {
            if (!(p.Overlap > 0)) return;
            double w = p.Overlap / sum;
            Double3 n = p.N, v = PointVelocity(s, p.Point);
            Double3 vt = v - n * Double3.Dot(n, v);
            if (!p.Anchored)
            {
                p.Stretch = default;
                p.Anchored = true;
                p.Sliding = false;
            }
            else
            {
                p.Stretch = p.Stretch + vt * dt;
            }
            p.Stretch = p.Stretch - n * Double3.Dot(n, p.Stretch);
            Double3 d = n * deepest - p.Stretch;
            double a = Double3.Dot(up, d);
            Double3 lat = d - up * a;
            if (moved)
            {
                a -= along;
                lat = lat - across;
            }
            double va = Double3.Dot(up, v);
            p.Force = (up * (ka * a - ca * va) + Across(lat, kl, klf, frame, frameAxis) - Across(v - up * va, cl, clf, frame, frameAxis)) * w;
            p.Share = w;
            p.Drift = lat;
        }

        // LeverTrial on a frame leg (p7g-legs, model 54): the lever flown in each plane at the point's whole lean δ, across the
        // skid on the cantilever's give and link and along it on the frame's, its force along the leg blended by the lean's
        // share along the frame's way (w = (δ·e_f)²/δ²; exact in each plane), across it each way's secant in series with the
        // anchor, the dampers each way's.
        static void FrameLeverTrial(ref Pair p, in FlightState s, Double3 up, Double3 frameAxis, double sum, double deepest, double ka, double ca,
            double kt, double l0, double ktf, double l0f, double cap, double anchor, double cl, double clf, bool moved, double along, Double3 across,
            double dt, ref double tube, ref double side, ref double tubeF, ref double sideF, ref double lean)
        {
            if (!(p.Overlap > 0)) return;
            double w = p.Overlap / sum;
            Double3 n = p.N, v = PointVelocity(s, p.Point);
            Double3 vt = v - n * Double3.Dot(n, v);
            if (!p.Anchored)
            {
                p.Stretch = default;
                p.Anchored = true;
                p.Sliding = false;
            }
            else
            {
                p.Stretch = p.Stretch + vt * dt;
            }
            p.Stretch = p.Stretch - n * Double3.Dot(n, p.Stretch);
            Double3 d = n * deepest - p.Stretch;
            double a = Double3.Dot(up, d);
            Double3 lat = d - up * a;
            if (moved)
            {
                a -= along;
                lat = lat - across;
            }
            double va = Double3.Dot(up, v), delta = lat.Length(), lf = Double3.Dot(lat, frameAxis);
            double share = delta > 0 ? lf * lf / (delta * delta) : 0;
            double fs = Lever(a, delta, ka, -ca * va, kt, l0, cap, out double secant, out _);
            double ff = Lever(a, delta, ka, -ca * va, ktf, l0f, cap, out double secantF, out _);
            double kl = OnAnchor(secant, anchor), klf = OnAnchor(secantF, anchor);
            p.Force = (up * (fs + (ff - fs) * share) + Across(lat, kl, klf, true, frameAxis) - Across(v - up * va, cl, clf, true, frameAxis)) * w;
            p.Share = w;
            p.Drift = lat;
            tube += secant * w;
            side += kl * w;
            tubeF += secantF * w;
            sideF += klf * w;
            lean += delta * w;
        }

        // A leg point's force within its friction cone, the anchor left as it is: the loads the legs' law judges.
        static Double3 Coned(in Pair p, in Row row)
        {
            Double3 f = p.Force, n = p.N;
            double fn = Double3.Dot(n, f);
            if (!(fn > 0)) return default;
            Double3 ft = f - n * fn;
            double size = ft.Length(), limit = (p.Sliding ? row.KineticFriction : row.StaticFriction) * fn;
            return size > limit ? n * fn + ft * (row.KineticFriction * fn / size) : f;
        }

        // The legs' law and the legs' points' final forces: each corner judged on its loads (its points' within their cones,
        // its tubes'), what its give relieves taken off its points, then each point through its friction cone into the step's
        // force and moment, the anchor slid when it slips. The tubes' sums cleared for the next step.
        void LegSums(in FlightState s, Double3 up, Double3 com, ref Double3 f, ref Double3 m, ref Corners load, ref int touching,
            ref int strongest, ref int strongestPair, ref double strongestForce, StepEvents events)
        {
            // The hanging halves first: each one's fold at its bore, and its force and couple onto the leg that holds it.
            if (_legs != null)
                for (int c = 0; c < 4; c++)
                    if ((_corner[c] & CornerState.Hanging) != 0) Hang(c, s, up, events);
            for (int c = 0; c < 4; c++)
            {
                // A leg is judged while its tube is whole (its own points too while it holds its skid half); a stub or a
                // gone tube carries nothing.
                if (_legs != null)
                {
                    if ((_corner[c] & (CornerState.Stub | CornerState.TubeGone)) == 0) Judge(c, s, up, events);
                    else _legs.Idle(c);
                }
                if (_legTouching[c])
                {
                    double axial = 0;
                    for (int i = 0; i < _shapes.Length; i++)
                    {
                        ContactRole role = _shapes[i].Role;
                        if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                        if (role == ContactRole.Support && _ground[i].Touching && _ground[i].Overlap > 0)
                            axial += Final(ref _ground[i], i, -1, up, c, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                        for (int k = 0; k < _count[i]; k++)
                        {
                            ref Pair p = ref _pairs[i * PairsPerShape + k];
                            if (p.Touching && !p.Suppressed && p.Overlap > 0)
                                axial += Final(ref p, i, k, up, c, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                        }
                    }
                    if (_soilPoint[c].On) axial += SoilFinal(c, up, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                    load[c] = axial;
                }
                _lastAxial[c] = _legTouching[c] ? load[c] : 0;
                _tubeForce[c] = default;
                _tubeMoment[c] = default;
                _tubeStrongest[c] = 0;
            }
            if (_legs == null) return;
            MoveShapes(s);
            for (int c = 0; c < 4; c++)
                if ((_legs.Flags(c) & (LegFlags.Broken | LegFlags.Detached | LegFlags.SkidLoose)) != (_followed[c] & (LegFlags.Broken | LegFlags.Detached | LegFlags.SkidLoose)))
                    _restructure = true;
        }

        // A hanging skid half (p7g-legs 1.3): its points' forces within their cones, the moment they make about the holding
        // leg's bore, and past the skid's first yield there (the holding leg's drawn σ_y·I/c; w9 2.6: the bore edge's K_t lowers
        // only its local yield) the fold, perfectly plastic: the excess over the cap, (|F| − M_y/a)/k along F, turns the half about
        // the bore (from the next step its shapes, and its held points' anchors with them), and this step's force is relieved
        // by it. Its force and its couple about the bore load the holding leg (Judge).
        void Hang(int c, in FlightState s, Double3 up, StepEvents events)
        {
            LegDamageSpec l = _spec.Legs;
            int holder = l.Sibling[c];
            Double3 law = default, bore = World(s, HangBore(c));
            Double3 couple = default, deepAt = default, deepN = default;
            int causeShape = -1, causePair = -1;
            double strong = 0, deepest = 0;
            bool deepCrown = false;
            if (_legTouching[c])
            {
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    for (int k = -1; k < _count[i]; k++)
                    {
                        ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                        if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                        if (!(p.Overlap > 0)) continue;
                        if (p.Overlap > deepest)
                        {
                            deepest = p.Overlap;
                            deepAt = p.Point;
                            deepN = p.N;
                            deepCrown = RowOf(p).Crown != null;
                        }
                        Double3 fp = Coned(p, RowOf(p));
                        law = law + fp;
                        couple = couple + Double3.Cross(p.Point - p.Stretch - bore, fp);
                        double size = fp.Length();
                        if (size > strong)
                        {
                            strong = size;
                            causeShape = i;
                            causePair = k;
                        }
                    }
                }
            }
            _hangTouching[c] = _legTouching[c];
            double cap = _legs.YieldMoment(holder) / _skidLever, size2 = law.Length();
            if (_legTouching[c] && size2 > cap && _axial[c] > 0)
            {
                Double3 e = law * (1 / size2);
                double delta = (size2 - cap) / _axial[c];
                // The half turns about the bore so that its point at the lever (the joint's support) moves by δ along F.
                Double3 r = s.Attitude.InverseRotate(World(s, _shapes[JointSupport(c)].A) - bore);
                Double3 eb = s.Attitude.InverseRotate(e);
                double r2 = Double3.Dot(r, r);
                if (r2 > 0) _fold[c] = _fold[c] + Double3.Cross(r, eb) * (delta / r2);
                Double3 off = e * (_axial[c] * delta);
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    for (int k = -1; k < _count[i]; k++)
                    {
                        ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                        if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                        if (!(p.Overlap > 0)) continue;
                        p.Force = p.Force - off * p.Share;
                    }
                }
                law = law - off;
                couple = couple * (cap / size2);
                if ((_corner[c] & CornerState.Folded) == 0)
                {
                    _corner[c] |= CornerState.Folded;
                    FlightEvent ev = causeShape >= 0
                        ? Of(EventKind.LegSet, causeShape, causePair < 0 ? _ground[causeShape] : _pairs[causeShape * PairsPerShape + causePair])
                        : FlightEvent.Of(EventKind.LegSet);
                    ev.Part = (ushort)l.SkidParts[c];
                    ev.Index = (byte)LegMode.SkidFold;
                    ev.A = size2 * _skidLever;
                    ev.B = cap * _skidLever;
                    ev.C = causeShape >= 0 ? (causePair < 0 ? _ground[causeShape] : _pairs[causeShape * PairsPerShape + causePair]).MadeSpeed : 0;
                    events.Add(ev);
                }
            }
            // Folded past its hinge's tear at the bore (w9 2.6), the skid tears there and its holder lets it go: the half cannot
            // fold on through what it meets (the sweep's and the crowns' tunnels through a hanging half at a crash's speed).
            double tear = _legs.TearRotation(holder);
            if (Double3.Dot(_fold[c], _fold[c]) > tear * tear && (_legs.Flags(holder) & LegFlags.SkidLoose) == 0)
            {
                FlightEvent cause = causeShape >= 0
                    ? Of(EventKind.SkidLoose, causeShape, causePair < 0 ? _ground[causeShape] : _pairs[causeShape * PairsPerShape + causePair])
                    : FlightEvent.Of(EventKind.SkidLoose);
                _legs.TearSkid(holder, events, cause, _fold[c].Length(), tear, cause.C);
                _restructure = true;
            }
            // The elastic turn: the massless half gives |F|/k_cantilever, its deepest point moved that far out along its normal
            // by the least turn about the bore, φ = (r × n)·δ/|r × n|² (none untouched).
            _elastic[c] = default;
            _hangShift[c] = default;
            double carried = law.Length(), kc = HangingStiffness;
            if (deepest > 0 && carried > 0 && kc > 0)
            {
                Double3 r = s.Attitude.InverseRotate(deepAt - bore), n = s.Attitude.InverseRotate(deepN), axis = Double3.Cross(r, n);
                double a2 = Double3.Dot(axis, axis);
                if (!_spec.HangingGive)
                {
                    if (a2 > 0) _elastic[c] = axis * (carried / kc / a2);
                }
                else
                {
                    // The whole give of the half's chain but its contact spring's, along the deepest point's normal (p7g-legs, model
                    // 51), from the chain's springs at the deepest point's overlap g (its dampers act beside the chain, never
                    // bending it): the push g·n along the legs' axis (k_a) and across it (k_l) is the springs' force F; the
                    // cantilever gives F·n/k_c, by the turn φ = (r × n)·δ/|r|² across the half (r·n's share of it, which no turn
                    // about the bore gives, by the move); the rest of the chain across the axis, the holding leg's give across its
                    // tube and the stick anchor in series (1/k_l − 1/k_0), gives its share of the push across by the move. Each
                    // point's overlap without them is what the chain reads (Unturned); its shapes stand out of what they touch by
                    // the contact spring's share alone (and the touching material's own, a compliant row's).
                    double k0 = Series(_skidContact, kc), ka = _axial[c], kl = _side[c];
                    Double3 push = deepN * deepest;
                    double a = Double3.Dot(up, push);
                    Double3 lat = push - up * a;
                    // In a crown's twig shell the chain gives by the force its points carry (w9 2.6): the twigs give way first
                    // (CapCrown), so they carry less than the springs' push on the overlap, and read from the push alone the half
                    // gave by the whole overlap, which the next step read back as overlap (Unturned): the give ran away (0.43 m in
                    // 50 ms in Pine002_M) and the half's shapes passed a branch inside the shell. And no give moves the deepest point
                    // past its overlap.
                    Double3 spring = up * (ka * a) + lat * kl;
                    double size = spring.Length(), share = deepCrown && size > carried ? carried / size : 1;
                    double give = Double3.Dot(spring, deepN) / kc * share;
                    double r2 = Double3.Dot(r, r), rn = Double3.Dot(r, n), along = 0, turned = 0;
                    if (r2 > 0 && give > 0)
                    {
                        _elastic[c] = axis * (give / r2);
                        along = give * (rn * rn / r2);
                        turned = give * (a2 / r2);
                    }
                    if (kl > 0 && kl < k0) along += Double3.Dot(lat, deepN) * kl * (1 / kl - 1 / k0) * share;
                    double moved = turned + along;
                    if (moved > deepest)
                    {
                        _elastic[c] = _elastic[c] * (deepest / moved);
                        along *= deepest / moved;
                    }
                    _hangShift[c] = n * along;
                }
            }
            _hangForce[c] = law;
            _hangCouple[c] = s.Attitude.InverseRotate(couple);
            _hangShape[c] = causeShape;
            _hangPair[c] = causePair;
        }

        // A corner's support under its leg (the skid's sphere at the T-joint): the hanging half's lever point.
        readonly int[] _jointSupport = { -2, -2, -2, -2 };

        int JointSupport(int c)
        {
            if (_jointSupport[c] != -2) return _jointSupport[c];
            int best = -1;
            double near = double.PositiveInfinity;
            Double3 foot = _spec.Legs.Bore[c];
            for (int i = 0; i < _base.Length; i++)
            {
                if (_base[i].Corner != c || _base[i].Role != ContactRole.Support) continue;
                Double3 d = _base[i].A - foot;
                double dd = Double3.Dot(d, d);
                if (dd < near)
                {
                    near = dd;
                    best = i;
                }
            }
            _jointSupport[c] = best;
            return best;
        }

        // The legs' law on corner c: its points' forces within their cones and its tubes' as its loads, what its give relieves
        // taken off its points' forces (by their shares), and its events, caused by the contact that loads it most.
        void Judge(int c, in FlightState s, Double3 up, StepEvents events)
        {
            Double3 law = default, foot = World(s, HalfRotated(_spec.Legs.Foot[c], c)), skid = s.Attitude.Rotate(new Double3(0, 0, 1));
            double twist = 0, turning = 0, along = 0, strong = 0;
            int causeShape = -1, causePair = -1;
            Double3 drift = default, trial = default;
            bool own = (_corner[c] & (CornerState.Hanging | CornerState.SkidGone)) == 0, touching = own && _legTouching[c];
            if (touching)
            {
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    for (int k = -1; k < _count[i]; k++)
                    {
                        ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                        if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                        if (!(p.Overlap > 0)) continue;
                        Double3 fp = Coned(p, RowOf(p)), r = p.Point - p.Stretch - foot;
                        law = law + fp;
                        drift = drift + p.Drift * p.Share;
                        trial = trial + p.Force;
                        twist += Double3.Dot(Double3.Cross(r, fp), up);
                        Double3 across = r - up * Double3.Dot(up, r);
                        turning += p.Share * _side[c] * Double3.Dot(across, across);
                        along += Double3.Dot(fp, skid);
                        double size = fp.Length();
                        if (size > strong)
                        {
                            strong = size;
                            causeShape = i;
                            causePair = k;
                        }
                    }
                }
                // The soil's load centroid (p7g-legs on the line): its force within the groove's cone, and the wet groove's pull.
                if (_soilPoint[c].On)
                {
                    ref SoilPoint sp = ref _soilPoint[c];
                    ref Pair q = ref sp.Foot;
                    Double3 fp = SoilConed(c), r = q.Point - q.Stretch - foot;
                    law = law + fp + sp.Pull;
                    drift = drift + q.Drift * q.Share;
                    trial = trial + q.Force + sp.Pull;
                    twist += Double3.Dot(Double3.Cross(r, fp), up);
                    Double3 across = r - up * Double3.Dot(up, r);
                    turning += q.Share * _side[c] * Double3.Dot(across, across);
                    along += Double3.Dot(fp, skid);
                    double size = fp.Length();
                    if (size > strong)
                    {
                        strong = size;
                        causeShape = sp.Shape;
                        causePair = sp.Pair;
                    }
                }
            }
            bool tube = _tubeStrongest[c] > strong;
            if (tube)
            {
                causeShape = _tubeShape[c];
                causePair = _tubePair[c];
            }
            // The other half of the skid hanging from this T-joint: its force through the foot and its couple about the bore.
            Double3 footCouple = default;
            int hung = _spec.Legs.Sibling[c];
            if ((_corner[hung] & CornerState.Hanging) != 0 && _hangTouching[hung])
            {
                law = law + _hangForce[hung];
                along += Double3.Dot(_hangForce[hung], skid);
                footCouple = _hangCouple[hung];
                if (_hangShape[hung] >= 0 && (causeShape < 0 || _hangForce[hung].Length() > DetMath.Max(strong, _tubeStrongest[c])))
                {
                    causeShape = _hangShape[hung]; // the load comes through the hanging half: its contact is the cause
                    causePair = _hangPair[hung];
                }
            }
            double axial = Double3.Dot(up, law);
            // The tube's own deflection across at the foot (1.3): its share of the spring's push across, d·k_a/(k_s + k_a) with the
            // anchor's k_a in series (all of it with no give left), less what a sliding foot's cone lets through.
            Double3 lateral = law - up * axial, deflection = default;
            if (_spec.LegTubeSideStiffness > 0 && touching)
            {
                double anchor = _spec.FrictionStiffnessRatio * _axial[c], ks = _tubeSide[c];
                deflection = drift * (anchor / (ks + anchor));
                if (_frame[c])
                {
                    // Along the skid the frame's give takes its share of the push (model 54).
                    Double3 e = _frameAxis[c];
                    double onSkid = Double3.Dot(drift, e);
                    deflection = deflection + e * (onSkid * (anchor / (_tubeSideF[c] + anchor) - anchor / (ks + anchor)));
                }
                Double3 mine = lateral;
                if ((_corner[hung] & CornerState.Hanging) != 0 && _hangTouching[hung])
                    mine = mine - (_hangForce[hung] - up * Double3.Dot(up, _hangForce[hung]));
                Double3 tried = trial - up * Double3.Dot(up, trial);
                double t2 = Double3.Dot(tried, tried), o2 = Double3.Dot(mine, mine);
                if (t2 > 0 && o2 < t2) deflection = deflection * DetMath.Sqrt(o2 / t2);
            }
            var loads = new LegLoads
            {
                LawAxial = axial,
                LawLateral = s.Attitude.InverseRotate(lateral),
                Deflection = s.Attitude.InverseRotate(deflection),
                LawTouching = touching,
                ShapeAxial = Double3.Dot(up, _tubeForce[c]),
                ShapeMoment = s.Attitude.InverseRotate(_tubeMoment[c]),
                Twist = twist,
                TwistStiffness = turning,
                SkidAxial = along,
                AxialStiffness = _axial[c],
                LateralStiffness = _side[c],
                FootCouple = footCouple,
                Frame = _frame[c],
                FrameLateralStiffness = _frame[c] ? _sideF[c] : 0,
            };
            FlightEvent cause = FlightEvent.Of(EventKind.None);
            double speed = 0;
            if (causeShape >= 0)
            {
                ref Pair p = ref causePair < 0 ? ref _ground[causeShape] : ref _pairs[causeShape * PairsPerShape + causePair];
                cause = Of(EventKind.None, causeShape, p);
                speed = p.MadeSpeed;
            }
            _legs.Step(c, loads, out LegRelief relief, events, cause, speed);
            if (!relief.Any || !touching) return;
            Double3 off = up * relief.Axial + s.Attitude.Rotate(relief.Lateral);
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int k = -1; k < _count[i]; k++)
                {
                    ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                    if (k < 0 ? role != ContactRole.Support || !p.Touching : !p.Touching || p.Suppressed) continue;
                    if (!(p.Overlap > 0)) continue;
                    Double3 d = off * p.Share;
                    if (relief.Twist != 0) d = d + Double3.Cross(up, p.Point - p.Stretch - foot) * (p.Share * _side[c] * relief.Twist);
                    p.Force = p.Force - d;
                }
            }
            if (_soilPoint[c].On)
            {
                ref Pair q = ref _soilPoint[c].Foot;
                Double3 d = off * q.Share;
                if (relief.Twist != 0) d = d + Double3.Cross(up, q.Point - q.Stretch - foot) * (q.Share * _side[c] * relief.Twist);
                q.Force = q.Force - d;
            }
        }

        // The soil's point within the groove's cone (SoilFinal's limit), its anchor left as it is: the load the law judges.
        Double3 SoilConed(int c)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            Double3 f = sp.Foot.Force, n = sp.Foot.N;
            double fn = Double3.Dot(n, f);
            if (!(fn > 0)) return default;
            Double3 ft = f - n * fn;
            double size = ft.Length();
            if (!(size > 0)) return f;
            double along = DetMath.Abs(Double3.Dot(ft, sp.Axis)) / size;
            if (along > 1) along = 1;
            double plough = sp.Side * DetMath.Sqrt(1 - along * along) + sp.End * along;
            double limit = (_skids[c].Sliding ? sp.Kinetic : sp.Static) * fn + sp.Adhesion + plough;
            return size > limit ? n * fn + ft * ((sp.Kinetic * fn + sp.Adhesion + plough) / size) : f;
        }

        // Whether a leg holds any stiffness along a slip (w3-farmland-ground 2.2): the anchor's slide to the kinetic limit,
        // (size − kinetic)/k_tt, is taken only where k_tt is more than NilStiffness of the leg's axial spring's share. Below it the
        // leg is a mechanism that way (its side give spent past the sway load, the slip square to the tube) and, as at k_tt 0,
        // the stretch stays: dividing by it ran a 38° tumble's anchor 709 km in one step (k_tt 5e-7 N/m, 2.7e-9 of the axial
        // share), the foot then the moment's lever (207,766 m/s²).
        static bool Holds(double ktt, double axialShare) => ktt > NilStiffness * axialShare;

        // The anchor's stretch within what the leg can reach (w9-village-physics 2.4): the stretch is the foot's move from its
        // rest, the body's point, to where the ground holds it, and the leg reaches it only by turning its lever (p7g-legs 1.4,
        // the link of γL about its characteristic pivot): turned square, its foot is γL across, the most any turn of it moves
        // the foot across the leg. So the reach is γL (LegLeverLength), and along the skid's frame the frame's own lever
        // (LegFrameLeverLength), by the square of the stretch's way's cosine to the frame as the slide's stiffness is. A stretch
        // past it is a foot the leg no longer reaches: the ground has let it go, the leg springs back, and the hold starts
        // again where the foot is (the stretch 0). True when it let one go.
        // A held foot within its reach is ContactSpec.AnchorReach's (2.4, logged since 2.9).
        bool Reached(int c, ref Double3 stretch, double hold = double.PositiveInfinity)
        {
            double l0 = _spec.LegLeverLength, s2 = Double3.Dot(stretch, stretch);
            if (!_spec.AnchorReach || !(l0 > 0) || !(s2 > 0)) return false;
            double reach = l0;
            if (_frame[c])
            {
                double sf = Double3.Dot(stretch, _frameAxis[c]);
                reach += (_spec.LegFrameLeverLength - l0) * (sf * sf / s2);
            }
            if (hold < reach) reach = hold; // a crown's twig bent sideways past its reach has broken (2.3)
            if (!(s2 > reach * reach)) return false;
            stretch = default;
            return true;
        }

        /// <summary>A leg's stiffness along a slip below this share of its axial spring's is none, numerically (assumed: six orders
        /// under the axial; the legs' gives across their tubes are 2 to 3 orders under it, and a direction cosine of 1e-3 to the
        /// tube, 1e-6 of the axial, is past any slip the law resolves).</summary>
        public const double NilStiffness = 1e-6;

        // A leg point's force through its friction cone into the step's force and moment (Apply's bookkeeping): past the cone
        // it slides at μk·fn and its anchor slides along the force's way by the excess over the leg's stiffness that way;
        // no push, no anchor. Its force along the leg.
        double Final(ref Pair p, int shape, int pair, Double3 up, int c, in FlightState s, Double3 com, ref Double3 f, ref Double3 m,
            ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce)
        {
            Row row = RowOf(p);
            Double3 n = p.N, force = p.Force;
            double fn = Double3.Dot(n, force);
            if (!(fn > 0))
            {
                p.Anchored = false;
                fn = 0;
                force = default;
            }
            else
            {
                Double3 ft = force - n * fn;
                double size = ft.Length(), limit = (p.Sliding ? row.KineticFriction : row.StaticFriction) * fn;
                if (size > limit)
                {
                    double kinetic = row.KineticFriction * fn;
                    Double3 t = ft * (1 / size);
                    double ut = Double3.Dot(up, t), ktt = (_axial[c] * ut * ut + _side[c] * (1 - ut * ut)) * p.Share;
                    if (_frame[c])
                    {
                        double tf = Double3.Dot(t, _frameAxis[c]);
                        ktt += (_sideF[c] - _side[c]) * tf * tf * p.Share; // along the skid the frame's give (model 54)
                    }
                    force = n * fn + t * kinetic;
                    if (Holds(ktt, _axial[c] * p.Share))
                    {
                        Double3 st = p.Stretch + t * ((size - kinetic) / ktt);
                        p.Stretch = st - n * Double3.Dot(n, st);
                        // Every hold lets the foot go where the leg no longer reaches it (2.4's reach, the leg's geometry, whatever
                        // holds it): the ground, a soil shape (its groove's give is millimetres, left out as 2.4 left it), a rigid
                        // shape; a crown's twigs sooner, at their own reach δ̄, past which the twig bent sideways has broken (2.3).
                        Reached(c, ref p.Stretch, row.Crown != null ? row.Crown.TwigReach : double.PositiveInfinity);
                    }
                    p.Sliding = true;
                }
                else
                {
                    p.Sliding = false;
                }
            }
            // The leg is massless between the clamp and the foot, which stays where the ground holds it: its force on the body
            // (at the clamp, with the tube's moment) is its force at the foot, the contact point less the anchor's stretch. On
            // the body's own point it would turn the body about a support the soft leg lets wander centimetres (a drone past
            // its tip righted itself).
            f = f + force;
            m = m + Double3.Cross(s.Attitude.InverseRotate(p.Point - p.Stretch - s.Position) - com, s.Attitude.InverseRotate(force));
            touching++;
            p.Impulse += fn * _dt;
            if (fn > p.PeakForce) p.PeakForce = fn;
            if (fn > strongestForce)
            {
                strongestForce = fn;
                strongest = shape;
                strongestPair = pair;
            }
            return Double3.Dot(up, force);
        }

        // ---- A damaged leg changes the drone (p7g-legs 1.3, design 8) -------------------------------------------------------

        // The rotation of a rotation vector ρ (body, rad), exactly: (sin(θ/2)·ρ/θ, cos(θ/2)).
        static Quat Turn(Double3 rho)
        {
            double t = rho.Length();
            if (!(t > 0)) return Quat.Identity;
            DetMath.SinCos(t / 2, out double sin, out double cos);
            double k = sin / t;
            return new Quat(rho.X * k, rho.Y * k, rho.Z * k, cos);
        }

        readonly Quat[] _setTurn = { Quat.Identity, Quat.Identity, Quat.Identity, Quat.Identity };
        readonly Quat[] _foldTurn = { Quat.Identity, Quat.Identity, Quat.Identity, Quat.Identity };
        readonly bool[] _moved = new bool[4];
        readonly Double3[] _setIgnored = new Double3[4]; // a preset set the shapes do not follow (a scenario's known-wrong)

        // A point of the build (body) carried by leg c's set: turned about its clamp's edge.
        Double3 Rotated(Double3 p, int c)
        {
            Quat q = _setTurn[c];
            if (q.X == 0 && q.Y == 0 && q.Z == 0) return p; // an unbent leg's point as the build put it, exactly
            Double3 pivot = _spec.Legs.Clamp[c];
            return pivot + q.Rotate(p - pivot);
        }

        // A point of corner c's skid half (body) carried by its leg's set: as the tube's (Rotated), but for a frame leg's sway
        // (p7g-legs, model 54), which carries its T-joint square: the bore where the tube's turn takes it, the half turned about
        // it by the set less the sway. A set without a sway is the tube's, exactly.
        Double3 HalfRotated(Double3 p, int c)
        {
            if (!_swayed[c]) return Rotated(p, c);
            Double3 bore = _spec.Legs.Bore[c];
            return Rotated(bore, c) + _halfTurn[c].Rotate(p - bore);
        }

        readonly Quat[] _halfTurn = { Quat.Identity, Quat.Identity, Quat.Identity, Quat.Identity };
        readonly bool[] _swayed = new bool[4];

        // Where a shape's build point is now: a leg tube's by its leg's set; a skid half's by its leg's set, or hanging, by
        // the holding leg's set and then its fold about that leg's bore.
        Double3 Placed(int i, Double3 p)
        {
            int c = _base[i].Corner;
            if (_legOf[i] >= 0) return Rotated(p, _legOf[i]);
            if ((_corner[c] & CornerState.Hanging) == 0) return HalfRotated(p, c);
            int h = _spec.Legs.Sibling[c];
            Double3 bore = Rotated(_spec.Legs.Bore[h], h), q = HalfRotated(p, h);
            if (!_spec.HangingGive) return bore + _foldTurn[c].Rotate(q - bore);
            return bore + _foldTurn[c].Rotate(q - bore) + _shiftApplied[c];
        }

        // A hanging half's whole turn about its holder's bore: its fold and its elastic give.
        Double3 HangTurn(int c) => _fold[c] + _elastic[c];

        // The bore a hanging half turns about now (body): its holder's, by that leg's set, and moved with the half (HangingGive).
        Double3 HangBore(int c)
        {
            int h = _spec.Legs.Sibling[c];
            return _spec.HangingGive ? Rotated(_spec.Legs.Bore[h], h) + _shiftApplied[c] : Rotated(_spec.Legs.Bore[h], h);
        }

        // The shapes moved by the legs' sets and the hanging halves' folds since they last moved, each touching point's anchor
        // carried with it so a held foot stays where the ground holds it.
        void MoveShapes(in FlightState s)
        {
            for (int c = 0; c < 4; c++)
            {
                Double3 set = _legs.Set(c);
                _moved[c] = !Same(set, _setApplied[c]);
                if (!_moved[c]) continue;
                _setApplied[c] = set;
                _setTurn[c] = Turn(_legs.SetRotation(c) - _setIgnored[c]);
                _swayed[c] = _legs.Sway(c) != 0;
                if (_swayed[c]) _halfTurn[c] = Turn(_legs.HalfRotation(c) - _setIgnored[c]);
            }
            for (int i = 0; i < _shapes.Length; i++)
            {
                if (_off[i]) continue;
                int c = _base[i].Corner, leg = _legOf[i];
                bool move;
                if (leg >= 0) move = _moved[leg] && (_corner[leg] & (CornerState.Stub | CornerState.TubeGone)) == 0;
                else if (c < 0 || _base[i].Role != ContactRole.Support && _base[i].Role != ContactRole.Skid) continue;
                else if ((_corner[c] & CornerState.Hanging) != 0)
                    move = _moved[_spec.Legs.Sibling[c]] || !Same(HangTurn(c), _foldApplied[c]) || !Same(_hangShift[c], _shiftApplied[c]);
                else move = _moved[c];
                if (move) Place(i, Placed(i, _base[i].A), Placed(i, _base[i].B), s);
            }
            for (int c = 0; c < 4; c++)
            {
                if (Same(HangTurn(c), _foldApplied[c]) && Same(_hangShift[c], _shiftApplied[c])) continue;
                _foldApplied[c] = HangTurn(c);
                _foldTurn[c] = Turn(HangTurn(c));
                _shiftApplied[c] = _hangShift[c];
                for (int i = 0; i < _shapes.Length; i++)
                    if (!_off[i] && _legOf[i] < 0 && _base[i].Corner == c && (_base[i].Role == ContactRole.Support || _base[i].Role == ContactRole.Skid))
                        Place(i, Placed(i, _base[i].A), Placed(i, _base[i].B), s);
            }
        }

        // Shape i to its new core a–b (body): each touching pair's anchor moved by the core's move at its contact point.
        void Place(int i, Double3 a, Double3 b, in FlightState s)
        {
            ref ContactShape shape = ref _shapes[i];
            Double3 a0 = shape.A, b0 = shape.B;
            for (int k = -1; k < _count[i]; k++)
            {
                if (k < 0 && shape.Role != ContactRole.Support) continue;
                ref Pair p = ref k < 0 ? ref _ground[i] : ref _pairs[i * PairsPerShape + k];
                if (!p.Touching || !p.Anchored) continue;
                Double3 at = s.Attitude.InverseRotate(p.Point - s.Position), ab = b0 - a0;
                double len = Double3.Dot(ab, ab), t = len > 0 ? Double3.Dot(at - a0, ab) / len : 0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                Double3 d = s.Attitude.Rotate(a + (b - a) * t - (a0 + ab * t));
                d = d - p.N * Double3.Dot(p.N, d);
                p.Stretch = p.Stretch + d;
            }
            // Its soil (p7g-legs on the line): the half-skid's middle node with its capsule, and the groove's anchor, a material
            // point of the skid, moved with it, so a held foot stays where the soil holds it.
            int corner = _base[i].Corner;
            if (_skids != null && corner >= 0 && corner < 4 && _skids[corner].Known && _skids[corner].Capsule == i)
            {
                ref Skid k = ref _skids[corner];
                if (k.Anchored)
                {
                    Double3 ab = b0 - a0;
                    double len = Double3.Dot(ab, ab), t = len > 0 ? Double3.Dot(k.AnchorBody - a0, ab) / len : 0;
                    t = t < 0 ? 0 : t > 1 ? 1 : t;
                    k.AnchorBody = k.AnchorBody + (a + (b - a) * t - (a0 + ab * t));
                }
                k.Middle = b;
            }
            shape.A = a;
            shape.B = b;
        }

        /// <summary>
        /// The drone's structure after a leg or a T-joint let go (RestructurePending), called once after the step's move and
        /// before its Sense: what carries each corner's skid half (its leg; hanging from the other T-joint on its skid while
        /// that leg and T-joint hold; else gone with the skid), a broken leg's tube cut to its stub in the clamp, and the pieces
        /// that leave, their shapes out of the contact set (ContactBroken for each touching pair). The rule (design 4, the
        /// spec's "A leg comes off"): a leg that parts or slides out leaves with its T-joint (unless that T-joint's socket broke
        /// off it onto the skid); its skid stays on the drone while the other leg on it and that leg's T-joint hold it, and
        /// otherwise leaves too, with the leaving leg whose T-joint still holds it, or alone. The returned change lists the
        /// pieces for the flight's build and its loose bodies (FlightCore); it is reused, allocating nothing.
        /// </summary>
        public LegChange Restructure(in FlightState s, StepEvents events)
        {
            LegChange ch = _change;
            ch.Clear();
            _restructure = false;
            if (_legs == null) return ch;
            LegDamageSpec l = _spec.Legs;
            for (int side = 0; side < 2; side++)
            {
                // The side's two corners: a (its lower index) and b.
                int a = -1, b = -1;
                for (int c = 0; c < 4; c++)
                {
                    if ((l.Bore[c].X >= 0) != (side == 0)) continue;
                    if (a < 0) a = c;
                    else b = c;
                }
                if (a < 0 || b < 0) continue;
                bool skidWas = (_corner[a] & CornerState.SkidGone) == 0;
                bool skidOn = Holds(a) || Holds(b);
                int skidPiece = -1;
                for (int pass = 0; pass < 2; pass++)
                {
                    int c = pass == 0 ? a : b;
                    LegFlags now = _legs.Flags(c);
                    bool tubeWas = (_corner[c] & (CornerState.Stub | CornerState.TubeGone)) == 0;
                    bool tubeOn = (now & (LegFlags.Broken | LegFlags.Detached)) == 0;
                    if (tubeWas && !tubeOn)
                    {
                        int piece = ch.Pieces++;
                        bool off = (now & LegFlags.Detached) != 0;
                        ch.Parts[piece] = off ? 1 << c : 1 << (4 + c);
                        if (!_legs.TJointOffTube(c)) ch.Parts[piece] |= 1 << (8 + c);
                        int tube = _legTube[c];
                        if (tube >= 0)
                        {
                            if (off) Leave(tube, ch, piece, s, events);
                            else
                            {
                                // The broken tube's lower part leaves as its own piece with the tube's shape below the
                                // edge; the stub in the clamp stays as the tube's shape, cut at the edge.
                                Leave(tube, ch, piece, s, events);
                                ch.Geometry[piece][ch.ShapeCount[piece] - 1].A = l.Clamp[c];
                                _off[tube] = false;
                                ref ContactShape t = ref _shapes[tube];
                                t.A = _wasA[tube] = _base[tube].A;
                                t.B = _wasB[tube] = l.Clamp[c];
                                // The stub is no free tube: it is held in the clamp, so a touch on it meets the clamp's mount.
                                if (l.StubStiffness > 0)
                                {
                                    t.Stiffness = l.StubStiffness;
                                    t.StiffnessUp = l.StubStiffnessUp;
                                }
                                ch.Stubs |= 1 << c;
                            }
                        }
                        _corner[c] |= off ? CornerState.TubeGone : CornerState.Stub;
                        if (skidWas && !skidOn && (now & LegFlags.SkidLoose) == 0) skidPiece = piece;
                    }
                }
                if (skidWas && !skidOn)
                {
                    int piece = skidPiece;
                    if (piece < 0) piece = ch.Pieces++;
                    ch.Parts[piece] |= 1 << (12 + side);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int c = pass == 0 ? a : b;
                        if (_legs.TJointOffTube(c)) ch.Parts[piece] |= 1 << (8 + c);
                        if (_spec.HangingGive && (_corner[c] & CornerState.Hanging) != 0) Unload(c, s);
                        for (int i = 0; i < _shapes.Length; i++)
                            if (!_off[i] && _legOf[i] < 0 && _base[i].Corner == c && (_base[i].Role == ContactRole.Support || _base[i].Role == ContactRole.Skid))
                                Leave(i, ch, piece, s, events);
                        _corner[c] = (_corner[c] & ~(CornerState.Hanging | CornerState.Folded)) | CornerState.SkidGone;
                    }
                }
                else if (skidOn)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int c = pass == 0 ? a : b;
                        if (Holds(c) || (_corner[c] & CornerState.Hanging) != 0) continue;
                        // Its half now hangs from the other T-joint: placed by that leg's set from here.
                        _corner[c] |= CornerState.Hanging;
                        for (int i = 0; i < _shapes.Length; i++)
                            if (!_off[i] && _legOf[i] < 0 && _base[i].Corner == c && (_base[i].Role == ContactRole.Support || _base[i].Role == ContactRole.Skid))
                                Place(i, Placed(i, _base[i].A), Placed(i, _base[i].B), s);
                    }
                }
            }
            for (int c = 0; c < 4; c++) _followed[c] = _legs.Flags(c);
            return ch;
        }

        // A hanging half that leaves with its skid is unloaded: its elastic give springs back (its fold stays), so the piece
        // leaves whole, the half where its skid's other half holds it (HangingGive).
        void Unload(int c, in FlightState s)
        {
            _elastic[c] = default;
            _hangShift[c] = default;
            if (Same(HangTurn(c), _foldApplied[c]) && Same(_hangShift[c], _shiftApplied[c])) return;
            _foldApplied[c] = HangTurn(c);
            _foldTurn[c] = Turn(HangTurn(c));
            _shiftApplied[c] = default;
            for (int i = 0; i < _shapes.Length; i++)
                if (!_off[i] && _legOf[i] < 0 && _base[i].Corner == c && (_base[i].Role == ContactRole.Support || _base[i].Role == ContactRole.Skid))
                    Place(i, Placed(i, _base[i].A), Placed(i, _base[i].B), s);
        }

        // Leg c holds its half of the skid: it is on, and its T-joint holds the skid.
        bool Holds(int c) => (_legs.Flags(c) & (LegFlags.Broken | LegFlags.Detached | LegFlags.SkidLoose)) == 0;

        // Shape i leaves the contact set with a piece: its touching pairs broken (ContactBroken), its pairs cleared.
        void Leave(int i, LegChange ch, int piece, in FlightState s, StepEvents events)
        {
            ch.Geometry[piece][ch.ShapeCount[piece]] = _shapes[i];
            ch.Shapes[piece][ch.ShapeCount[piece]++] = i;
            if (_shapes[i].Role == ContactRole.Support && _ground[i].Touching) Broken(ref _ground[i], i, s.Step, events);
            _ground[i] = default;
            for (int k = 0; k < _count[i]; k++)
                if (_pairs[i * PairsPerShape + k].Touching) Broken(ref _pairs[i * PairsPerShape + k], i, s.Step, events);
            _count[i] = 0;
            _off[i] = true;
        }

        // The legs' state into the step's trace: without the law, intact and at full stiffness.
        void LegTrace(ref StepTrace trace)
        {
            uint state = 0, corners = 0;
            for (int c = 0; c < 4; c++)
            {
                trace.LegSideGive[c] = _side[c];
                trace.LegLean[c] = _lean[c];
                if (_legs == null)
                {
                    trace.LegSet[c] = default;
                    trace.LegStiffnessFactor[c] = 1;
                    trace.LegMoment[c] = 0;
                    trace.LegJaw[c] = default;
                    trace.SkidFold[c] = default;
                    continue;
                }
                state |= (uint)_legs.Flags(c) << (8 * c);
                corners |= (uint)_corner[c] << (8 * c);
                trace.LegSet[c] = _legs.Set(c);
                trace.LegStiffnessFactor[c] = _legs.StiffnessFactor(c);
                trace.LegMoment[c] = _legs.Moment(c);
                trace.LegJaw[c] = _legs.LateralOffset(c);
                trace.SkidFold[c] = _fold[c];
            }
            trace.LegState = state;
            trace.CornerState = corners;
        }

        // A pair's share of a normal force fn (its depth over total), its friction, into the step's force and moment; the
        // force it took (world).
        Double3 Apply(ref Pair p, int shape, int pair, double fn, double kn, double cn, double total, in Row row, in FlightState s, Double3 com,
            ref Double3 f, ref Double3 m, ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce) =>
            Apply(ref p, shape, pair, fn, kn, cn, total, row, s, com, PointVelocity(s, p.Point), ref f, ref m, ref touching, ref strongest,
                ref strongestPair, ref strongestForce);

        // The same with the touching material's velocity v (world): a resting blade's carries its rotor's turn, a turning bell's
        // its surface's, which slips (slip: no anchor, MotorBell).
        Double3 Apply(ref Pair p, int shape, int pair, double fn, double kn, double cn, double total, in Row row, in FlightState s, Double3 com,
            Double3 v, ref Double3 f, ref Double3 m, ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce, bool slip = false)
        {
            if (!(p.Depth > 0)) return default;
            double w = p.Depth / total, share = fn * w;
            Double3 ft = slip ? Slip(ref p, share, v, row)
                : Friction(ref p, share, _spec.FrictionStiffnessRatio * kn * w, DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cn * w, v, row, _dt);
            Double3 fw = p.N * share + ft;
            f = f + fw;
            m = m + Double3.Cross(s.Attitude.InverseRotate(p.Point - s.Position) - com, s.Attitude.InverseRotate(fw));
            touching++;
            p.Impulse += share * _dt;
            if (share > p.PeakForce) p.PeakForce = share;
            if (share > strongestForce)
            {
                strongestForce = share;
                strongest = shape;
                strongestPair = pair;
            }
            return fw;
        }

        /// <summary>
        /// Corner c lifting off a wet layer it pressed (1.13: wet straw clings): its deepest pair on a clinging row, at its
        /// overlap δ ≤ 0, held over the corner's footprint A by the soil's cohesive law (Soil.Cohesion) on the layer's own
        /// stiffness k, in series with the leg k0. Rising, the layer and the leg stretch with the part, F = kn·δ (kn their
        /// series), to −σ_a·A at δ = −σ_a·A/kn; then the bond fails over the part's separation s = x − δ (x = F/k0, the
        /// leg's stretch), F = −σ_a·A·(δ_a − s)/(δ_a − s_e) with s_e = σ_a·A/k, which solves to F = −c·(δ_a + δ)/(1 − c/k0),
        /// c = σ_a·A/(δ_a − s_e) (derived; the constructor holds s_e below δ_a and c below k0), nothing at δ = −δ_a. Static,
        /// like the soil's pull; past δ_a, or with no such pair left in the result, the footprint is cleared.
        /// </summary>
        void Cling(int c, in FlightState s, Double3 com, ref Double3 f, ref Double3 m, ref Corners load, ref Corners compression, ref int touching)
        {
            int shape = -1, pair = -1;
            double depth = double.NegativeInfinity;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (p.Suppressed || !(RowOf(p).Adhesion > 0) || !(p.Depth > depth)) continue;
                    depth = p.Depth;
                    shape = i;
                    pair = k;
                }
            }
            Row row = shape >= 0 ? RowOf(_pairs[shape * PairsPerShape + pair]) : default;
            if (shape < 0 || !(depth > -row.AdhesionRelease))
            {
                _cling[c] = 0;
                return;
            }
            ref Pair q = ref _pairs[shape * PairsPerShape + pair];
            // The layer's own stiffness under the clinging footprint, the half-skid's whole length (its area's, 1.12): at
            // least the row's in every law, so the constructor's check at the row's holds.
            double kl = row.Law != AreaLaw.None ? row.Stiffness * Footprint(q, row, false, _skids[c].Length, 2 * _skids[c].Radius) : row.Stiffness;
            double k0 = _spec.LegStiffness, kn = Series(k0, kl), peak = row.Adhesion * _cling[c], fn;
            if (kn * depth >= -peak) fn = kn * depth;
            else
            {
                double slope = peak / (row.AdhesionRelease - peak / kl);
                fn = -slope * (row.AdhesionRelease + depth) / (1 - slope / k0);
            }
            Double3 fw = q.N * fn;
            f = f + fw;
            m = m + Double3.Cross(s.Attitude.InverseRotate(q.Point - s.Position) - com, s.Attitude.InverseRotate(fw));
            touching++;
            load[c] = fn;
            compression[c] = depth;
        }

        // ---- Compliant layers (p7a-ground 1.12) -----------------------------------------------------------------------------

        /// <summary>
        /// A layer row under a touching part (design 6): its stiffness and damping by the part's footprint on pair p's face, by
        /// the row's area law at the layer's depth (Layer.Scale, Layer.Depth), the damping at the row's ratio (√ of the
        /// factor); and its stroke, the share _stroke of the shape's thickness along the pair's normal (Layer.Thickness). The
        /// part's footprint: a sphere's the disc of its diameter; a body capsule's its length on the face (Layer.Pressed) times
        /// its diameter; a corner's half-skid (along) its whole length lying along the face, whichever of the corner's pairs is
        /// deepest: the leg is one spring, and a footprint read on its deepest pair's face would change by a step whenever that
        /// pair passed to another face (between S04's overlapping tiles). A compliant row without an area law (a sheet's, a
        /// mesh's or a crown's give, not a layer's) keeps its own and no stroke.
        /// </summary>
        void Layered(in Pair p, in Row row, int part, bool along, in FlightState s, out double k, out double c, out double stroke)
        {
            k = row.Stiffness;
            c = row.Damping;
            stroke = double.PositiveInfinity;
            if (row.Crown != null)
            {
                // No damper: the twigs' resistance is their bending and breaking (W̄ a twig), not a viscous one; the row's ζ on k_e
                // gave 0.3–1 N·s/m a part, 30 times the yield at 2 m/s. Their inertia, ½·m_e·v² a twig met, is left out (design
                // 2.3: 4 % of W̄ at 2 m/s, half at 7 m/s, of a hold that is itself 1.6 % of the drone's weight).
                k = CrownStiffness(row.Crown, part, p.N, s);
                c = 0;
                return;
            }
            if (row.Law == AreaLaw.None) return;
            ContactShape sh = _shapes[part];
            bool disc = Same(sh.A, sh.B);
            double length = disc ? 0 : along ? (sh.B - sh.A).Length() : Layer.Pressed(World(s, sh.A), World(s, sh.B), sh.Radius, p.N);
            double scale = Footprint(p, row, disc, length, 2 * sh.Radius, out double thickness);
            k = row.Stiffness * scale;
            c = row.Damping * DetMath.Sqrt(scale);
            stroke = _stroke * thickness;
        }

        // ---- A crown's twigs (w9-village-physics 2.3, design.md 2.3) -------------------------------------------------------

        // A part pushed along n into a crown's twigs (L_V of them a m³, random directions) meets L_V·p/4 of them a metre, p the
        // perimeter of its silhouette along n (a capsule's 2·l⊥ + π·w, a sphere's π·w: the lines hitting a convex body, L_V·S/4),
        // and each takes W̄ to break, so past the twigs' mean reach δ̄ it carries F_y = (L_V·p/4)·W̄ steadily; its stiffness is
        // that hold over δ̄ (the secant; CrownRow's W̄ and δ̄). In series with the part's own, as every compliant row's.
        double CrownStiffness(CrownRow k, int part, Double3 n, in FlightState s)
        {
            ContactShape sh = _shapes[part];
            double perimeter = 3.141592653589793 * 2 * sh.Radius;
            if (!Same(sh.A, sh.B))
            {
                Double3 ab = s.Attitude.Rotate(sh.B - sh.A), across = ab - n * Double3.Dot(n, ab);
                perimeter += 2 * across.Length();
            }
            return k.TwigLength * perimeter / 4 * k.TwigWork / k.TwigReach;
        }

        // A crown pair's yield: the depth its spring reads is at most the twigs' mean reach δ̄. Pushed deeper, the twigs it bent
        // past it have broken and it meets fresh ones: its plane moves in with it. Backing out, the plane stays (the twigs
        // behind it are broken; it meets nothing until it is past where it was).
        static void Yield(ref Pair p, double reach)
        {
            double x = p.Depth - reach;
            if (!(x > 0)) return;
            p.P = p.P - p.N * x;
            p.Point = p.Point - p.N * x;
            p.Depth = reach;
        }

        // A layer row's factor for a footprint on pair p's shape (Layer.Scale at the layer's depth), and the shape's thickness
        // along the pair's normal; a shape the world no longer has reads as a plane (the row's depth, no stroke).
        double Footprint(in Pair p, in Row row, bool disc, double length, double width, out double thickness)
        {
            double depth = double.PositiveInfinity;
            thickness = double.PositiveInfinity;
            if (_world.Geometry(p.Object, p.Shape, out ShapeGeometry g))
            {
                depth = Layer.Depth(g);
                thickness = Layer.Thickness(g, p.N);
            }
            return Layer.Scale(row.Law, row.LawDepth, depth, disc, length, width);
        }

        double Footprint(in Pair p, in Row row, bool disc, double length, double width) => Footprint(p, row, disc, length, width, out _);

        /// <summary>
        /// The lift of soil pair q's face under a layer at its stroke (design 6's carry-through): the corner c's deepest
        /// touching layer pair of q's own object whose face lies over q's (the same side, q's face within the layer's
        /// thickness plus Layer.CarryReach under its top along its normal), solid at its stroke, carries the part onto q, so
        /// q's face reads sep − stroke higher (sep its depth under the layer's top); 0 with none, or where q's face reaches
        /// above the layer's stroke (the part meets it first).
        /// </summary>
        double Carry(int c, in Pair q)
        {
            double best = double.NegativeInfinity, lift = 0;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (!p.Touching || p.Suppressed || p.Surface != WorldIds.NoSurface || p.Object != q.Object || !(p.Depth > best)) continue;
                    Row row = RowOf(p);
                    if (row.Law == AreaLaw.None || !(Double3.Dot(p.N, q.N) > 0) || !_world.Geometry(p.Object, p.Shape, out ShapeGeometry g)) continue;
                    double thickness = Layer.Thickness(g, p.N), sep = Double3.Dot(p.N, p.P - q.P), stroke = _stroke * thickness;
                    if (!(sep > 0 && sep <= thickness + Layer.CarryReach)) continue;
                    best = p.Depth;
                    lift = sep > stroke ? sep - stroke : 0;
                }
            }
            return lift;
        }

        // ---- The soil under a half-skid (p7a-ground 1.10) -----------------------------------------------------------------

        /// <summary>
        /// Corner c's leg on soil (design 2, 3), when its half-skid lies below a soil's surface at some node: the series solve
        /// with the leg, the soil's force and damping at its load centroid with the groove's anchor, the corner's other points
        /// (rigid or compliant, Surface 0) through the same leg, the grooves' state and the footprint ring. False leaves the
        /// corner to the leg alone on its points, as without the table (no soil under it, or a rigid surface).
        ///
        /// With the legs' side give (series, p7g-legs on the line, model 46) the leg is p7g's: its springs this step (LegGive:
        /// the crack's factor, P·Δ), along the soil's normal its tube's stiffness and its side give by its tilt, k·cos²φ +
        /// k_q·sin²φ, less what the anchor's stretch and the law's rests take off along it (the strut: a foot slid along its
        /// tube unloads it), solved in series with the soil as before; across, the groove's anchor (ratio × the normal's, the
        /// line's) in series with the tube's side give. The soil's normal force, its tangential trial (the strut and the side
        /// give, before the groove's cone) and the other points' shares become the corner's points for the legs' law (Judge),
        /// which takes its relief off them; LegSums puts each through its cone at the foot, where the ground holds it.
        /// </summary>
        bool SoilCorner(int c, in FlightState s, Double3 com, ref Double3 f, ref Double3 m, ref Corners load, ref Corners compression,
            ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce, bool series = false)
        {
            ref Skid k = ref _skids[c];
            double r = k.Radius;
            Double3 c0 = World(s, k.Middle), c1 = World(s, _shapes[k.Joint].A), c2 = World(s, _shapes[k.End].A);
            bool holding = k.Engaged && (k.Wet0 > 0 || k.Wet1 > 0 || k.Wet2 > 0); // its wet groove still clings (1.13)

            // The deepest soil shape the corner's parts touch: where the plane is absent (a patch, a hole band) it is the ground.
            // Under a layer of its own object at its stroke the soil's face reads lifted by the layer's solid rest (1.12, Carry).
            int soilShape = -1, soilPair = -1;
            double soilDepth = 0, lift = 0;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (p.Surface == WorldIds.NoSurface) continue;
                    double up = Carry(c, p);
                    if ((p.Touching || up > 0) && p.Depth + up > soilDepth)
                    {
                        soilDepth = p.Depth + up;
                        soilShape = i;
                        soilPair = j;
                        lift = up;
                    }
                }
            }
            // A groove clinging to a soil shape holds the skid above its face (1.13): that shape's pair while it is in the result.
            double clingDepth = double.NegativeInfinity;
            bool clingSearch = soilShape < 0 && holding && k.Object != WorldIds.Ground;
            for (int i = 0; clingSearch && i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (p.Object != k.Object || p.Shape != k.Shape || !(p.Depth > clingDepth)) continue;
                    clingDepth = p.Depth;
                    soilShape = i;
                    soilPair = j;
                }
            }
            Pair none = default;
            ref Pair soil = ref soilShape >= 0 ? ref _pairs[soilShape * PairsPerShape + soilPair] : ref none;
            if (clingDepth > double.NegativeInfinity) lift = Carry(c, soil);
            bool has0 = Node(k.Ground, c0, r, soil, soilShape >= 0, lift, out double e0, out Double3 n0, out byte s0, out int o0, out int h0);
            bool has1 = Node(_ground[k.Joint], c1, r, soil, soilShape >= 0, lift, out double e1, out Double3 n1, out byte s1, out int o1, out int h1);
            bool has2 = Node(_ground[k.End], c2, r, soil, soilShape >= 0, lift, out double e2, out Double3 n2, out byte s2, out int o2, out int h2);

            // The deepest node sets the surface, the normal and the soil's identity (design 5's rule, carried).
            double deepNode = double.NegativeInfinity;
            Double3 n = default;
            byte surface = WorldIds.NoSurface;
            int obj = 0, shp = 0;
            if (has0 && e0 > deepNode) { deepNode = e0; n = n0; surface = s0; obj = o0; shp = h0; }
            if (has1 && e1 > deepNode) { deepNode = e1; n = n1; surface = s1; obj = o1; shp = h1; }
            if (has2 && e2 > deepNode) { deepNode = e2; n = n2; surface = s2; obj = o2; shp = h2; }
            bool soft = surface != WorldIds.NoSurface && _surfaceRow[surface].Known && !_soil[surface].Rigid;
            if (k.Engaged && (!soft || !(deepNode > 0) && !holding || k.Object != obj || k.Shape != shp || k.Surface != surface)) Leave(ref k, c0, c2, n);
            if (!soft || !(deepNode > 0) && !k.Engaged) return false;
            if (!k.Engaged) Land(ref k, obj, shp, surface, c0, c2, n);

            // The corner's other points (rigid or compliant shapes, Surface 0) as one spring through the same leg, as P7's.
            double deep = 0, sum = 0, closing = 0;
            int deepShape = -1, deepPair = -1;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (p.Touching && !p.Suppressed && p.Surface == WorldIds.NoSurface) Gather(ref p, i, j, s, ref deep, ref sum, ref closing, ref deepShape, ref deepPair);
                }
            }
            bool other = sum > 0;
            Row orow = other ? RowOf(_pairs[deepShape * PairsPerShape + deepPair]) : default;
            // A layer among them meets the half-skid by its footprint, to its stroke (1.12): over this soil it carries the part
            // onto it past the stroke (Carry's lift), elsewhere it is solid there and stops the leg as a rigid point does.
            double ko = 0, oc = 0, ostroke = double.PositiveInfinity;
            if (other && !orow.Rigid) Layered(_pairs[deepShape * PairsPerShape + deepPair], orow, _halfSkid[c], true, s, out ko, out oc, out ostroke);
            bool carrying = lift > 0 && other && orow.Law != AreaLaw.None && _pairs[deepShape * PairsPerShape + deepPair].Object == soil.Object;

            // The series solve: k·x = F_soil(δ − x) + k_o·min(δ_o − x, its stroke)⁺, safeguarded Newton from the last step's x;
            // a rigid other point holds the leg to its depth, a solid layer to its stroke.
            SoilSet set = _soil[surface];
            double k0 = _spec.LegStiffness, c0leg = _spec.LegDamping;
            // The legs' side give (p7g-legs on the line): the leg's springs this step, and its stiffness along the soil's normal
            // with its offset there, k_N·x − b (the line's k·x without it).
            double legSide = 0, legSideDamp = 0, kN = k0, b = 0, along = 0;
            Double3 legUp = default, across = default, dt = default;
            if (series)
            {
                LegGive(c, out k0, out c0leg, out legSide, out legSideDamp);
                legUp = s.Attitude.Rotate(new Double3(0, 1, 0));
                if (_spec.LegLeverLength > 0 && legSide > 0) legUp = LeverAxis(c, s, legUp);
                if (_legs != null && _legs.Moved(c))
                {
                    along = _legs.AxialOffset(c);
                    across = s.Attitude.Rotate(_legs.LateralOffset(c));
                }
                if (k.Anchored)
                {
                    dt = World(s, k.AnchorBody) - k.Anchor;
                    dt = dt - n * Double3.Dot(n, dt);
                }
                double cu = Double3.Dot(legUp, n), ud = Double3.Dot(legUp, dt);
                double kq0 = legSide > 0 ? Series(legSide, _spec.FrictionStiffnessRatio * k0) : 0, rise = 0;
                bool frame = _frame[c];
                double kq0f = 0;
                if (frame)
                {
                    _frameAxis[c] = FrameAxis(s, legUp, s.Attitude.Rotate(new Double3(0, 1, 0)));
                    kq0f = _frameGive[c] > 0 ? Series(_frameGive[c], _spec.FrictionStiffnessRatio * k0) : 0;
                }
                if (_spec.LegLeverLength > 0 && legSide > 0)
                {
                    // The lever (1.4) at the last step's sink: the foot's rise on its circle is an axial rest of the solve, and
                    // the lever's secant its give across (SoilTrial flies both at this step's).
                    Double3 d = n * k.X - dt;
                    double a = Double3.Dot(legUp, d) - along;
                    Double3 lat = d - legUp * Double3.Dot(legUp, d) - across;
                    double delta = lat.Length();
                    Lever(a, delta, k0, 0, legSide, _spec.LegLeverLength, SwayCap(c), out double secant, out rise);
                    kq0 = OnAnchor(secant, _spec.FrictionStiffnessRatio * k0);
                    if (frame)
                    {
                        // The frame's plane on its own lever (model 54): the rise blended by the lean's share along the skid.
                        Lever(a, delta, k0, 0, _frameGive[c], _spec.LegFrameLeverLength, SwayCap(c), out double secantF, out double riseF);
                        double lf = Double3.Dot(lat, _frameAxis[c]), share = delta > 0 ? lf * lf / (delta * delta) : 0;
                        rise += (riseF - rise) * share;
                        kq0f = OnAnchor(secantF, _spec.FrictionStiffnessRatio * k0);
                    }
                }
                kN = k0 * cu * cu + kq0 * (1 - cu * cu);
                b = k0 * cu * (ud + along + rise) - kq0 * cu * ud + kq0 * Double3.Dot(n, across);
                if (frame)
                {
                    // Along the skid the frame's give (model 54): its share of the push across along the soil's normal.
                    double nf = Double3.Dot(n, _frameAxis[c]);
                    kN += (kq0f - kq0) * nf * nf;
                    b += (kq0f - kq0) * nf * (Double3.Dot(dt, _frameAxis[c]) + Double3.Dot(across, _frameAxis[c]));
                }
            }
            double kl = Soil.Modulus(set, Soil.Width(SoilPart.Tube, r)), firm = _spec.Surfaces.FirmLayer;
            double lo = 0, hi = DetMath.Max(deepNode, other ? deep : 0);
            // Wet soil (1.13): the groove's cohesive pull enters the solve as a tension, so the leg may stretch by up to the
            // whole pull over its stiffness; the bracket's top stays at least 0, where no node presses.
            bool wet = set.Adhesion > 0;
            double ku = wet ? set.UnloadRatio * kl : 0;
            if (wet)
            {
                lo = -set.Adhesion * (k.W0 * k.Wet0 + k.W1 * k.Wet1 + k.W2 * k.Wet2) / (series ? kN : k0);
                if (hi < 0) hi = 0;
            }
            if (series && b < 0 && b / kN < lo) lo = b / kN; // a leg lengthened by its slip pushes the skid deeper
            double x = DetMath.Min(hi, DetMath.Max(lo, k.X));
            for (int it = 0; it < NewtonSteps; it++)
            {
                double fs = Tube(k, set, kl, firm, e0 - x, e1 - x, e2 - x, out double ks);
                if (wet)
                {
                    fs -= Pulls(k, set, ku, e0 - x, e1 - x, e2 - x, out double kp, out _, out _, out _);
                    ks += kp;
                }
                double fo = ko > 0 && deep > x ? ko * DetMath.Min(deep - x, ostroke) : 0;
                double legForce = series ? kN * x - b : k0 * x;
                double residual = legForce - fs - fo, slope = (series ? kN : k0) + ks + (ko > 0 && deep > x && deep - x < ostroke ? ko : 0);
                if (residual == 0) break;
                if (residual > 0) hi = x;
                else lo = x;
                double next = x - residual / slope;
                x = next >= lo && next <= hi ? next : 0.5 * (lo + hi); // a converged step lands on its bracket's end
            }
            // The rigid point, or a solid layer that carries nothing beneath, stops the leg short of the soil's solution.
            double floor = !other ? double.NegativeInfinity : orow.Rigid ? deep : carrying ? double.NegativeInfinity : deep - ostroke;
            bool held = floor > x;
            if (held) x = floor;
            bool past = other && !orow.Rigid && deep - x >= ostroke; // a layer at its stroke: solid, its damper still

            // The soil's force at the solution: its nodes, its load centroid, its footprints now and at the deepest sinks.
            double z0 = e0 - x, z1 = e1 - x, z2 = e2 - x;
            double f0 = Soil.Force(SoilPart.Tube, r, set, kl, firm, z0, k.Max0, out double t0);
            double f1 = Soil.Force(SoilPart.Tube, r, set, kl, firm, z1, k.Max1, out double t1);
            double f2 = Soil.Force(SoilPart.Tube, r, set, kl, firm, z2, k.Max2, out double t2);
            double soilStatic = k.W0 * f0 + k.W1 * f1 + k.W2 * f2, soilStiffness = k.W0 * t0 + k.W1 * t1 + k.W2 * t2;
            Soil.Displaced(SoilPart.Tube, r, z0, out _, out double w0);
            Soil.Displaced(SoilPart.Tube, r, z1, out _, out double w1);
            Soil.Displaced(SoilPart.Tube, r, z2, out _, out double w2);
            double footprint = k.W0 * w0 + k.W1 * w1 + k.W2 * w2;
            Soil.Displaced(SoilPart.Tube, r, DetMath.Max(k.Max0, z0), out _, out double m0);
            Soil.Displaced(SoilPart.Tube, r, DetMath.Max(k.Max1, z1), out _, out double m1);
            Soil.Displaced(SoilPart.Tube, r, DetMath.Max(k.Max2, z2), out _, out double m2);
            double wetted = k.W0 * m0 + k.W1 * m1 + k.W2 * m2;
            double arc = soilStatic > 0 ? (k.W1 * f1 * k.Arc + k.W2 * f2 * k.Length) / soilStatic : k.Arc;
            Double3 point = c0 + (c2 - c0) * (arc / k.Length) - n * r;
            double soilDamping = set.Damping * footprint, cs = soilDamping > 0 ? Series(c0leg, soilDamping) : 0;
            double fn = soilStatic - cs * Double3.Dot(PointVelocity(s, point), n);
            if (fn < 0) fn = 0;

            // Its anchor: the groove's passive resistance across the half-skid (its sunk length) and at its outer end.
            Double3 axis = c2 - c0;
            axis = axis - n * Double3.Dot(n, axis);
            axis = axis * (1 / axis.Length());
            double p0 = z0 > 0 ? z0 : 0, p1 = z1 > 0 ? z1 : 0, p2 = z2 > 0 ? z2 : 0;
            double side = 0.5 * kl * (k.W0 * p0 * p0 + k.W1 * p1 * p1 + k.W2 * p2 * p2), end = 0.5 * kl * 2 * r * p2 * p2;
            double kn = soilStiffness > 0 ? Series(k0, soilStiffness) : 0;
            Double3 ft = default;
            if (!series)
            {
                ft = SoilFriction(ref k, s, point, n, fn, _spec.FrictionStiffnessRatio * kn, DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cs,
                    axis, set, set.InterfaceAdhesion * wetted, side, end);
                Double3 fw = n * fn + ft;
                f = f + fw;
                m = m + Double3.Cross(s.Attitude.InverseRotate(point - s.Position) - com, s.Attitude.InverseRotate(fw));
                touching++;
            }

            // The wet groove's cohesive pull toward the soil (1.13), at its own centroid along the half-skid.
            double pull = 0;
            if (wet)
            {
                pull = Pulls(k, set, ku, z0, z1, z2, out _, out double q0, out double q1, out double q2);
                if (pull > 0)
                {
                    double pullArc = (k.W1 * q1 * k.Arc + k.W2 * q2 * k.Length) / pull;
                    Double3 at = c0 + (c2 - c0) * (pullArc / k.Length) - n * r, fp = n * -pull;
                    f = f + fp;
                    m = m + Double3.Cross(s.Attitude.InverseRotate(at - s.Position) - com, s.Attitude.InverseRotate(fp));
                }
            }
            if (!series)
            {
                ref Pair credit = ref obj == WorldIds.Ground ? ref _ground[k.End] : ref soil;
                credit.Impulse += fn * _dt;
                if (fn > credit.PeakForce) credit.PeakForce = fn;
                if (fn > strongestForce)
                {
                    strongestForce = fn;
                    strongest = obj == WorldIds.Ground ? k.End : soilShape;
                    strongestPair = obj == WorldIds.Ground ? -1 : soilPair;
                }
            }

            // The other points: what the leg carries beyond the soil (a rigid point) or their own spring's share.
            double carried = 0, oStatic = 0;
            if (series) _otherNormal[c] = 0;
            if (other)
            {
                double fo = held ? (series ? kN * x - b : k0 * x) - (soilStatic - pull) : ko > 0 && deep > x ? ko * DetMath.Min(deep - x, ostroke) : 0;
                oStatic = fo;
                double co = orow.Rigid || held ? c0leg : past ? 0 : Series(c0leg, oc);
                fo -= co * closing / sum;
                if (fo > 0)
                {
                    carried = fo;
                    double kno = orow.Rigid || held || past ? k0 : Series(k0, ko);
                    if (series)
                    {
                        // Each point a leg point of the corner (Judge, Final): its share of the push, its anchor across in series
                        // with the side give, the strut by the leg's deflection x along its normal; its share by force below.
                        _otherNormal[c] = fo;
                        _otherStiffness[c] = kno;
                        _otherDamping[c] = co;
                    }
                    else
                        for (int i = 0; i < _shapes.Length; i++)
                        {
                            ContactRole role = _shapes[i].Role;
                            if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                            for (int j = 0; j < _count[i]; j++)
                            {
                                ref Pair p = ref _pairs[i * PairsPerShape + j];
                                if (p.Touching && !p.Suppressed && p.Surface == WorldIds.NoSurface)
                                    Apply(ref p, i, j, fo, kno, co, sum, RowOf(p), s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                            }
                        }
                }
            }
            if (series)
                SoilTrial(c, ref k, s, legUp, n, point, x, fn, soilStatic, kn, cs, k0, legSide, legSideDamp, along, across, carried > 0 ? oStatic : 0, sum,
                    axis, set, set.InterfaceAdhesion * wetted, side, end, pull, obj == WorldIds.Ground ? k.End : soilShape, obj == WorldIds.Ground ? -1 : soilPair);
            else load[c] = pull > 0 ? fn + carried - pull : fn + carried;
            compression[c] = x;

            // Each node's wetted width: the widest contact since it last let go, cleared past δ_a (1.13).
            if (wet)
            {
                k.Wet0 = Wetted(set, r, z0, k.Max0, k.Wet0);
                k.Wet1 = Wetted(set, r, z1, k.Max1, k.Wet1);
                k.Wet2 = Wetted(set, r, z2, k.Max2, k.Wet2);
            }

            k.Last = new SkidSoil
            {
                Engaged = true, Surface = surface, Object = obj, Shape = shp, Deflection = x, Sink0 = z0, Sink1 = z1, Sink2 = z2,
                Max0 = DetMath.Max(k.Max0, z0), Max1 = DetMath.Max(k.Max1, z1), Max2 = DetMath.Max(k.Max2, z2), Static = soilStatic,
                Force = fn, Stiffness = soilStiffness, Centroid = arc, Footprint = footprint, Wetted = wetted, Friction = ft, Sliding = k.Sliding,
                Residual = (series ? kN * x - b : k0 * x) - (soilStatic - pull)
                    - (held ? (series ? kN * x - b : k0 * x) - (soilStatic - pull) : other && ko > 0 && deep > x ? ko * DetMath.Min(deep - x, ostroke) : 0),
                Pull = pull, Clinging = k.W0 * k.Wet0 + k.W1 * k.Wet1 + k.W2 * k.Wet2,
            };

            // The supports' own ground anchors stand aside while the soil carries the corner.
            _ground[k.End].Anchored = _ground[k.Joint].Anchored = false;

            // The grooves keep their deepest sinks.
            k.X = x;
            if (z0 > k.Max0) k.Max0 = z0;
            if (z1 > k.Max1) k.Max1 = z1;
            if (z2 > k.Max2) k.Max2 = z2;
            return true;
        }

        // A soil corner's points for the legs' law (p7g-legs on the line, model 46): the soil's load centroid as one point
        // (Foot: its normal force the series solve's, its tangential trial before the groove's cone), and the corner's other
        // points (Surface 0) each its share of what the leg carries beyond the soil (_otherNormal). Shares by static force.
        struct SoilPoint
        {
            public bool On;
            public Pair Foot;                         // Force, Share, Drift, Point (the centroid), Stretch (the anchor's), N
            public double Static, Kinetic, Adhesion, Side, End, Along, Across; // the groove's cone; the anchor's stiffness by way
            public double AcrossF;                    // the stiffness along the skid's frame (p7g-legs, model 54; Across's off it)
            public Double3 Axis, Pull;                // the half-skid's axis on the soil; the wet groove's pull (world, N)
            public int Shape, Pair;                   // the soil's credit pair (its shape, or the ground's at the end support)
        }

        readonly SoilPoint[] _soilPoint = new SoilPoint[4];
        readonly double[] _otherNormal = new double[4], _otherStiffness = new double[4], _otherDamping = new double[4];

        // The soil corner's trial (design 3 on the soil): the leg's push d = n·x − (the anchor's stretch), along the tube k·a
        // less the law's rest, across it the side give in series with the groove's anchor, both damped as p7g's points; its
        // normal force the solve's (the soil's with its damping), its tangential part the trial's. The other points: their
        // shares of the leg's force beyond the soil and their own anchors' stretch the same way.
        void SoilTrial(int c, ref Skid k, in FlightState s, Double3 up, Double3 n, Double3 point, double x, double fn, double soilStatic, double kn,
            double cn, double k0, double legSide, double legSideDamp, double along, Double3 across, double others, double otherSum, Double3 axis,
            in SoilSet set, double adhesion, double side, double end, double pull, int creditShape, int creditPair)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            double ratio = _spec.FrictionStiffnessRatio;
            double kq = legSide > 0 && kn > 0 ? Series(legSide, ratio * kn) : 0;
            bool lever = _spec.LegLeverLength > 0 && legSide > 0;
            double secant = legSide, cap = SwayCap(c);
            double cq = legSideDamp > 0 && cn > 0 ? Series(legSideDamp, DetMath.Sqrt(ratio) * cn) : 0;
            // The skid's frame along the skid (p7g-legs, model 54): its give and damper in series with the groove's anchor.
            bool frame = _frame[c];
            Double3 fa = _frameAxis[c];
            double legFrame = frame ? _frameGive[c] : 0, kqf = frame && legFrame > 0 && kn > 0 ? Series(legFrame, ratio * kn) : 0;
            double cqf = frame && _frameDamp[c] > 0 && cn > 0 ? Series(_frameDamp[c], DetMath.Sqrt(ratio) * cn) : 0;
            double total = (soilStatic > 0 ? soilStatic : 0) + (others > 0 ? others : 0);
            double share = total > 0 ? (soilStatic > 0 ? soilStatic : 0) / total : 1;
            _axial[c] = kn;
            _side[c] = kq;
            _tubeSide[c] = legSide;
            _legTouching[c] = true;
            sp.On = true;
            sp.Static = set.StaticFriction;
            sp.Kinetic = set.KineticFriction;
            sp.Adhesion = adhesion;
            sp.Side = side;
            sp.End = end;
            sp.Axis = axis;
            sp.Along = k0;
            sp.Across = kq;
            sp.AcrossF = kqf;
            if (frame)
            {
                _sideF[c] = kqf;
                _tubeSideF[c] = legFrame;
            }
            sp.Pull = n * -pull;
            sp.Shape = creditShape;
            sp.Pair = creditPair;
            ref Pair q = ref sp.Foot;
            q.N = n;
            q.Point = point;
            q.Share = share;
            q.Overlap = 1;
            if (!(fn > 0))
            {
                k.Anchored = false;
                q.Force = default;
                q.Stretch = default;
                q.Drift = default;
            }
            else
            {
                if (!k.Anchored)
                {
                    k.Anchor = point;
                    k.AnchorBody = s.Attitude.InverseRotate(point - s.Position);
                    k.Anchored = true;
                    k.Sliding = false;
                }
                Double3 held = World(s, k.AnchorBody), dt = held - k.Anchor;
                dt = dt - n * Double3.Dot(n, dt);
                if (Reached(c, ref dt)) k.Anchor = held - dt;
                Double3 v = PointVelocity(s, held);
                Double3 d = n * x - dt;
                double a = Double3.Dot(up, d) - along, va = Double3.Dot(up, v);
                Double3 lat = d - up * Double3.Dot(up, d) - across;
                Double3 trial;
                if (lever)
                {
                    // The lever (1.4) at this step's sink: along the leg its statics, across its secant with the groove's anchor.
                    double delta = lat.Length(), force = Lever(a, delta, k0, -cn * va, legSide, _spec.LegLeverLength, cap, out secant, out _);
                    kq = kn > 0 ? OnAnchor(secant, ratio * kn) : 0;
                    _side[c] = kq;
                    _tubeSide[c] = secant;
                    sp.Across = kq;
                    _lean[c] = delta * share;
                    if (frame)
                    {
                        // The frame's plane on its own lever (model 54), blended along the leg by the lean's share along the skid.
                        double forceF = Lever(a, delta, k0, -cn * va, legFrame, _spec.LegFrameLeverLength, cap, out double secantF, out _);
                        double lf = Double3.Dot(lat, fa), w = delta > 0 ? lf * lf / (delta * delta) : 0;
                        force += (forceF - force) * w;
                        kqf = kn > 0 ? OnAnchor(secantF, ratio * kn) : 0;
                        _sideF[c] = kqf;
                        _tubeSideF[c] = secantF;
                        sp.AcrossF = kqf;
                    }
                    trial = (up * force + Across(lat, kq, kqf, frame, fa) - Across(v - up * va, cq, cqf, frame, fa)) * share;
                }
                else trial = (up * (k0 * a - cn * va) + Across(lat, kq, kqf, frame, fa) - Across(v - up * va, cq, cqf, frame, fa)) * share;
                q.Force = n * fn + (trial - n * Double3.Dot(n, trial));
                q.Stretch = dt;
                q.Drift = lat;
            }

            // The other points (Surface 0): their shares of the leg's force beyond the soil, their anchors' stretch; none when
            // the leg carries nothing beyond the soil.
            bool carry = others > 0 && _otherNormal[c] > 0;
            double ko = _otherStiffness[c], co = _otherDamping[c];
            double kqo = legSide > 0 ? Series(legSide, ratio * ko) : 0, cqo = legSideDamp > 0 ? Series(legSideDamp, DetMath.Sqrt(ratio) * co) : 0;
            double kqof = frame && legFrame > 0 ? Series(legFrame, ratio * ko) : 0, cqof = frame && _frameDamp[c] > 0 ? Series(_frameDamp[c], DetMath.Sqrt(ratio) * co) : 0;
            double rest = total > 0 ? others / total : 0;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (!p.Touching || p.Suppressed || p.Surface != WorldIds.NoSurface || !(p.Overlap > 0)) continue;
                    if (!carry)
                    {
                        p.Overlap = 0;
                        continue;
                    }
                    double w = p.Overlap / otherSum;
                    Double3 pn = p.N, v = PointVelocity(s, p.Point), vt = v - pn * Double3.Dot(pn, v);
                    if (!p.Anchored)
                    {
                        p.Stretch = default;
                        p.Anchored = true;
                        p.Sliding = false;
                    }
                    else p.Stretch = p.Stretch + vt * _dt;
                    p.Stretch = p.Stretch - pn * Double3.Dot(pn, p.Stretch);
                    Double3 d = pn * x - p.Stretch;
                    double a = Double3.Dot(up, d) - along, va = Double3.Dot(up, v);
                    Double3 lat = d - up * Double3.Dot(up, d) - across;
                    Double3 trial;
                    if (lever)
                    {
                        double delta = lat.Length(), force = Lever(a, delta, ko, -co * va, legSide, _spec.LegLeverLength, cap, out double so, out _);
                        double klo = ko > 0 ? OnAnchor(so, ratio * ko) : 0, klof = 0;
                        if (frame)
                        {
                            double forceF = Lever(a, delta, ko, -co * va, legFrame, _spec.LegFrameLeverLength, cap, out double sof, out _);
                            double lf = Double3.Dot(lat, fa), wf = delta > 0 ? lf * lf / (delta * delta) : 0;
                            force += (forceF - force) * wf;
                            klof = ko > 0 ? OnAnchor(sof, ratio * ko) : 0;
                        }
                        trial = (up * force + Across(lat, klo, klof, frame, fa) - Across(v - up * va, cqo, cqof, frame, fa)) * (rest * w);
                        _lean[c] += delta * rest * w;
                    }
                    else trial = (up * (ko * a - co * va) + Across(lat, kqo, kqof, frame, fa) - Across(v - up * va, cqo, cqof, frame, fa)) * (rest * w);
                    p.Force = pn * (_otherNormal[c] * w) + (trial - pn * Double3.Dot(pn, trial));
                    p.Share = rest * w;
                    p.Drift = lat;
                }
            }
        }

        // The soil corner's point through the groove's cone at the foot (SoilFriction's limit, μ·fn + c_a·S_m + P(θ)), into the
        // step's force and moment: past it the anchor slides along the force's way by the excess over the anchor's stiffness
        // that way. Its force along the leg.
        double SoilFinal(int c, Double3 up, in FlightState s, Double3 com, ref Double3 f, ref Double3 m, ref int touching, ref int strongest,
            ref int strongestPair, ref double strongestForce)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            ref Skid k = ref _skids[c];
            ref Pair q = ref sp.Foot;
            Double3 n = q.N, force = q.Force;
            double fn = Double3.Dot(n, force);
            Double3 ft = default;
            if (!(fn > 0))
            {
                k.Anchored = false;
                fn = 0;
                force = default;
            }
            else
            {
                ft = force - n * fn;
                double size = ft.Length();
                if (size > 0)
                {
                    double along = DetMath.Abs(Double3.Dot(ft, sp.Axis)) / size;
                    if (along > 1) along = 1;
                    double plough = sp.Side * DetMath.Sqrt(1 - along * along) + sp.End * along;
                    double limit = (k.Sliding ? sp.Kinetic : sp.Static) * fn + sp.Adhesion + plough;
                    if (size > limit)
                    {
                        double kinetic = sp.Kinetic * fn + sp.Adhesion + plough;
                        Double3 t = ft * (1 / size);
                        double ut = Double3.Dot(up, t), ktt = (sp.Along * ut * ut + sp.Across * (1 - ut * ut)) * q.Share;
                        if (_frame[c])
                        {
                            double tf = Double3.Dot(t, _frameAxis[c]);
                            ktt += (sp.AcrossF - sp.Across) * tf * tf * q.Share; // along the skid the frame's give (model 54)
                        }
                        ft = t * kinetic;
                        force = n * fn + ft;
                        if (Holds(ktt, sp.Along * q.Share))
                        {
                            Double3 st = q.Stretch + t * ((size - kinetic) / ktt);
                            q.Stretch = st - n * Double3.Dot(n, st);
                            Reached(c, ref q.Stretch);
                            k.Anchor = World(s, k.AnchorBody) - q.Stretch;
                        }
                        k.Sliding = true;
                    }
                    else k.Sliding = false;
                }
                else k.Sliding = false;
            }
            // At the foot, where the ground holds it: the centroid less the anchor's stretch (Final's rule).
            f = f + force;
            m = m + Double3.Cross(s.Attitude.InverseRotate(q.Point - q.Stretch - s.Position) - com, s.Attitude.InverseRotate(force));
            touching++;
            ref Pair credit = ref sp.Pair < 0 ? ref _ground[sp.Shape] : ref _pairs[sp.Shape * PairsPerShape + sp.Pair];
            credit.Impulse += fn * _dt;
            if (fn > credit.PeakForce) credit.PeakForce = fn;
            if (fn > strongestForce)
            {
                strongestForce = fn;
                strongest = sp.Shape;
                strongestPair = sp.Pair;
            }
            k.Last.Friction = ft;
            k.Last.Sliding = k.Sliding;
            return Double3.Dot(up, force) + Double3.Dot(up, sp.Pull);
        }

        /// <summary>The leg's series solve's Newton steps a step (design 2, assumed; the tests hold the rest's residual under 1e-9 N).</summary>
        public const int NewtonSteps = 4;

        // A half-skid's soil force (N) and stiffness (N/m) at its nodes' sinks: the three-point rule over Soil's section law.
        static double Tube(in Skid k, in SoilSet set, double kl, double firm, double z0, double z1, double z2, out double stiffness)
        {
            double f0 = Soil.Force(SoilPart.Tube, k.Radius, set, kl, firm, z0, k.Max0, out double t0);
            double f1 = Soil.Force(SoilPart.Tube, k.Radius, set, kl, firm, z1, k.Max1, out double t1);
            double f2 = Soil.Force(SoilPart.Tube, k.Radius, set, kl, firm, z2, k.Max2, out double t2);
            stiffness = k.W0 * t0 + k.W1 * t1 + k.W2 * t2;
            return k.W0 * f0 + k.W1 * f1 + k.W2 * f2;
        }

        // A half-skid's wet groove pulling it toward the soil (N, 1.13) at its nodes' sinks, by the same three-point rule:
        // each node's cohesive law (Soil.Cohesion) over its wetted width at its separation from its groove's set; its rate
        // d/dz of the soil's force (N/m), and each node's pull per metre.
        static double Pulls(in Skid k, in SoilSet set, double ku, double z0, double z1, double z2, out double stiffness, out double q0, out double q1, out double q2)
        {
            q0 = Soil.Cohesion(set, ku, k.Wet0, Soil.Set(set, k.Max0) - z0, out double s0);
            q1 = Soil.Cohesion(set, ku, k.Wet1, Soil.Set(set, k.Max1) - z1, out double s1);
            q2 = Soil.Cohesion(set, ku, k.Wet2, Soil.Set(set, k.Max2) - z2, out double s2);
            stiffness = k.W0 * s0 + k.W1 * s1 + k.W2 * s2;
            return k.W0 * q0 + k.W1 * q1 + k.W2 * q2;
        }

        // A node's wetted width after a step at sink z (1.13): the widest contact since it last let go, the chord at Wong's
        // effective sink while the node presses its groove; nothing once it has separated from the set by δ_a.
        static double Wetted(in SoilSet set, double r, double z, double max, double width)
        {
            double effective = z >= max ? z : max - set.UnloadRatio * (max - z);
            if (effective > 0)
            {
                Soil.Displaced(SoilPart.Tube, r, effective, out _, out double chord);
                return chord > width ? chord : width;
            }
            return Soil.Set(set, max) - z >= set.AdhesionRelease ? 0 : width;
        }

        // A node's overlap of the ground (δ, m, along its normal), from the plane's sample where it has one, else the soil
        // shape's plane, lifted by a layer carrying the part onto it (1.12, Carry); false where neither is there.
        static bool Node(in Pair ground, Double3 core, double r, in Pair soil, bool hasSoil, double lift, out double depth, out Double3 n, out byte surface,
            out int obj, out int shape)
        {
            if (ground.Surface != WorldIds.NoSurface)
            {
                depth = ground.Depth;
                n = ground.N;
                surface = ground.Surface;
                obj = WorldIds.Ground;
                shape = 0;
                return true;
            }
            if (hasSoil)
            {
                depth = r - Double3.Dot(soil.N, core - soil.P);
                if (lift > 0) depth += lift;
                n = soil.N;
                surface = soil.Surface;
                obj = soil.Object;
                shape = soil.Shape;
                return true;
            }
            depth = -1;
            n = default;
            surface = WorldIds.NoSurface;
            obj = shape = 0;
            return false;
        }

        // A half-skid enters a soil: its grooves start from the newest print on that soil that lies under each node, within
        // half the print's width across and 10° of its axis, else virgin.
        void Land(ref Skid k, int obj, int shape, byte surface, Double3 c0, Double3 c2, Double3 n)
        {
            k.Engaged = true;
            k.Object = obj;
            k.Shape = shape;
            k.Surface = surface;
            k.X = 0;
            k.Anchored = k.Sliding = false;
            k.Max0 = k.Max1 = k.Max2 = 0;
            k.Wet0 = k.Wet1 = k.Wet2 = 0;
            Double3 c1 = c0 + (c2 - c0) * (k.Arc / k.Length);
            for (int back = 1; back <= PrintRing; back++)
            {
                ref Print p = ref _prints[(_printNext - back + PrintRing) % PrintRing];
                if (!p.Used || p.Object != obj || p.Shape != shape || p.Surface != surface) continue;
                Double3 a = p.P2 - p.P0, b = c2 - c0;
                a = a - n * Double3.Dot(n, a);
                b = b - n * Double3.Dot(n, b);
                double la = a.Length(), lb = b.Length();
                if (!(la > 0 && lb > 0) || DetMath.Abs(Double3.Dot(a, b)) < PrintAngleCos * la * lb) continue;
                if (k.Max0 == 0) k.Max0 = Under(p, c0, a, la, n, k.Radius);
                if (k.Max1 == 0) k.Max1 = Under(p, c1, a, la, n, k.Radius);
                if (k.Max2 == 0) k.Max2 = Under(p, c2, a, la, n, k.Radius);
            }
        }

        // The print's deepest sink under the point x: linear between its nodes along its axis a (length la in the tangent
        // plane), 0 off its length or farther across than half its groove's width there.
        static double Under(in Print p, Double3 x, Double3 a, double la, Double3 n, double r)
        {
            Double3 d = x - p.P0;
            d = d - n * Double3.Dot(n, d);
            double t = Double3.Dot(d, a) / (la * la);
            if (!(t >= 0 && t <= 1)) return 0;
            double max = t <= p.Arc ? p.Max0 + (p.Max1 - p.Max0) * (t / p.Arc) : p.Max1 + (p.Max2 - p.Max1) * ((t - p.Arc) / (1 - p.Arc));
            Double3 across = d - a * t;
            Soil.Displaced(SoilPart.Tube, r, max, out _, out double width);
            return Double3.Dot(across, across) <= 0.25 * width * width ? max : 0;
        }

        // A half-skid leaves its soil: its grooves go into the ring, the oldest out.
        void Leave(ref Skid k, Double3 c0, Double3 c2, Double3 n)
        {
            if (k.Max0 > 0 || k.Max1 > 0 || k.Max2 > 0)
            {
                _prints[_printNext] = new Print
                {
                    Used = true, Object = k.Object, Shape = k.Shape, Surface = k.Surface, P0 = c0, P2 = c2, Arc = k.Arc / k.Length,
                    Max0 = k.Max0, Max1 = k.Max1, Max2 = k.Max2,
                };
                _printNext = (_printNext + 1) % PrintRing;
            }
            k.Engaged = false;
            k.X = 0;
            k.Anchored = k.Sliding = false;
            k.Max0 = k.Max1 = k.Max2 = 0;
            k.Wet0 = k.Wet1 = k.Wet2 = 0;
            k.Last = default;
        }

        /// <summary>Corner c's half-skid on soil as the last Forces left it (default when it is not in soil, or without
        /// surfaces.json).</summary>
        public SkidSoil SoilOf(int corner) => _skids != null && _skids[corner].Engaged ? _skids[corner].Last : default;

        /// <summary>Corner c's footprint clinging to a wet layer (wet straw, 1.13) since it last let go, m²; 0 when none.</summary>
        public double ClingOf(int corner) => _cling != null ? _cling[corner] : 0;

        /// <summary>A cover kind's set at the flight's moisture (1.13: wet in Rain and Severe rain, so wet stems and straws
        /// hold harder before they break and cost more to cut), which the drawn elements read (1.14, 1.15); default
        /// without surfaces.json.</summary>
        public CoverSet Cover(CoverKind kind) => _cover != null ? _cover[(int)kind] : default;

        /// <summary>The ring's grooves in use.</summary>
        public int Prints
        {
            get
            {
                int n = 0;
                for (int i = 0; _prints != null && i < PrintRing; i++)
                    if (_prints[i].Used) n++;
                return n;
            }
        }

        /// <summary>
        /// The half-skid's stick anchor (design 3, K§7.1 step 5): a material point of the skid held to where it stuck by a
        /// tangential spring kt and damper ct, holding to μs·fn + c_a·S_m + P(θ) and sliding at μk·fn + c_a·S_m + P(θ)
        /// (Coulomb with interface adhesion, τ = c_a + σ·tan δ), P(θ) = side·|sin θ| + end·|cos θ| the groove's passive
        /// resistance toward the stretch, θ from the half-skid's axis; the anchor dragged along so the spring carries the
        /// sliding force.
        /// </summary>
        static Double3 SoilFriction(ref Skid k, in FlightState s, Double3 point, Double3 n, double fn, double kt, double ct, Double3 axis,
            in SoilSet set, double adhesion, double side, double end)
        {
            if (!(fn > 0) || !(kt > 0))
            {
                k.Anchored = false;
                return default;
            }
            if (!k.Anchored)
            {
                k.Anchor = point;
                k.AnchorBody = s.Attitude.InverseRotate(point - s.Position);
                k.Anchored = true;
                k.Sliding = false;
            }
            Double3 held = World(s, k.AnchorBody), d = held - k.Anchor;
            d = d - n * Double3.Dot(n, d);
            Double3 v = PointVelocity(s, held), vt = v - n * Double3.Dot(n, v);
            Double3 ft = d * -kt - vt * ct;
            double size = ft.Length();
            if (!(size > 0))
            {
                k.Sliding = false;
                return ft;
            }
            double along = DetMath.Abs(Double3.Dot(ft, axis)) / size;
            if (along > 1) along = 1;
            double plough = side * DetMath.Sqrt(1 - along * along) + end * along;
            double limit = (k.Sliding ? set.KineticFriction : set.StaticFriction) * fn + adhesion + plough;
            if (size > limit)
            {
                ft = ft * ((set.KineticFriction * fn + adhesion + plough) / size);
                k.Anchor = held + ft * (1 / kt);
                k.Sliding = true;
            }
            else
            {
                k.Sliding = false;
            }
            return ft;
        }

        /// <summary>The stick anchor (K§7.1 step 5): a tangential spring kt and damper ct, holding to μs·fn and sliding at
        /// μk·fn, the anchor dragged along so the spring carries the sliding force. The spring's stretch is the slip of the
        /// body's material at the contact, its point velocity v on the tangent plane times the step (the velocity that moved
        /// it there, semi-implicit), never the contact point's own move: a part rolling on a curved face (the coil on its
        /// side, a skid's tube) moves its contact point with no slip at all, and reading that move as slip held the coil's
        /// contact where it first touched, an axle on its axis (1.6c, D-067).</summary>
        static Double3 Friction(ref Pair p, double fn, double kt, double ct, Double3 v, in Row row, double dt)
        {
            if (!(fn > 0) || !(kt > 0))
            {
                p.Anchored = false;
                return default;
            }
            Double3 n = p.N;
            Double3 vt = v - n * Double3.Dot(n, v);
            if (!p.Anchored)
            {
                p.Stretch = default;
                p.Anchored = true;
                p.Sliding = false;
            }
            else
            {
                p.Stretch = p.Stretch + vt * dt; // the touching material's slip over the step that brought it here
            }
            Double3 d = p.Stretch - n * Double3.Dot(n, p.Stretch);
            p.Stretch = d;
            Double3 ft = d * -kt - vt * ct;
            double size = ft.Length(), limit = (p.Sliding ? row.KineticFriction : row.StaticFriction) * fn;
            if (size > limit)
            {
                ft = ft * (row.KineticFriction * fn / size);
                p.Stretch = ft * (-1 / kt);
                p.Sliding = true;
            }
            else
            {
                p.Sliding = false;
            }
            return ft;
        }

        // A surface sliding on the face at v (world; a turning bell's, p7f-damage 1.5): kinetic friction μk·fn against its slip on
        // the tangent plane, no stick anchor (it slips while it moves).
        static Double3 Slip(ref Pair p, double fn, Double3 v, in Row row)
        {
            p.Anchored = false;
            p.Sliding = true;
            p.Stretch = default;
            Double3 n = p.N, vt = v - n * Double3.Dot(n, v);
            double speed = vt.Length();
            return fn > 0 && speed > 0 ? vt * (-row.KineticFriction * fn / speed) : default;
        }

        // The ground is the plane and every soil shape (a patch's soil, a hole's fillers): its surface, never its object
        // (p6-flight-core 1.6b's dispatch); with surfaces.json, that surface's own friction.
        Row RowOf(in Pair p)
        {
            if (p.Surface == WorldIds.NoSurface && p.Material < _byMaterial.Length && _byMaterial[p.Material].Known) return _byMaterial[p.Material];
            return _surfaceRow != null && _surfaceRow[p.Surface].Known ? _surfaceRow[p.Surface] : _groundRow;
        }

        static double Series(double a, double b) => a * b / (a + b);

        static Double3 PointVelocity(in FlightState s, Double3 point) =>
            s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, s.Attitude.InverseRotate(point - s.Position)));

        // ---- Sense: after the move ----------------------------------------------------------------------------------------

        /// <summary>
        /// The world around the new pose: the supports' ground, every shape swept from was to now, the latches, and the
        /// step's ContactMade, ContactBroken, PropStrike, Tunnel, Crash (by a strike) and ContactsLost events. Call it once
        /// at the flight's start with was = now (the drone at rest on the stand or the floor), then after every move.
        /// </summary>
        public void Sense(in FlightState was, ref FlightState now, StepEvents events)
        {
            if (_rotors != null) PlaceBlades(was, now, events);

            // The supports' ground, in one batch.
            for (int k = 0; k < _supports.Length; k++)
            {
                Double3 x = World(now, _shapes[_supports[k]].A);
                _at[k] = new XZ(x.X, x.Z);
            }
            for (int c = 0; _skids != null && c < 4; c++)
            {
                Double3 x = World(now, _skids[c].Middle);
                _at[_supports.Length + c] = new XZ(x.X, x.Z);
            }
            if (_at.Length > 0) _world.SampleGround(_at, _samples);
            for (int c = 0; _skids != null && c < 4; c++)
            {
                if (!_skids[c].Known) continue;
                GroundSample gs = _samples[_supports.Length + c];
                Double3 x = World(now, _skids[c].Middle);
                ref Pair g = ref _skids[c].Ground;
                bool hole = (gs.Flags & GroundFlags.Hole) != 0;
                g.Object = WorldIds.Ground;
                g.Surface = gs.Surface;
                g.N = hole ? new Double3(0, 1, 0) : gs.Normal;
                g.Depth = hole ? -1 : Sunk(gs, x.Y, _skids[c].Radius); // as a support's (K§7.1 step 2)
            }
            for (int k = 0; k < _supports.Length; k++)
            {
                int i = _supports[k];
                if (_off[i]) continue;
                ref Pair g = ref _ground[i];
                GroundSample gs = _samples[k];
                Double3 x = World(now, _shapes[i].A);
                bool hole = (gs.Flags & GroundFlags.Hole) != 0;
                g.Object = WorldIds.Ground;
                g.Shape = 0;
                g.Material = WorldIds.NoMaterial;
                g.Surface = gs.Surface;
                g.N = hole ? new Double3(0, 1, 0) : gs.Normal;
                g.Depth = hole ? -1 : Sunk(gs, x.Y, _shapes[i].Radius); // K§7.1 step 2
                g.Point = new Double3(x.X, hole ? x.Y : gs.Height, x.Z);
                g.P = g.Point;
                if (!_terrain) Transition(ref g, i, now, events, false); // on a terrain its own sweep finishes it (TerrainGround)
            }

            for (int o = 0; o < _order.Length; o++)
            {
                int i = _order[o];
                if (_off[i]) continue;
                ContactShape shape = _shapes[i];
                if (shape.Role == ContactRole.Disc)
                {
                    Disc(i, was, ref now, events);
                    continue;
                }
                // The sweep from the shape where the last Sense left it (a leg's set may have moved it since, p7g-legs 1.3).
                var before = new Capsule(World(was, _wasA[i]), World(was, _wasB[i]), shape.Radius);
                var after = new Capsule(World(now, shape.A), World(now, shape.B), shape.Radius);
                _wasA[i] = shape.A;
                _wasB[i] = shape.B;
                // A skid's ground is its nodes' (the samples and the supports'): on a terrain its sweep leaves the ground out, which
                // Update would only drop; a support takes its ground from its own sweep (TerrainGround).
                int found = _world.StaticContacts(before, after, _spec.Margin, _buf,
                    _terrain && shape.Role == ContactRole.Skid ? QueryFilter.NoGround : QueryFilter.None);
                if (found > Buffer) Lost(events, i, found - Buffer);
                if (_terrain && shape.Role == ContactRole.Support) TerrainGround(i, found < Buffer ? found : Buffer, after, now, events);
                Update(i, found < Buffer ? found : Buffer, after, now, events);
            }
        }

        // A support's overlap of the sample's facet along its normal: r − n·(c − P), P the facet's point under the core, so
        // r − n_y·(y − h). K§7.1 step 2's (h − (y − r))·n_y leaves out r·(1 − n_y): 1.06 mm on a 5 mm support at 38°. On a level
        // plane n_y is 1, and the two are the same bits.
        static double Sunk(in GroundSample gs, double y, double r) => (gs.Height - (y - r)) * gs.Normal.Y + r * (1 - gs.Normal.Y);

        // A support's ground on a terrain (w3-farmland-ground 2.2): its own sweep's contact with the ground, the piece nearest
        // it (a facet, a step's face or the lip between them; on a facet's inside the sample's facet), its plane measured at the
        // core now as a latch's (Depth). SampleGround reports a step's top, so a support past a face below it would read the
        // step's height as its overlap (1.4–1.6 m at the farmland's ledge). Where the sweep finds no ground (farther than the
        // margin) the sample stands, never touching: a core inside the ground always has its contact.
        void TerrainGround(int i, int found, in Capsule part, in FlightState now, StepEvents events)
        {
            ref Pair g = ref _ground[i];
            for (int j = 0; j < found; j++)
            {
                if (_buf[j].Object != WorldIds.Ground) continue;
                g.Surface = _buf[j].Surface;
                g.N = _buf[j].Normal;
                g.P = _buf[j].Point;
                Depth(ref g, part);
                break;
            }
            Transition(ref g, i, now, events, false);
        }

        // One shape's pairs against this step's contacts (the latch, P1), then each pair's depth and its transitions.
        void Update(int i, int found, in Capsule part, in FlightState now, StepEvents events)
        {
            ContactShape shape = _shapes[i];
            int first = i * PairsPerShape;
            bool groundless = shape.Role == ContactRole.Support || shape.Role == ContactRole.Skid;
            bool blade = shape.Role == ContactRole.Blade;
            bool capsule = !Same(shape.A, shape.B);
            // The side a part comes into a rigid shape from (BodySide): the drone's, or a blade's own rotor's hub.
            Double3 side = blade ? World(now, _hub[shape.Corner]) : now.Position;
            for (int k = 0; k < _count[i]; k++)
            {
                _pairs[first + k].Seen = false;
                _pairs[first + k].Suppressed = false;
            }
            int lost = 0;
            for (int j = 0; j < found; j++)
            {
                StaticContact c = _buf[j];
                if (groundless && c.Object == WorldIds.Ground) continue; // the supports' ground is SampleGround's
                // A soil carved by the blade is the strikes' (p7f-damage 1.3b); and without surfaces.json every surface, the ground's and
                // the soil shapes', whose passes glance through it (no face to them): a blade at rest bears on it no more than a turning
                // one, so a rotor stopped with its disc in the floor never comes back to rest inside a rigid one (w3-farmland-ground 2.7).
                if (blade && (YieldsToBlade(c.Surface) || _soil == null && c.Surface != WorldIds.NoSurface)) continue;
                int k = 0;
                while (k < _count[i] && (_pairs[first + k].Object != c.Object || _pairs[first + k].Shape != c.Shape)) k++;
                bool same = Buried(j, found) || (capsule ? SameSupport(i, c) : Seam(j, found, groundless));
                if (k == _count[i])
                {
                    if (same) continue;
                    Double3 at = c.Point, normal = c.Normal;
                    bool slot = blade && Slot(c, side, shape.Radius, ref at, ref normal);
                    if (slot && c.Distance >= -shape.Radius) continue; // past the box's far face: no touch of the blade in its slot
                    if (k == PairsPerShape)
                    {
                        lost++;
                        continue;
                    }
                    _count[i]++;
                    if (!blade && !(c.Time > 0 && c.Time < 1) && c.Distance < -shape.Radius) BodySide(c, side, shape.Radius, ref at, ref normal);
                    _pairs[first + k] = new Pair
                    {
                        Object = c.Object, Shape = c.Shape, Material = c.Material, Surface = c.Surface, P = at, N = normal, Entry = normal,
                        Latched = c.Time < 1 || !Same(normal, c.Normal), Seen = true, Time = c.Time,
                        Slack = slot ? double.PositiveInfinity : 0, // a blade in its slot: no motion pressed its spring (PlaceBlades)
                    };
                    continue;
                }
                ref Pair p = ref _pairs[first + k];
                p.Seen = true;
                if (same)
                {
                    p.Suppressed = true; // the support beside it, or the piece around a buried face, carries this touch
                    continue;
                }
                p.Time = c.Time;
                if (c.Time < 1)
                {
                    if (!p.Latched)
                    {
                        // A first touch starts a contact; a Time 0 (through during the step) continues one.
                        if (!p.Touching) p.Entry = c.Normal;
                        p.P = c.Point;
                        p.N = c.Normal;
                        p.Latched = true;
                        p.Flipped = false;
                    }
                }
                else if (c.Distance < -shape.Radius)
                {
                    p.Latched = true; // the core is inside the shape: the plane stays where it was (P1)
                }
                else if (!p.Latched || Double3.Dot(c.Normal, p.N) > 0)
                {
                    if (!p.Touching) p.Entry = c.Normal;
                    p.P = c.Point; // refreshed: an ordinary contact
                    p.N = c.Normal;
                    p.Latched = false;
                    p.Flipped = false;
                }
                else
                {
                    p.Flipped = true;
                }
            }
            if (lost > 0) Lost(events, i, lost);

            int kept = 0;
            for (int k = 0; k < _count[i]; k++)
            {
                ref Pair p = ref _pairs[first + k];
                Depth(ref p, part);
                CrownRow crown = RowOf(p).Crown;
                if (crown != null) Yield(ref p, crown.TwigReach);
                // A blade's slack: its overlap where it came back after its rotor's passes, then only less as the blade leaves
                // (never a spring pressed by no motion, PlaceBlades).
                if (blade) p.Slack = _appeared[i] ? DetMath.Max(p.Depth, 0) : DetMath.Min(p.Slack, DetMath.Max(p.Depth, 0));
                if (!p.Seen || p.Suppressed)
                {
                    // Only a rigid shape or the ground can be passed by mistake: a compliant one (a crown's twig shell, a
                    // straw mat) yields to its own law, and leaving it is an ordinary break.
                    if (!p.Seen && p.Latched && p.Depth > 0 && RowOf(p).Rigid && (p.Flipped || PastCentre(p, part))) Tunnel(i, p, part, now, events);
                    if (p.Touching) Broken(ref p, i, now.Step, events);
                    continue;
                }
                if (p.Latched && -p.Depth > _spec.Margin && crown == null)
                {
                    if (p.Touching) Broken(ref p, i, now.Step, events);
                    continue;
                }
                Transition(ref p, i, now, events, true);
                if (kept != k) _pairs[first + kept] = p;
                kept++;
            }
            _count[i] = kept;
            // One crown a part (2.3): its overlapping shells stand for one crown's twigs, so its deepest touching crown pair carries
            // them and the others are held for the step (Twigs takes the deepest shell's meeting for their union, as here).
            int deepest = -1;
            for (int k = 0; k < kept; k++)
            {
                ref Pair p = ref _pairs[first + k];
                if (p.Touching && RowOf(p).Crown != null && (deepest < 0 || p.Depth > _pairs[first + deepest].Depth)) deepest = k;
            }
            for (int k = 0; deepest >= 0 && k < kept; k++)
                if (k != deepest && _pairs[first + k].Touching && RowOf(_pairs[first + k]).Crown != null) _pairs[first + k].Suppressed = true;
        }

        // A part found inside a rigid box with no first touch this step (a blade come back to rest, a piece a leg's set or break
        // moved, a pair the step before did not keep) has no entry to latch: the query's nearest face, which on a box thinner than
        // the part's depth is the far one once its core is past the middle, and that face pushes the part on through (a 2 cm wall
        // skin, w3-farmland-ground 2.7). The drone is never inside a rigid shape, so a part's way in is from the body's side: the face
        // the line from its origin to the part's core enters the box by, as a slab's would be (a blade's is its hub's, Slot). Compliant rows,
        // soil and the ground keep the query's face (they yield, or are one side); a round shape's far side is its tunnel rule's
        // (Entry, PastCentre).
        void BodySide(in StaticContact c, Double3 side, double radius, ref Double3 point, ref Double3 normal)
        {
            if (!RigidBox(c, out ShapeGeometry g) || !Enters(g, side, Core(c, radius), out Double3 n)) return;
            point = OnFace(g, n, point);
            normal = n;
        }

        // x moved along a box's face normal n onto that face's plane.
        static Double3 OnFace(in ShapeGeometry g, Double3 n, Double3 x)
        {
            double s = DetMath.Abs(Double3.Dot(n, g.AxisX)) * g.HalfExtents.X + DetMath.Abs(Double3.Dot(n, g.AxisY)) * g.HalfExtents.Y
                + DetMath.Abs(Double3.Dot(n, g.AxisZ)) * g.HalfExtents.Z;
            return x - n * (Double3.Dot(n, x - g.Center) - s);
        }

        // A resting blade on a rigid box its hub's line to the blade's core enters by another face than the contact's: the blade's
        // own root passes through the box, so the blade came there by its rotor's passes (the strikes' carve, which leaves the world
        // whole) and lies in the slot it cut (w3-farmland-ground 2.7). Its face is the one the root's line enters by (as BodySide's);
        // past the far face it touches nothing. A blade pressed in by the body's motion, or resting on a box beside its root, meets
        // the contact's own face.
        bool Slot(in StaticContact c, Double3 hub, double radius, ref Double3 point, ref Double3 normal)
        {
            if (!RigidBox(c, out ShapeGeometry g) || !Enters(g, hub, Core(c, radius), out Double3 n) || Double3.Dot(n, c.Normal) > 0.5) return false;
            point = OnFace(g, n, point);
            normal = n;
            return true;
        }

        // The part's core point nearest the contact's.
        static Double3 Core(in StaticContact c, double radius) => c.Point + c.Normal * (c.Distance + radius);

        // A contact on a rigid placed or runtime box: not the ground, soil or a compliant row.
        bool RigidBox(in StaticContact c, out ShapeGeometry g)
        {
            g = default;
            if (c.Object == WorldIds.Ground || c.Surface != WorldIds.NoSurface) return false;
            if (c.Material < _byMaterial.Length && _byMaterial[c.Material].Known && !_byMaterial[c.Material].Rigid) return false;
            return _world.Geometry(c.Object, c.Shape, out g) && g.Kind == ShapeKind.Box;
        }

        // The segment from a (outside box g) to b enters the box: the outward normal of the face it enters by (the slabs' method).
        static bool Enters(in ShapeGeometry g, Double3 a, Double3 b, out Double3 normal)
        {
            normal = default;
            double lo = 0, hi = 1;
            Double3 from = a - g.Center, d = b - a;
            for (int k = 0; k < 3; k++)
            {
                Double3 axis = k == 0 ? g.AxisX : k == 1 ? g.AxisY : g.AxisZ;
                double h = k == 0 ? g.HalfExtents.X : k == 1 ? g.HalfExtents.Y : g.HalfExtents.Z;
                double p = Double3.Dot(from, axis), v = Double3.Dot(d, axis);
                if (v == 0)
                {
                    if (p < -h || p > h) return false;
                    continue;
                }
                double near = ((v > 0 ? -h : h) - p) / v, far = ((v > 0 ? h : -h) - p) / v;
                if (near > lo)
                {
                    lo = near;
                    normal = v > 0 ? -axis : axis;
                }
                if (far < hi) hi = far;
                if (lo > hi) return false;
            }
            return lo > 0;
        }

        // A capsule's contact is the same support as a touching sphere of the same part (P3) when its point lies within two
        // radii of the sphere's, or when the sphere is a support on the same world shape, or on one whose face holds the
        // contact's point (a seam's coplanar piece, Seam), and the capsule is no deeper into it by more than its own radius:
        // a straight skid on a flat face, whose line contact's tie falls at either core end (IWorldQuery: the start of the
        // flat stretch), the far one's point well clear of every sphere. A crossing between the spheres (a rod, a wire, a
        // ridge or a hump the spheres stand off) is deeper, or off that face, and counts. The coplanar face is needed where a
        // skid's middle lies on a flush piece no support stands on (RangeContactTests.OnFlushBoardsEachSupportIsOnePoint:
        // without it, 12 pairs); the range's soil and run have no such piece under the skids.
        bool SameSupport(int capsule, in StaticContact contact)
        {
            ContactShape c = _shapes[capsule];
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactShape s = _shapes[i];
                if (i == capsule || s.Part != c.Part || s.Role == ContactRole.Disc || !Same(s.A, s.B)) continue;
                double reach = c.Radius + s.Radius;
                if (s.Role == ContactRole.Support && _ground[i].Touching && Near(_ground[i].Point, contact.Point, reach)) return true;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (!p.Touching || p.Suppressed) continue;
                    if (Near(p.Point, contact.Point, reach)) return true;
                    if (s.Role == ContactRole.Support && (p.Object == contact.Object && p.Shape == contact.Shape || OnPlane(p.N, p.Point, contact.Point))
                        && -contact.Distance <= p.Depth + c.Radius) return true;
                }
            }
            return false;
        }

        // A sphere meets a flat surface at one point, however many coplanar pieces make it (a filler beside a filler, a box
        // flush with another): its contact whose point lies on the plane of a deeper one on another world shape (or of one
        // as deep within SeamTie, earlier in the results) is that face's seam, and the face carries it. Without it E01's fillers under
        // D-050's legs carry the drone off the plane's way twice over: a support 2 mm past a filler's edge meets that edge,
        // its normal 10° off the vertical (the drone 1.3 µm deeper than on the plane, 1.001 of its weight on the legs), and
        // a support on two fillers is two of its leg's points against one beside it, so the leg's depth split leans on it
        // (pitched, 9 µm higher, the rear legs 5 % over the front). A real edge (a curb, a step, a rod or a wire on the
        // floor) stands off that plane and counts. A leg's ground is SampleGround's, never the plane's contact here. A
        // capsule's line contact is left to the query and the skids' same-face rule (SameSupport). A face on that plane facing
        // the other way (normals more than 120° apart, FacingAway) is not a piece of it but the face of a piece on its other
        // side (a layer's underside on its soil, a tile's side against its neighbour's), so it counts (1.12: a sphere through
        // a straw tile's bottom kept its tile and its soil).
        bool Seam(int j, int found, bool groundless)
        {
            StaticContact c = _buf[j];
            for (int q = 0; q < found; q++)
            {
                StaticContact d = _buf[q];
                double deeper = c.Distance - d.Distance;
                if (q == j || !(deeper > SeamTie || deeper >= -SeamTie && q < j) || groundless && d.Object == WorldIds.Ground) continue;
                if (Double3.Dot(c.Normal, d.Normal) > FacingAway && OnPlane(d.Normal, d.Point, c.Point)) return true;
            }
            return false;
        }

        // A face buried in its own layer (physics-integration-2's side-face latch, derived): contact j on a layer row's shape
        // (one with an area law: straw, fabric, a sandbag's fill) whose point lies inside another of this result's shapes of
        // the same object and the same material, deeper than SeamTolerance. A layer's overlapping pieces are one loose mass,
        // continuous across the faces between them, so such a face carries no normal stress: the layer's surface is the
        // union's, and the piece around the point meets the part by its own face. On S04's mat a skid sunk 9 mm into one tile
        // lies 2–5 mm inside a neighbouring tile's side face; the query reports that side, the part's shortest way out of
        // that tile, and without this rule the leg took it as a point with a horizontal normal: its share of the corner's
        // force pushed the drone sideways (a skid's for a step at a time, as the query's nearest face of that tile switched; a
        // support's for good), and a corner lifting out kept it, the latched plane (P1) held as the top face's normal,
        // perpendicular to it, read as a flip. A real face is the layer's surface, inside no other piece of it, and counts:
        // the mat's own cut edge, a rock of another material in the straw, a wall's face or a box's edge. Only a layer is
        // entered past a part's radius by a valid contact: a rigid shape's inner face is reached only through its surface,
        // which the tunnel rule must see, and a compliant row without an area law gives by bending, each piece its own member.
        // Soil shapes keep design 5's own rule (p7a-ground 1.11).
        bool Buried(int j, int found)
        {
            StaticContact c = _buf[j];
            if (c.Object == WorldIds.Ground || c.Surface != WorldIds.NoSurface || c.Material >= _byMaterial.Length
                || _byMaterial[c.Material].Law == AreaLaw.None) return false;
            for (int q = 0; q < found; q++)
            {
                StaticContact d = _buf[q];
                if (q == j || d.Object != c.Object || d.Shape == c.Shape || d.Material != c.Material || d.Surface != WorldIds.NoSurface) continue;
                if (_world.Geometry(d.Object, d.Shape, out ShapeGeometry g) && Within(g, c.Point) > SeamTolerance) return true;
            }
            return false;
        }

        // How deep x lies inside shape g, m (at most 0 outside): to its nearest face for a box, a cylinder (axis AxisY) and the
        // plane's half-space, to its surface for a sphere and a capsule (axis AxisY); a wire is a chain, no solid: never.
        static double Within(in ShapeGeometry g, Double3 x)
        {
            Double3 d = x - g.Center;
            switch (g.Kind)
            {
                case ShapeKind.Plane:
                    return -Double3.Dot(d, g.AxisY);
                case ShapeKind.Box:
                {
                    Double3 h = g.HalfExtents;
                    double ix = h.X - DetMath.Abs(Double3.Dot(d, g.AxisX)), iy = h.Y - DetMath.Abs(Double3.Dot(d, g.AxisY));
                    return DetMath.Min(DetMath.Min(ix, iy), h.Z - DetMath.Abs(Double3.Dot(d, g.AxisZ)));
                }
                case ShapeKind.Sphere:
                    return g.Radius - d.Length();
                case ShapeKind.Capsule:
                    return g.Radius - (x - Closest(g.Center - g.AxisY * g.HalfLength, g.Center + g.AxisY * g.HalfLength, x)).Length();
                case ShapeKind.Cylinder:
                {
                    double y = Double3.Dot(d, g.AxisY);
                    return DetMath.Min(g.Radius - (d - g.AxisY * y).Length(), g.HalfLength - DetMath.Abs(y));
                }
                default:
                    return double.NegativeInfinity;
            }
        }

        // x lies on the plane through p with normal n, within SeamTolerance.
        static bool OnPlane(Double3 n, Double3 p, Double3 x)
        {
            double off = Double3.Dot(n, x - p);
            return off <= SeamTolerance && off >= -SeamTolerance;
        }

        static bool Near(Double3 a, Double3 b, double reach)
        {
            Double3 d = a - b;
            return Double3.Dot(d, d) <= reach * reach;
        }

        // The pair's depth against its plane at the part's pose, and the contact point under the core.
        static void Depth(ref Pair p, in Capsule part)
        {
            Double3 core = Closest(part.A, part.B, p.P);
            double h = Double3.Dot(p.N, core - p.P);
            p.Depth = part.Radius - h;
            p.Point = core - p.N * h;
        }

        static Double3 Closest(Double3 a, Double3 b, Double3 x)
        {
            Double3 ab = b - a;
            double len = Double3.Dot(ab, ab);
            if (!(len > 0)) return a;
            double t = Double3.Dot(x - a, ab) / len;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return a + ab * t;
        }

        /// <summary>The part's deepest core end lies past the shape's centre along the normal it came in by.</summary>
        bool PastCentre(in Pair p, in Capsule part)
        {
            if (!_world.Geometry(p.Object, p.Shape, out ShapeGeometry g) || g.Kind == ShapeKind.Plane) return false;
            Double3 deep = Double3.Dot(p.Entry, part.A) <= Double3.Dot(p.Entry, part.B) ? part.A : part.B;
            return Double3.Dot(p.Entry, deep - g.Center) < 0;
        }

        // A hanging half's point still pressed (w9-village-physics 2.6): its shapes stand out of what they touch by the half's
        // elastic give, which the massless half holds only while it is pressed, so its pair touches while its overlap without
        // that give is positive. On its turned depth alone a pair the give had just lifted clear read no force for a step, the
        // give fell to 0 and the half's shapes snapped back into the ground by it (the sweep's 91 mm, a one-step limit cycle).
        bool Hung(int i, in Pair p, in FlightState s)
        {
            if (!_spec.HangingGive || _legs == null) return false;
            ContactRole role = _shapes[i].Role;
            int c = _shapes[i].Corner;
            if (role != ContactRole.Support && role != ContactRole.Skid || c < 0 || c >= 4 || (_corner[c] & CornerState.Hanging) == 0) return false;
            return p.Depth + Unturned(c, p, s) > 0;
        }

        // Touching starts or ends: ContactMade (and maybe an impact) or ContactBroken.
        void Transition(ref Pair p, int i, in FlightState now, StepEvents events, bool objectPair)
        {
            bool touching = p.Depth > 0 || Hung(i, p, now);
            if (touching == p.Touching) return;
            if (!touching)
            {
                Broken(ref p, i, now.Step, events);
                return;
            }
            p.Touching = true;
            p.Since = now.Step;
            p.Impulse = 0;
            p.PeakForce = 0;
            p.Anchored = false;
            p.MadeSpeed = -Double3.Dot(PointVelocity(now, p.Point), p.N);
            FlightEvent e = Of(EventKind.ContactMade, i, p);
            e.A = p.MadeSpeed;
            events.Add(e);
            if (p.MadeSpeed >= _spec.ImpactSpeed) StartImpact(e, p.MadeSpeed);
        }

        void Broken(ref Pair p, int i, long step, StepEvents events)
        {
            p.Touching = false;
            p.Anchored = false;
            FlightEvent e = Of(EventKind.ContactBroken, i, p);
            e.A = (step - p.Since) * FlightCore.Dt;
            e.B = p.Impulse;
            e.C = p.PeakForce;
            events.Add(e);
        }

        void Tunnel(int i, in Pair p, in Capsule part, in FlightState now, StepEvents events)
        {
            FlightEvent e = Of(EventKind.Tunnel, i, p);
            e.Point = Double3.Dot(p.Entry, part.A) <= Double3.Dot(p.Entry, part.B) ? part.A : part.B;
            e.A = p.Depth;
            e.B = PointVelocity(now, e.Point).Length();
            events.Add(e);
        }

        void Lost(StepEvents events, int i, int count)
        {
            FlightEvent e = FlightEvent.Of(EventKind.ContactsLost);
            e.Part = (ushort)_shapes[i].Part;
            e.Index = (byte)i;
            e.A = count;
            events.Add(e);
        }

        FlightEvent Of(EventKind kind, int i, in Pair p)
        {
            FlightEvent e = FlightEvent.Of(kind);
            e.Part = (ushort)_shapes[i].Part;
            e.Index = (byte)i;
            e.Object = p.Object;
            e.Shape = p.Shape;
            e.Material = p.Material;
            e.Surface = p.Surface;
            e.Point = p.Point;
            return e;
        }

        void StartImpact(in FlightEvent cause, double speed)
        {
            if (_impactLeft > 0) return;
            _impactLeft = (int)(_spec.ImpactWindow / _dt + 0.5);
            _impactPeak = 0;
            _impactImpulse = default;
            _impacts++;
            _lastPeak = 0;
            _lastImpulse = 0;
            _impact = cause;
            _impact.Kind = EventKind.Impact;
            _impact.C = speed;
        }

        void Crash(ref FlightState s, StepEvents events, int i, in Pair p, CrashCause cause, double speed)
        {
            s.Crashed = true;
            FlightEvent e = Of(EventKind.Crash, i, p);
            e.Index = (byte)cause;
            e.A = speed;
            e.B = _impactLeft > 0 ? _impactImpulse.Length() : 0;
            e.C = _impactLeft > 0 ? _impactPeak : 0;
            events.Add(e);
        }

        // A prop disc: the bounding sphere, then the ring; each shape entering it is a strike.
        void Disc(int i, in FlightState was, ref FlightState now, StepEvents events)
        {
            ContactShape d = _shapes[i];
            int first = i * PairsPerShape;
            for (int k = 0; k < _count[i]; k++) _pairs[first + k].Seen = false;
            double r = d.Radius + d.HalfThickness;
            Double3 hubWas = World(was, d.A), hubNow = World(now, d.A);
            int gate = _world.StaticContacts(new Capsule(hubWas, hubWas, r), new Capsule(hubNow, hubNow, r), 0, _buf);
            int gated = gate < Buffer ? gate : Buffer;
            for (int j = 0; j < gated; j++) _gate[j] = _buf[j];
            int lost = 0;
            for (int chord = 0; gate > 0 && chord < _chordReach.Length; chord++)
            {
                int from = chord * ChordSegments, to = from + ChordSegments < _spec.RingSegments ? from + ChordSegments : _spec.RingSegments;
                if (to - from > 1)
                {
                    Double3 ca = d.A + _ring[from] * d.Radius, cb = d.A + _ring[to] * d.Radius;
                    double reach = _chordReach[chord] * d.Radius + d.HalfThickness + ChordPad;
                    if (_world.StaticContacts(new Capsule(World(was, ca), World(was, cb), reach), new Capsule(World(now, ca), World(now, cb), reach), 0, _buf) == 0)
                        continue;
                }
                for (int s = from; s < to; s++)
                {
                    Double3 a = d.A + _ring[s] * d.Radius, b = d.A + _ring[s + 1] * d.Radius;
                    int found = _world.StaticContacts(new Capsule(World(was, a), World(was, b), d.HalfThickness),
                        new Capsule(World(now, a), World(now, b), d.HalfThickness), 0, _buf);
                    if (found > Buffer) lost += found - Buffer;
                    for (int j = 0; j < found && j < Buffer; j++)
                    {
                        StaticContact c = _buf[j];
                        if (!(c.Time < 1 || c.Distance <= 0)) continue;
                        int k = 0;
                        while (k < _count[i] && (_pairs[first + k].Object != c.Object || _pairs[first + k].Shape != c.Shape)) k++;
                        if (k == _count[i])
                        {
                            if (k == PairsPerShape)
                            {
                                lost++;
                                continue;
                            }
                            _count[i]++;
                            _pairs[first + k] = new Pair { Object = c.Object, Shape = c.Shape, Material = c.Material, Surface = c.Surface, Time = 2 };
                        }
                        ref Pair p = ref _pairs[first + k];
                        if (!p.Seen || c.Time < p.Time)
                        {
                            p.Time = c.Time;
                            p.P = p.Point = c.Point;
                            p.N = c.Normal;
                        }
                        p.Seen = true;
                    }
                }
            }
            // The slab's cut (p7f-damage design 4): a shape the bounding sphere meets that cuts the disc within the rim is the
            // disc's, and a pair the ring no longer touches lives while its shape still cuts it, so a rod inside the rim is met.
            Double3 axis = now.Attitude.Rotate(new Double3(0, 1, 0));
            for (int j = 0; gate > 0 && j < gated; j++)
            {
                StaticContact c = _gate[j];
                int k = 0;
                while (k < _count[i] && (_pairs[first + k].Object != c.Object || _pairs[first + k].Shape != c.Shape)) k++;
                if (k < _count[i] || !Cuts(d, hubNow, axis, c.Object, c.Shape, c.Point, c.Normal, c.Surface, out BladeCut.Meeting m)) continue;
                if (k == PairsPerShape)
                {
                    lost++;
                    continue;
                }
                _count[i]++;
                _pairs[first + k] = new Pair
                {
                    Object = c.Object, Shape = c.Shape, Material = c.Material, Surface = c.Surface, Time = 2, Seen = true, P = m.Point, Point = m.Point,
                    N = m.Normal,
                };
            }
            for (int k = 0; k < _count[i]; k++)
            {
                ref Pair p = ref _pairs[first + k];
                if (!p.Seen && Cuts(d, hubNow, axis, p.Object, p.Shape, p.P, p.N, p.Surface, out _)) p.Seen = true;
            }
            if (lost > 0) Lost(events, i, lost);
            int kept = 0;
            for (int k = 0; k < _count[i]; k++)
            {
                ref Pair p = ref _pairs[first + k];
                if (!p.Seen) continue; // it left the disc
                if (!p.Touching)
                {
                    p.Touching = true;
                    p.Since = now.Step;
                    p.MadeSpeed = -Double3.Dot(PointVelocity(now, p.Point), p.N);
                    FlightEvent e = Of(EventKind.PropStrike, i, p);
                    e.Index = (byte)d.Corner;
                    e.A = p.MadeSpeed;
                    events.Add(e);
                    StartImpact(e, p.MadeSpeed);
                }
                if (kept != k) _pairs[first + kept] = p;
                kept++;
            }
            _count[i] = kept;
        }

        // Whether (obj, shape) cuts the disc's slab within its rim at the hub's pose now (BladeCut, the world's geometry now:
        // a runtime object where it is; a terrain's ground the plane of the contact that names it, GroundPlane).
        bool Cuts(in ContactShape d, Double3 hub, Double3 axis, int obj, int shape, Double3 point, Double3 normal, byte surface, out BladeCut.Meeting m)
        {
            m = default;
            ShapeGeometry g;
            if (obj == WorldIds.Ground && _terrain) g = GroundPlane(point, normal, surface);
            else if (!_world.Geometry(obj, shape, out g)) return false;
            ReadOnlySpan<Double3> chain = default;
            if (g.Kind == ShapeKind.Wire)
            {
                chain = _world.WirePoints(obj, shape);
                BladeCut.WireWindow(chain, g.Radius, hub, d.Radius + d.HalfThickness, out int lo, out int hi);
                if (hi < lo) return false;
                chain = chain.Slice(lo, hi - lo + 2);
            }
            return BladeCut.Nearest(g, chain, hub, axis, d.HalfThickness, d.Radius, out m);
        }

        /// <summary>A terrain's ground at a contact as a plane (w3-farmland-ground 2.2): the plane through the contact's point with
        /// its normal, the piece it names (a facet, a step's face). A terrain has no one geometry (Geometry(Ground, 0) is false),
        /// and a prop's disc (0.127 m) spans the facets round one 0.977 m cell at most: its blades meet the piece its contact
        /// names.</summary>
        public static ShapeGeometry GroundPlane(Double3 point, Double3 normal, byte surface)
        {
            // Any two axes square to the normal: a plane's geometry is its point and AxisY.
            Double3 t = DetMath.Abs(normal.Y) < 0.9 ? new Double3(0, 1, 0) : new Double3(1, 0, 0);
            Double3 x = Double3.Cross(t, normal);
            x = x * (1 / x.Length());
            return new ShapeGeometry
            {
                Kind = ShapeKind.Plane, Center = point, AxisX = x, AxisY = normal, AxisZ = Double3.Cross(normal, x),
                Material = WorldIds.NoMaterial, Surface = surface,
            };
        }

        /// <summary>The ground is a terrain, not one plane: a disc's pair on it takes its geometry from GroundPlane.</summary>
        public bool Terrain => _terrain;

        /// <summary>Disc pair k of shape: the point and normal it touched at (on a terrain's ground, the piece's plane).</summary>
        public void DiscPairPlane(int shape, int k, out Double3 point, out Double3 normal)
        {
            ref Pair p = ref _pairs[shape * PairsPerShape + k];
            point = p.P;
            normal = p.N;
        }

        static Double3 World(in FlightState s, Double3 body) => s.Position + s.Attitude.Rotate(body);

        static bool Same(Double3 a, Double3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }
}
