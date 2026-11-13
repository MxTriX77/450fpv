using Fpv450.Sim.Contact;

namespace Fpv450.Sim.Tether
{
    /// <summary>
    /// The rotors' air on the line (p7d-tether design 4): momentum theory's flow about each actuator disc, from its own thrust,
    /// so a full-throttle punch moves the line hardest. Each step Begin fixes every rotor's induced velocity and wake; At gives
    /// the rotors' added air velocity at a point, world m/s, summed over the rotors (the wind is the caller's).
    /// - The induced velocity: Glauert's v_i·√(u_p² + (u_a + v_i)²) = v_h², v_h = √(T/(2ρπR²)) (published, Johnson 1980,
    ///   Leishman 2006), with u_a the free stream down through the disc and u_p across it; in a descent (u_a &lt; 0), where
    ///   the momentum solution fails, the hover's v_h (assumed).
    /// - Above a disc (on its intake side), inside its radius: the vortex cylinder's inflow toward the disc,
    ///   v_i·(1 − z/√(z² + R²)) (published, on its axis); outside it: a hemispherical sink of the disc's flux πR²·v_i,
    ///   Q/(2πd²) toward the hub (derived).
    /// - Below it: the wake column of radius far_wake_radius_ratio·R moving at far_wake_speed_ratio·v_i along −n (published
    ///   far wake), laid along the flow through the disc (the free stream with v_i, so a forward flight sweeps it back).
    /// - Where the column meets the ground, a radial wall jet of the column's flux, u = πR²·v_i/(2π·r·h_j) outward from where
    ///   it lands, inside h_j of the ground (mass conservation; h_j from the table).
    /// The wake with distance (design 19): the columns hold to one motor distance l below the hubs' centre (along the jets'
    /// mean direction) and from 2.5 l the four have merged into one turbulent round jet, U_C = U_H·Bd/(s/l − s₀/l),
    /// r½ = S·(s/l − s₀/l)·l, U = U_C/(1 + (√2 − 1)ξ²)² (published, Bauersfeld et al. 2024), U_H the rotors' rms v_i; between,
    /// linearly blended (assumed). Where its axis meets the ground (from any height) its momentum turns into a radial wall jet:
    /// u_m = U_g·(r_i/r)^1.1, δ = 0.098·r·(r/H)^−0.1, Verhoff's profile across it (published, Poreh et al. 1967, Verhoff 1963),
    /// r_i from the momentum flux, the stagnation flow inside it (derived); the near landings (their wall jets, the fountain and
    /// the vortices) share the ground by 1 − w(s_g). The known-wrong (FlightSetup.TetherWakeReach4R, model 94's field): each
    /// column without end, a wall jet only within wake_reach_over_radius·R, no merged jet.
    /// The fountain and the ground vortex (p7d-tether 1.6b, D-075), the paths by which a line under or ahead of the drone
    /// reaches a disc (off for the known-wrong design 4 field, FlightSetup.TetherDesign4Air):
    /// - The fountain: where the wall jets of two rotors meet (at a point, the two strongest; their meeting line the locus of
    ///   equal speed, the measured stagnation line of equal rotors at their midpoint, Lee et al. 2026) the jets' flux and
    ///   momentum normal to it turn up into a plane jet, its momentum fountain_momentum_ratio of theirs (derived from Dekker
    ///   et al. 2023's fountain reaching 3R at H/R 4), self-similar and Gaussian across with the plane jet's spreading rate
    ///   (published), its virtual origin where it carries the jets' flux, rising linearly from the ground's stagnation line
    ///   through the jets' thickness h_j (derived: stagnation flow); from the ground up to the colliding discs' plane, where
    ///   their intake takes it (re-ingestion: at H/R 2 the measured fountain rises above the discs and is re-ingested); never
    ///   inside a wake column. Whole in the hover-like regime, fading to nothing as μ* = U/v_h rises from
    ///   hover_like_advance_ratio to ground_vortex_advance_ratio's start (published bounds, Wolf et al. 2022 after Sheridan
    ///   and Wiesner 1977 and Curtiss et al. 1984; the linear fade assumed).
    /// - The ground vortex: each rotor whose wake lands in a stream along the ground (U, the air at the hub across the
    ///   vertical less the hub's motion) rolls its upstream wall jet into a vortex ahead of the landing point at
    ///   offset·R + penetration·r_p, r_p = R²·v_i/(2·h_j·U) where the jet meets the stream (derived from Wolf et al. 2022's
    ///   full-scale centres, 1.07 R and 0.49 R ahead of the hub at μ* 0.55 and 0.80), at the jet's height, spanning the
    ///   wake's width, a straight vortex with a Scully core and its image under the ground, its circulation
    ///   ground_vortex_circulation·v_h·R (derived from Wolf et al.'s speeds under it) growing from hover to
    ///   hover_like_advance_ratio, fading out over ground_vortex_advance_ratio (published bounds; the ramps assumed); a rotor
    ///   whose upstream jet meets another rotor's landing wake (within its span, ahead of it) forms none (assumed).
    /// The drone's body over the fountain (p7d-tether, model 61): the rising air meets the drone's own contact shapes (every
    /// shape but the discs and a turning rotor's blades: the fuselage's stack, the coil, the packs, the payload, the arms, the
    /// legs; ContactModel's, at the step's pose) and does not pass through them. On the vertical through a point:
    /// - Inside a shape: no air.
    /// - Above a shape, within its lee (body_lee_length_over_diameter of its diameter over its top): no rise; the rising air
    ///   parts round the body and leaves a separated near wake behind it whose mean speed is taken as nothing (the measured
    ///   recirculation bubble behind a bluff body; assumed from the published band).
    /// - Below a shape's underside, within the impingement layer δ = min(fountain_impingement_depth_ratio·L_w, δ_body): the
    ///   stagnation and the deflection. L_w is the plane jet's length from its virtual origin to the underside (a plane jet
    ///   impinging on a wall stays free to 70–75 % of its length, published); δ_body the bluff body's own stagnation depth, its
    ///   potential flow's slope at the stagnation point, a/2 under a capsule's cylinder (U·(1 − a²/r²)) and a/3 under a sphere
    ///   (U·(1 − a³/r³)) (published, Batchelor 1967 §6.6, §6.8), the smaller governing (a body much wider than the jet is a
    ///   wall to it, one much narrower a bluff body in it: assumed composition). In the layer the rise falls linearly to
    ///   nothing at the underside (the stagnation flow, Hiemenz's near-wall law), w = W_top·d/δ with W_top the free fountain at
    ///   the layer's top, and by continuity (∂u/∂s = −∂w/∂y in the sheet's cross plane) the air turns outward across the sheet,
    ///   u = (W_c/δ)·∫₀^s e^(−ln2·t²/b²)dt along its normal away from the meeting line, never faster than the jet's own
    ///   centreline W_c (Bernoulli: the stagnation pressure ½ρW_c² turns it, it cannot speed it; published), only under the
    ///   body (the turned wall jet along the underside, its decay over the body's half-width left out: assumed).
    /// - Shapes whose underside stands above the colliding discs' plane are the discs' business (the fountain turns into their
    ///   intake first). Without a body (FlightSetup.TetherFountainThroughBody, the known-wrong) the fountain is model 59's.
    /// Only + − × ÷ √ and DetMath.Exp; preallocated; the ground under each hub is the caller's (one SampleGround batch with
    /// the nodes).
    /// </summary>
    public sealed class RotorAir
    {
        const int Rotors = 4;
        const double Pi = 3.141592653589793;

        // ln 2: the Gaussian's half-width at half its height (the plane jet's half-width b, Pope 2000's spreading rate).
        const double Ln2 = 0.6931471805599453;

        // Glauert's iterations (numerical: Newton from v_h converges to rounding within 6 on the drone's flight envelope).
        const int Iterations = 8;

        readonly double _radius, _density, _wakeSpeed, _wakeRadius, _reach, _jetThickness;
        readonly Double3[] _hub = new Double3[Rotors], _normal = new Double3[Rotors], _wake = new Double3[Rotors];
        readonly Double3[] _landing = new Double3[Rotors];
        readonly double[] _induced = new double[Rotors], _ground = new double[Rotors];
        readonly bool[] _jet = new bool[Rotors];

        // 1.6b: the fountain's and the ground vortex's constants (the table's) and the step's state.
        readonly bool _recirculation;
        readonly double _fountainMomentum, _spread, _hoverLike, _vortexEnd, _vortexGone, _vortexOffset, _vortexPenetration;
        readonly double _vortexHeight, _vortexCore, _vortexCirculation, _vortexSpan;
        readonly double _profileMass, _profileMomentum; // ∫e^(−ln2·η²)dη = √(π/ln2), ∫e^(−2·ln2·η²)dη = √(π/(2·ln2))
        readonly double _jetSpeed, _originScale, _gaussScale, _centreScale, _inverseJet; // the fountain's constants, once

        // The fountain's Gaussian is not evaluated beyond e^−40 (4e-18) of its peak, 7.6 half-widths out (numerical).
        const double CutOff = -40;
        readonly double[] _hover = new double[Rotors], _fade = new double[Rotors], _advance = new double[Rotors];
        readonly double[] _circulation = new double[Rotors];
        readonly Double3[] _stream = new Double3[Rotors], _vortexA = new Double3[Rotors], _vortexB = new Double3[Rotors];
        readonly Double3[] _vortexCentre = new Double3[Rotors];

        // Model 61: the drone's body over the fountain (null: it passes through, model 59's field) and the step's world shapes.
        readonly ContactModel _body;
        readonly Double3[] _bodyA, _bodyB;
        readonly double[] _bodyRadius, _boxX0, _boxX1, _boxZ0, _boxZ1; // each placed shape's plan box, world m
        int _placed;
        double _allX0, _allX1, _allZ0, _allZ1; // the placed body's plan box
        readonly double _impingement, _leeLength, _sqrtLn2, _halfMass;

        // A capsule whose axis lies within this of the vertical (1 − cos²) is taken as vertical on the column (numerical).
        const double Vertical = 1e-12;

        // Abramowitz and Stegun 7.1.26 (1964): erf(x) = 1 − (a₁t + … + a₅t⁵)·e^(−x²), t = 1/(1 + p·x), |error| ≤ 1.5e-7
        // (published): the deflection's Gaussian integral.
        const double ErfP = 0.3275911, ErfA1 = 0.254829592, ErfA2 = -0.284496736, ErfA3 = 1.421413741, ErfA4 = -1.453152027,
            ErfA5 = 1.061405429;

        // Design 19: the wake with distance (the table's constants) and the step's merged jet.
        readonly bool _cutOff;
        readonly double _mergeStart, _mergeEnd, _farDecay, _farSpread, _farOrigin;
        readonly double _wallRatio, _wallExponent, _wallDecay, _profileA, _profileB, _profileSquare;
        double _motorDistance, _farSpeed; // l, m; U_H, m/s
        Double3 _farCentre, _farAxis;     // the hubs' centre and the jets' mean direction (unit, downstream)
        double _near = 1;                 // the near wake's share at the ground: 1 − w(s_g)
        bool _farLands;
        Double3 _farLanding;
        double _farLandingShare, _farGroundSpeed, _farStart, _farOriginDistance; // w(s_g), U_g, r_i, H

        // (√2 − 1): the merged jet's profile constant (Bauersfeld et al. 2024's eq. 3, published).
        const double ProfileA = 0.41421356237309503;

        // The fixed-point passes for r_i (numerical: the (r/H)^−0.1 factor converges to rounding within 4).
        const int StartPasses = 4;

        /// <param name="radius">R, the props' radius, m.</param>
        /// <param name="recirculation">The fountain and the ground vortex (1.6b); false for design 4's field alone, the
        /// known-wrong (FlightSetup.TetherDesign4Air).</param>
        /// <param name="body">The drone's contact shapes over the fountain (model 61); null lets the fountain through the body,
        /// model 59's field (FlightSetup.TetherFountainThroughBody, the known-wrong).</param>
        /// <param name="cutOff">Design 19's known-wrong (FlightSetup.TetherWakeReach4R, model 94's field): each column without
        /// end, a wall jet only within wake_reach_over_radius, no merged jet.</param>
        public RotorAir(TetherTable table, double radius, bool recirculation = true, ContactModel body = null, bool cutOff = false)
        {
            _radius = radius;
            _density = table.Number("fiber.air_density_kg_m3");
            _wakeSpeed = table.Number("rotor_air.far_wake_speed_ratio");
            _wakeRadius = table.Number("rotor_air.far_wake_radius_ratio") * radius;
            _reach = table.Number("rotor_air.wake_reach_over_radius") * radius;
            _cutOff = cutOff;
            double[] merge = table.Row("rotor_air.wake_merge_over_motor_distance").Values;
            _mergeStart = merge[0];
            _mergeEnd = merge[1];
            _farDecay = table.Number("rotor_air.far_jet_decay");
            _farSpread = table.Number("rotor_air.far_jet_spreading_rate");
            _farOrigin = table.Number("rotor_air.far_jet_virtual_origin_over_motor_distance");
            _wallRatio = table.Number("rotor_air.wall_jet_half_height_ratio");
            _wallExponent = table.Number("rotor_air.wall_jet_half_height_exponent");
            _wallDecay = table.Number("rotor_air.wall_jet_decay_exponent");
            double[] profile = table.Row("rotor_air.wall_jet_profile").Values;
            _profileA = profile[0];
            _profileB = profile[1];
            _profileSquare = table.Number("rotor_air.wall_jet_profile_square_integral");
            _jetThickness = table.Number("rotor_air.wall_jet_thickness_over_radius") * radius;
            _recirculation = recirculation;
            _fountainMomentum = table.Number("rotor_air.fountain_momentum_ratio");
            _spread = table.Number("rotor_air.plane_jet_spreading_rate");
            _hoverLike = table.Number("rotor_air.hover_like_advance_ratio");
            double[] end = table.Row("rotor_air.ground_vortex_advance_ratio").Values;
            _vortexEnd = end[0];
            _vortexGone = end[1];
            _vortexOffset = table.Number("rotor_air.ground_vortex_offset_over_radius") * radius;
            _vortexPenetration = table.Number("rotor_air.ground_vortex_penetration_ratio");
            _vortexHeight = table.Number("rotor_air.ground_vortex_height_over_jet") * _jetThickness;
            _vortexCore = table.Number("rotor_air.ground_vortex_core_over_height") * _vortexHeight;
            _vortexCirculation = table.Number("rotor_air.ground_vortex_circulation");
            _vortexSpan = table.Number("rotor_air.ground_vortex_half_span_over_radius") * radius;
            _profileMass = DetMath.Sqrt(Pi / Ln2);
            _profileMomentum = DetMath.Sqrt(Pi / (2 * Ln2));
            _jetSpeed = radius * radius / (2 * _jetThickness);
            _originScale = _profileMomentum / (_profileMass * _profileMass * _spread);
            _gaussScale = Ln2 / (_spread * _spread);
            _centreScale = 1 / (_profileMomentum * _spread);
            _inverseJet = 1 / _jetThickness;
            _body = recirculation ? body : null;
            _impingement = table.Number("rotor_air.fountain_impingement_depth_ratio");
            _leeLength = table.Number("rotor_air.body_lee_length_over_diameter");
            _sqrtLn2 = DetMath.Sqrt(Ln2);
            _halfMass = 0.5 * _profileMass;
            int shapes = _body != null ? _body.ShapeCount : 0;
            _bodyA = new Double3[shapes];
            _bodyB = new Double3[shapes];
            _bodyRadius = new double[shapes];
            _boxX0 = new double[shapes];
            _boxX1 = new double[shapes];
            _boxZ0 = new double[shapes];
            _boxZ1 = new double[shapes];
        }

        /// <summary>Whether the drone's body blocks the fountain (model 61; false: model 59's field).</summary>
        public bool BodyBlocks => _body != null;

        /// <summary>
        /// The step's body over the fountain, before Begin: every contact shape but the discs and the shapes off the set (a
        /// turning rotor's blades, a part gone), at the drone's pose, as the fiber's contacts take them.
        /// </summary>
        public void PlaceBody(Double3 position, Quat attitude)
        {
            _placed = 0;
            _allX0 = _allZ0 = double.PositiveInfinity;
            _allX1 = _allZ1 = double.NegativeInfinity;
            for (int i = 0; i < _bodyA.Length; i++)
            {
                ContactShape c = _body.Shape(i);
                if (c.Role == ContactRole.Disc || _body.ShapeOff(i)) continue;
                int k = _placed++;
                Double3 a = position + attitude.Rotate(c.A), b = position + attitude.Rotate(c.B);
                double r = c.Radius;
                _bodyA[k] = a;
                _bodyB[k] = b;
                _bodyRadius[k] = r;
                _boxX0[k] = (a.X < b.X ? a.X : b.X) - r;
                _boxX1[k] = (a.X > b.X ? a.X : b.X) + r;
                _boxZ0[k] = (a.Z < b.Z ? a.Z : b.Z) - r;
                _boxZ1[k] = (a.Z > b.Z ? a.Z : b.Z) + r;
                if (_boxX0[k] < _allX0) _allX0 = _boxX0[k];
                if (_boxX1[k] > _allX1) _allX1 = _boxX1[k];
                if (_boxZ0[k] < _allZ0) _allZ0 = _boxZ0[k];
                if (_boxZ1[k] > _allZ1) _allZ1 = _boxZ1[k];
            }
        }

        public double Radius => _radius;

        /// <summary>The wall jet's thickness h_j, m.</summary>
        public double JetThickness => _jetThickness;

        /// <summary>Whether the fountain and the ground vortex act (false: design 4's field, the known-wrong).</summary>
        public bool Recirculation => _recirculation;

        /// <summary>Each rotor's induced velocity this step, m/s.</summary>
        public double Induced(int rotor) => _induced[rotor];

        /// <summary>Each rotor's hover induced velocity v_h from its thrust this step, m/s.</summary>
        public double Hover(int rotor) => _hover[rotor];

        /// <summary>Each rotor's normalised advance ratio μ* = U/v_h this step (U the stream along the ground at its hub).</summary>
        public double Advance(int rotor) => _advance[rotor];

        /// <summary>Each rotor's wake direction this step, world (unit, downstream).</summary>
        public Double3 Wake(int rotor) => _wake[rotor];

        /// <summary>Where each rotor's wake lands on the ground this step, and whether it does within the reach.</summary>
        public Double3 Landing(int rotor) => _landing[rotor];
        public bool Lands(int rotor) => _jet[rotor];

        /// <summary>Each rotor's fountain share this step (1 in the hover-like regime, 0 from the ground vortex's end).</summary>
        public double FountainShare(int rotor) => _fade[rotor];

        /// <summary>Each rotor's ground vortex this step: its circulation, m²/s (0: none), its centre and its ends, world m
        /// (its axis from A to B, the right-hand sense of the circulation: the flow upstream along the ground under it).</summary>
        public double VortexCirculation(int rotor) => _circulation[rotor];
        public Double3 VortexCentre(int rotor) => _vortexCentre[rotor];
        public Double3 VortexA(int rotor) => _vortexA[rotor];
        public Double3 VortexB(int rotor) => _vortexB[rotor];

        /// <summary>The ground vortex's core radius, m.</summary>
        public double VortexCore => _vortexCore;

        /// <summary>
        /// The step's rotors: each hub (world m), its disc's normal (world, unit, the intake side), its thrust (N), the air's
        /// velocity at it relative to the hub (world m/s: the wind less the hub's velocity) and the ground's height under it.
        /// </summary>
        public void Begin(Double3[] hubs, Double3 normal, double[] thrust, Double3[] relativeAir, double[] groundUnderHub)
        {
            double area = Pi * _radius * _radius;
            for (int i = 0; i < Rotors; i++)
            {
                _hub[i] = hubs[i];
                _normal[i] = normal;
                double t = thrust[i] > 0 ? thrust[i] : 0;
                double vh = DetMath.Sqrt(t / (2 * _density * area));
                Double3 a = relativeAir[i];
                double down = -Double3.Dot(normal, a); // the free stream down through the disc, as a climb's
                Double3 across = a + normal * down;
                double cross2 = Double3.Dot(across, across);
                double vi = vh;
                if (vh > 0 && down >= 0)
                {
                    // Newton on f(v) = v·√(u_p² + (u_a + v)²) − v_h², increasing for v ≥ 0.
                    for (int k = 0; k < Iterations; k++)
                    {
                        double s = down + vi, root = DetMath.Sqrt(cross2 + s * s);
                        double f = vi * root - vh * vh, df = root + vi * s / root;
                        double next = vi - f / df;
                        vi = next > 0 ? next : 0.5 * vi;
                    }
                }
                _induced[i] = vi;
                _hover[i] = vh;
                // The wake leaves along the flow through the disc: the free stream with v_i down through it.
                Double3 flow = a - normal * vi;
                double len = flow.Length();
                _wake[i] = len > 0 ? flow * (1 / len) : normal * -1.0;
                _ground[i] = groundUnderHub[i];
                _jet[i] = false;
                if (vi > 0 && _wake[i].Y < 0)
                {
                    double along = (hubs[i].Y - groundUnderHub[i]) / -_wake[i].Y;
                    if (along >= 0 && (_cutOff ? along <= _reach : along < double.PositiveInfinity)) // no ground under it (−∞): no landing
                    {
                        _jet[i] = true;
                        _landing[i] = hubs[i] + _wake[i] * along;
                    }
                }
                // The stream along the ground at the hub (the air across the vertical) and μ* on it.
                double ux = a.X, uz = a.Z, u = DetMath.Sqrt(ux * ux + uz * uz);
                _advance[i] = vh > 0 ? u / vh : 0;
                _stream[i] = u > 0 ? new Double3(ux / u, 0, uz / u) : default;
                double mu = _advance[i];
                _fade[i] = mu <= _hoverLike ? 1 : mu >= _vortexEnd ? 0 : (_vortexEnd - mu) / (_vortexEnd - _hoverLike);
                _circulation[i] = 0;
                if (!_recirculation || !_jet[i] || !(u > 0) || !(vh > 0)) continue;
                double grow = mu < _hoverLike ? mu / _hoverLike : 1;
                if (mu > _vortexEnd) grow = mu < _vortexGone ? (_vortexGone - mu) / (_vortexGone - _vortexEnd) : 0;
                if (!(grow > 0)) continue;
                double penetration = _radius * _radius * vi / (2 * _jetThickness * u);
                double ahead = _vortexOffset + _vortexPenetration * penetration;
                Double3 up = new Double3(0, 1, 0), st = _stream[i];
                Double3 centre = _landing[i] - st * ahead + up * _vortexHeight;
                var axis = new Double3(st.Z, 0, -st.X); // up × the stream: the flow upstream under it, up at its upstream side
                _vortexCentre[i] = centre;
                _vortexA[i] = centre - axis * _vortexSpan;
                _vortexB[i] = centre + axis * _vortexSpan;
                _circulation[i] = _vortexCirculation * vh * _radius * grow;
            }
            if (!_cutOff)
            {
                // Design 19: the merged far jet and its landing; the near wake's landings share the ground by 1 − w(s_g).
                Far(groundUnderHub);
                for (int i = 0; i < Rotors; i++)
                {
                    if (!(_near > 0)) _jet[i] = false;
                    _circulation[i] = _circulation[i] * _near;
                }
            }
            // A rotor whose upstream wall jet meets another's landing wake forms no vortex of its own: shadowed by a landing
            // ahead of it, wholly within one span across, not at all beyond two, ramped over a span along (assumed).
            if (!_recirculation) return;
            for (int j = 0; j < Rotors; j++)
            {
                if (_circulation[j] == 0) continue;
                Double3 st = _stream[j];
                double keep = 1;
                for (int i = 0; i < Rotors; i++)
                {
                    if (i == j || !_jet[i]) continue;
                    double dx = _landing[i].X - _landing[j].X, dz = _landing[i].Z - _landing[j].Z;
                    double along = -(dx * st.X + dz * st.Z), lateral = DetMath.Abs(dx * st.Z - dz * st.X);
                    double a = along / _vortexSpan, b = 2 - lateral / _vortexSpan;
                    a = a < 0 ? 0 : a > 1 ? 1 : a;
                    b = b < 0 ? 0 : b > 1 ? 1 : b;
                    keep = keep * (1 - a * b);
                }
                _circulation[j] = _circulation[j] * keep;
            }
        }

        // Design 19's merged jet this step (after the rotors' wakes): l from the hubs, the jets' mean direction from the hubs'
        // centre, U_H the rms induced velocity; where its axis meets the ground (the hubs' mean ground), U_g, r_i and H.
        void Far(double[] groundUnderHub)
        {
            Double3 centre = default, axis = default;
            double square = 0, ground = 0;
            int grounds = 0;
            for (int i = 0; i < Rotors; i++)
            {
                centre = centre + _hub[i];
                axis = axis + _wake[i];
                square = square + _induced[i] * _induced[i];
                double g = groundUnderHub[i];
                if (g > double.NegativeInfinity && g < double.PositiveInfinity)
                {
                    ground = ground + g; // the hubs' mean ground over those with ground under them (a hole's −∞ left out)
                    grounds++;
                }
            }
            centre = centre * 0.25;
            ground = grounds > 0 ? ground / grounds : double.NegativeInfinity;
            double spread = 0;
            for (int i = 0; i < Rotors; i++) spread = spread + (_hub[i] - centre).Length();
            _motorDistance = 0.5 * spread; // 2 × the mean distance from the centre
            _farCentre = centre;
            double len = axis.Length();
            _farAxis = len > 0 ? axis * (1 / len) : new Double3(0, -1, 0);
            _farSpeed = DetMath.Sqrt(0.25 * square);
            _near = 1;
            _farLands = false;
            _farLandingShare = 0;
            if (!(_motorDistance > 0) || !(_farAxis.Y < 0) || !(_farSpeed > 0)) return;
            double along = (centre.Y - ground) / -_farAxis.Y;
            if (!(along >= 0) || !(along < double.PositiveInfinity)) return; // no ground under a hub (−∞): no landing
            double w = Merged(along);
            _near = 1 - w;
            if (!(w > 0)) return;
            double l = _motorDistance, x = along / l - _farOrigin, half = _farSpread * x * l;
            _farLands = true;
            _farLandingShare = w;
            _farLanding = centre + _farAxis * along;
            _farGroundSpeed = _farSpeed * _farDecay / x;
            _farOriginDistance = along - _farOrigin * l;
            // r_i from the momentum flux: r_i = r½/√(6(√2 − 1)·c_i·∫f²), c_i = c·(r_i/H)^e.
            double c = _wallRatio, start = half;
            for (int k = 0; k < StartPasses; k++)
            {
                start = half / DetMath.Sqrt(6 * ProfileA * c * _profileSquare);
                c = _wallRatio * Power(start / _farOriginDistance, _wallExponent);
            }
            _farStart = start;
        }

        // The merged jet's share at s along its axis: 0 to the merge's start, 1 from its end, linear between (design 19).
        double Merged(double s)
        {
            double t = (s / _motorDistance - _mergeStart) / (_mergeEnd - _mergeStart);
            return t <= 0 ? 0 : t >= 1 ? 1 : t;
        }

        static double Power(double x, double e) => DetMath.Exp(e * DetMath.Log(x));

        /// <summary>Design 19's step: the motor distance l (m), the merged jet's U_H (m/s), the near wake's share at the ground
        /// (1 − w(s_g)), and the merged jet's landing (whether, where, its share w(s_g), U_g in m/s, r_i in m).</summary>
        public double MotorDistance => _motorDistance;
        public double FarSpeed => _farSpeed;
        public double NearShare => _near;
        public bool FarLands => _farLands;
        public Double3 FarLanding => _farLanding;
        public double FarLandingShare => _farLandingShare;
        public double FarGroundSpeed => _farGroundSpeed;
        public double FarStart => _farStart;

        /// <summary>
        /// The rotors' added air at a point, world m/s, with the ground's height under it (−∞ for none); jet is the wall
        /// jets' share of it (horizontal, the flow along the ground that lifts a line lying in it).
        /// </summary>
        public Double3 At(Double3 p, double groundUnderPoint, out Double3 jet)
        {
            Double3 air = default;
            jet = default;
            bool column = false;
            int first = -1, second = -1; // the two strongest wall jets here, by v_i²/r² (their speeds' order)
            double firstKey = 0, secondKey = 0, firstZ = 0, secondZ = 0;
            // Design 19: the point's distance along the merged jet and the near columns' share there (1: model 94's columns).
            double farAlong = 0, columnShare = 1;
            if (!_cutOff)
            {
                farAlong = Double3.Dot(p - _farCentre, _farAxis);
                columnShare = 1 - Merged(farAlong);
            }
            for (int i = 0; i < Rotors; i++)
            {
                double vi = _induced[i];
                if (!(vi > 0)) continue;
                Double3 n = _normal[i];
                Double3 q = p - _hub[i];
                double z = Double3.Dot(n, q);
                if (_recirculation && _jet[i])
                {
                    double fx = p.X - _landing[i].X, fz = p.Z - _landing[i].Z, f2 = fx * fx + fz * fz;
                    if (f2 > _wakeRadius * _wakeRadius)
                    {
                        double key = vi * vi / f2;
                        if (key > firstKey)
                        {
                            second = first; secondKey = firstKey; secondZ = firstZ;
                            first = i; firstKey = key; firstZ = z;
                        }
                        else if (key > secondKey)
                        {
                            second = i; secondKey = key; secondZ = z;
                        }
                    }
                }
                if (z > 0)
                {
                    // The intake side: the cylinder's axial inflow inside the radius, the sink outside it.
                    Double3 lateral = q - n * z;
                    if (Double3.Dot(lateral, lateral) < _radius * _radius)
                        air = air - n * (vi * (1 - z / DetMath.Sqrt(z * z + _radius * _radius)));
                    else
                    {
                        double d2 = Double3.Dot(q, q), d = DetMath.Sqrt(d2);
                        air = air - q * (vi * _radius * _radius / (2 * d2 * d));
                    }
                    continue;
                }
                // The wake side: inside the column, down through the disc at the far wake's speed, to the ground.
                Double3 w = _wake[i];
                double h = Double3.Dot(q, w);
                if (h > 0)
                {
                    Double3 off = q - w * h;
                    bool above = !_jet[i] || p.Y > _ground[i];
                    if (above && Double3.Dot(off, off) < _wakeRadius * _wakeRadius && columnShare > 0)
                    {
                        air = air - n * (_wakeSpeed * vi * columnShare);
                        column = true;
                        continue;
                    }
                }
                // The wall jet: outward from where the column lands, inside h_j of the ground.
                if (!_jet[i] || !(p.Y - groundUnderPoint <= _jetThickness)) continue;
                double dx = p.X - _landing[i].X, dz = p.Z - _landing[i].Z, r2 = dx * dx + dz * dz;
                if (!(r2 > _wakeRadius * _wakeRadius)) continue;
                double scale = vi * _radius * _radius / (2 * _jetThickness * r2) * _near; // Q/(2π·r·h_j) along (dx, dz)/r
                var u = new Double3(dx * scale, 0, dz * scale);
                air = air + u;
                jet = jet + u;
            }
            if (!_cutOff) FarAir(p, groundUnderPoint, farAlong, ref air, ref jet);
            if (!_recirculation) return air;
            if (!column && second >= 0 && firstZ <= 0 && secondZ <= 0)
            {
                double rise = Fountain(p, groundUnderPoint, first, second, out FountainSheet sheet);
                if (rise > 0 && _body != null)
                {
                    // The column's height to the colliding discs' nearer plane (the normal is every rotor's).
                    double top = _normal[first].Y > 0 ? -(firstZ > secondZ ? firstZ : secondZ) / _normal[first].Y : double.PositiveInfinity;
                    rise = Blocked(p, top, rise, sheet, ref air);
                }
                air = air + new Double3(0, rise, 0);
            }
            for (int i = 0; i < Rotors; i++)
            {
                double g = _circulation[i];
                if (g == 0) continue;
                // The vortex and its image under the ground (the ground's height at the landing): no flow through the ground.
                double ground = _landing[i].Y;
                Double3 a = _vortexA[i], b = _vortexB[i];
                air = air + Segment(p, a, b, g);
                air = air + Segment(p, new Double3(a.X, 2 * ground - a.Y, a.Z), new Double3(b.X, 2 * ground - b.Y, b.Z), -g);
            }
            return air;
        }

        // Design 19's merged jet at p (s along its axis), weighted w(s), its descent falling linearly to nothing over r_i/2 above
        // the ground where it lands; and its radial wall jet, weighted w(s_g): u_m = U_g·r/r_i inside r_i (the stagnation
        // flow), U_g·(r_i/r)^1.1 beyond, across the height Verhoff's profile on δ = c·R·(R/H)^e, R = max(r, r_i).
        void FarAir(Double3 p, double groundUnderPoint, double s, ref Double3 air, ref Double3 jet)
        {
            double height = p.Y - groundUnderPoint;
            double w = Merged(s);
            if (w > 0)
            {
                double l = _motorDistance, x = s / l - _farOrigin, half = _farSpread * x * l;
                Double3 off = p - _farCentre - _farAxis * s;
                double d = 1 + ProfileA * Double3.Dot(off, off) / (half * half);
                double u = _farSpeed * _farDecay / x / (d * d) * w;
                if (_farLands)
                {
                    double depth = 0.5 * _farStart;
                    if (height < depth) u = height > 0 ? u * (height / depth) : 0;
                }
                air = air + _farAxis * u;
            }
            if (!_farLands || !(height > 0)) return;
            double dx = p.X - _farLanding.X, dz = p.Z - _farLanding.Z, r = DetMath.Sqrt(dx * dx + dz * dz);
            if (!(r > 0)) return;
            double ri = _farStart, big = r > ri ? r : ri;
            double um = r < ri ? _farGroundSpeed * (r / ri) : _farGroundSpeed * Power(ri / r, _wallDecay);
            double delta = _wallRatio * big * Power(big / _farOriginDistance, _wallExponent);
            double eta = height / delta;
            double f = _profileB * DetMath.Exp(DetMath.Log(eta) / 7) * (1 - Erf(_profileA * eta));
            double scale = um * f * _farLandingShare / r;
            var v = new Double3(dx * scale, 0, dz * scale);
            air = air + v;
            jet = jet + v;
        }

        // The fountain's upward speed at p from the wall jets of rotors k and m (k's the stronger here): their meeting line
        // the locus u_k = u_m, p's distance from it (u_k² − u_m²)/(2·|u_m·∇u_k − u_k·∇u_m|), exact for equal jets (the
        // midline) and well conditioned for unequal ones; the jets' parts normal to it turn up.
        double Fountain(Double3 p, double groundUnderPoint, int k, int m, out FountainSheet sheet)
        {
            sheet = default;
            double height = p.Y - groundUnderPoint;
            if (!(height >= 0)) return 0;
            double kx = p.X - _landing[k].X, kz = p.Z - _landing[k].Z, ik = 1 / (kx * kx + kz * kz), rk = DetMath.Sqrt(kx * kx + kz * kz);
            double mx = p.X - _landing[m].X, mz = p.Z - _landing[m].Z, im = 1 / (mx * mx + mz * mz), rm = DetMath.Sqrt(mx * mx + mz * mz);
            double uk = _induced[k] * _jetSpeed * rk * ik, um = _induced[m] * _jetSpeed * rm * im; // Q/(2π·r·h_j)
            // ∇u = −u·(p − L)/r²; u_m·∇u_k − u_k·∇u_m points across the line toward k's landing.
            double gx = uk * um * (mx * im - kx * ik), gz = uk * um * (mz * im - kz * ik);
            double gn = DetMath.Sqrt(gx * gx + gz * gz);
            if (!(gn > 0)) return 0;
            double ig = 1 / gn, nx = gx * ig, nz = gz * ig;
            double delta = (uk * uk - um * um) * 0.5 * ig;
            double kn = -uk * (kx * nx + kz * nz) * rk * ik, mn = um * (mx * nx + mz * nz) * rm * im;
            kn = kn > 0 ? kn : 0;
            mn = mn > 0 ? mn : 0;
            double flux = _jetThickness * (kn + mn), momentum = _fountainMomentum * _jetThickness * (kn * kn + mn * mn);
            if (!(momentum > 0)) return 0;
            // The plane jet: w_c²·b·∫f² = M, b = S·(y + y₀), y₀ where its flux w_c·b·∫f is the jets'.
            double y = height + _originScale * flux * flux / momentum, iy = 1 / y;
            double exponent = -_gaussScale * delta * delta * iy * iy; // −ln2·δ²/b²
            if (!(exponent > CutOff)) return 0;
            double centre = DetMath.Sqrt(momentum * iy * _centreScale);
            double rise = height < _jetThickness ? height * _inverseJet : 1;
            double share = (_fade[k] < _fade[m] ? _fade[k] : _fade[m]) * _near; // design 19: the near wake's share
            sheet = new FountainSheet { Nx = nx, Nz = nz, Delta = delta, Momentum = momentum, Y = y, Height = height, Share = share };
            return centre * DetMath.Exp(exponent) * rise * share;
        }

        /// <summary>A point's place in the fountain's plane jet: the meeting line's normal (toward the stronger jet's landing),
        /// the point's distance from it along that normal, the jet's momentum, the point's distance from the virtual origin and
        /// its height over the ground, and the regimes' share.</summary>
        struct FountainSheet
        {
            public double Nx, Nz, Delta, Momentum, Y, Height, Share;
        }

        // The fountain's rise at p, w (free, > 0), against the drone's body on p's vertical (model 61, the summary's rules):
        // nothing inside a shape or in its lee; in the impingement layer under the lowest underside below the discs' plane
        // (top, m above p) the stagnation's linear fall and the deflection across the sheet, added to air.
        double Blocked(Double3 p, double top, double w, in FountainSheet f, ref Double3 air)
        {
            if (p.X < _allX0 || p.X > _allX1 || p.Z < _allZ0 || p.Z > _allZ1) return w; // no part over or under p
            double under = double.PositiveInfinity, bodyDepth = 0;
            for (int i = 0; i < _placed; i++)
            {
                if (p.X < _boxX0[i] || p.X > _boxX1[i] || p.Z < _boxZ0[i] || p.Z > _boxZ1[i]) continue;
                double a = _bodyRadius[i];
                if (!Column(p, _bodyA[i], _bodyB[i], a, out double lo, out double hi, out bool cap)) continue;
                if (lo <= 0 && hi >= 0) return 0; // inside the body
                if (hi < 0)
                {
                    if (-hi < _leeLength * 2 * a) return 0; // in its lee
                    continue;
                }
                if (lo < under)
                {
                    under = lo;
                    bodyDepth = cap ? a / 3 : a / 2;
                }
            }
            if (!(under < top)) return w;
            double jetDepth = _impingement * (f.Y + under);
            double depth = jetDepth < bodyDepth ? jetDepth : bodyDepth;
            if (!(under < depth)) return w;
            // The free sheet at the layer's top, and the stagnation flow under it.
            double yTop = f.Y + (under - depth), iy = 1 / yTop, heightTop = f.Height + (under - depth);
            double centre = DetMath.Sqrt(f.Momentum * iy * _centreScale);
            double wc = centre * (heightTop < _jetThickness ? heightTop * _inverseJet : 1) * f.Share;
            double rise = wc * DetMath.Exp(-_gaussScale * f.Delta * f.Delta * iy * iy) * (under / depth);
            // The deflection: (W_c/δ)·∫₀^s e^(−ln2·t²/b²)dt = (W_c/δ)·b·(√π/(2√ln2))·erf(√ln2·s/b), at most W_c.
            double b = _spread * yTop, s = DetMath.Abs(f.Delta);
            double u = wc * b * _halfMass * Erf(_sqrtLn2 * s / b) / depth;
            if (u > wc) u = wc;
            if (f.Delta < 0) u = -u;
            air = air + new Double3(f.Nx * u, 0, f.Nz * u);
            return rise;
        }

        // The vertical through p meets the capsule (or sphere, a = b) of radius r over heights [lo, hi] relative to p.Y; cap:
        // its lowest point is on an end sphere (a capsule's cap or a sphere), not on its cylinder. False when it misses.
        static bool Column(Double3 p, Double3 a, Double3 b, double r, out double lo, out double hi, out bool cap)
        {
            lo = double.PositiveInfinity;
            hi = double.NegativeInfinity;
            cap = false;
            Sphere(p, a, r, ref lo, ref hi, ref cap);
            Sphere(p, b, r, ref lo, ref hi, ref cap);
            Double3 d = b - a, m = p - a;
            double dd = Double3.Dot(d, d);
            if (dd > 0)
            {
                double md = Double3.Dot(m, d), ud = d.Y;
                double qa = 1 - ud * ud / dd;
                // The parts across the axis of p - a and of the vertical.
                Double3 mp = m - d * (md / dd), up = new Double3(0, 1, 0) - d * (ud / dd);
                double qb = Double3.Dot(mp, up), qc = Double3.Dot(mp, mp) - r * r;
                double t0, t1;
                if (qa > Vertical)
                {
                    double disc = qb * qb - qa * qc;
                    if (disc >= 0)
                    {
                        double root = DetMath.Sqrt(disc);
                        t0 = (-qb - root) / qa;
                        t1 = (-qb + root) / qa;
                    }
                    else { t0 = 1; t1 = 0; }
                }
                else if (qc <= 0) { t0 = double.NegativeInfinity; t1 = double.PositiveInfinity; }
                else { t0 = 1; t1 = 0; }
                // Within the axis's span: 0 <= (m·d + t·d_y)/(d·d) <= 1.
                if (ud != 0)
                {
                    double s0 = -md / ud, s1 = (dd - md) / ud;
                    if (s0 > s1) { double x = s0; s0 = s1; s1 = x; }
                    if (s0 > t0) t0 = s0;
                    if (s1 < t1) t1 = s1;
                }
                else if (md < 0 || md > dd) t1 = t0 - 1;
                if (t0 <= t1)
                {
                    if (t0 < lo) { lo = t0; cap = false; }
                    if (t1 > hi) hi = t1;
                }
            }
            return lo <= hi;
        }

        static void Sphere(Double3 p, Double3 c, double r, ref double lo, ref double hi, ref bool cap)
        {
            double dx = p.X - c.X, dz = p.Z - c.Z, h2 = dx * dx + dz * dz;
            if (!(h2 <= r * r)) return;
            double half = DetMath.Sqrt(r * r - h2), y = c.Y - p.Y;
            if (y - half < lo) { lo = y - half; cap = true; }
            if (y + half > hi) hi = y + half;
        }

        // erf(x) for x >= 0 (Abramowitz and Stegun 7.1.26).
        static double Erf(double x)
        {
            double t = 1 / (1 + ErfP * x);
            double poly = t * (ErfA1 + t * (ErfA2 + t * (ErfA3 + t * (ErfA4 + t * ErfA5))));
            return 1 - poly * DetMath.Exp(-x * x);
        }

        // A straight vortex from a to b of circulation g (right-handed about a → b) at p, with a Scully core: Biot–Savart's
        // segment law, |r₁ × r₂|² + r_c²·|r₀|² below (published, Leishman 2006 §10).
        Double3 Segment(Double3 p, Double3 a, Double3 b, double g)
        {
            Double3 r0 = b - a, r1 = p - a, r2 = p - b;
            double n1 = r1.Length(), n2 = r2.Length();
            if (!(n1 > 0) || !(n2 > 0)) return default;
            Double3 cross = Double3.Cross(r1, r2);
            double t = Double3.Dot(r1, r0) / n1 - Double3.Dot(r2, r0) / n2;
            double den = Double3.Dot(cross, cross) + _vortexCore * _vortexCore * Double3.Dot(r0, r0);
            return cross * (g * t / (4 * Pi * den));
        }
    }
}
