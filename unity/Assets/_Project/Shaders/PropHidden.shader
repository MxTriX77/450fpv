// Draws nothing (w13-props-in-frame): PropView puts it on the model's prop material slot, so D01's still blades, merged
// into each LOD's mesh, leave the picture while Shaders/PropDisc draws the turning props. Its one pass has a light mode no
// URP pass draws: no colour, no depth, no shadow.
Shader "Hidden/Fpv450/PropHidden"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "Fpv450Never" }
            ColorMask 0
            ZWrite Off
        }
    }
}
