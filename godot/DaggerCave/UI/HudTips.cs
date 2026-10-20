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
    public static (string title, string body) AbilityTip(string label)
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
            "DASHSLASH" => ("Dash Slash", "A dash in an orange ghost-glow: the first creature it touches is slashed for one and a half times a strike, with everything a strike brings. It dodges nothing of itself, but you can use it to get away. About fifteen seconds to come back."),
            "DODGE" => ("Dodge", "A quick roll out of harm's way; each ring is one dodge, refilling on its own."),
            "BRACE" or "BRACED" => ("Brace", "Plant your feet: blows push you less and hurt less while braced."),
            "HEX" => ("Hex", "Curses the creature you aim at."),
            "VANISH" or "SMOKE" => ("Vanish", "Slip out of sight: the creatures lose you for a moment."),
            "BUBBLE" or "WARD" => ("Bubble", "A bubble round you or the friend you aim at; its wearer breathes under water."),
            "UPDRAFT" => ("Updraft", "Hold to aim, let go to raise a column of air anyone can float in."),
            _ => (Capital(label), "The creature's own special trick while you wear its form."),
        };
    }

    /// <summary>What an elemental ability does, by its name (<see cref="Elements.SingleName"/>, <see cref="Elements.DualName"/>).</summary>
    public static (string title, string body) ElementTip(string name) => (name, name switch
    {
        // the Warden: the attack
        "Shortsword" => "The shortsword. In earth mode it is Stonecutter, in frost Rime Edge, in wind Gale Edge; overcharged, the mode's greater cut.",
        "Stonecutter" => "Earth's cut: a quarter harder, 50 earth (a full meter stuns).",
        "Rime Edge" => "Frost's cut: a quarter harder, 50 frost (full: chilled; full again: frozen solid).",
        "Gale Edge" => "Wind's cut: reaches farther and throws what it strikes back.",
        "Boulder Strike" => "Earth and earth: a crushing blow, more than twice as hard: stunned, tossed up, and the frozen shattered.",
        "Icicle Lance" => "Frost and frost: the cut, and a lance of ice thrown on along your aim that freezes what it meets.",
        "Cyclone Slash" => "Wind and wind: a whirling cut all round you, reaching far, throwing everything it strikes away.",
        "Frozen Earth" => "Earth and frost: a hard blow of frozen stone, 60 earth and 60 frost; it shatters the frozen.",
        "Sandblast" => "Earth and wind: the cut throws a cone of grit ahead: everything in it is nicked and its attacks slowed.",
        "Frost Gust" => "Frost and wind: the cut looses a freezing wind ahead: everything in it is filled with frost and driven back.",
        // the Warden: the block (the shield raised in a mode takes its guard, holding while it stays up, 6 s at most)
        "Block" => "Your shield, raised. In a mode it takes that element's guard each time it goes up, holding while it stays up: earth Stone Guard, frost Rime Guard, wind Gale Guard; overcharged, the greater guard.",
        "Stone Guard" => "Earth's guard: nothing gets through and the shield doesn't wear while it holds; a creature striking it is stunned.",
        "Rime Guard" => "Frost's guard: a creature striking it is filled with frost.",
        "Gale Guard" => "Wind's guard: a creature striking it is thrown back, and shots go back where they came from.",
        "Bastion" => "Earth and earth: the guard of stone, all round you: blows from behind are stopped too.",
        "Glacial Bulwark" => "Frost and frost: a wall of ice rises in front (creatures go for it, friends walk through), and whatever strikes your shield is frozen solid.",
        "Eye of the Storm" => "Wind and wind: a ring of wind about you pushes creatures away and turns shots aside; shots on the shield go back.",
        "Permafrost Guard" => "Earth and frost: the ground about you freezes (creatures on it chilled, friends quicker); strikers are stunned and filled with frost, and nothing gets through.",
        "Dust Wall" => "Earth and wind: a cloud of dust in front hides your friends and slows the creatures in it; strikers are stunned and thrown back.",
        "Hoarfrost Gale" => "Frost and wind: strikers are filled with frost and thrown back, and shots go back.",
        // the Warden: the dash (every elemental charge runs through creatures instead of stopping)
        "Guarded Charge" => "A charge behind the shield. In earth mode it is a Rockslide Charge, in frost Frozen Tundra, in wind a Gale Charge; overcharged, the greater charge.",
        "Rockslide Charge" => "Earth's charge: it ploughs through everything in its way and ends in a shockwave of earth that stuns.",
        "Frozen Tundra" => "Frost's charge: it fills what it passes with frost, and the ground it crossed freezes over (creatures on it chilled, friends quicker).",
        "Gale Charge" => "Wind's charge: farther and faster, throwing creatures aside; friends it passes get a wind at their back.",
        "Avalanche" => "Earth and earth: the rockslide, with a great quake at its end and a fissure running on ahead.",
        "Glacier Charge" => "Frost and frost: everything it passes is frozen solid, and the ground it crossed freezes over.",
        "Tempest Charge" => "Wind and wind: a long charge that carries every creature it meets along in its wake, then flings them.",
        "Frostquake" => "Earth and frost: it ploughs through, ends in a quake, and ice bursts up where it stops.",
        "Duststorm" => "Earth and wind: a long charge leaving clouds of dust behind (friends in them hidden, creatures slowed), and a quake at its end.",
        "Blizzard Charge" => "Frost and wind: farther and faster, filling what it passes with frost and throwing it aside; the ground it crossed freezes over.",
        // the Swordsman
        "Empower Hilt" => "For 8 s every blow lands 20% harder; in a mode, every blow carries its element instead: earth saps a creature's attacks, frost and fire fill its build-up.",
        "Earth Hilt" or "Fire Hilt" or "Frost Hilt" => "For 8 s every blow carries the element: earth saps a creature's attacks, frost and fire fill its build-up.",
        "Swing" => "The sword combo. In earth mode the Heaving Swing comes every few seconds; in fire mode every swing is a Searing Strike, in frost Razor Ice.",
        "Heaving Swing" => "Earth's swing: rooted, twice as hard, 60 earth (a full meter stuns).",
        "Searing Strike" => "Fire's swing: a quick blow, 50 fire (full: alight; full again: it combusts).",
        "Razor Ice" => "Frost's swing: a quick blow, 50 frost (full: chilled; full again: frozen solid).",
        "Dash Slash" => "The dodge: a slashing dash. In a mode, from the ground, the dodge is the mode's leap (fire an Explosive Lunge, frost a Glacial Vault, earth Earthfall) whenever it is ready, the Dash Slash meanwhile.",
        "Explosive Lunge" => "Fire's dodge, from the ground: the ground bursts behind you (fire to everything beside you) and throws you forward in a low leap.",
        "Glacial Vault" => "Frost's dodge, from the ground: you spring back, leaving a likeness of yourself in ice where you stood. Creatures go for it; it takes a couple of blows before it shatters, filling those beside it with frost.",
        "Earthfall" => "Earth's dodge, from the ground: a leap, and you come down like a rock, in a shockwave of earth (the harder the fall, the harder it hits).",
        "Hard Swing" => "A slow, rooted heavy blow: in earth mode ThunderCrush, in fire Pyroclasty, in frost the Ice Axe; overcharged, the mode's great cleave.",
        "ThunderCrush" => "Earth's Hard Swing: slow and crushing, a full earth build-up (a stun), and it shatters the frozen.",
        "Pyroclasty" => "Fire's Hard Swing: a full fire build-up (alight; alight already, it combusts).",
        "Ice Axe" => "Frost's Hard Swing: the sword goes up over your head, an axe head of ice grows out of it, and it comes down in front of you: a full frost build-up (chilled; chilled already, frozen solid).",
        "Earthsplitter" => "Earth and earth: a fissure runs along the floor, tossing up and stunning everything it passes under. Rubble in its way comes down.",
        "Inferno Cleave" => "Fire and fire: a wall of fire stands where the blade lands. Creatures in it burn; shots flying into it burn up.",
        "Glacier Cleaver" => "Frost and frost: held up behind you, the blade slowly grows a vast, ornate axe head of ice, then comes down in front of you. Its blow freezes what it cuts, and the head shatters it as it lands.",
        "Frost Heave" => "Earth and frost: the ground bursts up in ice under the nearest creature: a great blow of frost, and it's thrown into the air.",
        "Magma Rend" => "Earth and fire: the floor splits and magma wells up. Creatures in it are mired, slow in everything, and burn.",
        "Thermal Shock" => "Fire and frost: on a creature already burning, chilled or frozen the blow lands far harder and bursts into steam that hides you.",
        // the Elementalist
        "Pop" => "With nothing readied: a small burst right on the creature nearest your aim, a short way off. No bolt; weaker than the elements. Each charge of fire, frost or wind readied makes the next one a Firebolt, Frostbolt or Gust.",
        "Meteorite" => "Fire and fire: a meteorite falls on the thickest knot of creatures, smashing every platform it passes through (they grow back), and bursts in fire.",
        "Cryometeor" => "Frost and frost: a rock of ice falls on the thickest knot of creatures; everything about it is frozen solid, and the ground freezes over.",
        "Strong Gust" => "Wind and wind: a little whirlwind races along the floor from the staff, shoving every creature it meets back and nicking them as it goes.",
        "Fire Barrage" or "Frost Barrage" => "Fire (or frost) and wind: a volley of seven bolts loosed one after another, fanned along the aim.",
        "Lift" => "The dodge: a push of air straight up, on the ground or in the air. In a mode the dodge is the mode's movement whenever it is ready (fire Fire Walk, frost Tundra, wind the Updraft), Lift meanwhile; overcharged, Flamebreath or Frost Tower.",
        "Fire Walk" => "Fire's dodge: for 4 s you move 30% faster and fire burns where you walk.",
        "Tundra" => "Frost's dodge: the ground about you freezes over: heroes on it go faster, creatures on it are chilled and filled with frost.",
        "Updraft" => "Wind's dodge: a column of rising air along your aim (straight up, aimed nowhere) to ride up. It carries heroes along it.",
        "Flamebreath" => "Fire and fire: fire pours from the staff in a cone, burning creatures and the shots in it. Aimed down, it holds you up.",
        "Frost Tower" => "Frost and frost: you leap, and a pillar of ice rises under you to stand and cast from. Creatures near go for it instead of you (it has a little health, more as you grow); your friends walk through it.",
        "Fire Barrier" => "Fire and wind: a ring of fire about you, going where you go: creatures that touch it burn and are thrown back; shots burn up.",
        "Frost Barrier" => "Frost and wind: frost armour: the next creature to strike you in melee is filled with frost (a full meter).",
        "Force Blast" => "A wide, weak burst of force at a spot ahead, throwing creatures out of it. In fire mode it is Burst, in frost Snap, in wind a Cyclone.",
        "Burst" => "Fire's spell: an explosion at a spot ahead, 60 fire; and every creature in view already burning, wherever it is, takes a full build-up (and combusts).",
        "Snap" => "Frost's spell: every creature in view frozen solid shatters, wherever it is; and the cold closes on a spot ahead, filling what's there with frost.",
        "Cyclone" => "Wind's spell: a storm at a spot ahead that drags light creatures into its middle and swallows the shots that fly into it.",
        "Rain of Fire" => "Fire and fire: fire rains over a spot ahead for 3 s, each fireball bursting where it lands.",
        "Hailstorm" => "Frost and frost: hail pours over a spot ahead for 3 s, filling creatures with frost and beating flying ones down.",
        "Fire Storm" => "Fire and wind: a storm of fire, fanned by the wind: it burns, drags light creatures in and swallows shots.",
        "Blizzard" => "Frost and wind: a storm of snow, fanned by the wind: it chills, drags light creatures in and swallows shots.",
        "Tornado" => "Wind and wind: a tornado runs along the floor, carrying light creatures along inside it and swallowing their shots, then flings them away.",
        "Frostfire Lance" => "Fire and frost: one beam through every creature in a line, fire and frost together; one already burning or chilled takes far more.",
        "Ward" => "Aid: a barrier for you and your friends near. In fire mode it is Kindling, in frost Frost Armor, in wind Narrow Draft.",
        "Kindling" => "Fire's Aid: for 8 s you and your friends near add a little fire to every blow.",
        "Frost Armor" => "Frost's Aid: for 8 s a creature that strikes you in melee is filled with frost.",
        "Narrow Draft" => "Wind's Aid: for 4 s you and your friends near move 20% faster and jump higher.",
        _ => "",
    });

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
        UiArt.Shadow(this, r, 10f, 0.4f * a);
        UiArt.FillRounded(this, r, 10f, UiArt.Surface.Panel, new Color(1, 1, 1, 0.96f * a), 0.6f);
        UiArt.Outline(this, r, 10f, new Color(0.85f, 0.7f, 0.35f, 0.8f * a), 1.5f);
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
            UiArt.Outline(this, r.Grow(2f), 6f, new Color(gold, 0.85f), 1.5f);
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
        UiArt.Shadow(this, new Rect2(at, new Vector2(w, h)), 16f, 0.5f);
        UiArt.FillRounded(this, new Rect2(at, new Vector2(w, h)), 16f, UiArt.Surface.Panel, Colors.White, 0.8f);
        UiArt.Frame(this, new Rect2(at, new Vector2(w, h)), 16f, UiArt.Rarity.Rare, (float)Time.GetTicksMsec() / 1000f);
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
