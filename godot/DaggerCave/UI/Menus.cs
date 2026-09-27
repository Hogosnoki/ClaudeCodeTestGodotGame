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
        "shield" => new Color(0.45f, 0.7f, 1f),
        "skip" => new Color(0.6f, 0.58f, 0.55f),
        "risk" => new Color(0.86f, 0.42f, 1f),
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
        else if (Input.IsActionJustPressed("pick_4")) Choose(3);
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
        string sub = G.Main.UsingPad ? "Choose one  (left / right to browse, A to take)" : $"Choose one  (click, 1 - {_choices.Count}, or arrows + ENTER)";
        var ssz = font.GetStringSize(sub, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2(vs.X / 2 - ssz.X / 2, vs.Y * 0.2f + 28), sub, HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.7f));

        _cards.Clear();
        int n = _choices.Count;
        float cw = n > 3 ? 220 : 250, ch = 300, gap = n > 3 ? 18 : 26;
        float total = n * cw + (n - 1) * gap;
        float x0 = vs.X / 2 - total / 2, y0 = vs.Y * 0.3f;
        var stats = G.Player?.Stats;
        for (int k = 0; k < n; k++)
        {
            var u = _choices[k];
            bool hov = k == _hover;
            if (u.Icon == "skip") { ch = 230; }
            else ch = 300;
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
            if (u.Icon == "risk")
                DrawString(font, r.Position + new Vector2(cw - 112, 22), "RISK · REWARD", HorizontalAlignment.Left, -1, 11, cat);
            else if (u.Tier == UpgradeTier.Ability)
                DrawString(font, r.Position + new Vector2(cw - 70, 22), "ABILITY", HorizontalAlignment.Left, -1, 11, cat);
            DrawString(font, r.Position + new Vector2(0, 130), u.Name, HorizontalAlignment.Center, cw, 20, Colors.White);
            DrawMultilineString(font, r.Position + new Vector2(16, 162), Upgrades.DescFor(u, stats), HorizontalAlignment.Center, cw - 32, 14, -1, new Color(0.85f, 0.85f, 0.9f));
            if (stats != null && u.MaxStacks > 1 && u.Icon != "skip")
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
    /// <summary>Show the hero cards where a line reads "@" (title and death screens).</summary>
    public bool HeroCards;
    private float _t;
    private readonly Rect2[] _cardRects = new Rect2[3];
    private readonly HeroPortrait[] _portraits = new HeroPortrait[3];

    private const float CardW = 372, CardH = 170;
    private static readonly Vector2 PortraitSize = new(126, CardH - 4);
    private static GradientTexture2D _halo;
    /// <summary>A soft radial spot, white in the middle fading to nothing.</summary>
    private static GradientTexture2D Halo => _halo ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 0.45f, 1f } },
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 128, Height = 128,
    };

    private static readonly (HeroKind kind, string name, string sheet, string[] lines)[] Heroes =
    {
        (HeroKind.Swordsman, "SWORDSMAN", "swordsman", new[]
        {
            "The damage dealer. A heavy sword,",
            "a quick dodge roll, a Charged Strike",
            "and a rooted, crushing heaving swing.",
        }),
        (HeroKind.Warden, "WARDEN", "warden", new[]
        {
            "The guardian. Shield stops 70% of",
            "each blow; a guarded dash breaks",
            "attacks off; a shield bash stuns.",
        }),
        (HeroKind.Vitalist, "VITALIST", "vitalist", new[]
        {
            "The healer. Drained life feeds",
            "alimus for heals and ruptures; a hex",
            "slows foes and makes them frail.",
        }),
    };

    private static Color Accent(HeroKind k) => k switch
    {
        HeroKind.Warden => new Color(0.45f, 0.7f, 1f),
        HeroKind.Vitalist => new Color(0.5f, 1f, 0.45f),
        _ => new Color(0.95f, 0.45f, 0.35f),
    };

    /// <summary>Which hero card (0/1) is under a screen point, or -1.</summary>
    public int CardAt(Vector2 p)
    {
        if (!Visible || !HeroCards) return -1;
        for (int k = 0; k < _cardRects.Length; k++) if (_cardRects[k].HasPoint(p)) return k;
        return -1;
    }

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

    public override void _Process(double delta)
    {
        _t += (float)delta;
        bool cards = Visible && HeroCards;
        // the hero portraits are made the first time the cards show (rendered at twice the size
        // they're drawn), and only render while they show
        if (cards && _portraits[0] == null)
            for (int k = 0; k < Heroes.Length; k++)
            {
                _portraits[k] = new HeroPortrait { Design = Heroes[k].sheet, Size = (Vector2I)(PortraitSize * 2) };
                AddChild(_portraits[k]);
            }
        foreach (var p in _portraits)
            if (p != null) p.RenderTargetUpdateMode = cards ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, Dim));
        int ts = 56;
        var tsz = font.GetStringSize(Title, HorizontalAlignment.Left, -1, ts);
        var tp = new Vector2(vs.X / 2 - tsz.X / 2, vs.Y * (HeroCards ? 0.17f : 0.32f));
        float y = tp.Y + 50;
        DrawString(font, tp + new Vector2(3, 3), Title, HorizontalAlignment.Left, -1, ts, new Color(0, 0, 0, 0.8f));
        DrawString(font, tp, Title, HorizontalAlignment.Left, -1, ts, new Color(0.95f, 0.85f, 0.6f));
        for (int k = 0; k < Lines.Length; k++)
        {
            var line = Lines[k];
            if (line == "@")
            {
                if (HeroCards) { DrawHeroCards(font, vs, y); y += CardH + 22; }
                continue;
            }
            bool emph = line.StartsWith("!");
            if (emph) line = line.Substring(1);
            int size = emph ? 20 : 15;
            if (!emph && line.Length > 90) size = 13;
            var sz = font.GetStringSize(line, HorizontalAlignment.Left, -1, size);
            float a = emph ? 0.6f + 0.4f * MathF.Sin(_t * 4) : 0.85f;
            DrawString(font, new Vector2(vs.X / 2 - sz.X / 2, y), line, HorizontalAlignment.Left, -1, size, new Color(1, 1, 1, a));
            y += 26;
        }
    }

    private void DrawHeroCards(Font font, Vector2 vs, float top)
    {
        int n = Heroes.Length;
        float gap = 18, x0 = vs.X / 2 - (n * CardW + (n - 1) * gap) / 2;
        for (int k = 0; k < n; k++)
        {
            var h = Heroes[k];
            bool sel = G.Hero == h.kind;
            var r = new Rect2(x0 + k * (CardW + gap), top - (sel ? 6 : 0), CardW, CardH);
            _cardRects[k] = r;
            var accent = Accent(h.kind);
            DrawRect(r, new Color(0.07f, 0.07f, 0.1f, sel ? 0.95f : 0.75f));
            DrawRect(r, sel ? accent : accent.Darkened(0.55f), false, sel ? 3 : 1.5f);
            // the hero in 3D, idling (only the chosen one moves)
            var portrait = _portraits[k];
            if (portrait != null)
            {
                portrait.Playing = sel;
                // a soft glow of the hero's colour behind the chosen one
                if (sel) DrawTextureRect(Halo, new Rect2(r.Position + new Vector2(66 - 80, CardH * 0.5f - 80), new Vector2(160, 160)), false, new Color(accent, 0.5f));
                DrawTextureRect(portrait.GetTexture(), new Rect2(r.Position + new Vector2(2, 2), PortraitSize), false, sel ? Colors.White : new Color(0.6f, 0.6f, 0.65f));
            }
            DrawString(font, r.Position + new Vector2(130, 40), h.name, HorizontalAlignment.Left, -1, 22, sel ? accent.Lightened(0.3f) : new Color(1, 1, 1, 0.6f));
            for (int j = 0; j < h.lines.Length; j++)
                DrawString(font, r.Position + new Vector2(130, 72 + j * 22), h.lines[j], HorizontalAlignment.Left, CardW - 136, 12, new Color(1, 1, 1, sel ? 0.9f : 0.5f));
        }
    }
}
