// The analog feed (p4-feed task 1.2): the Godot feed the pilot chose, `video_feed.gdshader`, ported to one URP pass.
//
// Ported, never merged (D-026, D-027), from the verbatim copy in docs/legacy/video-feed-godot/shaders/video_feed.gdshader.
// `gd:N` after a constant or a block names its line there. The algorithm, constants, hash, draw order and structure are
// Godot's; retuning them is the pilot's call, never the port's. Godot's plumbing becomes three blits (FeedFeature.cs):
//
//   0 Encode  the FPV camera's linear render to sRGB code values. Godot's shader read the screen texture, an 8-bit
//             sRGB-encoded buffer filtered as it is stored, so every constant here (OSD_WHITE, the no-signal blue,
//             the snow levels) is a code value. A frame that is already code values (a PNG) skips this.
//   1 Field   the whole of Godot's fragment() for one PAL field: one row per field line, one column per output
//             column (gd:165-412). Godot ran it per screen pixel, but every term except the vignette and the border
//             is constant along a field line (each reads `line` or `sy`), so this is the same picture at 287 rows.
//   2 Output  the bob deinterlace and the upscale: each screen row shows its field line (gd:169) with the P8 border
//             (gd:415), back to linear for URP.
//
// Two departures, both measured in the still A/B:
//   - the vignette (gd:374) is read at the line's centre `sy` rather than the screen row: within a line it moves by
//     at most 0.03 % (0.07 of a code level), and it has to sit before the P7 clip.
//   - Godot's top-down SCREEN_UV is 1 - Unity's texcoord.y, and GLSL's mod and smoothstep are written out (glsl_mod,
//     glsl_smoothstep) because HLSL's fmod truncates and the shader runs smoothstep with its edges reversed.
//
// One addition, off by default in the A/B: O1, the lens (K§11.4, FEED-02), on the sensor_read coordinate only, so the
// OSD keeps its straight horizon while the real one bends (gd:28-30). With k1 = 0 and coverage 1 it is Godot's read.
// A second, off in the A/B (p7e-camera-feel): the camera's read, each field row turned by the camera's rotation at its
// read and averaged over its exposure (shutter_tex), on the scene only; with _Shutter.x = 0 the read is the one above.
// One removal (D-065, the camera's noise, not a radio link's): the tear's black wedge is drawn only with _TearWedge = 1,
// Godot's feed in the A/B; the feed draws its tear as a shear alone.
Shader "Hidden/Fpv450/Feed"
{
    Properties
    {
        _OsdCodes ("OSD codes (FeedOsd, task 1.4)", 2D) = "black" {}
        _OsdGlyphs ("OSD glyph atlas (FeedFont, task 1.4)", 2D) = "black" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    // --- the uniforms, set per field by FeedFeature from FeedUniforms (gd:32-68) -----------------------------------
    TEXTURE2D(_OsdCodes);   // gd:36
    TEXTURE2D(_OsdGlyphs);  // gd:37
    float _OsdCells;        // gd:38
    float2 _OsdCell;        // gd:42
    float2 _OsdOrigin;      // gd:43

    float _Gain;            // gd:45
    float _LinkMargin;      // gd:46
    float _MotorCurrent;    // gd:47
    float _Seed;            // gd:48
    float _Field;           // gd:49
    float _N3Amp;           // gd:50
    float _N3Roll;          // gd:51
    float _N3Spacing;       // gd:52
    float _N4Amp;           // gd:53
    float _N4Span;          // gd:54
    float _Degrade;         // gd:55

    float _LossState;       // gd:58
    float _LossSplit;       // gd:59
    float _LossTopStrip;    // gd:60
    float _BlackA;          // gd:61
    float _BlackB;          // gd:62
    float _SnowLevel;       // gd:63
    float _SnowChroma;      // gd:64
    float _Tell;            // gd:65
    float _TearAmp;         // gd:66
    float _TearTop;         // gd:67
    float _GrainBoost;      // gd:68
    // D-065: 1 draws the tear's black wedge (gd:382-385, the pilot's "black sticks"), Godot's link artefact, only in
    // the FEED-00 A/B (FeedEvents.GodotLinkArtefacts); 0 leaves the tear its shear alone.
    float _TearWedge;

    // O1 (the port's addition): x = k1, y = the render's coverage in half-widths at the corner (1 + k1 r²), z = the
    // picture's aspect. (0, 1, any) reads the scene exactly as Godot did.
    float4 _Lens;
    // The field texture: x = columns, y = rows (FieldRows = LINES + 1, so an odd field's half line at the top fits).
    float4 _FieldSize;

    // The camera's read (p7e-camera-feel, RollingShutter.cs): per field row, 8 tap rotations of the camera against the
    // field's reference instant (x, y, z, w) then (tap count, 0, 0, 0), 9 x 287 texels, row 0 the top. _Shutter: x = 1 to
    // read through it, y = the pinhole's f in half-widths, z = the render's coverage (the lens's and the margin's). x = 0
    // is the old read, exactly.
    TEXTURE2D(_ShutterTrack);
    float4 _Shutter;
    static float4 g_tap[8];
    static int g_taps;

    // --- the constants, literal (gd:70-101) ---------------------------------------------------------------------
    #define GD_TWO_PI 6.2831853         // gd:70 (core RP defines its own TWO_PI)
    #define LINES 286.0                 // gd:71
    #define SC_SAMPLES 920.0            // gd:72
    #define SC_CYCLES 230.0             // gd:73
    #define LUMA 450.0                  // gd:74
    #define SHARPEN 0.30                // gd:75
    #define GRAIN 0.011                 // gd:76
    #define APERTURE_V 0.15             // gd:77
    #define CROSS 2.6                   // gd:78
    #define CROSS_V 0.35                // gd:80
    #define SATURATION 0.78             // gd:81
    #define LIFT 0.05                   // gd:82
    #define CONTRAST 0.90               // gd:82
    #define BANDING 0.006               // gd:83
    #define OSD_COLS 30.0               // gd:85
    #define OSD_ROWS 16.0               // gd:86
    #define CODE_ROWS 18.0              // gd:87
    #define OSD_WHITE 0.96              // gd:88
    #define OSD_COMB 0.10               // gd:92
    #define OSD_BLACK 0.02              // gd:93
    static const float3 NO_SIGNAL_BLUE = float3(0.0, 0.039, 0.784);   // gd:99
    static const float3 RX_TEXT_GREEN = float3(0.16, 0.85, 0.28);     // gd:100
    #define TEAR_PX 60.0                // gd:101

    // GLSL's mod: x - y * floor(x / y). HLSL's fmod truncates, which differs for the -1 line of an odd field.
    float glsl_mod(float x, float y) { return x - y * floor(x / y); }
    // GLSL's smoothstep as its spec writes it; the tear band (gd:179) calls it with the edges reversed.
    float glsl_smoothstep(float e0, float e1, float x)
    {
        float t = clamp((x - e0) / (e1 - e0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    // gd:105-109, the precision-safe hash: ported exactly (README §1).
    float hash(float3 p)
    {
        float3 q = frac(p * float3(0.1031, 0.1030, 0.0973) + _Seed * 0.017);
        q += dot(q, q.yzx + 33.33);
        return frac((q.x + q.y) * q.z);
    }

    // O1: the picture coordinate (top-down, 0-1) to where the pinhole render saw it, p_u = p_d (1 + k1 r_d²) with r
    // normalised to the half-width (K§11.4). The render covers `coverage` half-widths, so its corners are the
    // picture's corners.
    float2 lens_read(float2 uv)
    {
        float2 halfSize = float2(1.0, 1.0 / _Lens.z);
        float2 pd = (uv - 0.5) * 2.0 * halfSize;
        float2 pu = pd * (1.0 + _Lens.x * dot(pd, pd));
        return 0.5 + 0.5 * pu / (_Lens.y * halfSize);
    }

    // v rotated by the unit quaternion q: v + 2w(u x v) + 2u x (u x v), the sim's Quat.Rotate.
    float3 rotate_q(float4 q, float3 v)
    {
        float3 t = 2.0 * cross(q.xyz, v);
        return v + q.w * t + cross(q.xyz, t);
    }

    // The camera's read (p7e-camera-feel): the picture point's pinhole ray (x right, y up, z forward, in half-widths) as
    // the camera saw it at each of this row's taps, turned into the reference camera's axes and projected into the
    // render, the taps averaged: the rolling shutter's skew and the exposure's blur.
    float3 shutter_tex(float2 uv)
    {
        float2 halfSize = float2(1.0, 1.0 / _Lens.z);
        float2 pd = (uv - 0.5) * 2.0 * halfSize;
        float2 pu = pd * (1.0 + _Lens.x * dot(pd, pd));
        float f = _Shutter.y;
        float3 ray = float3(pu.x, -pu.y, f);
        float3 sum = float3(0.0, 0.0, 0.0);
        [loop] for (int i = 0; i < g_taps; i++)
        {
            float3 r = rotate_q(g_tap[i], ray);
            float2 pr = f * float2(r.x, -r.y) / max(r.z, 1e-4);
            float2 s = 0.5 + 0.5 * pr / (_Shutter.z * halfSize);
            sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, float2(s.x, 1.0 - s.y), 0).rgb;
        }
        return sum / float(g_taps);
    }

    float3 screen_tex(float2 uv)
    {
        if (_Shutter.x > 0.5)
        {
            return shutter_tex(uv);
        }
        float2 s = lens_read(uv);
        return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, float2(s.x, 1.0 - s.y), 0).rgb;
    }

    // This field row's taps into g_tap and g_taps (one identity tap with the shutter off).
    void shutter_row(float row)
    {
        g_taps = 1;
        g_tap[0] = float4(0.0, 0.0, 0.0, 1.0);
        if (_Shutter.x < 0.5)
        {
            return;
        }
        int r = clamp(int(row), 0, 286);
        g_taps = clamp(int(LOAD_TEXTURE2D(_ShutterTrack, int2(8, r)).x + 0.5), 1, 8);
        [loop] for (int j = 0; j < g_taps; j++)
        {
            g_tap[j] = LOAD_TEXTURE2D(_ShutterTrack, int2(j, r));
        }
    }

    // gd:114-117. The sensor's vertical aperture lives on the sensor, so the lens maps each of its two reads.
    float3 sensor_read(float2 uv)
    {
        return 0.5 * (screen_tex(uv - float2(0.0, APERTURE_V / LINES))
            + screen_tex(uv + float2(0.0, APERTURE_V / LINES)));
    }

    // gd:119-121
    float luma_of(float3 c)
    {
        return dot(c, float3(0.299, 0.587, 0.114));
    }

    // gd:124-129. The 30 x 18 codes texture holds grid row r in its pixel row r (Texture2D.SetPixel(col, row), as
    // Godot's Image.SetPixel), so it is read without a flip.
    float cell_code(float col, float row)
    {
        if (col < 0.0 || col >= OSD_COLS || row < 0.0 || row >= CODE_ROWS)
        {
            return 0.0;
        }
        return SAMPLE_TEXTURE2D_LOD(_OsdCodes, sampler_PointClamp,
            float2((col + 0.5) / OSD_COLS, (row + 0.5) / CODE_ROWS), 0).r;
    }

    // gd:132-134. The atlas is one row of cells, its pixel rows top-down as FeedFont writes them, read without a flip.
    float2 glyph_ink(float code, float cx, float v)
    {
        return SAMPLE_TEXTURE2D_LOD(_OsdGlyphs, sampler_PointClamp,
            float2((code + frac(cx)) / _OsdCells, v), 0).rg;
    }

    // gd:138-143
    float2 grid_ink(float x, float y, float row0)
    {
        float cx = (x - _OsdOrigin.x) / _OsdCell.x;
        float cy = (y - _OsdOrigin.y) / _OsdCell.y;
        float code = cell_code(floor(cx), floor(cy) + row0);
        return code < 0.5 ? float2(0.0, 0.0) : glyph_ink(code, cx, frac(cy));
    }

    // gd:148-163, the receiver's snow.
    float3 snow_of(float x, float line_, float fw)
    {
        float s = hash(float3(floor(x * LUMA * 0.55), line_, fw + 511.0));
        float white = step(0.80, s);
        float lvl = white * (0.55 + 0.40 * frac(s * 37.0)) + step(0.62, s) * 0.10;
        lvl += 0.04 * sin(GD_TWO_PI * (x * 57.0 + line_ * 0.5));  // the herringbone
        float3 c = (lvl + _SnowLevel - 0.188).xxx;
        if (_SnowChroma > 0.0)
        {
            float h = hash(float3(floor(x * LUMA * 0.55), line_, fw + 733.0));
            c += _SnowChroma * 0.20 * white * (float3(frac(h * 5.0), frac(h * 11.0), frac(h * 17.0)) - 0.5);
        }
        return clamp(c, 0.0, 1.0);
    }

    // gd:165-412: one field line's picture at column `x`. `row` counts the field texture's rows from the top.
    float3 feed_field(float x, float row)
    {
        shutter_row(row);

        // --- field sampling (P1), gd:166-174
        float fw = glsl_mod(_Field, 1024.0);
        float parity = glsl_mod(_Field, 2.0);
        float line_ = row - parity;                               // gd:169 floor(SCREEN_UV.y * LINES - 0.5 * parity)
        float sy = (line_ + 0.5 + 0.5 * parity) / LINES;
        float abs_line = line_ + glsl_mod(_Field, 8.0) * 312.5;
        float vsw = glsl_mod(abs_line, 2.0) < 1.0 ? 1.0 : -1.0;
        float ph_line = 0.75 * abs_line;

        // --- N8 tearing, gd:179-184
        float tear_band = _TearTop > 0.5 ? glsl_smoothstep(0.18, 0.0, sy) : glsl_smoothstep(0.82, 1.0, sy);
        float px = x;
        if (_TearAmp > 0.0)
        {
            px += (hash(float3(floor(line_ / 3.0), 77.0, fw)) - 0.5) * 2.0
                * _TearAmp * tear_band * TEAR_PX / 1920.0;
        }

        // --- 17 scene taps along the line, gd:188-195
        float3 mid[18];
        [unroll] for (int ia = 0; ia < 18; ia++)
        {
            mid[ia] = sensor_read(float2(px + (float(ia) - 8.5) / SC_SAMPLES, sy));
        }
        float3 tap[17];
        [unroll] for (int ib = 0; ib < 17; ib++)
        {
            tap[ib] = 0.5 * (mid[ib] + mid[ib + 1]);
        }

        // --- P3 sharpening, gd:199-204. At the window's two ends Godot reads tap[-1] and tap[17], past its 17
        // taps; those two samples carry zero weight in both chroma windows (wl and ws are 0 at 0 and 12, gd:310-311)
        // and the luma path reads only the centre (gd:334), so any finite value leaves the picture as it is. HLSL
        // refuses the out-of-range index, so the port clamps it.
        float sharpen = SHARPEN * (1.0 + 1.6 * _Degrade);
        float3 sharp[13];
        [unroll] for (int ic = 0; ic < 13; ic++)
        {
            int c = ic + 2;
            sharp[ic] = tap[c] * (1.0 + 2.0 * sharpen) - sharpen * (tap[max(c - 3, 0)] + tap[min(c + 3, 16)]);
        }

        // --- N2, gd:209-215
        float texture_gate = clamp(0.15 + 12.0 * abs(luma_of(tap[8]) - 0.5 * (luma_of(tap[5]) + luma_of(tap[11]))), 0.0, 2.0);
        float imp_rate = (0.0002 + 0.010 * (1.0 - _LinkMargin) * (1.0 - _LinkMargin) + 0.001 * _MotorCurrent
            + 0.004 * _Degrade) * texture_gate;
        if (_Tell > 0.0)
        {
            imp_rate += step(0.62, hash(float3(floor(line_ / 24.0), 131.0, fw))) * (0.05 + 0.10 * _Tell);
        }

        // --- N3, gd:219
        float n3 = _N3Amp * step(frac(line_ / _N3Spacing + _N3Roll), 1.0 / _N3Spacing);

        // --- the OSD cells this pixel needs, gd:224-229
        float cy_osd = (sy - _OsdOrigin.y) / _OsdCell.y;
        float cx_px = (px - _OsdOrigin.x) / _OsdCell.x;
        float cx_lo = floor(cx_px - 0.2), cx_hi = floor(cx_px + 0.2);
        float code_lo = cell_code(cx_lo, floor(cy_osd));
        float code_hi = cx_hi == cx_lo ? code_lo : cell_code(cx_hi, floor(cy_osd));
        bool has_osd = code_lo > 0.5 || code_hi > 0.5;

        // --- encode, disturb, decode, gd:232-250
        float grain_sigma = GRAIN * _Gain * (1.0 + 2.2 * _Degrade) * (1.0 + _GrainBoost);
        float osd_ink[13], osd_out[13];
        [unroll] for (int id = 0; id < 13; id++)
        {
            osd_ink[id] = 0.0;
            osd_out[id] = 0.0;
            if (has_osd)
            {
                float xo = px + float(id - 6) / SC_SAMPLES;
                float cx = (xo - _OsdOrigin.x) / _OsdCell.x;
                float code = floor(cx) <= cx_lo ? code_lo : code_hi;
                if (code > 0.5)
                {
                    float2 osd = glyph_ink(code, cx, frac(cy_osd));
                    osd_ink[id] = osd.x;
                    osd_out[id] = osd.y;
                }
            }
        }

        // gd:259-265, the OSD as the PAL delay line leaves it in the chroma path
        float ink_soft = 0.0, out_soft = 0.0;
        [unroll] for (int ie = 0; ie < 13; ie++)
        {
            ink_soft += osd_ink[ie];
            out_soft += osd_out[ie];
        }
        ink_soft /= 13.0;
        out_soft /= 13.0;

        // gd:267-299
        float comp[13];
        float comp_c[13];
        [unroll] for (int ig = 0; ig < 13; ig++)
        {
            float xs = px + float(ig - 6) / SC_SAMPLES;
            float3 scene = clamp(sharp[ig], 0.0, 1.0);

            float3 c = lerp(scene, (OSD_BLACK).xxx, osd_out[ig]);
            c = lerp(c, (OSD_WHITE).xxx, osd_ink[ig]);
            float3 cc = lerp(scene, (OSD_BLACK).xxx, lerp(out_soft, osd_out[ig], OSD_COMB));
            cc = lerp(cc, (OSD_WHITE).xxx, lerp(ink_soft, osd_ink[ig], OSD_COMB));

            float yc = luma_of(cc);
            float u = 0.493 * (cc.b - yc);
            float v = 0.877 * (cc.r - yc);
            float ph = GD_TWO_PI * (SC_CYCLES * xs + ph_line);
            float sub = u * sin(ph) + v * vsw * cos(ph) - n3;

            // N1, gd:285
            sub += (hash(float3(floor(xs * SC_SAMPLES * 0.5), line_, fw)) - 0.5) * 2.0 * grain_sigma;

            // N2's impulses, gd:290-295
            float cell = floor(xs * SC_SAMPLES * 0.25);
            float imp = hash(float3(cell, line_, fw + 17.0));
            if (imp < imp_rate)
            {
                sub += 0.30 * sin(ph + GD_TWO_PI * hash(float3(cell, line_, fw + 29.0)))
                    + (imp < imp_rate * 0.4 ? 0.10 : -0.10);
            }

            comp[ig] = luma_of(c) + sub;
            comp_c[ig] = yc + sub;
        }

        // U/V synchronous detection, gd:307-325
        float u_wide = 0.0, v_wide = 0.0, w_wide = 0.0;
        float u_fine = 0.0, v_fine = 0.0, w_fine = 0.0;
        [unroll] for (int ih = 0; ih < 13; ih++)
        {
            float wl = 0.5 - 0.5 * cos(GD_TWO_PI * float(ih) / 12.0);
            float ws = ih < 2 || ih > 10 ? 0.0 : (ih == 2 || ih == 10 ? 0.5 : 1.0);
            float xs = px + float(ih - 6) / SC_SAMPLES;
            float ph = GD_TWO_PI * (SC_CYCLES * xs + ph_line);
            float sp = sin(ph), cp = vsw * cos(ph);
            u_wide += comp_c[ih] * sp * wl;
            v_wide += comp_c[ih] * cp * wl;
            w_wide += wl;
            u_fine += comp_c[ih] * sp * ws;
            v_fine += comp_c[ih] * cp * ws;
            w_fine += ws;
        }
        u_wide = 2.0 * u_wide / w_wide;
        v_wide = 2.0 * v_wide / w_wide;
        u_fine = 2.0 * u_fine / w_fine;
        v_fine = 2.0 * v_fine / w_fine;

        // Y: the chroma trap, gd:333-334
        float ph_mid = GD_TWO_PI * (SC_CYCLES * px + ph_line);
        float y_dec = comp[6] - (u_wide * sin(ph_mid) + v_wide * vsw * cos(ph_mid));
        // gd:339-340
        float u_dec = u_wide + CROSS * (u_fine - u_wide);
        float v_dec = v_wide + CROSS * CROSS_V * (v_fine - v_wide);

        // C3, gd:344-346
        float sat = clamp(SATURATION - 0.32 * (_Gain - 1.0) - 0.35 * _Degrade, 0.0, 1.0);
        u_dec *= sat;
        v_dec *= sat;

        // N4, gd:350-356
        if (_N4Amp > 0.0 && sy < _N4Span)
        {
            float diag = frac(px * 10.0 - line_ * (8.0 / 1920.0) * 10.0);
            float amp = step(frac(px * LUMA * 0.5), 0.5) * step(diag, 0.06) * _N4Amp * 2.0;
            y_dec += amp * 0.5;
            u_dec += amp * (glsl_mod(line_, 2.0) < 1.0 ? 1.1 : -0.6);
            v_dec += amp * (glsl_mod(line_, 2.0) < 1.0 ? -0.7 : 0.9);
        }

        // gd:360-361
        y_dec = y_dec * (1.0 - 0.10 * _Degrade) - 0.03 * _Degrade;
        y_dec *= 1.0 + _Tell;

        // gd:365-366
        y_dec = LIFT + CONTRAST * y_dec
            + BANDING * (sin(GD_TWO_PI * px * 3.7) + 0.7 * sin(GD_TWO_PI * px * 9.3 + 2.1));

        // gd:368-371
        float3 rgb = float3(
            y_dec + 1.140 * v_dec,
            y_dec - 0.394 * u_dec - 0.581 * v_dec,
            y_dec + 2.028 * u_dec);

        // O2 vignette, gd:374-375, at the line's centre (see the header)
        float2 d = (float2(x, sy) - 0.5) * float2(2.0, 1.125);
        rgb *= 1.0 - 0.12 * dot(d, d);

        // P7, gd:378-379
        rgb += float3(0.05, 0.0, 0.07) * smoothstep(0.86, 1.0, y_dec);
        rgb = clamp(rgb, 0.0, 1.0);

        // the tear's wedge, gd:382-385, Godot's only (D-065)
        if (_TearAmp > 0.0)
        {
            float wedge = _TearWedge * _TearAmp * tear_band * 0.10 * hash(float3(floor(line_ / 3.0), 211.0, fw));
            rgb *= step(x, 1.0 - wedge);
        }

        // --- the receiver's loss of picture, gd:388-403
        bool snowy = false;
        if (_LossState > 0.5 && _LossState < 1.5)
        {
            snowy = sy < _LossSplit && sy > _LossTopStrip;
        }
        else if (_LossState > 1.5 && _LossState < 2.5)
        {
            rgb = NO_SIGNAL_BLUE;
        }
        else if (_LossState > 2.5 && _LossState < 3.5)
        {
            snowy = true;
        }
        else if (_LossState > 3.5)
        {
            rgb *= 1.0 - step(_BlackA, sy) * step(sy, _BlackB);
        }
        if (snowy)
        {
            rgb = snow_of(px, line_, fw);
        }

        // the receiver's text on blue and snow only, gd:408-412
        if (_LossState > 1.5 && _LossState < 3.5)
        {
            float2 t = grid_ink(x, sy, OSD_ROWS);
            rgb = lerp(rgb, float3(0.0, 0.0, 0.0), t.y);
            rgb = lerp(rgb, RX_TEXT_GREEN, t.x);
        }
        return rgb;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Encode"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragEncode
            float4 FragEncode(Varyings i) : SV_Target
            {
                float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, i.texcoord, 0).rgb;
                return float4(LinearToSRGB(saturate(c)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Field"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragField
            #pragma target 4.5
            float4 FragField(Varyings i) : SV_Target
            {
                float row = floor((1.0 - i.texcoord.y) * _FieldSize.y);
                return float4(feed_field(i.texcoord.x, row), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Output"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOutput
            float4 FragOutput(Varyings i) : SV_Target
            {
                // gd:166-169: the screen row's field line, then the line's row in the field texture.
                float gy = 1.0 - i.texcoord.y;
                float parity = glsl_mod(_Field, 2.0);
                float line_ = floor(gy * LINES - 0.5 * parity);
                float v = 1.0 - (line_ + parity + 0.5) / _FieldSize.y;
                float3 rgb = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, float2(i.texcoord.x, v), 0).rgb;
                // P8 receiver border, gd:415
                rgb *= step(0.0037, gy) * step(gy, 0.9981);
                return float4(SRGBToLinear(rgb), 1.0);
            }
            ENDHLSL
        }
    }
}
