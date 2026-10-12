using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Debug mode's red, green and blue gains for each distinct material (the Debug tab's "Materials" group; the characters' are with theirs).
/// The shader-drawn materials (the cave's dirt, grass and snow, the camp's meadow and cliff, the grass blades) read them as shader globals
/// (dbg_m_*); the rest, standard materials (wood, metal, stone props, roots and bark, bone, ice, the camp's trees), are registered here with
/// <see cref="Tint"/> and have their albedo colour multiplied by the gain, live.
/// </summary>
public static class MaterialGains
{
    private static readonly List<(WeakRef m, string kind, Color baseColor)> Made = new();
    private static readonly HashSet<ulong> Seen = new();

    /// <summary>The gain of a material kind (dirt, grass, stone, snow, wood, metal, roots, bone, trees).</summary>
    public static Color Gain(string kind) => new(DebugTuning.V($"m_{kind}_r"), DebugTuning.V($"m_{kind}_g"), DebugTuning.V($"m_{kind}_b"), 1f);

    /// <summary>Registers a standard material as a kind: its albedo colour is what it is times the kind's gain (now, and as the gain changes).</summary>
    public static T Tint<T>(T m, string kind) where T : StandardMaterial3D
    {
        if (m == null || !Seen.Add(m.GetInstanceId())) return m;
        Made.Add((GodotObject.WeakRef(m), kind, m.AlbedoColor));
        Apply(m, kind, m.AlbedoColor);
        return m;
    }

    private static void Apply(StandardMaterial3D m, string kind, Color baseColor)
    {
        var g = Gain(kind);
        m.AlbedoColor = new Color(baseColor.R * g.R, baseColor.G * g.G, baseColor.B * g.B, baseColor.A);
    }

    /// <summary>The gains changed: every registered material takes the new colour.</summary>
    public static void Refresh()
    {
        Made.RemoveAll(e => e.m.GetRef().Obj is not StandardMaterial3D);
        foreach (var (w, kind, baseColor) in Made) Apply((StandardMaterial3D)w.GetRef().Obj, kind, baseColor);
    }
}
