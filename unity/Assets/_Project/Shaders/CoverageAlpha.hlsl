// The alpha an alpha-clipped card is clipped by, read where its texture resolves the leaves (p1-v01-locust task
// 1.2i). URP's passes clip at _Cutoff by the base map's alpha at the pixel's mip. Once a card spans a pixel or two,
// that mip is a few texels across a sprig cell of the leaf atlas: its filter blurs the cell into the empty atlas round
// it (a LeafSet002 cell keeps 0.94 of its mean alpha at mip 7, 0.79 at mip 8, 0.48 at mip 9, measured), and the few
// texels left pass the cutoff whole or not at all, so a far card covers whole pixels or none and its crown's coverage
// drifts with the distance (task 1.2g: V01's last mesh LOD 15-23 % over its baked crown at the impostor's switch).
// Here the alpha is read no coarser than a texel 1/1024 of the map wide: the 2k leaf atlas at mip 1, where each cell
// keeps its own mean alpha and its share over the cutoff (within 0.1 %, measured), and the 1k impostor atlas at mip 0,
// its bake's own texels (read at mip 2 the card still covered 14 % less than V01_M's last mesh LOD at 190 m, at mip 0
// 3 %, measured). A pixel is covered where its centre falls on an opaque part of the card, as it would on a leaf
// modelled in geometry, so a card covers its opaque share at every distance at MSAA 1, without a dither; the import
// still keeps the mips' coverage at the cutoff. The colour is read at the pixel's mip, as before. The cost: one more
// texture fetch, and the sub-pixel sparkle of real leaves.
// Include it before URP's LitInput.hlsl: it redefines SampleAlbedoAlpha, which URP's passes call for the base map.
#ifndef FPV450_COVERAGE_ALPHA_INCLUDED
#define FPV450_COVERAGE_ALPHA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// The base map at `uv` for a map of `mapSize` texels: read with the pixel's own uv gradients, as the colour is, until
// the pixel's footprint passes a texel 1/1024 of the map wide; beyond it with both gradients shrunk to that footprint,
// so the read stays at that mip (gradients, not a level of detail, which ps_4_0 cannot compute).
half4 CoverageSample(float2 uv, float2 mapSize)
{
    float2 dx = ddx(uv), dy = ddy(uv);
    float footprint = sqrt(max(dot(dx * mapSize, dx * mapSize), dot(dy * mapSize, dy * mapSize)));
    float shrink = min(1.0, max(mapSize.x / 1024.0, 1.0) / max(footprint, 1e-6));
    return SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, dx * shrink, dy * shrink);
}

// The base map's alpha, read where the map resolves the leaves (CoverageSample).
half CoverageAlpha(float2 uv, float2 mapSize)
{
    return CoverageSample(uv, mapSize).a;
}

// URP's SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)), which every URP Lit pass calls with the
// base map: the alpha CoverageAlpha's, the colour at the pixel's mip. A shader that defines FPV450_COVERAGE_COLOUR
// before this file (the impostor, p1-v01-locust task 1.2l) reads its colour with its alpha, in one fetch: a texel of
// its atlas holds whichever leaf, twig or limb the bake saw there, and the card's normal is read at full resolution
// too, so a covered pixel shows the surface under its centre in that surface's colour and lit at its normal, as a
// mesh LOD's pixel shows the leaf or the twig the raster put there. At the pixel's mip the colour mixed the surfaces
// round it, and the empty atlas Unity dilates between them, into a pixel lit as one of them, and the card's crown
// darkened the coarser the mip (V01_M's albedo, unlit: 0.1178, 0.1094 and 0.1096 at 50, 116.9 and 190 m, against
// 0.1195, 0.1197 and 0.1198 read with its alpha, measured). A macro, as LitInput.hlsl declares _BaseMap_TexelSize
// after this file.
#ifdef FPV450_COVERAGE_COLOUR
#define SampleAlbedoAlpha(uv, textureArgs) CoverageSample(uv, _BaseMap_TexelSize.zw)
#else
#define SampleAlbedoAlpha(uv, textureArgs) \
    half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb, CoverageAlpha(uv, _BaseMap_TexelSize.zw))
#endif

#endif
