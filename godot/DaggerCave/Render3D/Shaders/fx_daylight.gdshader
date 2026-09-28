// The daylight at the cave mouth: an overexposed white that floods the end of the tunnel and
// fades into the cave. UV.x runs from outside (0) into the cave (1); UV.y from the roof (0) to
// the floor (1). Brighter than white, so the glow blooms it.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled, blend_mix;

uniform vec3 day_color : source_color = vec3(1.0, 0.98, 0.92);
uniform float energy = 2.4;
// how soft the roof and floor edges are, and where along the quad the white starts to thin out
uniform float edge = 0.14;
instance uniform float fade_start = 0.2;
instance uniform float shimmer = 1.0;
instance uniform float strength = 1.0;

void fragment() {
	float x = UV.x, y = UV.y;
	float v = smoothstep(0.0, edge, y) * smoothstep(1.0, 1.0 - edge * 0.6, y);
	float h = 1.0 - smoothstep(fade_start, 1.0, x);
	// faint moving shafts: the light breaking through leaves outside
	float shafts = 0.88 + 0.12 * sin(y * 9.0 - x * 4.0 + TIME * 0.7 * shimmer) * sin(y * 3.7 + TIME * 0.31);
	float a = clamp(v * h * strength, 0.0, 1.0);
	ALBEDO = day_color * energy * mix(1.0, shafts, x);
	ALPHA = a * a * (3.0 - 2.0 * a);
}
