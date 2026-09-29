using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's Blizzard: a small storm swirling where it was cast. It strikes everything
/// inside it a number of times over its life, each strike with a chance of freezing a regular
/// creature solid; a Firestorm strikes harder and sets creatures alight instead. The caster's game
/// deals the strikes; the other games show a harmless copy.
/// </summary>
public partial class Blizzard : Node2D
{
    public float Radius = Tune.Elementalist.BlizzardRadius, Seconds = Tune.Elementalist.BlizzardSeconds, Damage = Tune.Elementalist.BlizzardDamage;
    public int Ticks = Tune.Elementalist.BlizzardTicks;
    public float FreezeChance = Tune.Elementalist.BlizzardFreeze, IgniteChance, IgniteDps, IgniteSeconds;
    /// <summary>A Firestorm (the alteration): fire instead of frost.</summary>
    public bool Fire;
    /// <summary>Online: another player's storm, shown here (their game deals its strikes).</summary>
    public bool Harmless;
    public Player Caster;
    private float _t, _fxT;
    private int _struck;

    public float Age => _t;
    /// <summary>0..1: how strong it is now (it gathers, then dies away).</summary>
    public float Strength => Math.Clamp(_t / 0.3f, 0f, 1f) * Math.Clamp((Seconds - _t) / 0.4f, 0f, 1f);
    /// <summary>Strikes dealt so far (for the tests).</summary>
    public int Struck => _struck;

    public override void _Ready() => ZIndex = 1;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        // the strikes, evenly through its life (the first a moment in)
        float every = Seconds / Math.Max(1, Ticks);
        while (_struck < Ticks && _t >= every * (_struck + 0.5f))
        {
            _struck++;
            if (!Harmless) StrikeAll();
        }
        if (_t >= Seconds) { QueueFree(); return; }
        Flurry(dt);
        QueueRedraw();
    }

    /// <summary>One strike on everything inside it (that the storm can reach: not through rock).</summary>
    private void StrikeAll()
    {
        var c = GlobalPosition;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            if (e.GlobalPosition.DistanceTo(c) > Radius + e.HitRadius) continue;
            if (!G.Cave.LineClear(c, e.GlobalPosition)) continue;
            if (Caster != null && IsInstanceValid(Caster)) Caster.StormStruck(this, e);
        }
    }

    /// <summary>Snow (or sparks) whirling through it, and a frost ring (or glow) on the ground.</summary>
    private void Flurry(float dt)
    {
        _fxT -= dt;
        if (_fxT > 0) return;
        _fxT = 0.025f;
        float s = Strength;
        var at = GlobalPosition + G.RandDir() * G.Range(0f, Radius);
        if (Fire)
        {
            G.Fx.Ember(at, G.Chance(0.5f) ? new Color(1f, 0.5f, 0.12f) : new Color(1f, 0.8f, 0.35f));
            if (G.Chance(0.25f)) G.Fx.Burst(at, new Color(1f, 0.45f, 0.1f, 0.8f * s), 1, 40, 2.4f, 0.35f, -80);
        }
        else
        {
            // flakes blown slantwise down through it
            var from = at + new Vector2(-8f, -Radius * 0.9f);
            G.Fx.Directional(from, new Vector2(0.35f, 1f).Normalized(), 0.25f, new Color(0.92f, 0.97f, 1f, 0.9f * s), 1, 150, 1.5f, 0.35f, 60, 0);
        }
        if (G.Chance(0.015f)) G.Fx.Ring(GlobalPosition, Radius, Fire ? new Color(1f, 0.5f, 0.15f, 0.3f * s) : new Color(0.8f, 0.94f, 1f, 0.35f * s), 0.4f);
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay)
        float s = Strength;
        var col = Fire ? new Color(1f, 0.5f, 0.15f) : new Color(0.8f, 0.94f, 1f);
        DrawCircle(Vector2.Zero, Radius, new Color(col, 0.16f * s));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 32, new Color(col, 0.6f * s), 1.2f);
    }
}
