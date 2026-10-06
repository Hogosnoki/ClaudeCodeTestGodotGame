using System;
using System.Collections.Generic;

namespace DaggerCave;

/// <summary>
/// A feeling a lost Delver's page leaves in whoever reads it: an <b>inspiration</b> or a <b>sorrow</b> that turns to something useful (a
/// little higher jump, a little less damage taken). It lasts until the hero leaves the cave (the run's end), over every level between.
/// </summary>
public sealed class RunBuff
{
    public string Id, Name, Text;
    /// <summary>Inspired (a lift) or saddened (a weight that steadies).</summary>
    public bool Inspired;
    public Action<PlayerStats> Apply;
}

public static class RunBuffs
{
    private static RunBuff B(string id, string name, bool inspired, string text, Action<PlayerStats> apply) => new() { Id = id, Name = name, Inspired = inspired, Text = text, Apply = apply };

    /// <summary>What each kind of level's page leaves with the reader.</summary>
    public static readonly Dictionary<BiomeId, RunBuff> ForPage = new()
    {
        [BiomeId.Den] = B("den", "Marta's caution", false, "-8% damage taken", s => s.DamageTakenMult *= 0.92f),
        [BiomeId.Roots] = B("roots", "Hob's grip", true, "+5% jump height", s => s.JumpMult *= 1.05f),
        [BiomeId.Nest] = B("nest", "Ines's held breath", false, "+25% breath", s => s.BreathMax *= 1.25f),
        [BiomeId.Ruins] = B("ruins", "Orrin's curiosity", true, "-10% ability and dodge cooldowns", s => { s.AbilityCdMult *= 0.9f; s.DodgeCdMult *= 0.9f; }),
        [BiomeId.Mine] = B("mine", "Dask's craft", true, "+8% damage", s => s.DamageMult *= 1.08f),
        [BiomeId.Catacombs] = B("catacombs", "Aveline's requiem", false, "+20% healing received", s => s.HealingTakenMult *= 1.2f),
        [BiomeId.Fungal] = B("fungal", "Tam's last garden", false, "+15% healing given, +10% received", s => { s.HealMult *= 1.15f; s.HealingTakenMult *= 1.1f; }),
        [BiomeId.Tunnels] = B("tunnels", "Hale's road", true, "+7% movement speed (further jumps)", s => s.MoveSpeed *= 1.07f),
        [BiomeId.Slime] = B("slime", "Ines's warning", false, "+15% swim speed", s => s.SwimSpeed *= 1.15f),
        [BiomeId.Frost] = B("frost", "Cael's cold resolve", false, "-8% damage taken", s => s.DamageTakenMult *= 0.92f),
        [BiomeId.Fossils] = B("fossils", "Orrin's wonder", true, "+6% damage", s => s.DamageMult *= 1.06f),
        [BiomeId.Crystal] = B("crystal", "Hob's song", true, "+8% attack speed", s => s.AttackSpeed *= 1.08f),
        [BiomeId.LavaTubes] = B("lavatubes", "Dask's lamp", true, "+6% jump height, +3% speed", s => { s.JumpMult *= 1.06f; s.MoveSpeed *= 1.03f; }),
        [BiomeId.Magma] = B("magma", "Brandt's warning", false, "-10% damage taken", s => s.DamageTakenMult *= 0.9f),
        [BiomeId.Abyss] = B("abyss", "Aveline's wonder", true, "+30% breath, +10% swim speed", s => { s.BreathMax *= 1.3f; s.SwimSpeed *= 1.1f; }),
    };

    /// <summary>The reader takes the page's feeling (once each: a second reading of the same page adds nothing). Null if it gave nothing new.</summary>
    public static RunBuff Grant(Player p, BiomeId id)
    {
        if (p == null || !ForPage.TryGetValue(id, out var b) || p.Buffs.Exists(x => x.Id == b.Id)) return null;
        b.Apply(p.Stats);
        p.Buffs.Add(b);
        return b;
    }
}
