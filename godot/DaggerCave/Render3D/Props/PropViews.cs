using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// 3D bodies for the game's props: projectiles, pickups, chests, portals and hazards. Each prop
/// is still a 2D node running its own gameplay; when one enters the tree, <see cref="Attach"/>
/// gives it a <see cref="PropView"/> child that follows it in 3D and reads its state each frame.
/// </summary>
public static class PropViews
{
    public static void Attach(Node n)
    {
        PropView v = n switch
        {
            LifeMote => new LifeMoteView(),
            EnemyProjectile => new ProjectileView(),
            LavaPuddle => new LavaPuddleView(),
            Shockwave => new ShockwaveView(),
            FallingRock => new FallingRockView(),
            SwordWave => new SwordWaveView(),
            ElementBolt => new ElementBoltView(),
            Updraft => new UpdraftView(),
            Rope => new RopeView(),
            Rubble => new RubbleView(),
            Blizzard => new BlizzardView(),
            IceBlock => new IceBlockView(),
            ThrownDagger => new ThrownDaggerView(),
            SmokeCloud => new SmokeCloudView(),
            XpOrb => new XpOrbView(),
            HeartPickup => new HeartView(),
            PotionPickup => new PotionView(),
            KeyPickup => new KeyView(),
            VaultGate => new VaultGateView(),
            Chest => new ChestView(),
            HeroCage => new HeroCageView(),
            Portal { Outside: true } => new MouthView(),
            Waterfall => new WaterfallView(),
            Portal { Drain: true } => new DrainView(),
            Portal => new PortalView(),
            AirVent => new AirVentView(),
            AirBubble => new AirBubbleView(),
            SporeCloud => new SporeCloudView(),
            SporePod => new SporePodView(),
            WebPatch => new WebView(),
            GraspingRoots => new GraspingRootsView(),
            CaveIn => new CaveInView(),
            CrystalSpikes => new CrystalSpikesView(),
            FireVent => new FireVentView(),
            IceSheet => new IceSheetView(),
            StalagGrip => new StalagGripView(),
            IcePlatform => new IcePlatformView(),
            _ => null,
        };
        if (v == null) return;
        v.Owner2D = (Node2D)n;
        n.CallDeferred(Node.MethodName.AddChild, v);
    }

    // ---- shared materials and meshes
    private static ShaderMaterial _sprite, _cloud, _bubble;
    public static ShaderMaterial SpriteMat => _sprite ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_sprite.gdshader") };
    public static ShaderMaterial CloudMat => _cloud ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_cloud.gdshader") };
    public static ShaderMaterial BubbleMat => _bubble ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_bubble.gdshader") };
    private static QuadMesh _quad;
    public static QuadMesh Quad => _quad ??= new QuadMesh { Size = new Vector2(2, 2) };

    private static StandardMaterial3D _ice, _steel, _wood, _gold, _glass, _rock, _rubble, _vcol;
    public static StandardMaterial3D Ice => _ice ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.62f, 0.85f, 1f, 0.72f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.08f,
        RimEnabled = true, Rim = 0.8f, EmissionEnabled = true, Emission = new Color(0.25f, 0.55f, 0.8f), EmissionEnergyMultiplier = 0.25f,
    };
    public static StandardMaterial3D Steel => _steel ??= new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.78f, 0.82f), Metallic = 0.95f, Roughness = 0.28f };
    public static StandardMaterial3D Wood => _wood ??= new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.21f, 0.11f), Roughness = 0.8f };
    public static StandardMaterial3D Gold => _gold ??= new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.72f, 0.3f), Metallic = 1f, Roughness = 0.3f };
    public static StandardMaterial3D Glass => _glass ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.85f, 0.92f, 1f, 0.3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, RimEnabled = true, Rim = 1f,
    };
    public static StandardMaterial3D Rock => _rock ??= new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.38f, 0.34f), Roughness = 0.9f, VertexColorUseAsAlbedo = true };
    /// <summary>Rubble stone: real rock grain, projected from all three sides, over the chunks' own face tones.</summary>
    public static StandardMaterial3D RubbleRock => _rubble ??= new StandardMaterial3D
    {
        AlbedoTexture = TerrainLook.Tex("rock_face", "diff"), NormalEnabled = true, NormalTexture = TerrainLook.Tex("rock_face", "nor"), NormalScale = 1.3f,
        Uv1Triplanar = true, Uv1Scale = new Vector3(0.9f, 0.9f, 0.9f), VertexColorUseAsAlbedo = true, Roughness = 1f,
    };
    public static StandardMaterial3D VertexColored => _vcol ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.6f };

    /// <summary>
    /// Lets go of the shared resources before the engine shuts down (static references would
    /// otherwise be released after the rendering server is gone).
    /// </summary>
    public static void ReleaseShared()
    {
        foreach (var r in new Resource[] { _sprite, _cloud, _bubble, PortalView.StairMatOrNull, _quad, _ice, _steel, _wood, _gold, _glass, _rock, _rubble, _vcol }) r?.Dispose();
        _sprite = _cloud = _bubble = null; _quad = null;
        PortalView.ReleaseShared();
        WaterfallView.ReleaseShared();
        DrainView.ReleaseShared();
        MouthView.ReleaseShared();
        _ice = _steel = _wood = _gold = _glass = _rock = _vcol = null;
    }

    public static StandardMaterial3D Emissive(Color c, float energy, float alpha = 1f) => new()
    {
        AlbedoColor = new Color(c.R, c.G, c.B, alpha), EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy, Roughness = 0.4f,
        Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };

    /// <summary>A glow sprite (billboarded quad) of a given shape.</summary>
    public static MeshInstance3D Sprite(Color col, float shape, float intensity, float size, float param = 0f)
    {
        var mi = new MeshInstance3D { Mesh = Quad, MaterialOverride = SpriteMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * size };
        SetSprite(mi, col, shape, intensity, param);
        return mi;
    }

    public static void SetSprite(MeshInstance3D mi, Color col, float shape, float intensity, float param = 0f)
    {
        mi.SetInstanceShaderParameter("sprite_color", col);
        mi.SetInstanceShaderParameter("sprite_shape", shape);
        mi.SetInstanceShaderParameter("sprite_intensity", intensity);
        mi.SetInstanceShaderParameter("sprite_param", param);
    }

    public static OmniLight3D Light(Color col, float energy, float range) => new()
    {
        LightColor = col, LightEnergy = energy, OmniRange = range, ShadowEnabled = false, LightVolumetricFogEnergy = 0.8f, OmniAttenuation = 1.3f,
    };

    public static MeshInstance3D Mesh(MeshBuilder mb, Material mat, bool shadow = true) => new()
    {
        Mesh = mb.ToMesh(mat), CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
    };
}

/// <summary>A prop's 3D body: follows its 2D node and reads its state every frame.</summary>
public abstract partial class PropView : Node3D
{
    public Node2D Owner2D;
    protected float Time;

    public override void _Ready()
    {
        Build();
        Sync(0f);
        CreatureModel.SetActorLayer(this);
    }

    public override void _Process(double delta)
    {
        if (Owner2D == null || !IsInstanceValid(Owner2D)) { QueueFree(); return; }
        Time += (float)delta;
        Sync((float)delta);
    }

    protected abstract void Build();
    protected abstract void Sync(float dt);

    /// <summary>Places this view at the owner (plus a 2D pixel offset), at a depth toward the camera.</summary>
    protected void Follow(Vector2 offsetPx = default, float z = 0f) => Position = W3.P(Owner2D.GlobalPosition + offsetPx, z);
}

// ============================================================================ projectiles

/// <summary>Stolen life flying home to the Vitalist: a pulsing crimson mote with a comet tail, lighting its way.</summary>
public partial class LifeMoteView : PropView
{
    private MeshInstance3D _core, _halo;
    private readonly MeshInstance3D[] _tail = new MeshInstance3D[5];
    private OmniLight3D _light;
    private float _size = 1f;

    protected override void Build()
    {
        _size = ((LifeMote)Owner2D).Size;
        _halo = PropViews.Sprite(Player.LifeColor, 0, 1.7f, 0.6f);
        AddChild(_halo);
        _core = PropViews.Sprite(new Color(0.85f, 1f, 0.82f), 3, 2.4f, 0.28f);
        AddChild(_core);
        for (int k = 0; k < _tail.Length; k++)
        {
            float f = 1f - k / (float)_tail.Length;
            _tail[k] = PropViews.Sprite(new Color(0.15f, 0.85f, 0.22f), 0, 1.3f * f, (0.36f * f + 0.08f) * _size);
            AddChild(_tail[k]);
        }
        _light = PropViews.Light(new Color(0.3f, 1f, 0.4f), 0.5f * _size, 1.6f);
        AddChild(_light);
        // (the stolen life is a cluster of small green orbs: no tails, just the glow)
        foreach (var t in _tail) t.Visible = false;
    }

    protected override void Sync(float dt)
    {
        var m = (LifeMote)Owner2D;
        Follow(default, 0.3f);
        float a = m.Alpha;
        Visible = a > 0.02f;
        _halo.Scale = Vector3.One * (0.14f + 0.2f * _size);
        _core.Scale = Vector3.One * (0.06f + 0.09f * _size);
        PropViews.SetSprite(_halo, new Color(Player.LifeColor, 0.55f * a), 0, 1.2f);
        PropViews.SetSprite(_core, new Color(0.85f, 1f, 0.82f, a), 3, 2.0f);
        _light.LightEnergy = 0.5f * _size * a;
    }
}

public partial class ProjectileView : PropView
{
    private Node3D _body;
    private MeshInstance3D _glow;
    private OmniLight3D _light;
    private string _kind;

    protected override void Build()
    {
        var p = (EnemyProjectile)Owner2D;
        _kind = p.Kind;
        float r = p.Radius / W3.Ppu;
        var rng = new Random((int)(GetInstanceId() % 10000));
        switch (_kind)
        {
            case "lava":
                {
                    var mb = new MeshBuilder();
                    mb.Blob(Vector3.Zero, Vector3.One * r * 1.3f, 5, Colors.White, new Noise3(rng.Next()), 0.3f, 2f);
                    _body = PropViews.Mesh(mb, PropViews.Emissive(new Color(1f, 0.42f, 0.08f), 3.2f), false);
                    _glow = PropViews.Sprite(new Color(1f, 0.45f, 0.1f), 4, 0.9f, r * 4f);
                    _light = PropViews.Light(new Color(1f, 0.5f, 0.15f), 1.6f, 4f);
                    break;
                }
            case "fire":
                {
                    _body = new Node3D();
                    _glow = PropViews.Sprite(new Color(1f, 0.5f, 0.12f), 4, 1.4f, r * 3.5f);
                    var core = PropViews.Sprite(new Color(1f, 0.85f, 0.45f), 0, 3f, r * 2.2f);
                    _body.AddChild(core);
                    _light = PropViews.Light(new Color(1f, 0.55f, 0.2f), 1.4f, 3.5f);
                    break;
                }
            case "ice":
            case "crystal":
                {
                    var mb = new MeshBuilder();
                    var col = _kind == "ice" ? new Color(0.75f, 0.95f, 1f) : new Color(0.65f, 0.8f, 1f);
                    DesignKit.CrystalAt(mb, new Vector3(-r * 1.2f, 0, 0), Vector3.Right, r * 0.55f, r * 3.2f, col);
                    _body = PropViews.Mesh(mb, PropViews.Emissive(col, 1.2f, 0.85f), false);
                    _glow = PropViews.Sprite(col, 0, 1.2f, r * 3f);
                    _light = PropViews.Light(col, 0.6f, 2.5f);
                    break;
                }
            case "water":
                {
                    var mb = new MeshBuilder();
                    mb.Blob(Vector3.Zero, new Vector3(r * 1.1f, r * 1.3f, r * 1.1f), 5, Colors.White, new Noise3(rng.Next()), 0.12f, 3f);
                    _body = PropViews.Mesh(mb, PropViews.Emissive(new Color(0.35f, 0.65f, 1f), 0.9f, 0.7f), false);
                    _glow = PropViews.Sprite(new Color(0.45f, 0.75f, 1f), 0, 0.5f, r * 3f);
                    _light = PropViews.Light(new Color(0.45f, 0.75f, 1f), 0.5f, 2.5f);
                    break;
                }
            case "leaf":
                {
                    var mb = new MeshBuilder();
                    mb.Blob(Vector3.Zero, new Vector3(r * 2.4f, r * 0.35f, r * 1.1f), 5, new Color(0.3f, 0.6f, 0.15f), new Noise3(rng.Next()), 0.1f, 3f);
                    _body = PropViews.Mesh(mb, PropViews.VertexColored);
                    _glow = PropViews.Sprite(new Color(0.45f, 1f, 0.3f), 0, 0.35f, r * 2.4f);
                    break;
                }
            case "spit":
                {
                    var mb = new MeshBuilder();
                    mb.Blob(Vector3.Zero, Vector3.One * r, 4, Colors.White, new Noise3(rng.Next()), 0.2f, 3f);
                    _body = PropViews.Mesh(mb, PropViews.Emissive(new Color(0.45f, 0.9f, 0.25f), 0.8f, 0.85f), false);
                    _glow = PropViews.Sprite(new Color(0.5f, 1f, 0.3f), 0, 0.5f, r * 2.5f);
                    break;
                }
            default:
                {
                    var mb = new MeshBuilder();
                    mb.Blob(Vector3.Zero, new Vector3(r * 1.2f, r, r * 1.1f), 4, new Color(0.5f, 0.46f, 0.42f), new Noise3(rng.Next()), 0.35f, 2.5f);
                    _body = PropViews.Mesh(mb, PropViews.VertexColored);
                    break;
                }
        }
        AddChild(_body);
        if (_glow != null) AddChild(_glow);
        if (_light != null) AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var p = (EnemyProjectile)Owner2D;
        Follow(default, 0.15f);
        var v = p.Vel;
        switch (_kind)
        {
            case "ice":
            case "crystal":
                _body.Rotation = new Vector3(Time * 6f, 0, MathF.Atan2(-v.Y, v.X));
                break;
            case "leaf":
                _body.Rotation = new Vector3(Time * 9f, 0, MathF.Atan2(-v.Y, v.X));
                break;
            case "fire":
                {
                    float a = Math.Clamp(p.Life / 0.85f, 0, 1);
                    float s = 1.6f - a * 0.6f;
                    _body.Scale = Vector3.One * s;
                    _glow.Scale = Vector3.One * (p.Radius / W3.Ppu * 4f * s);
                    PropViews.SetSprite(_glow, new Color(1f, 0.5f, 0.12f, a), 4, 1.4f);
                    _light.LightEnergy = 1.4f * a;
                    break;
                }
            default:
                _body.Rotation = new Vector3(Time * 4f, Time * 3f, Time * 5f);
                break;
        }
    }
}

public partial class LavaPuddleView : PropView
{
    private MeshInstance3D _pool, _glow;
    private OmniLight3D _light;

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(Vector3.Zero, new Vector3(LavaPuddle.HalfW / W3.Ppu, 0.06f, 0.9f), 6, Colors.White, new Noise3(5), 0.25f, 3f, 1f);
        _pool = PropViews.Mesh(mb, PropViews.Emissive(new Color(1f, 0.45f, 0.08f), 2.6f), false);
        AddChild(_pool);
        _glow = PropViews.Sprite(new Color(1f, 0.45f, 0.1f), 4, 1f, 1.6f);
        _glow.Position = new Vector3(0, 0.25f, 0.6f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(1f, 0.5f, 0.15f), 1.5f, 3.5f);
        _light.Position = new Vector3(0, 0.5f, 0.8f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var p = (LavaPuddle)Owner2D;
        Follow(new Vector2(0, 1), 0f);
        float a = Math.Clamp(p.LifeLeft / 0.6f, 0, 1);
        float w = 0.9f + 0.1f * MathF.Sin(Time * 6);
        _pool.Scale = new Vector3(w * (0.4f + 0.6f * a), 1, 0.8f + 0.2f * a);
        _light.LightEnergy = 1.5f * a * (0.85f + 0.15f * MathF.Sin(Time * 9f));
        _glow.Visible = a > 0.05f;
    }
}

public partial class ShockwaveView : PropView
{
    private readonly MeshInstance3D[] _spikes = new MeshInstance3D[3];

    protected override void Build()
    {
        for (int k = 0; k < 3; k++)
        {
            var mb = new MeshBuilder();
            mb.Tube(new[] { new Vector3(0, -0.1f, 0), new Vector3(0.05f, 0.5f, 0), new Vector3(0.08f, 1f, 0) }, new[] { 0.32f, 0.16f, 0.0f }, 6, new Color(0.55f, 0.5f, 0.45f), capStart: true,
                bump: (i, j) => 0.25f * (float)Math.Sin(i * 3.1 + j * 1.7 + k));
            mb.SmoothNormals();
            _spikes[k] = PropViews.Mesh(mb, PropViews.VertexColored);
            AddChild(_spikes[k]);
        }
    }

    protected override void Sync(float dt)
    {
        var s = (Shockwave)Owner2D;
        Follow(new Vector2(0, 2), 0f);
        for (int k = 0; k < 3; k++)
        {
            float x = -s.Dir * k * 7 * s.Size / W3.Ppu;
            float h = (16 - k * 4) * s.Size * (0.8f + 0.2f * MathF.Sin(s.T * 30 + k)) / W3.Ppu * 1.15f;
            _spikes[k].Position = new Vector3(x, 0, (k - 1) * 0.35f);
            _spikes[k].Scale = new Vector3(s.Size, Math.Max(0.05f, h), s.Size);
            _spikes[k].RotationDegrees = new Vector3((k - 1) * 12f, k * 40f, s.Dir * 14f);
        }
    }
}

public partial class FallingRockView : PropView
{
    private Node3D _rock;
    private MeshInstance3D _mark;

    protected override void Build()
    {
        var mb = DecorMeshes.Stalactite(new Random((int)(GetInstanceId() % 1000)), 1.6f, 0.45f, new Noise3(3));
        var mi = PropViews.Mesh(mb, PropViews.Rock);
        mi.RotationDegrees = new Vector3(180, 0, 0);
        mi.Position = new Vector3(0, 0.6f, 0);
        _rock = new Node3D();
        _rock.AddChild(mi);
        AddChild(_rock);
        // the telegraph: a dark shadow on the floor where it will land
        _mark = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.8f, Height = 0.02f, RadialSegments = 24 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0, 0, 0, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true,
        };
        AddChild(_mark);
    }

    protected override void Sync(float dt)
    {
        var r = (FallingRock)Owner2D;
        Follow();
        float shake = r.Warn > 0 ? MathF.Sin(r.Warn * 60) * 0.1f : 0f;
        _rock.Position = new Vector3(shake, 0, 0);
        _mark.Visible = r.Warn > 0 && r.Target != null;
        if (_mark.Visible)
        {
            float a = 0.35f + 0.25f * MathF.Sin(r.Warn * 30);
            _mark.GlobalPosition = W3.P(r.Target.Value, 0f) + new Vector3(0, 0.03f, 0);
            _mark.Scale = new Vector3(1f + 0.15f * MathF.Sin(r.Warn * 20), 1, 1f + 0.15f * MathF.Sin(r.Warn * 20));
            ((StandardMaterial3D)_mark.MaterialOverride).AlbedoColor = new Color(0, 0, 0, a + 0.2f);
        }
    }
}

public partial class SwordWaveView : PropView
{
    private MeshInstance3D _arc, _core;
    private OmniLight3D _light;

    protected override void Build()
    {
        _arc = PropViews.Sprite(new Color(0.7f, 0.9f, 1f), 2, 2.2f, 1.1f, 0.09f);
        _arc.MaterialOverride = (Material)PropViews.SpriteMat.Duplicate();
        ((ShaderMaterial)_arc.MaterialOverride).SetShaderParameter("billboard", false);
        AddChild(_arc);
        _core = PropViews.Sprite(new Color(0.85f, 0.97f, 1f), 1, 2.5f, 0.9f);
        _core.MaterialOverride = _arc.MaterialOverride;
        AddChild(_core);
        _light = PropViews.Light(new Color(0.7f, 0.9f, 1f), 1.2f, 3f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var w = (SwordWave)Owner2D;
        Follow(default, 0.3f);
        float a = 1f - w.Traveled / w.Range;
        float ang = MathF.Atan2(-w.Dir.Y, w.Dir.X);
        // a crescent: a ring squashed thin across the flight, pushed forward
        _arc.Rotation = new Vector3(0, 0, ang);
        _arc.Scale = new Vector3(0.45f, 1.05f, 1f);
        PropViews.SetSprite(_arc, new Color(0.7f, 0.9f, 1f, a), 2, 2.4f, 0.1f);
        _core.Rotation = new Vector3(0, 0, ang);
        _core.Scale = new Vector3(0.9f, 0.18f, 1f);
        PropViews.SetSprite(_core, new Color(0.85f, 0.97f, 1f, a * 0.8f), 1, 2f);
        _light.LightEnergy = 1.2f * a;
    }
}

/// <summary>The Elementalist's bolt: a knot of fire (a hot white core in an orange halo) or of frost
/// (a bright star in an icy halo), with a tail streaming back along its flight, lighting its way.</summary>
public partial class ElementBoltView : PropView
{
    private MeshInstance3D _core, _halo;
    private readonly MeshInstance3D[] _tail = new MeshInstance3D[5];
    private OmniLight3D _light;
    private Color _col;

    protected override void Build()
    {
        var b = (ElementBolt)Owner2D;
        _col = b.Tint;
        _halo = PropViews.Sprite(_col, b.Frost ? 0 : 4, 1.6f, 0.55f);
        AddChild(_halo);
        _core = PropViews.Sprite(b.Frost ? new Color(0.95f, 1f, 1f) : new Color(1f, 0.92f, 0.7f), b.Frost ? 3 : 0, 2.6f, 0.26f);
        AddChild(_core);
        for (int k = 0; k < _tail.Length; k++)
        {
            float f = 1f - k / (float)_tail.Length;
            _tail[k] = PropViews.Sprite(_col, 0, 1.2f * f, 0.32f * f + 0.06f);
            AddChild(_tail[k]);
        }
        _light = PropViews.Light(_col, 1.3f, 3f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var b = (ElementBolt)Owner2D;
        Follow(default, 0.3f);
        float flick = b.Frost ? 1f : 0.85f + 0.15f * MathF.Sin(b.Age * 47f);
        _halo.Scale = Vector3.One * 0.55f * flick;
        var back = new Vector3(-b.Dir.X, b.Dir.Y, 0f);
        for (int k = 0; k < _tail.Length; k++) _tail[k].Position = back * (0.1f + 0.12f * k);
        _light.LightEnergy = 1.3f * flick;
    }
}

/// <summary>
/// The Elementalist's updraft: a wide, tall column of faint air, pale streaks drifting up it and a
/// slow swirl at its foot and its crown, all translucent (it's air, not a beam) and fading as it dies.
/// </summary>
public partial class UpdraftView : PropView
{
    private readonly MeshInstance3D[] _streaks = new MeshInstance3D[20];
    private readonly float[] _phase = new float[20], _x = new float[20], _speed = new float[20];
    private MeshInstance3D _foot, _haze;
    private OmniLight3D _light;
    private ShaderMaterial _flat;

    protected override void Build()
    {
        var u = (Updraft)Owner2D;
        // (streaks stand upright in the view plane: not turned to face the camera)
        _flat = (ShaderMaterial)PropViews.SpriteMat.Duplicate();
        _flat.SetShaderParameter("billboard", false);
        var rng = new Random((int)(GetInstanceId() % 10000));
        for (int k = 0; k < _streaks.Length; k++)
        {
            _phase[k] = (float)rng.NextDouble();
            _x[k] = (float)rng.NextDouble() * 2f - 1f;
            _speed[k] = 0.7f + 0.6f * (float)rng.NextDouble();
            _streaks[k] = PropViews.Sprite(new Color(0.85f, 0.95f, 1f), 1, 0.5f, 0.3f);
            _streaks[k].MaterialOverride = _flat;
            _streaks[k].Rotation = new Vector3(0, 0, MathF.PI / 2f);
            AddChild(_streaks[k]);
        }
        _haze = PropViews.Sprite(new Color(0.7f, 0.88f, 1f), 0, 0.2f, 1f);
        _haze.MaterialOverride = _flat;
        AddChild(_haze);
        // (a soft glow on the ground where it rises: no ring or swirl at either end)
        _foot = PropViews.Sprite(new Color(0.8f, 0.94f, 1f), 0, 0.2f, W3.M(u.Width) * 0.9f);
        AddChild(_foot);
        _light = PropViews.Light(new Color(0.75f, 0.9f, 1f), 0.15f, 4f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var u = (Updraft)Owner2D;
        Follow(default, 0.15f);
        Rotation = new Vector3(0, 0, -u.Angle);
        float h = W3.M(u.Height), w = W3.M(u.Width), s = u.Strength;
        for (int k = 0; k < _streaks.Length; k++)
        {
            float y = (_phase[k] + u.Age * 0.55f * _speed[k]) % 1f;
            _streaks[k].Position = new Vector3(_x[k] * w * 0.45f, y * h, 0);
            float edge = MathF.Min(y / 0.2f, (1f - y) / 0.3f);
            PropViews.SetSprite(_streaks[k], new Color(0.85f, 0.95f, 1f, Math.Clamp(edge, 0f, 1f) * 0.16f * s), 1, 0.5f);
            _streaks[k].Scale = new Vector3(Math.Min(h * 0.22f, 1.4f), w * 0.05f, 1f);
        }
        _haze.Position = new Vector3(0, h * 0.5f, -0.05f);
        _haze.Scale = new Vector3(w * 0.95f, h * 0.98f, 1f);
        PropViews.SetSprite(_haze, new Color(0.7f, 0.88f, 1f, 0.05f * s), 0, 0.3f);
        _foot.Position = new Vector3(0, 0.05f, 0);
        _foot.Scale = new Vector3(w * 0.9f, w * 0.25f, 1f);
        PropViews.SetSprite(_foot, new Color(0.8f, 0.94f, 1f, 0.1f * s), 0, 0.2f);
        _light.Position = new Vector3(0, h * 0.5f, 0.3f);
        _light.LightEnergy = 0.08f * s;
    }
}

/// <summary>A party rope: a twisted hemp line with a knot every so often, unrolling when cast and
/// swaying a little, hanging from a small iron peg.</summary>
public partial class RopeView : PropView
{
    private MeshInstance3D _line, _peg;
    private readonly MeshInstance3D[] _knots = new MeshInstance3D[12];
    private StandardMaterial3D _hemp;

    protected override void Build()
    {
        _hemp = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.5f, 0.3f), Roughness = 0.95f };
        _line = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.03f, Height = 1f, RadialSegments = 6, Rings = 1 }, MaterialOverride = _hemp };
        AddChild(_line);
        for (int k = 0; k < _knots.Length; k++)
        {
            _knots[k] = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 8, Rings = 4 }, MaterialOverride = _hemp };
            AddChild(_knots[k]);
        }
        _peg = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.07f, 0.1f) }, MaterialOverride = PropViews.Steel };
        AddChild(_peg);
    }

    protected override void Sync(float dt)
    {
        var r = (Rope)Owner2D;
        Follow(default, 0.1f);
        float len = W3.M(r.Unrolled), s = r.Strength;
        Visible = s > 0.02f;
        var sway = new Vector3(MathF.Sin(r.Age * 1.7f) * 0.012f * len, 0, 0);
        _line.Scale = new Vector3(1, Math.Max(0.01f, len), 1);
        _line.Position = new Vector3(0, -len * 0.5f, 0) + sway * 0.5f;
        float step = 0.55f;
        for (int k = 0; k < _knots.Length; k++)
        {
            float y = step * (k + 1);
            _knots[k].Visible = y < len;
            _knots[k].Position = new Vector3(0, -y, 0) + sway * (y / Math.Max(len, 0.1f));
        }
        _peg.Position = new Vector3(0, 0.02f, 0);
    }
}

/// <summary>The Elementalist's blizzard (or firestorm): a churning storm-cloud over the spot (grey-blue,
/// or for a firestorm a smoky red lit from within) and a faint swirl beneath it; the snow and the
/// sparks themselves are particles.</summary>
public partial class BlizzardView : PropView
{
    private readonly MeshInstance3D[] _puffs = new MeshInstance3D[6];
    private MeshInstance3D _swirl;
    private OmniLight3D _light;
    private Color _col, _cloud;
    private bool _fire;

    protected override void Build()
    {
        var z = (Blizzard)Owner2D;
        _fire = z.Fire;
        _col = _fire ? new Color(1f, 0.5f, 0.15f) : new Color(0.8f, 0.94f, 1f);
        _cloud = _fire ? new Color(0.42f, 0.16f, 0.08f) : new Color(0.62f, 0.7f, 0.82f);
        for (int k = 0; k < _puffs.Length; k++)
        {
            _puffs[k] = new MeshInstance3D { Mesh = PropViews.Quad, MaterialOverride = PropViews.CloudMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _puffs[k].SetInstanceShaderParameter("cloud_seed", k * 1.61f + 0.4f);
            _puffs[k].SetInstanceShaderParameter("cloud_glow", _fire ? 0.6f : 0.25f);
            AddChild(_puffs[k]);
        }
        _swirl = PropViews.Sprite(_col, 7, 0.45f, W3.M(z.Radius));
        AddChild(_swirl);
        _light = PropViews.Light(_col, 0.7f, 3.5f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var z = (Blizzard)Owner2D;
        Follow(default, 0.2f);
        float s = z.Strength, r = W3.M(z.Radius);
        // the cloud: puffs churning in a flat heap over the storm
        for (int k = 0; k < _puffs.Length; k++)
        {
            float a = k * 1.05f + z.Age * (0.6f + 0.1f * k);
            _puffs[k].Position = new Vector3(MathF.Cos(a) * r * 0.75f, r * 1.05f + MathF.Sin(a * 1.3f) * r * 0.15f, MathF.Sin(a) * r * 0.3f);
            _puffs[k].Scale = Vector3.One * r * (0.75f + 0.12f * (k % 3));
            _puffs[k].SetInstanceShaderParameter("cloud_color", new Color(_cloud, 0.6f * s));
        }
        _swirl.Scale = Vector3.One * r * 1.2f;
        PropViews.SetSprite(_swirl, new Color(_col, 0.35f * s), 7, 0.45f);
        _light.LightEnergy = (_fire ? 0.9f + 0.3f * MathF.Sin(z.Age * 29f) : 0.6f) * s;
    }
}

/// <summary>A creature frozen solid: jagged crystals of ice around it, catching the light, that grow
/// in as it freezes.</summary>
public partial class IceBlockView : PropView
{
    private MeshInstance3D _ice, _glow;

    protected override void Build()
    {
        var b = (IceBlock)Owner2D;
        float r = W3.M(b.Size);
        var rng = new Random((int)(GetInstanceId() % 10000));
        var mb = new MeshBuilder();
        // a ring of crystals leaning out from the creature's middle, the biggest ones low down
        for (int k = 0; k < 9; k++)
        {
            float a = k * Mathf.Tau / 9f + (float)rng.NextDouble() * 0.4f;
            var dir = new Vector3(MathF.Cos(a), MathF.Sin(a) * 0.9f, ((float)rng.NextDouble() - 0.5f) * 0.9f).Normalized();
            float big = 1f - 0.35f * Math.Max(0f, dir.Y);
            DesignKit.CrystalAt(mb, dir * r * 0.35f, dir, r * 0.32f * big, r * (1.05f + 0.3f * (float)rng.NextDouble()) * big, Colors.White, (float)rng.NextDouble());
        }
        _ice = PropViews.Mesh(mb, PropViews.Ice, false);
        AddChild(_ice);
        _glow = PropViews.Sprite(new Color(0.7f, 0.9f, 1f), 0, 0.5f, r * 2.6f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var b = (IceBlock)Owner2D;
        Follow(default, 0.12f);
        float grow = Math.Clamp(b.Age / 0.12f, 0.2f, 1f);
        _ice.Scale = Vector3.One * grow;
    }
}

/// <summary>The Rogue's thrown dagger: the blade itself, pointing along its flight (or where it went
/// in), a glint on it, and a faint streak behind it while it flies.</summary>
public partial class ThrownDaggerView : PropView
{
    private MeshInstance3D _blade, _glint;
    // (a trail of fading motes behind it in flight: billboards can't turn, so no single streak)
    private readonly MeshInstance3D[] _trail = new MeshInstance3D[3];
    private static Mesh _mesh;

    protected override void Build()
    {
        _mesh ??= PropMeshes.Sword(0.3f, 0.026f, new Color(0.78f, 0.8f, 0.84f), new Color(0.6f, 0.46f, 0.2f), new Color(0.1f, 0.08f, 0.07f), 0.05f)
            .ToMesh(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Metallic = 0.8f, Roughness = 0.3f });
        _blade = new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * 1.6f };
        AddChild(_blade);
        _glint = PropViews.Sprite(new Color(1f, 0.97f, 0.9f), 3, 1.2f, 0.18f);
        AddChild(_glint);
        for (int k = 0; k < _trail.Length; k++)
        {
            _trail[k] = PropViews.Sprite(new Color(0.8f, 0.87f, 1f), 0, 0.9f - k * 0.25f, 0.09f - k * 0.02f);
            AddChild(_trail[k]);
        }
    }

    protected override void Sync(float dt)
    {
        var d = (ThrownDagger)Owner2D;
        Follow(default, 0.25f);
        var p = d.Pointing;
        // (the blade runs down -Y from its grip: turn -Y onto the way it points, 2D y-down flipped)
        bool flying = d.State == ThrownDagger.Phase.Flying;
        if (flying)
        {
            // end over end, fast: a spinning knife, turning about its middle
            float ang = MathF.Atan2(d.Dir.X, d.Dir.Y) + d.Age * 34f;
            _blade.Rotation = new Vector3(0, 0, ang);
            var mid = new Vector3(0f, -0.11f * 1.6f, 0f);
            _blade.Position = -(new Basis(Vector3.Back, ang) * mid);
        }
        else
        {
            _blade.Rotation = new Vector3(0, 0, MathF.Atan2(p.X, p.Y));
            _blade.Position = new Vector3(-p.X, p.Y, 0) * 0.2f;
        }
        var back = new Vector3(-d.Dir.X, d.Dir.Y, 0);
        for (int k = 0; k < _trail.Length; k++)
        {
            _trail[k].Visible = flying;
            _trail[k].Position = back * (0.1f + 0.12f * k);
        }
        _glint.Scale = Vector3.One * (0.12f + 0.08f * MathF.Abs(MathF.Sin(d.Age * 9f)));
    }
}

/// <summary>The Rogue's smoke bomb: a heap of thick grey smoke, churning, billowing up and thinning away.</summary>
public partial class SmokeCloudView : PropView
{
    private readonly MeshInstance3D[] _puffs = new MeshInstance3D[9];

    protected override void Build()
    {
        for (int k = 0; k < _puffs.Length; k++)
        {
            _puffs[k] = new MeshInstance3D { Mesh = PropViews.Quad, MaterialOverride = PropViews.CloudMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _puffs[k].SetInstanceShaderParameter("cloud_seed", k * 1.29f + 0.7f);
            _puffs[k].SetInstanceShaderParameter("cloud_glow", 0.05f);
            AddChild(_puffs[k]);
        }
    }

    protected override void Sync(float dt)
    {
        var c = (SmokeCloud)Owner2D;
        Follow(default, 0.35f);
        float a = c.Thickness, r = W3.M(c.Radius) * (0.6f + 0.4f * Math.Min(1f, c.Age * 3f));
        for (int k = 0; k < _puffs.Length; k++)
        {
            float ang = k * 0.7f + c.Age * (0.25f + 0.05f * (k % 3));
            _puffs[k].Position = new Vector3(MathF.Cos(ang) * r * 0.55f, MathF.Sin(ang * 1.3f) * r * 0.35f + r * 0.1f, MathF.Sin(ang) * r * 0.4f);
            _puffs[k].Scale = Vector3.One * r * (0.8f + 0.12f * (k % 3));
            _puffs[k].SetInstanceShaderParameter("cloud_color", new Color(0.32f, 0.32f, 0.36f, 0.75f * a));
        }
    }
}

// ============================================================================ pickups

public partial class XpOrbView : PropView
{
    private MeshInstance3D _gem, _glow;

    protected override void Build()
    {
        var o = (XpOrb)Owner2D;
        float s = (2.2f + Math.Min(o.Value, 12) * 0.18f) / W3.Ppu * 1.6f;
        var mb = new MeshBuilder();
        DesignKit.CrystalAt(mb, new Vector3(0, 0, 0), Vector3.Up, s * 0.55f, s * 1.1f, Colors.White);
        DesignKit.CrystalAt(mb, new Vector3(0, 0, 0), Vector3.Down, s * 0.55f, s * 1.1f, Colors.White, 0.5f);
        _gem = PropViews.Mesh(mb, PropViews.Emissive(new Color(0.4f, 1f, 0.7f), 1.6f), false);
        AddChild(_gem);
        _glow = PropViews.Sprite(new Color(0.4f, 1f, 0.7f), 0, 0.5f, s * 2.4f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var o = (XpOrb)Owner2D;
        Follow(new Vector2(0, MathF.Sin(o.T * 5) * 1.2f), 0.25f);
        _gem.Rotation = new Vector3(0, o.T * 3f, 0.3f);
    }
}

public partial class HeartView : PropView
{
    private MeshInstance3D _gem, _glow;
    private OmniLight3D _light;

    protected override void Build()
    {
        // a heart of ruby: two lobes and a point, faceted
        var mb = new MeshBuilder();
        mb.Blob(new Vector3(-0.13f, 0.06f, 0), new Vector3(0.17f, 0.17f, 0.12f), 4, Colors.White, new Noise3(1), 0f, 1f);
        mb.Blob(new Vector3(0.13f, 0.06f, 0), new Vector3(0.17f, 0.17f, 0.12f), 4, Colors.White, new Noise3(2), 0f, 1f);
        DesignKit.CrystalAt(mb, new Vector3(0, 0.08f, 0), Vector3.Down, 0.2f, 0.36f, Colors.White);
        _gem = PropViews.Mesh(mb, PropViews.Emissive(new Color(1f, 0.2f, 0.3f), 1.4f), false);
        AddChild(_gem);
        _glow = PropViews.Sprite(new Color(1f, 0.3f, 0.4f), 0, 1f, 0.8f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(1f, 0.3f, 0.4f), 0.6f, 2.5f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var h = (HeartPickup)Owner2D;
        Follow(default, 0.25f);
        bool blink = h.LifeLeft < 4 && (int)(h.T * 8) % 2 == 0;
        Visible = !blink;
        float s = 1f + 0.1f * MathF.Sin(h.T * 6);
        _gem.Scale = Vector3.One * s;
        _gem.Rotation = new Vector3(0, MathF.Sin(h.T * 1.5f) * 0.6f, 0);
    }
}

public partial class PotionView : PropView
{
    private Node3D _flask;
    private MeshInstance3D _glow;

    protected override void Build()
    {
        _flask = new Node3D();
        AddChild(_flask);
        // a round-bellied flask: red liquid inside a glass skin, a cork
        var liquid = new MeshBuilder();
        DecorMeshes.AddSphere(liquid, new Vector3(0, 0, 0), 0.26f, Colors.White, 8);
        _flask.AddChild(PropViews.Mesh(liquid, PropViews.Emissive(new Color(1f, 0.2f, 0.35f), 0.9f), false));
        var glass = new MeshBuilder();
        DecorMeshes.AddSphere(glass, new Vector3(0, 0, 0), 0.31f, Colors.White, 8);
        glass.Tube(new[] { new Vector3(0, 0.22f, 0), new Vector3(0, 0.5f, 0) }, new[] { 0.1f, 0.09f }, 10, Colors.White, capStart: false);
        _flask.AddChild(PropViews.Mesh(glass, PropViews.Glass, false));
        var cork = new MeshBuilder();
        cork.Tube(new[] { new Vector3(0, 0.46f, 0), new Vector3(0, 0.6f, 0) }, new[] { 0.085f, 0.08f }, 8, new Color(0.55f, 0.36f, 0.2f), capStart: true);
        _flask.AddChild(PropViews.Mesh(cork, PropViews.VertexColored));
        _glow = PropViews.Sprite(new Color(1f, 0.3f, 0.45f), 0, 0.8f, 0.9f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var p = (PotionPickup)Owner2D;
        Follow(new Vector2(0, -2), 0.25f);
        Visible = !(p.LifeLeft < 5 && (int)(p.T * 8) % 2 == 0);
        _flask.Scale = Vector3.One * (1f + 0.08f * MathF.Sin(p.T * 5));
        _flask.Rotation = new Vector3(0, p.T * 1.2f, MathF.Sin(p.T * 2f) * 0.15f);
    }
}

/// <summary>An iron key, gold-bright: its bow, shaft and teeth, turning slowly as it bobs, with a glint and a little light.</summary>
public partial class KeyView : PropView
{
    private Node3D _key;
    private MeshInstance3D _glow;
    private OmniLight3D _light;

    protected override void Build()
    {
        _key = new Node3D();
        AddChild(_key);
        var mb = new MeshBuilder();
        // the bow: a ring
        var ring = new List<Vector3>();
        for (int k = 0; k <= 16; k++) { float a = k / 16f * Mathf.Tau; ring.Add(new Vector3(-0.3f + MathF.Cos(a) * 0.14f, MathF.Sin(a) * 0.14f, 0)); }
        mb.Tube(ring, ring.Select(_ => 0.04f).ToList(), 6, Colors.White, false);
        // the shaft and its teeth
        mb.Tube(new List<Vector3> { new(-0.16f, 0, 0), new(0.34f, 0, 0) }, new List<float> { 0.035f, 0.035f }, 6, Colors.White, true);
        PropMeshes.Box(mb, new Vector3(0.24f, -0.07f, 0), new Vector3(0.03f, 0.06f, 0.025f), Colors.White);
        PropMeshes.Box(mb, new Vector3(0.32f, -0.06f, 0), new Vector3(0.025f, 0.05f, 0.025f), Colors.White);
        _key.AddChild(PropViews.Mesh(mb, PropViews.Gold, false));
        _glow = PropViews.Sprite(new Color(1f, 0.82f, 0.4f), 0, 0.7f, 0.9f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(1f, 0.8f, 0.45f), 0.6f, 2.6f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var k = (KeyPickup)Owner2D;
        Follow(new Vector2(0, -4 + MathF.Sin(k.T * 3f) * 1.2f), 0.2f);
        _key.Rotation = new Vector3(0.25f, k.T * 1.4f, 0);
        _key.Scale = Vector3.One * 1.5f;
        // (a hidden key is dimmer: it's there to be found)
        float a = k.Stashed ? 0.55f : 1f;
        PropViews.SetSprite(_glow, new Color(1f, 0.82f, 0.4f, a * (0.6f + 0.25f * MathF.Sin(k.T * 4f))), 0, 0.7f);
        _light.LightEnergy = a * (0.5f + 0.15f * MathF.Sin(k.T * 4f));
    }
}

/// <summary>
/// A vault's gate: an iron portcullis, its face of bars toward you where the passage meets the
/// rock's front and more bars running back through the passage's depth, a heavy gold padlock
/// on its face and a lamp's glow on the iron. Opened, the lock falls away and the gate grinds up
/// into the rock overhead.
/// </summary>
public partial class VaultGateView : PropView
{
    private Node3D _grate, _lock;
    private OmniLight3D _lamp;

    protected override void Build()
    {
        var g = (VaultGate)Owner2D;
        float h = W3.M(g.Height);
        _grate = new Node3D();
        AddChild(_grate);
        var iron = new MeshBuilder();
        var dark = new Color(0.32f, 0.31f, 0.34f);
        void Bar(Vector3 a, Vector3 b, float r) => iron.Tube(new List<Vector3> { a, b }, new List<float> { r, r }, 6, dark, true);
        // the face you see: a grid of bars across the passage, spiked at the foot
        const float fz = 0.9f;
        for (int k = -2; k <= 2; k++)
        {
            float x = k * 0.19f;
            Bar(new Vector3(x, -0.2f, fz), new Vector3(x, h + 0.4f, fz), 0.05f);
            DesignKit.CrystalAt(iron, new Vector3(x, 0.02f, fz), Vector3.Down, 0.06f, 0.2f, dark);
        }
        foreach (float y in new[] { 0.35f, h * 0.5f, h - 0.25f })
            Bar(new Vector3(-0.48f, y, fz), new Vector3(0.48f, y, fz), 0.045f);
        // and bars back through the passage's depth to its back wall
        for (int k = 0; k < 4; k++)
        {
            float z = Mathf.Lerp(-2.3f, 0.3f, k / 3f);
            Bar(new Vector3(0, -0.2f, z), new Vector3(0, h + 0.4f, z), 0.05f);
        }
        foreach (float y in new[] { 0.35f, h * 0.5f, h - 0.25f })
            Bar(new Vector3(0, y, -2.3f), new Vector3(0, y, fz), 0.04f);
        _grate.AddChild(PropViews.Mesh(iron, PropViews.Steel));
        // the padlock on its face
        _lock = new Node3D { Position = new Vector3(0, h * 0.44f, fz + 0.1f) };
        AddChild(_lock);
        var lockBody = new MeshBuilder();
        PropMeshes.Box(lockBody, Vector3.Zero, new Vector3(0.22f, 0.19f, 0.07f), Colors.White);
        _lock.AddChild(PropViews.Mesh(lockBody, PropViews.Gold));
        var shackle = new MeshBuilder();
        var arc = new List<Vector3>();
        for (int k = 0; k <= 10; k++) { float a = k / 10f * Mathf.Pi; arc.Add(new Vector3(MathF.Cos(a) * 0.13f, 0.19f + MathF.Sin(a) * 0.15f, 0)); }
        shackle.Tube(arc, arc.Select(_ => 0.035f).ToList(), 6, Colors.White, true);
        _lock.AddChild(PropViews.Mesh(shackle, PropViews.Steel));
        var hole = new MeshBuilder();
        PropMeshes.Box(hole, new Vector3(0, -0.03f, 0.072f), new Vector3(0.03f, 0.065f, 0.004f), new Color(0.05f, 0.04f, 0.03f));
        _lock.AddChild(PropViews.Mesh(hole, PropViews.VertexColored, false));
        // a lamp's glow on the iron, so it reads in the dark
        _lamp = PropViews.Light(new Color(1f, 0.78f, 0.45f), 0.9f, 3.4f);
        _lamp.Position = new Vector3(-0.4f, h * 0.7f, 1.8f);
        AddChild(_lamp);
    }

    protected override void Sync(float dt)
    {
        var g = (VaultGate)Owner2D;
        Follow(default, 0f);
        float h = W3.M(g.Height);
        // opened, it grinds up into the rock
        float rise = W3.Smooth01(g.Rise) * (h + 0.6f);
        // (rattled without a key, it shudders)
        float shake = g.Rattle > 0 ? MathF.Sin(Time * 60f) * 0.025f * g.Rattle : 0f;
        _grate.Position = new Vector3(shake, rise, 0);
        _lock.Visible = !g.Opened;
        _lock.Position = new Vector3(shake, _lock.Position.Y, _lock.Position.Z);
        _lamp.LightEnergy = g.Opened ? Math.Max(0f, 0.9f - g.OpenT * 0.5f) : 0.8f + 0.1f * MathF.Sin(Time * 2.5f);
    }
}

public partial class ChestView : PropView
{
    private Node3D _lid, _web, _float;
    private float _floatY;
    private MeshInstance3D _shine;
    private OmniLight3D _light;
    private Label3D _owner;
    private bool _vault;
    private ChestTier _tier;
    private Color _tint;

    protected override void Build()
    {
        var c0 = (Chest)Owner2D;
        _tier = c0.Tier;
        bool boss = _tier == ChestTier.Boss;
        // (a guardian's chest is bigger)
        float k = boss ? 1.3f : 1f;
        float w = 0.72f * k, d = 0.5f * k, h = 0.46f * k;
        // looks by what's inside: wood with gold bands (plain), dark violet with silver and a rose gem (a relic),
        // bright gold and studded (a guardian's), black iron bound in gold (the vault's)
        _vault = c0.Vault;
        Color wood, lidCol;
        Material bandMat = PropViews.Gold;
        if (_vault) { wood = new Color(0.13f, 0.12f, 0.15f); lidCol = new Color(0.16f, 0.15f, 0.19f); _tint = new Color(0.85f, 0.6f, 1f); }
        else if (_tier == ChestTier.Relic) { wood = new Color(0.2f, 0.12f, 0.3f); lidCol = new Color(0.26f, 0.16f, 0.38f); bandMat = PropViews.Steel; _tint = new Color(1f, 0.45f, 0.62f); }
        else if (boss) { wood = new Color(0.72f, 0.5f, 0.14f); lidCol = new Color(0.82f, 0.6f, 0.18f); _tint = new Color(1f, 0.9f, 0.45f); }
        else { wood = new Color(0.38f, 0.22f, 0.11f); lidCol = new Color(0.44f, 0.26f, 0.13f); _tint = new Color(1f, 0.85f, 0.4f); }
        // upgrades don't come in chests: a shrine, three prongs hovering over a stone (for the three choices)
        if (!_vault && (_tier == ChestTier.Wood || boss))
        {
            _tint = boss ? new Color(1f, 0.9f, 0.45f) : new Color(0.5f, 0.92f, 1f);
            BuildShrine(boss, _tint);
            _lid = new Node3D();
            AddChild(_lid);
        }
        else
        {
            var body = new MeshBuilder();
            PropMeshes.Box(body, new Vector3(0, h * 0.5f, 0), new Vector3(w, h * 0.5f, d), wood);
            AddChild(PropViews.Mesh(body, PropViews.VertexColored));
            var bands = new MeshBuilder();
            foreach (float x in new[] { -w * 0.7f, w * 0.7f })
                PropMeshes.Box(bands, new Vector3(x, h * 0.5f, 0), new Vector3(0.05f, h * 0.52f, d * 1.02f), Colors.White);
            PropMeshes.Box(bands, new Vector3(0, h * 0.15f, 0), new Vector3(w * 1.02f, 0.04f, d * 1.02f), Colors.White);
            PropMeshes.Box(bands, new Vector3(0, h * 0.62f, d * 1.03f), new Vector3(0.09f, 0.1f, 0.02f), Colors.White);
            if (boss)
            {
                // studs along the corners, a heavy lock plate
                foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { 0.2f, 0.85f })
                    PropMeshes.Box(bands, new Vector3(sx * w * 0.93f, h * sy, d * 1.04f), new Vector3(0.045f, 0.045f, 0.03f), Colors.White);
                PropMeshes.Box(bands, new Vector3(0, h * 0.62f, d * 1.05f), new Vector3(0.15f, 0.15f, 0.025f), Colors.White);
            }
            AddChild(PropViews.Mesh(bands, bandMat));
            // the lid, hinged at the back edge
            _lid = new Node3D { Position = new Vector3(0, h, -d) };
            AddChild(_lid);
            var lid = new MeshBuilder();
            PropMeshes.Box(lid, new Vector3(0, 0.12f, d), new Vector3(w * 1.02f, 0.12f, d * 1.02f), lidCol);
            _lid.AddChild(PropViews.Mesh(lid, PropViews.VertexColored));
            var lidBands = new MeshBuilder();
            foreach (float x in new[] { -w * 0.7f, w * 0.7f })
                PropMeshes.Box(lidBands, new Vector3(x, 0.13f, d), new Vector3(0.05f, 0.13f, d * 1.04f), Colors.White);
            _lid.AddChild(PropViews.Mesh(lidBands, bandMat));
            if (_tier == ChestTier.Relic)
            {
                // a rose gem set in the lid's front
                var gem = new MeshBuilder();
                PropMeshes.Box(gem, new Vector3(0, 0.15f, d * 2.05f), new Vector3(0.07f, 0.07f, 0.03f), new Color(1f, 0.4f, 0.6f), new Basis(Vector3.Back, Mathf.Pi / 4));
                _lid.AddChild(PropViews.Mesh(gem, PropViews.Emissive(new Color(1f, 0.4f, 0.6f), 1.6f), false));
            }

        }
        bool shrine = _float != null;
        // (a shrine's own shape glows: the wash of light round it is kept faint so the shape reads)
        _shine = PropViews.Sprite(_tint, 4, shrine ? 0.35f : boss ? 1.6f : 1.2f, shrine ? 0.9f : boss ? 1.9f : 1.4f);
        _shine.Position = new Vector3(0, shrine ? _floatY + 0.1f : h + 0.2f, 0);
        AddChild(_shine);
        _light = PropViews.Light(_tint.Lerp(Colors.White, 0.2f), shrine ? 0.45f : boss ? 1.3f : 0.8f, boss ? 5.5f : 4f);
        _light.Position = new Vector3(0, shrine ? _floatY + 0.3f : h + 0.4f, 0.4f);
        AddChild(_light);

        if (c0.Hung) BuildWeb(c0, w, h, d);
        // whose it is, online (until they've looked and left it)
        _owner = new Label3D
        {
            Text = "", Modulate = new Color(1f, 0.9f, 0.55f), OutlineModulate = new Color(0.05f, 0.04f, 0.03f), FontSize = 48, PixelSize = 0.011f, OutlineSize = 16,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1, Position = new Vector3(0, h + 0.95f, 0.3f), Visible = false,
        };
        AddChild(_owner);
    }

    /// <summary>
    /// A shrine of upgrades: nothing set on the ground. A glowing, flat, three-pronged shape floats loose in
    /// the dungeon (a triangular body, wide at the top and tapering to a heavy point beneath, and three
    /// spikes rising from it), sinking slowly to the floor, swinging on its heavy point like a pendulum, and
    /// settling upright.
    /// </summary>
    private void BuildShrine(bool boss, Color glow)
    {
        float k = boss ? 2.6f : 1.9f;
        _floatY = 0.1f * k;
        _float = new Node3D { Position = new Vector3(0, _floatY, 0) };
        AddChild(_float);
        float t = 0.035f * k;     // half thickness
        void Poly(MeshBuilder mb, Color c, params Vector2[] p)
        {
            // (a flat convex polygon, a front and a back; the rim is bevelled by a smaller one in front)
            var front = new int[p.Length]; var back = new int[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                front[i] = mb.Add(new Vector3(p[i].X * k, p[i].Y * k, t), Vector3.Back, c);
                back[i] = mb.Add(new Vector3(p[i].X * k, p[i].Y * k, -t), Vector3.Forward, c);
            }
            for (int i = 1; i < p.Length - 1; i++) { mb.Tri(front[0], front[i], front[i + 1]); mb.Tri(back[0], back[i + 1], back[i]); }
            for (int i = 0; i < p.Length; i++)
            {
                int j = (i + 1) % p.Length;
                var edge = new Vector3((p[j].Y - p[i].Y), -(p[j].X - p[i].X), 0).Normalized();
                int a0 = mb.Add(new Vector3(p[i].X * k, p[i].Y * k, t), edge, c), a1 = mb.Add(new Vector3(p[j].X * k, p[j].Y * k, t), edge, c);
                int b0 = mb.Add(new Vector3(p[i].X * k, p[i].Y * k, -t), edge, c), b1 = mb.Add(new Vector3(p[j].X * k, p[j].Y * k, -t), edge, c);
                mb.Quad(a0, b0, b1, a1); mb.Quad(a0, a1, b1, b0);
            }
        }
        // three leaf-blades fanning from one point at the bottom (the heavy end): a tall one upright and a shorter one
        // either side leaning out, each a long diamond, widest a little below its middle
        void Blade(MeshBuilder mb, Vector2 baseP, Vector2 tip, float halfWidth, float widest = 0.45f)
        {
            var d = tip - baseP; var dir = d.Normalized(); var perp = new Vector2(-dir.Y, dir.X);
            var mid = baseP + d * widest;
            Poly(mb, Colors.White, baseP, mid + perp * halfWidth * -1f, tip, mid + perp * halfWidth);
        }
        var body = new MeshBuilder();
        Blade(body, new Vector2(0f, 0f), new Vector2(0f, 0.47f), 0.085f, 0.46f);
        Blade(body, new Vector2(-0.045f, 0f), new Vector2(-0.36f, 0.34f), 0.07f);
        Blade(body, new Vector2(0.045f, 0f), new Vector2(0.36f, 0.34f), 0.07f);
        _float.AddChild(PropViews.Mesh(body, PropViews.Emissive(glow, 1.1f), false));
        // a brighter heart in the middle blade, and a halo
        var core = new MeshBuilder();
        Blade(core, new Vector2(0f, 0.05f), new Vector2(0f, 0.34f), 0.04f, 0.46f);
        var coreNode = PropViews.Mesh(core, PropViews.Emissive(glow.Lerp(Colors.White, 0.6f), 2.0f), false);
        coreNode.Position = new Vector3(0, 0, t * 0.6f);
        _float.AddChild(coreNode);
        var halo = PropViews.Sprite(glow, 0, 0.3f, 0.8f * k);
        halo.Position = new Vector3(0, 0.2f * k, 0);
        _float.AddChild(halo);
    }

    /// <summary>A chest strung up in a web: a thread to the ceiling and a pale cocoon of silk round it.</summary>
    private void BuildWeb(Chest c, float w, float h, float d)
    {
        _web = new Node3D();
        AddChild(_web);
        var silk = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.92f, 0.96f, 0.62f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true, Emission = new Color(0.6f, 0.62f, 0.7f), EmissionEnergyMultiplier = 0.35f, Roughness = 0.4f,
        };
        var mb = new MeshBuilder();
        float top = Math.Max(1.5f, W3.M(c.GlobalPosition.Y - c.TopY));
        // the thread (a slightly wavering line), and the strands spreading from it to the chest's corners
        var path = new System.Collections.Generic.List<Vector3>(); var radii = new System.Collections.Generic.List<float>();
        for (int k = 0; k <= 8; k++) { float t = k / 8f; path.Add(new Vector3(0.04f * MathF.Sin(t * 9f), h + (top - h) * t, 0)); radii.Add(0.028f); }
        mb.Tube(path, radii, 5, new Color(0.93f, 0.93f, 0.97f));
        foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
        {
            var a = new Vector3(sx * w * 0.95f, h * 0.9f, sz * d * 0.9f); var b = new Vector3(0, h + 0.9f, 0);
            mb.Tube(new[] { a, (a + b) * 0.5f + new Vector3(0, 0.05f, 0), b }, new[] { 0.014f, 0.012f, 0.014f }, 4, new Color(0.93f, 0.93f, 0.97f));
        }
        _web.AddChild(PropViews.Mesh(mb, silk, false));
        // the wrap: lumpy silk over the chest
        var wrap = new MeshBuilder();
        wrap.Blob(new Vector3(0, h * 0.55f, 0), new Vector3(w * 1.12f, h * 0.85f, d * 1.25f), 7, new Color(0.94f, 0.94f, 0.98f), new Noise3(91), 0.12f, 2f, 0f);
        _web.AddChild(PropViews.Mesh(wrap, silk, false));
    }

    protected override void Sync(float dt)
    {
        var c = (Chest)Owner2D;
        Follow(default, 0f);
        Rotation = new Vector3(0, 0, -c.Tilt);
        if (_float != null)
        {
            // (the shrine's shape sinks loose from the air, swinging on its heavy point as a pendulum would,
            // then settles upright and hangs a hand above the floor, turning a little; taken, it shrinks away)
            float lift = W3.M(c.ShrineLift);
            float sway = 0.32f * MathF.Sin(Time * 1.9f) * Math.Clamp(lift / 2f, 0.12f, 1f);
            _float.Position = new Vector3(0, _floatY + lift + 0.03f * MathF.Sin(Time * 2f), 0);
            _float.Rotation = new Vector3(0, 0.55f * MathF.Sin(Time * 0.9f), sway);
            _float.Scale = Vector3.One * (c.Open ? Math.Max(0.001f, 1f - c.OpenT * 3f) : 1f);
        }
        // the web holds until it's cut (then it parts: the thread goes, and the silk)
        if (_web != null) _web.Visible = c.Hung && c.CutT < 0;
        bool owned = c.Owner != 0 && Net.Online && !c.Open;
        _owner.Visible = owned;
        if (owned) { string t = Net.NameOf(c.Owner) + "'S"; if (_owner.Text != t) _owner.Text = t; }
        float lid = c.Open ? Math.Min(1f, c.OpenT * 5f) : 0f;
        _lid.Rotation = new Vector3(-lid * 1.9f, 0, 0);
        bool boss = _tier == ChestTier.Boss;
        if (c.Open)
        {
            float k = Math.Max(0f, 1f - c.OpenT / 1.5f);
            _shine.Visible = k > 0.01f;
            _shine.Scale = Vector3.One * (1.4f + 2f * (1 - k));
            PropViews.SetSprite(_shine, new Color(1f, 0.9f, 0.5f, k), 4, 2.5f);
            _light.LightEnergy = 0.3f + 3f * k;
        }
        else
        {
            if (_float != null) { _shine.Position = new Vector3(0, _floatY, 0); PropViews.SetSprite(_shine, new Color(_tint, 0.1f + 0.04f * MathF.Sin(Time * 3)), 4, 0.55f); }
            else PropViews.SetSprite(_shine, new Color(_tint, 0.35f + 0.1f * MathF.Sin(Time * 3)), 4, boss ? 1.4f : 1f);
            _light.LightColor = _tint.Lerp(Colors.White, 0.2f);
            _light.LightEnergy = (_float != null ? (boss ? 0.8f : 0.45f) : (boss ? 1.2f : 0.7f)) + 0.15f * MathF.Sin(Time * 3);
        }
    }
}

/// <summary>A hero in an iron cage: bars round a glow in the hero's colour, their name overhead, the door swinging open when freed.</summary>
public partial class HeroCageView : PropView
{
    private Node3D _door;
    private OmniLight3D _light;
    private MeshInstance3D _glow;
    private Color _col;

    protected override void Build()
    {
        var c = (HeroCage)Owner2D;
        _col = Hud.HeroColor(c.Hero);
        var iron = new Color(0.12f, 0.12f, 0.15f);
        var mb = new MeshBuilder();
        const float w = 0.5f, h = 0.95f, d = 0.34f;
        PropMeshes.Box(mb, new Vector3(0, 0.03f, 0), new Vector3(w + 0.05f, 0.03f, d + 0.05f), iron);
        PropMeshes.Box(mb, new Vector3(0, h, 0), new Vector3(w + 0.05f, 0.03f, d + 0.05f), iron);
        foreach (float x in new[] { -w, -w / 3f, w / 3f, w })
            foreach (float z in new[] { -d, d })
                PropMeshes.Box(mb, new Vector3(x, h * 0.5f, z), new Vector3(0.022f, h * 0.5f, 0.022f), iron);
        foreach (float z in new[] { -d, d })
            foreach (float y in new[] { 0.3f, 0.62f })
                PropMeshes.Box(mb, new Vector3(0, y, z), new Vector3(w, 0.018f, 0.018f), iron);
        foreach (float x in new[] { -w, w })
            PropMeshes.Box(mb, new Vector3(x, h * 0.5f, 0), new Vector3(0.02f, h * 0.5f, d), iron);
        AddChild(PropViews.Mesh(mb, PropViews.VertexColored));
        // the door in the front bars: it swings open when the hero is freed
        _door = new Node3D { Position = new Vector3(-w, 0, d) };
        AddChild(_door);
        var db = new MeshBuilder();
        PropMeshes.Box(db, new Vector3(w, h * 0.5f, 0), new Vector3(w, h * 0.5f, 0.02f), iron.Lightened(0.08f));
        _door.AddChild(PropViews.Mesh(db, PropViews.VertexColored));
        var gm = new MeshBuilder();
        PropMeshes.Box(gm, new Vector3(0, 0.38f, 0), new Vector3(0.12f, 0.22f, 0.1f), Colors.White);
        PropMeshes.Box(gm, new Vector3(0, 0.66f, 0), new Vector3(0.09f, 0.09f, 0.09f), Colors.White);
        _glow = PropViews.Mesh(gm, PropViews.Emissive(_col, 1.3f), false);
        AddChild(_glow);
        _light = PropViews.Light(_col.Lerp(Colors.White, 0.2f), 0.7f, 3.5f);
        _light.Position = new Vector3(0, 0.5f, 0.5f);
        AddChild(_light);
        AddChild(new Label3D
        {
            Text = c.Hero.ToString().ToUpperInvariant(), Modulate = _col.Lightened(0.35f), OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.9f), FontSize = 40, PixelSize = 0.008f,
            OutlineSize = 12, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1, Position = new Vector3(0, h + 0.35f, 0),
        });
    }

    protected override void Sync(float dt)
    {
        var c = (HeroCage)Owner2D;
        Follow(default, 0f);
        _door.Rotation = new Vector3(0, -Math.Min(1f, c.FreedT * 3f) * 1.8f, 0);
        _glow.Visible = !c.Freed || c.FreedT < 0.6f;
        _light.LightEnergy = (c.Freed ? Math.Max(0f, 1.6f - c.FreedT * 2f) : 0.7f + 0.15f * MathF.Sin(Time * 3f));
    }
}

/// <summary>
/// An exit: a dressed-stone doorway cut into a knuckle of rock, and through it a stairway going
/// down into the dark (fx_stairwell traces the steps behind the opening). It reads as a way
/// down by its depth, not by light: the only glow is a faint breath of the next biome far below
/// and a carved chevron (two for the steep way) on the keystone. The name above it is pale text
/// with a heavy dark outline, and a prompt shows when someone is standing at the door.
/// </summary>
public partial class PortalView : PropView
{
    private const float HalfW = 1.05f, DoorH = 3f;
    private MeshInstance3D _stairs, _glyph;
    private OmniLight3D _breath;
    private Label3D _title, _sub, _prompt;
    private StandardMaterial3D _glyphMat;
    private Color _glow;

    private static ShaderMaterial _stairMat;
    private static ShaderMaterial StairMat => _stairMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_stairwell.gdshader") };
    public static ShaderMaterial StairMatOrNull => _stairMat;
    public static void ReleaseShared() => _stairMat = null;

    protected override void Build()
    {
        var p = (Portal)Owner2D;
        _glow = p.To?.Glow ?? new Color(0.7f, 0.5f, 1f);
        var rockCol = (G.Biome?.Edge ?? new Color(0.35f, 0.32f, 0.3f)).Lerp(new Color(0.24f, 0.22f, 0.2f), 0.5f);
        var stone = rockCol.Lerp(new Color(0.33f, 0.31f, 0.29f), 0.5f);
        var rng = new Random(p.Depth * 31 + (int)(p.To?.Id ?? 0));
        float R() => (float)rng.NextDouble();
        var mb = new MeshBuilder();
        // the rock the stair is cut into
        for (int k = 0; k < 9; k++)
        {
            float a = MathF.PI * (0.08f + 0.84f * k / 8f);
            var at = new Vector3(MathF.Cos(a) * 2.6f, 0.2f + MathF.Sin(a) * 3.1f, -1.4f - 0.6f * R());
            mb.Blob(at, new Vector3(1.3f, 1.2f, 1.2f) * (0.8f + 0.35f * R()), 5, rockCol.Darkened(0.15f + 0.15f * R()), new Noise3(40 + k), 0.35f, 1.4f, 0.3f);
        }
        // jambs: coursed blocks up to the springing of the arch
        foreach (float side in new[] { -1f, 1f })
        {
            float y = 0f;
            for (int k = 0; y < DoorH - HalfW - 0.05f; k++)
            {
                float h = Math.Min(0.42f + 0.12f * R(), DoorH - HalfW - y);
                float w = 0.34f + 0.1f * R();
                var at = new Vector3(side * (HalfW + w), y + h * 0.5f, -0.25f + 0.05f * R());
                var xf = new Transform3D(new Basis(Vector3.Up, (R() - 0.5f) * 0.06f), at);
                mb.Box(xf, new Vector3(w - 0.02f, h * 0.5f - 0.02f, 0.38f), stone.Darkened(0.05f + 0.2f * R()));
                y += h;
            }
        }
        // voussoirs around the arch, the keystone proud of the rest
        const int n = 9;
        for (int k = 0; k < n; k++)
        {
            float a = MathF.PI * (k + 0.5f) / n;
            bool key = k == n / 2;
            float r = HalfW + 0.3f;
            var at = new Vector3(MathF.Cos(a) * r, DoorH - HalfW + MathF.Sin(a) * r, key ? -0.12f : -0.24f);
            var basis = new Basis(Vector3.Back, a - MathF.PI / 2);
            var half = new Vector3(MathF.PI * r / n * 0.5f - 0.02f, key ? 0.4f : 0.31f, key ? 0.5f : 0.38f);
            mb.Box(new Transform3D(basis, at), half, stone.Darkened(key ? 0.02f : 0.08f + 0.15f * R()));
        }
        // a worn threshold slab and some fallen stones
        mb.Box(new Transform3D(Basis.Identity, new Vector3(0, -0.04f, 0.05f)), new Vector3(HalfW + 0.2f, 0.08f, 0.35f), stone.Darkened(0.2f));
        for (int k = 0; k < 4; k++)
        {
            float x = (k % 2 == 0 ? -1 : 1) * (HalfW + 0.9f + 0.9f * R());
            mb.Blob(new Vector3(x, 0.12f, 0.2f + 0.5f * R()), new Vector3(0.3f, 0.2f, 0.26f) * (0.7f + 0.6f * R()), 4, rockCol.Darkened(0.2f), new Noise3(60 + k), 0.3f, 2f, 0.5f);
        }
        AddChild(PropViews.Mesh(mb, PropViews.VertexColored));

        // the stair going down, behind the opening
        _stairs = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(HalfW * 2f, DoorH) }, MaterialOverride = StairMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, DoorH * 0.5f, -0.2f) };
        _stairs.SetInstanceShaderParameter("below_color", _glow);
        _stairs.SetInstanceShaderParameter("stone_color", stone);
        AddChild(_stairs);

        // the chevron cut into the keystone (two for the steep way), holding a little of the light below
        var gb = new MeshBuilder();
        int chevrons = p.Depth - G.Depth >= 2 ? 2 : 1;
        for (int c = 0; c < chevrons; c++)
        {
            float y = DoorH + 0.36f - c * 0.2f + (chevrons - 1) * 0.1f;
            foreach (float side in new[] { -1f, 1f })
            {
                var basis = new Basis(Vector3.Back, side * 0.62f);
                gb.Box(new Transform3D(basis, new Vector3(side * 0.085f, y, 0.4f)), new Vector3(0.12f, 0.026f, 0.02f), Colors.White);
            }
        }
        _glyphMat = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.05f), EmissionEnabled = true, Emission = _glow, EmissionEnergyMultiplier = 0.9f, Roughness = 0.8f };
        _glyph = new MeshInstance3D { Mesh = gb.ToMesh(_glyphMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_glyph);

        // barely a light: enough to tint the threshold and the inner faces of the jambs
        _breath = PropViews.Light(_glow, 0.35f, 2.6f);
        _breath.LightVolumetricFogEnergy = 0f;
        _breath.Position = new Vector3(0, 0.5f, 0.35f);
        AddChild(_breath);

        var dark = new Color(0f, 0f, 0f, 0.92f);
        _title = new Label3D
        {
            Text = p.To?.Name.ToUpperInvariant() ?? "DEEPER", Modulate = _glow.Lerp(Colors.White, 0.6f), OutlineModulate = dark,
            FontSize = 72, PixelSize = 0.011f, OutlineSize = 24, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1,
            Position = new Vector3(0, 5.25f, 0.6f),
        };
        _sub = new Label3D
        {
            Text = p.Label, Modulate = new Color(0.92f, 0.9f, 0.86f), OutlineModulate = dark,
            FontSize = 44, PixelSize = 0.011f, OutlineSize = 18, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1,
            Position = new Vector3(0, 4.62f, 0.6f),
        };
        _prompt = new Label3D
        {
            Text = "", Modulate = new Color(1f, 0.96f, 0.85f), OutlineModulate = dark,
            FontSize = 40, PixelSize = 0.011f, OutlineSize = 16, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1,
            Position = new Vector3(0, 2.35f, 0.9f), Visible = false,
        };
        AddChild(_title);
        AddChild(_sub);
        AddChild(_prompt);
    }

    protected override void Sync(float dt)
    {
        var p = (Portal)Owner2D;
        Follow(new Vector2(0, 30), 0f);
        // it rises out of the floor rather than popping into being
        float grow = W3.Smooth01(p.Age / 0.9f);
        Scale = new Vector3(1f, Math.Max(0.01f, grow), 1f);
        float breathe = 0.85f + 0.15f * MathF.Sin(Time * 1.3f);
        _breath.LightEnergy = 0.35f * grow * breathe;
        _glyphMat.EmissionEnergyMultiplier = (0.6f + 0.5f * p.Near) * breathe;
        float a = Math.Clamp(p.Age * 2f - 0.6f, 0f, 1f);
        _title.Modulate = _title.Modulate with { A = a };
        _title.OutlineModulate = _title.OutlineModulate with { A = 0.92f * a };
        _sub.Modulate = _sub.Modulate with { A = 0.95f * a };
        _sub.OutlineModulate = _sub.OutlineModulate with { A = 0.92f * a };
        _prompt.Visible = p.Near > 0.01f && !p.Used;
        if (_prompt.Visible)
        {
            _prompt.Text = G.Main.UsingPad ? "UP  ·  DESCEND" : "E  ·  DESCEND";
            float pa = p.Near * (0.8f + 0.2f * MathF.Sin(Time * 5f));
            _prompt.Modulate = _prompt.Modulate with { A = pa };
            _prompt.OutlineModulate = _prompt.OutlineModulate with { A = 0.92f * p.Near };
        }
    }
}

/// <summary>
/// The cave mouth at depth 0: the end of the tunnel you came in by, flooded with daylight so
/// bright it washes out to white (one sheet thinning into the tunnel), a warm light spilling in through the dust, and a prompt to leave.
/// </summary>
public partial class MouthView : PropView
{
    private OmniLight3D _light;
    private MeshInstance3D _front;
    private Label3D _title, _prompt;

    private static ShaderMaterial _dayMat;
    private static ShaderMaterial DayMat => _dayMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_daylight.gdshader") };
    public static void ReleaseShared() { _dayMat?.Dispose(); _dayMat = null; }

    // (the view sits on the floor 5.5 m in from the map's left edge; the rock there ends at 3 m)
    private const float Edge = -5.5f;

    protected override void Build()
    {
        // one wash of white over the rock at the map's edge, thinning out gently over several metres into
        // the tunnel: outside is simply brighter than in here (a single source, not two glows)
        _front = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(12f, 7.8f) }, MaterialOverride = DayMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(Edge + 2.4f, 3.3f, 3.8f) };
        _front.SetInstanceShaderParameter("strength", 1f);
        _front.SetInstanceShaderParameter("fade_start", 0.3f);
        AddChild(_front);
        _light = PropViews.Light(new Color(1f, 0.95f, 0.86f), 2.4f, 15f);
        _light.LightVolumetricFogEnergy = 1.4f;
        _light.Position = new Vector3(Edge + 1.8f, 3.4f, 1.2f);
        AddChild(_light);

        var dark = new Color(0.1f, 0.08f, 0.04f, 0.85f);
        _title = new Label3D
        {
            Text = "THE WAY OUT", Modulate = new Color(1f, 0.97f, 0.88f), OutlineModulate = dark,
            FontSize = 64, PixelSize = 0.011f, OutlineSize = 22, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1,
            Position = new Vector3(5.8f, 5.2f, 1f),
        };
        _prompt = new Label3D
        {
            Text = "", Modulate = new Color(1f, 0.96f, 0.85f), OutlineModulate = dark,
            FontSize = 40, PixelSize = 0.011f, OutlineSize = 16, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1,
            Position = new Vector3(5.8f, 4.45f, 1.2f), Visible = false,
        };
        AddChild(_title);
        AddChild(_prompt);
    }

    protected override void Sync(float dt)
    {
        var p = (Portal)Owner2D;
        Follow(new Vector2(0, 30), 0f);
        // the light shifts a little, like sun through moving leaves
        float flicker = 0.92f + 0.08f * MathF.Sin(Time * 0.9f) * MathF.Sin(Time * 2.3f + 1f);
        _light.LightEnergy = 2.4f * flicker;
        _title.Modulate = _title.Modulate with { A = 0.55f + 0.45f * p.Near };
        _prompt.Visible = p.Near > 0.01f && !p.Used;
        if (_prompt.Visible)
        {
            string key = Controls.Name("interact", G.Main.UsingPad).ToUpperInvariant();
            _prompt.Text = Net.Online ? $"{key}  ·  LEAVE TOGETHER (ENDS THE RUN)" : $"{key}  ·  LEAVE THE CAVE (ENDS THE RUN)";
            float pa = p.Near * (0.8f + 0.2f * MathF.Sin(Time * 5f));
            _prompt.Modulate = _prompt.Modulate with { A = pa };
            _prompt.OutlineModulate = _prompt.OutlineModulate with { A = 0.85f * p.Near };
        }
    }
}

/// <summary>Grasping roots: a knot of bark-dark tendrils that sway, and writhe as they close on someone.</summary>
public partial class GraspingRootsView : PropView
{
    private readonly List<(Node3D pivot, float phase, float lean)> _tendrils = new();

    protected override void Build()
    {
        var gr = (GraspingRoots)Owner2D;
        var rng = new Random((int)(GetInstanceId() % 100000));
        float R() => (float)rng.NextDouble();
        var bark = new Color(0.26f, 0.18f, 0.11f);
        var noise = new Noise3(rng.Next(1000));
        // a gnarled mound where they come up through the floor
        var mound = new MeshBuilder();
        mound.Blob(Vector3.Zero, new Vector3(W3.M(gr.Radius) * 0.9f, 0.18f, 0.6f), 5, bark.Darkened(0.2f), noise, 0.35f, 3f, 1f);
        AddChild(PropViews.Mesh(mound, PropViews.VertexColored));
        int n = 9 + rng.Next(4);
        for (int k = 0; k < n; k++)
        {
            float x = (R() * 2f - 1f) * W3.M(gr.Radius) * 0.85f;
            float z = (R() * 2f - 1f) * 0.45f;
            float len = 0.7f + R() * 0.75f;
            var path = new List<Vector3>();
            var radii = new List<float>();
            float bend = (R() - 0.5f) * 0.6f, curl = 0.15f + R() * 0.25f;
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                // up, leaning, and curling over at the tip like a grasping finger
                path.Add(new Vector3(bend * t * len + curl * t * t * t, len * t, 0.1f * MathF.Sin(t * 3f + k)));
                radii.Add(0.08f * (1f - 0.85f * t) + 0.01f);
            }
            var mb = new MeshBuilder();
            mb.Tube(path, radii, 6, bark.Lightened(0.1f * R()), capStart: false);
            mb.SmoothNormals();
            var pivot = new Node3D { Position = new Vector3(x, 0.05f, z) };
            pivot.AddChild(PropViews.Mesh(mb, PropViews.VertexColored));
            AddChild(pivot);
            _tendrils.Add((pivot, R() * 6f, R() * 2f - 1f));
        }
    }

    protected override void Sync(float dt)
    {
        var gr = (GraspingRoots)Owner2D;
        Follow(default, 0f);
        float grip = gr.Grip;
        float sag = gr.Wounded ? 0.65f : 1f;
        foreach (var (pivot, phase, lean) in _tendrils)
        {
            // idle: a slow sway; closing: a quick writhe, bending in toward the middle
            float sway = 0.12f * MathF.Sin(Time * 1.3f + phase) + grip * 0.35f * MathF.Sin(Time * 11f + phase * 2f);
            pivot.Rotation = new Vector3(0.1f * MathF.Sin(Time * 0.9f + phase), 0, sway - lean * 0.2f - Math.Sign(pivot.Position.X) * grip * 0.4f);
            pivot.Scale = new Vector3(1f, sag * (1f + 0.1f * grip), 1f);
        }
    }
}

/// <summary>An unstable ceiling: a web of cracks and a few loose stones that shake before it gives way.</summary>
public partial class CaveInView : PropView
{
    private readonly List<(MeshInstance3D rock, Vector3 at)> _loose = new();

    protected override void Build()
    {
        var rng = new Random((int)(GetInstanceId() % 100000));
        float R() => (float)rng.NextDouble();
        var rock = (G.Biome?.Edge ?? new Color(0.36f, 0.33f, 0.3f)).Darkened(0.1f);
        float reach = W3.M(Tune.CaveIn.Reach);
        // cracks: dark slivers pressed into the ceiling
        var cracks = new MeshBuilder();
        for (int k = 0; k < 6; k++)
        {
            var at = new Vector3((R() * 2f - 1f) * reach, -0.02f, (R() * 2f - 1f) * 0.9f);
            cracks.Box(new Transform3D(new Basis(Vector3.Up, R() * 3f), at), new Vector3(0.35f + R() * 0.5f, 0.03f, 0.025f), new Color(0.05f, 0.04f, 0.035f));
        }
        AddChild(PropViews.Mesh(cracks, PropViews.VertexColored, false));
        for (int k = 0; k < 4; k++)
        {
            var mb = new MeshBuilder();
            mb.Blob(Vector3.Zero, new Vector3(0.18f, 0.14f, 0.16f) * (0.8f + 0.6f * R()), 4, rock.Lerp(new Color(0.5f, 0.46f, 0.4f), R() * 0.3f), new Noise3(rng.Next(1000)), 0.35f, 4f);
            var mi = PropViews.Mesh(mb, PropViews.VertexColored);
            var at = new Vector3((R() * 2f - 1f) * reach * 0.8f, -0.12f - R() * 0.08f, (R() * 2f - 1f) * 0.7f);
            mi.Position = at;
            AddChild(mi);
            _loose.Add((mi, at));
        }
    }

    protected override void Sync(float dt)
    {
        var c = (CaveIn)Owner2D;
        Follow(default, 0f);
        float r = c.Rumbling;
        int k = 0;
        foreach (var (rock, at) in _loose)
        {
            float j = r * 0.035f;
            rock.Position = at + new Vector3(j * MathF.Sin(Time * 47f + k), -r * 0.06f + j * MathF.Sin(Time * 53f + k * 2f), 0);
            k++;
        }
    }
}

/// <summary>
/// The drain at the lowest point of a lake: a round black mouth in the bed, and from just above it all the way down to the bottom of the
/// map a curtain of black that thickens from nothing to solid, so nothing below it can be seen (black water, and a negative light that
/// takes the colour out of the water round it). Nothing to invite anyone in.
/// </summary>
public partial class DrainView : PropView
{
    private OmniLight3D _void;
    private MeshInstance3D _dark;
    private static Shader _shader;
    private static ShaderMaterial _darkMat;

    public static void ReleaseShared() { _darkMat?.Dispose(); _darkMat = null; _shader = null; }

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(Vector3.Zero, new Vector3(1.5f, 0.12f, 1.1f), 6, new Color(0.0f, 0.0f, 0.0f), new Noise3(9), 0.25f, 2f, 1f);
        var mouth = PropViews.Mesh(mb, PropViews.VertexColored, false);
        mouth.Position = new Vector3(0, -0.9f, 0.1f);
        AddChild(mouth);
        // the curtain: from a few metres over the mouth down past the map's floor
        float above = 4.5f, below = W3.M(Math.Max(0f, (G.Cave?.SizePx.Y ?? Owner2D.GlobalPosition.Y) - Owner2D.GlobalPosition.Y)) + 2f;
        float h = above + below;
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/drain_dark.gdshader");
        _darkMat ??= new ShaderMaterial { Shader = _shader };
        _dark = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(13f, h) }, MaterialOverride = _darkMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, above - h * 0.5f, 1.2f) };
        _dark.SetInstanceShaderParameter("fade_frac", Math.Min(0.9f, 5f / h));
        AddChild(_dark);
        _void = PropViews.Light(Colors.White, 2.2f, 8f);
        _void.LightNegative = true;
        _void.LightVolumetricFogEnergy = 1f;
        _void.Position = new Vector3(0, 0.6f, 0.9f);
        AddChild(_void);
    }

    protected override void Sync(float dt)
    {
        Follow(default, 0f);
        _void.LightEnergy = 1.9f + 0.6f * (0.5f + 0.5f * MathF.Sin(Time * 0.9f));
    }
}

/// <summary>A waterfall: two sheets of falling water (one wider and fainter behind), spray at the foot, a faint pale light on what it lands on.</summary>
public partial class WaterfallView : PropView
{
    private MeshInstance3D _spray1, _spray2;
    private OmniLight3D _glow;
    private static ShaderMaterial _mat, _matBack;
    private static Shader _shader;

    private static ShaderMaterial Mat(bool back)
    {
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/waterfall.gdshader");
        var m = new ShaderMaterial { Shader = _shader };
        m.SetShaderParameter("water_color", back ? new Color(0.22f, 0.38f, 0.5f, 0.42f) : new Color(0.3f, 0.5f, 0.64f, 0.7f));
        m.SetShaderParameter("speed", back ? 1.7f : 2.6f);
        m.SetShaderParameter("seed", back ? 11f : 0f);
        return m;
    }
    public static void ReleaseShared() { _mat?.Dispose(); _matBack?.Dispose(); _mat = _matBack = null; _shader = null; }

    protected override void Build()
    {
        var f = (Waterfall)Owner2D;
        float h = W3.M(f.Height), w = W3.M(f.Width);
        foreach (bool back in new[] { true, false })
        {
            var mat = back ? (_matBack ??= Mat(true)) : (_mat ??= Mat(false));
            var q = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(w * (back ? 1.5f : 1f), h) }, MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, h * 0.5f, back ? -0.35f : 0.1f),
            };
            // (the height tells the shader how fast to run the streaks down it)
            q.SetInstanceShaderParameter("height_m", h);
            AddChild(q);
        }
        _spray1 = PropViews.Sprite(new Color(0.8f, 0.92f, 1f), 0, 0.14f, 3.2f);
        _spray1.Position = new Vector3(0, 0.5f, 0.5f);
        AddChild(_spray1);
        _spray2 = PropViews.Sprite(new Color(0.8f, 0.92f, 1f), 0, 0.09f, 5f);
        _spray2.Position = new Vector3(0, 0.9f, 0.7f);
        AddChild(_spray2);
        _glow = PropViews.Light(new Color(0.6f, 0.82f, 1f), 0.5f, 8f);
        _glow.Position = new Vector3(0, 1.2f, 1.2f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        Follow(default, 0f);
        float s = 0.5f + 0.5f * MathF.Sin(Time * 2.3f);
        PropViews.SetSprite(_spray1, new Color(0.8f, 0.92f, 1f, 0.3f + 0.12f * s), 0, 0.14f);
        PropViews.SetSprite(_spray2, new Color(0.8f, 0.92f, 1f, 0.2f + 0.08f * (1f - s)), 0, 0.09f);
        _glow.LightEnergy = 0.45f + 0.15f * s;
    }
}


public partial class AirVentView : PropView
{
    private MeshInstance3D _shimmer;

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(Vector3.Zero, new Vector3(0.45f, 0.06f, 0.35f), 4, new Color(0.02f, 0.03f, 0.05f), new Noise3(4), 0.3f, 3f, 1f);
        AddChild(PropViews.Mesh(mb, PropViews.VertexColored, false));
        _shimmer = PropViews.Sprite(new Color(0.7f, 0.9f, 1f), 0, 0.5f, 0.6f);
        _shimmer.Position = new Vector3(0, 0.35f, 0.2f);
        AddChild(_shimmer);
    }

    protected override void Sync(float dt)
    {
        Follow(new Vector2(0, 1), 0f);
        PropViews.SetSprite(_shimmer, new Color(0.7f, 0.9f, 1f, 0.5f + 0.3f * MathF.Sin(Time * 3)), 0, 0.6f);
    }
}

public partial class AirBubbleView : PropView
{
    private MeshInstance3D _ball, _hl;

    protected override void Build()
    {
        _ball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 }, MaterialOverride = PropViews.BubbleMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ball);
        _hl = PropViews.Sprite(new Color(0.8f, 0.95f, 1f), 0, 0.2f, 0.6f);
        AddChild(_hl);
    }

    protected override void Sync(float dt)
    {
        var b = (AirBubble)Owner2D;
        Follow(default, 0.2f);
        float r = (6f + 0.6f * MathF.Sin(b.T * 5)) / W3.Ppu;
        _ball.Scale = new Vector3(r * (1f + 0.06f * MathF.Sin(b.T * 7)), r * (1f - 0.06f * MathF.Sin(b.T * 7)), r);
    }
}

// ============================================================================ hazards

public partial class SporeCloudView : PropView
{
    private readonly MeshInstance3D[] _puffs = new MeshInstance3D[7];
    private MeshInstance3D _glow;

    protected override void Build()
    {
        for (int k = 0; k < _puffs.Length; k++)
        {
            _puffs[k] = new MeshInstance3D { Mesh = PropViews.Quad, MaterialOverride = PropViews.CloudMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _puffs[k].SetInstanceShaderParameter("cloud_seed", k * 1.37f);
            AddChild(_puffs[k]);
        }
        _glow = PropViews.Sprite(new Color(0.75f, 0.5f, 1f), 4, 0.35f, 2f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var c = (SporeCloud)Owner2D;
        Follow(default, 0.2f);
        float a = Math.Clamp((c.Life - c.T) / 0.6f, 0, 1) * Math.Min(1f, c.T * 4f);
        float r = c.Radius * Math.Min(1f, c.T * 4f) / W3.Ppu;
        for (int k = 0; k < _puffs.Length; k++)
        {
            var o = new Vector3(MathF.Cos(k * 1.1f + c.T), -MathF.Sin(k * 1.7f + c.T * 1.3f), MathF.Sin(k * 2.3f + c.T * 0.7f)) * r * 0.45f;
            _puffs[k].Position = o;
            _puffs[k].Scale = Vector3.One * r * (0.7f + 0.1f * (k % 3));
            _puffs[k].SetInstanceShaderParameter("cloud_color", new Color(0.55f, 0.36f, 0.78f, 0.45f * a));
        }
        _glow.Scale = Vector3.One * r * 1.6f;
        PropViews.SetSprite(_glow, new Color(0.7f, 0.45f, 1f, a), 4, 0.35f);
    }
}

public partial class SporePodView : PropView
{
    private MeshInstance3D _pod, _glow;
    private readonly List<MeshInstance3D> _spots = new();

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.45f, 0.5f), 6, new Color(0.38f, 0.2f, 0.45f), new Noise3(8), 0.12f, 3f, 0.8f);
        _pod = PropViews.Mesh(mb, PropViews.VertexColored);
        AddChild(_pod);
        var spots = new MeshBuilder();
        var rng = new Random(11);
        for (int k = 0; k < 9; k++)
        {
            float a = (float)rng.NextDouble() * Mathf.Tau, y = 0.3f + 0.5f * (float)rng.NextDouble();
            var d = new Vector3(MathF.Cos(a) * 0.45f, y, MathF.Sin(a) * 0.45f);
            DecorMeshes.AddSphere(spots, d, 0.06f + 0.04f * (float)rng.NextDouble(), Colors.White, 4);
        }
        var sm = PropViews.Mesh(spots, PropViews.Emissive(new Color(0.9f, 0.7f, 1f), 1.2f), false);
        _pod.AddChild(sm);
        _glow = PropViews.Sprite(new Color(0.75f, 0.5f, 1f), 0, 0.6f, 1.2f);
        _glow.Position = new Vector3(0, 0.4f, 0.4f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var p = (SporePod)Owner2D;
        Follow(new Vector2(0, 1), 0f);
        float s = 1f + (p.Swell > 0 ? p.Swell * 0.8f : 0.05f * MathF.Sin(Time * 2));
        _pod.Scale = new Vector3(s, s * (p.Swell > 0 ? 1f + p.Swell * 0.3f : 1f), s);
        float ready = p.Primed ? 1f : 0.45f;
        PropViews.SetSprite(_glow, new Color(0.75f, 0.5f, 1f, ready), 0, 0.6f + p.Swell * 3f);
    }
}

public partial class WebView : PropView
{
    protected override void Build()
    {
        var w = (WebPatch)Owner2D;
        float R = w.Radius / W3.Ppu;
        var mb = new MeshBuilder();
        var col = new Color(0.92f, 0.92f, 0.96f);
        void Strand(Vector3 a, Vector3 b, float th)
        {
            var d = (b - a).Normalized();
            var n = d.Cross(Vector3.Back).Normalized() * th;
            int i0 = mb.Add(a - n, Vector3.Back, col), i1 = mb.Add(a + n, Vector3.Back, col), i2 = mb.Add(b + n, Vector3.Back, col), i3 = mb.Add(b - n, Vector3.Back, col);
            mb.Quad(i0, i1, i2, i3);
        }
        var rng = new Random(5);
        var spokes = new Vector3[9];
        for (int k = 0; k < 9; k++)
        {
            float a = k * Mathf.Tau / 9 + 0.2f + (float)(rng.NextDouble() - 0.5) * 0.3f;
            spokes[k] = new Vector3(MathF.Cos(a), MathF.Sin(a), 0) * R * (0.9f + 0.2f * (float)rng.NextDouble());
            Strand(Vector3.Zero, spokes[k], 0.012f);
        }
        for (int ring = 1; ring <= 5; ring++)
        {
            float f = ring / 5.4f;
            for (int k = 0; k < 9; k++)
            {
                var a = spokes[k] * f * (k % 2 == 0 ? 1f : 0.93f);
                var b = spokes[(k + 1) % 9] * f * ((k + 1) % 2 == 0 ? 1f : 0.93f);
                // strands sag a little between spokes
                var mid = (a + b) * 0.5f * 0.94f;
                Strand(a, mid, 0.008f); Strand(mid, b, 0.008f);
            }
        }
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.92f, 0.96f, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true, Emission = new Color(0.6f, 0.62f, 0.7f), EmissionEnergyMultiplier = 0.35f, Roughness = 0.4f,
        };
        AddChild(PropViews.Mesh(mb, mat, false));
    }

    protected override void Sync(float dt)
    {
        Follow(default, 0.1f);
        Rotation = new Vector3(0, 0, MathF.Sin(Time * 0.8f) * 0.02f);
    }
}

public partial class CrystalSpikesView : PropView
{
    private MeshInstance3D _glow;

    protected override void Build()
    {
        var c = (CrystalSpikes)Owner2D;
        var mb = new MeshBuilder();
        var rng = new Random(9);
        var h = c.Heights;
        for (int k = 0; k < h.Length; k++)
        {
            float x = (-c.HalfW + k * (c.HalfW * 2 / (h.Length - 1))) / W3.Ppu;
            float len = h[k] / W3.Ppu * 1.3f;
            var dir = new Vector3((k - 2) * 0.12f, 1f, ((float)rng.NextDouble() - 0.5f) * 0.4f).Normalized();
            DesignKit.CrystalAt(mb, new Vector3(x, -0.05f, ((float)rng.NextDouble() - 0.5f) * 0.6f), dir, 0.09f + 0.03f * (float)rng.NextDouble(), len, new Color(0.55f, 0.78f, 1f), (float)rng.NextDouble());
            DesignKit.CrystalAt(mb, new Vector3(x + 0.1f, -0.05f, ((float)rng.NextDouble() - 0.5f) * 0.9f), (dir + new Vector3(0.3f, 0, 0.2f)).Normalized(), 0.05f, len * 0.55f, new Color(0.6f, 0.82f, 1f), (float)rng.NextDouble());
        }
        var mat = PropViews.Emissive(new Color(0.55f, 0.8f, 1f), 0.9f, 0.82f);
        mat.Roughness = 0.08f; mat.RimEnabled = true; mat.Rim = 0.8f;
        AddChild(PropViews.Mesh(mb, mat, false));
        _glow = PropViews.Sprite(new Color(0.6f, 0.8f, 1f), 0, 0.4f, (c.HalfW + 10) / W3.Ppu);
        _glow.Position = new Vector3(0, 0.4f, 0.4f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        Follow(new Vector2(0, 1), 0f);
        PropViews.SetSprite(_glow, new Color(0.6f, 0.8f, 1f, 0.5f + 0.25f * MathF.Sin(Time * 2)), 0, 0.4f);
    }
}

public partial class FireVentView : PropView
{
    private MeshInstance3D _heat;
    private readonly MeshInstance3D[] _flames = new MeshInstance3D[4];
    private OmniLight3D _light;

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(Vector3.Zero, new Vector3(0.6f, 0.05f, 0.45f), 4, new Color(0.12f, 0.03f, 0.02f), new Noise3(6), 0.3f, 3f, 1f);
        AddChild(PropViews.Mesh(mb, PropViews.VertexColored, false));
        _heat = PropViews.Sprite(new Color(1f, 0.45f, 0.1f), 0, 1f, 0.7f);
        _heat.Position = new Vector3(0, 0.1f, 0.3f);
        AddChild(_heat);
        var cols = new[] { new Color(0.95f, 0.28f, 0.05f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.78f, 0.3f), new Color(1f, 0.95f, 0.75f) };
        for (int k = 0; k < 4; k++)
        {
            _flames[k] = PropViews.Sprite(cols[k], 6, 0.8f + k * 0.18f, 1f, k * 0.37f);
            _flames[k].Visible = false;
            AddChild(_flames[k]);
        }
        _light = PropViews.Light(new Color(1f, 0.5f, 0.15f), 0f, 6f);
        _light.Position = new Vector3(0, 1.5f, 0.8f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var v = (FireVent)Owner2D;
        Follow(new Vector2(0, 1), 0f);
        float c = v.Cycle;
        float glow = c > FireVent.Idle ? Math.Min(1, (c - FireVent.Idle) / FireVent.Warn) : 0.15f;
        PropViews.SetSprite(_heat, new Color(1f, 0.45f, 0.1f, glow), 0, 1.5f);
        bool burn = c >= FireVent.Idle + FireVent.Warn;
        float h = 0f;
        if (burn)
        {
            float k = (c - FireVent.Idle - FireVent.Warn) / FireVent.Burn;
            h = 80 * MathF.Sin(Math.Min(1, k * 3) * Mathf.Pi / 2) * (k > 0.8f ? (1 - k) * 5 : 1) / W3.Ppu;
        }
        for (int k = 0; k < 4; k++)
        {
            _flames[k].Visible = burn && h > 0.05f;
            float fh = h * (1 - k * 0.12f);
            float fw = (11 - k * 2.5f) / W3.Ppu * 1.6f;
            _flames[k].Scale = new Vector3(fw, fh * 0.5f, 1f);
            _flames[k].Position = new Vector3(MathF.Sin(Time * 30 + k) * 0.05f, fh * 0.5f, 0.3f + k * 0.02f);
        }
        _light.LightEnergy = burn ? 2.4f * Math.Min(1f, h / 2f) : 0.6f * glow;
    }
}

public partial class IceSheetView : PropView
{
    private MeshInstance3D _slab;
    private StandardMaterial3D _mat;
    private Node3D _cracks;

    protected override void Build()
    {
        var s = (IceSheet)Owner2D;
        var mb = new MeshBuilder();
        // a slab across the whole depth of the water's surface
        PropMeshes.Box(mb, new Vector3(0, 0.14f / 2f, 0), new Vector3(s.HalfW / W3.Ppu, 0.2f, 2.2f), Colors.White);
        _mat = (StandardMaterial3D)PropViews.Ice.Duplicate();
        _slab = PropViews.Mesh(mb, _mat, false);
        AddChild(_slab);
        var cr = new MeshBuilder();
        var dark = new Color(0.3f, 0.5f, 0.68f);
        PropMeshes.Box(cr, new Vector3(-0.15f, 0.28f, 0.3f), new Vector3(0.35f, 0.012f, 0.02f), dark, new Basis(Vector3.Up, 0.6f));
        PropMeshes.Box(cr, new Vector3(0.25f, 0.28f, -0.1f), new Vector3(0.3f, 0.012f, 0.02f), dark, new Basis(Vector3.Up, -0.8f));
        PropMeshes.Box(cr, new Vector3(0.05f, 0.28f, 0.6f), new Vector3(0.25f, 0.012f, 0.02f), dark, new Basis(Vector3.Up, 1.7f));
        _cracks = PropViews.Mesh(cr, PropViews.VertexColored, false);
        _cracks.Visible = false;
        AddChild(_cracks);
    }

    protected override void Sync(float dt)
    {
        var s = (IceSheet)Owner2D;
        Follow(new Vector2(0, -2), 0f);
        _cracks.Visible = s.Cracked;
        _mat.EmissionEnergyMultiplier = s.FlashT > 0 ? 2.5f : 0.25f;
    }
}

public partial class IcePlatformView : PropView
{
    private MeshInstance3D _block;
    private StandardMaterial3D _mat;
    private readonly List<MeshInstance3D> _cracks = new();

    protected override void Build()
    {
        var p = (IcePlatform)Owner2D;
        float hw = p.HalfW / W3.Ppu;
        var mb = new MeshBuilder();
        PropMeshes.Box(mb, new Vector3(0, -0.35f, 0), new Vector3(hw, 0.35f, 1.1f), Colors.White);
        for (int k = 0; k < 5; k++)
        {
            // icicles under the ledge
            float x = -hw * 0.8f + k * hw * 0.4f;
            mb.Tube(new[] { new Vector3(x, -0.68f, 0.2f), new Vector3(x, -1.1f - 0.2f * (k % 2), 0.2f) }, new[] { 0.12f, 0f }, 6, Colors.White, capStart: true);
        }
        _mat = (StandardMaterial3D)PropViews.Ice.Duplicate();
        _block = PropViews.Mesh(mb, _mat, false);
        AddChild(_block);
        for (int k = 0; k < 5; k++)
        {
            var cr = new MeshBuilder();
            PropMeshes.Box(cr, new Vector3(-hw * 0.6f + k * 0.88f, -0.3f, 1.12f), new Vector3(0.02f, 0.3f, 0.01f), new Color(0.3f, 0.5f, 0.7f), new Basis(Vector3.Back, 0.4f));
            var mi = PropViews.Mesh(cr, PropViews.VertexColored, false);
            mi.Visible = false;
            _cracks.Add(mi);
            AddChild(mi);
        }
    }

    protected override void Sync(float dt)
    {
        var p = (IcePlatform)Owner2D;
        float shake = p.Shaking > 0 ? MathF.Sin(Time * 90f) * 0.05f : 0f;
        Follow(new Vector2(shake * W3.Ppu, 0), 0f);
        Visible = !p.Broken;
        for (int k = 0; k < _cracks.Count; k++) _cracks[k].Visible = k < p.Cracks;
        _mat.EmissionEnergyMultiplier = p.FlashT > 0 ? 2.5f : 0.25f;
    }
}

/// <summary>
/// A plug of fallen boulders: an unstable stack of rough rocks jammed into the passage, big ones at the
/// bottom, smaller wedged on top, each turned and leaning its own way. It creaks and sways when struck or
/// heaved at, sagging a little more with each blow; rocks work loose from the top and tumble off, and when it
/// is cleared the rest slide and roll away and settle.
/// </summary>
public partial class RubbleView : PropView
{
    private const int MaxRocks = 44;
    private readonly MeshInstance3D[] _rock = new MeshInstance3D[MaxRocks];
    private readonly Vector3[] _home = new Vector3[MaxRocks], _vel = new Vector3[MaxRocks], _off = new Vector3[MaxRocks], _spin = new Vector3[MaxRocks];
    private readonly Basis[] _rest = new Basis[MaxRocks];
    private readonly float[] _size = new float[MaxRocks], _row = new float[MaxRocks];
    private readonly bool[] _loose = new bool[MaxRocks];
    private int _count, _shown;
    private Node3D _stack;
    private float _h, _sway, _swayV;
    private bool _skulls;

    protected override void Build()
    {
        var r = (Rubble)Owner2D;
        var rng = new Random(r.Index * 131 + 7);
        var noise = new Noise3(r.Index * 17 + 3);
        float w = W3.M(r.Size.X), h = W3.M(r.Size.Y);
        _h = h;
        // (everything hangs from the base so the whole stack can lean over together)
        _stack = new Node3D { Position = new Vector3(0, -h * 0.5f, 0) };
        AddChild(_stack);
        if (_skulls = G.Cave?.Biome?.Ossuary == true) { BuildSkulls(rng, w, h); return; }
        float y = 0f; int row = 0;
        while (y < h - 0.25f && _count < MaxRocks - 3)
        {
            // big rocks below, smaller wedged in above; a row holds two or three, shifted from the one beneath
            float yf = Math.Clamp(y / Math.Max(0.5f, h), 0f, 1f);
            float big = Mathf.Lerp(0.78f, 0.5f, yf);
            int n = row % 2 == 0 ? 2 : 3;
            if (w < 1.4f) n = 2;
            // (barely narrower at the bottom than the top: it stands, but only just, and wants knocking over)
            float width = w * Mathf.Lerp(0.8f, 1.05f, yf);
            float rowH = 0f;
            for (int k = 0; k < n && _count < MaxRocks; k++)
            {
                float size = big * (0.62f + 0.75f * (float)rng.NextDouble());
                float span = Math.Max(0.1f, width - size * 1.2f);
                float x = n == 1 ? 0f : (k / (float)(n - 1) - 0.5f) * span + ((float)rng.NextDouble() - 0.5f) * 0.4f;
                if (row % 2 == 1) x += 0.2f * (rng.Next(2) == 0 ? -1 : 1) * (float)rng.NextDouble();
                var mb = DecorMeshes.RubbleChunk(rng, noise, size * 1.05f);
                var mat = PropViews.RubbleRock;
                var node = PropViews.Mesh(mb, mat);
                // jammed in at its own angle, a little sunk into its neighbours
                var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau) * new Basis(Vector3.Back, ((float)rng.NextDouble() - 0.5f) * 1.1f) * new Basis(Vector3.Right, ((float)rng.NextDouble() - 0.5f) * 0.8f);
                node.Basis = basis;
                _rest[_count] = basis;
                _home[_count] = new Vector3(x, y - 0.06f + ((float)rng.NextDouble() - 0.5f) * 0.22f, ((float)rng.NextDouble() - 0.5f) * 0.6f);
                _size[_count] = size; _row[_count] = y / Math.Max(0.5f, h);
                _spin[_count] = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 7f;
                node.Position = _home[_count];
                _stack.AddChild(node);
                _rock[_count++] = node;
                rowH = Math.Max(rowH, size * 0.72f);
            }
            y += rowH * 0.9f;
            row++;
        }
        _shown = _count;
    }

    // the catacombs' barriers: a bank of skulls with long bones jutting out of it (meshes shared by every pile, a few sizes and shades)
    private static readonly Dictionary<(int, int), ArrayMesh> _skullMeshes = new();
    private static readonly float[] SkullSizes = { 0.36f, 0.42f, 0.49f };
    private static StandardMaterial3D _boneMat;
    private static StandardMaterial3D BoneMat => _boneMat ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f, RimEnabled = true, Rim = 0.25f };
    private static ArrayMesh PileMesh(int size, int tone, bool longBone)
    {
        var key = (size + (longBone ? 10 : 0), tone);
        if (_skullMeshes.TryGetValue(key, out var m)) return m;
        var rng = new Random(size * 31 + tone * 7 + (longBone ? 99 : 0));
        var noise = new Noise3(size * 13 + tone * 5 + 1);
        var bone = new Color(0.74f, 0.7f, 0.6f).Darkened(0.12f * tone);
        var mb = longBone ? OssuaryMeshes.LongBone(rng, noise, SkullSizes[size] * 3.2f, bone) : OssuaryMeshes.HumanSkull(rng, noise, SkullSizes[size], bone);
        return _skullMeshes[key] = mb.ToMesh(BoneMat);
    }

    private void BuildSkulls(Random rng, float w, float h)
    {
        float y = 0f; int row = 0;
        while (y < h - 0.2f && _count < MaxRocks - 4)
        {
            float rowH = 0f;
            float yf = Math.Clamp(y / Math.Max(0.5f, h), 0f, 1f);
            float s0 = Mathf.Lerp(0.49f, 0.38f, yf);
            // (barely narrower at the bottom than the top: it stands, but only just, and wants knocking over)
            float width = w * Mathf.Lerp(0.8f, 1.05f, yf);
            int n = Math.Max(2, (int)Math.Round(width / (s0 * 1.1f)));
            for (int k = 0; k < n && _count < MaxRocks; k++)
            {
                bool bone = rng.NextDouble() < 0.16;
                int sz = Math.Clamp((int)Math.Round((s0 - 0.36f) / 0.065f + (rng.NextDouble() - 0.5) * 1.6), 0, 2), tone = rng.Next(3);
                float size = SkullSizes[sz];
                float x = (n == 1 ? 0f : (k / (float)(n - 1) - 0.5f) * Math.Max(0.1f, width - size * 0.9f)) + ((float)rng.NextDouble() - 0.5f) * 0.3f + (row % 2 == 1 ? 0.5f * size * (rng.Next(2) == 0 ? -1 : 1) * 0.6f : 0f);
                var node = new MeshInstance3D { Mesh = PileMesh(sz, tone, bone), CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
                Basis basis;
                if (bone) basis = new Basis(Vector3.Up, ((float)rng.NextDouble() - 0.5f) * 0.5f) * new Basis(Vector3.Back, ((float)rng.NextDouble() - 0.5f) * 1.2f);
                else basis = new Basis(Vector3.Up, ((float)rng.NextDouble() - 0.5f) * 2.4f) * new Basis(Vector3.Back, ((float)rng.NextDouble() - 0.5f) * 1.2f) * new Basis(Vector3.Right, ((float)rng.NextDouble() - 0.5f) * 0.9f);
                node.Basis = basis;
                _rest[_count] = basis;
                // (skulls sit on their jaws, a bone lies across the bank: both a little sunk into what is beneath)
                _home[_count] = new Vector3(x, y + size * (bone ? 0.1f : 0.58f) + ((float)rng.NextDouble() - 0.5f) * 0.14f, ((float)rng.NextDouble() - 0.5f) * 0.5f + (bone ? 0.25f : 0f));
                _size[_count] = size; _row[_count] = y / Math.Max(0.5f, h);
                _spin[_count] = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 7f;
                node.Position = _home[_count];
                _stack.AddChild(node);
                _rock[_count++] = node;
                rowH = Math.Max(rowH, size * 0.82f);
            }
            y += rowH * 0.92f;
            row++;
        }
        _shown = _count;
    }

    protected override void Sync(float dt)
    {
        var r = (Rubble)Owner2D;
        Follow(default, 0.1f);
        // (each blow jolts the stack: it rocks on its base like a loose pile, then settles slowly at a worse lean)
        if (r.ShakeT > 0 && _swayV == 0f) _swayV = (G.Chance(0.5f) ? 1f : -1f) * 3.2f;
        if (r.ShakeT <= 0) _swayV = 0f;
        else { _sway += _swayV * dt; _swayV -= (_sway * 80f + _swayV * 4f) * dt; }
        _sway *= MathF.Exp(-3f * dt);
        float sag = r.Cleared ? 0f : (1f - r.Left / (float)Tune.Rubble.Hits) * 0.05f;
        _stack.Rotation = new Vector3(0, 0, _sway * 0.09f + sag);
        // each blow sends a few of the top rocks rolling off
        int keep = r.Cleared ? 0 : (int)Math.Ceiling(_count * (r.Left / (float)Tune.Rubble.Hits));
        for (int k = _count - 1; k >= keep && k < _count; k--)
        {
            if (_loose[k]) continue;
            // (cleared: the whole stack gives way, the top rocks first, so it slumps into a low heap across the passage)
            if (r.Cleared && r.ClearedT < (1f - _row[k]) * 0.55f) continue;
            _loose[k] = true;
            float spread = r.Cleared ? 0.7f + 0.9f * (float)((k * 37) % 11) / 10f : 0.9f + (k % 3) * 0.5f;
            _vel[k] = new Vector3((k % 2 == 0 ? 1 : -1) * spread * (r.Cleared ? 1.1f : 1f), r.Cleared ? 0.3f + (k % 3) * 0.2f : 1.2f + (k % 4) * 0.35f, 0.6f * ((k % 3) - 1));
        }
        _shown = Math.Min(_shown, keep);
        for (int k = 0; k < _count; k++)
        {
            if (!_loose[k])
            {
                // (the higher a rock sits, the more it shifts in a jolt)
                float lean = _sway * 0.12f * _row[k];
                _rock[k].Position = _home[k] + new Vector3(lean, 0, 0);
                _rock[k].Basis = _rest[k] * new Basis(Vector3.Back, _sway * 0.15f * _row[k]);
                continue;
            }
            // tumbling: gravity, bounces off the passage floor, then it rests
            _vel[k].Y -= 9.8f * dt * 1.5f;
            _off[k] += _vel[k] * dt;
            float floorY = _size[k] * (_skulls ? 0.5f : 0.02f);
            if (_home[k].Y + _off[k].Y < floorY)
            {
                _off[k].Y = floorY - _home[k].Y;
                _vel[k].Y = Math.Abs(_vel[k].Y) > 0.9f ? -_vel[k].Y * 0.35f : 0f;
                _vel[k].X *= 0.8f; _vel[k].Z *= 0.8f;
                if (_vel[k].Y == 0f) _spin[k] *= 0.88f;
            }
            _rock[k].Position = _home[k] + _off[k];
            _rock[k].Basis = (_rock[k].Basis * new Basis(Vector3.Right, _spin[k].X * dt) * new Basis(Vector3.Back, _spin[k].Z * dt)).Orthonormalized();
        }
        // (the flat heap lies there a few seconds, so it is plain the way is open, then sinks away)
        if (r.Cleared && r.ClearedT > 6f)
        {
            float a = Math.Clamp(1f - (r.ClearedT - 6f) / 1.5f, 0f, 1f);
            Scale = Vector3.One * Math.Max(a, 0.001f);
            if (a <= 0f) Visible = false;
        }
    }
}

/// <summary>Stalag-Might's grip: rocks heave up from the ground round a creature's feet, leaning in over it like a fist, then sink away.</summary>
public partial class StalagGripView : PropView
{
    private const int Rocks = 9;
    private readonly MeshInstance3D[] _rock = new MeshInstance3D[Rocks];
    private readonly Vector3[] _home = new Vector3[Rocks];
    private readonly Basis[] _rest = new Basis[Rocks];
    private readonly float[] _lag = new float[Rocks];
    private Node3D _mound;

    protected override void Build()
    {
        var g = (StalagGrip)Owner2D;
        var rng = new Random(((int)(g.GetInstanceId() % 9973)) + 11);
        var noise = new Noise3((int)(g.GetInstanceId() % 997));
        float r = Math.Max(0.25f, W3.M(g.Radius));
        _mound = new Node3D();
        AddChild(_mound);
        for (int k = 0; k < Rocks; k++)
        {
            // a ring of rocks about the feet: low ones in front, tall ones leaning in at the sides and behind
            float ang = (k / (float)Rocks) * Mathf.Tau + 0.3f * (float)rng.NextDouble();
            bool tall = k % 3 == 0;
            float size = r * (tall ? 0.95f : 0.62f) * (0.85f + 0.3f * (float)rng.NextDouble());
            var mb = DecorMeshes.Boulder(rng, noise, size);
            float tone = 0.36f + 0.14f * (float)rng.NextDouble();
            for (int c = 0; c < mb.Count; c++)
            {
                float up = Math.Clamp(mb.V[c].Y / size + 0.3f, 0f, 1f);
                float t = tone * (0.7f + 0.4f * up);
                mb.C[c] = new Color(t * 1.02f, t * 0.96f, t * 0.88f);
            }
            var node = PropViews.Mesh(mb, PropViews.Rock);
            float rad = r * (0.75f + 0.3f * (float)rng.NextDouble());
            _home[k] = new Vector3(MathF.Cos(ang) * rad * 1.15f, size * (tall ? 0.55f : 0.2f), MathF.Sin(ang) * rad * 0.5f);
            // the tall ones lean in toward the middle
            float lean = tall ? -MathF.Sign(MathF.Cos(ang)) * 0.45f : ((float)rng.NextDouble() - 0.5f) * 0.4f;
            _rest[k] = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau) * new Basis(Vector3.Back, lean) * (tall ? Basis.FromScale(new Vector3(0.8f, 1.5f, 0.8f)) : Basis.Identity);
            _lag[k] = 0.25f * (float)rng.NextDouble();
            node.Basis = _rest[k];
            _mound.AddChild(node);
            _rock[k] = node;
        }
    }

    protected override void Sync(float dt)
    {
        var g = (StalagGrip)Owner2D;
        Follow(new Vector2(0, 2), 0.12f);
        float rise = g.Rise;
        for (int k = 0; k < Rocks; k++)
        {
            // each rock bursts up and out from the middle a beat apart; they sink back down together
            float u = Math.Clamp(rise, 0f, 1.25f);
            var home = _home[k];
            var p = new Vector3(home.X * Mathf.Lerp(0.2f, 1f, Math.Min(1f, u)), home.Y * u - (1f - Math.Min(1f, u)) * 0.5f, home.Z);
            _rock[k].Position = p;
            float s = Math.Clamp(u * 1.1f, 0.01f, 1.3f);
            _rock[k].Basis = _rest[k] * Basis.FromScale(Vector3.One * s);
            _rock[k].Visible = rise > 0.02f;
        }
    }
}
