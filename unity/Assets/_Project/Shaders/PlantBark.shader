// The farmland's wood in the flight's air (w16-plants-in-the-air 1.3): the project's own replacement for the friend's
// "Farm Land/Tree Bark" (Farm_land/Shaders/FarmLandTreeBark.shader, which stays as he left it), drawn through runtime copies of
// his materials (FarmlandPlantDrawing). His look is kept as he wrote it: URP Lit's lighting on an opaque surface with a normal
// map, an occlusion map in green and a constant smoothness. His wind is gone: every pass moves the vertices by the sim's air
// (PlantBend.hlsl, 1.2's law): trunks, stumps and limbs by the whole plant's sway alone (the pilot: the leaves breathe, "but not
// the pillars"), the island tree's twigs also on its branches and twigs, as its leaves, so they stay on them.
Shader "Fpv450/Plant Bark"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _OcclusionMap("Occlusion Map (G)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _PlantSway("Sway Band", Float) = 12
        _PlantHeight("Drawn Height at Scale 1 (m)", Float) = 10
        _PlantTwig("Twig Band", Float) = 9
        _PlantBranch("On Branches (1) or a Bush's Stems (0)", Float) = 1
        _PlantFoliage("Twigs (1) or Wood (0)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PlantBend.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap);
        SAMPLER(sampler_OcclusionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _BumpScale;
            half _OcclusionStrength;
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

        half3 SampleNormalTS(float2 uv)
        {
            #if defined(_NORMALMAP)
                return UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
            #else
                return half3(0, 0, 1);
            #endif
        }

        float3 PerPixelNormal(float2 uv, float3 normalWS, float4 tangentWS)
        {
            #if defined(_NORMALMAP)
                float3 bitangentWS = tangentWS.w * cross(normalWS, tangentWS.xyz);
                return NormalizeNormalPerPixel(TransformTangentToWorld(SampleNormalTS(uv), half3x3(tangentWS.xyz, bitangentWS, normalWS)));
            #else
                return NormalizeNormalPerPixel(normalWS);
            #endif
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
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local_fragment _ _OCCLUSIONMAP
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionWS = ApplyAir(TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normals.normalWS;
                output.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                surface.alpha = 1;
                surface.smoothness = _Smoothness;
                surface.normalTS = SampleNormalTS(input.uv);
                #if defined(_OCCLUSIONMAP)
                    surface.occlusion = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
                #else
                    surface.occlusion = 1;
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = PerPixelNormal(input.uv, normalize(input.normalWS), input.tangentWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                return half4(MixFog(color.rgb, inputData.fogCoord), 1);
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = ApplyAir(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Fragment() : SV_Target
            {
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(ApplyAir(TransformObjectToWorld(input.positionOS.xyz)));
                return output;
            }

            half Fragment(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // Screen-space ambient occlusion reads its normals from this pass.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #if defined(_GBUFFER_NORMALS_OCT)
                #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = TransformWorldToHClip(ApplyAir(TransformObjectToWorld(input.positionOS.xyz)));
                output.normalWS = normals.normalWS;
                output.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = PerPixelNormal(input.uv, normalize(input.normalWS), input.tangentWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
