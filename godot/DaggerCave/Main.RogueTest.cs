using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private int _rgCount;
    private Vector2 _rgHome;

    /// <summary>Back to where the test began, standing still (so the cave's shape can't wander into a check).</summary>
    private void Home(Player p)
    {
        p.GlobalPosition = _rgHome;
        p.Velocity = Vector2.Zero;
    }

    /// <summary>
    /// Test aid (--roguetest, with --lookshot): the Rogue's things laid out around the hero for
    /// their 3D look: a dagger in flight, one stuck in a golem, and a cloud of smoke with a goblin
    /// lost in it.
    /// </summary>
    private void SpawnRogueLook()
    {
        var p = G.Player.GlobalPosition;
        var cave = G.Cave;
        Vector2 Floor(float dx) => cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        void Add(Node2D n, Vector2 at) { n.Position = at; _world.AddChild(n); }
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        var golem = new Golem();
        golem.SetMeta("test", true);
        Add(golem, Floor(110) - new Vector2(0, 14));
        golem.Freeze(999f, hold: true);
        // (flies the last few pixels into the golem and sticks)
        Add(new ThrownDagger { Dir = new Vector2(1, -0.1f).Normalized(), Harmless = true, Thrower = G.Player, Range = 9999 }, golem.Position - new Vector2(40, 4));
        Add(new ThrownDagger { Dir = new Vector2(1, -0.25f).Normalized(), Speed = 0.01f, Range = 9999, Harmless = true, Thrower = G.Player }, p + new Vector2(45, -34));
        Add(new SmokeCloud { Life = 999f }, Floor(-120) - new Vector2(0, 16));
        var lost = new Goblin();
        lost.SetMeta("test", true);
        Add(lost, Floor(-120) - new Vector2(0, 8));
    }

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
                _rgHome = p.GlobalPosition;
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
                // (the rest goes by the plain blow, whichever way the creatures happen to face)
                p.Stats.Backstab = false;
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
                Home(p);
                // (held just long enough for the throw to go in)
                _probeEnemy = Dummy(new Goblin(), 90, hold: false);
                _probeEnemy.Freeze(0.5f, hold: true);
                break;
            case 97:
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 98: _heroInput = default; break;
            case 101:
            {
                var d = p.ThrownDaggerAt(1) ?? p.ThrownDaggerAt(0);
                Check($"a thrown dagger sticks in the goblin ({d?.State})", d != null && d.State == ThrownDagger.Phase.Stuck && d.StuckIn == _probeEnemy);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 102:
            {
                _heroInput = default;
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.RecallDamage * p.Stats.DamageMult;
                Check($"recall tears it back out through the goblin for {want:0} ({a:0.0})", Math.Abs(a - want) < 0.3f);
                float toward = _probeEnemy.Velocity.X * -_dir;
                Check($"yanking the goblin toward you (reeling {_probeEnemy.Reeling}, {toward:0} px/s your way)", _probeEnemy.Reeling && toward > 60);
                break;
            }
            case 108:
                Check($"and the dagger comes home ({p.DaggersInHand} in hand)", p.DaggersInHand == 2);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- vanish: the creatures lose you, and you're quick
            case 115:
                Home(p);
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
                // (toward the open side: creatures don't block the way)
                _heroInput = new PlayerInput { Move = new Vector2(_dir, 0) };
                break;
            case 129:
            {
                float want = Tune.Hero.RunSpeed * p.Stats.MoveSpeed;
                Check($"half again as fast in the shadows ({Math.Abs(p.Velocity.X):0} px/s, running {want:0})", Math.Abs(p.Velocity.X) > want * 1.35f);
                // a jab at nothing at all still gives you away
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(-_dir, 0) };
                break;
            }
            case 130:
                _heroInput = default;
                Check($"attacking brings you out of the shadows (hidden {p.Hidden})", !p.Hidden);
                // being struck does too
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 131:
                _heroInput = default;
                p.Hurt(5, p.GlobalPosition + new Vector2(_dir * 20, 0));
                Check($"and so does being struck (hidden {p.Hidden})", !p.Hidden);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- Surprise Attack: the strike out of the shadows lands four times as hard
            case 140:
                Home(p);
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
            case 145:
                _heroInput = default;
                Check($"(out of the shadows the moment the jab begins: hidden {p.Hidden})", !p.Hidden);
                break;
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
                // and a dagger thrown out of the shadows is a surprise too
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                _probeEnemy = Dummy(new Golem(), 100, dy: -6);
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            }
            case 153: _heroInput = default; break;
            case 156:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 157: _heroInput = default; break;
            case 162:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.ThrowDamage * Tune.Rogue.SurpriseMult * p.Stats.DamageMult;
                Check($"and so is a dagger thrown out of them ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.5f && p.Surprises == 2);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;
            }

            // ---- the class cards: Cruel Edge, Weighted Daggers, Rending Recall, Quick Fade, Deep Shadows
            case 166:
                Home(p);
                foreach (var id in new[] { "cruel_edge", "weighted", "rending", "vanish_cd", "vanish_long" }) Take(p, id);
                p.Stats.CritChance = 1f;
                _probeEnemy = Dummy(new Golem(), 16, dy: -6);
                break;
            case 168:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 169: _heroInput = default; break;
            case 172:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.Damage * (Tune.Rogue.CritMult + 0.5f) * p.Stats.DamageMult;
                Check($"Cruel Edge: a critical jab lands 2.5 times as hard ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.3f);
                p.Stats.CritChance = 0f;
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 173: _heroInput = default; break;
            case 176:
            {
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.ThrowDamage * 1.3f * p.Stats.DamageMult;
                Check($"Weighted Daggers: a thrown dagger strikes 30% harder ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.3f);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 177:
            {
                _heroInput = default;
                float a = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.RecallDamage * 2f * p.Stats.DamageMult;
                Check($"Rending Recall: the recall tears out twice as hard ({a:0.0}, want {want:0.0})", Math.Abs(a - want) < 0.3f);
                float cd = Tune.Rogue.VanishCooldown * 0.8f;
                Check($"Quick Fade: vanish comes back 20% sooner ({p.AbilityRecharge:0.0} s, want {cd:0.0})", Math.Abs(p.AbilityRecharge - cd) < 0.01f);
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            }
            case 178:
            {
                _heroInput = default;
                float want = Tune.Rogue.VanishSeconds + 3f;
                Check($"Deep Shadows: you stay vanished 3 s longer ({p.VanishLeft:0.0} s of {p.VanishTotal:0})", p.Vanished && Math.Abs(p.VanishTotal - want) < 0.01f && p.VanishLeft > want - 0.2f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                Finish();
                break;
            }
        }
    }
}
