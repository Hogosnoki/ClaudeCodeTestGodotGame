using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>The Comet's and Stalagmite Vault's crystals of ice: clear, glowing blue-white, each bursting up out of the floor, standing, and
/// shrinking away as it melts (<see cref="IceCrystals"/>). A crystal laid later (the vault's trail) gets its mesh as it appears.</summary>
public partial class IceCrystalsView : PropView
{
    private static ArrayMesh _crystal;
    private static StandardMaterial3D _mat;
    private readonly List<MeshInstance3D> _spikes = new();
    private OmniLight3D _light;

    protected override void Build()
    {
        if (_crystal == null)
        {
            var mb = new MeshBuilder();
            // (one crystal a unit long, a main shard and two smaller beside it)
            DesignKit.CrystalAt(mb, Vector3.Zero, Vector3.Up, 0.5f, 1f, new Color(0.45f, 0.72f, 1f), 0.2f);
            DesignKit.CrystalAt(mb, new Vector3(0.3f, 0f, 0.15f), new Vector3(0.45f, 1f, 0.1f), 0.26f, 0.55f, new Color(0.55f, 0.8f, 1f), 0.6f);
            DesignKit.CrystalAt(mb, new Vector3(-0.28f, 0f, -0.12f), new Vector3(-0.4f, 1f, -0.15f), 0.22f, 0.45f, new Color(0.55f, 0.8f, 1f), 0.9f);
            _crystal = mb.ToMesh();
            // (a cold blue that holds its own under the cave's warm light: a little glow, not a lamp)
            _mat = PropViews.Emissive(new Color(0.3f, 0.6f, 1f), 0.3f, 0.82f);
            _mat.Roughness = 0.06f; _mat.RimEnabled = true; _mat.Rim = 0.9f; _mat.VertexColorUseAsAlbedo = true;
        }
        _light = PropViews.Light(new Color(0.4f, 0.65f, 1f), 0f, 5f);
        _light.Position = new Vector3(0, 0.8f, 1f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (IceCrystals)Owner2D;
        Position = Vector3.Zero;
        while (_spikes.Count < c.Spikes.Count)
        {
            var mi = new MeshInstance3D { Mesh = _crystal, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(mi);
            if (IsInsideTree()) CreatureModel.SetActorLayer(mi);
            _spikes.Add(mi);
        }
        float most = 0f;
        Vector3 at = Vector3.Zero;
        for (int i = 0; i < _spikes.Count; i++)
        {
            var s = c.Spikes[i];
            float k = c.SpikeScale(i);
            most = Math.Max(most, k);
            var mi = _spikes[i];
            mi.Visible = k > 0.01f;
            if (!mi.Visible) continue;
            float len = W3.M(s.Length) * k, w = W3.M(s.Width) * Math.Max(0.3f, k);
            // (rolled about the view axis by its lean; a little toward or away from the camera, so a burst has depth)
            mi.Basis = new Basis(Vector3.Back, -s.Lean) * new Basis(Vector3.Right, ((i * 37 % 7) - 3) * 0.08f) * Basis.FromScale(new Vector3(w, len, w));
            mi.Position = W3.P(s.Foot, ((i * 53 % 9) - 4) * 0.06f);
            at = mi.Position;
        }
        if (_spikes.Count > 0) _light.Position = at + new Vector3(0, 0.8f, 1f);
        _light.LightEnergy = 0.7f * most;
    }
}

/// <summary>Breaking Quake's cracks: jagged, gaping rifts along the floor either way, glowing with magma, the rock heaved up along their
/// lips; they run out from the middle and close up again (<see cref="MagmaCracks"/>).</summary>
public partial class MagmaCracksView : PropView
{
    private readonly List<(MeshInstance3D glow, MeshInstance3D lip, float at, float width)> _parts = new();
    private OmniLight3D _light;
    private static StandardMaterial3D _magma;

    protected override void Build()
    {
        var c = (MagmaCracks)Owner2D;
        var rng = new Random(c.Seed + 7);
        _magma ??= PropViews.Emissive(new Color(1f, 0.42f, 0.08f), 3.2f);
        var box = new BoxMesh { Size = Vector3.One };
        foreach (var side in new[] { c.Left, c.Right })
        {
            for (int k = 0; k + 1 < side.Count; k++)
            {
                // a segment of crack between two floor points: wide and deep near the middle, a hairline at the tip, zig-zagging in depth
                float u = k / (float)Math.Max(1, side.Count - 1);
                var a = side[k]; var b = side[k + 1];
                var mid = (a + b) * 0.5f;
                float width = Mathf.Lerp(0.42f, 0.06f, u) * (0.8f + 0.4f * (float)rng.NextDouble());
                float zig = ((float)rng.NextDouble() - 0.5f) * 0.7f;
                var glow = new MeshInstance3D { Mesh = box, MaterialOverride = _magma, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                glow.Position = W3.P(mid, zig) - W3.P(c.GlobalPosition) + new Vector3(0, 0.02f, 0);
                var d = W3.D(b - a);
                glow.Basis = new Basis(Vector3.Back, MathF.Atan2(d.Y, d.X)) * new Basis(Vector3.Up, zig * 0.9f) * Basis.FromScale(new Vector3(d.Length() * 1.3f, 0.12f, width));
                AddChild(glow);
                // the rock heaved up along the lip
                var mb = new MeshBuilder();
                mb.Blob(Vector3.Zero, new Vector3(0.22f, 0.12f, 0.16f), 3, new Color(0.3f, 0.24f, 0.2f), new Noise3(k + side.Count), 0.35f, 3f, 1f);
                var lip = PropViews.Mesh(mb, PropViews.Rock, false);
                lip.Position = glow.Position + new Vector3(0, 0.04f, (rng.NextDouble() < 0.5 ? -1f : 1f) * (width * 0.5f + 0.1f));
                lip.RotateY((float)rng.NextDouble() * Mathf.Tau);
                AddChild(lip);
                _parts.Add((glow, lip, u, width));
            }
        }
        _light = PropViews.Light(new Color(1f, 0.45f, 0.12f), 0f, 7f);
        _light.Position = new Vector3(0, 0.6f, 1f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (MagmaCracks)Owner2D;
        Follow(default, 0f);
        float reach = c.Reach, open = c.Open;
        foreach (var (glow, lip, at, width) in _parts)
        {
            bool shown = at <= reach && open > 0.01f;
            glow.Visible = lip.Visible = shown;
            if (!shown) continue;
            var s = glow.Basis.Scale;
            glow.Basis = glow.Basis.Orthonormalized() * Basis.FromScale(new Vector3(s.X, 0.12f, Math.Max(0.01f, width * open * (0.9f + 0.1f * MathF.Sin(Time * 9f + at * 20f)))));
            lip.Scale = Vector3.One * Math.Max(0.05f, open);
        }
        _light.LightEnergy = 2.4f * open * (0.85f + 0.15f * MathF.Sin(Time * 11f));
    }
}

/// <summary>Blazing Charge's sphere of fire about the Swordsman: nearly opaque, roaring flame wrapping him, with a hot light inside.</summary>
public partial class BlazeShellView : PropView
{
    private MeshInstance3D _shell;
    private OmniLight3D _light;
    private static ShaderMaterial _mat;

    protected override void Build()
    {
        _mat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_wardshell.gdshader") };
        _shell = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 },
            MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _shell.SetInstanceShaderParameter("shell_color", new Color(1f, 0.5f, 0.12f));
        _shell.SetInstanceShaderParameter("kind", 2f);
        AddChild(_shell);
        _light = PropViews.Light(new Color(1f, 0.5f, 0.15f), 2f, 5f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var b = (BlazeShell)Owner2D;
        Follow(default, 0.15f);
        float grow = Math.Min(1f, b.Age / 0.08f);
        float r = W3.M(19f) * (0.5f + 0.5f * grow) * (1f + 0.05f * MathF.Sin(Time * 23f));
        _shell.Scale = new Vector3(r * 1.12f, r, r * 0.85f);
        _shell.RotateY(dt * 6f);
        _light.LightEnergy = 2f + 0.6f * MathF.Sin(Time * 17f);
    }
}
