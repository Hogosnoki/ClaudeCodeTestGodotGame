// The water's surface, seen at a grazing angle: rippling normals catch the lights, and what lies
// beneath shows through, refracted and absorbed with depth.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D ripple : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec3 shallow : source_color = vec3(0.1, 0.35, 0.5);
uniform vec3 deep : source_color = vec3(0.02, 0.08, 0.16);
uniform float absorb = 0.3;

varying vec3 v_wp;

void vertex() {
	v_wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += sin(v_wp.x * 0.48 + TIME * 2.0) * 0.1 + sin(v_wp.x * 1.13 - TIME * 1.3) * 0.07;
}

vec3 unpack_n(vec4 t) {
	vec2 xy = t.rg * 2.0 - 1.0;
	return vec3(xy, sqrt(max(0.0, 1.0 - dot(xy, xy))));
}

void fragment() {
	vec2 uv1 = v_wp.xz * 0.21 + vec2(TIME * 0.03, TIME * 0.017);
	vec2 uv2 = v_wp.xz * 0.37 - vec2(TIME * 0.021, -TIME * 0.029);
	vec3 n1 = unpack_n(texture(ripple, uv1));
	vec3 n2 = unpack_n(texture(ripple, uv2));
	vec3 wn = normalize(vec3(n1.x + n2.x, 2.2, n1.y + n2.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(wn, 0.0)).xyz);
	vec2 off = (n1.xy + n2.xy) * 0.012;
	float d = textureLod(depth_tex, SCREEN_UV + off, 0.0).r;
	vec4 ndc = vec4((SCREEN_UV + off) * 2.0 - 1.0, d, 1.0);
	vec4 view = INV_PROJECTION_MATRIX * ndc;
	view.xyz /= view.w;
	float dist = max(length(view.xyz) - length(VERTEX), 0.0);
	vec3 behind = textureLod(screen_tex, SCREEN_UV + off, 0.0).rgb;
	float k = 1.0 - exp(-dist * absorb);
	vec3 col = mix(behind * 0.8, deep, k);
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALBEDO = vec3(0.0);
	EMISSION = col * (1.0 - fres * 0.6) + shallow * fres * 0.2;
	ROUGHNESS = 0.06;
	SPECULAR = 0.8;
	METALLIC = 0.0;
	ALPHA = 1.0;
}
