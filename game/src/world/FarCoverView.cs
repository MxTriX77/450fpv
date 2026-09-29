using System;
using System.Collections.Generic;
using Godot;

/// Far cover: cheap grass and straw tufts beyond the near micro-detail, out to about 80 m. Density only, render-only:
/// nothing here is physical, and the near ring (MicroDetailView) stays the physics' own elements.
///
/// Levels square grids of PerSide² tufts centred on the camera, each with twice the spacing and reach of the one inside.
/// Every tuft is a MultiMesh instance whose shape comes from far_cover.gdshader: it stands at its grid point with a
/// jitter hashed from the point, on the terrain, and is drawn only where the cover map and its surface's cover density
/// (SurfaceLook rows 5–8) put grass or straw. The grids snap to their spacing, so tufts never swim. Each level fades in
/// and out over a band, tuft by tuft, by a hash against the fade; level 0 fades in over the near ring's last
/// MicroDetailView.Fade m as the near elements fade out, so no edge shows.
public partial class FarCoverView : Node3D
{
    const int Levels = 4;
    const int PerSide = 64;       // tufts per grid side
    const float Spacing = 0.375f; // m between tufts at level 0: level 0 reaches 12 m, level 3 96 m
    const int Cards = 5;          // per tuft: 3 standing, 2 lying

    WorldQuery _world;
    readonly List<(ShaderMaterial Material, float Spacing)> _levels = new();

    public void Init(WorldQuery world, HeightmapTerrain terrain, SurfaceLook look)
    {
        _world = world;
        var shader = GD.Load<Shader>("res://src/world/far_cover.gdshader");
        ArrayMesh tuft = Tuft();
        var identity = new float[PerSide * PerSide * 12];
        for (int i = 0; i < PerSide * PerSide; i++)
            (identity[i * 12], identity[i * 12 + 5], identity[i * 12 + 10]) = (1f, 1f, 1f);
        float near = (float)MicroDetailView.Radius, fade = (float)MicroDetailView.Fade;
        // Fade-in and fade-out bands per level (m): in over the near ring's fade, then each level hands over to the next
        // over the outer quarter of its reach; the last fades out between 70 and 90 m.
        var bands = new Vector4[Levels];
        for (int level = 0; level < Levels; level++)
        {
            float reach = Spacing * (1 << level) * PerSide / 2;
            bands[level] = new Vector4(level == 0 ? near - fade : bands[level - 1].Z, level == 0 ? near : bands[level - 1].W,
                level == Levels - 1 ? 70f : 0.75f * reach, level == Levels - 1 ? 90f : reach);
        }
        for (int level = 0; level < Levels; level++)
        {
            float spacing = Spacing * (1 << level);
            var material = new ShaderMaterial { Shader = shader };
            foreach (string name in new[] { "height_offset", "height_range", "resolution", "half_size", "samples" })
                material.SetShaderParameter(name, terrain.Material.GetShaderParameter(name));
            material.SetShaderParameter("heights", terrain.HeightTexture);
            material.SetShaderParameter("surface_ids", terrain.SurfaceTexture);
            material.SetShaderParameter("cover", terrain.CoverTexture);
            if (terrain.HoleTexture != null)
            {
                material.SetShaderParameter("holes", terrain.HoleTexture);
                material.SetShaderParameter("use_holes", true);
            }
            material.SetShaderParameter("surface_params", look.Params);
            material.SetShaderParameter("surface_resolution", (float)world.CellResolution);
            material.SetShaderParameter("surface_cells", world.Cells);
            material.SetShaderParameter("spacing", spacing);
            material.SetShaderParameter("per_side", PerSide);
            material.SetShaderParameter("band", bands[level]);
            material.SetShaderParameter("level", level);
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = tuft,
                InstanceCount = PerSide * PerSide,
                // The node stays at the origin and the shader moves the tufts, so the box is the whole map.
                CustomAabb = new Aabb(new Vector3((float)-world.Half, terrain.Lowest - 1f, (float)-world.Half),
                    new Vector3((float)world.SizeM, terrain.Highest - terrain.Lowest + 4f, (float)world.SizeM)),
            };
            multimesh.Buffer = identity;
            AddChild(new MultiMeshInstance3D
            {
                Name = $"Level {level}",
                Multimesh = multimesh,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            _levels.Add((material, spacing));
        }
    }

    public override void _Process(double delta)
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (_world == null || camera == null)
            return;
        Vector3 eye = camera.GlobalPosition;
        Span<XZ> point = stackalloc XZ[] { new XZ(eye.X, eye.Z) };
        Span<GroundSample> ground = stackalloc GroundSample[1];
        _world.SampleGround(point, ground);
        float lift = (float)Math.Max(eye.Y - ground[0].TerrainHeight, 0);
        foreach ((ShaderMaterial material, float spacing) in _levels)
        {
            material.SetShaderParameter("eye", eye);
            material.SetShaderParameter("lift", lift);
            material.SetShaderParameter("origin_cell", new Vector2I(Mathf.FloorToInt(eye.X / spacing), Mathf.FloorToInt(eye.Z / spacing)));
        }
    }

    /// Cards indexed quads; each vertex carries its card in x and its corner in y (0 and 1 at the root, left and right;
    /// 2 and 3 at the top). The shader builds the real positions.
    static ArrayMesh Tuft()
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int card = 0; card < Cards; card++)
        {
            for (int corner = 0; corner < 4; corner++)
                vertices.Add(new Vector3(card, corner, 0));
            foreach (int corner in new[] { 0, 1, 3, 0, 3, 2 })
                indices.Add(card * 4 + corner);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
