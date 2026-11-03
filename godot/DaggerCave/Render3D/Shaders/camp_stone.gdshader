// The camp's rock (cliff, boulders, the far mountains): photographed rock, projected from three sides so it never stretches, with
// its normal and roughness maps; moss and grass settle on whatever faces the sky, lichen speckles the rest, and rain streaks
// darken the faces downward. No toon steps: lit as it is.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_nor : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_arm : filter_linear_mipmap, repeat_enable;
uniform sampler2D moss_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float scale = 0.22;
uniform float macro = 0.035;
uniform vec3 tint : source_color = vec3(0.86, 0.82, 0.84);
uniform vec3 moss_col : source_color = vec3(0.24, 0.30, 0.13);
uniform vec3 grass_col : source_color = vec3(0.26, 0.27, 0.12);
uniform float moss_amount = 1.0;
uniform float grass_amount = 0.0;
uniform float use_vertex_color = 1.0;
// how far the rock's own colour is drained toward grey (the photographs run warm)
uniform float desat = 0.0;

varying vec3 v_wp;
varying vec3 v_wn;

float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float vnoise(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 p) { float s = 0.0, a = 0.5; for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.07 + 5.3; a *= 0.5; } return s; }

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// (a macro, not a function: a function may only ever be handed textures with the same sampling settings)
#define tri3(t, p, w) (texture(t, (p).zy).rgb * (w).x + texture(t, (p).xz).rgb * (w).y + texture(t, (p).xy).rgb * (w).z)

void fragment() {
	vec3 n = normalize(v_wn);
	vec3 w = pow(abs(n), vec3(4.0));
	w /= (w.x + w.y + w.z);
	vec3 p = v_wp * scale;
	// two scales of the same rock, so neither repeats visibly (the big one breaks up the small one's tiling)
	vec3 rock = tri3(rock_tex, p, w);
	vec3 rockBig = tri3(rock_tex, v_wp * macro + 0.37, w);
	rock = rock * mix(0.75, 1.25, dot(rockBig, vec3(0.333)));
	vec3 arm = tri3(rock_arm, p, w);
	rock = mix(rock, vec3(dot(rock, vec3(0.3, 0.55, 0.15))), desat);
	vec3 base = rock * tint;
	if (use_vertex_color > 0.5) base *= COLOR.rgb * 1.3;
	// weathering: big blotches of tone, darker in crevices (the AO map), rain streaks down the faces
	float blotch = fbm(v_wp * 0.09);
	base *= 0.78 + 0.45 * blotch;
	float streak = vnoise(vec3(v_wp.x * 1.4, v_wp.y * 0.08, v_wp.z * 1.4));
	base *= mix(1.0, 0.72, smoothstep(0.55, 0.9, streak) * (1.0 - abs(n.y)));
	// lichen: pale grey-green specks
	float lich = smoothstep(0.72, 0.78, vnoise(v_wp * 3.1)) * smoothstep(0.3, 0.7, fbm(v_wp * 0.6));
	base = mix(base, vec3(0.5, 0.51, 0.46), lich * 0.15);
	// moss and grass on what faces the sky (ragged, by noise)
	float upness = n.y + (fbm(v_wp * 0.7) - 0.5) * 0.5;
	float moss = smoothstep(0.35, 0.75, upness) * moss_amount;
	vec3 mossC = moss_col * tri3(moss_tex, v_wp * 0.6, w) * 2.2;
	base = mix(base, mossC, moss * 0.85);
	float grass = smoothstep(0.6, 0.85, upness) * grass_amount;
	base = mix(base, grass_col * (0.75 + 0.5 * vnoise(v_wp * 2.3)), grass);

	ALBEDO = base;
	// the rock's own relief (its normal map, triplanar, in the face's tangent frame near enough)
	vec3 nm = tri3(rock_nor, p, w);
	NORMAL_MAP = mix(nm, vec3(0.5, 0.5, 1.0), moss * 0.6);
	NORMAL_MAP_DEPTH = 2.0;
	AO = arm.r * arm.r;
	AO_LIGHT_AFFECT = 0.6;
	ROUGHNESS = clamp(arm.g * 1.05 + moss * 0.2, 0.45, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.35;
}
