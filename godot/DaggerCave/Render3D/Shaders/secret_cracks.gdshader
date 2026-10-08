// The cracks over a secret wing's wall: a web of fine cracks across the rock facing the camera (a Voronoi of broken stone), black, kept to the wall's own rectangle (fading out at its edges) so the rock round it is
// unmarked. `hurt` (0..1) is how much of it has been knocked off: the more, the more and the wider the cracks.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_lambert, specular_disabled;

uniform vec4 rect = vec4(0.0, 0.0, 1.0, 1.0); // x0, y0, x1, y1 in world metres (y up)
uniform float hurt = 0.0;
uniform float seed = 0.0;

varying vec3 v_wp;
varying vec3 v_wn;

vec2 hash2(vec2 p) {
	p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
	return fract(sin(p) * 43758.5453);
}

// distance to the nearest Voronoi edge (0 on a crack)
float edge(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			vec2 g = vec2(float(x), float(y));
			vec2 o = hash2(i + g + seed);
			float d = length(g + o - f);
			if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
		}
	return d2 - d1;
}

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	VERTEX += NORMAL * 0.012;
}

void fragment() {
	// only the faces turned to the camera, and only inside the wall
	float facing = smoothstep(-0.35, 0.05, v_wn.z);
	vec2 inside = smoothstep(rect.xy - 0.2, rect.xy + 0.6, v_wp.xy) * (1.0 - smoothstep(rect.zw - 0.6, rect.zw + 0.2, v_wp.xy));
	float mask = facing * inside.x * inside.y;
	// big plates, then small ones, then a fine grit: wider with each blow it has taken
	float w = 0.085 + 0.1 * hurt;
	float a = 1.0 - smoothstep(0.0, w, edge(v_wp.xy * 0.8));
	float b = 1.0 - smoothstep(0.0, w * 0.8, edge(v_wp.xy * 1.9 + 11.0));
	float c = (1.0 - smoothstep(0.0, w * 0.7, edge(v_wp.xy * 4.2 + 23.0))) * (0.45 + 0.55 * hurt);
	float crack = clamp(a + b * 0.85 + c * 0.7, 0.0, 1.0) * mask;
	ALBEDO = vec3(0.0);
	EMISSION = vec3(0.0);
	ALPHA = crack * (0.92 + 0.08 * hurt);
	ROUGHNESS = 1.0;
}
