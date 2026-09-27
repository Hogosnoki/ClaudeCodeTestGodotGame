using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A sporeling: a toadstool that walks. Its stalk is a pallid, fleshy trunk threaded with
/// mycelium, with a slack mouth and pinprick black eyes where a face should be; its bruise-purple
/// cap is warted white and its gills glow a sick green. It hunches under its cap, the gills
/// brighten, and it bursts a cloud of spores.
/// </summary>
public sealed class SporelingDesign : CreatureDesign
{
    public override string Name => "sporeling";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 22f;
    public override float FloorY => Floor;
    private const float Floor = -0.5625f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.1f, 0.1f, 0.1f), EyeEnergy = 0f,
        Glow = new Color(0.45f, 1f, 0.3f), GlowEnergy = 2.6f,
        Rim = new Color(0.7f, 0.9f, 0.6f), RimEnergy = 0.3f,
        DetailScale = 28f, DetailStrength = 1f, Veins = 0.8f, VeinColor = new Color(0.45f, 0.5f, 0.38f), Wet = 0.4f,
        LightColor = new Color(0.4f, 1f, 0.3f), LightEnergy = 0.5f, LightRange = 2.2f, LightOffset = new Vector3(0, 0.35f, 0),
    };

    private int _hips, _stalk, _cap, _jaw;
    private readonly int[] _thigh = new int[2], _shin = new int[2], _arm = new int[2];

    protected override void OnBonesBound()
    {
        _hips = B("hips"); _stalk = B("stalk"); _cap = B("cap"); _jaw = B("jaw");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _thigh[k] = B("thigh" + s); _shin[k] = B("shin" + s); _arm[k] = B("arm" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var flesh = new Color(0.72f, 0.66f, 0.58f);
        var bruise = new Color(0.34f, 0.16f, 0.32f);
        var wart = new Color(0.85f, 0.82f, 0.75f);
        var gill = new Color(0.5f, 0.75f, 0.35f);
        var root = new Color(0.4f, 0.33f, 0.25f);
        int hips = s.Bone("hips", -1, new(0, -0.3f, 0));
        int stalk = s.Bone("stalk", hips, new(0, -0.15f, 0));
        int cap = s.Bone("cap", stalk, new(0, 0.18f, 0));
        int jaw = s.Bone("jaw", stalk, new(0.08f, 0.0f, 0));

        // the cap: a broad dome, warted, its rim curled under; glowing gills beneath
        s.Egg(cap, new(0, 0.2f, 0), new(0.34f, 0.17f, 0.34f), bruise, Mat.Skin, 0.04f, bump: 0.008f);
        s.Egg(cap, new(0, 0.11f, 0), new(0.31f, 0.04f, 0.31f), gill, Mat.Fungus, 0.03f).Emit = 1f;
        // the stalk: a pallid trunk, thicker at the base, a ring of torn veil under the cap
        s.Limb(hips, new(0, -0.4f, 0), new(0, -0.18f, 0), 0.15f, 0.13f, flesh.Darkened(0.1f), Mat.Fungus, 0.05f, bump: 0.006f);
        s.Limb(stalk, new(0, -0.18f, 0), new(0.01f, 0.16f, 0), 0.13f, 0.11f, flesh, Mat.Fungus, 0.05f, bump: 0.004f);
        s.Egg(stalk, new(0, 0.08f, 0), new(0.14f, 0.025f, 0.14f), flesh.Darkened(0.2f), Mat.Fungus, 0.02f, bump: 0.006f);
        // the face: a slack wet mouth, pinprick black eyes, sunken cheeks
        s.CarveBall(stalk, new(0.12f, -0.01f, 0), 0.04f, 0.015f);
        s.Egg(jaw, new(0.1f, -0.045f, 0), new(0.04f, 0.02f, 0.06f), flesh.Darkened(0.15f), Mat.Fungus, 0.02f);
        s.Egg(stalk, new(0.095f, -0.005f, 0), new(0.02f, 0.02f, 0.04f), new Color(0.15f, 0.05f, 0.06f), Mat.Flesh, 0.01f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.CarveBall(stalk, new(0.105f, 0.06f, 0.04f * sd), 0.018f, 0.008f);
            s.Ball(stalk, new(0.098f, 0.06f, 0.04f * sd), 0.009f, new Color(0.01f, 0.01f, 0.01f), Mat.Eye, 0.004f).Emit = 0f;
        }

        var rng = s.Rng;
        for (int k = 0; k < 16; k++)
        {
            float a = k * 2.39996f, u = MathF.Sqrt((k + 0.5f) / 16f);
            var d = new Vector3(MathF.Cos(a) * u * 0.3f, 0, MathF.Sin(a) * u * 0.3f);
            float y = 0.2f + 0.17f * MathF.Sqrt(Math.Max(0f, 1f - (d.X * d.X + d.Z * d.Z) / (0.34f * 0.34f)));
            s.Ball(cap, new Vector3(d.X, y, d.Z), 0.022f + 0.02f * (float)rng.NextDouble(), wart, Mat.Fungus, 0.012f);
        }
        // hanging threads of mycelium from the rim
        for (int k = 0; k < 10; k++)
        {
            float a = k / 10f * Mathf.Tau + 0.3f;
            var at = new Vector3(MathF.Cos(a) * 0.29f, 0.1f, MathF.Sin(a) * 0.29f);
            s.Horn(cap, at, at + new Vector3(0.01f, -0.08f - 0.06f * (float)rng.NextDouble(), 0.01f), 0.006f, gill.Lightened(0.2f), Mat.Fungus, sides: 4, rings: 3, emit: 0.8f);
        }

        // legs: root-like stumps; arms: thin rootlets that dangle
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var hip = new Vector3(0, -0.4f, 0.08f * z);
            var knee = new Vector3(0.03f, -0.48f, 0.11f * z);
            var foot = new Vector3(0.02f, Floor + 0.02f, 0.12f * z);
            int th = s.Bone("thigh" + sfx, hips, hip);
            int sh = s.Bone("shin" + sfx, th, knee);
            s.Limb(th, hip, knee, 0.07f, 0.06f, root, Mat.Wood, 0.03f, bump: 0.005f);
            s.Limb(sh, knee, foot, 0.06f, 0.05f, root, Mat.Wood, 0.02f, bump: 0.005f);
            for (int f = 0; f < 3; f++)
                s.Horn(sh, foot + new Vector3(0, 0.01f, 0), foot + new Vector3(0.07f * MathF.Cos(f - 1f), -0.015f, (0.05f * MathF.Sin(f - 1f) + 0.02f) * z), 0.018f, root.Darkened(0.2f), Mat.Wood, sides: 5, rings: 3);
            var sh0 = new Vector3(0.02f, 0.02f, 0.11f * z);
            int arm = s.Bone("arm" + sfx, stalk, sh0);
            s.Horn(arm, sh0, sh0 + new Vector3(0.06f, -0.22f, 0.08f * z), 0.022f, flesh.Darkened(0.25f), Mat.Fungus, new Vector3(1, 0, 0), 0.04f, 6, 5);
            s.Horn(arm, sh0 + new Vector3(0.03f, -0.1f, 0.04f * z), sh0 + new Vector3(0.12f, -0.18f, 0.1f * z), 0.01f, flesh.Darkened(0.25f), Mat.Fungus, sides: 4, rings: 3);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        ref float ph = ref p.Vars[0];
        if (a.OnFloor) ph = (ph + speed / 0.9f * a.Dt) % 1f;
        float walk = W3.SmoothStep(0.1f, 0.8f, speed);
        float sn = MathF.Sin(ph * Mathf.Tau);
        // a waddle: the whole body rocks side to side, the cap lags behind
        float rock = 7f * sn * walk + 2f * MathF.Sin(time * 1.3f);
        float capSquash = 1f, lean = 4f * walk, jaw = 6f + 5f * MathF.Sin(time * 0.9f), capTilt = -rock * 0.6f;
        p.Glow = 1f + 0.2f * MathF.Sin(time * 2f);
        float drop = 0f;
        switch (c)
        {
            case "puff_windup":
                {
                    float k = W3.Smooth01(t);
                    capSquash = 1f - 0.22f * k; drop = 0.05f * k; lean = -6f * k; jaw = 20f * k;
                    p.Glow = 1f + 2.2f * k + 0.4f * MathF.Sin(time * 30f) * k;
                    break;
                }
            case "puff":
                {
                    float k = Key(t, (0, 0), (0.15f, 1), (1, 0));
                    capSquash = 1f + 0.35f * k; drop = -0.04f * k; jaw = 35f * k;
                    p.Glow = 1f + 3f * k;
                    break;
                }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); lean = -18f * k; capTilt += 20f * k; capSquash = 1f - 0.1f * k; break; }
            case "death":
                {
                    // wilts: the stalk buckles, the cap slumps off to one side, the light goes out
                    float k = W3.Smooth01(t);
                    lean = 50f * k; capTilt = 35f * k; drop = 0.2f * k; capSquash = 1f - 0.2f * k; jaw = 30f * k;
                    p.Glow = 1f - 0.9f * k;
                    break;
                }
        }
        p.Set(_hips, rock * 0.5f, 0, -lean * 0.3f);
        p.Move(_hips, new Vector3(0, -drop + 0.012f * MathF.Abs(sn) * walk, 0));
        p.Set(_stalk, rock * 0.4f, 0, -lean * 0.7f);
        p.Set(_cap, capTilt * 0.5f, 0, capTilt * 0.4f - lean * 0.2f);
        p.Grow(_cap, new Vector3(1f / MathF.Sqrt(capSquash), capSquash, 1f / MathF.Sqrt(capSquash)));
        p.Set(_jaw, 0, 0, -jaw);
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? 1 : -1;
            float step = sn * s;
            p.Set(_thigh[k], 0, 0, 24f * step * walk + (c == "death" ? 40f : 0f) * W3.Smooth01(t));
            p.Set(_shin[k], 0, 0, -18f * Math.Max(0, -step) * walk);
            p.Set(_arm[k], 8f * MathF.Sin(time * 1.7f + k) * s, 0, 10f * MathF.Sin(time * 1.1f + k * 2f) - 15f * step * walk);
        }
    }
}
