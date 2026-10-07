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
    public static ArrayMesh Build(CaveData cave, LedgeRec rec, Material mat, out Vector3 offset, float backZ = -3f)
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
        // (alone in its own field the slab stands before a back wall of its own, which would show as a patch of wall in front of the
        // cave's real one, lit and shadowed on its own: everything behind a plane just behind the slab is cut away, cleanly, and the
        // plane is no deeper than the real back wall)
        float zc = Math.Max(backZ + 0.1f, -1.6f);
        var poly = new List<(Vector3 p, Vector3 n, Color c)>(6);
        var clipped = new List<(Vector3 p, Vector3 n, Color c)>(6);
        foreach (var d in pre.Datas)
        {
            if (d.Indices.Length == 0) continue;
            for (int t = 0; t < d.Indices.Length; t += 3)
            {
                var m = (d.Verts[d.Indices[t]] + d.Verts[d.Indices[t + 1]] + d.Verts[d.Indices[t + 2]]) / 3f;
                if (Math.Abs(m.X - cx) > half || Math.Abs(m.Y - cyUp) > rec.Thick * 0.5f + 2.4f) continue;
                poly.Clear();
                for (int e = 0; e < 3; e++)
                {
                    int vi = d.Indices[t + e];
                    // (alone in its own field a slab has nothing near it to shade it, and its face looks thin: it would glow against the
                    // cave's rock. The camera-facing side is given the depth of a rock mass's face (it recedes into darkness as the cave's
                    // own walls do) and every surface the occlusion the same rock has beside others)
                    var c0 = d.Colors[vi];
                    float face = Math.Clamp((d.Normals[vi].Z - 0.2f) / 0.4f, 0f, 1f);
                    poly.Add((d.Verts[vi], d.Normals[vi], new Color(Math.Max(c0.R, 0.25f + 0.4f * face), c0.G * 0.5f, c0.B, c0.A)));
                }
                // Sutherland-Hodgman against z >= zc
                clipped.Clear();
                for (int e = 0; e < poly.Count; e++)
                {
                    var cur = poly[e]; var nxt = poly[(e + 1) % poly.Count];
                    bool ci = cur.p.Z >= zc, ni2 = nxt.p.Z >= zc;
                    if (ci) clipped.Add(cur);
                    if (ci != ni2)
                    {
                        float k = (zc - cur.p.Z) / (nxt.p.Z - cur.p.Z);
                        clipped.Add((cur.p.Lerp(nxt.p, k), cur.n.Lerp(nxt.n, k).Normalized(), cur.c.Lerp(nxt.c, k)));
                    }
                }
                if (clipped.Count < 3) continue;
                int first = verts.Count;
                foreach (var v in clipped) { verts.Add(v.p); norms.Add(v.n); cols.Add(v.c); }
                for (int e = 1; e + 1 < clipped.Count; e++) { idxs.Add(first); idxs.Add(first + e); idxs.Add(first + e + 1); }
            }
        }
        if (System.Environment.GetEnvironmentVariable("LEDGE_DEBUG") != null)
        {
            float zmin = 1e9f, zmax = -1e9f; int back = 0, front = 0, top = 0, other = 0;
            for (int t = 0; t < idxs.Count; t += 3)
            {
                var m = (verts[idxs[t]] + verts[idxs[t + 1]] + verts[idxs[t + 2]]) / 3f; var nn = (norms[idxs[t]] + norms[idxs[t + 1]] + norms[idxs[t + 2]]).Normalized();
                zmin = Math.Min(zmin, m.Z); zmax = Math.Max(zmax, m.Z);
                if (nn.Z > 0.5f && m.Z < -1f) back++; else if (nn.Z > 0.5f) front++; else if (nn.Y < -0.5f) top++; else other++;
            }
            GD.Print($"[ledge] half {rec.Half:0.0} tris {idxs.Count / 3}: z {zmin:0.0}..{zmax:0.0}, back-facing-camera-at-depth {back}, camera-facing {front}, up {top}, other {other}");
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
