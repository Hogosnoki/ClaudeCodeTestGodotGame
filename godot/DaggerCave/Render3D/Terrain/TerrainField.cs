using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace DaggerCave;

/// <summary>How a biome's rock is sculpted in 3D (all lengths in metres).</summary>
public sealed class TerrainStyle
{
    /// <summary>Depth of the tunnels behind the play plane at their walls, extra depth in the middle of big rooms, and how fast it grows.</summary>
    public float B0 = 2.6f, Bk = 4.5f, Ba = 4.5f;
    /// <summary>How far floors and ceilings reach toward the camera before the rock's front face, how much that face bulges forward deep in the rock, and how fast.</summary>
    public float Fe = 1.15f, Ck = 2.4f, Ca = 3.5f;
    /// <summary>Rounding of the tunnel's inner corners (floor meets back wall) and of the front lip.</summary>
    public float KFillet = 1.6f, KLip = 0.9f;
    /// <summary>Large-scale lumps, fine detail, horizontal strata and sharp ridges.</summary>
    public float NoiseAmp = 0.6f, NoiseFreq = 0.32f, DetailAmp = 0.2f, DetailFreq = 1.25f;
    public float StrataAmp = 0.1f, StrataFreq = 2.4f, RidgeAmp = 0f, RidgeFreq = 0.6f;
    /// <summary>Dents carved into the camera-facing rock.</summary>
    public float CapNoiseAmp = 0.8f;

    public static TerrainStyle For(BiomeDef b)
    {
        var s = new TerrainStyle();
        switch (b?.Id)
        {
            case BiomeId.Entrance: s.StrataAmp = 0.16f; s.NoiseAmp = 0.7f; break;
            case BiomeId.Den: s.NoiseAmp = 0.75f; s.Bk = 5.5f; break;
            case BiomeId.Nest: s.NoiseAmp = 0.65f; s.DetailAmp = 0.26f; break;
            case BiomeId.Ruins: s.NoiseAmp = 0.22f; s.DetailAmp = 0.06f; s.StrataAmp = 0.02f; s.KFillet = 0.5f; s.KLip = 0.35f; s.CapNoiseAmp = 0.35f; break;
            case BiomeId.Fungal: s.NoiseAmp = 0.8f; s.Bk = 5.5f; s.DetailAmp = 0.16f; break;
            case BiomeId.Tunnels: s.B0 = 2.2f; s.Bk = 3.5f; s.StrataAmp = 0.18f; break;
            case BiomeId.Frost: s.NoiseAmp = 0.45f; s.DetailAmp = 0.1f; s.StrataAmp = 0.04f; s.KFillet = 2f; break;
            case BiomeId.Crystal: s.RidgeAmp = 0.35f; s.DetailAmp = 0.12f; s.StrataAmp = 0.02f; break;
            case BiomeId.Magma: s.RidgeAmp = 0.3f; s.DetailAmp = 0.28f; s.StrataAmp = 0.05f; break;
            // fibrous, root-riven rock; and old sediment, laid down in thick layers
            case BiomeId.Roots: s.NoiseAmp = 0.7f; s.DetailAmp = 0.24f; s.RidgeAmp = 0.18f; s.RidgeFreq = 0.9f; s.StrataAmp = 0.04f; break;
            case BiomeId.Fossils: s.NoiseAmp = 0.5f; s.Bk = 6f; s.StrataAmp = 0.2f; s.StrataFreq = 1.6f; s.DetailAmp = 0.14f; break;
            // blasted, braced rock: squarer than a natural cave
            case BiomeId.Mine: s.NoiseAmp = 0.3f; s.DetailAmp = 0.1f; s.StrataAmp = 0.1f; s.KFillet = 0.8f; break;
            case BiomeId.Lair: s.RidgeAmp = 0.3f; s.DetailAmp = 0.26f; s.Bk = 7f; s.StrataAmp = 0.05f; break;
        }
        return s;
    }
}

/// <summary>
/// The cave as a 3D volume. The 2D field (openness sampled on the cave's cell corners) is turned
/// into a signed distance S to the collision contour (positive in rock, metres), then extruded:
/// open regions become tunnels between a back wall (deeper in the middle of big rooms) and the
/// camera, rock becomes a mass whose front face bulges toward the camera, with rounded inner
/// corners and a rounded front lip. Noise relief fades out within ~0.35 m of the play plane, so
/// the surface there is exactly the collision line and feet sit on the ground you see.
/// Cave space is x right, y down (cells = metres), z toward the camera.
/// </summary>
public sealed class TerrainField
{
    public const float G = 0.5f;           // grid step, metres
    public const float MaxDist = 9f;       // distances saturate here
    /// <summary>Solid rock modelled around the map, so the perspective camera never sees past its edge.</summary>
    public const float Margin = 10f;

    public readonly CaveData Cave;
    public readonly TerrainStyle Style;
    public readonly Noise3 Noise;
    public readonly int Nx, Ny, Nz;        // grid points per axis
    public readonly float OX, OY;          // cave-space position of grid column 0
    public readonly float ZLo;             // z of grid plane 0
    public readonly float[] S, ZBack, ZCap;
    /// <summary>Iso-contour segments in cave metres (a, b pairs) with open-side normals.</summary>
    public readonly List<Vector2> Segs = new();
    public readonly List<Vector2> SegNormals = new();
    private readonly float _reliefBound;

    public TerrainField(CaveData cave, TerrainStyle style)
    {
        Cave = cave;
        Style = style;
        Noise = new Noise3(cave.Seed * 7919 + 17);
        OX = -Margin; OY = -Margin;
        Nx = (int)MathF.Round((cave.W + 2 * Margin) / G) + 1;
        Ny = (int)MathF.Round((cave.H + 2 * Margin) / G) + 1;
        float zlo = -(style.B0 + style.Bk + style.NoiseAmp + style.RidgeAmp + 1.6f);
        float zhi = style.Fe + style.Ck + 1.2f;
        ZLo = -MathF.Ceiling(-zlo / G) * G;
        Nz = (int)MathF.Ceiling((zhi - ZLo) / G) + 1;
        _reliefBound = style.NoiseAmp + style.DetailAmp + style.StrataAmp + style.RidgeAmp * 1.2f + style.CapNoiseAmp + 1.0f;
        S = new float[Nx * Ny];
        ZBack = new float[Nx * Ny];
        ZCap = new float[Nx * Ny];
        ExtractContour();
        ComputeDistance();
        for (int k = 0; k < S.Length; k++)
        {
            float s = S[k];
            ZBack[k] = BackDepth(s);
            ZCap[k] = CapHeight(s);
        }
    }

    public float Z(int k) => ZLo + k * G;

    private float BackDepth(float s) => -(Style.B0 + Style.Bk * (1f - MathF.Exp(-Math.Max(0f, -s) / Style.Ba)));
    private float CapHeight(float s) => Style.Fe + Style.Ck * (1f - MathF.Exp(-Math.Max(0f, s) / Style.Ca));

    // ------------------------------------------------------------------ 2D distance

    private void ExtractContour()
    {
        var c = Cave;
        Span<Vector2> cp = stackalloc Vector2[4];
        Span<float> cv = stackalloc float[4];
        Span<Vector2> poly = stackalloc Vector2[8];
        Span<bool> edge = stackalloc bool[8];
        for (int j = -1; j <= c.H; j++)
            for (int i = -1; i <= c.W; i++)
            {
                cv[0] = c.Corner(i, j); cv[1] = c.Corner(i + 1, j); cv[2] = c.Corner(i + 1, j + 1); cv[3] = c.Corner(i, j + 1);
                bool s0 = cv[0] < 0.5f, s1 = cv[1] < 0.5f, s2 = cv[2] < 0.5f, s3 = cv[3] < 0.5f;
                if (s0 == s1 && s1 == s2 && s2 == s3) continue;
                cp[0] = new Vector2(i, j); cp[1] = new Vector2(i + 1, j); cp[2] = new Vector2(i + 1, j + 1); cp[3] = new Vector2(i, j + 1);
                int n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) & 3;
                    bool sa = cv[k] < 0.5f, sb = cv[k2] < 0.5f;
                    if (sa) { poly[n] = cp[k]; edge[n] = false; n++; }
                    if (sa != sb)
                    {
                        float t = (0.5f - cv[k]) / (cv[k2] - cv[k]);
                        poly[n] = cp[k].Lerp(cp[k2], Math.Clamp(t, 0.02f, 0.98f)); edge[n] = true; n++;
                    }
                }
                for (int k = 0; k < n; k++)
                {
                    int k2 = (k + 1) % n;
                    if (!edge[k] || !edge[k2]) continue;
                    Vector2 a = poly[k], b = poly[k2];
                    if (a.DistanceSquaredTo(b) < 1e-6f) continue;
                    var m = (a + b) * 0.5f;
                    var nrm = new Vector2(-(b - a).Y, (b - a).X).Normalized();
                    if (c.SampleCells(m.X + nrm.X * 0.2f, m.Y + nrm.Y * 0.2f) < c.SampleCells(m.X - nrm.X * 0.2f, m.Y - nrm.Y * 0.2f)) nrm = -nrm;
                    Segs.Add(a); Segs.Add(b);
                    SegNormals.Add(nrm);
                }
            }
    }

    private void ComputeDistance()
    {
        const float bucket = 4f;
        int bw = (int)MathF.Ceiling((Cave.W + 2) / bucket) + 1, bh = (int)MathF.Ceiling((Cave.H + 2) / bucket) + 1;
        int nseg = Segs.Count / 2;
        var count = new int[bw * bh + 1];
        int Bucket(Vector2 m) => Math.Clamp((int)((m.Y + 1) / bucket), 0, bh - 1) * bw + Math.Clamp((int)((m.X + 1) / bucket), 0, bw - 1);
        var segBucket = new int[nseg];
        for (int s = 0; s < nseg; s++) { segBucket[s] = Bucket((Segs[2 * s] + Segs[2 * s + 1]) * 0.5f); count[segBucket[s] + 1]++; }
        for (int k = 1; k < count.Length; k++) count[k] += count[k - 1];
        var fill = new int[bw * bh];
        var list = new int[nseg];
        for (int s = 0; s < nseg; s++) { int b = segBucket[s]; list[count[b] + fill[b]++] = s; }
        var segs = Segs.ToArray();

        Parallel.For(0, Ny, j =>
        {
            for (int i = 0; i < Nx; i++)
            {
                var p = new Vector2(OX + i * G, OY + j * G);
                int bx = Math.Clamp((int)((p.X + 1) / bucket), 0, bw - 1), by = Math.Clamp((int)((p.Y + 1) / bucket), 0, bh - 1);
                float best = MaxDist * MaxDist;
                int rings = (int)MathF.Ceiling(MaxDist / bucket) + 1;
                for (int r = 0; r <= rings; r++)
                {
                    // everything in ring r is at least r-1 buckets away (less half a segment,
                    // since segments are bucketed by their midpoint)
                    float minRing = (r - 1) * bucket - 0.75f;
                    if (minRing > 0 && minRing * minRing > best) break;
                    for (int yy = by - r; yy <= by + r; yy++)
                    {
                        if (yy < 0 || yy >= bh) continue;
                        for (int xx = bx - r; xx <= bx + r; xx++)
                        {
                            if (xx < 0 || xx >= bw) continue;
                            if (Math.Max(Math.Abs(xx - bx), Math.Abs(yy - by)) != r) continue;
                            int bi = yy * bw + xx;
                            for (int q = count[bi]; q < count[bi + 1]; q++)
                            {
                                int s = list[q];
                                float d = DistSq(p, segs[2 * s], segs[2 * s + 1]);
                                if (d < best) best = d;
                            }
                        }
                    }
                }
                float dist = MathF.Sqrt(best);
                bool open = Cave.SampleCells(p.X, p.Y) >= 0.5f;
                float sSeg = open ? -dist : dist;
                // near the contour, use the bilinear field's own first-order distance so S is continuous
                float sLin = LinearDistance(p.X, p.Y);
                float w = W3.SmoothStep(0.15f, 0.45f, dist);
                S[j * Nx + i] = sLin + (sSeg - sLin) * w;
            }
        });
    }

    /// <summary>(0.5 - open) / |grad open|: a signed distance estimate that is exact at the iso-line.</summary>
    private float LinearDistance(float x, float y)
    {
        int i = (int)MathF.Floor(x), j = (int)MathF.Floor(y);
        float fx = x - i, fy = y - j;
        var c = Cave;
        float a = c.Corner(i, j), b = c.Corner(i + 1, j), cc = c.Corner(i, j + 1), d = c.Corner(i + 1, j + 1);
        float v = (a + (b - a) * fx) * (1 - fy) + (cc + (d - cc) * fx) * fy;
        float gx = (b - a) * (1 - fy) + (d - cc) * fy;
        float gy = (cc - a) * (1 - fx) + (d - b) * fx;
        float g = MathF.Max(0.3f, MathF.Sqrt(gx * gx + gy * gy));
        return (0.5f - v) / g;
    }

    private static float DistSq(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp((p - a).Dot(ab) / Math.Max(1e-6f, ab.LengthSquared()), 0f, 1f);
        return p.DistanceSquaredTo(a + ab * t);
    }

    // ------------------------------------------------------------------ 3D field

    /// <summary>Bilinear column values at a cave-space point.</summary>
    public void Column(float x, float y, out float s, out float zb, out float zc)
    {
        float gx = Math.Clamp((x - OX) / G, 0f, Nx - 1.001f), gy = Math.Clamp((y - OY) / G, 0f, Ny - 1.001f);
        int i = (int)gx, j = (int)gy;
        float fx = gx - i, fy = gy - j;
        int k = j * Nx + i;
        s = Bil(S, k, fx, fy);
        zb = Bil(ZBack, k, fx, fy);
        zc = Bil(ZCap, k, fx, fy);
    }

    private float Bil(float[] a, int k, float fx, float fy)
    {
        float v00 = a[k], v10 = a[k + 1], v01 = a[k + Nx], v11 = a[k + Nx + 1];
        return (v00 + (v10 - v00) * fx) * (1 - fy) + (v01 + (v11 - v01) * fx) * fy;
    }

    /// <summary>The field at any cave-space point: positive in rock, negative in air.</summary>
    public float Eval(float x, float y, float z)
    {
        Column(x, y, out float s, out float zb, out float zc);
        return Shape(s, zb, zc, x, y, z);
    }

    /// <summary>The field at a grid point (column values exact).</summary>
    public float EvalGrid(int i, int j, int k)
    {
        int c = Math.Clamp(j, 0, Ny - 1) * Nx + Math.Clamp(i, 0, Nx - 1);
        float x = OX + i * G, y = OY + j * G;
        float s = S[c];
        if (i < 0 || j < 0 || i >= Nx || j >= Ny) s = Math.Max(s, 2f);
        return Shape(s, ZBack[c], ZCap[c], x, y, Z(k));
    }

    /// <summary>The field without its noise relief (cheap; used for soft occlusion).</summary>
    public float EvalBase(float x, float y, float z)
    {
        Column(x, y, out float s, out float zb, out float zc);
        var st = Style;
        return W3.SMin(W3.SMax(s, zb - z, st.KFillet), zc - z, st.KLip);
    }

    private float Shape(float s, float zb, float zc, float x, float y, float z)
    {
        var st = Style;
        float tunnel = W3.SMax(s, zb - z, st.KFillet);   // air inside the tunnel is negative
        float front = zc - z;                              // air in front of the rock face is negative
        float air = W3.SMin(tunnel, front, st.KLip);
        float ramp = W3.SmoothStep(0.35f, 1.5f, MathF.Abs(z));
        // far from the surface the relief can't change which side a point is on: skip it
        if (ramp <= 0f || MathF.Abs(air) > _reliefBound) return air;
        var n = Noise;
        float f = st.NoiseFreq, d = st.DetailFreq;
        float relief = n.Fbm(x * f, y * f, z * f, 3) * st.NoiseAmp
                     + n.Sample(x * d + 31.1f, y * d + 7.3f, z * d + 3.7f) * st.DetailAmp;
        if (st.StrataAmp > 0f)
            relief += MathF.Sin(y * st.StrataFreq + n.Sample(x * 0.15f, y * 0.15f, z * 0.15f) * 2.5f) * st.StrataAmp;
        if (st.RidgeAmp > 0f)
            relief += (n.Ridged(x * st.RidgeFreq, y * st.RidgeFreq, z * st.RidgeFreq, 3) - 0.45f) * st.RidgeAmp * 2f;
        if (z > 0f)
        {
            // in front of the play plane the relief only ever carves away, so nothing rises
            // between the camera and the action
            relief = -MathF.Abs(relief) * 0.8f;
            float cap = W3.SmoothStep(0.2f, 2.5f, s) * st.CapNoiseAmp;
            if (cap > 0f) relief -= (n.Fbm(x * 0.22f + 5f, y * 0.22f, 3f, 2) * 0.5f + 0.5f) * cap;
        }
        return air + relief * ramp;
    }

    /// <summary>Surface normal (pointing out of the rock) in Godot space at a cave-space point.</summary>
    public Vector3 Normal(float x, float y, float z)
    {
        const float h = 0.08f;
        float dx = Eval(x + h, y, z) - Eval(x - h, y, z);
        float dy = Eval(x, y + h, z) - Eval(x, y - h, z);
        float dz = Eval(x, y, z + h) - Eval(x, y, z - h);
        // the field grows into the rock; y flips between cave space and Godot space
        var nrm = new Vector3(-dx, dy, -dz);
        float len = nrm.Length();
        return len > 1e-6f ? nrm / len : Vector3.Back;
    }

    /// <summary>Soft occlusion from the field: how enclosed a surface point is (1 = open, 0 = buried).</summary>
    public float Occlusion(float x, float y, float z, Vector3 nGodot)
    {
        float occ = 0f, wsum = 0f;
        float w = 1f;
        for (int k = 1; k <= 4; k++)
        {
            float d = 0.35f * k * k * 0.5f + 0.2f;
            float px = x + nGodot.X * d, py = y - nGodot.Y * d, pz = z + nGodot.Z * d;
            float free = -EvalBase(px, py, pz);
            occ += w * Math.Clamp((d - free) / d, 0f, 1f);
            wsum += w;
            w *= 0.7f;
        }
        return 1f - occ / wsum;
    }
}
