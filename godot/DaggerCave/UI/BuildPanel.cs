using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Your build: each of the hero's abilities as a small tree (the upgrades that grow it, taken or
/// not, and its alteration, a branch with upgrades of its own), then the generic, conditional and
/// risk-reward cards you've taken. Opened from the pause menu, or with TAB / BACK while picking a
/// card; the same keys (or ESC / B) close it.
/// </summary>
public partial class BuildPanel : Control
{
    /// <summary>True while it's up (the card picker underneath ignores the keys meanwhile).</summary>
    public static bool Showing { get; private set; }
    /// <summary>The frame it last closed on (so the key that closed it doesn't open it again).</summary>
    public static ulong ClosedFrame { get; private set; }
    public Action Closed;
    private float _t, _openedAt;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open()
    {
        Visible = Showing = true;
        _openedAt = _t;
        QueueRedraw();
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = Showing = false;
        ClosedFrame = Engine.GetProcessFrames();
        Closed?.Invoke();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (!Visible) return;
        // (a beat before the same key that opened it can close it)
        if (_t - _openedAt > 0.15f && (Input.IsActionJustPressed("build") || Input.IsActionJustPressed("pause") || Input.IsActionJustPressed("ui_cancel")))
            Close();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _t - _openedAt > 0.15f) { Close(); AcceptEvent(); }
    }

    public override void _Draw()
    {
        var p = G.Player;
        if (p == null) return;
        var s = p.Stats;
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0.02f, 0.02f, 0.03f, 0.97f));
        string title = $"YOUR BUILD  ·  {s.Hero.ToString().ToUpperInvariant()}  ·  LEVEL {p.Level}";
        DrawString(font, new Vector2(0, 54), title, HorizontalAlignment.Center, vs.X, 26, new Color(1f, 0.88f, 0.5f));
        string hint = G.Main.UsingPad ? "BACK or B to close" : "TAB or ESC to close";
        DrawString(font, new Vector2(0, vs.Y - 22), hint, HorizontalAlignment.Center, vs.X, 13, new Color(1, 1, 1, 0.5f));

        // the four ability trees, side by side
        var trees = Upgrades.Abilities(s.Hero);
        float margin = 40, gap = 18;
        float colW = (vs.X - margin * 2 - gap * (trees.Length - 1)) / trees.Length;
        float top = 96;
        var cls = UpgradeMenu.KindColor(UpgradeKind.Class);
        var alt = UpgradeMenu.KindColor(UpgradeKind.Alteration);
        float bottom = top;
        for (int t = 0; t < trees.Length; t++)
        {
            var (key, name) = trees[t];
            float x = margin + t * (colW + gap), y = top;
            DrawRect(new Rect2(x, y - 26, colW, 3), cls);
            DrawString(font, new Vector2(x, y - 6), name.ToUpperInvariant(), HorizontalAlignment.Left, colW, 16, cls);
            y += 16;
            var alterations = Upgrades.Chest.Where(u => u.Ability == key && u.Alteration && HasFor(u, s.Hero)).ToList();
            var grown = Upgrades.Chest.Where(u => u.Ability == key && !u.Alteration && HasFor(u, s.Hero) && !alterations.Any(a => a.Id == u.Requires)).ToList();
            foreach (var u in grown) y = Line(font, x, y, colW, u, s, 0);
            // the alteration branch (the primary attack has none)
            if (alterations.Count > 0)
            {
                y += 8;
                DrawString(font, new Vector2(x, y), "ALTERATION", HorizontalAlignment.Left, colW, 11, new Color(alt, 0.8f));
                y += 16;
            }
            var taken = Upgrades.AlterationOf(s, key);
            foreach (var a in alterations)
            {
                bool mine = taken == a, other = taken != null && !mine;
                var col = mine ? alt : other ? new Color(0.45f, 0.45f, 0.5f, 0.6f) : new Color(alt, 0.45f);
                DrawString(font, new Vector2(x + 4, y), (mine ? "◆ " : "◇ ") + a.Name, HorizontalAlignment.Left, colW - 4, 14, col);
                y += 18;
                foreach (var u in Upgrades.Chest.Where(u => u.Requires == a.Id)) y = Line(font, x, y, colW, u, s, 16, dim: !mine);
            }
            bottom = Math.Max(bottom, y);
        }

        // everything else taken: generic, conditional and risk-reward cards
        float yy = bottom + 26;
        foreach (var kind in new[] { UpgradeKind.Generic, UpgradeKind.Conditional, UpgradeKind.SideGrade })
        {
            var cards = Upgrades.Chest.Where(u => u.Kind == kind && s.StackOf(u.Id) > 0)
                .Select(u => u.MaxStacks > 1 ? $"{u.Name} {s.StackOf(u.Id)}/{u.MaxStacks}" : u.Name).ToList();
            var col = UpgradeMenu.KindColor(kind);
            string label = kind switch { UpgradeKind.Generic => "GENERIC", UpgradeKind.Conditional => "CONDITIONAL", _ => "RISK · REWARD" };
            DrawString(font, new Vector2(margin, yy), label, HorizontalAlignment.Left, 160, 13, col);
            DrawString(font, new Vector2(margin + 170, yy), cards.Count > 0 ? string.Join("   ·   ", cards) : "none yet", HorizontalAlignment.Left, vs.X - margin * 2 - 170, 13,
                cards.Count > 0 ? new Color(0.9f, 0.9f, 0.95f) : new Color(1, 1, 1, 0.35f));
            yy += 22;
        }
    }

    private static bool HasFor(Upgrade u, HeroKind h) => u.For != null && Array.IndexOf(u.For, h) >= 0;

    /// <summary>One upgrade in a tree: bright if taken (with its ranks), dim if not, fainter still if ruled out.</summary>
    private float Line(Font font, float x, float y, float w, Upgrade u, PlayerStats s, float indent, bool dim = false)
    {
        int n = s.StackOf(u.Id);
        // (one a card you have rules out: faint, and crossed)
        bool barred = n == 0 && u.Excludes.Any(x => s.StackOf(x) > 0);
        var col = n > 0 ? new Color(0.95f, 0.95f, 1f) : new Color(1, 1, 1, dim || barred ? 0.16f : 0.36f);
        string ranks = barred ? "  ×" : u.MaxStacks > 1 ? $"  {n}/{u.MaxStacks}" : n > 0 ? "  ✓" : "";
        DrawString(font, new Vector2(x + 8 + indent, y), u.Name + ranks, HorizontalAlignment.Left, w - 8 - indent, 13, col);
        return y + 17;
    }
}
