using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Ember Grid's materials as they turn up in a run (<see cref="Tune.Grid"/>): a drop a hero can't use (a potion with a full belt, a
/// heart at full health, a key with no room) is now and then a material instead. The host's game decides, like any pickup, and tells a
/// friend's game what they found; each game keeps its own player's materials (they're kept between runs at once, like embers).
/// </summary>
public static class GridFinds
{
    /// <summary>The kinds, in the order they're sent online ("essence" is whichever hero finds it).</summary>
    public static readonly string[] Kinds = { "reagent", "essence", "tumbler", "seal", "dust", "pearl" };
    /// <summary>What this game's player found this run (for the summary at the end), by material key.</summary>
    public static readonly Dictionary<string, int> Run = new();
    private static float _lastSeal = -999f;

    /// <summary>The <see cref="NetSync.GivePickup"/> kind for a material (20 and up).</summary>
    public const byte NetBase = 20;

    /// <summary>A new run: nothing found yet.</summary>
    public static void Reset() { Run.Clear(); _lastSeal = -999f; }

    /// <summary>The host's game: a hero found a material (sent on to them if they're a friend's).</summary>
    public static void Give(Player p, string kind, Vector2 at)
    {
        if (p == null) return;
        if (p.IsRemote) { NetSync.GivePickup(p, (byte)(NetBase + Array.IndexOf(Kinds, kind)), 1); return; }
        Found(p, kind, at);
    }

    /// <summary>A material for this game's player: kept, counted, and shown over them.</summary>
    public static void Found(Player p, string kind, Vector2 at, int n = 1)
    {
        string key = kind == "essence" ? HexGrid.EssenceKey(p.Hero) : kind;
        HexGrid.Gain(key, n);
        Run[key] = (Run.TryGetValue(key, out var had) ? had : 0) + n;
        var col = HexGrid.MatColor(key);
        G.Fx?.Text(at + new Vector2(0, -22), $"+{n} {HexGrid.MatName(key).ToUpperInvariant()}", col, 11, 1.6f);
        G.Fx?.Pop(at, col, 9);
        for (int k = 0; k < 5; k++) G.Fx?.Glint(at + G.RandDir() * G.Range(4, 16), col, 7);
        G.Sfx?.Play("relic_reveal", at, -8, 0.05f, 1.5f);
        Achievements.CheckState();
    }

    /// <summary>A material by its online kind (a friend's game hears of their find).</summary>
    public static void FoundNet(Player p, byte kind)
    {
        int k = kind - NetBase;
        if (k >= 0 && k < Kinds.Length) Found(p, Kinds[k], p.GlobalPosition);
    }

    /// <summary>A roll for a drop the hero couldn't use.</summary>
    public static bool Roll(float chance) => G.Chance(chance);

    /// <summary>This game's player picked up gold with a full purse: now and then a Guild Seal (once the merchant is known).</summary>
    public static void GoldGained(Player p, int carriedBefore)
    {
        if (!Meta.MerchantKnown || carriedBefore < Tune.Grid.SealPurse || G.RunTime - _lastSeal < Tune.Grid.SealGap) return;
        _lastSeal = G.RunTime;
        if (Roll(Tune.Grid.Seal)) Found(p, "seal", p.GlobalPosition);
    }

    /// <summary>The summary's line: ", 2 Reagents, 1 Tumbler"...</summary>
    public static string Line() => string.Concat(Run.Where(kv => kv.Value > 0).Select(kv => $", {kv.Value} {HexGrid.MatName(kv.Key)}"));
}
