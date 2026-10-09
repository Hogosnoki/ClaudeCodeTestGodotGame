using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's bolt: a knot of fire (or, with Frostbolt, of frost) flying out from the
/// staff until it meets a creature, meets rock, or runs out of range. It homes: it picks the
/// creature nearest its heading (one it can see, within a wide cone ahead), leads a moving one a
/// little and bends after it, so a rat scurrying along the floor or a bat fluttering about still
/// takes it. It never turns back on itself: a creature behind it is left alone. The caster's game
/// deals the damage and rolls what it does to the creature (setting it alight, chilling it,
/// freezing it solid); the other games show a harmless copy (which homes on what it sees, too).
/// </summary>
public partial class ElementBolt : Node2D
{
    public Vector2 Dir = Vector2.Right;
    public float Damage, Range = Tune.Elementalist.BoltRange, Speed = Tune.Elementalist.BoltSpeed;
    /// <summary>Frost instead of fire.</summary>
    public bool Frost;
    /// <summary>The Aegis's ward bolt (light, bursting on contact, weakening what it strikes).</summary>
    public bool Ward;
    /// <summary>Online: another player's bolt, shown here (their game deals its damage).</summary>
    public bool Harmless;
    public Player Caster;
    /// <summary>How fast it can turn toward its mark (degrees a second); 0 flies straight.</summary>
    public float TurnRate = Tune.Elementalist.BoltTurnDegrees;
    private float _traveled, _t, _trailT;
    private Enemy _mark;

    public float Traveled => _traveled;
    public float Age => _t;
    public static Color FireColor => new(1f, 0.55f, 0.16f);
    public static Color FrostColor => new(0.62f, 0.88f, 1f);
    public static Color WardColor => new(1f, 0.9f, 0.5f);
    public Color Tint => Ward ? WardColor : Frost ? FrostColor : FireColor;

    public override void _Ready() => ZIndex = 2;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Steer(dt);
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
        if (!Harmless)
        {
            Breakables.Shoot(from, to);
            if (Breakables.Spell(from, to)) { Fizzle(to, false); return; }
        }
        _traveled += step.Length();
        if (G.Cave.IsSolid(to)) { Fizzle(from, true); return; }
        if (_traveled >= Range) { Fizzle(to, false); return; }
        GlobalPosition = to;
        if (!Frost && !Ward && GasCloud.All.Count > 0) GasCloud.IgniteAt(to, 5f);
        // a trail of sparks (or frost) behind it
        _trailT -= dt;
        if (_trailT <= 0)
        {
            _trailT = 0.03f;
            if (Ward) { _trailT = 0.11f; if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition - Dir * 5f, new Color(1f, 0.95f, 0.75f, 0.5f), 1, 12, 1.1f, 0.25f, -30); }
            else if (Frost) G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.95f, 1f, 0.8f), 1, 20, 1.4f, 0.3f, 40);
            else G.Fx.Ember(GlobalPosition - Dir * 4f, G.Chance(0.5f) ? FireColor : new Color(1f, 0.85f, 0.4f));
        }
        QueueRedraw();
    }

    /// <summary>Bends its flight toward the creature it has marked (choosing one if it has none).</summary>
    private void Steer(float dt)
    {
        if (TurnRate <= 0f || Speed < 1f) return;
        if (_mark != null && (!IsInstanceValid(_mark) || _mark.Dead || !_mark.CanBeHit || !G.Cave.LineClear(GlobalPosition, _mark.GlobalPosition))) _mark = null;
        _mark ??= Acquire();
        if (_mark == null) return;
        var to = _mark.GlobalPosition - GlobalPosition;
        float d = to.Length();
        // lead a creature that's moving (a little short of the full lead: they swerve)
        var aimAt = to;
        if (_mark.Velocity.LengthSquared() > 1f) aimAt += _mark.Velocity * Math.Min(d / Speed, 0.5f) * 0.75f;
        float want = Dir.AngleTo(aimAt);
        // (tighter as it closes, so it can't be left circling a creature it has caught up with)
        float rate = Mathf.DegToRad(TurnRate) * (d < 70f ? 2.5f : 1f) * dt;
        Dir = Dir.Rotated(Math.Clamp(want, -rate, rate)).Normalized();
    }

    /// <summary>The creature to home on: in sight, ahead of it within the seek cone, the one nearest its heading.</summary>
    private Enemy Acquire()
    {
        Enemy best = null;
        float bestScore = float.MaxValue, cone = Mathf.DegToRad(Tune.Elementalist.BoltSeekDegrees), left = Range - _traveled;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - GlobalPosition;
            float d = to.Length();
            if (d > left + e.HitRadius) continue;
            float ang = Math.Abs(Dir.AngleTo(to));
            if (d > 12f && ang > cone + MathF.Atan2(e.HitRadius, d)) continue;
            if (!G.Cave.LineClear(GlobalPosition, e.GlobalPosition)) continue;
            float score = d * (1f + ang * 2.5f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    /// <summary>It meets a creature: the caster's game deals the blow, every game shows it.</summary>
    private void Strike(Enemy e)
    {
        var at = e.GlobalPosition - Dir * e.HitRadius * 0.6f;
        if (!Harmless && Caster != null && IsInstanceValid(Caster)) { if (Ward) Caster.WardBoltStruck(this, e, at); else Caster.BoltStruck(this, e, at); }
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
        if (Ward)
        {
            G.Fx.Ring(at, (Tune.Aegis.BurstRadius * 0.6f) * (0.5f + 0.5f * size), new Color(1f, 0.92f, 0.6f, 0.85f), 0.25f);
            // (the bubble pops: a ring, a few droplets of light and a bubble or two)
            G.Fx.Burst(at, new Color(1f, 0.95f, 0.75f), (int)(6 * size) + 2, 90, 1.4f, 0.25f, -20);
            G.Fx.Bubbles(at, 2);
            G.Sfx.Play("clink", at, -10, 0.1f, 1.7f);
        }
        else if (Frost)
        {
            G.Fx.Burst(at, new Color(0.85f, 0.96f, 1f), (int)(8 * size) + 2, 110, 1.8f, 0.3f, 120);
            G.Fx.Ring(at, 6 + 6 * size, new Color(0.75f, 0.92f, 1f, 0.8f), 0.2f);
            G.Sfx.Play("clink", at, -12, 0.1f, 1.6f);
        }
        else
        {
            GasCloud.IgniteAt(at, 12f + 8f * size);
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
