// The farmland's grass in the flight's air (w16-plants-in-the-air 1.3): the project's own replacement for the friend's
// "Farm Land/Grass" (Farm_land/Shaders/FarmLandGrass.shader, which stays as he left it), drawn through runtime copies of his
// materials (FarmlandPlantDrawing). His look is kept as he wrote it: blades lit with a normal bent towards the sky, a darker base
// standing in for the blades shading each other, far instances thinned, a screen-space dither to the draw distance. His wind is
// gone: each clump bends by its culms' bend in the sim's air (PlantBend.hlsl: 1.2's law, its class's bands at its length, read
// at its base), as a cantilever from its base, so a gust bends it and it springs back as the law gives and the wash flattens it.
Shader "Fpv450/Plant Grass"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Mask Map (G occlusion)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Clip Threshold", Range(0, 1)) = 0.5
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 0.5
        _NormalUp("Normal Up Share", Range(0, 1)) = 0.6
        _BaseShade("Light Left at the Base", Range(0, 1)) = 0.4
        _ShadeHeight("Height Fully Lit (m)", Float) = 0.8
        _FadeStart("Distance Fade Start", Float) = 150
        _FadeEnd("Distance Fade End", Float) = 195
        _ThinStart("Thinning Start", Float) = 50
        _ThinEnd("Thinning End", Float) = 130
        _ThinKeep("Share Kept Past Thinning End", Range(0, 1)) = 0.35
        _PlantClass("Culms (0 green, 1 dry, 2 burnt)", Float) = 0
        _PlantMeshTop("The Mesh's Top at Scale 1 (m)", Float) = 0.5
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
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _OcclusionStrength;
            half _NormalUp;
            half _BaseShade;
            float _ShadeHeight;
            float _FadeStart;
            float _FadeEnd;
            float _ThinStart;
            float _ThinEnd;
            half _ThinKeep;
            float _PlantClass;
            float _PlantMeshTop;
        CBUFFER_END

        // The clump bent by its culms' bend in the air, read at its base: its length is the mesh's top at the instance's scale.
        float3 ApplyAir(float3 positionWS, float3 origin)
        {
            float culm = _PlantMeshTop * PlantScale();
            return PlantTurn(positionWS, origin.y, GrassBend((int)_PlantClass, culm, origin.xz), culm);
        }

        // False once the instance's random value passes the share kept at its distance (the friend's thinning). Thinned
        // instances collapse to a point, which saves rasterising their triangles and skips the air.
        bool KeepInstance(float3 origin)
        {
            float share = lerp(1, _ThinKeep, saturate((distance(origin, _WorldSpaceCameraPos) - _ThinStart) / (_ThinEnd - _ThinStart)));
            float random = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
            return random < share;
        }

        // Clips the blade shape, then removes more pixels with distance using screen-space noise (the friend's).
        half4 SampleBaseAndClip(float2 uv, float3 positionWS, float2 positionCS)
        {
            half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            clip(base.a - _Cutoff);

            float fade = saturate((_FadeEnd - distance(positionWS, _WorldSpaceCameraPos)) / (_FadeEnd - _FadeStart));
            float noise = frac(52.9829189 * frac(dot(positionCS, float2(0.06711056, 0.00583715))));
            clip(fade - noise - 0.001);
            return base;
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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float height : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 origin = PlantOrigin();
                bool keep = KeepInstance(origin);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.height = positionWS.y - origin.y;
                if (keep)
                    positionWS = ApplyAir(positionWS, origin);
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionCS = keep ? positionCS : float4(0, 0, 0, 0);
                output.positionWS = positionWS;
                output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0, 1, 0), _NormalUp));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 base = SampleBaseAndClip(input.uv, input.positionWS, input.positionCS.xy);
                half3 albedo = base.rgb * _BaseColor.rgb;
                half occlusion = lerp(1, SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).g, _OcclusionStrength);

                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Inside a clump, less sky and less sun reach the lower blades.
                half shade = lerp(_BaseShade, 1, smoothstep(0, _ShadeHeight, input.height));
                half3 direct = light.color * light.shadowAttenuation * saturate(dot(normalWS, light.direction)) * lerp(shade, 1, 0.35);
                half3 ambient = SampleSH(normalWS) * occlusion * shade;

                half3 color = albedo * (direct + ambient);
                return half4(MixFog(color, input.fogFactor), 1);
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

                float3 origin = PlantOrigin();
                bool keep = KeepInstance(origin);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (keep)
                    output.positionWS = ApplyAir(output.positionWS, origin);
                output.positionCS = keep ? TransformWorldToHClip(output.positionWS) : float4(0, 0, 0, 0);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Fragment(Varyings input) : SV_Depth
            {
                SampleBaseAndClip(input.uv, input.positionWS, input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
