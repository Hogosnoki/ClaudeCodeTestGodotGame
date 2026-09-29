using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's Updraft: a column of rising air standing on the ground where it was cast.
/// Any hero inside it (the caster or a friend) is carried up to its top and held there, where a
/// jump takes them on; pushing down lets them sink out of it. It stops at a ceiling. Every game
/// keeps a copy, and each copy lifts only that game's own hero, so every hero rides every column.
/// </summary>
public partial class Updraft : Node2D
{
    /// <summary>Every column standing now (a hero asks which, if any, holds them).</summary>
    public static readonly List<Updraft> All = new();

    public float Width = Tune.Elementalist.UpdraftWidth, Height = Tune.Elementalist.UpdraftHeight, Life = Tune.Elementalist.UpdraftSeconds;
    public float TotalLife { get; private set; }
    public float Age => _t;
    private float _t, _fxT;

    /// <summary>The column's top (world y): where a hero rides up to.</summary>
    public float TopY => GlobalPosition.Y - Height;
    /// <summary>0..1: how much of it is left (it fades at the end).</summary>
    public float Strength => Math.Clamp(Life / 0.6f, 0f, 1f) * Math.Clamp(_t / 0.25f, 0f, 1f);

    public override void _Ready()
    {
        ZIndex = -1;
        TotalLife = Life;
        // a ceiling cuts it short
        float h = 0;
        while (h < Height && !G.Cave.IsSolid(GlobalPosition - new Vector2(0, h + 18f))) h += 4f;
        Height = Math.Max(24f, h);
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Whether a hero standing at <paramref name="p"/> (their middle) is inside it.</summary>
    public bool Holds(Vector2 p)
        => Life > 0 && Math.Abs(p.X - GlobalPosition.X) < Width * 0.5f && p.Y <= GlobalPosition.Y + 6f && p.Y >= TopY - 10f;

    /// <summary>The column holding a hero at <paramref name="p"/>, if any (the tallest top wins).</summary>
    public static Updraft At(Vector2 p)
    {
        Updraft best = null;
        foreach (var u in All)
            if (IsInstanceValid(u) && u.Holds(p) && (best == null || u.TopY < best.TopY)) best = u;
        return best;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Life -= dt;
        if (Life <= 0) { QueueFree(); return; }
        // wisps of air racing up it, and a stir of dust at its foot
        _fxT -= dt;
        if (_fxT <= 0)
        {
            _fxT = 0.035f;
            float s = Strength;
            var at = GlobalPosition + new Vector2(G.Range(-0.45f, 0.45f) * Width, -G.Range(0f, Height));
            G.Fx.Directional(at, Vector2.Up, 0.1f, new Color(0.82f, 0.95f, 1f, 0.55f * s), 1, 220, 1.3f, 0.3f, 0, 1);
            if (G.Chance(0.3f)) G.Fx.Dust(GlobalPosition + new Vector2(G.Range(-0.5f, 0.5f) * Width, 0), 1, 0.8f, new Color(0.85f, 0.9f, 0.95f, 0.5f * s));
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay)
        float a = 0.18f * Strength;
        DrawRect(new Rect2(-Width * 0.5f, -Height, Width, Height), new Color(0.75f, 0.92f, 1f, a));
        DrawLine(new Vector2(-Width * 0.5f, 0), new Vector2(-Width * 0.5f, -Height), new Color(0.85f, 0.95f, 1f, a * 2f), 1f);
        DrawLine(new Vector2(Width * 0.5f, 0), new Vector2(Width * 0.5f, -Height), new Color(0.85f, 0.95f, 1f, a * 2f), 1f);
    }
}
