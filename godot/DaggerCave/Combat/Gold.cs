using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Gold lying in the cave: a coin (worth 1) or a small pile of coins (4 to 8). Hidden in the dead ends, where there was nothing; picked up by
/// touching it, and the gold is the hero's to spend on a merchant's wares. Online, the host's game decides who gets it, as with a key.
/// </summary>
public partial class CoinPickup : Node2D
{
    /// <summary>Online: a copy of the host's (the host decides who gets it).</summary>
    public bool Puppet;
    /// <summary>How many gold pieces it is worth: 1 for a coin, 4 to 8 for a pile.</summary>
    public int Count = 1;
    public bool IsPile => Count > 1;
    private float _t;
    public float T => _t;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        if (G.Chance(IsPile ? 0.03f : 0.012f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-5, 5), -G.Range(0, 7)), new Color(1f, 0.85f, 0.35f), 4);
        foreach (var p in G.Players)
        {
            if (Puppet || p.Dead || p.GlobalPosition.DistanceTo(GlobalPosition + new Vector2(0, -4)) >= 16 + (p.Stats?.PickupReach ?? 0f)) continue;
            NetSync.Scope++;
            try
            {
                if (p.IsRemote) NetSync.GivePickup(p, 5, Count);
                else p.GainGold(Count);
                G.Fx.Pop(GlobalPosition, new Color(1f, 0.82f, 0.3f), IsPile ? 10 : 5);
            }
            finally { NetSync.Scope--; }
            NetSync.PropGone(this, quiet: true);
            QueueFree();
            return;
        }
    }

    /// <summary>The gold coin icon (also the HUD's).</summary>
    public static void DrawCoin(CanvasItem c, Vector2 at, float scale, float alpha = 1f)
    {
        c.DrawSetTransform(at, 0, new Vector2(scale, scale));
        c.DrawCircle(Vector2.Zero, 7f, new Color(1f, 0.85f, 0.3f, 0.14f * alpha));
        c.DrawCircle(Vector2.Zero, 4.6f, new Color(0.72f, 0.5f, 0.1f, alpha));
        c.DrawCircle(Vector2.Zero, 3.8f, new Color(1f, 0.82f, 0.28f, alpha));
        c.DrawArc(Vector2.Zero, 2.4f, 0, Mathf.Tau, 12, new Color(0.78f, 0.55f, 0.12f, alpha), 0.9f);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    public override void _Draw() => DrawCoin(this, new Vector2(0, -4), IsPile ? 1.6f : 1f);
}
