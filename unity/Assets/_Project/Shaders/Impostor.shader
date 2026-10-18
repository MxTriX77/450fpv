// The impostor card (p1-v01-locust, design decision 3; asset-pipeline "Baked impostor LOD"): the far LOD of a tree or
// a clump, an upright quad that the vertex stage turns about the asset's vertical axis to face the camera (in the
// shadow pass, the light), showing the atlas cell of the nearest of the views tools/blender/lib/impostor.py renders.
// The card mesh, built by the catalog importer, holds the quad in its own frame: x across it, y up it, 0 on the axis.
// The views are _ImpostorGrid.x orthographic views from the azimuths k * 360 / views (from +Z towards +X, object
// space), view k in cell (k mod columns, k div columns) from the atlas's top left. The atlas holds albedo; the card is
// lit as URP/Lit lights a surface, with the crown's normal per pixel (lib/impostor.py card_normals, which this
// follows): the normal of the ellipsoid round the crown (_ImpostorCrown: centre, up share; _ImpostorRadii: horizontal,
// vertical) on the side facing the camera (above and below the crown, an upright cylinder's), leaning up by the up
// share. The crown's occlusion reaches URP/Lit's
// ambient term as an occlusion map does, through the default black _OcclusionMap at strength 1 - occlusion. It casts
// shadows facing the light and receives none (the material sets _RECEIVE_SHADOWS_OFF): the light-facing caster would
// shadow the camera-facing card along their shared axis. Each pass includes URP's own Lit pass and wraps its vertex
// function (and the forward pass its fragment), as Fpv450/Leaf does; the extra vectors stand outside URP/Lit's
// per-material buffer, so the SRP batcher draws the card on its own. No wind.
Shader "Fpv450/Impostor"
{
    Properties
    {
        [MainTexture] _BaseMap("Atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 0.0
        _OcclusionMap("Occlusion", 2D) = "black" {}
        _ImpostorGrid("Views, columns, rows", Vector) = (8, 4, 2, 0)
        _ImpostorCard("Card width, height, bottom", Vector) = (1, 2, 0, 0)
        _ImpostorCrown("Crown centre x, y, z, up share", Vector) = (0, 1, 0, 0)
        _ImpostorRadii("Crown radii horizontal, vertical", Vector) = (1, 1, 0, 0)

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
        float4 _ImpostorGrid;   // x: views, y: columns, z: rows
        float4 _ImpostorCard;   // x: width, y: height, z: bottom, in the card's frame
        float4 _ImpostorCrown;  // xyz: the crown's centre (object space), w: the up share of its normals
        float4 _ImpostorRadii;  // x: the crown's horizontal radius, y: its vertical one

        // The horizontal direction from the vertical axis towards `toward` (an object-space point or direction), and
        // the card's right across it.
        void ImpostorFrame(float3 toward, out float3 facing, out float3 right)
        {
            float2 d = toward.xz;
            float len = length(d);
            facing = len > 1e-5 ? float3(d.x, 0.0, d.y) / len : float3(0.0, 0.0, 1.0);
            right = float3(-facing.z, 0.0, facing.x);
        }

        // Turns the card point positionOS (x across, y up) to face `toward` and maps uv (0..1 over the card) into the
        // nearest view's cell.
        void ImpostorCard(float3 toward, inout float3 positionOS, inout float2 uv)
        {
            float3 facing, right;
            ImpostorFrame(toward, facing, right);
            float views = _ImpostorGrid.x;
            float k = fmod(round(atan2(facing.x, facing.z) * views / 6.28318530718) + views, views);
            float column = fmod(k, _ImpostorGrid.y);
            float row = floor(k / _ImpostorGrid.y);
            uv = float2((column + uv.x) / _ImpostorGrid.y, (_ImpostorGrid.z - 1.0 - row + uv.y) / _ImpostorGrid.z);
            positionOS = right * positionOS.x + float3(0.0, positionOS.y, 0.0);
        }

        // The crown's normal (object space) at the atlas uv of a card facing `facing`.
        float3 ImpostorNormal(float2 uv, float3 facing, float3 right)
        {
            float across = (frac(uv.x * _ImpostorGrid.y) - 0.5) * _ImpostorCard.x;
            float height = _ImpostorCard.z + frac(uv.y * _ImpostorGrid.z) * _ImpostorCard.y;
            float2 p = float2((across - dot(_ImpostorCrown.xyz * float3(1.0, 0.0, 1.0), right)) / _ImpostorRadii.x,
                              (height - _ImpostorCrown.y) / _ImpostorRadii.y);
            if (abs(p.y) >= 1.0) // above or below the crown, where trunks and stems stand: an upright cylinder's
                p = float2(clamp(p.x, -1.0, 1.0), 0.0);
            p /= max(length(p), 1.0);
            float3 n = p.x * right + float3(0.0, p.y, 0.0) + sqrt(max(1.0 - dot(p, p), 0.0)) * facing;
            float up = _ImpostorCrown.w;
            return normalize(n * sqrt(1.0 - up * up) + float3(0.0, up, 0.0));
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
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
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

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

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

            void ImpostorPassFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 facing, right;
                ImpostorFrame(TransformWorldToObject(GetCameraPositionWS()), facing, right);
                input.normalWS = TransformObjectToWorldNormal(ImpostorNormal(input.uv, facing, right));
                LitPassFragment(input, outColor
            #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
            #endif
                );
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
