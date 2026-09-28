// The meadow outside the cave: two greens in broad patches, sunnier dry grass here and there, a
// fine mottle up close, and the trodden earth round the camp fire.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 grass_a : source_color = vec3(0.26, 0.5, 0.13);
uniform vec3 grass_b : source_color = vec3(0.42, 0.66, 0.2);
uniform vec3 dry : source_color = vec3(0.66, 0.68, 0.3);
uniform vec3 dirt : source_color = vec3(0.4, 0.31, 0.2);
uniform float bare_radius = 2.4;

varying vec3 v_wp;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float s = 0.0, a = 0.5;
	for (int k = 0; k < 4; k++) { s += a * vnoise(p); p *= 2.03; a *= 0.5; }
	return s;
}

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float n = fbm(v_wp.xz * 0.18);
	float fine = fbm(v_wp.xz * 2.2 + 3.0);
	vec3 c = mix(grass_a, grass_b, smoothstep(0.3, 0.7, n));
	c = mix(c, dry, smoothstep(0.6, 0.8, fbm(v_wp.xz * 0.05 + 11.0)) * 0.55);
	c *= 0.86 + 0.28 * fine;
	float d = length(v_wp.xz);
	c = mix(dirt * (0.8 + 0.3 * fine), c, smoothstep(bare_radius * 0.55, bare_radius, d + (fine - 0.5) * 0.9));
	ALBEDO = c;
	ROUGHNESS = 0.95;
	SPECULAR = 0.15;
}
