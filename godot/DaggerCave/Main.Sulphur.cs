using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _sulSteps;

    /// <summary>
    /// `--scenario=sulphur`: the Sulphur Springs, start to finish. A vent hisses (its warning) and then blows clouds that hang about and
    /// poison a hero standing in them; a fire lights a cloud and the blast hurts the hero in it and runs through the clouds beside it;
    /// a gasbag hangs over a hero and lets a cloud go, bursts into one when struck, and goes up if lit; a worm lunges out of its hole
    /// and bites; a newt spits acid that lands as a pool; the guardian breathes out a ring of clouds. sulphur_*.png.
    /// </summary>
    private void SulphurScenario()
    {
        if (_sulSteps == null) { if (_scT < 0.6f) return; _sulSteps = SulphurRun().GetEnumerator(); }
        if (!_sulSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> SulphurRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 4000; p.Hp = 4000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        foreach (var r in cave.Rooms) r.Triggered = true;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        void Stand(Vector2 at) { p.GlobalPosition = at; p.Velocity = Vector2.Zero; }
        Vector2 FloorNear(Vector2 at) => cave.FindFloor(at + new Vector2(0, -24), 120, out var f) ? f : at;

        // ---------------------------------------------------------------- the vents
        var vents = _world.GetChildren().OfType<GasVent>().OrderBy(v => v.GlobalPosition.DistanceTo(cave.StartPos)).ToList();
        ScCheck($"the level has gas vents ({vents.Count})", vents.Count >= 4);
        if (vents.Count == 0) yield break;
        // (one with open air over it and room to stand beside it, out of the water; the clouds already out cleared, so the level's cap can't starve it)
        bool Fair(GasVent v) => !cave.IsWater(v.GlobalPosition + new Vector2(0, -12)) && !cave.IsSolid(v.GlobalPosition + new Vector2(0, -40))
            && !cave.IsSolid(v.GlobalPosition + new Vector2(-90, -14)) && !cave.IsSolid(v.GlobalPosition + new Vector2(-90, -28));
        var vent = vents.FirstOrDefault(Fair) ?? vents[0];
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();
        Stand(vent.GlobalPosition + new Vector2(-90, -14));
        foreach (var _ in Wait(0.4f)) yield return null;
        for (float t = 0; t < 14f && !vent.Warning; t += (float)GetProcessDeltaTime()) yield return null;
        ScCheck($"it hisses before it blows ({vent.Cycle:0.0}s into its cycle)", vent.Warning);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("sulphur_vent_warn");
        for (float t = 0; t < 4f && !vent.Blowing; t += (float)GetProcessDeltaTime()) yield return null;
        foreach (var _ in Wait(1.6f)) yield return null;
        int clouds = GasCloud.All.Count(c => c.GlobalPosition.DistanceTo(vent.GlobalPosition) < 120);
        ScCheck($"it blows clouds that stay about it ({clouds})", clouds >= 2);
        ScShot("sulphur_vent_blow");

        // ---------------------------------------------------------------- the acid, and the toxic air over it
        ScCheck($"the water lies in the lowest quarter of the map (at {cave.WaterY / cave.SizePx.Y:0.00} of its depth)", cave.WaterY / cave.SizePx.Y > 0.7f);
        Vector2? acidAt = null, toxicAt = null;
        for (float x = 200; x < cave.SizePx.X - 200 && (acidAt == null || toxicAt == null); x += 24)
        {
            var w = new Vector2(x, cave.WaterY + 26);
            if (acidAt == null && !cave.IsSolid(w) && !cave.IsSolid(w + new Vector2(0, -20)) && cave.IsWater(w)) acidAt = w;
            if (toxicAt == null && cave.FindFloor(new Vector2(x, cave.WaterY - 120), 110, out var tf) && cave.IsToxic(tf + new Vector2(0, -14)) && !cave.IsWater(tf + new Vector2(0, -4))) toxicAt = tf;
        }
        ScCheck($"the springs have water to be burned in ({acidAt}) and air to choke on ({toxicAt})", acidAt != null && toxicAt != null);
        if (acidAt != null)
        {
            p.TestClearStatus(); p.Hp = p.Stats.MaxHp;
            Stand(acidAt.Value);
            float hpA = p.Hp;
            foreach (var _ in Wait(1.3f)) yield return null;
            ScShot("sulphur_acid");
            ScCheck($"the water is acid: a hero in it is burned ({hpA:0} -> {p.Hp:0})", p.Hp < hpA - 1f);
            Stand(vent.GlobalPosition + new Vector2(-90, -14));
            p.Hp = p.Stats.MaxHp;
        }
        if (toxicAt != null)
        {
            p.Hp = p.Stats.MaxHp; p.Breath = p.Stats.BreathMax;
            Stand(toxicAt.Value + new Vector2(0, -14));
            foreach (var _ in Wait(2.5f)) yield return null;
            ScShot("sulphur_toxic");
            ScCheck($"the lowest air is toxic: the hero's breath drains in it ({p.Breath:0.0} of {p.Stats.BreathMax:0})", p.Breath < p.Stats.BreathMax - 2f);
            Stand(vent.GlobalPosition + new Vector2(-90, -14));
            foreach (var _ in Wait(3f)) yield return null;
            ScCheck($"and comes back out of it ({p.Breath:0.0})", p.Breath > p.Stats.BreathMax - 3f);
            p.Breath = p.Stats.BreathMax;
        }

        // ---------------------------------------------------------------- the gas poisons
        p.TestClearStatus();
        var cloud = GasCloud.All.OrderBy(c => c.GlobalPosition.DistanceTo(vent.GlobalPosition)).FirstOrDefault();
        if (cloud != null) Stand(cloud.GlobalPosition + new Vector2(0, 8));
        float hp0 = p.Hp;
        foreach (var _ in Wait(1.6f)) yield return null;
        ScCheck($"a hero standing in it is poisoned (poison left {p.PoisonLeft:0.0}s, health {hp0:0} -> {p.Hp:0})", p.PoisonLeft > 0.5f);
        ScShot("sulphur_poisoned");
        p.TestClearStatus();
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- fire lights it
        // (a run of open floor away from the vents, so nothing else is blowing)
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();
        yield return null;
        bool found = false; Vector2 run = default;
        for (float fx = 200; fx < cave.SizePx.X - 400 && !found; fx += 160)
        {
            if (!FindRun(1, 380, false, out run, fx)) break;
            found = vents.All(v => v.GlobalPosition.DistanceTo(run) > 900) || fx > cave.SizePx.X * 0.6f;
            if (!found) fx = run.X + 200;
        }
        if (!found) FindRun(1, 380, false, out run, 200f);
        var spot = FloorNear(run + new Vector2(0, 20));
        Stand(spot + new Vector2(0, -14));
        var a = new GasCloud { Position = spot + new Vector2(0, -26), Radius = 40, Life = 30f };
        var b = new GasCloud { Position = spot + new Vector2(60, -30), Radius = 40, Life = 30f };
        var c3 = new GasCloud { Position = spot + new Vector2(120, -34), Radius = 40, Life = 30f };
        G.Spawn(a); G.Spawn(b); G.Spawn(c3);
        foreach (var _ in Wait(0.8f)) yield return null;
        ScCheck("clouds hang unlit until a fire touches them", !a.Lit && !b.Lit);
        // (a frost bolt does nothing to it, a fire bolt does)
        ScCheck($"a spark far off lights nothing ({GasCloud.IgniteAt(spot + new Vector2(0, -400), 8f)})", GasCloud.IgniteAt(spot + new Vector2(0, -400), 8f) == 0);
        float before = p.Hp;
        int lit = GasCloud.IgniteAt(a.GlobalPosition, 6f);
        ScCheck($"a fire in a cloud lights it ({lit})", lit >= 1 && a.Lit);
        foreach (var _ in Wait(0.12f)) yield return null;
        ScShot("sulphur_blast");
        foreach (var _ in Wait(0.9f)) yield return null;
        ScCheck($"the blast hurt the hero in it and was burning ({before:0} -> {p.Hp:0}, burning {p.BurnLeft:0.0}s)", p.Hp < before && p.BurnLeft > 0f);
        ScCheck($"and ran through the clouds beside it ({GasCloud.All.Count(c => c == a || c == b || c == c3 )} left)", !GodotObject.IsInstanceValid(b) && !GodotObject.IsInstanceValid(c3));
        p.TestClearStatus();
        p.Hp = p.Stats.MaxHp;
        foreach (var _ in Wait(0.5f)) yield return null;

        // ---------------------------------------------------------------- the gasbag
        var floor = spot;
        Stand(floor + new Vector2(0, -14));
        var gb = new Gasbag { Position = floor + new Vector2(70, -90) };
        _world.AddChild(gb);
        gb.Wake();
        foreach (var _ in Wait(1.0f)) yield return null;
        ScShot("sulphur_gasbag");
        float nearest = float.MaxValue;
        int cloudsBefore = GasCloud.All.Count;
        for (float t = 0; t < 14f && GasCloud.All.Count <= cloudsBefore; t += (float)GetProcessDeltaTime())
        {
            nearest = Math.Min(nearest, gb.GlobalPosition.DistanceTo(p.GlobalPosition));
            yield return null;
        }
        ScCheck($"a gasbag comes over the hero (within {nearest:0} px) and lets a cloud go ({GasCloud.All.Count - cloudsBefore} new)", nearest < 140f && GasCloud.All.Count > cloudsBefore);
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();
        yield return null;
        Stand(p.GlobalPosition + new Vector2(-400, 0));
        gb.GlobalPosition = floor + new Vector2(70, -90);
        int n0 = GasCloud.All.Count;
        gb.Pop(false);
        yield return null;
        ScCheck($"struck down, a gasbag bursts into a cloud ({GasCloud.All.Count - n0}), unlit", gb.Dead && GasCloud.All.Count > n0 && GasCloud.All.All(c => !c.Lit));
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();
        var gb2 = new Gasbag { Position = floor + new Vector2(70, -90) };
        _world.AddChild(gb2);
        yield return null;
        GasCloud.IgniteAt(gb2.GlobalPosition, 6f);
        yield return null;
        ScCheck($"a fire among gasbags sets them off, their gas with them ({gb2.Dead}, unlit clouds of its left {GasCloud.All.Count(c => c.Source == gb2 && !c.Lit)})", gb2.Dead && !GasCloud.All.Any(c => c.Source == gb2 && !c.Lit));
        foreach (var _ in Wait(0.6f)) yield return null;
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();

        // ---------------------------------------------------------------- the worm
        Stand(floor + new Vector2(0, -14));
        var wfloor = FloorNear(floor + new Vector2(70, 0));
        var worm = new BrimstoneWorm { Position = wfloor + new Vector2(0, -10) };
        _world.AddChild(worm);
        worm.Wake();
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck("a worm lies hid in its hole (no one can strike it)", !worm.CanBeHit);
        ScShot("sulphur_worm_hidden");
        bool out_ = false;
        hp0 = p.Hp;
        for (float t = 0; t < 8f && !out_; t += (float)GetProcessDeltaTime()) { out_ = worm.CanBeHit; yield return null; }
        ScCheck($"come near and it strikes out ({out_})", out_);
        foreach (var _ in Wait(0.25f)) yield return null;
        ScShot("sulphur_worm_out");
        foreach (var _ in Wait(1.8f)) yield return null;
        ScCheck($"and draws back, leaving a puff of gas ({!worm.CanBeHit}, clouds {GasCloud.All.Count})", !worm.CanBeHit && GasCloud.All.Count >= 1);
        worm.QueueFree();
        foreach (var c in GasCloud.All.ToArray()) c.QueueFree();

        // ---------------------------------------------------------------- the newt
        p.TestClearStatus(); p.Hp = p.Stats.MaxHp;
        Stand(floor + new Vector2(0, -14));
        var nfloor = FloorNear(floor + new Vector2(190, 0));
        var newt = new AcidNewt { Position = nfloor + new Vector2(0, -12) };
        _world.AddChild(newt);
        newt.Wake();
        bool spat = false, puddle = false;
        for (float t = 0; t < 12f && !(spat && puddle); t += (float)GetProcessDeltaTime())
        {
            spat |= G.Main.EnemyProjectiles.Any(q => q.Kind == "acid");
            puddle |= _world.GetChildren().OfType<AcidPuddle>().Any();
            if (spat && t > 0 && Math.Abs(t % 3f) < 0.02f) ScShot("sulphur_newt");
            yield return null;
        }
        ScCheck($"a newt spits acid ({spat}) that lands as a pool ({puddle})", spat && puddle);
        ScShot("sulphur_newt_pool");
        newt.QueueFree();

        // ---------------------------------------------------------------- the guardian
        var room = cave.Boss;
        if (room != null)
        {
            Stand(room.Floor + new Vector2(-room.RxPx * 0.6f, -16));
            var g = (VentColossus)Biomes.Get(BiomeId.Sulphur).Guardian(room);
            g.Position = room.Floor + new Vector2(room.RxPx * 0.2f, -60);
            _world.AddChild(g);
            for (float t = 0; t < 22f && GasCloud.All.Count < 3; t += (float)GetProcessDeltaTime()) yield return null;
            ScCheck($"the guardian breathes out a ring of clouds ({GasCloud.All.Count})", GasCloud.All.Count >= 3);
            ScShot("sulphur_guardian");
            g.QueueFree();
        }
    }
}
