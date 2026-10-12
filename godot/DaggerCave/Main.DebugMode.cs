using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _dmSteps;

    /// <summary>`--scenario=debugmode`: dies to reach the character select, types "debug" (it must not step the hero or carry on), checks a lone D
    /// still steps the hero, opens the Debug tab. debugmode_*.png.</summary>
    private void DebugModeScenario()
    {
        if (_dmSteps == null) { if (_scT < 0.8f) return; _dmSteps = DebugModeRun().GetEnumerator(); }
        if (!_dmSteps.MoveNext()) ScEnd();
    }

    private static void PressKey(Viewport vp, Key key)
    {
        vp.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        vp.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private IEnumerable<object> DebugModeRun()
    {
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        G.Player?.GiveUp();
        for (float t = 0; t < 12f && !(_heroChoice.Visible && _state == State.Dead && _deadT > 1.6f); t += (float)GetProcessDeltaTime()) yield return null;
        ScCheck("the character select is up", _heroChoice.Visible);
        var hero = G.Hero;
        var stage = _heroChoice.Stage;
        // (all in one frame: this machine's frames are slow, a typist's letters are not)
        foreach (var k in new[] { Key.D, Key.E, Key.B, Key.U, Key.G }) PressKey(GetViewport(), k);
        yield return null;
        ScCheck("typing debug turns debug mode on", DebugTuning.On);
        ScCheck("typing it did not step the hero or move on a stage", G.Hero == hero && _heroChoice.Stage == stage);
        // a lone D still steps the hero (after a moment)
        PressKey(GetViewport(), Key.D);
        foreach (var _ in Wait(1.3f)) yield return null;
        ScCheck("a lone D still steps the hero", G.Hero != hero);
        // not the word: D then X
        var hero2 = G.Hero;
        PressKey(GetViewport(), Key.D);
        PressKey(GetViewport(), Key.X);
        yield return null;
        ScCheck("D then another key steps the hero at once", G.Hero != hero2);
        // the Debug tab
        OpenDebugSettings();
        foreach (var _ in Wait(0.6f)) yield return null;
        ScShot("debugmode_tab");
        ScCheck("the Debug tab is up", _settingsMenu.Visible);
        // the dump lists a changed value
        DebugTuning.Set("fg_bright", 1.4f);
        ScCheck("the dump marks a changed value", DebugTuning.Dump().Contains("fg_bright = 1.4   *"));
        DebugTuning.Reset();
        _settingsMenu.Visible = false;
        // typing it again leaves debug mode
        _heroChoice.Visible = true;
        foreach (var k in new[] { Key.D, Key.E, Key.B, Key.U, Key.G }) PressKey(GetViewport(), k);
        yield return null;
        ScCheck("typing it again turns debug mode off", !DebugTuning.On);
    }
}
