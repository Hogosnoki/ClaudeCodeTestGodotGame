using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The camp outside the cave, where every run begins and ends: a meadow at dusk under a violet and
/// peach sky, the sun going down beyond misty mountains, a cooking fire with the heroes sitting round
/// it on logs, and the cave's great dark mouth in a towering cliff to the right (CampDusk.cs builds
/// the world round them, CampCliff.cs the cliff). It is a world of its own, drawn in a viewport behind the menus (so
/// the cave's lights and fog stay out of it). The chosen hero stands up ready; the camera eases
/// from a wide view of the meadow (the main menu) in to the fire (choosing a hero), and on toward
/// the cave mouth as a run begins.
/// </summary>
public partial class CampScene : SubViewport
{
    /// <summary>The camp, while there is one.</summary>
    public static CampScene I;
    private Godot.Environment _env;
    // (the dusk sky's light on the land: lavender, from everywhere)
    private const float AmbientBase = 0.9f;
    private static readonly Color AmbientColor = new(0.52f, 0.48f, 0.58f);

    public override void _EnterTree() => I = this;
    public override void _ExitTree() { if (I == this) I = null; }

    /// <summary>The camp keeps its own dusk light whatever the cave's lighting setting (the sky's violet and the sun and the fire are the scene).</summary>
    public void ApplyLighting()
    {
        if (_env == null) return;
        _env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        _env.AmbientLightColor = AmbientColor;
        _env.AmbientLightEnergy = AmbientBase;
    }

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
            if (_ink != null) _ink.Active = value;
        }
    }
    private bool _active = true;

    private static readonly (HeroKind kind, string design, string name, float angle)[] Roster =
    {
        // (round the fire, left to right as the choice goes; none straight behind the flames
        // from where the camera looks, and none hiding another from it)
        (HeroKind.Swordsman, "swordsman", "SWORDSMAN", 204f),
        (HeroKind.Warden, "warden", "WARDEN", 148f),
        (HeroKind.Vitalist, "vitalist", "VITALIST", 118f),
        (HeroKind.Elementalist, "elementalist", "ELEMENTALIST", 64f),
        (HeroKind.Rogue, "rogue", "ROGUE", 34f),
        (HeroKind.Aegis, "aegis", "AEGIS", 6f),
        (HeroKind.ShapeShifter, "shapeshifter", "SHAPE SHIFTER", -24f),
        // (the Automaton, last in the choice and so next round from the Swordsman, sits at the front left)
        (HeroKind.Automaton, "automaton_hero", "AUTOMATON", 242f),
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
    private InkOutline _ink;
    private OmniLight3D _fireLight;
    private readonly List<MeshInstance3D> _flames = new();
    private float _time, _close, _depart;
    private readonly Noise3 _noise = new(4242);
    private readonly Random _rng = new(7);

    private const float SeatRadius = 2.3f, LogTop = 0.42f, LogHalf = 0.6f;
    private const float FireEnergy = 4.2f;
    // the cave mouth: where it stands and which way it faces (toward the camp)
    private static readonly Vector3 MouthAt = new(13f, 0f, -14.5f);
    private static readonly Vector3 MouthFacing = new Vector3(-0.47f, 0f, 0.88f).Normalized();

    public override void _Ready()
    {
        OwnWorld3D = true;
        Msaa3D = Msaa.Msaa4X;
        TransparentBg = false;
        ProcessMode = ProcessModeEnum.Always;
        RenderTargetUpdateMode = UpdateMode.Always;

        BuildDuskSky();
        BuildLand();
        BuildCliff();
        BuildMeadow();
        BuildMeadowRocks();
        BuildOak();
        BuildMist();
        BuildFire();
        BuildHeroes();

        _cam = new Camera3D { Current = true, Fov = 56f, Far = 4000f, Near = 0.08f };
        AddChild(_cam);
        // (the heroes keep their ink line, as everywhere in the game; the world out here has none: it is drawn as it is)
        _ink = new InkOutline(_cam);
        AddChild(_ink);
        PlaceCamera(0f);
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
            mb.Tube(new List<Vector3> { logC - along * LogHalf, logC, logC + along * LogHalf }, new List<float> { 0.2f, 0.21f, 0.19f }, 9, new Color(0.42f, 0.3f, 0.18f), true,
                (i, k) => 0.05f * noise.Sample(i * 2.1f, k * 0.7f, r.angle));
            mb.Tube(new List<Vector3> { logC + along * LogHalf, logC - along * LogHalf }, new List<float> { 0.19f, 0.2f }, 9, new Color(0.62f, 0.5f, 0.34f), true);
        }
        mb.SmoothNormals();
        // (grained and pitted, not flat colour: the stone material's photographed grain over each part's own colour)
        var campMat = MakeStoneMaterial(2.2f, 0.4f, 0f, 0f, true, "dark_rock");
        campMat.SetShaderParameter("tint", new Color(1.7f, 1.6f, 1.55f));
        AddChild(new MeshInstance3D { Mesh = mb.ToMesh(campMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

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
                Scale = new Vector3(k == 0 ? 1.0f : 0.68f, k == 0 ? 1.7f : 1.0f + 0.3f * (float)_rng.NextDouble(), 1f),
            };
            f.SetInstanceShaderParameter("seed", (float)_rng.NextDouble() * 10f);
            f.SetInstanceShaderParameter("energy", k == 0 ? 1.6f : 1.25f);
            AddChild(f);
            _flames.Add(f);
        }
        // a soft warm halo round the flames (faint: the screen's bloom does most of it)
        var halo = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2.6f, 2.6f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.55f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Puff, AlbedoColor = new Color(1f, 0.5f, 0.18f, 0.15f), NoDepthTest = false, DisableFog = true,
            },
        };
        AddChild(halo);
        // (at dusk the fire is the brightest thing near: a warm pool of light round the camp, throwing the heroes' shadows out across the grass)
        _fireLight = new OmniLight3D
        {
            LightColor = new Color(1f, 0.55f, 0.24f), LightEnergy = FireEnergy, LightSpecular = 0.4f, OmniRange = 8f, OmniAttenuation = 2f, Position = new Vector3(0, 0.75f, 0),
            ShadowEnabled = true, ShadowBlur = 1.5f, LightVolumetricFogEnergy = 2.5f,
        };
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
                FontSize = 48, PixelSize = 0.005f, OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
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
        _fireLight.LightEnergy = FireEnergy * flick;

        foreach (var s in _seats)
        {
            // (a hero not yet found isn't at the fire)
            bool here = Meta.IsUnlocked(s.Kind);
            s.Model.Visible = here;
            if (!here) { s.Label.Visible = false; continue; }
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
        var widePos = WidePos;
        var wideLook = WideLook;
        var closePos = new Vector3(-0.25f, 1.95f, 5.1f);
        var closeLook = new Vector3(0.1f, 0.85f, -0.9f);
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
        pos += new Vector3(MathF.Sin(_time * 0.21f) * 0.18f, MathF.Sin(_time * 0.17f + 1f) * 0.05f, 0f);
        _cam.Position = pos;
        _cam.LookAt(look, Vector3.Up);
    }
}
