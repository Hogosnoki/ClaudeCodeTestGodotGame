using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A frost wraith: a tall hooded shape in a rotted, rime-stiff robe that frays into nothing
/// below the knees, a cowl with only darkness and two cold eyes inside, a glimpse of a jawbone,
/// long skeletal hands with ice forming on the knuckles. It gathers a knot of frost in one hand
/// and flings it.
/// </summary>
public sealed class WraithDesign : CreatureDesign
{
    public override string Name => "wraith";
    public override float Cell => 0.013f;
    public override float ThreeQuarter => 24f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.55f, 0.9f, 1f), EyeEnergy = 3.5f,
        Glow = new Color(0.5f, 0.85f, 1f), GlowEnergy = 2.5f,
        Rim = new Color(0.6f, 0.85f, 1f), RimEnergy = 0.5f,
        DetailScale = 22f, DetailStrength = 0.9f,
        LightColor = new Color(0.45f, 0.8f, 1f), LightEnergy = 0.7f, LightRange = 3f, LightOffset = new Vector3(0.1f, 0.3f, 0),
        GhostBelow = -0.62f, GhostFade = 0.4f,
    };

    private int _body, _chest, _head, _jaw, _robe0, _robe1, _orb;
    private readonly int[] _uarm = new int[2], _farm = new int[2], _hand = new int[2];

    protected override void OnBonesBound()
    {
        _body = B("body"); _chest = B("chest"); _head = B("head"); _jaw = B("jaw"); _robe0 = B("robe0"); _robe1 = B("robe1"); _orb = B("orb");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _uarm[k] = B("uarm" + s); _farm[k] = B("farm" + s); _hand[k] = B("hand" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var robe = new Color(0.13f, 0.17f, 0.25f);
        var hem = new Color(0.55f, 0.65f, 0.75f);
        var bone = new Color(0.6f, 0.66f, 0.7f);
        var ice = new Color(0.7f, 0.9f, 1f);
        int body = s.Bone("body", -1, new(0, 0, 0));
        int chest = s.Bone("chest", body, new(0.02f, 0.3f, 0));
        int head = s.Bone("head", chest, new(0.06f, 0.52f, 0));
        int jaw = s.Bone("jaw", head, new(0.1f, 0.54f, 0));
        int robe0 = s.Bone("robe0", body, new(0, -0.1f, 0));
        int robe1 = s.Bone("robe1", robe0, new(-0.03f, -0.45f, 0));

        // the robe: a tall hollow shape from the shoulders, fraying below
        s.Egg(chest, new(0, 0.32f, 0), new(0.17f, 0.2f, 0.2f), robe, Mat.Cloth, 0.05f, bump: 0.008f);
        s.Limb(body, new(0, 0.2f, 0), new(-0.02f, -0.1f, 0), 0.19f, 0.22f, robe, Mat.Cloth, 0.06f, bump: 0.01f);
        s.Limb(robe0, new(-0.02f, -0.1f, 0), new(-0.04f, -0.45f, 0), 0.22f, 0.24f, robe, Mat.Cloth, 0.05f, bump: 0.012f);
        s.Limb(robe1, new(-0.04f, -0.45f, 0), new(-0.1f, -0.85f, 0), 0.24f, 0.16f, robe.Lerp(hem, 0.2f), Mat.Cloth, 0.05f, bump: 0.015f);
        // torn strips trailing behind
        var rng = s.Rng;
        for (int k = 0; k < 6; k++)
        {
            float z = ((k % 3) - 1) * 0.12f;
            var top = new Vector3(-0.15f, -0.3f - 0.08f * (k / 3), z);
            s.Sheet(new[] { top, top + new Vector3(-0.08f, -0.05f, 0.06f), top + new Vector3(-0.2f - 0.1f * (float)rng.NextDouble(), -0.45f, 0.03f) },
                new[] { robe0, robe1, robe1 }, new[] { (0, 1, 2) }, 3, robe.Lerp(hem, 0.35f), 0.004f, Mat.Cloth, new[] { (1, 2) }, 0.02f);
        }
        // frost on the shoulders
        for (int k = 0; k < 8; k++)
        {
            float z = (k % 2 == 0 ? 1 : -1) * (0.1f + 0.02f * (k / 2));
            var at = new Vector3(-0.02f + 0.03f * (k / 2), 0.46f - 0.02f * (k / 2), z);
            s.Horn(chest, at, at + new Vector3((float)(rng.NextDouble() - 0.5) * 0.06f, 0.05f + 0.04f * (float)rng.NextDouble(), z * 0.3f), 0.012f, ice, Mat.Ice, sides: 4, rings: 2, emit: 0.6f);
        }

        // the cowl: deep, peaked, with darkness inside and two cold eyes
        s.Egg(head, new(0.05f, 0.58f, 0), new(0.15f, 0.17f, 0.15f), robe, Mat.Cloth, 0.04f, bump: 0.006f);
        s.Horn(head, new(-0.02f, 0.68f, 0), new(-0.12f, 0.8f, 0), 0.07f, robe, Mat.Cloth, Vector3.Down, 0.03f, 7, 4);
        s.CarveBall(head, new(0.17f, 0.56f, 0), 0.11f, 0.03f);
        s.Egg(head, new(0.08f, 0.56f, 0), new(0.06f, 0.1f, 0.1f), new Color(0.01f, 0.012f, 0.02f), Mat.Cloth, 0.02f);
        foreach (int sd in new[] { 1, -1 })
            s.Eye(head, new(0.15f, 0.575f, 0.042f * sd), 0.018f, new Color(0.6f, 0.9f, 1f), 1f);
        s.Egg(jaw, new(0.14f, 0.49f, 0), new(0.035f, 0.02f, 0.045f), bone, Mat.Bone, 0.01f);
        DesignKit.Teeth(s, jaw, new(0.155f, 0.505f, -0.03f), new(0.165f, 0.505f, 0.03f), Vector3.Up, 6, 0.012f, 0.003f, bone.Lightened(0.2f), 0f, 0.1f);

        // arms: sleeves to the elbow, then bone; long fingers, ice on the knuckles
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var sh = new Vector3(0.02f, 0.42f, 0.17f * z);
            var el = new Vector3(0.08f, 0.2f, 0.22f * z);
            var wr = new Vector3(0.2f, 0.08f, 0.2f * z);
            int ua = s.Bone("uarm" + sfx, chest, sh);
            int fa = s.Bone("farm" + sfx, ua, el);
            int ha = s.Bone("hand" + sfx, fa, wr);
            s.Limb(ua, sh, el, 0.07f, 0.08f, robe, Mat.Cloth, 0.03f, bump: 0.006f);
            s.Limb(fa, el, el.Lerp(wr, 0.45f), 0.09f, 0.1f, robe.Lerp(hem, 0.2f), Mat.Cloth, 0.02f, bump: 0.006f);
            s.Limb(fa, el, wr, 0.018f, 0.014f, bone, Mat.Bone, 0.01f);
            for (int f = 0; f < 4; f++)
            {
                var root = wr + new Vector3(0.02f, -0.01f, (f - 1.5f) * 0.018f * z);
                var mid = root + new Vector3(0.07f, -0.04f, (f - 1.5f) * 0.01f * z);
                s.Limb(ha, root, mid, 0.007f, 0.006f, bone, Mat.Bone, 0.004f);
                s.Horn(ha, mid, mid + new Vector3(0.05f, -0.05f, 0), 0.006f, bone.Darkened(0.2f), Mat.Claw, Vector3.Up, 0.01f, 4, 3);
                s.Horn(ha, root + new Vector3(0, 0.008f, 0), root + new Vector3(0.01f, 0.03f, 0), 0.006f, ice, Mat.Ice, sides: 4, rings: 2, emit: 0.7f);
            }
        }
        // the frost it gathers in its right hand
        var oc = new Vector3(0.3f, 0.06f, 0.2f);
        int orb = s.Bone("orb", s["hand_r"], oc);
        var om = new MeshBuilder();
        DesignKit.CrystalAt(om, oc + new Vector3(0, -0.05f, 0), Vector3.Up, 0.035f, 0.13f, ice);
        DesignKit.CrystalAt(om, oc + new Vector3(0, 0.03f, 0), Vector3.Down, 0.03f, 0.11f, ice, 0.5f);
        DesignKit.CrystalAt(om, oc, new Vector3(1, 0.3f, 0.5f), 0.022f, 0.09f, ice, 0.2f);
        DecorMeshes.AddSphere(om, oc, 0.045f, new Color(0.9f, 0.97f, 1f), 6);
        s.Rigid(orb, om, Mat.Ice, 2.2f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "float";
        float t = a.T, time = a.Time;
        float vx = a.Vel.X * a.Facing;
        float lean = Math.Clamp(vx * 4f, -12f, 16f);
        float bob = 0.05f * MathF.Sin(time * 1.8f);
        float armR = 0f, armL = 0f, reachR = 0f, orb = 0.01f, jaw = 5f + 5f * MathF.Sin(time * 0.7f);
        p.Glow = 1f + 0.2f * MathF.Sin(time * 2.4f);
        switch (c)
        {
            case "cast_windup":
                {
                    float k = W3.Smooth01(t);
                    armR = 120f * k; reachR = -20f * k; orb = 0.2f + 0.8f * k; lean -= 8f * k; jaw = 25f * k;
                    p.Glow = 1f + 2.5f * k;
                    break;
                }
            case "cast":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 2.2f));
                    armR = Mathf.Lerp(120f, 70f, k); reachR = Mathf.Lerp(-20f, 60f, k); orb = t < 0.2f ? 1f : 0.01f; lean += 12f * k; jaw = 35f * (1 - t);
                    p.Glow = 3f - 2f * t;
                    break;
                }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); lean -= 25f * k; armL = 40f * k; armR = 40f * k; break; }
            case "death":
                {
                    // rises, spreads its arms, and comes apart (the dissolve does the rest)
                    float k = W3.Smooth01(t);
                    bob += 0.4f * k; armR = armL = 80f * k; jaw = 45f * k; lean = -20f * k;
                    p.Glow = 1f + 2f * k;
                    break;
                }
        }
        p.Root = new Vector3(0, bob, 0);
        p.Set(_body, 0, 0, -lean * 0.4f);
        p.Set(_chest, 0, 0, -lean * 0.4f + 3f * MathF.Sin(time * 1.8f + 0.5f));
        p.Set(_head, 4f * MathF.Sin(time * 0.6f), 6f * MathF.Sin(time * 0.43f), lean * 0.3f);
        p.Set(_jaw, 0, 0, -jaw);
        // the robe trails and sways behind the motion
        float trail = p.Spring(0, -vx * 5f, a.Dt, 30f, 6f);
        p.Set(_robe0, 3f * MathF.Sin(time * 1.3f), 0, trail * 0.5f + 5f * MathF.Sin(time * 1.1f));
        p.Set(_robe1, 5f * MathF.Sin(time * 1.3f + 0.8f), 0, trail * 0.8f + 8f * MathF.Sin(time * 1.1f + 0.7f));
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            float raise = k == 0 ? armR : armL;
            float reach = k == 0 ? reachR : 0f;
            float idle = 8f * MathF.Sin(time * 1.2f + k * 1.7f);
            p.Set(_uarm[k], -10f * z, -reach * 0.3f * z, raise * 0.7f + idle);
            p.Set(_farm[k], 0, 0, raise * 0.3f + reach * 0.4f + 15f);
            p.Set(_hand[k], 0, 0, -10f + 12f * MathF.Sin(time * 2f + k));
        }
        p.Grow(_orb, orb * (1f + 0.08f * MathF.Sin(time * 14f)));
    }
}
