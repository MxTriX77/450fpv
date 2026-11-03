using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>What failed in a leg, an event's Index (p7g-legs design 3).</summary>
    public enum LegMode : byte
    {
        /// <summary>The leg bent at its clamp by a force across it (at the foot, or on its tube).</summary>
        Bending = 1,

        /// <summary>The leg bowed past its buckling load, and the bow's moment bent it at the clamp.</summary>
        Buckling = 2,

        /// <summary>The leg turned about its axis in the clamp's jaws.</summary>
        ClampTwist = 3,

        /// <summary>The leg slid out of the clamp's jaws under a pull.</summary>
        ClampSlip = 4,

        /// <summary>The skid slid in its T-joint's bore.</summary>
        TJointSlip = 5,

        /// <summary>The T-joint's socket broke across its print layers.</summary>
        TJointFracture = 6,
    }

    /// <summary>A leg's state, bit by bit (the step record's leg_state).</summary>
    [Flags]
    public enum LegFlags : byte
    {
        None = 0,

        /// <summary>It keeps a set angle at its clamp (bent past its yield), or a twist in its clamp.</summary>
        Set = 1,

        /// <summary>Its clamp's edge cracked (at the edge's first yield).</summary>
        Cracked = 2,

        /// <summary>The tube parted at the clamp's edge: the strain there passed the material's elongation at break.</summary>
        Broken = 4,

        /// <summary>The tube slid out of its clamp: the leg left the drone.</summary>
        Detached = 8,

        /// <summary>It is bowed past its buckling load now (elastic: it straightens as the load falls).</summary>
        Bowed = 16,

        /// <summary>It turned in its clamp's jaws (its twist is in the set).</summary>
        Twisted = 32,

        /// <summary>Its T-joint let its skid slip or broke.</summary>
        SkidLoose = 64,
    }

    /// <summary>
    /// The legs' failure data (p7g-legs 1.2, design 3 and 6), from drone.json's legs block (DroneSpec.BuildContacts): each
    /// leg's parts and its clamp's edge and foot on its axis, the tube's section and modulus, the cracked factor, the clamp's
    /// and the T-joint's fasteners, and the strengths a flight draws within their published bounds. The log header records
    /// every field (LogHeader.LegFieldsLogged).
    /// </summary>
    public sealed class LegDamageSpec
    {
        /// <summary>Each leg's part and its T-joint's part (DroneSpec.Parts), in Corners order.</summary>
        public int[] Parts = new int[4], TJointParts = new int[4];

        /// <summary>Each leg's clamp's bottom edge and its foot (the skid's underside) on its axis, body m.</summary>
        public Double3[] Clamp = new Double3[4], Foot = new Double3[4];

        /// <summary>The tube's lower end in the T-joint's socket above the foot, m.</summary>
        public double SocketLever;

        /// <summary>The tube: its free length (the clamp's bottom to the T-joint's top), its section, its modulus (nominal; the
        /// stiffness's spread is p7a-ground 1.17's leg.stiffness, never drawn here), m and Pa; a cracked tube's stiffness over
        /// an intact one's.</summary>
        public double FreeLength, OuterDiameter, InnerDiameter, Modulus, CrackedFactor;

        /// <summary>The clamp's bolt and the tube's length in its jaws; the T-joint's pinch screw and its socket's outer
        /// diameter, m.</summary>
        public double BoltDiameter, GripLength, ScrewDiameter, SocketDiameter;

        /// <summary>The strengths each leg draws (its stream leg.strength.&lt;leg&gt;): the tube's yield stress (Pa) and
        /// elongation at break, the clamp's edge factor, the wing nut's hand torque (N·m), the nut factor, the jaws' friction,
        /// the T-joint's screw torque (N·m) and the print's interlayer strength (Pa).</summary>
        public LegBound YieldStress, Elongation, EdgeFactor, ClampTorque, NutFactor, Friction, TJointTorque, Interlayer;

        const double Pi = 3.141592653589793;

        /// <summary>The fixed–pinned column's first buckling load factor, β² (β the root of tan β = β; Timoshenko and Gere).</summary>
        public const double FixedPinned = 20.190728556426624;

        public double SecondMoment
        {
            get
            {
                double d2 = OuterDiameter * OuterDiameter, i2 = InnerDiameter * InnerDiameter;
                return Pi * (d2 * d2 - i2 * i2) / 64;
            }
        }

        public double Area => Pi * (OuterDiameter * OuterDiameter - InnerDiameter * InnerDiameter) / 4;

        /// <summary>The plastic modulus over the elastic one, Z_p·c/I: the section's shape factor.</summary>
        public double ShapeFactor => (OuterDiameter * OuterDiameter * OuterDiameter - InnerDiameter * InnerDiameter * InnerDiameter) / 6
            * (OuterDiameter / 2) / SecondMoment;

        /// <summary>The free length's buckling load, fixed at the clamp and pinned at the foot, N.</summary>
        public double BucklingLoad => FixedPinned * Modulus * SecondMoment / (FreeLength * FreeLength);

        public LegDamageSpec Clone()
        {
            var c = (LegDamageSpec)MemberwiseClone();
            c.Parts = (int[])Parts.Clone();
            c.TJointParts = (int[])TJointParts.Clone();
            c.Clamp = (Double3[])Clamp.Clone();
            c.Foot = (Double3[])Foot.Clone();
            return c;
        }
    }

    /// <summary>One leg's loads in a step, body axes (the leg along +y), from the contact model's forces.</summary>
    public struct LegLoads
    {
        /// <summary>The leg spring's force along the leg (compression +), N, and across it at the foot (x and z), N; whether
        /// any of its points touch, so its give can relieve them.</summary>
        public double LawAxial;
        public Double3 LawLateral;
        public bool LawTouching;

        /// <summary>The leg tube shapes' force along the leg (compression +), N, and their moment at the clamp's edge as the
        /// lateral vector it bends the foot toward (height × force across), N·m.</summary>
        public double ShapeAxial;
        public Double3 ShapeMoment;

        /// <summary>The moment about the leg's axis at the foot (+y), N·m, and the leg spring's stiffness to it, Σ w·k_l·r⊥²,
        /// N·m/rad.</summary>
        public double Twist, TwistStiffness;

        /// <summary>The leg spring's force along the skid (body z) through the T-joint, N.</summary>
        public double SkidAxial;

        /// <summary>The leg spring's stiffness along the leg and across it at the foot, N/m.</summary>
        public double AxialStiffness, LateralStiffness;
    }

    /// <summary>What a leg's give takes off its spring's force in the step (body axes): along the leg (compression +), across
    /// it at the foot, and the twist's turn (rad, about +y).</summary>
    public struct LegRelief
    {
        public double Axial, Twist;
        public Double3 Lateral;
        public bool Any;
    }

    /// <summary>
    /// The legs' failure law (p7g-legs 1.2, design 3), engine-free and allocation-free in a step. Each leg's strengths are
    /// drawn once per flight from its own stream, leg.strength.&lt;leg&gt;, within their published bounds (PHY-24), and logged.
    /// Each step the contact model hands each leg its loads (LegLoads); the law compares them with each capacity, keeps the
    /// leg's state, takes off what the leg's own give relieves (LegRelief, the leg spring's force this step; its offsets, the
    /// next steps'), and makes the events. A leg below every capacity changes nothing, not a bit.
    ///
    /// The modes, in order each step (closed forms in + − × ÷ √ only):
    /// - The clamp. The wing nut's preload P = T/(K·d); its jaws hold a pull of 2μP and a twist of μ·P·D, and the two share
    ///   the friction (the friction circle). Past it the tube slides out (the slip takes the leg spring's excess pull; past
    ///   the grip's length the leg is off, Detached) and turns (a twist kept in the set). A push never loads the grip: the
    ///   tube's top bears on the arm (p7g-legs 1.1).
    /// - Buckling. Past the fixed–pinned load (20.19·EI/L², 1.1) the tube bows; the bow takes the leg spring's excess
    ///   shortening Δ, its amplitude w = √(Δ·L/2.5816) and its moment at the clamp 0.7151·P·w (the first mode's shape,
    ///   derived); it straightens as the load falls. The bow lies across the skid (body x), whose roll leaves the foot pinned
    ///   there: the way the leg already leans, else outward.
    /// - The clamp's edge. Its moment M is the sum of the leg spring's force across the foot times the lever, the tube
    ///   shapes' and the bow's. The axial load s = |P|/(σ_y·A) lowers the yield moment to (1 − s) and raises the shape
    ///   factor to f(1 + s) (the plastic moment's (1 − s²), the rectangle's interaction). The edge first yields at
    ///   M_c = (1 − s)·σ_y·I/(c·K_t): there it cracks (design 3), and the crack's tip is the section's yield from then on
    ///   (with the cracked factor on M_y the tube would part at 0.70 M_y, under the crack's own onset for K_t below 1.4: no
    ///   leg would ever keep a set, against the pilot's "bend", D-079).
    /// - The set. Past M_c the section yields, by the elastic–plastic tube's moment–curvature law in the rectangle's closed
    ///   form at the tube's own shape factor, κ/κ_c = √((f − 1)/(f − m)), m = M/M_c, and its curvature beyond the elastic
    ///   one spread over the cantilever's falling moment is kept: the set angle θ_p and the foot's offset δ_p, closed forms
    ///   of m. The offset moves the leg spring's rest across the leg, which is what holds the moment at the section's
    ///   capacity while it yields; the contact shapes stay where they are (1.3 rotates them).
    /// - Parting by strain. The clamp's edge's strain K_t·κ·c passes the elongation at break ε_b at
    ///   m* = f − (f − 1)·(κ_c·K_t·c/ε_b)²: the tube parts there (Broken), never at a stress ahead of its set.
    /// - The T-joint. The M3 pinch screw's preload holds the skid in its bore to 2μP_t along it; the socket breaks across
    ///   its layers past σ_i·π(D_s⁴ − D⁴)/(32·D_s). Either lets the skid go (SkidLoose).
    /// A broken or detached leg's state is kept and its law stops; its offsets stay. 1.3 takes its parts off the contacts
    /// and the build.
    /// </summary>
    public sealed class LegDamage
    {
        public const int Legs = 4;

        /// <summary>The legs' names in their streams, in Corners order.</summary>
        public static readonly string[] Names = { "rr", "fr", "rl", "fl" };

        /// <summary>The fixed–pinned column's first mode (derived; β the root of tan β = β, its shape
        /// w = C[sin kx − β cos kx − kx + β]): the clamp's moment over P·w_max, β/2π, and the shortening Δ over w_max²/L,
        /// β·∫₀^β(cos θ + β sin θ − 1)²dθ/(8π²).</summary>
        public const double BowMoment = 0.7151483265621014, BowShortening = 2.58157206125142;

        // Bisections of the set's return mapping: 60 halve its interval to 1e-18 of the shape factor's span.
        const int Bisections = 60;

        readonly LegDamageSpec _spec;
        readonly double _ei, _c, _shape, _buckling;
        readonly double[] _yieldMoment = new double[Legs], _squash = new double[Legs], _edge = new double[Legs], _partCurvature = new double[Legs],
            _slipForce = new double[Legs], _twistMoment = new double[Legs], _skidSlip = new double[Legs], _socketMoment = new double[Legs];

        readonly LegFlags[] _flags = new LegFlags[Legs];
        readonly double[] _envelope = { 1, 1, 1, 1 }, _twist = new double[Legs], _bow = new double[Legs], _slip = new double[Legs], _moment = new double[Legs];
        readonly Double3[] _bend = new Double3[Legs], _offset = new Double3[Legs], _bowDir = new Double3[Legs];
        readonly bool[] _flowing = new bool[Legs], _turning = new bool[Legs];

        /// <summary>Each leg's strengths drawn from its stream, recorded with their bounds in random's draws.</summary>
        public LegDamage(LegDamageSpec spec, FlightRandom random)
        {
            _spec = (spec ?? throw new ArgumentNullException(nameof(spec))).Clone();
            if (random == null) throw new ArgumentNullException(nameof(random));
            double i = _spec.SecondMoment, area = _spec.Area;
            _c = _spec.OuterDiameter / 2;
            _ei = _spec.Modulus * i;
            _shape = _spec.ShapeFactor;
            _buckling = _spec.BucklingLoad;
            double socket = Pi * (Pow4(_spec.SocketDiameter) - Pow4(_spec.OuterDiameter)) / (32 * _spec.SocketDiameter);
            for (int k = 0; k < Legs; k++)
            {
                SeedStream s = random.Open("leg.strength." + Names[k]);
                double yieldStress = Draw(random, ref s, "tube.yield_stress_pa", _spec.YieldStress);
                double elongation = Draw(random, ref s, "tube.elongation_at_break", _spec.Elongation);
                double edge = Draw(random, ref s, "clamp.edge_stress_factor", _spec.EdgeFactor);
                double torque = Draw(random, ref s, "clamp.torque_n_m", _spec.ClampTorque);
                double nut = Draw(random, ref s, "clamp.nut_factor", _spec.NutFactor);
                double mu = Draw(random, ref s, "clamp.friction", _spec.Friction);
                double screw = Draw(random, ref s, "tjoint.torque_n_m", _spec.TJointTorque);
                double layers = Draw(random, ref s, "tjoint.interlayer_strength_pa", _spec.Interlayer);
                _yieldMoment[k] = yieldStress * i / _c;
                _squash[k] = yieldStress * area;
                _edge[k] = edge;
                _partCurvature[k] = elongation / (edge * _c);
                double preload = torque / (nut * _spec.BoltDiameter);
                _slipForce[k] = 2 * mu * preload;
                _twistMoment[k] = mu * preload * _spec.OuterDiameter;
                _skidSlip[k] = 2 * mu * (screw / (nut * _spec.ScrewDiameter));
                _socketMoment[k] = layers * socket;
            }
        }

        const double Pi = 3.141592653589793;

        static double Pow4(double x) => x * x * x * x;

        static double Draw(FlightRandom random, ref SeedStream s, string parameter, in LegBound b) => random.Draw(ref s, parameter, b.Min, b.Max);

        public LegDamageSpec Spec => _spec;

        /// <summary>A leg's state now.</summary>
        public LegFlags Flags(int leg) => _flags[leg];

        /// <summary>A leg's set (body, rad): x and z its bend, the foot's way times the set angle at the clamp; y its twist in
        /// the clamp.</summary>
        public Double3 Set(int leg) => new Double3(_bend[leg].X, _twist[leg], _bend[leg].Z);

        /// <summary>The leg's stiffness over an intact one's by its state: the cracked factor once cracked, 0 broken or off
        /// (1.3 makes the contact law fly it).</summary>
        public double StiffnessFactor(int leg) =>
            (_flags[leg] & (LegFlags.Broken | LegFlags.Detached)) != 0 ? 0 : (_flags[leg] & LegFlags.Cracked) != 0 ? _spec.CrackedFactor : 1;

        /// <summary>The leg spring's rest moved by the leg's give: along the leg (the bow's shortening less the clamp's slip,
        /// m), across it at the foot (body, m) and turned about it (rad).</summary>
        public double AxialOffset(int leg) => _bow[leg] - _slip[leg];
        public Double3 LateralOffset(int leg) => _offset[leg];
        public double TwistOffset(int leg) => _twist[leg];

        /// <summary>Whether the leg's give has moved its spring's rest at all (an intact leg's spring flies as without the law,
        /// bit for bit).</summary>
        public bool Moved(int leg) => _bow[leg] != 0 || _slip[leg] != 0 || _twist[leg] != 0 || _offset[leg].X != 0 || _offset[leg].Z != 0;

        /// <summary>The bow's moment at the leg's clamp now, as the lateral vector it bends the foot toward (body), N·m.</summary>
        public Double3 BowMomentVector(int leg, double axial)
        {
            if (!(_bow[leg] > 0)) return default;
            double p = axial < _buckling ? axial : _buckling;
            if (!(p > 0)) return default;
            return _bowDir[leg] * (BowMoment * p * DetMath.Sqrt(_bow[leg] * _spec.FreeLength / BowShortening));
        }

        /// <summary>The leg's drawn capacities (for the harness and the tests): the clamp's edge's first yield unloaded,
        /// σ_y·I/(c·K_t), N·m; the moment the tube parts at unloaded, N·m; the buckling load, N; the clamp's pull and twist,
        /// N and N·m; the T-joint's skid slip, N, and socket, N·m.</summary>
        public double CrackMoment(int leg) => _yieldMoment[leg] / _edge[leg];

        /// <summary>The clamp's edge's first yield under an axial load, (1 − |P|/(σ_y·A))·σ_y·I/(c·K_t), N·m.</summary>
        public double CrackMoment(int leg, double axial) => _yieldMoment[leg] * (1 - DetMath.Abs(axial) / _squash[leg]) / _edge[leg];
        public double PartingMoment(int leg) => PartingRatio(leg, 0) * CrackMoment(leg);
        public double BucklingLoad => _buckling;
        public double ClampSlip(int leg) => _slipForce[leg];
        public double ClampTwist(int leg) => _twistMoment[leg];
        public double SkidSlip(int leg) => _skidSlip[leg];
        public double SocketMoment(int leg) => _socketMoment[leg];
        public double Lever(int leg) => _spec.Clamp[leg].Y - _spec.Foot[leg].Y;

        // m* at axial share s: the moment ratio where the edge's curvature κ_c·g(m) reaches the parting curvature.
        double PartingRatio(int leg, double s)
        {
            double f = _shape * (1 + s), kc = _yieldMoment[leg] * (1 - s) / _edge[leg] / _ei, g = _partCurvature[leg] / kc;
            return f - (f - 1) / (g * g);
        }

        /// <summary>
        /// One leg's step: its loads in, what its give relieves out, and its events (Part the leg's, or its T-joint's, Index
        /// the LegMode; A the load, B the capacity it passed, C the touch's closing speed). cause carries the contact that
        /// loads it most (its object, shape, material, surface and point).
        /// </summary>
        public void Step(int leg, in LegLoads loads, out LegRelief relief, StepEvents events, in FlightEvent cause, double speed)
        {
            relief = default;
            ref LegFlags flags = ref _flags[leg];
            _moment[leg] = 0;
            if ((flags & (LegFlags.Broken | LegFlags.Detached)) != 0) return;
            double p = loads.LawAxial + loads.ShapeAxial;

            // The clamp: the pull and the twist on the jaws' friction circle.
            double pull = p < 0 ? -p : 0, twist = loads.Twist, slipCap = _slipForce[leg], twistCap = _twistMoment[leg];
            double q = pull / slipCap * (pull / slipCap) + twist / twistCap * (twist / twistCap);
            if (q > 1)
            {
                double keep = 1 / DetMath.Sqrt(q);
                double over = DetMath.Abs(twist) * (1 - keep);
                if (over > 0 && loads.TwistStiffness > 0)
                {
                    double turn = (twist > 0 ? over : -over) / loads.TwistStiffness;
                    _twist[leg] += turn;
                    relief.Twist = turn;
                    relief.Any = true;
                    // A twist set when the twist is the jaws' larger share; a pull's slip turns them by its own small share.
                    if (DetMath.Abs(twist) / twistCap >= pull / slipCap)
                    {
                        flags |= LegFlags.Twisted | LegFlags.Set;
                        if (!_turning[leg]) Emit(events, EventKind.LegSet, leg, LegMode.ClampTwist, cause, DetMath.Abs(twist), keep * DetMath.Abs(twist), speed);
                        _turning[leg] = true;
                    }
                }
                double excess = pull * (1 - keep);
                if (excess > 0)
                {
                    _slip[leg] += excess / loads.AxialStiffness;
                    double law = loads.LawAxial < 0 ? -loads.LawAxial : 0;
                    relief.Axial -= excess < law ? excess : law;
                    relief.Any = true;
                    if (_slip[leg] > _spec.GripLength)
                    {
                        flags |= LegFlags.Detached;
                        Emit(events, EventKind.LegDetached, leg, LegMode.ClampSlip, cause, pull, keep * pull, speed);
                        return;
                    }
                }
            }
            else _turning[leg] = false;

            // Buckling: the bow takes the excess shortening past the buckling load, and gives it back as the load falls.
            if (p > _buckling || _bow[leg] > 0)
            {
                double was = _bow[leg], now = was + (p - _buckling) / loads.AxialStiffness;
                if (!(now > 0)) now = 0;
                if (now != was)
                {
                    if (!(was > 0))
                    {
                        // The bow's way: across the skid (body x), the way the leg already leans, else outward.
                        double lean = loads.LawLateral.X * Lever(leg) + loads.ShapeMoment.X;
                        double side = lean > 0 ? 1 : lean < 0 ? -1 : _spec.Foot[leg].X >= 0 ? 1 : -1;
                        _bowDir[leg] = new Double3(side, 0, 0);
                    }
                    double take = (now - was) * loads.AxialStiffness;
                    double law = loads.LawAxial > 0 ? loads.LawAxial : 0;
                    relief.Axial += take < law ? take : law;
                    relief.Any = true;
                    _bow[leg] = now;
                }
                if (now > 0) flags |= LegFlags.Bowed;
                else flags &= ~LegFlags.Bowed;
            }

            // The clamp's edge: the moment's lateral vector, the section under its axial load.
            double carried = p < _buckling ? p : _buckling;
            Double3 bow = BowMomentVector(leg, p);
            Double3 mu = loads.LawLateral * Lever(leg) + loads.ShapeMoment + bow;
            double m = mu.Length();
            _moment[leg] = m;
            double s = DetMath.Abs(carried) / _squash[leg];
            if (s > 0.999) s = 0.999;
            double mc = _yieldMoment[leg] * (1 - s) / _edge[leg], f = _shape * (1 + s);
            LegMode mode = bow.Length() * 2 >= m ? LegMode.Buckling : LegMode.Bending;
            if (m >= mc && (flags & LegFlags.Cracked) == 0)
            {
                flags |= LegFlags.Cracked;
                Emit(events, EventKind.LegCracked, leg, mode, cause, m, mc, speed);
            }
            if (m > _envelope[leg] * mc)
            {
                double env = _envelope[leg], top = PartingRatio(leg, s);
                Double3 way = mu * (1 / m);
                double relievable = 0;
                if (loads.LawTouching)
                {
                    relievable = Double3.Dot(loads.LawLateral, way);
                    if (!(relievable > 0)) relievable = 0;
                }
                double kl = loads.LateralStiffness, lever = Lever(leg);
                // δ_p and θ_p per unit of κ_c·L² and κ_c·L.
                double kc = mc / _ei, d0 = OffsetRatio(env, f), r0 = RotationRatio(env, f), scale = kc * lever * lever;
                if (!(top > env) || Excess(top, mc, m, kl, lever, relievable, scale, d0, f) < 0)
                {
                    double last = RotationRatio(top, f) - r0;
                    if (last > 0) _bend[leg] = _bend[leg] + way * (last * kc * lever);
                    if (top > env) _envelope[leg] = top;
                    flags |= LegFlags.Broken;
                    Emit(events, EventKind.LegBroken, leg, mode, cause, m, top * mc, speed);
                    return;
                }
                double lo = env, hi = top;
                for (int n = 0; n < Bisections; n++)
                {
                    double mid = lo + (hi - lo) / 2;
                    if (Excess(mid, mc, m, kl, lever, relievable, scale, d0, f) < 0) lo = mid;
                    else hi = mid;
                }
                double grown = (OffsetRatio(hi, f) - d0) * scale;
                if (relievable > 0)
                {
                    double give = relievable / kl;
                    double move = grown < give ? grown : give;
                    if (move > 0)
                    {
                        _offset[leg] = _offset[leg] + way * move;
                        relief.Lateral = relief.Lateral + way * (kl * move);
                        relief.Any = true;
                    }
                }
                _bend[leg] = _bend[leg] + way * ((RotationRatio(hi, f) - r0) * kc * lever);
                _envelope[leg] = hi;
                flags |= LegFlags.Set;
                if (!_flowing[leg]) Emit(events, EventKind.LegSet, leg, mode, cause, m, env * mc, speed);
                _flowing[leg] = true;
            }
            else _flowing[leg] = false;

            // The T-joint: the skid's slip in its bore, the socket's break.
            if ((flags & LegFlags.SkidLoose) != 0) return;
            double along = DetMath.Abs(loads.SkidAxial), socket = _spec.SocketLever * loads.LawLateral.Length();
            if (along > _skidSlip[leg])
            {
                flags |= LegFlags.SkidLoose;
                Emit(events, EventKind.SkidLoose, leg, LegMode.TJointSlip, cause, along, _skidSlip[leg], speed);
            }
            else if (socket > _socketMoment[leg])
            {
                flags |= LegFlags.SkidLoose;
                Emit(events, EventKind.SkidLoose, leg, LegMode.TJointFracture, cause, socket, _socketMoment[leg], speed);
            }
        }

        /// <summary>The leg's moment at the clamp's edge in the last step, N·m (the record's leg_moment).</summary>
        public double Moment(int leg) => _moment[leg];

        // The set's return mapping: h(m) = m·M_c + min(L·k_l·(δ_p(m) − δ_p(m_env)), L·relievable) − M, increasing in m.
        static double Excess(double m, double mc, double moment, double kl, double lever, double relievable, double scale, double d0, double f)
        {
            double give = lever * kl * (OffsetRatio(m, f) - d0) * scale, cap = lever * relievable;
            return m * mc + (give < cap ? give : cap) - moment;
        }

        /// <summary>The set angle at the clamp per κ_c·L of a cantilever whose clamp carries m·M_c, its moment falling to 0 at
        /// the foot: ∫ κ_p dx = (1/m)·[2(f − 1) − 2√((f − 1)(f − m)) − (m² − 1)/2].</summary>
        public static double RotationRatio(double m, double f)
        {
            if (!(m > 1)) return 0;
            if (m > f) m = f;
            return (2 * (f - 1) - 2 * DetMath.Sqrt((f - 1) * (f - m)) - (m * m - 1) / 2) / m;
        }

        /// <summary>The foot's offset per κ_c·L² from the same plastic curvature: ∫ κ_p·(L − x) dx = (1/m²)·[√(f − 1)·(2f(√(f − 1) −
        /// √(f − m)) − (2/3)((f − 1)^1.5 − (f − m)^1.5)) − (m³ − 1)/3].</summary>
        public static double OffsetRatio(double m, double f)
        {
            if (!(m > 1)) return 0;
            if (m > f) m = f;
            double a = f - 1, b = f - m, ra = DetMath.Sqrt(a), rb = DetMath.Sqrt(b);
            return (ra * (2 * f * (ra - rb) - 2.0 / 3 * (a * ra - b * rb)) - (m * m * m - 1) / 3) / (m * m);
        }

        void Emit(StepEvents events, EventKind kind, int leg, LegMode mode, in FlightEvent cause, double load, double capacity, double speed)
        {
            FlightEvent e = cause;
            e.Kind = kind;
            e.Part = (ushort)(kind == EventKind.SkidLoose ? _spec.TJointParts[leg] : _spec.Parts[leg]);
            e.Index = (byte)mode;
            e.A = load;
            e.B = capacity;
            e.C = speed;
            events.Add(e);
        }
    }
}
