// The Aegis's field: every bubble is a soft disc nobody draws; this adds their glow up and shows
// only where the total is just past full, so clustered bubbles run together into one blob with a
// bright rim and lone ones stay round.
shader_type spatial;
render_mode unshaded, blend_mix, depth_test_disabled, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform vec4 c0 = vec4(0.0);
uniform vec4 c1 = vec4(0.0);
uniform vec4 c2 = vec4(0.0);
uniform vec4 c3 = vec4(0.0);
uniform vec4 c4 = vec4(0.0);
uniform vec4 c5 = vec4(0.0);
uniform vec4 tint : source_color = vec4(0.55, 0.95, 0.9, 1.0);
uniform float time = 0.0;

varying vec2 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xy;
}

float bubble(vec4 c) {
	// (xy: where, z: radius, w: how much of it is left; nothing where w is 0)
	vec2 d = wpos - c.xy;
	return c.w * (c.z * c.z) / (dot(d, d) + 0.0004);
}

void fragment() {
	float f = bubble(c0) + bubble(c1) + bubble(c2) + bubble(c3) + bubble(c4) + bubble(c5);
	float rim = smoothstep(1.0, 1.035, f) * (1.0 - smoothstep(1.1, 1.3, f));
	float body = smoothstep(1.0, 1.25, f) * 0.06;
	float shimmer = 0.85 + 0.15 * sin(time * 2.2 + wpos.x * 3.0 + wpos.y * 2.0);
	ALBEDO = tint.rgb + vec3(0.25) * rim;
	ALPHA = clamp(rim * 0.8 * shimmer + body, 0.0, 1.0);
}
