using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// An iron key. Any key opens any vault's gate; a hero carries up to Tune.Vault.MaxKeys, and
/// unused ones go on down with them. A level holds two: the first mini-boss slain there drops
/// one, and another lies hidden somewhere out of the way, glinting now and then. Online,
/// whoever touches it first holds it (the host decides; the others' games show a copy).
/// </summary>
public partial class KeyPickup : Node2D
{
    /// <summary>Online: a copy of the host's key (the host decides who gets it).</summary>
    public bool Puppet;
    /// <summary>Hidden where the level was built (it glints more faintly than a dropped one).</summary>
    public bool Stashed;
    /// <summary>Falling speed (a dropped key is tossed up a little first).</summary>
    public float Vy;
    private float _t, _fullT;
    public float T => _t;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _fullT -= dt;
        var cave = G.Cave;
        // it drops to the floor (sinking slowly in water)
        Vy = Math.Min(Vy + (cave.IsWater(GlobalPosition) ? 60 : 500) * dt, 200);
        var np = GlobalPosition + new Vector2(0, Vy * dt);
        if (!cave.IsSolid(np + new Vector2(0, 7))) GlobalPosition = np; else Vy = 0;
        if (G.Chance(Stashed ? 0.012f : 0.05f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-5, 5), -G.Range(0, 8)), new Color(1f, 0.85f, 0.45f), Stashed ? 4 : 6);
        foreach (var p in G.Players)
        {
            if (Puppet || p.Dead || p.GlobalPosition.DistanceTo(GlobalPosition) >= 18) continue;
            if (p.Keys >= Tune.Vault.MaxKeys)
            {
                if (_fullT <= 0 && !p.IsRemote) { _fullT = 2f; G.Fx.Text(GlobalPosition + new Vector2(0, -18), "KEYS FULL", new Color(1f, 0.8f, 0.5f), 10, 1f); }
                continue;
            }
            NetSync.Scope++;
            try
            {
                if (p.IsRemote) NetSync.GivePickup(p, 3, 1);
                else p.GainKey();
                G.Fx.Pop(GlobalPosition, new Color(1f, 0.82f, 0.4f), 8);
                G.Fx.Glint(GlobalPosition + new Vector2(0, -6), new Color(1f, 0.95f, 0.7f), 9);
            }
            finally { NetSync.Scope--; }
            NetSync.PropGone(this, quiet: true);
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw() => DrawKey(this, new Vector2(0, -4 + MathF.Sin(_t * 3) * 1.2f), 1f, 1f);

    /// <summary>The key icon (also the HUD's).</summary>
    public static void DrawKey(CanvasItem c, Vector2 at, float scale, float alpha, bool empty = false)
    {
        var gold = empty ? new Color(0.35f, 0.32f, 0.28f, 0.6f * alpha) : new Color(1f, 0.8f, 0.35f, alpha);
        var dark = new Color(0.35f, 0.24f, 0.08f, alpha);
        c.DrawSetTransform(at, 0, new Vector2(scale, scale));
        if (!empty) c.DrawCircle(new Vector2(-3, 0), 8, new Color(1f, 0.85f, 0.4f, 0.12f * alpha));
        // the bow (a ring), the shaft, and two teeth
        c.DrawArc(new Vector2(-5, 0), 3.2f, 0, Mathf.Tau, 14, gold, 2f);
        c.DrawRect(new Rect2(-2, -1, 9, 2), gold);
        c.DrawRect(new Rect2(4, 1, 1.6f, 2.6f), gold);
        c.DrawRect(new Rect2(6.4f, 1, 1.6f, 1.8f), gold);
        if (!empty) c.DrawCircle(new Vector2(-5, 0), 1.2f, dark);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>
/// The iron gate across a vault's passage. It keeps everyone out, heroes and creatures alike,
/// until a hero with a key opens it (any key opens any gate), and then it rises into the rock
/// for good. The node stands on the passage floor; the gate reaches up to <see cref="Top"/>.
/// Online, the host says whose key opened it, and every game opens its own.
/// </summary>
public partial class VaultGate : Node2D
{
    /// <summary>The passage's ceiling (world y).</summary>
    public float Top;
    /// <summary>Which way the vault lies (+1 right, -1 left).</summary>
    public int Side = 1;
    public bool Opened { get; private set; }
    /// <summary>Seconds since it opened (the view raises it over the first of them).</summary>
    public float OpenT { get; private set; }
    public float Height => GlobalPosition.Y - Top;
    /// <summary>For the view: 0 shut, 1 all the way up.</summary>
    public float Rise => Opened ? Math.Clamp(OpenT / 1.1f, 0f, 1f) : 0f;
    /// <summary>A hero rattled it without a key a moment ago (for the view's shudder).</summary>
    public float Rattle => Math.Max(0f, _noT);

    public static readonly List<VaultGate> All = new();
    private CollisionShape2D _shape;
    private float _askT, _noT;

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    public override void _Ready()
    {
        ZIndex = 1;
        // (bars from floor to ceiling, sunk a little into both)
        float h = Height;
        var body = new StaticBody2D { CollisionLayer = G.LayerTerrain, CollisionMask = 0 };
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(10, h + 12) }, Position = new Vector2(0, -h * 0.5f) };
        body.AddChild(_shape);
        AddChild(body);
    }

    /// <summary>Is a hero at <paramref name="p"/> standing at the (shut) gate?</summary>
    public bool Reaches(Vector2 p) => !Opened && Math.Abs(p.X - GlobalPosition.X) < 30 && p.Y > Top - 6 && p.Y < GlobalPosition.Y + 10;

    /// <summary>Does the shut gate close off cave cell <paramref name="c"/> (for a flood over the
    /// cave's cells, which knows only the rock)?</summary>
    public bool BlocksCell(Vector2I c) => !Opened && c.X == (int)(GlobalPosition.X / CaveData.Cell)
                                          && c.Y >= (int)(Top / CaveData.Cell) && c.Y * CaveData.Cell < GlobalPosition.Y;

    /// <summary>The shut gate a hero at <paramref name="p"/> stands at, if any.</summary>
    public static VaultGate At(Vector2 p)
    {
        foreach (var g in All) if (IsInstanceValid(g) && g.Reaches(p)) return g;
        return null;
    }

    /// <summary>A hero pressed interact at it: a key opens it (online, the host decides whose).</summary>
    public void TryOpen(Player p)
    {
        if (Opened || _askT > 0) return;
        if (p.Keys <= 0)
        {
            if (_noT <= 0)
            {
                _noT = 1f;
                G.Fx.Text(GlobalPosition + new Vector2(0, -Height - 12), "LOCKED: IT TAKES A KEY", new Color(1f, 0.78f, 0.5f), 10, 1.3f);
                G.Sfx.Play("clink", GlobalPosition + new Vector2(0, -20), -4, 0.05f, 0.6f);
            }
            return;
        }
        if (!Net.Online) { p.SpendKey(); Open(); return; }
        _askT = 1f;
        NetSync.AskGate(this);
    }

    /// <summary>It opens (in every game): the lock drops and the gate grinds up into the rock.</summary>
    public void Open()
    {
        if (Opened) return;
        Opened = true;
        OpenT = 0;
        _askT = 0;
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        var lockAt = GlobalPosition + new Vector2(0, -Height * 0.45f);
        G.Sfx.Play("chest", lockAt, -2, 0, 0.6f);
        G.Sfx.Play("rock", GlobalPosition + new Vector2(0, -Height), -4, 0.05f, 0.5f);
        G.Fx.Flash(lockAt, 14, new Color(1f, 0.85f, 0.45f), 0.12f);
        G.Fx.Dust(GlobalPosition + new Vector2(0, -Height), 8);
        G.Fx.AddShake(2.5f);
        G.Main?.OnVaultOpened(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _askT -= dt;
        _noT -= dt;
        if (Opened)
        {
            OpenT += dt;
            // grit trickling down as it grinds up
            if (OpenT < 1.1f && G.Chance(0.3f)) G.Fx.Dust(GlobalPosition + new Vector2(G.Range(-4, 4), -Height), 1);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay): bars, rising when open
        float h = Height, up = Rise * h;
        var iron = new Color(0.3f, 0.3f, 0.33f);
        for (int k = -1; k <= 1; k++) DrawLine(new Vector2(k * 3, -up), new Vector2(k * 3, -h), iron, 1.6f);
        if (!Opened) DrawRect(new Rect2(-3, -h * 0.5f, 6, 5), new Color(0.85f, 0.65f, 0.25f));
    }
}
