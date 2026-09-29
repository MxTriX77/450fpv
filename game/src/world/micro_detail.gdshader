shader_type spatial;
render_mode cull_disabled;

// Near micro-detail (MicroDetailView): the physics' own stems, straws, twigs and leaves, one MultiMesh instance each.
// Over the last `fade` m of the ring every element is kept or dropped whole by its own hash against the fade (a dither
// per element), so the ring thins out with no edge while the far cover (far_cover.gdshader) thickens over the same
// metres. Only drawing changes: the instances, which physics parity checks, are untouched. Elements are lit with a
// normal leaning to the sky, as the far cover's blades are, so near and far cover shade alike and the ring's edge does
// not show as a change of light.
uniform vec3 eye;             // camera, world
uniform float lift;           // camera height above the terrain, m: the vertical term of the ring's distance
uniform float radius = 8.0;   // m, MicroDetailView.Radius
uniform float fade = 2.0;     // m

varying vec3 color;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void vertex() {
	// COLOR.a is 1 for standing grass, which darkens toward its root (VERTEX.y runs 0 to 1 from root to tip).
	color = COLOR.rgb * mix(1.0, mix(0.55, 1.05, VERTEX.y), COLOR.a);
	vec3 base = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec2 across = base.xz - eye.xz;
	float keep = clamp((radius - sqrt(dot(across, across) + lift * lift)) / fade, 0.0, 1.0);
	if (hash12(base.xz * 97.13) >= keep) {
		VERTEX = vec3(0.0); // every corner on the base: zero area, not drawn
	}
}

void fragment() {
	ALBEDO = color;
	ROUGHNESS = 1.0;
	NORMAL = normalize(mix(NORMAL, (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz, 0.75));
}
