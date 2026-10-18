using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _achSteps;

    /// <summary>
    /// `--scenario=achievements`: the list (ids, kinds, the guardians it names), the hooks the game calls (a depth reached, a guardian slain,
    /// a big blow dealt, a blow past the whole health survived, a depth untouched, every chest opened, the dragon), the notice for a progress
    /// achievement and for a feat (ach_toast_*.png), the page's two tabs (ach_menu_*.png), and the save's round trip.
    /// </summary>
    private void AchievementsScenario()
    {
        if (_achSteps == null) { if (_scT < 0.8f) return; _achSteps = AchievementsRun().GetEnumerator(); }
        if (!_achSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> AchievementsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 100;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        Achievements.FromJson("");
        Achievements.NewRun();

        // ---- the list
        var all = Achievements.All;
        ScCheck($"{all.Count(d => d.Kind == AchKind.Progress)} progress achievements and {all.Count(d => d.Kind == AchKind.Feat)} feats, every id its own", all.Select(d => d.Id).Distinct().Count() == all.Count && all.Count(d => d.Kind == AchKind.Feat) >= 20);
        ScCheck("progress is party-wide and visible; every feat is hidden", all.Where(d => d.Kind == AchKind.Progress).All(d => d.Party && !d.Hidden) && all.Where(d => d.Kind == AchKind.Feat).All(d => d.Hidden));
        ScCheck($"a 'Defeat' for each of the {Achievements.Guardians.Length} guardians, and Conqueror", Achievements.Guardians.All(t => all.Any(d => d.Id.StartsWith("boss_") && d.Name.ToUpperInvariant().Contains(t.Replace("THE ", "")))) && Achievements.Find("conqueror") != null);
        var titles = new HashSet<string>();
        foreach (var b in Biomes.All.Where(b => b.Guardian != null && !b.NoGuardian))
            for (int k = 0; k < 12; k++) { try { var g = b.Guardian(null); if (!string.IsNullOrEmpty(g.Title)) titles.Add(g.Title); g.Free(); } catch { break; } }
        ScCheck($"every guardian the caves make is on the list ({string.Join(", ", titles.Where(t => Array.IndexOf(Achievements.Guardians, t) < 0))} missing)", titles.Count > 10 && titles.All(t => Array.IndexOf(Achievements.Guardians, t) >= 0));

        // ---- a depth reached, a guardian slain: progress, and its notice
        _achToast.Clear();
        int shown0 = _achToast.Shown;
        Achievements.EnteredLevel(3, Biomes.Get(BiomeId.Den), G.Cave, false);
        ScCheck($"reaching depth 3 earns depths 1 to 3 ({Achievements.Has("depth_1")}, {Achievements.Has("depth_3")}, not 4: {!Achievements.Has("depth_4")})", Achievements.Has("depth_1") && Achievements.Has("depth_2") && Achievements.Has("depth_3") && !Achievements.Has("depth_4"));
        foreach (var _ in Wait(0.6f)) yield return null;
        ScCheck($"and the notice slides in ({_achToast.Current?.Name})", _achToast.Shown > shown0 && _achToast.Current?.Kind == AchKind.Progress);
        ScShot("ach_toast_progress");
        Achievements.GuardianSlain("THE WEB-MOTHER", false, p.GlobalPosition, p);
        ScCheck("slaying the Web-Mother earns 'Defeat The Web-Mother!'", Achievements.Has("boss_webmother"));

        // ---- feats, through the game's own paths
        p.OnDealtDamage(620f);
        ScCheck("a 620 blow earns Devastating Blow!", Achievements.Has("heavy_hitter"));
        p.Stats.DeathWard = true; p.Hp = p.Stats.MaxHp; p.ClearInvuln();
        p.Hurt(350f, p.GlobalPosition + new Vector2(20, 0), 0f);
        ScCheck($"a 350 blow at 100 health, held off by a Death Ward: Too Stubborn to Die, and not Obliterated ({p.Hp:0}, dead {p.Dead})", !p.Dead && Achievements.Has("unbreakable") && !Achievements.Has("obliterated"));
        p.Stats.DeathWard = false; p.Hp = p.Stats.MaxHp;
        Achievements.TookDamage(150f, 100f, 100f, true, true, "hit");
        ScCheck("felled from full health by one blow: Obliterated!", Achievements.Has("obliterated"));
        for (float t = 0; t < 30f && _achToast.Current?.Kind != AchKind.Feat; t += (float)GetProcessDeltaTime()) yield return null;
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"a feat's notice comes in its turn, as a FEAT ({_achToast.Current?.Name}, {_achToast.Current?.Kind})", _achToast.Current?.Kind == AchKind.Feat);
        ScShot("ach_toast_feat");

        // ---- a depth: hurt means no Untouchable; chests left closed mean no Looter
        Achievements.LeavingLevel(3, Biomes.Get(BiomeId.Den), false, true, p);
        ScCheck($"leaving a depth after taking harm: no Untouchable ({Achievements.Has("untouchable")})", !Achievements.Has("untouchable"));
        ScCheck($"nor Power Hungry with shrines left closed ({Chest.All.Count(c => IsInstanceValid(c) && c.IsShrine && !c.Open)} closed)", !Achievements.Has("power_hungry") || Chest.All.All(c => !IsInstanceValid(c) || c.Open));
        Achievements.EnteredLevel(3, Biomes.Get(BiomeId.Den), G.Cave, false);
        var relic = new Chest { Position = p.GlobalPosition + new Vector2(60, 0), Tier = ChestTier.Relic };
        _world.AddChild(relic);
        yield return null;
        foreach (var c in Chest.All.Where(IsInstanceValid).ToList()) c.OpenBy(Net.Me);
        Achievements.LeavingLevel(3, Biomes.Get(BiomeId.Den), false, true, p);
        ScCheck("a depth untouched, every shrine and chest opened: Untouchable!, Power Hungry!, Looter!, Ransacked!", Achievements.Has("untouchable") && Achievements.Has("power_hungry") && Achievements.Has("looter") && Achievements.Has("ransacked"));

        // ---- over time: speed and gold
        p.Stats.MoveSpeed *= 2.05f; p.Gold = 310;
        Achievements.Tick(0.1f, p, null);
        ScCheck("double the hero's speed and 300 gold carried: Fleet of Foot, Hoarder", Achievements.Has("double_speed") && Achievements.Has("hoarder"));
        p.Stats.MoveSpeed /= 2.05f; p.Gold = 0;

        // ---- side-grades, a hero freed, the dragon
        foreach (var n in HexGrid.Nodes.Where(n => n.Kind == HexKind.SideGrade && n.Branch.Hero == HeroKind.Warden)) HexGrid.Owned.Add(n.Id);
        Achievements.CheckState();
        ScCheck("every Warden side-grade hex owned on the Ember Grid: Competent Warden!", Achievements.Has("competent_warden"));
        Achievements.Freed(HeroKind.Rogue);
        ScCheck("the Rogue freed: Freed the Rogue!", Achievements.Has("freed_rogue"));
        Achievements.GuardianSlain("THE ELDER DRAGON", true, p.GlobalPosition, p);
        ScCheck($"the dragon slain: Victory!, Victory as the {p.Hero}, Undefeatable!, Minimalist", Achievements.Has("victory") && Achievements.Has($"victory_{p.Hero.ToString().ToLowerInvariant()}") && Achievements.Has("undefeatable") && Achievements.Has("minimalist") && !Achievements.Has("long_way"));
        var (got, of) = Achievements.Completion(AchKind.Progress);
        ScCheck($"the game's completion counts progress only ({got} of {of})", of == all.Count(d => d.Kind == AchKind.Progress) && got == all.Count(d => d.Kind == AchKind.Progress && Achievements.Has(d.Id)));

        // ---- kept: the save's round trip; and the list for Steamworks
        string json = Achievements.ToJson();
        int earned = Achievements.Earned.Count;
        Achievements.FromJson(json);
        ScCheck($"what is earned survives the save ({earned} -> {Achievements.Earned.Count}; bosses {string.Join(",", Achievements.BossesSlain)})", Achievements.Earned.Count == earned && Achievements.BossesSlain.Contains("THE WEB-MOTHER") && Achievements.DeepestDepth == 3);
        string dump = Achievements.Dump();
        ScCheck("the Steamworks list names them in order (progress, then the hidden feats)", dump.Contains("`ACH_DEPTH_1`") && dump.IndexOf("`ACH_DEPTH_1`") < dump.IndexOf("`ACH_POWER_HUNGRY`") && dump.Contains("## Feats"));

        // ---- the page: two tabs
        foreach (var _ in Wait(9f)) yield return null;
        _achMenu.Open(0);
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"the page opens on Progress ({_achMenu.Rows} rows)", _achMenu.Visible && _achMenu.Tab == 0 && _achMenu.Rows == of);
        ScShot("ach_menu_progress");
        _achMenu.Open(1);
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"and Feats, its own tab ({_achMenu.Rows} rows)", _achMenu.Tab == 1 && _achMenu.Rows == all.Count(d => d.Kind == AchKind.Feat));
        ScShot("ach_menu_feats");
        _achMenu.Visible = false;
    }
}
