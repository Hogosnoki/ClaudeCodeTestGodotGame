using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _tcSteps;

    /// <summary>
    /// `--scenario=touch --touch` (the on-screen controls, UI/TouchControls.cs, driven by made-up touches): they're on screen in play;
    /// the left stick moves the hero; the jump button jumps; a tap on the attack button attacks; dragged off it, it aims (the hero
    /// turns that way); a controller's button hides them and a touch brings them back; the pause button pauses, and in the pause
    /// menu they're gone and a touch is a click again.
    /// </summary>
    private void TouchScenario()
    {
        if (_tcSteps == null) { if (_scT < 1.2f) return; _tcSteps = TouchRun().GetEnumerator(); }
        if (!_tcSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> TouchRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        // (the hero reads this machine's controls, as in a real game: the scenario's own made-up input is put away)
        p.InputOverride = null;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        // (a finger's touch comes in the window's pixels; the controls are laid out on the 1280x720 picture, scaled to the window)
        Vector2 Win(Vector2 at) => GetViewport().GetFinalTransform() * at;
        void Touch(int i, Vector2 at, bool down) => Input.ParseInputEvent(new InputEventScreenTouch { Index = i, Position = Win(at), Pressed = down });
        void Drag(int i, Vector2 at) => Input.ParseInputEvent(new InputEventScreenDrag { Index = i, Position = Win(at) });
        Vector2 Btn(string action) => TouchControls.Layout.FirstOrDefault(b => b.Action == action).At;
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"the on-screen controls are up in play ({TouchControls.Enabled}, shown {TouchControls.Shown}, {TouchControls.Layout.Count} buttons)", TouchControls.Shown && TouchControls.Layout.Count >= 8);
        ScCheck($"and a touch isn't also a mouse click meanwhile ({Input.EmulateMouseFromTouch})", !Input.EmulateMouseFromTouch);
        ScShot("touch_play");

        // ---- the stick: touch the left half and drag toward where the room is
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(60, -10)) ? -1 : 1;
        var origin = new Vector2(260, 560);
        float x0 = p.GlobalPosition.X;
        Touch(0, origin, true); yield return null;
        Drag(0, origin + new Vector2(dir * 70, 0)); yield return null;
        ScCheck($"the left stick is pushed ({TouchControls.Move.X:0.00})", TouchControls.Move.X * dir > 0.5f);
        foreach (var _ in Wait(0.25f)) yield return null;
        ScShot("touch_stick");
        foreach (var _ in Wait(0.35f)) yield return null;
        float moved = (p.GlobalPosition.X - x0) * dir;
        Touch(0, origin + new Vector2(dir * 70, 0), false); yield return null;
        ScCheck($"and the hero runs that way ({moved:0} px); let go, it stops ({TouchControls.Move})", moved > 30f && TouchControls.Move == Vector2.Zero);

        // ---- jump
        foreach (var _ in Wait(0.4f)) yield return null;
        float y0 = p.GlobalPosition.Y, top = y0;
        Touch(1, Btn("jump"), true);
        for (float t = 0; t < 0.35f; t += (float)GetProcessDeltaTime()) { top = Math.Min(top, p.GlobalPosition.Y); yield return null; }
        Touch(1, Btn("jump"), false);
        ScCheck($"the jump button jumps ({y0 - top:0} px up)", y0 - top > 20f);
        foreach (var _ in Wait(0.9f)) yield return null;

        // ---- a tap on attack
        bool swung = false;
        Touch(2, Btn("attack"), true);
        for (float t = 0; t < 0.3f; t += (float)GetProcessDeltaTime()) { swung |= p.IsSwinging; yield return null; }
        Touch(2, Btn("attack"), false);
        ScCheck($"a tap on the attack button attacks ({swung})", swung);
        foreach (var _ in Wait(0.6f)) yield return null;

        // ---- dragged off the attack button, it aims: the hero turns to face it
        var a = Btn("attack");
        float face0 = p.Facing;
        Touch(3, a, true); yield return null;
        Drag(3, a + new Vector2(-face0 * 90, 0)); yield return null;
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("touch_aim");
        ScCheck($"dragged off the attack button it aims and attacks that way (aim {TouchControls.AimStick.X:0.00}, facing {face0} -> {p.Facing})", TouchControls.AimStick.X * face0 < -0.5f && p.Facing == -face0);
        Touch(3, a + new Vector2(-face0 * 90, 0), false); yield return null;
        foreach (var _ in Wait(0.5f)) yield return null;

        // ---- a controller hides them; a touch brings them back
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = true }); yield return null;
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = false });
        foreach (var _ in Wait(0.1f)) yield return null;
        bool hid = !TouchControls.Shown;
        Touch(4, new Vector2(640, 300), true); yield return null; Touch(4, new Vector2(640, 300), false);
        foreach (var _ in Wait(0.1f)) yield return null;
        ScCheck($"a controller's button hides them ({hid}), and a touch brings them back ({TouchControls.Shown})", hid && TouchControls.Shown);

        // ---- pause
        Touch(5, Btn("pause"), true); yield return null; Touch(5, Btn("pause"), false);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("touch_pause");
        ScCheck($"the pause button pauses ({_state}); the controls are gone and a touch is a click again ({TouchControls.Shown}, {Input.EmulateMouseFromTouch})", _state == State.Paused && !TouchControls.Shown && Input.EmulateMouseFromTouch);
    }
}
