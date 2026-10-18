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

// Debug-mode tuning (DebugTuning.cs; Settings > Debug after typing "debug" at the character select): brightness and contrast of the
// foreground ground, the background ground and the characters, and the dirt's texture dials. All 1 (or the defaults) normally.
global uniform float dbg_fg_bright;
global uniform float dbg_fg_contrast;
global uniform float dbg_bg_bright;
global uniform float dbg_bg_contrast;
global uniform float dbg_obj_bright;
global uniform float dbg_obj_contrast;
global uniform float dbg_obj_r;
global uniform float dbg_obj_g;
global uniform float dbg_obj_b;
global uniform float dbg_m_dirt_r;
global uniform float dbg_m_dirt_g;
global uniform float dbg_m_dirt_b;
global uniform float dbg_m_grass_r;
global uniform float dbg_m_grass_g;
global uniform float dbg_m_grass_b;
global uniform float dbg_m_stone_r;
global uniform float dbg_m_stone_g;
global uniform float dbg_m_stone_b;
global uniform float dbg_m_snow_r;
global uniform float dbg_m_snow_g;
global uniform float dbg_m_snow_b;
global uniform float dbg_poly_smooth;
global uniform float dbg_trans_soft;
global uniform float dbg_bg_fade;
global uniform float dbg_crevice_soft;
global uniform float dbg_wall_gain;
global uniform float dbg_back_dirt;
global uniform float dbg_grain_amount;
global uniform float dbg_lump_amount;
global uniform float dbg_crevice_amount;
global uniform float dbg_stone_size;
global uniform float dbg_facet_shade;
global uniform float dbg_grass_lean;
global uniform float dbg_ground_up;

// A colour's brightness and contrast. Contrast is about the AVERAGE colour of what is being drawn (mean, linear), not about grey: at 0 every
// colour becomes that average, a flat wash of the thing's own colour; at 1 nothing changes; above 1 the colours spread away from the average.
// It is made in the display's perceptual scale. Brightness multiplies on top, and then the red, green and blue gains (1 = unchanged).
vec3 dbg_adjust(vec3 c, float bright, float contrast, vec3 mean, vec3 rgb) {
	vec3 s = pow(max(c, vec3(0.0)), vec3(1.0 / 2.2));
	vec3 m = pow(max(mean, vec3(0.0)), vec3(1.0 / 2.2));
	s = clamp(m + (s - m) * contrast, 0.0, 1.0);
	return pow(s, vec3(2.2)) * bright * rgb;
}

vec3 dbg_dirt_rgb() { return vec3(dbg_m_dirt_r, dbg_m_dirt_g, dbg_m_dirt_b); }
vec3 dbg_grass_rgb() { return vec3(dbg_m_grass_r, dbg_m_grass_g, dbg_m_grass_b); }
vec3 dbg_stone_rgb() { return vec3(dbg_m_stone_r, dbg_m_stone_g, dbg_m_stone_b); }
vec3 dbg_snow_rgb() { return vec3(dbg_m_snow_r, dbg_m_snow_g, dbg_m_snow_b); }
vec3 dbg_obj_rgb() { return vec3(dbg_obj_r, dbg_obj_g, dbg_obj_b); }

// The ground at depth z (metres; the play plane is 0, behind it negative): its foreground adjustment fading into its background one
// from the play plane back to 3 m behind it (the same fade the back dirt's darkness takes).
// (each with the average colour of the ground there: mean_fg at the play plane, mean_bg behind it)
vec3 dbg_ground(vec3 c, float z, vec3 mean_fg, vec3 mean_bg) {
	float k = smoothstep(-0.2, -0.2 - dbg_bg_fade, z);
	return mix(dbg_adjust(c, dbg_fg_bright, dbg_fg_contrast, mean_fg, vec3(1.0)), dbg_adjust(c, dbg_bg_bright, dbg_bg_contrast, mean_bg, vec3(1.0)), k);
}

// The mip bias textures are sampled with.
float cel_bias() { return cel_shading * cel_tex_blur; }

// Detail generated in a shader (noise, grain, flecks) follows the texture blur as a sampled texture does: smoothed toward its
// average, none at 0 and nearly all at 4 (as the creatures' skin detail is). v is the detail, mean its average value.
float cel_soft() { return cel_shading * clamp(cel_tex_blur / 4.0, 0.0, 1.0); }
float cel_detail(float v, float mean) { return mix(v, mean, cel_soft() * 0.9); }

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
