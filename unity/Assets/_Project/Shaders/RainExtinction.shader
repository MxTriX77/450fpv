// The rain's extinction of the picture (w21-rain-on-camera 1.3, VIS-03, K§8.7 R3): Koschmieder's law over the
// drops between the camera and each opaque pixel, c' = c T + air (1 - T) with T = exp(-beta d), beta the drop
// spectrum's (WetOptics.ExtinctionPerM) and d the pixel's distance along its ray. The air is the fog's colour, the
// horizon the scene's light gives the haze, so the rain's in-scattered light is the overcast's as the haze's is; on
// top of the scene's own fog the two transmissions multiply, which is the law for two media under one light.
// WetLook draws it on the scene's cameras (never the feed's output) while it rains, from the camera's depth buffer
// (_BlitTexture), blended over the colour: src = air (1 - T), alpha = T, Blend One SrcAlpha. The sky (no depth)
// keeps its own colour.
Shader "Hidden/Fpv450/RainExtinction"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend One SrcAlpha, Zero One

        Pass
        {
            Name "Extinction"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Fpv450RainExtinction; // x beta (1/m)
            float4 _Fpv450RainAir;        // rgb the air's colour, linear

            float4 Frag(Varyings i) : SV_Target
            {
                float raw = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord, 0).r;
            #if UNITY_REVERSED_Z
                if (raw <= 0.0)
                    return float4(0.0, 0.0, 0.0, 1.0);
            #else
                if (raw >= 1.0)
                    return float4(0.0, 0.0, 0.0, 1.0);
            #endif
                // The distance along the pixel's ray from its view depth: the ray's length per unit of depth, from the
                // projection's own slopes, is the same whichever way the target is flipped.
                float depth = LinearEyeDepth(raw, _ZBufferParams);
                float2 slope = (i.texcoord * 2.0 - 1.0) / float2(unity_CameraProjection._m00, unity_CameraProjection._m11);
                float d = depth * sqrt(1.0 + dot(slope, slope));
                float t = exp(-_Fpv450RainExtinction.x * d);
                return float4(_Fpv450RainAir.rgb * (1.0 - t), t);
            }
            ENDHLSL
        }
    }
}
