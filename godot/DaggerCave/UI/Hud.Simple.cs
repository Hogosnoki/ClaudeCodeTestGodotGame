using System;
using Godot;

namespace DaggerCave;

/// <summary>The simple kits' panel (<see cref="Elements.Simple"/>): the elements on the d-pad above, then the three abilities, each square in
/// the element's colour and named for what it is (the tip gives its full name and what the element adds).</summary>
public partial class Hud
{
    private void DrawSimpleKit(Font font, Player p, Vector2 ab)
    {
        DrawElementRow(font, p, ab + new Vector2(0, -74));
        var col = Elements.ColorOf(p.Mode);
        const float step = 58f;
        var x = ab;
        // the primary attack (the Rogue's calls her daggers home while none is in hand)
        bool recall = p.Stats.Hero == HeroKind.Rogue && p.DaggersInHand == 0;
        float pf = p.SimpleFrac(KitRole.Primary);
        AbilitySquare(font, x, recall ? "RECALL" : Elements.Noun(p.Stats.Hero, KitRole.Primary).ToUpperInvariant(), recall ? 0f : pf, recall || pf <= 0, col, (c, cc) =>
        {
            // a blade's stroke
            DrawLine(c + new Vector2(-9, 9), c + new Vector2(9, -9), cc, 3f);
            DrawArc(c, 12, -MathF.PI * 0.95f, -MathF.PI * 0.35f, 10, new Color(cc, 0.5f), 2f);
        }, recall ? null : p.SimpleName(KitRole.Primary));
        KeyHint(font, x, "attack");
        // the defensive (the Vitalist's heal shows its cost in vital force)
        x += new Vector2(step, 0);
        float df = p.SimpleFrac(KitRole.Defensive);
        string dl = Elements.Noun(p.Stats.Hero, KitRole.Defensive).ToUpperInvariant();
        bool dready = df <= 0;
        if (p.Stats.Hero == HeroKind.Vitalist) { dl += $" ({p.HealCost:0})"; dready &= p.VitalForce >= p.HealCost - 0.001f; }
        if (p.Stats.Hero == HeroKind.Warden && p.ShieldRaised) dl = "RAISED";
        AbilitySquare(font, x, dl, df, dready, col, (c, cc) =>
        {
            // a shield
            DrawColoredPolygon(new[] { c + new Vector2(-9, -9), c + new Vector2(9, -9), c + new Vector2(9, 1), c + new Vector2(0, 11), c + new Vector2(-9, 1) }, cc);
            DrawColoredPolygon(new[] { c + new Vector2(-5, -5), c + new Vector2(5, -5), c + new Vector2(5, 0), c + new Vector2(0, 6), c + new Vector2(-5, 0) }, new Color(0, 0, 0, 0.35f));
        }, p.SimpleName(KitRole.Defensive));
        if (p.Stats.Hero is HeroKind.Elementalist or HeroKind.Vitalist or HeroKind.Aegis or HeroKind.Rogue) UsePips(x, p, col);
        KeyHint(font, x, "dodge");
        // the special
        x += new Vector2(step, 0);
        float sf = p.SimpleFrac(KitRole.Special);
        AbilitySquare(font, x, Elements.Noun(p.Stats.Hero, KitRole.Special).ToUpperInvariant(), sf, sf <= 0, col, (c, cc) =>
        {
            // a burst
            DrawCircle(c, 4.5f, cc);
            for (int k = 0; k < 8; k++)
            {
                var d = Vector2.Right.Rotated(k * Mathf.Tau / 8 + 0.2f);
                DrawLine(c + d * 7, c + d * (k % 2 == 0 ? 13 : 10), cc, 2f);
            }
        }, p.SimpleName(KitRole.Special));
        if (p.Stats.Hero == HeroKind.Warden) UsePips(x, p, col);
        KeyHint(font, x, "ability");
        _gaugeEnd = Math.Max(_gaugeEnd, step * 3f);
        if (p.Stats.Hero != HeroKind.Vitalist) return;
        // the Vitalist's vital force reserve (each notch one heal)
        var bp = x + new Vector2(52, 10);
        const float w = 150;
        float max = Math.Max(1f, p.Stats.VitalForceMax);
        UiArt.Bar(this, new Rect2(bp, new Vector2(w, 12)), Math.Clamp(p.VitalForce / max, 0, 1), new Color(0.42f, 0.85f, 0.4f));
        for (float v = p.HealCost; v < max - 0.01f; v += p.HealCost)
            DrawLine(bp + new Vector2(w * v / max, 0), bp + new Vector2(w * v / max, 12), new Color(0, 0, 0, 0.45f), 1f);
        Tip(new Rect2(bp - new Vector2(2, 18), new Vector2(w + 4, 34)), "Vital force", "Life you have drained, held in reserve. Each notch is one Mending.");
        DrawString(font, bp + new Vector2(0, -8), $"VITAL FORCE  {Num.Shown(p.VitalForce)} / {Mathf.RoundToInt(max)}", HorizontalAlignment.Left, -1, 10, new Color(0.75f, 1f, 0.7f, 0.8f));
        _gaugeEnd = Math.Max(_gaugeEnd, step * 3f + 52f + w);
    }

    /// <summary>What a simple kit's ability does, by its name ("Searing Strike"): false for any other name.</summary>
    private static bool SimpleTip(string name, out (string title, string body) tip)
    {
        tip = default;
        if (!Elements.Simple) return false;
        foreach (var h in new[] { HeroKind.Swordsman, HeroKind.Warden, HeroKind.Elementalist, HeroKind.Vitalist, HeroKind.Aegis, HeroKind.Rogue })
            foreach (var el in Elements.Of(h))
                foreach (var r in new[] { KitRole.Primary, KitRole.Defensive, KitRole.Special })
                    if (Elements.SimpleName(h, r, el) == name) { tip = (name, SimpleBody(h, r, el)); return true; }
        return false;
    }

    private static string SimpleBody(HeroKind h, KitRole r, HeroElement el)
    {
        string lower = Elements.Name(el).ToLowerInvariant();
        bool wind = el == HeroElement.Wind;
        // ("builds up fire by 40", or, wind, "shoves it away")
        string B(float amount) => wind ? "shoves it away" : $"builds up {lower} by {amount:0}";
        float P = Tune.Elements.PrimaryBuild, S = Tune.Elements.SpecialBuild;
        string what = (h, r) switch
        {
            (HeroKind.Swordsman, KitRole.Primary) => $"A swing of the sword: it {B(P)} in each creature it strikes. A **tipper**, a blow landing with the outer part of the blade, deals {Tune.Elements.TipperDamage:0.#}x and {(wind ? "shoves twice as hard" : "builds double")}: keep your distance.",
            (HeroKind.Swordsman, KitRole.Defensive) => $"A quick roll out of harm's way. It lays on no element; but for {Tune.Elements.RiposteWindow:0.#} s after it, every blow is a tipper (the riposte).",
            (HeroKind.Swordsman, KitRole.Special) => $"The Hard Swing: a great two-handed blow, on your feet. It {B(S)} in each creature it strikes (a tipper, double).",
            (HeroKind.Warden, KitRole.Primary) => "A cut of the shortsword. The Warden's element isn't in his blade but in his shield.",
            (HeroKind.Warden, KitRole.Defensive) => $"Hold to raise the shield: it stops most of each blow from the front, wearing as it does. A creature whose blow it stops: the shield {B(Tune.Elements.CounterBuild)}; raised just in time (a perfect block, all the blow stopped), it {B(S)}.",
            (HeroKind.Warden, KitRole.Special) => $"The Guarded Charge: a shield-first rush that breaks off the attack it meets, and {B(S)} in that creature.",
            (HeroKind.Elementalist, KitRole.Primary) => $"A bolt from the staff, flying to the creature nearest your aim: it {B(P)}.",
            (HeroKind.Elementalist, KitRole.Defensive) => "Ward: a barrier on you and the friends near you. It lays on no element.",
            (HeroKind.Elementalist, KitRole.Special) => $"A blast at the spot along your aim: it {B(S)} in everything there. Then the element **lingers** there for {Tune.Elements.ZoneSeconds:0} s, " + (wind ? "drawing what's in it to the middle." : $"building {Tune.Elements.ZoneBuild:0} more {lower} in everything in it, twice a second."),
            (HeroKind.Vitalist, KitRole.Primary) => $"Tears the life out of the creature you aim at (it becomes vital force), and {B(P)}. On a creature your hex is seeping into, it **tears the rest out at once** ({Tune.Elements.DrainCash:0.#}x).",
            (HeroKind.Vitalist, KitRole.Defensive) => "Spends vital force to mend you and the hurt friends near you. It lays on no element.",
            (HeroKind.Vitalist, KitRole.Special) => "The hex about you: what it reaches is slowed, and takes more from every blow. It **carries your element**: " + (wind ? "wind buffets each creature in it, away from you, for as long as the hex lasts." : $"{lower} seeps into each creature it reaches, {Tune.Elements.HexSeep:0} a second for as long as the hex lasts (your drain tears it out at once)."),
            (HeroKind.Aegis, KitRole.Primary) => $"A bubble shot that bursts on what it hits, weakening it and those beside it: it {B(P)} in each.",
            (HeroKind.Aegis, KitRole.Defensive) => $"A barrier of light on the friend you aim at (or on you), and it **lends them your element** for {Tune.Elements.ImbueSeconds:0} s: each of their blows {B(Tune.Elements.ImbueBuild)}.",
            (HeroKind.Aegis, KitRole.Special) => $"A field of bubbles: you and every hero near you are wrapped in one, and lent your element for {Tune.Elements.ImbueSeconds:0} s (each of their blows {B(Tune.Elements.ImbueBuild)}); each creature in it: it {B(S)}.",
            (HeroKind.Rogue, KitRole.Primary) => $"A quick jab of the daggers: it {B(P)}. From hiding, {(wind ? "it shoves twice as hard" : "it builds double")}. With no dagger in hand, the attack calls them home.",
            (HeroKind.Rogue, KitRole.Defensive) => "Slip out of sight: the creatures lose you for a moment, and your next jab, from hiding, lays on twice the element.",
            _ => $"Throw a dagger at the creature nearest your aim: it {B(Tune.Elements.KnifeTouch)} as it lands, then **stores** {lower} while it stays in ({Tune.Elements.KnifeStore:0} a second, up to {Tune.Elements.KnifeStoreMax:0}). Press it again to call the knife home: it releases all it stored at once.",
        };
        // (what a full meter does: "full, it burns; full again while burning, it combusts")
        string tt = Elements.TouchText(el);
        int open = tt.IndexOf('(');
        string full = open > 0 && tt.EndsWith(")") ? $" Of {lower}: {tt[(open + 1)..^1]}." : "";
        return $"{what.Replace("**", "")}{full} It works the same in every element: only what the element does changes.";
    }
}
