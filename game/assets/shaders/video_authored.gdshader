shader_type canvas_item;

// Spike candidate 2: AUTHORED APPROXIMATION.
//
// No signal is modelled. Every trait of docs/reference-notes/video-feed.md is drawn directly at the size and strength
// the notes measured: field lines, soft luma with a dark halo, chroma smeared 5x wider and offset right, grain,
// coloured sparkle dashes, rolling dark lines, diagonal dotted interference. 7 scene taps.
//
// Randomness is the same as the other two candidates: N1 is redrawn from (seed, field, line, sample) every field,
// and N3/N4 fire from FeedEvents. What is fixed here is the LOOK of each artifact, not its timing — the sparkle
// colours, the fringes on the diagonal dots and the rainbow on fine texture are chosen here rather than falling out
// of a subcarrier, so a new texture or a new interference frequency does not change them on its own.

uniform sampler2D screen : hint_screen_texture, repeat_disable, filter_linear;

uniform float gain = 1.0;
uniform float link_margin = 1.0;
uniform float motor_current = 0.0;
uniform float seed = 1.0;
uniform float field = 0.0;
uniform float n3_amp = 0.0;
uniform float n3_roll = 0.0;
uniform float n3_spacing = 40.0;
uniform float n4_amp = 0.0;
uniform float n4_span = 0.0;

const float TWO_PI = 6.2831853;
const float LINES = 286.0;
const float LUMA = 450.0;     // effective luma samples per line (P2)
const float GRAIN = 0.013;

// The sensor aperture: half a field line vertically, half a luma sample horizontally. Without it the 1080p render's
// fine texture aliases into the chroma.
vec3 sensor_read(float x, float y) {
	vec2 o = vec2(0.25 / LUMA, 0.25 / LINES);
	return 0.25 * (texture(screen, vec2(x - o.x, y - o.y)).rgb + texture(screen, vec2(x + o.x, y - o.y)).rgb
		+ texture(screen, vec2(x - o.x, y + o.y)).rgb + texture(screen, vec2(x + o.x, y + o.y)).rgb);
}

float hash(vec3 p) {
	return fract(sin(dot(p + seed, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float luma_of(vec3 c) {
	return dot(c, vec3(0.299, 0.587, 0.114));
}

void fragment() {
	// P1 field lines: sample at the centre of one field line, so diagonals stair-step and fine detail breaks up.
	float parity = mod(field, 2.0);
	float line = floor(SCREEN_UV.y * LINES - 0.5 * parity);
	float sy = (line + 0.5 + 0.5 * parity) / LINES;

	float dx = 1.0 / LUMA;
	vec3 c0 = sensor_read(SCREEN_UV.x, sy);
	vec3 cl = sensor_read(SCREEN_UV.x - dx, sy);
	vec3 cr = sensor_read(SCREEN_UV.x + dx, sy);

	// P2 + P3: soften the luma over one source sample, then draw the unsharp halo on top (dark undershoot ~40 levels
	// on the dark side of a strong edge, a smaller overshoot on the bright side).
	float y_soft = luma_of(c0) * 0.5 + 0.25 * (luma_of(cl) + luma_of(cr));
	float detail = luma_of(c0) - 0.5 * (luma_of(cl) + luma_of(cr));

	// P4: chroma from a window ~5x wider than luma, shifted a sample to the right.
	vec3 cw = c0 + cl + cr
		+ sensor_read(SCREEN_UV.x - 2.0 * dx, sy) + sensor_read(SCREEN_UV.x + 2.0 * dx, sy)
		+ sensor_read(SCREEN_UV.x + 3.0 * dx, sy) + sensor_read(SCREEN_UV.x + 4.0 * dx, sy);
	cw /= 7.0;
	float yw = luma_of(cw);
	float u = 0.493 * (cw.b - yw);
	float v = 0.877 * (cw.r - yw);

	// The halo is drawn around the SOFT picture, not around the original: the feed carries ~450 samples per line and
	// the sharpener sits on top of that, so the edge stays soft and only gains its undershoot.
	float y = y_soft + 0.9 * (y_soft - yw);

	// P5 cross-colour: fine luma detail is repainted as a chroma streak whose sign flips line to line and field to
	// field, which is what makes it shimmer. Drawn, not decoded.
	float phase = mod(line + field, 2.0) * 2.0 - 1.0;
	float cross = clamp(detail * 3.0, -0.16, 0.16) * phase; // the measured bands reach +-30-40 levels
	u += cross;
	v -= cross * 0.8;

	// C3 saturation falls off at high gain.
	float sat = clamp(1.0 - 0.32 * (gain - 1.0), 0.0, 1.0);
	u *= sat;
	v *= sat;

	// N1 grain: one luma sample wide, one field line tall, a new pattern every field, sigma follows the AE gain.
	float cell = floor(SCREEN_UV.x * LUMA);
	y += (hash(vec3(cell, line, field)) - 0.5) * 2.0 * GRAIN * gain;
	float cnoise = (hash(vec3(floor(cell * 0.2), line, field + 91.0)) - 0.5) * 2.0 * GRAIN * gain * sat;
	u += cnoise;
	v -= cnoise * 0.7;

	// N2 sparkles: coloured dashes one field line tall and ~2 luma samples long, about 45-60 levels off the local
	// mean. Density follows the link margin AND the amount of fine texture, which is why the footage shows them on
	// grass and walls and almost none on flat sky.
	float dash = floor(SCREEN_UV.x * LUMA / 2.0);
	float s = hash(vec3(dash, line, field + 17.0));
	float density = (0.0006 + 0.03 * (1.0 - link_margin) * (1.0 - link_margin) + 0.003 * motor_current)
		* clamp(0.15 + 12.0 * abs(detail), 0.0, 2.0);
	if (s < density) {
		float t = s / density;
		u += (t < 0.5 ? 0.09 : -0.09) * sat;
		v += (fract(t * 7.0) < 0.5 ? 0.10 : -0.10) * sat;
	}

	// N3 rolling thin dark lines: one field line tall, 15-30 levels deep, ~40 lines apart, the set rolling down.
	y -= n3_amp * step(fract(line / n3_spacing + n3_roll), 1.0 / n3_spacing);

	// N4 diagonal dotted interference: a staircase of dashes stepping ~8 px right per line, blue/white with
	// yellow/green fringes, gated to the top of the field. Every colour here is a choice, not a decode.
	if (n4_amp > 0.0 && sy < n4_span) {
		float diag = fract(SCREEN_UV.x * 10.0 - line * (8.0 / 1920.0) * 10.0);
		float amp = step(fract(SCREEN_UV.x * LUMA * 0.5), 0.5) * step(diag, 0.06) * n4_amp * 2.0;
		y += amp * 0.5;
		u += amp * (mod(line, 2.0) < 1.0 ? 1.1 : -0.6);
		v += amp * (mod(line, 2.0) < 1.0 ? -0.7 : 0.9);
	}

	vec3 rgb = vec3(
		y + 1.140 * v,
		y - 0.394 * u - 0.581 * v,
		y + 2.028 * u);

	vec2 d = (SCREEN_UV - 0.5) * vec2(2.0, 1.125);
	rgb *= 1.0 - 0.12 * dot(d, d);
	rgb += vec3(0.05, 0.0, 0.07) * smoothstep(0.86, 1.0, y);
	rgb = clamp(rgb, 0.0, 1.0);
	rgb *= step(0.0037, SCREEN_UV.y) * step(SCREEN_UV.y, 0.9981);

	COLOR = vec4(rgb, 1.0);
}
