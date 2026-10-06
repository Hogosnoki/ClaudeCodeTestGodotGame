using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public static partial class CaveGenerator
{
    // ================================================================== the catacombs

    /// <summary>
    /// A necropolis, nothing like the ruins' stacked boxes: a great nave in the middle of the map under a scalloped vault (a
    /// row of overlapping arches, sarcophagi along its floor), and out of each end a long stair down into a landing hall, a
    /// stair down into a vaulted gallery, and so on, outward and downward in a stepped pyramid. One cascade ends in the crypt
    /// lord's rotunda (round, domed, rock behind it for the treasury); the other in a sealed tomb. Every room starts where the
    /// stair before it lands and runs on the way the stair was heading, so a stair's foot always meets a floor flush (a room
    /// doubling back under its own stair would leave the foot a cliff).
    /// </summary>
    private static CaveData GenerateCrypt(int seed)
    {
        var rng = new Random(seed);
        float Rf(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        int RndI(int a, int bIncl) => rng.Next(a, bIncl + 1);
        var f = new Field(seed, 0.45f);
        var cave = new CaveData { W = W, H = H, Seed = seed, WaterY = (H + 200) * CaveData.Cell, Liquid = Liquid.None, Biome = B };
        float stairSlope = MathF.Tan(MaxPitch * 0.82f);
        var spawnStamps = new List<Stamp>();
        void Floor(float x0, float x1, float floorY)
        {
            for (float x = x0; x < x1; x += 5) spawnStamps.Add(new Stamp { X = x, Y = floorY - 2.5f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        }

        int fN = (int)(H * Rf(0.32f, 0.37f)), fBoss = (int)(H * Rf(0.8f, 0.85f));
        int fTomb = fN + (int)((fBoss - fN) * Rf(0.55f, 0.7f));
        int bossDir = rng.Next(2) == 0 ? -1 : 1;

        // ---- the nave: overlapping arches in the middle of the map, a level floor between
        float naveHalf = Rf(38, 48), mid = W * (bossDir > 0 ? 0.34f : 0.66f) + Rf(-5, 5);
        float naveL = mid - naveHalf, naveR = mid + naveHalf;
        var bays = new List<(float cx, float rx, float ry)>();
        float x = naveL;
        while (true)
        {
            float rx = Rf(12, 19), ry = Rf(11, 17);
            float cx = x + rx - 2;
            if (cx + rx > naveR + 4 && bays.Count >= 3) break;
            f.Dome(cx, fN, rx, ry, true);
            bays.Add((cx, rx, ry));
            x = cx + rx * 0.62f;
            if (bays.Count > 12) break;
        }
        naveR = Math.Min(x + 6, W - 60);
        f.Rect((int)naveL, fN - 8, (int)naveR, fN);
        Floor(naveL + 6, naveR - 6, fN);
        int startBay = 0;
        for (int k = 1; k < bays.Count; k++) if (Math.Abs(bays[k].cx - mid) < Math.Abs(bays[startBay].cx - mid)) startBay = k;
        for (int k = 0; k < bays.Count; k++)
        {
            var (cx, rx, ry) = bays[k];
            cave.Rooms.Add(k == startBay ? MakeRoom(RoomKind.Start, cx, fN - 3, fN, rx, ry * 0.9f) : MakeRoom(RoomKind.Treasure, cx, fN - ry * 0.45f, fN, rx, ry * 0.5f));
        }
        float startCx = bays[startBay].cx;
        // a stepped tomb: a ziggurat of stacked sarcophagi in the middle of a tall bay, up one side and down the other, a chest on top
        void Ziggurat(float cx, float floorY)
        {
            for (int i = 0; i < 3; i++)
            {
                float half = 6.5f + (2 - i) * 3.5f, h = 2.2f * (i + 1);
                f.Solid((int)(cx - half), (int)(floorY - h), (int)(cx + half), (int)floorY);
            }
            cave.Rooms.Add(MakeRoom(RoomKind.Treasure, cx, floorY - 8.5f, floorY - 6.6f, 5f, 3f));
            spawnStamps.Add(new Stamp { X = cx, Y = floorY - 9f, R = 3, Main = true, Mode = ModeAir, Kind = 7 });
        }
        float lastZig = -99;
        for (int k = 0; k < bays.Count; k++)
        {
            var (cx, rx, ry) = bays[k];
            if (k == startBay || rx < 15 || ry < 14 || Math.Abs(cx - startCx) < 22 || cx < naveL + 28 || cx > naveR - 28 || cx - lastZig < 34 || rng.NextDouble() < 0.25) continue;
            Ziggurat(cx, fN);
            lastZig = cx;
        }
        // sarcophagi along it: low slabs a hero hops onto, away from where you begin
        for (float tx = naveL + 14; tx < naveR - 22; tx += Rf(22, 36))
        {
            if (Math.Abs(tx - startCx) < 16 || bays.Any(b => Math.Abs(b.cx - tx - 4) < 16 && b.rx >= 15 && b.ry >= 14)) continue;
            int len = RndI(7, 10);
            f.Solid((int)tx, fN - 2, (int)tx + len, fN);
            if (rng.NextDouble() < 0.4) f.Solid((int)tx + 2, fN - 4, (int)tx + len - 2, fN - 2);
        }

        // a landing hall: one lofty round room, treasure in it (lo..hi, the floor level)
        void Hall(float lo, float hi, float floorY)
        {
            float len = hi - lo;
            f.Dome((lo + hi) * 0.5f, floorY, len * 0.5f + 1, Rf(10, 12), true);
            f.Rect((int)lo, (int)(floorY - 5), (int)hi, (int)floorY);
            cave.Rooms.Add(MakeRoom(RoomKind.Treasure, (lo + hi) * 0.5f, floorY - 5, floorY, len * 0.4f, 5f));
            Floor(lo + 3, hi - 3, floorY);
        }
        // a gallery: a chain of low arches along one floor (the floor kept level; some bays wider and loftier, the crypts' halls)
        void BuildGallery(float x0, float x1, float floorY, float height)
        {
            float gx = x0;
            int n = 0;
            while (gx < x1)
            {
                float rx = Rf(10, 15), ry = height + Rf(-1f, 1.5f);
                bool hall = n % 4 == 3 && x1 - gx > 40;
                if (hall) { rx = Rf(16, 20); ry = height + Rf(4f, 6f); }
                float cx = Math.Min(gx + rx - 2, x1 - 2);
                f.Dome(cx, floorY, rx, ry, true);
                if (hall && cx - rx > x0 + 20 && cx + rx < x1 - 20) cave.Rooms.Add(MakeRoom(RoomKind.Treasure, cx, floorY - ry * 0.5f, floorY, rx, ry * 0.6f));
                gx = cx + rx * 0.55f;
                n++;
            }
            f.Rect((int)x0, (int)(floorY - height * 0.6f), (int)x1, (int)floorY);
            Floor(x0 + 4, x1 - 4, floorY);
            // sarcophagi along it: low slabs to hop onto
            for (float tx = x0 + 18; tx < x1 - 24; tx += Rf(24, 40))
            {
                int slen = RndI(6, 9);
                f.Solid((int)tx, (int)(floorY - 2), (int)tx + slen, (int)floorY);
            }
        }
        // how many stairs (and rooms between them) fit in a width: each stair is as long as its drop wants, each room has a least length
        int PickN(float width, float totalDrop, int maxN)
        {
            for (int n = maxN; n > 1; n--)
            {
                float need = totalDrop / stairSlope + 6 * n;
                for (int k = 0; k < n; k++) need += k == n - 1 ? 18 : k % 2 == 0 ? 17 : 23;
                if (need + 8 <= width) return n;
            }
            return 1;
        }
        // one cascade: n stairs outward from the nave's end, a room after each (a hall, a gallery, a hall, ...), the last of them
        // the passage that ends at xe
        void Cascade(int dir, float xs, float finalFloor, int n, float xe)
        {
            var drop = new float[n];
            float dsum = 0, runSum = 0, wsum = 0;
            for (int k = 0; k < n; k++) { drop[k] = Rf(0.7f, 1.3f); dsum += drop[k]; }
            var weight = new float[n];
            for (int k = 0; k < n; k++)
            {
                drop[k] *= (finalFloor - fN) / dsum;
                runSum += drop[k] / stairSlope + 6;
                weight[k] = k == n - 1 ? 1.6f : k % 2 == 0 ? 1f : 2.2f;
                wsum += weight[k];
            }
            float avail = Math.Abs(xe - xs) - runSum + n + 6 * (n - 1);
            float cur = fN, xsA = xs;
            for (int k = 0; k < n; k++)
            {
                float next = k == n - 1 ? finalFloor : cur + drop[k];
                var a0 = new Vector2(xsA, cur - 2);
                var b0 = new Vector2(xsA + dir * ((next - cur) / stairSlope + 6), next - 2);
                f.Walkable(a0, b0, 3.2f, 8, null, dir);
                float xl = b0.X - dir, len = Math.Max(k % 2 == 0 ? 16 : 22, avail * weight[k] / wsum);
                if (k == n - 1) len = Math.Abs(xe - xl);
                float lo = dir > 0 ? xl : xl - len, hi = dir > 0 ? xl + len : xl;
                if (k % 2 == 0 && k != n - 1) Hall(lo, hi, next); else BuildGallery(lo, hi, next, Rf(9f, 12f));
                xsA = dir > 0 ? hi - 6 : lo + 6;
                cur = next;
            }
        }

        // ---- the cascade to the crypt lord's rotunda: a great round dome, rock behind it for the treasury
        float rotR = 18;
        float rotCx = bossDir > 0 ? W - 8 - rotR - 24 : 8 + rotR + 24;
        float xsBoss = bossDir > 0 ? naveR - 6 : naveL + 6, xeBoss = rotCx - bossDir * (rotR - 4);
        Cascade(bossDir, xsBoss, fBoss, PickN(Math.Abs(xeBoss - xsBoss), fBoss - fN, 4), xeBoss);
        f.Dome(rotCx, fBoss, rotR, 15, true);
        f.FloorAt(rotCx - rotR - 2, rotCx + rotR + 2, fBoss, 6);
        float door = rotCx - bossDir * rotR;
        f.Rect((int)Math.Min(door - 8, door + 6), fBoss - 6, (int)Math.Max(door - 8, door + 6), fBoss);
        var boss = MakeRoom(RoomKind.Boss, rotCx, fBoss - 6, fBoss, rotR - 1, 8f);
        cave.Boss = boss;
        cave.Rooms.Add(boss);
        Floor(rotCx - rotR + 4, rotCx + rotR - 4, fBoss);

        // ---- the other cascade: down to a sealed tomb, a lofty round room with its own treasure
        int tombDir = -bossDir;
        float tombR = Rf(11, 14);
        float tombCx = tombDir > 0 ? W - 8 - tombR - 6 : 8 + tombR + 6;
        float xsTomb = tombDir > 0 ? naveR - 6 : naveL + 6, xeTomb = tombCx - tombDir * (tombR - 3);
        Cascade(tombDir, xsTomb, fTomb, PickN(Math.Abs(xeTomb - xsTomb), fTomb - fN, 3), xeTomb);
        f.Dome(tombCx, fTomb, tombR, 14, true);
        f.FloorAt(tombCx - tombR - 1, tombCx + tombR + 1, fTomb, 4);
        cave.Rooms.Add(MakeRoom(RoomKind.Treasure, tombCx, fTomb - 5, fTomb, tombR * 0.8f, 6f));
        Floor(tombCx - tombR + 3, tombCx + tombR - 3, fTomb);

        cave.StartPos = new Vector2(startCx, fN - 1.2f) * CaveData.Cell;
        AssignMiniBosses(cave, rng, 40);
        return Finish(cave, f, rng, new Vector2I((int)startCx, fN - 2), spawnStamps, 1, null, platforms: false);
    }
}
