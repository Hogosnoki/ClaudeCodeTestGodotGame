using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _orbSteps;

    /// <summary>`--scenario=orbs --hero=elementalist`: the staff holds two bolts and gains them back one at a time (Frostbolt: three, a second each); each spell's orb shows while it is ready.</summary>
    private void OrbsScenario()
    {
        if (_orbSteps == null) { if (_scT < 0.6f) return; _orbSteps = OrbRun().GetEnumerator(); }
        if (!_orbSteps.MoveNext()) ScEnd();
    }

    /// <summary>How many of the hero's orbs are showing: (hand orbs, ring orbs), and the colour of each by name.</summary>
    private string OrbsShowing(Player p, out int hands, out int ring)
    {
        hands = ring = 0;
        var m = p.Anim?.Model3D;
        if (m == null) return "(no model)";
        var sb = new System.Text.StringBuilder();
        var paths = new List<string> { "Pivot/Skeleton/StaffHand/OrbBolt0" };
        for (int k = 1; k < 8; k++) paths.Add("Pivot/Skeleton/OrbHandL/OrbBolt" + k);
        paths.AddRange(new[] { "Pivot/Skeleton/OrbRing/OrbWind", "Pivot/Skeleton/OrbRing/OrbEarth", "Pivot/Skeleton/OrbRing/OrbStorm" });
        foreach (var path in paths)
        {
            var n = m.GetNodeOrNull<Node3D>(path);
            if (n == null || !n.Visible) continue;
            if (path.Contains("OrbRing")) ring++; else hands++;
            var c = n.GetNode<MeshInstance3D>("Core").Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat ? mat.AlbedoColor : Colors.Black;
            sb.Append($"{path[(path.LastIndexOf('/') + 1)..]}#{c.ToHtml(false)} ");
        }
        return sb.ToString().Trim();
    }

    /// <summary>The colour of one hand orb's core (black if it isn't there).</summary>
    private static Color OrbCore(Player p, string path) =>
        p.Anim?.Model3D?.GetNodeOrNull<Node3D>("Pivot/Skeleton/" + path)?.GetNode<MeshInstance3D>("Core").Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat ? mat.AlbedoColor : Colors.Black;

    private IEnumerable<object> OrbRun()
    {
        var p = G.Player;
        p.Stats.MaxHp = 2000; p.Hp = 2000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.SetAlimus(p.Stats.AlimusMax);
        p.TestResetBolt();
        void Press(bool atk = false, bool dodge = false, bool ab = false, bool ab2 = false) => _scInput = new PlayerInput { Attack = atk, Dodge = dodge, Ability = ab, Ability2 = ab2, Aim = new Vector2(p.Facing, 0) };
        IEnumerable<object> Tap(bool atk = false, bool dodge = false, bool ab = false, bool ab2 = false)
        {
            Press(atk, dodge, ab, ab2); yield return null; _scInput = default; yield return null;
        }
        foreach (var _ in SbSleep(1.0f)) yield return null;
        GD.Print($"[scenario] frame {GetProcessDeltaTime():0.000}s, physics {Engine.PhysicsTicksPerSecond}/s, time scale {Engine.TimeScale}");
        ScCheck($"the staff starts with {Tune.Elementalist.FireCharges} bolts ({p.BoltCharges} of {p.BoltChargesMax})", p.BoltCharges == 2 && p.BoltChargesMax == 2);
        string s = OrbsShowing(p, out int hands, out int ring);
        ScCheck($"both hand orbs and the three circling orbs show, orange in the hands ({s})", hands == 2 && ring == 3 && OrbCore(p, "StaffHand/OrbBolt0") is var oc && oc.R > 0.8f && oc.G < 0.65f && oc.B < 0.3f);
        ScShot("orbs_full");

        Press(atk: true); yield return null;
        ScCheck($"a throw spends one ({p.BoltCharges} left, {p.BoltsCast} thrown)", p.BoltCharges == 1 && p.BoltsCast == 1);
        // (this harness runs at a few frames a second: a press on the very next frame is inside the quarter second)
        Press(atk: true); yield return null; _scInput = default;
        ScCheck($"a second throw on the very next frame is held back for the gap ({p.BoltsCast} thrown, {p.BoltCharges} left)", p.BoltsCast == 1 && p.BoltCharges == 1);
        foreach (var _ in SbSleep(0.3f)) yield return null;
        foreach (var _ in Tap(atk: true)) yield return null;
        ScCheck($"after the gap the second goes ({p.BoltsCast} thrown, {p.BoltCharges} left)", p.BoltsCast >= 2 && p.BoltCharges == 0);
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"the hand orbs have gone out ({hands} showing, {ring} circling)", hands == 0 && ring == 3);
        ScShot("orbs_spent");
        int thrown = p.BoltsCast;
        foreach (var _ in Tap(atk: true)) yield return null;
        ScCheck($"with none left the attack does nothing ({p.BoltsCast - thrown} more thrown)", p.BoltsCast == thrown);

        float t0 = 0f; int seen1 = 0;
        for (float t = 0; t < 4.2f && p.BoltCharges < 2; t += (float)GetProcessDeltaTime())
        {
            if (seen1 == 0 && p.BoltCharges == 1) { seen1 = 1; t0 = t; }
            yield return null;
        }
        ScCheck($"one comes back at a time, the first after about {Tune.Elementalist.FireEvery:0.0} s less what passed ({t0:0.00} s into the wait)", seen1 == 1);
        ScCheck($"and the second {Tune.Elementalist.FireEvery:0.0} s after it ({p.BoltCharges})", p.BoltCharges == 2);

        // the updraft, the storm and the snap each put out their own orb while recharging
        p.SetAlimus(p.Stats.AlimusMax);
        foreach (var _ in Tap(dodge: true)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        foreach (var _ in SbSleep(0.25f)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"the gray wind orb has shrunk away while the updraft recovers ({s})", !s.Contains("OrbWind"));
        foreach (var _ in Tap(ab: true)) yield return null;
        foreach (var _ in SbSleep(0.6f)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"after a blizzard the white storm orb is out for its twenty seconds ({s})", !s.Contains("OrbStorm"));
        ScShot("orbs_ring_out");
        foreach (var _ in SbSleep(1.0f)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"the wind orb is back, gray, by now ({s})", s.Contains("OrbWind#"));

        // the alterations
        p.Stats.Frostbolt = true; p.Stats.Firestorm = true;
        p.TestResetBolt();
        p.SetAlimus(p.Stats.AlimusMax);
        foreach (var _ in SbSleep(1.0f)) yield return null;
        ScCheck($"Frostbolt holds four ({p.BoltCharges} of {p.BoltChargesMax})", p.BoltCharges == 4 && p.BoltChargesMax == 4);
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"and the hand orbs are white now, four of them ({hands}): {s}", hands == 4 && OrbCore(p, "StaffHand/OrbBolt0") is var wc && wc.R > 0.85f && wc.G > 0.85f && wc.B > 0.85f);
        ScShot("orbs_frost");
        int b0 = p.BoltsCast;
        for (int k = 0; k < 4; k++)
        {
            foreach (var _ in Tap(atk: true)) yield return null;
        }
        ScCheck($"four frostbolts in a row ({p.BoltsCast - b0} thrown, {p.BoltCharges} left)", p.BoltsCast - b0 == 4 && p.BoltCharges <= 1);
        float tf = 0f;
        for (float t = 0; t < 2f && p.BoltCharges < 1; t += (float)GetProcessDeltaTime()) { tf = t; yield return null; }
        ScCheck($"one is back inside {Tune.Elementalist.FrostEvery:0.0} s ({tf:0.00} s later)", p.BoltCharges >= 1 && tf < Tune.Elementalist.FrostEvery + 0.1f);

        // the upgrades: Frozen Quiver (+2 frostbolts), Spare Bolt (+1 firebolt)
        p.Stats.FrostChargeBonus += 2;
        p.TestResetBolt();
        foreach (var _ in SbSleep(0.6f)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"Frozen Quiver: the staff holds {p.BoltChargesMax} frostbolts ({p.BoltCharges} ready, {hands} orbs)", p.BoltChargesMax == 6 && p.BoltCharges == 6 && hands == 6);
        ScShot("orbs_quiver");
        p.Stats.Frostbolt = false; p.Stats.FireChargeBonus += 1;
        p.TestResetBolt();
        foreach (var _ in SbSleep(0.6f)) yield return null;
        s = OrbsShowing(p, out hands, out ring);
        ScCheck($"Spare Bolt: three firebolts ({p.BoltChargesMax} held, {hands} orbs)", p.BoltChargesMax == 3 && p.BoltCharges == 3 && hands == 3);
        yield return null;
    }
}
