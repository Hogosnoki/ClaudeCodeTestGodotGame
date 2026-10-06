using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// --scenario=shifterforms --hero=shifter [--shots=DIR]: the Shape Shifter becomes each of the twelve creatures in turn. For
/// each: it walks when the stick is pushed (its own gait, its own speed), and the attack button sets the creature attacking:
/// it winds up first, as that creature does, and only then does the blow land on a creature held still before it.
/// </summary>
public partial class Main
{
    private int _sfForm = -1;
    private int _sfPhase;
    private float _sfX0, _sfWindAt, _sfHitAt, _sfHp0;
    private Enemy _sfFoe;
    private Vector2 _sfHome;
    private string _sfWalkClip = "";

    private void ShifterFormsScenario()
    {
        var p = G.Player;
        float dt = (float)GetProcessDeltaTime();
        if (_sfForm < 0)
        {
            if (_scT < 0.6f) return;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.Stats.MaxHp = 5000; p.Hp = 5000;
            _sfHome = p.GlobalPosition;
            _sfForm = 0; _sfPhase = 0; _scT = 0;
            return;
        }
        if (_sfForm >= ShiftForm.All.Length) { ScEnd(); return; }
        var f = ShiftForm.All[_sfForm];
        switch (_sfPhase)
        {
            case 0: // become the creature
            {
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                if (p.Shifted) p.LeaveForm(false);
                p.GlobalPosition = _sfHome; p.Velocity = Vector2.Zero;
                _scInput = default;
                p.EnterForm(f);
                var g = p.Ghost;
                ScCheck($"{f.Name}: the Shape Shifter is a real {f.Name} now ({g?.GetType().Name ?? "none"})", g != null && g.GetType().Name.Equals(f.Name == "Crab" ? "Crab" : f.Name, StringComparison.OrdinalIgnoreCase));
                _sfPhase = 1; _scT = 0;
                break;
            }
            case 1: // settle, then push the stick right
            {
                if (_scT < 0.8f) return;
                _sfX0 = p.GlobalPosition.X;
                _scInput = new PlayerInput { Move = new Vector2(1, 0) };
                _sfPhase = 2; _scT = 0; _sfWalkClip = "";
                break;
            }
            case 2: // walking
            {
                if (_scT > 0.5f && _sfWalkClip == "") _sfWalkClip = p.Ghost?.Animator?.Current ?? "";
                if (_scT < 2.2f) return;
                float moved = p.GlobalPosition.X - _sfX0;
                _scInput = default;
                ScCheck($"{f.Name}: pushing the stick right moves it ({moved:0} px in 2 s, its own gait '{_sfWalkClip}')", moved > 25f && _sfWalkClip != "idle" && _sfWalkClip != "");
                _sfPhase = 3; _scT = 0;
                break;
            }
            case 3: // a foe held still ahead of it; hold the attack
            {
                if (_scT < 0.6f) return;
                var at = p.GlobalPosition + new Vector2(f.Flier ? 46 : 38, f.Flier ? 0 : 0);
                _sfFoe = new Golem { Position = at };
                _sfFoe.SetMeta("test", true);
                _world.AddChild(_sfFoe);
                _sfFoe.MaxHp = _sfFoe.Hp = 100000;
                _sfFoe.Freeze(99f, hold: true);
                _sfHp0 = _sfFoe.Hp;
                _sfWindAt = _sfHitAt = -1f;
                _scInput = new PlayerInput { Attack = true, AttackHeld = true, Aim = new Vector2(1, 0) };
                _sfPhase = 4; _scT = 0;
                break;
            }
            case 4: // the wind-up, then the blow
            {
                _sfFoe.Freeze(99f, hold: true);
                // a player aims at what it wants to hit
                _scInput = new PlayerInput { Attack = true, AttackHeld = true, Aim = (_sfFoe.GlobalPosition - p.GlobalPosition).Normalized() };
                if (_sfWindAt < 0 && p.Ghost != null && (p.Ghost.Attacking || p.Ghost.MidAction)) { _sfWindAt = _scT; ScShot($"form_{f.Key}_windup"); }
                if (_sfHitAt < 0 && _sfFoe.Hp < _sfHp0) { _sfHitAt = _scT; ScShot($"form_{f.Key}_hit"); }
                if (_sfHitAt >= 0 && _scT > _sfHitAt + 0.15f || _scT > 4f)
                {
                    float dealt = _sfHp0 - _sfFoe.Hp;
                    ScCheck($"{f.Name}: the attack winds up first ({_sfWindAt:0.00} s) and the blow lands after ({_sfHitAt:0.00} s, {dealt:0.0} damage)", _sfWindAt >= 0f && _sfHitAt > _sfWindAt + 0.04f && dealt > 0f);
                    _scInput = default;
                    _sfFoe.QueueFree();
                    _sfForm++; _sfPhase = 0; _scT = 0;
                }
                break;
            }
        }
    }
}
