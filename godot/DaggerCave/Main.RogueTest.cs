using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private float _rgMark, _rgMark2;
    private int _rgCount;
    private Vector2 _rgPos;

    /// <summary>--herotest --hero=rogue: the jabs, the throw, the recall, and vanishing.</summary>
    private void RogueStep(int s, Player p)
    {
        Enemy Dummy(Enemy e, float dx, bool hold = true, float dy = -4)
        {
            e.Position = p.GlobalPosition + new Vector2(_dir * dx, dy);
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.MaxHp = e.Hp = 3000;
            if (hold) e.Freeze(60f, hold: true);
            return e;
        }
        float Hp(Enemy e) => IsInstanceValid(e) ? e.Hp : 0;
        switch (s)
        {
            // ---- the jabs: quick, one creature at a time, now and then critical
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                p.Stats.CritChance = 0f;
                Check($"the Rogue slides down walls and kicks off them from the start (wall jump {p.Stats.WallJump})", p.Stats.WallJump);
                _probeEnemy = Dummy(new Goblin(), 14);
                _probe2 = Dummy(new Goblin(), 24);
                break;
            case 8:
                _hpMark = _probeEnemy.Hp; _hp2Mark = _probe2.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 9: _heroInput = default; break;
            case 12:
            {
                float a = _hpMark - Hp(_probeEnemy), b = _hp2Mark - Hp(_probe2), want = Tune.Rogue.Damage * p.Stats.DamageMult;
                Check($"a jab strikes the nearest creature for {want:0} and only it ({a:0.0} and {b:0.0})", Math.Abs(a - want) < 0.3f && b < 0.01f);
                _rgCount = p.AttacksStarted;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 22:
                Check($"held, the jabs come five a second ({p.AttacksStarted - _rgCount} in 1 s)", p.AttacksStarted - _rgCount >= 4);
                _heroInput = default;
                p.Stats.CritChance = 1f;
                break;
            case 26:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 27: _heroInput = default; break;
            case 30:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * Tune.Rogue.CritMult * p.Stats.DamageMult;
                Check($"a critical jab lands twice as hard ({a:0.0}, want {want:0.0}; crits {p.Crits})", Math.Abs(a - want) < 0.3f && p.Crits >= 1);
                p.Stats.CritChance = 0f;
                Take(p, "backstab");
                // its back to you
                _probeEnemy.FaceToward(_dir);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 31: _heroInput = default; break;
            case 34:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * Tune.Rogue.BackstabMult * p.Stats.DamageMult;
                Check($"Backstab: a jab in the back lands half again as hard ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.3f);
                _probeEnemy.FaceToward(-_dir);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 35: _heroInput = default; break;
            case 38:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * p.Stats.DamageMult;
                Check($"but not a jab to its face ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.3f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                break;
            }

            // ---- the throw: a dagger sticks in the creature it meets
            case 40:
                _probeEnemy = Dummy(new Golem(), 120, dy: -6);
                break;
            case 42:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 43: _heroInput = default; break;
            case 48:
            {
                var d = p.ThrownDaggerAt(1) ?? p.ThrownDaggerAt(0);
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.ThrowDamage * p.Stats.DamageMult;
                Check($"a thrown dagger strikes the golem 120 px away for {want:0} ({a:0.0}) and sticks in it ({d?.State}, {p.DaggersInHand} in hand)",
                    Math.Abs(a - want) < 0.3f && d != null && d.State == ThrownDagger.Phase.Stuck && p.DaggersInHand == 1);
                _rgCount = p.AttacksStarted;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(-_dir, 0) };
                break;
            }
            case 58:
                Check($"with one dagger out, the jabs come half as fast ({p.AttacksStarted - _rgCount} in 1 s)", p.AttacksStarted - _rgCount is >= 2 and <= 3);
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 59: _heroInput = default; break;
            case 64:
                Check($"the second sticks in it too, and with both out both come home by themselves ({p.DaggersInHand} in hand)", true);
                break;
            case 72:
                Check($"both daggers back in hand ({p.DaggersInHand})", p.DaggersInHand == 2);
                // a throw at nothing comes back by itself
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(-_dir, -0.4f).Normalized() };
                break;
            case 73:
                _heroInput = default;
                Check($"a throw at nothing is out ({p.DaggersInHand} in hand)", p.DaggersInHand == 1);
                break;
            case 90:
                Check($"and comes back by itself ({p.DaggersInHand} in hand)", p.DaggersInHand == 2);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- recall: the dagger tears back out through its creature, yanking it toward you
            case 95:
                _probeEnemy = Dummy(new Goblin(), 150, hold: false);
                break;
            case 97:
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 98: _heroInput = default; break;
            case 103:
            {
                var d = p.ThrownDaggerAt(1) ?? p.ThrownDaggerAt(0);
                Check($"(a dagger sticks in a goblin: {d?.State})", d != null && d.State == ThrownDagger.Phase.Stuck);
                _hpMark = _probeEnemy.Hp;
                _rgPos = _probeEnemy.GlobalPosition;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 104:
            {
                _heroInput = default;
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.RecallDamage * p.Stats.DamageMult;
                Check($"recall tears it back out through the goblin for {want:0} ({a:0.0})", Math.Abs(a - want) < 0.3f);
                break;
            }
            case 106:
            {
                float pulled = (_rgPos.X - _probeEnemy.GlobalPosition.X) * _dir;
                Check($"yanking the goblin toward you ({pulled:0} px in 0.3 s)", pulled > 12);
                break;
            }
            case 112:
                Check($"and the dagger comes home ({p.DaggersInHand} in hand)", p.DaggersInHand == 2);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- vanish: the creatures lose you, and you're quick
            case 115:
                _probeEnemy = Dummy(new Goblin(), 110, hold: false);
                break;
            case 120:
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 121:
                _heroInput = default;
                Check($"the dodge button vanishes (hidden {p.Hidden}, {p.VanishLeft:0.0} s)", p.Hidden && p.Vanished);
                break;
            case 124:
                Check($"and the goblin loses you (lost track {_probeEnemy.LostTrack})", _probeEnemy.LostTrack);
                _heroInput = new PlayerInput { Move = new Vector2(-_dir, 0) };
                break;
            case 130:
            {
                float want = Tune.Hero.RunSpeed * p.Stats.MoveSpeed;
                Check($"half again as fast in the shadows ({Math.Abs(p.Velocity.X):0} px/s, running {want:0})", Math.Abs(p.Velocity.X) > want * 1.35f);
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(-_dir, 0) };
                break;
            }
            case 131:
                _heroInput = default;
                Check($"striking brings you out of the shadows (hidden {p.Hidden})", !p.Hidden);
                // being struck does too
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 132:
                _heroInput = default;
                p.Hurt(5, p.GlobalPosition + new Vector2(_dir * 20, 0));
                Check($"and so does being struck (hidden {p.Hidden})", !p.Hidden);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- Surprise Attack: the strike out of the shadows lands four times as hard
            case 140:
                Take(p, "surprise");
                _probeEnemy = Dummy(new Goblin(), 14);
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 141: _heroInput = default; break;
            case 144:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 145: _heroInput = default; break;
            case 148:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * Tune.Rogue.SurpriseMult * p.Stats.DamageMult;
                Check($"Surprise Attack: the jab out of the shadows lands four times as hard ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.5f && p.Surprises == 1);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 149: _heroInput = default; break;
            case 152:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * p.Stats.DamageMult;
                Check($"and only that one ({a:0.0})", Math.Abs(a - want) < 0.5f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                Finish();
                break;
            }
        }
    }
}
