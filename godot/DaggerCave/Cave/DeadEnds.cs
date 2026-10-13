using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    /// <summary>
    /// The dead ends of a finished level (world px, a floor each): the tips of its branches, where the path from the start stops going on.
    /// Found the same way for every kind of cave: a flood of the open cells from the start, and the cells no cell nearby is farther along than
    /// (the far end of a tunnel, of a pocket, of a room off to one side). Kept apart from one another, away from the start, and out of the
    /// guardian's room and the vaults' and hidden chambers' own (they hold a reward already). The gold is hidden in them (see Main.PlaceGold).
    /// </summary>
    internal static void AddDeadEnds(CaveData cave)
    {
        cave.DeadEnds.Clear();
        if (cave.ReachMask == null) return;
        int w = cave.W, h = cave.H;
        var dist = new int[w * h];
        Array.Fill(dist, -1);
        int si = Math.Clamp((int)(cave.StartPos.X / CaveData.Cell), 0, w - 1), sj = Math.Clamp((int)(cave.StartPos.Y / CaveData.Cell) - 1, 0, h - 1);
        if (!cave.CellOpen(si, sj)) return;
        var q = new Queue<int>();
        dist[sj * w + si] = 0; q.Enqueue(sj * w + si);
        int far = 0;
        while (q.Count > 0)
        {
            int u = q.Dequeue(); int ui = u % w, uj = u / w;
            for (int k = 0; k < 4; k++)
            {
                int i = ui + (k == 0 ? -1 : k == 1 ? 1 : 0), j = uj + (k == 2 ? -1 : k == 3 ? 1 : 0);
                if (i < 0 || j < 0 || i >= w || j >= h) continue;
                int v = j * w + i;
                if (dist[v] >= 0 || !cave.CellOpen(i, j)) continue;
                dist[v] = dist[u] + 1; far = Math.Max(far, dist[v]); q.Enqueue(v);
            }
        }
        const int R = 9;
        var maxima = new List<(int i, int j, int d)>();
        for (int j = 2; j < h - 2; j++)
            for (int i = 2; i < w - 2; i++)
            {
                int d = dist[j * w + i];
                if (d < 20 || !cave.ReachMask[j * w + i]) continue;
                bool best = true;
                for (int dj = -R; dj <= R && best; dj++)
                    for (int di = -R; di <= R; di++)
                    {
                        if (di * di + dj * dj > R * R) continue;
                        int ii = i + di, jj = j + dj;
                        if (ii < 0 || jj < 0 || ii >= w || jj >= h) continue;
                        if (dist[jj * w + ii] > d) { best = false; break; }
                    }
                if (best) maxima.Add((i, j, d));
            }
        maxima.Sort((a, b) => b.d.CompareTo(a.d));
        var kept = new List<Vector2>();
        foreach (var (i, j, d) in maxima)
        {
            var cell = new Vector2(i + 0.5f, j + 0.5f);
            var at = cell * CaveData.Cell;
            if (at.DistanceTo(cave.StartPos) < 26 * CaveData.Cell) continue;
            bool taken = false;
            foreach (var k in kept) if (k.DistanceTo(cell) < 14f) { taken = true; break; }
            if (taken) continue;
            // (not the guardian's room, nor a vault's or a hidden chamber's: each holds a reward already)
            bool reward = false;
            if (cave.Boss != null && at.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 3 * CaveData.Cell) reward = true;
            foreach (var r in cave.Rooms)
                if (r.Kind == RoomKind.Secret && at.DistanceTo(r.Center) < Math.Max(r.RxPx, r.RyPx) + 2 * CaveData.Cell) reward = true;
            foreach (var v in new[] { cave.Vault, cave.ExtraVault })
                if (v != null && (at.DistanceTo(v.Chest) < 12 * CaveData.Cell || at.DistanceTo(v.Gate) < 8 * CaveData.Cell)) reward = true;
            if (reward) continue;
            if (!cave.FindFloor(at, 400, out var floor) || floor.DistanceTo(at) > 14 * CaveData.Cell || cave.IsLava(floor + new Vector2(0, -6))) continue;
            kept.Add(cell);
            cave.DeadEnds.Add(floor);
        }
    }
}
