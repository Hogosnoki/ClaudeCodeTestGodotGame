using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _riverSteps;

    /// <summary>
    /// `--scenario=river [--hero=shifter]`: the underground river. The current carries a swimmer back toward the beach; an ordinary swimmer
    /// pushing against it makes no way, one with a faster stroke does (so does the Elementalist's draft aimed along it, and a fish);
    /// the stepping stones are a dry way to the guardian; the hidden chamber under its plateau is reached only against the current;
    /// only bats live here. river_*.png.
    /// </summary>
    private void RiverScenario()
    {
        if (_riverSteps == null) { if (_scT < 0.6f) return; _riverSteps = RiverRun().GetEnumerator(); }
        if (!_riverSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> RiverRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 9000; p.Hp = 9000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        ScCheck($"this is the {G.Biome.Name}, its current runs toward the beach ({cave.Flow:0} px/s)", G.Biome.Id == BiomeId.River && cave.Flow < -50f);
        ScCheck($"there is a dry way to the guardian: stone to stone, no swimming ({cave.DryOk})", cave.DryOk);
        ScShot("river_start");

        // ---- the stones, and what is sunk in the river bed
        Vector2? stone = null;
        for (float sx = 2400f; sx < cave.Boss.Center.X - 800f && stone == null; sx += 16f)
            if (cave.FindFloor(new Vector2(sx, cave.WaterY - 200f), 400, out var fl) && fl.Y < cave.WaterY - 24f && cave.FindFloor(new Vector2(sx + 140f, cave.WaterY - 200f), 400, out var fl2) && fl2.Y < cave.WaterY - 8f) stone = fl;
        ScCheck($"a stepping stone stands out of the river ({stone?.Round()})", stone != null);
        if (stone != null)
        {
            p.GlobalPosition = stone.Value + new Vector2(0, -16); p.Velocity = Vector2.Zero;
            foreach (var _ in Wait(0.8f)) yield return null;
            ScShot("river_stones");
        }
        int sunk = _world.GetChildren().OfType<Chest>().Count(c => c.GlobalPosition.Y > cave.WaterY + 120f && c != null);
        ScCheck($"upgrades lie at the bottom of the river ({sunk} chests below the surface)", sunk >= 2);

        // ---- the current
        float y = cave.WaterY + 70f;
        Vector2? lane = null;
        for (float x = 2200f; x < cave.Boss.Center.X - 500f && lane == null; x += 20f)
        {
            var a = new Vector2(x - 260f, y); var b = new Vector2(x + 260f, y);
            if (cave.IsWater(a) && cave.IsWater(b) && cave.LineClear(a, b) && cave.LineClear(new Vector2(x, y), new Vector2(x, cave.WaterY + 4f))) lane = new Vector2(x, y);
        }
        ScCheck($"open water to swim in ({lane?.Round()})", lane != null);
        if (lane == null) yield break;
        var pt = lane.Value;
        ScCheck($"the current there: {cave.FlowAt(pt).X:0} px/s, none on the dry stones ({cave.FlowAt(cave.StartPos).X:0})",
            Math.Abs(cave.FlowAt(pt).X - cave.Flow) < 1f && cave.FlowAt(cave.StartPos) == Vector2.Zero);
        p.InputOverride = () => _scInput;
        float Swim(Vector2 move, float seconds, float swimMult, bool shot = false)
        {
            float was = p.Stats.SwimSpeed; p.Stats.SwimSpeed = swimMult;
            p.GlobalPosition = pt; p.Velocity = Vector2.Zero; p.Breath = p.Stats.BreathMax;
            _scInput = new PlayerInput { Move = move };
            return was;
        }
        IEnumerable<object> Run(Vector2 move, float seconds, float swimMult, Action<float> done, string shot = null)
        {
            float was = Swim(move, seconds, swimMult);
            foreach (var _ in Wait(0.25f)) yield return null;
            float x0 = p.GlobalPosition.X;
            bool shotDone = shot == null;
            for (float t = 0; t < seconds; t += (float)GetProcessDeltaTime()) { if (!shotDone && t > seconds * 0.5f) { shotDone = true; ScShot(shot); } p.Breath = p.Stats.BreathMax; yield return null; }
            float dx = p.GlobalPosition.X - x0;
            _scInput = default;
            p.Stats.SwimSpeed = was;
            done(dx / seconds);
        }
        float v = 0;
        foreach (var _ in Run(default, 1.5f, 1f, s => v = s, "river_drift")) yield return null;
        ScCheck($"left alone in it, a swimmer is carried back down the river ({v:0} px/s)", v < -Tune.River.Flow * 0.7f && v > -Tune.River.Flow * 1.3f);
        float pushed = 0;
        foreach (var _ in Run(new Vector2(1, 0), 1.5f, 1f, s => pushed = s)) yield return null;
        ScCheck($"swimming against it at an ordinary stroke makes no way ({pushed:0} px/s)", pushed < 8f);
        float fast = 0;
        foreach (var _ in Run(new Vector2(1, 0), 1.5f, 1.7f, s => fast = s)) yield return null;
        ScCheck($"a faster stroke (x1.7) does ({fast:0} px/s upstream)", fast > 25f);

        // ---- the Elementalist's draft is a wind in the water
        {
            float was = Swim(new Vector2(1, 0), 1f, 1f);
            var draft = new Updraft { Position = pt + new Vector2(-24, 0), Angle = MathF.PI / 2f, Width = 60f, Height = 200f, Life = 3f };
            G.Spawn(draft);
            foreach (var _ in Wait(0.6f)) yield return null;
            float vx = p.Velocity.X;
            draft.QueueFree();
            _scInput = default; p.Stats.SwimSpeed = was;
            ScCheck($"a draft aimed along the water pushes a swimmer in it upstream ({vx:0} px/s)", vx > 30f);
        }

        // ---- a Shape Shifter's fish makes its way against it
        if (p.IsShifter)
        {
            p.GlobalPosition = pt; p.Velocity = Vector2.Zero;
            p.EnterForm(ShiftForm.All.First(f => f.Key == "fish"));
            foreach (var _ in Wait(0.8f)) yield return null;
            p.GlobalPosition = pt;
            _scInput = new PlayerInput { Move = new Vector2(1, 0) };
            foreach (var _ in Wait(0.3f)) yield return null;
            float xs = p.GlobalPosition.X;
            foreach (var _ in Wait(1.5f)) yield return null;
            float fv = (p.GlobalPosition.X - xs) / 1.5f;
            _scInput = default;
            ScCheck($"as a fish she swims up it ({fv:0} px/s)", fv > 10f);
            p.LeaveForm(false);
        }

        // ---- the hidden chamber beneath the guardian's ground
        var sec = cave.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Secret);
        ScCheck($"a hidden chamber lies under the plateau ({sec?.Center.Round()}; the guardian's floor at {cave.Boss.Floor.Round()})", sec != null && sec.Center.X > cave.Tunnel.Position.X && sec.Center.Y > cave.Boss.Floor.Y + 40f);
        var chest = _world.GetChildren().OfType<Chest>().FirstOrDefault(c => sec != null && c.Tier == ChestTier.Relic && c.GlobalPosition.DistanceTo(sec.Center) < sec.RxPx + 40f);
        ScCheck($"with a silver chest in it ({chest?.GlobalPosition.Round()})", chest != null);
        var mouth = new Vector2(cave.Tunnel.Position.X - 60f, cave.Tunnel.Position.Y + cave.Tunnel.Size.Y * 0.5f);
        ScShot("river_mouth_before");
        IEnumerable<object> Tunnel(float swimMult, int seconds, Action<bool, float> done)
        {
            float was = p.Stats.SwimSpeed; p.Stats.SwimSpeed = swimMult;
            p.GlobalPosition = mouth; p.Velocity = Vector2.Zero;
            _scInput = new PlayerInput { Move = new Vector2(1, 0) };
            bool got = false; float t0 = 0;
            for (float t = 0; t < seconds && !got; t += (float)GetProcessDeltaTime())
            {
                p.Breath = p.Stats.BreathMax;
                got = sec != null && p.GlobalPosition.X > sec.Center.X - sec.RxPx * 0.4f;
                t0 = t;
                yield return null;
            }
            _scInput = default; p.Stats.SwimSpeed = was;
            done(got, t0);
        }
        bool inBase = true; float tb = 0;
        foreach (var _ in Tunnel(1f, 5, (g, t) => { inBase = g; tb = t; })) yield return null;
        ScCheck($"at an ordinary stroke the tunnel can't be swum ({inBase}, {p.GlobalPosition.X - mouth.X:0} px from the mouth)", !inBase);
        bool inFast = false; float tf = 0;
        foreach (var _ in Tunnel(2.0f, 24, (g, t) => { inFast = g; tf = t; })) yield return null;
        ScShot("river_chamber");
        ScCheck($"at a swimmer's stroke (x2) the chamber is reached against the water ({inFast}, {tf:0.0} s)", inFast);

        // ---- only bats
        var all = new List<Enemy>();
        foreach (var l in G.Biome.Residents.Values) foreach (var e in l) all.Add(e.Make());
        foreach (var l in new[] { G.Biome.GroundEntrants, G.Biome.AirEntrants, G.Biome.WaterEntrants }) foreach (var e in l) all.Add(e.Make());
        foreach (var m in G.Biome.MiniBosses.Concat(G.Biome.WaterMiniBosses)) all.Add(m());
        var guardian = G.Biome.Guardian(cave.Boss);
        all.Add(guardian);
        bool bats = all.All(e => e is Bat);
        ScCheck($"nothing lives here but bats ({all.Count} kinds checked{(bats ? "" : ", not: " + string.Join(", ", all.Where(e => e is not Bat).Select(e => e.GetType().Name).Distinct()))}, guardian {guardian.Title}, size x{guardian.Size:0.0})", bats && guardian.IsGuardian);
        foreach (var e in all) e.Free();
    }
}
