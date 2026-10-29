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

        /// <summary>A pilot's level only: the second of the seed's direction history the flight starts at (0 for every
        /// flight but the harness's, whose start rule picks it, K§8.5), 0 to WindPreset.Horizon.</summary>
        public int StartSecond;

        /// <summary>A synthetic tree belt (p7c-wind 1.2's harness, until 1.3's wake grid), or null for none (every flight
        /// but the harness's).</summary>
        public SyntheticBelt Belt;

        /// <summary>A gust front (p7c-wind 1.4's per-rotor scenario), or null for none (every other flight).</summary>
        public GustFront Front;

        public static WindSetup Of(WindLevel level) => new WindSetup { Level = level };

        public static WindSetup SteadyFrom(double fromDeg, double speed) =>
            new WindSetup { Level = WindLevel.Steady, SteadySpeed = speed, SteadyFromDeg = fromDeg };
    }

    /// <summary>
    /// A synthetic tree belt (p7c-wind 1.2, labelled: the harness's belt until 1.3's wake grid lands). A region, not a
    /// wake: it has no wake physics and does not follow the wind's direction. It starts at a line through Edge across the
    /// horizontal unit vector Across and runs RegionHeights belt heights along it (K§8.5: from the belt's upwind edge to
    /// 10 belt heights downwind), at every height. Inside:
    /// - the mean wind cut by Deficit,
    /// - plus a field of eddies: Modes plane waves u = a·ĉ·cos(κ·(x − X(t)) + φ), κ in a uniformly drawn direction
    ///   with a wavelength uniform in EddyMin–EddyMax, ĉ across κ (no divergence, so the field carries vorticity) at a
    ///   drawn angle, φ uniform; a = σ_b·√(6/Modes) gives each component σ_b = √(Boost² − 1)·Intensity·U, so the belt's
    ///   turbulence intensity is Boost times the open field's. Bounded by σ_b·√(6·Modes). Frozen and advected with the
    ///   mean wind, X(t) the field's drift (WindField, 1.4), inside the region, which stays where it is.
    /// Both scaled by the region's weight: 0 at its ends, 1 from Ramp belt heights in, smooth (t²·(3 − 2t)).
    /// Every value is assumed and named; the log header records them.
    /// </summary>
    public sealed class SyntheticBelt
    {
        /// <summary>The region's length in belt heights (K§8.5: to 10 belt heights downwind).</summary>
        public const double RegionHeights = 10;

        /// <summary>A point on the belt's upwind edge (world, m; its height is not used) and the horizontal unit vector
        /// from it into the region.</summary>
        public Double3 Edge, Across;

        /// <summary>The belt's height H, m.</summary>
        public double Height;

        /// <summary>The mean wind's deficit inside (assumed: a porous belt's 20–50 % near the ground, less 2–5 H
        /// above it).</summary>
        public double Deficit = 0.2;

        /// <summary>The belt's turbulence intensity over the open field's (assumed: shelterbelt studies' about 2×,
        /// K§8.2).</summary>
        public double Boost = 2;

        /// <summary>The eddies' wavelengths, m (measured: clip P's eddies 2.5–11 m across and 5–20 m apart, K§8.2).</summary>
        public double EddyMin = 5, EddyMax = 20;

        /// <summary>The eddy field's plane waves (assumed).</summary>
        public int Modes = 24;

        /// <summary>The ramps at the region's ends, belt heights (assumed).</summary>
        public double Ramp = 1;

        /// <summary>The region's weight at a world point: 0 outside, ramping to 1 inside.</summary>
        public double Weight(in Double3 point)
        {
            double x = Double3.Dot(point - Edge, Across) / Height;
            if (!(x > 0 && x < RegionHeights)) return 0;
            return Smooth(x / Ramp) * Smooth((RegionHeights - x) / Ramp);
        }

        internal SyntheticBelt Checked()
        {
            double across = Across.X * Across.X + Across.Z * Across.Z;
            if (!(Height > 0) || !(Across.Y == 0) || !(DetMath.Abs(across - 1) < 1e-12) || !(Deficit >= 0 && Deficit <= 1)
                || !(Boost >= 1) || !(EddyMin > 0 && EddyMax >= EddyMin) || Modes < 1 || !(Ramp > 0 && 2 * Ramp <= RegionHeights))
                throw new ArgumentException("the synthetic belt is out of its bounds");
            return this;
        }

        internal static double Smooth(double t) => t >= 1 ? 1 : t <= 0 ? 0 : t * t * (3 - 2 * t);
    }

    /// <summary>
    /// A gust front (p7c-wind 1.4, labelled: a deterministic stimulus for the per-rotor gradient scenario, PR-3; no level
    /// draws it). A plane across the horizontal unit vector Normal moves along it at Speed from Start (world, m, at the
    /// flight's start); behind it the wind carries Delta more (world, m/s), ramping smoothly (t²·(3 − 2t)) over Width
    /// across the plane. It adds to the level's wind at every point, so each rotor meets it when the plane reaches its
    /// hub. Uniform is the known-wrong field: every point takes the front's value at the body's origin, as a field that
    /// is the same everywhere over the drone would. The log header records it.
    /// </summary>
    public sealed class GustFront
    {
        public Double3 Start, Normal, Delta;
        public double Speed, Width;
        public bool Uniform;

        /// <summary>The front's gust at a world point at flight time t, s.</summary>
        public Double3 At(in Double3 point, double t)
        {
            double behind = (Speed * t - Double3.Dot(point - Start, Normal)) / Width + 0.5;
            return Delta * SyntheticBelt.Smooth(behind);
        }

        internal GustFront Checked()
        {
            double n = Normal.X * Normal.X + Normal.Z * Normal.Z;
            if (!(Width > 0) || !(Normal.Y == 0) || !(DetMath.Abs(n - 1) < 1e-12) || !(DetMath.Abs(Speed) < 1e3)
                || !(Delta.Length() < 1e3))
                throw new ArgumentException("the gust front is out of its bounds");
            return this;
        }
    }
}
