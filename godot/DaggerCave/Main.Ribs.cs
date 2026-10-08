using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private int _ribStep;

    /// <summary>`--scenario=ribs` (the fossil graveyards): the hero walks along the inside of a leviathan's ribcage, under its spine; ribs_*.png.</summary>
    private void RibsScenario()
    {
        var p = G.Player; var cave = G.Cave;
        if (_ribStep == 0)
        {
            if (_scT < 0.6f) return;
            p.Stats.MaxHp = 9000; p.Hp = 9000;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            foreach (var r in cave.Rooms) r.Triggered = true;
            var room = cave.Rooms.Where(r => r.RxPx / CaveData.Cell >= 9f && r.RyPx / CaveData.Cell >= 6f).OrderBy(r => r.Center.DistanceTo(cave.StartPos)).FirstOrDefault();
            ScCheck($"a great chamber to carry a ribcage ({room?.Center.Round()})", room != null);
            if (room == null) { ScEnd(); return; }
            p.GlobalPosition = room.Floor + new Vector2(-room.RxPx * 0.35f, -16);
            p.Velocity = Vector2.Zero;
            _ribStep = 1; _scT = 0;
            return;
        }
        if (_ribStep == 1 && _scT > 2.0f) { ScShot("ribs_a"); G.Player.GlobalPosition += new Vector2(150, 0); _ribStep = 2; _scT = 0; return; }
        if (_ribStep == 2 && _scT > 1.8f) { ScShot("ribs_b"); ScEnd(); }
    }
}
