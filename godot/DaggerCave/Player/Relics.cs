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
    private static readonly HashSet<string> Worldly = new() { "relic_deeper", "relic_shortcut", "relic_locksmith", "relic_treasure", "relic_kidding" };

    /// <summary>This game's hero gave a relic up (traded it to the merchant).</summary>
    public static void Forget(string id)
    {
        if (By.TryGetValue(Net.Me, out var set)) set.Remove(id);
    }

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
    private static readonly HeroKind[] HS = { HeroKind.Swordsman }, HW = { HeroKind.Warden }, HV = { HeroKind.Vitalist }, HE = { HeroKind.Elementalist }, HR = { HeroKind.Rogue }, HA = { HeroKind.Aegis }, HSH = { HeroKind.ShapeShifter }, HAU = { HeroKind.Automaton }, HMELEE = { HeroKind.Swordsman, HeroKind.Warden, HeroKind.Rogue, HeroKind.Automaton };

    /// <summary>How often a relic of a rarity is dealt (the weight it is drawn at): common 1.2, uncommon 1, rare 0.6, legendary 0.3.</summary>
    public static float RarityWeight(RelicRarity r) => r switch { RelicRarity.Common => 1.2f, RelicRarity.Uncommon => 1f, RelicRarity.Rare => 0.6f, _ => 0.3f };

    private static Upgrade Rl(string id, string name, string desc, Action<PlayerStats, Player> apply, HeroKind[] only = null, float weight = 1f, RelicRarity rarity = RelicRarity.Uncommon, string tags = "")
        => new() { Id = id, Name = name, Desc = desc, Icon = Ic, Relic = true, For = only, Rarity = rarity, Weight = weight * RarityWeight(rarity), Apply = apply, Tier = UpgradeTier.Rare, Multi = MultiOnly.Contains(id), Tags = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries) };

    /// <summary>A bargain: a plain stat swapped for another (taken once, from a vault).</summary>
    private static Upgrade Bg(string id, string name, string desc, Action<PlayerStats, Player> apply, HeroKind[] only = null, float weight = 0.8f)
        => new() { Id = id, Name = name, Desc = desc, Icon = "risk", For = only, Weight = weight, Apply = apply, Tags = new[] { "bargain" } };

    /// <summary>Relics that only matter with others about: they pull or turn away the creatures' eyes, or carry a friend's blows.</summary>
    private static readonly HashSet<string> MultiOnly = new() { "relic_bait", "relic_shroud", "relic_w_banner", "relic_a_mantle", "relic_last_rites", "relic_phoenix", "relic_healer", "relic_fastres", "relic_kidding" };

    /// <summary>Relics: bargains and twists, found in some chests. Each can be carried once.</summary>
    public static readonly List<Upgrade> Relics = new()
    {
        // --- for anyone ---
        Rl("relic_treasure", "Hunter's Map", "20% more chests in every cave, but no chest of your own when a guardian falls.", (s, p) => { }, rarity: RelicRarity.Uncommon),
        Rl("relic_fount", "Fount of Vigor", "Every level you gain heals you for 20% of your health.", (s, p) => s.LevelHeal = true, rarity: RelicRarity.Rare),
        Rl("relic_spring", "Spring Water", "Each depth you advance heals you for 20% of your health.", (s, p) => s.DepthHeal = true, rarity: RelicRarity.Uncommon),
        Rl("relic_locksmith", "Locksmith's Ring", "Every cave holds a second vault, and one more key to open it.", (s, p) => { }, rarity: RelicRarity.Rare),
        Rl("relic_deeper", "Deeper Dark", "One more level lies between here and the dragon.", (s, p) => { }, rarity: RelicRarity.Rare),
        Rl("relic_shortcut", "Hasty Descent", "One level fewer before the dragon, which is 10% tougher for it.", (s, p) => { }, rarity: RelicRarity.Rare),
        Rl("relic_bait", "Gaudy Charm", "Creatures see you 20% closer than you are: you draw their eyes.", (s, p) => s.ThreatDist *= 0.8f, rarity: RelicRarity.Common),
        Rl("relic_shroud", "Dim Cloak", "Creatures see you 20% farther than you are: they look to others first.", (s, p) => s.ThreatDist *= 1.2f, rarity: RelicRarity.Common),

        Rl("relic_lunge", "Hawk's Pursuit", "Melee lunges are twice as strong and last longer (even for those who never lunged), and in the air a swing hangs you in place and pulls you after the creature in its way: you hover and hunt on the wing. But you hit 10% softer.", (s, p) => { s.LungeMult *= 2f; s.AerialLunge = true; s.DamageMult *= 0.9f; }, HMELEE, rarity: RelicRarity.Rare),

        Rl("relic_struck_fire", "Cinder Brand", "When a boss or mini-boss strikes you, there is a 20% chance it is set alight.", (s, p) =>
        {
            s.StruckIgnite = Tune.Relics.StruckIgniteChance;
            s.On.Hurt += (pl, e, dmg) =>
            {
                if (e == null || e.Dead || !(e.IsBoss || e.IsGuardian || e.Elite)) return;
                if (pl.Stats.StruckIgnite > 0f && G.Chance(pl.Stats.StruckIgnite)) { e.Ignite(Tune.Relics.StruckIgniteDps * pl.Stats.DamageMult, Tune.Relics.StruckIgniteSeconds); pl.Bump("ignited"); }
            };
        }, rarity: RelicRarity.Uncommon, tags: "on-hurt status"),
        Rl("relic_struck_frost", "Hoarfrost Mail", "When a regular creature strikes you, there is a 15% chance it is frozen solid.", (s, p) =>
        {
            s.StruckFreeze = Tune.Relics.StruckFreezeChance;
            s.On.Hurt += (pl, e, dmg) =>
            {
                if (e == null || e.Dead || e.IsBoss || e.IsGuardian || e.Elite) return;
                // (it catches the very creature that just struck, even mid-attack, where other freezes never do)
                if (pl.Stats.StruckFreeze > 0f && G.Chance(pl.Stats.StruckFreeze) && e.FreezeSolid(Tune.Relics.StruckFreezeSeconds, force: true)) pl.Bump("froze");
            };
        }, rarity: RelicRarity.Uncommon, tags: "on-hurt status"),

        Rl("relic_echo", "Echoing Blade", "When your blade lands, there is a 20% chance it strikes again for half the damage, with everything a strike brings.", (s, p) => s.EchoStrike = Tune.Relics.EchoChance, HMELEE, 0.8f, rarity: RelicRarity.Rare),

        Rl("relic_trophy", "Trophy Hunter", "Every kill heals you 2 HP.", (s, p) => s.HealOnKill += 2f, rarity: RelicRarity.Common, tags: "on-kill heal"),

        // ---- on a kill
        Rl("relic_soul", "Soul Lantern", "Creatures you kill explode for 10% of their health on those about them.", (s, p) => s.On.Kill += RelicEffects.SoulBurst, rarity: RelicRarity.Rare, tags: "on-kill offence"),
        Rl("relic_hunger", "Hunger", "Every kill gives your reserve a little back (vital force, alimus, fuel, shield; a little health for the rest).", (s, p) => s.On.Kill += (pl, e) => pl.GainReserve(3f), rarity: RelicRarity.Common, tags: "on-kill resource"),
        Rl("relic_bounty", "Bounty Hunter", "A kill has a 15% chance to drop two or three gold.", (s, p) => s.On.Kill += RelicEffects.Bounty, rarity: RelicRarity.Common, tags: "on-kill economy"),
        Rl("relic_wither", "Wither", "A creature that dies burning or poisoned passes it on to those about it, at its full length again.", (s, p) => s.On.Kill += RelicEffects.Wither, rarity: RelicRarity.Common, tags: "on-kill status"),

        // ---- on a hit
        Rl("relic_mark", "Tracking Mark", "A creature you hit is marked for 4 s: it takes 10% more from you.", (s, p) => { s.MarkBonus = 0.1f; s.On.Hit += (pl, e, dealt, k) => e.MarkedUntil = (float)(Time.GetTicksMsec() / 1000.0 + 4.0); }, rarity: RelicRarity.Common, tags: "on-hit offence"),
        Rl("relic_spill", "Spillover", "A blow that kills spills 30% of its damage onto the nearest other creature.", (s, p) => s.On.Hit += (pl, e, dealt, k) => { if (e.Dead) RelicEffects.Spill(pl, e, dealt); }, rarity: RelicRarity.Common, tags: "on-hit offence"),

        // ---- on the dodge button, on a blow taken, at a fall, a level, a chest
        Rl("relic_riposte", "Riposte", "After you use the dodge button, your next blow within 3 s hits 50% harder.", (s, p) => s.On.Dodge += pl => pl.ArmNextHit(1.5f, 3f), rarity: RelicRarity.Uncommon, tags: "on-dodge offence"),
        Rl("relic_retribution", "Retribution", "When struck, your next blow within 4 s hits 30% harder.", (s, p) => s.On.Hurt += (pl, e, dmg) => pl.ArmNextHit(1.3f, 4f), rarity: RelicRarity.Uncommon, tags: "on-hurt offence"),
        Rl("relic_scavenger", "Scavenger", "Opening a chest mends 10% of your health and gives 5 gold.", (s, p) => s.On.Chest += pl => { pl.Heal(pl.Stats.MaxHp * 0.1f); pl.GainGold(5); }, rarity: RelicRarity.Uncommon, tags: "on-chest economy heal"),

        // ---- rules
        Rl("relic_hardcase", "Hard Case", "No single blow can take more than 30% of your health.", (s, p) => s.BlowCap = 0.3f, rarity: RelicRarity.Rare, tags: "defence"),
        Rl("relic_berserk", "Berserker's Pulse", "Below a quarter of your health you hit 35% harder and take 20% less.", (s, p) => { s.LowHpDealMult = 1.35f; s.LowHpTakenMult = 0.8f; }, rarity: RelicRarity.Rare, tags: "offence defence"),
        Rl("relic_deathward", "Death Ward", "Once a depth, a blow that would kill you leaves you at 1 health, untouchable for two seconds.", (s, p) => s.DeathWard = true, rarity: RelicRarity.Legendary, tags: "defence"),
        Rl("relic_highground", "High Ground", "You hit creatures below you 20% harder.", (s, p) => s.HighGroundMult = 1.2f, rarity: RelicRarity.Uncommon, tags: "offence"),
        Rl("relic_frostfire", "Frostfire", "Fire hits frozen or chilled creatures, and frost hits burning ones, 30% harder.", (s, p) => s.Frostfire = 1.3f, rarity: RelicRarity.Uncommon, tags: "offence status"),
        Rl("relic_lucky", "Lucky Coin", "Gold you find is worth 30% more, and creatures are 1% likelier to drop a potion.", (s, p) => { s.GoldMult *= 1.3f; s.PotionChanceBonus += 0.01f; }, rarity: RelicRarity.Common, tags: "economy"),
        // ---- the merchant's (gold, and what it buys)
        Rl("relic_fatpurse", "Fat Purse", "Every pile of gold holds 4 more coins.", (s, p) => s.PileBonus += 4, rarity: RelicRarity.Common, tags: "economy shop"),
        Rl("relic_overstock", "Overstock", "The merchant has one additional item for sale.", (s, p) => s.ShopExtra += 1, rarity: RelicRarity.Uncommon, tags: "economy shop"),
        Rl("relic_haggle", "Haggler's Tongue", "Everything the merchant sells costs 20% less.", (s, p) => s.ShopPriceMult *= 0.8f, rarity: RelicRarity.Uncommon, tags: "economy shop"),
        Rl("relic_dice", "Loaded Dice", "At every shop you may reroll once for free.", (s, p) => s.FreeRerolls += 1, rarity: RelicRarity.Uncommon, tags: "economy shop"),
        Rl("relic_ledger", "Fence's Ledger", "Having the merchant's wares rerolled costs 40% less.", (s, p) => s.RerollMult *= 0.6f, rarity: RelicRarity.Common, tags: "economy shop"),
        Rl("relic_purse", "Guardian's Purse", "A fallen guardian leaves 20 gold.", (s, p) => s.GuardianGold += 20, rarity: RelicRarity.Common, tags: "economy"),
        Rl("relic_interest", "Compound Interest", "The gold you carry grows by 10% (at least 1) each time you advance a depth.", (s, p) => s.GoldInterest += 0.1f, rarity: RelicRarity.Rare, tags: "economy"),
        Rl("relic_midas", "Midas Mark", "You hit 1% harder for every 25 gold you carry (at most 100% more).", (s, p) => s.GoldDamageStep = 0.01f, rarity: RelicRarity.Rare, tags: "offence economy"),

        // ---- for one hero
        Rl("relic_v_gorge", "Gorge", "Every kill gives you 4 vital force.", (s, p) => s.On.Kill += (pl, e) => pl.GainVitalForce(4f), HV, 1.5f, RelicRarity.Common, "on-kill resource"),
        Rl("relic_sh_adapt", "Adaptive Hide", "Returning to your own shape heals you for 15% of your health over 6 s.", (s, p) => s.On.Unshift += pl => pl.GiveMending(pl.Stats.MaxHp * 0.15f, 6f, 0f), HSH, 1.5f, RelicRarity.Uncommon, "on-shift heal"),
        Rl("relic_e_overload", "Overload", "While your alimus is nearly full, your fire and frost hit 15% harder.", (s, p) => s.ArcaneFlow = 1.15f, HE, 1.5f, RelicRarity.Uncommon, "offence"),
        Rl("relic_au_scrap", "Scrap Heap", "Every kill gives you 4 fuel.", (s, p) => s.On.Kill += (pl, e) => pl.GainFuel(4f), HAU, 1.5f, RelicRarity.Common, "on-kill resource"),

        // (uncommon: the ordinary weight; the rare ones above are likelier to be passed over)
        Rl("relic_last_rites", "Last Rites", "When you fall, every living ally is healed by 20.", (s, p) =>
        {
            s.FallHealAllies = Tune.Relics.FallHeal;
            s.On.Fall += pl =>
            {
                foreach (var h in G.Players.ToArray())
                {
                    if (h == pl || !GodotObject.IsInstanceValid(h) || h.Dead) continue;
                    h.Heal(pl.Stats.FallHealAllies);
                    G.Fx.Ring(h.GlobalPosition, 20, Player.HealColorLight, 0.4f, FxLayer.RingStyle.Wave);
                    pl.Bump("fall_healed");
                }
            };
        }, rarity: RelicRarity.Uncommon, tags: "on-fall party heal"),
        Rl("relic_phoenix", "Phoenix Ash", "When a friend gets you back on your feet, you rise with 20% more health.", (s, p) => s.ReviveBonus += Tune.Relics.ReviveExtra, rarity: RelicRarity.Uncommon),
        Rl("relic_healer", "Healer's Hands", "When you get a fallen friend back on their feet, they rise with 20% more health.", (s, p) => s.ReviveGiveBonus += Tune.Relics.ReviveExtra, rarity: RelicRarity.Uncommon),
        Rl("relic_fastres", "Fast Res", "You raise a fallen friend 40% faster.", (s, p) => s.ReviveSpeed += Tune.Relics.ReviveFaster, rarity: RelicRarity.Uncommon, tags: "party"),
        Rl("relic_kidding", "Just Kidding", "When you have fallen, friends raise you 40% faster.", (s, p) => { }, rarity: RelicRarity.Uncommon, tags: "party"),

        // --- Elementalist ---
        Rl("relic_e_blood", "Blood Channeling", "A spell's recharge that lacks the alimus to begin is paid for with your own health instead.", (s, p) => s.BloodCast = true, HE, 1.5f, rarity: RelicRarity.Uncommon),
        Rl("relic_e_rime", "Rime Heart", "Your frost is 12% likelier to freeze a creature solid, but your fire sets nothing alight.", (s, p) => { s.FreezeBonus += 0.12f; s.IgniteChance = 0f; }, HE, 1.5f, rarity: RelicRarity.Common),
        Rl("relic_e_burnheart", "Burning Heart", "Your fire burns for double the damage, but your frost can't freeze creatures solid.", (s, p) => { s.BurnMult *= 2f; s.NoFreeze = true; }, HE, 1.5f, rarity: RelicRarity.Common),

        // --- Vitalist ---

        // --- Swordsman ---
        Rl("relic_s_fire", "Lingering Fire", "Charged Strike burns for 5 s (every swing is charged) instead of one swing, but takes 60% longer to come back.", (s, p) => { s.ChargeDuration = 5f; s.ChargeCooldown *= 1.6f; }, HS, 1.5f, rarity: RelicRarity.Uncommon),

        // --- Warden ---
        Rl("relic_w_siphon", "Siphoning Bulwark", "A quarter of the damage you deal recharges your shield.", (s, p) => s.ShieldSiphon += 0.25f, HW, 1.5f, rarity: RelicRarity.Common),
        Rl("relic_w_ram", "Juggernaut's Charge", "Your Guarded Charge no longer stops for what it meets: it runs its whole length and strikes everything in its way.", (s, p) => s.Juggernaut = true, HW, 1.5f, rarity: RelicRarity.Uncommon),
        Rl("relic_w_banner", "Warbanner", "Creatures see you 20% closer than you are, and you hit 10% harder.", (s, p) => { s.ThreatDist *= 0.8f; s.DamageMult *= 1.1f; }, HW, 1.5f, rarity: RelicRarity.Common),

        // --- Aegis ---
        Rl("relic_a_mantle", "Martyr's Mantle", "You carry 20% more of the blows of a friend under your Shared Burden.", (s, p) => s.BurdenShare += 0.2f, HA, 1.5f, rarity: RelicRarity.Uncommon),
        Rl("relic_a_prism", "Prism", "Your bolt's burst reaches 20% farther, and the creatures in it take 20% more of the blow.", (s, p) => { s.BurstMult *= 1.2f; s.BurstShare = Math.Min(1f, s.BurstShare + 0.2f); }, HA, 1.5f, rarity: RelicRarity.Uncommon),

        // --- Automaton ---

        // --- Rogue ---
        Rl("relic_r_lone", "Lone Blade", "You can only throw one dagger, but it hits twice as hard, and its recall 1.5x as hard.", (s, p) => { s.LoneThrow = true; s.ThrowDamageMult *= 2f; s.RecallDamageMult *= 1.5f; }, HR, 1.5f, rarity: RelicRarity.Uncommon),
        Rl("relic_r_hemo", "Hemorrhage", "A recall always makes its creature bleed.", (s, p) => { s.RecallBleed = true; s.RecallBleedChance = 1f; }, HR, 1.5f, rarity: RelicRarity.Common),
        Rl("relic_r_wound", "Undying Wound", "A recall's bleed never stops until the creature is dead.", (s, p) => { s.RecallBleed = true; s.BleedForever = true; }, HR, 1.5f, rarity: RelicRarity.Rare),
    };


    /// <summary>The bargains: stat-for-stat swaps (they used to be relics). Vault cards: each taken once. Added to <see cref="Chest"/> with the others.</summary>
    public static readonly List<Upgrade> Bargains = new()
    {
        Bg("rr_leap", "Long Stride", "Jump 20% lower, but move 40% faster through the air.", (s, p) => { s.JumpMult *= 0.8f; s.AirSpeedMult *= 1.4f; }),
        Bg("rr_anvil", "Anvil Grip", "Attack 20% slower, but hit 20% harder.", (s, p) => { s.AttackSpeed *= 0.8f; s.DamageMult *= 1.2f; }),
        Bg("rr_quicksilver", "Quicksilver Grip", "Attack 20% faster, but hit 20% softer.", (s, p) => { s.AttackSpeed *= 1.2f; s.DamageMult *= 0.8f; }),
        Bg("rr_amphibian", "Amphibian Charm", "Move 35% faster in water, but 15% slower on land.", (s, p) => { s.SwimSpeed *= 1.35f; s.MoveSpeed *= 0.85f; }),
        Bg("rr_assassin", "Assassin's Edge", "Hit creatures' backs 25% harder, but take 15% more from blows at yours.", (s, p) => { s.BackDealMult *= 1.25f; s.BackTakenMult *= 1.15f; }),
        Bg("rr_sniper", "Far Sight", "Hit far-off creatures up to 20% harder, and near ones as much softer.", (s, p) => s.DistanceBias += 0.2f),
        Bg("rr_brawler", "Close Quarters", "Hit near creatures up to 20% harder, and far ones as much softer.", (s, p) => s.DistanceBias -= 0.2f),
        Bg("rr_e_ember", "Ember Heart", "Your fire is 25% likelier to set creatures alight, but your alimus comes back 20% slower.", (s, p) => { s.IgniteChance += 0.25f; s.AlimusRegenMult *= 0.8f; }, HE, 0.9f),
        Bg("rr_e_vessel", "Overflowing Vessel", "50% more alimus to hold, but it comes back 25% slower.", (s, p) => { s.AlimusMax *= 1.5f; s.AlimusRegenMult *= 0.75f; p?.SetAlimus(s.AlimusMax); }, HE, 0.9f),
        Bg("rr_v_fury", "Bloodlust", "Hit 25% harder, but gather 30% less vital force.", (s, p) => { s.DamageMult *= 1.25f; s.VitalForceGain *= 0.7f; }, HV, 0.9f),
        Bg("rr_v_siphon", "Siphon Crystal", "Gather 30% more vital force, but hit 25% softer.", (s, p) => { s.VitalForceGain *= 1.3f; s.DamageMult *= 0.75f; }, HV, 0.9f),
        Bg("rr_v_well", "Deep Well", "Hold 50% more vital force, but heals cost 25% more of it.", (s, p) => { s.VitalForceMax *= 1.5f; s.HealCostMult *= 1.25f; }, HV, 0.9f),
        Bg("rr_s_quake", "Earthshaker", "Your heaving swing hits 50% harder, but takes 40% longer to come back.", (s, p) => { s.HeaveDamageMult *= 1.5f; s.HeaveCooldown *= 1.4f; }, HS, 0.9f),
        Bg("rr_s_sash", "Featherfoot Sash", "Your dash-slash comes back 30% sooner, but slashes 20% softer.", (s, p) => { s.DodgeCdMult *= 0.7f; s.DashSlashMult *= 0.8f; }, HS, 0.9f),
        Bg("rr_w_plate", "Tower Plate", "Your shield is 30% stronger, but you move 10% slower.", (s, p) => { s.ShieldMult *= 1.3f; s.MoveSpeed *= 0.9f; }, HW, 0.9f),
        Bg("rr_sh_hide", "Second Skin", "Every form shrugs off 20% more of the blows it takes, but you take 15% more damage.", (s, p) => { s.FormArmorAdd += 0.2f; s.DamageTakenMult *= 1.15f; }, HSH, 0.9f),
        Bg("rr_sh_fang", "Borrowed Fangs", "Every form's attacks hit 20% harder, but your staff hits 20% softer.", (s, p) => { s.FormDmgMult += 0.2f; s.PrimaryDamageMult *= 0.8f; }, HSH, 0.9f),
        Bg("rr_sh_echo", "Echoing Voice", "Form specials come back 20% sooner, but you take 10% more damage.", (s, p) => { s.FormSpecCdMult *= 0.8f; s.DamageTakenMult *= 1.1f; }, HSH, 0.9f),
        Bg("rr_a_crest", "Solar Crest", "What your ward bolt weakens is weakened 10% more, but your bolt hits 15% softer.", (s, p) => { s.DebuffMult *= 0.9f; s.DamageMult *= 0.85f; }, HA, 0.9f),
        Bg("rr_a_sea", "Bottled Sea", "Your bubble absorbs half of every blow, but bursts after absorbing 20% less.", (s, p) => { s.BubbleAbsorb = 0.5f; s.BubbleMult *= 0.8f; }, HA, 0.9f),
        Bg("rr_au_boiler", "Overfired Boiler", "Steam Release comes back twice as fast, but you take 10% more damage.", (s, p) => { s.SteamCdMult *= 0.5f; s.DamageTakenMult *= 1.1f; }, HAU, 0.9f),
        Bg("rr_au_dynamo", "Dynamo", "Healing gives you 15% more of itself as fuel, but your cleaver hits 10% softer.", (s, p) => { s.FuelFromHeal += 0.15f; s.PrimaryDamageMult *= 0.9f; }, HAU, 0.9f),
        Bg("rr_au_ballast", "Ballast Tanks", "You take no shove at all, but you move 10% slower.", (s, p) => { s.KnockTakenMult = 0f; s.MoveSpeed *= 0.9f; }, HAU, 0.9f),
        Bg("rr_au_governor", "Loose Governor", "You swing 20% faster, but Self-Repair mends 30% slower.", (s, p) => { s.AttackSpeed += 0.2f; s.RepairRateMult *= 0.7f; }, HAU, 0.9f),
        Bg("rr_r_quick", "Quickfingers", "Daggers are ready again in 60% of the time, but thrown ones hit 15% softer.", (s, p) => { s.ThrowCdMult *= 0.6f; s.ThrowDamageMult *= 0.85f; }, HR, 0.9f),
    };

    static Upgrades()
    {
        Chest.AddRange(Bargains);
        // (the pairs that pull against each other: one of each pair, never both)
        foreach (var (a, b) in new[] { ("rr_anvil", "rr_quicksilver"), ("rr_sniper", "rr_brawler"), ("rr_v_fury", "rr_v_siphon"), ("relic_bait", "relic_shroud"), ("relic_e_rime", "relic_e_burnheart") })
        {
            Find(a).Excludes = Find(a).Excludes.Append(b).ToArray();
            Find(b).Excludes = Find(b).Excludes.Append(a).ToArray();
        }
        BuildReverts();
    }

    /// <summary>A relic this hero could be offered (not carried yet; generic or theirs).</summary>
    public static List<Upgrade> RelicPool(PlayerStats s, IEnumerable<string> not = null)
        => Relics.Where(u => Available(u, s) && (not == null || !not.Contains(u.Id))).ToList();

    /// <summary>One relic for this hero's chest (null when none are left).</summary>
    public static Upgrade RollRelic(PlayerStats s, Random rng, IEnumerable<string> not = null)
        => Pick(RelicPool(s, not), 1, rng, u => u.Weight).FirstOrDefault();
}
