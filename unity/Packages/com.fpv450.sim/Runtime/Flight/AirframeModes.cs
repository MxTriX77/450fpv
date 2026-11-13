using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The airframe's small elastic modes (p7b-airframe design 5, PHY-10), from drone.json's frame (DroneSpec.BuildModes):
    /// - the arms: each a cantilever from the body's edge to the motor mount, its tip (the motor, the prop and Rayleigh's
    ///   0.2357 of the arm) bending along the rotor's axis, k = 3EI/L³ of the carbon plate (Euler–Bernoulli, published)
    /// - the payload and the coil: each a mass on its strapped mount, three ways, MountStiffness every way (D-050: the
    ///   bottle strapped rigidly on top, the coil in its canister on two hangers beneath, the canister's mass with it;
    ///   neither swings on a sling); each translates on
    ///   its mount, its own inertia turning with the frame (assumed: its rocking on the straps is left out)
    /// The rest of the drone is rigid. The modes are the free-free ones of that system about the drone's rigid motion
    /// (mean axes, Waszak and Schmidt 1988, published): the rigid body (FlightCore's, the whole drone's mass and inertia)
    /// moves exactly as without them, and the deformation moves the frame against it so that its own momentum and
    /// angular momentum are zero. Each mode is driven by the rotors' thrust at the tips less the inertial relief of every
    /// load the rigid body feels; the frame's elastic turn reaches the gyro (so the filter chain sees the frame's
    /// resonance) and the camera, and each tip's elastic velocity reaches its rotor's inflow (the prop's own damping of
    /// its arm). Never on the pose: the pose is the rigid body's.
    /// </summary>
    public sealed class AirframeModesSpec
    {
        /// <summary>The arm plate's flexural modulus, Pa: 55 GPa (assumed, inside the published 45–70 GPa of woven 3K carbon
        /// plate, the quasi-isotropic laminate the class's arms are cut from).</summary>
        public double ArmModulus = 55e9;

        /// <summary>The arm plate's width and thickness, m: 25 × 6 mm (published: the CX10 ECO class's 6 mm arm, drone.json's
        /// arm parts); and the cantilever's length from the plates' clamp to the motor mount, 0.18 m (drone.json's arm
        /// length_m, derived). The clamp is taken as rigid (assumed: a bolted clamp's give lowers the mode).</summary>
        public double ArmWidth = 0.025, ArmThickness = 0.006, ArmLength = 0.18;

        /// <summary>The arm's share of its own mass that moves with its tip: 33/140 = 0.2357 (published: Rayleigh's
        /// cantilever with a tip mass).</summary>
        public double ArmTipShare = 33.0 / 140.0;

        /// <summary>The arm's structural damping ratio: 0.02 (assumed: a carbon laminate's own 0.3–1 % with the bolted clamp's
        /// friction; the prop's aerodynamic damping of the tip comes from its inflow, apart).</summary>
        public double ArmDamping = 0.02;

        /// <summary>A strapped mount's stiffness every way: 30 kN/m (assumed: the coil's canister on its two printed hangers
        /// and the bottle's two straps through the plate, 10–100 kN/m).</summary>
        public double MountStiffness = 30000;

        /// <summary>A mount's damping ratio: 0.1 (assumed: webbing, a filled PET bottle's wall and printed plastic).</summary>
        public double MountDamping = 0.1;

        /// <summary>The camera mount's point, body m: drone.json's camera.lens_m (DroneSpec.BuildModes); this default (0, 0.014,
        /// 0.1), assumed at the plates' front edge mid-height, only for a file without one.</summary>
        public Double3 Camera = new Double3(0, 0.014, 0.1);

        /// <summary>Each arm's tip mass, kg, in Corners order: its motor's and prop's parts and ArmTipShare of its arm's
        /// (DroneSpec.BuildModes, from drone.json's parts).</summary>
        public double[] TipMass = new double[4];

        /// <summary>The payload's and the coil's parts in the mass model, −1 for none (DroneSpec.BuildModes).</summary>
        public int PayloadPart = -1, CoilPart = -1;

        /// <summary>The coil's housing (drone.json's coil.housing, the canister), −1 for none: the pack rides in it, so the
        /// coil's mount carries both, their mass at their common centre (model 64).</summary>
        public int CoilHousingPart = -1;

        /// <summary>Each arm in its plane too (ArmFlex, p7f-damage 1.6, the losses, model 72): the tip's bend across the arm and the arm's
        /// twist under the rotor's in-plane 1/rev loads. Off, the arm is stiff that way and the body takes those loads at the hub
        /// (model 68's).</summary>
        public bool ArmInPlane = true;

        /// <summary>The arm plate's in-plane shear modulus G₁₂, Pa: 4.5 GPa (assumed, inside the 3.5–5.5 GPa of woven carbon/epoxy
        /// laminates; the twist of a plate 4.2 times as wide as thick is its in-plane shear).</summary>
        public double ArmShearModulus = 4.5e9;

        /// <summary>The arm plate's flexural strength, Pa: 600 MPa (assumed: a 3K twill carbon/epoxy laminate; a single-ply 3K
        /// twill T300 prepreg publishes 882 MPa to ISO 14125, a thick cut plate's lower fibre share less), and its in-plane shear
        /// strength, Pa: 90 MPa (assumed, woven carbon/epoxy's 80–125 MPa). Past either at the root the arm breaks (ArmFlex).</summary>
        public double ArmFlexuralStrength = 600e6, ArmShearStrength = 90e6;

        /// <summary>A strike's impulse along its rotor's axis loads that arm's mode at its tip in the step it happens (w9-village-physics
        /// 2.2, G8: AirframeModes.Strike). On for every flight; off only for the kept audits of earlier models, whose arms felt a
        /// strike only through the rigid body's relief and the rotor's thrust a step later.</summary>
        public bool StrikeLoad = true;

        /// <summary>Each corner's hub above its arm's mid-plane, m; its tip's first moment of mass about the mid-plane, Σm·h (kg·m),
        /// and its tip's inertia about the arm's axis, Σ(m·h² + I_own) (kg·m²): the motor's and the prop's parts (DroneSpec.BuildModes),
        /// in Corners order.</summary>
        public double[] HubHeight = new double[4], TwistMoment = new double[4], TwistInertia = new double[4];

        public AirframeModesSpec Clone()
        {
            var c = (AirframeModesSpec)MemberwiseClone();
            c.TipMass = (double[])TipMass.Clone();
            c.HubHeight = (double[])HubHeight.Clone();
            c.TwistMoment = (double[])TwistMoment.Clone();
            c.TwistInertia = (double[])TwistInertia.Clone();
            return c;
        }

        /// <summary>The arm's bending stiffness at its tip, N/m: 3EI/L³, I = w·t³/12.</summary>
        public double ArmStiffness => 3 * ArmModulus * (ArmWidth * ArmThickness * ArmThickness * ArmThickness / 12) / (ArmLength * ArmLength * ArmLength);
    }

    /// <summary>The modes' coordinates or rates, up to AirframeModes.MaxModes, in the modes' order (lowest first).</summary>
    public struct ModeSet
    {
        public double M0, M1, M2, M3, M4, M5, M6, M7, M8, M9;

        public double this[int i]
        {
            readonly get
            {
                switch (i)
                {
                    case 0: return M0;
                    case 1: return M1;
                    case 2: return M2;
                    case 3: return M3;
                    case 4: return M4;
                    case 5: return M5;
                    case 6: return M6;
                    case 7: return M7;
                    case 8: return M8;
                    case 9: return M9;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
            set
            {
                switch (i)
                {
                    case 0: M0 = value; break;
                    case 1: M1 = value; break;
                    case 2: M2 = value; break;
                    case 3: M3 = value; break;
                    case 4: M4 = value; break;
                    case 5: M5 = value; break;
                    case 6: M6 = value; break;
                    case 7: M7 = value; break;
                    case 8: M8 = value; break;
                    case 9: M9 = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
        }
    }

    /// <summary>
    /// The airframe's modes for one flight's body (AirframeModesSpec), solved once at the start: the elastic degrees of
    /// freedom (four arm tips along +y, then the payload's and the coil's three each when they have mass), their mass
    /// matrix with the rigid body free, M̃ = M − Bᵀ·M_rr⁻¹·B (B the rigid motion each one's mass rides), and their stiffness
    /// K; K·φ = ω²·M̃·φ by Cholesky and Jacobi's rotations (basic operations and square roots only), the modes mass-normalised,
    /// each damped by its share of the dampers (classical damping). Each step moves the modes by semi-implicit Euler, the
    /// damping implicit. Holds constants only: the modes' state is the FlightState's. Rebuild solves them again for a new
    /// mass model (the coil as the fiber pays out, p7d-tether 1.6) into the same buffers, allocating nothing.
    /// </summary>
    public sealed class AirframeModes
    {
        public const int MaxModes = 10;

        readonly AirframeModesSpec _spec;
        readonly Double3[] _hubs;
        int _n;
        readonly double[] _omega2 = new double[MaxModes], _twoZetaOmega = new double[MaxModes], _frequency = new double[MaxModes];
        readonly double[,] _tipForce = new double[MaxModes, 4]; // [mode, corner]: the modal force a newton of a rotor's thrust makes
        readonly double[,] _relief = new double[MaxModes, 6];   // [mode, 6]: the modal force a newton (body) or a newton-metre about the CoM makes
        readonly double[,] _frame = new double[6, MaxModes];    // [6, mode]: the frame's elastic translation (body m) and turn (rad) a unit mode makes
        readonly double[,] _hub = new double[4, MaxModes];      // [corner, mode]: each hub's elastic displacement along +y a unit mode makes
        Double3 _camera;                                         // the camera from the CoM, body m

        // The solve's buffers, kept for Rebuild: each degree of freedom's mass, point, direction, stiffness and damper; B and
        // M_rr⁻¹·B; M̃; its Cholesky factor and inverse; the eigenproblem; Φ (and the last build's, for the state's map).
        readonly double[] _m = new double[MaxModes], _k = new double[MaxModes], _c = new double[MaxModes];
        readonly Double3[] _r = new Double3[MaxModes], _u = new Double3[MaxModes];
        readonly double[,] _b = new double[6, MaxModes], _mb = new double[6, MaxModes];
        readonly double[,] _mt = new double[MaxModes, MaxModes], _l = new double[MaxModes, MaxModes], _linv = new double[MaxModes, MaxModes];
        readonly double[,] _am = new double[MaxModes, MaxModes], _v = new double[MaxModes, MaxModes], _vs = new double[MaxModes, MaxModes];
        readonly double[,] _phi = new double[MaxModes, MaxModes];
        readonly double[] _lambda = new double[MaxModes], _sorted = new double[MaxModes];
        readonly int[] _order = new int[MaxModes];
        readonly double[] _x = new double[MaxModes], _xd = new double[MaxModes];

        public AirframeModes(AirframeModesSpec spec, MassModel mass, Double3[] hubs)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _hubs = (Double3[])hubs.Clone();
            Build(mass);
        }

        /// <summary>
        /// The modes again for the mass model as it now is (a part's mass changed), the state carried by each degree of
        /// freedom's elastic deflection and its rate, x = Φ·q, so q' = Φ'ᵀ·M̃'·x (Φ'ᵀ·M̃'·Φ' = I): the frame keeps its shape
        /// and motion across the rebuild. A body whose degrees of freedom changed in number starts its modes at rest.
        /// </summary>
        public void Rebuild(MassModel mass, ref ModeSet q, ref ModeSet rate)
        {
            int was = _n;
            for (int a = 0; a < was; a++)
            {
                double x = 0, xd = 0;
                for (int mode = 0; mode < was; mode++)
                {
                    x += _phi[a, mode] * q[mode];
                    xd += _phi[a, mode] * rate[mode];
                }
                _x[a] = x;
                _xd[a] = xd;
            }
            Build(mass);
            q = default;
            rate = default;
            if (_n != was) return;
            for (int mode = 0; mode < _n; mode++)
            {
                double y = 0, yd = 0;
                for (int a = 0; a < _n; a++)
                {
                    double w = 0;
                    for (int e = 0; e < _n; e++) w += _mt[a, e] * _phi[e, mode];
                    y += w * _x[a];
                    yd += w * _xd[a];
                }
                q[mode] = y;
                rate[mode] = yd;
            }
        }

        void Build(MassModel mass)
        {
            AirframeModesSpec spec = _spec;
            Double3[] hubs = _hubs;
            ref readonly MassProperties body = ref mass.Properties;
            Double3 com = body.CenterOfMass;
            Sym3 inv = body.InverseInertia;
            double invM = body.InverseMass;
            Array.Clear(_l, 0, _l.Length);
            Array.Clear(_linv, 0, _linv.Length);
            Array.Clear(_am, 0, _am.Length);
            Array.Clear(_v, 0, _v.Length);
            Array.Clear(_vs, 0, _vs.Length);
            Array.Clear(_phi, 0, _phi.Length);
            Array.Clear(_mt, 0, _mt.Length);

            // The elastic degrees of freedom: each one's mass, its point from the CoM, its direction, stiffness and damper.
            double[] m = _m, k = _k, c = _c;
            Double3[] r = _r, u = _u;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                m[n] = spec.TipMass[i];
                r[n] = hubs[i] - com;
                u[n] = new Double3(0, 1, 0);
                k[n] = spec.ArmStiffness;
                c[n] = 2 * spec.ArmDamping * DetMath.Sqrt(k[n] * m[n]);
                n++;
            }
            for (int which = 0; which < 2; which++)
            {
                int part = which == 0 ? spec.PayloadPart : spec.CoilPart;
                if (part < 0 || !(mass.Part(part).Mass > 0)) continue;
                ref readonly DronePart p = ref mass.Part(part);
                double mount = p.Mass;
                Double3 at = p.Position;
                if (which == 1 && spec.CoilHousingPart >= 0)
                {
                    ref readonly DronePart h = ref mass.Part(spec.CoilHousingPart);
                    mount = p.Mass + h.Mass;
                    at = (p.Position * p.Mass + h.Position * h.Mass) * (1 / mount);
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    m[n] = mount;
                    r[n] = at - com;
                    u[n] = axis == 0 ? new Double3(1, 0, 0) : axis == 1 ? new Double3(0, 1, 0) : new Double3(0, 0, 1);
                    k[n] = spec.MountStiffness;
                    c[n] = 2 * spec.MountDamping * DetMath.Sqrt(k[n] * m[n]);
                    n++;
                }
            }
            _n = n;

            // B's columns: the rigid motion (translation, turn about the CoM) each degree of freedom's mass rides, m·u and m·r×u.
            double[,] b = _b;
            for (int a = 0; a < n; a++)
            {
                Double3 ru = Double3.Cross(r[a], u[a]);
                b[0, a] = m[a] * u[a].X;
                b[1, a] = m[a] * u[a].Y;
                b[2, a] = m[a] * u[a].Z;
                b[3, a] = m[a] * ru.X;
                b[4, a] = m[a] * ru.Y;
                b[5, a] = m[a] * ru.Z;
            }
            // M_rr⁻¹·B, 6 × n.
            double[,] mb = _mb;
            for (int a = 0; a < n; a++)
            {
                mb[0, a] = b[0, a] * invM;
                mb[1, a] = b[1, a] * invM;
                mb[2, a] = b[2, a] * invM;
                Double3 w = inv * new Double3(b[3, a], b[4, a], b[5, a]);
                mb[3, a] = w.X;
                mb[4, a] = w.Y;
                mb[5, a] = w.Z;
            }
            double[,] mt = _mt;
            for (int a = 0; a < n; a++)
                for (int e = 0; e < n; e++)
                {
                    double s = a == e ? m[a] : 0;
                    for (int j = 0; j < 6; j++) s -= b[j, a] * mb[j, e];
                    mt[a, e] = s;
                }

            // Cholesky, M̃ = L·Lᵀ; A = L⁻¹·K·L⁻ᵀ (K diagonal); A = V·Λ·Vᵀ; Φ = L⁻ᵀ·V, so Φᵀ·M̃·Φ = I and Φᵀ·K·Φ = Λ.
            double[,] l = _l;
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++)
                {
                    double s = mt[i, j];
                    for (int q = 0; q < j; q++) s -= l[i, q] * l[j, q];
                    l[i, j] = i == j ? DetMath.Sqrt(s) : s / l[j, j];
                }
            double[,] linv = _linv;
            LowerInverse(l, n, linv);
            double[,] am = _am;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int q = 0; q < n; q++) s += linv[i, q] * k[q] * linv[j, q];
                    am[i, j] = s;
                }
            Jacobi(am, n, _v, _lambda, _order, _sorted, _vs);
            double[] lambda = _sorted;
            double[,] v = _vs;
            double[,] phi = _phi; // [dof, mode]
            for (int a = 0; a < n; a++)
                for (int mode = 0; mode < n; mode++)
                {
                    double s = 0;
                    for (int q = 0; q < n; q++) s += linv[q, a] * v[q, mode];
                    phi[a, mode] = s;
                }

            for (int mode = 0; mode < n; mode++)
            {
                double w2 = lambda[mode], w = DetMath.Sqrt(w2);
                double cm = 0;
                for (int a = 0; a < n; a++) cm += phi[a, mode] * c[a] * phi[a, mode];
                _omega2[mode] = w2;
                _twoZetaOmega[mode] = cm; // 2ζω = Φᵀ·C·Φ on the diagonal, the modes mass-normalised
                _frequency[mode] = w / 6.283185307179586;
                for (int i = 0; i < 4; i++) _tipForce[mode, i] = phi[i, mode];
                // The relief: Q = Φᵀ·(F_e − Bᵀ·M_rr⁻¹·F_r), so a unit generalised force F_r,j gives −Σ_a φ_a·(M_rr⁻¹·B)_ja.
                for (int j = 0; j < 6; j++)
                {
                    double s = 0, f = 0;
                    for (int a = 0; a < n; a++)
                    {
                        s -= phi[a, mode] * mb[j, a];
                        f -= mb[j, a] * phi[a, mode];
                    }
                    _relief[mode, j] = s;
                    _frame[j, mode] = f;
                }
                for (int i = 0; i < 4; i++)
                    _hub[i, mode] = phi[i, mode] + _frame[1, mode] + _frame[5, mode] * r[i].X - _frame[3, mode] * r[i].Z;
            }
            _camera = spec.Camera - com;
            _n = n;
        }

        /// <summary>The modes this body has: 4 with no payload or coil, up to MaxModes.</summary>
        public int Count => _n;

        /// <summary>A mode's natural frequency, Hz (lowest first), and its damping ratio.</summary>
        public double Frequency(int mode) => _frequency[mode];
        public double Damping(int mode) => _twoZetaOmega[mode] / (2 * DetMath.Sqrt(_omega2[mode]));

        /// <summary>
        /// One step of dt seconds: the modes under the rotors' thrust at their tips (N, along +y) and the inertial relief of
        /// the rigid body's whole load, its force (body N, gravity aside, which loads every mass alike and drives no mode) and
        /// its moment about the CoM (body N·m); the modes' accelerations into accel, for the camera.
        /// </summary>
        public void Advance(ref ModeSet q, ref ModeSet rate, out ModeSet accel, in Corners thrust, in Double3 force, in Double3 moment, double dt)
        {
            accel = default;
            for (int mode = 0; mode < _n; mode++)
            {
                double f = _relief[mode, 0] * force.X + _relief[mode, 1] * force.Y + _relief[mode, 2] * force.Z
                    + _relief[mode, 3] * moment.X + _relief[mode, 4] * moment.Y + _relief[mode, 5] * moment.Z;
                for (int i = 0; i < 4; i++) f += _tipForce[mode, i] * thrust[i];
                double x = q[mode], v = rate[mode];
                double next = (v + dt * (f - _omega2[mode] * x)) / (1 + dt * _twoZetaOmega[mode]);
                accel[mode] = (next - v) / dt;
                rate[mode] = next;
                q[mode] = x + dt * next;
            }
        }

        /// <summary>
        /// The step's strikes on the arms' tips (w9-village-physics 2.2, G8): each corner's impulse along +y (body N·s, its strikes'
        /// share along its rotor's axis, PropStrikes.TipImpulse) loads the modes in the step it happens, as Advance's thrust at the tip
        /// does over a step: the modal impulse Φᵢ·P at the tip and the relief of the rigid body's share (the impulse and its moment
        /// about the CoM, which the body has already taken). The rates change at once; the accelerations gain the change over dt, so
        /// the camera sees the hit in this step. Allocation-free.
        /// </summary>
        public void Strike(ref ModeSet rate, ref ModeSet accel, in Corners impulse, double dt)
        {
            for (int mode = 0; mode < _n; mode++)
            {
                double p = 0;
                for (int i = 0; i < 4; i++)
                {
                    double j = impulse[i];
                    if (j == 0) continue;
                    Double3 r = _r[i], f = new Double3(0, j, 0), m = Double3.Cross(r, f);
                    p += _tipForce[mode, i] * j + _relief[mode, 1] * j + _relief[mode, 3] * m.X + _relief[mode, 4] * m.Y + _relief[mode, 5] * m.Z;
                }
                rate[mode] = rate[mode] + p;
                accel[mode] = accel[mode] + p / dt;
            }
        }

        /// <summary>The frame's elastic turn rate, body rad/s, at the modes' rates (the gyro's and the camera's share).</summary>
        public Double3 FrameRate(in ModeSet rate)
        {
            double x = 0, y = 0, z = 0;
            for (int mode = 0; mode < _n; mode++)
            {
                x += _frame[3, mode] * rate[mode];
                y += _frame[4, mode] * rate[mode];
                z += _frame[5, mode] * rate[mode];
            }
            return new Double3(x, y, z);
        }

        /// <summary>The frame's elastic turn, body rad (small angles about x, y, z), at the modes' coordinates: the angle
        /// FrameRate turns at (the camera's elastic tilt on the frame, p7e-camera-feel).</summary>
        public Double3 FrameTurn(in ModeSet q) => FrameRate(q);

        /// <summary>A hub's elastic velocity along +y, m/s, at the modes' rates (its rotor's inflow gains it).</summary>
        public double HubVelocity(in ModeSet rate, int corner)
        {
            double s = 0;
            for (int mode = 0; mode < _n; mode++) s += _hub[corner, mode] * rate[mode];
            return s;
        }

        /// <summary>A hub's elastic deflection along +y, m, at the modes' coordinates.</summary>
        public double HubDeflection(in ModeSet q, int corner)
        {
            double s = 0;
            for (int mode = 0; mode < _n; mode++) s += _hub[corner, mode] * q[mode];
            return s;
        }

        /// <summary>The camera's elastic acceleration, body m/s², at the modes' accelerations: the frame's translation
        /// and its turn's at the camera (the turn's rate is small enough that its centripetal share is left out).</summary>
        public Double3 CameraAcceleration(in ModeSet accel)
        {
            Double3 t = default, a = default;
            for (int mode = 0; mode < _n; mode++)
            {
                double e = accel[mode];
                t = t + new Double3(_frame[0, mode] * e, _frame[1, mode] * e, _frame[2, mode] * e);
                a = a + new Double3(_frame[3, mode] * e, _frame[4, mode] * e, _frame[5, mode] * e);
            }
            return t + Double3.Cross(a, _camera);
        }

        // The inverse of a lower-triangular matrix, by forward substitution, into x (zeroed by the caller).
        static void LowerInverse(double[,] l, int n, double[,] x)
        {
            for (int col = 0; col < n; col++)
                for (int i = col; i < n; i++)
                {
                    double s = i == col ? 1 : 0;
                    for (int q = col; q < i; q++) s -= l[i, q] * x[q, col];
                    x[i, col] = s / l[i, i];
                }
        }

        // A symmetric matrix's eigenvalues (ascending, into sorted) and unit eigenvectors (columns, into vs), by cyclic Jacobi
        // rotations in a fixed order (Golub and Van Loan's symmetric Schur decomposition): basic operations and square roots
        // only. v and vs zeroed by the caller.
        static void Jacobi(double[,] a, int n, double[,] v, double[] lambda, int[] order, double[] sorted, double[,] vs)
        {
            for (int i = 0; i < n; i++) v[i, i] = 1;
            for (int sweep = 0; sweep < 100; sweep++)
            {
                double off = 0, diag = 0;
                for (int p = 0; p < n; p++)
                {
                    diag += a[p, p] * a[p, p];
                    for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
                }
                if (!(off > 1e-30 * diag)) break;
                for (int p = 0; p < n - 1; p++)
                    for (int q = p + 1; q < n; q++)
                    {
                        double apq = a[p, q];
                        if (apq == 0) continue;
                        double tau = (a[q, q] - a[p, p]) / (2 * apq);
                        double t = (tau >= 0 ? 1 : -1) / (DetMath.Abs(tau) + DetMath.Sqrt(1 + tau * tau));
                        double cs = 1 / DetMath.Sqrt(1 + t * t), sn = t * cs;
                        for (int j = 0; j < n; j++)
                        {
                            double ap = a[p, j], aq = a[q, j];
                            a[p, j] = cs * ap - sn * aq;
                            a[q, j] = sn * ap + cs * aq;
                        }
                        for (int j = 0; j < n; j++)
                        {
                            double ap = a[j, p], aq = a[j, q];
                            a[j, p] = cs * ap - sn * aq;
                            a[j, q] = sn * ap + cs * aq;
                        }
                        for (int j = 0; j < n; j++)
                        {
                            double vp = v[j, p], vq = v[j, q];
                            v[j, p] = cs * vp - sn * vq;
                            v[j, q] = sn * vp + cs * vq;
                        }
                    }
            }
            for (int i = 0; i < n; i++)
            {
                lambda[i] = a[i, i];
                order[i] = i;
            }
            // Ascending, ties by index (an insertion sort: a fixed order on every runtime).
            for (int i = 1; i < n; i++)
            {
                int o = order[i];
                int j = i - 1;
                while (j >= 0 && (lambda[order[j]] > lambda[o])) order[j + 1] = order[j--];
                order[j + 1] = o;
            }
            for (int i = 0; i < n; i++)
            {
                sorted[i] = lambda[order[i]];
                for (int j = 0; j < n; j++) vs[j, i] = v[j, order[i]];
            }
        }
    }
}
