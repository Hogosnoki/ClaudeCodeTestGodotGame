using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _mlSteps;

    /// <summary>`--scenario=melee --hero=NAME`: a melee attack holds the hero still for 0.1 s as it begins, and a blow that lands holds it again for 0.1 s once the hit-stop is over.</summary>
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
        ScCheck("the pauses are 0.1 s each", Math.Abs(Tune.Feel.MeleePause - 0.1f) < 0.001f && Math.Abs(Tune.Feel.MeleeHitPause - 0.1f) < 0.001f);

        // a swing at nothing: the opening pause only, walking pushed the whole time
        var at = p.GlobalPosition;
        _scInput = new PlayerInput { Attack = true, Move = new Vector2(1, 0), Aim = new Vector2(1, 0), AimGiven = true };
        yield return null; yield return null;
        ScCheck($"a swing begins with a pause of 0.1 s ({p.LastOpenPause:0.###})", Math.Abs(p.LastOpenPause - 0.1f) < 0.001f);
        _scInput = new PlayerInput { Move = new Vector2(1, 0) };
        for (int f = 0; p.PauseLeft > 0f && f < 400; f++) yield return null;
        ScCheck($"the hero did not move in it (drift {p.PauseDrift:0.00} px)", p.PauseDrift < 0.01f);
        ScCheck("a swing at nothing queues no second pause", p.LastHitPause == 0f);
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
    }
}
