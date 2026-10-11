using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _mtSteps;

    /// <summary>
    /// `--scenario=metrics [--hero=NAME]`: the run's record: swinging at creatures counts attacks and hits, damage (by attack, by kind of
    /// creature, overkill) and kills; a blow taken counts, and so does what a barrier soaked of it (for whoever gave it); a heal
    /// counts, past full too. Then the metrics page on Your Build. metrics.png.
    /// </summary>
    private void MetricsScenario()
    {
        if (_mtSteps == null) { if (_scT < 0.8f) return; _mtSteps = MetricsRun().GetEnumerator(); }
        if (!_mtSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> MetricsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        RunMetrics.Reset();
        p.Stats.MaxHp = 400; p.Hp = 400;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        yield return null;
        // two goblins in reach, one after the other: swing until each falls
        for (int n = 0; n < 2; n++)
        {
            var g = new Goblin { Position = p.GlobalPosition + new Vector2(36 * p.Facing, -6) };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Wake();
            for (float t = 0; t < 6f && GodotObject.IsInstanceValid(g) && !g.Dead; t += (float)GetProcessDeltaTime())
            {
                g.Freeze(0.2f, hold: true);
                _scInput = (int)(t * 10) % 2 == 0 ? new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0) } : default;
                yield return null;
            }
            _scInput = default;
            foreach (var _ in Wait(0.3f)) yield return null;
        }
        // a swing at nothing: a miss
        p.Facing = -p.Facing;
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0) };
        yield return null;
        _scInput = default;
        foreach (var _ in Wait(1.8f)) yield return null;
        var m = RunMetrics.Mine;
        ScCheck($"swings count as attacks, and the ones that struck as hits ({m.Attacks} attacks, {m.Hits} hits)", m.Attacks >= 3 && m.Hits >= 1 && m.Hits < m.Attacks);
        ScCheck($"the damage is counted, by attack and by creature ({RunMetrics.SlotName(p, 0)} {m.DealtBy.GetValueOrDefault(RunMetrics.SlotName(p, 0)):0}, goblins {m.DealtTo.Where(kv => kv.Key.Contains("oblin")).Sum(kv => kv.Value):0}, total {m.Dealt:0})",
            m.Dealt > 0 && m.DealtBy.GetValueOrDefault(RunMetrics.SlotName(p, 0)) > 0 && m.DealtTo.Keys.Any(k => k.Contains("oblin")));
        ScCheck($"and the kills ({m.Kills}, overkill {m.Overkill:0})", m.Kills >= 2 && m.KillsOf.Values.Sum() >= 2);
        // a blow taken, part of it soaked by a barrier
        var foe = new Goblin { Position = p.GlobalPosition + new Vector2(30, 0) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        p.GiveBarrier(10, 5);
        p.Hurt(30, foe.GlobalPosition, 0, foe, pooled: true);
        foe.QueueFree();
        ScCheck($"a blow taken counts (taken {m.Taken:0} from {string.Join(", ", m.TakenFrom.Keys)}), and so does what the barrier soaked ({RunMetrics.MitigationGiven(Net.Me):0} prevented)",
            m.Taken > 0 && RunMetrics.MitigationGiven(Net.Me) >= 9.9f);
        // a heal, past full
        p.Hp = p.Stats.MaxHp - 5;
        p.Heal(50);
        ScCheck($"healing counts, and what ran past full ({RunMetrics.HealingGiven(Net.Me):0} healed, {RunMetrics.OverhealGiven(Net.Me):0} over)", RunMetrics.HealingGiven(Net.Me) > 0 && RunMetrics.OverhealGiven(Net.Me) > 0);
        // the page
        _buildPanel.Open(metrics: true);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("metrics");
        ScCheck("the metrics page opens on Your Build", BuildPanel.Showing && _buildPanel.Metrics);
        _buildPanel.Close();
    }
}
