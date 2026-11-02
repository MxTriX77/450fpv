// WakeGridMath: the transcendental functions the wake grid needs (p7c-wind 1.3), from the IEEE basic operations only
// (DetMath's rule), so every runtime gives the same grid bits. DetMath has no exp or log yet; these stay internal to
// the wake grid until the sim core wants them (then they move into DetMath, one home for the rules).
// - Exp: Cody–Waite reduction x = k·ln 2 + r, |r| ≤ ln 2 / 2 (fdlibm's ln2_hi and ln2_lo, published), the Taylor series
//   to r^13 (the next term is below 3e-16 of the result), then 2^k put on exactly through the exponent bits.
// - Ln: x = m·2^e exactly from the bits with m in [√½, √2), then ln m = 2·atanh(t), t = (m − 1)/(m + 1), |t| ≤ 0.172,
//   by its series to t^23 (the next term is below 1e-19).
// - Pow(a, b) = Exp(b·Ln a), a > 0.
// - Erf: Abramowitz and Stegun 7.1.26 (published, |error| ≤ 1.5e-7), enough for a lateral spread weight.
// Accuracy is a few ulps; determinism, not the last bit of accuracy, is the point.

using System;

namespace Fpv450.Sim.World
{
    public static class WakeGridMath
    {
        const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10;
        const double InvLn2 = 1.44269504088896338700e+00;
        const double Sqrt2 = 1.41421356237309504880;

        public static double Exp(double x)
        {
            if (x < -708) return 0;
            if (x > 709) return double.PositiveInfinity;
            double k = Math.Round(x * InvLn2); // an integer: one correctly rounded op
            double r = x - k * Ln2Hi - k * Ln2Lo;
            double p = 1 + r * (1 + r * (1.0 / 2 + r * (1.0 / 6 + r * (1.0 / 24 + r * (1.0 / 120 + r * (1.0 / 720
                + r * (1.0 / 5040 + r * (1.0 / 40320 + r * (1.0 / 362880 + r * (1.0 / 3628800 + r * (1.0 / 39916800
                + r * (1.0 / 479001600 + r * (1.0 / 6227020800)))))))))))));
            long e = (long)k;
            if (e < -1020)
            {
                // Keep 2^e normal: two exact scalings.
                p *= BitConverter.Int64BitsToDouble((long)(e + 60 + 1023) << 52);
                return p * BitConverter.Int64BitsToDouble((long)(-60 + 1023) << 52);
            }
            return p * BitConverter.Int64BitsToDouble((e + 1023) << 52);
        }

        public static double Ln(double x)
        {
            if (!(x > 0)) return x == 0 ? double.NegativeInfinity : double.NaN;
            if (x > double.MaxValue) return x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)((bits >> 52) & 0x7FF);
            if (e == 0)
            {
                // Subnormal: scale by 2^54 exactly first.
                x *= 18014398509481984.0;
                bits = BitConverter.DoubleToInt64Bits(x);
                e = (int)((bits >> 52) & 0x7FF) - 54;
            }
            e -= 1023;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L); // [1, 2)
            if (m > Sqrt2) { m *= 0.5; e += 1; }
            double t = (m - 1) / (m + 1), t2 = t * t;
            double s = t * (2 + t2 * (2.0 / 3 + t2 * (2.0 / 5 + t2 * (2.0 / 7 + t2 * (2.0 / 9 + t2 * (2.0 / 11 + t2 * (2.0 / 13
                + t2 * (2.0 / 15 + t2 * (2.0 / 17 + t2 * (2.0 / 19 + t2 * (2.0 / 21 + t2 * (2.0 / 23))))))))))));
            return e * Ln2Hi + (s + e * Ln2Lo);
        }

        public static double Pow(double a, double b) => Exp(b * Ln(a));

        public static double Erf(double x)
        {
            double ax = DetMath.Abs(x);
            double t = 1 / (1 + 0.3275911 * ax);
            double y = 1 - t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))))
                * Exp(-ax * ax);
            return x < 0 ? -y : y;
        }

        /// <summary>The standard normal distribution function Φ(x) = (1 + erf(x/√2))/2.</summary>
        public static double Phi(double x) => 0.5 * (1 + Erf(x * 0.70710678118654752440));

        /// <summary>The smooth step t²·(3 − 2t) of t clamped to [0, 1].</summary>
        public static double Smooth(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
    }
}
