// Foliage for every tree, since URP has no light transmission and flat foliage cards otherwise go black on
// their shaded side. Light wraps past the shadow line and shines through leaves seen against the sun. The
// island tree's normals point out of its crown and its vertex colours hold baked occlusion and tint. Pine
// meshes use their vertex colours for something else, so they turn those off and bend normals up instead.
// Every pass moves vertices with the shared tree wind, so shadows and depth follow the swaying crown.
// Desaturate turns green foliage grey before the tint, so a warm tint can make a dried-out copy of it.
Shader "Farm Land/Tree Leaves"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Desaturate("Desaturate", Range(0, 1)) = 0
        _Cutoff("Alpha Clip Threshold", Range(0, 1)) = 0.5
        _Wrap("Light Wrap", Range(0, 1)) = 0.5
        _Translucency("Translucency", Range(0, 2)) = 0.6
        _VertexColorShare("Vertex Color Share", Range(0, 1)) = 1
        _NormalUp("Normal Up Share", Range(0, 1)) = 0
        _ReceiveShadows("Receive Shadows", Range(0, 1)) = 1
        _EdgeFade("Hide Cards Seen Edge-On", Range(0, 1)) = 0
        _TreeSway("Sway (m at 10 m height)", Range(0, 1)) = 0.15
        _TreeFlutter("Leaf Flutter (m)", Range(0, 0.3)) = 0.08
        _WindSpeed("Gust Speed (m/s)", Float) = 6
        _WindGustSize("Gust Size (m)", Float) = 35
        _WindDirection("Wind Direction (XZ)", Vector) = (0.8, 0, 0.6, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Desaturate;
            half _Cutoff;
            half _Wrap;
            half _Translucency;
            half _VertexColorShare;
            half _NormalUp;
            half _ReceiveShadows;
            half _EdgeFade;
            float _TreeSway;
            float _TreeFlutter;
            float _WindSpeed;
            float _WindGustSize;
            float4 _WindDirection;
        CBUFFER_END

        #include "FarmLandTreeWind.hlsl"

        // Impostor cards turn into a thin line when seen edge-on, and the other cards cover the gap. Cards are
        // upright, so this only compares directions around them. Otherwise looking down at a tree from high
        // up would count as edge-on for every card, and only its shadow would be left.
        void ClipEdgeOnCard(float3 positionWS)
        {
            float3 cardNormal = normalize(cross(ddx(positionWS), ddy(positionWS)));
            float3 aroundView = GetWorldSpaceNormalizeViewDir(positionWS);
            aroundView.y = 0;
            clip(abs(dot(cardNormal, normalize(aroundView + float3(0.0001, 0, 0)))) - _EdgeFade);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half3 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyTreeWind(TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0, 1, 0), _NormalUp));
                output.color = lerp(half3(1, 1, 1), input.color.rgb, _VertexColorShare);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(base.a - _Cutoff);
                ClipEdgeOnCard(input.positionWS);

                half3 albedo = lerp(base.rgb, dot(base.rgb, half3(0.299, 0.587, 0.114)), _Desaturate) * input.color * _BaseColor.rgb;
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half wrapped = saturate((dot(normalWS, light.direction) + _Wrap) / (1 + _Wrap));
                half through = saturate(dot(-viewWS, light.direction));
                half shadow = lerp(1, light.shadowAttenuation, _ReceiveShadows);
                half3 direct = light.color * shadow * (wrapped + _Translucency * through * through);
                half3 ambient = SampleSH(normalWS);

                return half4(MixFog(albedo * (direct + ambient), input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = ApplyTreeWind(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = ApplyTreeWind(TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Fragment(Varyings input) : SV_Depth
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                ClipEdgeOnCard(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
