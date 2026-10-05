using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Stalag-Might's grip: a bulge of rock that bursts up out of the ground round a creature's feet and holds it; once the hold
/// is over (or the creature is freed or dead) it sinks back into the ground. Placed at the floor under the creature.
/// </summary>
public partial class StalagGrip : Node2D
{
    public Enemy Foe;
    /// <summary>How long it holds (the debuff's seconds), after the rise.</summary>
    public float Seconds = 2f;
    /// <summary>Half the width of the foe's body, in px.</summary>
    public float Radius = 12f;
    public const float RiseTime = 0.22f, SinkTime = 0.55f;

    private float _t, _sinkT = -1f;

    /// <summary>0 = flat ground, 1 = fully burst up (it overshoots a little as it bursts).</summary>
    public float Rise
    {
        get
        {
            if (_sinkT >= 0f) return 1f - Smooth(_sinkT / SinkTime);
            float u = Math.Clamp(_t / RiseTime, 0f, 1f);
            // a burst: fast, overshooting, settling
            return u < 1f ? 1.18f * MathF.Sin(u * MathF.PI * 0.5f) : 1f + 0.18f * MathF.Exp(-(_t - RiseTime) * 9f) * MathF.Cos((_t - RiseTime) * 18f);
        }
    }

    /// <summary>Whether it has begun to sink away.</summary>
    public bool Sinking => _sinkT >= 0f;

    private static float Smooth(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    public override void _Ready() => ZIndex = 2;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_sinkT >= 0f)
        {
            _sinkT += dt;
            if (_sinkT >= SinkTime) QueueFree();
            return;
        }
        bool freed = Foe == null || !IsInstanceValid(Foe) || Foe.Dead || !Foe.Reeling;
        if ((_t > 0.1f && freed) || _t >= RiseTime + Seconds)
        {
            _sinkT = 0f;
            G.Fx.Debris(GlobalPosition, new Color(0.5f, 0.43f, 0.35f), 6, 80);
            G.Sfx.Play("rock", GlobalPosition, -10, 0.1f, 0.6f);
        }
    }
}
