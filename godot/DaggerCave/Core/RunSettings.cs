using System;

namespace DaggerCave;

/// <summary>
/// The run's difficulty setup, chosen where heroes are chosen. Two sliders: the difficulty
/// (0.5 to 2.0) and the per-player difficulty (1.0 to 2.0); the enemies' strength is
/// <c>difficulty x (players x per-player)</c>: on your own at the defaults it is 1, and at the
/// top of both sliders with six players it is 2 x (6 x 2) = 24. Health takes all of it; damage
/// a fifth of the way (so a hard cave is a long fight, not a single blow). Hard Mode stays on top.
/// The host's choices rule online.
/// </summary>
public static class RunSettings
{
    /// <summary>The difficulty slider.</summary>
    public static float Difficulty = 1f;
    /// <summary>The per-player slider: what each player in the party adds.</summary>
    public static float PerPlayer = 1f;
    /// <summary>Hard Mode: all enemies far tougher, alone or together.</summary>
    public static bool Hard;

    public const float DifficultyMin = 0.5f, DifficultyMax = 2f, PerPlayerMin = 1f, PerPlayerMax = 2f;
    /// <summary>The most heroes in a party, whatever the roster.</summary>
    public const int MaxParty = 6;

    /// <summary>The players the enemies scale for (an online party; one alone).</summary>
    public static int Players => Net.Online ? Math.Max(1, Net.Count) : 1;

    /// <summary>The overall strength: difficulty x (players x per-player).</summary>
    public static float Strength(int players, float difficulty, float perPlayer) => difficulty * (Math.Max(1, players) * perPlayer);

    /// <summary>Enemy health multiplier for this setup (Hard Mode on top).</summary>
    public static float HpFor(int players, float difficulty, float perPlayer, bool hard)
        => Strength(players, difficulty, perPlayer) * (hard ? Tune.Difficulty.HardHp : 1f);

    /// <summary>Enemy damage multiplier: a fifth of the way from 1 toward the strength (Hard Mode on top).</summary>
    public static float DmgFor(int players, float difficulty, float perPlayer, bool hard)
        => (1f + (Strength(players, difficulty, perPlayer) - 1f) * Tune.Difficulty.DamageShare) * (hard ? Tune.Difficulty.HardDamage : 1f);

    public static float HpMult => HpFor(Players, Difficulty, PerPlayer, Hard);
    public static float DmgMult => DmgFor(Players, Difficulty, PerPlayer, Hard);

    public static string Describe()
        => (Hard ? "HARD  ·  " : "") + $"enemies x{HpMult:0.0#} health, x{DmgMult:0.0#} damage";
}
