using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tether
{
    /// <summary>The line's summary each step, for the log (p7d-tether 1.6; LogFormat's tether block).</summary>
    public struct TetherTrace
    {
        /// <summary>The line paid out this flight, m, and the balloon's speed ṡ, m/s.</summary>
        public double PaidOut, PayoutSpeed;
        /// <summary>The pack's resistance T_r (payout_tension_n), N, and the exit segment's tension at the step's end, N.</summary>
        public double PayoutTension, ExitTension;
        /// <summary>The exit lip's wrap θ_e, rad, and the pack's irregularity at the section at the peel.</summary>
        public double LipAngle, Irregularity;
        /// <summary>The line's force on the drone this step, world N, and its moment about the CoM, body N·m.</summary>
        public Double3 Force, Moment;
        /// <summary>The chain's nodes and the laid trail's points after the step.</summary>
        public int Nodes, Trail;
        /// <summary>TetherFlags, and the extra solves the chain took this step.</summary>
        public byte Flags, Corrections;
    }

    /// <summary>
    /// The air's drag on a length of line (p7d-tether design 1, 3, 5): across it White's fit for a circular cylinder,
    /// C_d = base + coefficient·Re^exponent (1 + 10·Re^(−2/3), published for 1 &lt; Re &lt; 2·10⁵, held at the band's ends), on
    /// ½ρ·u⊥²·D per metre; along it skin friction on its surface, ½ρ·u∥²·C_f·πD. Taken as a linear drag about the node's
    /// velocity whose coefficients are the law's secant at the step's start (exact there, implicit in the step).
    /// </summary>
    public readonly struct FiberDrag
    {
        public readonly double Density, Diameter, Viscosity, Base, Coefficient, ReynoldsLo, ReynoldsHi, Axial;

        /// <summary>The table's law for a line of this diameter (the coating's, or wet in rain).</summary>
        public FiberDrag(TetherTable table, double diameter)
        {
            Density = table.Number("fiber.air_density_kg_m3");
            Diameter = diameter;
            Viscosity = table.Number("fiber.air_kinematic_viscosity_m2_s");
            Base = table.Number("fiber.drag_cross_base");
            Coefficient = table.Number("fiber.drag_cross_coefficient");
            double[] band = table.Row("fiber.drag_cross_reynolds").Values;
            ReynoldsLo = band[0];
            ReynoldsHi = band[1];
            Axial = table.Number("fiber.drag_axial");
            double exponent = table.Number("fiber.drag_cross_exponent");
            // The fit's exponent is −2/3, taken through the cube root (the law's form is fixed; the table pins its value).
            if (!(DetMath.Abs(exponent + 2.0 / 3.0) < 1e-12)) throw new ArgumentException("the drag fit's exponent must be −2/3, Re^(−2/3) = 1/∛(Re²)");
        }

        /// <summary>C_d across at the cross-flow speed u, m/s.</summary>
        public double Cross(double u)
        {
            double re = u * Diameter / Viscosity;
            if (re < ReynoldsLo) re = ReynoldsLo;
            else if (re > ReynoldsHi) re = ReynoldsHi;
            return Base + Coefficient / DetMath.Cbrt(re * re);
        }

        /// <summary>The drag per metre on the line at rest in air moving at u across it, N/m.</summary>
        public double PerMetre(double u) => 0.5 * Density * Diameter * Cross(u) * u * u;

        /// <summary>A node's drag for length metres of line along the unit tangent t, at its velocity v in air moving at air:
        /// node.Drag and node.Air so that −Drag·(v − Air) is the law's force now; returns the cross-flow speed.</summary>
        public double Apply(ref CableNode node, Double3 air, Double3 t, double length)
        {
            Double3 u = air - node.Velocity;
            double along = Double3.Dot(u, t);
            Double3 cross = u - t * along;
            double uc = cross.Length(), ua = DetMath.Abs(along);
            double across = 0.5 * Density * Diameter * Cross(uc) * uc * length;
            double axial = 0.5 * Density * 3.141592653589793 * Diameter * Axial * ua * length;
            node.Drag = new Sym3(across, across, across, 0, 0, 0)
                + new Sym3((axial - across) * t.X * t.X, (axial - across) * t.Y * t.Y, (axial - across) * t.Z * t.Z,
                    (axial - across) * t.X * t.Y, (axial - across) * t.X * t.Z, (axial - across) * t.Y * t.Z);
            node.Air = air;
            return uc;
        }
    }

    /// <summary>TetherTrace.Flags.</summary>
    [Flags]
    public enum TetherFlags : byte
    {
        None = 0,
        /// <summary>Line flowed out this step.</summary>
        Paying = 1,
        /// <summary>The coil is empty: its end is held at the core.</summary>
        CoilEmpty = 2,
        /// <summary>The energy check clipped the step (labelled numerical damping).</summary>
        Clipped = 4,
        /// <summary>The step was solved again on majorizing models.</summary>
        SafeStep = 8,
        /// <summary>A TetherWedge: the step undone, the line stopped.</summary>
        Wedge = 16,
        /// <summary>The line's spool is the known-wrong two-way one (design 16).</summary>
        TwoWay = 32,
        /// <summary>The line is broken (1.8): this step or before.</summary>
        Broken = 64,
        /// <summary>The link is in a dip after the step (1.8).</summary>
        Dip = 128,
    }

    /// <summary>
    /// The fiber-optic line of one flight (p7d-tether design 3, 4, 5; PHY-18, DRN-05): the coil beneath paying out one way
    /// through its exit, the span (a CableChain of at most span.max_nodes nodes from the touchdown to the exit) and the laid
    /// trail behind it. FlightCore steps it inside its 1 ms step, after the body moves:
    /// - The exit is the chain's last node, anchored to the canister's nozzle tip (spool.exit_position_m) and following it;
    ///   line flows out through it by CableSpool's law (the peel's rate law at the section's irregularity, the momentum flux,
    ///   the balloon, the nozzle's factor over the wrap θ between the nozzle's axis, spool.exit_direction, and the first
    ///   segment: design 18's point contacts at the bore's entry and the hole's edge, FiberCuts.LipFactor), and never back. The coil's mass falls by the line paid out and the mass model is rebuilt each span.coil_rebuild_m.
    /// - The line pulls the drone at the exit: the exit segment's tension along it less the momentum flux μ·ṡ² (the leaving
    ///   line carries it away; derived from the variable-mass momentum balance), applied in the next step (the one-step lag
    ///   of design 2).
    /// - Each node carries gravity, the drag of its relative air by White's fit across it and skin friction along it, the
    ///   wind field at it, the rotors' air (RotorAir, with its fountain and ground vortex unless the setup flies design 4's
    ///   field, TetherDesign4Air) and in rain its wet film (heavier and thicker); a node lying in a wall jet is lifted by the
    ///   wall's lift on a cylinder.
    /// - A node near the ground rests on its plane (SampleGround, one batch a step; over a hole the soil a ray finds) with the
    ///   coating's friction; with 1.7's contacts (FiberContacts, unless FlightSetup.TetherWithoutContacts) it meets the plane
    ///   with its surface's own friction, over a hole the shapes there, and every shape, element, snag record, conductor and
    ///   part of the drone near it; at rest on any of them trail.freeze_seconds (its holds stuck there, or slower than
    ///   trail.freeze_speed_m_s), or held still that long in a span between such nodes, more than near_length_m of line from the
    ///   exit, it freezes into the trail with the tension and the air it bore; the trail's last point lifts back into the chain
    ///   when the chain's pull would move it (across or off its laid run past the friction of the line it lumps, or along it
    ///   past what the run and its corners hold: w3-farmland-ground 2.4), or the air at it passes what it bore by the ground's
    ///   friction. A laid node the ground holds across the chord between its neighbours (a turn) is held as a crest is.
    /// - On a terrain the line lies over the ground (w3-farmland-ground 2.5, Crests): a segment whose chord passes more than
    ///   KeepGap under the ground takes a node there, on the ground at its deepest point (a crest, a step's lip), and a node the
    ///   ground holds over the chord between its neighbours is never merged away; CrestNodes such nodes besides span.max_nodes.
    /// - The launch: the line laid on the ground from its anchor (launch.anchor_behind_start_m behind the start, or a given
    ///   layout) to where the nozzle's lead (spool.exit_direction) meets the ground, then up along it to the exit; beneath
    ///   the exit for a lead level or rising.
    /// - Cut risk and the link (1.8, unless FlightSetup.TetherWithoutCuts): after the chain's step a segment crossing a turning
    ///   disc is cut at the next blade pass (the prop), then FiberCuts' tension, bend, abrasion and peel checks; the first that
    ///   holds breaks the line (TetherBreak, its strength as FiberSection): the far piece is left in the trail where it lies, the
    ///   near piece hangs from the exit with a free end (no trail from then), or nothing hangs after a break at the drone (the
    ///   lip, the peel, the exit's segment). FiberLink's margin and dips each step (LinkDip); Broken and −∞ from the break.
    /// The randomness: spool.peel (G₀, one draw a flight) and spool.irregularity (per section_length_m of line, counter-based
    /// on the section's index, logged in the step record while at the peel); 1.8's fiber.span_strength and
    /// fiber.section_strength (FiberCuts). Nothing about where the line lies is drawn.
    /// Engine-free, + − × ÷ √ and DetMath, preallocated: 0 B a step.
    /// </summary>
    public sealed class FiberLine
    {
        // Numerical values (labelled): the exit's and the trail end's anchor springs, stiff against the line (0.2 N moves the
        // exit node 20 µm) and critically damped on the node's mass; the ground's penalty spring per metre of line, so a
        // resting line sinks 7 µm, critically damped; and the stick spring a node on the ground is held by, 1 N/m a node: under
        // its friction μs·N it gives μs·w·L/k (3.4 mm for 10 m of line, 34 µm for 0.1 m), and a node sliding under it is held
        // back by no more than its pull over k·dt (50 m/s at 0.05 N), so a dragged line slides at its own speed (the stick
        // spring is implicit in the step and slips only after it; a stiff one would drag the line like a damper of k·dt).
        const double ExitStiffness = 1e4, TrailStiffness = 1e4;
        const double GroundStiffnessPerMetre = 100, GroundHold = 1;

        // The drag fit's Reynolds band ends (the table's drag_cross_reynolds) bound the fit; outside, its end value.
        readonly TetherTable _table;
        readonly CableChain _chain;
        readonly RotorAir _rotors;
        readonly double _dt;

        // The line.
        readonly double _dryMass, _diameter, _mass, _weight, _radius;
        readonly FiberDrag _drag;
        readonly double _liftCoefficient, _liftGap;

        // The spool.
        readonly Double3 _exitBody, _exitLead;

        // Two body axes square to the nozzle's (the lead's azimuth about it, the hole's edge's wear) and the azimuth after the last
        // step (NaN before the first).
        readonly Double3 _azimuthX, _azimuthY;
        double _lipAzimuth = double.NaN;
        // The lip's law (design 18): the bore's free angle, the edge's radius plus the coating, the glass's EI; the factor this step.
        readonly double _freeAngle, _lipCap, _bending;
        double _lipFactor = 1;
        readonly double _lipFriction, _peelEnergy, _rateExponent, _referenceRate, _balloonLength, _balloonMass, _sectionLength;
        readonly double _irregularityLo, _irregularityHi;
        readonly SeedStream _irregularity;

        // The span.
        readonly double _spacing, _nearLength, _growth, _reach, _groundStatic, _groundKinetic, _rebuildStep;

        // The trail.
        readonly Double3[] _trail;
        readonly double[] _trailMaterial, _trailHeld, _trailAir;
        readonly double _freezeTime, _freezeSpeed, _hold;
        readonly bool _slidingTrail; // FlightSetup.TetherSlidingTrail: the trail before w3-farmland-ground 2.4
        readonly bool _capstanLip;   // FlightSetup.TetherBeforeDesign18: the line before design 18
        int _trailCount;

        // The coil.
        readonly int _coilPart;
        readonly double _coilMass0, _coilLine;
        readonly double _exitMaterial0;
        int _rebuilds;

        // Per-step buffers: the ground under each node and each hub (one batch), the rotors' state.
        readonly XZ[] _at;
        readonly GroundSample[] _ground;
        readonly XZ[] _windAt;               // AddWind's: the ground under each node posted to the wind's batch
        readonly GroundSample[] _windGround;
        readonly Double3[] _soilAt;          // AddWind's: each node over a hole and the soil it cast there, for Ground (one cast a step)
        readonly double[] _soilHeight;
        int _soilCount, _soilCursor;
        readonly double[] _groundHeight;
        readonly Double3[] _groundNormal;
        readonly Double3[] _groundAt; // a point of each node's ground plane: under it, or on a step's face it lies behind
        readonly bool _terrain;       // the ground is a terrain, whose steps SampleGround reports by their tops
        readonly StaticContact[] _face = new StaticContact[8];
        readonly Ray[] _soilRay = new Ray[1];
        readonly RayHit[] _soilHit = new RayHit[1];
        readonly Double3[] _hubs = new Double3[4], _hubAir = new Double3[4];
        readonly double[] _thrust = new double[4], _hubGround = new double[4];
        readonly double[] _airPerMetre; // the air's force per metre on each node at rest against what holds it, the freeze's and lift's test
        // The step's loads as each node took them, in the order the nodes had then (forensics: the field's law at each node).
        readonly Double3[] _loadPoint, _loadVelocity, _loadAir, _loadTangent, _loadForce, _loadLift;
        readonly double[] _loadLength, _loadMaterial;
        int _loadCount;

        Double3 _force; // the line's force on the drone at the last step's end, world N
        double _lipAngle, _section;

        // The line over the ground's crests (w3-farmland-ground 2.5; Crests): the clearance certificate's margin (the
        // table's), and the tests' buffers.
        readonly double _crestMargin;
        readonly Ray[] _crestRay = new Ray[1];
        readonly RayHit[] _crestHit = new RayHit[1];
        readonly XZ[] _crestAt = new XZ[2];
        readonly GroundSample[] _crestGround = new GroundSample[2];
        readonly StaticContact[] _crestContact = new StaticContact[2];

        // The contacts of 1.7 (null: the line meets the plane alone, as model 50's, FlightSetup.TetherWithoutContacts).
        readonly FiberContacts _contacts;

        // The cuts and the link of 1.8 (null: the line never breaks and carries no link, as model 55's,
        // FlightSetup.TetherWithoutCuts), and the rotors' discs the prop check reads; the drone's contact model, whose resting
        // rotors (p7f-damage 1.3b) turn no disc.
        readonly FiberCuts _cuts;
        readonly FiberLink _link;
        readonly Propulsion _propulsion;
        readonly ContactModel _parts;
        readonly int _blades;
        readonly double _rotorRadius, _hubRadius;
        readonly double[] _spin = new double[4], _discHalf = new double[4];
        readonly int[] _discShape = { -1, -1, -1, -1 }, _discPart = { -1, -1, -1, -1 };
        bool _attached = true;

        public FiberLine(TetherTable table, FlightSetup setup, MassModel mass, FlightRandom random, in FlightState start, double dt,
            ContactModel parts = null, Propulsion propulsion = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            _table = table;
            _dt = dt;
            World = setup.World;
            _terrain = !World.Geometry(WorldIds.Ground, 0, out _);
            TwoWay = setup.TetherTwoWay;
            _slidingTrail = setup.TetherSlidingTrail;
            _capstanLip = setup.TetherBeforeDesign18;

            double film = setup.Rain == RainLevel.Rain ? table.Number("fiber.wet_film_rain_m")
                : setup.Rain == RainLevel.SevereRain ? table.Number("fiber.wet_film_severe_m") : 0;
            _dryMass = table.Number("fiber.mass_per_m_kg");
            double dry = table.Number("fiber.coating_diameter_m");
            _diameter = dry + 2 * film;
            const double Pi = 3.141592653589793;
            _mass = _dryMass + table.Number("fiber.water_density_kg_m3") * Pi * (_diameter * _diameter - dry * dry) / 4;
            _weight = -_mass * FlightCore.Gravity.Y;
            _radius = _diameter / 2;
            _drag = new FiberDrag(table, _diameter);
            _liftCoefficient = table.Number("rotor_air.wall_lift_coefficient");
            _liftGap = table.Number("rotor_air.wall_lift_gap_over_diameter");

            double[] exit = table.Row("spool.exit_position_m").Values;
            _exitBody = new Double3(exit[0], exit[1], exit[2]);
            double[] lead = table.Row("spool.exit_direction").Values;
            double leadLength = DetMath.Sqrt(lead[0] * lead[0] + lead[1] * lead[1] + lead[2] * lead[2]);
            _exitLead = new Double3(lead[0] / leadLength, lead[1] / leadLength, lead[2] / leadLength);
            Double3 across = new Double3(1, 0, 0) - _exitLead * _exitLead.X;
            _azimuthX = across * (1 / across.Length());
            _azimuthY = Double3.Cross(_exitLead, _azimuthX);
            _lipFriction = table.Number("spool.lip_friction");
            _freeAngle = DetMath.Atan2(2 * table.Number("spool.mouth_radius_m"), table.Number("spool.nozzle_length_m"));
            _lipCap = table.Number("spool.lip_radius_m") + table.Number("fiber.coating_thickness_m");
            _bending = table.Number("fiber.bending_stiffness_n_m2");
            _rateExponent = table.Number("spool.peel_rate_exponent");
            _referenceRate = table.Number("spool.peel_reference_rate_m_s");
            _balloonLength = table.Number("spool.balloon_length_m");
            _balloonMass = _dryMass * _balloonLength;
            _sectionLength = table.Number("fiber.section_length_m");
            double[] peel = table.Row("spool.peel_energy_j_m").Values;
            SeedStream peelStream = random.Open("spool.peel");
            _peelEnergy = random.DrawQuantile(ref peelStream, "peel_energy_j_m", peel);
            double[] irregularity = table.Row("spool.irregularity").Values;
            _irregularityLo = irregularity[0];
            _irregularityHi = irregularity[1];
            _irregularity = random.Open("spool.irregularity");

            _spacing = table.Number("span.near_spacing_m");
            _nearLength = table.Number("span.near_length_m");
            _growth = table.Number("span.spacing_growth");
            _reach = table.Number("span.ground_reach_m");
            _groundStatic = table.Number("span.ground_static_friction");
            _groundKinetic = table.Number("span.ground_kinetic_friction");
            _rebuildStep = table.Number("span.coil_rebuild_m");
            int nodes = (int)table.Number("span.max_nodes");
            _crestMargin = table.Number("contact.clearance_margin_m");

            _freezeTime = table.Number("trail.freeze_seconds");
            _freezeSpeed = table.Number("trail.freeze_speed_m_s");
            int capacity = (int)table.Number("trail.capacity");
            _hold = _groundStatic * _weight * table.Number("trail.hold_length_m");
            _trail = new Double3[capacity];
            _trailMaterial = new double[capacity];
            _trailHeld = new double[capacity];
            _trailAir = new double[capacity];

            // On a terrain the chain has room for the crests' nodes besides the spacing law's (w3-farmland-ground 2.5).
            _chain = new CableChain(nodes, _mass, table.Number("fiber.axial_stiffness_n"), _terrain ? CrestNodes : 0)
            {
                Gravity = FlightCore.Gravity, ZeroForceHold = setup.TetherZeroForceHold, LinearStep = setup.TetherLinearStep,
                KeepSlideStretch = setup.TetherBeforeDesign18,
            };
            nodes = _chain.Capacity;
            _at = new XZ[nodes + 4];
            _ground = new GroundSample[nodes + 4];
            _windAt = new XZ[nodes];
            _windGround = new GroundSample[nodes];
            _soilAt = new Double3[nodes];
            _soilHeight = new double[nodes];
            _groundHeight = new double[nodes];
            _groundNormal = new Double3[nodes];
            _groundAt = new Double3[nodes];
            _airPerMetre = new double[nodes];
            _loadPoint = new Double3[nodes];
            _loadVelocity = new Double3[nodes];
            _loadAir = new Double3[nodes];
            _loadTangent = new Double3[nodes];
            _loadForce = new Double3[nodes];
            _loadLift = new Double3[nodes];
            _loadMaterial = new double[nodes];
            _loadLength = new double[nodes];
            if (setup.Propulsion != null)
                _rotors = new RotorAir(table, setup.Propulsion.Props.Diameter / 2, !setup.TetherDesign4Air,
                    setup.TetherFountainThroughBody ? null : parts);

            // The coil: the line left on it, from its part's mass over the empty spool's.
            _coilPart = setup.TetherCoilPart;
            if (_coilPart >= 0)
            {
                _coilMass0 = mass.Part(_coilPart).Mass;
                CoilMass = _coilMass0;
                double line = (_coilMass0 - setup.TetherSpoolMass) / _dryMass;
                _coilLine = line > 0 ? line : 0;
            }
            else _coilLine = double.PositiveInfinity;

            Launch(setup, start);
            _exitMaterial0 = _chain[_chain.Count - 1].Material;
            if (!setup.TetherWithoutContacts)
                _contacts = new FiberContacts(table, setup, World, parts, _radius, _mass, GroundStiffnessPerMetre, GroundHold, dt);

            // The cuts (1.8): the strengths' draws after the spool's; the rotors' discs for the prop check.
            if (!setup.TetherWithoutCuts)
            {
                _cuts = new FiberCuts(table, random, setup.TetherOneStrength, setup.TetherBeforeDesign18);
                _link = new FiberLink(table);
            }
            _propulsion = propulsion;
            _parts = parts;
            if (setup.Propulsion != null)
            {
                _blades = setup.Propulsion.Props.Blades;
                _rotorRadius = setup.Propulsion.Props.Diameter / 2;
                PropBlade blade = setup.Propulsion.Rotor?.Blade;
                _hubRadius = blade != null ? blade.HubDiameter / 2 : 0; // the blade's root: none without props.blade (assumed)
                for (int i = 0; i < 4; i++) _spin[i] = setup.Propulsion.Motors[i].Clockwise ? -1 : 1;
            }
            if (setup.Contacts != null)
                for (int i = 0; i < setup.Contacts.Shapes.Length; i++)
                {
                    ContactShape d = setup.Contacts.Shapes[i];
                    if (d.Role != ContactRole.Disc || d.Corner < 0 || d.Corner > 3) continue;
                    _discHalf[d.Corner] = d.HalfThickness;
                    _discShape[d.Corner] = i;
                    _discPart[d.Corner] = d.Part;
                }
            // The laid line over its crests before the first step (2.5): the pass again while it puts a node in.
            for (long put = -1; _terrain && put != CrestsInserted && _chain.Crests < _chain.CrestCapacity;)
            {
                put = CrestsInserted;
                Crests(false);
            }
        }

        /// <summary>The line's cut risk (1.8), or null without it (FlightSetup.TetherWithoutCuts).</summary>
        public FiberCuts Cuts => _cuts;

        /// <summary>The optical link (1.8), or null without the cuts.</summary>
        public FiberLink Link => _link;

        /// <summary>The line broke this flight; this step; how (valid once Broken); the step count at the break.</summary>
        public bool Broken { get; private set; }
        public bool BrokeThisStep { get; private set; }
        public FiberBreak LastBreak { get; private set; }
        public long BrokenAt { get; private set; } = -1;

        /// <summary>A piece of line still hangs from the exit (false after a break at the drone: the lip, the peel, the exit's
        /// segment). The far piece is the trail's from the break on.</summary>
        public bool Attached => _attached;

        /// <summary>The trail's points before the far piece of a break (the renderer's: the laid trail ends there and the far
        /// piece follows to TrailCount, the chain, the near piece, not joined to it); 0 while whole (the trail always holds
        /// its anchor, so a break's count is at least 1).</summary>
        public int TrailBeforeBreak { get; private set; }

        /// <summary>The exit's tension after the last step, N (0 once nothing hangs from it).</summary>
        public double ExitTension => _attached && _chain.Count >= 2 ? _chain.Tension(_chain.Count - 2) : 0;

        /// <summary>The line's least bend radius after the last step, m: the cuts' (every bend and the lip), else the contacts'.</summary>
        public double MinBendRadius => _cuts != null ? _cuts.MinBendRadius : _contacts != null ? _contacts.MinBendRadius : double.PositiveInfinity;

        /// <summary>The line's contacts (p7d-tether 1.7), or null without them (FlightSetup.TetherWithoutContacts).</summary>
        public FiberContacts Contacts => _contacts;

        public FlightWorld World { get; }

        /// <summary>The chain from the touchdown (node 0) to the exit (the last node).</summary>
        public CableChain Chain => _chain;

        /// <summary>The laid trail's points, world m, from the anchor; the first TrailCount hold, the last of them the chain's
        /// node 0. Append-only between lifts (the renderer reads up to the published count).</summary>
        public Double3[] Trail => _trail;
        public int TrailCount => _trailCount;
        public double TrailMaterial(int i) => _trailMaterial[i];

        /// <summary>The tension trail point i held when it froze, N (it lifts when the pull at it passes this and what
        /// trail.hold_length_m of line holds).</summary>
        public double TrailHeld(int i) => _trailHeld[i];

        /// <summary>The known-wrong two-way spool (design 16): it takes line back at the static hold.</summary>
        public bool TwoWay { get; }

        /// <summary>The exit, body m.</summary>
        public Double3 ExitBody => _exitBody;

        /// <summary>The hole's edge's wear on the section at the lip after the last step, as a share of the coating (FiberCuts.WearLip):
        /// one pass's while line pays out, the section's sum while it is held.</summary>
        public double LipWear { get; private set; }

        /// <summary>The nozzle's axis out of the exit, body frame, a unit vector (spool.exit_direction).</summary>
        public Double3 ExitLead => _exitLead;

        /// <summary>The line's force on the drone at the last step's end, world N, applied at the exit in the next step.</summary>
        public Double3 Force => _force;

        /// <summary>The flight's G₀, J/m (spool.peel).</summary>
        public double PeelEnergy => _peelEnergy;

        /// <summary>The line paid out this flight, m, and from the coil in all (this flight's and before it), m.</summary>
        public double PaidOut => _chain[_chain.Count - 1].Material - _exitMaterial0;

        /// <summary>The line on the coil at the start, m (∞ without a coil part).</summary>
        public double CoilLineAtStart => _coilLine;

        /// <summary>The coil's part and its mass at the start and now, kg; MassChanged when the last step crossed a rebuild mark.</summary>
        public int CoilPart => _coilPart;
        public double CoilMassAtStart => _coilMass0;
        public double CoilMass { get; private set; }
        public bool MassChanged { get; private set; }

        /// <summary>The line's mass and diameter per metre as it flies (its wet film in rain), kg/m and m.</summary>
        public double MassPerMetre => _mass;
        public double Diameter => _diameter;

        /// <summary>The last step's loads as each node took them at the step's start (in the node order of then, before the
        /// chain's remesh and the trail): its point, its velocity, the air there (the wind and the rotors'), its tangent and its
        /// line's length, for forensics and the scenarios (the field's law at each node). The exit takes none.</summary>
        public int LoadCount => _loadCount;
        public Double3 LoadPoint(int i) => _loadPoint[i];
        public Double3 LoadVelocity(int i) => _loadVelocity[i];
        public Double3 LoadAir(int i) => _loadAir[i];
        public Double3 LoadTangent(int i) => _loadTangent[i];
        public double LoadLength(int i) => _loadLength[i];
        /// <summary>The drag the node's linear drag gave at the step's start, −Drag·(v − Air), world N; the wall's lift on it, N;
        /// its material coordinate then, m.</summary>
        public Double3 LoadForce(int i) => _loadForce[i];
        public Double3 LoadLift(int i) => _loadLift[i];
        public double LoadMaterial(int i) => _loadMaterial[i];

        /// <summary>The line's drag law (FiberDrag, at its diameter) and the rotors' air (null without propulsion).</summary>
        public FiberDrag Drag => _drag;
        public RotorAir Rotors => _rotors;

        /// <summary>The irregularity of the section at s, the material coordinate (spool.irregularity, counter-based).</summary>
        public double Irregularity(double s)
        {
            ulong k = s > 0 ? (ulong)(s / _sectionLength) : 0;
            return _irregularityLo + (_irregularityHi - _irregularityLo) * ((_irregularity.BitsAt(k) >> 11) * (1.0 / 9007199254740992.0));
        }

        /// <summary>The pack's resistance law at ṡ for the section at the peel now, the balloon steady: T_r, N.</summary>
        public double PackLaw(double sdot) =>
            _dryMass * sdot * sdot + _peelEnergy * Irregularity(_section) * (sdot > _referenceRate ? DetMath.Exp(_rateExponent * DetMath.Log(sdot / _referenceRate)) : 1);

        /// <summary>The spool's factor over the nozzle at the last step's wrap (FiberCuts.LipFactor, design 18).</summary>
        public double Lip => _lipFactor;

        // ---- the launch ----

        // The line laid from the anchor to where the nozzle's lead meets the ground (or beneath the exit), then up to the exit,
        // its material its length; nodes by the spacing law from the exit; node 0 the anchor (the trail's first point), the
        // exit anchored to the drone.
        void Launch(FlightSetup setup, in FlightState start)
        {
            Double3 exit = start.Position + start.Attitude.Rotate(_exitBody);
            Double3[] layout = setup.TetherLayout;
            int points;
            Double3[] path;
            double foot = GroundAt(exit), behind = _table.Number("launch.anchor_behind_start_m");
            if (layout != null && layout.Length > 0)
            {
                path = new Double3[layout.Length + 2];
                for (int i = 0; i < layout.Length; i++) path[i] = OnGround(layout[i]);
                points = layout.Length;
            }
            else
            {
                Double3 nose = start.Attitude.Rotate(new Double3(0, 0, 1));
                double h = DetMath.Sqrt(nose.X * nose.X + nose.Z * nose.Z);
                Double3 back = h > 0 ? new Double3(-nose.X / h, 0, -nose.Z / h) : new Double3(0, 0, -1);
                path = new Double3[3];
                path[0] = OnGround(new Double3(exit.X + back.X * behind, 0, exit.Z + back.Z * behind));
                points = 1;
            }
            // The line leaves the nozzle along its axis (spool.exit_direction) and lies on the ground where that lead meets it:
            // beneath the exit for a nozzle level or rising, one whose lead would reach the ground past the anchor, or a laid
            // line that ends short of where the lead meets the ground (its last point nearer the exit along the lead).
            Double3 lead = start.Attitude.Rotate(_exitLead);
            double drop = exit.Y - foot - _radius, reach = -lead.Y > 0 ? drop / -lead.Y : double.PositiveInfinity;
            double across = DetMath.Sqrt(lead.X * lead.X + lead.Z * lead.Z), laid = double.PositiveInfinity;
            if (points > 0 && across > 0)
                laid = ((path[points - 1].X - exit.X) * lead.X + (path[points - 1].Z - exit.Z) * lead.Z) / across;
            if (drop > 0 && reach * across < behind && laid >= reach * across)
                path[points++] = OnGround(new Double3(exit.X + lead.X * reach, 0, exit.Z + lead.Z * reach));
            else
                path[points++] = new Double3(exit.X, foot + _radius, exit.Z);
            path[points++] = exit;
            var along = new double[points];
            for (int i = 1; i < points; i++) along[i] = along[i - 1] + (path[i] - path[i - 1]).Length();
            double total = along[points - 1];

            // The material coordinates from the exit back: near_spacing_m within near_length_m, then growing.
            var s = new double[_chain.SpacingCapacity];
            int n = 0;
            double at = total;
            s[n++] = total;
            while (at > 0)
            {
                double away = total - at, h = away <= _nearLength ? _spacing : _spacing + (_growth - 1) * (away - _nearLength);
                at -= h;
                if (at <= 1e-9 || n == _chain.SpacingCapacity - 1) at = 0;
                s[n++] = at;
            }
            var material = new double[n];
            var x = new Double3[n];
            for (int i = 0; i < n; i++)
            {
                material[i] = s[n - 1 - i];
                x[i] = Along(path, along, points, material[i]);
            }
            _chain.Reset(x, material, n);
            _trail[0] = x[0];
            _trailMaterial[0] = 0;
            _trailCount = 1;
            AnchorTrailEnd();
            ref CableNode e = ref _chain[n - 1];
            e.AnchorStiffness = ExitStiffness;
            e.AnchorFollows = true;
            e.AnchorTarget = exit;
            e.AnchorVelocity = start.Velocity + start.Attitude.Rotate(Double3.Cross(start.BodyRate, _exitBody));
            e.Velocity = e.AnchorVelocity;
            _chain.SetSpacing(_spacing, _nearLength, _growth, total);
        }

        static Double3 Along(Double3[] path, double[] along, int points, double s)
        {
            for (int i = 1; i < points; i++)
            {
                if (s > along[i] && i < points - 1) continue;
                double span = along[i] - along[i - 1], f = span > 0 ? (s - along[i - 1]) / span : 0;
                return path[i - 1] + (path[i] - path[i - 1]) * f;
            }
            return path[points - 1];
        }

        Double3 OnGround(Double3 p) => new Double3(p.X, GroundAt(p) + _radius, p.Z);

        double GroundAt(Double3 p)
        {
            _at[0] = new XZ(p.X, p.Z);
            World.SampleGround(new ReadOnlySpan<XZ>(_at, 0, 1), new Span<GroundSample>(_ground, 0, 1));
            if ((_ground[0].Flags & GroundFlags.Hole) == 0) return _ground[0].Height;
            return World.SoilUnder(new Double3(p.X, p.Y + 1000, p.Z), _soilRay, _soilHit, out _);
        }

        void AnchorTrailEnd()
        {
            ref CableNode a = ref _chain[0];
            a.AnchorStiffness = TrailStiffness;
            a.AnchorDamping = 2 * DetMath.Sqrt(TrailStiffness * (a.Mass > 0 ? a.Mass : _mass * _spacing));
            a.AnchorTarget = _trail[_trailCount - 1];
            a.AnchorVelocity = default;
            a.AnchorFollows = false;
            a.Velocity = default;
            a.Contact0 = default;
            a.Contact1 = default;
            a.RestTime = 0;
        }

        // ---- the step ----

        /// <summary>
        /// One step of the line with the drone at its new state s: the exit's anchor, the spool, each node's loads and its
        /// ground, the chain's implicit step, the coil's mass, the trail's freeze and lift, and the force on the drone for the
        /// next step. hubs are the rotors' hubs (body m), thrust each rotor's mean thrust over the step (N), wind the step's
        /// field; events take Payout, TetherWedge and WorldBug.
        /// </summary>
        public void Step(in FlightState s, Double3[] hubsBody, in Corners thrust, WindField wind, StepEvents events, ref TetherTrace trace)
        {
            Corners still = s.RotorAngle;
            Step(s, hubsBody, thrust, wind, events, ref trace, still);
        }

        WindBatch _windBatch;
        int _windFirst = -1, _windCursor = -1;

        /// <summary>
        /// The batch worker the nodes' wind is evaluated on (FlightCore's, where the field has a profile), or null to evaluate
        /// every node in Loads. Either gives the same bits (WindBatch).
        /// </summary>
        public WindBatch WindBatch
        {
            get => _windBatch;
            set
            {
                _windBatch?.Join();
                _windBatch = value;
                _windFirst = -1;
            }
        }

        /// <summary>
        /// After the step's wind frame is begun, while the core builds its batch: every node's position but the exit's added,
        /// in order, each over its own ground (w7-farmland-wind 1.1: SampleGround's height, over a hole the soil's, as Ground
        /// reads it for the node); Loads takes their winds for the nodes still there bit for bit (a node the contacts put in
        /// is evaluated in Loads).
        /// </summary>
        public void AddWind()
        {
            _windFirst = -1;
            _soilCount = 0;
            if (_windBatch == null || !_attached) return;
            int n = _chain.Count - 1;
            for (int i = 0; i < n; i++) _windAt[i] = new XZ(_chain[i].Position.X, _chain[i].Position.Z);
            World.SampleGround(new ReadOnlySpan<XZ>(_windAt, 0, n), new Span<GroundSample>(_windGround, 0, n));
            for (int i = 0; i < n; i++)
            {
                GroundSample g = _windGround[i];
                double h = g.Height;
                if ((g.Flags & GroundFlags.Hole) != 0)
                {
                    // Over a hole the soil a ray finds, cast once: Ground takes it for the same node (w3-farmland-ground 2.4).
                    h = World.SoilUnder(_chain[i].Position, _soilRay, _soilHit, out _);
                    _soilAt[_soilCount] = _chain[i].Position;
                    _soilHeight[_soilCount++] = h;
                }
                int at = _windBatch.Add(_chain[i].Position, h);
                if (at < 0) break;
                if (i == 0) _windFirst = at;
            }
        }

        /// <summary>Step with the rotors' angles at the step's start (from), for the blade passes of the prop check (1.8).</summary>
        public void Step(in FlightState s, Double3[] hubsBody, in Corners thrust, WindField wind, StepEvents events, ref TetherTrace trace, in Corners from)
        {
            BrokeThisStep = false;
            if (!_attached)
            {
                _force = default;
                trace.PaidOut = PaidOut;
                trace.Flags = (byte)TetherFlags.Broken;
                trace.Nodes = 0;
                trace.Trail = _trailCount;
                return;
            }
            int n = _chain.Count;
            Quat q = s.Attitude;

            // The exit follows the drone.
            ref CableNode exit = ref _chain[n - 1];
            exit.AnchorTarget = s.Position + q.Rotate(_exitBody);
            exit.AnchorVelocity = s.Velocity + q.Rotate(Double3.Cross(s.BodyRate, _exitBody));
            exit.AnchorDamping = 2 * DetMath.Sqrt(ExitStiffness * exit.Mass);

            // The spool: the lip's wrap between the nozzle's axis out of the exit and the first segment, the section at the peel.
            Double3 aft = q.Rotate(_exitLead);
            Double3 first = _chain[n - 2].Position - exit.Position;
            _lipAngle = DetMath.Atan2(Double3.Cross(aft, first).Length(), Double3.Dot(aft, first));
            // The nozzle's law (design 18): point contacts at the bore's entry and the hole's edge; whether the line wraps the
            // edge is judged on the last step's tension outside (the factor is set before the step).
            _lipFactor = _capstanLip ? DetMath.Exp(_lipFriction * _lipAngle) : FiberCuts.LipFactor(_lipAngle, _freeAngle, _lipFriction, _lipCap, _bending, ExitTension);
            _section = exit.Material + _balloonLength;
            double irregularity = Irregularity(_section);
            bool empty = PaidOut >= _coilLine;
            _chain.Spool = new CableSpool
            {
                Active = true, Locked = empty, TwoWay = TwoWay, Lip = _lipFactor,
                Peel = _peelEnergy * irregularity, RateExponent = _rateExponent, ReferenceRate = _referenceRate,
                FluxMass = _dryMass, BalloonMass = _balloonMass,
            };

            // The members near each segment, and a node where one meets a segment between its nodes (1.7).
            if (_contacts != null)
            {
                _contacts.Prepare(_chain, s, events);
                n = _chain.Count;
                ref CableNode moved = ref _chain[n - 1]; // a node put into the exit's segment takes some of its mass
                moved.AnchorDamping = 2 * DetMath.Sqrt(ExitStiffness * moved.Mass);
            }

            // The ground under every node and every hub, one batch; the rotors' air from their thrust.
            for (int i = 0; i < n; i++) _at[i] = new XZ(_chain[i].Position.X, _chain[i].Position.Z);
            if (_rotors != null)
                for (int i = 0; i < 4; i++)
                {
                    _hubs[i] = s.Position + q.Rotate(hubsBody[i]);
                    _at[n + i] = new XZ(_hubs[i].X, _hubs[i].Z);
                }
            int batch = _rotors != null ? n + 4 : n;
            World.SampleGround(new ReadOnlySpan<XZ>(_at, 0, batch), new Span<GroundSample>(_ground, 0, batch));
            _soilCursor = 0;
            for (int i = 0; i < n; i++) Ground(i);
            _soilCount = 0;
            if (_rotors != null)
            {
                Double3 up = q.Rotate(new Double3(0, 1, 0));
                for (int i = 0; i < 4; i++)
                {
                    _thrust[i] = thrust[i];
                    Double3 v = s.Velocity + q.Rotate(Double3.Cross(s.BodyRate, hubsBody[i]));
                    GroundSample g = _ground[n + i];
                    _hubGround[i] = (g.Flags & GroundFlags.Hole) == 0 ? g.Height : World.SoilUnder(_hubs[i], _soilRay, _soilHit, out _);
                    // The drone's hub over its own terrain; over a hole the step's ground and frame, as the core's (WindField.At).
                    _hubAir[i] = wind.At(_hubs[i], (g.Flags & GroundFlags.Hole) == 0 ? g.Height : double.NaN) - v;
                }
                if (_rotors.BodyBlocks) _rotors.PlaceBody(s.Position, q);
                _rotors.Begin(_hubs, up, _thrust, _hubAir, _hubGround);
            }
            _windCursor = _windBatch != null ? _windFirst : -1;
            _windFirst = -1; // the batch's nodes are this step's only
            for (int i = 0; i < n - 1; i++) Loads(i, wind);
            ref CableNode end = ref _chain[n - 1];
            end.Drag = default;
            end.Force = default;
            end.Air = default;
            _contacts?.Assign(_chain, _ground, _groundHeight, _groundNormal, _groundAt, s);

            // The chain's step, its spacing from the exit.
            _chain.SetSpacing(_spacing, _nearLength, _growth, end.Material);
            long corrections = _chain.Corrections, safe = _chain.SafeSteps, clips = _chain.Clips, wedges = _chain.Wedges;
            _chain.Step(_dt);
            byte flags = 0;
            int wedgeNode = -1;
            if (_chain.SpoolPaying) flags |= (byte)TetherFlags.Paying;
            if (empty) flags |= (byte)TetherFlags.CoilEmpty;
            if (_chain.Clips != clips) flags |= (byte)TetherFlags.Clipped;
            if (_chain.SafeSteps != safe) flags |= (byte)TetherFlags.SafeStep;
            if (TwoWay) flags |= (byte)TetherFlags.TwoWay;
            if (_chain.Wedges != wedges)
            {
                flags |= (byte)TetherFlags.Wedge;
                CableWedge w = _chain.LastWedge;
                FlightEvent e = FlightEvent.Of(EventKind.TetherWedge);
                e.Index = w.Node >= 0 && w.Node < 255 ? (byte)w.Node : FlightEvent.NoIndex;
                e.Point = _chain[w.Node >= 0 && w.Node < _chain.Count ? w.Node : _chain.Count - 1].Position;
                e.A = w.Excess;
                e.B = w.Deficit;
                e.C = w.Removable;
                events.Add(e);
                wedgeNode = w.Node;
            }

            _contacts?.Finish(_chain, s, events);

            // The cuts and the link (1.8): a break leaves the near piece hanging from the exit, the far piece where it lies.
            if (_cuts != null && !Broken)
            {
                Cut(s, from, wedgeNode, events);
                if (!_attached)
                {
                    _force = default;
                    _contacts?.ClearReactions();
                    trace.PaidOut = PaidOut;
                    trace.PayoutSpeed = _chain.SpoolSpeed;
                    trace.PayoutTension = _chain.SpoolPackTension;
                    trace.LipAngle = _lipAngle;
                    trace.Irregularity = irregularity;
                    trace.Nodes = 0;
                    trace.Trail = _trailCount;
                    trace.Flags = (byte)(flags | (byte)TetherFlags.Broken);
                    long extra = _chain.Corrections - corrections;
                    trace.Corrections = (byte)(extra > 255 ? 255 : extra);
                    return;
                }
            }
            if (Broken) flags |= (byte)TetherFlags.Broken;
            if (_link != null && _link.InDip) flags |= (byte)TetherFlags.Dip;

            // The coil: a rebuild at each coil_rebuild_m of line paid out this flight.
            MassChanged = false;
            if (_coilPart >= 0)
            {
                double paid = PaidOut;
                int marks = paid > 0 ? (int)(paid / _rebuildStep) : 0;
                if (marks != _rebuilds)
                {
                    _rebuilds = marks;
                    double line = marks * _rebuildStep;
                    if (line > _coilLine) line = _coilLine;
                    CoilMass = _coilMass0 - _dryMass * line;
                    MassChanged = true;
                    FlightEvent e = FlightEvent.Of(EventKind.Payout);
                    e.Point = _chain[_chain.Count - 1].Position;
                    e.A = line;
                    e.B = CoilMass;
                    e.C = _chain.SpoolPackTension;
                    events.Add(e);
                }
            }

            if (!Broken) FreezeAndLift(events); // the near piece of a break has a free end: no trail

            // A node where the line lies over a terrain's crest the chord between two nodes would pass under (2.5), after the
            // remeshing, so no step starts, and no pose shows, a chord through the ground.
            if (_terrain) Crests(true);

            // The line's force on the drone for the next step: the exit's tension along the first segment, less the momentum
            // flux μ·ṡ² the leaving line carries (it leaves at its own velocity; the coil's mass change is no force).
            n = _chain.Count;
            Double3 outward = _chain[n - 2].Position - _chain[n - 1].Position;
            double len = outward.Length(), sdot = _chain.SpoolSpeed;
            double pull = _chain.SpoolExitTension - (sdot > 0 ? _dryMass * sdot * sdot : 0);
            _force = len > 0 ? outward * (pull / len) : default;

            trace.PaidOut = PaidOut;
            trace.PayoutSpeed = sdot;
            trace.PayoutTension = _chain.SpoolPackTension;
            trace.ExitTension = _chain.SpoolExitTension;
            trace.LipAngle = _lipAngle;
            trace.Irregularity = irregularity;
            trace.Nodes = n;
            trace.Trail = _trailCount;
            trace.Flags = flags;
            long added = _chain.Corrections - corrections;
            trace.Corrections = (byte)(added > 255 ? 255 : added);
        }

        /// <summary>The ground's piece nearest a node of radius r at x (a step's face it lies behind: w3-farmland-ground 2.2): a point
        /// of its plane and its normal, from StaticContacts' contact with the ground; unchanged where it finds none.</summary>
        public static void NearestGround(IWorldQuery world, Double3 x, double r, Span<StaticContact> buffer, ref Double3 point, ref Double3 normal)
        {
            var core = new Capsule(x, x, r);
            int found = world.StaticContacts(core, core, 0, buffer, QueryFilter.NoWires);
            for (int k = 0; k < found && k < buffer.Length; k++)
            {
                if (buffer[k].Object != WorldIds.Ground) continue;
                point = buffer[k].Point;
                normal = buffer[k].Normal;
                return;
            }
        }

        /// <summary>How far under its sample's plane a node lies behind a step's face, m (numerical: InsertGap, the line's travel
        /// in a step at 50 m/s, far past the ground spring's give of 0.1 mm a newton; an honest facet holds a node within it).</summary>
        public const double WallDepth = FiberContacts.InsertGap;

        // ---- the line over a terrain's crests (w3-farmland-ground 2.5) ----

        /// <summary>The crests' nodes a chain on a terrain holds besides the spacing law's span.max_nodes (assumed: the farmland
        /// line's 2 km over its hills holds 10–15 at a time; the room bounds the line's cost, PLT-04, and the deepest crests keep
        /// it when it is spent).</summary>
        public const int CrestNodes = 16;

        /// <summary>The deepest a segment lies in the ground between two tests of it, m: CrestGap and CrestSlack (below).</summary>
        public const double CrestDepth = CrestGap + CrestSlack;

        // The test of a segment against the ground: its chord raised by CrestGap entering the ground passes more than that under
        // it, vertically, so a node goes onto the ground at its deepest point. CrestGap is KeepGap, the reach a member on a slot
        // keeps its node to: a chord over a crest stays within it of the ground there, and of what stands on it. A test that finds
        // none lets the ends move CrestSlack (numerical: KeepGap, the tests' cost) before the next: a point's depth under the
        // ground (its distance from the surface) moves no more than the point, and a chord no more than CrestGap under the ground
        // vertically lies no deeper than that, so between tests no chord lies deeper than CrestDepth. A node is a crest's while it
        // lies on the ground (within CrestGap of its plane) and more than CrestKeep (InsertGap, half CrestGap: the hysteresis)
        // over the chord between its neighbours; one more than CrestGap over it becomes one. With the room spent, a crest more
        // than CrestYield times as deep as the shallowest held takes that one's room (numerical hysteresis). The deepest point:
        // the deepest of CrestSamples points across the stretch the raised chord lies in the ground; a piece's taut path over
        // the ground, the upper hull of CrestProfile samples under its chord (both numerical).
        const double CrestGap = FiberContacts.KeepGap, CrestKeep = FiberContacts.InsertGap, CrestSlack = FiberContacts.KeepGap;
        const double CrestYield = 2;
        const int CrestSamples = 9, CrestProfile = 32;
        readonly XZ[] _profileAt = new XZ[CrestProfile];
        readonly GroundSample[] _profileGround = new GroundSample[CrestProfile];
        readonly double[] _hullX = new double[CrestProfile + 2], _hullY = new double[CrestProfile + 2];

        /// <summary>The crests' nodes put in this flight, those refused (the crests' room spent: the chord stays under that crest,
        /// tested again once its ends move), and the segments tested against the ground.</summary>
        public long CrestsInserted { get; private set; }
        public long CrestsRefused { get; private set; }
        public long GroundTests { get; private set; }

        // After each step (the chain remeshed, so no step starts on a chord through the ground; hold): the nodes the ground holds
        // over a crest; then a node into each segment the test finds under the ground, from the exit's end so a split leaves the
        // indices below it. At the launch the tests alone, until they put none in (no slot holds a node yet). The exit's segment
        // is the spool's, within near_spacing_m of the drone: never tested.
        void Crests(bool hold)
        {
            int n = _chain.Count, crests = _chain.Crests;
            if (_chain[0].Crest)
            {
                _chain[0].Crest = false; // the trail's end, anchored where it froze
                crests--;
            }
            for (int i = 1; hold && i + 1 < n; i++)
            {
                ref CableNode a = ref _chain[i];
                bool held = OnGround(a.Contact0, a.Position) || OnGround(a.Contact1, a.Position) || a.Lifted; // a point lifted off the trail lies where it rested
                a.Lifted = false;
                double over = Over(_chain[i - 1].Position, a.Position, _chain[i + 1].Position);
                if (a.Crest)
                {
                    if (held && over >= CrestKeep) continue;
                    a.Crest = false;
                    crests--;
                }
                else if (held && over > CrestGap && crests < _chain.CrestCapacity)
                {
                    a.Crest = true;
                    crests++;
                }
            }
            for (int j = n - 3; j >= 0; j--) GroundTest(j);
        }

        // A slot on the ground (its plane under the node, or a step's face it lies behind) its node at x lies within CrestGap of.
        bool OnGround(in CableContact c, Double3 x) =>
            c.Active && (_contacts == null || c.MemberKind == (byte)FiberMember.Ground) && Double3.Dot(c.Normal, x - c.Point) <= CrestGap;

        // How far the ground holds x off the chord p–q, m: over it where the chord passes x in x–z (a crest), or across it in
        // x–z (the laid line's turn, held there by its friction: w3-farmland-ground 2.4), the larger; −∞ where the chord passes
        // x outside p–q's span. A node under its chord (in a hollow) is held off it by nothing but its own sag. The sliding
        // trail (FlightSetup.TetherSlidingTrail) held crests alone, so a laid turn was merged into its chord.
        double Over(Double3 p, Double3 x, Double3 q)
        {
            double dx = q.X - p.X, dz = q.Z - p.Z, dd = dx * dx + dz * dz;
            if (!(dd > 0)) return double.NegativeInfinity;
            double u = ((x.X - p.X) * dx + (x.Z - p.Z) * dz) / dd;
            if (!(u > 0 && u < 1)) return double.NegativeInfinity;
            double ax = x.X - (p.X + dx * u), az = x.Z - (p.Z + dz * u), across = DetMath.Sqrt(ax * ax + az * az);
            double over = x.Y - (p.Y + (q.Y - p.Y) * u);
            return over > across || _slidingTrail ? over : across;
        }

        // Segment j against the ground, once its ground certificate has run out.
        void GroundTest(int j)
        {
            ref CableNode na = ref _chain[j];
            Double3 a = na.Position, b = _chain[j + 1].Position, d = b - a;
            double allow = na.GroundSlack;
            if (na.GroundClear && FiberContacts.PointSegment(a, na.GroundA, na.GroundB) < allow * allow
                && FiberContacts.PointSegment(b, na.GroundA, na.GroundB) < allow * allow)
                return;
            na.GroundClear = false;
            double len = d.Length();
            if (!(len > 2 * CrestGap)) return; // every point of it lies within CrestGap of an end
            GroundTests++;
            _crestAt[0] = new XZ(a.X, a.Z);
            _crestAt[1] = new XZ(b.X, b.Z);
            World.SampleGround(_crestAt, _crestGround);
            double ha = (_crestGround[0].Flags & GroundFlags.Hole) == 0 ? a.Y - _crestGround[0].Height : double.PositiveInfinity;
            double hb = (_crestGround[1].Flags & GroundFlags.Hole) == 0 ? b.Y - _crestGround[1].Height : double.PositiveInfinity;
            // Both ends higher over their ground than the margin: a segment the ground lies farther than the margin from stays
            // off it until an end moves the margin (exact, the ground's own distance: its faces too).
            if (ha > _crestMargin && hb > _crestMargin)
            {
                var core = new Capsule(a, b, _radius);
                if (World.Query.StaticContacts(core, core, _crestMargin, _crestContact, QueryFilter.GroundOnly) == 0)
                {
                    Clear(ref na, a, b, _crestMargin);
                    return;
                }
            }
            // The chord raised by CrestGap, cast from an end above the ground (a ray from inside it finds nothing): where it
            // enters, and where it last leaves from the other end.
            bool fromA = ha > -CrestGap, fromB = hb > -CrestGap;
            if (!fromA && !fromB) return; // both ends behind a step's face: their own ground first
            var up = new Double3(0, CrestGap, 0);
            double t1 = Cast(fromA ? a + up : b + up, fromA ? d : -d, len), lo = 0, hi = 1;
            if (!(t1 <= len))
            {
                Clear(ref na, a, b, CrestSlack);
                return;
            }
            if (fromA)
            {
                lo = t1 / len;
                double t2 = fromB ? Cast(b + up, -d, len - t1) : double.PositiveInfinity;
                if (t2 <= len) hi = 1 - t2 / len;
            }
            else hi = 1 - t1 / len;
            // How deep each of CrestSamples points across that stretch lies in the ground (its distance from the surface: the
            // ground's own contact with the point; one outside it counts 0). The deepest, a piece of more than CrestGap either side,
            // takes the crest's node at its nearest point on the surface: under a crest that is the ground above it, beside a step's
            // face the face. A stretch no point of which lies deeper than CrestGap (a chord past a step's corner, under its top's
            // level but beside its face) takes none: the chord lies no deeper than CrestGap outside it (the raised chord is out of
            // the ground there) and no deeper than its deepest point and half the points' spacing in it (a point's depth moves no
            // more than the point), and it is tested again once its ends move what CrestDepth leaves past the deeper of the two.
            int best = -1;
            double deepest = 0, inside = double.NegativeInfinity;
            Double3 point = default, normal = default;
            for (int k = 0; k < CrestSamples; k++)
            {
                double f = lo + (hi - lo) * k / (CrestSamples - 1);
                Double3 x = a + d * f;
                var at = new Capsule(x, x, 0);
                if (World.Query.StaticContacts(at, at, 0, _crestContact, QueryFilter.GroundOnly) == 0) continue;
                double depth = -_crestContact[0].Distance;
                if (depth > deepest) deepest = depth;
                if (!(depth > inside) || !(f * len > CrestGap) || !((1 - f) * len > CrestGap)) continue;
                inside = depth;
                best = k;
                point = _crestContact[0].Point;
                normal = _crestContact[0].Normal;
            }
            if (!(deepest > CrestGap))
            {
                double slack = CrestDepth - DetMath.Max(CrestGap, deepest + 0.5 * (hi - lo) * len / (CrestSamples - 1));
                if (slack > 0) Clear(ref na, a, b, slack);
                return;
            }
            if (best < 0 || !(inside > CrestKeep)) return;
            Double3 on = point + normal * _radius;
            double share = Taut(a, on);
            share /= share + Taut(on, b);
            // The crest's node never stretches the line (p7d-tether design 18): where the segment's line covers both straight pieces
            // to the ground, no piece is shorter than its straight length (the taut paths' share had left the piece up a ledge's
            // face 2 % short, a 19 N spring that threw its node at 27 m/s); where it does not (a nearly taut chord cut through a
            // corner), the node goes from the chord toward the ground only as far as the line reaches, both pieces slack-free at
            // zero strain, and the ground's slot then holds the overlap left (CableContact.Sunk) until the line's own pull draws
            // line over the corner (it had been put 2.5 cm short, a 6.8 N spring).
            double rest = _chain[j + 1].Material - na.Material, la = (on - a).Length(), lb = (b - on).Length();
            if (_capstanLip) { }
            else if (rest >= la + lb)
            {
                double mine = rest * share;
                if (mine < la) mine = la;
                if (rest - mine < lb) mine = rest - lb;
                share = mine / rest;
            }
            else
            {
                double u = Double3.Dot(on - a, d) / (len * len);
                Double3 foot = a + d * (u < 0 ? 0 : u > 1 ? 1 : u);
                double lo2 = 0, hi2 = 1;
                for (int k = 0; k < 40; k++)
                {
                    double mid = 0.5 * (lo2 + hi2);
                    Double3 x = foot + (on - foot) * mid;
                    if ((x - a).Length() + (b - x).Length() > rest) hi2 = mid;
                    else lo2 = mid;
                }
                on = foot + (on - foot) * lo2;
                la = (on - a).Length();
                lb = (b - on).Length();
                share = la + lb > 0 ? la / (la + lb) : 0.5;
            }
            if (_chain.SplitCrest(j, share, on) || Yield(inside) && _chain.SplitCrest(j, share, on)) CrestsInserted++;
            else
            {
                CrestsRefused++;
                Clear(ref na, a, b, CrestSlack);
            }
        }

        // The crests' room spent: the shallowest crest held gives its room to one more than CrestYield times as deep (it stays a
        // node of the line, the spacing law's to keep or merge).
        bool Yield(double dip)
        {
            if (_chain.Crests < _chain.CrestCapacity) return false;
            int n = _chain.Count, least = -1;
            double shallowest = double.PositiveInfinity;
            for (int i = 1; i + 1 < n; i++)
            {
                if (!_chain[i].Crest) continue;
                double over = Over(_chain[i - 1].Position, _chain[i].Position, _chain[i + 1].Position);
                if (over < shallowest)
                {
                    shallowest = over;
                    least = i;
                }
            }
            if (least < 0 || !(dip > CrestYield * shallowest)) return false;
            _chain[least].Crest = false;
            return true;
        }


        static void Clear(ref CableNode a, Double3 from, Double3 to, double slack)
        {
            a.GroundClear = true;
            a.GroundA = from;
            a.GroundB = to;
            a.GroundSlack = slack;
        }

        // The length of the taut path from p to q over the ground, m: the upper hull of the ground's profile under the chord
        // (CrestProfile samples, each the ground's height there with the line's radius) between them. The crest's node shares
        // its segment's material by its pieces' taut paths, so no later crest's node, which lifts a piece no farther than its taut
        // path, leaves a piece short of line: the line laid over the ground holds at least that much there.
        double Taut(Double3 p, Double3 q)
        {
            Double3 d = q - p;
            for (int k = 0; k < CrestProfile; k++)
            {
                double f = (k + 1) / (CrestProfile + 1.0);
                _profileAt[k] = new XZ(p.X + d.X * f, p.Z + d.Z * f);
            }
            World.SampleGround(_profileAt, _profileGround);
            double across = DetMath.Sqrt(d.X * d.X + d.Z * d.Z);
            int top = 0;
            for (int k = -1; k <= CrestProfile; k++)
            {
                double f = k < 0 ? 0 : k == CrestProfile ? 1 : (k + 1) / (CrestProfile + 1.0);
                double y = k < 0 ? p.Y : k == CrestProfile ? q.Y : p.Y + d.Y * f;
                if (k >= 0 && k < CrestProfile && (_profileGround[k].Flags & GroundFlags.Hole) == 0)
                {
                    double g = _profileGround[k].Height + _radius;
                    if (g > y) y = g;
                }
                double x = f * across;
                while (top >= 2 && (_hullX[top - 1] - _hullX[top - 2]) * (y - _hullY[top - 2]) - (_hullY[top - 1] - _hullY[top - 2]) * (x - _hullX[top - 2]) >= 0)
                    top--;
                _hullX[top] = x;
                _hullY[top] = y;
                top++;
            }
            double length = 0;
            for (int k = 1; k < top; k++)
            {
                double dx = _hullX[k] - _hullX[k - 1], dy = _hullY[k] - _hullY[k - 1];
                length += DetMath.Sqrt(dx * dx + dy * dy);
            }
            return length;
        }

        // Where the ray o + t·d first enters the ground within limit, m (+∞ for none).
        double Cast(Double3 o, Double3 d, double limit)
        {
            _crestRay[0] = new Ray(o, d);
            World.Query.Raycast(_crestRay, limit, _crestHit, QueryFilter.GroundOnly);
            return _crestHit[0].Distance;
        }

        // Node i's ground: its plane within the reach (the coating's friction; a hold kept while it stays on), else none.
        void Ground(int i)
        {
            ref CableNode a = ref _chain[i];
            GroundSample g = _ground[i];
            double h = (g.Flags & GroundFlags.Hole) == 0 ? g.Height : Soil(a.Position);
            Double3 normal = (g.Flags & GroundFlags.Hole) == 0 ? g.Normal : new Double3(0, 1, 0);
            Double3 at = new Double3(a.Position.X, h, a.Position.Z);
            // Behind a step's face (w3-farmland-ground 2.2): SampleGround reports a step's top, so a node past a face below it
            // reads the step's height, and the top's plane would throw it there (1.4–1.6 m at the farmland's ledge). A node more
            // than WallDepth under its sample's plane is behind a face; its plane is the ground's nearest piece to it, the face.
            if (_terrain && (g.Flags & GroundFlags.Hole) == 0 && Double3.Dot(normal, a.Position - at) < -WallDepth)
                NearestGround(World.Query, a.Position, _radius, _face, ref at, ref normal);
            _groundHeight[i] = h;
            _groundNormal[i] = normal;
            _groundAt[i] = at;
            if (_contacts != null) return; // FiberContacts.Assign sets the slots, the plane among the members
            // The exit is on the drone and the trail's end on its point: neither takes the ground.
            if (i == _chain.Count - 1 || i == 0 && a.AnchorStiffness > 0 || !(a.Position.Y - h < _reach))
            {
                a.Contact0.Active = false;
                return;
            }
            double length = Tributary(i);
            Double3 point = at + normal * _radius;
            if (!a.Contact0.Active)
                a.Contact0 = CableContact.On(point, normal, default, GroundStiffnessPerMetre * length,
                    2 * DetMath.Sqrt(GroundStiffnessPerMetre * _mass) * length, GroundHold, _groundStatic, _groundKinetic, false);
            else
            {
                a.Contact0.Point = point;
                a.Contact0.Normal = normal;
                a.Contact0.Stiffness = GroundStiffnessPerMetre * length;
                a.Contact0.Damping = 2 * DetMath.Sqrt(GroundStiffnessPerMetre * _mass) * length;
                a.Contact0.HoldStiffness = GroundHold;
            }
            a.Contact0.Remeshable = true;
        }

        // The soil under a node over a hole: AddWind's cast for this node (the same point bit for bit, in the chain's order; a
        // node the contacts put in since is not among them), else a ray now. Either is SoilUnder's answer at the point.
        double Soil(Double3 x)
        {
            if (_soilCursor < _soilCount && _soilAt[_soilCursor].X == x.X && _soilAt[_soilCursor].Y == x.Y && _soilAt[_soilCursor].Z == x.Z)
                return _soilHeight[_soilCursor++];
            return World.SoilUnder(x, _soilRay, _soilHit, out _);
        }

        double Tributary(int i)
        {
            int n = _chain.Count;
            double before = i > 0 ? _chain[i].Material - _chain[i - 1].Material : 0;
            double after = i + 1 < n ? _chain[i + 1].Material - _chain[i].Material : 0;
            return 0.5 * (before + after);
        }

        // Node i's air and drag: the wind and the rotors' air at it; across the line White's fit at its Reynolds number, along
        // it skin friction on its surface, both on the relative air, as a linear drag about the node's velocity (the secant:
        // exact at the step's start, taken implicitly); a node lying in a wall jet is lifted by the wall.
        void Loads(int i, WindField wind)
        {
            ref CableNode a = ref _chain[i];
            int n = _chain.Count;
            Double3 x = a.Position;
            if (_windCursor >= 0 && _windBatch.TryAt(_windCursor, x, out Double3 air)) _windCursor++;
            else air = wind.At(x, _groundHeight[i]);
            if (_rotors != null) air = air + _rotors.At(x, _groundHeight[i], out _);
            Double3 t = (i + 1 < n ? _chain[i + 1].Position : x) - (i > 0 ? _chain[i - 1].Position : x);
            double tl = t.Length();
            t = tl > 0 ? t * (1 / tl) : new Double3(1, 0, 0);
            double length = Tributary(i);
            double uc = _drag.Apply(ref a, air, t, length);
            _loadPoint[i] = x;
            _loadVelocity[i] = a.Velocity;
            _loadAir[i] = air;
            _loadTangent[i] = t;
            _loadLength[i] = length;
            _loadForce[i] = a.Drag * (air - a.Velocity);
            _loadMaterial[i] = a.Material;
            _loadLift[i] = default;
            _loadCount = i + 1;
            a.Force = default;
            // The wall's lift on a cylinder lying in a cross-flow, falling to 0 at the gap's end (the wind's too, where it
            // blows at the ground).
            double gap = x.Y - _groundHeight[i] - _radius;
            if (gap < _liftGap * _diameter && uc > 0)
            {
                double share = gap > 0 ? 1 - gap / (_liftGap * _diameter) : 1;
                a.Force = _groundNormal[i] * (0.5 * _drag.Density * uc * uc * _diameter * _liftCoefficient * share * length);
                _loadLift[i] = a.Force;
            }
            // The air's push per metre on the node at rest, against the friction that holds a line lying still.
            Double3 still = air - t * Double3.Dot(air, t);
            double us = still.Length();
            _airPerMetre[i] = _drag.PerMetre(us);
        }

        // ---- the cuts and the link (1.8) ----

        // After the chain's step: a non-finite node broken there (the wedge), the prop's check, then the line's bends, the wear
        // and FiberCuts' checks in design 7's order; the first that holds breaks the line, else the link's margin and its dip.
        void Cut(in FlightState s, in Corners from, int wedgeNode, StepEvents events)
        {
            int n = _chain.Count;
            Quat q = s.Attitude;
            FiberBreak b = default;
            bool found = false;
            if (wedgeNode >= 0)
            {
                found = true;
                b.Mechanism = TetherMechanism.Wedge;
                b.Kind = (byte)FiberMember.Span;
                b.Segment = -1;
                b.Node = wedgeNode > 0 && wedgeNode < n - 1 ? wedgeNode : -1;
                int at = wedgeNode >= 0 && wedgeNode < n ? wedgeNode : n - 1;
                b.Material = _chain[at].Material;
                b.Point = _chain[at].Position;
                b.Radius = double.PositiveInfinity;
            }
            if (!found) found = Prop(s, from, out b);
            ref CableNode exit = ref _chain[n - 1];
            Double3 aft = q.Rotate(_exitLead);
            Double3 first = _chain[n - 2].Position - exit.Position;
            double lip = DetMath.Atan2(Double3.Cross(aft, first).Length(), Double3.Dot(aft, first));
            // The line's tension at the hole: the exit segment's, never above what the one-way spool lets out at (D-087): paying, its
            // law's (the pack's resistance through the lip's capstan, the balloon's inertia and the momentum flux), else its hold;
            // a segment stretched past it after the payout (a node moved by a contact in the step's end) is the payout's next step,
            // not a pull on the glass.
            double lipTension = _chain.Tension(n - 2);
            ref readonly CableSpool spool = ref _chain.Spool;
            if (spool.Active && !spool.Locked && !spool.TwoWay)
            {
                double most = _chain.SpoolPaying ? _chain.SpoolExitTension : spool.Lip * spool.Peel;
                if (lipTension > most) lipTension = most;
            }
            _cuts.Collect(_chain, lip, lipTension, exit.Position);
            // The hole's edge (D-087): the line paying out over it, and its lead turning about the nozzle's axis.
            Double3 lead = q.InverseRotate(first);
            double azimuth = DetMath.Atan2(Double3.Dot(lead, _azimuthY), Double3.Dot(lead, _azimuthX)), turn = 0;
            if (!double.IsNaN(_lipAzimuth))
            {
                turn = azimuth - _lipAzimuth;
                if (turn > 3.141592653589793) turn -= 2 * 3.141592653589793;
                else if (turn < -3.141592653589793) turn += 2 * 3.141592653589793;
            }
            _lipAzimuth = azimuth;
            LipWear = _cuts.WearLip(_chain.SpoolPaying ? _chain.SpoolSpeed * _dt : 0, turn, exit.Material, _contacts, events);
            _cuts.Wear(_chain, _dt, _contacts, events);
            if (!found)
            {
                double sdot = _chain.SpoolSpeed, peelAt = exit.Material + _balloonLength;
                double peel = _peelEnergy * Irregularity(peelAt) * (sdot > _referenceRate ? DetMath.Exp(_rateExponent * DetMath.Log(sdot / _referenceRate)) : 1);
                found = _cuts.Check(_chain, _chain.SpoolPaying, peel, _chain.SpoolPackTension, peelAt, _contacts, events, out b);
            }
            if (found)
            {
                Break(b, s, events);
                return;
            }
            _link.Update(exit.Material, _cuts, s.Step);
            if (_link.DipBegan)
            {
                ref readonly FiberBend l = ref _cuts.Bend(_link.Largest >= 0 ? _link.Largest : _cuts.BendCount - 1);
                FlightEvent e = Place(EventKind.LinkDip, l.Kind, l.Member, _contacts, l.Point, _link.Margin, _link.LargestLoss, 0);
                e.Index = l.Kind;
                events.Add(e);
            }
            if (_link.DipEnded)
            {
                FlightEvent e = Place(EventKind.LinkDip, _link.DipKind, _link.DipMember, _contacts, _link.DipPoint,
                    (s.Step - _link.DipStart) * _dt, _link.DipLeast, _link.DipLoss);
                e.Index = (byte)(_link.DipKind | 0x80);
                events.Add(e);
            }
        }

        // The prop's check (design 7): a segment crossing a turning disc (its slab, the blade's half-thickness either side of
        // the plane) between the blade's root and its tip is struck at the next blade pass: the pass in this step if a blade
        // crosses the crossing's azimuth between its angle at the step's start and now (PropStrikes' rule), so the line is cut
        // there (assumed certain: a 0.25 mm glass line struck at tens of m/s, PHY-19). A resting rotor (ContactModel.Resting:
        // slower than ContactSpec.BladeRestSpeed, its tip under 0.9 m/s, no carve holding it) turns no disc: its blades are the
        // drone's parts, which the line meets through FiberContacts and FiberCuts' bend and wear, as PropStrikes passes no blade
        // of it (the physics integration 2, model 59).
        bool Prop(in FlightState s, in Corners from, out FiberBreak b)
        {
            b = default;
            if (_blades == 0) return false;
            int n = _chain.Count;
            Quat q = s.Attitude;
            Double3 up = q.Rotate(new Double3(0, 1, 0));
            const double TwoPi = 2 * 3.141592653589793;
            for (int c = 0; c < 4; c++)
            {
                double omega = s.RotorSpeed[c];
                if (!(omega > 0) || _parts != null && _parts.Resting(c)) continue;
                Double3 hub = _hubs[c];
                double h = _discHalf[c];
                double start = from[c], sweep = s.RotorAngle[c] - start;
                if (sweep < 0) sweep += TwoPi;
                for (int j = 0; j < n - 1; j++)
                {
                    Double3 pa = _chain[j].Position, pb = _chain[j + 1].Position;
                    double za = Double3.Dot(pa - hub, up), zb = Double3.Dot(pb - hub, up);
                    if (za > h && zb > h || za < -h && zb < -h) continue;
                    double f = za * zb < 0 ? za / (za - zb) : DetMath.Abs(za) <= DetMath.Abs(zb) ? 0 : 1;
                    Double3 p = pa + (pb - pa) * f, rel = p - hub;
                    rel = rel - up * Double3.Dot(rel, up);
                    double r = rel.Length();
                    if (!(r >= _hubRadius) || !(r <= _rotorRadius)) continue;
                    Double3 v = q.InverseRotate(rel);
                    double psi = Wrap(DetMath.Atan2(_spin[c] * v.Z, v.X));
                    for (int bl = 0; bl < _blades; bl++)
                    {
                        double tip = _propulsion?.Rotor(c) != null ? _propulsion.Rotor(c).Blade(bl).TipRadius : 1;
                        if (!(r <= tip * _rotorRadius)) continue;
                        double ahead = Wrap(psi - (start + TwoPi * bl / _blades));
                        if (!(ahead > 0 && ahead <= sweep)) continue;
                        b.Mechanism = TetherMechanism.Prop;
                        b.Kind = (byte)FiberMember.Disc;
                        b.Member = (ulong)c;
                        b.Segment = j;
                        b.Fraction = f;
                        b.Node = j + 1 < n - 1 ? j + 1 : -1;
                        b.Material = _chain[j].Material + (_chain[j + 1].Material - _chain[j].Material) * f;
                        b.Turn = r;
                        b.Tension = _chain.Tension(j);
                        b.Radius = omega * r;
                        b.Point = p;
                        b.Section = _cuts.Section(b.Material);
                        return true;
                    }
                }
            }
            return false;
        }

        static double Wrap(double a)
        {
            const double TwoPi = 2 * 3.141592653589793;
            while (a >= TwoPi) a -= TwoPi;
            while (a < 0) a += TwoPi;
            return a;
        }

        // The break: its events (the strength it met as FiberSection with 0x80, then TetherBreak), the link lost, and the line
        // parted at the break's node: the far piece (the chain behind it) into the trail where it lies; the near piece the
        // chain from it to the exit, its end free; at the drone (the lip, the peel, the exit's segment) nothing hangs.
        void Break(in FiberBreak b, in FlightState s, StepEvents events)
        {
            if (b.Mechanism != TetherMechanism.Prop && b.Mechanism != TetherMechanism.Wedge)
                events.Add(_cuts.SectionEvent(_contacts, b.Kind, b.Member, b.Point, b.Section, b.Strength, b.Band, b.Stress, true));
            FlightEvent e = Place(EventKind.TetherBreak, b.Kind, b.Member, _contacts, b.Point, b.Turn, b.Tension, b.Radius);
            e.Index = (byte)((byte)b.Mechanism | b.Kind << 4);
            if (b.Kind == (byte)FiberMember.Disc)
            {
                int c = (int)b.Member;
                e.Object = FiberContacts.DroneObject;
                e.Shape = _discShape[c];
                e.Part = _discPart[c] >= 0 ? (ushort)_discPart[c] : FlightEvent.NoPart;
            }
            else if (b.Kind == (byte)FiberMember.Lip || b.Kind == (byte)FiberMember.Peel)
                e.Part = _coilPart >= 0 ? (ushort)_coilPart : FlightEvent.NoPart;
            events.Add(e);

            Broken = true;
            BrokeThisStep = true;
            BrokenAt = s.Step;
            LastBreak = b;
            _link.Break();

            int k = b.Node;
            if (b.Segment >= 0 && _chain.SplitAt(b.Segment, b.Fraction)) k = b.Segment + 1;
            int n = _chain.Count;
            TrailBeforeBreak = _trailCount;
            int last = k > 0 && k < n - 1 ? k : n - 1;
            for (int i = 1; i <= last && _trailCount < _trail.Length; i++)
            {
                _trail[_trailCount] = _chain[i].Position;
                _trailMaterial[_trailCount] = _chain[i].Material;
                _trailHeld[_trailCount] = 0;
                _trailAir[_trailCount] = 0;
                _trailCount++;
            }
            if (!(k > 0 && k < n - 1))
            {
                _attached = false;
                return;
            }
            for (int r = 0; r < k; r++) _chain.RemoveFirst();
            ref CableNode end = ref _chain[0];
            end.AnchorStiffness = 0;
            end.AnchorDamping = 0;
            end.AnchorFollows = false;
            end.RestTime = 0;
        }

        /// <summary>An event of kind at a place of the line: a member as FiberContact names it (through names), or the spool's
        /// exit lip and peel front (Object the drone, Shape none), a rotor's disc or the free span (Object none); A, B and C.</summary>
        public static FlightEvent Place(EventKind kind, byte memberKind, ulong member, FiberContacts names, Double3 point, double a, double b, double c)
        {
            if (memberKind <= (byte)FiberMember.Part && memberKind != (byte)FiberMember.None && names != null)
                return names.Named(kind, memberKind, member, point, a, b, c);
            FlightEvent e = FlightEvent.Of(kind);
            e.Index = memberKind;
            e.Object = memberKind == (byte)FiberMember.Lip || memberKind == (byte)FiberMember.Peel || memberKind == (byte)FiberMember.Disc
                ? FiberContacts.DroneObject : -1;
            e.Shape = -1;
            e.Point = point;
            e.A = a;
            e.B = b;
            e.C = c;
            return e;
        }

        // ---- the trail ----

        void FreezeAndLift(StepEvents events)
        {
            int n = _chain.Count;
            // At rest: on a support (the ground or an object's top), its holds stuck there through the step or the node slower
            // than trail.freeze_speed_m_s (w3-farmland-ground 2.4). A node the ground's stick spring holds rings about its hold
            // at up to the spring's frequency times its travel (3 cm/s for a 26 m lump, measured; the spring is the
            // regularisation of static friction, and its implicit step barely damps it), so the speed alone never saw it rest;
            // a node creeping by Coulomb under a light touch is at rest by its speed. A node draped over an object's twigs lies
            // on them (Touches). The sliding trail (FlightSetup.TetherSlidingTrail) held a node only while it pressed and only by
            // its speed.
            for (int i = 1; i < n - 1; i++)
            {
                ref CableNode a = ref _chain[i];
                bool resting = _slidingTrail
                    ? (Presses(a.Contact0) || Presses(a.Contact1)) && a.Velocity.Length() < _freezeSpeed
                    : (Touches(a.Contact0, a.Position) || Touches(a.Contact1, a.Position))
                        && (Sticks(a.Contact0) && Sticks(a.Contact1) || a.Velocity.Length() < _freezeSpeed);
                a.RestTime = resting ? a.RestTime + _dt : 0;
                if (a.StillTime > 0 && (a.Position - a.StillAt).Length() <= FiberContacts.InsertGap) a.StillTime += _dt;
                else
                {
                    a.StillAt = a.Position;
                    a.StillTime = _dt;
                }
            }
            // Freeze: node 1 at rest long enough, so held there by its support against whatever tension and air it bears (it
            // does not move), and farther than near_length_m of line from the exit: the line at the drone's feet stays live, so a
            // liftoff peels it off the ground (its compliance) while the trail behind lifts, rather than meeting a frozen point.
            // The point keeps the tension and the air it held. A node that long within InsertGap of one place between supports
            // (the line spanning a gap between twigs or a hollow, its weight carried by its tension, swinging there in the wind)
            // freezes with the supported node at rest beyond it (Spanned). The sliding trail froze only a node on a support, and also asked for air its ground's friction holds
            // (μs·w), which a node resting on a bush's top in wind never met.
            double exitMaterial = _chain[n - 1].Material;
            while (_chain.Count >= 3)
            {
                ref CableNode a = ref _chain[1];
                if (!(a.RestTime >= _freezeTime || !_slidingTrail && Spanned()) || _slidingTrail && !(_airPerMetre[1] <= _groundStatic * _weight)
                    || !(exitMaterial - a.Material > _nearLength)) break;
                if (_trailCount == _trail.Length)
                {
                    // Overflow: the oldest pair merged (the anchor kept), a world bug.
                    Array.Copy(_trail, 2, _trail, 1, _trailCount - 2);
                    Array.Copy(_trailMaterial, 2, _trailMaterial, 1, _trailCount - 2);
                    Array.Copy(_trailHeld, 2, _trailHeld, 1, _trailCount - 2);
                    Array.Copy(_trailAir, 2, _trailAir, 1, _trailCount - 2);
                    _trailCount--;
                    FlightEvent bug = FlightEvent.Of(EventKind.WorldBug);
                    bug.Index = 1;
                    bug.A = _trail.Length;
                    events.Add(bug);
                }
                _trail[_trailCount] = a.Position;
                _trailMaterial[_trailCount] = a.Material;
                _trailHeld[_trailCount] = _chain.Tension(1);
                _trailAir[_trailCount] = _airPerMetre[1];
                _trailCount++;
                _chain.RemoveFirst();
                ShiftAir(-1);
                AnchorTrailEnd();
            }
            // Lift: the trail's last point back into the chain while the chain pulls it off where it lies (Pulled), or the air
            // at it passes what it held at rest by the ground's friction. The sliding trail lifted it once the pull's size passed
            // the tension it held and the friction of trail.hold_length_m of line, whichever way it pulled, or the air passed the
            // ground's friction alone.
            while (_trailCount >= 2 && !_chain.SpacingFull && (_slidingTrail
                ? _chain.Tension(0) > _trailHeld[_trailCount - 1] + _hold || _airPerMetre[0] > _groundStatic * _weight
                : Pulled() || _airPerMetre[0] > _trailAir[_trailCount - 1] + _groundStatic * _weight))
            {
                ref CableNode a = ref _chain[0];
                a.AnchorStiffness = 0;
                a.AnchorDamping = 0;
                a.RestTime = 0;
                a.StillTime = 0;
                a.Lifted = !_slidingTrail;
                _trailCount--;
                _chain.InsertFirst(_trail[_trailCount - 1], _trailMaterial[_trailCount - 1]);
                ShiftAir(1);
                AnchorTrailEnd();
            }
        }

        // The trail's last point is pulled off where it lies (w3-farmland-ground 2.4). The chain pulls it with P along its
        // first segment; the laid run behind it (the trail's last segment, straight by construction) holds it with the tension
        // it froze with, T_h, back along the run.
        // - The part of P along the run's way out, past T_h, is a pull on the laid line: it stretches the run (micro-slip, no
        //   point moves) and reaches back along the trail, less the run's static friction μs·w a metre, until the anchor holds
        //   it; a trail point it reaches turns it by its corner, T·|u_in − u_out| more across, which the point holds while that
        //   stays within its static friction μs·w·(its lumped line) less what its own held tension already takes there. A
        //   corner it would move lifts the end (the chain then has the line, a point at a time).
        // - The rest of P (across the run, up off the ground, or back over it) bends or folds the line at the point, which the
        //   static friction of the line the point lumps (half the run, half the chain's first segment) holds.
        // Trail points on one place (a wrap round a twig froze them so) are one point: the run is the last of some length, the
        // point lumps the line to it. A chain's first segment of no length (or a trail all on one place) leaves no direction: the
        // pull's size against T_h and the friction of the line the point lumps.
        bool Pulled()
        {
            int t = _trailCount, r = t - 2;
            double holds = _groundStatic * _weight;
            Double3 run = _trail[t - 1] - _trail[r];
            while (!(run.Length() > 0) && r > 0) run = _trail[t - 1] - _trail[--r];
            Double3 first = _chain[1].Position - _chain[0].Position;
            double lr = run.Length(), lf = first.Length(), tension = _chain.Tension(0);
            double lumped = 0.5 * (_trailMaterial[t - 1] - _trailMaterial[r] + _chain[1].Material - _chain[0].Material);
            if (!(lr > 0) || !(lf > 0)) return tension > _trailHeld[t - 1] + holds * lumped;
            Double3 ut = run * (1 / lr), pull = first * (tension / lf);
            double along = Double3.Dot(pull, ut), outward = along > 0 ? along : 0;
            if ((pull - ut * outward).Length() > holds * lumped) return true;
            double excess = outward - _trailHeld[t - 1];
            Double3 uOut = ut;
            for (int k = r; k > 0 && excess > 0; k--)
            {
                excess -= holds * (_trailMaterial[k == r ? t - 1 : k + 1] - _trailMaterial[k]);
                if (!(excess > 0)) break;
                Double3 back = _trail[k] - _trail[k - 1];
                double lb = back.Length();
                if (!(lb > 0)) continue;
                Double3 uIn = back * (1 / lb);
                double turn = (uOut - uIn).Length();
                double margin = holds * 0.5 * (_trailMaterial[k + 1] - _trailMaterial[k - 1]) - _trailHeld[k] * turn;
                if (excess * turn > margin) return true;
                uOut = uIn;
            }
            return false;
        }

        // Node 1 still trail.freeze_seconds (within InsertGap of one place) in a span the line holds between the trail's end and
        // a node at rest on a support: every node from it to that one still as long, and each not on a support no more than
        // InsertGap over the chord between its neighbours (a span its tension holds sags under its chord; a node above it is
        // held by something else, as the rotors' fountain holds one under a hovering drone, and falls when that goes).
        bool Spanned()
        {
            int n = _chain.Count;
            for (int i = 1; i < n - 1; i++)
            {
                ref CableNode a = ref _chain[i];
                if (!(a.StillTime >= _freezeTime)) return false;
                if (a.RestTime >= _freezeTime)
                {
                    if (i > 1) return true;
                    continue;
                }
                Double3 p = _chain[i - 1].Position, q = _chain[i + 1].Position, x = a.Position;
                double dx = q.X - p.X, dz = q.Z - p.Z, dd = dx * dx + dz * dz;
                double u = dd > 0 ? ((x.X - p.X) * dx + (x.Z - p.Z) * dz) / dd : 0.5;
                if (u < 0) u = 0;
                else if (u > 1) u = 1;
                if (x.Y - (p.Y + (q.Y - p.Y) * u) > FiberContacts.InsertGap) return false;
            }
            return false;
        }

        static bool Presses(in CableContact c) => c.Active && c.NormalForce > 0;

        // A slot whose member the node presses, or, for an object's member (not the ground's plane or a layer, which a node at
        // rest presses with its weight), lies within InsertGap of: a node draped over twigs, its weight carried by the line there,
        // touches them on and off.
        static bool Touches(in CableContact c, Double3 x) =>
            c.Active && (c.NormalForce > 0 || c.MemberKind != (byte)FiberMember.None && c.MemberKind != (byte)FiberMember.Ground
                && c.MemberKind != (byte)FiberMember.Layer && Double3.Dot(c.Normal, x - c.Point) <= FiberContacts.InsertGap);

        // A pressing slot whose hold stuck through the step (no slip over its member, no line sliding through it).
        static bool Sticks(in CableContact c) => !Presses(c) || !c.Slid && !c.Sliding;

        // The per-node air test follows the nodes as the trail takes or gives one (the new node 0, a trail point, at rest: 0).
        void ShiftAir(int by)
        {
            int n = _chain.Count;
            if (by < 0) Array.Copy(_airPerMetre, 1, _airPerMetre, 0, n);
            else
            {
                Array.Copy(_airPerMetre, 0, _airPerMetre, 1, n - 1);
                _airPerMetre[0] = 0;
            }
        }

    }
}
