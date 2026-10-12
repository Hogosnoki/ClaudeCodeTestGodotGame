using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The left stick in the menus: one step when it is pushed, then, if it is held, a pause and a steady repeat (like a held key), instead of a
/// step on every frame the stick reports. The engine's own stick-to-focus mapping (the ui_* actions had the stick's axes bound to them) moved
/// the cursor on every axis event, which is every frame; those bindings are taken off the ui_* actions (see UiKit.EnsureMenuControls) and
/// this node presses ui_up / ui_down / ui_left / ui_right itself, edge by edge. It acts only while a menu has the game (not while playing).
/// </summary>
public partial class MenuStick : Node
{
    /// <summary>Whether a menu has the game now (set by Main).</summary>
    public static Func<bool> Active = () => true;

    /// <summary>The push at which a step is made, and the lower one at which the stick counts as let go (so it doesn't flicker at the edge).</summary>
    private const float Push = 0.6f, Release = 0.35f;
    /// <summary>The pause before a held stick repeats, and the time between repeats.</summary>
    private const float FirstDelay = 0.4f, RepeatEvery = 0.12f;

    private float _x, _y, _wait;
    private int _dir;

    public override void _Ready()
    {
        Name = "MenuStick";
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventJoypadMotion m) return;
        if (m.Axis == JoyAxis.LeftX) _x = m.AxisValue;
        else if (m.Axis == JoyAxis.LeftY) _y = m.AxisValue;
    }

    private static readonly string[] Actions = { "", "ui_up", "ui_down", "ui_left", "ui_right" };

    public override void _Process(double delta)
    {
        float ax = Math.Abs(_x), ay = Math.Abs(_y);
        if (!Active())
        {
            _dir = 0;
            return;
        }
        float most = Math.Max(ax, ay);
        if (most < Release) { _dir = 0; return; }
        int d = most >= Push ? (ax > ay ? (_x < 0 ? 3 : 4) : (_y < 0 ? 1 : 2)) : _dir;
        if (d == 0) return;
        if (d != _dir)
        {
            // a new push: one step now, then the pause before it repeats
            _dir = d;
            _wait = FirstDelay;
            Press(d);
            return;
        }
        _wait -= (float)delta;
        if (_wait <= 0f)
        {
            _wait = RepeatEvery;
            Press(d);
        }
    }

    private static void Press(int d)
    {
        if (!InputMap.HasAction(Actions[d])) return;
        Input.ParseInputEvent(new InputEventAction { Action = Actions[d], Pressed = true, Strength = 1f });
        Input.ParseInputEvent(new InputEventAction { Action = Actions[d], Pressed = false });
    }
}
