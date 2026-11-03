using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The puddles a Shape Shifter's form leaves (lava, acid): they burn the creatures that stand in them, as blows of her form.</summary>
public static class Puddles
{
    /// <summary>Every creature standing in the puddle at <paramref name="at"/> (half <paramref name="halfW"/> wide) takes <paramref name="dmg"/>; true if any did.</summary>
    public static bool BurnFoes(Player owner, Vector2 at, float halfW, float dmg)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner)) return false;
        bool any = false;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead || !e.CanBeHit) continue;
            var d = e.GlobalPosition - at;
            if (Math.Abs(d.X) > halfW + e.HitRadius || d.Y < -22 - e.HitRadius || d.Y > 8) continue;
            owner.FormBlow(e, dmg, new Vector2(0, -40f));
            any = true;
        }
        return any;
    }
}
