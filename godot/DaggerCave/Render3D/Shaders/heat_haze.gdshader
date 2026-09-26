// Shimmering air over lava (and fire): bends what lies behind it, strongest at the bottom edge
// of the quad and fading to nothing at the top so there is no visible seam.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float strength = 0.006;

varying vec3 v_wp;

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// QuadMesh UV.y runs from 0 at the top to 1 at the bottom
	float k = clamp(UV.y, 0.0, 1.0);
	k *= k;
	vec2 n = vec2(texture(noise_tex, v_wp.xy * 0.35 + vec2(0.0, -TIME * 0.5)).r,
		texture(noise_tex, v_wp.xy * 0.35 + vec2(0.5, -TIME * 0.43)).r) - 0.5;
	ALBEDO = textureLod(screen_tex, SCREEN_UV + n * strength * k, 0.0).rgb;
	ALPHA = 1.0;
}
