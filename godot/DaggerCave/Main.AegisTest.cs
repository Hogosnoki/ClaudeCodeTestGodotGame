using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>--scenario=aegis --hero=aegis: the Aegis's kit against a friend and a pair of creatures (see ScenarioTick).</summary>
public partial class Main
{
    private Player _scAlly;
    private Enemy _scFoe2;
    private float _scAegisHp, _scAllyHp, _scFoeHp2;

    // (held for a tenth of a second: a few physics frames, however fast the frames come)
    private void Pulse(PlayerInput i) { _scInput = i; _scPulse = 0.12f; }
    private float _scPulse;

    private void AegisScenario()
    {
        var p = G.Player;
        if (_scPulse > 0 && (_scPulse -= (float)GetProcessDeltaTime()) <= 0) { _scPulse = 0; _scInput = default; }
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.5f) return;
                Player.TestParty = true; p.Stats.AegisSupport = true;
                ScCheck($"the Aegis stands at {p.Stats.MaxHp:0} health, threat x{p.Stats.ThreatDist}, three abilities and a bolt", p.Stats.Hero == HeroKind.Aegis && Math.Abs(p.Stats.ThreatDist - 1.25f) < 0.001f);
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                Vector2 Floor(float dx) => G.Cave.FindFloor(p.GlobalPosition + new Vector2(dx, -40), 200, out var f) ? f : p.GlobalPosition + new Vector2(dx, 12);
                // a friend (a Swordsman) standing beside
                _scAlly = new Player { Stats = new PlayerStats(HeroKind.Swordsman), Position = Floor(50) + new Vector2(0, -13) };
                _scAlly.InputOverride = () => default;
                _scAlly.Stats.HurtInvuln = 0f;
                _world.AddChild(_scAlly);
                // two creatures side by side, held still
                _scFoe = new Golem { Position = Floor(150) + new Vector2(0, -20) };
                _scFoe.SetMeta("test", true);
                _world.AddChild(_scFoe);
                _scFoe2 = new Golem { Position = _scFoe.Position + new Vector2(24, 0) };
                _scFoe2.SetMeta("test", true);
                _world.AddChild(_scFoe2);
                foreach (var e in new[] { _scFoe, _scFoe2 }) { e.MaxHp = e.Hp = 900; e.Freeze(99f, hold: true); }
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
            {
                // ---- the ward bolt: one blow on the first creature, its burst on the second, both weakened, the Aegis mended
                if (_scT < 0.4f) return;
                p.Hp = p.Stats.MaxHp - 20f;
                _scAegisHp = p.Hp; _scHp = _scFoe.Hp; _scFoeHp2 = _scFoe2.Hp;
                Pulse(new PlayerInput { Attack = true, Aim = new Vector2(1, 0) });
                _scStep = 2; _scT = 0;
                break;
            }
            case 2:
            {
                if (_scT < 1.2f) return;
                float hit = _scHp - _scFoe.Hp, hit2 = _scFoeHp2 - _scFoe2.Hp;
                float want = Tune.Aegis.BoltDamage * p.Stats.DamageMult * Affinity.Mult(_scFoe.Element, DamageKind.Physical);
                ScCheck($"the bolt strikes the first creature ({hit:0.0}, want about {want:0.0})", Math.Abs(hit - want) < want * 0.25f);
                ScCheck($"its burst strikes the one beside it for {Tune.Aegis.BurstShare:0%} ({hit2:0.0})", Math.Abs(hit2 - want * Tune.Aegis.BurstShare) < want * 0.25f);
                ScCheck($"both deal less damage now ({_scFoe.Weakened}, {_scFoe2.Weakened})", _scFoe.Weakened && _scFoe2.Weakened);
                ScCheck($"the Aegis is mended by a share of the damage (hp {_scAegisHp:0.00} -> {p.Hp:0.00})", p.Hp > _scAegisHp + 0.3f && p.Hp < _scAegisHp + 3f);
                // ---- the Healing Ward side-grade: a glancing blow, and the burst mends the Aegis for the whole blow
                p.Stats.HealingWard = true;
                p.Hp = p.Stats.MaxHp - 30f;
                float h0 = p.Hp, f0 = _scFoe.Hp;
                var blob = new ElementBolt { Damage = 9f, Dir = new Vector2(1, 0) };
                p.WardBoltStruck(blob, _scFoe, _scFoe.GlobalPosition);
                blob.Free();
                ScCheck($"Healing Ward: the bolt only grazes ({f0 - _scFoe.Hp:0.0})", f0 - _scFoe.Hp < 9f * 0.5f);
                ScCheck($"...and mends the Aegis ({p.Hp - h0:0.0}, want about {9f * (1 + Tune.Aegis.BurstShare):0.0})", p.Hp - h0 > 9f * 0.9f);
                p.Stats.HealingWard = false;
                _scStep = 3; _scT = 0;
                break;
            }
            case 3:
            {
                // ---- a barrier for the friend, aimed at them: a share of THEIR health
                if (_scT < 0.3f) return;
                Pulse(new PlayerInput { Ability = true, Aim = new Vector2(1, 0) });
                _scStep = 4; _scT = 0;
                break;
            }
            case 4:
            {
                if (_scT < 0.3f) return;
                float want = Tune.Aegis.BarrierShare * _scAlly.Stats.MaxHp;
                ScCheck($"the friend wears a barrier of {_scAlly.BarrierHp:0} (a share, {want:0}, of their {_scAlly.Stats.MaxHp:0} health)", Math.Abs(_scAlly.BarrierHp - want) < 0.5f);
                ScCheck($"not the Aegis ({p.BarrierHp:0})", p.BarrierHp < 0.01f);
                _scStep = 5; _scT = 0;
                break;
            }
            case 5:
            {
                // ---- a bubble on the friend: a tenth of every blow until it has taken a tenth of their health, then it bursts (the barrier first spent: test the bubble alone)
                if (_scT < 0.2f) return;
                _scAlly.GiveBarrier(0.0001f, 0.01f); // (nothing: the barrier soaks what's left of the first blow)
                _scAlly.Hurt(_scAlly.BarrierHp + 1f, p.GlobalPosition, 0, null);
                _scAlly.Hp = _scAlly.Stats.MaxHp;
                Pulse(new PlayerInput { Dodge = true, Aim = new Vector2(1, 0) });
                _scStep = 6; _scT = 0;
                break;
            }
            case 6:
            {
                if (_scT < 0.4f) return;
                ScCheck($"the friend is in a bubble ({_scAlly.Bubbled}, absorbing {_scAlly.BubbleHp:0}; cooldown {p.BubbleCooldownFrac:0.00}, ally dead {_scAlly.Dead})", _scAlly.Bubbled && Math.Abs(_scAlly.BubbleHp - Tune.Aegis.BubbleShare * _scAlly.Stats.MaxHp) < 0.5f);
                _scAllyHp = _scAlly.Hp;
                _scAlly.Hurt(10f, p.GlobalPosition, 0, null);
                ScCheck($"a blow of 10 costs them nine tenths: {_scAllyHp - _scAlly.Hp:0.0} (bubble left {_scAlly.BubbleHp:0.0})", Math.Abs(_scAllyHp - _scAlly.Hp - 10f * (1f - Tune.Aegis.BubbleAbsorb)) < 0.6f);
                // blows until it bursts
                int n = 0;
                while (_scAlly.Bubbled && n++ < 80) { _scAlly.Hp = _scAlly.Stats.MaxHp; _scAlly.Hurt(20f, p.GlobalPosition, 0, null); }
                ScCheck($"after enough blows it bursts ({_scAlly.Bubbled}, {n} blows)", !_scAlly.Bubbled && n < 80);
                _scAlly.Hp = _scAlly.Stats.MaxHp;
                _scStep = 7; _scT = 0;
                break;
            }
            case 7:
            {
                // ---- a shared burden: the friend takes 80% of a blow, the Aegis 20%
                if (_scT < 0.3f) return;
                p.Hp = p.Stats.MaxHp;
                Pulse(new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0) });
                _scStep = 8; _scT = 0;
                break;
            }
            case 8:
            {
                if (_scT < 0.4f) return;
                ScCheck($"the friend carries a burden ({_scAlly.Burdened}) and the Aegis knows whose ({p.BurdenTarget == _scAlly})", _scAlly.Burdened && p.BurdenTarget == _scAlly);
                p.TestClearBubble(); _scAlly.TestClearBubble();
                _scAllyHp = _scAlly.Hp; _scAegisHp = p.Hp;
                _scAlly.Hurt(20f, p.GlobalPosition, 0, null);
                float friendLost = _scAllyHp - _scAlly.Hp, aegisLost = _scAegisHp - p.Hp, share = Tune.Aegis.BurdenShare;
                ScCheck($"a blow of 20: the friend loses {friendLost:0.0} (want {20 * (1 - share):0.0}), the Aegis {aegisLost:0.0} (want {20 * share:0.0})", Math.Abs(friendLost - 20 * (1 - share)) < 0.7f && Math.Abs(aegisLost - 20 * share) < 0.7f);
                _scStep = 9; _scT = 0;
                break;
            }
            case 9:
            {
                // ---- the hostile bubble (an alteration): on a creature, it takes half of each blow while the rest builds, then bursts
                if (_scT < 0.3f) return;
                p.TestResetCooldowns();
                p.Stats.HostileBubble = true;
                p.Stats.Stacks["bubble_hostile"] = 1;
                _scFoe.Hp = _scFoe.MaxHp; _scFoe2.Hp = _scFoe2.MaxHp;
                Pulse(new PlayerInput { Dodge = true, Aim = new Vector2(1, 0) });
                _scStep = 10; _scT = 0;
                break;
            }
            case 10:
            {
                if (_scT < 0.4f) return;
                Enemy warded = _scFoe.Warded ? _scFoe : _scFoe2.Warded ? _scFoe2 : null;
                ScCheck($"a creature is warded ({warded != null}; bubble cooldown {p.BubbleCooldownFrac:0.00}, foes at {_scFoe.GlobalPosition} / {_scFoe2.GlobalPosition}, me {p.GlobalPosition}, hostile {p.Stats.HostileBubble})", warded != null);
                if (warded == null) { ScEnd(); return; }
                var other = warded == _scFoe ? _scFoe2 : _scFoe;
                float before = warded.Hp, beforeOther = other.Hp;
                warded.Hurt(10f, Vector2.Zero, warded.GlobalPosition, DamageKind.Raw);
                ScCheck($"it takes half of a blow of 10 ({before - warded.Hp:0.0})", Math.Abs(before - warded.Hp - 5f) < 0.6f);
                int n = 0;
                float cap = Tune.Aegis.WardCap * p.Stats.DamageMult * G.DepthHp;
                float blast = Tune.Aegis.WardBlast * p.Stats.DamageMult * G.DepthHp;
                while (warded.Warded && n++ < 20) warded.Hurt(10f, Vector2.Zero, warded.GlobalPosition, DamageKind.Raw);
                float lost = before - warded.Hp;
                float expect = 5f * (n + 1) + blast;
                ScCheck($"the ward bursts after the blows build to {cap:0.0} ({n + 1} blows; it lost {lost:0}, want about {expect:0})", !warded.Warded && Math.Abs(lost - expect) < 8f);
                float splash = beforeOther - other.Hp;
                ScCheck($"the creature beside it takes the splash ({splash:0.0}, want about {Tune.Aegis.WardSplash * p.Stats.DamageMult * G.DepthHp:0.0})", splash > Tune.Aegis.WardSplash * 0.8f);
                // ---- alone (no party), the second ability is a smite: everything near is struck and weakened
                Player.TestParty = false;
                p.TestResetCooldowns();
                _scFoe.Hp = _scFoe.MaxHp; _scFoe2.Hp = _scFoe2.MaxHp;
                _scFoe.Position = p.GlobalPosition + new Vector2(60, 0); _scFoe2.Position = p.GlobalPosition + new Vector2(600, 0);
                _scAllyHp = _scFoe.Hp; _scAegisHp = _scFoe2.Hp;
                Pulse(new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0) });
                _scStep = 11; _scT = 0;
                break;
            }
            case 11:
            {
                if (_scT < 0.4f) return;
                float near = _scAllyHp - _scFoe.Hp, far = _scAegisHp - _scFoe2.Hp;
                float want = Tune.Aegis.SmiteDamage * p.Stats.DamageMult;
                ScCheck($"alone, her second ability smites: the creature beside her takes {near:0.0} (want about {want:0}), the far one {far:0.0}, and it is weakened ({_scFoe.Weakened})", near > want * 0.5f && far < 0.5f && _scFoe.Weakened);
                ScEnd();
                break;
            }
        }
    }
}
