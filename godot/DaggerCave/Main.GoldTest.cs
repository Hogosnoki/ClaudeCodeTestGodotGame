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
    }
}
