using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Minimal procedural mesh builder: positions, normals, colours, UVs and triangles, plus a few
/// shape primitives (tapered tubes, discs, prisms) and smooth-normal generation. Triangles are
/// added counter-clockwise as seen from their front (the usual maths convention) and flipped to
/// Godot's clockwise front faces on output.
/// </summary>
public sealed class MeshBuilder
{
    public readonly List<Vector3> V = new();
    public readonly List<Vector3> N = new();
    public readonly List<Color> C = new();
    public readonly List<Vector2> UV = new();
    public readonly List<Vector2> UV2 = new();
    public readonly List<int> I = new();

    public int Count => V.Count;

    public int Add(Vector3 p, Vector3 n, Color c, Vector2 uv = default, Vector2 uv2 = default)
    {
        V.Add(p); N.Add(n); C.Add(c); UV.Add(uv); UV2.Add(uv2);
        return V.Count - 1;
    }

    /// <summary>A triangle, counter-clockwise seen from its front.</summary>
    public void Tri(int a, int b, int c) { I.Add(a); I.Add(b); I.Add(c); }

    public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

    /// <summary>Appends another builder's geometry transformed by <paramref name="xf"/>.</summary>
    public void Append(MeshBuilder o, Transform3D xf, Color? tint = null)
    {
        int b0 = V.Count;
        var nb = xf.Basis.Inverse().Transposed();
        for (int k = 0; k < o.V.Count; k++)
        {
            V.Add(xf * o.V[k]);
            N.Add((nb * o.N[k]).Normalized());
            C.Add(tint is Color t ? o.C[k] * t : o.C[k]);
            UV.Add(o.UV[k]); UV2.Add(o.UV2[k]);
        }
        foreach (int i in o.I) I.Add(i + b0);
    }

    /// <summary>Recomputes normals as area-weighted averages of the triangle normals (positions welded by exact equality).</summary>
    public void SmoothNormals()
    {
        var acc = new Vector3[V.Count];
        var weld = new Dictionary<Vector3, List<int>>();
        for (int k = 0; k < I.Count; k += 3)
        {
            var a = V[I[k]]; var b = V[I[k + 1]]; var c = V[I[k + 2]];
            var n = (b - a).Cross(c - a);
            acc[I[k]] += n; acc[I[k + 1]] += n; acc[I[k + 2]] += n;
        }
        for (int k = 0; k < V.Count; k++)
        {
            if (!weld.TryGetValue(V[k], out var list)) weld[V[k]] = list = new List<int>();
            list.Add(k);
        }
        foreach (var list in weld.Values)
        {
            var s = Vector3.Zero;
            foreach (int k in list) s += acc[k];
            s = s.LengthSquared() > 1e-12f ? s.Normalized() : Vector3.Up;
            foreach (int k in list) N[k] = s;
        }
    }

    public Aabb Bounds()
    {
        if (V.Count == 0) return new Aabb();
        var min = V[0]; var max = V[0];
        foreach (var p in V) { min = min.Min(p); max = max.Max(p); }
        return new Aabb(min, max - min);
    }

    public Godot.Collections.Array Arrays(bool withUv2 = false)
    {
        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = V.ToArray();
        arr[(int)Mesh.ArrayType.Normal] = N.ToArray();
        arr[(int)Mesh.ArrayType.Color] = C.ToArray();
        arr[(int)Mesh.ArrayType.TexUV] = UV.ToArray();
        if (withUv2) arr[(int)Mesh.ArrayType.TexUV2] = UV2.ToArray();
        // counter-clockwise (maths) -> clockwise (Godot front faces)
        var idx = new int[I.Count];
        for (int k = 0; k < I.Count; k += 3) { idx[k] = I[k]; idx[k + 1] = I[k + 2]; idx[k + 2] = I[k + 1]; }
        arr[(int)Mesh.ArrayType.Index] = idx;
        return arr;
    }

    public ArrayMesh ToMesh(Material mat = null, bool withUv2 = false)
    {
        var m = new ArrayMesh();
        if (V.Count == 0) return m;
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, Arrays(withUv2));
        if (mat != null) m.SurfaceSetMaterial(0, mat);
        return m;
    }

    // ------------------------------------------------------------------ primitives

    /// <summary>
    /// A tube along a polyline with a radius per point (a cone when the last radius is 0),
    /// optionally capped at the start. <paramref name="radial"/> sides.
    /// </summary>
    public void Tube(IReadOnlyList<Vector3> path, IReadOnlyList<float> radii, int radial, Color col, bool capStart = true, Func<int, int, float> bump = null)
    {
        int n = path.Count;
        // a stable frame along the path
        var t0 = (path[1] - path[0]).Normalized();
        var side = MathF.Abs(t0.Y) < 0.9f ? Vector3.Up.Cross(t0).Normalized() : Vector3.Right.Cross(t0).Normalized();
        int ringStart = V.Count;
        for (int i = 0; i < n; i++)
        {
            var tan = i == 0 ? (path[1] - path[0]) : i == n - 1 ? (path[n - 1] - path[n - 2]) : (path[i + 1] - path[i - 1]);
            tan = tan.Normalized();
            side = (side - tan * side.Dot(tan)).Normalized();
            var up = tan.Cross(side);
            for (int k = 0; k <= radial; k++)
            {
                float a = k / (float)radial * Mathf.Tau;
                var dir = side * MathF.Cos(a) + up * MathF.Sin(a);
                float r = radii[i] * (bump != null ? 1f + bump(i, k % radial) : 1f);
                Add(path[i] + dir * r, dir, col, new Vector2(k / (float)radial, i / (float)(n - 1)));
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < radial; k++)
            {
                // angle increases counter-clockwise about the path, so (a, b, c) faces outward
                int a = ringStart + i * (radial + 1) + k, b = a + 1, c = a + radial + 1, d = c + 1;
                Tri(a, b, c); Tri(b, d, c);
            }
        if (capStart)
        {
            var tan = (path[1] - path[0]).Normalized();
            int ctr = Add(path[0], -tan, col, new Vector2(0.5f, 0f));
            for (int k = 0; k < radial; k++)
            {
                var p0 = V[ringStart + k]; var p1 = V[ringStart + k + 1];
                int a = Add(p0, -tan, col); int b = Add(p1, -tan, col);
                Tri(ctr, b, a);
            }
        }
    }

    /// <summary>A pointed crystal: hexagonal prism with a pyramid tip, base at the origin along +Y.</summary>
    public void Crystal(float radius, float length, float tip, Color col, float twist = 0f)
    {
        const int sides = 6;
        int firstIndex = I.Count;
        var ring0 = new Vector3[sides];
        var ring1 = new Vector3[sides];
        for (int k = 0; k < sides; k++)
        {
            float a = k / (float)sides * Mathf.Tau + twist;
            var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            ring0[k] = d * radius * 0.85f;
            ring1[k] = d * radius + Vector3.Up * (length - tip);
        }
        var apex = Vector3.Up * length;
        for (int k = 0; k < sides; k++)
        {
            int k2 = (k + 1) % sides;
            // flat-shaded facets catch light like a real crystal
            var n = (ring1[k] - ring0[k]).Cross(ring0[k2] - ring0[k]).Normalized();
            if (n.Dot(ring0[k] + ring0[k2]) < 0) n = -n;
            int a = Add(ring0[k], n, col, new Vector2(0, 0)), b = Add(ring0[k2], n, col, new Vector2(1, 0));
            int c = Add(ring1[k2], n, col, new Vector2(1, 0.8f)), d = Add(ring1[k], n, col, new Vector2(0, 0.8f));
            Tri(a, b, c); Tri(a, c, d);
            var nt = (ring1[k2] - ring1[k]).Cross(apex - ring1[k]).Normalized();
            if (nt.Dot(ring1[k] + ring1[k2] - apex) < 0) nt = -nt;
            int e = Add(ring1[k], nt, col, new Vector2(0, 0.8f)), f = Add(ring1[k2], nt, col, new Vector2(1, 0.8f)), g = Add(apex, nt, col, new Vector2(0.5f, 1f));
            Tri(e, f, g);
        }
        // make sure every facet faces outward
        FixWinding(firstIndex, I.Count);
    }

    /// <summary>Flips any triangle in [from, to) whose winding disagrees with its vertex normals.</summary>
    public void FixWinding(int from, int to)
    {
        for (int k = from; k < to; k += 3)
        {
            var a = V[I[k]]; var b = V[I[k + 1]]; var c = V[I[k + 2]];
            var n = (b - a).Cross(c - a);
            var want = N[I[k]] + N[I[k + 1]] + N[I[k + 2]];
            if (n.Dot(want) < 0) (I[k + 1], I[k + 2]) = (I[k + 2], I[k + 1]);
        }
    }

    /// <summary>A bumpy blob (displaced UV sphere), e.g. pebbles and boulders.</summary>
    public void Blob(Vector3 center, Vector3 radii, int seg, Color col, Noise3 noise, float bump, float noiseScale, float flattenBottom = 0f)
    {
        int rings = seg, sectors = seg * 2;
        int start = V.Count;
        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings;
            float phi = v * MathF.PI;
            for (int s = 0; s <= sectors; s++)
            {
                float u = s / (float)sectors;
                float th = u * Mathf.Tau;
                var d = new Vector3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                float k = 1f + bump * noise.Fbm(d.X * noiseScale + center.X, d.Y * noiseScale + center.Y, d.Z * noiseScale + center.Z, 3);
                var p = new Vector3(d.X * radii.X, d.Y * radii.Y, d.Z * radii.Z) * k;
                if (flattenBottom > 0f && p.Y < -radii.Y * (1f - flattenBottom)) p.Y = -radii.Y * (1f - flattenBottom);
                Add(center + p, d, col, new Vector2(u, v));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sectors; s++)
            {
                int a = start + r * (sectors + 1) + s, b = a + 1, c = a + sectors + 1, d = c + 1;
                Tri(a, b, c); Tri(b, d, c);
            }
    }
}
