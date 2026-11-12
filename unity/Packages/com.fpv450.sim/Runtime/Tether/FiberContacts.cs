using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tether
{
    /// <summary>What a fiber contact touches (p7d-tether design 6): FiberContact's and FiberSlip's Index, and CableContact.MemberKind.</summary>
    public enum FiberMember : byte
    {
        None = 0,
        /// <summary>The plane ground under a node (SampleGround's batch): the coating's friction on its surface.</summary>
        Ground = 1,
        /// <summary>A shape the line lies on as on the ground: a soil shape (a patch's soil, a trench's floor and walls) or a
        /// layer with an area law (S04's straw boxes, a mattress, a sandbag): the coating's friction there, never a capstan.</summary>
        Layer = 2,
        /// <summary>Any other shape (a limb, a post, a wall, the stand's bars): a capstan of its face, its arris edge_radius_m.</summary>
        Shape = 3,
        /// <summary>A drawn twig (MicroDetailNear): a fixed capstan of its own radius.</summary>
        Twig = 4,
        /// <summary>A drawn standing stem: a cantilever the line drags through, letting go past its slide-off or Euler's load.</summary>
        Stem = 5,
        /// <summary>A drawn anchored straw lying over the line at its first touch: a hook, letting go at its release force.</summary>
        Straw = 6,
        /// <summary>A snag record's segment (SnagsNear): a capstan of its edge radius; with a barb pitch held at its notch.</summary>
        Snag = 7,
        /// <summary>A conductor's rest chain (WiresNear; static until 1.9): a capstan of its strand.</summary>
        Wire = 8,
        /// <summary>One of the drone's own contact shapes (a skid, a leg, an arm, a can, a pack, the coil): a moving capstan, its
        /// reaction on the drone at the contact point.</summary>
        Part = 9,
        /// <summary>The spool's exit lip (p7d-tether 1.8: a bend, a break or a link loss there; never a contact slot).</summary>
        Lip = 10,
        /// <summary>The pack's peel front inside the canister (1.8: a break there).</summary>
        Peel = 11,
        /// <summary>A rotor's disc (1.8: a prop cut; Part the rotor's part, Shape its disc shape).</summary>
        Disc = 12,
        /// <summary>The line's free span (1.8: a tension break between contacts, or the paid-out length's attenuation).</summary>
        Span = 13,
    }

    /// <summary>
    /// The fiber's contacts (p7d-tether design 6 and 12; PHY-18, PHY-19): what each node of the line meets and how, set on the
    /// chain's two contact slots a node before each step, and what they did after it.
    /// - The ground: SampleGround's batch (FiberLine), the plane within span.ground_reach_m with its surface's friction (the
    ///   coating as a polymer part on surfaces.json's set at the flight's moisture). Over a hole (a patch, a trench) the plane
    ///   is absent and the soil shapes there are the ground (IWorldQuery's rule).
    /// - Shapes: each segment a capsule of the line's radius against StaticContacts (wires left to WiresNear), only while its
    ///   clearance certificate requires it: a query that finds nothing within contact.clearance_margin_m certifies the segment
    ///   clear while both its ends stay within that margin less the line's radius of the certified core (the distance from a
    ///   segment is convex, so a piece whose ends lie near the core lies near it everywhere). The homogenised twig, foliage
    ///   and razor_wire shells are skipped: the line reads their elements and records. A soil shape or a layer with an area
    ///   law is ground (lain on, held by Coulomb's friction); any other shape a capstan of its face. A node meets a shape a
    ///   segment's query found by the shape's own nearest point to it (StaticContacts on the node), so past a face's end it
    ///   meets the arris or nothing, never the face's plane.
    /// - The near lists (w3-farmland-ground 2.1, L6): a segment the certificate does not clear (one resting on a shape, or
    ///   within the margin of one) is answered from the shapes a query at the margin plus NearSkin found near a core it lay on,
    ///   the ground left out, while its ends stay within NearSkin of that core; only the listed shapes its bounds leave in doubt
    ///   are re-asked, one by one (ShapeContact), and so are a node's shapes. Every answer is the query's, to the bit: where a
    ///   list cannot prove it (the query's buffer could overflow, the list's own did), the query answers.
    /// - Elements, snag records and conductors: from caches (the drone's, contact.drone_cache_radius_m, and up to two at the
    ///   line's lowest touching nodes, contact.line_cache_radius_m) asked for on a refresh and swapped in exactly
    ///   contact.cache_swap_steps later (api-review §6), so a replay swaps at the same step with the same bits; each step every
    ///   segment within a cache's reach is tested against its members through a 0.25 m grid built at the swap. The query runs
    ///   at the request (the worker's job done in the flight thread; its result is the same, used at the same step).
    /// - The drone's parts: every contact shape but the discs, at the step's end pose, moving with the body; the exit's segment
    ///   skips the coil (the lip's capstan is its turn there). A part is a capstan that glides: its hold slips along the line
    ///   once the pull passes what the wrap holds, and a step whose wrap could not bring the node to the part's speed leaves it
    ///   free along the line (CableContact.Glide and Free), so a part sliding past a straight line never drags it. A member's
    ///   touch is predicted from the node's own motion (CableContact.Kinematic). The reactions act on the drone in the next step
    ///   (design 2's lag).
    /// - A member meeting a segment between its nodes puts a node there (CableChain.SplitAt; momentum kept); a node touching
    ///   two members takes both in the same solve (the wedge). Members of one object whose normals at the node lie within 45°
    ///   merge into the nearest (p7a-ground design 5's rule, carried to the line), so a seam or a record laid over a shape is
    ///   never counted twice.
    /// - Every bend is known: at each pressing contact R_eff = max(R_edge + c, λ/(2·sin(θ/4))), λ = √(EI/T), θ the line's turn
    ///   there, T the larger tension beside it (the elastica over a point edge, the fortifications reading §2.1), σ = E·r/R_eff
    ///   + T/A_g; MinBendRadius is the least over the line (FiberMinBendRadius).
    /// Engine-free, + − × ÷ √ and DetMath, preallocated: 0 B a step.
    /// </summary>
    public sealed class FiberContacts
    {
        // Numerical values (labelled): a fixed member's penalty spring a node, stiff against the line's loads (1 N presses
        // 0.1 mm) and critically damped on the node's mass, implicit in the chain; the gap within which a member takes a slot
        // (the line's travel in a step at 50 m/s), so the chain's speculative contact meets it in the step it is reached; the
        // least piece a contact node leaves either side of it (5 mm, or the member's and the line's radii twice).
        const double MemberStiffness = 1e4;
        public const double InsertGap = 0.05;
        // A member already on a slot keeps it to twice that (numerical hysteresis: a slot does not flicker at the edge).
        public const double KeepGap = 2 * InsertGap;
        const double LeastPiece = 0.005;
        const double CellSize = 0.25;
        // A box test's slack, m (numerical: far above the rounding of a closest approach's points a kilometre out, 1e-13 m).
        const double BoxSlack = 1e-9;
        const int ShapeBuffer = 64, MaxHits = 2048, MaxSpent = 256, MaxReactions = 64;
        const double Cos45 = 0.7071067811865476;

        // The microbend's surface class (p7d-tether 1.8, design 8; tether.json's link.microbend_rough_db_per_n and _smooth_): the
        // physics_materials.json rows a line meets as a rough contact (millimetre-scale roughness: soil, bark, concrete, straw,
        // fabric, the table's rationale), assumed by row; every other row is smooth (metal, polymer, glass, a conductor). Load-time.
        static readonly string[] RoughMaterials = { "masonry", "concrete", "timber", "bark", "twig", "deadwood", "asbestos_cement", "straw", "fabric", "sandbag", "foliage" };

        /// <summary>The object a drone part's contact names (FiberContact's Object): the drone is one body, so its shapes merge
        /// by the 45° rule as one object's.</summary>
        public const int DroneObject = -2;

        // The ground's springs (FiberLine's, labelled there).
        readonly double _groundPerMetre, _groundHold;

        readonly FlightWorld _world;
        readonly ContactModel _parts;
        readonly int _coilPart;
        readonly double _radius, _massPerMetre;
        readonly double _margin, _reach, _slideShare, _hookReach;
        readonly double _youngs, _glassRadius, _coating, _bending, _glassArea;
        readonly long _swap;
        readonly double _dt;
        readonly double _polymerStatic, _polymerKinetic, _aluminiumStatic, _aluminiumKinetic;

        // The world's materials and surfaces by id: μs, μk, the arris, skipped (a homogenised shell), a layer.
        readonly double[] _matStatic, _matKinetic, _matEdge;
        readonly bool[] _matSkip, _matLayer, _matRough;
        readonly double[] _surfStatic, _surfKinetic, _surfEdge;
        // The elements' tissue (surfaces.json's cover at the flight's moisture) and their friction (the twig and straw rows).
        readonly CoverSet _straw, _stem, _twig;
        readonly bool _cover;
        readonly double _twigStatic, _twigKinetic, _strawStatic, _strawKinetic;

        // The drone's parts: their friction and reach from the body origin.
        readonly double[] _partStatic, _partKinetic;
        readonly double _partReach;
        // The parts' world segments at this step's pose (PlaceParts), set once a Prepare at the first segment within reach
        // rather than at every segment: the same products, so the same bits. Each part's box in the body's axes (its ends, grown
        // by its radius, the line's, KeepGap and PartCull) passes over a part no segment's box in those axes reaches.
        readonly Double3[] _partA, _partB, _partLow, _partHigh;
        readonly double[] _partRadius;
        readonly int[] _partOf;
        readonly bool[] _partSkip;
        bool _partsPlaced;
        // Numerical: the boxes' slack, m, far above the rounding of a body-axis point within a kilometre (1e-13 m).
        const double PartCull = 1e-6;

        // The near lists (w3-farmland-ground 2.1): a segment the certificate does not clear is answered from the list of the
        // shapes within the margin plus NearSkin of a core it lay on (one StaticContacts at that reach, the ground left out),
        // while both its ends stay within NearSkin of that core, each listed shape re-asked alone (ShapeContact) only where
        // its listed distance less the segment's move from the core could put it within KeepGap or across the margin. The
        // skin (assumed, m): a resting segment keeps its list for good, a line dragged at 20 m/s past shapes rebuilds it
        // about once in 12 steps. The slack (numerical, m): far above a closest approach's rounding a kilometre out (a
        // cylinder's search reads a few nm long), so no bound below is ever decided by rounding. The capacity (assumed): four
        // times the query's own buffer; a reach holding more takes the full query until its segment leaves that core.
        const double NearSkin = 0.25, NearSlack = 1e-6;
        const int NearCapacity = 256;
        readonly Double3[] _nearA, _nearB;     // each slot's core
        readonly int[] _nearCount;             // its shapes, −1 for more than NearCapacity
        readonly long[] _nearSeen;             // the last Prepare whose chain named it (a slot no node names is free)
        long _nearStamp;
        readonly int[] _nearObj, _nearShape;   // its shapes in canonical order, NearCapacity a slot
        readonly double[] _nearDist;           // their Distance from the core then
        readonly bool[] _nearSkip;             // a homogenised shell (never a member, but counted in the buffer)
        readonly StaticContact[] _nearFound = new StaticContact[NearCapacity];
        // Per segment this step: its shapes that may lie within KeepGap of it (a node's guard: below the buffer, the node's
        // own query could not have dropped one), −1 where the full query answered.
        readonly int[] _segNear;

        readonly FiberCache[] _caches;
        readonly StaticContact[] _shapes = new StaticContact[ShapeBuffer];
        readonly Hit[] _hits = new Hit[MaxHits];
        int _hitCount;
        // AddHit's table of the current segment's hits by member (open addressing, twice MaxHits; a slot is the segment's while
        // its generation is _hitGeneration, which Prepare advances at each segment).
        const int HitTable = 2 * MaxHits;
        readonly long[] _hitGen = new long[HitTable];
        readonly int[] _hitAt = new int[HitTable];
        long _hitGeneration;
        // The hits by segment (Prepare adds a segment's hits together: [_segHits[j], _segHitsEnd[j])) and by node (each node's
        // hits in the list's order, _nodeHit[i] then _nextHit[h], −1 ending it): the same hits in the same order as a scan of
        // the whole list, without the scan (a line lying on the drone's parts has hundreds, and every segment and node read them).
        readonly int[] _segHits, _segHitsEnd, _nodeHit;
        readonly int[] _nextHit = new int[MaxHits];
        readonly double[] _split;
        readonly int[] _splitsBefore;
        readonly Candidate[] _cand = new Candidate[16];
        // The shapes asked alone at the current node of Assign (Evaluate), with their answers.
        readonly StaticContact[] _asked = new StaticContact[16];
        readonly int[] _askedObj = new int[16], _askedShape = new int[16];
        readonly bool[] _askedFound = new bool[16];
        int _askedCount;
        readonly ulong[] _spent = new ulong[MaxSpent];
        readonly byte[] _spentKind = new byte[MaxSpent];
        readonly bool[] _spentSeen = new bool[MaxSpent];
        int _spentCount;

        // The drone's reactions for the next step: world force and body point.
        readonly Double3[] _reactionForce = new Double3[MaxReactions], _reactionPoint = new Double3[MaxReactions];
        int _reactionCount;

        public FiberContacts(TetherTable table, FlightSetup setup, FlightWorld world, ContactModel parts, double radius, double massPerMetre,
            double groundPerMetre, double groundHold, double dt)
        {
            _dt = dt;
            _world = world;
            _parts = parts;
            _coilPart = setup.TetherCoilPart;
            _radius = radius;
            _massPerMetre = massPerMetre;
            _groundPerMetre = groundPerMetre;
            _groundHold = groundHold;
            _margin = table.Number("contact.clearance_margin_m");
            _reach = table.Number("span.ground_reach_m");
            _slideShare = table.Number("contact.stem_slide_off_share");
            _hookReach = table.Number("contact.hook_reach_m");
            _swap = (long)table.Number("contact.cache_swap_steps");
            _youngs = table.Number("fiber.youngs_modulus_pa");
            _glassRadius = table.Number("fiber.glass_diameter_m") / 2;
            _coating = table.Number("fiber.coating_thickness_m");
            _bending = table.Number("fiber.bending_stiffness_n_m2");
            _glassArea = table.Number("fiber.glass_area_m2");
            _polymerStatic = table.Number("span.ground_static_friction");
            _polymerKinetic = table.Number("span.ground_kinetic_friction");
            _aluminiumStatic = table.Number("contact.aluminium_static_friction");
            _aluminiumKinetic = table.Number("contact.aluminium_kinetic_friction");

            int nodes = (int)table.Number("span.max_nodes");
            _split = new double[nodes];
            _splitsBefore = new int[nodes + 1];
            _segHits = new int[nodes];
            _segHitsEnd = new int[nodes];
            _nodeHit = new int[nodes];
            _segNear = new int[nodes];
            // Two slots a node: every node names at most one, and each segment rebuilds at most once a Prepare.
            int slots = 2 * nodes;
            _nearA = new Double3[slots];
            _nearB = new Double3[slots];
            _nearCount = new int[slots];
            _nearSeen = new long[slots];
            _nearObj = new int[slots * NearCapacity];
            _nearShape = new int[slots * NearCapacity];
            _nearDist = new double[slots * NearCapacity];
            _nearSkip = new bool[slots * NearCapacity];

            // The world's materials: their rows' μ (wet in rain where a row has a wet set), arris and kind.
            bool wet = setup.Rain != RainLevel.Sunny;
            ContactSpec spec = setup.Contacts;
            IWorldQuery q = world.Query;
            int top = 0;
            for (int id = 1; id < ushort.MaxValue; id++)
                if (q.MaterialName((ushort)id) != null) top = id;
            _matStatic = new double[top + 1];
            _matKinetic = new double[top + 1];
            _matEdge = new double[top + 1];
            _matSkip = new bool[top + 1];
            _matLayer = new bool[top + 1];
            _matRough = new bool[top + 1];
            for (int id = 0; id <= top; id++)
            {
                _matStatic[id] = _polymerStatic;
                _matKinetic[id] = _polymerKinetic;
                string name = id > 0 ? q.MaterialName((ushort)id) : null;
                if (name == null) continue;
                _matSkip[id] = name == "twig" || name == "foliage" || name == "razor_wire";
                _matRough[id] = Array.IndexOf(RoughMaterials, name) >= 0;
                if (spec == null || !spec.Row(name, out MaterialRow r)) continue;
                _matStatic[id] = wet && r.HasWet ? r.WetStaticFriction : r.StaticFriction;
                _matKinetic[id] = wet && r.HasWet ? r.WetKineticFriction : r.KineticFriction;
                _matEdge[id] = r.EdgeRadius;
                _matLayer[id] = r.AreaLaw != AreaLaw.None;
            }
            _twigStatic = _strawStatic = _polymerStatic;
            _twigKinetic = _strawKinetic = _polymerKinetic;
            if (spec != null && spec.Row("twig", out MaterialRow tw))
            {
                _twigStatic = wet && tw.HasWet ? tw.WetStaticFriction : tw.StaticFriction;
                _twigKinetic = wet && tw.HasWet ? tw.WetKineticFriction : tw.KineticFriction;
            }
            if (spec != null && spec.Row("straw", out MaterialRow st))
            {
                _strawStatic = wet && st.HasWet ? st.WetStaticFriction : st.StaticFriction;
                _strawKinetic = wet && st.HasWet ? st.WetKineticFriction : st.KineticFriction;
            }

            // The surfaces: their set's μ at the flight's moisture (the drone's polymer pair, the coating as a polymer part) and
            // the lip's edge; without surfaces.json the span's polymer pair.
            _surfStatic = new double[256];
            _surfKinetic = new double[256];
            _surfEdge = new double[256];
            Moisture moisture = SurfaceTable.MoistureOf(setup.Rain);
            for (int id = 0; id < 256; id++)
            {
                _surfStatic[id] = _polymerStatic;
                _surfKinetic[id] = _polymerKinetic;
                string name = id > 0 ? q.SurfaceName((byte)id) : null;
                if (name == null || spec?.Surfaces == null) continue;
                foreach (SurfaceRow row in spec.Surfaces.Surfaces)
                {
                    if (row.Name != name) continue;
                    SoilSet set = row.Set(moisture);
                    _surfStatic[id] = set.StaticFriction;
                    _surfKinetic[id] = set.KineticFriction;
                    _surfEdge[id] = set.LipEdgeRadius;
                }
            }
            if (spec?.Surfaces != null && spec.Surfaces.Cover.Length == 4)
            {
                _cover = true;
                _straw = spec.Surfaces.Cover[(int)CoverKind.Straw].Set(moisture);
                _stem = spec.Surfaces.Cover[(int)CoverKind.Stem].Set(moisture);
                _twig = spec.Surfaces.Cover[(int)CoverKind.Twig].Set(moisture);
            }

            // The drone's parts: polymer the span's pair (the coating on a polymer part), carbon its row, aluminium the table's.
            if (parts != null)
            {
                int count = parts.ShapeCount;
                _partStatic = new double[count];
                _partKinetic = new double[count];
                _partA = new Double3[count];
                _partB = new Double3[count];
                _partLow = new Double3[count];
                _partHigh = new Double3[count];
                _partSkip = new bool[count];
                _partRadius = new double[count];
                _partOf = new int[count];
                for (int i = 0; i < count; i++)
                {
                    ContactShape p = parts.Shape(i);
                    _partStatic[i] = _polymerStatic;
                    _partKinetic[i] = _polymerKinetic;
                    if (p.Material == "aluminium")
                    {
                        _partStatic[i] = _aluminiumStatic;
                        _partKinetic[i] = _aluminiumKinetic;
                    }
                    else if (p.Material == "carbon_composite" && spec != null && spec.Row("carbon_composite", out MaterialRow c))
                    {
                        _partStatic[i] = c.StaticFriction;
                        _partKinetic[i] = c.KineticFriction;
                    }
                    double reach = DetMath.Max(p.A.Length(), p.B.Length()) + p.Radius;
                    if (p.Role != ContactRole.Disc && reach > _partReach) _partReach = reach;
                }
            }

            int elements = (int)table.Number("contact.element_capacity"), snags = (int)table.Number("contact.snag_capacity");
            int wires = (int)table.Number("contact.wire_capacity");
            double drone = table.Number("contact.drone_cache_radius_m"), droneRefresh = table.Number("contact.drone_cache_refresh_m");
            double line = table.Number("contact.line_cache_radius_m"), lineRefresh = table.Number("contact.line_cache_refresh_m");
            _caches = new[]
            {
                new FiberCache(drone, droneRefresh, elements, snags, wires),
                new FiberCache(line, lineRefresh, elements, snags, wires),
                new FiberCache(line, lineRefresh, elements, snags, wires),
            };
            foreach (FiberCache c in _caches) c.Owner = this;
        }

        // ---- what the tests and the log read ----

        /// <summary>The least bend radius over the line's pressing contacts after the last step (FiberMinBendRadius), m; +∞ for
        /// none. Its node, its member's kind and the bend's θ, T and σ there.</summary>
        public double MinBendRadius { get; private set; } = double.PositiveInfinity;
        public int MinBendNode { get; private set; } = -1;
        public FiberMember MinBendMember { get; private set; }
        public double MinBendTurn { get; private set; }
        public double MinBendTension { get; private set; }
        public double MinBendStress { get; private set; }

        /// <summary>The nodes with a pressing contact after the last step.</summary>
        public int Touching { get; private set; }

        /// <summary>The whole StaticContacts queries of the last step and of the flight: a near list built, a segment or a node
        /// the lists could not answer.</summary>
        public int Queries { get; private set; }
        public long QueriesTotal { get; private set; }

        /// <summary>The one-shape contacts (ShapeContact) the near lists asked for in the last step and in the flight.</summary>
        public int Checks { get; private set; }
        public long ChecksTotal { get; private set; }

        /// <summary>The near lists answer what they can (on, the default). Off, every segment the certificate does not clear
        /// takes the whole query, as before 2.1: the differential tests' reference, which must give the same bits.</summary>
        public bool NearLists { get; set; } = true;

        /// <summary>The segments the last step examined against the world's shapes (by a query or a near list), each named by
        /// its two nodes' material coordinates then, the exit's as +∞ (the exit's grows with the payout; forensics and the
        /// certificate's scenario).</summary>
        public int QueriedCount { get; private set; }
        public double QueriedMaterial(int k) => _queried[k];
        public double QueriedNext(int k) => _queriedNext[k];
        readonly double[] _queried = new double[64], _queriedNext = new double[64];

        /// <summary>Contact nodes put into segments (CableChain.SplitAt), and those refused (the chain full or the piece too
        /// short), so the contact went to the nearer end.</summary>
        public long Inserted { get; private set; }
        public long Refused { get; private set; }

        /// <summary>The caches (0 the drone's, 1 and 2 the line's): their centre, whether one is in use, and its members.</summary>
        public int CacheCount => _caches.Length;
        public FiberCache Cache(int i) => _caches[i];

        /// <summary>The drone's reactions to the line on its parts after the last step: world N at body m, applied in the next.</summary>
        public int ReactionCount => _reactionCount;

        /// <summary>No reactions for the next step (1.8: nothing of the line is left on the drone after a break at it).</summary>
        public void ClearReactions() => _reactionCount = 0;
        public Double3 ReactionForce(int i) => _reactionForce[i];
        public Double3 ReactionPoint(int i) => _reactionPoint[i];

        /// <summary>Design 6's bend: R_eff = max(R_edge + c, √(EI/T)/(2·sin(θ/4))) at a turn θ (rad) under T (N) over an arris of
        /// edge (m); +∞ with no tension or no turn.</summary>
        public double BendRadius(double edge, double theta, double tension)
        {
            if (!(tension > 0) || !(theta > 0)) return double.PositiveInfinity;
            DetMath.SinCos(theta / 4, out double sin, out _);
            double elastica = DetMath.Sqrt(_bending / tension) / (2 * sin);
            double cap = edge + _coating;
            return elastica > cap ? elastica : cap;
        }

        /// <summary>The glass's local stress at the bend, σ = E·r/R_eff + T/A_g, Pa.</summary>
        public double BendStress(double rEff, double tension) =>
            (rEff < double.PositiveInfinity ? _youngs * _glassRadius / rEff : 0) + (tension > 0 ? tension / _glassArea : 0);

        // ---- before the step: the members near each segment, the contact nodes they need ----

        /// <summary>
        /// Before the step's loads: refreshes the caches (the drone's at its origin, the line's at its lowest touching nodes),
        /// gathers every member within InsertGap of each segment (the world's shapes under the certificate, the caches' members
        /// through their grids, the drone's parts at s) and puts a contact node where a member meets a segment between its nodes.
        /// </summary>
        public void Prepare(CableChain chain, in FlightState s, StepEvents events)
        {
            long step = s.Step;
            for (int k = 0; k < _spentCount; k++) _spentSeen[k] = false;
            RefreshCaches(chain, s, step, events);

            _hitCount = 0;
            _partsPlaced = false;
            Queries = 0;
            Checks = 0;
            QueriedCount = 0;
            int n = chain.Count;
            // The slots this chain's nodes name are in use; every other is free for a list built this Prepare.
            _nearStamp++;
            for (int i = 0; i < n; i++)
                if (chain[i].Near > 0) _nearSeen[chain[i].Near - 1] = _nearStamp;
            Quat q = s.Attitude;
            for (int j = 0; j + 1 < n; j++)
            {
                _split[j] = -1;
                _segHits[j] = _hitCount;
                _hitGeneration++;
                Double3 a = chain[j].Position, b = chain[j + 1].Position;
                ShapeHits(chain, j, a, b, events);
                for (int c = 0; c < _caches.Length; c++)
                    if (_caches[c].Valid) CacheHits(_caches[c], j, a, b, events);
                if (_parts != null) PartHits(j, a, b, j + 2 == n, s, q);
                _segHitsEnd[j] = _hitCount;
            }
            // A spent member (a released hook, a stem the line slid off) is met again once the line has left it.
            for (int k = 0; k < _spentCount;)
            {
                if (_spentSeen[k])
                {
                    k++;
                    continue;
                }
                _spentCount--;
                _spent[k] = _spent[_spentCount];
                _spentKind[k] = _spentKind[_spentCount];
                _spentSeen[k] = _spentSeen[_spentCount];
            }

            // The contact nodes: the nearest hit within InsertGap of a segment's inside takes a node there, from the far end so
            // the indices below stay; the hits then name their nodes.
            for (int j = 0; j + 1 < n; j++)
            {
                double best = InsertGap, u = -1;
                double len = (chain[j + 1].Position - chain[j].Position).Length();
                for (int h = _segHits[j]; h < _segHitsEnd[j]; h++)
                {
                    ref Hit x = ref _hits[h];
                    if (!(x.Gap < best)) continue;
                    double piece = 2 * (x.Radius + _radius);
                    if (piece < LeastPiece) piece = LeastPiece;
                    if (!(x.U * len > piece) || !((1 - x.U) * len > piece)) continue;
                    best = x.Gap;
                    u = x.U;
                }
                _split[j] = u;
            }
            for (int j = n - 2; j >= 0; j--)
            {
                if (!(_split[j] > 0)) continue;
                if (chain.SplitAt(j, _split[j])) Inserted++;
                else
                {
                    Refused++;
                    _split[j] = -1;
                }
            }
            int before = 0;
            for (int j = 0; j + 1 < n; j++)
            {
                _splitsBefore[j] = before;
                if (_split[j] > 0) before++;
            }
            int last = chain.Count - 1;
            for (int h = 0; h < _hitCount; h++)
            {
                ref Hit x = ref _hits[h];
                int j = x.Seg, at = j + _splitsBefore[j];
                double cut = _split[j];
                int node;
                if (cut > 0)
                {
                    // The three nodes j, the new one and j + 1 at u 0, cut and 1: the nearest.
                    double d0 = x.U, d1 = DetMath.Abs(x.U - cut), d2 = 1 - x.U;
                    node = d1 <= d0 && d1 <= d2 ? at + 1 : d0 <= d2 ? at : at + 2;
                }
                else node = x.U <= 0.5 ? at : at + 1;
                x.Node = node;
                // The exit is on the drone (the lip is its wrap) and an anchored trail end on its point: neither takes one.
                if (node == last || node == 0 && chain[0].AnchorStiffness > 0) x.Node = -1;
            }
            for (int i = 0; i <= last; i++) _nodeHit[i] = -1;
            for (int h = _hitCount - 1; h >= 0; h--)
            {
                int node = _hits[h].Node;
                if (node < 0) continue;
                _nextHit[h] = _nodeHit[node];
                _nodeHit[node] = h;
            }
        }

        // The world's shapes near segment j, if its certificate has run out: what a query at the margin finds, the certificate
        // issued when it finds nothing (the ground, which the batch carries, and the skipped shells aside). Its near list
        // answers when it can, to the bit; else the query itself.
        void ShapeHits(CableChain chain, int j, Double3 a, Double3 b, StepEvents events)
        {
            ref CableNode na = ref chain[j];
            _segNear[j] = -1;
            double allow = _margin - _radius;
            if (na.Cleared && PointSegment(a, na.ClearA, na.ClearB) < allow * allow && PointSegment(b, na.ClearA, na.ClearB) < allow * allow)
                return;
            if (QueriedCount < _queried.Length)
            {
                _queried[QueriedCount] = na.Material;
                _queriedNext[QueriedCount++] = j + 2 == chain.Count ? double.PositiveInfinity : chain[j + 1].Material;
            }
            var cap = new Capsule(a, b, _radius);
            if (!NearLists || !NearHits(ref na, j, cap)) QueryHits(ref na, j, cap, events);
        }

        // The near list's answer for segment j (cap), or false where only the query can give it. Every shape within the margin
        // of the segment is on the list while both its ends lie within NearSkin of the list's core (the distance from a segment
        // is convex, so the whole segment lies within its ends' larger distance d1 of the core; a shape's Distance from it is then
        // at least its Distance from the core less d1, and at most that plus d2, the core's ends' larger distance from the
        // segment, or d2 for a shape the core met). Each listed shape whose bound leaves it possibly within KeepGap is re-asked
        // (ShapeContact: the query's own contact for it), so its hit is the query's to the bit, in the query's (canonical)
        // order. The rest decide the certificate: one surely within the margin denies it, and only where none is sure are those
        // whose bound straddles the margin re-asked. Where the query's buffer could overflow (its ground and every listed shape
        // possibly within the margin reaching ShapeBuffer), the query answers, as it would have.
        bool NearHits(ref CableNode na, int j, in Capsule cap)
        {
            Double3 a = cap.A, b = cap.B;
            int slot = na.Near - 1;
            double room = NearSkin - 2 * NearSlack;
            if (slot >= 0 && !(DetMath.Max(PointSegment(a, _nearA[slot], _nearB[slot]), PointSegment(b, _nearA[slot], _nearB[slot])) <= room * room))
                slot = -1;
            if (slot < 0)
            {
                for (int k = 0; k < _nearSeen.Length && slot < 0; k++)
                    if (_nearSeen[k] != _nearStamp) slot = k;
                if (slot < 0) return false;
                _nearSeen[slot] = _nearStamp;
                na.Near = slot + 1;
                int found = _world.StaticContacts(cap, cap, _margin + NearSkin, _nearFound, QueryFilter.NoWires | QueryFilter.NoGround);
                Queries++;
                QueriesTotal++;
                _nearA[slot] = a;
                _nearB[slot] = b;
                _nearCount[slot] = found > NearCapacity ? -1 : found;
                for (int i = 0, e = slot * NearCapacity; i < found && i < NearCapacity; i++, e++)
                {
                    ref StaticContact c = ref _nearFound[i];
                    _nearObj[e] = c.Object;
                    _nearShape[e] = c.Shape;
                    _nearDist[e] = c.Distance;
                    _nearSkip[e] = c.Surface == WorldIds.NoSurface && c.Material < _matSkip.Length && _matSkip[c.Material];
                }
            }
            int count = _nearCount[slot];
            if (count < 0) return false;
            Double3 ca = _nearA[slot], cb = _nearB[slot];
            double d1 = DetMath.Sqrt(DetMath.Max(PointSegment(a, ca, cb), PointSegment(b, ca, cb))) + NearSlack;
            double d2 = DetMath.Sqrt(DetMath.Max(PointSegment(ca, a, b), PointSegment(cb, a, b))) + NearSlack;
            int first = slot * NearCapacity, end = first + count;
            int possible = 0, nearKeep = 0;
            for (int e = first; e < end; e++)
            {
                double low = _nearDist[e] - d1;
                if (low <= _margin) possible++;
                if (low < KeepGap + NearSlack) nearKeep++;
            }
            if (possible >= ShapeBuffer)
            {
                // The query's buffer may overflow, and then the query re-asks at KeepGap: the same hits (each shape within
                // KeepGap, its same contact, in the same order) while that one cannot overflow, and no certificate. So the list
                // answers where a listed shape is surely within the margin (no certificate either way).
                bool sure = false;
                for (int e = first; e < end && !sure; e++)
                    sure = !_nearSkip[e] && (_nearDist[e] > 0 ? _nearDist[e] : 0) + d2 < _margin;
                if (!sure || nearKeep >= ShapeBuffer) return false;
            }
            _segNear[j] = nearKeep;

            bool clear = true, straddle = false;
            for (int e = first; e < end; e++)
            {
                if (_nearSkip[e]) continue;
                double low = _nearDist[e] - d1;
                if (low > _margin) continue;
                if (low >= KeepGap)
                {
                    if ((_nearDist[e] > 0 ? _nearDist[e] : 0) + d2 < _margin) clear = false;
                    else straddle = true;
                    continue;
                }
                Checks++;
                ChecksTotal++;
                if (!_world.ShapeContact(_nearObj[e], _nearShape[e], cap, cap, _margin, out StaticContact c)) continue;
                clear = false;
                if (c.Distance < KeepGap) ShapeHit(j, a, b, c);
            }
            for (int e = first; e < end && clear && straddle; e++)
            {
                if (_nearSkip[e]) continue;
                double low = _nearDist[e] - d1;
                if (low > _margin || low < KeepGap) continue;
                Checks++;
                ChecksTotal++;
                if (_world.ShapeContact(_nearObj[e], _nearShape[e], cap, cap, _margin, out _)) clear = false;
            }
            na.Cleared = clear;
            if (clear)
            {
                na.ClearA = a;
                na.ClearB = b;
            }
            return true;
        }

        // The query at the margin for segment j (cap): its contacts' hits, the certificate when it finds nothing.
        void QueryHits(ref CableNode na, int j, in Capsule cap, StepEvents events)
        {
            Double3 a = cap.A, b = cap.B;
            int count = _world.StaticContacts(cap, cap, _margin, _shapes, QueryFilter.NoWires);
            Queries++;
            QueriesTotal++;
            bool overflow = count > ShapeBuffer;
            if (overflow)
            {
                count = _world.StaticContacts(cap, cap, KeepGap, _shapes, QueryFilter.NoWires);
                Queries++;
                QueriesTotal++;
                if (count > ShapeBuffer)
                {
                    FlightEvent bug = FlightEvent.Of(EventKind.WorldBug);
                    bug.Index = (byte)WorldBugCause.FiberShapesFull;
                    bug.A = count;
                    bug.Point = a;
                    events.Add(bug);
                    count = ShapeBuffer;
                }
            }
            bool clear = true;
            for (int i = 0; i < count; i++)
            {
                ref StaticContact c = ref _shapes[i];
                if (c.Object == WorldIds.Ground) continue;
                if (c.Surface == WorldIds.NoSurface && c.Material < _matSkip.Length && _matSkip[c.Material]) continue;
                clear = false;
                if (c.Distance < KeepGap) ShapeHit(j, a, b, c);
            }
            na.Cleared = clear && !overflow;
            if (na.Cleared)
            {
                na.ClearA = a;
                na.ClearB = b;
            }
        }

        // A shape's contact within KeepGap of segment j (a–b): its member, a layer (lain on) or a capstan of its face, and its hit.
        void ShapeHit(int j, Double3 a, Double3 b, in StaticContact c)
        {
            bool layer = c.Surface != WorldIds.NoSurface || c.Material < _matLayer.Length && _matLayer[c.Material];
            var m = new MemberSeg
            {
                Kind = layer ? FiberMember.Layer : FiberMember.Shape,
                Member = ((ulong)(uint)c.Object << 32) | (uint)c.Shape,
                Object = c.Object, Shape = c.Shape, Part = -1, Material = c.Material, Surface = c.Surface, Plane = true,
            };
            if (c.Surface != WorldIds.NoSurface)
            {
                m.Static = _surfStatic[c.Surface];
                m.Kinetic = _surfKinetic[c.Surface];
                m.Edge = _surfEdge[c.Surface];
            }
            else
            {
                m.Static = c.Material < _matStatic.Length ? _matStatic[c.Material] : _polymerStatic;
                m.Kinetic = c.Material < _matKinetic.Length ? _matKinetic[c.Material] : _polymerKinetic;
                m.Edge = ShapeEdge(c.Object, c.Shape, c.Material);
            }
            AddHit(j, ClosestOnSegment(c.Point, a, b), c.Distance, c.Point, c.Normal, m);
        }

        // The arris a rigid shape bends the line over: a sphere's or a capsule's own radius (round everywhere), else its row's
        // edge_radius_m (0 where null: a point edge, the worst).
        double ShapeEdge(int obj, int shape, ushort material)
        {
            if (_world.Geometry(obj, shape, out ShapeGeometry g) && (g.Kind == ShapeKind.Sphere || g.Kind == ShapeKind.Capsule)) return g.Radius;
            return material < _matEdge.Length ? _matEdge[material] : 0;
        }

        // The members of a cache within InsertGap of segment j, where the segment lies inside the cache's sure reach (a member
        // within InsertGap of a point that far in lies inside the cache).
        void CacheHits(FiberCache cache, int j, Double3 a, Double3 b, StepEvents events)
        {
            double sure = cache.Radius - KeepGap - _radius;
            if (!(sure > 0)) return;
            Double3 c0 = cache.Centre;
            if (!(PointSegment(c0, a, b) < sure * sure)) return;
            Double3 d = b - a;
            double len = d.Length();
            int pieces = (int)(len / CellSize) + 1;
            cache.Stamp++;
            // Two passes over pieces that cannot add a member (w3-farmland-ground 2.1; a long segment's thousand pieces walked the
            // cube's edge cells over and over): a piece whose cells are the last visited piece's (a piece outside the cube is
            // clamped onto its edge cells), and every piece after the pieces have met the cube and left it. The second is exact:
            // a member kept lies within its reach of the segment's point u, within `sure` of the centre so inside the cube; the
            // grid lists it in that point's cell (its bounds grown by more than its reach while the line's radius is under
            // FiberCache.LineReach); the piece holding u visits that cell; and the pieces meeting the cube are one run (each
            // coordinate is monotone along the segment). Nothing else a later piece finds is kept, so the hits and their order stay.
            bool met = false, leave = _radius < FiberCache.LineReach;
            int lx0 = -1, ly0 = -1, lz0 = -1, lx1 = -1, ly1 = -1, lz1 = -1;
            Double3 low = new Double3(DetMath.Min(a.X, b.X), DetMath.Min(a.Y, b.Y), DetMath.Min(a.Z, b.Z));
            Double3 high = new Double3(DetMath.Max(a.X, b.X), DetMath.Max(a.Y, b.Y), DetMath.Max(a.Z, b.Z));
            for (int p = 0; p < pieces; p++)
            {
                Double3 pa = a + d * ((double)p / pieces), pb = a + d * ((double)(p + 1) / pieces);
                if (leave)
                {
                    bool meets = cache.Meets(pa, pb);
                    if (met && !meets) break;
                    met |= meets;
                }
                cache.Cells(pa, pb, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1);
                if (x0 == lx0 && y0 == ly0 && z0 == lz0 && x1 == lx1 && y1 == ly1 && z1 == lz1) continue;
                lx0 = x0;
                ly0 = y0;
                lz0 = z0;
                lx1 = x1;
                ly1 = y1;
                lz1 = z1;
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            int cell = cache.Cell(x, y, z), from = cache.CellStart[cell], to = cache.CellStart[cell + 1];
                            for (int e = from; e < to; e++)
                            {
                                int k = cache.Entries[e];
                                if (cache.Seen[k] == cache.Stamp) continue;
                                cache.Seen[k] = cache.Stamp;
                                ref MemberSeg m = ref cache.Members[k];
                                double reach = m.Radius + _radius + KeepGap;
                                // Farther than its reach (and BoxSlack) on one axis: farther from the segment too, so the closest
                                // approach below would pass it over as well (w3-farmland-ground 2.1).
                                double grown = reach + BoxSlack;
                                if (DetMath.Min(m.C.X, m.D.X) - grown > high.X || DetMath.Max(m.C.X, m.D.X) + grown < low.X
                                    || DetMath.Min(m.C.Y, m.D.Y) - grown > high.Y || DetMath.Max(m.C.Y, m.D.Y) + grown < low.Y
                                    || DetMath.Min(m.C.Z, m.D.Z) - grown > high.Z || DetMath.Max(m.C.Z, m.D.Z) + grown < low.Z)
                                    continue;
                                double dist2 = SegmentSegment(a, b, m.C, m.D, out double u, out double t);
                                if (!(dist2 < reach * reach)) continue;
                                Double3 onLine = a + d * u, onMember = m.C + (m.D - m.C) * t;
                                if (!((onLine - c0).Length() < sure)) continue;
                                double dist = DetMath.Sqrt(dist2), gap = dist - m.Radius - _radius;
                                Double3 normal = dist > 0 ? (onLine - onMember) * (1 / dist) : new Double3(0, 1, 0);
                                if (Spent(m.Kind, m.Member, gap)) continue;
                                AddHit(j, u, gap, onMember + normal * m.Radius, normal, m);
                            }
                        }
            }
        }

        // The drone's parts within KeepGap of segment j, at the step's end pose. The exit's segment skips the coil: it leaves the
        // nozzle on the coil's cap, and its turn there is the lip's capstan e^(μ·θ_e), the spool's (design 3).
        void PartHits(int j, Double3 a, Double3 b, bool nearExit, in FlightState s, Quat q)
        {
            double reach = _partReach + KeepGap + _radius;
            if (!(PointSegment(s.Position, a, b) < reach * reach)) return;
            if (!_partsPlaced) PlaceParts(s, q);
            // The segment's box in the body's axes: a part outside it is beyond its reach (only the boxes' test, never a contact).
            Double3 ba = q.InverseRotate(a - s.Position), bb = q.InverseRotate(b - s.Position);
            Double3 low = new Double3(DetMath.Min(ba.X, bb.X), DetMath.Min(ba.Y, bb.Y), DetMath.Min(ba.Z, bb.Z));
            Double3 high = new Double3(DetMath.Max(ba.X, bb.X), DetMath.Max(ba.Y, bb.Y), DetMath.Max(ba.Z, bb.Z));
            for (int i = 0; i < _partSkip.Length; i++)
            {
                if (_partSkip[i]) continue;
                Double3 pl = _partLow[i], ph = _partHigh[i];
                if (!(low.X <= ph.X && pl.X <= high.X && low.Y <= ph.Y && pl.Y <= high.Y && low.Z <= ph.Z && pl.Z <= high.Z)) continue;
                int part = _partOf[i];
                if (nearExit && part == _coilPart) continue;
                Double3 pa = _partA[i], pb = _partB[i];
                double dist2 = SegmentSegment(a, b, pa, pb, out double u, out double t);
                double radius = _partRadius[i], near = radius + _radius + KeepGap;
                if (!(dist2 < near * near)) continue;
                Double3 onLine = a + (b - a) * u, onPart = pa + (pb - pa) * t;
                double dist = DetMath.Sqrt(dist2);
                Double3 normal = dist > 0 ? (onLine - onPart) * (1 / dist) : q.Rotate(new Double3(0, -1, 0));
                var m = new MemberSeg
                {
                    Kind = FiberMember.Part, Member = (ulong)i, Object = DroneObject, Shape = i, Part = part, C = pa, D = pb,
                    Radius = radius, Edge = radius, Static = _partStatic[i], Kinetic = _partKinetic[i],
                };
                // Each part once a segment, and no other source names a part: no earlier hit of the segment is this member.
                if (_hitCount < MaxHits)
                    _hits[_hitCount++] = new Hit { Seg = j, U = u, Gap = dist - radius - _radius, Point = onPart + normal * radius, Normal = normal, M = m, Radius = radius };
            }
        }

        // The parts' world segments at the step's pose, as PartHits read them (the discs and the parts off skipped), and each
        // one's box in the body's axes grown by its reach: a segment whose box misses it is farther than its reach from it.
        void PlaceParts(in FlightState s, Quat q)
        {
            _partsPlaced = true;
            for (int i = 0; i < _partSkip.Length; i++)
            {
                ContactShape p = _parts.Shape(i);
                _partSkip[i] = p.Role == ContactRole.Disc || _parts.ShapeOff(i);
                if (_partSkip[i]) continue;
                _partA[i] = s.Position + q.Rotate(p.A);
                _partB[i] = s.Position + q.Rotate(p.B);
                _partRadius[i] = p.Radius;
                _partOf[i] = p.Part;
                double g = p.Radius + _radius + KeepGap + PartCull;
                _partLow[i] = new Double3(DetMath.Min(p.A.X, p.B.X) - g, DetMath.Min(p.A.Y, p.B.Y) - g, DetMath.Min(p.A.Z, p.B.Z) - g);
                _partHigh[i] = new Double3(DetMath.Max(p.A.X, p.B.X) + g, DetMath.Max(p.A.Y, p.B.Y) + g, DetMath.Max(p.A.Z, p.B.Z) + g);
            }
        }

        void AddHit(int seg, double u, double gap, Double3 point, Double3 normal, in MemberSeg m)
        {
            // One hit a member a segment (a member in two caches, or two segments of one element or record): the nearest. The
            // segment's hits are found by their member in a table of this segment's generation (w3-farmland-ground 2.1: a line
            // lying in straw has hundreds a segment, and a scan back over them for each was quadratic); a segment holds at most
            // one hit a member, so it finds the one the scan found.
            int at = (int)((m.Member * 0x9E3779B97F4A7C15UL + (ulong)m.Kind) >> 52) & (HitTable - 1); // 12 bits: HitTable 4096
            for (; _hitGen[at] == _hitGeneration; at = (at + 1) & (HitTable - 1))
            {
                ref Hit o = ref _hits[_hitAt[at]];
                if (o.M.Kind != m.Kind || o.M.Member != m.Member) continue;
                if (gap < o.Gap)
                {
                    o.U = u;
                    o.Gap = gap;
                    o.Point = point;
                    o.Normal = normal;
                    o.M = m;
                }
                return;
            }
            if (_hitCount == MaxHits) return;
            _hitGen[at] = _hitGeneration;
            _hitAt[at] = _hitCount;
            _hits[_hitCount++] = new Hit { Seg = seg, U = u, Gap = gap, Point = point, Normal = normal, M = m, Radius = m.Plane ? 0 : m.Radius };
        }

        bool Spent(FiberMember kind, ulong member, double gap)
        {
            for (int k = 0; k < _spentCount; k++)
            {
                if (_spentKind[k] != (byte)kind || _spent[k] != member) continue;
                if (gap < 0) _spentSeen[k] = true;
                return true;
            }
            return false;
        }

        // ---- the caches (design 12; api-review §6) ----

        void RefreshCaches(CableChain chain, in FlightState s, long step, StepEvents events)
        {
            foreach (FiberCache c in _caches) c.Swap(step, events);
            // The drone's: at its origin.
            _caches[0].Follow(_world, s.Position, step, _swap);
            // The line's: at its two lowest touching nodes beyond the drone's cache's reach, a cache radius apart.
            int n = chain.Count, low0 = -1, low1 = -1;
            double outside = _caches[0].Radius - InsertGap;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref chain[i];
                bool touching = a.Contact0.Active && a.Contact0.NormalForce > 0 || a.Contact1.Active && a.Contact1.NormalForce > 0;
                if (!touching || (a.Position - s.Position).Length() < outside) continue;
                if (low0 < 0 || a.Position.Y < chain[low0].Position.Y)
                {
                    low1 = low0;
                    low0 = i;
                }
                else if (low1 < 0 || a.Position.Y < chain[low1].Position.Y) low1 = i;
            }
            if (low1 >= 0 && (chain[low1].Position - chain[low0].Position).Length() < _caches[1].Radius) low1 = -1;
            if (low0 >= 0) _caches[1].Follow(_world, chain[low0].Position, step, _swap);
            if (low1 >= 0) _caches[2].Follow(_world, chain[low1].Position, step, _swap);
            foreach (FiberCache c in _caches)
                if (c.Overflow != 0)
                {
                    FlightEvent bug = FlightEvent.Of(EventKind.WorldBug);
                    bug.Index = (byte)c.Overflow;
                    bug.A = c.OverflowCount;
                    bug.Point = c.Centre;
                    events.Add(bug);
                    c.Overflow = 0;
                }
        }

        // ---- the slots: each node's two nearest members ----

        /// <summary>
        /// After the step's loads, before the solve: node i's candidates (the plane ground from the batch, the hits naming it),
        /// members of one object within 45° merged into the nearest, the two nearest set on its slots: a member already on a slot
        /// is updated there (its hold and lock kept), a new one set afresh (Fresh, its first touch its Origin).
        /// </summary>
        public void Assign(CableChain chain, GroundSample[] ground, double[] groundHeight, Double3[] groundNormal, Double3[] groundAt, in FlightState s)
        {
            int n = chain.Count;
            Quat q = s.Attitude;
            Double3 w = q.Rotate(s.BodyRate);
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref chain[i];
                a.Contact0.Fresh = false;
                a.Contact1.Fresh = false;
                if (i == n - 1 || i == 0 && a.AnchorStiffness > 0)
                {
                    a.Contact0.Active = false;
                    a.Contact1.Active = false;
                    continue;
                }
                int count = 0;
                Double3 x = a.Position;
                double tributary = Tributary(chain, i);
                // The plane ground within the reach (absent over a hole: the shapes there are the ground).
                if ((ground[i].Flags & GroundFlags.Hole) == 0 && x.Y - groundHeight[i] < _reach)
                {
                    Double3 nrm = groundNormal[i];
                    Double3 point = groundAt[i] + nrm * _radius; // under the node, or on a step's face behind it (FiberLine)
                    _cand[count++] = new Candidate
                    {
                        M = new MemberSeg
                        {
                            Kind = FiberMember.Ground, Object = WorldIds.Ground, Part = -1, Surface = ground[i].Surface, Plane = true,
                            Static = _surfStatic[ground[i].Surface], Kinetic = _surfKinetic[ground[i].Surface], Edge = _surfEdge[ground[i].Surface],
                        },
                        Point = point, Normal = nrm, Gap = Double3.Dot(nrm, x - point),
                    };
                }
                int near = -1; // the shapes at node i (NodeShapes), queried at the first shape's hit that needs them
                _askedCount = 0; // the shapes asked alone at node i (Evaluate)
                for (int h = _nodeHit[i]; h >= 0 && count < _cand.Length; h = _nextHit[h])
                {
                    ref Hit hit = ref _hits[h];
                    ref Candidate c = ref _cand[count]; // evaluated in place: kept as a new candidate, or copied onto its like
                    if (!Evaluate(ref a, hit, ref near, ref c)) continue;
                    // A new member within InsertGap, one on a slot within KeepGap.
                    if (!(c.Gap < InsertGap) && !Holds(a.Contact0, c.M) && !Holds(a.Contact1, c.M)) continue;
                    // One candidate a member a node; members of one object within 45° of each other merge into the nearest.
                    int same = -1;
                    for (int k = 0; k < count; k++)
                    {
                        ref Candidate o = ref _cand[k];
                        if (o.M.Kind == c.M.Kind && o.M.Member == c.M.Member
                            || o.M.Object == c.M.Object && o.M.Object != WorldIds.Ground && Double3.Dot(o.Normal, c.Normal) >= Cos45)
                        {
                            same = k;
                            break;
                        }
                    }
                    if (same < 0) count++;
                    else if (c.Gap < _cand[same].Gap) _cand[same] = c;
                }
                // The two nearest (the first on a tie).
                int first = -1, second = -1;
                for (int k = 0; k < count; k++)
                {
                    if (first < 0 || _cand[k].Gap < _cand[first].Gap)
                    {
                        second = first;
                        first = k;
                    }
                    else if (second < 0 || _cand[k].Gap < _cand[second].Gap) second = k;
                }
                bool keep0 = false, keep1 = false;
                int put0 = -1, put1 = -1; // which candidate each slot takes
                for (int pick = 0; pick < 2; pick++)
                {
                    int k = pick == 0 ? first : second;
                    if (k < 0) continue;
                    ref Candidate c = ref _cand[k];
                    if (!keep0 && Holds(a.Contact0, c.M)) { keep0 = true; put0 = k; }
                    else if (!keep1 && Holds(a.Contact1, c.M)) { keep1 = true; put1 = k; }
                }
                for (int pick = 0; pick < 2; pick++)
                {
                    int k = pick == 0 ? first : second;
                    if (k < 0 || k == put0 || k == put1) continue;
                    if (put0 < 0) put0 = k;
                    else if (put1 < 0) put1 = k;
                }
                Set(ref a.Contact0, put0, keep0, a, tributary, x, s.Velocity, w, s.Position);
                Set(ref a.Contact1, put1, keep1, a, tributary, x, s.Velocity, w, s.Position);
            }
            // Each run of neighbouring nodes on gliding members is one wrap: it sticks or slips as one (the capstan composes).
            for (int i = 1; i + 1 < n;)
            {
                if (!Gliding(chain[i]))
                {
                    i++;
                    continue;
                }
                int first = i;
                while (i + 1 < n && Gliding(chain[i])) i++;
                Slips(chain, first, i - 1);
            }
        }

        static bool Gliding(in CableNode a) => a.Contact0.Active && a.Contact0.Glide || a.Contact1.Active && a.Contact1.Glide;

        // A run of nodes first … last on gliding members for this step (the predictive Coulomb rule, over the run as one wrap):
        // it sticks only if the wrap could bring its line to the members' speed along the line within the step,
        // Σ m·|Δv|/dt ≤ T_lo·(e^(μs·Σθ) − 1) (Σθ the run's turn, T_lo the lower of the tensions entering it, μs the members'
        // least); else the line slips by every node of it.
        void Slips(CableChain chain, int first, int last)
        {
            double turn = 0, need = 0, mu = double.PositiveInfinity;
            for (int i = first; i <= last; i++)
            {
                ref CableNode a = ref chain[i];
                Double3 da = a.Position - chain[i - 1].Position, db = chain[i + 1].Position - a.Position;
                turn += DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
                Double3 along = da + db;
                double l = along.Length();
                along = l > 0 ? along * (1 / l) : default;
                for (int k = 0; k < 2; k++)
                {
                    ref CableContact c = ref k == 0 ? ref a.Contact0 : ref a.Contact1;
                    if (!c.Active || !c.Glide) continue;
                    double slip = Double3.Dot(a.Velocity - c.Velocity, along);
                    need += a.Mass * (slip < 0 ? -slip : slip) / _dt;
                    if (c.StaticFriction < mu) mu = c.StaticFriction;
                    break;
                }
            }
            double t0 = chain.Tension(first - 1), t1 = chain.Tension(last), low = t0 < t1 ? t0 : t1;
            bool free = need > low * (DetMath.Exp(mu * turn) - 1);
            for (int i = first; i <= last; i++)
            {
                ref CableNode a = ref chain[i];
                if (a.Contact0.Active && a.Contact0.Glide) a.Contact0.Free = free;
                if (a.Contact1.Active && a.Contact1.Glide) a.Contact1.Free = free;
            }
        }

        static bool Holds(in CableContact c, in MemberSeg m) => c.Active && c.MemberKind == (byte)m.Kind && c.Member == m.Member;

        static double Tributary(CableChain chain, int i)
        {
            int n = chain.Count;
            double before = i > 0 ? chain[i].Material - chain[i - 1].Material : 0;
            double after = i + 1 < n ? chain[i + 1].Material - chain[i].Material : 0;
            return 0.5 * (before + after);
        }

        // The world's shapes within KeepGap of node x: StaticContacts on the node itself (a capsule of the line's radius with
        // no length), each its own nearest point and normal to the node. The count is kept to the buffer: over it only where
        // the segment's query, which reaches every shape this one does, already logged FiberShapesFull.
        int NodeShapes(Double3 x)
        {
            var cap = new Capsule(x, x, _radius);
            int count = _world.StaticContacts(cap, cap, KeepGap, _shapes, QueryFilter.NoWires);
            Queries++;
            QueriesTotal++;
            return count < ShapeBuffer ? count : ShapeBuffer;
        }

        // A hit's member at node a: its plane there, the gap; false for a shape the node is not within KeepGap of, and for a
        // straw the line lies over at its first touch (only one lying over the line hooks it). A shape's plane is its own face
        // at the node (its nearest point to the node, from near's NodeShapes), not the plane through the segment's nearest
        // point, which has no extent: kept for every node the segment's hit names, it held a node past the face's end on the
        // face's plane (E01's run, model 62: two trail points 0.30 m off a layer's side face, 0.28–0.56 m over the spoil).
        // A core member's plane is from its nearest point to the node. Where the hit's segment counted fewer shapes possibly
        // within KeepGap of it than the buffer holds (with the ground, the node's own query could not have dropped one: a
        // node on a segment is no nearer a shape than the segment), the shape alone is asked (ShapeContact, the same contact);
        // else the node's query, made once (near: its count, −1 until then).
        bool Evaluate(ref CableNode a, in Hit hit, ref int near, ref Candidate c)
        {
            c.M = hit.M;
            Double3 x = a.Position;
            if (hit.M.Plane)
            {
                int guard = _segNear[hit.Seg];
                StaticContact s;
                if (NearLists && guard >= 0 && guard < ShapeBuffer)
                {
                    // A shape both segments by the node name is asked once (the same question, the same answer).
                    int k = 0;
                    while (k < _askedCount && (_askedObj[k] != hit.M.Object || _askedShape[k] != hit.M.Shape)) k++;
                    if (k == _askedCount)
                    {
                        var cap = new Capsule(x, x, _radius);
                        Checks++;
                        ChecksTotal++;
                        bool found = _world.ShapeContact(hit.M.Object, hit.M.Shape, cap, cap, KeepGap, out s);
                        if (k < _asked.Length)
                        {
                            _askedObj[k] = hit.M.Object;
                            _askedShape[k] = hit.M.Shape;
                            _askedFound[k] = found;
                            _asked[k] = s;
                            _askedCount++;
                        }
                        if (!found) return false;
                    }
                    else
                    {
                        if (!_askedFound[k]) return false;
                        s = _asked[k];
                    }
                }
                else
                {
                    if (near < 0) near = NodeShapes(x);
                    int k = 0;
                    while (k < near && (_shapes[k].Object != hit.M.Object || _shapes[k].Shape != hit.M.Shape)) k++;
                    if (k == near) return false;
                    s = _shapes[k];
                }
                c.Normal = s.Normal;
                c.Point = s.Point + s.Normal * _radius;
                c.Gap = Double3.Dot(c.Normal, x - c.Point);
                return true;
            }
            Double3 cd = hit.M.D - hit.M.C;
            double l2 = Double3.Dot(cd, cd), t = l2 > 0 ? Double3.Dot(x - hit.M.C, cd) / l2 : 0;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            Double3 core = hit.M.C + cd * t, r = x - core;
            double dist = r.Length();
            Double3 nrm = dist > 0 ? r * (1 / dist) : hit.Normal;
            c.Normal = nrm;
            c.Point = core + nrm * (hit.M.Radius + _radius);
            c.Gap = dist - hit.M.Radius - _radius;
            c.M.Along = hit.M.Along + t * DetMath.Sqrt(l2);
            if (hit.M.Kind == FiberMember.Straw && !Holds(a.Contact0, hit.M) && !Holds(a.Contact1, hit.M) && !(core.Y > x.Y)) return false;
            return true;
        }

        // Slot c takes candidate k (or none): kept, its plane, springs and friction updated; else set afresh.
        void Set(ref CableContact slot, int k, bool kept, in CableNode node, double tributary, Double3 x, Double3 v, Double3 w, Double3 origin)
        {
            if (k < 0)
            {
                slot.Active = false;
                slot.Sliding = false;
                return;
            }
            ref Candidate c = ref _cand[k];
            ref MemberSeg m = ref c.M;
            Double3 velocity = m.Kind == FiberMember.Part ? v + Double3.Cross(w, c.Point - origin) : default;
            double stiffness, damping, hold;
            bool capstan, remesh;
            double cap = 0, slide = 0;
            switch (m.Kind)
            {
                case FiberMember.Ground:
                case FiberMember.Layer:
                    stiffness = _groundPerMetre * tributary;
                    damping = 2 * DetMath.Sqrt(_groundPerMetre * _massPerMetre) * tributary;
                    hold = _groundHold;
                    capstan = false;
                    remesh = true;
                    break;
                case FiberMember.Stem:
                {
                    // The cantilever 3EI/h³ at the contact's height h up the stem (never stiffer than a fixed member), letting go
                    // past the slide-off press or Euler's load π²EI/(4L²) along it (p7a-ground design 8).
                    double h = m.Along > 1e-3 ? m.Along : 1e-3;
                    stiffness = 3 * m.Bending / (h * h * h);
                    if (stiffness > MemberStiffness) stiffness = MemberStiffness;
                    damping = 2 * _stem.DampingRatio * DetMath.Sqrt(stiffness * node.Mass);
                    hold = MemberStiffness;
                    capstan = true;
                    remesh = false;
                    cap = 3.141592653589793 * 3.141592653589793 * m.Bending / (4 * m.Length * m.Length);
                    slide = _slideShare * h;
                    break;
                }
                case FiberMember.Straw:
                    // A hook's spring reaching its release force R over contact.hook_reach_m (p7a-ground design 8).
                    stiffness = m.Release / _hookReach;
                    damping = 2 * _straw.DampingRatio * DetMath.Sqrt(stiffness * node.Mass);
                    hold = MemberStiffness;
                    capstan = true;
                    remesh = false;
                    cap = m.Release;
                    break;
                default:
                    stiffness = MemberStiffness;
                    damping = 2 * DetMath.Sqrt(MemberStiffness * node.Mass);
                    hold = MemberStiffness;
                    capstan = true;
                    remesh = false;
                    break;
            }
            if (!kept)
            {
                slot = CableContact.On(c.Point, c.Normal, velocity, stiffness, damping, hold, m.Static, m.Kinetic, capstan);
                slot.Member = m.Member;
                slot.MemberKind = (byte)m.Kind;
                slot.Origin = c.Point - c.Normal * _radius;
                slot.Fresh = m.Kind != FiberMember.Ground && m.Kind != FiberMember.Layer;
            }
            else
            {
                slot.Point = c.Point;
                slot.Normal = c.Normal;
                slot.Velocity = velocity;
                slot.Stiffness = stiffness;
                slot.Damping = damping;
                slot.HoldStiffness = hold;
                slot.StaticFriction = m.Static;
                slot.KineticFriction = m.Kinetic;
            }
            slot.Remeshable = remesh;
            slot.Notch = m.Kind == FiberMember.Snag && m.Pitch > 0;
            slot.Glide = m.Kind == FiberMember.Part;
            slot.Kinematic = m.Kind != FiberMember.Ground && m.Kind != FiberMember.Layer;
            slot.Edge = m.Edge;
            slot.Rough = RoughOf(m);
            slot.Cap = cap;
            slot.Slide = slide;
            slot.Through = node.Material;
        }

        // ---- after the step ----

        /// <summary>
        /// After the solve: each pressing contact's bend (R_eff and σ, the line's least), its reaction on the drone for a part, a
        /// hook or a stem letting go (spent until the line leaves it), a capstan starting to slide, and the events: FiberContact
        /// for a member taken this step, FiberSlip for a slide or a release.
        /// </summary>
        public void Finish(CableChain chain, in FlightState s, StepEvents events)
        {
            int n = chain.Count;
            Quat q = s.Attitude;
            _reactionCount = 0;
            MinBendRadius = double.PositiveInfinity;
            MinBendNode = -1;
            MinBendMember = FiberMember.None;
            MinBendTurn = 0;
            MinBendTension = 0;
            MinBendStress = 0;
            int touching = 0;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref chain[i];
                bool pressed = false;
                for (int k = 0; k < 2; k++)
                {
                    ref CableContact c = ref k == 0 ? ref a.Contact0 : ref a.Contact1;
                    if (!c.Active) continue;
                    bool pressing = c.NormalForce > 0;
                    pressed |= pressing;
                    double theta = 0, tHi = 0, tLo = 0;
                    if (i > 0 && i + 1 < n)
                    {
                        Double3 da = a.Position - chain[i - 1].Position, db = chain[i + 1].Position - a.Position;
                        theta = DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
                        double t0 = chain.Tension(i - 1), t1 = chain.Tension(i);
                        tHi = t0 > t1 ? t0 : t1;
                        tLo = t0 > t1 ? t1 : t0;
                    }
                    double rEff = pressing ? BendRadius(c.Edge, theta, tHi) : double.PositiveInfinity;
                    if (rEff < MinBendRadius)
                    {
                        MinBendRadius = rEff;
                        MinBendNode = i;
                        MinBendMember = (FiberMember)c.MemberKind;
                        MinBendTurn = theta;
                        MinBendTension = tHi;
                        MinBendStress = BendStress(rEff, tHi);
                    }

                    // The member's push on the node: the normal force and the hold's spring across the normal.
                    Double3 f = c.Normal * c.NormalForce;
                    if (c.Engaged && c.HoldStiffness > 0)
                    {
                        Double3 off = a.Position - c.Hold;
                        f = f - (off - c.Normal * Double3.Dot(c.Normal, off)) * c.HoldStiffness;
                    }
                    if (c.MemberKind == (byte)FiberMember.Part && pressing && _reactionCount < MaxReactions)
                    {
                        Double3 at = a.Position - c.Normal * _radius;
                        _reactionForce[_reactionCount] = f * -1.0;
                        _reactionPoint[_reactionCount] = q.InverseRotate(at - s.Position);
                        _reactionCount++;
                    }

                    if (c.Fresh) events.Add(Named(EventKind.FiberContact, c, a.Position - c.Normal * _radius, theta, tHi, rEff));

                    // A hook past its release force, or a stem past its slide-off press or Euler's load, lets go: spent.
                    double press = -Double3.Dot(c.Normal, a.Position - c.Point);
                    bool gone = c.Cap > 0 && c.NormalForce > c.Cap || c.Slide > 0 && press > c.Slide;
                    if (gone)
                    {
                        FlightEvent e = Named(EventKind.FiberSlip, c, a.Position, 0, c.NormalForce, c.NormalForce > c.Cap && c.Cap > 0 ? c.Cap : -c.Slide);
                        events.Add(e);
                        Spend((FiberMember)c.MemberKind, c.Member);
                        c.Active = false;
                        continue;
                    }

                    // A capstan the line had held at starting to slide through.
                    bool slid = c.Capstan && a.Material != c.Through;
                    if (slid && !c.Sliding) events.Add(Named(EventKind.FiberSlip, c, a.Position, theta, tHi, tLo));
                    c.Sliding = slid;
                }
                if (pressed) touching++;
            }
            Touching = touching;
        }

        // A member's surface class for the microbend (RoughMaterials): the ground, a layer and the drawn elements rough (soil,
        // straw, bark), the drone's parts and the conductors smooth, a shape or a record by its row.
        bool RoughOf(in MemberSeg m)
        {
            switch (m.Kind)
            {
                case FiberMember.Ground:
                case FiberMember.Layer:
                case FiberMember.Twig:
                case FiberMember.Stem:
                case FiberMember.Straw:
                    return true;
                case FiberMember.Shape:
                case FiberMember.Snag:
                    return m.Surface != WorldIds.NoSurface || m.Material < _matRough.Length && _matRough[m.Material];
                default:
                    return false;
            }
        }

        FlightEvent Named(EventKind kind, in CableContact c, Double3 point, double a, double b, double cc) =>
            Named(kind, c.MemberKind, c.Member, point, a, b, cc);

        /// <summary>An event of kind naming the member (memberKind, member) as FiberContact names it (Index its FiberMember, and
        /// Object, Shape and Part), at point with A, B and C.</summary>
        public FlightEvent Named(EventKind kind, byte memberKind, ulong member, Double3 point, double a, double b, double cc)
        {
            FlightEvent e = FlightEvent.Of(kind);
            e.Index = memberKind;
            FiberMember m = (FiberMember)memberKind;
            if (m == FiberMember.Part)
            {
                int shape = (int)member;
                e.Object = DroneObject;
                e.Shape = shape;
                e.Part = (ushort)_parts.Shape(shape).Part;
            }
            else
            {
                e.Object = (int)(uint)(member >> 32);
                e.Shape = (int)(uint)member;
            }
            e.Point = point;
            e.A = a;
            e.B = b;
            e.C = cc;
            return e;
        }

        void Spend(FiberMember kind, ulong member)
        {
            if (_spentCount == MaxSpent) return;
            _spent[_spentCount] = member;
            _spentKind[_spentCount] = (byte)kind;
            _spentSeen[_spentCount] = true;
            _spentCount++;
        }

        /// <summary>The members a cache builds from its query results: what each element, record segment and conductor segment
        /// is to the line (FiberContacts' cover sets and rows).</summary>
        internal void Build(ref MemberSeg m, in MicroElement e)
        {
            double d = e.Diameter;
            m.Radius = d / 2;
            m.Edge = d / 2;
            m.Object = e.Object;
            m.Shape = e.Index;
            m.Part = -1;
            m.Member = e.Id;
            ulong bits = FlightRandom.Mix64(e.Id);
            double u = (bits >> 11) * (1.0 / 9007199254740992.0);
            double u2 = (FlightRandom.Mix64(bits) >> 11) * (1.0 / 9007199254740992.0);
            switch (e.Kind)
            {
                case MicroKind.Twig:
                    m.Kind = FiberMember.Twig;
                    m.Static = _twigStatic;
                    m.Kinetic = _twigKinetic;
                    break;
                case MicroKind.Stem:
                {
                    m.Kind = FiberMember.Stem;
                    m.Static = _strawStatic;
                    m.Kinetic = _strawKinetic;
                    double modulus = _stem.ModulusMin + (_stem.ModulusMax - _stem.ModulusMin) * u, inner = d * (1 - 2 * _stem.WallShare);
                    m.Bending = modulus * 3.141592653589793 / 64 * (d * d * d * d - inner * inner * inner * inner);
                    break;
                }
                default:
                    m.Kind = FiberMember.Straw;
                    m.Static = _strawStatic;
                    m.Kinetic = _strawKinetic;
                    m.Release = _straw.HookReleaseMin + (_straw.HookReleaseMax - _straw.HookReleaseMin) * u2;
                    break;
            }
        }

        internal bool Wants(in MicroElement e) =>
            e.Kind == MicroKind.Twig || _cover && (e.Kind == MicroKind.Stem || e.Kind == MicroKind.Straw && e.Anchored);

        internal void Build(ref MemberSeg m, in SnagSegment g)
        {
            m.Kind = FiberMember.Snag;
            m.Member = ((ulong)(uint)g.Object << 32) | (uint)g.Record;
            m.Object = g.Object;
            m.Shape = g.Record;
            m.Part = -1;
            m.Material = g.Material;
            m.Radius = g.Radius;
            m.Edge = g.EdgeRadius;
            m.Pitch = g.BarbPitch;
            m.Static = g.Material < _matStatic.Length ? _matStatic[g.Material] : _polymerStatic;
            m.Kinetic = g.Material < _matKinetic.Length ? _matKinetic[g.Material] : _polymerKinetic;
        }

        internal void Build(ref MemberSeg m, in Conductor w)
        {
            m.Kind = FiberMember.Wire;
            m.Member = ((ulong)(uint)w.Object << 32) | (uint)w.Shape;
            m.Object = w.Object;
            m.Shape = w.Shape;
            m.Part = -1;
            m.Material = w.Material;
            m.Radius = w.Diameter / 2;
            m.Edge = w.EdgeRadius;
            m.Static = w.Material < _matStatic.Length ? _matStatic[w.Material] : _polymerStatic;
            m.Kinetic = w.Material < _matKinetic.Length ? _matKinetic[w.Material] : _polymerKinetic;
        }

        // ---- geometry (+ − × ÷ √ only) ----

        /// <summary>The squared distance from p to segment a–b.</summary>
        public static double PointSegment(Double3 p, Double3 a, Double3 b)
        {
            Double3 d = b - a, r = p - a;
            double l2 = Double3.Dot(d, d), t = l2 > 0 ? Double3.Dot(r, d) / l2 : 0;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            Double3 e = r - d * t;
            return Double3.Dot(e, e);
        }

        static double ClosestOnSegment(Double3 p, Double3 a, Double3 b)
        {
            Double3 d = b - a;
            double l2 = Double3.Dot(d, d), t = l2 > 0 ? Double3.Dot(p - a, d) / l2 : 0;
            return t < 0 ? 0 : t > 1 ? 1 : t;
        }

        /// <summary>The squared distance between segments p1–q1 and p2–q2, and the closest points' fractions s and t along each
        /// (Ericson 2005, §5.1.9).</summary>
        public static double SegmentSegment(Double3 p1, Double3 q1, Double3 p2, Double3 q2, out double s, out double t)
        {
            Double3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            double a = Double3.Dot(d1, d1), e = Double3.Dot(d2, d2), f = Double3.Dot(d2, r);
            if (!(a > 0) && !(e > 0))
            {
                s = t = 0;
                return Double3.Dot(r, r);
            }
            if (!(a > 0))
            {
                s = 0;
                t = Clamp01(f / e);
            }
            else
            {
                double c = Double3.Dot(d1, r);
                if (!(e > 0))
                {
                    t = 0;
                    s = Clamp01(-c / a);
                }
                else
                {
                    double b = Double3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 0 ? Clamp01((b * f - c * e) / denom) : 0;
                    t = (b * s + f) / e;
                    if (t < 0)
                    {
                        t = 0;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1)
                    {
                        t = 1;
                        s = Clamp01((b - c) / a);
                    }
                }
            }
            Double3 g = p1 + d1 * s - (p2 + d2 * t);
            return Double3.Dot(g, g);
        }

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

        struct Hit
        {
            public int Seg, Node;
            public double U, Gap, Radius;
            public Double3 Point, Normal;
            public MemberSeg M;
        }

        struct Candidate
        {
            public MemberSeg M;
            public Double3 Point, Normal;
            public double Gap;
        }
    }

    /// <summary>One segment of a member the line may meet, as a cache holds it (or a shape, Plane: its face is met at each node
    /// from StaticContacts on the node, FiberContacts.Evaluate): its kind and identity, its core C–D and contact radius, the arris it bends the line over, the line's μ on it,
    /// and an element's mechanics (a stem's EI and length, a straw's release force, the core's distance from the base).</summary>
    public struct MemberSeg
    {
        public FiberMember Kind;
        public ulong Member;
        public int Object, Shape, Part;
        public ushort Material;
        public byte Surface;
        public bool Plane;
        public Double3 C, D;
        public double Radius, Edge, Static, Kinetic, Pitch;
        public double Bending, Length, Release, Along;
    }

    /// <summary>
    /// One of the fiber's caches (p7d-tether design 12): its members near a centre, asked for in one step and in use from
    /// contact.cache_swap_steps later, with a grid of CellSize cells over its cube built at the swap. The query runs at the
    /// request into the back buffers (the worker's job; its result does not depend on when it runs) and the swap reads them.
    /// </summary>
    public sealed class FiberCache
    {
        const double CellSize = 0.25;
        // Grid entries a member slot (numerical: a 0.25 m straw grown by the reach meets up to 8 cells; a long one more).
        const int EntriesPerMember = 16;
        /// <summary>The line's radius a member's grid bounds are grown by besides its own and KeepGap, m (numerical: twice the dry
        /// line's, above the wettest line's 0.2 mm).</summary>
        internal const double LineReach = 0.000125 * 2;

        public readonly double Radius, Refresh;
        readonly MicroElement[] _elements;
        readonly SnagSegment[] _snags;
        readonly Conductor[] _wires;
        int _elementCount, _snagCount, _wireCount;
        Double3 _asked;
        long _due = -1;

        public MemberSeg[] Members;
        public int MemberCount;
        public int[] CellStart, Entries;
        public int[] Seen;
        public int Stamp;
        readonly int _dim;
        Double3 _origin;

        internal FiberContacts Owner;
        internal WorldBugCause Overflow;
        internal int OverflowCount;

        public FiberCache(double radius, double refresh, int elements, int snags, int wires)
        {
            Radius = radius;
            Refresh = refresh;
            _elements = new MicroElement[elements];
            _snags = new SnagSegment[snags];
            _wires = new Conductor[wires];
            int capacity = 2 * elements + snags + wires * 256;
            Members = new MemberSeg[capacity];
            Seen = new int[capacity];
            Entries = new int[capacity * EntriesPerMember];
            _dim = (int)(2 * radius / CellSize) + 1;
            CellStart = new int[_dim * _dim * _dim + 1];
        }

        /// <summary>A grid is in use (its centre the request's).</summary>
        public bool Valid { get; private set; }
        public Double3 Centre { get; private set; }
        /// <summary>A request is pending.</summary>
        public bool Pending => _due >= 0;
        public int Elements => _elementCount;
        public int Snags => _snagCount;
        public int Wires => _wireCount;

        // Asks again when the target is Refresh from the centre in use (or none is), unless a request is pending.
        internal void Follow(FlightWorld world, Double3 target, long step, long swap)
        {
            if (_due >= 0) return;
            if (Valid && (target - Centre).Length() < Refresh) return;
            _asked = target;
            _due = step + swap;
            _elementCount = world.MicroDetailNear(target, Radius, MicroKind.Straw | MicroKind.Stem | MicroKind.Twig, _elements);
            _snagCount = world.SnagsNear(target, Radius, _snags);
            _wireCount = world.WiresNear(target, Radius, _wires);
            if (_elementCount > _elements.Length) Over(WorldBugCause.FiberElementsFull, ref _elementCount, _elements.Length);
            if (_snagCount > _snags.Length) Over(WorldBugCause.FiberSnagsFull, ref _snagCount, _snags.Length);
            if (_wireCount > _wires.Length) Over(WorldBugCause.FiberWiresFull, ref _wireCount, _wires.Length);
        }

        void Over(WorldBugCause cause, ref int count, int capacity)
        {
            Overflow = cause;
            OverflowCount = count;
            count = capacity;
        }

        // The request's results in use from its due step: the members and the grid over the cube about the asked centre.
        internal void Swap(long step, StepEvents events)
        {
            if (_due < 0 || step < _due) return;
            _due = -1;
            Centre = _asked;
            _origin = _asked - new Double3(Radius, Radius, Radius);
            int count = 0;
            for (int i = 0; i < _elementCount; i++)
            {
                ref MicroElement e = ref _elements[i];
                if (!Owner.Wants(e)) continue;
                double length = (e.P1 - e.P0).Length() + (e.PointCount > 2 ? (e.P2 - e.P1).Length() : 0);
                for (int k = 0; k + 1 < e.PointCount && count < Members.Length; k++)
                {
                    ref MemberSeg m = ref Members[count++];
                    m = default;
                    Owner.Build(ref m, e);
                    m.C = k == 0 ? e.P0 : e.P1;
                    m.D = k == 0 ? e.P1 : e.P2;
                    m.Along = k == 0 ? 0 : (e.P1 - e.P0).Length();
                    m.Length = length;
                }
            }
            for (int i = 0; i < _snagCount && count < Members.Length; i++)
            {
                ref MemberSeg m = ref Members[count++];
                m = default;
                Owner.Build(ref m, _snags[i]);
                m.C = _snags[i].A;
                m.D = _snags[i].B;
            }
            for (int i = 0; i < _wireCount; i++)
            {
                ReadOnlySpan<Double3> p = _wires[i].Points.Span;
                for (int k = 0; k + 1 < p.Length && count < Members.Length; k++)
                {
                    ref MemberSeg m = ref Members[count++];
                    m = default;
                    Owner.Build(ref m, _wires[i]);
                    m.C = p[k];
                    m.D = p[k + 1];
                }
            }
            MemberCount = count;

            // The grid: each member in every cell its bounds grown by its reach meet (clamped to the cube), counting first.
            int cells = _dim * _dim * _dim;
            for (int c = 0; c <= cells; c++) CellStart[c] = 0;
            for (int k = 0; k < count; k++)
            {
                Bounds(k, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1);
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++) CellStart[Cell(x, y, z) + 1]++;
            }
            for (int c = 0; c < cells; c++) CellStart[c + 1] += CellStart[c];
            bool full = CellStart[cells] > Entries.Length;
            // Fill, each cell from its start (CellStart shifted back after).
            for (int k = 0; k < count; k++)
            {
                Bounds(k, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1);
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            int c = Cell(x, y, z), at = CellStart[c];
                            if (at < Entries.Length) Entries[at] = k;
                            CellStart[c] = at + 1;
                        }
            }
            for (int c = cells; c > 0; c--) CellStart[c] = CellStart[c - 1];
            CellStart[0] = 0;
            if (full)
            {
                // Past the entries' room: every cell's list cut at the room (numerical; a world beyond its budget).
                for (int c = 0; c <= cells; c++)
                    if (CellStart[c] > Entries.Length) CellStart[c] = Entries.Length;
                Overflow = WorldBugCause.FiberGridFull;
                OverflowCount = count;
            }
            for (int k = 0; k < count; k++) Seen[k] = 0;
            Stamp = 0;
            Valid = true;
        }

        void Bounds(int k, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
        {
            ref MemberSeg m = ref Members[k];
            double grow = m.Radius + FiberContacts.KeepGap + LineReach;
            Double3 lo = new Double3(DetMath.Min(m.C.X, m.D.X) - grow, DetMath.Min(m.C.Y, m.D.Y) - grow, DetMath.Min(m.C.Z, m.D.Z) - grow);
            Double3 hi = new Double3(DetMath.Max(m.C.X, m.D.X) + grow, DetMath.Max(m.C.Y, m.D.Y) + grow, DetMath.Max(m.C.Z, m.D.Z) + grow);
            Index(lo, out x0, out y0, out z0);
            Index(hi, out x1, out y1, out z1);
        }

        /// <summary>Whether a line piece a–b's bounds meet the grid's cube (unclamped).</summary>
        internal bool Meets(Double3 a, Double3 b)
        {
            double side = _dim * CellSize;
            return DetMath.Max(a.X, b.X) >= _origin.X && DetMath.Min(a.X, b.X) <= _origin.X + side
                && DetMath.Max(a.Y, b.Y) >= _origin.Y && DetMath.Min(a.Y, b.Y) <= _origin.Y + side
                && DetMath.Max(a.Z, b.Z) >= _origin.Z && DetMath.Min(a.Z, b.Z) <= _origin.Z + side;
        }

        /// <summary>The cells a line piece a–b's bounds meet (clamped to the cube).</summary>
        public void Cells(Double3 a, Double3 b, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
        {
            Index(new Double3(DetMath.Min(a.X, b.X), DetMath.Min(a.Y, b.Y), DetMath.Min(a.Z, b.Z)), out x0, out y0, out z0);
            Index(new Double3(DetMath.Max(a.X, b.X), DetMath.Max(a.Y, b.Y), DetMath.Max(a.Z, b.Z)), out x1, out y1, out z1);
        }

        void Index(Double3 p, out int x, out int y, out int z)
        {
            x = Axis(p.X - _origin.X);
            y = Axis(p.Y - _origin.Y);
            z = Axis(p.Z - _origin.Z);
        }

        int Axis(double v)
        {
            double f = v / CellSize;
            if (!(f > 0)) return 0;
            return f >= _dim ? _dim - 1 : (int)f;
        }

        public int Cell(int x, int y, int z) => (x * _dim + y) * _dim + z;
    }
}
