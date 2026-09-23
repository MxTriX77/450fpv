using System.Text.Json;

/// One cover entry of a surface (game/maps/README.md), as the world query uses it.
public sealed class CoverParams
{
    public double Density;                   // elements per m² at cover channel 1.0
    public double LengthMin, LengthMax;      // m
    public double DiameterMin, DiameterMax;  // m
    public double TipStiffness;              // N/m, at mean length and mean diameter
    public double HookProbability;
    public double HookReleaseMin, HookReleaseMax; // N
    public bool HasMat;
    public double MatDepthMin, MatDepthMax;  // m, at cover channel 1.0
}

/// The per-surface fields of surfaces.json that shape the ground and its micro-detail.
public sealed class SurfaceParams
{
    public string Id;
    public byte Index;
    public double ReliefAmplitude, ReliefWavelength; // RMS m, crest-to-crest m
    public double RidgeAmplitude, RidgeSpacing, RidgeAzimuthDeg; // amplitude 0 = no ridges
    public double PitDensity, PitDepthMin, PitDepthMax, PitRadiusMin, PitRadiusMax;
    public readonly CoverParams[] Cover = new CoverParams[4]; // by CoverKind; null = none

    public static SurfaceParams[] ParseTable(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement list = document.RootElement.GetProperty("surfaces");
        var table = new SurfaceParams[list.GetArrayLength()];
        int n = 0;
        foreach (JsonElement s in list.EnumerateArray())
        {
            JsonElement relief = s.GetProperty("micro_relief"), pits = s.GetProperty("pitfalls");
            var p = new SurfaceParams
            {
                Id = s.GetProperty("id").GetString(),
                Index = s.GetProperty("index").GetByte(),
                ReliefAmplitude = relief.GetProperty("amplitude_m").GetDouble(),
                ReliefWavelength = relief.GetProperty("wavelength_m").GetDouble(),
                PitDensity = pits.GetProperty("density_per_m2").GetDouble(),
            };
            (p.PitDepthMin, p.PitDepthMax) = Range(pits, "depth_m");
            (p.PitRadiusMin, p.PitRadiusMax) = Range(pits, "radius_m");
            if (relief.TryGetProperty("ridges", out JsonElement ridges))
            {
                p.RidgeAmplitude = ridges.GetProperty("amplitude_m").GetDouble();
                p.RidgeSpacing = ridges.GetProperty("spacing_m").GetDouble();
                p.RidgeAzimuthDeg = ridges.GetProperty("azimuth_deg").GetDouble();
            }
            foreach (JsonElement c in s.GetProperty("cover").EnumerateArray())
            {
                var cover = new CoverParams
                {
                    Density = c.GetProperty("stems_per_m2").GetDouble(),
                    TipStiffness = c.GetProperty("lateral_stiffness_n_per_m").GetDouble(),
                    HookProbability = c.GetProperty("hook_probability").GetDouble(),
                };
                (cover.LengthMin, cover.LengthMax) = Range(c, "height_m");
                (cover.DiameterMin, cover.DiameterMax) = Range(c, "diameter_m");
                (cover.HookReleaseMin, cover.HookReleaseMax) = Range(c, "hook_release_n");
                if (c.TryGetProperty("mat", out JsonElement mat))
                {
                    cover.HasMat = true;
                    (cover.MatDepthMin, cover.MatDepthMax) = Range(mat, "depth_m");
                }
                p.Cover[KindOf(c.GetProperty("type").GetString())] = cover;
            }
            table[n++] = p;
        }
        return table;
    }

    static int KindOf(string type) => type switch
    {
        "grass" => (int)CoverKind.Grass,
        "straw" => (int)CoverKind.Straw,
        "twigs" => (int)CoverKind.Twigs,
        "litter" => (int)CoverKind.Litter,
        _ => throw new JsonException($"unknown cover type '{type}'"),
    };

    static (double, double) Range(JsonElement parent, string name)
    {
        JsonElement range = parent.GetProperty(name);
        return (range[0].GetDouble(), range[1].GetDouble());
    }
}
