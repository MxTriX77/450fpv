// MapWorld's wind volumes (p7c-wind, the map's wind): each placed variant's `wind_volumes` records (K§10.2, the asset's
// physics metadata) placed at its row's pose, and each row's `group` (the map generator's: "posadka", "yard", ...), so the
// wind can build the wake grid's obstacles and read the tree belt from the world it flies in (MapWind). They are not
// queries: nothing here runs on the flight thread, and the content hash already covers every file they come from.
// - A volume upright in its asset (rotation the identity, or about the vertical only) keeps its shape and turns with the
//   row's yaw and its own (the asset's turn about +Y, 2·atan2(y, w) of its quaternion, D-019's sense).
// - A volume tilted off the vertical (a roof plane, a strut, a leaning car part) enters as the upright box that bounds
//   it in the asset's frame (worldbench's WakeRange does the same): the wake grid's obstacles are upright (K§10.3).
// - A part set into the ground keeps only what is above it (its base at 0); one with nothing above it is dropped.
// The cut records (p7c-wind design 10, D-071, task 1.10a): each ground.holes record of the placement file, with what the
// wind's cavity model reads, derived here from the files the content hash already covers (so the hash stays). Mirrored
// by tools/map/make_test_range.py (cut_record), whose cut_problems is the map's check on the same fields.
// - The centre line as the file gives it, each point's y the lip's height there; top_width_m and depth_m as the file
//   gives them.
// - Covered: a ray up from the centre line's middle (half its plan length along it), half the depth under the lip
//   there, meets one of its object's fillers below the lip, or starts in one: fillers roof it (E01's niches).
// - The lip width: an open record's opening at the lip, crumble included, is the mean narrowest_m of its object's
//   level gaps (its openings in the ground: E01's slots) whose centre lies in its band. With none (Z04's stand-in, box
//   walls without crumble), or for a covered record (no opening at its lip), it is its top_width_m.
// - The ends: the opening past the first and the last centre-line points, along the end segments LipProbe under the
//   lip, to its object's first filler (0 from inside one). One that meets none inside the band throws.
// Load time only: it allocates and throws FormatException naming the file and the field.

using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>A variant's wind volume in its own frame, as `.assetmeta.json` holds it.</summary>
        private struct WindRecord
        {
            public WindVolumeKind Kind;
            public Double3 Center, Size; // a cylinder's Size is (2r, height, 2r)
            public double Radius, Porosity;
            public Quat Rotation;
        }

        private WindVolume[] _windVolumes = Array.Empty<WindVolume>();
        private int[] _windObject = Array.Empty<int>();
        private string[] _groups;

        /// <summary>The placed objects' wind volumes, world metres (K§10.2), object by object in row order.</summary>
        public ReadOnlySpan<WindVolume> WindVolumes => _windVolumes;

        /// <summary>The placed object wind volume k belongs to (a row's index).</summary>
        public int WindVolumeObject(int k) => _windObject[k];

        /// <summary>A placed object's group in the placement file (the generator's: "posadka", "yard", ...), or null.</summary>
        public string ObjectGroup(int obj) => _groups[obj];

        /// <summary>The map's extent, world X and Z of its south-west corner, m, and its 2 m cells each way.</summary>
        public double ExtentX => _gridX;
        public double ExtentZ => _gridZ;
        public int CellsX => _nx;
        public int CellsZ => _nz;

        private static WindRecord[] ParseWind(JsonNode root, string path)
        {
            JsonNode list = root.Member("wind_volumes");
            if (list == null) return Array.Empty<WindRecord>();
            if (list.Kind != JsonKind.Array) throw new FormatException(path + ": wind_volumes is not an array");
            var records = new WindRecord[list.Items.Count];
            for (int v = 0; v < records.Length; v++)
            {
                JsonNode n = list.Items[v];
                string where = path + " wind_volumes " + v;
                var r = new WindRecord { Center = Vector(n, "center", where), Porosity = Number(n, "porosity", where) };
                JsonNode rotation = n.Member("rotation");
                r.Rotation = rotation == null ? Quat.Identity : Rotation(rotation, where);
                switch (Text(n, "shape", where))
                {
                    case "box":
                        r.Kind = WindVolumeKind.Box;
                        r.Size = Vector(n, "size", where);
                        break;
                    case "cylinder":
                        r.Kind = WindVolumeKind.Cylinder;
                        r.Radius = Number(n, "radius", where);
                        r.Size = new Double3(2 * r.Radius, Number(n, "height", where), 2 * r.Radius);
                        break;
                    default:
                        throw new FormatException(where + ": shape is box or cylinder");
                }
                if (!(r.Size.X > 0 && r.Size.Y > 0 && r.Size.Z > 0) || !(r.Porosity >= 0 && r.Porosity <= 1))
                    throw new FormatException(where + ": a size of 0 or less, or a porosity outside 0–1");
                records[v] = r;
            }
            return records;
        }

        /// <summary>
        /// Every row's wind volumes placed at its pose (position, yaw in radians, scale), and the rows' groups. A scaled row's
        /// volume is the scaled one: its centre and size per axis, a cylinder's radius by the scale's x. Its x must be its z,
        /// so the scale turns with any yaw. A tilted row (no yaw) has no wind volumes.
        /// </summary>
        private void PlaceWind(Meta[] rowMeta, Double3[] rowPosition, double[] rowYaw, Double3[] rowScale, string[] groups, string file)
        {
            _groups = groups;
            var volumes = new List<WindVolume>();
            var objects = new List<int>();
            for (int i = 0; i < rowMeta.Length; i++)
                foreach (WindRecord r in rowMeta[i].Wind)
                {
                    if (double.IsNaN(rowYaw[i]) || rowScale[i].X != rowScale[i].Z)
                        throw new FormatException(file + " placement " + i + ": a tilted row has no wind volumes, and a scaled one's x is its z");
                    Quat q = r.Rotation;
                    Double3 centre = r.Center, size = r.Size;
                    WindVolumeKind kind = r.Kind;
                    double localYaw = 0;
                    if (DetMath.Abs(q.X) < 1e-9 && DetMath.Abs(q.Z) < 1e-9)
                        localYaw = 2 * DetMath.Atan2(q.Y, q.W); // about the vertical only
                    else
                    {
                        // Tilted: the upright box bounding the turned one, in the asset's frame.
                        Double3 lo = new Double3(double.MaxValue, double.MaxValue, double.MaxValue), hi = lo * -1;
                        for (int k = 0; k < 8; k++)
                        {
                            Double3 c = q.Rotate(new Double3((k & 1) == 0 ? -size.X / 2 : size.X / 2,
                                (k & 2) == 0 ? -size.Y / 2 : size.Y / 2, (k & 4) == 0 ? -size.Z / 2 : size.Z / 2));
                            lo = new Double3(DetMath.Min(lo.X, c.X), DetMath.Min(lo.Y, c.Y), DetMath.Min(lo.Z, c.Z));
                            hi = new Double3(DetMath.Max(hi.X, c.X), DetMath.Max(hi.Y, c.Y), DetMath.Max(hi.Z, c.Z));
                        }
                        centre = centre + (lo + hi) * 0.5;
                        size = hi - lo;
                        kind = WindVolumeKind.Box;
                    }
                    Double3 p = rowPosition[i], scale = rowScale[i];
                    centre = Mul(centre, scale);
                    size = Mul(size, scale);
                    WindVolume w = WindVolume.Placed(kind, centre, size.X, size.Y, size.Z, kind == WindVolumeKind.Cylinder ? r.Radius * scale.X : 0,
                        r.Porosity, p.X, p.Z, rowYaw[i], 1);
                    w.Yaw = rowYaw[i] + localYaw;
                    if (w.BaseM < 0) w.BaseM = 0; // set into the ground
                    if (!(w.TopM > w.BaseM)) continue;
                    volumes.Add(w);
                    objects.Add(i);
                }
            _windVolumes = volumes.ToArray();
            _windObject = objects.ToArray();
        }

        // ---- The cut records ---------------------------------------------------------------------------------------

        /// <summary>The ends are measured this far under the lip, m (assumed: below the tops of the hole contract's cut
        /// faces, 3 mm under it in trench.py's LIP_DROP_M, so a ray there meets the cut's own walls).</summary>
        public const double LipProbe = 0.01;

        /// <summary>A gap whose normal lies within 2.6° of vertical is level, an opening in the ground (assumed).</summary>
        private const double LevelGap = 0.999;

        /// <summary>The hole contract's fillers, a cut's own shapes, are this surface (tools/blender/lib/holes.py, F-7).</summary>
        private const string FillerSurface = "belt_bare";

        private CutRecord[] _cuts = Array.Empty<CutRecord>();

        /// <summary>The placement file's hole records in its order, with what the wind's cavity model reads (D-071).</summary>
        public ReadOnlySpan<CutRecord> Cuts => _cuts;

        /// <summary>A variant's level gaps (see LevelGap): each one's vertices' mean, local, and its narrowest_m.</summary>
        private static void ParseLevelGaps(JsonNode root, string path, out Double3[] centres, out double[] narrowest)
        {
            var c = new List<Double3>();
            var w = new List<double>();
            JsonNode list = root.Member("gaps");
            if (list != null)
            {
                if (list.Kind != JsonKind.Array) throw new FormatException(path + ": gaps is not an array");
                for (int k = 0; k < list.Items.Count; k++)
                {
                    JsonNode gap = list.Items[k];
                    string where = path + " gaps " + k;
                    Double3 n = Vector(gap, "normal", where);
                    if (n.Y * n.Y < LevelGap * LevelGap * Double3.Dot(n, n)) continue;
                    JsonNode polygon = Need(gap, "polygon", JsonKind.Array, where);
                    if (polygon.Items.Count == 0) throw new FormatException(where + ": a polygon needs a point");
                    Double3 sum = default;
                    foreach (JsonNode p in polygon.Items) sum = sum + Vector(p, where + " polygon");
                    c.Add(sum * (1.0 / polygon.Items.Count));
                    w.Add(Number(gap, "narrowest_m", where));
                }
            }
            centres = c.ToArray();
            narrowest = w.ToArray();
        }

        /// <summary>A hole record of the placement file (its centre line read, `radius` half its band) as a cut record.</summary>
        private CutRecord PlaceCut(JsonNode hole, Double3[] line, double radius, Meta[] rowMeta, Double3[] rowPosition, Quat[] rowRotation,
            Double3[] rowScale, string where)
        {
            double instance = Number(hole, "instance", where);
            int obj = (int)instance;
            if (obj != instance || obj < 0 || obj >= rowMeta.Length)
                throw new FormatException(where + ": instance is not a row of the file");
            var cut = new CutRecord
            {
                Object = obj,
                Record = Text(hole, "record", where),
                CentreLine = line,
                TopWidth = Number(hole, "top_width_m", where),
                Depth = Number(hole, "depth_m", where),
            };
            if (!(cut.TopWidth > 0 && cut.Depth > 0))
                throw new FormatException(where + ": top_width_m and depth_m must be above 0");

            // Covered: the ray up from the centre line's middle at half depth.
            double half = 0;
            for (int k = 0; k + 1 < line.Length; k++)
                half += PlanLength(line[k + 1] - line[k]);
            half *= 0.5;
            Double3 mid = line[line.Length - 1];
            for (int k = 0; k + 1 < line.Length; k++)
            {
                double s = PlanLength(line[k + 1] - line[k]);
                if (s > 0 && s >= half)
                {
                    mid = line[k] + (line[k + 1] - line[k]) * (half / s);
                    break;
                }
                half -= s;
            }
            double down = 0.5 * cut.Depth;
            cut.Covered = Meet(obj, new Double3(mid.X, mid.Y - down, mid.Z), new Double3(0, 1, 0), down) < down;

            // The lip width: an open record's level gaps in its band.
            cut.LipWidth = cut.TopWidth;
            if (!cut.Covered)
            {
                Meta meta = rowMeta[obj];
                double sum = 0;
                int count = 0;
                for (int k = 0; k < meta.LevelGapCentre.Length; k++)
                {
                    Double3 c = rowPosition[obj] + rowRotation[obj].Rotate(Mul(meta.LevelGapCentre[k], rowScale[obj]));
                    if (PlanDistance(c, line) <= radius)
                    {
                        sum += meta.LevelGapNarrowest[k];
                        count++;
                    }
                }
                if (count > 0)
                    cut.LipWidth = sum / count;
            }

            // The ends.
            Double3 first = line[0], last = line[line.Length - 1];
            cut.EndFirst = Meet(obj, first - new Double3(0, LipProbe, 0), PlanUnit(first - line[1]), radius);
            cut.EndLast = Meet(obj, last - new Double3(0, LipProbe, 0), PlanUnit(last - line[line.Length - 2]), radius);
            if (double.IsPositiveInfinity(cut.EndFirst) || double.IsPositiveInfinity(cut.EndLast))
                throw new FormatException(where + $" {cut.Record}: the opening past an end meets no filler inside its band");
            return cut;
        }

        /// <summary>How far the ray o + t·d (d unit) runs to object obj's first filler: 0 when o lies in one, +∞ for none
        /// within `max`.</summary>
        private double Meet(int obj, Double3 o, Double3 d, double max)
        {
            double best = double.PositiveInfinity;
            byte filler = SurfaceId(FillerSurface);
            for (int k = _first[obj]; k < _first[obj + 1]; k++)
            {
                ref readonly ShapeGeometry s = ref _prims[k].Geometry;
                if (s.Surface != filler) continue;
                ShapeMath.Closest(s, o, o, 0, out Near near);
                if (near.Distance <= 0) return 0;
                double t = double.PositiveInfinity;
                Double3 normal = default;
                ShapeMath.RayPrim(s, o, d, ref t, ref normal);
                if (t < best) best = t;
            }
            return best <= max ? best : double.PositiveInfinity;
        }

        private static double PlanLength(Double3 v) => DetMath.Sqrt(v.X * v.X + v.Z * v.Z);

        private static Double3 PlanUnit(Double3 v) => new Double3(v.X, 0, v.Z) * (1 / PlanLength(v));

        /// <summary>The plan distance from p to the polyline `line`, its ends included.</summary>
        private static double PlanDistance(Double3 p, Double3[] line)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                double dx = line[i + 1].X - line[i].X, dz = line[i + 1].Z - line[i].Z, px = p.X - line[i].X, pz = p.Z - line[i].Z;
                double dd = dx * dx + dz * dz;
                double t = dd > 0 ? (px * dx + pz * dz) / dd : 0;
                t = t > 0 ? t < 1 ? t : 1 : 0;
                double ex = px - dx * t, ez = pz - dz * t;
                best = DetMath.Min(best, DetMath.Sqrt(ex * ex + ez * ez));
            }
            return best;
        }
    }

    /// <summary>
    /// A hole record for the wind's cavity model (p7c-wind design 10, D-071): a cut in the ground, held by MapWorld beside
    /// its wind volumes and derived at load (MapWorldWind.cs, the cut records). World metres, D-019's frame.
    /// </summary>
    public struct CutRecord
    {
        /// <summary>The placed object it belongs to (a row's index) and its record's id in the asset ("run", "niche_0").</summary>
        public int Object;
        public string Record;

        /// <summary>Its centre line, each point's Y the lip's height there.</summary>
        public Double3[] CentreLine;

        /// <summary>The cut's width at the top before any crumble (the spade's) and its depth under the lip, m.</summary>
        public double TopWidth, Depth;

        /// <summary>The opening's width at the lip, crumble included, m.</summary>
        public double LipWidth;

        /// <summary>The opening past the first and the last centre-line points, along the line, m.</summary>
        public double EndFirst, EndLast;

        /// <summary>Whether fillers roof it below the lip (still air under them).</summary>
        public bool Covered;
    }
}
