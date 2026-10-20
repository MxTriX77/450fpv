// The impostor card (p1-v01-locust, design decision 3; asset-pipeline "Baked impostor LOD"): the far LOD of a tree or
// a clump, an upright quad that the vertex stage turns about the asset's vertical axis to face the camera (in the
// shadow pass, the light), showing the atlas cell of the nearest of the views tools/blender/lib/impostor.py renders.
// The card mesh, built by the catalog importer, holds the quad in its own frame: x across it, y up it, 0 on the axis.
// The views are _ImpostorGrid.x orthographic views from the azimuths k * 360 / views (from +Z towards +X, object
// space), view k in cell (k mod columns, k div columns) from the atlas's top left, in both atlases. The albedo atlas
// (_BaseMap) holds albedo. The normal atlas (_BumpMap, linear) holds per texel the mean of the visible surfaces'
// geometric normals as view k saw them, unnormalised, in its axes (right, up, towards its camera, _ImpostorGrid.w
// degrees above the horizon) mapped into 0-1: the forward pass samples it at the cell's uv, at full resolution, turns
// it into object space with the same axes, and lights the texel as a distribution of normals round that mean's
// direction, as wide as its length R says (task 1.2n; URP's _NORMALMAP stays off, as the fragment sets the normal
// itself). URP/Lit lights a surface at one normal; lit so at the mean's direction V01_M's texels of leaves, twigs and
// limbs facing apart took 7 % less of the review's sun than their surfaces lit point by point, and 67 % less of the
// sky's grazing reflection (measured), the card 9 % dark by day against LOD2 at its switch. The distribution is the
// von Mises-Fisher one of mean resultant length R, the usual model of a texel's normals (HDRP's normal map filtering
// takes it): its concentration kappa = R (3 - R^2) / (1 - R^2) (Banerjee et al. 2005, as HDRP's
// TextureNormalVariance). Each light's diffuse is the clamped cosine averaged over it, Hill's fitted irradiance of a
// normalised spherical Gaussian (the cosine and the lobe swap roles; within 0.015 of the exact average for R >= 0.4,
// derived); the sky's reflection takes URP's Fresnel term Pow4(1 - saturate(n.v)) averaged over it, n.v taken as a
// normal variable with the distribution's own mean and variance and integrated exactly (within 0.04 of the exact
// average at R 0.5, 0.01 at 0.9, derived); the specular highlight stays URP's at the mean's direction, its lobe at
// the card's smoothness wide enough to hold the texel's spread (within 3 % of the surfaces' own, measured). Over
// V01_M's texels these took the review's sun and the sky's grazing reflection within 2.2 and 0.8 % of the surfaces lit
// point by point (measured). The normal atlas is imported as BC7 and read at the nearest texel, so each pixel takes
// one texel's mean and spread as the bake wrote them (ModelPostprocessor). The card has the leaves' smoothness, since
// it shows their normals (the catalog importer sets it). The
// normal atlas's alpha holds the occlusion of the surface under each texel, its recipe's (the mask's G), which URP/Lit
// applies to the ambient light and its reflections only, read with the normal and set as the surface's occlusion
// (task 1.2m: one crown-wide occlusion of 0.78 gave the leaves, 0.97 on the mesh LODs, part of the bark's 0.56 and the
// bark part of theirs; per texel V01_M's card gains 1.0 point against LOD2 at 190 m by day, measured). It casts
// shadows facing the light and receives none (the material sets _RECEIVE_SHADOWS_OFF): the light-facing caster would
// shadow the camera-facing card along their shared axis. Its alpha, the coverage the render found per texel, clips as
// the leaves' does, read where the atlas resolves the crown (CoverageAlpha.hlsl, task 1.2i: read at the pixel's mip,
// the card lost 14-22 % of V01's baked crowns at 116.9-230 m, measured), and its albedo with it, as its normal is
// (FPV450_COVERAGE_COLOUR, task 1.2l: at the pixel's mip V01_M's card darkened 7 % from 50 to 116.9 m). Each pass includes URP's own Lit pass and
// wraps its vertex function, as Fpv450/Leaf does, and the forward pass runs URP's fragment with the distribution's
// terms in its lighting (ImpostorPassFragment); the extra vectors stand outside
// URP/Lit's per-material buffer, so the SRP batcher draws the card on its own, and the material instances it. No wind.
Shader "Fpv450/Impostor"
{
    Properties
    {
        [MainTexture] _BaseMap("Atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _BumpMap("Normal atlas, occlusion in alpha", 2D) = "bump" {}
        _ImpostorGrid("Views, columns, rows, elevation", Vector) = (8, 4, 2, 15)
        _ImpostorCard("Card width, height, bottom", Vector) = (1, 2, 0, 0)

        // Render state, set by the catalog importer: clipping on, no face culled, no shadow received.
        [ToggleUI] _AlphaClip("__clip", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("__cull", Float) = 0.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 0.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _Surface("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #define FPV450_COVERAGE_COLOUR  // the albedo read with the alpha (CoverageAlpha.hlsl)
        float4 _ImpostorGrid;   // x: views, y: columns, z: rows, w: the views' elevation in degrees
        float4 _ImpostorCard;   // x: width, y: height, z: bottom, in the card's frame

        // The horizontal direction from the vertical axis towards `toward` (an object-space point or direction), and
        // the card's right across it.
        void ImpostorFrame(float3 toward, out float3 facing, out float3 right)
        {
            float2 d = toward.xz;
            float len = length(d);
            facing = len > 1e-5 ? float3(d.x, 0.0, d.y) / len : float3(0.0, 0.0, 1.0);
            right = float3(-facing.z, 0.0, facing.x);
        }

        // The nearest view to a card facing `facing`: k of the views from the azimuths k * 360 / views.
        float ImpostorView(float3 facing)
        {
            float views = _ImpostorGrid.x;
            return fmod(round(atan2(facing.x, facing.z) * views / 6.28318530718) + views, views);
        }

        // Turns the card point positionOS (x across, y up) to face `toward` and maps uv (0..1 over the card) into the
        // nearest view's cell.
        void ImpostorCard(float3 toward, inout float3 positionOS, inout float2 uv)
        {
            float3 facing, right;
            ImpostorFrame(toward, facing, right);
            float k = ImpostorView(facing);
            float column = fmod(k, _ImpostorGrid.y);
            float row = floor(k / _ImpostorGrid.y);
            uv = float2((column + uv.x) / _ImpostorGrid.y, (_ImpostorGrid.z - 1.0 - row + uv.y) / _ImpostorGrid.z);
            positionOS = right * positionOS.x + float3(0.0, positionOS.y, 0.0);
        }

        // An encoded texel of the normal atlas in object space, unnormalised: view k's right, up and
        // towards-the-camera axes (lib/impostor.py basis) weighted by its components. Its length is the texel's R.
        float3 ImpostorMean(float3 encoded, float k)
        {
            float a = k * 6.28318530718 / _ImpostorGrid.x, e = radians(_ImpostorGrid.w);
            float sa = sin(a), ca = cos(a), se = sin(e), ce = cos(e);
            float3 n = encoded * 2.0 - 1.0;
            return n.x * float3(-ca, 0.0, sa) + n.y * float3(-se * sa, ce, -se * ca) + n.z * float3(ce * sa, se, ce * ca);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ImpostorPassVertex
            #pragma fragment ImpostorPassFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

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

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "CoverageAlpha.hlsl"
            // URP's surface under its own name, so LitPassFragment calls the one below.
            #define InitializeStandardLitSurfaceData UrpInitializeStandardLitSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData

            // URP/Lit's surface with the occlusion of the surface under the pixel: the normal atlas's alpha, read
            // where the fragment reads its normal, the same texel at full resolution.
            inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
            {
                UrpInitializeStandardLitSurfaceData(uv, outSurfaceData);
                outSurfaceData.occlusion = SAMPLE_TEXTURE2D_LOD(_BumpMap, sampler_BumpMap, uv, 0).a;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // --- a texel's normals as a von Mises-Fisher distribution of mean resultant length R (the header) -----

            // The concentration (Banerjee et al. 2005, HDRP's TextureNormalVariance); 1e4 for one flat surface.
            float ImpostorKappa(float R)
            {
                R = min(R, 0.9999);
                return R * (3.0 - R * R) / (1.0 - R * R);
            }

            // The clamped cosine max(0, n.l) averaged over the distribution, t = l's cosine to the mean direction:
            // S. Hill's fitted irradiance of a normalised spherical Gaussian of sharpness kappa, as M. Pettineo gives
            // it ("SG Series Part 3: Diffuse Lighting From an SG", 2016), the lobe and the cosine swapping roles. The
            // fit holds from a sharpness of 1 (R 0.31); a wider texel is lit as that one.
            float ImpostorDiffuse(float t, float kappa)
            {
                float lambda = max(kappa, 1.0);
                float eml = exp(-lambda), em2l = eml * eml, rl = 1.0 / lambda;
                float scale = 1.0 + 2.0 * em2l - rl;
                float bias = (eml - em2l) * rl - em2l;
                float x = sqrt(1.0 - scale);
                float x0 = 0.36 * t, x1 = x / (4.0 * 0.36);
                float n = x0 + x1;
                float y = abs(x0) <= x1 ? n * n / x : saturate(t);
                return scale * y + bias;
            }

            // The standard normal's distribution function (erf by Abramowitz and Stegun 7.1.26, within 1.5e-7).
            float ImpostorNormalCdf(float z)
            {
                float x = abs(z) * 0.70710678;
                float k = 1.0 / (1.0 + 0.3275911 * x);
                float erfx = 1.0 - (((((1.061405429 * k - 1.453152027) * k) + 1.421413741) * k - 0.284496736) * k + 0.254829592) * k * exp(-x * x);
                return 0.5 + 0.5 * (z < 0.0 ? -erfx : erfx);
            }

            // URP's Fresnel term Pow4(1 - saturate(n.v)) averaged over the distribution, t = v's cosine to the mean
            // direction: n.v taken as a normal variable of the distribution's own mean R t and second moment
            // 1/3 + A2 (t^2 - 1/3), A2 = 2 R^2 / (3 - R^2) (the vMF's second Legendre moment, 1 - 3 R / kappa), and
            // Pow4(1 - x) integrated over it exactly: 1 below x = 0, y^4 for y = 1 - x in (0, 1] by the normal's
            // partial moments, 0 past x = 1.
            float ImpostorFresnel(float t, float R)
            {
                float a2 = 2.0 * R * R / (3.0 - R * R);
                float m = R * t;
                float s = sqrt(max(1.0 / 3.0 + a2 * (t * t - 1.0 / 3.0) - m * m, 1e-6));
                float mu = 1.0 - m;
                float a = -mu / s, b = (1.0 - mu) / s;
                float pa = exp(-0.5 * a * a) * 0.39894228, pb = exp(-0.5 * b * b) * 0.39894228;
                float cb = ImpostorNormalCdf(b);
                float m0 = cb - ImpostorNormalCdf(a);
                float m1 = pa - pb;
                float m2 = m0 + a * pa - b * pb;
                float m3 = 2.0 * m1 + a * a * pa - b * b * pb;
                float m4 = 3.0 * m2 + a * a * a * pa - b * b * b * pb;
                float mu2 = mu * mu, s2 = s * s;
                return mu2 * mu2 * m0 + 4.0 * mu2 * mu * s * m1 + 6.0 * mu2 * s2 * m2 + 4.0 * mu * s2 * s * m3 + s2 * s2 * m4 + (1.0 - cb);
            }

            // A light on the distribution: URP's LightingPhysicallyBased with the diffuse's cosine averaged over it.
            half3 ImpostorLight(BRDFData brdfData, Light light, float3 normalWS, half3 viewDirectionWS, float kappa)
            {
                float t = dot(normalWS, light.direction);
                half3 brdf = brdfData.diffuse * ImpostorDiffuse(t, kappa);
            #ifndef _SPECULARHIGHLIGHTS_OFF
                brdf += brdfData.specular * DirectBRDFSpecular(brdfData, normalWS, light.direction, viewDirectionWS) * saturate(t);
            #endif
                return brdf * light.color * (light.distanceAttenuation * light.shadowAttenuation);
            }

            // URP 17.3's UniversalFragmentPBR (Lighting.hlsl) without clear coat, each light by ImpostorLight and the
            // sky's reflection with the distribution's Fresnel term (GlobalIllumination.hlsl's GlobalIllumination).
            half4 ImpostorFragmentPBR(InputData inputData, SurfaceData surfaceData, float R)
            {
                float kappa = ImpostorKappa(R);
                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);
            #if defined(DEBUG_DISPLAY)
                half4 debugColor;
                if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
                    return debugColor;
            #endif
                half4 shadowMask = CalculateShadowMask(inputData);
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
                uint meshRenderingLayers = GetMeshRenderingLayer();
                Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
                LightingData lightingData = CreateLightingData(inputData, surfaceData);

                half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
                half fresnelTerm = ImpostorFresnel(dot(inputData.normalWS, inputData.viewDirectionWS), R);
                half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, brdfData.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);
                half3 giColor = EnvironmentBRDF(brdfData, inputData.bakedGI, indirectSpecular, fresnelTerm);
                if (IsOnlyAOLightingFeatureEnabled())
                    giColor = half3(1, 1, 1);
                lightingData.giColor = giColor * aoFactor.indirectAmbientOcclusion;

            #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
            #endif
                    lightingData.mainLightColor = ImpostorLight(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS, kappa);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
            #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
                        lightingData.additionalLightsColor += ImpostorLight(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, kappa);
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
            #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
                        lightingData.additionalLightsColor += ImpostorLight(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, kappa);
                LIGHT_LOOP_END
            #endif

            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
            #endif
            #if REAL_IS_HALF
                return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
            #else
                return CalculateFinalColor(lightingData, surfaceData.alpha);
            #endif
            }

            Varyings ImpostorPassVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 camera = TransformWorldToObject(GetCameraPositionWS());
                ImpostorCard(camera, input.positionOS.xyz, input.texcoord);
                float3 facing, right;
                ImpostorFrame(camera, facing, right);
                input.normalOS = facing;
                input.tangentOS = float4(right, -1.0);
                return LitPassVertex(input);
            }

            // URP 17.3's LitPassFragment (LitForwardPass.hlsl) with the texel's mean normal and its spread.
            void ImpostorPassFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 facing, right;
                ImpostorFrame(TransformWorldToObject(GetCameraPositionWS()), facing, right);
                // At full resolution: each pixel shows the surfaces under its texel, as each of LOD0's pixels shows the
                // leaf or the twig under it; a mip mixes the texels round it.
                float3 encoded = SAMPLE_TEXTURE2D_LOD(_BumpMap, sampler_BumpMap, input.uv, 0).rgb;
                float3 mean = ImpostorMean(encoded, ImpostorView(facing));
                float R = saturate(length(mean));
                input.normalWS = TransformObjectToWorldNormal(mean);

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif
                InitializeBakedGIData(input, inputData);

                half4 color = ImpostorFragmentPBR(inputData, surfaceData, R);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
                outColor = color;
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ImpostorShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "CoverageAlpha.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // The card faces the light, so its shadow is the crown seen from the light's azimuth.
            Varyings ImpostorShadowPassVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 toward = TransformWorldToObject(_LightPosition);
            #else
                float3 toward = TransformWorldToObjectDir(_LightDirection);
            #endif
                ImpostorCard(toward, input.positionOS.xyz, input.texcoord);
                float3 facing, right;
                ImpostorFrame(toward, facing, right);
                input.normalOS = facing;
                return ShadowPassVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ImpostorDepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "CoverageAlpha.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            Varyings ImpostorDepthOnlyVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ImpostorCard(TransformWorldToObject(GetCameraPositionWS()), input.position.xyz, input.texcoord);
                return DepthOnlyVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ImpostorDepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "CoverageAlpha.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            // The card's own facing direction stands for the crown's normals here (screen-space effects only).
            Varyings ImpostorDepthNormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 camera = TransformWorldToObject(GetCameraPositionWS());
                ImpostorCard(camera, input.positionOS.xyz, input.texcoord);
                float3 facing, right;
                ImpostorFrame(camera, facing, right);
                input.normal = facing;
                input.tangentOS = float4(right, -1.0);
                return DepthNormalsVertex(input);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
