using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// An automaton: a clockwork soldier of riveted brass plate, a barrel of a chest with a furnace burning behind a barred window, a domed
/// head with a single amber lens, a great key turning in its back, one arm ending in a cleaver and the other in a pincer. It runs on a
/// spring: the key turns slower as it runs down, and it droops; stopped to be rewound, the key whirls.
/// </summary>
public sealed class AutomatonDesign : BipedDesign
{
    public AutomatonDesign()
    {
        P = new Spec
        {
            Floor = -0.5625f, HipH = 0.5f, Spine = 0.12f, Chest = 0.22f, Neck = 0.06f, HeadUp = 0.08f, Hunch = 0.03f,
            ShoulderW = 0.2f, HipW = 0.1f, UpperArm = 0.22f, ForeArm = 0.22f, Hand = 0.1f, Thigh = 0.26f, Shin = 0.24f, Foot = 0.15f,
        };
    }

    public override string Name => "automaton";
    public override float LifeScale => 0.5f;
    public override float Cell => 0.014f;
    public override float ThreeQuarter => 22f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.65f, 0.15f), EyeEnergy = 3.2f,
        Glow = new Color(1f, 0.6f, 0.15f), GlowEnergy = 1.1f,
        Rim = new Color(0.8f, 0.7f, 0.5f), RimEnergy = 0.25f,
        DetailScale = 26f, DetailStrength = 0.8f,
        LightColor = new Color(1f, 0.6f, 0.2f), LightEnergy = 0.22f, LightRange = 2.2f, LightOffset = new Vector3(0.25f, 0.5f, 0),
    };

    private int _key;
    protected override void OnBonesBound() { base.OnBonesBound(); _key = B("key"); }

    public override void Sculpt(Sculptor s)
    {
        var brass = new Color(0.4f, 0.3f, 0.14f);
        var brassDk = brass.Darkened(0.3f);
        var iron = new Color(0.22f, 0.22f, 0.26f);
        var steel = new Color(0.46f, 0.46f, 0.52f);
        var amber = new Color(1f, 0.6f, 0.15f);
        BuildSkeleton(s);
        int hp = s["hips"], sp = s["spine"], ch = s["chest"], nk = s["neck"], hd = s["head"];
        int key = s.Bone("key", ch, Chest + new Vector3(-0.22f, 0.05f, 0));
        var rng = s.Rng;
        // the trunk: an iron pelvis, a brass barrel of a chest banded in iron
        s.Block(hp, Hips + new Vector3(0, 0.0f, 0), new(0.13f, 0.1f, 0.17f), 0.04f, iron, Mat.Metal, 0.03f);
        s.Egg(sp, Spine + new Vector3(0, 0.01f, 0), new(0.15f, 0.12f, 0.19f), brassDk, Mat.Metal, 0.04f);
        s.Egg(ch, Chest + new Vector3(0, 0.0f, 0), new(0.2f, 0.26f, 0.25f), brass, Mat.Metal, 0.04f, bump: 0.003f);
        foreach (float y in new[] { -0.15f, 0.0f, 0.15f })
            s.Egg(ch, Chest + new Vector3(0, y, 0), new(0.205f, 0.022f, 0.255f), iron, Mat.Metal, 0.012f);
        // the furnace behind its barred window
        s.Egg(ch, Chest + new Vector3(0.19f, 0.02f, 0), new(0.05f, 0.1f, 0.11f), amber, Mat.Ember, 0.02f).Emit = 0.6f;
        for (int k = -1; k <= 1; k++)
            s.Limb(ch, Chest + new Vector3(0.235f, 0.12f, 0.06f * k), Chest + new Vector3(0.235f, -0.08f, 0.06f * k), 0.012f, 0.012f, iron, Mat.Metal, 0.006f);
        // rivets round the bands
        for (int k = 0; k < 22; k++)
        {
            float a = k / 22f * Mathf.Tau, y = (k % 2 == 0 ? -0.15f : 0.15f);
            s.Ball(ch, Chest + new Vector3(0.2f * MathF.Cos(a) * 1.0f, y + 0.026f, 0.25f * MathF.Sin(a)), 0.012f, steel, Mat.Metal, 0.004f);
        }
        // the head: a dome with a single amber lens, a vent pipe, a collar
        s.Limb(nk, Neck + new Vector3(0, -0.04f, 0), Head, 0.09f, 0.08f, iron, Mat.Metal, 0.03f);
        s.Egg(hd, Head + new Vector3(0.02f, 0.05f, 0), new(0.12f, 0.12f, 0.12f), brass, Mat.Metal, 0.03f, bump: 0.002f);
        s.Egg(hd, Head + new Vector3(0.02f, 0.0f, 0), new(0.125f, 0.02f, 0.125f), iron, Mat.Metal, 0.01f);
        s.Eye(hd, Head + new Vector3(0.12f, 0.06f, 0), 0.045f, amber, 1f);
        s.Limb(hd, Head + new Vector3(-0.04f, 0.15f, 0.05f), Head + new Vector3(-0.06f, 0.26f, 0.06f), 0.025f, 0.02f, iron, Mat.Metal, 0.01f);
        // the key in its back: a stem and two bows
        s.Limb(key, Chest + new Vector3(-0.2f, 0.05f, 0), Chest + new Vector3(-0.36f, 0.05f, 0), 0.02f, 0.02f, steel, Mat.Metal, 0.01f);
        foreach (int sd in new[] { 1, -1 })
            s.Egg(key, Chest + new Vector3(-0.38f, 0.05f, 0.07f * sd), new(0.014f, 0.1f, 0.06f), brass.Lightened(0.1f), Mat.Metal, 0.01f);
        // shoulders and arms: pistons, brass joints; a cleaver and a pincer
        Limbs(s, 0.065f, 0.075f, iron, iron, Mat.Metal, Mat.Metal);
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            s.Ball(ch, Shoulder[k] + new Vector3(0, 0.02f, 0), 0.085f, brass, Mat.Metal, 0.03f);
            s.Ball(s["farm" + sfx], Elbow[k], 0.06f, brass, Mat.Metal, 0.02f);
            s.Ball(s["shin" + sfx], Knee[k], 0.065f, brass, Mat.Metal, 0.02f);
            s.Block(s["foot" + sfx], Ankle[k] + new Vector3(0.06f, -0.04f, 0), new(0.11f, 0.05f, 0.075f), 0.03f, iron, Mat.Metal, 0.02f);
            s.Block(s["thigh" + sfx], HipJ[k] + new Vector3(0, -0.05f, 0), new(0.075f, 0.08f, 0.07f), 0.03f, brassDk, Mat.Metal, 0.02f);
        }
        // right: a broad cleaver; left: a two-pronged pincer
        s.Block(hand[0], Wrist[0] + new Vector3(0.05f, -0.15f, 0), new(0.03f, 0.24f, 0.12f), 0.01f, steel, Mat.Metal, 0.008f, new Vector3(0, 0, 10f));
        s.Block(hand[0], Wrist[0] + new Vector3(0.0f, -0.02f, 0), new(0.05f, 0.05f, 0.06f), 0.02f, iron, Mat.Metal, 0.01f);
        foreach (int sd in new[] { 1, -1 })
            s.Horn(hand[1], Wrist[1] + new Vector3(0.0f, -0.02f, 0.03f * sd), Wrist[1] + new Vector3(0.05f, -0.2f, 0.07f * sd), 0.03f, steel, Mat.Metal, Vector3.Right, 0.04f, 6, 5);
    }

    protected override Body Idle(float time)
    {
        var o = base.Idle(time * 0.6f);
        o.Lean += 4; o.SR = 6; o.SL = 5; o.ER = 14; o.EL = 14;
        return o;
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        float speed = MathF.Abs(a.Vel.X);
        var au = a.Owner as Automaton;
        float spring = au?.Spring ?? 1f;
        float ph = Gait(p, a, 1.5f);
        Body o = Walk(ph, W3.SmoothStep(0.2f, 1.2f, speed), false, time);
        // every step falls like a dropped plate
        o.Root.Y -= 0.02f * MathF.Abs(MathF.Sin(ph * Mathf.Tau)) * W3.SmoothStep(0.2f, 1.2f, speed);
        // wound down, it droops and shivers
        float tired = 1f - spring;
        o.Lean += 8f * tired; o.HeadPitch += 8f * tired;
        o.Root.X += 0.004f * tired * MathF.Sin(time * 40f);
        // the key: turns with the spring, whirls when it is being rewound
        bool rewinding = au?.Rewinding ?? false;
        p.Vars[2] = (p.Vars[2] + a.Dt * (rewinding ? 560f : 25f + 130f * spring)) % 360f;
        p.Glow = 0.6f + 0.4f * spring + 0.12f * MathF.Sin(time * 3f);
        switch (c)
        {
            case "chop_windup":
                {
                    float k = W3.Smooth01(t);
                    o = Chop(o, k, 0f);
                    o.Lean -= 6f * k;
                    p.Glow += 1.2f * k;
                    break;
                }
            case "chop":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 2.4f));
                    o = Chop(o, 1f, k);
                    o.Root.Y -= 0.04f * k;
                    p.Glow += 1.6f * (1f - t);
                    break;
                }
            case "rewind":
                o.Lean = -10f; o.HeadPitch = 22f; o.SR = 22f; o.SL = 24f; o.ER = 30f; o.EL = 30f;
                o.KR += 10f; o.KL += 12f;
                p.Glow = 0.4f + 0.2f * MathF.Sin(time * 8f);
                break;
            case "hurt": o = Hurt(o, t); break;
            case "death":
                {
                    float k1 = W3.SmoothStep(0f, 0.3f, t), k2 = W3.SmoothStep(0.25f, 0.8f, t);
                    o.KR += 70f * k1; o.KL += 60f * k1; o.HR += 30f * k1; o.HL += 25f * k1;
                    o.Root = new Vector3(-0.08f * k2, -0.12f * k1 - 0.3f * k2, 0);
                    o.Lean = -20f * k2; o.Roll = 80f * k2; o.SR += 60f * k2; o.SL += 50f * k2;
                    p.Glow = 1f - k1;
                    break;
                }
        }
        Apply(p, o);
        p.Set(_key, p.Vars[2], 0, 0);
    }
}

/// <summary>
/// A boiler: a squat iron tank banded in brass on four stubby legs, rivets and pipes all over it, a pressure gauge on top whose needle
/// climbs, a furnace glowing in its belly and a flared nozzle out of its front. Charging, it shudders and glows; venting, the nozzle
/// kicks back.
/// </summary>
public sealed class BoilerDesign : CreatureDesign
{
    public override string Name => "boiler";
    public override float Cell => 0.013f;
    public override float ThreeQuarter => 20f;
    public override float FloorY => -0.4f;
    public override float LifeScale => 0f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(0.1f, 0.1f, 0.1f), EyeEnergy = 0f,
        Glow = new Color(1f, 0.55f, 0.15f), GlowEnergy = 2.2f,
        Rim = new Color(0.8f, 0.72f, 0.55f), RimEnergy = 0.3f,
        DetailScale = 22f, DetailStrength = 0.8f,
        LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 0.4f, LightRange = 2.2f, LightOffset = new Vector3(0.2f, 0f, 0),
    };

    private int _body, _tank, _nozzle, _needle, _wheel;

    protected override void OnBonesBound() { _body = B("body"); _tank = B("tank"); _nozzle = B("nozzle"); _needle = B("needle"); _wheel = B("wheel"); }

    public override void Sculpt(Sculptor s)
    {
        var iron = new Color(0.22f, 0.22f, 0.26f);
        var brass = new Color(0.5f, 0.36f, 0.14f);
        var steel = new Color(0.5f, 0.5f, 0.56f);
        var amber = new Color(1f, 0.55f, 0.15f);
        int body = s.Bone("body", -1, new(0, 0, 0));
        int tank = s.Bone("tank", body, new(0, 0, 0));
        int nozzle = s.Bone("nozzle", tank, new(0.3f, 0.02f, 0));
        int needle = s.Bone("needle", tank, new(0.0f, 0.3f, 0.15f));
        int wheel = s.Bone("wheel", tank, new(-0.14f, 0.29f, 0));
        // the tank: a barrel lying along x, banded in brass
        s.Egg(tank, new(0, 0.02f, 0), new(0.36f, 0.27f, 0.28f), iron, Mat.Metal, 0.05f, bump: 0.003f);
        foreach (float x in new[] { -0.2f, -0.02f, 0.18f })
            s.Egg(tank, new(x, 0.02f, 0), new(0.025f, 0.275f, 0.285f), brass, Mat.Metal, 0.012f);
        var rng = s.Rng;
        for (int k = 0; k < 18; k++)
        {
            float a = k / 18f * Mathf.Tau, x = k % 3 == 0 ? -0.2f : k % 3 == 1 ? -0.02f : 0.18f;
            s.Ball(tank, new(x, 0.02f + 0.275f * MathF.Cos(a), 0.285f * MathF.Sin(a)), 0.013f, steel, Mat.Metal, 0.004f);
        }
        // the furnace door in its belly and a stack of pipes along its back
        s.Egg(tank, new(0.2f, -0.08f, 0.0f), new(0.08f, 0.1f, 0.22f), amber, Mat.Ember, 0.02f).Emit = 0.9f;
        s.Limb(tank, new(-0.22f, 0.22f, -0.12f), new(0.1f, 0.3f, -0.12f), 0.035f, 0.035f, steel, Mat.Metal, 0.01f);
        s.Limb(tank, new(0.1f, 0.3f, -0.12f), new(0.12f, 0.4f, -0.12f), 0.04f, 0.03f, iron, Mat.Metal, 0.01f);
        // legs: four short iron posts on broad feet
        foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                s.Limb(body, new(x, -0.15f, z), new(x, -0.36f, z), 0.06f, 0.05f, iron, Mat.Metal, 0.02f);
                s.Block(body, new(x + 0.02f, -0.38f, z), new(0.1f, 0.025f, 0.07f), 0.01f, brass, Mat.Metal, 0.01f);
            }
        // the nozzle: a thick pipe out of the front, a flared lip, a dark mouth
        s.Limb(nozzle, new(0.3f, 0.02f, 0), new(0.55f, 0.05f, 0), 0.085f, 0.075f, brass, Mat.Metal, 0.02f);
        s.Egg(nozzle, new(0.56f, 0.05f, 0), new(0.03f, 0.115f, 0.115f), steel, Mat.Metal, 0.012f);
        s.CarveBall(nozzle, new(0.6f, 0.05f, 0), 0.06f, 0.012f);
        // the gauge: a brass-rimmed dial on top with a red needle
        s.Egg(tank, new(0.0f, 0.3f, 0.15f), new(0.1f, 0.1f, 0.03f), brass, Mat.Metal, 0.015f);
        s.Egg(tank, new(0.0f, 0.3f, 0.175f), new(0.085f, 0.085f, 0.012f), new Color(0.85f, 0.82f, 0.7f), Mat.Metal, 0.008f).Emit = 0.15f;
        s.Limb(needle, new(0.0f, 0.3f, 0.18f), new(0.0f, 0.37f, 0.18f), 0.008f, 0.004f, new Color(0.85f, 0.12f, 0.08f), Mat.Metal, 0.004f).Emit = 0.5f;
        // a valve wheel
        s.Egg(wheel, new(-0.14f, 0.34f, 0), new(0.1f, 0.012f, 0.1f), brass, Mat.Metal, 0.01f);
        s.Limb(tank, new(-0.14f, 0.27f, 0), new(-0.14f, 0.34f, 0), 0.02f, 0.02f, steel, Mat.Metal, 0.008f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        var bo = a.Owner as Boiler;
        float pressure = bo?.Pressure ?? 0f;
        bool venting = bo?.Venting ?? false;
        float shake = pressure * pressure * (venting ? 1.6f : 1f);
        p.Move(_tank, new Vector3(0.006f * shake * MathF.Sin(time * 47f), 0.005f * shake * MathF.Sin(time * 53f), 0));
        p.Set(_tank, 0, 0, 1.2f * shake * MathF.Sin(time * 37f));
        p.Glow = 0.8f + 2.4f * pressure + (venting ? 0.8f : 0f);
        p.Set(_needle, 0, 0, 70f - 140f * pressure);
        p.Set(_wheel, 0, time * (30f + 300f * pressure) % 360f, 0);
        // the nozzle kicks back as it vents
        p.Move(_nozzle, new Vector3(venting ? -0.05f - 0.02f * MathF.Sin(time * 60f) : 0f, 0, 0));
        if (c == "death") { float k = W3.Smooth01(t); p.Set(_tank, 0, 0, 25f * k); p.Move(_body, new Vector3(0, -0.05f * k, 0)); p.Glow = 1f - k; }
    }
}

/// <summary>
/// A cogwheel: a toothed iron wheel with a furnace for a hub, a ring of dark spokes and a rim of brass; it turns as it rolls, and when it
/// revs it blurs in place, sparks flying off the teeth.
/// </summary>
public sealed class CogwheelDesign : CreatureDesign
{
    public override string Name => "cogwheel";
    public override float Cell => 0.012f;
    public override float ThreeQuarter => 18f;
    public override float FloorY => -0.44f;
    public override float LifeScale => 0.3f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.6f, 0.15f), EyeEnergy = 2f,
        Glow = new Color(1f, 0.55f, 0.15f), GlowEnergy = 2.4f,
        Rim = new Color(0.8f, 0.7f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 26f, DetailStrength = 0.7f,
        LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 0.4f, LightRange = 2f, LightOffset = new Vector3(0f, 0f, 0.2f),
    };

    private int _body, _wheel;
    protected override void OnBonesBound() { _body = B("body"); _wheel = B("wheel"); }

    public override void Sculpt(Sculptor s)
    {
        var iron = new Color(0.3f, 0.3f, 0.34f);
        var brass = new Color(0.52f, 0.37f, 0.14f);
        var amber = new Color(1f, 0.55f, 0.15f);
        int body = s.Bone("body", -1, new(0, 0, 0));
        int wheel = s.Bone("wheel", body, new(0, 0, 0));
        // the wheel: a toothed disc, a brass rim, a plate with spokes cut out (shown by dark bars), a glowing hub front and back
        var disc = Machine.Disc(13, 0.34f, 0.1f, 0.18f, iron, brass);
        s.Rigid(wheel, disc, Mat.Metal);
        s.Egg(wheel, new(0, 0, 0.085f), new(0.31f, 0.31f, 0.03f), brass, Mat.Metal, 0.015f);
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau;
            s.Limb(wheel, new(0.09f * MathF.Cos(a), 0.09f * MathF.Sin(a), 0.11f), new(0.27f * MathF.Cos(a), 0.27f * MathF.Sin(a), 0.11f), 0.03f, 0.03f, iron.Darkened(0.35f), Mat.Metal, 0.008f);
        }
        s.Ball(wheel, new(0, 0, 0.1f), 0.1f, amber, Mat.Ember, 0.01f).Emit = 1f;
        s.Ball(wheel, new(0, 0, -0.1f), 0.1f, amber, Mat.Ember, 0.01f).Emit = 1f;
        // a small iron frame it carries: two eye-slits on a plate in front of the hub
        s.Block(body, new(0.1f, 0.0f, 0.18f), new(0.04f, 0.1f, 0.012f), 0.01f, iron.Darkened(0.2f), Mat.Metal, 0.008f);
        s.Eye(body, new(0.11f, 0.03f, 0.2f), 0.022f, amber, 1f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        var cw = a.Owner as Cogwheel;
        bool rev = cw?.Revving ?? false;
        // it turns as far as it rolls (and spins in place when it revs)
        float rate = a.Vel.X / 0.34f * Mathf.RadToDeg(1f);
        if (rev) rate = a.Facing * 900f;
        p.Vars[2] = (p.Vars[2] - rate * a.Dt) % 360f;
        p.Set(_wheel, 0, 0, p.Vars[2]);
        p.Glow = 0.9f + (rev ? 1.8f : 0.2f * MathF.Sin(time * 3f));
        if (rev) p.Move(_body, new Vector3(0.006f * MathF.Sin(time * 70f), 0, 0));
        switch (c)
        {
            case "hurt": p.Set(_body, 0, 0, 12f * Key(t, (0, 0), (0.2f, 1), (1, 0))); break;
            case "death": { float k = W3.Smooth01(t); p.Set(_body, 0, 0, 20f * k); p.Glow = 1f - k; break; }
        }
    }
}
