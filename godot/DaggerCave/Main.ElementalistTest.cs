using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private float _elMark;
    private int _elCount;
    private Updraft _elUpdraft;
    private float _elApex, _elFall;

    /// <summary>--herotest --hero=elementalist: the bolts (and their homing), the alimus, the updraft, the blizzard and the snap.</summary>
    private void ElementalistStep(int s, Player p)
    {
        Enemy Dummy(Enemy e, float dx, float dy = -6)
        {
            e.Position = p.GlobalPosition + new Vector2(_dir * dx, dy);
            e.SetMeta("test", true);
            _world.AddChild(e);
            return e;
        }
        switch (s)
        {
            // ---- a firebolt: its damage, and nothing more when it doesn't catch
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                p.SetAlimus(p.Stats.AlimusMax);
                p.Stats.IgniteChance = 0f;
                _probeEnemy = Dummy(new Golem(), 90);
                _probeEnemy.MaxHp = _probeEnemy.Hp = 3000;
                _probeEnemy.Freeze(60f, hold: true);
                break;
            case 8:
                _hpMark = _probeEnemy.Hp;
                _elCount = p.BoltsCast;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 9: _heroInput = default; break;
            case 15:
            {
                float dealt = _hpMark - _probeEnemy.Hp, want = Tune.Elementalist.FireDamage * p.Stats.DamageMult;
                Check($"a firebolt flies out and strikes the golem 90 px away for {want:0} ({dealt:0.0}; bolts {p.BoltsCast - _elCount})", p.BoltsCast == _elCount + 1 && Math.Abs(dealt - want) < 0.5f);
                Check($"it costs a little alimus, barely more than comes back ({Tune.Elementalist.FireCost / Tune.Elementalist.FireEvery:0.00} a second at the full rate of fire against {Tune.Elementalist.AlimusRegen:0.00} regenerated)",
                    Tune.Elementalist.FireCost / Tune.Elementalist.FireEvery is var spend && spend > Tune.Elementalist.AlimusRegen && spend < Tune.Elementalist.AlimusRegen * 1.25f);
                Check($"and with no luck, sets nothing alight (burning {_probeEnemy.Ignited})", !_probeEnemy.Ignited);
                // (now it always catches)
                p.Stats.IgniteChance = 1f;
                p.TestResetBolt();
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 16: _heroInput = default; break;
            case 21:
                Check($"a firebolt that catches sets the golem alight (burning {_probeEnemy.Ignited})", _probeEnemy.Ignited);
                _hpMark = _probeEnemy.Hp;
                break;
            case 31:
            {
                float burned = _hpMark - _probeEnemy.Hp, want = Tune.Elementalist.IgniteDps * p.Stats.DamageMult;
                Check($"and it burns for {want:0} a second ({burned:0.0} in 1 s)", Math.Abs(burned - want) < 0.8f);
                p.Stats.IgniteChance = 0f;
                // holding the attack keeps casting
                _elCount = p.BoltsCast;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 43:
                Check($"holding the attack keeps casting ({p.BoltsCast - _elCount} bolts in 1.2 s)", p.BoltsCast - _elCount >= 2);
                _heroInput = default;
                // alimus comes back by itself
                p.SetAlimus(10);
                break;
            case 63:
            {
                float want = 10 + Tune.Elementalist.AlimusRegen * 2f;
                Check($"alimus comes back by itself ({p.Alimus:0.0} after 2 s, want {want:0.0})", Math.Abs(p.Alimus - want) < 0.6f);
                break;
            }

            // ---- the updraft: a column of air that carries you up it
            case 70:
                p.SetAlimus(p.Stats.AlimusMax);
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 71:
            {
                _heroInput = default;
                var u = p.LastUpdraft;
                Check($"the dodge button raises an updraft at your feet for {Tune.Elementalist.UpdraftCost:0} alimus ({p.Alimus:0} left, column {(u != null ? $"{u.Height:0} px tall" : "none")})",
                    u != null && IsInstanceValid(u) && Math.Abs(p.Alimus - (p.Stats.AlimusMax - Tune.Elementalist.UpdraftCost)) < 0.3f && Math.Abs(u.GlobalPosition.X - p.GlobalPosition.X) < 4);
                break;
            }
            case 82:
                Check($"it lifts nobody: you're still on the ground ({_posMark.Y - p.GlobalPosition.Y:0} px up, on the floor {p.IsOnFloor()})",
                    p.IsOnFloor() && Math.Abs(_posMark.Y - p.GlobalPosition.Y) < 3);
                // a jump from inside it (held for its full height) goes far higher than a jump normally does
                _posMark = p.GlobalPosition;
                _elApex = _posMark.Y;
                _elFall = 0;
                _heroInput = new PlayerInput { Jump = true, JumpHeld = true };
                break;
            case 83:
                _heroInput = new PlayerInput { JumpHeld = true };
                break;
            case 90:
                _heroInput = default;
                break;
            case 106:
            {
                float rose = _posMark.Y - _elApex;
                // (a jump normally peaks at about 80 px; a ceiling can cut the column, and the jump, short)
                Check($"a jump in it floats far higher ({rose:0} px, where one on the ground peaks near 80)", rose > 105);
                break;
            }
            case 126:
            {
                var u = p.LastUpdraft;
                float cap = Tune.Hero.MaxFallSpeed * Tune.Elementalist.UpdraftFallMult;
                Check($"it drifts down at no more than {Tune.Elementalist.UpdraftFallMult:0.0} of the usual terminal speed (fastest {_elFall:0} px/s, limit {cap:0} of {Tune.Hero.MaxFallSpeed:0})", _elFall < cap + 15f);
                Check($"and lands you gently back on the ground (on the floor {p.IsOnFloor()})", p.IsOnFloor());
                // (the column goes, so it doesn't ease you during what follows)
                if (IsInstanceValid(u)) u.QueueFree();
                break;
            }
            default:
                if (s > 83 && s < 126)
                {
                    _elApex = Math.Min(_elApex, p.GlobalPosition.Y);
                    if (s > 100) _elFall = Math.Max(_elFall, p.Velocity.Y);
                }
                break;
            case 128:
                // no alimus, no updraft
                p.SetAlimus(5);
                _elUpdraft = p.LastUpdraft;
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 129:
                _heroInput = default;
                Check($"without the alimus there's no updraft (a new one {p.LastUpdraft != _elUpdraft}, {p.Alimus:0.00} alimus)", p.LastUpdraft == _elUpdraft && p.Alimus > 4.99f && p.Alimus < 5.6f);
                break;

            // ---- the blizzard: strikes on everything in it, and frost that can freeze
            case 150:
                // (4 s on, the golem's fire is long out)
                p.SetAlimus(p.Stats.AlimusMax);
                p.ResetAbilityCooldowns();
                p.Stats.FreezeBonus = -1f;
                p.Stats.BlizzardSecondsMult = 0.5f; // (the test waits out half a storm: a full one lasts twice as long)
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 151:
            {
                _heroInput = default;
                var z = p.LastBlizzard;
                Check($"the ability button calls a blizzard down on the golem for {Tune.Elementalist.BlizzardCost:0} alimus ({p.Alimus:0} left, {(z != null ? $"{z.GlobalPosition.DistanceTo(_probeEnemy.GlobalPosition):0} px from it" : "none")})",
                    z != null && Math.Abs(p.Alimus - (p.Stats.AlimusMax - Tune.Elementalist.BlizzardCost)) < 0.3f && z.GlobalPosition.DistanceTo(_probeEnemy.GlobalPosition) < 8);
                break;
            }
            case 186:
            {
                int ticks = Tune.Elementalist.BlizzardTicks / 2;
                float dealt = _hpMark - _probeEnemy.Hp, want = ticks * Tune.Elementalist.BlizzardDamage * p.Stats.DamageMult;
                Check($"it strikes {ticks} times for {Tune.Elementalist.BlizzardDamage:0} over {Tune.Elementalist.BlizzardSeconds / 2:0} s ({dealt:0.0}, want {want:0.0})", Math.Abs(dealt - want) < 0.6f);
                Check($"and waits out its cooldown (ready {p.AbilityChargeReady}, {p.AbilityCooldownFrac:0.00})", !p.AbilityChargeReady);
                // (now every strike freezes)
                p.Stats.FreezeBonus = 1f;
                p.ResetAbilityCooldowns();
                p.SetAlimus(p.Stats.AlimusMax);
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 187: _heroInput = default; break;
            case 193:
                Check($"its frost can freeze a regular creature solid (frozen {_probeEnemy.FrozenSolid})", _probeEnemy.FrozenSolid);
                break;

            // ---- the snap: every frozen creature in view shatters, hurting what's beside it
            // (once the second storm has blown itself out)
            case 220:
            {
                var gob = Dummy(new Goblin(), 90 + 30, -4);
                gob.Freeze(60f, hold: true);
                _probe2 = gob;
                p.SetAlimus(p.Stats.AlimusMax);
                _probeEnemy.FreezeSolid(10f);
                _hpMark = _probeEnemy.Hp;
                _hp2Mark = gob.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 221: _heroInput = default; break;
            case 226:
            {
                float dealt = _hpMark - _probeEnemy.Hp, splash = _hp2Mark - (IsInstanceValid(_probe2) ? _probe2.Hp : 0);
                float want = Tune.Elementalist.SnapDamage * p.Stats.DamageMult, wantSplash = Tune.Elementalist.SnapSplash * p.Stats.DamageMult;
                Check($"a snap shatters the frozen golem for {want:0} ({dealt:0.0}, frozen now {_probeEnemy.FrozenSolid})", Math.Abs(dealt - want) < 0.6f && !_probeEnemy.FrozenSolid);
                Check($"and the goblin beside it takes {wantSplash:0} ({splash:0.0})", Math.Abs(splash - wantSplash) < 0.6f);
                // (less what came back in the moment since)
                float left = p.Stats.AlimusMax - Tune.Elementalist.SnapCost;
                Check($"for {Tune.Elementalist.SnapCost:0} alimus ({p.Alimus:0.0} of {p.Stats.AlimusMax:0})", p.Alimus > left - 0.1f && p.Alimus < left + 2f);
                _elMark = p.Alimus;
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 227: _heroInput = default; break;
            case 234:
                Check($"with nothing frozen, a snap does nothing and costs nothing ({_elMark:0.0} -> {p.Alimus:0.0} alimus, golem hp {_hpMark:0} -> {_probeEnemy.Hp:0})",
                    Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f && p.Alimus >= _elMark - 0.01f);
                // an elite never freezes solid
                _probeEnemy.Elite = true;
                Check($"a mini-boss (an elite) can't be frozen solid (froze {_probeEnemy.FreezeSolid(2f)})", !_probeEnemy.FrozenSolid);
                _probeEnemy.Elite = false;
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                break;

            // ---- homing: a bolt loosed wide still bends after a creature, and never turns back on one behind it
            case 236:
            {
                var gob = Dummy(new Goblin(), 150, -6);
                gob.Freeze(60f, hold: true);
                gob.MaxHp = gob.Hp = 500;
                _probe2 = gob;
                _hp2Mark = gob.Hp;
                p.Stats.IgniteChance = 0f;
                // (loosed 32 degrees off the creature)
                var wide = new Vector2(_dir, 0).Rotated(Mathf.DegToRad(-32f) * _dir);
                G.Spawn(new ElementBolt { Position = p.GlobalPosition + new Vector2(0, -6) + wide * 6f, Dir = wide, Damage = Tune.Elementalist.FireDamage, Caster = p });
                break;
            }
            case 246:
            {
                float dealt = _hp2Mark - (IsInstanceValid(_probe2) ? _probe2.Hp : 0);
                Check($"a bolt loosed 32 degrees wide homes in on the creature 150 px away ({dealt:0.0} of {Tune.Elementalist.FireDamage:0} damage)", dealt > Tune.Elementalist.FireDamage - 0.5f);
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                break;
            }
            case 248:
            {
                // a creature behind the caster, and none ahead
                var gob = Dummy(new Goblin(), -90, -6);
                gob.Freeze(60f, hold: true);
                gob.MaxHp = gob.Hp = 500;
                _probe2 = gob;
                _hp2Mark = gob.Hp;
                G.Spawn(new ElementBolt { Position = p.GlobalPosition + new Vector2(_dir * 6f, -6), Dir = new Vector2(_dir, 0), Damage = Tune.Elementalist.FireDamage, Caster = p });
                break;
            }
            case 260:
            {
                float dealt = _hp2Mark - (IsInstanceValid(_probe2) ? _probe2.Hp : 0);
                Check($"but it never turns back on a creature behind it ({dealt:0.0} damage)", dealt < 0.01f);
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                Finish();
                break;
            }
        }
    }
}
