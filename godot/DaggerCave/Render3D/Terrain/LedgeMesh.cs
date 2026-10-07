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
    public static ArrayMesh Build(CaveData cave, LedgeRec rec, Material mat, out Vector3 offset)
    {
        offset = default;
        var d = rec.MeshData as Data ?? Arrays(cave, FieldWithLedges(cave), rec);
        rec.MeshData = null;
        if (d == null || d.Indices.Length == 0) return null;
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

        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var idxs = new List<int>();
        var remap = new Dictionary<int, int>();
        for (int t = 0; t < d.Indices.Length; t += 3)
        {
            int a = d.Indices[t], b = d.Indices[t + 1], c = d.Indices[t + 2];
            var m = (d.Verts[a] + d.Verts[b] + d.Verts[c]) / 3f;
            var n = d.Normals[a] + d.Normals[b] + d.Normals[c];
            if (n.Z > 0.6f * n.Length() && m.Z < -0.5f) continue;
            if (!Mine(m)) continue;
            foreach (int v in new[] { a, b, c })
            {
                if (!remap.TryGetValue(v, out int nv))
                {
                    nv = verts.Count; remap[v] = nv;
                    verts.Add(d.Verts[v]); norms.Add(d.Normals[v]); cols.Add(d.Colors[v]);
                }
                idxs.Add(nv);
            }
        }
        if (System.Environment.GetEnvironmentVariable("LEDGE_DEBUG") != null)
            GD.Print($"[ledge] at {rec.Cx:0.0},{rec.Cy:0.0} half {rec.Half:0.0}{(rec.Natural ? " (natural)" : "")}: {idxs.Count / 3} of {d.Indices.Length / 3} triangles in its window");
        // (the mesher works in cave space, x right and y up: the ledge's node stands at its centre)
        return new Data { Verts = verts.ToArray(), Normals = norms.ToArray(), Colors = cols.ToArray(), Indices = idxs.ToArray(), Offset = new Vector3(-rec.Cx, rec.Cy, 0f) };
    }
}
