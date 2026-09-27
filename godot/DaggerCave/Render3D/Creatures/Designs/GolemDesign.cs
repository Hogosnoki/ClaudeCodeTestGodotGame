using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A stone golem: a hunched hill of fitted boulders taller than a man, a small head sunk between
/// the shoulders with one glowing slit of an eye, runes cut into its chest that glow when it
/// wakes, moss in the cracks, and fists like millstones hanging to its knees. Raises both fists
/// high, then brings them down hard enough to split the floor. (Mossback, crystal and obsidian
/// golems are the same body in another stone: the gameplay tint.)
/// </summary>
public sealed class GolemDesign : BipedDesign
{
    public GolemDesign()
    {
        P = new Spec
        {
            Floor = -0.9375f, HipH = 0.62f, Spine = 0.2f, Chest = 0.3f, Neck = 0.1f, HeadUp = 0.1f, Hunch = 0.2f,
            ShoulderW = 0.42f, HipW = 0.2f, UpperArm = 0.42f, ForeArm = 0.4f, Hand = 0.2f, Thigh = 0.32f, Shin = 0.28f, Foot = 0.22f,
        };
    }

    public override string Name => "golem";
    public override float Cell => 0.024f;
    public override float ThreeQuarter => 24f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.35f, 0.95f, 1f), EyeEnergy = 4f,
        Glow = new Color(0.3f, 0.9f, 1f), GlowEnergy = 2.2f,
        Rim = new Color(0.6f, 0.75f, 0.8f), RimEnergy = 0.2f,
        DetailScale = 16f, DetailStrength = 0.8f,
        LightColor = new Color(0.35f, 0.9f, 1f), LightEnergy = 0.5f, LightRange = 2.5f, LightOffset = new Vector3(0.45f, 0.95f, 0),
    };

    public override void Sculpt(Sculptor s)
    {
        var stone = new Color(0.3f, 0.29f, 0.27f);
        var dark = new Color(0.2f, 0.19f, 0.18f);
        var moss = new Color(0.22f, 0.32f, 0.1f);
        var rune = new Color(0.4f, 0.8f, 0.85f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"];
        var rng = s.Rng;

        // the trunk: stacked boulders, the chest a great slab, the back humped
        s.Block(hp, Hips + new Vector3(0, 0.02f, 0), new(0.2f, 0.14f, 0.24f), 0.08f, dark, Mat.Rock, 0.05f, new Vector3(0, 0, -5f), bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0.02f, 0.02f, 0), new(0.28f, 0.22f, 0.3f), stone, Mat.Rock, 0.06f, bump: 0.025f);
        s.Block(ch, Chest + new Vector3(0.02f, 0.02f, 0), new(0.26f, 0.26f, 0.36f), 0.1f, stone, Mat.Rock, 0.06f, new Vector3(0, 0, -12f), bump: 0.03f);
        s.Egg(ch, Chest + new Vector3(-0.18f, 0.18f, 0), new(0.26f, 0.2f, 0.34f), dark, Mat.Rock, 0.08f, bump: 0.025f);
        // shoulder boulders, moss in the seams
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Egg(ch, Shoulder[k] + new Vector3(-0.02f, 0.08f, 0.03f * z), new(0.2f, 0.17f, 0.18f), stone, Mat.Rock, 0.05f, bump: 0.025f);
            s.Egg(ch, Shoulder[k] + new Vector3(-0.06f, 0.2f, 0.0f), new(0.14f, 0.05f, 0.12f), moss, Mat.Fur, 0.04f, bump: 0.02f);
        }
        s.Egg(ch, Chest + new Vector3(-0.1f, 0.33f, 0.1f), new(0.16f, 0.05f, 0.14f), moss, Mat.Fur, 0.05f, bump: 0.02f);
        // runes cut across the chest (they glow)
        for (int k = 0; k < 5; k++)
        {
            var a = Chest + new Vector3(0.25f, 0.16f - k * 0.08f, (k % 2 == 0 ? -0.14f : 0.12f));
            var b = a + new Vector3(0.02f, -0.06f + 0.03f * (k % 3), (k % 2 == 0 ? 0.2f : -0.18f));
            s.Limb(ch, a, b, 0.018f, 0.018f, rune, Mat.Crystal, 0.008f).Emit = 0.9f;
        }
        // head: a small squared stone sunk between the shoulders, one burning slit
        s.Block(hd, Head + new Vector3(0.06f, 0.05f, 0), new(0.13f, 0.12f, 0.13f), 0.05f, stone, Mat.Rock, 0.04f, bump: 0.012f);
        s.Limb(hd, Head + new Vector3(0.15f, 0.14f, -0.12f), Head + new Vector3(0.15f, 0.14f, 0.12f), 0.05f, 0.05f, dark, Mat.Rock, 0.03f);
        var slit = new MeshBuilder();
        DecorMeshes.AddSphere(slit, Vector3.Zero, 1f, new Color(0.3f, 0.8f, 0.9f), 7);
        var visor = new MeshBuilder();
        visor.Append(slit, new Transform3D(Basis.FromScale(new Vector3(0.03f, 0.026f, 0.1f)), Head + new Vector3(0.18f, 0.075f, 0)));
        s.Rigid(hd, visor, Mat.Eye, 1f);
        s.Limb(nk, Neck + new Vector3(-0.05f, -0.05f, 0), Head, 0.13f, 0.12f, dark, Mat.Rock, 0.05f);

        // arms: boulders strung on a glowing seam, fists like millstones
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            s.Egg(ua, Shoulder[k].Lerp(Elbow[k], 0.45f), new(0.13f, 0.24f, 0.13f), stone, Mat.Rock, 0.05f, bump: 0.02f);
            s.Ball(fa, Elbow[k], 0.1f, dark, Mat.Rock, 0.04f, bump: 0.015f);
            s.Egg(fa, Elbow[k].Lerp(Wrist[k], 0.5f), new(0.14f, 0.24f, 0.14f), stone, Mat.Rock, 0.05f, bump: 0.02f);
            s.Limb(fa, Elbow[k], Wrist[k], 0.03f, 0.03f, rune, Mat.Crystal, 0.02f).Emit = 0.5f;
            var fist = Wrist[k] + new Vector3(0.02f, -0.14f, 0);
            s.Block(ha, fist, new(0.15f, 0.14f, 0.14f), 0.07f, stone, Mat.Rock, 0.04f, new Vector3(0, 0, 8f), bump: 0.02f);
            for (int f = 0; f < 3; f++)
                s.Egg(ha, fist + new Vector3(0.13f, -0.06f, (f - 1) * 0.08f), new(0.05f, 0.07f, 0.04f), dark, Mat.Rock, 0.03f, bump: 0.01f);
            // legs: short stacked stones on broad feet
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Egg(th, HipJ[k].Lerp(Knee[k], 0.45f), new(0.16f, 0.2f, 0.15f), stone, Mat.Rock, 0.05f, bump: 0.02f);
            s.Egg(sh, Knee[k].Lerp(Ankle[k], 0.5f), new(0.14f, 0.18f, 0.14f), dark, Mat.Rock, 0.05f, bump: 0.02f);
            s.Block(ft, Ankle[k] + new Vector3(0.07f, -0.06f, 0), new(0.17f, 0.07f, 0.12f), 0.04f, dark, Mat.Rock, 0.03f, bump: 0.015f);
        }
        // loose rubble stuck to the back
        for (int k = 0; k < 6; k++)
        {
            var at = Chest + new Vector3(-0.3f - 0.05f * (float)rng.NextDouble(), 0.1f - k * 0.1f, (float)(rng.NextDouble() - 0.5) * 0.4f);
            s.Ball(k < 3 ? ch : sp, at, 0.06f + 0.03f * (float)rng.NextDouble(), dark, Mat.Rock, 0.03f, bump: 0.01f);
        }
    }

    protected override Body Idle(float time)
    {
        var o = base.Idle(time * 0.6f);
        o.Lean += 10; o.SR = 6; o.SL = 4; o.ER = 12; o.EL = 12; o.AR = 14; o.AL = 14;
        return o;
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, 1.9f);
        Body o = Walk(ph, W3.SmoothStep(0.2f, 1.6f, speed), false, time);
        // every step lands like a dropped boulder
        o.Root.Y -= 0.03f * MathF.Abs(MathF.Sin(ph * Mathf.Tau)) * W3.SmoothStep(0.2f, 1.6f, speed);
        o.AR = o.AL = 14f;
        p.Glow = 1f + 0.15f * MathF.Sin(time * 1.5f);
        switch (c)
        {
            case "slam_windup":
                {
                    float k = W3.Smooth01(t);
                    o = Chop(o, k, 0f, rightOnly: false);
                    o.AR = o.AL = 8f;
                    o.Lean -= 8f * k; o.HeadPitch = 12f * k;
                    o.KR += 12f * k; o.KL += 12f * k;
                    p.Glow = 1f + 1.8f * k;
                    break;
                }
            case "slam":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 2.2f));
                    o = Chop(o, 1f, k, rightOnly: false);
                    o.AR = o.AL = 8f;
                    o.ER = o.EL = Mathf.Lerp(70f, 0f, k);
                    o.Lean += 18f * k; o.KR += 30f * k; o.KL += 30f * k; o.Root.Y -= 0.1f * k;
                    p.Glow = 2.8f - 1.4f * t;
                    break;
                }
            case "recover": o = Mix(Chop(o, 1f, 1f, rightOnly: false), o, W3.Smooth01(t)); break;
            case "hurt": o = Hurt(o, t); o.Lean += 10f; break;
            case "death":
                {
                    // the knees go, then it topples forward onto its face
                    float k1 = W3.SmoothStep(0f, 0.35f, t), k2 = W3.SmoothStep(0.3f, 0.85f, t);
                    o.KR += 80f * k1; o.KL += 80f * k1; o.HR += 60f * k1; o.HL += 60f * k1;
                    o.Root = new Vector3(0.1f * k2, -0.38f * k1 - 0.2f * k2, 0);
                    o.Lean = 20f + 60f * k2; o.SR = o.SL = 40f + 60f * k2; o.ER = o.EL = 20f;
                    o.HeadPitch = -30f * k2;
                    p.Glow = 1f - k1;
                    break;
                }
        }
        Apply(p, o);
    }
}
