using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _struckSteps;

    /// <summary>`--scenario=struck`: Cinder Brand (a boss or mini-boss that strikes you may be set alight, 20%) and Hoarfrost Mail (a regular creature that does may be frozen solid, 15%).</summary>
    private void StruckScenario()
    {
        if (_struckSteps == null) { if (_scT < 0.8f) return; _struckSteps = StruckRun().GetEnumerator(); }
        if (!_struckSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> StruckRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 9000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1f)) yield return null;
        var fire = Upgrades.Relics.First(r => r.Id == "relic_struck_fire");
        var frost = Upgrades.Relics.First(r => r.Id == "relic_struck_frost");
        fire.Apply(p.Stats, p); frost.Apply(p.Stats, p);
        ScCheck($"the relics' chances are 20% and 15% ({p.Stats.StruckIgnite:0.##}, {p.Stats.StruckFreeze:0.##})", Math.Abs(p.Stats.StruckIgnite - 0.2f) < 0.001f && Math.Abs(p.Stats.StruckFreeze - 0.15f) < 0.001f);
        float was1 = p.Stats.StruckIgnite, was2 = p.Stats.StruckFreeze;
        // (the chance made certain, to see what each does and to whom)
        p.Stats.StruckIgnite = p.Stats.StruckFreeze = 1f;

        Enemy Make(bool elite)
        {
            var g = new Goblin { Position = p.GlobalPosition + new Vector2(30 * p.Facing, 0), Elite = elite };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Wake();
            return g;
        }
        void Strike(Enemy e) { p.ClearInvuln(); p.Hurt(5f, e.GlobalPosition, 100f, e); }

        var plain = Make(false);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(plain);
        yield return null;
        ScCheck($"a regular creature that strikes you is frozen solid, and not set alight (frozen {plain.FrozenSolid}, burning {plain.Ignited}, {p.StruckFroze} froze)", plain.FrozenSolid && !plain.Ignited && p.StruckFroze == 1);

        var big = Make(true);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(big);
        yield return null;
        ScCheck($"a mini-boss that strikes you is set alight, and not frozen (burning {big.Ignited}, frozen {big.FrozenSolid}, {p.StruckIgnited} lit)", big.Ignited && !big.FrozenSolid && p.StruckIgnited == 1);

        // with the chance at nothing, neither happens
        p.Stats.StruckIgnite = p.Stats.StruckFreeze = 0f;
        var again = Make(false);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(again);
        yield return null;
        ScCheck($"without the relics nothing happens ({again.FrozenSolid})", !again.FrozenSolid);
        p.Stats.StruckIgnite = was1; p.Stats.StruckFreeze = was2;
    }
}
