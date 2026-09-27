// Afterimages: a creature's pose left behind as a glowing, fading shell.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_back, fog_disabled, shadows_disabled;

instance uniform vec4 ghost_color : source_color = vec4(0.5, 0.8, 1.0, 1.0);

void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(1.0 - ndv, 1.6);
	ALBEDO = ghost_color.rgb * (0.25 + rim * 1.6) * ghost_color.a;
}
