using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>One rank of a meta tree: bought with embers, then activated with the tree's resource.</summary>
public sealed class MetaTier
{
    public string Id, Branch, Name, Desc;
    public int Rank;   // 1-based within its branch
    public int Cost;   // embers
}

/// <summary>A progression tree: its resource and its branches of ranks.</summary>
public sealed class MetaTree
{
    public string Id, Name, Resource, ResourcePlural;
    public Color Color;
    public readonly List<(string id, string name)> Branches = new();
    public readonly List<MetaTier> Tiers = new();
    public int MaxResource => Tiers.Count;
    public IEnumerable<MetaTier> Branch(string b) => Tiers.Where(t => t.Branch == b).OrderBy(t => t.Rank);
}

/// <summary>
/// What carries over between runs, saved to user://meta.json:
///  * Embers, the currency: from exit guardians and from rewards you skipped in levels whose
///    guardian you then beat. Embers buy the ranks of the trees.
///  * Resources, one per rank in the game (13 Reagents, 9 Obsidian Pearls): an exit guardian
///    yields one, drawn evenly from everything not yet found. A bought rank only takes effect
///    once a resource of its tree is spent on it.
/// A tree stays hidden until its first resource is found.
/// </summary>
public static class Meta
{
    public static int Embers;
    public static int Runs, Victories, BestDepth;
    public static readonly Dictionary<string, int> Held = new();   // unspent resources per tree
    public static readonly Dictionary<string, int> Found = new();  // resources found per tree
    public static readonly HashSet<string> Bought = new(), Active = new();
    public static bool PotionTutorialDone, PearlTutorialDone;
    /// <summary>Class perks: the rank bought of each (kept for good), and which are brought on a run.</summary>
    public static readonly Dictionary<string, int> PerkRanks = new();
    public static readonly HashSet<string> PerksEquipped = new();
    public static int PerkRank(string id) => PerkRanks.TryGetValue(id, out var n) ? n : 0;
    public static bool PerkEquipped(string id) => PerksEquipped.Contains(id);

    /// <summary>Buys the next rank of a class perk with embers (and brings it along if there's a free slot). False if it can't be afforded or is complete.</summary>
    public static bool PerkBuy(ClassPerk perk)
    {
        int rank = PerkRank(perk.Id);
        if (rank >= perk.MaxRank || Embers < perk.Costs[rank]) return false;
        Embers -= perk.Costs[rank];
        PerkRanks[perk.Id] = rank + 1;
        if (rank == 0 && ClassPerks.EquippedCount(perk.Hero) < Tune.Perks.Slots) PerksEquipped.Add(perk.Id);
        Save();
        return true;
    }

    /// <summary>Brings a bought perk on runs, or leaves it at the camp. False if the slots are full.</summary>
    public static bool PerkToggle(ClassPerk perk)
    {
        if (PerkRank(perk.Id) <= 0) return false;
        if (PerksEquipped.Remove(perk.Id)) { Save(); return true; }
        if (ClassPerks.EquippedCount(perk.Hero) >= Tune.Perks.Slots) return false;
        PerksEquipped.Add(perk.Id);
        Save();
        return true;
    }

    public static readonly MetaTree PotionTree = new() { Id = "potion", Name = "THE POTION", Resource = "Reagent", ResourcePlural = "Reagents", Color = new Color(1f, 0.4f, 0.55f) };
    public static readonly MetaTree PearlTree = new() { Id = "pearl", Name = "THE PATH", Resource = "Obsidian Pearl", ResourcePlural = "Obsidian Pearls", Color = new Color(0.7f, 0.6f, 1f) };
    public static IEnumerable<MetaTree> Trees => new[] { PotionTree, PearlTree };

    static Meta()
    {
        void B(MetaTree t, string branch, string name, params (string n, string d)[] ranks)
        {
            t.Branches.Add((branch, name));
            for (int k = 0; k < ranks.Length; k++)
                t.Tiers.Add(new MetaTier { Id = $"{branch}{k + 1}", Branch = branch, Name = ranks[k].n, Desc = ranks[k].d, Rank = k + 1, Cost = k + 1 });
        }
        B(PotionTree, "heal", "Immediate heal", ("Quick Draught I", "Potions heal 5% more at once (20%)."), ("Quick Draught II", "Potions heal 5% more at once (25%)."), ("Quick Draught III", "Potions heal 5% more at once (30%)."));
        B(PotionTree, "hot", "Heal over time", ("Lingering I", "Potions heal 5% more over time (20%)."), ("Lingering II", "Potions heal 5% more over time (25%)."), ("Lingering III", "Potions heal 5% more over time (30%)."));
        B(PotionTree, "speed", "Heal speed", ("Strong Brew I", "The heal over time takes 3 s less (17 s)."), ("Strong Brew II", "The heal over time takes 3 s less (14 s)."), ("Strong Brew III", "The heal over time takes 4 s less (10 s)."));
        B(PotionTree, "drop", "Drop rate", ("Forager", "Enemies drop potions 1% more often (2%)."), ("Desperate Forager", "Another +1% while you carry no potion."));
        B(PotionTree, "max", "Potion belt", ("Second Flask", "Carry up to 2 potions."), ("Third Flask", "Carry up to 3 potions."));
        B(PearlTree, "mile", "Milestones", ("Waypoints I", "A milestone pick every 7 levels."), ("Waypoints II", "A milestone pick every 6 levels."), ("Waypoints III", "A milestone pick every 5 levels."));
        B(PearlTree, "xp", "Experience", ("Studious I", "+5% experience."), ("Studious II", "+7% more experience (12%)."), ("Studious III", "+8% more experience (20%)."));
        B(PearlTree, "exp", "Elite experience", ("Trophies I", "+5% experience from elites."), ("Trophies II", "+7% more from elites (12%)."), ("Trophies III", "+8% more from elites (20%)."));
        Load();
    }

    public static MetaTree TreeOf(string tierId) => Trees.First(t => t.Tiers.Any(x => x.Id == tierId));
    public static int HeldOf(MetaTree t) => Held.TryGetValue(t.Id, out var n) ? n : 0;
    public static int FoundOf(MetaTree t) => Found.TryGetValue(t.Id, out var n) ? n : 0;
    public static bool Visible(MetaTree t) => FoundOf(t) > 0;
    public static int Rank(string branch) { int n = 0; while (Active.Contains($"{branch}{n + 1}")) n++; return n; }

    // ---- effects
    public static float PotionHealNow => 0.15f + 0.05f * Rank("heal");
    public static float PotionHealOverTime => 0.15f + 0.05f * Rank("hot");
    public static float PotionHotSeconds => 20f - new[] { 0f, 3f, 6f, 10f }[Rank("speed")];
    public static int MaxPotions => 1 + Rank("max");
    public static int MilestoneEvery => 8 - Rank("mile");
    public static float XpMult => 1f + new[] { 0f, 0.05f, 0.12f, 0.2f }[Rank("xp")];
    public static float EliteXpMult => 1f + new[] { 0f, 0.05f, 0.12f, 0.2f }[Rank("exp")];
    public static float PotionDropChance(Player p)
    {
        float c = Tune.Drops.PotionChance;
        if (Active.Contains("drop1")) c += 0.01f;
        if (Active.Contains("drop2") && p != null && p.Potions == 0) c += 0.01f;
        return c;
    }

    // ---- buying and activating
    /// <summary>The next rank to buy in a branch (null when all are bought).</summary>
    public static MetaTier NextToBuy(MetaTree t, string branch) => t.Branch(branch).FirstOrDefault(x => !Bought.Contains(x.Id));
    /// <summary>The first bought rank still waiting for a resource.</summary>
    public static MetaTier NextToActivate(MetaTree t, string branch) => t.Branch(branch).FirstOrDefault(x => Bought.Contains(x.Id) && !Active.Contains(x.Id));

    public static bool Buy(MetaTier tier, bool free = false)
    {
        if (Bought.Contains(tier.Id) || (!free && Embers < tier.Cost)) return false;
        if (!free) Embers -= tier.Cost;
        Bought.Add(tier.Id);
        Save();
        return true;
    }

    public static bool Activate(MetaTier tier)
    {
        var t = TreeOf(tier.Id);
        if (!Bought.Contains(tier.Id) || Active.Contains(tier.Id) || HeldOf(t) <= 0) return false;
        Held[t.Id] = HeldOf(t) - 1;
        Active.Add(tier.Id);
        Save();
        return true;
    }

    /// <summary>
    /// A guardian's resource: one drawn evenly from every resource not yet found (so each tree
    /// is picked in proportion to what's left of it). Null once everything has been found.
    /// </summary>
    public static MetaTree RollResource(Random rng)
    {
        int left = Trees.Sum(t => t.MaxResource - FoundOf(t));
        if (left <= 0) return null;
        int r = rng.Next(left);
        foreach (var t in Trees)
        {
            int n = t.MaxResource - FoundOf(t);
            if (r < n)
            {
                Found[t.Id] = FoundOf(t) + 1;
                Held[t.Id] = HeldOf(t) + 1;
                Save();
                return t;
            }
            r -= n;
        }
        return null;
    }

    public static void AddEmbers(int n) { Embers += n; Save(); }

    // ---- persistence
    private const string Path = "user://meta.json";

    public static void Save()
    {
        if (G.NoSave) return;
        var d = ToDict();
        try
        {
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
            f?.StoreString(Json.Stringify(d, "  "));
        }
        catch (Exception e) { GD.PrintErr($"[meta] save failed: {e.Message}"); }
    }

    public static void Load()
    {
        if (G.NoSave || !FileAccess.FileExists(Path)) return;
        try { FromDict(Json.ParseString(FileAccess.GetFileAsString(Path)).AsGodotDictionary(), true); }
        catch (Exception e) { GD.PrintErr($"[meta] load failed: {e.Message}"); }
    }

    /// <summary>Everything kept between runs, as it stands (taken as a run begins).</summary>
    public static Godot.Collections.Dictionary Snapshot() => ToDict();

    /// <summary>
    /// Puts everything kept between runs back as it was in a snapshot and saves it: a run
    /// abandoned at the cave mouth keeps nothing. (The tutorials already seen stay seen.)
    /// </summary>
    public static void Restore(Godot.Collections.Dictionary d)
    {
        if (d == null) return;
        FromDict(d, false);
        Save();
    }

    private static Godot.Collections.Dictionary ToDict()
    {
        var d = new Godot.Collections.Dictionary
        {
            ["embers"] = Embers, ["runs"] = Runs, ["victories"] = Victories, ["best_depth"] = BestDepth,
            ["potion_tut"] = PotionTutorialDone, ["pearl_tut"] = PearlTutorialDone,
            ["bought"] = new Godot.Collections.Array(Bought.Select(x => (Variant)x)),
            ["active"] = new Godot.Collections.Array(Active.Select(x => (Variant)x)),
            ["perk_equipped"] = new Godot.Collections.Array(PerksEquipped.Select(x => (Variant)x)),
        };
        var pr = new Godot.Collections.Dictionary();
        foreach (var kv in PerkRanks) pr[kv.Key] = kv.Value;
        d["perk_ranks"] = pr;
        foreach (var t in Trees) { d["held_" + t.Id] = HeldOf(t); d["found_" + t.Id] = FoundOf(t); }
        return d;
    }

    private static void FromDict(Godot.Collections.Dictionary d, bool tutorials)
    {
        int I(string k) => d.ContainsKey(k) ? (int)d[k] : 0;
        Embers = I("embers"); Runs = I("runs"); Victories = I("victories"); BestDepth = I("best_depth");
        if (tutorials)
        {
            PotionTutorialDone = d.ContainsKey("potion_tut") && (bool)d["potion_tut"];
            PearlTutorialDone = d.ContainsKey("pearl_tut") && (bool)d["pearl_tut"];
        }
        Bought.Clear(); Active.Clear();
        if (d.ContainsKey("bought")) foreach (var v in d["bought"].AsGodotArray()) Bought.Add((string)v);
        if (d.ContainsKey("active")) foreach (var v in d["active"].AsGodotArray()) Active.Add((string)v);
        foreach (var t in Trees) { Held[t.Id] = I("held_" + t.Id); Found[t.Id] = I("found_" + t.Id); }
        PerkRanks.Clear(); PerksEquipped.Clear();
        if (d.ContainsKey("perk_ranks")) foreach (var kv in d["perk_ranks"].AsGodotDictionary()) PerkRanks[(string)kv.Key] = (int)kv.Value;
        if (d.ContainsKey("perk_equipped")) foreach (var v in d["perk_equipped"].AsGodotArray()) PerksEquipped.Add((string)v);
    }
}
