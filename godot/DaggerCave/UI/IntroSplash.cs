using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The intro at launch: on black, the carved title (<see cref="TitleLogo"/>) grows in, its letters forming first and their spikes
/// and points growing out of them last; it holds a moment, then the black and the title fade away to the camp fire and the main menu
/// behind. Any key, click or button skips straight to the fade. It sits over everything and lets nothing through until it fades.
/// </summary>
public partial class IntroSplash : CanvasLayer
{
    /// <summary>Black before the title starts to grow, how long it grows, how long it holds, how long it fades (seconds).</summary>
    public const float Lead = 0.5f, GrowTime = 3.4f, Hold = 1.3f, FadeTime = 1.1f;

    private Control _root;
    private TitleLogo _logo;
    private float _t;
    private float _fadeAt = Lead + GrowTime + Hold;
    private bool _started;

    /// <summary>Called once the intro has faded away (it frees itself).</summary>
    public Action Done;

    /// <summary>How far along it is (seconds since it began), for the screenshot run.</summary>
    public float T => _t;

    public override void _Ready()
    {
        Layer = 120;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(black);
        // the title, big, a little above the middle (as wide as fits both ways)
        var vs = GetViewport().GetVisibleRect().Size;
        float width = MathF.Min(vs.X * 0.74f, vs.Y * 1.5f);
        _logo = TitleLogo.Create(width, 0f, 0f);
        if (_logo == null) { _fadeAt = 0.2f; return; }
        var h = _logo.CustomMinimumSize.Y;
        _logo.Position = new Vector2((vs.X - width) * 0.5f, (vs.Y - h) * 0.46f);
        _root.AddChild(_logo);
        _logo.Grow(GrowTime);
        _logo.ProcessMode = ProcessModeEnum.Disabled; // (still, until the lead-in's black has had its moment)
    }

    public override void _Process(double delta)
    {
        // (the first frames after launch can be long, the camp still settling: don't let them eat the lead-in)
        _t += MathF.Min((float)delta, 1f / 20f);
        if (!_started && _t >= Lead && _logo != null) { _started = true; _logo.ProcessMode = ProcessModeEnum.Always; }
        if (_t < _fadeAt) return;
        float f = Math.Clamp((_t - _fadeAt) / FadeTime, 0f, 1f);
        _root.Modulate = new Color(1f, 1f, 1f, 1f - f * f * (3f - 2f * f));
        // (the menu under it takes the mouse again as soon as it starts to clear)
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (f >= 1f)
        {
            Done?.Invoke();
            QueueFree();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_t >= _fadeAt) return;
        bool press = e is InputEventKey { Pressed: true, Echo: false } || e is InputEventMouseButton { Pressed: true } || e is InputEventJoypadButton { Pressed: true };
        if (!press) return;
        Skip();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Straight to the fade, the title whole.</summary>
    public void Skip()
    {
        if (_t >= _fadeAt) return;
        _logo?.FinishGrow();
        if (_logo != null) _logo.ProcessMode = ProcessModeEnum.Always;
        _started = true;
        _fadeAt = _t;
    }
}
