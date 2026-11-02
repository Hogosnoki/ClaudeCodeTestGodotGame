using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The hordes (<see cref="Tune.Hordes"/>): generators set through the level, pouring out hordes until they're broken. Their spots come
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
        if (Tune.Testing.NoHordes || cave.Biome == null || cave.Biome.Id is BiomeId.Lair or BiomeId.Null) return;
        var rng = new Random(cave.Seed ^ 0x6e7a);
        int want = Math.Min(Tune.Hordes.GeneratorMax, Tune.Hordes.Generators + (int)(G.Depth * Tune.Hordes.GeneratorsPerDepth));
        var spots = cave.Spawns.Where(s => s.Kind == SpawnKind.Ground).Select(s => s.Pos).OrderBy(_ => rng.Next()).ToList();
        foreach (var floor in spots)
        {
            if (_genSpots.Count >= want) break;
            var at = floor + new Vector2(0, -22);
            if (at.DistanceTo(cave.StartPos) < Tune.Hordes.GeneratorClear) continue;
            if (cave.Boss != null && at.DistanceTo(cave.Boss.Center) < Math.Max(cave.Boss.RxPx, cave.Boss.RyPx) + 120f) continue;
            if (cave.IsSolid(at) || cave.IsSolid(at + new Vector2(-20, -6)) || cave.IsSolid(at + new Vector2(20, -6)) || cave.IsSolid(at + new Vector2(0, -26))) continue;
            if (cave.IsLava(at) || cave.IsLava(floor + new Vector2(0, -4))) continue;
            if (_genSpots.Any(g => g.DistanceTo(at) < Tune.Hordes.GeneratorGap)) continue;
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
/// Trial rooms: a room that shuts behind you. Its rim is warded (<see cref="TrialSeal"/>), generators rise inside, and
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
        /// <summary>How long it has run, and whether it has been told it grows angry.</summary>
        public float Age;
        public bool Angry;
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
        if (Tune.Testing.NoHordes || G.Shop) return false;
        int idx = cave.Rooms.IndexOf(room);
        return room.Kind switch
        {
            RoomKind.Ambush or RoomKind.MiniBoss => true,
            RoomKind.Treasure => !room.Underwater && new Random(cave.Seed ^ (idx * 7919 + 0x51a1)).NextDouble() < Tune.Hordes.TrialTreasureChance,
            _ => false,
        };
    }

    /// <summary>Well inside a trial room: on one of its cells, and well in from its sides (its floor may lie near the rim of its oval: only the sideways distance counts).</summary>
    private bool InTrial(CaveData cave, Room room, Vector2 at)
    {
        var cells = RoomCells(cave, room);
        int i = (int)(at.X / CaveData.Cell), j = (int)(at.Y / CaveData.Cell);
        if (!cells.Contains(j * cave.W + i) && !cells.Contains((j - 1) * cave.W + i)) return false;
        return Math.Abs(at.X - room.Center.X) <= (room.RxPx + 1.5f * CaveData.Cell) * 0.7f;
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

    /// <summary>
    /// A trial's own cells: the room's box grown outward step by step (two cells sideways, one up and down, each step). Of the steps whose rim (the cells to ward) is near the narrowest
    /// (so the wards stand in tunnel mouths, not across open cave), the first at least <see cref="Tune.Hordes.TrialMinWidth"/> across
    /// wins, or else the widest. Never so far it takes in where you arrived, the guardian's chamber or another trial's ground.
    /// </summary>
    private static HashSet<int> TrialCells(CaveData cave, Room room, HashSet<int> taken = null)
    {
        int Cell(Vector2 p) => (int)(p.Y / CaveData.Cell) * cave.W + (int)(p.X / CaveData.Cell);
        int start = Cell(cave.StartPos);
        int boss = cave.Boss != null && cave.Boss != room ? Cell(cave.Boss.Center) : -1;
        var seed = TrialSeed(cave, room);
        if (seed == null) return new HashSet<int>();
        int si = seed.Value.X, sj = seed.Value.Y;
        float ci = room.Center.X / CaveData.Cell, cj = room.Center.Y / CaveData.Cell;
        var steps = new List<(HashSet<int> set, int rim, float width)>();
        // (grown in steps of two cells sideways and one up and down: a cave's tunnels run sideways, and its shafts are best left out)
        for (float m = 0; m <= Tune.Hordes.TrialMinWidth * 0.6f; m += 2f * CaveData.Cell)
        {
            float rx = (room.RxPx + m) / CaveData.Cell + 1.5f, ry = (room.RyPx + m * 0.5f) / CaveData.Cell + 2f;
            var set = new HashSet<int> { sj * cave.W + si };
            var q = new Queue<(int, int)>();
            q.Enqueue((si, sj));
            int minX = si, maxX = si;
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue();
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, c = j + dj;
                    if (a < 0 || c < 0 || a >= cave.W || c >= cave.H || !cave.CellOpen(a, c)) continue;
                    // (a box, not an oval: its walls cross a tunnel straight up and down, a clean column of wards)
                    if (Math.Abs(a + 0.5f - ci) > rx || Math.Abs(c + 0.5f - cj) > ry) continue;
                    if (!set.Add(c * cave.W + a)) continue;
                    q.Enqueue((a, c));
                    minX = Math.Min(minX, a); maxX = Math.Max(maxX, a);
                }
            }
            // (nor into another trial's ground: two trials never overlap)
            if (set.Contains(start) || (boss >= 0 && set.Contains(boss)) || (taken != null && set.Overlaps(taken))) break;
            steps.Add((set, RimCells(cave, set).Count, (maxX - minX + 1) * CaveData.Cell));
        }
        if (steps.Count == 0) return new HashSet<int>();
        int narrowest = steps.Min(x => x.rim);
        var ok = steps.Where(x => x.rim <= narrowest * Tune.Hordes.TrialRimSlack + 2).ToList();
        return (ok.FirstOrDefault(x => x.width >= Tune.Hordes.TrialMinWidth).set ?? ok.OrderByDescending(x => x.width).First().set);
    }

    /// <summary>The open cell a trial is grown from: on the room's floor (or at its middle). Every game finds the same one.</summary>
    private static Vector2I? TrialSeed(CaveData cave, Room room)
    {
        int si = (int)(room.Floor.X / CaveData.Cell), sj = (int)(room.Floor.Y / CaveData.Cell) - 1;
        for (int k = 0; k < 4 && !cave.CellOpen(si, sj); k++) sj--;
        if (!cave.CellOpen(si, sj)) { si = (int)(room.Center.X / CaveData.Cell); sj = (int)(room.Center.Y / CaveData.Cell); }
        return cave.CellOpen(si, sj) ? new Vector2I(si, sj) : null;
    }

    private void BeginTrial(CaveData cave, Room room)
    {
        // (the ground of every trial so far, its wards included: a new one never reaches into it)
        var taken = new HashSet<int>();
        foreach (var other in _trials) { taken.UnionWith(other.Cells); foreach (var c in RimCells(cave, other.Cells)) taken.Add(c.Y * cave.W + c.X); }
        var inside = TrialCells(cave, room, taken);
        if (inside.Count < 12) return;
        // (another trial room lying in this one's ground is part of it: it won't shut a second time around the heroes)
        foreach (var r in cave.Rooms)
        {
            if (r == room || r.Triggered || !IsTrial(cave, r) || TrialSeed(cave, r) is not Vector2I sd) continue;
            if (inside.Contains(sd.Y * cave.W + sd.X)) r.Triggered = true;
        }
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
        t.Seal = RaiseSeal(t.Index, rim, TrialSeed(cave, room));
        NetSync.SendTrial(t.Index, rim);
        // the generators: on the room's floor, apart, never on top of a hero
        if (room.Kind != RoomKind.MiniBoss)
        {
            int want = Math.Min(Tune.Hordes.TrialMax, Tune.Hordes.TrialGenerators + G.Depth / Math.Max(1, Tune.Hordes.TrialPerDepth));
            var floors = inside.Where(k =>
            {
                int i = k % cave.W, j = k / cave.W;
                return !cave.CellOpen(i, j + 1) && inside.Contains((j - 1) * cave.W + i) && inside.Contains((j - 2) * cave.W + i);
            }).Select(k => new Vector2((k % cave.W + 0.5f) * CaveData.Cell, (k / cave.W + 1) * CaveData.Cell - 22f)).OrderBy(_ => G.Rng.Next()).ToList();
            // (a generator floats: when the floor is crowded, one hangs in the room's open air instead)
            var air = inside.Where(k =>
            {
                int i = k % cave.W, j = k / cave.W;
                return inside.Contains(j * cave.W + i - 1) && inside.Contains(j * cave.W + i + 1) && inside.Contains((j - 1) * cave.W + i) && inside.Contains((j + 1) * cave.W + i);
            }).Select(Mid).OrderBy(_ => G.Rng.Next());
            var spots = new List<Vector2>();
            foreach (var at in floors.Concat(air))
            {
                if (spots.Count >= want) break;
                if (cave.IsSolid(at) || cave.IsSolid(at + new Vector2(-16, 0)) || cave.IsSolid(at + new Vector2(16, 0))) continue;
                if (G.Players.Any(h => h != null && !h.Dead && h.GlobalPosition.DistanceTo(at) < 70f)) continue;
                if (spots.Any(s => s.DistanceTo(at) < 140f)) continue;
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

    private TrialSeal RaiseSeal(int index, List<Vector2I> cells, Vector2I? seed)
    {
        var seal = new TrialSeal { RoomIndex = index, Seed = seed };
        seal.Cells.AddRange(cells);
        _world.AddChild(seal);
        return seal;
    }

    /// <summary>Whether a point lies in a trial under way (shut, not yet won).</summary>
    private bool InOpenTrial(Vector2 p)
    {
        if (_trials.Count == 0) return false;
        var cave = G.Cave;
        int k = (int)(p.Y / CaveData.Cell) * cave.W + (int)(p.X / CaveData.Cell);
        foreach (var t in _trials) if (!t.Won && t.Cells.Contains(k)) return true;
        return false;
    }

    /// <summary>The host's game: a trial is won once its generators (and a den's mini-boss) are down and nothing lives in the room.</summary>
    private void RunTrials()
    {
        var cave = G.Cave;
        foreach (var t in _trials)
        {
            if (t.Won) continue;
            // (a trial that drags on grows angry: its generators send faster and keep more out)
            t.Age += 0.25f;
            if (t.Age > Tune.Hordes.EscalateAfter)
            {
                float fury = Math.Min(Tune.Hordes.FuryMax, 1f + (t.Age - Tune.Hordes.EscalateAfter) / Tune.Hordes.EscalateStep * 0.5f);
                foreach (var g in t.Gens) if (IsInstanceValid(g) && !g.Dead) g.Fury = fury;
                if (!t.Angry && t.Gens.Any(g => IsInstanceValid(g) && !g.Dead))
                {
                    t.Angry = true;
                    _hud.ShowBanner("THE DEEP STIRS", 1.5f);
                    NetSync.SendBanner("THE DEEP STIRS", 1.5f);
                    foreach (var g in t.Gens) if (IsInstanceValid(g) && !g.Dead) G.Fx.Ring(g.GlobalPosition, 40, new Color(1f, 0.3f, 0.3f, 0.8f), 0.4f, FxLayer.RingStyle.Burst);
                }
            }
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
        bool known = G.Cave != null && room >= 0 && room < G.Cave.Rooms.Count;
        RaiseSeal(room, cells, known ? TrialSeed(G.Cave, G.Cave.Rooms[room]) : null);
        if (known) G.Cave.Rooms[room].Triggered = true;
    }

    /// <summary>Online: the host's trial is won.</summary>
    public void OnlineTrialWon(int room)
    {
        foreach (var s in TrialSeal.All.ToArray()) if (s.RoomIndex == room) s.Open();
    }
}
