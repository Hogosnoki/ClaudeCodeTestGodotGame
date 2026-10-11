using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _surfSteps;

    /// <summary>
    /// `--scenario=surface` (open water): swimming up stops at the surface with the head out (breathing, no bobbing); a press of jump there
    /// leaps out; falling back in, a press does nothing until the swimmer has gone back under (or stopped coming down). surface_*.png.
    /// </summary>
    private void SurfaceScenario()
    {
        if (_surfSteps == null) { if (_scT < 0.6f) return; _surfSteps = SurfaceRun().GetEnumerator(); }
        if (!_surfSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> SurfaceRun()
    {
        var p = G.Player; var cave = G.Cave;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = 5000; p.Hp = 5000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        float dt() => (float)GetProcessDeltaTime();
        // open water: 120 px of it under the surface, and open air above
        Vector2? spot = null;
        for (float x = 120; x < cave.SizePx.X - 120 && spot == null; x += 16)
        {
            var at = new Vector2(x, cave.WaterY + 70);
            bool ok = true;
            for (float y = cave.WaterY - 60; y < cave.WaterY + 120 && ok; y += 8)
                for (float dx = -20; dx <= 20 && ok; dx += 10) ok = !cave.IsSolid(new Vector2(x + dx, y));
            // (and no rock slab hanging over it)
            if (ok && RockLedge.All.Any(l => IsInstanceValid(l) && Math.Abs(l.GlobalPosition.X - x) < l.Half + 30f && l.GlobalPosition.Y > cave.WaterY - 120f && l.GlobalPosition.Y < cave.WaterY + 20f)) ok = false;
            if (ok) spot = at;
        }
        ScCheck($"open water to swim in ({spot})", spot != null);
        if (spot == null) yield break;
        p.GlobalPosition = spot.Value; p.Velocity = Vector2.Zero;
        // ---- swimming up: it stops at the surface
        _scInput = new PlayerInput { Move = new Vector2(0, -1), Up = true };
        foreach (var _ in Wait(1.6f)) yield return null;
        float yMin = float.MaxValue, yMax = float.MinValue;
        for (float t = 0; t < 1.2f; t += dt()) { yMin = Math.Min(yMin, p.GlobalPosition.Y); yMax = Math.Max(yMax, p.GlobalPosition.Y); yield return null; }
        ScShot("surface_rest");
        ScCheck($"swimming up, it comes to rest at the surface without bobbing (y {yMin - cave.WaterY:0.0}..{yMax - cave.WaterY:0.0} from the surface, in water {p.InWater})", p.InWater && yMax - yMin < 3f && yMin > cave.WaterY - 2f && yMax < cave.WaterY + 12f);
        ScCheck($"with its head out, breathing ({!p.HeadUnder})", !p.HeadUnder);
        // ---- a leap out
        _scInput = new PlayerInput { Jump = true, JumpHeld = true };
        foreach (var _ in Wait(0.05f)) yield return null;
        _scInput = new PlayerInput { JumpHeld = true };
        float top = p.GlobalPosition.Y;
        for (float t = 0; t < 0.6f; t += dt()) { top = Math.Min(top, p.GlobalPosition.Y); yield return null; }
        ScCheck($"a press of jump at the surface leaps out ({cave.WaterY - top:0} px above it)", top < cave.WaterY - 24f);
        // ---- back in, coming down: a press does nothing
        _scInput = default;
        for (float t = 0; t < 2f && !p.InWater; t += dt()) yield return null;
        ScCheck($"it falls back in ({p.InWater}, coming down at {p.Velocity.Y:0} px/s)", p.InWater);
        float yIn = p.GlobalPosition.Y;
        _scInput = new PlayerInput { Jump = true, JumpHeld = true };
        foreach (var _ in Wait(0.05f)) yield return null;
        _scInput = default;
        float top2 = p.GlobalPosition.Y;
        for (float t = 0; t < 0.4f; t += dt()) { top2 = Math.Min(top2, p.GlobalPosition.Y); yield return null; }
        ScCheck($"falling back in, a press of jump doesn't leap again (rose {yIn - top2:0} px)", top2 > cave.WaterY - 6f);
        // ---- under again, it can
        for (float t = 0; t < 3f && !p.HeadUnder; t += dt()) yield return null;
        _scInput = new PlayerInput { Move = new Vector2(0, -1), Up = true };
        foreach (var _ in Wait(1.4f)) yield return null;
        _scInput = new PlayerInput { Jump = true, JumpHeld = true };
        foreach (var _ in Wait(0.05f)) yield return null;
        _scInput = new PlayerInput { JumpHeld = true };
        float top3 = p.GlobalPosition.Y;
        for (float t = 0; t < 0.6f; t += dt()) { top3 = Math.Min(top3, p.GlobalPosition.Y); yield return null; }
        _scInput = default;
        ScCheck($"gone back under and up again, it can leap ({cave.WaterY - top3:0} px above the surface)", top3 < cave.WaterY - 24f);
    }
}
