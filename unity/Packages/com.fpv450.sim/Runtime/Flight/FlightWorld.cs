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

        /// <summary>The world-query stub's floor at y = 0 (FloorWorld), for tests and benches.</summary>
        public static FlightWorld Floor() => new FlightWorld(new FloorWorld(0, "belt_bare", "steel"));

        /// <summary>The world itself, for setup before the flight thread starts (runtime objects such as the stand).</summary>
        public IWorldQuery Query => _query;

        public ulong ContentHash => _query.ContentHash;
        public int QueryVersion => _query.QueryVersion;

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _query.SampleGround(points, results);

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results) =>
            _query.StaticContacts(previous, current, margin, results);

        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits) => _query.Raycast(rays, maxDistance, hits);
    }
}
