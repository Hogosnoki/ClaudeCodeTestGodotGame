using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Class perks, at the camp: the chosen hero's permanent upgrades. Buy the next rank of one with
/// embers; choose which of them the hero brings on the next run (a few slots). What's bought is
/// kept for good.
/// </summary>
public partial class PerkMenu : Control
{
    public event Action Closed;
    private HeroKind _hero;
    private int _sel;
    private float _t, _openedAt, _flashT;
    private string _flash = "";
    private readonly List<(ClassPerk perk, Rect2 rect)> _rows = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public void Open(HeroKind hero)
    {
        _hero = hero;
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

    private void Say(string s) { _flash = s; _flashT = 2.4f; }
    private List<ClassPerk> Perks => ClassPerks.For(_hero).ToList();

    /// <summary>Buys the next rank (bringing it along if there's a slot); once complete, brings it or leaves it.</summary>
    private void Act()
    {
        var perks = Perks;
        if (_sel < 0 || _sel >= perks.Count) return;
        var perk = perks[_sel];
        int rank = Meta.PerkRank(perk.Id);
        if (rank >= perk.MaxRank) { Equip(perk); return; }
        int cost = perk.Costs[rank];
        if (Meta.PerkBuy(perk))
        {
            G.Sfx.Play("chest", null, -4);
            Say(rank == 0 ? $"{perk.Name} is yours." + (Meta.PerkEquipped(perk.Id) ? "" : " (Your slots are full: swap it in with X.)") : $"{perk.Name} grows stronger.");
        }
        else { G.Sfx.Play("ui", null, -6, 0, 0.6f); Say($"{perk.Name} costs {cost} ember{(cost > 1 ? "s" : "")}."); }
    }

    private void Equip(ClassPerk perk)
    {
        if (Meta.PerkRank(perk.Id) <= 0) { Say("Buy it first."); return; }
        if (Meta.PerkToggle(perk)) { G.Sfx.Play("ui", null, -4); Say(Meta.PerkEquipped(perk.Id) ? $"{perk.Name} comes along." : $"{perk.Name} stays at the camp."); }
        else { G.Sfx.Play("ui", null, -6, 0, 0.6f); Say($"All {Tune.Perks.Slots} slots are in use: leave one behind first."); }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _flashT -= (float)delta;
        if (!Visible) return;
        QueueRedraw();
        if (_t - _openedAt < 0.25f) return;
        int n = Perks.Count;
        if (Input.IsActionJustPressed("move_up")) { _sel = (_sel + n - 1) % Math.Max(1, n); G.Sfx.Play("ui", null, -8); }
        if (Input.IsActionJustPressed("move_down")) { _sel = (_sel + 1) % Math.Max(1, n); G.Sfx.Play("ui", null, -8); }
        if (Input.IsActionJustPressed("confirm")) Act();
        else if (Input.IsActionJustPressed("skip")) { var p = Perks; if (_sel < p.Count) Equip(p[_sel]); }
        else if (Input.IsActionJustPressed("pause") || Input.IsActionJustPressed("perks") || Input.IsActionJustPressed("dodge")) Close();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
            for (int k = 0; k < _rows.Count; k++) if (_rows[k].rect.HasPoint(mm.Position)) _sel = k;
        if (e is InputEventMouseButton { Pressed: true } mb)
            for (int k = 0; k < _rows.Count; k++)
                if (_rows[k].rect.HasPoint(mb.Position))
                {
                    _sel = k;
                    if (mb.ButtonIndex == MouseButton.Left) Act(); else if (mb.ButtonIndex == MouseButton.Right) Equip(_rows[k].perk);
                    AcceptEvent();
                    return;
                }
    }

    public override void _Draw()
    {
        var vs = Size;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0.03f, 0.02f, 0.04f, 0.95f));
        void Center(string text, float y, int size, Color col)
        {
            var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            DrawString(font, new Vector2(vs.X / 2 - sz.X / 2, y), text, HorizontalAlignment.Left, -1, size, col);
        }
        var accent = Hud.HeroColor(_hero);
        bool pad = G.Main?.UsingPad ?? false;
        string ok = pad ? "A" : "ENTER", eq = "X", back = pad ? "B" : "ESC";
        var (heroName, _) = ScreenOverlay.HeroInfo(_hero);
        Center($"{heroName}  ·  CLASS PERKS", 70, 36, accent.Lightened(0.3f));
        int used = ClassPerks.EquippedCount(_hero);
        Center($"Embers: {Meta.Embers}     Brought on the next run: {used} / {Tune.Perks.Slots}", 104, 15, new Color(1f, 0.75f, 0.45f));
        Center("Buy ranks with embers; they're yours for good. Choose which ones come along.", 128, 13, new Color(1, 1, 1, 0.55f));

        _rows.Clear();
        var perks = Perks;
        _sel = Math.Clamp(_sel, 0, Math.Max(0, perks.Count - 1));
        float rw = Math.Min(900, vs.X - 80), x0 = vs.X / 2 - rw / 2, y = 156;
        for (int k = 0; k < perks.Count; k++)
        {
            var perk = perks[k];
            int rank = Meta.PerkRank(perk.Id);
            bool equipped = rank > 0 && Meta.PerkEquipped(perk.Id);
            var r = new Rect2(x0, y, rw, 52);
            _rows.Add((perk, r));
            bool sel = k == _sel;
            DrawRect(r, sel ? new Color(accent, 0.2f) : new Color(1, 1, 1, 0.045f));
            if (equipped) DrawRect(new Rect2(r.Position, new Vector2(4, r.Size.Y)), accent);
            if (sel) DrawRect(r, accent, false, 2);
            DrawString(font, r.Position + new Vector2(16, 22), perk.Name, HorizontalAlignment.Left, -1, 17, rank > 0 ? Colors.White : new Color(1, 1, 1, 0.75f));
            // rank pips
            for (int j = 0; j < perk.MaxRank; j++)
            {
                var c = r.Position + new Vector2(16 + j * 22 + 8, 40);
                DrawCircle(c, 8, new Color(0, 0, 0, 0.5f));
                if (j < rank) DrawCircle(c, 7, accent);
                else { DrawArc(c, 7, 0, Mathf.Tau, 16, new Color(1, 1, 1, 0.35f), 1.5f); if (j == rank) DrawString(font, c + new Vector2(-3, 4), perk.Costs[j].ToString(), HorizontalAlignment.Left, -1, 10, new Color(1f, 0.75f, 0.45f, 0.9f)); }
            }
            // what it does now (and what the next rank adds)
            int shown = Math.Clamp(rank == 0 ? 1 : rank, 1, perk.MaxRank);
            string what = rank == 0 ? $"{perk.Ranks[0]}" : perk.Ranks[Math.Min(rank, perk.MaxRank) - 1];
            string next = rank > 0 && rank < perk.MaxRank ? $"   Next ({perk.Costs[rank]} ember{(perk.Costs[rank] > 1 ? "s" : "")}): {perk.Ranks[rank]}" : "";
            float dx = 300;
            DrawString(font, r.Position + new Vector2(dx, 21), what, HorizontalAlignment.Left, rw - dx - 130, 13, new Color(1, 1, 1, sel ? 0.95f : 0.7f));
            if (next != "") DrawString(font, r.Position + new Vector2(dx, 40), next, HorizontalAlignment.Left, rw - dx - 130, 12, new Color(1f, 0.85f, 0.55f, sel ? 0.9f : 0.5f));
            string tag = rank <= 0 ? $"{perk.Costs[0]} embers" : equipped ? "BROUGHT" : "at camp";
            var col = rank <= 0 ? new Color(1f, 0.75f, 0.45f, 0.85f) : equipped ? accent.Lightened(0.4f) : new Color(1, 1, 1, 0.4f);
            var ts = font.GetStringSize(tag, HorizontalAlignment.Left, -1, 13);
            DrawString(font, r.Position + new Vector2(rw - ts.X - 14, 22), tag, HorizontalAlignment.Left, -1, 13, col);
            y += 58;
        }
        if (_flashT > 0) Center(_flash, y + 26, 16, new Color(1f, 0.95f, 0.7f, Math.Min(1, _flashT)));
        Center($"UP / DOWN choose  ·  {ok} buy or strengthen  ·  {eq} bring it or leave it  ·  {back} close", vs.Y - 40, 15, new Color(1, 1, 1, 0.6f + 0.3f * MathF.Sin(_t * 3)));
    }
}
