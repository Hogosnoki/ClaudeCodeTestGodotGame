using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>--scenario=shifter --hero=shifter: the Shape Shifter copies a goblin, fights with its attacks, uses its special and drops the form.</summary>
public partial class Main
{
    // (one frame, like a real button press: the dual-state abilities toggle)
    private void Tap(PlayerInput i) { _scInput = i; _scPulse = 0.001f; }

    private Enemy _scGob;
    private int _scShot;
    private bool _scEntered;
    private float _scGobHp;

    private void ShifterScenario()
    {
        var p = G.Player;
        if (_scPulse > 0 && (_scPulse -= (float)GetProcessDeltaTime()) <= 0) { _scPulse = 0; _scInput = default; }
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.5f) return;
                ScCheck($"the Shape Shifter is the hero ({p.Stats.Hero}), {p.Stats.MaxHp:0} health", p.Stats.Hero == HeroKind.ShapeShifter);
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                p.Stats.MaxHp = 500; p.Hp = 500;
                Vector2 Floor(float dx) => G.Cave.FindFloor(p.GlobalPosition + new Vector2(dx, -40), 200, out var f) ? f : p.GlobalPosition + new Vector2(dx, 12);
                _scGob = new Goblin { Position = Floor(40) + new Vector2(0, -20) };
                _scGob.SetMeta("test", true);
                _world.AddChild(_scGob);
                _scGob.MaxHp = _scGob.Hp = 900; _scGob.Freeze(99f, hold: true);
                _scGobHp = _scGob.Hp;
                // a swing of the staff, unshifted: weak
                Pulse(new PlayerInput { Attack = true, Aim = new Vector2(1, 0) });
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
            {
                if (_scT < 0.9f) return;
                float staff = _scGobHp - _scGob.Hp;
                ScCheck($"the staff hits for little ({staff:0.0})", staff > 0 && staff < Tune.Shifter.StaffDamage * 2.2f);
                // shift into the goblin
                p.TestShift();
                _scStep = 2; _scT = 0;
                break;
            }
            case 2:
            {
                if (_scT < 0.5f) return;
                ScCheck($"Shift copies the goblin ({p.Form?.Name ?? "nothing"})", p.Form != null && p.Form.Key == "goblin");
                ScCheck("its model is the creature's (shown in a form)", p.Shifted);
                _scGobHp = _scGob.Hp;
                _scGob.Freeze(99f, hold: true);
                Pulse(new PlayerInput { Attack = true, Aim = new Vector2(1, 0) });
                _scStep = 3; _scT = 0;
                break;
            }
            case 3:
            {
                if (_scT < 0.9f) return;
                float hit = _scGobHp - _scGob.Hp;
                float want = Tune.Shifter.FormDamage * p.Form.Dmg;
                ScCheck($"the form's attack hits ({hit:0.0}, want about {want:0.0})", Math.Abs(hit - want) < want * 0.3f);
                _scGobHp = _scGob.Hp;
                _scGob.Freeze(99f, hold: true);
                p.TestSpecial(new Vector2(1, 0));
                _scStep = 4; _scT = 0;
                break;
            }
            case 4:
            {
                if (_scT < 0.6f) return;
                float hit = _scGobHp - _scGob.Hp;
                float want = Tune.Shifter.FormDamage * p.Form.SpecDmg;
                ScCheck($"{p.Form.SpecialName} hits harder ({hit:0.0}, want about {want:0.0}) and goes on cooldown ({p.FormSpecialFrac:0.00})", Math.Abs(hit - want) < want * 0.3f && p.FormSpecialFrac > 0.5f);
                // drop the form
                p.TestShift();
                _scStep = 5; _scT = 0;
                break;
            }
            case 5:
            {
                if (_scT < 0.5f) return;
                ScCheck($"Shift again drops the form (shifted {p.Shifted}), Shift recharging ({p.AbilityCooldownFrac:0.00})", !p.Shifted && p.AbilityCooldownFrac > 0.5f);
                // every copyable creature has its form
                int missing = 0;
                foreach (var f in ShiftForm.All) if (Icons.Get(f.Icon) == null || Icons.Get(f.SpecIcon) == null) missing++;
                ScCheck($"every form has its icons ({ShiftForm.All.Length} forms, {missing} missing)", missing == 0);
                if (_shotDir != "") { foreach (var e in G.Enemies.ToArray()) e.QueueFree(); _scStep = 10; _scT = 0; _scShot = 0; break; }
                ScEnd();
                break;
            }
            case 10:
            {
                // (with --shots=DIR: a picture of each form, standing and mid-attack)
                int k = _scShot / 2;
                if (k >= ShiftForm.All.Length) { ScEnd(); break; }
                bool atk = _scShot % 2 == 1;
                if (!atk && !_scEntered) { _scEntered = true; p.LeaveForm(false); p.EnterForm(ShiftForm.All[k]); }
                if (atk && _scT < 0.02f) { p.Stats.AttackSpeed = 1f; }
                if (_scT > (atk ? 0.18f : 0.8f))
                {
                    GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/form_{ShiftForm.All[k].Key}_{(atk ? "atk" : "idle")}.png");
                    _scShot++; _scT = 0; if (_scShot % 2 == 0) _scEntered = false;
                    if (_scShot % 2 == 1) p.TestAttack();
                }
                break;
            }
        }
    }
}
