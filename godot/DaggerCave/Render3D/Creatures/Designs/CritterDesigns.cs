using System;
using Godot;

namespace DaggerCave;

/// <summary>A pale moth whose wings glow a soft green-white, fluttering around a spot.</summary>
public sealed class MothDesign : CreatureDesign
{
    public override string Name => "moth";
    public override float Cell => 0.006f;
    public override float ThreeQuarter => 35f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.4f, 1f, 0.9f), EyeEnergy = 2f,
        Glow = new Color(0.6f, 1f, 0.85f), GlowEnergy = 2.2f,
        Rim = new Color(0.7f, 1f, 0.9f), RimEnergy = 0.4f,
        DetailScale = 90f, DetailStrength = 0.4f,
    };

    private int _body;
    private readonly int[] _wing = new int[4];

    protected override void OnBonesBound()
    {
        _body = B("body");
        string[] w = { "fw_r", "hw_r", "fw_l", "hw_l" };
        for (int k = 0; k < 4; k++) _wing[k] = B(w[k]);
    }

    public override void Sculpt(Sculptor s)
    {
        var fuzz = new Color(0.75f, 0.78f, 0.7f);
        var wing = new Color(0.72f, 0.85f, 0.78f);
        int body = s.Bone("body", -1, Vector3.Zero);
        s.Egg(body, new(0.02f, 0, 0), new(0.025f, 0.02f, 0.02f), fuzz, Mat.Fur, 0.008f);
        s.Limb(body, new(0.0f, 0, 0), new(-0.07f, -0.01f, 0), 0.016f, 0.008f, fuzz.Darkened(0.2f), Mat.Fur, 0.008f);
        s.Ball(body, new(0.048f, 0.004f, 0), 0.013f, fuzz, Mat.Fur, 0.006f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(body, new(0.055f, 0.008f, 0.009f * sd), 0.006f, new Color(0.2f, 0.4f, 0.35f), 1f);
            s.Horn(body, new(0.055f, 0.016f, 0.005f * sd), new(0.1f, 0.05f, 0.03f * sd), 0.002f, fuzz, Mat.Fur, Vector3.Down, 0.01f, 3, 3);
        }
        string[] names = { "fw_r", "hw_r", "fw_l", "hw_l" };
        for (int k = 0; k < 4; k++)
        {
            bool fore = k % 2 == 0;
            float z = k < 2 ? 1 : -1;
            var root = new Vector3(fore ? 0.02f : -0.005f, 0.012f, 0.012f * z);
            int b = s.Bone(names[k], body, root);
            var tip = root + (fore ? new Vector3(0.03f, 0.0f, 0.13f * z) : new Vector3(-0.05f, 0, 0.09f * z));
            var back = root + (fore ? new Vector3(-0.05f, 0, 0.1f * z) : new Vector3(-0.07f, 0, 0.04f * z));
            s.Sheet(new[] { root, tip, back }, new[] { b, b, b }, new[] { (0, 1, 2) }, 3, wing, 0.0015f, Mat.Membrane, new[] { (1, 2) }, fore ? -0.012f : -0.008f);
        }
        // the wing sheets glow
        foreach (var part in s.Parts) if (part.Mat == Mat.Membrane) part.Emit = 0.9f;
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        float t = a.T, time = a.Time;
        float beat = MathF.Sin(t * Mathf.Tau);
        p.Root = new Vector3(0, 0.01f * beat, 0);
        p.Set(_body, 0, 0, 10f + 6f * MathF.Sin(time * 2f));
        for (int k = 0; k < 4; k++)
        {
            float z = k < 2 ? 1 : -1;
            float ang = 20f + 60f * beat * (k % 2 == 0 ? 1f : 0.85f);
            p.Set(_wing[k], -ang * z, 0, 0);
        }
    }
}

/// <summary>A little cave crab with a lumpy rust-coloured shell, stalked eyes and one big claw.</summary>
public sealed class CrabDesign : CreatureDesign
{
    public override string Name => "crab";
    public override float Cell => 0.007f;
    // always faces the viewer and scuttles sideways
    public override float ThreeQuarter => 90f;
    public override float FloorY => Floor;
    private const float Floor = -0.3125f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.1f, 0.1f, 0.1f), EyeEnergy = 0f,
        Rim = new Color(1f, 0.7f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 60f, DetailStrength = 0.8f, Wet = 0.5f,
    };

    private int _body, _clawR, _clawL, _eyes;
    private readonly DesignKit.Leg[] _legs = new DesignKit.Leg[6];

    protected override void OnBonesBound() { _body = B("body"); _clawR = B("claw_r"); _clawL = B("claw_l"); _eyes = B("eyes"); }

    public override void Sculpt(Sculptor s)
    {
        var shell = new Color(0.5f, 0.22f, 0.12f);
        var pale = new Color(0.75f, 0.55f, 0.4f);
        int body = s.Bone("body", -1, new(0, -0.22f, 0));
        int eyes = s.Bone("eyes", body, new(0.06f, -0.2f, 0));
        s.Egg(body, new(0, -0.2f, 0), new(0.09f, 0.045f, 0.12f), shell, Mat.Chitin, 0.015f, bump: 0.004f);
        s.Egg(body, new(0.0f, -0.23f, 0), new(0.08f, 0.03f, 0.1f), pale, Mat.Chitin, 0.015f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Limb(eyes, new(0.07f, -0.2f, 0.025f * sd), new(0.085f, -0.15f, 0.03f * sd), 0.006f, 0.005f, pale, Mat.Chitin, 0.004f);
            s.Ball(eyes, new(0.087f, -0.145f, 0.03f * sd), 0.011f, new Color(0.03f, 0.03f, 0.03f), Mat.Eye, 0.003f);
            // claws: the right one big
            float big = sd > 0 ? 1.5f : 1f;
            var sh = new Vector3(0.07f, -0.22f, 0.07f * sd);
            int claw = s.Bone(sd > 0 ? "claw_r" : "claw_l", body, sh);
            s.Limb(claw, sh, sh + new Vector3(0.05f, -0.01f, 0.03f * sd), 0.012f, 0.012f, shell, Mat.Chitin, 0.005f);
            var hand = sh + new Vector3(0.08f, 0.0f, 0.035f * sd);
            s.Egg(claw, hand, new Vector3(0.03f, 0.02f, 0.02f) * big, shell, Mat.Chitin, 0.006f);
            s.Horn(claw, hand + new Vector3(0.02f, 0.006f, 0) * big, hand + new Vector3(0.055f, 0.0f, -0.005f * sd) * big, 0.008f * big, shell.Darkened(0.3f), Mat.Claw, Vector3.Down, 0.004f, 5, 3);
            s.Horn(claw, hand + new Vector3(0.02f, -0.008f, 0) * big, hand + new Vector3(0.05f, -0.012f, -0.005f * sd) * big, 0.006f * big, shell.Darkened(0.3f), Mat.Claw, Vector3.Up, 0.003f, 5, 3);
        }
        float[] xs = { 0.03f, -0.01f, -0.05f };
        for (int k = 0; k < 3; k++)
            for (int sd = 0; sd < 2; sd++)
            {
                int side = sd == 0 ? 1 : -1;
                _legs[k * 2 + sd] = DesignKit.ArthroLeg(s, $"leg{k}{(side > 0 ? "r" : "l")}", body, new Vector3(xs[k], -0.22f, 0.09f * side), -10f - k * 15f, side, 0.16f, 0.06f, Floor,
                    0.01f, shell, Mat.Chitin);
                _legs[k * 2 + sd].Phase = ((k + sd) % 2) * 0.5f;
            }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        bool run = c == "scuttle";
        float hide = c == "hide" ? W3.Smooth01(t * 1.5f) : 0f;
        foreach (var leg in _legs)
        {
            // scuttling sideways: the legs swing across the body line
            DesignKit.Step(p, leg, (run ? t * 2f : time * 0.2f) + leg.Phase, run ? 25f : 0f, run ? 30f : 0f, 0f);
            if (hide > 0) DesignKit.Curl(p, leg, hide * 0.9f);
        }
        p.Move(_body, new Vector3(0, -0.035f * hide + (run ? 0.006f * MathF.Abs(MathF.Sin(t * MathF.PI * 4f)) : 0f), 0));
        float snip = Math.Max(0, MathF.Sin(time * 2.3f)) * 20f;
        p.Set(_clawR, 0, -20f * hide, 10f * MathF.Sin(time * 1.7f) - 30f * hide);
        p.Set(_clawL, 0, 20f * hide, snip * 0.5f - 30f * hide);
        p.Set(_eyes, 0, 0, -60f * hide + 5f * MathF.Sin(time * 3f));
    }
}
