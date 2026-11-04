// MapWorld's micro elements and ground features (p7a-ground task 1.9, design 4 and 8; WLD-11, AST-10).
// - The elements: each variant's micro_elements sidecar (asset-pipeline; p7a-ground 1.5), the very straws, stems, twigs
//   and leaves its mesh draws, read once however often the variant is placed. Its text (CR LF read as LF) must hash to
//   the SHA-256 the metadata names, and its records count by kind as the metadata says, or the load fails: the content
//   hash covers the metadata's text, so it covers every element. A record's centre line is two or three points in the
//   variant's frame, base first.
// - The index: per variant, a uniform 3-D grid of ElementCell cells over its elements' bounds (each element's segments
//   grown by half its diameter), each cell listing the elements whose bounds meet it. One grid serves every placement of
//   the variant: a query takes the sphere into the placement's frame, marks the elements listed in the cells its box
//   meets in a bit set (an element in many cells counts once) and reads the set in index order. Placed in world space,
//   the test range's 46 copies of S04 would hold 482 k elements (about 70 MB); per variant S04's are 10 486 (1.6 MB).
// - The placements: every instance whose variant has elements, in object order, with its elements' world bounds. A query
//   checks each one's bounds (46 on the range), so results come in canonical order (object, then element) unsorted.
// - The test: an element is returned when its centre line (one or two segments), grown by half its diameter (the drawn
//   tube's base; it tapers toward its tip, so this is conservative), meets the sphere: the distance from the centre to a
//   segment's nearest point is at most radius + diameter / 2, with the points placed in world space first, so the test is
//   of the points returned. + − × ÷ only; the bit set is on the stack, so a query allocates nothing and shares nothing.
// - The pose: world = position + R·local, R the row's yaw (its quaternion's matrix, R·x̂ and R·ẑ taken by Quat.Rotate as
//   MapWorldLoad takes the shapes' axes). A placement's frame is R's transpose about the position.
// - Nothing rooted in a hole: an element growing from no parent (a ground element: a patch's straw, stem or twig) whose
//   base lies inside another instance's hole band (a shaft, a trench's run: no ground there, IWorldQuery) is left out of
//   that placement at load, so a patch laid over a trench never offers straws standing on air. A band of the element's
//   own instance is its own floor (a trench's floor straws lie in its run), so it keeps them.
// - The ids: (object << 32) | element index, the instance and its record's index in the sidecar (bake order).
// - Features (design 4): a variant's optional "features" records, each {"kind": "pitfall", "id": a non-negative integer
//   unique within the asset, "center": [x, y, z], "radius_m", "depth_m", "shapes": [its collision shapes' indices]}.
//   Feature(object, shape) names the record a placed shape belongs to, its centre placed in world space; a shape belongs
//   to at most one feature.
// Load time allocates and throws FormatException naming the file; queries are allocation-free and reentrant.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>The side of a variant's element grid cells, m.</summary>
        public const double ElementCell = 1.0;

        /// <summary>The most elements a variant may hold: a query's bit set of them lives on the stack (32 KB).</summary>
        private const int MaxVariantElements = 1 << 18;

        /// <summary>How far a query's box grows in a placement's frame, m: past the rounding of taking the sphere there.</summary>
        private const double FramePad = 1e-6;

        /// <summary>meta.py's ELEMENT_KINDS: kind i is MicroKind 1 &lt;&lt; i.</summary>
        private static readonly string[] MicroKindNames = { "straw", "stem", "twig", "leaf" };

        /// <summary>One element in its variant's frame.</summary>
        private struct Element
        {
            public Double3 P0, P1, P2;
            public Bounds Box; // its segments grown by Radius
            public double Radius, Diameter, Area;
            public int Parent, ParentShape;
            public MicroKind Kind;
            public bool Anchored;
            public byte PointCount;
        }

        private struct FeatureRecord
        {
            public int Id;
            public Double3 Center;
            public double Radius, Depth;
        }

        /// <summary>A variant's elements with their 3-D grid, and its ground features, in its own frame.</summary>
        private sealed class Micro
        {
            public Element[] Elements = Array.Empty<Element>();
            public Bounds Box = Bounds.Empty;
            public Double3 GridMin;
            public int Nx, Ny, Nz;
            public int[] CellStart, CellItems; // per cell ((z·Ny + y)·Nx + x): its elements, ascending
            public FeatureRecord[] Features;
            public int[] FeatureOf;            // per collision shape: its feature's index, or −1; null without features

            public int Words => (Elements.Length + 63) >> 6;
        }

        /// <summary>A row's pose: world = Position + R·local, R·x̂ = (Xx, 0, Xz), R·ŷ = ŷ, R·ẑ = (Zx, 0, Zz).</summary>
        private struct Pose
        {
            public Double3 Position;
            public double Xx, Xz, Zx, Zz;

            public readonly Double3 Place(Double3 v) =>
                new Double3(Position.X + (v.X * Xx + v.Z * Zx), Position.Y + v.Y, Position.Z + (v.X * Xz + v.Z * Zz));

            public readonly Double3 Local(Double3 w)
            {
                double dx = w.X - Position.X, dz = w.Z - Position.Z;
                return new Double3(dx * Xx + dz * Xz, w.Y - Position.Y, dx * Zx + dz * Zz);
            }
        }

        // Set at load only.
        private Micro[] _rowMicro;       // per placed object: its variant's, or null
        private Pose[] _rowPose;         // per placed object
        private int[] _microObjects;     // the placed objects with elements, ascending
        private Bounds[] _microBox;      // their elements' world bounds
        private ulong[][] _microDropped; // their elements rooted in another instance's hole band, a bit each; or null
        private int _microWords;         // the largest variant's bit set, 64-bit words

        /// <summary>The lowest set bit's index of a power of two p is Lowest[(p · DeBruijn) &gt;&gt; 58].</summary>
        private const ulong DeBruijn = 0x03F79D71B4CB0A89UL;

        private static readonly byte[] Lowest = LowestTable();

        private static byte[] LowestTable()
        {
            var table = new byte[64];
            for (int b = 0; b < 64; b++)
                table[((1UL << b) * DeBruijn) >> 58] = (byte)b;
            return table;
        }

        // ---- MicroDetailNear and Feature ---------------------------------------------------------------------------

        public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results)
        {
            Bounds reach = Bounds.Of(centre, centre, radius);
            Span<ulong> marks = stackalloc ulong[_microWords];
            int count = 0;
            for (int k = 0; k < _microObjects.Length; k++)
            {
                if (!_microBox[k].Overlaps(reach))
                    continue;
                int obj = _microObjects[k];
                Micro m = _rowMicro[obj];
                Pose pose = _rowPose[obj];
                Double3 local = pose.Local(centre);
                Bounds near = Bounds.Of(local, local, radius + FramePad);
                if (!m.Box.Overlaps(near))
                    continue;
                Span<ulong> marked = marks.Slice(0, m.Words);
                marked.Clear();
                int x0 = Cell(near.Min.X, m.GridMin.X, m.Nx), x1 = Cell(near.Max.X, m.GridMin.X, m.Nx);
                int y0 = Cell(near.Min.Y, m.GridMin.Y, m.Ny), y1 = Cell(near.Max.Y, m.GridMin.Y, m.Ny);
                int z0 = Cell(near.Min.Z, m.GridMin.Z, m.Nz), z1 = Cell(near.Max.Z, m.GridMin.Z, m.Nz);
                for (int cz = z0; cz <= z1; cz++)
                    for (int cy = y0; cy <= y1; cy++)
                        for (int cx = x0; cx <= x1; cx++)
                        {
                            int cell = (cz * m.Ny + cy) * m.Nx + cx;
                            for (int j = m.CellStart[cell]; j < m.CellStart[cell + 1]; j++)
                            {
                                int e = m.CellItems[j];
                                marked[e >> 6] |= 1UL << (e & 63);
                            }
                        }
                ulong[] dropped = _microDropped[k];
                Element[] elements = m.Elements;
                for (int w = 0; w < marked.Length; w++)
                {
                    ulong bits = dropped == null ? marked[w] : marked[w] & ~dropped[w];
                    while (bits != 0)
                    {
                        ulong low = bits & (0UL - bits);
                        bits ^= low;
                        int i = (w << 6) | Lowest[(low * DeBruijn) >> 58];
                        ref readonly Element e = ref elements[i];
                        if ((e.Kind & kinds) == 0 || !e.Box.Overlaps(near))
                            continue;
                        Double3 p0 = pose.Place(e.P0), p1 = pose.Place(e.P1), p2 = e.PointCount == 3 ? pose.Place(e.P2) : p1;
                        double r = radius + e.Radius, rr = r * r;
                        if (!(SegmentDistanceSquared(centre, p0, p1) <= rr || SegmentDistanceSquared(centre, p1, p2) <= rr))
                            continue;
                        if (count < results.Length)
                        {
                            results[count] = new MicroElement
                            {
                                Id = (ulong)(uint)obj << 32 | (uint)i,
                                Kind = e.Kind,
                                Anchored = e.Anchored,
                                PointCount = e.PointCount,
                                P0 = p0,
                                P1 = p1,
                                P2 = p2,
                                Diameter = e.Diameter,
                                Area = e.Area,
                                Parent = e.Parent,
                                ParentShape = e.ParentShape,
                            };
                        }
                        count++;
                    }
                }
            }
            return count;
        }

        public bool Feature(int obj, int shape, out GroundFeature feature)
        {
            feature = default;
            if (obj < 0 || obj >= _names.Length || shape < 0)
                return false;
            Micro m = _rowMicro[obj];
            if (m == null || m.FeatureOf == null || shape >= m.FeatureOf.Length || m.FeatureOf[shape] < 0)
                return false;
            FeatureRecord r = m.Features[m.FeatureOf[shape]];
            feature = new GroundFeature { Id = r.Id, Center = _rowPose[obj].Place(r.Center), Radius = r.Radius, Depth = r.Depth };
            return true;
        }

        /// <summary>The squared distance from c to the segment a–b (a point when a = b). On scalars, not Double3 temporaries
        /// (a tenth of a 2 m query's time in S04).</summary>
        private static double SegmentDistanceSquared(in Double3 c, in Double3 a, in Double3 b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z, acx = c.X - a.X, acy = c.Y - a.Y, acz = c.Z - a.Z;
            double dd = abx * abx + aby * aby + abz * abz;
            double t = dd > 0 ? (acx * abx + acy * aby + acz * abz) / dd : 0;
            t = t > 0 ? t < 1 ? t : 1 : 0;
            double ex = acx - abx * t, ey = acy - aby * t, ez = acz - abz * t;
            return ex * ex + ey * ey + ez * ez;
        }

        /// <summary>The grid cell of v along one axis: monotone, so a box's cells hold its points'; outside, the edge cell; NaN 0.</summary>
        private static int Cell(double v, double min, int n)
        {
            double f = (v - min) * (1 / ElementCell);
            return f >= n - 1 ? n - 1 : f >= 0 ? (int)f : 0;
        }

        // ---- Load --------------------------------------------------------------------------------------------------

        /// <summary>A variant's elements (its micro_elements sidecar, checked) and ground features; null when it has neither.</summary>
        private static Micro ParseMicro(string path, JsonNode root, int shapes, Func<string, string> readModel)
        {
            JsonNode record = root.Member("micro_elements"), features = root.Member("features");
            if (record == null && features == null)
                return null;
            var micro = new Micro();
            if (record != null)
            {
                micro.Elements = ParseElements(path, Text(root, "variant", path), record, shapes, readModel);
                BuildGrid(micro);
            }
            if (features != null)
                ParseFeatures(path, features, shapes, micro);
            return micro;
        }

        private static Element[] ParseElements(string path, string variant, JsonNode record, int shapes, Func<string, string> readModel)
        {
            string where = path + " micro_elements";
            if (record.Kind != JsonKind.Object)
                throw new FormatException(where + ": not an object");
            string file = Text(record, "file", where), sha = Text(record, "sha256", where);
            if (file != variant + ".elements.json")
                throw new FormatException(where + ": file must be " + variant + ".elements.json, beside the metadata");
            string sidecar = path.Substring(0, path.LastIndexOf('/') + 1) + file;
            string text = readModel(sidecar);
            if (text == null)
                throw new FormatException(sidecar + ": not found");
            string actual = Sha256(text);
            if (actual != sha)
                throw new FormatException(sidecar + ": its SHA-256 is " + actual + ", not the metadata's " + sha);

            JsonNode root = DetJson.Parse(text);
            if (Number(root, "schema", sidecar) != 1 || Text(root, "variant", sidecar) != variant || Text(root, "units", sidecar) != "m"
                || Text(root, "frame", sidecar) != "unity-lh-yup")
                throw new FormatException(sidecar + ": not schema 1 of " + variant + ", in m and unity-lh-yup");
            List<JsonNode> items = Need(root, "elements", JsonKind.Array, sidecar).Items;
            if (items.Count > MaxVariantElements)
                throw new FormatException(sidecar + $": {items.Count} elements, more than a variant may hold ({MaxVariantElements})");
            var elements = new Element[items.Count];
            var counts = new int[MicroKindNames.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                JsonNode e = items[i];
                string at = sidecar + " element " + i;
                int kind = Array.IndexOf(MicroKindNames, Text(e, "kind", at));
                if (kind < 0)
                    throw new FormatException(at + ": kind is straw, stem, twig or leaf");
                counts[kind]++;
                List<JsonNode> points = Need(e, "points", JsonKind.Array, at).Items;
                if (points.Count < 2 || points.Count > 3)
                    throw new FormatException(at + ": points are two or three [x, y, z]");
                var el = new Element
                {
                    Kind = (MicroKind)(1 << kind),
                    PointCount = (byte)points.Count,
                    P0 = Vector(points[0], at),
                    P1 = Vector(points[1], at),
                    Diameter = Number(e, "diameter_m", at),
                    Anchored = Need(e, "anchored", JsonKind.Bool, at).Bool,
                    Parent = IndexOrNone(e, "parent", i, at),
                    ParentShape = IndexOrNone(e, "parent_shape", shapes, at),
                };
                el.P2 = points.Count == 3 ? Vector(points[2], at) : el.P1;
                if (!(el.Diameter > 0))
                    throw new FormatException(at + ": diameter_m must be above 0");
                el.Radius = 0.5 * el.Diameter;
                if (el.Kind == MicroKind.Leaf && !((el.Area = Number(e, "area_m2", at)) > 0))
                    throw new FormatException(at + ": a leaf's area_m2 must be above 0");
                el.Box = Bounds.Of(el.P0, el.P1, el.Radius).Union(Bounds.Of(el.P1, el.P2, el.Radius));
                elements[i] = el;
            }
            JsonNode named = Need(record, "counts", JsonKind.Object, where);
            foreach (string key in named.Keys)
                if (Array.IndexOf(MicroKindNames, key) < 0)
                    throw new FormatException(where + ": counts names an unknown kind '" + key + "'");
            for (int k = 0; k < counts.Length; k++)
            {
                JsonNode n = named.Member(MicroKindNames[k]);
                if ((n == null ? 0 : Number(n, where + " counts")) != counts[k])
                    throw new FormatException(where + ": counts say " + (n == null ? 0 : n.Number) + " " + MicroKindNames[k] + ", the sidecar holds " + counts[k]);
            }
            return elements;
        }

        /// <summary>An integer in [−1, limit): an index, or −1 for none.</summary>
        private static int IndexOrNone(JsonNode o, string key, int limit, string where)
        {
            double v = Number(o, key, where);
            if (!(v >= -1 && v < limit && v == Math.Floor(v)))
                throw new FormatException(where + ": " + key + " must be −1 or an index below " + limit);
            return (int)v;
        }

        /// <summary>The SHA-256 of a text's UTF-8 bytes, CR LF read as LF (the sidecar is written with LF), in lower-case hex.</summary>
        private static string Sha256(string text)
        {
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")));
            var hex = new StringBuilder(2 * digest.Length);
            foreach (byte b in digest)
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }

        /// <summary>The variant's grid: ElementCell cells over its elements' bounds, each listing the elements whose bounds meet it.</summary>
        private static void BuildGrid(Micro m)
        {
            if (m.Elements.Length == 0)
                return;
            Bounds box = m.Elements[0].Box;
            for (int i = 1; i < m.Elements.Length; i++)
                box = box.Union(m.Elements[i].Box);
            m.Box = box;
            m.GridMin = box.Min;
            m.Nx = 1 + (int)((box.Max.X - box.Min.X) * (1 / ElementCell));
            m.Ny = 1 + (int)((box.Max.Y - box.Min.Y) * (1 / ElementCell));
            m.Nz = 1 + (int)((box.Max.Z - box.Min.Z) * (1 / ElementCell));
            m.CellStart = new int[m.Nx * m.Ny * m.Nz + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                var fill = pass == 0 ? null : new int[m.CellStart.Length - 1];
                for (int i = 0; i < m.Elements.Length; i++)
                {
                    Bounds b = m.Elements[i].Box;
                    for (int cz = Cell(b.Min.Z, box.Min.Z, m.Nz); cz <= Cell(b.Max.Z, box.Min.Z, m.Nz); cz++)
                        for (int cy = Cell(b.Min.Y, box.Min.Y, m.Ny); cy <= Cell(b.Max.Y, box.Min.Y, m.Ny); cy++)
                            for (int cx = Cell(b.Min.X, box.Min.X, m.Nx); cx <= Cell(b.Max.X, box.Min.X, m.Nx); cx++)
                            {
                                int cell = (cz * m.Ny + cy) * m.Nx + cx;
                                if (pass == 0)
                                    m.CellStart[cell + 1]++;
                                else
                                    m.CellItems[m.CellStart[cell] + fill[cell]++] = i;
                            }
                }
                if (pass == 0)
                {
                    for (int c = 1; c < m.CellStart.Length; c++)
                        m.CellStart[c] += m.CellStart[c - 1];
                    m.CellItems = new int[m.CellStart[m.CellStart.Length - 1]];
                }
            }
        }

        private static void ParseFeatures(string path, JsonNode features, int shapes, Micro micro)
        {
            string where = path + " features";
            if (features.Kind != JsonKind.Array)
                throw new FormatException(where + ": not an array");
            micro.Features = new FeatureRecord[features.Items.Count];
            micro.FeatureOf = new int[shapes];
            for (int s = 0; s < shapes; s++)
                micro.FeatureOf[s] = -1;
            for (int f = 0; f < micro.Features.Length; f++)
            {
                JsonNode r = features.Items[f];
                string at = where + " " + f;
                if (Text(r, "kind", at) != "pitfall")
                    throw new FormatException(at + ": kind must be pitfall");
                double id = Number(r, "id", at);
                if (!(id >= 0 && id <= int.MaxValue && id == Math.Floor(id)))
                    throw new FormatException(at + ": id must be a non-negative integer");
                for (int g = 0; g < f; g++)
                    if (micro.Features[g].Id == (int)id)
                        throw new FormatException(at + ": id " + id + " is feature " + g + "'s too");
                micro.Features[f] = new FeatureRecord
                {
                    Id = (int)id, Center = Vector(r, "center", at), Radius = Number(r, "radius_m", at), Depth = Number(r, "depth_m", at),
                };
                if (!(micro.Features[f].Radius > 0 && micro.Features[f].Depth > 0))
                    throw new FormatException(at + ": radius_m and depth_m must be above 0");
                List<JsonNode> members = Need(r, "shapes", JsonKind.Array, at).Items;
                if (members.Count == 0)
                    throw new FormatException(at + ": a feature names its shapes");
                foreach (JsonNode n in members)
                {
                    double s = Number(n, at + " shapes");
                    if (!(s >= 0 && s < shapes && s == Math.Floor(s)))
                        throw new FormatException(at + ": shape " + s + " is not a collision shape of the asset");
                    if (micro.FeatureOf[(int)s] >= 0)
                        throw new FormatException(at + ": shape " + s + " belongs to feature " + micro.FeatureOf[(int)s] + " too");
                    micro.FeatureOf[(int)s] = f;
                }
            }
        }

        /// <summary>
        /// Each row's pose and variant's micro data; the instances with elements, their world bounds and their elements
        /// rooted in another instance's hole band. After the gaps' cells are listed.
        /// </summary>
        private void PlaceMicro(Meta[] rowMeta, Double3[] rowPosition, Quat[] rowYaw)
        {
            _rowMicro = new Micro[rowMeta.Length];
            _rowPose = new Pose[rowMeta.Length];
            var objects = new List<int>();
            var boxes = new List<Bounds>();
            var dropped = new List<ulong[]>();
            for (int i = 0; i < rowMeta.Length; i++)
            {
                Double3 x = rowYaw[i].Rotate(new Double3(1, 0, 0)), z = rowYaw[i].Rotate(new Double3(0, 0, 1));
                _rowPose[i] = new Pose { Position = rowPosition[i], Xx = x.X, Xz = x.Z, Zx = z.X, Zz = z.Z };
                Micro m = _rowMicro[i] = rowMeta[i].Micro;
                if (m == null || m.Elements.Length == 0)
                    continue;
                Bounds world = Bounds.Empty;
                for (int c = 0; c < 8; c++)
                {
                    Double3 corner = _rowPose[i].Place(new Double3((c & 1) == 0 ? m.Box.Min.X : m.Box.Max.X,
                        (c & 2) == 0 ? m.Box.Min.Y : m.Box.Max.Y, (c & 4) == 0 ? m.Box.Min.Z : m.Box.Max.Z));
                    world = world.Union(Bounds.Of(corner, corner, FramePad));
                }
                ulong[] mask = null;
                for (int e = 0; e < m.Elements.Length; e++)
                {
                    ref readonly Element el = ref m.Elements[e];
                    if (el.Parent >= 0 || el.ParentShape >= 0)
                        continue;
                    Double3 b = _rowPose[i].Place(el.P0);
                    if (!InBandOfAnother(b.X, b.Z, i))
                        continue;
                    mask = mask ?? new ulong[m.Words];
                    mask[e >> 6] |= 1UL << (e & 63);
                }
                objects.Add(i);
                boxes.Add(world);
                dropped.Add(mask);
                _microWords = Math.Max(_microWords, m.Words);
            }
            _microObjects = objects.ToArray();
            _microBox = boxes.ToArray();
            _microDropped = dropped.ToArray();
        }

        /// <summary>Whether (x, z) lies inside a hole band (not a patch footprint) of an instance other than `obj`.</summary>
        private bool InBandOfAnother(double x, double z, int obj)
        {
            int cell = CellZ(z) * _nx + CellX(x);
            for (int k = _gapStart[cell]; k < _gapStart[cell + 1]; k++)
            {
                ref readonly Gap g = ref _gaps[_gapItems[k]];
                if (g.BandCount > 0 && g.Instance != obj && InGapBox(g, x, z) && InBand(g, x, z))
                    return true;
            }
            return false;
        }
    }
}
