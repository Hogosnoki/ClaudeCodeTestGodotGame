using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>--scenario=status: poison, burning, freezing and drowning, where they come from, and the spider's flip on a copy (see ScenarioTick).</summary>
public partial class Main
{
    private float _scHp0, _scX0, _scBreath0;
    private Spider _scSpider;

    private void StatusScenario()
    {
        var p = G.Player;
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.5f) return;
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                p.Stats.MaxHp = 500; p.Hp = 500;
                // ---- where they come from
                float chance = Tune.Status.CreaturePoisonChance, elem = Tune.Status.ElementalChance;
                Tune.Status.CreaturePoisonChance = 1f; Tune.Status.ElementalChance = 1f;
                G.Depth = 2;
                var frog = new Frog(); var spider = new Spider(); var frost = new FrostElemental(); var fire = new FireElemental(); var nature = new NatureElemental(); var water = new WaterElemental();
                foreach (var e in new Enemy[] { frog, spider, frost, fire, nature, water }) { e.SetMeta("test", true); _world.AddChild(e); e.Freeze(99f, hold: true); e.QueueRedraw(); }
                frog.StrikeStatus(p, 10f);
                ScCheck($"a frog doesn't poison at depth {G.Depth} ({p.Poisoned})", !p.Poisoned);
                G.Depth = Tune.Status.PoisonFromDepth;
                frog.StrikeStatus(p, 10f);
                ScCheck($"from depth {G.Depth} it can ({p.Poisoned}, colour {p.StatusInk})", p.Poisoned && p.StatusInk.G > 0.9f && p.StatusInk.R < 0.5f);
                p.TestClearStatus();
                spider.StrikeStatus(p, 10f);
                ScCheck($"so can a spider ({p.Poisoned})", p.Poisoned);
                p.TestClearStatus();
                nature.StrikeStatus(p, 10f);
                ScCheck($"the Nature Elemental poisons ({p.Poisoned})", p.Poisoned);
                p.TestClearStatus();
                fire.StrikeStatus(p, 10f);
                ScCheck($"the Fire Elemental burns ({p.Burning}, ink orange {p.StatusInk.R > 0.9f && p.StatusInk.B < 0.3f})", p.Burning && p.StatusInk.R > 0.9f && p.StatusInk.B < 0.3f);
                p.TestClearStatus();
                frost.StrikeStatus(p, 10f);
                ScCheck($"the Frost Elemental freezes ({p.Frozen}, ink pale blue {p.StatusInk.B > 0.9f && p.StatusInk.G > 0.8f})", p.Frozen && p.StatusInk.B > 0.9f && p.StatusInk.G > 0.8f);
                p.TestClearStatus();
                p.Breath = p.Stats.BreathMax;
                water.StrikeStatus(p, 10f);
                ScCheck($"the Water Elemental drowns: {p.Breath:0.0} of {p.Stats.BreathMax:0} s of air left ({p.Drowning})", p.Drowning && Math.Abs(p.Breath - (p.Stats.BreathMax - Tune.Status.DrownBreath)) < 0.1f);
                float before = p.Breath;
                p.AddBreath(3f);
                ScCheck($"and a bubble of air gives nothing meanwhile ({before:0.0} -> {p.Breath:0.0})", Math.Abs(p.Breath - before) < 0.01f);
                p.TestClearStatus();
                p.AddBreath(2f);
                ScCheck($"until it's over ({p.Breath:0.0})", p.Breath > before + 1.5f);
                Tune.Status.CreaturePoisonChance = chance; Tune.Status.ElementalChance = elem;
                foreach (var e in new Enemy[] { frog, spider, frost, fire, nature, water }) e.QueueFree();
                // ---- poison over time: the blow's damage again over its seconds
                p.Hp = 500; _scHp0 = 500;
                p.GivePoison(16f, 8f);
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
                if (_scT < 4f) return;
                ScCheck($"poison: half the time, half the damage ({_scHp0 - p.Hp:0.0} of 16, still poisoned {p.Poisoned})", Math.Abs(_scHp0 - p.Hp - 8f) < 1.2f && p.Poisoned);
                _scStep = 2; _scT = 0;
                break;
            case 2:
                if (_scT < 4.4f) return;
                ScCheck($"and all of it by the end ({_scHp0 - p.Hp:0.0} of 16, over {p.Poisoned})", Math.Abs(_scHp0 - p.Hp - 16f) < 1.5f && !p.Poisoned);
                p.Hp = 500; _scHp0 = 500;
                p.GiveBurn(12f, 5f);
                _scStep = 3; _scT = 0;
                break;
            case 3:
                if (_scT < 5.4f) return;
                ScCheck($"burning: {_scHp0 - p.Hp:0.0} of 12 over 5 s ({p.Burning})", Math.Abs(_scHp0 - p.Hp - 12f) < 1.5f && !p.Burning);
                // ---- frozen solid: told to run, it stays put
                _scX0 = p.GlobalPosition.X;
                _scInput = new PlayerInput { Move = new Vector2(1, 0) };
                p.GiveFrozen(2f);
                _scStep = 4; _scT = 0;
                break;
            case 4:
                if (_scT < 1.5f) return;
                ScCheck($"frozen, the hero stays put ({p.GlobalPosition.X - _scX0:0.0} px moved, frozen {p.Frozen})", Math.Abs(p.GlobalPosition.X - _scX0) < 3f && p.Frozen);
                _scStep = 5; _scT = 0;
                break;
            case 5:
                if (_scT < 1.0f) return;
                ScCheck($"then thawed, runs on ({p.GlobalPosition.X - _scX0:0.0} px, frozen {p.Frozen})", !p.Frozen && p.GlobalPosition.X - _scX0 > 10f);
                p.GiveFrozen(2f);
                ScCheck($"and can't be frozen again at once ({p.Frozen})", !p.Frozen);
                _scInput = default;
                // ---- a copy of a spider, on the ceiling, is upside down
                _scSpider = new Spider { Puppet = true, Position = p.GlobalPosition + new Vector2(60, -60) };
                _world.AddChild(_scSpider);
                _scSpider.TestSetState(0);
                _scStep = 6; _scT = 0;
                break;
            case 6:
                if (_scT < 0.4f) return;
                ScCheck($"a copy of a ceiling spider is upside down (scale y {_scSpider.Animator.Sprite.Scale.Y:0.00})", _scSpider.Animator.Sprite.Scale.Y < 0);
                _scSpider.TestSetState(4);
                _scStep = 7; _scT = 0;
                break;
            case 7:
                if (_scT < 0.4f) return;
                ScCheck($"and right way up on the ground (scale y {_scSpider.Animator.Sprite.Scale.Y:0.00})", _scSpider.Animator.Sprite.Scale.Y > 0);
                ScEnd();
                break;
        }
    }
}
