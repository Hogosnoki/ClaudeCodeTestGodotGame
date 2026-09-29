using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// One of the Rogue's two daggers, out of the hand: flying, stuck in a creature (it rides along
/// in it until recalled, or until the creature dies and it drops out), or flying back to the
/// hand. A throw that meets nothing (or meets rock) comes back by itself. With Ricochet it springs
/// on from the creature it strikes to one more, then comes back, never sticking. The thrower's game
/// deals its blows; the other games fly a harmless copy.
/// </summary>
public partial class ThrownDagger : Node2D
{
    public enum Phase { Flying, Stuck, Returning }

    /// <summary>The Rogue who threw it (online, on the other games, their copy of that hero).</summary>
    public Player Thrower;
    /// <summary>Which of the two daggers (0 or 1).</summary>
    public int Index;
    public Vector2 Dir = Vector2.Right;
    public float Damage, Speed = Tune.Rogue.ThrowSpeed, Range = Tune.Rogue.ThrowRange;
    public bool Ricochet;
    /// <summary>Online: another player's dagger, shown here (their game deals its blows).</summary>
    public bool Harmless;
    public Phase State = Phase.Flying;
    public Enemy StuckIn { get; private set; }
    private Vector2 _stuckOffset;
    private float _traveled, _t, _returnT;
    private int _bounces;
    private readonly HashSet<Enemy> _struck = new();

    public float Age => _t;
    /// <summary>Which way it points (for the 3D blade): along its flight, or where it went in.</summary>
    public Vector2 Pointing { get; private set; } = Vector2.Right;
    /// <summary>Creatures it has struck (for the tests).</summary>
    public int Hits => _struck.Count;

    public override void _Ready()
    {
        ZIndex = 2;
        Pointing = Dir;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        switch (State)
        {
            case Phase.Flying: Fly(dt); break;
            case Phase.Stuck:
                // it rides in the creature; if the creature dies it drops out and comes home
                if (StuckIn == null || !IsInstanceValid(StuckIn) || StuckIn.Dead) { ComeBack(); break; }
                GlobalPosition = StuckIn.GlobalPosition + _stuckOffset;
                break;
            case Phase.Returning: FlyHome(dt); break;
        }
        QueueRedraw();
    }

    private void Fly(float dt)
    {
        var from = GlobalPosition;
        var step = Dir * Speed * dt;
        var to = from + step;
        Enemy hit = null;
        float best = float.MaxValue;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit || _struck.Contains(e)) continue;
            var c = Geometry2D.GetClosestPointToSegment(e.GlobalPosition, from, to);
            if (c.DistanceTo(e.GlobalPosition) > e.HitRadius + 3f) continue;
            float d = c.DistanceSquaredTo(from);
            if (d < best) { best = d; hit = e; }
        }
        if (hit != null) { Strike(hit); return; }
        _traveled += step.Length();
        if (G.Cave.IsSolid(to))
        {
            G.Fx.Spark(from, -Dir, false, new Color(1f, 0.9f, 0.7f));
            G.Sfx.Play("clink", from, -8, 0.1f, 1.6f);
            ComeBack();
            return;
        }
        if (_traveled >= Range) { ComeBack(); return; }
        GlobalPosition = to;
        Pointing = Dir;
    }

    /// <summary>It meets a creature: the blow (in the thrower's game), then it sticks, or springs on.</summary>
    private void Strike(Enemy e)
    {
        _struck.Add(e);
        var at = e.GlobalPosition - Dir * e.HitRadius * 0.5f;
        if (!Harmless && Thrower != null && IsInstanceValid(Thrower)) Thrower.DaggerStruck(this, e, at);
        G.Fx.Spark(at, Dir, false, new Color(1f, 0.95f, 0.8f));
        if (Ricochet)
        {
            // it springs on to the nearest other creature in reach, then comes back
            if (_bounces == 0 && NextFor(e) is Enemy next)
            {
                _bounces++;
                GlobalPosition = at;
                Dir = (next.GlobalPosition - at).Normalized();
                _traveled = 0;
                Range = Tune.Rogue.RicochetRange + 20f;
                G.Sfx.Play("clink", at, -6, 0.1f, 1.8f);
                return;
            }
            ComeBack();
            return;
        }
        // it goes in, and stays in
        State = Phase.Stuck;
        StuckIn = e;
        _stuckOffset = at - e.GlobalPosition;
        GlobalPosition = at;
        Pointing = Dir;
    }

    private Enemy NextFor(Enemy from)
    {
        Enemy best = null;
        float bd = Tune.Rogue.RicochetRange;
        foreach (var e in G.Enemies)
        {
            if (e == from || e.Dead || !e.CanBeHit || _struck.Contains(e)) continue;
            float d = e.GlobalPosition.DistanceTo(from.GlobalPosition);
            if (d < bd && G.Cave.LineClear(from.GlobalPosition, e.GlobalPosition)) { bd = d; best = e; }
        }
        return best;
    }

    /// <summary>Home to the hand (a recall, a miss, or both daggers out).</summary>
    public void ComeBack()
    {
        if (State == Phase.Returning) return;
        State = Phase.Returning;
        StuckIn = null;
        _returnT = 0;
    }

    private void FlyHome(float dt)
    {
        if (Thrower == null || !IsInstanceValid(Thrower)) { QueueFree(); return; }
        _returnT += dt;
        var target = Thrower.GlobalPosition + new Vector2(0, -4);
        var to = target - GlobalPosition;
        float speed = Tune.Rogue.ReturnSpeed * Math.Min(1f, 0.35f + _returnT * 3f);
        if (to.Length() <= speed * dt + 8f)
        {
            if (!Harmless) Thrower.CatchDagger(this);
            QueueFree();
            return;
        }
        var dir = to.Normalized();
        GlobalPosition += dir * speed * dt;
        // (it comes back hilt-first, spinning)
        Pointing = Pointing.Rotated(dt * 22f);
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay)
        DrawLine(-Pointing * 5f, Pointing * 5f, new Color(0.85f, 0.88f, 0.92f), 2f);
    }
}
