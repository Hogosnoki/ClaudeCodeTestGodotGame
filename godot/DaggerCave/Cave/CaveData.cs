using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public enum SpawnKind { Ground, Ceiling, Water, WaterFloor, WaterWall }

public sealed class SpawnPoint
{
    public Vector2 Pos;
    public SpawnKind Kind;
    /// <summary>Direction pointing away from the attached surface (into open space).</summary>
    public Vector2 Normal = Vector2.Up;
    public bool Used;
    public float Cooldown;
}

public enum RoomKind { Start, Boss, Treasure, MiniBoss, Ambush }

public sealed class Room
{
    /// <summary>Middle of the room's open space, world px.</summary>
    public Vector2 Center;
    /// <summary>A point on the room's floor (for placing chests / the portal), world px.</summary>
    public Vector2 Floor;
    public float RxPx, RyPx;
    public RoomKind Kind;
    public bool Underwater;
    public bool Triggered;
}

/// <summary>
/// The generated cave: an "openness" scalar field sampled at grid corners (0 = rock, 1 = open,
/// iso-surface at 0.5), plus gameplay metadata (water line, rooms, spawn points). Terrain meshes
/// and collision are extracted from the field with marching squares, which is what gives the
/// walls their angled, non-blocky look; gameplay queries (line of sight, "is this point inside
/// rock") sample the same field bilinearly.
/// </summary>
public sealed class CaveData
{
    public const float Cell = 16f;

    public int W, H;
    public int Seed;
    public float[] Open;       // (W+1)*(H+1) corner samples
    public float[] RockDepth;  // (W+1)*(H+1): distance (in cells) from the nearest open corner
    public float WaterY;       // world px; everything open below this is underwater (or lava)
    public Liquid Liquid = Liquid.Water;
    public BiomeDef Biome;
    /// <summary>Breakable ice ledges (frozen caverns): centre x, top y (cells), half width.</summary>
    public readonly List<Vector3> IceLedges = new();

    public Vector2 StartPos;
    public Room Boss;
    public readonly List<Room> Rooms = new();
    public readonly List<SpawnPoint> Spawns = new();

    // Diagnostics from generation.
    public int TrapCells;
    public int Attempts;
    public int ReachableCells;
    public bool[] TrapMask;
    public bool[] ReachMask;
    public readonly List<(Vector2 pos, int kind)> DebugStamps = new();

    public Vector2 SizePx => new(W * Cell, H * Cell);

    public float Corner(int i, int j)
    {
        if (i < 0 || j < 0 || i > W || j > H) return 0f;
        return Open[j * (W + 1) + i];
    }

    public float SampleCells(float x, float y)
    {
        int i = (int)MathF.Floor(x), j = (int)MathF.Floor(y);
        float fx = x - i, fy = y - j;
        float a = Corner(i, j), b = Corner(i + 1, j), c = Corner(i, j + 1), d = Corner(i + 1, j + 1);
        float top = a + (b - a) * fx, bot = c + (d - c) * fx;
        return top + (bot - top) * fy;
    }

    public float Sample(Vector2 p) => SampleCells(p.X / Cell, p.Y / Cell);
    public bool IsSolid(Vector2 p) => Sample(p) < 0.5f;
    public bool IsWater(Vector2 p) => Liquid == Liquid.Water && p.Y > WaterY && !IsSolid(p);
    public bool IsLava(Vector2 p) => Liquid == Liquid.Lava && p.Y > WaterY && !IsSolid(p);
    public bool CellOpen(int i, int j) => SampleCells(i + 0.5f, j + 0.5f) >= 0.5f;

    /// <summary>True when no rock lies on the straight segment a-b.</summary>
    public bool LineClear(Vector2 a, Vector2 b, float step = 6f)
    {
        float len = a.DistanceTo(b);
        int n = Math.Max(1, (int)(len / step));
        for (int k = 1; k < n; k++)
            if (IsSolid(a.Lerp(b, k / (float)n))) return false;
        return true;
    }

    /// <summary>Marches from `from` along `dir` until it enters rock.</summary>
    public bool Raycast(Vector2 from, Vector2 dir, float maxDist, out Vector2 hit, float step = 4f)
    {
        dir = dir.Normalized();
        for (float t = 0; t <= maxDist; t += step)
        {
            Vector2 p = from + dir * t;
            if (IsSolid(p))
            {
                // refine
                float lo = Math.Max(0, t - step), hi = t;
                for (int k = 0; k < 5; k++)
                {
                    float m = (lo + hi) * 0.5f;
                    if (IsSolid(from + dir * m)) hi = m; else lo = m;
                }
                hit = from + dir * lo;
                return true;
            }
        }
        hit = from + dir * maxDist;
        return false;
    }

    /// <summary>Direction of increasing openness (points out of rock, into open space).</summary>
    public Vector2 OpenGradient(Vector2 p)
    {
        const float e = 6f;
        float gx = Sample(p + new Vector2(e, 0)) - Sample(p - new Vector2(e, 0));
        float gy = Sample(p + new Vector2(0, e)) - Sample(p - new Vector2(0, e));
        var g = new Vector2(gx, gy);
        return g.LengthSquared() < 1e-8f ? Vector2.Up : g.Normalized();
    }

    /// <summary>Finds the floor directly below a point (the open-to-rock transition).</summary>
    public bool FindFloor(Vector2 from, float maxDist, out Vector2 floor)
        => Raycast(from, Vector2.Down, maxDist, out floor, 3f);

    public bool FindCeiling(Vector2 from, float maxDist, out Vector2 ceil)
        => Raycast(from, Vector2.Up, maxDist, out ceil, 3f);
}
