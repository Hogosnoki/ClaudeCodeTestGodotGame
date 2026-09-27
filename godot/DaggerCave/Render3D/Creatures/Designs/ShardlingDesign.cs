using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A shardling: a crawling isopod the size of a dog, plated in dark teal chitin, its back grown
/// over with glassy ice-blue crystals that glow from within; a cluster of magenta eyes, working
/// mandibles, six short legs. It curls into a ball with its crystals bristling, then bursts them
/// outward like thrown knives.
/// </summary>
public sealed class ShardlingDesign : CreatureDesign
{
    public override string Name => "shardling";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 26f;
    public override float FloorY => Floor;
    private const float Floor = -0.5f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.25f, 0.85f), EyeEnergy = 3.5f,
        Glow = new Color(0.45f, 0.95f, 1f), GlowEnergy = 1.2f,
        Rim = new Color(0.5f, 0.9f, 1f), RimEnergy = 0.35f,
        DetailScale = 36f, DetailStrength = 0.8f, Wet = 0.3f,
        LightColor = new Color(0.4f, 0.9f, 1f), LightEnergy = 0.5f, LightRange = 2.2f, LightOffset = new Vector3(-0.05f, 0.1f, 0),
    };

    private int _body, _head, _tail, _crystals, _mandR, _mandL;
    private readonly DesignKit.Leg[] _legs = new DesignKit.Leg[6];

    protected override void OnBonesBound()
    {
        _body = B("body"); _head = B("head"); _tail = B("tail"); _crystals = B("crystals"); _mandR = B("mand_r"); _mandL = B("mand_l");
    }

    public override void Sculpt(Sculptor s)
    {
        var plate = new Color(0.08f, 0.2f, 0.22f);
        var plateHi = new Color(0.14f, 0.32f, 0.34f);
        var belly = new Color(0.2f, 0.26f, 0.26f);
        var ice = new Color(0.62f, 0.9f, 1f);
        int body = s.Bone("body", -1, new(0, -0.3f, 0));
        int head = s.Bone("head", body, new(0.24f, -0.3f, 0));
        int tail = s.Bone("tail", body, new(-0.2f, -0.28f, 0));
        int crystals = s.Bone("crystals", body, new(-0.02f, -0.18f, 0));

        // segmented plates, domed, overlapping front to back
        for (int k = 0; k < 7; k++)
        {
            float x = 0.2f - k * 0.07f;
            int b = k < 5 ? body : tail;
            float w = 0.19f - 0.012f * Math.Abs(k - 2.5f);
            s.Egg(b, new(x, -0.27f, 0), new(0.055f, 0.12f - 0.005f * Math.Abs(k - 2.5f), w), k % 2 == 0 ? plate : plateHi, Mat.Chitin, 0.012f, bump: 0.003f);
        }
        s.Egg(body, new(0, -0.35f, 0), new(0.26f, 0.07f, 0.17f), belly, Mat.Chitin, 0.04f);
        s.Egg(tail, new(-0.3f, -0.3f, 0), new(0.07f, 0.06f, 0.1f), plate, Mat.Chitin, 0.03f);
        // head: a blunt shield, a crown of magenta eyes, mandibles
        s.Egg(head, new(0.28f, -0.3f, 0), new(0.08f, 0.08f, 0.13f), plateHi, Mat.Chitin, 0.03f, bump: 0.003f);
        for (int e = 0; e < 5; e++)
        {
            float z = (e - 2) * 0.035f;
            s.Eye(head, new(0.345f - Math.Abs(e - 2) * 0.012f, -0.26f + 0.01f * (e % 2), z), e == 2 ? 0.02f : 0.014f, new Color(0.5f, 0.05f, 0.4f), 1f);
        }
        int mr = s.Bone("mand_r", head, new(0.34f, -0.34f, 0.04f));
        int ml = s.Bone("mand_l", head, new(0.34f, -0.34f, -0.04f));
        foreach (var (b, z) in new[] { (mr, 1f), (ml, -1f) })
            s.Horn(b, new(0.34f, -0.34f, 0.045f * z), new(0.41f, -0.37f, 0.0f), 0.016f, plate.Darkened(0.3f), Mat.Claw, new Vector3(0, 0, z), 0.015f, 5, 4);

        // the crystal crop: big shards along the spine, smaller ones down the flanks
        var mb = new MeshBuilder();
        var rng = s.Rng;
        for (int k = 0; k < 16; k++)
        {
            float u = (float)rng.NextDouble();
            float x = 0.16f - u * 0.4f;
            float zz = ((float)rng.NextDouble() - 0.5f) * 0.3f;
            float y = -0.2f + 0.05f * (1f - MathF.Abs(zz) * 3f);
            var dir = new Vector3(((float)rng.NextDouble() - 0.6f) * 0.5f, 1f, zz * 2.5f).Normalized();
            float len = 0.12f + 0.16f * (1f - MathF.Abs(zz) * 2.5f) * (float)rng.NextDouble() + 0.04f;
            DesignKit.CrystalAt(mb, new Vector3(x, y - 0.03f, zz), dir, 0.022f + 0.018f * (float)rng.NextDouble(), len, ice.Lerp(new Color(0.8f, 0.95f, 1f), (float)rng.NextDouble() * 0.5f), (float)rng.NextDouble());
        }
        s.Rigid(crystals, mb, Mat.Crystal, 0.3f);

        // six short legs under the plates
        float[] xs = { 0.15f, 0.02f, -0.12f };
        float[] yaws = { 35f, 0f, -35f };
        for (int k = 0; k < 3; k++)
            for (int sd = 0; sd < 2; sd++)
            {
                int side = sd == 0 ? 1 : -1;
                _legs[k * 2 + sd] = DesignKit.ArthroLeg(s, $"leg{k}{(side > 0 ? "r" : "l")}", body, new Vector3(xs[k], -0.36f, 0.12f * side), yaws[k], side, 0.3f, 0.08f, Floor,
                    0.026f, plate, Mat.Chitin, tipCol: ice.Darkened(0.4f));
                _legs[k * 2 + sd].Phase = ((k + sd) % 2) * 0.5f;
            }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        bool walk = c == "walk";
        float gait = walk ? t * 2f : time * 0.3f;
        float curl = 0f, grow = 1f, legCurl = 0f, mand = 10f * MathF.Sin(time * 6f), roll = 0f, pitch = 0f;
        p.Glow = 1f + 0.25f * MathF.Sin(time * 2.2f);
        switch (c)
        {
            case "curl":
                {
                    float k = W3.Smooth01(t);
                    curl = k; grow = 1f + 0.45f * k; legCurl = 0.9f * k;
                    p.Glow = 1f + 2.5f * k + 0.5f * MathF.Sin(time * 40f) * k;
                    break;
                }
            case "burst":
                {
                    // the crystals fly off (they are gameplay shards now) and grow back
                    float k = Key(t, (0, 1), (0.12f, 1.3f), (0.25f, 0.2f), (1, 0.9f));
                    curl = 1f - W3.Smooth01(t); grow = k; legCurl = curl * 0.9f;
                    p.Glow = 3.5f - 2.5f * t;
                    break;
                }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); curl = 0.35f * k; pitch = -10f * k; break; }
            case "death":
                {
                    float k = W3.Smooth01(t);
                    roll = 160f * W3.SmoothStep(0.1f, 0.6f, t); legCurl = 1.2f * k; grow = 1f - 0.5f * k;
                    p.Root = new Vector3(0, 0.1f * MathF.Sin(MathF.PI * W3.SmoothStep(0.1f, 0.6f, t)), 0);
                    p.Glow = 1f - 0.8f * k;
                    break;
                }
        }
        // curling pulls head and tail down under the body, like a pill bug
        p.Set(_body, roll, 0, pitch);
        p.Move(_body, new Vector3(0, -0.06f * curl, 0));
        p.Set(_head, 0, 3f * MathF.Sin(time * 1.4f), -55f * curl);
        p.Set(_tail, 0, 4f * MathF.Sin(time * 1.1f), 50f * curl);
        p.Grow(_crystals, grow);
        p.Set(_mandR, 0, mand + 10f, 0);
        p.Set(_mandL, 0, -mand - 10f, 0);
        foreach (var leg in _legs)
        {
            DesignKit.Step(p, leg, gait + leg.Phase, walk ? 22f : 0f, walk ? 26f : 0f);
            if (legCurl > 0f) DesignKit.Curl(p, leg, legCurl);
        }
        if (walk) p.Root += new Vector3(0, MathF.Abs(MathF.Sin(t * MathF.PI * 4f)) * 0.008f, 0);
    }
}
