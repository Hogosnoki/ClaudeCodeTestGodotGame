using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Gauntlet (<see cref="Tune.Gauntlet"/>): generators set through the level, pouring out hordes until they're broken. Their spots come
/// from the seed alone (so every game online picks the same), and the host's game raises each one when a hero first comes near, as the hive is.
/// </summary>
public partial class Main
{
    private readonly List<Vector2> _genSpots = new();
    private readonly HashSet<int> _genMade = new();
    /// <summary>The generators standing now (for the tests and the minimap).</summary>
    public static readonly List<Generator> Generators = new();

    private void PlaceGenerators(CaveData cave)
    {
        _genSpots.Clear(); _genMade.Clear(); Generators.Clear(); _trials.Clear(); _denElite = null;
        if (!Tune.Gauntlet.On || cave.Biome == null || cave.Biome.Id is BiomeId.Lair or BiomeId.Null) return;
        var rng = new Random(cave.Seed ^ 0x6e7a);
        int want = Math.Min(Tune.Gauntlet.GeneratorMax, Tune.Gauntlet.Generators + (int)(G.Depth * Tune.Gauntlet.GeneratorsPerDepth));
        var spots = cave.Spawns.Where(s => s.Kind == SpawnKind.Ground).Select(s => s.Pos).OrderBy(_ => rng.Next()).ToList();
        foreach (var floor in spots)
        {
            if (_genSpots.Count >= want) break;
            var at = floor + new Vector2(0, -22);
            if (at.DistanceTo(cave.StartPos) < Tune.Gauntlet.GeneratorClear) continue;
            if (cave.Boss != null && at.DistanceTo(cave.Boss.Center) < Math.Max(cave.Boss.RxPx, cave.Boss.RyPx) + 120f) continue;
            if (cave.IsSolid(at) || cave.IsSolid(at + new Vector2(-20, -6)) || cave.IsSolid(at + new Vector2(20, -6)) || cave.IsSolid(at + new Vector2(0, -26))) continue;
            if (cave.IsLava(at) || cave.IsLava(floor + new Vector2(0, -4))) continue;
            if (_genSpots.Any(g => g.DistanceTo(at) < Tune.Gauntlet.GeneratorGap)) continue;
            _genSpots.Add(at);
        }
        if (_autotest) GD.Print($"[autotest] generators: {_genSpots.Count} of {want}");
    }

    /// <summary>The host's game raises a generator once a hero comes within sight of its spot (it wakes nearer, see <see cref="Generator"/>).</summary>
    private void RunGenerators()
    {
        Generators.RemoveAll(g => g == null || !IsInstanceValid(g) || g.Dead);
        for (int k = 0; k < _genSpots.Count; k++)
        {
            if (_genMade.Contains(k)) continue;
            var at = _genSpots[k];
            bool near = false;
            foreach (var h in G.Players) if (h != null && !h.Dead && h.GlobalPosition.DistanceTo(at) < 900f) { near = true; break; }
            if (!near) continue;
            _genMade.Add(k);
            MakeGenerator(at);
        }
    }

    /// <summary>A generator at a spot (the level's, a trial room's or a test's).</summary>
    public Generator MakeGenerator(Vector2 at, Room trial = null)
    {
        var g = new Generator { Position = at, Trial = trial };
        _world.AddChild(g);
        Generators.Add(g);
        return g;
    }
}

/// <summary>
/// Trial rooms (the Gauntlet): a room that shuts behind you. Its rim is warded (<see cref="TrialSeal"/>), generators rise inside, and
/// nobody leaves until they're broken and the room is clear. Won, the wards fade and a chest waits.
/// </summary>
public partial class Main
{
    private sealed class TrialState
    {
        public Room Room;
        public int Index;
        public HashSet<int> Cells;
        public readonly List<Generator> Gens = new();
        public Enemy Elite;
        public TrialSeal Seal;
        public bool Won;
    }

    private readonly List<TrialState> _trials = new();
    /// <summary>The mini-boss the den's waking just made (for its trial).</summary>
    private Enemy _denElite;
    /// <summary>For the tests: trials begun and won this level.</summary>
    public int TrialsBegun => _trials.Count;
    public int TrialsWon => _trials.Count(t => t.Won);

    /// <summary>Whether a room is a trial: every ambush and mini-boss den, half the treasure rooms (from the seed, so every game agrees).</summary>
    private bool IsTrial(CaveData cave, Room room)
    {
        if (!Tune.Gauntlet.On || G.Shop) return false;
        int idx = cave.Rooms.IndexOf(room);
        return room.Kind switch
        {
            RoomKind.Ambush or RoomKind.MiniBoss => true,
            RoomKind.Treasure => !room.Underwater && new Random(cave.Seed ^ (idx * 7919 + 0x51a1)).NextDouble() < Tune.Gauntlet.TrialTreasureChance,
            _ => false,
        };
    }

    /// <summary>The cells to ward: every open cell just outside the room's own (the only ways in or out).</summary>
    private static List<Vector2I> RimCells(CaveData cave, HashSet<int> inside)
    {
        var rim = new HashSet<int>();
        foreach (int k in inside)
        {
            int i = k % cave.W, j = k / cave.W;
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, c = j + dj;
                if (a < 0 || c < 0 || a >= cave.W || c >= cave.H || !cave.CellOpen(a, c)) continue;
                int n = c * cave.W + a;
                if (!inside.Contains(n)) rim.Add(n);
            }
        }
        return rim.Select(n => new Vector2I(n % cave.W, n / cave.W)).ToList();
    }

    private void BeginTrial(CaveData cave, Room room)
    {
        var inside = RoomCells(cave, room);
        if (inside.Count < 12) return;
        var t = new TrialState { Room = room, Index = cave.Rooms.IndexOf(room), Cells = inside, Elite = room.Kind == RoomKind.MiniBoss ? _denElite : null };
        _denElite = null;
        var rim = RimCells(cave, inside);
        int Idx(Vector2 p) => (int)(p.Y / CaveData.Cell) * cave.W + (int)(p.X / CaveData.Cell);
        Vector2 Mid(int k) => new Vector2(k % cave.W + 0.5f, k / cave.W + 0.5f) * CaveData.Cell;
        // (a hero standing in a doorway is drawn inside before it shuts)
        var rimSet = rim.Select(c => c.Y * cave.W + c.X).ToHashSet();
        foreach (var h in G.Players)
        {
            if (h == null || h.Dead || !rimSet.Contains(Idx(h.GlobalPosition))) continue;
            int best = inside.OrderBy(k => Mid(k).DistanceSquaredTo(h.GlobalPosition)).First();
            h.GlobalPosition = Mid(best);
        }
        t.Seal = RaiseSeal(t.Index, rim);
        NetSync.SendTrial(t.Index, rim);
        // the generators: on the room's floor, apart, never on top of a hero
        if (room.Kind != RoomKind.MiniBoss)
        {
            int want = Math.Min(Tune.Gauntlet.TrialMax, Tune.Gauntlet.TrialGenerators + G.Depth / Math.Max(1, Tune.Gauntlet.TrialPerDepth));
            var floors = inside.Where(k =>
            {
                int i = k % cave.W, j = k / cave.W;
                return !cave.CellOpen(i, j + 1) && inside.Contains((j - 1) * cave.W + i) && inside.Contains((j - 2) * cave.W + i);
            }).Select(k => new Vector2((k % cave.W + 0.5f) * CaveData.Cell, (k / cave.W + 1) * CaveData.Cell - 22f)).OrderBy(_ => G.Rng.Next()).ToList();
            var spots = new List<Vector2>();
            foreach (var at in floors)
            {
                if (spots.Count >= want) break;
                if (cave.IsSolid(at) || cave.IsSolid(at + new Vector2(-16, 0)) || cave.IsSolid(at + new Vector2(16, 0))) continue;
                if (G.Players.Any(h => h != null && !h.Dead && h.GlobalPosition.DistanceTo(at) < 90f)) continue;
                if (spots.Any(s => s.DistanceTo(at) < 80f)) continue;
                spots.Add(at);
            }
            foreach (var at in spots)
            {
                var g = MakeGenerator(at, room);
                g.Engage();
                t.Gens.Add(g);
            }
        }
        _trials.Add(t);
        string title = room.Kind == RoomKind.MiniBoss ? "" : "TRIAL";
        if (title != "") { _hud.ShowBanner(title, 1.5f); NetSync.SendBanner(title, 1.5f); }
        G.Sfx.Play("roar", room.Center, -10, 0.1f, 0.7f);
        if (_autotest) GD.Print($"[autotest] trial: room {t.Index} ({room.Kind}), {rim.Count} wards, {t.Gens.Count} generators");
    }

    private TrialSeal RaiseSeal(int index, List<Vector2I> cells)
    {
        var seal = new TrialSeal { RoomIndex = index };
        seal.Cells.AddRange(cells);
        _world.AddChild(seal);
        return seal;
    }

    /// <summary>The host's game: a trial is won once its generators (and a den's mini-boss) are down and nothing lives in the room.</summary>
    private void RunTrials()
    {
        var cave = G.Cave;
        foreach (var t in _trials)
        {
            if (t.Won) continue;
            if (t.Gens.Any(g => IsInstanceValid(g) && !g.Dead)) continue;
            if (t.Elite != null && IsInstanceValid(t.Elite) && !t.Elite.Dead) continue;
            bool alive = false;
            foreach (var e in G.Enemies)
            {
                if (e == null || !IsInstanceValid(e) || e.Dead) continue;
                int k = (int)(e.GlobalPosition.Y / CaveData.Cell) * cave.W + (int)(e.GlobalPosition.X / CaveData.Cell);
                if (t.Cells.Contains(k)) { alive = true; break; }
            }
            if (alive) continue;
            t.Won = true;
            if (IsInstanceValid(t.Seal)) t.Seal.Open();
            NetSync.SendTrialWon(t.Index);
            if (t.Room.Kind != RoomKind.MiniBoss)
            {
                var floor = t.Room.Floor;
                if (cave.FindFloor(t.Room.Center, 900, out var f)) floor = f;
                SpawnChest(floor);
                _hud.ShowBanner("TRIAL WON", 1.5f);
                NetSync.SendBanner("TRIAL WON", 1.5f);
            }
            G.Sfx.Play("chime", t.Room.Center, -4);
        }
    }

    /// <summary>Online: the host shut a trial room.</summary>
    public void OnlineTrial(int room, List<Vector2I> cells)
    {
        if (TrialSeal.All.Any(s => s.RoomIndex == room)) return;
        RaiseSeal(room, cells);
        if (G.Cave != null && room >= 0 && room < G.Cave.Rooms.Count) G.Cave.Rooms[room].Triggered = true;
    }

    /// <summary>Online: the host's trial is won.</summary>
    public void OnlineTrialWon(int room)
    {
        foreach (var s in TrialSeal.All.ToArray()) if (s.RoomIndex == room) s.Open();
    }
}
