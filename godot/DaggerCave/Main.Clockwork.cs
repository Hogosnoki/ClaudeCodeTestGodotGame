using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _clkSteps;

    /// <summary>
    /// `--scenario=clockwork`: the Clockwork Deep. The level has pistons, belts, saws and lifts. A piston shudders and then slams, striking and
    /// stunning a hero under its head and no one beside it; a belt carries a hero standing on it; a saw on its rail cuts a hero in its way; a
    /// lift carries a hero up; steam scalds. An automaton winds down and stops to be rewound; a boiler charges and vents a jet; a cogwheel
    /// revs and drives through; the guardian erupts steam in columns. clockwork_*.png.
    /// </summary>
    private void ClockworkScenario()
    {
        if (_clkSteps == null) { if (_scT < 0.6f) return; _clkSteps = ClockworkRun().GetEnumerator(); }
        if (!_clkSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ClockworkRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 6000; p.Hp = 6000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        foreach (var r in cave.Rooms) r.Triggered = true;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        void Stand(Vector2 at) { p.GlobalPosition = at; p.Velocity = Vector2.Zero; }
        Vector2 FloorNear(Vector2 at) => cave.FindFloor(at, 120, out var f) ? f : at;

        // ---------------------------------------------------------------- what the generator laid
        var pistons = _world.GetChildren().OfType<Piston>().ToList();
        var belts = _world.GetChildren().OfType<Conveyor>().ToList();
        var saws = _world.GetChildren().OfType<SawBlade>().ToList();
        var lifts = _world.GetChildren().OfType<Lift>().ToList();
        ScCheck($"the works has pistons ({pistons.Count}), belts ({belts.Count}), saws ({saws.Count}) and lifts ({lifts.Count})", pistons.Count >= 3 && belts.Count >= 3 && saws.Count >= 2 && lifts.Count >= 2);
        // (clear the way for what is made here: the generator's own are put away)
        foreach (var n in pistons.Cast<Node>().Concat(belts).Concat(saws)) n.QueueFree();
        yield return null;
        // (a long level stretch of floor with clear air above it)
        bool ok = false; Vector2 run = default;
        for (float fx = 60; fx < cave.SizePx.X - 500 && !ok; fx += 120)
        {
            if (!FindRun(1, 460, false, out run, fx)) break;
            ok = cave.FindCeiling(run + new Vector2(60, -20), 200, out _) && cave.FindFloor(run + new Vector2(60, -20), 200, out var f1) && Math.Abs(f1.Y - (run.Y + 14)) < 3;
            if (!ok) fx = run.X + 60;
        }
        ScCheck($"a level gallery to work in ({run.Round()})", ok);
        if (!ok) yield break;
        var floorP = FloorNear(run + new Vector2(0, 20));

        // ---------------------------------------------------------------- the piston
        cave.FindCeiling(floorP + new Vector2(80, -20), 200, out var ceil);
        var pis = new Piston { Position = ceil + new Vector2(0, -2), Drop = floorP.Y - ceil.Y - 8f };
        _world.AddChild(pis);
        Stand(floorP + new Vector2(80, -14));
        float hp0 = p.Hp;
        for (float t = 0; t < 12f && !pis.Warning; t += (float)GetProcessDeltaTime()) yield return null;
        ScCheck("the piston shudders before it falls", pis.Warning);
        ScShot("clockwork_piston_warn");
        for (float t = 0; t < 3f && pis.Extension < 0.9f; t += (float)GetProcessDeltaTime()) yield return null;
        foreach (var _ in Wait(0.2f)) yield return null;
        ScShot("clockwork_piston_slam");
        ScCheck($"it struck a hero under its head ({hp0:0} -> {p.Hp:0}) and stunned them ({p.Stunned})", p.Hp < hp0 && p.Stunned);
        foreach (var _ in Wait(2.5f)) yield return null;
        p.TestClearStatus(); p.Hp = p.Stats.MaxHp;
        Stand(floorP + new Vector2(80 + 46, -14));
        hp0 = p.Hp;
        foreach (var _ in Wait(Piston.Total + 0.5f)) yield return null;
        ScCheck($"and no one beside it ({hp0:0} -> {p.Hp:0})", p.Hp >= hp0 - 1f);
        pis.QueueFree();

        // ---------------------------------------------------------------- the belt
        var belt = new Conveyor { Position = floorP + new Vector2(240, 0), Half = 70, Dir = 1 };
        _world.AddChild(belt);
        Stand(belt.Position + new Vector2(-30, -14));
        foreach (var _ in Wait(0.4f)) yield return null;
        float x0 = p.GlobalPosition.X;
        foreach (var _ in Wait(1.5f)) yield return null;
        ScShot("clockwork_belt");
        ScCheck($"a belt carries a hero standing on it ({p.GlobalPosition.X - x0:0} px in 1.5 s)", p.GlobalPosition.X - x0 > 40f);
        belt.Dir = -1;
        Stand(belt.Position + new Vector2(30, -14));
        foreach (var _ in Wait(0.4f)) yield return null;
        x0 = p.GlobalPosition.X;
        foreach (var _ in Wait(1.2f)) yield return null;
        ScCheck($"and the other way when it turns ({p.GlobalPosition.X - x0:0} px)", p.GlobalPosition.X - x0 < -30f);
        belt.QueueFree();

        // ---------------------------------------------------------------- the saw
        var a = floorP + new Vector2(60, -15);
        var b = a + new Vector2(150, 0);
        var saw = new SawBlade { Start = a, End = b, Position = a };
        _world.AddChild(saw);
        Stand(floorP + new Vector2(60 + 75, -14));
        hp0 = p.Hp;
        for (float t = 0; t < 8f && p.Hp >= hp0 - 0.5f; t += (float)GetProcessDeltaTime()) yield return null;
        ScShot("clockwork_saw");
        ScCheck($"a saw on its rail cuts a hero in its way ({hp0:0} -> {p.Hp:0})", p.Hp < hp0 - 0.5f);
        saw.QueueFree();
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- the lift
        var lift = lifts.FirstOrDefault(l => GodotObject.IsInstanceValid(l));
        if (lift != null)
        {
            Stand(new Vector2(lift.GlobalPosition.X, lift.Bottom - 14f));
            for (float t = 0; t < 30f && lift.Pos < 0.985f; t += (float)GetProcessDeltaTime()) yield return null;
            Stand(new Vector2(lift.GlobalPosition.X, lift.GlobalPosition.Y - 18f));
            foreach (var _ in Wait(0.3f)) yield return null;
            float y0 = p.GlobalPosition.Y;
            for (float t = 0; t < 6f; t += (float)GetProcessDeltaTime()) yield return null;
            ScShot("clockwork_lift");
            ScCheck($"a lift carries a hero up ({y0:0} -> {p.GlobalPosition.Y:0}, lift at {lift.GlobalPosition.Y:0}, standing {p.IsOnFloor()})", p.GlobalPosition.Y < y0 - 40f && p.GlobalPosition.Y > lift.GlobalPosition.Y - 40f);
        }

        // ---------------------------------------------------------------- the steam
        Stand(floorP + new Vector2(0, -14));
        p.TestClearStatus(); p.Hp = p.Stats.MaxHp;
        hp0 = p.Hp;
        Stand(floorP + new Vector2(110, -14));
        _world.AddChild(new SteamJet { Position = floorP + new Vector2(30, -14), Dir = Vector2.Right, Length = 140, Life = 2f });
        foreach (var _ in Wait(0.8f)) yield return null;
        ScShot("clockwork_steam");
        foreach (var _ in Wait(1.4f)) yield return null;
        ScCheck($"steam scalds ({hp0:0} -> {p.Hp:0})", p.Hp < hp0);
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- the automaton
        float oldWind = Tune.Clockwork.Automaton.Wind;
        Tune.Clockwork.Automaton.Wind = 3.5f;
        Stand(floorP + new Vector2(-30, -14));
        var au = new Automaton { Position = floorP + new Vector2(220, -14) };
        _world.AddChild(au);
        au.Wake();
        float minSpring = 1f; bool rewound = false;
        for (float t = 0; t < 20f && !rewound; t += (float)GetProcessDeltaTime())
        {
            Stand(new Vector2(floorP.X - 30, p.GlobalPosition.Y));
            minSpring = Math.Min(minSpring, au.Spring);
            if (au.Rewinding) { rewound = true; ScShot("clockwork_automaton_rewind"); }
            yield return null;
        }
        ScCheck($"an automaton winds down ({minSpring:0.00}) and stops to be rewound ({rewound})", rewound && minSpring < 0.1f);
        ScShot("clockwork_automaton");
        float dealt = 0;
        if (rewound) dealt = au.Hurt(10f, Vector2.Zero, au.GlobalPosition, DamageKind.Raw);
        // (a blow while it is running, for comparison)
        for (float t = 0; t < 8f && au.Rewinding; t += (float)GetProcessDeltaTime()) yield return null;
        foreach (var _ in Wait(0.6f)) yield return null;
        float runDealt = au.Hurt(10f, Vector2.Zero, au.GlobalPosition, DamageKind.Raw);
        ScCheck($"stopped, it takes half as much again ({dealt:0.0} against {runDealt:0.0})", dealt > runDealt * 1.3f);
        ScCheck($"and is rewound after ({au.Spring:0.00})", au.Spring > 0.6f);
        Tune.Clockwork.Automaton.Wind = oldWind;
        au.QueueFree();
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- the boiler
        Stand(floorP + new Vector2(0, -14));
        var boil = new Boiler { Position = floorP + new Vector2(130, -16) };
        _world.AddChild(boil);
        boil.Wake();
        hp0 = p.Hp;
        float topP = 0;
        for (float t = 0; t < 12f && !boil.Venting; t += (float)GetProcessDeltaTime()) { Stand(new Vector2(floorP.X, p.GlobalPosition.Y)); topP = Math.Max(topP, boil.Pressure); yield return null; }
        ScCheck($"a boiler builds pressure ({topP:0.00}) and vents ({boil.Venting})", boil.Venting && topP > 0.9f);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScShot("clockwork_boiler");
        bool jet = _world.GetChildren().OfType<SteamJet>().Any();
        foreach (var _ in Wait(1.3f)) yield return null;
        ScCheck($"in a jet of steam ({jet}) that scalds ({hp0:0} -> {p.Hp:0})", jet && p.Hp < hp0);
        boil.QueueFree();
        foreach (var j in _world.GetChildren().OfType<SteamJet>().ToArray()) j.QueueFree();
        p.Hp = p.Stats.MaxHp; p.TestClearStatus();

        // ---------------------------------------------------------------- the cogwheel
        Stand(floorP + new Vector2(0, -14));
        var cog = new Cogwheel { Position = floorP + new Vector2(200, -14) };
        _world.AddChild(cog);
        cog.Wake();
        bool revved = false, dashed = false;
        hp0 = p.Hp;
        for (float t = 0; t < 14f && !(revved && dashed); t += (float)GetProcessDeltaTime())
        {
            Stand(new Vector2(floorP.X, p.GlobalPosition.Y));
            revved |= cog.Revving;
            dashed |= Math.Abs(cog.Velocity.X) > 240f;
            if (cog.Revving) ScShot("clockwork_cog_rev");
            yield return null;
        }
        ScCheck($"a cogwheel rolls, revs ({revved}) and drives through ({dashed})", revved && dashed);
        cog.QueueFree();

        // ---------------------------------------------------------------- the guardian
        var room = cave.Boss;
        if (room != null)
        {
            Stand(room.Floor + new Vector2(-room.RxPx * 0.6f, -16));
            var g = (EngineWarden)Biomes.Get(BiomeId.Clockwork).Guardian(room);
            g.Position = room.Floor + new Vector2(room.RxPx * 0.2f, -60);
            _world.AddChild(g);
            int jets = 0;
            for (float t = 0; t < 26f && jets < 2; t += (float)GetProcessDeltaTime())
            {
                jets = Math.Max(jets, _world.GetChildren().OfType<SteamJet>().Count(j => j.Dir.Y < -0.9f));
                yield return null;
            }
            ScCheck($"the guardian sends up columns of steam ({jets})", jets >= 2);
            ScShot("clockwork_guardian");
            g.QueueFree();
        }
    }
}
