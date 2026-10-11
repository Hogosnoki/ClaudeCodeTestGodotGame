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
    /// <summary>Showing the run's metrics rather than the build (left / right, or a click on the tabs, switches).</summary>
    public bool Metrics;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open(bool metrics = false)
    {
        Metrics = metrics;
        RunMetrics.Send();
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
        else if (Input.IsActionJustPressed("ui_left") || Input.IsActionJustPressed("ui_right") || Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_right"))
            Metrics = !Metrics;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb && _t - _openedAt > 0.15f)
        {
            // (a click on the tabs switches the page; anywhere else closes it)
            if (mb.Position.Y < 84) Metrics = mb.Position.X > Size.X / 2;
            else Close();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var p = G.Player;
        if (p == null) return;
        var s = p.Stats;
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0.02f, 0.02f, 0.03f, 0.97f));
        // the two pages, as tabs
        var gold = new Color(1f, 0.88f, 0.5f);
        DrawString(font, new Vector2(0, 30), "YOUR BUILD", HorizontalAlignment.Right, vs.X / 2 - 24, 15, Metrics ? new Color(1, 1, 1, 0.4f) : gold);
        DrawString(font, new Vector2(vs.X / 2 + 24, 30), "RUN METRICS", HorizontalAlignment.Left, -1, 15, Metrics ? gold : new Color(1, 1, 1, 0.4f));
        DrawString(font, new Vector2(0, 30), "·", HorizontalAlignment.Center, vs.X, 15, new Color(1, 1, 1, 0.4f));
        string hint = (G.Main.UsingPad ? "BACK or B to close" : "TAB or ESC to close") + "   ·   left / right: build or metrics";
        DrawString(font, new Vector2(0, vs.Y - 22), hint, HorizontalAlignment.Center, vs.X, 13, new Color(1, 1, 1, 0.5f));
        if (Metrics) { DrawMetrics(font, vs); return; }
        string title = $"YOUR BUILD  ·  {s.Hero.ToString().ToUpperInvariant()}  ·  LEVEL {p.Level}";
        DrawString(font, new Vector2(0, 62), title, HorizontalAlignment.Center, vs.X, 24, gold);

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
                // (or ruled out by a card of another ability's: faint, and crossed)
                bool barred = !mine && a.Excludes.Any(ex => s.StackOf(ex) > 0);
                var col = mine ? alt : other || barred ? new Color(0.45f, 0.45f, 0.5f, 0.6f) : new Color(alt, 0.45f);
                DrawString(font, new Vector2(x + 4, y), (mine ? "◆ " : "◇ ") + a.Name + (barred ? "  ×" : ""), HorizontalAlignment.Left, colW - 4, 14, col);
                y += 18;
                foreach (var u in Upgrades.Chest.Where(u => u.Requires == a.Id)) y = Line(font, x, y, colW, u, s, 16, dim: !mine);
            }
            bottom = Math.Max(bottom, y);
        }

        // everything else taken: generic, conditional and risk-reward cards
        float yy = bottom + 26;
        foreach (var kind in new[] { UpgradeKind.Generic, UpgradeKind.Conditional, UpgradeKind.RiskReward, UpgradeKind.Relic })
        {
            var cards = Upgrades.Chest.Concat(Upgrades.Relics).Where(u => u.Kind == kind && s.StackOf(u.Id) > 0)
                .Select(u => u.MaxStacks > 1 ? $"{u.Name} {s.StackOf(u.Id)}/{u.MaxStacks}" : u.Name).ToList();
            var col = UpgradeMenu.KindColor(kind);
            string label = kind switch { UpgradeKind.Generic => "GENERIC", UpgradeKind.Conditional => "CONDITIONAL", UpgradeKind.Relic => "RELICS", _ => "RISK · REWARD" };
            DrawString(font, new Vector2(margin, yy), label, HorizontalAlignment.Left, 160, 13, col);
            DrawString(font, new Vector2(margin + 170, yy), cards.Count > 0 ? string.Join("   ·   ", cards) : "none yet", HorizontalAlignment.Left, vs.X - margin * 2 - 170, 13,
                cards.Count > 0 ? new Color(0.9f, 0.9f, 0.95f) : new Color(1, 1, 1, 0.35f));
            yy += 22;
        }
    }

    private static string N(float v) => v >= 10000 ? $"{v / 1000f:0.#}k" : Mathf.RoundToInt(v).ToString("N0");

    /// <summary>The run's metrics: the party's heroes side by side, then what each one's damage came from, and the creatures.</summary>
    private void DrawMetrics(Font font, Vector2 vs)
    {
        var party = RunMetrics.Party();
        var gold = new Color(1f, 0.88f, 0.5f);
        DrawString(font, new Vector2(0, 62), $"RUN METRICS  ·  DEPTH {G.Depth}  ·  {G.Biome?.Name.ToUpperInvariant() ?? ""}", HorizontalAlignment.Center, vs.X, 22, gold);
        float lx = 50, lw = 250, cx0 = lx + lw;
        float colW = Math.Min(260f, (vs.X - cx0 - 40) / Math.Max(1, party.Count));
        float y = 100;
        // the heroes along the top
        for (int k = 0; k < party.Count; k++)
        {
            var m = party[k];
            var hc = Hud.HeroColor(m.Hero);
            DrawString(font, new Vector2(cx0 + k * colW, y), m.Name.ToUpperInvariant(), HorizontalAlignment.Left, colW - 10, 14, hc);
            DrawString(font, new Vector2(cx0 + k * colW, y + 15), m.Hero.ToString(), HorizontalAlignment.Left, colW - 10, 11, new Color(hc, 0.7f));
        }
        y += 38;
        // (each row: the label, then each hero's value; the best of several in gold)
        void Row(string label, Func<HeroMetrics, float> value, Func<HeroMetrics, string> text, bool higherBetter = true)
        {
            DrawString(font, new Vector2(lx, y), label, HorizontalAlignment.Left, lw - 10, 13, new Color(0.8f, 0.8f, 0.85f));
            float best = higherBetter ? party.Max(value) : party.Min(value);
            for (int k = 0; k < party.Count; k++)
            {
                bool top = party.Count > 1 && Math.Abs(value(party[k]) - best) < 0.001f && best != 0;
                DrawString(font, new Vector2(cx0 + k * colW, y), text(party[k]), HorizontalAlignment.Left, colW - 10, 13, top ? gold : new Color(0.95f, 0.95f, 1f));
            }
            DrawLine(new Vector2(lx, y + 5), new Vector2(cx0 + party.Count * colW - 10, y + 5), new Color(1, 1, 1, 0.06f), 1f);
            y += 19;
        }
        Row("Attacks", m => m.Attacks, m => m.Attacks.ToString("N0"));
        Row("Hits · misses (hit rate)", m => m.HitRate, m => $"{m.Hits:N0} · {Math.Max(0, m.Attacks - m.Hits):N0}  ({m.HitRate * 100:0}%)");
        Row("Damage dealt", m => m.Dealt, m => N(m.Dealt));
        Row("Overkill (past a creature's last health)", m => m.Overkill, m => N(m.Overkill), false);
        Row("Kills  (elites · bosses)", m => m.Kills, m => $"{m.Kills}  ({m.EliteKills} · {m.BossKills})");
        Row("Damage to elites · bosses", m => m.DealtElites + m.DealtBosses, m => $"{N(m.DealtElites)} · {N(m.DealtBosses)}");
        Row("Damage taken", m => m.Taken, m => N(m.Taken), false);
        Row("Taken from elites · bosses", m => m.TakenElites + m.TakenBosses, m => $"{N(m.TakenElites)} · {N(m.TakenBosses)}", false);
        Row("Damage prevented (to the giver)", m => RunMetrics.MitigationGiven(m.Peer), m => N(RunMetrics.MitigationGiven(m.Peer)));
        Row("Healing given", m => RunMetrics.HealingGiven(m.Peer), m => N(RunMetrics.HealingGiven(m.Peer)));
        Row("Overheal (past full)", m => RunMetrics.OverhealGiven(m.Peer), m => N(RunMetrics.OverhealGiven(m.Peer)), false);
        Row("Deaths", m => m.Deaths, m => m.Deaths.ToString(), false);
        Row("Revives given", m => RunMetrics.RevivesGiven(m.Peer), m => RunMetrics.RevivesGiven(m.Peer).ToString());

        // what each hero's damage came from
        y += 12;
        DrawString(font, new Vector2(lx, y), "DAMAGE BY ATTACK", HorizontalAlignment.Left, lw, 13, gold);
        float y0 = y;
        float rowsMax = y;
        for (int k = 0; k < party.Count; k++)
        {
            var m = party[k];
            float yy = y0;
            foreach (var (src, amt) in m.DealtBy.OrderByDescending(kv => kv.Value).Take(6))
            {
                DrawString(font, new Vector2(cx0 + k * colW, yy), $"{src}  {N(amt)}  ({(m.Dealt > 0 ? amt / m.Dealt * 100 : 0):0}%)", HorizontalAlignment.Left, colW - 10, 12, new Color(0.9f, 0.9f, 0.95f));
                yy += 16;
            }
            rowsMax = Math.Max(rowsMax, yy);
        }
        y = Math.Max(rowsMax, y0 + 16) + 10;
        // and the creatures: the ones each hero hurt most (with kills), and what hurt them most
        DrawString(font, new Vector2(lx, y), "CREATURES  (dealt · kills)", HorizontalAlignment.Left, lw, 13, gold);
        DrawString(font, new Vector2(lx, y + 16 * 5 + 4), "HURT MOST BY", HorizontalAlignment.Left, lw, 13, gold);
        for (int k = 0; k < party.Count; k++)
        {
            var m = party[k];
            float yy = y;
            foreach (var (kind, amt) in m.DealtTo.OrderByDescending(kv => kv.Value).Take(5))
            {
                DrawString(font, new Vector2(cx0 + k * colW, yy), $"{kind}  {N(amt)} · {m.KillsOf.GetValueOrDefault(kind)}", HorizontalAlignment.Left, colW - 10, 12, new Color(0.9f, 0.9f, 0.95f));
                yy += 16;
            }
            yy = y + 16 * 5 + 4;
            foreach (var (kind, amt) in m.TakenFrom.OrderByDescending(kv => kv.Value).Take(3))
            {
                DrawString(font, new Vector2(cx0 + k * colW, yy), $"{kind}  {N(amt)}", HorizontalAlignment.Left, colW - 10, 12, new Color(1f, 0.75f, 0.7f));
                yy += 16;
            }
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
