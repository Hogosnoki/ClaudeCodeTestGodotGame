// Sunlit rock outside the cave: its own vertex colours, with moss and grass on whatever faces the
// sky and a little weathering streaked down the faces.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 moss : source_color = vec3(0.3, 0.5, 0.16);
uniform float moss_amount = 1.0;

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

void fragment() {
	float n = vnoise(v_wp * 1.3) * 0.6 + vnoise(v_wp * 4.1) * 0.4;
	vec3 c = pow(COLOR.rgb, vec3(2.2)) * (0.78 + 0.4 * n);
	// streaks of weathering down the faces
	c *= 0.9 + 0.1 * sin(v_wp.y * 3.0 + vnoise(v_wp * vec3(2.0, 0.2, 2.0)) * 6.0);
	float up = smoothstep(0.45, 0.8, v_wn.y + (n - 0.5) * 0.5) * moss_amount;
	c = mix(c, moss * (0.75 + 0.5 * n), up);
	ALBEDO = c;
	ROUGHNESS = mix(0.9, 0.95, up);
	SPECULAR = 0.2;
}
