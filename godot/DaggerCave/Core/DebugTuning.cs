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
        new Param { Key = "panel_opacity", Label = "Debug window opacity", Group = "Debug window", Note = "how solid the settings window is on the Debug tab (low: see the changes behind it)", Min = 0.05f, Max = 1f, Default = 0.323f, Value = 0.323f, Shader = false },
        new Param { Key = "fg_bright", Label = "Foreground brightness", Group = "Foreground (the ground at and in front of the play plane)", Note = "brightness of the ground colours and textures", Min = 0.3f, Max = 2.5f, Default = 0.922f, Value = 0.922f, Shader = true },
        new Param { Key = "fg_contrast", Label = "Foreground contrast", Group = null, Note = "0 = every colour the average colour; 1 = as drawn; more spreads them from it", Min = 0f, Max = 2.5f, Default = 1.025f, Value = 1.025f, Shader = true },
        new Param { Key = "bg_bright", Label = "Background brightness", Group = "Background (the back of the tunnels, behind the play plane)", Note = "brightness of the ground colours behind the play plane", Min = 0.2f, Max = 2.5f, Default = 0.919f, Value = 0.919f, Shader = true },
        new Param { Key = "bg_contrast", Label = "Background contrast", Group = null, Note = "the same, for the ground behind the play plane", Min = 0f, Max = 2.5f, Default = 1.056f, Value = 1.056f, Shader = true },
        new Param { Key = "back_dirt", Label = "Background dirt darkness", Group = null, Note = "the back dirt's brightness as a share of the foreground dirt's", Min = 0.0f, Max = 1.2f, Default = 0.294f, Value = 0.294f, Shader = true },
        new Param { Key = "bg_blur", Label = "Background blur", Group = null, Note = "depth blur: everything behind the play plane softens (0 = off)", Min = 0.0f, Max = 1.0f, Default = 0.0f, Value = 0.0f, Shader = false },
        new Param { Key = "bg_blur_dist", Label = "  Blur starts behind plane", Group = null, Note = "metres behind the play plane where the blur begins", Min = 0.0f, Max = 30.0f, Default = 30.0f, Value = 30.0f, Shader = false },
        new Param { Key = "obj_bright", Label = "Characters and creatures brightness", Group = "Characters and creatures", Note = "brightness of their colours and textures", Min = 0.3f, Max = 2.5f, Default = 2.5f, Value = 2.5f, Shader = true },
        new Param { Key = "obj_contrast", Label = "Characters and creatures contrast", Group = null, Note = "the same, toward each creature's own average colour", Min = 0f, Max = 2.5f, Default = 2.5f, Value = 2.5f, Shader = true },
        new Param { Key = "obj_r", Label = "Characters and creatures red", Group = null, Note = "the colour's red, green or blue, 1 = as it is, 0 = none of it", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "obj_g", Label = "Characters and creatures green", Group = null, Note = "the colour's red, green or blue, 1 = as it is, 0 = none of it", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "obj_b", Label = "Characters and creatures blue", Group = null, Note = "the colour's red, green or blue, 1 = as it is, 0 = none of it", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = true },
        new Param { Key = "wall_gain", Label = "Dirt colour gain", Group = "Dirt and grass", Note = "how bright the biome's wall colour is drawn", Min = 0.2f, Max = 2.0f, Default = 1.69f, Value = 1.69f, Shader = true },
        new Param { Key = "grain_amount", Label = "Grain", Group = null, Note = "fine variation in the dirt's brightness", Min = 0.0f, Max = 0.8f, Default = 0.338f, Value = 0.338f, Shader = true },
        new Param { Key = "lump_amount", Label = "Stone shade variation", Group = null, Note = "how much each stone's shade differs from the next", Min = 0.0f, Max = 0.8f, Default = 0.32f, Value = 0.32f, Shader = true },
        new Param { Key = "crevice_amount", Label = "Crevices", Group = null, Note = "how dark the cracks between stones are", Min = 0.0f, Max = 1.0f, Default = 0.273f, Value = 0.273f, Shader = true },
        new Param { Key = "stone_size", Label = "Stone size", Group = null, Note = "metres across", Min = 0.3f, Max = 3.0f, Default = 1.326f, Value = 1.326f, Shader = true },
        new Param { Key = "facet_shade", Label = "Facet shade", Group = null, Note = "each polygon face's own shade", Min = 0.0f, Max = 0.4f, Default = 0.0f, Value = 0.0f, Shader = true },
        new Param { Key = "grass_lean", Label = "Grass takes biome growth colour", Group = null, Note = "0 = plain green everywhere", Min = 0.0f, Max = 1.0f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "ground_up", Label = "Steepest ground", Group = null, Note = "how level a face must be to count as grass (its up component)", Min = 0.3f, Max = 0.95f, Default = 0.836f, Value = 0.836f, Shader = true },
        new Param { Key = "m_dirt_r", Label = "Dirt red", Group = "Materials: red, green and blue gains (1 = as it is)", Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "m_dirt_g", Label = "Dirt green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = true },
        new Param { Key = "m_dirt_b", Label = "Dirt blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "m_grass_r", Label = "Grass red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.99f, Value = 0.99f, Shader = true },
        new Param { Key = "m_grass_g", Label = "Grass green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = true },
        new Param { Key = "m_grass_b", Label = "Grass blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = true },
        new Param { Key = "m_stone_r", Label = "Stone red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = true },
        new Param { Key = "m_stone_g", Label = "Stone green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "m_stone_b", Label = "Stone blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.99f, Value = 0.99f, Shader = true },
        new Param { Key = "m_snow_r", Label = "Snow and ice red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.99f, Value = 0.99f, Shader = true },
        new Param { Key = "m_snow_g", Label = "Snow and ice green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "m_snow_b", Label = "Snow and ice blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = true },
        new Param { Key = "m_wood_r", Label = "Wood red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = false },
        new Param { Key = "m_wood_g", Label = "Wood green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_wood_b", Label = "Wood blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_metal_r", Label = "Metal red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_metal_g", Label = "Metal green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = false },
        new Param { Key = "m_metal_b", Label = "Metal blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = false },
        new Param { Key = "m_roots_r", Label = "Roots and bark red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.99f, Value = 0.99f, Shader = false },
        new Param { Key = "m_roots_g", Label = "Roots and bark green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_roots_b", Label = "Roots and bark blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = false },
        new Param { Key = "m_bone_r", Label = "Bone red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 1.005f, Value = 1.005f, Shader = false },
        new Param { Key = "m_bone_g", Label = "Bone green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_bone_b", Label = "Bone blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.99f, Value = 0.99f, Shader = false },
        new Param { Key = "m_trees_r", Label = "Camp trees red", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_trees_g", Label = "Camp trees green", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "m_trees_b", Label = "Camp trees blue", Group = null, Note = "0 = none of it, 2 = double", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = false },
        new Param { Key = "scr_r", Label = "Whole picture red", Group = "Whole picture", Note = "the whole lit picture's red, green or blue (a full-screen pass, after everything)", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = true },
        new Param { Key = "scr_g", Label = "Whole picture green", Group = null, Note = "the colour's red, green or blue, 1 = as it is, 0 = none of it", Min = 0f, Max = 2f, Default = 0.995f, Value = 0.995f, Shader = true },
        new Param { Key = "scr_b", Label = "Whole picture blue", Group = null, Note = "the colour's red, green or blue, 1 = as it is, 0 = none of it", Min = 0f, Max = 2f, Default = 1.0f, Value = 1.0f, Shader = true },
        new Param { Key = "poly_smooth", Label = "Polygon smoothing", Group = "3D shape and transitions", Note = "0 = every polygon flat, 1 = smooth shading across them (also smooths where grass ends)", Min = 0f, Max = 1f, Default = 0.9f, Value = 0.9f, Shader = true },
        new Param { Key = "trans_soft", Label = "Grass/dirt transition softness", Group = null, Note = "0 = a hard edge, 1 = a wide gradual blend between grass and dirt", Min = 0f, Max = 1f, Default = 0.035f, Value = 0.035f, Shader = true },
        new Param { Key = "bg_fade", Label = "Foreground-to-background fade", Group = null, Note = "metres behind the play plane over which the foreground look fades into the background look", Min = 0.3f, Max = 12f, Default = 0.3f, Value = 0.3f, Shader = true },
        new Param { Key = "crevice_soft", Label = "Crevice softness", Group = null, Note = "how sharp the cracks between stones are", Min = 0.02f, Max = 0.8f, Default = 0.203f, Value = 0.203f, Shader = true },
        new Param { Key = "fog_mult", Label = "Cave haze strength", Group = "Scene effects", Note = "times the biome's volumetric fog", Min = 0f, Max = 3f, Default = 3.0f, Value = 3.0f, Shader = false },
        new Param { Key = "ao_mult", Label = "Ambient occlusion strength", Group = null, Note = "times the usual", Min = 0f, Max = 3f, Default = 1.275f, Value = 1.275f, Shader = false },
        new Param { Key = "bloom_mult", Label = "Bloom strength", Group = null, Note = "times the usual", Min = 0f, Max = 3f, Default = 0.54f, Value = 0.54f, Shader = false },
        new Param { Key = "exposure", Label = "Exposure", Group = null, Note = "on top of the Brightness setting", Min = 0.4f, Max = 2.5f, Default = 0.993f, Value = 0.993f, Shader = false },
        new Param { Key = "cam_fov", Label = "Camera field of view", Group = null, Note = "degrees: the framing stays the same, only the perspective changes", Min = 15f, Max = 80f, Default = 50.1f, Value = 50.1f, Shader = false },
        new Param { Key = "edge_step", Label = "Outline sensitivity", Group = null, Note = "how big a depth step makes a border (lower: more borders)", Min = 0.01f, Max = 0.3f, Default = 0.3f, Value = 0.3f, Shader = true },
        new Param { Key = "edge_alpha", Label = "Border opacity", Group = null, Note = "object borders' ink", Min = 0.1f, Max = 1f, Default = 0.64f, Value = 0.64f, Shader = true },
        new Param { Key = "amb_energy", Label = "Ambient light strength", Group = "Light", Note = "with ambient light only, the cave's", Min = 0.2f, Max = 3.0f, Default = 1.243f, Value = 1.243f, Shader = false },
        new Param { Key = "amb_tint", Label = "Ambient light biome tint", Group = null, Note = "how much the biome's colour tints the (near white) ambient light", Min = 0.0f, Max = 1.0f, Default = 1.0f, Value = 1.0f, Shader = false },
        new Param { Key = "cam_zoom", Label = "Camera zoom", Group = "Camera", Note = "bigger is closer to the player", Min = 1.2f, Max = 4.0f, Default = 2.012f, Value = 2.012f, Shader = false },
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
        CelPost.Refresh();
        MaterialGains.Refresh();
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
