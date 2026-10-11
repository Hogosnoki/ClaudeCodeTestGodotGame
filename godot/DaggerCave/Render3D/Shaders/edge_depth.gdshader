// The border round every 3D thing in view: a thin ink line, laid over the whole view. (Not between the colours on one surface: grass
// meets dirt with no line.) A pixel is on a border
//  * where the scene steps away behind it (an outline): along some direction one side is farther than here by more than a slope would
//    explain, relative to the distance, and the other side isn't nearer; the line sits on the nearer thing's side of the step;
//  * or where the surface folds sharply (a hard edge: a block's corner, a ledge's lip): the surface's direction a pixel or two along
//    differs from here by more than crease_deg.
// The line is ink_terrain_px screen pixels wide (at 720 lines: it scales with the resolution); 0 turns it off. See EdgeInk.cs.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;

global uniform float ink_terrain_px;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D normal_tex : hint_normal_roughness_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.02, 0.018, 0.022, 0.9);
// how big a step (as a share of the distance) counts as an outline
uniform float step_share = 0.06;
// how sharp a fold (degrees between the two sides) counts as a hard edge
uniform float crease_deg = 50.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float lin(vec2 uv, mat4 inv_proj) {
	float z = texture(depth_tex, uv).r;
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, z, 1.0);
	float w = abs(v.w) < 1e-5 ? 1e-5 : v.w;
	return clamp(-v.z / w, 0.0, 5000.0);
}

void fragment() {
	if (ink_terrain_px <= 0.01) discard;
	vec2 uv = SCREEN_UV;
	vec2 texel = 1.0 / VIEWPORT_SIZE;
	float px = max(1.0, VIEWPORT_SIZE.y / 720.0) * ink_terrain_px;
	float d = lin(uv, INV_PROJECTION_MATRIX);
	// (open sky has no outline of its own)
	if (d > 4000.0) discard;
	// the step can be up to px pixels away (a wider line); a part of a pixel at the far end of a fractional width
	float reach = ceil(px);
	float cover = 0.0;
	vec3 nc = texture(normal_tex, uv).xyz * 2.0 - 1.0;
	float crease = cos(radians(crease_deg));
	for (int k = 0; k < 4; k++) {
		vec2 dir = k == 0 ? vec2(1.0, 0.0) : k == 1 ? vec2(0.0, 1.0) : k == 2 ? vec2(0.7071, 0.7071) : vec2(0.7071, -0.7071);
		for (float r = 1.0; r <= reach; r += 1.0) {
			vec2 o = dir * r * texel;
			float a = lin(uv + o, INV_PROJECTION_MATRIX);
			float b = lin(uv - o, INV_PROJECTION_MATRIX);
			// one side steps far behind, the other doesn't step nearer (so this pixel is the near side's edge, not a slope)
			float behind = max(a, b) - d;
			float nearer = d - min(a, b);
			if (behind > d * step_share && nearer < behind * 0.5) {
				cover = max(cover, clamp(px - r + 1.0, 0.0, 1.0));
			}
			// (a fold: looked for on one side only, so the line is drawn once, not on both faces; and only on one continuous surface)
			if (abs(a - d) < d * step_share) {
				vec3 na = texture(normal_tex, uv + o).xyz * 2.0 - 1.0;
				if (dot(normalize(na), normalize(nc)) < crease) cover = max(cover, clamp(px - r + 1.0, 0.0, 1.0));
			}
		}
	}
	if (cover <= 0.0) discard;
	ALBEDO = ink.rgb;
	ALPHA = ink.a * cover;
}
