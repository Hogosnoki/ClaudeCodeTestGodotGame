// A soft, lit cloud puff for props (spore clouds): alpha blended, softened against geometry.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert, specular_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;
instance uniform vec4 cloud_color : source_color = vec4(0.6, 0.4, 0.85, 0.3);
instance uniform float cloud_seed = 0.0;
instance uniform float cloud_glow = 0.3;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float vnoise(vec3 x) {
	vec3 i = floor(x); vec3 f = fract(x); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	vec3 sx = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx.x, INV_VIEW_MATRIX[1] * sx.y, INV_VIEW_MATRIX[2] * sx.z, MODEL_MATRIX[3]);
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float n = vnoise(vec3(uv * 2.0 + cloud_seed * 5.0, TIME * 0.3 + cloud_seed)) * 0.6 + vnoise(vec3(uv * 5.0 - cloud_seed, TIME * 0.5)) * 0.4;
	float a = smoothstep(1.0, 0.1, r + (n - 0.5) * 0.8) * (0.5 + 0.5 * n);
	float d = texture(depth_tex, SCREEN_UV).r;
	vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, d, 1.0);
	float soft = clamp((-v.z / v.w - (-VERTEX.z)) / 0.5, 0.0, 1.0);
	ALBEDO = cloud_color.rgb;
	EMISSION = cloud_color.rgb * cloud_glow;
	ALPHA = clamp(a * cloud_color.a * soft, 0.0, 1.0);
}
