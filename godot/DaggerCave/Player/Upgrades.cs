using System;
using System.Collections.Generic;
using System.Linq;

namespace DaggerCave;

/// <summary>Everything upgrades can change about the dagger wielder.</summary>
public sealed class PlayerStats
{
    public float MaxHp = Tune.Hero.StartHp;
    public float HurtInvuln = Tune.Hero.HurtInvuln;      // seconds of invulnerability after being struck
    public float DamageMult = 1f;
    public float AttackSpeed = 1f;       // swing cooldown divisor
    public float DaggerReach = 1f;       // swing range multiplier
    public float MoveSpeed = 1f;
    public float JumpMult = 1f;
    public float SwimSpeed = 1f;
    public float BreathMax = Tune.Hero.BreathSeconds;         // seconds
    public float DodgeCdMult = 1f;
    public float DamageReduction = 0f;   // 0..1
    public float LifeSteal = 0f;         // fraction of damage dealt
    public float HealOnKill = 0f;        // hp per kill
    public int KnockbackLevel = 0;
    public int DodgeCharges = 1;
    public int ThrowCharges = 1;
    public float ThrowCooldown = Tune.Hero.ThrowCooldown;
    public int Bounces = 0;
    public bool Pierce, WallJump, DoubleJump, AirDash, Pogo, Combo, ThirdCombo, DodgeIFrames, ThrowReturn;
    public float MagnetMult = 1f;

    public readonly Dictionary<string, int> Stacks = new();
    public int StackOf(string id) => Stacks.TryGetValue(id, out var n) ? n : 0;
}

public sealed class Upgrade
{
    public string Id, Name, Desc;
    /// <summary>Category used for the card color: blade, throw, move, dodge, life.</summary>
    public string Icon;
    public int MaxStacks = 1;
    public string Requires;
    public string[] Excludes = Array.Empty<string>();
    public float Weight = 1f;
    public Action<PlayerStats, Player> Apply;
    public UpgradeTier Tier = UpgradeTier.Common;
}

public enum UpgradeTier { Common, Rare, Ability }

public static class Upgrades
{
    public static readonly List<Upgrade> All = new()
    {
        // --- Dagger ---
        new() { Id = "atkspd", Name = "Quick Hands", Desc = "Swing 18% faster.", Icon = "blade", MaxStacks = 5, Apply = (s, p) => s.AttackSpeed += 0.18f },
        new() { Id = "reach", Name = "Longer Blade", Desc = "Dagger reach +22%.", Icon = "blade", MaxStacks = 3, Apply = (s, p) => s.DaggerReach += 0.22f },
        new() { Id = "dmg", Name = "Whetstone", Desc = "+15% damage.", Icon = "blade", MaxStacks = 6, Weight = 1.2f, Apply = (s, p) => s.DamageMult += 0.15f },
        new() { Id = "combo", Name = "Flurry", Desc = "Striking an enemy resets your swing cooldown once, chaining a combo.", Icon = "blade", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Combo = true },
        new() { Id = "combo3", Name = "Finisher", Desc = "Adds a third combo strike that hits much harder (x2 damage, wider arc).", Icon = "blade", Requires = "combo", Tier = UpgradeTier.Ability, Apply = (s, p) => s.ThirdCombo = true },
        new() { Id = "pogo", Name = "Downward Thrust", Desc = "Aerial down-slashes bounce you off enemies and refresh air jumps.", Icon = "blade", Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pogo = true },
        new() { Id = "knock", Name = "Heavy Pommel", Desc = "Dagger strikes knock enemies back.", Icon = "blade", Tier = UpgradeTier.Ability, Apply = (s, p) => s.KnockbackLevel = Math.Max(1, s.KnockbackLevel) },
        new() { Id = "knock2", Name = "Crushing Blows", Desc = "Knockback strength increased.", Icon = "blade", MaxStacks = 2, Requires = "knock", Apply = (s, p) => s.KnockbackLevel += 1 },

        // --- Throw ---
        new() { Id = "bounce", Name = "Ricochet", Desc = "Thrown dagger bounces to another nearby enemy (+1 bounce per stack).", Icon = "throw", MaxStacks = 3, Excludes = new[] { "pierce" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Bounces += 1 },
        new() { Id = "pierce", Name = "Skewer", Desc = "Thrown dagger pierces through every enemy in its path.", Icon = "throw", Excludes = new[] { "bounce" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.Pierce = true },
        new() { Id = "throw2", Name = "Spare Dagger", Desc = "A second throwing charge. While any dagger is in hand you can keep swinging.", Icon = "throw", Tier = UpgradeTier.Ability, Apply = (s, p) => { s.ThrowCharges = 2; p.SyncCharges(); } },
        new() { Id = "throwcd", Name = "Quick Recall", Desc = "Thrown daggers return 25% faster.", Icon = "throw", MaxStacks = 2, Apply = (s, p) => s.ThrowCooldown *= 0.75f },

        // --- Movement ---
        new() { Id = "walljump", Name = "Wall Kick", Desc = "Jump off walls. Hold toward a wall to slide down it slowly.", Icon = "move", Tier = UpgradeTier.Ability, Apply = (s, p) => s.WallJump = true },
        new() { Id = "djump", Name = "Double Jump", Desc = "Jump once more in mid-air.", Icon = "move", Excludes = new[] { "airdash" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.DoubleJump = true },
        new() { Id = "airdash", Name = "Air Dash", Desc = "Jumping in mid-air dashes in any direction you hold.", Icon = "move", Excludes = new[] { "djump" }, Tier = UpgradeTier.Ability, Apply = (s, p) => s.AirDash = true },
        new() { Id = "speed", Name = "Light Boots", Desc = "+10% movement speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.MoveSpeed += 0.10f },
        new() { Id = "jump", Name = "Spring Step", Desc = "+12% jump height.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.JumpMult += 0.12f },
        new() { Id = "swim", Name = "Webbed Gloves", Desc = "+25% swim speed.", Icon = "move", MaxStacks = 3, Apply = (s, p) => s.SwimSpeed += 0.25f },
        new() { Id = "breath", Name = "Deep Lungs", Desc = "+50% breath underwater.", Icon = "move", MaxStacks = 3, Apply = (s, p) => { s.BreathMax += 4f; p.Breath = s.BreathMax; } },

        // --- Dodge ---
        new() { Id = "iframes", Name = "Phantom Step", Desc = "Dodging makes you briefly invulnerable.", Icon = "dodge", Tier = UpgradeTier.Ability, Apply = (s, p) => s.DodgeIFrames = true },
        new() { Id = "dodgecd", Name = "Nimble", Desc = "Dodge cooldown -20%.", Icon = "dodge", MaxStacks = 3, Apply = (s, p) => s.DodgeCdMult *= 0.8f },
        new() { Id = "dodge2", Name = "Second Wind", Desc = "Gain a second dodge charge.", Icon = "dodge", Tier = UpgradeTier.Ability, Apply = (s, p) => { s.DodgeCharges = 2; p.SyncCharges(); } },

        // --- Survival ---
        new() { Id = "hp", Name = "Vitality", Desc = "+20 max HP and heal 20.", Icon = "life", MaxStacks = 5, Weight = 1.2f, Apply = (s, p) => { s.MaxHp += 20; p.Heal(20); } },
        new() { Id = "resilience", Name = "Resilience", Desc = "Stay invulnerable 0.2 s longer after being struck.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HurtInvuln += 0.2f },
        new() { Id = "armor", Name = "Toughened Hide", Desc = "Take 10% less damage.", Icon = "life", MaxStacks = 4, Apply = (s, p) => s.DamageReduction = Math.Min(0.6f, s.DamageReduction + 0.10f) },
        new() { Id = "leech", Name = "Thirsty Blade", Desc = "Heal 4% of damage dealt.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.LifeSteal += 0.04f },
        new() { Id = "onkill", Name = "Trophy Hunter", Desc = "Heal 3 HP on every kill.", Icon = "life", MaxStacks = 3, Apply = (s, p) => s.HealOnKill += 3f },
        new() { Id = "magnet", Name = "Lodestone", Desc = "Pull experience from further away.", Icon = "life", MaxStacks = 2, Weight = 0.6f, Apply = (s, p) => s.MagnetMult += 0.6f },
    };

    public static Upgrade Get(string id) => All.First(u => u.Id == id);

    public static bool Available(Upgrade u, PlayerStats s)
    {
        if (s.StackOf(u.Id) >= u.MaxStacks) return false;
        if (u.Requires != null && s.StackOf(u.Requires) == 0) return false;
        foreach (var ex in u.Excludes) if (s.StackOf(ex) > 0) return false;
        return true;
    }

    /// <summary>Picks `count` distinct available upgrades, weighted (abilities slightly favored on treasure).</summary>
    public static List<Upgrade> Roll(PlayerStats s, int count, bool treasure, Random rng)
    {
        var pool = All.Where(u => Available(u, s)).ToList();
        var result = new List<Upgrade>();
        while (result.Count < count && pool.Count > 0)
        {
            float total = 0;
            foreach (var u in pool) total += W(u);
            double r = rng.NextDouble() * total;
            Upgrade pick = pool[^1];
            foreach (var u in pool) { r -= W(u); if (r <= 0) { pick = u; break; } }
            result.Add(pick);
            pool.Remove(pick);
        }
        return result;

        float W(Upgrade u) => u.Weight * (treasure && u.Tier == UpgradeTier.Ability ? 2.5f : 1f);
    }

    public static void Apply(Upgrade u, PlayerStats s, Player p)
    {
        s.Stacks[u.Id] = s.StackOf(u.Id) + 1;
        u.Apply(s, p);
    }
}
