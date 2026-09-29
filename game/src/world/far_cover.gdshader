shader_type spatial;
render_mode cull_disabled;

// Far cover (FarCoverView): one level's grid of grass and straw tufts, density only. Every instance is a tuft at a grid
// point; the node and the instance transforms are identity, so VERTEX is written in world space.
// - The grid point is origin_cell + the instance's row and column, jittered by a hash of the point: stable as the
//   camera moves.
// - A tuft is GRASS_CARDS standing cards and STRAW_CARDS lying ones. Each card's top is cut into pointed teeth of
//   random heights (discarded above), so one card reads as several blades, each at least TOOTH_PIXELS wide on screen.
//   Each grass tooth is green or dry by the surface's dry fraction, shifted ±0.25 by low-frequency noise so the sod
//   mottles, and tall cover darkens more toward its root (a deeper, shaded stand).
// - The cards of a tuft carry all the cover of its grid cell: their area equals the surface's side-on silhouette
//   density (stems × diameter × length per m², SurfaceLook) times the cover channel (nearest cell) times the cell's
//   area. So the cover is as dense at 80 m as at 8 m, at every level. A card narrower than the surface's blade width
//   or MIN_PIXELS on screen (it would shimmer) is drawn at that width with the matching probability instead.
// - Seen from steeply above (the camera's lift over the distance, STEEP), tufts fade out: from there cover is texture.
// - It fades in and out over `band` by a hash against the fade, tuft by tuft, with the near ring's distance
//   (horizontal, with the camera's lift above the terrain).
uniform sampler2D heights : filter_linear, repeat_disable; // the terrain's
uniform float height_offset;
uniform float height_range;
uniform float resolution;
uniform float half_size;
uniform int samples;
uniform sampler2D surface_ids : filter_nearest, repeat_disable;
uniform sampler2D cover : filter_nearest, repeat_disable;
uniform sampler2D surface_params : filter_nearest, repeat_disable; // SurfaceLook.Params
uniform float surface_resolution = 0.5;
uniform int surface_cells = 1;
uniform bool use_holes = false;                          // nothing grows in a hole cell
uniform sampler2D holes : filter_nearest, repeat_disable; // R8: 255 = hole, one texel per surface cell

uniform ivec2 origin_cell;  // the camera's grid cell at this level's spacing
uniform float spacing;      // m between grid points
uniform int per_side;       // grid points per side
uniform vec4 band;          // m: fade in from x to y, out from z to w
uniform int level;
uniform vec3 eye;
uniform float lift;

const int GRASS_CARDS = 3;     // cards 0–2 stand, 3–4 lie (FarCoverView's tuft mesh has 5)
const float STRAW_CARDS = 2.0;
const float MIN_PIXELS = 1.3;
const float TOOTH_PIXELS = 1.6;
const float TOOTH_FILL = 0.3625; // mean share of a card under its teeth: mean height (0.725) × 0.5
const vec2 STEEP = vec2(0.45, 0.75); // lift / distance: whole below x, gone above y

varying vec3 blade_color;
varying vec3 blade_normal; // world
varying vec2 card_uv;      // across 0–1, up 0–1
varying float teeth;
varying float card_seed;
varying vec3 dry_color;    // grass teeth pick this or blade_color by dry_share
varying float dry_share;

vec4 hash42(vec2 p) {
	vec4 p4 = fract(vec4(p.xyxy) * vec4(0.1031, 0.1030, 0.0973, 0.1099));
	p4 += dot(p4, p4.wzxy + 33.33);
	return fract((p4.xxyz + p4.yzzw) * p4.zywx);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash42(i).x, hash42(i + vec2(1.0, 0.0)).x, u.x), mix(hash42(i + vec2(0.0, 1.0)).x, hash42(i + vec2(1.0, 1.0)).x, u.x), u.y);
}

float height_linear(vec2 xz) {
	vec2 uv = ((xz + half_size) / resolution + 0.5) / float(samples);
	return height_offset + textureLod(heights, uv, 0.0).r * height_range;
}

void vertex() {
	int blade = int(VERTEX.x + 0.5);
	int corner = int(VERTEX.y + 0.5); // 0, 1 root left and right; 2, 3 top left and right
	ivec2 cell = origin_cell + ivec2(INSTANCE_ID % per_side, INSTANCE_ID / per_side) - ivec2(per_side / 2);
	vec2 key = vec2(cell) + vec2(float(level) * 7919.0, 0.0);
	vec4 r = hash42(key);
	vec2 xz = (vec2(cell) + 0.5 + (r.xy - 0.5) * 0.9) * spacing;
	vec2 across = xz - eye.xz;
	float dist = sqrt(dot(across, across) + lift * lift);
	float fade = smoothstep(band.x, band.y, dist) * (1.0 - smoothstep(band.z, band.w, dist)) * (1.0 - smoothstep(STEEP.x, STEEP.y, lift / dist));
	vec3 pos = vec3(0.0); // collapsed: zero area, not drawn
	blade_color = vec3(0.0);
	blade_normal = vec3(0.0, 1.0, 0.0);
	card_uv = vec2(float(corner & 1), float(corner >> 1));
	teeth = 1.0;
	card_seed = 0.0;
	dry_color = vec3(0.0);
	dry_share = 0.0;
	if (fade > 0.0 && r.z < fade && abs(xz.x) < half_size && abs(xz.y) < half_size) {
		ivec2 sc = clamp(ivec2(floor((xz + half_size) / surface_resolution)), ivec2(0), ivec2(surface_cells - 1));
		int id = int(round(texelFetch(surface_ids, sc, 0).r * 255.0));
		vec4 cov = texelFetch(cover, sc, 0);
		vec4 grass = texelFetch(surface_params, ivec2(id, 5), 0); // silhouette, height min, max, dry fraction
		vec4 green = texelFetch(surface_params, ivec2(id, 6), 0); // colour, straw silhouette
		vec4 dry = texelFetch(surface_params, ivec2(id, 7), 0);   // colour, straw mean length
		vec4 straw = texelFetch(surface_params, ivec2(id, 8), 0); // colour, blade width
		float hole = use_holes ? texelFetch(holes, sc, 0).r : 0.0;
		bool standing = blade < GRASS_CARDS;
		float mean_len = standing ? 0.5 * (grass.y + grass.z) : dry.a;
		float area = (standing ? grass.x * cov.r / float(GRASS_CARDS) : green.a * cov.g / STRAW_CARDS) * spacing * spacing;
		// |[1][1]|: Godot's Vulkan projection carries a Y flip, so the entry is negative there.
		float metres_per_pixel = dist * 2.0 / (abs(PROJECTION_MATRIX[1][1]) * VIEWPORT_SIZE.y);
		float min_width = max(straw.a, MIN_PIXELS * metres_per_pixel);
		float width = mean_len > 0.0 ? area / (TOOTH_FILL * mean_len) : 0.0; // a toothed card of that side-on area
		vec4 rb = hash42(key * 1.37 + float(blade) * 17.13 + 3.7);
		if (width > 0.0 && hole < 0.5 && rb.w * min_width < width) {
			width = max(width, min_width);
			teeth = max(1.0, floor(width / (TOOTH_PIXELS * metres_per_pixel)));
			card_seed = floor(rb.y * 997.0);
			float azimuth = rb.x * TAU;
			vec2 dir = vec2(cos(azimuth), sin(azimuth));
			vec2 root = xz + dir * (rb.y * spacing * 0.35);
			float len, lean;
			vec3 col;
			if (standing) {
				len = mix(grass.y, grass.z, rb.z);
				lean = radians(4.0 + 22.0 * fract(rb.x * 7.3));
				col = green.rgb;
				dry_color = dry.rgb;
				dry_share = clamp(grass.w + 0.5 * (vnoise(xz / 6.0) - 0.5), 0.0, 1.0);
			} else {
				len = dry.a * mix(0.6, 1.3, rb.z);
				lean = radians(62.0 + 22.0 * fract(rb.x * 7.3));
				col = straw.rgb;
			}
			col *= 0.85 + 0.3 * fract(rb.z * 5.1);
			vec3 base = vec3(root.x, height_linear(root) - 0.02, root.y);
			vec3 up = vec3(dir.x * sin(lean), cos(lean), dir.y * sin(lean)) * len;
			vec3 side = vec3(-dir.y, 0.0, dir.x) * width;
			pos = base + side * (card_uv.x - 0.5) + up * card_uv.y;
			float shade = mix(mix(0.72, 0.45, clamp(len / 1.2, 0.0, 1.0)), 1.0, card_uv.y);
			blade_color = col * shade;
			dry_color *= shade;
			blade_normal = normalize(vec3(dir.x * 0.35, 1.0, dir.y * 0.35));
		}
	}
	VERTEX = pos;
	NORMAL = blade_normal;
}

void fragment() {
	// Pointed teeth of random height: tooth i peaks at 0.45–1.0 of the card.
	float x = card_uv.x * teeth;
	float tooth = floor(x);
	float peak = 0.45 + 0.55 * fract(sin((tooth + card_seed) * 12.9898) * 43758.5453);
	if (card_uv.y > peak * (1.0 - abs(2.0 * fract(x) - 1.0))) {
		discard;
	}
	float pick = fract(peak * 37.1);
	ALBEDO = (fract(peak * 71.3) < dry_share ? dry_color : blade_color) * (0.85 + 0.3 * pick);
	ROUGHNESS = 1.0;
	NORMAL = normalize((VIEW_MATRIX * vec4(blade_normal, 0.0)).xyz);
}
