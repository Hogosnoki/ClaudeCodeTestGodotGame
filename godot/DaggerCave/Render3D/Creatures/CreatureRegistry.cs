using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Every sprite set that has a 3D design (sets without one keep a placeholder).</summary>
public static class CreatureRegistry
{
    public static void RegisterAll()
    {
        CreatureLibrary.Register("swordsman", () => new HeroDesign(HeroKind.Swordsman));
        CreatureLibrary.Register("warden", () => new HeroDesign(HeroKind.Warden));
        CreatureLibrary.Register("vitalist", () => new HeroDesign(HeroKind.Vitalist));
        CreatureLibrary.Register("elementalist", () => new HeroDesign(HeroKind.Elementalist));
        CreatureLibrary.Register("rogue", () => new HeroDesign(HeroKind.Rogue));
        CreatureLibrary.Register("spider", () => new SpiderDesign());
        CreatureLibrary.Register("goblin", () => new GoblinDesign(false));
        CreatureLibrary.Register("slinger", () => new GoblinDesign(true));
        CreatureLibrary.Register("skeleton", () => new SkeletonDesign());
        CreatureLibrary.Register("rat", () => new RatDesign());
        CreatureLibrary.Register("bear", () => new BearDesign());
        CreatureLibrary.Register("bat", () => new BatDesign());
        CreatureLibrary.Register("scorpion", () => new ScorpionDesign());
        CreatureLibrary.Register("hornet", () => new HornetDesign());
        CreatureLibrary.Register("frog", () => new FrogDesign());
        CreatureLibrary.Register("fish", () => new FishDesign(false));
        CreatureLibrary.Register("fish2", () => new FishDesign(true));
        CreatureLibrary.Register("eel", () => new EelDesign());
        CreatureLibrary.Register("urchin", () => new UrchinDesign());
        CreatureLibrary.Register("golem", () => new GolemDesign());
        CreatureLibrary.Register("magma", () => new MagmaDesign());
        CreatureLibrary.Register("sporeling", () => new SporelingDesign());
        CreatureLibrary.Register("wraith", () => new WraithDesign());
        CreatureLibrary.Register("shardling", () => new ShardlingDesign());
        CreatureLibrary.Register("moth", () => new MothDesign());
        CreatureLibrary.Register("crab", () => new CrabDesign());
        CreatureLibrary.Register("colossus", () => new ColossusDesign());
        CreatureLibrary.Register("dragon", () => new DragonDesign());
        CreatureLibrary.Register("elem_earth", () => new EarthElementalDesign());
        CreatureLibrary.Register("elem_frost", () => new FrostElementalDesign());
        CreatureLibrary.Register("elem_nature", () => new NatureElementalDesign());
        CreatureLibrary.Register("elem_fire", () => new FireElementalDesign());
        CreatureLibrary.Register("elem_water", () => new WaterElementalDesign());
    }

    /// <summary>The sprite sets each enemy class wears.</summary>
    private static readonly Dictionary<string, string[]> SetsByType = new()
    {
        ["Bat"] = new[] { "bat" }, ["Spider"] = new[] { "spider" }, ["Goblin"] = new[] { "goblin", "slinger" }, ["Frog"] = new[] { "frog" },
        ["LavaMonster"] = new[] { "magma" }, ["Golem"] = new[] { "golem" }, ["Rat"] = new[] { "rat" }, ["Bear"] = new[] { "bear" },
        ["Scorpion"] = new[] { "scorpion" }, ["Hornet"] = new[] { "hornet" }, ["Skeleton"] = new[] { "skeleton" }, ["Sporeling"] = new[] { "sporeling" },
        ["FrostWraith"] = new[] { "wraith" }, ["Shardling"] = new[] { "shardling" }, ["Fish"] = new[] { "fish", "fish2" }, ["Urchin"] = new[] { "urchin" },
        ["Eel"] = new[] { "eel" }, ["EarthElemental"] = new[] { "elem_earth" }, ["FrostElemental"] = new[] { "elem_frost" }, ["NatureElemental"] = new[] { "elem_nature" },
        ["FireElemental"] = new[] { "elem_fire" }, ["WaterElemental"] = new[] { "elem_water" }, ["CavernColossus"] = new[] { "colossus" }, ["Dragon"] = new[] { "dragon" },
    };

    private static readonly Dictionary<BiomeId, string> GuardianSet = new()
    {
        [BiomeId.Entrance] = "spider", [BiomeId.Den] = "bear", [BiomeId.Nest] = "scorpion", [BiomeId.Ruins] = "skeleton", [BiomeId.Fungal] = "golem",
        [BiomeId.Tunnels] = "bear", [BiomeId.Slime] = "colossus", [BiomeId.Frost] = "colossus", [BiomeId.Crystal] = "golem", [BiomeId.Magma] = "colossus",
        [BiomeId.Lair] = "dragon", [BiomeId.Roots] = "bear", [BiomeId.Fossils] = "colossus",
    };

    /// <summary>Every creature type a level of this biome can put on screen (for loading ahead).</summary>
    public static IEnumerable<string> Roster(BiomeDef b)
    {
        var names = new HashSet<string> { Player.SheetName(G.Hero), "moth", "crab" };
        void Add(Func<Enemy> make)
        {
            if (make == null) return;
            var e = make();
            if (SetsByType.TryGetValue(e.GetType().Name, out var sets)) names.UnionWith(sets);
            e.Free();
        }
        foreach (var list in b.Residents.Values) foreach (var s in list) Add(s.Make);
        foreach (var s in b.GroundEntrants) Add(s.Make);
        foreach (var s in b.AirEntrants) Add(s.Make);
        foreach (var s in b.WaterEntrants) Add(s.Make);
        foreach (var m in b.MiniBosses) Add(m);
        foreach (var m in b.WaterMiniBosses) Add(m);
        if (GuardianSet.TryGetValue(b.Id, out var g)) names.Add(g);
        return names;
    }
}
