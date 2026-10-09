using System;
using Godot;

namespace DaggerCave;

/// <summary>A cloud of sulphurous gas: a heap of soft yellow puffs, glowing faintly; lit, it flares orange for a heartbeat before the blast.</summary>
public partial class GasCloudView : PropView
{
    private readonly MeshInstance3D[] _puffs = new MeshInstance3D[8];
    private MeshInstance3D _glow;
    private OmniLight3D _light;
    private float _seed;

    protected override void Build()
    {
        _seed = (GetInstanceId() % 100) * 0.37f;
        for (int k = 0; k < _puffs.Length; k++)
        {
            _puffs[k] = new MeshInstance3D { Mesh = PropViews.Quad, MaterialOverride = PropViews.CloudMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _puffs[k].SetInstanceShaderParameter("cloud_seed", k * 1.37f + _seed);
            AddChild(_puffs[k]);
        }
        _glow = PropViews.Sprite(new Color(0.95f, 0.92f, 0.3f), 4, 0.3f, 2f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(1f, 0.6f, 0.2f), 0f, 5f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (GasCloud)Owner2D;
        Follow(default, 0.25f);
        float d = c.Density;
        float r = c.Size / W3.Ppu;
        float fuse = c.Fuse;
        var calm = new Color(0.6f, 0.58f, 0.14f, 0.34f * d);
        var fire = new Color(1f, 0.5f, 0.12f, 0.6f * d);
        var col = fuse < 0 ? calm : calm.Lerp(fire, fuse);
        float t = c.T;
        for (int k = 0; k < _puffs.Length; k++)
        {
            var o = new Vector3(MathF.Cos(k * 1.3f + t * 0.5f), -MathF.Sin(k * 1.9f + t * 0.4f) * 0.8f, MathF.Sin(k * 2.3f + t * 0.35f)) * r * 0.5f;
            _puffs[k].Position = o;
            _puffs[k].Scale = Vector3.One * r * (0.72f + 0.1f * (k % 3));
            _puffs[k].SetInstanceShaderParameter("cloud_color", col);
            _puffs[k].SetInstanceShaderParameter("cloud_glow", fuse < 0 ? 0.1f : 0.35f);
        }
        _glow.Scale = Vector3.One * r * 1.7f;
        var gc = fuse < 0 ? new Color(0.95f, 0.92f, 0.3f, d) : new Color(1f, 0.6f, 0.2f, d).Lerp(new Color(1f, 0.85f, 0.4f, 1f), fuse);
        PropViews.SetSprite(_glow, gc, 4, fuse < 0 ? 0.14f : 0.6f + 1.4f * fuse);
        _light.LightEnergy = fuse < 0 ? 0f : 2.5f * fuse;
    }
}

/// <summary>A vent in the floor of the springs: a low crusted mound ringed with sulphur crystals around a dark mouth that glows yellow and hisses before it blows.</summary>
public partial class GasVentView : PropView
{
    private MeshInstance3D _mouth, _glow, _heat;
    private Node3D _mound;

    protected override void Build()
    {
        var mb = new MeshBuilder();
        var crust = new Color(0.62f, 0.52f, 0.18f);
        mb.Blob(Vector3.Zero, new Vector3(0.75f, 0.14f, 0.55f), 5, crust, new Noise3(4), 0.25f, 3f, 1f);
        var rng = new Random(13);
        for (int k = 0; k < 11; k++)
        {
            float a = k / 11f * Mathf.Tau + (float)rng.NextDouble() * 0.3f;
            var at = new Vector3(MathF.Cos(a) * 0.5f, 0.02f, MathF.Sin(a) * 0.36f);
            var dir = new Vector3(MathF.Cos(a) * 0.5f, 1f, MathF.Sin(a) * 0.3f).Normalized();
            DesignKit.CrystalAt(mb, at, dir, 0.04f + 0.03f * (float)rng.NextDouble(), 0.14f + 0.18f * (float)rng.NextDouble(), new Color(0.98f, 0.92f, 0.3f), (float)rng.NextDouble());
        }
        _mound = new Node3D();
        _mound.AddChild(PropViews.Mesh(mb, PropViews.VertexColored, false));
        AddChild(_mound);
        _mouth = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 6 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.02f, 0.01f), Roughness = 1f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(0.5f, 0.12f, 0.34f), Position = new Vector3(0, 0.08f, 0.06f),
        };
        AddChild(_mouth);
        _heat = PropViews.Sprite(new Color(0.95f, 0.9f, 0.25f), 0, 1f, 0.8f);
        _heat.Position = new Vector3(0, 0.12f, 0.3f);
        AddChild(_heat);
        _glow = PropViews.Sprite(new Color(0.95f, 0.9f, 0.25f), 4, 0.5f, 1.8f);
        _glow.Position = new Vector3(0, 0.5f, 0.5f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var v = (GasVent)Owner2D;
        Follow(new Vector2(0, 1), 0f);
        float c = v.Cycle;
        float warn = c >= Tune.Sulphur.VentIdle ? Math.Min(1f, (c - Tune.Sulphur.VentIdle) / Tune.Sulphur.VentWarn) : 0f;
        float act = v.Blowing ? 1f : warn;
        PropViews.SetSprite(_heat, new Color(0.95f, 0.9f, 0.25f, 0.15f + 0.85f * act), 0, 1.1f);
        PropViews.SetSprite(_glow, new Color(0.95f, 0.9f, 0.25f, (v.Blowing ? 0.7f : 0.25f * warn)), 4, 0.5f);
        // it shudders as it hisses and while it blows
        _mound.Position = act > 0.05f ? new Vector3(MathF.Sin(Time * 45f) * 0.012f * act, 0, 0) : Vector3.Zero;
    }
}

/// <summary>A pool of acid: a flat yellow-green puddle, glowing, bubbling at the edge as it dries.</summary>
public partial class AcidPuddleView : PropView
{
    private MeshInstance3D _pool, _glow;

    protected override void Build()
    {
        var mb = new MeshBuilder();
        mb.Blob(Vector3.Zero, new Vector3(AcidPuddle.HalfW / W3.Ppu, 0.05f, 0.85f), 6, Colors.White, new Noise3(7), 0.25f, 3f, 1f);
        _pool = PropViews.Mesh(mb, PropViews.Emissive(new Color(0.75f, 0.95f, 0.18f), 1.8f, 0.92f), false);
        AddChild(_pool);
        _glow = PropViews.Sprite(new Color(0.78f, 0.95f, 0.2f), 4, 0.6f, 1.5f);
        _glow.Position = new Vector3(0, 0.2f, 0.6f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var p = (AcidPuddle)Owner2D;
        Follow(new Vector2(0, 1), 0f);
        float a = Math.Clamp(p.LifeLeft / 0.8f, 0, 1);
        float grow = Math.Min(1f, p.T * 6f);
        _pool.Scale = new Vector3((0.9f + 0.06f * MathF.Sin(Time * 5)) * (0.35f + 0.65f * a) * grow, 1, (0.75f + 0.25f * a) * grow);
        _glow.Visible = a > 0.05f;
        PropViews.SetSprite(_glow, new Color(0.78f, 0.95f, 0.2f, a), 4, 0.6f);
    }
}
