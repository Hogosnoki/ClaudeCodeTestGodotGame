using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Per vertex, how much of each detail kind (0-3 in Custom1, 4-7 in Custom2) the skin there is: they blend smoothly across a triangle that joins two materials.</summary>
    public float[] Kinds0, Kinds1;
    public int[] BoneIdx;
    public float[] Weights;
    public BoneDef[] Bones;
    /// <summary>Triangles of the skin whose corners follow bones far apart in the skeleton (a weld between limbs: it stretches when they part).</summary>
    public int Bridges;
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
        arrays[(int)Mesh.ArrayType.Custom1] = d.Kinds0;
        arrays[(int)Mesh.ArrayType.Custom2] = d.Kinds1;
        arrays[(int)Mesh.ArrayType.Bones] = d.BoneIdx;
        arrays[(int)Mesh.ArrayType.Weights] = d.Weights;
        var fmt = (Mesh.ArrayFormat)(((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift)
            | ((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom1Shift)
            | ((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom2Shift));
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, fmt);
        var skin = new Skin();
        for (int k = 0; k < d.Bones.Length; k++) skin.AddBind(k, new Transform3D(Basis.Identity, -d.Bones[k].Head));
        return new SculptResult { Mesh = mesh, Skin = skin, Bones = d.Bones, Bounds = body.Bounds(), Triangles = body.I.Count / 3 };
    }

    /// <summary>How many steps apart in the skeleton two bones may be and still blend (the same bone, its parent, its child, a sibling...).</summary>
    private const int BlendHops = 1;

    /// <summary>Test aid: false bakes the old way (every primitive blends with every other), to compare with the audit's count.</summary>
    public static bool Isolate = true;
    /// <summary>Test aid: which bones the audit's welds join.</summary>
    public static bool AuditDetail;
    public static readonly Dictionary<string, int> AuditPairs = new();

    private static bool[,] AllRelated(int n)
    {
        var r = new bool[n, n];
        for (int a = 0; a < n; a++) for (int b = 0; b < n; b++) r[a, b] = true;
        return r;
    }

    /// <summary>Whether primitives on bones i and j may smooth-union and share a vertex's weights: bones within BlendHops of each other in the skeleton tree.</summary>
    /// <summary>The audit counts a triangle as a weld when its corners follow bones more than this many steps apart.</summary>
    private const int AuditHops = 2;

    private static bool[,] Related(Sculptor s, int maxHops)
    {
        int n = s.Bones.Count;
        var depth = new int[n];
        for (int k = 0; k < n; k++) { int d = 0; for (int b = s.Bones[k].Parent; b >= 0; b = s.Bones[b].Parent) d++; depth[k] = d; }
        var rel = new bool[n, n];
        for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                // tree distance: climb the deeper one until they meet
                int x = a, y = b, hops = 0;
                while (x != y && hops <= maxHops + 2)
                {
                    if (x < 0 || (y >= 0 && depth[y] > depth[x])) { y = s.Bones[y].Parent; }
                    else if (y < 0 || depth[x] > depth[y]) { x = s.Bones[x].Parent; }
                    else { x = s.Bones[x].Parent; y = s.Bones[y].Parent; hops++; }
                    hops++;
                    if (x < 0 && y < 0) break;
                }
                rel[a, b] = x == y && hops <= maxHops;
            }
        return rel;
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
        // which primitive (by bone) is nearest in each voxel: primitives only blend into those of bones close to theirs in the
        // skeleton, so an arm resting against a leg is not welded to it by the smooth union (webbing that stretches as they move)
        var own = new short[nx * ny * nz];
        Array.Fill(own, (short)-1);
        var relTrue = Related(s, AuditHops);
        var rel = Isolate ? Related(s, BlendHops) : AllRelated(s.Bones.Count);
        var noise = s.Noise;

        // (a coarse grid of which primitives reach each place, so the per-vertex passes below do not test them all)
        const float GridCell = 0.06f;
        const float GridPad = 0.02f;
        var gridOrg = box.Position - Vector3.One * GridPad;
        int gx = (int)MathF.Ceiling((box.Size.X + 2 * GridPad) / GridCell) + 1, gy = (int)MathF.Ceiling((box.Size.Y + 2 * GridPad) / GridCell) + 1, gz = (int)MathF.Ceiling((box.Size.Z + 2 * GridPad) / GridCell) + 1;
        var gridCells = new List<Prim>[gx * gy * gz];
        foreach (var p in prims)
        {
            var b = p.Bounds;
            int ax = Math.Max(0, (int)((b.Position.X - GridPad - gridOrg.X) / GridCell)), bx = Math.Min(gx - 1, (int)((b.End.X + GridPad - gridOrg.X) / GridCell));
            int ay = Math.Max(0, (int)((b.Position.Y - GridPad - gridOrg.Y) / GridCell)), by = Math.Min(gy - 1, (int)((b.End.Y + GridPad - gridOrg.Y) / GridCell));
            int az = Math.Max(0, (int)((b.Position.Z - GridPad - gridOrg.Z) / GridCell)), bz = Math.Min(gz - 1, (int)((b.End.Z + GridPad - gridOrg.Z) / GridCell));
            for (int z = az; z <= bz; z++)
                for (int y = ay; y <= by; y++)
                    for (int x = ax; x <= bx; x++)
                        (gridCells[(z * gy + y) * gx + x] ??= new List<Prim>()).Add(p);
        }
        var noPrims = new List<Prim>();
        List<Prim> Near(Vector3 q)
        {
            int x = (int)((q.X - gridOrg.X) / GridCell), y = (int)((q.Y - gridOrg.Y) / GridCell), z = (int)((q.Z - gridOrg.Z) / GridCell);
            if (x < 0 || y < 0 || z < 0 || x >= gx || y >= gy || z >= gz) return noPrims;
            return gridCells[(z * gy + y) * gx + x] ?? noPrims;
        }

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
                        if (p.Subtract) { v[i] = W3.SMax(v[i], -d, p.Blend); continue; }
                        int o = own[i];
                        bool near = o < 0 || rel[o, p.Bone];
                        float nv = near ? W3.SMin(v[i], d, p.Blend) : Math.Min(v[i], d);
                        if (d < v[i]) own[i] = (short)p.Bone;
                        v[i] = nv;
                    }
        }

        // where parts of far-apart bones touch or all but touch (a hand on a thigh), the skin of each is pulled back from the
        // other by a voxel or two, so there are two surfaces with a hairline between, not one welded web that stretches when
        // they part (a surface-net cell spanning the hairline would still join them: hence the margin)
        {
            var cut = new List<int>();
            for (int z = 2; z < nz - 2; z++)
                for (int y = 2; y < ny - 2; y++)
                    for (int x = 2; x < nx - 2; x++)
                    {
                        int i = (z * ny + y) * nx + x;
                        if (v[i] >= 0f || own[i] < 0) continue;
                        int o = own[i];
                        bool carve = false;
                        for (int dz = -2; dz <= 2 && !carve; dz++)
                            for (int dy = -2; dy <= 2 && !carve; dy++)
                                for (int dx = -2; dx <= 2; dx++)
                                {
                                    int j = i + (dz * ny + dy) * nx + dx;
                                    if (v[j] < 0f && own[j] >= 0 && !rel[o, own[j]]) { carve = true; break; }
                                }
                        if (carve) cut.Add(i);
                    }
            foreach (int i in cut) v[i] = cell * 0.6f;
        }

        // the same union, anywhere (for normals and vertex attributes)
        float Field(Vector3 q)
        {
            float f = 1e3f;
            int o = -1;
            foreach (var p in Near(q))
            {
                if (!p.Bounds.HasPoint(q)) continue;
                float d = PrimDist(p, q);
                if (p.Subtract) { f = W3.SMax(f, -d, p.Blend); continue; }
                bool near = o < 0 || rel[o, p.Bone];
                float nf = near ? W3.SMin(f, d, p.Blend) : Math.Min(f, d);
                if (d < f) o = p.Bone;
                f = nf;
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
        var kinds0 = new List<float>(pos.Count * 4);
        var kinds1 = new List<float>(pos.Count * 4);
        var kw = new float[8];
        var boneW = new float[nb];
        var dominant = new int[pos.Count];
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
            int nearest = -1;
            var nearMat = Mat.Skin;
            var near = Near(q);
            foreach (var p in near)
            {
                if (p.Subtract || !p.Bounds.HasPoint(q)) continue;
                float dp = p.Dist(q);
                if (dp < dmin) { dmin = dp; nearest = p.Bone; nearMat = p.Mat; }
            }
            Array.Clear(boneW);
            Array.Clear(kw);
            float wsum = 0f;
            Vector3 col = Vector3.Zero;
            float rough = 0, metal = 0, sss = 0, emit = 0, detail = 0;
            foreach (var p in near)
            {
                if (p.Subtract || !p.Bounds.HasPoint(q)) continue;
                // (only the bones near the nearest one's share the vertex: no weight from the far limb it merely touches)
                if (nearest >= 0 && !rel[nearest, p.Bone]) continue;
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
                kw[Math.Clamp((int)mi.Detail, 0, 7)] += w;
            }
            if (wsum <= 0f) { wsum = 1f; boneW[0] = 1f; col = Vector3.One * 0.5f; rough = 0.6f; }
            col /= wsum;
            var painted = new Color(col.X, col.Y, col.Z);
            foreach (var paint in s.Paints) painted = paint(q, painted, nearMat);
            body.C[k] = new Color(painted.R, painted.G, painted.B, rough / wsum);
            custom.Add(metal / wsum); custom.Add(sss / wsum); custom.Add(emit / wsum); custom.Add(DominantDetail(near, q, dmin) / 8f);
            for (int j = 0; j < 4; j++) kinds0.Add(kw[j] / wsum);
            for (int j = 0; j < 4; j++) kinds1.Add(kw[4 + j] / wsum);
            AddTop4(boneW, bones, weights);
            dominant[k] = bones[bones.Count - 4];
        }
        // (how many triangles of the skin join bones that are far apart: a weld)
        int bridges = 0;
        for (int t = 0; t + 2 < body.I.Count; t += 3)
        {
            int a = dominant[body.I[t]], b = dominant[body.I[t + 1]], c2 = dominant[body.I[t + 2]];
            if (!relTrue[a, b] || !relTrue[b, c2] || !relTrue[a, c2])
            {
                bridges++;
                if (AuditDetail) { string key = $"{s.Bones[a].Name}/{s.Bones[b].Name}/{s.Bones[c2].Name}"; AuditPairs[key] = AuditPairs.GetValueOrDefault(key) + 1; }
            }
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
                for (int j = 0; j < 8; j++) (j < 4 ? kinds0 : kinds1).Add(j == (int)mi.Detail ? 1f : 0f);
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

        return new SculptData { Body = body, Custom = custom.ToArray(), Kinds0 = kinds0.ToArray(), Kinds1 = kinds1.ToArray(), BoneIdx = bones.ToArray(), Weights = weights.ToArray(), Bones = s.Bones.ToArray(), Bridges = bridges };
    }

    /// <summary>The surface-detail kind of the primitive closest to q (kinds don't blend).</summary>
    private static float DominantDetail(IEnumerable<Prim> prims, Vector3 q, float dmin)
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
