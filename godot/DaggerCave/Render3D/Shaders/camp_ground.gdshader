// The meadow outside the cave: two greens in broad patches, sunnier dry grass here and there, a
// fine mottle up close, and the trodden earth round the camp fire.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 grass_a : source_color = vec3(0.17, 0.3, 0.09);
uniform vec3 grass_b : source_color = vec3(0.3, 0.42, 0.14);
uniform vec3 dry : source_color = vec3(0.5, 0.46, 0.24);
uniform vec3 dirt : source_color = vec3(0.3, 0.23, 0.15);
// the trodden way from the fire to the cave, and the stony soil at the cliff's foot
uniform vec2 path_a = vec2(0.0, 0.0);
uniform vec2 path_b = vec2(9.8, -7.5);
uniform float path_w = 1.3;
uniform vec2 cliff_o = vec2(9.8, -7.5);
uniform vec2 cliff_n = vec2(-0.62, 0.78);
uniform float bare_radius = 2.4;

varying vec3 v_wp;
#include "res://DaggerCave/Render3D/Shaders/cel.gdshaderinc"

// the cave mouth: where it stands, which way it faces (toward the camp), and its size. Whatever lies in it
// or just in front of it is shaded down into the dark of the tunnel, so the light gives out gradually.
uniform vec3 mouth_o = vec3(9.8, 0.0, -7.5);
uniform vec3 mouth_n = vec3(-0.6223, 0.0, 0.7828);
uniform float mouth_hw = 3.1;
uniform float mouth_h = 6.3;
// the camp's own haze (as the environment's: see CampScene.BuildSkyAndLight), drawn here so it can thin out in the mouth
uniform vec3 haze : source_color = vec3(0.72, 0.78, 0.86);
uniform float haze_density = 0.0036;

// 0 in daylight .. 1 in the dark of the mouth. (arch: whether the height counts, for the rock; the ground is all floor)
float mouth_shade(vec3 wp, bool arch) {
	vec3 rel = wp - mouth_o;
	vec3 right = vec3(mouth_n.z, 0.0, -mouth_n.x);
	float a = dot(rel, right), back = -dot(rel, mouth_n);
	float d = abs(a) - mouth_hw;
	if (arch) {
		float dy = wp.y - (mouth_h - mouth_hw);
		if (dy > 0.0) d = length(vec2(a, dy)) - mouth_hw;
	}
	float within = 1.0 - smoothstep(-0.6, 1.8, d);
	float deep = smoothstep(-4.5, -0.4, back);
	return clamp(within * deep * deep * (3.0 - 2.0 * deep), 0.0, 1.0);
}

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
	// the path: worn to earth, ragged at its edges
	vec2 pa = v_wp.xz - path_a, ba = path_b - path_a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	float pd = length(pa - ba * h);
	float worn = 1.0 - smoothstep(path_w * 0.45, path_w * (0.9 + 0.5 * fine), pd + (fine - 0.5) * 0.5);
	c = mix(c, dirt * (0.75 + 0.5 * fine), worn * 0.9);
	// stony soil toward the cliff
	float toward = dot(v_wp.xz - cliff_o, cliff_n);
	float foot = (1.0 - smoothstep(0.5, 6.5, toward + (fine - 0.5) * 2.0)) * step(-3.0, toward);
	c = mix(c, vec3(0.28, 0.25, 0.2) * (0.7 + 0.6 * fine), foot * 0.75);
	c = cel_tone(c);
	float sh = mouth_shade(v_wp, false);
	c *= 1.0 - 0.97 * sh;
	ALBEDO = c;
	ROUGHNESS = 0.95;
	SPECULAR = 0.15 * (1.0 - sh);
	// (the haze, but none over the dark of the mouth: otherwise the black there would be lifted to a pale grey)
	FOG = vec4(haze, (1.0 - exp(-haze_density * length(VERTEX))) * (1.0 - sh));
}

// The stylised light (as on the creatures and the cave rock): soft-edged bands in a perceptual scale; off, plain Lambert.
void light() {
	float lit = clamp(dot(NORMAL, LIGHT), 0.0, 1.0) * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * cel_light(dot(NORMAL, LIGHT), ATTENUATION) / PI;
	vec3 hv = normalize(LIGHT + VIEW);
	float sp = pow(clamp(dot(NORMAL, hv), 0.0, 1.0), mix(6.0, 48.0, 1.0 - ROUGHNESS)) * ATTENUATION * (1.0 - ROUGHNESS);
	SPECULAR_LIGHT += LIGHT_COLOR * cel_spec(sp, 0.3) * SPECULAR_AMOUNT * 0.5 / PI;
}
