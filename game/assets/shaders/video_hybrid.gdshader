shader_type canvas_item;

// Spike candidate 3: HYBRID.
//
// The core is a real PAL encode/decode, as in candidate 1, so the field structure, the soft luma, the chroma smear,
// the cross-colour rainbow and the dot crawl still emerge from the signal. The catalogue events are authored on top
// of the decoded picture instead, where each one has a direct knob: N2 sparkles, N3 rolling lines, N4 diagonal dots
// and the N5-N7/N12 receiver states.
//
// Two deliberate savings against candidate 1: 13 scene taps instead of 17, and the P3 halo is added after the
// band-limit rather than before, so its edge is a little harder than the footage shows.

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
const float SC_SAMPLES = 920.0;
const float SC_CYCLES = 230.0;
const float LUMA = 450.0;
const float GRAIN = 0.013;

// The sensor aperture, as in candidate 1: two reads half a field line apart, paired horizontally by the caller.
vec3 sensor_read(vec2 uv) {
	return 0.5 * (texture(screen, uv - vec2(0.0, 0.25 / LINES)).rgb
		+ texture(screen, uv + vec2(0.0, 0.25 / LINES)).rgb);
}

float hash(vec3 p) {
	return fract(sin(dot(p + seed, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float luma_of(vec3 c) {
	return dot(c, vec3(0.299, 0.587, 0.114));
}

void fragment() {
	float parity = mod(field, 2.0);
	float line = floor(SCREEN_UV.y * LINES - 0.5 * parity);
	float sy = (line + 0.5 + 0.5 * parity) / LINES;
	float abs_line = line + field * 312.5;
	float vsw = mod(abs_line, 2.0) < 1.0 ? 1.0 : -1.0;

	// --- real core: encode 13 composite samples, N1 grain on the signal, then decode.
	vec3 mid[14];
	for (int i = 0; i < 14; i++)
		mid[i] = sensor_read(vec2(SCREEN_UV.x + (float(i) - 6.5) / SC_SAMPLES, sy));

	float comp[13];
	float y_raw[13];
	for (int i = 0; i < 13; i++) {
		float x = SCREEN_UV.x + float(i - 6) / SC_SAMPLES;
		vec3 c = 0.5 * (mid[i] + mid[i + 1]);
		float y = luma_of(c);
		y_raw[i] = y;
		float u = 0.493 * (c.b - y);
		float v = 0.877 * (c.r - y);
		float ph = TWO_PI * (SC_CYCLES * x + 0.75 * abs_line);
		comp[i] = y + u * sin(ph) + v * vsw * cos(ph);
		comp[i] += (hash(vec3(floor(x * SC_SAMPLES * 0.5), abs_line, field)) - 0.5) * 2.0 * GRAIN * gain;
	}

	float y_dec = 0.25 * (comp[5] + comp[6] + comp[7] + comp[8]);
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

	// P3 halo, drawn after the band-limit: cheaper, but it does not soften with the signal.
	float detail = y_raw[6] - 0.5 * (y_raw[3] + y_raw[9]);
	y_dec += 0.50 * detail;

	float sat = clamp(1.0 - 0.32 * (gain - 1.0), 0.0, 1.0);
	u_dec *= sat;
	v_dec *= sat;

	// --- authored event layers, on the decoded picture. Their timing comes from FeedEvents exactly as in the other
	// candidates; what is authored is how each one looks.
	// N2 sparkles: dashes one field line tall, ~2 luma samples long; density from the link margin and, as the
	// footage shows, from how much fine texture is under them.
	float dash = floor(SCREEN_UV.x * LUMA / 2.0);
	float s = hash(vec3(dash, line, field + 17.0));
	float density = (0.0006 + 0.03 * (1.0 - link_margin) * (1.0 - link_margin) + 0.003 * motor_current)
		* clamp(0.15 + 12.0 * abs(detail), 0.0, 2.0);
	if (s < density) {
		float t = s / density;
		u_dec += (t < 0.5 ? 0.09 : -0.09) * sat;
		v_dec += (fract(t * 7.0) < 0.5 ? 0.10 : -0.10) * sat;
	}

	// N3 rolling thin dark lines.
	y_dec -= n3_amp * step(fract(line / n3_spacing + n3_roll), 1.0 / n3_spacing);

	// N4 diagonal dotted interference, top of the field.
	if (n4_amp > 0.0 && sy < n4_span) {
		float diag = fract(SCREEN_UV.x * 10.0 - line * (8.0 / 1920.0) * 10.0);
		float amp = step(fract(SCREEN_UV.x * LUMA * 0.5), 0.5) * step(diag, 0.06) * n4_amp * 2.0;
		y_dec += amp * 0.5;
		u_dec += amp * (mod(line, 2.0) < 1.0 ? 1.1 : -0.6);
		v_dec += amp * (mod(line, 2.0) < 1.0 ? -0.7 : 0.9);
	}

	vec3 rgb = vec3(
		y_dec + 1.140 * v_dec,
		y_dec - 0.394 * u_dec - 0.581 * v_dec,
		y_dec + 2.028 * u_dec);

	vec2 d = (SCREEN_UV - 0.5) * vec2(2.0, 1.125);
	rgb *= 1.0 - 0.12 * dot(d, d);
	rgb += vec3(0.05, 0.0, 0.07) * smoothstep(0.86, 1.0, y_dec);
	rgb = clamp(rgb, 0.0, 1.0);

	// N5-N7 / N12 receiver states. Losses are event-driven in the notes (impact, power loss, fiber break), never a
	// random rate, so these only show what the link margin says; the staged sequence and its drawn durations are
	// the hook that FeedEvents.TriggerLoss fills in once physics can report a link event.
	if (link_margin < 0.16) {
		float snow = step(0.62, hash(vec3(floor(SCREEN_UV.x * LUMA * 0.7), line, field)));
		rgb = vec3(snow * (0.82 + 0.18 * hash(vec3(field, 1.0, 2.0))));
	}
	if (link_margin < 0.06) {
		rgb = vec3(0.0, 0.04, 0.78);
	}

	rgb *= step(0.0037, SCREEN_UV.y) * step(SCREEN_UV.y, 0.9981);
	COLOR = vec4(rgb, 1.0);
}
