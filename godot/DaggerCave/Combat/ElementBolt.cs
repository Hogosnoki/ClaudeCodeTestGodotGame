using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's bolt: a knot of fire (or, with Frostbolt, of frost) flying straight out
/// from the staff until it meets a creature, meets rock, or runs out of range. The caster's game
/// deals the damage and rolls what it does to the creature (setting it alight, chilling it,
/// freezing it solid); the other games show a harmless copy.
/// </summary>
public partial class ElementBolt : Node2D
{
    public Vector2 Dir = Vector2.Right;
    public float Damage, Range = Tune.Elementalist.BoltRange, Speed = Tune.Elementalist.BoltSpeed;
    /// <summary>Frost instead of fire.</summary>
    public bool Frost;
    /// <summary>Online: another player's bolt, shown here (their game deals its damage).</summary>
    public bool Harmless;
    public Player Caster;
    private float _traveled, _t, _trailT;

    public float Traveled => _traveled;
    public float Age => _t;
    public static Color FireColor => new(1f, 0.55f, 0.16f);
    public static Color FrostColor => new(0.62f, 0.88f, 1f);
    public Color Tint => Frost ? FrostColor : FireColor;

    public override void _Ready() => ZIndex = 2;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        var from = GlobalPosition;
        var step = Dir * Speed * dt;
        var to = from + step;
        // the first creature along this stretch of its flight
        Enemy hit = null;
        float best = float.MaxValue;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var c = Geometry2D.GetClosestPointToSegment(e.GlobalPosition, from, to);
            if (c.DistanceTo(e.GlobalPosition) > e.HitRadius + 4f) continue;
            float d = c.DistanceSquaredTo(from);
            if (d < best) { best = d; hit = e; }
        }
        if (hit != null) { Strike(hit); return; }
        _traveled += step.Length();
        if (G.Cave.IsSolid(to)) { Fizzle(from, true); return; }
        if (_traveled >= Range) { Fizzle(to, false); return; }
        GlobalPosition = to;
        // a trail of sparks (or frost) behind it
        _trailT -= dt;
        if (_trailT <= 0)
        {
            _trailT = 0.03f;
            if (Frost) G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.95f, 1f, 0.8f), 1, 20, 1.4f, 0.3f, 40);
            else G.Fx.Ember(GlobalPosition - Dir * 4f, G.Chance(0.5f) ? FireColor : new Color(1f, 0.85f, 0.4f));
        }
        QueueRedraw();
    }

    /// <summary>It meets a creature: the caster's game deals the blow, every game shows it.</summary>
    private void Strike(Enemy e)
    {
        var at = e.GlobalPosition - Dir * e.HitRadius * 0.6f;
        if (!Harmless && Caster != null && IsInstanceValid(Caster)) Caster.BoltStruck(this, e, at);
        Burst(at, 1f);
        QueueFree();
    }

    /// <summary>It runs out, or splashes on rock.</summary>
    private void Fizzle(Vector2 at, bool wall)
    {
        Burst(at, wall ? 0.7f : 0.4f);
        QueueFree();
    }

    private void Burst(Vector2 at, float size)
    {
        var col = Tint;
        G.Fx.Flash(at, 8 + 6 * size, col, 0.1f);
        if (Frost)
        {
            G.Fx.Burst(at, new Color(0.85f, 0.96f, 1f), (int)(8 * size) + 2, 110, 1.8f, 0.3f, 120);
            G.Fx.Ring(at, 6 + 6 * size, new Color(0.75f, 0.92f, 1f, 0.8f), 0.2f);
            G.Sfx.Play("clink", at, -12, 0.1f, 1.6f);
        }
        else
        {
            G.Fx.Burst(at, col, (int)(9 * size) + 2, 120, 2f, 0.3f, -40);
            for (int k = 0; k < 3; k++) G.Fx.Ember(at + G.RandDir() * 3f, new Color(1f, 0.8f, 0.35f));
            G.Sfx.Play("lava", at, -12, 0.1f, 1.8f);
        }
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay: a glowing head and a short tail)
        var col = Tint;
        DrawCircle(Vector2.Zero, 3.5f, col);
        DrawLine(Vector2.Zero, -Dir * 12f, new Color(col, 0.5f), 2.5f);
    }
}
