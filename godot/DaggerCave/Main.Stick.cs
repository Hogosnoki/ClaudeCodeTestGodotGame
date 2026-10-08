using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _stickSteps;

    /// <summary>
    /// `--scenario=stick`: a real controller stick pushed at every angle in between reads back at that angle (not bent toward the eight
    /// directions 45 degrees apart), a gentle push is a gentle move, a keyboard diagonal still runs at full speed, and a hero aiming with
    /// the stick aims there.
    /// </summary>
    private void StickScenario()
    {
        if (_stickSteps == null) { if (_scT < 0.6f) return; _stickSteps = StickRun().GetEnumerator(); }
        if (!_stickSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> StickRun()
    {
        void Axis(JoyAxis a, float v) => Input.ParseInputEvent(new InputEventJoypadMotion { Axis = a, AxisValue = v, Device = 0 });
        float worst = 0f; string seen = "";
        foreach (int deg in new[] { 5, 10, 15, 20, 30, 40, 50, 60, 70, 80, 85, 100, 135, 170, 200, 250, 290, 340 })
        {
            float a = Mathf.DegToRad(deg);
            Axis(JoyAxis.LeftX, MathF.Cos(a)); Axis(JoyAxis.LeftY, MathF.Sin(a));
            yield return null; yield return null;
            var m = Player.ReadMove();
            float got = Mathf.RadToDeg(MathF.Atan2(m.Y, m.X)); if (got < 0) got += 360f;
            float err = Math.Abs(Mathf.AngleDifference(a, Mathf.DegToRad(got)));
            float errDeg = Mathf.RadToDeg(err);
            worst = Math.Max(worst, errDeg);
            seen += $" {deg}->{got:0}";
        }
        Axis(JoyAxis.LeftX, 0f); Axis(JoyAxis.LeftY, 0f);
        ScCheck($"the stick reads at the angle it is pushed, at any angle (worst error {worst:0.0} degrees:{seen})", worst < 2f);
        // a gentle push
        Axis(JoyAxis.LeftX, 0.5f);
        yield return null; yield return null;
        var gentle = Player.ReadMove();
        Axis(JoyAxis.LeftX, 0f);
        ScCheck($"a half push is a gentler move than a full one ({gentle.X:0.00})", gentle.X > 0.2f && gentle.X < 0.6f);
        // drift
        Axis(JoyAxis.LeftX, 0.12f); Axis(JoyAxis.LeftY, -0.1f);
        yield return null; yield return null;
        var drift = Player.ReadMove();
        Axis(JoyAxis.LeftX, 0f); Axis(JoyAxis.LeftY, 0f);
        ScCheck($"a stick's rest drift is no move ({drift})", drift == Vector2.Zero);
        // keys: a diagonal at full strength on both axes
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Pressed = true });
        yield return null; yield return null;
        var keys = Player.ReadMove();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = false });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Pressed = false });
        yield return null;
        ScCheck($"a keyboard diagonal still runs at full speed ({keys})", Math.Abs(keys.X - 1f) < 0.01f && Math.Abs(keys.Y - 1f) < 0.01f);
        // the range of directions the stick can give
        var set = new HashSet<int>();
        for (int deg = 0; deg < 360; deg += 3) { var m = Player.ShapeStick(new Vector2(MathF.Cos(Mathf.DegToRad(deg)), MathF.Sin(Mathf.DegToRad(deg)))); set.Add((int)MathF.Round(Mathf.RadToDeg(MathF.Atan2(m.Y, m.X)) / 3f)); }
        ScCheck($"120 pushes round the circle give {set.Count} directions (not 8)", set.Count >= 115);
    }
}
