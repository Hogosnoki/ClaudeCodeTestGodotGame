// The meadow's ground at dusk: matted turf in browns and dull greens (the blades themselves stand up out of it as their own
// meshes near the camp), worn to bare earth round the fire and along the trodden way to the cave, gravelly where the rock is
// near. Photographed ground for the earth; no toon steps.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D dirt_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dirt_nor : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 turf_a : source_color = vec3(0.20, 0.20, 0.10);
uniform vec3 turf_b : source_color = vec3(0.30, 0.25, 0.13);
uniform vec3 turf_dry : source_color = vec3(0.40, 0.33, 0.20);
uniform vec3 earth_tint : source_color = vec3(0.80, 0.66, 0.58);
// the clearing round the fire (its centre and radius) and the way to the cave (from the fire to here, and its width)
uniform vec2 clearing = vec2(0.0, 0.0);
uniform float clearing_r = 3.6;
uniform vec2 path_to = vec2(12.0, -14.0);
uniform float path_w = 1.5;
// the trodden patch out in front of the fire
uniform vec2 patch_to = vec2(-1.3, 6.4);

varying vec3 v_wp;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 x) {
	vec2 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) { float s = 0.0, a = 0.5; for (int k = 0; k < 5; k++) { s += a * vnoise(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }

void vertex() { v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	vec2 xz = v_wp.xz;
	// turf: patches of greener and browner, dry straw here and there, a fine grain of blades
	float big = fbm(xz * 0.06), mid = fbm(xz * 0.35 + 3.0), fine = vnoise(xz * 9.0) * 0.6 + vnoise(xz * 23.0) * 0.4;
	vec3 turf = mix(turf_a, turf_b, smoothstep(0.35, 0.7, big));
	turf = mix(turf, turf_dry, smoothstep(0.55, 0.8, mid) * 0.6);
	turf *= 0.72 + 0.5 * fine;
	// bare earth: the clearing (ragged at its edge) and the path
	float edge = (fbm(xz * 0.9) - 0.5) * 1.4;
	float clear = 1.0 - smoothstep(clearing_r - 0.6, clearing_r + 0.8, length(xz - clearing) + edge);
	float path = 1.0 - smoothstep(path_w * 0.5, path_w * 1.4, seg_dist(xz, clearing, path_to) + edge * 0.6);
	float tp = clamp(dot(xz - clearing, patch_to - clearing) / dot(patch_to - clearing, patch_to - clearing), 0.0, 1.0);
	float patch_ = 1.0 - smoothstep(1.2, 2.4, seg_dist(xz, clearing, patch_to) / (1.0 - 0.45 * tp) + edge * 0.7);
	float bare = max(max(clear, path * 0.85), patch_ * 0.9);
	vec3 earth = texture(dirt_tex, xz * 0.35).rgb * earth_tint;
	earth *= 0.8 + 0.35 * fbm(xz * 1.7);
	vec3 c = mix(turf, earth, bare);
	ALBEDO = c;
	NORMAL_MAP = mix(vec3(0.5, 0.5, 1.0), texture(dirt_nor, xz * 0.35).rgb, 0.4 + 0.6 * bare);
	NORMAL_MAP_DEPTH = 1.0;
	ROUGHNESS = mix(0.92, 0.85, bare);
	SPECULAR = 0.25;
	AO = 0.75 + 0.25 * fine;
}
