using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _rpSteps;

    /// <summary>`--scenario=report`: the seed on the HUD, the report box, and a report made (not sent): its file holds the seed and the map. report_*.png.</summary>
    private void ReportScenario()
    {
        if (_rpSteps == null) { if (_scT < 0.8f) return; _rpSteps = ReportRun().GetEnumerator(); }
        if (!_rpSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ReportRun()
    {
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        BugReports.OpenBrowser = false;
        foreach (var _ in Wait(0.5f)) yield return null;
        ScShot("report_hud");
        PauseGame();
        OpenReport();
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck("the report box is up", _bugReport.Visible);
        var rep = BugReports.Make("I spawned in a sealed enclosure.\nIt had no way out.", send: false);
        ScCheck("the report has the seed", rep.Full.Contains($"`{G.Cave.Seed}`") && rep.IssueBody.Contains($"`{G.Cave.Seed}`"));
        ScCheck("the report has the map, with the player on it", rep.Full.Contains("P") && rep.Full.Contains("## The whole map") && rep.IssueBody.Contains("```"));
        ScCheck("the report has the session, the party and the trail", rep.Full.Contains("## Session") && rep.Full.Contains("players now") && rep.Full.Contains("caves of this session") && rep.Full.Contains("entered Cave Entrance") && rep.IssueBody.Contains("| Party |") && rep.IssueBody.Contains("| Session |"));
        ScCheck("the report says whether the player is shut in", rep.Full.Contains("shut in") || rep.Full.Contains("Not shut in"));
        ScCheck("the report is saved", rep.Path != "" && Godot.FileAccess.FileExists(ProjectSettings.LocalizePath(rep.Path)) || System.IO.File.Exists(rep.Path));
        ScCheck($"the address is short enough for the default browser ({rep.Url.Length} of {BugReports.MaxUrl})", rep.Url.Length <= BugReports.MaxUrl && rep.Url.StartsWith("https://github.com/" + BugReports.Repo + "/issues/new"));
        GD.Print($"[report] url length {rep.Url.Length}, full report {rep.Full.Length} characters, file {rep.Path}");
        GD.Print(rep.IssueBody);
        ScShot("report_box");
    }
}
