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
        if (IsInstanceValid(foe)) foe.QueueFree();
        foreach (var _ in Wait(0.5f)) yield return null;

        // ---- Hawk's Pursuit: the lunge doubles, and in the air a swing hovers the hero and pulls it after the creature in its way
        var relic = Upgrades.Relics.First(r => r.Id == "relic_lunge");
        ScCheck($"the relic is for the melee heroes ({string.Join(",", relic.For)})", relic.For != null && relic.For.Contains(HeroKind.Swordsman) && !relic.For.Contains(HeroKind.Elementalist));
        // (without it: a swing in the air stays where gravity puts it)
        p.GlobalPosition += new Vector2(0, -90); p.Velocity = Vector2.Zero;
        var bat = new Bat { Position = p.GlobalPosition + new Vector2(80 * p.Facing, -40) };
        bat.SetMeta("test", true);
        _world.AddChild(bat);
        bat.Wake(); bat.Freeze(99f, hold: true);
        foreach (var _ in Wait(0.3f)) yield return null;
        p.GlobalPosition = bat.GlobalPosition - new Vector2(80 * p.Facing, -40); p.Velocity = Vector2.Zero;
        int before = p.LastAerialLunge;
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, -0.5f).Normalized(), AimGiven = true };
        yield return null; yield return null; _scInput = default;
        ScCheck($"without the relic a swing in the air makes no pursuit ({p.LastAerialLunge})", p.LastAerialLunge == before);
        foreach (var _ in Wait(1.5f)) yield return null;

        relic.Apply(p.Stats, p);
        ScCheck($"with it the lunge is twice the strength ({p.Stats.LungeMult:0.#}x) and a hero who never lunged lunges at least {Tune.Relics.LungeMin:0} px/s", p.Stats.LungeMult >= 2f && p.Stats.AerialLunge);
        // (somewhere in open air: up and ahead, as far as the rock allows)
        Vector2 batAt = p.GlobalPosition + new Vector2(30 * p.Facing, -10);
        foreach (var off in new[] { new Vector2(90, -50), new Vector2(80, -30), new Vector2(70, -10), new Vector2(60, 0), new Vector2(80, 20), new Vector2(-80, -30) })
        {
            var cand = p.GlobalPosition + new Vector2(off.X * p.Facing, off.Y);
            if (!G.Cave.IsSolid(cand) && !G.Cave.IsSolid(cand + new Vector2(0, -8)) && G.Cave.LineClear(p.GlobalPosition + new Vector2(0, -3), cand)) { batAt = cand; break; }
        }
        bat.GlobalPosition = batAt; bat.Freeze(99f, hold: true);
        foreach (var _ in Wait(0.4f)) yield return null;
        var start = p.GlobalPosition; float d0 = start.DistanceTo(bat.GlobalPosition);
        // (up in the air on a jump's strength, the swing made as it goes)
        p.Velocity = new Vector2(0, -330f);
        float fall = 0f, minD = d0; float yAtSwing = p.GlobalPosition.Y; float apex = p.GlobalPosition.Y;
        _scInput = new PlayerInput { Attack = true, Aim = (batAt - p.GlobalPosition).Normalized(), AimGiven = true };
        for (int f = 0; f < 90; f++)
        {
            yield return null;
            _scInput = default;
            minD = Math.Min(minD, p.GlobalPosition.DistanceTo(bat.GlobalPosition));
            apex = Math.Min(apex, p.GlobalPosition.Y);
            if (p.LastAerialLunge == 2 && p.HoverDrop >= 0f && !p.IsOnFloor() && f > 10) { fall = p.HoverDrop; }
            if (p.LastAerialLunge == 2 && p.IsOnFloor()) break;
        }
        ScCheck($"a swing in the air pursues the creature in its way ({(p.LastAerialLunge == 2 ? "after it" : "not")}; {d0:0} px away, closest {minD:0})", p.LastAerialLunge == 2 && minD < d0 - 30f);
        ScCheck($"and it hung there a moment, not falling (sank {p.HoverDrop:0.0} px while it hovered)", p.HoverDrop >= 0f && p.HoverDrop < 3f);

        // ---- Echoing Blade: a blow that lands strikes again for half
        if (IsInstanceValid(bat)) bat.QueueFree();
        foreach (var _ in Wait(1.0f)) yield return null;
        var echo = Upgrades.Relics.First(r => r.Id == "relic_echo");
        echo.Apply(p.Stats, p);
        ScCheck($"the relic is for the melee heroes, 20% for half ({p.Stats.EchoStrike:0.##}, x{Tune.Relics.EchoMult:0.#})", Math.Abs(p.Stats.EchoStrike - 0.2f) < 0.001f && Math.Abs(Tune.Relics.EchoMult - 0.5f) < 0.001f && echo.For.Contains(HeroKind.Warden));
        p.Stats.EchoStrike = 0f;
        var dummy = new Spider { Position = p.GlobalPosition + new Vector2(14 * p.Facing, 0) };
        dummy.SetMeta("test", true);
        _world.AddChild(dummy);
        dummy.Wake(); dummy.ForceGround();
        dummy.Hp = dummy.MaxHp = 1000000f;
        foreach (var _ in Wait(0.3f)) yield return null;
        IEnumerable<object> Swing(Action<float> got)
        {
            dummy.GlobalPosition = p.GlobalPosition + new Vector2(14 * p.Facing, 0);
            float h0 = dummy.Hp; int runs = p.HitPausesRun;
            _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true };
            for (int f = 0; f < 120 && p.HitPausesRun == runs; f++) { yield return null; _scInput = default; }
            foreach (var _ in Wait(1.2f)) yield return null;
            got(h0 - dummy.Hp);
        }
        float plainCut = 0f, echoCut = 0f;
        foreach (var s in Swing(c => plainCut = c)) yield return s;
        p.Stats.EchoStrike = 1f;
        int ech0 = p.EchoStrikes;
        foreach (var s in Swing(c => echoCut = c)) yield return s;
        ScCheck($"with the chance made certain, the blow struck again ({p.EchoStrikes - ech0} echoes)", p.EchoStrikes == ech0 + 1);
        ScCheck($"for half as much again ({plainCut:0.0} plain, {echoCut:0.0} with the echo: x{echoCut / Math.Max(0.01f, plainCut):0.00})", echoCut > plainCut * 1.3f && echoCut < plainCut * 1.7f);
    }
}
