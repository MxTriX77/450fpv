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

        /// <summary>A prop disc entered a rigid shape or the ground faster than ContactSpec.CrashPropSpeed.</summary>
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
        public const int MaxPieces = 3;

        /// <summary>The broken legs whose tubes are now their stubs in the clamps, bit 1 &lt;&lt; corner.</summary>
        public int Stubs;

        /// <summary>The pieces: each one's parts (a mask: bit c a leg's whole tube, 4 + c its part below a break, 8 + c a T-joint,
        /// 12 the right skid, 13 the left), its contact shapes (indices into the drone's) and their cores as they left (body).</summary>
        public int Pieces;
        public readonly int[] Parts = new int[MaxPieces];
        public readonly int[][] Shapes = { new int[16], new int[16], new int[16] };
        public readonly ContactShape[][] Geometry = { new ContactShape[16], new ContactShape[16], new ContactShape[16] };
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
    ///   capsules that touch nothing, so the ring finds what it found without them. A shape's entry is a PropStrike, and
    ///   a crash when the shape is rigid or the ground and the entry is fast.
    /// No restitution coefficient anywhere: a bounce is the stored energy of the compliance and the thrust.
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
        /// <summary>The (object, shape) pairs one shape holds at once; more is a ContactsLost event.</summary>
        public const int PairsPerShape = 8;

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

        const int Buffer = 32;

        struct Pair
        {
            public int Object, Shape;
            public ushort Material;
            public byte Surface;
            public Double3 P, N, Entry; // the plane, and the normal the part came in by
            public bool Latched, Flipped, Seen, Touching, Suppressed;
            public double Depth;        // at the pose Sense last saw, m
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
        }

        readonly ContactSpec _spec;
        readonly FlightWorld _world;
        readonly ContactShape[] _shapes;
        readonly int[] _order;          // spheres, then capsules, then discs: a capsule's dedupe reads its part's spheres
        readonly int[] _supports;       // the support shapes, in shape order
        readonly Pair[] _pairs;         // shape i's pairs at i·PairsPerShape
        readonly int[] _count;
        readonly Pair[] _ground;        // a support's ground (index: shape)
        readonly Row[] _byMaterial;     // world material id → its row
        readonly Row _groundRow;
        double _massShare;              // m·share, kg, for the damping ratios (a loose piece's own, Reuse)
        readonly double _dt;            // the step it is called at: the flight's 1 ms, a loose piece's sub-step
        readonly StaticContact[] _buf = new StaticContact[Buffer];
        readonly XZ[] _at;
        readonly GroundSample[] _samples;
        readonly Double3[] _ring;       // the rim's unit directions in the disc's plane (body xz)
        readonly double[] _chordReach;  // each chord's farthest rim vertex from it, on the unit ring

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
        readonly Double3[] _hangForce = new Double3[4], _hangCouple = new Double3[4];
        readonly bool[] _hangTouching = new bool[4];
        readonly int[] _legTube = { -1, -1, -1, -1 };  // each leg's tube shape
        readonly LegFlags[] _followed = new LegFlags[4]; // the legs' flags the structure last followed
        readonly LegChange _change = new LegChange();
        bool _restructure;

        // The hanging skid's cantilever: its tube's EI, and the lever from a bore to the other leg's (derived, 1.1's section).
        readonly double _skidBending, _skidLever, _skidContact;
        readonly Double3[] _elastic = new Double3[4];   // a hanging half's elastic turn about its holder's bore (body rotation vector)

        // The impact window.
        int _impactLeft, _impacts;
        double _impactPeak, _lastPeak, _lastImpulse;
        Double3 _impactImpulse;
        FlightEvent _impact;

        /// <summary>The contacts of a flight; random draws the legs' strengths when the spec has their law (a flight's
        /// FlightRandom, so its header lists them).</summary>
        public ContactModel(ContactSpec spec, FlightWorld world, double mass, FlightRandom random = null, double dt = FlightCore.Dt)
        {
            _dt = dt;
            _spec = (spec ?? throw new ArgumentNullException(nameof(spec))).Clone();
            _world = world ?? throw new ArgumentNullException(nameof(world));
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
            _at = new XZ[supports];
            _samples = new GroundSample[supports];
            _massShare = mass * _spec.DampingMassShare;

            // The world's materials by id, from the table by name; one the table lacks is refused.
            IWorldQuery q = world.Query;
            int top = 0;
            for (int id = 1; id < ushort.MaxValue; id++)
                if (q.MaterialName((ushort)id) != null) top = id;
            _byMaterial = new Row[top + 1];
            for (int id = 1; id <= top; id++)
            {
                string name = q.MaterialName((ushort)id);
                if (name == null) continue;
                if (!_spec.Row(name, out MaterialRow r))
                    throw new ArgumentException("the world's material '" + name + "' has no row in physics_materials.json", nameof(spec));
                _byMaterial[id] = new Row
                {
                    Known = true, Rigid = r.Rigid, StaticFriction = r.StaticFriction, KineticFriction = r.KineticFriction, Stiffness = r.Stiffness,
                    Damping = r.Rigid ? 0 : 2 * r.DampingRatio * DetMath.Sqrt(r.Stiffness * _massShare),
                };
            }
            _groundRow = new Row { Known = true, Rigid = true, StaticFriction = _spec.GroundStaticFriction, KineticFriction = _spec.GroundKineticFriction };

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
                _byMaterial[id].Damping = r.Rigid ? 0 : 2 * r.DampingRatio * DetMath.Sqrt(r.Stiffness * _massShare);
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

        static double SegmentDistance(Double3 p, Double3 a, Double3 b)
        {
            Double3 ab = b - a;
            double t = DetMath.Min(1, DetMath.Max(0, Double3.Dot(p - a, ab) / Double3.Dot(ab, ab)));
            return (p - (a + ab * t)).Length();
        }

        public ContactSpec Spec => _spec;

        /// <summary>Impacts so far, and the latest one's peak acceleration (m/s²) and impulse (N·s): the feed's Impact.</summary>
        public int ImpactCount => _impacts;
        public double ImpactPeakAccel => _lastPeak;
        public double ImpactImpulse => _lastImpulse;

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
            if (series) LegTrials(s, axis, ref compression);
            else for (int c = 0; c < 4; c++)
            {
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
                if (!(sum > 0)) continue;
                closing /= sum;
                double k0 = _spec.LegStiffness, c0 = _spec.LegDamping;
                Row row = RowOf(deepPair < 0 ? _ground[deepShape] : _pairs[deepShape * PairsPerShape + deepPair]);
                double kn = row.Rigid ? k0 : Series(k0, row.Stiffness), cn = row.Rigid ? c0 : Series(c0, row.Damping);
                double fn = kn * deepest - cn * closing;
                if (fn < 0) fn = 0;
                load[c] = fn;
                compression[c] = deepest;
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

            // The body shapes, pair by pair.
            for (int i = 0; i < _shapes.Length; i++)
            {
                if (_shapes[i].Role != ContactRole.Body || _off[i]) continue;
                ContactShape shape = _shapes[i];
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (!p.Touching || p.Suppressed) continue;
                    Row row = RowOf(p);
                    Double3 nb = s.Attitude.InverseRotate(p.N);
                    double up = nb.Y * nb.Y;
                    double ks = 1 / (up / shape.StiffnessUp + (1 - up) / shape.Stiffness);
                    double cs = 2 * shape.DampingRatio * DetMath.Sqrt(ks * _massShare);
                    double kn = row.Rigid ? ks : Series(ks, row.Stiffness), cn = row.Rigid ? cs : Series(cs, row.Damping);
                    double fn = kn * p.Depth - cn * Double3.Dot(PointVelocity(s, p.Point), p.N);
                    if (fn < 0) fn = 0;
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

            if (series) LegSums(s, axis, com, ref f, ref m, ref load, ref touching, ref strongest, ref strongestPair, ref strongestForce, events);
            LegTrace(ref trace);

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

        // A hanging half's point's move along its normal by the half's elastic turn about the holding leg's bore (body), m:
        // what its overlap without that turn adds to its depth.
        double Unturned(int c, in Pair p, in FlightState s)
        {
            if (Same(_elastic[c], default)) return 0;
            int h = _spec.Legs.Sibling[c];
            Double3 bore = Rotated(_spec.Legs.Bore[h], h), at = s.Attitude.InverseRotate(p.Point - s.Position) - bore;
            Double3 moved = at - Turn(_elastic[c]).Conjugate().Rotate(at);
            return Double3.Dot(p.N, s.Attitude.Rotate(moved));
        }

        // ---- The legs' side give (p7g-legs 1.2, design 3) -------------------------------------------------------------------

        // Each leg point's force before its friction cone. A leg is a spring along its tube (LegStiffness, in series with the
        // ground's material, as before) and across it at the foot (its side give in series with the stick anchor, the ratio
        // of the first): the point's push into it, d = δ_max·n − the anchor's stretch, splits along and across the tube, so a
        // gripping foot loads the leg through the tube's bending (262 N/m, never the anchor's 26 kN/m), and a tilted leg pushes
        // its foot along its tube, a strut. Its dampers act on the point's velocity the same way. The legs' law moves the
        // springs' rests (LegDamage's offsets) once a leg yields, bows, slips or turns.
        void LegTrials(in FlightState s, Double3 up, ref Corners compression)
        {
            // The legs first, then the hanging halves, whose give is in series with the leg that holds them.
            for (int pass = 0; pass < 2; pass++)
            for (int c = 0; c < 4; c++)
            {
                CornerState state = _corner[c];
                bool hanging = (state & CornerState.Hanging) != 0;
                if ((pass == 1) != hanging) continue;
                _legTouching[c] = false;
                if ((state & CornerState.SkidGone) != 0) continue;
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
                    continue;
                }
                compression[c] = deepest;
                Row row = RowOf(deepPair < 0 ? _ground[deepShape] : _pairs[deepShape * PairsPerShape + deepPair]);
                double ka = row.Rigid ? k0 : Series(k0, row.Stiffness), ca = row.Rigid ? c0 : Series(c0, row.Damping);
                double kl = ks > 0 ? Series(ks, ratio * ka) : 0;
                double cl = cs > 0 ? Series(cs, DetMath.Sqrt(ratio) * ca) : 0;
                _axial[c] = ka;
                _side[c] = kl;
                _tubeSide[c] = hanging ? 0 : ks;
                bool moved = !hanging && _legs != null && _legs.Moved(c);
                double along = 0;
                Double3 across = default;
                if (moved)
                {
                    along = _legs.AxialOffset(c);
                    across = s.Attitude.Rotate(_legs.LateralOffset(c));
                }
                for (int i = 0; i < _shapes.Length; i++)
                {
                    ContactRole role = _shapes[i].Role;
                    if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                    if (role == ContactRole.Support && _ground[i].Touching) Trial(ref _ground[i], s, up, sum, deepest, ka, ca, kl, cl, moved, along, across, _dt);
                    for (int k = 0; k < _count[i]; k++)
                    {
                        ref Pair p = ref _pairs[i * PairsPerShape + k];
                        if (p.Touching && !p.Suppressed) Trial(ref p, s, up, sum, deepest, ka, ca, kl, cl, moved, along, across, _dt);
                    }
                }
            }
        }

        // A leg's springs this step (p7g-legs 1.3): along it, k0 with its damper c0 (the tube's EA/L share scaled by its
        // crack's factor, the damper with it at the same ζ), and across it at the foot, ks with its damper cs: the tube's
        // 3EI/L³ by the crack's factor less the axial load's geometric stiffness, k_t·P/P_sway, never below 0; the jaws held
        // (their bed turns by their friction, LegDamage). Without the parts (1.2's chain), LegSideStiffness as it was.
        void LegGive(int c, out double k0, out double c0, out double ks, out double cs)
        {
            k0 = _spec.LegStiffness;
            c0 = _spec.LegDamping;
            ks = _spec.LegSideStiffness;
            cs = 2 * _spec.LegSideDampingRatio * DetMath.Sqrt(ks * _massShare);
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
            ks = kt - _spec.LegTubeSideStiffness * _lastAxial[c] / _spec.LegSwayLoad;
            if (!(ks > 0)) ks = 0;
        }

        // One leg point's force: its share of its leg's push along and across the tube, less the dampers; the stick anchor
        // stretched by the touching material's slip, as Friction does.
        static void Trial(ref Pair p, in FlightState s, Double3 up, double sum, double deepest, double ka, double ca, double kl, double cl,
            bool moved, double along, Double3 across, double dt)
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
            p.Force = (up * (ka * a - ca * va) + lat * kl - (v - up * va) * cl) * w;
            p.Share = w;
            p.Drift = lat;
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
                    if ((_corner[c] & CornerState.Hanging) != 0) Hang(c, s, events);
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
        // leg's bore, and past the skid's first yield there (the holding leg's drawn σ_y·I/(c·K_t), its edge factor on the
        // bore's edge) the fold, perfectly plastic: the excess over the cap, (|F| − M_y/a)/k along F, turns the half about
        // the bore (from the next step its shapes, and its held points' anchors with them), and this step's force is relieved
        // by it. Its force and its couple about the bore load the holding leg (Judge).
        void Hang(int c, in FlightState s, StepEvents events)
        {
            LegDamageSpec l = _spec.Legs;
            int holder = l.Sibling[c];
            Double3 law = default, bore = World(s, Rotated(l.Bore[holder], holder));
            Double3 couple = default, deepAt = default, deepN = default;
            int causeShape = -1, causePair = -1;
            double strong = 0, deepest = 0;
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
            double cap = _legs.CrackMoment(holder) / _skidLever, size2 = law.Length();
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
            // The elastic turn: the massless half gives |F|/k_cantilever, its deepest point moved that far out along its normal
            // by the least turn about the bore, φ = (r × n)·δ/|r × n|² (none untouched).
            _elastic[c] = default;
            double carried = law.Length(), kc = HangingStiffness;
            if (deepest > 0 && carried > 0 && kc > 0)
            {
                Double3 r = s.Attitude.InverseRotate(deepAt - bore), axis = Double3.Cross(r, s.Attitude.InverseRotate(deepN));
                double a2 = Double3.Dot(axis, axis);
                if (a2 > 0) _elastic[c] = axis * (carried / kc / a2);
            }
            _hangForce[c] = law;
            _hangCouple[c] = s.Attitude.InverseRotate(couple);
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
            Double3 law = default, foot = World(s, Rotated(_spec.Legs.Foot[c], c)), skid = s.Attitude.Rotate(new Double3(0, 0, 1));
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
            }
            double axial = Double3.Dot(up, law);
            // The tube's own deflection across at the foot (1.3): its share of the spring's push across, d·k_a/(k_s + k_a) with the
            // anchor's k_a in series (all of it with no give left), less what a sliding foot's cone lets through.
            Double3 lateral = law - up * axial, deflection = default;
            if (_spec.LegTubeSideStiffness > 0 && touching)
            {
                double anchor = _spec.FrictionStiffnessRatio * _axial[c], ks = _tubeSide[c];
                deflection = drift * (anchor / (ks + anchor));
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
        }

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
                    force = n * fn + t * kinetic;
                    if (ktt > 0)
                    {
                        Double3 st = p.Stretch + t * ((size - kinetic) / ktt);
                        p.Stretch = st - n * Double3.Dot(n, st);
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

        // Where a shape's build point is now: a leg tube's by its leg's set; a skid half's by its leg's set, or hanging, by
        // the holding leg's set and then its fold about that leg's bore.
        Double3 Placed(int i, Double3 p)
        {
            int c = _base[i].Corner;
            if (_legOf[i] >= 0) return Rotated(p, _legOf[i]);
            if ((_corner[c] & CornerState.Hanging) == 0) return Rotated(p, c);
            int h = _spec.Legs.Sibling[c];
            Double3 bore = Rotated(_spec.Legs.Bore[h], h), q = Rotated(p, h);
            return bore + _foldTurn[c].Rotate(q - bore);
        }

        // A hanging half's whole turn about its holder's bore: its fold and its elastic give.
        Double3 HangTurn(int c) => _fold[c] + _elastic[c];

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
            }
            for (int i = 0; i < _shapes.Length; i++)
            {
                if (_off[i]) continue;
                int c = _base[i].Corner, leg = _legOf[i];
                bool move;
                if (leg >= 0) move = _moved[leg] && (_corner[leg] & (CornerState.Stub | CornerState.TubeGone)) == 0;
                else if (c < 0 || _base[i].Role != ContactRole.Support && _base[i].Role != ContactRole.Skid) continue;
                else if ((_corner[c] & CornerState.Hanging) != 0) move = _moved[_spec.Legs.Sibling[c]] || !Same(HangTurn(c), _foldApplied[c]);
                else move = _moved[c];
                if (move) Place(i, Placed(i, _base[i].A), Placed(i, _base[i].B), s);
            }
            for (int c = 0; c < 4; c++)
            {
                if (Same(HangTurn(c), _foldApplied[c])) continue;
                _foldApplied[c] = HangTurn(c);
                _foldTurn[c] = Turn(HangTurn(c));
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
                        if (_legs.SkidMode(c) != LegMode.TJointFracture) ch.Parts[piece] |= 1 << (8 + c);
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
                        if (_legs.SkidMode(c) == LegMode.TJointFracture) ch.Parts[piece] |= 1 << (8 + c);
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
            ref Double3 f, ref Double3 m, ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce)
        {
            if (!(p.Depth > 0)) return default;
            double w = p.Depth / total, share = fn * w;
            Double3 v = PointVelocity(s, p.Point);
            Double3 ft = Friction(ref p, share, _spec.FrictionStiffnessRatio * kn * w, DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cn * w, v, row, _dt);
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

        // The ground is the plane and every soil shape (a patch's soil, a hole's fillers): its surface, never its object.
        Row RowOf(in Pair p) => p.Surface == WorldIds.NoSurface && p.Material < _byMaterial.Length && _byMaterial[p.Material].Known
            ? _byMaterial[p.Material] : _groundRow;

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
            // The supports' ground, in one batch.
            for (int k = 0; k < _supports.Length; k++)
            {
                Double3 x = World(now, _shapes[_supports[k]].A);
                _at[k] = new XZ(x.X, x.Z);
            }
            if (_supports.Length > 0) _world.SampleGround(_at, _samples);
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
                g.Depth = hole ? -1 : (gs.Height - (x.Y - _shapes[i].Radius)) * gs.Normal.Y; // K§7.1 step 2
                g.Point = new Double3(x.X, hole ? x.Y : gs.Height, x.Z);
                g.P = g.Point;
                Transition(ref g, i, now, events, false);
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
                int found = _world.StaticContacts(before, after, _spec.Margin, _buf);
                if (found > Buffer) Lost(events, i, found - Buffer);
                Update(i, found < Buffer ? found : Buffer, after, now, events);
            }
        }

        // One shape's pairs against this step's contacts (the latch, P1), then each pair's depth and its transitions.
        void Update(int i, int found, in Capsule part, in FlightState now, StepEvents events)
        {
            ContactShape shape = _shapes[i];
            int first = i * PairsPerShape;
            bool groundless = shape.Role == ContactRole.Support || shape.Role == ContactRole.Skid;
            bool capsule = !Same(shape.A, shape.B);
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
                int k = 0;
                while (k < _count[i] && (_pairs[first + k].Object != c.Object || _pairs[first + k].Shape != c.Shape)) k++;
                bool same = capsule ? SameSupport(i, c) : Seam(j, found, groundless);
                if (k == _count[i])
                {
                    if (same) continue;
                    if (k == PairsPerShape)
                    {
                        lost++;
                        continue;
                    }
                    _count[i]++;
                    _pairs[first + k] = new Pair
                    {
                        Object = c.Object, Shape = c.Shape, Material = c.Material, Surface = c.Surface, P = c.Point, N = c.Normal, Entry = c.Normal,
                        Latched = c.Time < 1, Seen = true, Time = c.Time,
                    };
                    continue;
                }
                ref Pair p = ref _pairs[first + k];
                p.Seen = true;
                if (same)
                {
                    p.Suppressed = true; // the support beside it carries this touch
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
                if (!p.Seen || p.Suppressed)
                {
                    // Only a rigid shape or the ground can be passed by mistake: a compliant one (a crown's twig shell, a
                    // straw mat) yields to its own law, and leaving it is an ordinary break.
                    if (!p.Seen && p.Latched && p.Depth > 0 && RowOf(p).Rigid && (p.Flipped || PastCentre(p, part))) Tunnel(i, p, part, now, events);
                    if (p.Touching) Broken(ref p, i, now.Step, events);
                    continue;
                }
                if (p.Latched && -p.Depth > _spec.Margin)
                {
                    if (p.Touching) Broken(ref p, i, now.Step, events);
                    continue;
                }
                Transition(ref p, i, now, events, true);
                if (kept != k) _pairs[first + kept] = p;
                kept++;
            }
            _count[i] = kept;
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
        // capsule's line contact is left to the query and the skids' same-face rule (SameSupport).
        bool Seam(int j, int found, bool groundless)
        {
            StaticContact c = _buf[j];
            for (int q = 0; q < found; q++)
            {
                StaticContact d = _buf[q];
                double deeper = c.Distance - d.Distance;
                if (q == j || !(deeper > SeamTie || deeper >= -SeamTie && q < j) || groundless && d.Object == WorldIds.Ground) continue;
                if (OnPlane(d.Normal, d.Point, c.Point)) return true;
            }
            return false;
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

        // Touching starts or ends: ContactMade (and maybe an impact) or ContactBroken.
        void Transition(ref Pair p, int i, in FlightState now, StepEvents events, bool objectPair)
        {
            bool touching = p.Depth > 0;
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
                    bool rigid = RowOf(p).Rigid;
                    if (!now.Crashed && rigid && p.MadeSpeed > _spec.CrashPropSpeed) Crash(ref now, events, i, p, CrashCause.PropStrike, p.MadeSpeed);
                }
                if (kept != k) _pairs[first + kept] = p;
                kept++;
            }
            _count[i] = kept;
        }

        static Double3 World(in FlightState s, Double3 body) => s.Position + s.Attitude.Rotate(body);

        static bool Same(Double3 a, Double3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }
}
