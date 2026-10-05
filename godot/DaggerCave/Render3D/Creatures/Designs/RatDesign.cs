using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A giant plague rat: mangy fur over a knobbled spine, weeping bald sores, a long naked tail,
/// yellow chisel teeth, trembling whiskers and small red eyes. Packs of them crouch, then lunge.
/// </summary>
public sealed class RatDesign : QuadrupedDesign
{
    public RatDesign()
    {
        Q = new Spec
        {
            Floor = -0.375f, HipH = 0.22f, ShoulderH = 0.19f, BodyLen = 0.34f, NeckLen = 0.1f, HeadLen = 0.18f, NeckRise = 0.02f,
            TailLen = 0.62f, TailBones = 5, LegW = 0.07f, FUpper = 0.09f, FLower = 0.08f, HUpper = 0.1f, HLower = 0.09f, Paw = 0.05f,
        };
    }

    public override string Name => "rat";
    public override float Cell => 0.01f;
    public override float ThreeQuarter => 25f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.1f, 0.04f), EyeEnergy = 4.5f,
        Rim = new Color(0.6f, 0.55f, 0.5f), RimEnergy = 0.25f,
        DetailScale = 45f, DetailStrength = 1f, Wet = 0.2f, Veins = 0.4f,
    };

    public override void Sculpt(Sculptor s)
    {
        var fur = new Color(0.34f, 0.29f, 0.24f);
        var furDk = fur.Darkened(0.35f);
        var skin = new Color(0.62f, 0.45f, 0.42f);
        var sore = new Color(0.32f, 0.07f, 0.06f);
        var tooth = new Color(0.85f, 0.7f, 0.3f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"], jw = s["jaw"];

        s.Egg(hp, Hips + new Vector3(0.02f, 0.02f, 0), new(0.15f, 0.12f, 0.12f), fur, Mat.Fur, 0.05f, bump: 0.008f);
        s.Egg(sp, Spine + new Vector3(0, 0.03f, 0), new(0.16f, 0.1f, 0.1f), fur, Mat.Fur, 0.05f, bump: 0.008f);
        s.Egg(ch, Chest + new Vector3(-0.02f, 0.0f, 0), new(0.12f, 0.1f, 0.095f), fur, Mat.Fur, 0.05f, bump: 0.008f);
        for (int k = 0; k < 6; k++)
        {
            var at = Hips.Lerp(Chest, k / 5f) + new Vector3(0, 0.1f + 0.02f * MathF.Sin(k * 1.3f), 0);
            s.Ball(k < 3 ? hp : ch, at, 0.02f, furDk, Mat.Fur, 0.02f);
        }
        // bald, weeping patches
        s.Egg(hp, Hips + new Vector3(0.05f, 0.06f, 0.1f), new(0.035f, 0.028f, 0.025f), sore, Mat.Flesh, 0.02f);
        s.Egg(sp, Spine + new Vector3(0.02f, 0.08f, -0.08f), new(0.04f, 0.03f, 0.03f), skin, Mat.Skin, 0.02f);
        s.Egg(ch, Chest + new Vector3(0.0f, -0.02f, 0.085f), new(0.022f, 0.026f, 0.016f), sore, Mat.Flesh, 0.015f);

        // head: skull, long snout to a pink nose, jaw, ears, eyes, whiskers, chisel teeth
        s.Limb(nk, Neck + new Vector3(-0.04f, 0, 0), Head, 0.07f, 0.06f, fur, Mat.Fur, 0.04f);
        s.Egg(hd, Head + new Vector3(0.02f, 0.01f, 0), new(0.075f, 0.06f, 0.06f), fur, Mat.Fur, 0.03f);
        s.Limb(hd, Head + new Vector3(0.04f, 0f, 0), Head + new Vector3(0.17f, -0.04f, 0), 0.055f, 0.022f, fur, Mat.Fur, 0.02f);
        s.Ball(hd, Head + new Vector3(0.18f, -0.042f, 0), 0.017f, skin, Mat.Skin, 0.01f);
        s.Egg(jw, Head + new Vector3(0.11f, -0.07f, 0), new(0.055f, 0.017f, 0.03f), fur, Mat.Fur, 0.012f);
        s.Horn(hd, Head + new Vector3(0.165f, -0.055f, 0.007f), Head + new Vector3(0.175f, -0.1f, 0.007f), 0.007f, tooth, Mat.Bone, Vector3.Back, 0.004f, 4, 3);
        s.Horn(hd, Head + new Vector3(0.165f, -0.055f, -0.007f), Head + new Vector3(0.175f, -0.1f, -0.007f), 0.007f, tooth, Mat.Bone, Vector3.Back, 0.004f, 4, 3);
        s.Horn(jw, Head + new Vector3(0.15f, -0.075f, 0.006f), Head + new Vector3(0.16f, -0.05f, 0.006f), 0.005f, tooth, Mat.Bone, sides: 4, rings: 3);
        s.Horn(jw, Head + new Vector3(0.15f, -0.075f, -0.006f), Head + new Vector3(0.16f, -0.05f, -0.006f), 0.005f, tooth, Mat.Bone, sides: 4, rings: 3);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Egg(hd, Head + new Vector3(-0.02f, 0.06f, 0.045f * sd), new(0.03f, 0.036f, 0.01f), skin, Mat.Membrane, 0.012f, new Vector3(0, 0, 15f));
            s.Eye(hd, Head + new Vector3(0.075f, 0.022f, 0.047f * sd), 0.015f, new Color(0.35f, 0.02f, 0.02f), 1f);
            for (int w = 0; w < 4; w++)
            {
                var root = Head + new Vector3(0.15f, -0.035f + w * 0.006f, 0.02f * sd);
                s.Horn(hd, root, root + new Vector3(0.05f - w * 0.01f, 0.01f - w * 0.012f, (0.09f + w * 0.01f) * sd), 0.0015f, new Color(0.8f, 0.75f, 0.7f), Mat.Claw, sides: 3, rings: 2);
            }
        }
        // a long naked tail
        var tailBones = new int[Q.TailBones];
        for (int k = 0; k < Q.TailBones; k++) tailBones[k] = s["tail" + k];
        var radii = new float[Q.TailBones + 1];
        for (int k = 0; k <= Q.TailBones; k++) radii[k] = 0.03f * (1f - k / (float)Q.TailBones) + 0.004f;
        s.Chain(tailBones, TailPts, radii, skin.Darkened(0.2f), Mat.Scale, 0.015f);

        Legs(s, 0.025f, 0.035f, fur, Mat.Fur, skin);
        Claws(s, 4, 0.025f, 0.005f, new Color(0.2f, 0.16f, 0.12f));
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Phase(p, a, 1.1f);
        var st = Gait(ph, W3.SmoothStep(0.2f, 1.5f, speed), speed > 3f, 32f);
        // sniffing, twitching
        st.HeadUp += 6f * MathF.Sin(time * 11f) * MathF.Sin(time * 1.3f);
        st.HeadYaw += 12f * MathF.Sin(time * 0.9f);
        st.TailSway += 18f * MathF.Sin(time * 1.7f);
        st.TailUp = 8f;
        switch (c)
        {
            case "windup":
                st.Pitch = -6; st.Root.Y -= 0.05f * W3.Smooth01(t); st.NeckUp = -18; st.Jaw = 10;
                st.HRb = st.HLb = 30; st.FRb = st.FLb = 20;
                break;
            case "bite":
                {
                    float k = Key(t, (0, 0), (0.3f, 1), (1, 0.2f));
                    st.Pitch = 8 * k; st.NeckUp = 20 * k; st.HeadUp = 10 * k; st.Jaw = 45 * k;
                    st.FR = st.FL = 35 * k; st.HR = st.HL = -25 * k;
                    st.Root += new Vector3(0.08f * k, 0.04f * k, 0);
                    break;
                }
            case "hurt": st.NeckUp -= 25 * Key(t, (0, 0), (0.25f, 1), (1, 0)); st.Jaw = 30; break;
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.4f));
                    st = Dead(st, k, 0.1f);
                    p.Set(hips, 0, 0, 0);
                    Apply(p, st);
                    p.Add(hips, 150f * k, 0, 0); // rolls onto its back
                    p.Move(hips, new Vector3(0, 0.08f * k, 0));
                    return;
                }
        }
        Apply(p, st);
    }
}
