using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// One value per corner of the X frame (a rotor, its motor, the leg under its arm), indexed in Betaflight's Quad X
    /// motor order: [0] rear right (motor 1), [1] front right (2), [2] rear left (3), [3] front left (4). A value type,
    /// so a copied state or pose never shares storage with the one it came from.
    /// </summary>
    public struct Corners
    {
        public double RearRight, FrontRight, RearLeft, FrontLeft;

        public double this[int i]
        {
            readonly get
            {
                switch (i)
                {
                    case 0: return RearRight;
                    case 1: return FrontRight;
                    case 2: return RearLeft;
                    case 3: return FrontLeft;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
            set
            {
                switch (i)
                {
                    case 0: RearRight = value; break;
                    case 1: FrontRight = value; break;
                    case 2: RearLeft = value; break;
                    case 3: FrontLeft = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(i));
                }
            }
        }

        public static Corners Lerp(in Corners a, in Corners b, double t) => new Corners
        {
            RearRight = a.RearRight + (b.RearRight - a.RearRight) * t,
            FrontRight = a.FrontRight + (b.FrontRight - a.FrontRight) * t,
            RearLeft = a.RearLeft + (b.RearLeft - a.RearLeft) * t,
            FrontLeft = a.FrontLeft + (b.FrontLeft - a.FrontLeft) * t,
        };
    }
}
