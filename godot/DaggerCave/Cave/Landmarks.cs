using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Landmarks: one or two great structures cut through a finished cave, so that how you get about it differs from level to level and
/// not only its outline. They are cut after the cave is carved and before the ledges are laid and the reach is checked, so the same
/// checks and repairs that keep every cave sound keep these sound too.
/// <list type="bullet">
/// <item><b>The Great Shaft</b>: a tall well down through several levels of the cave. The ledge pass strings footholds up it, so it
/// is a way up for the patient and a quick drop for the bold, and it ties floors together that were a long walk apart.</item>
/// <item><b>The Grand Hall</b>: one very large, level-floored chamber opened where tunnels meet, with whatever ran above it now
/// opening into its roof: room to fight a horde, ledges to take the high ground.</item>
/// <item><b>The Causeway</b>: a long, straight, level road cut across the cave at the height where most of it lies: a fast way
/// across that joins tunnels which never met, so the cave has loops and a spine.</item>
/// </list>
/// Which a level gets follows from its shape (<see cref="CaveShapes"/>) in the wandering-tunnel and chambered biomes, and from the
/// seed in the built ones (ruins, crypts, mines, the works), where the walls are square and so are these.
/// </summary>
public static partial class CaveGenerator
{
    internal static void CarveLandmarks(CaveData cave)
    {
        if (!CaveShapes.On || B == null || B.Shop || B.Style is GenStyle.Arena or GenStyle.Corridor or GenStyle.Lake or GenStyle.River) return;
        var rng = new Random(unchecked(cave.Seed * 31 + 977));
        bool built = B.Style is GenStyle.Ruins or GenStyle.Crypt or GenStyle.Mine or GenStyle.Works;
        var want = new List<string>();
        if (built)
        {
            switch (rng.Next(5))
            {
                case 1: want.Add("shaft"); break;
                case 2: want.Add("hall"); break;
                case 3: want.Add("causeway"); break;
                case 4: want.Add("shaft"); want.Add("causeway"); break;
            }
        }
        else
        {
            switch (B.ShapeName)
            {
                case "The Deeps": want.Add("shaft"); break;
                case "The Long Galleries": want.Add("causeway"); break;
                case "The Great Halls": want.Add("hall"); break;
                case "The Honeycomb": want.Add("shaft"); break;
                case "The Dry Hollows": want.Add("causeway"); want.Add("shaft"); break;
            }
        }
        if (want.Count == 0) return;

        int stride = W + 1;
        float waterRow = cave.Liquid == Liquid.None ? H + 100 : cave.WaterY / CaveData.Cell;
        int maxRow = (int)Math.Min(H - 5, waterRow - 4);
        // what must stay as it was built: the start, the guardian's and the lairs' and treasure rooms, the high caverns
        var keep = new List<(float x, float y, float r)>();
        keep.Add((cave.StartPos.X / CaveData.Cell, cave.StartPos.Y / CaveData.Cell, 12f));
        foreach (var r in cave.Rooms) keep.Add((r.Center.X / CaveData.Cell, r.Center.Y / CaveData.Cell, Math.Max(r.RxPx, r.RyPx) / CaveData.Cell + 7f));
        if (cave.Boss != null) keep.Add((cave.Boss.Center.X / CaveData.Cell, cave.Boss.Center.Y / CaveData.Cell, Math.Max(cave.Boss.RxPx, cave.Boss.RyPx) / CaveData.Cell + 9f));
        foreach (var hc in cave.HighCaverns) keep.Add(((hc.X + hc.Z) * 0.5f, (hc.Y + hc.W) * 0.5f, Math.Max(hc.Z - hc.X, hc.W - hc.Y) * 0.5f + 6f));
        bool Kept(int i, int j) { foreach (var (x, y, r) in keep) if ((i - x) * (i - x) + (j - y) * (j - y) < r * r) return true; return false; }
        bool IsOpen(int i, int j) => i > 0 && j > 0 && i < W && j < H && cave.Open[j * stride + i] > 0.5f;
        bool Cut(int i, int j)
        {
            if (i < 5 || j < 5 || i > W - 5 || j > maxRow || Kept(i, j)) return false;
            cave.Open[j * stride + i] = 1f;
            return true;
        }
        var made = new List<string>();
        foreach (string what in want)
        {
            CheckCancel();
            if (what == "shaft")
            {
                // the column, away from the map's ends, whose open cells span the most height
                int best = -1, bestTop = 0, bestBot = 0;
                for (int tries = 0; tries < 60; tries++)
                {
                    int x = rng.Next((int)(W * 0.2f), (int)(W * 0.8f));
                    int top = -1, bot = -1;
                    for (int j = 6; j <= maxRow; j++) if (IsOpen(x, j)) { if (top < 0) top = j; bot = j; }
                    if (top < 0 || bot - top < 26) continue;
                    if (best < 0 || bot - top > bestBot - bestTop) { best = x; bestTop = top; bestBot = bot; }
                }
                if (best < 0) continue;
                int hw = built ? 4 : 5;
                bestBot = Math.Min(bestBot, bestTop + 72);
                int cut = 0;
                for (int j = bestTop; j <= bestBot; j++)
                {
                    // (a cave's shaft wanders a little; a built one is plumb)
                    float wob = built ? 0f : 1.6f * MathF.Sin(j * 0.21f + best) + 0.8f * MathF.Sin(j * 0.53f);
                    for (int i = (int)MathF.Round(best - hw + wob); i <= (int)MathF.Round(best + hw + wob); i++) if (Cut(i, j)) cut++;
                }
                if (cut > 120) made.Add("the Great Shaft");
            }
            else if (what == "hall")
            {
                for (int tries = 0; tries < 80; tries++)
                {
                    int x = rng.Next((int)(W * 0.22f), (int)(W * 0.78f)), y = rng.Next(12, Math.Max(13, maxRow - 4));
                    if (!IsOpen(x, y) || Kept(x, y)) continue;
                    // its floor: the ground under that spot
                    int floor = y;
                    while (floor < maxRow && IsOpen(x, floor + 1)) floor++;
                    if (floor >= maxRow) continue;
                    int hw = built ? rng.Next(20, 28) : rng.Next(18, 25), hh = built ? rng.Next(12, 16) : rng.Next(12, 16);
                    if (floor - hh < 7) continue;
                    int cut = 0;
                    for (int j = floor - hh; j <= floor; j++)
                    {
                        // (a cave's hall is domed; a built one is square)
                        float t = (floor - j) / (float)hh;
                        float half = built ? hw : hw * MathF.Sqrt(Math.Max(0.02f, 1f - t * t));
                        for (int i = (int)(x - half); i <= (int)(x + half); i++) if (Cut(i, j)) cut++;
                    }
                    if (cut > 300) { made.Add("the Grand Hall"); break; }
                }
            }
            else if (what == "causeway")
            {
                // the row where the cave lies widest
                int bestRow = -1, bestSpan = 0, b0 = 0, b1 = 0;
                for (int j = 10; j <= maxRow - 2; j += 2)
                {
                    int x0 = -1, x1 = -1, n = 0;
                    for (int i = 6; i < W - 6; i++) if (IsOpen(i, j)) { if (x0 < 0) x0 = i; x1 = i; n++; }
                    // (a row that is already one long tunnel gains nothing)
                    if (x0 < 0 || n > (x1 - x0) * 0.7f) continue;
                    int span = x1 - x0 + rng.Next(0, 12);
                    if (span > bestSpan) { bestSpan = span; bestRow = j; b0 = x0; b1 = x1; }
                }
                if (bestRow < 0 || b1 - b0 < W * 0.35f) continue;
                int cut = 0;
                for (int i = b0; i <= b1; i++)
                    for (int j = bestRow - 5; j <= bestRow; j++) if (Cut(i, j)) cut++;
                if (cut > 200) made.Add("the Causeway");
            }
        }
        cave.Landmark = string.Join(" and ", made);
    }
}
