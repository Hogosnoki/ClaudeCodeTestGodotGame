// Ink outlines for creatures: a Sobel filter over the scene's depth and normals, run only over
// each creature's own pixels. This pass is the creature's mesh drawn again (the next pass of
// its material), pushed out a little along its normals so the band just outside the silhouette
// is covered too; each of its pixels looks at its 3x3 neighbourhood in the depth and normal
// buffers and inks where either jumps. Samples are a fixed step apart on screen, so the line is
// the same couple of pixels wide at any distance.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D normal_tex : hint_normal_roughness_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.015, 0.012, 0.02, 0.92);
/// How far the hull is pushed out along the normals (model metres).
uniform float width = 0.05;
/// Depth jump (as a share of the distance) that starts a line, and the normal jump that does.
uniform float depth_edge = 0.035;
uniform float normal_edge = 1.3;
// ghosts thin out into nothing below this rest-pose height (as in creature.gdshader)
uniform float ghost_below = -100.0;
uniform float ghost_fade = 0.3;

// shared with the creature's own pass: the same slots as in creature.gdshader
instance uniform float fade : instance_index(3) = 1.0;
instance uniform float dissolve : instance_index(4) = 0.0;

varying vec3 v_rest;

void vertex() {
	v_rest = vec3(UV.x, UV.y, UV2.x);
	VERTEX += NORMAL * width;
}

float linear_depth(vec2 uv, mat4 inv_proj) {
	float z = texture(depth_tex, uv).r;
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, z, 1.0);
	// (open sky at the far plane has no finite depth: keep it far off but finite, or the edge
	// test turns to NaN there and the glow spreads it into a blaze)
	float w = abs(v.w) < 1e-5 ? 1e-5 : v.w;
	return clamp(-v.z / w, 0.0, 5000.0);
}

void fragment() {
	// no ink on something burning away, fading out, or thinning into a ghostly trail
	if (dissolve > 0.02 || fade < 0.35 || v_rest.y < ghost_below + ghost_fade * 0.6) discard;
	// one sample step per 720 lines of screen, so the line keeps its weight at any resolution
	vec2 px = max(1.0, VIEWPORT_SIZE.y / 720.0) / VIEWPORT_SIZE;
	float d[9];
	vec3 n[9];
	for (int j = 0; j < 3; j++) {
		for (int i = 0; i < 3; i++) {
			vec2 uv = SCREEN_UV + vec2(float(i - 1), float(j - 1)) * px;
			d[j * 3 + i] = linear_depth(uv, INV_PROJECTION_MATRIX);
			n[j * 3 + i] = texture(normal_tex, uv).xyz * 2.0 - 1.0;
		}
	}
	float gx = -d[0] - 2.0 * d[3] - d[6] + d[2] + 2.0 * d[5] + d[8];
	float gy = -d[0] - 2.0 * d[1] - d[2] + d[6] + 2.0 * d[7] + d[8];
	float edge_d = sqrt(gx * gx + gy * gy) / max(d[4], 0.1);
	vec3 nx = -n[0] - 2.0 * n[3] - n[6] + n[2] + 2.0 * n[5] + n[8];
	vec3 ny = -n[0] - 2.0 * n[1] - n[2] + n[6] + 2.0 * n[7] + n[8];
	float edge_n = sqrt(dot(nx, nx) + dot(ny, ny));
	float e = max(smoothstep(depth_edge, depth_edge * 2.2, edge_d), 0.65 * smoothstep(normal_edge, normal_edge * 1.7, edge_n));
	if (!(e >= 0.03)) discard; // (NaN-safe)
	ALBEDO = ink.rgb;
	ALPHA = ink.a * e * clamp((fade - 0.35) / 0.4, 0.0, 1.0);
}
