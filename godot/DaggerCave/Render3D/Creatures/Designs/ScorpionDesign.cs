using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A cave scorpion the size of a pony: a flat carapace of black-bronze plates, crab-heavy
/// serrated pincers, eight spiked legs, and a jointed tail arched over its back to a swollen
/// amber venom bulb whose hooked sting drips glowing poison. Pulls the tail back, then whips it
/// over its head.
/// </summary>
public sealed class ScorpionDesign : CreatureDesign
{
    public override string Name => "scorpion";
    public override float Cell => 0.013f;
    public override float ThreeQuarter => 26f;
    public override float FloorY => Floor;
    private const float Floor = -0.5625f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.12f, 0.05f), EyeEnergy = 4f,
        Glow = new Color(0.7f, 1f, 0.2f), GlowEnergy = 3.5f,
        Rim = new Color(0.8f, 0.6f, 0.4f), RimEnergy = 0.25f,
        DetailScale = 34f, DetailStrength = 0.8f, Wet = 0.2f,
        LightColor = new Color(0.6f, 1f, 0.2f), LightEnergy = 0.3f, LightRange = 1.5f, LightOffset = new Vector3(-0.15f, 0.25f, 0),
    };

    private int _body, _abd, _telson;
    private readonly int[] _tail = new int[5];
    private readonly int[] _arm = new int[2], _fore = new int[2], _claw = new int[2], _finger = new int[2];
    private readonly DesignKit.Leg[] _legs = new DesignKit.Leg[8];

    protected override void OnBonesBound()
    {
        _body = B("body"); _abd = B("abd"); _telson = B("telson");
        for (int k = 0; k < 5; k++) _tail[k] = B("tail" + k);
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _arm[k] = B("arm" + s); _fore[k] = B("fore" + s); _claw[k] = B("claw" + s); _finger[k] = B("finger" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var black = new Color(0.13f, 0.095f, 0.07f);
        var bronze = new Color(0.4f, 0.25f, 0.1f);
        var amber = new Color(0.55f, 0.34f, 0.07f);
        var venom = new Color(0.75f, 1f, 0.3f);
        var dark = new Color(0.05f, 0.04f, 0.035f);

        const float lift = 0.07f; // the body rides higher than a real scorpion's, for a menacing profile
        int body = s.Bone("body", -1, new(0.2f, -0.36f + lift, 0));
        int abd = s.Bone("abd", body, new(0.06f, -0.35f + lift, 0));

        // carapace: a flat shield with a raised keel, the face with its small chelicerae
        s.Egg(body, new(0.25f, -0.34f + lift, 0), new(0.2f, 0.09f, 0.16f), black, Mat.Chitin, 0.03f, bump: 0.003f);
        s.Egg(body, new(0.27f, -0.27f + lift, 0), new(0.15f, 0.035f, 0.075f), bronze, Mat.Chitin, 0.025f);
        s.Egg(body, new(0.22f, -0.39f + lift, 0), new(0.16f, 0.06f, 0.13f), dark, Mat.Chitin, 0.03f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Horn(body, new(0.43f, -0.35f + lift, 0.03f * sd), new(0.5f, -0.4f + lift, 0.025f * sd), 0.018f, bronze, Mat.Chitin, Vector3.Down, 0.01f, 5, 3);
            // eyes: a glowing median pair on the keel, clusters at the front corners
            s.Eye(body, new(0.33f, -0.24f + lift, 0.024f * sd), 0.019f, new Color(0.3f, 0.02f, 0.02f), 1f);
            for (int e = 0; e < 3; e++)
                s.Eye(body, new(0.41f - e * 0.02f, -0.3f + lift + e * 0.006f, (0.1f + e * 0.012f) * sd), 0.01f, new Color(0.3f, 0.02f, 0.02f), 0.8f);
        }

        // the segmented back: overlapping plates, alternately black and bronze
        for (int k = 0; k < 6; k++)
        {
            float x = 0.05f - k * 0.085f;
            float zw = 0.17f - 0.012f * k - (k == 0 ? 0.02f : 0f);
            var col = k % 2 == 0 ? black : bronze.Darkened(0.2f);
            s.Egg(abd, new(x, -0.32f + lift + 0.01f * MathF.Sin(k), 0), new(0.06f, 0.1f - 0.005f * k, zw), col, Mat.Chitin, 0.014f, bump: 0.002f);
            s.Egg(abd, new(x - 0.01f, -0.4f + lift, 0), new(0.06f, 0.05f, zw * 0.85f), dark, Mat.Chitin, 0.02f);
        }

        // the tail: five beaded segments arching over the back, spined along the top
        var pts = new Vector3[]
        {
            new(-0.42f, -0.3f + lift, 0), new(-0.56f, -0.18f + lift, 0), new(-0.63f, -0.01f + lift, 0), new(-0.61f, 0.16f + lift, 0), new(-0.51f, 0.3f + lift, 0), new(-0.36f, 0.37f + lift, 0),
        };
        float[] rad = { 0.068f, 0.064f, 0.06f, 0.056f, 0.052f, 0.05f };
        int parent = abd;
        for (int k = 0; k < 5; k++) parent = s.Bone("tail" + k, parent, pts[k]);
        int telson = s.Bone("telson", parent, pts[5]);
        for (int k = 0; k < 5; k++)
        {
            int b = s["tail" + k];
            s.Limb(b, pts[k], pts[k + 1], rad[k], rad[k + 1], k % 2 == 0 ? black : bronze.Darkened(0.35f), Mat.Chitin, 0.012f, bump: 0.002f);
            var mid = pts[k].Lerp(pts[k + 1], 0.55f);
            s.Egg(b, mid, new(rad[k] * 1.12f, rad[k] * 1.12f, rad[k] * 1.05f), black, Mat.Chitin, 0.02f);
            var outward = (pts[k + 1] - pts[k]).Normalized().Cross(Vector3.Back);
            s.Horn(b, mid + outward * rad[k] * 0.9f, mid + outward * (rad[k] + 0.045f) - (pts[k + 1] - pts[k]).Normalized() * 0.02f, 0.012f, bronze, Mat.Claw, sides: 5, rings: 3);
        }
        // the venom bulb and its hooked sting, a bead of glowing poison at the point
        var tl = new Vector3(0, lift + 0.02f, 0);
        s.Egg(telson, new Vector3(-0.26f, 0.34f, 0) + tl, new(0.09f, 0.065f, 0.065f), amber, Mat.Chitin, 0.025f, new Vector3(0, 0, -10f)).Emit = 0.25f;
        s.Egg(telson, new Vector3(-0.25f, 0.31f, 0) + tl, new(0.055f, 0.034f, 0.045f), venom.Darkened(0.4f), Mat.Slime, 0.02f).Emit = 0.6f;
        s.Horn(telson, new Vector3(-0.19f, 0.31f, 0) + tl, new Vector3(-0.08f, 0.19f, 0) + tl, 0.027f, dark, Mat.Claw, new Vector3(1, 1, 0).Normalized(), 0.035f);
        s.Ball(telson, new Vector3(-0.078f, 0.18f, 0) + tl, 0.012f, venom, Mat.Slime, 0.004f).Emit = 1.4f;

        // pincers: a jointed arm to a swollen hand with two serrated fingers
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var S = new Vector3(0.4f, -0.35f + lift, 0.1f * z);
            var E = new Vector3(0.56f, -0.28f + lift, 0.26f * z);
            var W = new Vector3(0.74f, -0.3f + lift, 0.22f * z);
            var F = new Vector3(0.93f, -0.3f + lift, 0.2f * z);
            int arm = s.Bone("arm" + sfx, body, S);
            int fore = s.Bone("fore" + sfx, arm, E);
            int claw = s.Bone("claw" + sfx, fore, W);
            int finger = s.Bone("finger" + sfx, claw, F);
            s.Limb(arm, S, E, 0.045f, 0.042f, black, Mat.Chitin, 0.012f);
            s.Ball(fore, E, 0.05f, bronze.Darkened(0.3f), Mat.Chitin, 0.01f);
            s.Limb(fore, E, W, 0.055f, 0.05f, black, Mat.Chitin, 0.012f, bump: 0.002f);
            var hand = new Vector3(0.84f, -0.28f + lift, 0.21f * z);
            s.Egg(claw, hand, new(0.14f, 0.085f, 0.09f), black, Mat.Chitin, 0.025f, bump: 0.003f);
            s.Egg(claw, hand + new Vector3(0, 0.05f, 0), new(0.1f, 0.035f, 0.06f), bronze, Mat.Chitin, 0.02f);
            // fixed finger above, movable finger below, teeth facing each other
            var fixA = new Vector3(0.94f, -0.26f + lift, 0.2f * z);
            var fixB = new Vector3(1.13f, -0.26f + lift, 0.17f * z);
            var movA = F + new Vector3(0, -0.03f, 0);
            var movB = new Vector3(1.12f, -0.35f + lift, 0.17f * z);
            s.Limb(claw, fixA, fixB, 0.042f, 0.009f, black, Mat.Chitin, 0.01f);
            s.Limb(finger, movA, movB, 0.036f, 0.008f, black, Mat.Chitin, 0.01f);
            for (int t = 0; t < 5; t++)
            {
                float u = 0.15f + t * 0.17f;
                var up = fixA.Lerp(fixB, u) + new Vector3(0, -0.025f * (1 - u), 0);
                s.Horn(claw, up, up + new Vector3(0.006f, -0.026f, 0), 0.007f, bronze.Lightened(0.25f), Mat.Claw, sides: 4, rings: 2);
                var lo = movA.Lerp(movB, u) + new Vector3(0, 0.02f * (1 - u), 0);
                s.Horn(finger, lo, lo + new Vector3(0.006f, 0.024f, 0), 0.006f, bronze.Lightened(0.25f), Mat.Claw, sides: 4, rings: 2);
            }
        }

        // eight spiked legs under the plates
        float[] xs = { 0.3f, 0.2f, 0.09f, -0.02f };
        float[] yaws = { 40f, 14f, -14f, -38f };
        for (int k = 0; k < 4; k++)
            for (int sd = 0; sd < 2; sd++)
            {
                int side = sd == 0 ? 1 : -1;
                var hip = new Vector3(xs[k], -0.37f + lift, 0.12f * side);
                _legs[k * 2 + sd] = DesignKit.ArthroLeg(s, $"leg{k}{(side > 0 ? "r" : "l")}", k < 2 ? body : abd, hip, yaws[k], side, 0.56f + 0.03f * k, 0.22f, Floor,
                    0.034f, k % 2 == 0 ? black : bronze.Darkened(0.4f), Mat.Chitin, bristles: 0.7f, tipCol: bronze);
                _legs[k * 2 + sd].Phase = ((k + sd) % 2) * 0.5f + k * 0.08f;
            }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        bool walk = c == "walk";
        float gait = walk ? t * 2f : time * 0.25f;
        float stride = walk ? 15f : 0f, lift = walk ? 20f : 0f;
        float crouch = 0f, curl = 0f;
        // tail: overall raise at the root, curl along the segments, sway sideways
        float raise = 3f * MathF.Sin(time * 1.3f), bend = 2f * MathF.Sin(time * 1.7f + 1f), sway = 6f * MathF.Sin(time * 0.8f), sting = 0f;
        float armUp = 4f * MathF.Sin(time * 1.1f), open = 10f + 8f * MathF.Sin(time * 2.3f), reach = 0f;
        float pitch = 0f, roll = 0f;
        switch (c)
        {
            case "sting_windup":
                {
                    float k = W3.Smooth01(t);
                    raise = -14f * k; bend = -6f * k; sting = -15f * k;
                    armUp = 22f * k; open = 35f * k; crouch = 8f * k; pitch = -4f * k;
                    sway = 0f;
                    break;
                }
            case "sting":
                {
                    // the whip: over the back and down in front, then settling
                    float k = Key(t, (0, 0), (0.28f, 1), (0.6f, 1), (1, 0.35f));
                    raise = Mathf.Lerp(-14f, 58f, k); bend = Mathf.Lerp(-6f, 14f, k); sting = Mathf.Lerp(-15f, 35f, k);
                    armUp = Mathf.Lerp(22f, -8f, k); open = 35f * (1 - k) + 5f; reach = 18f * k;
                    pitch = 6f * k; crouch = -4f * k;
                    sway = 0f;
                    break;
                }
            case "hurt":
                {
                    float k = Key(t, (0, 0), (0.2f, 1), (1, 0));
                    pitch = -10f * k; raise = -10f * k; armUp = 25f * k; open = 40f * k; curl = 0.3f * k;
                    break;
                }
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.3f));
                    roll = 170f * W3.SmoothStep(0.1f, 0.7f, t);
                    curl = 1.1f * k; raise = -20f * k; bend = 14f * k * MathF.Sin(time * 9f) * (1 - W3.SmoothStep(0.6f, 1f, t)) + 10f * k;
                    open = 40f * k; armUp = 30f * k;
                    p.Root = new Vector3(0, 0.12f * MathF.Sin(MathF.PI * W3.SmoothStep(0.1f, 0.7f, t)), 0);
                    break;
                }
        }
        p.Set(_body, roll, 0, pitch);
        p.Set(_abd, 0, sway * 0.2f, -crouch * 0.3f);
        p.Set(_tail[0], 0, sway * 0.4f, -raise);
        for (int k = 1; k < 5; k++) p.Set(_tail[k], 0, sway * 0.25f, -bend);
        p.Set(_telson, 0, 0, -sting);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            float jitter = 5f * MathF.Sin(time * 2.9f + k * 2f);
            p.Set(_arm[k], 0, (-10f + reach + jitter) * z, armUp);
            p.Set(_fore[k], 0, -reach * 0.6f * z, -armUp * 0.4f);
            p.Set(_claw[k], 0, 0, -armUp * 0.3f);
            p.Set(_finger[k], 0, 0, -open);
        }
        foreach (var leg in _legs)
        {
            DesignKit.Step(p, leg, gait + leg.Phase, stride, lift, crouch);
            if (curl > 0f) DesignKit.Curl(p, leg, curl);
        }
        if (walk) p.Root += new Vector3(0, MathF.Abs(MathF.Sin(t * MathF.PI * 4f)) * 0.01f, 0);
    }
}
