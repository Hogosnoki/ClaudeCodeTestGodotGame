using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The playable heroes.</summary>
public enum HeroKind { Swordsman, Warden, Vitalist, Elementalist }

/// <summary>Everything upgrades can change about the hero.</summary>
public sealed class PlayerStats
{
    public readonly HeroKind Hero;

    public float MaxHp = Tune.Swordsman.StartHp;
    public float HurtInvuln = Tune.Hero.HurtInvuln;      // seconds of invulnerability after being struck
    public float DamageMult = 1f;
    public float AttackSpeed = 1f;       // swing / cast cooldown divisor
    /// <summary>Damage of the attack button only (a swing, the drain): Heavy Hand's side of the bargain.</summary>
    public float PrimaryDamageMult = 1f;
    /// <summary>The ability button (charged strike, Guarded Charge, heal): how many uses it holds, and
    /// how much longer each takes to come back (Twin Reserve).</summary>
    public int AbilityCharges = 1;
    public float AbilityCdMult = 1f;
    public float DaggerReach = 1f;       // swing reach (and bolt range) multiplier
    public float MoveSpeed = 1f;
    public float JumpMult = 1f;
    public float SwimSpeed = 1f;
    public float BreathMax = Tune.Hero.BreathSeconds;         // seconds
    public float DamageReduction = 0f;   // 0..1
    /// <summary>Everything that strikes you lands this many times as hard (the risk-reward bargains).</summary>
    public float DamageTakenMult = 1f;
    /// <summary>Healing you receive is multiplied by this (Drowned Lungs' price).</summary>
    public float HealingTakenMult = 1f;
    public float LifeSteal = 0f;         // fraction of damage dealt
    public float HealOnKill = 0f;        // hp per kill
    public int KnockbackLevel = 0;
    public bool WallJump, DoubleJump, AirDash, Pogo, ThirdCombo;
    /// <summary>Drowned Lungs: breath never runs out. Magma Skin: lava can be swum in, burning for a third as much.</summary>
    public bool InfiniteBreath, MagmaSkin;
    public float MagnetMult = 1f;
    /// <summary>How many times in a row a landed strike refunds the swing cooldown.</summary>
    public int ComboResets = Tune.Combat.ComboResetsBase;

    // swordsman: dodge + charged strike + heaving swing
    public float DodgeCdMult = 1f;
    public int DodgeCharges = 1;
    public bool DodgeIFrames;
    public float ChargeCooldown = Tune.Swordsman.ChargeCooldown;
    /// <summary>Swings each charged strike empowers.</summary>
    public int ChargeSwings = 1;
    public float WeakenMult = Tune.Swordsman.WeakenMult;
    public bool ChargeWave;
    public float BleedShare;             // Rending Edge
    public bool Execute, CrescentWave;
    public float HeaveCooldown = Tune.Swordsman.HeaveCooldown;
    /// <summary>Alterations: Relentless Charge (a charge empowers the whole next combo), Swift Heave
    /// (a charged heave is instant and works in the air), Counter Roll (rolling into a blow counters
    /// it) and Flowing Counter (a counter can start a combo).</summary>
    public bool RelentlessCharge, SwiftHeave, CounterRoll, CounterCombo;

    // warden: shield + Guarded Charge + shield bash
    public float ShieldMax = Tune.Warden.ShieldHp;
    public float ShieldRegen = Tune.Warden.ShieldRegen;
    public float ShieldBreakTime = Tune.Warden.ShieldBreakTime;
    public float ShieldArcMult = 1f;
    public float BlockShare = Tune.Warden.BlockShare;
    public bool PerfectReflect, PerfectSoak;
    public float DashCooldown = Tune.Warden.DashCooldown;
    public float DashTime = Tune.Warden.DashTime;
    public float DashDamageMult = 1f;
    public bool DashMend, ShieldThorns;
    public bool Stalwart, QuickMend, LastStand;
    public float BashCooldown = Tune.Warden.BashCooldown;
    /// <summary>Alterations: Unyielding Shield (never weakens or breaks, stops UnyieldingShare of a
    /// blow), Guardian's Charge (a barrier for a friend), Deflecting Bash (sends projectiles back).</summary>
    public bool Unyielding, GuardianCharge, DeflectingBash, DeflectDouble;
    public float UnyieldingShare = Tune.Warden.UnyieldingShare;
    public float BarrierAmount = Tune.Warden.BarrierAmount, BarrierSeconds = Tune.Warden.BarrierSeconds;

    // vitalist: drain, hex, heal, rupture
    public float AlimusMax = Tune.Vitalist.AlimusMax;
    public float AlimusGain = Tune.Vitalist.AlimusGain;
    public float HealMult = 1f, HealCostMult = 1f;
    /// <summary>Many Mouths: how many more creatures near the target each drain also strikes.</summary>
    public int DrainExtra;
    public float HexRadiusMult = 1f, HexSeconds = Tune.Vitalist.HexSeconds, HexCooldown = Tune.Vitalist.HexCooldown;
    public float HexRot;                 // damage per second to hexed creatures
    public float RuptureCostMult = 1f, RuptureRadiusMult = 1f, RuptureSplashMult = 1f;
    /// <summary>Alterations: Blight Burst or Endless Hex, Slow Mending (with Patient and Warding
    /// Mending), Lifebloom (with its pool).</summary>
    public bool BlightBurst, EndlessHex, SlowMending, PatientMending, WardingMending, Lifebloom, BloomPool;

    // elementalist: bolts, updraft, blizzard, snap
    public float AetherMax = Tune.Elementalist.AetherMax;
    /// <summary>Attunement: aether comes back this many times as fast.</summary>
    public float AetherRegenMult = 1f;
    /// <summary>A firebolt's chance of setting a creature alight (Kindling adds); Deep Chill's
    /// extra chance of freezing, for frostbolts and the blizzard alike.</summary>
    public float IgniteChance = Tune.Elementalist.IgniteChance, FreezeBonus;
    public float BlizzardSecondsMult = 1f, BlizzardWideMult = 1f, BlizzardCdMult = 1f;
    /// <summary>Shrapnel: snaps burst this much wider. Echo: each creature a snap bursts gives aether back.</summary>
    public float SnapWideMult = 1f;
    public bool SnapEcho;
    /// <summary>Alterations: Frostbolt (the bolts freeze instead of burning), Narrow Draft (a
    /// taller, narrower, longer updraft), Firestorm (a blizzard of fire), Cinder Snap (the snap
    /// bursts burning creatures instead of frozen ones).</summary>
    public bool Frostbolt, NarrowDraft, Firestorm, CinderSnap;

    public readonly Dictionary<string, int> Stacks = new();
    public int StackOf(string id) => Stacks.TryGetValue(id, out var n) ? n : 0;

    public PlayerStats(HeroKind hero = HeroKind.Swordsman)
    {
        Hero = hero;
        switch (hero)
        {
            case HeroKind.Warden:
                MoveSpeed = Tune.Warden.MoveMult; JumpMult = Tune.Warden.JumpMult;
                MaxHp = Tune.Warden.StartHp; DamageReduction = Tune.Warden.Armor; BreathMax += Tune.Warden.ExtraBreath;
                break;
            case HeroKind.Vitalist:
                MoveSpeed = Tune.Vitalist.MoveMult; JumpMult = Tune.Vitalist.JumpMult;
                MaxHp = Tune.Vitalist.StartHp;
                break;
            case HeroKind.Elementalist:
                MoveSpeed = Tune.Elementalist.MoveMult; JumpMult = Tune.Elementalist.JumpMult;
                MaxHp = Tune.Elementalist.StartHp;
                break;
            default:
                MoveSpeed = Tune.Swordsman.MoveMult; JumpMult = Tune.Swordsman.JumpMult;
                MaxHp = Tune.Swordsman.StartHp;
                break;
        }
    }
}

/// <summary>What kind of card an upgrade is (each has its own colour and label, and its own source).</summary>
public enum UpgradeKind
{
    /// <summary>Improves one of a hero's abilities (chests' class card, and milestones).</summary>
    Class,
    /// <summary>Changes how an ability works; one per ability (milestones only).</summary>
    Alteration,
    /// <summary>Anyone can use it (chests).</summary>
    Generic,
    /// <summary>Generic, but offered only once something else is true (chests).</summary>
    Conditional,
    /// <summary>Something given, something taken; each taken once (chests, until vaults).</summary>
    SideGrade,
}

public sealed class Upgrade
{
    public string Id, Name, Desc;
    /// <summary>Category used for the card's gem: blade, charge, move, dodge, shield, life, spell, risk.</summary>
    public string Icon;
    public int MaxStacks = 1;
    public string Requires;
    /// <summary>Another condition it waits for (a rare movement ability waits for its humbler cousin).</summary>
    public Func<PlayerStats, bool> When;
    public string[] Excludes = Array.Empty<string>();
    public float Weight = 1f;
    public Action<PlayerStats, Player> Apply;
    public UpgradeTier Tier = UpgradeTier.Common;
    /// <summary>Only these heroes can get it (null = all of them).</summary>
    public HeroKind[] For;
    /// <summary>The ability whose tree a class card grows on ("sword", "charge", "shield", "hex"...).</summary>
    public string Ability;
    /// <summary>It changes how its ability works (an ability takes one at most).</summary>
    public bool Alteration;

    public UpgradeKind Kind => Alteration ? UpgradeKind.Alteration
        : Icon == "risk" ? UpgradeKind.SideGrade
        : For != null ? UpgradeKind.Class
        : Requires != null || When != null ? UpgradeKind.Conditional
        : UpgradeKind.Generic;
}

public enum UpgradeTier { Common, Rare, Ability }

/// <summary>
/// Every upgrade, by kind:
///  * Class upgrades grow one of a hero's abilities (a tree per ability). A chest holds one;
///    milestones offer three.
///  * Alterations change how an ability works, one per ability. Only milestones offer them (at
///    least one on each milestone, while any are left), and each has upgrades of its own.
///  * Generic upgrades (anyone) and conditional ones (offered once something else is true) fill
///    the rest of a chest, with the risk-reward side-grades. Movement upgrades turn up more often
///    in chests underwater, survival ones in chests up high.
///  * Stats grow on their own at every level (<see cref="Progression"/>).
/// </summary>
public static class Upgrades
{
    private static readonly HeroKind[] S = { HeroKind.Swordsman }, W = { HeroKind.Warden }, V = { HeroKind.Vitalist }, E = { HeroKind.Elementalist };
    /// <summary>The two heroes who fight with a blade.</summary>
    private static readonly HeroKind[] Blades = { HeroKind.Swordsman, HeroKind.Warden };

    public static readonly List<Upgrade> Chest = new()
    {
        // --- Offence (all) ---
        new() { Id = "atkspd", Name = "Quick Hands", Desc = "Attack 18% faster.", Icon = "blade", MaxStacks = 5, Apply = (s, p) => s.AttackSpeed += 0.18f },
        new() { Id = "dmg", Name = "Whetstone", Desc = "+15% damage.", Icon = "blade", MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.DamageMult += 0.15f },

        // --- The blade (swordsman, warden) ---
        new() { Id = "reach", Name = "Longer Blade", Desc = "Blade reach +22%.", Icon = "blade", For = Blades, Ability = "sword", MaxStacks = 3, Apply = (s, p) => s.DaggerReach += 0.22f },
        new() { Id = "combo", Name = "Flurry", Desc = "Your combo chains one more strike: landing it refunds the swing cooldown again.", Icon = "blade", For = Blades, Ability = "sword", MaxStacks = 3, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ComboResets += 1 },
        new() { Id = "combo3", Name = "Finisher", Desc = "The last strike of a full combo hits much harder (x2 damage, wider arc).", Icon = "blade", For = Blades, Ability = "sword", Requires = "combo", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ThirdCombo = true },
        new() { Id = "pogo", Name = "Downward Thrust", Desc = "Aerial down-slashes bounce you off enemies and refresh air jumps.", Icon = "blade", For = Blades, Ability = "sword", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pogo = true },
        new() { Id = "knock", Name = "Heavy Pommel", Desc = "Your strikes knock enemies back much harder.", Icon = "blade", For = Blades, Ability = "sword", Tier = UpgradeTier.Ability, Apply = (s, p) => s.KnockbackLevel = Math.Max(1, s.KnockbackLevel) },
        new() { Id = "knock2", Name = "Crushing Blows", Desc = "Knockback strength increased.", Icon = "blade", For = Blades, Ability = "sword", MaxStacks = 2, Requires = "knock", Apply = (s, p) => s.KnockbackLevel += 1 },
        new() { Id = "leech", Name = "Thirsty Blade", Desc = "Heal 4% of damage dealt.", Icon = "life", For = Blades, Ability = "sword", MaxStacks = 3, Apply = (s, p) => s.LifeSteal += 0.04f },

        // --- Sword techniques (swordsman) ---
        new() { Id = "rend", Name = "Rending Edge", Desc = "Sword hits make enemies bleed for 40% more damage over 3 s.", Icon = "blade", For = S, Ability = "sword", MaxStacks = 2, Tier = UpgradeTier.Ability, Apply = (s, p) => s.BleedShare += Tune.Swordsman.BleedShare },
        new() { Id = "wave", Name = "Crescent Wave", Desc = "Swings loose a slicing wave that flies ahead and cuts through enemies (half damage, every 1.2 s).", Icon = "blade", For = S, Ability = "sword", Tier = UpgradeTier.Ability, Apply = (s, p) => s.CrescentWave = true },
        new() { Id = "execute", Name = "Executioner", Desc = "+60% damage to enemies below 35% health.", Icon = "blade", For = S, Ability = "sword", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Execute = true },

        // --- Charged Strike (swordsman) ---
        new() { Id = "charge_cd", Name = "Focus", Desc = "Charged Strike recharges 20% faster.", Icon = "charge", For = S, Ability = "charge", MaxStacks = 2, Apply = (s, p) => s.ChargeCooldown *= 0.8f },
        new() { Id = "charge2", Name = "Twin Charge", Desc = "Charged Strike empowers your next two swings (with Relentless Charge, two combos).", Icon = "charge", For = S, Ability = "charge", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ChargeSwings = 2 },
        new() { Id = "cripple", Name = "Crippling Strike", Desc = "Enemies struck by a charged swing deal 35% less damage instead of 20%.", Icon = "charge", For = S, Ability = "charge", Tier = UpgradeTier.Ability, Apply = (s, p) => s.WeakenMult = 0.65f },
        new() { Id = "storm", Name = "Storm Edge", Desc = "A charged swing looses a full-strength crescent wave.", Icon = "charge", For = S, Ability = "charge", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ChargeWave = true },
        new() { Id = "charge_combo", Name = "Relentless Charge", Desc = "Your Charged Strike empowers your whole next combo, at 80% strength: +40% damage and +20% reach on every strike.", Icon = "charge", For = S, Ability = "charge", Alteration = true, Apply = (s, p) => s.RelentlessCharge = true },
        new() { Id = "charge_combo_more", Name = "Unbroken", Desc = "Your combos chain one more strike.", Icon = "charge", For = S, Ability = "charge", Requires = "charge_combo", Apply = (s, p) => s.ComboResets += 1 },

        // --- Heaving Swing (swordsman) ---
        new() { Id = "heave_cd", Name = "Broad Shoulders", Desc = "Heaving swing recharges 20% faster.", Icon = "charge", For = S, Ability = "heave", MaxStacks = 2, Apply = (s, p) => s.HeaveCooldown *= 0.8f },
        new() { Id = "heave_swift", Name = "Swift Heave", Desc = "With a Charged Strike waiting, your heave spends it on speed instead of force: no wind-up, and you can heave in the air.", Icon = "charge", For = S, Ability = "heave", Alteration = true, Apply = (s, p) => s.SwiftHeave = true },
        new() { Id = "heave_swift_cd", Name = "Second Heave", Desc = "Your heaving swing recharges 3 s sooner.", Icon = "charge", For = S, Ability = "heave", Requires = "heave_swift", Apply = (s, p) => s.HeaveCooldown = Math.Max(1f, s.HeaveCooldown - 3f) },

        // --- Dodge (swordsman) ---
        new() { Id = "iframes", Name = "Phantom Step", Desc = "Dodging makes you briefly invulnerable.", Icon = "dodge", For = S, Ability = "dodge", Tier = UpgradeTier.Ability, Apply = (s, p) => s.DodgeIFrames = true },
        new() { Id = "dodgecd", Name = "Nimble", Desc = "Dodge cooldown -20%.", Icon = "dodge", For = S, Ability = "dodge", MaxStacks = 3, Apply = (s, p) => s.DodgeCdMult *= 0.8f },
        new() { Id = "dodge2", Name = "Second Wind", Desc = "Gain a second dodge charge.", Icon = "dodge", For = S, Ability = "dodge", Tier = UpgradeTier.Ability, Apply = (s, p) => { s.DodgeCharges = 2; p.SyncCharges(); } },
        new() { Id = "windrunner", Name = "Wind Runner", Desc = "Dodges and Charged Strike recharge 15% faster.", Icon = "dodge", For = S, Ability = "dodge", MaxStacks = 4, Apply = (s, p) => { s.DodgeCdMult *= 0.85f; s.ChargeCooldown *= 0.85f; } },
        new() { Id = "dodge_counter", Name = "Counter Roll", Desc = "Rolling into a melee blow ends the roll, stops all of that blow and swings back at the attacker.", Icon = "dodge", For = S, Ability = "dodge", Alteration = true, Apply = (s, p) => s.CounterRoll = true },
        new() { Id = "dodge_counter_combo", Name = "Flowing Counter", Desc = "A counter that lands can start a combo.", Icon = "dodge", For = S, Ability = "dodge", Requires = "dodge_counter", Apply = (s, p) => s.CounterCombo = true },

        // --- Shield (warden) ---
        new() { Id = "perfect_reflect", Name = "Riposte Guard", Desc = "A perfect block (raise the shield just before the hit) reflects projectiles back at enemies.", Icon = "shield", For = W, Ability = "shield", Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectReflect = true },
        new() { Id = "perfect_soak", Name = "Iron Timing", Desc = "Perfect blocks cost your shield 70% less.", Icon = "shield", For = W, Ability = "shield", Excludes = new[] { "shield_unyielding" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectSoak = true },
        new() { Id = "shield_wide", Name = "Tower Shield", Desc = "Your shield covers a 30% wider arc.", Icon = "shield", For = W, Ability = "shield", MaxStacks = 2, Apply = (s, p) => s.ShieldArcMult += 0.3f },
        new() { Id = "stalwart", Name = "Stalwart", Desc = "Full speed with the shield raised, and hits never knock you back.", Icon = "shield", For = W, Ability = "shield", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Stalwart = true },
        new() { Id = "quickmend", Name = "Quick Mend", Desc = "Your shield starts regenerating almost at once after a block, and 50% faster.", Icon = "shield", For = W, Ability = "shield", Excludes = new[] { "shield_unyielding" }, Apply = (s, p) => { s.QuickMend = true; s.ShieldRegen *= 1.5f; } },
        new() { Id = "thorns", Name = "Spiked Shield", Desc = "Melee attackers that strike your shield take 40% of the blow back.", Icon = "shield", For = W, Ability = "shield", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ShieldThorns = true },
        new() { Id = "laststand", Name = "Last Stand", Desc = "Once per depth, a killing blow leaves you at 1 HP, briefly invulnerable, with a whole shield.", Icon = "life", For = W, Ability = "shield", Tier = UpgradeTier.Ability, Apply = (s, p) => s.LastStand = true },
        new() { Id = "aegis", Name = "Aegis", Desc = "+15% shield strength and regeneration.", Icon = "shield", For = W, Ability = "shield", MaxStacks = 4, Apply = (s, p) => { s.ShieldMax *= 1.15f; s.ShieldRegen *= 1.15f; } },
        new() { Id = "shield_unyielding", Name = "Unyielding Shield", Desc = "Your shield never weakens or breaks, but stops only 70% of each blow (a perfect block still stops it all).", Icon = "shield", For = W, Ability = "shield", Alteration = true, Apply = (s, p) => s.Unyielding = true },
        new() { Id = "unyielding_more", Name = "Braced", Desc = "Your unyielding shield stops 5% more of each blow.", Icon = "shield", For = W, Ability = "shield", Requires = "shield_unyielding", MaxStacks = 2, Apply = (s, p) => s.UnyieldingShare += 0.05f },

        // --- Guarded Charge (warden) ---
        new() { Id = "dash_cd", Name = "Ready Charge", Desc = "Guarded Charge cooldown -20%.", Icon = "shield", For = W, Ability = "dash", MaxStacks = 3, Apply = (s, p) => s.DashCooldown *= 0.8f },
        new() { Id = "dash_far", Name = "Long Charge", Desc = "Your Guarded Charge carries you 30% farther.", Icon = "shield", For = W, Ability = "dash", MaxStacks = 2, Apply = (s, p) => s.DashTime *= 1.3f },
        new() { Id = "dash_bash", Name = "Battering Charge", Desc = "Your Guarded Charge hits what it stops four times as hard.", Icon = "shield", For = W, Ability = "dash", Tier = UpgradeTier.Ability, Apply = (s, p) => s.DashDamageMult = 4f },
        new() { Id = "dash_mend", Name = "Rallying Charge", Desc = "Breaking an attack with your Guarded Charge mends your shield by 12 and heals 4.", Icon = "shield", For = W, Ability = "dash", Tier = UpgradeTier.Ability, Apply = (s, p) => s.DashMend = true },
        new() { Id = "vanguard", Name = "Vanguard", Desc = "Guarded Charge recharges 15% faster.", Icon = "shield", For = W, Ability = "dash", MaxStacks = 4, Apply = (s, p) => s.DashCooldown *= 0.85f },
        new() { Id = "dash_guardian", Name = "Guardian's Charge", Desc = "Your Guarded Charge rushes to the friend nearest your aim and wraps them in a barrier that soaks 20 damage for 2 s (alone, it wraps you).", Icon = "shield", For = W, Ability = "dash", Alteration = true, Apply = (s, p) => s.GuardianCharge = true },
        new() { Id = "guardian_strong", Name = "Thick Barrier", Desc = "Your barrier soaks 50% more.", Icon = "shield", For = W, Ability = "dash", Requires = "dash_guardian", Apply = (s, p) => s.BarrierAmount *= 1.5f },
        new() { Id = "guardian_long", Name = "Lasting Barrier", Desc = "Your barrier lasts 33% longer.", Icon = "shield", For = W, Ability = "dash", Requires = "dash_guardian", Apply = (s, p) => s.BarrierSeconds *= 1.33f },

        // --- Shield bash (warden) ---
        new() { Id = "bash_cd", Name = "Hard Shoulder", Desc = "Shield bash recharges 20% faster.", Icon = "shield", For = W, Ability = "bash", MaxStacks = 2, Apply = (s, p) => s.BashCooldown *= 0.8f },
        new() { Id = "bash_deflect", Name = "Deflecting Bash", Desc = "Your shield bash no longer stuns: instead it sends every projectile in a wide arc in front back where it came from.", Icon = "shield", For = W, Ability = "bash", Alteration = true, Apply = (s, p) => s.DeflectingBash = true },
        new() { Id = "deflect_double", Name = "Return to Sender", Desc = "Projectiles your bash sends back deal double damage.", Icon = "shield", For = W, Ability = "bash", Requires = "bash_deflect", Apply = (s, p) => s.DeflectDouble = true },

        // --- Drain (vitalist) ---
        new() { Id = "mouths", Name = "Many Mouths", Desc = "Your drain also tears the life out of one more creature near its target (60% damage).", Icon = "spell", For = V, Ability = "drain", MaxStacks = 2, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DrainExtra += 1 },
        new() { Id = "bolt_range", Name = "Far Reach", Desc = "Your drain and rupture reach 25% farther.", Icon = "spell", For = V, Ability = "drain", MaxStacks = 2, Apply = (s, p) => s.DaggerReach += 0.25f },
        new() { Id = "hunger", Name = "Hungering Spirit", Desc = "Gain half again as much alimus from the damage you deal.", Icon = "spell", For = V, Ability = "drain", MaxStacks = 2, Apply = (s, p) => s.AlimusGain += Tune.Vitalist.AlimusGain * 0.5f },
        new() { Id = "well", Name = "Deep Well", Desc = "Hold 15 more alimus.", Icon = "spell", For = V, Ability = "drain", MaxStacks = 2, Apply = (s, p) => s.AlimusMax += 15f },

        // --- Hex (vitalist) ---
        new() { Id = "hex_wide", Name = "Spreading Blight", Desc = "Your hex reaches 30% farther.", Icon = "spell", For = V, Ability = "hex", MaxStacks = 2, Apply = (s, p) => s.HexRadiusMult += 0.3f },
        new() { Id = "hex_long", Name = "Lingering Hex", Desc = "Your hex lasts 2 s longer.", Icon = "spell", For = V, Ability = "hex", MaxStacks = 2, Apply = (s, p) => s.HexSeconds += 2f },
        new() { Id = "hex_rot", Name = "Withering Hex", Desc = "Hexed creatures rot, losing 6 health a second.", Icon = "spell", For = V, Ability = "hex", Tier = UpgradeTier.Ability, Apply = (s, p) => s.HexRot += 6f },
        new() { Id = "hex_burst", Name = "Blight Burst", Desc = "Your hex also deals 12 damage to everything it reaches, but slows and weakens half as much.", Icon = "spell", For = V, Ability = "hex", Alteration = true, Apply = (s, p) => s.BlightBurst = true },
        new() { Id = "hex_endless", Name = "Endless Hex", Desc = "Your hex has no cooldown, but costs 10 alimus.", Icon = "spell", For = V, Ability = "hex", Alteration = true, Apply = (s, p) => s.EndlessHex = true },

        // --- Heal (vitalist) ---
        new() { Id = "heal_more", Name = "Deep Mending", Desc = "Your heal restores 30% more.", Icon = "life", For = V, Ability = "heal", MaxStacks = 2, Apply = (s, p) => s.HealMult += 0.3f },
        new() { Id = "heal_cheap", Name = "Frugal Rites", Desc = "Your heal costs 25% less alimus.", Icon = "life", For = V, Ability = "heal", MaxStacks = 2, Apply = (s, p) => s.HealCostMult *= 0.75f },
        new() { Id = "wellspring", Name = "Wellspring", Desc = "+15% alimus gained and heal strength.", Icon = "spell", For = V, Ability = "heal", MaxStacks = 4, Apply = (s, p) => { s.AlimusGain *= 1.15f; s.HealMult *= 1.15f; } },
        new() { Id = "heal_slow", Name = "Slow Mending", Desc = "Your heal gives half at once and the other half over 6 s.", Icon = "life", For = V, Ability = "heal", Alteration = true, Apply = (s, p) => s.SlowMending = true },
        new() { Id = "heal_patient", Name = "Patient Mending", Desc = "All of your heal comes over the 6 s, and 20% more of it.", Icon = "life", For = V, Ability = "heal", Requires = "heal_slow", Apply = (s, p) => s.PatientMending = true },
        new() { Id = "heal_warding", Name = "Warding Mending", Desc = "Anyone your heal is mending takes 20% less damage.", Icon = "life", For = V, Ability = "heal", Requires = "heal_slow", Apply = (s, p) => s.WardingMending = true },

        // --- Rupture (vitalist) ---
        new() { Id = "rupture_wide", Name = "Burst Veins", Desc = "Your rupture's burst reaches 40% farther and splashes for half again as much.", Icon = "spell", For = V, Ability = "rupture", Tier = UpgradeTier.Ability, Apply = (s, p) => { s.RuptureRadiusMult += 0.4f; s.RuptureSplashMult += 0.5f; } },
        new() { Id = "rupture_cheap", Name = "Thin Blood", Desc = "Your rupture costs 20% less alimus.", Icon = "spell", For = V, Ability = "rupture", MaxStacks = 2, Apply = (s, p) => s.RuptureCostMult *= 0.8f },
        new() { Id = "rupture_bloom", Name = "Lifebloom", Desc = "Your rupture blooms on a friend instead of a creature (alone, on you): it heals them 30, and everyone else in the burst 10.", Icon = "life", For = V, Ability = "rupture", Alteration = true, Apply = (s, p) => s.Lifebloom = true },
        new() { Id = "bloom_pool", Name = "Healing Pool", Desc = "Your bloom leaves a pool that heals everyone in it 3 a second for 5 s.", Icon = "life", For = V, Ability = "rupture", Requires = "rupture_bloom", Apply = (s, p) => s.BloomPool = true },

        // --- Bolts (elementalist) ---
        new() { Id = "kindling", Name = "Kindling", Desc = "Your fire is 10% likelier to set a creature alight (firebolts, and a Firestorm).", Icon = "spell", For = E, Ability = "bolt", MaxStacks = 2,
                When = s => !s.Frostbolt || s.Firestorm, Apply = (s, p) => s.IgniteChance += Tune.Elementalist.KindlingChance },
        new() { Id = "reservoir", Name = "Deep Reservoir", Desc = "Hold 15 more aether.", Icon = "spell", For = E, Ability = "bolt", MaxStacks = 2, Apply = (s, p) => s.AetherMax += 15f },
        new() { Id = "bolt_frost", Name = "Frostbolt", Desc = "Your bolts are frost instead of fire: 8 damage every 0.3 s, slowing what they strike by 30% for 2 s, with an 8% chance of freezing a creature solid for 1.5 s (never a mini-boss, guardian or boss).", Icon = "spell", For = E, Ability = "bolt", Alteration = true, Apply = (s, p) => s.Frostbolt = true },

        // --- Updraft (elementalist) ---
        new() { Id = "attune", Name = "Attunement", Desc = "Your aether comes back 25% faster.", Icon = "spell", For = E, Ability = "updraft", MaxStacks = 3, Apply = (s, p) => s.AetherRegenMult += 0.25f },
        new() { Id = "updraft_narrow", Name = "Narrow Draft", Desc = "Your updraft is half as wide, but it carries you 6 m higher and lasts 15 s.", Icon = "move", For = E, Ability = "updraft", Alteration = true, Apply = (s, p) => s.NarrowDraft = true },

        // --- Blizzard (elementalist) ---
        new() { Id = "deepchill", Name = "Deep Chill", Desc = "Your frost is 4% likelier to freeze a creature solid (frostbolts, and the blizzard).", Icon = "spell", For = E, Ability = "blizzard", MaxStacks = 2,
                When = s => s.Frostbolt || !s.Firestorm, Apply = (s, p) => s.FreezeBonus += Tune.Elementalist.DeepChillChance },
        new() { Id = "winter", Name = "Long Winter", Desc = "Your blizzard lasts 50% longer.", Icon = "spell", For = E, Ability = "blizzard", Apply = (s, p) => s.BlizzardSecondsMult *= 1.5f },
        new() { Id = "whiteout", Name = "Whiteout", Desc = "Your blizzard is 30% wider.", Icon = "spell", For = E, Ability = "blizzard", Apply = (s, p) => s.BlizzardWideMult += 0.3f },
        new() { Id = "gathering", Name = "Gathering Storm", Desc = "Your blizzard comes back 25% sooner.", Icon = "spell", For = E, Ability = "blizzard", Apply = (s, p) => s.BlizzardCdMult *= 0.75f },
        new() { Id = "blizzard_fire", Name = "Firestorm", Desc = "Your blizzard is a storm of fire: 3 damage a strike, each with a 10% chance of setting a creature alight (but it freezes nothing).", Icon = "spell", For = E, Ability = "blizzard", Alteration = true, Apply = (s, p) => s.Firestorm = true },

        // --- Snap (elementalist) ---
        new() { Id = "shrapnel", Name = "Shrapnel", Desc = "Your snap's bursts reach 40% farther.", Icon = "spell", For = E, Ability = "snap", Apply = (s, p) => s.SnapWideMult += 0.4f },
        new() { Id = "echo", Name = "Echo", Desc = "Every creature your snap bursts gives you 5 aether back.", Icon = "spell", For = E, Ability = "snap", Apply = (s, p) => s.SnapEcho = true },
        new() { Id = "snap_cinder", Name = "Cinder Snap", Desc = "Your snap bursts burning creatures instead of frozen ones: 20 damage, and 6 to everything around them.", Icon = "spell", For = E, Ability = "snap", Alteration = true, Apply = (s, p) => s.CinderSnap = true },

        // --- Movement (all) ---
        // (rare, and only once you've a jump-height upgrade / a movement-speed upgrade)
        new() { Id = "djump", Name = "Double Jump", Desc = "Jump once more in mid-air.", Icon = "move", Excludes = new[] { "airdash" }, Tier = UpgradeTier.Ability, Weight = 0.3f,
                When = s => s.StackOf("jump") + s.StackOf("rr_bones") > 0, Apply = (s, p) => s.DoubleJump = true },
        new() { Id = "airdash", Name = "Air Dash", Desc = "Jumping in mid-air dashes in any direction you hold.", Icon = "move", Excludes = new[] { "djump" }, Tier = UpgradeTier.Ability, Weight = 0.3f,
                When = s => s.StackOf("speed") > 0, Apply = (s, p) => s.AirDash = true },
        new() { Id = "speed", Name = "Light Boots", Desc = "+10% movement speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.MoveSpeed += 0.10f },
        new() { Id = "jump", Name = "Spring Step", Desc = "+12% jump height.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.JumpMult += 0.12f },
        new() { Id = "swim", Name = "Webbed Gloves", Desc = "+25% swim speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.SwimSpeed += 0.25f },
        new() { Id = "breath", Name = "Deep Lungs", Desc = "+50% breath underwater.", Icon = "move", MaxStacks = 3, Excludes = new[] { "rr_lungs" }, Apply = (s, p) => { s.BreathMax += 4f; p.Breath = s.BreathMax; } },
        // (offered from depth 6, on the way down to the magma)
        new() { Id = "magma", Name = "Magma Skin", Desc = "Swim in lava, and it burns you for only 30% as much.", Icon = "move", Tier = UpgradeTier.Ability,
                When = s => G.Depth >= Tune.Hero.MagmaSkinFromDepth, Apply = (s, p) => s.MagmaSkin = true },

        // --- Survival (all) ---
        new() { Id = "hp", Name = "Vitality", Desc = "+20 max HP and heal 20.", Icon = "life", MaxStacks = 5, Weight = 1.2f, Apply = (s, p) => { s.MaxHp += 20; p.Heal(20); } },
        new() { Id = "resilience", Name = "Resilience", Desc = "Stay invulnerable 0.2 s longer after being struck.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HurtInvuln += 0.2f },
        new() { Id = "armor", Name = "Toughened Hide", Desc = "Take 10% less damage.", Icon = "life", MaxStacks = 4, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.10f) },
        new() { Id = "onkill", Name = "Trophy Hunter", Desc = "Heal 1 HP on every kill.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HealOnKill += 1f },
        new() { Id = "magnet", Name = "Lodestone", Desc = "Pull experience from further away.", Icon = "life", MaxStacks = 2, Weight = 0.6f, Apply = (s, p) => s.MagnetMult += 0.6f },

        // --- Risk and reward (all): something given, something taken. Each can be taken once:
        // they never stack, and once you have one it isn't offered again ---
        new() { Id = "rr_heavy", Name = "Heavy Hand", Desc = "Your attacks come 34% slower, but hit 66% harder.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.AttackSpeed *= 0.66f; s.PrimaryDamageMult *= 1.66f; } },
        new() { Id = "rr_bones", Name = "Hollow Bones", Desc = "Jump 20% higher, but swim 40% slower.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.JumpMult *= 1.2f; s.SwimSpeed *= 0.6f; } },
        new() { Id = "rr_gills", Name = "Gill-Touched", Desc = "Swim 50% faster, but run 20% slower.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.SwimSpeed *= 1.5f; s.MoveSpeed *= 0.8f; } },
        new() { Id = "rr_reserve", Name = "Twin Reserve", Desc = "Your {ability} holds a second use, but each use takes twice as long to come back.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.AbilityCharges += 1; s.AbilityCdMult *= 2f; p.SyncCharges(); } },
        new() { Id = "rr_glass", Name = "Glass Edge", Desc = "Deal 20% more damage, but take 25% more.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.DamageMult *= 1.2f; s.DamageTakenMult *= 1.25f; } },
        new() { Id = "rr_stone", Name = "Stoneskin", Desc = "Take 25% less damage, but deal 20% less.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Apply = (s, p) => { s.DamageTakenMult *= 0.75f; s.DamageMult *= 0.8f; } },
        new() { Id = "rr_lungs", Name = "Drowned Lungs", Desc = "You never run out of breath, but all healing you receive is 30% weaker.", Icon = "risk", MaxStacks = 1, Weight = 0.7f, Excludes = new[] { "breath" }, Apply = (s, p) => { s.InfiniteBreath = true; s.HealingTakenMult *= 0.7f; p.Breath = s.BreathMax; } },
    };

    /// <summary>The "leave it" card on a milestone: nothing.</summary>
    public static readonly Upgrade Skip = new() { Id = "skip", Name = "Leave It", Desc = "Take nothing.", Icon = "skip", MaxStacks = 9999, Apply = (s, p) => { } };
    /// <summary>The "leave it" card in a chest: it closes again, keeping its cards.</summary>
    public static readonly Upgrade LeaveChest = new() { Id = "leave", Name = "Leave It", Desc = "Close the chest. It keeps these cards: come back for one later, or leave it for a friend.", Icon = "skip", MaxStacks = 9999, Apply = (s, p) => { } };

    public static IEnumerable<Upgrade> All => Chest.Append(Skip).Append(LeaveChest);

    /// <summary>A card by id, or null if there's no such card (a chest dealt by another version).</summary>
    public static Upgrade Find(string id) => All.FirstOrDefault(u => u.Id == id);

    public static Upgrade Get(string id) => All.First(u => u.Id == id);

    /// <summary>The hero's ability button, by name (for card texts).</summary>
    public static string AbilityName(HeroKind h) => h switch
    {
        HeroKind.Warden => "Guarded Charge",
        HeroKind.Vitalist => "heal",
        HeroKind.Elementalist => "Blizzard",
        _ => "Charged Strike",
    };

    /// <summary>A hero's abilities, in the order their trees are shown: (key, name).</summary>
    public static (string key, string name)[] Abilities(HeroKind h) => h switch
    {
        HeroKind.Warden => new[] { ("sword", "Shortsword"), ("shield", "Shield"), ("dash", "Guarded Charge"), ("bash", "Shield Bash") },
        HeroKind.Vitalist => new[] { ("drain", "Drain"), ("hex", "Hex"), ("heal", "Heal"), ("rupture", "Rupture") },
        HeroKind.Elementalist => new[] { ("bolt", "Firebolt"), ("updraft", "Updraft"), ("blizzard", "Blizzard"), ("snap", "Snap") },
        _ => new[] { ("sword", "Sword"), ("charge", "Charged Strike"), ("heave", "Heaving Swing"), ("dodge", "Dodge Roll") },
    };

    /// <summary>An ability's display name, by its key (for card labels).</summary>
    public static string AbilityTitle(string key, HeroKind h)
    {
        foreach (var (k, n) in Abilities(h)) if (k == key) return n;
        foreach (var hero in Enum.GetValues<HeroKind>()) foreach (var (k, n) in Abilities(hero)) if (k == key) return n;
        return key;
    }

    /// <summary>A card's text for this hero.</summary>
    public static string DescFor(Upgrade u, PlayerStats s) => s == null ? u.Desc.Replace("{ability}", "ability") : u.Desc.Replace("{ability}", AbilityName(s.Hero));

    /// <summary>The alteration this hero has for an ability (null: none yet).</summary>
    public static Upgrade AlterationOf(PlayerStats s, string ability) => Chest.FirstOrDefault(u => u.Alteration && u.Ability == ability && s.StackOf(u.Id) > 0);

    public static bool Available(Upgrade u, PlayerStats s)
    {
        if (u.For != null && Array.IndexOf(u.For, s.Hero) < 0) return false;
        if (s.StackOf(u.Id) >= u.MaxStacks) return false;
        if (u.Requires != null && s.StackOf(u.Requires) == 0) return false;
        if (u.When != null && !u.When(s)) return false;
        foreach (var ex in u.Excludes) if (s.StackOf(ex) > 0) return false;
        // an ability takes one alteration at most
        if (u.Alteration && AlterationOf(s, u.Ability) != null) return false;
        return true;
    }

    /// <summary>
    /// A milestone's three cards, all for this hero's abilities: at least one alteration while any
    /// are left (they're only ever offered here), the rest class upgrades or more alterations.
    /// </summary>
    public static List<Upgrade> RollMilestone(PlayerStats s, Random rng)
    {
        var alterations = Chest.Where(u => u.Alteration && Available(u, s)).ToList();
        var result = Pick(alterations, 1, rng, u => u.Weight);
        var rest = Chest.Where(u => u.For != null && !result.Contains(u) && Available(u, s)).ToList();
        // (two alterations for the same ability can't both be offered as if both could be taken:
        // they can, one or the other, so that's fine to show)
        result.AddRange(Pick(rest, 3 - result.Count, rng, u => u.Weight * (u.Alteration ? 1.5f : 1f)));
        return result;
    }

    /// <summary>Weights for a chest's cards: where it was found tilts the odds (movement upgrades are
    /// likelier underwater, survival ones in the upper part of the cave).</summary>
    private static Func<Upgrade, float> ChestWeight(Vector2 at)
    {
        var cave = G.Cave;
        bool underwater = cave != null && at.Y > cave.WaterY;
        bool high = cave != null && at.Y < cave.WaterY * Tune.Drops.HighZoneFraction;
        return u =>
        {
            float w = u.Weight * (u.Tier == UpgradeTier.Ability ? 1.5f : 1f);
            if (underwater && u.Icon == "move") w *= Tune.Drops.ZoneBias;
            if (high && u.Icon == "life") w *= Tune.Drops.ZoneBias;
            return w;
        };
    }

    /// <summary>A chest's three cards, for one hero: one class upgrade and two others (generic,
    /// conditional or a side-grade).</summary>
    public static List<Upgrade> RollChest(PlayerStats s, Random rng, Vector2 at)
    {
        var weight = ChestWeight(at);
        var cls = Pick(Chest.Where(u => u.Kind == UpgradeKind.Class && Available(u, s)).ToList(), 1, rng, weight);
        var others = Pick(Chest.Where(u => u.For == null && Available(u, s)).ToList(), 3 - cls.Count, rng, weight);
        if (cls.Count + others.Count < 3) cls.AddRange(Pick(Chest.Where(u => u.Kind == UpgradeKind.Class && !cls.Contains(u) && Available(u, s)).ToList(), 3 - cls.Count - others.Count, rng, weight));
        return cls.Concat(others).ToList();
    }

    /// <summary>
    /// The three cards a chest holds (their ids). Alone, they're dealt as <see cref="RollChest"/>.
    /// With a party, the class card is for one of the party's heroes (any of them), so a chest may
    /// hold a card for a friend; the other two are what the dealer can take.
    /// </summary>
    public static string[] RollChestCards(PlayerStats mine, IReadOnlyCollection<HeroKind> party, Random rng, Vector2 at)
    {
        if (party == null || party.Count <= 1) return RollChest(mine, rng, at).Select(u => u.Id).ToArray();
        var weight = ChestWeight(at);
        var heroes = party.ToList();
        var hero = heroes[rng.Next(heroes.Count)];
        // a friend's class card can only be judged as for a fresh hero: simple ones only
        bool Fits(Upgrade u) => u.Kind == UpgradeKind.Class && Array.IndexOf(u.For, hero) >= 0
                                && (hero == mine.Hero ? Available(u, mine) : u.Requires == null && u.When == null && u.Excludes.Length == 0);
        var cls = Pick(Chest.Where(Fits).ToList(), 1, rng, weight);
        var others = Pick(Chest.Where(u => u.For == null && Available(u, mine)).ToList(), 3 - cls.Count, rng, weight);
        return cls.Concat(others).Select(u => u.Id).ToArray();
    }

    /// <summary>Why this hero can't take a card (null: they can).</summary>
    public static string LockReason(Upgrade u, PlayerStats s)
    {
        if (u.Icon == "skip" || s == null) return null;
        if (u.For != null && Array.IndexOf(u.For, s.Hero) < 0) return "FOR THE " + string.Join(" OR ", u.For.Select(h => h.ToString().ToUpperInvariant()));
        if (s.StackOf(u.Id) >= u.MaxStacks) return "YOU HAVE IT ALREADY";
        if (u.Alteration && AlterationOf(s, u.Ability) is Upgrade other && other != u) return $"YOU HAVE {other.Name.ToUpperInvariant()}";
        return Available(u, s) ? null : "NOT FOR YOU YET";
    }

    private static List<Upgrade> Pick(List<Upgrade> pool, int count, Random rng, Func<Upgrade, float> weight)
    {
        var result = new List<Upgrade>();
        while (result.Count < count && pool.Count > 0)
        {
            float total = 0;
            foreach (var u in pool) total += weight(u);
            double r = rng.NextDouble() * total;
            Upgrade pick = pool[^1];
            foreach (var u in pool) { r -= weight(u); if (r <= 0) { pick = u; break; } }
            result.Add(pick);
            pool.Remove(pick);
        }
        return result;
    }

    public static void Apply(Upgrade u, PlayerStats s, Player p)
    {
        s.Stacks[u.Id] = s.StackOf(u.Id) + 1;
        u.Apply(s, p);
    }
}

/// <summary>What each level gives on its own, by hero: the swordsman leans on damage, the warden
/// on toughness, the vitalist on its reserves of alimus, the elementalist on its aether.</summary>
public static class Progression
{
    public static void AutoLevel(Player p)
    {
        var s = p.Stats;
        string gain;
        switch (s.Hero)
        {
            case HeroKind.Warden:
                s.MaxHp += 4; p.Heal(4);
                s.DamageMult += 0.015f;
                s.DamageReduction = Math.Min(0.75f, s.DamageReduction + 0.01f);
                s.ShieldMax += 2;
                gain = "+4 HP  +1% guard";
                break;
            case HeroKind.Vitalist:
                s.MaxHp += 3; p.Heal(3);
                s.DamageMult += 0.02f;
                s.AlimusMax += 1;
                gain = "+2% damage  +1 alimus";
                break;
            case HeroKind.Elementalist:
                s.MaxHp += 3; p.Heal(3);
                s.DamageMult += 0.02f;
                s.AetherMax += 1;
                gain = "+2% damage  +1 aether";
                break;
            default:
                s.MaxHp += 2; p.Heal(2);
                s.DamageMult += 0.03f;
                s.AttackSpeed += 0.015f;
                gain = "+3% damage";
                break;
        }
        G.Sfx.Play("levelup", p.GlobalPosition, -2);
        G.Fx.Text(p.GlobalPosition + new Vector2(0, -34), $"LEVEL {p.Level}", new Color(1f, 0.9f, 0.45f), 14, 1.3f);
        G.Fx.Text(p.GlobalPosition + new Vector2(0, -20), gain, new Color(0.9f, 0.95f, 1f), 9, 1.1f);
        G.Fx.Flash(p.GlobalPosition, 30, new Color(1f, 0.9f, 0.5f));
        G.Fx.Ring(p.GlobalPosition, 26, new Color(1f, 0.9f, 0.5f));
        G.Fx.Shockwave(p.GlobalPosition + new Vector2(0, 13), 40, new Color(1f, 0.9f, 0.5f, 0.8f));
        for (int k = 0; k < 10; k++) G.Fx.Glint(p.GlobalPosition + G.RandDir() * G.Range(8, 26), new Color(1f, 0.9f, 0.5f), 6);
        p.Anim.Flash(0.7f);
    }
}
