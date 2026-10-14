using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>"Report a problem" (the pause menu's button, or F6): the pause menu steps aside for the box.</summary>
    private void OpenReport()
    {
        _pauseMenu.Visible = false;
        SessionLog.Note("opened the problem report");
        _bugReport.Open();
    }

    /// <summary>The achievements page, from the pause menu (or the title's, see Main.Camp).</summary>
    private void OpenAchievements()
    {
        _pauseMenu.Visible = false;
        _achFromTitle = false;
        _achMenu.Open();
    }

    private bool _achFromTitle;

    private void OnAchievementsClosed()
    {
        if (_achFromTitle) { _achFromTitle = false; _mainMenu.Visible = true; return; }
        if (_state == State.Paused) _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
    }

    private void OnReportClosed()
    {
        if (_state == State.Paused) _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
    }
}
