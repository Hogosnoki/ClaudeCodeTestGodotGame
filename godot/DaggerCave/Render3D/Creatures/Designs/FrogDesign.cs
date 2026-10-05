using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A cave toad as big as a boar: a bloated, warty body of olive and bruise-brown over a sickly
/// yellow belly, poison glands swelling behind bulging amber eyes with black slit pupils, a
/// mouth that splits the head, a throat sac that balloons when it croaks, and a thick tongue
/// that whips out further than you think.
/// </summary>
public sealed class FrogDesign : CreatureDesign
{
    public override string Name => "frog";
    public override float Cell => 0.014f;
    public override float ThreeQuarter => 26f;
    public override float FloorY => Floor;
    private const float Floor = -0.5625f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.42f, 0.04f), EyeEnergy = 1.8f,
        Glow = new Color(0.6f, 1f, 0.25f), GlowEnergy = 1.8f,
        Rim = new Color(0.6f, 0.8f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 26f, DetailStrength = 1.3f, Veins = 0.35f, VeinColor = new Color(0.18f, 0.12f, 0.05f), Wet = 0.75f,
    };

    private int _body, _head, _jaw, _sac;
    private readonly int[] _thigh = new int[2], _shin = new int[2], _foot = new int[2], _arm = new int[2], _fore = new int[2];

    protected override void OnBonesBound()
    {
        _body = B("body"); _head = B("head"); _jaw = B("jaw"); _sac = B("sac");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _thigh[k] = B("thigh" + s); _shin[k] = B("shin" + s); _foot[k] = B("foot" + s); _arm[k] = B("arm" + s); _fore[k] = B("fore" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var olive = new Color(0.17f, 0.18f, 0.1f);
        var bruise = new Color(0.16f, 0.12f, 0.09f);
        var belly = new Color(0.48f, 0.44f, 0.28f);
        var gland = new Color(0.3f, 0.34f, 0.12f);
        var mouth = new Color(0.45f, 0.14f, 0.14f);
        int body = s.Bone("body", -1, new(-0.05f, -0.3f, 0));
        int head = s.Bone("head", body, new(0.22f, -0.2f, 0));
        int jaw = s.Bone("jaw", head, new(0.2f, -0.22f, 0));
        int sac = s.Bone("sac", jaw, new(0.3f, -0.3f, 0));

        // the bloated body, pitched up toward the head, warty all over
        s.Egg(body, new(-0.05f, -0.24f, 0), new(0.33f, 0.24f, 0.3f), olive, Mat.Skin, 0.05f, new Vector3(0, 0, 14f), bump: 0.012f);
        s.Egg(body, new(-0.02f, -0.33f, 0), new(0.3f, 0.17f, 0.27f), belly, Mat.Skin, 0.06f, new Vector3(0, 0, 10f));
        s.Egg(body, new(-0.12f, -0.13f, 0), new(0.2f, 0.1f, 0.2f), bruise, Mat.Skin, 0.07f, bump: 0.01f);
        var rng = s.Rng;
        for (int k = 0; k < 22; k++)
        {
            // warts: clustered on the back and flanks
            float a = (float)rng.NextDouble() * MathF.PI * 2f, u = 0.25f + 0.75f * (float)rng.NextDouble();
            var at = new Vector3(-0.05f + MathF.Cos(a) * 0.28f * u, -0.18f + 0.1f * (1 - u), MathF.Sin(a) * 0.26f * u);
            s.Ball(body, at, 0.018f + 0.02f * (float)rng.NextDouble(), k % 3 == 0 ? bruise : olive.Lightened(0.08f), Mat.Skin, 0.02f);
        }

        // head: broad and flat, a mouth that splits it, the jaw beneath
        s.Egg(head, new(0.26f, -0.16f, 0), new(0.2f, 0.12f, 0.23f), olive, Mat.Skin, 0.06f, bump: 0.008f);
        s.Egg(jaw, new(0.27f, -0.27f, 0), new(0.2f, 0.06f, 0.22f), belly.Darkened(0.1f), Mat.Skin, 0.03f);
        // the mouth: a deep crease that turns down at the corners
        foreach (int sd in new[] { 1, -1 })
            s.CarveLimb(head, new(0.47f, -0.215f, 0), new(0.3f, -0.245f, 0.2f * sd), 0.016f, 0.014f, 0.008f);
        s.Egg(head, new(0.38f, -0.23f, 0), new(0.09f, 0.01f, 0.17f), mouth.Darkened(0.6f), Mat.Flesh, 0.008f);
        foreach (int sd in new[] { 1, -1 })
        {
            // bulging eyes on turrets, a slit pupil, the glands behind them
            s.Egg(head, new(0.28f, -0.05f, 0.13f * sd), new(0.07f, 0.06f, 0.06f), olive, Mat.Skin, 0.03f);
            var eye = new Vector3(0.3f, -0.02f, 0.14f * sd);
            s.Eye(head, eye, 0.046f, new Color(0.5f, 0.2f, 0.02f), 1f);
            // a black slit pupil on the eye's outward face
            var outward = new Vector3(0.75f, 0.1f, 0.65f * sd).Normalized();
            var pupil = new MeshBuilder();
            DecorMeshes.AddSphere(pupil, Vector3.Zero, 1f, new Color(0.01f, 0.008f, 0.005f), 5);
            var slit = new MeshBuilder();
            slit.Append(pupil, new Transform3D(new Basis(Vector3.Up, MathF.Atan2(outward.X, outward.Z)) * Basis.FromScale(new Vector3(0.011f, 0.036f, 0.008f)), eye + outward * 0.043f));
            s.Rigid(head, slit, Mat.Claw);
            // a bony ridge and horns over each eye
            s.Limb(head, eye + new Vector3(-0.07f, 0.04f, 0.0f), eye + new Vector3(0.03f, 0.045f, 0.01f * sd), 0.02f, 0.014f, bruise, Mat.Bone, 0.02f);
            s.Horn(head, eye + new Vector3(-0.04f, 0.05f, 0.01f * sd), eye + new Vector3(-0.1f, 0.1f, 0.04f * sd), 0.014f, bruise.Lightened(0.15f), Mat.Bone, Vector3.Up, 0.01f);
            s.Egg(head, new(0.14f, -0.07f, 0.17f * sd), new(0.1f, 0.05f, 0.05f), gland, Mat.Skin, 0.04f, new Vector3(0, -25f * sd, 0), bump: 0.006f).Emit = 0.12f;
            // nostrils
            s.CarveBall(head, new(0.44f, -0.13f, 0.045f * sd), 0.012f, 0.004f);
        }
        // the throat sac: loose, pale, veined; it balloons on the croak
        s.Egg(sac, new(0.3f, -0.31f, 0), new(0.12f, 0.06f, 0.14f), belly.Lightened(0.1f), Mat.Membrane, 0.04f);

        // forelegs: short, splayed, with fat clawed fingers
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var sh = new Vector3(0.18f, -0.33f, 0.18f * z);
            var el = new Vector3(0.24f, -0.44f, 0.24f * z);
            var wr = new Vector3(0.3f, -0.55f, 0.24f * z);
            int arm = s.Bone("arm" + sfx, body, sh);
            int fore = s.Bone("fore" + sfx, arm, el);
            s.Limb(arm, sh, el, 0.05f, 0.04f, olive, Mat.Skin, 0.03f);
            s.Limb(fore, el, wr, 0.04f, 0.03f, olive, Mat.Skin, 0.02f);
            for (int f = 0; f < 4; f++)
            {
                var tip = wr + new Vector3(0.07f, -0.005f, (f - 1.5f) * 0.035f * z);
                s.Limb(fore, wr, tip, 0.017f, 0.012f, olive, Mat.Skin, 0.01f);
                s.Ball(fore, tip, 0.016f, belly, Mat.Skin, 0.006f);
            }

            // hind legs: folded tight against the body, huge thighs, long webbed feet
            var hip = new Vector3(-0.25f, -0.3f, 0.2f * z);
            var knee = new Vector3(0.0f, -0.36f, 0.33f * z);
            var ankle = new Vector3(-0.3f, -0.5f, 0.3f * z);
            var toe = new Vector3(0.05f, -0.55f, 0.36f * z);
            int th = s.Bone("thigh" + sfx, body, hip);
            int shb = s.Bone("shin" + sfx, th, knee);
            int ft = s.Bone("foot" + sfx, shb, ankle);
            s.Limb(th, hip, knee, 0.1f, 0.07f, olive, Mat.Skin, 0.04f, bump: 0.006f);
            s.Limb(shb, knee, ankle, 0.065f, 0.045f, olive.Darkened(0.2f), Mat.Skin, 0.03f);
            s.Limb(ft, ankle, ankle.Lerp(toe, 0.5f), 0.04f, 0.03f, olive, Mat.Skin, 0.02f);
            var web = new Vector3[4];
            for (int f = 0; f < 4; f++)
            {
                var tip = toe + new Vector3(0.05f * f - 0.05f, -0.01f, (f - 1.5f) * 0.05f * z);
                web[f] = tip;
                s.Limb(ft, ankle.Lerp(toe, 0.5f), tip, 0.02f, 0.012f, olive, Mat.Skin, 0.012f);
                s.Ball(ft, tip, 0.018f, belly, Mat.Skin, 0.006f);
            }
            var mid = ankle.Lerp(toe, 0.5f);
            s.Sheet(new[] { mid, web[0], web[1], web[2], web[3] }, new[] { ft, ft, ft, ft, ft },
                new[] { (0, 1, 2), (0, 2, 3), (0, 3, 4) }, 3, bruise.Darkened(0.2f), 0.003f, Mat.Membrane, new[] { (1, 2), (2, 3), (3, 4) }, 0.02f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float breathe = MathF.Sin(time * 3.1f);
        // (it pants faster once it has noticed someone)
        if (a.Owner is Enemy { Alerted: true } && c is "idle") breathe = MathF.Sin(time * 6.2f);
        float sac = 1f + (a.Owner is Enemy { Alerted: true } && c is "idle" ? 0.16f : 0.08f) * Math.Max(0, breathe), jaw = 0f, pitch = 0f, crouch = 0f, roll = 0f;
        float legExt = 0f, armFwd = 0f, kick = 0f;
        switch (c)
        {
            case "croak":
                {
                    // three pulses of a balloon throat
                    float env = Key(t, (0, 0), (0.15f, 1), (0.85f, 1), (1, 0));
                    sac = 1f + env * (1.1f + 0.4f * MathF.Sin(t * MathF.PI * 6f));
                    pitch = 6f * env; jaw = 3f * env;
                    break;
                }
            case "tongue_windup":
                {
                    // the throat balloons and shudders, the mouth cracks, the body sits back on its haunches
                    float k = W3.Smooth01(t);
                    sac = 1f + k * (1.6f + 0.2f * MathF.Sin(time * 45f));
                    jaw = 6f * k; pitch = -9f * k; crouch = 0.55f * k;
                    p.Glow = 1f + 0.8f * k;
                    break;
                }
            case "crouch": crouch = W3.Smooth01(t); pitch = -8f * crouch; break;
            case "hop": legExt = 1f; armFwd = 1f; pitch = 22f; break;
            case "fall": legExt = 0.4f; armFwd = 0.8f; pitch = -10f; break;
            case "land": { float k = 1f - t; crouch = 0.9f * k; pitch = -12f * k; break; }
            case "tongue": jaw = Key(t, (0, 0), (0.2f, 38f), (0.75f, 38f), (1, 0)); pitch = 6f; crouch = 0.2f; break;
            case "swim":
                kick = MathF.Sin(t * Mathf.Tau);
                legExt = 0.5f + 0.5f * kick; armFwd = -0.3f; pitch = 8f;
                break;
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); pitch = -15f * k; jaw = 25f * k; sac = 1.2f; break; }
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.4f));
                    roll = 175f * k; jaw = 30f * k; legExt = 0.7f * k;
                    p.Root = new Vector3(0, 0.3f * MathF.Sin(MathF.PI * k), 0);
                    break;
                }
        }
        p.Set(_body, roll, 0, pitch + breathe * 1.5f);
        p.Move(_body, new Vector3(0, -0.08f * crouch, 0));
        p.Set(_head, 0, 4f * MathF.Sin(time * 0.7f), -pitch * 0.4f);
        p.Set(_jaw, 0, 0, -jaw);
        p.Grow(_sac, new Vector3(sac, sac, sac * 1.05f));
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            float kickK = c == "swim" ? MathF.Sin(t * Mathf.Tau + k * 0.3f) : 0f;
            // hind leg: folded (0) to stretched straight back (1); crouch folds it tighter
            float ext = Math.Clamp(legExt + 0.3f * kickK, 0f, 1f);
            p.Set(_thigh[k], 0, 25f * ext * z, 30f * ext - 10f * crouch);
            p.Set(_shin[k], 0, -20f * ext * z, -110f * ext + 12f * crouch);
            p.Set(_foot[k], 0, 0, 80f * ext - 10f * crouch);
            p.Set(_arm[k], 0, -10f * armFwd * z, 45f * armFwd + 10f * crouch);
            p.Set(_fore[k], 0, 0, -25f * armFwd);
        }
    }

    // ---- the tongue: a thick, wet rope from the mouth to where the gameplay says its tip is
    public override void Frame(CreatureModel m, in AnimInput a)
    {
        var tongue = m.GetNodeOrNull<Node3D>("Tongue");
        var frog = a.Owner as Frog;
        if (frog == null || !frog.TongueOut || a.Clip == "death")
        {
            if (tongue != null) tongue.Visible = false;
            return;
        }
        if (tongue == null)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.78f, 0.32f, 0.38f), Roughness = 0.22f,
                EmissionEnabled = true, Emission = new Color(0.5f, 0.12f, 0.16f), EmissionEnergyMultiplier = 0.4f,
                RimEnabled = true, Rim = 0.4f,
            };
            // a unit rope along +Y (thick at the root) and a sticky club for the tip
            var rope = new MeshBuilder();
            var path = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            for (int i = 0; i <= 8; i++) { path.Add(new Vector3(0, i / 8f, 0)); radii.Add(Mathf.Lerp(0.06f, 0.035f, i / 8f)); }
            rope.Tube(path, radii, 8, Colors.White, capStart: true);
            var club = new MeshBuilder();
            DecorMeshes.AddSphere(club, Vector3.Zero, 0.08f, Colors.White, 7);
            tongue = new Node3D { Name = "Tongue", TopLevel = true };
            tongue.AddChild(new MeshInstance3D { Name = "Rope", Mesh = rope.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = 1 | Stage3D.ActorLayer });
            tongue.AddChild(new MeshInstance3D { Name = "Club", Mesh = club.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = 1 | Stage3D.ActorLayer });
            m.AddChild(tongue);
        }
        var o = frog.GlobalPosition;
        var from = W3.P(o + frog.TongueMouth, 0.05f);
        var to = W3.P(o + frog.TongueTip, 0.05f);
        var d = to - from;
        float len = Math.Max(0.02f, d.Length());
        var y = d / len;
        var x = Math.Abs(y.Z) < 0.9f ? y.Cross(Vector3.Back).Normalized() : Vector3.Right;
        var z = x.Cross(y);
        tongue.GlobalTransform = Transform3D.Identity;
        tongue.GetNode<MeshInstance3D>("Rope").GlobalTransform = new Transform3D(new Basis(x, y * len, z), from);
        tongue.GetNode<MeshInstance3D>("Club").GlobalTransform = new Transform3D(Basis.Identity, to);
        tongue.Visible = true;
    }
}
