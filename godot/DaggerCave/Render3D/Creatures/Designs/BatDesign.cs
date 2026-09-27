using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A cave bat the size of a hound: hairless, wrinkled skin, veined membranes that glow red when
/// light is behind them, a leaf-nosed face with needle fangs and towering ears. Roosts upside
/// down, unfolds when you come near, swoops with its mouth open.
/// </summary>
public sealed class BatDesign : CreatureDesign
{
    public override string Name => "bat";
    public override float Cell => 0.01f;
    public override float ThreeQuarter => 34f;
    public override float FloorY => -0.44f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.1f, 0.05f), EyeEnergy = 4.5f,
        Rim = new Color(0.6f, 0.4f, 0.4f), RimEnergy = 0.3f,
        DetailScale = 38f, DetailStrength = 1f, Veins = 0.8f, VeinColor = new Color(0.3f, 0.03f, 0.04f), Wet = 0.3f,
    };

    private int _body, _head, _jaw;
    private readonly int[] _arm = new int[2], _fore = new int[2], _hand = new int[2], _f2 = new int[2], _f3 = new int[2], _f4 = new int[2], _leg = new int[2], _ear = new int[2];

    protected override void OnBonesBound()
    {
        _body = B("body"); _head = B("head"); _jaw = B("jaw");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _arm[k] = B("arm" + s); _fore[k] = B("fore" + s); _hand[k] = B("hand" + s);
            _f2[k] = B("f2" + s); _f3[k] = B("f3" + s); _f4[k] = B("f4" + s); _leg[k] = B("leg" + s); _ear[k] = B("ear" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var skin = new Color(0.27f, 0.21f, 0.26f);
        var dark = new Color(0.14f, 0.11f, 0.14f);
        var membrane = new Color(0.2f, 0.12f, 0.16f);
        var tooth = new Color(0.9f, 0.85f, 0.75f);
        int body = s.Bone("body", -1, new(0, 0, 0));
        int head = s.Bone("head", body, new(0.12f, 0.03f, 0));
        int jaw = s.Bone("jaw", head, new(0.17f, 0.0f, 0));

        s.Egg(body, new(-0.02f, 0, 0), new(0.14f, 0.085f, 0.08f), skin, Mat.Skin, 0.03f, bump: 0.004f);
        s.Egg(body, new(0.05f, 0.02f, 0), new(0.08f, 0.08f, 0.075f), skin, Mat.Skin, 0.03f);
        // the face: skull, snout, the leaf of the nose, fangs
        s.Egg(head, new(0.15f, 0.04f, 0), new(0.07f, 0.064f, 0.06f), skin, Mat.Skin, 0.025f, bump: 0.003f);
        s.Limb(head, new(0.18f, 0.03f, 0), new(0.245f, 0.005f, 0), 0.036f, 0.024f, skin, Mat.Skin, 0.015f);
        s.Egg(head, new(0.245f, 0.045f, 0), new(0.012f, 0.036f, 0.024f), dark, Mat.Skin, 0.01f, new Vector3(0, 0, -15f));
        s.Egg(jaw, new(0.21f, -0.025f, 0), new(0.045f, 0.016f, 0.03f), skin, Mat.Skin, 0.012f);
        s.Horn(head, new(0.235f, -0.005f, 0.015f), new(0.24f, -0.075f, 0.012f), 0.0075f, tooth, Mat.Bone, Vector3.Back, 0.004f);
        s.Horn(head, new(0.235f, -0.005f, -0.015f), new(0.24f, -0.075f, -0.012f), 0.0075f, tooth, Mat.Bone, Vector3.Back, 0.004f);
        DesignKit.Teeth(s, jaw, new(0.21f, -0.02f, -0.02f), new(0.235f, -0.02f, 0.02f), Vector3.Up, 5, 0.012f, 0.003f, tooth, 0.2f);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            // towering ears
            int ear = s.Bone("ear" + sfx, head, new(0.12f, 0.08f, 0.035f * z));
            s.Limb(ear, new(0.12f, 0.08f, 0.035f * z), new(0.07f, 0.24f, 0.08f * z), 0.04f, 0.004f, membrane.Lightened(0.15f), Mat.Membrane, 0.015f);
            s.Eye(head, new(0.205f, 0.058f, 0.04f * z), 0.016f, new Color(0.35f, 0.03f, 0.02f), 1f);

            // wing: arm, forearm, a clawed thumb, three long fingers, the membrane between
            var S = new Vector3(0.05f, 0.05f, 0.06f * z);
            var E = new Vector3(0.0f, 0.1f, 0.3f * z);
            var W = new Vector3(0.05f, 0.12f, 0.58f * z);
            var T2 = new Vector3(0.22f, 0.04f, 0.94f * z);
            var T3 = new Vector3(-0.12f, -0.04f, 0.92f * z);
            var T4 = new Vector3(-0.3f, -0.1f, 0.64f * z);
            var A = new Vector3(-0.2f, -0.16f, 0.07f * z);
            var H = new Vector3(-0.08f, -0.04f, 0.06f * z);
            int arm = s.Bone("arm" + sfx, body, S);
            int fore = s.Bone("fore" + sfx, arm, E);
            int hand = s.Bone("hand" + sfx, fore, W);
            int f2 = s.Bone("f2" + sfx, hand, W);
            int f3 = s.Bone("f3" + sfx, hand, W);
            int f4 = s.Bone("f4" + sfx, hand, W);
            int leg = s.Bone("leg" + sfx, body, H);
            s.Limb(arm, S, E, 0.024f, 0.017f, dark, Mat.Skin, 0.012f);
            s.Limb(fore, E, W, 0.017f, 0.011f, dark, Mat.Skin, 0.008f);
            s.Horn(hand, W, W + new Vector3(0.06f, 0.03f, -0.01f * z), 0.008f, tooth.Darkened(0.4f), Mat.Claw, Vector3.Down, 0.01f);
            s.Limb(f2, W, T2, 0.009f, 0.003f, dark, Mat.Skin, 0.004f);
            s.Limb(f3, W, T3, 0.009f, 0.003f, dark, Mat.Skin, 0.004f);
            s.Limb(f4, W, T4, 0.009f, 0.003f, dark, Mat.Skin, 0.004f);
            s.Limb(leg, H, A, 0.016f, 0.01f, dark, Mat.Skin, 0.008f);
            for (int c = 0; c < 3; c++)
                s.Horn(leg, A, A + new Vector3(-0.02f, -0.035f, (c - 1) * 0.012f), 0.004f, tooth.Darkened(0.5f), Mat.Claw, Vector3.Back, 0.01f);
            s.Sheet(new[] { W, T2, T3, T4, E, A, H, S }, new[] { hand, f2, f3, f4, fore, leg, body, body },
                new[] { (0, 1, 2), (0, 2, 3), (0, 3, 4), (4, 3, 5), (4, 5, 6), (4, 6, 7) }, 5, membrane, 0.003f, Mat.Membrane,
                new[] { (1, 2), (2, 3), (3, 5) }, 0.07f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "fly";
        float t = a.T, time = a.Time;
        float flap = 0f, fold = 0f, pitch = 0f, jaw = 8f, roll = 0f;
        float bob = 0f;
        switch (c)
        {
            case "fly":
                // (positive flap = wings down) an upward-biased beat so the wings read from the side
                flap = -14f + 66f * MathF.Sin(t * Mathf.Tau);
                fold = 30f * Math.Max(0, -MathF.Cos(t * Mathf.Tau)); // folds a little on the upstroke
                bob = 0.03f * MathF.Sin(t * Mathf.Tau - 0.6f);        // the downstroke lifts the body
                pitch = 5f;
                break;
            case "dive":
                flap = 12f * MathF.Sin(t * Mathf.Tau * 2f) - 25f;
                fold = 45f; pitch = -25f; jaw = 40f;
                break;
            case "roost":
                {
                    // hanging by the feet, wings wrapped round like a shroud, breathing
                    roll = 180f;
                    flap = 80f; fold = 70f;
                    jaw = 4f + 3f * MathF.Sin(time * 2f);
                    bob = 0.12f;
                    break;
                }
            case "wake":
                roll = 180f * (1f - W3.Smooth01(t));
                flap = Mathf.Lerp(80f, -40f, W3.Smooth01(t));
                fold = Mathf.Lerp(70f, 0f, W3.Smooth01(t));
                jaw = 45f * MathF.Sin(t * MathF.PI);
                bob = 0.12f * (1f - t);
                break;
            case "hurt":
                flap = -60f * Key(t, (0, 0), (0.3f, 1), (1, 0)); jaw = 40f; fold = 20f;
                break;
            case "death":
                {
                    float k = W3.Smooth01(t);
                    flap = Mathf.Lerp(20f, 70f, k); fold = 60f * k; jaw = 40f;
                    roll = 200f * k; pitch = -40f * k;
                    bob = -0.2f * k;
                    break;
                }
        }
        p.Set(_body, roll, 0, pitch);
        p.Root = new Vector3(0, bob, 0);
        p.Set(_head, 0, 8f * MathF.Sin(time * 1.9f), -pitch * 0.5f + 5f * MathF.Sin(time * 3.3f));
        p.Set(_jaw, 0, 0, -jaw);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1f : -1f;
            // flap about the body's long axis; fold sweeps the hand back and the fingers together
            p.Set(_arm[k], flap * z, -fold * 0.4f * z, 0);
            p.Set(_fore[k], 0, fold * 0.9f * z, 0);
            p.Set(_hand[k], flap * 0.25f * z, 0, 0);
            p.Set(_f2[k], 0, -fold * 0.6f * z, 0);
            p.Set(_f3[k], 0, fold * 0.2f * z, 0);
            p.Set(_f4[k], 0, fold * 0.5f * z, 0);
            p.Set(_leg[k], 0, 0, c == "roost" ? 60f : -20f);
            p.Set(_ear[k], 0, 0, 6f * MathF.Sin(time * 4f + k));
        }
    }
}
