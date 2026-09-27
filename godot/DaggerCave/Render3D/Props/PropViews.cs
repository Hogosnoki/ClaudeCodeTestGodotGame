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
            ThrownDagger => new DaggerView(),
            EnemyProjectile => new ProjectileView(),
            LavaPuddle => new LavaPuddleView(),
            Shockwave => new ShockwaveView(),
            FallingRock => new FallingRockView(),
            SwordWave => new SwordWaveView(),
            XpOrb => new XpOrbView(),
            HeartPickup => new HeartView(),
            PotionPickup => new PotionView(),
            Chest => new ChestView(),
            Portal => new PortalView(),
            AirVent => new AirVentView(),
            AirBubble => new AirBubbleView(),
            SporeCloud => new SporeCloudView(),
            SporePod => new SporePodView(),
            WebPatch => new WebView(),
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
    private static ShaderMaterial _sprite, _cloud, _bubble, _vortex;
    public static ShaderMaterial SpriteMat => _sprite ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_sprite.gdshader") };
    public static ShaderMaterial CloudMat => _cloud ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_cloud.gdshader") };
    public static ShaderMaterial BubbleMat => _bubble ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_bubble.gdshader") };
    public static ShaderMaterial VortexMat => _vortex ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_vortex.gdshader") };
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
        foreach (var r in new Resource[] { _sprite, _cloud, _bubble, _vortex, _quad, _ice, _steel, _wood, _gold, _glass, _rock, _vcol }) r?.Dispose();
        _sprite = _cloud = _bubble = _vortex = null; _quad = null;
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

public partial class DaggerView : PropView
{
    private Node3D _spin;
    private MeshInstance3D _trail, _glow;
    private OmniLight3D _light;

    protected override void Build()
    {
        _spin = new Node3D();
        AddChild(_spin);
        // a throwing knife, balanced at its middle, laid in the play plane
        var mb = PropMeshes.Sword(0.34f, 0.028f, new Color(0.85f, 0.88f, 0.92f), new Color(0.75f, 0.6f, 0.25f), new Color(0.35f, 0.22f, 0.12f), 0.045f);
        var knife = PropViews.Mesh(mb, PropViews.Steel);
        knife.Position = new Vector3(0, 0.2f, 0);
        knife.RotationDegrees = new Vector3(90, 0, 0);
        var arm = new Node3D { RotationDegrees = new Vector3(0, 0, -90) };
        arm.AddChild(knife);
        _spin.AddChild(arm);
        _trail = PropViews.Sprite(new Color(0.75f, 0.92f, 1f), 2, 1.8f, 0.62f, 0.06f);
        AddChild(_trail);
        _glow = PropViews.Sprite(new Color(0.7f, 0.9f, 1f), 0, 1.2f, 0.45f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(0.7f, 0.9f, 1f), 0.8f, 2.5f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var d = (ThrownDagger)Owner2D;
        Follow(default, 0.3f);
        // the 2D spin angle is y-down; mirror it into y-up
        _spin.Rotation = new Vector3(0, 0, -(d.Flying ? d.Spin : Owner2D.GlobalRotation));
        float a = d.Alpha;
        _trail.Visible = d.Flying;
        _glow.Visible = d.Flying;
        _light.LightEnergy = d.Flying ? 0.8f : 0f;
        Scale = Vector3.One * (0.6f + 0.4f * a);
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

public partial class PortalView : PropView
{
    private MeshInstance3D _mouth, _arch;
    private OmniLight3D _light;
    private Label3D _title, _sub;

    protected override void Build()
    {
        var p = (Portal)Owner2D;
        var glow = p.To?.Glow ?? new Color(0.7f, 0.5f, 1f);
        var deep = p.To?.Deep ?? new Color(0.1f, 0.05f, 0.2f);
        var edge = p.To?.Edge ?? new Color(0.4f, 0.3f, 0.5f);
        // the arch: rough standing stones around the mouth
        var mb = new MeshBuilder();
        var rng = new Random(3);
        for (int k = 0; k <= 10; k++)
        {
            float a = MathF.PI * k / 10f;
            var at = new Vector3(MathF.Cos(a) * 1.75f, 1.9f + MathF.Sin(a) * 1.9f - 0.1f, 0);
            if (k == 0 || k == 10) at.Y = 1.9f;
            mb.Blob(at, new Vector3(0.38f, 0.4f, 0.5f) * (0.8f + 0.4f * (float)rng.NextDouble()), 4, edge.Darkened(0.2f), new Noise3(k), 0.3f, 2f);
        }
        foreach (float x in new[] { -1.8f, 1.8f })
            for (int k = 0; k < 3; k++)
                mb.Blob(new Vector3(x, 0.35f + k * 0.62f, 0), new Vector3(0.42f, 0.36f, 0.5f), 4, edge.Darkened(0.25f), new Noise3(20 + k), 0.3f, 2f);
        _arch = PropViews.Mesh(mb, PropViews.VertexColored);
        AddChild(_arch);
        _mouth = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(3.2f, 3.8f) }, MaterialOverride = PropViews.VortexMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 1.9f, -0.05f) };
        _mouth.SetInstanceShaderParameter("deep_color", deep);
        _mouth.SetInstanceShaderParameter("glow_color", glow);
        AddChild(_mouth);
        _light = PropViews.Light(glow, 2.2f, 7f);
        _light.Position = new Vector3(0, 2f, 1f);
        AddChild(_light);
        _title = new Label3D { Text = p.To?.Name.ToUpperInvariant() ?? "DEEPER", Modulate = glow, FontSize = 64, PixelSize = 0.012f, OutlineSize = 12, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = new Vector3(0, 4.9f, 0.3f) };
        _sub = new Label3D { Text = p.Label, Modulate = new Color(1, 1, 1, 0.85f), FontSize = 40, PixelSize = 0.012f, OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = new Vector3(0, 4.35f, 0.3f) };
        AddChild(_title);
        AddChild(_sub);
    }

    protected override void Sync(float dt)
    {
        var p = (Portal)Owner2D;
        Follow(new Vector2(0, 30), 0f);
        float grow = Math.Min(1f, p.Age);
        Scale = Vector3.One * Math.Max(0.01f, grow);
        _mouth.SetInstanceShaderParameter("grow", grow);
        _light.LightEnergy = 2.2f * grow * (0.85f + 0.15f * MathF.Sin(Time * 3f));
        float a = Math.Min(1f, p.Age * 2f);
        _title.Modulate = _title.Modulate with { A = a };
        _sub.Modulate = _sub.Modulate with { A = 0.85f * a };
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
