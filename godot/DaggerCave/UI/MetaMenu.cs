using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The upgrade trees between runs: buy ranks with embers, then activate them with the tree's
/// resource. Also runs the potion tree's introduction (a free first rank, then spending the
/// first reagent on it), both steps skippable.
/// </summary>
public partial class MetaMenu : Control
{
    public enum Mode { Browse, PotionTutorial, PearlIntro }

    public event Action Closed;
    private Mode _mode;
    private int _step, _sel;
    private float _t, _openedAt;
    private string _flash = "";
    private float _flashT;
    private readonly List<(MetaTree tree, string branch, string name, Rect2 rect)> _rows = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open(Mode mode)
    {
        _mode = mode;
        _step = 0;
        _sel = 0;
        _openedAt = _t;
        Visible = true;
        QueueRedraw();
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    private IEnumerable<(MetaTree tree, string branch, string name)> Rows()
    {
        foreach (var t in Meta.Trees)
        {
            if (_mode == Mode.PotionTutorial && t != Meta.PotionTree) continue;
            if (!Meta.Visible(t)) continue;
            foreach (var (id, name) in t.Branches) yield return (t, id, name);
        }
    }

    private void Say(string s) { _flash = s; _flashT = 2.2f; }

    /// <summary>Acts on the selected row: buy the next rank, or activate a bought one.</summary>
    private void Act()
    {
        var rows = Rows().ToList();
        if (_sel < 0 || _sel >= rows.Count) return;
        var (tree, branch, _) = rows[_sel];
        if (_mode == Mode.PotionTutorial)
        {
            if (_step == 0)
            {
                var first = tree.Branch(branch).First();
                Meta.Buy(first, free: true);
                G.Sfx.Play("chest", null, -4);
                _step = 1;
                Say($"{first.Name} unlocked!");
            }
            else if (_step == 1)
            {
                var tier = Meta.NextToActivate(tree, branch);
                if (tier == null) { Say("Pick the rank you just unlocked."); return; }
                if (Meta.Activate(tier)) { G.Sfx.Play("levelup", null, -4); Say($"{tier.Name} is active!"); EndTutorial(); }
            }
            return;
        }
        var act = Meta.NextToActivate(tree, branch);
        if (act != null)
        {
            if (Meta.Activate(act)) { G.Sfx.Play("levelup", null, -4); Say($"{act.Name} is active!"); }
            else { G.Sfx.Play("ui", null, -6, 0, 0.6f); Say($"You need a {tree.Resource} to activate {act.Name}."); }
            return;
        }
        var buy = Meta.NextToBuy(tree, branch);
        if (buy == null) { Say("That branch is complete."); return; }
        if (Meta.Buy(buy)) { G.Sfx.Play("chest", null, -4); Say($"{buy.Name} bought: spend a {tree.Resource} to activate it."); }
        else { G.Sfx.Play("ui", null, -6, 0, 0.6f); Say($"{buy.Name} costs {buy.Cost} ember{(buy.Cost > 1 ? "s" : "")}."); }
    }

    private void EndTutorial()
    {
        Meta.PotionTutorialDone = true;
        Meta.Save();
        _mode = Mode.Browse;
        _step = 0;
    }

    private void Skip()
    {
        if (_mode == Mode.PotionTutorial) { Say(_step == 0 ? "Skipped. Buy ranks with embers any time (U / BACK)." : "Your reagent is kept for later."); EndTutorial(); return; }
        if (_mode == Mode.PearlIntro) { Meta.PearlTutorialDone = true; Meta.Save(); _mode = Mode.Browse; return; }
        Close();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _flashT -= (float)delta;
        if (!Visible) return;
        QueueRedraw();
        if (_t - _openedAt < 0.3f) return;
        int n = Rows().Count();
        if (Input.IsActionJustPressed("move_up")) { _sel = (_sel + n - 1) % Math.Max(1, n); G.Sfx.Play("ui", null, -8); }
        if (Input.IsActionJustPressed("move_down")) { _sel = (_sel + 1) % Math.Max(1, n); G.Sfx.Play("ui", null, -8); }
        if (_mode == Mode.PearlIntro)
        {
            if (Input.IsActionJustPressed("confirm") || Input.IsActionJustPressed("pause")) Skip();
            return;
        }
        if (Input.IsActionJustPressed("confirm")) Act();
        else if (Input.IsActionJustPressed("skip") && _mode == Mode.PotionTutorial) Skip();
        else if (Input.IsActionJustPressed("pause") || Input.IsActionJustPressed("meta") || Input.IsActionJustPressed("dodge")) Skip();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
            for (int k = 0; k < _rows.Count; k++) if (_rows[k].rect.HasPoint(mm.Position)) _sel = k;
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            for (int k = 0; k < _rows.Count; k++) if (_rows[k].rect.HasPoint(mb.Position)) { _sel = k; Act(); AcceptEvent(); return; }
            if (_mode == Mode.PearlIntro) Skip();
        }
    }

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0.03f, 0.02f, 0.04f, 0.94f));
        void Center(string text, float y, int size, Color col)
        {
            var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            DrawString(font, new Vector2(vs.X / 2 - sz.X / 2, y), text, HorizontalAlignment.Left, -1, size, col);
        }
        bool pad = G.Main?.UsingPad ?? false;
        string ok = pad ? "A" : "ENTER";
        string skipKey = pad ? "X" : "X";
        string back = pad ? "B / BACK" : "ESC";

        if (_mode == Mode.PearlIntro)
        {
            Center("AN OBSIDIAN PEARL", vs.Y * 0.3f, 40, Meta.PearlTree.Color);
            Center("Pearls power THE PATH: ranks that shape how you grow during a run.", vs.Y * 0.3f + 50, 16, Colors.White);
            Center("Buy a rank with embers, then spend a pearl on it to bring it to life.", vs.Y * 0.3f + 76, 16, Colors.White);
            Center($"{ok} to continue", vs.Y * 0.3f + 130, 18, new Color(1, 1, 1, 0.6f + 0.4f * MathF.Sin(_t * 4)));
            return;
        }

        // header
        string title = _mode == Mode.PotionTutorial ? "A REAGENT!" : "UPGRADE TREES";
        Center(title, 70, 38, new Color(1f, 0.9f, 0.6f));
        string res = string.Join("    ", Meta.Trees.Where(Meta.Visible).Select(t => $"{t.ResourcePlural}: {Meta.HeldOf(t)}  ({Meta.FoundOf(t)}/{t.MaxResource} found)"));
        Center($"Embers: {Meta.Embers}    {res}", 104, 15, new Color(1f, 0.75f, 0.45f));
        float y = 140;
        if (_mode == Mode.PotionTutorial)
        {
            string[] lines = _step == 0
                ? new[] { "Reagents power the Potion tree. Every rank is bought with embers, then brought to life by spending a reagent on it.", "Your first rank is free: choose one." }
                : new[] { "Now spend your reagent on it to make it take effect." };
            foreach (var l in lines) { Center(l, y, 15, Colors.White); y += 22; }
            y += 10;
        }

        // the rows
        _rows.Clear();
        var rows = Rows().ToList();
        _sel = Math.Clamp(_sel, 0, Math.Max(0, rows.Count - 1));
        MetaTree last = null;
        float rw = 760, x0 = vs.X / 2 - rw / 2;
        for (int k = 0; k < rows.Count; k++)
        {
            var (tree, branch, name) = rows[k];
            if (tree != last)
            {
                last = tree;
                y += 8;
                DrawString(font, new Vector2(x0, y + 14), $"{tree.Name}  ·  powered by {tree.ResourcePlural}", HorizontalAlignment.Left, -1, 17, tree.Color);
                y += 26;
            }
            var r = new Rect2(x0, y, rw, 34);
            _rows.Add((tree, branch, name, r));
            bool sel = k == _sel;
            DrawRect(r, sel ? new Color(tree.Color, 0.18f) : new Color(1, 1, 1, 0.04f));
            if (sel) DrawRect(r, tree.Color, false, 2);
            DrawString(font, r.Position + new Vector2(14, 22), name, HorizontalAlignment.Left, -1, 15, Colors.White);
            var tiers = tree.Branch(branch).ToList();
            for (int j = 0; j < tiers.Count; j++)
            {
                var c = r.Position + new Vector2(230 + j * 34, 17);
                var tr = tiers[j];
                bool active = Meta.Active.Contains(tr.Id), bought = Meta.Bought.Contains(tr.Id);
                DrawCircle(c, 11, new Color(0, 0, 0, 0.5f));
                if (active) { DrawCircle(c, 10, tree.Color); DrawCircle(c + new Vector2(-3, -3), 3, new Color(1, 1, 1, 0.6f)); }
                else if (bought) { DrawArc(c, 10, 0, Mathf.Tau, 20, tree.Color, 2); DrawCircle(c, 4 + MathF.Sin(_t * 5) * 1.2f, new Color(tree.Color, 0.6f)); }
                else { DrawArc(c, 10, 0, Mathf.Tau, 20, new Color(1, 1, 1, 0.3f), 1.5f); DrawString(font, c + new Vector2(-4, 5), tr.Cost.ToString(), HorizontalAlignment.Left, -1, 12, new Color(1f, 0.75f, 0.45f, 0.8f)); }
            }
            // what the next step here does
            var next = Meta.NextToActivate(tree, branch);
            string what;
            if (_mode == Mode.PotionTutorial && _step == 0) what = tiers[0].Desc;
            else if (next != null) what = $"{next.Name}: {next.Desc}  [activate]";
            else
            {
                var buy = Meta.NextToBuy(tree, branch);
                what = buy != null ? $"{buy.Name}: {buy.Desc}  [{buy.Cost} ember{(buy.Cost > 1 ? "s" : "")}]" : "Complete.";
            }
            DrawString(font, r.Position + new Vector2(230 + tiers.Count * 34 + 8, 22), what, HorizontalAlignment.Left, rw - (230 + tiers.Count * 34 + 16), 12, new Color(1, 1, 1, sel ? 0.95f : 0.6f));
            y += 40;
        }
        if (_mode == Mode.Browse && Meta.Visible(Meta.PearlTree)) { DrawString(font, new Vector2(x0 + 14, y + 16), "Other unlocks...  (to be discovered)", HorizontalAlignment.Left, -1, 13, new Color(1, 1, 1, 0.35f)); y += 30; }

        if (_flashT > 0) Center(_flash, y + 30, 16, new Color(1f, 0.95f, 0.7f, Math.Min(1, _flashT)));
        string help = _mode == Mode.PotionTutorial
            ? (_step == 0 ? $"UP / DOWN choose  ·  {ok} unlock it free  ·  {skipKey} skip" : $"{ok} spend the reagent  ·  {skipKey} keep it for later")
            : $"UP / DOWN choose  ·  {ok} buy or activate  ·  {back} close";
        Center(help, vs.Y - 40, 15, new Color(1, 1, 1, 0.6f + 0.3f * MathF.Sin(_t * 3)));
    }
}
