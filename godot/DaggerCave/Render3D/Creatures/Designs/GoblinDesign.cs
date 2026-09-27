using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Goblins: gaunt, hunched, grey-green things with bat ears, a hooked nose, a lipless grin of
/// needle teeth and yellow eyes that shine in the dark. The club goblin drags a spiked thighbone;
/// the slinger whirls a sling of stitched hide over its head.
/// </summary>
public sealed class GoblinDesign : BipedDesign
{
    private readonly bool _slinger;
    public GoblinDesign(bool slinger)
    {
        _slinger = slinger;
        P = new Spec
        {
            Floor = -0.5625f, HipH = 0.44f, Spine = 0.1f, Chest = 0.13f, Neck = 0.08f, HeadUp = 0.07f, Hunch = 0.13f,
            ShoulderW = 0.12f, HipW = 0.07f, UpperArm = 0.2f, ForeArm = 0.2f, Hand = 0.1f, Thigh = 0.22f, Shin = 0.21f, Foot = 0.13f,
        };
    }

    public override string Name => _slinger ? "slinger" : "goblin";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 24f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.72f, 0.12f), EyeEnergy = 4.5f,
        Rim = new Color(0.55f, 0.7f, 0.5f), RimEnergy = 0.25f,
        DetailScale = 30f, DetailStrength = 0.9f, Veins = 0.5f, VeinColor = new Color(0.2f, 0.25f, 0.12f), Wet = 0.35f,
    };

    public override void Sculpt(Sculptor s)
    {
        var skin = _slinger ? new Color(0.4f, 0.4f, 0.3f) : new Color(0.36f, 0.42f, 0.3f);
        var dark = skin.Darkened(0.35f);
        var tooth = new Color(0.82f, 0.76f, 0.56f);
        var rag = new Color(0.28f, 0.2f, 0.14f);
        var bone = new Color(0.78f, 0.72f, 0.58f);
        BuildSkeleton(s);
        int hd = s["head"], jw = s["jaw"], ch = s["chest"], sp = s["spine"], hp = s["hips"];

        // torso: a ribbed, pot-bellied frame with a knobbly spine
        s.Egg(ch, Chest + new Vector3(0.02f, 0.03f, 0), new(0.09f, 0.11f, 0.105f), skin, Mat.Skin, 0.06f, bump: 0.004f);
        s.Egg(sp, Spine + new Vector3(0.02f, -0.02f, 0), new(0.085f, 0.085f, 0.095f), skin, Mat.Skin, 0.07f);
        s.Egg(hp, Hips + new Vector3(0, 0.01f, 0), new(0.07f, 0.065f, 0.085f), rag, Mat.Cloth, 0.03f);
        for (int k = 0; k < 3; k++)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var a = Chest + new Vector3(-0.03f, 0.06f - k * 0.045f, 0.06f * sd);
                s.Limb(ch, a, a + new Vector3(0.1f, -0.03f, 0.04f * sd), 0.013f, 0.01f, skin, Mat.Skin, 0.01f);
            }
        for (int k = 0; k < 5; k++)
        {
            var at = Hips.Lerp(Neck, k / 4f) + new Vector3(-0.07f - 0.02f * MathF.Sin(k), 0.02f, 0);
            s.Ball(k < 2 ? sp : ch, at, 0.022f, dark, Mat.Skin, 0.015f);
        }
        // a mangy hide slung over the hunched back
        var hide = new Color(0.3f, 0.23f, 0.16f);
        s.Egg(ch, Chest + new Vector3(-0.075f, 0.02f, 0), new(0.06f, 0.13f, 0.115f), hide, Mat.Fur, 0.035f, new Vector3(0, 0, -12f), bump: 0.008f);
        s.Egg(sp, Spine + new Vector3(-0.07f, -0.03f, 0), new(0.05f, 0.1f, 0.1f), hide, Mat.Fur, 0.035f, new Vector3(0, 0, -6f), bump: 0.008f);
        // a rag hanging from the belt
        s.Block(hp, Hips + new Vector3(0.07f, -0.12f, 0), new(0.012f, 0.1f, 0.06f), 0.01f, rag, Mat.Cloth, 0.02f, new Vector3(0, 0, 8f), bump: 0.006f);

        // head: cranium, heavy brow, snout, hooked nose, bat ears, jaw full of needles
        var h = Head + new Vector3(0.04f, 0.04f, 0);
        s.Limb(s["neck"], Neck + new Vector3(-0.01f, -0.02f, 0), Head, 0.04f, 0.038f, skin, Mat.Skin, 0.03f);
        s.Egg(hd, h, new(0.11f, 0.09f, 0.095f), skin, Mat.Skin, 0.03f, bump: 0.003f);
        s.Limb(hd, h + new Vector3(0.082f, 0.05f, -0.055f), h + new Vector3(0.082f, 0.05f, 0.055f), 0.022f, 0.022f, dark, Mat.Skin, 0.02f);
        s.Egg(hd, h + new Vector3(0.08f, -0.04f, 0), new(0.07f, 0.05f, 0.07f), skin, Mat.Skin, 0.03f);
        s.Limb(hd, h + new Vector3(0.1f, 0.02f, 0), h + new Vector3(0.17f, -0.04f, 0), 0.024f, 0.012f, skin, Mat.Skin, 0.015f);
        s.Horn(hd, h + new Vector3(0.165f, -0.035f, 0), h + new Vector3(0.175f, -0.075f, 0), 0.012f, skin, Mat.Skin);
        foreach (int sd in new[] { 1, -1 })
        {
            var root = h + new Vector3(-0.01f, 0.03f, 0.075f * sd);
            s.Limb(hd, root, root + new Vector3(-0.1f, 0.07f, 0.13f * sd), 0.036f, 0.006f, skin, Mat.Membrane, 0.02f);
            s.Limb(hd, root + new Vector3(0, -0.02f, 0), root + new Vector3(-0.08f, 0.03f, 0.1f * sd), 0.028f, 0.01f, skin.Lerp(new Color(0.55f, 0.3f, 0.3f), 0.4f), Mat.Membrane, 0.015f);
            s.Eye(hd, h + new Vector3(0.095f, 0.014f, 0.046f * sd), 0.019f, new Color(0.6f, 0.45f, 0.1f), 1f);
        }
        // the mouth: upper needles in the snout, the jaw below with its own row
        s.Egg(jw, h + new Vector3(0.07f, -0.09f, 0), new(0.075f, 0.028f, 0.065f), skin, Mat.Skin, 0.02f);
        DesignKit.Teeth(s, hd, h + new Vector3(0.12f, -0.07f, -0.045f), h + new Vector3(0.14f, -0.07f, 0.045f), Vector3.Down, 7, 0.022f, 0.004f, tooth, 0.3f);
        DesignKit.Teeth(s, jw, h + new Vector3(0.115f, -0.075f, -0.04f), h + new Vector3(0.13f, -0.075f, 0.04f), Vector3.Up, 6, 0.018f, 0.004f, tooth, 0.3f);

        // long, knobbly limbs, clawed hands and feet
        Limbs(s, 0.028f, 0.034f, skin, skin, Mat.Skin, Mat.Skin, 0.03f, dark, 0.03f);
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            float z = k == 0 ? 1 : -1;
            s.Ball(s["farm" + sfx], Elbow[k], 0.032f, skin, Mat.Skin, 0.012f);
            s.Ball(s["shin" + sfx], Knee[k], 0.038f, skin, Mat.Skin, 0.012f);
            var palm = Wrist[k] + new Vector3(0.01f, -0.07f, 0);
            for (int f = 0; f < 3; f++)
            {
                var tip = palm + new Vector3(0.03f + f * 0.005f, -0.06f, (f - 1) * 0.022f);
                s.Limb(s["hand" + sfx], palm, tip, 0.011f, 0.008f, skin, Mat.Skin, 0.006f);
                s.Horn(s["hand" + sfx], tip, tip + new Vector3(0.02f, -0.02f, 0), 0.007f, new Color(0.15f, 0.12f, 0.1f), Mat.Claw);
            }
            for (int f = 0; f < 3; f++)
            {
                var tip = Toe[k] + new Vector3(0.02f, -0.005f, (f - 1) * 0.025f);
                s.Horn(s["foot" + sfx], tip, tip + new Vector3(0.035f, -0.01f, (f - 1) * 0.01f), 0.009f, new Color(0.15f, 0.12f, 0.1f), Mat.Claw);
            }
        }

        // weapon in the right hand, extending the arm (built hanging down)
        int hr = s["hand_r"];
        var grip = Wrist[0] + new Vector3(0.015f, -0.08f, 0);
        if (!_slinger)
        {
            // a thighbone club: grimy shaft, a knuckled head studded with nails
            var old = bone.Darkened(0.3f);
            s.Limb(hr, grip + new Vector3(0, 0.05f, 0), grip + new Vector3(0, -0.33f, 0), 0.02f, 0.03f, old, Mat.Bone, 0.01f, bump: 0.003f);
            s.Limb(hr, grip + new Vector3(0, 0.03f, 0), grip + new Vector3(0, -0.06f, 0), 0.026f, 0.026f, rag, Mat.Leather, 0.006f);
            s.Egg(hr, grip + new Vector3(0.012f, -0.39f, 0.018f), new(0.058f, 0.07f, 0.05f), old, Mat.Bone, 0.03f, bump: 0.006f);
            s.Egg(hr, grip + new Vector3(-0.015f, -0.4f, -0.02f), new(0.05f, 0.06f, 0.048f), old, Mat.Bone, 0.03f, bump: 0.006f);
            var rng = s.Rng;
            var iron = new Color(0.2f, 0.17f, 0.15f);
            for (int k = 0; k < 7; k++)
            {
                float a = k * 2.4f;
                var at = grip + new Vector3(MathF.Cos(a) * 0.045f, -0.36f - (k % 3) * 0.03f, MathF.Sin(a) * 0.04f);
                var dir = new Vector3(MathF.Cos(a), -0.15f + 0.2f * (k % 2), MathF.Sin(a)).Normalized();
                s.Horn(hr, at, at + dir * (0.05f + 0.02f * (float)rng.NextDouble()), 0.007f, iron, Mat.Metal, sides: 5, rings: 2);
            }
        }
        else
        {
            // the sling: two cords to a leather pouch holding a stone
            s.Limb(hr, grip, grip + new Vector3(0.02f, -0.3f, 0.015f), 0.004f, 0.004f, rag, Mat.Leather, 0.004f);
            s.Limb(hr, grip, grip + new Vector3(-0.02f, -0.3f, -0.015f), 0.004f, 0.004f, rag, Mat.Leather, 0.004f);
            s.Egg(hr, grip + new Vector3(0, -0.32f, 0), new(0.04f, 0.022f, 0.035f), rag, Mat.Leather, 0.01f);
            s.Ball(hr, grip + new Vector3(0, -0.3f, 0), 0.025f, new Color(0.45f, 0.42f, 0.38f), Mat.Rock, 0.005f);
            // a satchel of stones on the hip
            s.Egg(hp, Hips + new Vector3(-0.02f, -0.03f, -0.1f), new(0.05f, 0.06f, 0.035f), rag, Mat.Leather, 0.01f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, 2.2f);
        Body o = !a.OnFloor && !a.InWater ? Air(a.Vel.Y) : Walk(ph, W3.SmoothStep(0.3f, 2.5f, speed), speed > 4f, time);
        // they never quite stand still: a twitch of the head, a working jaw
        o.HeadYaw += 10f * MathF.Sin(time * 0.7f) * MathF.Sin(time * 2.3f);
        o.Jaw += 6f * Math.Max(0, MathF.Sin(time * 4.1f));
        if (!_slinger) { o.SR = Math.Min(o.SR, 30f); o.ER += 10f; o.WR = 15f; }

        switch (c)
        {
            case "windup": o = Chop(o, W3.Smooth01(t), 0f); o.Jaw = 30; o.HeadPitch = 10; break;
            case "strike": o = Chop(o, 1f, W3.Smooth01(Math.Min(1f, t * 1.6f))); o.Jaw = 35; break;
            case "recover": o = Mix(Chop(o, 1f, 1f), o, W3.Smooth01(t)); break;
            case "throw":
                {
                    // whirl the sling overhead twice, then let fly
                    float spin = t < 0.7f ? t / 0.7f * 2f : 2f;
                    float ang = spin * 360f;
                    o.SR = t < 0.7f ? 150f + 25f * MathF.Sin(Mathf.DegToRad(ang)) : Key(t, (0.7f, 150), (0.8f, 70), (1, 30));
                    o.AR = t < 0.7f ? 25f + 20f * MathF.Cos(Mathf.DegToRad(ang)) : 15f;
                    o.ER = 20f; o.WR = t < 0.7f ? ang * 0.5f : 0f;
                    o.Lean = t < 0.7f ? -6f : Key(t, (0.7f, -6), (0.8f, 18), (1, 8));
                    o.Twist = t < 0.7f ? -15f : 20f;
                    o.Jaw = 25;
                    break;
                }
            case "jump": o = Air(3f); o.Root.Y -= 0.04f * (1 - t); break;
            case "land": { float k = 1f - t; o.Root.Y -= 0.08f * k; o.KR += 45 * k; o.KL += 45 * k; o.HR += 30 * k; o.HL += 30 * k; o.Lean += 12 * k; break; }
            case "hurt": o = Hurt(o, t); break;
            case "death": o = Die(t, 0.1f); break;
        }
        Apply(p, o);
    }
}
