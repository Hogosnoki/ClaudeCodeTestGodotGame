using System;
using Godot;

namespace DaggerCave;

/// <summary>Flat back wall plus a parallax layer of dim rock silhouettes behind the tunnels.</summary>
public partial class Backdrop : Node2D
{
    private CaveData _cave;
    private readonly Random _rng = new();

    public void Setup(CaveData cave)
    {
        _cave = cave;
        ZIndex = -20;
        var par = new Parallax2D { ScrollScale = new Vector2(0.55f, 0.55f), ZIndex = -19 };
        par.AddChild(new FarRocks { Cave = cave });
        AddChild(par);
    }

    public override void _Draw()
    {
        var size = _cave.SizePx;
        var pal = CaveView.Palette(_cave);
        var top = pal.deep.Lerp(pal.edge, 0.35f);
        var bottom = _cave.Biome?.BackBottom ?? new Color(0.02f, 0.05f, 0.1f);
        const int bands = 24;
        for (int b = 0; b < bands; b++)
        {
            float y0 = size.Y * b / bands, y1 = size.Y * (b + 1) / bands;
            DrawRect(new Rect2(-400, y0, size.X + 800, y1 - y0 + 1), top.Lerp(bottom, b / (float)(bands - 1)));
        }
    }

    private partial class FarRocks : Node2D
    {
        public CaveData Cave;

        public override void _Draw()
        {
            var rng = new Random(Cave.Seed * 3 + 1);
            var size = Cave.SizePx;
            var pal = CaveView.Palette(Cave);
            var col = pal.edge.Darkened(0.3f);
            col.A = 0.45f;
            // Pillars / stalactite silhouettes spread over the (parallax-shrunk) map extent.
            for (int k = 0; k < 140; k++)
            {
                float x = (float)rng.NextDouble() * size.X * 0.7f;
                float y = (float)rng.NextDouble() * size.Y * 0.7f;
                float w = 20 + (float)rng.NextDouble() * 60;
                float h = 60 + (float)rng.NextDouble() * 200;
                bool down = rng.NextDouble() < 0.5;
                var pts = new Vector2[] { new(x - w, y), new(x + w, y), new(x + w * 0.2f, y + (down ? h : -h)) };
                DrawColoredPolygon(pts, col);
            }
            for (int k = 0; k < 60; k++)
            {
                var c = new Vector2((float)rng.NextDouble() * size.X * 0.7f, (float)rng.NextDouble() * size.Y * 0.7f);
                DrawCircle(c, 30 + (float)rng.NextDouble() * 90, new Color(col, 0.25f));
            }
        }
    }
}

/// <summary>
/// Translucent water drawn over entities (so submerged things look tinted) but under the rock,
/// with an animated surface line. Only the part inside the camera view is drawn.
/// </summary>
public partial class WaterView : Node2D
{
    private CaveData _cave;
    private float _t;

    public void Setup(CaveData cave) { _cave = cave; ZIndex = 5; }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var cam = GetViewport().GetCamera2D();
        if (cam == null) return;
        var vs = GetViewportRect().Size / cam.Zoom;
        var c = cam.GetScreenCenterPosition();
        float x0 = c.X - vs.X * 0.6f, x1 = c.X + vs.X * 0.6f;
        float yTop = c.Y - vs.Y * 0.6f, yBot = c.Y + vs.Y * 0.6f;
        float wy = _cave.WaterY;
        if (_cave.Liquid == Liquid.None || yBot < wy - 10) return;
        var bd = _cave.Biome ?? Biomes.Get(BiomeId.Slime);
        bool lava = _cave.Liquid == Liquid.Lava;
        float y0 = Math.Max(wy, yTop);
        // Depth-graded bands.
        const int bands = 6;
        for (int b = 0; b < bands; b++)
        {
            float ya = y0 + (yBot - y0) * b / bands, yb = y0 + (yBot - y0) * (b + 1) / bands;
            float depth = Math.Clamp((ya - wy) / (lava ? 200f : 900f), 0, 1);
            DrawRect(new Rect2(x0, ya, x1 - x0, yb - ya + 1), bd.LiquidTop.Lerp(bd.LiquidBottom, depth));
        }
        if (lava)
        {
            // molten glow and slow bright blotches
            for (int k = 0; k < 10; k++)
            {
                float x = Mathf.Floor(x0 / 70f) * 70f + k * 110f + Mathf.Sin(_t * 0.5f + k * 1.7f) * 30f;
                DrawCircle(new Vector2(x, wy + 18 + Mathf.Sin(_t + k) * 6), 22 + Mathf.Sin(_t * 1.3f + k) * 6, new Color(1f, 0.8f, 0.3f, 0.12f));
            }
            DrawRect(new Rect2(x0, wy - 40, x1 - x0, 40), new Color(1f, 0.45f, 0.1f, 0.06f));
            if (G.Fx != null && G.Chance(0.25f)) G.Fx.Ember(new Vector2(G.Range(x0, x1), wy), new Color(1f, 0.6f, 0.2f));
        }
        if (wy > yTop - 20)
        {
            int n = 64;
            var pts = new Vector2[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float x = x0 + (x1 - x0) * k / n;
                pts[k] = new Vector2(x, wy + Mathf.Sin(x * 0.03f + _t * 2f) * 1.6f + Mathf.Sin(x * 0.071f - _t * 1.3f) * 1.1f);
            }
            DrawPolyline(pts, bd.LiquidLine, lava ? 3f : 2f);
            // light shafts just below the surface
            for (int k = 0; k < (lava ? 0 : 8); k++)
            {
                float x = Mathf.Floor(x0 / 90f) * 90f + k * 150f + Mathf.Sin(_t * 0.4f + k) * 20f;
                DrawColoredPolygon(new[] { new Vector2(x, wy), new Vector2(x + 30, wy), new Vector2(x + 60, wy + 140), new Vector2(x + 10, wy + 140) },
                    new Color(0.6f, 0.9f, 1f, 0.04f));
            }
        }
    }
}
