// The camp's sky at dusk: a sun just on the horizon, the sky peach and gold round it, mauve and violet away from it and
// dusky purple overhead, and a deck of drifting clouds lit orange-pink from below near the sun, lavender-grey elsewhere.
shader_type sky;
render_mode use_quarter_res_pass;

uniform vec3 zenith : source_color = vec3(0.24, 0.19, 0.36);
uniform vec3 high : source_color = vec3(0.45, 0.36, 0.50);
uniform vec3 horizon_away : source_color = vec3(0.50, 0.40, 0.52);
uniform vec3 horizon_sun : source_color = vec3(1.0, 0.62, 0.40);
uniform vec3 sun_glow : source_color = vec3(1.0, 0.72, 0.42);
uniform vec3 below : source_color = vec3(0.16, 0.12, 0.15);
uniform vec3 cloud_lit : source_color = vec3(1.0, 0.56, 0.42);
uniform vec3 cloud_dim : source_color = vec3(0.32, 0.24, 0.34);
uniform vec3 cloud_dark : source_color = vec3(0.16, 0.12, 0.19);
uniform float exposure = 0.62;
uniform float sun_size = 0.02;
uniform float cloud_cover = 0.66;
uniform float cloud_drift = 0.004;
// the light the sky gives the land (its ambient and reflections, drawn from the radiance pass) against the sky as it is seen
uniform float radiance_boost = 5.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 x) {
	vec2 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) {
	float s = 0.0, a = 0.5;
	mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int k = 0; k < 6; k++) { s += a * vnoise(p); p = r * p * 2.03 + 11.7; a *= 0.5; }
	return s;
}

void sky() {
	vec3 d = normalize(EYEDIR);
	vec3 sd = LIGHT0_ENABLED ? normalize(LIGHT0_DIRECTION) : normalize(vec3(-0.8, 0.05, -0.6));
	// how near the sun's bearing this is (round the horizon), and its height
	vec2 hd = normalize(d.xz + 1e-5), hs = normalize(sd.xz + 1e-5);
	float toward = dot(hd, hs) * 0.5 + 0.5;
	float near = pow(max(dot(d, sd), 0.0), 1.0);
	float up = clamp(d.y, 0.0, 1.0);

	// the clear sky: warm low round the sun, mauve low away from it, violet and dusky purple higher up
	vec3 hz = mix(horizon_away, horizon_sun, pow(toward, 3.0));
	vec3 c = mix(hz, high, smoothstep(0.0, 0.32, up));
	c = mix(c, zenith, smoothstep(0.3, 0.9, up));
	// the sun's broad glow, and the bright band along the horizon under it
	c += sun_glow * (pow(near, 10.0) * 0.45 + pow(near, 60.0) * 1.6);
	c += horizon_sun * pow(toward, 8.0) * exp(-abs(d.y) * 14.0) * 0.6;
	if (d.y < 0.0) c = mix(c, below, smoothstep(0.0, 0.12, -d.y));

	// the clouds: a deck overhead, seen flattened toward the horizon
	if (d.y > 0.0 && !AT_CUBEMAP_PASS) {
		vec2 uv = d.xz / (d.y + 0.08) * 0.9 + vec2(TIME * cloud_drift, TIME * cloud_drift * 0.4);
		float n = fbm(uv * 1.3);
		float detail = fbm(uv * 5.1 + 3.0);
		float dens = smoothstep(1.0 - cloud_cover, 1.0 - cloud_cover + 0.28, n * 0.8 + detail * 0.3);
		// streaks: the deck thins in bands, and thins out straight overhead and right at the horizon
		dens *= smoothstep(0.02, 0.12, d.y) * (1.0 - 0.45 * smoothstep(0.55, 0.95, d.y));
		// lit from below by the sun: brightest at the clouds' thin edges near it; thick cores darker
		float edge = 1.0 - smoothstep(0.35, 0.9, dens);
		vec3 lit = mix(cloud_dim, cloud_lit, pow(toward, 2.2) * (0.55 + 0.45 * edge));
		lit += sun_glow * pow(near, 8.0) * edge * 1.4;
		vec3 cc = mix(lit, cloud_dark, smoothstep(0.55, 1.0, dens) * (0.5 - 0.3 * toward));
		c = mix(c, cc, dens * 0.95);
	}
	// the sun itself, on the horizon (a squashed, hot disc that the clouds don't cover)
	float disc = smoothstep(cos(sun_size * 1.08), cos(sun_size), dot(d, sd));
	c = mix(c, vec3(4.0, 3.4, 2.6), disc);
	COLOR = c * exposure * (AT_CUBEMAP_PASS ? radiance_boost : 1.0);
}
