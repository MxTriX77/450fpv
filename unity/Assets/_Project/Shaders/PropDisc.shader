// A turning prop as the camera exposes it (w13-props-in-frame, design.md in the change): the sim's blade (DroneSpec's
// props.blade: chord, thickness and section angle per station, the hub) at the sim's rotor angle, each pixel covered for
// the share of its exposure the blade spends on its ray.
//
// The mesh is a bounding cylinder about the rotor's axis (PropView.Bound: the disc's radius, the blade's height plus a
// margin so its far wall clears the near plane), drawn by its back faces so each pixel the disc can cover runs once.
// The fragment takes the camera's ray in the rotor's frame (object space: y the shaft, x and z the disc, blade 0 along
// (cos phi, 0, sin phi)), crosses the disc's plane and reads the station there. A blade's section at that radius is a
// rectangle in the plane of its motion and the shaft (chord c along the section angle, the leading edge ahead and up,
// thickness t across it); the ray, straight through that plane, meets the blade for an interval of blade angles, and
// the blade's sweep over the exposure, [theta_end - S, theta_end], meets that interval (once a blade spacing) for some
// share of the sweep: the pixel's coverage, its alpha. S -> 0 (a still rotor, a short exposure) is the plain inside test.
//
// The time: a CMOS sensor reads its rows in turn (RollingShutter.RowReadUs), so in the FPV view each pixel takes the
// angle at its own field row's read, the row from its view ray through the feed's lens (p_u = p_d (1 + k1 r_d^2));
// the chase view reads every row at the field's reference instant. The angle at a time comes from _PropAngles, the sim's
// rotor angle at the history's 1 ms steps against the reference (RollingShutter's samples).
//
// The light: the model's prop colour (its material's palette), the blade's face toward the camera lit by the main light
// and the ambient probe (no shadow, no fog: the
// blade is centimetres from the lens or seen across a chase view's 2 m).
Shader "Fpv450/PropDisc"
{
    Properties
    {
        [MainTexture] _BaseMap("Palette", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (0.05, 0.05, 0.05, 1)
    }

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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PropVertex
            #pragma fragment PropFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define PROP_SAMPLES 41        // RollingShutter.Samples
            #define PROP_SAMPLE_LEAD 20.0  // RollingShutter.SampleLead
            #define PROP_STATIONS 40       // the most stations PropView uploads
            #define PAL_LINE_US 64.0       // RollingShutter.LineUs
            #define PAL_BLANKING 25.0      // RollingShutter.BlankingLines
            #define PROP_TWO_PI 6.28318530718

            // The model's own prop material (PropView copies it): its palette's one colour, the "prop" slot's "blade".
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseColor;
            // x: the sim's rotor angle at the field's reference instant (rad); y: s, the angle's sign in the disc's frame
            // (-1 clockwise); z: the prop's radius (m); w: 1 to read each pixel at its row's time, 0 at the reference
            float4 _PropRotor;
            // x: the hub's radius, y: its half thickness (m); z: the first span's start (r / R); w: a span (r / R)
            float4 _PropHub;
            // x: the exposure (us); y: the reference instant (us of the field); z: the stations; w: the blades
            float4 _PropRead;
            float _PropAngles[PROP_SAMPLES];   // the rotor's angle at (i - 20) ms of the field, less x's, unwrapped
            float _PropChord[PROP_STATIONS];   // m
            float _PropThick[PROP_STATIONS];   // m
            float _PropPitch[PROP_STATIONS];   // rad, the chord line to the disc
            float4 _PropLens;                  // global: k1, the focal length (half-widths), the aspect, the field's rows

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float3 positionOS : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings PropVertex(Attributes input)
            {
                Varyings o;
                o.positionOS = input.positionOS.xyz;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            // The rotor's angle at us of the field's time, less the reference's: the 1 ms samples, linear between them.
            float AngleAt(float us)
            {
                float s = clamp(us / 1000.0 + PROP_SAMPLE_LEAD, 0.0, PROP_SAMPLES - 1.001);
                int i = (int)s;
                return lerp(_PropAngles[i], _PropAngles[i + 1], s - i);
            }

            // The field row's read (us of the field) for a point in view space, through the feed's lens.
            float ReadUs(float3 positionVS)
            {
                float k1 = _PropLens.x;
                float2 pu = _PropLens.y * positionVS.xy / max(-positionVS.z, 1e-5); // half-widths, y up
                float ru = length(pu), rd = ru;
                [unroll] for (int n = 0; n < 5; n++) // Newton on rd (1 + k1 rd^2) = ru
                    rd -= (rd + k1 * rd * rd * rd - ru) / (1.0 + 3.0 * k1 * rd * rd);
                float pdy = ru > 1e-6 ? pu.y * rd / ru : pu.y;
                float top = 0.5 - 0.5 * pdy * _PropLens.z; // the picture's height from its top, 0-1
                float row = clamp(top * _PropLens.w - 0.5, 0.0, _PropLens.w - 1.0);
                return (PAL_BLANKING + row + 0.5) * PAL_LINE_US;
            }

            // How much of [-sweep, 0] the interval [lo, hi] and its copies a blade spacing apart cover.
            float Overlap(float lo, float hi, float sweep, float spacing)
            {
                float k0 = ceil((-sweep - hi) / spacing), k1 = floor(-lo / spacing), sum = 0.0;
                [loop] for (float k = k0; k <= k1 && k < k0 + 64.0; k += 1.0)
                    sum += max(0.0, min(hi + k * spacing, 0.0) - max(lo + k * spacing, -sweep));
                return sum;
            }

            float4 PropFragment(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                float3 eye = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 d = normalize(input.positionOS - eye);
                float along = -eye.y / (abs(d.y) > 1e-5 ? d.y : 1e-5);
                clip(along);
                float3 p = eye + d * along; // the ray across the disc's plane
                float r = length(p.xz), R = _PropRotor.z;
                clip(R - r);
                float4 cs = TransformObjectToHClip(p);
                depth = cs.z / cs.w;

                float phi = atan2(p.z, p.x), s = _PropRotor.y;
                float read = _PropRotor.w > 0.5 ? ReadUs(TransformWorldToView(TransformObjectToWorld(p))) : _PropRead.y;
                float now = AngleAt(read);
                float sweep = max(0.0, now - AngleAt(read - _PropRead.x));
                float thetaEnd = _PropRotor.x + now;

                float3 up = float3(0.0, 1.0, 0.0), normal = up;
                float alpha;
                if (r < _PropHub.x)
                {
                    alpha = 1.0; // the hub
                }
                else
                {
                    int k = (int)floor((r / R - _PropHub.z) / _PropHub.w);
                    clip(k);
                    clip(_PropRead.z - 1 - k);
                    float c = _PropChord[k], t = _PropThick[k], a = _PropPitch[k];
                    float2 m = s * float2(-sin(phi), cos(phi)); // the blade's motion at the crossing
                    float slope = dot(d.xz, m) / (abs(d.y) > 1e-5 ? d.y : 1e-5); // the ray's way along the motion per metre up
                    float2 chord = 0.5 * c * float2(cos(a), sin(a)), across = 0.5 * t * float2(-sin(a), cos(a));
                    // The section's corners (along the motion, up), each as the blade's offset that puts it on the ray.
                    float v0 = (chord.x + across.x) - slope * (chord.y + across.y);
                    float v1 = (chord.x - across.x) - slope * (chord.y - across.y);
                    float lo = min(min(v0, v1), min(-v0, -v1)), hi = max(max(v0, v1), max(-v0, -v1));
                    // The blade angles whose blade meets the ray, against the sweep's end.
                    float base = s * phi - thetaEnd;
                    float qlo = base - hi / r, qhi = base - lo / r;
                    float spacing = PROP_TWO_PI / _PropRead.w;
                    alpha = sweep > 1e-6
                        ? Overlap(qlo, qhi, sweep, spacing) / sweep
                        : (ceil(-qhi / spacing) <= floor(-qlo / spacing) ? 1.0 : 0.0);
                    normal = float3(m.x, 0.0, m.y) * -sin(a) + up * cos(a);
                }
                clip(alpha - 1e-4);
                if (dot(normal, d) > 0.0) normal = -normal; // the face toward the camera
                float3 n = TransformObjectToWorldNormal(normal);
                Light sun = GetMainLight();
                float3 light = sun.color * saturate(dot(n, sun.direction)) + SampleSH(n);
                float3 albedo = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 0).rgb * _BaseColor.rgb;
                return float4(albedo * light, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
