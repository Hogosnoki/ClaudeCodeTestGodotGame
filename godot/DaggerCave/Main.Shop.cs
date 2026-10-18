using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The merchant's stall: his wares are dealt for the hero who looks, priced in gold; every purchase takes the card at once and the table is laid out again without it.</summary>
public partial class Main
{
    private Merchant _shop;

    /// <summary>The "leave" card of the stall.</summary>
    public static readonly Upgrade LeaveShop = new() { Id = "leave_shop", Name = "Walk Away", Desc = "Close the stall. His wares stay as they are until you go down.", Icon = "skip", MaxStacks = 1, Apply = (s, p) => { } };
    /// <summary>Dealing his wares again.</summary>
    public static readonly Upgrade RerollCard = new() { Id = "shop_reroll", Name = "Deal Again", Desc = "He shuffles the cards he has not sold and lays out new ones. Each time costs more.", Icon = "spell", MaxStacks = 1, Apply = (s, p) => { } };
    /// <summary>A token that lets a choice of cards be dealt again (once).</summary>
    public static readonly Upgrade Token = new() { Id = "shop_token", Name = "Second Thoughts", Icon = "spell", MaxStacks = 1, Apply = (s, p) => { }, Desc = "One more time in this run, a choice of cards (a chest's, a milestone's) can be dealt again." };
    /// <summary>His supplies, a stock item that is no upgrade: a full belt of potions and a bandage.</summary>
    public static readonly Upgrade Supplies = new()
    {
        Id = "shop_supplies", Name = "Provisions", Icon = "life", MaxStacks = 1, Apply = (s, p) => { },
        Desc = $"A meal and a bandage: mends {Tune.Merchant.SuppliesHeal:0%} of your health.",
    };
    /// <summary>His trading stand: a relic the hero carries for one of his.</summary>
    public static readonly Upgrade TradeCard = new() { Id = "shop_trade", Name = "Trading Stand", Icon = "relic", MaxStacks = 1, Apply = (s, p) => { }, Desc = "" };
    /// <summary>Back from the trading stand to his table.</summary>
    public static readonly Upgrade TradeBack = new() { Id = "trade_back", Name = "Not Now", Desc = "Back to his table.", Icon = "skip", MaxStacks = 1, Apply = (s, p) => { } };
    /// <summary>The trading stand's step: 0 at his table, 1 choosing what to give, 2 choosing what to take; what is being given; what he offers.</summary>
    private int _tradeStep;
    private string _tradeGive;
    private List<Upgrade> _tradeOffer;

    /// <summary>The relics this hero could hand over (not those that change the world).</summary>
    private static List<Upgrade> TradeableRelics(Player p) => RunRelics.Mine.Where(id => Upgrades.Tradeable(id, p.Stats)).Select(Upgrades.Find).Where(u => u != null).ToList();

    /// <summary>A potion for the belt.</summary>
    public static readonly Upgrade Potion = new() { Id = "shop_potion", Name = "Healing Potion", Icon = "life", MaxStacks = 1, Apply = (s, p) => { }, Desc = "One more potion for your belt (drink it with the potion key)." };

    /// <summary>What a card costs here: by its rarity, a little more with every level down, and what the hero's relics make of it.</summary>
    public static int PriceOf(Upgrade u)
    {
        var st = G.Player?.Stats;
        if (u.Id == "shop_supplies" && st?.FreeSupplies == true) return 0;
        int basePrice = u.Id == "shop_supplies" ? Tune.Merchant.PriceSupplies : u.Id == "shop_token" ? Tune.Merchant.PriceToken : u.Id == "shop_potion" ? Tune.Merchant.PriceSupplies * 2 / 3 : UiArt.RarityOf(u) switch
        {
            UiArt.Rarity.Relic => Tune.Merchant.PriceRelic,
            UiArt.Rarity.Rare => Tune.Merchant.PriceRare,
            UiArt.Rarity.Uncommon => Tune.Merchant.PriceUncommon,
            _ => Tune.Merchant.PriceCommon,
        };
        // (the Ember Grid's Merchant's Favor: his cards cost more)
        float favor = HexGrid.Owns("shop_favor") && !u.Relic && u.Id is not ("shop_supplies" or "shop_token" or "shop_potion") ? Tune.Grid.FavorCardMult : 1f;
        return Math.Max(1, (int)MathF.Round(basePrice * favor * (1f + Tune.Merchant.PerDepth * Math.Max(0, G.Depth)) * (st?.ShopPriceMult ?? 1f) * Meta.ShopPriceMult));
    }

    /// <summary>The stall's own free deal is still to be had (Loaded Dice).</summary>
    public static bool FreeDealLeft(Merchant m) => G.Player?.Stats is { } st && m.FreeRerollsUsed < st.FreeRerolls;

    /// <summary>What dealing his wares again costs the hero now (0: free, by Loaded Dice or a Second Thoughts charge).</summary>
    public static int RerollPrice(Merchant m)
    {
        var st = G.Player?.Stats;
        if (FreeDealLeft(m) || (G.Player?.Rerolls ?? 0) > 0) return 0;
        float price = Tune.Merchant.PriceReroll * (1f + Tune.Merchant.PerDepth * Math.Max(0, G.Depth)) * (1f + Tune.Merchant.RerollStep * m.Rerolls) * (st?.ShopPriceMult ?? 1f) * Meta.ShopPriceMult * (st?.RerollMult ?? 1f);
        return Math.Max(1, (int)MathF.Round(price));
    }

    /// <summary>
    /// His dice: the same in every game of a party (the level's seed, the depth and how many times he has dealt), so everyone sees the
    /// same wares; a separate stream for each part of his table, so what a hero's own relics add doesn't change the rest.
    /// </summary>
    private static Random ShopRng(Merchant m, int stream) => new(unchecked((G.Cave?.Seed ?? 1) * 7919 + G.Depth * 104729 + m.Rerolls * 7907 + stream * 1000003));

    /// <summary>The party's heroes, in the same order in every game (alone, this game's hero).</summary>
    private static List<HeroKind> ShopParty() => (Net.Online && Net.Count > 1 ? NetSync.PartyHeroes() : new[] { G.Hero }).Distinct().OrderBy(h => h).ToList();

    /// <summary>
    /// His cards: a class card for one of the party's heroes and a card anyone could take (as a chest deals), a relic, and one more for
    /// each thing a relic or the Ember Grid adds to his table. Dealt alike in every game (see <see cref="ShopRng"/>), from what a hero
    /// fresh to the run could take: what one already has is shown, locked, as at a chest.
    /// </summary>
    private List<ShopItem> DealCards(Merchant m)
    {
        var p = G.Player;
        var party = ShopParty();
        var fresh = party.ToDictionary(h => h, h => new PlayerStats(h));
        bool Open(Upgrade u) => party.Any(h => Upgrades.Available(u, fresh[h]));
        float W(Upgrade u) => u.Weight;
        var items = new List<ShopItem>();
        void Add(IEnumerable<Upgrade> cards) { foreach (var c in cards) items.Add(new ShopItem { Card = c, Price = PriceOf(c) }); }
        bool Fresh(Upgrade u) => items.All(i => i.Card.Id != u.Id);
        var rng = ShopRng(m, 0);
        var hero = party[rng.Next(party.Count)];
        Add(Upgrades.Pick(Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Class && !u.Alteration && u.For.Contains(hero) && Upgrades.Available(u, fresh[hero])).ToList(), 1, rng, W));
        Add(Upgrades.Pick(Upgrades.Chest.Where(u => Upgrades.IsChestFiller(u) && Open(u)).ToList(), Math.Max(0, Tune.Merchant.Cards - items.Count), rng, W));
        var relics = Upgrades.Relics.Where(u => Open(u)).ToList();
        Add(Upgrades.Pick(relics.Where(Fresh).ToList(), 1, ShopRng(m, 1), W));
        // (Merchant's Favor: a second relic on his table)
        if (HexGrid.Owns("shop_favor")) Add(Upgrades.Pick(relics.Where(Fresh).ToList(), 1, ShopRng(m, 2), W));
        // (Overstock: one more card each, the same ones in every game)
        var more = ShopRng(m, 3);
        for (int k = 0; k < p.Stats.ShopExtra; k++)
            Add(Upgrades.Pick(Upgrades.Chest.Where(u => Fresh(u) && (u.Kind == UpgradeKind.Class && !u.Alteration && party.Any(h => u.For.Contains(h)) || Upgrades.IsChestFiller(u)) && Open(u)).ToList(), 1, more, W));
        return items;
    }

    /// <summary>His wares: the cards, and two of his supplies (a potion, provisions, or, often, a token of Second Thoughts: what he has is a matter of luck, the same luck in every game).</summary>
    private List<ShopItem> DealStock(Merchant m)
    {
        var items = DealCards(m);
        var rng = ShopRng(m, 4);
        var pool = new List<(Upgrade card, float weight)> { (Potion, 1f), (Supplies, 1f), (Token, Tune.Merchant.TokenWeight) };
        for (int k = 0; k < Tune.Merchant.Supplies && pool.Count > 0; k++)
        {
            double r = rng.NextDouble() * pool.Sum(x => x.weight);
            int pick = pool.Count - 1;
            for (int j = 0; j < pool.Count; j++) { r -= pool[j].weight; if (r <= 0) { pick = j; break; } }
            items.Add(new ShopItem { Card = pool[pick].card, Price = PriceOf(pool[pick].card) });
            pool.RemoveAt(pick);
        }
        return items;
    }

    /// <summary>He deals the cards again (the supplies stay as they were).</summary>
    private void Reroll(Merchant m)
    {
        var p = G.Player;
        int price = RerollPrice(m);
        if (price > p.Gold) { ShowShop(); return; }
        // (free: the stall's own free deal first, then a Second Thoughts charge)
        if (price == 0) { if (FreeDealLeft(m)) m.FreeRerollsUsed++; else p.Rerolls--; }
        p.Gold -= price; p.GoldSpent += price;
        m.Rerolls++;
        var keep = m.Stock.Where(i => i.Service).ToList();
        m.Stock = DealCards(m).Concat(keep).ToList();
        _sfx.Play("card_reveal", null, -2, 0, 1.2f);
        G.Fx.Text(m.GlobalPosition + new Vector2(0, -52), price == 0 ? "a fresh deal" : $"-{price} gold", new Color(1f, 0.82f, 0.4f), 11, 1.2f);
        ShowShop();
    }

    /// <summary>The hero pressed interact at the merchant.</summary>
    public void OpenShop(Merchant m)
    {
        var p = G.Player;
        if (p == null || p.Dead || _upgradeMenu.Visible || _state != State.Playing) return;
        m.Stock ??= DealStock(m);
        _shop = m;
        ShowShop();
    }

    private void ShowShop()
    {
        var p = G.Player;
        var m = _shop;
        var items = m.Stock.Where(i => !i.Sold).ToList();
        if (items.Count == 0) { CloseShop(); _hud.ShowBanner("SOLD OUT", 1.4f); return; }
        var choices = items.Select(i => i.Card).Append(TradeCard).Append(RerollCard).Append(LeaveShop).ToList();
        var locks = new List<string>();
        var prices = new List<int>();
        _tradeStep = 0;
        foreach (var i in items)
        {
            string why = i.Card.Id == "shop_token" ? null : i.Card.Id == "shop_potion" ? (p.Potions >= p.MaxPotions ? "YOUR BELT IS FULL" : null) : i.Supplies ? (p.Hp >= p.Stats.MaxHp - 0.5f ? "YOU ARE FULL ALREADY" : null) : Upgrades.LockReason(i.Card, p.Stats);
            if (why == null && i.Price > p.Gold) why = $"NEEDS {i.Price - p.Gold} MORE GOLD";
            locks.Add(why);
            prices.Add(i.Price);
        }
        // the trading stand: a relic for a relic, a few times a run
        int tradeable = TradeableRelics(p).Count;
        TradeCard.Desc = Tune.Merchant.NpcTrades
            ? $"Give him a relic you carry for one of {Tune.Merchant.TradeOffer} of his ({p.Trades} trade{(p.Trades == 1 ? "" : "s")} left this run; the Ember Grid gives more)."
            : $"Hand a relic you carry to a friend who wants it more ({p.Trades} trade{(p.Trades == 1 ? "" : "s")} left this run; the Ember Grid gives more).";
        locks.Add(p.Trades <= 0 ? "NO TRADES LEFT THIS RUN" : tradeable == 0 ? "NO RELIC TO TRADE" : !Tune.Merchant.NpcTrades && TradeFriends().Count == 0 ? "NO FRIEND TO TRADE WITH" : null); prices.Add(0);
        int rp = RerollPrice(m);
        RerollCard.Desc = FreeDealLeft(m) ? "He shuffles the cards he has not sold and lays out new ones: this one is on the house."
            : rp == 0 ? $"He shuffles the cards he has not sold and lays out new ones, for one of your Second Thoughts ({p.Rerolls} left)."
            : "He shuffles the cards he has not sold and lays out new ones. Each time costs more.";
        locks.Add(rp > p.Gold ? $"NEEDS {rp - p.Gold} MORE GOLD" : null); prices.Add(rp);
        locks.Add(null); prices.Add(0);
        _state = State.Choosing;
        if (Net.InRun) p.Choosing = true;
        else GetTree().Paused = true;
        _sfx.Play("card_reveal", null, -4, 0, 0.9f);
        _upgradeMenu.Open(choices, $"THE MERCHANT  ·  {p.Gold} GOLD", locks, UiArt.Surface.Velvet, UiArt.Rarity.Rare, prices);
        _upgradeMenu.Reroll = null;
        _autoPickT = 0.5f;
    }

    /// <summary>The stand, first: which of the hero's relics to give.</summary>
    private void ShowTradeGive()
    {
        var p = G.Player;
        var mine = TradeableRelics(p);
        if (mine.Count == 0 || p.Trades <= 0) { ShowShop(); return; }
        _tradeStep = 1;
        var choices = mine.Append(TradeBack).ToList();
        _sfx.Play("relic_reveal", null, -6, 0, 1.1f);
        _upgradeMenu.Open(choices, "THE TRADING STAND  ·  WHICH RELIC WILL YOU GIVE?", choices.Select(_ => (string)null).ToList(), UiArt.Surface.Velvet, UiArt.Rarity.Relic);
        _upgradeMenu.Reroll = null;
        _autoPickT = 0.5f;
    }

    /// <summary>The friends a relic could go to (online, everyone else in the party).</summary>
    private static List<Net.PeerInfo> TradeFriends() => Net.Online ? Net.Peers.Values.Where(q => q.Id != Net.Me).ToList() : new List<Net.PeerInfo>();

    /// <summary>A card for a friend at the stand (its id carries who).</summary>
    private static Upgrade FriendCard(Net.PeerInfo q, Upgrade relic) => new()
    {
        Id = $"trade_to_{q.Id}", Name = Net.NameOf(q.Id), Icon = "relic", MaxStacks = 1, Apply = (s, p) => { },
        Desc = $"Give your {relic.Name} to them ({q.Hero}). If they carry it already, it comes back to you and the trade isn't spent.",
    };

    /// <summary>Player trading, then: to which friend.</summary>
    private void ShowTradeFriend()
    {
        var relic = Upgrades.Find(_tradeGive);
        var friends = TradeFriends();
        if (relic == null || friends.Count == 0) { ShowShop(); return; }
        _tradeStep = 3;
        var choices = friends.Select(q => FriendCard(q, relic)).Append(TradeBack).ToList();
        // (a hero's own relic can only go to a friend of that hero)
        var locks = friends.Select(q => relic.For != null && !relic.For.Contains(q.Hero) ? $"ONLY A {relic.For[0].ToString().ToUpperInvariant()} CAN CARRY IT" : null).Append(null).ToList();
        _sfx.Play("relic_reveal", null, -4, 0, 0.95f);
        _upgradeMenu.Open(choices, $"THE TRADING STAND  ·  WHO GETS YOUR {relic.Name.ToUpperInvariant()}?", locks, UiArt.Surface.Velvet, UiArt.Rarity.Relic);
        _upgradeMenu.Reroll = null;
        _autoPickT = 0.5f;
    }

    /// <summary>A relic of this hero's goes to a friend: off this hero now, on theirs when their game hears of it (back here if they can't carry it).</summary>
    public bool GiveRelicTo(int friend, string id)
    {
        var p = G.Player;
        if (p == null || p.Trades <= 0 || !Upgrades.RemoveRelic(id, p.Stats, p)) return false;
        p.Trades--;
        Trades++;
        NetSync.SendRelicGift(friend, id);
        Achievements.Traded();
        return true;
    }

    /// <summary>A friend gave this hero a relic at the stand: carried at once, or handed back (with why) if it can't be.</summary>
    public bool ReceiveRelicGift(int giver, string id)
    {
        var p = G.Player; var u = Upgrades.Find(id);
        string why = p == null || p.Dead ? "THEY HAVE FALLEN" : u == null || !u.Relic ? "NO SUCH RELIC" : p.Stats.StackOf(id) > 0 ? "THEY CARRY IT ALREADY" : !Upgrades.Available(u, p.Stats) ? "THEY CAN'T CARRY IT" : null;
        if (why != null) { NetSync.SendRelicGift(giver, id, why); return false; }
        Upgrades.Apply(u, p.Stats, p);
        GiftsReceived++;
        Achievements.Traded();
        _hud.ShowBanner($"{Net.NameOf(giver)} GAVE YOU {u.Name.ToUpperInvariant()}", 2f);
        _sfx.Play("card_take", null, 0, 0, 0.8f);
        return true;
    }

    /// <summary>A gift couldn't be carried: it is this hero's again, and the trade isn't spent.</summary>
    public void RelicGiftReturned(int friend, string id, string why)
    {
        var p = G.Player; var u = Upgrades.Find(id);
        if (p == null || u == null) return;
        if (p.Stats.StackOf(id) == 0) Upgrades.Apply(u, p.Stats, p);
        p.Trades++;
        _hud.ShowBanner($"{u.Name.ToUpperInvariant()} CAME BACK: {why}", 2f);
    }

    /// <summary>For the tests: relics given to this hero at the stand.</summary>
    public int GiftsReceived { get; private set; }

    /// <summary>Then: which of his to take for it (relics for this hero, none already carried).</summary>
    private void ShowTradeGet()
    {
        var p = G.Player;
        _tradeStep = 2;
        var not = RunRelics.Mine.Append(_tradeGive).ToList();
        // (nothing that changes the world: what he hands over can be traded on again)
        _tradeOffer = Upgrades.RelicPool(p.Stats, not).Where(u => Upgrades.TradeableKind(u.Id)).OrderBy(_ => _rng.Next()).Take(Tune.Merchant.TradeOffer).ToList();
        if (_tradeOffer.Count == 0) { _hud.ShowBanner("HE HAS NOTHING TO OFFER", 1.4f); ShowShop(); return; }
        var give = Upgrades.Find(_tradeGive);
        var choices = _tradeOffer.Append(TradeBack).ToList();
        _sfx.Play("relic_reveal", null, -4, 0, 0.95f);
        _upgradeMenu.Open(choices, $"THE TRADING STAND  ·  FOR YOUR {give?.Name.ToUpperInvariant()}, TAKE ONE", choices.Select(_ => (string)null).ToList(), UiArt.Surface.Velvet, UiArt.Rarity.Relic);
        _upgradeMenu.Reroll = null;
        _autoPickT = 0.5f;
    }

    /// <summary>The stand's choices: a relic to give, then one to take (or back to his table).</summary>
    private bool TradePicked(Upgrade u)
    {
        var p = G.Player;
        if (_tradeStep == 1)
        {
            if (u.Id == TradeBack.Id) { ShowShop(); return true; }
            _tradeGive = u.Id;
            if (Tune.Merchant.NpcTrades) ShowTradeGet(); else ShowTradeFriend();
            return true;
        }
        if (_tradeStep == 3)
        {
            if (u.Id == TradeBack.Id) { ShowTradeGive(); return true; }
            var give = Upgrades.Find(_tradeGive);
            if (give != null && u.Id.StartsWith("trade_to_") && int.TryParse(u.Id["trade_to_".Length..], out int friend) && GiveRelicTo(friend, _tradeGive))
            {
                _hud.ShowBanner($"YOUR {give.Name.ToUpperInvariant()} GOES TO {Net.NameOf(friend)}", 1.8f);
                _sfx.Play("card_take", null, 0, 0, 0.8f);
                G.Fx.Text(_shop.GlobalPosition + new Vector2(0, -52), "a gift between friends", new Color(1f, 0.82f, 0.4f), 11, 1.2f);
            }
            ShowShop();
            return true;
        }
        if (_tradeStep == 2)
        {
            if (u.Id == TradeBack.Id) { ShowTradeGive(); return true; }
            var give = Upgrades.Find(_tradeGive);
            if (_tradeOffer == null || !_tradeOffer.Contains(u) || give == null || !Upgrades.RemoveRelic(_tradeGive, p.Stats, p)) { ShowShop(); return true; }
            Upgrades.Apply(u, p.Stats, p);
            p.Trades--;
            Trades++;
            _hud.ShowBanner($"{give.Name} FOR {u.Name}", 1.8f);
            _sfx.Play("card_take", null, 0, 0, 0.8f);
            G.Fx.Text(_shop.GlobalPosition + new Vector2(0, -52), "a fair trade", new Color(1f, 0.82f, 0.4f), 11, 1.2f);
            ShowShop();
            return true;
        }
        return false;
    }

    /// <summary>For the tests: relics traded at the stand this session.</summary>
    public int Trades { get; private set; }

    private void CloseShop()
    {
        _shop = null;
        _tradeStep = 0;
        if (G.Player != null) G.Player.Choosing = false;
        GetTree().Paused = false;
        _state = State.Playing;
    }

    /// <summary>A card of the stall was taken (or the table left).</summary>
    private void ShopPicked(Upgrade u)
    {
        var p = G.Player;
        var m = _shop;
        if (m != null && TradePicked(u)) return;
        if (u.Icon == "skip" || m?.Stock == null) { _sfx.Play("card_leave", null, -2); CloseShop(); return; }
        if (u.Id == "shop_reroll") { Reroll(m); return; }
        if (u.Id == TradeCard.Id) { ShowTradeGive(); return; }
        var item = m.Stock.FirstOrDefault(i => !i.Sold && i.Card.Id == u.Id);
        if (item == null || item.Price > p.Gold) { ShowShop(); return; }
        p.Gold -= item.Price;
        p.GoldSpent += item.Price;
        item.Sold = true;
        if (item.Card.Id == "shop_token")
        {
            p.Rerolls++;
            _hud.ShowBanner("SECOND THOUGHTS", 1.4f);
        }
        else if (item.Card.Id == "shop_potion")
        {
            p.Potions = Math.Min(p.MaxPotions, p.Potions + 1);
            _hud.ShowBanner("A POTION", 1.4f);
        }
        else if (item.Supplies)
        {
            p.Heal(p.Stats.MaxHp * Tune.Merchant.SuppliesHeal);
            _hud.ShowBanner("PROVISIONS", 1.4f);
        }
        else
        {
            Upgrades.Apply(u, p.Stats, p);
            _hud.ShowBanner(u.Name, 1.6f);
        }
        _sfx.Play("card_take", null, 0, 0, u.Relic ? 0.8f : 1f);
        _sfx.Play("clink", null, -2, 0.05f, 1.3f);
        m.NoteSold();
        G.Fx.Text(m.GlobalPosition + new Vector2(0, -52), $"-{item.Price} gold", new Color(1f, 0.82f, 0.4f), 11, 1.2f);
        ShowShop();
    }
}
