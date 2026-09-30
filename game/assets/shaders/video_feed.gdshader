shader_type canvas_item;

// The analog feed. One pass over the 3D render.
//
// The pilot picked this from the three spike candidates: the baseline is the simulated composite chain (candidate 1)
// held back to the restraint of the authored version (candidate 2), and the heavier degraded look (candidate 3) is
// not a separate mode but something the feed slides into and back out of at random. On top of that, grain-only cuts:
// the picture gone for an instant, pure snow, then back.
//
// Structure, per output pixel:
//   1. 14 band-limited sensor reads along one field line, paired into 13 composite samples (P1, P2)
//   2. horizontal unsharp before the encode, so the halo softens with the signal (P3)
//   3. PAL encode: luma plus the subcarrier, V-switched per line, 0.75 cycles of phase per line
//   4. on the SIGNAL: N1 grain, N2 link impulses, N3 the rail transient
//   5. decode: a 4-tap notch for luma, a 13-tap windowed synchronous detector for chroma. The chroma smear, the
//      cross-colour rainbow and the dot crawl fall out of this; none of them is drawn (P4, P5)
//   6. authored on the decoded picture: N4 diagonal interference, which is the one artifact whose look is easier to
//      author than to derive, plus the receiver's snow during a cut
//   7. camera and receiver trim: saturation vs gain (C3), vignette (O2), highlight clip (P7), border (P8)
//
// `degrade` (0-1) slides every one of those toward the heavy look; `snow` (0/1) replaces the picture. Both come from
// FeedEvents, which draws their timing, depth and length. Reference: docs/reference-notes/video-feed.md.

uniform sampler2D screen : hint_screen_texture, repeat_disable, filter_linear;

uniform float gain = 1.0;          // camera AE gain: 1 day, ~4 night. N1 sigma and C3 saturation follow it.
uniform float link_margin = 1.0;   // 1 plenty, 0 at the receiver threshold.
uniform float motor_current = 0.0; // 0-1 of full current.
uniform float seed = 1.0;
uniform float field = 0.0;         // the 50 Hz field counter; the whole feed is a function of (seed, field).
uniform float n3_amp = 0.0;        // N3 depth in signal units, 0 while the event is not firing
uniform float n3_roll = 0.0;       // 0-1, where the line set currently sits
uniform float n3_spacing = 40.0;   // field lines between dark lines
uniform float n4_amp = 0.0;
uniform float n4_span = 0.0;       // top fraction of the field the burst covers
uniform float degrade = 0.0;       // 0 baseline, 1 the heaviest quality drop
uniform float snow = 0.0;          // 1 while the picture is cut to grain only

const float TWO_PI = 6.2831853;
const float LINES = 286.0;      // active lines per field, measured 286 on the B-G receiver (P1)
const float SC_SAMPLES = 920.0; // composite samples per active line (4 x subcarrier)
const float SC_CYCLES = 230.0;  // subcarrier cycles per active line (4.43 MHz x 52 us)
const float LUMA = 450.0;       // effective luma samples per line (P2)
const float SHARPEN = 0.30;     // P3, radius 3 composite samples so its peak does not sit on the subcarrier
const float GRAIN = 0.011;      // N1, about 2.8 levels at gain 1 (the notes' 2-4, U4)

// Precision-safe hash: a sin-based one loses float32 accuracy once the field counter grows and degenerates into a
// woven pattern, which is exactly what a feed must never do. Inputs are kept small for the same reason.
float hash(vec3 p) {
	vec3 q = fract(p * vec3(0.1031, 0.1030, 0.0973) + seed * 0.017);
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

// The lens and the sensor pixel aperture band-limit the picture before the encoder ever sees it. Without it a 1080p
// render aliases straight into the subcarrier band and the decoder turns the whole frame into rainbow.
vec3 sensor_read(vec2 uv) {
	return 0.5 * (texture(screen, uv - vec2(0.0, 0.25 / LINES)).rgb
		+ texture(screen, uv + vec2(0.0, 0.25 / LINES)).rgb);
}

float luma_of(vec3 c) {
	return dot(c, vec3(0.299, 0.587, 0.114));
}

void fragment() {
	// --- field sampling (P1): one PAL field per 1/50 s, bob-deinterlaced, sampled at the render rate.
	float fw = mod(field, 1024.0);
	float parity = mod(field, 2.0);
	float line = floor(SCREEN_UV.y * LINES - 0.5 * parity);
	float sy = (line + 0.5 + 0.5 * parity) / LINES;
	// Only the line index modulo a few fields matters to the PAL phase, and keeping it small keeps it exact.
	float abs_line = line + mod(field, 8.0) * 312.5;
	float vsw = mod(abs_line, 2.0) < 1.0 ? 1.0 : -1.0;
	float ph_line = 0.75 * abs_line;

	// --- 17 scene taps along the line, one composite sample apart, each the mean of the two midpoint reads beside
	// it: that pair is the horizontal sensor aperture, and it nulls exactly where the line would otherwise fold.
	vec3 mid[18];
	for (int i = 0; i < 18; i++)
		mid[i] = sensor_read(vec2(SCREEN_UV.x + (float(i) - 8.5) / SC_SAMPLES, sy));
	vec3 tap[17];
	for (int i = 0; i < 17; i++)
		tap[i] = 0.5 * (mid[i] + mid[i + 1]);

	// --- P3 sharpening halo, before the encode. A quality drop sharpens harder, which is what pushes more luma
	// detail into the subcarrier band and makes the rainbow bloom on its own.
	float sharpen = SHARPEN * (1.0 + 1.6 * degrade);
	vec3 sharp[13];
	for (int i = 0; i < 13; i++) {
		int c = i + 2;
		sharp[i] = tap[c] * (1.0 + 2.0 * sharpen) - sharpen * (tap[c - 3] + tap[c + 3]);
	}

	// --- N2: optical power near the receiver threshold puts short impulses on the composite. Their dash colours are
	// whatever the chroma decoder makes of an impulse at that subcarrier phase, not a palette; as in the footage the
	// density follows the link margin and the amount of fine texture under them.
	float texture_gate = clamp(0.15 + 12.0 * abs(luma_of(tap[8]) - 0.5 * (luma_of(tap[5]) + luma_of(tap[11]))), 0.0, 2.0);
	float imp_rate = (0.0002 + 0.010 * (1.0 - link_margin) * (1.0 - link_margin) + 0.001 * motor_current
		+ 0.004 * degrade) * texture_gate;

	// --- N3, the ~390 Hz power-rail transient: one dark line every ~40 field lines, the set rolling. It goes on the
	// signal, one line tall and flat across the width, so the decoder keeps it colourless as the footage shows.
	float n3 = n3_amp * step(fract(line / n3_spacing + n3_roll), 1.0 / n3_spacing);

	// --- encode, disturb, decode.
	float grain_sigma = GRAIN * gain * (1.0 + 2.2 * degrade);
	float comp[13];
	for (int i = 0; i < 13; i++) {
		vec3 c = clamp(sharp[i], 0.0, 1.0);
		float y = luma_of(c);
		float u = 0.493 * (c.b - y);
		float v = 0.877 * (c.r - y);
		float x = SCREEN_UV.x + float(i - 6) / SC_SAMPLES;
		float ph = TWO_PI * (SC_CYCLES * x + ph_line);
		comp[i] = y + u * sin(ph) + v * vsw * cos(ph) - n3;

		// N1: always on, flat over one luma sample, a new pattern every field line and every field.
		comp[i] += (hash(vec3(floor(x * SC_SAMPLES * 0.5), line, fw)) - 0.5) * 2.0 * grain_sigma;

		float imp = hash(vec3(floor(x * SC_SAMPLES * 0.25), line, fw + 17.0));
		comp[i] += step(imp, imp_rate) * (imp < imp_rate * 0.5 ? 0.18 : -0.18);
	}

	// Y: a 4-tap box is exactly one subcarrier cycle, so it notches the subcarrier out; what the notch leaves behind
	// on moving chroma is the dot crawl. It also rolls the luma off near 2.6 MHz (P2).
	float y_dec = 0.25 * (comp[5] + comp[6] + comp[7] + comp[8]);

	// U/V: synchronous detection over 13 samples, Hann-windowed. The window is ~0.7 us, which is the measured 5x
	// lower chroma bandwidth and the 0-6 px rightward offset (P4). Luma detail inside the passband survives the
	// detector as false chroma: that is P5 cross-colour, not an added effect.
	float u_dec = 0.0, v_dec = 0.0, wsum = 0.0;
	for (int i = 0; i < 13; i++) {
		float w = 0.5 - 0.5 * cos(TWO_PI * float(i) / 12.0);
		float x = SCREEN_UV.x + float(i - 6) / SC_SAMPLES;
		float ph = TWO_PI * (SC_CYCLES * x + ph_line);
		u_dec += comp[i] * sin(ph) * w;
		v_dec += comp[i] * vsw * cos(ph) * w;
		wsum += w;
	}
	u_dec = 2.0 * u_dec / wsum;
	v_dec = 2.0 * v_dec / wsum;

	// C3: analog cameras drop chroma as gain rises, and a quality drop takes more of it with it.
	float sat = clamp(1.0 - 0.32 * (gain - 1.0) - 0.35 * degrade, 0.0, 1.0);
	u_dec *= sat;
	v_dec *= sat;

	// --- N4 diagonal dotted interference: a staircase of dashes stepping ~8 px right per line, gated to the top of
	// the field. Authored, because the decoder only reproduces its beat, not its dash structure.
	if (n4_amp > 0.0 && sy < n4_span) {
		float diag = fract(SCREEN_UV.x * 10.0 - line * (8.0 / 1920.0) * 10.0);
		float amp = step(fract(SCREEN_UV.x * LUMA * 0.5), 0.5) * step(diag, 0.06) * n4_amp * 2.0;
		y_dec += amp * 0.5;
		u_dec += amp * (mod(line, 2.0) < 1.0 ? 1.1 : -0.6);
		v_dec += amp * (mod(line, 2.0) < 1.0 ? -0.7 : 0.9);
	}

	// A drop also pulls the black level and the gain about, as the dim-light steps of N13 do.
	y_dec = y_dec * (1.0 - 0.10 * degrade) - 0.03 * degrade;

	vec3 rgb = vec3(
		y_dec + 1.140 * v_dec,
		y_dec - 0.394 * u_dec - 0.581 * v_dec,
		y_dec + 2.028 * u_dec);

	// O2 vignette, ~12 % in the corners.
	vec2 d = (SCREEN_UV - 0.5) * vec2(2.0, 1.125);
	rgb *= 1.0 - 0.12 * dot(d, d);

	// P7: highlights clip flat with a lilac cast in the last stop.
	rgb += vec3(0.05, 0.0, 0.07) * smoothstep(0.86, 1.0, y_dec);
	rgb = clamp(rgb, 0.0, 1.0);

	// --- the cut: the receiver shows its raw input instead of the picture. Near-binary white dashes one field line
	// tall and 6-8 px long, about 20 % white over black, the field's level flickering between 52 and 65 (the N5-N7
	// snow texture). A new pattern every field, so a cut never reads as a frozen still.
	if (snow > 0.5) {
		float s = hash(vec3(floor(SCREEN_UV.x * LUMA * 0.55), line, fw + 511.0));
		float level = step(0.80, s) * (0.72 + 0.28 * fract(s * 37.0)) + step(0.62, s) * 0.10;
		rgb = vec3(level * (0.85 + 0.30 * hash(vec3(fw, 5.0, 9.0))));
	}

	// P8 receiver border: 4 black rows at the top, 2 at the bottom.
	rgb *= step(0.0037, SCREEN_UV.y) * step(SCREEN_UV.y, 0.9981);

	COLOR = vec4(rgb, 1.0);
}
