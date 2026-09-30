shader_type canvas_item;

// The analog feed. One pass over the 3D render.
//
// The pilot picked this from the three spike candidates: the baseline is the simulated composite chain (candidate 1)
// held back to the restraint of the authored version (candidate 2), and the heavier degraded look (candidate 3) is
// not a separate mode but something the feed slides into and back out of at random.
//
// Structure, per output pixel:
//   1. the receiver's line-sync tear, which shifts where the whole chain is read from (N8)
//   2. 18 band-limited sensor reads along one field line, paired into 17 composite taps (P1, P2)
//   3. horizontal unsharp before the encode, so the halo softens with the signal (P3)
//   4. the OSD, inserted on the drone: white glyphs with a black outline over the picture, BEFORE the encode, so it
//      picks up the soft horizontal edges, the coloured fringes and the cross-colour the footage shows on it (§5)
//   5. PAL encode: luma plus the subcarrier, V-switched per line, 0.75 cycles of phase per line
//   6. on the SIGNAL: N1 grain, N2 link impulses, N3 the rail transient
//   7. decode: a 4-tap notch for luma, a 13-tap windowed synchronous detector for chroma. The chroma smear, the
//      cross-colour rainbow and the dot crawl fall out of this; none of them is drawn (P4, P5)
//   8. authored on the decoded picture: N4 diagonal interference
//   9. camera and receiver trim: saturation vs gain (C3), vignette (O2), highlight clip (P7)
//  10. the receiver's loss of picture: the N12 precursor, partial and full snow, the no-signal screen, the black
//      dropout and the receiver's own text (N5-N8, N12), then the border (P8)
//
// `degrade` (0-1) slides the composite stages toward the heavy look; the `loss_*` set is the state of FeedLoss, which
// draws the sequence. Reference: docs/reference-notes/video-feed.md, and docs/reference-notes/pilot-requirements.md
// PR-2 and PR-4.
//
// The OSD is read at the undistorted x while the scene is read through the lens. When barrel distortion (O1) lands it
// belongs on the `sensor_read` coordinate only, so the OSD keeps its straight horizon while the real one bends
// (A f236). Keep that split.

uniform sampler2D screen : hint_screen_texture, repeat_disable, filter_linear;

// The character layer, from FeedOsd: 30 x 18 cells of glyph codes (the OSD in rows 0-15, the receiver's two text
// lines in 16-17), and the atlas FeedFont bakes - one row of 12 x 18 cells, R the glyph and G its outline.
uniform sampler2D osd_codes : filter_nearest, repeat_disable;
uniform sampler2D osd_glyphs : filter_nearest, repeat_disable;
uniform float osd_cells = 1.0;

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

// --- the receiver's loss of picture. Every one of these is a FeedLoss output; it draws their values per event.
uniform float loss_state = 0.0;    // 0 picture, 1 partial snow, 2 the no-signal screen, 3 full snow, 4 black
uniform float loss_split = 0.85;   // partial snow: snow above this fraction of the height, picture below
uniform float loss_top_strip = 0.0;// a picture strip surviving at the very top of partial snow, 0 = none
uniform float black_a = 0.0;       // the black dropout covers these rows; it enters and leaves partway down a field
uniform float black_b = 1.0;
uniform float snow_level = 0.22;   // the snow field's mean level for this field (52-65 of 255)
uniform float snow_chroma = 0.0;   // 1 while the snow is still in colour, 0 once the colour killer has engaged
uniform float tell = 0.0;          // the N12 precursor: extra luma as a fraction, under dense bands of impulses
uniform float tear_amp = 0.0;      // N8 line-sync tear
uniform float tear_top = 0.0;      // 1 = the shear is at the top of the field, 0 = at the bottom
uniform float grain_boost = 0.0;   // the hard cut's other pre-cut glitch: a ~10 % rise in grain
uniform float rx_text = 0.0;       // 1 while the receiver keeps its own text up over the picture after a dropout

const float TWO_PI = 6.2831853;
const float LINES = 286.0;      // active lines per field, measured 286 on the B-G receiver (P1)
const float SC_SAMPLES = 920.0; // composite samples per active line (4 x subcarrier)
const float SC_CYCLES = 230.0;  // subcarrier cycles per active line (4.43 MHz x 52 us)
const float LUMA = 450.0;       // effective luma samples per line (P2)
const float SHARPEN = 0.30;     // P3, radius 3 composite samples so its peak does not sit on the subcarrier
const float GRAIN = 0.011;      // N1, about 2.8 levels at gain 1 (the notes' 2-4, U4)

const float OSD_COLS = 30.0;    // §5: a 30-column by 16-row character grid
const float OSD_ROWS = 16.0;
const float CODE_ROWS = 18.0;   // the 16 OSD rows plus the receiver's two text lines
const float OSD_WHITE = 0.96;   // the OSD whites measure 210-255; they are the brightest thing on screen (C5)
const float OSD_BLACK = 0.02;

// The no-signal screen of this simulator's receiver. §0 measures two receivers; this is the B-G one, which is also
// the one whose 286 field lines and 4-px top border the rest of this shader is built on: flat RGB (0, 8-11, 197-202)
// with two short lines of green text at the top left. (The A/H receiver is a brighter pure blue, B 245-255, with
// small centred text, 275 lines and a 1-px border. U13 keeps the choice open.)
const vec3 NO_SIGNAL_BLUE = vec3(0.0, 0.039, 0.784);
const vec3 RX_TEXT_GREEN = vec3(0.16, 0.85, 0.28);
const float TEAR_PX = 60.0;     // N8 displaces lines by 20-60 px at 1920 wide

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

// The character grid at (x, y), as (glyph, outline). `row0` is the first code row of the block: 0 for the OSD, 16 for
// the receiver's own text, which then lands in the top two rows of the display.
vec2 grid_ink(float x, float y, float row0) {
	if (x < 0.0 || x >= 1.0) {
		return vec2(0.0);
	}
	float cx = x * OSD_COLS;
	float cy = y * OSD_ROWS;
	float row = floor(cy) + row0;
	if (row < 0.0 || row >= CODE_ROWS) {
		return vec2(0.0);
	}
	float code = texture(osd_codes, vec2((floor(cx) + 0.5) / OSD_COLS, (row + 0.5) / CODE_ROWS)).r;
	if (code < 0.5) {
		return vec2(0.0);
	}
	return texture(osd_glyphs, vec2((code + fract(cx)) / osd_cells, fract(cy))).rg;
}

// The receiver showing its raw input: near-binary white dashes on black, one field line tall and 6-8 px long, about
// 20 % of pixels white, with a faint diagonal herringbone through the whole field. A new pattern every field, so a
// loss never reads as a frozen still. The first fields of a partial snow still carry colour.
vec3 snow_of(float x, float line, float fw) {
	float s = hash(vec3(floor(x * LUMA * 0.55), line, fw + 511.0));
	float white = step(0.80, s);
	float lvl = white * (0.72 + 0.28 * fract(s * 37.0)) + step(0.62, s) * 0.10;
	lvl += 0.04 * sin(TWO_PI * (x * 57.0 + line * 0.5));  // the herringbone
	vec3 c = vec3(lvl * snow_level / 0.21);               // scaled to the level drawn for this field
	if (snow_chroma > 0.0) {
		float h = hash(vec3(floor(x * LUMA * 0.55), line, fw + 733.0));
		c += snow_chroma * 0.20 * white * (vec3(fract(h * 5.0), fract(h * 11.0), fract(h * 17.0)) - 0.5);
	}
	return clamp(c, 0.0, 1.0);
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

	// --- N8 tearing: the receiver's line sync slips and a whole line lands sideways of where it belongs. The OSD is
	// already in the signal by then, which is why its rows shear with the picture (H f628). Reading the chain at a
	// shifted x is that shift. The amplitude rises towards the torn edge and is correlated over 3 lines.
	float tear_band = tear_top > 0.5 ? smoothstep(0.18, 0.0, sy) : smoothstep(0.82, 1.0, sy);
	float px = SCREEN_UV.x;
	if (tear_amp > 0.0) {
		px += (hash(vec3(floor(line / 3.0), 77.0, fw)) - 0.5) * 2.0
			* tear_amp * tear_band * TEAR_PX / 1920.0;
	}

	// --- 17 scene taps along the line, one composite sample apart, each the mean of the two midpoint reads beside
	// it: that pair is the horizontal sensor aperture, and it nulls exactly where the line would otherwise fold.
	vec3 mid[18];
	for (int i = 0; i < 18; i++) {
		mid[i] = sensor_read(vec2(px + (float(i) - 8.5) / SC_SAMPLES, sy));
	}
	vec3 tap[17];
	for (int i = 0; i < 17; i++) {
		tap[i] = 0.5 * (mid[i] + mid[i + 1]);
	}

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
	// The N12 precursor puts its impulses in dense horizontal bands, not over the whole field.
	if (tell > 0.0) {
		imp_rate += step(0.62, hash(vec3(floor(line / 24.0), 131.0, fw))) * (0.05 + 0.10 * tell);
	}

	// --- N3, the ~390 Hz power-rail transient: one dark line every ~40 field lines, the set rolling. It goes on the
	// signal, one line tall and flat across the width, so the decoder keeps it colourless as the footage shows.
	float n3 = n3_amp * step(fract(line / n3_spacing + n3_roll), 1.0 / n3_spacing);

	// --- encode, disturb, decode.
	float grain_sigma = GRAIN * gain * (1.0 + 2.2 * degrade) * (1.0 + grain_boost);
	float comp[13];
	for (int i = 0; i < 13; i++) {
		float x = px + float(i - 6) / SC_SAMPLES;
		vec3 c = clamp(sharp[i], 0.0, 1.0);

		// The OSD is inserted here, on the drone, between the camera and the composite encoder: white glyphs with a
		// black outline, hard-edged, at the undistorted x. Everything the footage shows on it - the 3-4 px edge rise,
		// the coloured fringes, the cross-colour near busy texture, the shear of a tear - comes from what follows.
		vec2 osd = grid_ink(x, sy, 0.0);
		c = mix(c, vec3(OSD_BLACK), osd.y);
		c = mix(c, vec3(OSD_WHITE), osd.x);

		float y = luma_of(c);
		float u = 0.493 * (c.b - y);
		float v = 0.877 * (c.r - y);
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
		float x = px + float(i - 6) / SC_SAMPLES;
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
		float diag = fract(px * 10.0 - line * (8.0 / 1920.0) * 10.0);
		float amp = step(fract(px * LUMA * 0.5), 0.5) * step(diag, 0.06) * n4_amp * 2.0;
		y_dec += amp * 0.5;
		u_dec += amp * (mod(line, 2.0) < 1.0 ? 1.1 : -0.6);
		v_dec += amp * (mod(line, 2.0) < 1.0 ? -0.7 : 0.9);
	}

	// A drop also pulls the black level and the gain about, as the dim-light steps of N13 do. The N12 precursor
	// brightens the whole picture, OSD included, because it acts on the signal after the insert.
	y_dec = y_dec * (1.0 - 0.10 * degrade) - 0.03 * degrade;
	y_dec *= 1.0 + tell;

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

	// A tear cuts a black wedge in from the right edge as well (P f1144).
	if (tear_amp > 0.0) {
		float wedge = tear_amp * tear_band * 0.10 * hash(vec3(floor(line / 3.0), 211.0, fw));
		rgb *= step(SCREEN_UV.x, 1.0 - wedge);
	}

	// --- the receiver's loss of picture (N5-N8, N12). FeedLoss holds the sequence; this only draws the state.
	bool snowy = false;
	if (loss_state > 0.5 && loss_state < 1.5) {
		// Stage 1 of a staged loss: snow down to the split line, the previous picture surviving below it and
		// sometimes in a strip at the very top.
		snowy = sy < loss_split && sy > loss_top_strip;
	} else if (loss_state > 1.5 && loss_state < 2.5) {
		rgb = NO_SIGNAL_BLUE;
	} else if (loss_state > 2.5 && loss_state < 3.5) {
		snowy = true;
	} else if (loss_state > 3.5) {
		// The short dropout: black, OSD included, entering and leaving partway down a field.
		rgb *= 1.0 - step(black_a, sy) * step(sy, black_b);
	}
	if (snowy) {
		rgb = snow_of(px, line, fw);
	}

	// The receiver draws its own two short lines of green text: on the no-signal screen and on the snow it puts up
	// (which is how we know that snow is the receiver's, not the camera's), and for ~7.3 s after a dropout recovers.
	// The black dropout never brings them up - the receiver kept its lock through it.
	if ((loss_state > 1.5 && loss_state < 3.5) || rx_text > 0.5) {
		vec2 t = grid_ink(SCREEN_UV.x, sy, OSD_ROWS);
		rgb = mix(rgb, vec3(0.0), t.y);
		rgb = mix(rgb, RX_TEXT_GREEN, t.x);
	}

	// P8 receiver border: 4 black rows at the top, 2 at the bottom.
	rgb *= step(0.0037, SCREEN_UV.y) * step(SCREEN_UV.y, 0.9981);

	COLOR = vec4(rgb, 1.0);
}
