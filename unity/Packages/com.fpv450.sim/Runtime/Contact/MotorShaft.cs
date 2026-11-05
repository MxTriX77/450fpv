using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The motors' shafts and bearings (p7f-damage 1.6, design 9 and 11; drone.json's motors[].internals), beside the bells
    /// (MotorBell): what the loads on a rotor do to its shaft and its two bearings. Engine-free, allocation-free in a step.
    /// - The loads: a blade pass's face impulse over its contact time at the meeting point (PropStrikes, Strike: design 9's "the
    ///   pass's impulse over t_c", t_c the pass's arc over ω held to the step as its push is; at most the pass's own peak force on
    ///   the blade, its crush, slap or cut, which a jam's impulse over the arc would pass, a break's the force its section failed
    ///   at), and each step's contact forces on the rotor (ContactModel, Add): a resting prop's blades' (1.3b) and the bell's ring's share of a bell pair's (1.5: past
    ///   the gap the stator's teeth carry the rest, so a side hit at the stator's height loads the shaft with at most k_r·g).
    ///   Each, at its point, gives the shaft's moment at the upper bearing M = (P − B) × F less its part along the axis (that is
    ///   the drive's), and by statics on the two bearings s apart the lower one's radial load R₂ = (M × ŷ)/s and the upper one's
    ///   R₁ = −F_∥ − R₂ (derived: a side force at the hub's lever l gives M = F·l, R₁ = F·(1 + l/s), R₂ = F·l/s, design 9). A
    ///   blade passes the hub at most its root's flatwise strength (form·σ_f·c·t²/6 at the first station, nominal): past it the
    ///   blade breaks (PropStrikes), so the part of M a blade's own bending makes is capped there; and along its span toward the
    ///   shaft at most its column (ColumnLoad: its centrifugal tension at the root and its Euler load; past them the span goes
    ///   into compression and the blade flaps out of the disc), the whole force scaled with it (the face's friction follows its
    ///   normal force), which 1.3's crush on a face square to the span, σ_c·A(r_m) of 0.6–4 kN with its μ·F_n, and 1.3b's
    ///   resting blade, rigid along its span, would pass; and across the span in the disc at most what turns the rotor, by its
    ///   moment balance F_t·r = J·ω̇ + the drive: (the pass's lost angular momentum over its contact time + the motor's stalled
    ///   drive Kt·V/R)/r, which 1.3's push held to the step on a slowly turning blade (σ_c·A along a face across its motion)
    ///   would pass.
    /// - The bend: past the yield moment M_y = σ_y·πd³/32 (derived) the shaft takes a plastic hinge at the upper bearing,
    ///   Δφ = (M − M_y)/k_φ along M (k_φ = ShaftHingeShare·3EI/l, assumed), fixed in the rotating frame (the shaft turns with the
    ///   rotor), and M_y rises to M (work hardening, isotropic: assumed, as the bell's collapse load). Everything above the hinge
    ///   turns about it: the prop's hub moves φ·l and the bell's centre φ·l_b (their unbalance, U = (m_p·l + m_b·l_b)·(φ × ŷ),
    ///   its pull ω²·U, the bell's at its own height), the disc tilts by φ (the blades' centrifugal couple −ω²·Σ I_b·(φ·q_b)·q_b,
    ///   q_b across each blade in the disc, and the bell's −ω²·(I_p − I_d)·φ, derived), the thrust and the drag torque tilt with
    ///   it (an in-plane T·(φ × ŷ) at the hub, whose line passes the hinge, and Q·(φ × ŷ)): all into Propulsion.SetShaft. The
    ///   bell's magnets swing toward the stator by φ times their farthest height from the hinge, which past the gap rubs as a
    ///   dent's interference does (MotorBell.Closure).
    /// - The break: past the ultimate moment M_u = 16/(3π)·σ_u·πd³/32 (the round section's plastic shape factor, derived) the
    ///   shaft breaks at the upper bearing and the prop is lost (PropLost: thrust 0, J the bell's, Propulsion.LoseProp); the
    ///   magnets hold the bell on the stator (assumed, design 9), and the broken shaft takes no more load.
    /// - The bearings (685ZZ): past the static rating C0r a radial load brinells the races, δ = 1e-4·D_w·(R/C0r)² (ISO 76's
    ///   permanent deformation at C0, Palmgren's δ ∝ Q², derived), each bearing keeping its deepest; below C0r ISO 76's tolerable
    ///   deformation is taken as none (assumed). A dent's drag is SKF's ½·μ·P·d times BearingDentDrag·δ/(1e-4·D_w) (the sound
    ///   bearing's own friction is in the motor's no-load current), P each bearing's share of the thrust (assumed: the axial
    ///   load shared, the 1/rev radial loads left out). Its ball-pass lines, BPFO and BPFI = (n/2)·f_r·(1 ∓ D_w/d_p) (published
    ///   kinematics, contact angle 0), carry m_rot·(2πf)²·δ each; most lie above the step's 500 Hz Nyquist, so they never enter the
    ///   body: the camera mount's share is their RMS F/(√2·M) (the rigid body's translation, assumed: the frame's modes lie
    ///   below the lines) into CameraVibration's bands (BallPass), an output only.
    /// The seam 1.6b acts on: BearingDrag and Closure go to the bell's Finish with its own drag (heat and dirt add there), and
    /// the record's bend and drag (Trace).
    /// </summary>
    public sealed class MotorShaft
    {
        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;

        /// <summary>A solid round section's plastic shape factor Z_p/Z = 16/(3π) (derived; design 9's 1.7).</summary>
        public const double ShapeFactor = 16 / (3 * Pi);

        /// <summary>ISO 76's permanent deformation at the static rating C0, over the ball's diameter (published).</summary>
        public const double IsoDent = 1e-4;

        static readonly Double3 Up = new Double3(0, 1, 0);

        readonly ContactSpec _spec;
        readonly Propulsion _propulsion;
        readonly MotorBell _bells;
        readonly int[] _part = new int[4];
        readonly double[] _sign = new double[4];
        readonly Double3[] _hub = new Double3[4], _bearing = new Double3[4];
        readonly double[] _lever = new double[4], _span = new double[4], _yield = new double[4], _ultimate = new double[4], _hingeK = new double[4];
        readonly double[] _rating = new double[4], _ball = new double[4], _bore = new double[4], _outer = new double[4], _inner = new double[4];
        readonly double[] _bellMass = new double[4], _bellArm = new double[4], _bellCouple = new double[4], _reach = new double[4];
        readonly double _propMass, _rootCap, _bladeMoment, _rootFlexural;
        readonly double _kpp, _kpq, _kqq; // the blades' Σ I_b·q_b·q_bᵀ in the rotor's frame (p, q), kg·m²

        // The state: each shaft's bend in its rotor's frame (rad, a rotation vector's p and q), its hinge's yield moment now (N·m),
        // whether it broke; each bearing's dent (m) and the largest radial load it has passed (N), upper and lower.
        readonly double[] _bendP = new double[4], _bendQ = new double[4], _hinge = new double[4];
        readonly bool[] _broken = new bool[4];
        readonly double[] _dentUpper = new double[4], _dentLower = new double[4], _peakUpper = new double[4], _peakLower = new double[4];

        // The step's contact loads per rotor (Add): the force (body N), the moment at the upper bearing (body N·m), the largest
        // pair's force and its cause.
        readonly Double3[] _force = new Double3[4], _moment = new Double3[4];
        readonly double[] _largest = new double[4];
        readonly FlightEvent[] _cause = new FlightEvent[4];

        /// <summary>The shafts of spec's motors (ContactSpec.Motors) under propulsion's rotors (its PropulsionSpec.Rotor's blade), with
        /// the bells whose parts name them.</summary>
        public MotorShaft(ContactSpec spec, Propulsion propulsion, PropulsionSpec motors, MotorBell bells)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _propulsion = propulsion ?? throw new ArgumentNullException(nameof(propulsion));
            _bells = bells ?? throw new ArgumentNullException(nameof(bells));
            PropBlade blade = motors?.Rotor?.Blade ?? throw new ArgumentException("the shafts need the props' blade (PropulsionSpec.Rotor)", nameof(motors));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("the shafts need the four motors' internals", nameof(spec));
            double radius = motors.Props.Diameter / 2;
            int blades = motors.Props.Blades;
            _propMass = blades * blade.BladeMass(radius) + blade.HubMass;
            _rootCap = spec.SectionForm * blade.FlexuralStrength * blade.Chord[0] * blade.Thickness[0] * blade.Thickness[0] / 6;
            _rootFlexural = spec.SectionForm * blade.FlexuralModulus * blade.Chord[0] * blade.Thickness[0] * blade.Thickness[0] * blade.Thickness[0] / 12;
            for (int k = 0; k < blade.Stations.Length; k++)
                _bladeMoment += blade.Density * blade.Fill[k] * blade.Chord[k] * blade.Thickness[k] * radius * blade.Span * blade.Stations[k] * radius;
            // The blades' couple across each: blade b along (cos a_b, s·sin a_b) in (p, q) (DamagedRotor's azimuths), q_b = ê_b × ŷ =
            // (−s·sin a_b, cos a_b); its sign s drops out of the products but the cross one, which is 0 for two blades.
            double ib = blade.BladeInertia(radius);
            for (int b = 0; b < blades; b++)
            {
                DetMath.SinCos(TwoPi * b / blades, out double sin, out double cos);
                _kpp += ib * sin * sin;
                _kqq += ib * cos * cos;
                _kpq -= ib * sin * cos;
            }
            for (int c = 0; c < 4; c++)
            {
                MotorInternals m = spec.Motors[c];
                _part[c] = bells.Part(c);
                _sign[c] = motors.Motors[c].Clockwise ? -1 : 1; // Propulsion's reaction sign
                Double3 hub = motors.Motors[c].Position;
                _hub[c] = hub;
                _lever[c] = m.HubHeight - m.UpperBearingHeight;
                _bearing[c] = hub - Up * _lever[c];
                _span[c] = m.UpperBearingHeight - m.LowerBearingHeight;
                double d = m.ShaftDiameter, section = Pi * d * d * d / 32, inertia = Pi * d * d * d * d / 64;
                _yield[c] = _hinge[c] = m.ShaftYield * section;
                _ultimate[c] = ShapeFactor * m.ShaftUltimate * section;
                _hingeK[c] = spec.ShaftHingeShare * 3 * m.ShaftModulus * inertia / _lever[c];
                _rating[c] = m.BearingStaticRating;
                _ball[c] = m.BallDiameter;
                _bore[c] = d;
                double ratio = m.BallDiameter / m.PitchDiameter;
                _outer[c] = 0.5 * m.BearingBalls * (1 - ratio);
                _inner[c] = 0.5 * m.BearingBalls * (1 + ratio);
                _bellMass[c] = m.BellMass;
                _bellArm[c] = m.BellCentre - m.UpperBearingHeight;
                // The bell as a thin shell over its band at the back iron's inner face (assumed): I_p − I_d = m·(r²/2 − b²/12).
                double rb = bells.Radius(c) - m.CanThickness - m.BackIronThickness, band = m.BellTo - m.BellFrom;
                _bellCouple[c] = m.BellMass * (rb * rb / 2 - band * band / 12);
                // The magnets' farthest reach from the hinge along the stator's band.
                _reach[c] = DetMath.Max(DetMath.Abs(m.StatorFrom - m.UpperBearingHeight), DetMath.Abs(m.StatorTo - m.UpperBearingHeight));
            }
        }

        /// <summary>The shaft's yield moment as built and now, its ultimate (N·m), its hinge's stiffness (N·m/rad), the hub's lever over
        /// the upper bearing and the bearings' spacing (m), a blade's root cap (N·m).</summary>
        public double YieldMoment(int corner) => _yield[corner];
        public double HingeMoment(int corner) => _hinge[corner];
        public double UltimateMoment(int corner) => _ultimate[corner];
        public double HingeStiffness(int corner) => _hingeK[corner];
        public double Lever(int corner) => _lever[corner];
        public double Spacing(int corner) => _span[corner];
        public double RootCap => _rootCap;

        /// <summary>The shaft's bend in its rotor's frame (rad: the hinge's rotation vector along blade 0 and across it), its size, and
        /// whether it broke.</summary>
        public double BendP(int corner) => _bendP[corner];
        public double BendQ(int corner) => _bendQ[corner];
        public double Bend(int corner) => DetMath.Sqrt(_bendP[corner] * _bendP[corner] + _bendQ[corner] * _bendQ[corner]);
        public bool Broken(int corner) => _broken[corner];

        /// <summary>The upper and lower bearings' dents (m) and the largest radial loads they passed (N).</summary>
        public double DentUpper(int corner) => _dentUpper[corner];
        public double DentLower(int corner) => _dentLower[corner];
        public double PeakUpper(int corner) => _peakUpper[corner];
        public double PeakLower(int corner) => _peakLower[corner];

        /// <summary>The ball-pass frequencies over the rotor's: BPFO's and BPFI's (n/2)·(1 ∓ D_w/d_p).</summary>
        public double OuterRatio(int corner) => _outer[corner];
        public double InnerRatio(int corner) => _inner[corner];

        /// <summary>The rotating mass the ball-pass lines shake, kg: the bell and, until it is lost, the prop.</summary>
        public double RotatingMass(int corner) => _bellMass[corner] + (_broken[corner] ? 0 : _propMass);

        /// <summary>
        /// A bend before the first step (the scenarios' damaged builds; a flight never calls it, and its log would not replay): the
        /// hinge's rotation in the rotor's frame (rad), into the rotor from the next step.
        /// </summary>
        public void PresetBend(int corner, double p, double q)
        {
            _bendP[corner] = p;
            _bendQ[corner] = q;
            Apply(corner);
        }

        /// <summary>A brinelled bearing before the first step (as PresetBend): the upper's or the lower's dent, m.</summary>
        public void PresetDent(int corner, bool upper, double dent)
        {
            if (upper) _dentUpper[corner] = dent;
            else _dentLower[corner] = dent;
        }

        /// <summary>The step's contact loads cleared (ContactModel.Forces, before the body shapes).</summary>
        public void Begin()
        {
            for (int c = 0; c < 4; c++)
            {
                _force[c] = _moment[c] = default;
                _largest[c] = 0;
            }
        }

        /// <summary>
        /// One contact pair's force on rotor c this step (ContactModel.Forces): at point (body m) the force (body N), a blade's
        /// (its push along its span held to its column at the rotor's speed, rad/s, its part across the span to its motor's
        /// stalled drive over the radius, and its bending's moment capped at its root's strength) or the bell's; cause names the
        /// pair (Object, Shape, Material, Surface, Point).
        /// </summary>
        public void Add(int c, Double3 point, Double3 force, bool blade, double speed, in FlightEvent cause)
        {
            if (_broken[c]) return;
            if (blade) force = Column(c, point, force, speed, 0);
            _force[c] = _force[c] + force;
            _moment[c] = _moment[c] + Moment(c, point, force, blade);
            double size = force.Length();
            if (size > _largest[c])
            {
                _largest[c] = size;
                _cause[c] = cause;
            }
        }

        /// <summary>The step's contact loads into the shafts and bearings (ContactModel.Forces, before the bells' Finish): each rotor's
        /// at its angle at the step's start.</summary>
        public void Finish(in FlightState s, StepEvents events)
        {
            for (int c = 0; c < 4; c++)
                if (_largest[c] > 0) Load(c, _force[c], _moment[c], s.RotorAngle[c], s.RotorSpeed[c], events, _cause[c]);
        }

        /// <summary>
        /// A blade pass's load on rotor c (PropStrikes): its face's impulse on the drone (body N·s) over the contact time tc (s) at
        /// the meeting point (body m), at most the pass's peak force on the blade (N: its crush, its slap or its cut; none for 0),
        /// along the blade's span at most its column (ColumnLoad), and across it at most what turns the rotor (Turning: the
        /// angular momentum the pass took from it, N·m·s, over tc, and its stalled drive); the rotor at angle (rad) and speed
        /// (rad/s).
        /// </summary>
        public void Strike(int c, Double3 point, Double3 impulse, double tc, double peak, double angular, double angle, double speed, StepEvents events,
            in FlightEvent cause)
        {
            if (_broken[c] || !(tc > 0)) return;
            Double3 force = impulse * (1 / tc);
            double size = force.Length();
            if (peak > 0 && size > peak) force = force * (peak / size);
            force = Column(c, point, force, speed, angular / tc);
            Load(c, force, Moment(c, point, force, true), angle, speed, events, cause);
        }

        /// <summary>
        /// A blade's column at speed (rad/s), the most it passes its hub along its span toward the shaft from radius r (m): its
        /// centrifugal tension at the root ω²·Σ m·r and its static Euler load π²·EI_f,root/(4r²) (derived: past them the span goes
        /// into compression and the blade flaps out of the disc; the whole blade's, its damage left out, and the root's section, an
        /// upper bound for the tapered blade, assumed).
        /// </summary>
        public double ColumnLoad(double speed, double r) =>
            speed * speed * _bladeMoment + (r > 0 ? Pi * Pi * _rootFlexural / (4 * r * r) : double.PositiveInfinity);

        // A blade's force at point (body) with its push toward the hub along its span held to its column, the whole force scaled
        // down with it (a face's friction on the blade follows its normal force, a crush's μ·F_n), and its part across the span in
        // the disc held to what turns the rotor: by the rotor's moment balance F_t·r = J·ω̇ + the drive, so at most (the rate of
        // its angular momentum's change, N·m, plus its motor's stalled drive Kt·V/R) over r (derived).
        Double3 Column(int c, Double3 point, Double3 force, double speed, double turning)
        {
            Double3 d = point - _hub[c];
            d = new Double3(d.X, 0, d.Z);
            double r = d.Length();
            if (!(r > 0)) return force;
            Double3 span = d * (1 / r), across = new Double3(-span.Z, 0, span.X);
            double push = -Double3.Dot(force, span), column = ColumnLoad(speed, r);
            if (push > column) force = force * (column / push);
            double ft = Double3.Dot(force, across), cap = (turning + Stall(c)) / r;
            if (ft > cap) force = force - across * (ft - cap);
            else if (ft < -cap) force = force - across * (ft + cap);
            return force;
        }

        /// <summary>Rotor c's motor's stalled drive at the last sub-step's bus, Kt·V/R, N·m (the most it turns its rotor with).</summary>
        public double Stall(int c) => _propulsion.TorqueConstant(c) * _propulsion.BusVolts / _propulsion.Resistance(c);

        // A force's moment at the upper bearing, body N·m, less its part along the shaft: (P − B) × F, a blade's own bending
        // ((P − H) × F, flatwise) capped at its root's strength.
        Double3 Moment(int c, Double3 point, Double3 force, bool blade)
        {
            Double3 m;
            if (blade)
            {
                Double3 own = Double3.Cross(point - _hub[c], force);
                own = new Double3(own.X, 0, own.Z);
                double size = own.Length();
                if (size > _rootCap) own = own * (_rootCap / size);
                m = Double3.Cross(_hub[c] - _bearing[c], force) + own;
            }
            else m = Double3.Cross(point - _bearing[c], force);
            return new Double3(m.X, 0, m.Z);
        }

        // A load on rotor c: the force (body N) and its moment at the upper bearing (body N·m, in the disc's plane), the rotor at
        // angle and speed: the bearings' loads, the bend or the break.
        void Load(int c, Double3 force, Double3 moment, double angle, double speed, StepEvents events, in FlightEvent cause)
        {
            double size = moment.Length();
            // The bearings: R₂ = (M × ŷ)/s, R₁ = −F_∥ − R₂.
            Double3 lower = Double3.Cross(moment, Up) * (1 / _span[c]);
            Double3 upper = new Double3(-force.X, 0, -force.Z) - lower;
            if (size > _ultimate[c])
            {
                _broken[c] = true;
                FlightEvent e = Event(EventKind.PropLost, c, 0, cause);
                e.A = size;
                e.B = _ultimate[c];
                e.C = speed;
                events.Add(e);
                Brinell(c, true, upper.Length(), events, cause);
                Brinell(c, false, lower.Length(), events, cause);
                _propulsion.LoseProp(c);
                Apply(c);
                return;
            }
            if (size > _hinge[c])
            {
                // The hinge along M in the rotor's frame at its angle: p along blade 0, (cos ψ, 0, s·sin ψ), q = p × ŷ.
                double turn = (size - _hinge[c]) / _hingeK[c], passed = _hinge[c];
                DetMath.SinCos(angle, out double sin, out double cos);
                double ss = _sign[c] * sin, mp = moment.X * cos + moment.Z * ss, mq = -moment.X * ss + moment.Z * cos;
                _bendP[c] += turn * mp / size;
                _bendQ[c] += turn * mq / size;
                _hinge[c] = size;
                FlightEvent e = Event(EventKind.MotorDamaged, c, 2, cause);
                e.A = Bend(c);
                e.B = size;
                e.C = passed;
                events.Add(e);
                Apply(c);
            }
            Brinell(c, true, upper.Length(), events, cause);
            Brinell(c, false, lower.Length(), events, cause);
        }

        // A bearing's radial load R: past C0r and its largest so far, its dent 1e-4·D_w·(R/C0r)².
        void Brinell(int c, bool upper, double load, StepEvents events, in FlightEvent cause)
        {
            ref double peak = ref upper ? ref _peakUpper[c] : ref _peakLower[c];
            double passed = DetMath.Max(_rating[c], peak);
            if (!(load > passed)) return;
            peak = load;
            double ratio = load / _rating[c], dent = IsoDent * _ball[c] * ratio * ratio;
            if (upper) _dentUpper[c] = dent;
            else _dentLower[c] = dent;
            FlightEvent e = Event(EventKind.MotorDamaged, c, upper ? 3 : 4, cause);
            e.A = dent;
            e.B = load;
            e.C = passed;
            events.Add(e);
        }

        FlightEvent Event(EventKind kind, int c, int band, in FlightEvent cause)
        {
            FlightEvent e = FlightEvent.Of(kind);
            e.Part = (ushort)_part[c];
            e.Index = (byte)(c | band << 4);
            e.Object = cause.Object;
            e.Shape = cause.Shape;
            e.Material = cause.Material;
            e.Surface = cause.Surface;
            e.Point = cause.Point;
            return e;
        }

        // The bend into the rotor's 1/rev loads (Propulsion.SetShaft) and the bell's run-out (MotorBell.Closure): u = φ × ŷ =
        // (−φ_q, φ_p) in (p, q), the masses' unbalance at their levers, the couples per ω².
        void Apply(int c)
        {
            double fp = _bendP[c], fq = _bendQ[c], up = -fq, uq = fp;
            double mp = _broken[c] ? 0 : _propMass, l = _lever[c], mb = _bellMass[c], lb = _bellArm[c];
            double first = mp * l + mb * lb, bell = mb * lb, offset = l - lb;
            // The blades' couple −Σ I_b·(φ·q_b)·q_b, the bell's −(I_p − I_d)·φ, and the bell's pull at its own height, not the hub's:
            // −(l − l_b)·ŷ × U_b, ŷ × (a, b) = (b, −a).
            double kb = _broken[c] ? 0 : 1, ic = _bellCouple[c];
            double kp = -kb * (_kpp * fp + _kpq * fq) - ic * fp - offset * bell * uq;
            double kq = -kb * (_kpq * fp + _kqq * fq) - ic * fq + offset * bell * up;
            _propulsion.SetShaft(c, first * up, first * uq, kp, kq, up, uq, l);
            _bells.Closure(c, Bend(c) * _reach[c]);
        }

        /// <summary>The bearings' added drag on rotor c at its thrust (N), N·m: each dent's share of SKF's ½·μ·P·d, P half the thrust.</summary>
        public double BearingDrag(int c, double thrust)
        {
            double dents = _dentUpper[c] + _dentLower[c];
            if (dents == 0) return 0;
            double load = 0.5 * DetMath.Abs(thrust);
            return 0.5 * _spec.BearingFriction * load * _bore[c] * _spec.BearingDentDrag * dents / (IsoDent * _ball[c]);
        }

        /// <summary>
        /// The brinelled bearings' ball-pass lines at the camera mount for rotors at speeds (rad/s) on a body of mass (kg): each
        /// line's RMS², (m_rot·(2πf)²·δ)²/(2M²), summed into CameraVibration's bands by its frequency (X 5–50 Hz, Y 50–200 Hz, Z
        /// above), m²/s⁴.
        /// </summary>
        public Double3 BallPass(in Corners speed, double mass)
        {
            double x = 0, y = 0, z = 0;
            for (int c = 0; c < 4; c++)
            {
                double dents = _dentUpper[c] + _dentLower[c], fr = speed[c] / TwoPi;
                if (dents == 0 || !(fr > 0)) continue;
                double m = RotatingMass(c);
                for (int k = 0; k < 2; k++)
                {
                    double f = (k == 0 ? _outer[c] : _inner[c]) * fr, w = TwoPi * f, w2 = w * w;
                    double a = m * w2 * _dentUpper[c] / mass, b = m * w2 * _dentLower[c] / mass, rms2 = 0.5 * (a * a + b * b);
                    if (f < VibrationBands.LowCorner) continue;
                    if (f < VibrationBands.MidCorner) x += rms2;
                    else if (f < VibrationBands.HighCorner) y += rms2;
                    else z += rms2;
                }
            }
            return new Double3(x, y, z);
        }

        /// <summary>The shafts into the step's record: each bend (rad, rotor frame), the bearings' drag at the rotor's thrust (N·m) and
        /// the outer race's ball-pass frequency of a brinelled bearing (Hz, 0 for none).</summary>
        public void Trace(in FlightState s, ref StepTrace trace)
        {
            for (int c = 0; c < 4; c++)
            {
                trace.ShaftBendP[c] = _bendP[c];
                trace.ShaftBendQ[c] = _bendQ[c];
                trace.BearingDrag[c] = BearingDrag(c, _propulsion.Thrust(c));
                trace.BallPassOuter[c] = _dentUpper[c] + _dentLower[c] > 0 ? _outer[c] * s.RotorSpeed[c] / TwoPi : 0;
            }
        }
    }
}
