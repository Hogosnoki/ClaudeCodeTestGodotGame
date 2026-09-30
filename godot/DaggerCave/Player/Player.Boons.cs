using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// What one hero can give another (or itself): the Warden's barrier (Guardian's Charge), which
/// soaks damage for a moment, and the Vitalist's mending (Slow Mending), which heals over a few
/// seconds and, with Warding Mending, softens every blow meanwhile. Online, a boon for another
/// player's hero is sent to their game, which holds it; the others see it by the hero's flags.
/// </summary>
public partial class Player
{
    /// <summary>Damage the barrier still soaks (0 = none).</summary>
    public float BarrierHp { get; private set; }
    private float _barrierT, _mendRate, _mendLeft;
    private float _mendWard;

    /// <summary>Wrapped in a barrier (a puppet goes by its game's flags).</summary>
    public bool Barriered => IsRemote ? (_netFlags & HfBarrier) != 0 : BarrierHp > 0.01f;
    /// <summary>A heal is still mending this hero (Slow Mending).</summary>
    public bool Mended => IsRemote ? (_netFlags & HfMending) != 0 : _mendLeft > 0;

    /// <summary>A barrier that soaks <paramref name="amount"/> damage for <paramref name="seconds"/> (the stronger of two stays).</summary>
    public void GiveBarrier(float amount, float seconds)
    {
        if (Dead) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Barrier, amount, seconds); return; }
        BarrierHp = Math.Max(BarrierHp, amount);
        _barrierT = Math.Max(_barrierT, seconds);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 20, new Color(0.75f, 0.9f, 1f), 0.4f);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -4), 16, new Color(0.6f, 0.85f, 1f), 0.15f);
        G.Sfx.Play("clink", GlobalPosition, -6, 0.05f, 1.5f);
    }

    /// <summary>A heal given over <paramref name="seconds"/> (and, warded, 20% less damage taken meanwhile).</summary>
    public void GiveMending(float amount, float seconds, float wardShare)
    {
        if (Dead || amount <= 0) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Mending, amount, seconds, wardShare); return; }
        // a new mending tops up whatever is still to come of the last one
        float left = _mendLeft > 0 ? _mendRate * _mendLeft : 0;
        _mendLeft = seconds;
        _mendRate = (left + amount) / seconds;
        _mendWard = Math.Max(_mendWard, wardShare);
    }

    /// <summary>The boons wearing off, and the mending doing its work.</summary>
    private void TickBoons(float dt)
    {
        if (_barrierT > 0 && (_barrierT -= dt) <= 0) BarrierHp = 0;
        if (_mendLeft > 0)
        {
            float step = Math.Min(dt, _mendLeft);
            _mendLeft -= dt;
            Heal(_mendRate * step);
            if (G.Chance(0.12f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), G.Range(-12, 10)), HealColor);
            if (_mendLeft <= 0) _mendWard = 0f;
        }
    }

    /// <summary>
    /// What's left of a blow once the barrier has soaked what it can, and Warding Mending has
    /// softened it. A blow the barrier soaks whole spends it with a flash of light.
    /// </summary>
    private float Soften(float dmg)
    {
        if (_mendLeft > 0 && _mendWard > 0f) dmg *= 1f - _mendWard;
        if (BarrierHp <= 0 || dmg <= 0) return dmg;
        float soaked = Math.Min(BarrierHp, dmg);
        BarrierHp -= soaked;
        G.Fx.Spark(GlobalPosition + new Vector2(0, -4), new Vector2(Facing, 0), false, new Color(0.75f, 0.9f, 1f));
        G.Fx.Text(GlobalPosition + new Vector2(0, -26), $"{Mathf.RoundToInt(soaked)} SOAKED", new Color(0.75f, 0.9f, 1f), 9, 0.6f);
        if (BarrierHp <= 0.01f)
        {
            BarrierHp = 0;
            _barrierT = 0;
            G.Fx.Burst(GlobalPosition + new Vector2(0, -4), new Color(0.7f, 0.88f, 1f), 12, 150, 2f, 0.4f);
            G.Sfx.Play("rock", GlobalPosition, -8, 0.1f, 1.8f);
        }
        return dmg - soaked;
    }
}
