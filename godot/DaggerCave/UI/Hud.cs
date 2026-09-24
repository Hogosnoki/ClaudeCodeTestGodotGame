using System;
using Godot;

namespace DaggerCave;

/// <summary>In-game heads-up display: health, XP, breath, ability charges, boss bar, minimap.</summary>
public partial class Hud : Control
{
    private Image _mapImg;
    private ImageTexture _mapTex;
    private bool[] _revealed;
    private float _revealT, _t;
    public float HintTime = 14f;
    public string Banner = "";
    public float BannerT;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ResetMap(CaveData cave)
    {
        _mapImg = Image.CreateEmpty(cave.W, cave.H, false, Image.Format.Rgba8);
        _mapImg.Fill(new Color(0, 0, 0, 0));
        _mapTex = ImageTexture.CreateFromImage(_mapImg);
        _revealed = new bool[cave.W * cave.H];
    }

    public void ShowBanner(string text, float time = 2.5f) { Banner = text; BannerT = time; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        BannerT -= dt;
        if (!GetTree().Paused) HintTime -= dt;
        _revealT -= dt;
        if (_revealT <= 0 && G.Player != null && G.Cave != null && _mapImg != null) { _revealT = 0.2f; Reveal(); }
        QueueRedraw();
    }

    private void Reveal()
    {
        var cave = G.Cave;
        var pc = G.Player.GlobalPosition / CaveData.Cell;
        int r = 17;
        bool changed = false;
        int ci = (int)pc.X, cj = (int)pc.Y;
        for (int j = cj - r; j <= cj + r; j++)
            for (int i = ci - r; i <= ci + r; i++)
            {
                if (i < 0 || j < 0 || i >= cave.W || j >= cave.H) continue;
                if ((i - ci) * (i - ci) + (j - cj) * (j - cj) > r * r) continue;
                int k = j * cave.W + i;
                if (_revealed[k]) continue;
                _revealed[k] = true; changed = true;
                bool open = cave.CellOpen(i, j);
                Color c;
                if (open) c = (j + 0.5f) * CaveData.Cell > cave.WaterY ? new Color(0.2f, 0.45f, 0.75f, 0.85f) : new Color(0.62f, 0.58f, 0.52f, 0.85f);
                else
                {
                    bool edge = cave.CellOpen(i + 1, j) || cave.CellOpen(i - 1, j) || cave.CellOpen(i, j + 1) || cave.CellOpen(i, j - 1);
                    c = edge ? new Color(0.2f, 0.17f, 0.16f, 0.9f) : new Color(0, 0, 0, 0);
                }
                _mapImg.SetPixel(i, j, c);
            }
        if (changed) _mapTex.Update(_mapImg);
    }

    public override void _Draw()
    {
        var p = G.Player;
        if (p == null || G.Cave == null) return;
        var font = ThemeDB.FallbackFont;
        var vs = GetViewportRect().Size;

        // --- HP ---
        var hpPos = new Vector2(20, 18);
        float hpW = 200 + p.Stats.MaxHp * 0.3f;
        DrawRect(new Rect2(hpPos - new Vector2(3, 3), new Vector2(hpW + 6, 22)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(hpPos, new Vector2(hpW, 16)), new Color(0.25f, 0.05f, 0.06f));
        float hf = Math.Clamp(p.Hp / p.Stats.MaxHp, 0, 1);
        DrawRect(new Rect2(hpPos, new Vector2(hpW * hf, 16)), hf < 0.3f && (int)(_t * 4) % 2 == 0 ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.18f, 0.22f));
        DrawRect(new Rect2(hpPos, new Vector2(hpW * hf, 5)), new Color(1, 1, 1, 0.15f));
        DrawString(font, hpPos + new Vector2(6, 13), $"{Mathf.CeilToInt(p.Hp)} / {Mathf.RoundToInt(p.Stats.MaxHp)}", HorizontalAlignment.Left, -1, 13, Colors.White);

        // --- XP ---
        var xpPos = hpPos + new Vector2(0, 26);
        float xpW = hpW;
        DrawRect(new Rect2(xpPos - new Vector2(3, 3), new Vector2(xpW + 6, 12)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(xpPos, new Vector2(xpW * Math.Clamp(p.Xp / (float)p.XpToNext, 0, 1), 6)), new Color(0.4f, 1f, 0.7f));
        DrawString(font, xpPos + new Vector2(xpW + 10, 8), $"Lv {p.Level}", HorizontalAlignment.Left, -1, 14, new Color(0.6f, 1f, 0.8f));

        // --- Breath ---
        if (p.Breath < p.Stats.BreathMax - 0.05f || p.HeadUnder)
        {
            var bp = xpPos + new Vector2(0, 16);
            int bubbles = Mathf.CeilToInt(p.Stats.BreathMax / 2f);
            float per = p.Stats.BreathMax / bubbles;
            for (int k = 0; k < bubbles; k++)
            {
                float fill = Math.Clamp((p.Breath - k * per) / per, 0, 1);
                var c = bp + new Vector2(8 + k * 18, 8);
                DrawArc(c, 7, 0, Mathf.Tau, 16, new Color(0.7f, 0.9f, 1f, 0.9f), 1.5f);
                if (fill > 0) DrawCircle(c, 6 * fill, new Color(0.5f, 0.8f, 1f, 0.7f));
            }
            if (p.Breath <= 0) DrawString(font, bp + new Vector2(8 + bubbles * 18, 13), "DROWNING!", HorizontalAlignment.Left, -1, 13, new Color(1f, 0.4f, 0.4f));
        }

        // --- Ability charges (bottom-left) ---
        var ab = new Vector2(24, vs.Y - 44);
        var tc = p.ThrowCooldowns;
        for (int k = 0; k < tc.Length; k++)
        {
            var c = ab + new Vector2(k * 40, 0);
            DrawRect(new Rect2(c, new Vector2(34, 34)), new Color(0, 0, 0, 0.55f));
            float f = Math.Clamp(tc[k] / p.Stats.ThrowCooldown, 0, 1);
            DrawDaggerIcon(c + new Vector2(17, 17), f <= 0 ? Colors.White : new Color(0.5f, 0.5f, 0.55f));
            if (f > 0) DrawRect(new Rect2(c + new Vector2(0, 34 * (1 - f)), new Vector2(34, 34 * f)), new Color(0, 0, 0, 0.55f));
        }
        DrawString(font, ab + new Vector2(0, -6), "THROW", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        var db = ab + new Vector2(tc.Length * 40 + 16, 0);
        var dc = p.DodgeCooldowns;
        for (int k = 0; k < dc.Length; k++)
        {
            var c = db + new Vector2(k * 40 + 17, 17);
            float cd = 0.95f * p.Stats.DodgeCdMult;
            float f = Math.Clamp(dc[k] / cd, 0, 1);
            DrawCircle(c, 17, new Color(0, 0, 0, 0.55f));
            DrawArc(c, 12, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (1 - f), 24, f <= 0 ? new Color(0.5f, 0.9f, 1f) : new Color(0.4f, 0.5f, 0.6f), 4f);
        }
        DrawString(font, db + new Vector2(0, -6), "DODGE", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));

        // --- Depth / kills ---
        DrawString(font, new Vector2(vs.X / 2 - 60, 22), $"DEPTH {G.Depth}   ·   {p.Kills} kills", HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.75f));

        // --- Boss bar ---
        var boss = G.Main.ActiveBoss;
        if (boss != null && !boss.Dead)
        {
            float bw = Math.Min(560, vs.X - 360);
            var bp = new Vector2(vs.X / 2 - bw / 2, vs.Y - 46);
            DrawString(font, bp + new Vector2(0, -6), boss.DisplayName.ToUpperInvariant(), HorizontalAlignment.Left, -1, 15, new Color(1f, 0.85f, 0.7f));
            DrawRect(new Rect2(bp - new Vector2(3, 3), new Vector2(bw + 6, 20)), new Color(0, 0, 0, 0.7f));
            DrawRect(new Rect2(bp, new Vector2(bw * Math.Clamp(boss.Hp / boss.MaxHp, 0, 1), 14)), new Color(0.8f, 0.25f, 0.15f));
        }

        // --- Minimap ---
        if (_mapTex != null)
        {
            float mw = 200, mh = mw * G.Cave.H / G.Cave.W;
            var mp = new Vector2(vs.X - mw - 16, 16);
            DrawRect(new Rect2(mp - new Vector2(4, 4), new Vector2(mw + 8, mh + 8)), new Color(0, 0, 0, 0.55f));
            DrawTextureRect(_mapTex, new Rect2(mp, new Vector2(mw, mh)), false);
            float sx = mw / G.Cave.SizePx.X, sy = mh / G.Cave.SizePx.Y;
            DrawLine(mp + new Vector2(0, G.Cave.WaterY * sy), mp + new Vector2(mw, G.Cave.WaterY * sy), new Color(0.4f, 0.7f, 1f, 0.35f), 1f);
            var pp = mp + new Vector2(p.GlobalPosition.X * sx, p.GlobalPosition.Y * sy);
            DrawCircle(pp, 3, new Color(1f, 0.95f, 0.4f));
            if (G.Cave.Boss != null)
            {
                var b = mp + new Vector2(G.Cave.Boss.Center.X * sx, G.Cave.Boss.Center.Y * sy);
                DrawArc(b, 4 + MathF.Sin(_t * 4), 0, Mathf.Tau, 12, new Color(1f, 0.25f, 0.2f), 2f);
                DrawString(font, b + new Vector2(6, 4), "BOSS", HorizontalAlignment.Left, -1, 9, new Color(1f, 0.4f, 0.35f));
            }
        }

        // --- Hints / banner ---
        if (HintTime > 0)
        {
            float a = Math.Clamp(HintTime / 2f, 0, 1) * 0.85f;
            string[] lines = G.Main.UsingPad
                ? new[]
                {
                    "Left stick move · A jump · hold up/down to swim",
                    "X swing (aim with right stick) · RB/RT throw dagger",
                    "B/LB dodge · START pause    —    find and slay the boss",
                }
                : new[]
                {
                    "A/D move · SPACE jump · W/S swim up/down",
                    "LEFT CLICK swing (aim with mouse) · RIGHT CLICK throw dagger",
                    "SHIFT dodge · ESC pause    —    find and slay the boss",
                };
            for (int k = 0; k < lines.Length; k++)
                DrawString(font, new Vector2(vs.X / 2 - 230, vs.Y - 110 + k * 20), lines[k], HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, a));
        }
        if (BannerT > 0)
        {
            float a = Math.Clamp(BannerT, 0, 1);
            int size = 30;
            var sz = font.GetStringSize(Banner, HorizontalAlignment.Left, -1, size);
            var pos = new Vector2(vs.X / 2 - sz.X / 2, vs.Y * 0.3f);
            DrawString(font, pos + new Vector2(2, 2), Banner, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, a * 0.8f));
            DrawString(font, pos, Banner, HorizontalAlignment.Left, -1, size, new Color(1f, 0.9f, 0.7f, a));
        }
    }

    private void DrawDaggerIcon(Vector2 c, Color col)
    {
        var d = new Vector2(1, -1).Normalized();
        var perp = new Vector2(-d.Y, d.X);
        DrawLine(c - d * 11, c - d * 5, new Color(0.55f, 0.35f, 0.2f), 3f);
        DrawLine(c - d * 5 + perp * 4, c - d * 5 - perp * 4, new Color(0.8f, 0.7f, 0.3f), 2f);
        DrawColoredPolygon(new[] { c - d * 4 + perp * 2.2f, c + d * 12, c - d * 4 - perp * 2.2f }, col);
    }
}

/// <summary>Darkness vignette centered on the player (a torch-lit feel without real lights).</summary>
public partial class DarknessOverlay : ColorRect
{
    private ShaderMaterial _mat;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Color = new Color(0, 0, 0, 0);
        _mat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type canvas_item;
uniform vec2 center = vec2(0.5);
uniform float aspect = 1.777;
uniform float radius = 0.62;
uniform float strength = 0.82;
uniform float water = 0.0;
void fragment() {
    vec2 d = SCREEN_UV - center;
    d.x *= aspect;
    float dist = length(d);
    float a = smoothstep(radius * 0.3, radius, dist) * strength;
    vec3 tint = mix(vec3(0.0, 0.0, 0.02), vec3(0.0, 0.04, 0.1), water);
    COLOR = vec4(tint, clamp(a + water * 0.12, 0.0, 1.0));
}"
            }
        };
        Material = _mat;
    }

    public override void _Process(double delta)
    {
        var p = G.Player;
        if (p == null || !IsInstanceValid(p)) return;
        var vp = GetViewport();
        var size = vp.GetVisibleRect().Size;
        var sp = p.GetGlobalTransformWithCanvas().Origin;
        _mat.SetShaderParameter("center", sp / size);
        _mat.SetShaderParameter("aspect", size.X / size.Y);
        float w = p.HeadUnder ? 1f : 0f;
        _mat.SetShaderParameter("water", w);
        _mat.SetShaderParameter("radius", p.HeadUnder ? 0.5f : 0.62f);
    }
}
