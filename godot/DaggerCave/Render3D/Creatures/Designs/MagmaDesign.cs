using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A magma brute: a hunched mound of half-cooled lava, black crust split by cracks that glow
/// orange-white, embers drifting off its back, a head that is just a furnace mouth and two
/// white-hot eyes, arms thick as tree trunks. It lights the cave around it. It scoops molten
/// rock out of its own chest and lobs it.
/// </summary>
public sealed class MagmaDesign : BipedDesign
{
    public MagmaDesign()
    {
        P = new Spec
        {
            Floor = -0.75f, HipH = 0.42f, Spine = 0.16f, Chest = 0.24f, Neck = 0.06f, HeadUp = 0.07f, Hunch = 0.24f,
            ShoulderW = 0.34f, HipW = 0.17f, UpperArm = 0.32f, ForeArm = 0.3f, Hand = 0.14f, Thigh = 0.22f, Shin = 0.2f, Foot = 0.18f,
        };
    }

    public override string Name => "magma";
    public override float Cell => 0.02f;
    public override float ThreeQuarter => 22f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.85f, 0.5f), EyeEnergy = 4f,
        Glow = new Color(1f, 0.38f, 0.06f), GlowEnergy = 2.2f,
        Rim = new Color(1f, 0.5f, 0.2f), RimEnergy = 0.35f,
        DetailScale = 9f, DetailStrength = 1.4f,
        LightColor = new Color(1f, 0.45f, 0.12f), LightEnergy = 1.4f, LightRange = 4.5f, LightOffset = new Vector3(0.1f, 0.3f, 0),
    };

    private int _glob;

    protected override void OnBonesBound()
    {
        base.OnBonesBound();
        _glob = B("glob");
    }

    public override void Sculpt(Sculptor s)
    {
        var crust = new Color(0.16f, 0.12f, 0.1f);
        var hot = new Color(0.9f, 0.4f, 0.1f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], hd = s["head"], jw = s["jaw"];
        var rng = s.Rng;

        // the mound: lumpy crust over a molten core (the ember material glows through its cracks)
        s.Egg(hp, Hips + new Vector3(0, 0.02f, 0), new(0.26f, 0.2f, 0.26f), crust, Mat.Ember, 0.07f, bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0.02f, 0.03f, 0), new(0.34f, 0.28f, 0.34f), crust, Mat.Ember, 0.08f, bump: 0.03f);
        s.Egg(ch, Chest + new Vector3(-0.04f, 0.04f, 0), new(0.32f, 0.3f, 0.36f), crust, Mat.Ember, 0.08f, bump: 0.03f);
        s.Egg(ch, Chest + new Vector3(-0.2f, 0.24f, 0), new(0.24f, 0.18f, 0.28f), crust, Mat.Ember, 0.08f, bump: 0.03f);
        // cooled plates jutting from the back, a molten chest wound
        for (int k = 0; k < 7; k++)
        {
            var at = Chest + new Vector3(-0.25f - 0.08f * (float)rng.NextDouble(), 0.32f - k * 0.12f, (float)(rng.NextDouble() - 0.5) * 0.4f);
            s.Horn(k < 4 ? ch : sp, at, at + new Vector3(-0.12f, 0.1f + 0.06f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.12f), 0.07f, crust.Darkened(0.3f), Mat.Rock, Vector3.Up, 0.02f, 6, 3);
        }
        s.Egg(ch, Chest + new Vector3(0.24f, -0.02f, 0), new(0.08f, 0.13f, 0.14f), hot, Mat.Slime, 0.05f).Emit = 1.1f;

        // head: a furnace mouth, white-hot eyes under a crusted brow
        s.Egg(hd, Head + new Vector3(0.1f, 0.02f, 0), new(0.14f, 0.12f, 0.15f), crust, Mat.Ember, 0.06f, bump: 0.015f);
        s.Limb(hd, Head + new Vector3(0.18f, 0.08f, -0.1f), Head + new Vector3(0.18f, 0.08f, 0.1f), 0.04f, 0.04f, crust.Darkened(0.3f), Mat.Rock, 0.03f);
        s.Egg(jw, Head + new Vector3(0.14f, -0.08f, 0), new(0.12f, 0.05f, 0.12f), crust, Mat.Ember, 0.03f);
        s.CarveLimb(hd, Head + new Vector3(0.28f, -0.03f, -0.07f), Head + new Vector3(0.28f, -0.03f, 0.07f), 0.035f, 0.035f, 0.015f);
        s.Egg(hd, Head + new Vector3(0.17f, -0.03f, 0), new(0.07f, 0.03f, 0.08f), hot, Mat.Slime, 0.02f).Emit = 1.4f;
        foreach (int sd in new[] { 1, -1 })
            s.Eye(hd, Head + new Vector3(0.22f, 0.05f, 0.065f * sd), 0.024f, new Color(1f, 0.8f, 0.5f), 1f);

        // arms: thick, crusted, dripping; hands like shovels
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx];
            s.Limb(ua, Shoulder[k] + new Vector3(0, 0.05f, 0), Elbow[k], 0.15f, 0.12f, crust, Mat.Ember, 0.05f, bump: 0.02f);
            s.Limb(fa, Elbow[k], Wrist[k], 0.13f, 0.11f, crust, Mat.Ember, 0.04f, bump: 0.02f);
            var palm = Wrist[k] + new Vector3(0.03f, -0.1f, 0);
            s.Egg(ha, palm, new(0.12f, 0.1f, 0.1f), crust, Mat.Ember, 0.04f, bump: 0.015f);
            for (int f = 0; f < 3; f++)
                s.Horn(ha, palm + new Vector3(0.08f, -0.05f, (f - 1) * 0.06f), palm + new Vector3(0.16f, -0.13f, (f - 1) * 0.07f), 0.035f, crust.Darkened(0.3f), Mat.Rock, Vector3.Up, 0.02f, 5, 3);
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(th, HipJ[k], Knee[k], 0.14f, 0.12f, crust, Mat.Ember, 0.04f, bump: 0.02f);
            s.Limb(sh, Knee[k], Ankle[k], 0.12f, 0.1f, crust, Mat.Ember, 0.04f, bump: 0.02f);
            s.Egg(ft, Ankle[k] + new Vector3(0.07f, -0.05f, 0), new(0.14f, 0.06f, 0.1f), crust, Mat.Ember, 0.03f, bump: 0.01f);
        }
        // the glob it throws, in the right hand, shown only while it winds up
        var gc = Wrist[0] + new Vector3(0.06f, -0.26f, 0);
        int glob = s.Bone("glob", s["hand_r"], gc);
        var gm = new MeshBuilder();
        DecorMeshes.AddSphere(gm, gc, 0.11f, hot, 8);
        s.Rigid(glob, gm, Mat.Slime, 1.6f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, 1.5f);
        Body o = Walk(ph, W3.SmoothStep(0.2f, 1.3f, speed), false, time);
        o.Lean += 12f; o.SR += 8f; o.SL += 8f; o.ER += 20f; o.EL += 20f; o.AR = o.AL = 18f;
        // the molten core breathes
        p.Glow = 1f + 0.25f * MathF.Sin(time * 2.3f) + 0.1f * MathF.Sin(time * 7.1f);
        float glob = 0.01f;
        switch (c)
        {
            case "lob_windup":
                {
                    // reach into its own chest, draw out a glob, raise it behind the head
                    float k = W3.Smooth01(t);
                    o.SR = Mathf.Lerp(30f, 175f, k); o.ER = Mathf.Lerp(90f, 60f, k); o.AR = 10f;
                    o.Twist = -25f * k; o.Lean -= 12f * k;
                    glob = W3.SmoothStep(0.15f, 0.5f, t);
                    p.Glow = 1.3f + 1.2f * k;
                    break;
                }
            case "lob":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 2f));
                    o.SR = Mathf.Lerp(175f, 20f, k); o.ER = Mathf.Lerp(60f, 5f, k); o.AR = 10f;
                    o.Twist = Mathf.Lerp(-25f, 20f, k); o.Lean += 18f * k;
                    glob = t < 0.25f ? 1f : 0.01f;
                    p.Glow = 2.4f - 1.4f * t;
                    break;
                }
            case "hurt": o = Hurt(o, t); p.Glow = 2f; break;
            case "death":
                {
                    // cools and slumps: the glow dies, it sinks into a heap
                    float k = W3.Smooth01(t);
                    o = Mix(o, Die(t, 0.3f), 0.5f);
                    o.KR += 60f * k; o.KL += 60f * k; o.Lean = 30f * k; o.Root = new Vector3(0, -0.35f * k, 0);
                    p.Glow = 1f - 0.85f * k;
                    break;
                }
        }
        Apply(p, o);
        p.Grow(_glob, glob);
    }
}
