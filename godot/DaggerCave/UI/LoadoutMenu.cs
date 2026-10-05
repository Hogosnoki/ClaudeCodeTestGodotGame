using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The loadout, chosen where the hero is: the side-grades (alterations: alternate abilities, like Frostbolt for
/// Firebolt) this hero starts the run with, up to <see cref="Tune.Loadout.Slots"/> of them (one to an ability),
/// applied as the run begins. (For now every side-grade is unlocked, for testing; they will be locked away as the
/// heroes are.) The give-and-take upgrades are the risk-rewards, found in vaults.
/// </summary>
public partial class LoadoutMenu : Control
{
    public event Action Closed;
    private HeroKind _hero;
    private VBoxContainer _list;
    private Label _title, _slots, _note;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.8f));
        var (panel, col) = UiKit.Panel(this, new Vector2(780, 0));
        _title = UiKit.Label("LOADOUT", 28, UiKit.Gold, HorizontalAlignment.Center);
        col.AddChild(_title);
        _slots = UiKit.Label("", 16, UiKit.Text, HorizontalAlignment.Center);
        col.AddChild(_slots);
        _note = UiKit.Label("Side-grades change how an ability works. Start the run with the ones you like, one to an ability.", 13, UiKit.Dim, HorizontalAlignment.Center);
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _note.CustomMinimumSize = new Vector2(740, 0);
        col.AddChild(_note);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(740, 330), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
        col.AddChild(UiKit.Button("Done", Close, 200));
    }

    public void Open(HeroKind hero)
    {
        _hero = hero;
        Visible = true;
        Rebuild();
    }

    private void Close()
    {
        Visible = false;
        Meta.Save();
        Closed?.Invoke();
    }

    /// <summary>The side-grades this hero can bring.</summary>
    public static List<Upgrade> Available(HeroKind hero)
        => Upgrades.Chest.Where(u => u.Alteration && (u.For == null || Array.IndexOf(u.For, hero) >= 0) && Meta.SideGradeUnlocked(u.Id)).ToList();

    private void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var all = Available(_hero);
        var mine = Meta.LoadoutFor(_hero);
        _title.Text = $"LOADOUT  ·  {_hero.ToString().ToUpperInvariant()}";
        _slots.Text = $"{mine.Count} of {Tune.Loadout.Slots} slots used";
        foreach (var u in all)
        {
            bool on = mine.Contains(u.Id);
            bool blocked = !on && (mine.Count >= Tune.Loadout.Slots || mine.Any(o => Conflicts(u, Upgrades.Get(o)))
                || (!on && mine.Any(o => Upgrades.Get(o)?.Ability == u.Ability)));
            var b = new Button
            {
                Text = $"{(on ? "[x]" : "[  ]")}  {Upgrades.AbilityTitle(u.Ability, _hero)}: {u.Name}   —   {Upgrades.DescFor(u, new PlayerStats(_hero))}",
                Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(730, 44), Disabled = blocked,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = FocusModeEnum.All,
            };
            if (on) b.AddThemeColorOverride("font_color", UiKit.Gold);
            string id = u.Id;
            b.Pressed += () => { G.Sfx?.Play("ui", null, -6); Meta.LoadoutToggle(_hero, id); Rebuild(); };
            _list.AddChild(b);
        }
        if (all.Count == 0) _list.AddChild(UiKit.Label("No side-grades unlocked yet.", 16, UiKit.Dim));
    }

    private static bool Conflicts(Upgrade a, Upgrade b) => a != null && b != null && (a.Excludes.Contains(b.Id) || b.Excludes.Contains(a.Id));

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
    }
}
