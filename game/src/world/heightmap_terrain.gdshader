shader_type spatial;
// Skirts are seen from either side, so both faces draw. The normal comes from the heights, not the winding.
render_mode cull_disabled;

// HeightmapTerrain patch shader: vertices sit exactly on height samples, normals come per pixel from the heights,
// so lighting does not pop when a patch changes spacing.
uniform sampler2D heights : filter_linear, repeat_disable; // R16: sample / 65535
uniform float height_offset;  // metres at sample 0
uniform float height_range;   // metres at sample 65535 above height_offset (scale_m * 65535)
uniform float resolution;     // metres between samples
uniform float half_size;      // half the map side, metres
uniform int samples;          // samples per side
uniform float skirt_bottom;   // world Y the skirt vertices drop to
uniform vec3 albedo : source_color = vec3(0.44, 0.42, 0.28);

// Surface materials (HeightmapTerrain.SetSurfaces, SurfaceLook). Rendering only: nothing here feeds the world query.
// - A pixel blends the surfaces of the 4 nearest cell centres, looked up at a position that wanders up to
//   BORDER_JITTER cells, so borders are not the 0.5 m grid. Equal surfaces are merged, then the weights are sharpened
//   by each set's height map (height blending), so one surface gives way to the other around its clods and tufts.
// - Each surface is a CC0 set (array layer) tinted to its photo albedo, tiled in world space, with low-frequency
//   macro variation and optional patches of another tint. Albedo, AO and normal are hex-tiled (Mikkelsen 2022): three
//   samples at random offsets, one per corner of a world-space hex-grid triangle, with sharpened weights, so no tile
//   repeat shows as a grid at any distance.
// - Straw and leaf overlays come from cover.png (G straw, A litter) times the surface's overlay amount.
// - Ridged surfaces (tilled) get furrows from the physics ridge: its direction, spacing and phase, so crests are where
//   the legs find them. They fade out before a pixel spans 0.4 of a furrow, so they never alias into bands.
uniform bool use_surfaces = false;
uniform sampler2D surface_ids : filter_nearest, repeat_disable; // R8: surface index / 255, one texel per cell
uniform sampler2D cover : filter_linear_mipmap, repeat_disable;  // RGBA cover multipliers, one texel per cell
uniform sampler2D surface_params : filter_nearest, repeat_disable; // SurfaceLook.Params: column = surface index
uniform float surface_resolution = 0.5; // metres per surface cell
uniform int surface_cells = 1;          // cells per side
uniform sampler2DArray set_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray set_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // RG, OpenGL
uniform sampler2DArray set_height : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray set_ao : filter_linear_mipmap_anisotropic, repeat_enable;
uniform float straw_layer;
uniform float straw_tile = 2.5;
uniform vec3 straw_tint = vec3(1.0);
uniform float litter_layer;
uniform float litter_tile = 2.0;
uniform vec3 litter_tint = vec3(1.0);

const float BORDER_JITTER = 0.6; // cells
const float BLEND_DEPTH = 0.2;   // height-blend softness, in height-map units
const float MAX_SLOPE_XY = 0.9;  // longest tangent-space normal xy
const float HEX_CELL = 2.2;      // m, hex-tiling cell
const float HEX_SHARPNESS = 5.0; // weight exponent: higher shows less blending between the three samples
const float HEX_MIN_WEIGHT = 0.03; // lighter samples are skipped (and the rest renormalised)

varying vec3 world_pos;

float height_at_sample(vec2 xz) {
	ivec2 p = clamp(ivec2(round((xz + half_size) / resolution)), ivec2(0), ivec2(samples - 1));
	return height_offset + texelFetch(heights, p, 0).r * height_range;
}

float height_linear(vec2 xz) {
	vec2 uv = ((xz + half_size) / resolution + 0.5) / float(samples);
	return height_offset + texture(heights, uv).r * height_range;
}

int surface_at(ivec2 cell) {
	return int(round(texelFetch(surface_ids, clamp(cell, ivec2(0), ivec2(surface_cells - 1)), 0).r * 255.0));
}

vec4 param(int id, int row) {
	return texelFetch(surface_params, ivec2(id, row), 0);
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// Value noise in 0–1 with one lattice node per unit.
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

// Hex tiling: the corners of the triangle of a skewed grid (hex cells of HEX_CELL m) around `xz`, their weights, and a
// random texture offset per corner.
void hex_grid(vec2 xz, out vec3 w, out vec2 o1, out vec2 o2, out vec2 o3) {
	vec2 st = xz / HEX_CELL * 2.0 * sqrt(3.0);
	vec2 skewed = mat2(vec2(1.0, 0.0), vec2(-0.57735027, 1.15470054)) * st;
	vec2 base = floor(skewed);
	vec3 t = vec3(fract(skewed), 0.0);
	t.z = 1.0 - t.x - t.y;
	float s = step(0.0, -t.z);
	float s2 = 2.0 * s - 1.0;
	w = vec3(-t.z * s2, s - t.y * s2, s - t.x * s2);
	o1 = hash22(base + vec2(s, s));
	o2 = hash22(base + vec2(s, 1.0 - s));
	o3 = hash22(base + vec2(1.0 - s, s));
	w = pow(max(w, vec3(0.0)), vec3(HEX_SHARPNESS));
	w /= dot(w, vec3(1.0));
	w *= step(HEX_MIN_WEIGHT, w);
	w /= dot(w, vec3(1.0));
}

// Three octaves of value noise, each turned 37° from the last so no lattice axis shows; about 0–1.
float fbm(vec2 p) {
	mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	float a = 0.5 * vnoise(p);
	p = turn * p * 2.03 + 11.3;
	a += 0.3 * vnoise(p);
	p = turn * p * 2.01 + 5.7;
	return a + 0.2 * vnoise(p);
}

void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX.x, 0.0, VERTEX.z, 1.0)).xyz;
	VERTEX.y = VERTEX.y < -0.5 ? skirt_bottom : height_at_sample(w.xz);
	world_pos = vec3(w.x, VERTEX.y, w.z);
}

void fragment() {
	float e = resolution;
	float dx = height_linear(world_pos.xz + vec2(e, 0.0)) - height_linear(world_pos.xz - vec2(e, 0.0));
	float dz = height_linear(world_pos.xz + vec2(0.0, e)) - height_linear(world_pos.xz - vec2(0.0, e));
	vec3 n = normalize(vec3(-dx, 2.0 * e, -dz));
	NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
	ALBEDO = albedo;
	ROUGHNESS = 0.95;
	if (use_surfaces) {
		vec2 xz = world_pos.xz;
		// Derivatives once, in uniform control flow: every sample below is a textureGrad inside branches.
		vec2 gx = dFdx(xz);
		vec2 gy = dFdy(xz);

		vec2 jitter = vec2(vnoise(xz * 0.8), vnoise(xz * 0.8 + 31.7)) - 0.5;
		vec2 s = (xz + half_size) / surface_resolution - 0.5 + jitter * (2.0 * BORDER_JITTER);
		vec2 f = s - floor(s);
		ivec2 c = ivec2(floor(s));
		vec4 w = vec4((1.0 - f.x) * (1.0 - f.y), f.x * (1.0 - f.y), (1.0 - f.x) * f.y, f.x * f.y);
		ivec4 id = ivec4(surface_at(c), surface_at(c + ivec2(1, 0)), surface_at(c + ivec2(0, 1)), surface_at(c + ivec2(1, 1)));
		for (int k = 1; k < 4; k++) {
			for (int j = 0; j < k; j++) {
				if (id[j] == id[k]) {
					w[j] += w[k];
					w[k] = 0.0;
				}
			}
		}

		// Height blending over the distinct surfaces; inside a surface (one weight left) there is nothing to blend.
		vec4 b = step(0.999, w);
		if (dot(b, vec4(1.0)) < 0.5) {
			vec4 score = vec4(-1.0);
			for (int k = 0; k < 4; k++) {
				if (w[k] > 0.0) {
					float tile = param(id[k], 1).x;
					float h = textureGrad(set_height, vec3(xz / tile, param(id[k], 0).a), gx / tile, gy / tile).r;
					score[k] = h + w[k];
				}
			}
			float top = max(max(score.x, score.y), max(score.z, score.w));
			b = max(score - top + BLEND_DEPTH, vec4(0.0));
			b /= dot(b, vec4(1.0));
		}

		vec3 hw;
		vec2 ho[3];
		hex_grid(xz, hw, ho[0], ho[1], ho[2]);
		vec3 alb = vec3(0.0);
		vec2 nxy = vec2(0.0);
		float ao = 0.0, rough = 0.0, straw_amount = 0.0, litter_amount = 0.0, macro_amount = 0.0, patch_amount = 0.0, furrow = 0.0;
		vec3 patch_tint = vec3(0.0);
		for (int k = 0; k < 4; k++) {
			if (b[k] > 0.0) {
				vec4 p0 = param(id[k], 0), p1 = param(id[k], 1), p2 = param(id[k], 2), p3 = param(id[k], 3);
				vec3 uvl = vec3(xz / p1.x, p0.a);
				vec2 ddx = gx / p1.x, ddy = gy / p1.x;
				for (int h = 0; h < 3; h++) {
					if (hw[h] > 0.0) {
						vec3 uvh = vec3(uvl.xy + ho[h], uvl.z);
						float wh = b[k] * hw[h];
						alb += wh * textureGrad(set_albedo, uvh, ddx, ddy).rgb * p0.rgb;
						ao += wh * textureGrad(set_ao, uvh, ddx, ddy).r;
						nxy += wh * (textureGrad(set_normal, uvh, ddx, ddy).rg * 2.0 - 1.0);
					}
				}
				rough += b[k] * p1.w;
				straw_amount += b[k] * p1.y;
				litter_amount += b[k] * p1.z;
				macro_amount += b[k] * p3.x;
				patch_amount += b[k] * p3.y;
				patch_tint += b[k] * param(id[k], 4).rgb;
				if (p2.z > 0.0) {
					// The physics ridge profile 1 − 2·smoothstep(2q), q = distance to the crest in spacings.
					float phase = dot(xz, p2.xy) / p2.z + p2.w;
					float cycles_per_pixel = (abs(dot(gx, p2.xy)) + abs(dot(gy, p2.xy))) / p2.z;
					float fade = 1.0 - smoothstep(0.18, 0.4, cycles_per_pixel);
					float fr = fract(phase);
					float t = 2.0 * min(fr, 1.0 - fr);
					float profile = 1.0 - 2.0 * t * t * (3.0 - 2.0 * t);
					float slope = p3.z * -24.0 * t * (1.0 - t) * (fr <= 0.5 ? 1.0 : -1.0) / p2.z; // m per m across
					vec2 grad = slope * p2.xy * fade * b[k];
					nxy += vec2(-grad.x, grad.y); // tangent x is east, binormal y is north (−z)
					furrow += b[k] * profile * fade;
				}
			}
		}
		// Furrow crests are dry and pale, troughs moist, dark and occluded.
		alb *= 1.0 + 0.22 * furrow;
		ao *= 1.0 - 0.25 * max(-furrow, 0.0);

		float macro = (0.6 * fbm(xz / 6.0) + 0.4 * vnoise(xz / 23.0 + 7.1) - 0.5) * 2.0;
		alb *= 1.0 + macro_amount * macro;
		if (patch_amount > 0.0) {
			float patch = smoothstep(0.56, 0.68, fbm(xz / 4.0 + 13.7)) * patch_amount;
			alb *= mix(vec3(1.0), patch_tint, patch);
		}

		float size = float(surface_cells) * surface_resolution;
		vec4 cov = textureGrad(cover, (xz + half_size) / size, gx / size, gy / size);
		float straw_cover = cov.g * straw_amount;
		if (straw_cover > 0.005) {
			straw_cover *= 0.5 + fbm(xz / 1.5 + 3.3);
			vec3 uvl = vec3(xz / straw_tile, straw_layer);
			vec2 ddx = gx / straw_tile, ddy = gy / straw_tile;
			float mask = smoothstep(0.0, 0.25, textureGrad(set_height, uvl, ddx, ddy).r + 1.15 * straw_cover - 1.0);
			alb = mix(alb, textureGrad(set_albedo, uvl, ddx, ddy).rgb * straw_tint, mask);
			nxy = mix(nxy, textureGrad(set_normal, uvl, ddx, ddy).rg * 2.0 - 1.0, mask);
			ao = mix(ao, textureGrad(set_ao, uvl, ddx, ddy).r, mask);
			rough = mix(rough, 0.9, mask);
		}
		float litter_cover = cov.a * litter_amount;
		if (litter_cover > 0.005) {
			litter_cover *= 0.5 + fbm(xz / 1.3 + 9.1);
			vec3 uvl = vec3(xz / litter_tile, litter_layer);
			vec2 ddx = gx / litter_tile, ddy = gy / litter_tile;
			float mask = smoothstep(0.0, 0.25, textureGrad(set_height, uvl, ddx, ddy).r + 1.15 * litter_cover - 1.0);
			alb = mix(alb, textureGrad(set_albedo, uvl, ddx, ddy).rgb * litter_tint, mask);
			nxy = mix(nxy, textureGrad(set_normal, uvl, ddx, ddy).rg * 2.0 - 1.0, mask);
			ao = mix(ao, textureGrad(set_ao, uvl, ddx, ddy).r, mask);
			rough = mix(rough, 0.85, mask);
		}

		ALBEDO = alb * ao;
		ROUGHNESS = rough;
		float len = length(nxy);
		nxy *= len > MAX_SLOPE_XY ? MAX_SLOPE_XY / len : 1.0;
		// Texture u runs east and v south, so the normal map's tangent is the slope along +x and its binormal (green,
		// OpenGL convention) points north.
		vec3 t = normalize(vec3(2.0 * e, dx, 0.0));
		TANGENT = normalize((VIEW_MATRIX * vec4(t, 0.0)).xyz);
		BINORMAL = normalize((VIEW_MATRIX * vec4(cross(n, t), 0.0)).xyz);
		NORMAL_MAP = vec3(nxy * 0.5 + 0.5, 1.0);
	}
}
