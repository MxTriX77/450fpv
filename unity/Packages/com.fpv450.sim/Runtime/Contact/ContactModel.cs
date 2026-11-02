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
    ///   its support spheres on SampleGround's ground and on objects, and its skid capsule's object contacts that are not
    ///   within two radii of a touching support of the skid (P3). The leg carries F = k·δ_max − c·v̄ (at least 0), δ_max the
    ///   deepest point's depth, split over the points by depth, w_i = δ_i/Σδ; v̄ is the depth-weighted closing speed.
    /// - A body shape: each touching pair is its own spring and damper, the shape's stiffness along the normal in series
    ///   with the material's (a rigid material drops out: the shape's alone, never 1/(1/k + 1/∞), P7), c = 2ζ·√(k·m·share).
    /// - Friction (K§7.1 step 5): a stick anchor per pair, tangential stiffness ratio × the normal's, holding to μs·Fn and
    ///   sliding at μk·Fn; μ from physics_materials.json's row, or the ground's pair.
    /// - A prop disc: its bounding sphere each step; when that meets anything, a chord capsule over each ChordSegments
    ///   of the rim's segments, sized to hold them (1.7: the stand's posts sit inside the sphere, far from the rim); and
    ///   when a chord meets anything, its segments of the ring of capsules round the rim (P4). Both gates only skip
    ///   capsules that touch nothing, so the ring finds what it found without them. A shape's entry is a PropStrike, and
    ///   a crash when the shape is rigid or the ground and the entry is fast.
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
    /// </summary>
    public sealed class ContactModel
    {
        /// <summary>A corner's half-skid in soil (ContactModel.SoilOf): its leg's deflection, each node's sink below the
        /// soil's surface (middle, T-joint, outer end) and deepest sink, the soil's static force, the force with its damping,
        /// its stiffness, the load centroid's arc from the middle, the footprint now and at the deepest sinks, the anchor's
        /// force, and the series solve's residual k·x − F.</summary>
        public struct SkidSoil
        {
            public bool Engaged, Sliding;
            public byte Surface;
            public int Object, Shape;
            public double Deflection, Sink0, Sink1, Sink2, Max0, Max1, Max2, Static, Force, Stiffness, Centroid, Footprint, Wetted, Residual;
            public Double3 Friction;
        }

        /// <summary>The (object, shape) pairs one shape holds at once; more is a ContactsLost event.</summary>
        public const int PairsPerShape = 8;

        /// <summary>The rim's segments under one chord of the disc's second gate (1.7): 3 of 12 make four chords.</summary>
        public const int ChordSegments = 3;

        // The chord capsules' pad over the farthest point they hold, m: far above the rounding of the body-to-world
        // transform and the query's tests, far below anything physical; a larger gate only costs time.
        const double ChordPad = 1e-6;

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
            public Double3 Anchor;      // the friction's stick point, world
            public bool Anchored, Sliding;
            public long Since;
            public double Impulse, PeakForce, MadeSpeed, Time;
        }

        struct Row
        {
            public bool Known, Rigid;
            public double StaticFriction, KineticFriction, Stiffness, Damping;
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
            public Double3 Middle;               // body m: the skid's middle, the half-skid's inner end
            public double Radius, Length, Arc;   // m: the tube's radius, the middle to the end, the middle to the joint
            public double W0, W1, W2;            // the nodes' weights, m (exact for a quadratic over 0, Arc, Length)
            public Pair Ground;                  // the middle node's ground (Sense), as a support's
            public bool Engaged;                 // some node lies below the soil's surface
            public int Object, Shape;            // the soil's identity: the ground (its surface) or a soil shape
            public byte Surface;
            public double Max0, Max1, Max2;      // each node's deepest sink, m
            public double X;                     // the leg's deflection, m
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
        readonly Row[] _byMaterial;     // world material id → its row
        readonly Row _groundRow;
        readonly double _massShare;     // m·share, kg, for the damping ratios
        readonly StaticContact[] _buf = new StaticContact[Buffer];
        readonly XZ[] _at;
        readonly GroundSample[] _samples;
        readonly Double3[] _ring;       // the rim's unit directions in the disc's plane (body xz)
        readonly double[] _chordReach;  // each chord's farthest rim vertex from it, on the unit ring

        // The soil (surfaces.json at the flight's moisture), or null without the table.
        readonly Skid[] _skids;         // by corner
        readonly SoilSet[] _soil;       // world surface id → its set
        readonly Row[] _surfaceRow;     // world surface id → its friction as a rigid ground
        readonly Print[] _prints;
        int _printNext;

        // The impact window.
        int _impactLeft, _impacts;
        double _impactPeak, _lastPeak, _lastImpulse;
        Double3 _impactImpulse;
        FlightEvent _impact;

        /// <summary>The model for a drone of the given mass in world; moisture picks surfaces.json's sets (D-021: Sunny is
        /// Dry, Rain Wet, Severe rain Saturated), unread without the table.</summary>
        public ContactModel(ContactSpec spec, FlightWorld world, double mass, Moisture moisture = Moisture.Dry)
        {
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
            SurfaceTable table = _spec.Surfaces;
            if (table != null) _skids = Skids();
            int middles = _skids != null ? 4 : 0; // the supports' samples, then the skids' middles
            _at = new XZ[supports + middles];
            _samples = new GroundSample[supports + middles];
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
                    Known = true, End = end, Joint = joint, Middle = sk.B, Radius = sk.Radius, Length = length, Arc = arc,
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

            // The legs, corner by corner.
            for (int c = 0; c < 4; c++)
            {
                if (_skids != null && _skids[c].Known
                    && SoilCorner(c, s, com, ref f, ref m, ref load, ref compression, ref touching, ref strongest, ref strongestPair, ref strongestForce))
                    continue;
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
                if (_shapes[i].Role != ContactRole.Body) continue;
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
                    Apply(ref p, i, k, fn, kn, cn, p.Depth, row, s, com, ref f, ref m, ref touching, ref strongest, ref strongestPair, ref strongestForce);
                }
            }

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
                _impactImpulse = _impactImpulse + f * FlightCore.Dt;
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
            if (!(p.Depth > 0)) return;
            if (p.Depth > deepest)
            {
                deepest = p.Depth;
                deepShape = shape;
                deepPair = pair;
            }
            sum += p.Depth;
            closing += p.Depth * Double3.Dot(PointVelocity(s, p.Point), p.N);
        }

        // A pair's share of a normal force fn (its depth over total), its friction, into the step's force and moment.
        void Apply(ref Pair p, int shape, int pair, double fn, double kn, double cn, double total, in Row row, in FlightState s, Double3 com,
            ref Double3 f, ref Double3 m, ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce)
        {
            if (!(p.Depth > 0)) return;
            double w = p.Depth / total, share = fn * w;
            Double3 v = PointVelocity(s, p.Point);
            Double3 ft = Friction(ref p, share, _spec.FrictionStiffnessRatio * kn * w, DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cn * w, v, row);
            Double3 fw = p.N * share + ft;
            f = f + fw;
            m = m + Double3.Cross(s.Attitude.InverseRotate(p.Point - s.Position) - com, s.Attitude.InverseRotate(fw));
            touching++;
            p.Impulse += share * FlightCore.Dt;
            if (share > p.PeakForce) p.PeakForce = share;
            if (share > strongestForce)
            {
                strongestForce = share;
                strongest = shape;
                strongestPair = pair;
            }
        }

        // ---- The soil under a half-skid (p7a-ground 1.10) -----------------------------------------------------------------

        /// <summary>
        /// Corner c's leg on soil (design 2, 3), when its half-skid lies below a soil's surface at some node: the series solve
        /// with the leg, the soil's force and damping at its load centroid with the groove's anchor, the corner's other points
        /// (rigid or compliant, Surface 0) through the same leg, the grooves' state and the footprint ring. False leaves the
        /// corner to the leg alone on its points, as without the table (no soil under it, or a rigid surface).
        /// </summary>
        bool SoilCorner(int c, in FlightState s, Double3 com, ref Double3 f, ref Double3 m, ref Corners load, ref Corners compression,
            ref int touching, ref int strongest, ref int strongestPair, ref double strongestForce)
        {
            ref Skid k = ref _skids[c];
            double r = k.Radius;
            Double3 c0 = World(s, k.Middle), c1 = World(s, _shapes[k.Joint].A), c2 = World(s, _shapes[k.End].A);

            // The deepest soil shape the corner's parts touch: where the plane is absent (a patch, a hole band) it is the ground.
            int soilShape = -1, soilPair = -1;
            double soilDepth = 0;
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactRole role = _shapes[i].Role;
                if (_shapes[i].Corner != c || role != ContactRole.Support && role != ContactRole.Skid) continue;
                for (int j = 0; j < _count[i]; j++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + j];
                    if (p.Touching && p.Surface != WorldIds.NoSurface && p.Depth > soilDepth)
                    {
                        soilDepth = p.Depth;
                        soilShape = i;
                        soilPair = j;
                    }
                }
            }
            Pair none = default;
            ref Pair soil = ref soilShape >= 0 ? ref _pairs[soilShape * PairsPerShape + soilPair] : ref none;
            bool has0 = Node(k.Ground, c0, r, soil, soilShape >= 0, out double e0, out Double3 n0, out byte s0, out int o0, out int h0);
            bool has1 = Node(_ground[k.Joint], c1, r, soil, soilShape >= 0, out double e1, out Double3 n1, out byte s1, out int o1, out int h1);
            bool has2 = Node(_ground[k.End], c2, r, soil, soilShape >= 0, out double e2, out Double3 n2, out byte s2, out int o2, out int h2);

            // The deepest node sets the surface, the normal and the soil's identity (design 5's rule, carried).
            double deepNode = double.NegativeInfinity;
            Double3 n = default;
            byte surface = WorldIds.NoSurface;
            int obj = 0, shp = 0;
            if (has0 && e0 > deepNode) { deepNode = e0; n = n0; surface = s0; obj = o0; shp = h0; }
            if (has1 && e1 > deepNode) { deepNode = e1; n = n1; surface = s1; obj = o1; shp = h1; }
            if (has2 && e2 > deepNode) { deepNode = e2; n = n2; surface = s2; obj = o2; shp = h2; }
            bool soft = surface != WorldIds.NoSurface && _surfaceRow[surface].Known && !_soil[surface].Rigid;
            if (k.Engaged && (!soft || !(deepNode > 0) || k.Object != obj || k.Shape != shp || k.Surface != surface)) Leave(ref k, c0, c2, n);
            if (!soft || !(deepNode > 0)) return false;
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

            // The series solve: k·x = F_soil(δ − x) + k_o·(δ_o − x)⁺, safeguarded Newton from the last step's x; a rigid
            // other point holds the leg to its depth.
            SoilSet set = _soil[surface];
            double k0 = _spec.LegStiffness, c0leg = _spec.LegDamping;
            double kl = Soil.Modulus(set, Soil.Width(SoilPart.Tube, r)), firm = _spec.Surfaces.FirmLayer;
            double ko = other && !orow.Rigid ? orow.Stiffness : 0;
            double lo = 0, hi = DetMath.Max(deepNode, other ? deep : 0);
            double x = DetMath.Min(hi, DetMath.Max(lo, k.X));
            for (int it = 0; it < NewtonSteps; it++)
            {
                double fs = Tube(k, set, kl, firm, e0 - x, e1 - x, e2 - x, out double ks);
                double fo = ko > 0 && deep > x ? ko * (deep - x) : 0;
                double residual = k0 * x - fs - fo, slope = k0 + ks + (ko > 0 && deep > x ? ko : 0);
                if (residual == 0) break;
                if (residual > 0) hi = x;
                else lo = x;
                double next = x - residual / slope;
                x = next >= lo && next <= hi ? next : 0.5 * (lo + hi); // a converged step lands on its bracket's end
            }
            bool held = other && orow.Rigid && deep > x; // the rigid point stops the leg short of the soil's solution
            if (held) x = deep;

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
            Double3 ft = SoilFriction(ref k, s, point, n, fn, _spec.FrictionStiffnessRatio * kn, DetMath.Sqrt(_spec.FrictionStiffnessRatio) * cs,
                axis, set, set.InterfaceAdhesion * wetted, side, end);
            Double3 fw = n * fn + ft;
            f = f + fw;
            m = m + Double3.Cross(s.Attitude.InverseRotate(point - s.Position) - com, s.Attitude.InverseRotate(fw));
            touching++;
            ref Pair credit = ref obj == WorldIds.Ground ? ref _ground[k.End] : ref soil;
            credit.Impulse += fn * FlightCore.Dt;
            if (fn > credit.PeakForce) credit.PeakForce = fn;
            if (fn > strongestForce)
            {
                strongestForce = fn;
                strongest = obj == WorldIds.Ground ? k.End : soilShape;
                strongestPair = obj == WorldIds.Ground ? -1 : soilPair;
            }

            // The other points: what the leg carries beyond the soil (a rigid point) or their own spring's share.
            double carried = 0;
            if (other)
            {
                double fo = held ? k0 * x - soilStatic : !orow.Rigid && deep > x ? ko * (deep - x) : 0;
                double co = orow.Rigid ? c0leg : Series(c0leg, orow.Damping);
                fo -= co * closing / sum;
                if (fo > 0)
                {
                    carried = fo;
                    double kno = orow.Rigid ? k0 : Series(k0, orow.Stiffness);
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
            load[c] = fn + carried;
            compression[c] = x;

            k.Last = new SkidSoil
            {
                Engaged = true, Surface = surface, Object = obj, Shape = shp, Deflection = x, Sink0 = z0, Sink1 = z1, Sink2 = z2,
                Max0 = DetMath.Max(k.Max0, z0), Max1 = DetMath.Max(k.Max1, z1), Max2 = DetMath.Max(k.Max2, z2), Static = soilStatic,
                Force = fn, Stiffness = soilStiffness, Centroid = arc, Footprint = footprint, Wetted = wetted, Friction = ft, Sliding = k.Sliding,
                Residual = k0 * x - soilStatic - (held ? k0 * x - soilStatic : !orow.Rigid && other && deep > x ? ko * (deep - x) : 0),
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

        // A node's overlap of the ground (δ, m, along its normal), from the plane's sample where it has one, else the soil
        // shape's plane; false where neither is there.
        static bool Node(in Pair ground, Double3 core, double r, in Pair soil, bool hasSoil, out double depth, out Double3 n, out byte surface, out int obj, out int shape)
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
            k.Last = default;
        }

        /// <summary>Corner c's half-skid on soil as the last Forces left it (default when it is not in soil, or without
        /// surfaces.json).</summary>
        public SkidSoil SoilOf(int corner) => _skids != null && _skids[corner].Engaged ? _skids[corner].Last : default;

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

        /// <summary>The stick anchor (K§7.1 step 5): a tangential spring kt and damper ct to the anchor, holding to μs·fn and
        /// sliding at μk·fn, the anchor dragged along so the spring carries the sliding force.</summary>
        static Double3 Friction(ref Pair p, double fn, double kt, double ct, Double3 v, in Row row)
        {
            if (!(fn > 0) || !(kt > 0))
            {
                p.Anchored = false;
                return default;
            }
            if (!p.Anchored)
            {
                p.Anchor = p.Point;
                p.Anchored = true;
                p.Sliding = false;
            }
            Double3 n = p.N, d = p.Point - p.Anchor;
            d = d - n * Double3.Dot(n, d);
            Double3 vt = v - n * Double3.Dot(n, v);
            Double3 ft = d * -kt - vt * ct;
            double size = ft.Length(), limit = (p.Sliding ? row.KineticFriction : row.StaticFriction) * fn;
            if (size > limit)
            {
                ft = ft * (row.KineticFriction * fn / size);
                p.Anchor = p.Point + ft * (1 / kt);
                p.Sliding = true;
            }
            else
            {
                p.Sliding = false;
            }
            return ft;
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
                g.Depth = hole ? -1 : (gs.Height - (x.Y - _skids[c].Radius)) * gs.Normal.Y; // as a support's (K§7.1 step 2)
            }
            for (int k = 0; k < _supports.Length; k++)
            {
                int i = _supports[k];
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
                ContactShape shape = _shapes[i];
                if (shape.Role == ContactRole.Disc)
                {
                    Disc(i, was, ref now, events);
                    continue;
                }
                var before = new Capsule(World(was, shape.A), World(was, shape.B), shape.Radius);
                var after = new Capsule(World(now, shape.A), World(now, shape.B), shape.Radius);
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
                bool same = capsule && SameSupport(i, c.Point);
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
                    if (!p.Seen && p.Latched && p.Depth > 0 && (p.Flipped || PastCentre(p, part))) Tunnel(i, p, part, now, events);
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

        // A capsule's contact at point lies within two radii of a touching sphere of the same part: the same support (P3).
        bool SameSupport(int capsule, Double3 point)
        {
            ContactShape c = _shapes[capsule];
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactShape s = _shapes[i];
                if (i == capsule || s.Part != c.Part || s.Role == ContactRole.Disc || !Same(s.A, s.B)) continue;
                double reach = c.Radius + s.Radius;
                if (s.Role == ContactRole.Support && _ground[i].Touching && Near(_ground[i].Point, point, reach)) return true;
                for (int k = 0; k < _count[i]; k++)
                {
                    ref Pair p = ref _pairs[i * PairsPerShape + k];
                    if (p.Touching && !p.Suppressed && Near(p.Point, point, reach)) return true;
                }
            }
            return false;
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
            _impactLeft = (int)(_spec.ImpactWindow / FlightCore.Dt + 0.5);
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
