using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// The wind of a flight. Calm, Windy and Severe are the pilot's levels (D-011, K§8.6), never drawn. Still and
    /// Steady are for benches and tests only: still air, and one fixed vector without turbulence (the "fixed vector"
    /// a real level must never be, K§8.3).
    /// </summary>
    public enum WindLevel : byte { Still = 0, Calm = 1, Windy = 2, Severe = 3, Steady = 4 }

    /// <summary>The wind a flight is set up with; the log header records it.</summary>
    public struct WindSetup
    {
        public WindLevel Level;

        /// <summary>Steady only: the wind's speed, m/s, and the compass bearing it blows from, degrees (0 from the
        /// north, 90 from the east).</summary>
        public double SteadySpeed, SteadyFromDeg;

        /// <summary>Steady only: the time it starts to blow, s (0: from the start). Before it the air is still, then the
        /// whole wind steps in at once: a gust step for the airframe's response (p7b-airframe 1.3c), carried in the gust.</summary>
        public double SteadyOnsetSeconds;

        public static WindSetup Of(WindLevel level) => new WindSetup { Level = level };

        public static WindSetup SteadyFrom(double fromDeg, double speed) =>
            new WindSetup { Level = WindLevel.Steady, SteadySpeed = speed, SteadyFromDeg = fromDeg };
    }

    /// <summary>
    /// A level's preset (design 8, K§8.6): what the level fixes. The stand-in has no height profile and no wakes, so
    /// its turbulence scales are taken at one height, 20 m (assumed: the lower edge of the wind study's 20–50 m test
    /// band, wind.md §6.2), with MIL-F-8785C's low-altitude Dryden formulas (published; h in feet, h = 65.6 ft).
    /// </summary>
    public readonly struct WindPreset
    {
        /// <summary>The model's name in the log header and the summary (PHB-06); p7c-wind replaces the model.</summary>
        public const string Model = "stand-in";

        /// <summary>Severe's mean crosswind, m/s (derived: clip P's 5 m/s at its derived 13 m/s airspeed, wind.md
        /// §5.3; K§8.2 notes the pilot's cruise answer puts it at 6.5–8.5 m/s, which p7c-wind settles with T0).</summary>
        public const double SevereCentre = 5.0;

        /// <summary>Windy's and Calm's centres as fractions of Severe's (derived, K§8.6: c = 0.5 and 0.15; Calm's
        /// 0.75 m/s rounded to 0.8 as design 8 has it).</summary>
        public const double WindyCentre = 2.5, CalmCentre = 0.8;

        /// <summary>Each band is its centre ± 25 % (derived, K§8.6), so no two overlap.</summary>
        public const double Band = 0.25;

        /// <summary>σ_u/U = σ_v/U (assumed: the middle of K§6.7's 0.12–0.15; MIL-F-8785C at 20 m over farmland gives
        /// about 0.14).</summary>
        public const double Intensity = 0.135;

        /// <summary>σ_w/σ_u (derived: MIL-F-8785C's (0.177 + 0.000823·h)^0.4 at h = 65.6 ft).</summary>
        public const double VerticalRatio = 0.556;

        /// <summary>L_u = L_v, m (derived: MIL-F-8785C's h / (0.177 + 0.000823·h)^1.2 at 20 m, the lower edge of
        /// K§6.7's 115–200 m).</summary>
        public const double HorizontalScale = 116;

        /// <summary>L_w, m (derived: MIL-F-8785C's L_w = h, at 20 m).</summary>
        public const double VerticalScale = 20;

        /// <summary>Each gust component is held within this many of its σ (assumed: a 4σ ceiling, met about once in
        /// 16 000 independent samples of a normal variable; it keeps every value inside a stated bound,
        /// flight-randomness).</summary>
        public const double GustBound = 4;

        public readonly WindLevel Level;

        /// <summary>The band the per-flight mean speed is drawn from, m/s.</summary>
        public readonly double MeanLo, MeanHi;

        WindPreset(WindLevel level, double centre)
        {
            Level = level;
            MeanLo = centre * (1 - Band);
            MeanHi = centre * (1 + Band);
        }

        /// <summary>A pilot's level's preset. Still and Steady have none.</summary>
        public static WindPreset Of(WindLevel level)
        {
            switch (level)
            {
                case WindLevel.Calm: return new WindPreset(level, CalmCentre);
                case WindLevel.Windy: return new WindPreset(level, WindyCentre);
                case WindLevel.Severe: return new WindPreset(level, SevereCentre);
                default: throw new ArgumentException("only Calm, Windy and Severe have a preset", nameof(level));
            }
        }
    }

    /// <summary>
    /// The stand-in wind (PHB-06, design 8), until p7c-wind: a mean wind per flight plus simple seeded turbulence,
    /// the same at every point (no profile, no wakes, no spatial field; At takes the point so the rotors and the body
    /// each ask for theirs, as p7c-wind's field will answer).
    /// - The mean: per flight, its bearing (uniform over the compass, stream wind.direction) and its speed (uniform in
    ///   the level's band, stream wind.strength), both per-flight draws in the header. Horizontal.
    /// - The gust: a first-order Gauss–Markov process per world axis (MIL-F-8785C's Dryden u component, the form of
    ///   design 8): horizontal X and Z with σ = Intensity·U and τ = L_u/U, vertical Y with σ·VerticalRatio and
    ///   τ = L_w/U, for the hovering drone's encounter speed U, the mean. σ_u = σ_v and τ_u = τ_v, so the horizontal
    ///   gust is isotropic and needs no rotation into the mean's axes. Each 1 ms step, g ← a·g + c·n with
    ///   a = τ/(τ + dt) (no exp) and c = σ·√(3·(1 − a²)), n uniform in [−1, 1) from the counter-based stream
    ///   wind.turbulence at draw 3·step + axis: the stationary variance is σ² exactly, and a sum of thousands of
    ///   such increments is normal. The flight starts with the process at rest in its stationary law: one nearly
    ///   normal per-flight draw per axis (stream wind.gust0). Each component is clamped to ±GustBound·σ.
    /// Randomness enters only the wind's velocity, a physical parameter, and reaches the body only through the drag
    /// and rotor forces (flight-randomness): never the pose. The gust is integrated state (FlightState.Gust), logged
    /// every step.
    /// </summary>
    public sealed class StandInWind
    {
        const double Dt = 0.001; // FlightCore.Dt
        const double DegToRad = 0.017453292519943295;

        public readonly WindSetup Setup;

        /// <summary>The bearing the mean wind blows from, degrees, and its speed, m/s (the per-flight draws).</summary>
        public readonly double FromDeg, MeanSpeed;

        /// <summary>The mean wind's velocity, world, m/s.</summary>
        public readonly Double3 Mean;

        /// <summary>Each gust component's σ, m/s, its time constant τ, s, and its bound, m/s: horizontal, vertical.</summary>
        public readonly double SigmaH, SigmaV, TauH, TauV, BoundH, BoundV;

        /// <summary>The gust the flight starts with, world, m/s (the per-flight draws of wind.gust0).</summary>
        public readonly Double3 StartGust;

        readonly bool _turbulent;
        readonly long _onsetStep;
        readonly SeedStream _turbulence;
        readonly double _aH, _cH, _aV, _cV;

        /// <summary>The wind of a flight with this setup, its per-flight draws made from random (at the flight's start).</summary>
        public StandInWind(in WindSetup setup, FlightRandom random)
        {
            Setup = setup;
            switch (setup.Level)
            {
                case WindLevel.Still:
                    return;
                case WindLevel.Steady:
                    if (!(setup.SteadySpeed >= 0)) throw new ArgumentException("a steady wind's speed is 0 or more", nameof(setup));
                    FromDeg = setup.SteadyFromDeg;
                    MeanSpeed = setup.SteadySpeed;
                    Mean = Toward(FromDeg, MeanSpeed);
                    if (!(setup.SteadyOnsetSeconds >= 0)) throw new ArgumentException("a steady wind's onset is 0 s or later", nameof(setup));
                    _onsetStep = (long)(setup.SteadyOnsetSeconds / Dt + 0.5);
                    if (_onsetStep > 0) StartGust = new Double3(-Mean.X, -Mean.Y, -Mean.Z);
                    return;
            }
            if (random == null) throw new ArgumentNullException(nameof(random));
            WindPreset preset = WindPreset.Of(setup.Level);
            SeedStream direction = random.Open("wind.direction");
            FromDeg = random.Draw(ref direction, "wind.from_deg", 0, 360);
            SeedStream strength = random.Open("wind.strength");
            MeanSpeed = random.Draw(ref strength, "wind.mean_speed_m_s", preset.MeanLo, preset.MeanHi);
            Mean = Toward(FromDeg, MeanSpeed);

            SigmaH = WindPreset.Intensity * MeanSpeed;
            SigmaV = SigmaH * WindPreset.VerticalRatio;
            TauH = WindPreset.HorizontalScale / MeanSpeed;
            TauV = WindPreset.VerticalScale / MeanSpeed;
            BoundH = WindPreset.GustBound * SigmaH;
            BoundV = WindPreset.GustBound * SigmaV;
            _aH = TauH / (TauH + Dt);
            _aV = TauV / (TauV + Dt);
            _cH = SigmaH * DetMath.Sqrt(3 * (1 - _aH * _aH));
            _cV = SigmaV * DetMath.Sqrt(3 * (1 - _aV * _aV));

            SeedStream start = random.Open("wind.gust0");
            StartGust = new Double3(
                random.DrawNormal(ref start, "wind.gust0.east_m_s", SigmaH, BoundH),
                random.DrawNormal(ref start, "wind.gust0.up_m_s", SigmaV, BoundV),
                random.DrawNormal(ref start, "wind.gust0.north_m_s", SigmaH, BoundH));
            _turbulence = random.Open("wind.turbulence");
            _turbulent = true;
        }

        /// <summary>The wind's velocity of a speed blowing from a bearing, world (+X east, +Z north), m/s.</summary>
        public static Double3 Toward(double fromDeg, double speed)
        {
            DetMath.SinCos(fromDeg * DegToRad, out double sin, out double cos);
            return new Double3(-sin * speed, 0, -cos * speed);
        }

        /// <summary>
        /// Advances the gust over the step that starts at `step` (the state's step before it): g ← a·g + c·n, n from
        /// wind.turbulence's draws 3·step to 3·step + 2, then clamped. Still and Steady air has no gust (a steady wind with an onset
        /// carries −Mean in it until the onset step, then none).
        /// </summary>
        public void Advance(ref Double3 gust, long step)
        {
            if (_onsetStep > 0 && step + 1 >= _onsetStep) gust = default;
            if (!_turbulent) return;
            ulong i = unchecked((ulong)step * 3);
            gust.X = Clamp(_aH * gust.X + _cH * Uniform(i), BoundH);
            gust.Y = Clamp(_aV * gust.Y + _cV * Uniform(i + 1), BoundV);
            gust.Z = Clamp(_aH * gust.Z + _cH * Uniform(i + 2), BoundH);
        }

        /// <summary>The wind's velocity at a world point with the gust in force, world, m/s.</summary>
        public Double3 At(in Double3 gust, in Double3 point) => Mean + gust;

        // Draw i of wind.turbulence as a uniform value in [−1, 1): the top 53 bits, exactly.
        double Uniform(ulong i) => 2 * ((_turbulence.BitsAt(i) >> 11) * (1.0 / 9007199254740992.0)) - 1;

        static double Clamp(double x, double bound) => x > bound ? bound : x < -bound ? -bound : x;
    }
}
