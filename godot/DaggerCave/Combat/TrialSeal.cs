using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The wards that shut a trial room (the Gauntlet): a block of violet light over every open cell on the room's rim, so nothing walks, flies or
/// shoots in or out until the trial is won. They rise when it begins and fade when it ends. Every game builds them from the same list of cells.
/// </summary>
public partial class TrialSeal : StaticBody2D
{
    public static readonly List<TrialSeal> All = new();
    public int RoomIndex;
    public readonly List<Vector2I> Cells = new();
    private float _t, _fade = -1f;
    public bool Opening => _fade >= 0f;
    /// <summary>How far up the wards stand: rising over a quarter second, fading over the last.</summary>
    public float Shown => Math.Min(1f, _t / 0.25f) * (Opening ? Math.Max(0f, 1f - _fade / 0.6f) : 1f);

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        ZIndex = 4;
        foreach (var c in Cells)
            AddChild(new CollisionShape2D { Position = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell, Shape = new RectangleShape2D { Size = new Vector2(CaveData.Cell, CaveData.Cell) } });
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    /// <summary>The trial is won: the wards fade and let everyone through.</summary>
    public void Open()
    {
        if (Opening) return;
        _fade = 0f;
        foreach (var n in GetChildren()) if (n is CollisionShape2D s) s.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        foreach (var c in Cells)
        {
            var at = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell;
            if (G.Chance(0.4f)) G.Fx.Burst(at, new Color(0.7f, 0.45f, 1f, 0.8f), 3, 60, 1.4f, 0.4f, -30f);
        }
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.1f, 0.6f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (Opening && (_fade += dt) > 0.6f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // (rising over the first quarter second, fading over the last)
        float rise = Math.Min(1f, _t / 0.25f), a = rise * (Opening ? 1f - _fade / 0.6f : 1f);
        float s = CaveData.Cell;
        foreach (var c in Cells)
        {
            var at = new Vector2(c.X, c.Y) * s;
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 4f + (c.X + c.Y) * 0.7f);
            DrawRect(new Rect2(at, new Vector2(s, s)), new Color(0.35f, 0.15f, 0.6f, (0.35f + 0.15f * pulse) * a));
            DrawRect(new Rect2(at + new Vector2(2, 2), new Vector2(s - 4, s - 4)), new Color(0.8f, 0.55f, 1f, (0.35f + 0.35f * pulse) * a), false, 1.5f);
            // (a rune at its heart)
            var mid = at + new Vector2(s, s) * 0.5f;
            DrawLine(mid + new Vector2(-3, -3), mid + new Vector2(3, 3), new Color(0.95f, 0.85f, 1f, 0.6f * a * pulse), 1.2f);
            DrawLine(mid + new Vector2(3, -3), mid + new Vector2(-3, 3), new Color(0.95f, 0.85f, 1f, 0.6f * a * pulse), 1.2f);
        }
    }
}
