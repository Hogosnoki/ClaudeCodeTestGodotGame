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
        // (the stall is offered twice a run, between the 1st depth and the 9th)
        bool okDraws = true;
        for (int k = 0; k < 200; k++) { RollStalls(); okDraws &= _stallAt1 >= Tune.Merchant.FirstDepth && _stallAt2 > _stallAt1 && _stallAt2 <= 9; }
        ScCheck($"his two stalls fall between the {Tune.Merchant.FirstDepth}st depth and the 9th, the first sooner ({okDraws}; e.g. {_stallAt1} and {_stallAt2})", okDraws);
        _stallAt1 = G.Depth + 1; _stallsOffered = 0;
        ScCheck($"none is offered before the first stall's depth ({PlanExits(new Random(5)).Count} exits at depth {G.Depth}, first stall at {_stallAt1})", PlanExits(new Random(5)).Count == 2);
        _stallAt1 = G.Depth; _stallAt2 = G.Depth + 2;
        var plan = PlanExits(new Random(5));
        ScCheck($"with him freed there are three: gentle way, stall, steep way ({string.Join(" | ", plan.Select(e => e.label))})", plan.Count == 3 && plan[1].shop && !plan[0].shop && !plan[2].shop);
        ScCheck($"the stall is the same biome at the same depth ({plan[1].biome.Id}/{plan[1].depth} against {G.Biome.Id}/{G.Depth})", plan[1].biome.Id == G.Biome.Id && plan[1].depth == G.Depth);
        _stallsOffered = 1;
        ScCheck($"after the first, none until the second's depth ({PlanExits(new Random(5)).Count} exits)", PlanExits(new Random(5)).Count == 2);
        _stallsOffered = 2; _stallAt2 = G.Depth;
        ScCheck($"and never a third ({PlanExits(new Random(5)).Count} exits)", PlanExits(new Random(5)).Count == 2);
        _stallsOffered = 0;
        Meta.MerchantUnlocked = had;
        Tune.Testing.UnlockEverything = testing;

        // ---- its door in the guardian's chamber (for the picture): beside a plain way down
        if (G.Cave.FindFloor(p.GlobalPosition + new Vector2(120, -40), 200, out var df) && G.Cave.FindFloor(p.GlobalPosition + new Vector2(260, -40), 200, out var df2))
        {
            var door = new Portal { Position = df + new Vector2(0, -30), To = G.Biome, Depth = G.Depth, Label = plan[1].label, Shop = true };
            var door2 = new Portal { Position = df2 + new Vector2(0, -30), To = Biomes.Get(BiomeId.Fungal), Depth = G.Depth + 1, Label = "depth 2  ·  the gentle way" };
            _world.AddChild(door); _world.AddChild(door2);
            p.GlobalPosition = df + new Vector2(0, -14);
            foreach (var _ in Wait(2.0f)) yield return null;
            ScShot("shop_door");
            door.QueueFree(); door2.QueueFree();
        }
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

        // ---- (frame times: the stall alone, with the stall's table up, with a chest's cards up)
        double Frames(int n) { ulong t0 = Time.GetTicksMsec(); for (int f = 0; f < n; f++) { } return 0; }
        IEnumerable<object> Timed(string what, int n) { ulong t0 = Time.GetTicksMsec(); for (int f = 0; f < n; f++) yield return null; GD.Print($"[frametime] {what}: {(Time.GetTicksMsec() - t0) / (float)n:0} ms/frame"); }
        foreach (var _ in Timed("stall, no menu", 20)) yield return null;
        // ---- trade
        p.GlobalPosition = merchant.GlobalPosition + new Vector2(-14, -2);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"standing at him, the hero is at his table ({Merchant.At(p.GlobalPosition) == merchant})", Merchant.At(p.GlobalPosition) == merchant);
        foreach (var _ in Wait(0.8f)) yield return null;
        ScShot("shop_stall");
        int gold0 = p.Gold = 100;
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        foreach (var _ in Timed("stall with the table up", 20)) yield return null;
        ScShot("shop_0");
        var stock = merchant.Stock;
        ScCheck($"he lays out his wares ({stock?.Count} items; the menu is up {_upgradeMenu.Visible}; priced {string.Join(", ", stock?.Select(i => i.Price) ?? Array.Empty<int>())})", stock != null && stock.Count >= 4 && stock.Count <= 7 && _upgradeMenu.Visible && stock.All(i => i.Price > 0));
        ScCheck($"a relic is among them ({stock.Any(i => i.Card.Relic)}) and there are no more than seven things on the table ({stock.Count})", stock.Any(i => i.Card.Relic) && stock.Count <= 7);
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
        // supplies: a potion for the belt, and a mend (what he has is luck: make sure both are on the table)
        if (merchant.Stock.All(i => i.Card.Id != "shop_potion")) merchant.Stock.Add(new ShopItem { Card = Potion, Price = PriceOf(Potion) });
        if (!merchant.Stock.Any(i => i.Supplies)) merchant.Stock.Add(new ShopItem { Card = Supplies, Price = PriceOf(Supplies) });
        p.Gold = 100; p.Potions = 0; p.Hp = p.Stats.MaxHp * 0.3f;
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        var pot = merchant.Stock.First(i => i.Card.Id == "shop_potion");
        int potIdx = merchant.Stock.Where(i => !i.Sold).ToList().IndexOf(pot);
        int goldP = p.Gold;
        _upgradeMenu.Choose(potIdx);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"he sells a potion for the belt ({p.Potions}/{p.MaxPotions}) for {pot.Price} gold ({goldP} -> {p.Gold})", p.Potions == 1 && p.Gold == goldP - pot.Price);
        var sup = merchant.Stock.First(i => i.Supplies);
        int idx2 = merchant.Stock.Where(i => !i.Sold).ToList().IndexOf(sup);
        float hp0 = p.Hp; int goldB = p.Gold;
        _upgradeMenu.Choose(idx2);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"his provisions mend ({hp0:0} -> {p.Hp:0} of {p.Stats.MaxHp:0}) for {sup.Price} gold ({goldB} -> {p.Gold})", p.Hp > hp0 + 1f && p.Gold == goldB - sup.Price);
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;

        // ---- shop relics and the grid's merchant hexes change what he asks
        p.Gold = 200;
        var cardItem = merchant.Stock.First(i => !i.Service);
        int plain = PriceOf(cardItem.Card);
        p.Stats.ShopPriceMult = 0.8f;
        int haggled = PriceOf(cardItem.Card);
        ScCheck($"Haggler's Tongue takes a fifth off ({plain} -> {haggled})", haggled == Math.Max(1, (int)MathF.Round(plain * 0.8f)) || Math.Abs(haggled - plain * 0.8f) <= 1f);
        p.Stats.ShopPriceMult = 1f;
        HexGrid.Owned.Add("shop_price1"); HexGrid.Owned.Add("shop_price2"); HexGrid.Owned.Add("shop_price3");
        int fair = PriceOf(cardItem.Card);
        ScCheck($"Fair Dealing III takes 15% off ({plain} -> {fair}, x{Meta.ShopPriceMult:0.00})", Math.Abs(Meta.ShopPriceMult - 0.85f) < 0.001f && Math.Abs(fair - plain * 0.85f) <= 1f);
        HexGrid.Owned.Remove("shop_price1"); HexGrid.Owned.Remove("shop_price2"); HexGrid.Owned.Remove("shop_price3");
        int rp0 = RerollPrice(merchant);
        p.Stats.RerollMult = 0.6f;
        int rp1 = RerollPrice(merchant);
        ScCheck($"Fence's Ledger makes dealing again 40% cheaper ({rp0} -> {rp1})", rp1 < rp0 && Math.Abs(rp1 - rp0 * 0.6f) <= 1f);
        p.Stats.RerollMult = 1f;
        p.Rerolls = 0; // (no Second Thoughts in hand: those would make a deal free too)
        p.Stats.FreeRerolls = 1;
        ScCheck($"Loaded Dice make the first deal-again free ({RerollPrice(merchant)})", RerollPrice(merchant) == 0);
        int goldR = p.Gold; int idsBefore = merchant.Stock.Where(i => !i.Service).Select(i => i.Card.Id).GetHashCode();
        var oldIds = merchant.Stock.Where(i => !i.Service).Select(i => i.Card.Id).ToList();
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        // (his table, then the trading stand, then dealing again)
        _upgradeMenu.Choose(merchant.Stock.Count(i => !i.Sold) + 1);
        foreach (var _ in Wait(0.4f)) yield return null;
        var newIds = merchant.Stock.Where(i => !i.Service).Select(i => i.Card.Id).ToList();
        ScCheck($"dealing again for free costs nothing ({goldR} -> {p.Gold}), the table has new cards ({string.Join(",", oldIds)} -> {string.Join(",", newIds)}) and the supplies stay ({merchant.Stock.Count(i => i.Service)})", p.Gold == goldR && merchant.Rerolls == 1 && merchant.Stock.Count(i => i.Service) >= 2 && newIds.Count >= 3);
        int paid = RerollPrice(merchant);
        ScCheck($"the second costs gold ({paid})", paid > 0);
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;
        p.Stats.FreeRerolls = 0;
        int baseCount = DealCards(merchant).Count;
        p.Stats.ShopExtra = 1;
        int extraCount = DealCards(merchant).Count;
        ScCheck($"Overstock puts one more thing on the table ({baseCount} -> {extraCount})", extraCount == baseCount + 1);
        // (his table is the same in every game of a party: dealt from the level's seed, not this game's dice or this hero's cards)
        var dealA = DealCards(merchant).Select(i => i.Card.Id).ToList();
        for (int k = 0; k < 50; k++) _rng.Next();
        p.Stats.Stacks[dealA[0]] = p.Stats.StackOf(dealA[0]) + 1;
        var dealB = DealCards(merchant).Select(i => i.Card.Id).ToList();
        p.Stats.Stacks[dealA[0]] -= 1;
        ScCheck($"his wares are the same in every game ({string.Join(",", dealA)} = {string.Join(",", dealB)})", dealA.SequenceEqual(dealB));
        p.Stats.ShopExtra = 0;
        // piles
        int pg = p.Gold; p.GainGold(5, true); int plainPile = p.Gold - pg;
        p.Stats.PileBonus = 3; pg = p.Gold; p.GainGold(5, true); int fat = p.Gold - pg;
        ScCheck($"Fat Purse: a pile of five holds three more ({plainPile} -> {fat})", fat == plainPile + 3 || Math.Abs(fat - (plainPile + 3 * Meta.GoldFoundMult * p.Stats.GoldMult)) <= 1f);
        p.Stats.PileBonus = 0;
        // a token for dealing a choice of cards again
        p.Gold = 100; p.Rerolls = 0;
        merchant.Stock.Add(new ShopItem { Card = Token, Price = PriceOf(Token) });
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        _upgradeMenu.Choose(merchant.Stock.Where(i => !i.Sold).ToList().FindIndex(i => i.Card.Id == "shop_token"));
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"the merchant sells a token for dealing a choice of cards again ({p.Rerolls} reroll, gold {p.Gold})", p.Rerolls == 1 && p.Gold < 100);
        _upgradeMenu.ChooseLeave();
        foreach (var _ in Wait(0.3f)) yield return null;
        // the trading stand: a relic carried for one of his three, a few times a run
        {
            var st = new PlayerStats(HeroKind.Swordsman);
            var killField = typeof(Triggers).GetField("Kill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int Kills(PlayerStats x) => (killField.GetValue(x.On) as Delegate)?.GetInvocationList().Length ?? 0;
            float dm0 = st.DamageMult, td0 = st.ThreatDist;
            Upgrades.Apply(Upgrades.Get("relic_soul"), st, null); Upgrades.Apply(Upgrades.Get("relic_w_banner"), st, null);
            int k1 = Kills(st);
            Upgrades.RemoveRelic("relic_soul", st, null); Upgrades.RemoveRelic("relic_w_banner", st, null);
            ScCheck($"a relic given back takes off what it did: its listener ({k1} -> {Kills(st)}) and its numbers (damage x{st.DamageMult:0.000}, threat x{st.ThreatDist:0.000})", k1 == 1 && Kills(st) == 0 && Math.Abs(st.DamageMult - dm0) < 1e-4f && Math.Abs(st.ThreatDist - td0) < 1e-4f && st.StackOf("relic_soul") == 0);
        }
        // (a party's trading first: a relic from one friend to another, through the stand)
        {
            p.Trades = 1;
            Upgrades.Apply(Upgrades.Get("relic_bounty"), p.Stats, p);
            ScCheck($"alone, the stand has no friend to trade with ({TradeFriends().Count})", TradeFriends().Count == 0);
            bool gave = GiveRelicTo(2, "relic_bounty");
            ScCheck($"giving a relic to a friend takes it off this hero and spends a trade ({gave}; carried {p.Stats.StackOf("relic_bounty")}, {p.Trades} left)", gave && p.Stats.StackOf("relic_bounty") == 0 && !RunRelics.Mine.Contains("relic_bounty") && p.Trades == 0);
            ScCheck("no trades left: nothing more can be given", !GiveRelicTo(2, "relic_highground"));
            RelicGiftReturned(2, "relic_bounty", "THEY CARRY IT ALREADY");
            ScCheck($"a gift the friend can't carry comes back, and the trade with it ({p.Stats.StackOf("relic_bounty")}, {p.Trades} left)", p.Stats.StackOf("relic_bounty") == 1 && p.Trades == 1);
            int got0 = GiftsReceived;
            bool tookGift = ReceiveRelicGift(2, "relic_lucky");
            ScCheck($"a friend's gift is carried at once ({tookGift}; Lucky Coin {p.Stats.StackOf("relic_lucky")})", tookGift && p.Stats.StackOf("relic_lucky") == 1 && GiftsReceived == got0 + 1);
            ScCheck("and one already carried is refused (it goes back to them)", !ReceiveRelicGift(2, "relic_lucky") && p.Stats.StackOf("relic_lucky") == 1);
        }
        // (and the merchant's own trading, kept for later behind a switch: a relic for one of his)
        Tune.Merchant.NpcTrades = true;
        p.Trades = 1;
        Upgrades.Apply(Upgrades.Get("relic_highground"), p.Stats, p);
        float hg = p.Stats.HighGroundMult;
        OpenShop(merchant);
        foreach (var _ in Wait(0.6f)) yield return null;
        _upgradeMenu.Choose(merchant.Stock.Count(i => !i.Sold));
        foreach (var _ in Wait(0.4f)) yield return null;
        var mineT = TradeableRelics(p);
        ScCheck($"the trading stand asks which relic to give ({_tradeStep}; {string.Join(", ", mineT.Select(u => u.Name))})", _tradeStep == 1 && _upgradeMenu.Visible && mineT.Any(u => u.Id == "relic_highground"));
        ScShot("shop_trade_give");
        _upgradeMenu.Choose(mineT.FindIndex(u => u.Id == "relic_highground"));
        foreach (var _ in Wait(0.4f)) yield return null;
        var offer = _tradeOffer?.ToList() ?? new List<Upgrade>();
        ScCheck($"then offers {Tune.Merchant.TradeOffer} of his, none carried ({string.Join(", ", offer.Select(u => u.Name))})", _tradeStep == 2 && offer.Count == Tune.Merchant.TradeOffer && offer.All(u => u.Relic && p.Stats.StackOf(u.Id) == 0 && u.Id != "relic_highground"));
        ScShot("shop_trade_get");
        var took = offer.FirstOrDefault();
        _upgradeMenu.Choose(0);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"the trade: High Ground is gone (x{hg:0.00} -> x{p.Stats.HighGroundMult:0.00}), {took?.Name} is carried, a trade spent ({p.Trades} left)", took != null && Math.Abs(p.Stats.HighGroundMult - 1f) < 1e-4f && !RunRelics.Mine.Contains("relic_highground") && p.Stats.StackOf(took.Id) == 1 && RunRelics.Mine.Contains(took.Id) && p.Trades == 0);
        ScCheck($"a run brings {Tune.Merchant.Trades} trade, and the grid's Trader hexes one or two more ({Meta.Trades} now)", Meta.Trades >= Tune.Merchant.Trades && HexGrid.Find("shop_trade2") != null);
        _upgradeMenu.ChooseLeave();
        Tune.Merchant.NpcTrades = false;
        foreach (var _ in Wait(0.3f)) yield return null;
        // rerolling a chest's cards
        p.Rerolls = 2;
        var chest = new Chest { Position = p.GlobalPosition + new Vector2(0, 0), Tier = ChestTier.Wood };
        _world.AddChild(chest);
        chest.Look();
        foreach (var _ in Wait(0.8f)) yield return null;
        var cards0 = chest.Cards.ToArray();
        ScCheck($"a chest's choice offers dealing again, two left ({_upgradeMenu.Visible}, {_upgradeMenu.RerollsLeft})", _upgradeMenu.Visible && _upgradeMenu.Reroll != null && _upgradeMenu.RerollsLeft == 2);
        foreach (var _ in Timed("a chest's 4 cards up", 10)) yield return null;
        ScShot("shop_reroll_menu");
        _upgradeMenu.Reroll();
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"dealing again spends one and lays out cards again ({p.Rerolls} left; {string.Join(",", cards0)} -> {string.Join(",", chest.Cards)})", p.Rerolls == 1 && _upgradeMenu.RerollsLeft == 1 && chest.Cards.Length == 3);
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
