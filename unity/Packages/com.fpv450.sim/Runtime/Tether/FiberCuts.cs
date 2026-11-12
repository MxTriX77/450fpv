using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tether
{
    /// <summary>What broke the line (p7d-tether design 7): TetherBreak's Index, low 4 bits (the place's FiberMember the high 4).</summary>
    public enum TetherMechanism : byte
    {
        None = 0,
        /// <summary>A segment crossed a turning disc between its hub and its tip, struck at the next blade pass.</summary>
        Prop = 1,
        /// <summary>The tension stress T/A_g on the span reached the flight's span strength (fiber.span_strength).</summary>
        Tension = 2,
        /// <summary>A bend's stress E·r/R_eff + T/A_g (a contact, the exit lip) reached its section's strength.</summary>
        Bend = 3,
        /// <summary>A section worn through its coating met its abraded strength (at a bend or in tension).</summary>
        Abrasion = 4,
        /// <summary>The pack's peel front bent the line past its section's strength, E·r·√(2G/EI) + T_r/A_g.</summary>
        Peel = 5,
        /// <summary>The chain went non-finite (CableChain's wedge at a node): the line broken there so the flight goes on.</summary>
        Wedge = 6,
    }

    /// <summary>A strength's band (FiberSection's Index, low 2 bits; 0x40 a wear-through, 0x80 the strength a break met).</summary>
    public enum SectionBand : byte { Intact = 0, Flaw = 1, Abraded = 2, Span = 3 }

    /// <summary>One bend of the line after a step (p7d-tether design 6, 8): a run of neighbouring nodes pressing one capstan
    /// member (one wrap), a single node on any other member, or the exit lip.</summary>
    public struct FiberBend
    {
        /// <summary>The member (FiberMember and its identity as CableContact carries it), and its surface class.</summary>
        public byte Kind;
        public ulong Member;
        public bool Rough;
        /// <summary>The run's first and last node and its sharpest (the exit for the lip).</summary>
        public int First, Last, Node;
        /// <summary>The run's whole turn, rad; the larger tension beside it, N; the arris, m; R_eff, m; σ at the bend, Pa; the
        /// normal force pressing it, N.</summary>
        public double Turn, Tension, Edge, Radius, Stress, Press;
        /// <summary>The link's loss there, dB: the macrobend L(R)·θ/2π and the microbend c_m·F_n.</summary>
        public double Macro, Micro;
        /// <summary>Where the line touches the member at its sharpest node, world m.</summary>
        public Double3 Point;
    }

    /// <summary>A break the checks found (FiberCuts.Check, FiberLine's prop check).</summary>
    public struct FiberBreak
    {
        public TetherMechanism Mechanism;
        /// <summary>The place: a member (FiberMember and identity), Lip, Peel, Disc (Member the corner) or Span.</summary>
        public byte Kind;
        public ulong Member;
        /// <summary>The node the near piece starts at; −1 when the line breaks at the drone (the lip, the peel): nothing hangs.</summary>
        public int Node;
        /// <summary>A segment's break (tension, prop, abrasion between nodes): the segment and the fraction along it, else −1.</summary>
        public int Segment;
        public double Fraction;
        /// <summary>s at the break, m; the bend's turn, rad (the prop: the crossing's radius, m), the tension, N, R_eff, m (the
        /// prop: the blade's speed there, m/s), the stress, Pa, and the strength it met, Pa.</summary>
        public double Material, Turn, Tension, Radius, Stress, Strength;
        public long Section;
        public SectionBand Band;
        public Double3 Point;
    }

    /// <summary>
    /// Cut risk, never certainty (p7d-tether design 7 and 14; PHY-19, PHY-24): the line's strengths and the checks that break it.
    /// - The strengths (tether.json's strength block): the span's per-flight σ_span (stream fiber.span_strength, one draw at the
    ///   start, met by T/A_g anywhere on the span), and each section_length_m section's local strength, counter-based on its
    ///   index k = ⌊s/ℓ⌋ (stream fiber.section_strength: draw 3k a flaw with probability ℓ/flaw_spacing_m, 3k+1 its strength in
    ///   the flaw's or the intact band, 3k+2 its abraded strength once worn through), so the order of use never shifts a draw.
    ///   Each band is its quantile table, read piecewise linearly: + − × ÷ only.
    /// - The bends after each step (Collect): every pressing contact, neighbouring nodes on one capstan member as one wrap of
    ///   their whole turn (the elastica over a point edge takes the wrap's θ), R_eff = max(R_edge + c, √(EI/T)/(2·sin(θ/4))),
    ///   σ = E·r/R_eff + T/A_g; and the exit lip, the hole's edge (spool.lip_radius_m at the lip's wrap θ_e under the exit's
    ///   tension: its 0.3 mm is under the elastica's radius at every tension the exit meets, so the elastica decides there).
    /// - The hole's edge's wear (WearLip, D-087): the line paying out over it and its lead turning round it, at the edge's own
    ///   Archard k (spool.lip_archard_wear_coefficient), beyond the angle the bore lets the line leave without touching it.
    /// - Abrasion (Wear): a section pressed on a member other than the ground or a layer (the coating on soil or straw under the
    ///   line's own weight is far from the edge's Hertz regime: assumed) wears by Archard, h += k·p·Δx/H, p the Hertz mean pressure
    ///   of the line (r = coating radius) on the arris (max(R_edge, c), crossed cylinders: R = √(r·R₂), E* = E_c/(1 − ν²)), never
    ///   above H (a plastic contact's area is F/H), k the table's severe band on an abrasive member (a rough row, a snag record, a
    ///   metal arris of AbrasiveEdge or less) and its mild band on a smooth one (the drone's parts, a conductor, a smooth face):
    ///   Δx the slide across the line at the node (the same point of the line rubbing on), and the material passing the touch
    ///   along the line (slid through a capstan, or the line gliding past a member), which rubs each point it passes once by the
    ///   contact's width 2a, so its depth is the passes' work over the swept range (Σ k·p·2a·|Δs| / max(range, 2a) over H,
    ///   derived). Worn past the coating's thickness, its strength is its abraded draw.
    /// - The checks (Check), the first in design 7's order the mechanism: tension, bend (intact and flawed sections), abrasion
    ///   (an abraded section at a bend, or in tension), the peel (κ = √(2G/EI) at the pack's peel front while line flows, G
    ///   the pack's peel energy at its rate: the elastica at a peel front, published). The prop check is FiberLine's (its rotors).
    /// - FiberSection: a section's first read at a contact; at the lip and the peel (every section passes them once, so their
    ///   reads would be most of the log) only when its stress first passes half its strength, and at a break.
    /// Engine-free, + − × ÷ √ and DetMath, preallocated: 0 B a step.
    /// </summary>
    public sealed class FiberCuts
    {
        /// <summary>The section table's size: sections read at a contact or worn (assumed, design 13: 4096); a power of two.</summary>
        public const int Capacity = 4096;

        /// <summary>The bends a step holds: two members a node of 48 (the spacing law's 32 and a terrain's crests,
        /// FiberLine.CrestNodes) and the lip (numerical).</summary>
        public const int MaxBends = 104;

        // The logging rule at the lip and the peel (numerical, not physics): a section's read is logged when its stress first
        // passes this share of its strength.
        const double NearMiss = 0.5;

        const double Pi = 3.141592653589793;

        /// <summary>An arris this sharp or sharper cuts as an abrasive edge (a sheared, rolled or sawn metal edge; assumed): the
        /// table's severe Archard k, else the mild one on a smooth member.</summary>
        public const double AbrasiveEdge = 0.001;

        readonly double _youngs, _glassRadius, _glassArea, _bending, _coating, _lineRadius, _reduced, _hardness, _severe, _mild;
        readonly double _sectionLength, _flawChance, _lipRadius, _lipWear, _mouthRadius, _freeAngle;
        readonly double[] _span, _flaw, _intact, _abraded;
        readonly double _macroA, _macroRadiusA, _macroDecay, _microSmooth, _microRough;
        readonly bool _oneStrength;
        SeedStream _sections;

        // The section table: open addressing on k, never emptied (the flight's).
        readonly long[] _key = new long[Capacity];
        readonly bool[] _used = new bool[Capacity], _worn = new bool[Capacity], _logged = new bool[Capacity];
        readonly double[] _direct = new double[Capacity], _passes = new double[Capacity], _lo = new double[Capacity], _hi = new double[Capacity];
        readonly double[] _wornStrength = new double[Capacity];
        readonly int[] _wornList = new int[Capacity];
        int _count, _wornCount;
        bool _full;

        readonly FiberBend[] _bends = new FiberBend[MaxBends];
        readonly int[] _open = new int[2], _next = new int[2];
        int _bendCount;

        public FiberCuts(TetherTable table, FlightRandom random, bool oneStrength = false)
        {
            _youngs = table.Number("fiber.youngs_modulus_pa");
            _glassRadius = table.Number("fiber.glass_diameter_m") / 2;
            _glassArea = table.Number("fiber.glass_area_m2");
            _bending = table.Number("fiber.bending_stiffness_n_m2");
            _coating = table.Number("fiber.coating_thickness_m");
            _lineRadius = table.Number("fiber.coating_diameter_m") / 2;
            double nu = table.Number("fiber.coating_poisson_ratio");
            _reduced = table.Number("fiber.coating_modulus_pa") / (1 - nu * nu);
            _hardness = table.Number("fiber.coating_hardness_pa");
            _severe = table.Number("fiber.archard_wear_coefficient");
            _mild = table.Number("fiber.archard_wear_coefficient_mild");
            _sectionLength = table.Number("fiber.section_length_m");
            _flawChance = _sectionLength / table.Number("strength.flaw_spacing_m");
            _span = table.Row("strength.span_pa").Values;
            _flaw = table.Row("strength.flaw_pa").Values;
            _intact = table.Row("strength.intact_pa").Values;
            _abraded = table.Row("strength.abraded_pa").Values;
            _lipRadius = table.Number("spool.lip_radius_m");
            _lipWear = table.Number("spool.lip_archard_wear_coefficient");
            _mouthRadius = table.Number("spool.mouth_radius_m");
            // The line lies across the bore before it bends over the hole's edge: no touch within atan(2·r_m/L_n) of the axis.
            _freeAngle = DetMath.Atan2(2 * _mouthRadius, table.Number("spool.nozzle_length_m"));
            _macroA = table.Number("link.macrobend_loss_a_db");
            _macroRadiusA = table.Number("link.macrobend_radius_a_m");
            _macroDecay = table.Number("link.macrobend_decay_per_m");
            _microSmooth = table.Number("link.microbend_smooth_db_per_n");
            _microRough = table.Number("link.microbend_rough_db_per_n");
            _oneStrength = oneStrength;

            SeedStream span = random.Open("fiber.span_strength");
            SpanStrength = random.DrawQuantile(ref span, "span_strength_pa", _span);
            _sections = random.Open("fiber.section_strength");
            for (int i = 0; i < Capacity; i++)
            {
                _lo[i] = double.PositiveInfinity;
                _hi[i] = double.NegativeInfinity;
            }
        }

        // ---- the strengths ----

        /// <summary>The flight's span strength σ_span, Pa.</summary>
        public double SpanStrength { get; }

        /// <summary>The section a material coordinate lies in.</summary>
        public long Section(double s) => s > 0 ? (long)(s / _sectionLength) : 0;

        /// <summary>The length of a section, m.</summary>
        public double SectionLength => _sectionLength;

        /// <summary>Section k's drawn strength, Pa, and its band (flaw or intact), from its own counter (never the table's state);
        /// the known-wrong one strength (oneStrength) gives σ_span everywhere.</summary>
        public double Drawn(long k, out SectionBand band)
        {
            if (_oneStrength)
            {
                band = SectionBand.Span;
                return SpanStrength;
            }
            ulong i = (ulong)k * 3;
            bool flawed = Unit(_sections.BitsAt(i)) < _flawChance;
            band = flawed ? SectionBand.Flaw : SectionBand.Intact;
            return Quantile(flawed ? _flaw : _intact, Unit(_sections.BitsAt(i + 1)));
        }

        /// <summary>Section k's abraded strength, Pa: its third draw.</summary>
        public double AbradedDraw(long k) => _oneStrength ? SpanStrength : Quantile(_abraded, Unit(_sections.BitsAt((ulong)k * 3 + 2)));

        /// <summary>Section k's strength now, Pa: its abraded draw once worn through, else its drawn strength.</summary>
        public double Strength(long k, out SectionBand band)
        {
            int slot = Find(k);
            if (slot >= 0 && _worn[slot])
            {
                band = SectionBand.Abraded;
                return _wornStrength[slot];
            }
            return Drawn(k, out band);
        }

        /// <summary>Section k's wear depth so far, m (0 for a section never rubbed).</summary>
        public double Wear(long k)
        {
            int slot = Find(k);
            return slot >= 0 ? Depth(slot, 0) : 0;
        }

        /// <summary>Whether section k is worn through its coating.</summary>
        public bool Worn(long k)
        {
            int slot = Find(k);
            return slot >= 0 && _worn[slot];
        }

        /// <summary>A quantile table [p₀, v₀, p₁, v₁, …] (p from 0 to 1) at u in [0, 1), read piecewise linearly.</summary>
        public static double Quantile(double[] q, double u)
        {
            int n = q.Length / 2;
            for (int i = 1; i < n; i++)
            {
                double p0 = q[2 * i - 2], p1 = q[2 * i];
                if (u > p1 && i < n - 1) continue;
                double f = p1 > p0 ? (u - p0) / (p1 - p0) : 0;
                if (f < 0) f = 0;
                else if (f > 1) f = 1;
                return q[2 * i - 1] + (q[2 * i + 1] - q[2 * i - 1]) * f;
            }
            return q[1];
        }

        static double Unit(ulong bits) => (bits >> 11) * (1.0 / 9007199254740992.0);

        // ---- the bend and its loss ----

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

        /// <summary>The glass's stress at a bend, σ = E·r/R_eff + T/A_g, Pa.</summary>
        public double BendStress(double rEff, double tension) =>
            (rEff < double.PositiveInfinity ? _youngs * _glassRadius / rEff : 0) + (tension > 0 ? tension / _glassArea : 0);

        /// <summary>The macrobend loss of a turn θ (rad) at a bend radius R_eff, dB: the table's law per full turn at the mandrel's
        /// radius R_eff − c (G.657's test radius is the mandrel's, under the line), L_a·e^(−decay·(R − R_a)), times θ/2π.</summary>
        public double MacroLoss(double rEff, double theta)
        {
            if (!(rEff < double.PositiveInfinity) || !(theta > 0)) return 0;
            double r = rEff - _coating;
            return _macroA * DetMath.Exp(-_macroDecay * (r - _macroRadiusA)) * theta / (2 * Pi);
        }

        /// <summary>The pack's peel front's bend stress, E·r·√(2G/EI) + T/A_g, Pa, at the peel energy G (J/m) under T (N).</summary>
        public double PeelStress(double peelEnergy, double tension) =>
            _youngs * _glassRadius * DetMath.Sqrt(2 * peelEnergy / _bending) + (tension > 0 ? tension / _glassArea : 0);

        /// <summary>The glass's area, m² (σ_t = T/A_g).</summary>
        public double GlassArea => _glassArea;

        // ---- after each step: the bends ----

        /// <summary>The bends after the last Collect: the lip last.</summary>
        public int BendCount => _bendCount;
        public ref readonly FiberBend Bend(int i) => ref _bends[i];

        /// <summary>The least R_eff over the bends and the lip after the last Collect (FiberMinBendRadius), m; +∞ for none.</summary>
        public double MinBendRadius { get; private set; } = double.PositiveInfinity;

        /// <summary>
        /// The line's bends after the chain's step: each pressing contact, neighbouring nodes on one capstan member as one wrap,
        /// and the exit lip at the lip's wrap (rad) under the exit's tension (N), at the exit's point.
        /// </summary>
        public void Collect(CableChain chain, double lipTurn, double exitTension, Double3 exit)
        {
            int n = chain.Count;
            _bendCount = 0;
            int open = 0;
            for (int i = 1; i < n - 1; i++)
            {
                ref CableNode a = ref chain[i];
                Double3 da = a.Position - chain[i - 1].Position, db = chain[i + 1].Position - a.Position;
                double theta = DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
                double t0 = chain.Tension(i - 1), t1 = chain.Tension(i), hi = t0 > t1 ? t0 : t1;
                int next = 0;
                for (int k = 0; k < 2; k++)
                {
                    ref CableContact c = ref k == 0 ? ref a.Contact0 : ref a.Contact1;
                    if (!c.Active || !(c.NormalForce > 0)) continue;
                    int b = -1;
                    if (c.Capstan)
                        for (int o = 0; o < open; o++)
                        {
                            ref FiberBend p = ref _bends[_open[o]];
                            if (p.Kind == c.MemberKind && p.Member == c.Member && p.Last == i - 1) b = _open[o];
                        }
                    if (b >= 0)
                    {
                        ref FiberBend p = ref _bends[b];
                        if (theta > TurnAt(chain, p.Node)) { p.Node = i; p.Point = a.Position - c.Normal * _lineRadius; }
                        p.Turn += theta;
                        if (hi > p.Tension) p.Tension = hi;
                        p.Press += c.NormalForce;
                        p.Last = i;
                    }
                    else
                    {
                        if (_bendCount == MaxBends - 1) continue; // the lip's place is kept
                        b = _bendCount++;
                        _bends[b] = new FiberBend
                        {
                            Kind = c.MemberKind, Member = c.Member, Rough = c.Rough, First = i, Last = i, Node = i, Turn = theta, Tension = hi,
                            Edge = c.Edge, Press = c.NormalForce, Point = a.Position - c.Normal * _lineRadius,
                        };
                    }
                    if (c.Capstan && next < 2) _next[next++] = b;
                }
                for (int o = 0; o < next; o++) _open[o] = _next[o];
                open = next;
            }
            _bends[_bendCount++] = new FiberBend
            {
                Kind = (byte)FiberMember.Lip, First = n - 1, Last = n - 1, Node = n - 1, Turn = lipTurn, Tension = exitTension,
                Edge = _lipRadius, Point = exit,
            };
            MinBendRadius = double.PositiveInfinity;
            for (int b = 0; b < _bendCount; b++)
            {
                ref FiberBend p = ref _bends[b];
                p.Radius = BendRadius(p.Edge, p.Turn, p.Tension);
                p.Stress = BendStress(p.Radius, p.Tension);
                p.Macro = MacroLoss(p.Radius, p.Turn);
                p.Micro = (p.Rough ? _microRough : _microSmooth) * p.Press;
                if (p.Radius < MinBendRadius) MinBendRadius = p.Radius;
            }
        }

        static double TurnAt(CableChain chain, int i)
        {
            Double3 da = chain[i].Position - chain[i - 1].Position, db = chain[i + 1].Position - chain[i].Position;
            return DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
        }

        // ---- after each step: the wear ----

        /// <summary>
        /// Archard's wear over the step at every pressing contact but the ground's and a layer's (the class doc); a section worn
        /// past the coating's thickness takes its abraded strength, logged as FiberSection (Abraded | 0x40) through names, which
        /// may be null (no event then). Returns whether any section wore through.
        /// </summary>
        public bool Wear(CableChain chain, double dt, FiberContacts names, StepEvents events)
        {
            bool through = false;
            int n = chain.Count;
            for (int i = 1; i < n - 1; i++)
            {
                ref CableNode a = ref chain[i];
                for (int k = 0; k < 2; k++)
                {
                    ref CableContact c = ref k == 0 ? ref a.Contact0 : ref a.Contact1;
                    if (!c.Active || !(c.NormalForce > 0)) continue;
                    if (c.MemberKind == (byte)FiberMember.Ground || c.MemberKind == (byte)FiberMember.Layer) continue;
                    // The line's slide on the member: across the line (the same point rubbing on), and along it (material passing
                    // the touch: slid through a capstan, or the line gliding past along its length, the touch moving along the
                    // line from the node's material point).
                    Double3 v = a.Velocity - c.Velocity;
                    Double3 tangent = chain[i + 1].Position - chain[i - 1].Position;
                    double tl = tangent.Length();
                    tangent = tl > 0 ? tangent * (1 / tl) : default;
                    double glide = Double3.Dot(v, tangent);
                    Double3 across = v - c.Normal * Double3.Dot(c.Normal, v) - tangent * glide;
                    double from = c.Through + c.Rubbed;
                    c.Rubbed -= glide * dt;
                    double to = a.Material + c.Rubbed;
                    double dx = across.Length() * dt, ds = DetMath.Abs(to - from);
                    if (!(dx > 0) && !(ds > 0)) continue;
                    // Hertz: crossed cylinders, the line's coating on the arris (a point edge as the coating's own radius); past the
                    // coating's hardness the contact is plastic, its area F/H (the pressure is never above H).
                    double r2 = c.Edge > _coating ? c.Edge : _coating, radius = DetMath.Sqrt(_lineRadius * r2);
                    double contact = DetMath.Cbrt(3 * c.NormalForce * radius / (4 * _reduced));
                    double p = c.NormalForce / (Pi * contact * contact);
                    if (p > _hardness)
                    {
                        p = _hardness;
                        contact = DetMath.Sqrt(c.NormalForce / (Pi * _hardness));
                    }
                    // Archard's k by the member: abrasive (a rough row, a snag record, a metal arris of 1 mm or less) or smooth.
                    bool abrasive = c.Rough || c.MemberKind == (byte)FiberMember.Snag
                        || c.MemberKind == (byte)FiberMember.Shape && c.Edge <= AbrasiveEdge;
                    double wk = abrasive ? _severe : _mild;
                    long sec = Section(to);
                    int slot = Slot(sec, events, a.Position);
                    if (slot < 0) continue;
                    _direct[slot] += wk * p * dx;
                    _passes[slot] += wk * p * 2 * contact * ds;
                    double lo = to < from ? to : from, hi = to < from ? from : to;
                    if (lo < _lo[slot]) _lo[slot] = lo;
                    if (hi > _hi[slot]) _hi[slot] = hi;
                    if (_worn[slot]) continue;
                    double depth = Depth(slot, 2 * contact);
                    if (!(depth >= _coating)) continue;
                    _worn[slot] = true;
                    _wornStrength[slot] = AbradedDraw(sec);
                    _wornList[_wornCount++] = slot;
                    through = true;
                    if (names == null) continue;
                    FlightEvent e = names.Named(EventKind.FiberSection, c.MemberKind, c.Member, a.Position - c.Normal * _lineRadius,
                        sec * _sectionLength, _wornStrength[slot], depth);
                    e.Index = (byte)SectionBand.Abraded | 0x40;
                    events.Add(e);
                }
            }
            return through;
        }

        /// <summary>
        /// The hole's edge's wear over the step (after Collect; p7d-tether 1.8, D-087): the line pressed on the edge at the lip's
        /// wrap θ under the exit's tension T, beyond the angle it passes the bore free (spool.nozzle_length_m, spool.mouth_radius_m).
        /// Its load per metre q = T/R_eff over the wrap's length R_eff·θ; Hertz's line contact of the coating on the edge, half-width
        /// b = √(4·q·r_c/(π·E*)), mean pressure p = q/(2b), never above H (plastic: 2b = q/H). Archard at the lip's own k
        /// (spool.lip_archard_wear_coefficient): line paying out (payout m this step) passes the edge once, each point sliding
        /// R_eff·θ along it and, as the lead turns about the nozzle's axis (turn rad this step), r_m·turn round it for every
        /// payout's worth, so its one pass wears k·p·R_eff·θ·√(1 + (r_m·turn/payout)²)/H, and the section at the lip wears through
        /// when that reaches the coating; line held still (no payout) rubs round the edge in place, k·p·r_m·turn on the section at
        /// the lip, its depth summed (Depth). A section worn through takes its abraded strength (FiberSection Abraded | 0x40 at the
        /// Lip); returns its depth's share of the coating this step (0 untouched, ≥ 1 worn through).
        /// </summary>
        public double WearLip(double payout, double turn, double material, FiberContacts names, StepEvents events)
        {
            ref readonly FiberBend lip = ref _bends[_bendCount - 1];
            if (!(lip.Turn > _freeAngle) || !(lip.Tension > 0) || !(lip.Radius < double.PositiveInfinity)) return 0;
            double q = lip.Tension / lip.Radius;
            double half = DetMath.Sqrt(4 * q * _lineRadius / (Pi * _reduced));
            double p = q / (2 * half);
            if (p > _hardness) p = _hardness;
            double across = _mouthRadius * DetMath.Abs(turn);
            long sec = Section(material);
            double depth;
            int slot;
            if (payout > 0)
            {
                double ratio = across / payout;
                depth = _lipWear * p * lip.Radius * lip.Turn * DetMath.Sqrt(1 + ratio * ratio) / _hardness;
                if (!(depth >= _coating)) return depth / _coating;
                slot = Slot(sec, events, lip.Point);
                if (slot < 0 || _worn[slot]) return depth / _coating;
            }
            else
            {
                if (!(across > 0)) return 0;
                slot = Slot(sec, events, lip.Point);
                if (slot < 0) return 0;
                _direct[slot] += _lipWear * p * across;
                double lo = material, hi = material;
                if (lo < _lo[slot]) _lo[slot] = lo;
                if (hi > _hi[slot]) _hi[slot] = hi;
                depth = Depth(slot, 0);
                if (_worn[slot] || !(depth >= _coating)) return depth / _coating;
            }
            _worn[slot] = true;
            _wornStrength[slot] = AbradedDraw(sec);
            _wornList[_wornCount++] = slot;
            if (names != null)
            {
                FlightEvent e = FiberLine.Place(EventKind.FiberSection, (byte)FiberMember.Lip, 0, names, lip.Point, sec * _sectionLength,
                    _wornStrength[slot], depth);
                e.Index = (byte)SectionBand.Abraded | 0x40;
                events.Add(e);
            }
            return depth / _coating;
        }

        // The depth of a slot's wear, m: the direct rubs and the passes over the swept range (no shorter than the contact's width).
        double Depth(int slot, double width)
        {
            double range = _hi[slot] - _lo[slot];
            if (!(range > width)) range = width;
            double passes = range > 0 ? _passes[slot] / range : 0;
            return (_direct[slot] + passes) / _hardness;
        }

        // ---- after each step: the checks ----

        /// <summary>
        /// The tension, bend, abrasion and peel checks after Collect and Wear (the prop's is FiberLine's, first): false with the
        /// line whole; else the first that holds in design 7's order. paying: line flowed this step, at the peel energy G (J/m,
        /// the rate law and the irregularity in it) under the pack's resistance T_r (N), the peel's section at peelMaterial (m).
        /// FiberSection logs each section's first read at a contact (and the lip's and the peel's near misses) through names.
        /// </summary>
        public bool Check(CableChain chain, bool paying, double peelEnergy, double packTension, double peelMaterial, FiberContacts names,
            StepEvents events, out FiberBreak b)
        {
            b = default;
            b.Segment = -1;
            int n = chain.Count;

            // The span's tension.
            double most = 0;
            int at = -1;
            for (int j = 0; j < n - 1; j++)
            {
                double t = chain.Tension(j);
                if (t > most)
                {
                    most = t;
                    at = j;
                }
            }
            if (at >= 0 && most / _glassArea >= SpanStrength)
            {
                b.Mechanism = TetherMechanism.Tension;
                b.Kind = (byte)FiberMember.Span;
                b.Segment = at;
                b.Fraction = 0.5;
                b.Node = at + 1 < n - 1 ? at + 1 : -1;
                b.Material = 0.5 * (chain[at].Material + chain[at + 1].Material);
                b.Tension = most;
                b.Radius = double.PositiveInfinity;
                b.Stress = most / _glassArea;
                b.Strength = SpanStrength;
                b.Band = SectionBand.Span;
                b.Section = Section(b.Material);
                b.Point = (chain[at].Position + chain[at + 1].Position) * 0.5;
                return true;
            }

            // The bends at intact and flawed sections, then the abraded ones.
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < _bendCount; k++)
                {
                    ref FiberBend p = ref _bends[k];
                    bool lip = p.Kind == (byte)FiberMember.Lip;
                    for (int i = p.First; i <= p.Last; i++)
                    {
                        double s = chain[i].Material;
                        long sec = Section(s);
                        double strength = Strength(sec, out SectionBand band);
                        if ((band == SectionBand.Abraded) != (pass == 1)) continue;
                        if (pass == 0) Read(sec, strength, band, p, lip, names, events);
                        if (!(p.Stress >= strength)) continue;
                        b.Mechanism = band == SectionBand.Abraded ? TetherMechanism.Abrasion : TetherMechanism.Bend;
                        b.Kind = p.Kind;
                        b.Member = p.Member;
                        b.Node = lip ? -1 : i;
                        b.Material = s;
                        b.Turn = p.Turn;
                        b.Tension = p.Tension;
                        b.Radius = p.Radius;
                        b.Stress = p.Stress;
                        b.Strength = strength;
                        b.Band = band;
                        b.Section = sec;
                        b.Point = lip ? p.Point : chain[i].Position;
                        return true;
                    }
                }
            // An abraded section in the free span: its tension alone.
            for (int w = 0; w < _wornCount; w++)
            {
                int slot = _wornList[w];
                double s = (_key[slot] + 0.5) * _sectionLength;
                for (int j = 0; j < n - 1; j++)
                {
                    if (!(s >= chain[j].Material && s < chain[j + 1].Material)) continue;
                    double t = chain.Tension(j);
                    if (!(t / _glassArea >= _wornStrength[slot])) break;
                    double f = (s - chain[j].Material) / (chain[j + 1].Material - chain[j].Material);
                    b.Mechanism = TetherMechanism.Abrasion;
                    b.Kind = (byte)FiberMember.Span;
                    b.Segment = j;
                    b.Fraction = f;
                    b.Node = j + 1 < n - 1 ? j + 1 : -1;
                    b.Material = s;
                    b.Tension = t;
                    b.Radius = double.PositiveInfinity;
                    b.Stress = t / _glassArea;
                    b.Strength = _wornStrength[slot];
                    b.Band = SectionBand.Abraded;
                    b.Section = _key[slot];
                    b.Point = chain[j].Position + (chain[j + 1].Position - chain[j].Position) * f;
                    return true;
                }
            }

            // The pack's peel front, while line flows.
            if (paying)
            {
                long sec = Section(peelMaterial);
                double strength = Strength(sec, out SectionBand band), stress = PeelStress(peelEnergy, packTension);
                var peel = new FiberBend { Kind = (byte)FiberMember.Peel, Stress = stress, Point = chain[n - 1].Position };
                Read(sec, strength, band, peel, true, names, events);
                if (stress >= strength)
                {
                    b.Mechanism = TetherMechanism.Peel;
                    b.Kind = (byte)FiberMember.Peel;
                    b.Node = -1;
                    b.Material = peelMaterial;
                    b.Turn = 0;
                    b.Tension = packTension;
                    b.Radius = DetMath.Sqrt(_bending / (2 * peelEnergy));
                    b.Stress = stress;
                    b.Strength = strength;
                    b.Band = band;
                    b.Section = sec;
                    b.Point = chain[n - 1].Position;
                    return true;
                }
            }
            return false;
        }

        // A section's read for the log: at a contact its first; at the lip or the peel its first past the near-miss share.
        void Read(long sec, double strength, SectionBand band, in FiberBend p, bool passing, FiberContacts names, StepEvents events)
        {
            if (names == null || passing && !(p.Stress >= NearMiss * strength)) return;
            int slot = Slot(sec, events, p.Point);
            if (slot < 0 || _logged[slot]) return;
            _logged[slot] = true;
            events.Add(SectionEvent(names, p.Kind, p.Member, p.Point, sec, strength, band, p.Stress, false));
        }

        /// <summary>FiberSection for section sec: Index its band (| 0x80 at a break), the member named, A the section's start s (m),
        /// B its strength (Pa), C the stress it met (Pa).</summary>
        public FlightEvent SectionEvent(FiberContacts names, byte kind, ulong member, Double3 point, long sec, double strength, SectionBand band,
            double stress, bool atBreak)
        {
            // Without the fiber's contacts (FlightSetup.TetherWithoutContacts) there are no names: the place as FiberLine.Place
            // writes it (model 60; at 56–59 a break there by tension, bend or abrasion threw).
            FlightEvent e = kind <= (byte)FiberMember.Part && names != null
                ? names.Named(EventKind.FiberSection, kind, member, point, sec * _sectionLength, strength, stress)
                : FiberLine.Place(EventKind.FiberSection, kind, member, names, point, sec * _sectionLength, strength, stress);
            e.Index = (byte)((byte)band | (atBreak ? 0x80 : 0));
            return e;
        }

        // ---- the table ----

        int Find(long k)
        {
            int h = (int)(FlightRandom.Mix64((ulong)k) & (Capacity - 1));
            for (int probe = 0; probe < Capacity; probe++)
            {
                int i = (h + probe) & (Capacity - 1);
                if (!_used[i]) return -1;
                if (_key[i] == k) return i;
            }
            return -1;
        }

        // Section k's slot, made on first use; −1 when the table is full (a WorldBug, once).
        int Slot(long k, StepEvents events, Double3 point)
        {
            int h = (int)(FlightRandom.Mix64((ulong)k) & (Capacity - 1));
            for (int probe = 0; probe < Capacity; probe++)
            {
                int i = (h + probe) & (Capacity - 1);
                if (_used[i])
                {
                    if (_key[i] == k) return i;
                    continue;
                }
                if (_count >= Capacity * 3 / 4) break; // the load factor's bound keeps the probes short (numerical)
                _used[i] = true;
                _key[i] = k;
                _count++;
                return i;
            }
            if (!_full)
            {
                _full = true;
                FlightEvent bug = FlightEvent.Of(EventKind.WorldBug);
                bug.Index = (byte)WorldBugCause.FiberSectionsFull;
                bug.A = Capacity;
                bug.Point = point;
                events.Add(bug);
            }
            return -1;
        }

        /// <summary>Sections in the table (read at a contact, worn, or near a miss), and those worn through.</summary>
        public int Sections => _count;
        public int WornCount => _wornCount;
    }
}
