using System;

namespace DaggerCave;

/// <summary>
/// Hit-stop as slow motion: when a blow lands, the hero and what was struck drop to <c>hitstop_speed</c> (20%) of their pace and
/// come back up to full speed over the hit-stop's length, gradually at first and then quickly (an S-curve), instead of freezing
/// stock-still. The Debug tab's <c>hitstop_slow</c> switches back to the freeze (0) to compare.
/// </summary>
public static class HitSlow
{
    /// <summary>Hit-stops slow down rather than stop.</summary>
    public static bool On => DebugTuning.V("hitstop_slow") >= 0.5f;

    /// <summary>The pace (0..1) with <paramref name="left"/> of a hit-stop <paramref name="total"/> long still to go.</summary>
    public static float Speed(float left, float total)
    {
        if (total <= 0f || left <= 0f) return 1f;
        float t = 1f - Math.Clamp(left / total, 0f, 1f);
        float e = t * t * (3f - 2f * t);
        float s = DebugTuning.V("hitstop_speed");
        return s + (1f - s) * e;
    }
}
