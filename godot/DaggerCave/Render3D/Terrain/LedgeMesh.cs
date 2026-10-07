using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The rock of one generated ledge, meshed the way all the terrain is (the same field, mesher and material), so a ledge that can be
/// broken is made of the same stone as the cave and looks like a part of it: a small field holding only the ledge's own corners is
/// meshed, and what is left of the frame round it is cut away.
/// </summary>
public static class LedgeMesh
{
    /// <summary>The ledge's mesh, and where its origin lies relative to the ledge's centre (cave metres, 3D: x right, y up).</summary>
    public static ArrayMesh Build(CaveData cave, LedgeRec rec, Material mat, out Vector3 offset)
    {
        offset = default;
        if (rec.Cells.Count == 0) return null;
        int stride = cave.W + 1;
        int imin = int.MaxValue, imax = int.MinValue, jmin = int.MaxValue, jmax = int.MinValue;
        foreach (var (idx, _, _) in rec.Cells)
        {
            int i = idx % stride, j = idx / stride;
            imin = Math.Min(imin, i); imax = Math.Max(imax, i); jmin = Math.Min(jmin, j); jmax = Math.Max(jmax, j);
        }
        const int Pad = 7;
        int x0 = imin - Pad, y0 = jmin - Pad, mw = imax - imin + 2 * Pad, mh = jmax - jmin + 2 * Pad;
        var mini = new CaveData { W = mw, H = mh, Seed = cave.Seed + (int)(rec.Cx * 31 + rec.Cy * 17), Biome = cave.Biome, Liquid = Liquid.None, WaterY = 1e9f };
        mini.Open = new float[(mw + 1) * (mh + 1)];
        Array.Fill(mini.Open, 1f);
        foreach (var (idx, _, now) in rec.Cells)
        {
            int k = (idx / stride - y0) * (mw + 1) + (idx % stride - x0);
            mini.Open[k] = Math.Min(mini.Open[k], now);
        }
        var pre = TerrainView.Precompute(mini);
        // (the field's own frame, far out beyond the ledge, is cut away: only triangles about the ledge itself stay)
        float cx = rec.Cx - x0, cyUp = -(rec.Cy - y0), half = rec.Half + 1.6f;
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var idxs = new List<int>();
        foreach (var d in pre.Datas)
        {
            if (d.Indices.Length == 0) continue;
            var remap = new Dictionary<int, int>();
            for (int t = 0; t < d.Indices.Length; t += 3)
            {
                var a = d.Verts[d.Indices[t]]; var b = d.Verts[d.Indices[t + 1]]; var c = d.Verts[d.Indices[t + 2]];
                var m = (a + b + c) / 3f;
                if (Math.Abs(m.X - cx) > half || Math.Abs(m.Y - cyUp) > 2.6f) continue;
                for (int e = 0; e < 3; e++)
                {
                    int vi = d.Indices[t + e];
                    if (!remap.TryGetValue(vi, out int ni))
                    {
                        ni = verts.Count; remap[vi] = ni;
                        // (alone in its own field a slab has nothing near it to shade it: it would glow against the cave's rock, so
                        // it gets the dimmer, occluded values the same surface has in the cave: more baked occlusion, a little depth)
                        var c0 = d.Colors[vi];
                        verts.Add(d.Verts[vi]); norms.Add(d.Normals[vi]); cols.Add(new Color(Math.Max(c0.R, 0.3f), c0.G * 0.55f, c0.B, c0.A));
                    }
                    idxs.Add(ni);
                }
            }
        }
        if (idxs.Count == 0) return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = norms.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idxs.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, mat);
        // the mini field's origin, against the ledge's centre in the cave
        offset = new Vector3(x0 - rec.Cx, -(y0 - rec.Cy), 0f);
        return mesh;
    }
}
