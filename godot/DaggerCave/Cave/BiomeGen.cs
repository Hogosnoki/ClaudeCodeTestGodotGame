using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProcGen.Engine.Noise;

namespace DaggerCave;

/// <summary>
/// The non-walker cave styles: the entrance corridor, the room-and-passage den and nest, the
/// right-angled ruins, and the dragon's arena. Each carves an openness field with the helpers on
/// <see cref="Field"/> and then goes through the same finishing pass as the walker caves (specks,
/// ledges, the trap check with repairs, spawn points).
/// </summary>
public static partial class CaveGenerator
{
    /// <summary>An openness field being carved, plus the tunnel stamps (used for spawn points).</summary>
    private sealed class Field
    {
        public readonly float[] Open, Rough;
        public readonly int Stride;
        public readonly List<Stamp> Stamps = new(4096);

        public Field(int seed, float roughAmp = 1.9f)
        {
            Stride = W + 1;
            Open = new float[Stride * (H + 1)];
            Rough = new float[Stride * (H + 1)];
            for (int j = 0; j <= H; j++)
                for (int i = 0; i <= W; i++)
                    Rough[j * Stride + i] = (float)LatticeNoise3D.SampleFbm(i, j, 0.5, 11.3, 7.7, 0, seed, 3, 0.11, 0.55, 2.0) * roughAmp;
        }

        private void Max(int k, float v) { if (v > Open[k]) Open[k] = v; }
        private void Min(int k, float v) { if (v < Open[k]) Open[k] = v; }

        /// <summary>A rough disc of open space (like one walker step).</summary>
        public void Circle(float cx, float cy, float r, bool record = true, int kind = 0)
        {
            int i0 = Math.Max(0, (int)(cx - r - 3)), i1 = Math.Min(W, (int)(cx + r + 3));
            int j0 = Math.Max(0, (int)(cy - r - 3)), j1 = Math.Min(H, (int)(cy + r + 3));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * Stride + i;
                    float d = MathF.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                    Max(k, Math.Clamp(0.5f + (r + Rough[k] - d) * 0.5f, 0f, 1f));
                }
            if (record) Stamps.Add(new Stamp { X = cx, Y = cy, R = r, Main = true, Mode = ModeAir, Kind = kind });
        }

        /// <summary>A tunnel of radius r along a straight segment.</summary>
        public void Line(Vector2 a, Vector2 b, float r, int kind = 0)
        {
            float len = a.DistanceTo(b);
            for (float t = 0; t <= len; t += 0.9f) { var p = a.Lerp(b, len < 0.01f ? 0 : t / len); Circle(p.X, p.Y, r, true, kind); }
            Circle(b.X, b.Y, r, true, kind);
        }

        /// <summary>An elliptical chamber; with flatFloor, only the upper half above floorY.</summary>
        public void Dome(float cx, float floorY, float rx, float ry, bool flatFloor)
        {
            float cy = flatFloor ? floorY : floorY - ry;
            int i0 = Math.Max(0, (int)(cx - rx - 3)), i1 = Math.Min(W, (int)(cx + rx + 3));
            int j0 = Math.Max(0, (int)(cy - ry - 3)), j1 = Math.Min(H, (int)(cy + ry + 3));
            float rmin = Math.Min(rx, ry);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * Stride + i;
                    float ex = (i - cx) / rx, ey = (j - cy) / ry;
                    float e = MathF.Sqrt(ex * ex + ey * ey);
                    float v = 0.5f + ((1 - e) * rmin + Rough[k] * 0.45f) * 0.5f;
                    if (flatFloor) v = Math.Min(v, 0.5f + (floorY - j) * 0.5f);
                    Max(k, Math.Clamp(v, 0f, 1f));
                }
        }

        /// <summary>A crisp axis-aligned open box (walls exactly on the given cell lines).</summary>
        public void Rect(int x0, int y0, int x1, int y1)
        {
            for (int j = Math.Max(0, y0 - 1); j <= Math.Min(H, y1 + 1); j++)
                for (int i = Math.Max(0, x0 - 1); i <= Math.Min(W, x1 + 1); i++)
                {
                    float sd = Math.Min(Math.Min(i - x0, x1 - i), Math.Min(j - y0, y1 - j));
                    Max(j * Stride + i, Math.Clamp(0.5f + sd * 0.5f, 0f, 1f));
                }
        }

        /// <summary>A crisp axis-aligned block of rock (at least 2 cells in each direction to survive).</summary>
        public void Solid(int x0, int y0, int x1, int y1)
        {
            for (int j = Math.Max(0, y0 - 1); j <= Math.Min(H, y1 + 1); j++)
                for (int i = Math.Max(0, x0 - 1); i <= Math.Min(W, x1 + 1); i++)
                {
                    float sd = Math.Min(Math.Min(i - x0, x1 - i), Math.Min(j - y0, y1 - j));
                    Min(j * Stride + i, Math.Clamp(0.5f - sd * 0.5f, 0f, 1f));
                }
        }

        /// <summary>Makes everything from row floorY down (for `depth` rows) rock, between x0 and x1.</summary>
        public void FloorAt(float x0, float x1, float floorY, int depth = 4)
        {
            for (int j = (int)floorY; j <= (int)floorY + depth && j <= H; j++)
                for (int i = Math.Max(0, (int)x0); i <= Math.Min(W, (int)x1); i++)
                    Min(j * Stride + i, Math.Clamp(0.5f - (j - floorY) * 0.5f, 0f, 1f));
        }

        /// <summary>A walkable tunnel between two points: straight when gentle, zig-zagging when steep.</summary>
        public void Walkable(Vector2 from, Vector2 to, float r, int kind = 0)
        {
            float dx = Math.Abs(to.X - from.X), dy = Math.Abs(to.Y - from.Y);
            if (dy <= Mathf.Tan(MaxPitch * 0.85f) * dx) { Line(from, to, r, kind); return; }
            var pos = from;
            float hs = to.X >= from.X ? 1 : -1, leg = 0;
            for (int k = 0; k < 1400 && pos.DistanceTo(to) > 1.5f; k++)
            {
                float ddx = to.X - pos.X, ddy = to.Y - pos.Y;
                if (leg > 20 && Math.Sign(ddx) != hs && Math.Abs(ddx) > 1) { hs = -hs; leg = 0; }
                if ((pos.X < 9 && hs < 0) || (pos.X > W - 9 && hs > 0)) { hs = -hs; leg = 0; }
                float pitch = Mathf.Clamp(Mathf.Atan2(ddy, Math.Max(Math.Abs(ddx), 0.001f)), -MaxPitch * 0.85f, MaxPitch * 0.85f);
                pos += new Vector2(hs * Mathf.Cos(pitch), Mathf.Sin(pitch)) * 0.9f;
                leg += 0.9f;
                Circle(pos.X, pos.Y, r, true, kind);
            }
            Line(pos, to, r, kind);
        }
    }

    private static Room MakeRoom(RoomKind kind, float cx, float cy, float floorY, float rx, float ry) => new()
    {
        Kind = kind,
        Center = new Vector2(cx, cy) * CaveData.Cell,
        Floor = new Vector2(cx, floorY) * CaveData.Cell,
        RxPx = rx * CaveData.Cell, RyPx = ry * CaveData.Cell,
    };

    /// <summary>
    /// The shared end of every style: solid border, speck cleanup, extra structures, ledges, the
    /// traversal check with repairs, spawn points and rock depth.
    /// </summary>
    private static CaveData Finish(CaveData cave, Field f, Random rng, Vector2I startCell, List<Stamp> spawnStamps, int spawnStride,
        Action late = null, bool platforms = true)
    {
        for (int j = 0; j <= H; j++)
            for (int i = 0; i <= W; i++)
                if (i < 3 || j < 3 || i > W - 3 || j > H - 3) f.Open[j * f.Stride + i] = 0;
        cave.Open = f.Open;
        foreach (var st in f.Stamps) cave.DebugStamps.Add((new Vector2(st.X, st.Y), st.Kind));
        RemoveSpecks(cave);
        late?.Invoke();
        if (platforms) AddPlatforms(cave, rng);
        ValidateTraversal(cave, startCell);
        var tried = new HashSet<int>();
        for (int rep = 0; rep < 14 && cave.TrapCells > 6; rep++)
        {
            if (!RepairTraps(cave, tried)) break;
            ValidateTraversal(cave, startCell);
        }
        BuildSpawns(cave, spawnStamps, new Vector2(startCell.X, startCell.Y), rng, spawnStride);
        cave.RockDepth = ComputeRockDepth(cave);
        return cave;
    }

    /// <summary>Marks the rooms farthest from the start as mini-boss lairs (spread apart); the rest stay treasure rooms.</summary>
    private static void AssignMiniBosses(CaveData cave, Random rng, float minSpacingCells)
    {
        var cands = cave.Rooms.Where(r => r.Kind is RoomKind.Treasure).ToList();
        cands.Sort((a, b) => b.Center.DistanceTo(cave.StartPos).CompareTo(a.Center.DistanceTo(cave.StartPos)));
        int want = B.MiniBossesMin + rng.Next(B.MiniBossesMax - B.MiniBossesMin + 1);
        var chosen = new List<Room>();
        foreach (float spacing in new[] { minSpacingCells, minSpacingCells * 0.5f, 0f })
            foreach (var r in cands)
            {
                if (chosen.Count >= want) break;
                if (chosen.Contains(r)) continue;
                if (chosen.Any(c => c.Center.DistanceTo(r.Center) < spacing * CaveData.Cell)) continue;
                chosen.Add(r);
            }
        foreach (var r in chosen) r.Kind = RoomKind.MiniBoss;
    }

    // ================================================================== the entrance corridor

    /// <summary>
    /// One long, gently winding tunnel from the daylight at the left to the guardian's chamber at
    /// the right, widening into the odd cavern, with a few side pockets holding treasure.
    /// </summary>
    private static CaveData GenerateCorridor(int seed)
    {
        var rng = new Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var f = new Field(seed, 1.3f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };

        float r = 3.6f, tr = 3.6f;
        float startFloor = H * 0.55f + Rnd(-3, 3);
        const float sx = 13;
        f.Dome(sx, startFloor, 9, 6.5f, true);
        cave.Rooms.Add(MakeRoom(RoomKind.Start, sx, startFloor - 3, startFloor, 9, 6.5f));

        float x = sx + 4, y = startFloor - r, slope = 0;
        float xEnd = W - 34;
        float nextCavern = x + Rnd(30, 45);
        var pocketAt = new List<float>();
        for (int k = 0; k < 3; k++) pocketAt.Add(Mathf.Lerp(x + 25, xEnd - 20, (k + Rnd(0.2f, 0.8f)) / 3f));
        while (x < xEnd)
        {
            slope += Rnd(-0.035f, 0.035f);
            slope *= 0.97f;
            if (y + r > H * 0.74f) slope = Math.Min(slope, -0.12f);
            if (y - r < H * 0.26f) slope = Math.Max(slope, 0.12f);
            slope = Math.Clamp(slope, -0.3f, 0.3f);
            if (rng.NextDouble() < 0.02) tr = Rnd(3.3f, 4.1f);
            float floor0 = y + r;
            r += (tr - r) * 0.05f;
            x += 0.9f;
            y = floor0 + slope * 0.9f - r; // the floor follows the slope even while the radius changes
            f.Circle(x, y, r);

            if (x >= nextCavern && x < xEnd - 24)
            {
                // a wider cavern on the way
                float rx = Rnd(9, 13), ry = Math.Min(Rnd(6, 8.5f), y + r - 5);
                f.Dome(x + rx * 0.6f, y + r, rx, ry, true);
                nextCavern = x + rx * 2 + Rnd(30, 50);
            }
            if (pocketAt.Count > 0 && x >= pocketAt[0])
            {
                pocketAt.RemoveAt(0);
                // a side pocket: a short walkable climb (or drop) off the main tunnel to a little chamber
                float dir = y - r - 14 * 0.3f - 6 > 5 ? -1 : 1;
                var from = new Vector2(x, y);
                var to = from + new Vector2(14, dir * 14 * 0.3f);
                f.Line(from, to, 2.8f);
                float pfloor = to.Y + 2.8f;
                float prx = Rnd(5, 6.5f), pry = Rnd(4, 4.8f);
                f.Dome(to.X + prx * 0.7f, pfloor, prx, pry, true);
                cave.Rooms.Add(MakeRoom(RoomKind.Treasure, to.X + prx * 0.7f, pfloor - pry * 0.5f, pfloor, prx, pry));
            }
        }
        // the guardian's chamber at the end
        float bfloor = y + r;
        const float brx = 13, bry = 7.5f;
        float bcx = W - 20;
        f.Line(new Vector2(x, y), new Vector2(bcx - brx + 2, bfloor - r), r);
        f.Dome(bcx, bfloor, brx, Math.Min(bry, bfloor - 5), true);
        f.FloorAt(bcx - brx - 2, bcx + brx + 2, bfloor, 5);
        f.FloorAt(sx - 9, sx + 9, startFloor, 3);
        var boss = MakeRoom(RoomKind.Boss, bcx, bfloor - bry * 0.45f, bfloor, brx, bry);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        cave.StartPos = new Vector2(sx, startFloor - 1.2f) * CaveData.Cell;
        return Finish(cave, f, rng, new Vector2I((int)sx, (int)(startFloor - 2)), f.Stamps, 8);
    }

    // ================================================================== den and nest: rooms and passages

    private sealed class RoomPlan { public float X, Y, R, Floor; public int Index; }

    /// <summary>
    /// Round chambers with flat floors joined by walkable passages. The den strings a few large
    /// rooms along one level; the nest scatters smaller ones up and down, joined by zig-zagging
    /// tunnels so every climb stays walkable. Spawns, chests and lairs are in the rooms.
    /// </summary>
    private static CaveData GenerateRooms(int seed)
    {
        var rng = new Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var f = new Field(seed, 1.5f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };
        bool line = B.RoomYSpread < 0.2f;

        var rooms = new List<RoomPlan>();
        if (line)
        {
            // along one level, left to right
            float x = 5;
            for (int k = 0; k < B.RoomCount; k++)
            {
                float rr = Rnd(B.RoomRMin, B.RoomRMax);
                float cx = x + rr + (k == 0 ? 0 : Rnd(5, 11));
                if (cx + rr > W - 5) break;
                float cy = H * 0.47f + Rnd(-1, 1) * H * B.RoomYSpread;
                rooms.Add(new RoomPlan { X = cx, Y = cy, R = rr });
                x = cx + rr;
            }
        }
        else
        {
            for (int tries = 0; tries < 4000 && rooms.Count < B.RoomCount; tries++)
            {
                float rr = Rnd(B.RoomRMin, B.RoomRMax);
                float cx = Rnd(rr + 6, W - rr - 6);
                float cy = Rnd(rr + 6, H - rr * 0.55f - 8);
                if (rooms.Any(o => new Vector2(o.X - cx, o.Y - cy).Length() < o.R + rr + 8)) continue;
                rooms.Add(new RoomPlan { X = cx, Y = cy, R = rr });
            }
        }
        for (int k = 0; k < rooms.Count; k++) rooms[k].Index = k;

        // the start is the left-most room (the highest of the left ones in the nest);
        // the exit is the one farthest from it
        var start = line ? rooms[0] : rooms.OrderBy(o => o.X + o.Y * 0.6f).First();
        var exit = rooms.OrderByDescending(o => new Vector2(o.X - start.X, o.Y - start.Y).Length()).First();
        exit.R = Math.Max(exit.R, line ? B.RoomRMax : 11.5f);
        start.R = Math.Max(start.R, 8);
        foreach (var o in rooms) o.Floor = o.Y + o.R * 0.55f;

        // links: a minimum spanning tree over the rooms, plus a couple of loops
        var links = new List<(RoomPlan a, RoomPlan b)>();
        if (line) for (int k = 1; k < rooms.Count; k++) links.Add((rooms[k - 1], rooms[k]));
        else
        {
            var inTree = new HashSet<RoomPlan> { rooms[0] };
            float D(RoomPlan a, RoomPlan b) => new Vector2(a.X - b.X, (a.Y - b.Y) * 1.6f).Length(); // climbing costs more
            while (inTree.Count < rooms.Count)
            {
                (RoomPlan a, RoomPlan b) best = default; float bd = float.MaxValue;
                foreach (var a in inTree)
                    foreach (var b in rooms)
                        if (!inTree.Contains(b) && D(a, b) < bd) { bd = D(a, b); best = (a, b); }
                links.Add(best); inTree.Add(best.b);
            }
            int extra = 0;
            foreach (var a in rooms)
                foreach (var b in rooms)
                {
                    if (extra >= 2 || a.Index >= b.Index || links.Any(l => (l.a == a && l.b == b) || (l.a == b && l.b == a))) continue;
                    if (D(a, b) < 55 && rng.NextDouble() < 0.3) { links.Add((a, b)); extra++; }
                }
        }

        // carve: rooms, then passages, then flatten the floors
        foreach (var o in rooms)
        {
            f.Circle(o.X, o.Y, o.R, false);
            for (float dx = -o.R * 0.5f; dx <= o.R * 0.5f + 0.01f; dx += o.R * 0.5f)
                f.Stamps.Add(new Stamp { X = o.X + dx, Y = o.Floor - 2.5f, R = o.R * 0.5f, Main = true, Mode = ModeAir, Kind = 7 });
            if (!line) f.Stamps.Add(new Stamp { X = o.X, Y = o.Y - o.R * 0.4f, R = o.R * 0.5f, Main = true, Mode = ModeAir, Kind = 7 });
        }
        var roomStamps = f.Stamps.ToList();
        // flat floors first: the passages may then cut through them wherever they need to
        foreach (var o in rooms) f.FloorAt(o.X - o.R - 1, o.X + o.R + 1, o.Floor, 3);
        float cr = B.CorridorR;
        foreach (var (a, b) in links)
        {
            // leave each room level through a side: facing each other when they're side by side,
            // the same side when one is over the other (so the way down runs outside both)
            float sa, sb;
            if (Math.Abs(b.X - a.X) > (a.R + b.R) * 0.8f) { sa = b.X >= a.X ? 1 : -1; sb = -sa; }
            else { sa = sb = (a.X + b.X) * 0.5f < W * 0.5f ? 1 : -1; }
            var da = new Vector2(a.X + sa * (a.R + 3), a.Floor - cr);
            var db = new Vector2(b.X + sb * (b.R + 3), b.Floor - cr);
            f.Line(new Vector2(a.X + sa * a.R * 0.6f, a.Floor - cr), da, cr, 1);
            f.Line(new Vector2(b.X + sb * b.R * 0.6f, b.Floor - cr), db, cr, 1);
            f.Walkable(da, db, cr, 1);
        }

        foreach (var o in rooms)
        {
            var kind = o == start ? RoomKind.Start : o == exit ? RoomKind.Boss : RoomKind.Treasure;
            var room = MakeRoom(kind, o.X, o.Y, o.Floor, o.R, o.R * 0.78f);
            cave.Rooms.Add(room);
            if (kind == RoomKind.Boss) cave.Boss = room;
        }
        cave.StartPos = new Vector2(start.X, start.Floor - 1.2f) * CaveData.Cell;
        AssignMiniBosses(cave, rng, 45);

        // spawns: in the rooms; the passages get only the odd one (the nest keeps them for the rooms)
        var spawnStamps = new List<Stamp>(roomStamps);
        var passage = f.Stamps.Where(s => s.Kind == 1).ToList();
        int every = line ? 24 : 60;
        for (int k = every / 2; k < passage.Count; k += every) spawnStamps.Add(passage[k]);
        return Finish(cave, f, rng, new Vector2I((int)start.X, (int)(start.Floor - 2)), spawnStamps, 1);
    }

    // ================================================================== ruins

    private sealed class Chamber { public int X0, X1, Top, Floor; public int Level; public bool Tall; }

    /// <summary>
    /// Three floors of right-angled chambers and corridors, joined by shafts with alternating
    /// stone slabs to climb (or drop) between them. Some halls are tall, with a stair of floating
    /// slabs. The way in is top left; the guardian's hall is at the bottom right.
    /// </summary>
    private static CaveData GenerateRuins(int seed)
    {
        var rng = new Random(seed);
        int RndI(int a, int bIncl) => rng.Next(a, bIncl + 1);
        var f = new Field(seed, 0.6f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };
        int[] floors = { (int)(H * 0.3f), (int)(H * 0.58f), (int)(H * 0.86f) };
        var chambers = new List<Chamber>();
        Chamber hall = null;

        for (int l = 0; l < 3; l++)
        {
            int F = floors[l];
            int x = 5;
            bool last = l == 2;
            int limit = last ? W - 5 - 30 - 5 : W - 5;
            var level = new List<Chamber>();
            while (true)
            {
                int w = RndI(14, 24);
                if (x + w > limit) break;
                bool tall = l > 0 && rng.NextDouble() < 0.3;
                int h = tall ? RndI(15, 17) : RndI(7, 11);
                var c = new Chamber { X0 = x, X1 = x + w, Top = F - h, Floor = F, Level = l, Tall = tall };
                f.Rect(c.X0, c.Top, c.X1, c.Floor);
                level.Add(c);
                int gap = RndI(4, 12);
                if (x + w + gap + 14 > limit) { x += w; break; }
                f.Rect(x + w - 1, F - 5, x + w + gap + 1, F); // corridor
                x += w + gap;
            }
            if (last)
            {
                int hx0 = W - 5 - 30;
                f.Rect(x - 1, F - 5, hx0 + 1, F);
                hall = new Chamber { X0 = hx0, X1 = W - 5, Top = F - 13, Floor = F, Level = l };
                f.Rect(hall.X0, hall.Top, hall.X1, hall.Floor);
            }
            chambers.AddRange(level);
        }

        // shafts: two between each pair of floors, far apart
        var shafts = new List<(int x, int l)>();
        for (int l = 0; l < 2; l++)
        {
            var upper = chambers.Where(c => c.Level == l && c.X1 - c.X0 >= 12).ToList();
            int lowerEnd = l + 1 == 2 ? W - 6 : chambers.Where(c => c.Level == l + 1).Max(c => c.X1);
            int made = 0;
            foreach (var c in upper.OrderBy(_ => rng.Next()))
            {
                int sx = RndI(c.X0 + 2, c.X1 - 10);
                if (sx + 8 > lowerEnd - 1 || shafts.Any(s => s.l == l && Math.Abs(s.x - sx) < 40)) continue;
                shafts.Add((sx, l));
                f.Rect(sx, floors[l] - 2, sx + 8, floors[l + 1] - 2);
                if (++made >= 2) break;
            }
        }

        // rooms
        var first = chambers.Where(c => c.Level == 0).OrderBy(c => c.X0).First();
        float scx = (first.X0 + first.X1) * 0.5f;
        cave.Rooms.Add(MakeRoom(RoomKind.Start, scx, first.Floor - 3, first.Floor, (first.X1 - first.X0) * 0.5f, first.Floor - first.Top));
        foreach (var c in chambers)
        {
            if (c == first) continue;
            float cx = (c.X0 + c.X1) * 0.5f;
            cave.Rooms.Add(MakeRoom(RoomKind.Treasure, cx, (c.Top + c.Floor) * 0.5f, c.Floor, (c.X1 - c.X0) * 0.5f, (c.Floor - c.Top) * 0.5f));
        }
        float hcx = (hall.X0 + hall.X1) * 0.5f;
        var boss = MakeRoom(RoomKind.Boss, hcx, hall.Floor - 5.5f, hall.Floor, 15, 6.5f);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        cave.StartPos = new Vector2(scx, first.Floor - 1.2f) * CaveData.Cell;
        AssignMiniBosses(cave, rng, 40);

        // spawn points along each floor and in the halls
        var spawnStamps = new List<Stamp>();
        foreach (var c in chambers)
            for (float x = c.X0 + 3; x < c.X1 - 2; x += 8)
                spawnStamps.Add(new Stamp { X = x, Y = c.Floor - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });

        void Slabs()
        {
            // climbing slabs in the shafts, alternating walls, 3 rows apart, leaving a clear
            // 2-cell channel down the middle to drop through
            foreach (var (sx, l) in shafts)
            {
                bool left = rng.Next(2) == 0;
                for (int t = floors[l + 1] - 4; t > floors[l] + 1; t -= 3)
                {
                    if (left) f.Solid(sx - 1, t, sx + 3, t + 2); else f.Solid(sx + 5, t, sx + 9, t + 2);
                    left = !left;
                }
            }
            // a stair of floating slabs up the tall halls
            foreach (var c in chambers.Where(c => c.Tall))
            {
                int x = c.X0 + 3, dir = 1;
                for (int t = c.Floor - 4; t >= c.Top + 3; t -= 4)
                {
                    if (x + 6 > c.X1 - 3) { dir = -1; x = c.X1 - 3 - 6; }
                    else if (x < c.X0 + 3) { dir = 1; x = c.X0 + 3; }
                    f.Solid(x, t, x + 6, t + 2);
                    x += dir * 4;
                }
            }
        }
        cave.Open = f.Open;
        return Finish(cave, f, rng, new Vector2I((int)scx, first.Floor - 2), spawnStamps, 1, Slabs, platforms: false);
    }

    // ================================================================== the dragon's arena

    /// <summary>
    /// A small antechamber and a short passage into one great domed arena over a lake of lava:
    /// a stone floor broken by lava pits, with tiers of ledges above.
    /// </summary>
    private static CaveData GenerateArena(int seed)
    {
        var rng = new Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        var f = new Field(seed, 1.2f);
        float lavaRow = H * (1f - B.LiquidFraction);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = lavaRow * CaveData.Cell, Liquid = B.Liquid, Biome = B };
        float F = lavaRow - 1.5f;

        const float sx = 12;
        f.Dome(sx, F, 8, 6, true);
        cave.Rooms.Add(MakeRoom(RoomKind.Start, sx, F - 3, F, 8, 6));
        float cx = W * 0.6f, rx = W * 0.36f, ry = H * 0.44f;
        float cy = H * 0.46f;
        f.Line(new Vector2(sx + 4, F - 3.2f), new Vector2(cx - rx + 4, F - 3.2f), 3.2f);
        f.Dome(cx, cy + ry, rx, ry, false);
        // the stone floor, broken by lava pits
        var pits = new List<(float a, float b)>();
        foreach (float off in new[] { -0.6f, 0.3f, 0.72f })
        {
            // the middle of the arena stays solid ground
            float px = cx + off * rx + Rnd(-2, 2);
            pits.Add((px - Rnd(3, 4.5f), px + Rnd(3, 4.5f)));
        }
        float x0 = sx - 9, x1 = cx + rx + 2;
        float xs = x0;
        foreach (var (a, b) in pits.OrderBy(p => p.a)) { f.FloorAt(xs, a, F, (int)(H - F)); xs = b; }
        f.FloorAt(xs, x1, F, (int)(H - F));

        var boss = MakeRoom(RoomKind.Boss, cx, F - ry * 0.45f, F, rx - 4, ry);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        cave.StartPos = new Vector2(sx, F - 1.2f) * CaveData.Cell;

        void Tiers()
        {
            // ledge tiers over the arena floor, staggered so each is a jump from the one below
            float[] rows = { F - 4, F - 8, F - 12, F - 16 };
            for (int t = 0; t < rows.Length; t++)
            {
                int n = 3 - (t % 2);
                for (int k = 0; k < n; k++)
                {
                    float lx = cx + (k - (n - 1) / 2f) * rx * (t % 2 == 0 ? 0.62f : 0.9f) + Rnd(-2, 2);
                    if (cave.CellOpen((int)lx, (int)rows[t] - 3)) StampLedge(cave, lx, rows[t] + 0.8f, Rnd(2.6f, 3.4f));
                }
            }
        }
        var spawns = new List<Stamp>();
        return Finish(cave, f, rng, new Vector2I((int)sx, (int)(F - 2)), spawns, 1, Tiers, platforms: false);
    }
}
