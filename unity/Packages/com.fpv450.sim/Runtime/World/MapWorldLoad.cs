// MapWorld's loader (p6-world-query-gallery task 1.4): a placement file in the form of
// p6-test-range design 7 ("fpv450-placements" v1, frame "unity-lh-yup") and the
// .assetmeta.json of each variant it places (asset-pipeline, frame "unity-lh-yup",
// metres) become world-space shapes, the ground's gaps, the 2 m grid and the content
// hash. The data is already in Unity's frame, so nothing is flipped (IWorldQuery, Frame).
// - A row's pose: world = position + R(yaw) · local, R(yaw) Unity's Quaternion.Euler(0,
//   yaw, 0) = (0, sin(yaw/2), 0, cos(yaw/2)); a shape's axes are R(yaw)·rotation applied
//   to its local axes (the rotation normalised first). Sines by DetMath.SinCos.
// - box size → half extents; capsule height (Unity's, end to end) → HalfLength = height/2
//   − radius (0 at least); cylinder height → HalfLength = height/2.
// - A wire's points in order make its chain, which must advance along its chord (end to
//   end) at every point; Geometry gives the chord: Center its midpoint, AxisY along it,
//   SpanA and SpanB its ends, Sag the record's, Radius half the diameter.
// - The ground record: the plane, each patch footprint ([xmin, zmin, xmax, zmax]) and each
//   band (centre line, band width); their cells are listed like the shapes'.
// Load time only: it allocates and throws FormatException naming the file and the field.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>Where the variants' metadata live, from the repository root (a row's class/asset/variant below it).</summary>
        public const string ModelsRoot = "unity/Assets/_Project/Art/Models";

        /// <summary>The physics material table, from the repository root.</summary>
        public const string MaterialsFile = "assets-src/physics_materials.json";

        private const double HalfDegree = 3.141592653589793 / 360;

        /// <summary>A variant's shapes in its own frame, read once however often it is placed.</summary>
        private sealed class Meta
        {
            public string Text;
            public ShapeGeometry[] Shapes; // local: Center, Radius, HalfLength, HalfExtents, ids; AxisX holds nothing
            public Quat[] Rotations;
            public Double3[][] WirePoints;
            public double[] WireRadius, WireSag;
            public ushort[] WireMaterial;
        }

        /// <summary>The map maps/&lt;mapId&gt;/placements.json of the repository at `root`, with its metadata and the material table.</summary>
        public static MapWorld Load(string root, string mapId)
        {
            string Read(string relative) => File.ReadAllText(Path.Combine(root, relative));
            return new MapWorld(Read("maps/" + mapId + "/placements.json"), model => Read(ModelsRoot + "/" + model),
                MaterialNames(Read(MaterialsFile)));
        }

        /// <summary>The names of physics_materials.json's rows in file order: material id i + 1 is name i.</summary>
        public static string[] MaterialNames(string physicsMaterialsJson)
        {
            JsonNode materials = DetJson.Parse(physicsMaterialsJson).Member("materials");
            if (materials == null || materials.Kind != JsonKind.Object)
                throw new FormatException(MaterialsFile + ": materials is missing or not an object");
            return materials.Keys.ToArray();
        }

        /// <summary>
        /// The world of a placement file. readModel returns the text of a variant's metadata by its path below
        /// ModelsRoot ("&lt;class&gt;/&lt;asset&gt;/&lt;variant&gt;.assetmeta.json"); materials are the material names, id i + 1 for
        /// name i (MaterialNames). Every material a shape or wire names must be among them.
        /// </summary>
        public MapWorld(string placementsJson, Func<string, string> readModel, string[] materials)
        {
            if (materials == null || materials.Length >= ushort.MaxValue)
                throw new ArgumentException("one to 65534 material names", nameof(materials));
            _materials = (string[])materials.Clone();
            var materialIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
            for (int i = 0; i < _materials.Length; i++)
            {
                if (string.IsNullOrEmpty(_materials[i]) || materialIds.ContainsKey(_materials[i]))
                    throw new ArgumentException($"material names must be non-empty and unique: '{_materials[i]}'", nameof(materials));
                materialIds.Add(_materials[i], (ushort)(i + 1));
            }

            const string file = "placements.json";
            JsonNode root = DetJson.Parse(placementsJson);
            if (Text(root, "format", file) != "fpv450-placements" || Number(root, "format_version", file) != 1)
                throw new FormatException(file + ": not an fpv450-placements file of format_version 1");
            if (Text(root, "frame", file) != "unity-lh-yup")
                throw new FormatException(file + ": frame must be unity-lh-yup");
            MapId = Text(root, "map", file);

            // The grid over the extent [xmin, zmin, xmax, zmax].
            JsonNode extent = Need(root, "extent_m", JsonKind.Array, file);
            if (extent.Items.Count != 4)
                throw new FormatException(file + ": extent_m is [xmin, zmin, xmax, zmax]");
            _gridX = Number(extent.Items[0], file + " extent_m");
            _gridZ = Number(extent.Items[1], file + " extent_m");
            double sizeX = Number(extent.Items[2], file + " extent_m") - _gridX, sizeZ = Number(extent.Items[3], file + " extent_m") - _gridZ;
            if (!(sizeX > 0 && sizeZ > 0 && sizeX <= 16384 && sizeZ <= 16384))
                throw new FormatException(file + ": extent_m must span 0 to 16 384 m each way");
            _nx = (int)(sizeX / CellSize) + (sizeX / CellSize > (int)(sizeX / CellSize) ? 1 : 0);
            _nz = (int)(sizeZ / CellSize) + (sizeZ / CellSize > (int)(sizeZ / CellSize) ? 1 : 0);

            // The instances, each variant's metadata read once.
            JsonNode instances = Need(root, "instances", JsonKind.Array, file);
            var metas = new Dictionary<string, Meta>(StringComparer.Ordinal);
            var rowMeta = new Meta[instances.Items.Count];
            var rowPosition = new Double3[rowMeta.Length];
            var rowYaw = new Quat[rowMeta.Length];
            _names = new string[rowMeta.Length];
            int shapeCount = 0, wirePointCount = 0;
            for (int i = 0; i < rowMeta.Length; i++)
            {
                JsonNode row = instances.Items[i];
                string where = file + " instance " + i;
                if (Number(row, "id", where) != i)
                    throw new FormatException(where + ": id must be its index");
                string variant = Text(row, "variant", where);
                string path = Text(row, "class", where) + "/" + Text(row, "asset", where) + "/" + variant + ".assetmeta.json";
                if (!metas.TryGetValue(path, out Meta meta))
                {
                    meta = ParseMeta(path, readModel(path), materialIds);
                    metas.Add(path, meta);
                }
                rowMeta[i] = meta;
                rowPosition[i] = Vector(row, "position_m", where);
                DetMath.SinCos(Number(row, "yaw_deg", where) * HalfDegree, out double sin, out double cos);
                rowYaw[i] = new Quat(0, sin, 0, cos);
                _names[i] = variant;
                shapeCount += meta.Shapes.Length + meta.WirePoints.Length;
                foreach (Double3[] points in meta.WirePoints)
                    wirePointCount += points.Length;
            }

            // Every shape and wire in world space, object by object.
            _prims = new Prim[shapeCount];
            _first = new int[rowMeta.Length + 1];
            _wirePoints = new Double3[wirePointCount];
            _wireAlong = new double[wirePointCount];
            _wireProj = new double[wirePointCount];
            int at = 0, wireAt = 0;
            for (int i = 0; i < rowMeta.Length; i++)
            {
                _first[i] = at;
                Meta meta = rowMeta[i];
                Double3 position = rowPosition[i];
                Quat yaw = rowYaw[i];
                for (int s = 0; s < meta.Shapes.Length; s++, at++)
                {
                    ShapeGeometry g = meta.Shapes[s];
                    Quat q = yaw * meta.Rotations[s];
                    g.Center = position + yaw.Rotate(g.Center);
                    g.AxisX = q.Rotate(new Double3(1, 0, 0));
                    g.AxisY = q.Rotate(new Double3(0, 1, 0));
                    g.AxisZ = q.Rotate(new Double3(0, 0, 1));
                    _prims[at] = new Prim { Geometry = g, Box = Bounds.OfShape(g), Object = i, Shape = s };
                }
                for (int w = 0; w < meta.WirePoints.Length; w++, at++)
                {
                    _prims[at] = PlaceWire(meta, w, position, yaw, wireAt, i, meta.Shapes.Length + w, file + " instance " + i);
                    wireAt += meta.WirePoints[w].Length;
                }
            }
            _first[rowMeta.Length] = at;

            // The ground: the plane, the patches' footprints and the hole bands.
            JsonNode ground = Need(root, "ground", JsonKind.Object, file);
            JsonNode plane = Need(ground, "plane", JsonKind.Object, file + " ground");
            byte surface = SurfaceId(Text(plane, "surface", file + " ground.plane"));
            if (surface == WorldIds.NoSurface)
                throw new FormatException(file + ": ground.plane.surface is not one of the thirteen surface ids");
            _ground = new ShapeGeometry
            {
                Kind = ShapeKind.Plane,
                Center = new Double3(0, Number(plane, "y_m", file + " ground.plane"), 0),
                AxisX = new Double3(1, 0, 0),
                AxisY = new Double3(0, 1, 0),
                AxisZ = new Double3(0, 0, 1),
                Material = WorldIds.NoMaterial,
                Surface = surface,
            };
            var gaps = new List<Gap>();
            var band = new List<XZ>();
            foreach (JsonNode patch in Need(ground, "patches", JsonKind.Array, file + " ground").Items)
            {
                JsonNode r = Need(patch, "footprint_m", JsonKind.Array, file + " ground.patches");
                if (r.Items.Count != 4)
                    throw new FormatException(file + ": a patch's footprint_m is [xmin, zmin, xmax, zmax]");
                gaps.Add(new Gap
                {
                    X0 = Number(r.Items[0], file + " footprint_m"), Z0 = Number(r.Items[1], file + " footprint_m"),
                    X1 = Number(r.Items[2], file + " footprint_m"), Z1 = Number(r.Items[3], file + " footprint_m"),
                });
            }
            foreach (JsonNode hole in Need(ground, "holes", JsonKind.Array, file + " ground").Items)
            {
                string where = file + " ground.holes";
                double radius = 0.5 * Number(hole, "band_width_m", where);
                JsonNode line = Need(hole, "centre_line_m", JsonKind.Array, where);
                if (line.Items.Count < 2 || !(radius > 0))
                    throw new FormatException(where + ": a band needs two centre-line points and a width");
                var g = new Gap { BandFirst = band.Count, BandCount = line.Items.Count, Radius = radius,
                    X0 = double.PositiveInfinity, Z0 = double.PositiveInfinity, X1 = double.NegativeInfinity, Z1 = double.NegativeInfinity };
                foreach (JsonNode p in line.Items)
                {
                    Double3 v = Vector(p, where);
                    band.Add(new XZ(v.X, v.Z));
                    g.X0 = DetMath.Min(g.X0, v.X - radius);
                    g.Z0 = DetMath.Min(g.Z0, v.Z - radius);
                    g.X1 = DetMath.Max(g.X1, v.X + radius);
                    g.Z1 = DetMath.Max(g.Z1, v.Z + radius);
                }
                gaps.Add(g);
            }
            _gaps = gaps.ToArray();
            _band = band.ToArray();

            // The cells: shapes by their bounds, gaps by their rectangles.
            ListCells(_prims.Length, k =>
            {
                ref Prim p = ref _prims[k];
                p.CellX = CellX(p.Box.Min.X);
                p.CellZ = CellZ(p.Box.Min.Z);
                return (p.CellX, CellX(p.Box.Max.X), p.CellZ, CellZ(p.Box.Max.Z));
            }, out _cellStart, out _cellItems);
            ListCells(_gaps.Length, k => (CellX(_gaps[k].X0), CellX(_gaps[k].X1), CellZ(_gaps[k].Z0), CellZ(_gaps[k].Z1)), out _gapStart, out _gapItems);

            // The start points.
            var starts = new List<StartPoint>();
            JsonNode startPoints = root.Member("start_points");
            if (startPoints != null)
            {
                foreach (JsonNode s in startPoints.Items)
                {
                    string where = file + " start_points";
                    string legs = Text(s, "legs", where);
                    if (legs != "on" && legs != "off")
                        throw new FormatException(where + ": legs is on or off");
                    JsonNode stand = Need(s, "stand", JsonKind.Bool, where);
                    starts.Add(new StartPoint(Text(s, "id", where), Vector(s, "position_m", where), Number(s, "heading_deg", where), legs == "on", stand.Bool));
                }
            }
            StartPoints = starts.ToArray();

            // The identity: the placement file, each metadata file by path, the material names.
            ulong hash = WorldHash.HashFile(WorldHash.Offset, Encoding.UTF8.GetBytes(placementsJson), true);
            var paths = new List<string>(metas.Keys);
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
                hash = WorldHash.HashFile(hash, Encoding.UTF8.GetBytes(metas[path].Text), true);
            hash = WorldHash.HashFile(hash, Encoding.UTF8.GetBytes("materials\n" + string.Join("\n", _materials)), true);
            ContentHash = hash;
        }

        /// <summary>A wire of `meta` placed at the row's pose: its points into the wire arrays from `wireAt`, and its shape.</summary>
        private Prim PlaceWire(Meta meta, int w, Double3 position, Quat yaw, int wireAt, int obj, int shape, string where)
        {
            Double3[] local = meta.WirePoints[w];
            double radius = meta.WireRadius[w];
            Double3 first = position + yaw.Rotate(local[0]), last = position + yaw.Rotate(local[local.Length - 1]);
            Double3 chord = last - first;
            double span = chord.Length();
            if (!(span > 0))
                throw new FormatException(where + $": wire {w} has no span");
            chord = chord * (1 / span);
            var box = Bounds.Of(first, first, radius);
            double along = 0;
            for (int k = 0; k < local.Length; k++)
            {
                Double3 p = position + yaw.Rotate(local[k]);
                if (k > 0)
                    along += (p - _wirePoints[wireAt + k - 1]).Length();
                _wirePoints[wireAt + k] = p;
                _wireAlong[wireAt + k] = along;
                _wireProj[wireAt + k] = Double3.Dot(p - first, chord);
                if (k > 0 && !(_wireProj[wireAt + k] > _wireProj[wireAt + k - 1]))
                    throw new FormatException(where + $": wire {w} turns back along its chord at point {k}");
                box = box.Union(Bounds.Of(p, p, radius));
            }
            Double3 side = Double3.Cross(chord, new Double3(0, 1, 0));
            side = side.Length() > 1e-6 ? side * (1 / side.Length()) : new Double3(1, 0, 0);
            var g = new ShapeGeometry
            {
                Kind = ShapeKind.Wire,
                Center = (first + last) * 0.5,
                AxisX = side,
                AxisY = chord,
                AxisZ = Double3.Cross(side, chord),
                Radius = radius,
                Material = meta.WireMaterial[w],
                SpanA = first,
                SpanB = last,
                Sag = meta.WireSag[w],
            };
            return new Prim { Geometry = g, Box = box, Object = obj, Shape = shape, WireFirst = wireAt, WireCount = local.Length };
        }

        /// <summary>Each item k's cell range (x0, x1, z0, z1) listed in compressed rows: start[cell] … start[cell + 1] in item order.</summary>
        private void ListCells(int items, Func<int, (int x0, int x1, int z0, int z1)> range, out int[] start, out int[] list)
        {
            int cells = _nx * _nz;
            start = new int[cells + 1];
            var ranges = new (int x0, int x1, int z0, int z1)[items];
            for (int k = 0; k < items; k++)
            {
                ranges[k] = range(k);
                for (int cz = ranges[k].z0; cz <= ranges[k].z1; cz++)
                    for (int cx = ranges[k].x0; cx <= ranges[k].x1; cx++)
                        start[cz * _nx + cx + 1]++;
            }
            for (int c = 0; c < cells; c++)
                start[c + 1] += start[c];
            list = new int[start[cells]];
            var fill = new int[cells];
            for (int k = 0; k < items; k++)
                for (int cz = ranges[k].z0; cz <= ranges[k].z1; cz++)
                    for (int cx = ranges[k].x0; cx <= ranges[k].x1; cx++)
                    {
                        int c = cz * _nx + cx;
                        list[start[c] + fill[c]++] = k;
                    }
        }

        // ---- The metadata ------------------------------------------------------------------------------------------

        private static Meta ParseMeta(string path, string text, Dictionary<string, ushort> materialIds)
        {
            if (text == null)
                throw new FormatException(path + ": not found");
            JsonNode root = DetJson.Parse(text);
            if (Text(root, "frame", path) != "unity-lh-yup" || Text(root, "units", path) != "m")
                throw new FormatException(path + ": frame must be unity-lh-yup and units m");
            JsonNode collision = Need(root, "collision", JsonKind.Array, path);
            JsonNode wires = Need(root, "wires", JsonKind.Array, path);
            var meta = new Meta
            {
                Text = text,
                Shapes = new ShapeGeometry[collision.Items.Count],
                Rotations = new Quat[collision.Items.Count],
                WirePoints = new Double3[wires.Items.Count][],
                WireRadius = new double[wires.Items.Count],
                WireSag = new double[wires.Items.Count],
                WireMaterial = new ushort[wires.Items.Count],
            };
            for (int s = 0; s < collision.Items.Count; s++)
            {
                JsonNode c = collision.Items[s];
                string where = path + " collision " + s;
                var g = new ShapeGeometry { Center = Vector(c, "center", where) };
                switch (Text(c, "shape", where))
                {
                    case "box":
                        g.Kind = ShapeKind.Box;
                        g.HalfExtents = Vector(c, "size", where) * 0.5;
                        break;
                    case "sphere":
                        g.Kind = ShapeKind.Sphere;
                        g.Radius = Number(c, "radius", where);
                        break;
                    case "capsule":
                        g.Kind = ShapeKind.Capsule;
                        g.Radius = Number(c, "radius", where);
                        g.HalfLength = DetMath.Max(0, Number(c, "height", where) * 0.5 - g.Radius);
                        break;
                    case "cylinder":
                        g.Kind = ShapeKind.Cylinder;
                        g.Radius = Number(c, "radius", where);
                        g.HalfLength = Number(c, "height", where) * 0.5;
                        break;
                    default:
                        throw new FormatException(where + ": shape is box, sphere, capsule or cylinder");
                }
                if (!(g.Radius >= 0 && g.HalfLength >= 0 && g.HalfExtents.X >= 0 && g.HalfExtents.Y >= 0 && g.HalfExtents.Z >= 0))
                    throw new FormatException(where + ": a negative size");
                Ids(Text(c, "material", where), materialIds, where, out g.Material, out g.Surface);
                meta.Shapes[s] = g;
                JsonNode rotation = c.Member("rotation");
                meta.Rotations[s] = rotation == null ? Quat.Identity : Rotation(rotation, where);
            }
            for (int w = 0; w < wires.Items.Count; w++)
            {
                JsonNode wire = wires.Items[w];
                string where = path + " wire " + w;
                JsonNode points = Need(wire, "points", JsonKind.Array, where);
                if (points.Items.Count < 2)
                    throw new FormatException(where + ": a wire needs two points");
                meta.WirePoints[w] = new Double3[points.Items.Count];
                for (int k = 0; k < points.Items.Count; k++)
                    meta.WirePoints[w][k] = Vector(points.Items[k], where + " points");
                meta.WireRadius[w] = 0.5 * Number(wire, "diameter_m", where);
                meta.WireSag[w] = Number(wire, "sag_m", where);
                if (!(meta.WireRadius[w] > 0))
                    throw new FormatException(where + ": diameter_m must be above 0");
                Ids(Text(wire, "material", where), materialIds, where, out meta.WireMaterial[w], out byte soil);
                if (soil != WorldIds.NoSurface)
                    throw new FormatException(where + ": a wire's material is a material, not a surface");
            }
            return meta;
        }

        private static void Ids(string name, Dictionary<string, ushort> materialIds, string where, out ushort material, out byte surface)
        {
            material = WorldIds.NoMaterial;
            surface = WorldIds.NoSurface;
            const string prefix = "surface:";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                surface = SurfaceId(name.Substring(prefix.Length));
                if (surface == WorldIds.NoSurface)
                    throw new FormatException(where + $": '{name}' is not one of the thirteen surface ids");
            }
            else if (!materialIds.TryGetValue(name, out material))
                throw new FormatException(where + $": '{name}' is not a material of the table");
        }

        private static Quat Rotation(JsonNode node, string where)
        {
            if (node.Kind != JsonKind.Array || node.Items.Count != 4)
                throw new FormatException(where + ": rotation is [x, y, z, w]");
            var q = new Quat(Number(node.Items[0], where), Number(node.Items[1], where), Number(node.Items[2], where), Number(node.Items[3], where));
            if (!(q.Norm() > 0.5))
                throw new FormatException(where + ": rotation is not a unit quaternion");
            return q.Normalized();
        }

        // ---- JSON fields -------------------------------------------------------------------------------------------

        private static JsonNode Need(JsonNode o, string key, JsonKind kind, string where)
        {
            JsonNode n = o.Member(key);
            if (n == null || n.Kind != kind)
                throw new FormatException(where + ": " + key + " is missing or not a" + (kind == JsonKind.Array ? "n array" : " " + kind.ToString().ToLowerInvariant()));
            return n;
        }

        private static string Text(JsonNode o, string key, string where) => Need(o, key, JsonKind.String, where).Text;

        private static double Number(JsonNode o, string key, string where) => Number(Need(o, key, JsonKind.Number, where), where + " " + key);

        private static double Number(JsonNode n, string where)
        {
            if (n.Kind != JsonKind.Number)
                throw new FormatException(where + ": not a number");
            return n.Number;
        }

        private static Double3 Vector(JsonNode o, string key, string where) => Vector(Need(o, key, JsonKind.Array, where), where + " " + key);

        private static Double3 Vector(JsonNode n, string where)
        {
            if (n.Kind != JsonKind.Array || n.Items.Count != 3)
                throw new FormatException(where + ": not [x, y, z]");
            return new Double3(Number(n.Items[0], where), Number(n.Items[1], where), Number(n.Items[2], where));
        }
    }
}
