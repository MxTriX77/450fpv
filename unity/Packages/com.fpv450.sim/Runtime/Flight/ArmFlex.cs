namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Each arm in its plane under the rotor's turning loads (p7f-damage 1.6, the losses, model 72; AirframeModesSpec.ArmInPlane): the arm a
    /// cantilever from the plates' clamp, its tip (the motor, the prop and Rayleigh's share of the arm) with two degrees of freedom,
    /// the tip's bend across the arm in the arm plane (x, along lat = arm × ŷ) and the arm's twist about its own axis (θ), so a
    /// point h above the arm's mid-plane moves x + h·θ across it. Their mass matrix is the tip's parts' [[Σm, Σm·h], [Σm·h,
    /// Σ(m·h² + I)]] (DroneSpec.BuildModes), their stiffness 3EI/L³ of the plate across its width and GK/L (Roark's rectangle's K)
    /// of its twist, each damped at ArmDamping. AirframeModes keeps the bend along the rotor's axis; this is the plate's other two
    /// ways, which only the in-plane loads drive.
    ///
    /// The loads (Propulsion.SubStep, each sub-step): the in-plane force the rotor's 1/rev puts on its hub (the factory unbalance,
    /// a bent shaft, a damaged rotor's lost mass and blade drag) and its couples. Its part along the arm goes to the body at the
    /// hub (the plate is stiff that way: EA/L ≈ 46 MN/m, 2.9 kHz, derived); its part across the arm, with the thrust tilted by
    /// the twist (T·θ across), drives the tip at the hub's height, and the couples' part about the arm's axis drives the twist.
    /// The body takes what the arm carries: the bend's spring and damper across the arm at its tip in the mid-plane, and the
    /// twist's about the arm's axis; the couples' other parts, and the turning rotor's gyroscopic reaction to the twist's rate,
    /// −θ̇·H·lat (H its spin momentum along +y; the arm's bend along the rotor's axis is stiff to it), go to the body at once.
    /// Assumed: the tip's motion relieves the rigid body's acceleration not at all (the tip is 3 % of the drone's mass; the
    /// body's own 1/rev acceleration on the tip is about 3 % of the load), the tip's inertia stays the built one when a blade is
    /// lost (5 % of it), and the clamp is rigid (AirframeModesSpec's).
    ///
    /// The arm's root (Roark): the bend's moment k_b·x·L over the plate's section modulus t·w²/6, and the twist's torque k_t·θ at
    /// the long face's middle, 3T/(8ab²)·(1 + 0.6095β + 0.8865β² − 1.8023β³ + 0.9100β⁴), β = b/a (Roark's rectangle 2a × 2b).
    /// Their share of ArmFlexuralStrength and ArmShearStrength is the arm's load; past 1 the arm breaks at its clamp: Broken
    /// carries it, the core logs ArmBroken and its corner's prop is lost (Propulsion.LoseProp: assumed, the motor hangs on its
    /// leads and lifts nothing) and the broken arm carries nothing more. Its state is the FlightState's (ArmBend, ArmTwist, their
    /// rates, ArmBroken); this holds constants and the step's peaks only. Semi-implicit Euler at the sub-step: its modes, 88 and
    /// 318 Hz on the repository's drone (derived), are far inside its 4 kHz.
    /// </summary>
    public sealed class ArmFlex
    {
        readonly Double3[] _arm = new Double3[4], _lat = new Double3[4], _tip = new Double3[4], _hubs = new Double3[4];
        readonly double[] _height = new double[4], _kb = new double[4], _kt = new double[4], _cb = new double[4], _ct = new double[4];
        readonly double[] _ia = new double[4], _ib = new double[4], _id = new double[4];
        readonly double _bendStress, _twistStress, _flexural, _shear;
        Corners _peak;
        int _breaking;

        public ArmFlex(AirframeModesSpec spec, Double3[] hubs)
        {
            double w = spec.ArmWidth, t = spec.ArmThickness, l = spec.ArmLength;
            double a = w / 2, b = t / 2, beta = b / a;
            double kTwist = a * b * b * b * (16.0 / 3 - 3.36 * beta * (1 - b * b * b * b / (12 * a * a * a * a)));
            double kb = 3 * spec.ArmModulus * (t * w * w * w / 12) / (l * l * l), kt = spec.ArmShearModulus * kTwist / l;
            _bendStress = kb * l / (t * w * w / 6);
            _twistStress = kt * 3 / (8 * a * b * b) * (1 + 0.6095 * beta + 0.8865 * beta * beta - 1.8023 * beta * beta * beta + 0.9100 * beta * beta * beta * beta);
            _flexural = spec.ArmFlexuralStrength;
            _shear = spec.ArmShearStrength;
            for (int i = 0; i < 4; i++)
            {
                double r = DetMath.Sqrt(hubs[i].X * hubs[i].X + hubs[i].Z * hubs[i].Z);
                _arm[i] = new Double3(hubs[i].X / r, 0, hubs[i].Z / r);
                _lat[i] = Double3.Cross(_arm[i], new Double3(0, 1, 0));
                _hubs[i] = hubs[i];
                _height[i] = spec.HubHeight[i];
                _tip[i] = new Double3(hubs[i].X, hubs[i].Y - spec.HubHeight[i], hubs[i].Z);
                double m = spec.TipMass[i], s = spec.TwistMoment[i], j = spec.TwistInertia[i], det = m * j - s * s;
                _ia[i] = j / det;
                _ib[i] = -s / det;
                _id[i] = m / det;
                _kb[i] = kb;
                _kt[i] = kt;
                _cb[i] = 2 * spec.ArmDamping * DetMath.Sqrt(kb * m);
                _ct[i] = 2 * spec.ArmDamping * DetMath.Sqrt(kt * j);
            }
        }

        /// <summary>The stiffness across the arm at its tip, N/m, and of its twist, N·m/rad.</summary>
        public double BendStiffness => _kb[0];
        public double TwistStiffness => _kt[0];

        /// <summary>The root's stress per metre of bend and per radian of twist, Pa.</summary>
        public double BendStress => _bendStress;
        public double TwistStress => _twistStress;

        /// <summary>The arms the last step broke, bit 1 &lt;&lt; corner (the core logs them and loses their props).</summary>
        public int Breaking => _breaking;

        /// <summary>Each arm's largest load over the step (its stress's share of the strength), set by BeginStep's caller.</summary>
        public ref readonly Corners Peak => ref _peak;

        public void BeginStep()
        {
            _peak = default;
            _breaking = 0;
        }

        /// <summary>
        /// One sub-step of h s of rotor i's arm with its in-plane force f (body N, at the hub) and couple (body N·m), its thrust
        /// (N, along the rotor's axis) and spin momentum (N·m·s along +y): the arm moves, and what it and the rotor put on the
        /// body goes into loads (force at the body's origin's frame, moment about the body's origin).
        /// </summary>
        public void Carry(ref FlightState s, int i, in Double3 f, in Double3 couple, double thrust, double spin, double h, ref RotorLoads loads)
        {
            int bit = 1 << i;
            if ((s.ArmBroken & bit) != 0) return;
            Double3 arm = _arm[i], lat = _lat[i];
            double x = s.ArmBend[i], v = s.ArmBendRate[i], th = s.ArmTwist[i], vt = s.ArmTwistRate[i];
            double across = Double3.Dot(f, lat) + thrust * th, along = Double3.Dot(f, arm), about = Double3.Dot(couple, arm);
            double ex = across - _kb[i] * x - _cb[i] * v, et = _height[i] * across + about - _kt[i] * th - _ct[i] * vt;
            v += h * (_ia[i] * ex + _ib[i] * et);
            vt += h * (_ib[i] * ex + _id[i] * et);
            x += h * v;
            th += h * vt;
            s.ArmBend[i] = x;
            s.ArmBendRate[i] = v;
            s.ArmTwist[i] = th;
            s.ArmTwistRate[i] = vt;

            Double3 radial = arm * along, carried = lat * (_kb[i] * x + _cb[i] * v);
            loads.UnbalanceForce = loads.UnbalanceForce + radial + carried;
            loads.UnbalanceMoment = loads.UnbalanceMoment + Double3.Cross(_hubs[i], radial) + Double3.Cross(_tip[i], carried)
                + (couple - arm * about) + arm * (_kt[i] * th + _ct[i] * vt) + lat * (-vt * spin);

            double load = DetMath.Max(DetMath.Abs(x) * _bendStress / _flexural, DetMath.Abs(th) * _twistStress / _shear);
            if (load > _peak[i]) _peak[i] = load;
            if (load < 1) return;
            s.ArmBroken |= (byte)bit;
            _breaking |= bit;
        }

    }
}
