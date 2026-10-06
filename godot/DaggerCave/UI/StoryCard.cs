using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The story so far (<see cref="Lore.Premise"/>) on a card over the camp: why anyone goes down into the Dagger, who is caged in
/// it and what waits at the bottom. Shown the first time a run is begun and from the main menu after that.
/// </summary>
public partial class StoryCard : Control
{
    public Action Closed;
    private Button _close;
    private ScrollContainer _scroll;
    private VBoxContainer _journal;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.8f));
        var (panel, col) = UiKit.Panel(this, new Vector2(760, 0));
        col.AddThemeConstantOverride("separation", 12);
        var title = UiKit.Label("THE DAGGER", 38, UiKit.Gold, HorizontalAlignment.Center);
        title.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.07f, 0.02f));
        title.AddThemeConstantOverride("outline_size", 5);
        col.AddChild(title);
        col.AddChild(UiKit.Label("What is known at the camp", 15, UiKit.Dim, HorizontalAlignment.Center));
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(700, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FocusMode = FocusModeEnum.All };
        col.AddChild(_scroll);
        var text = new VBoxContainer { CustomMinimumSize = new Vector2(680, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(text);
        for (int k = 0; k < Lore.Premise.Length; k++)
        {
            bool last = k == Lore.Premise.Length - 1;
            var p = UiKit.Label(Lore.Premise[k], last ? 19 : 17, last ? UiKit.Gold : UiKit.Text);
            p.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            p.CustomMinimumSize = new Vector2(670, 0);
            p.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.AddChild(p);
        }
        _journal = new VBoxContainer { CustomMinimumSize = new Vector2(670, 0) };
        _journal.AddThemeConstantOverride("separation", 8);
        text.AddChild(_journal);
        _close = UiKit.Button("Go down", Close, 220);
        _close.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        col.AddChild(_close);
    }

    public void Open(string closeText = "Close")
    {
        _close.Text = closeText;
        RefreshJournal();
        _scroll.ScrollVertical = 0;
        Visible = true;
        _close.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>The Delvers' journal: the pages found at lamps so far, under the story.</summary>
    private void RefreshJournal()
    {
        foreach (var c in _journal.GetChildren()) c.QueueFree();
        if (Meta.JournalFound.Count == 0) return;
        _journal.AddChild(UiKit.Label($"THE DELVERS' JOURNAL  ·  {Meta.JournalFound.Count} of {Lore.Journal.Length - 1} pages", 15, UiKit.Gold));
        foreach (var pg in Lore.Journal)
        {
            if (!Meta.JournalFound.Contains(pg.biome.ToString())) continue;
            var l = UiKit.Label($"{pg.author}:  \"{pg.text}\"", 15, new Color(0.85f, 0.86f, 0.9f));
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(670, 0);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _journal.AddChild(l);
        }
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Closed?.Invoke();
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pause")) { GetViewport().SetInputAsHandled(); Close(); return; }
        // (up and down read on while the button has the focus)
        if (e.IsActionPressed("ui_down")) { _scroll.ScrollVertical += 60; GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed("ui_up")) { _scroll.ScrollVertical -= 60; GetViewport().SetInputAsHandled(); }
    }
}
