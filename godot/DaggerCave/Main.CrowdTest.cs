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

public partial class Main
{
    private IEnumerator<object> _mvSteps;

    /// <summary>
    /// `--scenario=movers`, the creatures that hold other heights (Enemies/Movers.cs, <see cref="Tune.Movers"/>): a swarm of mites keeps
    /// together as one cloud about the hero's head and darts in to bite; a delver runs under the floor unseen and unhittable, and bursts out
    /// beside the hero; a bomber holds high over the hero and lets fall pods that burst on the floor.
    /// </summary>
    private void MoversScenario()
    {
        if (_mvSteps == null) { if (_scT < 0.8f) return; _mvSteps = MoversRun().GetEnumerator(); }
        if (!_mvSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> MoversRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        p.Stats.MaxHp = p.Hp = 50000;
        p.InputOverride = () => default;
        foreach (var _ in ElWait(0.4f)) yield return null;
        T Add<T>(T e, Vector2 at) where T : Enemy
        {
            e.Position = at;
            e.SetMeta("test", true);
            e.Engage();
            _world.AddChild(e);
            e.Hp = e.MaxHp = 100000f;
            return e;
        }
        var home = p.GlobalPosition;

        // ---- a swarm of mites
        int flock = Mite.NewFlock();
        var swarm = Enumerable.Range(0, 5).Select(k => Add(new Mite { Flock = flock }, home + new Vector2(p.Facing * (160f + k * 12f), -50f - k * 6f))).ToList();
        foreach (var _ in ElWait(2.5f)) yield return null;
        // (how much of the time each is up over the hero's middle)
        int up = 0, frames = 0;
        for (float t = 0; t < 1.5f; t += (float)GetProcessDeltaTime())
        {
            up += swarm.Count(m => m.GlobalPosition.Y < p.GlobalPosition.Y - 4f);
            frames += swarm.Count;
            yield return null;
        }
        ScShot("movers_swarm");
        float far = swarm.Max(m => m.GlobalPosition.DistanceTo(p.GlobalPosition));
        float spread = 0f;
        foreach (var a in swarm) foreach (var b in swarm) spread = Math.Max(spread, a.GlobalPosition.DistanceTo(b.GlobalPosition));
        ScCheck($"a swarm of mites gathers about the hero (the farthest {far:0} px off) as one cloud ({spread:0} px across)", far < 130f && spread < 130f);
        ScCheck($"  up in the air, not on the floor (above the hero's middle {100f * up / Math.Max(1, frames):0}% of the time)", up >= frames * 0.6f);
        ScCheck($"  and darts in to bite ({swarm.Sum(m => m.Darts)} darts, {50000 - p.Hp:0} taken)", swarm.Sum(m => m.Darts) >= 1);
        foreach (var m in swarm) m.QueueFree();
        p.Hp = p.Stats.MaxHp;
        foreach (var _ in ElWait(0.3f)) yield return null;

        // ---- a delver under the floor
        var start = home + new Vector2(-p.Facing * 220f, 0f);
        if (G.Cave.FindFloor(start + new Vector2(0, -20f), 200f, out var fl)) start = fl;
        var delver = Add(new Delver(), start + new Vector2(0, Tune.Movers.DelverDepth));
        bool hidden = true, untouchable = true;
        for (float t = 0; t < 1.0f; t += (float)GetProcessDeltaTime())
        {
            hidden &= !delver.Animator.Visible;
            untouchable &= !delver.CanBeHit;
            yield return null;
        }
        ScCheck($"a delver runs under the floor unseen ({hidden}) and can't be struck ({untouchable})", hidden && untouchable);
        for (float t = 0; t < 5f && delver.Eruptions == 0; t += (float)GetProcessDeltaTime()) yield return null;
        foreach (var _ in ElWait(0.15f)) yield return null;
        ScShot("movers_delver");
        float off = delver.GlobalPosition.DistanceTo(p.GlobalPosition);
        ScCheck($"  it bursts out beside the hero ({delver.Eruptions} times, {off:0} px off), seen ({delver.Animator.Visible}) and open to blows ({delver.CanBeHit})",
            delver.Eruptions >= 1 && off < 90f && delver.Animator.Visible && delver.CanBeHit);
        delver.QueueFree();
        p.Hp = p.Stats.MaxHp;
        foreach (var _ in ElWait(0.3f)) yield return null;

        // ---- a bomber up high
        int bursts = EnemyProjectile.Bursts;
        var bomber = Add(new Bomber(), home + new Vector2(p.Facing * 120f, -60f));
        float highest = 0f;
        bool shot = false;
        for (float t = 0; t < 8f; t += (float)GetProcessDeltaTime())
        {
            highest = Math.Max(highest, p.GlobalPosition.Y - bomber.GlobalPosition.Y);
            if (t > 2.4f && !shot) { shot = true; ScShot("movers_bomber"); }
            yield return null;
        }
        ScCheck($"a bomber holds high over the hero (up to {highest:0} px over)", highest > 70f);
        ScCheck($"  and lets fall pods ({bomber.Drops}) that burst ({EnemyProjectile.Bursts - bursts}), hurting the hero standing under ({50000 - p.Hp:0})", bomber.Drops >= 1 && EnemyProjectile.Bursts > bursts);
        bomber.QueueFree();
        p.InputOverride = null;
    }
}
