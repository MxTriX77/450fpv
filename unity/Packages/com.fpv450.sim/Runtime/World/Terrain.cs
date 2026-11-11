// Terrain: a map package's heightfield (w3-farmland-ground 1.1, design.md), MapWorld's ground where the map has one: the
// tiles of maps/<id>/map.json (fpv450-map-package; maps/farmland/README.md), as the physics engineer signed them off
// (w2-farmland-export design.md).
// - The heights: tools/map/validate.py's Tile.height, operation for operation (each cell split on its south-west to
//   north-east diagonal, Unity's indices, z from the south), and ground_height's rule where tiles overlap: the highest,
//   the first in manifest order on a tie. Only + − × ÷ and one √ (the facet's normal); bit-exact with Python at the
//   package's 1,000 height probes (tools/map/golden/farmland-probes.txt).
// - The lookup: each tile is its own regular grid, found from a point by a coarse lookup over the tiles' union (cells the
//   smallest tile's side, each listing in manifest order the tiles that hold a point mapping to it). A query whose reach
//   lies in one lookup cell visits that cell's tiles only. MapWorld's 2 m object grid stays on the map's rectangle.
// - Holes: a tile's optional hole mask (one byte per height cell, 255 a hole, row 0 northmost) removes that tile's column
//   over the cell; a point off every tile is a hole too. SampleGround reports Hole there (height −∞, +Y, no surface);
//   StaticContacts and Raycast keep the terrain's contact or hit only where its point has ground (the cell rule).
// - Surfaces: the class of the highest tile's nearest class sample (the README's sample positions, rounded half up).
// - Step lines: the ground is the union of the tiles' solid columns, so wherever the highest tile changes across a tile
//   edge and the two sides' heights differ, the step between them is a vertical wall (the surround's outer ledge and the
//   far ring's seams on the farmland). They are derived at load from the tile layout alone: every region between the
//   tiles' edges, each edge between two regions, both sides' heights at every breakpoint of either side's tiles, split
//   where the higher side changes. A wall's face looks out of the higher side; it is a piece of the ground for
//   StaticContacts and Raycast, and SampleGround reports the top.
// - The contact of a capsule (the ground, shape 0 of WorldIds.Ground): its pieces are the facets of every tile and the wall
//   quads whose bounds meet the query's reach; on a long reach, only those whose cells meet the sweep's footprint (its four
//   core ends' hull in x–z, grown by the radius and margin), so a long segment costs its length, not its bounding box.
//   Outside the ground the distance is the exact Euclidean one to the nearest piece (a facet buried under a higher tile is
//   never nearer than the ground above it). A core that meets a piece or has an end below the ground is inside: its depth
//   is the deepest of its ends and of its crossings of each facet's footprint, each the distance to the ground's own
//   facet's plane there (the supports' measure, K§7.1; Top, since a higher tile's facets may lie beyond the reach) or
//   to a wall face behind it if nearer, with that face's normal. ShapeMath.Sweep runs the Time rule over it (IWorldQuery), each piece
//   bounding the sweep along its own nearest direction, as a wire's segments do.
// - Exact shortcuts, each giving the same answer as the work it skips:
//   - The sweep's clearance: every piece within the pad lies under the footprint's highest cell corner, so the core's
//     lowest end above it, less the radius, is how far the sweep stays clear (ShapeMath.Sweep's Clearance). A part higher
//     above the ground than its margin costs one pass over its cells.
//   - The bound: the distance moves at most the core ends' larger move per unit of t, so a closest approach farther than
//     that over the rest of the step proves the sweep apart (a part sliding within the margin: one closest approach).
//   - A facet whose plane both core ends lie above, the nearer end's foot on it, is that foot; a piece whose bounds lie
//     as far as the nearest so far is skipped; an end above a facet of a tile no other overlaps is above the ground.
// - Rays: per tile, a walk through the cells the ray crosses (each cell's two facets, entered from above), and every wall
//   it enters from the lower side; the first entry wins. A ray that starts inside the ground does not hit it.
// Engine-free, allocation-free after loading, reentrant. Load time only: reading allocates and throws FormatException
// naming the file and the field; every file's SHA-256 is checked against the manifest, which the content hash covers.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    internal sealed class Terrain
    {
        /// <summary>Unity's heightmap: a height is sample / 32766 of the tile's height (validate.py's MAX_SAMPLE).</summary>
        public const double MaxSample = 32766;

        /// <summary>A reach of more cells than this in a tile walks only the cells its sweep's hull reaches (a long segment).</summary>
        private const int Corridor = 16;

        /// <summary>The footprint's slack beyond the radius and margin, m: rounding never drops a piece the reach holds.</summary>
        private const double Slack = 1e-6;

        internal sealed class Tile
        {
            public string Id, Ring;
            public double X0, Y0, Z0, Sx, Sy, Sz;
            /// <summary>X0 + Sx and Z0 + Sz, as validate.py's contains adds them.</summary>
            public double X1, Z1;
            /// <summary>Cells a side: the resolution less one.</summary>
            public int N;
            /// <summary>The spacings Sx / N and Sz / N, m (dyadic on the farmland: 0.9765625, 11.71875, 11.8125).</summary>
            public double Dx, Dz;
            /// <summary>Sy / MaxSample: m per sample unit (the facet's slope).</summary>
            public double K;
            /// <summary>(N + 1)² samples, row 0 northmost, column 0 westmost (the file's order).</summary>
            public ushort[] H;
            public byte[] Classes;
            public int ClassRes;
            /// <summary>N² bytes, 255 a hole, row 0 northmost; null for none.</summary>
            public byte[] Holes;
            /// <summary>No other tile overlaps it (shared edges aside): its surface is the ground wherever it has no hole.</summary>
            public bool Alone;
        }

        /// <summary>One quad of a step's face, between L0 and L1 along its line: its lower and upper edge at each end, m.</summary>
        private struct Wall
        {
            public double L0, L1, Bottom0, Top0, Bottom1, Top1;
            /// <summary>+1 where the higher side lies below the line's coordinate (the face looks along +axis), −1 above.</summary>
            public double Side;
            /// <summary>The higher side's tile (its class is the face's soil).</summary>
            public int High;
        }

        /// <summary>A line x = C (AlongZ) or z = C holding walls First … First + Count − 1, ordered along it.</summary>
        private struct Line
        {
            public bool AlongZ;
            public double C, From, To;
            public int First, Count;
        }

        private readonly Tile[] _tiles;
        private readonly double _ux0, _uz0, _ux1, _uz1, _side;
        private readonly int _nx, _nz;
        private readonly int[] _start, _list;
        private readonly Wall[] _walls;
        private readonly Line[] _lines;

        public string MapId { get; }

        /// <summary>The map's rectangle [xmin, zmin, xmax, zmax], m (map_rect_m): MapWorld's object grid.</summary>
        public double MapX0 { get; }
        public double MapZ0 { get; }
        public double MapX1 { get; }
        public double MapZ1 { get; }

        /// <summary>The steps derived at load, for the records and the tests.</summary>
        public TerrainStep[] Steps { get; }

        // ---- Loading -----------------------------------------------------------------------------------------------

        /// <summary>The package in `folder` (its map.json and the files it lists, each checked against its SHA-256).</summary>
        public static Terrain Load(string folder, out string manifest)
        {
            manifest = File.ReadAllText(Path.Combine(folder, "map.json"));
            return new Terrain(manifest, relative => File.ReadAllBytes(Path.Combine(folder, relative)));
        }

        public Terrain(string manifest, Func<string, byte[]> readFile)
        {
            const string file = "map.json";
            JsonNode root = DetJson.Parse(manifest);
            if (Text(root, "format", file) != "fpv450-map-package" || Number(root, "format_version", file) != 1)
                throw new FormatException(file + ": not an fpv450-map-package of format_version 1");
            if (Text(root, "frame", file) != "unity-lh-yup")
                throw new FormatException(file + ": frame must be unity-lh-yup");
            MapId = Text(root, "map", file);
            JsonNode rect = Need(root, "map_rect_m", JsonKind.Array, file);
            if (rect.Items.Count != 4)
                throw new FormatException(file + ": map_rect_m is [xmin, zmin, xmax, zmax]");
            MapX0 = Number(rect.Items[0], file + " map_rect_m");
            MapZ0 = Number(rect.Items[1], file + " map_rect_m");
            MapX1 = Number(rect.Items[2], file + " map_rect_m");
            MapZ1 = Number(rect.Items[3], file + " map_rect_m");
            JsonNode files = Need(root, "files", JsonKind.Object, file);

            JsonNode rows = Need(root, "tiles", JsonKind.Array, file);
            if (rows.Items.Count == 0)
                throw new FormatException(file + ": no tiles");
            _tiles = new Tile[rows.Items.Count];
            for (int k = 0; k < _tiles.Length; k++)
                _tiles[k] = ReadTile(rows.Items[k], files, readFile, file + " tile " + k);

            // The coarse lookup over the tiles' union.
            _ux0 = _uz0 = double.PositiveInfinity;
            _ux1 = _uz1 = double.NegativeInfinity;
            double least = double.PositiveInfinity;
            foreach (Tile t in _tiles)
            {
                _ux0 = DetMath.Min(_ux0, t.X0);
                _uz0 = DetMath.Min(_uz0, t.Z0);
                _ux1 = DetMath.Max(_ux1, t.X1);
                _uz1 = DetMath.Max(_uz1, t.Z1);
                least = DetMath.Min(least, DetMath.Min(t.Sx, t.Sz));
            }
            _side = least;
            _nx = Cells(_ux1 - _ux0, _side);
            _nz = Cells(_uz1 - _uz0, _side);
            _start = new int[_nx * _nz + 1];
            var list = new List<int>();
            for (int j = 0; j < _nz; j++)
            {
                for (int i = 0; i < _nx; i++)
                {
                    // A point maps to the cell whose [x0, x1) holds it (the last column and row closed): the tiles holding any such.
                    double x0 = _ux0 + i * _side, z0 = _uz0 + j * _side, x1 = x0 + _side, z1 = z0 + _side;
                    for (int k = 0; k < _tiles.Length; k++)
                    {
                        Tile t = _tiles[k];
                        if (x0 <= t.X1 && (t.X0 < x1 || i == _nx - 1 && t.X0 <= x1) && z0 <= t.Z1 && (t.Z0 < z1 || j == _nz - 1 && t.Z0 <= z1))
                            list.Add(k);
                    }
                    _start[j * _nx + i + 1] = list.Count;
                }
            }
            _list = list.ToArray();

            foreach (Tile t in _tiles)
            {
                t.Alone = true;
                foreach (Tile o in _tiles)
                    if (o != t && t.X0 < o.X1 && o.X0 < t.X1 && t.Z0 < o.Z1 && o.Z0 < t.Z1)
                        t.Alone = false;
            }
            BuildWalls(out _walls, out _lines, out TerrainStep[] steps);
            Steps = steps;
        }

        private static int Cells(double size, double side)
        {
            int n = (int)(size / side);
            return n * side < size ? n + 1 : n;
        }

        private static Tile ReadTile(JsonNode row, JsonNode files, Func<string, byte[]> readFile, string where)
        {
            var t = new Tile { Id = Text(row, "id", where), Ring = Text(row, "ring", where) };
            where = "map.json tile " + t.Id;
            JsonNode position = Need(row, "position_m", JsonKind.Array, where), size = Need(row, "size_m", JsonKind.Array, where);
            if (position.Items.Count != 3 || size.Items.Count != 3)
                throw new FormatException(where + ": position_m and size_m are [x, y, z]");
            t.X0 = Number(position.Items[0], where);
            t.Y0 = Number(position.Items[1], where);
            t.Z0 = Number(position.Items[2], where);
            t.Sx = Number(size.Items[0], where);
            t.Sy = Number(size.Items[1], where);
            t.Sz = Number(size.Items[2], where);
            if (!(t.Sx > 0 && t.Sy > 0 && t.Sz > 0))
                throw new FormatException(where + ": size_m must be positive");
            t.X1 = t.X0 + t.Sx;
            t.Z1 = t.Z0 + t.Sz;

            JsonNode heights = Need(row, "heights", JsonKind.Object, where);
            int res = (int)Number(heights, "resolution", where + " heights");
            if (res < 3 || ((res - 1) & (res - 2)) != 0)
                throw new FormatException(where + ": a heightmap resolution of " + res + ", not 2^n + 1");
            t.N = res - 1;
            t.Dx = t.Sx / t.N;
            t.Dz = t.Sz / t.N;
            t.K = t.Sy / MaxSample;
            byte[] data = Checked(Text(heights, "file", where + " heights"), files, readFile, where);
            if (data.Length != 2 * res * res)
                throw new FormatException(where + $": {data.Length} height bytes for {res} x {res} samples");
            t.H = new ushort[res * res];
            for (int i = 0; i < t.H.Length; i++)
            {
                t.H[i] = (ushort)(data[2 * i] | data[2 * i + 1] << 8);
                if (t.H[i] > MaxSample)
                    throw new FormatException(where + ": a sample above 32766");
            }

            JsonNode classes = Need(row, "classes", JsonKind.Object, where);
            t.ClassRes = (int)Number(classes, "resolution", where + " classes");
            t.Classes = Checked(Text(classes, "file", where + " classes"), files, readFile, where);
            if (t.ClassRes < 2 || t.Classes.Length != t.ClassRes * t.ClassRes)
                throw new FormatException(where + ": the classes are not resolution x resolution bytes");
            foreach (byte c in t.Classes)
                if (c == WorldIds.NoSurface || c > MapWorld.SurfaceIds.Length)
                    throw new FormatException(where + $": class {c} is not one of the thirteen surface ids");

            JsonNode holes = row.Member("holes");
            if (holes != null)
            {
                if ((int)Number(holes, "resolution", where + " holes") != t.N)
                    throw new FormatException(where + ": the hole mask's resolution is the heightmap's less one");
                t.Holes = Checked(Text(holes, "file", where + " holes"), files, readFile, where);
                if (t.Holes.Length != t.N * t.N)
                    throw new FormatException(where + ": the hole mask is not N x N bytes");
                foreach (byte h in t.Holes)
                    if (h != 0 && h != 255)
                        throw new FormatException(where + ": a hole mask holds 0 (ground) or 255 (a hole) only");
            }
            return t;
        }

        /// <summary>A listed file's bytes, its SHA-256 checked against the manifest's (an LFS pointer fails it).</summary>
        private static byte[] Checked(string relative, JsonNode files, Func<string, byte[]> readFile, string where)
        {
            JsonNode digest = files.Member(relative);
            if (digest == null || digest.Kind != JsonKind.String)
                throw new FormatException(where + ": " + relative + " is not listed in files");
            byte[] data = readFile(relative);
            string actual;
            using (SHA256 sha = SHA256.Create())
                actual = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            if (actual != digest.Text)
                throw new FormatException(where + ": " + relative + "'s SHA-256 is not the manifest's (a Git LFS pointer? run git lfs checkout)");
            return data;
        }

        // ---- The heights (validate.py's Tile.height) --------------------------------------------------------------

        /// <summary>Sample (column, Unity row) of a tile: column west to east, row south to north (validate.py's sample).</summary>
        private static int Sample(Tile t, int column, int unityRow) => t.H[(t.N - unityRow) * (t.N + 1) + column];

        /// <summary>validate.py's cell and fractions: fx = clamp((x − x0) / sx, 0, 1)·n, the cell ⌊fx⌋ held below n, u = fx − cell.</summary>
        private static void Locate(Tile t, double x, double z, out int cx, out int cz, out double u, out double v)
        {
            double ax = (x - t.X0) / t.Sx, az = (z - t.Z0) / t.Sz;
            if (0.0 > ax) ax = 0.0; // Python's max(a, 0.0) keeps a unless 0.0 is greater; min(·, 1.0) likewise
            if (1.0 < ax) ax = 1.0;
            if (0.0 > az) az = 0.0;
            if (1.0 < az) az = 1.0;
            double fx = ax * t.N, fz = az * t.N;
            cx = (int)fx;
            cz = (int)fz;
            if (cx > t.N - 1) cx = t.N - 1;
            if (cz > t.N - 1) cz = t.N - 1;
            u = fx - cx;
            v = fz - cz;
        }

        /// <summary>The surface of a facet at (u, v) of cell (cx, cz): the upper one (u &gt; v) or the other, in validate.py's order.</summary>
        private static double Surface(Tile t, int cx, int cz, bool upper, double u, double v)
        {
            double h00 = Sample(t, cx, cz), h11 = Sample(t, cx + 1, cz + 1), h;
            if (upper)
            {
                double h10 = Sample(t, cx + 1, cz);
                h = h00 + (h10 - h00) * u + (h11 - h10) * v;
            }
            else
            {
                double h01 = Sample(t, cx, cz + 1);
                h = h00 + (h11 - h01) * u + (h01 - h00) * v;
            }
            return t.Y0 + h / MaxSample * t.Sy;
        }

        /// <summary>validate.py's Tile.height.</summary>
        private static double Height(Tile t, double x, double z)
        {
            Locate(t, x, z, out int cx, out int cz, out double u, out double v);
            return Surface(t, cx, cz, u > v, u, v);
        }

        /// <summary>A facet's unit normal: normalize(−∂y/∂x, 1, −∂y/∂z), constant on the facet (the W2 sign-off).</summary>
        private static Double3 FacetNormal(Tile t, int cx, int cz, bool upper)
        {
            double h00 = Sample(t, cx, cz), h11 = Sample(t, cx + 1, cz + 1), gx, gz;
            if (upper)
            {
                double h10 = Sample(t, cx + 1, cz);
                gx = (h10 - h00) * t.K / t.Dx;
                gz = (h11 - h10) * t.K / t.Dz;
            }
            else
            {
                double h01 = Sample(t, cx, cz + 1);
                gx = (h11 - h01) * t.K / t.Dx;
                gz = (h01 - h00) * t.K / t.Dz;
            }
            double inv = 1 / DetMath.Sqrt(gx * gx + 1 + gz * gz);
            return new Double3(-gx * inv, inv, -gz * inv);
        }

        private static bool Contains(Tile t, double x, double z) => t.X0 <= x && x <= t.X1 && t.Z0 <= z && z <= t.Z1;

        private static bool Hole(Tile t, int cx, int cz) => t.Holes != null && t.Holes[(t.N - 1 - cz) * t.N + cx] != 0;

        /// <summary>
        /// The ground's top at (x, z): the highest tile surface whose cell there is no hole (validate.py's ground_height, the
        /// first in manifest order on a tie), with that tile's cell, fractions and height; −1 off every tile or over holes.
        /// </summary>
        private int Top(double x, double z, out int cx, out int cz, out double u, out double v, out double y)
        {
            cx = cz = 0;
            u = v = 0;
            y = double.NegativeInfinity;
            if (!(x >= _ux0 && x <= _ux1 && z >= _uz0 && z <= _uz1))
                return -1; // off the world (NaN too)
            int i = (int)((x - _ux0) / _side), j = (int)((z - _uz0) / _side);
            if (i > _nx - 1) i = _nx - 1;
            if (j > _nz - 1) j = _nz - 1;
            int best = -1, cell = j * _nx + i;
            for (int k = _start[cell]; k < _start[cell + 1]; k++)
            {
                Tile t = _tiles[_list[k]];
                if (!Contains(t, x, z))
                    continue;
                Locate(t, x, z, out int ax, out int az, out double au, out double av);
                if (Hole(t, ax, az))
                    continue;
                double h = Surface(t, ax, az, au > av, au, av);
                if (h > y)
                {
                    y = h;
                    best = _list[k];
                    cx = ax;
                    cz = az;
                    u = au;
                    v = av;
                }
            }
            return best;
        }

        /// <summary>The class at (x, z) of a tile: its nearest class sample (row 0 northmost), rounded half up.</summary>
        private static byte Class(Tile t, double x, double z)
        {
            int m = t.ClassRes - 1;
            double ax = (x - t.X0) / t.Sx, az = (t.Z1 - z) / t.Sz;
            ax = ax > 0 ? ax < 1 ? ax : 1 : 0;
            az = az > 0 ? az < 1 ? az : 1 : 0;
            return t.Classes[(int)(az * m + 0.5) * t.ClassRes + (int)(ax * m + 0.5)];
        }

        // ---- SampleGround ------------------------------------------------------------------------------------------

        public void Sample(double x, double z, out GroundSample g)
        {
            int k = Top(x, z, out int cx, out int cz, out double u, out double v, out double y);
            if (k < 0)
            {
                g = new GroundSample { Height = double.NegativeInfinity, Normal = new Double3(0, 1, 0), Surface = WorldIds.NoSurface, Flags = GroundFlags.Hole };
                return;
            }
            Tile t = _tiles[k];
            g = new GroundSample { Height = y, Normal = FacetNormal(t, cx, cz, u > v), Surface = Class(t, x, z), Flags = GroundFlags.None };
        }

        // ---- StaticContacts ----------------------------------------------------------------------------------------

        /// <summary>
        /// The ground's contact with the capsule swept from `previous` to `current` within `margin` (the Time rule of
        /// IWorldQuery, through ShapeMath.Sweep), `reach` the query's bounds: false for none, or where its point has no ground
        /// (a hole, off the world). `surface` is the class there.
        /// </summary>
        public bool Sweep(in Capsule previous, in Capsule current, double margin, double speed, in Bounds reach, out Near near, out double time,
            out byte surface)
        {
            near = default;
            time = 1;
            surface = WorldIds.NoSurface;
            var s = new TerrainSweep(this, reach, previous, current, current.Radius + margin + Slack, double.PositiveInfinity);
            s = new TerrainSweep(s, FootprintTop(s));
            // Every piece lies at or below the footprint's top: a sweep higher above it than the margin meets none.
            double lowest = DetMath.Min(DetMath.Min(previous.A.Y, previous.B.Y), DetMath.Min(current.A.Y, current.B.Y));
            if (!(lowest - current.Radius - s.Top <= margin) || !ShapeMath.Sweep(s, previous, current, margin, speed, out near, out time))
                return false;
            int k = Top(near.Point.X, near.Point.Z, out _, out _, out _, out _, out _);
            if (k < 0)
                return false;
            surface = Class(_tiles[k], near.Point.X, near.Point.Z);
            return true;
        }

        /// <summary>
        /// The highest ground under the sweep's footprint (each cell's highest corner, hole cells too), −∞ where it holds no
        /// tile: no piece lies above it, a wall's top being its higher tile's edge.
        /// </summary>
        private double FootprintTop(in TerrainSweep s)
        {
            double top = double.NegativeInfinity;
            Candidates(s.Reach, out int from, out int to);
            for (int k = from < 0 ? 0 : from; k < to; k++)
            {
                Tile t = _tiles[from < 0 ? k : _list[k]];
                if (!Cells(t, s.Reach, out int x0, out int x1, out int z0, out int z1))
                    continue;
                bool corridor = (x1 - x0 + 1) * (z1 - z0 + 1) > Corridor;
                int most = -1;
                for (int cz = z0; cz <= z1; cz++)
                {
                    if (!Row(t, s, cz, x0, x1, corridor, out int a, out int b))
                        continue;
                    for (int cx = a; cx <= b; cx++)
                    {
                        int h00 = Sample(t, cx, cz), h10 = Sample(t, cx + 1, cz), h01 = Sample(t, cx, cz + 1), h11 = Sample(t, cx + 1, cz + 1);
                        int cell = h00 > h10 ? h00 : h10, other = h01 > h11 ? h01 : h11;
                        cell = cell > other ? cell : other;
                        most = most > cell ? most : cell;
                    }
                }
                if (most >= 0)
                    top = DetMath.Max(top, t.Y0 + most / MaxSample * t.Sy);
            }
            return top;
        }

        /// <summary>
        /// The tiles a reach may meet: its lookup cell's list, _list[from .. to), when its x–z lies in one cell; else every
        /// tile (from −1). Either way in manifest order.
        /// </summary>
        private void Candidates(in Bounds reach, out int from, out int to)
        {
            from = -1;
            to = _tiles.Length;
            if (!(reach.Min.X >= _ux0 && reach.Max.X <= _ux1 && reach.Min.Z >= _uz0 && reach.Max.Z <= _uz1))
                return;
            int i = (int)((reach.Min.X - _ux0) / _side), j = (int)((reach.Min.Z - _uz0) / _side);
            int i1 = (int)((reach.Max.X - _ux0) / _side), j1 = (int)((reach.Max.Z - _uz0) / _side);
            if (i1 > _nx - 1) i1 = _nx - 1;
            if (j1 > _nz - 1) j1 = _nz - 1;
            if (i != i1 || j != j1)
                return;
            from = _start[j * _nx + i];
            to = _start[j * _nx + i + 1];
        }

        /// <summary>A tile's cells under the reach's x–z, held to the tile; false when the reach misses it.</summary>
        private static bool Cells(Tile t, in Bounds reach, out int x0, out int x1, out int z0, out int z1)
        {
            x0 = x1 = z0 = z1 = 0;
            if (!(t.X0 <= reach.Max.X && reach.Min.X <= t.X1 && t.Z0 <= reach.Max.Z && reach.Min.Z <= t.Z1))
                return false;
            z0 = Index(reach.Min.Z - t.Z0, t.Dz, t.N);
            z1 = Index(reach.Max.Z - t.Z0, t.Dz, t.N);
            x0 = Index(reach.Min.X - t.X0, t.Dx, t.N);
            x1 = Index(reach.Max.X - t.X0, t.Dx, t.N);
            return true;
        }

        /// <summary>Row cz's cells [a, b] within [x0, x1]: on a long reach (`corridor`) only those the sweep's hull reaches within the pad.</summary>
        private static bool Row(Tile t, in TerrainSweep s, int cz, int x0, int x1, bool corridor, out int a, out int b)
        {
            a = x0;
            b = x1;
            if (!corridor)
                return true;
            double za = t.Z0 + cz * t.Dz;
            if (!s.HullX(za - s.Pad, za + t.Dz + s.Pad, out double xa, out double xb))
                return false;
            a = Index(xa - s.Pad - t.X0, t.Dx, t.N);
            b = Index(xb + s.Pad - t.X0, t.Dx, t.N);
            if (a < x0) a = x0;
            if (b > x1) b = x1;
            return a <= b;
        }

        /// <summary>The cell of side `side` holding `offset` from the grid's start, held to [0, count − 1]; NaN goes to 0.</summary>
        private static int Index(double offset, double side, int count)
        {
            double f = offset / side;
            return f >= count - 1 ? count - 1 : f > 0 ? (int)f : 0;
        }

        /// <summary>The visit of each piece of the ground a sweep may meet: a facet, or a wall's triangle (`wall`).</summary>
        internal interface IPieceVisit
        {
            /// <summary>`alone`: a facet of a tile no other overlaps (Tile.Alone), so the ground wherever its footprint is.</summary>
            void Piece(Double3 a, Double3 b, Double3 c, bool wall, bool alone);
        }

        /// <summary>
        /// Every piece whose bounds meet the reach and whose cell meets the footprint: each tile's facets (on a long reach only
        /// the cells the sweep's hull reaches within the pad, row by row; no hole cell), then each wall quad's triangles; in a
        /// fixed order (tiles in manifest order, cells south to north and west to east, the upper facet first; then the
        /// lines and their walls in order).
        /// </summary>
        private void EachPiece<V>(in TerrainSweep s, ref V visit) where V : struct, IPieceVisit
        {
            Bounds reach = s.Reach;
            Candidates(reach, out int from, out int to);
            for (int k = from < 0 ? 0 : from; k < to; k++)
            {
                Tile t = _tiles[from < 0 ? k : _list[k]];
                if (!Cells(t, reach, out int x0, out int x1, out int z0, out int z1))
                    continue;
                bool corridor = (x1 - x0 + 1) * (z1 - z0 + 1) > Corridor;
                for (int cz = z0; cz <= z1; cz++)
                {
                    double za = t.Z0 + cz * t.Dz, zb = za + t.Dz;
                    if (!Row(t, s, cz, x0, x1, corridor, out int a, out int b))
                        continue;
                    for (int cx = a; cx <= b; cx++)
                    {
                        if (Hole(t, cx, cz))
                            continue;
                        double y00 = t.Y0 + Sample(t, cx, cz) / MaxSample * t.Sy, y10 = t.Y0 + Sample(t, cx + 1, cz) / MaxSample * t.Sy;
                        double y01 = t.Y0 + Sample(t, cx, cz + 1) / MaxSample * t.Sy, y11 = t.Y0 + Sample(t, cx + 1, cz + 1) / MaxSample * t.Sy;
                        double xw = t.X0 + cx * t.Dx, xe = xw + t.Dx;
                        var sw = new Double3(xw, y00, za);
                        var ne = new Double3(xe, y11, zb);
                        if (DetMath.Min(y00, DetMath.Min(y10, y11)) <= reach.Max.Y && DetMath.Max(y00, DetMath.Max(y10, y11)) >= reach.Min.Y)
                            visit.Piece(sw, new Double3(xe, y10, za), ne, false, t.Alone); // the upper facet (u > v): south-west, south-east, north-east
                        if (DetMath.Min(y00, DetMath.Min(y01, y11)) <= reach.Max.Y && DetMath.Max(y00, DetMath.Max(y01, y11)) >= reach.Min.Y)
                            visit.Piece(sw, ne, new Double3(xw, y01, zb), false, t.Alone); // the other: south-west, north-east, north-west
                    }
                }
            }
            for (int n = 0; n < _lines.Length; n++)
            {
                ref readonly Line line = ref _lines[n];
                double c0 = line.AlongZ ? reach.Min.X : reach.Min.Z, c1 = line.AlongZ ? reach.Max.X : reach.Max.Z;
                double l0 = line.AlongZ ? reach.Min.Z : reach.Min.X, l1 = line.AlongZ ? reach.Max.Z : reach.Max.X;
                if (line.C < c0 || line.C > c1 || line.To < l0 || line.From > l1)
                    continue;
                for (int w = FirstWall(line, l0); w < line.First + line.Count && _walls[w].L0 <= l1; w++)
                {
                    ref readonly Wall q = ref _walls[w];
                    if (DetMath.Min(q.Bottom0, q.Bottom1) > reach.Max.Y || DetMath.Max(q.Top0, q.Top1) < reach.Min.Y)
                        continue;
                    Double3 b0 = At(line, q.L0, q.Bottom0), b1 = At(line, q.L1, q.Bottom1), t0 = At(line, q.L0, q.Top0), t1 = At(line, q.L1, q.Top1);
                    if (q.Top1 > q.Bottom1)
                        visit.Piece(b0, b1, t1, true, false);
                    if (q.Top0 > q.Bottom0)
                        visit.Piece(b0, q.Top1 > q.Bottom1 ? t1 : b1, t0, true, false);
                }
            }
        }

        private static Double3 At(in Line line, double l, double y) => line.AlongZ ? new Double3(line.C, y, l) : new Double3(l, y, line.C);

        /// <summary>The first wall of a line whose far end reaches `l` (binary search; its walls are ordered and do not overlap).</summary>
        private int FirstWall(in Line line, double l)
        {
            int a = line.First, b = line.First + line.Count;
            while (a < b)
            {
                int m = a + (b - a) / 2;
                if (_walls[m].L1 >= l) b = m;
                else a = m + 1;
            }
            return a;
        }

        /// <summary>The ground's closest approach to the capsule with core p–q and radius r (TerrainSweep.Closest; above).</summary>
        internal void Closest(in TerrainSweep s, Double3 p, Double3 q, double r, out Near near)
        {
            near = default;
            near.WireParam = -1;
            var nearest = new NearestPiece(p, q);
            EachPiece(s, ref nearest);
            if (!(nearest.Best == 0) && (nearest.PAbove || !Below(s, p)) && (nearest.QAbove || !Below(s, q)))
            {
                if (nearest.Best == double.PositiveInfinity)
                {
                    near.Distance = double.PositiveInfinity;
                    return;
                }
                double d = DetMath.Sqrt(nearest.Best);
                near.Distance = d - r;
                near.Point = nearest.OnPiece;
                near.Normal = (nearest.OnCore - nearest.OnPiece) * (1 / d);
                return;
            }
            // Inside: the deepest of its ends and of its crossings of each facet's footprint, each under the ground's own facet
            // there (Top: a higher tile's facets may lie beyond the reach) or behind a step's face if nearer.
            var deepest = new DeepestPoint(this, p, q);
            deepest.Under(0);
            deepest.Under(1);
            EachPiece(s, ref deepest);
            near.Distance = -deepest.Depth - r;
            near.Point = deepest.Point;
            near.Normal = deepest.Normal;
        }

        /// <summary>Whether x lies below the ground (Top; never above the footprint's top): inside it.</summary>
        private bool Below(in TerrainSweep s, Double3 x) => x.Y < s.Top && Top(x.X, x.Z, out _, out _, out _, out _, out double y) >= 0 && x.Y < y;

        /// <summary>The separating-plane bound of the sweep from t: each piece's along its own nearest direction, the least (WireBound's rule).</summary>
        internal double Bound(in TerrainSweep s, in Capsule previous, in Capsule current, double t, out bool apart)
        {
            var bound = new PieceBound(previous, current, t);
            EachPiece(s, ref bound);
            apart = bound.Apart;
            return bound.Bound;
        }

        private struct NearestPiece : IPieceVisit
        {
            private readonly Double3 _p, _q, _lo, _hi;
            public double Best;
            public Double3 OnCore, OnPiece;
            /// <summary>Whether an end lies above a facet of a tile alone under it (so not below the ground, without Top).</summary>
            public bool PAbove, QAbove;

            public NearestPiece(Double3 p, Double3 q)
            {
                _p = p;
                _q = q;
                _lo = new Double3(DetMath.Min(p.X, q.X), DetMath.Min(p.Y, q.Y), DetMath.Min(p.Z, q.Z));
                _hi = new Double3(DetMath.Max(p.X, q.X), DetMath.Max(p.Y, q.Y), DetMath.Max(p.Z, q.Z));
                Best = double.PositiveInfinity;
                OnCore = OnPiece = default;
                PAbove = QAbove = false;
            }

            public void Piece(Double3 a, Double3 b, Double3 c, bool wall, bool alone)
            {
                if (Best == 0)
                    return;
                if (alone && !(PAbove && QAbove))
                {
                    // An end over this facet and above its plane is above the ground: its tile is the only one there.
                    Double3 n = Double3.Cross(b - a, c - a); // down
                    PAbove |= Left(a, b, _p) && Left(b, c, _p) && Left(c, a, _p) && Double3.Dot(_p - a, n) < 0;
                    QAbove |= Left(a, b, _q) && Left(b, c, _q) && Left(c, a, _q) && Double3.Dot(_q - a, n) < 0;
                }
                // The gap between the core's bounds and the piece's is no farther than the piece: one as far as the best so
                // far never replaces it (the first on a tie stays).
                double gx = Gap(_lo.X, _hi.X, a.X, b.X, c.X), gy = Gap(_lo.Y, _hi.Y, a.Y, b.Y, c.Y), gz = Gap(_lo.Z, _hi.Z, a.Z, b.Z, c.Z);
                if (gx * gx + gy * gy + gz * gz >= Best)
                    return;
                double d = Closest(_p, _q, a, b, c, wall, out Double3 onCore, out Double3 onPiece);
                if (d < Best)
                {
                    Best = d;
                    OnCore = onCore;
                    OnPiece = onPiece;
                }
            }
        }

        /// <summary>The gap between [lo, hi] and the three values' span (0 where they meet).</summary>
        private static double Gap(double lo, double hi, double a, double b, double c)
        {
            double min = DetMath.Min(a, DetMath.Min(b, c)), max = DetMath.Max(a, DetMath.Max(b, c));
            return min > hi ? min - hi : lo > max ? lo - max : 0;
        }

        /// <summary>
        /// The squared distance of the core p–q to a piece, with the two points (ShapeMath.SegmentTriangle). A facet (a b c
        /// counter-clockwise from above) whose plane both ends lie above, the nearer end's foot on it, is that foot: the
        /// distance to a plane is linear along the core.
        /// </summary>
        private static double Closest(Double3 p, Double3 q, Double3 a, Double3 b, Double3 c, bool wall, out Double3 onCore, out Double3 onPiece)
        {
            if (!wall)
            {
                Double3 n = Double3.Cross(b - a, c - a); // down: a facet's vertices run counter-clockwise from above
                double dp = Double3.Dot(p - a, n), dq = Double3.Dot(q - a, n);
                if (dp < 0 && dq < 0)
                {
                    bool atP = dp >= dq;
                    Double3 x = atP ? p : q;
                    double d = atP ? dp : dq, nn = Double3.Dot(n, n);
                    Double3 foot = x - n * (d / nn);
                    if (Left(a, b, foot) && Left(b, c, foot) && Left(c, a, foot))
                    {
                        onCore = x;
                        onPiece = foot;
                        return d * (d / nn);
                    }
                }
            }
            return ShapeMath.SegmentTriangle(p, q, a, b, c, out onCore, out onPiece);
        }

        /// <summary>Whether x lies on the left of a→b (or on it) seen from above in x–z.</summary>
        private static bool Left(Double3 a, Double3 b, Double3 x) => (b.X - a.X) * (x.Z - a.Z) - (b.Z - a.Z) * (x.X - a.X) >= 0;

        private struct PieceBound : IPieceVisit
        {
            private readonly Capsule _previous, _current;
            private readonly double _t;
            private readonly Double3 _p, _q;
            public double Bound;
            public bool Apart;

            public PieceBound(in Capsule previous, in Capsule current, double t)
            {
                _previous = previous;
                _current = current;
                _t = t;
                _p = previous.A + (current.A - previous.A) * t;
                _q = previous.B + (current.B - previous.B) * t;
                Bound = double.PositiveInfinity;
                Apart = true;
            }

            public void Piece(Double3 a, Double3 b, Double3 c, bool wall, bool alone)
            {
                if (!Apart && Bound == _t)
                    return; // the core meets a piece at t: no bound
                double d = Closest(_p, _q, a, b, c, wall, out Double3 onCore, out Double3 onPiece);
                if (!(d > 0))
                {
                    Apart = false;
                    Bound = _t;
                    return;
                }
                Double3 n = (onCore - onPiece) * (1 / DetMath.Sqrt(d));
                double support = DetMath.Max(Double3.Dot(a, n), DetMath.Max(Double3.Dot(b, n), Double3.Dot(c, n)));
                double bound = ShapeMath.Bound(n, support, _previous, _current, _t, out bool clear);
                Apart &= clear;
                Bound = DetMath.Min(Bound, bound);
            }
        }

        /// <summary>The deepest point of a core inside the ground: its ends and its crossings of each piece's footprint.</summary>
        private struct DeepestPoint : IPieceVisit
        {
            private readonly Terrain _terrain;
            private readonly Double3 _p, _d;
            public double Depth;
            public Double3 Point, Normal;

            public DeepestPoint(Terrain terrain, Double3 p, Double3 q)
            {
                _terrain = terrain;
                _p = p;
                _d = q - p;
                Depth = double.NegativeInfinity;
                Point = p;
                Normal = new Double3(0, 1, 0);
            }

            /// <summary>The core's point at f under the ground's own facet there (Top), or behind a step's face if nearer.</summary>
            public void Under(double f)
            {
                double depth = _terrain.Depth(_p + _d * f, out Double3 point, out Double3 normal);
                if (depth > Depth)
                {
                    Depth = depth;
                    Point = point;
                    Normal = normal;
                }
            }

            public void Piece(Double3 a, Double3 b, Double3 c, bool wall, bool alone)
            {
                if (wall)
                    return;
                // The core's x–z projection clipped to the facet's (counter-clockwise from above): where it crosses its edges.
                double lo = 0, hi = 1;
                if (!Clip(a, b, ref lo, ref hi) || !Clip(b, c, ref lo, ref hi) || !Clip(c, a, ref lo, ref hi))
                    return;
                if (lo > 0)
                    Under(lo);
                if (hi < 1)
                    Under(hi);
            }

            private bool Clip(Double3 a, Double3 b, ref double lo, ref double hi)
            {
                double ex = b.X - a.X, ez = b.Z - a.Z;
                double g0 = ex * (_p.Z - a.Z) - ez * (_p.X - a.X), gd = ex * _d.Z - ez * _d.X;
                if (gd == 0)
                    return g0 >= 0;
                double f = -g0 / gd;
                if (gd > 0)
                    lo = DetMath.Max(lo, f);
                else
                    hi = DetMath.Min(hi, f);
                return lo <= hi;
            }
        }

        /// <summary>
        /// How deep x lies inside the ground: the distance to its facet's plane under it (the top's normal), or to a wall
        /// face behind it if nearer (that face's normal); with the point on that surface. −∞ where it has no ground.
        /// </summary>
        private double Depth(Double3 x, out Double3 point, out Double3 normal)
        {
            int k = Top(x.X, x.Z, out int cx, out int cz, out double u, out double v, out double y);
            point = x;
            normal = new Double3(0, 1, 0);
            if (k < 0)
                return double.NegativeInfinity;
            normal = FacetNormal(_tiles[k], cx, cz, u > v);
            double depth = (y - x.Y) * normal.Y;
            point = x + normal * depth;
            Walls(x, ref depth, ref point, ref normal);
            return depth;
        }

        /// <summary>A point `depth` inside the ground: a step's face behind it, if nearer, with that face's normal.</summary>
        private void Walls(Double3 x, ref double depth, ref Double3 point, ref Double3 normal)
        {
            if (!(depth > 0))
                return;
            for (int n = 0; n < _lines.Length; n++)
            {
                ref readonly Line line = ref _lines[n];
                double across = (line.AlongZ ? x.X : x.Z) - line.C, along = line.AlongZ ? x.Z : x.X;
                if (!(DetMath.Abs(across) < depth) || along + depth < line.From || along - depth > line.To)
                    continue;
                for (int w = FirstWall(line, along - depth); w < line.First + line.Count && _walls[w].L0 <= along + depth; w++)
                {
                    ref readonly Wall q = ref _walls[w];
                    if (across * q.Side > 0)
                        continue; // x lies on the lower side: this face is no boundary of its column
                    Double3 b0 = At(line, q.L0, q.Bottom0), b1 = At(line, q.L1, q.Bottom1), t0 = At(line, q.L0, q.Top0), t1 = At(line, q.L1, q.Top1);
                    Double3 face = line.AlongZ ? new Double3(q.Side, 0, 0) : new Double3(0, 0, q.Side);
                    if (q.Top1 > q.Bottom1)
                        Nearer(x, b0, b1, t1, face, ref depth, ref point, ref normal);
                    if (q.Top0 > q.Bottom0)
                        Nearer(x, b0, q.Top1 > q.Bottom1 ? t1 : b1, t0, face, ref depth, ref point, ref normal);
                }
            }
        }

        private static void Nearer(Double3 x, Double3 a, Double3 b, Double3 c, Double3 face, ref double depth, ref Double3 point, ref Double3 normal)
        {
            Double3 on = ShapeMath.PointTriangle(x, a, b, c), v = x - on;
            double d = DetMath.Sqrt(Double3.Dot(v, v));
            if (d < depth)
            {
                depth = d;
                point = on;
                normal = face;
            }
        }

        // ---- Raycast -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Where the ray o + t·d (d unit) first enters the ground within `limit`: a facet from above or a wall from its lower
        /// side, its normal and the class there; false for none, for a ray that starts inside the ground, and for a hit whose
        /// point has no ground (a hole's edge).
        /// </summary>
        public bool Ray(Double3 o, Double3 d, double limit, out double hit, out Double3 normal, out byte surface)
        {
            hit = double.PositiveInfinity;
            normal = default;
            surface = WorldIds.NoSurface;
            if (Top(o.X, o.Z, out _, out _, out _, out _, out double y) >= 0 && o.Y < y)
                return false;
            double best = limit;
            bool found = false;
            Double3 end = o + d * limit;
            Candidates(new Bounds(new Double3(DetMath.Min(o.X, end.X), 0, DetMath.Min(o.Z, end.Z)), new Double3(DetMath.Max(o.X, end.X), 0, DetMath.Max(o.Z, end.Z))),
                out int from, out int to);
            for (int k = from < 0 ? 0 : from; k < to; k++)
                CastTile(_tiles[from < 0 ? k : _list[k]], o, d, ref best, ref found, ref normal);
            for (int n = 0; n < _lines.Length; n++)
                CastLine(_lines[n], o, d, ref best, ref found, ref normal);
            if (!found)
                return false;
            Double3 at = o + d * best;
            int top = Top(at.X, at.Z, out _, out _, out _, out _, out _);
            if (top < 0)
                return false;
            hit = best;
            surface = Class(_tiles[top], at.X, at.Z);
            return true;
        }

        /// <summary>A tile's first facet the ray enters from above before `best`: a walk through the cells it crosses.</summary>
        private static void CastTile(Tile t, Double3 o, Double3 d, ref double best, ref bool found, ref Double3 normal)
        {
            double ta = 0, tb = best;
            if (!Slab(o.X, d.X, t.X0, t.X1, ref ta, ref tb) || !Slab(o.Z, d.Z, t.Z0, t.Z1, ref ta, ref tb))
                return;
            Locate(t, o.X + d.X * ta, o.Z + d.Z * ta, out int cx, out int cz, out _, out _);
            int stepX = d.X > 0 ? 1 : d.X < 0 ? -1 : 0, stepZ = d.Z > 0 ? 1 : d.Z < 0 ? -1 : 0;
            double t0 = ta;
            while (t0 <= best)
            {
                double nx = stepX > 0 ? (t.X0 + (cx + 1) * t.Dx - o.X) / d.X : stepX < 0 ? (t.X0 + cx * t.Dx - o.X) / d.X : double.PositiveInfinity;
                double nz = stepZ > 0 ? (t.Z0 + (cz + 1) * t.Dz - o.Z) / d.Z : stepZ < 0 ? (t.Z0 + cz * t.Dz - o.Z) / d.Z : double.PositiveInfinity;
                double t1 = DetMath.Min(DetMath.Min(nx, nz), tb);
                if (!(t1 >= t0))
                    t1 = t0;
                if (!Hole(t, cx, cz))
                {
                    int cell = CastCell(t, cx, cz, o, d, t0, t1, ref best, ref normal);
                    if (cell > 0)
                    {
                        found = true;
                        return; // the tile's first entry: its later ones are inside the ground
                    }
                    if (cell < 0)
                        return; // under this tile's surface: inside the ground, entered through another piece
                }
                if (t1 >= tb)
                    return;
                if (nx <= nz) cx += stepX;
                if (nz <= nx) cz += stepZ;
                if (cx < 0 || cx >= t.N || cz < 0 || cz >= t.N)
                    return;
                t0 = t1;
            }
        }

        /// <summary>Clips [ta, tb] to the slab lo ≤ o + d·t ≤ hi along one axis; false when nothing is left.</summary>
        private static bool Slab(double o, double d, double lo, double hi, ref double ta, ref double tb)
        {
            if (d == 0)
                return o >= lo && o <= hi && ta <= tb;
            double a = (lo - o) / d, b = (hi - o) / d;
            if (a > b)
            {
                double s = a;
                a = b;
                b = s;
            }
            ta = DetMath.Max(ta, a);
            tb = DetMath.Min(tb, b);
            return ta <= tb;
        }

        /// <summary>
        /// The ray over cell (cx, cz) from t0 to t1: split where it crosses the diagonal, each part over one facet. 1 when it
        /// enters a facet from above before `best` (then `best` and `normal` are its), −1 when it starts a part under it.
        /// </summary>
        private static int CastCell(Tile t, int cx, int cz, Double3 o, Double3 d, double t0, double t1, ref double best, ref Double3 normal)
        {
            Fractions(t, cx, cz, o, d, t0, out double u0, out double v0);
            Fractions(t, cx, cz, o, d, t1, out double u1, out double v1);
            double w0 = u0 - v0, w1 = u1 - v1;
            if (w0 > 0 && w1 < 0 || w0 < 0 && w1 > 0)
            {
                double tc = t0 + w0 / (w0 - w1) * (t1 - t0);
                int first = CastFacet(t, cx, cz, w0 > 0, o, d, t0, tc, ref best, ref normal);
                return first != 0 ? first : CastFacet(t, cx, cz, w1 > 0, o, d, tc, t1, ref best, ref normal);
            }
            return CastFacet(t, cx, cz, w0 + w1 > 0, o, d, t0, t1, ref best, ref normal);
        }

        /// <summary>The ray's cell fractions at t, as Locate makes them (unclamped to the cell: the facet's plane extends).</summary>
        private static void Fractions(Tile t, int cx, int cz, Double3 o, Double3 d, double at, out double u, out double v)
        {
            double ax = (o.X + d.X * at - t.X0) / t.Sx, az = (o.Z + d.Z * at - t.Z0) / t.Sz;
            if (0.0 > ax) ax = 0.0;
            if (1.0 < ax) ax = 1.0;
            if (0.0 > az) az = 0.0;
            if (1.0 < az) az = 1.0;
            u = ax * t.N - cx;
            v = az * t.N - cz;
        }

        private static int CastFacet(Tile t, int cx, int cz, bool upper, Double3 o, Double3 d, double ta, double tb, ref double best, ref Double3 normal)
        {
            if (ta > best)
                return 0;
            Fractions(t, cx, cz, o, d, ta, out double ua, out double va);
            Fractions(t, cx, cz, o, d, tb, out double ub, out double vb);
            double fa = o.Y + d.Y * ta - Surface(t, cx, cz, upper, ua, va), fb = o.Y + d.Y * tb - Surface(t, cx, cz, upper, ub, vb);
            if (fa < 0)
                return -1;
            if (!(fb <= 0 && fa > fb))
                return 0;
            double at = ta + fa / (fa - fb) * (tb - ta);
            if (!(at <= best))
                return 0;
            best = at;
            normal = FacetNormal(t, cx, cz, upper);
            return 1;
        }

        /// <summary>A line's wall the ray enters from the lower side before `best`.</summary>
        private void CastLine(in Line line, Double3 o, Double3 d, ref double best, ref bool found, ref Double3 normal)
        {
            double rate = line.AlongZ ? d.X : d.Z;
            if (rate == 0)
                return;
            double at = (line.C - (line.AlongZ ? o.X : o.Z)) / rate;
            if (!(at >= 0 && at <= best))
                return;
            double l = line.AlongZ ? o.Z + d.Z * at : o.X + d.X * at;
            if (l < line.From || l > line.To)
                return;
            int w = FirstWall(line, l);
            if (w >= line.First + line.Count || _walls[w].L0 > l)
                return;
            ref readonly Wall q = ref _walls[w];
            if (!(rate * q.Side < 0))
                return; // leaving the higher side, or along the face
            double f = (l - q.L0) / (q.L1 - q.L0), y = o.Y + d.Y * at;
            if (y >= q.Bottom0 + (q.Bottom1 - q.Bottom0) * f && y <= q.Top0 + (q.Top1 - q.Top0) * f)
            {
                best = at;
                found = true;
                normal = line.AlongZ ? new Double3(q.Side, 0, 0) : new Double3(0, 0, q.Side);
            }
        }

        // ---- The step lines (load time) ------------------------------------------------------------------------------

        /// <summary>
        /// The walls: across every edge between two regions of the tiles' edge grid that both have ground, both sides' heights
        /// at every breakpoint of either side's tiles (their grid lines and diagonals' crossings of the edge), split where the
        /// higher side changes; a quad wherever they differ. Then grouped by line, and the steps (runs of one pair of rings).
        /// </summary>
        private void BuildWalls(out Wall[] walls, out Line[] lines, out TerrainStep[] steps)
        {
            var xs = new SortedSet<double>();
            var zs = new SortedSet<double>();
            foreach (Tile t in _tiles)
            {
                xs.Add(t.X0);
                xs.Add(t.X1);
                zs.Add(t.Z0);
                zs.Add(t.Z1);
            }
            double[] ex = new double[xs.Count], ez = new double[zs.Count];
            xs.CopyTo(ex);
            zs.CopyTo(ez);
            var found = new List<(bool AlongZ, double C, Wall W, string Rings)>();
            for (int axis = 0; axis < 2; axis++)
            {
                bool alongZ = axis == 0;
                double[] cs = alongZ ? ex : ez, ls = alongZ ? ez : ex;
                for (int i = 1; i + 1 < cs.Length; i++)
                    for (int j = 0; j + 1 < ls.Length; j++)
                        Edge(alongZ, cs[i], 0.5 * (cs[i - 1] + cs[i]), 0.5 * (cs[i] + cs[i + 1]), ls[j], ls[j + 1], found);
            }
            found.Sort(new WallOrder()); // an instance, not a lambda: no static delegate cache in the core (StaticInitTests)
            walls = new Wall[found.Count];
            var lineList = new List<Line>();
            var stepList = new List<TerrainStep>();
            for (int k = 0; k < found.Count; k++)
            {
                walls[k] = found[k].W;
                bool newLine = k == 0 || found[k].AlongZ != found[k - 1].AlongZ || found[k].C != found[k - 1].C;
                if (newLine)
                    lineList.Add(new Line { AlongZ = found[k].AlongZ, C = found[k].C, From = walls[k].L0, To = walls[k].L1, First = k, Count = 1 });
                else
                {
                    Line last = lineList[lineList.Count - 1];
                    last.To = DetMath.Max(last.To, walls[k].L1);
                    last.Count++;
                    lineList[lineList.Count - 1] = last;
                }
                double least = DetMath.Min(walls[k].Top0 - walls[k].Bottom0, walls[k].Top1 - walls[k].Bottom1);
                double most = DetMath.Max(walls[k].Top0 - walls[k].Bottom0, walls[k].Top1 - walls[k].Bottom1);
                if (newLine || stepList[stepList.Count - 1].Rings != found[k].Rings)
                    stepList.Add(new TerrainStep(found[k].AlongZ, found[k].C, walls[k].L0, walls[k].L1, least, most, found[k].Rings));
                else
                {
                    TerrainStep s = stepList[stepList.Count - 1];
                    stepList[stepList.Count - 1] = new TerrainStep(s.AlongZ, s.C, s.From, walls[k].L1, DetMath.Min(s.Least, least), DetMath.Max(s.Most, most), s.Rings);
                }
            }
            lines = lineList.ToArray();
            steps = stepList.ToArray();
        }

        /// <summary>The walls in line order: the lines x = C first, by C, then z = C; along each line by L0.</summary>
        private sealed class WallOrder : IComparer<(bool AlongZ, double C, Wall W, string Rings)>
        {
            public int Compare((bool AlongZ, double C, Wall W, string Rings) a, (bool AlongZ, double C, Wall W, string Rings) b) =>
                a.AlongZ != b.AlongZ ? (a.AlongZ ? -1 : 1) : a.C != b.C ? a.C.CompareTo(b.C) : a.W.L0.CompareTo(b.W.L0);
        }

        /// <summary>The walls across the edge at `c` between the region centred at `before` and the one at `after`, from la to lb.</summary>
        private void Edge(bool alongZ, double c, double before, double after, double la, double lb, List<(bool, double, Wall, string)> found)
        {
            double mid = 0.5 * (la + lb);
            var low = new List<int>();
            var high = new List<int>();
            for (int k = 0; k < _tiles.Length; k++)
            {
                Tile t = _tiles[k];
                if (alongZ ? Contains(t, before, mid) : Contains(t, mid, before))
                    low.Add(k);
                if (alongZ ? Contains(t, after, mid) : Contains(t, mid, after))
                    high.Add(k);
            }
            if (low.Count == 0 || high.Count == 0)
                return; // the world's edge: off the world is a hole, never a wall
            var at = new List<double> { la, lb };
            foreach (int k in low)
                Breaks(_tiles[k], alongZ, c, la, lb, at);
            foreach (int k in high)
                Breaks(_tiles[k], alongZ, c, la, lb, at);
            at.Sort();
            double pa = double.NaN, ga = 0, gb = 0;
            int wa = -1, wb = -1;
            foreach (double l in at)
            {
                if (l == pa)
                    continue;
                double g0 = SideHeight(low, alongZ, c, l, out int w0), g1 = SideHeight(high, alongZ, c, l, out int w1);
                if (!double.IsNaN(pa))
                {
                    if (w0 != wa || w1 != wb)
                        throw new FormatException($"map.json: the highest tile changes along a step at {(alongZ ? "x" : "z")} = {c}, {l} (overlapping tiles whose surfaces cross are not supported)");
                    Quads(alongZ, c, pa, l, ga, gb, g0, g1, w0, w1, found);
                }
                pa = l;
                ga = g0;
                gb = g1;
                wa = w0;
                wb = w1;
            }
        }

        /// <summary>Adds a tile's breakpoints along the line in (la, lb): its grid lines, and where its cells' diagonals cross the line.</summary>
        private static void Breaks(Tile t, bool alongZ, double c, double la, double lb, List<double> at)
        {
            double o = alongZ ? t.Z0 : t.X0, step = alongZ ? t.Dz : t.Dx;
            int k0 = (int)((la - o) / step), k1 = (int)((lb - o) / step) + 1;
            if (k0 < 0) k0 = 0;
            if (k1 > t.N) k1 = t.N;
            double f = alongZ ? (c - t.X0) / t.Dx : (c - t.Z0) / t.Dz;
            int col = (int)f;
            double frac = f - col; // the diagonal crosses the line at this fraction of each cell along it
            for (int k = k0; k <= k1; k++)
            {
                double l = o + k * step;
                if (l > la && l < lb)
                    at.Add(l);
                if (frac > 0 && col < t.N && k < t.N)
                {
                    l = o + (k + frac) * step;
                    if (l > la && l < lb)
                        at.Add(l);
                }
            }
        }

        /// <summary>One side's ground at the line: its tiles' highest at (c, l), and that tile.</summary>
        private double SideHeight(List<int> side, bool alongZ, double c, double l, out int winner)
        {
            double best = double.NegativeInfinity;
            winner = -1;
            foreach (int k in side)
            {
                double h = alongZ ? Height(_tiles[k], c, l) : Height(_tiles[k], l, c);
                if (h > best)
                {
                    best = h;
                    winner = k;
                }
            }
            return best;
        }

        /// <summary>The wall between pa and pb: the before side ga (at pa) and g0 (at pb), the after side gb and g1; split where they cross.</summary>
        private void Quads(bool alongZ, double c, double pa, double pb, double ga, double gb, double g0, double g1, int w0, int w1,
            List<(bool, double, Wall, string)> found)
        {
            double da = ga - gb, db = g0 - g1;
            if (da == 0 && db == 0)
                return;
            string rings = string.CompareOrdinal(_tiles[w0].Ring, _tiles[w1].Ring) <= 0 ? _tiles[w0].Ring + "|" + _tiles[w1].Ring : _tiles[w1].Ring + "|" + _tiles[w0].Ring;
            if (da > 0 && db < 0 || da < 0 && db > 0)
            {
                double f = da / (da - db), pm = pa + (pb - pa) * f, gm = ga + (g0 - ga) * f;
                Add(alongZ, c, pa, pm, ga, gb, gm, gm, da, w0, w1, rings, found);
                Add(alongZ, c, pm, pb, gm, gm, g0, g1, db, w0, w1, rings, found);
                return;
            }
            Add(alongZ, c, pa, pb, ga, gb, g0, g1, da + db, w0, w1, rings, found);
        }

        private static void Add(bool alongZ, double c, double l0, double l1, double before0, double after0, double before1, double after1,
            double sign, int w0, int w1, string rings, List<(bool, double, Wall, string)> found)
        {
            if (!(l1 > l0))
                return;
            found.Add((alongZ, c, new Wall
            {
                L0 = l0, L1 = l1,
                Bottom0 = DetMath.Min(before0, after0), Top0 = DetMath.Max(before0, after0),
                Bottom1 = DetMath.Min(before1, after1), Top1 = DetMath.Max(before1, after1),
                Side = sign > 0 ? 1 : -1,
                High = sign > 0 ? w0 : w1,
            }, rings));
        }

        // ---- JSON fields -------------------------------------------------------------------------------------------

        private static JsonNode Need(JsonNode o, string key, JsonKind kind, string where)
        {
            JsonNode n = o.Member(key);
            if (n == null || n.Kind != kind)
                throw new FormatException(where + ": " + key + " is missing or not a" + (kind == JsonKind.Array || kind == JsonKind.Object ? "n " : " ") + kind.ToString().ToLowerInvariant());
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
    }

    /// <summary>
    /// The terrain's sweep for ShapeMath.Sweep (Terrain.Closest and Terrain.Bound): the query's reach, and its core ends at
    /// both poses, whose hull in x–z grown by Pad holds every point of the sweep within its margin.
    /// </summary>
    internal readonly struct TerrainSweep : IClosest
    {
        private readonly Terrain _terrain;
        public readonly Bounds Reach;
        /// <summary>The radius, the margin and the footprint's slack: no piece beyond the hull grown by it is within the margin.</summary>
        public readonly double Pad;
        /// <summary>The highest ground under the footprint (Terrain.FootprintTop), m.</summary>
        public readonly double Top;
        private readonly Double3 _a, _b, _c, _d;

        public TerrainSweep(Terrain terrain, in Bounds reach, in Capsule previous, in Capsule current, double pad, double top)
        {
            _terrain = terrain;
            Reach = reach;
            Pad = pad;
            Top = top;
            _a = previous.A;
            _b = previous.B;
            _c = current.A;
            _d = current.B;
        }

        public TerrainSweep(in TerrainSweep s, double top)
        {
            this = s;
            Top = top;
        }

        public void Closest(Double3 p, Double3 q, double r, out Near near) => _terrain.Closest(this, p, q, r, out near);

        /// <summary>
        /// How far the swept capsule stays from the ground from t on: its lowest core end over the rest of the step above the
        /// footprint's top, less the radius (every piece within the pad lies under that top), and never more than the pad less
        /// the radius (a piece beyond the footprint is farther).
        /// </summary>
        public double Clearance(in Capsule previous, in Capsule current, double t)
        {
            double lowest = DetMath.Min(DetMath.Min(previous.A.Y + (current.A.Y - previous.A.Y) * t, previous.B.Y + (current.B.Y - previous.B.Y) * t),
                DetMath.Min(current.A.Y, current.B.Y));
            return DetMath.Min(lowest - Top - current.Radius, Pad - current.Radius);
        }

        /// <summary>
        /// The separating-plane bound (Terrain.Bound), after a cheaper proof: the distance moves at most the core ends' larger
        /// move per unit of t, so `near` (a closest approach at a pose in [t, 1]) farther than that move over the rest of the
        /// step, and TouchDistance and its slack, is apart for the rest of it. That is the advancement's own conclusion, so the
        /// result is the same; it only skips the pieces' passes for a part sliding within the margin.
        /// </summary>
        public double Bound(in Capsule previous, in Capsule current, double t, in Near near, out bool apart)
        {
            double speed = DetMath.Max((current.A - previous.A).Length(), (current.B - previous.B).Length());
            if (near.Distance - speed * (1 - t) > ShapeMath.TouchDistance + ShapeMath.ClearSlack)
            {
                apart = true;
                return double.PositiveInfinity;
            }
            return _terrain.Bound(this, previous, current, t, out apart);
        }

        public bool Flat => false;

        /// <summary>The x range of the four ends' hull within za ≤ z ≤ zb: of the six segments between them, clipped to the strip.</summary>
        public bool HullX(double za, double zb, out double xa, out double xb)
        {
            xa = double.PositiveInfinity;
            xb = double.NegativeInfinity;
            Clip(_a, _b, za, zb, ref xa, ref xb);
            Clip(_a, _c, za, zb, ref xa, ref xb);
            Clip(_a, _d, za, zb, ref xa, ref xb);
            Clip(_b, _c, za, zb, ref xa, ref xb);
            Clip(_b, _d, za, zb, ref xa, ref xb);
            Clip(_c, _d, za, zb, ref xa, ref xb);
            return xa <= xb;
        }

        private static void Clip(Double3 a, Double3 b, double za, double zb, ref double xa, ref double xb)
        {
            double lo = DetMath.Min(a.Z, b.Z), hi = DetMath.Max(a.Z, b.Z);
            if (hi < za || lo > zb)
                return;
            if (!(hi > lo))
            {
                xa = DetMath.Min(xa, DetMath.Min(a.X, b.X));
                xb = DetMath.Max(xb, DetMath.Max(a.X, b.X));
                return;
            }
            double z0 = DetMath.Max(lo, za), z1 = DetMath.Min(hi, zb);
            double x0 = a.X + (b.X - a.X) * ((z0 - a.Z) / (b.Z - a.Z)), x1 = a.X + (b.X - a.X) * ((z1 - a.Z) / (b.Z - a.Z));
            xa = DetMath.Min(xa, DetMath.Min(x0, x1));
            xb = DetMath.Max(xb, DetMath.Max(x0, x1));
        }
    }

    /// <summary>
    /// A step of a terrain (Terrain, the step lines): a run of walls along the line x = C (AlongZ) or z = C, from From to To,
    /// between one pair of rings ("far|surround" for the farmland's ledge, "far|far" for a far seam), its face from Least to
    /// Most high, m. For the records and the tests; the queries read the walls themselves.
    /// </summary>
    public readonly struct TerrainStep
    {
        public readonly bool AlongZ;
        public readonly double C, From, To, Least, Most;
        public readonly string Rings;

        public TerrainStep(bool alongZ, double c, double from, double to, double least, double most, string rings)
        {
            AlongZ = alongZ;
            C = c;
            From = from;
            To = to;
            Least = least;
            Most = most;
            Rings = rings;
        }
    }
}
