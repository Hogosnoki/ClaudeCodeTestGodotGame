using System;
using System.Collections.Generic;
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
            Blizzard => new BlizzardView(),
            IceBlock => new IceBlockView(),
            ThrownDagger => new ThrownDaggerView(),
            SmokeCloud => new SmokeCloudView(),
            XpOrb => new XpOrbView(),
            HeartPickup => new HeartView(),
            PotionPickup => new PotionView(),
            Chest => new ChestView(),
            Portal { Outside: true } => new MouthView(),
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

    private static StandardMaterial3D _ice, _steel, _wood, _gold, _glass, _rock, _vcol;
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
    public static StandardMaterial3D VertexColored => _vcol ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.6f };

    /// <summary>
    /// Lets go of the shared resources before the engine shuts down (static references would
    /// otherwise be released after the rendering server is gone).
    /// </summary>
    public static void ReleaseShared()
    {
        foreach (var r in new Resource[] { _sprite, _cloud, _bubble, PortalView.StairMatOrNull, _quad, _ice, _steel, _wood, _gold, _glass, _rock, _vcol }) r?.Dispose();
        _sprite = _cloud = _bubble = null; _quad = null;
        PortalView.ReleaseShared();
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
        _core = PropViews.Sprite(new Color(1f, 0.82f, 0.8f), 3, 2.4f, 0.28f);
        AddChild(_core);
        for (int k = 0; k < _tail.Length; k++)
        {
            float f = 1f - k / (float)_tail.Length;
            _tail[k] = PropViews.Sprite(new Color(0.85f, 0.08f, 0.16f), 0, 1.3f * f, (0.36f * f + 0.08f) * _size);
            AddChild(_tail[k]);
        }
        _light = PropViews.Light(new Color(1f, 0.3f, 0.35f), 1.2f * _size, 2.8f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var m = (LifeMote)Owner2D;
        Follow(default, 0.3f);
        float a = m.Alpha, grow = (0.4f + 0.6f * a) * _size;
        float pulse = 1f + 0.18f * MathF.Sin(m.Age * 38f);
        _halo.Scale = Vector3.One * 0.6f * pulse * grow;
        _core.Scale = Vector3.One * 0.28f * grow;
        // the tail streams back along its flight (2D y-down mirrored to y-up), longer the faster it goes
        var v = m.Vel;
        float stretch = Math.Clamp(v.Length() / 600f, 0.3f, 1.3f);
        var back = v.LengthSquared() > 1 ? new Vector3(-v.X, v.Y, 0f).Normalized() : Vector3.Zero;
        for (int k = 0; k < _tail.Length; k++)
        {
            _tail[k].Position = back * (0.12f + 0.13f * k) * stretch * _size;
            _tail[k].Visible = a > 0.4f;
        }
        _light.LightEnergy = 1.2f * a * _size;
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
/// The Elementalist's updraft: a column of pale air, streaks racing up it and a slow swirl at its
/// foot and its crown, all fading as it dies.
/// </summary>
public partial class UpdraftView : PropView
{
    private readonly MeshInstance3D[] _streaks = new MeshInstance3D[12];
    private readonly float[] _phase = new float[12], _x = new float[12];
    private MeshInstance3D _foot, _crown, _haze;
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
            _streaks[k] = PropViews.Sprite(new Color(0.85f, 0.95f, 1f), 1, 1.4f, 0.3f);
            _streaks[k].MaterialOverride = _flat;
            _streaks[k].Rotation = new Vector3(0, 0, MathF.PI / 2f);
            AddChild(_streaks[k]);
        }
        _haze = PropViews.Sprite(new Color(0.7f, 0.88f, 1f), 0, 0.35f, 1f);
        _haze.MaterialOverride = _flat;
        AddChild(_haze);
        _foot = PropViews.Sprite(new Color(0.8f, 0.94f, 1f), 7, 1.1f, W3.M(u.Width) * 0.8f);
        AddChild(_foot);
        _crown = PropViews.Sprite(new Color(0.8f, 0.94f, 1f), 7, 0.8f, W3.M(u.Width) * 0.6f);
        AddChild(_crown);
        _light = PropViews.Light(new Color(0.75f, 0.9f, 1f), 0.6f, 3f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var u = (Updraft)Owner2D;
        Follow(default, 0.15f);
        float h = W3.M(u.Height), w = W3.M(u.Width), s = u.Strength;
        for (int k = 0; k < _streaks.Length; k++)
        {
            float y = (_phase[k] + u.Age * 1.1f) % 1f;
            _streaks[k].Position = new Vector3(_x[k] * w * 0.4f, y * h, 0);
            float edge = MathF.Min(y / 0.15f, (1f - y) / 0.2f);
            PropViews.SetSprite(_streaks[k], new Color(0.85f, 0.95f, 1f, Math.Clamp(edge, 0f, 1f) * 0.7f * s), 1, 1.4f);
            _streaks[k].Scale = new Vector3(Math.Min(h * 0.35f, 0.9f), w * 0.12f, 1f);
        }
        _haze.Position = new Vector3(0, h * 0.5f, -0.05f);
        _haze.Scale = new Vector3(w * 0.7f, h * 0.55f, 1f);
        PropViews.SetSprite(_haze, new Color(0.7f, 0.88f, 1f, 0.35f * s), 0, 0.5f);
        _foot.Position = new Vector3(0, 0.05f, 0);
        PropViews.SetSprite(_foot, new Color(0.8f, 0.94f, 1f, 0.8f * s), 7, 1.1f);
        _crown.Position = new Vector3(0, h, 0);
        PropViews.SetSprite(_crown, new Color(0.8f, 0.94f, 1f, 0.55f * s), 7, 0.8f);
        _light.Position = new Vector3(0, h * 0.5f, 0.3f);
        _light.LightEnergy = 0.6f * s;
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
    private MeshInstance3D _blade, _glint, _streak;
    private static Mesh _mesh;

    protected override void Build()
    {
        _mesh ??= PropMeshes.Sword(0.3f, 0.026f, new Color(0.78f, 0.8f, 0.84f), new Color(0.6f, 0.46f, 0.2f), new Color(0.1f, 0.08f, 0.07f), 0.05f)
            .ToMesh(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Metallic = 0.8f, Roughness = 0.3f });
        _blade = new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * 1.6f };
        AddChild(_blade);
        _glint = PropViews.Sprite(new Color(1f, 0.97f, 0.9f), 3, 1.2f, 0.18f);
        AddChild(_glint);
        _streak = PropViews.Sprite(new Color(0.85f, 0.9f, 1f), 1, 0.9f, 0.5f);
        AddChild(_streak);
    }

    protected override void Sync(float dt)
    {
        var d = (ThrownDagger)Owner2D;
        Follow(default, 0.25f);
        var p = d.Pointing;
        // (the blade runs down -Y from its grip: turn -Y onto the way it points, 2D y-down flipped)
        _blade.Rotation = new Vector3(0, 0, MathF.Atan2(p.X, p.Y));
        _blade.Position = new Vector3(-p.X, p.Y, 0) * 0.2f;
        bool flying = d.State != ThrownDagger.Phase.Stuck;
        _streak.Visible = flying;
        _streak.Position = new Vector3(-p.X, p.Y, 0) * 0.35f;
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

public partial class ChestView : PropView
{
    private Node3D _lid;
    private MeshInstance3D _shine;
    private OmniLight3D _light;

    protected override void Build()
    {
        const float w = 0.72f, d = 0.5f, h = 0.46f;
        var body = new MeshBuilder();
        PropMeshes.Box(body, new Vector3(0, h * 0.5f, 0), new Vector3(w, h * 0.5f, d), new Color(0.38f, 0.22f, 0.11f));
        AddChild(PropViews.Mesh(body, PropViews.VertexColored));
        var bands = new MeshBuilder();
        foreach (float x in new[] { -w * 0.7f, w * 0.7f })
            PropMeshes.Box(bands, new Vector3(x, h * 0.5f, 0), new Vector3(0.05f, h * 0.52f, d * 1.02f), Colors.White);
        PropMeshes.Box(bands, new Vector3(0, h * 0.15f, 0), new Vector3(w * 1.02f, 0.04f, d * 1.02f), Colors.White);
        PropMeshes.Box(bands, new Vector3(0, h * 0.62f, d * 1.03f), new Vector3(0.09f, 0.1f, 0.02f), Colors.White);
        AddChild(PropViews.Mesh(bands, PropViews.Gold));
        // the lid, hinged at the back edge
        _lid = new Node3D { Position = new Vector3(0, h, -d) };
        AddChild(_lid);
        var lid = new MeshBuilder();
        PropMeshes.Box(lid, new Vector3(0, 0.12f, d), new Vector3(w * 1.02f, 0.12f, d * 1.02f), new Color(0.44f, 0.26f, 0.13f));
        _lid.AddChild(PropViews.Mesh(lid, PropViews.VertexColored));
        var lidBands = new MeshBuilder();
        foreach (float x in new[] { -w * 0.7f, w * 0.7f })
            PropMeshes.Box(lidBands, new Vector3(x, 0.13f, d), new Vector3(0.05f, 0.13f, d * 1.04f), Colors.White);
        _lid.AddChild(PropViews.Mesh(lidBands, PropViews.Gold));
        _shine = PropViews.Sprite(new Color(1f, 0.85f, 0.4f), 4, 1.2f, 1.4f);
        _shine.Position = new Vector3(0, h + 0.2f, 0);
        AddChild(_shine);
        _light = PropViews.Light(new Color(1f, 0.8f, 0.45f), 0.8f, 4f);
        _light.Position = new Vector3(0, h + 0.4f, 0.4f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (Chest)Owner2D;
        Follow(default, 0f);
        float lid = c.Open ? Math.Min(1f, c.OpenT * 5f) : 0f;
        _lid.Rotation = new Vector3(-lid * 1.9f, 0, 0);
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
            PropViews.SetSprite(_shine, new Color(1f, 0.85f, 0.4f, 0.35f + 0.1f * MathF.Sin(Time * 3)), 4, 1f);
            _light.LightEnergy = 0.7f + 0.15f * MathF.Sin(Time * 3);
        }
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
/// bright it washes out to white (one sheet in front of the rock at the map's edge, one lighting
/// the back of the tunnel), a warm light spilling in through the dust, and a prompt to leave.
/// </summary>
public partial class MouthView : PropView
{
    private OmniLight3D _light;
    private MeshInstance3D _front, _back, _bloom;
    private Label3D _title, _prompt;

    private static ShaderMaterial _dayMat;
    private static ShaderMaterial DayMat => _dayMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_daylight.gdshader") };
    public static void ReleaseShared() { _dayMat?.Dispose(); _dayMat = null; }

    // (the view sits on the floor 5.5 m in from the map's left edge; the rock there ends at 3 m)
    private const float Edge = -5.5f;

    protected override void Build()
    {
        // solid white over the rock at the map's edge, thinning out a metre or so into the tunnel
        // (so a hero at the door still stands clear of it)
        _front = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(9.2f, 7.8f) }, MaterialOverride = DayMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(Edge + 0.6f, 3.3f, 3.8f) };
        _front.SetInstanceShaderParameter("strength", 1f);
        _front.SetInstanceShaderParameter("fade_start", 0.78f);
        AddChild(_front);
        // the back of the tunnel lit up by it, fading off into the cave
        _back = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(11f, 7.4f) }, MaterialOverride = DayMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(Edge + 3.5f, 3.3f, -2.2f) };
        _back.SetInstanceShaderParameter("strength", 0.7f);
        _back.SetInstanceShaderParameter("fade_start", 0.3f);
        AddChild(_back);
        _bloom = PropViews.Sprite(new Color(1f, 0.95f, 0.85f), 0, 1.6f, 4.5f);
        _bloom.Position = new Vector3(Edge + 1.5f, 3.4f, 2.4f);
        AddChild(_bloom);
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
