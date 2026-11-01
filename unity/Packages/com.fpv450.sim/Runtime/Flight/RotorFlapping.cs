using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The rotors' edgewise-flow moments (p7b-airframe 1.3c, PHY-17a's gust response): blade flapping and the dissymmetry of
    /// lift per rotor, from blade-element theory on the props' sheet, inside the inflow seam (Propulsion.HubMoment).
    /// - The blade: linear lift, dL = ½ρ·a·c·(ΩR)²·(u_T²·θ − u_T·u_P) per unit span, u_T = x + μ·sin ψ, u_P = λ_c + λ_i·(1 +
    ///   κ_x·x·cos ψ) + x·β′ + μ·β·cos ψ (x = r/R; ψ from downstream; μ the hub's in-plane airspeed and λ the axial inflow over
    ///   ΩR; β the flap). θ is the section's angle over its zero-lift line, the 4.5-inch helix less α_L0, on the sheet's
    ///   chord (Stations, Chords, Angles: linear between). Johnson, W., Helicopter Theory, Princeton 1980, §5.2–5.4
    ///   (published: the flap equation and its harmonics in forward flight).
    /// - The flap: a spring-restrained hinge at the hub, I_b·Ω²·(β″ + ν²·β) = M_β, ν² = 1 + K_β/(I_b·Ω²) (Johnson's
    ///   hingeless model, published). The prop's flapwise stiffness is its blade's bending: K_β = I_b·(ω₀² + (K_s − 1)·Ω²),
    ///   ω₀ its non-rotating first bending mode, K_s the Southwell coefficient of a cantilever's first mode. Quasi-steady
    ///   (the flap mode settles in ~16 ms, assumed fast against the body): the 0/rev and 1/rev harmonics.
    ///   - Coning: I_b·Ω²·ν²·β₀ = A·(J₃ + μ²·J₁/2 − λ·I₂).
    ///   - 1/rev: K_β·β₁c = −A·(κ_x·λ_i·I₃ + μ·β₀·I₂ + (I₃ + μ²·I₁/4)·β₁s); K_β·β₁s = A·(μ·(2·J₂ − λ·I₁) + (I₃ − μ²·I₁/4)·β₁c);
    ///     A = ½ρ·a·Ω²·R⁴, I_n = ∫c·xⁿ dx, J_n = ∫c·θ·xⁿ dx. At K_β = 0 these are Johnson's articulated flapping exactly
    ///     (β₁c = −μ(8θ₀/3 + 2θ_tw − 2λ)/(1 − μ²/2), β₁s = −(4μβ₀/3 + λ_x)/(1 + μ²/2) on a constant chord); as K_β → ∞ the
    ///     rigid rotor's hub moments (the dissymmetry of lift, C_Mx/σa = μ(θ₀/6 + θ_tw/8 − λ/8)).
    /// - The hub moment, averaged over a turn: (N_b/2)·K_β·(s·β₁s·X − β₁c·Y), X the in-plane airflow's direction (downstream),
    ///   Y = body +y × X, s +1 for a clockwise rotor (a positive rotation about +y), −1 counter-clockwise. The rolling part (the
    ///   dissymmetry, along X) changes sign with the spin, so it cancels between a quad's opposite pairs; the pitching part
    ///   (along Y: the disc tilting away from the airflow, back flapping) is the same for both and adds over the four. A
    ///   2-blade rotor's 2/rev part is left out (assumed: 300 Hz at hover, above the body's and the loop's band).
    /// - The inflow: λ_i from Glauert's v_i = v_h²/√(V_∥² + (V_a + v_i)²) at the rotor's thrust (momentum theory, published),
    ///   six relaxed passes from v_h with the root held at ½v_h or more (assumed: descent, where the ring takes over); the skew
    ///   κ_x = (15π/32)·tan(χ/2), tan χ = μ/λ (Pitt, D. M., Peters, D. A., Vertica 5, 1981, published), χ held at 90°.
    /// - The rotor's in-plane force stays the H-force (DroneProps.HForce, at the hub): the flapping adds only its hub moment,
    ///   not the tilted thrust's in-plane part T·β₁c (that is inside the H-force in shaft axes, so it is not counted twice).
    /// Basic operations only; ApplyTo is per step, allocation-free.
    /// </summary>
    public sealed class RotorFlappingSpec
    {
        const double Pi = 3.141592653589793;

        /// <summary>The sections' lift slope, per rad: 5.7 (assumed, as p7c-wind 1.6's blade-element H-force).</summary>
        public double LiftSlope = 5.7;

        /// <summary>The sections' zero-lift angle, rad: −6.3° (derived: p7c-wind 1.6 fitted it with c_d0 so the same elements
        /// give APC's published static 12.43 N and κ 0.0144 m at 9000 rpm).</summary>
        public double ZeroLiftAngle = -0.10995574287564276;

        /// <summary>The blade's non-rotating first flapwise bending mode, Hz: 150 (assumed: a glass-filled nylon cantilever,
        /// E 8–10 GPa, the sheet's root chord at about 11 % thickness, 100–200 Hz with its taper).</summary>
        public double BendingFrequency = 150;

        /// <summary>The first flap mode's Southwell coefficient: 1.193 (published: a uniform cantilever's, Bramwell,
        /// Helicopter Dynamics, 1976; the tapered blade's, assumed).</summary>
        public double Southwell = 1.193;

        /// <summary>The longitudinal inflow's gain on tan(χ/2): 15π/32 (published: Pitt and Peters 1981).</summary>
        public double SkewGain = 1.4726215563702154;

        /// <summary>The prop's radius, m, and its blades (drone.json's props).</summary>
        public double Radius = 0.127;
        public int Blades = 2;

        /// <summary>A blade's flap inertia about the hub, kg·m²: the prop's mass as a uniform rod (drone.json's prop part,
        /// derived): m/N_b·R²/3.</summary>
        public double BladeInertia = 4.3e-5;

        /// <summary>The blade's stations x = r/R, its chord there, m, and its section angle over the zero-lift line, rad
        /// (linear between; the integrals run from the first station to the last). DroneSpec.BuildFlapping: the sheet's chord
        /// (26 mm at 0.3 R tapering to 8 mm at the tip; 18 mm at 0.1 R assumed) and the 4.5-inch helix less ZeroLiftAngle.</summary>
        public double[] Stations, Chords, Angles;

        /// <summary>The air's density, kg/m³ (drone.json's aero air_density_kg_m3, 1.225, published ISA).</summary>
        public double AirDensity = 1.225;

        double _i1, _i2, _i3, _j1, _j2, _j3;
        bool _ready;

        public RotorFlappingSpec Clone()
        {
            var c = (RotorFlappingSpec)MemberwiseClone();
            c.Stations = (double[])Stations?.Clone();
            c.Chords = (double[])Chords?.Clone();
            c.Angles = (double[])Angles?.Clone();
            c._ready = false;
            return c;
        }

        /// <summary>The chord's moments I₁…I₃ and the chord-weighted angle's J₁…J₃ over the blade (3-point Gauss–Legendre on 16
        /// cells a segment: exact for the linear chord and angle's products up to x³).</summary>
        public void Integrals(out double i1, out double i2, out double i3, out double j1, out double j2, out double j3)
        {
            Prepare();
            i1 = _i1; i2 = _i2; i3 = _i3; j1 = _j1; j2 = _j2; j3 = _j3;
        }

        void Prepare()
        {
            if (_ready) return;
            if (Stations == null || Chords == null || Angles == null || Stations.Length < 2
                || Chords.Length != Stations.Length || Angles.Length != Stations.Length)
                throw new InvalidOperationException("the blade needs two stations or more, each with a chord and an angle");
            double g = DetMath.Sqrt(0.6) / 2;
            double i1 = 0, i2 = 0, i3 = 0, j1 = 0, j2 = 0, j3 = 0;
            const int Cells = 16;
            for (int k = 0; k + 1 < Stations.Length; k++)
            {
                double x0 = Stations[k], dx = (Stations[k + 1] - x0) / Cells;
                for (int n = 0; n < Cells; n++)
                {
                    double mid = x0 + (n + 0.5) * dx;
                    for (int q = 0; q < 3; q++)
                    {
                        double x = q == 0 ? mid - g * dx : q == 1 ? mid : mid + g * dx;
                        double w = (q == 1 ? 8.0 / 18 : 5.0 / 18) * dx;
                        double t = (x - x0) / (Stations[k + 1] - x0);
                        double c = Chords[k] + (Chords[k + 1] - Chords[k]) * t;
                        double th = Angles[k] + (Angles[k + 1] - Angles[k]) * t;
                        double cx = c * x * w, cx2 = cx * x, cx3 = cx2 * x;
                        i1 += cx; i2 += cx2; i3 += cx3;
                        j1 += cx * th; j2 += cx2 * th; j3 += cx3 * th;
                    }
                }
            }
            _i1 = i1; _i2 = i2; _i3 = i3; _j1 = j1; _j2 = j2; _j3 = j3;
            _ready = true;
        }

        /// <summary>
        /// One blade's flapwise spring moments at the hub, 1/rev, N·m, K_β·β₁c and K_β·β₁s, and its flap β₀, β₁c, β₁s, rad,
        /// for a rotor at speed (rad/s) with its hub's in-plane airspeed (m/s, 0 or more), its axial inflow (m/s, positive down
        /// through the disc) and its thrust (N).
        /// </summary>
        public void Solve(double speed, double inPlane, double axial, double thrust,
            out double m1c, out double m1s, out double b0, out double b1c, out double b1s)
        {
            Prepare();
            m1c = m1s = b0 = b1c = b1s = 0;
            if (!(speed > 0)) return;
            double r = Radius, vt = speed * r;
            double vi = InducedVelocity(inPlane, axial, thrust);
            double mu = inPlane / vt, li = vi / vt, lambda = (axial + vi) / vt;
            // The wake's skew: tan(χ/2) = μ/(√(μ² + λ²) + λ), held at 1 (χ 90°).
            double root = DetMath.Sqrt(mu * mu + lambda * lambda) + lambda;
            double half = root > mu ? mu / root : 1;
            double lx = SkewGain * half * li;
            double r2 = r * r;
            double a = 0.5 * AirDensity * LiftSlope * speed * speed * r2 * r2;
            double ib = BladeInertia, w0 = 2 * Pi * BendingFrequency;
            double k = ib * (w0 * w0 + (Southwell - 1) * speed * speed);
            b0 = a * (_j3 + 0.5 * mu * mu * _j1 - lambda * _i2) / (ib * speed * speed + k);
            double fc = -a * (lx * _i3 + mu * b0 * _i2), fs = a * mu * (2 * _j2 - lambda * _i1);
            double dc = a * (_i3 + 0.25 * mu * mu * _i1), ds = a * (_i3 - 0.25 * mu * mu * _i1);
            if (k > 0)
            {
                // The flap and its spring's moment, divided through by k so a rigid blade (k → ∞) keeps its precision.
                double e = dc * ds / k;
                m1c = (k * fc - dc * fs) / (k + e);
                m1s = (k * fs + ds * fc) / (k + e);
                b1c = m1c / k;
                b1s = m1s / k;
            }
            else
            {
                b1c = -fs / ds;
                b1s = fc / dc;
            }
        }

        /// <summary>Glauert's induced velocity at a thrust (N) with the hub's in-plane airspeed and axial inflow, m/s.</summary>
        public double InducedVelocity(double inPlane, double axial, double thrust)
        {
            if (!(thrust > 0)) return 0;
            double vh2 = thrust / (2 * AirDensity * Pi * Radius * Radius), vh = DetMath.Sqrt(vh2), v = vh;
            for (int n = 0; n < 6; n++)
            {
                double u = axial + v;
                double d = DetMath.Max(DetMath.Sqrt(inPlane * inPlane + u * u), 0.5 * vh);
                v = 0.5 * (v + vh2 / d);
            }
            return v;
        }

        /// <summary>A section's helix angle over its zero-lift line at x = r/R for a pitch (m per revolution), rad: atan of
        /// p/(2πRx) by three half-angle passes and its series, less ZeroLiftAngle.</summary>
        public double HelixAngle(double pitch, double x)
        {
            double t = pitch / (2 * Pi * Radius * x);
            for (int n = 0; n < 3; n++) t = t / (1 + DetMath.Sqrt(1 + t * t));
            double t2 = t * t, s = 0, p = t;
            for (int n = 0; n < 12; n++)
            {
                s += (n % 2 == 0 ? p : -p) / (2 * n + 1);
                p *= t2;
            }
            return 8 * s - ZeroLiftAngle;
        }
    }
}
