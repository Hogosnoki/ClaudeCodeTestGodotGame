using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public enum SpawnKind { Ground, Ceiling, Water, WaterFloor, WaterWall, Shore }

public sealed class SpawnPoint
{
    public Vector2 Pos;
    public SpawnKind Kind;
    /// <summary>Direction pointing away from the attached surface (into open space).</summary>
    public Vector2 Normal = Vector2.Up;
    public bool Used;
    public float Cooldown;
}

/// <summary>(a Secret room is a hidden chamber out of reach: a chimney up from a tunnel's roof, a relic chest at the top)</summary>
public enum RoomKind { Start, Boss, Treasure, MiniBoss, Ambush, Secret }

/// <summary>
/// The level's vault: a passage cut straight into the rock off a tunnel, closed by an iron gate,
/// opening into a chamber with a chest. Everything in world px.
/// </summary>
/// <summary>(a second vault, with a Locksmith's Ring, is <see cref="CaveData.ExtraVault"/>)</summary>
public sealed class VaultSpot
{
    /// <summary>The gate's foot (on the passage floor) and the passage's ceiling above it.</summary>
    public Vector2 Gate;
    public float GateTop;
    /// <summary>Where the chest sits (the middle of the chamber floor).</summary>
    public Vector2 Chest;
    /// <summary>Which way the vault lies from the tunnel (+1 right, -1 left).</summary>
    public int Side;
    /// <summary>The tunnel floor its doorstep starts from (somewhere you can walk to).</summary>
    public Vector2 Approach;
    /// <summary>The cut's cells (x0..x1, y0..y1, inclusive): the passage, then the chamber.</summary>
    public Rect2I Passage, Chamber;
    /// <summary>Its middle (for the minimap).</summary>
    public Vector2 Center;
}

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
    /// <summary>Boulder plugs in narrow passages: centre (px), and the size (px).</summary>
    public readonly List<(Vector2 Pos, Vector2 Size)> Rubble = new();

    /// <summary>The 3D rock worked out ahead of the level being built (see TerrainView.Precompute), or null.</summary>
    public object TerrainPre;
    public Vector2 StartPos;
    /// <summary>The cave mouth (depth 0 only): the floor at the daylight on the far left, where you can leave.</summary>
    public Vector2? Mouth;
    public Room Boss;
    /// <summary>The level's vault (null in the dragon's lair, or where none could be cut).</summary>
    public VaultSpot Vault;
    /// <summary>A second vault (a Locksmith's Ring).</summary>
    public VaultSpot ExtraVault;
    public readonly List<Room> Rooms = new();
    public readonly List<SpawnPoint> Spawns = new();

    // Diagnostics from generation.
    public int TrapCells;
    /// <summary>Whether the strict check (FineReach) found the guardian reachable, and how many repairs it took.</summary>
    public bool FineOk;
    public int FineRepairs;
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
    /// <summary>Which cells were open when the traversal check last ran (CellOpen over the whole grid, taken once): only good until the rock next changes.</summary>
    public bool[] OpenCells;
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
