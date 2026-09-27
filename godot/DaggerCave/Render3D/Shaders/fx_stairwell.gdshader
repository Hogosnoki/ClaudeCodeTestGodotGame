// An exit's doorway: a passage going down into the dark. The quad is only the opening; each
// pixel traces its view ray into a virtual tunnel behind it, framed every metre by a stone rib
// (the doorway's own arch, repeated), each rib set lower than the last as the passage descends.
// From the game's nearly level camera the floor drops out of sight, so what reads is the ribs:
// nested arches stepping down and away, fading into black, and past the last of them, far
// below, a faint breath of the next biome's colour. Nothing here is bright.
shader_type spatial;
render_mode unshaded, cull_back, fog_disabled, shadows_disabled;

// (plain sRGB values as the view sets them; converted to linear below)
instance uniform vec4 below_color = vec4(0.7, 0.5, 1.0, 1.0);
instance uniform vec4 stone_color = vec4(0.32, 0.29, 0.27, 1.0);
/// The opening, in the quad's own space: half its width, its height at the top of the arch.
uniform float half_w = 1.05;
uniform float door_h = 3.0;
/// Ribs: how many, how far apart, how deep each is, and how much lower each sits than the last.
uniform int ribs = 13;
uniform float spacing = 0.85;
uniform float rib_w = 0.17;
uniform float drop = 0.3;

varying vec3 v_local;
varying vec3 v_cam;

void vertex() {
	v_local = VERTEX;
	v_cam = (inverse(MODEL_MATRIX) * vec4(CAMERA_POSITION_WORLD, 1.0)).xyz;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

// signed distance to the arch outline (negative inside): a rectangle below the springing, a half-disc above
float arch(vec2 p, float hw, float h) {
	float spring = h - hw;
	if (p.y > spring) return length(vec2(p.x, p.y - spring)) - hw;
	return max(abs(p.x) - hw, -p.y);
}

void fragment() {
	// door space: the threshold at y = 0, the opening at z = 0, the passage toward -z
	vec3 ro = v_local + vec3(0.0, door_h * 0.5, 0.0);
	vec3 rd = normalize(v_local - v_cam);
	if (arch(ro.xy, half_w, door_h) > 0.0) discard;
	vec3 stone = pow(stone_color.rgb, vec3(2.2));
	vec3 below = pow(below_color.rgb, vec3(2.2));
	float breathe = 0.85 + 0.15 * sin(TIME * 1.3);
	vec3 col = vec3(0.0);
	bool done = false;
	for (int k = 1; k <= 24; k++) {
		if (k > ribs) break;
		float z = -float(k) * spacing;
		float t = (z - ro.z) / min(rd.z, -1e-4);
		vec3 p = ro + rd * t;
		// in this rib's frame the passage has sunk by k * drop
		vec2 q = vec2(p.x, p.y + float(k) * drop);
		float d = arch(q, half_w, door_h);
		float depth = float(k) / float(ribs);
		float mouth = exp(-float(k) * 0.36);
		float glow = 1.6 * depth * depth * breathe;
		if (d > 0.0) {
			// the ray left the passage before this rib: the dark vault between two ribs
			col = stone * 0.6 * (5.0 * mouth + below * glow * 3.0);
			done = true;
			break;
		}
		if (d > -rib_w) {
			// a rib's face: coursed stones round the arch, its inner edge catching the light
			float spring = door_h - half_w;
			float ang = atan(q.y - spring, q.x);
			float course = q.y > spring ? ang * 9.0 / 3.14159 : q.y * 2.2;
			float joints = abs(fract(course) - 0.5);
			float shade = (0.8 + 0.3 * hash(vec2(float(k), floor(course)))) * smoothstep(0.0, 0.06, joints);
			float edge = 1.0 + 1.4 * smoothstep(-rib_w + 0.05, -rib_w, d);
			col = stone * shade * edge * (8.0 * mouth + below * glow * 6.0);
			done = true;
			break;
		}
	}
	if (!done) {
		// past the last rib: the stair turns away below, and the next biome's light comes up
		float lowness = clamp(1.0 - ro.y / door_h, 0.0, 1.0);
		col = below * (0.15 + 0.45 * lowness * lowness) * breathe;
	}
	ALBEDO = col;
}
