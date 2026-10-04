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
        int W = cave.W, H = cave.H;
        if (cave.ReachMask == null || cave.ReachMask.Length < W * H) return;
        var rng = new Random(cave.Seed * 7919 + 13);
        float cell = CaveData.Cell;
        int start = (int)(cave.StartPos.X / cell);

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
                if (h < 3 || h > 6) continue;
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
                    if (hh < 3 || hh > 7) ok = false;
                }
                if (!ok || !Open(i - 5, fj - 1) || !Open(i + 5, fj - 1)) continue;
                cands.Add((i, fj, h));
            }
        if (cands.Count == 0) return;

        // (shuffled, then taken in turn while they keep apart from rooms, the vault and one another)
        for (int k = cands.Count - 1; k > 0; k--) { int m = rng.Next(k + 1); (cands[k], cands[m]) = (cands[m], cands[k]); }
        int want = rng.Next(Tune.Rubble.Min, Tune.Rubble.Max + 1);
        foreach (var (i, fj, h) in cands)
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
            if (bad) continue;
            cave.Rubble.Add((centre, new Vector2(2f * cell, h * cell)));
        }
    }
}
