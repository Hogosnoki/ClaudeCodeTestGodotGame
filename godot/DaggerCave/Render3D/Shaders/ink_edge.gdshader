// Ink outlines for creatures, as a screen-space edge pass: a line round the creature's outline on the
// screen, and nowhere else. There is no second body and no second hull: the creatures are drawn once
// more (by a camera that sees only them, see InkOutline.cs) into a small mask whose pixels hold how far
// off the creature is there and what colour its line takes. This pass, laid over the whole view, finds
// the pixels just OUTSIDE a creature's silhouette (a few taps round each pixel hit the mask) and paints
// them with the ink. Nothing is ever drawn over the creature itself, so there are no lines along its
// own folds or limbs. A creature hidden behind rock in front of it gets no line there, and the line
// never lies over rock in front of it: both are told from the scene's depth. The line is a fixed
// number of screen pixels wide, the same at any distance.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
// R: the creature's distance from the camera (0: no creature here); G: 0 for the usual ink, else 1 + a packed colour
uniform sampler2D mask_tex : filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.015, 0.012, 0.02, 0.92);
/// The line's width in screen pixels (at 720 lines: it scales with the resolution).
uniform float edge_px = 3.5;
/// How much nearer than the creature the scene may be before it counts as covering it.
uniform float depth_tol = 0.12;

void vertex() {
	// a full-screen quad, drawn over everything at the near plane
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float linear_depth(vec2 uv, mat4 inv_proj) {
	float z = texture(depth_tex, uv).r;
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, z, 1.0);
	float w = abs(v.w) < 1e-5 ? 1e-5 : v.w;
	// (open sky at the far plane is far off but finite)
	return clamp(-v.z / w, 0.0, 5000.0);
}

// the colour packed into the mask's G channel (see creature.gdshader): three 3-bit square roots
vec3 unpack_ink(float g) {
	float k = max(g - 1.0, 0.0);
	float r = mod(k, 8.0);
	float gg = mod(floor(k / 8.0), 8.0);
	float b = floor(k / 64.0);
	vec3 q = vec3(r, gg, b) / 7.0;
	return q * q;
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 texel = 1.0 / VIEWPORT_SIZE;
	// inside a creature: no line (the creature itself is there)
	if (texture(mask_tex, uv).r > 0.0) discard;
	float px = max(1.0, VIEWPORT_SIZE.y / 720.0) * edge_px;
	float here_d = linear_depth(uv, INV_PROJECTION_MATRIX);
	float hit = 0.0;
	float kind = 0.0;
	// taps at two radii round the pixel: how much of the way round a creature lies near
	for (int r = 1; r <= 2; r++) {
		float rad = px * float(r) * 0.5;
		for (int k = 0; k < 12; k++) {
			float a = float(k) * 0.5235988;
			vec2 tuv = uv + vec2(cos(a), sin(a)) * rad * texel;
			vec4 m = texture(mask_tex, tuv);
			if (m.r <= 0.0) continue;
			// the creature shows there (nothing nearer hides it), and nothing nearer than it lies over this pixel
			float scene_t = linear_depth(tuv, INV_PROJECTION_MATRIX);
			if (scene_t < m.r - depth_tol || here_d < m.r - depth_tol) continue;
			hit += 1.0;
			kind = max(kind, m.g);
		}
	}
	if (hit < 0.5) discard;
	float e = clamp(hit / 8.0, 0.0, 1.0);
	vec3 col = kind > 0.5 ? unpack_ink(kind) : ink.rgb;
	ALBEDO = col;
	ALPHA = ink.a * e;
}
