using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A power cell (the Clockwork Deep): a brass-capped glass cylinder with a charge crackling in it. It lies hidden in a level whose
/// secret wing holds the Automaton, disabled in a heap; it is the only thing that can wake him. Whoever finds it finds it for the
/// whole party (online, the others' copies go too), and the heap can then be revived.
/// </summary>
public partial class PowerCell : Node2D
{
    /// <summary>The party has the cell this level.</summary>
    public static bool Found;
    private float _t;
    public float T => _t;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (G.Chance(0.02f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-4, 4), -G.Range(2, 12)), new Color(0.6f, 0.85f, 1f), 5);
        foreach (var p in G.Players)
        {
            if (p.Dead || p.IsRemote || p.GlobalPosition.DistanceTo(GlobalPosition + new Vector2(0, -6)) >= 20) continue;
            Take(true);
            return;
        }
    }

    /// <summary>The cell is found (here, by this game's hero, or in a friend's game).</summary>
    public void Take(bool local)
    {
        if (!IsInstanceValid(this) || IsQueuedForDeletion()) return;
        Found = true;
        if (local) NetSync.CellFound();
        G.Fx.Pop(GlobalPosition + new Vector2(0, -6), new Color(0.6f, 0.85f, 1f), 10);
        G.Fx.Glint(GlobalPosition + new Vector2(0, -8), new Color(0.85f, 0.95f, 1f), 10);
        G.Sfx.Play("levelup", GlobalPosition, -6, 0.05f, 1.4f);
        G.Main.Hud?.ShowBanner("A POWER CELL", 2.4f);
        G.Main.Hud?.ShowNotice("It still holds a charge. Something in these works could use it.", 4f);
        QueueFree();
    }
}
