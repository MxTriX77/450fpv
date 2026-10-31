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

        // The impact window.
        int _impactLeft, _impacts;
        double _impactPeak, _lastPeak, _lastImpulse;
        Double3 _impactImpulse;
        FlightEvent _impact;

        public ContactModel(ContactSpec spec, FlightWorld world, double mass)
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

        Row RowOf(in Pair p) => p.Material != WorldIds.NoMaterial && p.Material < _byMaterial.Length && _byMaterial[p.Material].Known
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
