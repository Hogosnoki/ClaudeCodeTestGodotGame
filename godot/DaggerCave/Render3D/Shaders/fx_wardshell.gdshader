// The elemental Warden's shells (Combat/WardenGuards.cs): kind 0 is the Frost Shield, a faceted shell of ice, clear in the middle and
// frosted white at the rim, cracking as it wears; kind 1 is the Wind Barrier, a thin skin of streaks of air spinning about him.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

instance uniform vec4 shell_color : source_color = vec4(0.65, 0.88, 1.0, 1.0);
instance uniform float kind = 0.0;
instance uniform float worn = 0.0;
instance uniform float flash = 0.0;

varying vec3 v_obj;
void vertex() { v_obj = VERTEX; }

float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

// the distance to the nearest edge between cells of a 3D Voronoi pattern (the cracks)
float cracks(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++) {
		vec3 g = vec3(float(x), float(y), float(z));
		vec3 o = vec3(hash(i + g), hash(i + g + 17.0), hash(i + g + 41.0));
		float d = length(g + o - f);
		if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
	}
	return d2 - d1;
}

void fragment() {
	vec3 n = normalize(v_obj);
	if (kind < 0.5) {
		// ice: flat facets (from the surface's own slope), a frosted rim, glints, and cracks spreading as it wears
		vec3 fn = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
		float ndv = clamp(abs(dot(fn, VIEW)), 0.0, 1.0);
		float rim = pow(1.0 - ndv, 2.0);
		float glint = pow(max(dot(reflect(-VIEW, fn), normalize(vec3(-0.4, 0.7, 0.6))), 0.0), 24.0);
		float c = cracks(n * 3.2);
		float crack = (1.0 - smoothstep(0.0, 0.05 + 0.08 * worn, c)) * smoothstep(0.1, 0.9, worn);
		vec3 ice = mix(shell_color.rgb * vec3(0.7, 0.85, 1.05), vec3(0.9, 0.97, 1.0), rim * 0.6);
		ALBEDO = ice + vec3(glint) * 1.5 + vec3(crack) + vec3(flash) * 0.6;
		ALPHA = clamp(0.28 + rim * 0.6 + glint + crack * 0.8 + flash * 0.35, 0.0, 1.0);
	} else {
		// wind: faint, with bright streaks running round him
		float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
		float rim = pow(1.0 - ndv, 3.0);
		float ang = atan(n.z, n.x);
		float band = sin(ang * 5.0 + n.y * 6.0 - TIME * 7.0) * 0.5 + 0.5;
		float band2 = sin(ang * 3.0 - n.y * 9.0 - TIME * 4.3 + 1.7) * 0.5 + 0.5;
		float streak = smoothstep(0.82, 1.0, band) + 0.6 * smoothstep(0.88, 1.0, band2);
		ALBEDO = mix(shell_color.rgb, vec3(1.0), 0.4 + 0.4 * streak) + vec3(flash) * 0.5;
		ALPHA = clamp((0.04 + rim * 0.4 + streak * 0.45) * (1.0 - worn * 0.5) + flash * 0.25, 0.0, 1.0);
	}
}
