// Alpha-blended, lit particles: smoke and dust puffs (shape 0) and liquid droplets (shape 1).
// They fade softly where they meet geometry. INSTANCE_CUSTOM: x = shape, w = seed.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert, specular_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;
varying vec4 v_custom;
varying vec4 v_col;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1.0, 0.0, 0.0)), f.x),
	               mix(hash3(i + vec3(0.0, 1.0, 0.0)), hash3(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0.0, 0.0, 1.0)), hash3(i + vec3(1.0, 0.0, 1.0)), f.x),
	               mix(hash3(i + vec3(0.0, 1.0, 1.0)), hash3(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}

void vertex() {
	v_custom = INSTANCE_CUSTOM;
	v_col = COLOR;
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float a;
	if (v_custom.x < 0.5) {
		float r = length(uv);
		float seed = v_custom.w * 37.0;
		float n = vnoise(vec3(uv * 2.2 + seed, TIME * 0.35 + seed)) * 0.65 + vnoise(vec3(uv * 5.0 - seed, TIME * 0.6)) * 0.35;
		a = smoothstep(1.0, 0.15, r + (n - 0.5) * 0.7);
		a *= 0.55 + 0.45 * n;
	} else {
		a = exp(-uv.y * uv.y * 9.0) * smoothstep(-1.0, 0.4, uv.x) * (1.0 - smoothstep(0.75, 1.0, uv.x));
	}
	// soft edges against the rock
	float d = texture(depth_tex, SCREEN_UV).r;
	vec4 ndc = vec4(SCREEN_UV * 2.0 - 1.0, d, 1.0);
	vec4 view = INV_PROJECTION_MATRIX * ndc;
	float sceneZ = -view.z / view.w;
	float soft = clamp((sceneZ - (-VERTEX.z)) / 0.35, 0.0, 1.0);
	ALBEDO = v_col.rgb;
	ALPHA = clamp(a * v_col.a * soft, 0.0, 1.0);
	NORMAL = normalize(vec3(uv * 0.6, 1.0));
}
