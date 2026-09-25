using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The playable heroes.</summary>
public enum HeroKind { Swordsman, Warden }

/// <summary>Everything upgrades can change about the hero.</summary>
public sealed class PlayerStats
{
    public readonly HeroKind Hero;

    public float MaxHp = Tune.Hero.StartHp;
    public float HurtInvuln = Tune.Hero.HurtInvuln;      // seconds of invulnerability after being struck
    public float DamageMult = 1f;
    public float AttackSpeed = 1f;       // swing cooldown divisor
    public float DaggerReach = 1f;       // swing range multiplier
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

    // swordsman: dodge + thrown dagger
    public float DodgeCdMult = 1f;
    public int DodgeCharges = 1;
    public bool DodgeIFrames;
    public int ThrowCharges = 1;
    public float ThrowCooldown = Tune.Hero.ThrowCooldown;
    public int Bounces = 0;
    public bool Pierce;
    public float BleedShare;             // Rending Edge
    public bool Execute, CrescentWave, FanOfKnives;

    // warden: shield + barrier
    public float ShieldMax = Tune.Warden.ShieldHp;
    public float ShieldRegen = Tune.Warden.ShieldRegen;
    public float ShieldBreakTime = Tune.Warden.ShieldBreakTime;
    public float ShieldArcMult = 1f;
    public bool PerfectReflect, PerfectSoak;
    public float BarrierAmount = Tune.Warden.BarrierAmount;
    public float BarrierDuration = Tune.Warden.BarrierDuration;
    public float BarrierCooldown = Tune.Warden.BarrierCooldown;
    public bool BarrierThorns;
    public bool Stalwart, QuickMend, RestoringWard, LastStand;

    public readonly Dictionary<string, int> Stacks = new();
    public int StackOf(string id) => Stacks.TryGetValue(id, out var n) ? n : 0;

    public PlayerStats(HeroKind hero = HeroKind.Swordsman)
    {
        Hero = hero;
        if (hero == HeroKind.Warden)
        {
            MoveSpeed = Tune.Warden.MoveMult; JumpMult = Tune.Warden.JumpMult;
            MaxHp = Tune.Warden.StartHp; BreathMax += Tune.Warden.ExtraBreath;
        }
        else { MoveSpeed = Tune.Swordsman.MoveMult; JumpMult = Tune.Swordsman.JumpMult; }
    }
}

public sealed class Upgrade
{
    public string Id, Name, Desc;
    /// <summary>Category used for the card color: blade, throw, move, dodge, shield, life.</summary>
    public string Icon;
    public int MaxStacks = 1;
    public string Requires;
    public string[] Excludes = Array.Empty<string>();
    public float Weight = 1f;
    public Action<PlayerStats, Player> Apply;
    public UpgradeTier Tier = UpgradeTier.Common;
    /// <summary>Only this hero can get it (null = both).</summary>
    public HeroKind? Only;
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
    private const HeroKind S = HeroKind.Swordsman, W = HeroKind.Warden;

    public static readonly List<Upgrade> Chest = new()
    {
        // --- Blade ---
        new() { Id = "atkspd", Name = "Quick Hands", Desc = "Swing 18% faster.", Icon = "blade", MaxStacks = 5, Apply = (s, p) => s.AttackSpeed += 0.18f },
        new() { Id = "reach", Name = "Longer Blade", Desc = "Blade reach +22%.", Icon = "blade", MaxStacks = 3, Apply = (s, p) => s.DaggerReach += 0.22f },
        new() { Id = "dmg", Name = "Whetstone", Desc = "+15% damage.", Icon = "blade", MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.DamageMult += 0.15f },
        new() { Id = "combo", Name = "Flurry", Desc = "Your combo chains one more strike: landing it refunds the swing cooldown again.", Icon = "blade", MaxStacks = 3, Tier = UpgradeTier.Ability, Apply = (s, p) => s.ComboResets += 1 },
        new() { Id = "combo3", Name = "Finisher", Desc = "The last strike of a full combo hits much harder (x2 damage, wider arc).", Icon = "blade", Requires = "combo", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ThirdCombo = true },
        new() { Id = "pogo", Name = "Downward Thrust", Desc = "Aerial down-slashes bounce you off enemies and refresh air jumps.", Icon = "blade", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pogo = true },
        new() { Id = "knock", Name = "Heavy Pommel", Desc = "Your strikes knock enemies back much harder.", Icon = "blade", Tier = UpgradeTier.Ability, Apply = (s, p) => s.KnockbackLevel = Math.Max(1, s.KnockbackLevel) },
        new() { Id = "knock2", Name = "Crushing Blows", Desc = "Knockback strength increased.", Icon = "blade", MaxStacks = 2, Requires = "knock", Apply = (s, p) => s.KnockbackLevel += 1 },

        // --- Thrown dagger (swordsman) ---
        new() { Id = "bounce", Name = "Ricochet", Desc = "Thrown dagger bounces to another nearby enemy (+1 bounce per stack).", Icon = "throw", Only = S, MaxStacks = 3, Excludes = new[] { "pierce" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Bounces += 1 },
        new() { Id = "pierce", Name = "Skewer", Desc = "Thrown dagger pierces through every enemy in its path.", Icon = "throw", Only = S, Excludes = new[] { "bounce" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pierce = true },
        new() { Id = "throw2", Name = "Spare Dagger", Desc = "A second throwing charge.", Icon = "throw", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => { s.ThrowCharges = 2; p.SyncCharges(); } },
        new() { Id = "throwcd", Name = "Quick Recall", Desc = "Thrown daggers return 25% faster.", Icon = "throw", Only = S, MaxStacks = 2, Apply = (s, p) => s.ThrowCooldown *= 0.75f },

        // --- Sword techniques (swordsman) ---
        new() { Id = "rend", Name = "Rending Edge", Desc = "Sword hits make enemies bleed for 40% more damage over 3 s.", Icon = "blade", Only = S, MaxStacks = 2, Tier = UpgradeTier.Ability, Apply = (s, p) => s.BleedShare += Tune.Swordsman.BleedShare },
        new() { Id = "wave", Name = "Crescent Wave", Desc = "Swings loose a slicing wave that flies ahead and cuts through enemies (half damage, every 1.2 s).", Icon = "blade", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.CrescentWave = true },
        new() { Id = "execute", Name = "Executioner", Desc = "+60% damage to enemies below 35% health.", Icon = "blade", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Execute = true },
        new() { Id = "fan", Name = "Fan of Knives", Desc = "Each throw also flings two daggers at an angle (60% damage).", Icon = "throw", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.FanOfKnives = true },

        // --- Dodge (swordsman) ---
        new() { Id = "iframes", Name = "Phantom Step", Desc = "Dodging makes you briefly invulnerable.", Icon = "dodge", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DodgeIFrames = true },
        new() { Id = "dodgecd", Name = "Nimble", Desc = "Dodge cooldown -20%.", Icon = "dodge", Only = S, MaxStacks = 3, Apply = (s, p) => s.DodgeCdMult *= 0.8f },
        new() { Id = "dodge2", Name = "Second Wind", Desc = "Gain a second dodge charge.", Icon = "dodge", Only = S, Tier = UpgradeTier.Ability, Apply = (s, p) => { s.DodgeCharges = 2; p.SyncCharges(); } },

        // --- Shield and barrier (warden) ---
        new() { Id = "perfect_reflect", Name = "Riposte Guard", Desc = "A perfect block (raise the shield just before the hit) reflects projectiles back at enemies.", Icon = "shield", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectReflect = true },
        new() { Id = "perfect_soak", Name = "Iron Timing", Desc = "Perfect blocks cost your shield 70% less.", Icon = "shield", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.PerfectSoak = true },
        new() { Id = "shield_wide", Name = "Tower Shield", Desc = "Your shield covers a 30% wider arc.", Icon = "shield", Only = W, MaxStacks = 2, Apply = (s, p) => s.ShieldArcMult += 0.3f },
        new() { Id = "barrier_cd", Name = "Ward Ready", Desc = "Barrier cooldown -20%.", Icon = "shield", Only = W, MaxStacks = 3, Apply = (s, p) => s.BarrierCooldown *= 0.8f },
        new() { Id = "barrier_time", Name = "Lasting Ward", Desc = "Barrier lasts 2 s longer.", Icon = "shield", Only = W, MaxStacks = 3, Apply = (s, p) => s.BarrierDuration += 2f },
        new() { Id = "barrier_amt", Name = "Thick Ward", Desc = "Barrier absorbs 5 more damage.", Icon = "shield", Only = W, MaxStacks = 3, Apply = (s, p) => s.BarrierAmount += 5f },
        new() { Id = "stalwart", Name = "Stalwart", Desc = "Full speed with the shield raised, and hits never knock you back.", Icon = "shield", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Stalwart = true },
        new() { Id = "quickmend", Name = "Quick Mend", Desc = "Your shield starts regenerating almost at once after a block, and 50% faster.", Icon = "shield", Only = W, Apply = (s, p) => { s.QuickMend = true; s.ShieldRegen *= 1.5f; } },
        new() { Id = "restoring", Name = "Restoring Ward", Desc = "When your barrier fades, whatever it didn't absorb heals you.", Icon = "shield", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.RestoringWard = true },
        new() { Id = "laststand", Name = "Last Stand", Desc = "Once per depth, a killing blow leaves you at 1 HP, briefly invulnerable, with a fresh barrier.", Icon = "life", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.LastStand = true },
        new() { Id = "barrier_thorns", Name = "Thorned Ward", Desc = "While your barrier is up, melee attackers take back the damage they deal.", Icon = "shield", Only = W, Tier = UpgradeTier.Ability, Apply = (s, p) => s.BarrierThorns = true },

        // --- Movement ---
        new() { Id = "walljump", Name = "Wall Kick", Desc = "Jump off walls. Hold toward a wall to slide down it slowly.", Icon = "move", Tier = UpgradeTier.Ability, Apply = (s, p) => s.WallJump = true },
        new() { Id = "djump", Name = "Double Jump", Desc = "Jump once more in mid-air.", Icon = "move", Excludes = new[] { "airdash" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DoubleJump = true },
        new() { Id = "airdash", Name = "Air Dash", Desc = "Jumping in mid-air dashes in any direction you hold.", Icon = "move", Excludes = new[] { "djump" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.AirDash = true },
        new() { Id = "speed", Name = "Light Boots", Desc = "+10% movement speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.MoveSpeed += 0.10f },
        new() { Id = "jump", Name = "Spring Step", Desc = "+12% jump height.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.JumpMult += 0.12f },
        new() { Id = "swim", Name = "Webbed Gloves", Desc = "+25% swim speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.SwimSpeed += 0.25f },
        new() { Id = "breath", Name = "Deep Lungs", Desc = "+50% breath underwater.", Icon = "move", MaxStacks = 3, Apply = (s, p) => { s.BreathMax += 4f; p.Breath = s.BreathMax; } },

        // --- Survival ---
        new() { Id = "hp", Name = "Vitality", Desc = "+20 max HP and heal 20.", Icon = "life", MaxStacks = 5, Weight = 1.2f, Apply = (s, p) => { s.MaxHp += 20; p.Heal(20); } },
        new() { Id = "resilience", Name = "Resilience", Desc = "Stay invulnerable 0.2 s longer after being struck.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HurtInvuln += 0.2f },
        new() { Id = "armor", Name = "Toughened Hide", Desc = "Take 10% less damage.", Icon = "life", MaxStacks = 4, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.10f) },
        new() { Id = "leech", Name = "Thirsty Blade", Desc = "Heal 4% of damage dealt.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.LifeSteal += 0.04f },
        new() { Id = "onkill", Name = "Trophy Hunter", Desc = "Heal 3 HP on every kill.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HealOnKill += 3f },
        new() { Id = "magnet", Name = "Lodestone", Desc = "Pull experience from further away.", Icon = "life", MaxStacks = 2, Weight = 0.6f, Apply = (s, p) => s.MagnetMult += 0.6f },
    };

    /// <summary>Small, stackable stat nudges offered on level-up.</summary>
    public static readonly List<Upgrade> LevelUp = new()
    {
        new() { Id = "lv_hp", Name = "Vigor", Desc = "+8 max HP (and heal 8).", Icon = "life", MaxStacks = 20, Apply = (s, p) => { s.MaxHp += 8; p.Heal(8); } },
        new() { Id = "lv_dmg", Name = "Edge", Desc = "+6% damage.", Icon = "blade", MaxStacks = 20, Apply = (s, p) => s.DamageMult += 0.06f },
        new() { Id = "lv_atk", Name = "Tempo", Desc = "+6% swing speed.", Icon = "blade", MaxStacks = 15, Apply = (s, p) => s.AttackSpeed += 0.06f },
        new() { Id = "lv_reach", Name = "Extension", Desc = "+5% blade reach.", Icon = "blade", MaxStacks = 10, Apply = (s, p) => s.DaggerReach += 0.05f },
        new() { Id = "lv_move", Name = "Stride", Desc = "+4% movement speed.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.MoveSpeed += 0.04f },
        new() { Id = "lv_jump", Name = "Spring", Desc = "+5% jump height.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.JumpMult += 0.05f },
        new() { Id = "lv_swim", Name = "Stroke", Desc = "+10% swim speed.", Icon = "move", MaxStacks = 10, Apply = (s, p) => s.SwimSpeed += 0.10f },
        new() { Id = "lv_breath", Name = "Lungs", Desc = "+1.5 s of breath.", Icon = "move", MaxStacks = 10, Apply = (s, p) => { s.BreathMax += 1.5f; p.Breath = s.BreathMax; } },
        new() { Id = "lv_armor", Name = "Grit", Desc = "Take 3% less damage.", Icon = "life", MaxStacks = 10, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.03f) },
        new() { Id = "lv_invuln", Name = "Composure", Desc = "+0.05 s of invulnerability after being struck.", Icon = "life", MaxStacks = 8, Apply = (s, p) => s.HurtInvuln += 0.05f },
        // swordsman
        new() { Id = "lv_dodge", Name = "Footwork", Desc = "Dodge cooldown -6%.", Icon = "dodge", Only = S, MaxStacks = 8, Apply = (s, p) => s.DodgeCdMult *= 0.94f },
        new() { Id = "lv_throw", Name = "Quick Draw", Desc = "Thrown dagger recharges 6% faster.", Icon = "throw", Only = S, MaxStacks = 8, Apply = (s, p) => s.ThrowCooldown *= 0.94f },
        // warden
        new() { Id = "lv_shield", Name = "Bulwark", Desc = "+8 shield strength.", Icon = "shield", Only = W, MaxStacks = 12, Weight = 1.4f, Apply = (s, p) => s.ShieldMax += 8 },
        new() { Id = "lv_regen", Name = "Mending", Desc = "Shield regenerates 0.6 more per second.", Icon = "shield", Only = W, MaxStacks = 10, Weight = 1.4f, Apply = (s, p) => s.ShieldRegen += 0.6f },
        new() { Id = "lv_break", Name = "Resolve", Desc = "A broken shield recovers 0.6 s sooner.", Icon = "shield", Only = W, MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.ShieldBreakTime = Math.Max(1.5f, s.ShieldBreakTime - 0.6f) },
    };

    public static IEnumerable<Upgrade> All => Chest.Concat(LevelUp);

    public static Upgrade Get(string id) => All.First(u => u.Id == id);

    public static bool Available(Upgrade u, PlayerStats s)
    {
        if (u.Only != null && u.Only != s.Hero) return false;
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
