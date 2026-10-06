using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    // ================================================================== the sunken sea

    /// <summary>
    /// The secret depth: one vast, deep, open underground lake. A shingle beach on the left where you arrive, a shelf on the
    /// right with the way on, and between them black water over a floor of trenches and shoals: rock islands, pillars rising
    /// from the deep, and treasure pits sunk into the lake bed. No guardian (the way on is always open).
    /// </summary>
    private static CaveData GenerateLake(int seed)
    {
        var rng = new Random(seed);
        float Rf(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var f = new Field(seed, 0.6f);
        int waterRow = (int)(H * 0.2f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = waterRow * CaveData.Cell, Liquid = Liquid.Water, Biome = B };

        const int marginL = 12;
        int marginR = 34; // (rock behind the exit shelf: the treasury is cut into it)
        float p1 = Rf(0, 6.28f), p2 = Rf(0, 6.28f), p3 = Rf(0, 6.28f), p4 = Rf(0, 6.28f), p5 = Rf(0, 6.28f);
        float Wave(float x) => 0.5f + 0.5f * (0.5f * MathF.Sin(x * 0.045f + p1) + 0.3f * MathF.Sin(x * 0.11f + p2) + 0.2f * MathF.Sin(x * 0.23f + p3));

        // ---- the lake bed (rows) and the roof
        var bot = new float[W + 1];
        var top = new float[W + 1];
        float shelfRow = waterRow - 3;
        for (int i = 0; i <= W; i++)
        {
            float t = Math.Clamp((i - marginL) / (float)(W - marginL - marginR), 0f, 1f);
            float bowl = Math.Clamp(Math.Min(t, 1 - t) / 0.2f, 0f, 1f);
            bowl = bowl * bowl * (3 - 2 * bowl);
            float deep = H - 9 - 24f * Wave(i);
            float b = Mathf.Lerp(waterRow + 9, deep, bowl);
            // the beach, rising gently to the dry shelf you start on, and the shelf at the far end
            float Slope(float d) => d <= 0 ? 0 : d < 32 ? d * 0.42f : 13.4f + (d - 32) * 2.2f;
            float land = shelfRow + Slope(i - 46);
            float exitLand = shelfRow + Slope((W - marginR - 46) - i);
            b = Math.Min(b, Math.Min(land, exitLand));
            bot[i] = b;
            top[i] = 6f + 6f * (0.5f + 0.5f * MathF.Sin(i * 0.07f + p4) * 0.6f + 0.4f * MathF.Sin(i * 0.19f + p5));
        }
        // ---- islands: a flat top just above the water, a gentle skirt round the waterline, then steep rock down
        int islands = 2;
        var spots = new List<float>();
        for (int k = 0; k < islands * 3 && spots.Count < islands; k++)
        {
            float c = Rf(110, W - marginR - 110);
            bool ok = true;
            foreach (var s in spots) if (Math.Abs(s - c) < 130) ok = false;
            if (!ok) continue;
            spots.Add(c);
            float flat = Rf(3, 6), skirt = Rf(6, 9), topRow = waterRow - Rf(3, 6);
            for (int i = (int)(c - flat - skirt - 40); i <= c + flat + skirt + 40; i++)
            {
                if (i < marginL + 2 || i > W - marginR - 2) continue;
                float d = Math.Abs(i - c);
                float h = d < flat ? topRow : d < flat + skirt ? topRow + (d - flat) * 0.45f : topRow + skirt * 0.45f + (d - flat - skirt) * 9f;
                bot[i] = Math.Min(bot[i], h);
            }
        }
        // ---- smooth the deep bed a little so no cliff of a single column is left (before the pillars, which stay crisp)
        for (int pass = 0; pass < 3; pass++)
        {
            var next = (float[])bot.Clone();
            for (int i = 1; i < W; i++)
                if (bot[i] > waterRow + 6) next[i] = bot[i] * 0.5f + (bot[i - 1] + bot[i + 1]) * 0.25f;
            bot = next;
        }
        // ---- pillars of rock standing up from the deep
        int pillars = (int)(W / 22f);
        for (int k = 0; k < pillars; k++)
        {
            int px = (int)Rf(60, W - marginR - 40), pw = (int)Rf(3, 6);
            float topRow = waterRow + Rf(5, 28);
            if (bot[px] < topRow + 6) continue;
            for (int i = px; i <= px + pw; i++) bot[Math.Min(W, i)] = Math.Min(bot[Math.Min(W, i)], topRow + (i == px || i == px + pw ? 1.5f : 0f));
        }
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
            {
                float sd = Math.Min(j - top[i], bot[i] - j);
                float hx = Math.Min(i - marginL, W - marginR - i);
                sd = Math.Min(sd, hx);
                int k = j * f.Stride + i;
                f.Open[k] = Math.Clamp(0.5f + sd * 0.5f + f.Rough[k] * 0.3f, 0f, 1f);
            }

        // ---- where you arrive, and the way on
        float startX = 30;
        cave.Rooms.Add(MakeRoom(RoomKind.Start, startX, shelfRow - 4, shelfRow, 12, 8));
        float exitCx = W - marginR - 22;
        var exit = MakeRoom(RoomKind.Boss, exitCx, shelfRow - 6, shelfRow, 15, 9);
        cave.Boss = exit;
        cave.Rooms.Add(exit);
        cave.StartPos = new Vector2(startX, shelfRow - 1.2f) * CaveData.Cell;

        // ---- treasure pits: round chambers sunk into the lake bed (and its flanks), open to the water above
        int pits = (int)(W / 30f);
        for (int k = 0, made = 0; k < pits * 6 && made < pits; k++)
        {
            float x = Rf(50, W - marginR - 30);
            float rx = Rf(5, 7), ry = Rf(3.8f, 5f);
            int xi = (int)x;
            float cy = bot[xi] + ry * Rf(0.1f, 0.6f);
            if (bot[xi] < waterRow + 12 || cy + ry > H - 5) continue;
            bool near = false;
            foreach (var r in cave.Rooms) if (Math.Abs(r.Center.X / CaveData.Cell - x) < 26 && r.Kind != RoomKind.Boss) near = true;
            if (near) continue;
            f.Dome(x, cy + ry, rx, ry, false);
            cave.Rooms.Add(new Room
            {
                Kind = RoomKind.Treasure, Underwater = true,
                Center = new Vector2(x, cy) * CaveData.Cell, Floor = new Vector2(x, cy + ry - 0.5f) * CaveData.Cell,
                RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
            });
            made++;
        }
        AssignMiniBosses(cave, rng, 60);

        // ---- creature spawns: all through the water, and on the beaches and islands, and up on the roof
        var stamps = new List<Stamp>();
        for (int j = waterRow + 5; j < H - 6; j += 6)
            for (int i = 18; i < W - marginR; i += 6)
            {
                if (bot[i] - j < 4 || j - top[i] < 4) continue;
                stamps.Add(new Stamp { X = i + (float)rng.NextDouble() * 3f, Y = j + (float)rng.NextDouble() * 3f, R = 3, Main = true, Mode = ModeWater, Kind = ModeWater });
            }
        for (int i = 20; i < W - marginR; i += 5)
        {
            if (bot[i] < waterRow - 1) stamps.Add(new Stamp { X = i, Y = bot[i] - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
            if (bot[i] > waterRow - 4 && bot[i] < waterRow + 8) stamps.Add(new Stamp { X = i, Y = waterRow - 2f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        }
        for (int i = 24; i < W - marginR; i += 16) stamps.Add(new Stamp { X = i, Y = top[i] + 4f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        // (Finish reads the field's own stamps for debug only: the spawn stamps go in separately)
        return Finish(cave, f, rng, new Vector2I((int)startX, (int)shelfRow - 2), stamps, 1, null, platforms: false);
    }
}
