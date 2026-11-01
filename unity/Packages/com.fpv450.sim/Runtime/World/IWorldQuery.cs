// IWorldQuery: the world questions the physics asks (K§10.4, WLD-11), cut to
// p6-world-query-gallery's calls. Fixed first by task 1.1 (D-041): the flight core's
// contacts build against it, so a later change to it is a superseding decision.
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// the public calls of refs/godot/main:game/src/world/WorldQuery.cs (SampleGround,
// ContentHash), Contacts.cs (StaticContacts, Raycast) and WorldObjects.cs (AddObject,
// ResetRuntimeObjects and Geometry, the API review's F2 and F3), gathered into one
// interface. AddObject takes world-space shapes instead of a catalog id, so the stand
// of drone.json and a catalog asset enter the same way. MicroDetailNear, WindGrid and
// GapsNear are not in this change.
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
// - The Z flip lives where world data enters, never in these calls: the gallery loader
//   reads metadata already in Unity's frame (asset-pipeline: frame "unity-lh-yup"), so
//   it flips nothing (task 1.3); phase 3's map grid puts row 0 on the +Z (north) edge,
//   where the first attempt's row index grew towards +Z (south); and a placement's yaw
//   is D-019's (positive turns north towards east), where the first attempt's positive
//   yaw turned east towards north.
//
// The Time rule of StaticContacts (K§7.5), per (object, shape)
// The capsule's two core ends move linearly from `previous` (t = 0) to `current`
// (t = 1); conservative advancement finds the first t at which it touches the shape
// (within 1 µm), so no shape is missed however thin it is and however far it moved.
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

        /// <summary>One GroundSample per point, into results[0 .. points.Length).</summary>
        void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results);

        /// <summary>
        /// The contacts of a capsule (a sphere when A = B) swept from `previous` to
        /// `current` with the ground (absent over a hole), every placed shape and wire and
        /// every runtime object within `margin` (m): at most one per (object, shape), under the Time rule
        /// above, in canonical order (object, then shape; the ground first). Returns the
        /// true count, which exceeds results.Length on overflow; the buffer then holds the
        /// first contacts in canonical order.
        /// </summary>
        int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results);

        /// <summary>
        /// The first surface each ray enters within maxDistance (m, finite): the ground
        /// (from above only, and never over a hole), shapes and wires. A ray that starts
        /// inside a shape does not hit it. A tie keeps the first in canonical order.
        /// </summary>
        void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits);

        /// <summary>The world-space geometry of (object, shape); false for a pair that does not exist.</summary>
        bool Geometry(int obj, int shape, out ShapeGeometry geometry);

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
