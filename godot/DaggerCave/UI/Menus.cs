using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Pick-one-of-three upgrade cards shown on level up or when opening a chest. Pauses the game.</summary>
public partial class UpgradeMenu : Control
{
    public event Action<Upgrade> Picked;
    private List<Upgrade> _choices = new();
    private readonly List<Rect2> _cards = new();
    private int _hover = -1;
    private string _title = "";
    private float _t;
    private float _openedAt;

    public static Color CategoryColor(string cat) => cat switch
    {
        "blade" => new Color(0.95f, 0.75f, 0.35f),
        "throw" => new Color(0.55f, 0.85f, 1f),
        "move" => new Color(0.5f, 1f, 0.6f),
        "dodge" => new Color(0.75f, 0.6f, 1f),
        _ => new Color(1f, 0.45f, 0.5f),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open(List<Upgrade> choices, string title)
    {
        _choices = choices;
        _title = title;
        _hover = G.Main.UsingPad ? 0 : -1;
        _openedAt = _t;
        Visible = true;
        QueueRedraw();
    }

    public void Choose(int i)
    {
        if (!Visible || i < 0 || i >= _choices.Count || _t - _openedAt < 0.25f) return;
        Visible = false;
        Picked?.Invoke(_choices[i]);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (!Visible) return;
        if (Input.IsActionJustPressed("pick_1")) Choose(0);
        else if (Input.IsActionJustPressed("pick_2")) Choose(1);
        else if (Input.IsActionJustPressed("pick_3")) Choose(2);
        // controller / arrow-key navigation
        int n = _choices.Count;
        if (n > 0)
        {
            if (Input.IsActionJustPressed("move_left")) { _hover = _hover < 0 ? 0 : (_hover + n - 1) % n; G.Sfx.Play("ui", null, -8); }
            if (Input.IsActionJustPressed("move_right")) { _hover = _hover < 0 ? 0 : (_hover + 1) % n; G.Sfx.Play("ui", null, -8); }
            if (Input.IsActionJustPressed("confirm") && _hover >= 0) Choose(_hover);
        }
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            _hover = -1;
            for (int k = 0; k < _cards.Count; k++) if (_cards[k].HasPoint(mm.Position)) _hover = k;
        }
        else if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            for (int k = 0; k < _cards.Count; k++) if (_cards[k].HasPoint(mb.Position)) { Choose(k); AcceptEvent(); }
        }
    }

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, 0.6f));
        var tsz = font.GetStringSize(_title, HorizontalAlignment.Left, -1, 34);
        float pop = 1 + 0.05f * MathF.Sin(_t * 5);
        DrawString(font, new Vector2(vs.X / 2 - tsz.X * pop / 2, vs.Y * 0.2f), _title, HorizontalAlignment.Left, -1, (int)(34 * pop), new Color(1f, 0.9f, 0.5f));
        string sub = G.Main.UsingPad ? "Choose one  (left / right to browse, A to take)" : "Choose one  (click, 1 / 2 / 3, or arrows + ENTER)";
        var ssz = font.GetStringSize(sub, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2(vs.X / 2 - ssz.X / 2, vs.Y * 0.2f + 28), sub, HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.7f));

        _cards.Clear();
        int n = _choices.Count;
        float cw = 250, ch = 300, gap = 26;
        float total = n * cw + (n - 1) * gap;
        float x0 = vs.X / 2 - total / 2, y0 = vs.Y * 0.3f;
        var stats = G.Player?.Stats;
        for (int k = 0; k < n; k++)
        {
            var u = _choices[k];
            bool hov = k == _hover;
            var r = new Rect2(x0 + k * (cw + gap), y0 - (hov ? 8 : 0), cw, ch);
            _cards.Add(r);
            var cat = CategoryColor(u.Icon);
            DrawRect(r, new Color(0.08f, 0.07f, 0.1f, 0.95f));
            DrawRect(r, hov ? cat : cat.Darkened(0.4f), false, hov ? 3 : 2);
            DrawRect(new Rect2(r.Position, new Vector2(cw, 6)), cat);
            // gem
            var gc = r.Position + new Vector2(cw / 2, 62);
            float spin = _t * 1.5f + k;
            var gem = new Vector2[6];
            for (int g = 0; g < 6; g++) gem[g] = gc + Vector2.Right.Rotated(spin + g * Mathf.Tau / 6) * new Vector2(26, 26);
            DrawCircle(gc, 36, new Color(cat, 0.12f));
            DrawColoredPolygon(gem, cat.Darkened(0.2f));
            DrawCircle(gc, 12, cat.Lightened(0.3f));
            DrawString(font, r.Position + new Vector2(12, 22), $"[{k + 1}]", HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.5f));
            if (u.Tier == UpgradeTier.Ability)
                DrawString(font, r.Position + new Vector2(cw - 70, 22), "ABILITY", HorizontalAlignment.Left, -1, 11, cat);
            DrawString(font, r.Position + new Vector2(0, 130), u.Name, HorizontalAlignment.Center, cw, 20, Colors.White);
            DrawMultilineString(font, r.Position + new Vector2(16, 162), u.Desc, HorizontalAlignment.Center, cw - 32, 14, -1, new Color(0.85f, 0.85f, 0.9f));
            if (stats != null && u.MaxStacks > 1)
                DrawString(font, r.Position + new Vector2(0, ch - 16), $"{stats.StackOf(u.Id)} / {u.MaxStacks}", HorizontalAlignment.Center, cw, 12, new Color(1, 1, 1, 0.5f));
            if (u.Excludes.Length > 0)
                DrawString(font, r.Position + new Vector2(0, ch - 34), "excludes " + Upgrades.Get(u.Excludes[0]).Name, HorizontalAlignment.Center, cw, 11, new Color(1f, 0.6f, 0.5f, 0.7f));
        }
    }
}

/// <summary>Full-screen text overlays: title, pause, game over.</summary>
public partial class ScreenOverlay : Control
{
    public string Title = "";
    public string[] Lines = Array.Empty<string>();
    public float Dim = 0.65f;
    private float _t;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Show(string title, float dim, params string[] lines)
    {
        Title = title; Lines = lines; Dim = dim; Visible = true; QueueRedraw();
    }

    public override void _Process(double delta) { _t += (float)delta; if (Visible) QueueRedraw(); }

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, Dim));
        int ts = 56;
        var tsz = font.GetStringSize(Title, HorizontalAlignment.Left, -1, ts);
        var tp = new Vector2(vs.X / 2 - tsz.X / 2, vs.Y * 0.32f);
        DrawString(font, tp + new Vector2(3, 3), Title, HorizontalAlignment.Left, -1, ts, new Color(0, 0, 0, 0.8f));
        DrawString(font, tp, Title, HorizontalAlignment.Left, -1, ts, new Color(0.95f, 0.85f, 0.6f));
        for (int k = 0; k < Lines.Length; k++)
        {
            var line = Lines[k];
            bool emph = line.StartsWith("!");
            if (emph) line = line.Substring(1);
            int size = emph ? 20 : 15;
            var sz = font.GetStringSize(line, HorizontalAlignment.Left, -1, size);
            float a = emph ? 0.6f + 0.4f * MathF.Sin(_t * 4) : 0.85f;
            DrawString(font, new Vector2(vs.X / 2 - sz.X / 2, tp.Y + 50 + k * 26), line, HorizontalAlignment.Left, -1, size, new Color(1, 1, 1, a));
        }
    }
}
