using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Warden's Earth Shield, as the ground does it (<see cref="Tune.ElementalWarden"/>, <c>Player/Player.WardenElements.cs</c>): a great lump
/// of earth heaves up out of the floor in front of him and curls over him, and stands for as long as he holds his shield up; let go, it
/// sinks back where it came from. It is a wall to creatures (on the tower layer, which they can't pass), and while it stands the Warden
/// takes nothing and can't be moved (his game keeps that). Every game makes its own from the Warden's state, so it shows online too.
/// </summary>
public partial class EarthMound : StaticBody2D
{
    /// <summary>Which way it rises from him (+1 right, -1 left).</summary>
    public float Side = 1f;
    private float _t, _sinkT = -1f;
    private CollisionShape2D _shape;

    /// <summary>0..1: how far up out of the ground it is (it bursts up, overshooting a little, stands, and sinks back once let go).</summary>
    public float Up
    {
        get
        {
            float rise = Tune.ElementalWarden.MoundRise, sink = Tune.ElementalWarden.MoundSink;
            if (_sinkT >= 0f) return Math.Clamp(1f - _sinkT / sink, 0f, 1f);
            if (_t < rise) { float k = _t / rise; return 1f + 0.12f * MathF.Sin(k * MathF.PI) - (1f - k) * (1f - k); }
            return 1f;
        }
    }
    /// <summary>The tremor in it while it rises and as it sinks (for the view's shake).</summary>
    public float Tremor => _t < Tune.ElementalWarden.MoundRise + 0.15f || _sinkT >= 0f ? 1f : 0.15f;
    /// <summary>Just struck (for the view's flash).</summary>
    public float Flash;
    public bool Sinking => _sinkT >= 0f;

    public override void _Ready()
    {
        ZIndex = 2;
        // (a wall to creatures: the tower layer, which their bodies don't pass; nothing it collides with itself)
        CollisionLayer = G.LayerTower;
        CollisionMask = 0;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Tune.ElementalWarden.MoundWallWidth, Tune.ElementalWarden.MoundWallHeight) }, Position = new Vector2(0, -Tune.ElementalWarden.MoundWallHeight * 0.5f) };
        AddChild(_shape);
    }

    /// <summary>Let go: it sinks back into the floor, and is gone.</summary>
    public void Release()
    {
        if (_sinkT >= 0f) return;
        _sinkT = 0f;
        _shape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        G.Fx.Dust(GlobalPosition, 10, 2.4f, new Color(0.6f, 0.5f, 0.38f, 0.7f));
        G.Sfx.Play("rock", GlobalPosition, -4, 0.05f, 0.6f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (Flash > 0) Flash -= dt * 4f;
        // (grit trickling off it as it stands)
        if (Engine.GetPhysicsFrames() % 6 == 0 && Up > 0.3f)
            G.Fx.Debris(GlobalPosition + new Vector2(G.Range(-14f, 14f), -G.Range(10f, 40f) * Up), Elements.ColorOf(HeroElement.Earth), 1, 40);
        if (_sinkT >= 0f && (_sinkT += dt) >= Tune.ElementalWarden.MoundSink) QueueFree();
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
