using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _bmSteps;

    /// <summary>
    /// `--scenario=bossmoves`: every boss's great moves (Enemies/Enemy.BossMoves.cs). A boss of each family (made a guardian as the biomes
    /// make them) is set down beside a sturdy hero and made to do each of its moves in turn: a striking move must hurt the hero, a Summon
    /// must raise its brood, a Blink must move it, a Guard must turn most of a blow aside. Then it is enraged below 40% health.
    /// Pictures of each move mid wind-up (bossmoves_&lt;move&gt;).
    /// </summary>
    private void BossMovesScenario()
    {
        if (_bmSteps == null) { if (_scT < 0.8f) return; _bmSteps = BossMovesRun().GetEnumerator(); }
        if (!_bmSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> BossMovesRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        _genSpots.Clear();
        p.Stats.MaxHp = p.Hp = 50000;
        p.InputOverride = () => default;
        foreach (var _ in ElWait(0.5f)) yield return null;
        var home = p.GlobalPosition;
        if (G.Cave.FindFloor(home + new Vector2(0, -20), 120, out var hf)) home = hf - new Vector2(0, 12);
        var bosses = new (string Name, Func<Enemy> Make)[]
        {
            ("Web-Mother", () => new Spider { Grounded = true }), ("Goblin Warchief", () => new Goblin()), ("Den Mother", () => new Bear()),
            ("Brood Queen", () => new Scorpion()), ("Bone Knight", () => new Skeleton()), ("Ore Golem", () => new Golem()),
            ("Echo Queen", () => new Bat()), ("Cavern Colossus", () => new CavernColossus()),
        };
        var shots = new HashSet<BMove>();
        var bad = new List<string>();
        int good = 0, total = 0;
        foreach (var (name, make) in bosses)
        {
            var e = make();
            if (e is CavernColossus c) c.Init(new Room { Center = home, Floor = home + new Vector2(0, 10), RxPx = 400, RyPx = 90 });
            else { e.MakeElite(); e.IsGuardian = true; e.MaxHp *= 2.5f; }
            e.Title = name.ToUpperInvariant();
            var spot = home + new Vector2(130, -30);
            if (G.Cave.FindFloor(spot + new Vector2(0, -30), 200, out var fl)) spot = fl - new Vector2(0, 24);
            e.Position = spot;
            e.Engage();
            _world.AddChild(e);
            foreach (var _ in ElWait(0.4f)) yield return null;
            var moves = e.BossMoves.ToList();
            var line = new List<string>();
            foreach (var m in moves)
            {
                total++;
                if (!IsInstanceValid(e) || e.Dead) { bad.Add($"{name}: gone before {m}"); continue; }
                // (a fair start for each: the hero back home and whole, the boss beside them, its brood gone)
                for (float t = 0; t < 4f && e.InBossMove; t += (float)GetProcessDeltaTime()) yield return null;
                p.GlobalPosition = home; p.Velocity = Vector2.Zero; p.Hp = p.Stats.MaxHp;
                e.GlobalPosition = spot; e.Velocity = Vector2.Zero;
                foreach (var b in G.Enemies.ToArray()) if (b != e && IsInstanceValid(b)) { G.Enemies.Remove(b); b.QueueFree(); }
                foreach (var pr in EnemyProjectiles.ToArray()) if (IsInstanceValid(pr)) pr.QueueFree();
                foreach (var _ in ElWait(0.3f)) yield return null;
                float hp0 = p.Hp;
                var at0 = e.GlobalPosition;
                int brood0 = e.BroodAlive;
                float guardTaken = -1f;
                e.BossMoveNow(m);
                bool shot = false;
                for (float t = 0; t < 5f; t += (float)GetProcessDeltaTime())
                {
                    if (!IsInstanceValid(e) || e.Dead) break;
                    if (!shot && e.BossMovePhase == 1 && shots.Add(m)) { foreach (var _ in ElWait(0.35f)) yield return null; ScShot($"bossmoves_{m.ToString().ToLowerInvariant()}"); shot = true; }
                    // (a blow struck at it while it guards)
                    if (m == BMove.Guard && e.Guarding && guardTaken < 0f) { float before = e.Hp; e.Hurt(100f, Vector2.Zero, e.GlobalPosition, DamageKind.Raw); guardTaken = before - e.Hp; }
                    if (e.BossMovePhase == 0 && t > 0.3f) break;
                    yield return null;
                }
                foreach (var _ in ElWait(0.8f)) yield return null; // (shots on their way, the wave running)
                float took = hp0 - p.Hp;
                bool ok = m switch
                {
                    BMove.Summon => e.BroodAlive > brood0,
                    BMove.Guard => guardTaken >= 0f && guardTaken <= 100f * Tune.BossMoves.GuardTaken + 1f,
                    BMove.Blink => IsInstanceValid(e) && e.GlobalPosition.DistanceTo(at0) > 40f && took > 0f,
                    _ => took > 0f,
                };
                string what = m switch { BMove.Summon => $"brood {brood0}->{e.BroodAlive}", BMove.Guard => $"a 100 blow did {guardTaken:0}", BMove.Blink => $"moved {e.GlobalPosition.DistanceTo(at0):0} px, {took:0} taken", _ => $"{took:0} taken" };
                line.Add($"{m} {(ok ? "ok" : "NO")} ({what})");
                if (ok) good++; else bad.Add($"{name} {m}: {what}");
            }
            // ---- enraged below 40%
            bool enraged = false;
            if (IsInstanceValid(e) && !e.Dead)
            {
                for (float t = 0; t < 4f && e.InBossMove; t += (float)GetProcessDeltaTime()) yield return null;
                e.Hp = e.MaxHp * 0.3f;
                foreach (var _ in ElWait(0.3f)) yield return null;
                enraged = e.Enraged;
            }
            total++;
            if (enraged) good++; else bad.Add($"{name}: not enraged at 30%");
            GD.Print($"[scenario]   {name} ({string.Join(", ", moves)}): {string.Join("; ", line)}; enraged {enraged}");
            if (IsInstanceValid(e)) { G.Enemies.Remove(e); e.QueueFree(); }
            foreach (var b in G.Enemies.ToArray()) if (IsInstanceValid(b)) { G.Enemies.Remove(b); b.QueueFree(); }
            foreach (var _ in ElWait(0.3f)) yield return null;
        }
        ScCheck($"every boss's great moves land, and each is enraged below 40% ({good} of {total}){(bad.Count > 0 ? ": " + string.Join(" | ", bad) : "")}", good == total);
        ScCheck($"bosses have three or four great moves, and between them all eight kinds ({string.Join(", ", shots)})", shots.Count == Enum.GetValues<BMove>().Length);
        p.InputOverride = null;
    }
}
