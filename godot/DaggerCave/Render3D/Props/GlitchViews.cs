using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Broken rectangles: the stuff the Glitch is made of, and what the wall it leaves behind turns into. A heap of flat, unlit boxes in
/// the colours of a broken screen (and the black and magenta checker of a texture that isn't there), each jumping, stretching into a
/// torn scanline and changing colour on its own beat. The randomness is visual only (its own generator, never the game's).
/// </summary>
public abstract partial class BrokenBoxes : PropView
{
    protected override bool ActorLit => false;
    protected readonly Random Vis = new();
    protected float VR(float a, float b) => a + (float)Vis.NextDouble() * (b - a);

    protected sealed class Box
    {
        public MeshInstance3D Mesh;
        public StandardMaterial3D Mat;
        public Vector3 Home, Size, Off;
        public float Beat, Tear;
    }
    protected readonly List<Box> Boxes = new();
    protected Node3D Heap;
    protected OmniLight3D Glow;

    private static readonly Color[] Screen =
    {
        new(1f, 0f, 1f), new(0f, 1f, 1f), new(0.15f, 1f, 0.2f), new(1f, 1f, 0f), new(1f, 1f, 1f), new(0.02f, 0.02f, 0.02f), new(0.3f, 0.1f, 0.6f), new(1f, 0.3f, 0.1f),
    };
    protected Color Pick() => Screen[Vis.Next(Screen.Length)];

    private static ImageTexture _checker;
    /// <summary>The black and magenta checkerboard of a texture that failed to load.</summary>
    protected static ImageTexture Checker
    {
        get
        {
            if (_checker != null) return _checker;
            var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgb8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    img.SetPixel(x, y, ((x / 2 + y / 2) % 2 == 0) ? new Color(1f, 0f, 1f) : new Color(0f, 0f, 0f));
            return _checker = ImageTexture.CreateFromImage(img);
        }
    }

    protected Box AddBox(Vector3 home, Vector3 size, bool checker)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = checker ? Colors.White : Pick(),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
        if (checker) { mat.AlbedoTexture = Checker; mat.Uv1Scale = new Vector3(VR(1f, 4f), VR(1f, 4f), 1f); }
        var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Heap.AddChild(mi);
        var b = new Box { Mesh = mi, Mat = mat, Home = home, Size = size, Beat = VR(0f, 0.3f) };
        Boxes.Add(b);
        Place(b);
        return b;
    }

    protected void Place(Box b)
    {
        var size = b.Size;
        if (b.Tear > 0) size = new Vector3(size.X * 6f, size.Y * 0.18f, size.Z);
        b.Mesh.Position = b.Home + b.Off;
        b.Mesh.Scale = new Vector3(Math.Max(0.001f, size.X), Math.Max(0.001f, size.Y), Math.Max(0.001f, size.Z));
    }

    /// <summary>Each box on its own beat: a jump, a colour, now and then a torn scanline; <paramref name="wild"/> (0..1) makes it all worse.</summary>
    protected void Jitter(float dt, float wild, float spread)
    {
        foreach (var b in Boxes)
        {
            if (b.Tear > 0) b.Tear -= dt;
            if ((b.Beat -= dt) > 0) continue;
            b.Beat = VR(0.03f, 0.25f) * (1f - 0.6f * wild);
            b.Off = new Vector3(VR(-1f, 1f), VR(-1f, 1f), VR(-0.3f, 0.3f)) * spread * (0.4f + wild);
            if (b.Mat.AlbedoTexture == null && Vis.NextDouble() < 0.5) b.Mat.AlbedoColor = Pick();
            if (Vis.NextDouble() < 0.06 + 0.2 * wild) b.Tear = VR(0.04f, 0.12f);
            b.Mesh.Visible = Vis.NextDouble() > 0.08;
            Place(b);
        }
        if (Glow != null && Vis.NextDouble() < 0.3) { Glow.LightColor = Pick(); Glow.LightEnergy = VR(0.4f, 1.4f) * (1f + wild); }
    }
}

/// <summary>The Glitch itself: a heap of broken boxes bigger than a hero, drawn somewhere near where it really is (never quite there).</summary>
public partial class GlitchView : BrokenBoxes
{
    private Vector2 _shown;
    private float _showT;

    protected override void Build()
    {
        Heap = new Node3D();
        AddChild(Heap);
        float r = W3.M(Tune.Glitch.Radius);
        for (int k = 0; k < 22; k++)
        {
            var home = new Vector3(VR(-1f, 1f) * r, VR(-1f, 1.3f) * r, VR(-0.4f, 0.4f) * r);
            var size = new Vector3(VR(0.25f, 0.9f), VR(0.15f, 0.7f), VR(0.15f, 0.5f)) * r;
            AddBox(home, size, k % 5 == 0);
        }
        Glow = PropViews.Light(new Color(1f, 0f, 1f), 0.8f, 3.5f);
        AddChild(Glow);
    }

    protected override void Sync(float dt)
    {
        var g = (Glitch)Owner2D;
        // where it seems to be: anywhere within ShowRadius of where it is, re-rolled every few frames
        if ((_showT -= dt) <= 0)
        {
            _showT = VR(Tune.Glitch.ShowMin, Tune.Glitch.ShowMax);
            float a = VR(0f, Mathf.Tau), d = MathF.Sqrt(VR(0f, 1f)) * Tune.Glitch.ShowRadius;
            _shown = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
        }
        Follow(_shown, 0.1f);
        float wild = g.Winding ? 1f : g.HurtFlashT > 0 ? 0.7f : 0f;
        Jitter(dt, wild, W3.M(Tune.Glitch.Radius) * 0.35f);
        Heap.Scale = Vector3.One * (g.Winding ? VR(1f, 1.35f) : 1f);
    }
}

/// <summary>The wall the Glitch leaves wrong behind it: a patch of the chamber's end wall gone to broken boxes and missing textures (walk into it).</summary>
public partial class GlitchWallView : BrokenBoxes
{
    protected override void Build()
    {
        Heap = new Node3D();
        AddChild(Heap);
        float w = W3.M(Tune.Glitch.WallWidth), h = W3.M(Tune.Glitch.WallHeight);
        for (int k = 0; k < 40; k++)
        {
            var home = new Vector3(VR(-0.5f, 0.5f) * w, VR(-0.5f, 0.5f) * h, VR(-0.6f, 0.4f));
            var size = new Vector3(VR(0.08f, 0.35f) * w, VR(0.05f, 0.22f) * h, VR(0.1f, 0.6f));
            AddBox(home, size, k % 3 == 0);
        }
        Glow = PropViews.Light(new Color(1f, 0f, 1f), 1.2f, 5f);
        Glow.Position = new Vector3(0, 0, 1f);
        AddChild(Glow);
    }

    protected override void Sync(float dt)
    {
        var p = (Portal)Owner2D;
        Follow(new Vector2(0, -Tune.Glitch.WallHeight * 0.5f + 16f), 0f);
        Jitter(dt, p.Near, W3.M(4f));
    }
}

/// <summary>
/// The wall of a secret wing: the very rock the cave's wall is (the terrain's own mesher, field and material, run on the field with the
/// passage's mouth put back as rock), so it lines up with the wall round it and there is nothing to tell it apart at a glance. Struck, it
/// shudders; brought down, it falls in pieces and the passage stands open.
/// </summary>
public partial class SecretWallView : PropView
{
    protected override bool ActorLit => false;
    private MeshInstance3D _body;
    private Node3D _pivot;
    private readonly List<MeshInstance3D> _bits = new();
    private readonly List<Vector3> _vel = new(), _spin = new();
    private float _fall = -1f;

    protected override void Build()
    {
        var r = (Rubble)Owner2D;
        var cave = G.Cave;
        var wing = cave.Wings.FirstOrDefault(w => w.RubbleIndex == r.Index);
        var mat = (Material)G.Main.Stage?.Terrain?.Material ?? TerrainLook.Make(cave);
        Vector3 off = default;
        List<(int kind, Transform3D xf)> grass = null;
        var mesh = wing?.Wall != null ? LedgeMesh.Build(cave, wing.Wall, mat, out off, out grass) : null;
        if (mesh == null) return;
        _body = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On, Position = off };
        // (the mesh's own origin is the cave's corner: it trembles in a pivot at the wall's middle)
        _pivot = new Node3D();
        AddChild(_pivot);
        _pivot.AddChild(_body);
    }

    private void Crumble(Rubble r)
    {
        var rng = new Random(r.Index * 29 + 5);
        var noise = new Noise3(rng.Next());
        var wing = G.Cave.Wings.FirstOrDefault(q => q.RubbleIndex == r.Index);
        float span = wing?.WallSpan ?? 2f;
        float w = span, h = W3.M(r.Size.Y);
        float x0 = (span - 2f) * 0.5f - 0f; // (the wall runs on to the right of the plug: its middle is a little to the right of the plug's)
        var e0 = (G.Biome?.Edge ?? new Color(0.4f, 0.35f, 0.3f)).Lerp(new Color(0.5f, 0.5f, 0.5f), 0.35f);
        var tint = new Color(Math.Min(1.4f, e0.R * 2.6f), Math.Min(1.4f, e0.G * 2.6f), Math.Min(1.4f, e0.B * 2.6f));
        int count = 16 + (int)(span * 1.5f);
        for (int i = 0; i < count; i++)
        {
            var mb = DecorMeshes.SlabChunk(rng, noise, 0.5f + (float)rng.NextDouble() * 0.8f, 0.45f + (float)rng.NextDouble() * 0.6f, 0.7f + (float)rng.NextDouble() * 0.6f, tint);
            var node = PropViews.Mesh(mb, PropViews.LedgeRock);
            node.Position = new Vector3(x0 + ((float)rng.NextDouble() - 0.5f) * w, ((float)rng.NextDouble() - 0.5f) * h, ((float)rng.NextDouble() - 0.5f) * 1.2f);
            node.Rotation = new Vector3((float)rng.NextDouble() * 3f, (float)rng.NextDouble() * 3f, (float)rng.NextDouble() * 3f);
            AddChild(node);
            _bits.Add(node);
            _vel.Add(new Vector3(((float)rng.NextDouble() - 0.5f) * 2.4f, 0.4f + (float)rng.NextDouble() * 2f, ((float)rng.NextDouble() - 0.2f) * 2f));
            _spin.Add(new Vector3(((float)rng.NextDouble() - 0.5f) * 8f, ((float)rng.NextDouble() - 0.5f) * 8f, ((float)rng.NextDouble() - 0.5f) * 8f));
        }
        if (_body != null) _body.Visible = false;
    }

    protected override void Sync(float dt)
    {
        var r = (Rubble)Owner2D;
        Follow(default, 0f);
        if (!r.Cleared)
        {
            if (_body == null) return;
            // it shudders when struck: a small, quick tremor that settles
            float k = Math.Clamp(r.ShakeT / 0.45f, 0f, 1f);
            k *= k;
            _pivot.Position = new Vector3(MathF.Sin(Time * 61f) * 0.02f * k, MathF.Sin(Time * 47f + 1f) * 0.012f * k, 0f);
            return;
        }
        if (_fall < 0) { _fall = 0; Crumble(r); }
        _fall += dt;
        for (int i = 0; i < _bits.Count; i++)
        {
            _vel[i] = _vel[i] with { Y = _vel[i].Y - 9.8f * 1.6f * dt };
            _bits[i].Position += _vel[i] * dt;
            _bits[i].Rotation += _spin[i] * dt;
            float s = Math.Clamp(1f - (_fall - 1.2f) / 1.0f, 0f, 1f);
            _bits[i].Scale = Vector3.One * Math.Max(0.001f, s);
        }
    }
}
