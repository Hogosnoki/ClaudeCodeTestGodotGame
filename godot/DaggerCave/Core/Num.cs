using System;

namespace DaggerCave;

/// <summary>
/// How a number is shown. Every pool (health, alimus, vital force, shields) is displayed rounded UP, so 0 on the screen means
/// empty (or dead) and never a fraction that rounds to 1; and a floating number for a change in a pool is the change in
/// what's displayed, |shown(after) - shown(before)|, so the numbers always agree with the bars and counters, whatever fraction
/// the real damage was.
/// </summary>
public static class Num
{
    /// <summary>What a pool holding <paramref name="v"/> shows: rounded up (a hair of float noise forgiven), 0 only when empty.</summary>
    public static int Shown(float v) => v <= 0f ? 0 : (int)MathF.Ceiling(v - 1e-3f);

    /// <summary>The number to float for a pool going from <paramref name="before"/> to <paramref name="after"/> (0: nothing visible changed).</summary>
    public static int Delta(float before, float after) => Math.Abs(Shown(after) - Shown(before));
}
