using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    // ================================================================== the underground river

    /// <summary>
    /// The Underground River: one long, high canyon with a river running along its floor from the guardian's end back toward where you
    /// arrive (see <see cref="CaveData.FlowAt"/>), too fast for an ordinary swimmer to make way against. The way on is dry: stepping
    /// stones, tall pillars and broad banks standing out of the water, each a jump from the last, and a fall between them only carries you
    /// back down the river. Treasure is sunk in pits in the river bed. The guardian waits on a high plateau at the far end, and the river
    /// comes from under it: a tunnel runs in beneath the plateau's rock to a hidden chamber with a silver chest, to be reached only
    /// by swimming faster than the water runs. Bats roost in the roof; nothing else lives here.
    /// </summary>
    private static CaveData GenerateRiver(int seed)
    {
        var rng = new Random(seed);
        float Rf(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var f = new Field(seed, 0.5f);
        int waterRow = (int)(H * 0.42f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = waterRow * CaveData.Cell, Liquid = Liquid.Water, Biome = B };

        const int marginL = 12;
        int marginR = 58;
        float shelfRow = waterRow - 3;
        int tunnelLen = Tune.River.TunnelCells;
        // the plateau (the guardian's ground) and the river's mouth beneath it
        int xb = W - marginR - 74;
        float p1 = Rf(0, 6.28f), p2 = Rf(0, 6.28f), p3 = Rf(0, 6.28f), p4 = Rf(0, 6.28f), p5 = Rf(0, 6.28f);
        float Wave(float x) => 0.5f + 0.5f * (0.5f * MathF.Sin(x * 0.05f + p1) + 0.3f * MathF.Sin(x * 0.13f + p2) + 0.2f * MathF.Sin(x * 0.29f + p3));

        // ---- the river bed (rows) and the roof
        var bot = new float[W + 1];
        var top = new float[W + 1];
        float Slope(float d) => d <= 0 ? 0 : d < 32 ? d * 0.42f : 13.4f + (d - 32) * 2.2f;
        for (int i = 0; i <= W; i++)
        {
            float bed = waterRow + 15f + 4f * Wave(i * 1.3f);
            float land = shelfRow + Slope(i - 46);
            bot[i] = Math.Min(bed, land);
            float roof = waterRow - 25f - 5f * (0.5f + 0.5f * MathF.Sin(i * 0.06f + p4) * 0.6f + 0.4f * MathF.Sin(i * 0.17f + p5));
            // (a taller hall over the guardian)
            if (i > xb - 6) roof -= 5f;
            top[i] = roof;
        }
        // ---- the plateau: solid from its top down over the tunnel, its face a cliff over the river
        for (int i = xb; i <= W; i++) bot[i] = shelfRow;

        // ---- the stepping stones: a chain of islands standing out of the river, each a jump from the last
        var stones = new List<(float x0, float x1, float h)>();
        float x = 52f, h = 1.5f;
        int banks = 0;
        while (x < xb - 12)
        {
            bool bank = x > 110 && banks < 2 && rng.NextDouble() < 0.1 && (stones.Count == 0 || stones[^1].x1 - stones[^1].x0 < 12);
            float w = bank ? Rf(15, 22) : Rf(3.8f, 7f);
            float nh = Math.Clamp(h + Rf(-3f, 1.8f), 2f, 9f);
            float rise = nh - h;
            // (a climb must be a short jump, a drop may be a longer one: the slowest jumper's reach, body width and all)
            float gap = (rise > 1.2f ? Rf(1.8f, 2.6f) : rise > 0.2f ? Rf(2.2f, 3.2f) : rise > -1.5f ? Rf(2.8f, 3.8f) : Rf(3.2f, 4.8f)) * Tune.River.GapScale;
            if (stones.Count == 0) gap = Rf(2.2f, 3f);
            float x0 = x + gap;
            if (x0 + w > xb - 8) break;
            stones.Add((x0, x0 + w, nh));
            if (bank) banks++;
            h = nh; x = x0 + w;
        }
        // (the last stone leads onto the plateau, a short jump from its edge)
        if (stones.Count > 0)
        {
            var last = stones[^1];
            float need = xb - 3.2f;
            stones[^1] = (last.x0, Math.Max(last.x1, need - 1f), Math.Clamp(last.h, 2f, 4f));
        }
        // ---- the bed smoothed
        for (int pass = 0; pass < 2; pass++)
        {
            var next = (float[])bot.Clone();
            for (int i = 1; i < W; i++)
                if (bot[i] > waterRow + 12 && bot[i] < waterRow + 20) next[i] = bot[i] * 0.5f + (bot[i - 1] + bot[i + 1]) * 0.25f;
            bot = next;
        }

        // ---- the tunnel under the plateau, and the hidden chamber at its end
        float tc = waterRow + 7f, tf = waterRow + 16f;
        float tx0 = xb - 4f, tx1 = xb + tunnelLen;
        float chamberX = xb + tunnelLen + 7f, chamberY = waterRow + 11.5f, chamberRx = 7.5f, chamberRy = 5.2f;
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
            {
                float sd = Math.Min(j - top[i], bot[i] - j);
                float hx = Math.Min(i - marginL, W - marginR - i);
                sd = Math.Min(sd, hx);
                // (the tunnel joins the river at its mouth and runs in under the rock)
                if (i >= tx0 && i <= tx1)
                {
                    float st = Math.Min(Math.Min(j - tc, tf - j), Math.Min(i - tx0, tx1 - i + 2f));
                    sd = Math.Max(sd, st);
                }
                int k = j * f.Stride + i;
                f.Open[k] = Math.Clamp(0.5f + sd * 0.5f + f.Rough[k] * 0.3f, 0f, 1f);
            }
        f.Dome(chamberX, chamberY + chamberRy, chamberRx, chamberRy, false);
        // ---- the stones themselves: boulders that hang in the river, flat on top and rounded beneath, their undersides just below the water
        // (so the current runs on under them, and a swimmer can pass beneath)
        foreach (var s in stones)
        {
            float topRow = waterRow - s.h;
            float cx = (s.x0 + s.x1) * 0.5f, half = (s.x1 - s.x0) * 0.5f;
            float thick = s.h + Tune.River.StoneDraft;
            for (int j = (int)topRow - 2; j <= (int)(topRow + thick) + 2; j++)
                for (int i = (int)(s.x0 - 3); i <= (int)(s.x1 + 3); i++)
                {
                    if (i < marginL + 2 || i >= xb - 1 || j < 0 || j > H) continue;
                    float u = Math.Clamp((i - cx) / (half + 1.5f), -1f, 1f);
                    float under = topRow + thick * (1f - u * u * u * u);
                    float inside = Math.Min(Math.Min(j - topRow, under - j), Math.Min(i - (s.x0 - 1.5f), (s.x1 + 1.5f) - i));
                    int k = j * f.Stride + i;
                    f.Open[k] = Math.Min(f.Open[k], Math.Clamp(0.5f - inside * 0.5f + f.Rough[k] * 0.25f, 0f, 1f));
                }
        }

        // ---- where you arrive, and the guardian's plateau
        float startX = 30;
        cave.Rooms.Add(MakeRoom(RoomKind.Start, startX, shelfRow - 4, shelfRow, 12, 8));
        float bossCx = xb + 34;
        var bossRoom = MakeRoom(RoomKind.Boss, bossCx, shelfRow - 7, shelfRow, 15, 10);
        cave.Boss = bossRoom;
        cave.Rooms.Add(bossRoom);
        cave.StartPos = new Vector2(startX, shelfRow - 1.2f) * CaveData.Cell;
        // the hidden chamber
        var secret = new Room
        {
            Kind = RoomKind.Secret, Underwater = true,
            Center = new Vector2(chamberX, chamberY) * CaveData.Cell, Floor = new Vector2(chamberX, chamberY + chamberRy - 0.5f) * CaveData.Cell,
            RxPx = chamberRx * CaveData.Cell, RyPx = chamberRy * CaveData.Cell,
        };
        cave.Rooms.Add(secret);
        cave.Sheltered.Add(secret);
        // the river's mouth is marked for the ones who look: bubbles escaping along the cliff
        cave.Hints.Add((new Vector2(xb - 1.5f, waterRow + 11f) * CaveData.Cell, 0));

        // ---- the current: from the guardian's end to the beach, and it dies out in the eddy there
        int dir = Math.Sign(bossRoom.Center.X - cave.StartPos.X);
        cave.Flow = -dir * Tune.River.Flow;
        cave.EddyX0 = 50f * CaveData.Cell;
        cave.EddyX1 = (Tune.River.EddyCells + 50f) * CaveData.Cell;
        cave.Tunnel = new Rect2(tx0 * CaveData.Cell, tc * CaveData.Cell, (tx1 - tx0) * CaveData.Cell, (tf - tc) * CaveData.Cell);

        // ---- treasure pits sunk in the river bed (the bottom of the river is where the upgrades are), out of the current
        int pits = (int)(W / 32f);
        for (int k = 0, made = 0; k < pits * 8 && made < pits; k++)
        {
            float px = Rf(60, xb - 6);
            float rx = Rf(4.5f, 6.5f), ry = Rf(3.6f, 4.8f);
            int xi = (int)px;
            float cy = bot[xi] + ry * Rf(0.1f, 0.5f);
            if (bot[xi] < waterRow + 11 || cy + ry > H - 8) continue;
            bool near = false;
            foreach (var r in cave.Rooms) if (r.Kind != RoomKind.Start && Math.Abs(r.Center.X / CaveData.Cell - px) < 22) near = true;
            if (near) continue;
            f.Dome(px, cy + ry, rx, ry, false);
            var pit = new Room
            {
                Kind = RoomKind.Treasure, Underwater = true,
                Center = new Vector2(px, cy) * CaveData.Cell, Floor = new Vector2(px, cy + ry - 0.5f) * CaveData.Cell,
                RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
            };
            cave.Rooms.Add(pit);
            cave.Sheltered.Add(pit);
            made++;
        }
        // ---- lairs for the elite bats: the broad banks
        int lairs = 0;
        foreach (var s in stones.Where(s => s.x1 - s.x0 > 12).OrderBy(_ => rng.Next()))
        {
            if (lairs >= Math.Max(1, B.MiniBossesMin)) break;
            float cx = (s.x0 + s.x1) * 0.5f;
            cave.Rooms.Add(new Room
            {
                Kind = RoomKind.MiniBoss, Center = new Vector2(cx, waterRow - s.h - 5f) * CaveData.Cell, Floor = new Vector2(cx, waterRow - s.h) * CaveData.Cell,
                RxPx = (s.x1 - s.x0) * 0.5f * CaveData.Cell, RyPx = 5f * CaveData.Cell,
            });
            lairs++;
        }

        // ---- creature spawns: bats, roosting along the roof (nothing else lives here)
        var stamps = new List<Stamp>();
        for (int i = 24; i < W - marginR; i += 11) stamps.Add(new Stamp { X = i, Y = top[i] + 4f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        var done = Finish(cave, f, rng, new Vector2I((int)startX, (int)shelfRow - 2), stamps, 1, null, platforms: false);
        // the dry way across: stone to stone, with no swimming at all
        var dry = new FineReach(done, dry: true);
        done.DryOk = dry.Run(done.StartPos, done.Boss.Floor);
        return done;
    }
}
