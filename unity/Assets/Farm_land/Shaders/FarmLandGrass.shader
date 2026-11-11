// Grass for terrain details. Lights blades with a normal bent towards the sky and never flips it, so
// grass facing away from the sun doesn't go black, and dithers out with distance so no edge shows. Far
// instances are thinned out, since each covers few pixels and terrain details have no LODs. Blades sway in
// slow gusts that travel across the map, with a small flutter of their own. Lower parts of each clump are
// darker, standing in for the blades shading each other, since real grass shadows cost several ms.
Shader "Farm Land/Grass"
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
        _WindStrength("Wind Strength (m at 1 m height)", Range(0, 1)) = 0.12
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
            float _WindStrength;
            float _WindSpeed;
            float _WindGustSize;
            float4 _WindDirection;
        CBUFFER_END

        // Bends a vertex downwind by the square of its height above the instance origin, so the base stays put.
        // Gusts are bands of stronger wind moving along the wind direction, and each clump flutters on its own.
        float3 ApplyWind(float3 positionWS, float3 origin)
        {
            float height = max(0, positionWS.y - origin.y);
            float2 direction = normalize(_WindDirection.xz);
            float phase = (dot(origin.xz, direction) - _Time.y * _WindSpeed) / _WindGustSize;
            float gust = 0.55 + 0.45 * sin(phase * 6.2832) * sin(phase * 2.31 + 1.7);
            float random = frac(sin(dot(origin.xz, float2(39.3468, 11.1352))) * 24634.6345);
            float flutter = 0.25 * sin(_Time.y * (2.2 + random) + random * 6.2832);
            float bend = _WindStrength * (gust + flutter) * height * height;
            positionWS.xz += direction * bend;
            positionWS.y -= 0.3 * bend * bend / max(height, 0.1);
            return positionWS;
        }

        // The instance's position, read straight from its matrix.
        float3 InstanceOrigin()
        {
            return GetObjectToWorldMatrix()._m03_m13_m23;
        }

        // False once the instance's random value passes the share kept at its distance. Thinned instances
        // collapse to a point, which saves rasterising their triangles and skips the wind.
        bool KeepInstance(float3 origin)
        {
            float share = lerp(1, _ThinKeep, saturate((distance(origin, _WorldSpaceCameraPos) - _ThinStart) / (_ThinEnd - _ThinStart)));
            float random = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
            return random < share;
        }

        // Clips the blade shape, then removes more pixels with distance using screen-space noise.
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

                float3 origin = InstanceOrigin();
                bool keep = KeepInstance(origin);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.height = positionWS.y - origin.y;
                if (keep)
                    positionWS = ApplyWind(positionWS, origin);
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

                float3 origin = InstanceOrigin();
                bool keep = KeepInstance(origin);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (keep)
                    output.positionWS = ApplyWind(output.positionWS, origin);
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
