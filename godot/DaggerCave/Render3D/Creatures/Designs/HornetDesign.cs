using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A hornet as long as your arm: a furred black thorax, a glossy abdomen banded amber and black
/// ending in a needle that beads venom, an orange skull-like head with serrated mandibles and
/// red compound eyes, six dangling legs and four smoky wings that buzz to a blur. Hovers, draws
/// back, then dives stinger-first.
/// </summary>
public sealed class HornetDesign : CreatureDesign
{
    public override string Name => "hornet";
    public override float Cell => 0.009f;
    public override float ThreeQuarter => 30f;

    public override CreatureLook Look => new()
    {
        Eye = new Color(1f, 0.1f, 0.05f), EyeEnergy = 2.2f,
        Glow = new Color(1f, 0.85f, 0.2f), GlowEnergy = 3f,
        Rim = new Color(1f, 0.8f, 0.5f), RimEnergy = 0.3f,
        DetailScale = 60f, DetailStrength = 0.7f,
    };

    private int _thorax, _head, _abd, _abd2, _mandR, _mandL;
    private readonly int[] _wing = new int[4];
    private readonly int[] _femur = new int[6], _tibia = new int[6];

    protected override void OnBonesBound()
    {
        _thorax = B("thorax"); _head = B("head"); _abd = B("abd"); _abd2 = B("abd2"); _mandR = B("mand_r"); _mandL = B("mand_l");
        string[] w = { "fw_r", "hw_r", "fw_l", "hw_l" };
        for (int k = 0; k < 4; k++) _wing[k] = B(w[k]);
        for (int k = 0; k < 6; k++) { _femur[k] = B($"leg{k}_f"); _tibia[k] = B($"leg{k}_t"); }
    }

    public override void Sculpt(Sculptor s)
    {
        var black = new Color(0.07f, 0.05f, 0.04f);
        var amber = new Color(0.85f, 0.55f, 0.08f);
        var orange = new Color(0.8f, 0.42f, 0.08f);
        var fuzz = new Color(0.22f, 0.14f, 0.08f);
        int thorax = s.Bone("thorax", -1, new(0.02f, 0.02f, 0));
        int head = s.Bone("head", thorax, new(0.14f, 0.03f, 0));
        int abd = s.Bone("abd", thorax, new(-0.1f, 0.0f, 0));
        int abd2 = s.Bone("abd2", abd, new(-0.28f, -0.05f, 0));

        // thorax: furred, humped
        s.Egg(thorax, new(0.02f, 0.03f, 0), new(0.11f, 0.095f, 0.085f), fuzz, Mat.Fur, 0.03f, bump: 0.004f);
        s.Egg(thorax, new(0.0f, 0.08f, 0), new(0.07f, 0.035f, 0.06f), black, Mat.Chitin, 0.02f);
        // the wasp waist and a banded, glossy abdomen tapering to the sting
        s.Limb(abd, new(-0.08f, 0.0f, 0), new(-0.13f, -0.01f, 0), 0.025f, 0.03f, black, Mat.Chitin, 0.015f);
        float[] bx = { -0.17f, -0.23f, -0.29f, -0.35f, -0.41f, -0.46f };
        float[] br = { 0.075f, 0.09f, 0.088f, 0.078f, 0.062f, 0.042f };
        for (int k = 0; k < bx.Length; k++)
        {
            int b = k < 3 ? abd : abd2;
            var c = new Vector3(bx[k], -0.02f - 0.012f * k, 0);
            s.Egg(b, c, new(0.045f, br[k], br[k] * 0.92f), k % 2 == 0 ? amber : black, Mat.Chitin, 0.012f);
        }
        s.Horn(abd2, new(-0.48f, -0.09f, 0), new(-0.6f, -0.13f, 0), 0.016f, black, Mat.Claw, Vector3.Down, 0.01f);
        s.Ball(abd2, new(-0.602f, -0.131f, 0), 0.008f, new Color(1f, 0.95f, 0.5f), Mat.Slime, 0.003f).Emit = 1.2f;

        // head: an orange, skull-like capsule, huge compound eyes, mandibles, antennae
        s.Egg(head, new(0.19f, 0.03f, 0), new(0.06f, 0.075f, 0.085f), orange, Mat.Chitin, 0.02f, bump: 0.002f);
        s.Egg(head, new(0.23f, -0.01f, 0), new(0.035f, 0.05f, 0.05f), amber, Mat.Chitin, 0.015f);
        foreach (int sd in new[] { 1, -1 })
        {
            // compound eyes: big, kidney-shaped, dark red with an inner glow
            s.Egg(head, new(0.2f, 0.04f, 0.06f * sd), new(0.035f, 0.058f, 0.028f), new Color(0.2f, 0.02f, 0.02f), Mat.Chitin, 0.008f, new Vector3(0, 20f * sd, 0)).Emit = 0.0f;
            s.Eye(head, new(0.212f, 0.045f, 0.072f * sd), 0.022f, new Color(0.25f, 0.02f, 0.02f), 0.6f);
            s.Eye(head, new(0.175f, 0.1f, 0.018f * sd), 0.009f, new Color(0.25f, 0.02f, 0.02f), 0.8f);
            // antennae: elbowed
            var a0 = new Vector3(0.235f, 0.06f, 0.02f * sd);
            var a1 = a0 + new Vector3(0.03f, 0.08f, 0.02f * sd);
            s.Horn(head, a0, a1 + new Vector3(0, 0.005f, 0), 0.006f, black, Mat.Chitin, sides: 4, rings: 2);
            s.Horn(head, a1, a1 + new Vector3(0.1f, 0.03f, 0.03f * sd), 0.005f, black, Mat.Chitin, Vector3.Down, 0.02f, 4, 4);
        }
        int mr = s.Bone("mand_r", head, new(0.24f, -0.04f, 0.025f));
        int ml = s.Bone("mand_l", head, new(0.24f, -0.04f, -0.025f));
        foreach (var (b, z) in new[] { (mr, 1f), (ml, -1f) })
        {
            s.Horn(b, new(0.24f, -0.04f, 0.028f * z), new(0.29f, -0.07f, -0.005f * z), 0.014f, orange.Darkened(0.4f), Mat.Claw, new Vector3(0, 0, -z), 0.012f, 5, 4);
            s.Horn(b, new(0.27f, -0.055f, 0.015f * z), new(0.275f, -0.075f, 0.005f * z), 0.005f, black, Mat.Claw, sides: 4, rings: 2);
        }

        // six legs dangling below, the hind pair longest
        for (int k = 0; k < 6; k++)
        {
            int pair = k / 2;
            float z = k % 2 == 0 ? 1 : -1;
            var hip = new Vector3(0.06f - pair * 0.05f, -0.04f, 0.04f * z);
            var knee = hip + new Vector3(0.02f - pair * 0.03f, -0.07f - pair * 0.01f, (0.07f + pair * 0.01f) * z);
            var foot = knee + new Vector3(-0.04f - pair * 0.04f, -0.1f - pair * 0.03f, 0.01f * z);
            int fb = s.Bone($"leg{k}_f", thorax, hip);
            int tb = s.Bone($"leg{k}_t", fb, knee);
            s.Limb(fb, hip, knee, 0.013f, 0.011f, black, Mat.Chitin, 0.006f);
            s.Limb(tb, knee, foot, 0.01f, 0.006f, pair == 2 ? amber.Darkened(0.3f) : black, Mat.Chitin, 0.005f);
            s.Horn(tb, foot, foot + new Vector3(0.012f, -0.012f, 0), 0.005f, black, Mat.Claw, sides: 4, rings: 2);
        }

        // wing roots (the wings themselves are translucent and attached in Attach)
        s.Bone("fw_r", thorax, new(0.05f, 0.1f, 0.05f));
        s.Bone("hw_r", thorax, new(0.0f, 0.09f, 0.05f));
        s.Bone("fw_l", thorax, new(0.05f, 0.1f, -0.05f));
        s.Bone("hw_l", thorax, new(0.0f, 0.09f, -0.05f));
    }

    /// <summary>Translucent smoky wings with dark veins, one per wing bone.</summary>
    public override void Attach(CreatureModel m)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.42f, 0.25f, 0.38f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.2f, Metallic = 0.1f,
            RimEnabled = true, Rim = 0.6f,
            VertexColorUseAsAlbedo = true,
        };
        string[] names = { "fw_r", "hw_r", "fw_l", "hw_l" };
        for (int k = 0; k < 4; k++)
        {
            bool fore = k % 2 == 0;
            float z = k < 2 ? 1 : -1;
            var at = m.AttachTo(names[k]);
            var mb = new MeshBuilder();
            WingMesh(mb, fore ? 0.38f : 0.26f, fore ? 0.11f : 0.08f, z);
            var mesh = mb.ToMesh(mat);
            // the attachment follows the bone's global pose; the mesh is authored at the root
            at.AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    /// <summary>A wing blade from the root outward along +/-Z, swept back, with darker veins in the vertex colour.</summary>
    private static void WingMesh(MeshBuilder mb, float len, float chord, float z)
    {
        const int n = 10;
        int start = mb.Count;
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n;
            float w = chord * MathF.Sin(MathF.PI * MathF.Pow(u, 0.7f)) + 0.012f;
            float sweep = -0.1f * u * u;
            var lead = new Vector3(sweep + w * 0.35f, 0.01f * u, u * len * z);
            var trail = new Vector3(sweep - w * 0.65f, 0.0f, u * len * z * 0.96f);
            var vein = new Color(0.35f, 0.26f, 0.15f, 0.55f);
            var clear = new Color(0.62f, 0.5f, 0.32f, 0.3f);
            mb.Add(lead, Vector3.Up, vein);
            mb.Add((lead + trail) * 0.5f, Vector3.Up, i % 3 == 0 ? vein : clear);
            mb.Add(trail, Vector3.Up, clear);
        }
        for (int i = 0; i < n; i++)
            for (int j = 0; j < 2; j++)
            {
                int a = start + i * 3 + j, b = a + 1, c = a + 3, d = c + 1;
                mb.Tri(a, b, d); mb.Tri(a, d, c);
            }
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "fly";
        float t = a.T, time = a.Time;
        float vx = MathF.Abs(a.Vel.X), vy = a.Vel.Y;
        // the body pitches with its flight path
        float pitch = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(vy, Math.Max(vx, 0.5f))) * 0.5f, -35f, 35f);
        float curl = 0f, legs = 0f, jaw = 12f * Math.Max(0, MathF.Sin(time * 7f)), roll = 0f;
        float beat = MathF.Sin(time * 95f); // a buzz: aliasing into a blur is fine
        float wingAmp = 55f, wingBias = -10f;
        switch (c)
        {
            case "aim":
                {
                    float k = W3.Smooth01(t);
                    pitch = 25f * k; curl = 55f * k; legs = 30f * k; jaw = 30f;
                    break;
                }
            case "dive":
                {
                    float ang = Mathf.RadToDeg(MathF.Atan2(vy, Math.Max(vx, 0.1f)));
                    pitch = Math.Clamp(ang, -85f, 85f);
                    curl = 70f; legs = 45f; jaw = 35f; wingAmp = 20f; wingBias = -45f;
                    break;
                }
            case "hurt":
                {
                    float k = Key(t, (0, 0), (0.2f, 1), (1, 0));
                    pitch += 25f * k; roll = 20f * k; curl = -20f * k;
                    break;
                }
            case "death":
                {
                    float k = W3.Smooth01(Math.Min(1f, t * 1.5f));
                    roll = 200f * k; pitch = -40f * k; curl = 60f * k; legs = 70f * k;
                    wingAmp = 55f * (1 - k); wingBias = Mathf.Lerp(-10f, 30f, k);
                    break;
                }
        }
        float hover = 0.025f * MathF.Sin(time * 5.5f);
        p.Root = new Vector3(0, hover, 0);
        p.Set(_thorax, roll, 0, pitch + 3f * MathF.Sin(time * 5.5f + 1f));
        p.Set(_head, 0, 6f * MathF.Sin(time * 1.7f), -pitch * 0.2f);
        // the abdomen pumps as it breathes and curls under to bring the sting forward
        float pump = 4f * MathF.Sin(time * 6f);
        p.Set(_abd, 0, 0, curl * 0.45f + pump);
        p.Set(_abd2, 0, 0, curl * 0.55f + pump * 0.5f);
        p.Set(_mandR, 0, jaw, 0);
        p.Set(_mandL, 0, -jaw, 0);
        for (int k = 0; k < 4; k++)
        {
            float z = k < 2 ? 1 : -1;
            float ang = wingBias + wingAmp * beat * (k % 2 == 0 ? 1f : 0.9f);
            // stroke about the body axis (X), swept back a little about Y
            p.Set(_wing[k], -ang * z, 12f * z, 0);
        }
        for (int k = 0; k < 6; k++)
        {
            float sway = 6f * MathF.Sin(time * 3f + k);
            p.Set(_femur[k], 0, 0, -legs * 0.5f + sway);
            p.Set(_tibia[k], 0, 0, -legs + sway * 0.5f);
        }
    }
}
