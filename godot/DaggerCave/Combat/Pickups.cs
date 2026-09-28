using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Experience shard dropped by enemies; drifts toward the player once in magnet range.</summary>
public partial class XpOrb : Node2D
{
    public int Value = 1;
    public Vector2 Vel;
    /// <summary>Online: a copy of the host's orb (it flies to whoever is nearest, but the host decides who takes it).</summary>
    public bool Puppet;
    private float _t, _life = 45f;
    public float T => _t;

    public override void _Ready() { ZIndex = 2; }

    /// <summary>The hero it's drawn to: the nearest one standing (there's only one, alone).</summary>
    private static Player Nearest(Vector2 at)
    {
        if (G.Players.Count <= 1) return G.Player;
        Player best = null; float bd = float.MaxValue;
        foreach (var h in G.Players)
        {
            if (h.Dead) continue;
            float d = h.GlobalPosition.DistanceSquaredTo(at);
            if (d < bd) { bd = d; best = h; }
        }
        return best;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _life -= dt;
        if (_life <= 0) { QueueFree(); return; }
        var p = Nearest(GlobalPosition);
        var cave = G.Cave;
        if (p != null && !p.Dead)
        {
            var to = p.GlobalPosition - GlobalPosition;
            float d = to.Length();
            float magnet = Tune.Hero.XpMagnetRange * p.Stats.MagnetMult;
            if (d < 14)
            {
                // experience is shared online: whoever takes the orb, everyone gains it
                if (!Puppet)
                {
                    if (Net.Online) NetSync.XpTaken(this, Value);
                    G.Player?.AddXp(Value);
                }
                G.Sfx.Play("xp", GlobalPosition, -6, 0.15f, 1f + Math.Min(Value, 10) * 0.02f);
                G.Fx.Glint(GlobalPosition, new Color(0.45f, 1f, 0.75f), 4 + Math.Min(Value, 10) * 0.4f);
                QueueFree();
                return;
            }
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
    /// <summary>Online: a copy of the host's heart (the host decides who gets it).</summary>
    public bool Puppet;
    private float _t, _life = 25f, _vy = -120f;
    public float T => _t;
    public float LifeLeft => _life;

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
        foreach (var p in G.Players)
        {
            if (Puppet || p.Dead || p.GlobalPosition.DistanceTo(GlobalPosition) >= 16) continue;
            NetSync.Scope++;
            try
            {
                float amount = p.Stats.MaxHp * Tune.Drops.HeartHealFrac;
                if (p.IsRemote) NetSync.GivePickup(p, 1, amount); else p.Heal(amount);
                G.Sfx.Play("heal", GlobalPosition, -4);
                G.Fx.Pop(GlobalPosition, Player.HealColor, 6);
                G.Fx.Ring(p.GlobalPosition, 16, Player.HealColorLight, 0.3f);
            }
            finally { NetSync.Scope--; }
            NetSync.PropGone(this, quiet: true);
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

/// <summary>A red potion flask: picked up if you have room on your belt (drink with Q / Y).</summary>
public partial class PotionPickup : Node2D
{
    /// <summary>Online: a copy of the host's potion (the host decides who gets it).</summary>
    public bool Puppet;
    private float _t, _life = 40f, _vy = -140f;
    public float T => _t;
    public float LifeLeft => _life;

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _life -= dt;
        if (_life <= 0) { QueueFree(); return; }
        var cave = G.Cave;
        _vy = Math.Min(_vy + (cave.IsWater(GlobalPosition) ? 60 : 500) * dt, 200);
        var np = GlobalPosition + new Vector2(0, _vy * dt);
        if (!cave.IsSolid(np + new Vector2(0, 8))) GlobalPosition = np; else _vy = 0;
        if (G.Chance(0.04f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-4, 4), -6), new Color(1f, 0.6f, 0.7f), 5);
        foreach (var p in G.Players)
        {
            if (Puppet || p.Dead || p.GlobalPosition.DistanceTo(GlobalPosition) >= 18 || p.Potions >= Meta.MaxPotions) continue;
            NetSync.Scope++;
            try
            {
                if (p.IsRemote) NetSync.GivePickup(p, 2, 1);
                else p.Potions++;
                G.Sfx.Play("chest", GlobalPosition, -6, 0, 1.4f);
                G.Fx.Text(GlobalPosition + new Vector2(0, -16), "+POTION", new Color(1f, 0.55f, 0.7f), 11, 1f);
                G.Fx.Pop(GlobalPosition, new Color(1f, 0.4f, 0.55f), 8);
            }
            finally { NetSync.Scope--; }
            NetSync.PropGone(this, quiet: true);
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw() => DrawFlask(this, Vector2.Zero, 1f + 0.08f * MathF.Sin(_t * 5), _life < 5 && (int)(_t * 8) % 2 == 0 ? 0.3f : 1f);

    /// <summary>The flask icon (also used by the HUD).</summary>
    public static void DrawFlask(CanvasItem c, Vector2 at, float scale, float alpha, bool empty = false)
    {
        var glass = new Color(0.85f, 0.9f, 1f, 0.8f * alpha);
        var liquid = empty ? new Color(0.25f, 0.2f, 0.25f, 0.6f * alpha) : new Color(1f, 0.25f, 0.4f, alpha);
        c.DrawSetTransform(at, 0, new Vector2(scale, scale));
        if (!empty) c.DrawCircle(new Vector2(0, -2), 10, new Color(1f, 0.3f, 0.45f, 0.15f * alpha));
        c.DrawCircle(new Vector2(0, 0), 5.5f, liquid);
        c.DrawRect(new Rect2(-2, -9, 4, 5), glass);
        c.DrawRect(new Rect2(-2.5f, -10.5f, 5, 2), new Color(0.6f, 0.4f, 0.25f, alpha));
        c.DrawArc(new Vector2(0, 0), 5.5f, 0, Mathf.Tau, 16, glass, 1.2f);
        c.DrawCircle(new Vector2(-2, -1.5f), 1.3f, new Color(1, 1, 1, 0.8f * alpha));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>
/// Treasure chest: opening it (the interact button, standing at it) heals and offers a pick of
/// three upgrades. Walking past no longer opens it by accident.
/// </summary>
public partial class Chest : Node2D
{
    private bool _open;
    private float _t, _openT, _askT;
    public bool Open => _open;
    public float OpenT => _openT;
    /// <summary>Every chest in the level (for the prompt and the interact button).</summary>
    public static readonly List<Chest> All = new();

    public override void _Ready() { ZIndex = 2; }
    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Is a hero standing at <paramref name="p"/> close enough to open it?</summary>
    public bool Reaches(Vector2 p) => !_open && p.DistanceTo(GlobalPosition + new Vector2(0, -8)) < 30;

    /// <summary>The unopened chest a hero at <paramref name="p"/> can open, if any.</summary>
    public static Chest At(Vector2 p)
    {
        foreach (var c in All) if (GodotObject.IsInstanceValid(c) && c.Reaches(p)) return c;
        return null;
    }

    /// <summary>The hero pressed interact at it (online, the host says who gets it: the first to ask).</summary>
    public void Interact()
    {
        if (_open) return;
        if (!Net.Online) { OpenBy(Net.Me); return; }
        if (_askT > 0) return;
        _askT = 1f;
        NetSync.AskChest(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _askT -= dt;
        if (_open) { _openT += dt; QueueRedraw(); return; }
        if (G.Chance(0.05f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-10, 10), -14), new Color(1f, 0.9f, 0.5f), 1, 10, 1.5f, 0.8f, -20);
        if (G.Chance(0.015f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-10, 10), -G.Range(4, 12)), new Color(1f, 0.95f, 0.6f), 6);
        QueueRedraw();
    }

    /// <summary>Opens (online: for whoever the host says reached it first; the upgrade pick is theirs).</summary>
    public void OpenBy(int opener)
    {
        if (_open) return;
        _open = true;
        G.Sfx.Play("chest", GlobalPosition);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -10), new Color(1f, 0.85f, 0.3f), 30, 220, 2.5f, 0.9f, 200);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -10), 30, new Color(1f, 0.9f, 0.5f));
        for (int k = 0; k < 8; k++) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-14, 14), -G.Range(6, 30)), new Color(1f, 0.9f, 0.5f), 7);
        if (opener != Net.Me) return;
        G.Player?.Heal(Tune.Drops.ChestHeal);
        G.Main.OfferChest(GlobalPosition);
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

/// <summary>
/// An exit that opens where the guardian fell: a stairway down into what lies below. Each
/// leads to a different biome and descends one or two levels deeper. Nobody is taken down
/// until they choose to go (the interact button, or up, while standing at it), so one that
/// opens underfoot can't snatch you away from the guardian's chest.
/// </summary>
public partial class Portal : Node2D
{
    public BiomeDef To;
    public int Depth;
    public string Label = "";
    private float _t, _near;
    private bool _used;
    public float Age => _t;
    /// <summary>For the 3D view: 0..1, how strongly to show the "descend" prompt (a hero is at the door).</summary>
    public float Near => _near;
    public bool Used => _used;

    public override void _Ready() { ZIndex = -1; G.Sfx.Play("portal", GlobalPosition, -6, 0.05f, 0.7f); }

    /// <summary>Is someone standing at <paramref name="p"/> close enough to go down?</summary>
    public bool Reaches(Vector2 p) => !_used && _t > 0.6f && Math.Abs(p.X - GlobalPosition.X) < 26 && Math.Abs(p.Y - GlobalPosition.Y) < 38;

    /// <summary>Go down (online: wait here until everyone still standing is at this exit).</summary>
    public void Enter()
    {
        if (_used) return;
        if (Net.Online) { G.Main.WaitAtExit(this); return; }
        _used = true;
        G.Sfx.Play("portal", GlobalPosition, 0, 0.05f, 0.8f);
        G.Fx.Flash(GlobalPosition, 40, (To?.Glow ?? new Color(0.7f, 0.5f, 1f)).Darkened(0.3f), 0.25f);
        G.Main.EnterExit(To, Depth);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        bool near = false;
        foreach (var p in G.Players) if (!p.Dead && Reaches(p.GlobalPosition)) near = true;
        _near = Math.Clamp(_near + (near ? dt * 5f : -dt * 3f), 0f, 1f);
        // a draught: faint motes of the world below, drawn down into the dark
        var glow = To?.Glow ?? new Color(0.7f, 0.5f, 1f);
        if (G.Chance(0.12f)) G.Fx.Mote(GlobalPosition + new Vector2(G.Range(-30, 30), G.Range(-40, -10)), GlobalPosition + new Vector2(G.Range(-6, 6), 24), new Color(glow, 0.55f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        float grow = Math.Min(1, _t);
        var b = To;
        var deep = b?.Deep ?? new Color(0.1f, 0.05f, 0.2f);
        var glow = b?.Glow ?? new Color(0.7f, 0.5f, 1f);
        var edge = b?.Edge ?? new Color(0.4f, 0.3f, 0.5f);
        // an arched tunnel mouth: rock rim, the next biome's colours receding inside it
        DrawSetTransform(new Vector2(0, 30), 0, new Vector2(grow, grow));
        var arch = new System.Collections.Generic.List<Vector2>();
        for (int k = 0; k <= 16; k++) { float a = Mathf.Pi + k * Mathf.Pi / 16; arch.Add(new Vector2(MathF.Cos(a) * 26, -30 + MathF.Sin(a) * 30)); }
        arch.Add(new Vector2(26, 0)); arch.Add(new Vector2(-26, 0));
        DrawColoredPolygon(arch.ToArray(), edge.Darkened(0.3f));
        for (int k = 0; k < 5; k++)
        {
            float s = 1f - k * 0.17f;
            var ring = new System.Collections.Generic.List<Vector2>();
            for (int j = 0; j <= 16; j++) { float a = Mathf.Pi + j * Mathf.Pi / 16; ring.Add(new Vector2(MathF.Cos(a) * 21 * s, -27 * s + MathF.Sin(a) * 27 * s)); }
            ring.Add(new Vector2(21 * s, 0)); ring.Add(new Vector2(-21 * s, 0));
            DrawColoredPolygon(ring.ToArray(), deep.Lerp(glow, k * 0.12f + 0.08f * MathF.Sin(_t * 3 + k)));
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var font = ThemeDB.FallbackFont;
        float a2 = Math.Min(1, _t * 2);
        DrawString(font, new Vector2(-90, -44), b?.Name.ToUpperInvariant() ?? "DEEPER", HorizontalAlignment.Center, 180, 12, new Color(glow, a2));
        DrawString(font, new Vector2(-90, -31), Label, HorizontalAlignment.Center, 180, 9, new Color(1, 1, 1, 0.8f * a2));
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
    public float T => _t;

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
