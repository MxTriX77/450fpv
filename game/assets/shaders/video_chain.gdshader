shader_type canvas_item;

// Spike candidate 1: SIMULATED CHAIN.
//
// The render is encoded to a PAL composite signal and decoded back. Nothing below draws an artifact: the field
// structure, the soft luma, the chroma smear, the cross-colour rainbow, the dot crawl, and the colour fringes on the
// interference all fall out of the encode/decode. Reference: docs/reference-notes/video-feed.md P1-P5, N1, N3, N4.
//
// Per output pixel: 17 band-limited scene taps -> horizontal unsharp (P3) -> encode 13 taps to composite -> add the
// N1 grain, the N3 rail transient and the N4 interference sinusoid to the SIGNAL -> decode Y with a 4-tap notch and
// U/V with a 13-tap windowed synchronous detector -> back to RGB.
//
// Randomness: N1 is redrawn from (seed, field, line, sample) every field and is never off, so the picture is never
// clean and never loops. N3 and N4 amplitudes come from FeedEvents, which fires them stochastically at the measured
// rates. Everything is a function of the seed and the field index, so a flight replays identically.

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

const float TWO_PI = 6.2831853;
const float LINES = 286.0;      // active lines per field, measured 286 on the B-G receiver (P1)
const float SC_SAMPLES = 920.0; // composite samples per active line (4 x subcarrier)
const float SC_CYCLES = 230.0;  // subcarrier cycles per active line (4.43 MHz x 52 us)
const float SHARPEN = 0.45;     // P3, radius 3 composite samples so its peak does not sit on the subcarrier
const float GRAIN = 0.013;      // N1, about 3.3 levels at gain 1

// The lens and the sensor pixel aperture band-limit the picture before the encoder ever sees it. Without it a 1080p
// render aliases straight into the subcarrier band and the decoder turns the whole frame into rainbow. Two reads
// half a field line apart give the vertical aperture; the horizontal one comes from pairing the midpoint reads.
vec3 sensor_read(vec2 uv) {
	return 0.5 * (texture(screen, uv - vec2(0.0, 0.25 / LINES)).rgb
		+ texture(screen, uv + vec2(0.0, 0.25 / LINES)).rgb);
}

// Precision-safe hash: a sin-based one loses float32 accuracy once the field counter grows and degenerates into a
// woven pattern, which is exactly what a feed must never do. Inputs are kept small for the same reason.
float hash(vec3 p) {
	vec3 q = fract(p * vec3(0.1031, 0.1030, 0.0973) + seed * 0.017);
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

float luma_of(vec3 c) {
	return dot(c, vec3(0.299, 0.587, 0.114));
}

void fragment() {
	// --- field sampling (P1): one PAL field per 1/50 s, bob-deinterlaced, sampled at the render rate.
	float parity = mod(field, 2.0);
	float line = floor(SCREEN_UV.y * LINES - 0.5 * parity);
	float sy = (line + 0.5 + 0.5 * parity) / LINES;
	float abs_line = line + mod(field, 8.0) * 312.5; // PAL: the subcarrier phase carries over between fields

	// --- 17 scene taps along the line, one composite sample apart, each the mean of the two midpoint reads beside
	// it: that pair is the horizontal sensor aperture, and it nulls exactly where the line would otherwise fold.
	vec3 mid[18];
	for (int i = 0; i < 18; i++)
		mid[i] = sensor_read(vec2(SCREEN_UV.x + (float(i) - 8.5) / SC_SAMPLES, sy));
	vec3 tap[17];
	for (int i = 0; i < 17; i++)
		tap[i] = 0.5 * (mid[i] + mid[i + 1]);

	// --- P3 sharpening halo, applied before the band-limit so it softens like the footage does.
	vec3 sharp[13];
	for (int i = 0; i < 13; i++) {
		int c = i + 2;
		sharp[i] = tap[c] * (1.0 + 2.0 * SHARPEN) - SHARPEN * (tap[c - 3] + tap[c + 3]);
	}

	// --- N2: optical power near the receiver threshold puts short impulses on the composite. Their dash colours are
	// whatever the chroma decoder makes of an impulse at that subcarrier phase, not a palette; as in the footage,
	// the density follows the link margin and the amount of fine texture under them.
	float texture_gate = clamp(0.15 + 12.0 * abs(luma_of(tap[8]) - 0.5 * (luma_of(tap[5]) + luma_of(tap[11]))), 0.0, 2.0);
	float imp_rate = (0.0003 + 0.015 * (1.0 - link_margin) * (1.0 - link_margin) + 0.0015 * motor_current) * texture_gate;

	// --- N3, the ~390 Hz power-rail transient: one dark line every ~40 field lines, the set rolling. It is put on
	// the signal, one line tall and flat across the width, so the decoder keeps it colourless as the footage shows.
	float n3 = n3_amp * step(fract(line / n3_spacing + n3_roll), 1.0 / n3_spacing);

	// --- encode each tap to one composite sample.
	float vsw = mod(abs_line, 2.0) < 1.0 ? 1.0 : -1.0; // PAL V-switch
	float comp[13];
	for (int i = 0; i < 13; i++) {
		vec3 c = clamp(sharp[i], 0.0, 1.0);
		float y = luma_of(c);
		float u = 0.493 * (c.b - y);
		float v = 0.877 * (c.r - y);
		float x = SCREEN_UV.x + float(i - 6) / SC_SAMPLES;
		// 283.75 subcarrier cycles per line: the +0.75 cycle per line is what makes the dots crawl.
		float ph = TWO_PI * (SC_CYCLES * x + 0.75 * abs_line);
		comp[i] = y + u * sin(ph) + v * vsw * cos(ph) - n3;

		// N1 grain, on the signal: flat over one luma sample, a new pattern every field line and every field.
		comp[i] += (hash(vec3(floor(x * SC_SAMPLES * 0.5), line, mod(field, 1024.0))) - 0.5) * 2.0 * GRAIN * gain;

		float imp = hash(vec3(floor(x * SC_SAMPLES * 0.25), line, mod(field, 1024.0) + 17.0));
		comp[i] += step(imp, imp_rate) * (imp < imp_rate * 0.5 ? 0.18 : -0.18);

		// N4, an airframe interference source near the colour subcarrier, gated to the top of the field. One
		// sinusoid: 10 cycles per line above the subcarrier, so the decoder beats it down to the ~10 diagonal lines
		// the footage shows, and 0.79 cycles of phase per line, so the beat steps ~8 px right per line. The
		// staircase and its colour fringes are whatever the decoder makes of that; nothing here draws them.
		comp[i] += n4_amp * step(sy, n4_span) * sin(TWO_PI * ((SC_CYCLES + 10.0) * x + 0.792 * abs_line));
	}

	// --- decode. Y: a 4-tap box is exactly one subcarrier cycle, so it notches the subcarrier out; what the notch
	// leaves behind on moving chroma is the dot pattern. It also rolls the luma off near 2.6 MHz (P2).
	float y_dec = 0.25 * (comp[5] + comp[6] + comp[7] + comp[8]);

	// U/V: synchronous detection over 13 samples, Hann-windowed. The window is ~0.7 us, which is the measured 5x
	// lower chroma bandwidth and the 0-6 px rightward offset (P4). Luma detail inside the passband survives the
	// detector as false chroma: that is P5 cross-colour, not an added effect.
	float u_dec = 0.0, v_dec = 0.0, wsum = 0.0;
	for (int i = 0; i < 13; i++) {
		float w = 0.5 - 0.5 * cos(TWO_PI * float(i) / 12.0);
		float x = SCREEN_UV.x + float(i - 6) / SC_SAMPLES;
		float ph = TWO_PI * (SC_CYCLES * x + 0.75 * abs_line);
		u_dec += comp[i] * sin(ph) * w;
		v_dec += comp[i] * vsw * cos(ph) * w;
		wsum += w;
	}
	u_dec = 2.0 * u_dec / wsum;
	v_dec = 2.0 * v_dec / wsum;

	// C3: analog cameras drop chroma as gain rises; at the night gain the picture goes monochrome (C5).
	float sat = clamp(1.0 - 0.32 * (gain - 1.0), 0.0, 1.0);
	u_dec *= sat;
	v_dec *= sat;

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

	// P8 receiver border: 4 black rows at the top, 2 at the bottom.
	rgb *= step(0.0037, SCREEN_UV.y) * step(SCREEN_UV.y, 0.9981);

	COLOR = vec4(rgb, 1.0);
}
