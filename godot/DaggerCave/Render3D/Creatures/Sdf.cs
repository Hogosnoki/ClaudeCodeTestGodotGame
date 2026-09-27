using System;
using System.Runtime.CompilerServices;
using Godot;

namespace DaggerCave;

/// <summary>Surface kinds creatures are sculpted from; each sets how light behaves on it.</summary>
public enum Mat
{
    Skin, Flesh, Chitin, Bone, Claw, Eye, Membrane, Metal, Cloth, Leather, Rock, Crystal, Ember, Fungus, Fur, Gold, Wood, Slime, Ice, Scale,
}

/// <summary>How each material responds to light (albedo comes from the part's own colour).</summary>
public readonly struct MatInfo
{
    public readonly float Rough, Metal, Sss, Emit, Detail;
    public MatInfo(float rough, float metal, float sss, float emit, float detail) { Rough = rough; Metal = metal; Sss = sss; Emit = emit; Detail = detail; }

    /// <summary>Detail kinds for the creature shader: 0 smooth, 1 skin pores, 2 scales, 3 rough stone,
    /// 4 fibres, 5 eye (glows in the eye colour), 6 ember cracks, 7 crystal.</summary>
    public static MatInfo Of(Mat m) => m switch
    {
        Mat.Skin => new(0.55f, 0f, 0.55f, 0f, 1),
        Mat.Flesh => new(0.35f, 0f, 0.8f, 0f, 1),
        Mat.Chitin => new(0.22f, 0.05f, 0.05f, 0f, 0),
        Mat.Bone => new(0.55f, 0f, 0.25f, 0f, 3),
        Mat.Claw => new(0.3f, 0f, 0.1f, 0f, 0),
        Mat.Eye => new(0.05f, 0f, 0f, 1f, 5),
        Mat.Membrane => new(0.78f, 0f, 0.9f, 0f, 1),
        Mat.Metal => new(0.35f, 0.9f, 0f, 0f, 3),
        Mat.Cloth => new(0.9f, 0f, 0.2f, 0f, 4),
        Mat.Leather => new(0.65f, 0f, 0.1f, 0f, 1),
        Mat.Rock => new(0.85f, 0f, 0f, 0f, 3),
        Mat.Crystal => new(0.08f, 0.1f, 0.3f, 0.35f, 7),
        Mat.Ember => new(0.6f, 0f, 0f, 1f, 6),
        Mat.Fungus => new(0.7f, 0f, 0.6f, 0f, 1),
        Mat.Fur => new(0.95f, 0f, 0.2f, 0f, 4),
        Mat.Gold => new(0.3f, 1f, 0f, 0f, 0),
        Mat.Wood => new(0.8f, 0f, 0f, 0f, 4),
        Mat.Slime => new(0.12f, 0f, 0.9f, 0.1f, 0),
        Mat.Ice => new(0.1f, 0f, 0.6f, 0.05f, 0),
        Mat.Scale => new(0.4f, 0.1f, 0.2f, 0f, 2),
        _ => new(0.6f, 0f, 0f, 0f, 0),
    };
}

public enum PrimKind { RoundCone, Ellipsoid, Box, Sphere }

/// <summary>One sculpting primitive, attached to a bone, in model space (rest pose).</summary>
public sealed class Prim
{
    public PrimKind Kind;
    public int Bone;
    public Vector3 A, B;          // round cone ends / centre (A) for the others
    public float Ra, Rb;          // round cone radii / sphere radius (Ra)
    public Vector3 Half;          // ellipsoid radii / box half extents
    public Basis InvRot = Basis.Identity; // model -> primitive local (ellipsoid, box)
    public float Round;           // box corner rounding
    public float Blend = 0.03f;   // smooth-union width with what came before
    public bool Subtract;
    public Color Col = new(0.6f, 0.55f, 0.5f);
    public Mat Mat = Mat.Skin;
    public float Bump;            // noise displacement amplitude (m)
    public float BumpScale = 30f; // noise frequency (per m)
    public float Emit = -1f;      // override of the material's glow
    public Aabb Bounds;

    // round cone precomputation
    private Vector3 _ba;
    private float _l2, _rr, _a2, _il2;

    public void Prepare()
    {
        float pad = Blend + MathF.Abs(Bump) * 1.5f + 0.01f;
        switch (Kind)
        {
            case PrimKind.RoundCone:
                _ba = B - A; _l2 = Math.Max(_ba.Dot(_ba), 1e-8f); _rr = Ra - Rb; _a2 = _l2 - _rr * _rr; _il2 = 1f / _l2;
                var r = Math.Max(Ra, Rb) + pad;
                var mn = A.Min(B) - Vector3.One * r; var mx = A.Max(B) + Vector3.One * r;
                Bounds = new Aabb(mn, mx - mn);
                break;
            case PrimKind.Sphere:
                Bounds = new Aabb(A - Vector3.One * (Ra + pad), Vector3.One * (Ra + pad) * 2f);
                break;
            default:
            {
                // rotated box/ellipsoid: bound by the rotated extents
                var rot = InvRot.Inverse();
                var e = new Vector3(
                    MathF.Abs(rot.X.X) * Half.X + MathF.Abs(rot.Y.X) * Half.Y + MathF.Abs(rot.Z.X) * Half.Z,
                    MathF.Abs(rot.X.Y) * Half.X + MathF.Abs(rot.Y.Y) * Half.Y + MathF.Abs(rot.Z.Y) * Half.Z,
                    MathF.Abs(rot.X.Z) * Half.X + MathF.Abs(rot.Y.Z) * Half.Y + MathF.Abs(rot.Z.Z) * Half.Z) + Vector3.One * pad;
                Bounds = new Aabb(A - e, e * 2f);
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dist(Vector3 p)
    {
        switch (Kind)
        {
            case PrimKind.Sphere: return (p - A).Length() - Ra;
            case PrimKind.RoundCone: return RoundCone(p);
            case PrimKind.Ellipsoid:
            {
                var q = InvRot * (p - A);
                var r = Half;
                float k0 = new Vector3(q.X / r.X, q.Y / r.Y, q.Z / r.Z).Length();
                float k1 = new Vector3(q.X / (r.X * r.X), q.Y / (r.Y * r.Y), q.Z / (r.Z * r.Z)).Length();
                return k1 > 1e-8f ? k0 * (k0 - 1f) / k1 : -Math.Min(r.X, Math.Min(r.Y, r.Z));
            }
            default:
            {
                var q = InvRot * (p - A);
                var d = new Vector3(MathF.Abs(q.X), MathF.Abs(q.Y), MathF.Abs(q.Z)) - Half + Vector3.One * Round;
                var o = new Vector3(Math.Max(d.X, 0), Math.Max(d.Y, 0), Math.Max(d.Z, 0));
                return o.Length() + Math.Min(Math.Max(d.X, Math.Max(d.Y, d.Z)), 0f) - Round;
            }
        }
    }

    /// <summary>Inigo Quilez's exact round cone (a capsule whose radius changes along its length).</summary>
    private float RoundCone(Vector3 p)
    {
        var pa = p - A;
        float y = pa.Dot(_ba);
        float z = y - _l2;
        var w = pa * _l2 - _ba * y;
        float x2 = w.Dot(w);
        float y2 = y * y * _l2;
        float z2 = z * z * _l2;
        float k = MathF.Sign(_rr) * _rr * _rr * x2;
        if (MathF.Sign(z) * _a2 * z2 > k) return MathF.Sqrt(x2 + z2) * _il2 - Rb;
        if (MathF.Sign(y) * _a2 * y2 < k) return MathF.Sqrt(x2 + y2) * _il2 - Ra;
        return (MathF.Sqrt(x2 * _a2 * _il2) + y * _rr) * _il2 - Ra;
    }
}
