using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The blades against the shapes their discs cut (p7f-damage design 4–6, PHY-26): after each Sense, every blade's pass across
    /// a disc's pair takes its work from the rotor, gives the body its impulses and leaves its damage on the blade's state
    /// (DamagedRotor), which the rotor flies from the next step. Engine-free, allocation-free in a step.
    /// - The pass: a blade sweeps from the last step's RotorAngle to this one's; it passes a shape when it crosses the azimuth of
    ///   the shape's nearest point in the disc's slab (BladeCut), once per turn. It meets it there at r_m, with the face's normal
    ///   n and its tilt to the disc, sin β = |n·axis|; only blade beyond r_m and inside its tip meets the face. A shape whose
    ///   radial line leaves it again inside the tip (a rod, a pole, a conductor inside the rim) is met by the blade's leading
    ///   edge; one that holds the line past the tip (a wall, a slab, a trunk's face) by its tip.
    /// - Who yields (Rabinowicz's rule, published): a row harder than the blade (physics_materials.json's hardness_pa against
    ///   props.blade's) is a hard target and the blade yields; a softer one is carved. The ground and the soil shapes are their
    ///   surface's set in surfaces.json at the flight's moisture (p7f-damage 1.1a): a soil is carved at its bearing pressure
    ///   p = c·N_c (N_c = 2 + π, Prandtl's) and flings its bulk density, its sand wearing the leading edge (below); a rigid
    ///   surface (asphalt, a concrete slab) is a face by its own hardness. Without surfaces.json the ground and the soil, a thin
    ///   compliant row (no crush strength: p7a's elements and p7d's fabric and mesh), and a material the table lacks take no
    ///   blade damage here: the pass is logged (Glance) and does no work.
    /// - A hard face past the tip: the blade either bends flatwise over it, k_f·p·cot β (p its tip's depth past the face along
    ///   n, k_f = K_β/R² the rotating flap stiffness, K_β = I_b·(ω₀² + (K_s − 1)·Ω²) as the flapping's), or crushes back to it,
    ///   σ_c·A(r_m); the smaller wins (assumed: the path of least resistance).
    ///   - Crush: the blade beyond r_m is crushed back to it, work σ_c·V plus the friction μ·F_n over the tip's slide r_tip·α
    ///     (cos α = r_m/r_tip, DetMath.Acos), F_n = σ_c·A(r_m) on the hub along n for t_c = α/ω (the step's at most: a slowly
    ///     turning blade presses the face only for the step it is in). The face's force on the blade
    ///     (F_n along n and μ·F_n against its slide) bends every inner station, M = F·(r_m − x·R): the first station inward whose
    ///     moment passes its edgewise or flatwise strength breaks instead, its work the fracture G_c·A(x).
    ///   - Bend: an elastic slap at the blade's flatwise speed u_n/sin β against the tip's flap stiffness and modal mass, F =
    ///     u·√(k_f·m_tip) (m_tip = I_b/R²), capped by the tip's local crush σ_c·c·w (w SlapWidth); its moment from the tip breaks a
    ///     section past M_f, or past the proportional limit leaves a flap set (M − M_p)/k_p there; then the blade slides over the
    ///     face pressed by k_f·p·cot β, its friction's work over the tip's arc 2·r_tip·α, and wears its leading edge by Archard,
    ///     V = k·F·s/H_b, off the stations in contact.
    /// - A hard shape inside the rim: the leading edge strikes it head on, crushed across the shape's width w at its thickness,
    ///   F = σ_c·t·w; F bends the inner stations edgewise and breaks the first that fails, else the shape severs the blade at r_m
    ///   (its work σ_c·t·w·c, the slot crushed through the chord).
    /// - A soft face: the blade carves it at its crush pressure p over the volume it sweeps, its frontal height (c·|sin α| +
    ///   t·|cos α|) times the area it sweeps inside (the circular segment past r_m for a face past the tip, the disc across its
    ///   radial chord for a shape inside the rim), and flings the mass it meets, Σ ρ·dV·(ω·r)² (closed form over the area):
    ///   E = p·V + m_f·v², at the blade's speed through the face (the tip slide, model 45): its own and the body's at the
    ///   carve's centroid, u = (ω·r_c + v·ê_t, v·axis), α = θ − atan2(u_a, u_t), V and the fling × |u|/(ω·r_c) and its cube.
    ///   The crush presses the leading face along its normal (c/h of its drag) and the edge along −u; the fling leaves along
    ///   u. The rotor pays the face's impulse along its motion, −I_t·ω·r_c, the body the rest (a body running through the soil
    ///   faster than the blades screw can drive the rotor); the axial push is the face's (1.3's cot θ of the whole impulse is
    ///   model 43's); a pass slower than the step gives the body, and a drive the rotor, the step's share of its arc. The
    ///   cutting force along the motion bends the inner stations edgewise; the blade is whole unless
    ///   one breaks. The world is not changed (assumed: a carved groove is not stored). In soil, its sand bears its share s of
    ///   that force on the leading edge along the path, and wears it by Archard, V = k·(s·E/path)·path/H_b (k the severe class's,
    ///   ContactSpec.WearCoefficient, while the sand's quartz is harder than the blade), off the stations in the soil; a jammed
    ///   carve's grind wears by the work it pays, put on the blade when the grind ends. A soil pass first meets a stone with
    ///   the probability its swept volume and the soil's stone fraction give (Stone, "soil.stones"); the disc's part past a
    ///   carved face's chord makes no thrust or air torque from the next step (Immerse, DamagedRotor.Immersed).
    /// - Each pass's work E leaves the rotor in the step, ω' = √(ω² − 2E/J); with E ≥ ½Jω² the rotor stops on the face
    ///   (RotorJammed), held at the face's entry, its damage cut short by the energy it had: a crush reaches that share of its
    ///   depth, a break does not happen, a carve keeps the rest of its crush (a stopped blade flings nothing), which the
    ///   motor's spin-up pays step by step until the blade is through (the blade held where the work has carried it), while the
    ///   body's motion drags the held blade through the soil (Grind's plough) and may break it.
    /// - The body takes the rotor's lost angular momentum ΔL = J·(ω − ω') as the reaction −s·ΔL about its axis (s Propulsion's
    ///   reaction sign), the face's tangential impulse ΔL/r_c at the meeting point against the blade's motion, and the face's
    ///   push: along n for a hard face (F_n·t_c), along the axis for a carve (the tangential impulse × cot θ: the pitched face's
    ///   pressure). A broken piece leaves with its own momentum: the rotor's J falls by its share at the next step
    ///   (Propulsion.Rebuild) and its speed does not, so its kinetic energy is not taken twice.
    /// - The step's strikes go to FlightState.BladeStrikes (kind Shape) and the log (BladeStrike, BladeDamaged, RotorJammed);
    ///   the damaged rotors are rebuilt, so their coefficients change from the next step.
    /// - Randomness: each blade's flexural, crush and fracture strengths × (1 + δ), δ from "prop.blade_strength" (nearly normal,
    ///   ContactSpec.StrengthSigma, clamped ±StrengthBound), drawn at the flight's start and logged. Nothing about a strike is
    ///   drawn.
    /// - Standing stems (w9-village-physics 2.2): PropStems.cs, each turning disc's before its pairs; every corner's strikes'
    ///   impulse along its rotor's axis is TipImpulse, which loads its arm (AirframeModes.Strike, G8).
    /// </summary>
    public sealed partial class PropStrikes
    {
        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;

        /// <summary>Prandtl's bearing capacity factor N_c = 2 + π (published): a soil's bearing pressure is c·N_c, as
        /// physics_materials.json's sandbag row derives its crush.</summary>
        public const double BearingFactor = 2 + Pi;

        static readonly string[] CornerIds = { "rr", "fr", "rl", "fl" };
        static readonly Double3 Up = new Double3(0, 1, 0);

        readonly ContactSpec _spec;
        readonly FlightWorld _world;
        readonly ContactModel _contacts;
        readonly Propulsion _propulsion;
        readonly PropBlade _b;
        readonly double _radius, _span, _bladeInertia, _flapFrequency;
        readonly int _blades, _stations;
        readonly double[] _spin = new double[4];
        readonly int[] _discs;
        readonly MaterialRow[] _rows;
        readonly bool[] _known;
        readonly CrownRow[] _crown; // world material id → a crown's twig shell's twigs and leaves (CrownRows with a crown block), or null
        readonly MaterialRow[] _surfaces; // world surface id → what a blade meets there (Name null: none, a Glance)
        readonly CoverSet _twig;            // surfaces.json's twig at the flight's moisture: a crown's twigs' tissue (CutEnergy 0 without it)
        readonly double[] _twigDue = new double[4]; // each rotor's crown twigs swept and not yet met (Twigs): below 1
        readonly int[] _twigBlade = new int[4];     // the blade each rotor meets its next twig with
        readonly double[] _sand;          // world surface id → its sand's wear per joule of a carve's work, m³/J
        readonly double[] _stones, _stoneSize; // world surface id → its stones' volume fraction (0: none, or none harder than the blade) and diameter, m
        readonly double _stoneDensity;
        readonly SeedStream _stoneStream;  // "soil.stones": one draw a soil carve's pass, keyed by the draws so far
        ulong _stoneDraws;
        readonly double[] _strength;
        readonly double[] _area, _flat, _edge, _stiff, _mass; // per station, the whole chord: A, M_f, M_e, EI_f/span, mass
        readonly Pending[] _pending = new Pending[4];
        readonly int[] _discOf = { -1, -1, -1, -1 }; // each corner's disc shape
        readonly double[] _restPeak;
        readonly bool[] _bladeLost; // per blade: lost at its root (BladeLost logged once)

        /// <summary>A rotor stopped in a carve: the work still to do and where it is (design 6); in soil, the sand's wear per joule
        /// of it (m³/J), what the grind has worn so far (m³), from where (r/R), and what it carves.</summary>
        struct Pending
        {
            public bool Active;
            public int Object, Shape, Blade;
            public double Left, Work, Entry, Psi;
            public double WearRate, Worn, From;
            public ushort Material;
            public byte Surface;
            public Double3 Point;
        }

        /// <summary>The last pass, for a test: its rotor, blade and mode, the meeting radius, the work and the rotor's speeds
        /// around it, the rotor's J, the face's impulse on the drone (world N·s) at its point and the reaction about the axis
        /// (body N·m·s), the blade's tip before and after (r/R), the speed into the face and the forces the mode decided on; Grind
        /// for a stopped carve's step (its Work the pass's whole work, Used this step's share, Plough the body's work on the held
        /// blade); for a carve, the body's velocity at its centroid and the blade's direction of motion (world), its speed
        /// through the face over its own (Ratio) and its attack there (rad), and in soil the stones it was expected to meet, λ;
        /// Stone for a stone the pass met (Radius where, Exit its diameter).</summary>
        public struct PassRecord
        {
            public int Corner, Blade;
            public StrikeMode Mode;
            public double Radius, Exit, Work, Used, SpeedBefore, SpeedAfter, Inertia, TipBefore, TipAfter, SpeedIn, Tilt;
            public double CrushForce, BendForce, PeakForce, PeakMoment, Margin, Wear, Friction, Fling, Ratio, Attack, Plough, Stones;
            public int Station;
            public bool Jammed, Grind, Stone;
            public Double3 Impulse, Point, Normal, Reaction, Body, Motion;
        }

        public PassRecord Last;

        /// <summary>Every pass's record when set (a test's or a bench's; null in a flight, which then allocates nothing).</summary>
        public System.Collections.Generic.List<PassRecord> Record;

        /// <summary>The passes so far and the work they took, J.</summary>
        public long Passes { get; private set; }

        /// <summary>The soil passes that drew for a stone so far ("soil.stones": the next draw's index).</summary>
        public ulong StoneDraws => _stoneDraws;

        public PropStrikes(ContactModel contacts, FlightWorld world, Propulsion propulsion, PropulsionSpec propulsionSpec, FlightRandom random)
        {
            _contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _propulsion = propulsion ?? throw new ArgumentNullException(nameof(propulsion));
            _spec = contacts.Spec;
            _b = propulsionSpec?.Rotor?.Blade ?? throw new ArgumentException("the strikes need the props' blade (PropulsionSpec.Rotor)", nameof(propulsionSpec));
            _radius = propulsionSpec.Props.Diameter / 2;
            _blades = propulsionSpec.Props.Blades;
            _stations = _b.Stations.Length;
            _span = _b.Span;
            _bladeInertia = _b.BladeInertia(_radius);
            _flapFrequency = TwoPi * _spec.FlapFrequency;
            for (int i = 0; i < 4; i++) _spin[i] = propulsionSpec.Motors[i].Clockwise ? -1 : 1;

            int discs = 0;
            foreach (ContactShape s in _spec.Shapes)
                if (s.Role == ContactRole.Disc) discs++;
            _discs = new int[discs];
            for (int i = 0, k = 0; i < _spec.Shapes.Length; i++)
                if (_spec.Shapes[i].Role == ContactRole.Disc)
                {
                    _discs[k++] = i;
                    _discOf[_spec.Shapes[i].Corner] = i;
                }
            _restPeak = new double[4 * _blades];
            _bladeLost = new bool[4 * _blades];

            // The world's materials by id, as the contacts read them.
            IWorldQuery q = world.Query;
            int top = 0;
            for (int id = 1; id < ushort.MaxValue; id++)
                if (q.MaterialName((ushort)id) != null) top = id;
            _rows = new MaterialRow[top + 1];
            _known = new bool[top + 1];
            _crown = new CrownRow[top + 1];
            for (int id = 1; id <= top; id++)
            {
                string name = q.MaterialName((ushort)id);
                if (name != null && _spec.Row(name, out MaterialRow r))
                {
                    _rows[id] = r;
                    _known[id] = true;
                    _crown[id] = Array.IndexOf(CrownRows, name) >= 0 ? r.Crown : null;
                }
            }

            // The world's surfaces by id at the flight's moisture, as the contacts read them (p7f-damage 1.1a): a soil is a soft
            // face at its bearing pressure c·N_c with its bulk density, its sand's wear per joule of a carve k·s/H_b (severe while
            // quartz is harder than the blade); a rigid surface is a face by its own hardness, crush strength and density. None
            // without surfaces.json: then the ground and the soil glance.
            _surfaces = new MaterialRow[256];
            _sand = new double[256];
            _stones = new double[256];
            _stoneSize = new double[256];
            SurfaceTable table = _spec.Surfaces;
            double severe = table != null && table.SandHardness > _b.Hardness ? _spec.WearCoefficient : 0;
            for (int id = 1; id < 256; id++)
            {
                if (!contacts.SurfaceSet((byte)id, out SoilSet set)) continue;
                double p = set.Cohesion * BearingFactor;
                _surfaces[id] = set.Rigid
                    ? new MaterialRow
                    {
                        Name = q.SurfaceName((byte)id), StaticFriction = set.StaticFriction, KineticFriction = set.KineticFriction, Hardness = set.Hardness,
                        CrushStrength = set.CrushStrength, Density = set.Density,
                    }
                    : new MaterialRow
                    {
                        Name = q.SurfaceName((byte)id), StaticFriction = set.StaticFriction, KineticFriction = set.KineticFriction, Hardness = p, CrushStrength = p,
                        Density = set.BulkDensity,
                    };
                _sand[id] = set.Rigid ? 0 : severe * set.SandShare / _b.Hardness;
                // Its stones (p7f-damage 1.3's tip slide): hard targets by the sand's quartz when that is harder than the blade.
                if (set.Rigid || table == null || !(table.SandHardness > _b.Hardness)) continue;
                _stones[id] = set.StoneFraction;
                _stoneSize[id] = set.StoneDiameter;
            }
            _twig = contacts.Cover(CoverKind.Twig); // a crown's twigs' tissue (Twigs)
            bool stony = false;
            for (int id = 1; id < 256; id++) stony |= _spec.TipSlide && _stones[id] > 0;
            if (stony)
            {
                _stoneDensity = table.ParticleDensity;
                _stoneStream = random.Open("soil.stones");
            }

            // The stations' sections, the whole chord: area, flatwise and edgewise strength, the flatwise stiffness over a span.
            _area = new double[_stations];
            _flat = new double[_stations];
            _edge = new double[_stations];
            _stiff = new double[_stations];
            _mass = new double[_stations];
            double form = _spec.SectionForm;
            for (int k = 0; k < _stations; k++)
            {
                double c = _b.Chord[k], t = _b.Thickness[k];
                _area[k] = _b.Fill[k] * c * t;
                _flat[k] = form * _b.FlexuralStrength * c * t * t / 6;
                _edge[k] = form * _b.FlexuralStrength * t * c * c / 6;
                _stiff[k] = form * _b.FlexuralModulus * c * t * t * t / 12 / (_span * _radius);
                _mass[k] = _b.Density * _area[k] * _radius * _span;
            }

            // Each blade's strength factor (design 12).
            _strength = new double[4 * _blades];
            SeedStream stream = random.Open("prop.blade_strength");
            for (int i = 0; i < 4; i++)
                for (int b = 0; b < _blades; b++)
                    _strength[i * _blades + b] = 1 + random.DrawNormal(ref stream, "prop.blade_strength." + CornerIds[i] + "." + b, _spec.StrengthSigma, _spec.StrengthBound);
            InitStems();
            InitLeaves(q);
        }

        /// <summary>A blade's strength factor, 1 + δ.</summary>
        public double Strength(int corner, int blade) => _strength[corner * _blades + blade];

        /// <summary>
        /// The step's passes, after Sense: each disc's pairs at the pose now, the rotors' sweep over the step from their angles
        /// at its start (from, before the propulsion's sub-steps turned them) to now's; the rotors' speeds and angles, the body's
        /// velocity and rate, BladeStrikes and the blades change; the damaged rotors are rebuilt.
        /// </summary>
        public void Apply(in Corners from, ref FlightState now, in MassProperties body, StepEvents events)
        {
            Double3 axis = now.Attitude.Rotate(Up), impulse = default, angular = default;
            int changed = 0, passed = 0;
            StemsBegin(now);
            for (int n = 0; n < _discs.Length; n++)
            {
                int i = _discs[n];
                ContactShape d = _spec.Shapes[i];
                int c = d.Corner, pairs = _contacts.DiscPairs(i);
                if (_contacts.Resting(c))
                {
                    // A resting rotor's blades are the contact model's parts (1.3b): no pass, no carve.
                    Immerse(c, double.PositiveInfinity);
                    continue;
                }
                Double3 before = impulse;
                double start = from[c], sweep = now.RotorAngle[c] - start;
                if (sweep < 0) sweep += TwoPi;
                // The stems the disc turns in (PropStems), before its pairs.
                if (Stems(i, d, c, ref now, body, axis, start, sweep, events, ref impulse, ref angular)) changed |= 1 << c;
                if (pairs == 0)
                {
                    if (_pending[c].Active && Finish(i, d, ref _pending[c], events)) changed |= 1 << c;
                    Immerse(c, double.PositiveInfinity);
                    _tip[c] = Double3.Dot(impulse - before, axis);
                    continue;
                }
                Double3 hub = now.Position + now.Attitude.Rotate(d.A);
                int skipObject = int.MinValue, skipShape = 0, skipBlade = -1;
                if (_pending[c].Active)
                {
                    skipObject = _pending[c].Object;
                    skipShape = _pending[c].Shape;
                    skipBlade = _pending[c].Blade;
                    if (Grind(i, d, c, ref now, body, hub, axis, events, ref impulse, ref angular)) changed |= 1 << c;
                }
                double chord = double.PositiveInfinity; // the nearest carved face across the disc: its part past the chord is in it
                var crown = new BladeCut.Meeting { Radius = double.PositiveInfinity }; // the crown's twigs: the deepest shell's meeting
                int crownObj = 0, crownShape = 0;
                ushort crownMaterial = 0;
                for (int k = 0; k < pairs; k++)
                {
                    _contacts.DiscPair(i, k, out int obj, out int shape, out ushort material, out byte surface);
                    if (!Geometry(i, k, obj, shape, surface, hub, d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)) continue;
                    if (!BladeCut.Nearest(g, chain, hub, axis, d.HalfThickness, d.Radius, out BladeCut.Meeting m)) continue;
                    if (Crown(surface, material))
                    {
                        // A crown's twig shell: its twigs (Twigs, below), once for the shells' union, never a glance per shell.
                        if (m.Radius < crown.Radius)
                        {
                            crown = m;
                            crownObj = obj;
                            crownShape = shape;
                            crownMaterial = material;
                        }
                        continue;
                    }
                    if (_spec.TipSlide && m.Radius < chord && Face(obj, material, surface, out MaterialRow row) && !(row.Hardness > _b.Hardness))
                    {
                        // A softer face that holds the radial line past the rim (the soil, a trunk's face): a segment of the disc.
                        BladeCut.Along(g, chain, hub, axis, m.Direction, d.HalfThickness, _radius, out _, out double rout, out _, out _);
                        if (!(rout < _radius) || BladeCut.Wide(g, chain, hub, axis, m.Direction, d.HalfThickness, m.Radius, rout, _radius)) chord = m.Radius;
                    }
                    if (!(now.RotorSpeed[c] > 0)) continue;
                    double psi = Azimuth(now.Attitude, m.Direction, c);
                    for (int b = 0; b < _blades && now.RotorSpeed[c] > 0; b++)
                    {
                        if (obj == skipObject && shape == skipShape && b == skipBlade) continue;
                        double ahead = Wrap(psi - (start + TwoPi * b / _blades));
                        if (!(ahead > 0 && ahead <= sweep)) continue;
                        passed |= 1 << c;
                        if (Pass(i, d, c, b, ref now, body, obj, shape, material, surface, g, chain, hub, axis, m, psi, events, ref impulse, ref angular))
                            changed |= 1 << c;
                    }
                }
                if (crown.Radius < double.PositiveInfinity && now.RotorSpeed[c] > 0)
                {
                    passed |= 1 << c;
                    if (Twigs(i, d, c, ref now, body, hub, axis, crown, crownObj, crownShape, crownMaterial, events, ref impulse, ref angular))
                        changed |= 1 << c;
                    if (now.RotorSpeed[c] > 0) Leaves(i, d, c, ref now, body, hub, axis, crown, crownObj, crownShape, crownMaterial, events, ref impulse, ref angular);
                }
                Immerse(c, chord);
                _tip[c] = Double3.Dot(impulse - before, axis); // every strike's push along the rotor's axis on its arm (G8)
            }
            StemsEnd(now, events);
            if (impulse.X != 0 || impulse.Y != 0 || impulse.Z != 0 || angular.X != 0 || angular.Y != 0 || angular.Z != 0)
            {
                now.Velocity = now.Velocity + impulse * body.InverseMass;
                now.BodyRate = now.BodyRate + body.InverseInertia * angular;
            }
            changed |= RestDamage(events);
            int held = 0;
            for (int c = 0; c < 4; c++)
                if (_pending[c].Active) held |= 1 << c;
            _contacts.HoldBlades(held);
            _contacts.Passes(passed); // what their blades come back to rest inside is these passes' (ContactModel.PlaceBlades)
            for (int c = 0; c < 4; c++)
                if ((changed & (1 << c)) != 0) _propulsion.Rebuild(c);
        }

        /// <summary>A resting blade's largest flatwise moment so far at its most loaded station, N·m (its plastic set's memory: a
        /// set grows only past it; 1.3b), per slot.</summary>
        public double RestPeak(int corner, int blade) => _restPeak[corner * _blades + blade];

        /// <summary>The last resting load that bent or broke a blade (1.3b), for a test: its rotor and blade, the section's
        /// forces (N), radius (m), the station, the moment and its share of the strength, the set (rad) and whether it broke.</summary>
        public struct RestRecord
        {
            public int Corner, Blade, Station;
            public double Flat, Edge, Radius, Moment, Margin, Set;
            public bool Broke;
        }

        public RestRecord LastRest;

        // The step's resting blades (p7f-damage 1.3b, design 16): each one's load from the contact model's Forces, its section
        // forces at their force-weighted radius, against its sections by design 5's laws as a strike's: the first station inward
        // whose moment passes its edgewise or flatwise strength (the blade's drawn factor, its chord share) breaks there, the
        // blade beyond it lost; else a flatwise moment past the proportional limit and past the blade's largest so far leaves a
        // flap set, (M − max(M_p, M_peak))/k_p at the worst station (a static load held step after step sets once: the
        // plastic hinge's memory, assumed without hardening). The bits of the rotors it changed.
        int RestDamage(StepEvents events)
        {
            int changed = 0;
            for (int c = 0; c < 4; c++)
            {
                if (!_contacts.Resting(c)) continue;
                int i = _discOf[c];
                if (i < 0) continue;
                ContactShape d = _spec.Shapes[i];
                for (int b = 0; b < _blades; b++)
                {
                    ContactModel.RestLoad l = _contacts.RestLoadOf(c * _blades + b);
                    if (!l.Loaded) continue;
                    BladeState blade = _propulsion.Rotor(c).Blade(b);
                    // The load acts on the blade's material, so at its tip at most: a pair deep in a face (a blade inside a crown's
                    // twig shell) has its point on the face's plane, up to the shell's depth past the tip (w9-village-physics 2.7).
                    double f = _strength[c * _blades + b], rc = DetMath.Min(l.Radius, blade.TipRadius * _radius), ff = DetMath.Abs(l.Flat),
                        fe = DetMath.Abs(l.Edge);
                    int broke = Breaks(blade, f, rc, rc, fe, ff, out double moment, out double margin);
                    var rec = new RestRecord { Corner = c, Blade = b, Flat = l.Flat, Edge = l.Edge, Radius = rc, Moment = moment, Margin = margin, Station = broke };
                    double before = Mass(blade);
                    if (broke >= 0)
                    {
                        blade.Trim(_b.Stations[broke]);
                        rec.Broke = true;
                        LastRest = rec;
                        Damaged(i, d, b, StrikeMode.Break, l.Object, l.Shape, l.Material, l.Surface, l.Point, blade.TipRadius, before - Mass(blade),
                            _b.Stations[broke], events);
                        changed |= 1 << c;
                        continue;
                    }
                    int set = Worst(blade, f, rc, ff, out double mf, out double limit);
                    ref double peak = ref _restPeak[c * _blades + b];
                    double from = DetMath.Max(limit, peak);
                    if (set < 0 || !(mf > from)) continue;
                    double phi = (mf - from) / (_spec.PlasticShare * _stiff[set] * blade.ChordShare[set]);
                    peak = mf;
                    double sign = l.Flat < 0 ? -1 : 1;
                    for (int j = set + 1; j < _stations; j++)
                        blade.FlapSet[j] += sign * phi * (_b.Stations[j] - _b.Stations[set]) * _radius;
                    rec.Station = set;
                    rec.Moment = mf;
                    rec.Set = phi;
                    LastRest = rec;
                    Damaged(i, d, b, StrikeMode.Bend, l.Object, l.Shape, l.Material, l.Surface, l.Point, blade.TipRadius, 0, _b.Stations[set], events);
                    changed |= 1 << c;
                }
            }
            return changed;
        }

        // One blade's pass across a pair; true when it changed the blade.
        bool Pass(int i, in ContactShape d, int c, int b, ref FlightState now, in MassProperties body, int obj, int shape, ushort material, byte surface,
            in ShapeGeometry g, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, in BladeCut.Meeting m, double psi, StepEvents events,
            ref Double3 impulse, ref Double3 angular, bool drawn = false, double stones = 0)
        {
            BladeState blade = _propulsion.Rotor(c).Blade(b);
            double R = _radius, tip = blade.TipRadius, rtip = tip * R, rm = m.Radius;
            if (!(rm < rtip)) return false; // the blade, cut back, no longer reaches it
            // Inside the blade's root (its first section's inner edge) the disc is the hub's and the motor's bell's (MotorBell): no
            // blade meets a shape there (model 64: a meeting at the axis took the rotor's lost momentum over a vanishing lever,
            // lost/r_c, a 107 N·s impulse in a graze, and threw the drone through the wall).
            if (_spec.BladeRoot && rm < (_b.Stations[0] - 0.5 * _span) * R) return false;
            double omega = now.RotorSpeed[c], J = _propulsion.Inertia(c), s = _spin[c], f = _strength[c * _blades + b];
            Double3 dir = m.Direction, nrm = m.Normal, motion = Double3.Cross(axis, dir) * -s;
            Double3 at = PointVelocity(now, m.Point);
            var rec = new PassRecord
            {
                Corner = c, Blade = b, Radius = rm, Exit = double.PositiveInfinity, SpeedBefore = omega, Inertia = J, TipBefore = tip, TipAfter = tip,
                Point = m.Point, Normal = nrm, Station = -1, Tilt = DetMath.Abs(Double3.Dot(nrm, axis)),
            };
            // The face: a surface's set (the ground, a soil shape) or a material's row.
            bool soil = surface != WorldIds.NoSurface;
            if (!Face(obj, material, surface, out MaterialRow row))
            {
                rec.Mode = StrikeMode.Glance;
                rec.SpeedAfter = omega;
                Strike(i, d, b, obj, shape, material, surface, rec, events);
                Slot(ref now, c, b, material, 0, 0, rm);
                Last = rec;
                Record?.Add(rec);
                Passes++;
                return false;
            }

            BladeCut.Along(g, chain, hub, axis, dir, d.HalfThickness, R, out _, out double rout, out _, out _);
            // A box's thin skin across the disc (a wall's, a board's) is a face the tip meets as a slab's, though the line leaves it
            // inside the tip (BladeCut.Wide, w3-farmland-ground 2.7); a carve sweeps its strip between rm and rout.
            bool inside = rout < rtip && !BladeCut.Wide(g, chain, hub, axis, dir, d.HalfThickness, rm, rout, rtip), hard = row.Hardness > _b.Hardness;
            rec.Exit = rout;
            double mu = row.KineticFriction, crush = _b.CrushStrength * f;
            double work, rotorWork = double.NaN, pushN = 0, axial = 0, cot = double.NaN, rc = rm, alpha, newTip = tip, rotorShare = 1;
            double carveCrush = 0, notch = 0;
            int broke = -1, set = -1;
            double phi = 0, wear = 0;
            bool crushPast = false; // a crush back to a face past the tip: its push acts over α, every other pass's over 2α
            StrikeMode mode;
            if (!hard)
            {
                // A soft face: the carve (design 5).
                mode = StrikeMode.Carve;
                double area, second, path;
                if (!inside)
                {
                    alpha = DetMath.Acos(rm / rtip);
                    DetMath.SinCos(alpha, out double sa, out double ca);
                    double ta = sa / ca, r2 = rtip * rtip, m2 = rm * rm;
                    area = r2 * (alpha - sa * ca);
                    second = r2 * r2 * alpha / 2 - m2 * m2 * (ta + ta * ta * ta / 3) / 2;
                    if (rout < rtip)
                    {
                        // A thin skin: less the segment past its far face, which the blade reaches only through it (air there).
                        double beta = DetMath.Acos(rout / rtip);
                        DetMath.SinCos(beta, out double sb0, out double cb0);
                        double tb = sb0 / cb0, o2 = rout * rout;
                        area -= r2 * (beta - sb0 * cb0);
                        second -= r2 * r2 * beta / 2 - o2 * o2 * (tb + tb * tb * tb / 3) / 2;
                    }
                    rc = 0.5 * (rm + rtip);
                    path = rc * 2 * alpha;
                }
                else
                {
                    double q = 0.5 * (rout - rm), dd = 0.5 * (rm + rout);
                    area = Pi * q * q;
                    second = area * (dd * dd + q * q / 2);
                    rc = dd;
                    path = 2 * q;
                    alpha = q / dd;
                }
                int k = Station(rc / R);
                double chord = _b.Chord[k] * blade.ChordShare[k], theta = _b.SectionAngle[k] + blade.FactoryPitch + blade.PitchSet[k];
                // The blade's motion through the face at the carve's centroid: its own, ω·r_c along its motion, and the body's
                // there (the tip slide: a drone moving along its rotor's axis advances its blades into the soil or out of it); the
                // body's share along the blade's span slides it along itself and sweeps nothing (assumed). Its speed over its own
                // scales the volume swept (by it) and the fling (by its cube); its attack sets the frontal height and splits the
                // crush's force between the leading face and edge (Section). The flung mass leaves at the blade's velocity through the
                // soil (design 5's m_f·v², the swept soil brought to it), so its reaction is along that motion only. A body at rest
                // leaves the carve's work as design 5 has it, bit for bit.
                Double3 moving = _spec.TipSlide ? PointVelocity(now, hub + dir * rc) : default;
                double ut = omega * rc + Double3.Dot(moving, motion), ua = Double3.Dot(moving, axis), speed = DetMath.Sqrt(ut * ut + ua * ua);
                double ratio = speed / (omega * rc), cb = ut / speed, sb = ua / speed;
                Section(chord, _b.Thickness[k], theta, ut, ua, speed, out double height, out double gt, out double ga, out double attack);
                if (soil && !drawn && _spec.TipSlide && _stones[surface] > 0)
                {
                    // The soil's stones (the tip slide): the pass meets one with the probability 1 − e^(−λ) of what it sweeps (Stone),
                    // drawn once a pass; met, the stone takes its energy and its damage first, then the pass carves on with what is
                    // left of the rotor and the blade.
                    double w = _stoneSize[surface];
                    stones = _stones[surface] * (height + w) * (area + 0.5 * w * path) * ratio / (Pi * w * w * w / 6);
                    double draw = (_stoneStream.BitsAt(_stoneDraws++) >> 11) * (1.0 / 9007199254740992.0);
                    if (draw < 1 - DetMath.Exp(-stones))
                    {
                        bool struck = Stone(i, d, c, b, ref now, body, obj, shape, material, surface, hub, dir, motion, rc, stones, events, ref impulse, ref angular);
                        if (now.RotorSpeed[c] > 0)
                            struck |= Pass(i, d, c, b, ref now, body, obj, shape, material, surface, g, chain, hub, axis, m, psi, events, ref impulse, ref angular, true, stones);
                        return struck;
                    }
                }
                rec.Stones = stones;
                double fling = row.Density * height * omega * omega * second * ratio * ratio * ratio;
                double crushing = row.CrushStrength * area * height * ratio;
                carveCrush = crushing;
                work = crushing + fling;
                rec.Fling = fling;
                // The soil the pass sweeps, flung from its centroid: its share reaches the motor's gap (p7f-damage 1.6b, MotorHealth).
                if (soil) _contacts.Health?.Carve(c, row.Density * area * height * ratio, rc, row.CrushStrength / BearingFactor, row.KineticFriction, row.Density);
                rec.Ratio = ratio;
                rec.Attack = attack;
                rec.Body = moving;
                rec.Motion = motion;
                // The soil's sand: its share of the cutting force on the leading edge along the path, by Archard (k·s/H_b per joule).
                wear = soil ? _sand[surface] * work : 0;
                rec.Wear = wear;
                // The face's impulse on the blade over the pass, (E_crush·g + E_fling·(−û))/|u|: the rotor pays its share along the
                // blade's own motion, −I_t·ω·r_c; the body the rest (the impulse on it, below), which a body moving through the soil
                // faster than the blades screw can even turn into the rotor's (the soil on the trailing face). The axial impulse is
                // the leading face's: 1.3's cot θ of the whole impulse gave the edge's and the fling's shares a face's push.
                // Both act over the pass's arc, t = 2α/ω; a pass slower than the step delivers only the step's share of the face's
                // push on the body and of a drive into the rotor (held to the step, as 1.3's hard push: a crawling blade's arc past
                // the step is the next steps'), so a slow blade in soil the body runs through is not one kick.
                double along = crushing * gt - fling * cb, hold = 2 * alpha > omega * FlightCore.Dt ? omega * FlightCore.Dt / (2 * alpha) : 1;
                rotorShare = -along / work * (omega * rc / speed);
                axial = (crushing * ga - fling * sb) / speed * hold;
                if (!_spec.TipSlide)
                {
                    // Model 43's carve (the known-wrong): the tangential impulse × cot θ of the whole drag (below).
                    DetMath.SinCos(theta, out double st0, out double ct0);
                    cot = ct0 / (st0 > 1e-12 ? st0 : 1e-12);
                }
                double cut = path > 0 ? DetMath.Abs(along) / (path * ratio) : 0;
                rec.PeakForce = cut;
                broke = Breaks(blade, f, rm, rc, cut, 0, out rec.PeakMoment, out rec.Margin);
                rotorWork = _spec.TipSlide ? work * rotorShare : work;
                if (rotorWork < 0) rotorWork *= hold;
                if (broke >= 0)
                {
                    mode = StrikeMode.Break;
                    newTip = _b.Stations[broke];
                    double fracture = _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
                    work += fracture;
                    rotorWork += fracture;
                }
                rec.SpeedIn = DetMath.Max(0, -Double3.Dot(nrm, motion * (omega * rtip) + at));
            }
            else if (!inside)
            {
                // A hard face past the tip: bend over it or crush back to it.
                alpha = DetMath.Acos(rm / rtip);
                DetMath.SinCos(alpha, out double sa, out double ca);
                double along = DetMath.Max(0, -Double3.Dot(nrm, dir)), tilt = rec.Tilt, depth = (rtip - rm) * along;
                double kb = _bladeInertia * (_flapFrequency * _flapFrequency + (_spec.FlapSouthwell - 1) * omega * omega);
                double kf = kb / (R * R), mtip = _bladeInertia / (R * R);
                int km = Station(rm / R);
                double fcrush = crush * _area[km] * blade.ChordShare[km];
                double fbend = tilt > 1e-12 ? kf * depth * DetMath.Sqrt(1 - tilt * tilt) / tilt : double.PositiveInfinity;
                rec.CrushForce = fcrush;
                rec.BendForce = fbend;
                Double3 entry = motion * ca + dir * sa;
                rec.SpeedIn = DetMath.Max(0, -Double3.Dot(nrm, entry * (omega * rtip) + at));
                if (fbend < fcrush)
                {
                    // Bend: the slap, then the slide and its wear.
                    mode = StrikeMode.Wear;
                    int kt = Station(DetMath.Max(rm, rtip - 1e-9) / R);
                    double flat = rec.SpeedIn / tilt;
                    double peak = DetMath.Min(flat * DetMath.Sqrt(kf * mtip), crush * _b.Chord[kt] * blade.ChordShare[kt] * _spec.SlapWidth);
                    rec.PeakForce = peak;
                    broke = Breaks(blade, f, rtip, rtip, 0, peak, out rec.PeakMoment, out rec.Margin);
                    if (broke >= 0)
                    {
                        mode = StrikeMode.Break;
                        newTip = _b.Stations[broke];
                        work = _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
                    }
                    else
                    {
                        set = Worst(blade, f, rtip, peak, out double moment, out double limit);
                        if (set >= 0 && moment > limit)
                        {
                            phi = (moment - limit) / (_spec.PlasticShare * _stiff[set] * blade.ChordShare[set]);
                            mode = StrikeMode.Bend;
                        }
                        else set = -1;
                        double slide = rtip * 2 * alpha;
                        work = mu * fbend * slide;
                        rec.Friction = work;
                        wear = _spec.WearCoefficient * fbend * slide / _b.Hardness;
                        rec.Wear = wear;
                        if (phi > 0) work += 0.5 * (rec.PeakMoment + limit) * phi;
                    }
                    pushN = fbend * Contact(2 * alpha, omega);
                }
                else
                {
                    // Crush back to the face; the face's force on the blade breaks a section first where it can.
                    mode = StrikeMode.Crush;
                    crushPast = true;
                    double slide = rtip * alpha;
                    work = crush * Volume(blade, rm, rtip) + mu * fcrush * slide;
                    rec.Friction = mu * fcrush * slide;
                    pushN = fcrush * Contact(alpha, omega);
                    Double3 across = motion - nrm * Double3.Dot(nrm, motion);
                    double al = across.Length();
                    Double3 force = nrm * fcrush - (al > 0 ? across * (mu * fcrush / al) : default);
                    rec.PeakForce = fcrush;
                    broke = Breaks(blade, f, rm, rm, DetMath.Abs(Double3.Dot(force, motion)), DetMath.Abs(Double3.Dot(force, axis)), out rec.PeakMoment, out rec.Margin);
                    if (broke >= 0)
                    {
                        mode = StrikeMode.Break;
                        newTip = _b.Stations[broke];
                        work = _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
                    }
                    else newTip = rm / R;
                }
            }
            else
            {
                // A hard shape inside the rim: the leading edge into it, across its width.
                double w = rout - rm;
                rc = 0.5 * (rm + rout);
                alpha = 0.5 * w / rc;
                int k = Station(rc / R);
                double force = crush * _b.Thickness[k] * w;
                rec.CrushForce = rec.PeakForce = force;
                rec.SpeedIn = DetMath.Max(0, omega * rc + Double3.Dot(at, motion));
                broke = Breaks(blade, f, rm, rc, force, 0, out rec.PeakMoment, out rec.Margin);
                if (broke >= 0)
                {
                    mode = StrikeMode.Break;
                    newTip = _b.Stations[broke];
                    work = _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
                }
                else
                {
                    mode = StrikeMode.Crush;
                    newTip = rm / R;
                    work = force * _b.Chord[k] * blade.ChordShare[k];
                }
            }
            rec.Station = broke >= 0 ? broke : set;
            if (double.IsNaN(rotorWork)) rotorWork = work;

            // The rotor's energy: a dip, or a stop on the face with the damage cut short (design 6). A carve's body share is the
            // body's (its impulse, below); a body moving through the soil faster than its blades screw can even drive the rotor.
            double energy = 0.5 * J * omega * omega, share = 1, after;
            bool jam = !(rotorWork < energy);
            if (jam)
            {
                share = rotorWork > 0 ? energy / rotorWork : 1;
                after = 0;
                if (mode == StrikeMode.Crush && !(hard && inside)) newTip = tip - share * (tip - newTip);
                if (mode == StrikeMode.Break)
                {
                    // Short of the fracture's work the section holds: the blade presses the face, crushing that share of its depth.
                    mode = StrikeMode.Crush;
                    newTip = hard && !inside ? tip - share * (tip - rm / R) : tip;
                    broke = -1;
                }
                if (hard && inside)
                {
                    // A hard shape inside the rim crushes the leading edge back through the chord against its force σ_c·t·w (1.3b):
                    // the rotor's energy reaches E/F of the chord left, a notch across the shape's width (design 6: the chip's size
                    // given the energy; 1.3 trimmed the tip by the crush's share and left a held break's section whole).
                    int kn = Station(rc / R);
                    notch = DetMath.Min(energy / rec.CrushForce, _b.Chord[kn] * blade.ChordShare[kn]);
                    newTip = tip;
                }
                phi *= share;
                wear *= share;
                // The rest of a jammed carve is carved at the motor's crawl, not at the jam's speed: only its crush is left (a
                // stopped blade flings nothing; the tip slide), the rotor's share of it.
                double rest = _spec.TipSlide ? carveCrush * rotorShare : rotorWork;
                if (mode == StrikeMode.Carve && rest > 0)
                {
                    _pending[c] = new Pending
                    {
                        Active = true, Object = obj, Shape = shape, Blade = b, Left = _spec.TipSlide ? rest * (1 - share) : rotorWork - energy, Work = rest,
                        Entry = psi - alpha, Psi = psi,
                        WearRate = soil ? _sand[surface] : 0, From = rm / R, Material = material, Surface = surface, Point = m.Point,
                    };
                }
            }
            else after = DetMath.Sqrt(omega * omega - 2 * rotorWork / J);
            double used = jam ? energy : rotorWork;

            // The impulses: the reaction about the axis, the tangential one at the meeting point, the face's push (a hard face's
            // along n, a carve's along the axis: its face's share, Section).
            double lost = J * (omega - after), tangential = rc > 0 ? lost / rc : 0;
            Double3 external = motion * -tangential + nrm * (pushN * share)
                + axis * (double.IsNaN(cot) ? axial * share : tangential * cot);
            Double3 reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(m.Point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            now.RotorSpeed[c] = after;
            if (jam) Hold(ref now, c, b, mode == StrikeMode.Carve ? psi - alpha + alpha * share : psi - Entry(rm, newTip * R, alpha, inside));

            // The damage.
            double before = Mass(blade);
            bool damaged = false;
            if (notch > 0 && Notch(blade, rc, rec.Exit - rm, notch, out double lo))
            {
                damaged = true;
                Damaged(i, d, b, StrikeMode.Crush, obj, shape, material, surface, m.Point, blade.TipRadius, before - Mass(blade), lo, events);
                before = Mass(blade);
            }
            if (newTip < tip)
            {
                blade.Trim(newTip);
                damaged = true;
                Damaged(i, d, b, mode == StrikeMode.Break ? StrikeMode.Break : StrikeMode.Crush, obj, shape, material, surface, m.Point, blade.TipRadius,
                    before - Mass(blade), newTip, events);
                before = Mass(blade);
            }
            if (phi > 0 && set >= 0)
            {
                double sign = Double3.Dot(nrm, axis) < 0 ? -1 : 1;
                for (int j = set + 1; j < _stations; j++)
                    blade.FlapSet[j] += sign * phi * (_b.Stations[j] - _b.Stations[set]) * R;
                damaged = true;
                Damaged(i, d, b, StrikeMode.Bend, obj, shape, material, surface, m.Point, blade.TipRadius, 0, _b.Stations[set], events);
            }
            if (wear > 0 && Wear(blade, rm / R, wear))
            {
                damaged = true;
                Damaged(i, d, b, StrikeMode.Wear, obj, shape, material, surface, m.Point, blade.TipRadius, before - Mass(blade), rm / R, events);
            }

            rec.Mode = mode;
            rec.Work = work;
            rec.Used = used;
            rec.SpeedAfter = after;
            rec.TipAfter = blade.TipRadius;
            rec.Jammed = jam;
            rec.Impulse = external;
            rec.Reaction = reaction;
            Strike(i, d, b, obj, shape, material, surface, rec, events);
            if (jam)
            {
                FlightEvent e = Of(EventKind.RotorJammed, d, obj, shape, material, surface, m.Point);
                e.Index = (byte)c;
                e.A = rotorWork;
                e.B = energy;
                e.C = share;
                events.Add(e);
            }
            // The hub's load (p7f-damage 1.6, design 9): the face's impulse over the pass's contact time, its arc over ω held to the
            // step as its push is, at the meeting point, into the shaft and its bearings; at most the pass's peak force (a break's
            // at the force its section failed at, the peak over its margin), along the span at most the blade's column
            // (MotorShaft.ColumnLoad) and across it at most what turns the rotor, its lost angular momentum over the contact time
            // and its motor's stalled drive, over the radius.
            MotorShaft shafts = _contacts.Shafts;
            if (shafts != null && omega > 0)
            {
                FlightEvent cause = new FlightEvent { Object = obj, Shape = shape, Material = material, Surface = surface, Point = m.Point };
                double peak = broke >= 0 && rec.Margin > 1 ? rec.PeakForce / rec.Margin : rec.PeakForce;
                shafts.Strike(c, now.Attitude.InverseRotate(m.Point - now.Position), now.Attitude.InverseRotate(external),
                    Contact(crushPast ? alpha : 2 * alpha, omega), peak, DetMath.Abs(lost), Wrap(psi - TwoPi * b / _blades), omega, events, cause);
            }
            Slot(ref now, c, b, material, used, external.Length(), rm);
            Last = rec;
            Record?.Add(rec);
            Passes++;
            return damaged;
        }

        // A soil carve's pass meets a stone (the tip slide, p7f-damage 1.3): the soil's rock fragments (surfaces.json's stone
        // fraction f_s and diameter w) sit in it at random, so the pass meets one with the probability 1 − e^(−λ), λ = f_s·V/(πw³/6),
        // V the volume within a stone's radius of what it sweeps: (h + w)·(A + w·path/2)·(its speed over its own), the carve's
        // frontal height and area grown by a stone (its centre in the soil, so past the tip and not over the chord), drawn from
        // "soil.stones" in Pass, before the carve (PHY-24: the stones' number in a pass is the soil's fraction, a physical
        // parameter; where and what the ground is stays the world's). The first stone only (assumed: a later one meets an edge
        // the first has struck). It sits at the carve's centroid r_c on the leading edge, a hard target by quartz's hardness, met at the blade's speed through
        // the soil there, u: 1.3's head-on rule, F = σ_c·t·w (the leading edge crushed across it), held to what the stone's
        // mass can react, u·√(k·m_s) (an elastic impact on the edge's contact stiffness k = E_b·w, Boussinesq's rigid punch's
        // order with ν² left out: assumed; a slow blade only pushes a stone aside); F bends the inner stations edgewise and
        // breaks the first that fails; else, at the crush, the leading edge is crushed until the stone has the blade's speed,
        // a notch ½·m_s·u²/F deep across w (through the chord: the blade severed at r_c); the stone is flung at the blade's
        // speed, m_s·u² (half its energy, half the crush's), with a break's fracture G_c·A. The rotor pays it (a stop on the
        // stone without the break, as 1.3's), the body takes its tangential impulse. True when it changed the blade.
        bool Stone(int i, in ContactShape d, int c, int b, ref FlightState now, in MassProperties body, int obj, int shape, ushort material, byte surface,
            Double3 hub, Double3 dir, Double3 motion, double rc, double stones, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            double w = _stoneSize[surface], R = _radius;
            BladeState blade = _propulsion.Rotor(c).Blade(b);
            double tip = blade.TipRadius, omega = now.RotorSpeed[c];
            if (!(rc < tip * R) || !(omega > 0)) return false;
            Double3 point = hub + dir * rc;
            double u = omega * rc + Double3.Dot(PointVelocity(now, point), motion);
            if (!(u > 0)) return false;
            double J = _propulsion.Inertia(c), s = _spin[c], f = _strength[c * _blades + b];
            int k = Station(rc / R);
            double ms = _stoneDensity * Pi * w * w * w / 6;
            double crush = _b.CrushStrength * f * _b.Thickness[k] * w, elastic = u * DetMath.Sqrt(_b.FlexuralModulus * w * ms);
            double force = DetMath.Min(crush, elastic);
            var rec = new PassRecord
            {
                Corner = c, Blade = b, Mode = StrikeMode.Glance, Radius = rc, Exit = w, SpeedBefore = omega, Inertia = J, TipBefore = tip, TipAfter = tip,
                Point = point, Station = -1, Stone = true, SpeedIn = u, CrushForce = crush, PeakForce = force, Motion = motion, Stones = stones,
            };
            int broke = Breaks(blade, f, rc, rc, force, 0, out rec.PeakMoment, out rec.Margin);
            double work = ms * u * u, notch = 0, newTip = tip;
            if (broke >= 0)
            {
                rec.Mode = StrikeMode.Break;
                newTip = _b.Stations[broke];
                work += _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
            }
            else if (!(elastic < crush))
            {
                rec.Mode = StrikeMode.Crush;
                notch = 0.5 * ms * u * u / crush;
            }
            double energy = 0.5 * J * omega * omega, share = 1, after;
            bool jam = !(work < energy);
            if (jam)
            {
                share = energy / work;
                after = 0;
                if (rec.Mode == StrikeMode.Break)
                {
                    // Short of the fracture's work the section holds: the edge is crushed as far as the energy took the stone.
                    rec.Mode = StrikeMode.Crush;
                    broke = -1;
                    newTip = tip;
                    notch = 0.5 * ms * u * u / crush;
                }
                notch *= share;
            }
            else after = DetMath.Sqrt(omega * omega - 2 * work / J);
            rec.Station = broke;

            // The impulses: the rotor's lost angular momentum, the stone's push against the blade's motion at r_c.
            double lost = J * (omega - after), tangential = lost / rc;
            Double3 external = motion * -tangential;
            Double3 reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            now.RotorSpeed[c] = after;

            // The damage: a break, or the notch (or the blade severed where the notch passes its chord).
            double before = Mass(blade);
            bool damaged = false;
            if (notch > 0)
            {
                double left = _b.Chord[k] * blade.ChordShare[k];
                if (!(notch < left)) newTip = rc / R;
                else if (Notch(blade, rc, w, notch, out double lo))
                {
                    damaged = true;
                    Damaged(i, d, b, StrikeMode.Crush, obj, shape, material, surface, point, blade.TipRadius, before - Mass(blade), lo, events);
                }
            }
            if (newTip < tip)
            {
                blade.Trim(newTip);
                damaged = true;
                Damaged(i, d, b, rec.Mode == StrikeMode.Break ? StrikeMode.Break : StrikeMode.Crush, obj, shape, material, surface, point, blade.TipRadius,
                    before - Mass(blade), newTip, events);
            }
            double used = jam ? energy : work;
            rec.Work = work;
            rec.Used = used;
            rec.SpeedAfter = after;
            rec.TipAfter = blade.TipRadius;
            rec.Jammed = jam;
            rec.Impulse = external;
            rec.Reaction = reaction;
            FlightEvent e = Of(EventKind.StoneStrike, d, obj, shape, material, surface, point);
            e.Index = (byte)(b | ((int)rec.Mode << 4));
            e.A = used;
            e.B = w;
            e.C = u;
            events.Add(e);
            if (jam)
            {
                FlightEvent j = Of(EventKind.RotorJammed, d, obj, shape, material, surface, point);
                j.Index = (byte)c;
                j.A = work;
                j.B = energy;
                j.C = share;
                events.Add(j);
            }
            Slot(ref now, c, b, material, used, external.Length(), rc);
            Last = rec;
            Record?.Add(rec);
            return damaged;
        }

        // A rotor stopped in a carve: the motor's spin-up pays the work still to do (design 6), and in soil its sand wears the
        // blade by that work; true when the grind's end changed the blade.
        bool Grind(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, StepEvents events,
            ref Double3 impulse, ref Double3 angular)
        {
            ref Pending p = ref _pending[c];
            int pairs = _contacts.DiscPairs(i), at = -1;
            ushort material = 0;
            byte surface = 0;
            for (int k = 0; k < pairs && at < 0; k++)
            {
                _contacts.DiscPair(i, k, out int obj, out int shape, out material, out surface);
                if (obj == p.Object && shape == p.Shape) at = k;
            }
            BladeState blade = _propulsion.Rotor(c).Blade(p.Blade);
            BladeCut.Meeting m = default;
            bool still = at >= 0 && Geometry(i, at, p.Object, p.Shape, surface, hub, d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)
                && BladeCut.Nearest(g, chain, hub, axis, d.HalfThickness, d.Radius, out m) && m.Radius < blade.TipRadius * _radius;
            if (!still) return Finish(i, d, ref p, events);
            double omega = now.RotorSpeed[c], J = _propulsion.Inertia(c), s = _spin[c], energy = 0.5 * J * omega * omega;
            double used = DetMath.Min(energy, p.Left), after = energy > p.Left ? DetMath.Sqrt(omega * omega - 2 * p.Left / J) : 0;
            p.Left -= used;
            p.Worn += p.WearRate * used;
            double rtip = blade.TipRadius * _radius, rc = 0.5 * (m.Radius + rtip);
            int ks = Station(rc / _radius);
            double chord = _b.Chord[ks] * blade.ChordShare[ks], thick = _b.Thickness[ks];
            double theta = _b.SectionAngle[ks] + blade.FactoryPitch + blade.PitchSet[ks];
            DetMath.SinCos(theta, out double st, out double ct);
            Double3 motion = Double3.Cross(axis, m.Direction) * -s;
            double lost = J * (omega - after), tangential = lost / rc;
            // The spin-up's push on the pitched face: its face's share along the axis, c·cos θ/h of the tangential (Section's).
            Double3 external = motion * -tangential + axis * (_spec.TipSlide ? tangential * chord * ct / (chord * st + thick * ct) : tangential * ct / (st > 1e-12 ? st : 1e-12));
            Double3 reaction = new Double3(0, -s * lost, 0);

            // The plough (the tip slide): the held blade, stopped in the soil, meets it at the body's speed there as the body moves
            // (the carve's relative speed with the blade's own 0): (p + ρ·u²) over its frontal area across that motion, h·L, L its
            // span in the soil, from the face's chord along the held blade to its tip, at its span's middle; the crush on the leading
            // face and edge, the fling along the motion, as a pass's. The impulse over the step is held to what stops the blade's
            // point along its motion (Coulomb's clamp: the soil's crush resists a slide, it never drives one back). The load is
            // held, not a pass's 0.3–0.7 ms against the blade's 6 ms flap mode, so its edgewise and flatwise bending are checked
            // as they stand (1.3's sections): a station past its strength breaks.
            DetMath.SinCos(now.RotorAngle[c] + TwoPi * p.Blade / _blades, out double sp, out double cp);
            Double3 bladeDir = now.Attitude.Rotate(new Double3(cp, 0, s * sp));
            double reach = Double3.Dot(bladeDir, m.Direction), ploughed = 0;
            Double3 plough = default, pushed = default;
            int broke = -1;
            double moment = 0, margin = 0;
            if (_spec.TipSlide && reach * rtip > m.Radius && Face(p.Object, p.Material, p.Surface, out MaterialRow row))
            {
                double rin = m.Radius / reach, rp = 0.5 * (rin + rtip);
                int kp = Station(rp / _radius);
                double cpl = _b.Chord[kp] * blade.ChordShare[kp], tp = _b.Thickness[kp];
                double thp = _b.SectionAngle[kp] + blade.FactoryPitch + blade.PitchSet[kp];
                Double3 motionP = Double3.Cross(axis, bladeDir) * -s;
                pushed = hub + bladeDir * rp;
                Double3 moving = PointVelocity(now, pushed);
                double ut = Double3.Dot(moving, motionP), ua = Double3.Dot(moving, axis), speed = DetMath.Sqrt(ut * ut + ua * ua);
                if (speed > 0)
                {
                    // The leading face's normal pressure, p·c·L along its inward normal, and the edge's crush and the fling along −û,
                    // each held to what stops the blade's point along its own direction (the face's normal approach u·|sin α|, the
                    // motion's u): a plastic pressure never pushes the blade back out.
                    Section(cpl, tp, thp, ut, ua, speed, out double height, out _, out _, out double attack);
                    DetMath.SinCos(attack, out double sa, out double ca);
                    DetMath.SinCos(thp, out double st2, out double ct2);
                    double side = sa < 0 ? -1 : 1, span = rtip - rin;
                    double normal = row.CrushStrength * cpl * span, drag = (row.CrushStrength * tp * DetMath.Abs(ca) + row.Density * speed * speed * height) * span;
                    Double3 lead = motionP * (side * st2) - axis * (side * ct2), along = motionP * (ut / speed) + axis * (ua / speed);
                    double jn = Held(normal * FlightCore.Dt, speed * DetMath.Abs(sa), lead, pushed, now, body);
                    double jd = Held(drag * FlightCore.Dt, speed, along, pushed, now, body);
                    plough = lead * -jn + along * -jd;
                    ploughed = jn * speed * DetMath.Abs(sa) + jd * speed;
                    double ft = Double3.Dot(plough, motionP) / FlightCore.Dt, fa = Double3.Dot(plough, axis) / FlightCore.Dt; // the soil's force on the blade, N
                    broke = Breaks(blade, _strength[c * _blades + p.Blade], rin, rp, DetMath.Abs(ft), DetMath.Abs(fa), out moment, out margin);
                }
            }
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(m.Point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            if (ploughed > 0)
            {
                impulse = impulse + plough;
                angular = angular + Double3.Cross(now.Attitude.InverseRotate(pushed - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(plough));
                external = external + plough;
            }
            now.RotorSpeed[c] = after;
            bool through = !(p.Left > 0);
            Hold(ref now, c, p.Blade, through ? p.Psi : p.Entry + (p.Psi - p.Entry) * (1 - p.Left / p.Work));
            var rec = new PassRecord
            {
                Corner = c, Blade = p.Blade, Mode = broke >= 0 ? StrikeMode.Break : StrikeMode.Carve, Radius = m.Radius, Exit = double.PositiveInfinity,
                Work = p.Work, Used = used, SpeedBefore = omega, SpeedAfter = after, Inertia = J, TipBefore = blade.TipRadius, TipAfter = blade.TipRadius,
                Point = m.Point, Normal = m.Normal, Impulse = external, Reaction = reaction, Jammed = !through && broke < 0, Station = broke, Grind = true,
                Plough = ploughed, PeakMoment = moment, Margin = margin, Motion = motion,
            };
            bool changed = false;
            if (broke >= 0)
            {
                // The held blade snaps where its section fails: the rotor is free of the face.
                double before = Mass(blade);
                blade.Trim(_b.Stations[broke]);
                rec.TipAfter = blade.TipRadius;
                Damaged(i, d, p.Blade, StrikeMode.Break, p.Object, p.Shape, material, surface, m.Point, blade.TipRadius, before - Mass(blade),
                    blade.TipRadius, events);
                changed = true;
                through = true;
            }
            // The step is logged when it pays work from the rotor or breaks the blade; the plough's work is in the step's slot.
            if (used > 0 || broke >= 0 || !_spec.TipSlide) Strike(i, d, p.Blade, p.Object, p.Shape, material, surface, rec, events);
            Slot(ref now, c, p.Blade, material, used + ploughed, external.Length(), m.Radius);
            Last = rec;
            Record?.Add(rec);
            Passes++;
            return (through && Finish(i, d, ref p, events)) || changed;
        }

        // A carve's grind ends, through or its shape gone: the sand's wear over the work it paid goes on the blade, from where the
        // carve met it, with one BladeDamaged; true when that changed the blade.
        bool Finish(int i, in ContactShape d, ref Pending p, StepEvents events)
        {
            p.Active = false;
            if (!(p.Worn > 0)) return false;
            BladeState blade = _propulsion.Rotor(d.Corner).Blade(p.Blade);
            double before = Mass(blade);
            if (!Wear(blade, p.From, p.Worn)) return false;
            Damaged(i, d, p.Blade, StrikeMode.Wear, p.Object, p.Shape, p.Material, p.Surface, p.Point, blade.TipRadius, before - Mass(blade), p.From, events);
            return true;
        }

        // What a blade meets on a pair: a surface's set (the ground, a soil shape) or a material's row; false for none, a Glance
        // (the ground and the soil without surfaces.json, a thin compliant row without crush strength, a material the table lacks).
        // ---- A crown's twigs (w3-farmland-ground 2.7) --------------------------------------------------------------------

        /// <summary>The rows whose shells stand for a crown's twigs (PhysicsMaterials' name lists' way): their twigs' length per
        /// volume and diameter, and their leaves, are the row's crown block (physics_materials.json's twig.crown, each value labelled
        /// there; w9-village-physics 2.3, which moved 2.7's CrownTwigLength 2.0 m/m³ and CrownTwigDiameter 7.5 mm there).</summary>
        public static readonly string[] CrownRows = { "twig" };

        // A crown's twig shell, with the twigs' tissue to cut (surfaces.json): its twigs are met (Twigs); without the table a glance.
        bool Crown(byte surface, ushort material) => surface == WorldIds.NoSurface && material < _crown.Length && _crown[material] != null && _twig.CutEnergy > 0;

        /// <summary>
        /// A rotor turning in a crown's twigs (w3-farmland-ground 2.7): the shells stand for twigs of the crown block's diameter at
        /// its length per m³, crossing the disc's plane at n_A = L_V/2 a square metre (isotropic lines: stereology's
        /// P_A = L_V/2). A blade cuts each crossing it meets, and the cut twig no longer crosses there, so a pass meets only what
        /// the disc's motion through the crown brought in since the last: across the plane, the immersed region's width times
        /// its move (the chord's into the shell, the segment's height along it; the disc's diameter with the hub inside), and
        /// along the axis, half the crossings (those rooted ahead) sliced again, a slice thinner than the twig costing its share
        /// (a twig's end crushed rather than cut, e_s/d of the order of green wood's crush, derived), at most every crossing
        /// each pass. A disc held still in a crown cuts its way clear and then meets nothing; one driven through it meets twig
        /// after twig. The count is the mean's, with no draw: the rotor meets its next twig when the crown it has swept holds one
        /// more (_twigDue), the blades in turn. The deepest shell's meeting stands for their union (they overlap, 7–10 at a point).
        /// Each twig, at the blade's speed u = ω·r_c at the immersed region's mean radius: its cut, E_cut = e_s·πd²/4, and its
        /// mass within one chord brought to the blade's speed, m_f·u² (p7a-ground design 9); the cutting force E_cut/d along the
        /// blade's motion bends its inner stations edgewise and breaks the first that fails (p7f-damage design 7), else the
        /// twig is cut and the blade is whole. The rotor pays the work (a stop when it has less, the twig then cut by the
        /// motor's next turns), the body the twig's impulse and the reaction, as a pass's. True when it changed a blade.
        /// </summary>
        bool Twigs(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, in BladeCut.Meeting m,
            int obj, int shape, ushort material, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            double R = _radius, rtip = 0, rm = m.Radius;
            for (int b = 0; b < _blades; b++) rtip = DetMath.Max(rtip, _propulsion.Rotor(c).Blade(b).TipRadius * R);
            if (!(rm < rtip)) return false;
            double area, width, rc, along;
            Double3 v = PointVelocity(now, hub), dir = m.Direction;
            double va = Double3.Dot(v, axis);
            Double3 across = v - axis * va;
            if (rm > 0)
            {
                double alpha = DetMath.Acos(rm / rtip);
                DetMath.SinCos(alpha, out double sa, out double ca);
                area = rtip * rtip * (alpha - sa * ca);
                width = 2 * rtip * sa;
                rc = 0.5 * (rm + rtip);
                along = width * DetMath.Max(0, Double3.Dot(across, dir)) + (rtip - rm) * DetMath.Abs(Double3.Dot(across, Double3.Cross(axis, dir)));
            }
            else
            {
                area = Pi * rtip * rtip;
                width = 2 * rtip;
                rc = 2 * rtip / 3;
                along = width * across.Length();
            }
            CrownRow k = _crown[material];
            double omega = now.RotorSpeed[c], passes = omega * _blades * FlightCore.Dt / TwoPi, nA = 0.5 * k.TwigLength;
            double swept = nA * (along * FlightCore.Dt + 0.5 * area * DetMath.Min(passes, DetMath.Abs(va) * FlightCore.Dt / k.TwigDiameter));
            _twigDue[c] += DetMath.Min(swept, nA * area * passes);
            bool changed = false;
            while (_twigDue[c] >= 1 && now.RotorSpeed[c] > 0)
            {
                _twigDue[c] -= 1;
                int b = _twigBlade[c];
                _twigBlade[c] = (b + 1) % _blades;
                if (Twig(i, d, c, b, ref now, body, hub, axis, dir, rm, rc, obj, shape, material, events, ref impulse, ref angular)) changed = true;
            }
            return changed;
        }

        // One twig met by blade b at r_c (Twigs).
        bool Twig(int i, in ContactShape d, int c, int b, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, Double3 dir, double rm,
            double rc, int obj, int shape, ushort material, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            BladeState blade = _propulsion.Rotor(c).Blade(b);
            double R = _radius, tip = blade.TipRadius, rtip = tip * R;
            if (!(rm < rtip)) return false; // this blade, cut back, no longer reaches the twigs
            if (!(rc < rtip)) rc = 0.5 * (rm + rtip);
            double omega = now.RotorSpeed[c], J = _propulsion.Inertia(c), s = _spin[c], f = _strength[c * _blades + b];
            int k = Station(rc / R);
            double twig = _crown[material].TwigDiameter, section = Pi * twig * twig / 4, chord = _b.Chord[k] * blade.ChordShare[k];
            double cut = _twig.CutEnergy * section, speed = omega * rc, fling = _twig.Density * section * chord * speed * speed;
            double force = cut / twig, work = cut + fling;
            int broke = Breaks(blade, f, rc, rc, force, 0, out double moment, out double margin);
            double newTip = tip;
            if (broke >= 0)
            {
                newTip = _b.Stations[broke];
                work += _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
            }
            double energy = 0.5 * J * omega * omega;
            bool jam = !(work < energy);
            if (jam && broke >= 0)
            {
                broke = -1; // short of the fracture's work the section holds: the twig stops the rotor first
                newTip = tip;
            }
            double after = jam ? 0 : DetMath.Sqrt(omega * omega - 2 * work / J), used = jam ? energy : work;
            Double3 point = hub + dir * rc, motion = Double3.Cross(axis, dir) * -s;
            double lost = J * (omega - after);
            Double3 external = motion * -(lost / rc), reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            now.RotorSpeed[c] = after;
            bool damaged = false;
            if (broke >= 0)
            {
                double before = Mass(blade);
                blade.Trim(newTip);
                damaged = true;
                Damaged(i, d, b, StrikeMode.Break, obj, shape, material, WorldIds.NoSurface, point, blade.TipRadius, before - Mass(blade), newTip, events);
            }
            var rec = new PassRecord
            {
                Corner = c, Blade = b, Mode = broke >= 0 ? StrikeMode.Break : StrikeMode.Carve, Radius = rc, Exit = twig, Work = work, Used = used,
                SpeedBefore = omega, SpeedAfter = after, Inertia = J, TipBefore = tip, TipAfter = blade.TipRadius, SpeedIn = speed, Fling = fling,
                PeakForce = force, PeakMoment = moment, Margin = margin, Station = broke, Jammed = jam, Impulse = external, Reaction = reaction, Point = point,
                Normal = -dir, Motion = motion,
            };
            Strike(i, d, b, obj, shape, material, WorldIds.NoSurface, rec, events);
            if (jam)
            {
                FlightEvent e = Of(EventKind.RotorJammed, d, obj, shape, material, WorldIds.NoSurface, point);
                e.Index = (byte)c;
                e.A = work;
                e.B = energy;
                e.C = energy / work;
                events.Add(e);
            }
            int slot = BladeStrikes.Slot(c, b);
            if (slot < BladeStrikes.Slots)
            {
                BladeStrike strike = now.BladeStrikes[slot];
                strike.Add(StrikeKind.Element, material, used, external.Length(), twig, cut, rc);
                now.BladeStrikes[slot] = strike;
            }
            Last = rec;
            Record?.Add(rec);
            Passes++;
            return damaged;
        }

        // ---- A crown's leaves (w9-village-physics 2.3, design.md 2.3 §4) --------------------------------------------------------

        /// <summary>The cleared cells' store: a crown's cubes (the crown block's leaf_cell_m) a disc's plane has passed, keyed by
        /// (crown, cube) (numerical: 16,384 slots, probed 8 deep, the home slot overwritten when full, as 2.12's stems' store).</summary>
        public const int LeafSlots = 16384, LeafProbe = 8;

        ulong[] _leafCells;            // the cleared cubes' keys (0: empty)
        double[] _leafX, _leafZ;       // the disc's samples, body x and z about the hub: a grid at the cell's spacing within R, then its rim
        double _leafArea;              // the disc's area each sample stands for: the cell's square, m²
        double[] _lad;                 // each placed object's leaf area density (its variant's), m²/m³; 0 past the placed ones
        readonly int[] _leafBlade = new int[4]; // the blade each rotor meets its next leaf cell with
        ushort _foliage;               // the world's foliage material (the leaves' strikes are logged on it), or the twig's

        /// <summary>The leaves met so far: the fresh cells, their work (J), and the work's cut share (J).</summary>
        public long LeafCells { get; private set; }

        public double LeafWork { get; private set; }

        public double LeafCutWork { get; private set; }

        readonly double[] _leafWork = new double[4];

        /// <summary>The leaves' work so far on rotor c, J.</summary>
        public double LeafWorkOn(int corner) => _leafWork[corner];

        void InitLeaves(IWorldQuery q)
        {
            CrownRow any = null;
            foreach (CrownRow k in _crown)
                if (k != null) any = k;
            if (any == null) return;
            _leafCells = new ulong[LeafSlots];
            // The samples: the plane every cell within R (what a disc moving along its axis meets), and its rim every cell of arc
            // (where a disc moving in its plane meets its fresh leaves: they enter it there and the tips take them), so the grid's
            // own points trail in cubes the rim has cleared.
            double h = any.LeafCell;
            _leafArea = h * h;
            int n = 0, side = (int)(_radius / h), rim = (int)(2 * Pi * _radius / h) + 1;
            for (int a = -side; a <= side; a++)
                for (int b = -side; b <= side; b++)
                    if ((a * a + b * b) * h * h < _radius * _radius) n++;
            _leafX = new double[n + rim];
            _leafZ = new double[n + rim];
            n = 0;
            for (int a = -side; a <= side; a++)
                for (int b = -side; b <= side; b++)
                    if ((a * a + b * b) * h * h < _radius * _radius)
                    {
                        _leafX[n] = a * h;
                        _leafZ[n++] = b * h;
                    }
            for (int j = 0; j < rim; j++)
            {
                DetMath.SinCos(2 * Pi * j / rim, out double sj, out double cj);
                _leafX[n] = _radius * cj;
                _leafZ[n++] = _radius * sj;
            }
            int objects = 0;
            while (q.ObjectName(objects) != null) objects++;
            _lad = new double[objects];
            for (int o = 0; o < objects; o++) _lad[o] = any.LeafAreaDensityOf(q.ObjectName(o));
            _foliage = q.MaterialId("foliage");
        }

        // A cube of the crown, cleared now: true when no disc had passed it (it is then kept).
        bool Fresh(int obj, long x, long y, long z)
        {
            ulong key = (ulong)FlightRandom.Mix64((ulong)(uint)obj ^ ((ulong)x * 0x9E3779B97F4A7C15UL) ^ ((ulong)y * 0xC2B2AE3D27D4EB4FUL)
                ^ ((ulong)z * 0x165667B19E3779F9UL)) | 1;
            int home = (int)(key % LeafSlots);
            for (int j = 0; j < LeafProbe; j++)
            {
                int at = (home + j) % LeafSlots;
                if (_leafCells[at] == key) return false;
                if (_leafCells[at] == 0)
                {
                    _leafCells[at] = key;
                    return true;
                }
            }
            _leafCells[home] = key;
            return true;
        }

        /// <summary>
        /// A rotor turning in a crown's leaves (design.md 2.3 §4): each disc sample in the immersed region (past the deepest shell's
        /// chord, Twigs') lies in a world cube of the crown block's cell; a cube no disc had passed is fresh, met now and kept
        /// cleared. Its leaves at the sample's radius r, the blade's speed u = ω·r: the blade's section strikes the leaf area
        /// LAD·A_c·t (A_c the sample's share of the disc, t its swept slab: the frontal height h_f moving in the plane, the cube
        /// moving along the axis, t = h_f + (s − h_f)·|v̂·â|), flinging its fresh mass to u (m_A·LAD·A_c·t·u²) and, where u reaches
        /// the leaves' critical speed, cutting it at the work to shear over its trace, (π/4)·LAD·A_c a slab, t/h_f slabs
        /// (stereology). The rotor pays it, the body takes the in-plane impulse and the reaction at the sample (Twig's accounting),
        /// each cube by the next blade in turn; one BladeStrike a step for the rotor's leaves, on the shell with the world's foliage.
        /// </summary>
        void Leaves(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, in BladeCut.Meeting m,
            int obj, int shape, ushort material, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            if (_leafCells == null || obj < 0 || obj >= _lad.Length || !(_lad[obj] > 0)) return;
            CrownRow k = _crown[material];
            if (k == null) return;
            double lad = _lad[obj], cell = k.LeafCell, area = _leafArea, mass = k.LeafFreshMass, rm = m.Radius;
            Double3 v = PointVelocity(now, hub);
            double speed = v.Length(), along = speed > 0 ? DetMath.Abs(Double3.Dot(v, axis)) / speed : 0;
            double J = _propulsion.Inertia(c), s = _spin[c], used = 0, cutWork = 0, weighted = 0;
            Double3 sumImpulse = default;
            int met = 0, last = -1;
            for (int j = 0; j < _leafX.Length && now.RotorSpeed[c] > 0; j++)
            {
                Double3 offset = now.Attitude.Rotate(new Double3(_leafX[j], 0, _leafZ[j]));
                double r = DetMath.Sqrt(_leafX[j] * _leafX[j] + _leafZ[j] * _leafZ[j]);
                if (Double3.Dot(offset, m.Direction) < rm) continue; // short of the crown's chord
                Double3 at = hub + offset;
                if (!Fresh(obj, Floor(at.X / cell), Floor(at.Y / cell), Floor(at.Z / cell))) continue;
                int b = _leafBlade[c];
                _leafBlade[c] = (b + 1) % _blades;
                BladeState blade = _propulsion.Rotor(c).Blade(b);
                double rr = DetMath.Max(r, (_b.Stations[0] - 0.5 * _span) * _radius); // the hub's centre sample: at the blade's root
                if (rr > blade.TipRadius * _radius) continue; // this blade, cut back, no longer reaches it
                int st = Station(rr / _radius);
                DetMath.SinCos(_b.SectionAngle[st] + blade.FactoryPitch + blade.PitchSet[st], out double sa, out double ca);
                double hf = _b.Chord[st] * blade.ChordShare[st] * sa + _b.Thickness[st] * ca, t = hf + (cell - hf) * along;
                double omega = now.RotorSpeed[c], u = omega * rr, leaf = lad * area * t;
                double cut = u >= k.LeafCriticalSpeed ? k.LeafWork * 0.7853981633974483 * lad * area * (t / hf) : 0;
                double work = mass * leaf * u * u + cut, energy = 0.5 * J * omega * omega;
                double after = work < energy ? DetMath.Sqrt(omega * omega - 2 * work / J) : 0, lost = J * (omega - after);
                now.RotorSpeed[c] = after;
                Double3 dir = offset * (1 / DetMath.Max(r, 1e-9)), motion = Double3.Cross(axis, dir) * -s, point = hub + offset;
                Double3 external = motion * -(lost / rr), reaction = new Double3(0, -s * lost, 0);
                impulse = impulse + external;
                angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
                double paid = work < energy ? work : energy;
                used += paid;
                cutWork += work < energy ? cut : cut * energy / work;
                weighted += paid * rr;
                sumImpulse = sumImpulse + external;
                met++;
                last = b;
                int slot = BladeStrikes.Slot(c, b);
                if (slot < BladeStrikes.Slots)
                {
                    BladeStrike strike = now.BladeStrikes[slot];
                    strike.Add(StrikeKind.Element, _foliage != WorldIds.NoMaterial ? _foliage : material, paid, external.Length(), 0, cut, rr);
                    now.BladeStrikes[slot] = strike;
                }
            }
            if (met == 0) return;
            LeafCells += met;
            LeafWork += used;
            _leafWork[c] += used;
            LeafCutWork += cutWork;
            double rc = used > 0 ? weighted / used : 0;
            var rec = new PassRecord
            {
                Corner = c, Blade = last, Mode = StrikeMode.Carve, Radius = rc, Exit = 0, Work = used, Used = used, SpeedAfter = now.RotorSpeed[c],
                Inertia = J, SpeedIn = now.RotorSpeed[c] * rc, Impulse = sumImpulse, Point = hub + m.Direction * rc, Normal = -m.Direction, Fling = used - cutWork,
                Station = -1,
            };
            Strike(i, d, last, obj, shape, _foliage != WorldIds.NoMaterial ? _foliage : material, WorldIds.NoSurface, rec, events);
            Last = rec;
            Record?.Add(rec);
            Passes++;
        }

        bool Face(int obj, ushort material, byte surface, out MaterialRow row)
        {
            bool soil = surface != WorldIds.NoSurface;
            bool known = soil ? _surfaces[surface].Name != null : obj != WorldIds.Ground && material < _known.Length && _known[material];
            row = !known ? default : soil ? _surfaces[surface] : _rows[material];
            return known && row.CrushStrength > 0;
        }

        // A blade section of chord c, thickness t and pitch θ (its chord line's, the leading edge toward +axis) moving through a
        // softer face at (u_t, u_a) relative to it, along the blade's motion and the rotor's axis (|u| its speed): its attack α = θ
        // − atan2(u_a, u_t), its frontal height across that motion h = c·|sin α| + t·|cos α| (design 5's, at the relative
        // speed), and the face's crushing force on it per newton of that crush's drag along −u, (g_t, g_a): the leading face (the
        // lower one at a positive attack) pressed along its normal at the face's bearing pressure, c/h of the drag (its share
        // c·|sin α|/h along −u), and the leading edge pushed along −u, t·|cos α|/h (the edge pushes only along the motion: 1.3's
        // cot θ of the whole drag gave the edge's share a face's push). A blade turning in soil at rest: g_t −1, g_a c·cos θ/h.
        static void Section(double chord, double thick, double theta, double ut, double ua, double speed, out double height, out double gt,
            out double ga, out double attack)
        {
            attack = theta - DetMath.Atan2(ua, ut);
            DetMath.SinCos(attack, out double sa, out double ca);
            DetMath.SinCos(theta, out double st, out double ct);
            double edge = thick * DetMath.Abs(ca), side = sa < 0 ? -1 : 1;
            height = chord * DetMath.Abs(sa) + edge;
            gt = -(side * chord * st + edge * (ut / speed)) / height;
            ga = (side * chord * ct - edge * (ua / speed)) / height;
        }

        // An impulse (N·s) along −direction at a point of the body, held to what stops the point's approach speed along it (the
        // point's effective mass along it, 1/m + (r × d)·I⁻¹·(r × d)).
        static double Held(double impulse, double approach, Double3 direction, Double3 point, in FlightState now, in MassProperties body)
        {
            Double3 arm = now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass;
            Double3 lever = Double3.Cross(arm, now.Attitude.InverseRotate(direction));
            double give = body.InverseMass + Double3.Dot(lever, body.InverseInertia * lever);
            return DetMath.Min(impulse, approach / give);
        }

        // The disc's part past the nearest carved face's chord (m; +∞ for none) makes no thrust: its shares go to the rotor for
        // the next step (DamagedRotor.Immersed).
        void Immerse(int c, double chord)
        {
            double thrust = 0, torque = 0;
            if (chord < _radius) _propulsion.Rotor(c).Immersed(chord, out thrust, out torque);
            if (thrust != _propulsion.ImmersedThrust(c) || torque != _propulsion.ImmersedTorque(c)) _propulsion.SetImmersed(c, thrust, torque);
        }

        // A pass's contact time over an arc (rad) at the rotor's speed, held to the step: a slowly turning blade presses the face
        // for the step it is in (the rest of its pass is the next step's), so its push is the face's force over the step at most.
        static double Contact(double arc, double omega) => arc < omega * FlightCore.Dt ? arc / omega : FlightCore.Dt;

        // The entry's half-angle of a jammed blade's remaining tip at a face past it, or the leading edge's for a shape inside.
        static double Entry(double rm, double rtip, double alpha, bool inside) =>
            inside ? alpha : rtip > rm ? DetMath.Acos(rm / rtip) : 0;

        // The rotor's angle held so blade b sits at azimuth psi.
        void Hold(ref FlightState now, int c, int b, double psi) => now.RotorAngle[c] = Wrap(psi - TwoPi * b / _blades);

        // ---- the blade's sections ------------------------------------------------------------------------------------------

        // The station holding x (r/R).
        int Station(double x)
        {
            double k = (x - (_b.Stations[0] - 0.5 * _span)) / _span;
            int i = (int)k;
            return i < 0 ? 0 : i >= _stations ? _stations - 1 : i;
        }

        // The first station inward from rm (m, the meeting radius) whose bending by an edgewise force fe and a flatwise force ff
        // at the arm rc − x·R (rc the struck span's radius) passes its strength (the blade's drawn factor f, the chord share
        // left): design 5's "first passes", searched from the struck span inward, as the load's bending reaches the sections from
        // the contact inward (a flexural wave runs a few centimetres in the contact's 0.1–0.3 ms, so the root does not take the
        // quasi-static moment of a short strike; assumed: quasi-static bending within that reach, which the search's order
        // stands for). −1 for none. moment and ratio: the breaking station's moment and its share of its strength, or the most
        // loaded station's when none breaks (the margin printed).
        int Breaks(BladeState blade, double f, double rm, double rc, double fe, double ff, out double moment, out double ratio)
        {
            moment = ratio = 0;
            for (int k = _stations - 1; k >= 0; k--)
            {
                double x = _b.Stations[k] * _radius;
                // A station at the blade's tip (a break leaves the tip at its station) holds nothing beyond it: no break there.
                if (!(x < rm) || !(_b.Stations[k] < blade.TipRadius)) continue;
                double chi = blade.ChordShare[k], arm = rc - x;
                double me = _edge[k] * f * chi * chi, mf = _flat[k] * f * chi;
                double share = DetMath.Max(me > 0 ? fe * arm / me : 0, mf > 0 ? ff * arm / mf : 0);
                if (share > 1)
                {
                    ratio = share;
                    moment = DetMath.Max(fe, ff) * arm;
                    return k;
                }
                if (!(share > ratio)) continue;
                ratio = share;
                moment = DetMath.Max(fe, ff) * arm;
            }
            return -1;
        }

        // The station whose flatwise moment under a force at rtip is the largest share of its strength; its moment and its
        // proportional limit's moment.
        int Worst(BladeState blade, double f, double rtip, double force, out double moment, out double limit)
        {
            int at = -1;
            double worst = 0;
            moment = limit = 0;
            for (int k = 0; k < _stations; k++)
            {
                double x = _b.Stations[k] * _radius;
                if (!(x < rtip) || !(blade.Inside(_b.Stations[k], _span) > 0)) continue;
                double mf = _flat[k] * f * blade.ChordShare[k], mk = force * (rtip - x);
                if (!(mf > 0) || !(mk / mf > worst)) continue;
                worst = mk / mf;
                at = k;
                moment = mk;
                limit = _b.ProportionalLimit * mf;
            }
            return at;
        }

        // A notch notch deep (m) into the leading edge across w (m) centred at rc (m): each station's chord share there falls by
        // notch over its chord, in proportion to its span inside the width; lo the notch's inner end (r/R). True as a stone's
        // since 1.3 (always, given a width).
        bool Notch(BladeState blade, double rc, double w, double notch, out double lo)
        {
            double R = _radius, hi = (rc + 0.5 * w) / R;
            lo = (rc - 0.5 * w) / R;
            for (int j = 0; j < _stations; j++)
            {
                double a = _b.Stations[j] - 0.5 * _span, z = DetMath.Min(a + _span, DetMath.Min(hi, blade.TipRadius));
                double o = z - DetMath.Max(a, lo);
                if (!(o > 0)) continue;
                double share2 = blade.ChordShare[j] - notch / _b.Chord[j] * (o / _span);
                blade.ChordShare[j] = share2 > 0 ? share2 : 0;
            }
            return true;
        }

        // The blade's volume between r1 and r2 (m), inside its tip, on its chord shares.
        double Volume(BladeState blade, double r1, double r2)
        {
            double lo = r1 / _radius, hi = DetMath.Min(r2 / _radius, blade.TipRadius), sum = 0;
            for (int k = 0; k < _stations; k++)
            {
                double a = _b.Stations[k] - 0.5 * _span, z = a + _span;
                double o = DetMath.Min(z, hi) - DetMath.Max(a, lo);
                if (o > 0) sum += _area[k] * blade.ChordShare[k] * o;
            }
            return sum * _radius;
        }

        // The blade's mass now (its stations inside the tip, on their chord shares), kg.
        double Mass(BladeState blade)
        {
            double sum = 0;
            for (int k = 0; k < _stations; k++) sum += _mass[k] * blade.ChordShare[k] * blade.Inside(_b.Stations[k], _span);
            return sum;
        }

        // Archard's worn volume off the leading edge of the stations from x (r/R) to the tip, in proportion to each one's span in
        // contact: its chord share falls by its volume over the station's whole section. False when nothing was there to wear.
        bool Wear(BladeState blade, double x, double volume)
        {
            double total = 0;
            for (int k = 0; k < _stations; k++) total += Overlap(blade, k, x);
            if (!(total > 0)) return false;
            for (int k = 0; k < _stations; k++)
            {
                double o = Overlap(blade, k, x);
                if (!(o > 0)) continue;
                double left = blade.ChordShare[k] - volume * o / total / (_mass[k] / _b.Density);
                blade.ChordShare[k] = left > 0 ? left : 0;
            }
            return true;
        }

        double Overlap(BladeState blade, int k, double x)
        {
            double a = _b.Stations[k] - 0.5 * _span, z = DetMath.Min(a + _span, blade.TipRadius);
            double o = z - DetMath.Max(a, x);
            return o > 0 ? o : 0;
        }

        // ---- the world, the frames, the log ------------------------------------------------------------------------------------

        // Disc i's pair k (obj, shape): the world's geometry, a terrain's ground the plane its contact touched (w3-farmland-ground
        // 2.2: ContactModel.GroundPlane), a wire its chain's window at the disc.
        bool Geometry(int i, int k, int obj, int shape, byte surface, Double3 hub, in ContactShape d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)
        {
            chain = default;
            if (obj == WorldIds.Ground && _contacts.Terrain)
            {
                _contacts.DiscPairPlane(i, k, out Double3 point, out Double3 normal);
                g = ContactModel.GroundPlane(point, normal, surface);
                return true;
            }
            if (!_world.Geometry(obj, shape, out g)) return false;
            if (g.Kind != ShapeKind.Wire) return true;
            chain = _world.WirePoints(obj, shape);
            BladeCut.WireWindow(chain, g.Radius, hub, d.Radius + d.HalfThickness, out int lo, out int hi);
            if (hi < lo) return false;
            chain = chain.Slice(lo, hi - lo + 2);
            return true;
        }

        // A world direction in the disc's plane as the rotor's azimuth: blade 0 at ψ lies along body (cos ψ, 0, s·sin ψ).
        double Azimuth(in Quat attitude, Double3 dir, int c)
        {
            Double3 v = attitude.InverseRotate(dir);
            return Wrap(DetMath.Atan2(_spin[c] * v.Z, v.X));
        }

        static double Wrap(double a)
        {
            while (a >= TwoPi) a -= TwoPi;
            while (a < 0) a += TwoPi;
            return a;
        }

        static Double3 PointVelocity(in FlightState s, Double3 point) =>
            s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, s.Attitude.InverseRotate(point - s.Position)));

        static void Slot(ref FlightState now, int c, int b, ushort material, double energy, double impulse, double radius)
        {
            int slot = BladeStrikes.Slot(c, b);
            if (slot >= BladeStrikes.Slots) return;
            BladeStrike e = now.BladeStrikes[slot];
            e.Add(StrikeKind.Shape, material, energy, impulse, 0, 0, radius);
            now.BladeStrikes[slot] = e;
        }

        void Strike(int i, in ContactShape d, int b, int obj, int shape, ushort material, byte surface, in PassRecord rec, StepEvents events)
        {
            FlightEvent e = Of(EventKind.BladeStrike, d, obj, shape, material, surface, rec.Point);
            e.Index = (byte)(b | ((int)rec.Mode << 4));
            e.A = rec.Used;
            e.B = rec.Radius;
            e.C = rec.SpeedIn;
            events.Add(e);
        }

        void Damaged(int i, in ContactShape d, int b, StrikeMode mode, int obj, int shape, ushort material, byte surface, Double3 point, double tip,
            double lost, double from, StepEvents events)
        {
            FlightEvent e = Of(EventKind.BladeDamaged, d, obj, shape, material, surface, point);
            e.Index = (byte)(b | ((int)mode << 4));
            e.A = tip;
            e.B = lost;
            e.C = from;
            events.Add(e);
            // A break that leaves the blade at its root's station (p7f-damage 1.6, design 11): the blade is lost, once.
            int slot = d.Corner * _blades + b;
            if (mode != StrikeMode.Break || !(tip <= _b.Stations[0]) || _bladeLost[slot]) return;
            _bladeLost[slot] = true;
            FlightEvent l = Of(EventKind.BladeLost, d, obj, shape, material, surface, point);
            l.Index = (byte)b;
            l.A = tip;
            l.B = lost;
            l.C = from;
            events.Add(l);
        }

        static FlightEvent Of(EventKind kind, in ContactShape d, int obj, int shape, ushort material, byte surface, Double3 point)
        {
            FlightEvent e = FlightEvent.Of(kind);
            e.Part = (ushort)d.Part;
            e.Object = obj;
            e.Shape = shape;
            e.Material = material;
            e.Surface = surface;
            e.Point = point;
            return e;
        }
    }
}
