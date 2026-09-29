// The cliff and boulders outside the cave: real rock textures projected from three sides (so there are
// no stretched faces on steep ground), with bedding lines, moss creeping on whatever faces the sky and
// gathers in the hollows, damp darkening and soil at the foot, and a little weathering streaked down.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D wall_tex : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D wall_nor : hint_normal, repeat_enable, filter_linear_mipmap;
uniform sampler2D moss_tex : source_color, repeat_enable, filter_linear_mipmap;
uniform vec3 tint : source_color = vec3(0.86, 0.82, 0.76);
uniform vec3 moss : source_color = vec3(0.26, 0.38, 0.13);
uniform vec3 soil : source_color = vec3(0.22, 0.17, 0.11);
uniform float scale = 0.22;
uniform float moss_amount = 1.0;
uniform float bump = 1.0;

varying vec3 v_wn;
varying vec3 v_wp;

float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

vec3 tri_w(vec3 n) {
	vec3 w = pow(abs(n), vec3(5.0));
	return w / (w.x + w.y + w.z + 1e-4);
}

vec3 tri_col(sampler2D t, vec3 p, vec3 w) {
	return texture(t, p.zy).rgb * w.x + texture(t, p.xz).rgb * w.y + texture(t, p.xy).rgb * w.z;
}

void fragment() {
	vec3 n = normalize(v_wn);
	vec3 w = tri_w(n);
	vec3 p = v_wp * scale;
	// two scales, so the pattern of the texture doesn't show
	vec3 rock = tri_col(wall_tex, p, w) * 0.6 + tri_col(wall_tex, p * 0.31 + 3.7, w) * 0.4;
	vec3 mosstex = tri_col(moss_tex, p * 1.3, w);
	float big = vnoise(v_wp * 0.35);
	float mid = vnoise(v_wp * 1.4);
	float fine = vnoise(v_wp * 5.0);
	vec3 c = rock * tint * (0.7 + 0.5 * big);
	// bedding lines: pale and dark bands running level through the rock
	float bed = sin(v_wp.y * 2.3 + vnoise(v_wp * vec3(0.3, 0.05, 0.3)) * 7.0);
	c *= 0.88 + 0.12 * bed;
	// weathering: dark streaks down the steep faces
	c *= 1.0 - 0.22 * smoothstep(0.5, 0.9, vnoise(v_wp * vec3(2.0, 0.15, 2.0))) * (1.0 - abs(n.y));
	// moss: on the upward faces, and in the hollows and cracks on the rest
	float facing = n.y * 0.5 + 0.5;
	float m = smoothstep(0.52, 0.78, facing + (big - 0.5) * 0.7 + (mid - 0.5) * 0.35) * moss_amount;
	m += smoothstep(0.62, 0.9, vnoise(v_wp * vec3(0.9, 0.6, 0.9)) + (1.0 - abs(n.y)) * 0.1) * 0.35 * moss_amount * smoothstep(1.0, 4.0, v_wp.y);
	m = clamp(m, 0.0, 1.0);
	vec3 mc = moss * (0.55 + 0.9 * mosstex.r) * (0.75 + 0.5 * fine);
	c = mix(c, mc, m);
	// the foot of it: damp, dark, and dusted with soil
	float foot = 1.0 - smoothstep(0.0, 1.6, v_wp.y);
	c = mix(c, soil * (0.7 + 0.6 * fine), foot * 0.6);
	c *= mix(1.0, 0.55, foot);
	// vertex colour tints (a stone's own shade)
	c *= pow(COLOR.rgb, vec3(2.2)) * 1.6;
	ALBEDO = c;
	ROUGHNESS = mix(0.86, 0.96, m);
	SPECULAR = 0.25 * (1.0 - m);

	// normal detail from the rock's normal map, blended in world space, then into view space
	vec3 nx = texture(wall_nor, p.zy).xyz * 2.0 - 1.0;
	vec3 ny = texture(wall_nor, p.xz).xyz * 2.0 - 1.0;
	vec3 nz = texture(wall_nor, p.xy).xyz * 2.0 - 1.0;
	vec3 tn = n + bump * (vec3(0.0, nx.y, nx.x) * w.x + vec3(ny.x, 0.0, ny.y) * w.y + vec3(nz.x, nz.y, 0.0) * w.z) * (1.0 - 0.5 * m);
	NORMAL = normalize((VIEW_MATRIX * vec4(normalize(tn), 0.0)).xyz);
}
