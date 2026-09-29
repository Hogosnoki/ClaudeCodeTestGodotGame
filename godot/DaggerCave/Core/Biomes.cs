using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public enum Liquid { None, Water, Lava }

/// <summary>How a biome's cave is carved (see CaveGenerator and BiomeGen).</summary>
public enum GenStyle
{
    /// <summary>Branching tunnel walkers (the original cave generator), tuned per biome.</summary>
    Walkers,
    /// <summary>One long winding corridor (the cave entrance).</summary>
    Corridor,
    /// <summary>Circular rooms joined by passages (den, nest).</summary>
    Rooms,
    /// <summary>Right-angled chambers and corridors on several floors with stone platforms.</summary>
    Ruins,
    /// <summary>An antechamber and one great arena over lava (the dragon's lair).</summary>
    Arena,
}

public enum BiomeId { Entrance, Den, Nest, Ruins, Fungal, Tunnels, Slime, Frost, Crystal, Magma, Lair, Roots, Fossils }

/// <summary>One weighted entry of a spawn table: a factory and a group size.</summary>
public sealed class SpawnEntry
{
    public float Weight = 1;
    public Func<Enemy> Make;
    public int Min = 1, Max = 1;
    public SpawnEntry(float w, Func<Enemy> make, int min = 1, int max = 1) { Weight = w; Make = make; Min = min; Max = max; }
}

/// <summary>
/// Everything that makes a biome itself: how its cave is generated, what it looks like, what
/// hazards it has, what lives there and what guards its exit.
/// </summary>
public sealed class BiomeDef
{
    public BiomeId Id;
    public string Name;
    public int MinDepth, MaxDepth;
    /// <summary>Relative chance of being picked for an exit among biomes that fit the depth.</summary>
    public float Weight = 1f;

    // ---- generation
    public GenStyle Style = GenStyle.Walkers;
    public int W = 240, H = 140;
    public int TunnelBudget = 1500;
    public float AirRMin = 2.6f, AirRMax = 3.7f, HorizontalBias = 0.012f, BranchPitchMult = 0.8f, MaxPitch = 0.6f;
    /// <summary>Snap dry tunnels to flat or ~35 degree runs (the tight tunnels).</summary>
    public bool Diagonal;
    public Liquid Liquid = Liquid.Water;
    /// <summary>Share of the map height (from the bottom) under the liquid line.</summary>
    public float LiquidFraction = 0.5f;
    public int RoomCount = 7;
    public float RoomRMin = 8, RoomRMax = 12, CorridorR = 3f, RoomYSpread = 0.1f;
    public float PlatformBottom = 0.85f, PlatformTop = 0.17f;
    /// <summary>Open vertical spans at least this tall (cells) get ledge chains (lower = fewer long drops).</summary>
    public int TallThreshold = 10;
    public int MiniBossesMin = 1, MiniBossesMax = 2;
    public int WaterCaches = 2, HighCaches = 2;
    /// <summary>Chests sunk in the lava pools (only Magma Skin reaches them).</summary>
    public int LavaCaches;
    /// <summary>At most this many treasure rooms hold a chest.</summary>
    public int RoomChests = 4;

    // ---- look
    public Color Edge, Deep, Moss, Rim, Glow;
    public Color LiquidTop = new(0.08f, 0.32f, 0.55f, 0.38f), LiquidBottom = new(0.05f, 0.16f, 0.32f, 0.55f), LiquidLine = new(0.55f, 0.85f, 1f, 0.7f);
    public Color BackBottom = new(0.02f, 0.05f, 0.1f);
    /// <summary>Darkness vignette strength (0 = none).</summary>
    public float Darkness = 0.82f;
    /// <summary>Decoration chances on rims: grass tufts, glowing mushrooms, stalactites, crystals.</summary>
    public float Grass = 0.35f, Mushrooms = 0.04f, Stalactites = 0.22f, Crystals = 0.03f;
    public bool Bricks;

    // ---- hazards and terrain features
    public bool Slippery, IceSheet, IcePlatforms, Webs, Spores, CrystalSpikes, FireVents;
    /// <summary>Grasping roots that hold you and snag your weapon (the root-choked tunnels).</summary>
    public bool RootSnares;
    /// <summary>Unstable ceilings that shed dust, then rock, on whoever walks beneath (the fossil graveyards).</summary>
    public bool CaveIns;
    /// <summary>Thick, rotting water: you run out of breath sooner and swim slower in it.</summary>
    public bool Murky;
    public int HazardCount = 12;

    // ---- set dressing in 3D
    /// <summary>Massive tree roots come down through the ceilings.</summary>
    public bool GiantRoots;
    /// <summary>The bones of leviathans: ribcages spanning the chambers, skulls half-buried in the walls.</summary>
    public bool Leviathans;

    // ---- life
    public readonly Dictionary<SpawnKind, List<SpawnEntry>> Residents = new();
    public List<SpawnEntry> GroundEntrants = new(), AirEntrants = new(), WaterEntrants = new();
    public List<Func<Enemy>> MiniBosses = new(), WaterMiniBosses = new();
    /// <summary>The guardian of the exit chamber (spawned when you walk in).</summary>
    public Func<Room, Enemy> Guardian;
    public string GuardianTitle = "";
    public float EliteChance;
    public float Density = 1f;
    /// <summary>Fixed share of resident spawn points that hold a group (negative: follow the run's curve).</summary>
    public float ResidentFill = -1f;
    public bool Waves = true;
    public int Critters = 60;

    public (Color edge, Color deep, Color moss, Color rim, Color glow) Palette => (Edge, Deep, Moss, Rim, Glow);
    public bool HasLiquid => Liquid != Liquid.None;
}

/// <summary>The biome registry, the run's depth track, and exit choices.</summary>
public static class Biomes
{
    public const int FinalDepth = 10;
    public static readonly List<BiomeDef> All = new();
    public static BiomeDef Get(BiomeId id) => All.First(b => b.Id == id);

    static Color C(string hex) => Color.FromHtml(hex);

    static Enemy Elite(Enemy e) { e.MakeElite(); return e; }

    /// <summary>A guardian: an elite with extra health and a title.</summary>
    static Enemy Guard(Enemy e, string title, float hp = 1.6f)
    {
        e.MakeElite();
        e.MaxHp *= hp;
        e.IsGuardian = true;
        e.NamePrefix = "";
        e.Title = title;
        return e;
    }

    static List<SpawnEntry> L(params SpawnEntry[] e) => e.ToList();
    static SpawnEntry E(float w, Func<Enemy> f, int min = 1, int max = 1) => new(w, f, min, max);

    static Biomes()
    {
        // ------------------------------------------------------------------ 0: the cave entrance
        var entrance = new BiomeDef
        {
            Id = BiomeId.Entrance, Name = "Cave Entrance", MinDepth = 0, MaxDepth = 0,
            Style = GenStyle.Corridor, W = 250, H = 62, Liquid = Liquid.None,
            Edge = C("5a4a3e"), Deep = C("1e1714"), Moss = C("5c8a36"), Rim = C("7d6a58"), Glow = C("ffe0a0"),
            BackBottom = C("0e0c0a"), Darkness = 0.0f, Grass = 0.6f, Stalactites = 0.12f,
            Density = 0.45f, ResidentFill = 0.7f, Waves = false, MiniBossesMin = 0, MiniBossesMax = 0, Critters = 18,
            RoomChests = 1, WaterCaches = 0, HighCaches = 0,
            GuardianTitle = "THE WEB-MOTHER",
        };
        entrance.Residents[SpawnKind.Ceiling] = L(E(3, () => new Spider()), E(1, () => new Bat(), 1, 2));
        entrance.Residents[SpawnKind.Ground] = L(E(1, () => new Rat(), 1, 2));
        entrance.Guardian = r => { var e = Guard(new Spider(), "THE WEB-MOTHER", 0.6f); ((Spider)e).Grounded = true; return e; };
        All.Add(entrance);

        // ------------------------------------------------------------------ 1-2: den
        var den = new BiomeDef
        {
            Id = BiomeId.Den, Name = "Den", MinDepth = 1, MaxDepth = 2, Weight = 0.6f,
            Style = GenStyle.Rooms, W = 210, H = 70, Liquid = Liquid.None, RoomCount = 7, RoomRMin = 10, RoomRMax = 14, CorridorR = 3.2f, RoomYSpread = 0.06f,
            Edge = C("4e3b30"), Deep = C("150f0c"), Moss = C("6b5a2e"), Rim = C("735a48"), Glow = C("ffb060"),
            BackBottom = C("100a08"), Grass = 0.2f, Stalactites = 0.3f, Mushrooms = 0.01f,
            EliteChance = 0.28f, MiniBossesMin = 2, MiniBossesMax = 3, Density = 0.9f,
        };
        den.Residents[SpawnKind.Ground] = L(E(3, () => new Rat(), 2, 4), E(2, () => new Bear()), E(1, () => new Goblin(), 1, 2));
        den.Residents[SpawnKind.Ceiling] = L(E(1, () => new Bat(), 2, 3));
        den.GroundEntrants = L(E(3, () => new Rat()), E(1, () => new Bear()), E(1, () => new Goblin()));
        den.AirEntrants = L(E(1, () => new Bat()));
        den.MiniBosses = new() { () => new Bear(), () => new Goblin(), () => new Rat() };
        den.Guardian = r => Guard(new Bear(), "THE DEN MOTHER", 1.2f);
        All.Add(den);

        // ------------------------------------------------------------------ 1-3: root-choked tunnels (the deep canopy)
        var roots = new BiomeDef
        {
            Id = BiomeId.Roots, Name = "Root-Choked Tunnels", MinDepth = 1, MaxDepth = 3,
            W = 230, H = 110, TunnelBudget = 1350, AirRMin = 2.7f, AirRMax = 3.9f, HorizontalBias = 0.018f, BranchPitchMult = 0.6f, LiquidFraction = 0.32f,
            Edge = C("40362a"), Deep = C("110d08"), Moss = C("4f6e2c"), Rim = C("6e5c40"), Glow = C("c8e06a"),
            LiquidTop = new Color(0.2f, 0.22f, 0.08f, 0.72f), LiquidBottom = new Color(0.09f, 0.1f, 0.03f, 0.92f), LiquidLine = new Color(0.62f, 0.66f, 0.3f, 0.75f),
            BackBottom = C("0a0905"), Grass = 0.3f, Mushrooms = 0.08f, Stalactites = 0.08f, Darkness = 0.62f,
            Murky = true, RootSnares = true, GiantRoots = true, HazardCount = 13, WaterCaches = 3, HighCaches = 2,
        };
        roots.Residents[SpawnKind.Ground] = L(E(3, () => new Rat(), 2, 3), E(2, () => new Spider { Grounded = true }), E(2, () => Var(new Frog(), "Rot ", "9aa84e"), 1, 2));
        roots.Residents[SpawnKind.Ceiling] = L(E(3, () => new Spider(), 1, 2), E(2, () => new Bat(), 1, 3));
        roots.Residents[SpawnKind.Water] = L(E(1, () => Var(new Fish(), "Mire ", "8a9a50"), 1, 3));
        roots.Residents[SpawnKind.WaterWall] = L(E(1, () => Var(new Eel(), "Root ", "8a6a40")));
        roots.GroundEntrants = L(E(3, () => new Rat()), E(2, () => Var(new Frog(), "Rot ", "9aa84e")), E(1, () => new Spider { Grounded = true }));
        roots.AirEntrants = L(E(1, () => new Bat()));
        roots.WaterEntrants = L(E(1, () => Var(new Fish(), "Mire ", "8a9a50")));
        roots.MiniBosses = new() { () => Var(new Bear(), "Rotback ", "8a9a5a"), () => Var(new Frog(), "Rot ", "9aa84e"), () => new Spider { Grounded = true } };
        roots.WaterMiniBosses = new() { () => Var(new Eel(), "Root ", "8a6a40") };
        roots.Guardian = r => Guard(Var(new Bear(), "", "7e8e4c"), "THE ROTBACK", 1.35f);
        All.Add(roots);

        // ------------------------------------------------------------------ 1-3: nest
        var nest = new BiomeDef
        {
            Id = BiomeId.Nest, Name = "Nest", MinDepth = 1, MaxDepth = 3, Weight = 1.2f,
            Style = GenStyle.Rooms, W = 180, H = 120, Liquid = Liquid.None, RoomCount = 8, RoomRMin = 7, RoomRMax = 10, CorridorR = 2.6f, RoomYSpread = 0.55f,
            Edge = C("3b3a2a"), Deep = C("100f0a"), Moss = C("8a8a3a"), Rim = C("67624a"), Glow = C("d0ff70"),
            BackBottom = C("0c0c06"), Grass = 0.15f, Stalactites = 0.3f, Webs = true, HazardCount = 14,
            MiniBossesMin = 1, MiniBossesMax = 2, Density = 1.1f,
        };
        nest.Residents[SpawnKind.Ground] = L(E(3, () => new Scorpion(), 1, 2), E(1, () => new Spider { Grounded = true }));
        nest.Residents[SpawnKind.Ceiling] = L(E(3, () => new Spider(), 1, 2), E(2, () => new Hornet(), 2, 3));
        nest.GroundEntrants = L(E(2, () => new Scorpion()));
        nest.AirEntrants = L(E(3, () => new Hornet()));
        nest.MiniBosses = new() { () => new Scorpion(), () => new Spider { Grounded = true }, () => new Hornet() };
        nest.Guardian = r => Guard(new Scorpion(), "THE BROOD QUEEN", 2.4f);
        All.Add(nest);

        // ------------------------------------------------------------------ 2-3: ruins
        var ruins = new BiomeDef
        {
            Id = BiomeId.Ruins, Name = "Ruins", MinDepth = 2, MaxDepth = 3,
            Style = GenStyle.Ruins, W = 190, H = 96, Liquid = Liquid.None,
            Edge = C("5c5650"), Deep = C("1a1816"), Moss = C("55703a"), Rim = C("8a8278"), Glow = C("ffd080"),
            BackBottom = C("0c0b0a"), Bricks = true, Grass = 0.12f, Stalactites = 0.05f, Crystals = 0.0f,
            MiniBossesMin = 1, MiniBossesMax = 2,
        };
        ruins.Residents[SpawnKind.Ground] = L(E(3, () => new Skeleton(), 1, 2), E(2, () => new Goblin(), 1, 2), E(1, () => new Goblin { Slinger = true }),
            E(1, () => new Rat(), 2, 3), E(1, () => new Scorpion()));
        ruins.Residents[SpawnKind.Ceiling] = L(E(1, () => new Bat(), 1, 2));
        ruins.GroundEntrants = L(E(3, () => new Skeleton()), E(2, () => new Goblin()), E(1, () => new Rat()));
        ruins.AirEntrants = L(E(1, () => new Bat()));
        ruins.MiniBosses = new() { () => new Skeleton(), () => new Goblin(), () => new Goblin { Slinger = true } };
        ruins.Guardian = r => Guard(new Skeleton(), "THE BONE KNIGHT", 2.2f);
        All.Add(ruins);

        // ------------------------------------------------------------------ 3-4: fungal cavern
        var fungal = new BiomeDef
        {
            Id = BiomeId.Fungal, Name = "Fungal Cavern", MinDepth = 3, MaxDepth = 4,
            W = 240, H = 90, Liquid = Liquid.None, TunnelBudget = 1000, AirRMin = 2.8f, AirRMax = 3.8f, HorizontalBias = 0.022f, BranchPitchMult = 0.45f,
            Edge = C("3a3044"), Deep = C("0e0a12"), Moss = C("8a4aa8"), Rim = C("5e4c6e"), Glow = C("80ffa0"),
            BackBottom = C("0a0610"), Grass = 0.25f, Mushrooms = 0.28f, Stalactites = 0.1f, Spores = true, HazardCount = 16,
            PlatformBottom = 0.6f, PlatformTop = 0.3f,
        };
        fungal.Residents[SpawnKind.Ground] = L(E(3, () => new Sporeling(), 1, 3), E(2, () => new Frog(), 1, 2), E(1, () => Var(new Golem(), "Mossback ", "7db86a", 1.1f)));
        fungal.Residents[SpawnKind.Ceiling] = L(E(1, () => new Spider()), E(1, () => Var(new Hornet(), "Spore ", "b070ff")));
        fungal.GroundEntrants = L(E(3, () => new Sporeling()), E(2, () => new Frog()));
        fungal.AirEntrants = L(E(1, () => Var(new Hornet(), "Spore ", "b070ff")));
        fungal.MiniBosses = new() { () => new Sporeling(), () => Var(new Golem(), "Mossback ", "7db86a") };
        fungal.Guardian = r => Guard(Var(new Golem(), "", "7db86a"), "THE MOSSBACK HULK", 1.2f);
        All.Add(fungal);

        // ------------------------------------------------------------------ 3-5: tunnels
        var tunnels = new BiomeDef
        {
            Id = BiomeId.Tunnels, Name = "Tunnels", MinDepth = 3, MaxDepth = 5,
            W = 240, H = 110, TunnelBudget = 1700, AirRMin = 1.9f, AirRMax = 2.5f, HorizontalBias = 0.02f, Diagonal = true, LiquidFraction = 0.3f,
            Edge = C("40372f"), Deep = C("120e0c"), Moss = C("4c6a34"), Rim = C("6a5a4c"), Glow = C("ffc070"),
            BackBottom = C("0c0a08"), Stalactites = 0.3f, WaterCaches = 3, HighCaches = 2,
        };
        tunnels.Residents[SpawnKind.Ground] = L(E(3, () => new Rat(), 2, 4), E(1, () => new Bear()), E(1, () => new Goblin()));
        tunnels.Residents[SpawnKind.Ceiling] = L(E(3, () => new Bat(), 2, 3));
        tunnels.Residents[SpawnKind.Water] = L(E(1, () => new Fish(), 2, 3));
        tunnels.Residents[SpawnKind.WaterFloor] = L(E(1, () => new Urchin(), 1, 2));
        tunnels.Residents[SpawnKind.WaterWall] = L(E(1, () => new Eel()));
        tunnels.GroundEntrants = L(E(3, () => new Rat()), E(1, () => new Bear()));
        tunnels.AirEntrants = L(E(1, () => new Bat()));
        tunnels.WaterEntrants = L(E(1, () => new Fish()));
        tunnels.MiniBosses = new() { () => new Bear(), () => new Rat() };
        tunnels.WaterMiniBosses = new() { () => new Eel(), () => new Fish() };
        tunnels.Guardian = r => Guard(new Bear(), "THE TUNNEL BRUTE", 1.5f);
        All.Add(tunnels);

        // ------------------------------------------------------------------ 4-6: slime cavern (the original cave)
        var slime = new BiomeDef
        {
            Id = BiomeId.Slime, Name = "Slime Cavern", MinDepth = 4, MaxDepth = 6,
            W = 250, H = 150, TunnelBudget = 1650,
            Edge = C("4a3d38"), Deep = C("120f11"), Moss = C("52853f"), Rim = C("75645a"), Glow = C("73e6ff"),
            WaterCaches = 5, HighCaches = 3, MiniBossesMin = 2, MiniBossesMax = 3,
        };
        slime.Residents[SpawnKind.Ground] = L(E(2, () => new Goblin { Slinger = G.Chance(0.35f) }, 1, 3), E(2, () => new Frog(), 1, 2), E(1, () => new LavaMonster()), E(1, () => new Golem()));
        slime.Residents[SpawnKind.Ceiling] = L(E(3, () => new Bat(), 2, 4), E(2, () => new Spider(), 1, 2));
        slime.Residents[SpawnKind.Water] = L(E(1, () => new Fish(), 2, 4));
        slime.Residents[SpawnKind.WaterFloor] = L(E(1, () => new Urchin(), 1, 2));
        slime.Residents[SpawnKind.WaterWall] = L(E(1, () => new Eel()));
        slime.GroundEntrants = L(E(4, () => new Frog()), E(4, () => new Goblin()), E(2, () => new Goblin { Slinger = true }), E(1, () => new Golem()), E(1, () => new LavaMonster()));
        slime.AirEntrants = L(E(1, () => new Bat()));
        slime.WaterEntrants = L(E(1, () => new Fish()));
        slime.MiniBosses = new() { () => new Golem(), () => new Goblin(), () => new Frog(), () => new LavaMonster(), () => new Goblin { Slinger = true } };
        slime.WaterMiniBosses = new() { () => new Eel(), () => new Fish() };
        slime.Guardian = r => { var c = new CavernColossus(); c.Init(r); c.Title = "THE CAVERN COLOSSUS"; return c; };
        All.Add(slime);

        // ------------------------------------------------------------------ 5-7: frost caverns
        var frost = new BiomeDef
        {
            Id = BiomeId.Frost, Name = "Frost Caverns", MinDepth = 5, MaxDepth = 7,
            W = 250, H = 110, TunnelBudget = 1250, AirRMin = 2.6f, AirRMax = 3.5f, HorizontalBias = 0.025f, BranchPitchMult = 0.45f, LiquidFraction = 0.4f,
            Edge = C("5a7288"), Deep = C("141c26"), Moss = C("c8e8ff"), Rim = C("9ab8d0"), Glow = C("a0f0ff"),
            LiquidTop = new Color(0.12f, 0.35f, 0.5f, 0.45f), LiquidBottom = new Color(0.05f, 0.14f, 0.26f, 0.6f), LiquidLine = new Color(0.85f, 0.97f, 1f, 0.8f),
            BackBottom = C("060c14"), Grass = 0.0f, Stalactites = 0.4f, Crystals = 0.06f, Darkness = 0.7f,
            Slippery = true, IceSheet = true, IcePlatforms = true, WaterCaches = 3, HighCaches = 3,
        };
        frost.Residents[SpawnKind.Ground] = L(E(2, () => Var(new Bear(), "Frost ", "c0e0ff", 1.1f)), E(2, () => Var(new Skeleton(), "Rime ", "b0e8ff"), 1, 2));
        frost.Residents[SpawnKind.Ceiling] = L(E(3, () => new FrostWraith()), E(1, () => Var(new Bat(), "Ice ", "a8e0ff"), 2, 3));
        frost.Residents[SpawnKind.Water] = L(E(1, () => new Fish(), 2, 3));
        frost.Residents[SpawnKind.WaterWall] = L(E(1, () => new Eel()));
        frost.GroundEntrants = L(E(2, () => Var(new Skeleton(), "Rime ", "b0e8ff")), E(1, () => Var(new Bear(), "Frost ", "c0e0ff", 1.1f)));
        frost.AirEntrants = L(E(2, () => new FrostWraith()), E(1, () => Var(new Bat(), "Ice ", "a8e0ff")));
        frost.WaterEntrants = L(E(1, () => new Fish()));
        frost.MiniBosses = new() { () => new FrostWraith(), () => Var(new Bear(), "Frost ", "c0e0ff") };
        frost.WaterMiniBosses = new() { () => new Eel() };
        frost.Guardian = r => { var c = new CavernColossus(); c.Init(r); c.Tint = C("b8e0ff"); c.Title = "THE RIME COLOSSUS"; return c; };
        All.Add(frost);

        // ------------------------------------------------------------------ 5-7: fossil graveyards
        var fossils = new BiomeDef
        {
            Id = BiomeId.Fossils, Name = "Fossil Graveyards", MinDepth = 5, MaxDepth = 7,
            Style = GenStyle.Rooms, W = 260, H = 120, Liquid = Liquid.None, RoomCount = 6, RoomRMin = 14, RoomRMax = 19, CorridorR = 3.2f, RoomYSpread = 0.3f,
            Edge = C("5c5448"), Deep = C("17140f"), Moss = C("8a8266"), Rim = C("a39a86"), Glow = C("eadcb4"),
            BackBottom = C("0b0a08"), Grass = 0.04f, Mushrooms = 0.0f, Stalactites = 0.14f, Crystals = 0.0f, Darkness = 0.8f,
            CaveIns = true, Leviathans = true, HazardCount = 10, EliteChance = 0.12f, MiniBossesMin = 2, MiniBossesMax = 3,
        };
        fossils.Residents[SpawnKind.Ground] = L(E(3, () => Var(new Skeleton(), "Fossil ", "e8dcc0"), 1, 3), E(2, () => Var(new Scorpion(), "Bone ", "d8ccb0"), 1, 2),
            E(1, () => Var(new Golem(), "Ossuary ", "e0d4b8", 1.2f)), E(1, () => Var(new Rat(), "Marrow ", "d0c4a8"), 2, 3));
        fossils.Residents[SpawnKind.Ceiling] = L(E(2, () => new Bat(), 2, 3), E(1, () => new Spider()));
        fossils.GroundEntrants = L(E(3, () => Var(new Skeleton(), "Fossil ", "e8dcc0")), E(1, () => Var(new Scorpion(), "Bone ", "d8ccb0")));
        fossils.AirEntrants = L(E(1, () => new Bat()));
        fossils.MiniBosses = new() { () => Var(new Golem(), "Ossuary ", "e0d4b8"), () => Var(new Skeleton(), "Fossil ", "e8dcc0"), () => Var(new Scorpion(), "Bone ", "d8ccb0") };
        fossils.Guardian = r => { var c = new CavernColossus(); c.Init(r); c.Tint = C("e8dcc0"); c.Title = "THE OSSUARY COLOSSUS"; return c; };
        All.Add(fossils);

        // ------------------------------------------------------------------ 6-8: crystal caves
        var crystal = new BiomeDef
        {
            Id = BiomeId.Crystal, Name = "Crystal Caves", MinDepth = 6, MaxDepth = 8,
            W = 200, H = 140, TunnelBudget = 1300, AirRMin = 2.6f, AirRMax = 3.6f, HorizontalBias = 0.01f, Liquid = Liquid.None,
            PlatformBottom = 1.0f, PlatformTop = 0.95f, TallThreshold = 7,
            Edge = C("2c3a52"), Deep = C("0a0e1a"), Moss = C("6ad0e8"), Rim = C("5a70a0"), Glow = C("b890ff"),
            BackBottom = C("06081a"), Grass = 0.05f, Crystals = 0.3f, Stalactites = 0.15f, CrystalSpikes = true, HazardCount = 14, Darkness = 0.72f,
        };
        crystal.Residents[SpawnKind.Ground] = L(E(3, () => new Shardling(), 1, 3), E(2, () => Var(new Golem(), "Crystal ", "a0d8ff", 1.2f)), E(1, () => new Skeleton()));
        crystal.Residents[SpawnKind.Ceiling] = L(E(2, () => Var(new Bat(), "Crystal ", "c0a8ff"), 2, 3), E(1, () => new Spider()));
        crystal.GroundEntrants = L(E(3, () => new Shardling()), E(1, () => Var(new Golem(), "Crystal ", "a0d8ff", 1.2f)));
        crystal.AirEntrants = L(E(1, () => Var(new Bat(), "Crystal ", "c0a8ff")));
        crystal.MiniBosses = new() { () => new Shardling(), () => Var(new Golem(), "Crystal ", "a0d8ff") };
        crystal.Guardian = r => Guard(Var(new Golem(), "", "b0e0ff"), "THE PRISM GOLEM", 1.3f);
        All.Add(crystal);

        // ------------------------------------------------------------------ 8-9: magma caverns
        var magma = new BiomeDef
        {
            Id = BiomeId.Magma, Name = "Magma Caverns", MinDepth = 8, MaxDepth = 9,
            W = 230, H = 120, TunnelBudget = 1450, AirRMin = 2.8f, AirRMax = 3.8f, HorizontalBias = 0.03f, Liquid = Liquid.Lava, LiquidFraction = 0.35f,
            PlatformBottom = 1.0f, PlatformTop = 0.6f,
            Edge = C("4a2a22"), Deep = C("140806"), Moss = C("8a3a1a"), Rim = C("7a4636"), Glow = C("ff9040"),
            LiquidTop = new Color(1f, 0.42f, 0.08f, 0.85f), LiquidBottom = new Color(0.6f, 0.12f, 0.02f, 0.95f), LiquidLine = new Color(1f, 0.85f, 0.4f, 1f),
            BackBottom = C("200604"), Grass = 0.0f, Stalactites = 0.25f, Mushrooms = 0.0f, FireVents = true, HazardCount = 12, Darkness = 0.6f,
            MiniBossesMin = 2, MiniBossesMax = 2, LavaCaches = 4,
        };
        magma.Residents[SpawnKind.Ground] = L(E(3, () => new LavaMonster()), E(2, () => Var(new Golem(), "Obsidian ", "6a5a7a", 1.3f)), E(1, () => Var(new Scorpion(), "Ember ", "ff9060"), 1, 2));
        magma.Residents[SpawnKind.Ceiling] = L(E(2, () => Var(new Bat(), "Fire ", "ff9050"), 2, 3), E(1, () => Var(new Hornet(), "Ember ", "ff8040")));
        magma.GroundEntrants = L(E(2, () => new LavaMonster()), E(1, () => Var(new Scorpion(), "Ember ", "ff9060")));
        magma.AirEntrants = L(E(2, () => Var(new Bat(), "Fire ", "ff9050")));
        magma.MiniBosses = new() { () => new LavaMonster(), () => Var(new Golem(), "Obsidian ", "6a5a7a") };
        magma.Guardian = r => { var c = new CavernColossus(); c.Init(r); c.Tint = C("ffa070"); c.Title = "THE MOLTEN COLOSSUS"; return c; };
        All.Add(magma);

        // ------------------------------------------------------------------ 10: the dragon's lair
        var lair = new BiomeDef
        {
            Id = BiomeId.Lair, Name = "Dragon's Lair", MinDepth = 10, MaxDepth = 10,
            Style = GenStyle.Arena, W = 128, H = 76, Liquid = Liquid.Lava, LiquidFraction = 0.2f,
            Edge = C("3a1a18"), Deep = C("120404"), Moss = C("6a2a1a"), Rim = C("6a3a30"), Glow = C("ffb040"),
            LiquidTop = new Color(1f, 0.45f, 0.1f, 0.9f), LiquidBottom = new Color(0.65f, 0.12f, 0.02f, 0.95f), LiquidLine = new Color(1f, 0.9f, 0.5f, 1f),
            BackBottom = C("2a0804"), Grass = 0f, Stalactites = 0.3f, Darkness = 0.45f, Waves = false, Density = 0f,
            MiniBossesMin = 0, MiniBossesMax = 0, Critters = 0, WaterCaches = 0, HighCaches = 0,
        };
        lair.Guardian = r => { var d = new Dragon(); d.Init(r); return d; };
        All.Add(lair);
        AddElementals();
    }

    /// <summary>
    /// The Elementals live where their element is: Nature in the roots and the fungal caves, Water in
    /// every biome with water in it, Fire in the magma caverns, Frost in the frost caverns, and Earth
    /// wherever the walls are dirt (dens, nests, tunnels, roots and the fossil graveyards).
    /// </summary>
    static void AddElementals()
    {
        static void Ground(BiomeId id, float w, Func<Enemy> make, int min = 1, int max = 1, bool entrant = true, bool mini = true)
        {
            var b = Get(id);
            if (!b.Residents.TryGetValue(SpawnKind.Ground, out var l)) b.Residents[SpawnKind.Ground] = l = new();
            l.Add(E(w, make, min, max));
            if (entrant) b.GroundEntrants.Add(E(Math.Max(1, w * 0.5f), make));
            if (mini) b.MiniBosses.Add(make);
        }
        Ground(BiomeId.Roots, 2, () => new NatureElemental());
        Ground(BiomeId.Fungal, 2, () => new NatureElemental());
        Ground(BiomeId.Frost, 2, () => new FrostElemental());
        Ground(BiomeId.Magma, 2, () => new FireElemental(), 1, 2);
        Ground(BiomeId.Den, 1, () => new EarthElemental());
        Ground(BiomeId.Nest, 1, () => new EarthElemental());
        Ground(BiomeId.Tunnels, 1, () => new EarthElemental());
        Ground(BiomeId.Roots, 1, () => new EarthElemental(), entrant: false, mini: false);
        Ground(BiomeId.Fossils, 2, () => new EarthElemental());
        foreach (var b in All)
        {
            if (b.Liquid != Liquid.Water) continue;
            if (!b.Residents.TryGetValue(SpawnKind.Water, out var l)) b.Residents[SpawnKind.Water] = l = new();
            l.Add(E(1, () => new WaterElemental()));
            b.WaterEntrants.Add(E(1, () => new WaterElemental()));
            b.WaterMiniBosses.Add(() => new WaterElemental());
        }
    }

    /// <summary>A tinted, renamed variant of a creature (with optional health scaling).</summary>
    public static Enemy Var(Enemy e, string prefix, string tint, float hp = 1f)
    {
        e.NamePrefix = prefix;
        e.Tint = Color.FromHtml(tint);
        e.MaxHp *= hp;
        return e;
    }

    /// <summary>
    /// The exits out of a finished level: a gentle one (one depth deeper) and a steep one (two
    /// deeper), each into a biome that suits its depth. The last levels lead to the lair.
    /// </summary>
    public static List<(BiomeDef biome, int depth)> ChooseExits(int depth, Random rng)
    {
        var exits = new List<(BiomeDef, int)>();
        if (depth + 1 >= FinalDepth) { exits.Add((Get(BiomeId.Lair), FinalDepth)); return exits; }
        var slow = PickFor(depth + 1, null, rng);
        exits.Add((slow, depth + 1));
        int fastDepth = depth + 2;
        if (fastDepth >= FinalDepth) exits.Add((Get(BiomeId.Lair), FinalDepth));
        else exits.Add((PickFor(fastDepth, slow, rng), fastDepth));
        return exits;
    }

    private static BiomeDef PickFor(int depth, BiomeDef avoid, Random rng)
    {
        var pool = All.Where(b => b.Id is not (BiomeId.Entrance or BiomeId.Lair) && depth >= b.MinDepth && depth <= b.MaxDepth).ToList();
        if (pool.Count > 1 && avoid != null) pool.Remove(avoid);
        if (pool.Count == 0) pool = All.Where(b => b.Id is not (BiomeId.Entrance or BiomeId.Lair)).ToList();
        float total = pool.Sum(b => b.Weight);
        double r = rng.NextDouble() * total;
        foreach (var b in pool) { r -= b.Weight; if (r <= 0) return b; }
        return pool[^1];
    }

    public static Enemy Roll(List<SpawnEntry> table, out int count)
    {
        count = 0;
        if (table == null || table.Count == 0) return null;
        float total = table.Sum(e => e.Weight);
        double r = G.Rng.NextDouble() * total;
        foreach (var e in table)
        {
            r -= e.Weight;
            if (r <= 0) { count = G.RangeI(e.Min, e.Max); return e.Make(); }
        }
        var last = table[^1];
        count = G.RangeI(last.Min, last.Max);
        return last.Make();
    }

    public static SpawnEntry Pick(List<SpawnEntry> table)
    {
        float total = table.Sum(e => e.Weight);
        double r = G.Rng.NextDouble() * total;
        foreach (var e in table) { r -= e.Weight; if (r <= 0) return e; }
        return table[^1];
    }

    public static Enemy Make(List<SpawnEntry> table)
    {
        if (table == null || table.Count == 0) return null;
        float total = table.Sum(e => e.Weight);
        double r = G.Rng.NextDouble() * total;
        foreach (var e in table) { r -= e.Weight; if (r <= 0) return e.Make(); }
        return table[^1].Make();
    }
}
