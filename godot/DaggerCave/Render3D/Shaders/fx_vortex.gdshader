// A portal's mouth: a swirling tunnel of the next biome's light, darker at its heart.
shader_type spatial;
render_mode blend_mix, unshaded, cull_disabled, fog_disabled, shadows_disabled;

instance uniform vec4 deep_color : source_color = vec4(0.1, 0.05, 0.2, 1.0);
instance uniform vec4 glow_color : source_color = vec4(0.7, 0.5, 1.0, 1.0);
instance uniform float grow = 1.0;

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	uv.y = -uv.y;
	// an arch: a half-disc on top of a rectangle
	vec2 q = vec2(uv.x, uv.y + 0.2);
	float inside = q.y > 0.0 ? step(length(q / vec2(1.0, 1.0)), 1.0) : step(abs(uv.x), 1.0) * step(-1.0, uv.y);
	if (inside < 0.5) discard;
	float r = length(q * vec2(1.0, 0.85));
	float ang = atan(q.y, q.x);
	float swirl = 0.5 + 0.5 * sin(ang * 4.0 + log(max(r, 0.02)) * 7.0 - TIME * 3.0);
	float depth = smoothstep(1.0, 0.0, r);
	vec3 col = mix(glow_color.rgb * 1.8, deep_color.rgb, depth * 0.85);
	col += glow_color.rgb * swirl * (1.0 - depth) * 0.9;
	col += glow_color.rgb * pow(1.0 - r, 6.0) * 2.0;
	ALBEDO = col * grow;
	ALPHA = grow;
}
