using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// What carries over between runs, saved to user://meta.json: embers (the currency), the Ember Grid's hexes and materials
/// (see <see cref="HexGrid"/>: every lasting advancement is bought there), the heroes and the merchant freed, the loadouts,
/// the journal, and a few counts.
/// </summary>
public static class Meta
{
    public static int Embers;
    public static int Runs, Victories, BestDepth;
    /// <summary>The Ember Grid has been explained (the first time it opens).</summary>
    public static bool GridTutorialDone;
    /// <summary>The story card has been shown (the first time a run is begun).</summary>
    public static bool StorySeen;
    /// <summary>The Delvers' journal pages found at lamps (by the kind of level: BiomeId names).</summary>
    public static readonly HashSet<string> JournalFound = new();
    /// <summary>A page read at a lamp: true the first time (it is kept, and paid in an ember).</summary>
    public static bool FindPage(BiomeId id)
    {
        if (!JournalFound.Add(id.ToString())) return false;
        Embers += 1;
        Save();
        Achievements.CheckState();
        return true;
    }
    static Meta() { Load(); }

    // ---- effects (read from the Ember Grid's hexes)
    public static float PotionHealNow => 0.15f * (1f + 0.1f * HexGrid.Count("pot_quick") + (HexGrid.Owns("pot_root") ? 0.05f : 0f)) * (HexGrid.Owns("pot_volatile") ? 0.85f : 1f);
    public static float PotionHealOverTime => 0.15f * (1f + 0.1f * HexGrid.Count("pot_linger")) * (HexGrid.Owns("pot_volatile") ? 0.85f : 1f);
    public static float PotionHotSeconds => 15f - new[] { 0f, 2f, 4f, 7f }[HexGrid.Count("pot_brew")];
    /// <summary>One flask, two with the Second Flask hex (a third only from a Deep Flask found on the way).</summary>
    public static int MaxPotions => 1 + (HexGrid.Owns("pot_flask") ? 1 : 0);
    /// <summary>What the merchant asks (x), what gold found is worth (x) and the choices of cards that may be dealt again in a run.</summary>
    public static float ShopPriceMult => 1f - 0.05f * HexGrid.Count("shop_price");
    public static float GoldFoundMult => 1f + 0.1f * HexGrid.Count("fort_gold");
    public static int RerollCharges => HexGrid.Count("fort_reroll");
    /// <summary>Relic trades with friends at the merchant's stand a run: one, and one or two more from the grid.</summary>
    public static int Trades => Tune.Merchant.Trades + HexGrid.Count("shop_trade");
    /// <summary>Embers a guardian gives (x): the Ember Pact.</summary>
    public static float EmberMult => HexGrid.Owns("fort_pact") ? 1.4f : 1f;
    // ---- heroes: three at the start; the others are found (caged in the caves) or won
    public static readonly HashSet<HeroKind> Unlocked = new() { HeroKind.Swordsman, HeroKind.Warden, HeroKind.Vitalist };
    // ---- the merchant: not a hero, but met like one (caged in the caves); once freed, a stall of his is offered after every guardian
    /// <summary>The merchant has been freed (kept between runs).</summary>
    public static bool MerchantUnlocked;
    /// <summary>Whether his stall is offered (everything is, while testing).</summary>
    public static bool MerchantKnown => Tune.Testing.UnlockEverything || MerchantUnlocked;
    /// <summary>Still to be found by someone in the party (so a cage may hold him): every game works it out alike (online, from what each player said they had).</summary>
    public static bool MerchantWanted()
    {
        if (Net.Online && Net.Count > 1) { foreach (var p in Net.Peers.Values) if ((p.Locked & Net.MerchantBit) != 0) return true; return false; }
        return !MerchantUnlocked;
    }
    public static bool UnlockMerchant()
    {
        if (MerchantUnlocked) return false;
        MerchantUnlocked = true;
        Save();
        Achievements.Freed(null);
        return true;
    }

    /// <summary>Heroes unlocked in the run now under way (the dragon's award only goes to a run that found none).</summary>
    public static int RunUnlocks;
    /// <summary>Whether a hero can be chosen (everything is, while testing).</summary>
    public static bool IsUnlocked(HeroKind h) => Tune.Testing.UnlockEverything || Unlocked.Contains(h);
    /// <summary>Whether a hero really is unlocked (cages and the lobby's lists go by this, not by the testing switch).</summary>
    public static bool IsReallyUnlocked(HeroKind h) => Unlocked.Contains(h);

    /// <summary>The side-grades each hero brings on a run (up to Tune.Loadout.Slots), by upgrade id.</summary>
    public static readonly Dictionary<HeroKind, HashSet<string>> Loadouts = new();
    public static HashSet<string> LoadoutFor(HeroKind h) => Loadouts.TryGetValue(h, out var s) ? s : Loadouts[h] = new HashSet<string>();
    /// <summary>A side-grade can be brought once its hex on the hero's branch is owned.</summary>
    public static bool SideGradeUnlocked(string id) => HexGrid.SideGradeOwned(id);
    public static void LoadoutToggle(HeroKind h, string id)
    {
        var set = LoadoutFor(h);
        if (!set.Remove(id) && set.Count < Tune.Loadout.Slots) set.Add(id);
        Save();
    }

    /// <summary>
    /// Which hero a cage holds: every locked hero counts once for each player (this one included)
    /// who still has them locked, and a roll over the total picks one, so a hero most of the party
    /// is missing is the likeliest. Null if nobody lacks anyone. (<paramref name="roll"/> is 0..1.)
    /// </summary>
    public static HeroKind? PickCageHero(float roll)
    {
        var kinds = Enum.GetValues<HeroKind>();
        var count = new int[kinds.Length];
        int sum = 0;
        if (Net.Online && Net.Count > 1)
        {
            foreach (var p in Net.Peers.Values)
                foreach (var h in kinds) if ((p.Locked & (1 << (int)h)) != 0) { count[(int)h]++; sum++; }
        }
        else foreach (var h in kinds) if (!IsReallyUnlocked(h)) { count[(int)h]++; sum++; }
        // (the Automaton is never caged: he lies disabled in the Clockwork Deep, waiting on a power cell)
        sum -= count[(int)HeroKind.Automaton]; count[(int)HeroKind.Automaton] = 0;
        if (sum == 0) return null;
        float v = roll * sum;
        for (int i = 0; i < kinds.Length; i++) { v -= count[i]; if (count[i] > 0 && v <= 0) return kinds[i]; }
        return kinds.Last(h => count[(int)h] > 0);
    }
    /// <summary>Is the Automaton still locked for someone in the party (so his heap and its power cell are put in the Clockwork Deep)?
    /// Every game works it out alike (online, from what each player said they had).</summary>
    public static bool AutomatonWanted()
    {
        int bit = 1 << (int)HeroKind.Automaton;
        if (Net.Online && Net.Count > 1) { foreach (var p in Net.Peers.Values) if ((p.Locked & bit) != 0) return true; return false; }
        return !IsReallyUnlocked(HeroKind.Automaton);
    }

    public static bool Unlock(HeroKind h)
    {
        if (!Unlocked.Add(h)) return false;
        GrantStarterPerks(h);
        RunUnlocks++;
        Save();
        Achievements.Freed(h);
        return true;
    }

    /// <summary>A hero comes with the loadout that makes them what their name says: the Aegis with her Bulwark Oath (equipped).</summary>
    private static void GrantStarterPerks(HeroKind h)
    {
        if (h != HeroKind.Aegis) return;
        HexGrid.Owned.Add("aegis_oath");
    }

    /// <summary>
    /// The Elder Dragon fell: if the run found no hero, one a party member played that you hadn't
    /// unlocked (they could not have been met in the caves) is yours now (one at random if several).
    /// </summary>
    public static HeroKind? AwardAfterDragon(System.Collections.Generic.IEnumerable<HeroKind> party, Random rng)
    {
        if (RunUnlocks > 0) return null;
        var locked = party.Distinct().Where(h => !IsReallyUnlocked(h)).ToList();
        if (locked.Count == 0) return null;
        var h = locked[rng.Next(locked.Count)];
        Unlock(h);
        return h;
    }

    public static int MilestoneEvery => Math.Max(5, 8 - HexGrid.Count("fort_mile"));
    public static float XpMult => 1f + new[] { 0f, 0.05f, 0.12f, 0.2f }[HexGrid.Count("fort_xp")] + (HexGrid.Owns("path_root") ? 0.03f : 0f);
    public static float EliteXpMult => 1f + new[] { 0f, 0.05f, 0.12f, 0.2f }[HexGrid.Count("fort_elite")];
    public static float PotionDropChance(Player p)
    {
        float c = Tune.Drops.PotionChance;
        if (HexGrid.Owns("pot_forage1")) c += 0.01f;
        if (HexGrid.Owns("pot_forage2") && p != null && p.Potions == 0) c += 0.01f;
        if (p != null) c += p.Stats.PotionChanceBonus;
        return c;
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

    private static Godot.Collections.Array LoadoutsToArray()
    {
        var a = new Godot.Collections.Array();
        foreach (var kv in Loadouts) foreach (var id in kv.Value) a.Add($"{(int)kv.Key}:{id}");
        return a;
    }

    private static Godot.Collections.Dictionary ToDict()
    {
        var d = new Godot.Collections.Dictionary
        {
            ["embers"] = Embers, ["runs"] = Runs, ["victories"] = Victories, ["best_depth"] = BestDepth,
            ["grid_tut"] = GridTutorialDone, ["story_seen"] = StorySeen, ["journal"] = new Godot.Collections.Array(JournalFound.Select(x => (Variant)x)),
            ["grid"] = HexGrid.ToDict(),
            ["unlocked"] = new Godot.Collections.Array(Unlocked.Select(x => (Variant)(int)x)),
            ["merchant"] = MerchantUnlocked,
            ["loadouts"] = LoadoutsToArray(),
        };
        return d;
    }

    private static void FromDict(Godot.Collections.Dictionary d, bool tutorials)
    {
        int I(string k) => d.ContainsKey(k) ? (int)d[k] : 0;
        Embers = I("embers"); Runs = I("runs"); Victories = I("victories"); BestDepth = I("best_depth");
        if (tutorials)
        {
            GridTutorialDone = d.ContainsKey("grid_tut") && (bool)d["grid_tut"];
            StorySeen = d.ContainsKey("story_seen") && (bool)d["story_seen"];
            JournalFound.Clear();
            if (d.ContainsKey("journal")) foreach (var v in d["journal"].AsGodotArray()) JournalFound.Add((string)v);
        }
        HexGrid.FromDict(d.ContainsKey("grid") ? d["grid"].AsGodotDictionary() : null);
        if (!d.ContainsKey("grid")) Migrate(d);
        // (a save from before heroes were unlocked keeps every hero it was playing)
        Unlocked.Clear();
        if (d.ContainsKey("unlocked")) foreach (var v in d["unlocked"].AsGodotArray()) Unlocked.Add((HeroKind)(int)v);
        else foreach (var h in Enum.GetValues<HeroKind>()) Unlocked.Add(h);
        if (Unlocked.Contains(HeroKind.Aegis)) HexGrid.Owned.Add("aegis_oath");
        MerchantUnlocked = d.ContainsKey("merchant") && (bool)d["merchant"];
        Loadouts.Clear();
        if (d.ContainsKey("loadouts"))
            foreach (var v in d["loadouts"].AsGodotArray())
            {
                var parts = ((string)v).Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out int hk)) LoadoutFor((HeroKind)hk).Add(parts[1]);
            }
    }

    /// <summary>
    /// A save from before the Ember Grid: what was spent on the old trees and class perks comes back as embers, and the old
    /// resources (held or spent) as materials, to be laid out on the grid afresh.
    /// </summary>
    private static void Migrate(Godot.Collections.Dictionary d)
    {
        int refund = 0;
        if (d.ContainsKey("bought"))
            foreach (var v in d["bought"].AsGodotArray()) { var id = (string)v; if (id.Length > 0 && char.IsDigit(id[^1])) refund += id[^1] - '0'; }
        if (d.ContainsKey("perk_ranks"))
            foreach (var kv in d["perk_ranks"].AsGodotDictionary())
            {
                var perk = ClassPerks.Find((string)kv.Key);
                if (perk == null || perk.Id == "aegis_oath") continue;
                for (int r = 0; r < Math.Min((int)kv.Value, perk.Costs.Length); r++) refund += perk.Costs[r];
            }
        Embers += refund;
        int Old(string tree) => d.ContainsKey("found_" + tree) ? (int)d["found_" + tree] : 0;
        if (Old("potion") > 0) HexGrid.Mats["reagent"] = Old("potion");
        if (Old("pearl") > 0) HexGrid.Mats["pearl"] = Old("pearl");
        if (refund > 0 || Old("potion") + Old("pearl") > 0) GD.Print($"[meta] the old trees and perks became the Ember Grid: {refund} embers and {Old("potion")} Reagents, {Old("pearl")} Pearls given back");
    }
}
