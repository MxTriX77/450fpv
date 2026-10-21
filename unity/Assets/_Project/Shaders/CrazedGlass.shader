// A glass pane (p1-r01-vaz-sedan, design decision 6): a transparent, double-sided dielectric with a slight green tint,
// and on a crazed pane its crack mask as an opacity and a normal perturbation. The pane is geometry (a 5 mm slab, its
// hole cut from the mesh, REAL-05); this shader only shades it, so nothing here opens or closes a hole.
//
// Light, in one premultiplied-alpha forward pass (URP/Lit's "Premultiply" blend, the mode it gives glass): the
// reflection (the sun's highlight and the environment's, at full strength) plus the body's own scatter times its
// opacity, over the background times what the pane lets through. The reflectance at normal incidence is the
// dielectric's F0 = ((n - 1) / (n + 1))^2 from the recipe's index of refraction (n 1.52 gives 4.3 %); towards grazing it
// rises as URP's environment term does, and the pane lets through only what it neither reflects nor stops:
// a = 1 - (1 - opacity) (1 - reflected), so a raked windscreen mirrors the sky without adding light. Both faces of the
// slab draw (cull off, a back face lit with its normal flipped), as both of a real pane's faces reflect. No shadow
// caster: clear glass passes the sun, so the cabin behind it stays lit; it receives shadows from the cascades.
//
// The body: _BaseMap x _BaseColor (a palette's flat colour times the recipe's tint) is what it scatters back, and the
// tint's alpha the share of the background each face stops (absorption and dust; a closed slab draws two faces).
//
// The crack mask (R, linear, 0 clear to 1 a crack line; the importer binds it per pane and maps it over the pane's UVs
// with _CrackMask_ST, clamped): a crack line covers _CrackOpacity of the pane at full mask and scatters _CrackColor, so
// a line is pale against the cabin and mips that average lines below a pixel leave a milky haze as dense crazing does;
// and its relief is a groove _CrackDepth texels deep, whose slope tilts the normal along each line's edges, so the
// lines catch the sun and break the reflection. An intact pane's material has no mask: the default black, no cracks.
Shader "Fpv450/CrazedGlass"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Tint (A: each face's opacity)", Color) = (0.4236, 0.4236, 0.4236, 0.02) // sRGB: a gain of 0.15
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.95
        _IOR("Index of refraction", Range(1.0, 2.0)) = 1.52

        _CrackMask("Crack mask (R)", 2D) = "black" {}
        _CrackColor("Crack colour", Color) = (0.663, 0.741, 0.706, 1) // #a9bdb4, edge-on glass (vehicles sheet, K07)
        _CrackOpacity("Crack opacity", Range(0.0, 1.0)) = 0.9
        _CrackDepth("Crack relief (texels)", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CrazedGlassVertex
            #pragma fragment CrazedGlassFragment

            // Universal Pipeline keywords (URP/Lit's forward pass, without lightmaps, decals and SSAO)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            // A transparent surface (the main light's shadow from the cascades, not the screen-space texture that holds
            // only opaque depth), premultiplied (URP's BRDF multiplies the diffuse by alpha and leaves the specular
            // whole), with its specular colour given (the F0 of the index of refraction).
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #define _SPECULAR_SETUP 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CrackMask);  SAMPLER(sampler_CrackMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _CrackMask_ST;
                float4 _CrackMask_TexelSize;
                half4 _BaseColor;
                half4 _CrackColor;
                half _Smoothness;
                half _IOR;
                half _CrackOpacity;
                half _CrackDepth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3; // w: the bitangent's sign
                half   fogFactor  : TEXCOORD4;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CrazedGlassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.uv = input.texcoord;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.positionCS = position.positionCS;
            #if !defined(_FOG_FRAGMENT)
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
            #endif
                return output;
            }

            half4 CrazedGlassFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // The crack lines and the slope of their groove, by central differences one texel either side.
                float2 crackUV = TRANSFORM_TEX(input.uv, _CrackMask);
                float2 du = float2(_CrackMask_TexelSize.x, 0.0), dv = float2(0.0, _CrackMask_TexelSize.y);
                half crack = SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, crackUV).r;
                half slopeU = SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, crackUV + du).r - SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, crackUV - du).r;
                half slopeV = SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, crackUV + dv).r - SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, crackUV - dv).r;
                // height = -crack * depth, so the normal (-dh/du, -dh/dv, 1) leans towards the groove's middle
                half3 normalTS = normalize(half3(0.5 * _CrackDepth * slopeU, 0.5 * _CrackDepth * slopeV, 1.0));

                float sgn = IS_FRONT_VFACE(face, 1.0, -1.0); // a back face is lit from its own side
                float3 n = normalize(input.normalWS) * sgn;
                float3 t = normalize(input.tangentWS.xyz);
                float3 b = cross(normalize(input.normalWS), t) * input.tangentWS.w;
                float3 normalWS = normalize(t * normalTS.x + b * normalTS.y + n * normalTS.z);

                // The body and the crack lines over it, as one layer: its coverage and its (unpremultiplied) albedo.
                half4 body = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv, _BaseMap)) * _BaseColor;
                half lines = crack * _CrackOpacity;
                half alpha = body.a + lines * (1.0 - body.a);
                half3 albedo = (body.rgb * body.a * (1.0 - lines) + _CrackColor.rgb * lines) / max(alpha, HALF_MIN);
                half f0 = Sq((_IOR - 1.0) / (_IOR + 1.0));

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(f0, f0, f0);
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = alpha;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // What the pane reflects of the background's view, as URP's environment term weights the reflection.
                half roughness = max(PerceptualRoughnessToRoughness(PerceptualSmoothnessToPerceptualRoughness(_Smoothness)), HALF_MIN_SQRT);
                half grazing = saturate(_Smoothness + f0);
                half NoV = saturate(dot(normalWS, inputData.viewDirectionWS));
                half reflected = lerp(f0, grazing, Pow4(1.0 - NoV)) / (roughness * roughness + 1.0);
                color.a = 1.0 - (1.0 - alpha) * (1.0 - reflected);
                color.rgb = MixFogColor(color.rgb, half3(unity_FogColor.rgb) * color.a, inputData.fogCoord); // premultiplied fog
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
