using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A brimstone worm: a blind, pale worm as thick as a leg, banded in dirty yellow and ochre, its head a blunt bulb with no face but a
/// round maw ringed with lips and rows of hooked teeth turned inward. The sculpted model is the head and neck; the body is the same live
/// tube the eel's is, from the neck back into its hole.
/// </summary>
public sealed class WormDesign : EelDesign
{
    public override string Name => "worm";
    protected override Color SkinCol => new(0.5f, 0.42f, 0.2f);
    protected override Color BellyCol => new(0.66f, 0.58f, 0.3f);

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.1f, 0.1f, 0.05f), EyeEnergy = 0f,
        Glow = new Color(1f, 0.9f, 0.3f), GlowEnergy = 0.6f,
        Rim = new Color(0.9f, 0.85f, 0.45f), RimEnergy = 0.3f,
        DetailScale = 26f, DetailStrength = 1f, Veins = 0.5f, VeinColor = new Color(0.5f, 0.3f, 0.08f), Wet = 0.8f,
    };

    public override void Sculpt(Sculptor s)
    {
        var flesh = SkinCol;
        var belly = BellyCol;
        var band = new Color(0.4f, 0.24f, 0.07f);
        var gum = new Color(0.42f, 0.13f, 0.08f);
        var dark = new Color(0.06f, 0.02f, 0.01f);
        var tooth = new Color(0.78f, 0.74f, 0.58f);
        int neck = s.Bone("neck", -1, new(-NeckLen, 0, 0));
        int head = s.Bone("head", neck, new(-0.05f, 0, 0));
        int jaw = s.Bone("jaw", head, new(0.1f, -0.1f, 0));
        // the neck: the start of the body tube, banded in rings (the rest is drawn live)
        s.Limb(neck, new(-NeckLen - 0.05f, 0, 0), new(-0.05f, 0.01f, 0), R, R * 1.05f, flesh, Mat.Flesh, 0.04f, bump: 0.004f);
        for (int k = 0; k < 6; k++)
        {
            float x = -NeckLen + k * (NeckLen - 0.05f) / 5f;
            s.Egg(neck, new(x, 0, 0), new(0.034f, R * 1.12f, R * 1.12f), k % 2 == 0 ? band : flesh, Mat.Flesh, 0.02f);
        }
        // the head: a blunt bulb, wrinkled
        s.Egg(head, new(0.06f, 0f, 0), new(0.2f, 0.17f, 0.17f), flesh.Lightened(0.05f), Mat.Flesh, 0.05f, bump: 0.006f);
        s.Egg(head, new(-0.02f, 0.09f, 0), new(0.1f, 0.05f, 0.1f), band, Mat.Flesh, 0.03f);
        // the maw: a round hole facing forward, a dark throat behind it, a ring of fat lips
        s.CarveBall(head, new(0.26f, 0, 0), 0.105f, 0.025f);
        s.Ball(head, new(0.17f, 0, 0), 0.09f, dark, Mat.Flesh, 0.02f);
        for (int k = 0; k < 9; k++)
        {
            float a = k / 9f * Mathf.Tau;
            s.Egg(head, new(0.235f, 0.125f * MathF.Cos(a), 0.125f * MathF.Sin(a)), new(0.03f, 0.04f, 0.04f), gum.Lightened(0.06f * (k % 2)), Mat.Flesh, 0.03f);
        }
        // the lower lip, on the jaw (it drops to open the maw)
        s.Egg(jaw, new(0.15f, -0.02f, 0), new(0.075f, 0.035f, 0.11f), belly, Mat.Flesh, 0.02f);
        // teeth: two rings of hooks pointing in and back into the throat
        for (int ring = 0; ring < 2; ring++)
        {
            int n = ring == 0 ? 12 : 8;
            float rr = ring == 0 ? 0.1f : 0.07f, x = ring == 0 ? 0.25f : 0.2f;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.Tau + ring * 0.2f;
                var at = new Vector3(x, rr * MathF.Cos(a), rr * MathF.Sin(a));
                var tip = at + new Vector3(-0.045f, -0.06f * MathF.Cos(a), -0.06f * MathF.Sin(a));
                // (the lower ones are on the jaw so they part from the upper)
                bool low = MathF.Sin(a - Mathf.Pi * 0.5f) > 0.9f && MathF.Cos(a) < -0.3f;
                s.Horn(low ? jaw : head, at, tip, 0.011f, tooth, Mat.Bone, sides: 4, rings: 3);
            }
        }
        // blind pits where the eyes should be
        foreach (int sd in new[] { 1, -1 })
            s.Ball(head, new(0.1f, 0.1f, 0.12f * sd), 0.022f, dark, Mat.Flesh, 0.008f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "lurk";
        float t = a.T, time = a.Time;
        float jaw = 8f + 4f * MathF.Sin(time * 1.3f), sway = 4f * MathF.Sin(time * 1.1f);
        switch (c)
        {
            case "bite":
                // it draws its head back, the maw yawning, and strikes
                jaw = Key(t, (0, 10), (0.3f, 50), (0.5f, 60), (0.62f, 4), (1, 12));
                sway = Key(t, (0, 0), (0.28f, -20f), (0.5f, -20f), (0.62f, 14f), (1, 0));
                break;
            case "hold": jaw = 18f + 12f * MathF.Sin(time * 8f); sway = 8f * MathF.Sin(time * 6f); break;
            case "hurt": jaw = 36f; sway = 15f * Key(t, (0, 0), (0.25f, 1), (1, 0)); break;
            case "death": jaw = 40f * W3.Smooth01(t); sway = 20f * MathF.Sin(t * 12f) * (1 - t); break;
        }
        p.Set(_neck, 0, 0, sway);
        p.Set(_head, 0, 0, sway * 0.5f + jaw * 0.1f);
        p.Set(_jaw, 0, 0, -jaw);
    }
}

/// <summary>
/// A gasbag: a lumpy bladder of thin, yellowed skin full of glowing sulphur gas, veined in olive, a sad little face at the front and a
/// puckered valve underneath, a few thin tendrils hanging from its sides. It floats; before it vents it swells and the glow brightens.
/// </summary>
public sealed class GasbagDesign : CreatureDesign
{
    public override string Name => "gasbag";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 14f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.05f, 0.05f, 0.02f), EyeEnergy = 0f,
        Glow = new Color(0.95f, 0.95f, 0.3f), GlowEnergy = 1.1f,
        Rim = new Color(0.9f, 0.85f, 0.4f), RimEnergy = 0.35f,
        DetailScale = 24f, DetailStrength = 0.8f, Veins = 0.9f, VeinColor = new Color(0.4f, 0.42f, 0.1f), Wet = 0.5f,
        LightColor = new Color(0.95f, 0.95f, 0.3f), LightEnergy = 0.45f, LightRange = 2.6f, LightOffset = new Vector3(0, 0, 0),
    };

    private int _body, _bag, _valve;
    private readonly int[] _tend = new int[4];

    protected override void OnBonesBound()
    {
        _body = B("body"); _bag = B("bag"); _valve = B("valve");
        for (int k = 0; k < 4; k++) _tend[k] = B("tend" + k);
    }

    public override void Sculpt(Sculptor s)
    {
        var skin = new Color(0.5f, 0.48f, 0.18f);
        var vein = new Color(0.3f, 0.33f, 0.07f);
        var glow = new Color(0.9f, 0.9f, 0.3f);
        var dark = new Color(0.05f, 0.05f, 0.02f);
        int body = s.Bone("body", -1, new(0, 0, 0));
        int bag = s.Bone("bag", body, new(0, 0, 0));
        int valve = s.Bone("valve", bag, new(0, -0.3f, 0));
        // the bladder: a main lobe and lumps, an inner glow showing through
        s.Egg(bag, new(0, 0.04f, 0), new(0.3f, 0.34f, 0.3f), skin, Mat.Membrane, 0.05f, bump: 0.006f);
        s.Egg(bag, new(0.12f, 0.14f, 0.1f), new(0.17f, 0.17f, 0.16f), skin.Lightened(0.05f), Mat.Membrane, 0.05f, bump: 0.005f);
        s.Egg(bag, new(-0.12f, 0.06f, -0.12f), new(0.17f, 0.2f, 0.16f), skin.Darkened(0.04f), Mat.Membrane, 0.05f, bump: 0.005f);
        s.Egg(bag, new(-0.04f, -0.1f, 0.14f), new(0.16f, 0.15f, 0.15f), skin, Mat.Membrane, 0.05f, bump: 0.005f);
        s.Egg(bag, new(0, 0.02f, 0), new(0.2f, 0.24f, 0.2f), glow, Mat.Ember, 0.03f).Emit = 0.35f;
        // veins running over it
        var rng = s.Rng;
        for (int k = 0; k < 9; k++)
        {
            float a = k * 2.39996f, y = -0.2f + 0.45f * (float)rng.NextDouble();
            float r = 0.31f * MathF.Sqrt(Math.Max(0.1f, 1f - (y / 0.36f) * (y / 0.36f)));
            var at = new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
            var to = new Vector3(MathF.Cos(a + 0.5f) * r, y + 0.1f, MathF.Sin(a + 0.5f) * r);
            s.Limb(bag, at, to, 0.01f, 0.007f, vein, Mat.Flesh, 0.006f);
        }
        // a small sad face at the front: two beads of eyes and a downturned slit
        foreach (int sd in new[] { 1, -1 })
        {
            s.CarveBall(bag, new(0.285f, 0.1f, 0.095f * sd), 0.032f, 0.01f);
            s.Ball(bag, new(0.275f, 0.1f, 0.095f * sd), 0.022f, dark, Mat.Eye, 0.006f).Emit = 0f;
        }
        s.CarveLimb(bag, new(0.3f, 0.0f, -0.07f), new(0.3f, -0.02f, 0.07f), 0.01f, 0.01f, 0.006f);
        // the valve underneath: a puckered nozzle
        s.Limb(valve, new(0, -0.28f, 0), new(0, -0.4f, 0), 0.07f, 0.05f, vein, Mat.Flesh, 0.03f, bump: 0.004f);
        s.Egg(valve, new(0, -0.41f, 0), new(0.06f, 0.025f, 0.06f), vein.Darkened(0.2f), Mat.Flesh, 0.015f);
        s.CarveBall(valve, new(0, -0.43f, 0), 0.025f, 0.01f);
        // tendrils hanging from its sides
        for (int k = 0; k < 4; k++)
        {
            float z = k % 2 == 0 ? 1 : -1, x = k < 2 ? 0.1f : -0.12f;
            var root = new Vector3(x, -0.18f, 0.24f * z);
            int t0 = s.Bone("tend" + k, bag, root);
            var mid = root + new Vector3(0.02f * z, -0.18f, 0.05f * z);
            var tip = mid + new Vector3(0.0f, -0.2f, 0.04f * z);
            s.Limb(t0, root, mid, 0.022f, 0.016f, vein, Mat.Flesh, 0.012f);
            s.Limb(t0, mid, tip, 0.016f, 0.006f, vein.Darkened(0.1f), Mat.Flesh, 0.008f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "float";
        float t = a.T, time = a.Time;
        var gb = a.Owner as Gasbag;
        float swell = gb?.Swell ?? 0f, spent = gb?.Spent ?? 0f;
        float pulse = 1f + 0.035f * MathF.Sin(time * 2.2f);
        float scale = pulse * (1f + 0.5f * swell) * (1f - 0.18f * spent);
        p.Glow = 1f + 0.2f * MathF.Sin(time * 1.7f) + 2.4f * swell;
        p.Move(_body, new Vector3(0, 0.03f * MathF.Sin(time * 1.4f), 0));
        p.Set(_body, 3f * MathF.Sin(time * 0.9f), 0, 4f * MathF.Sin(time * 1.1f));
        p.Grow(_bag, new Vector3(scale, scale * (1f + 0.04f * MathF.Sin(time * 2.2f + 1f)), scale));
        // the valve shivers as it fills, then gapes
        p.Set(_valve, 0, 0, 10f * swell * MathF.Sin(time * 28f) + 25f * spent);
        for (int k = 0; k < 4; k++)
            p.Set(_tend[k], 6f * MathF.Sin(time * 1.6f + k), 0, 10f * MathF.Sin(time * 1.3f + k * 1.7f) + 20f * swell);
        if (c == "hurt") p.Grow(_bag, new Vector3(1f - 0.15f * Key(t, (0, 0), (0.2f, 1), (1, 0)), 1f + 0.1f * Key(t, (0, 0), (0.2f, 1), (1, 0)), 1f));
        if (c == "death")
        {
            // it slumps and wrinkles as the gas goes
            float k = W3.Smooth01(t);
            p.Grow(_bag, new Vector3(1f + 0.1f * k, 1f - 0.6f * k, 1f + 0.1f * k));
            p.Move(_body, new Vector3(0, -0.18f * k, 0));
            p.Glow = 1f - 0.9f * k;
        }
    }
}

/// <summary>
/// An acid newt: a flat-headed salamander in the mustard and black of the crust it lives on, bulging eyes on top of its head, a loose
/// throat sac that swells with a sickly green glow before it spits, acid warts along its back and a long tail.
/// </summary>
public sealed class NewtDesign : QuadrupedDesign
{
    public NewtDesign()
    {
        Q = new Spec
        {
            Floor = -0.3f, HipH = 0.15f, ShoulderH = 0.15f, BodyLen = 0.5f, NeckLen = 0.07f, HeadLen = 0.2f, NeckRise = -0.005f,
            TailLen = 0.9f, TailBones = 6, LegW = 0.13f, FUpper = 0.08f, FLower = 0.07f, HUpper = 0.08f, HLower = 0.07f, Paw = 0.08f,
        };
    }

    public override string Name => "newt";
    public override float Cell => 0.011f;
    public override float ThreeQuarter => 28f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.85f, 0.1f), EyeEnergy = 2.2f,
        Glow = new Color(0.7f, 1f, 0.2f), GlowEnergy = 1.5f,
        Rim = new Color(0.85f, 0.85f, 0.4f), RimEnergy = 0.25f,
        DetailScale = 36f, DetailStrength = 1f, Veins = 0.3f, VeinColor = new Color(0.25f, 0.2f, 0.03f), Wet = 0.6f,
    };

    public override void Sculpt(Sculptor s)
    {
        var skin = new Color(0.48f, 0.36f, 0.07f);
        var belly = new Color(0.62f, 0.54f, 0.22f);
        var blot = new Color(0.06f, 0.05f, 0.025f);
        var acid = new Color(0.55f, 0.8f, 0.12f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"], jw = s["jaw"];
        // the body: long and flat, paler underneath, blotched
        s.Egg(hp, Hips + new Vector3(0.02f, 0.0f, 0), new(0.16f, 0.1f, 0.13f), skin, Mat.Skin, 0.05f, bump: 0.004f);
        s.Egg(sp, Spine + new Vector3(0, 0.0f, 0), new(0.16f, 0.095f, 0.12f), skin, Mat.Skin, 0.05f, bump: 0.004f);
        s.Egg(ch, Chest + new Vector3(-0.02f, 0.0f, 0), new(0.13f, 0.1f, 0.13f), skin, Mat.Skin, 0.05f, bump: 0.004f);
        s.Egg(sp, Spine + new Vector3(0, -0.06f, 0), new(0.28f, 0.05f, 0.1f), belly, Mat.Skin, 0.04f);
        var rng = s.Rng;
        for (int k = 0; k < 10; k++)
        {
            var at = Hips.Lerp(Chest, k / 9f) + new Vector3(0, 0.085f, ((float)rng.NextDouble() - 0.5f) * 0.12f);
            s.Ball(k < 5 ? hp : ch, at, 0.022f + 0.012f * (float)rng.NextDouble(), blot, Mat.Skin, 0.012f);
        }
        // acid warts along the spine, glowing
        for (int k = 0; k < 6; k++)
        {
            var at = Hips.Lerp(Chest, k / 5f) + new Vector3(0, 0.1f, 0);
            s.Ball(k < 3 ? hp : ch, at, 0.016f, acid, Mat.Ember, 0.008f).Emit = 1f;
        }
        // the head: broad and flat, bulging eyes on top, a throat sac under the chin
        s.Limb(nk, Neck + new Vector3(-0.03f, 0, 0), Head, 0.08f, 0.075f, skin, Mat.Skin, 0.04f);
        s.Egg(hd, Head + new Vector3(0.05f, -0.01f, 0), new(0.13f, 0.055f, 0.1f), skin, Mat.Skin, 0.03f, bump: 0.003f);
        s.Egg(hd, Head + new Vector3(0.12f, -0.02f, 0), new(0.06f, 0.035f, 0.07f), skin, Mat.Skin, 0.02f);
        s.Egg(jw, Head + new Vector3(0.08f, -0.06f, 0), new(0.1f, 0.022f, 0.08f), belly, Mat.Skin, 0.02f);
        s.Egg(jw, Head + new Vector3(0.07f, -0.075f, 0), new(0.08f, 0.04f, 0.065f), acid, Mat.Ember, 0.03f).Emit = 0.5f;
        foreach (int sd in new[] { 1, -1 })
        {
            s.Ball(hd, Head + new Vector3(0.06f, 0.06f, 0.06f * sd), 0.034f, skin, Mat.Skin, 0.015f);
            s.Eye(hd, Head + new Vector3(0.075f, 0.075f, 0.065f * sd), 0.022f, new Color(0.8f, 0.6f, 0.05f), 1f);
            s.Ball(hd, Head + new Vector3(0.185f, -0.012f, 0.025f * sd), 0.008f, blot, Mat.Skin, 0.004f);
        }
        // the tail, flattened like an oar, tapering, blotched
        var tailBones = new int[Q.TailBones];
        for (int k = 0; k < Q.TailBones; k++) tailBones[k] = s["tail" + k];
        var radii = new float[Q.TailBones + 1];
        for (int k = 0; k <= Q.TailBones; k++) radii[k] = 0.075f * (1f - k / (float)Q.TailBones) + 0.006f;
        s.Chain(tailBones, TailPts, radii, skin, Mat.Skin, 0.03f);
        for (int k = 1; k < Q.TailBones; k++) s.Ball(tailBones[Math.Min(k, Q.TailBones - 1)], TailPts[k] + new Vector3(0, 0.03f, 0), 0.02f, blot, Mat.Skin, 0.01f);
        Legs(s, 0.032f, 0.036f, skin, Mat.Skin, belly);
        Claws(s, 3, 0.02f, 0.004f, blot);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        float ph = Phase(p, a, 0.9f);
        var st = Gait(ph, W3.SmoothStep(0.2f, 1.2f, speed), false, 26f);
        // a lizard's swing: the whole spine undulates and the tail whips the opposite way
        st.Arch = 10f * MathF.Sin(ph * Mathf.Tau) * W3.SmoothStep(0.2f, 1.2f, speed);
        st.TailSway += 24f * MathF.Sin(ph * Mathf.Tau + 1f) * W3.SmoothStep(0.2f, 1.2f, speed) + 6f * MathF.Sin(time * 1.4f);
        st.HeadYaw += 5f * MathF.Sin(time * 0.8f);
        p.Glow = 1f + 0.2f * MathF.Sin(time * 2f);
        switch (c)
        {
            case "spit_windup":
                {
                    float k = W3.Smooth01(t);
                    st.NeckUp = 16f * k; st.HeadUp = 14f * k; st.Jaw = 14f * k; st.Pitch = -5f * k; st.Root.Y -= 0.02f * k;
                    p.Glow = 1f + 2.6f * k;
                    break;
                }
            case "spit":
                {
                    float k = Key(t, (0, 0), (0.15f, 1), (1, 0));
                    st.NeckUp = -8f * k; st.HeadUp = 12f * k; st.Jaw = 48f * k; st.Pitch = 6f * k;
                    st.Root += new Vector3(-0.04f * k, 0, 0);
                    p.Glow = 1f + 1.2f * k;
                    break;
                }
            case "hurt": st.NeckUp -= 22f * Key(t, (0, 0), (0.25f, 1), (1, 0)); st.Jaw = 30f; break;
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.4f));
                    st = Dead(st, k, 0.06f);
                    Apply(p, st);
                    p.Glow = 1f - 0.8f * k;
                    return;
                }
        }
        Apply(p, st);
    }
}
