using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A rope a hero lowers for the party (the only help that needs a party). It hangs from where it
/// was cast down to the ground or the length it reaches; any hero beside it who pushes up takes
/// hold and climbs, and jump lets go. Every game keeps a copy, and each copy carries only that
/// game's own hero.
/// </summary>
public partial class Rope : Node2D
{
    public static readonly List<Rope> All = new();

    public float Length = Tune.Rope.Length, Life = Tune.Rope.Seconds;
    public float Age => _t;
    public float TotalLife { get; private set; }
    private float _t;

    /// <summary>How much of it is there (it unrolls when cast and frays away at the end).</summary>
    public float Strength => Math.Clamp(Life / 0.8f, 0f, 1f);
    /// <summary>The length unrolled so far.</summary>
    public float Unrolled => Length * Math.Clamp(_t / 0.35f, 0f, 1f);

    public override void _Ready()
    {
        TotalLife = Life;
        // it stops where the ground (or the rock above the ground) is
        float h = 0;
        while (h < Length && !G.Cave.IsSolid(GlobalPosition + new Vector2(0, h + 4f))) h += 4f;
        Length = Math.Max(16f, h);
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Whether a hero whose middle is at <paramref name="p"/> is within reach of it.</summary>
    public bool Holds(Vector2 p)
    {
        if (Life <= 0) return false;
        return Math.Abs(p.X - GlobalPosition.X) < Tune.Rope.GrabWidth && p.Y > GlobalPosition.Y - 6f && p.Y < GlobalPosition.Y + Unrolled + 8f;
    }

    public static Rope At(Vector2 p)
    {
        Rope best = null;
        foreach (var r in All)
            if (IsInstanceValid(r) && r.Holds(p) && (best == null || Math.Abs(r.GlobalPosition.X - p.X) < Math.Abs(best.GlobalPosition.X - p.X))) best = r;
        return best;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Life -= dt;
        if (Life <= 0) QueueFree();
    }
}
