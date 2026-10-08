using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _guardSteps;

    /// <summary>
    /// `--scenario=guardians`: the guardians' own tricks, one after another beside the hero. The Web-Mother spits silk that wraps the
    /// hero (struggling frees them, so does a friend's blade, so does enough harm); a guardian bear roars, stunning the hero, and rushes
    /// in for a swipe; the Brood Queen sprays venom (a little harm, a lot of poison); the Cavern Colossus charges to its arena's end,
    /// wheels round and charges back (never vanishing back to the middle). guardian_*.png show each wind-up.
    /// </summary>
    private void GuardiansScenario()
    {
        if (_guardSteps == null) { if (_scT < 0.6f) return; _guardSteps = GuardiansRun().GetEnumerator(); }
        if (!_guardSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GuardiansRun()
    {
        var p = G.Player;
        p.Stats.MaxHp = 5000; p.Hp = 5000;
        // (a press lasts one physics tick, as a real one does)
        p.InputOverride = () => { var i = _scInput; _scInput.Jump = false; _scInput.Attack = false; return i; };
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        bool found = FindRun(1, 220, false, out var stand);
        ScCheck($"a long floor to meet them on ({stand.Round()})", found);
        if (!found) yield break;
        void Home() { p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Facing = 1; p.TestClearStatus(); p.Hp = p.Stats.MaxHp; }
        void Clear() { foreach (var e in G.Enemies.ToArray()) if (GodotObject.IsInstanceValid(e)) e.QueueFree(); }
        Enemy Spawn(BiomeId b, float dx)
        {
            var e = Biomes.Get(b).Guardian(null);
            e.Position = stand + new Vector2(dx, -12);
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.Engage();
            return e;
        }

        // ---------------------------------------------------------------- the Web-Mother's silk
        Home();
        var spider = (Spider)Spawn(BiomeId.Entrance, 150);
        foreach (var _ in SbSleep(0.8f)) yield return null;
        spider.TestReadyWeb();
        bool shot = false;
        for (float t = 0; t < 4f && !p.Webbed; t += (float)GetProcessDeltaTime())
        {
            if (!shot && t > 0.5f) { shot = true; ScShot("guardian_web_windup"); }
            p.GlobalPosition = new Vector2(stand.X, p.GlobalPosition.Y);
            yield return null;
        }
        ScCheck($"the Web-Mother rears back and spits silk ({spider.WebsSpat} spat): the hero is wrapped in it ({p.Webbed})", spider.WebsSpat > 0 && p.Webbed);
        foreach (var _ in SbSleep(0.2f)) yield return null;
        ScShot("guardian_webbed");
        // wrapped, the hero goes nowhere
        float x0 = p.GlobalPosition.X;
        _scInput = new PlayerInput { Move = new Vector2(1, 0) };
        foreach (var _ in SbSleep(0.4f)) yield return null;
        _scInput = default;
        ScCheck($"webbed, the hero can't walk off ({Math.Abs(p.GlobalPosition.X - x0):0.0} px)", Math.Abs(p.GlobalPosition.X - x0) < 3f || !p.Webbed);
        // struggling: every press counts
        int presses = 0;
        for (; presses < 20 && p.Webbed; presses++)
        {
            _scInput = new PlayerInput { Jump = true };
            foreach (var _ in SbSleep(0.08f)) yield return null;
        }
        _scInput = default;
        ScCheck($"struggling ({presses} presses) the hero breaks free ({!p.Webbed})", !p.Webbed && presses <= Tune.Status.WebStruggle + 2);
        Clear();
        // a friend's blade cuts it
        Home();
        p.GiveWeb(10f, 999f);
        var friend = new Player { Stats = new PlayerStats(HeroKind.Swordsman), Position = p.GlobalPosition + new Vector2(-26, 0) };
        bool swing = true;
        friend.InputOverride = () => { var i = new PlayerInput { Attack = swing, Aim = new Vector2(1, 0), AimGiven = true }; swing = false; return i; };
        _world.AddChild(friend);
        foreach (var _ in SbSleep(1.2f)) yield return null;
        ScCheck($"a friend's sword through the web cuts the hero free ({!p.Webbed})", !p.Webbed);
        friend.QueueFree();
        // and enough harm tears it
        p.GiveWeb(10f, 10f);
        p.Hurt(14f, p.GlobalPosition + new Vector2(20, 0), 0f);
        ScCheck($"a blow hard enough tears it ({!p.Webbed})", !p.Webbed);
        foreach (var _ in SbSleep(0.3f)) yield return null;

        // ---------------------------------------------------------------- a guardian bear's roar
        Home();
        var bear = (Bear)Spawn(BiomeId.Den, 130);
        foreach (var _ in SbSleep(0.6f)) yield return null;
        bear.TestReadyRoar();
        shot = false;
        float hp0 = p.Hp;
        for (float t = 0; t < 3f && !p.Stunned; t += (float)GetProcessDeltaTime())
        {
            if (!shot && t > 0.45f) { shot = true; ScShot("guardian_roar_windup"); }
            yield return null;
        }
        ScCheck($"the bear rears and roars ({bear.Roars}): the hero is stunned ({p.Stunned})", bear.Roars > 0 && p.Stunned);
        ScShot("guardian_stunned");
        bool hit = false;
        for (float t = 0; t < 2.5f && !hit; t += (float)GetProcessDeltaTime()) { hit = p.Hp < hp0 - 1f; yield return null; }
        ScCheck($"and rushes in for its swipe while they reel (hp {hp0:0} -> {p.Hp:0})", hit);
        Clear();
        foreach (var _ in SbSleep(0.4f)) yield return null;

        // ---------------------------------------------------------------- the Brood Queen's venom
        Home();
        var queen = (Scorpion)Spawn(BiomeId.Nest, 110);
        foreach (var _ in SbSleep(0.6f)) yield return null;
        queen.TestReadySpray();
        shot = false;
        hp0 = p.Hp;
        for (float t = 0; t < 3f && !p.Poisoned; t += (float)GetProcessDeltaTime())
        {
            if (!shot && t > 0.55f) { shot = true; ScShot("guardian_spray_windup"); }
            yield return null;
        }
        ScShot("guardian_spray");
        ScCheck($"the Brood Queen arches her tail and sprays ({queen.Sprays}): little harm ({hp0 - p.Hp:0.0}) but poisoned ({p.Poisoned}, {p.PoisonLeft:0.0} s)",
            queen.Sprays > 0 && p.Poisoned && p.PoisonLeft > Tune.Scorpion.VenomSeconds - 1.5f && hp0 - p.Hp < 12f);
        Clear();
        foreach (var _ in SbSleep(0.4f)) yield return null;

        // ---------------------------------------------------------------- the Cavern Colossus's charge
        Home();
        var room = new Room { Kind = RoomKind.Boss, Center = stand + new Vector2(110, -90), Floor = stand + new Vector2(110, 14), RxPx = 230, RyPx = 140 };
        var col = new CavernColossus { Position = stand + new Vector2(-40, -40) };
        col.Init(room);
        col.SetMeta("test", true);
        _world.AddChild(col);
        p.GlobalPosition = stand + new Vector2(260, 0);
        foreach (var _ in SbSleep(2.2f)) yield return null;
        col.TestCharge(1);
        var last = col.GlobalPosition;
        float worst = 0f;
        shot = false;
        for (float t = 0; t < 7f && !col.Reeling; t += (float)GetProcessDeltaTime())
        {
            worst = Math.Max(worst, col.GlobalPosition.DistanceTo(last));
            last = col.GlobalPosition;
            if (!shot && col.Turning) { shot = true; ScShot("guardian_colossus_turn"); }
            yield return null;
        }
        ScCheck($"the Colossus charges to its arena's end, wheels round and charges back ({col.ChargesTurned} turned), and reels at the wall ({col.Reeling})", col.ChargesTurned > 0 && col.Reeling);
        ScCheck($"it never vanishes back to the middle (its largest step in a frame {worst:0} px)", worst < 60f);
        Clear();
    }
}
