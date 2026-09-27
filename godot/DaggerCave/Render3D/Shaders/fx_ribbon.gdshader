// Additive ribbons from vertex colours (sword smears, shield arcs): HDR, so they bloom.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

uniform float intensity = 1.3;

void fragment() {
	ALBEDO = COLOR.rgb * COLOR.a * intensity;
}
