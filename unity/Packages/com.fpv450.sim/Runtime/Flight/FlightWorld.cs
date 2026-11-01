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
