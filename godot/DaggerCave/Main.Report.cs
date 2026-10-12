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

    private void OnReportClosed()
    {
        if (_state == State.Paused) _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
    }
}
