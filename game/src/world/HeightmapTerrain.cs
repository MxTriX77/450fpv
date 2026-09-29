using System;
using System.Collections.Generic;
using Godot;

/// Renders a map heightfield and gives it collision (D-009). Must sit at the world origin, unrotated and unscaled.
///
/// Rendering: the heights are one R16 texture. Each frame a quadtree picks square patches of one shared 32 × 32 grid
/// mesh, at 1 sample spacing near the camera and doubling with distance. The vertex shader puts every vertex exactly on
/// a height sample, so nothing is duplicated in VRAM. Skirts reaching below the lowest sample hide the cracks between
/// patches of different spacing.
/// Collision: HeightMapShape3D chunks of 256 × 256 cells, the same samples and triangles as the render at full detail.
/// Holes (map format 1.1): the fragment shader discards the surface over a hole cell, in the depth and shadow passes
/// too, and the collision replaces the quads that overlap one, see SetHoles.
public partial class HeightmapTerrain : Node3D
{
    const int PatchCells = 32;
    const int CollisionCells = 256;
    const float SplitDistance = 2f; // a patch splits into four while the camera is closer than this many patch sizes

    WorldQuery _holes; // the hole layer's owner, or null when the map has none
    int _samples;
    float _resolution;
    float _half;
    float _skirtBottom;
    float[][] _min; // per quadtree level (0 = leaf patches), per node row-major
    float[][] _max;
    int[] _nodesPerSide;
    ArrayMesh _patch;
    ShaderMaterial _material;
    readonly List<MeshInstance3D> _pool = new();
    List<long> _selected = new();
    List<long> _shown = new();

    /// The patches' shader material (heightmap_terrain.gdshader), set by Build.
    public ShaderMaterial Material => _material;

    /// The heights (R16, sample / 65535), set by Build; the surface indices (R8) and the cover (RGBA8, mipmapped), one
    /// texel per cell, set by SetSurfaces. The far cover draws from the same textures.
    public ImageTexture HeightTexture { get; private set; }
    public ImageTexture SurfaceTexture { get; private set; }
    public ImageTexture CoverTexture { get; private set; }
    /// The hole layer (R8, 255 = hole), one texel per surface cell, set by Build; null without holes. The far cover
    /// reads it too, so nothing is drawn over a hole.
    public ImageTexture HoleTexture { get; private set; }
    /// The lowest and highest height sample, m, set by Build.
    public float Lowest { get; private set; }
    public float Highest { get; private set; }

    /// `r16` is the raw little-endian heightfield of `samples` × `samples`; height = offsetM + sample × scaleM. `holes`
    /// is the world whose hole layer takes quads out of the ground, or null for terrain without holes.
    public void Build(byte[] r16, int samples, float resolution, float offsetM, float scaleM, WorldQuery holes = null)
    {
        _holes = holes != null && holes.HasHoles ? holes : null;
        _samples = samples;
        _resolution = resolution;
        _half = (samples - 1) * resolution / 2f;
        var heights = new float[samples * samples];
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = offsetM + (r16[2 * i] | r16[2 * i + 1] << 8) * scaleM;
            lowest = Math.Min(lowest, heights[i]);
            highest = Math.Max(highest, heights[i]);
        }
        (Lowest, Highest) = (lowest, highest);
        _skirtBottom = lowest - 1f;
        BuildPyramid(heights);

        HeightTexture = ImageTexture.CreateFromImage(Image.CreateFromData(samples, samples, false, Image.Format.R16, r16));
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/world/heightmap_terrain.gdshader") };
        _material.SetShaderParameter("heights", HeightTexture);
        _material.SetShaderParameter("height_offset", offsetM);
        _material.SetShaderParameter("height_range", scaleM * 65535f);
        _material.SetShaderParameter("resolution", resolution);
        _material.SetShaderParameter("half_size", _half);
        _material.SetShaderParameter("samples", samples);
        _material.SetShaderParameter("skirt_bottom", _skirtBottom);
        SetHoles();
        _patch = PatchMesh();
        BuildCollision(heights);
    }

    /// Uploads the hole layer for the fragment shader's discard, on the surface-cell grid.
    void SetHoles()
    {
        if (_holes == null)
            return;
        int cells = _holes.Cells;
        var bytes = new byte[cells * cells];
        for (int row = 0; row < cells; row++)
        {
            for (int column = 0; column < cells; column++)
                bytes[row * cells + column] = _holes.HoleCell(row, column) ? (byte)255 : (byte)0;
        }
        HoleTexture = ImageTexture.CreateFromImage(Image.CreateFromData(cells, cells, false, Image.Format.R8, bytes));
        _material.SetShaderParameter("holes", HoleTexture);
        _material.SetShaderParameter("hole_resolution", (float)_holes.CellResolution);
        _material.SetShaderParameter("hole_cells", cells);
        _material.SetShaderParameter("use_holes", true);
    }

    /// Draws the ground with each surface's look in place of the flat albedo. `ids` and `coverRgba` are the surface and
    /// cover layers, `cells` × `cells` cells of `cellResolution` m, row-major from the north-west corner. Call after Build.
    public void SetSurfaces(ReadOnlySpan<byte> ids, ReadOnlySpan<byte> coverRgba, int cells, float cellResolution, SurfaceLook look)
    {
        SurfaceTexture = ImageTexture.CreateFromImage(Image.CreateFromData(cells, cells, false, Image.Format.R8, ids));
        Image cover = Image.CreateFromData(cells, cells, false, Image.Format.Rgba8, coverRgba);
        cover.GenerateMipmaps(); // straw streaks and field edges filter out with distance instead of aliasing
        CoverTexture = ImageTexture.CreateFromImage(cover);
        _material.SetShaderParameter("surface_ids", SurfaceTexture);
        _material.SetShaderParameter("cover", CoverTexture);
        _material.SetShaderParameter("surface_params", look.Params);
        _material.SetShaderParameter("surface_resolution", cellResolution);
        _material.SetShaderParameter("surface_cells", cells);
        _material.SetShaderParameter("set_albedo", look.Albedo);
        _material.SetShaderParameter("set_normal", look.Normal);
        _material.SetShaderParameter("set_height", look.Height);
        _material.SetShaderParameter("set_ao", look.Occlusion);
        _material.SetShaderParameter("straw_layer", (float)look.StrawLayer);
        _material.SetShaderParameter("straw_tile", look.StrawTile);
        _material.SetShaderParameter("straw_tint", look.StrawTint);
        _material.SetShaderParameter("litter_layer", (float)look.LitterLayer);
        _material.SetShaderParameter("litter_tile", look.LitterTile);
        _material.SetShaderParameter("litter_tint", look.LitterTint);
        _material.SetShaderParameter("use_surfaces", true);
    }

    public override void _Process(double delta)
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (_patch == null || camera == null)
            return;
        _selected.Clear();
        Select(_min.Length - 1, 0, 0, camera.GlobalPosition);
        if (SameSelection())
            return;

        for (int k = 0; k < _selected.Count; k++)
        {
            if (k == _pool.Count)
            {
                var patch = new MeshInstance3D { Mesh = _patch, MaterialOverride = _material };
                AddChild(patch);
                _pool.Add(patch);
            }
            long key = _selected[k];
            int level = (int)(key >> 40), nz = (int)(key >> 20) & 0xFFFFF, nx = (int)key & 0xFFFFF;
            int i = nz * _nodesPerSide[level] + nx;
            float size = NodeSize(level);
            MeshInstance3D instance = _pool[k];
            instance.Transform = new Transform3D(
                new Basis(new Vector3(size, 0, 0), Vector3.Up, new Vector3(0, 0, size)),
                new Vector3(-_half + nx * size, 0, -_half + nz * size));
            instance.CustomAabb = new Aabb(new Vector3(0, _skirtBottom, 0), new Vector3(1, _max[level][i] - _skirtBottom, 1));
            instance.Visible = true;
        }
        for (int k = _selected.Count; k < _pool.Count; k++)
            _pool[k].Visible = false;
        (_shown, _selected) = (_selected, _shown);
    }

    float NodeSize(int level) => PatchCells * (1 << level) * _resolution;

    void Select(int level, int nx, int nz, Vector3 camera)
    {
        int n = _nodesPerSide[level];
        if (nx >= n || nz >= n)
            return;
        float size = NodeSize(level);
        int i = nz * n + nx;
        var min = new Vector3(-_half + nx * size, _min[level][i], -_half + nz * size);
        var max = new Vector3(min.X + size, _max[level][i], min.Z + size);
        if (level > 0 && camera.DistanceTo(camera.Clamp(min, max)) < SplitDistance * size)
        {
            for (int k = 0; k < 4; k++)
                Select(level - 1, 2 * nx + (k & 1), 2 * nz + (k >> 1), camera);
        }
        else
        {
            _selected.Add((long)level << 40 | (long)nz << 20 | (long)nx);
        }
    }

    bool SameSelection()
    {
        if (_selected.Count != _shown.Count)
            return false;
        for (int k = 0; k < _selected.Count; k++)
        {
            if (_selected[k] != _shown[k])
                return false;
        }
        return true;
    }

    /// Height range of every quadtree node, so patches get tight culling boxes and the split test sees relief.
    void BuildPyramid(float[] heights)
    {
        int cells = _samples - 1;
        int leaves = (cells + PatchCells - 1) / PatchCells;
        int levels = 1;
        while (1 << (levels - 1) < leaves)
            levels++;
        _min = new float[levels][];
        _max = new float[levels][];
        _nodesPerSide = new int[levels];
        for (int level = 0; level < levels; level++)
        {
            int n = (leaves + (1 << level) - 1) >> level;
            _nodesPerSide[level] = n;
            _min[level] = new float[n * n];
            _max[level] = new float[n * n];
            for (int nz = 0; nz < n; nz++)
            {
                for (int nx = 0; nx < n; nx++)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    if (level == 0)
                    {
                        for (int r = nz * PatchCells; r <= Math.Min((nz + 1) * PatchCells, cells); r++)
                        {
                            for (int c = nx * PatchCells; c <= Math.Min((nx + 1) * PatchCells, cells); c++)
                            {
                                lo = Math.Min(lo, heights[r * _samples + c]);
                                hi = Math.Max(hi, heights[r * _samples + c]);
                            }
                        }
                    }
                    else
                    {
                        int child = _nodesPerSide[level - 1];
                        for (int k = 0; k < 4; k++)
                        {
                            int cx = 2 * nx + (k & 1), cz = 2 * nz + (k >> 1);
                            if (cx < child && cz < child)
                            {
                                lo = Math.Min(lo, _min[level - 1][cz * child + cx]);
                                hi = Math.Max(hi, _max[level - 1][cz * child + cx]);
                            }
                        }
                    }
                    _min[level][nz * n + nx] = lo;
                    _max[level][nz * n + nx] = hi;
                }
            }
        }
    }

    /// Unit patch in x and z from 0 to 1. Skirt vertices have y = -1; the shader drops them to the skirt bottom.
    static ArrayMesh PatchMesh()
    {
        const int n = PatchCells + 1;
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
                vertices.Add(new Vector3((float)i / PatchCells, 0, (float)j / PatchCells));
        }
        for (int j = 0; j < PatchCells; j++)
        {
            for (int i = 0; i < PatchCells; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                indices.AddRange(new[] { a, b, c, b, d, c });
            }
        }
        foreach (Func<int, int> edge in new Func<int, int>[] { k => k, k => PatchCells * n + k, k => k * n, k => k * n + PatchCells })
        {
            int start = vertices.Count;
            for (int k = 0; k < n; k++)
                vertices.Add(new Vector3(vertices[edge(k)].X, -1, vertices[edge(k)].Z));
            for (int k = 0; k < PatchCells; k++)
                indices.AddRange(new[] { edge(k), edge(k + 1), start + k, edge(k + 1), start + k + 1, start + k });
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    void BuildCollision(float[] heights)
    {
        var body = new StaticBody3D { Name = "Collision" };
        AddChild(body);
        int cells = _samples - 1;
        float[] field = _holes == null ? heights : Holed(heights, cells, body);
        for (int z0 = 0; z0 < cells; z0 += CollisionCells)
        {
            for (int x0 = 0; x0 < cells; x0 += CollisionCells)
            {
                int width = Math.Min(CollisionCells, cells - x0) + 1, depth = Math.Min(CollisionCells, cells - z0) + 1;
                var data = new float[width * depth];
                for (int r = 0; r < depth; r++)
                    Array.Copy(field, (z0 + r) * _samples + x0, data, r * width, width);
                var shape = new CollisionShape3D
                {
                    Shape = new HeightMapShape3D { MapWidth = width, MapDepth = depth, MapData = data },
                    Position = new Vector3(-_half + (x0 + (width - 1) / 2f) * _resolution, 0, -_half + (z0 + (depth - 1) / 2f) * _resolution),
                };
                if (_resolution != 1f)
                    shape.Scale = new Vector3(_resolution, 1, _resolution);
                body.AddChild(shape);
            }
        }
    }

    /// The heights with holes cut in: Jolt drops every quad that uses a NaN sample, so a hole is only as fine as the 1 m
    /// sample grid. So each corner of a quad that overlaps a hole cell becomes NaN, and the parts of the dropped quads
    /// that are not holes come back as one mesh, each triangle clipped to the non-hole surface cells it crosses
    /// (Sutherland–Hodgman) and kept exactly on the rendered plane. Jolt then matches the rendered ground.
    float[] Holed(float[] heights, int cells, StaticBody3D body)
    {
        var field = (float[])heights.Clone();
        for (int cj = 0; cj < cells; cj++)
        {
            for (int ci = 0; ci < cells; ci++)
            {
                if (!QuadHoles(ci, cj, out _, out _, out _, out _))
                    continue;
                foreach ((int i, int j) in new[] { (ci, cj), (ci + 1, cj), (ci, cj + 1), (ci + 1, cj + 1) })
                    field[j * _samples + i] = float.NaN;
            }
        }
        var faces = new List<Vector3>();
        for (int cj = 0; cj < cells; cj++)
        {
            for (int ci = 0; ci < cells; ci++)
            {
                bool dropped = float.IsNaN(field[cj * _samples + ci]) || float.IsNaN(field[cj * _samples + ci + 1])
                    || float.IsNaN(field[(cj + 1) * _samples + ci]) || float.IsNaN(field[(cj + 1) * _samples + ci + 1]);
                if (dropped)
                    PatchQuad(heights, ci, cj, faces);
            }
        }
        if (faces.Count > 0)
        {
            var shape = new CollisionShape3D
            {
                Name = "Hole edges",
                Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
            };
            body.AddChild(shape);
        }
        return field;
    }

    /// Whether height quad (ci, cj) overlaps a hole cell, with the surface-cell range it covers.
    bool QuadHoles(int ci, int cj, out int c0, out int c1, out int r0, out int r1)
    {
        double per = 1.0 / _holes.CellResolution;
        c0 = (int)Math.Floor((-_half + ci * _resolution + _holes.Half) * per);
        c1 = (int)Math.Ceiling((-_half + (ci + 1) * _resolution + _holes.Half) * per) - 1;
        r0 = (int)Math.Floor((-_half + cj * _resolution + _holes.Half) * per);
        r1 = (int)Math.Ceiling((-_half + (cj + 1) * _resolution + _holes.Half) * per) - 1;
        for (int row = r0; row <= r1; row++)
        {
            for (int column = c0; column <= c1; column++)
            {
                if (_holes.HoleCell(row, column))
                    return true;
            }
        }
        return false;
    }

    /// The non-hole parts of height quad (ci, cj)'s two triangles, appended to `faces` as triangle vertices.
    void PatchQuad(float[] heights, int ci, int cj, List<Vector3> faces)
    {
        QuadHoles(ci, cj, out int c0, out int c1, out int r0, out int r1);
        float x0 = -_half + ci * _resolution, z0 = -_half + cj * _resolution;
        float h00 = heights[cj * _samples + ci], h10 = heights[cj * _samples + ci + 1];
        float h01 = heights[(cj + 1) * _samples + ci], h11 = heights[(cj + 1) * _samples + ci + 1];
        var polygon = new List<Vector2>();
        var clipped = new List<Vector2>();
        for (int row = r0; row <= r1; row++)
        {
            for (int column = c0; column <= c1; column++)
            {
                if (_holes.HoleCell(row, column))
                    continue;
                double cell = _holes.CellResolution;
                var min = new Vector2((float)(-_holes.Half + column * cell), (float)(-_holes.Half + row * cell));
                var max = new Vector2((float)(min.X + cell), (float)(min.Y + cell));
                // Triangles a-b-c (u + v <= 1) and b-d-c, as the render mesh and WorldQuery.Terrain split the quad.
                foreach (bool lower in new[] { true, false })
                {
                    polygon.Clear();
                    if (lower)
                    {
                        polygon.Add(new Vector2(x0, z0));
                        polygon.Add(new Vector2(x0 + _resolution, z0));
                        polygon.Add(new Vector2(x0, z0 + _resolution));
                    }
                    else
                    {
                        polygon.Add(new Vector2(x0 + _resolution, z0));
                        polygon.Add(new Vector2(x0 + _resolution, z0 + _resolution));
                        polygon.Add(new Vector2(x0, z0 + _resolution));
                    }
                    ClipToRect(polygon, clipped, min, max);
                    if (clipped.Count < 3)
                        continue;
                    float gx = (lower ? h10 - h00 : h11 - h01) / _resolution, gz = (lower ? h01 - h00 : h11 - h10) / _resolution;
                    float baseHeight = lower ? h00 : h11;
                    Vector2 origin = lower ? new Vector2(x0, z0) : new Vector2(x0 + _resolution, z0 + _resolution);
                    Vector3 At(Vector2 p) => new(p.X, baseHeight + gx * (p.X - origin.X) + gz * (p.Y - origin.Y), p.Y);
                    for (int k = 1; k + 1 < clipped.Count; k++)
                    {
                        faces.Add(At(clipped[0]));
                        faces.Add(At(clipped[k]));
                        faces.Add(At(clipped[k + 1]));
                    }
                }
            }
        }
    }

    /// Sutherland–Hodgman: the part of the convex polygon `input` inside the rectangle, into `output`.
    static void ClipToRect(List<Vector2> input, List<Vector2> output, Vector2 min, Vector2 max)
    {
        var source = new List<Vector2>(input);
        var target = new List<Vector2>();
        for (int edge = 0; edge < 4; edge++)
        {
            target.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 a = source[i], b = source[(i + 1) % source.Count];
                float da = Side(a, edge, min, max), db = Side(b, edge, min, max);
                if (da >= 0)
                    target.Add(a);
                if (da >= 0 != db >= 0)
                    target.Add(a + (b - a) * (da / (da - db)));
            }
            (source, target) = (target, source);
        }
        output.Clear();
        output.AddRange(source);
    }

    /// How far inside the rectangle's edge `edge` a point is (negative outside).
    static float Side(Vector2 p, int edge, Vector2 min, Vector2 max) =>
        edge switch { 0 => p.X - min.X, 1 => max.X - p.X, 2 => p.Y - min.Y, _ => max.Y - p.Y };
}
