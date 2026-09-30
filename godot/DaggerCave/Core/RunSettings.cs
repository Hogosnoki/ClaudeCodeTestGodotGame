using System;

namespace DaggerCave;

/// <summary>
/// The run's difficulty setup, chosen where heroes are chosen: how hard the enemies scale with the
/// number of players (a 1.0 to 3.0 meter: 1.0 is no scaling, 2.0 the default, 3.0 twice that),
/// and Hard Mode, which makes every enemy dramatically tougher. The host's choices rule online.
/// </summary>
public static class RunSettings
{
    /// <summary>Enemies grow with the party (online).</summary>
    public static bool ScaleWithPlayers = true;
    /// <summary>The scaling meter: 1 = none, 2 = +100% health and +20% damage per extra player, 3 = twice that.</summary>
    public static float Scale = Tune.Difficulty.PartyScaleDefault;
    /// <summary>Hard Mode: all enemies far tougher, alone or together.</summary>
    public static bool Hard;

    /// <summary>The players the enemies scale for (an online party; one alone).</summary>
    public static int Players => Net.Online ? Math.Max(1, Net.Count) : 1;

    /// <summary>Enemy health multiplier for this setup: 100% more per extra player at the default meter (+ the meter's steps above 1); Hard Mode on top.</summary>
    public static float HpFor(int players, bool scaleOn, float scale, bool hard)
        => (1f + Steps(players, scaleOn, scale) * Tune.Difficulty.PartyHpPerStep) * (hard ? Tune.Difficulty.HardHp : 1f);

    /// <summary>Enemy damage multiplier for this setup: 20% more per extra player at the default meter; Hard Mode on top.</summary>
    public static float DmgFor(int players, bool scaleOn, float scale, bool hard)
        => (1f + Steps(players, scaleOn, scale) * Tune.Difficulty.PartyDamagePerStep) * (hard ? Tune.Difficulty.HardDamage : 1f);

    private static float Steps(int players, bool scaleOn, float scale) => scaleOn ? Math.Max(0f, scale - 1f) * Math.Max(0, players - 1) : 0f;

    public static float HpMult => HpFor(Players, ScaleWithPlayers, Scale, Hard);
    public static float DmgMult => DmgFor(Players, ScaleWithPlayers, Scale, Hard);

    public static string Describe()
    {
        string s = ScaleWithPlayers && Players > 1 ? $"enemies x{HpMult:0.0#} health, x{DmgMult:0.0#} damage" : "";
        if (Hard && s == "") s = $"enemies x{HpMult:0.0#} health, x{DmgMult:0.0#} damage";
        return (Hard ? "HARD  ·  " : "") + s;
    }
}
