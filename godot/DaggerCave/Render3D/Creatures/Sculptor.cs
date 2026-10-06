using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>A bone of a sculpted creature: where its joint sits in the rest pose (model space).</summary>
public sealed class BoneDef
{
    public string Name;
    public int Parent;
    public Vector3 Head;
}

/// <summary>Explicit geometry (teeth, claws, spikes, eyes, membranes) bound to bones.</summary>
public sealed class Part
{
    public MeshBuilder Mesh = new();
    /// <summary>Per vertex: up to two bones and the weight of the first.</summary>
    public readonly List<(int a, int b, float wa)> Binds = new();
    public Mat Mat;
    public float Emit = -1f;
}

/// <summary>
/// The design surface for a creature: a skeleton, signed-distance primitives smooth-unioned into
/// one skin, and explicit parts for anything too thin or sharp for the voxel grid. Model space:
/// the creature faces +X, +Y is up, its right side is +Z, the origin is its gameplay centre.
/// </summary>
public sealed class Sculptor
{
    public readonly List<BoneDef> Bones = new();
    public readonly List<Prim> Prims = new();
    public readonly List<Part> Parts = new();
    /// <summary>
    /// Painters run over the finished skin, vertex by vertex: given the vertex's rest position, its colour and the material
    /// there, they return the colour it ends with (stubble, scuffs, streaks: the texture of a design). They may run on a
    /// worker thread, so they must only read.
    /// </summary>
    public readonly List<Func<Vector3, Color, Mat, Color>> Paints = new();
    private readonly Dictionary<string, int> _byName = new();
    public readonly Noise3 Noise;
    /// <summary>Voxel size in metres (smaller = finer skin, slower build).</summary>
    public float Cell = 0.02f;
    public readonly Random Rng;

    public Sculptor(int seed) { Noise = new Noise3(seed); Rng = new Random(seed); }

    public int this[string name] => _byName[name];
    public bool HasBone(string name) => _byName.ContainsKey(name);

    public int Bone(string name, int parent, Vector3 head)
    {
        Bones.Add(new BoneDef { Name = name, Parent = parent, Head = head });
        _byName[name] = Bones.Count - 1;
        return Bones.Count - 1;
    }

    public int Bone(string name, string parent, Vector3 head) => Bone(name, parent == null ? -1 : this[parent], head);

    public Vector3 Head(int bone) => Bones[bone].Head;

    // ------------------------------------------------------------------ primitives

    private Prim Add(Prim p) { p.Prepare(); Prims.Add(p); return p; }

    /// <summary>A tapered capsule from a to b.</summary>
    public Prim Limb(int bone, Vector3 a, Vector3 b, float ra, float rb, Color col, Mat mat = Mat.Skin, float blend = 0.03f, float bump = 0f)
        => Add(new Prim { Kind = PrimKind.RoundCone, Bone = bone, A = a, B = b, Ra = ra, Rb = rb, Col = col, Mat = mat, Blend = blend, Bump = bump });

    public Prim Ball(int bone, Vector3 c, float r, Color col, Mat mat = Mat.Skin, float blend = 0.03f, float bump = 0f)
        => Add(new Prim { Kind = PrimKind.Sphere, Bone = bone, A = c, Ra = r, Col = col, Mat = mat, Blend = blend, Bump = bump });

    /// <summary>An ellipsoid, rotated by Euler angles in degrees (applied X, then Y, then Z).</summary>
    public Prim Egg(int bone, Vector3 c, Vector3 radii, Color col, Mat mat = Mat.Skin, float blend = 0.03f, Vector3 rotDeg = default, float bump = 0f)
        => Add(new Prim { Kind = PrimKind.Ellipsoid, Bone = bone, A = c, Half = radii, InvRot = Rot(rotDeg).Inverse(), Col = col, Mat = mat, Blend = blend, Bump = bump });

    public Prim Block(int bone, Vector3 c, Vector3 half, float round, Color col, Mat mat = Mat.Skin, float blend = 0.02f, Vector3 rotDeg = default, float bump = 0f)
        => Add(new Prim { Kind = PrimKind.Box, Bone = bone, A = c, Half = half, Round = round, InvRot = Rot(rotDeg).Inverse(), Col = col, Mat = mat, Blend = blend, Bump = bump });

    /// <summary>A ring round a vertical axis (a belt, a collar, a cuff), <paramref name="rx"/> front to back and <paramref name="rz"/> side to side, of thickness radius <paramref name="r"/>; <paramref name="tiltDeg"/> tips it about the front-back axis.</summary>
    public Prim Ring(int bone, Vector3 c, float rx, float rz, float r, Color col, Mat mat = Mat.Cloth, float blend = 0.01f, float tiltDeg = 0f, float bump = 0f)
        => Add(new Prim { Kind = PrimKind.Torus, Bone = bone, A = c, Half = new Vector3(rx, r, rz), Ra = r, InvRot = Rot(new Vector3(tiltDeg, 0, 0)).Inverse(), Col = col, Mat = mat, Blend = blend, Bump = bump });

    /// <summary>Carves a capsule out of what is there so far (mouths, sockets, gouges).</summary>
    public Prim CarveLimb(int bone, Vector3 a, Vector3 b, float ra, float rb, float blend = 0.01f)
        => Add(new Prim { Kind = PrimKind.RoundCone, Bone = bone, A = a, B = b, Ra = ra, Rb = rb, Blend = blend, Subtract = true });

    public Prim CarveBall(int bone, Vector3 c, float r, float blend = 0.01f)
        => Add(new Prim { Kind = PrimKind.Sphere, Bone = bone, A = c, Ra = r, Blend = blend, Subtract = true });

    public static Basis Rot(Vector3 deg) =>
        Basis.FromEuler(new Vector3(Mathf.DegToRad(deg.X), Mathf.DegToRad(deg.Y), Mathf.DegToRad(deg.Z)), EulerOrder.Xyz);

    /// <summary>Several capsules chained through the given points (a tail, a neck, a tentacle), each on its own bone.</summary>
    public void Chain(int[] bones, Vector3[] pts, float[] radii, Color col, Mat mat = Mat.Skin, float blend = 0.03f, float bump = 0f)
    {
        for (int k = 0; k < pts.Length - 1; k++)
            Limb(bones[Math.Min(k, bones.Length - 1)], pts[k], pts[k + 1], radii[k], radii[k + 1], col, mat, blend, bump);
    }

    /// <summary>
    /// Where the skin built so far stands in the +X direction at height <paramref name="y"/> and side <paramref name="z"/>: the first
    /// point, coming in from <paramref name="x1"/> toward <paramref name="x0"/>, that is inside it (so that fine features, which are
    /// finer than the voxels, can be laid on it as explicit parts). The smooth unions round the surface a little, so this is the
    /// hard union's: within a millimetre or two.
    /// </summary>
    public float SurfaceX(float y, float z, float x0 = -0.1f, float x1 = 0.25f)
    {
        for (float x = x1; x >= x0; x -= 0.0005f)
        {
            var q = new Vector3(x, y, z);
            float f = 1e3f;
            foreach (var p in Prims)
            {
                float d = p.Dist(q);
                f = p.Subtract ? Math.Max(f, -d) : Math.Min(f, d);
            }
            if (f < 0f) return x;
        }
        return x0;
    }

    // ------------------------------------------------------------------ explicit parts

    private Part NewPart(Mat mat, float emit = -1f)
    {
        var p = new Part { Mat = mat, Emit = emit };
        Parts.Add(p);
        return p;
    }

    private static void BindAll(Part p, int from, int bone, int bone2 = -1, float wa = 1f)
    {
        for (int k = from; k < p.Mesh.Count; k++) p.Binds.Add((bone, bone2 < 0 ? bone : bone2, bone2 < 0 ? 1f : wa));
    }

    /// <summary>
    /// A curved cone: a tooth, claw, horn, spike or fang, from base toward tip, bending by
    /// <paramref name="curl"/> metres toward <paramref name="bendDir"/> at its middle.
    /// </summary>
    public void Horn(int bone, Vector3 b0, Vector3 tip, float r, Color col, Mat mat = Mat.Claw, Vector3 bendDir = default, float curl = 0f, int sides = 7, int rings = 6, float emit = -1f)
    {
        var p = NewPart(mat, emit);
        var path = new List<Vector3>();
        var radii = new List<float>();
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            var pt = b0.Lerp(tip, t) + bendDir * curl * MathF.Sin(t * MathF.PI) * (1f - t * 0.3f);
            path.Add(pt);
            radii.Add(r * MathF.Pow(1f - t, 0.9f) + (i == rings ? 0f : 0.0005f));
        }
        p.Mesh.Tube(path, radii, sides, col, capStart: true);
        BindAll(p, 0, bone);
    }

    /// <summary>A glossy, glowing eye (a sphere; the glow colour comes from the creature's look).</summary>
    public void Eye(int bone, Vector3 c, float r, Color col, float glow = 1f)
    {
        var p = NewPart(Mat.Eye, glow);
        DecorMeshes.AddSphere(p.Mesh, c, r, col, 6);
        BindAll(p, 0, bone);
    }

    /// <summary>
    /// A thin membrane (bat or dragon wing, fin, web) spanned between rows of points: row k runs
    /// from the leading edge to the trailing edge and follows bone k. Double-sided.
    /// </summary>
    public void Membrane(int[] bones, Vector3[][] rows, Color col, float thickness = 0.004f, Mat mat = Mat.Membrane)
    {
        var p = NewPart(mat);
        var mb = p.Mesh;
        int cols = rows[0].Length;
        for (int side = 0; side < 2; side++)
        {
            int start = mb.Count;
            float off = side == 0 ? thickness : -thickness;
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < cols; c++)
                {
                    mb.Add(rows[r][c] + Vector3.Back * off, Vector3.Back * (side == 0 ? 1 : -1), col, new Vector2(c / (float)(cols - 1), r / (float)(rows.Length - 1)));
                    // weights blend between neighbouring finger bones
                    p.Binds.Add((bones[r], bones[Math.Min(r + 1, rows.Length - 1)], 1f - 0.35f * (c / (float)(cols - 1)) * (r < rows.Length - 1 ? 1 : 0)));
                }
            for (int r = 0; r < rows.Length - 1; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    int a = start + r * cols + c, b = a + 1, cc = a + cols, d = cc + 1;
                    if (side == 0) { mb.Tri(a, b, d); mb.Tri(a, d, cc); }
                    else { mb.Tri(a, d, b); mb.Tri(a, cc, d); }
                }
        }
        mb.SmoothNormals();
        mb.FixWinding(0, mb.I.Count);
    }

    /// <summary>
    /// A double-sided skin stretched over triangles of anchor points (wing membranes, webs, fins).
    /// Each anchor follows its bone; points inside a triangle follow the two nearest anchors'
    /// bones. Edges listed in <paramref name="sagEdges"/> (as anchor pairs) sag inward by
    /// <paramref name="sag"/> metres at their middle, giving scalloped trailing edges.
    /// </summary>
    public void Sheet(Vector3[] pts, int[] bones, (int a, int b, int c)[] tris, int subdiv, Color col,
        float thickness = 0.004f, Mat mat = Mat.Membrane, (int a, int b)[] sagEdges = null, float sag = 0f)
    {
        var p = NewPart(mat);
        var mb = p.Mesh;
        bool IsSag(int a, int b)
        {
            if (sagEdges == null) return false;
            foreach (var e in sagEdges) if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return true;
            return false;
        }
        foreach (var (ia, ib, ic) in tris)
        {
            int[] corner = { ia, ib, ic };
            var faceN = (pts[ib] - pts[ia]).Cross(pts[ic] - pts[ia]).Normalized();
            for (int side = 0; side < 2; side++)
            {
                int start = mb.Count;
                var idx = new Dictionary<(int, int), int>();
                for (int i = 0; i <= subdiv; i++)
                    for (int j = 0; j <= subdiv - i; j++)
                    {
                        float u = i / (float)subdiv, v = j / (float)subdiv, w = 1f - u - v;
                        var pos = pts[ia] * w + pts[ib] * u + pts[ic] * v;
                        // sag the free edges toward the opposite corner
                        if (w < 1e-4f && IsSag(ib, ic)) pos += (pts[ia] - pos).Normalized() * sag * 4f * u * v;
                        if (u < 1e-4f && IsSag(ia, ic)) pos += (pts[ib] - pos).Normalized() * sag * 4f * w * v;
                        if (v < 1e-4f && IsSag(ia, ib)) pos += (pts[ic] - pos).Normalized() * sag * 4f * w * u;
                        pos += faceN * (side == 0 ? thickness : -thickness);
                        idx[(i, j)] = mb.Add(pos, side == 0 ? faceN : -faceN, col, new Vector2(u, v));
                        float[] bw = { w, u, v };
                        int m0 = 0; for (int q = 1; q < 3; q++) if (bw[q] > bw[m0]) m0 = q;
                        int m1 = m0 == 0 ? 1 : 0; for (int q = 0; q < 3; q++) if (q != m0 && bw[q] > bw[m1]) m1 = q;
                        float wa = bw[m0] / Math.Max(1e-5f, bw[m0] + bw[m1]);
                        p.Binds.Add((bones[corner[m0]], bones[corner[m1]], wa));
                    }
                for (int i = 0; i < subdiv; i++)
                    for (int j = 0; j < subdiv - i; j++)
                    {
                        int a = idx[(i, j)], b = idx[(i + 1, j)], c = idx[(i, j + 1)];
                        if (side == 0) mb.Tri(a, b, c); else mb.Tri(a, c, b);
                        if (i + j < subdiv - 1)
                        {
                            int d = idx[(i + 1, j + 1)];
                            if (side == 0) mb.Tri(b, d, c); else mb.Tri(b, c, d);
                        }
                    }
            }
        }
    }

    /// <summary>
    /// Draped cloth (a cloak, a banner) through a grid of points, with a little thickness. Row k
    /// follows bone k. The grid is smoothed and subdivided (Catmull-Rom both ways, <paramref name="sub"/>
    /// steps per span), and points between two rows blend those rows' bones, so the fabric bends
    /// smoothly. The side facing <paramref name="outward"/> at the middle of the grid is the
    /// outside; the inside is a shade darker, like a lining.
    /// </summary>
    public void Cloth(int[] bones, Vector3[][] rows, Color col, Vector3 outward, Mat mat = Mat.Cloth, float thickness = 0.006f, int sub = 3)
    {
        var p = NewPart(mat);
        var mb = p.Mesh;
        int R = rows.Length, C = rows[0].Length;
        int nr = (R - 1) * sub + 1, nc = (C - 1) * sub + 1;
        static Vector3 CatRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
            0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t));
        // across each row first, then down the columns
        var wide = new Vector3[R][];
        for (int r = 0; r < R; r++)
        {
            wide[r] = new Vector3[nc];
            var row = rows[r];
            for (int j = 0; j < nc; j++)
            {
                int c = Math.Min(j / sub, C - 2);
                float t = (j - c * sub) / (float)sub;
                wide[r][j] = CatRom(row[Math.Max(c - 1, 0)], row[c], row[c + 1], row[Math.Min(c + 2, C - 1)], t);
            }
        }
        var pos = new Vector3[nr, nc];
        var bind = new (int, int, float)[nr];
        for (int i = 0; i < nr; i++)
        {
            int r = Math.Min(i / sub, R - 2);
            float t = (i - r * sub) / (float)sub;
            for (int j = 0; j < nc; j++)
                pos[i, j] = CatRom(wide[Math.Max(r - 1, 0)][j], wide[r][j], wide[r + 1][j], wide[Math.Min(r + 2, R - 1)][j], t);
            bind[i] = (bones[r], bones[r + 1], 1f - t);
        }
        // normals from the grid's tangents, all turned to the same side
        var nrm = new Vector3[nr, nc];
        for (int i = 0; i < nr; i++)
            for (int j = 0; j < nc; j++)
            {
                var du = pos[i, Math.Min(j + 1, nc - 1)] - pos[i, Math.Max(j - 1, 0)];
                var dv = pos[Math.Min(i + 1, nr - 1), j] - pos[Math.Max(i - 1, 0), j];
                nrm[i, j] = du.Cross(dv).Normalized();
            }
        if (nrm[nr / 2, nc / 2].Dot(outward) < 0)
            for (int i = 0; i < nr; i++) for (int j = 0; j < nc; j++) nrm[i, j] = -nrm[i, j];

        var lining = col.Darkened(0.3f);
        var idx = new int[2, nr, nc];
        for (int side = 0; side < 2; side++)
        {
            float s = side == 0 ? 1f : -1f;
            for (int i = 0; i < nr; i++)
                for (int j = 0; j < nc; j++)
                {
                    idx[side, i, j] = mb.Add(pos[i, j] + nrm[i, j] * thickness * s, nrm[i, j] * s, side == 0 ? col : lining);
                    p.Binds.Add(bind[i]);
                }
        }
        int start = mb.I.Count;
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < nr - 1; i++)
                for (int j = 0; j < nc - 1; j++)
                    mb.Quad(idx[side, i, j], idx[side, i, j + 1], idx[side, i + 1, j + 1], idx[side, i + 1, j]);
        // the cut edges, so the cloth has a hem rather than a gap; (di, dj) steps inward
        void Edge(int i0, int j0, int i1, int j1, int di, int dj)
        {
            var e0 = (pos[i0, j0] - pos[i0 + di, j0 + dj]).Normalized();
            var e1 = (pos[i1, j1] - pos[i1 + di, j1 + dj]).Normalized();
            int a = mb.Add(pos[i0, j0] + nrm[i0, j0] * thickness, e0, lining);
            int b = mb.Add(pos[i0, j0] - nrm[i0, j0] * thickness, e0, lining);
            int c = mb.Add(pos[i1, j1] - nrm[i1, j1] * thickness, e1, lining);
            int d = mb.Add(pos[i1, j1] + nrm[i1, j1] * thickness, e1, lining);
            p.Binds.Add(bind[i0]); p.Binds.Add(bind[i0]); p.Binds.Add(bind[i1]); p.Binds.Add(bind[i1]);
            mb.Quad(a, b, c, d);
        }
        for (int j = 0; j < nc - 1; j++) { Edge(nr - 1, j, nr - 1, j + 1, -1, 0); Edge(0, j, 0, j + 1, 1, 0); }
        for (int i = 0; i < nr - 1; i++) { Edge(i, 0, i + 1, 0, 0, 1); Edge(i, nc - 1, i + 1, nc - 1, 0, -1); }
        mb.FixWinding(start, mb.I.Count);
    }

    /// <summary>Ready-made skinned geometry (model space): each vertex follows up to two bones (binds[k] = first bone, second bone, weight of the first).</summary>
    public void Skinned(MeshBuilder geo, (int a, int b, float wa)[] binds, Mat mat, float emit = -1f)
    {
        var p = NewPart(mat, emit);
        p.Mesh.Append(geo, Transform3D.Identity);
        p.Binds.AddRange(binds);
    }

    /// <summary>Any MeshBuilder geometry (already in model space) bound rigidly to one bone.</summary>
    public void Rigid(int bone, MeshBuilder geo, Mat mat, float emit = -1f)
    {
        var p = NewPart(mat, emit);
        p.Mesh.Append(geo, Transform3D.Identity);
        BindAll(p, 0, bone);
    }
}
