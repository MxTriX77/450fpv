namespace Fpv450.Sim.Contact
{
    /// <summary>The shape a part presses into soil with (p7a-ground design 2).</summary>
    public enum SoilPart : byte
    {
        /// <summary>A tube lying along the soil (a half-skid, a bar): its section per metre of length.</summary>
        Tube = 0,

        /// <summary>A sphere (a support on a box, the coil's end): a cap.</summary>
        Sphere = 1,

        /// <summary>A flat disc of the given radius (the reference foot): its area times its sink.</summary>
        Disc = 2,
    }

    /// <summary>
    /// Winkler's elasto-plastic soil (p7a-ground design 2, PHY-01): the soil's force is its modulus times the volume the part
    /// displaces below the soil's surface, under Wong's load and unload rule (Wong 2008, repetitive loading) applied to every
    /// soil column under the part. Bekker's linear pressure–sinkage (n = 1, Bekker 1969) gives the modulus at the part's
    /// width b, k = k_c/b + k_φ. Only + − × ÷ √; engine-free, allocation-free.
    ///
    /// Each column under a rigid part sinks with it, z_col = z − h(y), h the part's surface over its lowest point. A column
    /// loads on the virgin line p = k·z_col and unloads and reloads on Wong's stiffer line, slope k_u = ratio × k, from its
    /// own deepest sink z_col,m = z_m − h(y). Summed over the columns the unloading force is the virgin law at an effective
    /// sink, F_u(z) = F_l(z*), z* = z_m − ratio·(z_m − z) (derived): for a flat disc it is k_u·S·(z − z_p) exactly, with the
    /// plastic set z_p = z_m·(1 − 1/ratio); under a curved part its stiffness leaving z_m is k_u times the footprint there and
    /// the contact narrows as it rises; an unload ratio of 1 is elastic and keeps no set. Past z_m the reloading is virgin
    /// again. Past the set's maximum sink a firm layer adds its modulus times the footprint times the further sink.
    /// </summary>
    public static class Soil
    {
        /// <summary>Bekker's modulus k_c/b + k_φ at a contact of width b (m), N/m³.</summary>
        public static double Modulus(in SoilSet set, double b) => set.CohesiveModulus / b + set.FrictionalModulus;

        /// <summary>Bekker's b of a part (derived): a tube's diameter (its conforming groove's width, the rectangle's smaller
        /// side), a sphere's radius and a disc's radius (a circular plate's b is its radius, Bekker 1969, Wong 2008).</summary>
        public static double Width(SoilPart part, double radius) => part == SoilPart.Tube ? 2 * radius : radius;

        /// <summary>
        /// What a part of radius r displaces at sink z below the soil's surface, with its rate d/dz, and its footprint there.
        /// A tube's section per metre (m², its rate m, its footprint the chord, m): up to z = r the circular segment on the
        /// exact chord w = 2√(2rz − z²) by the classic segment formula A = (2/3)·w·z + z³/(2w) (derived: within 0.8 % of the
        /// circular segment up to z = r, 0.1 % at z = r/2, where design 2's parabola (2/3)·w·z alone is 15.1 % and 6 %
        /// low), then A(r) + 2r·(z − r). A sphere's cap (m³, its rate and footprint the cap's base, m²): V = πz²(r − z/3), the published
        /// geometry, then past its equator a cylinder of its radius (the soil does not close over it). A disc's area πr² times z.
        /// </summary>
        public static double Displaced(SoilPart part, double r, double z, out double rate, out double footprint)
        {
            if (!(z > 0))
            {
                rate = 0;
                footprint = 0;
                return 0;
            }
            const double Pi = 3.141592653589793;
            if (part == SoilPart.Tube)
            {
                if (z >= r)
                {
                    rate = 2 * r;
                    footprint = 2 * r;
                    return 19.0 / 12.0 * r * r + 2 * r * (z - r); // the formula's own A(r), 4/3 r² + r²/4
                }
                double w = 2 * DetMath.Sqrt(2 * r * z - z * z);
                footprint = w;
                if (!(w > 0))
                {
                    rate = 0;
                    return 0;
                }
                double dw = 4 * (r - z) / w;
                rate = 2.0 / 3.0 * (w + z * dw) + (3 * z * z * w - z * z * z * dw) / (2 * w * w);
                return 2.0 / 3.0 * w * z + z * z * z / (2 * w);
            }
            if (part == SoilPart.Sphere)
            {
                if (z >= r)
                {
                    rate = Pi * r * r;
                    footprint = rate;
                    return 2.0 / 3.0 * Pi * r * r * r + Pi * r * r * (z - r);
                }
                rate = Pi * (2 * r * z - z * z);
                footprint = rate;
                return Pi * z * z * (r - z / 3);
            }
            rate = Pi * r * r;
            footprint = rate;
            return rate * z;
        }

        /// <summary>
        /// The soil's force on the part at sink z (a tube's per metre, N/m; a sphere's or disc's, N) and its stiffness d/dz,
        /// with k the modulus at the part's width (Modulus), the set's unload ratio and maximum sink, the firm layer's modulus
        /// and the deepest sink zMax this soil has had under the part (0 for virgin soil). Never negative.
        /// </summary>
        public static double Force(SoilPart part, double r, in SoilSet set, double k, double firm, double z, double zMax, out double stiffness)
        {
            double f, fp;
            if (z >= zMax)
            {
                f = k * Displaced(part, r, z, out double rate, out fp);
                stiffness = k * rate;
            }
            else
            {
                double ratio = set.UnloadRatio;
                double a = Displaced(part, r, zMax - ratio * (zMax - z), out double rate, out _);
                Displaced(part, r, z, out _, out fp);
                f = k * a;
                stiffness = ratio * k * rate;
            }
            if (z > set.MaxSink)
            {
                f += firm * fp * (z - set.MaxSink);
                stiffness += firm * fp;
            }
            return f;
        }

        /// <summary>The plastic set z_p a deepest sink zMax leaves when the part is lifted off: where Force falls to zero,
        /// zMax·(1 − 1/ratio) (derived; 0 for an elastic soil, ratio 1).</summary>
        public static double Set(in SoilSet set, double zMax) => zMax > 0 ? zMax - zMax / set.UnloadRatio : 0;
    }
}
