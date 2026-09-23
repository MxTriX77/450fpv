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

varying vec3 world_pos;

float height_at_sample(vec2 xz) {
	ivec2 p = clamp(ivec2(round((xz + half_size) / resolution)), ivec2(0), ivec2(samples - 1));
	return height_offset + texelFetch(heights, p, 0).r * height_range;
}

float height_linear(vec2 xz) {
	vec2 uv = ((xz + half_size) / resolution + 0.5) / float(samples);
	return height_offset + texture(heights, uv).r * height_range;
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
	NORMAL = normalize((VIEW_MATRIX * vec4(-dx, 2.0 * e, -dz, 0.0)).xyz);
	ALBEDO = albedo;
	ROUGHNESS = 0.95;
}
