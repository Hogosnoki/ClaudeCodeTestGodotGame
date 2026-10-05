using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The main menu, over the camp outside the cave: the game's name, and single player, multiplayer,
/// settings, a way to support the game, and quit. Keys, the controller and the mouse all work
/// (up / down and A, or click).
/// </summary>
public partial class MainMenu : Control
{
    public Action SinglePlayer, MultiPlayer, Settings, Quit;
    /// <summary>The page the support button opens.</summary>
    public const string SupportUrl = "https://ko-fi.com/hogosnoki#";

    private Button _first;
    private Label _footer;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiKit.Theme;
        Visible = false;

        // a soft shade down the left, so the words stand out against the bright sky
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { new Color(0.02f, 0.03f, 0.05f, 0.72f), new Color(0.02f, 0.03f, 0.05f, 0.5f), new Color(0.02f, 0.03f, 0.05f, 0f) }, Offsets = new[] { 0f, 0.55f, 1f } },
                FillFrom = new Vector2(0, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 256, Height = 4,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
            AnchorBottom = 1f, OffsetRight = 560,
        };
        AddChild(shade);

        var col = new VBoxContainer { Position = new Vector2(72, 112), CustomMinimumSize = new Vector2(380, 0) };
        col.AddThemeConstantOverride("separation", 12);
        AddChild(col);
        var title = UiKit.Label("DAGGER DEEP", 66, UiKit.Gold);
        title.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.07f, 0.02f));
        title.AddThemeConstantOverride("outline_size", 7);
        col.AddChild(title);
        var tag = UiKit.Label("A rogue-lite descent from this sunny meadow to the dragon at the bottom of the world.", 16, new Color(0.95f, 0.95f, 0.9f));
        tag.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        tag.CustomMinimumSize = new Vector2(380, 0);
        tag.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        tag.AddThemeConstantOverride("outline_size", 5);
        col.AddChild(tag);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        Button Add(string text, Action act)
        {
            var b = UiKit.Button(text, () => act?.Invoke(), 320);
            b.CustomMinimumSize = new Vector2(320, 48);
            b.AddThemeFontSizeOverride("font_size", 19);
            b.Alignment = HorizontalAlignment.Left;
            col.AddChild(b);
            return b;
        }
        _first = Add("Single player", () => SinglePlayer?.Invoke());
        Add("Multiplayer (online)", () => MultiPlayer?.Invoke());
        Add("Settings", () => Settings?.Invoke());
        var support = Add("Support the game on Ko-fi", () => OS.ShellOpen(SupportUrl));
        support.AddThemeColorOverride("font_color", new Color(1f, 0.72f, 0.62f));
        support.TooltipText = SupportUrl;
        Add("Quit", () => Quit?.Invoke());

        _footer = UiKit.Label("", 14, new Color(0.92f, 0.9f, 0.84f));
        _footer.Position = new Vector2(74, 660);
        _footer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _footer.AddThemeConstantOverride("outline_size", 4);
        AddChild(_footer);
    }

    public void Open(string footer = "")
    {
        Visible = true;
        _footer.Text = footer;
        _first?.CallDeferred(Control.MethodName.GrabFocus);
    }
}

/// <summary>
/// Getting ready at the camp fire, one stage at a time: <b>1</b> choose the hero (they sit round the
/// fire and the chosen one stands), <b>2</b> prepare them (class perks, loadout, upgrade trees), and
/// <b>3</b> look over the difficulty and descend. This is the strip along the bottom for each stage.
/// After a run it also carries how the run went, at the top.
/// </summary>
public partial class HeroChoice : Control
{
    public enum StageKind { Hero, Prep, Difficulty }

    public Action Prev, Next, Descend, Back, Trees, Perks, Loadout;

    /// <summary>The stage on show (opening the choice starts again at the hero).</summary>
    public StageKind Stage { get; private set; } = StageKind.Hero;

    private static readonly string[] StageNames = { "CHARACTER", "LOADOUT & PERKS", "DIFFICULTY" };

    private Label _name, _desc, _hint, _embers;
    private Button _trees, _perks, _loadout, _go, _back;
    private Label _brought, _prepTitle, _prepHint, _diffHint, _diffBrought;
    private CheckBox _hard;
    private HSlider _diff, _perPlayer;
    private Label _diffText;
    private PanelContainer _summary;
    private Label _sumTitle, _sumLines;
    private StyleBoxFlat _nameBox;
    private readonly Label[] _steps = new Label[3];
    private readonly VBoxContainer[] _pages = new VBoxContainer[3];
    private Button _arrowL, _arrowR;
    private Control _lastFocus;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiKit.Theme;
        Visible = false;

        // the strip along the bottom
        // (it grows upward if it needs more room: it must never run off the bottom of the screen)
        var strip = new PanelContainer { AnchorTop = 1f, AnchorBottom = 1f, AnchorRight = 1f, OffsetTop = -178, OffsetBottom = -14, OffsetLeft = 60, OffsetRight = -60, GrowVertical = GrowDirection.Begin };
        _nameBox = new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.82f), BorderColor = UiKit.Gold, BorderWidthTop = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 10 };
        strip.AddThemeStyleboxOverride("panel", _nameBox);
        AddChild(strip);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 6);
        strip.AddChild(outer);

        // where you are: 1 CHARACTER > 2 LOADOUT & PERKS > 3 DIFFICULTY
        var steps = new HBoxContainer();
        steps.AddThemeConstantOverride("separation", 10);
        outer.AddChild(steps);
        for (int k = 0; k < 3; k++)
        {
            if (k > 0) steps.AddChild(UiKit.Label("›", 13, UiKit.Dim));
            _steps[k] = UiKit.Label($"{k + 1}  {StageNames[k]}", 13, UiKit.Dim);
            steps.AddChild(_steps[k]);
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        outer.AddChild(row);

        Button Arrow(string text, Action act)
        {
            var b = UiKit.Button(text, () => act?.Invoke(), 56);
            b.CustomMinimumSize = new Vector2(56, 96);
            b.FocusMode = FocusModeEnum.None;
            b.AddThemeFontSizeOverride("font_size", 30);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            return b;
        }
        _arrowL = Arrow("<", () => Prev?.Invoke());
        row.AddChild(_arrowL);

        // the middle: one page for each stage
        var mid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(mid);
        for (int k = 0; k < 3; k++)
        {
            _pages[k] = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _pages[k].AddThemeConstantOverride("separation", 4);
            mid.AddChild(_pages[k]);
        }

        // ---- stage 1: the hero
        _name = UiKit.Label("", 30, UiKit.Gold);
        _pages[0].AddChild(_name);
        _desc = UiKit.Label("", 16, UiKit.Text);
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desc.CustomMinimumSize = new Vector2(520, 0);
        _pages[0].AddChild(_desc);
        _hint = UiKit.Label("", 13, UiKit.Dim);
        _pages[0].AddChild(_hint);

        // ---- stage 2: perks, loadout, upgrade trees
        _prepTitle = UiKit.Label("", 24, UiKit.Gold);
        _pages[1].AddChild(_prepTitle);
        _brought = UiKit.Label("", 14, new Color(1f, 0.85f, 0.55f));
        _brought.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _brought.CustomMinimumSize = new Vector2(520, 0);
        _pages[1].AddChild(_brought);
        var prep = new HBoxContainer();
        prep.AddThemeConstantOverride("separation", 10);
        _pages[1].AddChild(prep);
        _perks = UiKit.Button("Class perks", () => Perks?.Invoke(), 190);
        prep.AddChild(_perks);
        _loadout = UiKit.Button("Loadout (side-grades)", () => Loadout?.Invoke(), 190);
        prep.AddChild(_loadout);
        _trees = UiKit.Button("Upgrade trees", () => Trees?.Invoke(), 190);
        prep.AddChild(_trees);
        _prepHint = UiKit.Label("", 13, UiKit.Dim);
        _pages[1].AddChild(_prepHint);

        // ---- stage 3: the difficulty (two sliders: on your own the per-player one still counts, once)
        _diffBrought = UiKit.Label("", 14, new Color(1f, 0.85f, 0.55f));
        _pages[2].AddChild(_diffBrought);
        var dr = new HBoxContainer();
        dr.AddThemeConstantOverride("separation", 8);
        dr.AddChild(UiKit.Label("Difficulty", 14, UiKit.Dim));
        _diff = new HSlider { MinValue = RunSettings.DifficultyMin, MaxValue = RunSettings.DifficultyMax, Step = 0.05, CustomMinimumSize = new Vector2(190, 22), SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.All };
        _diff.ValueChanged += v => { RunSettings.Difficulty = (float)v; Refresh(); };
        dr.AddChild(_diff);
        dr.AddChild(UiKit.Label("Per player", 14, UiKit.Dim));
        _perPlayer = new HSlider { MinValue = RunSettings.PerPlayerMin, MaxValue = RunSettings.PerPlayerMax, Step = 0.05, CustomMinimumSize = new Vector2(190, 22), SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.All };
        _perPlayer.ValueChanged += v => { RunSettings.PerPlayer = (float)v; Refresh(); };
        dr.AddChild(_perPlayer);
        _pages[2].AddChild(dr);
        _diffText = UiKit.Label("", 14, UiKit.Gold);
        _pages[2].AddChild(_diffText);
        _hard = new CheckBox { Text = "Hard Mode", FocusMode = FocusModeEnum.All, TooltipText = "Enemies have double the health and deal half as much again: far tougher." };
        _hard.Toggled += on => { RunSettings.Hard = on; Refresh(); };
        _pages[2].AddChild(_hard);
        _diffHint = UiKit.Label("", 13, UiKit.Dim);
        _pages[2].AddChild(_diffHint);

        _arrowR = Arrow(">", () => Next?.Invoke());
        row.AddChild(_arrowR);

        // the side: on to the next stage (on the last, down into the cave), and back
        var side = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        side.AddThemeConstantOverride("separation", 8);
        row.AddChild(side);
        _go = UiKit.Button("", Advance, 250);
        _go.CustomMinimumSize = new Vector2(250, 46);
        _go.AddThemeFontSizeOverride("font_size", 18);
        _go.AddThemeColorOverride("font_color", UiKit.Gold);
        side.AddChild(_go);
        _back = UiKit.Button("", Retreat, 250);
        side.AddChild(_back);
        _embers = UiKit.Label("", 13, new Color(1f, 0.72f, 0.4f), HorizontalAlignment.Center);
        side.AddChild(_embers);

        // how the last run went (after one), at the top
        _summary = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 26 };
        _summary.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.78f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 10, ContentMarginBottom = 12 });
        AddChild(_summary);
        var sc = new VBoxContainer();
        _summary.AddChild(sc);
        _sumTitle = UiKit.Label("", 40, new Color(0.95f, 0.85f, 0.6f), HorizontalAlignment.Center);
        sc.AddChild(_sumTitle);
        _sumLines = UiKit.Label("", 16, UiKit.Text, HorizontalAlignment.Center);
        sc.AddChild(_sumLines);
    }

    /// <summary>Shows the choice from its first stage (with how the last run went on top, if there's a <paramref name="summaryTitle"/>).</summary>
    public void Open(string summaryTitle = "", string summary = "")
    {
        Visible = true;
        _summary.Visible = summaryTitle != "";
        _sumTitle.Text = summaryTitle;
        _sumLines.Text = summary;
        _lastFocus = null;
        SetStage(StageKind.Hero);
    }

    /// <summary>On to the next stage; from the last, down into the cave.</summary>
    public void Advance()
    {
        switch (Stage)
        {
            case StageKind.Hero: SetStage(StageKind.Prep); break;
            case StageKind.Prep: SetStage(StageKind.Difficulty); break;
            default: Descend?.Invoke(); break;
        }
    }

    /// <summary>Back a stage; from the first, back to the menu.</summary>
    public void Retreat()
    {
        switch (Stage)
        {
            case StageKind.Difficulty: SetStage(StageKind.Prep); break;
            case StageKind.Prep: SetStage(StageKind.Hero); break;
            default: Back?.Invoke(); break;
        }
    }

    private void SetStage(StageKind stage)
    {
        Stage = stage;
        _lastFocus = null;
        Refresh();
        if (!IsInsideTree()) return;
        // the first stage is steered with left / right (no control may take those keys); the others
        // start on the button that carries on
        if (stage == StageKind.Hero) CallDeferred(MethodName.DropFocus);
        else CallDeferred(MethodName.RestoreFocus);
    }

    private void DropFocus() => GetViewport()?.GuiReleaseFocus();

    /// <summary>Gives the keys and the controller back to the stage's buttons (after a menu opened from it closes).</summary>
    public void RestoreFocus()
    {
        if (!Visible || !IsVisibleInTree() || Stage == StageKind.Hero) return;
        var target = _lastFocus != null && IsInstanceValid(_lastFocus) && _lastFocus.IsVisibleInTree() ? _lastFocus : _go;
        target.GrabFocus();
    }

    /// <summary>Remembers which control had the focus (so closing a sub-menu returns to it).</summary>
    public override void _Process(double delta)
    {
        if (!Visible || Stage == StageKind.Hero) return;
        var f = GetViewport()?.GuiGetFocusOwner();
        if (f != null && IsAncestorOf(f)) _lastFocus = f;
    }

    /// <summary>Whether the stage with the perks, loadout and trees is showing.</summary>
    public bool InPrep => Visible && Stage == StageKind.Prep;

    /// <summary>The chosen hero's name, colour and description, the stage on show and its key hints.</summary>
    public void Refresh()
    {
        if (_name == null) return;
        var (name, lines) = ScreenOverlay.HeroInfo(G.Hero);
        var accent = Hud.HeroColor(G.Hero);
        bool pad = G.Main?.UsingPad ?? false;
        bool trees = Meta.Trees.Any(Meta.Visible);
        _nameBox.BorderColor = accent;
        for (int k = 0; k < 3; k++)
        {
            _pages[k].Visible = (int)Stage == k;
            _steps[k].AddThemeColorOverride("font_color", (int)Stage == k ? accent.Lightened(0.35f) : k < (int)Stage ? UiKit.Text : UiKit.Dim);
        }
        _arrowL.Visible = _arrowR.Visible = Stage == StageKind.Hero;
        _embers.Text = Meta.Embers > 0 ? $"Embers: {Meta.Embers}" : "";

        // what's brought, for the two stages that show it
        string brought = ClassPerks.EquippedNames(G.Hero);
        string perksLine = brought != "" ? "Perks: " + brought : "No class perks brought";
        string loadoutLine = $"Loadout: {Meta.LoadoutFor(G.Hero).Count}/{Tune.Loadout.Slots} side-grades";

        // ---- stage 1
        _name.Text = name;
        _name.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        _desc.Text = string.Join(" ", lines);
        _hint.Text = pad
            ? "LEFT / RIGHT to choose  ·  A to carry on  ·  B back to the menu"
            : "LEFT / RIGHT to choose  ·  ENTER to carry on  ·  ESC back to the menu";
        _go.Text = Stage switch { StageKind.Hero => "Next: loadout & perks  >", StageKind.Prep => "Next: difficulty  >", _ => "Descend into the cave" };
        _back.Text = Stage == StageKind.Hero ? "Back to the menu" : "< Back";
        _go.AddThemeColorOverride("font_color", UiKit.Gold);

        // ---- stage 2
        _prepTitle.Text = $"Prepare the {name.Substring(0, 1)}{name.Substring(1).ToLowerInvariant()}";
        _prepTitle.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        _brought.Text = perksLine + "\n" + loadoutLine;
        _trees.Visible = trees;
        _prepHint.Text = pad
            ? "A on a button to open it  ·  A on Next to carry on  ·  B back"
            : "P class perks  ·  L loadout" + (trees ? "  ·  U upgrade trees" : "") + "  ·  ENTER to carry on  ·  ESC back";

        // ---- stage 3
        _diffBrought.Text = $"{name}   ·   {perksLine}   ·   {loadoutLine}";
        _hard.SetPressedNoSignal(RunSettings.Hard);
        _diff.SetValueNoSignal(RunSettings.Difficulty);
        _perPlayer.SetValueNoSignal(RunSettings.PerPlayer);
        _diffText.Text = $"Enemies x{RunSettings.HpMult:0.0#} health, x{RunSettings.DmgMult:0.0#} damage";
        _diffHint.Text = pad ? "Up / down to move, left / right to change  ·  A to descend  ·  B back" : "Click or use the arrow keys to change  ·  ENTER to descend  ·  ESC back";
        LinkFocus(trees);
    }

    /// <summary>
    /// Fixes which control the arrow keys and the d-pad reach from each (the engine's guess goes by
    /// where things happen to sit): left and right along a row, up and down between rows, and
    /// nowhere off the ends.
    /// </summary>
    private void LinkFocus(bool trees)
    {
        void Set(Control c, Control left, Control right, Control up, Control down)
        {
            c.FocusNeighborLeft = c.GetPathTo(left ?? c);
            c.FocusNeighborRight = c.GetPathTo(right ?? c);
            c.FocusNeighborTop = c.GetPathTo(up ?? c);
            c.FocusNeighborBottom = c.GetPathTo(down ?? c);
        }
        // stage 2: the three buttons side by side, then Next, then Back
        Set(_perks, null, _loadout, null, _go);
        Set(_loadout, _perks, trees ? _trees : null, null, _go);
        if (trees) Set(_trees, _loadout, null, null, _go);
        // stage 3: the two sliders, Hard Mode, then Descend and Back, top to bottom
        // (the buttons in the side column are shared: up from Next is the row's middle on stage 2, the last control on stage 3)
        if (Stage == StageKind.Difficulty)
        {
            Set(_diff, null, null, null, _perPlayer);
            Set(_perPlayer, null, null, _diff, _hard);
            Set(_hard, null, null, _perPlayer, _go);
            Set(_go, null, null, _hard, _back);
        }
        else Set(_go, null, null, _loadout, _back);
        Set(_back, null, null, _go, null);
    }
}
