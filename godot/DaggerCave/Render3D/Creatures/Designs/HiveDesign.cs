using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A hornets' nest as big as a boulder: a teardrop of grey and tan paper, laid on in scalloped bands, hung from the ceiling on a thick papery stalk,
/// with a dark mouth at the bottom where the hornets come and go (a glow of amber inside). It hums, and shudders when it is struck.
/// </summary>
public sealed class HiveDesign : CreatureDesign
{
    public override string Name => "hive";
    public override float Cell => 0.02f;
    public override float ThreeQuarter => 0f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.7f, 0.15f), EyeEnergy = 2.5f,
        Glow = new Color(1f, 0.75f, 0.25f), GlowEnergy = 1.5f,
        Rim = new Color(0.9f, 0.8f, 0.55f), RimEnergy = 0.25f,
        DetailScale = 22f, DetailStrength = 0.9f,
        LightColor = new Color(1f, 0.7f, 0.2f), LightEnergy = 0.35f, LightRange = 2.5f,
    };

    private int _body;

    protected override void OnBonesBound() { _body = B("body"); }

    public override void Sculpt(Sculptor s)
    {
        var tan = new Color(0.62f, 0.55f, 0.4f);
        var grey = new Color(0.46f, 0.43f, 0.38f);
        var pale = new Color(0.72f, 0.66f, 0.5f);
        var dark = new Color(0.07f, 0.045f, 0.03f);
        int body = s.Bone("body", -1, Vector3.Zero);

        // the bulb: an egg, fat toward the top, with scalloped bands of paper laid round it
        s.Egg(body, new(0f, 0.02f, 0f), new(0.9f, 1.0f, 0.9f), grey, Mat.Bone, 0.05f, bump: 0.015f);
        float[] ys = { 0.62f, 0.38f, 0.14f, -0.1f, -0.34f, -0.58f };
        for (int k = 0; k < ys.Length; k++)
        {
            float y = ys[k];
            // (the egg's half-width at this height)
            float t = Math.Clamp(y / 1.0f, -1f, 1f);
            float w = 0.9f * MathF.Sqrt(Math.Max(0.05f, 1f - t * t));
            s.Ring(body, new(0f, y, 0f), w * 0.98f, w * 0.98f, 0.075f, k % 2 == 0 ? tan : pale, Mat.Bone, 0.03f, 0f, 0.01f);
        }
        // a few larger plates of paper, patched on
        for (int k = 0; k < 7; k++)
        {
            float a = k * 0.9f, y = -0.45f + 0.9f * ((k * 5) % 7) / 7f;
            float rr = 0.9f * MathF.Sqrt(Math.Max(0.1f, 1f - y * y)) * 0.96f;
            s.Egg(body, new(MathF.Cos(a) * rr, y, MathF.Sin(a) * rr), new(0.26f, 0.16f, 0.26f), k % 2 == 0 ? tan : grey, Mat.Bone, 0.03f, new Vector3(0, -a * 57.3f, 0), bump: 0.01f);
        }
        // the mouth: a round hollow in the underside, dark inside, with an amber glow deep in it
        s.Ball(body, new(0f, -0.72f, 0f), 0.4f, dark, Mat.Bone, 0.03f);
        s.CarveLimb(body, new(0f, -0.82f, 0f), new(0f, -1.15f, 0f), 0.3f, 0.3f, 0.02f);
        s.Eye(body, new(0f, -0.66f, 0.05f), 0.16f, new Color(1f, 0.65f, 0.15f), 1.2f);
        s.Ring(body, new(0f, -0.9f, 0f), 0.32f, 0.32f, 0.05f, tan.Darkened(0.2f), Mat.Bone, 0.02f, 0f, 0.01f);
        // the stalk it hangs by
        s.Limb(body, new(0f, 0.85f, 0f), new(0.05f, 1.35f, 0f), 0.2f, 0.28f, tan.Darkened(0.25f), Mat.Bone, 0.05f, 0.01f);
        s.Ball(body, new(0.05f, 1.36f, 0f), 0.3f, grey.Darkened(0.2f), Mat.Bone, 0.05f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        // a hum: a tremble you can only just see, and a slow sway on the stalk
        float sway = 1.8f * MathF.Sin(time * 1.1f);
        float tremble = 0.006f * MathF.Sin(time * 47f);
        float roll = 0f, pitch = 0f, grow = 1f;
        switch (c)
        {
            case "hurt":
                {
                    float k = Key(t, (0, 0), (0.15f, 1), (1, 0));
                    roll = 14f * k * MathF.Sin(t * 40f); pitch = 6f * k; grow = 1f - 0.05f * k;
                    break;
                }
            case "death":
                {
                    float k = W3.Smooth01(t);
                    roll = 50f * k; pitch = 25f * k; grow = 1f - 0.35f * k;
                    p.Root = new Vector3(0, -0.5f * k * k, 0);
                    break;
                }
        }
        p.Set(_body, roll, 0, sway + pitch);
        p.Grow(_body, new Vector3(grow + tremble, grow - tremble, grow + tremble));
    }
}
