using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A crude autopilot used by `--autotest` to exercise the game headlessly: path-finds over open
/// cells toward the boss room (or a random room when stuck), jumps at ledges, swims toward
/// waypoints, and fights whatever is nearby.
/// </summary>
public sealed class BotPilot
{
    private PlayerInput _cur;
    private List<Vector2> _path = new();
    private float _pathT, _stuckT, _goalT, _atkCd, _throwCd, _ability2Cd, _dodgeCd, _jumpHoldT;
    private Vector2 _lastPos;
    private Vector2 _goal;
    private bool _haveGoal;
    private readonly Random _rng = new(5);

    public PlayerInput Read()
    {
        var r = _cur;
        _cur.Jump = false; _cur.Attack = false; _cur.Ability = false; _cur.Ability2 = false; _cur.Dodge = false; _cur.Potion = false; _cur.Interact = false;
        return r;
    }

    /// <summary>A new level: forget the old goal.</summary>
    public void Reset() { _haveGoal = false; _path.Clear(); _stuckT = 0; _bestGoalDist = float.MaxValue; _noProgressT = 0; }

    /// <summary>Head straight for the guardian (and then the exits) rather than wandering.</summary>
    public bool Focused;
    /// <summary>Test harness only: when the bot makes no headway for a while, skip it along its path.</summary>
    public bool SkipAhead;
    private float _bestGoalDist = float.MaxValue, _noProgressT;

    public void Tick(float dt)
    {
        var p = G.Player;
        var cave = G.Cave;
        if (p == null || p.Dead || cave == null) return;
        _pathT -= dt; _goalT -= dt; _atkCd -= dt; _throwCd -= dt; _ability2Cd -= dt; _dodgeCd -= dt; _jumpHoldT -= dt;
        var pos = p.GlobalPosition;

        var exits = G.Main.ExitSpots;
        if (exits.Count > 0 && (!_haveGoal || !exits.Contains(_goal) || _goalT <= 0))
        {
            // the guardian is down: take the steeper way on
            _haveGoal = true; _goal = exits[^1]; _goalT = 60; _pathT = 0; _bestGoalDist = float.MaxValue;
        }
        if (!_haveGoal || _goalT <= 0)
        {
            _haveGoal = true;
            if (cave.Boss != null && _rng.NextDouble() < (Focused ? 0.85 : 0.6)) { _goal = Focused ? cave.Boss.Floor + new Vector2(0, -20) : cave.Boss.Center; _goalT = 40; }
            else { _goal = cave.Rooms[_rng.Next(cave.Rooms.Count)].Center; _goalT = 25; }
            _bestGoalDist = float.MaxValue;
            _pathT = 0;
        }
        if (p.Hp < p.Stats.MaxHp * 0.35f && p.Potions > 0) _cur.Potion = true;
        // exits wait to be taken: step in when standing at one; open any chest passed on the way
        if (exits.Count > 0 && pos.DistanceTo(_goal) < 30) _cur.Interact = true;
        if (Chest.At(pos) != null) _cur.Interact = true;
        if (_pathT <= 0) { _pathT = 1.2f; _path = FindPath(cave, pos, _goal); }

        float gd = pos.DistanceTo(_goal);
        if (gd < _bestGoalDist - 32) { _bestGoalDist = gd; _noProgressT = 0; }
        else _noProgressT += dt;
        if (SkipAhead && _noProgressT > 15f && _path.Count > 2)
        {
            var at = _path[Math.Min(14, _path.Count - 1)];
            p.GlobalPosition = at; p.Velocity = Vector2.Zero;
            _noProgressT = 0; _bestGoalDist = at.DistanceTo(_goal);
            _pathT = 0;
            GD.Print($"[bot] no headway for 15 s: skipped ahead along the path to {at}");
        }

        // stuck detection
        if (pos.DistanceTo(_lastPos) < 1.5f) _stuckT += dt; else _stuckT = 0;
        _lastPos = pos;
        if (_stuckT > 5f) { _goalT = 0; _stuckT = 0; }

        Vector2 wp = _goal;
        while (_path.Count > 1 && _path[0].DistanceTo(pos) < 20) _path.RemoveAt(0);
        if (_path.Count > 0) wp = _path[Math.Min(2, _path.Count - 1)];

        var to = wp - pos;
        var move = Vector2.Zero;
        if (p.InWater)
        {
            move = to.Normalized();
            _cur.JumpHeld = to.Y < -10;
            if (to.Y < -10 && pos.Y < cave.WaterY + 24) _cur.Jump = true;
        }
        else
        {
            if (Math.Abs(to.X) > 5) move.X = Math.Sign(to.X);
            bool needUp = to.Y < -18;
            if ((needUp || _stuckT > 0.35f) && p.IsOnFloor()) { _cur.Jump = true; _jumpHoldT = 0.35f; }
            else if (!p.IsOnFloor() && needUp && p.Velocity.Y > 50) _cur.Jump = true; // uses double jump / air dash / wall jump
            _cur.JumpHeld = _jumpHoldT > 0;
            if (to.Y > 20) move.Y = 1;
        }

        // Combat
        Enemy target = null; float bd = 230;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            float d = e.GlobalPosition.DistanceTo(pos);
            if (d < bd && cave.LineClear(pos, e.GlobalPosition)) { bd = d; target = e; }
        }
        _cur.Aim = move.LengthSquared() > 0 ? move.Normalized() : Vector2.Right;
        if (target != null)
        {
            var te = target.GlobalPosition - pos;
            _cur.Aim = te.Normalized();
            // the casters fight from medium range; the blades close in
            bool elementalist = p.Stats.Hero == HeroKind.Elementalist;
            bool rogue = p.Stats.Hero == HeroKind.Rogue;
            bool caster = p.Stats.Hero == HeroKind.Vitalist || elementalist;
            float reach = elementalist ? Tune.Elementalist.BoltRange * 0.7f : caster ? Tune.Vitalist.DrainRange * 0.8f : 44 + target.HitRadius;
            if (bd < reach && _atkCd <= 0) { _cur.Attack = true; _atkCd = 0.12f; }
            // abilities: charge the blade before closing in, dash into attacks, heal when hurt
            if (_throwCd <= 0)
            {
                bool use = p.Stats.Hero switch
                {
                    HeroKind.Warden => target.Attacking && bd < 90,
                    HeroKind.Vitalist => p.Hp < p.Stats.MaxHp * 0.6f,
                    HeroKind.Elementalist => bd < Tune.Elementalist.BlizzardRange && p.SecondaryReady,
                    HeroKind.Rogue => bd > 50 && bd < Tune.Rogue.ThrowRange * 0.8f && p.DaggersInHand > 0,
                    _ => bd < 120,
                };
                if (use) { _cur.Ability = true; _throwCd = 1.0f; }
            }
            // second abilities: bash what's about to strike, heave at close quarters, rupture when the reserve is full
            if (_ability2Cd <= 0)
            {
                bool use2 = p.Stats.Hero switch
                {
                    HeroKind.Warden => bd < 50,
                    HeroKind.Vitalist => p.RuptureReady && bd < Tune.Vitalist.RuptureRange,
                    HeroKind.Elementalist => p.SnapTargets > 0 && p.Alimus >= p.SnapCost,
                    HeroKind.Rogue => p.DaggersInHand < 2,
                    _ => bd < 60 && p.IsOnFloor(),
                };
                if (use2) { _cur.Ability2 = true; _ability2Cd = 1.5f; }
            }
            if (bd > reach * 0.8f && !p.InWater) move.X = Math.Sign(te.X);
            else if (caster && bd < 60 && !p.InWater) move.X = -Math.Sign(te.X);
            if (_dodgeCd <= 0 && !elementalist && (!rogue || p.AbilityChargeReady) && (caster ? bd < 90 : p.Hp < p.Stats.MaxHp * 0.4f && bd < 60)) { _cur.Dodge = true; _dodgeCd = 1.5f; if (!caster) move.X = -Math.Sign(te.X); }
        }
        _cur.Move = move;
    }

    private static List<Vector2> FindPath(CaveData cave, Vector2 from, Vector2 to)
    {
        int W = cave.W, H = cave.H;
        var s = new Vector2I((int)(from.X / CaveData.Cell), (int)(from.Y / CaveData.Cell));
        var g = new Vector2I((int)(to.X / CaveData.Cell), (int)(to.Y / CaveData.Cell));
        var prev = new int[W * H];
        Array.Fill(prev, -2);
        var q = new Queue<int>();
        int si = s.Y * W + s.X;
        if (si < 0 || si >= W * H) return new List<Vector2>();
        prev[si] = -1; q.Enqueue(si);
        int gi = g.Y * W + g.X, found = -1;
        int best = si; int bestD = int.MaxValue;
        while (q.Count > 0)
        {
            int u = q.Dequeue();
            if (u == gi) { found = u; break; }
            int ui = u % W, uj = u / W;
            int dd = Math.Abs(ui - g.X) + Math.Abs(uj - g.Y);
            if (dd < bestD) { bestD = dd; best = u; }
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ui + di, j = uj + dj;
                    if (i < 0 || j < 0 || i >= W || j >= H) continue;
                    int v = j * W + i;
                    if (prev[v] != -2 || !cave.CellOpen(i, j)) continue;
                    if (cave.Liquid == Liquid.Lava && (j + 0.5f) * CaveData.Cell > cave.WaterY - 8) continue;
                    // keep a cell of clearance so the path doesn't hug walls
                    if (!cave.CellOpen(i, j - 1) && !cave.CellOpen(i, j + 1)) continue;
                    prev[v] = u; q.Enqueue(v);
                }
        }
        if (found < 0) found = best;
        var path = new List<Vector2>();
        for (int c = found; c >= 0; c = prev[c]) path.Add(new Vector2(c % W + 0.5f, c / W + 0.5f) * CaveData.Cell);
        path.Reverse();
        return path;
    }
}
