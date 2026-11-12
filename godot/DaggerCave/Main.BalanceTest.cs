using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _balSteps;

    /// <summary>
    /// `--scenario=balance --hero=NAME`: one hero measured the same way as every other, played by the same simple hand (aim at the
    /// nearest creature, hold the attack, press each ability as it comes back), with real cooldowns and no upgrades:
    /// <list type="bullet">
    /// <item>one: damage a second into a single creature held in reach;</item>
    /// <item>pack: damage a second into six held close round it (what its reach and its area are worth);</item>
    /// <item>brawl: twenty-four seconds with goblins walking in from both sides, two more every few seconds: how many it kills, the
    /// health it loses (net of what it heals back and soaks), as a share of its own health.</item>
    /// </list>
    /// One line of numbers, <c>[balance]</c>, to set beside the other heroes'. It checks nothing: it is a ruler.
    /// </summary>
    private void BalanceScenario()
    {
        if (_balSteps == null) { if (_scT < 0.8f) return; _balSteps = BalanceRun().GetEnumerator(); }
        if (!_balSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> BalanceRun()
    {
        var p = G.Player;
        void Clear() { foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); } }
        Clear();
        var home = p.GlobalPosition;
        float clock = 0f;
        Enemy Nearest() => G.Enemies.Where(e => IsInstanceValid(e) && !e.Dead).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition)).FirstOrDefault();
        bool guard = false;
        p.InputOverride = () =>
        {
            var foe = Nearest();
            var aim = foe != null ? (foe.GlobalPosition - p.GlobalPosition).Normalized() : new Vector2(p.Facing, 0);
            int tick = (int)(clock * 30f);
            return new PlayerInput
            {
                Aim = aim, AimGiven = true, AimDist = foe != null ? foe.GlobalPosition.DistanceTo(p.GlobalPosition) : 0f,
                Attack = tick % 2 == 0, AttackHeld = true,
                Ability = tick % 12 == 3, Ability2 = tick % 12 == 7,
                Dodge = guard && tick % 45 == 20,
            };
        };
        Goblin Dummy(Vector2 at)
        {
            var g = new Goblin { Position = at };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Hp = g.MaxHp = 1000000f;
            g.Wake();
            return g;
        }
        IEnumerable<object> Run(float seconds, Action each = null)
        {
            for (float t = 0; t < seconds; t += (float)GetProcessDeltaTime())
            {
                clock += (float)GetProcessDeltaTime();
                each?.Invoke();
                yield return null;
            }
        }
        float maxHp = p.Stats.MaxHp;
        void Fresh() { p.Hp = p.Stats.MaxHp; p.GlobalPosition = home; p.Velocity = Vector2.Zero; }

        // ---- one creature, held in reach
        const float T = 10f;
        var one = Dummy(home + new Vector2(38 * p.Facing, -6));
        foreach (var _ in Run(1.5f, () => { one.Pin(0.2f); Fresh(); })) yield return null;
        RunMetrics.Reset();
        foreach (var _ in Run(T, () => { one.Pin(0.2f); Fresh(); })) yield return null;
        float single = RunMetrics.Mine.Dealt / T;
        string by1 = string.Join(" ", RunMetrics.Mine.DealtBy.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key}:{kv.Value / T:0.0}"));
        Clear();
        foreach (var _ in Run(0.5f)) yield return null;

        // ---- six, held close round it
        var pack = new List<Goblin>();
        float[] dx = { 34, 58, 82, -34, -58, -82 };
        foreach (float x in dx) pack.Add(Dummy(home + new Vector2(x, -6)));
        foreach (var _ in Run(1.5f, () => { foreach (var g in pack) g.Pin(0.2f); Fresh(); })) yield return null;
        RunMetrics.Reset();
        foreach (var _ in Run(T, () => { foreach (var g in pack) g.Pin(0.2f); Fresh(); })) yield return null;
        float crowd = RunMetrics.Mine.Dealt / T;
        // (which of the six took it: by where each stood, px from the hero, ahead positive)
        GD.Print("[balance]   pack, by place: " + string.Join("  ", pack.Select((g, k) => $"{dx[k] * p.Facing:+0;-0}: {(1000000f - g.Hp) / T:0.0}/s (now at {(g.GlobalPosition.X - home.X) * p.Facing:+0;-0})")));
        string by2 = string.Join(" ", RunMetrics.Mine.DealtBy.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key}:{kv.Value / T:0.0}"));
        Clear();
        foreach (var _ in Run(0.5f)) yield return null;

        // ---- the brawl: goblins walk in from both sides
        const float B = 24f;
        Fresh();
        RunMetrics.Reset();
        guard = true;
        float next = 0f, lost = 0f, low = 1f, bt = 0f;
        int sent = 0, side = 1;
        foreach (var _ in Run(B, () =>
        {
            bt += (float)GetProcessDeltaTime();
            if (bt >= next && G.Enemies.Count(e => IsInstanceValid(e) && !e.Dead) < 10)
            {
                next = bt + 1.6f;
                for (int k = 0; k < 2; k++)
                {
                    var g = new Goblin { Position = home + new Vector2(side * (150 + 18 * k), -6) };
                    g.SetMeta("test", true);
                    _world.AddChild(g);
                    g.Wake();
                    side = -side; sent++;
                }
            }
            low = Math.Min(low, p.Hp / p.Stats.MaxHp);
            // (it never dies: what it lost is counted and given back, so every hero fights the whole fight)
            if (p.Hp < p.Stats.MaxHp * 0.4f) { lost += p.Stats.MaxHp * 0.5f; p.Hp += p.Stats.MaxHp * 0.5f; }
            if (p.GlobalPosition.DistanceTo(home) > 120f) p.GlobalPosition = home;
        })) yield return null;
        lost += p.Stats.MaxHp - p.Hp;
        var m = RunMetrics.Mine;
        GD.Print($"[balance] {G.Hero,-13} hp {maxHp,4:0}  one {single,5:0.0}/s  pack {crowd,6:0.0}/s  brawl: kills {m.Kills,2} of {sent}  dealt {m.Dealt / B,5:0.0}/s  took {m.Taken,4:0} soaked {m.MitigatedSelf,4:0}  net lost {lost,4:0} = {lost / maxHp * 100f,4:0}% of its health  low {low * 100f:0}%   [{by1}]  pack [{by2}]");
        p.InputOverride = null;
        Clear();
    }
}
