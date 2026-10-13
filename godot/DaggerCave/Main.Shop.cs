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
    public static readonly Upgrade LeaveShop = new() { Id = "leave_shop", Name = "Walk Away", Desc = "Close the stall. His wares stay as they are until you go down.", Icon = "skip", MaxStacks = 9999, Apply = (s, p) => { } };
    /// <summary>His supplies, a stock item that is no upgrade: a full belt of potions and a bandage.</summary>
    public static readonly Upgrade Supplies = new()
    {
        Id = "shop_supplies", Name = "Provisions", Icon = "life", MaxStacks = 9999, Apply = (s, p) => { },
        Desc = $"Fills your belt of potions, and mends {Tune.Merchant.SuppliesHeal:0%} of your health.",
    };

    /// <summary>What a card costs here: by its rarity, a little more with every level down.</summary>
    public static int PriceOf(Upgrade u)
    {
        int basePrice = u.Id == "shop_supplies" ? Tune.Merchant.PriceSupplies : UiArt.RarityOf(u) switch
        {
            UiArt.Rarity.Relic => Tune.Merchant.PriceRelic,
            UiArt.Rarity.Rare => Tune.Merchant.PriceRare,
            UiArt.Rarity.Uncommon => Tune.Merchant.PriceUncommon,
            _ => Tune.Merchant.PriceCommon,
        };
        return Math.Max(1, (int)MathF.Round(basePrice * (1f + Tune.Merchant.PerDepth * Math.Max(0, G.Depth))));
    }

    /// <summary>His wares for this hero: a class card and two others (as a chest deals), a relic, and his supplies.</summary>
    private List<ShopItem> DealStock(Merchant m)
    {
        var p = G.Player;
        var items = new List<ShopItem>();
        foreach (var c in Upgrades.RollChest(p.Stats, _rng, m.GlobalPosition)) items.Add(new ShopItem { Card = c, Price = PriceOf(c) });
        if (Upgrades.RollRelic(p.Stats, _rng, items.Select(i => i.Card.Id)) is Upgrade relic) items.Add(new ShopItem { Card = relic, Price = PriceOf(relic) });
        items.Add(new ShopItem { Card = Supplies, Price = PriceOf(Supplies) });
        return items;
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
        var choices = items.Select(i => i.Card).Append(LeaveShop).ToList();
        var locks = new List<string>();
        var prices = new List<int>();
        foreach (var i in items)
        {
            string why = i.Supplies ? (p.Potions >= p.MaxPotions && p.Hp >= p.Stats.MaxHp - 0.5f ? "YOU ARE FULL ALREADY" : null) : Upgrades.LockReason(i.Card, p.Stats);
            if (why == null && i.Price > p.Gold) why = $"NEEDS {i.Price - p.Gold} MORE GOLD";
            locks.Add(why);
            prices.Add(i.Price);
        }
        locks.Add(null); prices.Add(0);
        _state = State.Choosing;
        if (Net.InRun) p.Choosing = true;
        else GetTree().Paused = true;
        _sfx.Play("card_reveal", null, -4, 0, 0.9f);
        _upgradeMenu.Open(choices, $"THE MERCHANT  ·  {p.Gold} GOLD", locks, UiArt.Surface.Velvet, UiArt.Rarity.Rare, prices);
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
        var item = m.Stock.FirstOrDefault(i => !i.Sold && i.Card.Id == u.Id);
        if (item == null || item.Price > p.Gold) { ShowShop(); return; }
        p.Gold -= item.Price;
        p.GoldSpent += item.Price;
        item.Sold = true;
        if (item.Supplies)
        {
            p.Potions = Math.Max(p.Potions, p.MaxPotions);
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
