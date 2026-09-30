using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Shared motion of the four Elementals that stand on two legs (earth, frost, nature, fire): each
/// walks in its own gait with its own sway, gathers itself in the wind-up (its glow rising) and
/// throws in the strike; the design adds whatever hangs off it (orbiting rocks, dancing flames).
/// </summary>
public abstract class ElementalBiped : BipedDesign
{
    protected virtual float GaitLen => 1.8f;
    /// <summary>Metres and rate of its bobbing (the light ones float a little).</summary>
    protected virtual float Bob => 0f;
    protected virtual float BobRate => 2f;
    protected virtual float LieHeight => 0.12f;
    protected virtual bool BothHands => false;
    /// <summary>The design's own touches on the walking pose.</summary>
    protected virtual Body Style(Body o, float time, float speed) => o;
    protected virtual void Extra(CreaturePose p, in AnimInput a, string c, float t) { }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, GaitLen);
        Body o = !a.OnFloor && !a.InWater ? Air(a.Vel.Y) : Walk(ph, W3.SmoothStep(0.2f, 1.8f, speed), false, time);
        o.Root.Y += Bob * MathF.Sin(time * BobRate);
        o = Style(o, time, speed);
        p.Glow = 1f + 0.15f * MathF.Sin(time * 2.2f);
        switch (c)
        {
            case "slam_windup":
                {
                    float k = W3.Smooth01(t);
                    o = Chop(o, k, 0f, !BothHands);
                    o.Lean -= 6f * k; o.KR += 10f * k; o.KL += 10f * k;
                    p.Glow = 1f + 1.8f * k;
                    break;
                }
            case "slam":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 2.2f));
                    o = Chop(o, 1f, k, !BothHands);
                    o.Lean += 12f * k; o.Root.Y -= 0.06f * k;
                    p.Glow = 2.8f - 1.6f * t;
                    break;
                }
            case "recover": o = Mix(Chop(o, 1f, 1f, !BothHands), o, W3.Smooth01(t)); break;
            case "hurt": o = Hurt(o, t); p.Glow = 2.2f; break;
            case "death": o = Die(t, LieHeight); p.Glow = 1f - 0.9f * W3.Smooth01(t); break;
        }
        Extra(p, a, c, t);
        Apply(p, o);
    }

    protected static Color C(float r, float g, float b) => new(r, g, b);
}

// ============================================================================ earth

/// <summary>
/// The Earth Elemental: a squat construct of packed soil and fitted stone, built up in strata like a
/// cut bank, cracks in it glowing amber, grass growing on its shoulders and a few stones circling it
/// that it hasn't yet made a use of.
/// </summary>
public sealed class EarthElementalDesign : ElementalBiped
{
    public EarthElementalDesign()
    {
        P = new Spec
        {
            Floor = -0.875f, HipH = 0.55f, Spine = 0.18f, Chest = 0.3f, Neck = 0.08f, HeadUp = 0.1f, Hunch = 0.16f,
            ShoulderW = 0.4f, HipW = 0.18f, UpperArm = 0.4f, ForeArm = 0.38f, Hand = 0.2f, Thigh = 0.3f, Shin = 0.25f, Foot = 0.2f,
        };
    }

    public override string Name => "elem_earth";
    public override float Cell => 0.024f;
    public override float ThreeQuarter => 24f;
    protected override float GaitLen => 1.6f;
    protected override float LieHeight => 0.14f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.65f, 0.2f), EyeEnergy = 4f,
        Glow = new Color(1f, 0.55f, 0.15f), GlowEnergy = 2.2f,
        Rim = new Color(0.75f, 0.6f, 0.4f), RimEnergy = 0.2f,
        DetailScale = 14f, DetailStrength = 0.9f,
        LightColor = new Color(1f, 0.6f, 0.25f), LightEnergy = 0.35f, LightRange = 2.4f, LightOffset = new Vector3(0.4f, 0.8f, 0),
    };

    private readonly int[] _rock = new int[3];
    protected override void OnBonesBound()
    {
        base.OnBonesBound();
        for (int k = 0; k < 3; k++) _rock[k] = B("rock" + k);
    }

    public override void Sculpt(Sculptor s)
    {
        var soil = C(0.36f, 0.26f, 0.17f);
        var clay = C(0.52f, 0.37f, 0.22f);
        var stone = C(0.42f, 0.4f, 0.37f);
        var dark = C(0.23f, 0.17f, 0.11f);
        var amber = C(1f, 0.62f, 0.2f);
        var grass = C(0.28f, 0.42f, 0.12f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"];
        var rng = s.Rng;

        // the trunk: a bank of earth cut in layers, stone showing in it
        s.Block(hp, Hips + new Vector3(0, 0.02f, 0), new(0.2f, 0.13f, 0.24f), 0.07f, dark, Mat.Rock, 0.05f, new Vector3(0, 0, -4f), bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0.02f, 0.02f, 0), new(0.27f, 0.2f, 0.3f), soil, Mat.Rock, 0.06f, bump: 0.03f);
        s.Block(ch, Chest + new Vector3(0.02f, 0.02f, 0), new(0.27f, 0.25f, 0.37f), 0.1f, soil, Mat.Rock, 0.06f, new Vector3(0, 0, -10f), bump: 0.035f);
        for (int k = 0; k < 4; k++)
            s.Block(ch, Chest + new Vector3(0.03f, -0.16f + k * 0.11f, 0), new(0.285f, 0.022f, 0.385f), 0.01f, k % 2 == 0 ? clay : dark, Mat.Rock, 0.02f, new Vector3(0, 0, -10f), bump: 0.02f);
        s.Egg(ch, Chest + new Vector3(-0.17f, 0.16f, 0), new(0.25f, 0.2f, 0.34f), stone, Mat.Rock, 0.08f, bump: 0.03f);
        // stones set in it, and cracks that glow
        for (int k = 0; k < 6; k++)
        {
            var at = Chest + new Vector3(0.2f + 0.05f * (float)rng.NextDouble(), (float)rng.NextDouble() * 0.36f - 0.2f, ((float)rng.NextDouble() - 0.5f) * 0.55f);
            s.Ball(ch, at, 0.05f + 0.04f * (float)rng.NextDouble(), stone, Mat.Rock, 0.02f, bump: 0.02f);
        }
        for (int k = 0; k < 4; k++)
        {
            var a = Chest + new Vector3(0.26f, 0.15f - k * 0.09f, k % 2 == 0 ? -0.15f : 0.14f);
            var b = a + new Vector3(0.015f, -0.07f + 0.03f * (k % 2), k % 2 == 0 ? 0.2f : -0.2f);
            s.Limb(ch, a, b, 0.014f, 0.01f, amber, Mat.Crystal, 0.006f).Emit = 1.1f;
        }
        // shoulders: heaps of earth with grass growing on them
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Egg(ch, Shoulder[k] + new Vector3(-0.02f, 0.08f, 0.03f * z), new(0.2f, 0.17f, 0.18f), soil, Mat.Rock, 0.05f, bump: 0.03f);
            for (int g = 0; g < 7; g++)
            {
                var at = Shoulder[k] + new Vector3(-0.05f + (float)rng.NextDouble() * 0.1f, 0.22f, ((float)rng.NextDouble() - 0.5f) * 0.16f + 0.03f * z);
                s.Horn(ch, at, at + new Vector3(-0.02f, 0.09f + 0.05f * (float)rng.NextDouble(), 0.01f), 0.008f, grass, Mat.Fur, sides: 4, rings: 2);
            }
        }
        // the head: a rough block, a heavy brow, two amber eyes
        s.Block(hd, Head + new Vector3(0.06f, 0.05f, 0), new(0.14f, 0.12f, 0.15f), 0.05f, stone, Mat.Rock, 0.04f, bump: 0.02f);
        s.Limb(hd, Head + new Vector3(0.16f, 0.13f, -0.14f), Head + new Vector3(0.16f, 0.13f, 0.14f), 0.045f, 0.045f, dark, Mat.Rock, 0.03f);
        foreach (int sd in new[] { 1, -1 })
            s.Eye(hd, Head + new Vector3(0.19f, 0.07f, 0.06f * sd), 0.025f, amber, 1f);
        s.Limb(nk, Neck + new Vector3(-0.05f, -0.05f, 0), Head, 0.13f, 0.12f, dark, Mat.Rock, 0.05f);
        // arms and legs: earth-packed limbs and boulder fists
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            s.Egg(ua, Shoulder[k].Lerp(Elbow[k], 0.45f), new(0.13f, 0.24f, 0.13f), soil, Mat.Rock, 0.05f, bump: 0.03f);
            s.Ball(fa, Elbow[k], 0.1f, stone, Mat.Rock, 0.04f, bump: 0.02f);
            s.Egg(fa, Elbow[k].Lerp(Wrist[k], 0.5f), new(0.15f, 0.24f, 0.15f), clay, Mat.Rock, 0.05f, bump: 0.03f);
            var fist = Wrist[k] + new Vector3(0.02f, -0.14f, 0);
            s.Block(ha, fist, new(0.16f, 0.15f, 0.15f), 0.07f, stone, Mat.Rock, 0.04f, new Vector3(0, 0, 8f), bump: 0.03f);
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Egg(th, HipJ[k].Lerp(Knee[k], 0.45f), new(0.16f, 0.2f, 0.15f), soil, Mat.Rock, 0.05f, bump: 0.03f);
            s.Egg(sh, Knee[k].Lerp(Ankle[k], 0.5f), new(0.14f, 0.17f, 0.14f), dark, Mat.Rock, 0.05f, bump: 0.02f);
            s.Block(ft, Ankle[k] + new Vector3(0.07f, -0.06f, 0), new(0.18f, 0.07f, 0.12f), 0.04f, stone, Mat.Rock, 0.03f, bump: 0.02f);
        }
        // stones circling it, each on a bone of its own
        for (int k = 0; k < 3; k++)
        {
            float a = k / 3f * Mathf.Tau;
            var at = Chest + new Vector3(MathF.Cos(a) * 0.5f, 0.05f + 0.15f * k, MathF.Sin(a) * 0.5f);
            int rb = s.Bone("rock" + k, ch, at);
            s.Block(rb, at, new(0.08f + 0.02f * k, 0.07f, 0.08f), 0.03f, stone, Mat.Rock, 0.02f, new Vector3(20 * k, 30, 10), bump: 0.02f);
        }
    }

    protected override Body Style(Body o, float time, float speed)
    {
        // heavy: it sways its whole trunk as it plods
        o.Twist += 4f * MathF.Sin(time * 1.6f);
        o.Lean += 6f;
        return o;
    }

    protected override void Extra(CreaturePose p, in AnimInput a, string c, float t)
    {
        float time = a.Time;
        for (int k = 0; k < 3; k++)
        {
            float ang = time * (0.9f + 0.2f * k) + k * 2.1f;
            // (they rise and orbit; in the wind-up they gather above the raised hands)
            float gather = c == "slam_windup" ? W3.Smooth01(t) : 0f;
            p.Move(_rock[k], new Vector3(MathF.Cos(ang) * 0.07f * (1f - gather), MathF.Sin(ang * 1.7f) * 0.06f + 0.32f * gather, MathF.Sin(ang) * 0.07f * (1f - gather)));
            p.Add(_rock[k], time * 40f, time * 30f, 0);
        }
    }
}

// ============================================================================ frost

/// <summary>
/// The Frost Elemental: a crystalline golem, cut from pale ice in blocks and shards, a blue light
/// burning in its chest, crystals crowding its shoulders and back, and a jagged head with two white
/// eyes. It drives both fists into the ground in its strike.
/// </summary>
public sealed class FrostElementalDesign : ElementalBiped
{
    public FrostElementalDesign()
    {
        P = new Spec
        {
            Floor = -0.9375f, HipH = 0.6f, Spine = 0.2f, Chest = 0.32f, Neck = 0.08f, HeadUp = 0.1f, Hunch = 0.1f,
            ShoulderW = 0.4f, HipW = 0.19f, UpperArm = 0.42f, ForeArm = 0.4f, Hand = 0.2f, Thigh = 0.32f, Shin = 0.28f, Foot = 0.2f,
        };
    }

    public override string Name => "elem_frost";
    public override float Cell => 0.024f;
    public override float ThreeQuarter => 24f;
    protected override bool BothHands => true;
    protected override float GaitLen => 1.7f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.85f, 0.98f, 1f), EyeEnergy = 5f,
        Glow = new Color(0.4f, 0.8f, 1f), GlowEnergy = 0.9f,
        Rim = new Color(0.6f, 0.85f, 1f), RimEnergy = 0.25f,
        DetailScale = 12f, DetailStrength = 0.8f, Wet = 0.15f,
        LightColor = new Color(0.5f, 0.85f, 1f), LightEnergy = 0.45f, LightRange = 3f, LightOffset = new Vector3(0.3f, 0.9f, 0),
    };

    public override void Sculpt(Sculptor s)
    {
        var ice = C(0.3f, 0.55f, 0.8f);
        var deep = C(0.13f, 0.3f, 0.55f);
        var pale = C(0.6f, 0.85f, 1f);
        var core = C(0.5f, 0.9f, 1f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"];
        var rng = s.Rng;

        s.Block(hp, Hips + new Vector3(0, 0.02f, 0), new(0.2f, 0.14f, 0.24f), 0.05f, deep, Mat.Ice, 0.04f, new Vector3(0, 0, -4f));
        s.Block(sp, Spine + new Vector3(0.02f, 0.02f, 0), new(0.24f, 0.2f, 0.28f), 0.05f, ice, Mat.Ice, 0.05f, new Vector3(0, 0, 3f));
        s.Block(ch, Chest + new Vector3(0.02f, 0.02f, 0), new(0.26f, 0.27f, 0.36f), 0.06f, ice, Mat.Ice, 0.05f, new Vector3(0, 0, -12f));
        // the light in its chest, seen through a cut in the ice
        s.Ball(ch, Chest + new Vector3(0.22f, 0.04f, 0), 0.1f, core, Mat.Crystal, 0.02f).Emit = 1.0f;
        // crystals crowding the shoulders, the back, the brow
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Egg(ch, Shoulder[k] + new Vector3(-0.02f, 0.08f, 0.03f * z), new(0.19f, 0.16f, 0.17f), deep, Mat.Ice, 0.05f);
            for (int c = 0; c < 4; c++)
            {
                var b = Shoulder[k] + new Vector3(-0.08f + 0.05f * c, 0.14f, 0.03f * z * (c - 1));
                var tip = b + new Vector3(-0.06f + 0.03f * c, 0.22f + 0.1f * (float)rng.NextDouble(), 0.1f * z * (float)rng.NextDouble());
                s.Horn(ch, b, tip, 0.045f, pale, Mat.Crystal, sides: 6, rings: 3, emit: 0.3f);
            }
        }
        for (int c = 0; c < 6; c++)
        {
            var b = Chest + new Vector3(-0.22f, 0.22f - 0.09f * c, ((float)rng.NextDouble() - 0.5f) * 0.4f);
            s.Horn(ch, b, b + new Vector3(-0.2f - 0.08f * (float)rng.NextDouble(), 0.12f, ((float)rng.NextDouble() - 0.5f) * 0.1f), 0.05f, pale, Mat.Crystal, sides: 6, rings: 3, emit: 0.3f);
        }
        // the head: faceted, a crown of shards, two white eyes
        s.Block(hd, Head + new Vector3(0.06f, 0.05f, 0), new(0.13f, 0.13f, 0.13f), 0.03f, ice, Mat.Ice, 0.03f, new Vector3(0, 0, 6f));
        for (int c = 0; c < 5; c++)
        {
            float z = (c - 2) * 0.05f;
            s.Horn(hd, Head + new Vector3(0.0f, 0.16f, z), Head + new Vector3(-0.05f, 0.3f - 0.03f * Math.Abs(c - 2), z * 1.5f), 0.03f, pale, Mat.Crystal, sides: 6, rings: 3, emit: 0.4f);
        }
        foreach (int sd in new[] { 1, -1 })
            s.Eye(hd, Head + new Vector3(0.18f, 0.08f, 0.055f * sd), 0.024f, C(0.9f, 1f, 1f), 1f);
        s.Limb(nk, Neck + new Vector3(-0.05f, -0.05f, 0), Head, 0.12f, 0.11f, deep, Mat.Ice, 0.05f);
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            s.Egg(ua, Shoulder[k].Lerp(Elbow[k], 0.45f), new(0.12f, 0.24f, 0.12f), ice, Mat.Ice, 0.04f);
            s.Ball(fa, Elbow[k], 0.09f, deep, Mat.Ice, 0.03f);
            s.Egg(fa, Elbow[k].Lerp(Wrist[k], 0.5f), new(0.13f, 0.24f, 0.13f), ice, Mat.Ice, 0.04f);
            s.Horn(fa, Elbow[k] + new Vector3(-0.02f, 0.02f, 0.06f * (k == 0 ? 1 : -1)), Elbow[k] + new Vector3(-0.12f, 0.16f, 0.12f * (k == 0 ? 1 : -1)), 0.035f, pale, Mat.Crystal, sides: 6, rings: 3, emit: 0.3f);
            var fist = Wrist[k] + new Vector3(0.02f, -0.14f, 0);
            s.Block(ha, fist, new(0.15f, 0.14f, 0.14f), 0.04f, ice, Mat.Ice, 0.03f, new Vector3(0, 0, 8f));
            for (int f = 0; f < 3; f++)
                s.Horn(ha, fist + new Vector3(0.1f, -0.03f, (f - 1) * 0.08f), fist + new Vector3(0.24f, -0.09f, (f - 1) * 0.1f), 0.03f, pale, Mat.Crystal, sides: 5, rings: 3, emit: 0.3f);
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Egg(th, HipJ[k].Lerp(Knee[k], 0.45f), new(0.15f, 0.2f, 0.14f), ice, Mat.Ice, 0.05f);
            s.Egg(sh, Knee[k].Lerp(Ankle[k], 0.5f), new(0.13f, 0.18f, 0.13f), deep, Mat.Ice, 0.05f);
            s.Block(ft, Ankle[k] + new Vector3(0.07f, -0.06f, 0), new(0.17f, 0.07f, 0.12f), 0.03f, ice, Mat.Ice, 0.03f);
        }
    }

    protected override Body Style(Body o, float time, float speed)
    {
        o.Twist += 3f * MathF.Sin(time * 1.4f);
        o.Lean += 4f;
        return o;
    }
}

// ============================================================================ nature

/// <summary>
/// The Nature Elemental: roots wound together into a rough shape of a man, bark-dark and knotted,
/// long branch arms ending in root-fingers, a tangle of rootlets for a face with two green eyes,
/// and leaves on its shoulders, its back and its crown.
/// </summary>
public sealed class NatureElementalDesign : ElementalBiped
{
    public NatureElementalDesign()
    {
        P = new Spec
        {
            Floor = -0.8125f, HipH = 0.5f, Spine = 0.18f, Chest = 0.26f, Neck = 0.08f, HeadUp = 0.09f, Hunch = 0.14f,
            ShoulderW = 0.3f, HipW = 0.14f, UpperArm = 0.4f, ForeArm = 0.4f, Hand = 0.16f, Thigh = 0.28f, Shin = 0.25f, Foot = 0.16f,
        };
    }

    public override string Name => "elem_nature";
    public override float Cell => 0.02f;
    public override float ThreeQuarter => 22f;
    protected override float GaitLen => 1.7f;
    protected override float Bob => 0.01f;
    protected override float LieHeight => 0.1f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.55f, 1f, 0.3f), EyeEnergy = 4f,
        Glow = new Color(0.45f, 1f, 0.3f), GlowEnergy = 1.6f,
        Rim = new Color(0.6f, 0.85f, 0.4f), RimEnergy = 0.3f,
        DetailScale = 26f, DetailStrength = 1f,
        LightColor = new Color(0.4f, 1f, 0.3f), LightEnergy = 0.35f, LightRange = 2.2f, LightOffset = new Vector3(0.3f, 0.7f, 0),
    };

    private int _crown;
    protected override void OnBonesBound() { base.OnBonesBound(); _crown = B("crown"); }

    public override void Sculpt(Sculptor s)
    {
        var bark = C(0.27f, 0.18f, 0.1f);
        var bark2 = C(0.36f, 0.25f, 0.14f);
        var leaf = C(0.22f, 0.5f, 0.12f);
        var leaf2 = C(0.4f, 0.65f, 0.2f);
        var glow = C(0.55f, 1f, 0.3f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"];
        int crown = s.Bone("crown", hd, Head + new Vector3(0, 0.12f, 0));
        var rng = s.Rng;
        float R() => (float)rng.NextDouble();

        // the trunk: three or four thick roots twisted together, knots and burls
        for (int k = 0; k < 4; k++)
        {
            float z = (k - 1.5f) * 0.09f;
            s.Limb(hp, Hips + new Vector3(0.0f, -0.02f, z), Spine + new Vector3(0.02f, 0.02f, z * 1.2f), 0.1f, 0.09f, k % 2 == 0 ? bark : bark2, Mat.Wood, 0.03f, bump: 0.02f);
            s.Limb(sp, Spine + new Vector3(0.02f, 0.02f, z * 1.2f), Chest + new Vector3(0.03f, 0.05f, z * 1.5f), 0.09f, 0.1f, k % 2 == 0 ? bark2 : bark, Mat.Wood, 0.03f, bump: 0.02f);
        }
        s.Egg(ch, Chest + new Vector3(0.02f, 0.06f, 0), new(0.2f, 0.2f, 0.28f), bark, Mat.Wood, 0.06f, bump: 0.03f);
        // a hollow in the chest with a green light in it
        s.CarveBall(ch, Chest + new Vector3(0.22f, 0.04f, 0), 0.07f, 0.02f);
        s.Ball(ch, Chest + new Vector3(0.17f, 0.04f, 0), 0.05f, glow, Mat.Crystal, 0.01f).Emit = 1.4f;
        // shoulders, back and crown wear leaves
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Egg(ch, Shoulder[k] + new Vector3(-0.02f, 0.07f, 0.02f * z), new(0.14f, 0.12f, 0.13f), bark2, Mat.Wood, 0.04f, bump: 0.03f);
        }
        void Leaves(int bone, Vector3 at, int n, float spread, float size)
        {
            for (int k = 0; k < n; k++)
            {
                var p = at + new Vector3((R() - 0.5f) * spread, (R() - 0.3f) * spread * 0.8f, (R() - 0.5f) * spread);
                s.Egg(bone, p, new Vector3(size * 1.3f, size * 0.18f, size * 0.7f), R() < 0.5f ? leaf : leaf2, Mat.Fungus, 0.01f, new Vector3(R() * 360, R() * 360, R() * 360), 0.01f);
            }
        }
        Leaves(ch, Shoulder[0] + new Vector3(-0.02f, 0.13f, 0.03f), 9, 0.2f, 0.07f);
        Leaves(ch, Shoulder[1] + new Vector3(-0.02f, 0.13f, -0.03f), 9, 0.2f, 0.07f);
        Leaves(ch, Chest + new Vector3(-0.2f, 0.1f, 0), 16, 0.3f, 0.075f);
        Leaves(hp, Hips + new Vector3(-0.1f, 0.0f, 0), 7, 0.2f, 0.06f);
        // the head: a knot of rootlets, hollow eyes with green fire in them, a crown of leaves
        s.Egg(hd, Head + new Vector3(0.05f, 0.05f, 0), new(0.12f, 0.13f, 0.12f), bark2, Mat.Wood, 0.04f, bump: 0.03f);
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * Mathf.Tau;
            var b = Head + new Vector3(0.08f + 0.05f * MathF.Cos(a), 0.06f + 0.06f * MathF.Sin(a), 0.1f * MathF.Sin(a * 2f));
            s.Horn(hd, b, b + new Vector3(0.05f, -0.1f - 0.06f * R(), (R() - 0.5f) * 0.08f), 0.014f, bark, Mat.Wood, sides: 4, rings: 3);
        }
        foreach (int sd in new[] { 1, -1 })
        {
            s.CarveBall(hd, Head + new Vector3(0.14f, 0.07f, 0.05f * sd), 0.03f, 0.01f);
            s.Eye(hd, Head + new Vector3(0.125f, 0.07f, 0.05f * sd), 0.02f, glow, 1f);
        }
        Leaves(crown, Head + new Vector3(0.0f, 0.2f, 0), 12, 0.2f, 0.06f);
        s.Limb(nk, Neck + new Vector3(-0.04f, -0.05f, 0), Head, 0.08f, 0.09f, bark, Mat.Wood, 0.04f, bump: 0.02f);
        // arms: long branches, forked, ending in root-fingers; legs: roots gripping the ground
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            float z = k == 0 ? 1 : -1;
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            s.Limb(ua, Shoulder[k], Elbow[k], 0.075f, 0.06f, bark2, Mat.Wood, 0.03f, bump: 0.025f);
            s.Ball(fa, Elbow[k], 0.065f, bark, Mat.Wood, 0.02f, bump: 0.02f);
            s.Limb(fa, Elbow[k], Wrist[k], 0.06f, 0.05f, bark, Mat.Wood, 0.03f, bump: 0.025f);
            s.Horn(fa, Elbow[k] + new Vector3(0, -0.05f, 0), Elbow[k] + new Vector3(-0.14f, -0.02f, 0.06f * z), 0.02f, bark2, Mat.Wood, sides: 5, rings: 3);
            var palm = Wrist[k] + new Vector3(0.01f, -0.05f, 0);
            s.Ball(ha, palm, 0.06f, bark2, Mat.Wood, 0.02f, bump: 0.02f);
            for (int f = 0; f < 4; f++)
                s.Horn(ha, palm, palm + new Vector3(0.05f + 0.04f * (f % 2), -0.16f - 0.03f * R(), (f - 1.5f) * 0.05f), 0.016f, bark, Mat.Wood, sides: 5, rings: 3, curl: 0.3f);
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(th, HipJ[k], Knee[k], 0.09f, 0.07f, bark2, Mat.Wood, 0.03f, bump: 0.025f);
            s.Limb(sh, Knee[k], Ankle[k], 0.07f, 0.055f, bark, Mat.Wood, 0.03f, bump: 0.025f);
            for (int f = 0; f < 4; f++)
                s.Horn(ft, Ankle[k], new Vector3(Ankle[k].X + 0.1f + 0.06f * (f % 2), P.Floor + 0.015f, Ankle[k].Z + (f - 1.5f) * 0.07f), 0.03f, bark2, Mat.Wood, sides: 5, rings: 3);
        }
    }

    protected override Body Style(Body o, float time, float speed)
    {
        // a slow sway, like a tree in a wind, its long arms swinging loose
        o.Twist += 5f * MathF.Sin(time * 1.1f);
        o.SR += 6f * MathF.Sin(time * 1.3f); o.SL += 6f * MathF.Sin(time * 1.3f + 2f);
        o.Lean += 4f;
        return o;
    }

    protected override void Extra(CreaturePose p, in AnimInput a, string c, float t)
    {
        // the crown of leaves trembles
        p.Add(_crown, 0, 0, 5f * MathF.Sin(a.Time * 2.4f));
    }
}

// ============================================================================ fire

/// <summary>
/// The Fire Elemental: not one flame but many. Small flames, each its own teardrop of fire, stand
/// together in the shape of one larger one: a head of two, a chest of five, arms and legs of three
/// apiece, each licking upward on its own. They flare as it gathers to throw.
/// </summary>
public sealed class FireElementalDesign : ElementalBiped
{
    public FireElementalDesign()
    {
        P = new Spec
        {
            Floor = -0.75f, HipH = 0.46f, Spine = 0.14f, Chest = 0.2f, Neck = 0.06f, HeadUp = 0.08f, Hunch = 0.06f,
            ShoulderW = 0.22f, HipW = 0.1f, UpperArm = 0.26f, ForeArm = 0.24f, Hand = 0.1f, Thigh = 0.24f, Shin = 0.2f, Foot = 0.1f,
        };
    }

    public override string Name => "elem_fire";
    public override float Cell => 0.016f;
    public override float ThreeQuarter => 22f;
    protected override float GaitLen => 1.4f;
    protected override float Bob => 0.025f;
    protected override float BobRate => 6f;
    protected override float LieHeight => 0.05f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.95f, 0.7f), EyeEnergy = 6f,
        Glow = new Color(1f, 0.55f, 0.15f), GlowEnergy = 4.5f,
        Rim = new Color(1f, 0.6f, 0.25f), RimEnergy = 0.8f,
        DetailScale = 20f, DetailStrength = 0.4f,
        LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 1.6f, LightRange = 4f, LightOffset = new Vector3(0.1f, 0.6f, 0),
    };

    public override void Sculpt(Sculptor s)
    {
        BuildSkeleton(s);
        var rng = s.Rng;
        float R() => (float)rng.NextDouble();
        Color Flame(float heat) => C(1f, 0.28f + 0.5f * heat, 0.05f + 0.25f * heat * heat);
        // one small flame: a bright egg with a tongue drawn up from it
        void Wisp(int bone, Vector3 at, float size, float heat)
        {
            size *= 1.3f;
            s.Egg(bone, at, new Vector3(size * 0.75f, size, size * 0.75f), Flame(heat), Mat.Ember, 0.012f).Emit = 1.6f;
            s.Horn(bone, at + new Vector3(0, size * 0.5f, 0), at + new Vector3((R() - 0.5f) * size * 0.4f, size * (1.9f + 0.5f * R()), (R() - 0.5f) * size * 0.4f), size * 0.5f, Flame(Math.Min(1f, heat + 0.3f)), Mat.Ember, sides: 6, rings: 4, emit: 1.8f);
            s.Ball(bone, at + new Vector3(0.01f, -size * 0.1f, 0), size * 0.38f, C(1f, 0.95f, 0.65f), Mat.Ember, 0.01f).Emit = 2.4f;
        }
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], hd = s["head"];
        // hips and belly: the lowest flames, broad and dull
        Wisp(hp, Hips + new Vector3(0, -0.02f, 0.08f), 0.075f, 0.25f);
        Wisp(hp, Hips + new Vector3(0, -0.02f, -0.08f), 0.075f, 0.25f);
        Wisp(hp, Hips + new Vector3(0.03f, 0.02f, 0), 0.085f, 0.4f);
        Wisp(sp, Spine + new Vector3(0.0f, 0.0f, 0.06f), 0.07f, 0.5f);
        Wisp(sp, Spine + new Vector3(0.0f, 0.0f, -0.06f), 0.07f, 0.5f);
        // chest: five together, the hottest
        Wisp(ch, Chest + new Vector3(0.02f, 0.0f, 0), 0.1f, 0.85f);
        Wisp(ch, Chest + new Vector3(-0.02f, 0.02f, 0.1f), 0.08f, 0.65f);
        Wisp(ch, Chest + new Vector3(-0.02f, 0.02f, -0.1f), 0.08f, 0.65f);
        Wisp(ch, Chest + new Vector3(-0.07f, 0.06f, 0.03f), 0.075f, 0.55f);
        Wisp(ch, Chest + new Vector3(-0.07f, 0.06f, -0.04f), 0.075f, 0.55f);
        // head: two small bright ones, with eyes between
        Wisp(hd, Head + new Vector3(0.0f, 0.02f, 0.04f), 0.06f, 0.9f);
        Wisp(hd, Head + new Vector3(0.0f, 0.02f, -0.04f), 0.06f, 0.8f);
        foreach (int sd in new[] { 1, -1 })
            s.Eye(hd, Head + new Vector3(0.07f, 0.03f, 0.03f * sd), 0.014f, C(1f, 1f, 0.85f), 1f);
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            Wisp(ua, Shoulder[k].Lerp(Elbow[k], 0.3f), 0.06f, 0.6f);
            Wisp(ua, Shoulder[k].Lerp(Elbow[k], 0.75f), 0.055f, 0.5f);
            Wisp(fa, Elbow[k].Lerp(Wrist[k], 0.5f), 0.05f, 0.55f);
            Wisp(ha, Wrist[k] + new Vector3(0.01f, -0.05f, 0), 0.05f, 0.8f);
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            Wisp(th, HipJ[k].Lerp(Knee[k], 0.5f), 0.065f, 0.4f);
            Wisp(sh, Knee[k].Lerp(Ankle[k], 0.45f), 0.055f, 0.3f);
            Wisp(ft, Ankle[k] + new Vector3(0.03f, -0.04f, 0), 0.05f, 0.25f);
        }
    }

    protected override Body Style(Body o, float time, float speed)
    {
        // restless: it never stands still, flickering side to side
        o.Twist += 6f * MathF.Sin(time * 5.3f) * MathF.Sin(time * 1.7f);
        o.Lean += 8f;
        return o;
    }

    protected override void Extra(CreaturePose p, in AnimInput a, string c, float t)
    {
        // every flame flickers on its own: the parts breathe in and out of step
        float time = a.Time;
        p.Grow(B("chest"), 1f + 0.08f * MathF.Sin(time * 9f));
        p.Grow(B("head"), 1f + 0.1f * MathF.Sin(time * 11f + 1f));
        p.Grow(B("hips"), 1f + 0.06f * MathF.Sin(time * 8f + 2f));
        for (int k = 0; k < 2; k++)
        {
            p.Grow(uarm[k], 1f + 0.09f * MathF.Sin(time * 10f + k * 2.3f));
            p.Grow(farm[k], 1f + 0.09f * MathF.Sin(time * 12f + k * 1.7f));
            p.Grow(thigh[k], 1f + 0.07f * MathF.Sin(time * 9f + k * 3.1f));
        }
    }
}

// ============================================================================ water

/// <summary>
/// The Water Elemental: one great drop of water, round at the bottom and drawn up to a soft point,
/// with two bright eyes pressed into its face. No arms, no orbiting drops: the plain outline is what
/// reads. It floats, swells and settles; it gathers itself (squashing down, swelling wide) to spit,
/// and springs tall as it lets go.
/// </summary>
public sealed class WaterElementalDesign : CreatureDesign
{
    public override string Name => "elem_water";
    public override float Cell => 0.014f;
    public override float ThreeQuarter => 22f;
    public override float FloorY => -0.75f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.9f, 1f, 1f), EyeEnergy = 3f,
        Glow = new Color(0.3f, 0.6f, 1f), GlowEnergy = 0.5f,
        Rim = new Color(0.6f, 0.85f, 1f), RimEnergy = 0.55f,
        DetailScale = 10f, DetailStrength = 0.25f, Wet = 1f,
        LightColor = new Color(0.45f, 0.75f, 1f), LightEnergy = 0.4f, LightRange = 2.8f, LightOffset = new Vector3(0.1f, 0.1f, 0),
    };

    private int _hips, _body;

    protected override void OnBonesBound()
    {
        _hips = B("hips"); _body = B("body");
    }

    public override void Sculpt(Sculptor s)
    {
        var water = new Color(0.18f, 0.46f, 0.85f);
        var deep = new Color(0.1f, 0.3f, 0.66f);
        var pale = new Color(0.45f, 0.75f, 0.97f);
        int hips = s.Bone("hips", -1, new(0, -0.3f, 0));
        int body = s.Bone("body", hips, new(0, -0.05f, 0));
        // the drop: a full round belly, blended up into a soft tip (paler toward the top, where the light comes through)
        s.Egg(hips, new(0, -0.22f, 0), new(0.5f, 0.46f, 0.5f), deep, Mat.Slime, 0.1f).Emit = 0.1f;
        s.Egg(body, new(0, 0.22f, 0), new(0.38f, 0.36f, 0.38f), water, Mat.Slime, 0.2f).Emit = 0.06f;
        s.Egg(body, new(0, 0.5f, 0), new(0.14f, 0.24f, 0.14f), pale, Mat.Slime, 0.22f).Emit = 0.05f;
        // a face pressed into its front: two bright eyes
        foreach (int sd in new[] { 1, -1 })
            s.Eye(body, new(0.39f, -0.02f, 0.17f * sd), 0.075f, new Color(0.95f, 1f, 1f), 1f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Sqrt(a.Vel.X * a.Vel.X + a.Vel.Y * a.Vel.Y);
        float move = W3.SmoothStep(0.2f, 3f, speed);
        // it floats and swells and settles, leaning into its way
        float bob = MathF.Sin(time * 2.2f) * 0.04f;
        float swell = 0.05f * MathF.Sin(time * 3f);
        p.Move(_hips, new Vector3(0, bob, 0));
        p.Set(_body, 0, 0, -8f * move);
        p.Glow = 1f;
        // squash: below 1 is squat and wide, above 1 tall and narrow
        float squash = 1f;
        switch (c)
        {
            case "slam_windup": { float k = W3.Smooth01(t); squash = 1f - 0.22f * k; p.Glow = 1f + 1.2f * k; break; }
            case "slam": { float k = W3.Smooth01(Math.Min(1f, t * 3f)); squash = 0.78f + 0.45f * k - 0.2f * t; p.Glow = 2f - t; break; }
            case "recover": squash = 1.1f - 0.1f * W3.Smooth01(t); break;
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); squash = 1f - 0.2f * k; p.Glow = 2f; break; }
            case "death": { float k = W3.Smooth01(t); squash = 1f - 0.75f * k; p.Glow = 1f - 0.8f * k; p.Move(_hips, new Vector3(0, -0.2f * k, 0)); break; }
        }
        float wide = 1f + (1f - squash) * 0.7f;
        p.Grow(_body, new Vector3(wide * (1f + swell), squash * (1f - swell), wide * (1f + swell)));
        p.Grow(_hips, new Vector3(wide, squash, wide));
    }
}
