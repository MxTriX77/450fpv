using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>What a blade struck (BladeStrike.Kind).</summary>
    public enum StrikeKind : byte
    {
        None = 0,

        /// <summary>A world shape: a wall, a pole, a trunk, a car, a conductor as a rigid chain (p7f-damage 1.3).</summary>
        Shape = 1,

        /// <summary>A ground element: a straw, a stem, a twig or a leaf (p7a-ground 1.15's).</summary>
        Element = 2,

        /// <summary>A soft wire (p7d-tether 1.10's).</summary>
        Wire = 3,

        /// <summary>Standing stems (w9-village-physics 2.2, PropStems): a map's stem field or its drawn straws and stems. Material is
        /// their surfaces.json cover kind + 1 (Contact.CoverKind), never a world material id; Count the whole stems the blade's running
        /// count passed in the step (the field's counts are means, never drawn); Diameter the kind's or the element's.</summary>
        Stem = 4,
    }

    /// <summary>
    /// One blade's strikes in one step (p7a-ground design 9, p7f-damage design 4 and 7): the energy they took from the rotor,
    /// J; the impulse the struck thing gave the drone, N·s (its magnitude); the element's or wire's diameter, m (0 for a
    /// shape); an element's cut energy, J (0 for a shape); the strike's radius on the blade, m (the smallest of the step's);
    /// the struck material (the world's id; 0 for none) and how many strikes made it. The kind is the last strike's.
    /// </summary>
    public struct BladeStrike
    {
        public StrikeKind Kind;
        public byte Count;
        public ushort Material;
        public double Energy, Impulse, Diameter, CutEnergy, Radius;

        /// <summary>Adds one strike: its energy and impulse to the step's, its radius if it is the smallest.</summary>
        public void Add(StrikeKind kind, ushort material, double energy, double impulse, double diameter, double cutEnergy, double radius)
        {
            if (Count == 0 || radius < Radius) Radius = radius;
            Kind = kind;
            Material = material;
            Energy += energy;
            Impulse += impulse;
            Diameter = diameter;
            CutEnergy += cutEnergy;
            if (Count < byte.MaxValue) Count++;
        }

        /// <summary>Adds a step's stems (StrikeKind.Stem): their energy, impulse and edges' work to the step's, the radius if it is the
        /// smallest, and the whole stems counted (held to 255).</summary>
        public void AddStems(ushort kind, double energy, double impulse, double diameter, double cutEnergy, double radius, int whole)
        {
            if (Count == 0 && Energy == 0 || radius < Radius) Radius = radius;
            Kind = StrikeKind.Stem;
            Material = kind;
            Energy += energy;
            Impulse += impulse;
            Diameter = diameter;
            CutEnergy += cutEnergy;
            int count = Count + whole;
            Count = (byte)(count < byte.MaxValue ? count : byte.MaxValue);
        }
    }

    /// <summary>
    /// The step's blade strikes per rotor and blade (FlightState.BladeStrikes): slot corner × 2 + blade, in Corners' order, for
    /// two blades a prop (drone.json's props). Written in the step by whatever strikes a blade (p7f-damage's shapes, p7a-ground's
    /// elements, p7d-tether's wires) and read in the same step by the damage; cleared at the next step's start. A value type.
    /// </summary>
    public struct BladeStrikes
    {
        /// <summary>The slots: four rotors, two blades each.</summary>
        public const int Slots = 8;

        public BladeStrike S0, S1, S2, S3, S4, S5, S6, S7;

        public BladeStrike this[int i]
        {
            readonly get
            {
                switch (i)
                {
                    case 0: return S0;
                    case 1: return S1;
                    case 2: return S2;
                    case 3: return S3;
                    case 4: return S4;
                    case 5: return S5;
                    case 6: return S6;
                    case 7: return S7;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
            set
            {
                switch (i)
                {
                    case 0: S0 = value; break;
                    case 1: S1 = value; break;
                    case 2: S2 = value; break;
                    case 3: S3 = value; break;
                    case 4: S4 = value; break;
                    case 5: S5 = value; break;
                    case 6: S6 = value; break;
                    case 7: S7 = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
        }

        /// <summary>The slot of a rotor's blade.</summary>
        public static int Slot(int corner, int blade) => corner * 2 + blade;
    }
}
