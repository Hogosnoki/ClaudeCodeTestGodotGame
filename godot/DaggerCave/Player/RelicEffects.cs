using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>What the trigger-driven relics do (see Relics.cs, Triggers.cs): small, self-contained effects. A relic's own damage is Raw (no weakness, resistance or relic multiplier, and it raises no hit event of its own).</summary>
public static class RelicEffects
{
    private static double Now => Time.GetTicksMsec() / 1000.0;

    /// <summary>The creatures within <paramref name="radius"/> of a point, other than one, nearest first.</summary>
    public static System.Collections.Generic.List<Enemy> Near(Vector2 at, float radius, Enemy not = null)
        => G.Enemies.Where(e => GodotObject.IsInstanceValid(e) && !e.Dead && e.CanBeHit && e != not && e.GlobalPosition.DistanceTo(at) <= radius + e.HitRadius)
            .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(at)).ToList();

    private static void Zap(Vector2 a, Vector2 b, Color col)
    {
        int n = Math.Max(2, (int)(a.DistanceTo(b) / 8f));
        for (int k = 0; k <= n; k++) G.Fx.Ember(a.Lerp(b, k / (float)n) + new Vector2(G.Range(-3, 3), G.Range(-3, 3)), col);
    }

    /// <summary>Soul Lantern: a creature that falls bursts in a small blast.</summary>
    public static void SoulBurst(Player p, Enemy victim)
    {
        if (victim == null) return;
        var at = victim.GlobalPosition;
        float dmg = victim.MaxHp * Tune.Relics.SoulShare;
        G.Fx.Ring(at, 34, new Color(0.7f, 0.95f, 1f), 0.3f, FxLayer.RingStyle.Wave);
        G.Fx.Burst(at, new Color(0.7f, 0.95f, 1f), 10, 140, 2f, 0.4f);
        foreach (var e in Near(at, Tune.Relics.SoulRadius, victim)) { e.Hurt(dmg, (e.GlobalPosition - at).Normalized() * 80f, e.GlobalPosition, DamageKind.Raw); p.Bump("soul"); }
    }

    /// <summary>Chain Lightning: a hit may arc to creatures near the one struck.</summary>
    public static void Chain(Player p, Enemy from, float dealt)
    {
        if (!G.Chance(Tune.Relics.ChainChance)) return;
        var last = from.GlobalPosition;
        var struck = new System.Collections.Generic.HashSet<Enemy> { from };
        for (int k = 0; k < Tune.Relics.ChainJumps; k++)
        {
            var next = Near(last, Tune.Relics.ChainRadius).FirstOrDefault(e => !struck.Contains(e));
            if (next == null) break;
            struck.Add(next);
            Zap(last, next.GlobalPosition, new Color(0.75f, 0.85f, 1f));
            next.Hurt(dealt * Tune.Relics.ChainShare, Vector2.Zero, next.GlobalPosition, DamageKind.Raw);
            p.Bump("chain");
            last = next.GlobalPosition;
        }
    }

    /// <summary>Spillover: a blow that kills spills part of its damage onto the nearest other creature.</summary>
    public static void Spill(Player p, Enemy dead, float dealt)
    {
        var next = Near(dead.GlobalPosition, Tune.Relics.SpillRadius, dead).FirstOrDefault();
        if (next == null) return;
        Zap(dead.GlobalPosition, next.GlobalPosition, new Color(1f, 0.6f, 0.4f));
        next.Hurt(dealt * Tune.Relics.SpillShare, (next.GlobalPosition - dead.GlobalPosition).Normalized() * 60f, next.GlobalPosition, DamageKind.Raw);
        p.Bump("spill");
    }

    /// <summary>Wither: a creature that dies burning or poisoned spreads it.</summary>
    public static void Wither(Player p, Enemy dead)
    {
        if (!dead.Ignited && !dead.Envenomed) return;
        foreach (var e in Near(dead.GlobalPosition, Tune.Relics.WitherRadius, dead))
        {
            if (dead.Ignited) e.Ignite(Tune.Elementalist.IgniteDps * p.Stats.DamageMult, Tune.Elementalist.IgniteSeconds);
            if (dead.Envenomed) e.Envenom(p.Stats.MaxHp * 0.2f, 4f);
            p.Bump("wither");
        }
    }

    /// <summary>Rally: a kill shields the allies about.</summary>
    public static void Rally(Player p)
    {
        foreach (var h in G.Players.ToArray())
        {
            if (h == p || !GodotObject.IsInstanceValid(h) || h.Dead || h.GlobalPosition.DistanceTo(p.GlobalPosition) > Tune.Relics.RallyRadius) continue;
            h.GiveBarrier(h.Stats.MaxHp * Tune.Relics.RallyShare, Tune.Relics.RallySeconds);
            p.Bump("rally");
        }
    }

    /// <summary>Level Surge: a level gained is a blast about the hero, and a little mending.</summary>
    public static void LevelSurge(Player p)
    {
        float dmg = Tune.Relics.SurgeBase + Tune.Relics.SurgePerLevel * p.Level;
        G.Fx.Ring(p.GlobalPosition, 60, new Color(1f, 0.9f, 0.5f), 0.4f, FxLayer.RingStyle.Wave);
        G.Fx.Shockwave(p.GlobalPosition, 60, new Color(1f, 0.9f, 0.5f, 0.4f), 0.4f);
        foreach (var e in Near(p.GlobalPosition, Tune.Relics.SurgeRadius)) e.Hurt(dmg, (e.GlobalPosition - p.GlobalPosition).Normalized() * 160f, e.GlobalPosition, DamageKind.Raw);
        p.Heal(p.Stats.MaxHp * Tune.Relics.SurgeHeal);
        p.Bump("surge");
    }

    /// <summary>Bounty Hunter: a kill may drop a few coins.</summary>
    public static void Bounty(Player p, Enemy dead)
    {
        if (dead == null || !G.Chance(Tune.Relics.BountyChance)) return;
        G.Spawn(new CoinPickup { Position = dead.GlobalPosition + new Vector2(0, -4), Count = G.Rng.Next(2, 4), Puppet = false });
        p.Bump("bounty");
    }

    /// <summary>Overflow: a hit made at full health puts up a barrier (not more than once in a few seconds).</summary>
    public static void Overflow(Player p, ref double last)
    {
        if (p.Hp < p.Stats.MaxHp * 0.99f || Now < last + Tune.Relics.OverflowEvery) return;
        last = Now;
        p.GiveBarrier(p.Stats.MaxHp * Tune.Relics.OverflowShare, Tune.Relics.OverflowSeconds);
        p.Bump("overflow");
    }
}
