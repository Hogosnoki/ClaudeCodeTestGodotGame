using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Rogue's Smoke Bomb: a cloud of thick grey smoke where it burst. Every hero inside it is
/// hidden (creatures lose them), and a creature inside it can't find anyone at all. Every game
/// keeps a copy (the host's creatures go by the host's).
/// </summary>
public partial class SmokeCloud : Node2D
{
    public static readonly List<SmokeCloud> All = new();

    public float Radius = Tune.Rogue.SmokeRadius, Life = Tune.Rogue.SmokeSeconds;
    public float TotalLife { get; private set; }
    public float Age => _t;
    private float _t, _fxT;

    /// <summary>0..1: how thick it is (it billows up, and thins at the end).</summary>
    public float Thickness => Math.Clamp(_t / 0.3f, 0f, 1f) * Math.Clamp(Life / 0.8f, 0f, 1f);

    public override void _Ready()
    {
        ZIndex = 3;
        TotalLife = Life;
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Whether <paramref name="p"/> is inside a cloud of smoke.</summary>
    public static bool Covers(Vector2 p)
    {
        foreach (var c in All)
            if (IsInstanceValid(c) && c.Life > 0.3f && c.GlobalPosition.DistanceTo(p) < c.Radius) return true;
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        Life -= dt;
        if (Life <= 0) { QueueFree(); return; }
        _fxT -= dt;
        if (_fxT <= 0)
        {
            _fxT = 0.12f;
            G.Fx.Smoke(GlobalPosition + G.RandDir() * G.Range(0f, Radius * 0.8f), 1, new Color(0.3f, 0.3f, 0.33f, 0.5f * Thickness), 20f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay)
        DrawCircle(Vector2.Zero, Radius, new Color(0.35f, 0.35f, 0.38f, 0.35f * Thickness));
    }
}
