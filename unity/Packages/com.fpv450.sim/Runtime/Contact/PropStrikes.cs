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
    ///   props.blade's) is a hard target and the blade yields; a softer one is carved. The ground and the soil (no row until
    ///   p7a-ground's surfaces.json), a thin compliant row (no crush strength: p7a's elements and p7d's fabric and mesh), and a
    ///   material the table lacks take no blade damage here: the pass is logged (Glance) and does no work.
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
    /// - A soft face: the blade carves it at its crush pressure p over the volume it sweeps, its frontal height (c·sin θ +
    ///   t·cos θ) times the area it sweeps inside (the circular segment past r_m for a face past the tip, the disc across its
    ///   radial chord for a shape inside the rim), and flings the mass it meets, Σ ρ·dV·(ω·r)² (closed form over the area):
    ///   E = p·V + m_f·v². The cutting force E over the path inside bends the inner stations edgewise; the blade is whole unless
    ///   one breaks. The world is not changed (assumed: a carved groove is not stored).
    /// - Each pass's work E leaves the rotor in the step, ω' = √(ω² − 2E/J); with E ≥ ½Jω² the rotor stops on the face
    ///   (RotorJammed), held at the face's entry, its damage cut short by the energy it had: a crush reaches that share of its
    ///   depth, a break does not happen, a carve keeps the rest of its work, which the motor's spin-up pays step by step until
    ///   the blade is through (the blade held where the work has carried it).
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
    /// </summary>
    public sealed class PropStrikes
    {
        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;
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
        readonly double[] _strength;
        readonly double[] _area, _flat, _edge, _stiff, _mass; // per station, the whole chord: A, M_f, M_e, EI_f/span, mass
        readonly Pending[] _pending = new Pending[4];

        /// <summary>A rotor stopped in a carve: the work still to do and where it is (design 6).</summary>
        struct Pending
        {
            public bool Active;
            public int Object, Shape, Blade;
            public double Left, Work, Entry, Psi;
        }

        /// <summary>The last pass, for a test: its rotor, blade and mode, the meeting radius, the work and the rotor's speeds
        /// around it, the rotor's J, the face's impulse on the drone (world N·s) at its point and the reaction about the axis
        /// (body N·m·s), the blade's tip before and after (r/R), the speed into the face and the forces the mode decided on; Grind
        /// for a stopped carve's step (its Work the pass's whole work, Used this step's share).</summary>
        public struct PassRecord
        {
            public int Corner, Blade;
            public StrikeMode Mode;
            public double Radius, Exit, Work, Used, SpeedBefore, SpeedAfter, Inertia, TipBefore, TipAfter, SpeedIn, Tilt;
            public double CrushForce, BendForce, PeakForce, PeakMoment, Margin, Wear, Friction, Fling;
            public int Station;
            public bool Jammed, Grind;
            public Double3 Impulse, Point, Normal, Reaction;
        }

        public PassRecord Last;

        /// <summary>Every pass's record when set (a test's or a bench's; null in a flight, which then allocates nothing).</summary>
        public System.Collections.Generic.List<PassRecord> Record;

        /// <summary>The passes so far and the work they took, J.</summary>
        public long Passes { get; private set; }

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
                if (_spec.Shapes[i].Role == ContactRole.Disc) _discs[k++] = i;

            // The world's materials by id, as the contacts read them.
            IWorldQuery q = world.Query;
            int top = 0;
            for (int id = 1; id < ushort.MaxValue; id++)
                if (q.MaterialName((ushort)id) != null) top = id;
            _rows = new MaterialRow[top + 1];
            _known = new bool[top + 1];
            for (int id = 1; id <= top; id++)
            {
                string name = q.MaterialName((ushort)id);
                if (name != null && _spec.Row(name, out MaterialRow r))
                {
                    _rows[id] = r;
                    _known[id] = true;
                }
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
            int changed = 0;
            for (int n = 0; n < _discs.Length; n++)
            {
                int i = _discs[n];
                ContactShape d = _spec.Shapes[i];
                int c = d.Corner, pairs = _contacts.DiscPairs(i);
                if (pairs == 0)
                {
                    _pending[c].Active = false;
                    continue;
                }
                Double3 hub = now.Position + now.Attitude.Rotate(d.A);
                double start = from[c], sweep = now.RotorAngle[c] - start;
                if (sweep < 0) sweep += TwoPi;
                int skipObject = int.MinValue, skipShape = 0, skipBlade = -1;
                if (_pending[c].Active)
                {
                    skipObject = _pending[c].Object;
                    skipShape = _pending[c].Shape;
                    skipBlade = _pending[c].Blade;
                    Grind(i, d, c, ref now, body, hub, axis, events, ref impulse, ref angular);
                }
                for (int k = 0; k < pairs && now.RotorSpeed[c] > 0; k++)
                {
                    _contacts.DiscPair(i, k, out int obj, out int shape, out ushort material, out byte surface);
                    if (!Geometry(obj, shape, hub, d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)) continue;
                    if (!BladeCut.Nearest(g, chain, hub, axis, d.HalfThickness, d.Radius, out BladeCut.Meeting m)) continue;
                    double psi = Azimuth(now.Attitude, m.Direction, c);
                    for (int b = 0; b < _blades && now.RotorSpeed[c] > 0; b++)
                    {
                        if (obj == skipObject && shape == skipShape && b == skipBlade) continue;
                        double ahead = Wrap(psi - (start + TwoPi * b / _blades));
                        if (!(ahead > 0 && ahead <= sweep)) continue;
                        if (Pass(i, d, c, b, ref now, body, obj, shape, material, surface, g, chain, hub, axis, m, psi, events, ref impulse, ref angular))
                            changed |= 1 << c;
                    }
                }
            }
            if (impulse.X != 0 || impulse.Y != 0 || impulse.Z != 0 || angular.X != 0 || angular.Y != 0 || angular.Z != 0)
            {
                now.Velocity = now.Velocity + impulse * body.InverseMass;
                now.BodyRate = now.BodyRate + body.InverseInertia * angular;
            }
            for (int c = 0; c < 4; c++)
                if ((changed & (1 << c)) != 0) _propulsion.Rebuild(c);
        }

        // One blade's pass across a pair; true when it changed the blade.
        bool Pass(int i, in ContactShape d, int c, int b, ref FlightState now, in MassProperties body, int obj, int shape, ushort material, byte surface,
            in ShapeGeometry g, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, in BladeCut.Meeting m, double psi, StepEvents events,
            ref Double3 impulse, ref Double3 angular)
        {
            BladeState blade = _propulsion.Rotor(c).Blade(b);
            double R = _radius, tip = blade.TipRadius, rtip = tip * R, rm = m.Radius;
            if (!(rm < rtip)) return false; // the blade, cut back, no longer reaches it
            double omega = now.RotorSpeed[c], J = _propulsion.Inertia(c), s = _spin[c], f = _strength[c * _blades + b];
            Double3 dir = m.Direction, nrm = m.Normal, motion = Double3.Cross(axis, dir) * -s;
            Double3 at = PointVelocity(now, m.Point);
            var rec = new PassRecord
            {
                Corner = c, Blade = b, Radius = rm, Exit = double.PositiveInfinity, SpeedBefore = omega, Inertia = J, TipBefore = tip, TipAfter = tip,
                Point = m.Point, Normal = nrm, Station = -1, Tilt = DetMath.Abs(Double3.Dot(nrm, axis)),
            };
            bool known = surface == WorldIds.NoSurface && obj != WorldIds.Ground && material < _known.Length && _known[material];
            MaterialRow row = known ? _rows[material] : default;
            if (!known || !(row.CrushStrength > 0))
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
            bool inside = rout < rtip, hard = row.Hardness > _b.Hardness;
            rec.Exit = rout;
            double mu = row.KineticFriction, crush = _b.CrushStrength * f;
            double work, pushN = 0, axial = 0, rc = rm, alpha, newTip = tip;
            int broke = -1, set = -1;
            double phi = 0, wear = 0;
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
                DetMath.SinCos(theta, out double st, out double ct);
                double height = chord * st + _b.Thickness[k] * ct;
                double fling = row.Density * height * omega * omega * second;
                work = row.CrushStrength * area * height + fling;
                rec.Fling = fling;
                double cut = path > 0 ? work / path : 0;
                rec.PeakForce = cut;
                broke = Breaks(blade, f, rm, rc, cut, 0, out rec.PeakMoment, out rec.Margin);
                if (broke >= 0)
                {
                    mode = StrikeMode.Break;
                    newTip = _b.Stations[broke];
                    work += _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
                }
                axial = ct / (st > 1e-12 ? st : 1e-12); // cot θ: the pitched face's push per tangential impulse
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

            // The rotor's energy: a dip, or a stop on the face with the damage cut short (design 6).
            double energy = 0.5 * J * omega * omega, share = 1, after;
            bool jam = !(work < energy);
            if (jam)
            {
                share = work > 0 ? energy / work : 1;
                after = 0;
                if (mode == StrikeMode.Crush) newTip = tip - share * (tip - newTip);
                if (mode == StrikeMode.Break)
                {
                    // Short of the fracture's work the section holds: the blade presses the face, crushing that share of its depth.
                    mode = StrikeMode.Crush;
                    newTip = hard && !inside ? tip - share * (tip - rm / R) : tip;
                    broke = -1;
                }
                phi *= share;
                wear *= share;
                if (mode == StrikeMode.Carve)
                {
                    _pending[c] = new Pending
                    {
                        Active = true, Object = obj, Shape = shape, Blade = b, Left = work - energy, Work = work, Entry = psi - alpha, Psi = psi,
                    };
                }
            }
            else after = DetMath.Sqrt(omega * omega - 2 * work / J);
            double used = jam ? energy : work;

            // The impulses: the reaction about the axis, the tangential one at the meeting point, the face's push.
            double lost = J * (omega - after), tangential = rc > 0 ? lost / rc : 0;
            Double3 external = motion * -tangential + nrm * (pushN * share) + axis * (tangential * axial);
            Double3 reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(m.Point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            now.RotorSpeed[c] = after;
            if (jam) Hold(ref now, c, b, mode == StrikeMode.Carve ? psi - alpha + alpha * share : psi - Entry(rm, newTip * R, alpha, inside));

            // The damage.
            double before = Mass(blade);
            bool damaged = false;
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
                e.A = work;
                e.B = energy;
                e.C = share;
                events.Add(e);
            }
            Slot(ref now, c, b, material, used, external.Length(), rm);
            Last = rec;
            Record?.Add(rec);
            Passes++;
            return damaged;
        }

        // A rotor stopped in a carve: the motor's spin-up pays the work still to do (design 6).
        void Grind(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, StepEvents events,
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
            bool still = at >= 0 && Geometry(p.Object, p.Shape, hub, d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)
                && BladeCut.Nearest(g, chain, hub, axis, d.HalfThickness, d.Radius, out m) && m.Radius < blade.TipRadius * _radius;
            if (!still)
            {
                p.Active = false;
                return;
            }
            double omega = now.RotorSpeed[c], J = _propulsion.Inertia(c), s = _spin[c], energy = 0.5 * J * omega * omega;
            double used = DetMath.Min(energy, p.Left), after = energy > p.Left ? DetMath.Sqrt(omega * omega - 2 * p.Left / J) : 0;
            p.Left -= used;
            double rc = 0.5 * (m.Radius + blade.TipRadius * _radius);
            int ks = Station(rc / _radius);
            double theta = _b.SectionAngle[ks] + blade.FactoryPitch + blade.PitchSet[ks];
            DetMath.SinCos(theta, out double st, out double ct);
            Double3 motion = Double3.Cross(axis, m.Direction) * -s;
            double lost = J * (omega - after), tangential = lost / rc;
            Double3 external = motion * -tangential + axis * (tangential * ct / (st > 1e-12 ? st : 1e-12));
            Double3 reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(m.Point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            now.RotorSpeed[c] = after;
            bool through = !(p.Left > 0);
            Hold(ref now, c, p.Blade, through ? p.Psi : p.Entry + (p.Psi - p.Entry) * (1 - p.Left / p.Work));
            if (through) p.Active = false;
            var rec = new PassRecord
            {
                Corner = c, Blade = p.Blade, Mode = StrikeMode.Carve, Radius = m.Radius, Exit = double.PositiveInfinity, Work = p.Work, Used = used,
                SpeedBefore = omega, SpeedAfter = after, Inertia = J, TipBefore = blade.TipRadius, TipAfter = blade.TipRadius, Point = m.Point,
                Normal = m.Normal, Impulse = external, Reaction = reaction, Jammed = !through, Station = -1, Grind = true,
            };
            Strike(i, d, p.Blade, p.Object, p.Shape, material, surface, rec, events);
            Slot(ref now, c, p.Blade, material, used, external.Length(), m.Radius);
            Last = rec;
            Record?.Add(rec);
            Passes++;
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
                if (!(x < rm) || !(blade.Inside(_b.Stations[k], _span) > 0)) continue;
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

        bool Geometry(int obj, int shape, Double3 hub, in ContactShape d, out ShapeGeometry g, out ReadOnlySpan<Double3> chain)
        {
            chain = default;
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
