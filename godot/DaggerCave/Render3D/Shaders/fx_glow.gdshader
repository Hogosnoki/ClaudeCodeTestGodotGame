// Additive light particles (sparks, flashes, embers, rings, glints, swooshes), one quad per
// particle in a MultiMesh. INSTANCE_CUSTOM: x = shape, y = intensity, z/w = shape parameters.
// COLOR = linear colour with the particle's fade in alpha.
// No two particles are alike: where a shape has no parameter in w, w is the particle's seed (0..1; 0 = a clean, regular
// shape, for the marks that must read exactly), and the fleck's outline, the ring's wander and breaks, the flash's rays and
// the swell's edge are all drawn from it.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

varying vec4 v_custom;
varying vec4 v_col;

void vertex() {
	v_custom = INSTANCE_CUSTOM;
	v_col = COLOR;
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float shape = v_custom.x;
	float a = 0.0;
	float core = 0.0;
	float seed = v_custom.w;
	float irr = step(0.0005, seed);
	float s1 = seed * 43.98, s2 = seed * 91.7 + 1.3, s3 = seed * 157.3 + 4.1;
	if (shape < 0.5) {
		// a fleck of light: a lopsided, faceted shard with its hot spot off-centre (round only when unseeded)
		float ang = atan(uv.y, uv.x);
		float reach = 1.0 - irr * (0.3 + 0.2 * sin(ang * 3.0 + s1) + 0.13 * sin(ang * 5.0 + s2) + 0.12 * sin(ang * 2.0 + s3));
		float r = length(uv) / max(reach, 0.25);
		a = exp(-r * r * 4.5);
		vec2 hot = uv - irr * 0.16 * vec2(cos(s2), sin(s2));
		core = exp(-dot(hot, hot) * 26.0);
	} else if (shape < 1.5) {
		// a streak: the quad runs tail (u = -1) to head (u = 1); it tapers to its tail like a comet, and bows a little
		float bow = irr * (fract(seed * 7.3) - 0.5) * 0.5;
		float y = uv.y - bow * (1.0 - uv.x * uv.x);
		float taper = mix(1.0, 0.35 + 0.65 * smoothstep(-1.0, 0.7, uv.x), irr);
		float across = exp(-y * y * 7.0 / (taper * taper));
		float along = smoothstep(-1.0, 0.55, uv.x) * (1.0 - smoothstep(0.82, 1.0, uv.x));
		a = across * along;
		core = exp(-y * y * 40.0 / (taper * taper)) * smoothstep(0.1, 0.85, uv.x) * (1.0 - smoothstep(0.9, 1.0, uv.x));
	} else if (shape < 2.5) {
		// a ring of light (z = thickness): its radius wanders a little, it thickens and thins, and stretches of it burn brighter
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float j = 0.78 + irr * (0.022 * sin(ang * 3.0 + s1) + 0.014 * sin(ang * 7.0 - s2));
		float th = max(v_custom.z, 0.02) * (1.0 + irr * (0.22 * sin(ang * 2.0 + s2) + 0.16 * sin(ang * 5.0 + s3)));
		float lit = 1.0 - irr * (0.28 + 0.26 * sin(ang * 2.0 + s3) + 0.2 * sin(ang * 3.0 - s1) + 0.14 * sin(ang * 9.0 + s2));
		a = exp(-pow((r - j) / th, 2.0)) * lit;
		core = exp(-pow((r - j) / (th * 0.3), 2.0)) * 0.8 * lit;
	} else if (shape < 3.5) {
		// a four-point star
		float s1 = exp(-abs(uv.y) * 45.0) * max(0.0, 1.0 - abs(uv.x));
		float s2 = exp(-abs(uv.x) * 45.0) * max(0.0, 1.0 - abs(uv.y)) * 0.7;
		float r2 = dot(uv, uv);
		a = max(s1, s2) + exp(-r2 * 14.0) * 0.7;
		core = exp(-r2 * 50.0);
	} else if (shape < 4.5) {
		// a flash: a soft bloom around a white core, lopsided, with a few uneven rays thrown out of it
		float r2 = dot(uv, uv);
		float r = sqrt(r2);
		float ang = atan(uv.y, uv.x);
		float lob = 1.0 + irr * (0.1 * sin(ang * 2.0 + s1) + 0.08 * sin(ang * 3.0 + s2));
		a = exp(-r2 * 3.6 / (lob * lob)) * 0.6 + exp(-r2 * 10.0) * 0.6;
		float rays = pow(max(0.0, sin(ang * 2.0 + s2)), 28.0) * exp(-r * 2.6)
			+ pow(max(0.0, sin(ang * 3.0 + s3)), 40.0) * exp(-r * 4.2) * 0.8;
		a += irr * rays * 1.3;
		core = exp(-r2 * 30.0) + irr * rays * 0.5;
		// (never the quad's own square: it ends in a circle)
		float rim = 1.0 - smoothstep(0.7, 0.98, r);
		a *= rim; core *= rim;
	} else if (shape < 5.5) {
		// a crescent: an arc from angle z to angle w (radians), bright at the leading end (w)
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float a0 = v_custom.z, a1 = v_custom.w;
		float t = (ang - a0) / (a1 - a0);
		if (t < 0.0 || t > 1.0) {
			float ang2 = ang + (ang < a0 ? 6.2831853 : -6.2831853);
			t = (ang2 - a0) / (a1 - a0);
		}
		if (t >= 0.0 && t <= 1.0) {
			float band = exp(-pow((r - 0.8) / 0.11, 2.0));
			a = band * t * t;
			core = exp(-pow((r - 0.87) / 0.025, 2.0)) * t;
		}
	}
	else if (shape < 6.5) {
		// a jaw: an arc from angle z to angle w (radians) with a row of teeth pointing in toward the middle (a bite)
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float a0 = v_custom.z, a1 = v_custom.w;
		float t = (ang - a0) / (a1 - a0);
		if (t < 0.0 || t > 1.0) {
			float ang2 = ang + (ang < a0 ? 6.2831853 : -6.2831853);
			t = (ang2 - a0) / (a1 - a0);
		}
		if (t >= 0.0 && t <= 1.0) {
			float gum = exp(-pow((r - 0.8) / 0.06, 2.0));
			float f = fract(t * 5.0);
			float tip = 0.8 - 0.32 * (1.0 - abs(f - 0.5) * 2.0);
			float tooth = smoothstep(tip - 0.04, tip + 0.02, r) * (1.0 - smoothstep(0.78, 0.84, r));
			float ends = smoothstep(0.0, 0.08, t) * (1.0 - smoothstep(0.92, 1.0, t));
			a = max(gum, tooth) * ends;
			core = tooth * ends * 0.9;
		}
	}
	else if (shape < 7.5) {
		// a ragged blast front (w = a seed): the ring's radius wanders with the angle, and a faint fill behind it
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float j = 0.68 + 0.055 * sin(ang * 7.0 + v_custom.w) + 0.035 * sin(ang * 13.0 - v_custom.w * 1.7);
		float th = max(v_custom.z, 0.03);
		a = exp(-pow((r - j) / th, 2.0)) + (1.0 - smoothstep(0.0, j, r)) * 0.16;
		core = exp(-pow((r - j) / (th * 0.3), 2.0)) * 0.7;
	} else if (shape < 8.5) {
		// a diamond outline with a point of light at its middle
		float d = abs(uv.x) + abs(uv.y);
		float th = max(v_custom.z, 0.03);
		a = exp(-pow((d - 0.72) / th, 2.0));
		core = exp(-pow((d - 0.72) / (th * 0.3), 2.0)) * 0.8 + exp(-dot(uv, uv) * 70.0) * 0.6;
	} else if (shape < 9.5) {
		// a broken ring: w dashes round it
		float r = length(uv);
		float f = fract(atan(uv.y, uv.x) / 6.2831853 * max(v_custom.w, 3.0));
		float seg = smoothstep(0.08, 0.18, f) * (1.0 - smoothstep(0.55, 0.65, f));
		float th = max(v_custom.z, 0.03);
		a = exp(-pow((r - 0.76) / th, 2.0)) * seg;
		core = exp(-pow((r - 0.76) / (th * 0.3), 2.0)) * seg * 0.8;
	} else {
		// a soft swell: a pool of light, brighter toward its rim, its edge wandering and its inside mottled
		float r = length(uv);
		float ang = atan(uv.y, uv.x);
		float j = irr * (0.03 * sin(ang * 3.0 + s1) + 0.022 * sin(ang * 5.0 - s2) + 0.014 * sin(ang * 11.0 + s3));
		float mottle = 1.0 - irr * 0.35 * (0.5 + 0.5 * sin(uv.x * 9.0 + s2) * sin(uv.y * 8.0 - s1));
		a = (1.0 - smoothstep(0.7 + j, 0.84 + j, r)) * (0.2 + 0.8 * smoothstep(0.15, 0.75, r)) * mottle;
		core = exp(-pow((r - 0.74 - j) / 0.035, 2.0)) * 0.45 * (1.0 - irr * (0.35 + 0.35 * sin(ang * 2.0 + s3)));
	}
	// fade to nothing before the quad's edge
	float edge = 1.0 - smoothstep(0.8, 1.0, max(abs(uv.x), abs(uv.y)));
	a *= edge; core *= edge;
	vec3 col = v_col.rgb * v_col.a;
	ALBEDO = (col * a + vec3(1.0) * core * v_col.a * 0.85) * v_custom.y;
}
