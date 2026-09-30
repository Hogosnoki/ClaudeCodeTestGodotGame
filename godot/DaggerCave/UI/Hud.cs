using System.Collections.Generic;
using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>In-game heads-up display: health, XP, breath, ability charges, boss bar, minimap.</summary>
public partial class Hud : Control
{
    private Image _mapImg;
    private ImageTexture _mapTex;
    private bool[] _revealed;
    private float _revealT, _t;
    public float HintTime = 14f;
    public string Banner = "";
    public float BannerT;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ResetMap(CaveData cave)
    {
        _mapImg = Image.CreateEmpty(cave.W, cave.H, false, Image.Format.Rgba8);
        _mapImg.Fill(new Color(0, 0, 0, 0));
        _mapTex = ImageTexture.CreateFromImage(_mapImg);
        _revealed = new bool[cave.W * cave.H];
    }

    public void ShowBanner(string text, float time = 2.5f) { Banner = text; BannerT = time; }

    /// <summary>A smaller line under the depth (who joined or left, a tip).</summary>
    public string Notice = "";
    public float NoticeT;
    public void ShowNotice(string text, float time = 4f) { Notice = text; NoticeT = time; }

    /// <summary>Each hero's colour (their card on the title, their marker online).</summary>
    public static Color HeroColor(HeroKind k) => k switch
    {
        HeroKind.Warden => new Color(0.45f, 0.7f, 1f),
        HeroKind.Vitalist => new Color(0.5f, 1f, 0.45f),
        HeroKind.Elementalist => new Color(0.8f, 0.58f, 1f),
        HeroKind.Rogue => new Color(1f, 0.84f, 0.32f),
        HeroKind.Aegis => new Color(0.4f, 0.95f, 0.85f),
        _ => new Color(0.95f, 0.45f, 0.35f),
    };

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        BannerT -= dt;
        NoticeT -= dt;
        if (!GetTree().Paused) HintTime -= dt;
        _revealT -= dt;
        if (_revealT <= 0 && G.Player != null && G.Cave != null && _mapImg != null) { _revealT = 0.2f; Reveal(); }
        QueueRedraw();
    }

    private void Reveal()
    {
        var cave = G.Cave;
        var pc = G.Player.GlobalPosition / CaveData.Cell;
        int r = 17;
        bool changed = false;
        int ci = (int)pc.X, cj = (int)pc.Y;
        for (int j = cj - r; j <= cj + r; j++)
            for (int i = ci - r; i <= ci + r; i++)
            {
                if (i < 0 || j < 0 || i >= cave.W || j >= cave.H) continue;
                if ((i - ci) * (i - ci) + (j - cj) * (j - cj) > r * r) continue;
                int k = j * cave.W + i;
                if (_revealed[k]) continue;
                _revealed[k] = true; changed = true;
                bool open = cave.CellOpen(i, j);
                Color c;
                if (open) c = (j + 0.5f) * CaveData.Cell > cave.WaterY ? (cave.Liquid == Liquid.Lava ? new Color(0.9f, 0.35f, 0.1f, 0.85f) : new Color(0.2f, 0.45f, 0.75f, 0.85f)) : new Color(0.62f, 0.58f, 0.52f, 0.85f);
                else
                {
                    bool edge = cave.CellOpen(i + 1, j) || cave.CellOpen(i - 1, j) || cave.CellOpen(i, j + 1) || cave.CellOpen(i, j - 1);
                    c = edge ? new Color(0.2f, 0.17f, 0.16f, 0.9f) : new Color(0, 0, 0, 0);
                }
                _mapImg.SetPixel(i, j, c);
            }
        if (changed) _mapTex.Update(_mapImg);
    }

    public override void _Draw()
    {
        var p = G.Player;
        if (p == null || G.Cave == null) return;
        var font = ThemeDB.FallbackFont;
        var vs = GetViewportRect().Size;

        // --- HP ---
        var hpPos = new Vector2(20, 18);
        float hpW = 200 + p.Stats.MaxHp * 0.3f;
        DrawRect(new Rect2(hpPos - new Vector2(3, 3), new Vector2(hpW + 6, 22)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(hpPos, new Vector2(hpW, 16)), new Color(0.25f, 0.05f, 0.06f));
        float hf = Math.Clamp(p.Hp / p.Stats.MaxHp, 0, 1);
        DrawRect(new Rect2(hpPos, new Vector2(hpW * hf, 16)), hf < 0.3f && (int)(_t * 4) % 2 == 0 ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.18f, 0.22f));
        DrawRect(new Rect2(hpPos, new Vector2(hpW * hf, 5)), new Color(1, 1, 1, 0.15f));
        DrawString(font, hpPos + new Vector2(6, 13), $"{Mathf.CeilToInt(p.Hp)} / {Mathf.RoundToInt(p.Stats.MaxHp)}", HorizontalAlignment.Left, -1, 13, Colors.White);

        // --- XP ---
        var xpPos = hpPos + new Vector2(0, 26);
        float xpW = hpW;
        DrawRect(new Rect2(xpPos - new Vector2(3, 3), new Vector2(xpW + 6, 12)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(xpPos, new Vector2(xpW * Math.Clamp(p.Xp / (float)p.XpToNext, 0, 1), 6)), new Color(0.4f, 1f, 0.7f));
        DrawString(font, xpPos + new Vector2(xpW + 10, 8), $"Lv {p.Level}", HorizontalAlignment.Left, -1, 14, new Color(0.6f, 1f, 0.8f));
        if (p.PendingMilestones > 0)
        {
            // (a milestone waits for a quiet moment: it never opens by itself)
            float pulse = 0.7f + 0.3f * MathF.Sin(Time.GetTicksMsec() / 1000f * 4f);
            DrawString(font, xpPos + new Vector2(xpW + 10, 26), $"MILESTONE READY{(p.PendingMilestones > 1 ? $" x{p.PendingMilestones}" : "")}  ·  press {Controls.Name("milestone")}", HorizontalAlignment.Left, -1, 12, new Color(1f, 0.85f, 0.35f, pulse));
        }

        // --- Breath ---
        if (p.Breath < p.Stats.BreathMax - 0.05f || p.HeadUnder)
        {
            var bp = xpPos + new Vector2(0, 16);
            int bubbles = Mathf.CeilToInt(p.Stats.BreathMax / 2f);
            float per = p.Stats.BreathMax / bubbles;
            for (int k = 0; k < bubbles; k++)
            {
                float fill = Math.Clamp((p.Breath - k * per) / per, 0, 1);
                var c = bp + new Vector2(8 + k * 18, 8);
                DrawArc(c, 7, 0, Mathf.Tau, 16, new Color(0.7f, 0.9f, 1f, 0.9f), 1.5f);
                if (fill > 0) DrawCircle(c, 6 * fill, new Color(0.5f, 0.8f, 1f, 0.7f));
            }
            if (p.Breath <= 0) DrawString(font, bp + new Vector2(8 + bubbles * 18, 13), "DROWNING!", HorizontalAlignment.Left, -1, 13, new Color(1f, 0.4f, 0.4f));
        }

        // --- Ability gauges (bottom-left) ---
        var ab = new Vector2(24, vs.Y - 44);
        switch (p.Stats.Hero)
        {
            case HeroKind.Warden: DrawWardenGauges(font, p, ab); break;
            case HeroKind.Vitalist: DrawVitalistGauges(font, p, ab); break;
            case HeroKind.Elementalist: DrawElementalistGauges(font, p, ab); break;
            case HeroKind.Rogue: DrawRogueGauges(font, p, ab); break;
            case HeroKind.Aegis: DrawAegisGauges(font, p, ab); break;
            default: DrawSwordsmanGauges(font, p, ab); break;
        }

        // --- Potions (under the bars) ---
        {
            var pp = xpPos + new Vector2(xpW + 60, -18);
            for (int k = 0; k < Meta.MaxPotions; k++)
                PotionPickup.DrawFlask(this, pp + new Vector2(k * 18, 6), 1.35f, 1f, k >= p.Potions);
            DrawString(font, pp + new Vector2(-8, 28), Controls.Name("potion"), HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.5f));
            if (p.Mending) DrawString(font, pp + new Vector2(6, 28), "mending", HorizontalAlignment.Left, -1, 10, new Color(1f, 0.6f, 0.7f, 0.7f + 0.3f * MathF.Sin(_t * 6)));
            // the keys carried, beside the potions
            for (int k = 0; k < p.Keys; k++)
                KeyPickup.DrawKey(this, pp + new Vector2(Meta.MaxPotions * 18 + 16 + k * 22, 6), 1.4f, 1f);
        }

        // --- Depth / biome / kills ---
        string where = $"DEPTH {G.Depth}  ·  {G.Biome?.Name.ToUpperInvariant() ?? ""}  ·  {p.Kills} kills";
        var wsz = font.GetStringSize(where, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2(vs.X / 2 - wsz.X / 2, 22), where, HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.75f));

        // --- Boss bar ---
        var boss = G.Main.ActiveBoss;
        if (boss != null && IsInstanceValid(boss) && !boss.Dead)
        {
            float bw = Math.Min(560, vs.X - 360);
            var bp = new Vector2(vs.X / 2 - bw / 2, vs.Y - 46);
            DrawString(font, bp + new Vector2(0, -6), (boss.Title != "" ? boss.Title : boss.DisplayName).ToUpperInvariant(), HorizontalAlignment.Left, -1, 15, new Color(1f, 0.85f, 0.7f));
            DrawRect(new Rect2(bp - new Vector2(3, 3), new Vector2(bw + 6, 20)), new Color(0, 0, 0, 0.7f));
            DrawRect(new Rect2(bp, new Vector2(bw * Math.Clamp(boss.Hp / boss.MaxHp, 0, 1), 14)), new Color(0.8f, 0.25f, 0.15f));
        }

        // --- Minimap ---
        if (_mapTex != null)
        {
            float mw = 260, mh = mw * G.Cave.H / G.Cave.W;
            var mp = new Vector2(vs.X - mw - 16, 16);
            DrawRect(new Rect2(mp - new Vector2(4, 4), new Vector2(mw + 8, mh + 8)), new Color(0, 0, 0, 0.55f));
            DrawTextureRect(_mapTex, new Rect2(mp, new Vector2(mw, mh)), false);
            float sx = mw / G.Cave.SizePx.X, sy = mh / G.Cave.SizePx.Y;
            if (G.Cave.Liquid != Liquid.None)
                DrawLine(mp + new Vector2(0, G.Cave.WaterY * sy), mp + new Vector2(mw, G.Cave.WaterY * sy), G.Cave.Liquid == Liquid.Lava ? new Color(1f, 0.5f, 0.2f, 0.4f) : new Color(0.4f, 0.7f, 1f, 0.35f), 1f);
            // (online, the others too, in their heroes' colours)
            foreach (var h in G.Players)
                if (h != p && IsInstanceValid(h))
                    DrawCircle(mp + new Vector2(h.GlobalPosition.X * sx, h.GlobalPosition.Y * sy), 2.6f, h.Dead ? new Color(0.5f, 0.5f, 0.5f) : HeroColor(h.Hero));
            var pp = mp + new Vector2(p.GlobalPosition.X * sx, p.GlobalPosition.Y * sy);
            DrawCircle(pp, 3, new Color(1f, 0.95f, 0.4f));
            if (G.Cave.Boss != null)
            {
                var b = mp + new Vector2(G.Cave.Boss.Center.X * sx, G.Cave.Boss.Center.Y * sy);
                DrawArc(b, 4 + MathF.Sin(_t * 4), 0, Mathf.Tau, 12, new Color(1f, 0.25f, 0.2f), 2f);
                DrawString(font, b + new Vector2(6, 4), "EXIT", HorizontalAlignment.Left, -1, 9, new Color(1f, 0.4f, 0.35f));
            }
            // the vault: marked from the start (a gold lock; faint once it's open)
            foreach (var (vault, vgate) in new[] { (G.Cave.Vault, G.Main.Gate), (G.Cave.ExtraVault, G.Main.Gate2) })
            {
                if (vault == null) continue;
                bool open = vgate != null && IsInstanceValid(vgate) && vgate.Opened;
                var vp = mp + new Vector2(vault.Center.X * sx, vault.Center.Y * sy);
                var gold = new Color(1f, 0.8f, 0.35f, open ? 0.45f : 0.95f);
                DrawRect(new Rect2(vp - new Vector2(3.5f, 2f), new Vector2(7, 6)), gold);
                DrawArc(vp + new Vector2(0, -2f), 2.4f, Mathf.Pi, Mathf.Tau, 8, gold, 1.4f);
                DrawString(font, vp + new Vector2(6, 4), open ? "VAULT (OPEN)" : "VAULT", HorizontalAlignment.Left, -1, 9, gold);
            }
        }

        if (Net.InRun) DrawOnline(font, vs, p, xpPos + new Vector2(0, p.Breath < p.Stats.BreathMax - 0.05f || p.HeadUnder ? 44 : 24));

        // --- a chest at your feet opens with the interact button ---
        if (!p.Dead && Chest.At(p.GlobalPosition) is Chest chest)
        {
            var at = chest.GetGlobalTransformWithCanvas().Origin + new Vector2(0, -46);
            string open = $"{Controls.Name("interact")}  open";
            var osz = font.GetStringSize(open, HorizontalAlignment.Left, -1, 14);
            DrawRect(new Rect2(at - new Vector2(osz.X / 2 + 8, 16), new Vector2(osz.X + 16, 22)), new Color(0, 0, 0, 0.6f));
            DrawString(font, at - new Vector2(osz.X / 2, 0), open, HorizontalAlignment.Left, -1, 14, new Color(1f, 0.88f, 0.5f, 0.8f + 0.2f * MathF.Sin(_t * 5)));
        }
        // --- a vault's gate: a key opens it ---
        if (!p.Dead && VaultGate.At(p.GlobalPosition) is VaultGate gate)
        {
            // (over the top of the gate, on screen)
            var at = gate.GetGlobalTransformWithCanvas() * new Vector2(0, -gate.Height) + new Vector2(0, -18);
            bool key = p.Keys > 0;
            string say = key ? $"{Controls.Name("interact")}  open the vault (a key)" : "LOCKED: it takes a key";
            var gsz = font.GetStringSize(say, HorizontalAlignment.Left, -1, 14);
            DrawRect(new Rect2(at - new Vector2(gsz.X / 2 + 8, 16), new Vector2(gsz.X + 16, 22)), new Color(0, 0, 0, 0.6f));
            DrawString(font, at - new Vector2(gsz.X / 2, 0), say, HorizontalAlignment.Left, -1, 14,
                key ? new Color(1f, 0.88f, 0.5f, 0.8f + 0.2f * MathF.Sin(_t * 5)) : new Color(1f, 0.7f, 0.55f, 0.85f));
        }
        if (NoticeT > 0 && Notice != "")
        {
            var nsz = font.GetStringSize(Notice, HorizontalAlignment.Left, -1, 13);
            DrawString(font, new Vector2(vs.X / 2 - nsz.X / 2, 58), Notice, HorizontalAlignment.Left, -1, 13, new Color(1f, 0.92f, 0.75f, Math.Clamp(NoticeT, 0, 1) * 0.9f));
        }

        // --- Brain training panel (F9) ---
        if (Brains.Training) DrawBrainPanel(font, vs);

        // --- Hints / banner ---
        if (HintTime > 0)
        {
            float a = Math.Clamp(HintTime / 2f, 0, 1) * 0.85f;
            bool pad = G.Main.UsingPad;
            string move = pad ? $"Left stick move · {Controls.Name("jump")} jump · hold up/down to swim · {Controls.Name("potion")} drink a potion"
                              : $"{Controls.Name("move_left")}/{Controls.Name("move_right")} move · {Controls.Name("jump")} jump · {Controls.Name("move_up")}/{Controls.Name("move_down")} swim up/down · {Controls.Name("potion")} drink a potion";
            string a1 = Controls.Name("ability"), a2 = Controls.Name("ability2"), atk = Controls.Name("attack"), dg = Controls.Name("dodge");
            string pause = Controls.Name("pause") + " pause (and settings)";
            string[] lines = p.Stats.Hero switch
            {
                HeroKind.Warden => new[]
                {
                    move,
                    $"{atk} swing (hold to keep swinging) · {a1} Guarded Charge (breaks off attacks it meets) · {a2} shield bash (stuns all in front) · hold {dg}{(pad ? " or the right stick" : "")} to raise the shield",
                    "The shield stops every blow, and what breaks it is stunned; raise it just before the hit for a perfect block · " + pause,
                },
                HeroKind.Aegis => new[]
                {
                    move,
                    $"{atk} ward bolt (hold to keep casting{(pad ? ", or push the right stick" : ", aim with the mouse")}) · {a1} barrier · {a2} share a burden · {dg} bubble · barrier, burden and bubble go to the friend you aim at (or to you)",
                    "The bolt hurts, weakens what it bursts on, and mends you · the bubble lets its wearer breathe under water · " + pause,
                },
                HeroKind.Rogue => new[]
                {
                    move,
                    $"{atk} jab (hold to keep jabbing{(pad ? ", or push the right stick" : ", aim with the mouse")}) · {a1} throw a dagger · {a2} recall them · {dg} vanish · kick off walls",
                    "Thrown daggers stick in what they hit and stay until recalled (or jab with none left); coming out, they cut again · " + pause,
                },
                HeroKind.Elementalist => new[]
                {
                    move,
                    $"{atk} {(p.Stats.Frostbolt ? "frostbolt" : "firebolt")} (hold to keep casting{(pad ? ", or push the right stick" : ", aim with the mouse")}) · {dg} updraft (lifts everyone in it) · {a1} {(p.Stats.Firestorm ? "firestorm" : "blizzard")} · {a2} snap",
                    "Spells cost alimus, which comes back by itself · a snap shatters every frozen creature in view · " + pause,
                },
                HeroKind.Vitalist => new[]
                {
                    move,
                    $"{atk} drain (hold to keep draining{(pad ? ", or push the right stick" : ", aim with the mouse")}) · {dg} hex · {a1} heal · {a2} rupture (a full reserve)",
                    "Draining life fills your vital force · " + pause + "    —    find and slay the exit's guardian",
                },
                _ => new[]
                {
                    move,
                    $"{atk} swing (hold to keep swinging{(pad ? ", or push the right stick" : ", aim with the mouse")}) · {a1} charge your blade · {a2} heaving swing (on your feet) · {dg} dodge",
                    "A charged swing hits harder and saps what it cuts · " + pause + "    —    find and slay the exit's guardian",
                },
            };
            for (int k = 0; k < lines.Length; k++)
                DrawString(font, new Vector2(vs.X / 2 - 230, vs.Y - 110 + k * 20), lines[k], HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, a));
        }
        if (BannerT > 0)
        {
            float a = Math.Clamp(BannerT, 0, 1);
            int size = 30;
            var sz = font.GetStringSize(Banner, HorizontalAlignment.Left, -1, size);
            var pos = new Vector2(vs.X / 2 - sz.X / 2, vs.Y * 0.3f);
            DrawString(font, pos + new Vector2(2, 2), Banner, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, a * 0.8f));
            DrawString(font, pos, Banner, HorizontalAlignment.Left, -1, size, new Color(1f, 0.9f, 0.7f, a));
        }
    }

    /// <summary>
    /// Online: the others' health (under your own bars), their names over their heroes, bringing a
    /// fallen friend back, and who is waiting at an exit.
    /// </summary>
    private void DrawOnline(Font font, Vector2 vs, Player me, Vector2 at)
    {
        foreach (var peer in Net.Peers.Values)
        {
            if (peer.Id == Net.Me) continue;
            var h = peer.Avatar;
            bool here = h != null && IsInstanceValid(h);
            var col = HeroColor(peer.Hero);
            DrawCircle(at + new Vector2(5, 7), 4.5f, col);
            DrawString(font, at + new Vector2(14, 12), peer.Name, HorizontalAlignment.Left, 96, 12, new Color(1, 1, 1, 0.85f));
            var bar = at + new Vector2(114, 3);
            DrawRect(new Rect2(bar - new Vector2(1, 1), new Vector2(92, 10)), new Color(0, 0, 0, 0.6f));
            float f = here ? Math.Clamp(h.Hp / Math.Max(1f, h.Stats.MaxHp), 0, 1) : 0;
            DrawRect(new Rect2(bar, new Vector2(90 * f, 8)), new Color(0.85f, 0.18f, 0.22f));
            string state = !here ? "" : h.Dead ? "DOWN" : peer.AtExit ? "at the exit" : h.Choosing ? "choosing" : "";
            if (state != "") DrawString(font, bar + new Vector2(98, 9), state, HorizontalAlignment.Left, -1, 11, h.Dead ? new Color(1f, 0.45f, 0.4f) : new Color(1, 1, 1, 0.6f));
            at += new Vector2(0, 16);

            // their name over their hero (dim when far off)
            if (!here) continue;
            var sp = h.GetGlobalTransformWithCanvas().Origin + new Vector2(0, -44);
            if (sp.X < -50 || sp.Y < -50 || sp.X > vs.X + 50 || sp.Y > vs.Y + 50) continue;
            string tag = h.Dead ? $"{peer.Name}  ·  DOWN" : peer.Name;
            var tsz = font.GetStringSize(tag, HorizontalAlignment.Left, -1, 12);
            DrawString(font, sp - new Vector2(tsz.X / 2, 0) + new Vector2(1, 1), tag, HorizontalAlignment.Left, -1, 12, new Color(0, 0, 0, 0.7f));
            DrawString(font, sp - new Vector2(tsz.X / 2, 0), tag, HorizontalAlignment.Left, -1, 12, h.Dead ? new Color(1f, 0.55f, 0.5f) : col.Lightened(0.35f));
        }

        // bringing a fallen friend back
        var fallen = me.ReviveTarget;
        if (fallen != null && IsInstanceValid(fallen) && !me.Dead)
        {
            string name = fallen.NetName != "" ? fallen.NetName : "your friend";
            string line = me.ReviveProgress > 0 ? $"Bringing {name} back..." : $"Hold {Controls.Name("interact")} to bring {name} back";
            var lsz = font.GetStringSize(line, HorizontalAlignment.Left, -1, 16);
            var c = new Vector2(vs.X / 2, vs.Y * 0.62f);
            DrawString(font, c - new Vector2(lsz.X / 2, 0), line, HorizontalAlignment.Left, -1, 16, Player.HealColorLight);
            DrawRect(new Rect2(c + new Vector2(-80, 8), new Vector2(160, 8)), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(c + new Vector2(-79, 9), new Vector2(158 * Math.Clamp(me.ReviveProgress, 0, 1), 6)), Player.HealColor);
        }

        // going down together
        string wait = "";
        if (G.Main.WaitingAt != null)
        {
            var (here, of) = G.Main.ExitCount();
            wait = $"Waiting at the exit  ·  {here} of {of} here  ·  walk away to cancel";
        }
        else
        {
            var waiting = new List<string>();
            foreach (var peer in Net.Peers.Values) if (peer.Id != Net.Me && peer.AtExit) waiting.Add(peer.Name);
            if (waiting.Count > 0) wait = $"{string.Join(" and ", waiting)} {(waiting.Count > 1 ? "are" : "is")} waiting at an exit  ·  {Controls.Name("interact")} there to go down together";
        }
        if (wait != "")
        {
            var wsz = font.GetStringSize(wait, HorizontalAlignment.Left, -1, 14);
            DrawString(font, new Vector2(vs.X / 2 - wsz.X / 2, vs.Y - 132), wait, HorizontalAlignment.Left, -1, 14, new Color(0.85f, 0.75f, 1f, 0.75f + 0.25f * MathF.Sin(_t * 3)));
        }
        if (me.Dead)
        {
            string down = "You're down  ·  a friend can bring you back";
            var dsz = font.GetStringSize(down, HorizontalAlignment.Left, -1, 16);
            DrawString(font, new Vector2(vs.X / 2 - dsz.X / 2, vs.Y * 0.62f), down, HorizontalAlignment.Left, -1, 16, new Color(1f, 0.6f, 0.55f, 0.9f));
        }
    }

    private void DrawBrainPanel(Font font, Vector2 vs)
    {
        var brains = new List<Brain>(Brains.Loaded);
        brains.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        float w = 330, rowH = 15, h = 44 + Math.Max(1, brains.Count) * rowH;
        var o = new Vector2(vs.X - w - 12, 150);
        DrawRect(new Rect2(o, new Vector2(w, h)), new Color(0, 0, 0, 0.6f));
        DrawString(font, o + new Vector2(8, 16), $"TRAINING  (F9 off · F10 save)   {Brains.LastSaveText}", HorizontalAlignment.Left, w - 16, 11, new Color(1f, 0.85f, 0.35f));
        var dim = new Color(1, 1, 1, 0.55f);
        string[] heads = { "brain", "decisions", "teacher", "reward", "dealt" };
        float[] cols = { 8, 104, 170, 222, 282 };
        for (int c = 0; c < heads.Length; c++) DrawString(font, o + new Vector2(cols[c], 32), heads[c], HorizontalAlignment.Left, -1, 10, dim);
        if (brains.Count == 0) DrawString(font, o + new Vector2(8, 46), "(no creatures met yet)", HorizontalAlignment.Left, -1, 10, dim);
        for (int k = 0; k < brains.Count; k++)
        {
            var b = brains[k];
            bool locked = Brains.IsLocked(b.Name);
            var y = o.Y + 46 + k * rowH;
            var col = locked ? new Color(0.6f, 0.8f, 1f) : Colors.White;
            DrawString(font, new Vector2(o.X + 8, y), b.Name + (locked ? " (locked)" : ""), HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 104, y), b.Experience.ToString(), HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 170, y), $"{Math.Min(0.9f, b.TeacherWeight) * 100:0}%", HorizontalAlignment.Left, -1, 10, col);
            DrawString(font, new Vector2(o.X + 222, y), $"{b.AvgReward:+0.000;-0.000}", HorizontalAlignment.Left, -1, 10, b.AvgReward >= 0 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f));
            DrawString(font, new Vector2(o.X + 282, y), $"{b.AvgDealt:0.00}", HorizontalAlignment.Left, -1, 10, col);
        }
    }

    /// <summary>A square ability icon with its cooldown filling it from the top, and a label.</summary>
    private void AbilitySquare(Font font, Vector2 at, string label, float cooldownFrac, bool ready, Color col, Action<Vector2, Color> icon)
    {
        DrawRect(new Rect2(at, new Vector2(34, 34)), new Color(0, 0, 0, 0.55f));
        icon(at + new Vector2(17, 17), ready ? col : new Color(0.45f, 0.47f, 0.52f));
        if (cooldownFrac > 0) DrawRect(new Rect2(at + new Vector2(0, 34 * (1 - cooldownFrac)), new Vector2(34, 34 * cooldownFrac)), new Color(0, 0, 0, 0.55f));
        if (ready) DrawRect(new Rect2(at - new Vector2(1, 1), new Vector2(36, 36)), new Color(col, 0.35f + 0.25f * MathF.Sin(_t * 5)), false, 1.5f);
        DrawString(font, at + new Vector2(0, -6), label, HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
    }

    /// <summary>The key or button for an action, in small print under its square.</summary>
    private void KeyHint(Font font, Vector2 at, string action)
        => DrawString(font, at + new Vector2(0, 46), Controls.Name(action), HorizontalAlignment.Center, 34, 9, new Color(1, 1, 1, 0.4f));

    /// <summary>With Twin Reserve: a pip under the ability square for each use, lit when ready.</summary>
    private void UsePips(Vector2 at, Player p, Color col)
    {
        var cds = p.AbilityCooldowns;
        if (cds.Length < 2) return;
        for (int k = 0; k < cds.Length; k++)
        {
            var c = at + new Vector2(17 + (k - (cds.Length - 1) * 0.5f) * 10, 40);
            bool ready = cds[k] <= 0;
            DrawCircle(c, 3.2f, new Color(0, 0, 0, 0.6f));
            DrawCircle(c, 2.4f, ready ? col : new Color(0.4f, 0.42f, 0.46f));
        }
    }

    /// <summary>A round cooldown dial (dodges, the hex).</summary>
    private void Dial(Vector2 c, float frac, Color readyCol)
    {
        DrawCircle(c, 17, new Color(0, 0, 0, 0.55f));
        DrawArc(c, 12, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (1 - frac), 24, frac <= 0 ? readyCol : new Color(0.4f, 0.5f, 0.6f), 4f);
    }

    /// <summary>Dodge charges, the Charged Strike and the heaving swing.</summary>
    private void DrawSwordsmanGauges(Font font, Player p, Vector2 ab)
    {
        var dc = p.DodgeCooldowns;
        for (int k = 0; k < dc.Length; k++) Dial(ab + new Vector2(k * 40 + 17, 17), Math.Clamp(dc[k] / p.DodgeCooldownTotal, 0, 1), new Color(0.5f, 0.9f, 1f));
        DrawString(font, ab + new Vector2(0, -6), "DODGE", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        var cb = ab + new Vector2(dc.Length * 40 + 16, 0);
        bool charged = p.Charged > 0;
        var orange = new Color(1f, 0.55f, 0.25f);
        AbilitySquare(font, cb, charged ? "CHARGED" : "CHARGE", charged ? 0 : p.ChargeCooldownFrac, charged || p.ChargeCooldownFrac <= 0, orange, (c, col) =>
        {
            // a sword with a burning edge
            var d = new Vector2(1, -1).Normalized();
            var perp = new Vector2(-d.Y, d.X);
            DrawLine(c - d * 11, c - d * 5, new Color(0.55f, 0.35f, 0.2f), 3f);
            DrawLine(c - d * 5 + perp * 4, c - d * 5 - perp * 4, new Color(0.8f, 0.7f, 0.3f), 2f);
            DrawColoredPolygon(new[] { c - d * 4 + perp * 2.2f, c + d * 12, c - d * 4 - perp * 2.2f }, col);
            if (charged) DrawCircle(c + d * 3, 8 + MathF.Sin(_t * 9) * 1.5f, new Color(1f, 0.5f, 0.2f, 0.25f));
        });
        UsePips(cb, p, orange);
        KeyHint(font, cb, "ability");
        var hb = cb + new Vector2(50, 0);
        AbilitySquare(font, hb, "HEAVE", p.HeaveCooldownFrac, p.HeaveCooldownFrac <= 0, new Color(0.85f, 0.92f, 1f), (c, col) =>
        {
            // a sword coming down in a great arc
            DrawArc(c + new Vector2(-2, 4), 12, -Mathf.Pi * 0.95f, -Mathf.Pi * 0.05f, 12, new Color(col, 0.5f), 2f);
            var d = new Vector2(0.55f, 0.83f);
            var perp = new Vector2(-d.Y, d.X);
            DrawLine(c - d * 11 + new Vector2(6, -2), c - d * 5 + new Vector2(6, -2), new Color(0.55f, 0.35f, 0.2f), 3f);
            DrawColoredPolygon(new[] { c - d * 4 + perp * 2.2f + new Vector2(6, -2), c + d * 11 + new Vector2(6, -2), c - d * 4 - perp * 2.2f + new Vector2(6, -2) }, col);
        });
        KeyHint(font, hb, "ability2");
    }

    /// <summary>The Guarded Charge, the shield bash and the shield's strength bar.</summary>
    private void DrawWardenGauges(Font font, Player p, Vector2 ab)
    {
        var blue = new Color(0.55f, 0.85f, 1f);
        AbilitySquare(font, ab, "DASH", p.DashCooldownFrac, p.DashCooldownFrac <= 0, blue, (c, col) =>
        {
            // a kite shield with speed lines
            DrawColoredPolygon(new[] { c + new Vector2(-4, -9), c + new Vector2(7, -9), c + new Vector2(7, 1), c + new Vector2(1.5f, 10), c + new Vector2(-4, 1) }, col);
            for (int k = 0; k < 3; k++) DrawLine(c + new Vector2(-8, -5 + k * 5), c + new Vector2(-14, -5 + k * 5), new Color(col, 0.7f), 1.5f);
        });
        UsePips(ab, p, blue);
        KeyHint(font, ab, "ability");
        var bb = ab + new Vector2(50, 0);
        bool bashOk = p.BashCooldownFrac <= 0 && !p.ShieldBroken;
        AbilitySquare(font, bb, "BASH", p.BashCooldownFrac, bashOk, new Color(1f, 0.85f, 0.45f), (c, col) =>
        {
            // a shield meeting a burst
            DrawColoredPolygon(new[] { c + new Vector2(-9, -8), c + new Vector2(1, -8), c + new Vector2(1, 2), c + new Vector2(-4, 10), c + new Vector2(-9, 2) }, col);
            for (int k = 0; k < 5; k++)
            {
                var d = Vector2.Right.Rotated(-1.1f + k * 0.55f);
                DrawLine(c + new Vector2(5, 0) + d * 3, c + new Vector2(5, 0) + d * 9, col, 1.6f);
            }
        });
        KeyHint(font, bb, "ability2");

        // shield
        var sp = bb + new Vector2(52, 10);
        const float w = 130;
        float frac = Math.Clamp(p.ShieldHp / Math.Max(1f, p.Stats.ShieldMax), 0, 1);
        DrawRect(new Rect2(sp - new Vector2(2, 2), new Vector2(w + 4, 16)), new Color(0, 0, 0, 0.6f));
        var bar = p.ShieldBroken ? new Color(0.45f, 0.45f, 0.5f) : p.ShieldRaised ? new Color(0.6f, 0.85f, 1f) : new Color(0.35f, 0.6f, 0.95f);
        DrawRect(new Rect2(sp, new Vector2(w * frac, 12)), bar);
        // the bash's cost: a notch where one bash would leave the shield
        float notch = Tune.Warden.BashShieldShare;
        if (!p.ShieldBroken && frac > notch) DrawLine(sp + new Vector2(w * (frac - notch), 0), sp + new Vector2(w * (frac - notch), 12), new Color(1f, 0.85f, 0.45f, 0.6f), 1.5f);
        string label = p.ShieldBroken ? $"SHIELD BROKEN  {p.ShieldBrokenLeft:0.0}s" : $"SHIELD  {Mathf.CeilToInt(p.ShieldHp)} / {Mathf.RoundToInt(p.Stats.ShieldMax)}";
        DrawString(font, sp + new Vector2(0, -8), label, HorizontalAlignment.Left, -1, 10, p.ShieldBroken ? new Color(1f, 0.6f, 0.5f) : new Color(1, 1, 1, 0.6f));
    }

    /// <summary>Vital force, the hex, the heal and the rupture.</summary>
    private void DrawVitalistGauges(Font font, Player p, Vector2 ab)
    {
        var green = new Color(0.55f, 1f, 0.45f);
        Dial(ab + new Vector2(17, 17), p.HexCooldownFrac, green);
        DrawString(font, ab + new Vector2(0, -6), "HEX", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        var hb = ab + new Vector2(50, 0);
        bool affordable = p.VitalForce >= p.HealCost - 0.001f;
        AbilitySquare(font, hb, $"HEAL ({p.HealCost:0})", p.HealCooldownFrac, affordable && p.HealCooldownFrac <= 0, Player.HealColor, (c, col) =>
        {
            DrawRect(new Rect2(c - new Vector2(3, 10), new Vector2(6, 20)), col);
            DrawRect(new Rect2(c - new Vector2(10, 3), new Vector2(20, 6)), col);
        });
        UsePips(hb, p, Player.HealColor);
        KeyHint(font, hb, "ability");
        var rb = hb + new Vector2(50, 0);
        AbilitySquare(font, rb, $"RUPTURE ({p.RuptureCost:0})", p.RuptureCooldownFrac, p.RuptureReady, Player.LifeColorLight, (c, col) =>
        {
            // a heart bursting
            DrawCircle(c, 5, col);
            for (int k = 0; k < 8; k++)
            {
                var d = Vector2.Right.Rotated(k * Mathf.Tau / 8 + 0.2f);
                DrawLine(c + d * 7, c + d * (k % 2 == 0 ? 13 : 10), col, 2f);
            }
        });
        KeyHint(font, rb, "ability2");
        // the vital force reserve
        var bp = rb + new Vector2(52, 10);
        const float w = 150;
        float max = Math.Max(1f, p.Stats.VitalForceMax);
        float frac = Math.Clamp(p.VitalForce / max, 0, 1);
        DrawRect(new Rect2(bp - new Vector2(2, 2), new Vector2(w + 4, 16)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bp, new Vector2(w * frac, 12)), new Color(0.42f, 0.85f, 0.4f));
        DrawRect(new Rect2(bp, new Vector2(w * frac, 4)), new Color(0.9f, 0.3f, 0.35f, 0.5f));
        // tick marks: one heal's worth each; the rupture's cost in crimson
        for (float x = p.HealCost; x < max - 0.01f; x += p.HealCost)
            DrawLine(bp + new Vector2(w * x / max, 0), bp + new Vector2(w * x / max, 12), new Color(0, 0, 0, 0.45f), 1f);
        if (p.RuptureCost <= max) DrawLine(bp + new Vector2(w * p.RuptureCost / max, -3), bp + new Vector2(w * p.RuptureCost / max, 15), Player.LifeColor, 2f);
        DrawString(font, bp + new Vector2(0, -8), $"VITAL FORCE  {Mathf.FloorToInt(p.VitalForce + 0.001f)} / {Mathf.RoundToInt(max)}", HorizontalAlignment.Left, -1, 10, affordable ? new Color(0.75f, 1f, 0.7f, 0.8f) : new Color(1, 1, 1, 0.5f));
    }

    /// <summary>The vanish (a dial, with Twin Reserve's pips), the two daggers, and the recall (a square).</summary>
    private void DrawRogueGauges(Font font, Player p, Vector2 ab)
    {
        var gold = Hud.HeroColor(HeroKind.Rogue);
        var shadow = new Color(0.7f, 0.62f, 0.95f);
        Dial(ab + new Vector2(17, 17), p.Vanished ? 0f : p.AbilityCooldownFrac, shadow);
        if (p.Vanished)
        {
            // the time left in the shadows, as a shrinking ring
            DrawArc(ab + new Vector2(17, 17), 15, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (p.VanishLeft / p.VanishTotal), 24, new Color(shadow, 0.9f), 2f);
        }
        DrawString(font, ab + new Vector2(0, -6), p.Stats.SmokeBomb ? "SMOKE" : "VANISH", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        UsePips(ab, p, shadow);
        KeyHint(font, ab, "dodge");
        // the daggers: lit in hand, dark while thrown
        var db = ab + new Vector2(56, 0);
        DrawRect(new Rect2(db, new Vector2(46, 34)), new Color(0, 0, 0, 0.55f));
        for (int k = 0; k < 2; k++)
        {
            bool inHand = p.DaggerInHand(k);
            var c = db + new Vector2(14 + k * 18, 17);
            var col = inHand ? new Color(0.9f, 0.92f, 0.96f) : new Color(0.4f, 0.42f, 0.46f);
            DrawColoredPolygon(new[] { c + new Vector2(-2.5f, -3), c + new Vector2(0, -14), c + new Vector2(2.5f, -3) }, col);
            DrawLine(c + new Vector2(-5, -3), c + new Vector2(5, -3), inHand ? gold : col, 2f);
            DrawLine(c + new Vector2(0, -3), c + new Vector2(0, 8), inHand ? new Color(0.45f, 0.32f, 0.2f) : col, 2.5f);
        }
        float tcd = p.ThrowCooldownFrac;
        if (tcd > 0) DrawRect(new Rect2(db + new Vector2(0, 34 * (1 - tcd)), new Vector2(46, 34 * tcd)), new Color(0, 0, 0, 0.55f));
        DrawString(font, db + new Vector2(0, -6), "THROW", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        DrawString(font, db + new Vector2(0, 46), Controls.Name("ability"), HorizontalAlignment.Center, 46, 9, new Color(1, 1, 1, 0.4f));
        // recall: ready whenever a dagger is out
        var rb = db + new Vector2(60, 0);
        bool outs = p.DaggersInHand < 2;
        AbilitySquare(font, rb, p.Stats.Tether ? "TETHER" : "RECALL", 0, outs, gold, (c, col) =>
        {
            // an arrow curving home
            DrawArc(c, 9, Mathf.Pi * 0.2f, Mathf.Pi * 1.5f, 16, col, 2f);
            DrawColoredPolygon(new[] { c + new Vector2(9, 0), c + new Vector2(4, 6), c + new Vector2(13, 5) }, col);
        });
        KeyHint(font, rb, "ability2");
    }

    /// <summary>The Elementalist's colours: its alimus, and its fire and frost.</summary>
    public static readonly Color AlimusColor = new(0.72f, 0.55f, 1f);

    /// <summary>The updraft (a dial), the blizzard and the snap (squares), and the alimus reserve.</summary>
    private void DrawAegisGauges(Font font, Player p, Vector2 ab)
    {
        var teal = HeroColor(HeroKind.Aegis);
        var gold = new Color(1f, 0.88f, 0.5f);
        // the bubble (dodge button): a ring with a glint
        Dial(ab + new Vector2(17, 17), p.BubbleCooldownFrac, teal);
        DrawString(font, ab + new Vector2(0, -6), p.Stats.HostileBubble ? "WARD" : "BUBBLE", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        DrawArc(ab + new Vector2(17, 17), 8, 0, Mathf.Tau, 16, p.BubbleCooldownFrac <= 0 ? new Color(teal, 0.9f) : new Color(0.45f, 0.47f, 0.52f), 1.5f);
        DrawArc(ab + new Vector2(17, 17), 5, 3.6f, 4.8f, 6, p.BubbleCooldownFrac <= 0 ? new Color(1, 1, 1, 0.8f) : new Color(0.45f, 0.47f, 0.52f), 1.2f);
        KeyHint(font, ab, "dodge");

        var bb = ab + new Vector2(62, 0);
        AbilitySquare(font, bb, "BARRIER", p.AbilityCooldownFrac, p.AbilityChargeReady, new Color(0.75f, 0.92f, 1f), (c, col) =>
        {
            // a shield-shaped ward
            DrawColoredPolygon(new[] { c + new Vector2(-9, -9), c + new Vector2(9, -9), c + new Vector2(9, 1), c + new Vector2(0, 11), c + new Vector2(-9, 1) }, col);
            DrawColoredPolygon(new[] { c + new Vector2(-5, -5), c + new Vector2(5, -5), c + new Vector2(5, 0), c + new Vector2(0, 6), c + new Vector2(-5, 0) }, new Color(0, 0, 0, 0.35f));
        });
        UsePips(bb, p, new Color(0.75f, 0.92f, 1f));
        KeyHint(font, bb, "ability");

        var sb = bb + new Vector2(60, 0);
        var carried = p.BurdenTarget;
        string lab = carried != null ? $"BURDEN {Mathf.CeilToInt(p.BurdenLeft)}s" : "BURDEN";
        AbilitySquare(font, sb, lab, carried != null ? Math.Clamp(1f - p.BurdenLeft / Math.Max(1f, p.Stats.BurdenSeconds), 0f, 1f) : 0f, true, gold, (c, col) =>
        {
            // two links of a chain
            DrawArc(c + new Vector2(-4, 0), 6, 0, Mathf.Tau, 14, col, 2.2f);
            DrawArc(c + new Vector2(4, 0), 6, 0, Mathf.Tau, 14, col, 2.2f);
        });
        KeyHint(font, sb, "ability2");
        if (carried != null)
        {
            string who = Net.Peers.Values.FirstOrDefault(pr => pr.Avatar == carried)?.Name ?? "an ally";
            DrawString(font, sb + new Vector2(56, 20), "carrying " + (p.BurdenCount > 1 ? $"{p.BurdenCount} friends" : who), HorizontalAlignment.Left, -1, 11, new Color(gold, 0.85f));
        }
    }

    private void DrawElementalistGauges(Font font, Player p, Vector2 ab)
    {
        var air = new Color(0.8f, 0.94f, 1f);
        bool canDraft = p.Alimus >= p.UpdraftCost - 0.001f;
        Dial(ab + new Vector2(17, 17), canDraft ? p.UpdraftCooldownFrac : 1f, air);
        DrawString(font, ab + new Vector2(0, -6), $"UPDRAFT {p.UpdraftCost:0}", HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.6f));
        // (three rising streaks in the dial)
        for (int k = -1; k <= 1; k++)
            DrawLine(ab + new Vector2(17 + k * 5, 25), ab + new Vector2(17 + k * 5, 10 - Math.Abs(k) * -2), canDraft ? new Color(air, 0.8f) : new Color(0.45f, 0.47f, 0.52f), 1.5f);
        KeyHint(font, ab, "dodge");

        var zb = ab + new Vector2(66, 0);
        bool fire = p.Stats.Firestorm;
        var stormCol = fire ? ElementBolt.FireColor : ElementBolt.FrostColor;
        bool stormReady = p.AbilityChargeReady && p.Alimus >= p.BlizzardCost - 0.001f;
        AbilitySquare(font, zb, $"{(fire ? "FIRE" : "STORM")} {p.BlizzardCost:0}", p.AbilityCooldownFrac, stormReady, stormCol, (c, col) =>
        {
            if (fire)
            {
                // a cluster of flames
                for (int k = -1; k <= 1; k++)
                    DrawColoredPolygon(new[] { c + new Vector2(k * 7 - 4, 10), c + new Vector2(k * 7, -8 - (k == 0 ? 4 : 0)), c + new Vector2(k * 7 + 4, 10) }, col);
            }
            else
            {
                // a snowflake
                for (int k = 0; k < 3; k++)
                {
                    var d = Vector2.Right.Rotated(k * Mathf.Pi / 3f) * 12f;
                    DrawLine(c - d, c + d, col, 2f);
                }
                DrawCircle(c, 3, col);
            }
        });
        UsePips(zb, p, stormCol);
        KeyHint(font, zb, "ability");

        var sb = zb + new Vector2(60, 0);
        bool cinder = p.Stats.CinderSnap;
        int marked = p.SnapTargets;
        bool snapReady = p.SnapCooldownFrac <= 0 && p.Alimus >= p.SnapCost - 0.001f && marked > 0;
        var snapCol = cinder ? ElementBolt.FireColor : ElementBolt.FrostColor;
        AbilitySquare(font, sb, $"SNAP {p.SnapCost:0}{(marked > 0 ? $" x{marked}" : "")}", p.SnapCooldownFrac, snapReady, snapCol, (c, col) =>
        {
            // a crystal (or a cinder) cracking apart
            DrawColoredPolygon(new[] { c + new Vector2(0, -12), c + new Vector2(7, 0), c + new Vector2(0, 12), c + new Vector2(-7, 0) }, col);
            DrawLine(c + new Vector2(-3, -7), c + new Vector2(2, 1), new Color(0, 0, 0, 0.7f), 1.5f);
            DrawLine(c + new Vector2(2, 1), c + new Vector2(-1, 8), new Color(0, 0, 0, 0.7f), 1.5f);
            for (int k = 0; k < 4; k++)
            {
                var d = Vector2.Right.Rotated(k * Mathf.Tau / 4 + 0.78f);
                DrawLine(c + d * 11, c + d * 15, col, 1.5f);
            }
        });
        KeyHint(font, sb, "ability2");

        // the alimus reserve (tick marks at each spell's cost)
        var bp = sb + new Vector2(60, 10);
        const float w = 150;
        float max = Math.Max(1f, p.Stats.AlimusMax);
        float frac = Math.Clamp(p.Alimus / max, 0, 1);
        DrawRect(new Rect2(bp - new Vector2(2, 2), new Vector2(w + 4, 16)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bp, new Vector2(w * frac, 12)), AlimusColor);
        DrawRect(new Rect2(bp, new Vector2(w * frac, 4)), new Color(1, 1, 1, 0.25f));
        foreach (float cost in new[] { p.UpdraftCost, p.BlizzardCost })
            if (cost < max) DrawLine(bp + new Vector2(w * cost / max, -2), bp + new Vector2(w * cost / max, 14), new Color(0, 0, 0, 0.5f), 1.5f);
        DrawString(font, bp + new Vector2(0, -8), $"ALIMUS  {Mathf.FloorToInt(p.Alimus + 0.001f)} / {Mathf.RoundToInt(max)}", HorizontalAlignment.Left, -1, 10, new Color(0.85f, 0.78f, 1f, 0.8f));
    }
}

/// <summary>Darkness vignette centered on the player (a torch-lit feel without real lights).</summary>
public partial class DarknessOverlay : ColorRect
{
    private ShaderMaterial _mat;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Color = new Color(0, 0, 0, 0);
        _mat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type canvas_item;
uniform vec2 center = vec2(0.5);
uniform float aspect = 1.777;
uniform float radius = 0.62;
uniform float strength = 0.82;
uniform float water = 0.0;
void fragment() {
    vec2 d = SCREEN_UV - center;
    d.x *= aspect;
    float dist = length(d);
    float a = smoothstep(radius * 0.3, radius, dist) * strength;
    vec3 tint = mix(vec3(0.0, 0.0, 0.02), vec3(0.0, 0.04, 0.1), water);
    COLOR = vec4(tint, clamp(a + water * 0.12, 0.0, 1.0));
}"
            }
        };
        Material = _mat;
    }

    public override void _Process(double delta)
    {
        var p = G.Player;
        if (p == null || !IsInstanceValid(p)) return;
        // (online, while you're down the view follows a friend: so does the light)
        if (p.Dead && Net.InRun)
            foreach (var h in G.Players) if (IsInstanceValid(h) && !h.Dead) { p = h; break; }
        var vp = GetViewport();
        var size = vp.GetVisibleRect().Size;
        var sp = p.GetGlobalTransformWithCanvas().Origin;
        _mat.SetShaderParameter("center", sp / size);
        _mat.SetShaderParameter("aspect", size.X / size.Y);
        float w = p.HeadUnder ? 1f : 0f;
        _mat.SetShaderParameter("water", w);
        _mat.SetShaderParameter("radius", p.HeadUnder ? 0.5f : 0.62f);
        _mat.SetShaderParameter("strength", p.HeadUnder ? Math.Max(0.6f, G.Biome?.Darkness ?? 0.82f) : G.Biome?.Darkness ?? 0.82f);
    }
}
