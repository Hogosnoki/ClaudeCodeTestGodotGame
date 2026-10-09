using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Procedural meshes for cave dressing. Every mesh grows from the origin along +Y (the placer
/// points +Y along the surface normal or gravity), in metres. Vertex colours: for rock pieces
/// (drawn with the terrain's rock material) r = 0, g = occlusion, b = variation; for glowing
/// things rgb = base colour and a = how much that part glows.
/// </summary>
public static class DecorMeshes
{
    private static Color Rock(float ao, float variation) => new(0f, ao, variation, 1f);

    public static MeshBuilder Stalactite(Random rng, float length, float radius, Noise3 noise)
    {
        var mb = new MeshBuilder();
        int rings = 10;
        var path = new List<Vector3>();
        var radii = new List<float>();
        var bend = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * length * 0.18f;
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            path.Add(new Vector3(0, -0.25f * radius + t * (length + 0.25f * radius), 0) + bend * t * t);
            radii.Add(radius * MathF.Pow(1f - t, 1.35f) * (i == 0 ? 1.35f : 1f) + (i == rings ? 0f : 0.004f));
        }
        float seed = (float)rng.NextDouble() * 100f;
        mb.Tube(path, radii, 8, Rock(0.8f, 0.5f), capStart: true,
            bump: (i, k) => 0.22f * noise.Sample(k * 0.9f + seed, i * 0.45f, seed));
        // occlusion: darker at the root
        for (int k = 0; k < mb.Count; k++)
        {
            float t = Math.Clamp(mb.V[k].Y / length, 0f, 1f);
            mb.C[k] = Rock(0.55f + 0.45f * t, 0.3f + 0.4f * t);
        }
        mb.SmoothNormals();
        return mb;
    }

    public static MeshBuilder Pebbles(Random rng, Noise3 noise, int count, float size)
    {
        var mb = new MeshBuilder();
        for (int k = 0; k < count; k++)
        {
            float r = size * (0.35f + (float)rng.NextDouble() * 0.65f);
            var c = new Vector3(((float)rng.NextDouble() - 0.5f) * size * 4f, r * 0.45f, ((float)rng.NextDouble() - 0.5f) * size * 3f);
            mb.Blob(c, new Vector3(r, r * 0.7f, r * 0.9f), 5, Rock(0.9f, (float)rng.NextDouble()), noise, 0.28f, 1.7f, 0.3f);
        }
        return mb;
    }

    public static MeshBuilder Boulder(Random rng, Noise3 noise, float size)
    {
        var mb = new MeshBuilder();
        mb.Blob(new Vector3(0, size * 0.35f, 0), new Vector3(size, size * 0.72f, size * 0.85f), 10, Rock(1f, (float)rng.NextDouble()), noise, 0.22f, 1.3f, 0.45f);
        for (int k = 0; k < mb.Count; k++) mb.C[k] = Rock(0.5f + 0.5f * Math.Clamp(mb.V[k].Y / size + 0.3f, 0f, 1f), mb.C[k].B);
        return mb;
    }

    /// <summary>
    /// A broken chunk of rock for a rubble pile: a low, lumpy, uneven polyhedron (never a smooth ball), flat-shaded face by face,
    /// each face its own tone, lighter on top and darker underneath. The pile's material gives it the grain of real stone.
    /// </summary>
    public static MeshBuilder RubbleChunk(Random rng, Noise3 noise, float size)
    {
        float R() => (float)rng.NextDouble();
        var raw = new MeshBuilder();
        var radii = new Vector3(size * (0.75f + 0.5f * R()), size * (0.42f + 0.4f * R()), size * (0.6f + 0.45f * R()));
        raw.Blob(new Vector3(0, radii.Y * 0.8f, 0), radii, 3, Colors.White, noise, 0.5f, 1.7f + R(), 0.35f);
        // (knock the corners about further, so the outline is angular and different from every other)
        for (int k = 0; k < raw.V.Count; k++)
        {
            var v = raw.V[k];
            float j = 1f + (R() - 0.5f) * 0.42f;
            raw.V[k] = new Vector3(v.X * j, v.Y * (1f + (R() - 0.5f) * 0.3f), v.Z * j);
        }
        var mb = raw.Faceted();
        float baseTone = 0.78f + 0.3f * R();
        var warm = new Color(1.0f, 0.97f, 0.93f);
        for (int k = 0; k < mb.Count; k += 3)
        {
            // (one tone per face)
            float up = Math.Clamp(mb.N[k].Y * 0.5f + 0.5f, 0f, 1f);
            float t = baseTone * (0.62f + 0.5f * up) * (0.8f + 0.4f * R());
            var c = new Color(t * warm.R, t * warm.G, t * warm.B);
            mb.C[k] = c; mb.C[k + 1] = c; mb.C[k + 2] = c;
        }
        return mb;
    }

    /// <summary>
    /// One block of a broken-stone ledge: a lumpy faceted chunk w wide, h thick and d deep, flat across the top (where you stand), each
    /// face its own tone (lighter up, darker under) over <paramref name="tint"/>.
    /// </summary>
    public static MeshBuilder SlabChunk(Random rng, Noise3 noise, float w, float h, float d, Color tint)
    {
        float R() => (float)rng.NextDouble();
        // a block hewn roughly: eight corners knocked about (the top kept flat), flat faces each its own tone
        var c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float sx = (i & 1) == 0 ? -1 : 1, sy = (i & 2) == 0 ? -1 : 1, sz = (i & 4) == 0 ? -1 : 1;
            float y = sy > 0 ? h * 0.5f : -h * (0.5f + 0.18f * R());
            c[i] = new Vector3(sx * w * 0.5f * (1f + (R() - 0.5f) * 0.22f), y, sz * d * 0.5f * (1f + (R() - 0.5f) * 0.22f));
        }
        var mb = new MeshBuilder();
        void Face(int a, int b2, int c2, int d2, float shade)
        {
            var n = (c[b2] - c[a]).Cross(c[d2] - c[a]).Normalized();
            float t = shade * (0.82f + 0.36f * R());
            var col = new Color(t * tint.R, t * tint.G, t * tint.B);
            int i0 = mb.Add(c[a], n, col, new Vector2(0, 0)), i1 = mb.Add(c[b2], n, col, new Vector2(1, 0)), i2 = mb.Add(c[c2], n, col, new Vector2(1, 1)), i3 = mb.Add(c[d2], n, col, new Vector2(0, 1));
            mb.Quad(i0, i1, i2, i3);
        }
        // (corner bits: x = 1, y = 2, z = 4; wound so each face looks out)
        Face(2, 6, 7, 3, 1.1f);   // top
        Face(0, 1, 5, 4, 0.55f);  // underside
        Face(4, 5, 7, 6, 0.85f);  // front (+z)
        Face(0, 2, 3, 1, 0.8f);   // back
        Face(0, 4, 6, 2, 0.9f);   // left
        Face(1, 3, 7, 5, 0.9f);   // right
        return mb;
    }

    /// <summary>A spray of pointed crystals from one root.</summary>
    public static MeshBuilder CrystalCluster(Random rng, int count, float scale, Color body)
    {
        var mb = new MeshBuilder();
        for (int k = 0; k < count; k++)
        {
            float len = scale * (0.35f + (float)rng.NextDouble() * (k == 0 ? 1.1f : 0.8f));
            float rad = len * (0.1f + (float)rng.NextDouble() * 0.07f);
            var one = new MeshBuilder();
            one.Crystal(rad, len, rad * 1.8f, new Color(body, 0.35f + (float)rng.NextDouble() * 0.5f), (float)rng.NextDouble());
            float tilt = k == 0 ? 0.1f : 0.25f + (float)rng.NextDouble() * 0.6f;
            float yaw = (float)rng.NextDouble() * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, tilt);
            var off = new Vector3(((float)rng.NextDouble() - 0.5f) * scale * 0.25f, -rad * 0.6f, ((float)rng.NextDouble() - 0.5f) * scale * 0.25f);
            mb.Append(one, new Transform3D(basis, off));
        }
        return mb;
    }

    /// <summary>A clump of mushrooms: pale stems, caps whose gills (and spots) glow.</summary>
    public static MeshBuilder MushroomCluster(Random rng, int count, float scale, Color cap, bool giant = false)
    {
        var mb = new MeshBuilder();
        var stem = new Color(0.78f, 0.76f, 0.68f, 0.05f);
        for (int k = 0; k < count; k++)
        {
            float h = scale * (giant ? 0.6f + (float)rng.NextDouble() * 0.9f : 0.35f + (float)rng.NextDouble() * 0.9f) * (k == 0 ? 1.25f : 1f);
            float sr = h * (giant ? 0.07f : 0.09f);
            float capR = h * (giant ? 0.55f : 0.45f) * (0.8f + (float)rng.NextDouble() * 0.5f);
            var baseP = k == 0 ? Vector3.Zero : new Vector3(((float)rng.NextDouble() - 0.5f) * scale * 1.1f, 0, ((float)rng.NextDouble() - 0.5f) * scale * 0.8f);
            var lean = new Vector3(((float)rng.NextDouble() - 0.5f), 0, ((float)rng.NextDouble() - 0.5f)) * h * 0.35f;
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                path.Add(baseP + new Vector3(0, -0.05f + t * h, 0) + lean * t * t);
                radii.Add(sr * (1.25f - 0.35f * t));
            }
            mb.Tube(path, radii, 7, stem, capStart: false);
            var top = path[^1];
            // cap: a flattened dome, with a glowing underside
            int start = mb.Count;
            int seg = 12, rings = 5;
            var capCol = new Color(cap.Darkened(0.35f), 0.25f);
            var gill = new Color(cap, 1f);
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * MathF.PI * 0.5f;
                for (int s = 0; s <= seg; s++)
                {
                    float th = s / (float)seg * Mathf.Tau;
                    var d = new Vector3(MathF.Cos(th) * MathF.Sin(phi), MathF.Cos(phi), MathF.Sin(th) * MathF.Sin(phi));
                    var p = top + new Vector3(d.X * capR, d.Y * capR * (giant ? 0.45f : 0.6f), d.Z * capR);
                    mb.Add(p, d, v > 0.95f ? gill : capCol);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = start + r * (seg + 1) + s, b = a + 1, c = a + seg + 1, d = c + 1;
                    mb.Tri(a, b, c); mb.Tri(b, d, c);
                }
            // underside (gills) as a slightly concave disc facing down
            int ctr = mb.Add(top + Vector3.Down * capR * 0.05f, Vector3.Down, gill);
            int ring = mb.Count;
            for (int s = 0; s <= seg; s++)
            {
                float th = s / (float)seg * Mathf.Tau;
                mb.Add(top + new Vector3(MathF.Cos(th) * capR, 0.02f * capR, MathF.Sin(th) * capR), Vector3.Down, gill);
            }
            for (int s = 0; s < seg; s++) mb.Tri(ctr, ring + s, ring + s + 1);
        }
        mb.FixWinding(0, mb.I.Count);
        return mb;
    }

    /// <summary>A tuft of grass blades (UV.y = 0 at the root, 1 at the tip, for the wind).</summary>
    public static MeshBuilder GrassTuft(Random rng, int blades, float height)
    {
        var mb = new MeshBuilder();
        for (int b = 0; b < blades; b++)
        {
            float h = height * (0.5f + (float)rng.NextDouble() * 0.7f);
            float w = 0.012f + (float)rng.NextDouble() * 0.014f;
            float yaw = (float)rng.NextDouble() * Mathf.Tau;
            var dir = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
            var side = new Vector3(-dir.Z, 0, dir.X);
            var root = new Vector3(((float)rng.NextDouble() - 0.5f) * height * 0.5f, -0.02f, ((float)rng.NextDouble() - 0.5f) * height * 0.4f);
            float lean = 0.2f + (float)rng.NextDouble() * 0.45f;
            int prev0 = -1, prev1 = -1;
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                var c = root + Vector3.Up * h * t + dir * h * lean * t * t;
                float ww = w * (1f - t * 0.92f);
                var n = (Vector3.Up * 0.3f - dir).Normalized();
                int a = mb.Add(c - side * ww, n, new Color(1, 1, 1, 1), new Vector2(0, t));
                int bb = mb.Add(c + side * ww, n, new Color(1, 1, 1, 1), new Vector2(1, t));
                if (prev0 >= 0) { mb.Tri(prev0, prev1, bb); mb.Tri(prev0, bb, a); }
                prev0 = a; prev1 = bb;
            }
        }
        return mb;
    }

    /// <summary>Glow-worm threads hanging from one spot, with glowing beads of mucus.</summary>
    public static MeshBuilder GlowThreads(Random rng, int threads, float length, Color glow)
    {
        var mb = new MeshBuilder();
        var thread = new Color(0.8f, 0.85f, 0.9f, 0.35f);
        var bead = new Color(glow, 1f);
        for (int k = 0; k < threads; k++)
        {
            float len = length * (0.4f + (float)rng.NextDouble() * 0.8f);
            var o = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.5f, 0, ((float)rng.NextDouble() - 0.5f) * 0.4f);
            mb.Tube(new[] { o, o + Vector3.Up * len }, new[] { 0.004f, 0.003f }, 3, thread, capStart: false);
            int beads = 2 + rng.Next(4);
            for (int b = 0; b < beads; b++)
            {
                float t = 0.25f + b / (float)beads * 0.75f;
                float br = 0.012f + (float)rng.NextDouble() * 0.014f;
                AddSphere(mb, o + Vector3.Up * len * t, br, bead);
            }
        }
        return mb;
    }

    public static MeshBuilder Icicles(Random rng, int count, float length)
    {
        var mb = new MeshBuilder();
        var ice = new Color(0.75f, 0.9f, 1f, 0.12f);
        for (int k = 0; k < count; k++)
        {
            float len = length * (0.3f + (float)rng.NextDouble() * 0.9f);
            float r = len * (0.08f + (float)rng.NextDouble() * 0.05f);
            var o = new Vector3(((float)rng.NextDouble() - 0.5f) * length * 0.8f, 0, ((float)rng.NextDouble() - 0.5f) * length * 0.5f);
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                path.Add(o + Vector3.Up * (-0.03f + len * t));
                radii.Add(r * MathF.Pow(1f - t, 1.2f) * (1f + 0.15f * MathF.Sin(t * 19f + k)));
            }
            mb.Tube(path, radii, 6, ice, capStart: true);
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>Roots dangling from the roof (the cave mouth).</summary>
    public static MeshBuilder Roots(Random rng, int count, float length, Noise3 noise)
    {
        var mb = new MeshBuilder();
        var col = new Color(0.32f, 0.22f, 0.15f, 0f);
        for (int k = 0; k < count; k++)
        {
            float len = length * (0.4f + (float)rng.NextDouble() * 0.8f);
            float r = 0.012f + (float)rng.NextDouble() * 0.03f;
            var o = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, -0.1f, ((float)rng.NextDouble() - 0.5f) * 0.6f);
            float seed = (float)rng.NextDouble() * 50f;
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                var wig = new Vector3(noise.Sample(seed, t * 2.5f, 0), 0, noise.Sample(seed + 9f, t * 2.5f, 3f)) * len * 0.25f * t;
                path.Add(o + Vector3.Up * len * t + wig);
                radii.Add(r * (1f - t * 0.85f));
            }
            mb.Tube(path, radii, 5, col, capStart: false);
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>
    /// A massive root come down through the ceiling from the world above: flared where it enters
    /// the rock, fluted like bark, knotted, meandering along +Y from the origin and thinning to a
    /// point, with a few side roots splitting away.
    /// </summary>
    public static MeshBuilder GiantRoot(Random rng, Noise3 noise, float length, float radius, Color col)
    {
        var mb = new MeshBuilder();
        float seed = (float)rng.NextDouble() * 50f;
        var path = new List<Vector3>();
        var radii = new List<float>();
        const int n = 18;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            var wig = new Vector3(noise.Sample(seed, t * 2.2f, 0f), 0f, noise.Sample(seed + 7f, t * 2.2f, 5f) * 0.6f) * length * 0.2f * t;
            path.Add(new Vector3(0f, length * t, 0f) + wig);
            float flare = 1f + 0.9f * MathF.Max(0f, 1f - t * 5f);
            float knot = 1f + 0.14f * MathF.Max(0f, MathF.Sin(t * 21f + seed));
            radii.Add(radius * flare * knot * (1f - 0.9f * t) + 0.01f);
        }
        path[0] -= Vector3.Up * radius * 0.9f; // the flare sinks into the rock
        mb.Tube(path, radii, 10, col, capStart: true, bump: (i, k) => (k % 2 == 0 ? 0.07f : -0.05f));
        int branches = 2 + rng.Next(3);
        for (int b = 0; b < branches; b++)
        {
            int i0 = (int)((0.12f + (float)rng.NextDouble() * 0.5f) * n);
            var o = path[i0];
            var dir = new Vector3(((float)rng.NextDouble() - 0.5f) * 2f, 0.5f + (float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 1.2f).Normalized();
            float bl = length * (0.22f + (float)rng.NextDouble() * 0.3f), br = radii[i0] * 0.42f;
            var bp = new List<Vector3>();
            var brr = new List<float>();
            for (int i = 0; i <= 9; i++)
            {
                float t = i / 9f;
                bp.Add(o + dir * bl * t + new Vector3(0f, bl * 0.35f * t * t, 0f)
                       + new Vector3(noise.Sample(seed + b * 3f, t * 3f, 1f), 0f, noise.Sample(seed + b * 5f, t * 3f, 2f)) * bl * 0.15f);
                brr.Add(br * (1f - 0.9f * t) + 0.006f);
            }
            mb.Tube(bp, brr, 6, col.Darkened(0.08f), capStart: false);
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>
    /// A leviathan's ribcage lying along Z (head end at -Z, the tail end at +Z), drawn from real whale
    /// skeletons: a spine along the top whose vertebrae each carry a tall flat spine and a flat plate
    /// standing out to either side, and from the end of each plate a rib that goes out, bows into a barrel
    /// and curls back in toward its tip, swept back toward the tail. The ribs are flat, tapering blades
    /// with a knobbed head, the longest and most bowed a third of the way along, the hindmost shorter,
    /// straighter and thinner. Ribs hang down (-Y) to about <paramref name="height"/>, out to about
    /// <paramref name="halfWidth"/>; the spine starts at the origin and runs to +Z.
    /// </summary>
    public static MeshBuilder Ribcage(Random rng, Noise3 noise, int ribs, float spacing, float halfWidth, float height, Color bone)
    {
        var mb = new MeshBuilder();
        float len = (ribs - 1) * spacing;
        // (stout bones: a rib as thick as a man's arm at its root, and the vertebrae to match)
        float thick = Math.Clamp(height / 6f, 1.0f, 1.7f);
        // the spine: a round vertebral body between short gaps, a tall flat spine leaning back from each
        for (float z = -spacing; z <= len + spacing * 1.01f; z += spacing * 0.5f)
        {
            float r = 0.36f * thick * (1f + 0.15f * noise.Sample(z * 0.7f, 0f, 3f));
            mb.Blob(new Vector3(0f, 0f, z), new Vector3(r * 1.0f, r, r * 0.8f), 5, bone.Darkened(0.06f), noise, 0.22f, 2.5f);
            mb.Tube(new[] { new Vector3(0f, r * 0.6f, z), new Vector3(0f, r * 2.2f, z + 0.12f * spacing), new Vector3(0f, r * 3.6f, z + 0.3f * spacing) },
                new[] { r * 0.46f, r * 0.34f, r * 0.12f }, 5, bone, capStart: false, broad: Vector3.Forward, aspect: 1.9f);
        }
        const float tail = 1f;
        for (int i = 0; i < ribs; i++)
        {
            float z = i * spacing;
            float u = ribs <= 1 ? 0f : i / (float)(ribs - 1);
            // the length: rising to the longest a third of the way back, then falling away to the tail
            float f = u < 0.3f ? 0.78f + 0.22f * (u / 0.3f) : 1f - 0.5f * MathF.Pow((u - 0.3f) / 0.7f, 1.4f);
            f *= 0.96f + 0.08f * (float)rng.NextDouble();
            float h = height * f;
            float rr = thick * (0.3f - 0.07f * u);
            // the plate the rib stands on, out from its vertebra
            float x0 = 0.95f * thick;
            // the barrel: an arc of an ellipse out and round, front ribs bowed further, rear ones straighter
            float w = Math.Max(x0 + 0.6f, halfWidth * (0.6f + 0.4f * f)) - x0;
            float psiMax = Mathf.DegToRad(145f - 38f * u);
            float b = h / (1f - MathF.Cos(psiMax));
            // swept back toward the tail, the rear ribs more
            float sweep = h * (0.13f + 0.2f * u) * tail;
            foreach (float side in new[] { -1f, 1f })
            {
                var plate = new[] { new Vector3(0f, 0f, z), new Vector3(side * x0 * 0.55f, 0.02f * thick, z + 0.03f * spacing), new Vector3(side * x0, -0.05f * thick, z + 0.06f * spacing) };
                mb.Tube(plate, new[] { rr * 1.1f, rr * 0.95f, rr * 0.9f }, 6, bone.Darkened(0.04f), capStart: false, broad: Vector3.Forward, aspect: 1.7f);
                var path = new List<Vector3>();
                var radii = new List<float>();
                const int n = 18;
                float warp = (float)rng.NextDouble() * 10f;
                for (int k = 0; k <= n; k++)
                {
                    float t = k / (float)n;
                    float psi = t * psiMax;
                    float x = x0 + w * MathF.Sin(psi);
                    float y = -b + b * MathF.Cos(psi);
                    float zz = z + 0.06f * spacing + sweep * MathF.Pow(t, 1.35f) + noise.Sample(warp, t * 2f, side) * 0.12f;
                    path.Add(new Vector3(side * x, y, zz));
                    // thickest at the head and the angle, then thinning to the tip; a knob where it meets the plate
                    float taper = 1f - 0.62f * MathF.Pow(t, 1.15f);
                    float knob = 1f + 0.55f * MathF.Max(0f, 1f - t * 9f);
                    radii.Add(rr * taper * knob + 0.01f);
                }
                mb.Tube(path, radii, 8, bone.Darkened(0.05f * (i % 3)), capStart: true, broad: Vector3.Forward, aspect: 1.9f - 0.5f * u);
            }
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>
    /// A great fossil skull facing +X: a domed cranium with heavy brows over deep dark sockets, a
    /// long snout lined with teeth, and a jaw hanging open beneath (half of it will be buried).
    /// </summary>
    public static MeshBuilder Skull(Random rng, Noise3 noise, float size, Color bone)
    {
        var mb = new MeshBuilder();
        var dark = new Color(0.03f, 0.025f, 0.02f);
        var tooth = bone.Lightened(0.12f);
        Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * size;
        mb.Blob(S(0f, 0.12f, 0f), S(0.62f, 0.5f, 0.55f), 7, bone, noise, 0.12f, 2.2f);
        foreach (float z in new[] { -1f, 1f })
        {
            mb.Blob(S(0.4f, 0.3f, 0.22f * z), S(0.24f, 0.1f, 0.15f), 4, bone, noise, 0.1f, 3f);
            mb.Blob(S(0.45f, 0.13f, 0.27f * z), S(0.14f, 0.13f, 0.1f), 5, dark, noise, 0f, 1f);
            mb.Blob(S(1.27f, -0.03f, 0.06f * z), S(0.05f, 0.04f, 0.04f), 3, dark, noise, 0f, 1f);
            // cheekbones sweeping back to the jaw hinge
            mb.Tube(new[] { S(0.55f, -0.05f, 0.28f * z), S(0.1f, -0.12f, 0.42f * z), S(-0.25f, -0.2f, 0.36f * z) }, new[] { 0.07f * size, 0.08f * size, 0.06f * size }, 6, bone, capStart: true);
            // the lower jaw, hanging open
            mb.Tube(new[] { S(-0.25f, -0.25f, 0.34f * z), S(0.35f, -0.62f, 0.22f * z), S(1.1f, -0.78f, 0.1f * z) }, new[] { 0.09f * size, 0.08f * size, 0.05f * size }, 6, bone.Darkened(0.05f), capStart: true);
        }
        // the snout, tapering forward, and its blunt end
        mb.Tube(new[] { S(0.25f, 0f, 0f), S(0.75f, -0.07f, 0f), S(1.28f, -0.12f, 0f) }, new[] { 0.34f * size, 0.25f * size, 0.14f * size }, 9, bone, capStart: false);
        mb.Blob(S(1.3f, -0.12f, 0f), S(0.14f, 0.13f, 0.14f), 5, bone, noise, 0.08f, 3f);
        // teeth: down from the snout, up from the jaw
        var cone = new MeshBuilder();
        cone.Crystal(0.035f * size, 0.16f * size, 0.1f * size, tooth);
        for (int i = 0; i < 9; i++)
        {
            float x = 0.45f + i * 0.095f;
            foreach (float z in new[] { -1f, 1f })
            {
                float zz = (0.2f - i * 0.012f) * z;
                mb.Append(cone, new Transform3D(new Basis(Vector3.Right, MathF.PI) * Basis.FromScale(Vector3.One * (1.1f - i * 0.05f)), S(x, -0.12f - i * 0.012f, zz)));
                mb.Append(cone, new Transform3D(Basis.FromScale(Vector3.One * (0.9f - i * 0.04f)), S(x + 0.05f, -0.58f - i * 0.022f, zz * 0.8f)));
            }
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>A scatter of old bones for a floor: long bones with knobbed ends, vertebrae, a broken rib.</summary>
    public static MeshBuilder Bones(Random rng, Noise3 noise, int count, float size, Color bone)
    {
        var mb = new MeshBuilder();
        float R() => (float)rng.NextDouble();
        for (int k = 0; k < count; k++)
        {
            var o = new Vector3((R() - 0.5f) * 1.6f, 0.02f, (R() - 0.5f) * 1.2f) * size;
            float yaw = R() * Mathf.Tau;
            var d = new Vector3(MathF.Cos(yaw), 0.05f, MathF.Sin(yaw));
            float roll = R();
            var col = bone.Darkened(R() * 0.25f);
            if (roll < 0.45f)
            {
                float l = (0.5f + R() * 0.7f) * size, r = 0.05f * size;
                mb.Tube(new[] { o - d * l * 0.5f, o + d * l * 0.5f }, new[] { r, r * 0.85f }, 6, col, capStart: false);
                mb.Blob(o - d * l * 0.5f, Vector3.One * r * 1.8f, 4, col, noise, 0.2f, 4f);
                mb.Blob(o + d * l * 0.5f, Vector3.One * r * 1.6f, 4, col, noise, 0.2f, 4f);
            }
            else if (roll < 0.8f)
            {
                float r = (0.1f + R() * 0.08f) * size;
                mb.Blob(o, new Vector3(r, r * 0.6f, r), 5, col, noise, 0.25f, 3f);
                mb.Tube(new[] { o + Vector3.Up * r * 0.3f, o + (Vector3.Up * 1.6f + d * 0.4f) * r * 1.4f }, new[] { r * 0.3f, r * 0.08f }, 4, col, capStart: false);
            }
            else
            {
                float l = (0.8f + R() * 0.8f) * size;
                var side = new Vector3(-d.Z, 0f, d.X);
                var pts = new List<Vector3>();
                var rr = new List<float>();
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    pts.Add(o + d * l * (t - 0.5f) + side * MathF.Sin(t * MathF.PI) * l * 0.25f + Vector3.Up * MathF.Sin(t * MathF.PI) * l * 0.12f);
                    rr.Add(0.045f * size * (1f - 0.5f * t));
                }
                mb.Tube(pts, rr, 5, col, capStart: true);
            }
        }
        mb.SmoothNormals();
        return mb;
    }

    public static void AddSphere(MeshBuilder mb, Vector3 c, float r, Color col, int seg = 5)
    {
        int start = mb.Count;
        int rings = seg, sectors = seg * 2;
        for (int i = 0; i <= rings; i++)
        {
            float phi = i / (float)rings * MathF.PI;
            for (int s = 0; s <= sectors; s++)
            {
                float th = s / (float)sectors * Mathf.Tau;
                var d = new Vector3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                mb.Add(c + d * r, d, col);
            }
        }
        for (int i = 0; i < rings; i++)
            for (int s = 0; s < sectors; s++)
            {
                int a = start + i * (sectors + 1) + s, b = a + 1, cc = a + sectors + 1, d = cc + 1;
                mb.Tri(a, b, cc); mb.Tri(b, d, cc);
            }
    }
}
