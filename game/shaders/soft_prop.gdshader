// SPIKE (style test): round props with faceted (flat) shading and a wind sway that grows with height.
shader_type spatial;
render_mode cull_back, diffuse_lambert_wrap;

uniform float sway = 0.0; // metres of sway at 3 m above the prop's feet

varying vec3 v_world;

vec3 to_linear(vec3 c) {
	return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
}

void vertex() {
	vec3 origin = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float lift = clamp((w.y - origin.y + 1.0) / 3.0, 0.0, 2.0);
	float t = TIME * 1.3 + origin.x * 0.37 + origin.z * 0.23;
	vec3 push = vec3(sin(t), 0.0, cos(t * 0.8)) * sway * lift * lift;
	VERTEX += (inverse(MODEL_MATRIX) * vec4(push, 0.0)).xyz;
	v_world = w;
}

void fragment() {
	// flat normals from screen-space derivatives give the low-poly facets
	vec3 fn = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
	NORMAL = fn;
	vec3 col = mix(COLOR.rgb, vec3(1.0), 0.05);
	ALBEDO = to_linear(col);
	ROUGHNESS = 0.9;
}
