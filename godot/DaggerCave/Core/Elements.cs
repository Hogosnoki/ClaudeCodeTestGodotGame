using System;
using Godot;

namespace DaggerCave;

/// <summary>An element a hero can lend an attack (see <see cref="Elements"/>).</summary>
public enum HeroElement : byte { None, Earth, Fire, Frost, Wind }

/// <summary>
/// The elemental heroes: a hero's attacks are plain until an element is added to one. Pressing an element's button (1, 2, 3; a
/// controller's d-pad left, up, right) readies a charge of it; the next of the hero's attacks spends it and becomes that element's
/// version (the Swordsman's Swing with Earth is the Heaving Swing, with Fire the Pyre Strike...), and the ability then rests for as
/// long as that version asks. Elemental blows fill a creature's build-up of that element (<see cref="Enemy.Build"/>) rather than
/// rolling chances: full, it takes hold (fire: alight, then it combusts; frost: chilled, then frozen solid; earth: stunned).
/// The Swordsman and the Elementalist have them so far; <see cref="GameSettings.ClassicKits"/> brings back their old kits.
/// </summary>
public static class Elements
{
    /// <summary>The hero fights with elements (not the classic kit).</summary>
    public static bool On(HeroKind h) => !GameSettings.ClassicKits && !ClassicForTests && h is HeroKind.Swordsman or HeroKind.Elementalist;
    /// <summary>The tests of the classic kits keep them (never saved): `--scenario=elements` or `--elements` tests the elemental ones.</summary>
    public static bool ClassicForTests;

    private static readonly HeroElement[] None = Array.Empty<HeroElement>();
    private static readonly HeroElement[] Sword = { HeroElement.Earth, HeroElement.Fire, HeroElement.Frost };
    private static readonly HeroElement[] Ele = { HeroElement.Fire, HeroElement.Frost, HeroElement.Wind };

    /// <summary>The hero's three elements, in button order (1, 2, 3).</summary>
    public static HeroElement[] Of(HeroKind h) => !On(h) ? None : h == HeroKind.Swordsman ? Sword : Ele;

    public static string Name(HeroElement e) => e switch { HeroElement.Earth => "Earth", HeroElement.Fire => "Fire", HeroElement.Frost => "Frost", HeroElement.Wind => "Wind", _ => "" };

    public static Color ColorOf(HeroElement e) => e switch
    {
        HeroElement.Earth => new Color(0.82f, 0.62f, 0.35f),
        HeroElement.Fire => new Color(1f, 0.5f, 0.15f),
        HeroElement.Frost => new Color(0.65f, 0.88f, 1f),
        HeroElement.Wind => new Color(0.72f, 1f, 0.82f),
        _ => new Color(0.85f, 0.8f, 1f),
    };

    public static DamageKind KindOf(HeroElement e) => e switch { HeroElement.Fire => DamageKind.Fire, HeroElement.Frost => DamageKind.Frost, _ => DamageKind.Physical };

    /// <summary>The input action of an element slot (0-2).</summary>
    public static string Action(int slot) => $"infuse_{slot + 1}";

    /// <summary>Two charges in a set order (earth, fire, frost, wind), so a pair is the same whichever was readied first.</summary>
    public static (HeroElement a, HeroElement b) Pair(HeroElement x, HeroElement y) => x <= y ? (x, y) : (y, x);

    /// <summary>
    /// The ability two charges make (<see cref="Tune.Dual"/>): the Swordsman's Hard Swing, the Elementalist's Burst. Who has which is
    /// by who would: the Swordsman, the earth under him and the blade in his hand; the Elementalist, the sky (fire falling, wind, ice).
    /// </summary>
    public static string DualName(HeroKind h, HeroElement x, HeroElement y)
    {
        var (a, b) = Pair(x, y);
        if (h == HeroKind.Swordsman)
            return (a, b) switch
            {
                (HeroElement.Earth, HeroElement.Earth) => "Earthsplitter",
                (HeroElement.Fire, HeroElement.Fire) => "Inferno Cleave",
                (HeroElement.Frost, HeroElement.Frost) => "Glacier Cleaver",
                (HeroElement.Earth, HeroElement.Frost) => "Frost Heave",
                (HeroElement.Earth, HeroElement.Fire) => "Magma Rend",
                (HeroElement.Fire, HeroElement.Frost) => "Thermal Shock",
                _ => "",
            };
        if (h == HeroKind.Elementalist)
            return (a, b) switch
            {
                (HeroElement.Fire, HeroElement.Fire) => "Meteor",
                (HeroElement.Frost, HeroElement.Frost) => "Frozen Orb",
                (HeroElement.Wind, HeroElement.Wind) => "Tornado",
                (HeroElement.Fire, HeroElement.Frost) => "Frostfire Lance",
                (HeroElement.Fire, HeroElement.Wind) => "Wildfire",
                (HeroElement.Frost, HeroElement.Wind) => "Whiteout",
                _ => "",
            };
        return "";
    }

    /// <summary>A pair's colour: one element's, or the two blended.</summary>
    public static Color DualColor(HeroElement x, HeroElement y) => x == y ? ColorOf(x) : ColorOf(x).Lerp(ColorOf(y), 0.5f);
}
