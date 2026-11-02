using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _crSteps;

    /// <summary>
    /// `--scenario=crowd`, soft separation (Enemy.Crowd.cs, <see cref="Tune.Crowd"/>): eight goblins dropped on one spot spread out along the
    /// ground rather than stacking, none pushed into rock; a held creature (a generator, here a frozen-solid one) isn't shoved; and a walker
    /// and a flier pass over each other.
    /// </summary>
    private void CrowdScenario()
    {
        if (_crSteps == null) { if (_scT < 0.8f) return; _crSteps = CrowdRun().GetEnumerator(); }
        if (!_crSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> CrowdRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        p.Stats.MaxHp = p.Hp = 50000;
        p.InputOverride = () => default;
        foreach (var _ in ElWait(0.4f)) yield return null;
        // (on the floor a little way off, all on the very same spot)
        var spot = p.GlobalPosition + new Vector2(p.Facing * 140f, -6f);
        var pack = new List<Enemy>();
        for (int k = 0; k < 8; k++)
        {
            var g = new Goblin { Position = spot };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Hp = g.MaxHp = 100000f;
            pack.Add(g);
        }
        foreach (var _ in ElWait(1.6f)) yield return null;
        ScShot("crowd_spread");
        var xs = pack.Select(e => e.GlobalPosition.X).OrderBy(x => x).ToArray();
        float span = xs[^1] - xs[0];
        float closest = float.MaxValue;
        for (int i = 0; i < pack.Count; i++)
        for (int j = i + 1; j < pack.Count; j++)
            closest = Math.Min(closest, pack[i].GlobalPosition.DistanceTo(pack[j].GlobalPosition));
        float want = pack[0].HitRadius * 2f * Tune.Crowd.Spacing;
        ScCheck($"eight goblins dropped on one spot spread out: {span:0} px from first to last, the closest two {closest:0.0} px apart (want about {want:0.0})", span > 60f && closest > want * 0.5f);
        ScCheck($"  none is pushed into rock ({pack.Count(e => G.Cave.IsSolid(e.GlobalPosition))} in it)", pack.All(e => !G.Cave.IsSolid(e.GlobalPosition)));
        // (held at the back, the light ones leap the crowd to the hero's far side)
        foreach (var _ in ElWait(3f)) yield return null;
        ScShot("crowd_flank");
        float hx = p.GlobalPosition.X;
        int far = pack.Count(e => Math.Sign(e.GlobalPosition.X - hx) == -Math.Sign(spot.X - hx));
        ScCheck($"  held at the back, some leap the crowd to the hero's far side ({pack.Sum(e => e.Flanks)} leaps, {far} now beyond the hero)", pack.Sum(e => e.Flanks) >= 1 && far >= 1);
        ScCheck($"  and every one was eased apart ({pack.Count(e => e.CrowdNudged > 0.5f)} of {pack.Count})", pack.Count(e => e.CrowdNudged > 0.5f) >= 6);

        // a creature frozen solid holds its ground: the one pressed into it gives way
        foreach (var e in pack) e.QueueFree();
        foreach (var _ in ElWait(0.2f)) yield return null;
        var rock = new Goblin { Position = spot };
        rock.SetMeta("test", true);
        _world.AddChild(rock);
        rock.Hp = rock.MaxHp = 100000f;
        foreach (var _ in ElWait(0.6f)) yield return null;
        rock.FreezeSolid(20f, force: true);
        var at = rock.GlobalPosition;
        var pusher = new Goblin { Position = at };
        pusher.SetMeta("test", true);
        _world.AddChild(pusher);
        pusher.Hp = pusher.MaxHp = 100000f;
        foreach (var _ in ElWait(0.8f)) yield return null;
        ScCheck($"one frozen solid isn't shoved ({rock.GlobalPosition.DistanceTo(at):0.0} px); the other gives way ({pusher.GlobalPosition.DistanceTo(at):0} px off)",
            rock.GlobalPosition.DistanceTo(at) < 2f && pusher.GlobalPosition.DistanceTo(at) > want * 0.6f);
        rock.QueueFree(); pusher.QueueFree();

        // a walker and a flier don't push each other: the air is the flier's
        var walker = new Goblin { Position = spot };
        var flier = new Bat { Position = spot };
        foreach (var e in new Enemy[] { walker, flier }) { e.SetMeta("test", true); _world.AddChild(e); e.Hp = e.MaxHp = 100000f; }
        foreach (var _ in ElWait(0.5f)) yield return null;
        ScCheck($"a walker and a flier pass over each other ({walker.CrowdNudged:0.0} and {flier.CrowdNudged:0.0} px eased)", walker.CrowdNudged < 0.5f && flier.CrowdNudged < 0.5f);
        walker.QueueFree(); flier.QueueFree();
        p.InputOverride = null;
    }
}
