// SPIKE (style test): soft blocks. No pixel pattern and no block outlines: colour drifts slowly over about 2 m,
// lighter tops, cooler sides, richer colour, wrapped diffuse light. Inputs as block.gdshader.
shader_type spatial;
render_mode cull_back, diffuse_lambert_wrap, specular_schlick_ggx;

const float BLOCK = 0.25;
const int PIXEL = 2; const int WATER = 1; const int GRASS = 4; const int SAND = 5; const int SNOW = 9;
const int GLASS = 15; const int METAL = 14;

varying vec3 v_pos;
varying vec3 v_nrm;

void vertex() { v_pos = VERTEX; v_nrm = NORMAL; }

float h3(ivec3 b) {
	uint h = uint(b.x) * 73856093u ^ uint(b.y) * 19349663u ^ uint(b.z) * 83492791u;
	h ^= h >> 13u; h *= 1274126177u; h ^= h >> 16u;
	return float(h & 0xFFFFu) / 65535.0;
}

vec3 to_linear(vec3 c) {
	return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
}

void fragment() {
	vec3 n = normalize(v_nrm);
	vec3 g = v_pos / BLOCK - n * 0.5;
	ivec3 b = ivec3(floor(g));
	vec3 f = v_pos / BLOCK - vec3(b);
	vec2 uv = abs(n.x) > 0.5 ? f.zy : (abs(n.y) > 0.5 ? f.xz : f.xy);
	int m = int(round(UV2.x));

	vec3 col = COLOR.rgb;
	if (m != PIXEL) {
		// no per-block pattern: colour drifts slowly over the ground (about 2 m), so terrain reads as one surface
		vec3 q = v_pos / 2.0;
		ivec3 c = ivec3(floor(q));
		vec3 t = smoothstep(0.0, 1.0, fract(q));
		float n000 = h3(c), n100 = h3(c + ivec3(1,0,0)), n010 = h3(c + ivec3(0,1,0)), n110 = h3(c + ivec3(1,1,0));
		float n001 = h3(c + ivec3(0,0,1)), n101 = h3(c + ivec3(1,0,1)), n011 = h3(c + ivec3(0,1,1)), n111 = h3(c + ivec3(1,1,1));
		float nz0 = mix(mix(n000, n100, t.x), mix(n010, n110, t.x), t.y);
		float nz1 = mix(mix(n001, n101, t.x), mix(n011, n111, t.x), t.y);
		float drift = mix(nz0, nz1, t.z);
		col *= 0.93 + 0.14 * drift;
		// lighter, warmer tops; cooler, darker sides; dark undersides
		if (n.y > 0.5) col *= vec3(1.07, 1.05, 0.98);
		else if (n.y < -0.5) col *= 0.75;
		else col *= vec3(0.88, 0.9, 0.97);
		// richer colour than the photo's washed palette
		float l = dot(col, vec3(0.299, 0.587, 0.114));
		col = mix(vec3(l), col, 1.25);
	}
	ALBEDO = to_linear(clamp(col, 0.0, 1.0));
	ROUGHNESS = m == METAL ? 0.4 : (m == SNOW || m == SAND ? 0.95 : 0.85);
}
