using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>How rare a graded card is: the higher, the bigger its effect, the rarer it's dealt and the grander it looks.</summary>
public enum CardGrade { None, Common, Uncommon, Rare, Epic, Legendary }

/// <summary>
/// Boons: plain growth dealt with a rarity, like loot (<see cref="Tune.Boons"/>). Each family (defence, health, attack, speed, jump...)
/// comes in grades from Common to Legendary, each grade a bigger step; the plain stats come at every grade (defence the most often,
/// then health and attack, then speed, then jumping), and rarer things only from Uncommon or Rare up: a chance to shrug a blow off
/// entirely, critical hits, an extra jump in the air, speed in the air, attack rate, shorter rests for every ability, a harder special.
/// A level's pick deals some of these beside the hero's own cards; a milestone deals one of Epic grade or better.
/// </summary>
public static class Boons
{
    private sealed class Family
    {
        public string Key, Icon, Tag;
        /// <summary>A card's name at each grade (Common..Legendary; null: not dealt at that grade).</summary>
        public string[] Names;
        /// <summary>The step at each grade (0: not dealt at that grade).</summary>
        public float[] Steps;
        public float Weight;
        public Func<float, string> Desc;
        public Action<PlayerStats, Player, float> Apply;
        public Func<PlayerStats, bool> When;
    }

    private static readonly Family[] Families =
    {
        new() { Key = "def", Icon = "shield", Tag = "defence", Weight = 10f,
            Names = new[] { "Sturdy", "Stalwart", "Ironclad", "Adamant", "Unbreakable" },
            Steps = new[] { 0.04f, 0.07f, 0.11f, 0.16f, 0.24f },
            Desc = x => $"Take {x * 100f:0}% less damage.",
            Apply = (s, p, x) => s.DamageTakenMult *= 1f - x },
        new() { Key = "hp", Icon = "life", Tag = "defence", Weight = 8f,
            Names = new[] { "Hale", "Hardy", "Vigorous", "Titanic", "Undying" },
            Steps = new[] { 0.05f, 0.08f, 0.12f, 0.17f, 0.25f },
            Desc = x => $"{x * 100f:0}% more health (and that much mended).",
            Apply = (s, p, x) => { float more = s.MaxHp * x; s.MaxHp += more; p?.Heal(more); } },
        new() { Key = "atk", Icon = "blade", Tag = "offence", Weight = 8f,
            Names = new[] { "Sharpened", "Honed", "Brutal", "Savage", "Godslayer" },
            Steps = new[] { 0.05f, 0.09f, 0.14f, 0.20f, 0.30f },
            Desc = x => $"{x * 100f:0}% more damage with everything.",
            Apply = (s, p, x) => s.DamageMult *= 1f + x },
        new() { Key = "spd", Icon = "move", Tag = "mobility", Weight = 6f,
            Names = new[] { "Brisk", "Swift", "Fleetfoot", "Windrunner", "Quicksilver" },
            Steps = new[] { 0.04f, 0.07f, 0.10f, 0.14f, 0.20f },
            Desc = x => $"Run {x * 100f:0}% faster.",
            Apply = (s, p, x) => s.MoveSpeed *= 1f + x },
        new() { Key = "jmp", Icon = "move", Tag = "mobility", Weight = 4.5f,
            Names = new[] { "Springy", "Bounding", "Soaring", "Skyward", "Weightless" },
            Steps = new[] { 0.05f, 0.08f, 0.12f, 0.17f, 0.24f },
            Desc = x => $"Jump {x * 100f:0}% higher.",
            Apply = (s, p, x) => s.JumpMult *= (1f + x) * (1f + x) }, // (jump speed goes as the square root of JumpMult: this is height)
        new() { Key = "rate", Icon = "blade", Tag = "offence", Weight = 3f,
            Names = new[] { null, "Quick Hands", "Flurry", "Frenzy", "Tempest" },
            Steps = new[] { 0f, 0.06f, 0.09f, 0.13f, 0.18f },
            Desc = x => $"Attack {x * 100f:0}% faster.",
            Apply = (s, p, x) => s.AttackSpeed *= 1f + x },
        new() { Key = "crit", Icon = "blade", Tag = "offence", Weight = 3f,
            Names = new[] { null, "Keen Eye", "Precision", "Executioner", "Deathmark" },
            Steps = new[] { 0f, 0.03f, 0.05f, 0.08f, 0.12f },
            Desc = x => $"Your hits are {x * 100f:0}% likelier to be critical (x1.5, more with Deadly Aim).",
            Apply = (s, p, x) => s.GenCritChance += x,
            When = s => s.GenCritChance < 0.6f },
        new() { Key = "haste", Icon = "charge", Tag = "utility", Weight = 3f,
            Names = new[] { null, "Focus", "Clarity", "Flow", "Timeless" },
            Steps = new[] { 0f, 0.06f, 0.09f, 0.13f, 0.18f },
            Desc = x => $"Every ability rests {x * 100f:0}% less.",
            Apply = (s, p, x) => RestAll(s, 1f - x) },
        new() { Key = "air", Icon = "move", Tag = "mobility", Weight = 2.5f,
            Names = new[] { null, "Drifting", "Gliding", "Wind-Rider", "Skydancer" },
            Steps = new[] { 0f, 0.08f, 0.12f, 0.18f, 0.26f },
            Desc = x => $"Move {x * 100f:0}% faster in the air.",
            Apply = (s, p, x) => s.AirSpeedMult *= 1f + x },
        new() { Key = "ward", Icon = "shield", Tag = "defence", Weight = 2f,
            Names = new[] { null, "Lucky Charm", "Blessed Ward", "Fateweaver", "Untouchable" },
            Steps = new[] { 0f, 0.03f, 0.05f, 0.08f, 0.12f },
            Desc = x => $"{x * 100f:0}% chance to shrug a blow off entirely.",
            Apply = (s, p, x) => s.NegateChance = Math.Min(Tune.Boons.NegateCap, s.NegateChance + x),
            When = s => s.NegateChance < Tune.Boons.NegateCap },
        new() { Key = "spec", Icon = "spell", Tag = "special", Weight = 2f,
            Names = new[] { null, null, "Empowered", "Overcharged", "Cataclysmic" },
            Steps = new[] { 0f, 0f, 0.15f, 0.25f, 0.40f },
            Desc = x => $"Your special strikes {x * 100f:0}% harder.",
            Apply = (s, p, x) => s.SpecialDamageMult *= 1f + x,
            When = s => Cards.Grows(s.Hero) },
        new() { Key = "leap", Icon = "move", Tag = "mobility", Weight = 1.2f,
            Names = new[] { null, null, null, "Cloud Step", "Wings of Dawn" },
            Steps = new[] { 0f, 0f, 0f, 1f, 2f },
            Desc = x => x > 1.5f ? "Two more jumps in mid-air." : "One more jump in mid-air.",
            Apply = (s, p, x) => s.ExtraAirJumps += (int)MathF.Round(x),
            When = s => s.ExtraAirJumps < 3 },
        new() { Key = "luck", Icon = "fortune", Tag = "economy", Weight = 2.5f,
            Names = new[] { "Four-Leaf", "Lucky Find", "Fortune's Favour", "Treasure Sense", "Midas Touch" },
            Steps = new[] { 0.10f, 0.18f, 0.30f, 0.45f, 0.70f },
            Desc = x => $"Rare cards and relics come {x * 100f:0}% more often; Epic {x * 150f:0}% and Legendary {x * 200f:0}% more.",
            Apply = (s, p, x) => s.Luck += x,
            When = s => s.Luck < Tune.Boons.LuckCap },
    };

    /// <summary>How much more often fortune deals a card of this grade (Rare: 1+luck, Epic: 1+1.5 luck, Legendary: 1+2 luck).</summary>
    public static float LuckFactor(CardGrade g, float luck) => g switch
    {
        CardGrade.Rare => 1f + luck, CardGrade.Epic => 1f + luck * 1.5f, CardGrade.Legendary => 1f + luck * 2f, _ => 1f,
    };

    /// <summary>The same for a relic's rarity.</summary>
    public static float LuckFactor(RelicRarity r, float luck) => r switch
    {
        RelicRarity.Rare => 1f + luck, RelicRarity.Legendary => 1f + luck * 2f, _ => 1f,
    };

    /// <summary>Every ability's rest cut to this share (the special, the second ability, the dodge; the attack's own speed is Attack Rate's).</summary>
    private static void RestAll(PlayerStats s, float f)
    {
        s.SpecialRestMult *= f;
        s.OtherRestMult *= f;
        s.DefensiveRestMult *= f;
        s.AbilityCdMult *= f;
        switch (s.Hero)
        {
            case HeroKind.Swordsman: s.DodgeCdMult *= f; break;
            case HeroKind.Warden: s.BashCooldown *= f; break;
            case HeroKind.Vitalist: s.HealCostMult *= f; break;
            case HeroKind.Aegis: s.BarrierCdMult *= f; break;
            case HeroKind.Rogue: s.VanishCdMult *= f; break;
        }
    }

    private static readonly CardGrade[] Grades = { CardGrade.Common, CardGrade.Uncommon, CardGrade.Rare, CardGrade.Epic, CardGrade.Legendary };

    /// <summary>Every boon card: one per family and grade it comes at (ids "b_def_3": defence, Rare).</summary>
    private static readonly Dictionary<Upgrade, Family> _family = new();
    public static readonly List<Upgrade> All = Build();

    private static List<Upgrade> Build()
    {
        var list = new List<Upgrade>();
        foreach (var f in Families)
            for (int g = 0; g < 5; g++)
            {
                if (f.Steps[g] <= 0f || f.Names[g] == null) continue;
                float step = f.Steps[g];
                var fam = f;
                var u = new Upgrade
                {
                    Id = $"b_{f.Key}_{g + 1}", Name = f.Names[g], Desc = f.Desc(step), Icon = f.Icon, Grade = Grades[g], MaxStacks = 99,
                    Tags = new[] { f.Tag, "boon" }, When = f.When,
                    Apply = (s, p) => fam.Apply(s, p, step),
                };
                _family[u] = f;
                list.Add(u);
            }
        return list;
    }

    public static bool IsBoon(Upgrade u) => u.Grade != CardGrade.None && _family.ContainsKey(u);
    private static string FamilyOf(Upgrade u) => _family.TryGetValue(u, out var f) ? f.Key : "";

    /// <summary>A grade, as luck deals it (a little luckier deeper down), never below <paramref name="least"/>.</summary>
    public static CardGrade RollGrade(Random rng, CardGrade least = CardGrade.Common, float fortune = 0f)
    {
        float luck = 1f + Math.Max(0, G.Depth) * Tune.Boons.LuckPerDepth;
        float[] w = Tune.Boons.GradeWeights.Select((x, k) => (k >= 2 ? x * luck : x) * LuckFactor(Grades[k], fortune)).ToArray();
        for (int k = 0; k < 5; k++) if (Grades[k] < least) w[k] = 0f;
        float r = (float)rng.NextDouble() * w.Sum();
        for (int k = 0; k < 5; k++) { r -= w[k]; if (r <= 0f) return Grades[k]; }
        return least;
    }

    /// <summary>A boon of this grade for this hero, of a family not in <paramref name="taken"/> (null if none can be had).</summary>
    public static Upgrade Deal(PlayerStats s, Random rng, CardGrade grade, HashSet<string> taken)
    {
        var pool = All.Where(u => u.Grade == grade && !taken.Contains(FamilyOf(u)) && Upgrades.Available(u, s)).ToList();
        if (pool.Count == 0) return null;
        var pick = Upgrades.Pick(pool, 1, rng, u => _family[u].Weight).FirstOrDefault();
        if (pick != null) taken.Add(FamilyOf(pick));
        return pick;
    }

    /// <summary>A boon at a rolled grade (falling to a lower grade, then a higher one, if none of that grade can be had).</summary>
    public static Upgrade DealRolled(PlayerStats s, Random rng, HashSet<string> taken, CardGrade least = CardGrade.Common)
    {
        var g = RollGrade(rng, least, s.Luck);
        for (var k = g; k >= least; k--) if (Deal(s, rng, k, taken) is Upgrade u) return u;
        for (var k = g + 1; k <= CardGrade.Legendary; k++) if (Deal(s, rng, k, taken) is Upgrade u) return u;
        return null;
    }
}
