// The stylised look, shared by the creature, rock and camp shaders. Every setting is a shader global (Settings > Graphics), so a
// change shows at once, in every material, with nothing reloaded:
//   cel_shading     on (1) or off (0): the master switch
//   cel_tex_blur    how soft textures are sampled (mip levels down; on creatures, how far their skin detail is smoothed away)
//   cel_tex_tones   how many tones a texture's brightness is divided into (0 or 1: not divided)
//   cel_light_bands how many bands the light falls in (0 or 1: a smooth falloff)
//   cel_band_soft   how soft the edge between two tones or bands is (0 hard, 1 a gentle ramp)
global uniform float cel_shading;
global uniform float cel_tex_blur;
global uniform float cel_tex_tones;
global uniform float cel_light_bands;
global uniform float cel_band_soft;

// The mip bias textures are sampled with.
float cel_bias() { return cel_shading * cel_tex_blur; }

// A value in 0..1 divided into n soft-edged steps (n < 1.5: returned as it is).
float cel_steps(float v, float n) {
	if (n < 1.5) return v;
	float w = clamp(cel_band_soft, 0.02, 1.0) * 0.5;
	float x = clamp(v, 0.0, 1.0) * n;
	return clamp((floor(x) + smoothstep(0.5 - w, 0.5 + w, fract(x))) / n, 0.0, 1.0);
}

// A colour's brightness divided into the texture tones (hue kept), measured in a perceptual scale.
vec3 cel_tone(vec3 c) {
	if (cel_shading <= 0.0 || cel_tex_tones < 1.5) return c;
	float L = dot(c, vec3(0.299, 0.587, 0.114)) + 1e-4;
	float s = sqrt(L);
	float t = cel_steps(s, cel_tex_tones);
	return mix(c, c / L * t * t, cel_shading);
}

// The diffuse light term: banded in a perceptual scale (a dim light keeps its brightness on average, only its steps show), with a
// floor so a shadowed side keeps a little of the light's colour.
float cel_light(float lit) {
	if (cel_shading <= 0.0 || cel_light_bands < 1.5) return lit;
	float b = cel_steps(sqrt(lit), cel_light_bands);
	return mix(lit, max(b * b, lit * 0.25), cel_shading);
}

// A highlight made crisp: a hard-edged spot instead of a smooth gloss.
float cel_spec(float sp, float gain) {
	if (cel_shading <= 0.0 || cel_light_bands < 1.5) return sp;
	float w = clamp(cel_band_soft, 0.02, 1.0) * 0.08;
	return mix(sp, smoothstep(0.4 - w, 0.4 + w, sp) * gain, cel_shading);
}
