using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _wingSteps;

    /// <summary>
    /// `--scenario=wing --wing`: a level's camera keeps to the main occupied area, so a secret wing past its edge can't be seen; its wall
    /// of rubble is in the way; once it is down the view is let out (it slides, it doesn't jump) and the wing is there to walk into, with a
    /// silver chest. wing_*.png.
    /// </summary>
    private void WingScenario()
    {
        if (_wingSteps == null) { if (_scT < 0.6f) return; _wingSteps = WingRun().GetEnumerator(); }
        if (!_wingSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> WingRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 9000; p.Hp = 9000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        var wing = cave.Wings.FirstOrDefault();
        ScCheck($"this level has a secret wing ({cave.Wings.Count}), and its wall of rubble ({wing?.Plug.Round()})", wing != null);
        if (wing == null) yield break;
        var wall = Rubble.All.FirstOrDefault(r => r.Index == wing.RubbleIndex);
        ScCheck($"the wall is a rubble plug, secret, standing ({wall != null && wall.Secret && !wall.Cleared})", wall != null && wall.Secret && !wall.Cleared);
        var view = cave.ViewRect;
        ScCheck($"the map is wider than the view of the main area ({cave.SizePx.X:0} px against {view.End.X:0}), and the chamber lies out of the view ({wing.Chamber.Center.X:0} > {_cam.LimitRight})",
            view.End.X < cave.SizePx.X - 200f && wing.Chamber.Center.X - wing.Chamber.RxPx > _cam.LimitRight);

        // ---- before: stand at the wall, the camera held to the main area
        int side = 1;
        FindStandBy(cave, wing, out var stand);
        p.GlobalPosition = stand; p.Velocity = Vector2.Zero;
        foreach (var _ in Wait(1.8f)) yield return null;
        var half = GetViewport().GetVisibleRect().Size / _cam.Zoom * 0.5f;
        float seenRight = _cam.GetScreenCenterPosition().X + half.X;
        ScCheck($"at the wall the view stops at the edge of the main area (sees to {seenRight:0}, limit {_cam.LimitRight}, the chamber from {wing.Chamber.Center.X - wing.Chamber.RxPx:0})",
            seenRight <= _cam.LimitRight + 1f && seenRight < wing.Chamber.Center.X - wing.Chamber.RxPx);
        ScShot("wing_wall");
        int limit0 = _cam.LimitRight;

        // ---- the wall comes down
        wall.Smash();
        float maxStep = 0; float lastX = _cam.GetScreenCenterPosition().X;
        for (float t = 0; t < 3.5f; t += (float)GetProcessDeltaTime())
        {
            float cx = _cam.GetScreenCenterPosition().X;
            maxStep = Math.Max(maxStep, Math.Abs(cx - lastX) / Math.Max(0.001f, (float)GetProcessDeltaTime())); lastX = cx;
            if (t > 1.0f && t < 1.0f + (float)GetProcessDeltaTime() * 1.5f) ScShot("wing_opening");
            yield return null;
        }
        ScCheck($"with it down the view is let out ({limit0} -> {_cam.LimitRight}, to take in the wing to {wing.Bounds.End.X:0})", _cam.LimitRight >= (int)wing.Bounds.End.X - 3 && wall.Cleared);
        ScCheck($"and it slid out, it didn't jump (the view's top speed {maxStep:0} px/s)", maxStep < 900f);

        // ---- the wing: walk in
        p.InputOverride = () => _scInput;
        _scInput = new PlayerInput { Move = new Vector2(side, 0) };
        float tEnd = 0;
        for (float t = 0; t < 14f && p.GlobalPosition.X < wing.Chamber.Center.X - 20f; t += (float)GetProcessDeltaTime()) { tEnd = t; yield return null; }
        _scInput = default;
        foreach (var _ in Wait(1.5f)) yield return null;
        ScCheck($"a hero walks through the passage into the chamber ({p.GlobalPosition.X:0} of {wing.Chamber.Center.X:0}, {tEnd:0.0} s)", p.GlobalPosition.X > wing.Chamber.Center.X - 40f);
        ScShot("wing_chamber");
        var chest = _world.GetChildren().OfType<Chest>().FirstOrDefault(c => c.Tier == ChestTier.Relic && c.GlobalPosition.DistanceTo(wing.Chamber.Center) < wing.Chamber.RxPx);
        ScCheck($"with a silver chest in it ({chest?.GlobalPosition.Round()})", chest != null);
        var seen = _cam.GetScreenCenterPosition().X + half.X;
        ScCheck($"and the view takes it in (centre at {_cam.GetScreenCenterPosition().X:0}, the chamber at {wing.Chamber.Center.X:0})", Math.Abs(_cam.GetScreenCenterPosition().X - wing.Chamber.Center.X) < half.X * 0.8f);
    }

    private static void FindStandBy(CaveData cave, SecretWing wing, out Vector2 stand)
    {
        stand = wing.Plug + new Vector2(-60, 0);
        if (cave.FindFloor(wing.Plug + new Vector2(-40, -20), 100, out var f)) stand = f + new Vector2(0, -14);
    }
}
