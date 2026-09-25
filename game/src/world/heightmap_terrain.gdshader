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

// Surface materials (HeightmapTerrain.SetSurfaces). Each surface index is a layer of the three arrays, tiled in world
// space; a pixel blends the layers of the 4 nearest surface-cell centres bilinearly, as the world query blends relief.
uniform bool use_surfaces = false;
uniform sampler2D surface_ids : filter_nearest, repeat_disable; // R8: surface index / 255, one texel per cell
uniform float surface_resolution = 0.5; // metres per surface cell
uniform int surface_cells = 1;          // cells per side
uniform sampler2DArray surface_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray surface_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray surface_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float texture_tile = 2.0;       // metres per texture repeat

varying vec3 world_pos;

float height_at_sample(vec2 xz) {
	ivec2 p = clamp(ivec2(round((xz + half_size) / resolution)), ivec2(0), ivec2(samples - 1));
	return height_offset + texelFetch(heights, p, 0).r * height_range;
}

float height_linear(vec2 xz) {
	vec2 uv = ((xz + half_size) / resolution + 0.5) / float(samples);
	return height_offset + texture(heights, uv).r * height_range;
}

float surface_at(ivec2 cell) {
	return round(texelFetch(surface_ids, clamp(cell, ivec2(0), ivec2(surface_cells - 1)), 0).r * 255.0);
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
		vec2 s = (world_pos.xz + half_size) / surface_resolution - 0.5;
		vec2 f = s - floor(s);
		ivec2 c = ivec2(floor(s));
		vec4 w = vec4((1.0 - f.x) * (1.0 - f.y), f.x * (1.0 - f.y), (1.0 - f.x) * f.y, f.x * f.y);
		vec4 id = vec4(surface_at(c), surface_at(c + ivec2(1, 0)), surface_at(c + ivec2(0, 1)), surface_at(c + ivec2(1, 1)));
		vec2 uv = world_pos.xz / texture_tile;
		vec3 a = vec3(0.0), m = vec3(0.0);
		float r = 0.0;
		for (int k = 0; k < 4; k++) {
			a += w[k] * texture(surface_albedo, vec3(uv, id[k])).rgb;
			m += w[k] * texture(surface_normal, vec3(uv, id[k])).rgb;
			r += w[k] * texture(surface_roughness, vec3(uv, id[k])).r;
		}
		ALBEDO = a;
		ROUGHNESS = r;
		// Texture u runs east and v south, so the normal map's tangent is the slope along +x and its binormal (green,
		// OpenGL convention) points north.
		vec3 t = normalize(vec3(2.0 * e, dx, 0.0));
		TANGENT = normalize((VIEW_MATRIX * vec4(t, 0.0)).xyz);
		BINORMAL = normalize((VIEW_MATRIX * vec4(cross(n, t), 0.0)).xyz);
		NORMAL_MAP = m;
	}
}
