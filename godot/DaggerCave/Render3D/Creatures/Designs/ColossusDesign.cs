using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Cavern Colossus: a walking cathedral of stone. A domed carapace of fitted slabs hung with
/// moss and dripping stalactites, a heart of living crystal burning through a rent in its flank,
/// a head like a battering ram crowned with horns and slit by one blazing visor, arms that end
/// in stone talons, and four pillar legs. When enraged its heart burns red. (The Rime and
/// Molten colossi are the same beast in another stone: the gameplay tint.)
/// </summary>
public sealed class ColossusDesign : CreatureDesign
{
    public override string Name => "colossus";
    public override float LifeScale => 0.5f;
    public override float Cell => 0.042f;
    public override float ThreeQuarter => 20f;
    public override float FloorY => Floor;
    private const float Floor = -1.875f;

    private static readonly Color CoreCalm = new(0.4f, 0.92f, 1f), CoreRage = new(1f, 0.35f, 0.12f);

    public override CreatureLook Look => new()
    {
        Eye = CoreCalm, EyeEnergy = 3.5f,
        Glow = CoreCalm, GlowEnergy = 2.4f,
        Rim = new Color(0.6f, 0.72f, 0.8f), RimEnergy = 0.2f,
        DetailScale = 10f, DetailStrength = 0.7f,
        LightColor = CoreCalm, LightEnergy = 2f, LightRange = 7f, LightOffset = new Vector3(0.2f, -0.6f, 0f),
    };

    private int _body, _front, _neck, _head, _jaw, _core, _tail0, _tail1;
    private readonly int[] _arm = new int[2], _fore = new int[2], _claw = new int[2];
    private readonly int[] _midT = new int[2], _midS = new int[2], _hindT = new int[2], _hindS = new int[2];

    protected override void OnBonesBound()
    {
        _body = B("body"); _front = B("front"); _neck = B("neck"); _head = B("head"); _jaw = B("jaw"); _core = B("core");
        _tail0 = B("tail0"); _tail1 = B("tail1");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            _arm[k] = B("arm" + s); _fore[k] = B("fore" + s); _claw[k] = B("claw" + s);
            _midT[k] = B("midt" + s); _midS[k] = B("mids" + s); _hindT[k] = B("hindt" + s); _hindS[k] = B("hinds" + s);
        }
    }

    public override void Sculpt(Sculptor s)
    {
        var stone = new Color(0.22f, 0.21f, 0.2f);
        var slab = new Color(0.28f, 0.27f, 0.25f);
        var dark = new Color(0.12f, 0.115f, 0.11f);
        var moss = new Color(0.17f, 0.26f, 0.08f);
        var crystal = new Color(0.55f, 0.9f, 1f);
        var rng = s.Rng;
        int body = s.Bone("body", -1, new(0, -0.4f, 0));
        int front = s.Bone("front", body, new(0.9f, -0.3f, 0));
        int neck = s.Bone("neck", front, new(1.45f, -0.15f, 0));
        int head = s.Bone("head", neck, new(1.95f, -0.1f, 0));
        int jaw = s.Bone("jaw", head, new(2.05f, -0.4f, 0));
        int core = s.Bone("core", body, new(0.15f, -0.55f, 0));
        int tail0 = s.Bone("tail0", body, new(-1.7f, -0.55f, 0));
        int tail1 = s.Bone("tail1", tail0, new(-2.3f, -0.8f, 0));

        // the heart: a great crystal burning inside, seen through rents in both flanks
        var cm = new MeshBuilder();
        for (int k = 0; k < 9; k++)
        {
            float a = k / 9f * Mathf.Tau;
            var dir = new Vector3(MathF.Cos(a) * 0.6f, 0.4f + 0.5f * (float)rng.NextDouble(), MathF.Sin(a)).Normalized();
            DesignKit.CrystalAt(cm, new Vector3(0.15f, -0.65f, 0) - dir * 0.15f, dir, 0.14f + 0.06f * (float)rng.NextDouble(), 0.75f + 0.3f * (float)rng.NextDouble(), crystal, a);
        }
        s.Rigid(core, cm, Mat.Crystal, 1.3f);
        s.Ball(core, new(0.15f, -0.6f, 0), 0.45f, crystal.Darkened(0.3f), Mat.Crystal, 0.1f).Emit = 0.9f;

        // the carapace: a dome of slabs over a heavy trunk, hollowed at the heart
        s.Egg(body, new(-0.1f, -0.2f, 0), new(1.75f, 1.05f, 1.25f), stone, Mat.Rock, 0.12f, bump: 0.05f);
        s.Egg(front, new(1.0f, -0.25f, 0), new(0.8f, 0.8f, 1.05f), stone, Mat.Rock, 0.12f, bump: 0.05f);
        for (int k = 0; k < 16; k++)
        {
            // slabs laid over the dome like armour plates, each tilted to the curve
            float u = (k % 8) / 7f, row = k / 8;
            float x = -1.4f + u * 2.6f;
            float lat = row == 0 ? 0.35f : 0.95f;
            foreach (int sd in new[] { 1, -1 })
            {
                float y = 0.45f + 0.45f * MathF.Cos((x + 0.1f) / 1.75f * 1.3f) - (row == 0 ? 0f : 0.45f);
                var at = new Vector3(x, y, lat * sd);
                var tilt = new Vector3(row == 0 ? 25f * sd : 55f * sd, 0, -(x + 0.1f) * 18f);
                s.Block(x > 0.5f ? front : body, at, new(0.28f + 0.06f * (float)rng.NextDouble(), 0.07f, 0.34f), 0.04f, (k + sd) % 3 == 0 ? slab : stone, Mat.Rock, 0.05f, tilt, bump: 0.02f);
            }
        }
        // a ridge of broken spines along the top
        for (int k = 0; k < 7; k++)
        {
            float x = -1.2f + k * 0.38f;
            var at = new Vector3(x, 0.72f + 0.12f * MathF.Cos(x * 0.9f), 0);
            s.Horn(x > 0.4f ? front : body, at - new Vector3(0, 0.2f, 0), at + new Vector3(-0.15f, 0.28f + 0.12f * (float)rng.NextDouble(), 0), 0.12f, dark, Mat.Rock, Vector3.Left, 0.05f, 6, 3);
        }
        // the rent in each flank where the heart shows, ringed with crystal growth
        foreach (int sd in new[] { 1, -1 })
        {
            s.CarveBall(body, new(0.15f, -0.6f, 1.12f * sd), 0.5f, 0.12f);
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.Tau;
                var rim = new Vector3(0.15f + MathF.Cos(a) * 0.52f, -0.6f + MathF.Sin(a) * 0.46f, 1.02f * sd);
                var mb = new MeshBuilder();
                DesignKit.CrystalAt(mb, rim, new Vector3(MathF.Cos(a) * 0.6f, MathF.Sin(a) * 0.6f, 0.7f * sd), 0.05f, 0.22f + 0.1f * (float)rng.NextDouble(), crystal.Darkened(0.2f), a);
                s.Rigid(body, mb, Mat.Crystal, 0.6f);
            }
        }
        // moss on the shoulders, stone drips under the rim
        s.Egg(front, new(0.9f, 0.45f, 0.5f), new(0.5f, 0.12f, 0.35f), moss, Mat.Fur, 0.1f, bump: 0.04f);
        s.Egg(body, new(-0.6f, 0.75f, -0.3f), new(0.45f, 0.1f, 0.3f), moss, Mat.Fur, 0.1f, bump: 0.04f);
        for (int k = 0; k < 12; k++)
        {
            float x = -1.5f + k * 0.26f;
            float sd = k % 2 == 0 ? 1 : -1;
            var at = new Vector3(x, -0.55f - 0.1f * MathF.Abs(x) * 0.3f, 1.12f * sd);
            s.Horn(x > 0.5f ? front : body, at, at + new Vector3(0, -0.2f - 0.15f * (float)rng.NextDouble(), 0), 0.05f, dark, Mat.Rock, sides: 5, rings: 3);
        }

        // the head: a battering ram with swept horns and one blazing visor slit
        s.Limb(neck, new(1.3f, -0.2f, 0), new(1.95f, -0.12f, 0), 0.45f, 0.4f, stone, Mat.Rock, 0.1f, bump: 0.04f);
        s.Block(head, new(2.2f, -0.12f, 0), new(0.42f, 0.34f, 0.38f), 0.12f, slab, Mat.Rock, 0.08f, new Vector3(0, 0, -6f), bump: 0.03f);
        s.Block(head, new(2.52f, -0.2f, 0), new(0.14f, 0.26f, 0.3f), 0.08f, dark, Mat.Rock, 0.05f, bump: 0.02f);
        var visor = new MeshBuilder();
        var vs = new MeshBuilder();
        DecorMeshes.AddSphere(vs, Vector3.Zero, 1f, new Color(0.5f, 0.9f, 1f), 8);
        visor.Append(vs, new Transform3D(Basis.FromScale(new Vector3(0.08f, 0.055f, 0.3f)), new Vector3(2.58f, -0.02f, 0)));
        s.Rigid(head, visor, Mat.Eye, 1f);
        s.Limb(head, new(2.45f, 0.1f, -0.32f), new(2.45f, 0.1f, 0.32f), 0.1f, 0.1f, dark, Mat.Rock, 0.06f);
        foreach (int sd in new[] { 1, -1 })
        {
            s.Horn(head, new(2.1f, 0.12f, 0.28f * sd), new(2.55f, 0.75f, 0.5f * sd), 0.13f, dark, Mat.Bone, new Vector3(1, 0, 0), 0.12f, 7, 5);
            s.Horn(head, new(1.95f, 0.1f, 0.22f * sd), new(1.7f, 0.65f, 0.35f * sd), 0.09f, dark, Mat.Bone, Vector3.Left, 0.08f, 6, 4);
        }
        s.Block(jaw, new(2.3f, -0.45f, 0), new(0.34f, 0.1f, 0.3f), 0.05f, stone, Mat.Rock, 0.04f, bump: 0.02f);
        DesignKit.Teeth(s, jaw, new(2.1f, -0.36f, 0.25f), new(2.6f, -0.36f, 0.2f), Vector3.Up, 5, 0.12f, 0.035f, dark, 0.3f);
        DesignKit.Teeth(s, jaw, new(2.1f, -0.36f, -0.25f), new(2.6f, -0.36f, -0.2f), Vector3.Up, 5, 0.12f, 0.035f, dark, 0.3f);

        // arms and legs: pillars of stone; the arms end in three talons each
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            var S = new Vector3(1.15f, -0.35f, 0.85f * z);
            var E = new Vector3(1.6f, -0.95f, 1.05f * z);
            var W = new Vector3(2.0f, -1.55f, 0.95f * z);
            int arm = s.Bone("arm" + sfx, front, S);
            int fore = s.Bone("fore" + sfx, arm, E);
            int claw = s.Bone("claw" + sfx, fore, W);
            s.Limb(arm, S, E, 0.42f, 0.34f, stone, Mat.Rock, 0.08f, bump: 0.04f);
            s.Ball(fore, E, 0.36f, dark, Mat.Rock, 0.06f, bump: 0.03f);
            s.Limb(fore, E, W, 0.34f, 0.3f, slab, Mat.Rock, 0.07f, bump: 0.04f);
            s.Block(claw, W + new Vector3(0.08f, -0.08f, 0), new(0.3f, 0.2f, 0.28f), 0.08f, stone, Mat.Rock, 0.05f, bump: 0.03f);
            for (int f = 0; f < 3; f++)
            {
                var root = W + new Vector3(0.25f, -0.12f, (f - 1) * 0.18f);
                s.Horn(claw, root, root + new Vector3(0.45f, -0.18f, (f - 1) * 0.08f), 0.09f, dark, Mat.Bone, Vector3.Up, 0.1f, 6, 4);
            }
            s.Horn(fore, E + new Vector3(-0.1f, 0.15f, 0.2f * z), E + new Vector3(-0.45f, 0.5f, 0.35f * z), 0.1f, dark, Mat.Bone, Vector3.Up, 0.06f, 6, 3);

            foreach (var (tn, sn, hip, knee, foot) in new[]
            {
                ("midt", "mids", new Vector3(0.35f, -0.85f, 0.95f * z), new Vector3(0.6f, -1.25f, 1.2f * z), new Vector3(0.4f, Floor + 0.12f, 1.15f * z)),
                ("hindt", "hinds", new Vector3(-0.95f, -0.85f, 0.9f * z), new Vector3(-0.7f, -1.25f, 1.15f * z), new Vector3(-0.9f, Floor + 0.12f, 1.1f * z)),
            })
            {
                int th = s.Bone(tn + sfx, body, hip);
                int sh = s.Bone(sn + sfx, th, knee);
                s.Limb(th, hip, knee, 0.4f, 0.34f, stone, Mat.Rock, 0.08f, bump: 0.04f);
                s.Limb(sh, knee, foot, 0.32f, 0.36f, slab, Mat.Rock, 0.07f, bump: 0.04f);
                s.Block(sh, foot + new Vector3(0.05f, -0.03f, 0), new(0.36f, 0.12f, 0.3f), 0.06f, dark, Mat.Rock, 0.05f, bump: 0.02f);
            }
        }
        // a stub of a tail
        s.Limb(tail0, new(-1.6f, -0.5f, 0), new(-2.3f, -0.8f, 0), 0.45f, 0.3f, stone, Mat.Rock, 0.1f, bump: 0.04f);
        s.Limb(tail1, new(-2.3f, -0.8f, 0), new(-2.75f, -1.1f, 0), 0.3f, 0.12f, dark, Mat.Rock, 0.08f, bump: 0.03f);
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T, time = a.Time;
        bool charge = c == "charge";
        float speed = MathF.Abs(a.Vel.X);
        p.Vars[0] = (p.Vars[0] + speed / (charge ? 5f : 3.2f) * a.Dt) % 1f;
        float ph = p.Vars[0];
        float walk = Math.Max(W3.SmoothStep(0.2f, 1.5f, speed), c == "walk" || charge ? 1f : 0f);
        float breathe = MathF.Sin(time * 1.2f);
        float headUp = 2f * breathe, jaw = 4f + 3f * breathe, neck = 0f, pitch = 0f, drop = 0f, armFwd = 0f, armUp = 0f, legBend = 0f, roll = 0f;
        float glow = 1f + 0.25f * MathF.Sin(time * 3f);
        float stride = charge ? 26f : 16f;
        switch (c)
        {
            case "crouch": { float k = W3.Smooth01(t); drop = 0.35f * k; legBend = 35f * k; neck = -15f * k; armUp = -10f * k; glow = 1f + k; break; }
            case "leap": legBend = 45f; armUp = 30f; armFwd = 20f; neck = 10f; pitch = 6f; break;
            case "land":
                {
                    float k = Key(t, (0, 1), (0.3f, 0.6f), (1, 0));
                    drop = 0.4f * k; legBend = 40f * k; armUp = -25f * k; armFwd = 15f * k; neck = -10f * k; glow = 1f + 2f * k;
                    break;
                }
            case "roar":
                {
                    float k = Key(t, (0, 0), (0.2f, 1), (0.85f, 1), (1, 0));
                    neck = 22f * k; headUp = 25f * k; jaw = 40f * k; armUp = 25f * k; armFwd = -15f * k; pitch = 8f * k;
                    glow = 1f + 2.5f * k + 0.5f * MathF.Sin(time * 25f) * k;
                    break;
                }
            case "charge_windup":
                {
                    float k = W3.Smooth01(t);
                    neck = -30f * k; headUp = -10f * k; drop = 0.2f * k; legBend = 20f * k; armFwd = -20f * k; glow = 1f + 1.5f * k;
                    break;
                }
            case "charge": neck = -32f; headUp = -12f; jaw = 18f; glow = 2f; break;
            case "stunned":
                {
                    // slumped, head lolling, the heart guttering
                    neck = -25f + 6f * MathF.Sin(time * 2.1f); headUp = -8f; jaw = 25f + 5f * MathF.Sin(time * 1.3f);
                    drop = 0.3f; legBend = 25f; roll = 4f * MathF.Sin(time * 1.7f);
                    glow = 0.35f + 0.25f * Math.Max(0, MathF.Sin(time * 9f));
                    break;
                }
            case "hurt": { float k = Key(t, (0, 0), (0.2f, 1), (1, 0)); neck = 12f * k; jaw = 25f * k; pitch = 4f * k; glow = 1f + k; break; }
            case "death":
                {
                    float k = W3.Smooth01(t);
                    drop = 0.9f * W3.SmoothStep(0.1f, 0.7f, t); legBend = 60f * k; neck = -35f * k; jaw = 35f * k; roll = 10f * k; armUp = -15f * k;
                    glow = 1f - 0.9f * W3.SmoothStep(0.3f, 1f, t);
                    break;
                }
        }
        p.Glow = glow;
        float sn = MathF.Sin(ph * Mathf.Tau);
        p.Set(_body, roll, 0, pitch + 1.5f * sn * walk);
        p.Move(_body, new Vector3(0, -drop + 0.05f * MathF.Abs(sn) * walk, 0));
        p.Set(_front, 0, 2f * sn * walk, -pitch * 0.3f);
        p.Set(_neck, 0, 3f * MathF.Sin(time * 0.5f), neck + 1.5f * breathe);
        p.Set(_head, 0, 0, headUp);
        p.Set(_jaw, 0, 0, -jaw);
        p.Set(_tail0, 0, 8f * MathF.Sin(time * 0.8f) + 6f * sn * walk, 3f * breathe);
        p.Set(_tail1, 0, 10f * MathF.Sin(time * 0.8f - 0.7f), 0);
        p.Grow(_core, 1f + 0.04f * MathF.Sin(time * (glow > 1.5f ? 9f : 3f)));
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            // tripod gait: right arm, left middle and right hind leg move together
            float Leg(float off) => MathF.Sin((ph + off) * Mathf.Tau);
            float Lift(float off) => Math.Max(0, MathF.Cos((ph + off) * Mathf.Tau));
            float oA = k == 0 ? 0f : 0.5f, oM = k == 0 ? 0.5f : 0f, oH = k == 0 ? 0f : 0.5f;
            p.Set(_arm[k], 0, -8f * z, stride * Leg(oA) * walk + armUp + armFwd * 0.5f);
            p.Set(_fore[k], 0, 0, -30f * Lift(oA) * walk - armUp * 0.6f + armFwd * 0.5f);
            p.Set(_claw[k], 0, 0, 20f * Lift(oA) * walk - armFwd * 0.3f);
            p.Set(_midT[k], 0, 0, stride * Leg(oM) * walk + legBend * 0.5f);
            p.Set(_midS[k], 0, 0, -25f * Lift(oM) * walk - legBend);
            p.Set(_hindT[k], 0, 0, stride * Leg(oH) * walk + legBend * 0.5f);
            p.Set(_hindS[k], 0, 0, -25f * Lift(oH) * walk - legBend);
        }
    }

    // enraged: the heart and the visor burn red
    private bool _rage;
    public override void Frame(CreatureModel m, in AnimInput a)
    {
        bool rage = (a.Owner as CavernColossus)?.Phase2 == true;
        if (rage == _rage) return;
        _rage = rage;
        var col = rage ? CoreRage : CoreCalm;
        m.Kit.Material.SetShaderParameter("glow_color", col);
        m.Kit.Material.SetShaderParameter("eye_color", col);
        if (m.Light != null) m.Light.LightColor = col;
    }
}
