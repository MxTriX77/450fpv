// The look setup's sky (p1-g1-look-lock-gallery design 4): a skybox that grades from the horizon to the zenith as
// the elevation's sine to the zenith exponent, and from the horizon down to the distant land over a few degrees below
// it, plus the sun's disc and a halo around it. LookModel.Sky is the same gradient on the CPU (the ambient probe and
// the reflection are projected from it), so the two must change together. Every colour is linear; LookSetup sets
// them as vectors so Unity does not convert them.
Shader "Fpv450/GradientSky"
{
    Properties
    {
        _SkyZenith("Zenith (linear)", Vector) = (0.13, 0.26, 0.55, 1)
        _SkyHorizon("Horizon (linear)", Vector) = (0.42, 0.5, 0.62, 1)
        _SkyGround("Distant land below the horizon (linear)", Vector) = (0.2, 0.19, 0.16, 1)
        _SkyZenithExponent("Zenith exponent", Float) = 0.5
        _SkyGroundFade("Horizon-to-land fade, sine of the depression", Float) = 0.03
        _SunDirection("Towards the sun", Vector) = (0, 0.7071, -0.7071, 0)
        _SunDisc("Sun disc radiance (linear)", Vector) = (0, 0, 0, 0)
        _SunHalo("Sun halo radiance (linear)", Vector) = (0, 0, 0, 0)
        _SunCosRadius("Cosine of the disc's angular radius", Float) = 0.9999889
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyZenith;
                float4 _SkyHorizon;
                float4 _SkyGround;
                float4 _SunDirection;
                float4 _SunDisc;
                float4 _SunHalo;
                float _SkyZenithExponent;
                float _SkyGroundFade;
                float _SunCosRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float3 sky = d.y >= 0.0
                    ? lerp(_SkyHorizon.rgb, _SkyZenith.rgb, pow(saturate(d.y), _SkyZenithExponent))
                    : lerp(_SkyHorizon.rgb, _SkyGround.rgb, saturate(-d.y / _SkyGroundFade));
                float c = dot(d, _SunDirection.xyz);
                float halo = pow(saturate(c), 48.0);
                float disc = smoothstep(_SunCosRadius - 1.5e-5, _SunCosRadius, c); // a soft edge about 0.1° wide
                sky += _SunHalo.rgb * halo + _SunDisc.rgb * disc * step(0.0, d.y);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
