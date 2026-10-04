using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The look shared by the game's button menus (pause, settings, online play), and a few building blocks.</summary>
public static class UiKit
{
    private static Theme _theme;
    public static readonly Color Gold = new(1f, 0.85f, 0.45f);
    public static readonly Color Text = new(0.9f, 0.9f, 0.94f);
    public static readonly Color Dim = new(0.65f, 0.64f, 0.7f);

    private static StyleBoxFlat Box(Color bg, Color border, int bw, int radius = 4, int padX = 12, int padY = 6) => new()
    {
        BgColor = bg, BorderColor = border,
        BorderWidthLeft = bw, BorderWidthRight = bw, BorderWidthTop = bw, BorderWidthBottom = bw,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
    };

    public static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            var t = new Theme { DefaultFontSize = 16 };
            var normal = Box(new Color(0.11f, 0.1f, 0.13f, 0.95f), new Color(0.3f, 0.28f, 0.34f), 1);
            var hover = Box(new Color(0.17f, 0.15f, 0.2f, 0.98f), new Color(0.5f, 0.46f, 0.55f), 1);
            var pressed = Box(new Color(0.24f, 0.2f, 0.12f, 1f), Gold, 1);
            var focus = Box(new Color(0, 0, 0, 0), Gold, 2);
            var disabled = Box(new Color(0.08f, 0.08f, 0.09f, 0.8f), new Color(0.2f, 0.2f, 0.22f), 1);
            foreach (var type in new[] { "Button", "CheckButton" })
            {
                t.SetStylebox("normal", type, normal);
                t.SetStylebox("hover", type, hover);
                t.SetStylebox("pressed", type, pressed);
                t.SetStylebox("hover_pressed", type, pressed);
                t.SetStylebox("focus", type, focus);
                t.SetStylebox("disabled", type, disabled);
                t.SetColor("font_color", type, Text);
                t.SetColor("font_hover_color", type, Colors.White);
                t.SetColor("font_pressed_color", type, Gold);
                t.SetColor("font_hover_pressed_color", type, Gold);
                t.SetColor("font_focus_color", type, Colors.White);
                t.SetColor("font_disabled_color", type, new Color(0.45f, 0.45f, 0.5f));
            }
            t.SetColor("font_color", "Label", Text);
            var panel = Box(new Color(0.045f, 0.04f, 0.055f, 0.96f), new Color(0.32f, 0.28f, 0.22f), 2, 6, 24, 18);
            t.SetStylebox("panel", "PanelContainer", panel);
            t.SetStylebox("panel", "Panel", panel);
            // sliders: a thin track, a bright grabber, gold when focused
            t.SetStylebox("slider", "HSlider", Box(new Color(0.2f, 0.19f, 0.23f), new Color(0.35f, 0.33f, 0.4f), 1, 3, 0, 3));
            t.SetStylebox("grabber_area", "HSlider", Box(new Color(0.75f, 0.62f, 0.3f), new Color(0.75f, 0.62f, 0.3f), 0, 3, 0, 3));
            t.SetStylebox("grabber_area_highlight", "HSlider", Box(Gold, Gold, 0, 3, 0, 3));
            t.SetStylebox("focus", "HSlider", Box(new Color(0, 0, 0, 0), Gold, 2, 4, 2, 2));
            t.SetStylebox("focus", "ScrollContainer", new StyleBoxEmpty());
            _theme = t;
            return t;
        }
    }

    public static Label Label(string text, int size = 16, Color? col = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        if (col is Color c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static Button Button(string text, Action onPress, int minW = 260)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minW, 40), FocusMode = Control.FocusModeEnum.All };
        b.Pressed += () => { G.Sfx?.Play("ui", null, -6); onPress(); };
        return b;
    }

    /// <summary>A full-screen dim layer that swallows the mouse.</summary>
    public static ColorRect Dimmer(float alpha = 0.72f)
    {
        var r = new ColorRect { Color = new Color(0, 0, 0, alpha), MouseFilter = Control.MouseFilterEnum.Stop };
        r.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return r;
    }

    /// <summary>A centred panel with a column inside it.</summary>
    public static (PanelContainer panel, VBoxContainer col) Panel(Control parent, Vector2 minSize)
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = minSize };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        return (panel, col);
    }

    /// <summary>
    /// Makes sure the menus work with a controller: A presses, B backs out, and the d-pad and left
    /// stick move between buttons (the engine's defaults only cover some of this).
    /// </summary>
    public static void EnsureMenuControls()
    {
        void Axis(string action, JoyAxis axis, float v)
        {
            if (!InputMap.HasAction(action)) return;
            foreach (var e in InputMap.ActionGetEvents(action))
                if (e is InputEventJoypadMotion m && m.Axis == axis && Math.Sign(m.AxisValue) == Math.Sign(v)) return;
            InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = v });
        }
        void Button(string action, JoyButton b)
        {
            if (!InputMap.HasAction(action)) return;
            foreach (var e in InputMap.ActionGetEvents(action))
                if (e is InputEventJoypadButton j && j.ButtonIndex == b) return;
            InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = b });
        }
        Button("ui_accept", JoyButton.A); Button("ui_cancel", JoyButton.B);
        Button("ui_left", JoyButton.DpadLeft); Button("ui_right", JoyButton.DpadRight);
        Button("ui_up", JoyButton.DpadUp); Button("ui_down", JoyButton.DpadDown);
        Axis("ui_left", JoyAxis.LeftX, -1); Axis("ui_right", JoyAxis.LeftX, 1);
        Axis("ui_up", JoyAxis.LeftY, -1); Axis("ui_down", JoyAxis.LeftY, 1);
    }
}

/// <summary>
/// A button that steps through a list of choices: press it (or push left / right while it has
/// focus) to change. Easier with a controller than a drop-down list.
/// </summary>
public partial class Cycler : Button
{
    public string[] Options = Array.Empty<string>();
    public int Index;
    public Action<int> Changed;

    public Cycler() { FocusMode = FocusModeEnum.All; CustomMinimumSize = new Vector2(250, 36); }

    public override void _Ready()
    {
        Pressed += () => Step(1);
        Refresh();
    }

    public void Refresh() => Text = Options.Length > 0 ? $"<   {Options[Math.Clamp(Index, 0, Options.Length - 1)]}   >" : "";

    private void Step(int d)
    {
        if (Options.Length == 0) return;
        Index = (Index + d + Options.Length) % Options.Length;
        Refresh();
        G.Sfx?.Play("ui", null, -8);
        Changed?.Invoke(Index);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_left")) { Step(-1); AcceptEvent(); }
        else if (e.IsActionPressed("ui_right")) { Step(1); AcceptEvent(); }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) { Step(-1); AcceptEvent(); }
    }
}

/// <summary>
/// The pause menu: resume, settings, your build, and leaving (the run, or the online game). In a game on your
/// own the world stands still behind it; online it carries on.
/// </summary>
public partial class PauseMenu : Control
{
    public Action Resume, Settings, Build, Quit, QuitGame, Unstick;
    private Button _first, _quit;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.55f));
        var (_, col) = UiKit.Panel(this, new Vector2(360, 0));
        col.AddChild(UiKit.Label("PAUSED", 30, UiKit.Gold, HorizontalAlignment.Center));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _first = UiKit.Button("Resume", () => Resume?.Invoke());
        col.AddChild(_first);
        col.AddChild(UiKit.Button("Settings", () => Settings?.Invoke()));
        col.AddChild(UiKit.Button("Your build", () => Build?.Invoke()));
        col.AddChild(UiKit.Button("Unstick me", () => Unstick?.Invoke()));
        _quit = UiKit.Button("Give up this run", () => Quit?.Invoke());
        col.AddChild(_quit);
        col.AddChild(UiKit.Button("Quit to desktop", () => QuitGame?.Invoke()));
    }

    /// <summary>Shows the menu (online, leaving is the host's end of the game for everyone).</summary>
    public void Open(bool online, bool host = false)
    {
        _quit.Text = !online ? "Give up this run" : host ? "End the online game (for everyone)" : "Leave the online game";
        Visible = true;
        _first.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Resume?.Invoke(); }
    }
}

/// <summary>
/// Settings: Graphics, Sound and Controls. Everything takes effect at once and is saved when
/// the screen closes. Controls rebinds up to three keys or mouse buttons and three controller
/// inputs per action.
/// </summary>
public partial class SettingsMenu : Control
{
    public Action Closed;
    private int _tab;
    private readonly List<Button> _tabs = new();
    private VBoxContainer _body;
    private Label _hint;
    // rebinding
    private string _capAction;
    private int _capSlot;
    private bool _capPad;
    private Button _capButton;
    private float _capT;
    private string _focusAfter;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.6f));
        var (_, col) = UiKit.Panel(this, new Vector2(1160, 620));
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        col.AddChild(head);
        head.AddChild(UiKit.Label("SETTINGS", 26, UiKit.Gold));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foreach (var (name, k) in new[] { ("Graphics", 0), ("Sound", 1), ("Controls", 2) })
        {
            int tab = k;
            var b = UiKit.Button(name, () => Show(tab), 150);
            _tabs.Add(b);
            head.AddChild(b);
        }
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        col.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_body);
        var foot = new HBoxContainer();
        col.AddChild(foot);
        _hint = UiKit.Label("", 13, UiKit.Dim);
        _hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foot.AddChild(_hint);
        foot.AddChild(UiKit.Button("Back", Close, 160));
    }

    public void Open()
    {
        Visible = true;
        Show(_tab);
    }

    private void Close()
    {
        if (_capAction != null) return;
        Visible = false;
        GameSettings.Save();
        Closed?.Invoke();
    }

    public void ShowTab(int tab) => Show(tab);

    private void Show(int tab)
    {
        _tab = tab;
        foreach (var c in _body.GetChildren()) { _body.RemoveChild(c); c.QueueFree(); }
        for (int k = 0; k < _tabs.Count; k++) _tabs[k].Modulate = k == tab ? UiKit.Gold : Colors.White;
        switch (tab)
        {
            case 0: BuildGraphics(); break;
            case 1: BuildSound(); break;
            default: BuildControls(); break;
        }
        _hint.Text = tab == 2
            ? "Pick a slot and press the key, mouse button or controller input for it  ·  DELETE / X clears a slot  ·  LB / RB switch tabs  ·  ESC / B back"
            : "LB / RB switch tabs  ·  LEFT / RIGHT change a setting  ·  ESC / B back";
        // focus the tab's first control (the controller needs somewhere to start)
        _tabs[tab].CallDeferred(Control.MethodName.GrabFocus);
    }

    // ---------------------------------------------------------------- rows

    private GridContainer Grid(int columns = 2)
    {
        var g = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        g.AddThemeConstantOverride("h_separation", 18);
        g.AddThemeConstantOverride("v_separation", 8);
        _body.AddChild(g);
        return g;
    }

    private static void Row(GridContainer g, string label, Control c, string note = "")
    {
        var l = UiKit.Label(label, 16);
        l.CustomMinimumSize = new Vector2(330, 0);
        g.AddChild(l);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        h.AddChild(c);
        if (note != "") h.AddChild(UiKit.Label(note, 13, UiKit.Dim));
        g.AddChild(h);
    }

    private static Cycler Choice(string[] options, int index, Action<int> changed) => new() { Options = options, Index = index, Changed = changed };

    private static CheckButton Toggle(bool on, Action<bool> changed)
    {
        var c = new CheckButton { ButtonPressed = on, Text = on ? "On" : "Off", FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(250, 36) };
        c.Toggled += v => { c.Text = v ? "On" : "Off"; G.Sfx?.Play("ui", null, -8); changed(v); };
        return c;
    }

    private static HSlider Slider(float min, float max, float value, Action<float> changed, Func<float, string> label, out Label shown)
    {
        var s = new HSlider { MinValue = min, MaxValue = max, Step = (max - min) / 20f, Value = value, CustomMinimumSize = new Vector2(250, 30), FocusMode = FocusModeEnum.All, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var l = UiKit.Label(label(value), 15, UiKit.Dim);
        l.CustomMinimumSize = new Vector2(60, 0);
        s.ValueChanged += v => { l.Text = label((float)v); changed((float)v); };
        shown = l;
        return s;
    }

    private static Control SliderRow(float min, float max, float value, Action<float> changed, Func<float, string> label)
    {
        var s = Slider(min, max, value, changed, label, out var shown);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        h.AddChild(s);
        h.AddChild(shown);
        return h;
    }

    private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

    // ---------------------------------------------------------------- graphics

    private static readonly int[] FpsCaps = BuildFpsCaps();

    /// <summary>The usual caps, and this display's own refresh rate among them.</summary>
    private static int[] BuildFpsCaps()
    {
        var l = new System.Collections.Generic.List<int> { 0, 30, 60, 90, 120, 144, 165, 240 };
        try
        {
            int hz = (int)Math.Round(DisplayServer.ScreenGetRefreshRate());
            if (hz >= 20 && hz <= 480 && !l.Contains(hz)) l.Add(hz);
        }
        catch { }
        l.Sort();
        return l.ToArray();
    }
    private static readonly float[] Scales = { 0.5f, 0.67f, 0.75f, 0.85f, 1f };

    private void BuildGraphics()
    {
        var g = Grid();
        void Render() => GameSettings.ApplyRendering(this);
        void Display() => GameSettings.ApplyDisplay(this);
        Row(g, "Window", Choice(new[] { "Windowed", "Borderless fullscreen", "Fullscreen" }, (int)GameSettings.Window, i => { GameSettings.Window = (GameSettings.WindowMode)i; Display(); }));
        Row(g, "Vertical sync", Toggle(GameSettings.VSync, v => { GameSettings.VSync = v; Display(); }));
        Row(g, "Frame rate limit", Choice(Array.ConvertAll(FpsCaps, f => f == 0 ? "Unlimited" : $"{f} fps"), Math.Max(0, Array.IndexOf(FpsCaps, GameSettings.MaxFps)), i => { GameSettings.MaxFps = FpsCaps[i]; Display(); }), "with V-sync on, your display's own rate is the ceiling");
        int si = 0;
        for (int k = 0; k < Scales.Length; k++) if (Math.Abs(Scales[k] - GameSettings.RenderScale) < 0.02f) si = k;
        Row(g, "Resolution scale", Choice(Array.ConvertAll(Scales, f => Pct(f)), si, i => { GameSettings.RenderScale = Scales[i]; Render(); }), "lower is faster");
        Row(g, "Shadows", Choice(new[] { "Off", "Low", "High" }, (int)GameSettings.Shadows, i => { GameSettings.Shadows = (GameSettings.Quality)i; Render(); }));
        Row(g, "Cave haze (volumetric fog)", Toggle(GameSettings.Fog, v => { GameSettings.Fog = v; Render(); }), "the heaviest effect");
        Row(g, "Bloom", Toggle(GameSettings.Bloom, v => { GameSettings.Bloom = v; Render(); }));
        Row(g, "Ambient occlusion", Toggle(GameSettings.AmbientOcclusion, v => { GameSettings.AmbientOcclusion = v; Render(); }));
        Row(g, "Anti-aliasing", Choice(new[] { "Off", "FXAA" }, Math.Min(1, (int)GameSettings.Aa), i => { GameSettings.Aa = (GameSettings.AntiAlias)i; Render(); }), "MSAA is gone: it flickered against the ink outlines");
        Row(g, "Ink outlines on creatures", Toggle(GameSettings.InkOutlines, v => { GameSettings.InkOutlines = v; Render(); }));
        Row(g, "Brightness", SliderRow(0.6f, 1.6f, GameSettings.Brightness, v => { GameSettings.Brightness = v; Render(); }, Pct));
        Row(g, "Screen shake", SliderRow(0f, 1f, GameSettings.Shake, v => GameSettings.Shake = v, Pct));
    }

    // ---------------------------------------------------------------- sound

    private void BuildSound()
    {
        var g = Grid();
        Row(g, "Master volume", SliderRow(0f, 1f, GameSettings.MasterVolume, v => { GameSettings.MasterVolume = v; GameSettings.ApplySound(); }, Pct));
        Row(g, "Music", SliderRow(0f, 1f, GameSettings.MusicVolume, v => { GameSettings.MusicVolume = v; GameSettings.ApplySound(); }, Pct));
        Row(g, "Sound effects", SliderRow(0f, 1f, GameSettings.SfxVolume, v => { GameSettings.SfxVolume = v; GameSettings.ApplySound(); G.Sfx?.Play("hit", null, -6); }, Pct));
    }

    // ---------------------------------------------------------------- controls

    private readonly Dictionary<(string, bool, int), Button> _slots = new();

    private void BuildControls()
    {
        var g = Grid();
        Row(g, "Keyboard presses aim", Choice(new[] { "At the mouse, while you use it", "Always at the mouse", "Where you move" }, (int)GameSettings.Aim, i => GameSettings.Aim = (GameSettings.AimMode)i),
            "controllers aim with the right stick");
        Row(g, "Controller vibration", Toggle(GameSettings.Vibration, v => GameSettings.Vibration = v));

        var table = new GridContainer { Columns = 1 + 2 * Controls.Slots, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 6);
        table.AddThemeConstantOverride("v_separation", 6);
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _body.AddChild(table);
        table.AddChild(UiKit.Label("", 14));
        for (int k = 0; k < Controls.Slots; k++) table.AddChild(UiKit.Label(k == 0 ? "KEYBOARD / MOUSE" : "", 13, UiKit.Gold));
        for (int k = 0; k < Controls.Slots; k++) table.AddChild(UiKit.Label(k == 0 ? "CONTROLLER" : "", 13, UiKit.Gold));
        _slots.Clear();
        foreach (var (action, label) in Controls.Rebindable)
        {
            var l = UiKit.Label(label, 14);
            l.CustomMinimumSize = new Vector2(270, 0);
            // (a long name wraps rather than widening the table)
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            table.AddChild(l);
            foreach (bool pad in new[] { false, true })
            {
                var binds = Controls.Bindings(action, pad);
                for (int k = 0; k < Controls.Slots; k++)
                {
                    int slot = k;
                    bool isPad = pad;
                    string a = action;
                    var b = new Button { Text = slot < binds.Count ? Controls.NameOf(binds[slot]) : "—", CustomMinimumSize = new Vector2(138, 32), FocusMode = FocusModeEnum.All, ClipText = true };
                    b.AddThemeFontSizeOverride("font_size", 13);
                    b.Pressed += () => BeginCapture(a, isPad, slot, b);
                    b.GuiInput += e =>
                    {
                        if (_capAction != null) return;
                        bool clear = e is InputEventKey { Pressed: true } k2 && (k2.PhysicalKeycode is Key.Delete or Key.Backspace)
                                     || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X }
                                     || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
                        if (!clear) return;
                        Controls.Clear(a, isPad, slot);
                        G.Sfx?.Play("clink", null, -10);
                        b.AcceptEvent();
                        Rebuild(a, isPad, slot);
                    };
                    _slots[(action, pad, slot)] = b;
                    table.AddChild(b);
                }
            }
        }
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var reset = UiKit.Button("Reset every control to its default", () =>
        {
            foreach (var (action, _) in Controls.Rebindable) Controls.ResetAction(action);
            Show(2);
        }, 360);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _body.AddChild(reset);
    }

    private void Rebuild(string action, bool pad, int slot)
    {
        Show(2);
        if (_slots.TryGetValue((action, pad, slot), out var b)) b.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void BeginCapture(string action, bool pad, int slot, Button b)
    {
        if (_capAction != null) return;
        _capAction = action; _capPad = pad; _capSlot = slot; _capButton = b; _capT = 6f;
        b.Text = pad ? "press a button…" : "press a key…";
        b.Modulate = UiKit.Gold;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _capAction == null) return;
        _capT -= (float)delta;
        if (_capT <= 0) EndCapture(null);
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (_capAction == null)
        {
            // tabs with the shoulder buttons (or Q / E... no: those may be bound; use PageUp / PageDown)
            if (e is InputEventJoypadButton { Pressed: true } jb && jb.ButtonIndex is JoyButton.LeftShoulder or JoyButton.RightShoulder)
            {
                Show((_tab + (jb.ButtonIndex == JoyButton.RightShoulder ? 1 : 2)) % 3);
                GetViewport().SetInputAsHandled();
            }
            else if (e is InputEventKey { Pressed: true, Echo: false } kp && kp.PhysicalKeycode is Key.Pageup or Key.Pagedown)
            {
                Show((_tab + (kp.PhysicalKeycode == Key.Pagedown ? 1 : 2)) % 3);
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        // capturing a binding: the first press of the right kind of device
        GetViewport().SetInputAsHandled();
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) { EndCapture(null); return; }
        bool wanted = _capPad
            ? e is InputEventJoypadButton { Pressed: true } || (e is InputEventJoypadMotion m && Math.Abs(m.AxisValue) > 0.6f)
            : e is InputEventKey { Pressed: true, Echo: false } || e is InputEventMouseButton { Pressed: true };
        if (wanted) EndCapture(e);
    }

    private void EndCapture(InputEvent e)
    {
        string action = _capAction;
        bool pad = _capPad;
        int slot = _capSlot;
        _capAction = null;
        if (_capButton != null && IsInstanceValid(_capButton)) _capButton.Modulate = Colors.White;
        if (e != null)
        {
            string stolen = Controls.Rebind(action, slot, e);
            G.Sfx?.Play("ui", null, -4);
            if (stolen != null) _hint.Text = $"{Controls.NameOf(e)} was taken off \"{stolen}\".";
        }
        // (rebuilt a frame later, so the press that set the binding doesn't also press the new button)
        CallDeferred(MethodName.RebuildDeferred, action, pad, slot);
    }

    private void RebuildDeferred(string action, bool pad, int slot)
    {
        string keep = _hint.Text;
        Rebuild(action, pad, slot);
        if (keep.Contains("was taken off")) _hint.Text = keep;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || _capAction != null) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pause")) { GetViewport().SetInputAsHandled(); Close(); }
    }
}
