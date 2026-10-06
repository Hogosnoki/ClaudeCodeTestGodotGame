using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Finds narrow, level passages (a few cells tall, a few long) that the player can reach, and plugs some of them with boulders.</summary>
internal static class RubbleGen
{
    internal static void Build(CaveData cave)
    {
        cave.Rubble.Clear();
        cave.RubbleAtDeadEnds = 0;
        // (none in the first level)
        if (cave.Biome?.Id == BiomeId.Entrance) return;
        int W = cave.W, H = cave.H;
        if (cave.ReachMask == null || cave.ReachMask.Length < W * H) return;
        var rng = new Random(cave.Seed * 7919 + 13);
        float cell = CaveData.Cell;
        int start = (int)(cave.StartPos.X / cell);
        // (the catacombs' vaulted passages and long stairs are taller than a natural cave's: their banks of skulls stand higher)
        int maxH = cave.Biome?.Ossuary == true ? 8 : 6;

        bool Open(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && cave.CellOpen(i, j);
        // the headroom above a floor cell (open cells counted up to a ceiling), or 0 if this isn't a floor
        int Head(int i, int fj)
        {
            if (!Open(i, fj) || Open(i, fj + 1)) return 0;
            int h = 0;
            while (h < 9 && Open(i, fj - h)) h++;
            return h;
        }

        var cands = new List<(int i, int fj, int h)>();
        for (int i = 8; i < W - 8; i++)
            for (int fj = 8; fj < H - 8; fj++)
            {
                int h = Head(i, fj);
                if (h < 3 || h > maxH) continue;
                if (!cave.ReachMask[fj * W + i]) continue;
                if (Math.Abs(i - start) < Tune.Rubble.KeepFromStart) continue;
                float wy = (fj + 1) * cell;
                if (cave.Liquid != Liquid.None && wy > cave.WaterY - 8 * cell) continue;
                // a level corridor: three cells to each side the same shape, and more open beyond
                bool ok = true;
                for (int d = -3; d <= 3 && ok; d++)
                {
                    // (the floor may step a cell up or down along it)
                    int hh = Math.Max(Head(i + d, fj), Math.Max(Head(i + d, fj - 1), Head(i + d, fj + 1)));
                    if (hh < 3 || hh > maxH + 1) ok = false;
                }
                if (!ok || !Open(i - 5, fj - 1) || !Open(i + 5, fj - 1)) continue;
                cands.Add((i, fj, h));
            }
        // (and a looser sort of place, for the mouths of dead ends only: a passage that is only level over a cell or two to each side)
        var loose = new List<(int i, int fj, int h)>();
        var candSet = new HashSet<(int, int, int)>(cands);
        for (int i = 8; i < W - 8; i++)
            for (int fj = 8; fj < H - 8; fj++)
            {
                int h = Head(i, fj);
                if (h < 3 || h > maxH || !cave.ReachMask[fj * W + i] || Math.Abs(i - start) < Tune.Rubble.KeepFromStart) continue;
                if (cave.Liquid != Liquid.None && (fj + 1) * cell > cave.WaterY - 8 * cell) continue;
                bool ok = true;
                for (int d = -2; d <= 2 && ok; d += 1)
                {
                    int hh = Math.Max(Head(i + d, fj), Math.Max(Head(i + d, fj - 1), Head(i + d, fj + 1)));
                    if (hh < 3 || hh > maxH + 1) ok = false;
                }
                if (ok && !candSet.Contains((i, fj, h))) loose.Add((i, fj, h));
            }
        if (CaveGenerator.Verbose) GD.Print($"      rubble: {cands.Count} candidates, {loose.Count} loose");
        if (cands.Count == 0 && loose.Count == 0) return;

        // (shuffled, then taken in turn while they keep apart from rooms, the vault and one another)
        for (int k = cands.Count - 1; k > 0; k--) { int m = rng.Next(k + 1); (cands[k], cands[m]) = (cands[m], cands[k]); }
        int want = rng.Next(Tune.Rubble.Min, Tune.Rubble.Max + 1);

        // Preferably a plug sits at the mouth of a dead end: a side passage that goes nowhere but to what is hidden in it. The
        // plug's own cells are walled off and what can be reached from each side is counted (up to a cap); a side that runs out
        // with neither the start nor the guardian in it is a pocket, and the bigger it is the nearer the plug is to its mouth.
        var seen = new int[W * H];
        int stamp = 0;
        var queue = new Queue<int>();
        int bossCell = cave.Boss != null ? (int)(cave.Boss.Floor.Y / cell - 1) * W + (int)(cave.Boss.Floor.X / cell) : -1;
        int startCell = (int)(cave.StartPos.Y / cell) * W + start;
        const int Cap = 2600;
        int PocketSize(int from, HashSet<int> wall)
        {
            if (wall.Contains(from) || !cave.CellOpen(from % W, from / W)) return -1;
            stamp++;
            queue.Clear(); queue.Enqueue(from); seen[from] = stamp;
            int n = 0;
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (++n > Cap) return -1;
                if (u == startCell || u == bossCell || u == bossCell - W) return -1;
                int ui = u % W, uj = u / W;
                for (int d = 0; d < 4; d++)
                {
                    int vi = ui + (d == 0 ? 1 : d == 1 ? -1 : 0), vj = uj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (vi < 0 || vj < 0 || vi >= W || vj >= H) continue;
                    int v = vj * W + vi;
                    if (seen[v] == stamp || wall.Contains(v) || !cave.CellOpen(vi, vj)) continue;
                    seen[v] = stamp; queue.Enqueue(v);
                }
            }
            return n;
        }
        int pocketPlugs = 0;
        var pockets = new List<((int i, int fj, int h) c, int size)>();
        int tried = 0;
        for (int k = loose.Count - 1; k > 0; k--) { int m = rng.Next(k + 1); (loose[k], loose[m]) = (loose[m], loose[k]); }
        var probe = new List<(int i, int fj, int h)>(cands);
        probe.AddRange(loose);
        foreach (var c in probe)
        {
            if (++tried > 500) break;
            var wall = new HashSet<int>();
            for (int di = -1; di <= 1; di++)
                for (int hh = 0; hh < c.h; hh++) wall.Add((c.fj - hh) * W + c.i + di);
            int a = PocketSize(c.fj * W + c.i - 4, wall) , b = PocketSize(c.fj * W + c.i + 4, wall);
            int size = Math.Max(a, b);
            // (both sides closed off would be a sealed hole: not one the plug can be walked up to)
            if (size >= 70 && !(a > 0 && b > 0)) { pockets.Add((c, size)); pocketPlugs++; }
        }
        pockets.Sort((x, y) => y.size.CompareTo(x.size));
        if (CaveGenerator.Verbose) GD.Print($"      rubble: {cands.Count} candidates, {pockets.Count} dead-end mouths, want {want}");
        var order = new List<(int i, int fj, int h)>();
        var deadEnd = new HashSet<(int, int, int)>();
        foreach (var (c, _) in pockets) { order.Add(c); deadEnd.Add(c); }
        var inOrder = new HashSet<(int, int, int)>(order);
        foreach (var c in cands) if (inOrder.Add(c)) order.Add(c);
        int rejected = 0;
        foreach (var (i, fj, h) in order)
        {
            if (cave.Rubble.Count >= want) break;
            var centre = new Vector2((i + 0.5f) * cell, (fj + 1) * cell - h * cell * 0.5f);
            bool bad = false;
            foreach (var r in cave.Rooms)
                if (Math.Abs(r.Center.X - centre.X) < r.RxPx + 6 * cell && Math.Abs(r.Center.Y - centre.Y) < r.RyPx + 6 * cell) { bad = true; break; }
            if (!bad && cave.Boss != null && cave.Boss.Center.DistanceTo(centre) < cave.Boss.RxPx + 10 * cell) bad = true;
            if (!bad && cave.Vault != null && (cave.Vault.Gate.DistanceTo(centre) < 12 * cell || cave.Vault.Chest.DistanceTo(centre) < 12 * cell)) bad = true;
            if (!bad && cave.Mouth is Vector2 mo && mo.DistanceTo(centre) < 20 * cell) bad = true;
            if (!bad) foreach (var (p, _) in cave.Rubble) if (p.DistanceTo(centre) < 28 * cell) { bad = true; break; }
            if (bad) { if (CaveGenerator.Verbose && deadEnd.Contains((i, fj, h))) rejected++; continue; }
            cave.Rubble.Add((centre, new Vector2(2f * cell, h * cell)));
            if (deadEnd.Contains((i, fj, h))) cave.RubbleAtDeadEnds++;
        }
        if (CaveGenerator.Verbose) GD.Print($"      rubble: placed {cave.Rubble.Count} ({cave.RubbleAtDeadEnds} at dead ends), {rejected} dead-end mouths turned down");
    }
}
