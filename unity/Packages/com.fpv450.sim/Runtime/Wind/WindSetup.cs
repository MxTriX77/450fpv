using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

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
        /// whole wind steps in at once: a gust step for the airframe's response (p7b-airframe 1.3c).</summary>
        public double SteadyOnsetSeconds;

        /// <summary>A pilot's level only: the second of the seed's direction history the flight starts at (0 for every
        /// flight but the harness's, whose start rule picks it, K§8.5), 0 to WindPreset.Horizon.</summary>
        public int StartSecond;

        /// <summary>The obstacles whose wakes the field carries (p7c-wind 1.5: the wake grid of 1.3, built from them), or
        /// null for none (open country).</summary>
        public WakeLayout Wakes;

        /// <summary>A gust front (p7c-wind 1.4's per-rotor scenario), or null for none (every other flight).</summary>
        public GustFront Front;

        /// <summary>A wooded strip whose canopy flow the field carries (p7c-wind 1.5, WindField's canopy layer), or null
        /// for none. Not with Wakes: the wake grid would read the same trees as a thin obstacle.</summary>
        public CanopyStrip Canopy;

        public static WindSetup Of(WindLevel level) => new WindSetup { Level = level };

        public static WindSetup SteadyFrom(double fromDeg, double speed) =>
            new WindSetup { Level = WindLevel.Steady, SteadySpeed = speed, SteadyFromDeg = fromDeg };
    }

    /// <summary>
    /// The obstacles of a flight's wakes (p7c-wind 1.5, design 4): the wake grid's obstacle grid (K§10.3) over its origin
    /// (world X, Z of its south-west corner, m) and its 2 m cells, from the placed wind volumes. The field builds its
    /// WakeGridWorker from it at the flight's start; the log header records every value, so a replay rebuilds the same
    /// grid (WakeGrid.Version with it). The replay's fixture row (--wakes) is one.
    /// </summary>
    public sealed class WakeLayout
    {
        public double OriginX, OriginZ;
        public int NX, NZ;
        public WindVolume[] Volumes;

        /// <summary>The obstacle grid (K§10.3).</summary>
        public WakeObstacles Obstacles() => new WakeObstacles(OriginX, OriginZ, NX, NZ, Volumes);

        internal WakeLayout Checked()
        {
            if (NX < 1 || NZ < 1 || NX > 4096 || NZ > 4096 || Volumes == null || !(DetMath.Abs(OriginX) < 1e7) || !(DetMath.Abs(OriginZ) < 1e7))
                throw new ArgumentException("the wake layout is out of its bounds");
            return this;
        }
    }

    /// <summary>
    /// A wooded strip (p7c-wind 1.5, belt-geometry-p.md: clip P's belt is a wide strip of scrub with scattered tall
    /// trees, the wind along it): a rectangle in plan around its centre (world X, Z, m), its long axis at the bearing
    /// AxisDeg (degrees from north toward east), Length along that axis and Width across it, m; its canopy's height h,
    /// m (the tall trees'), and its frontal area index λ (the trees' and the scrub's frontal area per ground area, so
    /// Raupach (1994)'s canopy area index is 2λ). WindField's canopy layer derives the flow inside and above it from
    /// these. The log header records every value. The harness's strip (WindHarness.Strip) is one, until the world's
    /// woodlands carry theirs.
    /// </summary>
    public sealed class CanopyStrip
    {
        public double CentreX, CentreZ, AxisDeg, Length, Width, Height, FrontalAreaIndex;

        internal CanopyStrip Checked()
        {
            if (!(DetMath.Abs(CentreX) < 1e7) || !(DetMath.Abs(CentreZ) < 1e7) || !(DetMath.Abs(AxisDeg) <= 360)
                || !(Length > 0 && Length < 1e5) || !(Width > 0 && Width < 1e5) || !(Height >= 1 && Height <= 60)
                || !(FrontalAreaIndex >= 0.01 && FrontalAreaIndex <= 5))
                throw new ArgumentException("the canopy strip is out of its bounds");
            return this;
        }
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
            return Delta * Smooth(behind);
        }

        internal static double Smooth(double t) => t >= 1 ? 1 : t <= 0 ? 0 : t * t * (3 - 2 * t);

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
