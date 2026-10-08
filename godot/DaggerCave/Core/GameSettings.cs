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
    public enum AntiAlias { Off, Fxaa, Msaa2, Msaa4 }
    public static AntiAlias Aa = AntiAlias.Off;
    /// <summary>Exposure multiplier (1 = as designed).</summary>
    public static float Brightness = 1f;
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
        Aa = (AntiAlias)Math.Min(1, I("graphics", "aa", (int)Aa)); // (MSAA flickered against the ink outlines: any old choice of it is FXAA now)
        Brightness = F("graphics", "brightness", Brightness);
        Shake = F("graphics", "shake", Shake);
        MasterVolume = F("sound", "master", MasterVolume);
        MusicVolume = F("sound", "music", MusicVolume);
        SfxVolume = F("sound", "sfx", SfxVolume);
        Aim = (AimMode)I("controls", "aim", (int)Aim);
        Vibration = B("controls", "vibration", Vibration);
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
        cfg.SetValue("graphics", "aa", (int)Aa);
        cfg.SetValue("graphics", "brightness", Brightness);
        cfg.SetValue("graphics", "shake", Shake);
        cfg.SetValue("sound", "master", MasterVolume);
        cfg.SetValue("sound", "music", MusicVolume);
        cfg.SetValue("sound", "sfx", SfxVolume);
        cfg.SetValue("controls", "aim", (int)Aim);
        cfg.SetValue("controls", "vibration", Vibration);
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
