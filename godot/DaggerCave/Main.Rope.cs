using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _ropeSteps;

    /// <summary>
    /// `--scenario=rope`: a hero at an edge holds the rope button and the rope pays out (the coil falling at its bottom, the hero held where
    /// they stand); letting go stops it, a second press reels it in; held on, it runs out at five heights; a climber's grab sets it swinging
    /// their way, they climb, pump, and jump off with the swing (rope_N.png).
    /// </summary>
    private void RopeScenario()
    {
        if (_ropeSteps == null) { if (_scT < 0.6f) return; _ropeSteps = RopeRun().GetEnumerator(); }
        if (!_ropeSteps.MoveNext()) ScEnd();
    }

    /// <summary>Floors with a sheer drop just beside them, deepest first (side +1: the drop is to the right).</summary>
    private List<(Vector2 stand, int side, float depth)> FindEdges(float drop)
    {
        var cave = G.Cave;
        var found = new List<(Vector2 stand, int side, float depth)>();
        for (float x = 40; x < cave.SizePx.X - 40; x += 8)
            for (float y = 40; y < cave.SizePx.Y - 40; y += 24)
            {
                var p = new Vector2(x, y);
                if (cave.IsSolid(p) || !cave.FindFloor(p, 30, out var f)) continue;
                // (real ground: solid well down under the feet, and to either side of them)
                if (!cave.IsSolid(f + new Vector2(0, 10)) || !cave.IsSolid(f + new Vector2(-6, 6)) || !cave.IsSolid(f + new Vector2(6, 6))) continue;
                if (cave.IsWater(f + new Vector2(0, -6)) || cave.IsSolid(f + new Vector2(0, -30))) continue;
                foreach (int s in new[] { 1, -1 })
                {
                    var over = f + new Vector2(s * 18, 4);
                    if (cave.IsSolid(over) || cave.IsSolid(over + new Vector2(0, -20))) continue;
                    float h = 0; while (h < drop && !cave.IsSolid(over + new Vector2(0, h + 4))) h += 4;
                    if (h < 60 || cave.IsWater(over + new Vector2(0, h))) continue;
                    found.Add((f + new Vector2(0, -14), s, h));
                }
            }
        return found.OrderByDescending(e => Math.Min(e.depth, drop)).ToList();
    }

    private IEnumerable<object> RopeRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 5000; p.Hp = 5000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        // (an edge the hero really stands at: tried in turn until one holds them)
        Vector2 stand = default; int side = 0; float depth = 0; bool found = false;
        foreach (var (st, sd, dp) in FindEdges(Tune.Rope.Length + 20).Take(12))
        {
            p.GlobalPosition = st; p.Velocity = Vector2.Zero;
            foreach (var _ in SbSleep(0.8f)) yield return null;
            if (p.IsOnFloor() && p.GlobalPosition.DistanceTo(st) < 8f) { stand = p.GlobalPosition; side = sd; depth = dp; found = true; break; }
        }
        ScCheck($"an edge with a drop beside it ({stand.Round()}, side {side}, {depth:0} px deep)", found);
        if (!found) yield break;

        // ---- hold the button: it pays out; the hero can't walk off meanwhile
        float x0 = p.GlobalPosition.X;
        _scInput = new PlayerInput { Rope = true, RopeHeld = true, Move = new Vector2(side, 0) };
        yield return null;
        _scInput = new PlayerInput { RopeHeld = true, Move = new Vector2(side, 0) };
        var rope = Rope.All.FirstOrDefault(r => r.Holder == p);
        ScCheck($"a press lets a rope down from the hero's hands ({rope != null}, holding {p.HoldsRope})", rope != null && p.HoldsRope);
        if (rope == null) yield break;
        foreach (var _ in SbSleep(0.35f)) yield return null;
        bool coilAtBottom = rope.P.Count > 2 && rope.P[^1].Y >= rope.P.Take(rope.P.Count - 1).Max(q => q.Y) - 0.5f;
        ScCheck($"it pays out as the coil falls ({rope.Unrolled:0} px out, {rope.CoilLeft:0} still wound, the coil the lowest point: {coilAtBottom})", rope.State == Rope.Phase.Lowering && rope.Unrolled > 15f && coilAtBottom);
        ScShot("rope_0_lowering");
        ScCheck($"holding it, the hero stays put though pushing on ({Math.Abs(p.GlobalPosition.X - x0):0.0} px)", Math.Abs(p.GlobalPosition.X - x0) < 2f);

        // ---- let go of the button: it stops there and swings
        _scInput = default;
        foreach (var _ in SbSleep(0.1f)) yield return null;
        float stopped = rope.Unrolled;
        ScCheck($"letting go of the button stops it ({rope.State}, {stopped:0} px)", rope.State == Rope.Phase.Hanging && stopped < Tune.Rope.Length - 5f);
        foreach (var _ in SbSleep(0.8f)) yield return null;
        ScCheck($"and no more comes out ({rope.Unrolled:0} px)", Math.Abs(rope.Unrolled - stopped) < 0.5f);
        ScShot("rope_1_stopped");

        // ---- a second press: reeled in, and the hero is free
        _scInput = new PlayerInput { Rope = true }; yield return null; _scInput = default;
        ScCheck($"a second press reels it in ({rope.State})", rope.State == Rope.Phase.Reeling);
        for (float t = 0; t < 4f && GodotObject.IsInstanceValid(rope); t += (float)GetProcessDeltaTime()) yield return null;
        ScCheck($"it comes all the way in and is gone ({!GodotObject.IsInstanceValid(rope)}), the hero free ({!p.HoldsRope})", !GodotObject.IsInstanceValid(rope) && !p.HoldsRope);

        // ---- held on: it runs out at five heights, and the coil becomes its end
        p.GlobalPosition = stand; p.Velocity = Vector2.Zero;
        foreach (var _ in SbSleep(Tune.Rope.Cooldown + 0.3f)) yield return null;
        _scInput = new PlayerInput { Rope = true, RopeHeld = true }; yield return null;
        _scInput = new PlayerInput { RopeHeld = true };
        rope = Rope.All.FirstOrDefault(r => r.Holder == p);
        for (float t = 0; t < 4f && rope != null && rope.State == Rope.Phase.Lowering; t += (float)GetProcessDeltaTime()) yield return null;
        if (depth > Tune.Rope.Length + 4f)
            ScCheck($"held on, it runs out at {Tune.Rope.Length:0} px (five heights) and stops by itself ({rope?.State}, {rope?.Unrolled:0} px)", rope != null && rope.State == Rope.Phase.Hanging && rope.Unrolled > Tune.Rope.Length - 3f);
        else
            ScCheck($"held on, it comes to rest on the ground {depth:0} px down and stops by itself ({rope?.State}, {rope?.Unrolled:0} px)", rope != null && rope.State == Rope.Phase.Hanging && rope.Unrolled > depth * 0.8f);
        foreach (var _ in SbSleep(0.4f)) yield return null;
        ScShot("rope_2_full");
        // (the end swings a little: it moves after the stop)
        var e0 = rope.P[^1];
        foreach (var _ in SbSleep(0.3f)) yield return null;
        ScCheck($"its end swings a little after it stops ({rope.P[^1].DistanceTo(e0):0.0} px in 0.3 s)", rope.P[^1].DistanceTo(e0) > 0.3f);
        _scInput = default;

        // ---- a climber on a rope tied off over open air: the grab sets it swinging their way, they pump, climb, and jump off with the swing
        var at = rope.P[^1] + new Vector2(0, -20);
        _scInput = new PlayerInput { Rope = true }; yield return null; _scInput = default;
        for (float t = 0; t < 4f && GodotObject.IsInstanceValid(rope); t += (float)GetProcessDeltaTime()) yield return null;
        // (out over the drop, clear of the cliff, so it can swing)
        var top = stand + new Vector2(side * 70, 0);
        for (int k = 0; k < 6 && G.Cave.IsSolid(top + new Vector2(0, 40)); k++) top += new Vector2(side * 16, 0);
        var tied = new Rope { Position = top, Length = Tune.Rope.Length };
        _world.AddChild(tied);
        foreach (var _ in SbSleep(0.5f)) yield return null;
        var grabAt = tied.PointAt(tied.Unrolled * 0.6f);
        p.GlobalPosition = grabAt + new Vector2(-4, 8); p.Velocity = new Vector2(150, 0);
        _scInput = new PlayerInput { Move = new Vector2(0, -1) }; yield return null; yield return null;
        _scInput = default;
        ScCheck($"pushing up beside it takes hold ({p.OnRope})", p.OnRope);
        var v0 = tied.VelocityAt(tied.ClimbS, 1f / 60f);
        ScCheck($"and the rope there goes the hero's way ({v0.X:0} px/s, the hero had 150)", v0.X > 30f);
        float maxX = 0, minX = 0, cx = tied.GlobalPosition.X;
        for (float t = 0; t < 2.2f; t += (float)GetProcessDeltaTime())
        {
            // pump it: push the way it swings
            var sv = tied.VelocityAt(tied.ClimbS, 1f / 60f);
            _scInput = new PlayerInput { Move = new Vector2(Math.Sign(sv.X), 0) };
            maxX = Math.Max(maxX, p.GlobalPosition.X - cx); minX = Math.Min(minX, p.GlobalPosition.X - cx);
            yield return null;
        }
        ScCheck($"pumping swings it, and the hero with it ({minX:0}..{maxX:0} px either side)", maxX - minX > 30f && p.OnRope);
        ScShot("rope_3_swing");
        float y0 = p.GlobalPosition.Y;
        for (float t = 0; t < 1.0f; t += (float)GetProcessDeltaTime()) { _scInput = new PlayerInput { Move = new Vector2(0, -1) }; yield return null; }
        ScCheck($"up climbs ({y0 - p.GlobalPosition.Y:0} px)", y0 - p.GlobalPosition.Y > 25f);
        var before = tied.VelocityAt(tied.ClimbS, 1f / 60f);
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null; _scInput = default; yield return null;
        ScCheck($"jump lets go ({!p.OnRope}), with the swing's speed ({p.Velocity.X:0} px/s; the rope had {before.X:0})", !p.OnRope);
    }
}
