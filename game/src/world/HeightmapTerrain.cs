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
public partial class HeightmapTerrain : Node3D
{
    const int PatchCells = 32;
    const int CollisionCells = 256;
    const float SplitDistance = 2f; // a patch splits into four while the camera is closer than this many patch sizes

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

    /// `r16` is the raw little-endian heightfield of `samples` × `samples`; height = offsetM + sample × scaleM.
    public void Build(byte[] r16, int samples, float resolution, float offsetM, float scaleM)
    {
        _samples = samples;
        _resolution = resolution;
        _half = (samples - 1) * resolution / 2f;
        var heights = new float[samples * samples];
        float lowest = float.MaxValue;
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = offsetM + (r16[2 * i] | r16[2 * i + 1] << 8) * scaleM;
            lowest = Math.Min(lowest, heights[i]);
        }
        _skirtBottom = lowest - 1f;
        BuildPyramid(heights);

        var texture = ImageTexture.CreateFromImage(Image.CreateFromData(samples, samples, false, Image.Format.R16, r16));
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/world/heightmap_terrain.gdshader") };
        _material.SetShaderParameter("heights", texture);
        _material.SetShaderParameter("height_offset", offsetM);
        _material.SetShaderParameter("height_range", scaleM * 65535f);
        _material.SetShaderParameter("resolution", resolution);
        _material.SetShaderParameter("half_size", _half);
        _material.SetShaderParameter("samples", samples);
        _material.SetShaderParameter("skirt_bottom", _skirtBottom);
        _patch = PatchMesh();
        BuildCollision(heights);
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
        for (int z0 = 0; z0 < cells; z0 += CollisionCells)
        {
            for (int x0 = 0; x0 < cells; x0 += CollisionCells)
            {
                int width = Math.Min(CollisionCells, cells - x0) + 1, depth = Math.Min(CollisionCells, cells - z0) + 1;
                var data = new float[width * depth];
                for (int r = 0; r < depth; r++)
                    Array.Copy(heights, (z0 + r) * _samples + x0, data, r * width, width);
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
}
