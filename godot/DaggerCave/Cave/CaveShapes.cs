using System;

namespace DaggerCave;

/// <summary>
/// The shape of a cave. Every level of a biome used to be cut from the same numbers (the same tunnel width, the same branching, the
/// same map size, the same share of water), so they felt alike however the seed fell. Now each level of the wandering-tunnel and
/// chambered biomes draws a shape from its seed, and the generator's own numbers are bent toward it before it carves:
/// <list type="bullet">
/// <item><b>The Warren</b>: narrow, twisting tunnels that fork again and again; small chambers.</item>
/// <item><b>The Great Halls</b>: few, broad passages and big chambers under high roofs.</item>
/// <item><b>The Long Galleries</b>: a wide, low map; long level runs that seldom climb.</item>
/// <item><b>The Deeps</b>: a narrow, tall map; the way winds down and down (wandering-tunnel biomes).</item>
/// <item><b>The Flooded Reaches</b>: the water stands much higher (water biomes only).</item>
/// <item><b>The Dry Hollows</b>: the water lies low; most of the cave is walked (water biomes only).</item>
/// <item><b>The Honeycomb</b>: many small chambers close together, at every height (chambered biomes).</item>
/// </list>
/// About one level in five keeps the biome's plain shape. The shape is a pure function of the biome and the seed, so every player of
/// an online game makes the same cave, and the cave is still checked and repaired by the same reachability tests as before.
/// </summary>
public static class CaveShapes
{
    /// <summary>Off in the tests (their arenas were written for the plain shapes) unless asked for with <c>--shapes</c>.</summary>
    public static bool On = true;

    public static BiomeDef Shape(BiomeDef b, int seed, out string name)
    {
        name = "";
        if (!On || b.Shop || b.Style is not (GenStyle.Walkers or GenStyle.Rooms)) return b;
        // (a hash of the seed, apart from the generator's own use of it)
        uint h = unchecked((uint)seed + 0x9E3779B9u);
        h ^= h >> 16; h = unchecked(h * 0x85EBCA6Bu); h ^= h >> 13; h = unchecked(h * 0xC2B2AE35u); h ^= h >> 16;
        bool water = b.Liquid == Liquid.Water && b.LiquidFraction > 0.2f;
        string[] pool = b.Style == GenStyle.Walkers
            ? (water ? new[] { "", "The Warren", "The Great Halls", "The Long Galleries", "The Deeps", "The Flooded Reaches", "The Dry Hollows" }
                     : new[] { "", "The Warren", "The Great Halls", "The Long Galleries", "The Deeps", "The Warren", "The Great Halls" })
            : new[] { "", "The Great Halls", "The Honeycomb", "The Long Galleries" };
        name = pool[h % (uint)pool.Length];
        if (name == "") return b;
        var c = b.Copy();
        c.ShapeName = name;
        bool walk = b.Style == GenStyle.Walkers;
        switch (name)
        {
            case "The Warren":
                c.AirRMin = Math.Max(2.4f, b.AirRMin * 0.9f); c.AirRMax = Math.Max(c.AirRMin + 0.5f, b.AirRMax * 0.86f);
                c.BranchChance = b.BranchChance * 1.4f; c.MaxBranchGen = b.MaxBranchGen + 1; c.TunnelBudget = (int)(b.TunnelBudget * 1.06f);
                c.RoomScale = b.RoomScale * 0.9f; c.RoughAmp = b.RoughAmp * 1.15f;
                break;
            case "The Great Halls":
                if (walk)
                {
                    c.AirRMin = b.AirRMin * 1.22f; c.AirRMax = b.AirRMax * 1.42f;
                    c.BranchChance = b.BranchChance * 0.65f; c.TunnelBudget = (int)(b.TunnelBudget * 0.86f); c.RoomScale = b.RoomScale * 1.3f;
                }
                else { c.RoomCount = Math.Max(3, b.RoomCount - 2); c.RoomRMin = b.RoomRMin * 1.25f; c.RoomRMax = b.RoomRMax * 1.3f; c.CorridorR = b.CorridorR * 1.2f; }
                c.Stalactites = Math.Min(0.6f, b.Stalactites * 1.5f);
                break;
            case "The Long Galleries":
                c.W = (int)(b.W * (walk ? 1.14f : 1.22f)); c.H = (int)(b.H * (walk ? 0.9f : 0.82f));
                if (walk) { c.HorizontalBias = b.HorizontalBias * 1.7f; c.BranchPitchMult = b.BranchPitchMult * 0.75f; }
                else c.RoomYSpread = b.RoomYSpread * 0.5f;
                break;
            case "The Deeps":
                c.W = (int)(b.W * 0.82f); c.H = (int)(b.H * 1.22f);
                if (walk) { c.HorizontalBias = b.HorizontalBias * 0.5f; c.BranchPitchMult = Math.Min(1f, b.BranchPitchMult * 1.15f); c.StartRow = Math.Min(b.StartRow, 0.17f); }
                else c.RoomYSpread = Math.Min(0.4f, b.RoomYSpread * 2.2f);
                break;
            case "The Flooded Reaches":
                c.LiquidFraction = Math.Min(0.7f, b.LiquidFraction + 0.15f); c.WaterCaches = b.WaterCaches + 1;
                break;
            case "The Dry Hollows":
                c.LiquidFraction = b.LiquidFraction * 0.6f; c.WaterCaches = Math.Max(0, b.WaterCaches - 1); c.HighCaches = b.HighCaches + 1;
                break;
            case "The Honeycomb":
                c.RoomCount = b.RoomCount + 4; c.RoomRMin = Math.Max(6f, b.RoomRMin * 0.75f); c.RoomRMax = Math.Max(c.RoomRMin + 1f, b.RoomRMax * 0.75f);
                c.RoomYSpread = Math.Min(0.4f, b.RoomYSpread * 1.8f);
                break;
        }
        return c;
    }
}
