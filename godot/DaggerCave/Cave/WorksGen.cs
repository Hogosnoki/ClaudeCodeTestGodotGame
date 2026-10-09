using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    // ================================================================== the clockwork deep

    /// <summary>
    /// The Clockwork Deep: four stacked galleries with perfectly level floors (long runs of corridor, a roof that steps now and then),
    /// great machine halls cut off them (tall, with a stair of catwalks up one wall and the other), and shafts between the floors with a
    /// stair of catwalks too, some of which also hold a lift (see <see cref="CaveData.LiftShafts"/>). The way in is top left; the
    /// guardian's engine hall is at the bottom right. The floors are dead level so that belts and pistons have somewhere to be.
    /// </summary>
    private static CaveData GenerateWorks(int seed)
    {
        var rng = new Random(seed);
        int RndI(int a, int bIncl) => rng.Next(a, bIncl + 1);
        var f = new Field(seed, 0.3f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };
        const int levels = 4;
        var baseRow = new int[levels];
        for (int l = 0; l < levels; l++) baseRow[l] = (int)(H * (0.21f + 0.21f * l)) + RndI(-1, 1);
        int hallW = 32, hallX0 = W - 5 - hallW;
        var floor = new int[levels][];
        var chambers = new List<Chamber>();
        // the galleries: one dead-level floor each, a roof of varying height
        for (int l = 0; l < levels; l++)
        {
            floor[l] = new int[W + 1];
            int F = baseRow[l], x = 5, end = l == levels - 1 ? hallX0 + 1 : W - 5;
            while (x < end)
            {
                int run = RndI(8, 18), hgt = RndI(5, 7);
                int x1 = Math.Min(end, x + run);
                for (int i = x; i <= x1; i++) floor[l][i] = F;
                f.Rect(x, F - hgt, x1, F);
                // the join: the roof steps, the floor does not
                f.Rect(x1, F - 7, x1 + 1, F);
                x = x1;
            }
        }
        // the machine halls: wide, tall chambers cut off the galleries
        for (int l = 0; l < levels; l++)
        {
            int made = 0, tries = 0;
            int lo = l == 0 ? 24 : 8;
            while (made < (l == levels - 1 ? 2 : 3) && tries++ < 40)
            {
                int w = RndI(18, 28), h = RndI(11, 15);
                int x0 = RndI(lo, (l == levels - 1 ? hallX0 - 12 : W - 8) - w);
                if (x0 + w > (l == levels - 1 ? hallX0 - 6 : W - 6) || chambers.Any(c => c.Level == l && x0 < c.X1 + 6 && x0 + w > c.X0 - 6)) continue;
                int F = floor[l][x0 + w / 2];
                if (F == 0 || F - h < 6) continue;
                var c = new Chamber { X0 = x0, X1 = x0 + w, Top = F - h, Floor = F, Level = l };
                f.Rect(c.X0, c.Top, c.X1, c.Floor);
                chambers.Add(c);
                made++;
            }
        }
        // shafts between the floors: a stair of catwalks up one wall and the other; about half hold a lift as well
        var shafts = new List<(int x, int l, bool lift)>();
        for (int l = 0; l < levels - 1; l++)
        {
            int count = RndI(2, 3), tries = 0;
            while (count > 0 && tries++ < 40)
            {
                int sx = RndI(10, (l + 1 == levels - 1 ? hallX0 - 14 : W - 18));
                if (shafts.Any(sh => sh.l == l && Math.Abs(sh.x - sx) < 32)) continue;
                if (floor[l][sx] == 0 || floor[l + 1][sx + 8] == 0 || floor[l][sx + 8] == 0) continue;
                bool lift = shafts.Count(sh => sh.l == l && sh.lift) == 0 || rng.NextDouble() < 0.4;
                shafts.Add((sx, l, lift));
                f.Rect(sx, floor[l][sx] - 2, sx + 8, floor[l + 1][sx + 4] - 2);
                if (lift) cave.LiftShafts.Add((new Vector2((sx + 4) * CaveData.Cell, (floor[l][sx] - 0.5f) * CaveData.Cell), new Vector2((sx + 4) * CaveData.Cell, (floor[l + 1][sx + 4] - 0.5f) * CaveData.Cell)));
                count--;
            }
        }
        // rooms and spawn points
        int sc = 12;
        var startFloor = floor[0][sc];
        f.Rect(5, startFloor - 9, 20, startFloor);
        cave.Rooms.Add(MakeRoom(RoomKind.Start, 12, startFloor - 3, startFloor, 7, 8));
        var hallF = floor[levels - 1][hallX0 - 1] != 0 ? floor[levels - 1][hallX0 - 1] : baseRow[levels - 1];
        var hall = new Chamber { X0 = hallX0, X1 = W - 5, Top = hallF - 14, Floor = hallF, Level = levels - 1 };
        f.Rect(hall.X0, hall.Top, hall.X1, hall.Floor);
        foreach (var c in chambers)
            cave.Rooms.Add(MakeRoom(RoomKind.Treasure, (c.X0 + c.X1) * 0.5f, (c.Top + c.Floor) * 0.5f, c.Floor, (c.X1 - c.X0) * 0.5f, (c.Floor - c.Top) * 0.5f));
        var boss = MakeRoom(RoomKind.Boss, (hall.X0 + hall.X1) * 0.5f, hall.Floor - 6f, hall.Floor, 15, 7f);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        cave.StartPos = new Vector2(12, startFloor - 1.2f) * CaveData.Cell;
        AssignMiniBosses(cave, rng, 40);
        var spawnStamps = new List<Stamp>();
        for (int l = 0; l < levels; l++)
            for (int x = (l == 0 ? 26 : 8); x < (l == levels - 1 ? hallX0 - 4 : W - 8); x += 9)
                if (floor[l][x] != 0) spawnStamps.Add(new Stamp { X = x, Y = floor[l][x] - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        foreach (var c in chambers)
            spawnStamps.Add(new Stamp { X = (c.X0 + c.X1) * 0.5f, Y = c.Floor - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });

        void Catwalks()
        {
            foreach (var (sx, l, _) in shafts)
            {
                bool left = rng.Next(2) == 0;
                int bottom = floor[l + 1][sx + 4] - 4, top = floor[l][sx] + 1;
                for (int t = bottom; t > top; t -= 3)
                {
                    if (left) f.Solid(sx - 1, t, sx + 3, t + 2); else f.Solid(sx + 5, t, sx + 9, t + 2);
                    left = !left;
                }
            }
            // the halls get a stair of catwalks too
            foreach (var c in chambers.Where(c => c.Floor - c.Top >= 11))
            {
                int x = c.X0 + 3, dir = 1;
                for (int t = c.Floor - 4; t >= c.Top + 3; t -= 4)
                {
                    if (x + 6 > c.X1 - 3) { dir = -1; x = c.X1 - 3 - 6; }
                    else if (x < c.X0 + 3) { dir = 1; x = c.X0 + 3; }
                    f.Solid(x, t, x + 6, t + 1);
                    x += dir * 4;
                }
            }
        }
        cave.Open = f.Open;
        return Finish(cave, f, rng, new Vector2I(12, startFloor - 2), spawnStamps, 1, Catwalks, platforms: false);
    }
}
