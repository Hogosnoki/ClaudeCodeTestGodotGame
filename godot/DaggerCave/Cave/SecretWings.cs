using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A hidden wing of a level: a corridor and a chamber past the cave's right-hand edge, shut off by a plug of rubble (a breakable wall).
/// The camera keeps to the main occupied area (<see cref="CaveData.ViewRect"/>), so the wing cannot be seen until the wall is down; then
/// the view is let out to take it in (see Main.OpenWing).
/// </summary>
public sealed class SecretWing
{
    /// <summary>Where the plug stands (px) and which of the level's rubble plugs it is.</summary>
    public Vector2 Plug;
    public int RubbleIndex;
    /// <summary>The wing's whole extent (px): the corridor and the chamber, the view it opens up.</summary>
    public Rect2 Bounds;
    public Room Chamber;
}

public static partial class CaveGenerator
{
    /// <summary>--wing: every level gets a secret wing (where one can be cut).</summary>
    public static bool ForceWings;

    /// <summary>The cells of the open space (once, as a grid), the bounds of it, and the last open column of each row.</summary>
    private static (bool[] open, int x0, int x1, int y0, int y1, int[] lastOpen) OpenGrid(CaveData cave)
    {
        var oc = new bool[cave.W * cave.H];
        int x0 = cave.W, x1 = -1, y0 = cave.H, y1 = -1;
        var last = new int[cave.H];
        for (int j = 0; j < cave.H; j++)
        {
            last[j] = -1;
            for (int i = 0; i < cave.W; i++)
                if (cave.CellOpen(i, j)) { oc[j * cave.W + i] = true; x0 = Math.Min(x0, i); x1 = Math.Max(x1, i); y0 = Math.Min(y0, j); y1 = Math.Max(y1, j); last[j] = i; }
        }
        return (oc, x0, x1, y0, y1, last);
    }

    /// <summary>
    /// The last step of a level: its view is kept to the main occupied area, and (now and then) a hidden wing is cut past its right-hand edge,
    /// behind a wall of rubble at the end of a passage. The map is made wider for it (the rock out past the edge).
    /// </summary>
    internal static void AddSecretWings(CaveData cave, int seed)
    {
        var (oc, x0, x1, y0, y1, last) = OpenGrid(cave);
        int pad = Tune.Secrets.ViewPadCells;
        int vx0 = Math.Max(0, x0 - pad), vy0 = Math.Max(0, y0 - pad), vx1 = Math.Min(cave.W, x1 + 1 + pad), vy1 = Math.Min(cave.H, y1 + 1 + pad);
        cave.ViewRect = new Rect2(vx0 * CaveData.Cell, vy0 * CaveData.Cell, (vx1 - vx0) * CaveData.Cell, (vy1 - vy0) * CaveData.Cell);
        if (B == null || B.Style == GenStyle.Arena || B.MinDepth < 1 || cave.ReachMask == null) return;
        var rng = new Random(seed * 193 + 77);
        if (!ForceWings && rng.NextDouble() > Tune.Secrets.WingChance) return;

        int W0 = cave.W, H0 = cave.H;
        float wrow = cave.Liquid == Liquid.None ? float.MaxValue : cave.WaterY / CaveData.Cell;
        bool Open(int i, int j) => i >= 0 && j >= 0 && i < W0 && j < H0 && oc[j * W0 + i];
        // the floors where the cave reaches its right-hand extreme for four rows of headroom, within reach of the edge, and dry
        var cands = new List<(int i, int j, int wx)>();
        for (int j = 8; j < H0 - 6; j++)
        {
            if (j + 1 >= wrow - 3) continue;
            int band = -1;
            for (int y = j - 4; y <= j + 1; y++) band = Math.Max(band, last[y]);
            for (int i = Math.Max(6, band - 2); i <= band && i < W0 - 2; i++)
            {
                if (!Open(i, j) || Open(i, j + 1) || !Open(i, j - 1) || !Open(i, j - 2) || !Open(i, j - 3)) continue;
                if (!cave.ReachMask[j * W0 + i]) continue;
                // (never out of a vault: its chamber's end is not the cave's edge)
                bool inVault = false;
                foreach (var v in new[] { cave.Vault, cave.ExtraVault })
                    if (v != null && i >= v.Passage.Position.X - 6 && i <= Math.Max(v.Chamber.End.X, v.Passage.End.X) + 6 && Math.Abs(j - v.Chamber.End.Y) < 14) inVault = true;
                if (inVault) continue;
                int wx = band + 1;
                if (W0 - wx > Tune.Secrets.MaxReachCells) continue;
                cands.Add((i, j, wx));
            }
        }
        if (cands.Count == 0) return;
        // (the ones nearest the right-hand edge of the main area, one of the best few)
        cands.Sort((a, b) => b.wx.CompareTo(a.wx));
        var pick = cands[rng.Next(Math.Min(cands.Count, Math.Max(3, cands.Count / 6)))];
        int fi = pick.i, fj = pick.j, wallX = pick.wx;

        // ---- widen the map: more rock on the right, for the wing
        int padCells = Tune.Secrets.PadCells;
        int W1 = W0 + padCells;
        cave.Open = Relayout(cave.Open, W0 + 1, W1 + 1, H0 + 1);
        cave.ReachMask = Relayout(cave.ReachMask, W0, W1, H0);
        cave.TrapMask = cave.TrapMask == null ? null : Relayout(cave.TrapMask, W0, W1, H0);
        cave.OpenCells = null;
        cave.W = W1;
        int stride = W1 + 1;

        // ---- the passage (four rows tall, floored level with the cave's floor) and the chamber at its end
        float rx = Rf(rng, 8.5f, 11f), ry = Rf(rng, 5f, 6.2f);
        float cx = W0 + 3 + rx;
        float floorY = fj + 1;
        var cut = new HashSet<(int, int)>();
        for (int x = fi + 1; x <= (int)(cx - rx * 0.5f); x++)
            for (int y = fj - 3; y <= fj; y++) cut.Add((x, y));
        var done = new HashSet<int>();
        foreach (var (x, y) in cut)
            for (int cy = y; cy <= y + 1; cy++)
                for (int cxx = x; cxx <= x + 1; cxx++)
                {
                    int k = cy * stride + cxx;
                    if (!done.Add(k)) continue;
                    int inside = (cut.Contains((cxx - 1, cy - 1)) ? 1 : 0) + (cut.Contains((cxx, cy - 1)) ? 1 : 0) + (cut.Contains((cxx - 1, cy)) ? 1 : 0) + (cut.Contains((cxx, cy)) ? 1 : 0);
                    cave.Open[k] = Math.Max(cave.Open[k], inside == 4 ? 1f : 0.52f);
                }
        // (a domed chamber with a flat floor, the rock round it left as it was)
        float rmin = Math.Min(rx, ry);
        for (int y = (int)(floorY - ry - 3); y <= (int)floorY + 1; y++)
            for (int x = (int)(cx - rx - 3); x <= (int)(cx + rx + 3); x++)
            {
                if (x < 0 || x > W1 || y < 0 || y > H0) continue;
                float ex = (x - cx) / rx, ey = (y - floorY) / ry;
                float e = MathF.Sqrt(ex * ex + ey * ey);
                float v = 0.5f + (1f - e) * rmin * 0.5f;
                v = Math.Min(v, 0.5f + (floorY - y) * 0.5f);
                int k = y * stride + x;
                cave.Open[k] = Math.Max(cave.Open[k], Math.Clamp(v, 0f, 1f));
            }
        // (no corner on the new map's edge is open: it stays a skin of rock)
        for (int y = 0; y <= H0; y++) for (int x = W1 - 2; x <= W1; x++) cave.Open[y * stride + x] = 0f;

        var chamber = new Room
        {
            Kind = RoomKind.Secret, Center = new Vector2(cx, floorY - ry * 0.45f) * CaveData.Cell, Floor = new Vector2(cx, floorY - 0.5f) * CaveData.Cell,
            RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
        };
        cave.Rooms.Add(chamber);
        // ---- the wall: a plug of rubble in the passage's mouth, flush with the cave's wall
        var size = new Vector2(2f, 4f) * CaveData.Cell;
        var plug = new Vector2(fi + 2f, fj - 1f) * CaveData.Cell;
        cave.Rubble.Add((plug, size));
        float top = (floorY - ry * 2f - 3f) * CaveData.Cell;
        cave.Wings.Add(new SecretWing
        {
            Plug = plug, RubbleIndex = cave.Rubble.Count - 1, Chamber = chamber,
            Bounds = new Rect2((fi - 2) * CaveData.Cell, top, (W1 - fi + 2) * CaveData.Cell, (floorY + 3) * CaveData.Cell - top),
        });
        // (the rock's depth is measured over the new grid)
        W = W1;
        try { cave.RockDepth = ComputeRockDepth(cave); } finally { W = W0; }
    }

    private static float Rf(Random rng, float a, float b) => a + (b - a) * (float)rng.NextDouble();

    /// <summary>A grid of <paramref name="rows"/> rows widened on the right (the old cells where they were, the rest default).</summary>
    private static T[] Relayout<T>(T[] src, int oldW, int newW, int rows)
    {
        var dst = new T[newW * rows];
        for (int r = 0; r < rows; r++) Array.Copy(src, r * oldW, dst, r * newW, oldW);
        return dst;
    }
}
