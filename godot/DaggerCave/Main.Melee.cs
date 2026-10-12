using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _mlSteps;

    /// <summary>`--scenario=melee --hero=NAME`: a melee attack has no delay as it begins; a blow that lands stops the hero's vertical speed and holds it still for 0.1 s once the hit-stop is over.</summary>
    private void MeleeScenario()
    {
        if (_mlSteps == null) { if (_scT < 0.8f) return; _mlSteps = MeleeRun().GetEnumerator(); }
        if (!_mlSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> MeleeRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.InputOverride = () => { var i = _scInput; _scInput.Attack = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1.0f)) yield return null;
        ScCheck("the pause after a landed blow is 0.1 s", Math.Abs(Tune.Feel.MeleeHitPause - 0.1f) < 0.001f);

        // a swing at nothing: no pause at all, and the walking pushed the whole time carries the hero on
        var at = p.GlobalPosition;
        _scInput = new PlayerInput { Attack = true, Move = new Vector2(1, 0), Aim = new Vector2(1, 0), AimGiven = true };
        yield return null; yield return null;
        ScCheck($"a swing begins with no delay ({p.PauseLeft:0.###} s of pause, {p.FreezeLeft:0.###} s of freeze)", p.PauseLeft <= 0f && p.FreezeLeft <= 0f);
        _scInput = new PlayerInput { Move = new Vector2(1, 0) };
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"the hero walked on through it ({p.GlobalPosition.X - at.X:0.0} px)", p.GlobalPosition.X > at.X + 2f);
        ScCheck("a swing at nothing queues no pause", p.LastHitPause == 0f);
        foreach (var _ in Wait(1.0f)) yield return null;
        _scInput = default;

        // a swing that lands: the second pause is queued behind the hit-stop and runs after it
        var foe = new Spider { Position = p.GlobalPosition + new Vector2(14 * p.Facing, 0) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        foe.Wake();
        foe.ForceGround();
        foe.Hp = foe.MaxHp = 100000f;
        foreach (var _ in Wait(0.3f)) yield return null;
        int runs0 = p.HitPausesRun;
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true };
        for (int f = 0; f < 120 && p.HitPausesRun == runs0; f++)
        {
            yield return null;
            _scInput = default;
        }
        ScCheck($"the blow landed and a second pause of 0.1 s was queued behind its hit-stop ({p.LastHitPause:0.###})", Math.Abs(p.LastHitPause - 0.1f) < 0.001f);
        ScCheck($"the second pause ran once the hit-stop was over ({p.HitPausesRun - runs0})", p.HitPausesRun == runs0 + 1);
        for (int f = 0; p.PauseLeft > 0f && f < 400; f++) yield return null;
        ScCheck($"and the hero held still through it (drift {p.PauseDrift:0.00} px)", p.PauseDrift < 0.01f);

        // a strike that lands in mid-air stops the fall
        p.GlobalPosition += new Vector2(0, -60);
        foe.GlobalPosition = p.GlobalPosition + new Vector2(14 * p.Facing, 0);
        p.Velocity = new Vector2(0, 150);
        bool landed = false;
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true };
        runs0 = p.HitPausesRun;
        for (int f = 0; f < 120 && !landed; f++)
        {
            yield return null;
            _scInput = default;
            if (p.LastHitPause > 0f && p.HitPausesRun > runs0) landed = true;
        }
        ScCheck($"a blow that lands in the air stops the fall (it landed {landed}; vertical speed {p.HitVyBefore:0} -> {p.HitVyAfter:0})", landed && p.HitVyBefore > 20f && p.HitVyAfter == 0f);
    }
}
