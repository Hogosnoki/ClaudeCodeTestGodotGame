using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// An American black bear, recognisable at a glance: a long, level back with no shoulder hump (the rump as high as the shoulders),
/// glossy black fur, a long tan muzzle with a straight profile, big round ears set high, a pale blaze on the chest, longish legs and
/// short claws. It rears up to swipe, and roars before it charges.
/// </summary>
public sealed class BearDesign : QuadrupedDesign
{
    public BearDesign()
    {
        Q = new Spec
        {
            // (long and level: the rump a touch higher than the shoulders, no hump; the legs about as long as the body is deep,
            // and thick, so it stands low and heavy rather than on stilts)
            Floor = -0.875f, HipH = 0.8f, ShoulderH = 0.78f, BodyLen = 1.22f, NeckLen = 0.26f, HeadLen = 0.42f, NeckRise = 0.16f,
            TailLen = 0.1f, TailBones = 2, LegW = 0.21f, FUpper = 0.36f, FLower = 0.36f, HUpper = 0.37f, HLower = 0.35f, Paw = 0.17f,
        };
    }

    public override string Name => "bear";
    public override float Cell => 0.022f;
    public override float ThreeQuarter => 24f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.55f, 0.32f, 0.1f), EyeEnergy = 1.2f,
        Rim = new Color(0.75f, 0.68f, 0.6f), RimEnergy = 0.45f,
        DetailScale = 16f, DetailStrength = 1.1f, Wet = 0.4f,
    };

    public override void Sculpt(Sculptor s)
    {
        // glossy black (kept off pure black so its masses read in the cave), a tan muzzle, a pale blaze on the chest
        var fur = new Color(0.15f, 0.13f, 0.125f);
        var furDk = new Color(0.075f, 0.065f, 0.065f);
        var sheen = new Color(0.3f, 0.28f, 0.29f);
        // (the muzzle a dull, short-furred tan: as bare skin it glowed like a lamp against the black)
        var muzzle = new Color(0.52f, 0.39f, 0.27f);
        var blaze = new Color(0.82f, 0.74f, 0.6f);
        var bone = new Color(0.82f, 0.78f, 0.68f);
        var gum = new Color(0.45f, 0.12f, 0.12f);
        var pad = new Color(0.1f, 0.075f, 0.07f);
        var nose = new Color(0.05f, 0.045f, 0.045f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"], jw = s["jaw"];

        // the body: a long, level back from withers to rump (no hump), a deep chest, the belly slung low between the legs
        s.Egg(hp, Hips + new Vector3(0.0f, 0.06f, 0), new(0.42f, 0.33f, 0.36f), fur, Mat.Fur, 0.09f, bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0, 0.04f, 0), new(0.58f, 0.31f, 0.37f), fur, Mat.Fur, 0.09f, bump: 0.02f);
        s.Egg(sp, Spine + new Vector3(0.05f, -0.13f, 0), new(0.46f, 0.18f, 0.3f), fur, Mat.Fur, 0.09f, bump: 0.018f);
        s.Egg(ch, Chest + new Vector3(0.06f, -0.05f, 0), new(0.36f, 0.36f, 0.36f), fur, Mat.Fur, 0.09f, bump: 0.02f);
        foreach (int sd in new[] { 1, -1 })
        {
            // the shoulder blades, two low mounds at the withers that roll as it walks
            s.Egg(ch, Chest + new Vector3(-0.07f, 0.2f, 0.17f * sd), new(0.22f, 0.15f, 0.12f), fur, Mat.Fur, 0.06f, bump: 0.018f);
            // the flank's fur, shaggier and longer below
            s.Egg(sp, Spine + new Vector3(0.0f, -0.06f, 0.26f * sd), new(0.42f, 0.2f, 0.12f), fur, Mat.Fur, 0.07f, bump: 0.024f);
        }
        // (a sheen along the back, so the black reads as fur in the cave light)
        s.Egg(sp, Spine + new Vector3(0, 0.27f, 0), new(0.52f, 0.06f, 0.24f), sheen, Mat.Fur, 0.06f, bump: 0.01f);
        // the chest blaze, a pale crescent between the forelegs
        s.Egg(ch, Chest + new Vector3(0.3f, -0.08f, 0), new(0.06f, 0.11f, 0.15f), blaze, Mat.Fur, 0.04f);

        // the neck, thick and short, a ruff of fur under the throat
        s.Limb(nk, Neck + new Vector3(-0.12f, 0.02f, 0), Head + new Vector3(-0.05f, 0, 0), 0.27f, 0.21f, fur, Mat.Fur, 0.09f, bump: 0.018f);
        s.Egg(nk, Neck + new Vector3(0.06f, -0.11f, 0), new(0.2f, 0.17f, 0.23f), fur, Mat.Fur, 0.07f, bump: 0.024f);
        // the head: a broad skull, wider than tall, full cheeks, a brow over the eyes
        s.Egg(hd, Head + new Vector3(-0.01f, 0.06f, 0), new(0.22f, 0.19f, 0.21f), fur, Mat.Fur, 0.06f, bump: 0.012f);
        s.Egg(hd, Head + new Vector3(0.11f, 0.13f, 0), new(0.09f, 0.065f, 0.15f), fur, Mat.Fur, 0.04f, bump: 0.01f);
        foreach (int sd in new[] { 1, -1 })
            s.Egg(hd, Head + new Vector3(0.04f, -0.005f, 0.12f * sd), new(0.13f, 0.12f, 0.09f), fur, Mat.Fur, 0.05f, bump: 0.016f);
        // the muzzle: straight in profile, thick at its root, flat on top, tan; the nose pad black with its nostrils
        s.Limb(hd, Head + new Vector3(0.08f, 0.02f, 0), Head + new Vector3(0.32f, -0.035f, 0), 0.12f, 0.085f, muzzle, Mat.Fur, 0.05f, 0.006f);
        s.Egg(hd, Head + new Vector3(0.21f, 0.035f, 0), new(0.12f, 0.05f, 0.075f), muzzle, Mat.Fur, 0.045f, bump: 0.006f);
        s.Egg(hd, Head + new Vector3(0.355f, -0.025f, 0), new(0.052f, 0.046f, 0.062f), nose, Mat.Skin, 0.018f);
        foreach (int sd in new[] { 1, -1 }) s.CarveBall(hd, Head + new Vector3(0.395f, -0.032f, 0.022f * sd), 0.013f, 0.008f);
        s.Egg(jw, Head + new Vector3(0.22f, -0.11f, 0), new(0.14f, 0.045f, 0.075f), muzzle.Darkened(0.1f), Mat.Skin, 0.03f);
        s.Egg(jw, Head + new Vector3(0.22f, -0.09f, 0), new(0.12f, 0.022f, 0.06f), gum, Mat.Flesh, 0.02f);
        foreach (int sd in new[] { 1, -1 })
        {
            // (the ears: big, round and upright, a black bear's mark, cupped open toward the front)
            s.Egg(hd, Head + new Vector3(-0.06f, 0.26f, 0.14f * sd), new(0.11f, 0.12f, 0.045f), fur, Mat.Fur, 0.04f, new(0, -18f * sd, 0), 0.008f);
            s.Egg(hd, Head + new Vector3(-0.04f, 0.26f, 0.153f * sd), new(0.068f, 0.076f, 0.024f), muzzle.Darkened(0.4f), Mat.Skin, 0.015f, new(0, -18f * sd, 0));
            // the eyes, small and set under the brow
            s.Eye(hd, Head + new Vector3(0.15f, 0.085f, 0.1f * sd), 0.024f, new Color(0.12f, 0.06f, 0.02f), 1f);
            // (the tan above each eye)
            s.Egg(hd, Head + new Vector3(0.13f, 0.12f, 0.1f * sd), new(0.025f, 0.012f, 0.02f), muzzle, Mat.Fur, 0.01f);
        }
        DesignKit.Teeth(s, hd, Head + new Vector3(0.18f, -0.07f, 0.055f), Head + new Vector3(0.3f, -0.08f, 0.045f), Vector3.Down, 4, 0.02f, 0.007f, bone, 0.3f);
        DesignKit.Teeth(s, hd, Head + new Vector3(0.18f, -0.07f, -0.055f), Head + new Vector3(0.3f, -0.08f, -0.045f), Vector3.Down, 4, 0.02f, 0.007f, bone, 0.3f);
        DesignKit.Teeth(s, jw, Head + new Vector3(0.17f, -0.1f, 0.05f), Head + new Vector3(0.28f, -0.1f, 0.04f), Vector3.Up, 4, 0.018f, 0.007f, bone, 0.3f);
        DesignKit.Teeth(s, jw, Head + new Vector3(0.17f, -0.1f, -0.05f), Head + new Vector3(0.28f, -0.1f, -0.04f), Vector3.Up, 4, 0.018f, 0.007f, bone, 0.3f);

        MuscledLegs(s, 0.115f, 0.14f, fur, Mat.Fur, furDk, pad, 0.016f);
        Claws(s, 4, 0.1f, 0.02f, new Color(0.1f, 0.09f, 0.08f));
        s.Ball(s["tail0"], TailPts[0], 0.08f, fur, Mat.Fur, 0.05f, 0.012f);

        // the fur's colour: clumps of lighter and darker, a lighter back where the light catches it, darker legs and belly
        var noise = s.Noise;
        s.Paints.Add((p, c, mat) =>
        {
            if (mat != Mat.Fur) return c;
            float clump = noise.Fbm(p.X * 7f, p.Y * 7f, p.Z * 7f, 3);
            float top = Math.Clamp((p.Y - 0.1f) / 0.35f, 0f, 1f);
            float low = Math.Clamp((-0.2f - p.Y) / 0.5f, 0f, 1f);
            float k = 0.92f + 0.17f * clump + 0.18f * top - 0.2f * low;
            return new Color(c.R * k, c.G * k, c.B * k);
        });
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
            case "roar_windup":
                {
                    // it rises onto its hind legs and draws in a great breath: head thrown back, chest swelling, forepaws spread wide
                    float k = W3.Smooth01(t);
                    st.Pitch = 38f * k;
                    st.HR = st.HL = -st.Pitch + 8f; st.HRb = st.HLb = 14f * k;
                    st.FR = 70f * k; st.FRb = 40f * k; st.FL = 55f * k; st.FLb = 55f * k;
                    st.NeckUp = 26f * k; st.HeadUp = 34f * k + 3f * k * MathF.Sin(time * 9f); st.Jaw = 12f * k;
                    st.Arch -= 10f * k;
                    st.Root += new Vector3(-0.06f * k, 0.04f * k + 0.01f * MathF.Sin(time * 4f) * k, 0);
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
        if (a.InWater && c is "walk" or "idle" or "roar") st = Swim(st, c, t, time);
        else if (c == "idle" && (a.Owner as Enemy)?.Dead == false && speed < 0.2f)
        {
            // stunned bears sway their heads
            st.HeadYaw += 3f * MathF.Sin(time * 5f);
        }
        Apply(p, st);
    }

    /// <summary>
    /// In water: treading, head held up out of it, forepaws paddling in turn and hind legs
    /// kicking; its roar clip is a bite there (the head drawn back, jaws wide, then a lunge that
    /// snaps them shut at the clip's midpoint, when the bite lands).
    /// </summary>
    private static Stance Swim(Stance st, string c, float t, float time)
    {
        float stroke = time * (c == "walk" ? 6.5f : 4f);
        st.Pitch = 16f;
        st.FR = 45f + 50f * MathF.Sin(stroke); st.FRb = 35f + 30f * MathF.Max(0f, MathF.Cos(stroke));
        st.FL = 45f + 50f * MathF.Sin(stroke + MathF.PI); st.FLb = 35f + 30f * MathF.Max(0f, MathF.Cos(stroke + MathF.PI));
        st.HR = -st.Pitch + 28f * MathF.Sin(stroke + 1.6f); st.HRb = 25f;
        st.HL = -st.Pitch + 28f * MathF.Sin(stroke + 1.6f + MathF.PI); st.HLb = 25f;
        st.NeckUp = 14f; st.HeadUp = 12f; st.Jaw = 10f + 5f * MathF.Sin(time * 2f);
        st.Arch = 0f; st.TailUp = -10f;
        st.Root += new Vector3(0, 0.035f * MathF.Sin(stroke * 2f), 0);
        if (c == "roar")
        {
            // back (0..0.45), lunge and snap (0.5), hold, settle
            float back = Key(t, (0, 0), (0.42f, 1), (0.5f, 0), (1, 0));
            float lunge = Key(t, (0, 0), (0.45f, 0), (0.55f, 1), (0.75f, 1), (1, 0));
            st.NeckUp += 18f * back - 22f * lunge;
            st.HeadUp += 20f * back - 18f * lunge;
            st.Jaw = Key(t, (0, 10), (0.4f, 60), (0.5f, 62), (0.56f, 4), (1, 8));
            st.Root += new Vector3(-0.08f * back + 0.16f * lunge, 0, 0);
            st.Pitch += -6f * lunge;
        }
        return st;
    }
}
