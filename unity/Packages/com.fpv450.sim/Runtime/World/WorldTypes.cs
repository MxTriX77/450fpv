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
        /// No plane or terrain ground at this point: over a ground patch's footprint or inside a hole band (IWorldQuery, The
        /// ground), over a terrain's hole cell, or off every tile of a terrain (IWorldQuery, The terrain). The shapes there (a
        /// patch's soil and straw, a hole's fillers and walls) are the ground, through StaticContacts.
        /// </summary>
        Hole = 1,
    }

    /// <summary>The ground under one (x, z) point.</summary>
    public struct GroundSample
    {
        /// <summary>The ground's height (K§10.4's ground height: the plane's, or a terrain's highest tile), m; −∞ where Hole.</summary>
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

    /// <summary>A micro element's kind (the metadata's micro_elements records, AST-10); as a mask, the kinds a query wants.</summary>
    [Flags]
    public enum MicroKind : byte
    {
        None = 0,
        Straw = 1,
        Stem = 2,
        Twig = 4,
        Leaf = 8,
        All = Straw | Stem | Twig | Leaf,
    }

    /// <summary>
    /// One drawn element of a placed instance (IWorldQuery.MicroDetailNear; p7a-ground design 8): a straw, stem, twig or
    /// leaf of its variant's micro_elements records, placed in world space at its instance's pose.
    /// </summary>
    public struct MicroElement
    {
        /// <summary>Stable: (Object &lt;&lt; 32) | Index, the instance and the element's index in its variant's records.</summary>
        public ulong Id;
        public MicroKind Kind;
        /// <summary>Held at its base (rooted, grown from a parent, or a lying straw held by the mat's tangle): only an anchored element can hook.</summary>
        public bool Anchored;
        /// <summary>The centre line's points: 2 or 3.</summary>
        public byte PointCount;
        /// <summary>The centre line in world space, m, base first: P0–P1 and P1–P2 (P2 = P1 for two points).</summary>
        public Double3 P0, P1, P2;
        /// <summary>The drawn tube's diameter at its base, m (it tapers toward its tip; a leaf's thickness is ignored).</summary>
        public double Diameter;
        /// <summary>A leaf's area, m²; 0 for the other kinds.</summary>
        public double Area;
        /// <summary>The index of the element of the same instance it grows from; −1 for none.</summary>
        public int Parent;
        /// <summary>The collision shape of the same instance it grows from (Geometry(Object, ParentShape)); −1 for none.</summary>
        public int ParentShape;

        /// <summary>The placed instance (object id).</summary>
        public int Object => (int)(Id >> 32);

        /// <summary>The element's index in its variant's records (bake order).</summary>
        public int Index => (int)(uint)Id;
    }

    /// <summary>What StaticContacts and Raycast leave out (p7d-tether design 9 and 12): None sees everything, as before.</summary>
    [Flags]
    public enum QueryFilter : byte
    {
        None = 0,
        /// <summary>No wire shapes: physics owns the conductors' live state and meets them through WiresNear.</summary>
        NoWires = 1,
        /// <summary>No ground (StaticContacts only; Raycast ignores it): the fiber's queries, whose nodes take the ground from
        /// SampleGround's batch, so a terrain's pieces cost them nothing (w3-farmland-ground 2.1).</summary>
        NoGround = 2,
        /// <summary>The ground alone (StaticContacts and Raycast): its plane or its terrain, a step's faces included, and no
        /// placed or runtime shape: the fiber's test of a segment against the ground it lies on (w3-farmland-ground 2.5).</summary>
        GroundOnly = 4,
    }

    /// <summary>The fitting a conductor's end hangs from (IWorldQuery.WiresNear).</summary>
    public enum FittingKind : byte
    {
        /// <summary>No port within 5 mm of the end's seat: a world bug, reported at load (MapWorld.WorldBugs).</summary>
        None = 0,
        /// <summary>A pole's insulator hook: a port of an asset of any class but houses (L01's hook_0 … hook_3).</summary>
        PoleHook = 1,
        /// <summary>A house's wall anchor: a port of a houses-class asset.</summary>
        HouseAnchor = 2,
    }

    /// <summary>One end of a conductor (Conductor.A at its first point, Conductor.B at its last).</summary>
    public struct WireEnd
    {
        /// <summary>The placed object whose port it hangs from; −1 for none.</summary>
        public int Object;
        /// <summary>That port's index among its object's metadata ports, in their order; −1 for none.</summary>
        public int Port;
        public FittingKind Kind;
        /// <summary>The span sharing the port on the same conductor line (its object and wire shape); −1 and −1 for none (a line's last pole).</summary>
        public int NeighbourObject, NeighbourShape;
    }

    /// <summary>
    /// A placed conductor near a point (IWorldQuery.WiresNear; p7d-tether design 12): its wire record's fields, its rest
    /// chain and its two ends. The chain is the world's rest shape; physics owns its live state (design 9).
    /// </summary>
    public struct Conductor
    {
        /// <summary>The placed object and its wire shape (Geometry gives its chord, span and sag).</summary>
        public int Object, Shape;
        /// <summary>The record's diameter_m, m.</summary>
        public double Diameter;
        /// <summary>The record's mass_kg_per_m, kg/m.</summary>
        public double MassPerMetre;
        /// <summary>The record's sag_m at mid-span, m.</summary>
        public double Sag;
        /// <summary>EA: the record's axial_stiffness_n, N.</summary>
        public double AxialStiffness;
        /// <summary>T_b: the record's breaking_force_n, N.</summary>
        public double BreakingForce;
        /// <summary>EI: the record's bending_stiffness_n_m2, N·m² (p7d-tether 1.4; required, above 0).</summary>
        public double BendingStiffness;
        /// <summary>The arris the fiber bends over: its own edge snag record's edge_radius_m (a strand), else half its diameter, m.</summary>
        public double EdgeRadius;
        /// <summary>The record's material id (cable).</summary>
        public ushort Material;
        /// <summary>The rest chain in world space, first point first (MapWorld.WirePoints); the world's own, read-only.</summary>
        public ReadOnlyMemory<Double3> Points;
        /// <summary>The ends at the first and the last point.</summary>
        public WireEnd A, B;
    }

    /// <summary>A snag record's kind (tools/blender/lib/meta.py SNAG_KINDS).</summary>
    public enum SnagKind : byte
    {
        /// <summary>A polyline the fiber bends over.</summary>
        Edge = 0,
        /// <summary>A free-ended member, from its base (the first point) to its tip (the last).</summary>
        Hook = 1,
    }

    /// <summary>
    /// One segment of a placed snag record near a point (IWorldQuery.SnagsNear; p7d-tether design 12, the fortifications
    /// reading §1 and §7): the record's fields and the segment's two points in world space.
    /// </summary>
    public struct SnagSegment
    {
        /// <summary>The placed object (instance).</summary>
        public int Object;
        /// <summary>The record's index among its variant's snag records, in their order.</summary>
        public int Record;
        /// <summary>The segment's index in its record: from point Segment to point Segment + 1.</summary>
        public int Segment;
        public SnagKind Kind;
        /// <summary>The record's radius_m: the member's reach (props, legs and the fiber touch it within this), m.</summary>
        public double Radius;
        /// <summary>The arris the fiber bends over: the record's edge_radius_m, else its radius_m, m.</summary>
        public double EdgeRadius;
        /// <summary>The record's barb_pitch_m (an edge's barb clusters' spacing), m; 0 for a smooth member.</summary>
        public double BarbPitch;
        /// <summary>The record's material id (the member it follows).</summary>
        public ushort Material;
        /// <summary>The segment's ends in world space, m: the record's points Segment and Segment + 1.</summary>
        public Double3 A, B;
    }

    /// <summary>A ground feature an asset's shapes belong to (IWorldQuery.Feature): a pitfall of its metadata's features records.</summary>
    public struct GroundFeature
    {
        /// <summary>The record's id, unique within its asset.</summary>
        public int Id;
        /// <summary>The pit's centre in world space, m (the record's, placed at its instance's pose).</summary>
        public Double3 Center;
        /// <summary>m.</summary>
        public double Radius;
        /// <summary>Below the ground around it, m.</summary>
        public double Depth;
    }
}
