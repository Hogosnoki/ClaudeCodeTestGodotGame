// A single additive glow sprite (props: fireballs, orbs, vortices, flames). Same shapes as the
// particle glow, set per instance: shape 0 soft dot, 1 streak, 2 ring, 3 star, 4 flash,
// 6 flame (tongue rising from the bottom edge), 7 swirl (a vortex).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

instance uniform vec4 sprite_color : source_color = vec4(1.0, 0.6, 0.2, 1.0);
instance uniform float sprite_shape = 0.0;
instance uniform float sprite_intensity = 2.0;
instance uniform float sprite_param = 0.0;
uniform bool billboard = true;

float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise2(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	if (billboard) {
		// face the camera, keep the node's scale
		vec3 sx = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx.x, INV_VIEW_MATRIX[1] * sx.y, INV_VIEW_MATRIX[2] * sx.z, MODEL_MATRIX[3]);
		MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	}
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	uv.y = -uv.y; // +y up
	float s = sprite_shape;
	float a = 0.0, core = 0.0;
	if (s < 0.5) {
		float r2 = dot(uv, uv);
		a = exp(-r2 * 4.5); core = exp(-r2 * 22.0);
	} else if (s < 1.5) {
		a = exp(-uv.y * uv.y * 7.0) * smoothstep(-1.0, 0.55, uv.x) * (1.0 - smoothstep(0.82, 1.0, uv.x));
		core = exp(-uv.y * uv.y * 40.0) * smoothstep(0.1, 0.85, uv.x) * (1.0 - smoothstep(0.9, 1.0, uv.x));
	} else if (s < 2.5) {
		float r = length(uv); float th = max(sprite_param, 0.02);
		a = exp(-pow((r - 0.78) / th, 2.0)); core = exp(-pow((r - 0.78) / (th * 0.3), 2.0)) * 0.8;
	} else if (s < 3.5) {
		float r2 = dot(uv, uv);
		a = max(exp(-abs(uv.y) * 45.0) * max(0.0, 1.0 - abs(uv.x)), exp(-abs(uv.x) * 45.0) * max(0.0, 1.0 - abs(uv.y)) * 0.7) + exp(-r2 * 14.0) * 0.7;
		core = exp(-r2 * 50.0);
	} else if (s < 4.5) {
		float r2 = dot(uv, uv);
		a = exp(-r2 * 2.6) * 0.65 + exp(-r2 * 9.0) * 0.6; core = exp(-r2 * 30.0);
	} else if (s < 6.5) {
		// flame: a tongue from the bottom, wavering, hot at its root
		float h = uv.y * 0.5 + 0.5;
		float n = noise2(vec2(uv.x * 3.0, h * 4.0 - TIME * 6.0 + sprite_param * 10.0));
		float w = (1.0 - h) * 0.9 * (0.75 + 0.5 * n);
		float x = abs(uv.x + (n - 0.5) * 0.35 * h);
		a = smoothstep(w, w * 0.4, x) * smoothstep(1.0, 0.55, h) * smoothstep(0.0, 0.08, h);
		core = smoothstep(w * 0.5, 0.0, x) * smoothstep(0.7, 0.1, h) * smoothstep(0.0, 0.1, h);
	} else {
		// swirl: a spiral vortex that turns
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float spiral = sin(ang * 3.0 + r * 9.0 - TIME * 4.0);
		a = smoothstep(1.0, 0.3, r) * (0.55 + 0.45 * spiral) * smoothstep(0.0, 0.15, r);
		core = exp(-r * r * 9.0);
	}
	// fade to nothing before the quad's edge
	float edge = 1.0 - smoothstep(0.8, 1.0, max(abs(uv.x), abs(uv.y)));
	a *= edge; core *= edge;
	vec3 col = sprite_color.rgb * sprite_color.a;
	ALBEDO = (col * a + vec3(1.0) * core * sprite_color.a * 0.8) * sprite_intensity;
}
