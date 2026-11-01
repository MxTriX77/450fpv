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
    /// The rotors' in-plane drag, the H-force, is the rotors' (Propulsion.HForce). Basic operations only.
    /// </summary>
    public sealed class Aero
    {
        const double QuarterPi = 0.7853981633974483;

        readonly double _q;
        readonly Double3 _area;

        public Aero(AeroSpec spec)
        {
            if (spec == null) throw new System.ArgumentNullException(nameof(spec));
            Spec = spec.Clone();
            _q = 0.5 * spec.AirDensity * spec.DragCoefficient;
            _area = spec.BodyArea;
        }

        public AeroSpec Spec { get; }

        /// <summary>The body's drag, body N, for its centre of pressure moving at v through the air (body m/s).</summary>
        public Double3 Body(in Double3 v)
        {
            double area = _area.X * DetMath.Abs(v.X) + _area.Y * DetMath.Abs(v.Y) + _area.Z * DetMath.Abs(v.Z);
            return v * (-_q * area);
        }

        /// <summary>The coil's drag, body N, for its centre moving at v through the air (body m/s).</summary>
        public Double3 Coil(in Double3 v, in DronePart coil)
        {
            double along = Double3.Dot(v, coil.Axis), across2 = Double3.Dot(v, v) - along * along;
            double area = coil.Length * coil.Diameter * DetMath.Sqrt(DetMath.Max(across2, 0))
                + QuarterPi * coil.Diameter * coil.Diameter * DetMath.Abs(along);
            return v * (-_q * area);
        }
    }
}
