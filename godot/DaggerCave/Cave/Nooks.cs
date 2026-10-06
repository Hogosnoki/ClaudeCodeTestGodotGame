using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    // ================================================================== hidden ways in, for creatures

    /// <summary>Test aid (<c>--gentest --forcenooks</c>): every level gets whichever hidden ways it can hold.</summary>
    public static bool ForceNooks;

    /// <summary>
    /// Two secrets only a creature can reach, each ending in a small hidden chamber with a silver (relic) chest:
    /// <b>a fish's slit</b> (under water, a crack in the side of a lake less than a cell tall, too small for any hero, a spider, or
    /// anything but a Shape Shifter's fish), and <b>a spider's crack</b> (a narrow vertical crack up out of a high roof, too high to
    /// jump to and too tight to climb for anyone but a spider's legs, ending in a chamber above). Carved after everything else; from
    /// the seed alone (online, every game makes the same).
    /// </summary>
    internal static void AddHiddenNooks(CaveData cave, int seed)
    {
        cave.Hints.RemoveAll(h => h.Kind is 0 or 1);
        var biome = cave.Biome;
        if (biome == null || cave.ReachMask == null || biome.Style == GenStyle.Arena || biome.Id is BiomeId.Entrance or BiomeId.Lair || biome.MinDepth < 1 && biome.Id != BiomeId.Abyss) return;
        var rng = new Random(seed * 53 + 29);
        double chance = ForceNooks ? 1.0 : 0.4;
        bool wet = cave.Liquid == Liquid.Water;
        if (wet && rng.NextDouble() < chance) FishSlit(cave, rng);
        if (cave.Liquid != Liquid.Lava && rng.NextDouble() < chance) SpiderCrack(cave, rng);
        cave.RockDepth = ComputeRockDepth(cave);
    }

    // is every cell in the box rock (a margin of untouched stone round whatever is carved)?
    private static bool BoxSolid(CaveData cave, int x0, int y0, int x1, int y1)
    {
        if (x0 < 4 || y0 < 4 || x1 > cave.W - 5 || y1 > cave.H - 5) return false;
        for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
                if (cave.CellOpen(i, j)) return false;
        return true;
    }

    // Opens the lattice point (i, j) so that the gap round it, between the iso-line on its two sides along `axis` (0: up and down;
    // 1: left and right), comes out `gap` cells wide. (The rock either side is whatever it is: the contour crosses between
    // the lattice points, where the field passes one half, so the point's value is found to put it just where we want it.)
    private static void OpenPoint(CaveData cave, int i, int j, int axis, float gap)
    {
        int stride = cave.W + 1;
        float a = cave.Open[(axis == 0 ? j - 1 : j) * stride + (axis == 0 ? i : i - 1)];
        float b = cave.Open[(axis == 0 ? j + 1 : j) * stride + (axis == 0 ? i : i + 1)];
        float lo = 0f, hi = 0.5f;
        for (int k = 0; k < 24; k++)
        {
            float t = (lo + hi) * 0.5f, v = 0.5f + t;
            float width = t / Math.Max(0.0001f, v - a) + t / Math.Max(0.0001f, v - b);
            if (width < gap) lo = t; else hi = t;
        }
        int idx = j * stride + i;
        cave.Open[idx] = Math.Max(cave.Open[idx], 0.5f + (lo + hi) * 0.5f);
    }

    // a round chamber (open values as the field's own domes), flat-floored if asked
    private static void CarveChamber(CaveData cave, float cx, float cy, float rx, float ry, bool flatFloor, float floorY = 0)
    {
        int stride = cave.W + 1;
        float rmin = Math.Min(rx, ry);
        for (int j = Math.Max(0, (int)(cy - ry - 3)); j <= Math.Min(cave.H, (int)(cy + ry + 3)); j++)
            for (int i = Math.Max(0, (int)(cx - rx - 3)); i <= Math.Min(cave.W, (int)(cx + rx + 3)); i++)
            {
                float ex = (i - cx) / rx, ey = (j - cy) / ry;
                float v = 0.5f + (1f - MathF.Sqrt(ex * ex + ey * ey)) * rmin * 0.5f;
                if (flatFloor) v = Math.Min(v, 0.5f + (floorY - j) * 0.5f);
                v = Math.Clamp(v, 0f, 1f);
                int k = j * stride + i;
                if (v > cave.Open[k]) cave.Open[k] = v;
            }
    }

    /// <summary>
    /// Out of the side of a lake's deep water, a horizontal slit (0.88 of a cell tall: 14 px, where a fish form is 11 and a spider 14.4
    /// across, so a spider form sticks fast too) into the rock, and at its end a round chamber, under water, with a chest.
    /// </summary>
    private static bool FishSlit(CaveData cave, Random rng)
    {
        int waterRow = (int)(cave.WaterY / CaveData.Cell);
        for (int tries = 0; tries < 800; tries++)
        {
            int j = rng.Next(waterRow + 10, cave.H - 14), i = rng.Next(10, cave.W - 10);
            if (!cave.CellOpen(i, j) || !cave.ReachMask[j * cave.W + i] || !cave.CellOpen(i, j - 3) || !cave.CellOpen(i, j + 1)) continue;
            int dir = rng.Next(2) == 0 ? -1 : 1;
            // the wall: the first rock along the row, close by
            int mi = i, steps = 0;
            while (steps < 8 && cave.CellOpen(mi + dir, j)) { mi += dir; steps++; }
            if (cave.CellOpen(mi + dir, j)) continue;
            int len = rng.Next(9, 14);
            float rx = 4.6f, ry = 3.3f;
            int cxi = mi + dir * (len + 5);
            int xa = Math.Min(mi + dir, cxi + dir * 6), xb = Math.Max(mi + dir, cxi + dir * 6);
            if (!BoxSolid(cave, xa, j - 7, xb, j + 7)) continue;
            // the slit, its mouth a little out of the wall's face
            for (int s = 1; s <= len; s++) OpenPoint(cave, mi + dir * s, j, 0, 0.88f);
            CarveChamber(cave, cxi, j, rx, ry, false);
            cave.Rooms.Add(new Room
            {
                Kind = RoomKind.Secret, Underwater = true,
                Center = new Vector2(cxi, j) * CaveData.Cell, Floor = new Vector2(cxi, j + ry - 0.5f) * CaveData.Cell,
                RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
            });
            cave.Hints.Add((new Vector2(mi + dir * 1.5f, j) * CaveData.Cell, 0));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Up out of a high roof (too high for a jump, from ground you can walk to), a vertical crack 1.4 cells across (22 px: a hero would
    /// fit if there were anything to hold, and a spider's legs take hold of both walls), and at its top a small chamber with a chest.
    /// </summary>
    private static bool SpiderCrack(CaveData cave, Random rng)
    {
        int waterRow = (int)(cave.WaterY / CaveData.Cell);
        for (int tries = 0; tries < 3000; tries++)
        {
            int i = rng.Next(14, cave.W - 14), j = rng.Next(20, cave.H - 14);
            // somewhere you can stand and walk to
            if (!cave.CellOpen(i, j) || cave.CellOpen(i, j + 1) || !cave.ReachMask[j * cave.W + i]) continue;
            if (cave.Liquid != Liquid.None && j + 1 > waterRow - 6) continue;
            if (cave.Boss != null && Math.Abs(i - cave.Boss.Center.X / CaveData.Cell) < cave.Boss.RxPx / CaveData.Cell + 12) continue;
            // the roof over it, at least seven cells up (and no reachable ledge within reach of it)
            int c = j;
            while (c > 6 && cave.CellOpen(i, c - 1)) c--;
            if (j - c < 7 || j - c > 30) continue;
            bool ledge = false;
            for (int y = c + 1; y < c + 6 && !ledge; y++)
                for (int x = i - 3; x <= i + 3 && !ledge; x++)
                    if (cave.CellOpen(x, y) && !cave.CellOpen(x, y + 1) && cave.ReachMask[y * cave.W + x]) ledge = true;
            if (ledge) continue;
            int len = rng.Next(10, 17);
            int floor = c - len;
            float rx = 5.4f, ry = 3.8f;
            if (floor - 6 < 5) continue;
            if (!BoxSolid(cave, i - 8, floor - 6, i + 8, c - 1)) continue;
            for (int y = c; y >= floor - 1; y--) OpenPoint(cave, i, y, 1, 1.4f);
            // (the chamber's floor has the crack for a hole at one side: the chest stands on the other)
            CarveChamber(cave, i + 2.2f, floor - 1f, rx, ry, true, floor);
            cave.Rooms.Add(new Room
            {
                Kind = RoomKind.Secret,
                Center = new Vector2(i + 3.2f, floor - 2.4f) * CaveData.Cell, Floor = new Vector2(i + 3.2f, floor) * CaveData.Cell,
                RxPx = 4f * CaveData.Cell, RyPx = 3f * CaveData.Cell,
            });
            cave.Hints.Add((new Vector2(i + 0.5f, c + 0.5f) * CaveData.Cell, 1));
            return true;
        }
        return false;
    }
}
