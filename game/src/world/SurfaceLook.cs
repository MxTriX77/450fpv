using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// How every surface looks, render-only (game/assets/materials/surface_look.json; not in the content hash, so no world
/// query depends on it). Built once when a map loads:
/// - Texture arrays with one layer per CC0 set in use: albedo, normal (RG, OpenGL convention), height and AO. The layers
///   are the imported, block-compressed textures with their 2K top mip dropped, so nothing is decoded or re-compressed.
/// - Params: a 256 × ParamRows float texture, column = surface index, read by the terrain and far-cover shaders:
///   row 0 tint (linear, so the set's mean albedo × AO lands on the target colour) and array layer;
///   row 1 tile m, straw overlay, litter overlay, roughness; row 2 ridge normal x, z, spacing m (0 = none), phase;
///   row 3 macro variation, patch amount, ridge amplitude m; row 4 patch tint;
///   row 5 grass silhouette density (stems × mean diameter × mean height, m²/m²), height min, max, dry fraction;
///   row 6 green grass colour, straw silhouette density; row 7 dry grass colour, straw mean length m;
///   row 8 straw colour, blade width m.
/// - Linear vegetation colours per surface index for the near micro-detail, so near and far cover match.
public sealed class SurfaceLook
{
    public const string LookPath = "res://assets/materials/surface_look.json";
    public const int ParamRows = 9;
    const string TextureDir = "res://assets/textures";
    const int MaxLayerSize = 1024; // px: at the 2–4 m tiles used, about 3 mm per texel

    public Texture2DArray Albedo, Normal, Height, Occlusion;
    public ImageTexture Params;
    /// Array layer i holds set Sets[i]; Layer[surface index] is the surface's layer.
    public string[] Sets;
    public readonly int[] Layer = new int[256];
    public int StrawLayer, LitterLayer;
    public float StrawTile, LitterTile;
    public Vector3 StrawTint, LitterTint;

    public readonly Color[] GrassGreen = new Color[256], GrassDry = new Color[256], Straw = new Color[256], Twigs = new Color[256],
        Litter = new Color[256];
    public readonly float[] DryFraction = new float[256];

    public static SurfaceLook Load(WorldQuery world, string surfacesPath, string lookPath)
    {
        using JsonDocument lookDoc = JsonDocument.Parse(File.ReadAllText(lookPath));
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(surfacesPath));
        JsonElement look = lookDoc.RootElement, entries = look.GetProperty("surfaces"), veg = look.GetProperty("vegetation_default");
        JsonElement straw = look.GetProperty("overlays").GetProperty("straw"), litter = look.GetProperty("overlays").GetProperty("litter");
        Dictionary<string, JsonElement> material = table.RootElement.GetProperty("surfaces").EnumerateArray()
            .ToDictionary(s => s.GetProperty("id").GetString(), s => s.GetProperty("material"));

        JsonElement? Entry(SurfaceParams s) => entries.TryGetProperty(s.Id, out JsonElement e) ? e : null;
        string SetOf(SurfaceParams s) => Entry(s) is JsonElement e && e.TryGetProperty("set", out JsonElement set)
            ? set.GetString() : look.GetProperty("default_set").GetString();
        var sets = world.Surfaces.Select(SetOf).Append(straw.GetProperty("set").GetString()).Append(litter.GetProperty("set").GetString())
            .Distinct().ToList();

        var result = new SurfaceLook { Sets = sets.ToArray() };
        var maps = new List<Image>[4];
        var means = new Vector3[sets.Count];
        string[] names = { "albedo", "normal", "height", "ao" };
        for (int m = 0; m < 4; m++)
            maps[m] = sets.Select(set => Shrink(Map(set, names[m]))).ToList();
        for (int i = 0; i < sets.Count; i++)
            means[i] = Mean(maps[0][i], true) * Mean(maps[3][i], false).X;
        result.Albedo = ArrayOf(maps[0]);
        result.Normal = ArrayOf(maps[1]);
        result.Height = ArrayOf(maps[2]);
        result.Occlusion = ArrayOf(maps[3]);

        result.StrawLayer = sets.IndexOf(straw.GetProperty("set").GetString());
        result.LitterLayer = sets.IndexOf(litter.GetProperty("set").GetString());
        result.StrawTile = straw.GetProperty("tile_m").GetSingle();
        result.LitterTile = litter.GetProperty("tile_m").GetSingle();
        result.StrawTint = Linear(straw.GetProperty("albedo_srgb").GetString()) / means[result.StrawLayer];
        result.LitterTint = Linear(litter.GetProperty("albedo_srgb").GetString()) / means[result.LitterLayer];

        Image param = Image.CreateEmpty(256, ParamRows, false, Image.Format.Rgbaf);
        foreach (SurfaceParams s in world.Surfaces)
        {
            JsonElement e = Entry(s) ?? default;
            float Num(string name, float fallback) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v.GetSingle() : fallback;
            string Hex(string name, string fallback) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v.GetString() : fallback;
            JsonElement mat = material[s.Id];
            int layer = result.Layer[s.Index] = sets.IndexOf(SetOf(s));
            Vector3 target = Linear(Hex("albedo_srgb", mat.GetProperty("albedo_srgb").GetString()));
            Vector3 tint = target / means[layer];
            Vector3 patch = Linear(Hex("patch_srgb", mat.GetProperty("albedo_srgb").GetString())) / target;
            bool ridged = s.RidgeAmplitude > 0;
            double azimuth = s.RidgeAzimuthDeg * Math.PI / 180;

            string[] grass = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("grass", out JsonElement g)
                ? g.EnumerateArray().Select(c => c.GetString()).ToArray()
                : veg.GetProperty("grass").EnumerateArray().Select(c => c.GetString()).ToArray();
            int i = s.Index;
            result.GrassGreen[i] = Color.FromHtml(grass[0]).SrgbToLinear();
            result.GrassDry[i] = Color.FromHtml(grass[1]).SrgbToLinear();
            result.DryFraction[i] = Num("dry_fraction", veg.GetProperty("dry_fraction").GetSingle());
            result.Straw[i] = Color.FromHtml(Hex("straw_srgb", veg.GetProperty("straw").GetString())).SrgbToLinear();
            result.Twigs[i] = Color.FromHtml(veg.GetProperty("twigs").GetString()).SrgbToLinear();
            result.Litter[i] = Color.FromHtml(Hex("litter_srgb", veg.GetProperty("litter").GetString())).SrgbToLinear();
            CoverParams grassCover = s.Cover[(int)CoverKind.Grass], strawCover = s.Cover[(int)CoverKind.Straw];
            float bladeWidth = Num("blade_width_m", veg.GetProperty("blade_width_m").GetSingle());

            param.SetPixel(i, 0, new Color(tint.X, tint.Y, tint.Z, layer));
            param.SetPixel(i, 1, new Color(Num("tile_m", 3f), Num("straw", 0f), Num("litter", 0f), mat.GetProperty("roughness").GetSingle()));
            param.SetPixel(i, 2, ridged ? new Color((float)Math.Sin(azimuth), (float)Math.Cos(azimuth), (float)s.RidgeSpacing,
                (float)world.RidgePhase(s.Index)) : new Color(0, 0, 0, 0));
            param.SetPixel(i, 3, new Color(Num("macro", 0.08f), Num("patch", 0f), (float)s.RidgeAmplitude, 0));
            param.SetPixel(i, 4, new Color(patch.X, patch.Y, patch.Z, 0));
            param.SetPixel(i, 5, grassCover == null ? new Color(0, 0, 0, 0) : new Color(Silhouette(grassCover), (float)grassCover.LengthMin,
                (float)grassCover.LengthMax, result.DryFraction[i]));
            param.SetPixel(i, 6, WithAlpha(result.GrassGreen[i], strawCover == null ? 0 : Silhouette(strawCover)));
            param.SetPixel(i, 7, WithAlpha(result.GrassDry[i], strawCover == null ? 0 : (float)(strawCover.LengthMin + strawCover.LengthMax) / 2));
            param.SetPixel(i, 8, WithAlpha(result.Straw[i], bladeWidth));
        }
        result.Params = ImageTexture.CreateFromImage(param);
        return result;
    }

    /// Stems × mean diameter × mean length: the side-on area of a cover per m² of ground at channel 1.0.
    static float Silhouette(CoverParams c) =>
        (float)(c.Density * (c.DiameterMin + c.DiameterMax) / 2 * (c.LengthMin + c.LengthMax) / 2);

    static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);

    static Vector3 Linear(string hex)
    {
        Color c = Color.FromHtml(hex).SrgbToLinear();
        return new Vector3(c.R, c.G, c.B);
    }

    /// The imported texture of one map of a set (its Godot import keeps it block-compressed with mipmaps).
    static Image Map(string set, string map)
    {
        string path = $"{TextureDir}/{set}/{set}_{map}.jpg";
        if (!ResourceLoader.Exists(path))
            throw new FileNotFoundException($"surface look: texture set '{set}' has no {map} map at {path}");
        return GD.Load<Texture2D>(path).GetImage();
    }

    /// The image from the first mip level no larger than MaxLayerSize down, as its own mipmapped image: a slice of the
    /// data, no resampling.
    static Image Shrink(Image image)
    {
        if (!image.HasMipmaps())
            image.GenerateMipmaps();
        int level = 0;
        while ((image.GetWidth() >> level) > MaxLayerSize)
            level++;
        if (level == 0)
            return image;
        byte[] data = image.GetData();
        int offset = (int)image.GetMipmapOffset(level);
        return Image.CreateFromData(image.GetWidth() >> level, image.GetHeight() >> level, true, image.GetFormat(), data[offset..]);
    }

    /// Mean of a map from its 4 × 4 mip, linear (sRGB decoded when `srgb`).
    static Vector3 Mean(Image image, bool srgb)
    {
        int level = 0;
        while ((image.GetWidth() >> level) > 4)
            level++;
        byte[] data = image.GetData();
        int start = (int)image.GetMipmapOffset(level);
        int end = level < image.GetMipmapCount() ? (int)image.GetMipmapOffset(level + 1) : data.Length;
        Image small = Image.CreateFromData(image.GetWidth() >> level, image.GetHeight() >> level, false, image.GetFormat(), data[start..end]);
        if (small.IsCompressed())
            small.Decompress();
        var sum = Vector3.Zero;
        for (int y = 0; y < small.GetHeight(); y++)
        {
            for (int x = 0; x < small.GetWidth(); x++)
            {
                Color c = small.GetPixel(x, y);
                if (srgb)
                    c = c.SrgbToLinear();
                sum += new Vector3(c.R, c.G, c.B);
            }
        }
        return sum / (small.GetWidth() * small.GetHeight());
    }

    /// One layer per image. Layers must share size and format; if an import differs, all are decoded to RGBA8 at the
    /// largest size instead.
    static Texture2DArray ArrayOf(List<Image> images)
    {
        if (images.Any(i => i.GetFormat() != images[0].GetFormat() || i.GetSize() != images[0].GetSize()))
        {
            int size = images.Max(i => i.GetWidth());
            foreach (Image image in images)
            {
                if (image.IsCompressed())
                    image.Decompress();
                image.ClearMipmaps();
                image.Convert(Image.Format.Rgba8);
                image.Resize(size, size, Image.Interpolation.Lanczos);
                image.GenerateMipmaps();
            }
        }
        var array = new Texture2DArray();
        array.CreateFromImages(new Godot.Collections.Array<Image>(images));
        return array;
    }
}
