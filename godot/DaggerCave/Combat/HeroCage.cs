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
    /// <summary>Not a cage: the Automaton, disabled in a heap on the floor (the Clockwork Deep). Only a power cell wakes him.</summary>
    public bool NeedsCell;
    private float _sayT;
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
        if (NeedsCell && !PowerCell.Found)
        {
            if (_sayT > 0) return;
            _sayT = 1.5f;
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "IT NEEDS A POWER CELL", new Color(0.6f, 0.85f, 1f), 10, 1.4f);
            G.Sfx.Play("clink", GlobalPosition, -10, 0.05f, 0.5f);
            return;
        }
        Free(local: true);
    }

    /// <summary>Opens the cage: the hero joins everyone's roster (a friend freeing them frees them here too).</summary>
    public void Free(bool local)
    {
        if (Freed && !local) return;
        Freed = true;
        bool fresh = Meta.Unlock(Hero);
        Net.EveryoneUnlocked(Hero);
        if (local) NetSync.CageFreed(Hero);
        G.Sfx.Play("chest", GlobalPosition, -2, 0, 0.8f);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -12), Hud.HeroColor(Hero), 30, 200, 2.6f, 0.7f);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -12), 30, Hud.HeroColor(Hero));
        G.Main.Hud?.ShowBanner(Lore.FreedBanner(Hero, fresh), 5.5f);
        if (fresh) G.Main.Hud?.ShowNotice(Lore.Hero(Hero).backstory, 8f);
    }

    public override void _PhysicsProcess(double delta)
    {
        _sayT -= (float)delta;
        if (Freed) FreedT += (float)delta;
        // (the heap: a spark jumps from its joints now and then, the last of a charge)
        else if (NeedsCell && G.Chance((float)delta * 0.6f)) G.Fx.Spark(GlobalPosition + new Vector2(G.Range(-12, 12), G.Range(-10, 0)), G.RandDir(), false, new Color(0.7f, 0.85f, 1f));
    }
}
