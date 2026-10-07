using System;
using System.Collections.Generic;
using System.Linq;
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
public static partial class CaveGenerator
{
    /// <summary>The biome being generated, and its size in cells (set by Generate).</summary>
    public static BiomeDef B;
    public static int W, H;
    private static float MaxPitch => B?.MaxPitch ?? 0.6f; // ~34 degrees
    private const int ModeAir = 0, ModeShaft = 1, ModeWater = 2;

    private sealed class Walker
    {
        public float X, Y, A, AV, R, TR, Len;
        public int Mode, Gen, WaterSteps, Extra;
        public bool Main, MustExit, Exited, Descender;
    }

    internal struct Stamp { public float X, Y, R; public bool Main; public int Mode; public int Kind; }
    private struct EndInfo { public float X, Y, R, A; public int Mode; }

    public static CaveData Generate(int seed) => Generate(Biomes.Get(BiomeId.Slime), seed);

    /// <summary>Dev switches (--gentest with FR_RAW=1): leave the strict check's repairs and retries off, to see what the generator makes on its own.</summary>
    private static readonly bool LooseAir = System.Environment.GetEnvironmentVariable("FR_LOOSEAIR") != null;
    private static readonly string RawJump = System.Environment.GetEnvironmentVariable("FR_JUMPH");
    public static readonly bool RawMode = System.Environment.GetEnvironmentVariable("FR_RAW") != null;

    /// <summary>--gentest --genverbose: print every attempt's score.</summary>
    public static bool Verbose;
    public static Action<CaveData, int> OnAttempt;

    /// <summary>Set, for an attempt running beside others: true once its result is no longer wanted (an earlier attempt made a sound cave).</summary>
    [ThreadStatic] private static Func<bool> _unwanted;
    /// <summary>Called through the long loops: abandons an attempt whose result is no longer wanted.</summary>
    internal static void CheckCancel() { if (_unwanted != null && _unwanted()) throw new OperationCanceledException(); }

    /// <summary>--gentest --genprof: where the generation time goes (milliseconds and calls by phase).</summary>
    public static readonly Dictionary<string, (long ticks, int calls)> Prof = new();
    internal static T Timed<T>(string what, Func<T> f)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var r = f();
        long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        lock (Prof) { Prof.TryGetValue(what, out var cur); Prof[what] = (cur.ticks + dt, cur.calls + 1); }
        return r;
    }
    internal static void Timed(string what, Action f) => Timed<int>(what, () => { f(); return 0; });

    public static CaveData Generate(BiomeDef biome, int seed)
    {
        // (the dragon's lair is a set piece: an antechamber and one arena, as drawn)
        int w = biome.Style == GenStyle.Arena ? biome.W : (int)MathF.Round(biome.W * Tune.Cave.WidthScale);
        int h = biome.Style is GenStyle.Walkers or GenStyle.Rooms or GenStyle.LavaTube ? (int)MathF.Round(biome.H * Tune.Cave.HeightScale) : biome.H;
        B = biome; W = w; H = h;
        const int MaxAttempts = 12;
        int lanes = Verbose || RawMode || OnAttempt != null ? 1 : int.TryParse(System.Environment.GetEnvironmentVariable("GEN_LANES"), out var gl) ? gl : Math.Clamp(System.Environment.ProcessorCount, 1, 8);
        int next = 0, firstGood = int.MaxValue;
        CaveData Attempt(int attempt)
        {
            // (these belong to the thread making the cave: attempts run side by side)
            B = biome; W = w; H = h;
            _unwanted = lanes > 1 ? () => System.Threading.Volatile.Read(ref firstGood) < attempt : null;
            int s = seed + attempt * 7919;
            var c = Timed("generate once", () => biome.Style switch
            {
                GenStyle.Corridor => GenerateCorridor(s),
                GenStyle.Rooms => GenerateRooms(s),
                GenStyle.Ruins => GenerateRuins(s),
                GenStyle.Crypt => GenerateCrypt(s),
                GenStyle.Lake => GenerateLake(s),
                GenStyle.Mine => GenerateMine(s),
                GenStyle.Arena => GenerateArena(s),
                _ => GenerateOnce(s),
            });
            c.Attempts = attempt + 1;
            return c;
        }
        // Attempts are independent of one another (each is made from its own seed), so they run side by side, and are judged in
        // order: the cave is the one the one-at-a-time loop would have chosen, only sooner. A worker takes the next attempt
        // as it finishes one, and stops once an earlier attempt than the one it would take has already made a sound cave.
        var results = new CaveData[MaxAttempts];
        var scores = new int[MaxAttempts];
        var finished = new bool[MaxAttempts];
        var gate = new object();
        void Work()
        {
            while (true)
            {
                int a = System.Threading.Interlocked.Increment(ref next) - 1;
                if (a >= MaxAttempts || a > System.Threading.Volatile.Read(ref firstGood)) return;
                CaveData c;
                try { c = Attempt(a); }
                catch (OperationCanceledException) { continue; }
                catch (Exception e)
                {
                    // (a generator bug must cost one attempt, not hang the one waiting on it)
                    GD.PushError($"cave attempt {a} failed: {e}");
                    lock (gate) { finished[a] = true; scores[a] = int.MaxValue; System.Threading.Monitor.PulseAll(gate); }
                    continue;
                }
                int sc = Score(c);
                lock (gate)
                {
                    results[a] = c; scores[a] = sc; finished[a] = true;
                    if (sc <= 6 && a < firstGood) firstGood = a;
                    System.Threading.Monitor.PulseAll(gate);
                }
            }
        }
        if (lanes == 1) Work();
        else for (int k = 0; k < lanes; k++) System.Threading.Tasks.Task.Run(Work);
        CaveData best = null; int bestScore = int.MaxValue;
        for (int a = 0; a < MaxAttempts; a++)
        {
            lock (gate) { while (!finished[a]) System.Threading.Monitor.Wait(gate); }
            var c = results[a];
            if (c == null) continue;
            if (Verbose) { GD.Print($"    attempt {a + 1} (seed {seed + a * 7919}): score {scores[a]} traps {c.TrapCells}"); OnAttempt?.Invoke(c, seed + a * 7919); }
            if (best == null || scores[a] < bestScore) { best = c; bestScore = scores[a]; }
            if (scores[a] <= 6) break;
        }
        if (best == null) throw new InvalidOperationException("every cave attempt failed");
        B = biome; W = w; H = h;
        AddDrain(best, seed);
        AddHiddenNooks(best, seed);
        ConvertSmallPlatforms(best);
        LiftLedges(best);
        return best;
    }

    /// <summary>Lower is better: trapped cells, plus a big penalty for a missing or nearby boss room.</summary>
    private static int Score(CaveData c)
    {
        int score = c.TrapCells;
        if (c.Boss == null) score += 100000;
        else
        {
            // standing height just above the exit chamber's floor
            if (!BossFloorReached(c)) score += 50000;
            // (a guardian a real hero can't reach, though the coarse check says it can be: nearly as bad)
            if (!c.FineOk && c.Attempts <= 6 && !RawMode) score += 20000;
            if (B.Style == GenStyle.Walkers && c.Boss.Center.DistanceTo(c.StartPos) < W * 0.33f * CaveData.Cell) score += 5000;
            int minis = 0;
            foreach (var r in c.Rooms) if (r.Kind == RoomKind.MiniBoss) minis++;
            if (minis < B.MiniBossesMin) score += 2000;
        }
        // (a level without a vault, where there should be one: worse than a clean cave with one,
        // never worse than a trapped cave)
        if (c.Vault == null && B.Style != GenStyle.Arena) score += c.Attempts <= 8 ? 30000 : 7;
        return score;
    }

    public static CaveData GenerateOnce(int seed)
    {
        var rng = new Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        // no liquid: the "water line" sits below the map, so nothing ever counts as submerged
        // only water gets flooded tunnel systems; lava caves are carved dry and the lava then
        // pools in the lowest basins (see below)
        bool flooded = B.Liquid == Liquid.Water;
        float waterRow = flooded ? H * (1f - B.LiquidFraction) : H + 200;
        // descenders head for the water, or (in dry caves) for the lower reaches of the map
        float descendTo = flooded ? waterRow + 2 : H * 0.78f;

        var stamps = new List<Stamp>(4096);
        var ends = new List<EndInfo>();
        var beaches = new List<Vector2>();
        var queue = new Queue<Walker>();
        int total = 0;
        int budget = (int)(B.TunnelBudget * Tune.Cave.WidthScale * (H / (float)B.H));

        float sx = W * 0.5f + Rnd(-W * 0.08f, W * 0.08f);
        // (well down from the top of the map: the way in you came by is behind you, up the stairs)
        float sy = H * B.StartRow + Rnd(-2, 3);

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
            CheckCancel();
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
                        if (!w.Descender) w.A = Mathf.LerpAngle(w.A, Mathf.Cos(w.A) >= 0 ? 0 : Mathf.Pi, B.HorizontalBias);
                        if (B.Diagonal && !w.Descender)
                        {
                            // tight tunnels run flat or at a steady diagonal
                            float hsd = Mathf.Cos(w.A) >= 0 ? 1 : -1;
                            float pd = Mathf.Asin(Mathf.Clamp(Mathf.Sin(w.A), -1, 1));
                            float snap = Math.Abs(pd) < MaxPitch * 0.45f ? 0 : Math.Sign(pd) * MaxPitch * 0.95f;
                            float target = hsd > 0 ? snap : Mathf.Pi - snap;
                            w.A = Mathf.LerpAngle(w.A, target, 0.15f);
                        }
                        if (w.Descender && w.Y >= descendTo) w.Descender = false;
                        if (w.Descender && w.Y < descendTo)
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

                if (rng.NextDouble() < 0.02) w.TR = w.Mode == ModeWater ? Rnd(3.0f, 4.7f) : Rnd(B.AirRMin, B.AirRMax);
                w.R += (w.TR - w.R) * 0.04f;
                w.X += Mathf.Cos(w.A) * 0.9f;
                w.Y += Mathf.Sin(w.A) * 0.9f;
                w.X = Mathf.Clamp(w.X, 7, W - 7);
                w.Y = Mathf.Clamp(w.Y, 7, H - 7);
                stamps.Add(new Stamp { X = w.X, Y = w.Y, R = w.R, Main = w.Main && w.Mode != ModeShaft, Mode = w.Mode, Kind = w.Descender ? 6 : w.Mode });
                w.Len -= 1; total++;

                if (w.Mode != ModeShaft && total < budget && w.Gen < B.MaxBranchGen && rng.NextDouble() < B.BranchChance)
                {
                    var c = new Walker
                    {
                        X = w.X, Y = w.Y, Gen = w.Gen + 1,
                        R = Math.Min(w.R, 3.4f), TR = w.Mode == ModeAir ? Rnd(B.AirRMin, B.AirRMax) : Rnd(3.0f, 4.2f),
                        Len = Rnd(70, 200) * (1f - w.Gen * 0.11f),
                    };
                    if (flooded && w.Mode == ModeAir && w.Y < waterRow - 12 && rng.NextDouble() < 0.2)
                    {
                        c.Mode = ModeShaft; c.A = Mathf.Pi / 2 + Rnd(-0.3f, 0.3f); c.MustExit = true; c.Len = Math.Max(c.Len, 70);
                    }
                    else if (w.Mode == ModeAir)
                    {
                        c.Mode = ModeAir; c.Main = w.Main;
                        float hs = rng.NextDouble() < 0.6 ? -Mathf.Sign(Mathf.Cos(w.A)) : Mathf.Sign(Mathf.Cos(w.A));
                        float p = Rnd(-MaxPitch, MaxPitch) * B.BranchPitchMult;
                        if (w.Main && w.Y < descendTo - 12 && rng.NextDouble() < 0.12) { p = MaxPitch * 0.85f; c.Descender = true; c.Len = Math.Max(c.Len, 60); }
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
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = waterRow * CaveData.Cell, Liquid = B.Liquid, Biome = B };
        int stride = W + 1;
        var open = new float[stride * (H + 1)];
        var rough = new float[stride * (H + 1)];
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
                rough[j * stride + i] = (float)LatticeNoise3D.SampleFbm(i, j, 0.5, 11.3, 7.7, 0, seed, 3, 0.11, 0.55, 2.0) * B.RoughAmp;

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
                float rx = Rnd(5, 6.5f) * B.RoomScale, ry = Rnd(3.8f, 4.8f) * B.RoomScale;
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
                float rx = Rnd(6, 8.5f) * B.RoomScale, ry = Rnd(4.5f, 6.2f) * B.RoomScale;
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
            int want = B.MiniBossesMin + rng.Next(B.MiniBossesMax - B.MiniBossesMin + 1);
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
        if (B.Liquid == Liquid.Lava)
        {
            // lava fills the bottoms of the lowest tunnels, a few cells deep, below the exit chamber
            int lowest = 0;
            for (int j = 0; j < H; j++) for (int i = 0; i < W; i++) if (cave.CellOpen(i, j)) lowest = Math.Max(lowest, j);
            float row = lowest - 3.5f;
            // a biome with chests down in the lava lets it rise into a proper lake (for Magma
            // Skin to swim in), though never over a room's floor or where the hero starts
            float ceiling = cave.StartPos.Y / CaveData.Cell + 2;
            foreach (var r in cave.Rooms) ceiling = Math.Max(ceiling, r.Floor.Y / CaveData.Cell + 1);
            if (B.LavaCaches > 0)
                for (int k = 0; k < 12 && row - 1 > ceiling && LavaCells(cave, row) < Tune.Drops.LavaLakeCells; k++) row -= 1;
            if (cave.Boss != null) row = Math.Max(row, cave.Boss.Floor.Y / CaveData.Cell + 2);
            cave.WaterY = row * CaveData.Cell;
            // still too little lake (the guardian's chamber lies lowest): lava wells, shafts sunk
            // from low tunnel floors down past the lava's surface, each with room for a chest
            if (B.LavaCaches > 0)
            {
                var wells = new List<int>();
                for (int tries = 0; tries < 600 && wells.Count < B.LavaCaches && LavaCells(cave, row) < Tune.Drops.LavaLakeCells; tries++)
                {
                    int i = rng.Next(10, W - 10);
                    if (wells.Any(w => Math.Abs(w - i) < 28) || Math.Abs(i - cave.StartPos.X / CaveData.Cell) < 30) continue;
                    if (cave.Boss != null && Math.Abs(i - cave.Boss.Floor.X / CaveData.Cell) < cave.Boss.RxPx / CaveData.Cell + 10) continue;
                    if (cave.Rooms.Any(r => Math.Abs(i - r.Floor.X / CaveData.Cell) < r.RxPx / CaveData.Cell + 4 && Math.Abs(row - r.Floor.Y / CaveData.Cell) < 20)) continue;
                    // the lowest floor in this column with headroom, not far above the lava
                    int f = -1;
                    for (int j = (int)row - 1; j > row - 18 && j > 4; j--)
                        if (cave.CellOpen(i, j) && cave.CellOpen(i, j - 1) && cave.CellOpen(i, j - 2) && !cave.CellOpen(i, j + 1)) { f = j; break; }
                    if (f < 0 || row + 6 > H - 4) continue;
                    for (float y = f; y <= row + 5; y += 0.8f) Carve(i, y, 2.4f);
                    wells.Add(i);
                }
            }
        }
        Timed("platforms", () => AddPlatforms(cave, rng));

        // hidden chimneys up out of the tunnels' roofs (after the ledges, so none of them gets a stair up it)
        if (B.SecretChimneys > 0)
        {
            open = cave.Open;
            int made = 0;
            for (int tries = 0; tries < 500 && made < B.SecretChimneys; tries++)
            {
                if (stamps.Count == 0) break;
                var st = stamps[rng.Next(stamps.Count)];
                if (!st.Main || st.Mode != ModeAir) continue;
                int ci = (int)st.X;
                if (ci < 20 || ci > W - 20 || cave.Rooms.Any(r => Math.Abs(ci - r.Center.X / CaveData.Cell) < 14 + r.RxPx / CaveData.Cell && r.Kind is RoomKind.Boss or RoomKind.Start)) continue;
                if (cave.Rooms.Any(r => r.Kind == RoomKind.Secret && Math.Abs(ci - r.Center.X / CaveData.Cell) < 40)) continue;
                // the tube's roof above this column (the first rock over open ground)
                int j = (int)st.Y;
                if (!cave.CellOpen(ci, j)) continue;
                while (j > 4 && cave.CellOpen(ci, j - 1)) j--;
                int roof = j;
                int len = rng.Next(20, 29), topRow = roof - len;
                if (topRow < 10) continue;
                float drift = Rnd(-7, 7);
                float cxs = Math.Clamp(ci + drift, 12, W - 12), floorRow = topRow + 5.5f;
                // rock all the way up: the shaft, the chamber and a margin round them
                bool solid = true;
                for (int jj = topRow - 7; jj <= roof - 2 && solid; jj++)
                    for (int ii = (int)(Math.Min(ci, cxs) - 10); ii <= (int)(Math.Max(ci, cxs) + 10) && solid; ii++)
                        if (ii < 3 || ii > W - 3 || cave.CellOpen(ii, jj)) solid = false;
                if (!solid) continue;
                // the shaft, leaning toward the chamber
                float holeX = cxs - 2.4f;
                for (float y = roof + 1; y >= floorRow - 0.5f; y -= 0.8f)
                {
                    float t = Mathf.Clamp((roof - y) / Math.Max(1f, roof - floorRow), 0f, 1f);
                    Carve(Mathf.Lerp(ci, holeX, t * t * (3 - 2 * t)), y, 1.9f);
                }
                Dome(cxs, floorRow, 6f, 4.4f, true);
                // (the hole's rim: the floor stays solid under the chest's side)
                for (int jj = (int)floorRow; jj <= (int)floorRow + 2; jj++)
                    for (int ii = (int)(cxs + 0.6f); ii <= (int)(cxs + 8); ii++)
                    {
                        int k = jj * stride + ii;
                        if (ii <= W) open[k] = Math.Min(open[k], Math.Clamp(0.5f - (jj - floorRow) * 0.5f, 0f, 1f));
                    }
                cave.Rooms.Add(new Room
                {
                    Kind = RoomKind.Secret,
                    Center = new Vector2(cxs + 1.5f, floorRow - 2.2f) * CaveData.Cell,
                    Floor = new Vector2(cxs + 1.5f, floorRow) * CaveData.Cell,
                    RxPx = 5f * CaveData.Cell, RyPx = 3.5f * CaveData.Cell,
                });
                made++;
            }
        }

        // Reachability validation, with repairs: stepping-stone ledges up out of any pit the
        // movement model says you could fall into but not climb out of.
        Timed("validate+repair", () => ValidateAndRepair(cave, startCell));

        Timed("spawns", () => { BuildSpawns(cave, stamps, new Vector2(sx, sy), rng); BuildShores(cave, stamps); });
        Timed("vault", () => CarveVault(cave, rng));
        Timed("rubble", () => RubbleGen.Build(cave));
        cave.RockDepth = Timed("rockdepth", () => ComputeRockDepth(cave));
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
    internal static void AddPlatforms(CaveData cave, Random rng)
    {
        // no liquid: measure heights from the bottom of the map instead
        int waterRow = Math.Min((int)(cave.WaterY / CaveData.Cell), H - 3);
        bool Open(int i, int j) => cave.CellOpen(i, j);
        var placed = new List<Vector3>(); // (centre x, standing row, half width)
        var keepOut = new List<Room>();
        foreach (var r in cave.Rooms) if (r.Kind is RoomKind.Boss or RoomKind.Start) keepOut.Add(r);

        float Chance(int standRow)
        {
            float h = Math.Clamp((waterRow - standRow) / (float)Math.Max(1, waterRow - 4), 0f, 1f);
            return Mathf.Lerp(B.PlatformBottom, B.PlatformTop, h);
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
            // frozen caverns: the ledges are breakable ice (placed as objects by the level), not rock
            if (B.IcePlatforms) cave.IceLedges.Add(new Vector3(cx, s + 1.0f, half));
            else StampLedge(cave, cx, s + 1.8f, half);
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
                int clear = 0, need = B.TallThreshold;
                while (clear < need && Open(i, j - 1 - clear)) clear++;
                if (clear >= need) Chain(x + (rng.Next(2) == 0 ? -3 : 3), j - 3 - rng.Next(2));
            }
        }
    }

    /// <summary>
    /// Any platform the cave grew by itself that is small enough (a free-floating island of rock, thin and wider than it is deep) is taken
    /// out of the field and made one of the slabs that can be broken, like the ledges the generator lays: it is the same stone, but it can
    /// be brought down, so none of them can trap anyone or wall the way off. Counted in <see cref="CaveData.NaturalPlatforms"/>.
    /// </summary>
    internal static void ConvertSmallPlatforms(CaveData cave)
    {
        if (B == null || B.Style == GenStyle.Arena) return;
        int stride = W + 1, n = stride * (H + 1);
        var stamped = new HashSet<int>();
        foreach (var l in cave.Ledges) foreach (var (idx, _, _) in l.Cells) stamped.Add(idx);
        var seen = new bool[n];
        var comp = new List<int>();
        var stack = new Stack<int>();
        for (int start = 0; start < n; start++)
        {
            if (seen[start] || cave.Open[start] >= 0.5f) continue;
            comp.Clear();
            stack.Push(start); seen[start] = true;
            bool border = false, ours = false;
            int imin = int.MaxValue, imax = int.MinValue, jmin = int.MaxValue, jmax = int.MinValue;
            while (stack.Count > 0)
            {
                int u = stack.Pop(); comp.Add(u);
                int ui = u % stride, uj = u / stride;
                if (ui == 0 || uj == 0 || ui == W || uj == H) border = true;
                if (stamped.Contains(u)) ours = true;
                imin = Math.Min(imin, ui); imax = Math.Max(imax, ui); jmin = Math.Min(jmin, uj); jmax = Math.Max(jmax, uj);
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int vi = ui + di, vj = uj + dj;
                        if (vi < 0 || vj < 0 || vi > W || vj > H) continue;
                        int v = vj * stride + vi;
                        if (seen[v] || cave.Open[v] >= 0.5f) continue;
                        seen[v] = true; stack.Push(v);
                    }
            }
            int w = imax - imin + 1, hgt = jmax - jmin + 1;
            // (small ones only: about the size of a ledge the generator lays; anything bigger is the cave's ground, and stays)
            if (border || ours || comp.Count > MaxPlatformVertices || hgt > 4 || w < 4 || w > 10 || w < hgt * 2f) continue;
            // (the surface of its middle: where the field crosses a half, top and bottom, in the columns away from its rounded ends)
            var tops = new List<float>(); var bots = new List<float>();
            for (int i = imin; i <= imax; i++)
            {
                int jt = -1, jb = -1;
                for (int j = jmin; j <= jmax; j++) if (cave.Open[j * stride + i] < 0.5f) { if (jt < 0) jt = j; jb = j; }
                if (jt < 0) continue;
                float vt = cave.Open[jt * stride + i], vb = cave.Open[jb * stride + i];
                float up = jt > 0 ? cave.Open[(jt - 1) * stride + i] : 1f, dn = jb < H ? cave.Open[(jb + 1) * stride + i] : 1f;
                tops.Add(jt - 1 + (up > vt ? Math.Clamp((up - 0.5f) / (up - vt), 0f, 1f) : 1f));
                bots.Add(jb + (dn > vb ? Math.Clamp((0.5f - vb) / (dn - vb), 0f, 1f) : 0f));
            }
            if (tops.Count == 0) continue;
            tops.Sort(); bots.Sort();
            float top = tops[tops.Count / 2], bot = bots[bots.Count / 2];
            if (bot - top < 0.8f) continue;
            var rec = new LedgeRec { Cx = (imin + imax) * 0.5f, Cy = (top + bot) * 0.5f, Half = (w - 1) * 0.5f + 0.2f, Thick = bot - top, Natural = true };
            // the corners it holds: its own solid ones, and the half-closed ring about them (but not one a neighbouring rock shares)
            var own = new HashSet<int>(comp);
            foreach (int u in comp)
            {
                int ui = u % stride, uj = u / stride;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int vi = ui + di, vj = uj + dj;
                        if (vi < 0 || vj < 0 || vi > W || vj > H) continue;
                        int v = vj * stride + vi;
                        if (own.Contains(v) || cave.Open[v] >= 0.999f) continue;
                        bool shared = false;
                        for (int ej = -1; ej <= 1 && !shared; ej++)
                            for (int ei = -1; ei <= 1 && !shared; ei++)
                            {
                                int wi = vi + ei, wj = vj + ej;
                                if (wi < 0 || wj < 0 || wi > W || wj > H) continue;
                                int x = wj * stride + wi;
                                if (!own.Contains(x) && cave.Open[x] < 0.5f) shared = true;
                            }
                        if (!shared) own.Add(v);
                    }
            }
            foreach (int idx in own) rec.Cells.Add((idx, 1f, cave.Open[idx]));
            cave.Ledges.Add(rec);
            cave.NaturalPlatforms++;
        }
    }

    /// <summary>The largest island (in field corners) taken for a platform.</summary>
    private const int MaxPlatformVertices = 64;

    /// <summary>A flat-topped rock slab centred at (cx, cy) in cells, 1.6 cells thick.</summary>
    internal static void StampLedge(CaveData cave, float cx, float cy, float halfWidth)
    {
        int stride = W + 1;
        const float ry = 0.8f;
        // (the dragon's tiers are laid out as drawn: they stay in the rock; every other ledge is lifted out later, to be broken)
        var rec = B != null && B.Style != GenStyle.Arena ? new LedgeRec { Cx = cx, Cy = cy, Half = halfWidth } : null;
        for (int j = (int)(cy - 2); j <= (int)(cy + 2); j++)
            for (int i = (int)(cx - halfWidth - 2); i <= (int)(cx + halfWidth + 2); i++)
            {
                if (i < 0 || j < 0 || i > W || j > H) continue;
                float ex = (i - cx) / halfWidth, ey = (j - cy) / ry;
                float e = MathF.Pow(ex * ex * ex * ex + ey * ey * ey * ey, 0.25f); // squarish: flat top
                float v = Math.Clamp(0.5f - (1 - e) * 0.9f, 0f, 1f);
                int k = j * stride + i;
                if (v < cave.Open[k]) { rec?.Cells.Add((k, cave.Open[k], v)); cave.Open[k] = v; }
            }
        if (rec != null) cave.Ledges.Add(rec);
    }

    /// <summary>
    /// The ledges go out of the field (the corners they changed go back to what they were, unless something has opened them wider
    /// since) once the level is checked: the level puts them back as slabs that can be broken, so none can trap anyone or wall off the
    /// guardian for good.
    /// </summary>
    internal static void LiftLedges(CaveData cave)
    {
        // (a ledge a repair has since cut through, or taken back, is not lifted: whatever of it is left stays in the rock as it was left)
        cave.Ledges.RemoveAll(l =>
        {
            int still = 0;
            foreach (var (idx, _, now) in l.Cells) if (cave.Open[idx] <= now + 0.001f) still++;
            return l.Cells.Count == 0 || still < l.Cells.Count;
        });
        foreach (var l in cave.Ledges)
            foreach (var (idx, old, _) in l.Cells)
                cave.Open[idx] = Math.Max(cave.Open[idx], old);
    }

    /// <summary>The ledges back into the field (the generator test checks the level as it stands before anything is broken).</summary>
    internal static void PutLedgesBack(CaveData cave)
    {
        foreach (var l in cave.Ledges)
            foreach (var (idx, _, now) in l.Cells)
                cave.Open[idx] = Math.Min(cave.Open[idx], now);
    }

    /// <summary>
    /// Deletes tiny isolated rock specks floating in open space (and tiny sealed air pockets
    /// inside rock) that noise leaves behind; they read as visual glitches.
    /// </summary>
    internal static void RemoveSpecks(CaveData cave)
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

    /// <summary>Open cells below a liquid surface at <paramref name="row"/>.</summary>
    private static int LavaCells(CaveData cave, float row)
    {
        int n = 0;
        for (int j = (int)Math.Ceiling(row); j < cave.H; j++) for (int i = 0; i < cave.W; i++) if (j + 0.5f > row && cave.CellOpen(i, j)) n++;
        return n;
    }

    /// <summary>
    /// Coarse movement model over cells: walk/step-up on ground, a ~4-cell jump with sideways air
    /// control, falling with sideways drift, and free swimming in water (plus jumping out at the
    /// surface). Counts cells reachable from the start that cannot get back to it.
    /// </summary>
    public static void ValidateTraversal(CaveData cave, Vector2I start) => Timed("traversal", () => ValidateTraversalImpl(cave, start));

    /// <summary>The traversal check's working arrays, kept per thread between calls (it runs hundreds of times to a cave, and they run to megabytes).</summary>
    private sealed class TravBuffers
    {
        public bool[] Op, Plat, Seen;
        public int[] First, Edge, Queue, RFirst, REdge;
    }
    [ThreadStatic] private static TravBuffers _trav;

    private static void ValidateTraversalImpl(CaveData cave, Vector2I start)
    {
        int n = W * H;
        const int Pad = 4;
        int pw = W + 2 * Pad;
        var tb = _trav ??= new TravBuffers();
        if (tb.First == null || tb.First.Length != n + 1 || tb.Op.Length != pw * (H + 2 * Pad))
        {
            tb.Op = new bool[pw * (H + 2 * Pad)]; tb.Plat = new bool[n]; tb.Seen = new bool[n];
            tb.First = new int[n + 1]; tb.Edge = new int[n * 6]; tb.Queue = new int[n]; tb.RFirst = new int[n + 2]; tb.REdge = new int[n * 6];
        }
        else { Array.Clear(tb.Plat); Array.Clear(tb.Seen); Array.Clear(tb.RFirst); }
        // which cells are open, in a grid with a margin of rock round it (so no move needs a bounds check): CellOpen's own sum
        var op = tb.Op;
        var oc = new bool[n];
        {
            var o = cave.Open;
            int stride = W + 1;
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * stride + i;
                    float a = o[k], b = o[k + 1], c = o[k + stride], d = o[k + stride + 1];
                    float top = a + (b - a) * 0.5f, bot = c + (d - c) * 0.5f;
                    bool open = top + (bot - top) * 0.5f >= 0.5f;
                    oc[j * W + i] = open;
                    op[(j + Pad) * pw + i + Pad] = open;
                }
        }
        // (frozen caverns' ledges are objects, not rock: a cell above one is stood on all the same)
        var plat = tb.Plat;
        foreach (var l in cave.IceLedges)
            for (int i = (int)MathF.Floor(l.X - l.Z); i <= (int)MathF.Ceiling(l.X + l.Z); i++)
                if (i >= 0 && i < W && (int)l.Y >= 0 && (int)l.Y < H) plat[(int)l.Y * W + i] = true;
        float wrow = cave.WaterY / CaveData.Cell;

        // the moves between cells, as flat arrays (edges of cell u are edge[first[u] .. first[u + 1]]): no lists to allocate, and
        // the same edges in the same order as ever
        var first = tb.First;
        var edge = tb.Edge;
        int ne = 0;
        // (what the slowest hero clears: about 66 px at full height, four cells with nothing to spare)
        int jumpH = RawJump != null ? int.Parse(RawJump) : 4;
        for (int j = 0; j < H; j++)
        {
            bool wetRow = j + 0.5f > wrow, wetAbove = j - 1 + 0.5f > wrow;
            for (int i = 0; i < W; i++)
            {
                int u = j * W + i;
                first[u] = ne;
                if (!oc[u]) continue;
                if (ne + 64 > edge.Length) { Array.Resize(ref edge, edge.Length * 2); tb.Edge = edge; }
                int p = (j + Pad) * pw + i + Pad;
                // (the cell at an offset, as the margined grid has it, and its index among the cells)
                bool jumpFrom = false;
                if (wetRow)
                {
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                            if ((di != 0 || dj != 0) && op[p + dj * pw + di]) edge[ne++] = u + dj * W + di;
                    // at the surface
                    if (!(op[p - pw] && wetAbove)) jumpFrom = true;
                }
                else
                {
                    bool standing = !op[p + pw] || (j + 1 < H && plat[u + W]);
                    if (standing)
                    {
                        // walking needs headroom: the hero is two cells tall
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            if (op[p - pw + sd]) { if (op[p + sd]) edge[ne++] = u + sd; }
                            if (op[p - pw] && op[p - 2 * pw + sd] && op[p - pw + sd]) edge[ne++] = u - W + sd;
                        }
                        jumpFrom = true;
                    }
                    else
                    {
                        if (op[p + pw]) edge[ne++] = u + W;
                        if (op[p - 1] && op[p + pw - 1]) edge[ne++] = u + W - 1;
                        if (op[p + 1] && op[p + pw + 1]) edge[ne++] = u + W + 1;
                        // (no sideways steps level with the air one is in: a body that falls drifts about as far as it falls,
                        // not any distance at all)
                        if (LooseAir)
                        {
                            if (op[p - 1]) edge[ne++] = u - 1;
                            if (op[p + 1]) edge[ne++] = u + 1;
                        }
                    }
                }
                if (jumpFrom)
                {
                    for (int up = 1; up <= jumpH; up++)
                    {
                        int pu = p - up * pw;
                        if (!op[pu]) break;
                        edge[ne++] = u - up * W;
                        for (int sd = -1; sd <= 1; sd += 2)
                            for (int dx = 1; dx <= 3; dx++)
                            {
                                if (!op[pu + sd * dx]) break;
                                edge[ne++] = u - up * W + sd * dx;
                            }
                    }
                }
            }
        }
        first[n] = ne;

        cave.OpenCells = oc;
        int s0 = start.Y * W + start.X;
        if (!oc[s0]) { cave.TrapCells = int.MaxValue / 2; cave.ReachMask = new bool[n]; cave.TrapMask = new bool[n]; return; }
        var reach = new bool[n];
        var q = tb.Queue;
        int qh = 0, qt = 0;
        reach[s0] = true; q[qt++] = s0;
        while (qh < qt) { int u = q[qh++]; for (int e = first[u]; e < first[u + 1]; e++) { int v = edge[e]; if (!reach[v]) { reach[v] = true; q[qt++] = v; } } }

        // the same moves reversed (a counting sort of the edges by where they end), only those from cells the start reaches: a cell
        // that can get back to the start is one of them, and so is every cell on its way
        var rfirst = tb.RFirst;
        for (int u = 0; u < n; u++)
            if (reach[u]) for (int e = first[u]; e < first[u + 1]; e++) rfirst[edge[e] + 2]++;
        for (int u = 0; u < n; u++) rfirst[u + 2] += rfirst[u + 1];
        if (tb.REdge.Length < rfirst[n + 1]) tb.REdge = new int[rfirst[n + 1] * 2];
        var redge = tb.REdge;
        for (int u = 0; u < n; u++)
            if (reach[u]) for (int e = first[u]; e < first[u + 1]; e++) redge[rfirst[edge[e] + 1]++] = u;
        // (rfirst[v + 1] is now the end of v's edges, and rfirst[v] their start)
        var back = new bool[n];
        qh = qt = 0;
        back[s0] = true; q[qt++] = s0;
        while (qh < qt) { int u = q[qh++]; for (int e = rfirst[u]; e < rfirst[u + 1]; e++) { int v = redge[e]; if (!back[v]) { back[v] = true; q[qt++] = v; } } }

        int traps = 0, reachable = 0;
        cave.TrapMask = new bool[n];
        cave.ReachMask = reach;
        for (int u = 0; u < n; u++) { if (reach[u]) { reachable++; if (!back[u]) cave.TrapMask[u] = true; } }
        // A cell or two on its own is a notch in a wall the coarse model can slip into but not out
        // of, not a pit: anything you could really fall into holds a floor and headroom above it.
        var seen = tb.Seen;
        var group = new List<int>();
        for (int u = 0; u < n; u++)
        {
            if (!cave.TrapMask[u] || seen[u]) continue;
            group.Clear();
            seen[u] = true; qh = qt = 0; q[qt++] = u;
            while (qh < qt)
            {
                int c = q[qh++]; group.Add(c);
                int ci = c % W, cj = c / W;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int i = ci + di, j = cj + dj;
                        if (i < 0 || j < 0 || i >= W || j >= H) continue;
                        int v = j * W + i;
                        if (cave.TrapMask[v] && !seen[v]) { seen[v] = true; q[qt++] = v; }
                    }
            }
            if (group.Count < 4) foreach (int c in group) cave.TrapMask[c] = false;
            else traps += group.Count;
        }
        cave.TrapCells = traps;
        cave.ReachableCells = reachable;
    }

    /// <summary>
    /// The traversal check, then repairs until nothing is trapped and the guardian's floor can be
    /// reached (or no repair is left to try): ledges up out of pits, and up to the chamber.
    /// </summary>
    internal static void ValidateAndRepair(CaveData cave, Vector2I startCell)
    {
        ValidateTraversal(cave, startCell);
        var tried = new HashSet<int>();
        bool reachGaveUp = false;
        // (more room to repair on the wider maps)
        int budget = (int)(Tune.Cave.RepairBudget * Tune.Cave.WidthScale);
        for (int rep = 0; rep < budget; rep++)
        {
            CheckCancel();
            var open = (float[])cave.Open.Clone();
            var (reach, trap, traps, count) = (cave.ReachMask, cave.TrapMask, cave.TrapCells, cave.ReachableCells);
            bool bossBefore = BossFloorReached(cave), reaching = false;
            if (!(cave.TrapCells > 6 && Timed("repair traps", () => RepairTraps(cave, tried))))
            {
                if (reachGaveUp || !Timed("repair reach", () => RepairReach(cave))) break;
                reaching = true;
            }
            ValidateTraversal(cave, startCell);
            // A ledge that plugs a narrow tunnel cuts off everything past it: undo that repair. (A
            // pit closed off is a fine way to fix a pit, and a few cells tucked under a new ledge
            // don't count, but a stair up to the guardian must only ever add to where you can go.)
            int lost = 0;
            for (int u = 0; u < reach.Length; u++)
                if (reach[u] && !cave.ReachMask[u] && cave.CellOpen(u % W, u / W)) lost++;
            if ((bossBefore && !BossFloorReached(cave)) || (reaching && lost > 40))
            {
                cave.Open = open;
                (cave.ReachMask, cave.TrapMask, cave.TrapCells, cave.ReachableCells) = (reach, trap, traps, count);
                if (reaching) reachGaveUp = true;
            }
        }
        // then the strict check: a real body, real jumps (see FineReach), repaired where it fails
        if (cave.Boss != null)
        {
            var start = cave.StartPos != Vector2.Zero ? cave.StartPos : new Vector2((startCell.X + 0.5f) * CaveData.Cell, (startCell.Y + 0.5f) * CaveData.Cell);
            cave.FineRepairs = 0;
            float[] openBefore = null; Vector3[] iceBefore = null;
            var triedGoals = new HashSet<int>();
            int before = 0;
            for (int it = 0; it < 5; it++)
            {
                CheckCancel();
                var fine = Timed("fine build", () => new FineReach(cave));
                bool ok = Timed("fine reach", () => fine.Run(start, cave.Boss.Floor));
                if (Verbose) GD.Print($"      fine it {it}: ok {ok} reached {fine.ReachedCount} start {start} boss {cave.Boss.Floor}");
                var probe = System.Environment.GetEnvironmentVariable("FR_PROBE");
                if (probe != null && it == int.Parse(System.Environment.GetEnvironmentVariable("FR_IT") ?? "0") && probe.Split(',') is var pp && int.Parse(pp[0]) == cave.Seed) fine.Probe(int.Parse(pp[1]), int.Parse(pp[2]), int.Parse(pp[3]), int.Parse(pp[4]));
                if (it > 0 && !ok && fine.ReachedCount < before * 0.97f)
                {
                    // the last repair walled off more than it opened: take it back
                    cave.Open = openBefore; cave.IceLedges.Clear(); cave.IceLedges.AddRange(iceBefore);
                    break;
                }
                if (ok) { cave.FineOk = true; break; }
                cave.FineOk = false;
                before = fine.ReachedCount;
                openBefore = (float[])cave.Open.Clone(); iceBefore = cave.IceLedges.ToArray();
                if (!Timed("fine repair", () => FineRepairStep(cave, fine, triedGoals))) break;
                cave.FineRepairs++;
            }
            ValidateTraversal(cave, startCell);
        }
    }

    /// <summary>
    /// The way on to the guardian, by the cheapest route (digging costs, open space doesn't) from
    /// anywhere a body can get to: the rock on it is cut away to a passage four cells wide, and
    /// ledges stand every three cells of climb.
    /// </summary>
    internal static bool FineRepairStep(CaveData cave, FineReach fine, HashSet<int> tried)
    {
        int n = W * H;
        var dist = new float[n];
        var prev = new int[n];
        Array.Fill(dist, float.MaxValue); Array.Fill(prev, -2);
        var pq = new PriorityQueue<int, float>();
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                if (fine.CellReached(i, j)) { dist[j * W + i] = 0; prev[j * W + i] = -1; pq.Enqueue(j * W + i, 0); }
        if (pq.Count == 0) return false;
        int bi = (int)(cave.Boss.Floor.X / CaveData.Cell), bj = (int)(cave.Boss.Floor.Y / CaveData.Cell) - 2;
        while (pq.Count > 0)
        {
            pq.TryDequeue(out int u, out float du);
            if (du > dist[u]) continue;
            int ui = u % W, uj = u / W;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ui + di, j = uj + dj;
                    if (i < 2 || j < 2 || i >= W - 2 || j >= H - 2) continue;
                    int v = j * W + i;
                    // (open cells are cheap; climbing is dearer than going along; rock costs to dig)
                    float cost = (cave.CellOpen(i, j) ? 1f : 12f) + (dj < 0 ? 0.6f : 0f) + (di != 0 && dj != 0 ? 0.4f : 0f);
                    if (du + cost < dist[v]) { dist[v] = du + cost; prev[v] = u; pq.Enqueue(v, dist[v]); }
                }
        }
        // where to build: the nearest standing place the coarse check can reach and a body can't (mending
        // the first break in the way, the rest of the way on beyond it), leaning toward the guardian
        int goal = -1; float best = float.MaxValue;
        for (int u = 0; u < n; u++)
        {
            int ui = u % W, uj = u / W;
            if (dist[u] >= float.MaxValue || dist[u] < 5f || tried.Contains(u) || !cave.ReachMask[u] || !cave.CellOpen(ui, uj) || cave.CellOpen(ui, uj + 1) || fine.CellReached(ui, uj)) continue;
            float score = dist[u] + 0.35f * (Math.Abs(ui - bi) + Math.Abs(uj - bj));
            if (score < best) { best = score; goal = u; }
        }
        if (goal < 0 && dist[bj * W + bi] < float.MaxValue) goal = bj * W + bi;
        if (goal < 0) return false;
        tried.Add(goal);
        for (int dj = -2; dj <= 2; dj++) for (int di = -2; di <= 2; di++) tried.Add(goal + dj * W + di);
        var path = new List<int>();
        for (int c = goal; c >= 0; c = prev[c]) path.Add(c);
        path.Reverse();
        if (System.Environment.GetEnvironmentVariable("FR_DEBUG") != null) fine.SavePng($"/tmp/claude-0/fine/path_{cave.Seed}_{cave.FineRepairs}.png", cave.StartPos, cave.Boss.Floor, path);
        if (RawMode)
        {
            // (a diagnosis: what the first break looks like, in cells)
            int g = path[^1], s0 = path[0];
            int open = 0, rock = 0; foreach (int c in path) if (cave.CellOpen(c % W, c / W)) open++; else rock++;
            GD.Print($"      BREAK {cave.Biome?.Id} seed {cave.Seed}: from {s0 % W},{s0 / W} to {g % W},{g / W}: across {Math.Abs(g % W - s0 % W)} up {s0 / W - g / W} (path {path.Count}, rock {rock})");
            return false;
        }
        int cut = 0;
        // cut rock (and any neck a body doesn't fit through) along the way
        foreach (int c in path)
        {
            int ci = c % W, cj = c / W;
            var centre = new Vector2(ci + 0.5f, cj + 0.5f) * CaveData.Cell;
            if (!cave.CellOpen(ci, cj) || !fine.FitsAt(centre)) { CarveDisc(cave, ci + 0.5f, cj + 0.5f, 2.3f); cut++; }
        }
        int steps = FineStair(cave, path);
        if (Verbose) GD.Print($"      repair: goal cell {goal % W},{goal / W} cost {dist[goal]:0.0} path {path.Count} cells from {path[0] % W},{path[0] / W}: cut {cut}, ledges {steps}");
        // (nothing to build and nothing to cut: this route can't be mended)
        return steps > 0 || cut > 0;
    }

    /// <summary>
    /// Stepping stones along a route (from its low end): one wherever the way leaves the ground for
    /// open air and has climbed three cells or crossed four since the last footing, never across the
    /// whole of a narrow passage (rock ledges; ice ones in frozen caverns).
    /// </summary>
    private static int FineStair(CaveData cave, List<int> path)
    {
        bool Standing(int i, int j) => cave.CellOpen(i, j) && !cave.CellOpen(i, j + 1);
        int lastI = path[0] % W, lastJ = path[0] / W, placed = 0, side = 1;
        foreach (int c in path)
        {
            int ci = c % W, cj = c / W;
            if (Standing(ci, cj)) { lastI = ci; lastJ = cj; continue; }
            int rise = lastJ - cj, dx = Math.Abs(ci - lastI);
            if (!(rise >= 3 || dx >= 4 || (rise >= 2 && dx >= 3))) continue;
            // (not in the start chamber, or the guardian's: they are laid out as they are)
            var here = new Vector2(ci + 0.5f, cj + 1f) * CaveData.Cell;
            bool keep = false;
            foreach (var room in cave.Rooms)
                if (room.Kind is RoomKind.Start or RoomKind.Boss && Math.Abs(here.X - room.Center.X) < room.RxPx + 48 && Math.Abs(here.Y - room.Center.Y) < room.RyPx + 64) keep = true;
            if (keep) { lastI = ci; lastJ = cj; continue; }
            int row = cj + 2, l = ci, r = ci;
            void Measure()
            {
                l = ci; r = ci;
                while (l > 0 && cave.CellOpen(l - 1, row) && ci - l < 14) l--;
                while (r < W - 1 && cave.CellOpen(r + 1, row) && r - ci < 14) r++;
            }
            Measure();
            // a passage too narrow for a stone and a way past it is opened out first
            if (r - l + 1 < 6) { CarveDisc(cave, ci + 0.5f, cj + 1f, 3.6f); Measure(); }
            int run = r - l + 1;
            float cx, half;
            if (run >= 10) { half = 2.2f; cx = ci + 0.5f; }
            else if (run >= 6)
            {
                // a shelf on one wall, a three-cell gap left to pass it by
                half = (run - 3) * 0.5f + 0.8f;
                cx = side > 0 ? l - 0.8f + half : r + 1.8f - half;
                side = -side;
            }
            else { lastI = ci; lastJ = cj; continue; }
            if (B.IcePlatforms) cave.IceLedges.Add(new Vector3(cx, cj + 1.0f, half));
            else StampLedge(cave, cx, cj + 1.8f, half);
            placed++;
            lastI = ci; lastJ = cj;
        }
        return placed;
    }

    /// <summary>Opens a round hole (radius in cells) in the rock.</summary>
    private static void CarveDisc(CaveData cave, float cx, float cy, float r)
    {
        int stride = W + 1;
        for (int j = (int)(cy - r - 1); j <= (int)(cy + r + 1); j++)
            for (int i = (int)(cx - r - 1); i <= (int)(cx + r + 1); i++)
            {
                if (i < 1 || j < 1 || i >= W || j >= H) continue;
                float d = MathF.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                float v = Math.Clamp(0.5f + (r - d) * 0.7f, 0f, 1f);
                int k = j * stride + i;
                if (v > cave.Open[k]) cave.Open[k] = v;
            }
    }

    /// <summary>
    /// Finds the trapped floor cell closest (through open space) to the safe region and builds a
    /// staircase of small rock ledges along that route, 3 cells of height apart.
    /// </summary>
    internal static bool RepairTraps(CaveData cave, HashSet<int> tried)
    {
        int n = W * H;
        var oc = cave.OpenCells;
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
                    if (prev[v] != -2 || !oc[v]) continue;
                    prev[v] = u; q.Enqueue(v);
                    bool standing = j + 1 >= H || !oc[v + W];
                    if (cave.TrapMask[v] && standing && j + 0.5f < wrow && !tried.Contains(v)) { target = v; break; }
                }
        }
        if (target < 0) return false;
        tried.Add(target);
        var path = new List<int>();
        for (int c = target; c >= 0; c = prev[c]) path.Add(c);
        Stair(cave, path, true);
        return true;
    }

    /// <summary>Whether you can stand just above the guardian chamber's floor (see Score).</summary>
    internal static bool BossFloorReached(CaveData c)
    {
        if (c.Boss == null || c.ReachMask == null) return false;
        int bi = (int)(c.Boss.Floor.X / CaveData.Cell), bj = (int)(c.Boss.Floor.Y / CaveData.Cell) - 2;
        for (int dj = -3; dj <= 3; dj++)
            for (int di = -4; di <= 4; di++)
            {
                int k = (bj + dj) * W + bi + di;
                if (k >= 0 && k < W * H && c.ReachMask[k]) return true;
            }
        return false;
    }

    /// <summary>
    /// The other half of RepairTraps: when the guardian's chamber can't be reached at all (a climb
    /// somewhere on the way is too high, so the way there is one-way down), builds a staircase of
    /// ledges up along the shortest open route from where you can get to its floor.
    /// </summary>
    internal static bool RepairReach(CaveData cave)
    {
        if (cave.Boss == null || cave.ReachMask == null || BossFloorReached(cave)) return false;
        int n = W * H;
        var oc = cave.OpenCells;
        var prev = new int[n];
        Array.Fill(prev, -2);
        var q = new Queue<int>();
        // (from the floors you can stand on: the stair's first step must be one jump up from them)
        for (int u = 0; u < n; u++)
            if (cave.ReachMask[u] && (u + W >= n || !oc[u + W])) { prev[u] = -1; q.Enqueue(u); }
        int bi = (int)(cave.Boss.Floor.X / CaveData.Cell), bj = (int)(cave.Boss.Floor.Y / CaveData.Cell) - 2;
        int goal = -1;
        while (q.Count > 0 && goal < 0)
        {
            int u = q.Dequeue(); int ui = u % W, uj = u / W;
            for (int dj = -1; dj <= 1 && goal < 0; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int i = ui + di, j = uj + dj;
                    if (i < 0 || j < 0 || i >= W || j >= H) continue;
                    int v = j * W + i;
                    if (prev[v] != -2 || !oc[v]) continue;
                    prev[v] = u; q.Enqueue(v);
                    if (Math.Abs(j - bj) <= 3 && Math.Abs(i - bi) <= 4) { goal = v; break; }
                }
        }
        if (goal < 0) return false;
        // (from the goal back to where you can already get: the climb runs the other way)
        var path = new List<int>();
        for (int c = goal; c >= 0; c = prev[c]) path.Add(c);
        path.Reverse();
        Stair(cave, path, false);
        return true;
    }

    /// <summary>
    /// Small rock ledges along a route (listed from its low end), one every 3 cells of climb,
    /// alternating sides. `spareBoss` keeps them out of the guardian's chamber.
    /// </summary>
    private static void Stair(CaveData cave, List<int> path, bool spareBoss)
    {
        int stride = W + 1;
        int lastY = path[0] / W, side = 1;
        for (int p = 1; p < path.Count; p++)
        {
            int c = path[p];
            int ci = c % W, cj = c / W;
            if (lastY - cj < 3) continue;
            lastY = cj;
            if (spareBoss && cave.Boss != null && new Vector2(ci, cj).DistanceTo(cave.Boss.Center / CaveData.Cell) < cave.Boss.RxPx / CaveData.Cell + 2) continue;
            float cx = ci + 0.5f + side * 1.2f, cy = cj + 1.8f;
            side = -side;
            // (frozen caverns: the steps are ice, as everywhere else in them)
            if (B.IcePlatforms) { cave.IceLedges.Add(new Vector3(cx, cj + 1.0f, 2.3f)); continue; }
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
    }

    internal static void BuildSpawns(CaveData cave, List<Stamp> stamps, Vector2 startCells, Random rng, int stride = 8)
    {
        float cell = CaveData.Cell;
        for (int k = 0; k < stamps.Count; k += stride)
        {
            var s = stamps[k];
            var pc = new Vector2(s.X, s.Y);
            if (pc.DistanceTo(startCells) < 28) continue;
            var p = pc * cell;
            if (cave.IsSolid(p)) continue;
            bool spaced = true;
            foreach (var sp in cave.Spawns) if (sp.Pos.DistanceTo(p) < (B?.SpawnSpacing ?? 11f) * cell) { spaced = false; break; }
            if (!spaced) continue;
            bool inBossRoom = cave.Boss != null && p.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 3 * cell;
            if (inBossRoom) continue;

            if (p.Y > cave.WaterY + 1.5f * cell)
            {
                if (cave.Liquid != Liquid.Water) continue; // nothing lives in lava
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

    /// <summary>
    /// Shores: a slab of ground that runs on down into the water at an angle a creature can walk,
    /// found by walking the floor away from a point just above the waterline (the crab's home).
    /// </summary>
    internal static void BuildShores(CaveData cave, List<Stamp> stamps)
    {
        if (cave.Liquid != Liquid.Water) return;
        float cell = CaveData.Cell, step = 12f;
        float maxDy = step * MathF.Tan(Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees - 4f));
        for (int k = 0; k < stamps.Count; k++)
        {
            var s = stamps[k];
            var p = new Vector2(s.X, s.Y) * cell;
            if (MathF.Abs(p.Y - cave.WaterY) > 8 * cell || cave.IsSolid(p)) continue;
            if (!cave.FindFloor(p + new Vector2(0, -cell), 9 * cell, out var f0)) continue;
            // the slab starts at the waterline or a little above it
            if (f0.Y > cave.WaterY - 2f || f0.Y < cave.WaterY - 5 * cell) continue;
            bool near = false;
            foreach (var sp in cave.Spawns) if (sp.Kind == SpawnKind.Shore && sp.Pos.DistanceTo(f0) < 6 * cell) { near = true; break; }
            if (near) continue;
            foreach (int dir in new[] { 1, -1 })
            {
                var prev = f0; bool ok = true; float deepest = f0.Y;
                for (int i = 1; i <= 12 && ok; i++)
                {
                    var from = new Vector2(f0.X + dir * i * step, prev.Y - 18);
                    if (cave.IsSolid(from) || !cave.FindFloor(from, 60, out var f)) { ok = false; break; }
                    if (MathF.Abs(f.Y - prev.Y) > maxDy || cave.IsSolid(f + new Vector2(0, -10))) { ok = false; break; }
                    deepest = MathF.Max(deepest, f.Y);
                    prev = f;
                }
                // (it must carry on well under the surface, not just dip in)
                if (ok && deepest > cave.WaterY + 3 * cell)
                {
                    cave.Spawns.Add(new SpawnPoint { Pos = f0 + new Vector2(0, -8), Kind = SpawnKind.Shore, Normal = new Vector2(dir, 0) });
                    break;
                }
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

    internal static float[] ComputeRockDepth(CaveData cave)
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

public static partial class CaveGenerator
{
    /// <summary>
    /// Now and then (<see cref="Tune.Abyss.DrainChance"/>) the lowest point of a level with water is a way out through the bottom
    /// of the map: the lowest swimmable cell gets a shaft sunk below it, and a drain at its foot. It comes from the seed alone
    /// (online, every game makes the same one).
    /// </summary>
    internal static void AddDrain(CaveData cave, int seed)
    {
        cave.Drain = null;
        if (cave.Biome == null || cave.Liquid != Liquid.Water || cave.Biome.Id is BiomeId.Entrance or BiomeId.Lair or BiomeId.Abyss || cave.Biome.MinDepth < 1) return;
        if (!(new Random(seed * 31 + 17).NextDouble() < Tune.Abyss.DrainChance) && !ForceDrain) return;
        if (cave.ReachMask == null) return;
        int W = cave.W, H = cave.H;
        int waterRow = (int)(cave.WaterY / CaveData.Cell);
        // the lowest cell you can swim to, well in from the edges
        int bi = -1, bj = -1;
        for (int j = H - 12; j > waterRow + 8 && bi < 0; j--)
            for (int i = 14; i < W - 14; i++)
                if (cave.ReachMask[j * W + i] && cave.CellOpen(i, j) && cave.CellOpen(i, j - 1) && cave.CellOpen(i, j - 2) && cave.CellOpen(i - 1, j - 1) && cave.CellOpen(i + 1, j - 1)) { bi = i; bj = j; break; }
        if (bi < 0) return;
        // the drain is at the lowest point of the water; below it a shaft runs on down to the bottom of the map (the view blacks it out)
        int end = H - 6;
        void Carve(float cx, float cy, float r)
        {
            int stride = W + 1;
            for (int j = Math.Max(0, (int)(cy - r - 2)); j <= Math.Min(H, (int)(cy + r + 2)); j++)
                for (int i = Math.Max(0, (int)(cx - r - 2)); i <= Math.Min(W, (int)(cx + r + 2)); i++)
                {
                    float d = MathF.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                    int k = j * stride + i;
                    float v = Math.Clamp(0.5f + (r - d) * 0.5f, 0f, 1f);
                    if (v > cave.Open[k]) cave.Open[k] = v;
                }
        }
        for (float y = bj - 1; y <= end; y += 0.8f) Carve(bi + 0.5f, y, 2.6f);
        cave.Drain = new Vector2(bi + 0.5f, bj + 0.5f) * CaveData.Cell;
    }

    /// <summary>Test aid (<c>--gentest --forcedrain</c>): every level with water gets its drain.</summary>
    public static bool ForceDrain;
}
