using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Save Files page (from the main menu): every save with what it holds (embers, runs, the deepest depth, heroes freed, achievements),
/// and for each: play it, rename it, reset it (start again from nothing) or delete it; a new save at the bottom. Resetting and deleting
/// ask twice (the button turns red and says so; press it again within a few seconds). See <see cref="Profiles"/>.
/// </summary>
public partial class SaveFilesMenu : Control
{
    public Action Closed;
    /// <summary>Another save is in use now (or this one was reset): the camp has to show it.</summary>
    public Action Changed;
    private VBoxContainer _rows;
    private Label _hint;
    private string _confirm;
    private float _confirmT;
    /// <summary>For the tests: how many saves are listed.</summary>
    public int Rows => _rows.GetChildCount();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.6f));
        var (_, col) = UiKit.Panel(this, new Vector2(900, 560));
        col.AddChild(UiKit.Label("SAVE FILES", 26, UiKit.Gold));
        var note = UiKit.Label("Each save keeps its own progress and its own achievements. (On Steam, achievements will be the account's.) Settings and controls are shared.", 13, UiKit.Dim);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(860, 0);
        col.AddChild(note);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        col.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_rows);
        _hint = UiKit.Label("", 13, new Color(1f, 0.55f, 0.45f));
        col.AddChild(_hint);
        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 10);
        col.AddChild(foot);
        foot.AddChild(UiKit.Button("New save file", () => { Profiles.Create(); Rebuild(); }, 220));
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foot.AddChild(UiKit.Button("Back", Close, 160));
    }

    public void Open()
    {
        Visible = true;
        _confirm = null;
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _confirm == null) return;
        _confirmT -= (float)delta;
        if (_confirmT <= 0) { _confirm = null; Rebuild(); }
    }

    /// <summary>Reset and Delete ask twice: true the second time (within a few seconds).</summary>
    private bool Confirmed(string key)
    {
        if (_confirm == key) { _confirm = null; return true; }
        _confirm = key;
        _confirmT = 4f;
        Rebuild();
        return false;
    }

    private void Rebuild()
    {
        foreach (var c in _rows.GetChildren()) { _rows.RemoveChild(c); c.QueueFree(); }
        Button focus = null;
        foreach (var p in Profiles.All.ToList())
        {
            var info = p;
            bool active = info.Id == Profiles.Active;
            var (embers, runs, best, heroes, earned) = Profiles.Peek(info.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            left.AddThemeConstantOverride("separation", 2);
            var name = new LineEdit { Text = info.Name, MaxLength = 24, CustomMinimumSize = new Vector2(300, 0), TooltipText = "Rename it: type and press Enter" };
            name.TextSubmitted += t => { Profiles.Rename(info.Id, t); Rebuild(); };
            name.FocusExited += () => { if (name.Text != info.Name) Profiles.Rename(info.Id, name.Text); };
            left.AddChild(name);
            left.AddChild(UiKit.Label($"{(active ? "IN USE  ·  " : "")}{embers} embers  ·  {runs} runs  ·  deepest {best}  ·  {heroes} heroes  ·  {earned} achievements", 13, active ? UiKit.Gold : UiKit.Dim));
            row.AddChild(left);
            var use = UiKit.Button(active ? "In use" : "Play this", () => { if (Profiles.Active != info.Id) { Profiles.Switch(info.Id); Changed?.Invoke(); Rebuild(); } }, 130);
            use.Disabled = active;
            row.AddChild(use);
            bool resetAsk = _confirm == "reset" + info.Id, delAsk = _confirm == "del" + info.Id;
            var reset = UiKit.Button(resetAsk ? "Really reset?" : "Reset", () => { if (Confirmed("reset" + info.Id)) { Profiles.Reset(info.Id); if (info.Id == Profiles.Active) Changed?.Invoke(); Rebuild(); } }, 140);
            if (resetAsk) reset.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.4f));
            row.AddChild(reset);
            var del = UiKit.Button(delAsk ? "Really delete?" : "Delete", () => { if (Confirmed("del" + info.Id)) { bool was = info.Id == Profiles.Active; Profiles.Delete(info.Id); if (was) Changed?.Invoke(); Rebuild(); } }, 140);
            if (delAsk) del.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.4f));
            row.AddChild(del);
            _rows.AddChild(row);
            if (resetAsk) focus = reset; else if (delAsk) focus = del; else if (active && focus == null) focus = reset;
        }
        _hint.Text = _confirm == null ? "" : _confirm.StartsWith("reset") ? "Press Reset again to start that save over from nothing (its progress and achievements are lost)." : "Press Delete again to delete that save for good.";
        focus?.CallDeferred(Control.MethodName.GrabFocus);
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
