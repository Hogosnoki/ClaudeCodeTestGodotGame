using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The camp outside the cave, where every run begins and ends: a sunny meadow under a blue sky,
/// a cooking fire with the heroes sitting round it on logs, and the cave's great dark mouth in a
/// mossy cliff to the right. It is a world of its own, drawn in a viewport behind the menus (so
/// the cave's lights and fog stay out of it). The chosen hero stands up ready; the camera eases
/// from a wide view of the meadow (the main menu) in to the fire (choosing a hero), and on toward
/// the cave mouth as a run begins.
/// </summary>
public partial class CampScene : SubViewport
{
    /// <summary>The hero standing ready (the others stay sitting).</summary>
    public HeroKind Selected = HeroKind.Swordsman;
    /// <summary>Choosing a hero: the camera comes in to the fire and the names show.</summary>
    public bool Close;
    /// <summary>0..1: the camera drifting toward the cave mouth as a run begins.</summary>
    public float Depart;

    /// <summary>Whether it renders at all (off while you're down in the cave).</summary>
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            RenderTargetUpdateMode = value ? UpdateMode.Always : UpdateMode.Disabled;
            ProcessMode = value ? ProcessModeEnum.Always : ProcessModeEnum.Disabled;
        }
    }
    private bool _active = true;

    private static readonly (HeroKind kind, string design, string name, float angle)[] Roster =
    {
        // (round the far side of the fire, left to right as the choice goes; none straight
        // behind the flames from where the camera looks)
        (HeroKind.Swordsman, "swordsman", "SWORDSMAN", 172f),
        (HeroKind.Warden, "warden", "WARDEN", 128f),
        (HeroKind.Vitalist, "vitalist", "VITALIST", 52f),
        (HeroKind.Elementalist, "elementalist", "ELEMENTALIST", 8f),
    };

    private sealed class Seat
    {
        public HeroKind Kind;
        public CreatureModel Model;
        public float FootY, FireYaw, Stand, Phase;
        public Vector3 At;
        public Label3D Label;
        public Color Accent;
    }

    private readonly List<Seat> _seats = new();
    private Camera3D _cam;
    private OmniLight3D _fireLight;
    private readonly List<MeshInstance3D> _flames = new();
    private readonly List<(Node3D node, float speed)> _clouds = new();
    private float _time, _close, _depart;
    private readonly Noise3 _noise = new(4242);
    private readonly Random _rng = new(7);

    private const float SeatRadius = 1.95f, LogTop = 0.42f;
    // the cave mouth: where it stands and which way it faces (toward the camp)
    private static readonly Vector3 MouthAt = new(9.8f, 0f, -7.5f);
    private static readonly Vector3 MouthFacing = new Vector3(-0.62f, 0f, 0.78f).Normalized();

    public override void _Ready()
    {
        OwnWorld3D = true;
        Msaa3D = Msaa.Msaa4X;
        TransparentBg = false;
        ProcessMode = ProcessModeEnum.Always;
        RenderTargetUpdateMode = UpdateMode.Always;

        BuildSkyAndLight();
        BuildGround();
        BuildGrass();
        BuildTrees();
        BuildDistance();
        BuildCliff();
        BuildFire();
        BuildHeroes();

        _cam = new Camera3D { Current = true, Fov = 50f, Far = 900f };
        AddChild(_cam);
        PlaceCamera(0f);
    }

    /// <summary>Height of the meadow (flat round the camp, rolling hills further out).</summary>
    private float Ground(float x, float z)
    {
        float d = MathF.Sqrt(x * x + z * z);
        float hills = _noise.Fbm(x * 0.011f, 0.5f, z * 0.011f, 4) * 20f + _noise.Fbm(x * 0.045f, 3.3f, z * 0.045f, 3) * 3f;
        float lumps = _noise.Fbm(x * 0.35f, 9.1f, z * 0.35f, 2) * 0.07f;
        return hills * W3.SmoothStep(20f, 80f, d) + lumps;
    }

    // ================================================================== sky, sun, air

    private void BuildSkyAndLight()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.17f, 0.42f, 0.86f), SkyHorizonColor = new Color(0.64f, 0.8f, 0.96f), SkyCurve = 0.1f,
            GroundBottomColor = new Color(0.22f, 0.32f, 0.16f), GroundHorizonColor = new Color(0.6f, 0.74f, 0.6f),
            SunAngleMax = 22f, SunCurve = 0.1f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky, AmbientLightEnergy = 0.55f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.05f, TonemapWhite = 6f,
            // (no screen glow: in sunlight the heroes' armour blooms into blazes; the fire has a halo of its own)
            GlowEnabled = false,
            FogEnabled = true, FogLightColor = new Color(0.7f, 0.8f, 0.94f), FogDensity = 0.0028f, FogSkyAffect = 0f, FogAerialPerspective = 0.35f,
            AdjustmentEnabled = true, AdjustmentSaturation = 1.12f, AdjustmentContrast = 1.04f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        // afternoon sun from the upper left, warm
        var sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.95f, 0.84f), LightEnergy = 1.45f, LightSpecular = 0.1f, ShadowEnabled = true,
            DirectionalShadowMaxDistance = 60f, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
        };
        AddChild(sun);
        sun.LookAtFromPosition(new Vector3(-32f, 38f, 22f), Vector3.Zero, Vector3.Up);
    }

    // ================================================================== the meadow

    private void BuildGround()
    {
        const float half = 170f;
        const int n = 136;
        var mb = new MeshBuilder();
        float step = half * 2f / n;
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -half + i * step, z = -half + j * step;
                float h = Ground(x, z), e = 0.6f;
                var nrm = new Vector3(Ground(x - e, z) - Ground(x + e, z), 2f * e, Ground(x, z - e) - Ground(x, z + e)).Normalized();
                mb.Add(new Vector3(x, h, z), nrm, Colors.White);
            }
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                // counter-clockwise seen from above
                mb.Tri(a, c, b); mb.Tri(b, c, d);
            }
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_ground.gdshader") };
        AddChild(new MeshInstance3D { Mesh = mb.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private bool NearCliff(float x, float z) => x > 5.5f && z < -2.5f && x < 22f && z > -19f;

    /// <summary>Tufts of grass and wild flowers round the camp.</summary>
    private void BuildGrass()
    {
        // a tuft: a few blades fanning out, tips curling
        var tuft = new MeshBuilder();
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * Mathf.Tau + (float)_rng.NextDouble() * 0.6f;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            var side = new Vector3(-dir.Z, 0, dir.X);
            float h = 0.12f + 0.12f * (float)_rng.NextDouble(), lean = 0.05f + 0.07f * (float)_rng.NextDouble(), w = 0.018f;
            int b0 = tuft.Add(dir * 0.02f - side * w, Vector3.Up, Colors.White, new Vector2(0, 0));
            int b1 = tuft.Add(dir * 0.02f + side * w, Vector3.Up, Colors.White, new Vector2(1, 0));
            int m0 = tuft.Add(dir * (0.02f + lean * 0.45f) - side * w * 0.7f + Vector3.Up * h * 0.55f, Vector3.Up, Colors.White, new Vector2(0, 0.55f));
            int m1 = tuft.Add(dir * (0.02f + lean * 0.45f) + side * w * 0.7f + Vector3.Up * h * 0.55f, Vector3.Up, Colors.White, new Vector2(1, 0.55f));
            int tip = tuft.Add(dir * (0.02f + lean) + Vector3.Up * h, Vector3.Up, Colors.White, new Vector2(0.5f, 1f));
            tuft.Quad(b0, b1, m1, m0);
            tuft.Tri(m0, m1, tip);
        }
        var grassMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/grass.gdshader") };
        grassMat.SetShaderParameter("root_color", new Color(0.16f, 0.32f, 0.07f));
        grassMat.SetShaderParameter("tip_color", new Color(0.58f, 0.84f, 0.3f));
        grassMat.SetShaderParameter("wind", 0.1f);
        var xforms = new List<Transform3D>();
        for (int tries = 0; tries < 30000 && xforms.Count < 5200; tries++)
        {
            float r = 2.3f + 18f * MathF.Sqrt((float)_rng.NextDouble());
            float a = (float)_rng.NextDouble() * Mathf.Tau;
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            if (NearCliff(x, z)) continue;
            float s = 0.7f + 0.8f * (float)_rng.NextDouble();
            var basis = new Basis(Vector3.Up, (float)_rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(s, s * (0.8f + 0.5f * (float)_rng.NextDouble()), s));
            xforms.Add(new Transform3D(basis, new Vector3(x, Ground(x, z) - 0.02f, z)));
        }
        AddChild(Instances(tuft.ToMesh(grassMat), xforms, null, false));

        // flowers: a stem and a little star of petals, in cheerful colours
        var flower = new MeshBuilder();
        flower.Tube(new List<Vector3> { Vector3.Zero, new(0.01f, 0.18f, 0) }, new List<float> { 0.006f, 0.005f }, 4, new Color(0.3f, 0.55f, 0.15f), false);
        int ctr = flower.Add(new Vector3(0.01f, 0.19f, 0), Vector3.Up, Colors.White);
        for (int k = 0; k < 10; k++)
        {
            float a0 = k / 10f * Mathf.Tau, a1 = (k + 1) / 10f * Mathf.Tau;
            float r0 = k % 2 == 0 ? 0.055f : 0.025f, r1 = k % 2 == 0 ? 0.025f : 0.055f;
            int p0 = flower.Add(new Vector3(0.01f + MathF.Cos(a0) * r0, 0.19f, MathF.Sin(a0) * r0), Vector3.Up, Colors.White);
            int p1 = flower.Add(new Vector3(0.01f + MathF.Cos(a1) * r1, 0.19f, MathF.Sin(a1) * r1), Vector3.Up, Colors.White);
            flower.Tri(ctr, p1, p0); flower.Tri(ctr, p0, p1);
        }
        var petals = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.7f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var palette = new[] { new Color(1f, 0.86f, 0.2f), new Color(1f, 1f, 0.94f), new Color(1f, 0.62f, 0.76f), new Color(0.74f, 0.55f, 1f), new Color(1f, 0.55f, 0.25f) };
        var fx = new List<Transform3D>();
        var fc = new List<Color>();
        for (int tries = 0; tries < 6000 && fx.Count < 520; tries++)
        {
            float r = 2.6f + 18f * MathF.Sqrt((float)_rng.NextDouble());
            float a = (float)_rng.NextDouble() * Mathf.Tau;
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            if (NearCliff(x, z)) continue;
            // in drifts: a colour holds for a patch of meadow
            float patch = _noise.Sample(x * 0.25f, 1.7f, z * 0.25f);
            if (patch < -0.1f) continue;
            float s = 0.8f + 0.6f * (float)_rng.NextDouble();
            fx.Add(new Transform3D(new Basis(Vector3.Up, (float)_rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * s), new Vector3(x, Ground(x, z) - 0.01f, z)));
            fc.Add(palette[(int)((patch + 1f) * 3.7f + (x > 0 ? 1 : 0)) % palette.Length]);
        }
        AddChild(Instances(flower.ToMesh(petals), fx, fc, false));
    }

    private static MultiMeshInstance3D Instances(Mesh mesh, List<Transform3D> xforms, List<Color> colors, bool shadows)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colors != null, Mesh = mesh, InstanceCount = xforms.Count };
        for (int k = 0; k < xforms.Count; k++)
        {
            mm.SetInstanceTransform(k, xforms[k]);
            if (colors != null) mm.SetInstanceColor(k, colors[k]);
        }
        return new MultiMeshInstance3D { Multimesh = mm, CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
    }

    // ================================================================== trees, hills, clouds

    private void AddTree(MeshBuilder mb, Vector3 at, float height, Noise3 noise)
    {
        var bark = new Color(0.36f, 0.26f, 0.17f);
        float lean = ((float)_rng.NextDouble() - 0.5f) * 0.3f;
        var path = new List<Vector3> { at + new Vector3(0, -0.3f, 0), at + new Vector3(lean * 0.3f, height * 0.35f, 0), at + new Vector3(lean, height * 0.62f, 0.1f) };
        mb.Tube(path, new List<float> { height * 0.055f, height * 0.04f, height * 0.025f }, 7, bark, false);
        // a crown of leafy clumps, lighter on top
        var top = at + new Vector3(lean, height * 0.66f, 0.1f);
        int clumps = 5 + _rng.Next(3);
        for (int k = 0; k < clumps; k++)
        {
            var o = new Vector3(((float)_rng.NextDouble() - 0.5f) * height * 0.45f, ((float)_rng.NextDouble() - 0.2f) * height * 0.3f, ((float)_rng.NextDouble() - 0.5f) * height * 0.45f);
            float r = height * (0.2f + 0.1f * (float)_rng.NextDouble());
            var green = new Color(0.2f, 0.45f, 0.14f).Lerp(new Color(0.38f, 0.62f, 0.2f), Math.Clamp(o.Y / (height * 0.25f) * 0.5f + 0.5f, 0f, 1f) * (0.6f + 0.4f * (float)_rng.NextDouble()));
            mb.Blob(top + o, new Vector3(r, r * 0.85f, r), 6, green, noise, 0.22f, 2.2f);
        }
    }

    private void BuildTrees()
    {
        var mb = new MeshBuilder();
        var noise = new Noise3(88);
        // a few near the camp (left and behind), leaving the view of the cave clear
        var near = new (float x, float z, float h)[]
        {
            (-9.5f, -7f, 7.5f), (-13f, -2.5f, 6.2f), (-7f, -13f, 8.4f), (-16f, -11f, 7f), (-4.5f, -19f, 7.8f),
            (2f, -21f, 6.8f), (-21f, -5f, 8f), (-11f, 6f, 5.6f), (27f, -14f, 7.4f), (24f, -26f, 8.2f),
        };
        foreach (var (x, z, h) in near) AddTree(mb, new Vector3(x, Ground(x, z), z), h, noise);
        // woods on the far hills
        for (int k = 0; k < 70; k++)
        {
            float a = Mathf.DegToRad(-165f + 150f * (float)_rng.NextDouble());
            float r = 45f + 45f * (float)_rng.NextDouble();
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            AddTree(mb, new Vector3(x, Ground(x, z), z), 7f + 5f * (float)_rng.NextDouble(), noise);
        }
        mb.SmoothNormals();
        var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f };
        AddChild(new MeshInstance3D { Mesh = mb.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
    }

    private void BuildDistance()
    {
        // blue-green mountains on the horizon (the air softens them further)
        var mb = new MeshBuilder();
        var noise = new Noise3(51);
        var peaks = new (float x, float z, float w, float h)[]
        {
            (-170f, -230f, 110f, 70f), (-60f, -260f, 130f, 95f), (60f, -250f, 120f, 80f), (180f, -220f, 110f, 65f), (-250f, -150f, 90f, 55f), (260f, -140f, 90f, 50f),
        };
        foreach (var (x, z, w, h) in peaks)
            mb.Blob(new Vector3(x, -10f, z), new Vector3(w, h, w * 0.6f), 10, new Color(0.36f, 0.48f, 0.42f), noise, 0.25f, 1.6f, 0.5f);
        mb.SmoothNormals();
        AddChild(new MeshInstance3D { Mesh = mb.ToMesh(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f }), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // fair-weather clouds drifting over
        var cloudMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 1f, 1f), Roughness = 1f, EmissionEnabled = true, Emission = new Color(0.82f, 0.86f, 0.95f), EmissionEnergyMultiplier = 0.45f };
        for (int k = 0; k < 9; k++)
        {
            var cb = new MeshBuilder();
            int puffs = 4 + _rng.Next(4);
            float w = 14f + 14f * (float)_rng.NextDouble();
            for (int p = 0; p < puffs; p++)
            {
                float t = p / (float)(puffs - 1) - 0.5f;
                float r = w * (0.28f + 0.14f * (float)_rng.NextDouble()) * (1f - MathF.Abs(t) * 0.8f);
                cb.Blob(new Vector3(t * w, r * 0.3f, ((float)_rng.NextDouble() - 0.5f) * w * 0.3f), new Vector3(r, r * 0.7f, r * 0.8f), 7, Colors.White, noise, 0.15f, 2f, 0.55f);
            }
            cb.SmoothNormals();
            var node = new MeshInstance3D
            {
                Mesh = cb.ToMesh(cloudMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(-220f + 440f * (float)_rng.NextDouble(), 55f + 30f * (float)_rng.NextDouble(), -120f - 110f * (float)_rng.NextDouble()),
            };
            AddChild(node);
            _clouds.Add((node, 0.6f + 0.8f * (float)_rng.NextDouble()));
        }
    }

    // ================================================================== the cliff and the cave mouth

    private void BuildCliff()
    {
        var noise = new Noise3(19);
        var rock = new MeshBuilder();
        var right = new Vector3(MouthFacing.Z, 0, -MouthFacing.X); // along the cliff face (to the right of the mouth, seen from the camp)
        Vector3 Face(float along, float up, float back) => MouthAt + right * along + Vector3.Up * up - MouthFacing * back;

        const float halfW = 2.5f, archH = 5.2f, depth = 12f;
        // Every stone keeps out of the tunnel (it's the dark you look into), so the hill is built
        // round it: jambs and a lintel framing the mouth, masses stepping down to either side, and
        // the body of the hill above and behind. (along the face, up, back into the hill; radii)
        var stones = new (float a, float u, float b, float ra, float ru, float rb)[]
        {
            (-3.8f, 1.4f, 0.8f, 1.3f, 1.6f, 1.4f), (-3.9f, 4f, 0.9f, 1.4f, 1.4f, 1.5f), (-4.3f, 6.3f, 1.1f, 1.8f, 1.3f, 1.6f),
            (3.8f, 1.5f, 0.8f, 1.3f, 1.7f, 1.4f), (4f, 4.2f, 1f, 1.5f, 1.5f, 1.5f), (4.5f, 6.6f, 1.2f, 2f, 1.4f, 1.6f),
            (0f, 7.1f, 1f, 3.2f, 1.5f, 1.8f), (-1.5f, 9.2f, 2.2f, 3.6f, 1.8f, 2.4f), (2.2f, 9.6f, 2.6f, 3.8f, 2f, 2.6f), (0.2f, 11.4f, 4.5f, 4.5f, 1.8f, 3.5f),
            (-7f, 2.2f, 1.8f, 2.4f, 2.4f, 2.4f), (-7.6f, 5.4f, 2.8f, 2.6f, 2f, 2.6f), (-10.5f, 1.6f, 2.8f, 2.4f, 1.8f, 2.4f), (-13f, 0.8f, 3.4f, 2.2f, 1.2f, 2.2f), (-9.5f, 7.4f, 4.5f, 3f, 1.8f, 3f),
            (7.2f, 2.6f, 1.8f, 2.6f, 2.8f, 2.5f), (7.8f, 6.2f, 2.6f, 2.8f, 2.2f, 2.6f), (11f, 2.2f, 2.8f, 2.8f, 2.4f, 2.6f), (11.8f, 6f, 3.8f, 3f, 2.2f, 3f), (14.8f, 1.4f, 3.4f, 2.6f, 1.6f, 2.4f), (9.8f, 9.2f, 4.6f, 3.6f, 2.2f, 3.4f),
            (0f, 3.5f, 18f, 9f, 6f, 5.2f), (-6f, 4f, 8f, 3.2f, 4.5f, 5f), (6.2f, 4.5f, 8f, 3.3f, 5f, 5f), (0f, 9f, 9.5f, 5f, 3.2f, 4.5f),
            (-3f, 12f, 8f, 6f, 3f, 5f), (4f, 12.5f, 9f, 6.5f, 3.2f, 5.5f),
            // tumbled in front
            (-4.2f, 0.35f, -1.8f, 0.8f, 0.55f, 0.7f), (4.6f, 0.45f, -2.4f, 1.1f, 0.75f, 0.9f), (3f, 0.2f, -1.4f, 0.45f, 0.3f, 0.4f), (-6.5f, 0.3f, -1.2f, 0.6f, 0.4f, 0.5f),
        };
        foreach (var (a, u, b, ra, ru, rb) in stones)
        {
            bool clear = a + ra < -halfW - 0.2f || a - ra > halfW + 0.2f || u - ru * 0.7f > archH + 0.3f || b - rb > depth + 0.2f || b + rb < -0.3f;
            if (!clear) continue;
            float shade = 0.25f * (float)_rng.NextDouble();
            var col = new Color(0.52f, 0.47f, 0.41f).Darkened(shade).Lerp(new Color(0.55f, 0.5f, 0.38f), 0.3f * (float)_rng.NextDouble());
            rock.Blob(Face(a, u, b), new Vector3(ra, ru, rb), 8, col, noise, 0.34f, 1.7f, 0.3f);
        }
        rock = rock.Faceted();
        var rockMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_rock.gdshader") };
        rockMat.SetShaderParameter("moss", new Color(0.32f, 0.52f, 0.17f));
        AddChild(new MeshInstance3D { Mesh = rock.ToMesh(rockMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

        // the mouth: a tunnel going back into the hill, lit only at its lip and black beyond
        var tunnel = new MeshBuilder();
        const int arcN = 22, depthN = 7;
        // the arch's outline, bottom left, up, over and down to the bottom right: straight sides
        // up to the springing, then a half circle
        static Vector2 Arch(float u)
        {
            float wall = archH - halfW, round = MathF.PI * halfW;
            float s = u * (2f * wall + round);
            if (s < wall) return new Vector2(-halfW, s);
            s -= wall;
            if (s < round) { float a = MathF.PI - s / halfW; return new Vector2(MathF.Cos(a) * halfW, wall + MathF.Sin(a) * halfW); }
            return new Vector2(halfW, wall - (s - round));
        }
        for (int d = 0; d <= depthN; d++)
        {
            float back = d / (float)depthN * depth;
            float shrink = 1f - 0.18f * d / depthN;
            float dark = MathF.Pow(1f - d / (float)depthN, 2.2f);
            var col = new Color(0.16f, 0.13f, 0.11f) * dark;
            for (int k = 0; k <= arcN; k++)
            {
                var s = Arch(k / (float)arcN) * shrink;
                var p = Face(s.X, s.Y, back - 0.4f);
                var inward = (MouthAt + Vector3.Up * (archH * 0.45f) - MouthFacing * back - p).Normalized();
                tunnel.Add(p, inward, new Color(col.R, col.G, col.B), new Vector2(k / (float)arcN, d / (float)depthN));
            }
        }
        for (int d = 0; d < depthN; d++)
            for (int k = 0; k < arcN; k++)
            {
                int a = d * (arcN + 1) + k, b = a + 1, c = a + arcN + 1, e = c + 1;
                tunnel.Tri(a, b, c); tunnel.Tri(b, e, c);
            }
        // a black back wall where the tunnel turns away into the dark
        int ctr = tunnel.Add(Face(0f, archH * 0.4f, depth), MouthFacing, Colors.Black);
        int last = depthN * (arcN + 1);
        for (int k = 0; k < arcN; k++) { tunnel.Tri(ctr, last + k, last + k + 1); tunnel.Tri(ctr, last + k + 1, last + k); }
        // (and across the bottom, from the arch's last corner back to its first)
        tunnel.Tri(ctr, last + arcN, last); tunnel.Tri(ctr, last, last + arcN);
        // the floor of the mouth, disappearing into the dark
        int f0 = tunnel.Add(Face(-halfW, 0.02f, -0.6f), Vector3.Up, new Color(0.2f, 0.17f, 0.13f));
        int f1 = tunnel.Add(Face(halfW, 0.02f, -0.6f), Vector3.Up, new Color(0.2f, 0.17f, 0.13f));
        int f2 = tunnel.Add(Face(halfW * 0.8f, 0.02f, depth), Vector3.Up, Colors.Black);
        int f3 = tunnel.Add(Face(-halfW * 0.8f, 0.02f, depth), Vector3.Up, Colors.Black);
        tunnel.Tri(f0, f1, f2); tunnel.Tri(f0, f2, f3); tunnel.Tri(f0, f2, f1); tunnel.Tri(f0, f3, f2);
        var dim = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = true };
        AddChild(new MeshInstance3D { Mesh = tunnel.ToMesh(dim), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ================================================================== the camp fire

    private void BuildFire()
    {
        var noise = new Noise3(5);
        var mb = new MeshBuilder();
        // a ring of stones round a bed of ash
        mb.Blob(new Vector3(0, -0.06f, 0), new Vector3(0.72f, 0.1f, 0.72f), 8, new Color(0.18f, 0.16f, 0.15f), noise, 0.2f, 3f, 0.4f);
        for (int k = 0; k < 11; k++)
        {
            float a = k / 11f * Mathf.Tau + 0.2f;
            float r = 0.18f + 0.06f * (float)_rng.NextDouble();
            mb.Blob(new Vector3(MathF.Cos(a) * 0.82f, r * 0.45f, MathF.Sin(a) * 0.82f), new Vector3(r, r * 0.75f, r), 6,
                new Color(0.46f, 0.44f, 0.41f).Darkened(0.3f * (float)_rng.NextDouble()), noise, 0.25f, 3f, 0.4f);
        }
        // logs stacked in a cone, charred where they meet
        for (int k = 0; k < 5; k++)
        {
            float a = k / 5f * Mathf.Tau + 0.4f;
            var foot = new Vector3(MathF.Cos(a) * 0.6f, 0.04f, MathF.Sin(a) * 0.6f);
            var tip = new Vector3(MathF.Cos(a) * 0.08f, 0.5f, MathF.Sin(a) * 0.08f);
            mb.Tube(new List<Vector3> { foot, foot.Lerp(tip, 0.5f), tip }, new List<float> { 0.075f, 0.065f, 0.045f }, 6, new Color(0.3f, 0.2f, 0.12f), true);
            mb.Tube(new List<Vector3> { foot.Lerp(tip, 0.55f), tip + (tip - foot) * 0.05f }, new List<float> { 0.07f, 0.05f }, 6, new Color(0.07f, 0.05f, 0.04f), false);
        }
        // the cooking pot on its tripod, hanging over the flames
        var wood = new Color(0.4f, 0.28f, 0.16f);
        var apex = new Vector3(0.02f, 1.75f, 0f);
        for (int k = 0; k < 3; k++)
        {
            float a = k / 3f * Mathf.Tau + 1.1f;
            var foot = new Vector3(MathF.Cos(a) * 1.05f, -0.05f, MathF.Sin(a) * 1.05f);
            mb.Tube(new List<Vector3> { foot, foot.Lerp(apex, 0.5f), apex + (apex - foot) * 0.1f }, new List<float> { 0.035f, 0.03f, 0.025f }, 5, wood, true);
        }
        var iron = new Color(0.13f, 0.13f, 0.14f);
        mb.Tube(new List<Vector3> { apex, new(0.02f, 1.02f, 0f) }, new List<float> { 0.008f, 0.008f }, 4, iron, false);
        mb.Blob(new Vector3(0.02f, 0.82f, 0f), new Vector3(0.27f, 0.22f, 0.27f), 9, iron, noise, 0.02f, 4f);
        mb.Tube(new List<Vector3> { new(-0.27f, 0.97f, 0f), new(0.02f, 1.08f, 0f), new(0.31f, 0.97f, 0f) }, new List<float> { 0.012f, 0.012f, 0.012f }, 4, iron, false);
        // the stew inside, just showing at the rim
        mb.Blob(new Vector3(0.02f, 0.97f, 0f), new Vector3(0.2f, 0.035f, 0.2f), 7, new Color(0.46f, 0.28f, 0.12f), noise, 0.1f, 6f);
        // a log to sit on at each hero's place
        foreach (var r in Roster)
        {
            float a = Mathf.DegToRad(r.angle);
            var at = new Vector3(MathF.Cos(a), 0, -MathF.Sin(a)) * (SeatRadius + 0.05f);
            var along = new Vector3(MathF.Sin(a), 0, MathF.Cos(a));
            var logC = at + Vector3.Up * (LogTop - 0.19f);
            mb.Tube(new List<Vector3> { logC - along * 0.75f, logC, logC + along * 0.75f }, new List<float> { 0.2f, 0.21f, 0.19f }, 9, new Color(0.42f, 0.3f, 0.18f), true,
                (i, k) => 0.05f * noise.Sample(i * 2.1f, k * 0.7f, r.angle));
            mb.Tube(new List<Vector3> { logC + along * 0.75f, logC - along * 0.75f }, new List<float> { 0.19f, 0.2f }, 9, new Color(0.62f, 0.5f, 0.34f), true);
        }
        mb.SmoothNormals();
        AddChild(new MeshInstance3D { Mesh = mb.ToMesh(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f }), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

        // the flames
        var flameMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_fire.gdshader") };
        var quad = new QuadMesh { Size = new Vector2(1f, 1f), CenterOffset = new Vector3(0, 0.5f, 0) };
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau;
            float r = k == 0 ? 0f : 0.2f;
            var f = new MeshInstance3D
            {
                Mesh = quad, MaterialOverride = flameMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(MathF.Cos(a) * r, 0.05f, MathF.Sin(a) * r),
                Scale = new Vector3(k == 0 ? 0.75f : 0.5f, k == 0 ? 1.15f : 0.7f + 0.2f * (float)_rng.NextDouble(), 1f),
            };
            f.SetInstanceShaderParameter("seed", (float)_rng.NextDouble() * 10f);
            f.SetInstanceShaderParameter("energy", k == 0 ? 1.6f : 1.25f);
            AddChild(f);
            _flames.Add(f);
        }
        // a soft warm halo round the flames (the camp has no screen glow)
        var halo = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2.6f, 2.6f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.55f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Puff, AlbedoColor = new Color(1f, 0.55f, 0.2f, 0.32f), NoDepthTest = false, DisableFog = true,
            },
        };
        AddChild(halo);
        _fireLight = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.28f), LightEnergy = 1.1f, LightSpecular = 0f, OmniRange = 5.5f, Position = new Vector3(0, 0.7f, 0), ShadowEnabled = false };
        AddChild(_fireLight);

        // sparks, smoke and the steam off the pot
        AddChild(Particles(new Vector3(0, 0.35f, 0), 24, 1.8f, new Color(1f, 0.62f, 0.2f), 0.035f, 0.035f, 1.1f, 0.22f, true, 0.4f));
        AddChild(Particles(new Vector3(0, 1.1f, 0), 18, 5.5f, new Color(0.5f, 0.5f, 0.52f, 0.2f), 0.35f, 1.6f, 0.45f, 0.2f, false, 0.25f));
        AddChild(Particles(new Vector3(0.02f, 1.0f, 0f), 8, 2.4f, new Color(0.95f, 0.95f, 0.95f, 0.16f), 0.12f, 0.45f, 0.35f, 0.1f, false, 0.15f));
    }

    private GradientTexture2D _puff;
    private GradientTexture2D Puff => _puff ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 0.4f, 1f } },
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 64, Height = 64,
    };

    /// <summary>Rising particles: sparks (additive, bright) or smoke and steam (soft, spreading as they rise).</summary>
    private GpuParticles3D Particles(Vector3 at, int amount, float life, Color col, float size0, float size1, float speed, float radius, bool glow, float drift)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = glow ? 18f : 10f, InitialVelocityMin = speed * 0.6f, InitialVelocityMax = speed,
            Gravity = new Vector3(drift, glow ? 0.25f : 0.05f, 0f), EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius,
            DampingMin = glow ? 0.2f : 0.1f, DampingMax = glow ? 0.6f : 0.2f,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = glow ? 1.4f : 0.6f, TurbulenceNoiseScale = 2.5f,
            ScaleMin = 1f, ScaleMax = 1f,
            ScaleCurve = new CurveTexture { Curve = new Curve { PointCount = 0 } },
            ColorRamp = new GradientTexture1D { Gradient = new Gradient { Colors = new[] { new Color(col, 0f), col, new Color(col, 0f) }, Offsets = new[] { 0f, 0.15f, 1f } } },
        };
        var curve = ((CurveTexture)pm.ScaleCurve).Curve;
        curve.AddPoint(new Vector2(0f, size0 / Math.Max(size0, size1)));
        curve.AddPoint(new Vector2(1f, size1 / Math.Max(size0, size1)));
        var mat = new StandardMaterial3D
        {
            ShadingMode = glow ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Puff,
            BlendMode = glow ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            EmissionEnabled = glow, Emission = col, EmissionEnergyMultiplier = glow ? 1.6f : 0f,
        };
        float big = Math.Max(size0, size1);
        return new GpuParticles3D
        {
            Amount = amount, Lifetime = life, Position = at, ProcessMaterial = pm, Preprocess = life,
            DrawPass1 = new QuadMesh { Size = new Vector2(big, big), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 12, 8)),
        };
    }

    // ================================================================== the heroes

    private void BuildHeroes()
    {
        foreach (var (kind, design, name, angle) in Roster)
        {
            var model = CreatureModel.Create(design);
            if (model == null) continue;
            AddChild(model);
            // no lanterns lit in broad daylight (they'd blaze across the camp); and in the sun a
            // hero casts a shadow like everything else
            foreach (var light in model.FindChildren("*", nameof(OmniLight3D), true, false)) ((OmniLight3D)light).Visible = false;
            model.Body.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            float a = Mathf.DegToRad(angle);
            var at = new Vector3(MathF.Cos(a), 0, -MathF.Sin(a)) * SeatRadius;
            var b = model.Kit.Sculpt.Bounds;
            float footY = float.IsNaN(model.Design.FloorY) ? b.Position.Y : model.Design.FloorY;
            // face the fire: at yaw 0 a hero looks along +x
            float fireYaw = Mathf.RadToDeg(MathF.Atan2(at.Z, -at.X));
            var accent = Hud.HeroColor(kind);
            var label = new Label3D
            {
                Text = name, Modulate = accent.Lightened(0.35f), OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.9f),
                FontSize = 48, PixelSize = 0.005f, OutlineSize = 16, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Position = at + new Vector3(0, 2.35f, 0), Visible = false, RenderPriority = 2, OutlineRenderPriority = 1,
            };
            AddChild(label);
            _seats.Add(new Seat { Kind = kind, Model = model, FootY = footY, FireYaw = fireYaw, At = at, Label = label, Accent = accent, Phase = (float)_rng.NextDouble() * 10f });
        }
    }

    // ================================================================== every frame

    public override void _Process(double delta)
    {
        float dt = (float)Math.Min(delta, 0.1);
        _time += dt;
        _close = Mathf.MoveToward(_close, Close ? 1f : 0f, dt * 1.3f);
        _depart = Depart;
        PlaceCamera(dt);

        // the fire breathes
        float flick = 0.82f + 0.12f * MathF.Sin(_time * 11f) * MathF.Sin(_time * 4.3f + 1f) + 0.06f * MathF.Sin(_time * 23f);
        _fireLight.LightEnergy = 1.1f * flick;

        foreach (var (node, speed) in _clouds)
        {
            var p = node.Position;
            p.X += speed * dt;
            if (p.X > 260f) p.X = -260f;
            node.Position = p;
        }

        foreach (var s in _seats)
        {
            bool up = s.Kind == Selected;
            s.Stand = Mathf.MoveToward(s.Stand, up ? 1f : 0f, dt * 2.2f);
            var a = new AnimInput
            {
                Clip = "sit", T = s.Stand, Frame = 0, Frames = 1, Loop = true,
                Time = _time + s.Phase, Dt = dt, Facing = 1, OnFloor = true, ClipTime = _time,
            };
            s.Model.Face(1, "sit", s.Stand, dt);
            s.Model.Animate(a);
            // the lantern's glass only glints in daylight (lit up, it blazes out the whole camp)
            s.Model.Body.SetInstanceShaderParameter("glow_boost", 0.1f);
            s.Model.UpdatePivot(Vector2.One, 0f, -s.FootY, false);
            // standing, the chosen hero turns from the fire toward you
            var toCam = _cam.GlobalPosition - s.At;
            float camYaw = Mathf.RadToDeg(MathF.Atan2(-toCam.Z, toCam.X));
            float yaw = Mathf.LerpAngle(Mathf.DegToRad(s.FireYaw), Mathf.DegToRad(camYaw), 0.55f * W3.Smooth01(s.Stand));
            var r = s.Model.Pivot.RotationDegrees;
            s.Model.Pivot.RotationDegrees = new Vector3(r.X, Mathf.RadToDeg(yaw), r.Z);
            s.Model.Position = s.At + new Vector3(0, -s.FootY, 0);
            // the chosen hero's name over them as they stand
            float la = Math.Clamp((_close - 0.6f) / 0.4f, 0f, 1f) * W3.Smooth01(s.Stand);
            s.Label.Visible = la > 0.01f && _depart <= 0f;
            s.Label.Modulate = s.Accent.Lightened(0.45f) with { A = la };
            s.Label.OutlineModulate = s.Label.OutlineModulate with { A = 0.9f * la };
            s.Label.Position = s.At + new Vector3(0, 2.3f, 0);
        }
    }

    private void PlaceCamera(float dt)
    {
        // wide over the meadow (the menu), in by the fire (choosing), on toward the cave (leaving)
        var widePos = new Vector3(-6.5f, 3.3f, 12.5f);
        var wideLook = new Vector3(3.2f, 1.8f, -3.5f);
        var closePos = new Vector3(0.1f, 1.95f, 5.1f);
        var closeLook = new Vector3(0.5f, 0.85f, -0.9f);
        float k = W3.Smooth01(_close);
        var pos = widePos.Lerp(closePos, k);
        var look = wideLook.Lerp(closeLook, k);
        if (_depart > 0f)
        {
            float d = W3.Smooth01(_depart);
            // (rising on the way, over the heads of the ones staying behind)
            pos = pos.Lerp(MouthAt + MouthFacing * 4.5f + new Vector3(0, 2.2f, 0), d) + Vector3.Up * (1.6f * MathF.Sin(d * MathF.PI));
            look = look.Lerp(MouthAt + Vector3.Up * 2.2f - MouthFacing * 4f, d);
        }
        // a slow drift, like someone looking about
        pos += new Vector3(MathF.Sin(_time * 0.21f) * 0.25f, MathF.Sin(_time * 0.17f + 1f) * 0.08f, 0f);
        _cam.Position = pos;
        _cam.LookAt(look, Vector3.Up);
    }
}
