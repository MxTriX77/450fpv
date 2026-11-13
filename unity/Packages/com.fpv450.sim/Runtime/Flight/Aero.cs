namespace Fpv450.Sim.Flight
{
    /// <summary>The airframe's drag (drone.json's aero block, DroneSpec.BuildAero); the log header records it.</summary>
    public sealed class AeroSpec
    {
        /// <summary>The air's density, kg/m³.</summary>
        public double AirDensity;

        /// <summary>The body's silhouette less the coil's, projected along body x (the side), y (the top) and z (the
        /// front), m².</summary>
        public Double3 BodyArea;

        /// <summary>Where the body's drag acts, body m.</summary>
        public Double3 BodyCenterOfPressure;

        /// <summary>The drag coefficient of the body's silhouette and of the coil.</summary>
        public double DragCoefficient;

        /// <summary>The coil's mass part, whose cylinder and centre in the flight's mass model carry the coil's own drag
        /// (so a build that moves the spool drags it where it hangs); −1 for none.</summary>
        public int CoilPart = -1;

        /// <summary>The hull's frontal silhouette, m² (drone.json's aero.hull, tools/hullmass): the union of the closed parts the air
        /// sees as one body; 0 for none (no hull moment).</summary>
        public double HullFrontalArea;

        /// <summary>The hull's size, m: x its silhouette's width, y its height, z its mean length (its volume over its frontal
        /// area, a cylinder's length).</summary>
        public Double3 HullSize;

        /// <summary>The measured curves of the hull's moment (Aero.Hull): each curve's length over diameter, rising.</summary>
        public double[] HullRatios;

        /// <summary>Each curve's incidences, deg, rising from 0 to 90.</summary>
        public double[][] HullAngles;

        /// <summary>Each curve's moment coefficient at them about the body's mid-length, on q·A·d, + turning the nose from the
        /// flow (0 at 0° and at 90°).</summary>
        public double[][] HullMoments;

        /// <summary>The hull's potential-flow added mass per unit density in body axes, m³ (tools/hullmass), which the measured
        /// moment replaces; flown only as the known-wrong (HullPotential).</summary>
        public Sym3 HullAddedVolume;

        /// <summary>The known-wrong: the hull's potential-flow couple ρ(A·v) × v in place of its measured moment.</summary>
        public bool HullPotential;

        public AeroSpec Clone() => (AeroSpec)MemberwiseClone();
    }

    /// <summary>
    /// The airframe's drag (design 6, K§6.5): bluff-body drag on the air's velocity relative to each shape, with the
    /// silhouette the air sees from where it comes.
    /// - The body: F = −½ρ·C_d·A(n)·|v|·v, v the centre of pressure's velocity through the air in body axes, with
    ///   A(n) = A_x|n_x| + A_y|n_y| + A_z|n_z| the projection of a box of those side, top and front silhouettes along
    ///   the flow's direction n (exact for a box), so A(n)·|v| = A_x|v_x| + A_y|v_y| + A_z|v_z|.
    /// - The coil, at its own centre: its cylinder's silhouette along n, L·D·√(1 − (n·a)²) + (π/4)·D²·|n·a|
    ///   (exact for a cylinder of axis a), with the same coefficient; beneath the frame on both builds (D-050) it hangs
    ///   below the centre of mass, so its drag also rolls the drone, its top into the wind (K§5.2's sense).
    /// - The hull's moment (p7c-wind design 12): the closed parts (the plates, the bottle, the packs, the canister) form one
    ///   short blunt body. Its potential-flow (Munk) couple would turn it across the flow, but a flat-faced body of length over
    ///   diameter 0.75–1.5 separates at its front edges and is restoring up to 30–70° of incidence (Rhudy and Baker 1972,
    ///   AEDC-TR-72-180, measured). Each plane takes the measured C_m(α, L/d) on its own incidence and dynamic pressure:
    ///   about x (pitch) α = atan2(−v_y, v_z) and L/d = L/H, about y (yaw) β = atan2(v_x, v_z) and L/d = L/W, the moment
    ///   −C_m·½ρ(v_⊥² + v_z²)·A·d with d the hull's height or width; the curves interpolated linearly in the angle and in L/d,
    ///   held at their ends; beyond 90° (the hull backwards) C_m(180° − α) = −C_m(α), a symmetric body's. A pure moment: the
    ///   forces stay the drag's above.
    /// The rotors' in-plane drag, the H-force, is the rotors' (Propulsion.HForce). Basic operations only.
    /// </summary>
    public sealed class Aero
    {
        const double QuarterPi = 0.7853981633974483, RadToDeg = 57.29577951308232;

        readonly double _q;
        readonly Double3 _area;
        readonly double _hull, _pitchRatio, _yawRatio;

        public Aero(AeroSpec spec)
        {
            if (spec == null) throw new System.ArgumentNullException(nameof(spec));
            Spec = spec.Clone();
            _q = 0.5 * spec.AirDensity * spec.DragCoefficient;
            _area = spec.BodyArea;
            HasHull = spec.HullFrontalArea > 0 || spec.HullPotential;
            if (!(spec.HullFrontalArea > 0)) return;
            _hull = 0.5 * spec.AirDensity * spec.HullFrontalArea;
            _pitchRatio = spec.HullSize.Z / spec.HullSize.Y;
            _yawRatio = spec.HullSize.Z / spec.HullSize.X;
        }

        public AeroSpec Spec { get; }

        /// <summary>Whether the airframe carries a hull moment (Hull).</summary>
        public bool HasHull { get; }

        /// <summary>The hull's moment, body N·m, for the airframe moving at v through the air (body m/s): the measured moment
        /// in pitch and in yaw, or with HullPotential the potential couple ρ(A·v) × v.</summary>
        public Double3 Hull(in Double3 v)
        {
            if (Spec.HullPotential) return Double3.Cross(Spec.HullAddedVolume * v, v) * Spec.AirDensity;
            double zz = v.Z * v.Z, pitch = v.Y * v.Y + zz, yaw = v.X * v.X + zz, mx = 0, my = 0;
            if (pitch > 0) mx = -Coefficient(DetMath.Atan2(-v.Y, v.Z), _pitchRatio) * _hull * pitch * Spec.HullSize.Y;
            if (yaw > 0) my = -Coefficient(DetMath.Atan2(v.X, v.Z), _yawRatio) * _hull * yaw * Spec.HullSize.X;
            return new Double3(mx, my, 0);
        }

        /// <summary>The measured C_m at an incidence (rad, any sign and turn) and length over diameter.</summary>
        public double Coefficient(double incidence, double ratio)
        {
            double deg = incidence * RadToDeg, sign = 1;
            if (deg < 0) { deg = -deg; sign = -1; }
            if (deg > 90) { deg = 180 - deg; sign = -sign; }
            double[] ratios = Spec.HullRatios;
            int last = ratios.Length - 1;
            if (!(ratio > ratios[0])) return sign * Curve(0, deg);
            if (!(ratio < ratios[last])) return sign * Curve(last, deg);
            int i = 0;
            while (ratio > ratios[i + 1]) i++;
            double f = (ratio - ratios[i]) / (ratios[i + 1] - ratios[i]);
            return sign * (Curve(i, deg) + f * (Curve(i + 1, deg) - Curve(i, deg)));
        }

        // Curve c at deg in [0, 90], linear between its points.
        double Curve(int c, double deg)
        {
            double[] a = Spec.HullAngles[c], m = Spec.HullMoments[c];
            int i = 0;
            while (i < a.Length - 2 && deg > a[i + 1]) i++;
            return m[i] + (deg - a[i]) / (a[i + 1] - a[i]) * (m[i + 1] - m[i]);
        }

        /// <summary>The body's drag, body N, for its centre of pressure moving at v through the air (body m/s).</summary>
        public Double3 Body(in Double3 v) => v * (-_q * BodyFlux(v));

        /// <summary>The body's silhouette along v times |v|, m³/s: A(n)·|v| = A_x|v_x| + A_y|v_y| + A_z|v_z| (the
        /// volume it sweeps per second; the rain's mass flux per kg/m³ of water).</summary>
        public double BodyFlux(in Double3 v) => _area.X * DetMath.Abs(v.X) + _area.Y * DetMath.Abs(v.Y) + _area.Z * DetMath.Abs(v.Z);

        /// <summary>The coil's drag, body N, for its centre moving at v through the air (body m/s).</summary>
        public Double3 Coil(in Double3 v, in DronePart coil) => v * (-_q * CoilFlux(v, coil));

        /// <summary>The coil's silhouette along v times |v|, m³/s.</summary>
        public static double CoilFlux(in Double3 v, in DronePart coil)
        {
            double along = Double3.Dot(v, coil.Axis), across2 = Double3.Dot(v, v) - along * along;
            return coil.Length * coil.Diameter * DetMath.Sqrt(DetMath.Max(across2, 0))
                + QuarterPi * coil.Diameter * coil.Diameter * DetMath.Abs(along);
        }

        /// <summary>The coil's wetted surface, m²: its cylinder's side and ends, π·D·(L + D/2).</summary>
        public static double CoilSurface(in DronePart coil) => 4 * QuarterPi * coil.Diameter * (coil.Length + 0.5 * coil.Diameter);
    }
}
