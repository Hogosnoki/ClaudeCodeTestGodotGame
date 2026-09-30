using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A permanent, class-specific upgrade bought with embers between runs. Every perk has one rank or
/// three; once a rank is bought it is yours for good, and before a run you choose which of a hero's
/// perks to bring (a few slots: see <see cref="Tune.Perks"/>).
/// </summary>
public sealed class ClassPerk
{
    public string Id, Name;
    public HeroKind Hero;
    /// <summary>Embers for each rank (its length is the number of ranks).</summary>
    public int[] Costs;
    /// <summary>What each rank does, in words (one line per rank, the later ones complete).</summary>
    public string[] Ranks;
    /// <summary>Changes the hero's stats for a run, given the rank owned (1-based).</summary>
    public Action<PlayerStats, int> Apply;
    public int MaxRank => Costs.Length;
}

public static class ClassPerks
{
    public static readonly List<ClassPerk> All = new();

    private static void P(HeroKind hero, string id, string name, int[] costs, string[] ranks, Action<PlayerStats, int> apply)
        => All.Add(new ClassPerk { Id = id, Name = name, Hero = hero, Costs = costs, Ranks = ranks, Apply = apply });
    private static readonly int[] Three = { 2, 3, 4 }, One = { 3 };

    static ClassPerks()
    {
        // ---- Swordsman
        P(HeroKind.Swordsman, "sword_dmg", "Honed Edge", Three, new[] { "+5% damage.", "+10% damage.", "+15% damage." }, (s, r) => s.DamageMult += 0.05f * r);
        P(HeroKind.Swordsman, "sword_haste", "Quick Wrists", Three, new[] { "Swings come 15% sooner.", "Swings come 30% sooner.", "Swings come 45% sooner." }, (s, r) => s.AttackSpeed *= 1f / (1f - 0.15f * r));
        P(HeroKind.Swordsman, "sword_heave_refund", "Shake it Off", One, new[] { "A heaving swing that strikes nothing is ready again in half the time." }, (s, r) => s.HeaveRefund = true);
        P(HeroKind.Swordsman, "sword_heave_guard", "Braced", One, new[] { "While you heave, you take half damage." }, (s, r) => s.HeaveGuard = true);
        P(HeroKind.Swordsman, "sword_combo", "Flow", One, new[] { "Your combo chains one more strike from the start." }, (s, r) => s.ComboResets += 1);

        // ---- Warden
        P(HeroKind.Warden, "warden_window", "Steady Nerve", One, new[] { "Perfect blocks are twice as forgiving." }, (s, r) => s.PerfectWindowMult = 2f);
        P(HeroKind.Warden, "warden_coyote", "Coyote Shield", One, new[] { "Near misses still block: a wider arc, and a grace moment after you lower it." }, (s, r) => s.CoyoteShield = true);
        P(HeroKind.Warden, "warden_dr", "Iron Skin", Three, new[] { "10% less damage taken.", "20% less damage taken.", "30% less damage taken." }, (s, r) => s.DamageReduction = Math.Min(0.75f, s.DamageReduction + 0.1f * r));
        P(HeroKind.Warden, "warden_hp", "Stout", Three, new[] { "+10% health.", "+20% health.", "+30% health." }, (s, r) => s.MaxHp *= 1f + 0.1f * r);
        P(HeroKind.Warden, "warden_indignation", "Indignation", One, new[] { "Being struck makes you hit 30% harder for 4 s." }, (s, r) => s.Indignation = true);

        // ---- Vitalist
        P(HeroKind.Vitalist, "vit_drain_heal", "Sip of Life", Three, new[] { "10% chance for your drain to heal you 1 HP.", "20% chance for your drain to heal you 1 HP.", "30% chance for your drain to heal you 1 HP." }, (s, r) => s.DrainHealChance = 0.1f * r);
        P(HeroKind.Vitalist, "vit_force", "Deep Reserves", Three, new[] { "+5 vital force.", "+10 vital force.", "+15 vital force." }, (s, r) => s.VitalForceMax += 5f * r);
        P(HeroKind.Vitalist, "vit_ward", "Mending Ward", Three, new[] { "Those you've healed take 3% less damage for a while.", "Those you've healed take 6% less damage for a while.", "Those you've healed take 10% less damage for a while." }, (s, r) => s.HotWard = new[] { 0f, 0.03f, 0.06f, 0.1f }[r]);
        P(HeroKind.Vitalist, "vit_heal", "Gentle Hands", Three, new[] { "Your heal restores 10% more.", "Your heal restores 20% more.", "Your heal restores 30% more." }, (s, r) => s.HealMult += 0.1f * r);
        P(HeroKind.Vitalist, "vit_rupture", "Ruinous", Three, new[] { "Rupture's burst hurts what's around it 25% more.", "Rupture's burst hurts what's around it 50% more.", "Rupture's burst hurts what's around it 75% more." }, (s, r) => s.RuptureSplashMult += 0.25f * r);

        // ---- Elementalist
        P(HeroKind.Elementalist, "ele_alimus", "Deep Well", Three, new[] { "+5 alimus.", "+10 alimus.", "+15 alimus." }, (s, r) => s.AlimusMax += 5f * r);
        P(HeroKind.Elementalist, "ele_regen", "Attunement", Three, new[] { "Alimus comes back 10% faster.", "Alimus comes back 20% faster.", "Alimus comes back 30% faster." }, (s, r) => s.AlimusRegenMult += 0.1f * r);
        P(HeroKind.Elementalist, "ele_haste", "Quickened", Three, new[] { "Bolts fly 5% faster in succession.", "Bolts fly 10% faster in succession.", "Bolts fly 15% faster in succession." }, (s, r) => s.AttackSpeed *= 1f + 0.05f * r);
        P(HeroKind.Elementalist, "ele_elements", "Kindred Elements", Three, new[] { "+5% chance to ignite or freeze.", "+10% chance to ignite or freeze.", "+15% chance to ignite or freeze." }, (s, r) => { s.IgniteChance += 0.05f * r; s.FreezeBonus += 0.05f * r; });
        P(HeroKind.Elementalist, "ele_storm", "Wide Weather", Three, new[] { "Your storms are 10% wider.", "Your storms are 20% wider.", "Your storms are 30% wider." }, (s, r) => s.BlizzardWideMult += 0.1f * r);

        // ---- Rogue
        P(HeroKind.Rogue, "rogue_dmg", "Sharpened Steel", Three, new[] { "+5% damage.", "+10% damage.", "+15% damage." }, (s, r) => s.DamageMult += 0.05f * r);
        P(HeroKind.Rogue, "rogue_crit", "Killer's Eye", Three, new[] { "+3% critical chance.", "+6% critical chance.", "+9% critical chance." }, (s, r) => s.CritChance += 0.03f * r);
        P(HeroKind.Rogue, "rogue_throw", "Quick Draw", Three, new[] { "Daggers can be thrown again 10% sooner.", "Daggers can be thrown again 20% sooner.", "Daggers can be thrown again 30% sooner." }, (s, r) => s.ThrowCdMult *= 1f - 0.1f * r);
        P(HeroKind.Rogue, "rogue_bleed", "Serrated Edges", One, new[] { "A recall has a chance to make what it cuts bleed." }, (s, r) => s.RecallBleed = true);
        P(HeroKind.Rogue, "rogue_shadows", "Long Shadows", Three, new[] { "Vanish lasts 1 s longer.", "Vanish lasts 2 s longer.", "Vanish lasts 3 s longer." }, (s, r) => s.VanishBonus += r);
    }

    public static IEnumerable<ClassPerk> For(HeroKind hero) => All.Where(p => p.Hero == hero);
    public static ClassPerk Find(string id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>The ranks this run brings: the hero's equipped perks, each at the rank owned.</summary>
    public static void Apply(PlayerStats s)
    {
        foreach (var perk in For(s.Hero))
        {
            int rank = Math.Min(Meta.PerkRank(perk.Id), perk.MaxRank);
            if (rank > 0 && Meta.PerkEquipped(perk.Id)) perk.Apply(s, rank);
        }
    }

    public static int EquippedCount(HeroKind hero) => For(hero).Count(p => Meta.PerkRank(p.Id) > 0 && Meta.PerkEquipped(p.Id));

    /// <summary>The names of the perks a hero brings (for the camp's strip).</summary>
    public static string EquippedNames(HeroKind hero)
    {
        var names = For(hero).Where(p => Meta.PerkRank(p.Id) > 0 && Meta.PerkEquipped(p.Id)).Select(p => p.MaxRank > 1 ? $"{p.Name} {new[] { "", "I", "II", "III" }[Math.Min(3, Meta.PerkRank(p.Id))]}" : p.Name).ToList();
        return names.Count == 0 ? "" : string.Join("  ·  ", names);
    }
}
