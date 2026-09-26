// The flooded part of the cave seen through its cut face: light is absorbed by how much water
// lies behind each pixel (from the depth buffer), everything wobbles a little, and an animated
// bright waterline marks the surface.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform vec3 shallow : source_color = vec3(0.1, 0.35, 0.5);
uniform vec3 deep : source_color = vec3(0.02, 0.08, 0.16);
uniform vec3 line_color : source_color = vec3(0.6, 0.9, 1.0);
uniform float absorb = 0.16;
uniform float surface_y = 0.0;
uniform float tint = 0.45;

varying vec3 v_wp;

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float wave(float x, float t) {
	return sin(x * 0.48 + t * 2.0) * 0.1 + sin(x * 1.13 - t * 1.3) * 0.07;
}

void fragment() {
	float t = TIME;
	float sy = surface_y + wave(v_wp.x, t);
	float below = sy - v_wp.y;
	if (below < -0.25) discard;
	// refraction wobble
	vec2 wob = vec2(sin(t * 1.3 + v_wp.y * 2.3 + v_wp.x * 0.7), cos(t * 1.1 + v_wp.x * 1.9 + v_wp.y)) * 0.0025;
	vec2 uv = SCREEN_UV + wob * step(0.0, below);
	float d = textureLod(depth_tex, uv, 0.0).r;
	vec4 ndc = vec4(uv * 2.0 - 1.0, d, 1.0);
	vec4 view = INV_PROJECTION_MATRIX * ndc;
	view.xyz /= view.w;
	float dist = max(length(view.xyz) - length(VERTEX), 0.0);
	vec3 behind = textureLod(screen_tex, uv, 0.0).rgb;
	vec3 water = mix(shallow, deep, clamp(below / 30.0, 0.0, 1.0));
	float k = 1.0 - exp(-dist * absorb);
	vec3 col = behind * mix(vec3(1.0), water / max(max(water.r, water.g), max(water.b, 1e-3)), tint);
	col = mix(col, water * 0.6, k);
	// the waterline: a bright animated seam with a soft glow under it
	float line = 1.0 - smoothstep(0.0, 0.06, abs(below));
	float glow = exp(-max(below, 0.0) * 1.8) * 0.25;
	col += line_color * (line * 2.2 + glow * step(0.0, below));
	if (below < 0.0) {
		// just above the seam: only the line itself
		ALBEDO = line_color * 2.0;
		ALPHA = line;
	} else {
		ALBEDO = col;
		ALPHA = 1.0;
	}
}
