using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Warden's Earth Shield, as the ground does it (<see cref="Tune.ElementalWarden"/>, <c>Player/Player.WardenElements.cs</c>): a great lump
/// of earth heaves up out of the floor in front of him and curls over him, holds, then sinks back where it came from. It is only what is
/// seen: while it stands the Warden himself takes nothing and can't be moved (his game keeps that). Every game makes its own from the
/// Warden's state, so it shows online as it does here.
/// </summary>
public partial class EarthMound : Node2D
{
    /// <summary>Which way it rises from him (+1 right, -1 left), and how long it stands, rise and sinking included.</summary>
    public float Side = 1f, Seconds = Tune.ElementalWarden.MoundSeconds;
    private float _t;

    /// <summary>0..1: how far up out of the ground it is (it bursts up, overshooting a little, then sinks back at the end).</summary>
    public float Up
    {
        get
        {
            float rise = Tune.ElementalWarden.MoundRise, sink = Tune.ElementalWarden.MoundSink;
            if (_t < rise) { float k = _t / rise; return 1f + 0.12f * MathF.Sin(k * MathF.PI) - (1f - k) * (1f - k); }
            float left = Seconds - _t;
            return left < sink ? Math.Clamp(left / sink, 0f, 1f) : 1f;
        }
    }
    /// <summary>The tremor in it while it rises and as it settles (for the view's shake).</summary>
    public float Tremor => _t < Tune.ElementalWarden.MoundRise + 0.15f || Seconds - _t < Tune.ElementalWarden.MoundSink ? 1f : 0.15f;
    /// <summary>Just struck (for the view's flash).</summary>
    public float Flash;

    public override void _Ready() => ZIndex = 2;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (Flash > 0) Flash -= dt * 4f;
        // (grit trickling off it as it stands, a cloud as it goes down)
        if (Engine.GetPhysicsFrames() % 6 == 0 && Up > 0.3f)
            G.Fx.Debris(GlobalPosition + new Vector2(G.Range(-14f, 14f), -G.Range(10f, 40f) * Up), Elements.ColorOf(HeroElement.Earth), 1, 40);
        if (_t >= Seconds) QueueFree();
    }
}

/// <summary>
/// The Warden's Frost Shield or Wind Barrier, seen: a shell about him (ice, or a thinner skin of wind) that follows him while it holds
/// (<see cref="Player.ShellElement"/>), wearing as it soaks blows, and is gone once it breaks. Every game makes its own from his state.
/// </summary>
public partial class WardShell : Node2D
{
    public Player Hero;
    public HeroElement El;
    public float Age { get; private set; }

    public override void _Ready() => ZIndex = 3;

    public override void _PhysicsProcess(double delta)
    {
        Age += (float)delta;
        if (Hero == null || !IsInstanceValid(Hero) || Hero.ShellElement != El) { QueueFree(); return; }
        GlobalPosition = Hero.GlobalPosition + new Vector2(0, -6);
    }

    /// <summary>0..1: how much more it can soak.</summary>
    public float Left => Hero != null && IsInstanceValid(Hero) ? Hero.ShellLeft : 0f;
    /// <summary>Just struck (for the view's flash).</summary>
    public float Flash => Hero != null && IsInstanceValid(Hero) ? Hero.ShellFlash : 0f;
}
