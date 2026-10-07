using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A rock ledge or stepping stone the generator laid in a cave (not part of its own rock): a slab you can stand on, and break. It
/// takes about ten blows to bring down: it shudders when struck and sprays dirt, and on the last it collapses in pieces and is gone
/// for good, so no ledge can trap anyone or cut off the guardian. Every game builds the same ones from the same cave and tells the
/// others of each blow.
/// </summary>
public partial class RockLedge : StaticBody2D, IBreakable
{
    public static readonly System.Collections.Generic.List<RockLedge> All = new();

    public int Index;
    /// <summary>What the generator recorded of it (its corners), for the 3D view to mesh.</summary>
    public LedgeRec Rec;
    /// <summary>Half the slab's width, px.</summary>
    public float Half = 40f;
    public int Left = Tune.Ledge.Hits;
    public bool Broken => Left <= 0;
    public float BrokenT { get; private set; }
    public float ShakeT { get; private set; }
    /// <summary>The slab is this thick (px).</summary>
    public const float Thick = 25.6f;
    /// <summary>This slab's own thickness, px (a natural platform is as thick as it grew).</summary>
    public float ThickPx => Rec != null && Rec.Thick > 0 ? Rec.Thick * CaveData.Cell : Thick;
    private float _cool;
    private CollisionShape2D _shape;

    /// <summary>The nearest point of the slab's top to the one striking (so a blow aimed at it from anywhere along it lands).</summary>
    public Vector2 HitCenter
    {
        get
        {
            var p = G.Player;
            float x = p != null ? Math.Clamp(p.GlobalPosition.X, GlobalPosition.X - Half + 4, GlobalPosition.X + Half - 4) : GlobalPosition.X;
            return new Vector2(x, GlobalPosition.Y);
        }
    }
    public float HitSize => Broken ? 0 : 22f;

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Half * 2f - 2f, ThickPx) } };
        AddChild(_shape);
        Breakables.All.Add(this);
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() { All.Remove(this); Breakables.All.Remove(this); }

    public void Strike(Vector2 from) => Hit(false);

    /// <summary>A friend's blow.</summary>
    public void NetHit() => Hit(true);

    private void Hit(bool remote)
    {
        if (Broken || _cool > 0) return;
        _cool = 0.12f;
        ShakeT = 0.45f;
        Left--;
        var dirt = new Color(0.46f, 0.37f, 0.29f);
        var at = HitCenter;
        G.Sfx.Play("rock", at, -6, 0.12f, 1.2f);
        // dirt flung everywhere, out of the slab and down off it
        G.Fx.Debris(at, dirt, 14, 150);
        G.Fx.Dust(at + new Vector2(0, 4), 5, 1.6f, new Color(0.55f, 0.46f, 0.36f, 0.6f));
        G.Fx.Debris(GlobalPosition + new Vector2(G.Range(-Half, Half) * 0.8f, ThickPx * 0.5f), dirt.Darkened(0.15f), 5, 90);
        if (!remote) NetSync.LedgeHit(this);
        if (Left <= 0) Collapse();
    }

    private void Collapse()
    {
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        G.Sfx.Play("rock", GlobalPosition, 0, 0.1f, 0.7f);
        G.Fx.Debris(GlobalPosition, new Color(0.46f, 0.37f, 0.29f), 36, 220);
        G.Fx.Dust(GlobalPosition, 14, 2.6f, new Color(0.55f, 0.46f, 0.36f, 0.7f));
        G.Fx.AddShake(3);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_cool > 0) _cool -= dt;
        if (ShakeT > 0) ShakeT -= dt;
        if (Broken)
        {
            BrokenT += dt;
            // (the pieces have fallen and gone: nothing is left of it)
            if (BrokenT > 2.6f) QueueFree();
        }
    }
}
