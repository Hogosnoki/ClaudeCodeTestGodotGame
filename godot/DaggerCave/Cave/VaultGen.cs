using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    /// <summary>
    /// Cuts the level's vault into the rock. From a floor you can walk to (well away from the
    /// start and the guardian, and dry), beside a tunnel wall that is a thick slab of rock, a
    /// straight passage runs into the rock and opens into a chamber, both floored level with the
    /// tunnel. The farthest such spot from the start wins; where there's none, the vault is the
    /// treasury beyond the guardian chamber's far wall. It only adds cells you can walk into
    /// and back out of, so it runs after the traversal check and after the spawn points (no
    /// creature starts inside it). Main puts the iron gate across the passage and the chest in
    /// the chamber. Nothing is cut in the dragon's lair.
    /// </summary>
    internal static void CarveVault(CaveData cave, Random rng)
    {
        cave.Vault = null;
        if (B == null || B.Style == GenStyle.Arena || cave.ReachMask == null) return;
        int corridor = Tune.Vault.CorridorCells, chamber = Tune.Vault.ChamberCells, len = corridor + chamber;
        int cRows = Tune.Vault.CorridorRows, rRows = Tune.Vault.ChamberRows;
        var start = cave.StartPos / CaveData.Cell;
        float wrow = cave.WaterY / CaveData.Cell;
        bool Open(int i, int j) => cave.CellOpen(i, j);
        bool Reach(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && cave.ReachMask[j * W + i];

        // is column d of a cut from (i, j) toward s rock from rows top..bottom?
        bool Rock(int i, int j, int s, int d, int top, int bottom)
        {
            int x = i + s * d;
            if (x < 4 || x > W - 5) return false;
            for (int y = top; y <= bottom; y++)
                if (y < 3 || y > H - 4 || Open(x, y)) return false;
            return true;
        }

        int bi = 0, bj = 0, bs = 0, bk = 0, bg = 0;
        // (first clear of the guardian's chamber; failing that, the treasury beyond its far wall.
        // In the entrance corridor that's the only wall there is.)
        for (int pass = B.Style == GenStyle.Corridor ? 1 : 0; pass < 2; pass++)
        {
            // every floor you can stand on and walk to, with room overhead, dry, away from the
            // start (and the guardian): the candidates
            var floors = new List<Vector2I>();
            for (int j = rRows + 6; j < H - 5; j++)
            {
                if (j + 3 > wrow) break;
                for (int i = 4; i < W - 4; i++)
                {
                    // (on solid ground, not a thin ledge the cut's rim could notch)
                    if (!Reach(i, j) || !Open(i, j) || Open(i, j + 1) || Open(i, j + 2) || !Open(i, j - 1)) continue;
                    if (new Vector2(i, j).DistanceTo(start) < Tune.Vault.MinFromStart) continue;
                    if (pass == 0 && cave.Boss != null && new Vector2(i, j).DistanceTo(cave.Boss.Center / CaveData.Cell) < cave.Boss.RxPx / CaveData.Cell + 12) continue;
                    floors.Add(new Vector2I(i, j));
                }
            }
            float best = -1;
            for (int tries = 0; tries < 1500 && floors.Count > 0; tries++)
            {
                var f = floors[rng.Next(floors.Count)];
                int i = f.X, j = f.Y;
                float fromStart = new Vector2(i, j).DistanceTo(start);
                int first = rng.Next(2) == 0 ? -1 : 1;
                for (int t = 0; t < 2; t++)
                {
                    int s = t == 0 ? first : -first;
                    // the rock: the first solid cell along the floor, a step or three away, over flat
                    // floor with room overhead all the way to it
                    int k0 = 1;
                    while (k0 <= 3 && Open(i + s * k0, j))
                    {
                        if (Open(i + s * k0, j + 1) || Open(i + s * k0, j + 2) || !Open(i + s * k0, j - 1)) { k0 = 99; break; }
                        k0++;
                    }
                    if (k0 > 3) continue;
                    // the doorstep: up to three cells where the tunnel's air may still hang over the
                    // passage (a wall that slopes or curves up), with rock under its floor; the gate
                    // stands on the first cell with rock over it, and from there on the whole cut, with
                    // a skin of rock round it (two cells over and under it, and past its far end), must
                    // be solid: it opens only into the tunnel it starts from
                    int gate = -1;
                    for (int g = 1; g <= 3 && gate < 0; g++)
                    {
                        if (!Rock(i, j, s, k0 + g - 1, j + 1, j + 2)) break;
                        int total = g + corridor - 1 + chamber;
                        bool ok = true;
                        for (int d = g; d < total + 2 && ok; d++)
                        {
                            int rows = d < g + corridor - 1 ? cRows : rRows;
                            ok = Rock(i, j, s, k0 + d, j - rows - 1, j + 2);
                        }
                        if (ok) gate = g;
                    }
                    if (gate < 0) continue;
                    // (the shortest doorstep, farthest from the start, a little shuffled)
                    float score = fromStart - gate * 4f + (float)rng.NextDouble() * 25f;
                    if (score > best) { best = score; bi = i; bj = j; bs = s; bk = k0; bg = gate; }
                }
            }
            if (best >= 0) break;
            if (pass == 1) return;
        }

        // the cells cut: the doorstep and the passage (the gate across its cell bg), then the chamber
        int plen = bg + corridor - 1;
        int xa = bi + bs * bk, xb = bi + bs * (bk + plen - 1);
        int xc = bi + bs * (bk + plen), xd = bi + bs * (bk + plen + chamber - 1);
        var passage = new Rect2I(Math.Min(xa, xb), bj - cRows + 1, plen, cRows);
        var room = new Rect2I(Math.Min(xc, xd), bj - rRows + 1, chamber, rRows);
        var cut = new HashSet<(int, int)>();
        foreach (var r in new[] { passage, room })
            for (int y = r.Position.Y; y < r.End.Y; y++)
                for (int x = r.Position.X; x < r.End.X; x++)
                    cut.Add((x, y));
        // the field's corners: open inside, and just open along the rim, so the walls fall on the
        // cut's edges (square ones: masons cut it), and never less open than they were
        int stride = W + 1;
        var done = new HashSet<int>();
        foreach (var (x, y) in cut)
            for (int cy = y; cy <= y + 1; cy++)
                for (int cx = x; cx <= x + 1; cx++)
                {
                    int k = cy * stride + cx;
                    if (!done.Add(k)) continue;
                    int inside = (cut.Contains((cx - 1, cy - 1)) ? 1 : 0) + (cut.Contains((cx, cy - 1)) ? 1 : 0)
                               + (cut.Contains((cx - 1, cy)) ? 1 : 0) + (cut.Contains((cx, cy)) ? 1 : 0);
                    cave.Open[k] = Math.Max(cave.Open[k], inside == 4 ? 1f : 0.52f);
                }

        float cell = CaveData.Cell;
        // the gate stands across the passage, past its doorstep
        int gx = bi + bs * (bk + bg);
        float chestX = (room.Position.X + room.Size.X * 0.5f) * cell;
        cave.Vault = new VaultSpot
        {
            Side = bs,
            Approach = new Vector2((bi + 0.5f) * cell, (bj + 1) * cell),
            Gate = new Vector2((gx + 0.5f) * cell, (bj + 1) * cell),
            GateTop = (bj - cRows + 1) * cell,
            Chest = new Vector2(chestX, (bj + 1) * cell),
            Passage = passage,
            Chamber = room,
            Center = new Vector2(chestX, (bj + 1 - rRows * 0.5f) * cell),
        };
    }
}
