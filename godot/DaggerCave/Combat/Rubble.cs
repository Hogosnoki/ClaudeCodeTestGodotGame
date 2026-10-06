using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A fall of boulders plugging a narrow passage. It is solid until it has been cleared, by blows
/// of a blade or by heaving the rocks away (the interact button), a few at a time; the last of
/// them tumbles clear and the way stays open. Every game builds the same plugs from the same
/// cave and tells the others when one is cleared.
/// </summary>
public partial class Rubble : StaticBody2D, IBreakable
{
    public static readonly List<Rubble> All = new();

    public int Index;
    public Vector2 Size = new(32, 64);
    public int Left = Tune.Rubble.Hits;
    public bool Cleared => Left <= 0;
    /// <summary>A bank of skulls (the catacombs), not a fall of boulders.</summary>
    private static bool Bones => G.Cave?.Biome?.Ossuary == true;
    private static Color Dust => Bones ? new Color(0.78f, 0.74f, 0.64f) : new Color(0.5f, 0.45f, 0.4f);
    public float ClearedT { get; private set; }
    public float ShakeT { get; private set; }
    private float _cool;
    private CollisionShape2D _shape;

    public Vector2 HitCenter => GlobalPosition;
    public float HitSize => Cleared ? 0 : Math.Max(Size.X, Size.Y) * 0.5f + 6f;

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } };
        AddChild(_shape);
        Breakables.All.Add(this);
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() { All.Remove(this); Breakables.All.Remove(this); }

    /// <summary>Whether a hero at <paramref name="p"/> is close enough to heave at it.</summary>
    public bool Reaches(Vector2 p)
    {
        if (Cleared) return false;
        var d = (p - GlobalPosition).Abs() - Size * 0.5f;
        return Math.Max(d.X, 0) < 26f && Math.Max(d.Y, 0) < 26f;
    }

    public static Rubble At(Vector2 p)
    {
        foreach (var r in All) if (IsInstanceValid(r) && r.Reaches(p)) return r;
        return null;
    }

    /// <summary>A blow of a blade.</summary>
    public void Strike(Vector2 from) => Hit(false);

    /// <summary>The interact button: heave at the rocks.</summary>
    public void Heave() => Hit(true);

    private void Hit(bool heave, bool remote = false)
    {
        if (Cleared || _cool > 0) return;
        _cool = 0.18f;
        ShakeT = 0.45f;
        Left--;
        G.Sfx.Play(Bones ? "clink" : "rock", GlobalPosition, -4, 0.1f, (heave ? 0.8f : 1f) * (Bones ? 1.3f : 1f));
        G.Fx.Debris(GlobalPosition, Dust, 6, 120);
        if (!remote) NetSync.RubbleHit(this);
        if (Left <= 0) Clear();
    }

    /// <summary>A friend's game cleared (or chipped) this plug.</summary>
    public void NetHit() => Hit(false, remote: true);

    private void Clear()
    {
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        G.Sfx.Play(Bones ? "clink" : "rock", GlobalPosition, 0, 0.1f, Bones ? 0.9f : 0.6f);
        G.Fx.Debris(GlobalPosition, Dust, 22, 200);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, Size.Y * 0.5f), 24, new Color(1, 1, 1, 0.3f), 0.25f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_cool > 0) _cool -= dt;
        if (ShakeT > 0) ShakeT -= dt;
        if (Cleared) ClearedT += dt;
    }
}
