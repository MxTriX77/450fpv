#ifndef FPV450_PLANT_BEND_INCLUDED
#define FPV450_PLANT_BEND_INCLUDED

// The drawn plants in the flight's air (w16-plants-in-the-air 1.3): each plant's bend from its drawn rest, read from the field
// PlantField.compute writes each frame (1.2's law: the wind's closed form and the wash's grids), and the vertex moved by it.
// The field's levels, finest first: the fine wash grid (5 cm), the coarse one (15 cm), near (0.5 m), mid (2 m), far (8 m); each
// a texture array of bands, x and z the bend (rad, world: the top's way times the angle), z where the texel holds one. A point
// takes the finest level that holds its band, faded into the next one over the level's last texels. Without a flight no level
// is set (their boxes are 0) and nothing bends: the plants stand as drawn.

TEXTURE2D_ARRAY(_PlantLevel0);
TEXTURE2D_ARRAY(_PlantLevel1);
TEXTURE2D_ARRAY(_PlantLevel2);
TEXTURE2D_ARRAY(_PlantLevel3);
TEXTURE2D_ARRAY(_PlantLevel4);
SAMPLER(sampler_linear_clamp);
float4 _PlantLevelBox[5]; // per level: texel 0's corner x, z, the cell (m), the size (texels; 0: no level)

// The bands (PlantBands.cs): 0-8 the grass culms (green 0.5, 0.8, 1.3 m; dry and burnt 0.6, 1.0, 1.7 m), 9 a crown's leafy twig,
// 10 a bush's, 11 a crown's branch, 12-17 the whole plants' sway (the island tree, small, mid and large pines, leafy and bare
// bushes). Which levels hold each (bits: 1 fine wash, 2 coarse wash, 4 near, 8 mid, 16 far) and its slice there.
#define PLANT_TREE_TWIG 9
#define PLANT_BUSH_TWIG 10
#define PLANT_BRANCH 11
static const int PlantBandLevels[18] = { 15, 15, 15, 15, 15, 15, 15, 15, 15, 12, 12, 24, 16, 16, 16, 16, 16, 16 };

// The elements' lengths the drawn bend is shaped over, m (PlantBands.cs: a leafy twig 0.5 m, a branch 3.5 m).
#define PLANT_TWIG_LENGTH 0.5
#define PLANT_BRANCH_LENGTH 3.5

float4 PlantLevelSample(int level, int slice, float2 uv)
{
    switch (level)
    {
        case 0: return SAMPLE_TEXTURE2D_ARRAY_LOD(_PlantLevel0, sampler_linear_clamp, uv, slice, 0);
        case 1: return SAMPLE_TEXTURE2D_ARRAY_LOD(_PlantLevel1, sampler_linear_clamp, uv, slice, 0);
        case 2: return SAMPLE_TEXTURE2D_ARRAY_LOD(_PlantLevel2, sampler_linear_clamp, uv, slice, 0);
        case 3: return SAMPLE_TEXTURE2D_ARRAY_LOD(_PlantLevel3, sampler_linear_clamp, uv, slice, 0);
        default: return SAMPLE_TEXTURE2D_ARRAY_LOD(_PlantLevel4, sampler_linear_clamp, uv, slice, 0);
    }
}

// A band's bend at a place (rad, world x and z).
float2 PlantBendAt(int band, float2 xz)
{
    float2 bend = 0;
    float left = 1;
    int levels = PlantBandLevels[band];
    [loop] for (int level = 0; level < 5 && left > 0; level++)
    {
        float4 box = _PlantLevelBox[level];
        if ((levels & (1 << level)) == 0 || box.w <= 0) continue;
        float2 t = (xz - box.xy) / box.z; // in texels
        float edge = min(min(t.x, t.y), min(box.w - t.x, box.w - t.y));
        if (edge <= 1) continue;
        float4 s = PlantLevelSample(level, level == 4 ? band - PLANT_BRANCH : band, t / box.w);
        float weight = saturate((edge - 1) / 4) * saturate(s.z);
        if (weight <= 0) continue;
        bend += left * weight * (s.xy / max(s.z, 1e-4));
        left *= 1 - weight;
    }
    return bend;
}

// A culm of a class (0 green, 1 dry, 2 burnt) and length (m): its class's three bands, interpolated in the log of the length.
float2 GrassBend(int plantClass, float culm, float2 xz)
{
    float lo = plantClass == 0 ? 0.5 : 0.6, mid = plantClass == 0 ? 0.8 : 1.0, hi = plantClass == 0 ? 1.3 : 1.7;
    int first = plantClass * 3;
    float l = clamp(culm, lo, hi);
    if (l <= mid)
        return lerp(PlantBendAt(first, xz), PlantBendAt(first + 1, xz), log(l / lo) / log(mid / lo));
    return lerp(PlantBendAt(first + 1, xz), PlantBendAt(first + 2, xz), log(l / mid) / log(hi / mid));
}

// The cantilever's chord angle at a share s of its length, over its tip's (1 at the tip): (s³ - 4s² + 6s)/3, a uniformly loaded
// cantilever's deflection over s (derived).
float PlantChord(float s)
{
    s = saturate(s);
    return (s * s * s - 4 * s * s + 6 * s) / 3;
}

// The cantilever's deflection at a share s of its length, over its tip's: (s⁴ - 4s³ + 6s²)/3 (derived).
float PlantDeflection(float s)
{
    s = saturate(s);
    return s * s * (s * s - 4 * s + 6) / 3;
}

// A point of an upright plant of the given length, its base at baseY, turned about its base by the bend as the cantilever bends:
// each point at its height h keeps its distance from the base and turns by the chord's angle at h (its horizontal place in the
// plant is kept, as each blade or limb stands on the ground beside the base).
float3 PlantTurn(float3 positionWS, float baseY, float2 bend, float span)
{
    float phi = length(bend);
    float h = positionWS.y - baseY;
    if (phi < 1e-5 || h <= 0) return positionWS;
    float a = phi * PlantChord(h / span), s, c;
    sincos(a, s, c);
    positionWS.xz += bend / phi * (h * s);
    positionWS.y = baseY + h * c;
    return positionWS;
}

// An element's tip pushed by its bend (rad) over its length, at a share of its length: the horizontal deflection and the tip's
// fall with it.
float3 PlantPush(float2 bend, float span, float share)
{
    float phi = length(bend);
    if (phi < 1e-5) return 0;
    float d = span * PlantDeflection(share), s, c;
    sincos(phi, s, c);
    return float3(bend.x / phi * d * s, -d * (1 - c), bend.y / phi * d * s);
}

// The instance's origin and its height scale, from its matrix (terrain details and trees are instanced, so per instance).
float3 PlantOrigin() { return GetObjectToWorldMatrix()._m03_m13_m23; }
float PlantScale() { return length(GetObjectToWorldMatrix()._m01_m11_m21); }

// A tree's or a bush's vertex in the air: the whole plant's sway (its band, at its base) turns it about the base over its drawn
// height; a crown's foliage and twigs also ride its branches and twigs, read where the vertex is (the branch from the trunk out
// over its 3.5 m, the twig whole). Wood and impostor cards take the sway only.
float3 PlantTree(float3 positionWS, int swayBand, float height, int twigBand, float branch, float foliage)
{
    float3 origin = PlantOrigin();
    float tall = height * PlantScale();
    float3 p = PlantTurn(positionWS, origin.y, PlantBendAt(swayBand, origin.xz), tall);
    if (foliage <= 0) return p;
    float2 at = positionWS.xz;
    if (branch > 0)
    {
        float reach = length(positionWS.xz - origin.xz);
        p += PlantPush(PlantBendAt(PLANT_BRANCH, at), PLANT_BRANCH_LENGTH, reach / PLANT_BRANCH_LENGTH);
    }
    p += PlantPush(PlantBendAt(twigBand, at), PLANT_TWIG_LENGTH, 1);
    return p;
}

#endif
