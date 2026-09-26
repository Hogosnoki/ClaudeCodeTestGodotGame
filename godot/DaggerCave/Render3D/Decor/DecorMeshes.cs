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
