using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// --scenario=shifterbehaviors --hero=shifter [--shots=DIR]: what a Shape Shifter keeps of the creature it becomes.
///  * a fish swims in water without drowning, leaps out of it, and on land flops about (rather than walking) and drowns
///  * a spider walks up a wall, takes hold of a ceiling, drops on its web, and climbs back up it
///  * a bat has the air to itself, and hangs from a ceiling when idle (and lets go when it is asked to go)
///  * the cave's creatures take a shifted hero to be twice as far, and its own kind much further
/// </summary>
public partial class Main
{
    private IEnumerator<object> _sbSteps;
    private bool _sbDebug = System.Environment.GetEnvironmentVariable("SB_DEBUG") != null;
    private Enemy _sbGhost => G.Player.Ghost;

    private IEnumerable<object> SbSleep(float seconds)
    {
        for (float t = 0; t < seconds; t += (float)GetProcessDeltaTime()) yield return null;
    }

    private void SbTeleport(Vector2 at)
    {
        var p = G.Player;
        p.GlobalPosition = at; p.Velocity = Vector2.Zero;
        if (_sbGhost != null) { _sbGhost.GlobalPosition = at; _sbGhost.Velocity = Vector2.Zero; }
    }

    private void SbBecome(string key, Vector2 at)
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        if (p.Shifted) p.LeaveForm(false);
        _scInput = default;
        p.GlobalPosition = at; p.Velocity = Vector2.Zero;
        p.EnterForm(ShiftForm.All.First(f => f.Key == key));
    }

    private void ShifterBehaviorsScenario()
    {
        if (_sbSteps == null) { if (_scT < 0.6f) return; _sbSteps = SbRun().GetEnumerator(); }
        if (!_sbSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> SbRun()
    {
        var p = G.Player;
        var cave = G.Cave;
        p.Stats.MaxHp = 1000; p.Hp = 1000;

        // ------------------------------------------------------------ the fish
        Vector2? water = null;
        foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Water).OrderByDescending(s => s.Pos.Y))
        {
            bool open = true;
            for (int dx = -120; dx <= 120 && open; dx += 20)
                for (int dy = -30; dy <= 30 && open; dy += 15)
                    open = cave.IsWater(sp.Pos + new Vector2(dx, dy)) && !cave.IsSolid(sp.Pos + new Vector2(dx, dy));
            if (open) { water = sp.Pos; break; }
        }
        if (water == null) { ScCheck("open water to test in", false); yield break; }
        SbBecome("fish", water.Value);
        yield return null;
        ScCheck($"the Shape Shifter is a real fish ({_sbGhost?.GetType().Name})", _sbGhost is Fish);
        float hp0 = p.Hp;
        _scInput = new PlayerInput { Move = new Vector2(1, 0) };
        float x0 = p.GlobalPosition.X;
        foreach (var _ in SbSleep(2.5f)) yield return null;
        ScCheck($"it swims where it is pushed ({p.GlobalPosition.X - x0:0} px) and does not drown in water (hp {hp0:0} -> {p.Hp:0})", p.GlobalPosition.X - x0 > 40f && p.Hp >= hp0 * 0.95f);
        ScShot("sb_fish_swim");

        // up under the surface, with open air over it: the jump button throws it out of the water
        Vector2? shore = null;
        foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Water))
        {
            var at = new Vector2(sp.Pos.X, cave.WaterY + 22);
            bool ok = cave.IsWater(at) && !cave.IsSolid(at);
            for (int dy = 1; dy < 120 && ok; dy += 10) ok = !cave.IsSolid(new Vector2(at.X, cave.WaterY - dy));
            if (ok) { shore = at; break; }
        }
        if (shore != null)
        {
            SbTeleport(shore.Value);
            _scInput = default;
            foreach (var _ in SbSleep(0.5f)) yield return null;
            float topY = float.MaxValue;
            _scInput = new PlayerInput { Move = new Vector2(0.4f, -0.6f), Jump = true };
            for (float t = 0; t < 1.5f; t += (float)GetProcessDeltaTime()) { _scInput = new PlayerInput { Move = new Vector2(0.4f, -0.6f), Jump = t < 0.3f }; topY = Math.Min(topY, _sbGhost.GlobalPosition.Y); yield return null; }
            ScCheck($"the jump button leaps it out of the water (rose to {cave.WaterY - topY:0} px over the surface)", topY < cave.WaterY - 8f);
        }
        else ScCheck("a shore to leap from", false);

        // on dry land: it flops about, and drowns in the air
        Vector2? land = null;
        foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Ground))
            if (!cave.IsWater(sp.Pos) && sp.Pos.Y < cave.WaterY - 20 && !cave.IsSolid(sp.Pos)) { land = sp.Pos; break; }
        if (land == null) { ScCheck("dry land to flop on", false); yield break; }
        SbBecome("fish", land.Value);
        _scInput = default;
        hp0 = p.Hp;
        bool flopped = false; float far = 0; var from = p.GlobalPosition;
        for (float t = 0; t < 3.5f; t += (float)GetProcessDeltaTime())
        {
            if (_sbGhost.Animator?.Current == "flop") flopped = true;
            far = Math.Max(far, p.GlobalPosition.DistanceTo(from));
            if (t > 1f) ScShot("sb_fish_flop");
            yield return null;
        }
        ScCheck($"on land the fish flops about (flop clip {flopped}, moved {far:0} px)", flopped);
        ScCheck($"and drowns in the air (hp {hp0:0} -> {p.Hp:0})", p.Hp < hp0 - 20f);
        p.Hp = p.Stats.MaxHp;

        // ------------------------------------------------------------ the spider
        // a floor that runs flat to a tall wall (to the right or to the left of it)
        Vector2? wallSpot = null; int wallDir = 1;
        foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Ground))
        {
            if (wallSpot != null) break;
            foreach (var (dir, off) in Enumerable.Range(-12, 25).SelectMany(k => new[] { (1, k * 30), (-1, k * 30) }))
            {
                var start = sp.Pos + new Vector2(off, -10);
                if (cave.IsSolid(start) || cave.IsWater(start) || !cave.FindFloor(start, 40, out var f0)) continue;
                float fy = f0.Y; bool ok = false;
                for (int k = 1; k <= 25; k++)
                {
                    float x = start.X + dir * k * 8f;
                    if (cave.IsSolid(new Vector2(x, fy - 14)))
                    {
                        // the wall: rock to well above where it climbs, and the floor under the start flat up to it
                        ok = k >= 5 && cave.IsSolid(new Vector2(x + dir * 4, fy - 40)) && cave.IsSolid(new Vector2(x + dir * 6, fy - 70)) && cave.IsSolid(new Vector2(x + dir * 8, fy - 100));
                        break;
                    }
                    if (cave.IsSolid(new Vector2(x, fy - 36)) || !cave.FindFloor(new Vector2(x, fy - 12), 30, out var fx) || Math.Abs(fx.Y - fy) > 8f || Math.Abs(fx.Y - f0.Y) > 18f || cave.IsWater(new Vector2(x, fy - 6))) break;
                    fy = fx.Y;
                }
                if (ok) { wallSpot = start; wallDir = dir; break; }
            }
        }
        if (wallSpot == null) GD.Print("[scenario] note: no clean wall in this cave to climb (try another --seed)");
        else
        {
            SbBecome("spider", wallSpot.Value);
            yield return null;
            ScCheck($"the Shape Shifter is a real spider ({_sbGhost?.GetType().Name})", _sbGhost is Spider);
            foreach (var _ in SbSleep(0.6f)) yield return null;
            float y0 = p.GlobalPosition.Y;
            _scInput = new PlayerInput { Move = new Vector2(wallDir, 0) };
            bool onWall = false; float topY = y0;
            for (float t = 0; t < 2.5f; t += (float)GetProcessDeltaTime())
            {
                var sp = (Spider)_sbGhost;
                if (sp.State == 5) { onWall = true; _scInput = new PlayerInput { Move = new Vector2(wallDir * 0.2f, -1) }; }
                topY = Math.Min(topY, p.GlobalPosition.Y);
                if (_sbDebug && (int)(t * 60) % 12 == 0) GD.Print($"  [sb] t={t:0.00} pos={p.GlobalPosition} state={sp.State} n={sp.TestN} floor={sp.IsOnFloor()}");
                yield return null;
            }
            ScCheck($"pushed into a wall the spider takes hold of it and climbs (rose {y0 - topY:0} px, clinging {onWall})", onWall && y0 - topY > 40f);
            ScShot("sb_spider_wall");
        }

        // the ceiling and the web
        Vector2? ceil = null;
        foreach (var (sp, off) in cave.Spawns.Where(s => s.Kind == SpawnKind.Ceiling).SelectMany(s => Enumerable.Range(-8, 17).Select(k => (s, k * 30))))
        {
            if (!cave.FindCeiling(sp.Pos + new Vector2(off, 20), 200, out var ce0)) continue;
            var at = ce0 + new Vector2(0, 8);
            if (cave.IsSolid(at)) continue;
            if (!cave.FindFloor(at, 400, out var fl) || fl.Y - at.Y < 110) continue;
            // (a ceiling that runs flat a good way either side)
            bool flatCeil = true;
            for (int dx = -60; dx <= 60 && flatCeil; dx += 15)
                flatCeil = cave.FindCeiling(new Vector2(at.X + dx, at.Y), 30, out var cx) && Math.Abs(cx.Y - ce0.Y) < 8f && !cave.IsSolid(new Vector2(at.X + dx, at.Y + 6));
            if (!flatCeil) continue;
            if (cave.IsWater(fl + new Vector2(0, -6))) continue;
            ceil = at; break;
        }
        if (ceil == null) GD.Print("[scenario] note: no flat ceiling in this cave to hang from (try another --seed)");
        else
        {
            SbBecome("spider", ceil.Value + new Vector2(0, 40));
            yield return null;
            var sp = (Spider)_sbGhost;
            SbTeleport(ceil.Value);
            sp.TestCling(new Vector2(0, 1));
            _scInput = default;
            foreach (var _ in SbSleep(0.4f)) yield return null;
            ScCheck($"it can hang from a ceiling (state {sp.State})", sp.State == 5);
            // along it
            float cx0 = p.GlobalPosition.X;
            _scInput = new PlayerInput { Move = new Vector2(1, -0.2f) };
            foreach (var _ in SbSleep(1.2f)) yield return null;
            ScCheck($"and crawl along it ({p.GlobalPosition.X - cx0:0} px, still hanging {sp.State == 5})", Math.Abs(p.GlobalPosition.X - cx0) > 20f && sp.State == 5);
            // down: it drops on its thread
            float cy0 = p.GlobalPosition.Y;
            _scInput = new PlayerInput { Move = new Vector2(0, 1) };
            foreach (var _ in SbSleep(0.5f)) yield return null;
            ScShot("sb_spider_web");
            float low = p.GlobalPosition.Y;
            ScCheck($"pushed down it drops on its web ({low - cy0:0} px, state {sp.State})", low - cy0 > 40f && sp.State is 1 or 2);
            // let go of the stick: it hangs there
            _scInput = default;
            foreach (var _ in SbSleep(0.4f)) yield return null;
            float held = p.GlobalPosition.Y;
            ScCheck($"released, it hangs there (state {sp.State})", sp.State == 2 && Math.Abs(held - low) < 30f && sp.ThreadAnchorY != null);
            // up: it climbs back and takes hold again
            _scInput = new PlayerInput { Move = new Vector2(0, -1) };
            foreach (var _ in SbSleep(2.5f)) yield return null;
            ScCheck($"pushed up it climbs back up its web and takes the ceiling again (state {sp.State}, {cy0 - p.GlobalPosition.Y:0} px from where it left)", sp.State == 5 && Math.Abs(p.GlobalPosition.Y - cy0) < 12f);
            // the jump button cuts the web
            _scInput = new PlayerInput { Move = new Vector2(0, 1) };
            foreach (var _ in SbSleep(0.3f)) yield return null;
            _scInput = new PlayerInput { Jump = true };
            foreach (var _ in SbSleep(0.15f)) yield return null;
            _scInput = default;
            foreach (var _ in SbSleep(0.3f)) yield return null;
            ScCheck($"and the jump button cuts the thread (state {sp.State})", sp.State == 4);
        }

        // ------------------------------------------------------------ the bat
        Vector2? roost = null;
        foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Ceiling))
        {
            if (!cave.FindCeiling(sp.Pos + new Vector2(0, 20), 200, out var ce1)) continue;
            var at = ce1 + new Vector2(0, 8);
            if (cave.IsSolid(at) || cave.IsWater(at)) continue;
            if (!cave.FindFloor(at, 300, out var fl) || fl.Y - at.Y < 90) continue;
            roost = new Vector2(at.X, at.Y + 70); break;
        }
        if (roost == null) ScCheck("a ceiling for the bat", false);
        else
        {
            SbBecome("bat", roost.Value);
            yield return null;
            var bat = (Bat)_sbGhost;
            ScCheck($"the Shape Shifter is a real bat ({_sbGhost?.GetType().Name})", _sbGhost is Bat);
            foreach (var _ in SbSleep(1.0f)) yield return null;
            ScCheck("with no ceiling near it hovers on the wing, hanging from nothing", !bat.Roosting);
            _scInput = new PlayerInput { Move = new Vector2(0, -1) };
            foreach (var _ in SbSleep(1.2f)) yield return null;
            ScCheck($"pushed up into a ceiling it takes hold, hanging as a roosting bat does ({bat.Roosting})", bat.Roosting);
            ScShot("sb_bat_roost");
            _scInput = default;
            var at = p.GlobalPosition;
            foreach (var _ in SbSleep(1.0f)) yield return null;
            ScCheck("and stays there, idle", bat.Roosting && p.GlobalPosition.DistanceTo(at) < 4f);
            _scInput = new PlayerInput { Move = new Vector2(1, 0.2f) };
            foreach (var _ in SbSleep(0.8f)) yield return null;
            ScCheck($"pushed any other way it lets go and flies ({bat.Roosting}, moved {p.GlobalPosition.DistanceTo(at):0} px)", !bat.Roosting && p.GlobalPosition.DistanceTo(at) > 30f);
            // never lands: idling low over the floor
            _scInput = new PlayerInput { Move = new Vector2(0, 1) };
            foreach (var _ in SbSleep(1.5f)) yield return null;
            _scInput = default;
            foreach (var _ in SbSleep(1.0f)) yield return null;
            ScCheck($"on the way down it can't land or perch on the floor: it only hovers (roosting {bat.Roosting})", !bat.Roosting);
        }

        // ------------------------------------------------------------ what the cave takes it to be
        SbBecome("goblin", cave.Spawns.First(s => s.Kind == SpawnKind.Ground).Pos);
        yield return null;
        var foe = new Goblin { Position = p.GlobalPosition + new Vector2(200, 0) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        yield return null;
        float kin = foe.NoticeOf(p);
        p.LeaveForm(false);
        float plain = foe.NoticeOf(p);
        p.EnterForm(ShiftForm.All.First(f => f.Key == "skeleton"));
        float other = foe.NoticeOf(p);
        p.LeaveForm(false);
        p.EnterForm(ShiftForm.All.First(f => f.Key == "goblin"));
        kin = foe.NoticeOf(p);
        ScCheck($"a hero as itself seems the real distance away ({plain:0.0}x)", Math.Abs(plain - 1f) < 0.01f);
        ScCheck($"in another creature's form it seems twice as far ({other:0.0}x)", Math.Abs(other - 2f) < 0.01f);
        ScCheck($"to its own kind it seems far further ({kin:0.0}x)", kin >= 4f);
        // among several heroes, the kin come last of all (but are chosen when alone)
        int pick = Enemy.ChooseHero(Vector2.Zero, new[] { (new Vector2(30, 0), Tune.Shifter.KinChoosingFactor, false), (new Vector2(500, 0), 1f, false) });
        ScCheck($"among several heroes it goes for the other one, though the kin stand right by (picked {pick})", pick == 1);
        pick = Enemy.ChooseHero(Vector2.Zero, new[] { (new Vector2(30, 0), Tune.Shifter.KinChoosingFactor, false) });
        ScCheck($"alone, the kin is still a valid target (picked {pick})", pick == 0);
        pick = Enemy.ChooseHero(Vector2.Zero, new[] { (new Vector2(60, 0), Tune.Shifter.NoticeFactor, false), (new Vector2(100, 0), 1f, false) });
        ScCheck($"a shifted hero 60 px off loses to a plain one 100 px off (picked {pick})", pick == 1);
        foe.QueueFree();
    }
}
