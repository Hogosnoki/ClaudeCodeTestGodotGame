// Additive light particles (sparks, flashes, embers, rings, glints, swooshes), one quad per
// particle in a MultiMesh. INSTANCE_CUSTOM: x = shape, y = intensity, z/w = shape parameters.
// COLOR = linear colour with the particle's fade in alpha.
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
	if (shape < 0.5) {
		// soft round glow with a hot centre
		float r2 = dot(uv, uv);
		a = exp(-r2 * 4.5);
		core = exp(-r2 * 22.0);
	} else if (shape < 1.5) {
		// a streak: the quad runs tail (u = -1) to head (u = 1)
		float across = exp(-uv.y * uv.y * 7.0);
		float along = smoothstep(-1.0, 0.55, uv.x) * (1.0 - smoothstep(0.82, 1.0, uv.x));
		a = across * along;
		core = exp(-uv.y * uv.y * 40.0) * smoothstep(0.1, 0.85, uv.x) * (1.0 - smoothstep(0.9, 1.0, uv.x));
	} else if (shape < 2.5) {
		// a ring of light (z = thickness)
		float r = length(uv);
		float th = max(v_custom.z, 0.02);
		a = exp(-pow((r - 0.78) / th, 2.0));
		core = exp(-pow((r - 0.78) / (th * 0.3), 2.0)) * 0.8;
	} else if (shape < 3.5) {
		// a four-point star
		float s1 = exp(-abs(uv.y) * 45.0) * max(0.0, 1.0 - abs(uv.x));
		float s2 = exp(-abs(uv.x) * 45.0) * max(0.0, 1.0 - abs(uv.y)) * 0.7;
		float r2 = dot(uv, uv);
		a = max(s1, s2) + exp(-r2 * 14.0) * 0.7;
		core = exp(-r2 * 50.0);
	} else if (shape < 4.5) {
		// a flash: a wide soft bloom around a white core
		float r2 = dot(uv, uv);
		a = exp(-r2 * 2.6) * 0.65 + exp(-r2 * 9.0) * 0.6;
		core = exp(-r2 * 30.0);
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
	else {
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
	// fade to nothing before the quad's edge
	float edge = 1.0 - smoothstep(0.8, 1.0, max(abs(uv.x), abs(uv.y)));
	a *= edge; core *= edge;
	vec3 col = v_col.rgb * v_col.a;
	ALBEDO = (col * a + vec3(1.0) * core * v_col.a * 0.85) * v_custom.y;
}
