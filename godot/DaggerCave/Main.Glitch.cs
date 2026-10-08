using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>--glitch: the Glitch comes to this level (any level but the Null), a moment after it begins.</summary>
    public static bool ForceGlitch;
    private float _glitchT = -1f;
    private bool _glitchWallOpen;
    /// <summary>The way through the wall the Glitch left wrong (null until it is beaten).</summary>
    public Portal GlitchWall { get; private set; }
    private const int GlitchWallId = 999_000;

    /// <summary>A new level: will the Glitch come? (Only to a party that has all slain the Elder Dragon, and rarely even then.)</summary>
    private void RollGlitch()
    {
        _glitchT = -1f;
        _glitchWallOpen = false;
        GlitchWall = null;
        _sfx.Hush(false);
        if (Net.IsClient || G.Biome == null || G.Biome.Id == BiomeId.Null) return;
        if (ForceGlitch) { _glitchT = 2.5f; return; }
        bool eligible = G.Depth >= 1 && G.Depth + 1 < Biomes.FinalDepth && Net.PartySlayers;
        if (eligible && _rng.NextDouble() < Tune.Glitch.Chance) _glitchT = Tune.Glitch.ArriveMin + (float)_rng.NextDouble() * (Tune.Glitch.ArriveMax - Tune.Glitch.ArriveMin);
    }

    /// <summary>The host's clock for its coming: when it runs out, it is simply there, beside one of the heroes.</summary>
    private void WatchGlitch(float dt)
    {
        if (_glitchT < 0 || (_glitchT -= dt) > 0) return;
        _glitchT = -1f;
        var heroes = G.Players.Where(h => IsInstanceValid(h) && !h.Dead).ToList();
        if (heroes.Count == 0) return;
        var h = heroes[_rng.Next(heroes.Count)];
        var at = h.GlobalPosition + new Vector2(_rng.Next(2) == 0 ? -90 : 90, -20);
        _world.AddChild(new Glitch { Position = at });
    }

    public void GlitchArrived(Glitch g)
    {
        _sfx.Hush(true);
        _hud.ShowBanner("SØMETHING IS WRØNG", 3f);
        G.Fx.ScreenFlash(new Color(1f, 0f, 1f), 0.35f);
        G.Fx.AddShake(6);
    }

    public void GlitchLeft(Glitch g)
    {
        if (!G.Enemies.Any(e => e is Glitch && e != g && IsInstanceValid(e) && !e.Dead)) _sfx.Hush(false);
    }

    public void GlitchDefeated(Glitch g)
    {
        _sfx.Hush(false);
        _hud.ShowBanner("THE GLITCH IS GONE  ·  BUT THE GUARDIAN'S HALL IS WRONG NOW", 4.5f);
        OpenGlitchWall();
    }

    /// <summary>
    /// The Null's finds: a corrupted chest for each hero in the party (theirs alone to open first), in the rooms furthest from where you
    /// arrive. Every game places the same, from the seed and the party.
    /// </summary>
    private void PlaceNullFinds(CaveData cave, int seed)
    {
        if (G.Biome?.Id != BiomeId.Null) return;
        var owners = Net.Online ? Net.Peers.Keys.OrderBy(k => k).ToList() : new List<int> { 0 };
        var rooms = cave.Rooms.Where(r => r.Kind != RoomKind.Boss && !r.Underwater).OrderByDescending(r => r.Center.DistanceTo(cave.StartPos)).ToList();
        var used = new List<Vector2>();
        int ri = 0;
        foreach (int owner in owners)
        {
            Vector2? spot = null;
            for (; ri < rooms.Count && spot == null; ri++)
                if (cave.FindFloor(rooms[ri].Center, 300, out var f) && used.All(u => u.DistanceTo(f) > 60f)) spot = f;
            if (spot == null && cave.Boss != null && cave.FindFloor(cave.Boss.Center + new Vector2(used.Count * 50f - 50f, 0), 400, out var bf)) spot = bf;
            if (spot is not Vector2 at) continue;
            used.Add(at);
            var chest = new Chest { Position = at, Tier = ChestTier.Relic, Vault = true, Corrupt = true, Owner = owner };
            NetSync.LevelId(chest);
            _world.AddChild(chest);
        }
    }

    public int NullChests => G.World == null ? 0 : G.World.GetChildren().OfType<Chest>().Count(c => c.Corrupt);

    private float _skipT = 8f;
    /// <summary>For the tests: how many times this game's hero has skipped a frame in the Null.</summary>
    public int FrameSkips { get; private set; }

    /// <summary>
    /// In the Null nothing holds quite still: now and then this game's hero skips ahead a step the way they are going (never into the
    /// rock), with a chirp and a spray of broken pixels.
    /// </summary>
    private void TickNull(float dt)
    {
        var p = G.Player;
        if (G.Biome?.Id != BiomeId.Null || p == null || p.Dead || (_skipT -= dt) > 0) return;
        _skipT = 6f + (float)_rng.NextDouble() * 7f;
        if (p.Velocity.Length() < 40f || p.Shifted) return;
        var to = p.GlobalPosition + p.Velocity.Normalized() * 30f;
        var cave = G.Cave;
        if (cave.IsSolid(to) || cave.IsSolid(to + new Vector2(0, -12)) || !cave.LineClear(p.GlobalPosition, to)) return;
        G.Fx.Debris(p.GlobalPosition, Glitch.GlitchColor(), 5, 100);
        p.GlobalPosition = to;
        G.Fx.Debris(to, Glitch.GlitchColor(), 5, 100);
        G.Sfx.Play("glitch_chirp", to, -6, 0.4f, 1.6f);
        FrameSkips++;
    }

    /// <summary>
    /// The wall at the far end of the guardian's chamber (the end away from where the level begins) goes wrong: a patch of it turns to
    /// broken boxes and missing textures, and isn't really there. Walk into it and you go through, to the Null. Every game makes it the
    /// same, from the cave alone.
    /// </summary>
    public void OpenGlitchWall()
    {
        var cave = G.Cave;
        var room = cave?.Boss;
        if (_glitchWallOpen || room == null) return;
        _glitchWallOpen = true;
        int side = room.Center.X >= cave.StartPos.X ? 1 : -1;
        var from = new Vector2(room.Floor.X, room.Floor.Y - 20f);
        Vector2 hit = default;
        bool found = false;
        for (int pass = 0; pass < 2 && !found; pass++)
        {
            if (cave.Raycast(from, new Vector2(side, 0), room.RxPx * 3f + 200f, out hit, 2f)) found = true;
            else side = -side;
        }
        if (!found) hit = new Vector2(room.Center.X + side * room.RxPx, room.Floor.Y - 20f);
        // (its foot on the floor at the wall)
        var probe = hit - new Vector2(side * 12f, 10f);
        float footY = cave.FindFloor(probe, 200, out var f) ? f.Y : room.Floor.Y;
        var wall = new Portal { Position = new Vector2(hit.X - side * 4f, footY - 2f), Glitch = true, To = Biomes.Get(BiomeId.Null), Depth = G.Depth + 1, Label = "" };
        NetSync.FixedId(wall, GlitchWallId);
        _world.AddChild(wall);
        GlitchWall = wall;
    }
}
