using System.Collections.Generic;
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
                if (open) c = (j + 0.5f) * CaveData.Cell > cave.WaterY ? (cave.Liquid == Liquid.Lava ? new Color(0.9f, 0.35f, 0.1f, 0.85f) : new Color(0.2f, 0.45f, 0.75f, 0.85f)) : new Color(0.62f, 0.58f, 0.52f, 0.85f);
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

        // --- Ability gauges (bottom-left) ---
        var ab = new Vector2(24, vs.Y - 44);
        switch (p.Stats.Hero)
        {
            case HeroKind.Warden: DrawWardenGauges(font, p, ab); break;
            case HeroKind.Vitalist: DrawVitalistGauges(font, p, ab); break;
            default: DrawSwordsmanGauges(font, p, ab); break;
        }

        // --- Potions (under the bars) ---
        {
            var pp = xpPos + new Vector2(xpW + 60, -18);
            for (int k = 0; k < Meta.MaxPotions; k++)
                PotionPickup.DrawFlask(this, pp + new Vector2(k * 18, 6), 1.35f, 1f, k >= p.Potions);
            DrawString(font, pp + new Vector2(-8, 28), G.Main.UsingPad ? "Y" : "Q", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.5f));
            if (p.Mending) DrawString(font, pp + new Vector2(6, 28), "mending", HorizontalAlignment.Left, -1, 10, new Color(1f, 0.6f, 0.7f, 0.7f + 0.3f * MathF.Sin(_t * 6)));
        }

        // --- Depth / biome / kills ---
        string where = $"DEPTH {G.Depth}  ·  {G.Biome?.Name.ToUpperInvariant() ?? ""}  ·  {p.Kills} kills";
        var wsz = font.GetStringSize(where, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2(vs.X / 2 - wsz.X / 2, 22), where, HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.75f));
        if (G.Main.SkipBank > 0)
            DrawString(font, new Vector2(vs.X / 2 - 90, 40), $"{G.Main.SkipBank} ember{(G.Main.SkipBank > 1 ? "s" : "")} riding on the guardian", HorizontalAlignment.Center, 180, 11, new Color(1f, 0.7f, 0.4f, 0.7f));

        // --- Boss bar ---
        var boss = G.Main.ActiveBoss;
        if (boss != null && IsInstanceValid(boss) && !boss.Dead)
        {
            float bw = Math.Min(560, vs.X - 360);
            var bp = new Vector2(vs.X / 2 - bw / 2, vs.Y - 46);
            DrawString(font, bp + new Vector2(0, -6), (boss.Title != "" ? boss.Title : boss.DisplayName).ToUpperInvariant(), HorizontalAlignment.Left, -1, 15, new Color(1f, 0.85f, 0.7f));
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
            if (G.Cave.Liquid != Liquid.None)
                DrawLine(mp + new Vector2(0, G.Cave.WaterY * sy), mp + new Vector2(mw, G.Cave.WaterY * sy), G.Cave.Liquid == Liquid.Lava ? new Color(1f, 0.5f, 0.2f, 0.4f) : new Color(0.4f, 0.7f, 1f, 0.35f), 1f);
            var pp = mp + new Vector2(p.GlobalPosition.X * sx, p.GlobalPosition.Y * sy);
            DrawCircle(pp, 3, new Color(1f, 0.95f, 0.4f));
            if (G.Cave.Boss != null)
            {
                var b = mp + new Vector2(G.Cave.Boss.Center.X * sx, G.Cave.Boss.Center.Y * sy);
                DrawArc(b, 4 + MathF.Sin(_t * 4), 0, Mathf.Tau, 12, new Color(1f, 0.25f, 0.2f), 2f);
                DrawString(font, b + new Vector2(6, 4), "EXIT", HorizontalAlignment.Left, -1, 9, new Color(1f, 0.4f, 0.35f));
            }
        }

        // --- Brain training panel (F9) ---
        if (Brains.Training) DrawBrainPanel(font, vs);

        // --- Hints / banner ---
        if (HintTime > 0)
        {
            float a = Math.Clamp(HintTime / 2f, 0, 1) * 0.85f;
            bool pad = G.Main.UsingPad;
            string move = pad ? "Left stick move · A jump · hold up/down to swim · Y drink a potion" : "A/D move · SPACE jump · W/S swim up/down · Q drink a potion";
            string[] lines = p.Stats.Hero switch
            {
                HeroKind.Warden => new[]
                {
                    move,
                    pad ? "X swing · RB/RT shield dash (breaks off attacks it meets) · B/LB or the right stick raise the shield"
                        : "LEFT CLICK swing · RIGHT CLICK shield dash (breaks off attacks it meets) · hold SHIFT to raise the shield",
                    "The shield stops 70% of a blow; raise it just before the hit for a perfect block · " + (pad ? "START pause" : "ESC pause"),
                },
                HeroKind.Vitalist => new[]
                {
                    move,
                    pad ? "X drain bolt (aim with the right stick) · B/LB hex · RB/RT heal (costs alimus)"
                        : "LEFT CLICK drain bolt (aim with the mouse) · SHIFT hex · RIGHT CLICK heal (costs alimus)",
                    "Your bolts' damage feeds your alimus · " + (pad ? "START pause" : "ESC pause") + "    —    find and slay the exit's guardian",
                },
                _ => new[]
                {
                    move,
                    pad ? "X swing (aim with the right stick) · RB/RT charge your blade · B/LB dodge (swing out of it)"
                        : "LEFT CLICK swing (aim with the mouse) · RIGHT CLICK charge your blade · SHIFT dodge (swing out of it)",
                    "A charged swing hits harder and saps what it cuts · " + (pad ? "START pause" : "ESC pause") + "    —    find and slay the exit's guardian",
                },
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

    private void DrawBrainPanel(Font font, Vector2 vs)
    {
        var brains = new List<Brain>(Brains.Loaded);
        brains.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        float w = 330, rowH = 15, h = 44 + Math.Max(1, brains.Count) * rowH;
        var o = new Vector2(vs.X - w - 12, 150);
        DrawRect(new Rect2(o, new Vector2(w, h)), new Color(0, 0, 0, 0.6f));
        DrawString(font, o + new Vector2(8, 16), $"TRAINING  (F9 off · F10 save)   {Brains.LastSaveText}", HorizontalAlignment.Left, w - 16, 11, new Color(1f, 0.85f, 0.35f));
        var dim = new Color(1, 1, 1, 0.55f);
        string[] heads = { "brain", "decisions", "teacher", "reward", "dealt" };
        float[] cols = { 8, 104, 170, 222, 282 };
        for (int c = 0; c < heads.Length; c++) DrawString(font, o + new Vector2(cols[c], 32), heads[c], HorizontalAlignment.Left, -1, 10, dim);
        if (brains.Count == 0) DrawString(font, o + new Vector2(8, 46), "(no creatures met yet)", HorizontalAlignment.Left, -1, 10, dim);
        for (int k = 0; k < brains.Count; k++)
        {
            var b = brains[k];
            bool locked = Brains.IsLocked(b.Name);
            var y = o.Y + 46 + k * rowH;
            var col = locked ? new Color(0.6f, 0.8f, 1f) : Colors.White;
            DrawString(font, new Vector2(o.X + 8, y), b.Name + (locked ? " (locked)" : ""), HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 104, y), b.Experience.ToString(), HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 170, y), $"{Math.Min(0.9f, b.TeacherWeight) * 100:0}%", HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 222, y), $"{b.AvgReward:+0.000;-0.000}", HorizontalAlignment.Left, -1, 10, b.AvgReward >= 0 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f));
            DrawString(font, new Vector2(o.X + 282, y), $"{b.AvgDealt:0.00}", HorizontalAlignment.Left, -1, 10, col);
        }
    }

    /// <summary>A square ability icon with its cooldown filling it from the top, and a label.</summary>
    private void AbilitySquare(Font font, Vector2 at, string label, float cooldownFrac, bool ready, Color col, Action<Vector2, Color> icon)
    {
        DrawRect(new Rect2(at, new Vector2(34, 34)), new Color(0, 0, 0, 0.55f));
        icon(at + new Vector2(17, 17), ready ? col : new Color(0.45f, 0.47f, 0.52f));
        if (cooldownFrac > 0) DrawRect(new Rect2(at + new Vector2(0, 34 * (1 - cooldownFrac)), new Vector2(34, 34 * cooldownFrac)), new Color(0, 0, 0, 0.55f));
        if (ready) DrawRect(new Rect2(at - new Vector2(1, 1), new Vector2(36, 36)), new Color(col, 0.35f + 0.25f * MathF.Sin(_t * 5)), false, 1.5f);
        DrawString(font, at + new Vector2(0, -6), label, HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
    }

    /// <summary>A round cooldown dial (dodges, the hex).</summary>
    private void Dial(Vector2 c, float frac, Color readyCol)
    {
        DrawCircle(c, 17, new Color(0, 0, 0, 0.55f));
        DrawArc(c, 12, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (1 - frac), 24, frac <= 0 ? readyCol : new Color(0.4f, 0.5f, 0.6f), 4f);
    }

    /// <summary>Dodge charges and the Charged Strike.</summary>
    private void DrawSwordsmanGauges(Font font, Player p, Vector2 ab)
    {
        var dc = p.DodgeCooldowns;
        for (int k = 0; k < dc.Length; k++) Dial(ab + new Vector2(k * 40 + 17, 17), Math.Clamp(dc[k] / p.DodgeCooldownTotal, 0, 1), new Color(0.5f, 0.9f, 1f));
        DrawString(font, ab + new Vector2(0, -6), "DODGE", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        var cb = ab + new Vector2(dc.Length * 40 + 16, 0);
        bool charged = p.Charged > 0;
        AbilitySquare(font, cb, charged ? "CHARGED" : "CHARGE", charged ? 0 : p.ChargeCooldownFrac, charged || p.ChargeCooldownFrac <= 0, new Color(1f, 0.55f, 0.25f), (c, col) =>
        {
            // a sword with a burning edge
            var d = new Vector2(1, -1).Normalized();
            var perp = new Vector2(-d.Y, d.X);
            DrawLine(c - d * 11, c - d * 5, new Color(0.55f, 0.35f, 0.2f), 3f);
            DrawLine(c - d * 5 + perp * 4, c - d * 5 - perp * 4, new Color(0.8f, 0.7f, 0.3f), 2f);
            DrawColoredPolygon(new[] { c - d * 4 + perp * 2.2f, c + d * 12, c - d * 4 - perp * 2.2f }, col);
            if (charged) DrawCircle(c + d * 3, 8 + MathF.Sin(_t * 9) * 1.5f, new Color(1f, 0.5f, 0.2f, 0.25f));
        });
    }

    /// <summary>The shield dash and the shield's strength bar.</summary>
    private void DrawWardenGauges(Font font, Player p, Vector2 ab)
    {
        AbilitySquare(font, ab, "DASH", p.DashCooldownFrac, p.DashCooldownFrac <= 0, new Color(0.55f, 0.85f, 1f), (c, col) =>
        {
            // a kite shield with speed lines
            DrawColoredPolygon(new[] { c + new Vector2(-4, -9), c + new Vector2(7, -9), c + new Vector2(7, 1), c + new Vector2(1.5f, 10), c + new Vector2(-4, 1) }, col);
            for (int k = 0; k < 3; k++) DrawLine(c + new Vector2(-8, -5 + k * 5), c + new Vector2(-14, -5 + k * 5), new Color(col, 0.7f), 1.5f);
        });

        // shield
        var sp = ab + new Vector2(52, 10);
        const float w = 130;
        float frac = Math.Clamp(p.ShieldHp / Math.Max(1f, p.Stats.ShieldMax), 0, 1);
        DrawRect(new Rect2(sp - new Vector2(2, 2), new Vector2(w + 4, 16)), new Color(0, 0, 0, 0.6f));
        var bar = p.ShieldBroken ? new Color(0.45f, 0.45f, 0.5f) : p.ShieldRaised ? new Color(0.6f, 0.85f, 1f) : new Color(0.35f, 0.6f, 0.95f);
        DrawRect(new Rect2(sp, new Vector2(w * frac, 12)), bar);
        string label = p.ShieldBroken ? $"SHIELD BROKEN  {p.ShieldBrokenLeft:0.0}s" : $"SHIELD  {Mathf.CeilToInt(p.ShieldHp)} / {Mathf.RoundToInt(p.Stats.ShieldMax)}   blocks {p.Stats.BlockShare * 100:0}%";
        DrawString(font, sp + new Vector2(0, -8), label, HorizontalAlignment.Left, -1, 10, p.ShieldBroken ? new Color(1f, 0.6f, 0.5f) : new Color(1, 1, 1, 0.6f));
    }

    /// <summary>Alimus, the hex and the heal.</summary>
    private void DrawVitalistGauges(Font font, Player p, Vector2 ab)
    {
        var green = new Color(0.55f, 1f, 0.45f);
        Dial(ab + new Vector2(17, 17), p.HexCooldownFrac, green);
        DrawString(font, ab + new Vector2(0, -6), "HEX", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        var hb = ab + new Vector2(56, 0);
        bool affordable = p.Alimus >= p.HealCost;
        AbilitySquare(font, hb, $"HEAL ({p.HealCost:0})", p.HealCooldownFrac, affordable && p.HealCooldownFrac <= 0, new Color(0.5f, 1f, 0.55f), (c, col) =>
        {
            DrawRect(new Rect2(c - new Vector2(3, 10), new Vector2(6, 20)), col);
            DrawRect(new Rect2(c - new Vector2(10, 3), new Vector2(20, 6)), col);
        });
        // the alimus reserve
        var bp = hb + new Vector2(52, 10);
        const float w = 150;
        float frac = Math.Clamp(p.Alimus / Math.Max(1f, p.Stats.AlimusMax), 0, 1);
        DrawRect(new Rect2(bp - new Vector2(2, 2), new Vector2(w + 4, 16)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bp, new Vector2(w * frac, 12)), new Color(0.42f, 0.85f, 0.4f));
        DrawRect(new Rect2(bp, new Vector2(w * frac, 4)), new Color(0.9f, 0.3f, 0.35f, 0.5f));
        // tick marks: one heal's worth each
        for (float x = p.HealCost; x < p.Stats.AlimusMax; x += p.HealCost)
            DrawLine(bp + new Vector2(w * x / p.Stats.AlimusMax, 0), bp + new Vector2(w * x / p.Stats.AlimusMax, 12), new Color(0, 0, 0, 0.45f), 1f);
        DrawString(font, bp + new Vector2(0, -8), $"ALIMUS  {Mathf.FloorToInt(p.Alimus)} / {Mathf.RoundToInt(p.Stats.AlimusMax)}", HorizontalAlignment.Left, -1, 10, affordable ? new Color(0.75f, 1f, 0.7f, 0.8f) : new Color(1, 1, 1, 0.5f));
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
        _mat.SetShaderParameter("strength", p.HeadUnder ? Math.Max(0.6f, G.Biome?.Darkness ?? 0.82f) : G.Biome?.Darkness ?? 0.82f);
    }
}
