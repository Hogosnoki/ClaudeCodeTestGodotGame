using System;
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Noise;

namespace DaggerCave;

/// <summary>
/// Builds a <see cref="CaveData"/> from a seed.
///
/// Tunnels are carved by "walkers" that wander and branch in every direction. Three walker modes
/// keep the cave traversable without abilities:
///  * Air walkers never exceed ~35 degrees of slope, so every above-water tunnel is walkable
///    both ways.
///  * Shaft walkers drop steeply -- the "fall down a hole" branches -- but only ever end in the
///    flooded bottom half of the map, so a fall always lands in water.
///  * Water walkers wander freely underwater (swimming goes anywhere). A walker that came down a
///    shaft must eventually surface through a gentle beach, and each such beach gets a gentle
///    connector tunnel back to the main network, so nothing you can fall into is a dead trap.
/// After carving, a coarse movement-aware reachability check (walk/jump/fall/swim over cells)
/// counts "trap" cells -- reachable from the start but with no way back -- and generation retries
/// with a new seed if any meaningful trap exists.
/// </summary>
public static class CaveGenerator
{
    public static int W => Tune.Cave.Width;
    public static int H => Tune.Cave.Height;
    private const float MaxPitch = 0.60f; // ~34 degrees
    private const int ModeAir = 0, ModeShaft = 1, ModeWater = 2;

    private sealed class Walker
    {
        public float X, Y, A, AV, R, TR, Len;
        public int Mode, Gen, WaterSteps, Extra;
        public bool Main, MustExit, Exited, Descender;
    }

    private struct Stamp { public float X, Y, R; public bool Main; public int Mode; public int Kind; }
    private struct EndInfo { public float X, Y, R, A; public int Mode; }

    public static CaveData Generate(int seed)
    {
        CaveData best = null;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var c = GenerateOnce(seed + attempt * 7919);
            c.Attempts = attempt + 1;
            if (best == null || Score(c) < Score(best)) best = c;
            if (Score(c) <= 6) break;
        }
        return best;
    }

    /// <summary>Lower is better: trapped cells, plus a big penalty for a missing or nearby boss room.</summary>
    private static int Score(CaveData c)
    {
        int score = c.TrapCells;
        if (c.Boss == null) score += 100000;
        else
        {
            int bi = (int)(c.Boss.Center.X / CaveData.Cell), bj = (int)(c.Boss.Center.Y / CaveData.Cell);
            bool reachable = false;
            for (int dj = -3; dj <= 3 && !reachable; dj++)
                for (int di = -3; di <= 3; di++)
                {
                    int k = (bj + dj) * W + bi + di;
                    if (k >= 0 && k < W * H && c.ReachMask[k]) { reachable = true; break; }
                }
            if (!reachable) score += 50000;
            if (c.Boss.Center.DistanceTo(c.StartPos) < Tune.Cave.BossMinDistanceCells * CaveData.Cell) score += 5000;
            int minis = 0;
            foreach (var r in c.Rooms) if (r.Kind == RoomKind.MiniBoss) minis++;
            if (minis < Tune.Cave.MiniBossesMin) score += 2000;
        }
        return score;
    }

    public static CaveData GenerateOnce(int seed)
    {
        var rng = new Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        float waterRow = H * 0.5f;

        var stamps = new List<Stamp>(4096);
        var ends = new List<EndInfo>();
        var beaches = new List<Vector2>();
        var queue = new Queue<Walker>();
        int total = 0;
        int budget = Tune.Cave.TunnelBudget;

        float sx = W * 0.5f + Rnd(-40, 40);
        float sy = H * 0.15f + Rnd(-2, 3);

        // Initial walkers leave from either side of the start room: one level, one descending gently
        // all the way to the water so the flooded half is always reachable (and escapable) on foot.
        {
            float hs = rng.Next(2) == 0 ? 1 : -1;
            queue.Enqueue(new Walker { X = sx - hs * 7, Y = sy + 0.5f, A = hs > 0 ? Mathf.Pi + Rnd(-0.15f, 0.15f) : Rnd(-0.15f, 0.15f), R = 3.4f, TR = 3.6f, Len = Rnd(320, 440), Main = true });
            queue.Enqueue(new Walker { X = sx + hs * 7, Y = sy + 0.5f, A = hs > 0 ? MaxPitch * 0.6f : Mathf.Pi - MaxPitch * 0.6f, R = 3.3f, TR = 3.5f, Len = Rnd(260, 360), Main = true, Descender = true });
        }

        void ClampPitch(Walker w)
        {
            float hs = Mathf.Cos(w.A) >= 0 ? 1 : -1;
            float p = Mathf.Asin(Mathf.Clamp(Mathf.Sin(w.A), -1, 1));
            float pc = Mathf.Clamp(p, -MaxPitch, MaxPitch);
            if (pc != p) w.AV = 0;
            w.A = hs > 0 ? pc : Mathf.Pi - pc;
        }

        while (queue.Count > 0)
        {
            var w = queue.Dequeue();
            while (true)
            {
                bool mustContinue = w.MustExit && !w.Exited;
                if (w.Len <= 0 && !mustContinue) break;
                if (w.Len <= 0 && mustContinue && ++w.Extra > 260) break;

                switch (w.Mode)
                {
                    case ModeAir:
                        w.AV += Rnd(-0.05f, 0.05f); w.AV *= 0.92f; w.A += w.AV;
                        // a gentle preference for long horizontal sweeps (more floor to walk on)
                        if (!w.Descender) w.A = Mathf.LerpAngle(w.A, Mathf.Cos(w.A) >= 0 ? 0 : Mathf.Pi, Tune.Cave.HorizontalBias);
                        if (w.Descender && w.Y < waterRow + 2)
                        {
                            // keep heading downhill until it reaches the water
                            float hs0 = Mathf.Cos(w.A) >= 0 ? 1 : -1;
                            w.A = Mathf.LerpAngle(w.A, hs0 > 0 ? MaxPitch * 0.8f : Mathf.Pi - MaxPitch * 0.8f, 0.08f);
                            if (w.Len < 20) w.Len = 20;
                        }
                        ClampPitch(w);
                        if (w.Y > waterRow + 0.5f) { w.Mode = ModeWater; w.WaterSteps = 0; w.Descender = false; }
                        break;
                    case ModeShaft:
                        w.AV += Rnd(-0.08f, 0.08f); w.AV *= 0.85f; w.A += w.AV;
                        w.A = Mathf.Clamp(w.A, Mathf.Pi / 2 - 0.45f, Mathf.Pi / 2 + 0.45f);
                        if (w.Y > waterRow + 8)
                        {
                            w.Mode = ModeWater; w.WaterSteps = 0;
                            w.A = rng.Next(2) == 0 ? 0.35f : Mathf.Pi - 0.35f;
                        }
                        break;
                    default:
                        w.WaterSteps++;
                        w.AV += Rnd(-0.09f, 0.09f); w.AV *= 0.9f; w.A += w.AV;
                        // drift deeper so the flooded half gets used
                        if (!w.MustExit && w.Y < waterRow + 40) w.A = Mathf.LerpAngle(w.A, Mathf.Pi / 2, 0.012f);
                        if (w.MustExit && !w.Exited && (w.WaterSteps > 45 || w.Len <= 0))
                        {
                            float hs = Mathf.Cos(w.A) >= 0 ? 1 : -1;
                            float target = hs > 0 ? -MaxPitch * 0.8f : Mathf.Pi + MaxPitch * 0.8f;
                            w.A = Mathf.LerpAngle(w.A, target, 0.1f);
                        }
                        if (w.Y < waterRow + 1.5f && Mathf.Sin(w.A) < 0)
                        {
                            // Surfacing: only ever through a gentle slope.
                            w.Mode = ModeAir;
                            ClampPitch(w);
                            if (w.MustExit && !w.Exited && w.WaterSteps > 15)
                            {
                                w.Exited = true;
                                beaches.Add(new Vector2(w.X, w.Y));
                            }
                        }
                        if (w.Y > H - 10 && Mathf.Sin(w.A) > 0) { w.A = -w.A; w.AV = 0; }
                        break;
                }

                if ((w.X < 10 && Mathf.Cos(w.A) < 0) || (w.X > W - 10 && Mathf.Cos(w.A) > 0)) { w.A = Mathf.Pi - w.A; w.AV = 0; }
                if (w.Y < 9 && Mathf.Sin(w.A) < 0) { w.A = -w.A; w.AV = 0; }
                if (w.Y > H - 8 && Mathf.Sin(w.A) > 0) { w.A = -w.A; w.AV = 0; }

                if (rng.NextDouble() < 0.02) w.TR = w.Mode == ModeWater ? Rnd(3.0f, 4.7f) : Rnd(Tune.Cave.AirRadiusMin, Tune.Cave.AirRadiusMax);
                w.R += (w.TR - w.R) * 0.04f;
                w.X += Mathf.Cos(w.A) * 0.9f;
                w.Y += Mathf.Sin(w.A) * 0.9f;
                w.X = Mathf.Clamp(w.X, 7, W - 7);
                w.Y = Mathf.Clamp(w.Y, 7, H - 7);
                stamps.Add(new Stamp { X = w.X, Y = w.Y, R = w.R, Main = w.Main && w.Mode != ModeShaft, Mode = w.Mode, Kind = w.Descender ? 6 : w.Mode });
                w.Len -= 1; total++;

                if (w.Mode != ModeShaft && total < budget && w.Gen < 6 && rng.NextDouble() < 0.017)
                {
                    var c = new Walker
                    {
                        X = w.X, Y = w.Y, Gen = w.Gen + 1,
                        R = Math.Min(w.R, 3.4f), TR = w.Mode == ModeAir ? Rnd(Tune.Cave.AirRadiusMin, Tune.Cave.AirRadiusMax) : Rnd(3.0f, 4.2f),
                        Len = Rnd(70, 200) * (1f - w.Gen * 0.11f),
                    };
                    if (w.Mode == ModeAir && w.Y < waterRow - 12 && rng.NextDouble() < 0.2)
                    {
                        c.Mode = ModeShaft; c.A = Mathf.Pi / 2 + Rnd(-0.3f, 0.3f); c.MustExit = true; c.Len = Math.Max(c.Len, 70);
                    }
                    else if (w.Mode == ModeAir)
                    {
                        c.Mode = ModeAir; c.Main = w.Main;
                        float hs = rng.NextDouble() < 0.6 ? -Mathf.Sign(Mathf.Cos(w.A)) : Mathf.Sign(Mathf.Cos(w.A));
                        float p = Rnd(-MaxPitch, MaxPitch) * Tune.Cave.BranchPitchMult;
                        if (w.Main && w.Y < waterRow - 10 && rng.NextDouble() < 0.12) { p = MaxPitch * 0.85f; c.Descender = true; c.Len = Math.Max(c.Len, 60); }
                        c.A = hs > 0 ? p : Mathf.Pi - p;
                    }
                    else
                    {
                        c.Mode = ModeWater; c.Main = w.Main; c.A = Rnd(0, Mathf.Tau);
                    }
                    queue.Enqueue(c);
                }
            }
            ends.Add(new EndInfo { X = w.X, Y = w.Y, R = w.R, A = w.A, Mode = w.Mode });
        }

        // Connectors: every beach of a shaft-fed water system gets a gentle tunnel to the main network.
        foreach (var b in beaches)
        {
            float bestD = float.MaxValue; Stamp bestS = default; bool found = false;
            foreach (var s in stamps)
            {
                if (!s.Main || s.Mode != ModeAir) continue;
                float dy = b.Y - s.Y, dx = Math.Abs(s.X - b.X);
                if (dy < 2 || dy > 0.6f * dx) continue;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; bestS = s; found = true; }
            }
            if (found)
            {
                var from = new Vector2(b.X, b.Y); var to = new Vector2(bestS.X, bestS.Y);
                float len = from.DistanceTo(to);
                for (float t = 0; t <= len; t += 0.9f)
                {
                    var p = from.Lerp(to, t / len);
                    stamps.Add(new Stamp { X = p.X, Y = p.Y, R = 2.8f, Main = true, Mode = ModeAir, Kind = 4 });
                }
                continue;
            }
            // No straight gentle route: climb to the nearest main tunnel with switchbacks.
            float bd2 = float.MaxValue;
            foreach (var st in stamps)
            {
                if (!st.Main || st.Mode != ModeAir || st.Y > b.Y - 2) continue;
                float d = (st.X - b.X) * (st.X - b.X) + (st.Y - b.Y) * (st.Y - b.Y);
                if (d < bd2) { bd2 = d; bestS = st; found = true; }
            }
            if (found) Switchback(new Vector2(b.X, b.Y), new Vector2(bestS.X, bestS.Y));
        }

        void Switchback(Vector2 from, Vector2 to)
        {
            var pos = from;
            float hs = to.X >= from.X ? 1 : -1, leg = 0;
            for (int k = 0; k < 900 && pos.DistanceTo(to) > 2.5f; k++)
            {
                float dx = to.X - pos.X, dy = to.Y - pos.Y;
                if (leg > 24 && Math.Sign(dx) != hs && Math.Abs(dx) > 1) { hs = -hs; leg = 0; }
                if ((pos.X < 10 && hs < 0) || (pos.X > W - 10 && hs > 0)) { hs = -hs; leg = 0; }
                float pitch = Mathf.Clamp(Mathf.Atan2(dy, Math.Max(Math.Abs(dx), 0.001f)), -MaxPitch * 0.9f, MaxPitch * 0.9f);
                var d = new Vector2(hs * Mathf.Cos(pitch), Mathf.Sin(pitch));
                pos += d * 0.9f; leg += 0.9f;
                stamps.Add(new Stamp { X = pos.X, Y = pos.Y, R = 2.7f, Main = true, Mode = ModeAir, Kind = 5 });
            }
        }

        // --- Field ---
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = waterRow * CaveData.Cell };
        int stride = W + 1;
        var open = new float[stride * (H + 1)];
        var rough = new float[stride * (H + 1)];
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
                rough[j * stride + i] = (float)LatticeNoise3D.SampleFbm(i, j, 0.5, 11.3, 7.7, 0, seed, 3, 0.11, 0.55, 2.0) * 1.9f;

        void Carve(float cx, float cy, float r)
        {
            int i0 = Math.Max(0, (int)(cx - r - 3)), i1 = Math.Min(W, (int)(cx + r + 3));
            int j0 = Math.Max(0, (int)(cy - r - 3)), j1 = Math.Min(H, (int)(cy + r + 3));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * stride + i;
                    float d = MathF.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                    float v = Math.Clamp(0.5f + (r + rough[k] - d) * 0.5f, 0f, 1f);
                    if (v > open[k]) open[k] = v;
                }
        }

        void Dome(float cx, float floorY, float rx, float ry, bool flatFloor)
        {
            float cy = flatFloor ? floorY : floorY - ry;
            int i0 = Math.Max(0, (int)(cx - rx - 3)), i1 = Math.Min(W, (int)(cx + rx + 3));
            int j0 = Math.Max(0, (int)(cy - ry - 3)), j1 = Math.Min(H, (int)(cy + ry + 3));
            float rmin = Math.Min(rx, ry);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * stride + i;
                    float ex = (i - cx) / rx, ey = (j - cy) / ry;
                    float e = MathF.Sqrt(ex * ex + ey * ey);
                    float v = 0.5f + ((1 - e) * rmin + rough[k] * 0.45f) * 0.5f;
                    if (flatFloor) v = Math.Min(v, 0.5f + (floorY - j) * 0.5f);
                    v = Math.Clamp(v, 0f, 1f);
                    if (v > open[k]) open[k] = v;
                }
        }

        Dome(sx, sy + 3.2f, 9, 6.5f, true);
        foreach (var s in stamps) Carve(s.X, s.Y, s.R);
        // the start room keeps a solid floor, so no tunnel passing underneath drops you on arrival
        for (int j = (int)(sy + 3.2f); j <= (int)(sy + 3.2f) + 3 && j <= H; j++)
            for (int i = Math.Max(0, (int)(sx - 8)); i <= Math.Min(W, (int)(sx + 8)); i++)
            {
                int k = j * stride + i;
                open[k] = Math.Min(open[k], Math.Clamp(0.5f - (j - (sy + 3.2f)) * 0.5f, 0f, 1f));
            }
        cave.Open = open;

        // --- Dead ends and rooms ---
        var startCell = new Vector2I((int)sx, (int)(sy + 1));
        int[] geo = GeodesicFrom(cave, startCell);

        var deadEnds = new List<EndInfo>();
        foreach (var e in ends)
        {
            if (new Vector2(e.X - sx, e.Y - sy).Length() < 26) continue;
            if (CountOpenRuns(cave, e.X, e.Y, e.R + 4.5f) != 1) continue;
            deadEnds.Add(e);
        }

        // Boss: the reachable above-water dead end farthest (by tunnel distance) from the start.
        EndInfo? bossEnd = null; int bossDist = -1;
        foreach (var e in deadEnds)
        {
            float floorY = e.Y + e.R;
            if (e.Mode == ModeWater || floorY > waterRow - 3 || floorY - 11 < 4) continue;
            int ci = (int)e.X, cj = (int)e.Y;
            if (ci < 0 || cj < 0 || ci >= W || cj >= H) continue;
            int d = geo[cj * W + ci];
            if (d > bossDist) { bossDist = d; bossEnd = e; }
        }
        if (bossEnd == null)
        {
            // Fallback: farthest main air stamp.
            foreach (var s in stamps)
            {
                if (s.Mode != ModeAir || s.Y + s.R > waterRow - 3 || s.Y + s.R - 11 < 4) continue;
                int d = geo[(int)s.Y * W + (int)s.X];
                if (d > bossDist) { bossDist = d; bossEnd = new EndInfo { X = s.X, Y = s.Y, R = s.R, A = 0, Mode = ModeAir }; }
            }
        }

        var roomCenters = new List<Vector2> { new(sx, sy) };
        cave.Rooms.Add(new Room
        {
            Kind = RoomKind.Start,
            Center = new Vector2(sx, sy) * CaveData.Cell,
            Floor = new Vector2(sx, sy + 3.2f) * CaveData.Cell,
            RxPx = 9 * CaveData.Cell, RyPx = 6.5f * CaveData.Cell,
        });

        if (bossEnd is EndInfo be)
        {
            const float rx = 16, ry = 10.5f;
            float hs = Mathf.Cos(be.A) >= 0 ? 1 : -1;
            float cx = Mathf.Clamp(be.X + hs * 9, rx + 3, W - rx - 3);
            float floorY = be.Y + be.R;
            Dome(cx, floorY, rx, ry, true);
            // Solid slab under the arena so no tunnel undercuts the fight.
            for (int j = (int)floorY; j <= (int)floorY + 5 && j <= H; j++)
                for (int i = Math.Max(0, (int)(cx - rx - 2)); i <= Math.Min(W, (int)(cx + rx + 2)); i++)
                {
                    int k = j * stride + i;
                    open[k] = Math.Min(open[k], Math.Clamp(0.5f - (j - floorY) * 0.5f, 0f, 1f));
                }
            // Make sure the dome actually meets the tunnel end.
            for (float t = 0; t <= 1; t += 0.05f) Carve(Mathf.Lerp(be.X, cx, t), floorY - 3.2f, 3.2f);
            var room = new Room
            {
                Kind = RoomKind.Boss,
                Center = new Vector2(cx, floorY - ry * 0.45f) * CaveData.Cell,
                Floor = new Vector2(cx, floorY) * CaveData.Cell,
                RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
            };
            cave.Boss = room;
            cave.Rooms.Add(room);
            roomCenters.Add(new Vector2(cx, floorY - 4));
        }

        foreach (var e in deadEnds)
        {
            if (bossEnd is EndInfo b2 && Math.Abs(b2.X - e.X) < 0.01f && Math.Abs(b2.Y - e.Y) < 0.01f) continue;
            var c = new Vector2(e.X, e.Y);
            bool tooClose = false;
            foreach (var rc in roomCenters) if (rc.DistanceTo(c) < 16) { tooClose = true; break; }
            if (tooClose) continue;
            roomCenters.Add(c);

            bool under = e.Y > waterRow + 2;
            if (under)
            {
                float rx = Rnd(5, 6.5f), ry = Rnd(3.8f, 4.8f);
                float hs = Mathf.Cos(e.A) >= 0 ? 1 : -1;
                float cx = Mathf.Clamp(e.X + hs * 2, rx + 3, W - rx - 3);
                float cy = Mathf.Clamp(e.Y, waterRow + ry + 1, H - ry - 3);
                Dome(cx, cy + ry, rx, ry, false);
                cave.Rooms.Add(new Room
                {
                    Kind = RoomKind.Treasure,
                    Underwater = true,
                    Center = new Vector2(cx, cy) * CaveData.Cell,
                    Floor = new Vector2(cx, cy + ry - 0.5f) * CaveData.Cell,
                    RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
                });
            }
            else
            {
                float rx = Rnd(6, 8.5f), ry = Rnd(4.5f, 6.2f);
                float floorY = e.Y + e.R;
                if (floorY - ry < 4) ry = floorY - 4;
                float hs = Mathf.Cos(e.A) >= 0 ? 1 : -1;
                float cx = Mathf.Clamp(e.X + hs * 3, rx + 3, W - rx - 3);
                Dome(cx, floorY, rx, ry, true);
                double roll = rng.NextDouble();
                cave.Rooms.Add(new Room
                {
                    Kind = roll < 0.6 ? RoomKind.Treasure : RoomKind.Ambush,
                    Center = new Vector2(cx, floorY - ry * 0.5f) * CaveData.Cell,
                    Floor = new Vector2(cx, floorY) * CaveData.Cell,
                    RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
                });
            }
        }

        // Exactly 3-4 mini-boss lairs, spread out and weighted toward the far reaches of the cave.
        {
            var candidates = new List<Room>();
            foreach (var r in cave.Rooms) if (r.Kind is RoomKind.Treasure or RoomKind.Ambush) candidates.Add(r);
            var startPx = new Vector2(sx, sy) * CaveData.Cell;
            candidates.Sort((a, b) => b.Center.DistanceTo(startPx).CompareTo(a.Center.DistanceTo(startPx)));
            int want = Tune.Cave.MiniBossesMin + rng.Next(Tune.Cave.MiniBossesMax - Tune.Cave.MiniBossesMin + 1);
            var chosen = new List<Room>();
            foreach (float spacing in new[] { 70f, 45f, 25f })
                foreach (var r in candidates)
                {
                    if (chosen.Count >= want) break;
                    if (chosen.Contains(r) || r.Center.DistanceTo(startPx) < 40 * CaveData.Cell) continue;
                    bool ok = true;
                    foreach (var c in chosen) if (c.Center.DistanceTo(r.Center) < spacing * CaveData.Cell) { ok = false; break; }
                    if (ok) chosen.Add(r);
                }
            foreach (var r in chosen) r.Kind = RoomKind.MiniBoss;
        }

        // Solid border.
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
                if (i < 3 || j < 3 || i > W - 3 || j > H - 3) open[j * stride + i] = 0;

        cave.StartPos = new Vector2(sx, sy + 3.2f - 1.2f) * CaveData.Cell;
        foreach (var st in stamps) cave.DebugStamps.Add((new Vector2(st.X, st.Y), st.Kind));
        RemoveSpecks(cave);
        AddPlatforms(cave, rng);

        // Reachability validation, with repairs: stepping-stone ledges up out of any pit the
        // movement model says you could fall into but not climb out of.
        ValidateTraversal(cave, startCell);
        var tried = new HashSet<int>();
        for (int rep = 0; rep < 12 && cave.TrapCells > 6; rep++)
        {
            if (!RepairTraps(cave, tried)) break;
            ValidateTraversal(cave, startCell);
        }

        BuildSpawns(cave, stamps, new Vector2(sx, sy), rng);
        cave.RockDepth = ComputeRockDepth(cave);
        return cave;
    }

    /// <summary>
    /// Ledge staircases through tall open spaces, so the high caverns can be climbed back into
    /// rather than only reached by swimming around. Chains start at the water surface and on the
    /// floors of tall spaces and zig-zag upward one jump at a time. Each step continues with a
    /// chance that falls with height (Tune.Cave.PlatformDensityBottom at the water line down to
    /// PlatformDensityTop at the roof), so the low caves are easy to climb around and the heights
    /// take more luck or better movement upgrades. In narrow shafts a ledge becomes a shelf on one
    /// wall that leaves a gap to drop through.
    /// </summary>
    private static void AddPlatforms(CaveData cave, Random rng)
    {
        int waterRow = (int)(cave.WaterY / CaveData.Cell);
        bool Open(int i, int j) => cave.CellOpen(i, j);
        var placed = new List<Vector3>(); // (centre x, standing row, half width)
        var keepOut = new List<Room>();
        foreach (var r in cave.Rooms) if (r.Kind is RoomKind.Boss or RoomKind.Start) keepOut.Add(r);

        float Chance(int standRow)
        {
            float h = Math.Clamp((waterRow - standRow) / (float)Math.Max(1, waterRow - 4), 0f, 1f);
            return Mathf.Lerp(Tune.Cave.PlatformDensityBottom, Tune.Cave.PlatformDensityTop, h);
        }

        // Tries to put a ledge whose top you stand on in cell row s, near column x. Returns the
        // column it was centred on, or null.
        float? TryLedge(float x, int s)
        {
            int ci = (int)x;
            if (s < 6 || s >= waterRow - 1 || ci < 4 || ci >= W - 4) return null;
            // headroom to stand and jump
            for (int j = s - 3; j <= s; j++) if (!Open(ci, j)) return null;
            if (!Open(ci, s + 1)) return null; // there's already ground here
            // keep a clear sideways gap from ledges at a similar height, and never stack one
            // right above another (you'd bump your head jumping up)
            float gap = Tune.Cave.PlatformGapCells;
            foreach (var p in placed)
                if (Math.Abs(p.Y - s) < 6 && Math.Abs(p.X - x) < p.Z + 3.2f + gap) return null;
            var px = new Vector2(x, s) * CaveData.Cell;
            foreach (var r in keepOut) if (Math.Abs(px.X - r.Center.X) < r.RxPx + 48 && Math.Abs(px.Y - r.Center.Y) < r.RyPx + 64) return null;
            // how wide is the gap at the ledge's row?
            int row = s + 1, l = ci, rr = ci;
            while (l > 0 && Open(l - 1, row) && ci - l < 12) l--;
            while (rr < W - 1 && Open(rr + 1, row) && rr - ci < 12) rr++;
            int run = rr - l + 1;
            float cx, half;
            if (run >= 10) { half = 2.2f + (float)rng.NextDouble() * 1.0f; cx = x; }
            else if (run >= 6)
            {
                // wall shelf, leaving a 3-cell gap
                float len = run - 3;
                half = len * 0.5f + 0.8f;
                bool left = rng.Next(2) == 0;
                cx = left ? l - 0.8f + half : rr + 1.8f - half;
            }
            else return null;
            foreach (var p in placed)
                if (Math.Abs(p.Y - s) < 6 && Math.Abs(p.X - cx) < p.Z + half + gap) return null;
            StampLedge(cave, cx, s + 1.8f, half);
            placed.Add(new Vector3(cx, s, half));
            return cx;
        }

        void Chain(float x, int s)
        {
            int side = rng.Next(2) == 0 ? -1 : 1;
            for (int step = 0; step < 40; step++)
            {
                if (rng.NextDouble() > Chance(s)) return;
                float? at = TryLedge(x, s);
                if (at == null)
                {
                    // try the other side once before giving up
                    at = TryLedge(x - side * 4, s);
                    if (at == null) return;
                }
                // the next ledge sits off to the side (a clear gap between edges) and one short hop up
                float halfHere = placed[^1].Z;
                x = at.Value + side * (halfHere + 3.3f + Tune.Cave.PlatformGapCells + (float)rng.NextDouble() * 1.5f);
                s -= 3; // 3 cells: within even the warden's jump
                side = -side;
            }
        }

        int spacing = Math.Max(4, Tune.Cave.PlatformSpacingCells);
        // from the water surface
        for (float x = 5; x < W - 5; x += spacing + (float)rng.NextDouble() * 3)
        {
            int i = (int)x;
            bool tall = true;
            for (int j = waterRow - 1; j >= waterRow - 8; j--) if (!Open(i, j)) { tall = false; break; }
            if (tall) Chain(x, waterRow - 3);
        }
        // from the floors of tall dry spaces
        for (float x = 5; x < W - 5; x += spacing + (float)rng.NextDouble() * 3)
        {
            int i = (int)x;
            for (int j = waterRow - 2; j > 8; j--)
            {
                if (!Open(i, j) || Open(i, j + 1)) continue; // standing cells only
                int clear = 0;
                while (clear < 10 && Open(i, j - 1 - clear)) clear++;
                if (clear >= 10) Chain(x + (rng.Next(2) == 0 ? -3 : 3), j - 3 - rng.Next(2));
            }
        }
    }

    /// <summary>A flat-topped rock slab centred at (cx, cy) in cells, 1.6 cells thick.</summary>
    private static void StampLedge(CaveData cave, float cx, float cy, float halfWidth)
    {
        int stride = W + 1;
        const float ry = 0.8f;
        for (int j = (int)(cy - 2); j <= (int)(cy + 2); j++)
            for (int i = (int)(cx - halfWidth - 2); i <= (int)(cx + halfWidth + 2); i++)
            {
                if (i < 0 || j < 0 || i > W || j > H) continue;
                float ex = (i - cx) / halfWidth, ey = (j - cy) / ry;
                float e = MathF.Pow(ex * ex * ex * ex + ey * ey * ey * ey, 0.25f); // squarish: flat top
                float v = Math.Clamp(0.5f - (1 - e) * 0.9f, 0f, 1f);
                int k = j * stride + i;
                if (v < cave.Open[k]) cave.Open[k] = v;
            }
    }

    /// <summary>
    /// Deletes tiny isolated rock specks floating in open space (and tiny sealed air pockets
    /// inside rock) that noise leaves behind; they read as visual glitches.
    /// </summary>
    private static void RemoveSpecks(CaveData cave)
    {
        int stride = W + 1, n = stride * (H + 1);
        var seen = new bool[n];
        var comp = new List<int>();
        var stack = new Stack<int>();
        for (int start = 0; start < n; start++)
        {
            if (seen[start]) continue;
            bool solid = cave.Open[start] < 0.5f;
            comp.Clear();
            stack.Push(start); seen[start] = true;
            bool border = false;
            while (stack.Count > 0)
            {
                int u = stack.Pop(); comp.Add(u);
                int ui = u % stride, uj = u / stride;
                if (ui == 0 || uj == 0 || ui == W || uj == H) border = true;
                if (ui > 0) Visit(u - 1);
                if (ui < W) Visit(u + 1);
                if (uj > 0) Visit(u - stride);
                if (uj < H) Visit(u + stride);
            }
            if (comp.Count <= 40 && !border)
                foreach (int k in comp) cave.Open[k] = solid ? 0.6f : 0.4f;

            void Visit(int v)
            {
                if (seen[v] || (cave.Open[v] < 0.5f) != solid) return;
                seen[v] = true; stack.Push(v);
            }
        }
    }

    private static int CountOpenRuns(CaveData cave, float cx, float cy, float r)
    {
        const int n = 56;
        bool first = cave.SampleCells(cx + r, cy) >= 0.5f;
        bool prev = first; int runs = 0;
        for (int k = 1; k <= n; k++)
        {
            float a = k * Mathf.Tau / n;
            bool o = k == n ? first : cave.SampleCells(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r) >= 0.5f;
            if (o && !prev) runs++;
            prev = o;
        }
        if (runs == 0 && first) runs = 0; // fully open ring: not a dead end
        return runs;
    }

    private static int[] GeodesicFrom(CaveData cave, Vector2I start)
    {
        var dist = new int[W * H];
        Array.Fill(dist, -1);
        var q = new Queue<int>();
        if (!cave.CellOpen(start.X, start.Y)) return dist;
        dist[start.Y * W + start.X] = 0; q.Enqueue(start.Y * W + start.X);
        while (q.Count > 0)
        {
            int u = q.Dequeue(); int ui = u % W, uj = u / W;
            Span<int> nb = stackalloc int[] { ui - 1, uj, ui + 1, uj, ui, uj - 1, ui, uj + 1 };
            for (int k = 0; k < 8; k += 2)
            {
                int i = nb[k], j = nb[k + 1];
                if (i < 0 || j < 0 || i >= W || j >= H) continue;
                int v = j * W + i;
                if (dist[v] >= 0 || !cave.CellOpen(i, j)) continue;
                dist[v] = dist[u] + 1; q.Enqueue(v);
            }
        }
        return dist;
    }

    /// <summary>
    /// Coarse movement model over cells: walk/step-up on ground, a ~4-cell jump with sideways air
    /// control, falling with sideways drift, and free swimming in water (plus jumping out at the
    /// surface). Counts cells reachable from the start that cannot get back to it.
    /// </summary>
    public static void ValidateTraversal(CaveData cave, Vector2I start)
    {
        int n = W * H;
        var oc = new bool[n];
        for (int j = 0; j < H; j++) for (int i = 0; i < W; i++) oc[j * W + i] = cave.CellOpen(i, j);
        float wrow = cave.WaterY / CaveData.Cell;
        bool O(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && oc[j * W + i];
        bool Wt(int i, int j) => O(i, j) && j + 0.5f > wrow;

        var fwd = new List<int>[n];
        const int jumpH = 4;
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                if (!oc[j * W + i]) continue;
                var list = fwd[j * W + i] = new List<int>(8);
                void Add(int a, int b) { if (O(a, b)) list.Add(b * W + a); }
                bool jumpFrom = false;
                if (Wt(i, j))
                {
                    for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) if (di != 0 || dj != 0) Add(i + di, j + dj);
                    if (!Wt(i, j - 1)) jumpFrom = true; // at the surface
                }
                else
                {
                    bool standing = !O(i, j + 1);
                    if (standing)
                    {
                        Add(i - 1, j); Add(i + 1, j);
                        if (O(i, j - 1)) { Add(i - 1, j - 1); Add(i + 1, j - 1); }
                        jumpFrom = true;
                    }
                    else
                    {
                        Add(i, j + 1);
                        if (O(i - 1, j)) Add(i - 1, j + 1);
                        if (O(i + 1, j)) Add(i + 1, j + 1);
                        if (O(i - 1, j)) Add(i - 1, j);
                        if (O(i + 1, j)) Add(i + 1, j);
                    }
                }
                if (jumpFrom)
                {
                    for (int up = 1; up <= jumpH; up++)
                    {
                        if (!O(i, j - up)) break;
                        Add(i, j - up);
                        for (int s = -1; s <= 1; s += 2)
                            for (int dx = 1; dx <= 3; dx++) { if (!O(i + s * dx, j - up)) break; Add(i + s * dx, j - up); }
                    }
                }
            }

        int s0 = start.Y * W + start.X;
        if (!oc[s0]) { cave.TrapCells = int.MaxValue / 2; cave.ReachMask = new bool[n]; cave.TrapMask = new bool[n]; return; }
        var reach = new bool[n];
        var q = new Queue<int>(); reach[s0] = true; q.Enqueue(s0);
        while (q.Count > 0) { int u = q.Dequeue(); foreach (int v in fwd[u]) if (!reach[v]) { reach[v] = true; q.Enqueue(v); } }

        var rev = new List<int>[n];
        for (int u = 0; u < n; u++) if (fwd[u] != null) foreach (int v in fwd[u]) (rev[v] ??= new List<int>(4)).Add(u);
        var back = new bool[n]; back[s0] = true; q.Enqueue(s0);
        while (q.Count > 0) { int u = q.Dequeue(); if (rev[u] == null) continue; foreach (int v in rev[u]) if (!back[v]) { back[v] = true; q.Enqueue(v); } }

        int traps = 0, reachable = 0;
        cave.TrapMask = new bool[n];
        cave.ReachMask = reach;
        for (int u = 0; u < n; u++) { if (reach[u]) { reachable++; if (!back[u]) { traps++; cave.TrapMask[u] = true; } } }
        cave.TrapCells = traps;
        cave.ReachableCells = reachable;
    }

    /// <summary>
    /// Finds the trapped floor cell closest (through open space) to the safe region and builds a
    /// staircase of small rock ledges along that route, 3 cells of height apart.
    /// </summary>
    private static bool RepairTraps(CaveData cave, HashSet<int> tried)
    {
        int n = W * H;
        var prev = new int[n];
        Array.Fill(prev, -2);
        var q = new Queue<int>();
        for (int u = 0; u < n; u++)
            if (cave.ReachMask[u] && !cave.TrapMask[u]) { prev[u] = -1; q.Enqueue(u); }
        int target = -1;
        float wrow = cave.WaterY / CaveData.Cell;
        while (q.Count > 0 && target < 0)
        {
            int u = q.Dequeue(); int ui = u % W, uj = u / W;
            for (int dj = -1; dj <= 1 && target < 0; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int i = ui + di, j = uj + dj;
                    if (i < 0 || j < 0 || i >= W || j >= H) continue;
                    int v = j * W + i;
                    if (prev[v] != -2 || !cave.CellOpen(i, j)) continue;
                    prev[v] = u; q.Enqueue(v);
                    bool standing = !cave.CellOpen(i, j + 1);
                    if (cave.TrapMask[v] && standing && j + 0.5f < wrow && !tried.Contains(v)) { target = v; break; }
                }
        }
        if (target < 0) return false;
        tried.Add(target);

        int stride = W + 1;
        int lastY = target / W, side = 1;
        for (int c = prev[target]; c >= 0; c = prev[c])
        {
            int ci = c % W, cj = c / W;
            if (lastY - cj < 3) continue;
            lastY = cj;
            if (cave.Boss != null && new Vector2(ci, cj).DistanceTo(cave.Boss.Center / CaveData.Cell) < cave.Boss.RxPx / CaveData.Cell + 2) continue;
            float cx = ci + 0.5f + side * 1.2f, cy = cj + 1.8f;
            side = -side;
            const float rx = 2.3f, ry = 0.8f;
            for (int j = (int)(cy - 2); j <= (int)(cy + 2); j++)
                for (int i = (int)(cx - 3); i <= (int)(cx + 3); i++)
                {
                    if (i < 0 || j < 0 || i > W || j > H) continue;
                    float ex = (i - cx) / rx, ey = (j - cy) / ry;
                    float e = MathF.Sqrt(ex * ex + ey * ey);
                    float v = Math.Clamp(0.5f - (1 - e) * 0.9f, 0f, 1f);
                    int k = j * stride + i;
                    if (v < cave.Open[k]) cave.Open[k] = v;
                }
        }
        return true;
    }

    private static void BuildSpawns(CaveData cave, List<Stamp> stamps, Vector2 startCells, Random rng)
    {
        float cell = CaveData.Cell;
        for (int k = 0; k < stamps.Count; k += 8)
        {
            var s = stamps[k];
            var pc = new Vector2(s.X, s.Y);
            if (pc.DistanceTo(startCells) < 28) continue;
            var p = pc * cell;
            if (cave.IsSolid(p)) continue;
            bool spaced = true;
            foreach (var sp in cave.Spawns) if (sp.Pos.DistanceTo(p) < 11 * cell) { spaced = false; break; }
            if (!spaced) continue;
            bool inBossRoom = cave.Boss != null && p.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 3 * cell;
            if (inBossRoom) continue;

            if (p.Y > cave.WaterY + 1.5f * cell)
            {
                double roll = rng.NextDouble();
                if (roll < 0.25 && cave.FindFloor(p, (s.R + 3) * cell, out var fl))
                    cave.Spawns.Add(new SpawnPoint { Pos = fl + new Vector2(0, -8), Kind = SpawnKind.WaterFloor });
                else if (roll < 0.5 && FindWall(cave, p, (s.R + 3) * cell, out var wall, out var nrm))
                    cave.Spawns.Add(new SpawnPoint { Pos = wall + nrm * 6, Kind = SpawnKind.WaterWall, Normal = nrm });
                else
                    cave.Spawns.Add(new SpawnPoint { Pos = p, Kind = SpawnKind.Water });
            }
            else if (p.Y < cave.WaterY - 2 * cell)
            {
                bool hasFloor = cave.FindFloor(p, (s.R + 4) * cell, out var fl);
                bool hasCeil = cave.FindCeiling(p, (s.R + 4) * cell, out var ce);
                if (hasCeil && (!hasFloor || rng.NextDouble() < 0.4))
                    cave.Spawns.Add(new SpawnPoint { Pos = ce + new Vector2(0, 8), Kind = SpawnKind.Ceiling, Normal = Vector2.Down });
                else if (hasFloor && fl.Y < cave.WaterY - 4)
                    cave.Spawns.Add(new SpawnPoint { Pos = fl + new Vector2(0, -14), Kind = SpawnKind.Ground });
            }
        }
    }

    private static bool FindWall(CaveData cave, Vector2 p, float maxDist, out Vector2 hit, out Vector2 normal)
    {
        float best = float.MaxValue; hit = p; normal = Vector2.Up;
        for (int k = 0; k < 8; k++)
        {
            var d = Vector2.Right.Rotated(k * Mathf.Tau / 8);
            if (cave.Raycast(p, d, maxDist, out var h, 4f))
            {
                float dist = h.DistanceTo(p);
                if (dist < best) { best = dist; hit = h; normal = -d; }
            }
        }
        if (best == float.MaxValue) return false;
        normal = cave.OpenGradient(hit + normal * 2);
        return true;
    }

    private static float[] ComputeRockDepth(CaveData cave)
    {
        int stride = W + 1, n = stride * (H + 1);
        var depth = new float[n];
        Array.Fill(depth, 99f);
        var q = new Queue<int>();
        for (int k = 0; k < n; k++) if (cave.Open[k] >= 0.5f) { depth[k] = 0; q.Enqueue(k); }
        while (q.Count > 0)
        {
            int u = q.Dequeue(); int ui = u % stride, uj = u / stride;
            float du = depth[u];
            if (du >= 10) continue;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ui + di, j = uj + dj;
                    if (i < 0 || j < 0 || i > W || j > H) continue;
                    int v = j * stride + i;
                    float nd = du + ((di != 0 && dj != 0) ? 1.414f : 1f);
                    if (nd < depth[v]) { depth[v] = nd; q.Enqueue(v); }
                }
        }
        return depth;
    }
}
