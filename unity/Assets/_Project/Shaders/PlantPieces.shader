// The pieces in the air round the drone (w16-plants-in-the-air 1.4, design.md "## 1.4"; PlantPieces, PlantPiecesView): each tick's
// pieces as PlantPieces.Snapshot holds them (nine float4s a slot), drawn at the frame's drawn time, a tick behind the field as the
// wash is, between the tick's sub-step points.
//
// Pass 0, "Pieces" (PlantPiecesView's pass, after the transparents, at the render's resolution, tested against its depth): a cut piece or a bit as the camera exposes it. Its centre and its
// sweep over the exposure (RollingShutter.ExposureS) from its path at the drawn time ± half the exposure; its own extent, its
// length along its tumbling axis and its width (a straw's diameter whatever the view, a flat bit's foreshortened); the quad is the
// screen's bounding box of the piece swept along its motion, one pixel wider each side, and its alpha the piece's own projected
// area over the quad's: each pixel covered for the share of the exposure the piece spends on it, on average (motion blur and
// sub-pixel coverage in one rule, so a far or fast piece fades rather than sparkles). Lit by the sun (its shadow) and the sky.
//
// Pass 1, "Dust" (PlantPiecesView's pass, a quarter-resolution target): each puff an anisotropic Gaussian of mass m, σ_h across
// and σ_v up; its optical depth along each pixel's ray in closed form, τ = k·m/(2π·σ_h²·σ_v·|d'|)·e^(−b'²/2)·(Φ(|d'|(s_b − s*)) −
// Φ(|d'|(s_a − s*))), the ray scaled by the puff's σ's (d' its direction, b' its closest approach, s* where), from the camera's
// near plane to the scene's surface on it (the camera's depth texture): exact for the Gaussian, soft against every surface,
// unclipped when the camera is inside a puff. Each puff adds (τ·colour, τ): the sum is the same in any order.
//
// Pass 2, "Composite": the quarter target over the scene, α = 1 − e^(−Στ), colour Σ(τ·c)/Στ·α (a well-mixed medium's).
Shader "Fpv450/Plant Pieces"
{
    Properties
    {
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    #define SUB_STEPS 4      // PlantPieces.SubSteps
    #define LIE 0.5          // PlantPieces.Lie, s
    #define KIND_DUST 1
    #define KIND_CUT 2
    #define KIND_CHAFF 3
    #define KIND_STRAW 4
    #define KIND_LEAF 5

    struct Piece
    {
        float4 p[SUB_STEPS + 1]; // the path's points; w: kind, birth, lay (s from T0), the seed's fraction, the tumble's rate
        float4 size;             // length, width, σ_h at T0, σ_h at T1
        float4 dust;             // σ_v at T0, σ_v at T1, k·m (m²) or 1, colour
        float4 axis;             // the piece's axis at its birth
        float4 tumble;           // the tumble's axis
    };

    StructuredBuffer<Piece> _Pieces;
    float4 _PieceTime;   // the drawn time (s from the snapshot's T0), the tick (s), the sub-step (s), the exposure (s)
    int _PieceOffset;    // pass 0's first slot
    float4 _DustTarget;  // pass 1's target: width, height, 1/width, 1/height

    // The palette (PlantPieces.Colours, sRGB): loam dust, ash, brick dust, paving's film, green culm, dry culm, burnt culm, chaff,
    // straw, dead leaf. The loam's dust matched to clip F's veil through the feed (its thick veil's median 146, 125, 97 at 8 s, its
    // lower third's 161–170, 127–132, 80–91; the drawn veil's over the tilled field was 158, 145, 121 with 0.72, 0.62, 0.47, so its
    // green and blue were scaled by 0.93 and 0.87: measured); the others assumed.
    static const float3 Palette[10] =
    {
        float3(0.72, 0.58, 0.41), float3(0.22, 0.21, 0.20), float3(0.62, 0.45, 0.38), float3(0.55, 0.55, 0.53),
        float3(0.33, 0.42, 0.18), float3(0.70, 0.62, 0.45), float3(0.10, 0.09, 0.08),
        float3(0.75, 0.68, 0.50), float3(0.72, 0.64, 0.46), float3(0.45, 0.32, 0.18),
    };

    float3 Albedo(float index)
    {
        return SRGBToLinear(Palette[(int)clamp(index, 0, 9)]);
    }

    // The path at time t (s from T0): its sub-step points, linearly between.
    float3 PathAt(Piece q, float t)
    {
        float f = clamp(t / _PieceTime.z, 0, SUB_STEPS - 0.0001);
        int i = (int)floor(f);
        return lerp(q.p[i].xyz, q.p[i + 1].xyz, f - i);
    }

    // Rodrigues: v turned by angle about the unit axis n.
    float3 Turn(float3 v, float3 n, float angle)
    {
        float s, c;
        sincos(angle, s, c);
        return v * c + cross(n, v) * s + n * dot(n, v) * (1 - c);
    }

    // The sun at a point (its shadow) and the sky.
    void SunAndSky(float3 positionWS, out float3 sunColour, out float3 sunDirection, out float3 sky)
    {
        Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
        sunColour = sun.color * sun.shadowAttenuation;
        sunDirection = sun.direction;
        sky = 0.5 * (SampleSH(float3(0, 1, 0)) + SampleSH(float3(0, -1, 0)));
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Pieces"
            Tags { "LightMode" = "PlantPieces" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PieceVertex
            #pragma fragment PieceFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float4 colour : COLOR;
                float fogFactor : TEXCOORD0;
            };

            Varyings PieceVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                Varyings o = (Varyings)0;
                Piece q = _Pieces[instance + _PieceOffset];
                float kind = q.p[0].w, born = q.p[1].w, lay = q.p[2].w, rate = q.p[4].w;
                float t = _PieceTime.x;
                if (kind < KIND_CUT || t < born)
                {
                    o.positionCS = float4(0, 0, -2, 1); // behind the near plane: nothing drawn
                    return o;
                }
                // The sweep over the exposure, inside the piece's life over the tick.
                float halfExposure = 0.5 * _PieceTime.w, start = max(born, 0);
                float3 a0 = PathAt(q, clamp(t - halfExposure, start, _PieceTime.y)), a1 = PathAt(q, clamp(t + halfExposure, start, _PieceTime.y));
                float3 centre = 0.5 * (a0 + a1), sweep = 0.5 * (a1 - a0);
                // Its axis, turned by its tumble (held from the moment it lay down).
                float age = min(t, lay) - born;
                float3 axis = normalize(Turn(q.axis.xyz, q.tumble.xyz, rate * age));
                float3 face = normalize(cross(axis, q.tumble.xyz) + 1e-6);
                bool flat = kind == KIND_CHAFF || kind == KIND_LEAF;

                // The view plane at the centre's depth: each extent's (x, y) over its depth, in pixels.
                float3 c = TransformWorldToView(centre);
                float depth = max(-c.z, 1e-3);
                float focal = 0.5 * _ScreenParams.y * abs(UNITY_MATRIX_P[1][1]);
                float scale = focal / depth;
                float2 A = mul((float3x3)UNITY_MATRIX_V, axis * (0.5 * q.size.x)).xy * scale;
                float2 S = mul((float3x3)UNITY_MATRIX_V, sweep).xy * scale;
                float2 B;
                if (flat) B = mul((float3x3)UNITY_MATRIX_V, cross(axis, face) * (0.5 * q.size.y)).xy * scale;
                else
                {
                    float2 across = length(A) > 1e-6 ? normalize(float2(-A.y, A.x)) : float2(1, 0);
                    B = across * (0.5 * q.size.y * scale);
                }
                // The box: its long axis along the longer of the piece and its sweep; each half-extent the three vectors' reach,
                // and a pixel's margin each side.
                float2 e1 = dot(S, S) > dot(A, A) ? S : A;
                e1 = length(e1) > 1e-6 ? normalize(e1) : float2(1, 0);
                float2 e2 = float2(-e1.y, e1.x);
                float h1 = abs(dot(A, e1)) + abs(dot(B, e1)) + abs(dot(S, e1)) + 1;
                float h2 = abs(dot(A, e2)) + abs(dot(B, e2)) + abs(dot(S, e2)) + 1;
                float own = 4 * abs(A.x * B.y - A.y * B.x);
                float coverage = saturate(own / (4 * h1 * h2));
                if (t > lay) coverage *= saturate(1 - (t - lay) / LIE);

                float2 corner = float2((vertex == 1 || vertex == 2 || vertex == 4) ? 1 : -1, (vertex == 2 || vertex == 4 || vertex == 5) ? 1 : -1);
                float2 offset = (e1 * h1 * corner.x + e2 * h2 * corner.y) / scale;
                float3 positionVS = c + float3(offset, 0);
                o.positionCS = TransformWViewToHClip(positionVS);

                float3 sun, direction, sky;
                SunAndSky(centre, sun, direction, sky);
                float lit = flat ? abs(dot(face, direction)) : sqrt(saturate(1 - dot(axis, direction) * dot(axis, direction))) * 0.64;
                float3 albedo = Albedo(q.dust.w);
                o.colour = float4(albedo * (sun * lit + sky), coverage);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 PieceFragment(Varyings i) : SV_Target
            {
                return half4(MixFog(i.colour.rgb, i.fogFactor), i.colour.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dust"
            Tags { "LightMode" = "PlantDust" }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DustVertex
            #pragma fragment DustFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Mineral dust's asymmetry parameter g (published: 0.6–0.7 for coarse dust at 550 nm, Dubovik et al. 2002) and a dense
            // cloud's diffuse share of the sun (assumed: its multiple scattering, a diffuse body's).
            #define DUST_G 0.6
            #define DUST_DIFFUSE 0.35

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                nointerpolation float4 centre : TEXCOORD1;  // xyz, k·m
                nointerpolation float2 sigma : TEXCOORD2;   // σ_h, σ_v
                nointerpolation float3 colour : TEXCOORD3;
            };

            Varyings DustVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                Varyings o = (Varyings)0;
                Piece q = _Pieces[instance];
                float t = _PieceTime.x;
                if (q.p[0].w != KIND_DUST || t < q.p[1].w)
                {
                    o.positionCS = float4(0, 0, -2, 1);
                    return o;
                }
                float share = saturate(t / _PieceTime.y);
                float3 centre = PathAt(q, clamp(t, max(q.p[1].w, 0), _PieceTime.y));
                float sh = lerp(q.size.z, q.size.w, share), sv = lerp(q.dust.x, q.dust.y, share);
                o.centre = float4(centre, q.dust.z);
                o.sigma = float2(sh, sv);

                // The quad: a billboard at the centre that covers the puff's 3σ bound as the camera sees it; the near plane's
                // rectangle when the camera is inside the bound.
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz, forward = -UNITY_MATRIX_V[2].xyz;
                float2 corner = float2((vertex == 1 || vertex == 2 || vertex == 4) ? 1 : -1, (vertex == 2 || vertex == 4 || vertex == 5) ? 1 : -1);
                float radius = 3 * max(sh, sv), d = length(centre - _WorldSpaceCameraPos);
                float near = _ProjectionParams.y * 1.5;
                float3 positionWS;
                if (d < radius + near * 2)
                {
                    float tx = 1.05 / abs(UNITY_MATRIX_P[0][0]), ty = 1.05 / abs(UNITY_MATRIX_P[1][1]);
                    positionWS = _WorldSpaceCameraPos + forward * near + right * (corner.x * tx * near) + up * (corner.y * ty * near);
                }
                else
                {
                    float r = radius * d / sqrt(d * d - radius * radius);
                    positionWS = centre + right * (corner.x * r) + up * (corner.y * r);
                }
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);

                float3 sun, direction, sky;
                SunAndSky(centre, sun, direction, sky);
                float3 view = normalize(centre - _WorldSpaceCameraPos);
                float g2 = DUST_G * DUST_G, cosine = dot(view, direction);
                float phase = (1 - g2) / pow(max(1e-4, 1 + g2 - 2 * DUST_G * cosine), 1.5);
                float3 colour = Albedo(q.dust.w) * (sun * (DUST_DIFFUSE + 0.25 * phase) + sky);
                o.colour = MixFog(colour, ComputeFogFactor(TransformWorldToHClip(centre).z));
                return o;
            }

            // Φ, the standard normal's distribution function (Abramowitz and Stegun 7.1.26's erf, |error| ≤ 1.5e-7).
            float Phi(float x)
            {
                float z = abs(x) * 0.70710678;
                float t = 1 / (1 + 0.3275911 * z);
                float y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * exp(-z * z);
                return 0.5 * (1 + (x < 0 ? -y : y));
            }

            float4 DustFragment(Varyings i) : SV_Target
            {
                float3 origin = _WorldSpaceCameraPos, ray = normalize(i.positionWS - origin);
                float2 uv = i.positionCS.xy * _DustTarget.zw;
                float raw = SampleSceneDepth(uv);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float far = LinearEyeDepth(raw, _ZBufferParams) / max(dot(ray, forward), 1e-3);
                float nearest = _ProjectionParams.y / max(dot(ray, forward), 1e-3);
                float3 sigma = float3(i.sigma.x, i.sigma.y, i.sigma.x);
                float3 o = (origin - i.centre.xyz) / sigma, d = ray / sigma;
                float dd = dot(d, d), od = dot(o, d), length_d = sqrt(dd);
                float closest = -od / dd, b2 = max(0, dot(o, o) - od * od / dd);
                float span = Phi(length_d * (far - closest)) - Phi(length_d * (nearest - closest));
                float tau = i.centre.w / (6.2831853 * i.sigma.x * i.sigma.x * i.sigma.y * length_d) * exp(-0.5 * b2) * max(0, span);
                return float4(i.colour * tau, tau);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Tags { "LightMode" = "PlantDustComposite" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment CompositeFragment

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 CompositeFragment(Varyings input) : SV_Target
            {
                float4 d = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                float tau = max(d.a, 0), alpha = 1 - exp(-tau);
                float3 colour = tau > 1e-6 ? d.rgb / tau : 0;
                return float4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
