using System;
using Godot;

namespace DaggerCave;

/// <summary>The colours of the afflictions: the ink outline takes them (see creature_ink.gdshader).</summary>
public static class StatusColors
{
    public static readonly Color Poison = new(0.35f, 1f, 0.3f), Fire = new(1f, 0.55f, 0.12f), Frost = new(0.65f, 0.9f, 1f), Drown = new(0.25f, 0.5f, 1f);
}

/// <summary>
/// What afflicts a hero: poison (deep frogs and spiders, the Nature Elemental) and burning (the Fire Elemental) hurt a
/// little all the while; frost (the Frost Elemental) freezes the hero solid for a moment; drowning (the Water Elemental) takes
/// some breath and keeps the air bubbles from giving any for a while. Each lights the hero's ink outline in its colour.
/// Applied by the hero's own game (the blow was taken there); the others see it by the hero's flags.
/// </summary>
public partial class Player
{
    private float _poisonLeft, _poisonRate, _burnLeft, _burnRate, _frozenT, _frozenImmune, _drownT, _dotText, _statusFx;

    public bool Poisoned => IsRemote ? (_netFlags & HfPoison) != 0 : _poisonLeft > 0;
    public bool Burning => IsRemote ? (_netFlags & HfBurn) != 0 : _burnLeft > 0;
    public bool Frozen => IsRemote ? (_netFlags & HfFrozen) != 0 : _frozenT > 0;
    public bool Drowning => IsRemote ? (_netFlags & HfDrown) != 0 : _drownT > 0;
    /// <summary>Seconds of poison left (for the HUD).</summary>
    public float PoisonLeft => _poisonLeft;
    public float BurnLeft => _burnLeft;
    public float FrozenLeft => _frozenT;
    public float DrownLeft => _drownT;

    /// <summary>The ink outline's colour while afflicted (alpha 0 = its usual dark line).</summary>
    public Color StatusInk => Dead ? new Color(0, 0, 0, 0) : Frozen ? Opaque(StatusColors.Frost) : Burning ? Opaque(StatusColors.Fire) : Poisoned ? Opaque(StatusColors.Poison) : Drowning ? Opaque(StatusColors.Drown) : Form != null ? new Color(0.9f, 0.92f, 0.97f, 1f) : new Color(0, 0, 0, 0);
    /// <summary>A faint wash of the same colour over the body.</summary>
    public Color StatusAura => Dead ? new Color(0, 0, 0, 0) : Frozen ? new Color(StatusColors.Frost, 0.8f) : Burning ? new Color(StatusColors.Fire, 0.45f) : Poisoned ? new Color(StatusColors.Poison, 0.3f) : new Color(0, 0, 0, 0);
    private static Color Opaque(Color c) => new(c, 1f);

    /// <summary>A poison: <paramref name="total"/> damage over <paramref name="seconds"/> (another tops up what's left).</summary>
    public void GivePoison(float total, float seconds)
    {
        if (Dead || IsRemote || total <= 0) return;
        float left = _poisonLeft > 0 ? _poisonRate * _poisonLeft : 0;
        _poisonLeft = Math.Max(_poisonLeft, seconds);
        _poisonRate = (left + total) / _poisonLeft;
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "POISONED", StatusColors.Poison, 10, 0.9f);
        G.Sfx.Play("bubble", GlobalPosition, -8, 0.05f, 0.6f);
    }

    /// <summary>Burning: <paramref name="total"/> damage over <paramref name="seconds"/>; water puts it out.</summary>
    public void GiveBurn(float total, float seconds)
    {
        if (Dead || IsRemote || total <= 0 || InWater) return;
        float left = _burnLeft > 0 ? _burnRate * _burnLeft : 0;
        _burnLeft = Math.Max(_burnLeft, seconds);
        _burnRate = (left + total) / _burnLeft;
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "BURNING", StatusColors.Fire, 10, 0.9f);
        G.Sfx.Play("lava", GlobalPosition, -10, 0.1f, 1.4f);
    }

    /// <summary>Frozen solid: no moving or acting for <paramref name="seconds"/>, then a few seconds in which it can't happen again.</summary>
    public void GiveFrozen(float seconds)
    {
        if (Dead || IsRemote || _frozenT > 0 || _frozenImmune > 0) return;
        _frozenT = seconds;
        _frozenImmune = seconds + Tune.Status.FreezeImmune;
        _swingT = -1;
        Velocity = new Vector2(0, Velocity.Y);
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "FROZEN", StatusColors.Frost, 11, 1f);
        G.Fx.Burst(GlobalPosition, new Color(0.85f, 0.96f, 1f), 14, 120, 2f, 0.4f, 60);
        G.Sfx.Play("clink", GlobalPosition, -4, 0.05f, 1.7f);
    }

    /// <summary>Drowned: <paramref name="breath"/> seconds of breath gone, and no air from bubbles for <paramref name="seconds"/>.</summary>
    public void GiveDrown(float seconds, float breath)
    {
        if (Dead || IsRemote) return;
        _drownT = Math.Max(_drownT, seconds);
        Breath = Math.Max(0f, Breath - breath);
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "DROWNING", StatusColors.Drown, 10, 0.9f);
        G.Fx.Bubbles(GlobalPosition + new Vector2(0, -8), 6);
        G.Sfx.Play("bubble", GlobalPosition, -6, 0.05f, 0.7f);
    }

    /// <summary>Test aid: every affliction gone.</summary>
    public void TestClearStatus() => ClearStatus();

    /// <summary>Every affliction gone (death, or a friend's revive).</summary>
    private void ClearStatus()
    {
        _poisonLeft = _burnLeft = _frozenT = _drownT = 0;
        _frozenImmune = 0;
    }

    private void TickStatus(float dt)
    {
        _statusFx -= dt; _dotText -= dt;
        if (_frozenImmune > 0) _frozenImmune -= dt;
        if (_frozenT > 0 && (_frozenT -= dt) <= 0)
        {
            _frozenT = 0;
            G.Fx.Burst(GlobalPosition, new Color(0.85f, 0.96f, 1f), 12, 140, 2f, 0.4f, 60);
        }
        if (_drownT > 0) _drownT -= dt;
        if (_burnLeft > 0 && InWater) { _burnLeft = 0; G.Fx.Smoke(GlobalPosition, 4, new Color(0.7f, 0.7f, 0.75f, 0.5f), 30f); }
        float dot = 0;
        if (_poisonLeft > 0) { float s = Math.Min(dt, _poisonLeft); _poisonLeft -= dt; dot += _poisonRate * s; }
        if (_burnLeft > 0) { float s = Math.Min(dt, _burnLeft); _burnLeft -= dt; dot += _burnRate * s; }
        // damage over time lands in ticks, a quarter of a second apart, each with its own small, falling number
        if (dot > 0 && !Dead)
        {
            _dotAcc += dot;
            if (_dotBase < 0) _dotBase = Hp;
            if ((_dotClock += dt) >= 0.25f)
            {
                _dotClock = 0;
                float hp0 = Hp;
                // (through the usual armour, but not the barrier: it's not a blow)
                TakeRawDamage(_dotAcc * (1f - Stats.DamageReduction) * Stats.DamageTakenMult, "dot");
                _dotAcc = 0;
                int shown = Num.Delta(hp0, Math.Max(0f, Hp));
                if (shown > 0) G.Fx.TickText(GlobalPosition + new Vector2(G.Range(-4, 4), -22), "-" + shown, Burning ? StatusColors.Fire : StatusColors.Poison, 9);
            }
        }
        else
        {
            // (what was owed when the affliction ended is paid at once)
            if (_dotAcc > 0 && !Dead)
            {
                float hp0 = Hp;
                TakeRawDamage(_dotAcc * (1f - Stats.DamageReduction) * Stats.DamageTakenMult, "dot");
                int shown = Num.Delta(hp0, Math.Max(0f, Hp));
                if (shown > 0) G.Fx.TickText(GlobalPosition + new Vector2(G.Range(-4, 4), -22), "-" + shown, Burning ? StatusColors.Fire : StatusColors.Poison, 9);
            }
            _dotBase = -1f; _dotAcc = 0; _dotClock = 0;
        }
        if (_statusFx <= 0 && (Poisoned || Burning))
        {
            _statusFx = 0.12f;
            var at = GlobalPosition + new Vector2(G.Range(-6, 6), G.Range(-12, 6));
            if (Burning) G.Fx.Ember(at, G.Chance(0.5f) ? StatusColors.Fire : new Color(1f, 0.85f, 0.4f));
            else G.Fx.Burst(at, new Color(StatusColors.Poison, 0.8f), 1, 20, 1.4f, 0.6f, -30);
        }
    }
    private float _dotBase = -1f, _dotAcc, _dotClock;
}
