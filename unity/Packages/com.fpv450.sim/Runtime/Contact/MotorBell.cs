using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The motors' bells (p7f-damage 1.5, design 8 and 10; drone.json's motors[].internals), for the contact model: what a contact
    /// on a motor's turning band does to its rotor and its motor. Engine-free, allocation-free in a step.
    /// - The band: a motor sphere's contact whose point lies between the internals' bell_band_m heights above the motor's base
    ///   (the hub less hub_height_m) is on the bell, the can of radius R (the motor sphere's, the part's Ø37.2 mm, published), which
    ///   turns with the rotor.
    /// - The scrape: while the rotor turns the bell's surface moves at ω·R along its turn, so the pair slides on the bell's
    ///   velocity relative to the face (no stick anchor: ContactModel), its kinetic friction μ_k·F_n against that slip; the rotor
    ///   feels its moment about the shaft, R·(f·ê_t), against its turn, and the drone the force, pushed along the face as a
    ///   spinning wheel is. μ_k is the face's row (assumed: physics_materials.json's rows are the drone's polymer parts on the
    ///   material; no aluminium pairing is published per row). At rest the pair keeps its anchor, and the rotor is held by its
    ///   static friction's moment μ_s·F_n·R until the drive passes it.
    /// - The ring: the band as a thin ring of the can and the back iron (Σ of their sections, unbonded, a lower bound), b the
    ///   band's height: its radial stiffness under opposed point loads k_r = Σ E·b·t³/12 / (0.149·R³) (Roark's, Young and Budynas
    ///   2002, published) and its plastic collapse load P_c = 4·M_p/R, M_p = Σ σ_y·b·t²/4 (DeRuntz and Hodge 1963, a ring
    ///   crushed between plates; the single load against the shaft's support taken as equivalent, assumed). Behind the band is
    ///   the stator between stator_band_m's heights, across the gap g, and the windings' end turns elsewhere (assumed at the
    ///   teeth's radius, so the clearance R − t_can − t_iron − r_stator, derived: 1.8 mm). A contact's load F bends the ring
    ///   inward by F/k_r until it meets what is behind it; past that what is behind carries the rest (the stator's teeth through
    ///   the magnets; the end turns, 1.6b's insulation). With the class's numbers k_r·g (137 N) is far below P_c (524 N): a side
    ///   hit at the stator's height never collapses the ring, it closes the gap and presses the magnets onto the teeth with
    ///   F − k_r·g, which rub while the rotor turns (the transient seize). Above or below the stator the ring is free to the end
    ///   turns' clearance (k_r times it 821 N), so a hit there past P_c dents it.
    /// - The dent: past P_c the ring collapses and caps the contact's force there, so the approach the pair's spring read past
    ///   it is the dent, δ += (F − P_c)/k_n with k_n the pair's normal stiffness (derived: design 8's k_h, which it assumed at a
    ///   tenth of k_r, follows from the collapse capping the force), at most to the clearance; P_c rises to F (work hardening,
    ///   assumed). Logged MotorDamaged. Each band keeps its deepest dent (assumed: a later hit deepens it; a second site's rub
    ///   would add to the first's).
    /// - The gap and Kt: a dent in the stator's band narrows the gap over BellDentArc of the circumference to g − δ; the
    ///   surface magnet's air-gap flux B = B_r/(1 + μ_r·g/l_m) (Hanselman 2006, published form) over the arc and the rest gives
    ///   Kt (= Ke) its ratio to new (derived).
    /// - The rub: a dent past the gap presses the magnets onto the teeth with N = k_r·(δ − g − w) (Roark's, the ring's own
    ///   spring; w what the rub has worn), which with the seize's drags the rotor by BellRubFriction·N·r_stator. It wears both
    ///   by Archard, V = k·N·s/H on each (s the sliding distance ω·r_stator·dt): the magnets over the dent's arc of the stator's
    ///   height, the teeth over the stator's whole face, so the interference falls by both depths and the magnet thins by its
    ///   own (l_m down, so B and Kt down at the dent): a dented motor runs rough and weak, and grinds in. The seize's wear (a
    ///   micron in a crash's few milliseconds) is not kept (assumed).
    /// - Binding: the drag goes to Propulsion.SetMotor with the Kt ratio, so a drag the drive cannot pass holds the rotor at rest
    ///   (Propulsion.Bound, FlightCore's MotorBound); it turns again only when the drive passes it.
    /// The seams 1.6 and 1.6b act on: the gap's interference (DentGap, Worn), the magnet (MagnetThickness), the Kt ratio and the
    /// drag passed to Propulsion.
    /// - The shaft (p7f-damage 1.6, MotorShaft): a bent shaft swings the magnets toward the stator by its run-out (Closure), which
    ///   past the gap rubs as a dent does (the deeper of the two sites, assumed; the rub's wear off both), and the brinelled
    ///   bearings' drag joins the bell's (MotorShaft.BearingDrag). A straight shaft on sound bearings adds nothing, bit for bit.
    /// </summary>
    public sealed class MotorBell
    {
        /// <summary>Roark's coefficient for a thin ring's change of diameter under opposed point loads, δ = 0.149·P·R³/(EI)
        /// (Young and Budynas 2002, published).</summary>
        public const double RoarkRing = 0.149;

        const double TwoPi = 6.283185307179586;

        readonly ContactSpec _spec;
        readonly Propulsion _propulsion;
        readonly int[] _shape = { -1, -1, -1, -1 }, _part = new int[4];
        readonly double[] _hubX = new double[4], _hubZ = new double[4], _sign = new double[4];
        readonly double[] _from = new double[4], _to = new double[4], _statorFrom = new double[4], _statorTo = new double[4];
        readonly double[] _radius = new double[4], _statorRadius = new double[4], _ring = new double[4], _collapse = new double[4];
        readonly double[] _gap = new double[4], _magnet = new double[4], _permeability = new double[4], _clearance = new double[4];
        readonly double[] _dentArea = new double[4], _statorArea = new double[4];

        // The state: each band's deepest dent (m) and its collapse load now (N), what the rub has worn off the gap's interference
        // and off the magnet (m), and the Kt ratio.
        readonly double[] _dentGap = new double[4], _dentFree = new double[4], _collapseGap = new double[4], _collapseFree = new double[4];
        readonly double[] _worn = new double[4], _magnetWorn = new double[4], _kt = { 1, 1, 1, 1 };

        // A bent shaft's run-out of the magnets at the stator (m, MotorShaft), and the shafts (null: none).
        readonly double[] _closure = new double[4];

        /// <summary>The motors' shafts and bearings (p7f-damage 1.6), or null: their run-out and their drag join the bell's.</summary>
        public MotorShaft Shaft { get; set; }

        // The step: each bell's normal load (N), its scrape's moment on the rotor turning (N·m) or its hold at rest, the magnets'
        // press onto the teeth (N), and the rub's force and the drag passed.
        readonly double[] _normal = new double[4], _scrape = new double[4], _press = new double[4], _rub = new double[4], _drag = new double[4];
        readonly bool[] _turning = new bool[4];

        /// <summary>The bells of spec's motors (ContactSpec.Motors) on its shapes (each corner's "motor_rr", … sphere and its disc's
        /// hub), turning with propulsion's rotors in its motors' sense.</summary>
        public MotorBell(ContactSpec spec, ContactShape[] shapes, Propulsion propulsion, PropulsionSpec motors)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _propulsion = propulsion ?? throw new ArgumentNullException(nameof(propulsion));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("the bells need the four motors' internals", nameof(spec));
            string[] ids = { "rr", "fr", "rl", "fl" };
            for (int i = 0; i < shapes.Length; i++)
                for (int c = 0; c < 4; c++)
                    if (shapes[i].Role == ContactRole.Body && shapes[i].Name == "motor_" + ids[c])
                    {
                        _shape[c] = i;
                        _part[c] = shapes[i].Part;
                    }
            for (int c = 0; c < 4; c++)
            {
                if (_shape[c] < 0) throw new ArgumentException("no motor_" + ids[c] + " shape for its bell", nameof(shapes));
                MotorInternals m = spec.Motors[c];
                Double3 hub = motors.Motors[c].Position;
                _hubX[c] = hub.X;
                _hubZ[c] = hub.Z;
                _sign[c] = motors.Motors[c].Clockwise ? -1 : 1; // the bell's turn in ContactModel's sense (its blades')
                double baseY = hub.Y - m.HubHeight;
                _from[c] = baseY + m.BellFrom;
                _to[c] = baseY + m.BellTo;
                _statorFrom[c] = baseY + m.StatorFrom;
                _statorTo[c] = baseY + m.StatorTo;
                double R = shapes[_shape[c]].Radius, b = m.BellTo - m.BellFrom, tc = m.CanThickness, tb = m.BackIronThickness;
                _radius[c] = R;
                _statorRadius[c] = m.StatorDiameter / 2;
                _ring[c] = (m.CanModulus * tc * tc * tc + m.BackIronModulus * tb * tb * tb) * b / 12 / (RoarkRing * R * R * R);
                _collapse[c] = _collapseGap[c] = _collapseFree[c] = (m.CanYield * tc * tc + m.BackIronYield * tb * tb) * b / R;
                _gap[c] = m.Gap;
                _magnet[c] = m.MagnetThickness;
                _permeability[c] = m.MagnetPermeability;
                _clearance[c] = R - tc - tb - m.StatorDiameter / 2;
                double face = TwoPi * _statorRadius[c] * (m.StatorTo - m.StatorFrom);
                _statorArea[c] = face;
                _dentArea[c] = spec.BellDentArc * face;
            }
        }

        /// <summary>Corner c's motor shape (its contact shape's index) and part.</summary>
        public int Shape(int corner) => _shape[corner];
        public int Part(int corner) => _part[corner];

        /// <summary>The can's radius R, the stator's, the ring's k_r (N/m) and its collapse load P_c as built (N), the gap g and the
        /// end turns' clearance (m).</summary>
        public double Radius(int corner) => _radius[corner];
        public double StatorRadius(int corner) => _statorRadius[corner];
        public double RingStiffness(int corner) => _ring[corner];
        public double CollapseLoad(int corner) => _collapse[corner];
        public double Gap(int corner) => _gap[corner];
        public double Clearance(int corner) => _clearance[corner];

        /// <summary>The deepest dent in the stator's band and outside it (m), the rub's wear off the gap's interference and off
        /// the magnet (m), the magnet's thickness at the dent now and the gap's interference past it (m), and the Kt ratio.</summary>
        public double DentGap(int corner) => _dentGap[corner];
        public double DentFree(int corner) => _dentFree[corner];
        public double Worn(int corner) => _worn[corner];
        public double MagnetThickness(int corner) => _magnet[corner] - _magnetWorn[corner];
        public double Interference(int corner) =>
            DetMath.Max(DetMath.Max(_dentGap[corner], _closure[corner]) - _worn[corner] - _gap[corner], 0);

        /// <summary>A bent shaft's run-out of the magnets toward the stator, m (MotorShaft: its bend times the stator's farthest
        /// height from the hinge): past the gap it rubs as a dent's interference does.</summary>
        public void Closure(int corner, double closure) => _closure[corner] = closure;
        public double RunOut(int corner) => _closure[corner];
        public double KtRatio(int corner) => _kt[corner];

        /// <summary>The last step's normal load on the bell (N), its scrape's moment on the turning rotor or its hold at rest (N·m),
        /// the magnets' press onto the teeth under the load (N), the rub's whole force on the teeth (N) and the drag passed (N·m).</summary>
        public double Normal(int corner) => _normal[corner];
        public double Scrape(int corner) => _scrape[corner];
        public double Press(int corner) => _press[corner];
        public double Rub(int corner) => _rub[corner];
        public double Drag(int corner) => _drag[corner];

        /// <summary>A dent before the first step (the scenarios' damaged builds; a flight never calls it, and its log would not
        /// replay): the stator band's dent, m, its Kt from the next step.</summary>
        public void PresetDent(int corner, double depth)
        {
            _dentGap[corner] = depth;
            Flux(corner);
        }

        /// <summary>
        /// Whether a contact at point (body m) on corner c's motor sphere is on its turning band; then the bell's direction of motion
        /// there (body, unit: its turn about +y in the rotor's sense) and whether the stator lies behind it.
        /// </summary>
        public bool OnBand(int corner, Double3 point, out Double3 motion, out bool stator)
        {
            motion = default;
            stator = false;
            if (point.Y < _from[corner] || point.Y > _to[corner]) return false;
            double x = point.X - _hubX[corner], z = point.Z - _hubZ[corner], r = DetMath.Sqrt(x * x + z * z);
            if (!(r > 0)) return false;
            motion = new Double3(-z / r, 0, x / r) * _sign[corner];
            stator = point.Y >= _statorFrom[corner] && point.Y <= _statorTo[corner];
            return true;
        }

        /// <summary>The step's sums cleared (ContactModel.Forces, before the body shapes).</summary>
        public void Begin(in FlightState s)
        {
            for (int c = 0; c < 4; c++)
            {
                _normal[c] = _scrape[c] = _press[c] = 0;
                _turning[c] = s.RotorSpeed[c] > 0;
            }
        }

        /// <summary>Whether corner c's rotor turns at the step's start (its bell slides on what it touches).</summary>
        public bool Turning(int corner) => _turning[corner];

        /// <summary>
        /// One bell pair's load this step: its normal force fn (N) and friction (body N), the bell's motion there, whether the
        /// stator is behind it, the pair's normal stiffness kn (N/m) and the face's static friction. Its scrape (turning) or hold
        /// (at rest) into the rotor's, the magnets' press past the gap, and the ring's collapse: true when the band dents, with
        /// the dent's depth after (m), the ring's load (N) and the collapse load it passed (N).
        /// </summary>
        public bool Pair(int c, double fn, Double3 friction, Double3 motion, bool stator, double kn, double staticFriction,
            out double depth, out double load, out double passed)
        {
            double R = _radius[c];
            _normal[c] += fn;
            _scrape[c] += _turning[c] ? -R * Double3.Dot(friction, motion) : staticFriction * fn * R;
            depth = load = passed = 0;
            if (stator)
            {
                // The gap closes at k_r·g and the teeth carry the rest; the ring's own load stops there.
                double close = _ring[c] * _gap[c];
                if (fn > close) _press[c] += fn - close;
                return Collapse(c, DetMath.Min(fn, close), kn, _gap[c], ref _dentGap[c], ref _collapseGap[c], out depth, out load, out passed);
            }
            double room = _ring[c] * (_clearance[c] - _dentFree[c]);
            return Collapse(c, DetMath.Min(fn, room > 0 ? room : 0), kn, _clearance[c], ref _dentFree[c], ref _collapseFree[c], out depth, out load, out passed);
        }

        // The ring at a band under its load F: past its collapse load it dents by (F − P_c)/k_n to at most the clearance behind it,
        // and P_c rises to F.
        bool Collapse(int c, double f, double kn, double limit, ref double dent, ref double collapse, out double depth, out double load, out double passed)
        {
            depth = dent;
            load = f;
            passed = collapse;
            if (!(f > collapse) || !(dent < limit)) return false;
            dent = DetMath.Min(dent + (f - collapse) / kn, limit);
            collapse = f;
            depth = dent;
            Flux(c);
            return true;
        }

        /// <summary>
        /// The step's bells into the rotors (ContactModel.Forces, after the body shapes): each motor's rub on the teeth (the
        /// press and the dent's interference), its drag (the scrape and the rub, turning; the holds at rest) and Kt ratio to
        /// Propulsion.SetMotor for the next step's sub-steps; the rub's wear over the step of dt seconds at the rotor's speed;
        /// the trace's bell fields.
        /// </summary>
        public void Finish(in FlightState s, double dt, ref StepTrace trace)
        {
            for (int c = 0; c < 4; c++)
            {
                double interference = Interference(c), dentForce = _ring[c] * interference;
                double rub = _press[c] + dentForce, rs = _statorRadius[c], mu = _spec.BellRubFriction;
                _rub[c] = rub;
                double rubTorque = mu * rub * rs;
                _drag[c] = _scrape[c] + rubTorque;
                if (Shaft != null)
                {
                    double bearings = Shaft.BearingDrag(c, _propulsion.Thrust(c));
                    if (bearings != 0) _drag[c] += bearings;
                }
                if (_turning[c] && dentForce > 0)
                {
                    // Archard on both faces over the step's slide; the interference falls by both depths, the magnet by its own.
                    double slide = s.RotorSpeed[c] * rs * dt, work = _spec.BellWearCoefficient * dentForce * slide;
                    double magnet = work / _spec.MagnetHardness / _dentArea[c], teeth = work / _spec.StatorHardness / _statorArea[c];
                    _worn[c] += magnet + teeth;
                    _magnetWorn[c] += magnet;
                    Flux(c);
                }
                _propulsion.SetMotor(c, _kt[c], _drag[c]);
                trace.BellScrape[c] = _scrape[c];
                trace.BellRub[c] = rubTorque;
                trace.BellKtChange[c] = _kt[c] - 1; // exact (Sterbenz): a sound gap logs 0
            }
        }

        // The Kt ratio from the gap's flux: the dent's arc at its gap g − (δ − w) (0 past it) and its worn magnet, the rest new.
        void Flux(int c)
        {
            double g = _gap[c], l0 = _magnet[c], mu = _permeability[c], a = _spec.BellDentArc;
            double gd = DetMath.Max(g - (_dentGap[c] - _worn[c]), 0), ld = l0 - _magnetWorn[c];
            _kt[c] = a * ((1 + mu * g / l0) / (1 + mu * gd / ld)) + (1 - a);
        }
    }
}
