using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The motors' heat and dirt (p7f-damage 1.6b, design 17, D-083; drone.json's motors[].internals), beside the bells (MotorBell),
    /// whose Finish steps it once a step after the propulsion's sub-steps: how hot each motor runs, what heat does to it, and the
    /// soil in its gap. Engine-free, allocation-free in a step. How fast a motor dies is the throttle's, the contact's and the
    /// load's through these laws: nothing here is a timer or a rate.
    /// - Two nodes a motor (temperatures θ over the air's): the windings, C_W = m_Cu·c_Cu, and the rest of the motor (the stator,
    ///   the bell and the base), C_S = (m_motor − m_Cu)·c_Fe. The windings take I²R from the step's real current, ∫i²dt over its
    ///   sub-steps (Propulsion.CurrentSquared: driving, braking or shorted, so a stalled or ground-loaded rotor's larger current
    ///   heats it faster), R_W = R_W20·(1 + α_Cu·(T − 20)) (copper's published coefficient), only the winding's own share of the
    ///   chain's resistance; they pass it to the stator through the slots (winding_to_stator_w_per_k) and, turning, to the air
    ///   inside the bell by their end turns (Churchill and Bernstein's cylinder in crossflow, the bundle in the bell's air at half
    ///   the stator face's speed, assumed: a Couette flow's mean). The rest takes the motor's no-load loss Kt·I0·ω (its iron's
    ///   and bearings') and the bell's mechanical losses (its scrape's, its rub's, its bearings' and its dirt's drag times ω,
    ///   all as heat in the motor: aluminium's effusivity is 10–30 times a soil's or a stone's, so it takes most of a scrape's
    ///   heat, assumed whole) and loses it to the air by convection and radiation and to its arm by conduction (ArmConductance):
    ///   the bell's side by its own spin (Kays and Bjorklund's rotating cylinder, Nu = 0.133·Re^(2/3)·Pr^(1/3), Re = ω·D²/ν), its
    ///   top as a rotating disc (Cobb and Saunders, Nu = 0.36·Re^(1/2), Re = ω·R²/ν), the airspeed across it (Churchill and
    ///   Bernstein) and natural convection over its height (Churchill and Chu), blended as (Σ h³)^(1/3) (Churchill and Usagi's
    ///   form, assumed), and radiation, εσ(T² + T_a²)(T + T_a). A stalled motor loses its own air: only the natural convection,
    ///   the radiation and the arm are left. Stepped by backward Euler on the two nodes, the copper's resistance implicit, the
    ///   conductances at the step's start (all published correlations; the air's numbers at 15 °C).
    /// - What heat does: the magnets on the bell (at the rest's temperature) lose remanence by their reversible coefficient, so
    ///   Kt (= Ke) falls, ×(1 + α_B·θ_S); past their maximum working temperature the loss doubles, its extra share kept on
    ///   cooling (assumed: the load line passes the knee there), ×(1 + α_B·(T_peak − T_max)). The copper's rise raises the
    ///   chain's resistance (Propulsion.SetWinding). Past the insulation's class rating the enamel fails between turns and one
    ///   coil shorts (assumed: one of the stator's slots' coils, where the windings are hottest and pressed): the phase loses
    ///   that coil's EMF and resistance, so Kt and R_W × (1 − 1/slots) (derived, averaged over the six commutation sectors), and
    ///   the shorted coil is a loop the magnets drive, E_c = Ke·ω/n, R_c = R_W/n, X_c = p·ω·τ_w·R/n (n = 2·slots/3, the coil's
    ///   share of the line; its inductance the winding's L/R at the drawn R, assumed in proportion), dissipating E_c²·R_c/(R_c² +
    ///   X_c²) in the windings and braking the rotor by that over ω. Past the enamel's breakdown the turns short throughout, the
    ///   phases with them: the motor fails (Propulsion.Fail: no drive, the shorted winding brakes as a three-phase short).
    /// - Dirt: soil enters the gap from a turning bell's scrape on a soil (Scrape: the bell slides at ω·R, Archard's V =
    ///   k·F_n·s/H with H the soil's bearing pressure c·N_c, its bulk density's mass) and from a blade's carve of soil next to
    ///   it (Carve: PropStrikes' swept mass), each by the share of the motor's openings in the half space its source sees,
    ///   A_open/(2π·d²), d at least the can's radius and the share at most a half (derived: an isotropic fling into a
    ///   hemisphere; the openings drone.json's). Its loose volume V (its mass at its soil's bulk density) lies over the stator's
    ///   face (assumed: the bell's turn spreads it) and fills the gap's volume V_g = A_face·g by φ = V/V_g: thinner than the gap
    ///   it rides the turning magnets (their spin presses it outward) and touches nothing; filled, φ ≥ 1, it bridges the gap over
    ///   the face and shears at the soil's own cohesion, c·A_face·r_s (assumed: no grain spans a part-filled gap). Past φ = 1 more soil packs the
    ///   layer toward its solids (its mass at surfaces.json's particle density filling V_g, where no more enters: the rest
    ///   clogs the openings and falls away, assumed), and the packed layer presses the teeth with the soil's bearing pressure
    ///   c·N_c over the face times its packing share s = (V − V_g)/(V − m/ρ_p), 0 loose and 1 solid (assumed: linear between,
    ///   the pressure it resisted a blade with at its solids), its friction μ·c·N_c·s·A_face·r_s (μ the soil's kinetic). The drag
    ///   goes to Propulsion.SetMotor with the bell's, so a packed gap binds a motor its drive cannot turn through it, by the
    ///   bind law (MotorBound). What enters stays (assumed). The carve's flung mass is PropStrikes' swept mass, whose groove is
    ///   not stored (1.3): a held carve flings the same soil each pass, so this channel is an upper bound the solids' cap holds.
    /// - Logged: each motor's winding and stator temperatures, its Kt's change by heat, its dirt's mass and drag, and its state
    ///   (StepTrace); MotorDamaged's bands 5 (the magnets past their maximum), 6 (a coil shorted), 7 (the motor failed) and 8 (the
    ///   gap packed with dirt).
    /// </summary>
    public sealed class MotorHealth
    {
        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;

        /// <summary>The Stefan–Boltzmann constant, W/(m²·K⁴) (published, CODATA 2018), and 0 °C in kelvin.</summary>
        public const double StefanBoltzmann = 5.670374419e-8, Kelvin = 273.15;

        /// <summary>A motor's state (the record's motor_state): sound, its magnets past their maximum working temperature, a coil
        /// shorted, failed.</summary>
        public const int Sound = 0, Hot = 1, Shorted = 2, Failed = 3;

        const double Gravity = 9.80665;

        readonly ContactSpec _spec;
        readonly Propulsion _propulsion;
        readonly MotorBell _bells;

        // Each motor's constants: the nodes' capacities (J/K), the winding's conductance to the stator (W/K), its resistance at 20 °C
        // and at the air's temperature (Ω), the end turns' area (m²) and bundle (m), the coil's share of the line n and the share a
        // short takes, the pole pairs; the thresholds (°C) and the remanence's coefficient (1/K); the convection's areas (m²: the
        // bell's spinning side, the motor's still side, the top), its diameter, radius and height (m), the emissivity; the dirt's
        // openings (m²), the stator's face (m²), radius (m) and the gap (m); the soils' particle density (kg/m³, 0 without surfaces.json).
        readonly double[] _cw = new double[4], _cs = new double[4], _gws = new double[4], _rw20 = new double[4], _rwa = new double[4];
        readonly double[] _endArea = new double[4], _endDiameter = new double[4], _line = new double[4], _coil = new double[4], _poles = new double[4];
        readonly double[] _rating = new double[4], _breakdown = new double[4], _tmax = new double[4], _remanence = new double[4];
        readonly double[] _spinSide = new double[4], _stillSide = new double[4], _top = new double[4], _diameter = new double[4], _radius = new double[4];
        readonly double[] _height = new double[4], _emissivity = new double[4];
        readonly double[] _open = new double[4], _face = new double[4], _statorRadius = new double[4], _gap = new double[4];
        readonly double _particle;
        readonly int[] _part = new int[4];
        readonly double _pr3, _chu, _bernstein;

        // The state: each motor's temperatures and the rest's peak (°C), its state; its dirt's volume (m³) and mass (kg), and the
        // mass-weighted sums of its soils' cohesion (Pa·kg) and friction (kg).
        readonly double[] _tw = new double[4], _ts = new double[4], _peak = new double[4];
        readonly int[] _state = new int[4];
        readonly bool[] _packed = new bool[4], _hot = new bool[4];
        readonly double[] _dirtVolume = new double[4], _dirtMass = new double[4], _dirtCohesion = new double[4], _dirtFriction = new double[4];

        // The step's outputs: the Kt factor by heat, the shorted coil's drag (N·m) and heat (W), the dirt's drag (N·m), the
        // conductance to the air (W/K, the rest's, and the windings' own).
        readonly double[] _kt = { 1, 1, 1, 1 }, _loopDrag = new double[4], _loop = new double[4], _dirtDrag = new double[4];
        readonly double[] _ambient = new double[4], _windingAir = new double[4];

        /// <summary>The motors of spec's internals (ContactSpec.Motors) on propulsion's rotors, beside their bells.</summary>
        public MotorHealth(ContactSpec spec, Propulsion propulsion, MotorBell bells)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _propulsion = propulsion ?? throw new ArgumentNullException(nameof(propulsion));
            _bells = bells ?? throw new ArgumentNullException(nameof(bells));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("the motors' heat needs their internals", nameof(spec));
            double pr = spec.AirPrandtl;
            _pr3 = DetMath.Cbrt(pr);
            // Churchill and Chu's [1 + (0.492/Pr)^(9/16)]^(8/27) and Churchill and Bernstein's [1 + (0.4/Pr)^(2/3)]^(1/4), load-time.
            _chu = DetMath.Exp(8.0 / 27 * DetMath.Log(1 + DetMath.Exp(9.0 / 16 * DetMath.Log(0.492 / pr))));
            double b = DetMath.Cbrt(0.4 / pr);
            _bernstein = DetMath.Sqrt(DetMath.Sqrt(1 + b * b));
            _particle = spec.Surfaces != null ? spec.Surfaces.ParticleDensity : 0;
            for (int c = 0; c < 4; c++)
            {
                MotorInternals m = spec.Motors[c];
                if (!(m.MotorMass > m.CopperMass)) throw new ArgumentException("motor " + c + "'s internals lack its mass", nameof(spec));
                _part[c] = bells.Part(c);
                _cw[c] = m.CopperMass * spec.CopperSpecificHeat;
                _cs[c] = (m.MotorMass - m.CopperMass) * spec.IronSpecificHeat;
                _gws[c] = m.WindingConductance;
                _rw20[c] = m.WindingResistance;
                _rwa[c] = m.WindingResistance * (1 + spec.CopperTemperatureCoefficient * (spec.AirTemperature - 20));
                _endArea[c] = m.EndTurnArea;
                _endDiameter[c] = m.EndTurnDiameter;
                _line[c] = 2.0 * m.StatorSlots / 3;
                _coil[c] = 1.0 / m.StatorSlots;
                _poles[c] = propulsion.PolePairs(c);
                _rating[c] = m.InsulationRating;
                _breakdown[c] = m.InsulationBreakdown;
                _tmax[c] = m.MagnetMaxTemperature;
                _remanence[c] = m.RemanenceCoefficient;
                double R = bells.Radius(c), D = 2 * R, band = m.BellTo - m.BellFrom;
                _radius[c] = R;
                _diameter[c] = D;
                _height[c] = m.BellTo;
                _spinSide[c] = Pi * D * band;
                _stillSide[c] = Pi * D * m.BellFrom;
                _top[c] = Pi * R * R;
                _emissivity[c] = m.Emissivity;
                _open[c] = m.OpenArea;
                double hs = m.StatorTo - m.StatorFrom, rs = m.StatorDiameter / 2;
                _statorRadius[c] = rs;
                _face[c] = TwoPi * rs * hs;
                _gap[c] = m.Gap;
                _tw[c] = _ts[c] = _peak[c] = spec.AirTemperature;
            }
        }

        /// <summary>Each motor's windings' and rest's temperatures and the rest's peak, °C; its state (Sound … Failed).</summary>
        public double WindingTemperature(int corner) => _tw[corner];
        public double StatorTemperature(int corner) => _ts[corner];
        public double PeakTemperature(int corner) => _peak[corner];
        public int State(int corner) => _state[corner];

        /// <summary>The Kt factor heat leaves (the magnets' and a shorted coil's), and the drag the motor's health adds (a shorted
        /// coil's loop and the dirt), N·m; the dirt's share of it, its mass (kg) and volume (m³).</summary>
        public double KtFactor(int corner) => _kt[corner];
        public double Drag(int corner) => _loopDrag[corner] + _dirtDrag[corner];
        public double DirtDrag(int corner) => _dirtDrag[corner];
        public double DirtMass(int corner) => _dirtMass[corner];
        public double DirtVolume(int corner) => _dirtVolume[corner];

        /// <summary>The nodes' capacities (J/K), the windings' conductance to the stator (W/K), the last step's conductances to the
        /// air (W/K: the rest's, with its arm, and the windings' end turns'), a shorted coil's loop heat (W), the gap's capacity
        /// (the stator's face times the gap, m³) and the most dirt it takes (its solids at the particle density, kg).</summary>
        public double WindingCapacity(int corner) => _cw[corner];
        public double StatorCapacity(int corner) => _cs[corner];
        public double WindingConductance(int corner) => _gws[corner];
        public double AmbientConductance(int corner) => _ambient[corner];
        public double WindingAirConductance(int corner) => _windingAir[corner];
        public double LoopHeat(int corner) => _loop[corner];
        public double GapCapacity(int corner) => _face[corner] * _gap[corner];
        public double DirtCapacity(int corner) => _face[corner] * _gap[corner] * _particle;

        /// <summary>The winding's resistance at temperature t (°C), Ω (its copper's, before a short).</summary>
        public double WindingResistanceAt(int corner, double t) => _rw20[corner] * (1 + _spec.CopperTemperatureCoefficient * (t - 20));

        /// <summary>
        /// A turning bell scraping a soil (ContactModel, a bell pair on a soil surface): its load fn (N) and its slide on it (m/s) over
        /// dt seconds, at distance (m) from the motor's openings, the soil set: the soil it scrapes off by Archard and its share in.
        /// </summary>
        public void Scrape(int corner, double fn, double slide, double distance, in SoilSet set, double dt)
        {
            double hardness = set.Cohesion * PropStrikes.BearingFactor;
            if (!(hardness > 0) || !(fn > 0) || !(slide > 0)) return;
            double volume = _spec.ScrapeWearCoefficient * fn * slide * dt / hardness;
            Enter(corner, volume * set.BulkDensity * Share(corner, distance), set.Cohesion, set.KineticFriction, set.BulkDensity);
        }

        /// <summary>A blade's carve of a soil next to the motor (PropStrikes): the mass it swept (kg) at radius r (m) from the hub, the
        /// soil's cohesion (Pa), kinetic friction and bulk density (kg/m³): its share in.</summary>
        public void Carve(int corner, double mass, double r, double cohesion, double friction, double density)
        {
            if (!(mass > 0) || !(density > 0)) return;
            Enter(corner, mass * Share(corner, r - _radius[corner]), cohesion, friction, density);
        }

        // The share of a fling from distance d that reaches the openings: A_open/(2π·d²), d at least the can's radius, at most a half.
        double Share(int c, double d)
        {
            double r = DetMath.Max(d, _radius[c]);
            return DetMath.Min(0.5, _open[c] / (TwoPi * r * r));
        }

        void Enter(int c, double mass, double cohesion, double friction, double density)
        {
            // At most what fills the gap with the soil's solids; the rest clogs the openings and falls away.
            double room = DirtCapacity(c) - _dirtMass[c];
            if (mass > room) mass = room;
            if (!(mass > 0)) return;
            _dirtMass[c] += mass;
            _dirtVolume[c] += mass / density;
            _dirtCohesion[c] += mass * cohesion;
            _dirtFriction[c] += mass * friction;
        }

        /// <summary>
        /// One step of motor c (MotorBell.Finish, after the sub-steps): its rotor at speed (rad/s), the airspeed across it (m/s), the
        /// mechanical loss heat into the rest (W: Kt·I0 and the bell's drags times ω), dt seconds; the temperatures, the Kt factor,
        /// the winding's resistance (Propulsion.SetWinding), a short, a failure (Propulsion.Fail) and the dirt's drag for the next
        /// step's sub-steps, the events and the trace.
        /// </summary>
        public void Step(int c, double speed, double airspeed, double mechanical, double dt, StepEvents events, ref StepTrace trace)
        {
            double ta = _spec.AirTemperature, alpha = _spec.CopperTemperatureCoefficient;
            double keep = _state[c] >= Shorted ? 1 - _coil[c] : 1;
            double thW = _tw[c] - ta, thS = _ts[c] - ta;

            // A shorted coil's loop, driven by the magnets: E²·R_c/(R_c² + X_c²) = (Ke·ω)²·R_W/(n·(R_W² + X²)), X = p·ω·τ_w·R.
            double loop = 0;
            if (_state[c] >= Shorted && _state[c] < Failed && speed > 0)
            {
                double rw = WindingResistanceAt(c, _tw[c]), e = _propulsion.TorqueConstant(c) * speed;
                double x = _poles[c] * speed * _spec.WindingTimeConstant * _propulsion.DrawnResistance(c);
                loop = e * e * rw / (_line[c] * (rw * rw + x * x));
            }
            _loop[c] = loop;

            // The heat in: the windings' I²R at their implicit temperature, the loop's, the rest's mechanical loss and the dirt's drag.
            double q = _propulsion.CurrentSquared(c);
            double rise = keep * q * _rw20[c] * alpha; // J/K: the copper's heat per kelvin over the step
            double heatW = keep * q * _rwa[c] + loop * dt;
            double heatS = (mechanical + (speed > 0 ? _dirtDrag[c] * speed : 0)) * dt;
            double ambient = Ambient(c, thS, speed, airspeed), air = EndTurns(c, speed);
            _ambient[c] = ambient;
            _windingAir[c] = air;
            double g = _gws[c];
            double a11 = _cw[c] + dt * (g + air) - rise, a12 = -dt * g, b1 = _cw[c] * thW + heatW;
            double a21 = -dt * g, a22 = _cs[c] + dt * (g + ambient), b2 = _cs[c] * thS + heatS;
            double det = a11 * a22 - a12 * a21;
            thW = (b1 * a22 - a12 * b2) / det;
            thS = (a11 * b2 - a21 * b1) / det;
            _tw[c] = ta + thW;
            _ts[c] = ta + thS;
            if (_ts[c] > _peak[c]) _peak[c] = _ts[c];

            // The thresholds, each once.
            if (!_hot[c] && _peak[c] > _tmax[c])
            {
                _hot[c] = true;
                Become(c, _state[c] < Hot ? Hot : _state[c], 5, _ts[c], _tmax[c], 0, events);
            }
            if (_state[c] < Shorted && _tw[c] >= _rating[c])
            {
                Become(c, Shorted, 6, _tw[c], _rating[c], 0, events);
                keep = 1 - _coil[c];
            }
            if (_state[c] < Failed && _tw[c] >= _breakdown[c])
            {
                Become(c, Failed, 7, _tw[c], _breakdown[c], speed, events);
                _propulsion.Fail(c);
            }

            // The magnets' flux and the copper's resistance for the next step.
            double past = _peak[c] - _tmax[c];
            _kt[c] = keep * (1 + _remanence[c] * thS + (past > 0 ? _remanence[c] * past : 0));
            _propulsion.SetWinding(c, keep * WindingResistanceAt(c, _tw[c]) - _rwa[c]);
            _loopDrag[c] = speed > 0 ? loop / speed : 0;

            // The dirt: a layer thinner than the gap rides the magnets; filled, it shears at its cohesion over the face, and packed past
            // the gap it presses the teeth with its bearing pressure by its packing share.
            double drag = 0;
            if (_dirtMass[c] > 0)
            {
                double vg = _face[c] * _gap[c], v = _dirtVolume[c], fill = v / vg, rs = _statorRadius[c];
                double cohesion = _dirtCohesion[c] / _dirtMass[c];
                if (fill >= 1)
                {
                    drag = cohesion * _face[c] * rs;
                    double packing = (v - vg) / (v - _dirtMass[c] / _particle);
                    drag += _dirtFriction[c] / _dirtMass[c] * cohesion * PropStrikes.BearingFactor * packing * _face[c] * rs;
                    if (!_packed[c])
                    {
                        _packed[c] = true;
                        FlightEvent e = Event(c, 8);
                        e.A = _dirtMass[c];
                        e.B = _dirtVolume[c];
                        e.C = GapCapacity(c);
                        events.Add(e);
                    }
                }
            }
            _dirtDrag[c] = drag;

            trace.WindingTemperature[c] = _tw[c];
            trace.StatorTemperature[c] = _ts[c];
            trace.HeatKtChange[c] = _kt[c] - 1;
            trace.DirtMass[c] = _dirtMass[c];
            trace.DirtDrag[c] = drag;
            trace.MotorState[c] = _state[c];
        }

        void Become(int c, int state, int band, double a, double b, double d, StepEvents events)
        {
            _state[c] = state;
            FlightEvent e = Event(c, band);
            e.A = a;
            e.B = b;
            e.C = d;
            events.Add(e);
        }

        FlightEvent Event(int c, int band)
        {
            FlightEvent e = FlightEvent.Of(EventKind.MotorDamaged);
            e.Part = (ushort)_part[c];
            e.Index = (byte)(c | band << 4);
            return e;
        }

        /// <summary>The rest's conductance to the air and the arm at its rise thS (K), the rotor's speed (rad/s) and the airspeed across
        /// the motor (m/s), W/K.</summary>
        public double Ambient(int c, double thS, double speed, double airspeed)
        {
            double k = _spec.AirConductivity, nu = _spec.AirViscosity, D = _diameter[c], R = _radius[c], L = _height[c];
            double ta = _spec.AirTemperature + Kelvin, ts = ta + thS;
            // Natural convection over the height (Churchill and Chu): Ra = g·β·ΔT·L³·Pr/ν², β = 1/T_film.
            double ra = thS > 0 ? Gravity / (ta + 0.5 * thS) * thS * L * L * L * _spec.AirPrandtl / (nu * nu) : 0;
            double nn = 0.825 + 0.387 * DetMath.Sqrt(DetMath.Cbrt(ra)) / _chu, hn = nn * nn * k / L;
            double hn3 = hn * hn * hn;
            // The spin: the bell's side (Kays and Bjorklund) and its top (Cobb and Saunders).
            double w = speed > 0 ? speed : 0, re = w * D * D / nu, cr = DetMath.Cbrt(re);
            double hc = 0.133 * cr * cr * _pr3 * k / D, hd = 0.36 * DetMath.Sqrt(w * R * R / nu) * k / R;
            // The airspeed across (Churchill and Bernstein).
            double hx = airspeed > 0 ? Bernstein(airspeed * D / nu) * k / D : 0;
            double hx3 = hx * hx * hx;
            double side = DetMath.Cbrt(hc * hc * hc + hx3 + hn3), still = DetMath.Cbrt(hx3 + hn3), top = DetMath.Cbrt(hd * hd * hd + hn3);
            double hr = _emissivity[c] * StefanBoltzmann * (ts * ts + ta * ta) * (ts + ta);
            return _spinSide[c] * side + _stillSide[c] * still + _top[c] * top + (_spinSide[c] + _stillSide[c] + _top[c]) * hr + _spec.ArmConductance;
        }

        /// <summary>The windings' end turns' conductance to the bell's air at the rotor's speed (rad/s), W/K: the bundles in crossflow
        /// at half the stator face's speed (Churchill and Bernstein).</summary>
        public double EndTurns(int c, double speed)
        {
            double v = (speed > 0 ? speed : 0) * _statorRadius[c] / 2, d = _endDiameter[c];
            return Bernstein(v * d / _spec.AirViscosity) * _spec.AirConductivity / d * _endArea[c];
        }

        // Churchill and Bernstein's cylinder in crossflow (Re·Pr > 0.2): Nu = 0.3 + 0.62·Re^½·Pr^⅓/[1 + (0.4/Pr)^⅔]^¼·[1 + (Re/282000)^⅝]^⅘.
        double Bernstein(double re)
        {
            if (!(re > 0)) return 0.3;
            double tail = DetMath.Exp(0.8 * DetMath.Log(1 + DetMath.Exp(0.625 * DetMath.Log(re / 282000))));
            return 0.3 + 0.62 * DetMath.Sqrt(re) * _pr3 / _bernstein * tail;
        }
    }
}
