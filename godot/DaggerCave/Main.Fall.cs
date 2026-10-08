using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _fallSteps;

    /// <summary>
    /// `--scenario=fall`: fall damage counts the time spent at the terminal speed. An ordinary jump costs nothing, nor does a short drop;
    /// a long fall costs a share of the current health (up to half); and braking on the way down (here: a second jump's kick upward)
    /// starts the count afresh. (A long fall is made by holding the falling hero at one height until its time is up.)
    /// </summary>
    private void FallScenario()
    {
        if (_fallSteps == null) { if (_scT < 0.6f) return; _fallSteps = FallRun().GetEnumerator(); }
        if (!_fallSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> FallRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 1000; p.Hp = 1000;
        p.InputOverride = () => { var i = _scInput; _scInput.Jump = false; return i; };
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        // a floor with 140 px of open air above it
        bool found = false; var stand = Vector2.Zero;
        for (float x = 60; !found && FindRun(1, 60, false, out stand, x); x = stand.X + 24)
            found = cave.LineClear(stand, stand + new Vector2(0, -140));
        ScCheck($"a floor with open air above ({stand.Round()})", found);
        if (!found) yield break;

        void Home() { p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Hp = p.Stats.MaxHp; }
        IEnumerable<object> Fall(float seconds, bool brake)
        {
            // (held just above the floor, so the last of the fall adds next to nothing)
            var top = stand + new Vector2(0, -60);
            p.GlobalPosition = top; p.Velocity = new Vector2(0, Tune.Hero.MaxFallSpeed);
            // (by the hero's own clock: a slow frame holds several physics ticks)
            for (float t = 0; t < 10f && p.TerminalT < seconds; t += (float)GetProcessDeltaTime())
            {
                p.GlobalPosition = top;
                yield return null;
            }
            p.GlobalPosition = top;
            if (brake) p.Velocity = new Vector2(0, -180f);
            for (float t = 0; t < 2f && !p.IsOnFloor(); t += (float)GetProcessDeltaTime()) yield return null;
            foreach (var _ in SbSleep(0.2f)) yield return null;
        }

        // an ordinary jump
        Home();
        foreach (var _ in SbSleep(0.5f)) yield return null;
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        bool left = false;
        for (float t = 0; t < 2f; t += (float)GetProcessDeltaTime())
        {
            _scInput = new PlayerInput { JumpHeld = t < 0.4f };
            if (!p.IsOnFloor()) left = true; else if (left) break;
            yield return null;
        }
        _scInput = default;
        foreach (var _ in SbSleep(0.2f)) yield return null;
        ScCheck($"an ordinary jump costs nothing (hp {p.Hp:0} of {p.Stats.MaxHp:0}, jumped {left})", left && p.Hp >= p.Stats.MaxHp - 0.01f);

        // a short drop at the terminal speed
        Home();
        foreach (var _ in Fall(0.05f, false)) yield return null;
        ScCheck($"a short drop costs nothing ({p.LastFallDamage:0.0}, hp {p.Hp:0})", p.LastFallDamage == 0f && p.Hp >= p.Stats.MaxHp - 0.01f);

        // a moderate fall: a little
        Home();
        foreach (var _ in Fall(Tune.Hero.FallGraceSeconds + 0.25f, false)) yield return null;
        float mid = p.LastFallDamage;
        ScCheck($"a longer fall at the terminal speed costs a little ({mid:0.0} of 1000)", mid > 5f && mid < 300f);

        // a very long one: half of what it had
        Home();
        foreach (var _ in Fall(Tune.Hero.FallGraceSeconds + Tune.Hero.FallRampSeconds + 0.6f, false)) yield return null;
        ScCheck($"a very long fall costs half the current health ({p.LastFallDamage:0.0}, hp {p.Hp:0})", Math.Abs(p.LastFallDamage - 500f) < 2f && p.Hp > 400f);
        ScShot("fall_hard");

        // the same, braked at the last moment
        Home();
        foreach (var _ in Fall(Tune.Hero.FallGraceSeconds + Tune.Hero.FallRampSeconds + 0.6f, true)) yield return null;
        ScCheck($"braked just before the ground (a second jump, an updraft...), it costs nothing ({p.LastFallDamage:0.0}, hp {p.Hp:0})", p.LastFallDamage == 0f && p.Hp >= p.Stats.MaxHp - 0.01f);
    }
}
