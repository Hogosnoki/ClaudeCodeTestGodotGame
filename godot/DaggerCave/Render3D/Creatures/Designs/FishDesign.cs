using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Cave fish. The orange kind is a blood piranha: a deep, armoured body the colour of rust and
/// raw meat, a jutting underbite of interlocking triangular teeth and a mad glassy eye. The blue
/// kind is a ghost fish: long, pale and half see-through, lit along its flanks by cold spots of
/// light, blind white eyes and a mouth of needle fangs too long to close.
/// </summary>
public sealed class FishDesign : CreatureDesign
{
    private readonly bool _ghost;
    public FishDesign(bool ghost) { _ghost = ghost; }

    public override string Name => _ghost ? "fish2" : "fish";
    public override float Cell => 0.009f;
    public override float ThreeQuarter => 16f;

    public override CreatureLook Look => _ghost
        ? new CreatureLook
        {
            Eye = new Color(0.7f, 0.9f, 1f), EyeEnergy = 2.5f,
            Glow = new Color(0.35f, 0.8f, 1f), GlowEnergy = 1.4f,
            Rim = new Color(0.6f, 0.85f, 1f), RimEnergy = 0.3f,
            DetailScale = 70f, DetailStrength = 0.6f, Veins = 0.6f, VeinColor = new Color(0.3f, 0.1f, 0.15f), Wet = 1f,
        }
        : new CreatureLook
        {
            Eye = new Color(1f, 0.25f, 0.05f), EyeEnergy = 2.2f,
            Glow = new Color(1f, 0.4f, 0.1f), GlowEnergy = 1f,
            Rim = new Color(1f, 0.7f, 0.5f), RimEnergy = 0.3f,
            DetailScale = 55f, DetailStrength = 0.9f, Wet = 1f,
        };

    private int _body, _head, _jaw, _tail0, _tail1, _pecR, _pecL;

    protected override void OnBonesBound()
    {
        _body = B("body"); _head = B("head"); _jaw = B("jaw"); _tail0 = B("tail0"); _tail1 = B("tail1"); _pecR = B("pec_r"); _pecL = B("pec_l");
    }

    public override void Sculpt(Sculptor s)
    {
        float len = _ghost ? 1.15f : 1f;      // the ghost fish is longer and slimmer
        float deep = _ghost ? 0.7f : 1f;
        var back = _ghost ? new Color(0.2f, 0.24f, 0.3f) : new Color(0.15f, 0.06f, 0.035f);
        var flank = _ghost ? new Color(0.38f, 0.43f, 0.48f) : new Color(0.52f, 0.19f, 0.06f);
        var bellyC = _ghost ? new Color(0.5f, 0.52f, 0.55f) : new Color(0.62f, 0.34f, 0.2f);
        var fin = _ghost ? new Color(0.3f, 0.36f, 0.44f) : new Color(0.4f, 0.07f, 0.04f);
        var tooth = new Color(0.92f, 0.9f, 0.82f);
        var bodyMat = _ghost ? Mat.Skin : Mat.Scale;

        int body = s.Bone("body", -1, new(0, 0, 0));
        int head = s.Bone("head", body, new(0.12f * len, 0, 0));
        int jaw = s.Bone("jaw", head, new(0.2f * len, -0.06f * deep, 0));
        int t0 = s.Bone("tail0", body, new(-0.1f * len, 0, 0));
        int t1 = s.Bone("tail1", t0, new(-0.25f * len, 0.0f, 0));

        // body: deep and laterally flattened, a hump behind the head
        s.Egg(body, new(0, 0.01f, 0), new(0.2f * len, 0.15f * deep, 0.075f), flank, bodyMat, 0.03f);
        s.Egg(body, new(0.02f, 0.07f * deep, 0), new(0.17f * len, 0.08f * deep, 0.06f), back, bodyMat, 0.04f);
        s.Egg(body, new(0.01f, -0.07f * deep, 0), new(0.16f * len, 0.07f * deep, 0.06f), bellyC, bodyMat, 0.04f);
        s.Limb(t0, new(-0.12f * len, 0.01f, 0), new(-0.26f * len, 0.0f, 0), 0.1f * deep, 0.045f * deep, flank, bodyMat, 0.03f);
        s.Limb(t1, new(-0.25f * len, 0, 0), new(-0.33f * len, 0, 0), 0.045f * deep, 0.025f, back, bodyMat, 0.02f);
        // head: a blunt, bony brow, the gaping underbite
        s.Egg(head, new(0.15f * len, 0.02f, 0), new(0.11f * len, 0.12f * deep, 0.07f), back.Lerp(flank, 0.5f), _ghost ? Mat.Skin : Mat.Bone, 0.03f);
        s.Egg(jaw, new(0.22f * len, -0.075f * deep, 0), new(0.08f * len, 0.035f, 0.055f), bellyC, bodyMat, 0.02f, new Vector3(0, 0, 12f));
        foreach (int sd in new[] { 1, -1 })
        {
            s.Eye(head, new(0.19f * len, 0.04f * deep, 0.052f * sd), _ghost ? 0.02f : 0.026f, _ghost ? new Color(0.85f, 0.9f, 0.95f) : new Color(0.3f, 0.05f, 0.02f), 1f);
            // teeth: interlocking triangles (piranha) or needles (ghost), upper and lower
            int n = _ghost ? 4 : 6;
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)(n - 1);
                var up = new Vector3((0.2f + 0.08f * u) * len, -0.035f * deep, (0.045f - 0.03f * u) * sd);
                var lo = new Vector3((0.21f + 0.08f * u) * len, -0.075f * deep, (0.042f - 0.03f * u) * sd);
                float tl = _ghost ? 0.05f + 0.03f * (k % 2) : 0.022f;
                float tr = _ghost ? 0.005f : 0.009f;
                s.Horn(head, up, up + new Vector3(0.004f, -tl, 0), tr, tooth, Mat.Bone, sides: 4, rings: 2);
                s.Horn(jaw, lo, lo + new Vector3(0.006f, tl * (_ghost ? 1.3f : 1f), 0), tr, tooth, Mat.Bone, sides: 4, rings: 2);
            }
            // gill slits
            for (int g = 0; g < 3; g++)
                s.CarveLimb(head, new(0.08f * len - g * 0.015f, 0.04f * deep, 0.07f * sd), new(0.09f * len - g * 0.015f, -0.05f * deep, 0.07f * sd), 0.006f, 0.006f, 0.004f);
        }
        if (_ghost)
        {
            // cold lights along the flanks
            for (int k = 0; k < 7; k++)
                foreach (int sd in new[] { 1, -1 })
                    s.Ball(k < 4 ? body : t0, new(0.12f * len - k * 0.055f * len, -0.03f, 0.068f * sd - k * 0.005f * sd), 0.013f, new Color(0.6f, 0.9f, 1f), Mat.Slime, 0.004f).Emit = 1f;
        }

        // fins: spiky dorsal and anal fins, pectoral fans, the forked tail
        s.Sheet(new[] { new Vector3(0.08f * len, 0.14f * deep, 0), new Vector3(-0.02f * len, 0.3f * deep, 0), new Vector3(-0.16f * len, 0.12f * deep, 0) },
            new[] { body, body, t0 }, new[] { (0, 1, 2) }, 4, fin, 0.002f, Mat.Membrane, new[] { (1, 2) }, 0.02f);
        s.Horn(body, new(0.06f * len, 0.14f * deep, 0), new(-0.02f * len, 0.3f * deep, 0), 0.006f, fin.Darkened(0.3f), Mat.Claw, sides: 4, rings: 3);
        s.Sheet(new[] { new Vector3(-0.06f * len, -0.13f * deep, 0), new Vector3(-0.16f * len, -0.22f * deep, 0), new Vector3(-0.2f * len, -0.08f * deep, 0) },
            new[] { body, t0, t0 }, new[] { (0, 1, 2) }, 3, fin, 0.002f, Mat.Membrane, new[] { (1, 2) }, 0.015f);
        s.Sheet(new[] { new Vector3(-0.3f * len, 0, 0), new Vector3(-0.45f * len, 0.16f * deep, 0), new Vector3(-0.4f * len, 0, 0), new Vector3(-0.45f * len, -0.16f * deep, 0) },
            new[] { t1, t1, t1, t1 }, new[] { (0, 1, 2), (0, 2, 3) }, 4, fin, 0.002f, Mat.Membrane, new[] { (1, 2), (2, 3) }, 0.03f);
        foreach (int sd in new[] { 1, -1 })
        {
            int pec = s.Bone(sd > 0 ? "pec_r" : "pec_l", body, new(0.08f * len, -0.04f, 0.06f * sd));
            s.Sheet(new[] { new Vector3(0.08f * len, -0.04f, 0.06f * sd), new Vector3(-0.02f * len, -0.02f, 0.13f * sd), new Vector3(0.0f, -0.1f, 0.11f * sd) },
                new[] { pec, pec, pec }, new[] { (0, 1, 2) }, 3, fin, 0.002f, Mat.Membrane, new[] { (1, 2) }, 0.01f);
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "swim";
        float t = a.T, time = a.Time;
        float beat = MathF.Sin(t * Mathf.Tau), amp = 18f, jaw = 8f + 6f * MathF.Sin(time * 2.1f), bend = 0f, roll = 0f;
        float vx = MathF.Abs(a.Vel.X), vy = a.Vel.Y;
        float pitch = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(vy, Math.Max(vx, 0.6f))) * 0.6f, -35f, 35f);
        switch (c)
        {
            case "dart": amp = 30f; jaw = 30f; break;
            case "leap": amp = 10f; bend = 20f * MathF.Sin(t * Mathf.Tau); jaw = 35f; break;
            case "flop":
                // stranded: on its side, thrashing, gasping
                roll = 90f; bend = 35f * MathF.Sin(t * Mathf.Tau); amp = 0f; jaw = 20f + 20f * Math.Max(0, MathF.Sin(time * 5f)); pitch = 0f;
                p.Root = new Vector3(0, -0.1f + 0.08f * Math.Max(0, MathF.Sin(t * Mathf.Tau)), 0);
                break;
            case "hurt": amp = 35f; jaw = 30f; bend = 25f * Key(t, (0, 0), (0.2f, 1), (1, 0)); break;
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.4f));
                    roll = 180f * k; amp = 25f * (1 - k); jaw = 25f; pitch = 0f;
                    bend = 10f * k;
                    break;
                }
        }
        p.Set(_body, roll, beat * amp * 0.15f, pitch + bend * 0.3f);
        p.Set(_head, 0, -beat * amp * 0.25f, 0);
        p.Set(_jaw, 0, 0, -jaw);
        p.Set(_tail0, 0, MathF.Sin(t * Mathf.Tau - 0.9f) * amp, -bend * 0.7f);
        p.Set(_tail1, 0, MathF.Sin(t * Mathf.Tau - 1.8f) * amp * 1.3f, -bend * 0.6f);
        float fan = 15f * MathF.Sin(time * 4f);
        p.Set(_pecR, 0, 20f + fan, 0);
        p.Set(_pecL, 0, -20f - fan, 0);
    }
}
