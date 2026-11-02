// The inputs and results of IWorldQuery (K§10.4, cut to p6-world-query-gallery's calls).
// SI units, D-019's world frame (+X east, +Y up, +Z north, left-handed).
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// - refs/godot/main:game/src/world/WorldQuery.cs: XZ, GroundFlags, GroundSample
// - refs/godot/main:game/src/world/Contacts.cs: Capsule, StaticContact, Ray, RayHit
// - refs/godot/main:game/src/world/Catalog.cs: ShapeKind
// - refs/godot/main:game/src/world/WorldObjects.cs: ShapeGeometry (the API review's F3)
// Changes: every vector and scalar is a double (no System.Numerics, no float); the
// ground sample is cut to height, normal, surface and flags; Shape is an int and
// RayHit names it too; ShapeKind gains Plane (the ground); ShapeGeometry flattens its
// Axes into AxisX/Y/Z, gives HalfLength in place of Height, and holds one span per
// wire, since each wire record of the Unity metadata is one span. Nothing here depends
// on handedness; the first attempt's +Z south frame touched only its map grid and
// object yaw, which this change does not port (see IWorldQuery).

using System;

namespace Fpv450.Sim.World
{
    /// <summary>A horizontal world position, m.</summary>
    public readonly struct XZ
    {
        public readonly double X, Z;

        public XZ(double x, double z)
        {
            X = x;
            Z = z;
        }
    }

    [Flags]
    public enum GroundFlags : byte
    {
        None = 0,
        /// <summary>
        /// No plane ground at this point: over a ground patch's footprint or inside a hole band (IWorldQuery, The ground).
        /// The shapes there (a patch's soil and straw, a hole's fillers and walls) are the ground, through StaticContacts.
        /// </summary>
        Hole = 1,
    }

    /// <summary>The ground under one (x, z) point.</summary>
    public struct GroundSample
    {
        /// <summary>The plane ground's height (K§10.4's ground height), m; −∞ where Hole.</summary>
        public double Height;
        /// <summary>Unit normal of the ground at the point; +Y where Hole.</summary>
        public Double3 Normal;
        /// <summary>The ground's surface id (IWorldQuery.SurfaceName); WorldIds.NoSurface where Hole.</summary>
        public byte Surface;
        public GroundFlags Flags;
    }

    /// <summary>A query capsule: its core segment A–B and its radius, m. A sphere has A = B.</summary>
    public readonly struct Capsule
    {
        public readonly Double3 A, B;
        public readonly double Radius;

        public Capsule(Double3 a, Double3 b, double radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }
    }

    /// <summary>A swept capsule's contact with one shape (IWorldQuery.StaticContacts).</summary>
    public struct StaticContact
    {
        /// <summary>On the shape's surface, m: at the first touch when Time &lt; 1, else at the current pose (a line contact's: IWorldQuery).</summary>
        public Double3 Point;
        /// <summary>Unit, out of the shape toward the capsule.</summary>
        public Double3 Normal;
        /// <summary>Signed, surface to surface, m: negative = penetration, positive = within the margin.</summary>
        public double Distance;
        /// <summary>WorldIds.Ground, or a placed object, then the runtime objects after them.</summary>
        public int Object;
        /// <summary>The shape's index within its object (Geometry); 0 for the ground.</summary>
        public int Shape;
        /// <summary>The shape's material id (MaterialName); WorldIds.NoMaterial for a soil shape and the ground.</summary>
        public ushort Material;
        /// <summary>Non-zero: a soil shape or the ground, with this surface id (SurfaceName). Physics dispatches on it.</summary>
        public byte Surface;
        /// <summary>A wire's: the contact's length along its chain over the chain's length, 0 at its first point, 1 at its last; −1 for every other shape.</summary>
        public double WireParam;
        /// <summary>1: at the current pose. Below 1: the fraction of the step at the first touch (the Time rule, IWorldQuery).</summary>
        public double Time;
    }

    /// <summary>A ray from Origin along Direction, m. Raycast normalises Direction, which must not be zero.</summary>
    public readonly struct Ray
    {
        public readonly Double3 Origin, Direction;

        public Ray(Double3 origin, Double3 direction)
        {
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>The first surface a ray enters (IWorldQuery.Raycast).</summary>
    public struct RayHit
    {
        /// <summary>m along the ray; +∞ for a miss.</summary>
        public double Distance;
        public Double3 Point;
        /// <summary>Unit, out of the surface hit.</summary>
        public Double3 Normal;
        /// <summary>WorldIds.Ground for the ground and for a miss (a miss has Surface 0); else the object hit.</summary>
        public int Object;
        public int Shape;
        public ushort Material;
        public byte Surface;
    }

    public enum ShapeKind : byte
    {
        /// <summary>The ground's half-space: everything below the plane through Center with normal AxisY is inside.</summary>
        Plane,
        Box,
        Sphere,
        Capsule,
        Cylinder,
        /// <summary>A conductor: a capsule chain along its point list at Radius (task 1.4). Geometry holds its chord; MapWorld.WirePoints the chain.</summary>
        Wire,
    }

    /// <summary>
    /// One shape in world space: what Geometry returns and what AddObject takes. The
    /// axes are the shape's local axes in world space, orthonormal with AxisX x AxisY =
    /// AxisZ (DetMath's cross product, as Unity's rotation of the metadata's local axes).
    /// </summary>
    public struct ShapeGeometry
    {
        public ShapeKind Kind;
        /// <summary>The box's, sphere's or cylinder's centre, the capsule core's midpoint, a wire's chord midpoint, or a point on the plane, m.</summary>
        public Double3 Center;
        /// <summary>Local axes in world space. A capsule, a cylinder and a wire's chord run along AxisY; a plane's normal is AxisY.</summary>
        public Double3 AxisX, AxisY, AxisZ;
        /// <summary>Box: half its size along AxisX, AxisY and AxisZ, m.</summary>
        public Double3 HalfExtents;
        /// <summary>Sphere, capsule, cylinder and wire (half the wire's diameter), m.</summary>
        public double Radius;
        /// <summary>Capsule: half its core segment (the metadata's height / 2 − radius). Cylinder: half its height. m.</summary>
        public double HalfLength;
        /// <summary>The material id; WorldIds.NoMaterial for a soil shape.</summary>
        public ushort Material;
        /// <summary>Non-zero for a soil shape (the metadata's "surface:&lt;id&gt;") and the ground.</summary>
        public byte Surface;
        /// <summary>Wire: the end points of its span (its chain's first and last points), m.</summary>
        public Double3 SpanA, SpanB;
        /// <summary>Wire: its sag at mid-span (the metadata's sag_m), m.</summary>
        public double Sag;
    }
}
