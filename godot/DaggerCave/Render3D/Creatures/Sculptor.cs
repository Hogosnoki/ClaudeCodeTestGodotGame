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

    /// <summary>Any MeshBuilder geometry (already in model space) bound rigidly to one bone.</summary>
    public void Rigid(int bone, MeshBuilder geo, Mat mat, float emit = -1f)
    {
        var p = NewPart(mat, emit);
        p.Mesh.Append(geo, Transform3D.Identity);
        BindAll(p, 0, bone);
    }
}
