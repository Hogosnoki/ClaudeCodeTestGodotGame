// The lighting tones: the lit 3D picture (all the lights done, before any 2D is drawn over it) blurred by cel_light_blur mip levels,
// then its brightness divided into cel_light_tones tones, the way a texture's is (cel.gdshaderinc, cel_tone): hue kept, steps even to the
// eye (a square-root scale), each tone the middle of its range so the picture is no brighter or darker on average, and the darkest
// shades left alone so black stays black. The edges between tones are as soft as cel_band_soft says. See CelPost.cs.
shader_type canvas_item;
render_mode unshaded;

global uniform float cel_shading;
global uniform float cel_light_blur;
global uniform float cel_light_tones;
global uniform float cel_band_soft;
// debug mode: the whole picture's red, green and blue gains (after the tones; 1 = unchanged)
global uniform float dbg_scr_r;
global uniform float dbg_scr_g;
global uniform float dbg_scr_b;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec3 c = textureLod(screen_tex, SCREEN_UV, cel_light_blur * cel_shading).rgb;
	float n = cel_light_tones;
	if (cel_shading > 0.0 && n >= 1.5) {
		float L = dot(c, vec3(0.299, 0.587, 0.114)) + 1e-4;
		float s = sqrt(L);
		float w = clamp(cel_band_soft, 0.02, 1.0) * 0.5;
		float y = s * n - 0.5;
		float t = (floor(y) + smoothstep(0.5 - w, 0.5 + w, fract(y)) + 0.5) / n;
		t = s < 0.5 / n ? s : t;
		c = mix(c, c / L * t * t, cel_shading);
	}
	c *= vec3(dbg_scr_r, dbg_scr_g, dbg_scr_b);
	COLOR = vec4(c, 1.0);
}
