using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The events a relic can listen for: a hero's hit that landed, a kill, a blow taken, the dodge button, a fall, a heal, a level, a chest, and a beat of every frame. A relic is a short
/// subscription made when it is taken (see Relics.cs); the game raises the events from the few places they happen (Enemy.Hurt, Player.OnKill, HurtHere, DodgeButton, Die, HealHere,
/// LevelUpOnce, Simulate). One set per hero, on its stats.
/// </summary>
public sealed class Triggers
{
    /// <summary>The hero's own hit landed on a creature: who, what, how much, what kind. (Not raised for a relic's own damage, which is Raw, nor from inside another handler.)</summary>
    public event Action<Player, Enemy, float, DamageKind> Hit;
    /// <summary>A creature fell to this hero.</summary>
    public event Action<Player, Enemy> Kill;
    /// <summary>A creature's blow got through to the hero: who struck (may be null for the world), how much.</summary>
    public event Action<Player, Enemy, float> Hurt;
    /// <summary>The dodge button did something.</summary>
    public event Action<Player> Dodge;
    /// <summary>The hero fell.</summary>
    public event Action<Player> Fall;
    /// <summary>The hero gained a level.</summary>
    public event Action<Player> Level;
    /// <summary>The hero opened a chest.</summary>
    public event Action<Player> Chest;
    /// <summary>The hero was healed by this much (after the multipliers).</summary>
    public event Action<Player, float> Heal;
    /// <summary>Every physics frame the hero is alive.</summary>
    public event Action<Player, float> Tick;

    private bool _busy;

    public void RaiseHit(Player p, Enemy e, float dealt, DamageKind k) { if (Hit == null || _busy) return; _busy = true; try { Hit(p, e, dealt, k); } finally { _busy = false; } }
    public void RaiseKill(Player p, Enemy e) { if (Kill == null || _busy) return; _busy = true; try { Kill(p, e); } finally { _busy = false; } }
    public void RaiseHurt(Player p, Enemy e, float dmg) { Hurt?.Invoke(p, e, dmg); }
    public void RaiseDodge(Player p) { Dodge?.Invoke(p); }
    public void RaiseFall(Player p) { Fall?.Invoke(p); }
    public void RaiseLevel(Player p) { Level?.Invoke(p); }
    public void RaiseChest(Player p) { Chest?.Invoke(p); }
    public void RaiseHeal(Player p, float amount) { Heal?.Invoke(p, amount); }
    public void RaiseTick(Player p, float dt) { Tick?.Invoke(p, dt); }
}

public partial class Player
{
    /// <summary>What a relic has done, by name (the relics bump it; the tests read it).</summary>
    private readonly Dictionary<string, int> _tally = new();
    public void Bump(string what) => _tally[what] = _tally.GetValueOrDefault(what) + 1;
    public int Tally(string what) => _tally.GetValueOrDefault(what);
    /// <summary>Test aid: ready to be struck again at once.</summary>
    public void ClearInvuln() { _invuln = 0f; _iframes = 0f; }
}
