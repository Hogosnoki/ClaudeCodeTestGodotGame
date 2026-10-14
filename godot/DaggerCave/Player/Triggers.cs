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
    /// <summary>The hero took a form (a Shape Shifter's Shift or Recall).</summary>
    public event Action<Player> Shift;
    /// <summary>The Shape Shifter returns to its own shape.</summary>
    public event Action<Player> Unshift;
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
    public void RaiseShift(Player p) { Shift?.Invoke(p); }
    public void RaiseUnshift(Player p) { Unshift?.Invoke(p); }
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
    /// <summary>A kill's gift to the hero's own reserve: vital force, alimus, fuel, shield; a little health for the rest.</summary>
    public void GainReserve(float amount)
    {
        switch (Stats.Hero)
        {
            case HeroKind.Vitalist: GainVitalForce(amount); break;
            case HeroKind.Elementalist: GainAlimus(amount); break;
            case HeroKind.Automaton: GainFuel(amount); break;
            case HeroKind.Warden: MendShield(amount * 2f); break;
            default: Heal(amount * 0.4f); break;
        }
    }

    /// <summary>Test aid: ready to be struck again at once.</summary>
    // ---- the little clocks the generic upgrades and the newer relics read
    private float _stillT, _sinceHurt, _adrT, _runT;
    private int _wardDepth = -1;
    /// <summary>Seconds the hero has stood still (steady hand).</summary>
    public float StillTime => _stillT;
    /// <summary>The next blow dealt (before this moment, on the clock) is stronger by this much (a riposte, a retribution); both reset when it lands.</summary>
    public double NextHitUntil;
    public float NextHitMult = 1f;
    /// <summary>Arms the next blow: x for seconds.</summary>
    public void ArmNextHit(float mult, float seconds) { NextHitMult = mult; NextHitUntil = Time.GetTicksMsec() / 1000.0 + seconds; }
    /// <summary>The keys this hero can carry.</summary>
    public int MaxKeys => Tune.Vault.MaxKeys + (int)Stats.KeyCapBonus;

    /// <summary>Each frame: the clocks, and out-of-combat mending (Recuperation).</summary>
    private void TickUpgrades(float dt)
    {
        _stillT = Velocity.LengthSquared() < 25f && IsOnFloor() ? _stillT + dt : 0f;
        _sinceHurt += dt;
        if (_adrT > 0f) _adrT -= dt;
        bool running = Math.Abs(Velocity.X) > 60f && IsOnFloor();
        _runT = running ? Math.Min(1f, _runT + dt) : Math.Max(0f, _runT - dt * 3f);
        if (Stats.RegenShare > 0f && _sinceHurt > 4f && Hp < Stats.MaxHp && !Dead)
            Hp = Math.Min(Stats.MaxHp, Hp + Stats.MaxHp * Stats.RegenShare * dt);
    }

    /// <summary>Test aid: ready to be struck again at once.</summary>
    public void ClearInvuln() { _invuln = 0f; _iframes = 0f; }
}
