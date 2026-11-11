// IWorldQuery: the world questions the physics asks (K§10.4, WLD-11), cut to
// p6-world-query-gallery's calls. Fixed first by task 1.1 (D-041): the flight core's
// contacts build against it, so a later change to it is a superseding decision.
// Implementations: FloorWorld (the stub: an endless floor and runtime objects) and
// MapWorld (task 1.4: a map's placement file and its assets' metadata; the test range,
// the one map of D-047, is MapWorld.Load(repositoryRoot, "test-range")).
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// the public calls of refs/godot/main:game/src/world/WorldQuery.cs (SampleGround,
// ContentHash), Contacts.cs (StaticContacts, Raycast) and WorldObjects.cs (AddObject,
// ResetRuntimeObjects and Geometry, the API review's F2 and F3), gathered into one
// interface. AddObject takes world-space shapes instead of a catalog id, so the stand
// of drone.json and a catalog asset enter the same way. WindGrid and GapsNear are not in
// this interface. MicroDetailNear and Feature joined it in p7a-ground task 1.9 (design 4
// and 8, QueryVersion 5): the first attempt generated its micro-detail by hashing map
// cells; here the elements are each placed asset's own drawn records (AST-10).
//
// Rules for every implementation
// - Engine-free, allocation-free (every result goes into a caller-owned span),
//   reentrant (no shared scratch state) and callable from the physics thread.
// - Only IEEE + - * / and sqrt, through DetMath; fixed iteration order, so every
//   result has the same bits on every runtime, batched or one by one.
// - AddObject and ResetRuntimeObjects change the world: call them before the flight
//   thread starts, never while a query runs.
//
// Frame (D-019): Unity's, +X east, +Y up, +Z north, left-handed, SI.
// - The first attempt's frame was +Z south. Its query math is vector algebra with no
//   handedness (closest points, sweeps, rays, the hash), so it is taken unchanged;
//   only Radial's cross product picks an arbitrary perpendicular, in any frame.
// - The Z flip lives where world data enters, never in these calls: MapWorld reads a
//   placement file and metadata already in Unity's frame (frame "unity-lh-yup"), so it
//   flips nothing; a placement's yaw is D-019's (positive turns north towards east),
//   where the first attempt's positive yaw turned east towards north.
//
// The Time rule of StaticContacts (K§7.5), per (object, shape)
// The capsule's two core ends move linearly from `previous` (t = 0) to `current`
// (t = 1); conservative advancement finds the first t at which it touches the shape
// (within 1 µm), so no shape is missed however thin it is and however far it moved.
// - On a plane the first touch is exact: its Distance is 0 to rounding.
// - A separating plane proves when the sweep cannot touch a shape, so a capsule sliding
//   past or along any shape is never a first touch (QueryVersion 3).
// - A sweep that neither touches nor is proven clear within 128 steps (a core pitching
//   across a curved shape: its far ends swing at the shape while its middle creeps in):
//   - its current pose touches (within 1 µm): the step touched, and bisection between the
//     last pose shown clear and the current one finds that touch, the first touch
//     (QueryVersion 4). It is the first where the distance is convex in t (a translation
//     past a convex shape); on QA's and the tests' pitching sets it was never later.
//   - otherwise it counts no first touch (the current contact, if within the margin). The
//     remainder: a turning graze that dips in and comes back clear within the step may
//     count none (QA's sets: at most about 6 µm deep).
//   So a first touch is always a touch, and a step that ends inside a shape it began
//   clear of is always a first touch.
// - A first touch lies within 1 µm of the shape and not after the true touch, except that
//   a cylinder's may lie up to a few nm inside, up to about 0.1 µm of travel late: its
//   closest approach is a search that reads up to a few nm long (QA's re-check L4).
// - Touching since the step began (t = 0):
//   - the current pose on the same side (normals agree) and within the margin: the
//     current contact, Time = 1;
//   - on the same side and beyond the margin: no contact (it moved clear);
//   - the normals disagree (it went through): the contact at the step's start, Time = 0.
// - First touch during the step (0 < t ≤ 1): the contact at the first touch, Time = t
//   (held below 1), its Distance about 0, so physics latches that plane (K§7.5). The
//   first attempt reported a same-side entry as Time = 1 at the current pose; here every
//   first touch latches, as K§7.5 reads, so every impact is one Time < 1 record.
// - Never touched: the current contact if within the margin, Time = 1.
// A Time < 1 contact whose latched plane shows no penetration at the current pose is a
// graze (API review F8). The capsule's radius is current.Radius.
//
// The contact's point (review-physics W5). Point is on the shape's surface, nearest the
// core point closest to the shape. Where a stretch of the core is equally close (a line
// contact: a skid lying on the plane or along a face), that core point is the stretch's
// end nearest A: on the plane the lower core end (A on a tie); on a box face the start of
// the flat stretch, A itself when A lies over the face; along a parallel capsule or wire
// segment, A's end of the overlap.
//
// The ground (design 5; review-physics W3 and W4, option a). A world may have no plane
// ground over parts of it: MapWorld over each ground patch's footprint (S04, E01) and
// inside each hole band (an asset's holes: a centre line and a band of half its width).
// There SampleGround reports Hole, and the shapes there (a patch's soil and straw, a
// hole's fillers and walls) are the only ground, through StaticContacts, so nothing is
// counted twice. StaticContacts decides it per contact point: the ground's contact is
// kept only where its Point lies outside every patch and band. A capsule straddling a
// band's edge so meets the plane when its lower core end lies outside the band, and the
// shapes inside carry the rest; with its lower end inside, the plane is absent for it.
// Raycast likewise drops a ground hit inside a patch or band, and the ray goes on.
//
// The terrain (w3-farmland-ground 1.1, QueryVersion 8). A map package's heightfield (MapWorld over maps/<id>/map.json;
// Terrain.cs) is the ground in the plane's place, still object WorldIds.Ground, shape 0:
// - SampleGround: the highest tile's height, triangulated as tools/map/validate.py's Tile.height to the bit; its facet's
//   normal; its nearest class sample. Hole over a tile's hole cell (unless a lower tile has ground there) and off every tile.
// - The step lines where the highest tile changes across a tile edge (the farmland's ledge and far seams) are vertical
//   faces of the ground: StaticContacts and Raycast meet them, out of the higher side; SampleGround reports the top.
// - StaticContacts: the ground's one contact (the nearest of its facets and faces; inside it, the deepest point's depth
//   under its facet's plane or behind a face), under the Time rule above. Raycast: the first facet entered from above or
//   face entered from its lower side; a ray that starts inside the ground does not hit it.
// - Geometry has no ground pair on a terrain: it has no one plane. Its local plane is a contact's Point and Normal, or
//   SampleGround's height and normal.
//
// Wires (review-physics W1, W2, W6). A wire is a capsule chain along its point list at
// Radius (half its diameter). Its Distance is the true signed distance to the chain's
// nearest segment, deep inside too; WireParam is the contact's length along the chain
// over the chain's whole length (0 at its first point, 1 at its last). Geometry gives its
// chord (Center its midpoint, AxisY along it, SpanA and SpanB the chain's ends, Sag,
// Radius), and MapWorld.WirePoints(object, shape) the chain itself. A ray that starts
// inside any segment of a wire does not hit that wire, so a rotor ray re-cast from inside
// a conductor goes on to what lies past it.
//
// Wires left to physics (p7d-tether task 1.2, design 9 and 12; QueryVersion 6). StaticContacts
// and Raycast take a QueryFilter: with NoWires they skip every wire shape, so a ray goes on to
// what lies beyond it; with None (the default) every caller sees wires as before, bit for bit.
// WiresNear gives physics the conductors themselves: every placed wire whose swing envelope
// meets a sphere. The envelope is its rest chain inflated by its radius and its blow-out at 45°
// (assumed, design 12): turned about its chord by θ, a point d from the chord moves 2·d·sin(θ/2),
// so for |θ| ≤ 45° by at most 2·d_max·sin 22.5°, d_max the chain's farthest point from its chord.
// A conductor is returned when the centre lies within radius + its radius + that blow-out of its
// rest chain. Its ends are paired at load with the placed ports: an end hangs from the port whose
// seat (its position plus the conductor's radius along its direction, the pole-wires reading §1)
// lies nearest it within 5 mm, the first in canonical order on a tie; an end with none is a world
// bug, named at load in MapWorld.WorldBugs, and reads FittingKind.None. Its neighbour at an end is
// the first other conductor, in canonical order, with an end on the same port.
//
// Snag records near a point (p7d-tether task 1.3, design 12; the fortifications reading §1 and §7; QueryVersion 7).
// SnagsNear gives the fiber the members it can catch on: each placed asset's snag records (its metadata's "snag": an
// edge or a hook, its points in the variant's frame, radius_m, optionally edge_radius_m and, on an edge, barb_pitch_m,
// and the material of the member it follows), placed at their row's pose. A segment is returned when the distance from
// the centre to it is at most radius + its record's radius_m (+ − × ÷ only), its points placed in world space first,
// so the test is of the points returned. It feeds the fiber's 40 Hz caches and never runs in the step (the reading §7);
// the map compiler holds the segments meeting any 2 m cube to 2048 (tools/map), which sizes those caches.

using System;

namespace Fpv450.Sim.World
{
    public static class WorldIds
    {
        /// <summary>The ground's object id (the floor, later the terrain); its one shape is 0. Also a ray miss's.</summary>
        public const int Ground = -1;
        public const ushort NoMaterial = 0;
        public const byte NoSurface = 0;
    }

    public interface IWorldQuery
    {
        /// <summary>The 64-bit FNV-1a hash of the data this world was built from (WorldHash). The flight log records it.</summary>
        ulong ContentHash { get; }

        /// <summary>WorldHash.QueryVersion: the version of the query math, logged beside ContentHash.</summary>
        int QueryVersion { get; }

        /// <summary>One GroundSample per point, into results[0 .. points.Length); Hole where the world has no plane ground (above).</summary>
        void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results);

        /// <summary>
        /// The contacts of a capsule (a sphere when A = B) swept from `previous` to
        /// `current` with the ground (kept where its point lies outside every patch and hole
        /// band, above), every placed shape and wire and
        /// every runtime object within `margin` (m): at most one per (object, shape), under the Time rule
        /// above, in canonical order (object, then shape; the ground first). Returns the
        /// true count, which exceeds results.Length on overflow; the buffer then holds the
        /// first contacts in canonical order. QueryFilter.NoWires leaves every wire out (above).
        /// </summary>
        int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results,
            QueryFilter filter = QueryFilter.None);

        /// <summary>
        /// The first surface each ray enters within maxDistance (m, finite): the ground
        /// (from above only, and never over a patch or inside a hole band), shapes and wires.
        /// A ray that starts inside a shape (or any segment of a wire) does not hit it. A tie
        /// keeps the first in canonical order. QueryFilter.NoWires leaves every wire out, so the
        /// ray goes on to what lies beyond it.
        /// </summary>
        void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits, QueryFilter filter = QueryFilter.None);

        /// <summary>
        /// Every placed conductor whose swing envelope (above) meets the sphere at `centre` of `radius` (m), with its
        /// record's fields, its rest chain and its two ends, in canonical order (object, then shape). Returns the true
        /// count, which exceeds results.Length on overflow; the buffer then holds the first conductors in canonical order.
        /// </summary>
        int WiresNear(Double3 centre, double radius, Span<Conductor> results);

        /// <summary>
        /// Every segment of every placed snag record that meets the sphere at `centre` of `radius` (m) within the record's
        /// radius_m (above), with its record's fields and its two points, in canonical order (object, then record, then
        /// segment). Returns the true count, which exceeds results.Length on overflow; the buffer then holds the first
        /// segments in canonical order.
        /// </summary>
        int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results);

        /// <summary>The world-space geometry of (object, shape); false for a pair that does not exist, and for the ground on a terrain (above).</summary>
        bool Geometry(int obj, int shape, out ShapeGeometry geometry);

        /// <summary>
        /// The micro elements of every placed instance (S04's straws, stems and twigs; the crowns' twigs and leaves) whose
        /// centre line, grown by half its diameter, meets the sphere at `centre` of `radius` (m), of the kinds in `kinds`,
        /// in canonical order (object, then element index), none rooted in a hole. Returns the true count, which exceeds
        /// results.Length on overflow; the buffer then holds the first elements in canonical order.
        /// </summary>
        int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results);

        /// <summary>The ground feature (a pitfall) a placed object's shape belongs to; false for a shape of none.</summary>
        bool Feature(int obj, int shape, out GroundFeature feature);

        /// <summary>
        /// Adds a runtime object (the launch stand) from world-space boxes, spheres,
        /// capsules and cylinders, each with exactly one of a material and a soil surface.
        /// Returns its object id: after the placed objects and the earlier runtime objects.
        /// Throws ArgumentException for any other shape.
        /// </summary>
        int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes);

        /// <summary>Removes every runtime object: the world is as built. Call it before each flight.</summary>
        void ResetRuntimeObjects();

        /// <summary>A placed object's variant id (such as "L01_straight") or a runtime object's name; null for any other id.</summary>
        string ObjectName(int obj);

        /// <summary>The physics material id of `assets-src/physics_materials.json` (such as "timber"); null for NoMaterial or an unknown id.</summary>
        string MaterialName(ushort material);

        /// <summary>The id of a material name; NoMaterial for a name this world does not know.</summary>
        ushort MaterialId(string name);

        /// <summary>The surface name (such as "belt_bare"); null for NoSurface or an unknown id.</summary>
        string SurfaceName(byte surface);
    }
}
