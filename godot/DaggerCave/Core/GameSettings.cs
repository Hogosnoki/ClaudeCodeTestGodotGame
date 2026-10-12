using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The player's settings (graphics, sound, controls), kept in user://settings.cfg and applied at
/// start-up. The pause menu's Settings screen edits them; <see cref="Apply"/> puts them in force.
/// </summary>
public static class GameSettings
{
    private const string FilePath = "user://settings.cfg";

    // ---------------------------------------------------------------- graphics
    public enum WindowMode { Windowed, Borderless, Fullscreen }
    public static WindowMode Window = WindowMode.Windowed;
    public static bool VSync = true;
    /// <summary>0 = unlimited.</summary>
    public static int MaxFps = 0;
    /// <summary>The 3D picture's resolution, as a share of the window's (sharpened up with FSR).</summary>
    public static float RenderScale = 1f;
    public enum Quality { Off, Low, High }
    public static Quality Shadows = Quality.High;
    /// <summary>Volumetric fog (the cave's haze and light shafts): the heaviest effect.</summary>
    public static bool Fog = true;
    public static bool Bloom = true;
    public static bool AmbientOcclusion = true;
    public static bool InkOutlines = true;
    /// <summary>The stylised (cel-shaded) look: light in soft bands, flatter colour, painted rock (the shader global cel_shading).</summary>
    public static bool CelShading = LookDef.Shading;
    /// <summary>The minimalist ground: two flat colours, grass on the floors and the biome's wall colour everywhere else, with the polygons
    /// lit face by face (every corner of the rock a distinct edge), instead of the photographed textures (the shader global terrain_solid).</summary>
    public static bool SolidTerrain = LookDef.Solid;
    /// <summary>Ambient light only: every other light muted, the ambient raised to make up for it (<see cref="AmbientOnly"/>).</summary>
    public static bool AmbientOnly = LookDef.Ambient;
    /// <summary>The ink borders' widths, in pixels (at 720 lines; they scale with the resolution): round the outline of every 3D thing
    /// (0 = none), and round the characters, a little bolder. (Never between the colours on one surface.)</summary>
    public static float TerrainInkPx = LookDef.TerrainInk, ActorInkPx = LookDef.ActorInk;
    /// <summary>The stylised look's dials (shader globals, live). Two pairs, the same two dials each: how blurred, then how many tones the
    /// brightness is divided into (0 = not divided, else 2-12):
    ///  * texture blur and tones: the textures, as each material samples them (blur in mip levels down, 0-4);
    ///  * lighting blur and tones: the lit picture, after the lights are done (a full-screen pass, <see cref="CelPost"/>; blur 0-4);
    /// then how soft the edges between tones are (0-1).</summary>
    public static float CelTexBlur = LookDef.TexBlur, CelTexTones = LookDef.TexTones, CelLightBlur = LookDef.LightBlur, CelLightTones = LookDef.LightTones, CelBandSoft = LookDef.Soft;
    /// <summary>How flat the light is (0-1): its contrast squeezed toward its own average, never brighter or darker overall. With lighting
    /// tones it reads as a cartoon; without, as plainer, cheaper lighting.</summary>
    public static float CelLightFlat = LookDef.Flat;

    /// <summary>The look's default settings (debug mode's defaults button goes back to these; change them once a look is settled).</summary>
    public static class LookDef
    {
        public const bool Shading = true, Solid = true, Ambient = true;
        public const float TerrainInk = 1.2f, ActorInk = 2.975f;
        public const float TexBlur = 0f, TexTones = 0f, LightBlur = 0f, LightTones = 9f, Soft = 0.95f, Flat = 0f;
    }

    /// <summary>The look's settings as a set, one for each of the two looks: the cave's and the title screen's (the camp: more like the
    /// original, lit by the sun and sky with the photographed rock and the old meadow, no object borders).</summary>
    public sealed class LookSnap
    {
        public bool Shading, Solid, Ambient;
        public float TerrainInk, ActorInk, TexBlur, TexTones, LightBlur, LightTones, Soft, Flat;

        public static LookSnap CaveDefaults() => new()
        {
            Shading = LookDef.Shading, Solid = LookDef.Solid, Ambient = LookDef.Ambient, TerrainInk = LookDef.TerrainInk, ActorInk = LookDef.ActorInk,
            TexBlur = LookDef.TexBlur, TexTones = LookDef.TexTones, LightBlur = LookDef.LightBlur, LightTones = LookDef.LightTones, Soft = LookDef.Soft, Flat = LookDef.Flat,
        };

        /// <summary>The title screen's defaults: not stylised, sky and sun, the photographed rock, a character line as it was (3.5 px), no object borders.</summary>
        public static LookSnap TitleDefaults() => new()
        {
            Shading = false, Solid = false, Ambient = false, TerrainInk = 0f, ActorInk = 3.5f,
            TexBlur = 0f, TexTones = 0f, LightBlur = 0f, LightTones = 0f, Soft = 0.4f, Flat = 0f,
        };
    }

    /// <summary>The title screen's look is on show (the camp is up): the settings fields below hold it, and the cave's are kept aside.</summary>
    public static bool TitleLook;
    private static LookSnap _lookStash = LookSnap.TitleDefaults();

    private static LookSnap Capture() => new()
    {
        Shading = CelShading, Solid = SolidTerrain, Ambient = AmbientOnly, TerrainInk = TerrainInkPx, ActorInk = ActorInkPx,
        TexBlur = CelTexBlur, TexTones = CelTexTones, LightBlur = CelLightBlur, LightTones = CelLightTones, Soft = CelBandSoft, Flat = CelLightFlat,
    };

    private static void Restore(LookSnap s)
    {
        CelShading = s.Shading; SolidTerrain = s.Solid; AmbientOnly = s.Ambient; TerrainInkPx = s.TerrainInk; ActorInkPx = s.ActorInk;
        CelTexBlur = s.TexBlur; CelTexTones = s.TexTones; CelLightBlur = s.LightBlur; CelLightTones = s.LightTones; CelBandSoft = s.Soft; CelLightFlat = s.Flat;
    }

    public static LookSnap CaveSnap => TitleLook ? _lookStash : Capture();
    public static LookSnap TitleSnap => TitleLook ? Capture() : _lookStash;

    /// <summary>Shows the title screen's look (at the camp) or the cave's (below): the settings and the debug tuning values swap, live.</summary>
    public static void SetLookProfile(bool title)
    {
        if (title == TitleLook) return;
        var current = Capture();
        Restore(_lookStash);
        _lookStash = current;
        TitleLook = title;
        DebugTuning.SwapProfile();
        ApplyCel();
    }

    /// <summary>Every look setting of the look on show back to its default.</summary>
    public static void ResetLook()
    {
        Restore(TitleLook ? LookSnap.TitleDefaults() : LookSnap.CaveDefaults());
        ApplyCel();
    }

    /// <summary>Every look setting as (name, value, default) text, for debug mode's dump.</summary>
    public static System.Collections.Generic.IEnumerable<(string key, string value, string def)> LookValues(bool title)
    {
        string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        string Bo(bool v) => v ? "on" : "off";
        var s = title ? TitleSnap : CaveSnap;
        var d = title ? LookSnap.TitleDefaults() : LookSnap.CaveDefaults();
        yield return ("stylised_shading", Bo(s.Shading), Bo(d.Shading));
        yield return ("minimalist_ground", Bo(s.Solid), Bo(d.Solid));
        yield return ("ambient_light_only", Bo(s.Ambient), Bo(d.Ambient));
        yield return ("object_border_px", F(s.TerrainInk), F(d.TerrainInk));
        yield return ("character_border_px", F(s.ActorInk), F(d.ActorInk));
        yield return ("texture_blur", F(s.TexBlur), F(d.TexBlur));
        yield return ("texture_tones", F(s.TexTones), F(d.TexTones));
        yield return ("lighting_blur", F(s.LightBlur), F(d.LightBlur));
        yield return ("lighting_tones", F(s.LightTones), F(d.LightTones));
        yield return ("band_softness", F(s.Soft), F(d.Soft));
        yield return ("light_flatness", F(s.Flat), F(d.Flat));
        yield return ("ink_outlines_on_creatures", Bo(InkOutlines), Bo(true));
        yield return ("brightness", F(Brightness), F(1.4f));
    }

    /// <summary>Ready-made settings of those dials.</summary>
    public static readonly (string name, float blur, float tones, float lblur, float ltones, float soft, float flat)[] CelPresets =
    {
        ("Subtle", 0f, 0f, 0f, 8f, 0.7f, 0f),
        ("Painted", 0f, 6f, 0f, 5f, 0.4f, 0.25f),
        ("Cartoon", 0f, 5f, 1f, 4f, 0.2f, 0.55f),
        ("Bold", 1f, 4f, 1.5f, 3f, 0.15f, 0.4f),
        ("Poster", 2f, 3f, 2.5f, 3f, 0.05f, 0.7f),
        ("Flat", 0f, 0f, 0f, 0f, 0.5f, 0.85f),
    };

    /// <summary>The stylised look's settings into the shader globals (they take effect everywhere at once).</summary>
    public static void ApplyCel()
    {
        RenderingServer.GlobalShaderParameterSet("cel_shading", CelShading ? 1f : 0f);
        RenderingServer.GlobalShaderParameterSet("cel_tex_blur", CelTexBlur);
        RenderingServer.GlobalShaderParameterSet("cel_tex_tones", CelTexTones);
        RenderingServer.GlobalShaderParameterSet("cel_light_blur", CelLightBlur);
        RenderingServer.GlobalShaderParameterSet("cel_light_tones", CelLightTones);
        RenderingServer.GlobalShaderParameterSet("cel_band_soft", CelBandSoft);
        RenderingServer.GlobalShaderParameterSet("cel_light_flat", CelLightFlat);
        RenderingServer.GlobalShaderParameterSet("terrain_solid", SolidTerrain ? 1f : 0f);
        RenderingServer.GlobalShaderParameterSet("ink_terrain_px", TerrainInkPx);
        RenderingServer.GlobalShaderParameterSet("ink_actor_px", ActorInkPx);
        RenderingServer.GlobalShaderParameterSet("light_ambient_only", AmbientOnly ? 1f : 0f);
        CelPost.Refresh();
        EdgeInk.Refresh();
        DebugTuning.Apply();
        PropViews.RestyleRock();
    }
    public enum AntiAlias { Off, Fxaa, Msaa2, Msaa4 }
    public static AntiAlias Aa = AntiAlias.Off;
    /// <summary>Exposure multiplier (1 = as designed).</summary>
    public static float Brightness = 1.4f;
    /// <summary>Screen shake and camera kicks (0..1).</summary>
    public static float Shake = 1f;

    // ---------------------------------------------------------------- sound
    public static float MasterVolume = 1f, MusicVolume = 0.8f, SfxVolume = 1f;

    // ---------------------------------------------------------------- controls
    public enum AimMode { Auto, Mouse, Movement }

    /// <summary>How keyboard presses aim: at the mouse pointer, where you move, or (Auto) the
    /// pointer while the mouse is in use.</summary>
    public static AimMode Aim = AimMode.Auto;
    public static bool Vibration = true;
    /// <summary>HUD tooltips on mouse hover: 0 off, 1 abilities, potions and relics, 2 everything on the HUD (see <see cref="Hud.Guide"/> too).</summary>
    public static int HudTips = 1;

    // ---------------------------------------------------------------- online
    /// <summary>The name the others see online ("" = this computer's user name).</summary>
    public static string PlayerName = "";
    /// <summary>The last join code (or address) typed, offered again next time.</summary>
    public static string LastJoin = "";

    /// <summary>When the mouse last moved or clicked (ms since start).</summary>
    public static ulong MouseSeenMs;

    /// <summary>Keyboard presses aim at the mouse pointer (rather than the way you move).</summary>
    public static bool AimFromMouse => Aim switch
    {
        AimMode.Mouse => true,
        AimMode.Movement => false,
        _ => MouseSeenMs > 0 && Time.GetTicksMsec() - MouseSeenMs < 10000,
    };

    /// <summary>Notes mouse use (Main forwards every input event here).</summary>
    public static void NoteInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } || (e is InputEventMouseMotion mm && mm.Relative.Length() > 2f))
            MouseSeenMs = Math.Max(1, Time.GetTicksMsec());
    }

    // ---------------------------------------------------------------- saving

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) != Error.Ok) return;
        int I(string sec, string key, int def) => cfg.HasSectionKey(sec, key) ? (int)cfg.GetValue(sec, key) : def;
        float F(string sec, string key, float def) => cfg.HasSectionKey(sec, key) ? (float)cfg.GetValue(sec, key) : def;
        bool B(string sec, string key, bool def) => cfg.HasSectionKey(sec, key) ? (bool)cfg.GetValue(sec, key) : def;
        string S(string sec, string key, string def) => cfg.HasSectionKey(sec, key) ? (string)cfg.GetValue(sec, key) : def;
        Window = (WindowMode)I("graphics", "window", (int)Window);
        VSync = B("graphics", "vsync", VSync);
        MaxFps = I("graphics", "max_fps", MaxFps);
        RenderScale = F("graphics", "render_scale", RenderScale);
        Shadows = (Quality)I("graphics", "shadows", (int)Shadows);
        Fog = B("graphics", "fog", Fog);
        Bloom = B("graphics", "bloom", Bloom);
        AmbientOcclusion = B("graphics", "ao", AmbientOcclusion);
        InkOutlines = B("graphics", "ink", InkOutlines);
        CelShading = B("graphics", "cel", CelShading);
        DebugTuning.Load(cfg);
        SolidTerrain = B("graphics", "solid_terrain", SolidTerrain);
        AmbientOnly = B("graphics", "ambient_only", AmbientOnly);
        TerrainInkPx = Math.Clamp(F("graphics", "ink_terrain_px", TerrainInkPx), 0f, 4f);
        ActorInkPx = Math.Clamp(F("graphics", "ink_actor_px", ActorInkPx), 0.5f, 6f);
        // (the title screen's look, kept aside until the camp is up)
        {
            var t = LookSnap.TitleDefaults();
            t.Shading = B("title_look", "cel", t.Shading); t.Solid = B("title_look", "solid_terrain", t.Solid); t.Ambient = B("title_look", "ambient_only", t.Ambient);
            t.TerrainInk = Math.Clamp(F("title_look", "ink_terrain_px", t.TerrainInk), 0f, 4f); t.ActorInk = Math.Clamp(F("title_look", "ink_actor_px", t.ActorInk), 0.5f, 6f);
            t.TexBlur = Math.Clamp(F("title_look", "cel_tex_blur", t.TexBlur), 0f, 4f); t.TexTones = Math.Clamp(F("title_look", "cel_tones", t.TexTones), 0f, 12f);
            t.LightBlur = Math.Clamp(F("title_look", "cel_light_blur", t.LightBlur), 0f, 4f); t.LightTones = Math.Clamp(F("title_look", "cel_light_tones", t.LightTones), 0f, 12f);
            t.Soft = Math.Clamp(F("title_look", "cel_soft", t.Soft), 0f, 1f); t.Flat = Math.Clamp(F("title_look", "cel_flat", t.Flat), 0f, 1f);
            _lookStash = t;
        }
        // (texture blur is saved under a new name: the old default of 2 smudged away what the textures show)
        CelTexBlur = Math.Clamp(F("graphics", "cel_tex_blur", CelTexBlur), 0f, 4f);
        CelTexTones = Math.Clamp(F("graphics", "cel_tones", CelTexTones), 0f, 12f);
        CelLightBlur = Math.Clamp(F("graphics", "cel_light_blur", CelLightBlur), 0f, 4f);
        CelLightTones = Math.Clamp(F("graphics", "cel_light_tones", CelLightTones), 0f, 12f);
        CelBandSoft = Math.Clamp(F("graphics", "cel_soft", CelBandSoft), 0f, 1f);
        CelLightFlat = Math.Clamp(F("graphics", "cel_flat", CelLightFlat), 0f, 1f);
        Aa = (AntiAlias)Math.Min(1, I("graphics", "aa", (int)Aa)); // (MSAA flickered against the ink outlines: any old choice of it is FXAA now)
        Brightness = F("graphics", "brightness", Brightness);
        Shake = F("graphics", "shake", Shake);
        MasterVolume = F("sound", "master", MasterVolume);
        MusicVolume = F("sound", "music", MusicVolume);
        SfxVolume = F("sound", "sfx", SfxVolume);
        Aim = (AimMode)I("controls", "aim", (int)Aim);
        Vibration = B("controls", "vibration", Vibration);
        HudTips = Math.Clamp(I("interface", "hud_tips", HudTips), 0, 2);
        PlayerName = S("online", "name", PlayerName);
        LastJoin = S("online", "last_join", LastJoin);
        Controls.Load(cfg);
    }

    public static void Save()
    {
        if (G.NoSave) return;
        var cfg = new ConfigFile();
        cfg.SetValue("graphics", "window", (int)Window);
        cfg.SetValue("graphics", "vsync", VSync);
        cfg.SetValue("graphics", "max_fps", MaxFps);
        cfg.SetValue("graphics", "render_scale", RenderScale);
        cfg.SetValue("graphics", "shadows", (int)Shadows);
        cfg.SetValue("graphics", "fog", Fog);
        cfg.SetValue("graphics", "bloom", Bloom);
        cfg.SetValue("graphics", "ao", AmbientOcclusion);
        cfg.SetValue("graphics", "ink", InkOutlines);
        DebugTuning.Save(cfg);
        // (the cave's look under the old keys, the title screen's under its own)
        foreach (var (sec, s) in new[] { ("graphics", CaveSnap), ("title_look", TitleSnap) })
        {
            cfg.SetValue(sec, "cel", s.Shading);
            cfg.SetValue(sec, "solid_terrain", s.Solid);
            cfg.SetValue(sec, "ambient_only", s.Ambient);
            cfg.SetValue(sec, "ink_terrain_px", s.TerrainInk);
            cfg.SetValue(sec, "ink_actor_px", s.ActorInk);
            cfg.SetValue(sec, "cel_tex_blur", s.TexBlur);
            cfg.SetValue(sec, "cel_tones", s.TexTones);
            cfg.SetValue(sec, "cel_light_blur", s.LightBlur);
            cfg.SetValue(sec, "cel_light_tones", s.LightTones);
            cfg.SetValue(sec, "cel_soft", s.Soft);
            cfg.SetValue(sec, "cel_flat", s.Flat);
        }
        cfg.SetValue("graphics", "aa", (int)Aa);
        cfg.SetValue("graphics", "brightness", Brightness);
        cfg.SetValue("graphics", "shake", Shake);
        cfg.SetValue("sound", "master", MasterVolume);
        cfg.SetValue("sound", "music", MusicVolume);
        cfg.SetValue("sound", "sfx", SfxVolume);
        cfg.SetValue("controls", "aim", (int)Aim);
        cfg.SetValue("controls", "vibration", Vibration);
        cfg.SetValue("interface", "hud_tips", HudTips);
        cfg.SetValue("online", "name", PlayerName);
        cfg.SetValue("online", "last_join", LastJoin);
        Controls.Save(cfg);
        cfg.Save(FilePath);
    }

    // ---------------------------------------------------------------- applying

    /// <summary>Puts every setting in force (window, rendering, sound).</summary>
    public static void Apply(Node anyNode)
    {
        ApplyDisplay(anyNode);
        ApplyRendering(anyNode);
        ApplySound();
    }

    public static void ApplyDisplay(Node anyNode)
    {
        if (DisplayServer.GetName() == "headless") return;
        var win = anyNode.GetWindow();
        switch (Window)
        {
            case WindowMode.Fullscreen: win.Mode = Godot.Window.ModeEnum.ExclusiveFullscreen; break;
            case WindowMode.Borderless: win.Mode = Godot.Window.ModeEnum.Fullscreen; break;
            default: if (win.Mode is Godot.Window.ModeEnum.Fullscreen or Godot.Window.ModeEnum.ExclusiveFullscreen) win.Mode = Godot.Window.ModeEnum.Windowed; break;
        }
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = MaxFps;
    }

    public static void ApplyRendering(Node anyNode)
    {
        var vp = anyNode.GetViewport();
        vp.Scaling3DMode = RenderScale < 0.99f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        vp.Scaling3DScale = Math.Clamp(RenderScale, 0.5f, 1f);
        vp.PositionalShadowAtlasSize = Shadows switch { Quality.Off => 0, Quality.Low => 2048, _ => 4096 };
        RenderingServer.PositionalSoftShadowFilterSetQuality(Shadows == Quality.High ? RenderingServer.ShadowQuality.SoftLow : RenderingServer.ShadowQuality.Hard);
        vp.ScreenSpaceAA = Aa == AntiAlias.Fxaa ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        vp.Msaa3D = Viewport.Msaa.Disabled;
        Stage3D.I?.ApplySettings();
        CreatureLibrary.SetInk(InkOutlines);
        ApplyCel();
    }

    /// <summary>The Music and Effects buses (made on first use), with the volumes applied.</summary>
    public static void ApplySound()
    {
        int Bus(string name)
        {
            int i = AudioServer.GetBusIndex(name);
            if (i >= 0) return i;
            AudioServer.AddBus();
            i = AudioServer.BusCount - 1;
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
            return i;
        }
        // (a slider follows the ear, not the waveform: its square is the amplitude, so half way is about half as loud (-12 dB) and a
        // tenth all but silent (-40 dB); straight amplitude left the top half of every slider sounding the same)
        void Set(int bus, float v)
        {
            AudioServer.SetBusMute(bus, v <= 0.001f);
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Math.Max(0.0001f, v * v)));
        }
        Set(0, MasterVolume);
        Set(Bus("Music"), MusicVolume);
        Set(Bus("SFX"), SfxVolume);
    }
}
