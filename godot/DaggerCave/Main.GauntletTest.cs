using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _gtSteps;

    /// <summary>
    /// `--scenario=gauntlet`: the level holds its generators; one, raised near the hero, wakes and pours out minions (weaker than their kind,
    /// marked as minions) up to its cap; struck down a tier it shrinks; broken it gives its bonus (gold, the power to heroes near) and sends no more.
    /// </summary>
    private void GauntletScenario()
    {
        if (_gtSteps == null) { if (_scT < 0.8f) return; _gtSteps = GauntletRun().GetEnumerator(); }
        if (!_gtSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GauntletRun()
    {
        var p = G.Player;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        int want = Math.Min(Tune.Gauntlet.GeneratorMax, Tune.Gauntlet.Generators + (int)(G.Depth * Tune.Gauntlet.GeneratorsPerDepth));
        ScCheck($"the level has its generators ({_genSpots.Count} of {want}), none near the start", _genSpots.Count >= Math.Min(2, want) && _genSpots.All(s => s.DistanceTo(G.Cave.StartPos) >= Tune.Gauntlet.GeneratorClear));
        ScCheck($"apart from each other (closest {(_genSpots.Count > 1 ? _genSpots.SelectMany(a => _genSpots.Where(b => b != a).Select(b => a.DistanceTo(b))).Min() : 0):0} px)", _genSpots.Count < 2 || _genSpots.SelectMany(a => _genSpots.Where(b => b != a).Select(b => a.DistanceTo(b))).Min() >= Tune.Gauntlet.GeneratorGap);
        // (the level's own are put aside: one is raised by the hero for the test)
        _genSpots.Clear();
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        p.Stats.MaxHp = p.Hp = 50000;
        foreach (var _ in Wait(0.3f)) yield return null;
        var spot = p.GlobalPosition + new Vector2(p.Facing * 140, -24);
        if (G.Cave.IsSolid(spot)) spot = p.GlobalPosition + new Vector2(-p.Facing * 140, -24);
        if (G.Cave.IsSolid(spot)) spot = p.GlobalPosition + new Vector2(0, -60);
        var gen = MakeGenerator(spot);
        foreach (var _ in Wait(1.5f)) yield return null;
        ScShot("generator_0");
        ScCheck($"a generator near the hero wakes and sends a creature out ({gen.Released} sent, awake {gen.Alerted})", gen.Alerted && gen.Released >= 1);
        var minion = G.Enemies.FirstOrDefault(e => e.Minion && !e.Dead);
        ScCheck($"what it sends is a minion ({minion?.GetType().Name})", minion != null);
        if (minion != null)
        {
            var fresh = (Enemy)Activator.CreateInstance(minion.GetType());
            _world.AddChild(fresh);
            float full = fresh.MaxHp;
            G.Enemies.Remove(fresh); fresh.QueueFree();
            ScCheck($"weaker than its kind ({minion.MaxHp:0.0} health against {full:0.0})", Math.Abs(minion.MaxHp - full * Tune.Gauntlet.MinionHp) < 0.6f);
        }
        // (the minions are kept from the hero: held where they are)
        int cap = (int)Tune.Gauntlet.Cap[3] + G.Depth / Tune.Gauntlet.CapPerDepth;
        for (float t = 0; t < 14f; t += (float)GetProcessDeltaTime())
        {
            foreach (var e in G.Enemies.Where(e => e.Minion)) e.Velocity = Vector2.Zero;
            p.Hp = p.Stats.MaxHp;
            yield return null;
        }
        ScShot("generator_horde");
        ScCheck($"it keeps sending them up to its cap ({gen.BroodAlive} out, cap {cap}, {gen.Released} sent)", gen.BroodAlive == cap);
        ScCheck($"whole, it's in its top tier (tier {gen.Tier}, size {gen.Size:0.00})", gen.Tier == 3);
        gen.Hurt(gen.MaxHp * 0.5f, Vector2.Zero, gen.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"struck to half it drops a tier and shrinks (tier {gen.Tier}, size {gen.Size:0.00})", gen.Tier == 2 && gen.Size < 1.15f);

        // broken: the bonus
        float power = Tune.Gauntlet.PowerChance; Tune.Gauntlet.PowerChance = 1f;
        int coins = _world.GetChildren().OfType<CoinPickup>().Sum(c => c.Count), broken = Generator.Broken;
        float dmg = p.Stats.DamageMult, speed = p.Stats.MoveSpeed;
        p.GlobalPosition = gen.GlobalPosition + new Vector2(-40, 10);
        gen.Hurt(gen.MaxHp * 4f, Vector2.Zero, gen.GlobalPosition, DamageKind.Raw);
        Tune.Gauntlet.PowerChance = power;
        foreach (var _ in Wait(0.3f)) yield return null;
        int coinsNow = _world.GetChildren().OfType<CoinPickup>().Sum(c => c.Count);
        ScCheck($"broken, it bursts into gold ({coinsNow - coins} pieces, want {Tune.Gauntlet.BonusGold + G.Depth})", coinsNow - coins >= Tune.Gauntlet.BonusGold + G.Depth && Generator.Broken == broken + 1);
        bool fury = p.HasBuff(Player.BuffFury), haste = p.HasBuff(Player.BuffHaste);
        ScCheck($"and a power for the hero near (fury {fury}: damage x{p.Stats.DamageMult / dmg:0.00}; haste {haste}: speed x{p.Stats.MoveSpeed / speed:0.00})",
            (fury && Math.Abs(p.Stats.DamageMult / dmg - Tune.Gauntlet.FuryDamage) < 0.01f) || (haste && Math.Abs(p.Stats.MoveSpeed / speed - Tune.Gauntlet.HasteSpeed) < 0.01f));
        ScShot("generator_broken");
        int sent = gen.Released;
        foreach (var _ in Wait(4f)) yield return null;
        ScCheck($"and it sends no more ({sent} before, {(IsInstanceValid(gen) ? gen.Released : sent)} now)", !IsInstanceValid(gen) || gen.Released == sent);
        foreach (var _ in Wait(Tune.Gauntlet.PowerSeconds + 0.5f)) yield return null;
        ScCheck($"the power wears off ({p.HasBuff(Player.BuffFury) || p.HasBuff(Player.BuffHaste)}; damage x{p.Stats.DamageMult / dmg:0.00}, speed x{p.Stats.MoveSpeed / speed:0.00})",
            !p.HasBuff(Player.BuffFury) && !p.HasBuff(Player.BuffHaste) && Math.Abs(p.Stats.DamageMult / dmg - 1f) < 0.01f && Math.Abs(p.Stats.MoveSpeed / speed - 1f) < 0.01f);
        // a minion drops less: its gold and food chances are cut
        ScCheck($"minions drop less (x{Tune.Gauntlet.MinionDropMult})", Tune.Gauntlet.MinionDropMult < 1f);
    }
}

public partial class Main
{
    private IEnumerator<object> _trSteps;

    /// <summary>`--scenario=trial`: a hero walks well into a trial room; it shuts (wards over its rim, solid), generators rise; broken and cleared, it opens and leaves a chest.</summary>
    private void TrialScenario()
    {
        if (_trSteps == null) { if (_scT < 0.8f) return; _trSteps = TrialRun().GetEnumerator(); }
        if (!_trSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> TrialRun()
    {
        var p = G.Player;
        var cave = G.Cave;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        p.Stats.MaxHp = p.Hp = 50000;
        // (every treasure room a trial for the test, so there's always one to walk into)
        float chance = Tune.Gauntlet.TrialTreasureChance; Tune.Gauntlet.TrialTreasureChance = 1f;
        var room = cave.Rooms.Where(r => !r.Triggered && r.Kind is RoomKind.Ambush or RoomKind.Treasure && !r.Underwater && IsTrial(cave, r) && RoomCells(cave, r).Count >= 12)
            .OrderBy(r => r.Center.DistanceTo(p.GlobalPosition)).FirstOrDefault();
        ScCheck($"the level has a trial room ({room?.Kind})", room != null);
        if (room == null) { Tune.Gauntlet.TrialTreasureChance = chance; yield break; }
        int chests = Chest.All.Count;
        // walk in: the middle of its floor
        var inside = RoomCells(cave, room);
        Vector2 Mid(int k) => new Vector2(k % cave.W + 0.5f, k / cave.W + 0.5f) * CaveData.Cell;
        var stand = inside.Where(k => !cave.CellOpen(k % cave.W, k / cave.W + 1)).OrderBy(k => Mid(k).DistanceSquaredTo(room.Center)).Select(Mid).FirstOrDefault(room.Floor + new Vector2(0, -12));
        p.GlobalPosition = stand;
        p.Velocity = Vector2.Zero;
        foreach (var _ in Wait(1.2f)) yield return null;
        Tune.Gauntlet.TrialTreasureChance = chance;
        var seal = TrialSeal.All.FirstOrDefault(s => s.RoomIndex == cave.Rooms.IndexOf(room));
        ScCheck($"walking in shuts it (trials {TrialsBegun}, wards over {seal?.Cells.Count ?? 0} cells)", TrialsBegun >= 1 && seal != null && seal.Cells.Count > 0);
        if (seal == null) yield break;
        // the wards are solid: a point in each one meets a body there
        var space = GetWorld2D().DirectSpaceState;
        int solid = 0;
        foreach (var c in seal.Cells)
        {
            var q = new PhysicsPointQueryParameters2D { Position = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell, CollisionMask = G.LayerTerrain };
            if (space.IntersectPoint(q, 4).Any(d => d["collider"].AsGodotObject() == seal)) solid++;
        }
        ScCheck($"the wards are solid ({solid} of {seal.Cells.Count})", solid == seal.Cells.Count);
        // nothing of the room's own reaches out past them: every way out of its cells is warded
        var rim = seal.Cells.Select(c => c.Y * cave.W + c.X).ToHashSet();
        bool leak = false;
        foreach (int k in inside)
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = k % cave.W + di, b = k / cave.W + dj, n = b * cave.W + a;
                if (cave.CellOpen(a, b) && !inside.Contains(n) && !rim.Contains(n)) leak = true;
            }
        ScCheck($"every way out is warded (leak {leak})", !leak);
        var gens = Generators.Where(g => IsInstanceValid(g) && !g.Dead && g.Trial == room).ToList();
        ScCheck($"generators rise inside ({gens.Count})", gens.Count >= Tune.Gauntlet.TrialGenerators && gens.All(g => inside.Contains((int)(g.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(g.GlobalPosition.X / CaveData.Cell))));
        foreach (var _ in Wait(2.5f)) yield return null;
        ScShot("trial_shut");
        // not won while a generator stands
        foreach (var g in gens.Skip(1)) g.Hurt(g.MaxHp * 4f, Vector2.Zero, g.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.8f)) yield return null;
        ScCheck($"one generator still standing: not won ({TrialsWon})", TrialsWon == 0 && IsInstanceValid(seal) && !seal.Opening);
        gens[0].Hurt(gens[0].MaxHp * 4f, Vector2.Zero, gens[0].GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.8f)) yield return null;
        int left = G.Enemies.Count(e => !e.Dead && inside.Contains((int)(e.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(e.GlobalPosition.X / CaveData.Cell)));
        ScCheck($"with the generators down but {left} creatures still in the room: not won ({TrialsWon})", left == 0 || TrialsWon == 0);
        foreach (var e in G.Enemies.ToArray())
            if (!e.Dead && inside.Contains((int)(e.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(e.GlobalPosition.X / CaveData.Cell))) e.Hurt(99999f, Vector2.Zero, e.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(1.5f)) yield return null;
        ScCheck($"cleared, it's won ({TrialsWon}) and the wards fall ({!IsInstanceValid(seal) || seal.Opening})", TrialsWon == 1 && (!IsInstanceValid(seal) || seal.Opening));
        ScCheck($"and a chest is left ({Chest.All.Count} against {chests})", Chest.All.Count > chests);
        ScShot("trial_won");
    }
}
