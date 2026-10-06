using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The relics the party carries this run, by player. Some change the world itself (more chests,
/// a second vault, how many levels lie above the dragon), so every game keeps the whole party's
/// list: a relic taken is announced to the others.
/// </summary>
public static class RunRelics
{
    private static readonly Dictionary<int, HashSet<string>> By = new();

    public static void Reset() => By.Clear();

    /// <summary>This game's hero took a relic: remember it and tell the others.</summary>
    public static void Take(string id)
    {
        Note(Net.Me, id);
        // (a relic is its bearer's alone: only the few that change the world, or the chests, are told to the others)
        if (Worldly.Contains(id)) NetSync.SendRelic(id);
    }

    /// <summary>The relics whose effect reaches everyone (how the caves are built, the road to the dragon) or decides the party's chests.</summary>
    private static readonly HashSet<string> Worldly = new() { "relic_deeper", "relic_shortcut", "relic_locksmith", "relic_treasure", "relic_prodigy" };

    public static void Note(int peer, string id)
    {
        if (!By.TryGetValue(peer, out var set)) By[peer] = set = new HashSet<string>();
        set.Add(id);
    }

    /// <summary>How many of the party carry it.</summary>
    public static int Count(string id) => By.Values.Count(s => s.Contains(id));
    /// <summary>The relics this game's hero carries, in the order taken.</summary>
    public static IEnumerable<string> Mine => By.TryGetValue(Net.Me, out var s) ? s : System.Linq.Enumerable.Empty<string>();
    public static bool Has(int peer, string id) => By.TryGetValue(peer, out var s) && s.Contains(id);

    /// <summary>Levels added to (or taken from) the way down to the dragon.</summary>
    public static int DepthDelta => Count("relic_deeper") - Count("relic_shortcut");
    /// <summary>Each hunter's map: this many more chests.</summary>
    public static float ChestBonus => Tune.Relics.ChestBonus * Count("relic_treasure");
    /// <summary>A locksmith's ring adds a second vault (and its key) to every level.</summary>
    public static bool ExtraVault => Count("relic_locksmith") > 0;
    /// <summary>How much tougher the dragon is (a hasty descent).</summary>
    public static float DragonMult => 1f + Tune.Relics.ShortcutDragon * Count("relic_shortcut");
}

public static partial class Upgrades
{
    private const string Ic = "relic";
    // (own copies: the order partial parts of a class initialise in isn't guaranteed)
    private static readonly HeroKind[] HS = { HeroKind.Swordsman }, HW = { HeroKind.Warden }, HV = { HeroKind.Vitalist }, HE = { HeroKind.Elementalist }, HR = { HeroKind.Rogue }, HA = { HeroKind.Aegis }, HSH = { HeroKind.ShapeShifter };

    private static Upgrade Rl(string id, string name, string desc, Action<PlayerStats, Player> apply, HeroKind[] only = null, float weight = 1f)
        => new() { Id = id, Name = name, Desc = desc, Icon = Ic, Relic = true, For = only, Weight = weight, Apply = apply, Tier = UpgradeTier.Rare, Multi = MultiOnly.Contains(id) };

    /// <summary>Relics that only matter with others about: they pull or turn away the creatures' eyes, or carry a friend's blows.</summary>
    private static readonly HashSet<string> MultiOnly = new() { "relic_bait", "relic_shroud", "relic_w_banner", "relic_a_mantle" };

    /// <summary>Relics: bargains and twists, found in some chests. Each can be carried once.</summary>
    public static readonly List<Upgrade> Relics = new()
    {
        // --- for anyone ---
        Rl("relic_leap", "Long Stride", "Jump 20% lower, but move 40% faster through the air.", (s, p) => { s.JumpMult *= 0.8f; s.AirSpeedMult *= 1.4f; }),
        Rl("relic_anvil", "Anvil Grip", "Attack 20% slower, but hit 20% harder.", (s, p) => { s.AttackSpeed *= 0.8f; s.DamageMult *= 1.2f; }),
        Rl("relic_quicksilver", "Quicksilver Grip", "Attack 20% faster, but hit 20% softer.", (s, p) => { s.AttackSpeed *= 1.2f; s.DamageMult *= 0.8f; }),
        Rl("relic_amphibian", "Amphibian Charm", "Move 30% faster in water, but 15% slower on land.", (s, p) => { s.SwimSpeed *= 1.3f; s.MoveSpeed *= 0.85f; }),
        Rl("relic_flask", "Deep Flask", "Carry one more potion, and creatures are 1% likelier to drop one.", (s, p) => { s.ExtraPotions += 1; s.PotionChanceBonus += 0.01f; }),
        Rl("relic_prodigy", "Prodigy's Brand", "You level up twenty times at once, but can no longer pick up relics or open the chests in vaults.", (s, p) => { s.NoRelics = true; p?.GainLevels(20); }),
        Rl("relic_treasure", "Hunter's Map", "20% more chests in every cave, but no chest of your own when a guardian falls.", (s, p) => { }, weight: 0.8f),
        Rl("relic_fount", "Fount of Vigor", "Every level you gain heals you to full.", (s, p) => s.LevelHeal = true),
        Rl("relic_spring", "Spring Water", "Going deeper heals you to full.", (s, p) => s.DepthHeal = true),
        Rl("relic_locksmith", "Locksmith's Ring", "Every cave holds a second vault, and one more key to open it.", (s, p) => { }, weight: 0.7f),
        Rl("relic_deeper", "Deeper Dark", "One more level lies between here and the dragon.", (s, p) => { }, weight: 0.6f),
        Rl("relic_shortcut", "Hasty Descent", "One level fewer before the dragon, which is 10% tougher for it.", (s, p) => { }, weight: 0.6f),
        Rl("relic_assassin", "Assassin's Edge", "Hit creatures' backs 20% harder, but take 20% more from blows at yours.", (s, p) => { s.BackDealMult *= 1.2f; s.BackTakenMult *= 1.2f; }),
        Rl("relic_sniper", "Far Sight", "Hit far-off creatures up to 20% harder, and near ones as much softer.", (s, p) => s.DistanceBias += 0.2f),
        Rl("relic_brawler", "Close Quarters", "Hit near creatures up to 20% harder, and far ones as much softer.", (s, p) => s.DistanceBias -= 0.2f),
        Rl("relic_bait", "Gaudy Charm", "Creatures see you 10% closer than you are: you draw their eyes.", (s, p) => s.ThreatDist *= 0.9f),
        Rl("relic_shroud", "Dim Cloak", "Creatures see you 10% farther than you are: they look to others first.", (s, p) => s.ThreatDist *= 1.1f),

        // --- Elementalist ---
        Rl("relic_e_blood", "Blood Channeling", "Spells you lack the alimus for are paid for with your own health instead.", (s, p) => s.BloodCast = true, HE, 1.5f),
        Rl("relic_e_ember", "Ember Heart", "Your fire is 25% likelier to set creatures alight, but your alimus comes back 20% slower.", (s, p) => { s.IgniteChance += 0.25f; s.AlimusRegenMult *= 0.8f; }, HE, 1.5f),
        Rl("relic_e_rime", "Rime Heart", "Your frost is 12% likelier to freeze a creature solid, but your fire sets nothing alight.", (s, p) => { s.FreezeBonus += 0.12f; s.IgniteChance = 0f; }, HE, 1.5f),
        Rl("relic_e_vessel", "Overflowing Vessel", "50% more alimus to hold, but it comes back 25% slower.", (s, p) => { s.AlimusMax *= 1.5f; s.AlimusRegenMult *= 0.75f; p?.SetAlimus(s.AlimusMax); }, HE, 1.5f),

        // --- Vitalist ---
        Rl("relic_v_fury", "Bloodlust", "Hit 25% harder, but gather 30% less vital force.", (s, p) => { s.DamageMult *= 1.25f; s.VitalForceGain *= 0.7f; }, HV, 1.5f),
        Rl("relic_v_siphon", "Siphon Crystal", "Gather 30% more vital force, but hit 25% softer.", (s, p) => { s.VitalForceGain *= 1.3f; s.DamageMult *= 0.75f; }, HV, 1.5f),
        Rl("relic_v_well", "Deep Well", "Hold 50% more vital force, but heals cost 25% more of it.", (s, p) => { s.VitalForceMax *= 1.5f; s.HealCostMult *= 1.25f; }, HV, 1.5f),

        // --- Swordsman ---
        Rl("relic_s_fire", "Lingering Fire", "Charged Strike burns for 5 s (every swing is charged) instead of one swing, but takes 60% longer to come back.", (s, p) => { s.ChargeDuration = 5f; s.ChargeCooldown *= 1.6f; }, HS, 1.5f),
        Rl("relic_s_quake", "Earthshaker", "Your heaving swing hits 50% harder, but takes 40% longer to come back.", (s, p) => { s.HeaveDamageMult *= 1.5f; s.HeaveCooldown *= 1.4f; }, HS, 1.5f),
        Rl("relic_s_sash", "Featherfoot Sash", "Your dodge roll comes back 30% sooner, but you have 10% less health.", (s, p) => { s.DodgeCdMult *= 0.7f; s.MaxHp *= 0.9f; if (p != null) p.Hp = Math.Min(p.Hp, s.MaxHp); }, HS, 1.5f),

        // --- Warden ---
        Rl("relic_w_siphon", "Siphoning Bulwark", "A quarter of the damage you deal recharges your shield.", (s, p) => s.ShieldSiphon += 0.25f, HW, 1.5f),
        Rl("relic_w_ram", "Juggernaut's Charge", "Your Guarded Charge no longer stops for what it meets: it runs its whole length and strikes everything in its way.", (s, p) => s.Juggernaut = true, HW, 1.5f),
        Rl("relic_w_plate", "Tower Plate", "Your shield is 30% stronger, but you move 10% slower.", (s, p) => { s.ShieldMult *= 1.3f; s.MoveSpeed *= 0.9f; }, HW, 1.5f),
        Rl("relic_w_banner", "Warbanner", "Creatures see you 20% closer than you are, and you hit 10% harder.", (s, p) => { s.ThreatDist *= 0.8f; s.DamageMult *= 1.1f; }, HW, 1.5f),

        // --- Aegis ---
        Rl("relic_sh_hide", "Second Skin", "Every form shrugs off 12% more of the blows it takes, but your staff hits 20% softer.", (s, p) => { s.FormArmorAdd += 0.12f; s.PrimaryDamageMult *= 0.8f; }, HSH, 1.5f),
        Rl("relic_sh_fang", "Borrowed Fangs", "Every form's attacks hit 20% harder, but Shift comes back 30% slower.", (s, p) => { s.FormDmgMult += 0.2f; s.AbilityCdMult *= 1.3f; }, HSH, 1.5f),
        Rl("relic_sh_echo", "Echoing Voice", "Form specials come back 30% sooner, but you take 10% more damage.", (s, p) => { s.FormSpecCdMult *= 0.7f; s.DamageTakenMult *= 1.1f; }, HSH, 1.5f),
        Rl("relic_a_crest", "Solar Crest", "Your ward bolt mends you for 10% more of its damage, but hits 15% softer.", (s, p) => { s.AegisLifesteal += 0.10f; s.DamageMult *= 0.85f; }, HA, 1.5f),
        Rl("relic_a_mantle", "Martyr's Mantle", "You carry 35% of the blows of a friend under your Shared Burden, and take 10% more damage yourself.", (s, p) => { s.BurdenShare = Math.Max(s.BurdenShare, 0.35f); s.DamageTakenMult *= 1.1f; }, HA, 1.5f),
        Rl("relic_a_prism", "Prism", "Your bolt's burst reaches 50% farther and the creatures in it take the whole blow.", (s, p) => { s.BurstMult *= 1.5f; s.BurstShare = 1f; }, HA, 1.5f),
        Rl("relic_a_sea", "Bottled Sea", "Your bubble absorbs three quarters of every blow, but bursts after absorbing 30% less.", (s, p) => { s.BubbleAbsorb = 0.75f; s.BubbleMult *= 0.7f; }, HA, 1.5f),

        // --- Rogue ---
        Rl("relic_r_lone", "Lone Blade", "Only one dagger flies, and only with both home: the throw hits twice as hard, and the recall half again as hard.", (s, p) => { s.LoneThrow = true; s.ThrowDamageMult *= 2f; s.RecallDamageMult *= 1.5f; }, HR, 1.5f),
        Rl("relic_r_hemo", "Hemorrhage", "A recall always makes its creature bleed.", (s, p) => { s.RecallBleed = true; s.RecallBleedChance = 1f; }, HR, 1.5f),
        Rl("relic_r_wound", "Undying Wound", "A recall's bleed never stops until the creature is dead.", (s, p) => { s.RecallBleed = true; s.BleedForever = true; }, HR, 1.5f),
        Rl("relic_r_quick", "Quickfingers", "Daggers are ready again in 60% of the time, but thrown ones hit 15% softer.", (s, p) => { s.ThrowCdMult *= 0.6f; s.ThrowDamageMult *= 0.85f; }, HR, 1.5f),
    };


    /// <summary>A relic this hero could be offered (not carried yet; generic or theirs).</summary>
    public static List<Upgrade> RelicPool(PlayerStats s, IEnumerable<string> not = null)
        => Relics.Where(u => Available(u, s) && (not == null || !not.Contains(u.Id))).ToList();

    /// <summary>One relic for this hero's chest (null when none are left).</summary>
    public static Upgrade RollRelic(PlayerStats s, Random rng, IEnumerable<string> not = null)
        => Pick(RelicPool(s, not), 1, rng, u => u.Weight).FirstOrDefault();
}
