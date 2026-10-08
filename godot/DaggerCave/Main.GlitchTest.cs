using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _glSteps;

    /// <summary>
    /// `--scenario=glitch`: the Glitch, start to finish. It silences the music; it never glides (it stutters and blinks beside a hero); its
    /// blow rolls a hero's health anew but can't take it to nothing before the eighth; a blow rolls its own health anew and nothing but
    /// the hundredth and after (not bleeding) brings it down; beaten, the music comes back and the far wall of the guardian's chamber goes
    /// wrong; walking into that wall takes you to the Null, where corrupted chests wait and the hero skips frames. glitch_*.png.
    /// </summary>
    private void GlitchScenario()
    {
        if (_glSteps == null) { if (_scT < 0.6f) return; _glSteps = GlitchRun().GetEnumerator(); }
        if (!_glSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GlitchRun()
    {
        var p = G.Player;
        p.Stats.MaxHp = 1000; p.Hp = 1000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        bool found = FindRun(1, 120, false, out var stand, 300f);
        ScCheck($"a floor to meet it on ({stand.Round()})", found);
        if (!found) yield break;
        p.GlobalPosition = stand; p.Velocity = Vector2.Zero;
        _sfx.SetMusic("ambient");
        foreach (var _ in Wait(0.3f)) yield return null;

        // ---------------------------------------------------------------- it comes, and the music stops
        var g = new Glitch { Position = stand + new Vector2(90, -20) };
        _world.AddChild(g);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"it is here, and the music has stopped ({_sfx.Hushed})", GodotObject.IsInstanceValid(g) && _sfx.Hushed);
        ScShot("glitch_here");

        // ---------------------------------------------------------------- it never glides
        var last = g.GlobalPosition;
        int frames = 0, moving = 0, blinks0 = g.Blinks;
        float biggest = 0f;
        var hps = new List<float>();
        float hp0 = p.Hp;
        for (float t = 0; t < 6f; t += (float)GetProcessDeltaTime())
        {
            p.GlobalPosition = new Vector2(stand.X, p.GlobalPosition.Y);
            float d = g.GlobalPosition.DistanceTo(last);
            last = g.GlobalPosition;
            frames++;
            if (d > 0.01f) moving++;
            biggest = Math.Max(biggest, d);
            if (Math.Abs(p.Hp - hp0) > 0.5f) { hps.Add(p.Hp); hp0 = p.Hp; }
            if (t > 1.5f && t < 1.5f + (float)GetProcessDeltaTime() * 1.5f) ScShot("glitch_near");
            yield return null;
        }
        ScCheck($"it doesn't glide: still most frames ({moving} of {frames} moved), then a jump at a time (the biggest {biggest:0} px), and it blinked {g.Blinks - blinks0} times",
            moving < frames * 0.5f && g.Blinks - blinks0 >= 1 && biggest > 8f);
        ScCheck($"its blows rolled the hero's health anew ({string.Join(", ", hps.Select(h => h.ToString("0")))}), never to nothing so early (alive {!p.Dead})", hps.Count >= 1 && !p.Dead);

        // ---------------------------------------------------------------- its blow, seven times: never to nothing
        g.QueueFree();
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"gone (not beaten), the music comes back ({!_sfx.Hushed})", !_sfx.Hushed);
        p.Hp = p.Stats.MaxHp;
        var rolled = new List<float>();
        while (p.GlitchHits < Tune.Glitch.HeroHitsToMortal - 1)
        {
            p.TestClearStatus();
            p.Afflict(NetSync.Boon.Glitch, 0, 0);
            rolled.Add(p.Hp);
            foreach (var _ in Wait(0.45f)) yield return null;
        }
        ScCheck($"seven of its blows: health rolled anew each time ({string.Join(", ", rolled.Select(h => h.ToString("0")))}), and the hero still stands",
            !p.Dead && rolled.All(h => h >= 1f && h <= p.Stats.MaxHp) && rolled.Distinct().Count() >= 3);
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- a hundred blows before it can fall
        var g2 = new Glitch { Position = stand + new Vector2(60, -20) };
        _world.AddChild(g2);
        foreach (var _ in Wait(0.3f)) yield return null;
        float lo = float.MaxValue, hi = 0f;
        for (int k = 0; k < Tune.Glitch.HitsToMortal - 1 && GodotObject.IsInstanceValid(g2) && !g2.Dead; k++)
        {
            g2.Hurt(50f, Vector2.Zero, g2.GlobalPosition);
            lo = Math.Min(lo, g2.Hp); hi = Math.Max(hi, g2.Hp);
        }
        ScCheck($"99 blows and it stands ({!g2.Dead}): its health was rolled anywhere from {lo:0} to {hi:0} of {g2.MaxHp:0}", !g2.Dead && lo < g2.MaxHp * 0.3f && hi > g2.MaxHp * 0.7f && lo >= 1f);
        g2.Bleed(100000f, 0.5f);
        foreach (var _ in Wait(0.8f)) yield return null;
        ScCheck($"bleeding can't bring it down either ({!g2.Dead}, {g2.Hp:0})", GodotObject.IsInstanceValid(g2) && !g2.Dead);
        var at = g2.GlobalPosition;
        int more = 0;
        while (GodotObject.IsInstanceValid(g2) && !g2.Dead && more < 400) { g2.Hurt(50f, Vector2.Zero, g2.GlobalPosition); more++; }
        ScCheck($"after the hundredth, a blow can roll nothing: it fell {more} blows later", GodotObject.IsInstanceValid(g2) ? g2.Dead : true);
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"beaten, the music comes back ({!_sfx.Hushed})", !_sfx.Hushed);

        // ---------------------------------------------------------------- the wall goes wrong
        var wall = GlitchWall;
        var room = G.Cave.Boss;
        ScCheck($"the far end of the guardian's chamber has gone wrong ({wall?.GlobalPosition.Round()}, the chamber at {room?.Center.Round()})",
            wall != null && room != null && Math.Abs(wall.GlobalPosition.X - room.Center.X) > room.RxPx * 0.4f && wall.To?.Id == BiomeId.Null);
        if (wall == null) yield break;
        p.GlobalPosition = wall.GlobalPosition + new Vector2(Math.Sign(room.Center.X - wall.GlobalPosition.X) * 60f, -14f);
        p.Velocity = Vector2.Zero;
        foreach (var _ in Wait(1.2f)) yield return null;
        ScShot("glitch_wall");
        // walk into it
        int side = Math.Sign(wall.GlobalPosition.X - p.GlobalPosition.X);
        _scInput = new PlayerInput { Move = new Vector2(side, 0) };
        p.InputOverride = () => _scInput;
        int depth0 = G.Depth;
        for (float t = 0; t < 8f && !_loading && G.Biome.Id != BiomeId.Null; t += (float)GetProcessDeltaTime()) yield return null;
        _scInput = default;
        for (float t = 0; t < 90f && (_loading || G.Biome.Id != BiomeId.Null); t += (float)GetProcessDeltaTime()) yield return null;
        ScCheck($"walking into it takes you through, to {G.Biome.Name} at depth {G.Depth} (from {depth0})", G.Biome.Id == BiomeId.Null && G.Depth == depth0 + 1 && !_loading);
        foreach (var _ in Wait(1.5f)) yield return null;
        // (a new level: the hero is made anew)
        p = G.Player;
        p.InputOverride = () => _scInput;
        ScShot("glitch_null");

        // ---------------------------------------------------------------- the Null
        ScCheck($"a corrupted chest waits there ({NullChests})", NullChests >= 1);
        var cards = Upgrades.RollCorruptCards(p.Stats, new Random(5));
        ScCheck($"and deals corrupted cards ({string.Join(", ", cards)})", cards.Length == 3 && cards.All(c => c.StartsWith("c_")));
        float dm = p.Stats.DamageMult, dt0 = p.Stats.DamageTakenMult;
        Upgrades.Get("c_overflow").Apply(p.Stats, p);
        ScCheck($"Overflow: three times the damage dealt ({p.Stats.DamageMult / dm:0.00}x) and taken ({p.Stats.DamageTakenMult / dt0:0.00}x)",
            Math.Abs(p.Stats.DamageMult / dm - 3f) < 0.01f && Math.Abs(p.Stats.DamageTakenMult / dt0 - 3f) < 0.01f);
        var exits = _world.GetChildren().OfType<Portal>().Where(x => !x.Drain && !x.Outside && !x.Entry && !x.Glitch).ToList();
        ScCheck($"and the way on is open from the start ({exits.Count} exits)", exits.Count >= 1);
        // it doesn't hold still: a hero on the move skips a step now and then
        if (FindRun(1, 120, false, out var run, 200f)) { p.GlobalPosition = run; p.Velocity = Vector2.Zero; }
        _skipT = 0.3f;
        int skips0 = FrameSkips;
        _scInput = new PlayerInput { Move = new Vector2(1, 0) };
        for (float t = 0; t < 3f && FrameSkips == skips0; t += (float)GetProcessDeltaTime()) { if (t > 1.5f) _skipT = Math.Min(_skipT, 0.05f); yield return null; }
        _scInput = default;
        ScCheck($"on the move, the hero skips a frame now and then ({FrameSkips - skips0})", FrameSkips > skips0);
    }
}
