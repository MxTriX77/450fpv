namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The props' blades as elements (p7f-damage design 2): drone.json's props.blade and the sections' aerodynamics that
    /// DamagedRotor's blade-element momentum theory flies (PropulsionSpec.Rotor; DroneSpec.BuildPropulsion). The sections are
    /// the flapping's (RotorFlappingSpec: p7c-wind 1.6's fit), so the drone has one blade.
    /// </summary>
    public sealed class DamagedRotorSpec
    {
        /// <summary>The blade's stations, chord, section angle (the chord line's), thickness, fill and material (props.blade).
        /// Read-only by convention.</summary>
        public PropBlade Blade;

        /// <summary>The sections' lift slope, per rad: 5.7 (assumed, RotorFlappingSpec's).</summary>
        public double LiftSlope = 5.7;

        /// <summary>The sections' zero-lift angle, rad: −6.3° (derived: p7c-wind 1.6's fit, RotorFlappingSpec's).</summary>
        public double ZeroLiftAngle = -0.10995574287564276;

        /// <summary>The sections' profile drag coefficient c_d0: 0.049 (derived: p7c-wind 1.6 fitted it with α_L0 so the same
        /// elements give APC's published static 12.43 N and κ 0.0144 m at 9000 rpm; drone.json's h_force note).</summary>
        public double ProfileDrag = 0.049;

        public DamagedRotorSpec Clone() => (DamagedRotorSpec)MemberwiseClone();
    }

    /// <summary>
    /// One rotor's blades and what they make of it (p7f-damage design 2 and 3, PHY-26, PHY-07): each blade's state
    /// (BladeState), and, when a blade is off the sheet (a damage or a factory pitch offset), the rotor's thrust and torque
    /// ratios to the intact sheet blade, each blade's shares and arms, and the material it has lost.
    /// - Blade-element momentum theory on what is left (Leishman 2006, §3.4, the closed form, published): linear lift (a,
    ///   α_L0, c_d0 as the flapping's), the climb inflow λ_c = J/π, each annulus's inflow from momentum on the blades
    ///   lifting there, λ = √((σa/16 − λ_c/2)² + aσ_θ·x/8) − (σa/16 − λ_c/2), σ and σ_θ = Σ c_b·θ_b/(πR) over the blades
    ///   present (a blade's θ its section's angle over the zero-lift line plus its factory pitch and its pitch set; its
    ///   chord the sheet's times its share). dC_T,b = (c_b/πR)·(a/2)·(θ_b·x² − λ·x)·dx, dC_Q,b = λ·dC_T,b + (c_b/πR)·(c_d0/2)·
    ///   x³·dx (published BET). Each blade lifts to its own effective tip B·x_tip, B = 1 − √(2C_T)/N_b (Sissingh's tip loss,
    ///   Leishman 2006, published), C_T the rotor's, by TipLossPasses fixed passes from B = 1; its profile drag acts to its
    ///   tip. A station partly past a tip counts its share of the span (the annulus smeared over the station).
    /// - The tables (when the blades change, never in steady state): at each of the props' published advance ratios
    ///   (DroneProps.InflowAdvanceRatio), η_T and η_Q, the rotor's C_T and C_Q over the intact sheet blade's; each blade's
    ///   thrust and torque shares, its thrust arm (its thrust's moment about the hub per newton of the rotor's thrust, R·
    ///   Σ dC_T,b·x / C_T) and its drag per torque (its in-plane force per N·m of the rotor's torque, Σ (dC_Q,b/x) / (R·C_Q)).
    ///   The ratios are nondimensional, so they hold at every rpm (the model has no Reynolds number; the design's 9000 rpm
    ///   reference is exact within it).
    /// - The 1/rev loads per unit, in the rotor's frame (p along blade 0, q along blade 0 × body +y): the blades' thrust
    ///   moment Σ_b arm_b·(−s·sin φ_b, cos φ_b) per N of thrust and their drag Σ_b drag_b·(sin φ_b, −s·cos φ_b) per N·m of
    ///   torque, φ_b = 2πb/N_b the blade's azimuth in the turning direction, s the rotor's reaction sign (Propulsion: +1
    ///   counter-clockwise). Both vanish on a whole prop with equal pitches.
    /// - The material lost (BladeState's stations, density × fill × chord × thickness × span): its mass, its moment of
    ///   inertia about the shaft (the rotor's J less it) and its first moment, whose pull is −Σ_b S_b·ê_b·ω²; a flap set's
    ///   centrifugal couple, −ω²·Σ_b P_b·(ê_b × y), P_b = Σ m·r·z of the stations left (derived).
    /// Basic operations only; Rebuild allocates nothing (the buffers exist from construction).
    /// </summary>
    public sealed class DamagedRotor
    {
        /// <summary>The tip loss's fixed passes from B = 1 (each moves C_T by about 0.14 of the last move at hover).</summary>
        public const int TipLossPasses = 12;

        const double Pi = 3.141592653589793;

        readonly DamagedRotorSpec _spec;
        readonly PropBlade _b;
        readonly double _radius, _spin;
        readonly int _blades, _n, _nj;
        readonly double[] _advance;
        readonly BladeState[] _state;
        readonly double[] _cos, _sin, _mass;
        readonly double[] _ct0, _cq0;
        readonly double[] _etaT, _etaQ, _momentP, _momentQ, _forceP, _forceQ;
        readonly double[] _thrustShare, _thrustArm, _torqueShare, _dragPerTorque;
        readonly double[] _ctb, _ctbx, _cqb, _cqbx;
        readonly double[] _stationThrust, _stationTorque; // the blades' dC_T and dC_Q per station (both blades) at J's first point
        readonly double[] _intactThrust, _intactTorque;   // the intact sheet blade's

        /// <summary>A rotor of spec's blades on a prop of this radius and blade count, turning with reaction sign spin
        /// (Propulsion: −1 clockwise, +1 counter-clockwise), its tables on the advance ratios given (the props' published
        /// grid). Every blade whole, no pitch offset.</summary>
        public DamagedRotor(DamagedRotorSpec spec, double radius, int blades, double spin, double[] advance)
        {
            _spec = spec ?? throw new System.ArgumentNullException(nameof(spec));
            _b = spec.Blade ?? throw new System.ArgumentException("the rotor needs props.blade", nameof(spec));
            if (blades < 1) throw new System.ArgumentException("a rotor has a blade or more", nameof(blades));
            _radius = radius;
            _blades = blades;
            _spin = spin;
            _n = _b.Stations.Length;
            _advance = (double[])advance.Clone();
            _nj = _advance.Length;
            _state = new BladeState[blades];
            _cos = new double[blades];
            _sin = new double[blades];
            for (int b = 0; b < blades; b++)
            {
                _state[b] = new BladeState(_n);
                Azimuth(b, blades, out _cos[b], out _sin[b]);
            }
            _mass = new double[_n];
            double h = _b.Span;
            for (int k = 0; k < _n; k++) _mass[k] = _b.Density * _b.Fill[k] * _b.Chord[k] * _b.Thickness[k] * radius * h;
            _ct0 = new double[_nj];
            _cq0 = new double[_nj];
            _etaT = new double[_nj];
            _etaQ = new double[_nj];
            _momentP = new double[_nj];
            _momentQ = new double[_nj];
            _forceP = new double[_nj];
            _forceQ = new double[_nj];
            _thrustShare = new double[_nj * blades];
            _thrustArm = new double[_nj * blades];
            _torqueShare = new double[_nj * blades];
            _dragPerTorque = new double[_nj * blades];
            _ctb = new double[blades];
            _ctbx = new double[blades];
            _cqb = new double[blades];
            _cqbx = new double[blades];
            _stationThrust = new double[_n];
            _stationTorque = new double[_n];
            for (int j = 0; j < _nj; j++)
            {
                _ct0[j] = Bemt(_advance[j] / Pi, true, out _cq0[j], j == 0);
                if (j == 0)
                {
                    _intactThrust = (double[])_stationThrust.Clone();
                    _intactTorque = (double[])_stationTorque.Clone();
                }
                _etaT[j] = _etaQ[j] = 1;
            }
            Sound = true;
        }

        /// <summary>A blade's state (0 to Blades − 1): a fixture or a strike changes it, then Propulsion.Rebuild.</summary>
        public BladeState Blade(int b) => _state[b];

        public int Blades => _blades;

        /// <summary>The advance ratios the tables hold.</summary>
        public double AdvanceRatio(int j) => _advance[j];
        public int Points => _nj;

        /// <summary>Every blade whole and no pitch offset: the rotor is the published table's (Propulsion skips it).</summary>
        public bool Sound { get; private set; }

        /// <summary>The material lost, kg, and its moment of inertia about the shaft, kg·m².</summary>
        public double LostMass { get; private set; }
        public double LostInertia { get; private set; }

        /// <summary>The lost material's unbalance in the rotor's frame (p, q), kg·m: −Σ_b S_b·ê_b; its pull is it times ω².</summary>
        public double UnbalanceP { get; private set; }
        public double UnbalanceQ { get; private set; }

        /// <summary>The flap sets' couple per ω² in the rotor's frame (p, q), kg·m².</summary>
        public double CoupleP { get; private set; }
        public double CoupleQ { get; private set; }

        /// <summary>A table point j: the thrust and torque ratios; blade b's thrust and torque shares, its thrust arm (m) and its
        /// drag per torque (1/m).</summary>
        public double ThrustRatio(int j) => _etaT[j];
        public double TorqueRatio(int j) => _etaQ[j];
        public double ThrustShare(int j, int b) => _thrustShare[b * _nj + j];
        public double ThrustArm(int j, int b) => _thrustArm[b * _nj + j];
        public double TorqueShare(int j, int b) => _torqueShare[b * _nj + j];
        public double DragPerTorque(int j, int b) => _dragPerTorque[b * _nj + j];

        /// <summary>A table point j: the blades' 1/rev hub moment per N of the rotor's thrust (p, q), m, and their in-plane drag
        /// per N·m of its torque (p, q), 1/m, in the rotor's frame.</summary>
        public void HubMomentPerThrust(int j, out double p, out double q)
        {
            p = _momentP[j];
            q = _momentQ[j];
        }

        public void HubDragPerTorque(int j, out double p, out double q)
        {
            p = _forceP[j];
            q = _forceQ[j];
        }

        /// <summary>The prop's radius, m.</summary>
        public double Radius => _radius;

        /// <summary>The intact sheet blade's C_T and C_Q at table point j (the ratios' reference).</summary>
        public double IntactThrust(int j) => _ct0[j];
        public double IntactTorque(int j) => _cq0[j];

        /// <summary>The blades as they now are into the tables and the lost material (a damaging step or a fixture; at the
        /// flight's start for the factory pitch). A sound rotor's tables are 1 and its loads 0.</summary>
        public void Rebuild()
        {
            bool sound = true;
            for (int b = 0; b < _blades; b++)
                sound &= _state[b].Whole && _state[b].FactoryPitch == 0;
            Sound = sound;
            double lostMass = 0, lostInertia = 0, up = 0, uq = 0, cp = 0, cq = 0;
            double h = _b.Span, r = _radius;
            for (int b = 0; b < _blades; b++)
            {
                BladeState s = _state[b];
                if (s.Whole) continue;
                double mass = 0, inertia = 0, first = 0, flap = 0;
                for (int k = 0; k < _n; k++)
                {
                    double x = _b.Stations[k], f = s.Inside(x, h), left = f * s.ChordShare[k];
                    double xr = x - 0.5 * h + 0.5 * f * h, fh = f * h;
                    mass += _mass[k] * (1 - left);
                    inertia += _mass[k] * ((x * x + h * h / 12) - left * (xr * xr + fh * fh / 12));
                    first += _mass[k] * (x - left * xr);
                    flap += _mass[k] * left * xr * s.FlapSet[k];
                }
                lostMass += mass;
                lostInertia += inertia * r * r;
                up -= first * r * _cos[b];
                uq -= first * r * _spin * _sin[b];
                cp -= flap * r * (-_spin * _sin[b]);
                cq -= flap * r * _cos[b];
            }
            LostMass = lostMass;
            LostInertia = lostInertia;
            UnbalanceP = up;
            UnbalanceQ = uq;
            CoupleP = cp;
            CoupleQ = cq;
            for (int j = 0; j < _nj; j++)
            {
                if (sound)
                {
                    _etaT[j] = _etaQ[j] = 1;
                    _momentP[j] = _momentQ[j] = _forceP[j] = _forceQ[j] = 0;
                    for (int b = 0; b < _blades; b++)
                    {
                        _thrustShare[b * _nj + j] = _torqueShare[b * _nj + j] = 1.0 / _blades;
                        _thrustArm[b * _nj + j] = _dragPerTorque[b * _nj + j] = 0;
                    }
                    continue;
                }
                double ct = Bemt(_advance[j] / Pi, false, out double cqr, j == 0);
                _etaT[j] = _ct0[j] > 0 ? ct / _ct0[j] : 1;
                _etaQ[j] = _cq0[j] > 0 ? cqr / _cq0[j] : 1;
                double mp = 0, mq = 0, fp = 0, fq = 0;
                for (int b = 0; b < _blades; b++)
                {
                    int at = b * _nj + j;
                    double share = ct != 0 ? _ctb[b] / ct : 0, arm = ct != 0 ? r * _ctbx[b] / ct : 0;
                    double tshare = cqr != 0 ? _cqb[b] / cqr : 0, drag = cqr != 0 ? _cqbx[b] / (r * cqr) : 0;
                    _thrustShare[at] = share;
                    _thrustArm[at] = arm;
                    _torqueShare[at] = tshare;
                    _dragPerTorque[at] = drag;
                    mp += arm * (-_spin * _sin[b]);
                    mq += arm * _cos[b];
                    fp += drag * _sin[b];
                    fq += drag * (-_spin * _cos[b]);
                }
                _momentP[j] = mp;
                _momentQ[j] = mq;
                _forceP[j] = fp;
                _forceQ[j] = fq;
            }
        }

        /// <summary>The thrust ratio at a thrust advance ratio jt and the torque ratio at a torque advance ratio jq (linear
        /// between the table's points, held at its ends: a descent keeps the static ratio, assumed).</summary>
        public void Ratios(double jt, double jq, out double thrust, out double torque)
        {
            thrust = Lerp(_etaT, jt);
            torque = Lerp(_etaQ, jq);
        }

        /// <summary>The 1/rev aerodynamic loads in the rotor's frame at a thrust (N, at advance ratio jt) and a torque (N·m, at
        /// jq): the hub moment (p, q), N·m, and the in-plane force (p, q), N.</summary>
        public void Loads(double jt, double jq, double thrust, double torque, out double momentP, out double momentQ,
            out double forceP, out double forceQ)
        {
            momentP = thrust * Lerp(_momentP, jt);
            momentQ = thrust * Lerp(_momentQ, jt);
            forceP = torque * Lerp(_forceP, jq);
            forceQ = torque * Lerp(_forceQ, jq);
        }

        /// <summary>
        /// The blade-element momentum solution at a climb inflow λ_c (J/π) for the blades as they now are (or the intact sheet
        /// blade): the rotor's C_T, returned, and C_Q; each blade's C_T, Σ dC_T·x, C_Q and Σ dC_Q/x into the blade buffers.
        /// </summary>
        public double Solve(double lambdaC, out double cq) => Bemt(lambdaC, false, out cq, false);

        /// <summary>
        /// The shares of the rotor's thrust and torque its blades make beyond a chord d (m) from the hub (p7f-damage 1.3's tip
        /// slide: the part of the disc inside a carved face makes none, the face's carve its drag): each station's static dC_T and
        /// dC_Q (BEMT at the table's first advance ratio, the blades as they now are) times the share of its annulus past the
        /// chord, acos(d/r)/π (the annulus's load even round it in a hover, the inflow the whole disc's: assumed). 0 for a chord
        /// at or past the tips.
        /// </summary>
        public void Immersed(double d, out double thrust, out double torque)
        {
            double[] dt = Sound ? _intactThrust : _stationThrust, dq = Sound ? _intactTorque : _stationTorque;
            double x0 = d / _radius, t = 0, q = 0;
            thrust = torque = 0;
            for (int k = 0; k < _n; k++)
            {
                t += dt[k];
                q += dq[k];
                double x = _b.Stations[k];
                if (!(x > x0)) continue;
                double share = DetMath.Acos(x0 / x) / Pi;
                thrust += dt[k] * share;
                torque += dq[k] * share;
            }
            thrust = t > 0 ? thrust / t : 0;
            torque = q > 0 ? torque / q : 0;
        }

        /// <summary>Blade b's C_T, Σ dC_T·x, C_Q and Σ dC_Q/x from the last Solve or Rebuild point.</summary>
        public void BladeSums(int b, out double ct, out double ctx, out double cq, out double cqx)
        {
            ct = _ctb[b];
            ctx = _ctbx[b];
            cq = _cqb[b];
            cqx = _cqbx[b];
        }

        double Bemt(double lambdaC, bool intact, out double cq, bool stations)
        {
            double a = _spec.LiftSlope, alpha0 = _spec.ZeroLiftAngle, cd0 = _spec.ProfileDrag;
            double h = _b.Span, piR = Pi * _radius;
            double ct = 0;
            cq = 0;
            for (int pass = 0; pass < TipLossPasses; pass++)
            {
                double loss = 1 - DetMath.Sqrt(2 * DetMath.Max(ct, 0)) / _blades;
                for (int b = 0; b < _blades; b++) _ctb[b] = _ctbx[b] = _cqb[b] = _cqbx[b] = 0;
                for (int k = 0; k < _n; k++)
                {
                    double x = _b.Stations[k], from = x - 0.5 * h;
                    double sigma = 0, sigmaTheta = 0;
                    for (int b = 0; b < _blades; b++)
                    {
                        Element(b, k, x, from, h, loss, intact, alpha0, out double c, out double lift, out _, out double theta);
                        sigma += c * lift;
                        sigmaTheta += c * lift * theta;
                    }
                    sigma /= piR;
                    sigmaTheta /= piR;
                    double s = sigma * a / 16 - lambdaC / 2;
                    double lambda = sigma > 0 ? DetMath.Sqrt(DetMath.Max(s * s + a * sigmaTheta * x / 8, 0)) - s : lambdaC;
                    double stationT = 0, stationQ = 0;
                    for (int b = 0; b < _blades; b++)
                    {
                        Element(b, k, x, from, h, loss, intact, alpha0, out double c, out double lift, out double profile, out double theta);
                        double dct = c * lift / piR * (a / 2) * (theta * x * x - lambda * x) * h;
                        double dcq = lambda * dct + c * profile / piR * (cd0 / 2) * x * x * x * h;
                        _ctb[b] += dct;
                        _ctbx[b] += dct * x;
                        _cqb[b] += dcq;
                        _cqbx[b] += dcq / x;
                        stationT += dct;
                        stationQ += dcq;
                    }
                    if (stations)
                    {
                        _stationThrust[k] = stationT;
                        _stationTorque[k] = stationQ;
                    }
                }
                ct = 0;
                cq = 0;
                for (int b = 0; b < _blades; b++)
                {
                    ct += _ctb[b];
                    cq += _cqb[b];
                }
            }
            return ct;
        }

        // Blade b at station k: its chord (m), the shares of the station's span that lift (inside B·x_tip) and that drag
        // (inside x_tip), and its section's angle over the zero-lift line with its offsets (rad).
        void Element(int b, int k, double x, double from, double h, double loss, bool intact, double alpha0, out double chord,
            out double lift, out double profile, out double theta)
        {
            double tip = intact ? 1 : _state[b].TipRadius;
            lift = Clamp((loss * tip - from) / h);
            profile = Clamp((tip - from) / h);
            chord = intact ? _b.Chord[k] : _b.Chord[k] * _state[b].ChordShare[k];
            theta = _b.SectionAngle[k] - alpha0;
            if (!intact) theta += _state[b].FactoryPitch + _state[b].PitchSet[k];
        }

        static double Clamp(double f) => f > 1 ? 1 : f < 0 ? 0 : f;

        // Linear between the advance ratios, held at the ends.
        double Lerp(double[] y, double at)
        {
            double[] x = _advance;
            if (!(at > x[0])) return y[0];
            if (at >= x[_nj - 1]) return y[_nj - 1];
            int k = 0;
            while (at >= x[k + 1]) k++;
            return y[k] + (at - x[k]) * (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
        }

        // Blade b's azimuth 2πb/N_b from blade 0 in the turning direction: exact on the quarter turns.
        static void Azimuth(int b, int blades, out double cos, out double sin)
        {
            if (b == 0) { cos = 1; sin = 0; return; }
            if (2 * b == blades) { cos = -1; sin = 0; return; }
            if (4 * b == blades) { cos = 0; sin = 1; return; }
            if (4 * b == 3 * blades) { cos = 0; sin = -1; return; }
            DetMath.SinCos(2 * Pi * b / blades, out sin, out cos);
        }
    }
}
