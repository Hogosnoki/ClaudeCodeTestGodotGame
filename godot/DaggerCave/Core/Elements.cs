using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>An element a hero can lend an attack (see <see cref="Elements"/>).</summary>
public enum HeroElement : byte { None, Earth, Fire, Frost, Wind, Nature, Water, Dark }

/// <summary>Which of a hero's buttons an element changes: the attack, movement (the Swordsman's and Elementalist's dodge, the Warden's
/// Guarded Charge), the heavy one (the Hard Swing, the fixed-range spell, the Warden's block) and the fourth (Empower Hilt, Aid).</summary>
public enum ElemSlot : byte { Attack, Move, Heavy, Aid }

/// <summary>
/// The elemental heroes: a hero's attacks are plain until an element is added to one. Pressing an element's button (1, 2, 3; a
/// controller's d-pad left, up, right) readies a charge of it; the next of the hero's attacks spends it and becomes that element's
/// version (the Swordsman's Swing with Earth is the Heaving Swing, with Fire the Searing Strike...), and the ability then rests for as
/// long as that version asks. Elemental blows fill a creature's build-up of that element (<see cref="Enemy.Build"/>) rather than
/// rolling chances: full, it takes hold (fire: alight, then it combusts; frost: chilled, then frozen solid; earth: stunned).
/// The Swordsman, the Elementalist and the Warden have them so far; <see cref="GameSettings.ClassicKits"/> brings back their old kits.
/// </summary>
public static class Elements
{
    /// <summary>The hero fights with elements (not the classic kit).</summary>
    public static bool On(HeroKind h) => !GameSettings.ClassicKits && !ClassicForTests && h is HeroKind.Swordsman or HeroKind.Elementalist or HeroKind.Warden or HeroKind.Vitalist or HeroKind.Aegis or HeroKind.Rogue or HeroKind.ShapeShifter;
    /// <summary>The tests of the classic kits keep them (never saved): `--scenario=elements` or `--elements` tests the elemental ones.</summary>
    public static bool ClassicForTests;

    private static readonly HeroElement[] None = Array.Empty<HeroElement>();
    private static readonly HeroElement[] Sword = { HeroElement.Earth, HeroElement.Fire, HeroElement.Frost };
    private static readonly HeroElement[] Ele = { HeroElement.Fire, HeroElement.Frost, HeroElement.Wind };
    private static readonly HeroElement[] Ward = { HeroElement.Earth, HeroElement.Frost, HeroElement.Wind };
    private static readonly HeroElement[] Vital = { HeroElement.Nature, HeroElement.Wind, HeroElement.Water };
    private static readonly HeroElement[] Aeg = { HeroElement.Wind, HeroElement.Earth, HeroElement.Frost };
    private static readonly HeroElement[] Rog = { HeroElement.Dark, HeroElement.Wind, HeroElement.Nature };
    private static readonly HeroElement[] Shift = { HeroElement.Nature, HeroElement.Wind, HeroElement.Water };

    /// <summary>The mode a hero starts in: the element that is most their own.</summary>
    public static HeroElement Signature(HeroKind h) => h switch
    {
        HeroKind.Warden => HeroElement.Earth, HeroKind.Swordsman or HeroKind.Elementalist => HeroElement.Fire, HeroKind.Vitalist => HeroElement.Nature,
        HeroKind.Aegis => HeroElement.Wind, HeroKind.Rogue => HeroElement.Dark, HeroKind.ShapeShifter => HeroElement.Nature, _ => Of(h).FirstOrDefault(),
    };

    /// <summary>The hero's three elements, in button order (1, 2, 3).</summary>
    public static HeroElement[] Of(HeroKind h) => !On(h) ? None : h switch { HeroKind.Swordsman => Sword, HeroKind.Warden => Ward, HeroKind.Vitalist => Vital, HeroKind.Aegis => Aeg, HeroKind.Rogue => Rog, HeroKind.ShapeShifter => Shift, _ => Ele };

    public static string Name(HeroElement e) => e switch { HeroElement.Earth => "Earth", HeroElement.Fire => "Fire", HeroElement.Frost => "Frost", HeroElement.Wind => "Wind", HeroElement.Nature => "Nature", HeroElement.Water => "Water", HeroElement.Dark => "Dark", _ => "" };

    public static Color ColorOf(HeroElement e) => e switch
    {
        HeroElement.Earth => new Color(0.82f, 0.62f, 0.35f),
        HeroElement.Fire => new Color(1f, 0.5f, 0.15f),
        HeroElement.Frost => new Color(0.65f, 0.88f, 1f),
        HeroElement.Wind => new Color(0.72f, 1f, 0.82f),
        HeroElement.Nature => new Color(0.42f, 0.88f, 0.3f),
        HeroElement.Water => new Color(0.3f, 0.6f, 1f),
        HeroElement.Dark => new Color(0.6f, 0.4f, 0.92f),
        _ => new Color(0.85f, 0.8f, 1f),
    };

    public static DamageKind KindOf(HeroElement e) => e switch { HeroElement.Fire => DamageKind.Fire, HeroElement.Frost => DamageKind.Frost, HeroElement.Nature => DamageKind.Nature, HeroElement.Water => DamageKind.Water, _ => DamageKind.Physical };

    /// <summary>The input action of an element slot (0-2).</summary>
    public static string Action(int slot) => $"infuse_{slot + 1}";

    /// <summary>Two charges in a set order (earth, fire, frost, wind, nature, water, dark), so a pair is the same whichever was readied first.</summary>
    public static (HeroElement a, HeroElement b) Pair(HeroElement x, HeroElement y) => x <= y ? (x, y) : (y, x);

    /// <summary>
    /// What an element (or none) makes of each button. The Swordsman: Swing, movement (the dodge: the Dash Slash, or an element's leap), the
    /// Hard Swing and Empower Hilt; the Elementalist: the attack (a pop; a bolt with an element), movement (the dodge: Lift, or an element's),
    /// the fixed-range spell (the Burst button) and Aid (the Extended button).
    /// </summary>
    public static string SingleName(HeroKind h, ElemSlot slot, HeroElement e)
    {
        if (h == HeroKind.Swordsman)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Earth => "Heaving Swing", HeroElement.Fire => "Searing Strike", HeroElement.Frost => "Razor Ice", _ => "Swing" },
                ElemSlot.Move => e switch { HeroElement.Earth => "Earthfall", HeroElement.Fire => "Explosive Lunge", HeroElement.Frost => "Glacial Vault", _ => "Dash Slash" },
                ElemSlot.Heavy => e switch { HeroElement.Earth => "ThunderCrush", HeroElement.Fire => "Pyroclasty", HeroElement.Frost => "Ice Axe", _ => "Hard Swing" },
                _ => e == HeroElement.None ? "Empower Hilt" : $"{Name(e)} Hilt",
            };
        // the Warden: the attack (his shortsword), movement (the Guarded Charge) and the block (his shield, raised); no fourth
        if (h == HeroKind.Warden)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Earth => "Stonecutter", HeroElement.Frost => "Rime Edge", HeroElement.Wind => "Gale Edge", _ => "Shortsword" },
                ElemSlot.Move => e switch { HeroElement.Earth => "Rockslide Charge", HeroElement.Frost => "Frozen Tundra", HeroElement.Wind => "Gale Charge", _ => "Guarded Charge" },
                ElemSlot.Heavy => e switch { HeroElement.Earth => "Earth Shield", HeroElement.Frost => "Frost Shield", HeroElement.Wind => "Wind Barrier", _ => "Block" },
                _ => "",
            };
        // the Vitalist: the drain (attack), the hex about her (the dodge button: movement's slot) and the heal (the ability: Aid's slot)
        if (h == HeroKind.Vitalist)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Nature => "Thorn Drain", HeroElement.Wind => "Wisp Drain", HeroElement.Water => "Tide Drain", _ => "Drain" },
                ElemSlot.Move => e switch { HeroElement.Nature => "Overgrowth", HeroElement.Wind => "Withering Gale", HeroElement.Water => "Drowning Hex", _ => "Hex" },
                ElemSlot.Aid => e switch { HeroElement.Nature => "Regrowth", HeroElement.Wind => "Breath of Life", HeroElement.Water => "Cleansing Wave", _ => "Heal" },
                _ => "",
            };
        // the Shape Shifter: her staff (attack, out of a form), the creature's special (in one; see Player.ShifterElements.cs for its names)
        if (h == HeroKind.ShapeShifter)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Nature => "Bramble Staff", HeroElement.Wind => "Gale Staff", HeroElement.Water => "Tide Staff", _ => "Staff" },
                ElemSlot.Move => e switch { HeroElement.Nature => "Walking Shape", HeroElement.Wind => "Flying Shape", HeroElement.Water => "Water Shape", _ => "Shift" },
                _ => "",
            };
        // the Rogue: the strike (attack, her jab), the evade (the dodge button: her vanish) and the throw (the ability)
        if (h == HeroKind.Rogue)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Dark => "Shadow Strike", HeroElement.Wind => "Gale Jab", HeroElement.Nature => "Venom Jab", _ => "Jab" },
                ElemSlot.Move => e switch { HeroElement.Dark => "Shadowmeld", HeroElement.Wind => "Gale Step", HeroElement.Nature => "Spore Cloud", _ => "Vanish" },
                ElemSlot.Aid => e switch { HeroElement.Dark => "Shadow Knife", HeroElement.Wind => "Gale Knife", HeroElement.Nature => "Barbed Knife", _ => "Dagger Throw" },
                _ => "",
            };
        // the Aegis: the bubble shot (attack, her ward bolt), the bubble field (the dodge button) and the barrier (the ability)
        if (h == HeroKind.Aegis)
            return slot switch
            {
                ElemSlot.Attack => e switch { HeroElement.Wind => "Gale Bubble", HeroElement.Earth => "Stone Bubble", HeroElement.Frost => "Rime Bubble", _ => "Bubble Shot" },
                ElemSlot.Move => e switch { HeroElement.Wind => "Air Dome", HeroElement.Earth => "Stone Dome", HeroElement.Frost => "Ice Dome", _ => "Bubble Field" },
                ElemSlot.Aid => e switch { HeroElement.Wind => "Wind Ward", HeroElement.Earth => "Stone Skin", HeroElement.Frost => "Frost Ward", _ => "Barrier" },
                _ => "",
            };
        return slot switch
        {
            ElemSlot.Attack => e switch { HeroElement.Fire => "Firebolt", HeroElement.Frost => "Frostbolt", HeroElement.Wind => "Gust", _ => "Pop" },
            ElemSlot.Move => e switch { HeroElement.Fire => "Fire Walk", HeroElement.Frost => "Tundra", HeroElement.Wind => "Updraft", _ => "Lift" },
            ElemSlot.Heavy => e switch { HeroElement.Fire => "Burst", HeroElement.Frost => "Snap", HeroElement.Wind => "Cyclone", _ => "Force Blast" },
            _ => e switch { HeroElement.Fire => "Kindling", HeroElement.Frost => "Frost Armor", HeroElement.Wind => "Narrow Draft", _ => "Ward" },
        };
    }

    /// <summary>
    /// The ability two charges make on a button (<see cref="Tune.Dual"/>; "" where the pair has none there, and the button spends just the
    /// oldest charge). Every hero has every pair on each elemental button: the same element twice is the mode's greater version (an
    /// overcharge), two different the mode's mix with its attribute.
    /// </summary>
    public static string DualName(HeroKind h, ElemSlot slot, HeroElement x, HeroElement y)
    {
        var (a, b) = Pair(x, y);
        if (h == HeroKind.Swordsman)
            return slot switch
            {
                ElemSlot.Attack => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Fault Strike",
                    (HeroElement.Fire, HeroElement.Fire) => "Flame Crescent",
                    (HeroElement.Frost, HeroElement.Frost) => "Frost Lance",
                    (HeroElement.Earth, HeroElement.Fire) => "Magma Strike",
                    (HeroElement.Earth, HeroElement.Frost) => "Permafrost Strike",
                    (HeroElement.Fire, HeroElement.Frost) => "Steam Strike",
                    _ => "",
                },
                ElemSlot.Move => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Meteor Fall",
                    (HeroElement.Fire, HeroElement.Fire) => "Blast Lunge",
                    (HeroElement.Frost, HeroElement.Frost) => "Frozen Effigy",
                    (HeroElement.Earth, HeroElement.Fire) => "Magma Leap",
                    (HeroElement.Earth, HeroElement.Frost) => "Frostfall",
                    (HeroElement.Fire, HeroElement.Frost) => "Steam Vault",
                    _ => "",
                },
                ElemSlot.Heavy => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Earthsplitter",
                    (HeroElement.Fire, HeroElement.Fire) => "Inferno Cleave",
                    (HeroElement.Frost, HeroElement.Frost) => "Glacier Cleaver",
                    (HeroElement.Earth, HeroElement.Frost) => "Frost Heave",
                    (HeroElement.Earth, HeroElement.Fire) => "Magma Rend",
                    (HeroElement.Fire, HeroElement.Frost) => "Thermal Shock",
                    _ => "",
                },
                _ => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Stoneheart Hilt",
                    (HeroElement.Fire, HeroElement.Fire) => "Blazing Hilt",
                    (HeroElement.Frost, HeroElement.Frost) => "Glacial Hilt",
                    (HeroElement.Earth, HeroElement.Fire) => "Magma Hilt",
                    (HeroElement.Earth, HeroElement.Frost) => "Permafrost Hilt",
                    (HeroElement.Fire, HeroElement.Frost) => "Steam Hilt",
                    _ => "",
                },
            };
        if (h == HeroKind.Warden)
            return slot switch
            {
                ElemSlot.Attack => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Boulder Strike",
                    (HeroElement.Frost, HeroElement.Frost) => "Icicle Lance",
                    (HeroElement.Wind, HeroElement.Wind) => "Cyclone Slash",
                    (HeroElement.Earth, HeroElement.Frost) => "Frozen Earth",
                    (HeroElement.Earth, HeroElement.Wind) => "Sandblast",
                    (HeroElement.Frost, HeroElement.Wind) => "Frost Gust",
                    _ => "",
                },
                ElemSlot.Move => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Avalanche",
                    (HeroElement.Frost, HeroElement.Frost) => "Glacier Charge",
                    (HeroElement.Wind, HeroElement.Wind) => "Tempest Charge",
                    (HeroElement.Earth, HeroElement.Frost) => "Frostquake",
                    (HeroElement.Earth, HeroElement.Wind) => "Duststorm",
                    (HeroElement.Frost, HeroElement.Wind) => "Blizzard Charge",
                    _ => "",
                },
                ElemSlot.Heavy => (a, b) switch
                {
                    (HeroElement.Earth, HeroElement.Earth) => "Bastion",
                    (HeroElement.Frost, HeroElement.Frost) => "Glacial Bulwark",
                    (HeroElement.Wind, HeroElement.Wind) => "Eye of the Storm",
                    (HeroElement.Earth, HeroElement.Frost) => "Permafrost Guard",
                    (HeroElement.Earth, HeroElement.Wind) => "Dust Wall",
                    (HeroElement.Frost, HeroElement.Wind) => "Hoarfrost Gale",
                    _ => "",
                },
                _ => "",
            };
        // (the pairs come sorted: wind, nature, water for the Vitalist; earth, frost, wind for the Aegis)
        const HeroElement W = HeroElement.Wind, N = HeroElement.Nature, Aq = HeroElement.Water, E = HeroElement.Earth, F = HeroElement.Frost, D = HeroElement.Dark;
        // (the Rogue's sorted: wind, nature, dark)
        if (h == HeroKind.Rogue)
            return slot switch
            {
                ElemSlot.Attack => (a, b) switch
                {
                    (D, D) => "Nightfall", (W, W) => "Flurry", (N, N) => "Deathcap",
                    (W, N) => "Thornwind", (W, D) => "Shade Step", (N, D) => "Nightshade", _ => "",
                },
                ElemSlot.Move => (a, b) switch
                {
                    (D, D) => "Umbral Veil", (W, W) => "Wind Walk", (N, N) => "Overgrowth Veil",
                    (W, N) => "Leaf Storm", (W, D) => "Shadow Dash", (N, D) => "Nightbloom", _ => "",
                },
                ElemSlot.Aid => (a, b) switch
                {
                    (D, D) => "Umbral Knife", (W, W) => "Fan of Knives", (N, N) => "Thorn Knife",
                    (W, N) => "Pollen Knife", (W, D) => "Shadow Tether", (N, D) => "Nightshade Knife", _ => "",
                },
                _ => "",
            };
        if (h == HeroKind.Vitalist)
            return slot switch
            {
                ElemSlot.Attack => (a, b) switch
                {
                    (N, N) => "Bramble Lash", (W, W) => "Whirling Siphon", (Aq, Aq) => "Riptide",
                    (W, N) => "Pollen Drain", (N, Aq) => "Life Spring", (W, Aq) => "Rain Siphon", _ => "",
                },
                ElemSlot.Move => (a, b) switch
                {
                    (N, N) => "Strangling Grove", (W, W) => "Eye of the Tempest", (Aq, Aq) => "Maelstrom",
                    (W, N) => "Spore Storm", (N, Aq) => "Mire Bloom", (W, Aq) => "Squall", _ => "",
                },
                ElemSlot.Aid => (a, b) switch
                {
                    (N, N) => "Bloom of Life", (W, W) => "Second Wind", (Aq, Aq) => "Wellspring",
                    (W, N) => "Seeds on the Wind", (N, Aq) => "Rejuvenation", (W, Aq) => "Mist of Mending", _ => "",
                },
                _ => "",
            };
        if (h == HeroKind.Aegis)
            return slot switch
            {
                ElemSlot.Attack => (a, b) switch
                {
                    (E, E) => "Boulder Bubble", (F, F) => "Frost Orb", (W, W) => "Bubble Volley",
                    (E, F) => "Hail Bubble", (E, W) => "Sand Bubble", (F, W) => "Snow Bubble", _ => "",
                },
                ElemSlot.Move => (a, b) switch
                {
                    (E, E) => "Fortress", (F, F) => "Glacier Dome", (W, W) => "Gale Dome",
                    (E, F) => "Crystal Dome", (E, W) => "Dust Dome", (F, W) => "Blizzard Dome", _ => "",
                },
                ElemSlot.Aid => (a, b) switch
                {
                    (E, E) => "Bulwark Ward", (F, F) => "Glacial Ward", (W, W) => "Twin Gale",
                    (E, F) => "Permafrost Ward", (E, W) => "Sandstorm Ward", (F, W) => "Winter Ward", _ => "",
                },
                _ => "",
            };
        if (h != HeroKind.Elementalist) return "";
        return slot switch
        {
            ElemSlot.Attack => (a, b) switch
            {
                (HeroElement.Fire, HeroElement.Fire) => "Meteorite",
                (HeroElement.Frost, HeroElement.Frost) => "Cryometeor",
                (HeroElement.Fire, HeroElement.Wind) => "Fire Barrage",
                (HeroElement.Frost, HeroElement.Wind) => "Frost Barrage",
                (HeroElement.Wind, HeroElement.Wind) => "Strong Gust",
                (HeroElement.Fire, HeroElement.Frost) => "Frostfire Bolts",
                _ => "",
            },
            ElemSlot.Move => (a, b) switch
            {
                (HeroElement.Fire, HeroElement.Fire) => "Flamebreath",
                (HeroElement.Frost, HeroElement.Frost) => "Frost Tower",
                (HeroElement.Fire, HeroElement.Wind) => "Fire Barrier",
                (HeroElement.Frost, HeroElement.Wind) => "Frost Barrier",
                (HeroElement.Wind, HeroElement.Wind) => "Sky Lift",
                (HeroElement.Fire, HeroElement.Frost) => "Steam Veil",
                _ => "",
            },
            ElemSlot.Heavy => (a, b) switch
            {
                (HeroElement.Fire, HeroElement.Fire) => "Rain of Fire",
                (HeroElement.Frost, HeroElement.Frost) => "Hailstorm",
                (HeroElement.Fire, HeroElement.Wind) => "Fire Storm",
                (HeroElement.Frost, HeroElement.Wind) => "Blizzard",
                (HeroElement.Wind, HeroElement.Wind) => "Tornado",
                (HeroElement.Fire, HeroElement.Frost) => "Frostfire Lance",
                _ => "",
            },
            _ => (a, b) switch
            {
                (HeroElement.Fire, HeroElement.Fire) => "Bonfire",
                (HeroElement.Frost, HeroElement.Frost) => "Ice Armor",
                (HeroElement.Wind, HeroElement.Wind) => "Gale Draft",
                (HeroElement.Fire, HeroElement.Frost) => "Thermal Armor",
                (HeroElement.Fire, HeroElement.Wind) => "Wildfire",
                (HeroElement.Frost, HeroElement.Wind) => "Frostwind",
                _ => "",
            },
        };
    }

    /// <summary>A pair's colour: one element's, or the two blended.</summary>
    public static Color DualColor(HeroElement x, HeroElement y) => x == y ? ColorOf(x) : ColorOf(x).Lerp(ColorOf(y), 0.5f);
}
