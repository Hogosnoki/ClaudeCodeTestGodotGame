using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The screen over a level that is being made: shown the moment the party takes an exit, with a picture of the kind of place
/// they are going to (its colours, and the things that are its own: the catacombs' skulls and arches, the lava tubes' glowing
/// bore, the mine's timbers, the frost's snow...), the depth, a line of the story, and a moving flame so it is plain that the
/// game has not stopped. Drawn in code from the biome's palette, so every biome has its own with no art to carry.
/// </summary>
public partial class LoadingScreen : Control
{
    private BiomeDef _b;
    private int _depth;
    private float _t, _alpha;
    private bool _open;
    private string _tip = "";
    private readonly List<(Vector2 pos, float seed)> _motes = new();
    private Random _rng = new(1);

    public bool IsOpen => _open;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    /// <summary>Shows the screen for a place about to be made.</summary>
    public void Open(BiomeDef b, int depth)
    {
        _b = b; _depth = depth;
        _t = 0; _alpha = 0; _open = true; Visible = true;
        _rng = new Random((int)b.Id * 7919 + depth);
        _tip = Lore.Tips[_rng.Next(Lore.Tips.Length)];
        _motes.Clear();
        for (int k = 0; k < 70; k++) _motes.Add((new Vector2((float)_rng.NextDouble() * 1280, (float)_rng.NextDouble() * 720), (float)_rng.NextDouble()));
        QueueRedraw();
    }

    public void Close() { _open = false; }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _t += (float)delta;
        _alpha = _open ? Math.Min(1f, _alpha + (float)delta * 5f) : Math.Max(0f, _alpha - (float)delta * 3.5f);
        if (!_open && _alpha <= 0f) { Visible = false; return; }
        QueueRedraw();
    }

    private static Color Mix(Color a, Color b, float t) => a.Lerp(b, Math.Clamp(t, 0f, 1f));

    public override void _Draw()
    {
        if (_b == null) return;
        var vs = new Vector2(1280, 720);
        float a = W3.Smooth01(_alpha);
        var deep = _b.Deep; var edge = _b.Edge; var glow = _b.Glow; var back = _b.BackBottom;
        // the dark of the place, brighter toward its glow
        const int bands = 24;
        for (int k = 0; k < bands; k++)
        {
            float y0 = vs.Y * k / bands, y1 = vs.Y * (k + 1) / bands;
            float t = k / (float)(bands - 1);
            var c = Mix(Mix(deep.Darkened(0.35f), back, 0.5f), Mix(deep, edge, 0.35f), (float)Math.Pow(t, 1.4));
            DrawRect(new Rect2(0, y0, vs.X, y1 - y0 + 1), new Color(c.R, c.G, c.B, a));
        }
        DrawGlow(new Vector2(vs.X * 0.5f, vs.Y * 0.62f), 520, glow, 0.2f * a);
        DrawMotif(a);
        // the ceiling and the floor of the cave, as black teeth
        var rock = Mix(back, Colors.Black, 0.55f);
        DrawTeeth(true, rock, a, _b.Stalactites * 2.2f + 0.25f);
        DrawTeeth(false, rock, a, 0.4f);
        // dust, spores, snow, embers: the biome's air
        DrawMotes(a);

        var font = ThemeDB.FallbackFont;
        // the words
        float cx = vs.X * 0.5f;
        string depthText = _depth == 0 ? "THE WAY DOWN" : $"DEPTH {_depth}";
        DrawCentered(font, depthText, new Vector2(cx, 262), 20, Mix(glow, Colors.White, 0.35f), a * 0.9f);
        string name = _b.Name.ToUpperInvariant();
        DrawCentered(font, name, new Vector2(cx, 322), 60, Mix(glow, Colors.White, 0.55f), a, outline: true);
        float lw = 180 + 80 * MathF.Sin(_t * 1.5f);
        DrawRect(new Rect2(cx - lw * 0.5f, 342, lw, 2), new Color(glow.R, glow.G, glow.B, 0.7f * a));
        string line = Lore.Line(_b.Id);
        if (line != "") DrawWrapped(font, line, new Vector2(cx, 386), 18, new Color(0.92f, 0.9f, 0.84f), a * 0.92f, 640);
        DrawCentered(font, _tip, new Vector2(cx, 560), 14, new Color(0.8f, 0.78f, 0.74f), a * 0.6f);
        // the flame, turning
        DrawFlame(new Vector2(1180, 640), a);
        string dots = new string('.', 1 + (int)(_t * 2.5f) % 3);
        DrawString(font, new Vector2(1000, 648), "Descending" + dots, HorizontalAlignment.Right, 150, 16, new Color(1f, 0.9f, 0.7f, 0.8f * a));
    }

    private void DrawCentered(Font font, string s, Vector2 at, int size, Color c, float a, bool outline = false)
    {
        var sz = font.GetStringSize(s, HorizontalAlignment.Left, -1, size);
        var p = new Vector2(at.X - sz.X * 0.5f, at.Y);
        if (outline) DrawString(font, p, s, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, 0.85f * a), 0, 0);
        if (outline) for (int dx = -2; dx <= 2; dx += 2) for (int dy = -2; dy <= 2; dy += 2) DrawString(font, p + new Vector2(dx, dy), s, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, 0.7f * a));
        DrawString(font, p, s, HorizontalAlignment.Left, -1, size, new Color(c.R, c.G, c.B, a));
    }

    private void DrawWrapped(Font font, string s, Vector2 at, int size, Color c, float a, float width)
    {
        // (centred lines, broken at spaces)
        var words = s.Split(' ');
        var lines = new List<string>(); string cur = "";
        foreach (var w in words)
        {
            string tryLine = cur == "" ? w : cur + " " + w;
            if (font.GetStringSize(tryLine, HorizontalAlignment.Left, -1, size).X > width && cur != "") { lines.Add(cur); cur = w; }
            else cur = tryLine;
        }
        if (cur != "") lines.Add(cur);
        for (int k = 0; k < lines.Count; k++) DrawCentered(font, lines[k], at + new Vector2(0, k * (size + 8)), size, c, a);
    }

    private void DrawGlow(Vector2 at, float r, Color c, float a)
    {
        const int steps = 28;
        for (int k = steps; k >= 1; k--)
        {
            float f = k / (float)steps;
            DrawCircle(at, r * f, new Color(c.R, c.G, c.B, a * 0.5f * (1f - f) / steps * 3.2f));
        }
    }

    private void DrawFlame(Vector2 at, float a)
    {
        DrawGlow(at, 70, new Color(1f, 0.6f, 0.2f), 0.35f * a);
        for (int k = 0; k < 3; k++)
        {
            float f = 1f - k * 0.28f;
            float sway = MathF.Sin(_t * 7f + k * 1.7f) * 3f;
            var pts = new[] { at + new Vector2(-9 * f, 0), at + new Vector2(-5 * f + sway * 0.4f, -16 * f), at + new Vector2(sway, -34 * f), at + new Vector2(5 * f + sway * 0.4f, -16 * f), at + new Vector2(9 * f, 0) };
            var c = k == 0 ? new Color(1f, 0.45f, 0.1f) : k == 1 ? new Color(1f, 0.72f, 0.2f) : new Color(1f, 0.95f, 0.6f);
            DrawColoredPolygon(pts, new Color(c.R, c.G, c.B, 0.95f * a));
        }
        // the lamp under it
        DrawRect(new Rect2(at.X - 12, at.Y, 24, 6), new Color(0.2f, 0.17f, 0.14f, a));
        DrawRect(new Rect2(at.X - 8, at.Y + 6, 16, 4), new Color(0.14f, 0.12f, 0.1f, a));
    }

    /// <summary>Black teeth along the top (stalactites) or bottom (stalagmites) of the screen.</summary>
    private void DrawTeeth(bool top, Color c, float a, float density)
    {
        var rng = new Random((int)_b.Id * 31 + (top ? 1 : 2));
        var pts = new List<Vector2>();
        float x = -20;
        float baseH = top ? 60 : 44;
        pts.Add(new Vector2(-20, top ? -10 : 730));
        while (x < 1300)
        {
            float w = 30 + (float)rng.NextDouble() * 70;
            // (the last tooth ends on the screen's edge: one that ran past it would double back across itself, and a polygon that crosses itself can't be drawn)
            if (x + w > 1300) w = 1300 - x;
            if (w < 6) break;
            float h = baseH + (float)rng.NextDouble() * (top ? 120 : 70) * Math.Clamp(density, 0.2f, 1.4f);
            if (rng.NextDouble() < 0.35) h *= 0.35f;
            float y = top ? h : 720 - h;
            float by = top ? baseH * 0.5f : 720 - baseH * 0.5f;
            pts.Add(new Vector2(x, by));
            pts.Add(new Vector2(x + w * 0.5f, y));
            x += w;
        }
        pts.Add(new Vector2(1300, top ? baseH * 0.5f : 720 - baseH * 0.5f));
        pts.Add(new Vector2(1300, top ? -10 : 730));
        DrawColoredPolygon(pts.ToArray(), new Color(c.R, c.G, c.B, a));
    }

    private void DrawMotes(float a)
    {
        var glow = _b.Glow;
        for (int k = 0; k < _motes.Count; k++)
        {
            var (p0, seed) = _motes[k];
            Vector2 p; float r = 1.2f + seed * 1.6f; var c = new Color(glow.R, glow.G, glow.B, (0.25f + 0.4f * seed) * a);
            switch (_b.Id)
            {
                case BiomeId.Frost:
                    p = new Vector2((p0.X + _t * (14 + 20 * seed) * 0.6f + MathF.Sin(_t + seed * 9) * 14) % 1300, (p0.Y + _t * (30 + 40 * seed)) % 740);
                    c = new Color(0.9f, 0.97f, 1f, (0.35f + 0.5f * seed) * a); r += 0.6f; break;
                case BiomeId.Magma: case BiomeId.LavaTubes: case BiomeId.Lair:
                    p = new Vector2((p0.X + MathF.Sin(_t * 0.7f + seed * 8) * 30) % 1300, 760 - ((p0.Y + _t * (30 + 70 * seed)) % 780));
                    c = new Color(1f, 0.55f + 0.3f * seed, 0.15f, (0.4f + 0.5f * seed) * a); break;
                case BiomeId.Fungal:
                    p = new Vector2((p0.X + MathF.Sin(_t * 0.5f + seed * 6) * 40) % 1300, 740 - ((p0.Y + _t * (12 + 20 * seed)) % 760));
                    break;
                case BiomeId.Slime: case BiomeId.Roots:
                    p = new Vector2(p0.X, (p0.Y + _t * (80 + 90 * seed)) % 740); r *= 0.8f;
                    c = new Color(0.6f, 0.85f, 1f, 0.3f * a);
                    break;
                default:
                    p = new Vector2((p0.X + MathF.Sin(_t * 0.4f + seed * 10) * 25 + _t * 6) % 1300, (p0.Y + MathF.Cos(_t * 0.3f + seed * 7) * 20) % 740);
                    break;
            }
            DrawCircle(p, r, c);
        }
    }

    // ------------------------------------------------------------------------------------------------ what is each place's own

    private void DrawMotif(float a)
    {
        var edge = _b.Edge; var glow = _b.Glow; var rim = _b.Rim;
        var dark = Mix(_b.Deep, Colors.Black, 0.4f);
        switch (_b.Id)
        {
            case BiomeId.Catacombs: Catacombs(a); break;
            case BiomeId.LavaTubes: LavaTube(a); break;
            case BiomeId.Magma: case BiomeId.Lair:
                // a lake of fire along the bottom
                for (int k = 0; k < 40; k++)
                {
                    float x = k * 34f;
                    float h = 70 + 18 * MathF.Sin(_t * 1.5f + k * 0.6f) + 10 * MathF.Sin(_t * 3f + k);
                    DrawRect(new Rect2(x, 720 - h, 36, h), new Color(1f, 0.4f + 0.2f * MathF.Sin(k + _t), 0.08f, 0.55f * a));
                }
                break;
            case BiomeId.Null:
            {
                // broken rectangles, jumping every few frames: a screen that has stopped working properly
                var rng = new Random((int)(_t * 12f));
                for (int k = 0; k < 26; k++)
                {
                    var c = rng.Next(4) switch { 0 => new Color(1f, 0f, 1f), 1 => new Color(0f, 1f, 1f), 2 => new Color(0.2f, 1f, 0.2f), _ => new Color(1f, 1f, 1f) };
                    float w = 20 + rng.Next(220), h = 3 + rng.Next(rng.Next(5) == 0 ? 90 : 16);
                    DrawRect(new Rect2(rng.Next(1280), rng.Next(720), w, h), new Color(c, (0.12f + 0.2f * (float)rng.NextDouble()) * a));
                }
                break;
            }
            case BiomeId.Abyss:
                // black water over the lower half, a few pale shafts of light falling into it, and a whirl of bubbles at the bottom
                for (int k = 0; k < 7; k++)
                {
                    float x = 130 + k * 170 + MathF.Sin(_t * 0.4f + k) * 20;
                    DrawColoredPolygon(new[] { new Vector2(x - 8, 300), new Vector2(x + 8, 300), new Vector2(x + 60, 720), new Vector2(x - 60, 720) }, new Color(glow.R, glow.G, glow.B, 0.05f * a));
                }
                var surf = new List<Vector2>();
                for (int k = 0; k <= 64; k++) surf.Add(new Vector2(k * 20f, 330 + 7 * MathF.Sin(_t * 1.1f + k * 0.45f) + 4 * MathF.Sin(_t * 2.3f + k * 0.9f)));
                var water = new List<Vector2>(surf) { new Vector2(1280, 720), new Vector2(0, 720) };
                DrawColoredPolygon(water.ToArray(), new Color(0.02f, 0.1f, 0.2f, 0.7f * a));
                for (int k = 0; k + 1 < surf.Count; k++) DrawLine(surf[k], surf[k + 1], new Color(glow.R, glow.G, glow.B, 0.5f * a), 2);
                for (int k = 0; k < 14; k++)
                {
                    float ph = (_t * 0.25f + k * 0.37f) % 1f;
                    DrawArc(new Vector2(640 + MathF.Sin(k * 2.1f + _t) * 30 * (1f - ph), 690 - ph * 280), 4 + 8 * ph, 0, Mathf.Tau, 14, new Color(0.7f, 0.95f, 1f, 0.45f * (1f - ph) * a), 1.5f);
                }
                break;
            case BiomeId.Mine:
                // a timbered gallery: two posts and a beam, a lamp swinging from it
                DrawRect(new Rect2(240, 190, 22, 560), new Color(0.28f, 0.19f, 0.11f, a));
                DrawRect(new Rect2(1018, 190, 22, 560), new Color(0.28f, 0.19f, 0.11f, a));
                DrawRect(new Rect2(210, 180, 860, 26), new Color(0.32f, 0.22f, 0.13f, a));
                float sw = MathF.Sin(_t * 1.6f) * 0.12f;
                var lamp = new Vector2(640 + MathF.Sin(sw) * 70, 206 + MathF.Cos(sw) * 70);
                DrawLine(new Vector2(640, 206), lamp, new Color(0.2f, 0.17f, 0.14f, a), 2);
                DrawGlow(lamp, 140, glow, 0.5f * a);
                DrawRect(new Rect2(lamp.X - 7, lamp.Y, 14, 16), new Color(1f, 0.8f, 0.4f, a));
                break;
            case BiomeId.Ruins:
                // a broken arch and a column
                for (int k = 0; k < 14; k++)
                {
                    float ang = Mathf.Pi + k / 13f * Mathf.Pi * (k < 10 ? 1f : 0f);
                    if (k >= 10) continue;
                    var p = new Vector2(640, 520) + new Vector2(MathF.Cos(ang) * 270, MathF.Sin(ang) * 270);
                    DrawRect(new Rect2(p - new Vector2(22, 14), new Vector2(44, 28)), new Color(rim.R, rim.G, rim.B, 0.55f * a));
                }
                DrawRect(new Rect2(330, 420, 40, 300), new Color(rim.R, rim.G, rim.B, 0.5f * a));
                DrawRect(new Rect2(910, 520, 40, 200), new Color(rim.R, rim.G, rim.B, 0.45f * a));
                break;
            case BiomeId.Crystal:
                for (int k = 0; k < 11; k++)
                {
                    float x = 120 + k * 105 + (k % 2) * 30, h = 90 + (k * 53 % 140), w = 26 + (k * 17 % 24);
                    var c = Mix(glow, _b.Moss, (k % 3) / 3f);
                    DrawColoredPolygon(new[] { new Vector2(x - w, 720), new Vector2(x - w * 0.3f, 720 - h), new Vector2(x + w * 0.5f, 720 - h * 0.85f), new Vector2(x + w, 720) }, new Color(c.R, c.G, c.B, 0.45f * a));
                }
                break;
            case BiomeId.Frost:
                for (int k = 0; k < 9; k++)
                {
                    float x = 90 + k * 140, h = 70 + (k * 41 % 110);
                    DrawColoredPolygon(new[] { new Vector2(x - 26, 720), new Vector2(x, 720 - h), new Vector2(x + 26, 720) }, new Color(0.7f, 0.9f, 1f, 0.35f * a));
                }
                break;
            case BiomeId.Fungal:
                for (int k = 0; k < 8; k++)
                {
                    float x = 100 + k * 150 + (k * 37 % 40), h = 60 + (k * 59 % 120);
                    DrawRect(new Rect2(x - 5, 720 - h, 10, h), new Color(0.82f, 0.78f, 0.7f, 0.5f * a));
                    DrawColoredPolygon(CapPoly(new Vector2(x, 720 - h), 38 + (k % 3) * 8, 22), new Color(_b.Moss.R, _b.Moss.G, _b.Moss.B, 0.7f * a));
                    DrawGlow(new Vector2(x, 720 - h), 70, glow, 0.14f * a);
                }
                break;
            case BiomeId.Fossils:
                // a leviathan's ribs arching over the dark
                for (int k = 0; k < 6; k++)
                {
                    float x = 200 + k * 160;
                    var pts = new List<Vector2>();
                    for (int j = 0; j <= 14; j++) { float t = j / 14f; pts.Add(new Vector2(x + 90 * MathF.Sin(t * 2.4f) , 720 - t * 560)); }
                    for (int j = 0; j < pts.Count - 1; j++) DrawLine(pts[j], pts[j + 1], new Color(glow.R, glow.G, glow.B, 0.28f * a), 12 * (1f - j / 16f));
                }
                break;
            case BiomeId.Roots:
                for (int k = 0; k < 16; k++)
                {
                    float x = 40 + k * 80 + (k * 29 % 30);
                    float len = 140 + (k * 61 % 260);
                    var prev = new Vector2(x, 0);
                    for (int j = 1; j <= 10; j++)
                    {
                        var p = new Vector2(x + MathF.Sin(_t * 0.8f + k + j * 0.5f) * 8 * j / 10f, len * j / 10f);
                        DrawLine(prev, p, new Color(0.26f, 0.2f, 0.12f, 0.8f * a), 6 - j * 0.45f);
                        prev = p;
                    }
                }
                break;
            case BiomeId.Nest:
                for (int corner = 0; corner < 2; corner++)
                {
                    float sx = corner == 0 ? 0 : 1280, dirx = corner == 0 ? 1 : -1;
                    for (int k = 0; k < 7; k++)
                    {
                        float ang = k / 6f * 1.45f;
                        DrawLine(new Vector2(sx, 0), new Vector2(sx + dirx * MathF.Cos(ang) * 420, MathF.Sin(ang) * 420), new Color(0.85f, 0.88f, 0.8f, 0.22f * a), 1.2f);
                    }
                    for (int r = 1; r <= 5; r++)
                        for (int k = 0; k < 6; k++)
                        {
                            float a0 = k / 6f * 1.45f, a1 = (k + 1) / 6f * 1.45f, rr = r * 80;
                            DrawLine(new Vector2(sx + dirx * MathF.Cos(a0) * rr, MathF.Sin(a0) * rr), new Vector2(sx + dirx * MathF.Cos(a1) * rr * 0.97f, MathF.Sin(a1) * rr * 0.97f), new Color(0.85f, 0.88f, 0.8f, 0.2f * a), 1f);
                        }
                }
                break;
            case BiomeId.Tunnels:
                // a bore running away: rings shrinking to a point
                for (int k = 0; k < 12; k++)
                {
                    float f = 1f - k / 12f;
                    float r = 40 + f * f * 620;
                    DrawArc(new Vector2(640 + (1 - f) * 90, 380), r, 0, Mathf.Tau, 48, new Color(rim.R, rim.G, rim.B, (0.08f + 0.3f * (1 - f)) * a), 3f);
                }
                break;
            case BiomeId.Slime:
                // a pool, and rings widening on it
                DrawRect(new Rect2(0, 600, 1280, 120), new Color(0.1f, 0.35f, 0.5f, 0.45f * a));
                for (int k = 0; k < 4; k++)
                {
                    float t = (_t * 0.6f + k * 0.25f) % 1f;
                    DrawArc(new Vector2(260 + k * 250, 640), 10 + t * 90, 0, Mathf.Tau, 32, new Color(0.7f, 0.92f, 1f, (1f - t) * 0.5f * a), 2f);
                }
                break;
            case BiomeId.Entrance:
                // the daylight left behind, a bright cleft at the top
                DrawColoredPolygon(new[] { new Vector2(540, -10), new Vector2(740, -10), new Vector2(680, 230), new Vector2(600, 230) }, new Color(1f, 0.95f, 0.75f, 0.35f * a));
                DrawGlow(new Vector2(640, 40), 300, new Color(1f, 0.92f, 0.7f), 0.22f * a);
                break;
            case BiomeId.Den:
                // bones, heaped
                for (int k = 0; k < 18; k++)
                {
                    float x = 60 + k * 70 + (k * 31 % 30), y = 690 - (k * 17 % 40);
                    float ang = (k * 53 % 180) / 180f * Mathf.Pi;
                    var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 34;
                    DrawLine(new Vector2(x, y) - d, new Vector2(x, y) + d, new Color(0.75f, 0.7f, 0.6f, 0.35f * a), 7);
                }
                break;
        }
    }

    private static Vector2[] CapPoly(Vector2 c, float w, float h)
    {
        var pts = new Vector2[12];
        for (int k = 0; k < 12; k++) { float t = Mathf.Pi + k / 11f * Mathf.Pi; pts[k] = c + new Vector2(MathF.Cos(t) * w, MathF.Sin(t) * h); }
        return pts;
    }

    /// <summary>The catacombs: a row of burial niches in a blue gloom, a skull in each, and a heap of skulls before them.</summary>
    private void Catacombs(float a)
    {
        var rim = _b.Rim; var glow = _b.Glow;
        for (int row = 0; row < 3; row++)
            for (int k = 0; k < 11; k++)
            {
                float x = 70 + k * 112 + (row % 2) * 40, y = 120 + row * 150;
                // the niche's arch
                var arch = new List<Vector2> { new Vector2(x - 30, y + 70) };
                for (int j = 0; j <= 10; j++) { float t = Mathf.Pi + j / 10f * Mathf.Pi; arch.Add(new Vector2(x + MathF.Cos(t) * 30, y + MathF.Sin(t) * 40)); }
                arch.Add(new Vector2(x + 30, y + 70));
                DrawColoredPolygon(arch.ToArray(), new Color(0f, 0f, 0.02f, 0.55f * a));
                DrawPolyline(arch.ToArray(), new Color(rim.R, rim.G, rim.B, 0.25f * a), 2f);
                if ((k + row * 3) % 3 != 0) DrawSkull(new Vector2(x, y + 54), 13, new Color(0.62f, 0.68f, 0.82f, 0.5f * a));
            }
        DrawGlow(new Vector2(640, 720), 380, glow, 0.2f * a);
        // the heap
        for (int k = 0; k < 34; k++)
        {
            float t = k / 33f;
            float x = 640 + (t - 0.5f) * 560 + (k * 37 % 21) - 10;
            float h = 96 * (1f - MathF.Abs(2 * t - 1) * MathF.Abs(2 * t - 1));
            float y = 722 - h * ((k * 53 % 100) / 100f) - 4;
            DrawSkull(new Vector2(x, y), 15 + (k % 4) * 2, new Color(0.7f, 0.76f, 0.9f, 0.62f * a), k % 2 == 0 ? 0.2f : -0.25f);
        }
        // torches
        foreach (float tx in new[] { 150f, 1130f })
        {
            DrawRect(new Rect2(tx - 3, 430, 6, 40), new Color(0.2f, 0.15f, 0.1f, a));
            DrawGlow(new Vector2(tx, 424), 110, new Color(1f, 0.6f, 0.25f), 0.45f * a);
            float fl = MathF.Sin(_t * 9 + tx) * 2f;
            DrawColoredPolygon(new[] { new Vector2(tx - 8, 430), new Vector2(tx + fl, 396), new Vector2(tx + 8, 430) }, new Color(1f, 0.65f, 0.2f, 0.95f * a));
        }
    }

    private void DrawSkull(Vector2 at, float r, Color c, float tilt = 0f)
    {
        var dark = new Color(0.02f, 0.03f, 0.08f, c.A);
        DrawCircle(at, r, c);
        DrawRect(new Rect2(at + new Vector2(-r * 0.55f, r * 0.45f), new Vector2(r * 1.1f, r * 0.6f)), c);
        DrawCircle(at + new Vector2(-r * 0.38f, -r * 0.05f), r * 0.27f, dark);
        DrawCircle(at + new Vector2(r * 0.38f, -r * 0.05f), r * 0.27f, dark);
        DrawColoredPolygon(new[] { at + new Vector2(0, r * 0.2f), at + new Vector2(-r * 0.12f, r * 0.45f), at + new Vector2(r * 0.12f, r * 0.45f) }, dark);
        for (int k = -1; k <= 1; k++) DrawLine(at + new Vector2(k * r * 0.3f, r * 0.7f), at + new Vector2(k * r * 0.3f, r * 1.0f), dark, 1.2f);
    }

    /// <summary>The lava tubes: a great round bore, ringed in glowing rock, a river of fire down its floor, its far end a white-hot eye.</summary>
    private void LavaTube(float a)
    {
        var glow = _b.Glow;
        var c = new Vector2(640, 400);
        for (int k = 0; k < 12; k++)
        {
            float f = 1f - k / 12f;
            float r = 30 + f * f * 640;
            var col = Mix(new Color(1f, 0.85f, 0.45f), new Color(0.35f, 0.08f, 0.04f), 1f - f);
            DrawArc(c, r, 0, Mathf.Tau, 56, new Color(col.R, col.G, col.B, (0.1f + 0.5f * f) * a * (0.7f + 0.3f * MathF.Sin(_t * 2f + k))), 4f + 6f * (1f - f));
        }
        DrawGlow(c, 170, new Color(1f, 0.85f, 0.5f), 0.7f * a);
        // the river of fire along the floor, in perspective
        for (int k = 0; k < 16; k++)
        {
            float t = k / 15f;
            float y = 440 + t * t * 300, w = 20 + t * t * 520;
            DrawRect(new Rect2(c.X - w * 0.5f, y, w, 10 + t * 30), new Color(1f, 0.38f + 0.25f * (1 - t), 0.08f, (0.25f + 0.35f * t) * a));
        }
    }
}
