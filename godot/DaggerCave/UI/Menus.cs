using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>Pick-one-of-three upgrade cards shown on level up or when opening a chest. Pauses the game.</summary>
public partial class UpgradeMenu : Control
{
    public event Action<Upgrade> Picked;
    /// <summary>Opens your build over the cards (TAB / BACK).</summary>
    public Action ShowBuild;
    private List<Upgrade> _choices = new();
    /// <summary>Why each card can't be taken (null: it can): a friend's card, or one you have.</summary>
    private List<string> _locks = new();
    private readonly List<Rect2> _cards = new();
    private float _deniedT;
    private int _denied = -1;
    private int _hover = -1;
    private string _title = "";
    private float _t;
    private float _openedAt;

    /// <summary>The colour of each kind of card (its top stripe, frame and label).</summary>
    public static Color KindColor(UpgradeKind k) => k switch
    {
        UpgradeKind.Class => new Color(1f, 0.8f, 0.38f),
        UpgradeKind.Alteration => new Color(0.35f, 0.95f, 0.88f),
        UpgradeKind.Conditional => new Color(0.55f, 1f, 0.6f),
        UpgradeKind.RiskReward => new Color(0.86f, 0.42f, 1f),
        UpgradeKind.Relic => new Color(1f, 0.45f, 0.58f),
        _ => new Color(0.74f, 0.8f, 0.94f),
    };

    /// <summary>A card's label: its kind, and for a hero's card, the ability it grows.</summary>
    public static string KindLabel(Upgrade u, HeroKind hero) => u.Kind switch
    {
        UpgradeKind.Class => "CLASS · " + Upgrades.AbilityTitle(u.Ability, hero).ToUpperInvariant(),
        UpgradeKind.Alteration => "ALTERATION · " + Upgrades.AbilityTitle(u.Ability, hero).ToUpperInvariant(),
        UpgradeKind.Conditional => "CONDITIONAL",
        UpgradeKind.RiskReward => "RISK · REWARD",
        UpgradeKind.Relic => u.For != null ? "RELIC · " + string.Join(" / ", u.For.Select(h => h.ToString().ToUpperInvariant())) : "RELIC",
        _ => "GENERIC",
    };

    public static Color CategoryColor(string cat) => cat switch
    {
        "relic" => new Color(1f, 0.5f, 0.62f),
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
        TextureRepeat = TextureRepeatEnum.Enabled;
        Visible = false;
    }

    /// <summary>What the choice is printed on: a chest's wood, a relic's velvet, a milestone's night sky, a hoard's gold, a vault's stone.</summary>
    private UiArt.Surface _backdrop = UiArt.Surface.Wood;

    public void Open(List<Upgrade> choices, string title, List<string> locks = null, UiArt.Surface backdrop = UiArt.Surface.Wood)
    {
        _backdrop = backdrop;
        _choices = choices;
        _locks = locks ?? new List<string>();
        while (_locks.Count < _choices.Count) _locks.Add(null);
        _title = title;
        // (a controller starts on the first card you can take)
        _hover = G.Main.UsingPad ? Math.Max(0, _locks.FindIndex(l => l == null)) : -1;
        _openedAt = _t;
        _denied = -1;
        Visible = true;
        QueueRedraw();
    }

    public void Choose(int i)
    {
        if (!Visible || i < 0 || i >= _choices.Count || _t - _openedAt < 0.25f) return;
        if (_locks[i] != null)
        {
            // a friend's card, or one you can't use: it stays in the chest
            _denied = i; _deniedT = 0.5f;
            G.Sfx.Play("clink", null, -8, 0, 0.6f);
            return;
        }
        Visible = false;
        Picked?.Invoke(_choices[i]);
    }

    /// <summary>The test bots: take the first card that can be taken.</summary>
    public void ChooseFirstOpen() => Choose(Math.Max(0, _locks.FindIndex(l => l == null)));

    /// <summary>The test bots: whether any card (not "leave it") can be taken.</summary>
    public bool HasOpenCard
    {
        get { for (int k = 0; k < _choices.Count; k++) if (_choices[k].Icon != "skip" && _locks[k] == null) return true; return false; }
    }

    /// <summary>The test bots: take nothing (the "leave it" card).</summary>
    public void ChooseLeave() => Choose(_choices.FindIndex(u => u.Icon == "skip"));

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _deniedT -= (float)delta;
        if (!Visible) return;
        // your build over the cards: the keys are its own until it closes
        if (BuildPanel.Showing) { QueueRedraw(); return; }
        if (Input.IsActionJustPressed("build") && _t - _openedAt > 0.25f && BuildPanel.ClosedFrame != Engine.GetProcessFrames()) { ShowBuild?.Invoke(); return; }
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

    /// <summary>The frame round the whole choice, by what it is: iron brackets on a chest, gems on a relic, stars on a milestone.</summary>
    private UiArt.Rarity BackdropFrame => _backdrop switch
    {
        UiArt.Surface.Velvet => UiArt.Rarity.Relic,
        UiArt.Surface.Gold or UiArt.Surface.Vault or UiArt.Surface.Night => UiArt.Rarity.Rare,
        _ => UiArt.Rarity.Uncommon,
    };

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, 0.55f));

        int n = _choices.Count;
        float cw = n > 3 ? 220 : 250, gap = n > 3 ? 18 : 26;
        float total = n * cw + (n - 1) * gap;
        float x0 = vs.X / 2 - total / 2, y0 = vs.Y * 0.3f;
        // the mat the cards lie on: what kind of choice this is
        var mat = new Rect2(x0 - 40, vs.Y * 0.2f - 46, total + 80, y0 - vs.Y * 0.2f + 46 + 300 + 44);
        UiArt.Shadow(this, mat, 22f, 0.6f);
        UiArt.FillRounded(this, mat, 22f, _backdrop, Colors.White, 1f);
        if (_backdrop == UiArt.Surface.Night)
        {
            // (a milestone: rays from behind the title)
            var o = new Vector2(vs.X / 2, vs.Y * 0.2f);
            for (int k = 0; k < 14; k++)
            {
                float a = k * MathF.Tau / 14f + _t * 0.08f;
                DrawColoredPolygon(new[] { o, o + new Vector2(MathF.Cos(a - 0.05f), MathF.Sin(a - 0.05f)) * 260f, o + new Vector2(MathF.Cos(a + 0.05f), MathF.Sin(a + 0.05f)) * 260f },
                    new Color(0.7f, 0.75f, 1f, 0.035f));
            }
        }
        UiArt.Frame(this, mat, 22f, BackdropFrame, _t, false, _backdrop == UiArt.Surface.Velvet ? new Color(0.95f, 0.25f, 0.4f) : new Color(0.4f, 0.75f, 1f));

        var tsz = font.GetStringSize(_title, HorizontalAlignment.Left, -1, 34);
        float pop = 1 + 0.05f * MathF.Sin(_t * 5);
        // (the title breathes by scaling the drawing, not by stepping through whole font sizes: those jump as the centring doesn't)
        DrawSetTransform(new Vector2(vs.X / 2, vs.Y * 0.2f - 11f), 0f, new Vector2(pop, pop));
        DrawString(font, new Vector2(-tsz.X / 2 + 2, 13f), _title, HorizontalAlignment.Left, -1, 34, new Color(0, 0, 0, 0.7f));
        DrawString(font, new Vector2(-tsz.X / 2, 11f), _title, HorizontalAlignment.Left, -1, 34, new Color(1f, 0.9f, 0.5f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        string sub = G.Main.UsingPad ? "Choose one  (left / right to browse, A to take)  ·  BACK: your build" : $"Choose one  (click, 1 - {_choices.Count}, or arrows + ENTER)  ·  TAB: your build";
        var ssz = font.GetStringSize(sub, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2(vs.X / 2 - ssz.X / 2, vs.Y * 0.2f + 28), sub, HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.75f));

        _cards.Clear();
        var stats = G.Player?.Stats;
        for (int k = 0; k < n; k++)
        {
            var u = _choices[k];
            bool hov = k == _hover;
            bool skip = u.Icon == "skip";
            float ch = skip ? 230 : 300;
            string locked = k < _locks.Count ? _locks[k] : null;
            // a refused card gives a little shake
            float shake = k == _denied && _deniedT > 0 ? MathF.Sin(_deniedT * 60f) * 6f * _deniedT : 0f;
            var r = new Rect2(x0 + k * (cw + gap) + shake, y0 - (hov && locked == null ? 8 : 0), cw, ch);
            _cards.Add(r);
            DrawCard(font, r, u, k, hov, locked, stats);
        }
    }

    /// <summary>
    /// One card: printed on its kind's surface, framed by its rarity, with whose card it is (a hero's emblem in their colour, or a compass
    /// for anyone's) at the top left, what it is for (offense, survival, mobility, a bargain) at the top right, and a chain at the foot
    /// if it rules out another.
    /// </summary>
    private void DrawCard(Font font, Rect2 r, Upgrade u, int k, bool hov, string locked, PlayerStats stats)
    {
        const float rad = 16f;
        float cw = r.Size.X, ch = r.Size.Y;
        bool skip = u.Icon == "skip";
        var rar = UiArt.RarityOf(u);
        var role = UiArt.RoleOf(u);
        var kind = skip ? CategoryColor(u.Icon) : KindColor(u.Kind);
        var dim = locked != null ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
        if (locked != null) kind = kind.Darkened(0.55f);
        UiArt.Shadow(this, r, rad);
        UiArt.FillRounded(this, r, rad, UiArt.CardSurface(u), dim, 0.8f, r.Position + new Vector2(k * 97, 0));
        // a wash of the kind's colour across the top
        var band = new Rect2(r.Position + new Vector2(3, 3), new Vector2(cw - 6, 44));
        var bp = UiArt.Rounded(band, rad - 3);
        DrawPolygon(bp, bp.Select(p => new Color(kind, p.Y < band.Position.Y + 22 ? 0.3f : 0f)).ToArray());
        UiArt.Frame(this, r, rad, rar, _t + k, hov && locked == null, rar == UiArt.Rarity.Relic ? new Color(0.95f, 0.3f, 0.45f) : null);
        // an alteration's card glows a little, all round
        if (u.Kind == UpgradeKind.Alteration && !skip) UiArt.Outline(this, r.Grow(6), rad + 6, new Color(kind, 0.2f + 0.1f * MathF.Sin(_t * 3f)), 2f);

        // whose it is: a hero's emblem in their colour, or a compass rose for any hero
        HeroKind? owner = u.For != null && u.For.Length == 1 ? u.For[0] : null;
        if (!skip)
        {
            var ec = r.Position + new Vector2(24, 24);
            var hc = owner is HeroKind h ? Hud.HeroColor(h) : new Color(0.78f, 0.78f, 0.82f);
            if (locked != null) hc = hc.Darkened(0.5f);
            DrawCircle(ec, 14, new Color(0, 0, 0, 0.6f));
            DrawArc(ec, 14, 0, Mathf.Tau, 20, new Color(hc, 0.85f), 1.5f, true);
            UiArt.HeroEmblem(this, ec, 8f, owner, hc);
            // what it's for: a pill at the top right
            if (role != UiArt.Role.None)
            {
                var rc = UiArt.RoleColor(role);
                if (locked != null) rc = rc.Darkened(0.5f);
                string rn = UiArt.RoleName(role);
                float pw = font.GetStringSize(rn, HorizontalAlignment.Left, -1, 10).X + 30;
                var pill = new Rect2(r.End.X - pw - 12, r.Position.Y + 14, pw, 20);
                DrawColoredPolygon(UiArt.Rounded(pill, 10), new Color(0, 0, 0, 0.6f));
                UiArt.Outline(this, pill, 10, new Color(rc, 0.8f), 1.2f);
                UiArt.RoleGlyph(this, pill.Position + new Vector2(12, 10), 5f, role, rc);
                DrawString(font, pill.Position + new Vector2(22, 14), rn, HorizontalAlignment.Left, -1, 10, rc);
            }
        }
        // the symbol (or a relic's painted icon) in a medallion
        var gc = r.Position + new Vector2(cw / 2, 80);
        var cat = CategoryColor(u.Icon);
        DrawCircle(gc, 36, new Color(0, 0, 0, 0.45f));
        DrawArc(gc, 36, 0, Mathf.Tau, 32, new Color(UiArt.FrameColor(rar), locked != null ? 0.3f : 0.75f), 2f, true);
        DrawCircle(gc, 33, new Color(cat, 0.1f));
        var relicTex = u.Kind == UpgradeKind.Relic ? Icons.Get(u.Id) : null;
        if (relicTex != null)
            DrawTextureRect(relicTex, new Rect2(gc - new Vector2(32, 32), new Vector2(64, 64)), false, dim);
        else if (Icons.Get(Icons.CategoryFor(u)) is Texture2D sym)
            DrawTextureRect(sym, new Rect2(gc - new Vector2(24, 24), new Vector2(48, 48)), false, kind);
        if (u.Multi && Icons.Get("cat_multi") is Texture2D mt)
        {
            DrawTextureRect(mt, new Rect2(gc + new Vector2(24, 12), new Vector2(20, 20)), false, new Color(1f, 0.85f, 0.45f));
            DrawString(font, gc + new Vector2(20, 42), "PARTY", HorizontalAlignment.Left, -1, 9, new Color(1f, 0.85f, 0.45f, 0.85f));
        }
        // the kind and rarity, the name, the words
        if (!skip)
        {
            string label = KindLabel(u, stats?.Hero ?? G.Hero) + (rar == UiArt.Rarity.Relic ? "" : "  ·  " + UiArt.RarityName(rar));
            DrawString(font, r.Position + new Vector2(10, 132), label, HorizontalAlignment.Center, cw - 20, 10, new Color(kind, 0.95f));
        }
        DrawString(font, r.Position + new Vector2(1, 157), u.Name, HorizontalAlignment.Center, cw, 20, new Color(0, 0, 0, 0.7f));
        DrawString(font, r.Position + new Vector2(0, 156), u.Name, HorizontalAlignment.Center, cw, 20, locked != null ? new Color(0.6f, 0.6f, 0.6f) : Colors.White);
        DrawMultilineString(font, r.Position + new Vector2(16, 180), Upgrades.DescFor(u, stats), HorizontalAlignment.Center, cw - 32, 13, -1, new Color(0.88f, 0.88f, 0.92f, locked != null ? 0.5f : 1f));
        DrawString(font, r.Position + new Vector2(12, ch - 10), $"{k + 1}", HorizontalAlignment.Left, -1, 11, new Color(1, 1, 1, 0.35f));
        if (stats != null && u.MaxStacks > 1 && !skip)
        {
            // the ranks as pips
            int have = stats.StackOf(u.Id);
            float px = r.Position.X + cw / 2 - (u.MaxStacks - 1) * 6f;
            for (int s = 0; s < u.MaxStacks; s++)
            {
                var pc = new Vector2(px + s * 12f, r.End.Y - 14);
                DrawCircle(pc, 4.2f, new Color(0, 0, 0, 0.6f));
                DrawCircle(pc, 3f, s < have ? UiArt.FrameColor(rar) : s == have ? new Color(1, 1, 1, 0.5f + 0.4f * MathF.Sin(_t * 5)) : new Color(1, 1, 1, 0.12f));
            }
        }
        var excl = skip ? new List<Upgrade>() : UiArt.ExclusiveWith(u).ToList();
        if (excl.Count > 0)
        {
            // a chain ribbon: what it can't be had with
            var rib = new Rect2(r.Position + new Vector2(14, ch - 48), new Vector2(cw - 28, 20));
            DrawColoredPolygon(UiArt.Rounded(rib, 10), new Color(0.25f, 0.05f, 0.05f, 0.8f));
            UiArt.Outline(this, rib, 10, new Color(1f, 0.5f, 0.45f, 0.6f), 1f);
            UiArt.ChainGlyph(this, rib.Position + new Vector2(14, 10), 5f, new Color(1f, 0.75f, 0.6f));
            DrawString(font, rib.Position + new Vector2(26, 14), "not with " + string.Join(", ", excl.Select(x => x.Name).Distinct().Take(2)), HorizontalAlignment.Left, rib.Size.X - 32, 10, new Color(1f, 0.78f, 0.7f));
        }
        if (locked != null)
        {
            // a band across the card: whose it is, or why it isn't yours
            var lb = new Rect2(r.Position + new Vector2(8, 112), new Vector2(cw - 16, 26));
            DrawColoredPolygon(UiArt.Rounded(lb, 12), new Color(0, 0, 0, 0.8f));
            DrawString(font, lb.Position + new Vector2(0, 18), locked, HorizontalAlignment.Center, lb.Size.X, 12, new Color(1f, 0.85f, 0.6f, k == _denied && _deniedT > 0 ? 1f : 0.85f));
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
    private readonly Rect2[] _cardRects = new Rect2[8];
    private readonly HeroPortrait[] _portraits = new HeroPortrait[8];

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
            "The guardian. The shield stops every",
            "blow; a Guarded Charge breaks attacks",
            "off; a shield bash stuns all in front.",
        }),
        (HeroKind.Vitalist, "VITALIST", "vitalist", new[]
        {
            "The healer. Drained life feeds",
            "vital force for heals and ruptures; a hex",
            "slows foes and makes them frail.",
        }),
        (HeroKind.Elementalist, "ELEMENTALIST", "elementalist", new[]
        {
            "The storm-caller. Firebolts set foes",
            "alight; a blizzard freezes them and a",
            "snap shatters them; an updraft lifts all.",
        }),
        (HeroKind.Rogue, "ROGUE", "rogue", new[]
        {
            "The cutthroat. Quick dagger jabs that",
            "can strike twice as hard; daggers thrown",
            "and torn back; vanishing into shadow.",
        }),
        (HeroKind.Aegis, "AEGIS", "aegis", new[]
        {
            "The ward. A bolt that hurts, weakens and mends;",
            "a barrier, a burden shared and a bubble for a",
            "friend (or yourself): it keeps the others alive.",
        }),
        (HeroKind.ShapeShifter, "SHAPE SHIFTER", "shapeshifter", new[]
        {
            "The mimic. A feeble staff, until it becomes the",
            "creature beside it: that one's gait, bite and",
            "special trick, for as long as it likes.",
        }),
        (HeroKind.Automaton, "AUTOMATON", "automaton_hero", new[]
        {
            "The engine. Slow, heavy and hard to stop: a cleaver,",
            "a brace, a blast of steam. Needs no air, fears no water;",
            "healing only fuels the Self-Repair that mends it.",
        }),
    };

    private static Color Accent(HeroKind k) => Hud.HeroColor(k);

    /// <summary>A hero's name and what they do (for the choice at the camp fire).</summary>
    public static (string name, string[] lines) HeroInfo(HeroKind k)
    {
        foreach (var h in Heroes) if (h.kind == k) return (h.name, h.lines);
        return (k.ToString().ToUpperInvariant(), Array.Empty<string>());
    }

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
        TextureRepeat = TextureRepeatEnum.Enabled;
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
            // a card on leather, in a frame of the hero's colour (gilded when chosen), the hero's emblem at the corner
            UiArt.Shadow(this, r, 14f, sel ? 0.55f : 0.35f);
            UiArt.FillRounded(this, r, 14f, UiArt.Surface.Leather, sel ? new Color(1.05f, 1f, 1f) : new Color(0.6f, 0.58f, 0.6f, 0.9f), 0.9f, r.Position + new Vector2(k * 113, 0));
            UiArt.Frame(this, r, 14f, sel ? UiArt.Rarity.Rare : UiArt.Rarity.Uncommon, _t + k, sel);
            UiArt.Outline(this, r.Grow(-3f), 11f, new Color(accent, sel ? 0.8f : 0.35f), 1.5f);
            DrawCircle(r.End - new Vector2(18, 18), 11, new Color(0, 0, 0, 0.55f));
            UiArt.HeroEmblem(this, r.End - new Vector2(18, 18), 6.5f, h.kind, sel ? accent : accent.Darkened(0.4f));
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
