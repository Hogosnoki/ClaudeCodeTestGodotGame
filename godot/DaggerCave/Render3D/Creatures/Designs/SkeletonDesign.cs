using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A walking skeleton: yellowed bone, a ribcage you can see through, deep sockets with cold
/// fire in them, a rust-eaten blade and grave rags. When it "dies" it clatters apart into
/// a heap; when it rises again, the heap pulls itself back together.
/// </summary>
public sealed class SkeletonDesign : BipedDesign
{
    public SkeletonDesign()
    {
        P = new Spec
        {
            Floor = -0.5625f, HipH = 0.8f, Spine = 0.14f, Chest = 0.2f, Neck = 0.13f, HeadUp = 0.07f, Hunch = 0.05f,
            ShoulderW = 0.16f, HipW = 0.085f, UpperArm = 0.27f, ForeArm = 0.25f, Hand = 0.09f, Thigh = 0.4f, Shin = 0.37f, Foot = 0.14f,
        };
    }

    public override string Name => "skeleton";
    public override float Cell => 0.011f;
    public override float ThreeQuarter => 22f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.3f, 0.06f), EyeEnergy = 2.6f,
        Rim = new Color(0.7f, 0.6f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 26f, DetailStrength = 1f,
    };

    private static readonly Color Bone = new(0.74f, 0.67f, 0.52f);

    public override void Sculpt(Sculptor s)
    {
        var dirty = Bone.Darkened(0.35f);
        var rag = new Color(0.22f, 0.2f, 0.18f);
        var rust = new Color(0.42f, 0.25f, 0.14f);
        BuildSkeleton(s);
        int hd = s["head"], jw = s["jaw"], ch = s["chest"], sp = s["spine"], hp = s["hips"], nk = s["neck"];

        // skull: cranium, deep sockets, nasal hole, cheekbones, upper teeth; the jaw on its own bone
        var sk = Head + new Vector3(0.03f, 0.08f, 0);
        s.Egg(hd, sk, new(0.1f, 0.098f, 0.085f), Bone, Mat.Bone, 0.02f);
        s.Egg(hd, sk + new Vector3(0.05f, -0.055f, 0), new(0.065f, 0.045f, 0.07f), Bone, Mat.Bone, 0.02f);
        s.CarveBall(hd, sk + new Vector3(0.09f, -0.005f, 0.038f), 0.024f, 0.006f);
        s.CarveBall(hd, sk + new Vector3(0.09f, -0.005f, -0.038f), 0.024f, 0.006f);
        s.CarveLimb(hd, sk + new Vector3(0.105f, -0.035f, 0), sk + new Vector3(0.11f, -0.058f, 0), 0.01f, 0.013f, 0.004f);
        s.Limb(hd, sk + new Vector3(0.06f, -0.04f, 0.06f), sk + new Vector3(0.02f, -0.045f, 0.075f), 0.015f, 0.012f, Bone, Mat.Bone, 0.01f);
        s.Limb(hd, sk + new Vector3(0.06f, -0.04f, -0.06f), sk + new Vector3(0.02f, -0.045f, -0.075f), 0.015f, 0.012f, Bone, Mat.Bone, 0.01f);
        DesignKit.Teeth(s, hd, sk + new Vector3(0.09f, -0.085f, -0.035f), sk + new Vector3(0.105f, -0.085f, 0.035f), Vector3.Down, 8, 0.016f, 0.006f, Bone.Lightened(0.1f), 0f, 0.1f);
        s.Egg(jw, sk + new Vector3(0.05f, -0.11f, 0), new(0.055f, 0.02f, 0.055f), Bone, Mat.Bone, 0.01f);
        s.Limb(jw, sk + new Vector3(0.0f, -0.1f, 0.055f), sk + new Vector3(-0.02f, -0.06f, 0.06f), 0.012f, 0.01f, Bone, Mat.Bone, 0.008f);
        s.Limb(jw, sk + new Vector3(0.0f, -0.1f, -0.055f), sk + new Vector3(-0.02f, -0.06f, -0.06f), 0.012f, 0.01f, Bone, Mat.Bone, 0.008f);
        DesignKit.Teeth(s, jw, sk + new Vector3(0.085f, -0.1f, -0.03f), sk + new Vector3(0.1f, -0.1f, 0.03f), Vector3.Up, 7, 0.013f, 0.005f, Bone.Lightened(0.1f), 0f, 0.1f);
        // eye-fires in the sockets, flames licking back out of them
        foreach (int sd in new[] { 1, -1 })
        {
            var e = sk + new Vector3(0.083f, -0.004f, 0.04f * sd);
            s.Eye(hd, e, 0.016f, new Color(0.9f, 0.3f, 0.1f), 1f);
            // the flame streams back along the temple, outside the skull
            s.Horn(hd, e + new Vector3(0.012f, 0.004f, 0.01f * sd), e + new Vector3(-0.06f, 0.04f, 0.065f * sd), 0.007f, new Color(0.9f, 0.3f, 0.1f), Mat.Eye,
                new Vector3(0, 0.3f, sd).Normalized(), 0.02f, 5, 5, emit: 0.6f);
        }

        // spine: vertebrae from the pelvis to the skull
        for (int k = 0; k <= 9; k++)
        {
            float t = k / 9f;
            var at = t < 0.5f ? Hips.Lerp(Chest, t * 2f) : Chest.Lerp(Head, (t - 0.5f) * 2f);
            at += new Vector3(-0.05f + (t > 0.5f ? 0.03f : 0f), 0, 0);
            int b = t < 0.3f ? hp : t < 0.55f ? sp : t < 0.85f ? ch : nk;
            s.Ball(b, at, 0.022f - 0.006f * t, dirty, Mat.Bone, 0.012f);
            s.Horn(b, at + new Vector3(-0.015f, 0, 0), at + new Vector3(-0.045f, -0.01f, 0), 0.01f, Bone, Mat.Bone, sides: 4, rings: 2);
        }
        // pelvis
        s.Egg(hp, Hips + new Vector3(0, 0.02f, 0), new(0.06f, 0.06f, 0.12f), Bone, Mat.Bone, 0.02f);
        s.CarveBall(hp, Hips + new Vector3(0.05f, 0.0f, 0.06f), 0.04f, 0.01f);
        s.CarveBall(hp, Hips + new Vector3(0.05f, 0.0f, -0.06f), 0.04f, 0.01f);
        // ribcage: curved ribs from the spine round to the breastbone
        var ribs = new MeshBuilder();
        for (int k = 0; k < 5; k++)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float y = Chest.Y + 0.1f - k * 0.045f;
                float wide = 0.1f + 0.012f * (k < 3 ? k : 4 - k);
                var path = new List<Vector3>();
                var radii = new List<float>();
                for (int i = 0; i <= 8; i++)
                {
                    float a = i / 8f * MathF.PI * 0.95f;
                    float x = Chest.X - 0.05f + (1f - MathF.Cos(a)) * 0.075f;
                    float z = MathF.Sin(a) * wide * sd;
                    path.Add(new Vector3(x, y - (1f - MathF.Cos(a)) * 0.03f, z));
                    radii.Add(0.0085f);
                }
                ribs.Tube(path, radii, 5, Bone, capStart: true);
            }
        ribs.Tube(new[] { Chest + new Vector3(0.1f, 0.1f, 0), Chest + new Vector3(0.1f, -0.1f, 0) }, new[] { 0.014f, 0.01f }, 5, Bone, capStart: true);
        s.Rigid(ch, ribs, Mat.Bone);
        // clavicles and shoulder blades
        s.Limb(ch, Chest + new Vector3(0.06f, 0.12f, 0), Shoulder[0] + new Vector3(0, 0.02f, 0), 0.011f, 0.011f, Bone, Mat.Bone, 0.006f);
        s.Limb(ch, Chest + new Vector3(0.06f, 0.12f, 0), Shoulder[1] + new Vector3(0, 0.02f, 0), 0.011f, 0.011f, Bone, Mat.Bone, 0.006f);

        // limbs: long bones with knobbled joints, twin forearm/shin bones, finger and toe bones
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            float z = k == 0 ? 1 : -1;
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], ha = s["hand" + sfx], th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Ball(ua, Shoulder[k], 0.026f, Bone, Mat.Bone, 0.01f);
            s.Limb(ua, Shoulder[k], Elbow[k], 0.015f, 0.013f, Bone, Mat.Bone, 0.008f);
            s.Ball(fa, Elbow[k], 0.02f, dirty, Mat.Bone, 0.008f);
            s.Limb(fa, Elbow[k] + new Vector3(0.01f, 0, 0), Wrist[k] + new Vector3(0.01f, 0, 0), 0.01f, 0.009f, Bone, Mat.Bone, 0.004f);
            s.Limb(fa, Elbow[k] - new Vector3(0.01f, 0, 0), Wrist[k] - new Vector3(0.01f, 0, 0), 0.009f, 0.008f, Bone, Mat.Bone, 0.004f);
            s.Egg(ha, Wrist[k] + new Vector3(0.005f, -0.035f, 0), new(0.022f, 0.035f, 0.02f), Bone, Mat.Bone, 0.008f);
            for (int f = 0; f < 4; f++)
            {
                var root = Wrist[k] + new Vector3(0.01f, -0.065f, (f - 1.5f) * 0.011f);
                s.Limb(ha, root, root + new Vector3(0.025f, -0.05f, 0), 0.006f, 0.004f, Bone, Mat.Bone, 0.003f);
            }
            s.Ball(th, HipJ[k], 0.028f, Bone, Mat.Bone, 0.012f);
            s.Limb(th, HipJ[k], Knee[k], 0.02f, 0.016f, Bone, Mat.Bone, 0.01f);
            s.Ball(sh, Knee[k], 0.026f, dirty, Mat.Bone, 0.01f);
            s.Limb(sh, Knee[k] + new Vector3(0.012f, 0, 0), Ankle[k], 0.014f, 0.011f, Bone, Mat.Bone, 0.006f);
            s.Limb(sh, Knee[k] - new Vector3(0.012f, 0, 0), Ankle[k] - new Vector3(0.008f, 0, 0), 0.008f, 0.007f, Bone, Mat.Bone, 0.004f);
            s.Egg(ft, Ankle[k] + new Vector3(0.04f, -0.02f, 0), new(0.055f, 0.018f, 0.028f), Bone, Mat.Bone, 0.01f);
            for (int f = 0; f < 3; f++)
            {
                var root = Ankle[k] + new Vector3(0.08f, -0.03f, (f - 1) * 0.015f);
                s.Limb(ft, root, root + new Vector3(0.05f, -0.005f, 0), 0.007f, 0.005f, Bone, Mat.Bone, 0.003f);
            }
        }
        // grave rags hanging from the hips
        s.Block(hp, Hips + new Vector3(0.05f, -0.12f, 0.05f), new(0.012f, 0.13f, 0.06f), 0.01f, rag, Mat.Cloth, 0.02f, new Vector3(0, 0, 6f), bump: 0.008f);
        s.Block(hp, Hips + new Vector3(-0.05f, -0.1f, -0.03f), new(0.012f, 0.11f, 0.07f), 0.01f, rag, Mat.Cloth, 0.02f, new Vector3(0, 0, -8f), bump: 0.008f);
        // a rusted blade in the right hand
        var sword = PropMeshes.Sword(0.5f, 0.036f, rust, rust.Darkened(0.3f), rag, 0.08f);
        var sw = new MeshBuilder();
        sw.Append(sword, new Transform3D(Basis.Identity, Wrist[0] + new Vector3(0.015f, -0.06f, 0)));
        s.Rigid(s["hand_r"], sw, Mat.Metal);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Gait(p, a, 1.6f);
        // a stiff, lurching gait with a head that lolls
        Body o = !a.OnFloor && !a.InWater ? Air(a.Vel.Y) : Walk(ph, W3.SmoothStep(0.2f, 2f, speed), false, time);
        o.HeadRoll = 12f * MathF.Sin(time * 0.9f);
        o.Jaw = 6f + 14f * Math.Max(0, MathF.Sin(time * 6f)) * Math.Max(0, MathF.Sin(time * 0.8f));
        o.SR = Math.Min(o.SR, 30f) + 10f; o.ER += 12f; o.WR = 40f;
        ref float collapsed = ref p.Vars[1];
        switch (c)
        {
            case "windup": o = Chop(o, W3.Smooth01(t), 0f); o.Jaw = 25; break;
            case "slash": o = Chop(o, 1f, W3.Smooth01(Math.Min(1f, t * 1.5f))); o.Jaw = 30; break;
            case "recover":
                if (collapsed > 0.5f) { ApplyHeap(p, Idle(time), 1f - W3.Smooth01(t)); if (t > 0.97f) collapsed = 0; return; }
                o = Mix(Chop(o, 1f, 1f), o, W3.Smooth01(t));
                break;
            case "hurt": o = Hurt(o, t); break;
            case "death": collapsed = 1f; ApplyHeap(p, Idle(time), W3.Smooth01(Math.Min(1f, t * 1.3f))); return;
        }
        Apply(p, o);
    }

    /// <summary>Falls apart into a heap (k = 1) or stands assembled (k = 0).</summary>
    private void ApplyHeap(CreaturePose p, Body stand, float k)
    {
        var o = stand;
        o.Root = new Vector3(-0.05f * k, -(Hips.Y - P.Floor - 0.06f) * k, 0);
        o.Lean = Mathf.Lerp(o.Lean, 70f, k);
        o.KR = Mathf.Lerp(o.KR, 150f, k); o.KL = Mathf.Lerp(o.KL, 140f, k);
        o.HR = Mathf.Lerp(o.HR, 80f, k); o.HL = Mathf.Lerp(o.HL, 60f, k); o.HRx = 25f * k; o.HLx = 30f * k;
        o.SR = Mathf.Lerp(o.SR, -30f, k); o.AR = 60f * k; o.SL = Mathf.Lerp(o.SL, 50f, k); o.AL = 70f * k;
        o.HeadPitch = 40f * k; o.HeadRoll = 50f * k; o.Jaw = 35f * k;
        Apply(p, o);
        // the pieces come loose: the skull rolls off, the arms drop away
        p.Move(head, new Vector3(0.18f, -0.12f, 0.1f) * k);
        p.Move(uarm[0], new Vector3(0.1f, -0.25f, 0.12f) * k);
        p.Move(uarm[1], new Vector3(-0.12f, -0.22f, -0.1f) * k);
        p.Move(chest, new Vector3(0.05f, -0.1f, 0) * k);
    }
}
