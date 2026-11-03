// Leaves and needles: cards cut out by a generated leaf picture, two-sided, each card its own shade (the vertex colour), light
// coming through them from behind, stirring in the breeze (more at the ends of the boughs: UV2.x is how far out a card sits).
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform sampler2D leaf_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 color : source_color = vec3(0.16, 0.20, 0.08);
uniform float translucency = 0.7;
uniform float wind = 0.05;

void vertex() {
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float out_ = UV2.x;
	float s = sin(TIME * 1.6 + wp.x * 0.8 + wp.y * 0.5) * 0.6 + sin(TIME * 3.1 + wp.z * 1.7) * 0.4;
	VERTEX += vec3(s, s * 0.3, s * 0.6) * wind * out_;
}

void fragment() {
	vec4 t = texture(leaf_tex, UV);
	if (t.a < 0.5) discard;
	vec3 c = color * COLOR.rgb * (0.7 + 0.6 * t.r);
	ALBEDO = c;
	ROUGHNESS = 0.75;
	SPECULAR = 0.3;
	BACKLIGHT = c * translucency;
}
