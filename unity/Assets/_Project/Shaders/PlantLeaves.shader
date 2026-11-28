// The farmland's foliage in the flight's air (w16-plants-in-the-air 1.3): the project's own replacement for the friend's
// "Farm Land/Tree Leaves" (Farm_land/Shaders/FarmLandTreeLeaves.shader, which stays as he left it), drawn through runtime
// copies of his materials (FarmlandPlantDrawing). His look is kept as he wrote it: light wraps past the shadow line and shines
// through leaves seen against the sun, the island tree's vertex colours hold its baked occlusion and tint, the pines bend their
// normals up, impostor cards seen edge-on are hidden, and a desaturated tint makes the dried-out copies. His wind is gone: every
// pass moves the vertices by the sim's air (PlantBend.hlsl, 1.2's law): the whole plant's sway at its base, and the foliage on
// its branches and twigs where it hangs (cards and impostors by the sway only), so shadows and depth follow the crown.
Shader "Fpv450/Plant Leaves"
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
        _PlantSway("Sway Band", Float) = 12
        _PlantHeight("Drawn Height at Scale 1 (m)", Float) = 10
        _PlantTwig("Twig Band", Float) = 9
        _PlantBranch("On Branches (1) or a Bush's Stems (0)", Float) = 1
        _PlantFoliage("Foliage (1) or Sway Only (0)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PlantBend.hlsl"

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
            float _PlantSway;
            float _PlantHeight;
            float _PlantTwig;
            float _PlantBranch;
            float _PlantFoliage;
        CBUFFER_END

        float3 ApplyAir(float3 positionWS)
        {
            return PlantTree(positionWS, (int)_PlantSway, _PlantHeight, (int)_PlantTwig, _PlantBranch, _PlantFoliage);
        }

        // Impostor cards turn into a thin line when seen edge-on, and the other cards cover the gap. Cards are upright, so this
        // only compares directions around them (the friend's).
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
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // The wet look while it rains (w21-rain-on-camera 1.3, WetLaw.hlsl): the wetted body; no specular to wet.
            #pragma multi_compile_fragment _ FPV450_WET
            #include "WetLaw.hlsl"

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

                float3 positionWS = ApplyAir(TransformObjectToWorld(input.positionOS.xyz));
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
                #if defined(FPV450_WET)
                    albedo = Fpv450WetAlbedo(albedo);
                #endif
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
            #pragma target 4.5
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

                float3 positionWS = ApplyAir(TransformObjectToWorld(input.positionOS.xyz));
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
            #pragma target 4.5
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

                output.positionWS = ApplyAir(TransformObjectToWorld(input.positionOS.xyz));
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
