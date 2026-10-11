// The stylised look, shared by the creature, rock and camp shaders. Every setting is a shader global (Settings > Graphics), so a
// change shows at once, in every material, with nothing reloaded:
//   cel_shading     on (1) or off (0): the master switch
//   cel_tex_blur    how soft textures are sampled (mip levels down; on creatures, how far their skin detail is smoothed away)
//   cel_tex_tones   how many tones a texture's brightness is divided into (0 or 1: not divided)
//   cel_light_tones how many tones the lit picture is divided into (by the full-screen pass, cel_post.gdshader; here only whether
//                   highlights are crisp)
//   cel_band_soft   how soft the edge between two tones is (0 hard, 1 a gentle ramp)
//   cel_light_flat  how flat the light is (0 the light's own contrast, 1 none): squeezed toward its own average, never brighter or darker
global uniform float cel_shading;
global uniform float cel_tex_blur;
global uniform float cel_tex_tones;
global uniform float cel_light_tones;
global uniform float cel_band_soft;
global uniform float cel_light_flat;

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

// How a surface takes a light by its angle, flattened. Flat light is the lit side all one level and the dark side another: 0.65 where the
// surface faces the light and 0 where it faces away, with a soft edge between. 0.65 is about the average cosine the lit surfaces show
// in the game (the lights stand where they light what you see, so the surfaces in view face them more squarely than a random one
// would: measured on the caves and the heroes), so moving toward it squeezes the light's contrast without making the picture brighter
// or darker. (Distance and shadow are kept.)
float cel_angle(float ndl) {
	float f = cel_shading * clamp(cel_light_flat, 0.0, 1.0);
	float lam = max(ndl, 0.0);
	if (f <= 0.0) return lam;
	float w = 0.08 + 0.3 * clamp(cel_band_soft, 0.0, 1.0);
	float two = 0.65 * smoothstep(-w, w, ndl);
	return mix(lam, two, f);
}

// The diffuse light term, from the surface's angle to the light (dot(NORMAL, LIGHT), unclamped) and the light's attenuation, flattened
// (cel_angle). (Its tones are made after the lighting, on the whole picture: cel_post.gdshader.)
float cel_light(float ndl, float atten) {
	return cel_angle(ndl) * atten;
}

// A highlight made crisp: a hard-edged spot instead of a smooth gloss.
float cel_spec(float sp, float gain) {
	// (flatter light, duller highlights: a flat-lit thing has no gloss to catch)
	sp *= 1.0 - cel_shading * clamp(cel_light_flat, 0.0, 1.0) * 0.85;
	if (cel_shading <= 0.0 || cel_light_tones < 1.5) return sp;
	float w = clamp(cel_band_soft, 0.02, 1.0) * 0.08;
	return mix(sp, smoothstep(0.4 - w, 0.4 + w, sp) * gain, cel_shading);
}
