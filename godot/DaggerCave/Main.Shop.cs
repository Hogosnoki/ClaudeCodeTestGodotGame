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
        return Math.Max(1, (int)MathF.Round(basePrice * (1f + Tune.Merchant.PerDepth * Math.Max(0, G.Depth)) * (st?.ShopPriceMult ?? 1f) * Meta.ShopPriceMult));
    }

    /// <summary>What dealing his wares again costs the hero now (0: free).</summary>
    public static int RerollPrice(Merchant m)
    {
        var st = G.Player?.Stats;
        if (st != null && m.FreeRerollsUsed < st.FreeRerolls) return 0;
        float price = Tune.Merchant.PriceReroll * (1f + Tune.Merchant.PerDepth * Math.Max(0, G.Depth)) * (1f + Tune.Merchant.RerollStep * m.Rerolls) * (st?.ShopPriceMult ?? 1f) * Meta.ShopPriceMult * (st?.RerollMult ?? 1f);
        return Math.Max(1, (int)MathF.Round(price));
    }

    /// <summary>His cards for this hero: a class card and two others (as a chest deals), a relic, and one more of the others for each thing a relic adds to his table.</summary>
    private List<ShopItem> DealCards(Merchant m)
    {
        var p = G.Player;
        var items = new List<ShopItem>();
        foreach (var c in Upgrades.RollChest(p.Stats, _rng, m.GlobalPosition).Take(Tune.Merchant.Cards)) items.Add(new ShopItem { Card = c, Price = PriceOf(c) });
        if (Upgrades.RollRelic(p.Stats, _rng, items.Select(i => i.Card.Id)) is Upgrade relic) items.Add(new ShopItem { Card = relic, Price = PriceOf(relic) });
        for (int k = 0; k < p.Stats.ShopExtra; k++)
            for (int tries = 0, got = 0; tries < 8 && got == 0; tries++)
                foreach (var c in Upgrades.RollChest(p.Stats, _rng, m.GlobalPosition))
                    if (items.All(i => i.Card.Id != c.Id)) { items.Add(new ShopItem { Card = c, Price = PriceOf(c) }); got++; break; }
        return items;
    }

    /// <summary>His wares for this hero: the cards, and two of his supplies (a potion, provisions, or, often, a token of Second Thoughts: what he has is a matter of luck).</summary>
    private List<ShopItem> DealStock(Merchant m)
    {
        var items = DealCards(m);
        var pool = new List<(Upgrade card, float weight)> { (Potion, 1f), (Supplies, 1f), (Token, Tune.Merchant.TokenWeight) };
        for (int k = 0; k < Tune.Merchant.Supplies && pool.Count > 0; k++)
        {
            double r = _rng.NextDouble() * pool.Sum(x => x.weight);
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
        if (price == 0) m.FreeRerollsUsed++;
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
        var choices = items.Select(i => i.Card).Append(RerollCard).Append(LeaveShop).ToList();
        var locks = new List<string>();
        var prices = new List<int>();
        foreach (var i in items)
        {
            string why = i.Card.Id == "shop_token" ? null : i.Card.Id == "shop_potion" ? (p.Potions >= p.MaxPotions ? "YOUR BELT IS FULL" : null) : i.Supplies ? (p.Hp >= p.Stats.MaxHp - 0.5f ? "YOU ARE FULL ALREADY" : null) : Upgrades.LockReason(i.Card, p.Stats);
            if (why == null && i.Price > p.Gold) why = $"NEEDS {i.Price - p.Gold} MORE GOLD";
            locks.Add(why);
            prices.Add(i.Price);
        }
        int rp = RerollPrice(m);
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

    private void CloseShop()
    {
        _shop = null;
        if (G.Player != null) G.Player.Choosing = false;
        GetTree().Paused = false;
        _state = State.Playing;
    }

    /// <summary>A card of the stall was taken (or the table left).</summary>
    private void ShopPicked(Upgrade u)
    {
        var p = G.Player;
        var m = _shop;
        if (u.Icon == "skip" || m?.Stock == null) { _sfx.Play("card_leave", null, -2); CloseShop(); return; }
        if (u.Id == "shop_reroll") { Reroll(m); return; }
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
