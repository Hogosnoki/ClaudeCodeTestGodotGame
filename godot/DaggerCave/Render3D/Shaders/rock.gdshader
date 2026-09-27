// Cave rock: triplanar PBR from photographed CC0 textures, recoloured per biome.
// COLOR (vertex): r = how deep into the rock mass this camera-facing surface is,
//                 g = baked occlusion from the terrain field, b = slow material variation.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D a_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D a_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D a_arm : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D b_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D b_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D b_arm : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D g_arm : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D m_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D m_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D s_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D s_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D s_arm : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D mask_noise : filter_linear_mipmap, repeat_enable;

uniform vec3 tint_edge : source_color = vec3(0.30, 0.25, 0.22);
uniform vec3 tint_deep : source_color = vec3(0.08, 0.06, 0.06);
uniform vec3 moss_tint : source_color = vec3(0.32, 0.52, 0.22);
uniform vec3 glow_color : source_color = vec3(0.45, 0.9, 1.0);
uniform float tint_strength = 0.7;
uniform float brightness = 1.0;
uniform float scale_a = 0.3;
uniform float scale_b = 0.19;
uniform float scale_g = 0.42;
uniform float scale_m = 0.35;
uniform float scale_s = 0.34;
uniform float normal_strength = 1.0;
uniform float moss_amount = 0.5;
uniform float ground_amount = 0.85;
// 0 none, 1 masonry, 2 snow and ice, 3 basalt with magma veins, 4 crystal sparkle, 5 fungal glow
uniform int special_mode = 0;
uniform float special_amount = 1.0;
uniform float liquid_y = -100000.0;
uniform int liquid_mode = 0; // 0 none, 1 water, 2 lava
uniform vec3 liquid_glow : source_color = vec3(1.0, 0.42, 0.08);
uniform vec3 water_light : source_color = vec3(0.45, 0.8, 1.0);
uniform float cap_darkness = 0.6;
// how much darker the back wall of the tunnels is than the surfaces at the play plane
uniform float back_dim = 0.38;
// the front lip of floors and ceilings (metres toward the camera): beyond it is the cut rock face
uniform float lip_z = 1.15;
// A soft light from the viewer's side that only reaches the rock face toward the camera, so the
// solid rock shows its relief everywhere (like the 2D game's always-visible rock) without
// lighting the tunnels behind the play plane.
uniform vec3 face_fill : source_color = vec3(0.55, 0.6, 0.75);
uniform float face_fill_energy = 0.12;
uniform vec3 face_fill_dir = vec3(-0.35, 0.55, 0.76);
// debug: 1 = colour by surface kind (red camera-facing rock, blue back wall, green floors, yellow walls/ceilings)
uniform int debug_view = 0;

varying vec3 v_wpos;
varying vec3 v_wnrm;

void vertex() {
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wnrm = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

vec3 unpack_n(vec4 t) {
	vec2 xy = t.rg * 2.0 - 1.0;
	return vec3(xy, sqrt(max(0.0, 1.0 - dot(xy, xy))));
}

// Triplanar set-up. Each projection gets an upright, non-mirrored tangent frame so normal
// maps light the right way on every face.
struct Tri { vec2 ux; vec2 uy; vec2 uz; vec3 w; vec3 s; };

Tri tri_setup(vec3 p, vec3 n) {
	Tri t;
	vec3 s = vec3(n.x < 0.0 ? -1.0 : 1.0, n.y < 0.0 ? -1.0 : 1.0, n.z < 0.0 ? -1.0 : 1.0);
	t.s = s;
	t.ux = vec2(-s.x * p.z, -p.y);
	t.uy = vec2(p.x, s.y * p.z);
	t.uz = vec2(s.z * p.x, -p.y);
	vec3 w = pow(abs(n), vec3(5.0));
	w = max(w - 0.02, 0.0);
	t.w = w / max(1e-4, w.x + w.y + w.z);
	return t;
}

vec4 tri_col(sampler2D tex, Tri t, float sc) {
	vec4 c = vec4(0.0);
	if (t.w.x > 0.0) c += texture(tex, t.ux * sc) * t.w.x;
	if (t.w.y > 0.0) c += texture(tex, t.uy * sc) * t.w.y;
	if (t.w.z > 0.0) c += texture(tex, t.uz * sc) * t.w.z;
	return c;
}

// World-space normal offset from a tangent-space map (added to the geometric normal).
vec3 tri_nrm(sampler2D tex, Tri t, float sc) {
	vec3 d = vec3(0.0);
	if (t.w.x > 0.0) { vec3 tn = unpack_n(texture(tex, t.ux * sc)); d += (vec3(0.0, 0.0, -t.s.x) * tn.x + vec3(0.0, 1.0, 0.0) * tn.y) * t.w.x; }
	if (t.w.y > 0.0) { vec3 tn = unpack_n(texture(tex, t.uy * sc)); d += (vec3(1.0, 0.0, 0.0) * tn.x + vec3(0.0, 0.0, -t.s.y) * tn.y) * t.w.y; }
	if (t.w.z > 0.0) { vec3 tn = unpack_n(texture(tex, t.uz * sc)); d += (vec3(t.s.z, 0.0, 0.0) * tn.x + vec3(0.0, 1.0, 0.0) * tn.y) * t.w.z; }
	return d;
}

float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

// A colour's hue and saturation at unit brightness (so photos keep their own brightness).
vec3 hue(vec3 c) { return c / max(luma(c), 1e-3); }

float caustic(vec2 uv, float time) {
	vec2 p = mod(uv * 6.28318, 6.28318) - 250.0;
	vec2 i = p;
	float c = 1.0;
	float inten = 0.005;
	for (int n = 0; n < 4; n++) {
		float t = time * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(t - i.x) + sin(t + i.y), sin(t - i.y) + cos(t + i.x));
		c += 1.0 / length(vec2(p.x / (sin(i.x + t) / inten), p.y / (cos(i.y + t) / inten)));
	}
	c /= 4.0;
	c = 1.17 - pow(c, 1.4);
	return pow(abs(c), 8.0);
}

void fragment() {
	vec3 p = v_wpos;
	vec3 n = normalize(v_wnrm);
	Tri t = tri_setup(p, n);
	float capness = COLOR.r;
	float bake_ao = COLOR.g;
	float variation = COLOR.b;
	float big = texture(mask_noise, p.xy * 0.045 + p.z * 0.02).r;
	float fine = texture(mask_noise, p.xz * 0.21 + p.y * 0.07).r;

	// ---- base rock: two photographed rocks, mixed by a slow variation mask
	float mb = smoothstep(0.38, 0.62, variation * 0.7 + big * 0.5 - 0.1);
	vec3 alb = vec3(0.0);
	vec3 arm = vec3(0.0);
	vec3 dn = vec3(0.0);
	if (mb < 0.999) {
		alb += tri_col(a_alb, t, scale_a).rgb * (1.0 - mb);
		arm += tri_col(a_arm, t, scale_a).rgb * (1.0 - mb);
		dn += tri_nrm(a_nrm, t, scale_a) * (1.0 - mb);
	}
	if (mb > 0.001) {
		alb += tri_col(b_alb, t, scale_b).rgb * mb;
		arm += tri_col(b_arm, t, scale_b).rgb * mb;
		dn += tri_nrm(b_nrm, t, scale_b) * mb;
	}

	// ---- biome colour: keep the photo's detail, take the biome's hue
	vec3 tint = mix(hue(tint_edge), hue(tint_deep), smoothstep(0.0, 1.0, capness));
	float l = luma(alb);
	alb = mix(alb, l * tint, tint_strength);

	// ---- special surfaces
	if (special_mode == 1) {
		// masonry on the walls near the open space (the ruins)
		float walls = (1.0 - smoothstep(0.55, 0.85, abs(n.y))) * (1.0 - smoothstep(0.35, 0.8, capness)) * special_amount;
		walls *= smoothstep(0.25, 0.45, big + 0.2);
		if (walls > 0.001) {
			vec3 sa = tri_col(s_alb, t, scale_s).rgb;
			sa = mix(sa, luma(sa) * tint, 0.5) * 1.1;
			alb = mix(alb, sa, walls);
			arm = mix(arm, tri_col(s_arm, t, scale_s).rgb, walls);
			dn = mix(dn, tri_nrm(s_nrm, t, scale_s), walls);
		}
	} else if (special_mode == 3) {
		// basalt instead of the second rock
		vec3 sa = tri_col(s_alb, t, scale_s).rgb;
		float k = 0.65 * special_amount;
		alb = mix(alb, mix(sa, luma(sa) * tint, 0.4), k);
		arm = mix(arm, tri_col(s_arm, t, scale_s).rgb, k);
		dn = mix(dn, tri_nrm(s_nrm, t, scale_s), k);
	} else if (special_mode == 6) {
		// root mats: dark fibrous strands running down the rock (the root-choked tunnels)
		float fib = texture(mask_noise, vec2(p.x * 0.55 + p.z * 0.3, p.y * 0.05)).r;
		float strand = 1.0 - smoothstep(0.0, 0.035, abs(fract(fib * 7.0) - 0.5) - 0.44);
		float mat = smoothstep(0.45, 0.65, texture(mask_noise, p.xy * 0.03 + vec2(0.13, 0.57)).r) * (1.0 - capness * 0.6);
		float roots_k = strand * mat * special_amount;
		alb = mix(alb, vec3(0.2, 0.14, 0.08) * (0.8 + 0.4 * fib), roots_k * 0.85);
		arm.g = mix(arm.g, 0.95, roots_k);
	} else if (special_mode == 7) {
		// sediment laid down in layers, with the bones of old things caught in it (the fossil graveyards)
		float wob = texture(mask_noise, p.xz * 0.02).r * 2.0;
		float layer = 0.5 + 0.5 * sin(p.y * 2.3 + wob * 3.0);
		alb *= mix(0.78, 1.12, smoothstep(0.2, 0.8, layer));
		float bone = smoothstep(0.86, 0.91, texture(mask_noise, vec2(p.x * 0.35, p.y * 1.1) + p.z * 0.2).r) * (1.0 - capness * 0.5);
		alb = mix(alb, vec3(0.78, 0.72, 0.6), bone * special_amount);
		arm.g = mix(arm.g, 0.6, bone);
	}

	// ---- floors: gravel and grit on anything you could stand on
	float up = smoothstep(0.42, 0.78, n.y);
	float ground = up * ground_amount * (1.0 - capness * 0.7);
	if (ground > 0.001) {
		vec2 guv = p.xz * scale_g;
		vec3 ga = texture(g_alb, guv).rgb;
		ga = mix(ga, luma(ga) * tint, tint_strength * 0.8);
		alb = mix(alb, ga, ground);
		arm = mix(arm, texture(g_arm, guv).rgb, ground);
		vec3 tn = unpack_n(texture(g_nrm, guv));
		dn = mix(dn, vec3(tn.x, 0.0, -tn.y), ground);
	}

	// ---- moss and growth on dry walkable ground (green means you can walk it)
	float wet_line = liquid_mode == 1 ? smoothstep(liquid_y + 0.8, liquid_y - 0.3, p.y) : 0.0;
	float moss = smoothstep(0.55, 0.8, n.y) * moss_amount * smoothstep(0.3, 0.55, fine * 0.6 + big * 0.6) * (1.0 - wet_line);
	moss = clamp(moss * 1.6, 0.0, 1.0) * (1.0 - capness);
	if (moss > 0.001) {
		vec2 muv = p.xz * scale_m;
		vec3 ma = texture(m_alb, muv).rgb;
		ma = luma(ma) * hue(moss_tint) * 1.25;
		alb = mix(alb, ma, moss);
		arm.g = mix(arm.g, 0.9, moss);
		vec3 tn = unpack_n(texture(m_nrm, muv));
		dn = mix(dn, vec3(tn.x, 0.0, -tn.y), moss);
	}

	float rough = clamp(arm.g * 1.05, 0.25, 1.0);
	float spec = 0.45;
	vec3 emit = vec3(0.0);

	if (special_mode == 2) {
		// snow on the tops, a glaze of ice everywhere else
		float snow = smoothstep(0.5, 0.75, n.y) * smoothstep(0.35, 0.55, fine + big * 0.4) * special_amount;
		vec2 suv = p.xz * scale_s;
		vec3 sa = texture(s_alb, suv).rgb * vec3(0.9, 0.96, 1.05) * 1.25;
		alb = mix(alb, sa, snow);
		rough = mix(rough, 0.55, snow);
		float glaze = (1.0 - snow) * 0.7 * special_amount;
		alb = mix(alb, alb * vec3(0.75, 0.9, 1.1) + vec3(0.02, 0.05, 0.08), glaze);
		rough = mix(rough, 0.12, glaze * smoothstep(0.3, 0.7, fine));
		spec = mix(spec, 0.7, glaze);
	} else if (special_mode == 3) {
		// magma veins: a few thin glowing seams in patches of the rock, hotter near the lava
		float v = texture(mask_noise, p.xy * 0.04 + p.z * 0.025).r;
		float seam = 1.0 - smoothstep(0.0, 0.01, abs(v - 0.5));
		float patch = smoothstep(0.56, 0.7, texture(mask_noise, p.xy * 0.017 + vec2(0.31, 0.77)).r);
		seam *= patch * (1.0 - capness * 0.7);
		float heat = liquid_mode == 2 ? exp(-max(p.y - liquid_y, 0.0) / 9.0) : 0.3;
		float pulse = 0.75 + 0.25 * sin(TIME * 1.3 + p.x * 0.7 + p.y * 0.4);
		emit += liquid_glow * seam * pulse * 3.0 * (0.3 + heat) * special_amount;
		alb *= mix(1.0, 0.5, seam);
	} else if (special_mode == 4) {
		// crystal flecks: tiny mirror-like facets that catch the light
		float fleck = smoothstep(0.82, 0.95, texture(mask_noise, p.xy * 1.7 + p.z * 0.9).r);
		rough = mix(rough, 0.08, fleck);
		spec = mix(spec, 0.9, fleck);
		emit += glow_color * fleck * 0.18 * special_amount * (1.0 - capness);
	} else if (special_mode == 6) {
		// a few pale glowing specks in the root mats
		float dots = smoothstep(0.9, 0.95, texture(mask_noise, p.xz * 1.1 + p.y * 0.4).r);
		float breathe = 0.6 + 0.4 * sin(TIME * 0.7 + big * 9.0);
		emit += glow_color * dots * max(moss, up * 0.3) * breathe * 0.8 * special_amount;
	} else if (special_mode == 5) {
		// bioluminescent spots in the growth
		float dots = smoothstep(0.86, 0.93, texture(mask_noise, p.xz * 0.9 + p.y * 0.3).r);
		float breathe = 0.6 + 0.4 * sin(TIME * 0.8 + big * 12.0);
		emit += glow_color * dots * max(moss, up * 0.4) * breathe * 1.4 * special_amount;
	}

	// ---- liquids: a wet band at the waterline, dark glossy rock below, caustics; heat over lava
	if (liquid_mode == 1) {
		float below = smoothstep(liquid_y + 0.15, liquid_y - 0.4, p.y);
		float wet = max(wet_line, below);
		alb *= mix(1.0, 0.5, wet);
		rough = mix(rough, 0.22, wet);
		// caustics only inside the flooded tunnels, not on the cut face toward the camera
		float inside = 1.0 - smoothstep(lip_z - 0.4, lip_z + 0.3, p.z);
		if (below * inside > 0.0) {
			float depth = clamp((liquid_y - p.y) / 18.0, 0.0, 1.0);
			float c = caustic(p.xz * 0.21 + p.y * 0.07 + vec2(p.y * 0.05, 0.0), TIME * 0.45);
			float facing = 0.35 + 0.65 * max(n.y, 0.0);
			emit += water_light * c * facing * below * inside * (1.0 - depth) * 0.22;
		}
	} else if (liquid_mode == 2) {
		float h = max(p.y - liquid_y, 0.0);
		float heat = exp(-h / 3.0) * (0.55 + 0.45 * max(-n.y, 0.0) + 0.3 * (1.0 - abs(n.y)));
		float flick = 0.85 + 0.15 * sin(TIME * 2.3 + p.x * 0.9);
		emit += liquid_glow * heat * flick * 0.55;
		alb *= mix(1.0, 0.6, exp(-h / 1.2));
	}

	// ---- the rock mass facing the camera recedes into darkness, like the 2D game's deep rock
	alb *= mix(1.0, 1.0 - cap_darkness, smoothstep(0.0, 1.0, capness));
	// ...and the back of the tunnels sits back, dimmer than the surfaces you walk on
	alb *= mix(1.0, back_dim, smoothstep(-0.8, -4.0, p.z));
	alb *= mix(0.55, 1.0, bake_ao) * brightness;
	if (debug_view == 1) {
		vec3 k = vec3(0.9, 0.85, 0.2);
		if (capness > 0.25 || (p.z > lip_z + 0.2 && n.z > 0.5)) k = vec3(0.85, 0.2, 0.2);
		else if (p.z < -1.0 && n.z > 0.35) k = vec3(0.2, 0.35, 0.9);
		else if (n.y > 0.5) k = vec3(0.25, 0.85, 0.3);
		alb = k * (0.5 + 0.5 * bake_ao);
		emit = vec3(0.0);
		dn = vec3(0.0);
	} else if (debug_view == 2) {
		// the triplanar projections' weights: red side-on, green top-down, blue front-on
		alb = vec3(0.0);
		emit = t.w * 0.7;
		dn = vec3(0.0);
	}

	vec3 wn = normalize(n + dn * normal_strength);
	// the viewer-side fill on the camera-facing rock (relief from the detail normal)
	float face = smoothstep(lip_z - 0.3, lip_z + 0.5, p.z) * smoothstep(0.15, 0.65, n.z);
	if (face > 0.0 && debug_view == 0) {
		float lambert = max(dot(wn, normalize(face_fill_dir)), 0.0);
		emit += alb * face_fill * face_fill_energy * face * (0.25 + 0.75 * lambert);
	}
	ALBEDO = clamp(alb, 0.0, 1.0);
	ROUGHNESS = rough;
	SPECULAR = spec;
	METALLIC = 0.0;
	AO = clamp(arm.r * bake_ao, 0.0, 1.0);
	AO_LIGHT_AFFECT = 0.35;
	EMISSION = emit;
	NORMAL = normalize((VIEW_MATRIX * vec4(wn, 0.0)).xyz);
}
