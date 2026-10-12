using Godot;

namespace DaggerCave;

/// <summary>
/// Debug mode's way in: type "debug" at the character select screen (again to leave it). While the letters are typed they don't do what
/// their keys do (D would step the hero, E would carry on): the first D is held back, and if the word turns out not to be typed (the next
/// key isn't the next letter, or a second passes) its step is made after all. A badge shows while debug mode is on, with a button to
/// the Settings > Debug tab (also reached from the pause menu's Settings, or the main menu's).
/// </summary>
public partial class Main
{
    private const string DebugWord = "debug";
    private int _dbgIdx;
    private bool _dbgReplayStep;
    private ulong _dbgLastMs;
    private CanvasLayer _dbgBadge;
    private bool _dbgBackToHeroes;

    private static char DebugLetter(Key k) => k switch { Key.D => 'd', Key.E => 'e', Key.B => 'b', Key.U => 'u', Key.G => 'g', _ => '\0' };

    /// <summary>A key press at the character select: true when it is part of "debug" being typed (and so is swallowed).</summary>
    private bool DebugTyping(InputEventKey k)
    {
        bool choosing = _heroChoice != null && _heroChoice.Visible && _heroChoice.Stage == HeroChoice.StageKind.Hero && !Net.Online
            && !_settingsMenu.Visible && _departT < 0f && (_state == State.Title || (_state == State.Dead && _deadT > 1.5f));
        if (!choosing) { _dbgIdx = 0; _dbgReplayStep = false; return false; }
        char c = DebugLetter(k.Keycode);
        if (c != '\0' && c == DebugWord[_dbgIdx])
        {
            if (_dbgIdx == 0) _dbgReplayStep = k.IsActionPressed("move_right") || k.IsActionPressed("ui_right");
            _dbgIdx++;
            _dbgLastMs = Time.GetTicksMsec();
            if (_dbgIdx >= DebugWord.Length)
            {
                _dbgIdx = 0;
                _dbgReplayStep = false;
                ToggleDebug();
            }
            return true;
        }
        if (_dbgIdx > 0)
        {
            // not the word after all: the held-back first D does its step now, and this key is looked at afresh
            AbortDebugTyping();
            if (c == 'd') return DebugTyping(k);
        }
        return false;
    }

    private void AbortDebugTyping()
    {
        if (_dbgReplayStep) StepHero(1);
        _dbgIdx = 0;
        _dbgReplayStep = false;
    }

    private void DebugTypingTick()
    {
        if (_dbgIdx > 0 && Time.GetTicksMsec() - _dbgLastMs > 1000) AbortDebugTyping();
    }

    private void ToggleDebug()
    {
        DebugTuning.On = !DebugTuning.On;
        _sfx.Play(DebugTuning.On ? "card_take" : "card_leave", null, -4);
        if (_dbgBadge == null)
        {
            _dbgBadge = new CanvasLayer { Layer = 40, ProcessMode = ProcessModeEnum.Always };
            AddChild(_dbgBadge);
            var box = new HBoxContainer { Position = new Vector2(12, 8) };
            box.AddThemeConstantOverride("separation", 10);
            var l = UiKit.Label("DEBUG MODE", 15, new Color(1f, 0.55f, 0.35f));
            box.AddChild(l);
            box.AddChild(UiKit.Button("Look settings", OpenDebugSettings, 150));
            box.AddChild(UiKit.Label("type  debug  at the character select to leave", 12, UiKit.Dim));
            _dbgBadge.AddChild(box);
        }
        _dbgBadge.Visible = DebugTuning.On;
        GD.Print(DebugTuning.On ? "[debug] on" : "[debug] off");
    }

    /// <summary>The Debug tab of the settings (from the badge: at the camp, the main menu and hero choice step aside for it).</summary>
    private void OpenDebugSettings()
    {
        if (_settingsMenu.Visible) { _settingsMenu.ShowTab(3); return; }
        if (_state == State.Playing) return;
        if (_state == State.Title || _state == State.Dead)
        {
            // (the settings close back to the main menu at the title, and to the character select after a run)
            _dbgBackToHeroes = _heroChoice.Visible && _state == State.Dead;
            _mainMenu.Visible = false;
            _heroChoice.Visible = false;
        }
        _settingsMenu.OpenDebug();
    }
}
