// Glowing cave life and minerals: vertex COLOR.rgb is the base colour, COLOR.a how much that
// part glows. `crystal` turns on glassy facets with a light-catching rim.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 glow_color : source_color = vec3(0.4, 0.9, 1.0);
uniform float glow_energy = 3.0;
uniform float roughness = 0.55;
uniform float crystal = 0.0;
uniform float pulse = 0.35;
uniform float rim = 0.0;

varying vec3 v_wp;

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float g = COLOR.a;
	vec3 base = pow(COLOR.rgb, vec3(2.2)); // authored in sRGB
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	float beat = 1.0 - pulse * (0.5 + 0.5 * sin(TIME * 1.3 + v_wp.x * 1.7 + v_wp.y * 1.1));
	ALBEDO = mix(base, glow_color * 0.5 + base * 0.3, g * 0.6 + crystal * 0.3);
	ROUGHNESS = mix(roughness, 0.06, crystal);
	SPECULAR = mix(0.5, 0.95, crystal);
	METALLIC = crystal * 0.15;
	EMISSION = glow_color * glow_energy * (g * g * beat + crystal * (fres * 0.8 + g * 0.25) + rim * fres);
	BACKLIGHT = glow_color * crystal * 0.5;
}
