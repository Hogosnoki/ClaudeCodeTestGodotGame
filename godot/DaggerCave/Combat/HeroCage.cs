using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A hero shut in an iron cage somewhere in the cave: free them (the interact button) and they
/// are unlocked for the camp from now on. Which hero is whichever the player hasn't got yet.
/// </summary>
public partial class HeroCage : Node2D
{
    public HeroKind Hero;
    public bool Freed { get; private set; }
    public float FreedT { get; private set; }

    public static readonly System.Collections.Generic.List<HeroCage> All = new();
    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    public bool Reaches(Vector2 p) => !Freed && p.DistanceTo(GlobalPosition + new Vector2(0, -10)) < 34;

    public static HeroCage At(Vector2 p)
    {
        foreach (var c in All) if (GodotObject.IsInstanceValid(c) && c.Reaches(p)) return c;
        return null;
    }

    /// <summary>The hero pressed interact at the cage.</summary>
    public void Interact()
    {
        if (Freed) return;
        Freed = true;
        bool fresh = Meta.Unlock(Hero);
        G.Sfx.Play("chest", GlobalPosition, -2, 0, 0.8f);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -12), Hud.HeroColor(Hero), 30, 200, 2.6f, 0.7f);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -12), 30, Hud.HeroColor(Hero));
        G.Main.Hud?.ShowBanner(fresh ? $"THE {Hero.ToString().ToUpperInvariant()} IS FREE  ·  waiting at the camp" : $"THE {Hero.ToString().ToUpperInvariant()} THANKS YOU", 4f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Freed) FreedT += (float)delta;
    }
}
