// Air bubbles: a thin shimmering skin, bright at the rim, with a highlight.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

varying vec4 v_col;
void vertex() { v_col = COLOR; }

void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(1.0 - ndv, 2.5);
	vec3 hl = normalize(vec3(-0.4, 0.6, 1.0));
	float spec = pow(max(dot(reflect(-VIEW, NORMAL), hl), 0.0), 40.0);
	ALBEDO = mix(v_col.rgb, vec3(1.0), rim * 0.5) + vec3(spec) * 2.0;
	ALPHA = clamp((0.08 + rim * 0.75 + spec) * v_col.a, 0.0, 1.0);
}
