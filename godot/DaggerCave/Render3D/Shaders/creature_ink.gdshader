// Ink outlines for creatures: a line around the creature's outline on the screen, and nowhere else.
// This pass is the creature's mesh drawn again (the next pass of its material), pushed out a few
// pixels along its normals so a band just outside the silhouette is covered. A pixel of that band
// is inked only where the scene behind it is much farther off than the creature itself is there:
// open air or distant rock around the figure. Over the creature's own body (and over its own
// parts: an arm in front of the chest) the scene is about as near as the creature, so there is no
// line, and no lines creep into folds or round limbs. The band is a fixed number of screen pixels
// wide, so the line is the same couple of pixels at any distance.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.015, 0.012, 0.02, 0.92);
/// How far the hull is pushed out along the normals, in screen pixels (at 720 lines; it scales with the
/// resolution, not with the camera's zoom or the creature's size): the line has one fixed width.
uniform float edge_px = 2.5;
/// How much farther (in world units) the scene must lie behind the creature to be inked against:
/// more than any gap between the creature's own limbs.
uniform float gap_min = 0.6;
// ghosts thin out into nothing below this rest-pose height (as in creature.gdshader)
uniform float ghost_below = -100.0;
uniform float ghost_fade = 0.3;

// shared with the creature's own pass: the same slots as in creature.gdshader
instance uniform float fade : instance_index(3) = 1.0;
instance uniform float dissolve : instance_index(4) = 0.0;
// an affliction recolours the line (poison green, fire orange, frost pale blue): alpha = how much
instance uniform vec4 ink_tint : source_color, instance_index(7) = vec4(0.0);

varying vec3 v_rest;

void vertex() {
	v_rest = vec3(UV.x, UV.y, UV2.x);
	// push the hull out in clip space by a fixed number of pixels
	vec4 clip = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	vec3 nv = MODELVIEW_NORMAL_MATRIX * NORMAL;
	vec2 dir = nv.xy;
	float l = length(dir);
	dir = l > 1e-4 ? dir / l : vec2(0.0);
	float px = max(1.0, VIEWPORT_SIZE.y / 720.0) * edge_px;
	clip.xy += dir * (px * 2.0 / VIEWPORT_SIZE) * clip.w;
	POSITION = clip;
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
	// this band pixel's own depth against the scene's there: only open background beyond it gets a line
	float self_d = -VERTEX.z;
	float scene_d = linear_depth(SCREEN_UV, INV_PROJECTION_MATRIX);
	float e = smoothstep(gap_min, gap_min * 1.6, scene_d - self_d);
	if (!(e >= 0.03)) discard; // (NaN-safe)
	ALBEDO = mix(ink.rgb, ink_tint.rgb, ink_tint.a);
	ALPHA = ink.a * e * clamp((fade - 0.35) / 0.4, 0.0, 1.0);
}
