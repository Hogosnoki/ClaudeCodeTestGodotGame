using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private float _elMark;
    private int _elCount;
    private Updraft _elUpdraft;

    /// <summary>--herotest --hero=elementalist: the bolts, the aether, the updraft, the blizzard and the snap.</summary>
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
                p.SetAether(p.Stats.AetherMax);
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
                Check($"it costs no aether ({p.Aether:0} of {p.Stats.AetherMax:0})", p.Aether >= p.Stats.AetherMax - 0.01f);
                Check($"and with no luck, sets nothing alight (burning {_probeEnemy.Ignited})", !_probeEnemy.Ignited);
                // (now it always catches)
                p.Stats.IgniteChance = 1f;
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
                // aether comes back by itself
                p.SetAether(10);
                break;
            case 63:
            {
                float want = 10 + Tune.Elementalist.AetherRegen * 2f;
                Check($"aether comes back by itself ({p.Aether:0.0} after 2 s, want {want:0.0})", Math.Abs(p.Aether - want) < 0.6f);
                break;
            }

            // ---- the updraft: a column of air that carries you up it
            case 70:
                p.SetAether(p.Stats.AetherMax);
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 71:
            {
                _heroInput = default;
                var u = p.LastUpdraft;
                Check($"the dodge button raises an updraft at your feet for {Tune.Elementalist.UpdraftCost:0} aether ({p.Aether:0} left, column {(u != null ? $"{u.Height:0} px tall" : "none")})",
                    u != null && IsInstanceValid(u) && Math.Abs(p.Aether - (p.Stats.AetherMax - Tune.Elementalist.UpdraftCost)) < 0.3f && Math.Abs(u.GlobalPosition.X - p.GlobalPosition.X) < 4);
                break;
            }
            case 82:
            {
                var u = p.LastUpdraft;
                float rose = _posMark.Y - p.GlobalPosition.Y;
                Check($"it carries you up ({rose:0} px in 1.1 s)", rose > 40);
                break;
            }
            case 92:
            {
                var u = p.LastUpdraft;
                bool held = u != null && IsInstanceValid(u) && Math.Abs(p.GlobalPosition.Y - u.TopY) < 12;
                Check($"and holds you at its top ({(u != null ? $"{p.GlobalPosition.Y - u.TopY:0}" : "?")} px from it, falling {p.Velocity.Y:0})", held && Math.Abs(p.Velocity.Y) < 60);
                // a push down lets you sink out of it
                _heroInput = new PlayerInput { Move = new Vector2(0, 1) };
                break;
            }
            case 112:
                _heroInput = default;
                Check($"a push down lets you sink back to the ground (on the floor {p.IsOnFloor()})", p.IsOnFloor());
                // (the column goes, so it doesn't carry you up again during what follows)
                if (IsInstanceValid(p.LastUpdraft)) p.LastUpdraft.QueueFree();
                break;
            case 114:
                // no aether, no updraft
                p.SetAether(5);
                _elUpdraft = p.LastUpdraft;
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 115:
                _heroInput = default;
                Check($"without the aether there's no updraft (a new one {p.LastUpdraft != _elUpdraft}, {p.Aether:0.00} aether)", p.LastUpdraft == _elUpdraft && p.Aether > 4.99f && p.Aether < 5.6f);
                break;

            // ---- the blizzard: strikes on everything in it, and frost that can freeze
            case 150:
                // (4 s on, the golem's fire is long out)
                p.SetAether(p.Stats.AetherMax);
                p.ResetAbilityCooldowns();
                p.Stats.FreezeBonus = -1f;
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 151:
            {
                _heroInput = default;
                var z = p.LastBlizzard;
                Check($"the ability button calls a blizzard down on the golem for {Tune.Elementalist.BlizzardCost:0} aether ({p.Aether:0} left, {(z != null ? $"{z.GlobalPosition.DistanceTo(_probeEnemy.GlobalPosition):0} px from it" : "none")})",
                    z != null && Math.Abs(p.Aether - (p.Stats.AetherMax - Tune.Elementalist.BlizzardCost)) < 0.3f && z.GlobalPosition.DistanceTo(_probeEnemy.GlobalPosition) < 8);
                break;
            }
            case 186:
            {
                float dealt = _hpMark - _probeEnemy.Hp, want = Tune.Elementalist.BlizzardTicks * Tune.Elementalist.BlizzardDamage * p.Stats.DamageMult;
                Check($"it strikes {Tune.Elementalist.BlizzardTicks} times for {Tune.Elementalist.BlizzardDamage:0} over {Tune.Elementalist.BlizzardSeconds:0} s ({dealt:0.0}, want {want:0.0})", Math.Abs(dealt - want) < 0.6f);
                Check($"and waits out its cooldown (ready {p.AbilityChargeReady}, {p.AbilityCooldownFrac:0.00})", !p.AbilityChargeReady);
                // (now every strike freezes)
                p.Stats.FreezeBonus = 1f;
                p.ResetAbilityCooldowns();
                p.SetAether(p.Stats.AetherMax);
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
                p.SetAether(p.Stats.AetherMax);
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
                float left = p.Stats.AetherMax - Tune.Elementalist.SnapCost;
                Check($"for {Tune.Elementalist.SnapCost:0} aether ({p.Aether:0.0} of {p.Stats.AetherMax:0})", p.Aether > left - 0.1f && p.Aether < left + 2f);
                _elMark = p.Aether;
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 227: _heroInput = default; break;
            case 234:
                Check($"with nothing frozen, a snap does nothing and costs nothing ({_elMark:0.0} -> {p.Aether:0.0} aether, golem hp {_hpMark:0} -> {_probeEnemy.Hp:0})",
                    Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f && p.Aether >= _elMark - 0.01f);
                // an elite never freezes solid
                _probeEnemy.Elite = true;
                Check($"a mini-boss (an elite) can't be frozen solid (froze {_probeEnemy.FreezeSolid(2f)})", !_probeEnemy.FrozenSolid);
                _probeEnemy.Elite = false;
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                Finish();
                break;
        }
    }
}
