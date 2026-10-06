using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _nkSteps;

    /// <summary>
    /// `--scenario=nooks --forcenooks`: the hidden ways in for creatures. A Shape Shifter fish swims the slit to its chamber and a hero
    /// cannot; a spider climbs its crack up out of the roof to the chamber.
    /// </summary>
    private void NooksScenario()
    {
        if (_nkSteps == null) { if (_scT < 0.6f) return; _nkSteps = NkRun().GetEnumerator(); }
        if (!_nkSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> NkRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 2000; p.Hp = 2000;
        var fish = cave.Hints.FirstOrDefault(h => h.Kind == 0);
        var crack = cave.Hints.FirstOrDefault(h => h.Kind == 1);
        ScCheck($"this cave has a fish's slit ({fish.Kind == 0 && fish.Pos != default}) and a spider's crack ({crack.Kind == 1 && crack.Pos != default})", (fish.Pos != default) || (crack.Pos != default));

        if (fish.Pos != default)
        {
            var room = cave.Rooms.Where(r => r.Kind == RoomKind.Secret && r.Underwater).OrderBy(r => r.Center.DistanceTo(fish.Pos)).First();
            int dir = Math.Sign(room.Center.X - fish.Pos.X);
            var start = fish.Pos + new Vector2(-dir * 44, 0);
            ScCheck($"the slit's mouth is in open water ({cave.IsWater(start)})", cave.IsWater(start) && !cave.IsSolid(start));
            // a hero cannot go in
            p.GlobalPosition = start; p.Velocity = Vector2.Zero;
            _scInput = new PlayerInput { Move = new Vector2(dir, 0) };
            foreach (var _ in SbSleep(3f)) yield return null;
            float ex = p.GlobalPosition.X;
            ScCheck($"a hero swimming at it goes no further than the rock ({Math.Abs(ex - fish.Pos.X):0} px from the mouth; the chamber is {Math.Abs(room.Center.X - fish.Pos.X):0} px in)", Math.Sign(ex - fish.Pos.X) != dir || Math.Abs(ex - fish.Pos.X) < 30);
            // a fish can
            SbBecome("fish", start);
            yield return null;
            ScCheck($"the Shape Shifter is a fish ({_sbGhost?.GetType().Name})", _sbGhost is Fish);
            _scInput = new PlayerInput { Move = new Vector2(dir, 0) };
            float best = float.MaxValue;
            for (float t = 0; t < 12f && best > 40f; t += (float)GetProcessDeltaTime())
            {
                best = Math.Min(best, p.GlobalPosition.DistanceTo(room.Center));
                if (t > 0.5f && (int)(t * 4) % 8 == 0) _scInput = new PlayerInput { Move = new Vector2(dir, 0) };
                yield return null;
            }
            ScCheck($"a fish swims the slit into the chamber (nearest {best:0} px)", best <= 40f);
            ScShot("nook_fish");
            _scInput = default;
            p.LeaveForm(false);
        }

        if (crack.Pos != default)
        {
            var room = cave.Rooms.Where(r => r.Kind == RoomKind.Secret && !r.Underwater).OrderBy(r => r.Center.DistanceTo(crack.Pos)).First();
            // on the roof a little way along from the crack, a spider crawls to it and then up into it
            SbBecome("spider", crack.Pos + new Vector2(-46, 40));
            yield return null;
            var sp = (Spider)_sbGhost;
            cave.FindCeiling(crack.Pos + new Vector2(-46, 30), 80, out var roofAt);
            SbTeleport(roofAt + new Vector2(0, 8));
            sp.TestCling(new Vector2(0, 1));
            _scInput = default;
            foreach (var _ in SbSleep(0.4f)) yield return null;
            float best = float.MaxValue, t0 = 0;
            for (float t = 0; t < 16f && best > 80f; t += (float)GetProcessDeltaTime())
            {
                // along the roof toward the crack until it is overhead, then up it
                float dx = crack.Pos.X - p.GlobalPosition.X;
                _scInput = Math.Abs(dx) > 5f && p.GlobalPosition.Y > crack.Pos.Y - 6 ? new PlayerInput { Move = new Vector2(Math.Sign(dx), -0.2f) } : new PlayerInput { Move = new Vector2(Math.Sign(dx) * 0.15f, -1) };
                best = Math.Min(best, p.GlobalPosition.DistanceTo(room.Center));
                yield return null;
            }
            ScCheck($"a spider climbs the crack up into its chamber (nearest {best:0} px, state {sp.State})", best <= 80f);
            ScShot("nook_spider");
            _scInput = default;
        }
    }

    private IEnumerator<object> _lpSteps;

    /// <summary>`--scenario=lamp`: a Guild lamp at the hero's feet is read: the page, the ember, the journal.</summary>
    private void LampScenario()
    {
        if (_lpSteps == null) { if (_scT < 0.6f) return; _lpSteps = LpRun().GetEnumerator(); }
        if (!_lpSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> LpRun()
    {
        var p = G.Player;
        var lamp = GuildLamp.All.FirstOrDefault();
        if (lamp == null && G.Cave.FindFloor(p.GlobalPosition + new Vector2(70, -30), 200, out var f))
            _world.AddChild(lamp = new GuildLamp { Position = f, Biome = G.Biome.Id });
        ScCheck("there is a lamp to read", lamp != null);
        if (lamp == null) yield break;
        p.GlobalPosition = lamp.GlobalPosition + new Vector2(-14, -14); p.Velocity = Vector2.Zero;
        Meta.JournalFound.Remove(G.Biome.Id.ToString());
        int embers = Meta.Embers;
        foreach (var _ in SbSleep(1.2f)) { p.GlobalPosition = lamp.GlobalPosition + new Vector2(-14, -14); yield return null; }
        ScShot("lamp_0");
        ScCheck($"the hero is within reach of it ({GuildLamp.At(p.GlobalPosition) == lamp})", GuildLamp.At(p.GlobalPosition) == lamp);
        lamp.Read();
        ScCheck($"the page is kept in the journal ({string.Join(",", Meta.JournalFound)}) and paid an ember ({embers} -> {Meta.Embers})", Meta.JournalFound.Contains(G.Biome.Id.ToString()) && Meta.Embers == embers + 1);
        foreach (var _ in SbSleep(0.8f)) yield return null;
        ScShot("lamp_1");
        lamp.Read();
        ScCheck($"reading it again pays nothing more ({Meta.Embers})", Meta.Embers == embers + 1);
    }
}
