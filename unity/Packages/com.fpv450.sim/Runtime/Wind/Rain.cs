using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// The rain of a flight (D-021, PHY-17): the pilot's own setting, chosen independently of the wind level, never drawn.
    /// Sunny is dry (no rain on the body; the rain-rate signal reads 0).
    /// </summary>
    public enum RainLevel : byte { Sunny = 0, Rain = 1, SevereRain = 2 }

    /// <summary>
    /// A rain level's numbers, each labelled; the log header records them by name (RainPreset.Named).
    /// - The rate is a per-flight draw, uniform inside the level's rainfall-intensity class (published: WMO-No. 8's
    ///   classes, recalled, not re-read here: light under 2.5 mm/h, moderate 2.5–10, heavy 10–50, violent over 50).
    ///   Rain is the moderate class and Severe rain the heavy one (assumed: a pilot's "severe rain" is heavy, not a
    ///   cloudburst); the uniform spread inside the class is assumed. The rate holds for the flight (assumed: a flight's
    ///   10–20 min under one shower).
    /// - The drops' fall speed is the median-volume drop's: Marshall–Palmer (1948, published) Λ = 4.1·R^−0.21 per mm, so
    ///   D0 = 3.67/Λ = 0.895·R^0.21 mm (derived for the exponential spectrum), and Atlas, Srivastava and Sekhon (1973,
    ///   published, a fit to Gunn and Kinzer 1949 at sea level) V = 9.65 − 10.3·e^(−0.6·D) m/s. 4.8 m/s at 5 mm/h, 6.1 at
    ///   25 (derived).
    /// - The drops move with the air across and fall at V through it (assumed: a 1–2 mm drop takes the air's velocity
    ///   within its relaxation time V/g, about 0.5 s, derived).
    /// - The film's thickness, the water a wetted surface keeps before it drains or is blown off, is a per-flight draw in
    ///   0.1–0.3 mm (assumed: the order of a wetted plate's or leaf's storage, 0.1–0.3 mm per side, recalled).
    /// </summary>
    public sealed class RainPreset
    {
        /// <summary>Water's density, kg/m³ (published: 999.7 at 10 °C, 1000 to 0.03 %).</summary>
        public const double WaterDensity = 1000;

        /// <summary>mm/h in m/s (exact).</summary>
        public const double MmPerHour = 1.0 / 3.6e6;

        /// <summary>The level's rainfall-intensity class, mm/h (published, WMO-No. 8).</summary>
        public double RateLo, RateHi;

        /// <summary>Marshall–Palmer's D0 = DropScale·R^DropExponent, mm (derived from the published Λ).</summary>
        public double DropScale = 0.895, DropExponent = 0.21;

        /// <summary>Atlas et al.'s V = FallA − FallB·e^(−FallC·D), m/s with D in mm (published).</summary>
        public double FallA = 9.65, FallB = 10.3, FallC = 0.6;

        /// <summary>The share of a drop's momentum relative to the airframe that it gives up on impact (assumed 1: the drop
        /// stops on the surface; a splash keeping part of it would give less, so this is the upper bound).</summary>
        public double MomentumShare = 1;

        /// <summary>The film's thickness bounds, m (assumed, see above).</summary>
        public double FilmLo = 1e-4, FilmHi = 3e-4;

        /// <summary>The film's share of its capacity at the flight's start (assumed 0: dry when armed; tunable).</summary>
        public double StartFilmShare = 0;

        public static RainPreset Of(RainLevel level)
        {
            switch (level)
            {
                case RainLevel.Rain: return new RainPreset { RateLo = 2.5, RateHi = 10 };
                case RainLevel.SevereRain: return new RainPreset { RateLo = 10, RateHi = 50 };
                default: throw new ArgumentException("Sunny has no rain preset", nameof(level));
            }
        }

        /// <summary>The preset by name, as the log header records it; none for Sunny.</summary>
        public static NamedValue[] Named(RainLevel level)
        {
            if (level == RainLevel.Sunny) return new NamedValue[0];
            RainPreset p = Of(level);
            return new[]
            {
                new NamedValue("rate_lo_mm_h", p.RateLo), new NamedValue("rate_hi_mm_h", p.RateHi),
                new NamedValue("water_density_kg_m3", WaterDensity),
                new NamedValue("drop_scale_mm", p.DropScale), new NamedValue("drop_exponent", p.DropExponent),
                new NamedValue("fall_a_m_s", p.FallA), new NamedValue("fall_b_m_s", p.FallB), new NamedValue("fall_c_per_mm", p.FallC),
                new NamedValue("momentum_share", p.MomentumShare),
                new NamedValue("film_lo_m", p.FilmLo), new NamedValue("film_hi_m", p.FilmHi),
                new NamedValue("start_film_share", p.StartFilmShare),
            };
        }
    }

    /// <summary>
    /// The rain on the airframe (p7c-wind 1.7, design 7). Built at the flight's start, its per-flight draws made then
    /// (streams "rain.rate" and "rain.film"); a Sunny flight builds none and opens no stream, so it steps as without rain.
    /// - The drag: the drops' momentum flux on the airframe's silhouette. With u the airframe's velocity through the
    ///   falling drops (body axes) and W = ρ_w·R/V the rain's water content (kg/m³, derived), the mass flux on a box of
    ///   side, top and front silhouettes A is W·(A_x|u_x| + A_y|u_y| + A_z|u_z|) and the force F = −share·W·(A·|u|)·u
    ///   (Aero's silhouettes, the coil's cylinder alike). In a hover it is ρ_w·R·V on the top area, downward.
    /// - The wet mass: a water film over the airframe's wetted surface (the body box's 2(A_x + A_y + A_z) and the coil's
    ///   cylinder), m_max = ρ_w·h·S, filling from the mass the drops bring, dm/dt = ṁ·(1 − m/m_max) (assumed: the share
    ///   a wet surface keeps falls as the film fills; the rest runs or blows off at the airframe's speed, so it carries
    ///   no momentum away). It is a part of the flight's mass model ("rain_film", a box of the body's silhouettes at its
    ///   centre of pressure, assumed: a solid box's inertia per kg under a shell's, and under 2 % of the body's).
    /// </summary>
    public sealed class RainField
    {
        public const string FilmPart = "rain_film";

        public RainField(RainLevel level, FlightRandom random, AeroSpec aero, double coilSurface)
        {
            if (level == RainLevel.Sunny) throw new ArgumentException("Sunny has no rain field", nameof(level));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (aero == null) throw new ArgumentException("rain needs the airframe's silhouettes (an AeroSpec)", nameof(aero));
            Level = level;
            Preset = RainPreset.Of(level);
            SeedStream rate = random.Open("rain.rate");
            RateMmPerHour = random.Draw(ref rate, "rain.rate_mm_h", Preset.RateLo, Preset.RateHi);
            SeedStream film = random.Open("rain.film");
            FilmThickness = random.Draw(ref film, "rain.film_thickness_m", Preset.FilmLo, Preset.FilmHi);

            DropDiameterMm = Preset.DropScale * DetMath.Exp(Preset.DropExponent * DetMath.Log(RateMmPerHour));
            FallSpeed = Preset.FallA - Preset.FallB * DetMath.Exp(-Preset.FallC * DropDiameterMm);
            WaterContent = RainPreset.WaterDensity * RateMmPerHour * RainPreset.MmPerHour / FallSpeed;
            Double3 a = aero.BodyArea;
            WettedArea = 2 * (a.X + a.Y + a.Z) + coilSurface;
            FilmCapacity = RainPreset.WaterDensity * FilmThickness * WettedArea;
            FilmMass = Preset.StartFilmShare * FilmCapacity;
        }

        public RainLevel Level { get; }
        public RainPreset Preset { get; }

        /// <summary>The flight's rain rate, mm/h (a draw in the level's class).</summary>
        public double RateMmPerHour { get; }

        /// <summary>The median-volume drop's diameter, mm, and its fall speed, m/s (derived).</summary>
        public double DropDiameterMm { get; }
        public double FallSpeed { get; }

        /// <summary>The rain's water content, kg of water per m³ of air (derived).</summary>
        public double WaterContent { get; }

        /// <summary>The film's thickness, m (a draw), the wetted surface, m², and the film's capacity, kg.</summary>
        public double FilmThickness { get; }
        public double WettedArea { get; }
        public double FilmCapacity { get; }

        /// <summary>The film's mass now, kg.</summary>
        public double FilmMass { get; private set; }

        /// <summary>The drops' velocity, world m/s, where the air moves at wind.</summary>
        public Double3 DropVelocity(in Double3 wind) => new Double3(wind.X, wind.Y - FallSpeed, wind.Z);

        /// <summary>The drops' force, body N, on a silhouette whose mass flux per kg/m³ of water is flux (m³/s: A·|u|),
        /// for u the airframe's velocity through the drops (body m/s).</summary>
        public Double3 Force(in Double3 u, double flux) => u * (-Preset.MomentumShare * WaterContent * flux);

        /// <summary>One step of the film with the mass the drops bring, kg/s.</summary>
        public void Fill(double massRate, double dt)
        {
            FilmMass = FilmMass + dt * massRate * (1 - FilmMass / FilmCapacity);
            if (FilmMass > FilmCapacity) FilmMass = FilmCapacity;
        }

        /// <summary>The film's part for the mass model (mass 0 until Fill), from the airframe's silhouettes.</summary>
        public static DronePart FilmPartOf(AeroSpec aero)
        {
            Double3 a = aero.BodyArea;
            double volume = DetMath.Sqrt(a.X * a.Y * a.Z);
            return new DronePart
            {
                Name = FilmPart,
                Kind = PartKind.Box,
                Material = "water",
                Mass = 0,
                Position = aero.BodyCenterOfPressure,
                Size = new Double3(volume / a.X, volume / a.Y, volume / a.Z),
            };
        }
    }
}
