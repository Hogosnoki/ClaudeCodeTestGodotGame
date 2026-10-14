using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>Progress achievements mark the way through the game (party-wide: everyone in the party earns them together; visible, and what the
/// in-game completion counts); feats are deeds (each player's own unless they suit the party; hidden until earned, and kept out of the completion).</summary>
public enum AchKind { Progress, Feat }

public sealed class AchDef
{
    public string Id, Name, Desc;
    public AchKind Kind;
    /// <summary>Earned by the whole party at once (a progress achievement, or a feat that suits a party).</summary>
    public bool Party;
    /// <summary>Hidden until earned (Steam's "hidden" flag): every feat.</summary>
    public bool Hidden => Kind == AchKind.Feat;
    /// <summary>The name Steamworks will know it by.</summary>
    public string ApiName => "ACH_" + Id.ToUpperInvariant();
}

/// <summary>
/// The achievements: what they are, which are earned (kept in user://achievements.json, apart from the run's save), the lifetime record
/// they are judged from (guardians slain, caves entered, the deepest depth, side-grades taken), and the run's and the level's tallies for
/// the feats. The game tells it what happens (see the hooks named in each rule); it unlocks, saves and raises <see cref="Unlocked"/> for the
/// notification. Steam comes later: <see cref="ToSteam"/> is where an unlock will be passed on.
/// </summary>
public static class Achievements
{
    /// <summary>The guardians, as their titles are shown (the list "Conqueror" is judged against: the launch's 21; new ones get their own achievement).</summary>
    public static readonly string[] Guardians =
    {
        "THE WEB-MOTHER", "THE DEN MOTHER", "THE GOBLIN WARCHIEF", "THE ROTBACK", "THE BROOD QUEEN", "THE BONE KNIGHT", "THE ORE GOLEM",
        "THE CRYPT LORD", "THE MOSSBACK HULK", "THE TUNNEL BRUTE", "THE BARROW KING", "THE CAVERN COLOSSUS", "THE RIME COLOSSUS",
        "THE OSSUARY COLOSSUS", "THE PRISM GOLEM", "THE BASALT WARDEN", "THE VENT COLOSSUS", "THE ENGINE-WARDEN", "THE ECHO QUEEN",
        "THE MOLTEN COLOSSUS", "THE ELDER DRAGON",
    };
    /// <summary>The heroes found in cages (the first three are everyone's from the start).</summary>
    private static readonly HeroKind[] Cagable = { HeroKind.Elementalist, HeroKind.Rogue, HeroKind.Aegis, HeroKind.ShapeShifter, HeroKind.Automaton };

    public static readonly List<AchDef> All = new();
    private static readonly Dictionary<string, AchDef> ById = new();

    // ---- what is earned, and the lifetime record
    public static readonly Dictionary<string, long> Earned = new();
    public static readonly HashSet<string> BossesSlain = new(), BiomesEntered = new(), SideGrades = new();
    public static int DeepestDepth;
    /// <summary>Raised when one is earned (the notification listens).</summary>
    public static event Action<AchDef> Unlocked;

    private static string Key(string title) => new string(title.ToLowerInvariant().Replace("the ", "").Where(char.IsLetter).ToArray());
    private static string Proper(string title) => string.Join(" ", title.ToLowerInvariant().Split(' ').Select(w => string.Join("-", w.Split('-').Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p))));
    private static string HeroName(HeroKind h) => h == HeroKind.ShapeShifter ? "Shape Shifter" : h.ToString();

    private static void P(string id, string name, string desc) => Add(new AchDef { Id = id, Name = name, Desc = desc, Kind = AchKind.Progress, Party = true });
    private static void F(string id, string name, string desc, bool party = false) => Add(new AchDef { Id = id, Name = name, Desc = desc, Kind = AchKind.Feat, Party = party });
    private static void Add(AchDef d) { All.Add(d); ById[d.Id] = d; }

    static Achievements()
    {
        // ---------------------------------------------------------------- progress (party-wide, in the order Steam will list them)
        for (int d = 1; d <= 11; d++)
            P($"depth_{d}", $"Advance to Depth {d}!", d < 11 ? $"Reach depth {d} for the first time." : "Reach depth 11, one level past the dragon's usual (a Deeper Dark).");
        foreach (var t in Guardians) P($"boss_{Key(t)}", $"Defeat {Proper(t)}!", $"Defeat {Proper(t)} for the first time.");
        P("conqueror", "Conqueror!", "Defeat every guardian (all 21).");
        P("explorer", "Explorer!", "Enter every cave a run can take you through.");
        P("sunken_sea", "Deep Sea Explorer", "Discover the Sunken Sea.");
        P("into_null", "Into the Null", "Find your way into the Null.");
        foreach (var h in Cagable) P($"freed_{h.ToString().ToLowerInvariant()}", $"Freed the {HeroName(h)}!", $"Free the {HeroName(h)} from a cage.");
        P("freed_merchant", "Freed the Merchant!", "Free the merchant from his cage.");
        P("all_accounted", "All Accounted For!", "Free every hero and the merchant.");
        P("victory", "Victory!", "Complete a run: slay the dragon.");
        foreach (var h in Enum.GetValues<HeroKind>()) P($"victory_{h.ToString().ToLowerInvariant()}", $"Victory as the {HeroName(h)}!", $"Complete a run as the {HeroName(h)}.");
        P("long_way", "The Long Way Down", "Slay the dragon at depth 11.");
        foreach (var h in Enum.GetValues<HeroKind>()) P($"competent_{h.ToString().ToLowerInvariant()}", $"Competent {HeroName(h)}!", $"Take every one of the {HeroName(h)}'s side-grades.");
        P("first_vault", "Vault Breaker", "Open a vault.");
        P("first_trade", "Generous", "Give a relic to a friend at the trading stand (or be given one).");
        P("shared_collection", "Fair Shares", "Empty a Relic Collection: every relic in it taken.");
        P("full_journal", "Lore Keeper", "Find every journal page.");
        P("master_path", "Master of the Path", "Buy every rank of the Path.");
        P("master_potion", "Master of the Potion", "Buy every rank of the Potion.");

        // ---------------------------------------------------------------- feats (hidden; each player's own unless marked for the party)
        F("power_hungry", "Power Hungry!", "Open every upgrade shrine in a depth (the level's, the mini-bosses', the hive's and the guardian's).", party: true);
        F("looter", "Looter!", "Open every chest in a depth: relic chests, collections, vaults and the hidden ones.", party: true);
        F("ransacked", "Ransacked!", "Open every shrine and every chest in a depth.", party: true);
        F("obliterated", "Obliterated!", "Fall to a single blow from full health.");
        F("unbreakable", "Too Stubborn to Die", "Survive a blow that exceeds your maximum health.");
        F("savior", "Savior", "Revive your friends 10 times in a single run.");
        F("burden", "Burden", "Be revived 10 times in a single run.");
        F("fish_kin", "Fish-Kin", "Stay under water for 2 minutes on the rising bubbles' air alone (not as a creature, nor in a bubble, nor without lungs).");
        F("not_fair", "Not Fair", "Be felled by the Glitch.");
        F("horrors", "Indescribable Horrors!", "Defeat the Glitch.", party: true);
        F("untouchable", "Untouchable!", "Take no damage at all for a whole depth (chip through a shield counts).");
        F("undefeatable", "Undefeatable!", "Complete a run in which nobody in the party fell.", party: true);
        F("heavy_hitter", "Devastating Blow!", "Deal 500 damage in a single strike.");
        F("double_speed", "Fleet of Foot", "Reach double your hero's movement speed.");
        F("max_fall", "Hard Landing", "Take the most a fall can take.");
        F("chain_reaction", "Chain Reaction", "Shatter 5 or more creatures with a single snap.");
        F("glass_cannon", "Glass Cannon", "Complete a run carrying both Glass Edge and Heavy Hand.");
        F("hoarder", "Hoarder", "Carry 300 gold at once.");
        F("minimalist", "Minimalist", "Slay the dragon having taken no more than 5 upgrades.");
        F("tight_squeeze", "Tight Squeeze", "As the Shape Shifter, slip into a hidden chamber only a creature can reach.");
        F("spike_dancer", "Spike-Dancer", "Defeat the Barrow King without it ever striking you.");
        F("brawler", "Brawler", "Defeat the Goblin Warchief with nobody in the party falling during the fight.", party: true);
    }

    public static AchDef Find(string id) => ById.TryGetValue(id, out var d) ? d : null;
    public static bool Has(string id) => Earned.ContainsKey(id);
    /// <summary>The in-game completion: progress achievements only (feats are kept apart).</summary>
    public static (int got, int of) Completion(AchKind k) => (All.Count(d => d.Kind == k && Has(d.Id)), All.Count(d => d.Kind == k));

    /// <summary>Earns one (once): saved, passed to Steam (later), and announced unless <paramref name="quiet"/>.</summary>
    public static bool Unlock(string id, bool quiet = false)
    {
        var d = Find(id);
        if (d == null || Earned.ContainsKey(id)) return false;
        Earned[id] = (long)Time.GetUnixTimeFromSystem();
        SessionLog.Note($"achievement: {d.Name}");
        ToSteam(d);
        if (!quiet) Unlocked?.Invoke(d);
        Save();
        return true;
    }

    /// <summary>Where an unlock will reach Steam (SteamUserStats.SetAchievement + StoreStats) once the game is set up there.</summary>
    private static void ToSteam(AchDef d) { }

    // ================================================================== the run's and the level's tallies (for the feats)

    public static class Run
    {
        public static int RevivesGiven, RevivesTaken, UpgradesTaken;
        public static bool AnyFell;
        public static void Reset() { RevivesGiven = RevivesTaken = UpgradesTaken = 0; AnyFell = false; }
    }

    public static class Level
    {
        public static bool TookDamage, BarrowStruckMe, FightStarted, FightFell;
        public static float UnderWater;
        public static readonly List<Room> Nooks = new();
        public static void Reset() { TookDamage = BarrowStruckMe = FightStarted = FightFell = false; UnderWater = 0f; Nooks.Clear(); }
    }

    // ================================================================== hooks

    /// <summary>A new run begins (Main.BuildLevel, the first level).</summary>
    public static void NewRun() => Run.Reset();

    /// <summary>A level was built (every game): the depth reached, the cave entered.</summary>
    public static void EnteredLevel(int depth, BiomeDef biome, CaveData cave, bool shop)
    {
        Level.Reset();
        if (biome == null) return;
        if (cave != null)
            foreach (var r in cave.Rooms)
                if (r.Kind == RoomKind.Secret && cave.Hints.Any(h => h.Kind is 0 or 1 && h.Pos.DistanceTo(r.Center) < 30 * CaveData.Cell)) Level.Nooks.Add(r);
        if (shop) return;
        bool changed = false;
        if (depth > DeepestDepth) { DeepestDepth = depth; changed = true; }
        for (int d = 1; d <= Math.Min(11, depth); d++) Unlock($"depth_{d}");
        if (BiomesEntered.Add(biome.Id.ToString())) changed = true;
        if (biome.Id == BiomeId.Abyss) Unlock("sunken_sea");
        if (biome.Id == BiomeId.Null) Unlock("into_null");
        if (Biomes.All.Where(b => b.Id is not (BiomeId.Abyss or BiomeId.Null)).All(b => BiomesEntered.Contains(b.Id.ToString()))) Unlock("explorer");
        if (changed) Save();
    }

    /// <summary>The party goes on from a level (Main.GoDeeper, every game): the feats a whole depth earns.</summary>
    public static void LeavingLevel(int depth, BiomeDef biome, bool shop, bool guardianDown, Player me)
    {
        if (shop || biome == null || depth < 1 || !guardianDown) return;
        if (me != null && GodotObject.IsInstanceValid(me) && !me.Dead && !Level.TookDamage) Unlock("untouchable");
        if (biome.Id == BiomeId.Lair) return;
        var chests = Chest.All.Where(GodotObject.IsInstanceValid).ToList();
        var shrines = chests.Where(c => c.IsShrine).ToList();
        var others = chests.Where(c => !c.IsShrine).ToList();
        bool allShrines = shrines.Count > 0 && shrines.All(c => c.Open), allChests = others.Count > 0 && others.All(c => c.Open);
        if (allShrines) Unlock("power_hungry");
        if (allChests) Unlock("looter");
        if (allShrines && allChests) Unlock("ransacked");
    }

    /// <summary>A guardian fell (Main.GuardianRewards, every game).</summary>
    public static void GuardianSlain(string title, bool dragon, Vector2 at, Player me)
    {
        if (string.IsNullOrEmpty(title)) return;
        if (Array.IndexOf(Guardians, title) >= 0)
        {
            if (BossesSlain.Add(title)) Save();
            Unlock($"boss_{Key(title)}");
            if (Guardians.All(BossesSlain.Contains)) Unlock("conqueror");
        }
        bool near = me != null && GodotObject.IsInstanceValid(me) && !me.Dead && me.GlobalPosition.DistanceTo(at) < 700f;
        if (title == "THE BARROW KING" && near && !Level.BarrowStruckMe) Unlock("spike_dancer");
        if (title == "THE GOBLIN WARCHIEF" && !Level.FightFell) Unlock("brawler");
        if (dragon) Victory(me);
    }

    /// <summary>The run is won (the dragon fell).</summary>
    private static void Victory(Player me)
    {
        Unlock("victory");
        if (me != null && GodotObject.IsInstanceValid(me))
        {
            Unlock($"victory_{me.Hero.ToString().ToLowerInvariant()}");
            if (me.Stats.StackOf("rr_glass") > 0 && me.Stats.StackOf("rr_heavy") > 0) Unlock("glass_cannon");
        }
        if (G.Depth >= 11) Unlock("long_way");
        if (!Run.AnyFell) Unlock("undefeatable");
        if (Run.UpgradesTaken <= 5) Unlock("minimalist");
    }

    /// <summary>A hero or the merchant was freed for this player (Meta.Unlock / Meta.UnlockMerchant).</summary>
    public static void Freed(HeroKind? hero)
    {
        Unlock(hero is HeroKind h ? $"freed_{h.ToString().ToLowerInvariant()}" : "freed_merchant");
        CheckState();
    }

    /// <summary>This player took a card (Upgrades.Apply): side-grades are remembered for "Competent", and upgrades counted for "Minimalist".</summary>
    public static void CardTaken(Upgrade u, HeroKind hero)
    {
        if (!u.Relic) Run.UpgradesTaken++;
        if (u.Alteration && SideGrades.Add($"{hero}:{u.Id}")) { Save(); CheckCompetent(hero); }
    }

    private static void CheckCompetent(HeroKind hero)
    {
        var alts = Upgrades.Chest.Where(u => u.Alteration && u.For != null && u.For.Contains(hero)).ToList();
        if (alts.Count > 0 && alts.All(u => SideGrades.Contains($"{hero}:{u.Id}"))) Unlock($"competent_{hero.ToString().ToLowerInvariant()}");
    }

    /// <summary>A chest was emptied (Chest.OpenBy, every game).</summary>
    public static void ChestOpened(Chest c)
    {
        if (c.Vault && !c.Corrupt) Unlock("first_vault");
        if (c.Collection) Unlock("shared_collection");
    }

    /// <summary>A relic changed hands at the trading stand (the giver's and the friend's games).</summary>
    public static void Traded() => Unlock("first_trade");

    /// <summary>This player's hero took harm (Player.TakeRawDamage): "Untouchable" is lost, and a blow may be a one-shot or one survived.</summary>
    public static void TookDamage(float dmg, float hpBefore, float maxHp, bool lethal, bool fell, string kind)
    {
        if (dmg <= 0f) return;
        Level.TookDamage = true;
        if (kind == "dot") return;
        if (lethal && fell && hpBefore >= maxHp - 0.01f) Unlock("obliterated");
        if (!fell && dmg > maxHp) Unlock("unbreakable");
    }

    public static void FellTo(Enemy source) { if (source is Glitch) Unlock("not_fair"); }
    public static void StruckBy(Enemy source) { if (source is Skeleton && source.IsGuardian && source.Title == "THE BARROW KING") Level.BarrowStruckMe = true; }
    public static void FellHard() => Unlock("max_fall");
    public static void Dealt(float dmg) { if (dmg >= 500f) Unlock("heavy_hitter"); }
    public static void Snapped(int burst) { if (burst >= 5) Unlock("chain_reaction"); }
    public static void GlitchDefeated() => Unlock("horrors");
    public static void RevivedAFriend() { if (++Run.RevivesGiven >= 10) Unlock("savior"); }
    public static void WasRevived() { if (++Run.RevivesTaken >= 10) Unlock("burden"); }

    /// <summary>Every frame of play (Main): the feats that build up over time, and the party's falls.</summary>
    public static void Tick(float dt, Player me, Enemy activeBoss)
    {
        foreach (var h in G.Players)
            if (GodotObject.IsInstanceValid(h) && h.Dead) { Run.AnyFell = true; if (Level.FightStarted) Level.FightFell = true; }
        if (activeBoss != null && GodotObject.IsInstanceValid(activeBoss) && !Level.FightStarted) { Level.FightStarted = true; Level.FightFell = G.Players.Any(h => GodotObject.IsInstanceValid(h) && h.Dead); }
        if (me == null || !GodotObject.IsInstanceValid(me) || me.Dead) { Level.UnderWater = 0f; return; }
        var s = me.Stats;
        // Fish-Kin: under water on the bubbles' air alone (not as a creature, nor in a bubble, nor without lungs to fill)
        bool breathing = me.InWater && me.HeadUnder && me.Breath > 0.01f && !s.InfiniteBreath && !s.Breathless && !me.Shifted && !me.Bubbled;
        Level.UnderWater = breathing ? Level.UnderWater + dt : 0f;
        if (Level.UnderWater >= 120f) Unlock("fish_kin");
        if (s.MoveSpeed >= 2f * BaseMove(me.Hero) - 1e-4f) Unlock("double_speed");
        if (me.Gold >= 300) Unlock("hoarder");
        if (me.Hero == HeroKind.ShapeShifter && me.Shifted)
            foreach (var r in Level.Nooks)
                if (Math.Abs(me.GlobalPosition.X - r.Center.X) < r.RxPx + 8f && Math.Abs(me.GlobalPosition.Y - r.Center.Y) < r.RyPx + 8f) { Unlock("tight_squeeze"); break; }
    }

    private static readonly Dictionary<HeroKind, float> _baseMove = new();
    private static float BaseMove(HeroKind h) => _baseMove.TryGetValue(h, out var v) ? v : _baseMove[h] = new PlayerStats(h).MoveSpeed;

    /// <summary>What can be judged from what is kept (freed heroes, the journal, the trees, side-grades): at start-up, quietly, and after each change.</summary>
    public static void CheckState(bool quiet = false)
    {
        foreach (var h in Cagable) if (Meta.IsReallyUnlocked(h)) Unlock($"freed_{h.ToString().ToLowerInvariant()}", quiet);
        if (Meta.MerchantUnlocked) Unlock("freed_merchant", quiet);
        if (Cagable.All(Meta.IsReallyUnlocked) && Meta.MerchantUnlocked) Unlock("all_accounted", quiet);
        if (Lore.Journal.Select(p => p.biome.ToString()).Distinct().All(Meta.JournalFound.Contains)) Unlock("full_journal", quiet);
        if (Meta.PearlTree.Tiers.All(t => Meta.Bought.Contains(t.Id))) Unlock("master_path", quiet);
        if (Meta.PotionTree.Tiers.All(t => Meta.Bought.Contains(t.Id))) Unlock("master_potion", quiet);
        for (int d = 1; d <= Math.Min(11, DeepestDepth); d++) Unlock($"depth_{d}", quiet);
        foreach (var t in BossesSlain) Unlock($"boss_{Key(t)}", quiet);
        if (Guardians.All(BossesSlain.Contains)) Unlock("conqueror", quiet);
        foreach (var h in Enum.GetValues<HeroKind>())
        {
            var alts = Upgrades.Chest.Where(u => u.Alteration && u.For != null && u.For.Contains(h)).ToList();
            if (alts.Count > 0 && alts.All(u => SideGrades.Contains($"{h}:{u.Id}"))) Unlock($"competent_{h.ToString().ToLowerInvariant()}", quiet);
        }
    }

    // ================================================================== saved

    private const string Path = "user://achievements.json";

    public static string ToJson()
    {
        var earned = new Godot.Collections.Dictionary();
        foreach (var kv in Earned) earned[kv.Key] = kv.Value;
        var d = new Godot.Collections.Dictionary
        {
            ["earned"] = earned,
            ["bosses"] = new Godot.Collections.Array(BossesSlain.Select(x => (Variant)x)),
            ["biomes"] = new Godot.Collections.Array(BiomesEntered.Select(x => (Variant)x)),
            ["sidegrades"] = new Godot.Collections.Array(SideGrades.Select(x => (Variant)x)),
            ["deepest"] = DeepestDepth,
        };
        return Json.Stringify(d);
    }

    public static void FromJson(string text)
    {
        Earned.Clear(); BossesSlain.Clear(); BiomesEntered.Clear(); SideGrades.Clear(); DeepestDepth = 0;
        if (string.IsNullOrEmpty(text)) return;
        var v = Json.ParseString(text);
        if (v.VariantType != Variant.Type.Dictionary) return;
        var d = v.AsGodotDictionary();
        if (d.ContainsKey("earned")) foreach (var kv in d["earned"].AsGodotDictionary()) Earned[(string)kv.Key] = (long)kv.Value;
        if (d.ContainsKey("bosses")) foreach (var x in d["bosses"].AsGodotArray()) BossesSlain.Add((string)x);
        if (d.ContainsKey("biomes")) foreach (var x in d["biomes"].AsGodotArray()) BiomesEntered.Add((string)x);
        if (d.ContainsKey("sidegrades")) foreach (var x in d["sidegrades"].AsGodotArray()) SideGrades.Add((string)x);
        if (d.ContainsKey("deepest")) DeepestDepth = (int)d["deepest"];
    }

    public static void Save()
    {
        if (G.NoSave) return;
        try { using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write); f?.StoreString(ToJson()); }
        catch (Exception e) { GD.PrintErr(e); }
    }

    public static void Load()
    {
        if (G.NoSave) { FromJson(""); return; }
        FromJson(FileAccess.FileExists(Path) ? FileAccess.GetFileAsString(Path) : "");
        CheckState(quiet: true);
    }

    /// <summary>`--dumpachievements=PATH`: the list in Steamworks' order (progress first, then the hidden feats), for setting them up there.</summary>
    public static string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Dagger Deep: achievements (Steamworks order)\n");
        sb.AppendLine("Progress achievements are party-wide and visible; feats are hidden and each player's own unless marked *(party)*.");
        sb.AppendLine("Steam's own completion count includes every achievement; the game's counts progress only.\n");
        foreach (var k in new[] { AchKind.Progress, AchKind.Feat })
        {
            sb.AppendLine(k == AchKind.Progress ? $"## Progress ({All.Count(d => d.Kind == k)})\n" : $"## Feats ({All.Count(d => d.Kind == k)}, hidden)\n");
            sb.AppendLine("| API name | Name | Description |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (var d in All.Where(d => d.Kind == k))
                sb.AppendLine($"| `{d.ApiName}` | {d.Name}{(d.Kind == AchKind.Feat && d.Party ? " *(party)*" : "")} | {d.Desc} |");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
