// Ice that builds itself out of frost (the Swordsman's axe heads): each surface carries a gradient from where it grows out of
// (the blade, a crystal's foot) to its far edge, roughened with noise; whatever lies past the growth front is not drawn, and the front
// itself burns white, so as `grow` rises the ice draws itself outward in a ragged, glittering frost line.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_always;

instance uniform float grow = 1.0;
instance uniform float flare = 0.0;
// 0: the gradient runs along UV.x (out from the blade); 1: along UV.y (up a crystal from its foot)
uniform int axis = 0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	float g = axis == 0 ? UV.x : UV.y;
	float n = vnoise(UV * vec2(9.0, 7.0)) * 0.6 + vnoise(UV * 23.0 + 3.7) * 0.4;
	float edge = g * 0.8 + (n - 0.5) * 0.45 + 0.1;
	float front = grow * 1.2;
	if (edge > front) discard;
	// (bright, frosted white at the growing front; clear blue ice behind it)
	float rim = 1.0 - smoothstep(0.0, 0.09, front - edge);
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	ALBEDO = mix(COLOR.rgb * vec3(0.8, 0.92, 1.0), vec3(0.9, 0.97, 1.0), rim * 0.8);
	ALPHA = mix(0.8, 1.0, rim);
	ROUGHNESS = mix(0.1, 0.6, rim);
	METALLIC = 0.15;
	RIM = 0.9;
	RIM_TINT = 0.4;
	EMISSION = vec3(0.3, 0.6, 1.0) * (0.3 + 1.6 * flare) * (0.5 + 0.8 * pow(1.0 - ndv, 2.0)) + vec3(0.7, 0.9, 1.0) * rim * 1.6;
}
