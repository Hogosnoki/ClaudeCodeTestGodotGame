using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A sea urchin the size of a boulder: a bruised, knobbed shell pulsing with violet lights,
/// wreathed in long black spines with pale venom tips. It draws its spines in (the tell), then
/// bursts them out twice as long, in one sweep that rakes everything around it.
/// </summary>
public sealed class UrchinDesign : CreatureDesign
{
    public override string Name => "urchin";
    public override float Cell => 0.016f;
    public override float ThreeQuarter => 0f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.8f, 0.4f, 1f), EyeEnergy = 3f,
        Glow = new Color(0.65f, 0.3f, 1f), GlowEnergy = 3f,
        Rim = new Color(0.7f, 0.5f, 0.9f), RimEnergy = 0.3f,
        DetailScale = 40f, DetailStrength = 0.8f, Wet = 0.8f,
        LightColor = new Color(0.6f, 0.3f, 1f), LightEnergy = 0.5f, LightRange = 2.5f,
    };

    private const float Shell = 0.5f;
    private int _body, _spines, _inner;

    protected override void OnBonesBound() { _body = B("body"); _spines = B("spines"); _inner = B("inner"); }

    public override void Sculpt(Sculptor s)
    {
        var shell = new Color(0.2f, 0.1f, 0.18f);
        var knob = new Color(0.38f, 0.22f, 0.3f);
        var spine = new Color(0.07f, 0.05f, 0.07f);
        var tip = new Color(0.85f, 0.8f, 0.7f);
        int body = s.Bone("body", -1, Vector3.Zero);
        int spines = s.Bone("spines", body, Vector3.Zero);
        int inner = s.Bone("inner", body, Vector3.Zero);

        // the shell: a flattened globe with five meridian grooves and rows of knobs
        s.Egg(body, Vector3.Zero, new(Shell, Shell * 0.86f, Shell), shell, Mat.Chitin, 0.03f, bump: 0.01f);
        for (int m = 0; m < 5; m++)
        {
            float a = m / 5f * Mathf.Tau;
            var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            s.CarveLimb(body, d * Shell * 0.2f + Vector3.Up * Shell * 0.9f, d * Shell * 1.02f, 0.03f, 0.03f, 0.02f);
        }
        // spines: a Fibonacci sphere of long black needles rooted deep in the shell (on their own
        // bone, which scales from the centre so they retract into the shell and burst out)
        var rng = s.Rng;
        const int n = 64;
        for (int k = 0; k < n; k++)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float r = MathF.Sqrt(1f - y * y);
            float phi = k * 2.39996f;
            var d = new Vector3(MathF.Cos(phi) * r, y * 0.9f, MathF.Sin(phi) * r).Normalized();
            if (d.Y < -0.75f) continue; // the underside rests on the rock
            var root = d * Shell * 0.3f;
            float len = Shell * (0.95f + 0.25f * (float)rng.NextDouble());
            var end = d * (Shell + len);
            s.Horn(spines, root, end, 0.03f, spine, Mat.Claw, sides: 5, rings: 4);
            s.Horn(spines, end - d * 0.08f, end + d * 0.005f, 0.008f, tip, Mat.Claw, sides: 4, rings: 2, emit: 0.35f);
            // a knob at every spine's base
            s.Ball(body, d * Shell * 0.9f, 0.045f, knob, Mat.Chitin, 0.02f);
        }
        // violet photophores between the spines, on a bone that pulses
        for (int k = 0; k < 18; k++)
        {
            float a = k * 2.1f, y = -0.3f + 0.9f * ((k * 7) % 18) / 18f;
            var d = new Vector3(MathF.Cos(a) * MathF.Sqrt(1 - y * y), y, MathF.Sin(a) * MathF.Sqrt(1 - y * y)).Normalized();
            float t = 1f / MathF.Sqrt(d.X * d.X / (Shell * Shell) + d.Y * d.Y / (Shell * Shell * 0.74f) + d.Z * d.Z / (Shell * Shell));
            s.Eye(inner, d * t, 0.024f, new Color(0.5f, 0.25f, 0.7f), 1f);
        }
    }

    /// <summary>Spine length in gameplay pixels for the 3 s pulse cycle (the same curve the damage uses).</summary>
    private static float SpikeLen(float c)
    {
        if (c < 1.6f) return 7;
        if (c < 2.1f) return 7 - (c - 1.6f) * 8;
        if (c < 2.25f) return 3 + (c - 2.1f) / 0.15f * 17;
        if (c < 2.6f) return 20;
        return 20 - (c - 2.6f) / 0.4f * 13;
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "pulse";
        float time = a.Time;
        float len = 7f;
        if (c == "pulse") len = SpikeLen(a.T * 3f);
        // shell radius 10 px; spine tips reach radius + length (their roots stay buried)
        float rest = 10f + 7f;
        float grow = (10f + len) / rest;
        float quiver = len < 7f ? 0.02f * MathF.Sin(time * 60f) : 0f; // tensing before the burst
        float breathe = 1f + 0.03f * MathF.Sin(time * 1.7f);
        switch (c)
        {
            case "hurt": grow *= 1f - 0.2f * Key(a.T, (0, 0), (0.2f, 1), (1, 0)); break;
            case "death":
                {
                    float k = W3.Smooth01(a.T);
                    grow = Mathf.Lerp(grow, 0.55f, k); breathe = 1f - 0.12f * k;
                    p.Set(_body, 0, 0, 25f * k);
                    p.Root = new Vector3(0, -0.15f * k, 0);
                    break;
                }
        }
        p.Grow(_body, new Vector3(breathe, 1f / breathe, breathe));
        p.Grow(_spines, grow + quiver);
        p.Set(_spines, 0, 4f * MathF.Sin(time * 0.6f), 0);
        // the lights swell a touch as it tenses
        p.Grow(_inner, len < 7f ? 1.03f : 1f);
    }
}
