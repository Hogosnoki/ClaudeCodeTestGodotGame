using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The merchant: a short, round peddler in a hooded brown robe with a belt of coins, a great pack on his back (a bedroll, pots and a kettle
/// hanging off it), and a lantern on a hooked staff held up beside him, its flame lighting the stall. He sways on his feet and now and then lifts the lantern.
/// </summary>
public sealed class MerchantDesign : BipedDesign
{
    public MerchantDesign()
    {
        P = new Spec
        {
            Floor = -0.5f, HipH = 0.4f, Spine = 0.09f, Chest = 0.12f, Neck = 0.06f, HeadUp = 0.07f, Hunch = 0.06f,
            ShoulderW = 0.12f, HipW = 0.07f, UpperArm = 0.17f, ForeArm = 0.16f, Hand = 0.07f, Thigh = 0.2f, Shin = 0.18f, Foot = 0.12f,
        };
    }

    public override string Name => "merchant";
    public override float Cell => 0.011f;
    public override float ThreeQuarter => 20f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.25f, 0.18f, 0.1f), EyeEnergy = 0.3f,
        Glow = new Color(1f, 0.75f, 0.3f), GlowEnergy = 2f,
        Rim = new Color(1f, 0.82f, 0.5f), RimEnergy = 0.25f,
        DetailScale = 26f, DetailStrength = 0.8f,
        LightColor = new Color(1f, 0.75f, 0.35f), LightEnergy = 0.9f, LightRange = 4f,
    };

    public override void Sculpt(Sculptor s)
    {
        var robe = new Color(0.46f, 0.27f, 0.15f);
        var robeDark = robe.Darkened(0.3f);
        var skin = new Color(0.78f, 0.6f, 0.46f);
        var leather = new Color(0.34f, 0.22f, 0.13f);
        var gold = new Color(0.9f, 0.7f, 0.25f);
        var iron = new Color(0.3f, 0.3f, 0.34f);
        BuildSkeleton(s);
        int hd = s["head"], ch = s["chest"], sp = s["spine"], hp = s["hips"];

        // the robe: round at the belly, falling to the boots
        s.Egg(ch, Chest + new Vector3(0.01f, 0f, 0), new(0.1f, 0.12f, 0.125f), robe, Mat.Cloth, 0.05f, bump: 0.004f);
        s.Egg(sp, Spine + new Vector3(0.015f, -0.02f, 0), new(0.115f, 0.1f, 0.13f), robe, Mat.Cloth, 0.05f);
        s.Egg(hp, Hips + new Vector3(0, -0.15f, 0), new(0.13f, 0.22f, 0.145f), robe, Mat.Cloth, 0.05f, bump: 0.004f);
        // a belt with a heavy purse and a few coins on a string
        s.Ring(hp, Hips + new Vector3(0.01f, 0.05f, 0), 0.125f, 0.14f, 0.014f, leather, Mat.Leather, 0.01f);
        s.Block(hp, Hips + new Vector3(0.12f, 0.05f, 0), new(0.012f, 0.014f, 0.016f), 0.004f, gold, Mat.Gold, 0.006f);
        s.Egg(hp, Hips + new Vector3(0.07f, -0.03f, 0.12f), new(0.036f, 0.05f, 0.032f), leather, Mat.Leather, 0.02f);
        s.Ball(hp, Hips + new Vector3(0.07f, 0.0f, 0.12f), 0.014f, gold, Mat.Gold, 0.006f);
        for (int k = 0; k < 3; k++) s.Ball(hp, Hips + new Vector3(0.1f, -0.06f - 0.03f * k, -0.1f), 0.014f, gold, Mat.Gold, 0.004f);

        // the head: a round, ruddy face in a hood, a big nose, bushy brows and a moustache
        var h = Head + new Vector3(0.03f, 0.03f, 0);
        s.Limb(s["neck"], Neck, Head, 0.04f, 0.04f, skin, Mat.Skin, 0.03f);
        s.Egg(hd, h, new(0.085f, 0.085f, 0.08f), skin, Mat.Skin, 0.03f, bump: 0.002f);
        s.Ball(hd, h + new Vector3(0.09f, -0.01f, 0), 0.027f, skin.Lerp(new Color(0.85f, 0.4f, 0.35f), 0.25f), Mat.Skin, 0.02f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(hd, h + new Vector3(0.075f, 0.02f, 0.034f * sd), 0.014f, new Color(0.15f, 0.1f, 0.06f), 0.4f);
            s.Limb(hd, h + new Vector3(0.078f, 0.045f, 0.012f * sd), h + new Vector3(0.07f, 0.05f, 0.05f * sd), 0.011f, 0.007f, new Color(0.75f, 0.72f, 0.66f), Mat.Fur, 0.008f);
            s.Limb(hd, h + new Vector3(0.095f, -0.035f, 0.005f * sd), h + new Vector3(0.082f, -0.045f, 0.06f * sd), 0.013f, 0.006f, new Color(0.75f, 0.72f, 0.66f), Mat.Fur, 0.01f);
        }
        s.Egg(hd, h + new Vector3(0.09f, -0.04f, 0), new(0.025f, 0.012f, 0.04f), new Color(0.75f, 0.72f, 0.66f), Mat.Fur, 0.012f);
        // the hood: up over the head and falling in a cowl on the shoulders
        s.Egg(hd, h + new Vector3(-0.03f, 0.02f, 0), new(0.105f, 0.1f, 0.1f), robeDark, Mat.Cloth, 0.04f, bump: 0.004f);
        s.Egg(hd, h + new Vector3(-0.07f, -0.04f, 0), new(0.07f, 0.06f, 0.11f), robeDark, Mat.Cloth, 0.04f);
        s.Egg(ch, Chest + new Vector3(-0.02f, 0.09f, 0), new(0.08f, 0.04f, 0.13f), robeDark, Mat.Cloth, 0.04f);

        Limbs(s, 0.03f, 0.03f, robe, robeDark, Mat.Cloth, Mat.Leather, 0.028f, skin, 0.034f);
        // (cuffs of fur at the wrists)
        for (int k = 0; k < 2; k++) s.Ball(s[k == 0 ? "farm_r" : "farm_l"], Wrist[k], 0.036f, new Color(0.7f, 0.65f, 0.55f), Mat.Fur, 0.012f, bump: 0.006f);

        // the pack: a great hump on his back with a bedroll over it and kettle, pots and a ladle hanging off
        var pk = Chest + new Vector3(-0.16f, 0.0f, 0);
        s.Egg(ch, pk, new(0.1f, 0.16f, 0.13f), leather, Mat.Leather, 0.04f, bump: 0.006f);
        s.Egg(ch, pk + new Vector3(-0.03f, 0.17f, 0), new(0.08f, 0.07f, 0.11f), leather.Lightened(0.1f), Mat.Leather, 0.03f);
        s.Limb(ch, pk + new Vector3(0.01f, 0.26f, -0.13f), pk + new Vector3(0.01f, 0.26f, 0.13f), 0.06f, 0.06f, new Color(0.62f, 0.55f, 0.4f), Mat.Cloth, 0.02f, bump: 0.005f);
        s.Ring(ch, pk + new Vector3(0.01f, 0.26f, -0.07f), 0.02f, 0.02f, 0.06f, leather.Darkened(0.2f), Mat.Leather, 0.01f, 90f);
        s.Ball(ch, pk + new Vector3(-0.09f, -0.13f, 0.1f), 0.05f, iron, Mat.Metal, 0.012f);
        s.Limb(ch, pk + new Vector3(-0.09f, -0.08f, 0.1f), pk + new Vector3(-0.09f, -0.02f, 0.06f), 0.006f, 0.006f, iron, Mat.Metal, 0.004f);
        s.Ball(ch, pk + new Vector3(-0.1f, -0.2f, -0.09f), 0.04f, gold.Darkened(0.2f), Mat.Metal, 0.01f);
        s.Ball(ch, pk + new Vector3(-0.1f, -0.13f, -0.12f), 0.032f, iron, Mat.Metal, 0.01f);
        s.Limb(ch, pk + new Vector3(-0.1f, 0.0f, 0.12f), pk + new Vector3(-0.1f, -0.2f, 0.14f), 0.008f, 0.008f, leather.Lightened(0.15f), Mat.Wood, 0.004f);
        // the straps over his shoulders
        s.Limb(ch, Chest + new Vector3(-0.06f, 0.1f, 0.08f), Chest + new Vector3(0.07f, -0.04f, 0.1f), 0.012f, 0.012f, leather.Darkened(0.2f), Mat.Leather, 0.006f);
        s.Limb(ch, Chest + new Vector3(-0.06f, 0.1f, -0.08f), Chest + new Vector3(0.07f, -0.04f, -0.1f), 0.012f, 0.012f, leather.Darkened(0.2f), Mat.Leather, 0.006f);

        // the staff in his left hand, with a lantern hung from its hook above his head
        int hl = s["hand_l"];
        var grip = Wrist[1] + new Vector3(0.015f, -0.04f, 0);
        var top = grip + new Vector3(0.02f, 0.88f, 0);
        s.Limb(hl, grip + new Vector3(0, -0.45f, 0), top, 0.013f, 0.011f, leather.Lightened(0.15f), Mat.Wood, 0.006f, bump: 0.002f);
        s.Limb(hl, top, top + new Vector3(0.1f, 0.03f, 0), 0.01f, 0.01f, leather.Lightened(0.15f), Mat.Wood, 0.006f);
        var lamp = top + new Vector3(0.1f, -0.08f, 0);
        s.Egg(hl, lamp + new Vector3(0, 0.075f, 0), new(0.032f, 0.012f, 0.032f), iron, Mat.Metal, 0.006f);
        s.Egg(hl, lamp + new Vector3(0, -0.075f, 0), new(0.034f, 0.01f, 0.034f), iron, Mat.Metal, 0.006f);
        foreach (var (dx, dz) in new[] { (0.03f, 0f), (-0.03f, 0f), (0f, 0.03f), (0f, -0.03f) })
            s.Limb(hl, lamp + new Vector3(dx, 0.07f, dz), lamp + new Vector3(dx, -0.07f, dz), 0.004f, 0.004f, iron, Mat.Metal, 0.003f);
        s.Eye(hl, lamp, 0.038f, new Color(1f, 0.72f, 0.3f), 3f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, 1.6f);
        Body o = Walk(ph, W3.SmoothStep(0.3f, 2.5f, speed), speed > 4f, time);
        // he sways, nods as if counting, and now and then lifts the lantern to look at you
        float lift = Math.Max(0f, MathF.Sin(time * 0.45f)) * Math.Max(0f, MathF.Sin(time * 0.45f + 1.1f));
        o.Lean += 3f * MathF.Sin(time * 0.8f);
        o.HeadPitch += 4f * MathF.Sin(time * 1.7f);
        o.HeadYaw += 8f * MathF.Sin(time * 0.5f);
        o.SL = 14f + 80f * lift; o.EL = 25f + 25f * lift; o.AL = 5f;
        o.SR = 20f; o.ER = 55f; o.WR = 10f;
        if (c == "hurt") o = Hurt(o, a.T);
        Apply(p, o);
    }
}
