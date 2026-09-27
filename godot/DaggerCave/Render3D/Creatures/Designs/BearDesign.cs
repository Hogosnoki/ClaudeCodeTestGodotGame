using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A cave bear gone wrong: a mountain of matted fur with a hump of bone plates breaking through
/// its back, a skull-like muzzle full of fangs, small ember eyes, and claws like sickles. It
/// rears up to swipe, and roars before it charges.
/// </summary>
public sealed class BearDesign : QuadrupedDesign
{
    public BearDesign()
    {
        Q = new Spec
        {
            Floor = -0.875f, HipH = 0.8f, ShoulderH = 0.92f, BodyLen = 0.95f, NeckLen = 0.24f, HeadLen = 0.4f, NeckRise = 0.02f,
            TailLen = 0.14f, TailBones = 2, LegW = 0.22f, FUpper = 0.44f, FLower = 0.4f, HUpper = 0.4f, HLower = 0.36f, Paw = 0.18f,
        };
    }

    public override string Name => "bear";
    public override float Cell => 0.022f;
    public override float ThreeQuarter => 24f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.55f, 0.08f), EyeEnergy = 4.5f,
        Rim = new Color(0.6f, 0.55f, 0.5f), RimEnergy = 0.2f,
        DetailScale = 16f, DetailStrength = 1.1f, Wet = 0.4f,
    };

    public override void Sculpt(Sculptor s)
    {
        var fur = new Color(0.19f, 0.13f, 0.09f);
        var furDk = fur.Darkened(0.4f);
        var muzzle = new Color(0.34f, 0.26f, 0.2f);
        var bone = new Color(0.72f, 0.66f, 0.54f);
        var gum = new Color(0.45f, 0.12f, 0.12f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"], jw = s["jaw"];

        // the bulk
        s.Egg(hp, Hips + new Vector3(0.05f, 0.02f, 0), new(0.46f, 0.42f, 0.42f), fur, Mat.Fur, 0.08f, bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0, 0.08f, 0), new(0.5f, 0.42f, 0.44f), fur, Mat.Fur, 0.08f, bump: 0.02f);
        s.Egg(ch, Chest + new Vector3(-0.05f, 0.02f, 0), new(0.46f, 0.5f, 0.46f), fur, Mat.Fur, 0.08f, bump: 0.02f);
        s.Egg(ch, Chest + new Vector3(-0.12f, 0.4f, 0), new(0.36f, 0.26f, 0.32f), furDk, Mat.Fur, 0.1f, bump: 0.02f);
        // bone plates breaking through the shoulder hump and down the spine, stained and chipped
        var rng = s.Rng;
        var plate = new Color(0.58f, 0.52f, 0.42f);
        for (int k = 0; k < 7; k++)
        {
            float t = k / 6f;                       // 0 = over the shoulders, 1 = mid-back
            var at = Chest.Lerp(Spine, t) + new Vector3(-0.12f, 0.5f - 0.18f * t, 0);
            float len = 0.36f - 0.16f * t + 0.05f * (float)rng.NextDouble();
            int bone2 = t < 0.5f ? ch : sp;
            s.Horn(bone2, at + new Vector3(0.02f, -0.1f, 0), at + new Vector3(-0.12f - 0.05f * t, len, 0), 0.065f - 0.02f * t, plate, Mat.Bone, Vector3.Left, 0.045f);
            if (k % 2 == 0 && k < 5)
                foreach (int sd in new[] { 1, -1 })
                    s.Horn(bone2, at + new Vector3(0.02f, -0.15f, 0.2f * sd), at + new Vector3(-0.06f, len * 0.45f - 0.05f, 0.33f * sd), 0.035f, plate, Mat.Bone, Vector3.Left, 0.02f);
        }
        // scars: raw gouges across the flank
        s.Limb(sp, Spine + new Vector3(0.1f, 0.2f, 0.4f), Spine + new Vector3(-0.2f, -0.05f, 0.42f), 0.03f, 0.02f, new Color(0.5f, 0.2f, 0.18f), Mat.Flesh, 0.02f);
        s.Limb(hp, Hips + new Vector3(0.1f, 0.25f, 0.38f), Hips + new Vector3(-0.1f, 0.05f, 0.42f), 0.025f, 0.018f, new Color(0.5f, 0.2f, 0.18f), Mat.Flesh, 0.02f);

        // head: a heavy skull, a long muzzle, a gaping jaw of fangs
        s.Limb(nk, Neck + new Vector3(-0.1f, 0, 0), Head, 0.3f, 0.24f, fur, Mat.Fur, 0.08f, bump: 0.015f);
        s.Egg(hd, Head + new Vector3(0.05f, 0.06f, 0), new(0.24f, 0.2f, 0.21f), fur, Mat.Fur, 0.06f, bump: 0.012f);
        s.Limb(hd, Head + new Vector3(0.12f, 0.02f, 0), Head + new Vector3(0.38f, -0.03f, 0), 0.14f, 0.085f, muzzle, Mat.Skin, 0.04f);
        s.Egg(hd, Head + new Vector3(0.41f, -0.01f, 0), new(0.05f, 0.04f, 0.055f), new Color(0.06f, 0.05f, 0.05f), Mat.Skin, 0.02f);
        s.Limb(hd, Head + new Vector3(0.15f, 0.14f, -0.12f), Head + new Vector3(0.15f, 0.14f, 0.12f), 0.05f, 0.05f, furDk, Mat.Fur, 0.04f);
        s.Egg(jw, Head + new Vector3(0.24f, -0.12f, 0), new(0.16f, 0.05f, 0.09f), muzzle, Mat.Skin, 0.03f);
        s.Egg(jw, Head + new Vector3(0.24f, -0.1f, 0), new(0.14f, 0.025f, 0.075f), gum, Mat.Flesh, 0.02f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Egg(hd, Head + new Vector3(-0.05f, 0.2f, 0.12f * sd), new(0.05f, 0.06f, 0.025f), furDk, Mat.Fur, 0.03f);
            s.Eye(hd, Head + new Vector3(0.2f, 0.085f, 0.112f * sd), 0.027f, new Color(0.4f, 0.1f, 0.02f), 1f);
            // fangs: great upper canines, lower tusks
            s.Horn(hd, Head + new Vector3(0.33f, -0.07f, 0.055f * sd), Head + new Vector3(0.35f, -0.2f, 0.06f * sd), 0.02f, bone, Mat.Bone, Vector3.Back, 0.01f);
            s.Horn(jw, Head + new Vector3(0.3f, -0.1f, 0.05f * sd), Head + new Vector3(0.33f, -0.01f, 0.055f * sd), 0.016f, bone, Mat.Bone);
        }
        DesignKit.Teeth(s, hd, Head + new Vector3(0.15f, -0.07f, 0.07f), Head + new Vector3(0.3f, -0.08f, 0.06f), Vector3.Down, 5, 0.035f, 0.009f, bone, 0.3f);
        DesignKit.Teeth(s, hd, Head + new Vector3(0.15f, -0.07f, -0.07f), Head + new Vector3(0.3f, -0.08f, -0.06f), Vector3.Down, 5, 0.035f, 0.009f, bone, 0.3f);
        DesignKit.Teeth(s, jw, Head + new Vector3(0.14f, -0.1f, 0.06f), Head + new Vector3(0.28f, -0.1f, 0.05f), Vector3.Up, 5, 0.03f, 0.008f, bone, 0.3f);
        DesignKit.Teeth(s, jw, Head + new Vector3(0.14f, -0.1f, -0.06f), Head + new Vector3(0.28f, -0.1f, -0.05f), Vector3.Up, 5, 0.03f, 0.008f, bone, 0.3f);

        Legs(s, 0.13f, 0.16f, fur, Mat.Fur, furDk, 0.01f);
        Claws(s, 4, 0.14f, 0.022f, new Color(0.12f, 0.1f, 0.08f));
        s.Ball(s["tail0"], TailPts[0], 0.08f, fur, Mat.Fur, 0.05f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Phase(p, a, c == "run" ? 3.4f : 2.6f);
        var st = Gait(ph, W3.SmoothStep(0.2f, 1.5f, speed), c == "run", c == "run" ? 30f : 22f);
        float breathe = MathF.Sin(time * 1.6f);
        st.Arch += breathe * 2f; st.HeadUp += breathe * 2f - 4f; st.Jaw = 6f + 4f * Math.Max(0, breathe);
        st.HeadYaw += 8f * MathF.Sin(time * 0.6f);
        switch (c)
        {
            case "rear":
            case "swipe":
                {
                    // up on the hind legs, then a forepaw comes down like a guillotine
                    float up = c == "rear" ? W3.Smooth01(t) : 1f;
                    float sw = c == "swipe" ? W3.Smooth01(Math.Min(1f, t * 1.6f)) : 0f;
                    st.Pitch = 55f * up - 25f * sw;
                    st.HR = st.HL = -st.Pitch + 10f; st.HRb = st.HLb = 20f * up;
                    st.FR = Mathf.Lerp(0f, 130f, up) - 150f * sw; st.FRb = 30f * up * (1 - sw);
                    st.FL = Mathf.Lerp(0f, 100f, up) - 60f * sw; st.FLb = 50f * up;
                    st.NeckUp = -20f * up; st.HeadUp = -10f * up + 20f * sw; st.Jaw = 25f * up + 20f * sw;
                    st.Root += new Vector3(-0.1f * up, 0.05f * up, 0);
                    break;
                }
            case "roar":
                {
                    float k = Key(t, (0, 0), (0.25f, 1), (0.85f, 1), (1, 0.3f));
                    st.NeckUp = 18f * k; st.HeadUp = 25f * k; st.Jaw = 55f * k;
                    st.HeadYaw = 6f * MathF.Sin(time * 30f) * k;
                    st.FRb = st.FLb = 15f * k; st.Pitch = 4f * k;
                    st.Root.Y -= 0.04f * k;
                    break;
                }
            case "hurt": st.NeckUp -= 20f * Key(t, (0, 0), (0.2f, 1), (1, 0)); st.Jaw = 35f; break;
            case "death": st = Dead(st, W3.Smooth01(t), 0.35f); Apply(p, st); p.Add(hips, 75f * W3.Smooth01(t), 0, 0); return;
        }
        if (c == "idle" && (a.Owner as Enemy)?.Dead == false && speed < 0.2f)
        {
            // stunned bears sway their heads
            st.HeadYaw += 3f * MathF.Sin(time * 5f);
        }
        Apply(p, st);
    }
}
