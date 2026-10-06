using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Root of the 3D presentation. The simulation (in the hidden 2D world) is untouched; this
/// builds the level's rock, water and lighting in 3D and flies a perspective camera that frames
/// exactly what the gameplay camera frames on the play plane (so "off screen" still means off
/// screen for spawning).
/// </summary>
public partial class Stage3D : Node3D
{
    public static Stage3D I { get; private set; }

    public Camera3D Cam { get; private set; }
    public WorldEnvironment WorldEnv { get; private set; }
    public Godot.Environment Env { get; private set; }
    public TerrainView Terrain { get; private set; }
    /// <summary>The level's glowing things, lit a few dozen at a time near the camera.</summary>
    public LightPool3D Lights { get; private set; }
    /// <summary>Everything built for the current level (freed on the next build).</summary>
    public Node3D Level { get; private set; }

    /// <summary>Visual layer of creatures and heroes: they get a key and rim light of their own.</summary>
    public const uint ActorLayer = 1u << 1;

    private DirectionalLight3D _fill, _top, _actorKey, _actorRim;
    private Vector3 _camPos;
    private bool _camInit;
    private float _shakeT;
    private readonly Noise3 _shakeNoise = new(99);

    public override void _Ready()
    {
        I = this;
        Name = "Stage3D";
        ProcessMode = ProcessModeEnum.Always;
        Env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.005f, 0.005f, 0.008f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.5f, 0.52f, 0.6f),
            AmbientLightEnergy = 0.25f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.1f,
            SsaoEnabled = true,
            SsaoRadius = 1.2f,
            SsaoIntensity = 2.2f,
            SsaoPower = 1.6f,
            SsaoDetail = 0.5f,
            GlowEnabled = true,
            GlowIntensity = 0.9f,
            GlowStrength = 1.0f,
            GlowBloom = 0.04f,
            GlowHdrThreshold = 1.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            VolumetricFogEnabled = true,
            VolumetricFogDensity = 0.018f,
            VolumetricFogAlbedo = new Color(0.8f, 0.82f, 0.9f),
            VolumetricFogLength = 72f,
            VolumetricFogDetailSpread = 2f,
            VolumetricFogAnisotropy = 0.35f,
            // a little more contrast than the tonemapper gives on its own (a gentle curve: the
            // separation comes mostly from darker back walls, thinner haze and inked creatures,
            // so shadows keep their detail)
            AdjustmentEnabled = true,
            AdjustmentContrast = 1.14f,
            AdjustmentSaturation = 1.08f,
        };
        for (int k = 0; k < 7; k++) Env.SetGlowLevel(k, k is >= 1 and <= 4 ? 1f : 0f);
        WorldEnv = new WorldEnvironment { Environment = Env };
        AddChild(WorldEnv);

        Cam = new Camera3D { Fov = Tune.Feel.Camera3DFov, Near = 0.5f, Far = 400f, Current = true };
        AddChild(Cam);
        // the ink line round every creature (a screen-space edge pass, laid over the view)
        AddChild(new InkOutline(Cam));

        // a soft light from the viewer's side, so the camera-facing rock reads as rock
        _fill = new DirectionalLight3D
        {
            LightColor = new Color(0.75f, 0.8f, 1f),
            LightEnergy = 0.12f,
            ShadowEnabled = false,
            LightVolumetricFogEnergy = 0f,
        };
        AddChild(_fill);
        _fill.RotationDegrees = new Vector3(-28f, 12f, 0f);

        // a shadowless light from above: every floor you could stand on reads, wherever it is
        _top = new DirectionalLight3D
        {
            LightColor = new Color(0.8f, 0.82f, 0.9f),
            LightEnergy = 0.3f,
            ShadowEnabled = false,
            LightVolumetricFogEnergy = 0f,
            LightSpecular = 0.3f,
        };
        AddChild(_top);
        _top.LookAtFromPosition(Vector3.Zero, new Vector3(0.12f, -1f, -0.3f), Vector3.Forward);

        // Actors get their own key and rim (lighting only the actor layer), so every creature
        // reads against the rock however dark the cave: a warm key from the viewer's upper left,
        // a cool rim from behind that draws the silhouette.
        _actorKey = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.92f, 0.82f), LightEnergy = 1.4f, ShadowEnabled = false,
            LightVolumetricFogEnergy = 0f, LightCullMask = ActorLayer, LightSpecular = 0.6f,
        };
        AddChild(_actorKey);
        _actorKey.LookAtFromPosition(Vector3.Zero, new Vector3(0.45f, -0.5f, -0.75f), Vector3.Up);
        _actorRim = new DirectionalLight3D
        {
            LightColor = new Color(0.65f, 0.78f, 1f), LightEnergy = 1.8f, ShadowEnabled = false,
            LightVolumetricFogEnergy = 0f, LightCullMask = ActorLayer, LightSpecular = 1f,
        };
        AddChild(_actorRim);
        _actorRim.LookAtFromPosition(Vector3.Zero, new Vector3(-0.35f, -0.4f, 0.85f), Vector3.Up);

        Level = new Node3D { Name = "Level" };
        AddChild(Level);
        Lights = new LightPool3D { Name = "Lights" };
        AddChild(Lights);
        // effects: particles and lights in 3D, damage numbers and screen washes over the top
        AddChild(new Fx3D());
        // every prop (projectile, pickup, hazard...) gets a 3D body as it enters the world
        GetTree().NodeAdded += PropViews.Attach;
        var overlay = new CanvasLayer { Name = "FxOverlayLayer", Layer = 6 };
        AddChild(overlay);
        overlay.AddChild(new FxOverlay());

        // debug: --cam3d=yaw,pitch,distance orbits the camera around the action;
        // --bright floods the scene with light to inspect geometry
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--cam3d="))
            {
                var parts = a[8..].Split(',');
                _debugOrbit = new Vector3(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            }
            if (a == "--bright") _debugBright = true;
            if (a == "--nofog") _debugNoFog = true;
            if (a == "--terraindebug") _debugTerrain = 1;
            if (a == "--terraindebug=weights") _debugTerrain = 2;
        }
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= PropViews.Attach;
        // shared resources held in statics must go while the engine is still up
        PropViews.ReleaseShared();
        Ghost3D.ReleaseShared();
        CreatureLibrary.ReleaseShared();
    }

    private Vector3? _debugOrbit;
    private bool _debugBright, _debugNoFog;
    private int _debugTerrain;

    /// <summary>Builds the 3D level for a freshly generated cave.</summary>
    public void BuildLevel(CaveData cave)
    {
        foreach (var c in Level.GetChildren()) { Level.RemoveChild(c); c.QueueFree(); }
        Lights.Clear();
        // this biome's creatures bake in parallel now rather than one by one as they spawn
        CreatureLibrary.Prefetch(CreatureRegistry.Roster(cave.Biome ?? G.Biome ?? Biomes.Get(BiomeId.Entrance)));
        Terrain = new TerrainView { Name = "Terrain" };
        Level.AddChild(Terrain);
        Terrain.Build(cave);
        if (_debugTerrain > 0) Terrain.Material.SetShaderParameter("debug_view", _debugTerrain);
        var decor = new CaveDecor3D { Name = "Decor" };
        Level.AddChild(decor);
        decor.Build(Terrain.Field, cave, Terrain.Material, Lights);
        var liquid = new Liquid3D { Name = "Liquid" };
        Level.AddChild(liquid);
        liquid.Build(cave, Terrain.Field, Lights);
        ApplyBiome(cave);
        _camInit = false;
        // everything else bakes in the background while this level plays
        CreatureLibrary.PrebuildRest();
    }

    /// <summary>Lighting and atmosphere per biome (the 2D game's darkness becomes real darkness).</summary>
    private void ApplyBiome(CaveData cave)
    {
        var b = cave.Biome ?? Biomes.Get(BiomeId.Slime);
        float dark = b.Darkness;
        var glow = b.Glow;
        var edge = b.Edge;
        Env.AmbientLightColor = edge.Lerp(glow, 0.35f).Lerp(new Color(0.6f, 0.62f, 0.7f), 0.4f);
        // thinner haze than before: distance falls away into the dark and the play layer stands
        // clear of it (contrast without murk); the fill stays, so shadows keep their detail
        Env.AmbientLightEnergy = Mathf.Lerp(0.95f, 0.45f, dark);
        Env.VolumetricFogAlbedo = glow.Lerp(new Color(0.75f, 0.78f, 0.85f), 0.6f);
        Env.VolumetricFogDensity = 0.008f + dark * 0.01f;
        Env.VolumetricFogEmission = b.BackBottom.Lerp(glow, 0.15f);
        Env.VolumetricFogEmissionEnergy = 0.5f;
        _fill.LightEnergy = Mathf.Lerp(0.3f, 0.06f, dark);
        _top.LightEnergy = Mathf.Lerp(0.7f, 0.3f, dark);
        _top.LightColor = edge.Lerp(glow, 0.3f).Lerp(new Color(0.85f, 0.87f, 0.95f), 0.6f);
        _actorKey.LightEnergy = Mathf.Lerp(1.0f, 0.75f, dark);
        _actorRim.LightEnergy = Mathf.Lerp(1.6f, 2.0f, dark);
        _actorRim.LightColor = glow.Lerp(new Color(0.7f, 0.8f, 1f), 0.6f);
        Terrain?.Material?.SetShaderParameter("face_fill_energy", Mathf.Lerp(1.1f, 0.6f, dark));
        Terrain?.Material?.SetShaderParameter("face_fill", edge.Lerp(glow, 0.25f).Lerp(new Color(0.6f, 0.65f, 0.8f), 0.5f));
        if (cave.Liquid == Liquid.Lava)
        {
            Env.VolumetricFogAlbedo = new Color(1f, 0.55f, 0.35f);
            Env.VolumetricFogDensity += 0.006f;
        }
        if (_debugBright)
        {
            Env.AmbientLightEnergy = 1.2f;
            Env.AmbientLightColor = new Color(0.8f, 0.8f, 0.8f);
            Env.VolumetricFogEnabled = false;
            _fill.LightEnergy = 1.2f;
        }
        if (_debugNoFog) Env.VolumetricFogEnabled = false;
        ApplySettings();
    }

    /// <summary>The graphics settings that live in the environment: haze, bloom, ambient occlusion, brightness.</summary>
    public void ApplySettings()
    {
        Env.VolumetricFogEnabled = GameSettings.Fog && !_debugBright && !_debugNoFog;
        Env.GlowEnabled = GameSettings.Bloom;
        Env.SsaoEnabled = GameSettings.AmbientOcclusion;
        Env.TonemapExposure = 1.1f * GameSettings.Brightness;
    }

    public override void _Process(double delta)
    {
        UpdateCamera((float)delta);
        UpdateProxies();
    }

    // ---- temporary stand-ins until every actor has its own 3D model
    private readonly Dictionary<Node2D, Node3D> _proxies = new();
    private OmniLight3D _lantern;

    private void UpdateProxies()
    {
        var p = G.Player;
        if (p != null && IsInstanceValid(p) && p.Anim?.Model3D == null)
        {
            if (_lantern == null || !IsInstanceValid(_lantern))
            {
                _lantern = new OmniLight3D
                {
                    LightColor = new Color(1f, 0.78f, 0.52f), LightEnergy = 2.2f, OmniRange = 15f, OmniAttenuation = 1.1f,
                    ShadowEnabled = true, LightVolumetricFogEnergy = 1.2f, LightSize = 0.15f,
                };
                AddChild(_lantern);
            }
            _lantern.Position = W3.P(p.GlobalPosition, 1.1f) + new Vector3(0, 0.6f, 0);
            Proxy(p, 0.4f, 1.62f, new Color(0.2f, 0.55f, 0.6f));
        }
        else if (_lantern != null && IsInstanceValid(_lantern)) { _lantern.QueueFree(); _lantern = null; }
        foreach (var e in G.Enemies)
            if (IsInstanceValid(e) && !e.Dead && e.Animator?.Model3D == null) Proxy(e, W3.M(e.HitRadius), W3.M(e.HitRadius) * 2f, new Color(0.7f, 0.15f, 0.12f));
        var dead = new List<Node2D>();
        foreach (var kv in _proxies)
            if (!IsInstanceValid(kv.Key) || (kv.Key is Enemy en && en.Dead)) { kv.Value.QueueFree(); dead.Add(kv.Key); }
        foreach (var d in dead) _proxies.Remove(d);
    }

    private void Proxy(Node2D n, float radius, float height, Color col)
    {
        if (!_proxies.TryGetValue(n, out var node))
        {
            var mi = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = radius, Height = Math.Max(height, radius * 2f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = col, Roughness = 0.5f },
                CastShadow = n is Player ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            };
            AddChild(mi);
            node = mi;
            _proxies[n] = node;
        }
        node.Position = W3.P(n.GlobalPosition);
    }

    /// <summary>Visible height of the play plane in metres, from the gameplay camera's zoom.</summary>
    public static float ViewHeightM(Viewport vp, Camera2D cam)
    {
        float h = vp.GetVisibleRect().Size.Y;
        float zoom = cam?.Zoom.Y ?? Tune.Feel.CameraZoom;
        return h / zoom / W3.Ppu;
    }

    private void UpdateCamera(float dt)
    {
        var cam2 = G.Main?.Cam2D;
        if (cam2 == null || !IsInstanceValid(cam2) || G.Cave == null) return;
        var vp = GetViewport();
        var size = vp.GetVisibleRect().Size;
        var half = size / cam2.Zoom * 0.5f;
        // the same limits the 2D camera obeys
        var c = cam2.GlobalPosition;
        float l = cam2.LimitLeft, r = cam2.LimitRight, t = cam2.LimitTop, bt = cam2.LimitBottom;
        c.X = r - l < half.X * 2 ? (l + r) * 0.5f : Math.Clamp(c.X, l + half.X, r - half.X);
        c.Y = bt - t < half.Y * 2 ? (t + bt) * 0.5f : Math.Clamp(c.Y, t + half.Y, bt - half.Y);
        c += cam2.Offset;

        float viewH = ViewHeightM(vp, cam2);
        float fov = Tune.Feel.Camera3DFov;
        float dist = viewH * 0.5f / MathF.Tan(Mathf.DegToRad(fov) * 0.5f);
        var target = W3.P(c);
        var pos = target + new Vector3(0, Tune.Feel.Camera3DLift, dist);
        Cam.Fov = fov;
        // a little rotational shake on top of the 2D camera's positional shake
        _shakeT += dt;
        float s = (G.Fx?.Shake ?? 0f) * GameSettings.Shake;
        float roll = s > 0 ? _shakeNoise.Sample(_shakeT * 18f, 0, 0) * s * 0.0035f : 0f;
        if (_debugOrbit is Vector3 o)
        {
            // yaw around the vertical axis, pitch up, at a fixed distance
            var dir = new Vector3(MathF.Sin(Mathf.DegToRad(o.X)) * MathF.Cos(Mathf.DegToRad(o.Y)), MathF.Sin(Mathf.DegToRad(o.Y)), MathF.Cos(Mathf.DegToRad(o.X)) * MathF.Cos(Mathf.DegToRad(o.Y)));
            pos = target + dir * o.Z;
            roll = 0;
        }
        Cam.Position = pos;
        Cam.LookAt(target + new Vector3(0, Tune.Feel.Camera3DLookLift, 0), Vector3.Up);
        if (roll != 0) Cam.RotateObjectLocal(Vector3.Back, roll);
        _camPos = pos;
        _camInit = true;
    }
}
