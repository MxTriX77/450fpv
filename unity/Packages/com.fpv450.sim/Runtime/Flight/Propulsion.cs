using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>The flight's motors, props and packs (drone.json's, DroneSpec.BuildPropulsion); the log header records it.</summary>
    public sealed class PropulsionSpec
    {
        /// <summary>Four, in Betaflight's Quad X order.</summary>
        public DroneMotor[] Motors;

        public DroneProps Props;

        /// <summary>Two, in parallel on the ESCs' bus (PHB-03).</summary>
        public DronePack[] Packs;
    }

    /// <summary>What the rotors did over a step's sub-steps, for the rigid body, the log and the feed.</summary>
    public struct RotorLoads
    {
        /// <summary>Each rotor's thrust along body +y, N, summed over the sub-steps.</summary>
        public Corners Thrust;

        /// <summary>The rotors' reaction on the body about +y, N·m, summed: their drag torque and spin-up.</summary>
        public double YawMoment;

        /// <summary>Each ESC's draw from the packs, A, summed.</summary>
        public Corners SupplyAmps;

        /// <summary>The packs' terminal voltage, V, and each pack's current, A, at the last sub-step.</summary>
        public double Volts, Amps0, Amps1;

        public int SubSteps;
    }

    /// <summary>
    /// The propulsion chain under the flight controller (design 5, PHB-03), per rotor, at the controller's 4 kHz:
    /// - the ESC: a first-order filter on the duty command, d += (u − d)·h/(τ_esc + h) (drone.json's τ_esc)
    /// - the motor: a DC motor at the duty's share of the bus voltage, i = (d·V − Ke·ω)/R, its torque Kt·i (Kt = Ke
    ///   in SI) against the no-load loss Kt·I0 and the prop's drag torque, J·ω̇ = Kt·i − Kt·I0 − Q. One state, ω; its
    ///   lag τ_m = J / (Kt·Ke/R + ∂Q/∂ω) follows from the load (about 40 ms at hover, Propulsion's tests), and the
    ///   update is the design's n += (n* − n)·h/(τ + h): backward Euler on the linearised motor, no transcendental
    ///   functions. The ESC drives both ways (Betaflight's DShot ESCs brake actively), so the current goes negative
    ///   when the prop is pulled down; disarmed, the ESC lets the motor freewheel (i = 0).
    /// - the prop: T = k_T(rpm)·f_T(J)·ω² and Q = κ·k_T·q(rpm)·f_Q(J)·ω², drone.json's published tables of the
    ///   static coefficients against rpm (C_T rises with rpm) and against the advance ratio J = V_a/(n·D) of the
    ///   axial inflow through the disc (the forward-flight derate, PHB-03): never a flat factor. A descending rotor
    ///   (J below 0) keeps the static slope to DescentAdvanceRatioLimit, then holds; the vortex ring replaces that
    ///   branch in p7b-airframe.
    /// - the packs: two in parallel at one bus voltage, each V_k = n_s·OCV(SoC_k) less its current through
    ///   n_s·R_cell/n_p, solved with the ESCs' load in closed form (the load is linear in V); each SoC integrates its
    ///   current. So the motors' top speed follows the sagging voltage.
    /// The body feels each rotor's thrust at its hub and, about +y, the reaction of its motor: the prop's drag torque
    /// plus the rotor's own spin-up, J·ω̇ (a clockwise rotor yaws the body left). Not yet: motor asynchrony, ground
    /// effect, the vortex ring and prop wash (p7b-airframe), the wind's inflow (1.5), the rotors' gyroscopic moment.
    /// </summary>
    public sealed class Propulsion
    {
        /// <summary>The advance ratio a descending rotor's static slope runs to (assumed: a 1.9 m/s descent at hover
        /// speed, below the vortex ring's onset; p7b-airframe's VRS replaces this branch).</summary>
        public const double DescentAdvanceRatioLimit = -0.05;

        const double TwoPi = 6.283185307179586;
        const double SecondsPerHour = 3600;

        readonly double[] _ke = new double[4], _invR = new double[4], _friction = new double[4], _inertia = new double[4];
        readonly double[] _escTau = new double[4], _reaction = new double[4], _polePairs = new double[4];
        readonly Double3[] _hub = new Double3[4];
        readonly double _kT, _kQ, _diameter;
        readonly double[] _rpm, _rpmThrust, _rpmTorque, _j, _jThrust, _jTorque;
        readonly double[] _series = new double[2], _conductance = new double[2], _capacity = new double[2];
        readonly double[][] _ocv = new double[2][];

        // The step's inflow per rotor, and each rotor's thrust and torque at its present speed.
        Corners _inflow;
        readonly double[] _thrust = new double[4], _torque = new double[4];

        public Propulsion(PropulsionSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("four motors", nameof(spec));
            if (spec.Packs == null || spec.Packs.Length != 2) throw new ArgumentException("two packs", nameof(spec));
            for (int i = 0; i < 4; i++)
            {
                DroneMotor m = spec.Motors[i];
                _ke[i] = 60 / (TwoPi * m.Kv);
                _invR[i] = 1 / m.Resistance;
                _friction[i] = _ke[i] * m.NoLoadCurrent;
                _inertia[i] = m.RotorInertia;
                _escTau[i] = m.EscTau;
                _reaction[i] = m.Clockwise ? -1 : +1;
                _polePairs[i] = m.MagnetPoles / 2;
                _hub[i] = m.Position;
            }
            DroneProps p = spec.Props;
            _kT = p.ThrustCoefficient;
            _kQ = p.TorqueRatio * p.ThrustCoefficient;
            _diameter = p.Diameter;
            _rpm = p.StaticRpm;
            _rpmThrust = p.StaticThrustRatio;
            _rpmTorque = p.StaticTorqueRatio;
            _j = p.InflowAdvanceRatio;
            _jThrust = p.InflowThrustRatio;
            _jTorque = p.InflowTorqueRatio;
            for (int k = 0; k < 2; k++)
            {
                DronePack pack = spec.Packs[k];
                _series[k] = pack.Series;
                _conductance[k] = pack.Parallel / (pack.Series * pack.CellResistance);
                _capacity[k] = pack.Parallel * pack.CellCapacity * SecondsPerHour;
                _ocv[k] = pack.Ocv;
            }
        }

        /// <summary>A rotor's hub, body m.</summary>
        public Double3 Hub(int corner) => _hub[corner];

        /// <summary>A motor's pole pairs, for its electrical frequency.</summary>
        public double PolePairs(int corner) => _polePairs[corner];

        /// <summary>The axial inflow through each disc this step, m/s, positive down through it (as in a climb).</summary>
        public ref readonly Corners Inflow => ref _inflow;

        /// <summary>A pack's open-circuit voltage at a state of charge, V: n_s × the cell's OCV table, held at its ends.</summary>
        public double OpenCircuitVolts(int pack, double charge)
        {
            double[] ocv = _ocv[pack];
            double x = charge * (ocv.Length - 1);
            if (!(x > 0)) return _series[pack] * ocv[0];
            if (x >= ocv.Length - 1) return _series[pack] * ocv[ocv.Length - 1];
            int k = (int)x;
            return _series[pack] * (ocv[k] + (x - k) * (ocv[k + 1] - ocv[k]));
        }

        /// <summary>
        /// The step's inflow from the state (the body origin's velocity in body axes plus ω × r at each hub, along
        /// the disc's normal, body +y; still air until the stand-in wind, 1.5) and the rotors' loads at their speeds.
        /// </summary>
        public void BeginStep(in FlightState s)
        {
            Double3 v = s.Attitude.InverseRotate(s.Velocity);
            Double3 w = s.BodyRate;
            for (int i = 0; i < 4; i++)
            {
                Double3 r = _hub[i];
                _inflow[i] = v.Y + (w.Z * r.X - w.X * r.Z);
                Aero(s.RotorSpeed[i], _inflow[i], out double t, out double q);
                _thrust[i] = t;
                _torque[i] = q;
            }
        }

        /// <summary>
        /// One sub-step of h seconds with the controller's duty commands (0–1); driving is false while disarmed. The
        /// state's ESC commands, rotor speeds and charges advance; loads gathers the thrust, the reaction and the
        /// currents.
        /// </summary>
        public void SubStep(ref FlightState s, in Corners duty, bool driving, double h, ref RotorLoads loads)
        {
            // The ESCs' command filters.
            for (int i = 0; i < 4; i++)
            {
                double d = s.EscCommand[i];
                s.EscCommand[i] = d + ((driving ? duty[i] : 0) - d) * h / (_escTau[i] + h);
            }

            // The bus: the packs' Thevenin source, loaded by the ESCs, I = Σ d·(d·V − Ke·ω)/R.
            double voc0 = OpenCircuitVolts(0, s.PackCharge0), voc1 = OpenCircuitVolts(1, s.PackCharge1);
            double g = _conductance[0] + _conductance[1];
            double vth = (_conductance[0] * voc0 + _conductance[1] * voc1) / g, rth = 1 / g;
            double load = 0, emf = 0;
            if (driving)
            {
                for (int i = 0; i < 4; i++)
                {
                    double d = s.EscCommand[i];
                    load += d * d * _invR[i];
                    emf += d * _ke[i] * s.RotorSpeed[i] * _invR[i];
                }
            }
            double volts = (vth + rth * emf) / (1 + rth * load);

            for (int i = 0; i < 4; i++)
            {
                double w = s.RotorSpeed[i], d = s.EscCommand[i], ke = _ke[i];
                double current = driving ? (d * volts - ke * w) * _invR[i] : 0;
                double q = _torque[i];
                double loss = w > 0 ? _friction[i] : 0;
                double rate = (ke * current - loss - q) / _inertia[i];
                double slope = -((driving ? ke * ke * _invR[i] : 0) + (w > 0 ? 2 * q / w : 0)) / _inertia[i];
                double next = w + h * rate / (1 - h * slope);
                if (next < 0) next = 0;
                Aero(next, _inflow[i], out double thrust, out double torque);
                loads.Thrust[i] += thrust;
                loads.YawMoment += _reaction[i] * (_inertia[i] * (next - w) / h + torque);
                loads.SupplyAmps[i] += d * current;
                s.RotorSpeed[i] = next;
                _thrust[i] = thrust;
                _torque[i] = torque;
            }

            double amps0 = (voc0 - volts) * _conductance[0], amps1 = (voc1 - volts) * _conductance[1];
            s.PackCharge0 -= amps0 * h / _capacity[0];
            s.PackCharge1 -= amps1 * h / _capacity[1];
            loads.Volts = volts;
            loads.Amps0 = amps0;
            loads.Amps1 = amps1;
            loads.SubSteps++;
        }

        /// <summary>A rotor's thrust, N, and drag torque, N·m, at speed w (rad/s) with axial inflow va (m/s).</summary>
        public void Aero(double w, double va, out double thrust, out double torque)
        {
            if (!(w > 0))
            {
                thrust = 0;
                torque = 0;
                return;
            }
            double rpm = w * (60 / TwoPi);
            double advance = va / (w * (_diameter / TwoPi));
            double w2 = w * w;
            thrust = _kT * Table(_rpm, _rpmThrust, rpm) * InflowRatio(_jThrust, advance) * w2;
            torque = _kQ * Table(_rpm, _rpmTorque, rpm) * InflowRatio(_jTorque, advance) * w2;
        }

        // A ratio against the advance ratio: the table, its first segment continued below 0 to the descent limit.
        double InflowRatio(double[] ratio, double advance)
        {
            if (advance >= 0) return Table(_j, ratio, advance);
            double j = DetMath.Max(advance, DescentAdvanceRatioLimit);
            return ratio[0] + j * (ratio[1] - ratio[0]) / (_j[1] - _j[0]);
        }

        // Linear between the points, held at the ends.
        static double Table(double[] x, double[] y, double at)
        {
            int n = x.Length;
            if (!(at > x[0])) return y[0];
            if (at >= x[n - 1]) return y[n - 1];
            int k = 0;
            while (at >= x[k + 1]) k++;
            return y[k] + (at - x[k]) * (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
        }
    }
}
