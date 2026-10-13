using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>One thing the merchant has for sale to this hero: a card (or his supplies) at a price in gold.</summary>
public sealed class ShopItem
{
    public Upgrade Card;
    public int Price;
    public bool Sold;
    /// <summary>His supplies (a belt of potions and a bandage) rather than an upgrade.</summary>
    public bool Supplies => Card.Id == "shop_supplies";
}

/// <summary>
/// The merchant, at his stall in the chamber where the way on opens (see <see cref="Main.PlaceMerchant"/>): stand at him and press interact to look at his wares,
/// paid for in gold (<see cref="Player.Gold"/>). What he has is dealt for the hero who first looks (see <c>Main.Shop.cs</c>) and stays the same until they leave
/// the level: what is sold is gone.
/// </summary>
public partial class Merchant : Node2D
{
    public static readonly List<Merchant> All = new();
    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Which way he faces (toward where the heroes arrive).</summary>
    public float Facing = -1f;
    /// <summary>His wares for this game's hero (null until they first look).</summary>
    public List<ShopItem> Stock;
    /// <summary>Seconds since he was put up.</summary>
    public float T { get; private set; }
    /// <summary>He has just sold something (for his pleased look): seconds since.</summary>
    public float SoldT { get; private set; } = 99f;
    private float _near;
    public float Near => _near;

    public bool Reaches(Vector2 p) => p.DistanceTo(GlobalPosition + new Vector2(0, -12)) < 46f;

    public static Merchant At(Vector2 p)
    {
        foreach (var m in All) if (GodotObject.IsInstanceValid(m) && m.Reaches(p)) return m;
        return null;
    }

    public void NoteSold() => SoldT = 0f;

    public void Interact() => G.Main.OpenShop(this);

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        T += dt; SoldT += dt;
        bool near = false;
        foreach (var p in G.Players) if (p != null && !p.Dead && Reaches(p.GlobalPosition)) near = true;
        _near = Math.Clamp(_near + (near ? dt * 5f : -dt * 3f), 0f, 1f);
        // he faces whoever is near him, or back toward the way in
        Player best = null; float bd = 220f;
        foreach (var p in G.Players) if (p != null && !p.Dead) { float d = Math.Abs(p.GlobalPosition.X - GlobalPosition.X); if (d < bd) { bd = d; best = p; } }
        if (best != null && Math.Abs(best.GlobalPosition.X - GlobalPosition.X) > 8f) Facing = Math.Sign(best.GlobalPosition.X - GlobalPosition.X);
    }
}
