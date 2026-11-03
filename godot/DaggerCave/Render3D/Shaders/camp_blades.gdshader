// The meadow's grass, blade by blade: each tuft's blades taper and curl, darker and browner at the root, paler at the tip, every
// tuft its own shade (the instance colour), glowing where the low sun comes through them from behind, and bending in a breeze
// that rolls across the meadow in gusts.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec3 root_color : source_color = vec3(0.07, 0.06, 0.03);
uniform vec3 mid_color : source_color = vec3(0.05, 0.085, 0.03);
uniform vec3 tip_color : source_color = vec3(0.12, 0.2, 0.07);
uniform float wind = 0.16;
uniform float translucency = 0.16;

varying float v_h;
varying vec3 v_tint;

void vertex() {
	v_h = UV.y;
	v_tint = COLOR.rgb;
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// gusts roll across the meadow; each blade nods on top of that
	float gust = sin(TIME * 0.7 + wp.x * 0.15 + wp.z * 0.1) * 0.5 + 0.5;
	float sway = (sin(TIME * 1.8 + wp.x * 0.9 + wp.z * 0.6) * 0.6 + sin(TIME * 3.7 + wp.z * 2.1) * 0.25) * (0.4 + gust);
	float bend = wind * v_h * v_h;
	VERTEX.x += (sway + gust * 0.8) * bend;
	VERTEX.z += cos(TIME * 1.3 + wp.x * 0.4) * bend * 0.4;
	VERTEX.y -= abs(sway) * bend * 0.25;
}

void fragment() {
	vec3 c = mix(root_color, mid_color, smoothstep(0.0, 0.45, v_h));
	c = mix(c, tip_color, smoothstep(0.45, 1.0, v_h));
	c *= v_tint;
	ALBEDO = c;
	ROUGHNESS = 0.7;
	SPECULAR = 0.3;
	// thin blades let the low sun through
	BACKLIGHT = c * translucency * (0.4 + 0.6 * v_h);
	AO = mix(0.25, 1.0, smoothstep(0.0, 0.6, v_h));
	AO_LIGHT_AFFECT = 0.5;
}
