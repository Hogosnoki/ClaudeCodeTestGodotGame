// A waterfall: streaks of water pouring down a flat quad, thin at the edges, foaming at the foot.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 water_color : source_color = vec4(0.45, 0.68, 0.82, 0.8);
uniform float speed = 2.4;
instance uniform float height_m = 10.0;
uniform float seed = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void fragment() {
	float x = UV.x;
	float y = UV.y * height_m;
	float s = vnoise(vec2(x * 13.0 + seed, y * 0.55 - TIME * speed)) * 0.6 + vnoise(vec2(x * 29.0 + 7.0 + seed, y * 0.3 - TIME * speed * 1.45)) * 0.4;
	float edge = smoothstep(0.0, 0.16, x) * smoothstep(1.0, 0.84, x);
	float top = smoothstep(0.0, 0.04, UV.y);
	float foot = smoothstep(0.86, 1.0, UV.y);
	float a = edge * top * (0.22 + 0.78 * s) * water_color.a;
	ALBEDO = mix(water_color.rgb, vec3(0.95, 0.98, 1.0), s * 0.45 + foot * 0.4);
	ALPHA = clamp(a + foot * edge * 0.22, 0.0, 1.0);
}
