using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elder Dragon: black-crimson scales split by veins of molten light, a long armoured neck
/// to a horned, fanged skull with ember eyes, a throat that glows as the fire rises in it,
/// vast tattered wings, and a tail that ends in a bone blade. It stalks, rears, breathes a
/// sweeping cone of fire, flies, dives, lashes its tail and roars. Enraged, it burns hotter.
/// </summary>
public sealed class DragonDesign : CreatureDesign
{
    public override string Name => "dragon";
    public override float Cell => 0.045f;
    public override float ThreeQuarter => 18f;
    public override float FloorY => Floor;
    private const float Floor = -1.875f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.6f, 0.1f), EyeEnergy = 3f,
        Glow = new Color(1f, 0.42f, 0.08f), GlowEnergy = 2.2f,
        Rim = new Color(1f, 0.5f, 0.3f), RimEnergy = 0.25f,
        DetailScale = 9f, DetailStrength = 1.2f, Veins = 0f, Wet = 0.15f,
        LightColor = new Color(1f, 0.45f, 0.15f), LightEnergy = 1.2f, LightRange = 6f, LightOffset = new Vector3(2.6f, 0.9f, 0),
    };

    // joints (model space: +X forward, floor at -1.875)
    private static readonly Vector3 HipsP = new(-1.0f, -0.45f, 0), SpineP = new(0, -0.32f, 0), ChestP = new(1.0f, -0.28f, 0);
    private static readonly Vector3 Neck0 = new(1.2f, -0.05f, 0), Neck1 = new(1.75f, 0.55f, 0), Neck2 = new(2.3f, 1.1f, 0), HeadP = new(2.8f, 1.42f, 0);
    private static readonly Vector3[] TailP =
    {
        new(-1.3f, -0.5f, 0), new(-1.95f, -0.64f, 0), new(-2.6f, -0.78f, 0), new(-3.25f, -0.88f, 0), new(-3.9f, -0.95f, 0), new(-4.5f, -0.99f, 0), new(-5.1f, -1.02f, 0),
    };

    private int _hips, _spine, _chest, _head, _jaw, _throat;
    private readonly int[] _neck = new int[3], _tail = new int[6];
    private readonly int[] _fUp = new int[2], _fLo = new int[2], _fPaw = new int[2], _hUp = new int[2], _hLo = new int[2], _hPaw = new int[2];
    private readonly int[] _wArm = new int[2], _wFore = new int[2], _wHand = new int[2], _wF1 = new int[2], _wF2 = new int[2], _wF3 = new int[2];

    protected override void OnBonesBound()
    {
        _hips = B("hips"); _spine = B("spine"); _chest = B("chest"); _head = B("head"); _jaw = B("jaw"); _throat = B("throat");
        for (int k = 0; k < 3; k++) _neck[k] = B("neck" + k);
        for (int k = 0; k < 6; k++) _tail[k] = B("tail" + k);
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _fUp[k] = B("fup" + s); _fLo[k] = B("flo" + s); _fPaw[k] = B("fpaw" + s);
            _hUp[k] = B("hup" + s); _hLo[k] = B("hlo" + s); _hPaw[k] = B("hpaw" + s);
            _wArm[k] = B("warm" + s); _wFore[k] = B("wfore" + s); _wHand[k] = B("whand" + s);
            _wF1[k] = B("wf1" + s); _wF2[k] = B("wf2" + s); _wF3[k] = B("wf3" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var scale = new Color(0.2f, 0.05f, 0.045f);
        var scaleDk = new Color(0.1f, 0.03f, 0.03f);
        var belly = new Color(0.42f, 0.24f, 0.1f);
        var bone = new Color(0.72f, 0.64f, 0.5f);
        var horn = new Color(0.2f, 0.16f, 0.13f);
        var membrane = new Color(0.17f, 0.045f, 0.04f);
        var vein = new Color(1f, 0.5f, 0.15f);
        var rng = s.Rng;

        int hips = s.Bone("hips", -1, HipsP);
        int spine = s.Bone("spine", hips, SpineP);
        int chest = s.Bone("chest", spine, ChestP);
        int n0 = s.Bone("neck0", chest, Neck0);
        int n1 = s.Bone("neck1", n0, Neck1);
        int n2 = s.Bone("neck2", n1, Neck2);
        int head = s.Bone("head", n2, HeadP);
        int jaw = s.Bone("jaw", head, HeadP + new Vector3(0.2f, -0.12f, 0));
        int throat = s.Bone("throat", n1, Neck1 + new Vector3(0.1f, -0.2f, 0));

        // body: a deep chest, a lean waist, heavy haunches; plated belly
        s.Egg(chest, ChestP + new Vector3(-0.05f, 0.05f, 0), new(0.85f, 0.72f, 0.62f), scale, Mat.Scale, 0.15f, bump: 0.03f);
        s.Egg(spine, SpineP + new Vector3(0, 0.02f, 0), new(0.9f, 0.58f, 0.55f), scale, Mat.Scale, 0.15f, bump: 0.03f);
        s.Egg(hips, HipsP + new Vector3(0.05f, 0.02f, 0), new(0.75f, 0.62f, 0.58f), scale, Mat.Scale, 0.15f, bump: 0.03f);
        for (int k = 0; k < 7; k++)
        {
            var at = HipsP.Lerp(ChestP, k / 6f) + new Vector3(0, -0.52f + 0.05f * MathF.Sin(k / 6f * MathF.PI), 0);
            s.Egg(k < 3 ? hips : k < 5 ? spine : chest, at, new(0.2f, 0.1f, 0.42f), belly, Mat.Chitin, 0.06f);
        }
        // a crest of spines from skull to tail, and molten veins down the flanks
        for (int k = 0; k < 6; k++)
        {
            var at = HipsP.Lerp(ChestP, k / 5f) + new Vector3(0, 0.62f, 0);
            int bn = k < 2 ? hips : k < 4 ? spine : chest;
            s.Horn(bn, at - new Vector3(0, 0.12f, 0), at + new Vector3(-0.28f, 0.3f + 0.12f * (float)rng.NextDouble(), 0), 0.09f, horn, Mat.Bone, Vector3.Left, 0.06f, 6, 4);
        }
        foreach (int sd in new[] { 1, -1 })
            for (int k = 0; k < 5; k++)
            {
                var a = HipsP.Lerp(ChestP, k / 4.5f) + new Vector3(0.1f, 0.25f - 0.08f * k, 0.52f * sd);
                var b = a + new Vector3(0.35f, -0.45f, 0.06f * sd);
                s.Limb(k < 2 ? hips : k < 4 ? spine : chest, a, b, 0.035f, 0.02f, vein, Mat.Ember, 0.02f).Emit = 1.1f;
            }

        // neck: armoured, glowing seams underneath, a throat that fills with fire
        var neckPts = new[] { Neck0, Neck1, Neck2, HeadP };
        float[] nr = { 0.5f, 0.4f, 0.33f, 0.28f };
        int[] nb = { n0, n1, n2 };
        for (int k = 0; k < 3; k++)
        {
            s.Limb(nb[k], neckPts[k], neckPts[k + 1], nr[k], nr[k + 1], scale, Mat.Scale, 0.12f, bump: 0.02f);
            var mid = neckPts[k].Lerp(neckPts[k + 1], 0.5f);
            var dir = (neckPts[k + 1] - neckPts[k]).Normalized();
            var up = dir.Cross(Vector3.Back).Normalized();
            s.Horn(nb[k], mid + up * nr[k] * 0.8f, mid + up * (nr[k] + 0.3f) - dir * 0.2f, 0.07f, horn, Mat.Bone, -dir, 0.04f, 6, 3);
            s.Egg(nb[k], mid - up * nr[k] * 0.75f, new(0.28f, 0.1f, nr[k] * 0.8f), belly, Mat.Chitin, 0.05f, new Vector3(0, 0, Mathf.RadToDeg(MathF.Atan2(dir.Y, dir.X))));
        }
        s.Egg(throat, Neck1 + new Vector3(0.1f, -0.28f, 0), new(0.4f, 0.16f, 0.26f), vein.Darkened(0.4f), Mat.Ember, 0.1f).Emit = 0.5f;

        // skull: long and wedge-shaped, heavy brow, swept horns, a fringe of spikes, ember eyes
        s.Egg(head, HeadP + new Vector3(0.2f, 0.05f, 0), new(0.42f, 0.28f, 0.3f), scale, Mat.Scale, 0.08f, bump: 0.015f);
        s.Limb(head, HeadP + new Vector3(0.3f, 0.02f, 0), HeadP + new Vector3(1.0f, -0.08f, 0), 0.22f, 0.12f, scale, Mat.Scale, 0.08f, bump: 0.01f);
        s.Limb(head, HeadP + new Vector3(0.35f, 0.2f, -0.18f), HeadP + new Vector3(0.35f, 0.2f, 0.18f), 0.1f, 0.1f, scaleDk, Mat.Bone, 0.05f);
        s.Egg(jaw, HeadP + new Vector3(0.55f, -0.18f, 0), new(0.45f, 0.08f, 0.17f), scaleDk, Mat.Scale, 0.05f);
        s.Egg(jaw, HeadP + new Vector3(0.5f, -0.12f, 0), new(0.35f, 0.04f, 0.12f), new Color(0.4f, 0.1f, 0.06f), Mat.Flesh, 0.03f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(head, HeadP + new Vector3(0.45f, 0.18f, 0.2f * sd), 0.055f, new Color(0.9f, 0.5f, 0.1f), 1f);
            s.Horn(head, HeadP + new Vector3(0.15f, 0.22f, 0.17f * sd), HeadP + new Vector3(-0.75f, 0.75f, 0.4f * sd), 0.11f, horn, Mat.Bone, Vector3.Up, 0.15f, 8, 6);
            s.Horn(head, HeadP + new Vector3(0.05f, 0.05f, 0.24f * sd), HeadP + new Vector3(-0.45f, 0.2f, 0.5f * sd), 0.06f, horn, Mat.Bone, Vector3.Up, 0.06f, 6, 4);
            s.Horn(head, HeadP + new Vector3(0.95f, 0.06f, 0.07f * sd), HeadP + new Vector3(1.05f, 0.22f, 0.08f * sd), 0.035f, horn, Mat.Bone, sides: 5, rings: 3);
            for (int k = 0; k < 4; k++)
            {
                var root = HeadP + new Vector3(-0.1f, 0.1f - k * 0.1f, 0.26f * sd);
                s.Horn(head, root, root + new Vector3(-0.3f, 0.02f, 0.14f * sd), 0.04f, horn, Mat.Bone, sides: 5, rings: 3);
            }
            DesignKit.Teeth(s, head, HeadP + new Vector3(0.3f, -0.1f, 0.15f * sd), HeadP + new Vector3(0.95f, -0.16f, 0.06f * sd), Vector3.Down, 8, 0.1f, 0.022f, bone, 0.4f);
            DesignKit.Teeth(s, jaw, HeadP + new Vector3(0.3f, -0.16f, 0.13f * sd), HeadP + new Vector3(0.9f, -0.16f, 0.05f * sd), Vector3.Up, 7, 0.08f, 0.02f, bone, 0.3f);
            s.CarveBall(head, HeadP + new Vector3(1.0f, 0.0f, 0.06f * sd), 0.035f, 0.01f);
        }

        // tail: a long tapering chain, spines along the top, a bone blade at the end
        var tailBones = new int[6];
        int parent = hips;
        for (int k = 0; k < 6; k++) { tailBones[k] = s.Bone("tail" + k, parent, TailP[k]); parent = tailBones[k]; }
        for (int k = 0; k < 6; k++)
        {
            float r0 = 0.42f * (1f - k / 6.5f), r1 = 0.42f * (1f - (k + 1) / 6.5f);
            s.Limb(tailBones[k], TailP[k], TailP[k + 1], r0, Math.Max(0.06f, r1), scale, Mat.Scale, 0.1f, bump: 0.02f);
            var mid = TailP[k].Lerp(TailP[k + 1], 0.5f);
            s.Horn(tailBones[k], mid + new Vector3(0, r0 * 0.7f, 0), mid + new Vector3(-0.2f, r0 + 0.2f, 0), 0.05f, horn, Mat.Bone, Vector3.Left, 0.03f, 5, 3);
        }
        var tip = TailP[6];
        foreach (int sd in new[] { 1, 0, -1 })
            s.Horn(tailBones[5], tip + new Vector3(0.2f, 0, 0), tip + new Vector3(-0.55f, 0.25f * (sd == 0 ? 1 : -0.3f), 0.25f * sd), 0.09f, bone, Mat.Bone, Vector3.Up, 0.05f, 6, 4);

        // legs: thick, scaled, with great hooked talons
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var fs = new Vector3(0.95f, -0.55f, 0.52f * z); var fe = new Vector3(0.75f, -1.15f, 0.6f * z); var fw = new Vector3(0.95f, -1.68f, 0.58f * z); var ft = new Vector3(1.35f, Floor + 0.06f, 0.6f * z);
            var hh = new Vector3(-1.0f, -0.5f, 0.48f * z); var hk = new Vector3(-0.55f, -1.05f, 0.58f * z); var hc = new Vector3(-1.1f, -1.55f, 0.55f * z); var ht = new Vector3(-0.7f, Floor + 0.06f, 0.55f * z);
            int fu = s.Bone("fup" + sfx, chest, fs), fl = s.Bone("flo" + sfx, fu, fe), fp = s.Bone("fpaw" + sfx, fl, fw);
            int hu = s.Bone("hup" + sfx, hips, hh), hl = s.Bone("hlo" + sfx, hu, hk), hp = s.Bone("hpaw" + sfx, hl, hc);
            s.Limb(fu, fs + new Vector3(0, 0.2f, 0), fe, 0.3f, 0.2f, scale, Mat.Scale, 0.08f, bump: 0.02f);
            s.Limb(fl, fe, fw, 0.2f, 0.15f, scale, Mat.Scale, 0.06f);
            s.Limb(fp, fw, ft, 0.15f, 0.12f, scaleDk, Mat.Scale, 0.05f);
            s.Limb(hu, hh + new Vector3(0, 0.2f, 0), hk, 0.38f, 0.24f, scale, Mat.Scale, 0.1f, bump: 0.02f);
            s.Limb(hl, hk, hc, 0.22f, 0.15f, scale, Mat.Scale, 0.06f);
            s.Limb(hp, hc, ht, 0.15f, 0.12f, scaleDk, Mat.Scale, 0.05f);
            foreach (var (bn, toe) in new[] { (fp, ft), (hp, ht) })
                for (int c = 0; c < 3; c++)
                {
                    var at = toe + new Vector3(0.05f, 0.02f, (c - 1) * 0.12f);
                    s.Horn(bn, at, at + new Vector3(0.28f, -0.1f, (c - 1) * 0.05f), 0.05f, bone.Darkened(0.35f), Mat.Claw, Vector3.Up, 0.07f, 6, 4);
                }

            // wing: arm, forearm and three long fingers carrying a tattered membrane
            var ws = new Vector3(0.55f, 0.3f, 0.45f * z);
            var we = new Vector3(0.1f, 1.25f, 1.3f * z);
            var ww = new Vector3(0.75f, 1.85f, 2.4f * z);
            var t1 = new Vector3(2.1f, 1.4f, 3.7f * z);
            var t2 = new Vector3(0.5f, 0.55f, 4.1f * z);
            var t3 = new Vector3(-0.8f, 0.2f, 3.3f * z);
            var root = new Vector3(-0.9f, 0.1f, 0.42f * z);
            int wa = s.Bone("warm" + sfx, chest, ws);
            int wf = s.Bone("wfore" + sfx, wa, we);
            int wh = s.Bone("whand" + sfx, wf, ww);
            int f1 = s.Bone("wf1" + sfx, wh, ww), f2 = s.Bone("wf2" + sfx, wh, ww), f3 = s.Bone("wf3" + sfx, wh, ww);
            var arm = new MeshBuilder();
            arm.Tube(new[] { ws, ws.Lerp(we, 0.5f) + new Vector3(0, 0.05f, 0), we }, new[] { 0.14f, 0.1f, 0.08f }, 8, scale, capStart: true);
            s.Rigid(wa, arm, Mat.Scale);
            var fore = new MeshBuilder();
            fore.Tube(new[] { we, we.Lerp(ww, 0.5f), ww }, new[] { 0.08f, 0.065f, 0.06f }, 7, scale, capStart: true);
            s.Rigid(wf, fore, Mat.Scale);
            s.Horn(wh, ww, ww + new Vector3(0.28f, 0.2f, 0.02f * z), 0.07f, bone.Darkened(0.3f), Mat.Claw, Vector3.Down, 0.08f, 6, 4);
            s.Horn(f1, ww, t1, 0.055f, scaleDk, Mat.Scale, Vector3.Up, 0.1f, 6, 6);
            s.Horn(f2, ww, t2, 0.05f, scaleDk, Mat.Scale, Vector3.Down, 0.1f, 6, 6);
            s.Horn(f3, ww, t3, 0.045f, scaleDk, Mat.Scale, Vector3.Down, 0.08f, 6, 6);
            s.Sheet(new[] { ws, we, ww, t1, t2, t3, root }, new[] { chest, wf, wh, f1, f2, f3, hips },
                new[] { (0, 1, 6), (1, 2, 6), (2, 5, 6), (2, 4, 5), (2, 3, 4) }, 6, membrane, 0.012f, Mat.Membrane,
                new[] { (3, 4), (4, 5), (5, 6) }, 0.45f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        var dragon = a.Owner as Dragon;
        float speed = MathF.Abs(a.Vel.X);
        p.Vars[0] = (p.Vars[0] + speed / 4.2f * a.Dt) % 1f;
        float ph = p.Vars[0];
        float walk = c == "walk" ? 1f : W3.SmoothStep(0.3f, 1.5f, speed) * (a.OnFloor ? 1f : 0f);
        float breathe = MathF.Sin(time * 1.1f);

        // defaults: standing, wings folded along the back, neck in an S, tail swaying
        float neck = 0f, headUp = 0f, jaw = 6f + 4f * Math.Max(0, breathe), pitch = 0f, roll = 0f, drop = 0f;
        float wingRaise = -12f, wingFold = 1f, wingBeat = 0f, legTuck = 0f;
        float tailUp = 3f * breathe, tailSwing = 8f * MathF.Sin(time * 0.7f), tailYaw = 0f;
        p.Glow = 1f + 0.15f * breathe;
        switch (c)
        {
            case "fly":
                {
                    float beat = MathF.Sin(t * Mathf.Tau);
                    wingFold = 0.05f + 0.25f * Math.Max(0, -MathF.Cos(t * Mathf.Tau));
                    wingRaise = 10f; wingBeat = 55f * beat;
                    legTuck = 1f; neck = -8f; pitch = 4f;
                    p.Root = new Vector3(0, 0.12f * MathF.Sin(t * Mathf.Tau - 0.8f), 0);
                    tailUp = -4f; tailSwing = 5f * MathF.Sin(time * 2f);
                    break;
                }
            case "dive":
                {
                    wingFold = 0.55f; wingRaise = 40f; legTuck = 0.2f; pitch = -32f; neck = -10f; jaw = 40f;
                    tailUp = 12f;
                    break;
                }
            case "breath_windup":
                {
                    float k = W3.Smooth01(t);
                    neck = 26f * k; headUp = 22f * k; jaw = 10f + 25f * k; pitch = 6f * k; wingRaise = -12f + 22f * k; wingFold = 1f - 0.4f * k;
                    p.Glow = 1f + 2.5f * k + 0.4f * MathF.Sin(time * 30f) * k;
                    break;
                }
            case "breath":
                {
                    // the head follows the fire's direction
                    var d = dragon?.BreathDir ?? new Vector2(1, 0.3f);
                    float aim = Mathf.RadToDeg(MathF.Atan2(-d.Y, Math.Abs(d.X)));
                    neck = Math.Clamp(aim * 0.6f, -35f, 25f); headUp = Math.Clamp(aim * 0.4f, -25f, 20f);
                    jaw = 42f + 4f * MathF.Sin(time * 20f); pitch = -4f; wingRaise = 5f; wingFold = 0.7f;
                    p.Glow = 3.2f + 0.3f * MathF.Sin(time * 25f);
                    break;
                }
            case "tail_windup":
                {
                    float k = W3.Smooth01(t);
                    tailUp = 28f * k; tailYaw = -30f * k; pitch = -4f * k; neck = -10f * k; headUp = 8f * k;
                    break;
                }
            case "tail":
                {
                    // the lash: the tail sweeps down and around toward the front
                    float k = W3.Smooth01(Math.Min(1f, t * 1.6f));
                    tailUp = Mathf.Lerp(28f, -14f, k); tailYaw = Mathf.Lerp(-30f, 70f, k); roll = -6f * MathF.Sin(k * MathF.PI);
                    neck = -10f; jaw = 20f;
                    break;
                }
            case "roar":
                {
                    float k = Key(t, (0, 0), (0.2f, 1), (0.85f, 1), (1, 0));
                    neck = 30f * k; headUp = 30f * k; jaw = 50f * k; pitch = 10f * k;
                    wingRaise = Mathf.Lerp(-12f, 65f, k); wingFold = 1f - 0.9f * k; wingBeat = 6f * MathF.Sin(time * 3f) * k;
                    tailUp = 15f * k;
                    p.Glow = 1f + 1.8f * k;
                    break;
                }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); neck = -15f * k; headUp = 20f * k; jaw = 35f * k; pitch = -5f * k; break; }
            case "death":
                {
                    float k1 = W3.SmoothStep(0f, 0.4f, t), k2 = W3.SmoothStep(0.3f, 0.9f, t);
                    drop = 0.9f * k2; roll = 55f * k2; neck = -30f * k2 + 15f * k1 * (1 - k2); headUp = -20f * k2; jaw = 40f * k1;
                    wingRaise = Mathf.Lerp(-12f, -35f, k2); wingFold = 0.6f;
                    legTuck = 0.5f * k2; tailUp = -8f * k2;
                    p.Glow = 1f - 0.8f * k2;
                    break;
                }
        }
        float sn = MathF.Sin(ph * Mathf.Tau);
        p.Set(_hips, roll * 0.6f, 0, pitch * 0.4f + 2f * sn * walk);
        p.Move(_hips, new Vector3(0, -drop + 0.05f * MathF.Abs(sn) * walk, 0));
        p.Set(_spine, roll * 0.4f, 2.5f * sn * walk, pitch * 0.3f);
        p.Set(_chest, 0, 2.5f * sn * walk, pitch * 0.3f + breathe * 1.2f);
        // the neck: a gentle S that rises (neck > 0) or lunges (neck < 0)
        p.Set(_neck[0], 0, 3f * MathF.Sin(time * 0.6f), neck * 0.45f - 4f * sn * walk);
        p.Set(_neck[1], 0, 3f * MathF.Sin(time * 0.6f + 0.6f), neck * 0.35f);
        p.Set(_neck[2], 0, 2f * MathF.Sin(time * 0.6f + 1.2f), neck * 0.2f + 4f * sn * walk);
        p.Set(_head, 0, 0, headUp - neck * 0.5f);
        p.Set(_jaw, 0, 0, -jaw);
        p.Grow(_throat, 1f + 0.12f * Math.Max(0, p.Glow - 1f));
        for (int k = 0; k < 6; k++)
        {
            float wave = MathF.Sin(time * 1.4f - k * 0.7f);
            p.Set(_tail[k], 0, (tailSwing * wave + tailYaw) / 6f * (1f + k * 0.25f), -tailUp / 6f * (1f + k * 0.15f) + (k > 2 ? 2f : 0f));
        }
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            // legs: a walking gait (diagonal pairs), tucked in flight
            float fo = k == 0 ? 0.5f : 0f, ho = k == 0 ? 0f : 0.5f;
            float fS = 24f * MathF.Sin((ph + fo) * Mathf.Tau) * walk, fL = 35f * Math.Max(0, MathF.Cos((ph + fo) * Mathf.Tau)) * walk;
            float hS = 24f * MathF.Sin((ph + ho) * Mathf.Tau) * walk, hL = 40f * Math.Max(0, MathF.Cos((ph + ho) * Mathf.Tau)) * walk;
            p.Set(_fUp[k], 0, 0, fS + 40f * legTuck);
            p.Set(_fLo[k], 0, 0, -fL - 60f * legTuck);
            p.Set(_fPaw[k], 0, 0, -(fS - fL) * 0.6f + fL * 0.5f + 30f * legTuck);
            p.Set(_hUp[k], 0, 0, hS + hL * 0.4f - 30f * legTuck);
            p.Set(_hLo[k], 0, 0, hL * 0.8f + 50f * legTuck);
            p.Set(_hPaw[k], 0, 0, -(hS + hL * 1.2f) * 0.8f - 20f * legTuck);
            // wings: raise about the body axis, fold the forearm back and the fingers together
            float raise = wingRaise + wingBeat;
            p.Set(_wArm[k], -raise * z, 50f * wingFold * z, -20f * wingFold);
            p.Set(_wFore[k], 0, -115f * wingFold * z, 0);
            p.Set(_wHand[k], wingBeat * 0.2f * z, 75f * wingFold * z, 0);
            p.Set(_wF1[k], 0, 20f * wingFold * z, 0);
            p.Set(_wF2[k], 0, 45f * wingFold * z, 0);
            p.Set(_wF3[k], 0, 60f * wingFold * z, 0);
        }
    }

    private bool _rage;
    public override void Frame(CreatureModel m, in AnimInput a)
    {
        bool rage = (a.Owner as Dragon)?.Phase2 == true;
        if (rage == _rage) return;
        _rage = rage;
        m.Kit.Material.SetShaderParameter("glow_energy", rage ? 3.4f : 2.2f);
        m.Kit.Material.SetShaderParameter("glow_color", rage ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.42f, 0.08f));
    }
}
