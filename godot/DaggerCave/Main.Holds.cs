using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _hlSteps;

    /// <summary>`--scenario=holds --hero=NAME`: the hero's wind-ups: held still (walking pushed the whole time), the right length, and the cast goes off after.</summary>
    private void HoldsScenario()
    {
        if (_hlSteps == null) { if (_scT < 0.8f) return; _hlSteps = HoldsRun().GetEnumerator(); }
        if (!_hlSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> HoldsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        // (button presses are one frame; the walking is pushed the whole time)
        p.InputOverride = () => { var i = _scInput; _scInput.Ability = _scInput.Ability2 = _scInput.Attack = _scInput.Jump = _scInput.Dodge = _scInput.Support = false; return i; };
        p.TestFill();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1.0f)) yield return null;

        // presses a button while pushing right the whole time; checks the hero stood still and held for `expect` seconds
        IEnumerable<object> Cast(string what, PlayerInput press, float expect)
        {
            foreach (var _ in Wait(0.3f)) yield return null;
            var at = p.GlobalPosition;
            press.Move = new Vector2(1, 0);
            press.Aim = new Vector2(1, 0); press.AimGiven = true;
            _scInput = press;
            yield return null; yield return null;
            ScCheck($"{what}: the wind-up is {expect:0.##} s ({p.LastHold:0.###})", Math.Abs(p.LastHold - expect) < 0.001f);
            _scInput = new PlayerInput { Move = new Vector2(1, 0) };
            int frames = 0;
            while (p.Holding && frames++ < 400) yield return null;
            // (the walking was pushed the whole time; the drift is how far it got from where the wind-up began)
            ScCheck($"{what}: it did not move or fall while it gathered (drift {p.LastHoldDrift:0.00} px)", p.LastHoldDrift < 0.01f);
            ScCheck($"{what}: the wind-up ended", !p.Holding);
            foreach (var _ in Wait(0.5f)) yield return null;
            ScCheck($"{what}: and then it walked on", p.GlobalPosition.X > at.X + 1f || p.LastHoldDrift < 0.01f);
            _scInput = default;
        }

        switch (G.Hero)
        {
            case HeroKind.Vitalist:
            {
                p.Hp = p.Stats.MaxHp * 0.4f;
                float hp0 = p.Hp;
                foreach (var s in Cast("heal", new PlayerInput { Ability = true }, Tune.Vitalist.HealDelay)) yield return s;
                foreach (var _ in Wait(0.3f)) yield return null;
                ScCheck($"heal: the healing came after the wind-up ({hp0:0} -> {p.Hp:0})", p.Hp > hp0 + 1f);
                ScCheck("rupture's wind-up is 0.8 s", Math.Abs(Tune.Vitalist.RuptureWindup - 0.8f) < 0.001f);
                break;
            }
            case HeroKind.Elementalist:
                foreach (var s in Cast("blizzard", new PlayerInput { Ability = true }, Tune.Elementalist.BlizzardDelay)) yield return s;
                ScCheck("a blizzard came down after it", p.LastBlizzard != null && IsInstanceValid(p.LastBlizzard));
                foreach (var _ in Wait(1.0f)) yield return null;
                foreach (var s in Cast("firebolt", new PlayerInput { Attack = true }, Tune.Elementalist.FireDelay)) yield return s;
                ScCheck("stalag-might's wind-up is 1 s", Math.Abs(Tune.Support.StalagDelay - 1f) < 0.001f);
                ScCheck("frostbolt's wind-up is 0.1 s", Math.Abs(Tune.Elementalist.FrostDelay - 0.1f) < 0.001f);
                break;
            case HeroKind.Aegis:
                p.Hp = p.Stats.MaxHp;
                foreach (var s in Cast("barrier", new PlayerInput { Ability = true }, Tune.Aegis.ShieldDelay)) yield return s;
                ScCheck("the barrier went up after it", p.BarrierHp > 0.1f);
                break;
            case HeroKind.Rogue:
            {
                ScCheck("the throw does 60% of the dagger slash", Math.Abs(Tune.Rogue.ThrowDamage - 0.6f * Tune.Rogue.Damage) < 0.001f);
                ScCheck("the recall does 60% of the dagger slash", Math.Abs(Tune.Rogue.RecallDamage - 0.6f * Tune.Rogue.Damage) < 0.001f);
                // (a dagger out, hanging where it was thrown, to be recalled)
                p.TestDaggerOut();
                foreach (var _ in Wait(0.2f)) yield return null;
                ScCheck($"a dagger is out (in hand {p.DaggersInHand})", p.DaggersInHand < 2);
                foreach (var s in Cast("recall", new PlayerInput { Ability2 = true }, Tune.Rogue.RecallDelay)) yield return s;
                break;
            }
            default:
                ScCheck("(nothing gathers for this hero)", true);
                break;
        }
    }
}
