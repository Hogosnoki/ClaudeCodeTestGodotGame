using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace DaggerCave;

/// <summary>
/// The rock of a ledge that can be broken, cut out of the terrain itself: the cave's own field is built once more with every ledge in
/// it (as the cave was before they were lifted out), the mesher runs over a window round each ledge, and the triangles of that ledge
/// are kept. Same field, same seed, same mesher, same material: every vertex has the colour, occlusion, depth and grain the ground
/// beside it would have, so a ledge looks exactly like the rock it stands among. Only the back wall it stood out of is left behind (the
/// terrain has its own there).
/// </summary>
public static class LedgeMesh
{
    /// <summary>One ledge's arrays (worked out apart from the engine, so they can be made on another thread while the loading screen is up).</summary>
    public sealed class Data
    {
        public Vector3[] Verts, Normals;
        public Color[] Colors;
        public int[] Indices;
        /// <summary>Where the arrays' origin lies relative to the ledge's centre (cave metres, 3D: x right, y up).</summary>
        public Vector3 Offset;
        /// <summary>The grass the ground would have on it (which of the decor's tufts, and where, in the arrays' space).</summary>
        public List<(int kind, Transform3D xf)> Grass = new();
    }

    private static readonly object Gate = new();

    /// <summary>The cave's field with every ledge in it (built once a level, and kept on the cave).</summary>
    public static TerrainField FieldWithLedges(CaveData cave)
    {
        lock (Gate)
        {
            if (cave.LedgeFieldPre is TerrainField f) return f;
            var twin = new CaveData { W = cave.W, H = cave.H, Seed = cave.Seed, Biome = cave.Biome, Liquid = cave.Liquid, WaterY = cave.WaterY };
            twin.Open = (float[])cave.Open.Clone();
            foreach (var l in cave.Ledges)
                foreach (var (idx, _, now) in l.Cells)
                    twin.Open[idx] = Math.Min(twin.Open[idx], now);
            f = new TerrainField(twin, TerrainStyle.For(cave.Biome));
            cave.LedgeFieldPre = f;
            return f;
        }
    }

    /// <summary>Every ledge's arrays, in parallel (the level's loading thread calls this).</summary>
    public static void Precompute(CaveData cave)
    {
        if (cave.Ledges.Count == 0) return;
        var field = FieldWithLedges(cave);
        Parallel.For(0, cave.Ledges.Count, k => { var l = cave.Ledges[k]; l.MeshData = Arrays(cave, field, l); });
    }

    /// <summary>The ledge's mesh (from its precomputed arrays when there are some), and where its origin lies relative to its centre.</summary>
    public static ArrayMesh Build(CaveData cave, LedgeRec rec, Material mat, out Vector3 offset, out List<(int kind, Transform3D xf)> grass)
    {
        offset = default; grass = null;
        var d = rec.MeshData as Data ?? Arrays(cave, FieldWithLedges(cave), rec);
        rec.MeshData = null;
        if (d == null || d.Indices.Length == 0) return null;
        grass = d.Grass;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = d.Verts;
        arrays[(int)Mesh.ArrayType.Normal] = d.Normals;
        arrays[(int)Mesh.ArrayType.Color] = d.Colors;
        arrays[(int)Mesh.ArrayType.Index] = d.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, mat);
        offset = d.Offset;
        return mesh;
    }

    /// <summary>Where the rock surface is at depth z near contour point p (cave metres), along the open-side normal n (as the decor finds it).</summary>
    private static bool SurfaceAt(TerrainField f, Vector2 p, Vector2 n, float z, out Vector3 pos, out Vector3 normal)
    {
        pos = default; normal = default;
        float lo = -1.8f, hi = 1.8f;
        if (!(f.Eval(p.X + n.X * lo, p.Y + n.Y * lo, z) > 0f && f.Eval(p.X + n.X * hi, p.Y + n.Y * hi, z) < 0f)) return false;
        for (int k = 0; k < 14; k++)
        {
            float m = (lo + hi) * 0.5f;
            if (f.Eval(p.X + n.X * m, p.Y + n.Y * m, z) > 0f) lo = m; else hi = m;
        }
        float t = (lo + hi) * 0.5f;
        float x = p.X + n.X * t, y = p.Y + n.Y * t;
        pos = new Vector3(x, -y, z);
        normal = f.Normal(x, y, z);
        return true;
    }

    private static Data Arrays(CaveData cave, TerrainField field, LedgeRec rec)
    {
        if (rec.Cells.Count == 0) return null;
        int stride = cave.W + 1;
        var open = field.Cave.Open;
        // the ledge's own rock corners, and the window of the field round them
        var mine = new HashSet<int>();
        int imin = int.MaxValue, imax = int.MinValue, jmin = int.MaxValue, jmax = int.MinValue;
        foreach (var (idx, _, now) in rec.Cells)
        {
            if (now >= 0.5f) continue;
            mine.Add(idx);
            int i = idx % stride, j = idx / stride;
            imin = Math.Min(imin, i); imax = Math.Max(imax, i); jmin = Math.Min(jmin, j); jmax = Math.Max(jmax, j);
        }
        if (mine.Count == 0) return null;
        const float Pad = 3f;
        int ci0 = Math.Max(2, (int)MathF.Floor((imin - Pad - field.OX) / TerrainField.G));
        int ci1 = Math.Min(field.Nx - 2, (int)MathF.Ceiling((imax + Pad - field.OX) / TerrainField.G));
        int cj0 = Math.Max(2, (int)MathF.Floor((jmin - Pad - field.OY) / TerrainField.G));
        int cj1 = Math.Min(field.Ny - 2, (int)MathF.Ceiling((jmax + Pad - field.OY) / TerrainField.G));
        if (ci1 <= ci0 || cj1 <= cj0) return null;
        var d = TerrainMesher.Build(field, ci0, cj0, ci1, cj1);

        // a triangle is the ledge's when the rock corner nearest it is one of the ledge's own (and it lies close to the ledge): the
        // rock round about keeps its own, and the back wall the ledge stood out of (facing the camera, deep in the cave) stays behind
        bool Mine(Vector3 c)
        {
            float x = c.X, y = -c.Y;
            int i0 = (int)MathF.Floor(x - 2f), i1 = (int)MathF.Ceiling(x + 2f), j0 = (int)MathF.Floor(y - 2f), j1 = (int)MathF.Ceiling(y + 2f);
            float best = float.MaxValue; bool ours = false;
            for (int j = Math.Max(0, j0); j <= Math.Min(cave.H, j1); j++)
                for (int i = Math.Max(0, i0); i <= Math.Min(cave.W, i1); i++)
                {
                    int k = j * stride + i;
                    if (open[k] >= 0.5f) continue;
                    float dd = (i - x) * (i - x) + (j - y) * (j - y);
                    if (dd < best) { best = dd; ours = mine.Contains(k); }
                }
            return ours && best <= 1.7f * 1.7f;
        }

        // (the ledge stood out of the back wall: its top and underside bend up into it in a fillet. The terrain keeps its own wall there,
        // so the ledge is cut cleanly by a plane just in front of that bend)
        var st = field.Style;
        float zc = -st.B0 + st.KFillet * 0.75f;
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var idxs = new List<int>();
        var poly = new List<int>(3);
        var clipped = new List<(Vector3 p, Vector3 n, Color c)>(6);
        for (int t = 0; t < d.Indices.Length; t += 3)
        {
            int a = d.Indices[t], b = d.Indices[t + 1], c = d.Indices[t + 2];
            var pa = d.Verts[a]; var pb = d.Verts[b]; var pc = d.Verts[c];
            if (pa.Z < zc && pb.Z < zc && pc.Z < zc) continue;
            if (!Mine((pa + pb + pc) / 3f)) continue;
            // Sutherland-Hodgman against z >= zc (most triangles lie wholly in front and pass through untouched)
            clipped.Clear();
            poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
            for (int e = 0; e < 3; e++)
            {
                int u = poly[e], v = poly[(e + 1) % 3];
                var pu = d.Verts[u]; var pv = d.Verts[v];
                bool iu = pu.Z >= zc, iv = pv.Z >= zc;
                if (iu) clipped.Add((pu, d.Normals[u], d.Colors[u]));
                if (iu != iv)
                {
                    float k = (zc - pu.Z) / (pv.Z - pu.Z);
                    clipped.Add((pu.Lerp(pv, k), d.Normals[u].Lerp(d.Normals[v], k).Normalized(), d.Colors[u].Lerp(d.Colors[v], k)));
                }
            }
            if (clipped.Count < 3) continue;
            int first = verts.Count;
            foreach (var q in clipped) { verts.Add(q.p); norms.Add(q.n); cols.Add(q.c); }
            for (int e = 1; e + 1 < clipped.Count; e++) { idxs.Add(first); idxs.Add(first + e); idxs.Add(first + e + 1); }
        }

        // the grass the ground would carry on it, by the decor's own rule (along the field's contour on walkable ground, denser at the back)
        var data = new Data { Verts = verts.ToArray(), Normals = norms.ToArray(), Colors = cols.ToArray(), Indices = idxs.ToArray(), Offset = new Vector3(-rec.Cx, rec.Cy, 0f) };
        var biome = cave.Biome;
        if (biome != null && biome.Grass > 0f)
        {
            var rng = new Random((int)(rec.Cx * 977 + rec.Cy * 131) ^ cave.Seed);
            float R(float lo, float hi) => lo + (hi - lo) * (float)rng.NextDouble();
            float walkable = MathF.Cos(Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees)) - 0.01f;
            float waterRow = cave.WaterY / CaveData.Cell;
            var segs = field.Segs; var nrms = field.SegNormals;
            for (int sIdx = 0; sIdx < nrms.Count; sIdx++)
            {
                var sa = segs[2 * sIdx]; var sb = segs[2 * sIdx + 1]; var n = nrms[sIdx];
                var mid = (sa + sb) * 0.5f;
                if (mid.X < imin - 2 || mid.X > imax + 2 || mid.Y < jmin - 2 || mid.Y > jmax + 2) continue;
                if (-n.Y <= walkable || (cave.Liquid != Liquid.None && mid.Y > waterRow)) continue;
                if (!Mine(new Vector3(mid.X, -mid.Y, 0f))) continue;
                int tufts = (int)(biome.Grass * 3.2f * sa.DistanceTo(sb) + rng.NextDouble());
                for (int k = 0; k < tufts; k++)
                {
                    float z = rng.NextDouble() < 0.7 ? R(-2.4f, -0.15f) : R(0.1f, 0.9f);
                    if (z < zc + 0.15f) continue;
                    if (!SurfaceAt(field, sa.Lerp(sb, (float)rng.NextDouble()), n, z, out var pos, out var nn)) continue;
                    float sc = z > 0 ? R(0.35f, 0.65f) : R(0.7f, 1.5f);
                    var up = nn.Lerp(Vector3.Up, 0.6f).Normalized();
                    var side = MathF.Abs(up.Dot(Vector3.Back)) < 0.95f ? up.Cross(Vector3.Back).Normalized() : up.Cross(Vector3.Right).Normalized();
                    var basis = new Basis(side, up, side.Cross(up)) * new Basis(Vector3.Up, R(0f, Mathf.Tau));
                    data.Grass.Add((rng.Next(3), new Transform3D(basis.Scaled(Vector3.One * sc), pos)));
                }
            }
        }
        if (System.Environment.GetEnvironmentVariable("LEDGE_DEBUG") != null)
            GD.Print($"[ledge] at {rec.Cx:0.0},{rec.Cy:0.0} half {rec.Half:0.0}{(rec.Natural ? " (natural)" : "")}: {idxs.Count / 3} of {d.Indices.Length / 3} triangles in its window, {data.Grass.Count} tufts of grass");
        // (the mesher works in cave space, x right and y up: the ledge's node stands at its centre)
        return data;
    }
}
