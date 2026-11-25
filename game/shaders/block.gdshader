// Block look (soft style, user decision 2026-11-25): no pixel pattern and no block outlines. Colour drifts slowly
// over about 2 m so ground reads as one surface; tops are lighter and warmer, sides cooler, undersides darker; colour
// is richer than the photo's palette; light wraps softly. Wear (Weathering) shows gently: cracked blocks a little
// darker, mossy blocks greening from the top edge down, worn blocks lighter and flatter. Photo blocks stay exact.
// Inputs: COLOR = block colour x AO (sRGB), UV2 = (material, state), model-space position and normal (chunk meshes
// sit at the origin, so model space is world space). Material and state numbers are Block.cs's.
shader_type spatial;
render_mode cull_back, diffuse_lambert_wrap, specular_schlick_ggx;

const float BLOCK = 0.25;
const int PIXEL = 2; const int SAND = 5; const int SNOW = 9; const int METAL = 14;
const int CRACKED = 1; const int MOSSY = 2; const int WORN = 3;
const vec3 MOSS = vec3(78.0, 122.0, 47.0) / 255.0; // BlockPattern.MossColor 0x4E7A2F

varying vec3 v_pos;
varying vec3 v_nrm;

void vertex() { v_pos = VERTEX; v_nrm = NORMAL; }

float h3(ivec3 b) {
	uint h = uint(b.x) * 73856093u ^ uint(b.y) * 19349663u ^ uint(b.z) * 83492791u;
	h ^= h >> 13u; h *= 1274126177u; h ^= h >> 16u;
	return float(h & 0xFFFFu) / 65535.0;
}

/// smooth value noise over a 3D grid of cell size 1
float smooth_noise(vec3 q) {
	ivec3 c = ivec3(floor(q));
	vec3 t = smoothstep(0.0, 1.0, fract(q));
	float n000 = h3(c), n100 = h3(c + ivec3(1, 0, 0)), n010 = h3(c + ivec3(0, 1, 0)), n110 = h3(c + ivec3(1, 1, 0));
	float n001 = h3(c + ivec3(0, 0, 1)), n101 = h3(c + ivec3(1, 0, 1)), n011 = h3(c + ivec3(0, 1, 1)), n111 = h3(c + ivec3(1, 1, 1));
	float z0 = mix(mix(n000, n100, t.x), mix(n010, n110, t.x), t.y);
	float z1 = mix(mix(n001, n101, t.x), mix(n011, n111, t.x), t.y);
	return mix(z0, z1, t.z);
}

vec3 to_linear(vec3 c) {
	return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
}

void fragment() {
	vec3 n = normalize(v_nrm);
	int m = int(round(UV2.x)), s = int(round(UV2.y));
	vec3 col = COLOR.rgb;
	if (m != PIXEL) {
		col *= 0.93 + 0.14 * smooth_noise(v_pos / 2.0);
		if (n.y > 0.5) col *= vec3(1.07, 1.05, 0.98);
		else if (n.y < -0.5) col *= 0.75;
		else col *= vec3(0.88, 0.9, 0.97);

		// wear, kept soft: where in its own block is this point (for moss growing down from the top)
		ivec3 b = ivec3(floor(v_pos / BLOCK - n * 0.5));
		float fromTop = n.y > 0.5 ? 0.0 : clamp(1.0 - (v_pos.y / BLOCK - float(b.y)), 0.0, 1.0);
		if (s == CRACKED) col *= 0.86 + 0.06 * h3(b);
		else if (s == MOSSY) col = mix(col, MOSS, 0.55 * (1.0 - smoothstep(0.25, 0.7, fromTop)));
		else if (s == WORN) col = mix(col, vec3(dot(col, vec3(0.333))), 0.25) * 1.06;

		float l = dot(col, vec3(0.299, 0.587, 0.114));
		col = mix(vec3(l), col, 1.35);
	}
	ALBEDO = to_linear(clamp(col, 0.0, 1.0));
	ROUGHNESS = m == METAL ? 0.4 : (m == SNOW || m == SAND ? 0.95 : 0.85);
}
