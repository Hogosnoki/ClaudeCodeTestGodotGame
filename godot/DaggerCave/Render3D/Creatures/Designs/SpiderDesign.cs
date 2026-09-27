using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A dog-sized cave spider: a bristling black body with bone-pale chevrons on a swollen abdomen,
/// eight glowing red eyes, hooked fangs, and eight long jointed legs arching above its back.
/// Crawls on ceilings (upside down), drops on a silk thread, pounces on the ground.
/// </summary>
public sealed class SpiderDesign : CreatureDesign
{
    public override string Name => "spider";
    public override float Cell => 0.014f;
    public override float ThreeQuarter => 28f;
    public override float FloorY => -0.5f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.08f, 0.03f), EyeEnergy = 4f,
        Rim = new Color(0.6f, 0.5f, 0.5f), RimEnergy = 0.2f,
        DetailScale = 40f, DetailStrength = 0.8f, Wet = 0.3f,
        LightColor = new Color(1f, 0.15f, 0.05f), LightEnergy = 0.35f, LightRange = 1.6f, LightOffset = new Vector3(0.45f, 0.1f, 0),
    };

    private const float Floor = -0.5f;
    private int _body, _abdomen, _head, _fangL, _fangR, _palpL, _palpR;
    private readonly DesignKit.Leg[] _legs = new DesignKit.Leg[8];

    protected override void OnBonesBound()
    {
        _body = B("body"); _abdomen = B("abdomen"); _head = B("head");
        _fangL = B("fang_l"); _fangR = B("fang_r"); _palpL = B("palp_l"); _palpR = B("palp_r");
    }

    public override void Sculpt(Sculptor s)
    {
        var black = new Color(0.1f, 0.08f, 0.07f);
        var brown = new Color(0.2f, 0.13f, 0.09f);
        var pale = new Color(0.75f, 0.68f, 0.55f);
        var red = new Color(0.45f, 0.05f, 0.04f);

        int body = s.Bone("body", -1, new(0.05f, 0.02f, 0));
        int abd = s.Bone("abdomen", body, new(-0.08f, 0.05f, 0));
        int head = s.Bone("head", body, new(0.22f, 0.04f, 0));

        // cephalothorax and head
        s.Egg(body, new(0.1f, 0.03f, 0), new(0.2f, 0.11f, 0.16f), black, Mat.Chitin, 0.03f, bump: 0.004f);
        s.Egg(head, new(0.27f, 0.06f, 0), new(0.1f, 0.09f, 0.11f), black, Mat.Chitin, 0.04f);
        s.Egg(body, new(0.08f, 0.11f, 0), new(0.14f, 0.04f, 0.1f), brown, Mat.Chitin, 0.03f);
        // pedicel and the swollen abdomen, tilted up at the back, bristling
        s.Limb(abd, new(-0.06f, 0.05f, 0), new(-0.14f, 0.08f, 0), 0.05f, 0.07f, black, Mat.Chitin, 0.03f);
        s.Egg(abd, new(-0.34f, 0.12f, 0), new(0.3f, 0.23f, 0.24f), brown, Mat.Fur, 0.05f, new Vector3(0, 0, -14f), bump: 0.012f);
        // bone-pale chevrons and a blood-red mark on the back
        for (int k = 0; k < 4; k++)
        {
            float x = -0.18f - k * 0.1f;
            s.Egg(abd, new(x, 0.3f - k * 0.015f, 0.07f), new(0.05f, 0.012f, 0.035f), pale, Mat.Chitin, 0.01f, new Vector3(0, 35f, 0));
            s.Egg(abd, new(x, 0.3f - k * 0.015f, -0.07f), new(0.05f, 0.012f, 0.035f), pale, Mat.Chitin, 0.01f, new Vector3(0, -35f, 0));
        }
        s.Egg(abd, new(-0.36f, 0.33f, 0), new(0.06f, 0.012f, 0.03f), red, Mat.Chitin, 0.01f);
        // spinnerets
        s.Horn(abd, new(-0.6f, 0.06f, 0.02f), new(-0.67f, 0.03f, 0.03f), 0.022f, black, Mat.Chitin);
        s.Horn(abd, new(-0.6f, 0.06f, -0.02f), new(-0.67f, 0.03f, -0.03f), 0.022f, black, Mat.Chitin);

        // eyes: two big ones in front, a ring of smaller ones behind
        s.Eye(head, new(0.36f, 0.1f, 0.035f), 0.028f, new Color(0.2f, 0.02f, 0.02f), 1f);
        s.Eye(head, new(0.36f, 0.1f, -0.035f), 0.028f, new Color(0.2f, 0.02f, 0.02f), 1f);
        s.Eye(head, new(0.335f, 0.135f, 0.075f), 0.018f, new Color(0.2f, 0.02f, 0.02f), 0.9f);
        s.Eye(head, new(0.335f, 0.135f, -0.075f), 0.018f, new Color(0.2f, 0.02f, 0.02f), 0.9f);
        s.Eye(head, new(0.3f, 0.145f, 0.05f), 0.013f, new Color(0.2f, 0.02f, 0.02f), 0.8f);
        s.Eye(head, new(0.3f, 0.145f, -0.05f), 0.013f, new Color(0.2f, 0.02f, 0.02f), 0.8f);
        s.Eye(head, new(0.32f, 0.08f, 0.1f), 0.012f, new Color(0.2f, 0.02f, 0.02f), 0.8f);
        s.Eye(head, new(0.32f, 0.08f, -0.1f), 0.012f, new Color(0.2f, 0.02f, 0.02f), 0.8f);

        // chelicerae with hooked fangs
        int fr = s.Bone("fang_r", head, new(0.33f, 0.0f, 0.045f));
        int fl = s.Bone("fang_l", head, new(0.33f, 0.0f, -0.045f));
        foreach (var (b, z) in new[] { (fr, 1f), (fl, -1f) })
        {
            s.Limb(b, new(0.33f, 0.02f, 0.045f * z), new(0.37f, -0.06f, 0.04f * z), 0.04f, 0.03f, black, Mat.Chitin, 0.015f, bump: 0.003f);
            s.Horn(b, new(0.375f, -0.07f, 0.04f * z), new(0.34f, -0.17f, 0.025f * z), 0.014f, new Color(0.08f, 0.05f, 0.04f), Mat.Claw, new Vector3(1, 0, 0), 0.035f);
        }
        // pedipalps
        int pr = s.Bone("palp_r", head, new(0.32f, 0.02f, 0.08f));
        int pl = s.Bone("palp_l", head, new(0.32f, 0.02f, -0.08f));
        foreach (var (b, z) in new[] { (pr, 1f), (pl, -1f) })
        {
            s.Limb(b, new(0.32f, 0.02f, 0.08f * z), new(0.43f, 0.08f, 0.1f * z), 0.02f, 0.017f, black, Mat.Chitin, 0.01f);
            s.Limb(b, new(0.43f, 0.08f, 0.1f * z), new(0.5f, -0.02f, 0.09f * z), 0.017f, 0.022f, brown, Mat.Fur, 0.01f);
        }

        // eight legs, knees high above the back
        float[] yaws = { 52f, 18f, -18f, -50f };
        float[] reach = { 0.95f, 0.85f, 0.85f, 0.95f };
        for (int k = 0; k < 4; k++)
        {
            float x = 0.18f - k * 0.075f;
            for (int sd = 0; sd < 2; sd++)
            {
                int side = sd == 0 ? 1 : -1;
                var hip = new Vector3(x, 0.02f, 0.11f * side);
                _legs[k * 2 + sd] = DesignKit.ArthroLeg(s, $"leg{k}{(side > 0 ? "r" : "l")}", body, hip, yaws[k], side, reach[k], 0.4f, Floor,
                    0.043f, k % 2 == 0 ? black : brown, Mat.Fur, bristles: 2.2f);
                // alternating gait: L1 R2 L3 R4 move together
                _legs[k * 2 + sd].Phase = ((k + sd) % 2) * 0.5f;
            }
        }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        // the gait phase runs with the crawl clip, twice per loop
        float gait = c == "crawl" ? t * 2f : time * 0.3f;
        float stride = c == "crawl" ? 16f : 0f, lift = c == "crawl" ? 22f : 0f;
        float curl = 0f, crouch = 0f;
        float fang = 8f * MathF.Sin(time * 5.3f) * MathF.Sin(time * 1.7f); // restless fangs
        float breathe = MathF.Sin(time * 2.2f);
        switch (c)
        {
            case "drop": curl = 0.35f; fang = 18f; break;
            case "hang":
                curl = 0.45f + 0.1f * MathF.Sin(time * 3f);
                p.Root = new Vector3(0, 0, 0);
                p.Set(_body, MathF.Sin(time * 1.4f) * 8f, 0, MathF.Sin(time * 1.1f) * 6f);
                break;
            case "pounce":
                crouch = Key(t, (0, 12), (0.3f, -8), (1, 0));
                fang = Key(t, (0, 5), (0.25f, 30), (1, 10));
                p.Set(_body, 0, 0, Key(t, (0, -8), (0.3f, 16), (1, 0)));
                break;
            case "hurt": curl = Key(t, (0, 0), (0.3f, 0.45f), (1, 0)); break;
            case "death":
                curl = W3.SmoothStep(0f, 0.7f, t) * 1.1f;
                p.Root = new Vector3(0, -0.22f * W3.SmoothStep(0.2f, 0.8f, t), 0);
                p.Set(_body, 0, 0, 12f * t);
                break;
        }
        p.Set(_abdomen, 0, MathF.Sin(time * 0.9f) * 4f, -3f + breathe * 2.5f);
        p.Set(_head, 0, 0, MathF.Sin(time * 2.7f) * 3f);
        p.Set(_fangR, 0, 0, fang);
        p.Set(_fangL, 0, 0, fang * 0.9f);
        p.Set(_palpR, 0, 0, 10f * MathF.Sin(time * 3.1f));
        p.Set(_palpL, 0, 0, 10f * MathF.Sin(time * 3.1f + 1.2f));
        foreach (var leg in _legs)
        {
            DesignKit.Step(p, leg, gait + leg.Phase, stride, lift, crouch);
            if (curl > 0f) DesignKit.Curl(p, leg, curl);
            else if (c == "idle")
            {
                // an occasional twitch
                float tw = MathF.Max(0f, MathF.Sin(time * 1.3f + leg.Phase * 7f + leg.Side * 2f) - 0.93f) * 90f;
                p.AddAxis(leg.Tibia, Vector3.Up.Cross(leg.Out), -tw);
            }
        }
        if (c == "crawl") p.Root = new Vector3(0, MathF.Abs(MathF.Sin(t * MathF.PI * 4f)) * 0.015f, 0);
    }

    // ---- the silk thread up to where it hangs from
    public override void Frame(CreatureModel m, in AnimInput a)
    {
        var thread = m.GetNodeOrNull<MeshInstance3D>("Thread");
        float? anchor = (a.Owner as Spider)?.ThreadAnchorY;
        if (anchor == null || a.Clip == "death")
        {
            if (thread != null) thread.Visible = false;
            return;
        }
        if (thread == null)
        {
            thread = new MeshInstance3D
            {
                Name = "Thread",
                Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 1f, RadialSegments = 4, Rings = 1 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.85f, 0.88f, 0.95f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    EmissionEnabled = true, Emission = new Color(0.6f, 0.65f, 0.75f), EmissionEnergyMultiplier = 0.4f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                TopLevel = true,
            };
            m.AddChild(thread);
        }
        float top = -anchor.Value / W3.Ppu;
        var body = m.GlobalPosition + new Vector3(0, 0.1f * m.Scale.Y, 0);
        float len = Math.Max(0.05f, top - body.Y);
        thread.Visible = true;
        thread.GlobalTransform = new Transform3D(Basis.FromScale(new Vector3(1, len, 1)), new Vector3(body.X, body.Y + len * 0.5f, body.Z));
    }
}
