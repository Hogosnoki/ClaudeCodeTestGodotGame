// Mist lying in the valleys and round the cave's foot: soft sheets of drifting noise, lavender in the dusk and warmer toward the
// sun, fading out where they meet the ground or the rock (so no hard line shows) and toward their own edges.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec3 mist_color : source_color = vec3(0.62, 0.55, 0.66);
uniform vec3 sun_color : source_color = vec3(1.0, 0.72, 0.55);
uniform vec3 sun_dir = vec3(-0.8, 0.06, -0.6);
uniform float density = 0.55;
uniform float scale = 0.05;
uniform float drift = 0.6;
uniform float soft = 6.0;
uniform float brightness = 1.0;
uniform sampler2D depth_tex : hint_depth_texture, filter_linear;

varying vec3 v_wp;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 x) {
	vec2 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) { float s = 0.0, a = 0.5; for (int k = 0; k < 5; k++) { s += a * vnoise(p); p = p * 2.07 + 3.7; a *= 0.5; } return s; }

void vertex() { v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	// (along the bank and up it: wisps, not stripes)
	vec2 uv = vec2(v_wp.x + v_wp.z, v_wp.y * 2.2) * scale + vec2(TIME * drift * 0.01, TIME * drift * 0.004);
	float n = fbm(uv) * 0.7 + fbm(uv * 3.1 - TIME * drift * 0.006) * 0.3;
	float a = smoothstep(0.35, 0.8, n) * density;
	// fade toward the sheet's own edges (its UVs run 0..1)
	vec2 e = min(UV, 1.0 - UV);
	a *= smoothstep(0.0, 0.25, e.x) * smoothstep(0.0, 0.5, e.y);
	// soft where it meets what's behind it
	float d = texture(depth_tex, SCREEN_UV).r;
	vec4 ndc = vec4(SCREEN_UV * 2.0 - 1.0, d, 1.0);
	vec4 view = INV_PROJECTION_MATRIX * ndc;
	view.xyz /= view.w;
	float behind = -view.z - (-VERTEX.z);
	a *= clamp(behind / soft, 0.0, 1.0);
	// (seen almost edge-on a sheet would show as a hard line: it fades out instead)
	a *= smoothstep(0.02, 0.18, abs(dot(NORMAL, VIEW)));
	// warmer looking toward the sun
	vec3 vd = normalize(v_wp - CAMERA_POSITION_WORLD);
	float sunward = pow(max(dot(vd, normalize(sun_dir)), 0.0), 3.0);
	ALBEDO = mix(mist_color, sun_color, sunward * 0.6) * brightness;
	ALPHA = a;
}
