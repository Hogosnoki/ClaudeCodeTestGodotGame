using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Harmless ambient wildlife. Critters use the same sprite/animation conventions as enemies
/// (left/right facing clips, turn transitions) but never fight; they just react to the player.
/// They sleep when far away.
/// </summary>
public abstract partial class Critter : Node2D
{
    protected SpriteAnimator Anim;
    protected Vector2 Home;
    protected float T;
    protected int Face = 1;

    public override void _Ready()
    {
        Home = GlobalPosition;
        ZIndex = 0;
        Face = G.Chance(0.5f) ? 1 : -1;
    }

    public override void _PhysicsProcess(double delta)
    {
        var p = G.Player;
        if (p == null || GlobalPosition.DistanceSquaredTo(p.GlobalPosition) > 900 * 900) return;
        T += (float)delta;
        Tick((float)delta, p);
        Anim.Face(Face);
    }

    protected abstract void Tick(float dt, Player p);
}

/// <summary>A pale glowing moth that drifts around a spot and scatters when you come close.</summary>
public partial class GlowMoth : Critter
{
    private Vector2 _vel;
    private float _seed;

    public override void _Ready()
    {
        base._Ready();
        _seed = G.Range(0, 100);
        Anim = SpriteAnimator.Create("moth");
        Anim.Loop("flutter", G.Range(0.8f, 1.2f));
        AddChild(Anim);
        ZIndex = 4;
    }

    protected override void Tick(float dt, Player p)
    {
        var cave = G.Cave;
        var away = GlobalPosition - p.GlobalPosition;
        Vector2 want;
        if (away.Length() < 70) want = away.Normalized() * 120 + new Vector2(0, -40);
        else
        {
            // lazy figure-eights around home
            var target = Home + new Vector2(MathF.Sin(T * 0.7f + _seed) * 40, MathF.Sin(T * 1.3f + _seed * 2) * 18);
            want = (target - GlobalPosition) * 1.5f;
        }
        want += new Vector2(MathF.Sin(T * 9 + _seed) * 25, MathF.Cos(T * 7 + _seed) * 25);
        _vel = _vel.Lerp(want, 1 - MathF.Exp(-dt * 3));
        var next = GlobalPosition + _vel * dt;
        if (!cave.IsSolid(next) && !cave.IsWater(next)) GlobalPosition = next; else _vel = -_vel * 0.5f;
        if (GlobalPosition.DistanceTo(Home) > 220) Home = GlobalPosition;
        if (Math.Abs(_vel.X) > 8) Face = Math.Sign(_vel.X);
        if (G.Chance(0.01f)) G.Fx.Burst(GlobalPosition, new Color(0.7f, 1f, 0.95f, 0.8f), 1, 10, 1.2f, 0.8f, 10);
    }
}

/// <summary>A little crab that potters along the floor and ducks into its shell when you approach.</summary>
public partial class CaveCrab : Critter
{
    private float _moveT, _dir;
    private bool _hiding;

    public override void _Ready()
    {
        base._Ready();
        Anim = SpriteAnimator.Create("crab");
        Anim.Loop("idle");
        AddChild(Anim);
    }

    protected override void Tick(float dt, Player p)
    {
        bool near = p.GlobalPosition.DistanceTo(GlobalPosition) < 64;
        if (near != _hiding)
        {
            _hiding = near;
            if (near) { Anim.Loop("hide"); Anim.Once("hide", 2); }
        }
        if (_hiding) return;
        _moveT -= dt;
        if (_moveT <= 0)
        {
            _moveT = G.Range(0.6f, 2.5f);
            _dir = G.Chance(0.4f) ? 0 : (GlobalPosition.X < Home.X - 50 ? 1 : GlobalPosition.X > Home.X + 50 ? -1 : (G.Chance(0.5f) ? 1 : -1));
        }
        if (_dir != 0)
        {
            var cave = G.Cave;
            var next = GlobalPosition + new Vector2(_dir * 28 * dt, 0);
            if (cave.FindFloor(next + new Vector2(0, -10), 24, out var fl) && !cave.IsSolid(fl + new Vector2(_dir * 6, -6)))
            {
                GlobalPosition = fl + new Vector2(0, -5);
                Face = (int)_dir;
            }
            else _dir = 0;
        }
        Anim.Loop(_dir != 0 ? "scuttle" : "idle");
    }
}
