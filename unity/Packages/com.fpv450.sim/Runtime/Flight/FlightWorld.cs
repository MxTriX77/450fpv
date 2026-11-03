using System;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The flight core's one door to the world (design 1): every query a step makes goes through here, and the log
    /// header reads the world's identity here. It wraps p6-world-query-gallery's IWorldQuery (K§10.4); a change to
    /// that interface is absorbed in this file.
    /// </summary>
    public sealed class FlightWorld
    {
        readonly IWorldQuery _query;

        public FlightWorld(IWorldQuery query) => _query = query ?? throw new ArgumentNullException(nameof(query));

        /// <summary>
        /// The world-query stub's floor at y = 0 (FloorWorld), for tests and benches: surface belt_bare, and the
        /// materials of the stand (steel) and of the stub's pole-sized runtime objects (timber, L01_straight's).
        /// </summary>
        public static FlightWorld Floor() => new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber"));

        /// <summary>The world itself, for setup before the flight thread starts (runtime objects such as the stand).</summary>
        public IWorldQuery Query => _query;

        public ulong ContentHash => _query.ContentHash;
        public int QueryVersion => _query.QueryVersion;

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _query.SampleGround(points, results);

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results) =>
            _query.StaticContacts(previous, current, margin, results);

        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits) => _query.Raycast(rays, maxDistance, hits);

        public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _query.Geometry(obj, shape, out geometry);

        /// <summary>The soil ray's reach, m (assumed: past any flight's height over a map and the wind profile's 100 m top).</summary>
        public const double SoilReach = 10000;

        /// <summary>The casts SoilUnder takes past shapes that are not soil (a crown's branches, a wire, a pole) before it
        /// gives up (assumed: eight times the 4 that a scan of the test range's patches and run needs at most, past its belts'
        /// crowns and stand-ins, RangeContactTests).</summary>
        public const int SoilCasts = 32;

        /// <summary>How far past a shape's entry SoilUnder's next cast starts, m: inside it, so the ray goes on (IWorldQuery:
        /// a ray that starts inside a shape does not hit it; a convex shape the ray has left it never meets again).</summary>
        public const double PastEntry = 1e-6;

        /// <summary>
        /// The ground's height under a point over a hole (model 20, the wind's ground): where SampleGround reports Hole the
        /// patch's or the hole's own shapes are the ground (S04's soil under its straw mat, E01's fillers), so a ray straight
        /// down gives the height the wind's profile, roughness and wakes read, as SampleGround's plane gives it everywhere
        /// else: the top of the object whose soil shape the ray meets first, so S04's straw boxes (the mat whose top the
        /// plane's belt_straw stands for, y 0) count with their soil box, and a bare stretch of soil between them is its own
        /// height. Another object's shapes stand on the ground (a crown, a wire, a pole: the plane's height ignores objects
        /// too), so the ray goes on past them. −∞ for no soil under the point (it lies in or under the soil, or past
        /// SoilCasts), which the wind takes as sheltered. `ray` and `hit` are the caller's one-element buffers (no
        /// allocation); `casts` the rays it cast.
        /// </summary>
        public double SoilUnder(in Double3 point, Span<Ray> ray, Span<RayHit> hit, out int casts)
        {
            var down = new Double3(0, -1, 0);
            double y = point.Y, top = point.Y;
            int run = WorldIds.Ground; // the object of the run of hits that ends at this one, and where the run began
            for (casts = 1; casts <= SoilCasts; casts++)
            {
                ray[0] = new Ray(new Double3(point.X, y, point.Z), down);
                _query.Raycast(ray.Slice(0, 1), SoilReach, hit.Slice(0, 1));
                if (!(hit[0].Distance < double.PositiveInfinity)) return double.NegativeInfinity;
                if (casts == 1 || hit[0].Object != run)
                {
                    run = hit[0].Object;
                    top = hit[0].Point.Y;
                }
                if (hit[0].Surface != WorldIds.NoSurface) return top;
                y = hit[0].Point.Y - PastEntry;
            }
            casts = SoilCasts;
            return double.NegativeInfinity;
        }
    }

    /// <summary>
    /// A runtime object the flight places in its world before its first step (the launch stand, a test's pole): its name
    /// and world-space shapes, their material and surface ids this world's. The flight resets the world's runtime
    /// objects and adds these in order (FlightCore), and the log header records them, so a replay places the same ones
    /// (K§6.10: asset, pose and order; QA review-base finding 5).
    /// </summary>
    public sealed class RuntimeObject
    {
        public string Name;
        public ShapeGeometry[] Shapes;

        public RuntimeObject(string name, ShapeGeometry[] shapes)
        {
            Name = name;
            Shapes = shapes;
        }
    }
}
