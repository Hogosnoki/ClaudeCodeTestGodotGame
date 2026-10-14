using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The notice when an achievement is earned (until Steam's own takes over): a plaque that slides in at the top right, under the map, for a
/// few seconds, one after another. A progress achievement is steel-blue with a laurel; a feat is gold with a star, and says FEAT.
/// </summary>
public partial class AchievementToast : Control
{
    private readonly Queue<AchDef> _queue = new();
    private AchDef _cur;
    private float _t;
    private const float Show = 4.2f, Slide = 0.35f;
    private static readonly Vector2 Size2 = new(370, 82);
    public static readonly Color ProgressCol = new(0.55f, 0.75f, 1f), FeatCol = new(1f, 0.78f, 0.3f);
    /// <summary>For the tests: how many have been shown, and the one on screen.</summary>
    public int Shown { get; private set; }
    public AchDef Current => _cur;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 50;
        Achievements.Unlocked += Enqueue;
    }

    public override void _ExitTree() => Achievements.Unlocked -= Enqueue;

    private void Enqueue(AchDef d) => _queue.Enqueue(d);

    /// <summary>Drops whatever is showing or waiting (tests).</summary>
    public void Clear() { _queue.Clear(); _cur = null; QueueRedraw(); }

    public override void _Process(double delta)
    {
        if (_cur == null)
        {
            if (_queue.Count == 0) return;
            _cur = _queue.Dequeue();
            _t = 0f;
            Shown++;
            G.Sfx?.Play(_cur.Kind == AchKind.Feat ? "relic_reveal" : "levelup", null, -4, 0f, _cur.Kind == AchKind.Feat ? 1.15f : 1.35f);
        }
        // (a crowd of them at once goes by quicker)
        _t += (float)delta * (_queue.Count > 2 ? 1.8f : 1f);
        if (_t > Show) _cur = null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_cur == null) return;
        var vs = GetViewportRect().Size;
        float slide = _t < Slide ? 1f - _t / Slide : _t > Show - Slide ? (_t - (Show - Slide)) / Slide : 0f;
        slide = slide * slide;
        var at = new Vector2(vs.X - Size2.X - 12 + slide * (Size2.X + 30), 96);
        bool feat = _cur.Kind == AchKind.Feat;
        var col = feat ? FeatCol : ProgressCol;
        var r = new Rect2(at, Size2);
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.09f, 0.94f), BorderColor = col, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ShadowColor = new Color(col, 0.25f), ShadowSize = 8,
        }, r);
        // the emblem: a star for a feat, a laurel ring for progress
        var c = at + new Vector2(35, Size2.Y / 2);
        DrawCircle(c, 22, new Color(col, 0.16f));
        DrawArc(c, 22, 0, Mathf.Tau, 40, col, 2f, true);
        if (feat)
        {
            var pts = new Vector2[10];
            for (int k = 0; k < 10; k++) { float a = -Mathf.Pi / 2 + k * Mathf.Pi / 5; pts[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (k % 2 == 0 ? 14f : 6f); }
            DrawColoredPolygon(pts, col);
        }
        else
        {
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 5; k++)
                {
                    float a = Mathf.Pi / 2 + s * (0.5f + k * 0.42f);
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f;
                    DrawCircle(p, 3.2f, col);
                }
            DrawCircle(c, 4f, col);
        }
        var font = ThemeDB.FallbackFont;
        float x = at.X + 66;
        DrawString(font, new Vector2(x, at.Y + 20), feat ? "FEAT!" : "ACHIEVEMENT", HorizontalAlignment.Left, -1, 12, new Color(col, 0.95f));
        DrawString(font, new Vector2(x, at.Y + 41), _cur.Name, HorizontalAlignment.Left, Size2.X - 76, 18, Colors.White);
        DrawMultilineString(font, new Vector2(x, at.Y + 57), _cur.Desc, HorizontalAlignment.Left, Size2.X - 76, 11, 2, new Color(0.78f, 0.78f, 0.84f));
    }
}

/// <summary>The achievements page: two tabs, Progress (the game's completion) and Feats (hidden until earned).</summary>
public partial class AchievementsMenu : Control
{
    public Action Closed;
    private int _tab;
    private readonly List<Button> _tabs = new();
    private VBoxContainer _body;
    private Label _summary;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.6f));
        var (_, col) = UiKit.Panel(this, new Vector2(900, 600));
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        col.AddChild(head);
        head.AddChild(UiKit.Label("ACHIEVEMENTS", 26, UiKit.Gold));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foreach (var (name, k) in new[] { ("Progress", 0), ("Feats", 1) })
        {
            int tab = k;
            var b = UiKit.Button(name, () => ShowTab(tab), 170);
            b.SetMeta("name", name);
            _tabs.Add(b);
            head.AddChild(b);
        }
        _summary = UiKit.Label("", 14, UiKit.Dim);
        _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _summary.CustomMinimumSize = new Vector2(860, 0);
        col.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        col.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_body);
        var foot = new HBoxContainer();
        col.AddChild(foot);
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foot.AddChild(UiKit.Button("Back", Close, 160));
    }

    public void Open(int tab = 0)
    {
        Visible = true;
        ShowTab(tab);
    }

    /// <summary>For the tests: the tab on show, and how many rows it lists.</summary>
    public int Tab => _tab;
    public int Rows => _body.GetChildCount();

    private void ShowTab(int tab)
    {
        _tab = tab;
        for (int k = 0; k < _tabs.Count; k++) _tabs[k].Text = (k == tab ? "▸ " : "") + (string)_tabs[k].GetMeta("name");
        foreach (var c in _body.GetChildren()) { _body.RemoveChild(c); c.QueueFree(); }
        var kind = tab == 0 ? AchKind.Progress : AchKind.Feat;
        var (got, of) = Achievements.Completion(kind);
        _summary.Text = kind == AchKind.Progress
            ? $"{got} of {of} earned ({(of == 0 ? 0 : got * 100 / of)}%). Progress achievements are earned by the whole party together."
            : $"{got} of {of} feats found. Feats are hidden until earned, are each player's own (unless marked for the party), and don't count toward completion.";
        var col = kind == AchKind.Feat ? AchievementToast.FeatCol : AchievementToast.ProgressCol;
        foreach (var d in Achievements.All.Where(d => d.Kind == kind).OrderBy(d => Achievements.Has(d.Id) ? 0 : 1))
        {
            bool has = Achievements.Has(d.Id);
            bool secret = d.Hidden && !has;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var mark = UiKit.Label(has ? (kind == AchKind.Feat ? "★" : "✔") : "·", 20, has ? col : UiKit.Dim);
            mark.CustomMinimumSize = new Vector2(28, 0);
            row.AddChild(mark);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 0);
            text.AddChild(UiKit.Label(secret ? "???" : d.Name + (d.Kind == AchKind.Feat && d.Party ? "  (party)" : ""), 17, has ? Colors.White : UiKit.Dim));
            var desc = UiKit.Label(secret ? "A hidden feat." : d.Desc, 13, has ? UiKit.Text : new Color(0.5f, 0.5f, 0.56f));
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.AddChild(desc);
            row.AddChild(text);
            if (has && Achievements.Earned.TryGetValue(d.Id, out long when) && when > 0)
                row.AddChild(UiKit.Label(Time.GetDateStringFromUnixTime(when), 12, UiKit.Dim));
            _body.AddChild(row);
        }
        _tabs[tab].CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
    }
}
