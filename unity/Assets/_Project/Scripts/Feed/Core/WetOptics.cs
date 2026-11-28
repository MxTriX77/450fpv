using System;

namespace Fpv450.Feed
{
    /// <summary>
    /// The wet look's numbers (w21-rain-on-camera 1.3, VIS-03, D-126), each labelled: what <c>Shaders/WetLaw.hlsl</c>
    /// draws, computed here once so the shaders, the captures and the tests read the same values.
    /// <list type="bullet">
    /// <item><b>The darkening</b>, Lekner and Dorf 1988 (Appl. Opt. 27, 1278, published): a wetted rough surface under
    /// water whose top is the air, its diffusely reflected light internally reflected at that top with probability
    /// <see cref="InternalReflection"/> back to the grains, which absorb again (<see cref="Body"/>, eq. 1 and 9), the
    /// grains' own absorption raised under water by <see cref="GrainAbsorption"/> (eq. 10-11). It reproduces the
    /// paper's numbers: p 0.475 (0.437 by Angstrom's total internal reflection alone), a_w/a_d 1.07, 1.08 and 1.10 at
    /// grain indices 1.5, 2 and 2.5 (WetOpticsTests).</item>
    /// <item><b>The top</b>: water's normal-incidence reflectance <see cref="WaterF0"/> (Fresnel, published index).</item>
    /// <item><b>Standing water</b> (Severe rain): its smoothness from the mean square slope of calm water
    /// (<see cref="PondSmoothness"/>), on faces flatter than the slope that drains a pavement (<see cref="DrainSlope"/>).</item>
    /// <item><b>The rain's extinction</b> from the drop spectrum (<see cref="ExtinctionPerM"/>).</item>
    /// </list>
    /// </summary>
    public static class WetOptics
    {
        /// <summary>Water's refractive index n_l (published: 1.333 at 589 nm and 20 °C; Lekner and Dorf take 4/3).</summary>
        public const double WaterIndex = 4.0 / 3.0;

        /// <summary>The grains' index n_r (assumed 1.5: quartz 1.54, feldspars 1.52-1.58, calcite 1.49-1.66, cellulose
        /// about 1.53, the published ranges of what soil, render, concrete, wood and leaves are made of).</summary>
        public const double GrainIndex = 1.5;

        /// <summary>Calm water's mean square slope (published: Cox and Munk 1954, the zero-wind intercept 0.003 of the
        /// clean surface's fit 0.003 + 5.12e-3 W); assumed for standing water, whose fetch is too short for wind waves.
        /// The rain's own ripples are not modelled.</summary>
        public const double CalmMeanSquareSlope = 0.003;

        /// <summary>The cross-fall that drains a paved surface (published practice: carriageways are built at a 2-2.5 %
        /// cross-fall to shed water); a face flatter than 2 % holds standing water in Severe rain (assumed).</summary>
        public const double DrainSlope = 0.02;

        /// <summary>The share of Q_ext = 2 (a drop much larger than the wavelength, van de Hulst 1957, published) that
        /// leaves a pixel: the diffracted half stays within λ/D, 0.03° for a 1 mm drop at 550 nm, far under the feed's
        /// pixel of about 0.2°, so the picture loses Q = 1 (derived).</summary>
        public const double PictureExtinctionEfficiency = 1.0;

        /// <summary>Fresnel's unpolarised reflectance at an interface of relative index n (n₂/n₁) for incidence
        /// sin²θ = x; 1 past the critical angle.</summary>
        public static double Reflectance(double x, double n)
        {
            if (x >= n * n)
                return 1.0;
            double ci = Math.Sqrt(1.0 - x), ct = Math.Sqrt(1.0 - x / (n * n));
            double rs = (ci - n * ct) / (ci + n * ct), rp = (n * ci - ct) / (n * ci + ct);
            return 0.5 * (rs * rs + rp * rp);
        }

        /// <summary>The reflectance for isotropic (Lambertian) light, R̄(n) = ∫₀¹ R(x, n) dx (LD eq. 7), by the midpoint
        /// rule over 2^16 steps up to the critical angle's x = n², all of it reflected past it (numerical: R̄(1/n_l)
        /// and LD eq. 9's p, which reciprocity makes equal, agree to 1e-9 in WetOpticsTests).</summary>
        public static double DiffuseReflectance(double n)
        {
            const int steps = 1 << 16;
            double cut = Math.Min(1.0, n * n), sum = 0.0;
            for (int i = 0; i < steps; i++)
                sum += Reflectance(cut * (i + 0.5) / steps, n) * cut / steps;
            return sum + (1.0 - cut); // past the critical angle all of it
        }

        /// <summary>R̄ for daylight from the sky entering the water (n_l), 0.0665 (derived).</summary>
        public static readonly double EntryReflectance = DiffuseReflectance(WaterIndex);

        /// <summary>T = 1 - R̄(n_l): the share of the sky's diffuse light that enters the water, 0.934 (derived).</summary>
        public static readonly double EntryTransmission = 1.0 - EntryReflectance;

        /// <summary>p = 1 - (1 - R̄(n_l)) / n_l²: the share of the grains' diffuse light the water's top sends back down,
        /// 0.475 (derived, LD eq. 9; it equals R̄(1/n_l), the internal reflectance computed directly).</summary>
        public static readonly double InternalReflection = 1.0 - EntryTransmission / (WaterIndex * WaterIndex);

        /// <summary>c = (1 - R̄(n_r/n_l)) / (1 - R̄(n_r)): a grain's absorption under water over in air when it absorbs
        /// little, 1.068 at n_r 1.5 (derived, LD eq. 10-11).</summary>
        public static readonly double GrainAbsorption = GrainAbsorptionFor(GrainIndex);

        /// <summary>c for a grain of index <paramref name="grainIndex"/> under water.</summary>
        public static double GrainAbsorptionFor(double grainIndex) =>
            (1.0 - DiffuseReflectance(grainIndex / WaterIndex)) / (1.0 - DiffuseReflectance(grainIndex));

        /// <summary>Water's normal-incidence reflectance ((n_l - 1) / (n_l + 1))², 1/49 (derived).</summary>
        public static readonly double WaterF0 = (WaterIndex - 1.0) * (WaterIndex - 1.0) / ((WaterIndex + 1.0) * (WaterIndex + 1.0));

        /// <summary>The standing water's GGX roughness α = √(mean square slope) (Beckmann's m, which GGX's α matches
        /// near the specular peak; derived), 0.055.</summary>
        public static readonly double PondRoughness = Math.Sqrt(CalmMeanSquareSlope);

        /// <summary>URP's smoothness for it, 1 - √α (URP: α = perceptual roughness²), 0.766 (derived).</summary>
        public static readonly double PondSmoothness = 1.0 - Math.Sqrt(PondRoughness);

        /// <summary>The cosine of the drain slope's angle, the least face normal's y that holds standing water.</summary>
        public static readonly double PondCosine = 1.0 / Math.Sqrt(1.0 + DrainSlope * DrainSlope);

        /// <summary>A grain's absorption under water from its absorption in air a_d (LD eq. 10-11, with a⁽¹⁾ = a_d):
        /// a_w = a_d (c (1 - a_d) + a_d).</summary>
        public static double WetAbsorption(double dryAbsorption) =>
            dryAbsorption * (GrainAbsorption * (1.0 - dryAbsorption) + dryAbsorption);

        /// <summary>
        /// The wetted body of a surface whose drawn diffuse albedo is <paramref name="albedo"/> (one channel): the light
        /// that leaves the water after every bounce, per unit of the sky's diffuse light,
        /// D = T (1 - a_w)(1 - p) / (1 - p (1 - a_w)) (LD eq. 1 without the top's own reflection, which is the
        /// specular term's). Sand's 0.182 gives 0.092 against the 0.091 Angstrom measured wet (LD's table III).
        /// </summary>
        public static double Body(double albedo)
        {
            double a = Math.Min(1.0, Math.Max(0.0, albedo));
            double rw = 1.0 - WetAbsorption(1.0 - a), p = InternalReflection;
            return EntryTransmission * rw * (1.0 - p) / (1.0 - p * rw);
        }

        /// <summary>LD's whole wet albedo under the sky's diffuse light, the top's reflection R̄(n_l) included:
        /// 1 - (1 - R̄) a_w / (1 - p (1 - a_w)) = R̄ + <see cref="Body"/>.</summary>
        public static double WetAlbedo(double albedo) => EntryReflectance + Body(albedo);

        /// <summary>
        /// The rain's extinction of the picture, 1/m, at <paramref name="rateMmPerHour"/>: Marshall and Palmer's
        /// N(D) = N0 e^(-ΛD) with Λ = 3.67 / D0 (D0 = dropScale · R^dropExponent mm, RainPreset's), its intercept N0 set
        /// so the spectrum carries the rate at Atlas et al.'s fall speed V(D) = a - b e^(-cD) (RainPreset's):
        /// R = N0 π (a/Λ⁴ - b/(Λ+c)⁴) 1e-9 (derived, ∫ N (π/6) D³ V dD), 6 800-7 400 m⁻³ mm⁻¹ against the published
        /// 8 000 (as the lens's bins, design 3.1); then β = Q π N0 / (2 Λ³) 1e-6 (∫ N Q πD²/4 dD, derived).
        /// 0 at a rate of 0.
        /// </summary>
        public static double ExtinctionPerM(double rateMmPerHour, double dropScale, double dropExponent, double fallA, double fallB, double fallC)
        {
            if (!(rateMmPerHour > 0.0))
                return 0.0;
            double lambda = 3.67 / (dropScale * Math.Pow(rateMmPerHour, dropExponent)); // per mm
            double rate = rateMmPerHour / 3.6e6; // m/s
            double n0 = rate / (Math.PI * (fallA / Math.Pow(lambda, 4) - fallB / Math.Pow(lambda + fallC, 4)) * 1e-9); // per m³ per mm
            return PictureExtinctionEfficiency * Math.PI * n0 / (2.0 * lambda * lambda * lambda) * 1e-6;
        }

        /// <summary>That spectrum's intercept N0, per m³ per mm (for the log and the tests).</summary>
        public static double Intercept(double rateMmPerHour, double dropScale, double dropExponent, double fallA, double fallB, double fallC)
        {
            double lambda = 3.67 / (dropScale * Math.Pow(rateMmPerHour, dropExponent));
            return rateMmPerHour / 3.6e6 / (Math.PI * (fallA / Math.Pow(lambda, 4) - fallB / Math.Pow(lambda + fallC, 4)) * 1e-9);
        }
    }
}
