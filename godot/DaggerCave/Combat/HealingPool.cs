using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Vitalist's Healing Pool (Lifebloom's upgrade): where a bloom burst, a pool of pink light
/// lingers for a few seconds, healing whoever stands in it. Every game keeps a copy, and each
/// copy heals only that game's own hero, so nobody is healed twice.
/// </summary>
public partial class HealingPool : Node2D
{
    public float Radius = 80f, Rate = 3f, Life = 5f;
    private float _t, _fxT, _ringT;

    public override void _Ready() => ZIndex = -1;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Life -= dt;
        if (Life <= 0) { QueueFree(); return; }
        var p = G.Player;
        if (p != null && IsInstanceValid(p) && !p.Dead && !p.IsRemote && p.GlobalPosition.DistanceTo(GlobalPosition) < Radius)
            p.Heal(Rate * dt);
        // motes rising from it, and a slow pulse at its edge
        _fxT -= dt;
        if (_fxT <= 0)
        {
            _fxT = 0.08f;
            var at = GlobalPosition + new Vector2(G.Range(-1f, 1f) * Radius * 0.9f, G.Range(-6f, 8f));
            G.Fx.Ember(at, G.Chance(0.5f) ? Player.HealColor : Player.HealColorLight);
        }
        _ringT -= dt;
        if (_ringT <= 0)
        {
            _ringT = 1f;
            G.Fx.Ring(GlobalPosition, Radius * 0.9f, new Color(Player.HealColor, 0.5f * Math.Min(1f, Life)), 0.8f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = Math.Min(1f, Life) * (0.18f + 0.06f * MathF.Sin(_t * 4f));
        DrawCircle(Vector2.Zero, Radius, new Color(Player.HealColor, a));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 48, new Color(Player.HealColorLight, a * 2.5f), 1.5f);
    }
}
