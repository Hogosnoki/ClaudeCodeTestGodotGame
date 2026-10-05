using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A moray the length of a boat: a blunt, scarred head with a jaw of backward-hooked needles
/// that never quite closes, pale yellow eyes that shine out of its burrow, mottled black-green
/// skin under a coat of slime. The sculpted model is the head and neck; the body is a live tube
/// from the neck back into the burrow, rebuilt every frame as it lunges and retracts.
/// </summary>
public sealed class EelDesign : CreatureDesign
{
    public override string Name => "eel";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 8f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.9f, 0.3f), EyeEnergy = 3.2f,
        Rim = new Color(0.6f, 0.9f, 0.6f), RimEnergy = 0.35f,
        DetailScale = 30f, DetailStrength = 1f, Veins = 0.7f, VeinColor = new Color(0.08f, 0.1f, 0.05f), Wet = 1f,
    };

    private static readonly Color Skin = new(0.2f, 0.26f, 0.15f);
    private static readonly Color Belly = new(0.5f, 0.52f, 0.3f);
    private const float NeckLen = 0.45f, R = 0.17f;
    private int _head, _jaw, _neck;

    protected override void OnBonesBound() { _head = B("head"); _jaw = B("jaw"); _neck = B("neck"); }

    public override void Sculpt(Sculptor s)
    {
        var tooth = new Color(0.9f, 0.88f, 0.75f);
        var gum = new Color(0.4f, 0.12f, 0.12f);
        int neck = s.Bone("neck", -1, new(-NeckLen, 0, 0));
        int head = s.Bone("head", neck, new(-0.05f, 0, 0));
        int jaw = s.Bone("jaw", head, new(0.02f, -0.09f, 0));
        // neck: the start of the body tube (the rest is drawn live)
        s.Limb(neck, new(-NeckLen - 0.05f, 0, 0), new(-0.05f, 0.01f, 0), R, R * 1.05f, Skin, Mat.Skin, 0.04f, bump: 0.004f);
        s.Egg(neck, new(-NeckLen * 0.5f, -0.06f, 0), new(NeckLen * 0.55f, 0.1f, R * 0.95f), Belly, Mat.Skin, 0.05f);
        // head: a long wedge, heavier at the jaw hinge, a bony ridge over each eye, scars
        s.Limb(head, new(-0.02f, 0.02f, 0), new(0.3f, 0.0f, 0), 0.15f, 0.075f, Skin, Mat.Skin, 0.05f, bump: 0.005f);
        s.Egg(head, new(0.05f, 0.05f, 0), new(0.16f, 0.1f, 0.13f), Skin.Darkened(0.15f), Mat.Skin, 0.05f, bump: 0.006f);
        s.Limb(head, new(0.0f, 0.13f, 0.05f), new(0.2f, 0.07f, 0.1f), 0.01f, 0.008f, new Color(0.45f, 0.4f, 0.3f), Mat.Flesh, 0.01f);
        // the gape: a dark gullet, upper gum and hooked needles; the lower jaw with its own
        s.CarveLimb(head, new(0.4f, -0.06f, 0), new(0.06f, -0.06f, 0), 0.045f, 0.06f, 0.012f);
        s.Egg(head, new(0.2f, -0.045f, 0), new(0.14f, 0.02f, 0.07f), gum, Mat.Flesh, 0.02f);
        s.Egg(head, new(0.06f, -0.06f, 0), new(0.06f, 0.05f, 0.06f), new Color(0.06f, 0.02f, 0.02f), Mat.Flesh, 0.02f);
        s.Limb(jaw, new(0.0f, -0.1f, 0), new(0.31f, -0.1f, 0), 0.075f, 0.04f, Belly.Darkened(0.2f), Mat.Skin, 0.03f);
        s.Egg(jaw, new(0.17f, -0.075f, 0), new(0.13f, 0.014f, 0.055f), gum, Mat.Flesh, 0.015f);
        foreach (int sd in new[] { 1, -1 })
        {
            DesignKit.Teeth(s, head, new(0.08f, -0.06f, 0.065f * sd), new(0.33f, -0.045f, 0.022f * sd), (Vector3.Down + Vector3.Left * 0.35f).Normalized(), 7, 0.05f, 0.008f, tooth, 0.6f);
            DesignKit.Teeth(s, jaw, new(0.08f, -0.085f, 0.06f * sd), new(0.31f, -0.08f, 0.02f * sd), (Vector3.Up + Vector3.Left * 0.3f).Normalized(), 6, 0.042f, 0.007f, tooth, 0.5f);
            // eyes sit high on the head under a bony ridge, shining out of the burrow
            var eye = new Vector3(0.14f, 0.1f, 0.085f * sd);
            s.Eye(head, eye, 0.028f, new Color(0.6f, 0.55f, 0.15f), 1f);
            s.Limb(head, eye + new Vector3(-0.06f, 0.03f, -0.01f * sd), eye + new Vector3(0.04f, 0.03f, 0.0f), 0.022f, 0.015f, Skin.Darkened(0.35f), Mat.Bone, 0.02f);
            // gill pore
            s.CarveBall(head, new(-0.08f, -0.02f, 0.14f * sd), 0.022f, 0.006f);
        }
        // a ragged dorsal fin along the neck
        s.Sheet(new[] { new Vector3(-NeckLen - 0.05f, R * 0.9f, 0), new Vector3(-NeckLen * 0.5f, R * 1.45f, 0), new Vector3(-0.02f, R * 0.95f, 0) },
            new[] { neck, neck, head }, new[] { (0, 1, 2) }, 4, Skin.Darkened(0.3f), 0.003f, Mat.Membrane, new[] { (0, 1), (1, 2) }, 0.02f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "lurk";
        float t = a.T, time = a.Time;
        float jaw = 16f + 6f * MathF.Sin(time * 1.3f), sway = 3f * MathF.Sin(time * 1.1f);
        switch (c)
        {
            case "bite":
                // it draws its head back in an S, mouth wide, and then strikes
                jaw = Key(t, (0, 20), (0.3f, 62), (0.5f, 70), (0.62f, 5), (1, 25));
                sway = Key(t, (0, 0), (0.28f, -24f), (0.5f, -24f), (0.62f, 16f), (1, 0));
                break;
            case "hold": jaw = 20f + 10f * MathF.Sin(time * 9f); sway = 6f * MathF.Sin(time * 7f); break;
            case "hurt": jaw = 40f; sway = 15f * Key(t, (0, 0), (0.25f, 1), (1, 0)); break;
            case "death": jaw = 45f * W3.Smooth01(t); sway = 20f * MathF.Sin(t * 12f) * (1 - t); break;
        }
        p.Set(_neck, 0, 0, sway);
        p.Set(_head, 0, 0, sway * 0.5f + jaw * 0.15f);
        p.Set(_jaw, 0, 0, -jaw);
    }

    // ---- the body: a tube from the neck back through the burrow into the rock
    public override void Frame(CreatureModel m, in AnimInput a)
    {
        var eel = a.Owner as Eel;
        var tube = m.GetNodeOrNull<MeshInstance3D>("Body3D");
        if (eel == null) return;
        if (tube == null)
        {
            tube = new MeshInstance3D { Name = "Body3D", TopLevel = true, Layers = 1 | Stage3D.ActorLayer, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            m.AddChild(tube);
            var hole = new MeshInstance3D
            {
                Name = "Burrow", TopLevel = true,
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.01f, 0.01f, 0.012f), Roughness = 1f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            m.AddChild(hole);
        }
        // the neck base in world space: the model's neck joint
        var skel = m.Skel;
        var neckT = skel.GlobalTransform * skel.GetBoneGlobalPose(_neck);
        var neckBase = neckT * new Vector3(-0.05f, 0, 0);
        var neckDir = (neckT.Basis * Vector3.Left).Normalized();
        float scale = m.Scale.X;
        var home = W3.P(eel.Home);
        var wallN = new Vector3(eel.WallNormal.X, -eel.WallNormal.Y, 0).Normalized();
        var burrow = m.GetNode<MeshInstance3D>("Burrow");
        // the hole faces out of the wall, turned partly toward the viewer so it reads as a hole
        burrow.GlobalTransform = new Transform3D(BasisAlong((wallN + Vector3.Back * 0.9f).Normalized()) * Basis.FromScale(new Vector3(0.8f, 0.3f, 0.8f) * scale), home + wallN * 0.03f);
        // lurking: the neck is still in the burrow, no body shows
        if ((neckBase - home).Dot(wallN) < 0.12f * scale) { tube.Visible = false; return; }
        tube.Visible = true;

        // path: from the neck base back along the neck, curving to the burrow, then into the wall
        var into = home - wallN * 1.2f * scale;
        float span = neckBase.DistanceTo(home);
        var pts = new List<Vector3>();
        int n = Math.Clamp((int)(span / 0.18f) + 4, 6, 60);
        var c1 = neckBase + neckDir * span * 0.35f;
        var c2 = home + wallN * span * 0.3f;
        var side = neckDir.Cross(Vector3.Back).Normalized();
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n;
            var q = Bezier(neckBase, c1, c2, home, u);
            // a travelling wave, pinned at both ends
            q += side * MathF.Sin(u * 9f - a.Time * 11f) * 0.12f * scale * MathF.Sin(u * MathF.PI) * Math.Min(1f, span);
            pts.Add(q);
        }
        pts.Add(into);
        BuildTube(tube, pts, R * scale * 1.02f, m.Kit.Material);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float s = 1 - t;
        return a * (s * s * s) + b * (3 * s * s * t) + c * (3 * s * t * t) + d * (t * t * t);
    }

    private static Basis BasisAlong(Vector3 y)
    {
        var x = Math.Abs(y.Z) < 0.9f ? y.Cross(Vector3.Back).Normalized() : Vector3.Right;
        return new Basis(x, y, x.Cross(y));
    }

    private readonly Dictionary<ulong, ArrayMesh> _meshes = new();

    /// <summary>Rebuilds the body tube along the path with the creature's own material and vertex data.</summary>
    private void BuildTube(MeshInstance3D mi, List<Vector3> path, float r, Material mat)
    {
        const int sides = 10;
        int n = path.Count;
        var v = new Vector3[n * (sides + 1)];
        var nr = new Vector3[v.Length];
        var col = new Color[v.Length];
        var uv = new Vector2[v.Length];
        var uv2 = new Vector2[v.Length];
        var custom = new float[v.Length * 4];
        var idx = new List<int>((n - 1) * sides * 6);
        var up = Vector3.Back;
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            var tan = (i == 0 ? path[1] - path[0] : i == n - 1 ? path[n - 1] - path[n - 2] : path[i + 1] - path[i - 1]).Normalized();
            var sx = tan.Cross(up).Normalized();
            if (sx.LengthSquared() < 0.5f) sx = Vector3.Up;
            var sy = sx.Cross(tan);
            if (i > 0) along += path[i].DistanceTo(path[i - 1]);
            float rr = r * (i == n - 1 ? 0.6f : 1f);
            for (int k = 0; k <= sides; k++)
            {
                float ang = k / (float)sides * Mathf.Tau;
                var dir = sx * MathF.Cos(ang) + sy * MathF.Sin(ang);
                int j = i * (sides + 1) + k;
                v[j] = path[i] + dir * rr;
                nr[j] = dir;
                // belly (toward -Y of the screen) paler; roughness in alpha
                float bel = Math.Clamp(-dir.Y, 0f, 1f);
                var c = Skin.Lerp(Belly, bel * bel);
                col[j] = new Color(c.R, c.G, c.B, 0.55f);
                // pseudo rest position so the skin's detail slides along with the body
                uv[j] = new Vector2(along, MathF.Cos(ang) * 0.17f);
                uv2[j] = new Vector2(MathF.Sin(ang) * 0.17f, 0);
                custom[j * 4] = 0f; custom[j * 4 + 1] = 0.55f; custom[j * 4 + 2] = 0f; custom[j * 4 + 3] = 1f / 8f;
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                // clockwise front faces
                idx.Add(a); idx.Add(b); idx.Add(c);
                idx.Add(b); idx.Add(d); idx.Add(c);
            }
        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = v;
        arr[(int)Mesh.ArrayType.Normal] = nr;
        arr[(int)Mesh.ArrayType.Color] = col;
        arr[(int)Mesh.ArrayType.TexUV] = uv;
        arr[(int)Mesh.ArrayType.TexUV2] = uv2;
        arr[(int)Mesh.ArrayType.Custom0] = custom;
        arr[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var fmt = (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift);
        if (mi.Mesh is not ArrayMesh am) { am = new ArrayMesh(); mi.Mesh = am; }
        am.ClearSurfaces();
        am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr, null, null, fmt);
        am.SurfaceSetMaterial(0, mat);
        mi.GlobalTransform = Transform3D.Identity;
    }
}
