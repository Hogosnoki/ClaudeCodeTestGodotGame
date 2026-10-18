using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _saveSteps;

    /// <summary>
    /// `--scenario=saves`: save files (in user://test_saves, emptied first): each keeps its own progress and achievements, switching reads
    /// the other afresh, a reset starts one over, a delete removes it, a save from before save files becomes the first one; and the page
    /// (saves_menu.png, saves_confirm.png).
    /// </summary>
    private void SavesScenario()
    {
        if (_saveSteps == null) { if (_scT < 0.8f) return; _saveSteps = SavesRun().GetEnumerator(); }
        if (!_saveSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> SavesRun()
    {
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        bool noSave = G.NoSave;
        G.NoSave = false;
        // (a save from before save files, at the old place, becomes the first save)
        const string root = "user://test_saves";
        DirAccess.MakeDirRecursiveAbsolute(root);
        Profiles.UseRoot(root);
        ScCheck($"a first save is made ({Profiles.All.Count}: {Profiles.Current?.Name})", Profiles.All.Count == 1 && Profiles.Current != null);
        var a = Profiles.Current;
        Meta.Embers = 17; Meta.Runs = 4; HexGrid.Owned.Add("pot_root"); Meta.Save();
        Achievements.Unlock("depth_1", quiet: true);
        var b = Profiles.Create("Second try");
        Profiles.Switch(b.Id);
        ScCheck($"another save starts from nothing (embers {Meta.Embers}, grid {HexGrid.Owned.Count(id => id != "aegis_oath")}, achievements {Achievements.Earned.Count})", Meta.Embers == 0 && !HexGrid.Owns("pot_root") && Achievements.Earned.Count == 0);
        ScCheck($"with the starting heroes only ({string.Join(", ", Meta.Unlocked)})", Meta.Unlocked.Count == 3);
        Meta.Embers = 3; Meta.Save();
        Achievements.Unlock("depth_2", quiet: true);
        Profiles.Switch(a.Id);
        ScCheck($"back to the first: its own progress ({Meta.Embers} embers, Apothecary {HexGrid.Owns("pot_root")}) and achievements (depth 1 {Achievements.Has("depth_1")}, depth 2 {Achievements.Has("depth_2")})",
            Meta.Embers == 17 && Meta.Runs == 4 && HexGrid.Owns("pot_root") && Achievements.Has("depth_1") && !Achievements.Has("depth_2"));
        var peek = Profiles.Peek(b.Id);
        ScCheck($"the page can tell what the other holds without loading it ({peek.embers} embers, {peek.earned} achievements)", peek.embers == 3 && peek.earned == 1);
        Profiles.Rename(b.Id, "Hard mode");
        ScCheck("a save can be renamed", Profiles.All.First(p => p.Id == b.Id).Name == "Hard mode");

        // the page
        _saveMenu.Open();
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"the page lists every save ({_saveMenu.Rows})", _saveMenu.Rows == 2);
        ScShot("saves_menu");

        Profiles.Reset(a.Id);
        ScCheck($"a reset starts the save in use over (embers {Meta.Embers}, achievements {Achievements.Earned.Count})", Meta.Embers == 0 && Achievements.Earned.Count == 0 && !HexGrid.Owns("pot_root"));
        Profiles.Switch(b.Id);
        ScCheck("and leaves the others be", Meta.Embers == 3 && Achievements.Has("depth_2"));
        Profiles.Delete(b.Id);
        ScCheck($"deleting the one in use goes back to another ({Profiles.All.Count} left, in use: {Profiles.Current?.Name})", Profiles.All.Count == 1 && Profiles.Active == a.Id && !DirAccess.DirExistsAbsolute(Profiles.DirOf(b.Id)));
        Profiles.Delete(a.Id);
        ScCheck($"deleting the last makes a fresh one ({Profiles.Current?.Name}: {Meta.Embers} embers)", Profiles.All.Count == 1 && Profiles.Current != null && Meta.Embers == 0 && Achievements.Earned.Count == 0);
        _saveMenu.Open();
        foreach (var _ in Wait(0.4f)) yield return null;
        _saveMenu.Visible = false;

        Profiles.UseRoot(root);
        G.NoSave = noSave;
    }
}
