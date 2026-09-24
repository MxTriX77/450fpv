using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

/// Deterministic math for the world query (W-13): integer hashes and functions built only from correctly rounded
/// IEEE operations (+ − × ÷ √, floor), evaluated in a fixed order, so every x64 machine gets the same bits.
/// Nothing here calls Math.Sin/Cos/Exp/Pow or fused multiply-add.
public static class DetMath
{
    /// `x <= y ? a : b` as a bitwise select, never a branch: for choices that are a coin toss from call to call, where a
    /// mispredicted branch costs more than evaluating both sides. The bits are those of a or b.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SelectLe(double x, double y, double a, double b)
    {
        Vector128<double> mask = Vector128.LessThanOrEqual(Vector128.CreateScalarUnsafe(x), Vector128.CreateScalarUnsafe(y));
        return Vector128.ConditionalSelect(mask, Vector128.CreateScalarUnsafe(a), Vector128.CreateScalarUnsafe(b)).ToScalar();
    }

    /// (int)v for a finite v whose integer part fits an int: the processor's truncating conversion, without the checks
    /// .NET adds to every (int) cast for NaN and out-of-range values since .NET 9. The same result for every such v.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt(double v) => Sse2.IsSupported ? Sse2.ConvertToInt32WithTruncation(Vector128.CreateScalarUnsafe(v)) : (int)v;

    /// PCG output permutation (Jarzynski and Olano, "Hash Functions for GPU Rendering", 2020).
    public static uint Hash(uint x)
    {
        uint state = x * 747796405u + 2891336453u;
        uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
        return (word >> 22) ^ word;
    }

    /// Hash of a 2-D integer cell under a key (the map seed mixed with a salt, see Key).
    public static uint Hash(int x, int z, uint key) => Hash((uint)x + Hash((uint)z + key));

    public static uint Key(uint seed, uint salt) => Hash(seed + Hash(salt));

    /// SplitMix64's output function (Steele, Lea and Flood, "Fast Splittable Pseudorandom Number Generators", 2014): a
    /// bijection of 64 bits with full avalanche, for when many bits are drawn at once.
    public static ulong Mix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// The i-th independent value drawn from one hash.
    public static uint Draw(uint h, uint i) => Hash(h + i * 0x9E3779B9u);

    /// [0, 1) from 32 bits.
    public static double Unit(uint h) => h * (1.0 / 4294967296.0);

    /// Cubic smoothstep on [0, 1]: C¹, flat at both ends.
    public static double Smooth3(double t) => t * t * (3.0 - 2.0 * t);

    public static double Smooth3Slope(double t) => 6.0 * t * (1.0 - t);

    /// Quintic fade on [0, 1]: C², used between value-noise nodes.
    public static double Fade5(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

    public static double Fade5Slope(double t)
    {
        double u = t * (1.0 - t);
        return 30.0 * u * u;
    }

    /// Sine and cosine of an angle given in turns (1 = 360°). Reduced to ±1/8 turn, then Taylor series to 1e-17.
    public static void SinCosTurns(double turns, out double sin, out double cos)
    {
        double x = turns - System.Math.Floor(turns);
        double k = System.Math.Floor(4.0 * x + 0.5); // nearest quarter turn, 0 to 4
        double a = (x - 0.25 * k) * (2.0 * System.Math.PI); // |a| ≤ π/4
        double a2 = a * a;
        double s = a * (1.0 + a2 * (-1.0 / 6 + a2 * (1.0 / 120 + a2 * (-1.0 / 5040 + a2 * (1.0 / 362880
            + a2 * (-1.0 / 39916800 + a2 * (1.0 / 6227020800 + a2 * (-1.0 / 1307674368000 + a2 * (1.0 / 355687428096000)))))))));
        double c = 1.0 + a2 * (-1.0 / 2 + a2 * (1.0 / 24 + a2 * (-1.0 / 720 + a2 * (1.0 / 40320 + a2 * (-1.0 / 3628800
            + a2 * (1.0 / 479001600 + a2 * (-1.0 / 87178291200 + a2 * (1.0 / 20922789888000))))))));
        switch ((int)k & 3)
        {
            case 0: sin = s; cos = c; break;
            case 1: sin = c; cos = -s; break;
            case 2: sin = -s; cos = -c; break;
            default: sin = -c; cos = s; break;
        }
    }

    const double Ln2 = 0.69314718055994530942;

    /// b^e for b in [0, 1] and e > 0, as exp(e·ln b): both series to about 1e-16, so the bits never depend on the C
    /// runtime's pow.
    public static double Pow(double b, double e)
    {
        if (!(b > 0))
            return 0;
        // ln b: b = m·2^k with m in [√½, √2), then ln m = 2·atanh(s), s = (m − 1)/(m + 1), |s| ≤ 0.172.
        long bits = System.BitConverter.DoubleToInt64Bits(b);
        int k = (int)((bits >> 52) & 0x7FF) - 1023;
        double m = System.BitConverter.Int64BitsToDouble(bits & 0xFFFFFFFFFFFFFL | 0x3FF0000000000000L);
        if (m > 1.4142135623730951)
        {
            m *= 0.5;
            k++;
        }
        double s = (m - 1.0) / (m + 1.0), s2 = s * s, series = 1.0 / 25;
        for (int n = 23; n >= 1; n -= 2)
            series = series * s2 + 1.0 / n;
        double y = e * (2.0 * s * series + k * Ln2);
        // exp y: y = j·ln 2 + r with |r| ≤ ln 2 / 2, Taylor to r¹⁷/17!, then scaled by 2^j exactly.
        double j = System.Math.Floor(y / Ln2 + 0.5), r = y - j * Ln2, sum = 1.0;
        for (int n = 17; n >= 1; n--)
            sum = 1.0 + sum * r / n;
        return System.Math.ScaleB(sum, (int)j);
    }
}
