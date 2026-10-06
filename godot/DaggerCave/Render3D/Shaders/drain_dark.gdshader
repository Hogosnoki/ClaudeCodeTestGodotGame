// The darkness over a drain: nothing at the top, thickening to solid black, soft at the sides.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

instance uniform float fade_frac = 0.2;

void fragment() {
	float down = smoothstep(0.0, fade_frac, UV.y);
	float side = 1.0 - smoothstep(0.28, 0.5, abs(UV.x - 0.5));
	ALBEDO = vec3(0.0);
	ALPHA = clamp(down * down * side * 0.99, 0.0, 1.0);
}
