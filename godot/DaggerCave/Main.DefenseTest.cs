using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _dfSteps;

    /// <summary>
    /// `--scenario=defense` (creatures that defend themselves, Enemies/Enemy.Defense.cs, and the boons that answer them): a goblin
    /// dodges a blow (nothing lands), a second blow in the dodge's window misses too, the next after it lands (the dodge is resting);
    /// Steady Hand at its full stops the dodging; Second Swing makes the strike after a dodge undodgeable and twice as hard. A golem
    /// blocks a blow from in front (its guard takes it), not one from behind; Sunder lets 30% of a blocked blow through; Rebuke makes
    /// the strike after a block twice as hard; a guard that takes more than it holds breaks, and its owner reels and blocks nothing.
    /// </summary>
    private void DefenseScenario()
    {
        if (_dfSteps == null) { if (_scT < 0.8f) return; _dfSteps = DefenseRun().GetEnumerator(); }
        if (!_dfSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> DefenseRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(200, -10)) ? -1 : 1;
        Enemy Spawn(Enemy e, float dx, float hp)
        {
            e.Position = p.GlobalPosition + new Vector2(dir * dx, -4);
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.Wake();
            e.Hp = e.MaxHp = hp;
            return e;
        }
        // (a blow as the hero's would land: knocked away from the hero)
        float Blow(Enemy e, float dmg) => e.Hurt(dmg, new Vector2(dir * 60f, 0), e.GlobalPosition);
        // (the goblin set back out of reach, so it's free to dodge rather than mid-attack)
        void Away(Enemy e) { e.GlobalPosition = p.GlobalPosition + new Vector2(dir * 300f, -4f); e.Velocity = Vector2.Zero; e.FaceToward(-dir); }
        ScCheck($"creatures defend themselves in this scenario ({Tune.EnemyDefense.On})", Tune.EnemyDefense.On);

        // ---- the quick dodge
        float chance0 = Tune.EnemyDefense.DodgeChance;
        Tune.EnemyDefense.DodgeChance = 1f;   // (every blow it can dodge, it does: the test is of what follows)
        var gob = Spawn(new Goblin(), 300, 100000f);
        gob.FaceToward(-dir);
        foreach (var _ in Wait(0.15f)) yield return null;
        ScCheck($"a goblin is quick, and a golem strong ({gob.Agile}, {new Golem().Guards})", gob.Agile);
        float hp0 = gob.Hp;
        float d1 = Blow(gob, 10f);
        float d2 = Blow(gob, 10f);
        ScCheck($"the goblin dodges a blow and the next in its window ({d1:0}, {d2:0}; hp {hp0:0} -> {gob.Hp:0}; dodges {gob.Dodges})", d1 == 0f && d2 == 0f && gob.Hp == hp0 && gob.Dodges == 1);
        foreach (var _ in Wait(0.45f)) yield return null;
        float d3 = Blow(gob, 10f);
        ScCheck($"once the window has passed, a blow lands while its dodge rests ({d3:0})", d3 > 5f);
        // Steady Hand at its fullest: no dodging at all
        p.Stats.DodgeDenied = 1f;
        foreach (var _ in Wait(Tune.EnemyDefense.DodgeCooldown + 0.2f)) yield return null;
        Away(gob);
        int dodges = gob.Dodges;
        float d4 = Blow(gob, 10f);
        ScCheck($"with the dodging denied, the rested goblin can't dodge ({d4:0}, dodges {dodges} -> {gob.Dodges})", d4 > 5f && gob.Dodges == dodges);
        // Second Swing: the strike after a dodge can't be dodged, and lands twice as hard
        p.Stats.DodgeDenied = 0f;
        p.Stats.DodgedNextMult = 1f;
        foreach (var _ in Wait(Tune.EnemyDefense.DodgeCooldown + 0.2f)) yield return null;
        Away(gob);
        float d5 = Blow(gob, 10f);
        foreach (var _ in Wait(0.45f)) yield return null;
        Away(gob);
        float d6 = Blow(gob, 10f);
        ScCheck($"after a dodge with Second Swing, the next strike lands and twice as hard ({d5:0}, then {d6:0} against {d3:0})", d5 == 0f && d6 > d3 * 1.7f);
        p.Stats.DodgedNextMult = 0f;
        Tune.EnemyDefense.DodgeChance = chance0;
        gob.QueueFree();

        // ---- the strong guard
        var golem = Spawn(new Golem(), 200, 1000000f);
        foreach (var _ in Wait(0.15f)) yield return null;
        // (a blow it can't guard, for the measure: turned away from the hero)
        // (twice: the very first blow on a creature can carry a bonus of its own)
        golem.FaceToward(dir);
        Blow(golem, 20f);
        float open = Blow(golem, 20f);
        golem.FaceToward(-dir);
        float g0 = golem.GuardLeft;
        float b1 = Blow(golem, 20f);
        ScCheck($"the golem blocks a blow from in front (dealt {b1:0} of an open {open:0}; guard {g0:0} -> {golem.GuardLeft:0})", open > 5f && b1 < 0.01f && golem.GuardLeft < g0 && golem.Blocks == 1);
        golem.FaceToward(dir);
        float b2 = Blow(golem, 20f);
        ScCheck($"but not one from behind ({b2:0})", b2 > open * 0.9f);
        golem.FaceToward(-dir);
        p.Stats.BlockPierce = 0.3f;
        float b3 = Blow(golem, 20f);
        ScCheck($"with Sunder, 30% of a blocked blow gets through ({b3:0}, want about {open * 0.3f:0})", Math.Abs(b3 - open * 0.3f) < open * 0.08f);
        p.Stats.BlockPierce = 0f;
        p.Stats.BlockedNextMult = 1f;
        Blow(golem, 20f);
        golem.FaceToward(dir);
        float b4 = Blow(golem, 20f);
        ScCheck($"with Rebuke, the strike after a block lands twice as hard ({b4:0} against {open:0})", b4 > open * 1.8f);
        p.Stats.BlockedNextMult = 0f;
        golem.QueueFree();

        // ---- the guard breaks
        var small = Spawn(new Golem(), 200, 400f);
        foreach (var _ in Wait(0.15f)) yield return null;
        small.FaceToward(-dir);
        float guard = small.GuardLeft;
        float big = Blow(small, 400f);
        ScCheck($"a blow bigger than its guard ({guard:0}) breaks it: the rest lands ({big:0}), it reels ({small.StunLeft:0.00} s), the guard is broken ({small.GuardBroken})",
            big > 0f && small.GuardBroken && small.StunLeft > 0.5f);
        float after = Blow(small, 10f);
        ScCheck($"and while it's broken, nothing is blocked ({after:0})", after > 0f);
        small.QueueFree();
    }
}
