// Sculpted creatures. Vertex data: COLOR = albedo + roughness; CUSTOM0 = metallic, subsurface,
// glow, detail kind / 8; UV, UV2.x = rest-pose position, so surface detail sticks to the skin
// while the skeleton moves it.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, depth_draw_opaque, cull_back;

uniform vec3 eye_color : source_color = vec3(1.0, 0.25, 0.08);
uniform float eye_energy = 7.0;
uniform vec3 glow_color : source_color = vec3(1.0, 0.5, 0.15);
uniform float glow_energy = 3.0;
uniform vec3 rim_color : source_color = vec3(0.55, 0.65, 0.85);
uniform float rim_energy = 0.35;
uniform float detail_scale = 22.0;
uniform float detail_strength = 0.5;
uniform float veins = 0.0;
uniform vec3 vein_color : source_color = vec3(0.25, 0.02, 0.03);
uniform float wet = 0.0;
uniform vec3 burn_color : source_color = vec3(1.0, 0.45, 0.1);

instance uniform float flash = 0.0;
instance uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
instance uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 0.0);
instance uniform float fade = 1.0;
instance uniform float dissolve = 0.0;

varying vec3 v_rest;
varying vec4 v_mat;

void vertex() {
	v_rest = vec3(UV.x, UV.y, UV2.x);
	v_mat = CUSTOM0;
}

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

float fbm(vec3 p) {
	float s = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		s += vnoise(p) * a;
		p = p * 2.07 + vec3(1.7, 9.2, 4.1);
		a *= 0.5;
	}
	return s;
}

// distance between the nearest two cell points: 0 on the borders of scales / plates
float cells(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	float d1 = 8.0;
	float d2 = 8.0;
	for (int z = -1; z <= 1; z++)
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec3 g = vec3(float(x), float(y), float(z));
		vec3 o = vec3(hash3(i + g), hash3(i + g + 11.1), hash3(i + g + 23.7));
		float d = length(g + o - f);
		if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
	}
	return d2 - d1;
}

// bump mapping from screen-space derivatives of a height (no UVs needed)
vec3 bump(vec3 n, vec3 pos, float h) {
	vec3 dpdx = dFdx(pos);
	vec3 dpdy = dFdy(pos);
	float dhdx = dFdx(h);
	float dhdy = dFdy(h);
	vec3 r1 = cross(dpdy, n);
	vec3 r2 = cross(n, dpdx);
	float det = dot(dpdx, r1);
	vec3 g = sign(det) * (dhdx * r1 + dhdy * r2);
	return normalize(abs(det) * n - g);
}

float bayer4(vec2 p) {
	ivec2 i = ivec2(mod(p, 4.0));
	int m[16] = int[](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
	return (float(m[i.y * 4 + i.x]) + 0.5) / 16.0;
}

void fragment() {
	// fading out (dithered, stays in the opaque pass)
	if (fade < 0.999 && fade < bayer4(FRAGCOORD.xy)) discard;
	vec3 p = v_rest;
	float kind = floor(v_mat.w * 8.0 + 0.5);
	float glow = v_mat.z;

	// burning away on death: noise threshold with a glowing rim
	float burn = 0.0;
	if (dissolve > 0.0) {
		float n = fbm(p * 7.0);
		if (n < dissolve * 1.1 - 0.05) discard;
		burn = 1.0 - smoothstep(0.0, 0.07, n - (dissolve * 1.1 - 0.05));
	}

	// part colours are authored in sRGB
	vec3 alb = pow(COLOR.rgb, vec3(2.2));
	float rough = COLOR.a;
	float sc = detail_scale;
	float h = 0.0;
	if (kind < 0.5) {
		h = vnoise(p * sc * 0.6) * 0.15;
	} else if (kind < 1.5) {
		// skin: pores and wrinkles
		float w = 1.0 - abs(vnoise(p * sc * 0.35) * 2.0 - 1.0);
		h = fbm(p * sc) * 0.6 + w * w * w * 0.5;
	} else if (kind < 2.5) {
		// scales
		float c = cells(p * sc * 0.55);
		h = smoothstep(0.0, 0.3, c);
		alb *= 0.8 + 0.25 * h;
	} else if (kind < 3.5) {
		// stone and bone: pitted
		h = fbm(p * sc * 0.7) + fbm(p * sc * 2.3) * 0.3;
		alb *= 0.75 + 0.35 * fbm(p * sc * 0.25);
	} else if (kind < 4.5) {
		// fibres: cloth weave, fur, wood grain
		h = vnoise(vec3(p.x * sc * 2.5, p.y * sc * 0.35, p.z * sc * 2.5));
		alb *= 0.85 + 0.25 * h;
	} else if (kind > 5.5 && kind < 6.5) {
		// embers: bright cracks through a charred crust
		float c = cells(p * sc * 0.3);
		float crack = 1.0 - smoothstep(0.0, 0.08, c);
		h = smoothstep(0.0, 0.3, c);
		glow *= crack * (0.7 + 0.3 * sin(TIME * 3.0 + p.y * 9.0));
		alb *= mix(1.0, 0.3, crack);
	}

	// mottling and veins make skin look alive (or not)
	if (kind > 0.5 && kind < 1.5) {
		alb *= 0.82 + 0.32 * fbm(p * 3.1);
		if (veins > 0.0) {
			float v = 1.0 - abs(vnoise(p * 9.0) * 2.0 - 1.0);
			v = pow(v, 14.0) + pow(1.0 - abs(vnoise(p * 23.0 + 5.0) * 2.0 - 1.0), 20.0) * 0.6;
			alb = mix(alb, vein_color, clamp(v * veins, 0.0, 1.0));
		}
		rough = mix(rough, rough * 0.35, wet);
	}
	if (tint.a > 0.0) alb = mix(alb, alb * tint.rgb * 1.45, tint.a);

	vec3 n = bump(NORMAL, VERTEX, h * detail_strength * 0.006);
	float ndv = clamp(dot(n, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - ndv, 4.0) * 0.6;

	vec3 emit = vec3(0.0);
	if (kind > 4.5 && kind < 5.5) emit += eye_color * eye_energy * glow * (0.85 + 0.15 * sin(TIME * 2.0));
	else emit += glow_color * glow_energy * glow;
	emit += rim_color * rim_energy * fres * (1.0 - v_mat.x * 0.6);
	emit += burn_color * burn * 8.0;
	emit += flash_color.rgb * flash * 1.6;

	// fleshy parts: a little light bleeds through where they are lit from behind (screen-space
	// subsurface scattering is far too blurry at this scale, so only backlight is used)
	ALBEDO = mix(alb, flash_color.rgb, flash * 0.85);
	ROUGHNESS = rough;
	METALLIC = v_mat.x;
	SPECULAR = 0.5;
	BACKLIGHT = alb * v_mat.y * 0.45;
	NORMAL = n;
	EMISSION = emit;
}
