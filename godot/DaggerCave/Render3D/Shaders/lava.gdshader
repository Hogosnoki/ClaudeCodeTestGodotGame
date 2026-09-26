// Molten rock. On the surface: dark cooling crust plates drifting on a blinding flow, bright in
// the cracks between them. On the cut face (`face` = 1): a churning cross-section, hottest at
// the top.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_disabled;

uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform vec3 hot : source_color = vec3(1.0, 0.75, 0.3);
uniform vec3 warm : source_color = vec3(1.0, 0.32, 0.05);
uniform vec3 crust : source_color = vec3(0.09, 0.05, 0.04);
uniform float energy = 5.0;
uniform float face = 0.0;
uniform float surface_y = 0.0;

varying vec3 v_wp;

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (face < 0.5) VERTEX.y += (texture(noise_tex, v_wp.xz * 0.05 + TIME * 0.01).r - 0.5) * 0.25;
}

void fragment() {
	float t = TIME;
	vec2 p = face > 0.5 ? vec2(v_wp.x, v_wp.y) : v_wp.xz;
	vec2 flow = vec2(t * 0.12, 0.0);
	float n1 = texture(noise_tex, p * 0.06 + flow * 0.3).r;
	float n2 = texture(noise_tex, p * 0.17 - flow * 0.5 + vec2(0.3, 0.7)).r;
	float n3 = texture(noise_tex, p * 0.41 + vec2(t * 0.03, -t * 0.02)).r;
	float cracks = smoothstep(0.42, 0.5, n1 * 0.6 + n2 * 0.4) - smoothstep(0.5, 0.58, n1 * 0.6 + n2 * 0.4);
	float plate = smoothstep(0.48, 0.62, n1 * 0.55 + n3 * 0.45);
	float heat = 1.0;
	if (face > 0.5) {
		float below = surface_y - v_wp.y;
		heat = exp(-below / 7.0);
		// convection: slow rising swirls
		float swirl = texture(noise_tex, vec2(v_wp.x * 0.07, v_wp.y * 0.05 - t * 0.04)).r;
		plate *= smoothstep(0.35, 0.8, swirl) * 0.8;
	}
	vec3 molten = mix(warm, hot, clamp(n3 * 1.4 - 0.2 + cracks, 0.0, 1.0));
	vec3 glow = molten * energy * mix(1.0, 0.25, plate) * (0.45 + 0.55 * heat);
	glow += hot * cracks * energy * 1.5 * (1.0 - plate * 0.5);
	ALBEDO = mix(molten * 0.3, crust, plate);
	ROUGHNESS = mix(0.35, 0.95, plate);
	SPECULAR = 0.3;
	EMISSION = glow;
}
