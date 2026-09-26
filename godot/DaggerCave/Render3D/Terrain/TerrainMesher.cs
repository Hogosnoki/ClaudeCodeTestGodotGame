using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>One chunk's worth of terrain triangles, built off the main thread.</summary>
public sealed class TerrainChunkData
{
    public Vector3[] Verts;
    public Vector3[] Normals;
    public Color[] Colors;
    public int[] Indices;
    public Aabb Bounds;
}

/// <summary>
/// Surface nets over a <see cref="TerrainField"/>: one vertex per grid cell the surface passes
/// through (at the mean of its edge crossings), one quad per grid edge the surface crosses.
/// Watertight by construction, and chunks line up exactly because each evaluates the same
/// deterministic field on shared grid points. Normals come from central differences on the
/// grid (the block is padded so they match across chunk borders).
/// </summary>
public static class TerrainMesher
{
    /// <summary>Godot treats clockwise triangles as front-facing.</summary>
    private static readonly bool ClockwiseFront = true;

    private static readonly int[] EdgeA = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
    private static readonly int[] EdgeB = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };

    public static TerrainChunkData Build(TerrainField f, int ci0, int cj0, int ci1, int cj1)
    {
        // Samples cover grid points [c0 - 2, c1 + 1]: owned edges start at [c0, c1), the cells
        // around them are [c0 - 1, c1 - 1], and their corners need one more point each side
        // for central differences.
        int sx0 = ci0 - 2, sy0 = cj0 - 2;
        int nx = ci1 - ci0 + 4, ny = cj1 - cj0 + 4, nz = f.Nz;
        var v = new float[nx * ny * nz];
        for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    v[(z * ny + y) * nx + x] = f.EvalGrid(sx0 + x, sy0 + y, z);

        float V(int x, int y, int z) => v[(z * ny + y) * nx + x];
        // gradient of the field at a grid point, in cave space (per metre)
        Vector3 Grad(int x, int y, int z)
        {
            int z0 = Math.Max(z - 1, 0), z1 = Math.Min(z + 1, nz - 1);
            return new Vector3(
                (V(x + 1, y, z) - V(x - 1, y, z)) * 0.5f,
                (V(x, y + 1, z) - V(x, y - 1, z)) * 0.5f,
                (V(x, y, z1) - V(x, y, z0)) / Math.Max(1, z1 - z0)) / TerrainField.G;
        }

        int cx = nx - 1, cy = ny - 1, cz = nz - 1;
        var vid = new int[cx * cy * cz];
        Array.Fill(vid, -1);
        var verts = new List<Vector3>(4096);
        var norms = new List<Vector3>(4096);
        var cols = new List<Color>(4096);
        Span<float> c = stackalloc float[8];
        Span<Vector3> g = stackalloc Vector3[8];
        var noise = f.Noise;

        for (int z = 0; z < cz; z++)
            for (int y = 1; y < cy - 1; y++)
                for (int x = 1; x < cx - 1; x++)
                {
                    int mask = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        c[b] = V(x + (b & 1), y + ((b >> 1) & 1), z + ((b >> 2) & 1));
                        if (c[b] > 0f) mask |= 1 << b;
                    }
                    if (mask == 0 || mask == 255) continue;
                    float sx = 0, sy = 0, sz = 0;
                    int cnt = 0;
                    for (int e = 0; e < 12; e++)
                    {
                        int a = EdgeA[e], bb = EdgeB[e];
                        if ((c[a] > 0f) == (c[bb] > 0f)) continue;
                        float t = c[a] / (c[a] - c[bb]);
                        float ax = a & 1, ay = (a >> 1) & 1, az = (a >> 2) & 1;
                        float bx = bb & 1, by = (bb >> 1) & 1, bz = (bb >> 2) & 1;
                        sx += ax + (bx - ax) * t; sy += ay + (by - ay) * t; sz += az + (bz - az) * t;
                        cnt++;
                    }
                    float fx = sx / cnt, fy = sy / cnt, fz = sz / cnt;
                    // trilinear gradient at the vertex
                    for (int b = 0; b < 8; b++) g[b] = Grad(x + (b & 1), y + ((b >> 1) & 1), z + ((b >> 2) & 1));
                    var g00 = g[0].Lerp(g[1], fx); var g10 = g[2].Lerp(g[3], fx);
                    var g01 = g[4].Lerp(g[5], fx); var g11 = g[6].Lerp(g[7], fx);
                    var grad = g00.Lerp(g10, fy).Lerp(g01.Lerp(g11, fy), fz);
                    // the field grows into the rock; cave y points down
                    var n = new Vector3(-grad.X, grad.Y, -grad.Z);
                    float len = n.Length();
                    n = len > 1e-6f ? n / len : Vector3.Back;

                    float px = f.OX + (sx0 + x + fx) * TerrainField.G;
                    float py = f.OY + (sy0 + y + fy) * TerrainField.G;
                    float pz = f.ZLo + (z + fz) * TerrainField.G;
                    vid[(z * cy + y) * cx + x] = verts.Count;
                    verts.Add(new Vector3(px, -py, pz));
                    norms.Add(n);
                    f.Column(px, py, out float s, out _, out _);
                    float capness = W3.SmoothStep(0.3f, 3.5f, s) * W3.SmoothStep(0.2f, 1.2f, pz);
                    float ao = f.Occlusion(px, py, pz, n);
                    float variation = 0.5f + 0.5f * noise.Fbm(px * 0.06f, py * 0.06f, pz * 0.06f + 9f, 2);
                    cols.Add(new Color(capness, ao, variation, 1f));
                }

        var idx = new List<int>(verts.Count * 6);
        int Cell(int x, int y, int z) => vid[(z * cy + y) * cx + x];
        // The four cells around an edge, in the order used below, run clockwise as seen from the
        // side their quad faces when `keep` is true (worked out per axis, with cave y pointing
        // down and Godot's clockwise front faces); otherwise the order is reversed. Winding comes
        // only from which end of the edge is rock, never from the (possibly folded) geometry.
        void Quad(int a, int b, int cc, int d, bool keep)
        {
            if (a < 0 || b < 0 || cc < 0 || d < 0) return;
            if (ClockwiseFront ? !keep : keep) (b, d) = (d, b);
            // split along the shorter diagonal
            if (verts[a].DistanceSquaredTo(verts[cc]) <= verts[b].DistanceSquaredTo(verts[d]))
            {
                idx.Add(a); idx.Add(b); idx.Add(cc);
                idx.Add(a); idx.Add(cc); idx.Add(d);
            }
            else
            {
                idx.Add(b); idx.Add(cc); idx.Add(d);
                idx.Add(b); idx.Add(d); idx.Add(a);
            }
        }

        // owned edges start at local [2, n - 2)
        for (int z = 0; z < nz - 1; z++)
            for (int y = 2; y < ny - 2; y++)
                for (int x = 2; x < nx - 2; x++)
                {
                    bool r0 = V(x, y, z) > 0f;
                    // edge along x: faces +X when the rock is at its low end
                    if (z >= 1 && r0 != V(x + 1, y, z) > 0f)
                        Quad(Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z), r0);
                    // edge along cave y (down): the order faces Godot +Y, right when the rock is below
                    if (z >= 1 && r0 != V(x, y + 1, z) > 0f)
                        Quad(Cell(x - 1, y, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x - 1, y, z), !r0);
                    // edge along z: faces +Z (the camera) when the rock is at its low end
                    if (r0 != V(x, y, z + 1) > 0f)
                        Quad(Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z), r0);
                }

        var data = new TerrainChunkData
        {
            Verts = verts.ToArray(),
            Normals = norms.ToArray(),
            Colors = cols.ToArray(),
            Indices = idx.ToArray(),
        };
        if (data.Verts.Length > 0)
        {
            var min = data.Verts[0]; var max = min;
            foreach (var p in data.Verts) { min = min.Min(p); max = max.Max(p); }
            data.Bounds = new Aabb(min, max - min);
        }
        return data;
    }
}
