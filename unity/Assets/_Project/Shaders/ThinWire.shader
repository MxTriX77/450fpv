// A conductor's tube (p1-l01-l03-pole-wires, design decision 8): URP/Lit's surface, read from the same properties
// (base map and tint, the packed mask's smoothness and occlusion), alpha-blended, casting its shadow at its true
// radius. A tube thinner than a pixel misses most pixel centres and breaks into dots and dashes that crawl as the
// camera moves; this one stays one continuous line that fades with its true width.
//
// _THIN_WIRE_COVERAGE (the catalog importer enables it on every wire material): the vertex stage widens the tube about
// its centre line until even its narrowest projection (a 4-sided tube seen across a face) spans the true width plus
// one pixel on each side, and the fragment stage gives each pixel the round conductor's own coverage of it, filtered
// by a tent one pixel in radius: c = T(x + h) - T(x - h), with x the pixel's distance from the centre line and h the
// true half-width, both in pixels, and T the tent's integral. Summed across the line those coverages are the true
// width in pixels wherever the line falls between pixel centres, so a 5 mm wire at 50 m (0.09 px at 935 px/rad) is a
// continuous line at 9 % that keeps its weight as it moves: no broken segments, no crawl, no sparkle, and no minimum
// visible width (the object sheet's 270 D / d on the feed). The lit normal is the round conductor's at that offset,
// so the shading does not depend on the tube's 6 or 4 sides. Off: the plain tube at full alpha, for comparisons.
//
// The mesh (wire_span's tubes): every vertex lies on its ring the conductor's radius r from the centre line, its normal
// radial (smooth shading), and UV channel 1 (Unity's uv2, full precision under the project's vertex compression)
// holds (r, 0) in metres. A uniform scale only.
//
// The shadow (task 1.8): the round member at its radius from uv2, alpha-tested, so a pole's far LODs cast the shadow
// its URP/Lit LOD0 casts. The vertex stage pushes the tube out to the circle's circumscribed polygon and the fragment
// stage keeps the shadow-map texels within the member's half-width of its centre line, both seen along the light: the
// round member's shadow whatever the mesh's sides, without the forward pass's widening (that is screen coverage).
// URP's depth bias as URP/Lit's caster applies it; its normal bias as an inset of the half-width. URP/Lit's caster moves
// each vertex in along its own normal by up to the bias (a shadow texel, 4 cm at the game's 50 m), so LOD0's flat
// 12-sided faces give up about 0.6 of it at the silhouette (derived for the day profile's 45° sun; 0.5 at 20°, 0.8 at
// 70°), while the far LODs' radial normals would give up all of it: a fifth less shadow at the 35 m switch. A
// renderer drawing only wires casts none: the importer turns its shadows off.
Shader "Fpv450/ThinWire"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [Toggle(_THIN_WIRE_COVERAGE)] _ThinWireCoverage("Sub-pixel coverage", Float) = 1.0
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

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0 // ddx/ddy
            #pragma vertex ThinWireVertex
            #pragma fragment ThinWireFragment

            // Material keywords (the thin wire's, and URP/Lit's that a wire recipe sets)
            #pragma shader_feature_local _THIN_WIRE_COVERAGE
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP

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

            // A transparent surface: the main light's shadow comes from the cascades, not the screen-space texture
            // that holds only opaque depth.
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define THIN_WIRE_FILTER_PX 1.0       // the tent's radius in pixels
            #define THIN_WIRE_NARROWEST 0.70710678 // cos(pi / 4): a 4-sided tube's narrowest projection over its widest

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                float2 wire       : TEXCOORD1; // x: the conductor's radius in metres
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 centreWS   : TEXCOORD3; // xyz: the centre line abreast of this point; w: the radius
                half   fogFactor  : TEXCOORD4;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // The world size of one pixel of the target at the depth of positionWS.
            float PixelSizeWS(float3 positionWS)
            {
                float depth = unity_OrthoParams.w > 0.5 ? 1.0 : max(-TransformWorldToView(positionWS).z, 1e-4);
                return 2.0 * depth / (abs(UNITY_MATRIX_P._m11) * _ScaledScreenParams.y);
            }

            // The integral of a tent of unit area and radius one, from minus infinity to t.
            float TentIntegral(float t)
            {
                t = clamp(t, -1.0, 1.0);
                return t < 0.0 ? 0.5 * (1.0 + t) * (1.0 + t) : 1.0 - 0.5 * (1.0 - t) * (1.0 - t);
            }

            Varyings ThinWireVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = normalize(input.normalOS);
                float radius = input.wire.x * length(GetObjectToWorldMatrix()._m00_m10_m20);
                float3 centreWS = TransformObjectToWorld(input.positionOS.xyz - normalOS * input.wire.x);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if defined(_THIN_WIRE_COVERAGE)
                float3 positionWS = centreWS + normalWS * ((radius + THIN_WIRE_FILTER_PX * PixelSizeWS(centreWS)) / THIN_WIRE_NARROWEST);
            #else
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            #endif

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.centreWS = float4(centreWS, radius);
                output.positionCS = TransformWorldToHClip(positionWS);
            #if !defined(_FOG_FRAGMENT)
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
            #endif
                return output;
            }

            half4 ThinWireFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                float3 normalWS = normalize(input.normalWS);
                float coverage = 1.0;

            #if defined(_THIN_WIRE_COVERAGE)
                float3 centreWS = input.centreWS.xyz;
                float radius = input.centreWS.w;
                float pixel = PixelSizeWS(centreWS);
                float3 toCamera = GetWorldSpaceNormalizeViewDir(centreWS);
                // Within a triangle the centre moves only along the line, so its screen derivatives lie on it.
                float3 dx = ddx(centreWS), dy = ddy(centreWS);
                float3 along = dot(dx, dx) > dot(dy, dy) ? dx : dy;
                float3 across = cross(toCamera, along);
                float3 offset = input.positionWS - centreWS;
                float s;        // signed, across the line on screen, in metres at the centre's depth
                float3 facing;  // towards the camera, square to the line
                if (dot(across, across) > 1e-6 * dot(along, along))
                {
                    along = normalize(along);
                    across = normalize(across);
                    facing = normalize(toCamera - along * dot(toCamera, along));
                    s = dot(offset, across);
                }
                else // seen end-on: a dot
                {
                    across = offset - toCamera * dot(offset, toCamera);
                    s = length(across);
                    across /= max(s, 1e-12);
                    facing = toCamera;
                }
                float x = abs(s) / pixel, h = radius / pixel;
                coverage = TentIntegral(x + h) - TentIntegral(x - h);
                float k = clamp(s / (radius + pixel), -1.0, 1.0);
                normalWS = across * k + facing * sqrt(1.0 - k * k);
            #endif

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
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surfaceData.alpha * coverage;
                return color;
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0 // ddx/ddy
            #pragma vertex ThinWireShadowVertex
            #pragma fragment ThinWireShadowFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // URP's caster globals (_LightDirection, _LightPosition, _ShadowBias) and clamping
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            #define THIN_WIRE_NARROWEST 0.70710678 // as the forward pass
            #define THIN_WIRE_SHADOW_INSET 0.6      // of URP's normal bias, at the silhouette (the header)

            struct ThinWireShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 wire       : TEXCOORD1; // x: the member's radius in metres
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ThinWireShadowVaryings
            {
                float3 centreWS   : TEXCOORD0; // the centre line abreast of this point
                float3 offsetWS   : TEXCOORD1; // this point from it
                float  radius     : TEXCOORD2; // the shadow's half-width
                float4 positionCS : SV_POSITION;
            };

            float3 ThinWireLightDirection(float3 positionWS)
            {
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                return normalize(_LightPosition - positionWS);
            #else
                return _LightDirection;
            #endif
            }

            ThinWireShadowVaryings ThinWireShadowVertex(ThinWireShadowAttributes input)
            {
                ThinWireShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalOS = normalize(input.normalOS);
                float radius = input.wire.x * length(GetObjectToWorldMatrix()._m00_m10_m20);
                float3 centreWS = TransformObjectToWorld(input.positionOS.xyz - normalOS * input.wire.x);
                // Out to the circle's circumscribed polygon, so the round member's whole shadow is rasterised.
                float3 offsetWS = TransformObjectToWorldNormal(normalOS) * (radius / THIN_WIRE_NARROWEST);
                float3 positionWS = centreWS + offsetWS + ThinWireLightDirection(centreWS) * _ShadowBias.x; // URP's depth bias

                output.centreWS = centreWS;
                output.offsetWS = offsetWS;
                output.radius = max(radius + THIN_WIRE_SHADOW_INSET * _ShadowBias.y, 0.0); // _ShadowBias.y <= 0
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(positionWS));
                return output;
            }

            half4 ThinWireShadowFragment(ThinWireShadowVaryings input) : SV_Target
            {
                // The round member seen along the light: within its half-width of its centre line, both projected on
                // the plane square to the light. Within a triangle the centre moves only along the line, so its
                // derivatives lie on it; a cap's centre does not move, and the cap's shadow is a disc.
                float3 light = ThinWireLightDirection(input.centreWS);
                float3 offset = input.offsetWS - light * dot(input.offsetWS, light);
                float3 dx = ddx(input.centreWS), dy = ddy(input.centreWS);
                float3 along = dot(dx, dx) > dot(dy, dy) ? dx : dy;
                along -= light * dot(along, light);
                if (dot(along, along) > 1e-12)
                {
                    along = normalize(along);
                    offset -= along * dot(offset, along);
                }
                clip(input.radius * input.radius - dot(offset, offset));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
