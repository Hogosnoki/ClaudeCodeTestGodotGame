using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A piston hanging from the roof of a gallery of the Clockwork Deep: it rests, then its housing shudders and steams (the warning), then the
/// head slams to the floor and holds there a moment before it rises. Whoever is under the head as it comes down is struck and stunned.
/// <c>Position</c> is the point on the roof it hangs from; <see cref="Drop"/> is how far the head falls.
/// </summary>
public partial class Piston : Node2D
{
    public float Drop = 90f, HalfW = 15f;
    private float _t, _phase;
    private int _struckCycle = -1, _lastCycle = -1;
    private static float Rest => Tune.Clockwork.PistonRest;
    private static float Warn => Tune.Clockwork.PistonWarn;
    private static float Fall => Tune.Clockwork.PistonFall;
    private static float Dwell => Tune.Clockwork.PistonDwell;
    private static float Rise => Tune.Clockwork.PistonRise;
    public static float Total => Rest + Warn + Fall + Dwell + Rise;
    private float Clock => _t + _phase;
    public int CycleIndex => (int)(Clock / Total);
    public float Cycle => Clock % Total;
    public bool Warning => Cycle >= Rest && Cycle < Rest + Warn;
    /// <summary>For the 3D view: 0 (drawn up) to 1 (down on the floor); a shade negative as it gathers itself for the blow.</summary>
    public float Extension
    {
        get
        {
            float c = Cycle;
            if (c < Rest) return 0f;
            c -= Rest;
            if (c < Warn) return -0.07f * MathF.Sin(c / Warn * Mathf.Pi) - 0.02f * MathF.Sin(c * 60f);
            c -= Warn;
            if (c < Fall) return (c / Fall) * (c / Fall);
            c -= Fall;
            if (c < Dwell) return 1f;
            c -= Dwell;
            float u = Math.Clamp(c / Rise, 0f, 1f);
            return 1f - u * u * (3f - 2f * u);
        }
    }

    public override void _Ready() { ZIndex = 2; _phase = G.Range(0, Total); }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        var p = G.Player;
        if (p == null || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 900 * 900) return;
        int cyc = CycleIndex;
        float c = Cycle;
        // the shudder and the steam
        if (Warning)
        {
            if (G.Chance(0.4f)) G.Fx.Smoke(GlobalPosition + new Vector2(G.Range(-HalfW, HalfW), 6), 1, new Color(0.8f, 0.8f, 0.82f, 0.5f), 14);
            if (_lastCycle != cyc) { _lastCycle = cyc; G.Sfx.Play("clink", GlobalPosition, -6, 0.1f, 0.5f); }
        }
        float ext = Extension;
        float headY = GlobalPosition.Y + ext * Drop;
        // the slam: it lands, and a struck hero is hurt (once a cycle)
        if (c >= Rest + Warn && c < Rest + Warn + Fall + Dwell)
        {
            if (_struckCycle != cyc && ext >= 0.97f)
            {
                _struckCycle = cyc;
                G.Sfx.Play("slam", new Vector2(GlobalPosition.X, headY), -2, 0.1f, 0.9f);
                G.Fx.Dust(new Vector2(GlobalPosition.X, headY), 3, 0.8f, new Color(0.5f, 0.48f, 0.46f, 0.7f));
                G.Fx.Debris(new Vector2(GlobalPosition.X, headY), new Color(0.5f, 0.5f, 0.55f), 4, 120);
                G.Fx.Spark(new Vector2(GlobalPosition.X - HalfW, headY), new Color(1f, 0.8f, 0.5f));
                G.Fx.Spark(new Vector2(GlobalPosition.X + HalfW, headY), new Color(1f, 0.8f, 0.5f));
                if (p.GlobalPosition.DistanceTo(GlobalPosition) < 600) G.Fx.AddShake(2.5f);
            }
            var d = p.GlobalPosition - GlobalPosition;
            // (the hero's body reaches a little above and below its centre)
            if (!p.Dead && Math.Abs(d.X) < HalfW + 6f && p.GlobalPosition.Y - 26f < headY && p.GlobalPosition.Y > GlobalPosition.Y + 4f && ext > 0.15f && _hitCycle != cyc)
            {
                _hitCycle = cyc;
                if (p.Hurt(p.Stats.MaxHp * Tune.Clockwork.PistonShare + Tune.Clockwork.PistonFlat * G.DepthDmg, new Vector2(GlobalPosition.X, headY - 30), 160) > 0 && !p.Dead)
                    p.GiveStun(Tune.Clockwork.PistonStun);
            }
        }
    }
    private int _hitCycle = -1;
}

/// <summary>
/// A belt along a stretch of level floor (the Clockwork Deep): whoever stands on it, hero or creature, is carried along it at
/// <see cref="Speed"/>. <c>Position</c> is the middle of the belt's surface.
/// </summary>
public partial class Conveyor : Node2D
{
    public static readonly List<Conveyor> All = new();
    public float Half = 56f;
    public int Dir = 1;
    public float Speed = Tune.Clockwork.BeltSpeed;

    public override void _Ready() { ZIndex = 1; All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    /// <summary>How fast a body whose feet are at <paramref name="feet"/> is carried along (px/s along x; 0 when no belt is under it).</summary>
    public static float DriftAt(Vector2 feet)
    {
        foreach (var c in All)
            if (Math.Abs(feet.X - c.GlobalPosition.X) <= c.Half && Math.Abs(feet.Y - c.GlobalPosition.Y) < 11f) return c.Dir * c.Speed;
        return 0f;
    }
}

/// <summary>A saw blade on a rail (the Clockwork Deep): it runs from <see cref="Start"/> to <see cref="End"/> and back, forever, whirring, and cuts whoever it touches.</summary>
public partial class SawBlade : Node2D
{
    public Vector2 Start, End;
    private float _t, _phase, _tick;
    public float Radius = Tune.Clockwork.SawRadius;
    /// <summary>For the 3D view: how far along the rail it is (0..1) and how far it has turned.</summary>
    public float Along => Pingpong(_t * Tune.Clockwork.SawSpeed / Math.Max(10f, Start.DistanceTo(End)) + _phase);
    public float Spin => _t * 14f;
    private static float Pingpong(float x) { x = Mathf.PosMod(x, 2f); return x <= 1f ? x : 2f - x; }

    public override void _Ready() { ZIndex = 2; _phase = G.Range(0, 2f); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        // (smoothed at the ends, as a blade slows to turn)
        float u = Along;
        GlobalPosition = Start.Lerp(End, u * u * (3f - 2f * u) * 0.35f + u * 0.65f);
        var p = G.Player;
        if (p == null || p.Dead || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 900 * 900) return;
        if (G.Chance(0.12f)) G.Fx.Spark(GlobalPosition + G.RandDir() * Radius, new Color(1f, 0.85f, 0.5f));
        if (_tick <= 0 && p.GlobalPosition.DistanceTo(GlobalPosition) < Radius + 9f)
        {
            _tick = 0.55f;
            G.Sfx.Play("clink", GlobalPosition, -2, 0.2f, 1.7f);
            G.Fx.Burst(p.GlobalPosition, new Color(0.75f, 0.1f, 0.1f), 6, 120, 2f, 0.4f);
            p.Hurt(p.Stats.MaxHp * Tune.Clockwork.SawShare + Tune.Clockwork.SawFlat * G.DepthDmg, GlobalPosition, 200);
        }
    }
}

/// <summary>
/// A lift in a shaft of the Clockwork Deep: a platform that runs up and down between the floors on a cable, resting a moment at each end.
/// It is one-way (you can jump up through it and stand on it), and carries whatever stands on it. <see cref="Top"/> and <see cref="Bottom"/>
/// are the heights of its surface at either end.
/// </summary>
public partial class Lift : AnimatableBody2D
{
    public float Top, Bottom;
    public float Half = Tune.Clockwork.LiftHalfWidth;
    private float _t, _phase;
    /// <summary>For the 3D view: where it is (0 top .. 1 bottom), and whether it is moving.</summary>
    public float Pos { get; private set; }
    public bool Moving { get; private set; }

    public override void _Ready()
    {
        ZIndex = 1;
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        SyncToPhysics = true;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Half * 2f, 6f) }, Position = new Vector2(0, 3), OneWayCollision = true });
        _phase = G.Range(0, 1f);
    }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        float travel = Math.Abs(Bottom - Top);
        float run = travel / Tune.Clockwork.LiftSpeed, rest = Tune.Clockwork.LiftRest;
        float cycle = 2f * (run + rest);
        float c = Mathf.PosMod(_t + _phase * cycle, cycle);
        float u;
        if (c < rest) { u = 0f; Moving = false; }
        else if (c < rest + run) { u = Smooth((c - rest) / run); Moving = true; }
        else if (c < 2f * rest + run) { u = 1f; Moving = false; }
        else { u = 1f - Smooth((c - 2f * rest - run) / run); Moving = true; }
        Pos = u;
        GlobalPosition = new Vector2(GlobalPosition.X, Mathf.Lerp(Top, Bottom, u));
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
}

/// <summary>
/// A jet of scalding steam (a boiler's, the guardian's): from its origin along <see cref="Dir"/> for <see cref="Length"/> px, <see cref="Life"/>
/// seconds, scalding whoever stands in it (a thin share of health every third of a second) and pushing them along it.
/// </summary>
public partial class SteamJet : Node2D
{
    public Vector2 Dir = Vector2.Right;
    public float Length = 140f, Width = 24f, Life = 1.4f;
    public Enemy Source;
    private float _t, _tick;
    public float T => _t;
    /// <summary>For the 3D view: the length it has reached now (it shoots out fast, then holds, then dies back).</summary>
    public float Reach => Length * Math.Min(1f, _t * 7f) * Math.Clamp((Life - _t) / 0.3f, 0f, 1f);

    public override void _Ready() { ZIndex = 3; Dir = Dir.Normalized(); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        if (_t > Life) { QueueFree(); return; }
        // (it stops short of rock)
        float len = Reach;
        if (G.Cave.Raycast(GlobalPosition, Dir, len, out var hit, 3f)) len = GlobalPosition.DistanceTo(hit);
        var p = G.Player;
        if (p != null && !p.Dead)
        {
            var to = p.GlobalPosition + new Vector2(0, -4) - GlobalPosition;
            float along = to.Dot(Dir);
            float across = Math.Abs(to.Dot(new Vector2(-Dir.Y, Dir.X)));
            if (along > -6f && along < len && across < Width * (0.5f + 0.5f * Math.Clamp(along / Length, 0f, 1f)) + 8f)
            {
                p.Velocity += Dir * Tune.Clockwork.SteamPush * dt * 8f * p.Stats.KnockTakenMult;
                // (steam is nothing to the Automaton but a push)
                if (_tick <= 0 && !p.Stats.Waterproof)
                {
                    _tick = Tune.Clockwork.SteamTick;
                    p.Hurt(p.Stats.MaxHp * Tune.Clockwork.SteamShare + Tune.Clockwork.SteamFlat * G.DepthDmg, GlobalPosition, 60, Source);
                }
            }
        }
        if (G.Chance(0.5f)) G.Fx.Smoke(GlobalPosition + Dir * G.Range(0, len), 1, new Color(0.9f, 0.9f, 0.92f, 0.45f), 10);
    }
}
