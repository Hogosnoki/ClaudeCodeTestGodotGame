using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;

namespace DaggerCave;

/// <summary>
/// Debug mode: typing "debug" at the character select screen turns it on (and again, off). Settings then has a Debug tab with every
/// graphical dial (the stylised-look options, the borders, the lighting, and the tuning values below) so the look can be tuned live; the
/// tab can copy every value, and its defaults button resets them. The tuned values are saved with the settings, so a look found in debug
/// mode stays; the defaults in the code (<see cref="Param.Default"/> here, the matching entries in GameSettings and project.godot's shader
/// globals) are what to change once a look is settled.
/// </summary>
public static class DebugTuning
{
    /// <summary>Debug mode is on (not saved: type "debug" again each launch).</summary>
    public static bool On;

    public sealed class Param
    {
        public string Key, Label, Group, Note;
        public float Min, Max, Default, Value;
        /// <summary>The shader global that carries it (dbg_ + key), or null when it is read from code.</summary>
        public bool Shader;
    }

    public static readonly List<Param> All = new()
    {
        new Param { Key = "fg_bright", Label = "Foreground brightness", Group = "Foreground (the ground at and in front of the play plane)", Note = "brightness of the ground colours and textures", Min = 0.3f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "fg_contrast", Label = "Foreground contrast", Group = null, Note = "contrast of the ground colours and textures", Min = 0.3f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "bg_bright", Label = "Background brightness", Group = "Background (the back of the tunnels, behind the play plane)", Note = "brightness of the ground colours behind the play plane", Min = 0.2f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "bg_contrast", Label = "Background contrast", Group = null, Note = "contrast of the ground colours behind the play plane", Min = 0.3f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "back_dirt", Label = "Background dirt darkness", Group = null, Note = "the back dirt's brightness as a share of the foreground dirt's", Min = 0.0f, Max = 1.2f, Default = 0.5f, Value = 0.5f, Shader = true },
        new Param { Key = "bg_blur", Label = "Background blur", Group = null, Note = "depth blur: everything behind the play plane softens (0 = off)", Min = 0.0f, Max = 1.0f, Default = 0.0f, Value = 0.0f, Shader = false },
        new Param { Key = "bg_blur_dist", Label = "  Blur starts behind plane", Group = null, Note = "metres behind the play plane where the blur begins", Min = 0.0f, Max = 30.0f, Default = 2.0f, Value = 2.0f, Shader = false },
        new Param { Key = "obj_bright", Label = "Characters and creatures brightness", Group = "Characters and creatures", Note = "brightness of their colours and textures", Min = 0.3f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "obj_contrast", Label = "Characters and creatures contrast", Group = null, Note = "contrast of their colours and textures", Min = 0.3f, Max = 2.5f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "wall_gain", Label = "Dirt colour gain", Group = "Dirt and grass", Note = "how bright the biome's wall colour is drawn", Min = 0.2f, Max = 2.0f, Default = 0.8f, Value = 0.8f, Shader = true },
        new Param { Key = "grain_amount", Label = "Grain", Group = null, Note = "fine variation in the dirt's brightness", Min = 0.0f, Max = 0.8f, Default = 0.3f, Value = 0.3f, Shader = true },
        new Param { Key = "lump_amount", Label = "Stone shade variation", Group = null, Note = "how much each stone's shade differs from the next", Min = 0.0f, Max = 0.8f, Default = 0.34f, Value = 0.34f, Shader = true },
        new Param { Key = "crevice_amount", Label = "Crevices", Group = null, Note = "how dark the cracks between stones are", Min = 0.0f, Max = 1.0f, Default = 0.5f, Value = 0.5f, Shader = true },
        new Param { Key = "stone_size", Label = "Stone size", Group = null, Note = "metres across", Min = 0.3f, Max = 3.0f, Default = 0.9f, Value = 0.9f, Shader = true },
        new Param { Key = "facet_shade", Label = "Facet shade", Group = null, Note = "each polygon face's own shade", Min = 0.0f, Max = 0.4f, Default = 0.06f, Value = 0.06f, Shader = true },
        new Param { Key = "grass_lean", Label = "Grass takes biome growth colour", Group = null, Note = "0 = plain green everywhere", Min = 0.0f, Max = 1.0f, Default = 0.25f, Value = 0.25f, Shader = true },
        new Param { Key = "ground_up", Label = "Steepest ground", Group = null, Note = "how level a face must be to count as grass (its up component)", Min = 0.3f, Max = 0.95f, Default = 0.6f, Value = 0.6f, Shader = true },
        new Param { Key = "amb_energy", Label = "Ambient light strength", Group = "Light", Note = "with ambient light only, the cave's", Min = 0.2f, Max = 3.0f, Default = 1.0f, Value = 1.0f, Shader = false },
        new Param { Key = "amb_tint", Label = "Ambient light biome tint", Group = null, Note = "how much the biome's colour tints the (near white) ambient light", Min = 0.0f, Max = 1.0f, Default = 0.2f, Value = 0.2f, Shader = false },
        new Param { Key = "cam_zoom", Label = "Camera zoom", Group = "Camera", Note = "bigger is closer to the player", Min = 1.2f, Max = 4.0f, Default = Tune.Feel.CameraZoom, Value = Tune.Feel.CameraZoom, Shader = false },
    };

    private static readonly Dictionary<string, Param> ByKey = BuildIndex();
    private static Dictionary<string, Param> BuildIndex() { var d = new Dictionary<string, Param>(); foreach (var p in All) d[p.Key] = p; return d; }

    public static float V(string key) => ByKey[key].Value;

    public static void Set(string key, float v)
    {
        var p = ByKey[key];
        p.Value = Math.Clamp(v, p.Min, p.Max);
        Apply();
    }

    public static void Reset()
    {
        foreach (var p in All) p.Value = p.Default;
        Apply();
    }

    /// <summary>Every dial back to its default (the tuning values and the stylised-look settings).</summary>
    public static void ResetAll()
    {
        Reset();
        GameSettings.ResetLook();
    }

    /// <summary>The values into the shader globals and the places that read them (live).</summary>
    public static void Apply()
    {
        foreach (var p in All)
            if (p.Shader) RenderingServer.GlobalShaderParameterSet("dbg_" + p.Key, p.Value);
        var cam = G.Main?.Cam2D;
        if (cam != null && GodotObject.IsInstanceValid(cam)) cam.Zoom = new Vector2(V("cam_zoom"), V("cam_zoom"));
        Stage3D.I?.ApplySettings();
        CampScene.I?.ApplyLighting();
    }

    public static void Save(ConfigFile cfg)
    {
        foreach (var p in All) cfg.SetValue("debug", p.Key, p.Value);
    }

    public static void Load(ConfigFile cfg)
    {
        foreach (var p in All)
            if (cfg.HasSectionKey("debug", p.Key)) p.Value = Math.Clamp((float)cfg.GetValue("debug", p.Key), p.Min, p.Max);
    }

    private static string N(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Every value, one per line (what the look is made of), with a mark where it differs from the default.</summary>
    public static string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Dagger Deep look settings (a * marks a value changed from its default)");
        sb.AppendLine("[settings]");
        foreach (var (k, v, d) in GameSettings.LookValues())
            sb.AppendLine($"{k} = {v}{(v != d ? $"   * (default {d})" : "")}");
        sb.AppendLine("[debug tuning]");
        foreach (var p in All)
            sb.AppendLine($"{p.Key} = {N(p.Value)}{(Math.Abs(p.Value - p.Default) > 1e-4f ? $"   * (default {N(p.Default)})" : "")}");
        return sb.ToString();
    }
}
