using System;
using Godot;

namespace DaggerCave;

/// <summary>Experience shard dropped by enemies; drifts toward the player once in magnet range.</summary>
public partial class XpOrb : Node2D
{
    public int Value = 1;
    public Vector2 Vel;
    private float _t, _life = 45f;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _life -= dt;
        if (_life <= 0) { QueueFree(); return; }
        var p = G.Player;
        var cave = G.Cave;
        if (p != null && !p.Dead)
        {
            var to = p.GlobalPosition - GlobalPosition;
            float d = to.Length();
            float magnet = Tune.Hero.XpMagnetRange * p.Stats.MagnetMult;
            if (d < 14) { p.AddXp(Value); G.Sfx.Play("xp", GlobalPosition, -6, 0.15f, 1f + Math.Min(Value, 10) * 0.02f); QueueFree(); return; }
            if (d < magnet || _t > 12) { Vel = Vel.MoveToward(to.Normalized() * 420f, 1400f * dt); GlobalPosition += Vel * dt; QueueRedraw(); return; }
        }
        Vel *= 1f / (1f + 3f * dt);
        if (!cave.IsWater(GlobalPosition)) Vel.Y += 200 * dt; else Vel.Y -= 30 * dt;
        var np = GlobalPosition + Vel * dt;
        if (cave.IsSolid(np)) Vel = -Vel * 0.3f; else GlobalPosition = np;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s = 2.2f + Math.Min(Value, 12) * 0.18f;
        float bob = MathF.Sin(_t * 5) * 1.2f;
        var c = new Color(0.45f, 1f, 0.75f);
        DrawCircle(new Vector2(0, bob), s + 3, new Color(c, 0.18f));
        DrawColoredPolygon(new[] { new Vector2(0, -s + bob), new Vector2(s * 0.7f, bob), new Vector2(0, s + bob), new Vector2(-s * 0.7f, bob) }, c);
    }
}

public partial class HeartPickup : Node2D
{
    private float _t, _life = 25f, _vy = -120f;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _life -= dt;
        if (_life <= 0) { QueueFree(); return; }
        var cave = G.Cave;
        _vy = Math.Min(_vy + (cave.IsWater(GlobalPosition) ? 60 : 500) * dt, 200);
        var np = GlobalPosition + new Vector2(0, _vy * dt);
        if (!cave.IsSolid(np + new Vector2(0, 6))) GlobalPosition = np; else _vy = 0;
        var p = G.Player;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition) < 16)
        {
            p.Heal(Tune.Drops.HeartHeal);
            G.Sfx.Play("heal", GlobalPosition, -4);
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = _life < 4 && (int)(_t * 8) % 2 == 0 ? 0.3f : 1f;
        float s = 1f + 0.1f * MathF.Sin(_t * 6);
        var c = new Color(1f, 0.25f, 0.35f, a);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(s, s));
        DrawCircle(new Vector2(-2.5f, -1), 3.2f, c);
        DrawCircle(new Vector2(2.5f, -1), 3.2f, c);
        DrawColoredPolygon(new[] { new Vector2(-5.5f, 0), new Vector2(5.5f, 0), new Vector2(0, 6) }, c);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>Treasure chest: opening it heals and offers a pick of three upgrades.</summary>
public partial class Chest : Node2D
{
    private bool _open;
    private float _t, _openT;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_open) { _openT += dt; QueueRedraw(); return; }
        var p = G.Player;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition + new Vector2(0, -8)) < 24)
        {
            _open = true;
            G.Sfx.Play("chest", GlobalPosition);
            G.Fx.Burst(GlobalPosition + new Vector2(0, -10), new Color(1f, 0.85f, 0.3f), 30, 220, 2.5f, 0.9f, 200);
            p.Heal(Tune.Drops.ChestHeal);
            G.Main.OfferChest(GlobalPosition);
        }
        if (G.Chance(0.05f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-10, 10), -14), new Color(1f, 0.9f, 0.5f), 1, 10, 1.5f, 0.8f, -20);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var wood = new Color(0.5f, 0.3f, 0.15f);
        var band = new Color(0.85f, 0.7f, 0.25f);
        if (!_open) DrawCircle(new Vector2(0, -8), 20 + MathF.Sin(_t * 3) * 2, new Color(1f, 0.85f, 0.4f, 0.08f));
        DrawRect(new Rect2(-11, -12, 22, 12), wood);
        DrawRect(new Rect2(-11, -12, 22, 12), band, false, 1.5f);
        float lid = _open ? Math.Min(1, _openT * 5) : 0;
        DrawSetTransform(new Vector2(-11, -12), -lid * 1.9f, Vector2.One);
        DrawRect(new Rect2(0, -6, 22, 6), wood.Lightened(0.1f));
        DrawRect(new Rect2(0, -6, 22, 6), band, false, 1.5f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawRect(new Rect2(-2, -9, 4, 4), band);
        if (_open && _openT < 1.5f) DrawCircle(new Vector2(0, -14), 10 * (1 - _openT / 1.5f) + 2, new Color(1f, 0.95f, 0.6f, 0.5f * (1 - _openT / 1.5f)));
    }
}

/// <summary>Appears where the boss fell; stepping in descends to the next, harder cave.</summary>
public partial class Portal : Node2D
{
    private float _t;
    private bool _used;

    public override void _Ready() { ZIndex = 2; G.Sfx.Play("portal", GlobalPosition); }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        var p = G.Player;
        if (!_used && _t > 1f && p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition) < 26)
        {
            _used = true;
            G.Sfx.Play("portal");
            G.Main.CallDeferred(Main.MethodName.NextDepth);
        }
        if (G.Chance(0.3f)) G.Fx.Burst(GlobalPosition + G.RandDir() * 26, new Color(0.7f, 0.5f, 1f), 1, 30, 2f, 0.6f, -40);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float grow = Math.Min(1, _t);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(0.65f * grow, 1f * grow));
        for (int k = 5; k >= 0; k--)
        {
            float r = 10 + k * 5 + MathF.Sin(_t * 4 + k) * 2;
            DrawCircle(Vector2.Zero, r, new Color(0.4f + k * 0.08f, 0.2f + k * 0.05f, 0.9f, 0.18f + (5 - k) * 0.05f));
        }
        for (int k = 0; k < 3; k++) DrawArc(Vector2.Zero, 14 + k * 8, _t * (2 + k) , _t * (2 + k) + 2.5f, 16, new Color(0.9f, 0.8f, 1f, 0.8f), 2f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>A crack in the flooded cave floor that now and then lets go of a big air bubble.</summary>
public partial class AirVent : Node2D
{
    private float _t, _next;

    public override void _Ready()
    {
        ZIndex = 1;
        _next = G.Range(0.5f, Tune.Hero.AirVentIntervalMax);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _next -= dt;
        var p = G.Player;
        if (p == null || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 1400 * 1400) return; // idle when far
        if (_next <= 0)
        {
            _next = G.Range(Tune.Hero.AirVentIntervalMin, Tune.Hero.AirVentIntervalMax);
            G.Spawn(new AirBubble { Position = GlobalPosition + new Vector2(G.Range(-3, 3), -4) });
        }
        if (G.Chance(0.06f)) G.Fx.Bubbles(GlobalPosition + new Vector2(G.Range(-4, 4), -2), 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // a dark fissure with a faint shimmer above it
        DrawColoredPolygon(new[] { new Vector2(-7, 1), new Vector2(-2, -2), new Vector2(2, -1.5f), new Vector2(7, 1) }, new Color(0.02f, 0.04f, 0.06f, 0.9f));
        float a = 0.08f + 0.05f * MathF.Sin(_t * 3);
        DrawCircle(new Vector2(0, -6), 7, new Color(0.7f, 0.9f, 1f, a));
    }
}

/// <summary>A rising air bubble: swim into it for a breath of air.</summary>
public partial class AirBubble : Node2D
{
    private float _t;
    private readonly float _phase = G.Range(0, 6);

    public override void _Ready() => ZIndex = 3;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        var pos = GlobalPosition + new Vector2(MathF.Sin(_t * 2.6f + _phase) * 14f * dt, -34f * dt);
        if (!G.Cave.IsWater(pos) || G.Cave.IsSolid(pos) || _t > 30f) { Pop(); return; }
        GlobalPosition = pos;
        var p = G.Player;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition + new Vector2(0, 4)) < 15)
        {
            p.AddBreath(Tune.Hero.AirBubbleBreath);
            G.Sfx.Play("bubble", GlobalPosition, -2, 0.1f, 0.7f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -12), "+AIR", new Color(0.7f, 0.95f, 1f), 9, 0.7f);
            Pop();
            return;
        }
        QueueRedraw();
    }

    private void Pop()
    {
        G.Fx.Bubbles(GlobalPosition, 4);
        QueueFree();
    }

    public override void _Draw()
    {
        float r = 6f + 0.6f * MathF.Sin(_t * 5);
        DrawCircle(Vector2.Zero, r + 3, new Color(0.6f, 0.9f, 1f, 0.12f));
        DrawCircle(Vector2.Zero, r, new Color(0.75f, 0.93f, 1f, 0.28f));
        DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 20, new Color(0.9f, 0.98f, 1f, 0.85f), 1.2f);
        DrawCircle(new Vector2(-r * 0.35f, -r * 0.4f), r * 0.25f, new Color(1, 1, 1, 0.8f));
    }
}
