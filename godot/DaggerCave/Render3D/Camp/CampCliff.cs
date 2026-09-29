using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The hill with the cave mouth in it. The rock is one sculpted surface, swept along the face from a
/// profile (a foot at the meadow, a cliff that leans back, a shoulder, a rounded crown, a long slope
/// down behind) with the rock's own character worked into it: the great buttresses and hollows,
/// bedding ledges, fractures, crags. The mouth is a hole cut through that surface with a thick rough
/// rim of rock round it; boulders shoulder up against the jambs and the lintel, scree and fallen
/// blocks lie at the foot, ferns and vines hang and grow where rock meets earth. All of it wears one
/// triplanar rock shader (camp_cliff), so there's no stretched or plastic-looking stone.
/// </summary>
public partial class CampScene
{
    private const float ArchHalfW = 3.1f, ArchH = 6.3f, TunnelDepth = 12f;

    private Vector3 CliffRight => new(MouthFacing.Z, 0, -MouthFacing.X);
    /// <summary>A point on the cliff: along the face (to the right seen from the camp), up, and back into the hill.</summary>
    private Vector3 Face(float along, float up, float back) => MouthAt + CliffRight * along + Vector3.Up * up - MouthFacing * back;

    /// <summary>Signed distance (metres, negative inside) from a point on the face to the mouth's outline.</summary>
    private static float ArchDist(float a, float u)
    {
        float wall = ArchH - ArchHalfW, dy = u - wall;
        if (dy <= 0f) return MathF.Abs(a) - ArchHalfW;
        return MathF.Sqrt(a * a + dy * dy) - ArchHalfW;
    }

    // the hill's profile: (metres back into the hill, metres up). Leaning back a little up the face, a
    // shoulder, a rounded crown and a long slope down behind (nobody sees round the back).
    private static readonly Vector2[] Profile =
    {
        new(-3.2f, -1.6f), new(-1.8f, -0.4f), new(-0.9f, 1.4f), new(-0.5f, 4.6f), new(-0.1f, 8.6f), new(0.5f, 11.4f), new(2.2f, 13.4f),
        new(6f, 14.4f), new(12f, 14.2f), new(20f, 12f), new(30f, 7f), new(40f, 2f), new(48f, -2.5f),
    };

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void BuildCliff()
    {
        var noise = new Noise3(19);
        var mat = MakeCliffMaterial();

        // ---- the hill: a grid of (along the face) x (along the profile)
        var rows = new List<(Vector2 pos, Vector2 nrm)>();
        for (int s = 0; s < Profile.Length - 1; s++)
        {
            var p0 = Profile[Math.Max(0, s - 1)]; var p1 = Profile[s]; var p2 = Profile[s + 1]; var p3 = Profile[Math.Min(Profile.Length - 1, s + 2)];
            float len = (p2 - p1).Length();
            float spacing = s <= 5 ? 0.17f : s <= 7 ? 0.55f : 1.6f;
            int steps = Math.Max(2, (int)MathF.Ceiling(len / spacing));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps;
                var pt = CatmullRom(p0, p1, p2, p3, t);
                var ahead = CatmullRom(p0, p1, p2, p3, Math.Min(1f, t + 0.02f)); var behind = CatmullRom(p0, p1, p2, p3, Math.Max(0f, t - 0.02f));
                var tan = (ahead - behind).Normalized();
                // (the outward normal: toward the camp and up)
                var nrm = new Vector2(-tan.Y, tan.X);
                rows.Add((pt, nrm));
            }
        }
        rows.Add((Profile[^1], new Vector2(0, 1)));

        const float aMax = 33f, aStep = 0.17f;
        int cols = (int)(aMax * 2f / aStep) + 1;
        int nr = rows.Count;
        var mb = new MeshBuilder();
        var grid = new int[cols * nr];
        var hole = new bool[cols * nr];
        for (int i = 0; i < cols; i++)
        {
            float a = -aMax + i * aStep;
            // the hill dies away into the meadow at either end
            float k = 1f - W3.SmoothStep(15f, 32f, MathF.Abs(a));
            for (int j = 0; j < nr; j++)
            {
                var (pt, nrm) = rows[j];
                float back = pt.X, up = pt.Y;
                float u0 = up;
                // what the rock does: buttresses and hollows, bedding ledges, fractures, crags (all strongest low on the face)
                float faceness = 1f - W3.SmoothStep(10f, 14f, up);
                float big = noise.Fbm(a * 0.10f, up * 0.085f, back * 0.09f, 4) * 2.4f;
                float mid = noise.Fbm(a * 0.42f, up * 0.42f, back * 0.42f + 3f, 4) * 0.6f;
                float fine = noise.Fbm(a * 1.6f, up * 1.6f, back * 1.6f + 7f, 3) * 0.13f;
                float layer = up * 0.42f + noise.Fbm(a * 0.16f, up * 0.05f, 5f, 2) * 1.5f;
                float saw = layer - MathF.Floor(layer);
                float ledge = (-0.55f * W3.SmoothStep(0.6f, 1f, saw) + 0.14f * saw) * faceness;
                float fracture = -0.3f * (1f - W3.SmoothStep(0f, 0.07f, MathF.Abs(noise.Sample(a * 0.42f, up * 0.05f, 9f)))) * faceness;
                float disp = (big * (0.55f + 0.45f * faceness) + mid + fine + ledge + fracture) * k;
                // the mouth's thick rough rim, and the brow of rock over it
                float d = ArchDist(a, u0);
                bool onFace = back < 3f && up < 11f;
                if (onFace)
                {
                    disp += 1.35f * MathF.Exp(-MathF.Pow(MathF.Max(d, 0f) / 1.5f, 2f));
                    disp += 0.9f * MathF.Exp(-MathF.Pow((u0 - ArchH - 1.1f) / 1.2f, 2f)) * MathF.Exp(-MathF.Pow(a / (ArchHalfW + 2.8f), 2f));
                }
                var pos2 = pt + nrm * disp;
                float bk = pos2.X * (0.3f + 0.7f * k) - 2.8f * (1f - k), uk = pos2.Y * k - 1.7f * (1f - k);
                var p = Face(a, uk, bk);
                // vertex colour: the rock's own shade
                float shade = 0.74f + 0.1f * noise.Fbm(a * 0.3f, up * 0.3f, back * 0.3f, 2);
                var warm = new Color(shade * 1.02f, shade * 0.98f, shade * 0.92f);
                var n3 = (Vector3.Up * nrm.Y - MouthFacing * nrm.X).Normalized();
                grid[i * nr + j] = mb.Add(p, n3, warm);
                hole[i * nr + j] = onFace && up > -0.3f && d + noise.Sample(a * 1.3f, up * 1.3f, 2f) * 0.22f < 0f;
            }
        }
        for (int i = 0; i < cols - 1; i++)
            for (int j = 0; j < nr - 1; j++)
            {
                int a0 = i * nr + j, b0 = (i + 1) * nr + j, c0 = a0 + 1, d0 = b0 + 1;
                // a cell is cut away where the mouth is
                if (hole[a0] && hole[b0] && hole[c0] && hole[d0]) continue;
                // (winding: counter-clockwise seen from the camp; along = right, profile = up the face)
                mb.Tri(grid[a0], grid[b0], grid[c0]); mb.Tri(grid[b0], grid[d0], grid[c0]);
            }
        mb.SmoothNormals();
        // (SmoothNormals may have turned a normal wrong-way where a fold is; the shader only needs them near the truth)
        AddChild(new MeshInstance3D { Name = "Hill", Mesh = mb.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

        BuildBoulders(noise, mat);
        BuildTunnel(noise);
        BuildFoliageOnRock(noise);
    }

    private ShaderMaterial MakeCliffMaterial()
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_cliff.gdshader") };
        mat.SetShaderParameter("wall_tex", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/rock_face/rock_face_diff.jpg"));
        mat.SetShaderParameter("wall_nor", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/rock_face/rock_face_nor.jpg"));
        mat.SetShaderParameter("moss_tex", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/mossy_rock/mossy_rock_diff.jpg"));
        return mat;
    }

    // ================================================================== boulders

    /// <summary>A hewn boulder: a sphere cut by a handful of planes into blunt facets, roughened, sat down with its base buried.</summary>
    private static void Boulder(MeshBuilder mb, Noise3 noise, Vector3 centre, Vector3 radii, float yaw, Color col, int seed, float bury = 0.3f)
    {
        var rng = new Random(seed);
        const int np = 6, seg = 11;
        var pn = new Vector3[np]; var pd = new float[np];
        for (int k = 0; k < np; k++)
        {
            var v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 1.4f - 0.5f, (float)rng.NextDouble() * 2f - 1f);
            pn[k] = v.Normalized();
            pd[k] = 0.7f + 0.28f * (float)rng.NextDouble();
        }
        var basis = new Basis(Vector3.Up, yaw);
        int rings = seg, sectors = seg * 2, start = mb.Count;
        for (int r = 0; r <= rings; r++)
        {
            float phi = r / (float)rings * MathF.PI;
            for (int s = 0; s <= sectors; s++)
            {
                float th = s / (float)sectors * Mathf.Tau;
                var d = new Vector3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                float rad = 1f + 0.12f * noise.Fbm(d.X * 2.1f + centre.X * 0.37f, d.Y * 2.1f + centre.Y * 0.37f, d.Z * 2.1f + centre.Z * 0.37f, 3);
                for (int k = 0; k < np; k++)
                {
                    float dp = d.Dot(pn[k]);
                    if (dp > 1e-3f) rad = MathF.Min(rad, pd[k] / dp);
                }
                var p = new Vector3(d.X * radii.X, d.Y * radii.Y, d.Z * radii.Z) * rad;
                // the bottom is flat, and sunk in the ground
                if (p.Y < -radii.Y * (1f - bury)) p.Y = -radii.Y * (1f - bury);
                p = basis * p;
                mb.Add(centre + p, d, col * (0.9f + 0.2f * (float)rng.NextDouble()));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sectors; s++)
            {
                int a = start + r * (sectors + 1) + s, b = a + 1, c = a + sectors + 1, d = c + 1;
                mb.Tri(a, b, c); mb.Tri(b, d, c);
            }
    }

    private void BuildBoulders(Noise3 noise, ShaderMaterial mat)
    {
        var mb = new MeshBuilder();
        var rng = new Random(311);
        Color Shade(float lo = 0.66f, float hi = 0.82f) { float s = lo + (hi - lo) * (float)rng.NextDouble(); return new Color(s * 1.02f, s, s * 0.94f); }
        int seed = 100;
        void At(float a, float u, float b, float rx, float ry, float rz, float bury = 0.3f)
        {
            var c = Face(a, u, b);
            Boulder(mb, noise, c, new Vector3(rx, ry, rz), (float)rng.NextDouble() * Mathf.Tau, Shade(), seed++, bury);
        }
        // the jambs: blocks shouldering up either side of the mouth, one on another, and the lintel over it
        foreach (int side in new[] { -1, 1 })
        {
            float w = ArchHalfW + 1.55f;
            At(side * w, 1.5f, -1.5f, 1.55f, 1.8f, 1.5f, 0.5f);
            At(side * (w - 0.35f), 4.1f, -1.3f, 1.35f, 1.4f, 1.3f, 0.2f);
            At(side * (w - 0.9f), ArchH + 0.4f, -1.1f, 1.25f, 1.05f, 1.2f, 0.1f);
            At(side * (w + 1.1f), 6.6f, -0.7f, 1.6f, 1.3f, 1.4f, 0.1f);
        }
        At(0.2f, ArchH + 1.5f, -0.9f, 2.5f, 1.0f, 1.3f, 0.1f);
        At(-1.5f, ArchH + 2.5f, -0.2f, 1.7f, 1.1f, 1.2f, 0.1f);
        At(1.6f, ArchH + 2.6f, -0.2f, 1.9f, 1.0f, 1.2f, 0.1f);
        // great blocks fallen along the foot, half sunk, either side
        float[] sizes = { 2.7f, 2.1f, 2.5f, 1.8f, 2.2f, 1.6f };
        for (int k = 0; k < 6; k++)
        {
            int side = k % 2 == 0 ? 1 : -1;
            float a = side * (7.8f + 5.4f * (k / 2));
            float sz = sizes[k];
            At(a, sz * 0.45f, -1.6f - (float)rng.NextDouble() * 1.6f, sz, sz * 0.85f, sz * 0.9f, 0.4f);
        }
        // scree: fallen stones of every size, thick near the jambs and thinning out, none on the path to the mouth
        for (int k = 0; k < 90; k++)
        {
            float a = (float)(rng.NextDouble() * 2 - 1) * 24f;
            if (MathF.Abs(a) < 3.6f) continue;
            float near = 1f - MathF.Min(1f, MathF.Abs(a) / 24f);
            float b = -1.0f - (float)rng.NextDouble() * (2f + 6f * (1f - near) * (float)rng.NextDouble());
            float sz = 0.16f + 0.5f * MathF.Pow((float)rng.NextDouble(), 2.2f) + (k < 12 ? 0.3f : 0f);
            At(a, sz * 0.4f, b, sz * 1.15f, sz * 0.75f, sz, 0.35f);
        }
        mb.SmoothNormals();
        AddChild(new MeshInstance3D { Name = "Boulders", Mesh = mb.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
    }

    // ================================================================== the tunnel

    /// <summary>The way in: a tunnel going back into the hill, its walls rough, lit only at its lip and black beyond.</summary>
    private void BuildTunnel(Noise3 noise)
    {
        var tunnel = new MeshBuilder();
        const int arcN = 26, depthN = 8;
        // the outline: straight sides up to the springing, then a half circle
        static Vector2 Arch(float u)
        {
            float wall = ArchH - ArchHalfW, round = MathF.PI * ArchHalfW;
            float s = u * (2f * wall + round);
            if (s < wall) return new Vector2(-ArchHalfW, s);
            s -= wall;
            if (s < round) { float a = MathF.PI - s / ArchHalfW; return new Vector2(MathF.Cos(a) * ArchHalfW, wall + MathF.Sin(a) * ArchHalfW); }
            return new Vector2(ArchHalfW, wall - (s - round));
        }
        for (int d = 0; d <= depthN; d++)
        {
            float back = -0.7f + d / (float)depthN * (TunnelDepth + 0.7f);
            // (a little wider than the hole at its lip, so the rim hides the join, then closing in)
            float shrink = 1.16f - 0.42f * MathF.Pow(d / (float)depthN, 0.8f);
            float dark = MathF.Pow(1f - d / (float)depthN, 2.0f);
            for (int k = 0; k <= arcN; k++)
            {
                var s = Arch(k / (float)arcN) * shrink;
                float rough = 1f + 0.06f * noise.Fbm(k * 0.6f, d * 0.9f, 4f, 2);
                var p = Face(s.X * rough, s.Y * rough, back);
                var inward = (Face(0f, ArchH * 0.45f, back) - p).Normalized();
                // damp grey stone near the light, warmer earth floor, going to black
                var col = new Color(0.20f, 0.17f, 0.14f) * dark * (0.75f + 0.5f * noise.Fbm(k * 0.5f, d * 0.7f, 8f, 2) * 0.5f + 0.25f);
                tunnel.Add(p, inward, new Color(col.R, col.G, col.B), new Vector2(k / (float)arcN, d / (float)depthN));
            }
        }
        for (int d = 0; d < depthN; d++)
            for (int k = 0; k < arcN; k++)
            {
                int a = d * (arcN + 1) + k, b = a + 1, c = a + arcN + 1, e = c + 1;
                tunnel.Tri(a, b, c); tunnel.Tri(b, e, c);
            }
        int ctr = tunnel.Add(Face(0f, ArchH * 0.4f, TunnelDepth + 1f), MouthFacing, Colors.Black);
        int last = depthN * (arcN + 1);
        for (int k = 0; k < arcN; k++) { tunnel.Tri(ctr, last + k, last + k + 1); tunnel.Tri(ctr, last + k + 1, last + k); }
        tunnel.Tri(ctr, last + arcN, last); tunnel.Tri(ctr, last, last + arcN);
        // the floor, trodden earth going into the dark
        var floorCol = new Color(0.22f, 0.18f, 0.13f);
        int f0 = tunnel.Add(Face(-ArchHalfW * 1.1f, 0.02f, -1.4f), Vector3.Up, floorCol);
        int f1 = tunnel.Add(Face(ArchHalfW * 1.1f, 0.02f, -1.4f), Vector3.Up, floorCol);
        int f2 = tunnel.Add(Face(ArchHalfW * 0.8f, 0.02f, TunnelDepth), Vector3.Up, Colors.Black);
        int f3 = tunnel.Add(Face(-ArchHalfW * 0.8f, 0.02f, TunnelDepth), Vector3.Up, Colors.Black);
        tunnel.Tri(f0, f1, f2); tunnel.Tri(f0, f2, f3); tunnel.Tri(f0, f2, f1); tunnel.Tri(f0, f3, f2);
        var dim = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = true };
        AddChild(new MeshInstance3D { Name = "Tunnel", Mesh = tunnel.ToMesh(dim), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ================================================================== ferns and vines

    private void BuildFoliageOnRock(Noise3 noise)
    {
        var rng = new Random(77);
        // vines hanging from the lintel and the brow: dark green strands with leaves along them
        var vine = new MeshBuilder();
        var leaf = new Color(0.16f, 0.34f, 0.1f);
        for (int k = 0; k < 26; k++)
        {
            float a = (float)(rng.NextDouble() * 2 - 1) * (ArchHalfW + 2.2f);
            float top = ArchDist(a, ArchH + 0.6f) < 0 ? ArchH + 0.6f : ArchH + 1.2f;
            // hang from the brow, in front of the rock
            var start = Face(a, top + (float)rng.NextDouble() * 1.4f, -1.55f - 0.25f * (float)rng.NextDouble());
            float len = 1.2f + 2.6f * (float)rng.NextDouble();
            var path = new List<Vector3>();
            var radii = new List<float>();
            const int n = 7;
            float sway = ((float)rng.NextDouble() - 0.5f) * 0.4f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                path.Add(start + Vector3.Down * len * t + CliffRight * (sway * t * t) + MouthFacing * (0.25f * MathF.Sin(t * 3f + k)));
                radii.Add(0.02f * (1f - 0.5f * t));
            }
            vine.Tube(path, radii, 4, new Color(0.13f, 0.2f, 0.08f), false);
            for (int i = 1; i < n; i++)
            {
                if (rng.NextDouble() < 0.35) continue;
                var p = path[i];
                float r = 0.09f + 0.09f * (float)rng.NextDouble();
                var side = CliffRight * ((float)rng.NextDouble() - 0.5f) * 0.25f;
                DecorMeshes.AddSphere(vine, p + side, r, leaf * (0.8f + 0.5f * (float)rng.NextDouble()), 4);
            }
        }
        vine.SmoothNormals();
        AddChild(new MeshInstance3D
        {
            Name = "Vines", Mesh = vine.ToMesh(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f }),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // ferns: arching fronds in fans, thick where rock meets earth
        var fern = new MeshBuilder();
        for (int f = 0; f < 9; f++)
        {
            float ang = f / 9f * Mathf.Tau + (float)rng.NextDouble() * 0.4f;
            var dir = new Vector3(MathF.Cos(ang), 0, MathF.Sin(ang));
            var side = new Vector3(-dir.Z, 0, dir.X);
            float len = 0.55f + 0.3f * (float)rng.NextDouble(), rise = 0.35f + 0.2f * (float)rng.NextDouble(), w = 0.085f;
            var prev0 = fern.Add(Vector3.Up * 0.02f - side * 0.008f, Vector3.Up, Colors.White, new Vector2(0, 0));
            var prev1 = fern.Add(Vector3.Up * 0.02f + side * 0.008f, Vector3.Up, Colors.White, new Vector2(1, 0));
            const int segs = 5;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                // an arch: up, then over and down
                var c = dir * (len * t) + Vector3.Up * (rise * MathF.Sin(t * 2.4f) * (1f - 0.25f * t) + 0.02f);
                float ww = w * MathF.Sin(MathF.PI * MathF.Pow(t, 0.7f)) + 0.006f;
                int a0 = fern.Add(c - side * ww, Vector3.Up, Colors.White, new Vector2(0, t));
                int a1 = fern.Add(c + side * ww, Vector3.Up, Colors.White, new Vector2(1, t));
                fern.Quad(prev0, prev1, a1, a0);
                fern.Quad(prev0, a0, a1, prev1);
                prev0 = a0; prev1 = a1;
            }
        }
        var fernMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/grass.gdshader") };
        fernMat.SetShaderParameter("root_color", new Color(0.06f, 0.13f, 0.05f));
        fernMat.SetShaderParameter("tip_color", new Color(0.24f, 0.44f, 0.14f));
        fernMat.SetShaderParameter("wind", 0.06f);
        var xf = new List<Transform3D>();
        for (int k = 0; k < 170; k++)
        {
            float a = (float)(rng.NextDouble() * 2 - 1) * 20f;
            // in the lee of the boulders and along the foot, none on the way to the mouth
            if (MathF.Abs(a) < 3.4f) continue;
            float b = -0.6f - (float)rng.NextDouble() * (2.5f + 2f * (float)rng.NextDouble());
            var at = Face(a, 0f, b);
            float s = 0.9f + 1.1f * (float)rng.NextDouble();
            var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(s, s * (0.8f + 0.5f * (float)rng.NextDouble()), s));
            xf.Add(new Transform3D(basis, new Vector3(at.X, Ground(at.X, at.Z) - 0.02f, at.Z)));
        }
        AddChild(Instances(fern.ToMesh(fernMat), xf, null, false));
    }
}
