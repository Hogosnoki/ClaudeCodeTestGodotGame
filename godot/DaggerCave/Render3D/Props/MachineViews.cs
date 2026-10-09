using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Shared look of the works' iron and brass.</summary>
public static class Machine
{
    private static StandardMaterial3D _iron;
    public static StandardMaterial3D Iron => _iron ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Metallic = 0.7f, Roughness = 0.42f };
    public static void Release() { _iron?.Dispose(); _iron = null; }
    public static readonly Color Dark = new(0.2f, 0.2f, 0.23f), Mid = new(0.34f, 0.34f, 0.38f), Brass = new(0.62f, 0.45f, 0.16f), Rust = new(0.4f, 0.22f, 0.12f), Hazard = new(0.85f, 0.65f, 0.1f);
    public static Transform3D At(Vector3 p) => new(Basis.Identity, p);

    /// <summary>A toothed disc in the XY plane (a saw blade, a gear), <paramref name="teeth"/> teeth, its thickness along Z.</summary>
    public static MeshBuilder Disc(int teeth, float r, float tooth, float thickness, Color col, Color? rim = null)
    {
        var mb = new MeshBuilder();
        int n = teeth * 4;
        var pts = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.Tau;
            // each tooth: root, root, tip, tip (a slightly slanted tooth)
            int ph = k % 4;
            float rr = ph == 0 || ph == 3 ? r - tooth * 0.15f : r + tooth;
            if (ph == 1) rr = r + tooth; else if (ph == 2) rr = r + tooth * 0.85f;
            a += ph == 2 ? 0.01f : 0f;
            pts[k] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr;
        }
        float h = thickness * 0.5f;
        int cf = mb.Add(new Vector3(0, 0, h), Vector3.Back, col), cb = mb.Add(new Vector3(0, 0, -h), Vector3.Forward, col);
        var front = new int[n]; var back = new int[n];
        for (int k = 0; k < n; k++)
        {
            front[k] = mb.Add(new Vector3(pts[k].X, pts[k].Y, h), Vector3.Back, col);
            back[k] = mb.Add(new Vector3(pts[k].X, pts[k].Y, -h), Vector3.Forward, col);
        }
        for (int k = 0; k < n; k++)
        {
            int j = (k + 1) % n;
            mb.Tri(cf, front[k], front[j]);
            mb.Tri(cb, back[j], back[k]);
            var e = (pts[j] - pts[k]);
            var nrm = new Vector3(e.Y, -e.X, 0).Normalized();
            var c2 = rim ?? col;
            int a0 = mb.Add(new Vector3(pts[k].X, pts[k].Y, h), nrm, c2), a1 = mb.Add(new Vector3(pts[j].X, pts[j].Y, h), nrm, c2);
            int b0 = mb.Add(new Vector3(pts[k].X, pts[k].Y, -h), nrm, c2), b1 = mb.Add(new Vector3(pts[j].X, pts[j].Y, -h), nrm, c2);
            mb.Tri(a0, b0, b1); mb.Tri(a0, b1, a1);
        }
        return mb;
    }
}

/// <summary>A piston hung from the roof: a housing with two lamps that blink as it gathers itself, a rod, and a heavy striped head that comes down.</summary>
public partial class PistonView : PropView
{
    private MeshInstance3D _rod, _head, _lampL, _lampR, _glow;
    private StandardMaterial3D _lamp;

    protected override void Build()
    {
        var p = (Piston)Owner2D;
        float hw = p.HalfW / W3.Ppu;
        var mb = new MeshBuilder();
        mb.Box(Machine.At(new Vector3(0, -0.3f, 0)), new Vector3(hw * 1.4f, 0.42f, 0.66f), Machine.Dark);
        mb.Box(Machine.At(new Vector3(0, -0.66f, 0)), new Vector3(hw * 1.55f, 0.1f, 0.72f), Machine.Brass);
        for (int s = -1; s <= 1; s += 2)
            for (int k = 0; k < 2; k++)
                mb.Box(Machine.At(new Vector3(s * hw * 1.15f, -0.3f, 0.7f)), new Vector3(0.07f, 0.07f, 0.05f), Machine.Mid);
        AddChild(PropViews.Mesh(mb, Machine.Iron));
        // the rod: a unit tube down from the housing, stretched as the head falls
        var rod = new MeshBuilder();
        rod.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, -1, 0) }, new[] { 0.16f, 0.16f }, 8, new Color(0.62f, 0.64f, 0.7f), capStart: false);
        _rod = PropViews.Mesh(rod, Machine.Iron);
        _rod.Position = new Vector3(0, -0.7f, 0);
        AddChild(_rod);
        // the head: a block with a hazard stripe along its face and a brass band
        var hd = new MeshBuilder();
        hd.Box(Machine.At(Vector3.Zero), new Vector3(hw * 1.05f, 0.45f, 0.64f), Machine.Dark);
        hd.Box(Machine.At(new Vector3(0, 0.36f, 0)), new Vector3(hw * 1.12f, 0.08f, 0.68f), Machine.Brass);
        int stripes = Math.Max(4, (int)(hw * 4f));
        for (int k = 0; k < stripes; k += 2)
        {
            float x = -hw + (k + 0.5f) * (2f * hw / stripes);
            hd.Box(new Transform3D(new Basis(Vector3.Back, 0.5f), new Vector3(x, -0.04f, 0.66f)), new Vector3(hw / stripes * 0.85f, 0.3f, 0.03f), Machine.Hazard);
        }
        _head = PropViews.Mesh(hd, Machine.Iron);
        AddChild(_head);
        _lamp = PropViews.Emissive(new Color(1f, 0.55f, 0.1f), 0.2f);
        var sph = new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 8, Rings = 4 };
        _lampL = new MeshInstance3D { Mesh = sph, MaterialOverride = _lamp, Position = new Vector3(-hw * 0.8f, -0.2f, 0.72f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _lampR = new MeshInstance3D { Mesh = sph, MaterialOverride = _lamp, Position = new Vector3(hw * 0.8f, -0.2f, 0.72f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_lampL); AddChild(_lampR);
        _glow = PropViews.Sprite(new Color(1f, 0.55f, 0.1f), 0, 0f, 1.2f);
        _glow.Position = new Vector3(0, -0.15f, 0.8f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var p = (Piston)Owner2D;
        Follow(default, 0.3f);
        float ext = p.Extension;
        float len = ext * p.Drop / W3.Ppu;
        _head.Position = new Vector3(0, -1.15f - len, 0);
        _rod.Scale = new Vector3(1f, Math.Max(0.02f, len + 0.45f), 1f);
        float blink = p.Warning ? 0.5f + 0.5f * MathF.Sin(Time * 28f) : 0f;
        _lamp.EmissionEnergyMultiplier = 0.15f + 3.2f * blink + (ext > 0.5f ? 1.4f : 0f);
        PropViews.SetSprite(_glow, new Color(1f, 0.5f, 0.1f, 1f), 0, 0.9f * blink);
    }
}

/// <summary>A belt along the floor: a dark bed with cleats that run along it, rollers at each end that turn, amber lamps.</summary>
public partial class ConveyorView : PropView
{
    private readonly List<MeshInstance3D> _cleats = new();
    private MeshInstance3D _r0, _r1;
    private float _len, _spacing = 0.55f;

    protected override void Build()
    {
        var c = (Conveyor)Owner2D;
        _len = c.Half * 2f / W3.Ppu;
        var bed = new MeshBuilder();
        bed.Box(Machine.At(new Vector3(0, 0.02f, 0)), new Vector3(_len * 0.5f, 0.2f, 0.8f), new Color(0.42f, 0.42f, 0.47f));
        bed.Box(Machine.At(new Vector3(0, 0.22f, 0.78f)), new Vector3(_len * 0.5f, 0.08f, 0.07f), Machine.Brass);
        bed.Box(Machine.At(new Vector3(0, 0.22f, -0.78f)), new Vector3(_len * 0.5f, 0.08f, 0.07f), Machine.Brass);
        AddChild(PropViews.Mesh(bed, Machine.Iron, false));
        var cleat = new MeshBuilder();
        cleat.Box(Machine.At(Vector3.Zero), new Vector3(0.09f, 0.07f, 0.72f), Machine.Hazard);
        var mesh = PropViews.Mesh(cleat, Machine.Iron, false).Mesh;
        int n = (int)(_len / _spacing) + 1;
        for (int k = 0; k < n; k++)
        {
            var mi = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _cleats.Add(mi);
            AddChild(mi);
        }
        var roller = new MeshBuilder();
        roller.Tube(new[] { new Vector3(0, 0, -0.8f), new Vector3(0, 0, 0.8f) }, new[] { 0.24f, 0.24f }, 10, Machine.Mid, capStart: true);
        for (int k = 0; k < 4; k++) roller.Box(new Transform3D(new Basis(Vector3.Back, k * Mathf.Pi / 4f), Vector3.Zero), new Vector3(0.27f, 0.035f, 0.82f), Machine.Brass);
        _r0 = PropViews.Mesh(roller, Machine.Iron, false); _r0.Position = new Vector3(-_len * 0.5f, 0.1f, 0);
        _r1 = PropViews.Mesh(roller, Machine.Iron, false); _r1.Position = new Vector3(_len * 0.5f, 0.1f, 0);
        AddChild(_r0); AddChild(_r1);
        var lamp = PropViews.Sprite(new Color(1f, 0.6f, 0.15f), 0, 0.55f, 0.5f);
        lamp.Position = new Vector3(c.Dir > 0 ? _len * 0.5f : -_len * 0.5f, 0.45f, 0.8f);
        AddChild(lamp);
    }

    protected override void Sync(float dt)
    {
        var c = (Conveyor)Owner2D;
        Follow(default, 0f);
        float shift = Mathf.PosMod(Time * c.Speed / W3.Ppu * c.Dir, _spacing);
        for (int k = 0; k < _cleats.Count; k++)
        {
            float x = -_len * 0.5f + Mathf.PosMod(k * _spacing + shift, _len + 0.0001f);
            _cleats[k].Position = new Vector3(x, 0.24f, 0);
        }
        float spin = -Time * c.Speed / W3.Ppu / 0.24f * c.Dir;
        _r0.Rotation = new Vector3(0, 0, spin);
        _r1.Rotation = new Vector3(0, 0, spin);
    }
}

/// <summary>A saw blade, whirring along a rail of iron with posts at each end and sparks where it bites.</summary>
public partial class SawView : PropView
{
    private MeshInstance3D _disc, _glow;

    protected override void Build()
    {
        var s = (SawBlade)Owner2D;
        float r = s.Radius / W3.Ppu;
        var disc = Machine.Disc(14, r, r * 0.28f, 0.09f, new Color(0.7f, 0.72f, 0.78f), new Color(0.55f, 0.57f, 0.62f));
        disc.Box(Machine.At(new Vector3(0, 0, 0.06f)), new Vector3(r * 0.32f, r * 0.32f, 0.05f), Machine.Brass);
        _disc = PropViews.Mesh(disc, Machine.Iron);
        AddChild(_disc);
        // the rail and its posts (still: it hangs in the world, not from the blade)
        var a = W3.P(s.Start); var b = W3.P(s.End);
        var rail = new MeshBuilder();
        var mid = (a + b) * 0.5f;
        float len = a.DistanceTo(b);
        rail.Box(Machine.At(new Vector3(0, 0, -0.1f)), new Vector3(len * 0.5f + 0.15f, 0.05f, 0.06f), Machine.Mid);
        rail.Box(Machine.At(new Vector3(-len * 0.5f - 0.15f, 0, -0.1f)), new Vector3(0.1f, 0.3f, 0.12f), Machine.Dark);
        rail.Box(Machine.At(new Vector3(len * 0.5f + 0.15f, 0, -0.1f)), new Vector3(0.1f, 0.3f, 0.12f), Machine.Dark);
        var rm = PropViews.Mesh(rail, Machine.Iron, false);
        rm.TopLevel = true;
        rm.GlobalTransform = new Transform3D(Basis.Identity, mid);
        AddChild(rm);
        _glow = PropViews.Sprite(new Color(1f, 0.8f, 0.4f), 0, 0.3f, r * 4f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var s = (SawBlade)Owner2D;
        Follow(default, 0f);
        _disc.Rotation = new Vector3(0, 0, -s.Spin);
        PropViews.SetSprite(_glow, new Color(1f, 0.8f, 0.4f, 0.4f + 0.2f * MathF.Sin(Time * 40f)), 0, 0.3f);
    }
}

/// <summary>A lift: a grated platform on a cable, rails at its edges, the cable running up to a pulley wheel (which turns while it moves).</summary>
public partial class LiftView : PropView
{
    private MeshInstance3D _cable, _pulley;
    private float _anchorY;

    protected override void Build()
    {
        var l = (Lift)Owner2D;
        float hw = l.Half / W3.Ppu;
        var mb = new MeshBuilder();
        mb.Box(Machine.At(new Vector3(0, -0.07f, 0)), new Vector3(hw, 0.07f, 0.55f), Machine.Dark);
        mb.Box(Machine.At(new Vector3(0, 0.0f, 0)), new Vector3(hw * 0.95f, 0.02f, 0.5f), Machine.Mid);
        for (int k = -2; k <= 2; k++) mb.Box(Machine.At(new Vector3(k * hw * 0.38f, 0.025f, 0)), new Vector3(0.025f, 0.015f, 0.5f), Machine.Dark);
        mb.Box(Machine.At(new Vector3(-hw, 0.12f, 0.5f)), new Vector3(0.05f, 0.22f, 0.05f), Machine.Brass);
        mb.Box(Machine.At(new Vector3(hw, 0.12f, 0.5f)), new Vector3(0.05f, 0.22f, 0.05f), Machine.Brass);
        mb.Box(Machine.At(new Vector3(0, 0.3f, 0.5f)), new Vector3(hw, 0.03f, 0.03f), Machine.Brass);
        AddChild(PropViews.Mesh(mb, Machine.Iron));
        var cab = new MeshBuilder();
        cab.Tube(new[] { Vector3.Zero, Vector3.Up }, new[] { 0.025f, 0.025f }, 5, new Color(0.25f, 0.24f, 0.25f), capStart: false);
        _cable = PropViews.Mesh(cab, Machine.Iron, false);
        AddChild(_cable);
        _anchorY = -(l.Top - 92f) / W3.Ppu;
        var pul = Machine.Disc(10, 0.3f, 0.06f, 0.12f, Machine.Mid, Machine.Brass);
        _pulley = PropViews.Mesh(pul, Machine.Iron, false);
        _pulley.TopLevel = true;
        _pulley.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(l.GlobalPosition.X / W3.Ppu, _anchorY, 0.1f));
        AddChild(_pulley);
    }

    protected override void Sync(float dt)
    {
        var l = (Lift)Owner2D;
        Follow(default, 0f);
        float cableLen = Math.Max(0.1f, _anchorY - Position.Y);
        _cable.Position = new Vector3(0, 0.3f, 0);
        _cable.Scale = new Vector3(1f, cableLen, 1f);
        if (l.Moving) _pulley.Rotation = new Vector3(0, 0, _pulley.Rotation.Z + dt * 3f);
    }
}

/// <summary>A jet of steam: puffs of white cloud that shoot out along it and swell as they go, hot at the nozzle.</summary>
public partial class SteamJetView : PropView
{
    private readonly MeshInstance3D[] _puffs = new MeshInstance3D[9];
    private MeshInstance3D _glow;

    protected override void Build()
    {
        for (int k = 0; k < _puffs.Length; k++)
        {
            _puffs[k] = new MeshInstance3D { Mesh = PropViews.Quad, MaterialOverride = PropViews.CloudMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _puffs[k].SetInstanceShaderParameter("cloud_seed", k * 1.7f);
            AddChild(_puffs[k]);
        }
        _glow = PropViews.Sprite(new Color(1f, 0.95f, 0.85f), 4, 0.5f, 0.8f);
        AddChild(_glow);
    }

    protected override void Sync(float dt)
    {
        var j = (SteamJet)Owner2D;
        Follow(default, 0.3f);
        var dir = new Vector3(j.Dir.X, -j.Dir.Y, 0f);
        float reach = j.Reach / W3.Ppu;
        float fade = Math.Clamp((j.Life - j.T) / 0.4f, 0f, 1f);
        for (int k = 0; k < _puffs.Length; k++)
        {
            float u = ((k + 0.5f) / _puffs.Length + j.T * 1.4f) % 1f;
            _puffs[k].Position = dir * reach * u + new Vector3(0, 0.04f * MathF.Sin(k * 3f + Time * 9f), 0);
            _puffs[k].Scale = Vector3.One * (0.25f + j.Width / W3.Ppu * 0.55f * u + 0.06f * (k % 3));
            _puffs[k].SetInstanceShaderParameter("cloud_color", new Color(0.93f, 0.93f, 0.96f, 0.6f * fade * (1f - u * 0.6f)));
            _puffs[k].SetInstanceShaderParameter("cloud_glow", 0.35f);
        }
        _glow.Visible = fade > 0.1f;
        PropViews.SetSprite(_glow, new Color(1f, 0.95f, 0.85f, fade), 4, 0.5f);
    }
}
