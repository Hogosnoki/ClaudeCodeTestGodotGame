using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A generator: a cairn of dark, rough stones heaped over a rift in the floor, a few skulls and long bones caught among them, and the rift's
/// violet glow showing through the gaps and pouring from a mouth at the front. It throbs while it works, and lurches when struck.
/// </summary>
public sealed class GeneratorDesign : CreatureDesign
{
    public override string Name => "generator";
    public override float Cell => 0.02f;
    public override float ThreeQuarter => 0f;
    public override float LifeScale => 0f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.75f, 0.45f, 1f), EyeEnergy = 3f,
        Glow = new Color(0.7f, 0.4f, 1f), GlowEnergy = 1.8f,
        Rim = new Color(0.6f, 0.5f, 0.8f), RimEnergy = 0.3f,
        DetailScale = 20f, DetailStrength = 1f,
        LightColor = new Color(0.65f, 0.4f, 1f), LightEnergy = 0.6f, LightRange = 3f,
    };

    private int _body;

    protected override void OnBonesBound() { _body = B("body"); }

    public override void Sculpt(Sculptor s)
    {
        var stone = new Color(0.27f, 0.25f, 0.29f);
        var stone2 = new Color(0.35f, 0.33f, 0.36f);
        var bone = new Color(0.82f, 0.78f, 0.66f);
        var dark = new Color(0.05f, 0.03f, 0.07f);
        int body = s.Bone("body", -1, Vector3.Zero);
        // the cairn: rough stones heaped in a low dome
        s.Egg(body, new(0f, -0.15f, 0f), new(1.0f, 0.75f, 0.85f), stone, Mat.Bone, 0.06f, bump: 0.03f);
        for (int k = 0; k < 11; k++)
        {
            float a = k * 2.399f, ring = k < 6 ? 0.8f : 0.45f, y = k < 6 ? -0.35f : 0.15f;
            var at = new Vector3(MathF.Cos(a) * ring, y + 0.08f * MathF.Sin(k * 1.7f), MathF.Sin(a) * ring * 0.85f);
            float r = 0.22f + 0.08f * ((k * 7) % 3);
            s.Egg(body, at, new(r * 1.2f, r * 0.9f, r), k % 2 == 0 ? stone2 : stone, Mat.Bone, 0.05f, new Vector3(k * 23f, k * 41f, 0), bump: 0.03f);
        }
        s.Egg(body, new(0.05f, 0.5f, 0f), new(0.3f, 0.25f, 0.28f), stone2, Mat.Bone, 0.05f, bump: 0.03f);
        // skulls and long bones caught among the stones
        s.Ball(body, new(-0.55f, -0.05f, 0.55f), 0.17f, bone, Mat.Bone, 0.03f);
        s.Eye(body, new(-0.6f, -0.03f, 0.7f), 0.045f, new Color(0.75f, 0.45f, 1f), 1.5f);
        s.Eye(body, new(-0.48f, -0.03f, 0.7f), 0.045f, new Color(0.75f, 0.45f, 1f), 1.5f);
        s.Ball(body, new(0.62f, -0.3f, 0.5f), 0.15f, bone, Mat.Bone, 0.03f);
        s.Limb(body, new(-0.2f, -0.5f, 0.75f), new(0.5f, -0.45f, 0.7f), 0.05f, 0.05f, bone, Mat.Bone, 0.02f, 0.01f);
        s.Limb(body, new(0.3f, 0.2f, 0.6f), new(0.7f, 0.0f, 0.3f), 0.045f, 0.045f, bone, Mat.Bone, 0.02f, 0.01f);
        // the rift's mouth at the front, dark, with the violet glow deep inside it
        s.Ball(body, new(0f, -0.35f, 0.72f), 0.3f, dark, Mat.Bone, 0.03f);
        s.CarveLimb(body, new(0f, -0.35f, 0.7f), new(0f, -0.35f, 1.1f), 0.24f, 0.24f, 0.02f);
        s.Eye(body, new(0f, -0.35f, 0.62f), 0.2f, new Color(0.7f, 0.4f, 1f), 1.6f);
        // the glow through the gaps at the top
        s.Eye(body, new(0f, 0.68f, 0.05f), 0.1f, new Color(0.75f, 0.5f, 1f), 1.2f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        // a slow throb while it works
        float throb = 1f + 0.025f * MathF.Sin(time * 3.2f);
        float roll = 0f, pitch = 0f, grow = 1f;
        switch (c)
        {
            case "hurt":
                {
                    float k = Key(t, (0, 0), (0.12f, 1), (1, 0));
                    roll = 8f * k * MathF.Sin(t * 45f); grow = 1f - 0.06f * k;
                    break;
                }
            case "death":
                {
                    float k = W3.Smooth01(t);
                    grow = 1f - 0.6f * k; pitch = 10f * k;
                    p.Root = new Vector3(0, -0.4f * k, 0);
                    break;
                }
        }
        p.Set(_body, roll, 0, pitch);
        p.Grow(_body, new Vector3(grow * throb, grow / throb, grow * throb));
    }
}
