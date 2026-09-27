using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>A sculpted creature ready to instance: its skinned mesh, skin binds and skeleton.</summary>
public sealed class SculptResult
{
    public ArrayMesh Mesh;
    public Skin Skin;
    public BoneDef[] Bones;
    public Aabb Bounds;
    public int Triangles;
}

/// <summary>
/// A sculpt baked to plain arrays (no engine resources), so it can be computed on a worker
/// thread; <see cref="SculptMesher.Finish"/> turns it into a mesh on the main thread.
/// </summary>
public sealed class SculptData
{
    public MeshBuilder Body;
    public float[] Custom;
    public int[] BoneIdx;
    public float[] Weights;
    public BoneDef[] Bones;
}

/// <summary>
/// Turns a <see cref="Sculptor"/> design into a skinned mesh: the primitives are smooth-unioned
/// into a voxel grid (each only within its own bounds), the surface is extracted with surface
/// nets, and every vertex takes its colour, material and bone weights from the primitives near
/// it, blended by how close each is, so joints bend smoothly and materials meet softly.
/// Vertex data: COLOR = albedo + roughness, CUSTOM0 = metallic, subsurface, glow, detail kind,
/// UV / UV2.x = rest-pose position (textures stick to the skin as it moves).
/// </summary>
public static class SculptMesher
{
    private static readonly int[] EdgeA = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
    private static readonly int[] EdgeB = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };

    public static SculptResult Build(Sculptor s) => Finish(Bake(s));

    /// <summary>Makes the engine mesh and skin (main thread).</summary>
    public static SculptResult Finish(SculptData d)
    {
        var body = d.Body;
        var arrays = body.Arrays(withUv2: true);
        arrays[(int)Mesh.ArrayType.Custom0] = d.Custom;
        arrays[(int)Mesh.ArrayType.Bones] = d.BoneIdx;
        arrays[(int)Mesh.ArrayType.Weights] = d.Weights;
        var fmt = (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift);
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, fmt);
        var skin = new Skin();
        for (int k = 0; k < d.Bones.Length; k++) skin.AddBind(k, new Transform3D(Basis.Identity, -d.Bones[k].Head));
        return new SculptResult { Mesh = mesh, Skin = skin, Bones = d.Bones, Bounds = body.Bounds(), Triangles = body.I.Count / 3 };
    }

    /// <summary>Voxelizes, meshes and weights the sculpt (pure computation; any thread).</summary>
    public static SculptData Bake(Sculptor s)
    {
        var prims = s.Prims;
        float cell = s.Cell;
        // grid bounds from the additive primitives
        var box = new Aabb();
        bool first = true;
        foreach (var p in prims)
        {
            if (p.Subtract) continue;
            box = first ? p.Bounds : box.Merge(p.Bounds);
            first = false;
        }
        box = box.Grow(cell * 2f);
        int nx = (int)MathF.Ceiling(box.Size.X / cell) + 1, ny = (int)MathF.Ceiling(box.Size.Y / cell) + 1, nz = (int)MathF.Ceiling(box.Size.Z / cell) + 1;
        var org = box.Position;
        var v = new float[nx * ny * nz];
        Array.Fill(v, 1e3f);
        var noise = s.Noise;

        float PrimDist(Prim p, Vector3 q)
        {
            float d = p.Dist(q);
            if (p.Bump != 0f)
                d += p.Bump * noise.Fbm(q.X * p.BumpScale, q.Y * p.BumpScale, q.Z * p.BumpScale, 2);
            return d;
        }

        foreach (var p in prims)
        {
            var b = p.Bounds;
            int x0 = Math.Max(0, (int)((b.Position.X - org.X) / cell)), x1 = Math.Min(nx - 1, (int)MathF.Ceiling((b.End.X - org.X) / cell));
            int y0 = Math.Max(0, (int)((b.Position.Y - org.Y) / cell)), y1 = Math.Min(ny - 1, (int)MathF.Ceiling((b.End.Y - org.Y) / cell));
            int z0 = Math.Max(0, (int)((b.Position.Z - org.Z) / cell)), z1 = Math.Min(nz - 1, (int)MathF.Ceiling((b.End.Z - org.Z) / cell));
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = (z * ny + y) * nx + x;
                        var q = org + new Vector3(x, y, z) * cell;
                        float d = PrimDist(p, q);
                        v[i] = p.Subtract ? W3.SMax(v[i], -d, p.Blend) : W3.SMin(v[i], d, p.Blend);
                    }
        }

        // the same union, anywhere (for normals and vertex attributes)
        float Field(Vector3 q)
        {
            float f = 1e3f;
            foreach (var p in prims)
            {
                if (!p.Bounds.HasPoint(q)) continue;
                float d = PrimDist(p, q);
                f = p.Subtract ? W3.SMax(f, -d, p.Blend) : W3.SMin(f, d, p.Blend);
            }
            return f;
        }

        // ---- surface nets
        int cx = nx - 1, cy = ny - 1, cz = nz - 1;
        var vid = new int[cx * cy * cz];
        Array.Fill(vid, -1);
        var pos = new List<Vector3>(8192);
        Span<float> c = stackalloc float[8];
        for (int z = 0; z < cz; z++)
            for (int y = 0; y < cy; y++)
                for (int x = 0; x < cx; x++)
                {
                    int mask = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        c[k] = v[((z + ((k >> 2) & 1)) * ny + y + ((k >> 1) & 1)) * nx + x + (k & 1)];
                        if (c[k] < 0f) mask |= 1 << k;
                    }
                    if (mask == 0 || mask == 255) continue;
                    float sx = 0, sy = 0, sz = 0; int cnt = 0;
                    for (int e = 0; e < 12; e++)
                    {
                        int a = EdgeA[e], bb = EdgeB[e];
                        if ((c[a] < 0f) == (c[bb] < 0f)) continue;
                        float t = c[a] / (c[a] - c[bb]);
                        float ax = a & 1, ay = (a >> 1) & 1, az = (a >> 2) & 1;
                        sx += ax + ((bb & 1) - ax) * t; sy += ay + (((bb >> 1) & 1) - ay) * t; sz += az + (((bb >> 2) & 1) - az) * t;
                        cnt++;
                    }
                    vid[(z * cy + y) * cx + x] = pos.Count;
                    pos.Add(org + new Vector3(x + sx / cnt, y + sy / cnt, z + sz / cnt) * cell);
                }

        var body = new MeshBuilder();
        foreach (var p in pos) body.Add(p, Vector3.Up, Colors.White);
        int Cell(int x, int y, int z) => vid[(z * cy + y) * cx + x];
        // Cells around an edge, in a fixed order; with the solid at the low end of the edge the
        // order is counter-clockwise seen from outside for x and z edges, clockwise for y edges.
        void Quad(int a, int b, int cc, int d, bool keep)
        {
            if (a < 0 || b < 0 || cc < 0 || d < 0) return;
            if (!keep) (b, d) = (d, b);
            if (pos[a].DistanceSquaredTo(pos[cc]) <= pos[b].DistanceSquaredTo(pos[d])) { body.Tri(a, b, cc); body.Tri(a, cc, d); }
            else { body.Tri(b, cc, d); body.Tri(b, d, a); }
        }
        for (int z = 1; z < nz - 1; z++)
            for (int y = 1; y < ny - 1; y++)
                for (int x = 1; x < nx - 1; x++)
                {
                    bool s0 = v[(z * ny + y) * nx + x] < 0f;
                    if (s0 != v[(z * ny + y) * nx + x + 1] < 0f)
                        Quad(Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z), s0);
                    if (s0 != v[(z * ny + y + 1) * nx + x] < 0f)
                        Quad(Cell(x - 1, y, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x - 1, y, z), !s0);
                    if (s0 != v[((z + 1) * ny + y) * nx + x] < 0f)
                        Quad(Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z), s0);
                }

        // ---- per-vertex attributes from the primitives nearby
        int nb = s.Bones.Count;
        var bones = new List<int>(pos.Count * 4);
        var weights = new List<float>(pos.Count * 4);
        var custom = new List<float>(pos.Count * 4);
        var boneW = new float[nb];
        float h = cell * 0.5f;
        for (int k = 0; k < pos.Count; k++)
        {
            var q = pos[k];
            var g = new Vector3(Field(q + new Vector3(h, 0, 0)) - Field(q - new Vector3(h, 0, 0)),
                                Field(q + new Vector3(0, h, 0)) - Field(q - new Vector3(0, h, 0)),
                                Field(q + new Vector3(0, 0, h)) - Field(q - new Vector3(0, 0, h)));
            body.N[k] = g.LengthSquared() > 1e-12f ? g.Normalized() : Vector3.Up;
            // blend weights: primitives within their blend width of the closest one
            float dmin = float.MaxValue;
            foreach (var p in prims)
            {
                if (p.Subtract || !p.Bounds.HasPoint(q)) continue;
                dmin = Math.Min(dmin, p.Dist(q));
            }
            Array.Clear(boneW);
            float wsum = 0f;
            Vector3 col = Vector3.Zero;
            float rough = 0, metal = 0, sss = 0, emit = 0, detail = 0;
            foreach (var p in prims)
            {
                if (p.Subtract || !p.Bounds.HasPoint(q)) continue;
                float d = p.Dist(q) - dmin;
                float sigma = Math.Max(p.Blend, cell) * 0.6f;
                float w = MathF.Exp(-d / sigma);
                if (w < 0.01f) continue;
                wsum += w;
                boneW[p.Bone] += w;
                col += new Vector3(p.Col.R, p.Col.G, p.Col.B) * w;
                var mi = MatInfo.Of(p.Mat);
                rough += mi.Rough * w; metal += mi.Metal * w; sss += mi.Sss * w;
                emit += (p.Emit >= 0 ? p.Emit : mi.Emit) * w; detail += mi.Detail * w;
            }
            if (wsum <= 0f) { wsum = 1f; boneW[0] = 1f; col = Vector3.One * 0.5f; rough = 0.6f; }
            col /= wsum;
            body.C[k] = new Color(col.X, col.Y, col.Z, rough / wsum);
            custom.Add(metal / wsum); custom.Add(sss / wsum); custom.Add(emit / wsum); custom.Add(DominantDetail(prims, q, dmin) / 8f);
            AddTop4(boneW, bones, weights);
        }

        // ---- explicit parts
        foreach (var part in s.Parts)
        {
            int b0 = body.Count;
            var mi = MatInfo.Of(part.Mat);
            float emit = part.Emit >= 0 ? part.Emit : mi.Emit;
            body.Append(part.Mesh, Transform3D.Identity);
            for (int k = 0; k < part.Mesh.Count; k++)
            {
                var col = part.Mesh.C[k];
                body.C[b0 + k] = new Color(col.R, col.G, col.B, mi.Rough);
                custom.Add(mi.Metal); custom.Add(mi.Sss); custom.Add(emit); custom.Add(mi.Detail / 8f);
                var (ba, bb, wa) = part.Binds[k];
                bones.Add(ba); bones.Add(bb); bones.Add(0); bones.Add(0);
                weights.Add(ba == bb ? 1f : wa); weights.Add(ba == bb ? 0f : 1f - wa); weights.Add(0); weights.Add(0);
            }
        }

        // rest positions for texturing
        for (int k = 0; k < body.Count; k++)
        {
            body.UV[k] = new Vector2(body.V[k].X, body.V[k].Y);
            body.UV2[k] = new Vector2(body.V[k].Z, 0f);
        }

        return new SculptData { Body = body, Custom = custom.ToArray(), BoneIdx = bones.ToArray(), Weights = weights.ToArray(), Bones = s.Bones.ToArray() };
    }

    /// <summary>The surface-detail kind of the primitive closest to q (kinds don't blend).</summary>
    private static float DominantDetail(List<Prim> prims, Vector3 q, float dmin)
    {
        foreach (var p in prims)
        {
            if (p.Subtract || !p.Bounds.HasPoint(q)) continue;
            if (p.Dist(q) <= dmin + 1e-5f) return MatInfo.Of(p.Mat).Detail;
        }
        return 0f;
    }

    private static void AddTop4(float[] w, List<int> bones, List<float> weights)
    {
        Span<int> bi = stackalloc int[4];
        Span<float> bw = stackalloc float[4];
        for (int j = 0; j < 4; j++) { bi[j] = 0; bw[j] = 0; }
        for (int b = 0; b < w.Length; b++)
        {
            float x = w[b];
            if (x <= bw[3]) continue;
            int j = 3;
            while (j > 0 && x > bw[j - 1]) { bw[j] = bw[j - 1]; bi[j] = bi[j - 1]; j--; }
            bw[j] = x; bi[j] = b;
        }
        float s = bw[0] + bw[1] + bw[2] + bw[3];
        if (s <= 0) { bw[0] = 1; s = 1; }
        for (int j = 0; j < 4; j++) { bones.Add(bi[j]); weights.Add(bw[j] / s); }
    }
}
