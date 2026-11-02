using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _gtSteps;

    /// <summary>
    /// `--scenario=hordes`: the level holds its generators; one, raised near the hero, wakes and pours out minions (weaker than their kind,
    /// marked as minions) up to its cap; struck down a tier it shrinks; broken it gives its bonus (gold, the power to heroes near) and sends no more.
    /// </summary>
    private void HordesScenario()
    {
        if (_gtSteps == null) { if (_scT < 0.8f) return; _gtSteps = HordesRun().GetEnumerator(); }
        if (!_gtSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> HordesRun()
    {
        // (the biome's own creatures only: the swarms, delvers and bombers have --scenario=movers)
        Tune.Testing.NoMovers = true;
        var p = G.Player;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        int want = Math.Min(Tune.Hordes.GeneratorMax, Tune.Hordes.Generators + (int)(G.Depth * Tune.Hordes.GeneratorsPerDepth));
        ScCheck($"the level has its generators ({_genSpots.Count} of {want}), none near the start", _genSpots.Count >= Math.Min(2, want) && _genSpots.All(s => s.DistanceTo(G.Cave.StartPos) >= Tune.Hordes.GeneratorClear));
        ScCheck($"apart from each other (closest {(_genSpots.Count > 1 ? _genSpots.SelectMany(a => _genSpots.Where(b => b != a).Select(b => a.DistanceTo(b))).Min() : 0):0} px)", _genSpots.Count < 2 || _genSpots.SelectMany(a => _genSpots.Where(b => b != a).Select(b => a.DistanceTo(b))).Min() >= Tune.Hordes.GeneratorGap);
        {
            var cave = G.Cave;
            var locked = new List<Vector2>();
            if (cave.Vault != null) locked.Add(cave.Vault.Chest);
            if (cave.ExtraVault != null) locked.Add(cave.ExtraVault.Chest);
            foreach (var w in cave.Wings) locked.Add(w.Bounds.GetCenter());
            foreach (var r in cave.Rooms) if (r.Kind == RoomKind.Secret) locked.Add(r.Center);
            ScCheck($"  none behind a locked gate or in a secret place ({_genSpots.Count(at => OffLimits(cave, at))} are); those places are off limits ({locked.Count(at => OffLimits(cave, at))} of {locked.Count})",
                _genSpots.All(at => !OffLimits(cave, at)) && locked.All(at => OffLimits(cave, at)));
        }
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
            ScCheck($"weaker than its kind ({minion.MaxHp:0.0} health against {full:0.0})", Math.Abs(minion.MaxHp - full * Tune.Hordes.MinionHp) < 0.6f);
            // a minion's death feeds a heal on a kill only a share of what a creature's does
            float hok = p.Stats.HealOnKill; p.Stats.HealOnKill = 20f;
            var plain = (Enemy)Activator.CreateInstance(minion.GetType());
            plain.Position = minion.GlobalPosition;
            _world.AddChild(plain);
            p.Hp = p.Stats.MaxHp * 0.5f; float h0 = p.Hp;
            plain.Hurt(99999f, Vector2.Zero, plain.GlobalPosition, DamageKind.Raw);
            float healPlain = p.Hp - h0;
            p.Hp = p.Stats.MaxHp * 0.5f; h0 = p.Hp;
            minion.Hurt(99999f, Vector2.Zero, minion.GlobalPosition, DamageKind.Raw);
            float healMinion = p.Hp - h0;
            p.Stats.HealOnKill = hok; p.Hp = p.Stats.MaxHp;
            ScCheck($"a minion's death heals {healMinion:0.0} where a creature's heals {healPlain:0.0} (x{(healPlain > 0 ? healMinion / healPlain : 0):0.00}, want {Tune.Hordes.MinionSustain})", healPlain > 0 && Math.Abs(healMinion / healPlain - Tune.Hordes.MinionSustain) < 0.05f);
        }
        // (the minions are kept from the hero: held where they are)
        int cap = (int)Tune.Hordes.Cap[3] + G.Depth / Tune.Hordes.CapPerDepth;
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
        float power = Tune.Hordes.PowerChance; Tune.Hordes.PowerChance = 1f;
        int coins = _world.GetChildren().OfType<CoinPickup>().Sum(c => c.Count), broken = Generator.Broken;
        float dmg = p.Stats.DamageMult, speed = p.Stats.MoveSpeed;
        p.GlobalPosition = gen.GlobalPosition + new Vector2(-40, 10);
        // (its experience would level the hero up mid-test and change the numbers: none for this one)
        gen.XpValue = 0;
        gen.Hurt(gen.MaxHp * 4f, Vector2.Zero, gen.GlobalPosition, DamageKind.Raw);
        Tune.Hordes.PowerChance = power;
        foreach (var _ in Wait(0.3f)) yield return null;
        int coinsNow = _world.GetChildren().OfType<CoinPickup>().Sum(c => c.Count);
        ScCheck($"broken, it bursts into gold ({coinsNow - coins} pieces, want {Tune.Hordes.BonusGold + G.Depth})", coinsNow - coins >= Tune.Hordes.BonusGold + G.Depth && Generator.Broken == broken + 1);
        bool fury = p.HasBuff(Player.BuffFury), haste = p.HasBuff(Player.BuffHaste);
        ScCheck($"and a power for the hero near (fury {fury}: damage x{p.Stats.DamageMult / dmg:0.00}; haste {haste}: speed x{p.Stats.MoveSpeed / speed:0.00})",
            (fury && Math.Abs(p.Stats.DamageMult / dmg - Tune.Hordes.FuryDamage) < 0.01f) || (haste && Math.Abs(p.Stats.MoveSpeed / speed - Tune.Hordes.HasteSpeed) < 0.01f));
        ScShot("generator_broken");
        int sent = gen.Released;
        foreach (var _ in Wait(4f)) yield return null;
        ScCheck($"and it sends no more ({sent} before, {(IsInstanceValid(gen) ? gen.Released : sent)} now)", !IsInstanceValid(gen) || gen.Released == sent);
        foreach (var _ in Wait(Tune.Hordes.PowerSeconds + 0.5f)) yield return null;
        ScCheck($"the power wears off ({p.HasBuff(Player.BuffFury) || p.HasBuff(Player.BuffHaste)}; damage x{p.Stats.DamageMult / dmg:0.00}, speed x{p.Stats.MoveSpeed / speed:0.00})",
            !p.HasBuff(Player.BuffFury) && !p.HasBuff(Player.BuffHaste) && Math.Abs(p.Stats.DamageMult / dmg - 1f) < 0.01f && Math.Abs(p.Stats.MoveSpeed / speed - 1f) < 0.01f);
        // a minion drops less: its gold and food chances are cut
        ScCheck($"minions drop less (x{Tune.Hordes.MinionDropMult})", Tune.Hordes.MinionDropMult < 1f);
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
        float chance = Tune.Hordes.TrialTreasureChance; Tune.Hordes.TrialTreasureChance = 1f;
        var room = cave.Rooms.Where(r => !r.Triggered && r.Kind is RoomKind.Ambush or RoomKind.Treasure && !r.Underwater && IsTrial(cave, r) && RoomCells(cave, r).Count >= 12)
            .OrderBy(r => r.Center.DistanceTo(p.GlobalPosition)).FirstOrDefault();
        ScCheck($"the level has a trial room ({room?.Kind})", room != null);
        if (room == null) { Tune.Hordes.TrialTreasureChance = chance; yield break; }
        int chests = Chest.All.Count;
        // walk in: the middle of its floor
        var inside = RoomCells(cave, room);
        Vector2 Mid(int k) => new Vector2(k % cave.W + 0.5f, k / cave.W + 0.5f) * CaveData.Cell;
        var stand = inside.Where(k => !cave.CellOpen(k % cave.W, k / cave.W + 1)).OrderBy(k => Mid(k).DistanceSquaredTo(room.Center)).Select(Mid).FirstOrDefault(room.Floor + new Vector2(0, -12));
        p.GlobalPosition = stand;
        p.Velocity = Vector2.Zero;
        foreach (var _ in Wait(1.2f)) yield return null;
        Tune.Hordes.TrialTreasureChance = chance;
        var seal = TrialSeal.All.FirstOrDefault(s => s.RoomIndex == cave.Rooms.IndexOf(room));
        // (the trial's own cells: the room grown out to its narrowest rim, flooded up to its wards)
        if (seal != null) inside = new HashSet<int>(seal.Inside);
        ScCheck($"walking in shuts it (trials {TrialsBegun}, wards over {seal?.Cells.Count ?? 0} cells)", TrialsBegun >= 1 && seal != null && seal.Cells.Count > 0);
        if (seal == null) yield break;
        // the wards are solid: a point in each one meets a body there
        var space = _world.GetWorld2D().DirectSpaceState;
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
        // a blink or a shadowstep keeps the rule too: in, never out
        Vector2 inPt = new Vector2(inside.First() % cave.W + 0.5f, inside.First() / cave.W + 0.5f) * CaveData.Cell, outPt = Vector2.Zero;
        foreach (var c in seal.Cells)
            foreach (var o in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
            {
                var n = c + o;
                int nk = n.Y * cave.W + n.X;
                if (outPt == Vector2.Zero && cave.CellOpen(n.X, n.Y) && !inside.Contains(nk) && !seal.Cells.Contains(n)) outPt = new Vector2(n.X + 0.5f, n.Y + 0.5f) * CaveData.Cell;
            }
        ScCheck($"a sudden move may come in ({!TrialSeal.BlocksMove(outPt, inPt)}) but never go out ({TrialSeal.BlocksMove(inPt, outPt)})", outPt != Vector2.Zero && !TrialSeal.BlocksMove(outPt, inPt) && TrialSeal.BlocksMove(inPt, outPt));
        float width = (inside.Max(k => k % cave.W) - inside.Min(k => k % cave.W) + 1) * CaveData.Cell;
        ScCheck($"the fighting area is wide ({width:0} px across, want {Tune.Hordes.TrialMinWidth:0}, or the most the cave allows)", width >= Math.Min(Tune.Hordes.TrialMinWidth, 700f));
        var gens = Generators.Where(g => IsInstanceValid(g) && !g.Dead && g.Trial == room).ToList();
        ScCheck($"generators rise inside ({gens.Count})", gens.Count >= Tune.Hordes.TrialGenerators && gens.All(g => inside.Contains((int)(g.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(g.GlobalPosition.X / CaveData.Cell))));
        // (for the picture: the hero by one of the warded doorways)
        var ward = new Vector2(seal.Cells[0].X + 0.5f, seal.Cells[0].Y + 0.5f) * CaveData.Cell;
        p.GlobalPosition = Mid(inside.OrderBy(k => Mid(k).DistanceSquaredTo(ward)).Skip(2).First());
        foreach (var _ in Wait(2.5f)) yield return null;
        ScShot("trial_shut");

        // a trial that drags on grows angry (the wait shortened for the test)
        float esc = Tune.Hordes.EscalateAfter; Tune.Hordes.EscalateAfter = 0.5f;
        foreach (var _ in Wait(1.5f)) yield return null;
        var angry = gens.Where(g => IsInstanceValid(g) && !g.Dead).ToList();
        ScCheck($"a trial that drags on angers its generators (fury {(angry.Count > 0 ? angry.Max(g => g.Fury) : 0):0.00})", angry.Count > 0 && angry.All(g => g.Fury > 1f));
        Tune.Hordes.EscalateAfter = esc;
        foreach (var g in angry) g.Fury = 1f;

        // one way, for heroes: a friend outside walks in through the wards, and then can't walk back out
        Vector2 CellMid(Vector2I c) => new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell;
        bool Open(Vector2I c) => cave.CellOpen(c.X, c.Y) && !seal.InRoom(CellMid(c)) && !seal.Cells.Contains(c);
        Vector2I door = default; Vector2 inward = default; bool found = false;
        foreach (var c in seal.Cells)
        {
            var dir = seal.InwardOf(c);
            if (dir.Y != 0 || dir.X == 0) continue;
            // (a floor near the doorway's foot, and room to stand two cells out from it)
            if (cave.CellOpen(c.X, c.Y + 1) && cave.CellOpen(c.X, c.Y + 2)) continue;
            var o = new Vector2I(-(int)dir.X, 0);
            if (!Open(c + o) || !Open(c + o * 2) || !Open(c + o * 2 + new Vector2I(0, -1))) continue;
            door = c; inward = dir; found = true; break;
        }
        ScCheck($"a doorway at the floor to walk in by ({found})", found);
        if (found)
        {
            p.InputOverride = () => _scInput;
            p.GlobalPosition = CellMid(door - new Vector2I((int)inward.X, 0) * 2) + new Vector2(0, -2);
            p.Velocity = Vector2.Zero;
            foreach (var _ in Wait(0.4f)) yield return null;
            ScCheck($"the friend starts outside ({!seal.InRoom(p.GlobalPosition)})", !seal.InRoom(p.GlobalPosition));
            int joined = seal.Joined;
            _scInput = new PlayerInput { Move = inward };
            for (float t = 0; t < 3f && !seal.WellInside(p.GlobalPosition); t += (float)GetProcessDeltaTime()) yield return null;
            foreach (var _ in Wait(0.3f)) yield return null;
            ScCheck($"walking in, the wards let them through ({seal.WellInside(p.GlobalPosition)}, joined {seal.Joined - joined})", seal.WellInside(p.GlobalPosition) && seal.Joined == joined + 1);
            ScShot("trial_joined");
            _scInput = new PlayerInput { Move = -inward };
            foreach (var _ in Wait(2f)) yield return null;
            _scInput = default;
            float past = (p.GlobalPosition.X - CellMid(door).X) * -inward.X;
            ScCheck($"walking back out, the wards hold them (in the room {seal.InRoom(p.GlobalPosition)}, {past:0} px short of the doorway's middle)", seal.InRoom(p.GlobalPosition) && past < 0f);
            p.InputOverride = null;
        }
        // not won while a generator stands
        foreach (var g in gens.Skip(1)) g.Hurt(g.MaxHp * 4f, Vector2.Zero, g.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.8f)) yield return null;
        ScCheck($"one generator still standing: not won ({TrialsWon})", TrialsWon == 0 && IsInstanceValid(seal) && !seal.Opening);
        gens[0].Hurt(gens[0].MaxHp * 4f, Vector2.Zero, gens[0].GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.8f)) yield return null;
        int left = G.Enemies.Count(e => !e.Dead && inside.Contains((int)(e.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(e.GlobalPosition.X / CaveData.Cell)));
        ScCheck($"with the generators down but {left} creatures still in the room: not won ({TrialsWon})", left == 0 || TrialsWon == 0);
        // (everything left in the room, a few times over: a level's own generator may stand in it too, and send one more before it falls)
        for (int round = 0; round < 3 && TrialsWon == 0; round++)
        {
            foreach (var e in G.Enemies.ToArray())
                if (!e.Dead && inside.Contains((int)(e.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(e.GlobalPosition.X / CaveData.Cell))) e.Hurt(99999f, Vector2.Zero, e.GlobalPosition, DamageKind.Raw);
            foreach (var _ in Wait(1.0f)) yield return null;
        }
        ScCheck($"cleared, it's won ({TrialsWon}) and the wards fall ({!IsInstanceValid(seal) || seal.Opening})", TrialsWon == 1 && (!IsInstanceValid(seal) || seal.Opening));
        ScCheck($"and a chest is left ({Chest.All.Count} against {chests})", Chest.All.Count > chests);
        ScShot("trial_won");

        // every other trial room on the level shut at once: no two trials share ground, and no trial's wards stand in another's room
        Tune.Hordes.TrialTreasureChance = 1f;
        foreach (var r in cave.Rooms.ToList())
            if (!r.Triggered && IsTrial(cave, r)) { r.Triggered = true; BeginTrial(cave, r); }
        Tune.Hordes.TrialTreasureChance = chance;
        int overlaps = 0, crossed = 0;
        for (int a = 0; a < _trials.Count; a++)
            for (int b = a + 1; b < _trials.Count; b++)
            {
                if (_trials[a].Cells.Overlaps(_trials[b].Cells)) overlaps++;
                if (RimCells(cave, _trials[a].Cells).Any(c => _trials[b].Cells.Contains(c.Y * cave.W + c.X)) || RimCells(cave, _trials[b].Cells).Any(c => _trials[a].Cells.Contains(c.Y * cave.W + c.X))) crossed++;
            }
        ScCheck($"the level's {_trials.Count} trials never overlap ({overlaps} sharing ground, {crossed} with wards in another's room)", overlaps == 0 && crossed == 0);
    }
}
