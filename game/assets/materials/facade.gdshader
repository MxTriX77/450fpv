// The wall of a village building: lime whitewash over a clay or lime render over the wall's core (adobe or brick),
// with the damage, soot and dirt of its facade map (tools/blender/buildingkit.py, Facade). Bound by name through a
// ShaderMaterial .tres per building, which supplies the map and the layer colours.
//
// UV is asset space in metres, upright on every face (assetkit, upright=True), so the layer textures run on unbroken
// across all the boxes a wall is drawn with. UV2 is the face's place on the facade map. The map's R is the damage
// depth: whitewash below wash_cut, the render between the cuts, the core above core_cut. A little noise at the scale
// of centimetres is added to it here, so the edges where plaster broke away are ragged, not the map's smooth contours.
shader_type spatial;

uniform sampler2D facade_map : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D plaster_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D plaster_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D core_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D core_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float plaster_tile_m = 2.5;
uniform float core_tile_m = 2.0;
// The linear mean of each albedo texture, so a layer's colour below is the colour it averages to.
uniform vec3 plaster_mean = vec3(0.3247, 0.2142, 0.1035);
uniform vec3 core_mean = vec3(0.1253, 0.0853, 0.0487);

uniform vec3 wash_color : source_color = vec3(0.78, 0.76, 0.70);
uniform float wash_texture = 0.35;      // how much of the plaster texture's light and shade shows through the wash
uniform vec3 render_color : source_color = vec3(0.58, 0.45, 0.31);
uniform vec3 core_color : source_color = vec3(0.50, 0.39, 0.28);
uniform vec3 band_color : source_color = vec3(0.47, 0.35, 0.28);
uniform vec3 soot_color : source_color = vec3(0.07, 0.065, 0.06);
uniform vec3 dirt_color : source_color = vec3(0.42, 0.38, 0.31);
uniform float wash_cut = 0.40;          // buildingkit.WASH_CUT
uniform float core_cut = 0.64;          // buildingkit.CORE_CUT
uniform float ragged = 0.07;            // the edge noise added to the damage depth
// Hand-made blocks of the core, seen where it is bare (a saman wall's blocks and clay joints); 0 for none.
uniform float block_joints = 1.0;
uniform vec2 block_m = vec2(0.40, 0.16);

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0)), f.x), f.y);
}

void fragment() {
	vec4 map = texture(facade_map, UV2);
	float fine = 0.6 * value_noise(UV * 14.0) + 0.4 * value_noise(UV * 37.0);
	float depth = map.r + (fine - 0.5) * 2.0 * ragged;
	float aa = fwidth(depth) + 0.002;
	float wash = 1.0 - smoothstep(wash_cut - aa, wash_cut + aa, depth);
	float core = smoothstep(core_cut - aa, core_cut + aa, depth);
	float render = max(1.0 - wash - core, 0.0);

	vec2 plaster_uv = UV / plaster_tile_m, core_uv = UV / core_tile_m;
	vec3 plaster = texture(plaster_albedo, plaster_uv).rgb;
	float shade = dot(plaster, vec3(0.2126, 0.7152, 0.0722)) / dot(plaster_mean, vec3(0.2126, 0.7152, 0.0722));
	vec3 wash_rgb = mix(wash_color, band_color, map.a) * mix(1.0, shade, wash_texture);
	vec3 render_rgb = render_color * plaster / plaster_mean;
	vec3 core_rgb = core_color * texture(core_albedo, core_uv).rgb / core_mean;

	// The core's blocks: running bond, joints about a centimetre wide, a little uneven.
	vec2 cell = UV / block_m;
	cell.x += 0.5 * floor(cell.y);
	vec2 f = fract(cell);
	float joint_m = min(min(f.x, 1.0 - f.x) * block_m.x, min(f.y, 1.0 - f.y) * block_m.y) + (fine - 0.5) * 0.008;
	core_rgb *= 1.0 - 0.4 * block_joints * (1.0 - smoothstep(0.003, 0.010, joint_m));

	// Where the wash broke off it leaves a lip: a thin shadowed line on the render side of each break, and a darker
	// one where the render broke to the core.
	float lip = (1.0 - smoothstep(0.0, 0.035, depth - wash_cut)) * step(wash_cut, depth) * (1.0 - core);
	float lip_core = (1.0 - smoothstep(0.0, 0.03, depth - core_cut)) * step(core_cut, depth);
	vec3 rgb = wash_rgb * wash + render_rgb * render + core_rgb * core;
	rgb *= 1.0 - 0.3 * lip - 0.35 * lip_core;

	// Dirt shows most on the whitewash; soot covers everything.
	rgb = mix(rgb, dirt_color * mix(1.0, shade, 0.5), map.b * (0.3 + 0.4 * wash));
	rgb = mix(rgb, soot_color, clamp(map.g, 0.0, 1.0) * 0.93);

	ALBEDO = rgb;
	ROUGHNESS = mix(0.9, 0.97, core);
	NORMAL_MAP = mix(texture(plaster_normal, plaster_uv).rgb, texture(core_normal, core_uv).rgb, core);
	NORMAL_MAP_DEPTH = mix(0.6, 1.3, core);
	AO = 1.0 - 0.25 * core - 0.2 * lip_core;
	AO_LIGHT_AFFECT = 0.3;
}
