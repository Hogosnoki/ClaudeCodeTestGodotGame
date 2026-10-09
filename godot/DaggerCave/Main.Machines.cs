using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>
    /// The works' machinery: a lift in each of the generator's lift shafts, and about the galleries pistons hung from the roof (where the
    /// roof is a floor and a half-height or so over the hero), belts along stretches of level floor, and saws on rails along the floor.
    /// </summary>
    private void PlaceMachinery(CaveData cave, Random rng, int count, Func<Vector2, bool> clear, List<Vector2> placed)
    {
        foreach (var (top, bottom) in cave.LiftShafts)
            _world.AddChild(new Lift { Position = new Vector2(top.X, top.Y), Top = top.Y, Bottom = bottom.Y });

        bool FlatAt(Vector2 fl, Vector2 p) => cave.FindFloor(p + new Vector2(0, -10), 22, out var q) && Math.Abs(q.Y - fl.Y) < 2f;

        // ---- pistons
        int made = 0;
        for (int tries = 0; tries < 5000 && made < Math.Max(3, count * 4 / 9); tries++)
        {
            var at = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
            if (cave.IsSolid(at) || !cave.FindCeiling(at, 300, out var ce) || !cave.FindFloor(at, 300, out var fl)) continue;
            float drop = fl.Y - ce.Y;
            if (drop < 70f || drop > 120f) continue;
            if (!FlatAt(fl, fl + new Vector2(-16, 0)) || !FlatAt(fl, fl + new Vector2(16, 0)) || !cave.IsSolid(ce + new Vector2(-16, -6)) || !cave.IsSolid(ce + new Vector2(16, -6))) continue;
            if (!clear(fl)) continue;
            placed.Add(fl);
            _world.AddChild(new Piston { Position = ce + new Vector2(0, -2), Drop = drop - 8f });
            made++;
        }
        // ---- belts
        made = 0;
        for (int tries = 0; tries < 4000 && made < Math.Max(3, count * 4 / 9); tries++)
        {
            var at = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
            if (cave.IsSolid(at) || !cave.FindFloor(at, 300, out var fl)) continue;
            float l = 0, r = 0;
            while (l < 110 && FlatAt(fl, fl + new Vector2(-l - 8, 0))) l += 8;
            while (r < 110 && FlatAt(fl, fl + new Vector2(r + 8, 0))) r += 8;
            if (l + r < 110f) continue;
            float len = Math.Min(l + r, 120 + rng.Next(70));
            float cx = fl.X + Math.Clamp((r - l) * 0.5f, -(l + r - len) * 0.5f - 0.001f, (l + r - len) * 0.5f + 0.001f) ;
            var mid = new Vector2(cx, fl.Y);
            if (!clear(mid)) continue;
            placed.Add(mid);
            _world.AddChild(new Conveyor { Position = mid, Half = len * 0.5f, Dir = rng.Next(2) == 0 ? -1 : 1 });
            made++;
        }
        // ---- saws, on rails along the floor
        made = 0;
        for (int tries = 0; tries < 4000 && made < Math.Max(2, count * 3 / 10); tries++)
        {
            var at = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
            if (cave.IsSolid(at) || !cave.FindFloor(at, 300, out var fl)) continue;
            float len = 100 + rng.Next(90);
            var a = fl + new Vector2(0, -15f);
            var b = a + new Vector2(len, 0);
            if (!FlatAt(fl, fl + new Vector2(len, 0)) || !cave.LineClear(a + new Vector2(-4, -14), b + new Vector2(4, -14)) || cave.IsSolid(a + new Vector2(-10, 0)) || cave.IsSolid(b + new Vector2(10, 0))) continue;
            if (!clear(a) || !clear(b)) continue;
            placed.Add(a); placed.Add(b);
            _world.AddChild(new SawBlade { Start = a, End = b, Position = a });
            made++;
        }
    }
}
