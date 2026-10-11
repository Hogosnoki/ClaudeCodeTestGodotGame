using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// What the HUD's pieces mean. Each piece says where it was drawn this frame and what it is (<see cref="Tip"/>); then:
///  * hovering the mouse over one for a moment shows its tooltip, as the setting allows (<see cref="GameSettings.HudTips"/>: off, the
///    abilities, potions and relics, or everything on the screen);
///  * the HUD guide (F1, or "HUD guide" in the pause menu) marks every piece at once with a number, and lists what each one is.
/// </summary>
public partial class Hud
{
    private readonly struct TipSpot
    {
        public readonly Rect2 R; public readonly string Title, Body; public readonly bool Basic;
        public TipSpot(Rect2 r, string title, string body, bool basic) { R = r; Title = title; Body = body; Basic = basic; }
    }

    private readonly List<TipSpot> _tips = new();
    private Vector2 _tipMouse;
    private float _tipStill;

    /// <summary>Test aid: the pieces marked last frame.</summary>
    public int TipCount => _tips.Count;
    public IEnumerable<string> TipTitles { get { foreach (var t in _tips) yield return t.Title; } }

    /// <summary>The HUD guide is up (every piece numbered and explained).</summary>
    public static bool Guide;

    /// <summary>
    /// Marks a piece of the HUD drawn this frame. <paramref name="basic"/>: an ability, a potion or a relic (shown on hover unless tooltips
    /// are off); the rest only with "everything" (and always in the guide).
    /// </summary>
    private void Tip(Rect2 r, string title, string body, bool basic = false) => _tips.Add(new TipSpot(r, title, body, basic));

    /// <summary>What an ability's square means, from its label (as the icons are found, see <see cref="Icons.AbilityFor"/>).</summary>
    private static (string title, string body) AbilityTip(string label)
    {
        string w = label.Split(' ')[0].ToUpperInvariant();
        return w switch
        {
            "CHARGE" or "CHARGED" => ("Charged Strike", "Charges your blade: the next swing hits harder and saps what it cuts. Lit while it waits."),
            "HEAVE" => ("Heaving Swing", "A great two-handed blow, on your feet. It shatters frozen creatures."),
            "DASH" => ("Guarded Charge", "A shield-first rush that breaks off the attacks it meets."),
            "BASH" => ("Shield Bash", "Stuns everything in front of you, for a share of the shield (the notch on the shield bar)."),
            "HEAL" => ("Heal", "Spends vital force (the number) to mend you, or the friend you aim at."),
            "RUPTURE" => ("Rupture", "A full reserve of vital force bursts in the creatures round you (the crimson mark on the bar)."),
            "RECALL" => ("Recall", "Calls your thrown daggers back; they cut again on the way. With Ricochet it also flings a poison pin at every marked creature."),
            "TETHER" => ("Tether", "Recall pulls you along the line to your dagger stuck in a creature, striking it as you arrive."),
            "BARRIER" => ("Barrier", "A ward of light on you or the friend you aim at, soaking up blows."),
            "BURDEN" => ("Share a Burden", "Carry part of a friend's hurt: aimed at a friend, you take a share of what they take."),
            "STORM" => ("Blizzard", "Costs alimus (the number). A storm that chills and freezes the creatures in it."),
            "FIRE" => ("Firestorm", "Costs alimus (the number). A storm of fire that sets the creatures in it alight."),
            "SNAP" => ("Snap", "Costs alimus. Shatters every frozen creature in view (the count is how many are frozen now)."),
            "SHIFT" => ("Shift", "Become the nearest creature; press again to drop the form. Your health and strength stay yours."),
            "REPAIR" or "MENDING" => ("Self-Repair", "Spends fuel to mend you, slowly. Healing you receive becomes fuel instead of health."),
            "STEAM" => ("Steam Release", "A blast of steam ahead of you; aimed down, it lifts you."),
            "SHOUT" => ("Battle Shout (support)", "You and the allies near you deal 10% more for 10 s."),
            "TAUNT" => ("Taunt (support)", "For 3 s every creature counts you as the nearest hero."),
            "TAP" => ("Health Tap (support)", "Turns 60% of your vital force into 15% of your health."),
            "STALAG" => ("Stalag-Might (support)", "Rock bursts up round a creature's feet: 30 damage, and it is held for 2 s."),
            "EXPOSE" => ("Expose (support)", "The creature you aim at takes 20% more from everyone, and is struck critically more often, for 8 s."),
            "SHARE" => ("Share Pain (support)", "Pick a friend: you take 15% of every blow they take, until you pick another (4 s between picks)."),
            "PRESSURIZE" => ("Pressurize (support)", "Builds pressure, then you move, jump and hit harder while it burns fuel. Press again to end it."),
            "HOWL" => ("Pack Howl (support)", "Allies near you run 20% and strike 15% faster for 8 s."),
            "DODGE" => ("Dodge", "A quick roll out of harm's way; each ring is one dodge, refilling on its own."),
            "BRACE" or "BRACED" => ("Brace", "Plant your feet: blows push you less and hurt less while braced."),
            "HEX" => ("Hex", "Curses the creature you aim at."),
            "VANISH" or "SMOKE" => ("Vanish", "Slip out of sight: the creatures lose you for a moment."),
            "BUBBLE" or "WARD" => ("Bubble", "A bubble round you or the friend you aim at; its wearer breathes under water."),
            "UPDRAFT" => ("Updraft", "Hold to aim, let go to raise a column of air anyone can float in."),
            _ => (Capital(label), "The creature's own special trick while you wear its form."),
        };
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>Every frame: the hovered piece's tooltip, or the guide.</summary>
    private void DrawTips(Font font, Vector2 vs)
    {
        if (Guide) { DrawGuide(font, vs); return; }
        if (GameSettings.HudTips == 0) return;
        var m = GetLocalMousePosition();
        // (only for a mouse resting there: a tooltip doesn't jump up under a cursor passing through)
        if (m.DistanceTo(_tipMouse) > 3f) { _tipMouse = m; _tipStill = 0; return; }
        _tipStill += (float)GetProcessDeltaTime();
        if (_tipStill < 0.35f || (Time.GetTicksMsec() - GameSettings.MouseSeenMs) > 15000) return;
        for (int k = _tips.Count - 1; k >= 0; k--)
        {
            var t = _tips[k];
            if (!t.Basic && GameSettings.HudTips < 2) continue;
            if (!t.R.HasPoint(m)) continue;
            TipBox(font, vs, m + new Vector2(16, 18), t.Title, t.Body, Mathf.Clamp((_tipStill - 0.35f) * 6f, 0f, 1f));
            return;
        }
    }

    private void TipBox(Font font, Vector2 vs, Vector2 at, string title, string body, float a)
    {
        const float w = 280;
        var lines = WrapTip(font, body, w - 16, 12);
        float h = 28 + lines.Count * 15;
        if (at.X + w > vs.X - 8) at.X = vs.X - 8 - w;
        if (at.Y + h > vs.Y - 8) at.Y = vs.Y - 8 - h;
        var r = new Rect2(at, new Vector2(w, h));
        DrawRect(r, new Color(0.06f, 0.055f, 0.07f, 0.92f * a));
        DrawRect(r, new Color(0.85f, 0.7f, 0.35f, 0.7f * a), false, 1.5f);
        DrawString(font, at + new Vector2(8, 18), title, HorizontalAlignment.Left, -1, 13, new Color(1f, 0.85f, 0.45f, a));
        for (int k = 0; k < lines.Count; k++)
            DrawString(font, at + new Vector2(8, 35 + k * 15), lines[k], HorizontalAlignment.Left, -1, 12, new Color(0.9f, 0.9f, 0.92f, a));
    }

    private static List<string> WrapTip(Font font, string text, float width, int size)
    {
        var lines = new List<string>();
        string cur = "";
        foreach (var word in text.Split(' '))
        {
            string next = cur == "" ? word : cur + " " + word;
            if (cur != "" && font.GetStringSize(next, HorizontalAlignment.Left, -1, size).X > width) { lines.Add(cur); cur = word; }
            else cur = next;
        }
        if (cur != "") lines.Add(cur);
        return lines;
    }

    /// <summary>The HUD guide: every piece outlined and numbered, and a list of what each number is.</summary>
    private void DrawGuide(Font font, Vector2 vs)
    {
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, 0.35f));
        // (one entry per kind of piece: five dodge rings are one "Dodge")
        var seen = new HashSet<string>();
        var list = new List<TipSpot>();
        foreach (var t in _tips) if (seen.Add(t.Title)) list.Add(t);
        var gold = new Color(1f, 0.82f, 0.4f);
        for (int k = 0; k < list.Count; k++)
        {
            var r = list[k].R;
            DrawRect(r, new Color(gold, 0.8f), false, 1.5f);
            var badge = r.Position + new Vector2(-4, -4);
            DrawCircle(badge, 8, new Color(0.08f, 0.07f, 0.09f, 0.95f));
            DrawArc(badge, 8, 0, Mathf.Tau, 16, gold, 1.2f);
            DrawString(font, badge + new Vector2(-8, 4), (k + 1).ToString(), HorizontalAlignment.Center, 16, 10, gold);
        }
        // the legend, in the middle of the screen
        const float w = 560;
        var rows = new List<(int n, string title, List<string> lines)>();
        float h = 50;
        for (int k = 0; k < list.Count; k++)
        {
            var lines = WrapTip(font, list[k].Body, w - 190, 11);
            rows.Add((k + 1, list[k].Title, lines));
            h += Math.Max(1, lines.Count) * 14 + 4;
        }
        var at = new Vector2(vs.X / 2 - w / 2, Math.Max(60, vs.Y / 2 - h / 2));
        DrawRect(new Rect2(at, new Vector2(w, h)), new Color(0.05f, 0.045f, 0.06f, 0.94f));
        DrawRect(new Rect2(at, new Vector2(w, h)), new Color(gold, 0.6f), false, 1.5f);
        DrawString(font, at + new Vector2(14, 24), "THE HUD", HorizontalAlignment.Left, -1, 15, gold);
        DrawString(font, at + new Vector2(14, 24), "F1 or any button closes it", HorizontalAlignment.Right, w - 28, 10, new Color(1, 1, 1, 0.5f));
        float y = at.Y + 46;
        foreach (var (n, title, lines) in rows)
        {
            DrawString(font, new Vector2(at.X + 14, y), $"{n}.", HorizontalAlignment.Left, -1, 11, gold);
            DrawString(font, new Vector2(at.X + 36, y), title, HorizontalAlignment.Left, 150, 11, new Color(1f, 0.92f, 0.75f));
            for (int k = 0; k < lines.Count; k++)
                DrawString(font, new Vector2(at.X + 186, y + k * 14), lines[k], HorizontalAlignment.Left, -1, 11, new Color(0.88f, 0.88f, 0.9f));
            y += Math.Max(1, lines.Count) * 14 + 4;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.F1 && G.Player != null) { Guide = !Guide; GetViewport().SetInputAsHandled(); return; }
        // (any other press closes the guide)
        if (Guide && e.IsPressed() && !e.IsEcho() && e is InputEventKey or InputEventJoypadButton or InputEventMouseButton)
        {
            Guide = false;
            GetViewport().SetInputAsHandled();
        }
    }
}
