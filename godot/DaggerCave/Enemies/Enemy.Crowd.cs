using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Soft separation (<see cref="Tune.Crowd"/>): creatures don't stack into one point. Each frame, two creatures closer than
/// <c>Spacing</c> x the sum of their radii are eased apart, the lighter giving way to the heavier (a generator, a boss or a creature
/// frozen solid doesn't give way at all). A walker is only eased sideways (the floor holds it up), and never into rock. A creature pressed
/// against the crowd for <c>SqueezeAfter</c> s while trying to get past squeezes through for <c>SqueezeFor</c> s, so a tunnel never jams.
/// It is not a solid body: a crowd stays a crowd you can carve through, spread across the ground rather than piled in one spot.
/// </summary>
public abstract partial class Enemy
{
    private float _crowdT, _squeezeT;
    /// <summary>A crowd-mate stands pressed against it on this side (-1, 1; 0 none): it doesn't walk on into them (it waits its turn).</summary>
    private int _crowdBlock;
    private float _heldT, _flankVx, _flankT;
    /// <summary>For the tests: leaps over the crowd to its hero's far side.</summary>
    public int Flanks { get; private set; }
    /// <summary>For the tests: how far it has been eased apart from the rest, in all.</summary>
    public float CrowdNudged { get; private set; }

    // (a grid of who stands where, built once a physics frame: neighbours are looked for in the cells about one)
    private static ulong _gridFrame = ulong.MaxValue;
    private static readonly Dictionary<long, List<Enemy>> _grid = new();
    private static readonly Stack<List<Enemy>> _spare = new();
    private const float GridCell = 48f;

    private static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;

    private static void BuildGrid()
    {
        ulong frame = Engine.GetPhysicsFrames();
        if (frame == _gridFrame) return;
        _gridFrame = frame;
        foreach (var l in _grid.Values) { l.Clear(); _spare.Push(l); }
        _grid.Clear();
        foreach (var e in G.Enemies)
        {
            if (e == null || e.Dead || !e.IsInsideTree()) continue;
            var p = e.GlobalPosition;
            long k = CellKey((int)MathF.Floor(p.X / GridCell), (int)MathF.Floor(p.Y / GridCell));
            if (!_grid.TryGetValue(k, out var list)) { list = _spare.Count > 0 ? _spare.Pop() : new List<Enemy>(); _grid[k] = list; }
            list.Add(e);
        }
    }

    /// <summary>How hard it holds its ground in a crowd (infinity: it never gives way).</summary>
    private float CrowdMass
    {
        get
        {
            if (this is Generator || IsBoss || IsGuardian || FrozenSolid || ManualMove) return float.PositiveInfinity;
            float r = HitRadius;
            return r * r * (1f + KnockResist * 2f) * (Elite ? 1.5f : 1f);
        }
    }

    /// <summary>Before it moves: a walker with a crowd-mate pressed against it on the side it's walking to holds there (unless squeezing through).</summary>
    private void HoldInCrowd(float dt)
    {
        // (in the air over the crowd: the leap carries it, whatever it would rather do)
        if (_flankT > 0f)
        {
            _flankT -= dt;
            if (_flankT < Tune.Crowd.FlankAir - 0.1f && IsOnFloor()) _flankT = 0f;
            else { Velocity = new Vector2(_flankVx, Velocity.Y); return; }
        }
        // (waiting behind a crowd-mate on the way to its hero: it holds there)
        bool behind = _crowdBlock != 0 && _squeezeT <= 0f && Walks && Math.Sign(ToP.X) == _crowdBlock;
        if (!behind) { _heldT = Math.Max(0f, _heldT - dt * 0.3f); return; }
        if (Math.Sign(Velocity.X) == _crowdBlock) Velocity = new Vector2(0f, Velocity.Y);
        // (held at the back a while, a light one leaps the crowd to come at its hero from the other side)
        if ((_heldT += dt) < Tune.Crowd.FlankAfter || !IsOnFloor() || HitRadius > Tune.Crowd.FlankMaxRadius || KnockResist > 0.5f || Attacking) return;
        float toHero = ToP.X;
        if (Math.Abs(toHero) > Tune.Crowd.FlankRange || Math.Sign(toHero) != _crowdBlock) return;
        // (aimed to land a little past its hero)
        float over = Math.Abs(toHero) + 40f;
        _flankVx = _crowdBlock * over / Tune.Crowd.FlankAir;
        _flankT = Tune.Crowd.FlankAir + 0.3f;
        Velocity = new Vector2(_flankVx, -Tune.Crowd.FlankAir * Grav * 0.5f);
        _heldT = 0f;
        _squeezeT = Tune.Crowd.FlankAir + 0.2f;
        Flanks++;
    }

    /// <summary>Eases it apart from the creatures it's pressed against (after it has moved this frame).</summary>
    private void Separate(float dt)
    {
        if (!Tune.Crowd.On || Master != null || Puppet || Dead || ManualMove || IsBoss || IsGuardian) return;
        _crowdBlock = 0;
        if (_squeezeT > 0f) { _squeezeT -= dt; return; }
        BuildGrid();
        float mine = CrowdMass;
        if (float.IsPositiveInfinity(mine)) return;
        var me = GlobalPosition;
        float rMe = HitRadius;
        int cx = (int)MathF.Floor(me.X / GridCell), cy = (int)MathF.Floor(me.Y / GridCell);
        var push = Vector2.Zero;
        bool againstWay = false;
        float way = Math.Sign(Velocity.X);
        for (int gx = cx - 1; gx <= cx + 1; gx++)
        for (int gy = cy - 1; gy <= cy + 1; gy++)
        {
            if (!_grid.TryGetValue(CellKey(gx, gy), out var list)) continue;
            foreach (var o in list)
            {
                if (o == this || o.Dead || o.Master != null) continue;
                float min = (rMe + o.HitRadius) * Tune.Crowd.Spacing;
                var d = me - o.GlobalPosition;
                float dist2 = d.LengthSquared();
                // (a walker and a flier pass over and under each other: the air is the flier's)
                if (Walks != o.Walks) continue;
                // (one standing just by it on a side: it won't walk on into them)
                if (Walks && dist2 < min * min * 1.4f && Math.Abs(d.X) > 0.5f && Math.Abs(d.Y) < min && (_crowdBlock == 0 || Math.Sign(-d.X) == Math.Sign(ToP.X))) _crowdBlock = Math.Sign(-d.X);
                if (dist2 >= min * min) continue;
                float dist = MathF.Sqrt(dist2);
                Vector2 dir;
                if (dist < 0.5f)
                {
                    // (stacked exactly: the pair part opposite ways, by which came first)
                    float side = GetInstanceId() > o.GetInstanceId() ? 1f : -1f;
                    float a = (GetInstanceId() * 37 % 360) * Mathf.Pi / 180f;
                    dir = Walks ? new Vector2(side, 0f) : new Vector2(MathF.Cos(a), MathF.Sin(a)) * side;
                }
                else dir = d / dist;
                float theirs = o.CrowdMass;
                // (the lighter gives way: all of it to something that never does)
                float share = float.IsPositiveInfinity(theirs) ? 1f : theirs / (mine + theirs);
                push += dir * (min - dist) * share;
                if (way != 0f && Math.Sign(-d.X) == way) againstWay = true;
            }
        }
        if (push == Vector2.Zero) { _crowdT = 0f; return; }
        if (_squeezeT > 0f) return;
        // (pressed against the crowd while trying to get by: after a while it squeezes through)
        // (only on the way somewhere: by its hero, a crowd waits its turn, spread about, rather than squeezing to the front)
        if (againstWay && Walks && Math.Abs(Velocity.X) > 10f && TrueDistP > Tune.Crowd.SqueezeFrom)
        {
            if ((_crowdT += dt) > Tune.Crowd.SqueezeAfter) { _crowdT = 0f; _squeezeT = Tune.Crowd.SqueezeFor; return; }
        }
        else _crowdT = Math.Max(0f, _crowdT - dt);
        var step = push * Math.Min(1f, Tune.Crowd.Stiffness * dt);
        if (Walks) step.Y = 0f;
        float len = step.Length();
        if (len < 0.05f) return;
        if (len > Tune.Crowd.MaxStep) step *= Tune.Crowd.MaxStep / len;
        // (never into rock: the step stops where the body would touch it)
        var hit = MoveAndCollide(step, testOnly: false);
        CrowdNudged += hit == null ? step.Length() : hit.GetTravel().Length();
    }
}
