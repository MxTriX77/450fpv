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
    /// - The exit is the chain's last node, anchored to the drone's rear cap (spool.exit_position_m) and following it; line
    ///   flows out through it by CableSpool's law (the peel's rate law at the section's irregularity, the momentum flux,
    ///   the balloon, the lip's capstan e^(μθ) over the wrap between the canister's aft axis and the first segment), and
    ///   never back. The coil's mass falls by the line paid out and the mass model is rebuilt each span.coil_rebuild_m.
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
    ///   part of the drone near it; resting on any of them trail.freeze_seconds slower than trail.freeze_speed_m_s (so held),
    ///   more than near_length_m of line from the exit, it freezes into the trail with the tension it held; the trail's last
    ///   point lifts back into the chain when the pull at it passes that and the friction of trail.hold_length_m of line, or
    ///   the rotors' air passes what holds it.
    /// - The launch: the line laid on the ground from its anchor (launch.anchor_behind_start_m behind the start, or a given
    ///   layout) to beneath the exit, then up to it.
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
        readonly Double3 _exitBody;
        readonly double _lipFriction, _peelEnergy, _rateExponent, _referenceRate, _balloonLength, _balloonMass, _sectionLength;
        readonly double _irregularityLo, _irregularityHi;
        readonly SeedStream _irregularity;

        // The span.
        readonly double _spacing, _nearLength, _growth, _reach, _groundStatic, _groundKinetic, _rebuildStep;

        // The trail.
        readonly Double3[] _trail;
        readonly double[] _trailMaterial, _trailHeld;
        readonly double _freezeTime, _freezeSpeed, _hold;
        int _trailCount;

        // The coil.
        readonly int _coilPart;
        readonly double _coilMass0, _coilLine;
        readonly double _exitMaterial0;
        int _rebuilds;

        // Per-step buffers: the ground under each node and each hub (one batch), the rotors' state.
        readonly XZ[] _at;
        readonly GroundSample[] _ground;
        readonly double[] _groundHeight;
        readonly Double3[] _groundNormal;
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
            TwoWay = setup.TetherTwoWay;

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
            _lipFriction = table.Number("spool.lip_friction");
            _rateExponent = table.Number("spool.peel_rate_exponent");
            _referenceRate = table.Number("spool.peel_reference_rate_m_s");
            _balloonLength = table.Number("spool.balloon_length_m");
            _balloonMass = _dryMass * _balloonLength;
            _sectionLength = table.Number("fiber.section_length_m");
            double[] peel = table.Row("spool.peel_energy_j_m").Values;
            SeedStream peelStream = random.Open("spool.peel");
            _peelEnergy = random.Draw(ref peelStream, "peel_energy_j_m", peel[0], peel[1]);
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

            _freezeTime = table.Number("trail.freeze_seconds");
            _freezeSpeed = table.Number("trail.freeze_speed_m_s");
            int capacity = (int)table.Number("trail.capacity");
            _hold = _groundStatic * _weight * table.Number("trail.hold_length_m");
            _trail = new Double3[capacity];
            _trailMaterial = new double[capacity];
            _trailHeld = new double[capacity];

            _chain = new CableChain(nodes, _mass, table.Number("fiber.axial_stiffness_n")) { Gravity = FlightCore.Gravity };
            _at = new XZ[nodes + 4];
            _ground = new GroundSample[nodes + 4];
            _groundHeight = new double[nodes];
            _groundNormal = new Double3[nodes];
            _airPerMetre = new double[nodes];
            _loadPoint = new Double3[nodes];
            _loadVelocity = new Double3[nodes];
            _loadAir = new Double3[nodes];
            _loadTangent = new Double3[nodes];
            _loadForce = new Double3[nodes];
            _loadLift = new Double3[nodes];
            _loadMaterial = new double[nodes];
            _loadLength = new double[nodes];
            if (setup.Propulsion != null) _rotors = new RotorAir(table, setup.Propulsion.Props.Diameter / 2, !setup.TetherDesign4Air);

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
                _cuts = new FiberCuts(table, random, setup.TetherOneStrength);
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

        /// <summary>The lip's capstan e^(μθ) at the last step's wrap.</summary>
        public double Lip => DetMath.Exp(_lipFriction * _lipAngle);

        // ---- the launch ----

        // The line laid from the anchor to beneath the exit, then up to the exit, its material its length; nodes by the spacing
        // law from the exit; node 0 the anchor (the trail's first point), the exit anchored to the drone.
        void Launch(FlightSetup setup, in FlightState start)
        {
            Double3 exit = start.Position + start.Attitude.Rotate(_exitBody);
            Double3[] layout = setup.TetherLayout;
            int points;
            Double3[] path;
            double foot = GroundAt(exit);
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
                double behind = _table.Number("launch.anchor_behind_start_m");
                path = new Double3[3];
                path[0] = OnGround(new Double3(exit.X + back.X * behind, 0, exit.Z + back.Z * behind));
                points = 1;
            }
            path[points++] = new Double3(exit.X, foot + _radius, exit.Z);
            path[points++] = exit;
            var along = new double[points];
            for (int i = 1; i < points; i++) along[i] = along[i - 1] + (path[i] - path[i - 1]).Length();
            double total = along[points - 1];

            // The material coordinates from the exit back: near_spacing_m within near_length_m, then growing.
            var s = new double[_chain.Capacity];
            int n = 0;
            double at = total;
            s[n++] = total;
            while (at > 0)
            {
                double away = total - at, h = away <= _nearLength ? _spacing : _spacing + (_growth - 1) * (away - _nearLength);
                at -= h;
                if (at <= 1e-9 || n == _chain.Capacity - 1) at = 0;
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

            // The spool: the lip's wrap between the canister's aft axis and the first segment, the section at the peel.
            Double3 aft = q.Rotate(new Double3(0, 0, -1));
            Double3 first = _chain[n - 2].Position - exit.Position;
            _lipAngle = DetMath.Atan2(Double3.Cross(aft, first).Length(), Double3.Dot(aft, first));
            _section = exit.Material + _balloonLength;
            double irregularity = Irregularity(_section);
            bool empty = PaidOut >= _coilLine;
            _chain.Spool = new CableSpool
            {
                Active = true, Locked = empty, TwoWay = TwoWay, Lip = DetMath.Exp(_lipFriction * _lipAngle),
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
            for (int i = 0; i < n; i++) Ground(i);
            if (_rotors != null)
            {
                Double3 up = q.Rotate(new Double3(0, 1, 0));
                for (int i = 0; i < 4; i++)
                {
                    _thrust[i] = thrust[i];
                    Double3 v = s.Velocity + q.Rotate(Double3.Cross(s.BodyRate, hubsBody[i]));
                    _hubAir[i] = wind.At(_hubs[i]) - v;
                    GroundSample g = _ground[n + i];
                    _hubGround[i] = (g.Flags & GroundFlags.Hole) == 0 ? g.Height : World.SoilUnder(_hubs[i], _soilRay, _soilHit, out _);
                }
                _rotors.Begin(_hubs, up, _thrust, _hubAir, _hubGround);
            }
            for (int i = 0; i < n - 1; i++) Loads(i, wind);
            ref CableNode end = ref _chain[n - 1];
            end.Drag = default;
            end.Force = default;
            end.Air = default;
            _contacts?.Assign(_chain, _ground, _groundHeight, _groundNormal, s);

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

        // Node i's ground: its plane within the reach (the coating's friction; a hold kept while it stays on), else none.
        void Ground(int i)
        {
            ref CableNode a = ref _chain[i];
            GroundSample g = _ground[i];
            double h = (g.Flags & GroundFlags.Hole) == 0 ? g.Height : World.SoilUnder(a.Position, _soilRay, _soilHit, out _);
            Double3 normal = (g.Flags & GroundFlags.Hole) == 0 ? g.Normal : new Double3(0, 1, 0);
            _groundHeight[i] = h;
            _groundNormal[i] = normal;
            if (_contacts != null) return; // FiberContacts.Assign sets the slots, the plane among the members
            // The exit is on the drone and the trail's end on its point: neither takes the ground.
            if (i == _chain.Count - 1 || i == 0 && a.AnchorStiffness > 0 || !(a.Position.Y - h < _reach))
            {
                a.Contact0.Active = false;
                return;
            }
            double length = Tributary(i);
            Double3 point = new Double3(a.Position.X, h, a.Position.Z) + normal * _radius;
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
            Double3 air = wind.At(x);
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
            Double3 aft = q.Rotate(new Double3(0, 0, -1));
            Double3 first = _chain[n - 2].Position - exit.Position;
            double lip = DetMath.Atan2(Double3.Cross(aft, first).Length(), Double3.Dot(aft, first));
            _cuts.Collect(_chain, lip, _chain.Tension(n - 2), exit.Position);
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
            for (int i = 1; i < n - 1; i++)
            {
                ref CableNode a = ref _chain[i];
                bool held = a.Contact0.Active && a.Contact0.NormalForce > 0 || a.Contact1.Active && a.Contact1.NormalForce > 0;
                bool resting = held && a.Velocity.Length() < _freezeSpeed;
                a.RestTime = resting ? a.RestTime + _dt : 0;
            }
            // Freeze: node 1 at rest long enough on the ground, so held there by its friction against whatever tension it bears
            // (it does not move), the air not lifting it, and farther than near_length_m of line from the exit: the line at the
            // drone's feet stays live, so a liftoff peels it off the ground (its compliance) while the trail behind lifts, rather
            // than meeting a frozen point. The point keeps the tension it held.
            double exitMaterial = _chain[n - 1].Material;
            while (_chain.Count >= 3)
            {
                ref CableNode a = ref _chain[1];
                if (!(a.RestTime >= _freezeTime) || !(_airPerMetre[1] <= _groundStatic * _weight) || !(exitMaterial - a.Material > _nearLength)) break;
                if (_trailCount == _trail.Length)
                {
                    // Overflow: the oldest pair merged (the anchor kept), a world bug.
                    Array.Copy(_trail, 2, _trail, 1, _trailCount - 2);
                    Array.Copy(_trailMaterial, 2, _trailMaterial, 1, _trailCount - 2);
                    Array.Copy(_trailHeld, 2, _trailHeld, 1, _trailCount - 2);
                    _trailCount--;
                    FlightEvent bug = FlightEvent.Of(EventKind.WorldBug);
                    bug.Index = 1;
                    bug.A = _trail.Length;
                    events.Add(bug);
                }
                _trail[_trailCount] = a.Position;
                _trailMaterial[_trailCount] = a.Material;
                _trailHeld[_trailCount] = _chain.Tension(1);
                _trailCount++;
                _chain.RemoveFirst();
                ShiftAir(-1);
                AnchorTrailEnd();
            }
            // Lift: the trail's last point back into the chain while the pull at it passes the tension it held at rest and the
            // friction of trail.hold_length_m of line, or the air passes what holds it.
            while (_trailCount >= 2 && _chain.Count < _chain.Capacity
                && (_chain.Tension(0) > _trailHeld[_trailCount - 1] + _hold || _airPerMetre[0] > _groundStatic * _weight))
            {
                ref CableNode a = ref _chain[0];
                a.AnchorStiffness = 0;
                a.AnchorDamping = 0;
                a.RestTime = 0;
                _trailCount--;
                _chain.InsertFirst(_trail[_trailCount - 1], _trailMaterial[_trailCount - 1]);
                ShiftAir(1);
                AnchorTrailEnd();
            }
        }

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
