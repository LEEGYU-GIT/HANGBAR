// Block texture (v2 design): an 8x8 pattern per block face, shaded around the block's own colour.
// A line-by-line port of client/core/World/BlockPattern.cs (Hash, Rand, Sample and its helpers);
// change both together. Inputs: COLOR = block colour x AO (sRGB), UV2 = (material, state), model-space position and
// normal (chunk meshes sit at the origin, so model space is world space).
// Face numbers follow ChunkMesher.Faces: 0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z. Inside a face, top/bottom faces use
// (x, z); side faces run px along the face and py from the top edge down (moss grows from the top).
shader_type spatial;
render_mode cull_back;

const float BLOCK = 0.25;
const vec3 MOSS = vec3(78.0, 122.0, 47.0) / 255.0; // BlockPattern.MossColor 0x4E7A2F

// Material and BlockState numbers (Block.cs)
const int AIR = 0; const int WATER = 1; const int PIXEL = 2; const int SOIL = 3; const int GRASS = 4; const int SAND = 5;
const int STONE = 6; const int DEEPSTONE = 7; const int BEDROCK = 8; const int SNOW = 9; const int WOOD = 10; const int BRICK = 11;
const int CONCRETE = 12; const int TILE = 13; const int METAL = 14; const int GLASS = 15; const int DARKGLASS = 16; const int CLOTH = 17;
const int CRACKED = 1; const int MOSSY = 2; const int WORN = 3;

varying vec3 v_pos;
varying vec3 v_nrm;

void vertex() {
	v_pos = VERTEX;
	v_nrm = NORMAL;
}

uint block_hash(int bx, int by, int bz, int face) {
	uint h = uint(bx) * 73856093u ^ uint(by) * 19349663u ^ uint(bz) * 83492791u ^ uint(face) * 2654435761u;
	h ^= h >> 15u; h *= 2246822519u; h ^= h >> 13u;
	return h;
}

float rnd(uint hash, int a, int b, uint salt) {
	uint h = hash ^ uint(a) * 374761393u ^ uint(b) * 668265263u ^ salt * 2246822519u;
	h ^= h >> 13u; h *= 1274126177u; h ^= h >> 16u;
	return float(h & 0xFFFFu) / 65535.0;
}

float signed_rnd(uint hash, int a, int b, uint salt) { return rnd(hash, a, b, salt) * 2.0 - 1.0; }

float blotch(int px, int py, uint hash, uint salt, float amp, float dark) {
	int cx = px / 2, cy = py / 2;
	float v = 1.0 + amp * signed_rnd(hash, cx, cy, salt);
	return rnd(hash, cx, cy, salt + 7u) > 0.85 ? v * dark : v;
}

float base_shade(int m, int px, int py, uint hash, uint salt) {
	if (m == STONE) return blotch(px, py, hash, salt, 0.12, 0.88);
	if (m == DEEPSTONE) return blotch(px, py, hash, salt, 0.14, 0.85);
	if (m == BEDROCK) return blotch(px, py, hash, salt, 0.2, 0.9);
	if (m == CONCRETE) return blotch(px, py, hash, salt, 0.05, 0.95);
	if (m == SOIL) return rnd(hash, px, py, salt + 1u) > 0.86 ? 0.8 : 1.0 + 0.14 * signed_rnd(hash, px, py, salt);
	if (m == GRASS) {
		float blade = rnd(hash, px, 0, salt + 2u) > 0.6 && py < 3 ? 0.08 : 0.0;
		return 1.0 + 0.1 * signed_rnd(hash, px, py, salt) + blade;
	}
	if (m == SAND) return 1.0 + 0.05 * signed_rnd(hash, px, py, salt);
	if (m == SNOW) return rnd(hash, px, py, salt + 3u) > 0.94 ? 1.12 : 1.0 + 0.03 * signed_rnd(hash, px, py, salt);
	if (m == WOOD) {
		float row = 1.0 + 0.08 * signed_rnd(hash, py, 0, salt);
		int offset = int(rnd(hash, 0, 0, salt + 4u) * 4.0);
		if ((py + offset) % 4 == 0) row *= 0.9;
		return row + 0.03 * signed_rnd(hash, px, py, salt + 5u);
	}
	if (m == BRICK) {
		int course = py / 4;
		bool mortar = py % 4 == 3 || px == (course % 2 == 0 ? 0 : 4);
		if (mortar) return 0.76;
		int brick = (px + (course % 2 == 0 ? 0 : 4)) / 4;
		return 1.06 + 0.06 * signed_rnd(hash, course, brick, salt);
	}
	if (m == TILE) return px % 4 == 0 || py % 4 == 0 ? 0.84 : 1.06 + 0.03 * signed_rnd(hash, px / 4, py / 4, salt);
	if (m == METAL) {
		bool rivet = (px == 1 || px == 6) && (py == 1 || py == 6);
		return rivet ? 0.78 : 1.0 + 0.05 * signed_rnd(hash, py, 0, salt) + 0.02 * signed_rnd(hash, px, py, salt + 6u);
	}
	if (m == GLASS) {
		bool frame = px == 0 || py == 0 || px == 7 || py == 7;
		return frame ? 0.82 : (px == py ? 1.12 : 1.0);
	}
	if (m == DARKGLASS) {
		bool frame = px == 0 || py == 0 || px == 7 || py == 7;
		return frame ? 0.85 : (px + py == 7 ? 1.1 : 1.0);
	}
	if (m == CLOTH) return ((px + py) % 2 == 0 ? 0.95 : 1.05) + 0.02 * signed_rnd(hash, px, py, salt);
	return 1.0;
}

bool on_crack(int px, int py, uint hash) {
	int x = 1 + int(rnd(hash, 11, 0, 99u) * 6.0);
	for (int r = 1; r <= py; r++) {
		float d = rnd(hash, 11, r, 98u);
		x += d < 0.33 ? -1 : (d > 0.66 ? 1 : 0);
		x = clamp(x, 0, 7);
	}
	return px == x;
}

float moss_at(int px, int py, uint hash) {
	if (py == 0) return 0.7;
	if (py == 1) return 0.45;
	int drip = 1 + int(rnd(hash, px, 21, 97u) * 3.0);
	return rnd(hash, px, 22, 96u) > 0.65 && py <= drip ? 0.3 : 0.0;
}

// returns (shade, moss)
vec2 sample_texel(int m, int s, int px, int py, uint hash) {
	if (m == PIXEL || m == AIR || m == WATER) return vec2(1.0, 0.0);
	uint salt = uint(m) * 0x9E3779B9u;
	float shade = base_shade(m, px, py, hash, salt);
	float moss = 0.0;
	if (s == CRACKED && on_crack(px, py, hash)) shade *= 0.62;
	else if (s == MOSSY) moss = moss_at(px, py, hash);
	else if (s == WORN) shade = 1.0 + (shade - 1.0) * 0.4 + 0.02;
	return vec2(shade, moss);
}

vec3 to_linear(vec3 c) {
	return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
}

void fragment() {
	vec3 n = normalize(v_nrm);
	vec3 a = abs(n);
	int face;
	if (a.x >= a.y && a.x >= a.z) face = n.x > 0.0 ? 0 : 1;
	else if (a.y >= a.z) face = n.y > 0.0 ? 2 : 3;
	else face = n.z > 0.0 ? 4 : 5;

	vec3 g = v_pos / BLOCK - n * 0.5;            // a point inside the block this face belongs to
	ivec3 b = ivec3(floor(g));
	vec3 f = clamp(v_pos / BLOCK - vec3(b), 0.0, 0.9999);
	vec2 uv = face <= 1 ? vec2(f.z, 1.0 - f.y) : (face <= 3 ? f.xz : vec2(f.x, 1.0 - f.y));
	int px = clamp(int(uv.x * 8.0), 0, 7), py = clamp(int(uv.y * 8.0), 0, 7);

	int m = int(round(UV2.x)), s = int(round(UV2.y));
	vec2 t = sample_texel(m, s, px, py, block_hash(b.x, b.y, b.z, face));
	ALBEDO = to_linear(mix(COLOR.rgb * t.x, MOSS, t.y));
	ROUGHNESS = m == METAL ? 0.45 : 0.9;
}
