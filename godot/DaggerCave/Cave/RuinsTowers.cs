using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    /// <summary>
    /// The ruins' second plan (about one ruin in two, by the seed): not floors of halls side by side but a row of drowned towers
    /// standing in the rock. Each tower is a stack of storeys with a hole at alternate ends of each floor and a ladder of slabs up
    /// under it; neighbouring towers are joined by bridges at the storeys they share, and no two towers rise or sink to the same
    /// heights. So the way on is up and down inside a tower, across to the next at whichever storey a bridge leaves from, and so
    /// on, to the guardian's hall at the foot of the last. The way in is the top of the first tower.
    /// </summary>
    private static CaveData GenerateTowers(int seed)
    {
        var rng = new Random(unchecked(seed * 7 + 3));
        int RndI(int a, int bIncl) => rng.Next(a, bIncl + 1);
        var f = new Field(seed, 0.6f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };
        // the storeys: one set of floor heights for every tower, so a bridge meets a floor at each end
        int spacing = RndI(9, 11), top = 15;
        var S = new List<int>();
        for (int y = top; y <= H - 8; y += spacing) S.Add(y);
        int K = S.Count;
        int hallW = 30, hallX0 = W - 5 - hallW;
        // the towers across the map, each over a run of storeys that overlaps its neighbour's
        var towers = new List<(int x0, int x1, int a, int b)>();
        int x = 5;
        int prevA = 0, prevB = Math.Min(K - 1, RndI(1, Math.Max(1, K - 2)));
        while (true)
        {
            int w = RndI(14, 20);
            if (x + w > hallX0 - 10) break;
            int a, b;
            if (towers.Count == 0) { a = 0; b = prevB; }
            else
            {
                // (it starts no lower than the storey above the bottom, so it has a storey below to reach, and it overlaps the last)
                a = RndI(0, Math.Min(prevB, K - 2));
                b = RndI(Math.Min(K - 1, Math.Max(a + 1, prevA)), K - 1);
            }
            towers.Add((x, x + w, a, b));
            prevA = a; prevB = b;
            x += w + RndI(12, 22);
        }
        if (towers.Count < 2 || K < 3) return GenerateRuinsPlain(seed);
        // the last tower must reach the bottom storey, to meet the guardian's hall
        var last = towers[^1];
        towers[^1] = (last.x0, last.x1, last.a, K - 1);
        var holes = new List<(int sx, int k)>();
        var spawnStamps = new List<Stamp>();
        var rooms = new List<(float cx, int floor, float rx)>();
        for (int t = 0; t < towers.Count; t++)
        {
            var (x0, x1, a, b) = towers[t];
            f.Rect(x0, S[a] - spacing + 2, x1, S[b]);
            bool left = rng.Next(2) == 0;
            for (int k = a; k <= b; k++)
            {
                rooms.Add(((x0 + x1) * 0.5f, S[k], (x1 - x0) * 0.5f));
                for (float sxs = x0 + 3; sxs < x1 - 2; sxs += 6) spawnStamps.Add(new Stamp { X = sxs, Y = S[k] - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
                if (k == b) continue;
                // the floor between this storey and the next, with its hole at one end (the next floor's at the other)
                int sx = left ? x0 + 1 : x1 - 9;
                holes.Add((sx, k));
                left = !left;
            }
        }
        // bridges between neighbours, at one or two of the storeys they share
        for (int t = 1; t < towers.Count; t++)
        {
            var p = towers[t - 1]; var q = towers[t];
            int lo = Math.Max(p.a, q.a), hi = Math.Min(p.b, q.b);
            var shared = Enumerable.Range(lo, Math.Max(0, hi - lo + 1)).OrderBy(_ => rng.Next()).Take(RndI(1, 2)).ToList();
            if (shared.Count == 0) shared.Add(Math.Clamp(hi, 0, K - 1));
            foreach (int k in shared) f.Rect(p.x1 - 1, S[k] - 5, q.x0 + 1, S[k]);
        }
        // the guardian's hall at the foot of the last tower
        var lt = towers[^1];
        int hallF = S[K - 1];
        f.Rect(lt.x1 - 1, hallF - 5, hallX0 + 1, hallF);
        f.Rect(hallX0, hallF - 13, W - 5, hallF);

        var t0 = towers[0];
        float scx = (t0.x0 + t0.x1) * 0.5f;
        int startFloor = S[t0.a];
        cave.Rooms.Add(MakeRoom(RoomKind.Start, scx, startFloor - 3, startFloor, (t0.x1 - t0.x0) * 0.5f, spacing - 2));
        foreach (var (cx, floor, rx) in rooms)
            if (!(Math.Abs(cx - scx) < 1 && floor == startFloor))
                cave.Rooms.Add(MakeRoom(RoomKind.Treasure, cx, floor - (spacing - 2) * 0.5f, floor, rx, (spacing - 2) * 0.5f));
        var boss = MakeRoom(RoomKind.Boss, (hallX0 + W - 5) * 0.5f, hallF - 5.5f, hallF, 15, 6.5f);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        cave.StartPos = new Vector2(scx, startFloor - 1.2f) * CaveData.Cell;
        AssignMiniBosses(cave, rng, 40);

        void Floors()
        {
            foreach (var (x0, x1, a, b) in towers)
                for (int k = a; k < b; k++)
                {
                    var (sx, _) = holes.First(h => h.k == k && h.sx >= x0 - 1 && h.sx <= x1);
                    // the floor, but for the hole
                    if (sx > x0 + 2) f.Solid(x0 - 1, S[k], sx - 1, S[k] + 2);
                    if (sx + 9 < x1 - 1) f.Solid(sx + 9, S[k], x1 + 1, S[k] + 2);
                    // the ladder up under the hole: slabs 3 rows apart, alternating walls, a channel down the middle to drop through
                    bool l = rng.Next(2) == 0;
                    for (int r = S[k + 1] - 3; r > S[k] + 2; r -= 3)
                    {
                        if (l) f.Solid(sx - 1, r, sx + 3, r + 2); else f.Solid(sx + 5, r, sx + 9, r + 2);
                        l = !l;
                    }
                }
        }
        cave.Landmark = "the Drowned Towers";
        cave.Open = f.Open;
        return Finish(cave, f, rng, new Vector2I((int)scx, startFloor - 2), spawnStamps, 1, Floors, platforms: false);
    }
}
