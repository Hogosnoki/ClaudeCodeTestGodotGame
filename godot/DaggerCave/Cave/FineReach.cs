using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The strict reachability check: where a real hero (a 13 x 26 px capsule, jumping the way the
/// slowest hero jumps, with no double jump, wall jump or updraft) can get to in a generated cave.
/// The generator's own check (see CaveGenerator.ValidateTraversal) works over 16 px cells, which
/// is kind to narrow necks and tall climbs; this one walks, jumps, falls and swims pixel-true
/// positions on a 6 px grid, so "the boss can be reached" means a player can really do it.
/// </summary>
public sealed class FineReach
{
    public const int Res = 6;
    // the hero's body and the slowest hero's jump (see Tune.Hero and the per-hero JumpMult / MoveMult)
    private static readonly float HalfW = Env("FR_HALFW", 6f), HalfH = Env("FR_HALFH", 12f);
    private static readonly float JumpV = Tune.Hero.JumpVelocity * MathF.Sqrt(Tune.Hero.Floatiness) * Env("FR_JUMP", 0.95f), Grav = Tune.Hero.Gravity * Tune.Hero.Floatiness, FallMult = Tune.Hero.FallGravityMult, RunV = Tune.Hero.RunSpeed * 0.9f * Env("FR_RUN", 1f), SwimV = Tune.Hero.SwimSpeed * 0.83f;
    private static float Env(string k, float d) => float.TryParse(System.Environment.GetEnvironmentVariable(k), out var v) ? v : d;

    private readonly CaveData _c;
    private readonly int _w, _h;
    private readonly bool[] _fit;
    private readonly bool[] _water;
    // frozen caverns' breakable ice ledges: one-way slabs, stood on where the body's feet meet the top
    private readonly bool[] _plat;
    public readonly bool[] Reached;
    public int ReachedCount;

    // the body's outline in grid steps (a little over the real 13 x 26 px, to be safe): sides, ends and the rounded corners
    private static readonly (int dx, int dy)[] Outline =
    {
        (0, 0), (-1, 0), (1, 0), (0, -2), (0, 2), (-1, -1), (1, -1), (-1, 1), (1, 1),
    };

    public FineReach(CaveData c)
    {
        _c = c;
        _w = (int)(c.W * CaveData.Cell / Res);
        _h = (int)(c.H * CaveData.Cell / Res);
        _fit = new bool[_w * _h];
        _water = new bool[_w * _h];
        _plat = new bool[_w * _h];
        foreach (var l in c.IceLedges)
        {
            int py = (int)MathF.Round((l.Y * CaveData.Cell - HalfH) / Res);
            int x0 = (int)MathF.Floor((l.X - l.Z) * CaveData.Cell / Res), x1 = (int)MathF.Ceiling((l.X + l.Z) * CaveData.Cell / Res);
            for (int x = x0; x <= x1; x++) if (x >= 0 && x < _w && py >= 0 && py < _h) _plat[py * _w + x] = true;
        }
        Reached = new bool[_w * _h];
        var solid = new bool[_w * _h];
        for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
                solid[y * _w + x] = c.IsSolid(new Vector2(x * Res, y * Res));
        for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                bool ok = true;
                foreach (var (dx, dy) in Outline)
                {
                    int ox = x + dx, oy = y + dy;
                    if (ox < 0 || oy < 0 || ox >= _w || oy >= _h || solid[oy * _w + ox]) { ok = false; break; }
                }
                _fit[y * _w + x] = ok;
                _water[y * _w + x] = ok && c.Liquid == Liquid.Water && y * Res > c.WaterY;
            }
    }

    /// <summary>Whether a body fits at this world point (to the nearest 6 px).</summary>
    public bool FitsAt(Vector2 p) => Fit((int)MathF.Round(p.X / Res), (int)MathF.Round(p.Y / Res));

    /// <summary>Whether this cell (16 px) holds a place a body reached.</summary>
    public bool CellReached(int i, int j)
    {
        int x0 = (int)MathF.Floor(i * CaveData.Cell / Res), x1 = (int)MathF.Ceiling((i + 1) * CaveData.Cell / Res) - 1;
        int y0 = (int)MathF.Floor(j * CaveData.Cell / Res), y1 = (int)MathF.Ceiling((j + 1) * CaveData.Cell / Res) - 1;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x >= 0 && y >= 0 && x < _w && y < _h && Reached[y * _w + x]) return true;
        return false;
    }

    private bool Fit(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _fit[y * _w + x];
    private bool Wet(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _water[y * _w + x];
    private bool Plat(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _plat[y * _w + x];
    private bool Support(int x, int y) => Fit(x, y) && (!Fit(x, y + 1) || Plat(x, y)) && !Wet(x, y);

    private bool Lava(int x, int y) => _c.Liquid == Liquid.Lava && y * Res > _c.WaterY;

    /// <summary>Settles a body hanging at (x, y) onto the floor under it within a few steps (or -1).</summary>
    private int Settle(int x, int y, int maxDrop)
    {
        for (int k = 0; k <= maxDrop; k++)
        {
            if (!Fit(x, y + k)) return -1;
            if (Support(x, y + k) || Wet(x, y + k)) return y + k;
        }
        return -1;
    }

    private readonly Queue<int> _q = new();

    private void Mark(int x, int y)
    {
        if (!Fit(x, y)) return;
        if (Lava(x, y)) return;
        int k = y * _w + x;
        if (Reached[k]) return;
        Reached[k] = true; ReachedCount++;
        _q.Enqueue(k);
    }

    /// <summary>Flies a hop: starts at (x, y) with the given launch speed, runs sideways at vxRise until the apex and vxFall after.</summary>
    private void Fly(int sx, int sy, float vy0, float vxRise, float vxFall, float hold)
    {
        float px = sx * Res, py = sy * Res, vy = vy0, t = 0;
        const float dt = 1f / 60f;
        for (int step = 0; step < 240; step++)
        {
            bool rising = vy < 0;
            // (letting go of the jump early: it's cut once the hold is over)
            if (rising && t > hold) vy *= 0.5f;
            vy += Grav * (rising ? 1f : FallMult) * dt;
            if (vy > Tune.Hero.MaxFallSpeed) vy = Tune.Hero.MaxFallSpeed;
            float vx = vy < 0 ? vxRise : vxFall;
            int nx = (int)MathF.Round((px + vx * dt) / Res), ny = (int)MathF.Round((py + vy * dt) / Res);
            int cx = (int)MathF.Round(px / Res), cy = (int)MathF.Round(py / Res);
            // sideways first, then up or down (sliding along what stops it)
            if (nx != cx && !Fit(nx, cy)) { vx = 0; nx = cx; }
            else px += vx * dt;
            if (ny > cy && vy > 0)
            {
                // a one-way slab under the feet: stop on it
                bool landed = false;
                for (int yy = cy + 1; yy <= ny && !landed; yy++) if (Plat(nx, yy) && Fit(nx, yy)) { Mark(nx, yy); landed = true; }
                if (landed) return;
            }
            if (ny != cy)
            {
                if (!Fit(nx, ny))
                {
                    if (vy < 0) { vy = 0; ny = cy; }
                    else { Land(nx, cy); return; }
                }
                else py += vy * dt;
            }
            else py += vy * dt;
            int ix = (int)MathF.Round(px / Res), iy = (int)MathF.Round(py / Res);
            if (!Fit(ix, iy)) return;
            if (Wet(ix, iy)) { Mark(ix, iy); return; }
            if (Lava(ix, iy)) return;
            t += dt;
        }
    }

    private void Land(int x, int y)
    {
        int s = Settle(x, y, 3);
        if (s >= 0) Mark(x, s);
    }

    private static readonly float[] Drifts = { -1f, 0f, 1f };
    private static readonly float[] RiseDrifts = { -1f, 0f, 1f };

    private void Launch(int x, int y, bool fromWater)
    {
        float v = fromWater ? JumpV * 0.8f : JumpV;
        foreach (float hold in new[] { 0.45f, 0.18f })
            foreach (float a in RiseDrifts)
                foreach (float b in Drifts)
                    Fly(x, y, -v, a * RunV, b * RunV, hold);
    }

    private void Drop(int x, int y)
    {
        foreach (float b in Drifts) Fly(x, y, 0, b * RunV, b * RunV, 0);
    }

    /// <summary>Floods from the start: true once the point is reached (or within a few cells of it).</summary>
    public bool Run(Vector2 start, Vector2 goal)
    {
        int sx = (int)(start.X / Res), sy = (int)(start.Y / Res);
        // the start: the nearest body-sized place about it
        bool found = false;
        for (int r = 0; r < 14 && !found; r++)
            for (int dy = -r; dy <= r && !found; dy++)
                for (int dx = -r; dx <= r && !found; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int s = Settle(sx + dx, sy + dy, 12);
                    if (s >= 0) { Mark(sx + dx, s); found = true; }
                }
        if (!found) return false;
        int gx = (int)(goal.X / Res), gy = (int)(goal.Y / Res);
        int pulse = 0;
        while (_q.Count > 0)
        {
            if ((++pulse & 4095) == 0) CaveGenerator.CheckCancel();
            int k = _q.Dequeue();
            int x = k % _w, y = k / _w;
            if (Math.Abs(x - gx) * Res <= 40 && Math.Abs(y - gy) * Res <= 48) return true;
            if (Wet(x, y))
            {
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (!Fit(nx, ny)) continue;
                        if (Wet(nx, ny)) Mark(nx, ny);
                        else
                        {
                            // out of the water: onto a beach, or a hop out from the surface
                            int s = Settle(nx, ny, 4);
                            if (s >= 0 && !Wet(nx, s)) Mark(nx, s);
                            if (dy < 0 && (x & 3) == 0) Launch(x, y, true);
                        }
                    }
                continue;
            }
            // on the ground: walk (and climb steps and slopes), jump, or step off an edge
            foreach (int dir in new[] { -1, 1 })
            {
                bool moved = false;
                for (int up = 2; up >= -1 && !moved; up--)
                {
                    int nx = x + dir, ny = y - up;
                    if (!Fit(nx, ny)) continue;
                    if (up >= 0 && Fit(x, y - up) == false) continue;
                    int s = Settle(nx, ny, 2);
                    if (s >= 0) { Mark(nx, s); moved = true; }
                }
                if (!moved && Fit(x + dir, y))
                {
                    // nothing under the next step: walk off the edge
                    Drop(x + dir, y);
                }
            }
            // (a hop from every fourth step along a floor is nearly as good as one from every step)
            if ((x & 3) == 0) Launch(x, y, false);
        }
        return false;
    }

    /// <summary>A picture of the flood: grey = no room for a body, dark blue = room but never reached, green = reached, red dot = the start, yellow = the guardian.</summary>
    public void SavePng(string path, Vector2 start, Vector2 goal, List<int> route = null)
    {
        var img = Image.CreateEmpty(_w, _h, false, Image.Format.Rgb8);
        for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int k = y * _w + x;
                bool coarse = _c.ReachMask != null && Fit(x, y) && !Reached[k] && Support(x, y) && _c.ReachMask[Math.Min(_c.H - 1, (int)(y * Res / CaveData.Cell)) * _c.W + Math.Min(_c.W - 1, (int)(x * Res / CaveData.Cell))];
                var col = coarse ? new Color(1f, 0.2f, 0.2f) : _c.IsSolid(new Vector2(x * Res, y * Res)) ? new Color(0.12f, 0.1f, 0.1f) : !_fit[k] ? new Color(0.45f, 0.45f, 0.5f) : Reached[k] ? (_water[k] ? new Color(0.3f, 0.9f, 0.9f) : new Color(0.3f, 0.9f, 0.3f)) : _water[k] ? new Color(0.05f, 0.1f, 0.4f) : new Color(0.15f, 0.2f, 0.7f);
                img.SetPixel(x, y, col);
            }
        void Dot(Vector2 p, Color c) { for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) { int x = (int)(p.X / Res) + dx, y = (int)(p.Y / Res) + dy; if (x >= 0 && y >= 0 && x < _w && y < _h) img.SetPixel(x, y, c); } }
        if (route != null) foreach (int c in route) { int ci = c % _c.W, cj = c / _c.W; for (int dy = 0; dy < 3; dy++) for (int dx = 0; dx < 3; dx++) { int x = (int)((ci + 0.5f) * CaveData.Cell / Res) - 1 + dx, y = (int)((cj + 0.5f) * CaveData.Cell / Res) - 1 + dy; if (x >= 0 && y >= 0 && x < _w && y < _h) img.SetPixel(x, y, new Color(1f, 0f, 1f)); } }
        Dot(start, Colors.Red); Dot(goal, Colors.Yellow);
        img.SavePng(path);
    }

    /// <summary>Debug: prints a patch of the grid ('#' no room, '.' room, 'S' standing place, 'R' reached).</summary>
    public void Probe(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = x0; x <= x1; x++)
                sb.Append(!Fit(x, y) ? (_c.IsSolid(new Vector2(x * Res, y * Res)) ? '=' : '#') : Reached[y * _w + x] ? 'R' : Support(x, y) ? 'S' : '.');
            GD.Print($"{y,4} {sb}");
        }
    }
}
