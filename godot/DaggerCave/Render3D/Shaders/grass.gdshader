// Grass and moss tufts: sway in a slow cave draught, translucent at the tips.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec3 root_color : source_color = vec3(0.10, 0.16, 0.06);
uniform vec3 tip_color : source_color = vec3(0.35, 0.55, 0.2);
uniform float wind = 0.06;
uniform float tip_glow = 0.0;

varying float v_h;
varying vec3 v_wp;

void vertex() {
	v_h = UV.y;
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float sway = sin(TIME * 1.4 + v_wp.x * 0.7 + v_wp.z * 0.4) * 0.65 + sin(TIME * 2.9 + v_wp.x * 2.3) * 0.25;
	VERTEX.x += sway * wind * v_h * v_h;
	VERTEX.z += cos(TIME * 1.1 + v_wp.x * 0.5) * wind * 0.4 * v_h * v_h;
}

void fragment() {
	vec3 c = mix(root_color, tip_color, smoothstep(0.0, 1.0, v_h));
	float var = fract(sin(dot(floor(v_wp.xz * 3.0), vec2(12.9898, 78.233))) * 43758.5453);
	c *= 0.8 + 0.4 * var;
	ALBEDO = c;
	ROUGHNESS = 0.8;
	SPECULAR = 0.25;
	BACKLIGHT = c * 0.6;
	AO = mix(0.35, 1.0, v_h);
	AO_LIGHT_AFFECT = 0.3;
	EMISSION = tip_color * tip_glow * v_h * v_h;
}
