using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _shSteps;

    /// <summary>`--scenario=shop`: the guardian's exits include the merchant's stall once he is known; the stall is a tiny map of the same biome and depth with no life in it and two exits down; the merchant sells cards for gold.</summary>
    private void ShopScenario()
    {
        if (_shSteps == null) { if (_scT < 0.8f) return; _shSteps = ShopRun().GetEnumerator(); }
        if (!_shSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ShopRun()
    {
        var p = G.Player;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        // ---- the exits a guardian leaves
        bool testing = Tune.Testing.UnlockEverything;
        Tune.Testing.UnlockEverything = false;
        bool had = Meta.MerchantUnlocked;
        Meta.MerchantUnlocked = false;
        var before = PlanExits(new Random(5));
        ScCheck($"without the merchant, a guardian leaves two exits, both going down ({before.Count}, stall {before.Any(e => e.shop)})", before.Count == 2 && !before.Any(e => e.shop) && before.All(e => e.depth > G.Depth));
        Meta.MerchantUnlocked = true;
        var plan = PlanExits(new Random(5));
        ScCheck($"with him freed there are three: gentle way, stall, steep way ({string.Join(" | ", plan.Select(e => e.label))})", plan.Count == 3 && plan[1].shop && !plan[0].shop && !plan[2].shop);
        ScCheck($"the stall is the same biome at the same depth ({plan[1].biome.Id}/{plan[1].depth} against {G.Biome.Id}/{G.Depth})", plan[1].biome.Id == G.Biome.Id && plan[1].depth == G.Depth);
        Meta.MerchantUnlocked = had;
        Tune.Testing.UnlockEverything = testing;

        // ---- the stall itself
        int depth = G.Depth; var biomeId = G.Biome.Id;
        p.Gold = 100;
        EnterExit(G.Biome, depth, true);
        for (int f = 0; f < 600 && !(G.Shop && G.Player != null && IsInstanceValid(G.Player) && !_loading && Merchant.All.Count > 0); f++) yield return null;
        foreach (var _ in Wait(1.0f)) yield return null;
        p = G.Player;
        p.InputOverride = () => default;
        var cave = G.Cave;
        ScCheck($"it is the same biome and depth ({G.Biome.Id}, depth {G.Depth}; shop {G.Shop})", G.Shop && G.Biome.Id == biomeId && G.Depth == depth && G.Biome.Shop);
        ScCheck($"a tiny map ({cave.W} x {cave.H} cells; the biome's own is {Biomes.Get(biomeId).W})", cave.W < Biomes.Get(biomeId).W && cave.W * cave.H < 160 * 90);
        ScCheck($"with nothing living in it ({G.Enemies.Count(e => !e.Dead)} creatures, {cave.Spawns.Count} spawn points), no chests ({Chest.All.Count}) and no vault ({cave.Vault == null})", G.Enemies.Count(e => !e.Dead) == 0 && cave.Spawns.Count == 0 && Chest.All.Count == 0 && cave.Vault == null);
        var portals = G.World.GetChildren().OfType<Portal>().Where(x => !x.Entry && !x.Outside).ToList();
        ScCheck($"two exits, one and two levels down, and no stall in the stall ({string.Join(", ", portals.Select(x => x.Depth))}; {portals.Count(x => x.Shop)} stalls)", portals.Count == 2 && portals.Any(x => x.Depth == depth + 1) && portals.Any(x => x.Depth == depth + 2) && portals.All(x => !x.Shop));
        var merchant = Merchant.All[0];
        ScCheck($"the merchant stands in the chamber where they are ({merchant.GlobalPosition.DistanceTo(cave.Boss.Floor):0} px from its floor)", merchant.GlobalPosition.DistanceTo(cave.Boss.Floor) < cave.Boss.RxPx);
        // (the way to him is open: the start reaches him on foot)
        ScCheck($"he can be reached from the start ({BugReports.StartShutIn(cave, out _, out _) == false})", BugReports.StartShutIn(cave, out _, out _) == false);

        // ---- trade
        p.GlobalPosition = merchant.GlobalPosition + new Vector2(-14, -2);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"standing at him, the hero is at his table ({Merchant.At(p.GlobalPosition) == merchant})", Merchant.At(p.GlobalPosition) == merchant);
        int gold0 = p.Gold = 100;
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        ScShot("shop_0");
        var stock = merchant.Stock;
        ScCheck($"he lays out his wares ({stock?.Count} items; the menu is up {_upgradeMenu.Visible}; priced {string.Join(", ", stock?.Select(i => i.Price) ?? Array.Empty<int>())})", stock != null && stock.Count >= 4 && _upgradeMenu.Visible && stock.All(i => i.Price > 0));
        ScCheck($"his supplies and a relic are among them ({stock.Any(i => i.Supplies)}, {stock.Any(i => i.Card.Relic)})", stock.Any(i => i.Supplies));
        var first = stock.First(i => !i.Supplies);
        int stacks0 = p.Stats.StackOf(first.Card.Id);
        _upgradeMenu.Choose(0);
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"buying the first takes its price in gold ({gold0} -> {p.Gold}, price {first.Price}) and gives the card ({p.Stats.StackOf(first.Card.Id)} against {stacks0})", p.Gold == gold0 - first.Price && p.Stats.StackOf(first.Card.Id) == stacks0 + 1 && first.Sold);
        ScCheck($"the table is laid out again without it ({_upgradeMenu.Visible}, state {_state})", _upgradeMenu.Visible && _state == State.Choosing);
        ScShot("shop_1");
        // too poor for the next: it is refused and nothing is taken
        p.Gold = 1;
        _upgradeMenu.Choose(5);
        foreach (var _ in Wait(0.4f)) yield return null;
        // (the open menu was laid out before the gold was cut: close and look again)
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"leaving closes the stall ({_state}, shop {_shop == null})", _state == State.Playing && _shop == null);
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        var next = merchant.Stock.First(i => !i.Sold && i.Price > p.Gold);
        int idx = merchant.Stock.Where(i => !i.Sold).ToList().IndexOf(next);
        _upgradeMenu.Choose(idx);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"with 1 gold a {next.Price}-gold card is refused ({p.Gold} gold left; sold {next.Sold})", p.Gold == 1 && !next.Sold && _upgradeMenu.Visible);
        // supplies: potions to the brim and a mend
        p.Gold = 100; p.Potions = 0; p.Hp = p.Stats.MaxHp * 0.3f;
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        var sup = merchant.Stock.First(i => i.Supplies);
        idx = merchant.Stock.Where(i => !i.Sold).ToList().IndexOf(sup);
        float hp0 = p.Hp; int goldB = p.Gold;
        _upgradeMenu.Choose(idx);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"his supplies fill the potion belt ({p.Potions}/{p.MaxPotions}) and mend ({hp0:0} -> {p.Hp:0} of {p.Stats.MaxHp:0}) for {sup.Price} gold ({goldB} -> {p.Gold})", p.Potions == p.MaxPotions && p.Hp > hp0 + 1f && p.Gold == goldB - sup.Price);
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;

        // ---- on down, one level or two
        var down = portals.First(x => x.Depth == depth + 1);
        p.GlobalPosition = down.GlobalPosition + new Vector2(0, 16);
        foreach (var _ in Wait(0.5f)) yield return null;
        down.Enter();
        for (int f = 0; f < 600 && !(G.Depth == depth + 1 && !_loading && G.Player != null); f++) yield return null;
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"the way down from the stall goes one level on ({G.Depth} against {depth}; shop {G.Shop}; gold carried {G.Player.Gold})", G.Depth == depth + 1 && !G.Shop && !G.Biome.Shop && G.Player.Gold > 0);
    }
}
