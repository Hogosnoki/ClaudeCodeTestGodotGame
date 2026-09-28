// A camp fire's flame: an upright billboard (it turns to the camera about its own up axis) with
// a tongue of fire licking upward, hot white-yellow at the heart, orange, then red at the edges.
// Additive and brighter than white, so the glow blooms it.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

instance uniform float seed = 0.0;
instance uniform float energy = 2.6;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	// face the camera, but stay upright
	vec3 up = normalize(MODEL_MATRIX[1].xyz);
	vec3 to_cam = INV_VIEW_MATRIX[3].xyz - MODEL_MATRIX[3].xyz;
	vec3 side = normalize(cross(up, to_cam));
	vec3 fwd = cross(side, up);
	mat4 bb = mat4(vec4(side * length(MODEL_MATRIX[0].xyz), 0.0), vec4(up * length(MODEL_MATRIX[1].xyz), 0.0), vec4(fwd, 0.0), MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = VIEW_MATRIX * bb;
}

void fragment() {
	vec2 uv = vec2(UV.x, 1.0 - UV.y); // y: 0 at the base
	float t = TIME * 2.3 + seed * 13.0;
	float n = vnoise(vec2(uv.x * 4.0 + seed * 7.0, uv.y * 3.0 - t)) * 0.6 + vnoise(vec2(uv.x * 9.0 + seed, uv.y * 7.0 - t * 1.7)) * 0.4;
	float w = 0.4 * (1.0 - uv.y) + 0.05;
	float dx = abs(uv.x - 0.5 + (n - 0.5) * 0.4 * uv.y);
	float body = smoothstep(w, w * 0.3, dx) * smoothstep(1.0, 0.2, uv.y + (n - 0.5) * 0.45) * smoothstep(0.0, 0.1, uv.y);
	vec3 cool = vec3(0.9, 0.18, 0.02), mid = vec3(1.0, 0.52, 0.1), hot = vec3(1.0, 0.93, 0.65);
	vec3 col = mix(cool, mid, smoothstep(0.1, 0.5, body));
	col = mix(col, hot, smoothstep(0.55, 0.95, body));
	ALBEDO = col * energy * body;
}
