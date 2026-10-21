// A surface layered by a damage mask (p1-r01-vaz-sedan, design decision 5; p1-h01-adobe-house, design decision 2):
// URP/Lit's lighting over a base surface and up to five layers, each a film that covers a share of the surface set by
// one channel of the mask. A car's paint: the paint, then soot (G), burnt-off steel (A), rust (R) and dust (B). A
// facade: the whitewash, then the base band (A), render and adobe (both R, at two thresholds), soot (G) and dirt (B).
// The recipe lists the layers in order (the catalog importer's EnsureLayeredMaterial); this shader only composes them.
//
// Every surface (the base and each layer) is a recipe's own: its albedo times its tint, its normal map, and its packed
// mask (metallic R, occlusion G, smoothness A, as tools/assets/derive.py packs it) scaled by its metallic and
// smoothness; without a mask the mask is white, so the scales are the recipe's constants. All of them are tiled in
// the slot's UVs, the asset's metres (AST-07), each by its own _ST; the base's tiling is _BaseMap_ST, as URP/Lit's.
//
// The damage mask (RGBA, linear, clamped; _DamageMask_ST maps the slot's UVs onto it, from the metadata's uv_min and
// uv_max) gives each layer k its coverage
//     w_k = saturate((dot(mask, _LayerkChannel) - threshold) / feather + 0.5),   (threshold, feather) = _LayerkCoverage
// with _LayerkChannel one-hot on R, G, B or A: threshold 0.5 and feather 1 make the coverage the channel's value (a
// rust halo of R 0.9 is 90 % rust); threshold 0.25 and feather 0.1 a feathered step from 0.2 to 0.3 (the facade's
// render). In the recipe's order each layer covers what lies below it: albedo, normal, metallic, occlusion and
// smoothness all lerp by w_k, the normal renormalised. A layer of coverage 0 is not sampled (a dynamic branch, its
// mips from the UVs' own derivatives), so a pixel pays only for the layers on it; an unused layer has channel 0 and
// threshold 1, coverage 0 everywhere.
//
// Opaque, back faces culled, no alpha test. The shadow caster and depth passes are URP/Lit's; the depth-normals pass
// reads the base's normal only. No DOTS instancing (the project's URP assets have no GPU resident drawer), no
// lightmap meta pass (nothing is baked), no detail, parallax, emission or clear coat.
Shader "Fpv450/LayeredSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Base albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Base normal", 2D) = "bump" {}
        _MetallicGlossMap("Base mask (metallic R, occlusion G, smoothness A)", 2D) = "white" {}
        _Metallic("Base metallic (x mask R)", Range(0.0, 1.0)) = 0.0
        _Smoothness("Base smoothness (x mask A)", Range(0.0, 1.0)) = 0.5

        _DamageMask("Damage mask (RGBA, linear)", 2D) = "black" {}

        _Layer1Map("Layer 1 albedo", 2D) = "white" {}
        _Layer1Color("Layer 1 tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer1BumpMap("Layer 1 normal", 2D) = "bump" {}
        _Layer1MaskMap("Layer 1 mask", 2D) = "white" {}
        _Layer1Channel("Layer 1 channel (one-hot RGBA)", Vector) = (0, 0, 0, 0)
        _Layer1Coverage("Layer 1 coverage (threshold, feather)", Vector) = (1, 1, 0, 0)
        _Layer1Surface("Layer 1 surface (metallic, smoothness)", Vector) = (0, 0.5, 0, 0)

        _Layer2Map("Layer 2 albedo", 2D) = "white" {}
        _Layer2Color("Layer 2 tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer2BumpMap("Layer 2 normal", 2D) = "bump" {}
        _Layer2MaskMap("Layer 2 mask", 2D) = "white" {}
        _Layer2Channel("Layer 2 channel (one-hot RGBA)", Vector) = (0, 0, 0, 0)
        _Layer2Coverage("Layer 2 coverage (threshold, feather)", Vector) = (1, 1, 0, 0)
        _Layer2Surface("Layer 2 surface (metallic, smoothness)", Vector) = (0, 0.5, 0, 0)

        _Layer3Map("Layer 3 albedo", 2D) = "white" {}
        _Layer3Color("Layer 3 tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer3BumpMap("Layer 3 normal", 2D) = "bump" {}
        _Layer3MaskMap("Layer 3 mask", 2D) = "white" {}
        _Layer3Channel("Layer 3 channel (one-hot RGBA)", Vector) = (0, 0, 0, 0)
        _Layer3Coverage("Layer 3 coverage (threshold, feather)", Vector) = (1, 1, 0, 0)
        _Layer3Surface("Layer 3 surface (metallic, smoothness)", Vector) = (0, 0.5, 0, 0)

        _Layer4Map("Layer 4 albedo", 2D) = "white" {}
        _Layer4Color("Layer 4 tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer4BumpMap("Layer 4 normal", 2D) = "bump" {}
        _Layer4MaskMap("Layer 4 mask", 2D) = "white" {}
        _Layer4Channel("Layer 4 channel (one-hot RGBA)", Vector) = (0, 0, 0, 0)
        _Layer4Coverage("Layer 4 coverage (threshold, feather)", Vector) = (1, 1, 0, 0)
        _Layer4Surface("Layer 4 surface (metallic, smoothness)", Vector) = (0, 0.5, 0, 0)

        _Layer5Map("Layer 5 albedo", 2D) = "white" {}
        _Layer5Color("Layer 5 tint", Color) = (1, 1, 1, 1)
        [Normal] _Layer5BumpMap("Layer 5 normal", 2D) = "bump" {}
        _Layer5MaskMap("Layer 5 mask", 2D) = "white" {}
        _Layer5Channel("Layer 5 channel (one-hot RGBA)", Vector) = (0, 0, 0, 0)
        _Layer5Coverage("Layer 5 coverage (threshold, feather)", Vector) = (1, 1, 0, 0)
        _Layer5Surface("Layer 5 surface (metallic, smoothness)", Vector) = (0, 0.5, 0, 0)

        // What URP's passes read of URP/Lit's state: opaque, never clipped, the normal unscaled.
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Cutoff("__cutoff", Float) = 0.5
        [HideInInspector] _BumpScale("__bumpScale", Float) = 1.0
    }

    HLSLINCLUDE
    // Every surface has a normal map (the property's default "bump" is flat), so URP's passes carry the tangent frame.
    #define _NORMALMAP 1

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

    // One layout for every pass (the SRP batcher needs it): URP/Lit's fields its passes read, then the layers.
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseMap_TexelSize;
        float4 _DamageMask_ST;
        float4 _Layer1Map_ST, _Layer2Map_ST, _Layer3Map_ST, _Layer4Map_ST, _Layer5Map_ST;
        float4 _Layer1Coverage, _Layer2Coverage, _Layer3Coverage, _Layer4Coverage, _Layer5Coverage;
        half4 _BaseColor;
        half4 _Layer1Color, _Layer2Color, _Layer3Color, _Layer4Color, _Layer5Color;
        half4 _Layer1Channel, _Layer2Channel, _Layer3Channel, _Layer4Channel, _Layer5Channel;
        half4 _Layer1Surface, _Layer2Surface, _Layer3Surface, _Layer4Surface, _Layer5Surface;
        half _Cutoff;
        half _Smoothness;
        half _Metallic;
        half _BumpScale;
        half _Surface;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // Universal Pipeline keywords (as URP/Lit's forward pass)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            TEXTURE2D(_MetallicGlossMap);
            TEXTURE2D(_DamageMask);     SAMPLER(sampler_DamageMask);
            TEXTURE2D(_Layer1Map);      TEXTURE2D(_Layer1BumpMap);  TEXTURE2D(_Layer1MaskMap);
            TEXTURE2D(_Layer2Map);      TEXTURE2D(_Layer2BumpMap);  TEXTURE2D(_Layer2MaskMap);
            TEXTURE2D(_Layer3Map);      TEXTURE2D(_Layer3BumpMap);  TEXTURE2D(_Layer3MaskMap);
            TEXTURE2D(_Layer4Map);      TEXTURE2D(_Layer4BumpMap);  TEXTURE2D(_Layer4MaskMap);
            TEXTURE2D(_Layer5Map);      TEXTURE2D(_Layer5BumpMap);  TEXTURE2D(_Layer5MaskMap);

            // One surface's shading inputs.
            struct LayerSurface
            {
                half3 albedo;
                half3 normalTS;
                half metallic;
                half occlusion;
                half smoothness;
            };

            // A recipe's surface at uv, its mips from the given derivatives (so it can be sampled inside a branch).
            // All 18 surface maps read through the base map's sampler (repeat): a stage takes 16 samplers at most.
            LayerSurface SampleSurface(TEXTURE2D_PARAM(albedoMap, albedoSampler), TEXTURE2D_PARAM(normalMap, normalSampler),
                TEXTURE2D_PARAM(maskMap, maskSampler), float2 uv, float2 dx, float2 dy, half4 tint, half2 metallicSmoothness)
            {
                LayerSurface s;
                s.albedo = SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uv, dx, dy).rgb * tint.rgb;
                s.normalTS = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uv, dx, dy));
                half4 mask = SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, uv, dx, dy);
                s.metallic = mask.r * metallicSmoothness.x;
                s.occlusion = mask.g;
                s.smoothness = mask.a * metallicSmoothness.y;
                return s;
            }

            half Coverage(half4 damage, half4 channel, float2 thresholdFeather)
            {
                return saturate((dot(damage, channel) - thresholdFeather.x) / thresholdFeather.y + 0.5);
            }

            LayerSurface Cover(LayerSurface below, LayerSurface layer, half w)
            {
                below.albedo = lerp(below.albedo, layer.albedo, w);
                below.normalTS = lerp(below.normalTS, layer.normalTS, w);
                below.metallic = lerp(below.metallic, layer.metallic, w);
                below.occlusion = lerp(below.occlusion, layer.occlusion, w);
                below.smoothness = lerp(below.smoothness, layer.smoothness, w);
                return below;
            }

            #define COVER_WITH_LAYER(k)                                                                                     \
            {                                                                                                               \
                half w = Coverage(damage, _Layer##k##Channel, _Layer##k##Coverage.xy);                                      \
                UNITY_BRANCH if (w > 0.0)                                                                                   \
                {                                                                                                           \
                    float2 tiling = _Layer##k##Map_ST.xy;                                                                   \
                    LayerSurface layer = SampleSurface(TEXTURE2D_ARGS(_Layer##k##Map, sampler_BaseMap),                     \
                        TEXTURE2D_ARGS(_Layer##k##BumpMap, sampler_BaseMap), TEXTURE2D_ARGS(_Layer##k##MaskMap, sampler_BaseMap), \
                        uvM * tiling + _Layer##k##Map_ST.zw, dx * tiling, dy * tiling, _Layer##k##Color, _Layer##k##Surface.xy); \
                    surface = Cover(surface, layer, w);                                                                     \
                }                                                                                                           \
            }

            // URP/Lit's forward pass calls this with the base map's UV (the vertex applied _BaseMap_ST); it is undone to
            // the slot's metres, from which every layer and the damage mask take their own.
            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
            {
                float2 uvM = (uv - _BaseMap_ST.zw) / _BaseMap_ST.xy;
                float2 dx = ddx(uvM), dy = ddy(uvM);
                half4 damage = SAMPLE_TEXTURE2D(_DamageMask, sampler_DamageMask, uvM * _DamageMask_ST.xy + _DamageMask_ST.zw);

                LayerSurface surface = SampleSurface(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), TEXTURE2D_ARGS(_BumpMap, sampler_BaseMap),
                    TEXTURE2D_ARGS(_MetallicGlossMap, sampler_BaseMap), uv, dx * _BaseMap_ST.xy, dy * _BaseMap_ST.xy, _BaseColor,
                    half2(_Metallic, _Smoothness));
                COVER_WITH_LAYER(1)
                COVER_WITH_LAYER(2)
                COVER_WITH_LAYER(3)
                COVER_WITH_LAYER(4)
                COVER_WITH_LAYER(5)

                outSurfaceData = (SurfaceData)0;
                outSurfaceData.albedo = surface.albedo;
                outSurfaceData.alpha = 1.0;
                outSurfaceData.metallic = surface.metallic;
                outSurfaceData.smoothness = surface.smoothness;
                outSurfaceData.normalTS = normalize(surface.normalTS);
                outSurfaceData.occlusion = surface.occlusion;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
