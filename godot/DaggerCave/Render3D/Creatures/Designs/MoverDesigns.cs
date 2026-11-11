using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A mite: a midge the size of a thumb, a soft amber body that glows from within, a dark head with two feathery feelers, long dangling
/// legs and two glassy wings beating to a blur. A swarm of them reads as a cloud of sparks.
/// </summary>
public sealed class MiteDesign : CreatureDesign
{
    public override string Name => "mite";
    public override float Cell => 0.004f;
    public override float ThreeQuarter => 25f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.3f, 0.1f), EyeEnergy = 2f,
        Glow = new Color(1f, 0.8f, 0.3f), GlowEnergy = 2.5f,
        Rim = new Color(1f, 0.85f, 0.5f), RimEnergy = 0.4f,
        DetailScale = 80f, DetailStrength = 0.5f,
        LightColor = new Color(1f, 0.75f, 0.3f), LightEnergy = 0.25f, LightRange = 1.2f,
    };

    private int _body, _wingR, _wingL;

    protected override void OnBonesBound() { _body = B("body"); _wingR = B("wing_r"); _wingL = B("wing_l"); }

    public override void Sculpt(Sculptor s)
    {
        var amber = new Color(0.95f, 0.65f, 0.2f);
        var dark = new Color(0.12f, 0.08f, 0.06f);
        int body = s.Bone("body", -1, Vector3.Zero);
        // the abdomen: soft and glowing
        s.Egg(body, new(-0.05f, -0.005f, 0), new(0.06f, 0.035f, 0.035f), amber, Mat.Slime, 0.012f).Emit = 1.4f;
        // the thorax and head
        s.Egg(body, new(0.025f, 0.01f, 0), new(0.03f, 0.028f, 0.026f), dark, Mat.Chitin, 0.01f);
        s.Ball(body, new(0.06f, 0.012f, 0), 0.02f, dark, Mat.Chitin, 0.008f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(body, new(0.072f, 0.02f, 0.012f * sd), 0.008f, new Color(1f, 0.3f, 0.1f), 1.2f);
            s.Horn(body, new(0.075f, 0.025f, 0.006f * sd), new(0.11f, 0.06f, 0.02f * sd), 0.003f, dark, Mat.Chitin, sides: 4, rings: 3);
            // two long dangling legs a side
            for (int k = 0; k < 2; k++)
            {
                var hip = new Vector3(0.03f - k * 0.025f, -0.01f, 0.01f * sd);
                s.Limb(body, hip, hip + new Vector3(-0.01f - k * 0.02f, -0.07f, 0.02f * sd), 0.0035f, 0.002f, dark, Mat.Chitin, 0.002f);
            }
        }
        int wr = s.Bone("wing_r", body, new(0.02f, 0.035f, 0.01f));
        int wl = s.Bone("wing_l", body, new(0.02f, 0.035f, -0.01f));
        var glass = new Color(0.85f, 0.85f, 0.75f);
        s.Egg(wr, new(0.0f, 0.04f, 0.05f), new(0.03f, 0.003f, 0.055f), glass, Mat.Membrane, 0.004f);
        s.Egg(wl, new(0.0f, 0.04f, -0.05f), new(0.03f, 0.003f, 0.055f), glass, Mat.Membrane, 0.004f);
    }

    /// <summary>Nip: it rears back on its wings, then tips flat and darts.</summary>
    public override void Windup(CreaturePose p, in WindupPose w)
    {
        float c = w.Coil, s = w.Snap, q = w.Tremble, time = w.Time;
        p.Add(_body, 0, 0, 26f * c - 30f * s);
        p.Move(_body, new Vector3(-0.03f * c + 0.03f * s, 0.02f * c, 0));
        p.Add(_wingR, -18f * c, 0, 0); p.Add(_wingL, 18f * c, 0, 0);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        float time = a.Time;
        float pitch = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(a.Vel.Y, Math.Max(MathF.Abs(a.Vel.X), 0.5f))) * 0.4f, -30f, 30f);
        float roll = 0f;
        if (a.Clip == "dive") pitch = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(a.Vel.Y, Math.Max(MathF.Abs(a.Vel.X), 0.1f))), -80f, 80f);
        if (a.Clip == "death") { float k = W3.Smooth01(Math.Min(1f, a.T * 1.5f)); roll = 240f * k; pitch = -50f * k; }
        p.Root = new Vector3(0, 0.01f * MathF.Sin(time * 9f), 0);
        p.Set(_body, roll, 0, pitch);
        float beat = 50f * MathF.Sin(time * 110f);
        p.Set(_wingR, -beat, 10f, 0);
        p.Set(_wingL, beat, -10f, 0);
    }
}

/// <summary>
/// A delver: a blind digger the length of a hound. A low, heavy body in coarse grey-brown fur, a long naked pink snout ending in a hard
/// shovel of horn, great spade claws on its forelimbs, tiny hind legs, a stub of a tail, and pale nubs where eyes should be. It rears up
/// out of the ground, claws spread, and bites with a mouth full of chisel teeth.
/// </summary>
public sealed class DelverDesign : CreatureDesign
{
    public override string Name => "delver";
    public override float Cell => 0.009f;
    public override float ThreeQuarter => 25f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.9f, 0.85f, 0.75f), EyeEnergy = 0.4f,
        Rim = new Color(0.9f, 0.75f, 0.6f), RimEnergy = 0.3f,
        DetailScale = 40f, DetailStrength = 0.8f,
    };

    private int _body, _head, _jaw, _armR, _armL;

    protected override void OnBonesBound() { _body = B("body"); _head = B("head"); _jaw = B("jaw"); _armR = B("arm_r"); _armL = B("arm_l"); }

    public override void Sculpt(Sculptor s)
    {
        var fur = new Color(0.42f, 0.36f, 0.3f);
        var furDark = new Color(0.3f, 0.25f, 0.21f);
        var pink = new Color(0.85f, 0.55f, 0.52f);
        var horn = new Color(0.78f, 0.7f, 0.55f);
        int body = s.Bone("body", -1, Vector3.Zero);
        int head = s.Bone("head", body, new(0.22f, 0.04f, 0));
        int jaw = s.Bone("jaw", head, new(0.32f, -0.02f, 0));
        // the body: low and heavy, furred, a little humped over the shoulders
        s.Egg(body, new(0.0f, 0.0f, 0), new(0.26f, 0.15f, 0.15f), fur, Mat.Fur, 0.05f, bump: 0.01f);
        s.Egg(body, new(0.1f, 0.06f, 0), new(0.14f, 0.12f, 0.14f), furDark, Mat.Fur, 0.05f, bump: 0.01f);
        s.Horn(body, new(-0.24f, 0.0f, 0), new(-0.33f, 0.02f, 0), 0.03f, pink, Mat.Skin, Vector3.Up, 0.01f);
        // the head and its long naked snout, shod in horn
        s.Egg(head, new(0.27f, 0.04f, 0), new(0.1f, 0.08f, 0.085f), fur, Mat.Fur, 0.04f, bump: 0.008f);
        s.Limb(head, new(0.3f, 0.03f, 0), new(0.44f, 0.0f, 0), 0.045f, 0.03f, pink, Mat.Skin, 0.02f, 0.01f);
        s.Egg(head, new(0.46f, 0.0f, 0), new(0.035f, 0.03f, 0.045f), horn, Mat.Claw, 0.01f);
        foreach (int sd in new[] { 1, -1 })
            s.Eye(head, new(0.31f, 0.08f, 0.06f * sd), 0.012f, new Color(0.9f, 0.85f, 0.75f), 0.3f);
        // the jaw: chisel teeth
        s.Egg(jaw, new(0.37f, -0.04f, 0), new(0.07f, 0.025f, 0.05f), pink.Darkened(0.2f), Mat.Skin, 0.015f);
        foreach (int sd in new[] { 1, -1 })
            s.Horn(jaw, new(0.4f, -0.025f, 0.012f * sd), new(0.41f, 0.0f, 0.012f * sd), 0.009f, new Color(0.95f, 0.9f, 0.75f), Mat.Bone, sides: 4, rings: 2);
        // great spade claws in front, stubby legs behind
        foreach (var (name, sd) in new[] { ("arm_r", 1), ("arm_l", -1) })
        {
            int arm = s.Bone(name, body, new(0.15f, -0.04f, 0.11f * sd));
            var sh = new Vector3(0.15f, -0.04f, 0.11f * sd);
            var wr = sh + new Vector3(0.08f, -0.1f, 0.03f * sd);
            s.Limb(arm, sh, wr, 0.05f, 0.04f, fur, Mat.Fur, 0.02f);
            s.Egg(arm, wr + new Vector3(0.03f, -0.02f, 0), new(0.055f, 0.02f, 0.05f), pink, Mat.Skin, 0.015f);
            for (int k = -1; k <= 1; k++)
                s.Horn(arm, wr + new Vector3(0.06f, -0.02f, 0.02f * k), wr + new Vector3(0.12f, -0.05f, 0.025f * k), 0.012f, horn, Mat.Claw, Vector3.Down, 0.02f, 5, 3);
            var hip = new Vector3(-0.16f, -0.06f, 0.09f * sd);
            s.Limb(body, hip, hip + new Vector3(0.0f, -0.08f, 0.01f * sd), 0.035f, 0.025f, fur, Mat.Fur, 0.015f);
        }
    }

    /// <summary>Upheaval: it rears out of the rock, head thrown back, both digging claws lifted high; then drives them down into the ground.</summary>
    public override void Windup(CreaturePose p, in WindupPose w)
    {
        float c = w.Coil, s = w.Snap, q = w.Tremble, time = w.Time;
        p.Add(_body, 0, 0, 28f * c - 30f * s);
        p.Add(_head, 0, 0, 14f * c - 10f * s);
        p.Add(_jaw, 0, 0, -(26f * c + 8f * s));
        p.Add(_armR, 0, 0, 62f * c - 70f * s + 4f * q); p.Add(_armL, 0, 0, 56f * c - 66f * s - 4f * q);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float rear = 0f, jaw = 5f + 4f * MathF.Sin(time * 3f), claws = 10f * MathF.Sin(time * 2f), roll = 0f;
        switch (c)
        {
            case "erupt": { float k = Key(t, (0, 0), (0.3f, 1), (1, 0.4f)); rear = 45f * k; claws = 70f * k; jaw = 30f * k; break; }
            case "bite": { float k = Key(t, (0, 0), (0.25f, 1), (0.6f, 0.3f), (1, 0)); rear = -12f * k; jaw = 45f * k; claws = -20f * k; break; }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); rear = 15f * k; roll = 12f * k; break; }
            case "death": { float k = W3.Smooth01(t); roll = 90f * k; rear = -10f * k; break; }
        }
        p.Root = new Vector3(0, 0.01f * MathF.Sin(time * 4f), 0);
        p.Set(_body, roll, 0, rear + 2f * MathF.Sin(time * 4f));
        p.Set(_head, 0, 5f * MathF.Sin(time * 1.3f), -rear * 0.4f);
        p.Set(_jaw, 0, 0, -jaw);
        p.Set(_armR, 0, 0, claws);
        p.Set(_armL, 0, 0, claws * 0.9f);
    }
}

/// <summary>
/// A bomber: a bloated flying beetle, its wing-cases split and lifted over two broad brown wings, a leathery olive body, a small horned
/// head, and slung beneath it a pale, swollen sac that glows sickly green: the pod it lets fall.
/// </summary>
public sealed class BomberDesign : CreatureDesign
{
    public override string Name => "bomber";
    public override float Cell => 0.008f;
    public override float ThreeQuarter => 25f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.7f, 0.2f), EyeEnergy = 1.6f,
        Glow = new Color(0.75f, 0.95f, 0.3f), GlowEnergy = 2f,
        Rim = new Color(0.8f, 0.9f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 45f, DetailStrength = 0.7f,
        LightColor = new Color(0.7f, 0.9f, 0.3f), LightEnergy = 0.35f, LightRange = 1.8f,
    };

    private int _body, _sac, _caseR, _caseL, _wingR, _wingL;

    protected override void OnBonesBound()
    {
        _body = B("body"); _sac = B("sac"); _caseR = B("case_r"); _caseL = B("case_l"); _wingR = B("wing_r"); _wingL = B("wing_l");
    }

    public override void Sculpt(Sculptor s)
    {
        var olive = new Color(0.38f, 0.4f, 0.2f);
        var shell = new Color(0.25f, 0.22f, 0.12f);
        var pale = new Color(0.78f, 0.85f, 0.55f);
        int body = s.Bone("body", -1, Vector3.Zero);
        int sac = s.Bone("sac", body, new(0f, -0.1f, 0));
        s.Egg(body, new(0f, 0f, 0), new(0.22f, 0.14f, 0.16f), olive, Mat.Leather, 0.04f, bump: 0.006f);
        s.Egg(body, new(0.2f, 0.02f, 0), new(0.07f, 0.065f, 0.07f), shell, Mat.Chitin, 0.02f);
        s.Horn(body, new(0.25f, 0.06f, 0), new(0.32f, 0.12f, 0), 0.02f, shell, Mat.Claw, Vector3.Up, 0.02f, 5, 3);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(body, new(0.25f, 0.03f, 0.045f * sd), 0.016f, new Color(1f, 0.7f, 0.2f), 1.2f);
            for (int k = 0; k < 3; k++)
            {
                var hip = new Vector3(0.1f - k * 0.08f, -0.09f, 0.08f * sd);
                s.Limb(body, hip, hip + new Vector3(-0.02f, -0.08f, 0.04f * sd), 0.012f, 0.007f, shell, Mat.Chitin, 0.005f);
            }
        }
        // the pod sac slung beneath: pale, swollen, glowing within
        s.Egg(sac, new(0f, -0.13f, 0), new(0.1f, 0.09f, 0.1f), pale, Mat.Slime, 0.03f).Emit = 0.9f;
        // the wing-cases, lifted, and the wings under them
        int cr = s.Bone("case_r", body, new(0.05f, 0.1f, 0.06f));
        int cl = s.Bone("case_l", body, new(0.05f, 0.1f, -0.06f));
        s.Egg(cr, new(-0.04f, 0.13f, 0.09f), new(0.16f, 0.02f, 0.07f), shell, Mat.Chitin, 0.01f);
        s.Egg(cl, new(-0.04f, 0.13f, -0.09f), new(0.16f, 0.02f, 0.07f), shell, Mat.Chitin, 0.01f);
        int wr = s.Bone("wing_r", body, new(0.02f, 0.11f, 0.05f));
        int wl = s.Bone("wing_l", body, new(0.02f, 0.11f, -0.05f));
        var wing = new Color(0.55f, 0.42f, 0.28f);
        s.Egg(wr, new(-0.02f, 0.12f, 0.2f), new(0.08f, 0.006f, 0.18f), wing, Mat.Membrane, 0.006f);
        s.Egg(wl, new(-0.02f, 0.12f, -0.2f), new(0.08f, 0.006f, 0.18f), wing, Mat.Membrane, 0.006f);
    }

    /// <summary>Kamikaze: its wing cases gape and the sac of fire under them swells, throbbing faster; it tips over and comes.</summary>
    public override void Windup(CreaturePose p, in WindupPose w)
    {
        float c = w.Coil, s = w.Snap, q = w.Tremble, time = w.Time;
        float throb = 0.5f + 0.5f * MathF.Sin(time * (10f + 26f * c));
        p.Grow(_sac, 1f + (0.25f + 0.2f * throb) * c + 0.2f * s);
        p.Add(_caseR, -32f * c, 0, 0); p.Add(_caseL, 32f * c, 0, 0);
        p.Add(_body, 0, 0, 12f * c - 20f * s);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "fly";
        float t = a.T, time = a.Time;
        float swell = 1f + 0.06f * MathF.Sin(time * 4f), roll = 0f, pitch = Math.Clamp(a.Vel.X * 0.05f, -12f, 12f);
        if (c == "drop") { float k = Key(t, (0, 0), (0.3f, 1), (1, 0)); swell = 1f - 0.25f * k; pitch -= 10f * k; }
        if (c == "hurt") { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); roll = 18f * k; }
        if (c == "death") { float k = W3.Smooth01(t); roll = 180f * k; pitch = -30f * k; }
        p.Root = new Vector3(0, 0.03f * MathF.Sin(time * 3f), 0);
        p.Set(_body, roll, 0, pitch);
        if (_sac >= 0) p.Scale[_sac] = Vector3.One * swell;
        float beat = 40f * MathF.Sin(time * 60f);
        p.Set(_wingR, -beat, 0, 0);
        p.Set(_wingL, beat, 0, 0);
        p.Set(_caseR, -25f, 0, 0);
        p.Set(_caseL, 25f, 0, 0);
    }
}
