// The wet look on URP's PBR lighting (w21-rain-on-camera 1.3, VIS-03): include it after Core.hlsl and before
// Lighting.hlsl (directly, or through LitForwardPass.hlsl or TerrainLitPasses.hlsl), in a pass that declares
//     #pragma multi_compile_fragment _ FPV450_WET
// (the wet copies of URP's Lit and Terrain/Lit define it). Without the keyword it adds nothing.
//
// - The body and the top (WetLaw.hlsl): every BRDFData URP builds (InitializeBRDFData, which UniversalFragmentPBR and
//   the impostor's loop call) gets the wetted diffuse body from its own dry one, and water's F0 for the dielectric
//   part of its specular: the top it shows the sky through is water. Its smoothness is its own: the film, 0.1-0.3 mm
//   (RainPreset's film), is thinner than the relief of soil, bark, render or concrete and takes its slopes (assumed).
// - Standing water (Severe rain): on a hard surface (the sim's rigid materials, FPV450_WET_HARD_LAYER) whose face
//   is flatter than the slope that drains it, the water stands deeper than the surface's texture, so its top is the
//   water's own: the face's geometric normal (the plane of the triangle, not the normal map) and the standing
//   water's smoothness.
#ifndef FPV450_WET_LIT_INCLUDED
#define FPV450_WET_LIT_INCLUDED

#if defined(FPV450_WET)

#ifdef UNIVERSAL_LIGHTING_INCLUDED
#error "WetLit.hlsl must be included before URP's Lighting.hlsl, or its BRDF never sees the rain"
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "WetLaw.hlsl"

// URP's BRDFData, then the rain's: the wetted body under water's top, and the captures' probes.
inline void Fpv450WetInitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, inout half alpha, out BRDFData outBRDFData)
{
    InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, outBRDFData);
    if (_Fpv450Wet.x > 0.5)
    {
        half f0 = (half)_Fpv450WetWater.w;
#ifdef _SPECULAR_SETUP
        half3 dry = albedo * (half(1.0) - ReflectivitySpecular(specular));
        half3 top = half3(f0, f0, f0); // the top it is seen through is water
        half reflectivity = f0;
#else
        half3 dry = albedo * OneMinusReflectivityMetallic(metallic);
        half3 top = lerp(half3(f0, f0, f0), albedo, metallic);
        half reflectivity = f0 + (half(1.0) - f0) * metallic;
#endif
        InitializeBRDFDataDirect(albedo, (half3)Fpv450WetBodyLaw(dry), top, reflectivity, half(1.0) - reflectivity, smoothness, alpha, outBRDFData);
    }
    if (_Fpv450Wet.z > 0.5)
    {
        outBRDFData.specular = half3(0.0, 0.0, 0.0);
        outBRDFData.reflectivity = half(0.0);
        outBRDFData.grazingTerm = half(0.0);
        if (_Fpv450Wet.z > 1.5)
            outBRDFData.diffuse = half3(1.0, 1.0, 1.0);
    }
}

inline void Fpv450WetInitializeBRDFData(inout SurfaceData surfaceData, out BRDFData brdfData)
{
    Fpv450WetInitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
}

#define InitializeBRDFData Fpv450WetInitializeBRDFData
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// The triangle's plane at the pixel, facing the shading normal's side.
float3 Fpv450WetFaceNormal(float3 positionWS, float3 normalWS)
{
    float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
    return dot(n, normalWS) < 0.0 ? -n : n;
}

// Severe rain's standing water: true on a hard, flat face, whose normal and smoothness become the water's.
bool Fpv450WetFlooded(InputData inputData, out float3 faceNormal)
{
    faceNormal = Fpv450WetFaceNormal(inputData.positionWS, inputData.normalWS);
    return _Fpv450Wet.x > 0.5 && _Fpv450Wet.y > 0.5 && (GetMeshRenderingLayer() & FPV450_WET_HARD_LAYER) != 0u
        && faceNormal.y >= _Fpv450WetPond.y;
}

half4 Fpv450WetFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    float3 face;
    if (Fpv450WetFlooded(inputData, face))
    {
        inputData.normalWS = (half3)face;
        surfaceData.smoothness = (half)_Fpv450WetPond.x;
    }
    return UniversalFragmentPBR(inputData, surfaceData);
}

half4 Fpv450WetFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular, half smoothness, half occlusion, half3 emission, half alpha)
{
    float3 face;
    if (Fpv450WetFlooded(inputData, face))
    {
        inputData.normalWS = (half3)face;
        smoothness = (half)_Fpv450WetPond.x;
    }
    return UniversalFragmentPBR(inputData, albedo, metallic, specular, smoothness, occlusion, emission, alpha);
}

#define UniversalFragmentPBR Fpv450WetFragmentPBR

#endif // FPV450_WET
#endif // FPV450_WET_LIT_INCLUDED
