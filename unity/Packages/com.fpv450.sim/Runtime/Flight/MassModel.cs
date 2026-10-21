using System;

namespace Fpv450.Sim.Flight
{
    public enum PartKind { Box, Cylinder }

    /// <summary>
    /// One rigid part of the drone (DRN-03): its mass, shape, centre and material, in body axes (DetMath.cs, "Frames
    /// and units": x right, y up, z forward, origin at the arm-plane centre), SI units.
    /// </summary>
    public struct DronePart
    {
        public string Name;
        public PartKind Kind;
        public string Material;

        /// <summary>kg. A loadout may set it to 0 (a part left off), never the file.</summary>
        public double Mass;

        /// <summary>The part's centre, m.</summary>
        public Double3 Position;

        /// <summary>A box's extents along body x, y and z, m.</summary>
        public Double3 Size;

        /// <summary>A cylinder's diameter and length, m: a solid cylinder about Axis.</summary>
        public double Diameter, Length;

        /// <summary>A cylinder's axis, a unit vector (normalised when read).</summary>
        public Double3 Axis;

        /// <summary>The inertia of one kilogram of this shape about its own centre, body axes, kg·m² per kg.</summary>
        public readonly Sym3 InertiaPerKg()
        {
            if (Kind == PartKind.Box)
            {
                double x = Size.X * Size.X, y = Size.Y * Size.Y, z = Size.Z * Size.Z;
                return new Sym3((y + z) / 12, (x + z) / 12, (x + y) / 12, 0, 0, 0);
            }
            // A solid cylinder of radius r and length L: (3r² + L²)/12 across the axis a and r²/2 about it, so
            // I = across·1 + (about − across)·a·aᵀ.
            double r2 = Diameter * Diameter / 4;
            double across = (3 * r2 + Length * Length) / 12, about = r2 / 2, k = about - across;
            Double3 a = Axis;
            return new Sym3(across + k * a.X * a.X, across + k * a.Y * a.Y, across + k * a.Z * a.Z,
                k * a.X * a.Y, k * a.X * a.Z, k * a.Y * a.Z);
        }
    }

    /// <summary>The rigid body's mass properties, in body axes about the centre of mass.</summary>
    public struct MassProperties
    {
        /// <summary>kg.</summary>
        public double Mass;
        public double InverseMass;

        /// <summary>Body, m, from the arm-plane centre (the body origin, D-019).</summary>
        public Double3 CenterOfMass;

        /// <summary>About the centre of mass, body axes, kg·m²: roll is ZZ, pitch XX and yaw YY.</summary>
        public Sym3 Inertia;
        public Sym3 InverseInertia;
    }

    /// <summary>
    /// The mass build (PHB-02, DRN-03, design 3): the drone's parts summed by the parallel-axis theorem into the mass,
    /// the centre of mass and the full inertia tensor (K§6.3: never three tuned scalars). A part's mass may change
    /// (the payload setting, the coil as it pays out, a leg left off at 0 kg) and the build runs again at once. Its
    /// shapes and positions are fixed. The rebuild allocates nothing, so the flight thread may run it inside a step.
    /// </summary>
    public sealed class MassModel
    {
        readonly DronePart[] _parts;
        readonly Sym3[] _inertiaPerKg;
        MassProperties _properties;

        /// <summary>A model of these parts (copied).</summary>
        public MassModel(DronePart[] parts)
        {
            if (parts == null || parts.Length == 0) throw new ArgumentException("a mass model needs parts", nameof(parts));
            _parts = (DronePart[])parts.Clone();
            _inertiaPerKg = new Sym3[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
                _inertiaPerKg[i] = _parts[i].InertiaPerKg();
            Build();
        }

        MassModel(MassModel other)
        {
            _parts = (DronePart[])other._parts.Clone();
            _inertiaPerKg = (Sym3[])other._inertiaPerKg.Clone();
            _properties = other._properties;
        }

        /// <summary>A copy that changes independently, for a flight of its own.</summary>
        public MassModel Clone() => new MassModel(this);

        public ref readonly MassProperties Properties => ref _properties;

        public int PartCount => _parts.Length;

        public ref readonly DronePart Part(int i) => ref _parts[i];

        /// <summary>Sets one part's mass, kg (0 leaves it off), and rebuilds.</summary>
        public void SetPartMass(int part, double mass)
        {
            if (!(mass >= 0)) throw new ArgumentOutOfRangeException(nameof(mass), "a part's mass is 0 or more");
            _parts[part].Mass = mass;
            Build();
        }

        // M = Σ m_i; c = Σ m_i·p_i / M; I = Σ m_i·(I_i/kg + |d_i|²·1 − d_i·d_iᵀ), d_i = p_i − c. In part order, so the
        // same masses give the same bits.
        void Build()
        {
            double mass = 0;
            Double3 moment = default;
            for (int i = 0; i < _parts.Length; i++)
            {
                double m = _parts[i].Mass;
                mass += m;
                moment = moment + _parts[i].Position * m;
            }
            if (!(mass > 0)) throw new InvalidOperationException("the parts weigh nothing");
            double inverseMass = 1 / mass;
            Double3 c = moment * inverseMass;

            Sym3 inertia = default;
            for (int i = 0; i < _parts.Length; i++)
                inertia = inertia + (_inertiaPerKg[i] + Sym3.PointMass(_parts[i].Position - c)) * _parts[i].Mass;

            _properties.Mass = mass;
            _properties.InverseMass = inverseMass;
            _properties.CenterOfMass = c;
            _properties.Inertia = inertia;
            _properties.InverseInertia = inertia.Inverse();
        }
    }
}
