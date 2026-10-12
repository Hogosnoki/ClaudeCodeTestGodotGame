using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The lighting tones (Settings > Graphics: lighting blur and lighting tones): a full-screen pass over the lit 3D picture, under every
/// 2D layer (HUD, menus, effects drawn in 2D), that blurs it and divides its brightness into tones (cel_post.gdshader), the same two
/// dials the textures have, but after the lighting. It is hidden (costing nothing) when no dial (nor debug mode's red, green and blue gains) does anything or the stylised look
/// is off.
/// </summary>
public partial class CelPost : CanvasLayer
{
    private static CelPost _i;
    private ColorRect _rect;

    public CelPost() { Name = "CelPost"; Layer = -1; ProcessMode = ProcessModeEnum.Always; }

    public override void _Ready()
    {
        _i = this;
        _rect = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/cel_post.gdshader") },
        };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        Refresh();
    }

    public override void _ExitTree() { if (_i == this) _i = null; }

    /// <summary>Shown only while a dial does something (called whenever the settings change).</summary>
    public static void Refresh()
    {
        if (_i == null || !IsInstanceValid(_i)) return;
        bool rgb = Math.Abs(DebugTuning.V("scr_r") - 1f) > 0.002f || Math.Abs(DebugTuning.V("scr_g") - 1f) > 0.002f || Math.Abs(DebugTuning.V("scr_b") - 1f) > 0.002f;
        _i.Visible = rgb || (GameSettings.CelShading && (GameSettings.CelLightTones >= 1.5f || GameSettings.CelLightBlur > 0.01f));
    }
}
