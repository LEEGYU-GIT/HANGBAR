// Round props (soft style): faceted (flat) shading from screen-space derivatives and a wind sway that grows with
// height above the prop's feet. Instance colours are sRGB.
shader_type spatial;
render_mode cull_back, diffuse_lambert_wrap, world_vertex_coords;

uniform float sway = 0.0; // metres of sway at 3 m above the prop's feet

vec3 to_linear(vec3 c) {
	return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
}

// world_vertex_coords: VERTEX is already in world space, so the sway is added directly (no per-vertex inverse)
void vertex() {
	if (sway > 0.0) {
		vec3 origin = MODEL_MATRIX[3].xyz;
		float lift = clamp((VERTEX.y - origin.y + 1.0) / 3.0, 0.0, 2.0);
		float t = TIME * 1.3 + origin.x * 0.37 + origin.z * 0.23;
		VERTEX += vec3(sin(t), 0.0, cos(t * 0.8)) * sway * lift * lift;
	}
}

void fragment() {
	// flat normals from screen-space derivatives give the low-poly facets
	vec3 fn = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
	NORMAL = fn;
	vec3 col = COLOR.rgb;
	ALBEDO = to_linear(col);
	ROUGHNESS = 0.9;
}
