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
/// Choosing a hero at the camp fire: the three sit round it and the chosen one stands; this is
/// the strip along the bottom with their name and what they do, arrows to change, and the way
/// down into the cave. After a run it also carries how the run went, at the top.
/// </summary>
public partial class HeroChoice : Control
{
    public Action Prev, Next, Descend, Back, Trees, Perks;

    private Label _name, _desc, _hint, _embers;
    private Button _trees, _perks;
    private Label _brought;
    private PanelContainer _summary;
    private Label _sumTitle, _sumLines;
    private StyleBoxFlat _nameBox;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiKit.Theme;
        Visible = false;

        // the strip along the bottom
        var strip = new PanelContainer { AnchorTop = 1f, AnchorBottom = 1f, AnchorRight = 1f, OffsetTop = -178, OffsetBottom = -14, OffsetLeft = 60, OffsetRight = -60 };
        _nameBox = new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.82f), BorderColor = UiKit.Gold, BorderWidthTop = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 10 };
        strip.AddThemeStyleboxOverride("panel", _nameBox);
        AddChild(strip);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        strip.AddChild(row);

        Button Arrow(string text, Action act)
        {
            var b = UiKit.Button(text, () => act?.Invoke(), 56);
            b.CustomMinimumSize = new Vector2(56, 96);
            b.FocusMode = FocusModeEnum.None;
            b.AddThemeFontSizeOverride("font_size", 30);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            return b;
        }
        row.AddChild(Arrow("<", () => Prev?.Invoke()));
        var mid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mid.AddThemeConstantOverride("separation", 4);
        row.AddChild(mid);
        _name = UiKit.Label("", 30, UiKit.Gold);
        mid.AddChild(_name);
        _desc = UiKit.Label("", 16, UiKit.Text);
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desc.CustomMinimumSize = new Vector2(520, 0);
        mid.AddChild(_desc);
        _brought = UiKit.Label("", 13, new Color(1f, 0.85f, 0.55f));
        mid.AddChild(_brought);
        _hint = UiKit.Label("", 13, UiKit.Dim);
        mid.AddChild(_hint);
        row.AddChild(Arrow(">", () => Next?.Invoke()));

        var side = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        side.AddThemeConstantOverride("separation", 8);
        row.AddChild(side);
        var go = UiKit.Button("Descend into the cave", () => Descend?.Invoke(), 250);
        go.FocusMode = FocusModeEnum.None;
        go.CustomMinimumSize = new Vector2(250, 46);
        go.AddThemeFontSizeOverride("font_size", 18);
        go.AddThemeColorOverride("font_color", UiKit.Gold);
        side.AddChild(go);
        _perks = UiKit.Button("Class perks", () => Perks?.Invoke(), 250);
        _perks.FocusMode = FocusModeEnum.None;
        side.AddChild(_perks);
        _trees = UiKit.Button("Upgrade trees", () => Trees?.Invoke(), 250);
        _trees.FocusMode = FocusModeEnum.None;
        side.AddChild(_trees);
        var back = UiKit.Button("Back to the menu", () => Back?.Invoke(), 250);
        back.FocusMode = FocusModeEnum.None;
        side.AddChild(back);
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

    /// <summary>Shows the choice (with how the last run went on top, if there's a <paramref name="summaryTitle"/>).</summary>
    public void Open(string summaryTitle = "", string summary = "")
    {
        Visible = true;
        _summary.Visible = summaryTitle != "";
        _sumTitle.Text = summaryTitle;
        _sumLines.Text = summary;
        Refresh();
    }

    /// <summary>The chosen hero's name, colour and description, and the key hints.</summary>
    public void Refresh()
    {
        var (name, lines) = ScreenOverlay.HeroInfo(G.Hero);
        var accent = Hud.HeroColor(G.Hero);
        _name.Text = name;
        _name.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        _nameBox.BorderColor = accent;
        _desc.Text = string.Join(" ", lines);
        bool trees = Meta.Trees.Any(Meta.Visible);
        _trees.Visible = trees;
        _embers.Text = Meta.Embers > 0 ? $"Embers: {Meta.Embers}" : "";
        string brought = ClassPerks.EquippedNames(G.Hero);
        _brought.Text = brought != "" ? "Perks: " + brought : "No class perks brought (P)";
        bool pad = G.Main?.UsingPad ?? false;
        _hint.Text = pad
            ? "LEFT / RIGHT to choose  ·  A to descend  ·  B back" + (trees ? "  ·  BACK upgrade trees" : "") + "  ·  ask for perks with the button"
            : "LEFT / RIGHT to choose  ·  ENTER to descend  ·  ESC back  ·  P class perks" + (trees ? "  ·  U upgrade trees" : "");
    }
}
