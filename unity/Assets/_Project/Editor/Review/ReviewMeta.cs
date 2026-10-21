using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// What the review sheet draws and lists from an asset's <c>.assetmeta.json</c> (schema 1, setup-asset-pipeline
    /// design decision 6), read from the file itself rather than from the prefab's colliders (design decision 5): the
    /// collision primitives (a compliant shell's pieces in a colour of their own), gaps, wind volumes and snag features
    /// as line segments in their colours, plus the summary lines of the legend.
    /// </summary>
    sealed class ReviewMeta
    {
        public static readonly Color32 CollisionColor = new Color32(0, 255, 0, 255);
        public static readonly Color32 GapColor = new Color32(255, 0, 255, 255);
        public static readonly Color32 WindColor = new Color32(0, 255, 255, 255);
        public static readonly Color32 SnagColor = new Color32(255, 0, 0, 255);
        /// <summary>
        /// A compliant shell's pieces, apart from the rigid primitives (p1-v13-shrub QA N-3, p1-v01-locust QA 2.1 S6):
        /// V01_L's 147 pieces over its 364 capsules could not be read in the capsules' green.
        /// </summary>
        public static readonly Color32 ShellColor = new Color32(255, 220, 0, 255);
        /// <summary>The physics materials of a compliant shell's pieces, as <c>lib/audit.py</c>'s SHELL_MATERIALS.</summary>
        public static readonly string[] ShellMaterials = { "twig", "foliage" };

        public sealed class Shape
        {
            public string kind;
            public string material;
            public Vector3 center;
            public Vector3 size;
            public float radius;
            public float height;
            public Quaternion rotation = Quaternion.identity;
        }

        public sealed class Gap
        {
            public Vector3[] polygon;
            public Vector3 normal;
            public float narrowest;
        }

        public sealed class Snag
        {
            public Vector3[] points;
            public float radius;
        }

        public struct Segment
        {
            public Vector3 A;
            public Vector3 B;
            public Color32 Color;
        }

        public Bounds Bounds;
        /// <summary>The metadata's optional <c>bounds.lod</c>: the object's own box, without what lies round it (a car's scatter, a house's rubble spread); null when absent.</summary>
        public Bounds? Lod;
        /// <summary>
        /// The box the review frames its shots on: <see cref="Lod"/> when the metadata carries it, else the bounds
        /// (p1-r01-vaz-sedan QA 2.2's T1, p1-h01-adobe-house task 1.6), as the LOD group is framed.
        /// </summary>
        public Bounds Frame => Lod ?? Bounds;
        public string LodSummary;
        public float CullDistance;
        public string MaterialSummary;
        public readonly List<Shape> Collision = new List<Shape>();
        public readonly List<Gap> Gaps = new List<Gap>();
        public readonly List<Shape> WindVolumes = new List<Shape>();
        public readonly List<Snag> Snags = new List<Snag>();

        public static ReviewMeta Read(string path)
        {
            var json = Json.Obj(Json.Parse(File.ReadAllText(path)), path);
            if ((int)Json.Num(json, "schema", path) != 1)
                throw new System.NotSupportedException($"{path}: schema {json["schema"]}; the review sheet reads schema 1");
            var meta = new ReviewMeta();

            var bounds = Json.Obj(json, "bounds", path);
            Vector3 min = Json.Vec3(bounds, "min", path), max = Json.Vec3(bounds, "max", path);
            meta.Bounds = new Bounds((min + max) * 0.5f, max - min);
            if (bounds.ContainsKey("lod"))
            {
                var lod = Json.Obj(bounds, "lod", path);
                Vector3 lodMin = Json.Vec3(lod, "min", path), lodMax = Json.Vec3(lod, "max", path);
                meta.Lod = new Bounds((lodMin + lodMax) * 0.5f, lodMax - lodMin);
            }

            meta.LodSummary = string.Join(", ", Items(json, "lods", path).Select((lod, i) =>
                $"{Json.Str(lod, "mesh", path).Replace($"{Json.Str(json, "variant", path)}_", "")} {Json.Num(lod, "tris", path):0} TRIS" +
                (i == 0 ? "" : $" @ {Json.Num(lod, "switch_distance_m", path):0.#} M")));
            meta.CullDistance = Json.Num(json, "cull_distance_m", path);
            meta.MaterialSummary = string.Join(", ", Items(json, "materials", path).Select(slot =>
                $"{Json.Str(slot, "slot", path)} > {Json.Str(slot, "visual", path)} / {Json.Str(slot, "physics", path)}"));

            foreach (var shape in Items(json, "collision", path))
                meta.Collision.Add(ReadShape(shape, path));
            foreach (var gap in Items(json, "gaps", path))
                meta.Gaps.Add(new Gap
                {
                    polygon = Json.Arr(gap, "polygon", path).Select(p => Json.Vec3(p, $"{path} polygon")).ToArray(),
                    normal = Json.Vec3(gap, "normal", path),
                    narrowest = Json.Num(gap, "narrowest_m", path),
                });
            foreach (var shape in Items(json, "wind_volumes", path))
                meta.WindVolumes.Add(ReadShape(shape, path));
            foreach (var feature in Items(json, "snag", path))
                meta.Snags.Add(new Snag
                {
                    points = Json.Arr(feature, "points", path).Select(p => Json.Vec3(p, $"{path} points")).ToArray(),
                    radius = Json.Num(feature, "radius_m", path),
                });
            return meta;
        }

        public static bool IsShell(Shape shape) => ShellMaterials.Contains(shape.material);

        static IEnumerable<Dictionary<string, object>> Items(Dictionary<string, object> json, string key, string what) =>
            Json.Arr(json, key, what).Select((item, i) => Json.Obj(item, $"{what} {key}[{i}]"));

        static Shape ReadShape(Dictionary<string, object> json, string what)
        {
            var shape = new Shape
            {
                kind = Json.Str(json, "shape", what), center = Json.Vec3(json, "center", what),
                material = json.TryGetValue("material", out object material) ? material as string : null,
            };
            switch (shape.kind)
            {
                case "box":
                    shape.size = Json.Vec3(json, "size", what);
                    shape.rotation = Json.Quat(json, "rotation", what);
                    break;
                case "sphere":
                    shape.radius = Json.Num(json, "radius", what);
                    break;
                case "capsule":
                case "cylinder":
                    shape.radius = Json.Num(json, "radius", what);
                    shape.height = Json.Num(json, "height", what);
                    shape.rotation = Json.Quat(json, "rotation", what);
                    break;
                default:
                    throw new System.FormatException($"{what}: unknown shape \"{shape.kind}\"");
            }
            return shape;
        }

        // --- the overlay lines, in world space ---------------------------------------------------------------------

        public List<Segment> Segments(Matrix4x4 localToWorld)
        {
            var segments = new List<Segment>();
            foreach (Shape shape in Collision)
                ShapeSegments(shape, localToWorld, IsShell(shape) ? ShellColor : CollisionColor, segments);
            foreach (Shape shape in WindVolumes)
                ShapeSegments(shape, localToWorld, WindColor, segments);
            foreach (Gap gap in Gaps)
            {
                Vector3[] p = gap.polygon.Select(localToWorld.MultiplyPoint3x4).ToArray();
                for (int i = 0; i < p.Length; i++)
                    Add(segments, p[i], p[(i + 1) % p.Length], GapColor);
                if (p.Length > 0)
                {
                    Vector3 centroid = p.Aggregate(Vector3.zero, (sum, v) => sum + v) / p.Length;
                    Vector3 normal = localToWorld.MultiplyVector(gap.normal).normalized;
                    Add(segments, centroid, centroid + normal * Mathf.Max(0.1f, gap.narrowest * 0.25f), GapColor);
                }
            }
            foreach (Snag snag in Snags)
            {
                Vector3[] p = snag.points.Select(localToWorld.MultiplyPoint3x4).ToArray();
                for (int i = 0; i + 1 < p.Length; i++)
                    Add(segments, p[i], p[i + 1], SnagColor);
                float r = Mathf.Max(snag.radius, 0.02f);
                foreach (Vector3 point in p)
                {
                    Add(segments, point - Vector3.right * r, point + Vector3.right * r, SnagColor);
                    Add(segments, point - Vector3.up * r, point + Vector3.up * r, SnagColor);
                    Add(segments, point - Vector3.forward * r, point + Vector3.forward * r, SnagColor);
                }
            }
            return segments;
        }

        static void ShapeSegments(Shape shape, Matrix4x4 localToWorld, Color32 color, List<Segment> into)
        {
            Matrix4x4 m = localToWorld * Matrix4x4.TRS(shape.center, shape.rotation, Vector3.one);
            Vector3 x = m.MultiplyVector(Vector3.right), y = m.MultiplyVector(Vector3.up), z = m.MultiplyVector(Vector3.forward);
            Vector3 c = m.MultiplyPoint3x4(Vector3.zero);
            switch (shape.kind)
            {
                case "box":
                {
                    Vector3 e = shape.size * 0.5f;
                    var corner = new Vector3[8];
                    for (int i = 0; i < 8; i++)
                        corner[i] = c + x * ((i & 1) == 0 ? -e.x : e.x) + y * ((i & 2) == 0 ? -e.y : e.y) + z * ((i & 4) == 0 ? -e.z : e.z);
                    for (int i = 0; i < 8; i++)
                        for (int bit = 1; bit < 8; bit <<= 1)
                            if ((i & bit) == 0)
                                Add(into, corner[i], corner[i | bit], color);
                    break;
                }
                case "sphere":
                    Circle(into, c, x, z, shape.radius, 0f, 360f, color);
                    Circle(into, c, x, y, shape.radius, 0f, 360f, color);
                    Circle(into, c, z, y, shape.radius, 0f, 360f, color);
                    break;
                case "capsule":
                case "cylinder":
                {
                    float r = shape.radius;
                    bool capsule = shape.kind == "capsule";
                    float half = capsule ? Mathf.Max(0f, shape.height * 0.5f - r) : shape.height * 0.5f;
                    Vector3 top = c + y * half, bottom = c - y * half;
                    Circle(into, top, x, z, r, 0f, 360f, color);
                    Circle(into, bottom, x, z, r, 0f, 360f, color);
                    foreach (Vector3 side in new[] { x, -x, z, -z })
                        Add(into, bottom + side * r, top + side * r, color);
                    if (capsule)
                    {
                        Circle(into, top, x, y, r, 0f, 180f, color);
                        Circle(into, top, z, y, r, 0f, 180f, color);
                        Circle(into, bottom, x, y, r, 180f, 360f, color);
                        Circle(into, bottom, z, y, r, 180f, 360f, color);
                    }
                    break;
                }
            }
        }

        /// <summary>An arc in the plane of two unit axes, from <paramref name="fromDeg"/> to <paramref name="toDeg"/> (0° along <paramref name="u"/>).</summary>
        static void Circle(List<Segment> into, Vector3 center, Vector3 u, Vector3 v, float radius, float fromDeg, float toDeg, Color32 color)
        {
            const int perTurn = 48;
            int steps = Mathf.Max(1, Mathf.RoundToInt((toDeg - fromDeg) / 360f * perTurn));
            Vector3 previous = Point(0);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 next = Point(i);
                Add(into, previous, next, color);
                previous = next;
            }

            Vector3 Point(int i)
            {
                float a = Mathf.Deg2Rad * (fromDeg + (toDeg - fromDeg) * i / steps);
                return center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
            }
        }

        static void Add(List<Segment> into, Vector3 a, Vector3 b, Color32 color) =>
            into.Add(new Segment { A = a, B = b, Color = color });
    }
}
