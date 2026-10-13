using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _goldSteps;

    /// <summary>`--scenario=gold`: gold hidden in the dead ends (single coins, and now and then a pile of four to eight), picked up by touch into the purse.</summary>
    private void GoldScenario()
    {
        if (_goldSteps == null) { if (_scT < 0.8f) return; _goldSteps = GoldRun().GetEnumerator(); }
        if (!_goldSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GoldRun()
    {
        var p = G.Player; var cave = G.Cave;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 110;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        var coins = _world.GetChildren().OfType<CoinPickup>().ToList();
        int piles = coins.Count(c => c.IsPile), singles = coins.Count - piles;
        ScCheck($"the level has dead ends ({cave.DeadEnds.Count})", cave.DeadEnds.Count >= 4);
        int withGold = cave.DeadEnds.Count(d => coins.Any(c => c.GlobalPosition.DistanceTo(d) < 40f));
        ScCheck($"gold lies in every one of them ({withGold} of {cave.DeadEnds.Count}; {singles} coins, {piles} piles)", withGold == cave.DeadEnds.Count);
        ScCheck($"a pile is worth four to eight ({string.Join(",", coins.Where(c => c.IsPile).Select(c => c.Count))}) and a coin one", coins.Where(c => c.IsPile).All(c => c.Count >= 4 && c.Count <= 8) && coins.Where(c => !c.IsPile).All(c => c.Count == 1));
        ScCheck($"the dead ends are away from where you arrive ({cave.DeadEnds.Min(d => d.DistanceTo(cave.StartPos)):0} px at the nearest)", cave.DeadEnds.All(d => d.DistanceTo(cave.StartPos) > 20 * CaveData.Cell));

        // gold elsewhere too: high platforms, ledges, chambers, quiet floors
        var offEnds = coins.Where(c => cave.DeadEnds.All(d => d.DistanceTo(c.GlobalPosition) > 60f)).ToList();
        ScCheck($"gold lies elsewhere than the dead ends ({offEnds.Count} coins and piles in {MoreGoldPlaces} other places)", MoreGoldPlaces >= 4 && offEnds.Count >= MoreGoldPlaces);
        int high = offEnds.Count(c => cave.FindFloor(c.GlobalPosition + new Vector2(0, 8), 900, out var u) ? u.Y - c.GlobalPosition.Y > 100f : true);
        ScCheck($"some of it on high platforms with nothing under them ({high} of {offEnds.Count})", high >= 1);
        var chests = Chest.All.Where(IsInstanceValid).ToList();
        float nearChest = chests.Count == 0 || offEnds.Count == 0 ? 99f : chests.Min(ch => offEnds.Min(c => c.GlobalPosition.DistanceTo(ch.GlobalPosition)));
        ScCheck($"none of it on top of a chest ({chests.Count} chests; nearest coin {nearChest:0} px)", chests.All(ch => offEnds.All(c => c.GlobalPosition.DistanceTo(ch.GlobalPosition) > 60f)));
        bool Walkable(Vector2 at)
        {
            for (int dj = -2; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int i = Math.Clamp((int)(at.X / CaveData.Cell) + di, 0, cave.W - 1), j = Math.Clamp((int)(at.Y / CaveData.Cell) + dj, 0, cave.H - 1);
                    if (cave.ReachMask[j * cave.W + i]) return true;
                }
            return false;
        }
        int unreachable = offEnds.Count(c => !Walkable(c.GlobalPosition));
        ScCheck($"and every place can be walked to ({unreachable} cannot)", unreachable == 0);

        // a look at a pile and a coin before they go (stood a little way off)
        var look = coins.FirstOrDefault(c => c.IsPile);
        if (look != null)
        {
            p.GlobalPosition = look.GlobalPosition + new Vector2(-34, -6); p.Velocity = Vector2.Zero;
            foreach (var _ in Wait(1.2f)) yield return null;
            ScShot("gold_pile");
        }
        // pick one up by walking into it
        var coin = coins.OrderBy(c => c.IsPile ? 1 : 0).First();
        int before = p.Gold;
        p.GlobalPosition = coin.GlobalPosition + new Vector2(0, -4); p.Velocity = Vector2.Zero;
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"touching a coin puts it in the purse, with any beside it ({before} -> {p.Gold})", p.Gold >= before + coin.Count && !IsInstanceValid(coin));
        var pile = coins.FirstOrDefault(c => c.IsPile && IsInstanceValid(c));
        if (pile != null)
        {
            int cnt = pile.Count; before = p.Gold;
            p.GlobalPosition = pile.GlobalPosition + new Vector2(0, -4); p.Velocity = Vector2.Zero;
            foreach (var _ in Wait(0.4f)) yield return null;
            ScCheck($"and a pile its whole worth ({before} -> {p.Gold}, a pile of {cnt})", p.Gold >= before + cnt);
        }
        ScCheck($"gold found this run counts it all ({p.GoldFound})", p.GoldFound == p.Gold);
        ScCheck($"gold can be spent, and not past what there is ({p.SpendGold(p.Gold + 1)}, then {p.SpendGold(1)})", !p.SpendGold(p.Gold + 1) && p.Gold >= 0);
        ScShot("gold_hud");

        // ---- creatures drop gold now and then
        foreach (var c in _world.GetChildren().OfType<CoinPickup>().ToList()) c.QueueFree();
        float oldDrop = Tune.Gold.DropChance; Tune.Gold.DropChance = 1f;
        var foe = new Golem { Position = p.GlobalPosition + new Vector2(60, -30) };
        _world.AddChild(foe);
        foe.Wake(); foe.Hp = 1f;
        foreach (var _ in Wait(0.3f)) yield return null;
        foe.Hurt(9999f, Vector2.Zero, foe.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.2f)) yield return null;
        var dropped = _world.GetChildren().OfType<CoinPickup>().Where(c => c.Dropped).ToList();
        ScCheck($"a creature that dies may leave gold ({dropped.Count} dropped, worth {string.Join(",", dropped.Select(c => c.Count))})", dropped.Count == 1);
        float y0 = dropped.Count > 0 ? dropped[0].GlobalPosition.Y : 0;
        foreach (var _ in Wait(1.5f)) yield return null;
        bool onFloor = dropped.Count > 0 && IsInstanceValid(dropped[0]) && G.Cave.IsSolid(dropped[0].GlobalPosition + new Vector2(0, 4));
        ScCheck($"it falls to the floor and lies there ({y0:0} -> {(dropped.Count > 0 && IsInstanceValid(dropped[0]) ? dropped[0].GlobalPosition.Y : 0):0})", onFloor);
        // a hive's hornet leaves none
        foreach (var c in _world.GetChildren().OfType<CoinPickup>().ToList()) c.QueueFree();
        var h = new Hornet { Brood = true, Position = p.GlobalPosition + new Vector2(60, -60) };
        _world.AddChild(h);
        foreach (var _ in Wait(0.3f)) yield return null;
        h.Hurt(9999f, Vector2.Zero, h.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"a hive's hornet leaves none ({_world.GetChildren().OfType<CoinPickup>().Count(c => c.Dropped)})", !_world.GetChildren().OfType<CoinPickup>().Any(c => c.Dropped));
        Tune.Gold.DropChance = oldDrop;
    }
}
