using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _dsSteps;

    /// <summary>`--scenario=dashslash --hero=swordsman`: the dodge button is a dash-slash: it carries the Swordsman a good way, slashes the first creature it touches for one and a half times a strike, takes about 15 s to come back, and makes no one invulnerable.</summary>
    private void DashSlashScenario()
    {
        if (_dsSteps == null) { if (_scT < 0.8f) return; _dsSteps = DashSlashRun().GetEnumerator(); }
        if (!_dsSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> DashSlashRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Dodge = _scInput.Attack = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1f)) yield return null;
        ScCheck($"the dash-slash comes back in about 15 s ({Tune.Swordsman.DashSlashCooldown:0}; the dodge roll it replaced came back in {Tune.Hero.DodgeCooldown:0.##})", Math.Abs(p.DodgeCooldownTotal - (Tune.Swordsman.DashSlashTime + 15f)) < 0.5f);

        // ---- a dash at nothing: a good way across the floor, no invulnerability, and then it is spent
        var at = p.GlobalPosition;
        _scInput = new PlayerInput { Dodge = true, Move = new Vector2(p.Facing, 0) };
        yield return null; yield return null;
        bool invulnerable = p.Invulnerable;
        _scInput = default;
        foreach (var _ in Wait(0.5f)) yield return null;
        float went = Math.Abs(p.GlobalPosition.X - at.X);
        ScCheck($"it carries the Swordsman a good way ({went:0} px) and slashes nothing ({p.DashSlashes})", went > 90f && p.DashSlashes == 0);
        ScCheck($"it dodges nothing of itself: he is not invulnerable in it ({invulnerable})", !invulnerable);
        float left = p.DodgeCooldowns.Max();
        ScCheck($"and the next is a long way off ({left:0.0} s to go)", left > 13f);
        int d0 = p.DashSlashes;
        _scInput = new PlayerInput { Dodge = true, Move = new Vector2(p.Facing, 0) };
        yield return null; yield return null; _scInput = default;
        ScCheck($"a second press does nothing until then ({(p.IsDodging ? "dashing" : "not dashing")})", !p.IsDodging);

        // ---- a dash that meets a creature: it slashes, for 1.5x a strike
        p.ResetAbilityCooldowns();
        var foe = new Spider { Position = p.GlobalPosition + new Vector2(110 * p.Facing, 0) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        foe.Wake(); foe.ForceGround();
        foe.Hp = foe.MaxHp = 1000000f;
        foe.Freeze(60f, hold: true);
        foreach (var _ in Wait(0.4f)) yield return null;
        foe.GlobalPosition = p.GlobalPosition + new Vector2(110 * p.Facing, 0);
        // (a plain swing at it, for what a strike does)
        float h0 = foe.Hp; int runs = p.HitPausesRun;
        p.GlobalPosition = foe.GlobalPosition - new Vector2(16 * p.Facing, 0);
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true };
        for (int f = 0; f < 120 && p.HitPausesRun == runs; f++) { yield return null; _scInput = default; }
        foreach (var _ in Wait(1.0f)) yield return null;
        float strike = h0 - foe.Hp;
        // (and the dash from further off)
        foe.GlobalPosition = p.GlobalPosition + new Vector2(110 * p.Facing, 0);
        foe.Freeze(60f, hold: true);
        foreach (var _ in Wait(0.5f)) yield return null;
        float h1 = foe.Hp; float dir0 = p.Facing; float startX0 = p.GlobalPosition.X, foeX0 = foe.GlobalPosition.X;
        _scInput = new PlayerInput { Dodge = true, Move = new Vector2(p.Facing, 0) };
        yield return null; _scInput = default;
        for (int f = 0; f < 60 && p.DashSlashes == d0; f++) yield return null;
        float faced = p.Facing;
        foreach (var _ in Wait(1.5f)) yield return null;
        float slash = h1 - foe.Hp;
        float side = Math.Sign(p.GlobalPosition.X - foe.GlobalPosition.X);
        ScCheck($"the dash went through the creature and slashed it from behind ({p.DashSlashes - d0}; he stands beyond it ({p.GlobalPosition.X - foe.GlobalPosition.X:0} px) facing back ({faced}), dash over {!p.IsDodging})", p.DashSlashes == d0 + 1 && !p.IsDodging && side == dir0 && faced == -dir0);
        ScCheck($"for one and a half times a strike ({slash:0.0} against a strike's {strike:0.0}: x{slash / Math.Max(0.01f, strike):0.00})", slash > strike * 1.25f && slash < strike * 1.75f);
    }
}
