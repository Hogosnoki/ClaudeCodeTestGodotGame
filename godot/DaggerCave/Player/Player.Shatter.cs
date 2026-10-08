using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Frozen creatures break. A snap does it from afar; a heaving swing (the Swordsman's) or a shield bash (the Warden's) does it up close, to
/// every frozen creature it strikes. Whatever bursts takes its damage and splashes a little on whatever stands near it.
/// </summary>
public partial class Player
{
    /// <summary>A creature bursts: the ice (or, <paramref name="cinder"/>, the fire) goes in a burst of shards (cinders), it takes <paramref name="dmg"/>, and everything within <paramref name="radius"/> takes <paramref name="splash"/>.</summary>
    private void BurstCreature(Enemy e, float dmg, float splash, float radius, bool cinder, bool igniteSplash)
    {
        var at = e.GlobalPosition;
        var col = cinder ? ElementBolt.FireColor : ElementBolt.FrostColor;
        if (cinder) e.Quench(); else e.Thaw();
        float dealt = e.Hurt(dmg, Vector2.Up * 60f, at, cinder ? DamageKind.Fire : DamageKind.Frost);
        if (dealt > 0) OnDealtDamage(dealt);
        G.Fx.Flash(at, e.HitRadius + 14, col, 0.14f);
        G.Fx.Burst(at, cinder ? new Color(1f, 0.6f, 0.2f) : new Color(0.85f, 0.97f, 1f), 18, 190, 2.4f, 0.45f, cinder ? -60f : 260f);
        G.Fx.Ring(at, radius, new Color(col, 0.8f), 0.3f);
        G.Sfx.Play(cinder ? "lava" : "rock", at, -4, 0.1f, cinder ? 1.2f : 2f);
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == e || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > radius + o.HitRadius) continue;
            if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
            float d2 = o.Hurt(splash, (o.GlobalPosition - at).Normalized() * 80f, o.GlobalPosition, cinder ? DamageKind.Fire : DamageKind.Frost);
            if (d2 > 0) OnDealtDamage(d2);
            // Cinder Snap: what the burst splashes on is set alight
            if (igniteSplash && !o.Dead) o.Ignite(Tune.Elementalist.IgniteDps, Tune.Elementalist.IgniteSeconds);
        }
    }

    /// <summary>A heavy blow met a frozen creature: it shatters (the same burst as a snap's, from whatever this hero deals).</summary>
    public int ShatteredByBlows { get; private set; }

    private void ShatterFrozen(Enemy e)
    {
        ShatteredByBlows++;
        BurstCreature(e, Tune.Elementalist.SnapDamage * Stats.DamageMult, Tune.Elementalist.SnapSplash * Stats.DamageMult, Tune.Elementalist.SnapRadius, false, false);
    }
}
