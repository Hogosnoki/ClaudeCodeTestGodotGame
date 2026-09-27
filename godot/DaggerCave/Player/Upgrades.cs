using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The playable heroes.</summary>
public enum HeroKind { Swordsman, Warden, Vitalist }

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
    /// <summary>The ability button (charged strike, shield dash, heal): how many uses it holds, and
    /// how much longer each takes to come back (Twin Reserve).</summary>
    public int AbilityCharges = 1;
    public float AbilityCdMult = 1f;
    public float DaggerReach = 1f;       // swing reach (and bolt range) multiplier
    public float MoveSpeed = 1f;
    public float JumpMult = 1f;
    public float SwimSpeed = 1f;
    public float BreathMax = Tune.Hero.BreathSeconds;         // seconds
    public float DamageReduction = 0f;   // 0..1
    public float LifeSteal = 0f;         // fraction of damage dealt
    public float HealOnKill = 0f;        // hp per kill
    public int KnockbackLevel = 0;
    public bool WallJump, DoubleJump, AirDash, Pogo, ThirdCombo;
    public float MagnetMult = 1f;
    /// <summary>How many times in a row a landed strike refunds the swing cooldown.</summary>
    public int ComboResets = Tune.Combat.ComboResetsBase;

    // swordsman: dodge + charged strike
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

    // warden: shield + shield dash
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

    // vitalist: drain, hex, heal, rupture
    public float AlimusMax = Tune.Vitalist.AlimusMax;
    public float AlimusGain = Tune.Vitalist.AlimusGain;
    public float HealMult = 1f, HealCostMult = 1f;
    /// <summary>Many Mouths: how many more creatures near the target each drain also strikes.</summary>
    public int DrainExtra;
    public float HexRadiusMult = 1f, HexSeconds = Tune.Vitalist.HexSeconds, HexCooldown = Tune.Vitalist.HexCooldown;
    public float HexRot;                 // damage per second to hexed creatures
    public float RuptureCostMult = 1f, RuptureRadiusMult = 1f, RuptureSplashMult = 1f;

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
            default:
                MoveSpeed = Tune.Swordsman.MoveMult; JumpMult = Tune.Swordsman.JumpMult;
                MaxHp = Tune.Swordsman.StartHp;
                break;
        }
    }
}

public sealed class Upgrade
{
    public string Id, Name, Desc;
    /// <summary>Category used for the card color: blade, charge, move, dodge, shield, life, spell.</summary>
    public string Icon;
    public int MaxStacks = 1;
    public string Requires;
    public string[] Excludes = Array.Empty<string>();
    public float Weight = 1f;
    public Action<PlayerStats, Player> Apply;
    public UpgradeTier Tier = UpgradeTier.Common;
    /// <summary>Only these heroes can get it (null = all of them).</summary>
    public HeroKind[] For;
}

public enum UpgradeTier { Common, Rare, Ability }

/// <summary>
/// Two pools:
///  * <see cref="Chest"/>: the real upgrades and abilities, found in chests around the cave.
///    Movement upgrades turn up more often underwater and survival ones up high.
///  * <see cref="LevelUp"/>: small stat nudges, picked on every level-up.
/// </summary>
public static class Upgrades
{
    private static readonly HeroKind[] S = { HeroKind.Swordsman }, W = { HeroKind.Warden }, V = { HeroKind.Vitalist };
    /// <summary>The two heroes who fight with a blade.</summary>
    private static readonly HeroKind[] Blades = { HeroKind.Swordsman, HeroKind.Warden };

    public static readonly List<Upgrade> Chest = new()
    {
        // --- Offence (all) ---
        new() { Id = "atkspd", Name = "Quick Hands", Desc = "Attack 18% faster.", Icon = "blade", MaxStacks = 5, Apply = (s, p) => s.AttackSpeed += 0.18f },
        new() { Id = "dmg", Name = "Whetstone", Desc = "+15% damage.", Icon = "blade", MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.DamageMult += 0.15f },

        // --- Blade (swordsman, warden) ---
        new() { Id = "reach", Name = "Longer Blade", Desc = "Blade reach +22%.", Icon = "blade", For = Blades, MaxStacks = 3, Apply = (s, p) => s.DaggerReach += 0.22f },
        new() { Id = "combo", Name = "Flurry", Desc = "Your combo chains one more strike: landing it refunds the swing cooldown again.", Icon = "blade", For = Blades, MaxStacks = 3, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ComboResets += 1 },
        new() { Id = "combo3", Name = "Finisher", Desc = "The last strike of a full combo hits much harder (x2 damage, wider arc).", Icon = "blade", For = Blades, Requires = "combo", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ThirdCombo = true },
        new() { Id = "pogo", Name = "Downward Thrust", Desc = "Aerial down-slashes bounce you off enemies and refresh air jumps.", Icon = "blade", For = Blades, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pogo = true },
        new() { Id = "knock", Name = "Heavy Pommel", Desc = "Your strikes knock enemies back much harder.", Icon = "blade", For = Blades, Tier = UpgradeTier.Ability, Apply = (s, p) => s.KnockbackLevel = Math.Max(1, s.KnockbackLevel) },
        new() { Id = "knock2", Name = "Crushing Blows", Desc = "Knockback strength increased.", Icon = "blade", For = Blades, MaxStacks = 2, Requires = "knock", Apply = (s, p) => s.KnockbackLevel += 1 },

        // --- Sword techniques (swordsman) ---
        new() { Id = "rend", Name = "Rending Edge", Desc = "Sword hits make enemies bleed for 40% more damage over 3 s.", Icon = "blade", For = S, MaxStacks = 2, Tier = UpgradeTier.Ability, Apply = (s, p) => s.BleedShare += Tune.Swordsman.BleedShare },
        new() { Id = "wave", Name = "Crescent Wave", Desc = "Swings loose a slicing wave that flies ahead and cuts through enemies (half damage, every 1.2 s).", Icon = "blade", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.CrescentWave = true },
        new() { Id = "execute", Name = "Executioner", Desc = "+60% damage to enemies below 35% health.", Icon = "blade", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Execute = true },

        // --- Charged strike (swordsman) ---
        new() { Id = "charge_cd", Name = "Focus", Desc = "Charged Strike recharges 20% faster.", Icon = "charge", For = S, MaxStacks = 2, Apply = (s, p) => s.ChargeCooldown *= 0.8f },
        new() { Id = "charge2", Name = "Twin Charge", Desc = "Charged Strike empowers your next two swings.", Icon = "charge", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ChargeSwings = 2 },
        new() { Id = "cripple", Name = "Crippling Strike", Desc = "Enemies struck by a charged swing deal 35% less damage instead of 20%.", Icon = "charge", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.WeakenMult = 0.65f },
        new() { Id = "storm", Name = "Storm Edge", Desc = "A charged swing looses a full-strength crescent wave.", Icon = "charge", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ChargeWave = true },
        new() { Id = "heave_cd", Name = "Broad Shoulders", Desc = "Heaving swing recharges 20% faster.", Icon = "charge", For = S, MaxStacks = 2, Apply = (s, p) => s.HeaveCooldown *= 0.8f },

        // --- Dodge (swordsman) ---
        new() { Id = "iframes", Name = "Phantom Step", Desc = "Dodging makes you briefly invulnerable.", Icon = "dodge", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DodgeIFrames = true },
        new() { Id = "dodgecd", Name = "Nimble", Desc = "Dodge cooldown -20%.", Icon = "dodge", For = S, MaxStacks = 3, Apply = (s, p) => s.DodgeCdMult *= 0.8f },
        new() { Id = "dodge2", Name = "Second Wind", Desc = "Gain a second dodge charge.", Icon = "dodge", For = S, Tier = UpgradeTier.Ability, Apply = (s, p) => { s.DodgeCharges = 2; p.SyncCharges(); } },

        // --- Shield and shield dash (warden) ---
        new() { Id = "block", Name = "Tempered Shield", Desc = "Your shield stops 10% more of each blow.", Icon = "shield", For = W, MaxStacks = 2, Weight = 1.3f, Apply = (s, p) => s.BlockShare = Math.Min(0.95f, s.BlockShare + 0.1f) },
        new() { Id = "perfect_reflect", Name = "Riposte Guard", Desc = "A perfect block (raise the shield just before the hit) reflects projectiles back at enemies.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectReflect = true },
        new() { Id = "perfect_soak", Name = "Iron Timing", Desc = "Perfect blocks cost your shield 70% less.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectSoak = true },
        new() { Id = "shield_wide", Name = "Tower Shield", Desc = "Your shield covers a 30% wider arc.", Icon = "shield", For = W, MaxStacks = 2, Apply = (s, p) => s.ShieldArcMult += 0.3f },
        new() { Id = "stalwart", Name = "Stalwart", Desc = "Full speed with the shield raised, and hits never knock you back.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Stalwart = true },
        new() { Id = "quickmend", Name = "Quick Mend", Desc = "Your shield starts regenerating almost at once after a block, and 50% faster.", Icon = "shield", For = W, Apply = (s, p) => { s.QuickMend = true; s.ShieldRegen *= 1.5f; } },
        new() { Id = "thorns", Name = "Spiked Shield", Desc = "Melee attackers that strike your shield take 40% of the blow back.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ShieldThorns = true },
        new() { Id = "laststand", Name = "Last Stand", Desc = "Once per depth, a killing blow leaves you at 1 HP, briefly invulnerable, with a whole shield.", Icon = "life", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.LastStand = true },
        new() { Id = "dash_cd", Name = "Ready Charge", Desc = "Shield dash cooldown -20%.", Icon = "shield", For = W, MaxStacks = 3, Apply = (s, p) => s.DashCooldown *= 0.8f },
        new() { Id = "dash_far", Name = "Long Charge", Desc = "Your shield dash carries you 30% farther.", Icon = "shield", For = W, MaxStacks = 2, Apply = (s, p) => s.DashTime *= 1.3f },
        new() { Id = "dash_bash", Name = "Battering Charge", Desc = "Your shield dash hits what it stops four times as hard.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DashDamageMult = 4f },
        new() { Id = "dash_mend", Name = "Rallying Charge", Desc = "Breaking an attack with your shield dash mends your shield by 12 and heals 4.", Icon = "shield", For = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DashMend = true },
        new() { Id = "bash_cd", Name = "Hard Shoulder", Desc = "Shield bash recharges 20% faster.", Icon = "shield", For = W, MaxStacks = 2, Apply = (s, p) => s.BashCooldown *= 0.8f },

        // --- Spells (vitalist) ---
        new() { Id = "mouths", Name = "Many Mouths", Desc = "Your drain also tears the life out of one more creature near its target (60% damage).", Icon = "spell", For = V, MaxStacks = 2, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DrainExtra += 1 },
        new() { Id = "bolt_range", Name = "Far Reach", Desc = "Your drain and rupture reach 25% farther.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.DaggerReach += 0.25f },
        new() { Id = "hunger", Name = "Hungering Spirit", Desc = "Gain half again as much alimus from the damage you deal.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.AlimusGain += Tune.Vitalist.AlimusGain * 0.5f },
        new() { Id = "well", Name = "Deep Well", Desc = "Hold 15 more alimus.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.AlimusMax += 15f },
        new() { Id = "rupture_wide", Name = "Burst Veins", Desc = "Your rupture's burst reaches 40% farther and splashes for half again as much.", Icon = "spell", For = V, Tier = UpgradeTier.Ability, Apply = (s, p) => { s.RuptureRadiusMult += 0.4f; s.RuptureSplashMult += 0.5f; } },
        new() { Id = "rupture_cheap", Name = "Thin Blood", Desc = "Your rupture costs 20% less alimus.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.RuptureCostMult *= 0.8f },
        new() { Id = "heal_more", Name = "Deep Mending", Desc = "Your heal restores 30% more.", Icon = "life", For = V, MaxStacks = 2, Apply = (s, p) => s.HealMult += 0.3f },
        new() { Id = "heal_cheap", Name = "Frugal Rites", Desc = "Your heal costs 25% less alimus.", Icon = "life", For = V, MaxStacks = 2, Apply = (s, p) => s.HealCostMult *= 0.75f },
        new() { Id = "hex_wide", Name = "Spreading Blight", Desc = "Your hex reaches 30% farther.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.HexRadiusMult += 0.3f },
        new() { Id = "hex_long", Name = "Lingering Hex", Desc = "Your hex lasts 2 s longer.", Icon = "spell", For = V, MaxStacks = 2, Apply = (s, p) => s.HexSeconds += 2f },
        new() { Id = "hex_rot", Name = "Withering Hex", Desc = "Hexed creatures rot, losing 6 health a second.", Icon = "spell", For = V, Tier = UpgradeTier.Ability, Apply = (s, p) => s.HexRot += 6f },

        // --- Movement (all) ---
        new() { Id = "walljump", Name = "Wall Kick", Desc = "Jump off walls. Hold toward a wall to slide down it slowly.", Icon = "move", Tier = UpgradeTier.Ability, Apply = (s, p) => s.WallJump = true },
        new() { Id = "djump", Name = "Double Jump", Desc = "Jump once more in mid-air.", Icon = "move", Excludes = new[] { "airdash" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DoubleJump = true },
        new() { Id = "airdash", Name = "Air Dash", Desc = "Jumping in mid-air dashes in any direction you hold.", Icon = "move", Excludes = new[] { "djump" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.AirDash = true },
        new() { Id = "speed", Name = "Light Boots", Desc = "+10% movement speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.MoveSpeed += 0.10f },
        new() { Id = "jump", Name = "Spring Step", Desc = "+12% jump height.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.JumpMult += 0.12f },
        new() { Id = "swim", Name = "Webbed Gloves", Desc = "+25% swim speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.SwimSpeed += 0.25f },
        new() { Id = "breath", Name = "Deep Lungs", Desc = "+50% breath underwater.", Icon = "move", MaxStacks = 3, Apply = (s, p) => { s.BreathMax += 4f; p.Breath = s.BreathMax; } },

        // --- Survival (all) ---
        new() { Id = "hp", Name = "Vitality", Desc = "+20 max HP and heal 20.", Icon = "life", MaxStacks = 5, Weight = 1.2f, Apply = (s, p) => { s.MaxHp += 20; p.Heal(20); } },
        new() { Id = "resilience", Name = "Resilience", Desc = "Stay invulnerable 0.2 s longer after being struck.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HurtInvuln += 0.2f },
        new() { Id = "armor", Name = "Toughened Hide", Desc = "Take 10% less damage.", Icon = "life", MaxStacks = 4, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.10f) },
        new() { Id = "leech", Name = "Thirsty Blade", Desc = "Heal 4% of damage dealt.", Icon = "life", For = Blades, MaxStacks = 3, Apply = (s, p) => s.LifeSteal += 0.04f },
        new() { Id = "onkill", Name = "Trophy Hunter", Desc = "Heal 3 HP on every kill.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HealOnKill += 3f },
        new() { Id = "magnet", Name = "Lodestone", Desc = "Pull experience from further away.", Icon = "life", MaxStacks = 2, Weight = 0.6f, Apply = (s, p) => s.MagnetMult += 0.6f },

        // --- Risk and reward (all): something given, something taken ---
        new() { Id = "rr_heavy", Name = "Heavy Hand", Desc = "Your attacks come 34% slower, but hit 66% harder.", Icon = "risk", Weight = 0.7f, Apply = (s, p) => { s.AttackSpeed *= 0.66f; s.PrimaryDamageMult *= 1.66f; } },
        new() { Id = "rr_bones", Name = "Hollow Bones", Desc = "Jump 20% higher, but swim 40% slower.", Icon = "risk", Weight = 0.7f, Apply = (s, p) => { s.JumpMult *= 1.2f; s.SwimSpeed *= 0.6f; } },
        new() { Id = "rr_gills", Name = "Gill-Touched", Desc = "Swim 50% faster, but run 20% slower.", Icon = "risk", Weight = 0.7f, Apply = (s, p) => { s.SwimSpeed *= 1.5f; s.MoveSpeed *= 0.8f; } },
        new() { Id = "rr_reserve", Name = "Twin Reserve", Desc = "Your {ability} holds a second use, but each use takes twice as long to come back.", Icon = "risk", Weight = 0.7f, Apply = (s, p) => { s.AbilityCharges += 1; s.AbilityCdMult *= 2f; p.SyncCharges(); } },
    };

    /// <summary>Small, stackable stat nudges offered on level-up.</summary>
    public static readonly List<Upgrade> LevelUp = new()
    {
        new() { Id = "lv_hp", Name = "Vigor", Desc = "+8 max HP (and heal 8).", Icon = "life", MaxStacks = 20, Apply = (s, p) => { s.MaxHp += 8; p.Heal(8); } },
        new() { Id = "lv_dmg", Name = "Edge", Desc = "+6% damage.", Icon = "blade", MaxStacks = 20, Apply = (s, p) => s.DamageMult += 0.06f },
        new() { Id = "lv_atk", Name = "Tempo", Desc = "+6% attack speed.", Icon = "blade", MaxStacks = 15, Apply = (s, p) => s.AttackSpeed += 0.06f },
        new() { Id = "lv_reach", Name = "Extension", Desc = "+5% reach.", Icon = "blade", MaxStacks = 10, Apply = (s, p) => s.DaggerReach += 0.05f },
        new() { Id = "lv_move", Name = "Stride", Desc = "+4% movement speed.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.MoveSpeed += 0.04f },
        new() { Id = "lv_jump", Name = "Spring", Desc = "+5% jump height.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.JumpMult += 0.05f },
        new() { Id = "lv_swim", Name = "Stroke", Desc = "+10% swim speed.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.SwimSpeed += 0.10f },
        new() { Id = "lv_breath", Name = "Lungs", Desc = "+1.5 s of breath.", Icon = "move", MaxStacks = 10, Apply = (s, p) => { s.BreathMax += 1.5f; p.Breath = s.BreathMax; } },
        new() { Id = "lv_armor", Name = "Grit", Desc = "Take 3% less damage.", Icon = "life", MaxStacks = 10, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.03f) },
        new() { Id = "lv_invuln", Name = "Composure", Desc = "+0.05 s of invulnerability after being struck.", Icon = "life", MaxStacks = 8, Apply = (s, p) => s.HurtInvuln += 0.05f },
        // swordsman
        new() { Id = "lv_dodge", Name = "Footwork", Desc = "Dodge cooldown -6%.", Icon = "dodge", For = S, MaxStacks = 8, Apply = (s, p) => s.DodgeCdMult *= 0.94f },
        new() { Id = "lv_charge", Name = "Resolve", Desc = "Charged Strike recharges 6% faster.", Icon = "charge", For = S, MaxStacks = 8, Apply = (s, p) => s.ChargeCooldown *= 0.94f },
        // warden
        new() { Id = "lv_shield", Name = "Bulwark", Desc = "+8 shield strength.", Icon = "shield", For = W, MaxStacks = 12, Weight = 1.4f, Apply = (s, p) => s.ShieldMax += 8 },
        new() { Id = "lv_regen", Name = "Mending", Desc = "Shield regenerates 0.6 more per second.", Icon = "shield", For = W, MaxStacks = 10, Weight = 1.4f, Apply = (s, p) => s.ShieldRegen += 0.6f },
        new() { Id = "lv_break", Name = "Steadfast", Desc = "A broken shield recovers 0.6 s sooner.", Icon = "shield", For = W, MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.ShieldBreakTime = Math.Max(1.5f, s.ShieldBreakTime - 0.6f) },
        new() { Id = "lv_dash", Name = "Momentum", Desc = "Shield dash recharges 6% faster.", Icon = "shield", For = W, MaxStacks = 8, Apply = (s, p) => s.DashCooldown *= 0.94f },
        // vitalist
        new() { Id = "lv_alimus", Name = "Reservoir", Desc = "Hold 3 more alimus.", Icon = "spell", For = V, MaxStacks = 10, Apply = (s, p) => s.AlimusMax += 3f },
        new() { Id = "lv_heal", Name = "Kindness", Desc = "Your heal restores 6% more.", Icon = "life", For = V, MaxStacks = 10, Apply = (s, p) => s.HealMult += 0.06f },
        new() { Id = "lv_hex", Name = "Malice", Desc = "Hex recharges 6% faster.", Icon = "spell", For = V, MaxStacks = 8, Apply = (s, p) => s.HexCooldown *= 0.94f },
    };

    /// <summary>The big picks offered every few levels (Meta.MilestoneEvery): +15% each.</summary>
    public static readonly List<Upgrade> Milestone = new()
    {
        new() { Id = "m_dmg", Name = "Might", Desc = "+15% damage.", Icon = "blade", MaxStacks = 6, Apply = (s, p) => s.DamageMult *= 1.15f },
        new() { Id = "m_atk", Name = "Haste", Desc = "Attack 15% faster.", Icon = "blade", MaxStacks = 6, Apply = (s, p) => s.AttackSpeed *= 1.15f },
        new() { Id = "m_hp", Name = "Fortitude", Desc = "+15% max health (and heal that much).", Icon = "life", MaxStacks = 6, Apply = (s, p) => { float add = s.MaxHp * 0.15f; s.MaxHp += add; p.Heal(add); } },
        new() { Id = "m_armor", Name = "Iron Skin", Desc = "Take 15% less damage.", Icon = "life", MaxStacks = 4, Apply = (s, p) => s.DamageReduction = Math.Min(0.75f, 1 - (1 - s.DamageReduction) * 0.85f) },
        new() { Id = "m_move", Name = "Fleetness", Desc = "Run and swim 15% faster.", Icon = "move", MaxStacks = 4, Apply = (s, p) => { s.MoveSpeed *= 1.15f; s.SwimSpeed *= 1.15f; } },
        new() { Id = "m_reach", Name = "Long Arm", Desc = "+15% reach.", Icon = "blade", MaxStacks = 3, Apply = (s, p) => s.DaggerReach *= 1.15f },
        new() { Id = "m_dodge", Name = "Wind Runner", Desc = "Dodges and Charged Strike recharge 15% faster.", Icon = "dodge", For = S, MaxStacks = 4, Apply = (s, p) => { s.DodgeCdMult *= 0.85f; s.ChargeCooldown *= 0.85f; } },
        new() { Id = "m_shield", Name = "Aegis", Desc = "+15% shield strength and regeneration.", Icon = "shield", For = W, MaxStacks = 4, Apply = (s, p) => { s.ShieldMax *= 1.15f; s.ShieldRegen *= 1.15f; } },
        new() { Id = "m_vanguard", Name = "Vanguard", Desc = "Shield dash recharges 15% faster, and your shield stops 5% more.", Icon = "shield", For = W, MaxStacks = 4, Apply = (s, p) => { s.DashCooldown *= 0.85f; s.BlockShare = Math.Min(0.95f, s.BlockShare + 0.05f); } },
        new() { Id = "m_font", Name = "Wellspring", Desc = "+15% alimus gained and heal strength.", Icon = "spell", For = V, MaxStacks = 4, Apply = (s, p) => { s.AlimusGain *= 1.15f; s.HealMult *= 1.15f; } },
    };

    /// <summary>The "leave it" card on reward screens: nothing now, an ember if the level's guardian falls.</summary>
    public static readonly Upgrade Skip = new() { Id = "skip", Name = "Leave It", Desc = "Take nothing. Earn 1 ember (kept between runs) if you defeat this level's guardian.", Icon = "skip", MaxStacks = 9999, Apply = (s, p) => { } };

    public static IEnumerable<Upgrade> All => Chest.Concat(LevelUp).Concat(Milestone).Append(Skip);

    /// <summary>Three milestone picks.</summary>
    public static List<Upgrade> RollMilestone(PlayerStats s, Random rng) => Roll(Milestone, s, 3, rng, u => u.Weight);

    public static Upgrade Get(string id) => All.First(u => u.Id == id);

    /// <summary>The hero's ability button, by name (for card texts).</summary>
    public static string AbilityName(HeroKind h) => h switch
    {
        HeroKind.Warden => "shield dash",
        HeroKind.Vitalist => "heal",
        _ => "Charged Strike",
    };

    /// <summary>A card's text for this hero.</summary>
    public static string DescFor(Upgrade u, PlayerStats s) => s == null ? u.Desc.Replace("{ability}", "ability") : u.Desc.Replace("{ability}", AbilityName(s.Hero));

    public static bool Available(Upgrade u, PlayerStats s)
    {
        if (u.For != null && Array.IndexOf(u.For, s.Hero) < 0) return false;
        if (s.StackOf(u.Id) >= u.MaxStacks) return false;
        if (u.Requires != null && s.StackOf(u.Requires) == 0) return false;
        foreach (var ex in u.Excludes) if (s.StackOf(ex) > 0) return false;
        return true;
    }

    /// <summary>Three small stat nudges for a level-up.</summary>
    public static List<Upgrade> RollLevelUp(PlayerStats s, Random rng) => Roll(LevelUp, s, 3, rng, u => u.Weight);

    /// <summary>
    /// Three chest upgrades. Where the chest was found tilts the odds: movement upgrades are
    /// likelier underwater, survival ones in the upper third of the cave.
    /// </summary>
    public static List<Upgrade> RollChest(PlayerStats s, Random rng, Vector2 at)
    {
        var cave = G.Cave;
        bool underwater = cave != null && at.Y > cave.WaterY;
        bool high = cave != null && at.Y < cave.WaterY * Tune.Drops.HighZoneFraction;
        return Roll(Chest, s, 3, rng, u =>
        {
            float w = u.Weight * (u.Tier == UpgradeTier.Ability ? 1.5f : 1f);
            if (underwater && u.Icon == "move") w *= Tune.Drops.ZoneBias;
            if (high && u.Icon == "life") w *= Tune.Drops.ZoneBias;
            return w;
        });
    }

    private static List<Upgrade> Roll(List<Upgrade> from, PlayerStats s, int count, Random rng, Func<Upgrade, float> weight)
    {
        var pool = from.Where(u => Available(u, s)).ToList();
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
/// on toughness, the vitalist on its reserves of alimus.</summary>
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
