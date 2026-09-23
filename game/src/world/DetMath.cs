/// Deterministic math for the world query (W-13): integer hashes and functions built only from correctly rounded
/// IEEE operations (+ − × ÷ √, floor), evaluated in a fixed order, so every x64 machine gets the same bits.
/// Nothing here calls Math.Sin/Cos/Exp/Pow or fused multiply-add.
public static class DetMath
{
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
}
