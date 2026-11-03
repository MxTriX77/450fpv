using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// A compliant layer under a part (p7a-ground design 6, 1.12): a straw mat, a mattress, a sandbag's fill, each a material
    /// row with an area law. The row's stiffness_n_per_m is the reference part's, the 15 mm foot's, at its
    /// area_reference_depth_m; the part that touches meets it scaled by its own footprint, by the row's law:
    /// - mat, a loose layer spreading the load 2:1 through its depth D (K§7.1 step 4): k = E_m·A_s/D, A_s the spread area at
    ///   D (a disc foot of diameter b: π((b + D)/2)²; a footprint of length ℓ and width w: (ℓ + D)(w + D), the 2:1 method's
    ///   rectangle), E_m = k_ref·D_ref/A_s(the reference foot at D_ref) (derived);
    /// - punch, an elastic flat punch (Sneddon 1965, published form): k in proportion to the footprint's equivalent
    ///   diameter √(4A/π);
    /// - winkler, a loose fill: k in proportion to the footprint's area A.
    /// A sphere's footprint is the disc of its diameter; a capsule's is its core's length on the face (Pressed) times its
    /// diameter. The damping keeps the row's ratio, so it scales as √k. The layer compresses at most StrokeShare of
    /// its thickness along the contact normal (Thickness), past which it is solid and carries the part onto what lies
    /// beneath it (ContactModel).
    /// </summary>
    public static class Layer
    {
        /// <summary>The reference part's diameter, m: the 15 mm foot every row's stiffness is stated for (K§7.2's reference
        /// foot; physics_materials.json's fields.stiffness_n_per_m).</summary>
        public const double ReferenceFoot = 0.015;

        /// <summary>The share of a layer's thickness it compresses before it is solid (design 6: K§7.1's mat constant,
        /// assumed for every layer).</summary>
        public const double StrokeShare = 0.8;

        /// <summary>How far under a layer's bottom face a soil or rigid shape of the layer's own object may lie and still carry
        /// the layer at its stroke, m (design 6: within the residual 20 % plus 5 mm of the part, assumed).</summary>
        public const double CarryReach = 0.005;

        const double Pi = 3.141592653589793;

        /// <summary>
        /// The factor on a row's stiffness for a part's footprint by the row's law: a disc of diameter width when disc, else
        /// length × width; depth is the layer's depth (Depth, the mat law's D; a layer without one, a plane, reads the row's
        /// referenceDepth). 1 without a law.
        /// </summary>
        public static double Scale(AreaLaw law, double referenceDepth, double depth, bool disc, double length, double width)
        {
            if (law == AreaLaw.Mat)
            {
                double d = depth > 0 && depth < double.PositiveInfinity ? depth : referenceDepth;
                double b = ReferenceFoot + referenceDepth, reference = 0.25 * Pi * b * b / referenceDepth;
                double spread = disc ? 0.25 * Pi * (width + d) * (width + d) : (length + d) * (width + d);
                return spread / d / reference;
            }
            if (law == AreaLaw.Punch) return DetMath.Sqrt(4 * Area(disc, length, width) / Pi) / ReferenceFoot;
            if (law == AreaLaw.Winkler) return Area(disc, length, width) / (0.25 * Pi * ReferenceFoot * ReferenceFoot);
            return 1;
        }

        static double Area(bool disc, double length, double width) => disc ? 0.25 * Pi * width * width : length * width;

        /// <summary>A layer's depth, m: its shape's least thickness (a mat tile's height, whichever face a part meets: a part
        /// pressing a mat's cut edge meets the mat, not a layer as deep as the tile is wide); +∞ for a plane.</summary>
        public static double Depth(in ShapeGeometry g)
        {
            switch (g.Kind)
            {
                case ShapeKind.Box:
                    return 2 * DetMath.Min(g.HalfExtents.X, DetMath.Min(g.HalfExtents.Y, g.HalfExtents.Z));
                case ShapeKind.Cylinder:
                    return 2 * DetMath.Min(g.Radius, g.HalfLength);
                case ShapeKind.Plane:
                    return double.PositiveInfinity;
                default:
                    return 2 * g.Radius; // a sphere, a capsule, a wire's tube
            }
        }

        /// <summary>A shape's thickness along the unit n, m: the distance between its two supporting planes normal to n (a
        /// box's caliper width), what a layer compresses along a contact's normal; +∞ for a plane, which has no bottom.</summary>
        public static double Thickness(in ShapeGeometry g, Double3 n)
        {
            switch (g.Kind)
            {
                case ShapeKind.Box:
                    return 2 * (g.HalfExtents.X * DetMath.Abs(Double3.Dot(n, g.AxisX)) + g.HalfExtents.Y * DetMath.Abs(Double3.Dot(n, g.AxisY))
                        + g.HalfExtents.Z * DetMath.Abs(Double3.Dot(n, g.AxisZ)));
                case ShapeKind.Capsule:
                    return 2 * (g.Radius + g.HalfLength * DetMath.Abs(Double3.Dot(n, g.AxisY)));
                case ShapeKind.Cylinder:
                {
                    double along = Double3.Dot(n, g.AxisY), across = 1 - along * along;
                    return 2 * (g.HalfLength * DetMath.Abs(along) + g.Radius * DetMath.Sqrt(across > 0 ? across : 0));
                }
                case ShapeKind.Plane:
                    return double.PositiveInfinity;
                default:
                    return 2 * g.Radius; // a sphere, a wire's tube
            }
        }

        /// <summary>
        /// A capsule's length on a face with unit normal n, m: its core a–b projected on the face (design 6's capsule lying
        /// along the face), at least πr/2, the length whose rectangle has its end cap's area (a capsule standing on its end).
        /// The whole projected length from the first touch, not the share pressed below the face (assumed: a part tilted
        /// into a layer meets it as one lying along it).
        /// </summary>
        public static double Pressed(Double3 a, Double3 b, double r, Double3 n)
        {
            Double3 core = b - a;
            double along = (core - n * Double3.Dot(n, core)).Length(), cap = 0.5 * Pi * r;
            return along > cap ? along : cap;
        }
    }
}
