// MapWorld's snag records for the fiber (p7d-tether task 1.3, design 12; the fortifications reading §1 and §7; WLD-11):
// SnagsNear. The query's rules are IWorldQuery's (Snag records near a point).
// - The records: a variant's "snag" array (asset-pipeline; tools/blender/lib/meta.py), read once however often it is
//   placed, each {"kind": "edge" or "hook", "points": two or more [x, y, z] in the variant's frame, "radius_m" above 0,
//   "material": a material of the table, optionally "edge_radius_m" in (0, radius_m] and, on an edge only,
//   "barb_pitch_m" above 0}. A variant's wires read their edge radii from these records too (MapWorldWires.cs).
// - The index: per variant, the micro elements' 3-D grid (MapWorldMicro.cs's BuildGrid, ElementCell cells) over its
//   segments' bounds, each grown by its record's radius_m; per map, the 2 m grid (MapWorld.cs) listing each placement
//   with records by its segments' world bounds. A query marks the placements listed in the cells its sphere's bounds
//   meet in a bit set on the stack and reads it in index order (object order); in each it takes the sphere into the
//   placement's frame, marks the segments listed in the cells its box meets in a second bit set and reads it in index
//   order (record, then segment), so results come in canonical order unsorted.
// - The test: a segment is returned when the squared distance from the centre to it, its points placed in world space
//   first (the micro elements' Pose), is at most (radius + radius_m)². + − × ÷ only; the result carries those points.
// Load time allocates and throws FormatException naming the file; queries are allocation-free and reentrant.

using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>The most placements with snag records a map may hold: a query's bit set of them lives on the stack (8 KB).</summary>
        private const int MaxSnagObjects = 1 << 16;

        /// <summary>The most snag record segments a variant may hold: a query's bit set of them lives on the stack (32 KB).</summary>
        private const int MaxVariantSegments = 1 << 18;

        /// <summary>A variant's snag records and their segments' grid, in its own frame.</summary>
        private sealed class Snags
        {
            // Per record.
            public SnagKind[] Kind;
            public double[] Radius, Edge, Pitch; // Edge and Pitch 0 when the record gives none
            public ushort[] Material;
            public int[] First;                  // its first point in Points; one more entry, the end
            public Double3[] Points;
            // Per segment, record by record.
            public int[] SegmentRecord, SegmentPoint; // its record; its first point in Points
            public Bounds[] SegmentBox;               // grown by its record's radius
            public Bounds Box = Bounds.Empty;
            public int Nx, Ny, Nz;
            public int[] CellStart, CellItems;        // per cell ((z·Ny + y)·Nx + x): its segments, ascending

            public int Count(int record) => First[record + 1] - First[record];

            public int Words => (SegmentRecord.Length + 63) >> 6;
        }

        // Set at load only.
        private int[] _snagObjects;   // the placed objects with snag records, ascending
        private Snags[] _snagOf;      // their variants'
        private Bounds[] _snagBox;    // their segments' world bounds
        private int[] _snagCellStart, _snagCellItems; // per 2 m cell: the placements (index in _snagObjects) whose bounds meet it
        private int _snagWords;       // the largest variant's segment bit set, 64-bit words

        // ---- SnagsNear ---------------------------------------------------------------------------------------------

        public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results)
        {
            if (_snagObjects.Length == 0)
                return 0;
            Bounds reach = Bounds.Of(centre, centre, radius);
            Span<ulong> placed = stackalloc ulong[(_snagObjects.Length + 63) >> 6];
            placed.Clear();
            CellRange(reach, out int x0, out int x1, out int z0, out int z1);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cz * _nx + cx;
                    for (int j = _snagCellStart[cell]; j < _snagCellStart[cell + 1]; j++)
                    {
                        int k = _snagCellItems[j];
                        placed[k >> 6] |= 1UL << (k & 63);
                    }
                }
            }
            Span<ulong> marks = stackalloc ulong[_snagWords];
            int count = 0;
            for (int w = 0; w < placed.Length; w++)
            {
                ulong bits = placed[w];
                while (bits != 0)
                {
                    ulong low = bits & (0UL - bits);
                    bits ^= low;
                    int k = (w << 6) | Lowest[(int)((low * DeBruijn) >> 58)];
                    if (_snagBox[k].Overlaps(reach))
                        count = Segments(k, centre, radius, marks, results, count);
                }
            }
            return count;
        }

        /// <summary>Placement k's segments that meet the sphere, appended to results from `count`; the new count.</summary>
        private int Segments(int k, Double3 centre, double radius, Span<ulong> marks, Span<SnagSegment> results, int count)
        {
            int obj = _snagObjects[k];
            Snags s = _snagOf[k];
            Pose pose = _rowPose[obj];
            Double3 local = pose.Local(centre);
            Bounds near = Bounds.Of(local, local, radius + FramePad);
            if (!s.Box.Overlaps(near))
                return count;
            Span<ulong> marked = marks.Slice(0, s.Words);
            marked.Clear();
            Double3 min = s.Box.Min;
            int x0 = Cell(near.Min.X, min.X, s.Nx), x1 = Cell(near.Max.X, min.X, s.Nx);
            int y0 = Cell(near.Min.Y, min.Y, s.Ny), y1 = Cell(near.Max.Y, min.Y, s.Ny);
            int z0 = Cell(near.Min.Z, min.Z, s.Nz), z1 = Cell(near.Max.Z, min.Z, s.Nz);
            for (int cz = z0; cz <= z1; cz++)
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        int cell = (cz * s.Ny + cy) * s.Nx + cx;
                        for (int j = s.CellStart[cell]; j < s.CellStart[cell + 1]; j++)
                        {
                            int e = s.CellItems[j];
                            marked[e >> 6] |= 1UL << (e & 63);
                        }
                    }
            for (int w = 0; w < marked.Length; w++)
            {
                ulong bits = marked[w];
                while (bits != 0)
                {
                    ulong low = bits & (0UL - bits);
                    bits ^= low;
                    int i = (w << 6) | Lowest[(int)((low * DeBruijn) >> 58)];
                    if (!s.SegmentBox[i].Overlaps(near))
                        continue;
                    int record = s.SegmentRecord[i], p = s.SegmentPoint[i];
                    Double3 a = pose.Place(s.Points[p]), b = pose.Place(s.Points[p + 1]);
                    double r = radius + s.Radius[record];
                    if (!(SegmentDistanceSquared(centre, a, b) <= r * r))
                        continue;
                    if (count < results.Length)
                    {
                        results[count] = new SnagSegment
                        {
                            Object = obj,
                            Record = record,
                            Segment = p - s.First[record],
                            Kind = s.Kind[record],
                            Radius = s.Radius[record],
                            EdgeRadius = s.Edge[record] > 0 ? s.Edge[record] : s.Radius[record],
                            BarbPitch = s.Pitch[record],
                            Material = s.Material[record],
                            A = a,
                            B = b,
                        };
                    }
                    count++;
                }
            }
            return count;
        }

        // ---- Load --------------------------------------------------------------------------------------------------

        /// <summary>A variant's snag records (none without a "snag" array) and its segments' grid (MapWorldLoad's ParseMeta).</summary>
        private static Snags ParseSnags(string path, JsonNode root, Dictionary<string, ushort> materialIds)
        {
            JsonNode snag = root.Member("snag");
            if (snag != null && snag.Kind != JsonKind.Array)
                throw new FormatException(path + ": snag is not an array");
            int n = snag == null ? 0 : snag.Items.Count;
            var s = new Snags
            {
                Kind = new SnagKind[n], Radius = new double[n], Edge = new double[n], Pitch = new double[n], Material = new ushort[n],
                First = new int[n + 1],
            };
            var points = new List<Double3>();
            for (int r = 0; r < n; r++)
            {
                JsonNode record = snag.Items[r];
                string where = path + " snag " + r;
                string kind = Text(record, "kind", where);
                if (kind != "edge" && kind != "hook")
                    throw new FormatException(where + ": kind is edge or hook");
                s.Kind[r] = kind == "edge" ? SnagKind.Edge : SnagKind.Hook;
                JsonNode list = Need(record, "points", JsonKind.Array, where);
                if (list.Items.Count < 2)
                    throw new FormatException(where + ": a snag record needs two points");
                s.First[r] = points.Count;
                foreach (JsonNode point in list.Items)
                    points.Add(Vector(point, where + " points"));
                s.Radius[r] = Positive(record, "radius_m", where);
                if (record.Member("edge_radius_m") != null)
                {
                    s.Edge[r] = Positive(record, "edge_radius_m", where);
                    if (!(s.Edge[r] <= s.Radius[r]))
                        throw new FormatException(where + ": edge_radius_m must be at most radius_m");
                }
                if (record.Member("barb_pitch_m") != null)
                {
                    if (s.Kind[r] != SnagKind.Edge)
                        throw new FormatException(where + ": barb_pitch_m is an edge's only");
                    s.Pitch[r] = Positive(record, "barb_pitch_m", where);
                }
                Ids(Text(record, "material", where), materialIds, where, out s.Material[r], out byte soil);
                if (soil != WorldIds.NoSurface)
                    throw new FormatException(where + ": a snag record's material is a material, not a surface");
            }
            s.First[n] = points.Count;
            s.Points = points.ToArray();
            int segments = s.Points.Length - n;
            if (segments > MaxVariantSegments)
                throw new FormatException(path + $": {segments} snag record segments, more than a variant may hold ({MaxVariantSegments})");
            s.SegmentRecord = new int[segments];
            s.SegmentPoint = new int[segments];
            s.SegmentBox = new Bounds[segments];
            for (int r = 0, i = 0; r < n; r++)
            {
                for (int p = s.First[r]; p + 1 < s.First[r + 1]; p++, i++)
                {
                    s.SegmentRecord[i] = r;
                    s.SegmentPoint[i] = p;
                    s.SegmentBox[i] = Bounds.Of(s.Points[p], s.Points[p + 1], s.Radius[r]);
                }
            }
            if (segments > 0)
                s.Box = BuildGrid(s.SegmentBox, out s.Nx, out s.Ny, out s.Nz, out s.CellStart, out s.CellItems);
            return s;
        }

        /// <summary>
        /// The placements with snag records, their world bounds (their variant's segments' bounds placed, as the micro
        /// elements' are) and their 2 m cells. After PlaceMicro, which sets every row's pose.
        /// </summary>
        private void PlaceSnags(Meta[] rowMeta, string file)
        {
            var objects = new List<int>();
            var of = new List<Snags>();
            var boxes = new List<Bounds>();
            for (int i = 0; i < rowMeta.Length; i++)
            {
                Snags s = rowMeta[i].Snags;
                if (s.SegmentRecord.Length == 0)
                    continue;
                Bounds world = Bounds.Empty;
                for (int c = 0; c < 8; c++)
                {
                    Double3 corner = _rowPose[i].Place(new Double3((c & 1) == 0 ? s.Box.Min.X : s.Box.Max.X,
                        (c & 2) == 0 ? s.Box.Min.Y : s.Box.Max.Y, (c & 4) == 0 ? s.Box.Min.Z : s.Box.Max.Z));
                    world = world.Union(Bounds.Of(corner, corner, FramePad));
                }
                objects.Add(i);
                of.Add(s);
                boxes.Add(world);
                _snagWords = Math.Max(_snagWords, s.Words);
            }
            if (objects.Count > MaxSnagObjects)
                throw new FormatException(file + $": {objects.Count} placements with snag records, more than a map may hold ({MaxSnagObjects})");
            _snagObjects = objects.ToArray();
            _snagOf = of.ToArray();
            _snagBox = boxes.ToArray();
            ListCells(_snagObjects.Length, k => (CellX(_snagBox[k].Min.X), CellX(_snagBox[k].Max.X), CellZ(_snagBox[k].Min.Z), CellZ(_snagBox[k].Max.Z)),
                out _snagCellStart, out _snagCellItems);
        }
    }
}
