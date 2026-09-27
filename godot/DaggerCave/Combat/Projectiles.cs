using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>The player's thrown dagger: flies straight, optionally ricochets to another enemy or pierces.</summary>
public partial class ThrownDagger : Node2D
{
    public Vector2 Dir;
    public float Damage;
    public int BouncesLeft;
    public bool Pierce;

    private static float Speed => Tune.Hero.ThrowSpeed;
    private static float MaxRange => Tune.Hero.ThrowRange;
    private float _traveled, _fadeT = -1, _t;
    private Vector2 _fallVel;
    /// <summary>For the 3D stage: still flying, the whirl angle, and how visible it is while it falls away.</summary>
    public bool Flying => _fadeT < 0;
    public float Spin => Flying ? _t * Tune.Hero.ThrowSpinRadPerSec * (Dir.X >= 0 ? 1 : -1) : 0;
    public float Alpha => _fadeT >= 0 ? Math.Clamp(_fadeT / 0.3f, 0, 1) : 1;
    private readonly HashSet<Enemy> _hit = new();

    public override void _Ready() { ZIndex = 2; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_fadeT >= 0)
        {
            _fadeT -= dt;
            if (_fallVel != Vector2.Zero)
            {
                _fallVel.Y += 900 * dt;
                var np = GlobalPosition + _fallVel * dt;
                if (G.Cave.IsSolid(np)) _fallVel = Vector2.Zero; else GlobalPosition = np;
                Rotation += dt * 14;
            }
            if (_fadeT <= 0) QueueFree();
            QueueRedraw();
            return;
        }
        var cave = G.Cave;
        float spd = cave.IsWater(GlobalPosition) ? Speed * 0.6f : Speed;
        var from = GlobalPosition;
        var step = Dir * spd * dt;
        var to = from + step;

        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || _hit.Contains(e)) continue;
            if (Geometry2D.GetClosestPointToSegment(e.GlobalPosition, from, to).DistanceTo(e.GlobalPosition) > e.HitRadius + 4) continue;
            _hit.Add(e);
            float dealt = e.Hurt(Damage, Dir * 120f, e.GlobalPosition - Dir * e.HitRadius);
            if (dealt > 0)
            {
                G.Player.OnDealtDamage(dealt);
                if (!e.Dead) e.Freeze(Tune.Feel.HitStopThrown);
                G.Fx.Spark(e.GlobalPosition - Dir * e.HitRadius, Dir, e.Dead, new Color(0.8f, 0.95f, 1f));
                G.Main.Kick(Dir * 2f);
                G.Main.Rumble(0.25f, 0.1f, 0.07f);
            }
            else G.Sfx.Play("clink", GlobalPosition, -4);
            if (Pierce) continue;
            if (BouncesLeft > 0 && Retarget(e.GlobalPosition))
            {
                BouncesLeft--;
                Damage *= Tune.Hero.RicochetDamageMult;
                _traveled = 0;
                GlobalPosition = e.GlobalPosition;
                G.Fx.Ring(e.GlobalPosition, 10, new Color(1f, 0.9f, 0.5f));
                QueueRedraw();
                return;
            }
            Drop();
            return;
        }

        if (cave.Raycast(from, Dir, step.Length(), out var hit, 3f))
        {
            GlobalPosition = hit;
            G.Sfx.Play("clink", hit, -4);
            G.Fx.Directional(hit, -Dir, 0.9f, new Color(1f, 0.9f, 0.6f), 6, 160, 1.5f, 0.25f, 300);
            _fadeT = 0.6f;
            _fallVel = Vector2.Zero;
            QueueRedraw();
            return;
        }
        GlobalPosition = to;
        _traveled += step.Length();
        if (_traveled > MaxRange) Drop();
        QueueRedraw();
    }

    private bool Retarget(Vector2 from)
    {
        Enemy best = null; float bd = Tune.Hero.RicochetRange;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || _hit.Contains(e)) continue;
            float d = e.GlobalPosition.DistanceTo(from);
            if (d < bd && G.Cave.LineClear(from, e.GlobalPosition)) { bd = d; best = e; }
        }
        if (best == null) return false;
        Dir = (best.GlobalPosition - from).Normalized();
        return true;
    }

    private void Drop()
    {
        _fadeT = 0.7f;
        _fallVel = new Vector2(-Dir.X * 80, -160);
    }

    /// <summary>
    /// A dagger cartwheeling end over end (~7 turns a second), with a faint spin disc and a blur
    /// arc trailing its tip so it reads as a whirling blade rather than a spear.
    /// </summary>
    public override void _Draw()
    {
        float a = _fadeT >= 0 ? Math.Clamp(_fadeT / 0.3f, 0, 1) : 1;
        bool flying = _fadeT < 0;
        float spin = flying ? _t * Tune.Hero.ThrowSpinRadPerSec * (Dir.X >= 0 ? 1 : -1) : 0;
        const float r = 9f;
        if (flying)
        {
            DrawCircle(Vector2.Zero, r + 1, new Color(0.8f, 0.95f, 1f, 0.10f));
            // blur arcs behind the tip and the pommel
            for (int k = 0; k < 2; k++)
            {
                float tip = spin + k * Mathf.Pi;
                const int n = 10;
                var pts = new Vector2[n];
                var cols = new Color[n];
                float sweep = 1.9f * (Dir.X >= 0 ? -1 : 1);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    pts[i] = Vector2.Right.Rotated(tip + sweep * (1 - t)) * (k == 0 ? r : r * 0.6f);
                    cols[i] = new Color(0.85f, 0.97f, 1f, t * (k == 0 ? 0.75f : 0.35f));
                }
                DrawPolylineColors(pts, cols, k == 0 ? 2.2f : 1.4f);
            }
        }
        DrawSetTransform(Vector2.Zero, spin, Vector2.One);
        // centered on its balance point so it whirls in place
        DrawLine(new Vector2(-6.5f, 0), new Vector2(-2f, 0), new Color(0.4f, 0.25f, 0.12f, a), 2.5f);
        DrawLine(new Vector2(-2f, -3), new Vector2(-2f, 3), new Color(0.78f, 0.66f, 0.3f, a), 1.6f);
        DrawColoredPolygon(new[] { new Vector2(-1, -1.7f), new Vector2(r, 0), new Vector2(-1, 1.7f) }, new Color(0.92f, 0.96f, 1f, a));
        DrawLine(new Vector2(-1, -0.4f), new Vector2(r - 1.5f, -0.1f), new Color(1f, 1f, 1f, a * 0.8f), 0.6f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>Deflectable enemy projectile (thrown rocks, lava globs, spit).</summary>
public partial class EnemyProjectile : Node2D
{
    public Vector2 Vel;
    public float Grav;
    public float Radius = 5f;
    public float Damage = 8f;
    public string Kind = "rock";
    public float Life = 4f;
    /// <summary>Who threw it (credited with the damage for learning).</summary>
    public Enemy Source;
    /// <summary>Sent back by a perfect shield block: now it hurts enemies instead.</summary>
    public bool Reflected;

    public override void _Ready()
    {
        ZIndex = 2;
        G.Main.EnemyProjectiles.Add(this);
    }

    public void Reflect(Vector2 dir, float damageMult)
    {
        Reflected = true;
        Vel = dir.Normalized() * Math.Max(Vel.Length(), 340f);
        Grav *= 0.3f;
        Damage *= 1.5f * damageMult;
        Life = 2f;
        G.Main.EnemyProjectiles.Remove(this); // no longer something to block or swat
        G.Fx.Ring(GlobalPosition, 10, new Color(1f, 0.95f, 0.6f));
    }

    public override void _ExitTree() => G.Main.EnemyProjectiles.Remove(this);

    private Color KindColor => Kind switch
    {
        "lava" or "fire" => new Color(1f, 0.5f, 0.1f),
        "ice" => new Color(0.75f, 0.95f, 1f),
        "crystal" => new Color(0.65f, 0.85f, 1f),
        "spit" => new Color(0.5f, 0.9f, 0.3f),
        _ => new Color(0.8f, 0.75f, 0.7f),
    };

    public void Deflect()
    {
        G.Fx.Burst(GlobalPosition, KindColor, 8, 140, 2f, 0.3f);
        G.Fx.Flash(GlobalPosition, 10, KindColor, 0.1f);
        G.Sfx.Play("clink", GlobalPosition, -4);
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Life -= dt;
        var cave = G.Cave;
        bool water = cave.IsWater(GlobalPosition);
        if (Kind == "lava" && water)
        {
            G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.8f, 0.8f, 0.6f), 6, 60, 3f, 0.6f, -80);
            G.Sfx.Play("lava", GlobalPosition, -10);
            QueueFree(); return;
        }
        Vel.Y += Grav * dt * (water ? 0.3f : 1f);
        if (water) Vel *= 1f / (1f + 2f * dt);
        var np = GlobalPosition + Vel * dt;
        if (cave.IsSolid(np) || Life <= 0)
        {
            Impact(np);
            return;
        }
        GlobalPosition = np;
        if (Reflected)
        {
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || e.GlobalPosition.DistanceTo(GlobalPosition) > Radius + e.HitRadius) continue;
                float dealt = e.Hurt(Damage, Vel.Normalized() * 150f, GlobalPosition);
                if (dealt > 0) G.Player?.OnDealtDamage(dealt);
                Impact(GlobalPosition);
                return;
            }
            QueueRedraw();
            return;
        }
        var p = G.Player;
        // the warden's shield sits a little in front of her
        if (p != null && !p.Dead && p.ShieldRaised && p.GlobalPosition.DistanceTo(GlobalPosition) < Radius + 20 && p.TryBlockProjectile(this))
            return;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition) < Radius + 9)
        {
            p.Hurt(Damage, GlobalPosition - Vel.Normalized() * 10, source: Source);
            Impact(GlobalPosition);
            return;
        }
        if (Kind == "lava" && G.Chance(0.4f)) G.Fx.Burst(GlobalPosition, new Color(1f, 0.55f, 0.1f, 0.8f), 1, 20, 2f, 0.3f, -30);
        else if (Kind == "fire" && G.Chance(0.35f)) G.Fx.Ember(GlobalPosition, new Color(1f, 0.6f, 0.2f));
        else if ((Kind == "ice" || Kind == "crystal") && G.Chance(0.3f)) G.Fx.Trail(GlobalPosition, new Color(KindColor, 0.6f));
        QueueRedraw();
    }

    private void Impact(Vector2 at)
    {
        if (Kind == "lava")
        {
            G.Sfx.Play("lava", at, -8);
            G.Fx.Burst(at, new Color(1f, 0.5f, 0.1f), 10, 120, 2.5f, 0.4f);
            if (G.Cave.FindFloor(GlobalPosition, 40, out var fl) && !G.Cave.IsWater(fl - new Vector2(0, 4)))
                G.Spawn(new LavaPuddle { Position = fl, Source = Source });
        }
        else if (Kind == "fire")
        {
            G.Fx.Smoke(at, 2, new Color(0.3f, 0.25f, 0.22f, 0.4f), 20);
            G.Fx.Ember(at, new Color(1f, 0.6f, 0.2f));
        }
        else if (Kind == "ice" || Kind == "crystal")
        {
            G.Sfx.Play("clink", at, -10, 0.2f, 1.3f);
            G.Fx.Debris(at, KindColor, 5, 120);
            G.Fx.Glint(at, KindColor, 6);
        }
        else
        {
            G.Sfx.Play("rock", at, -10);
            G.Fx.Burst(at, new Color(0.6f, 0.55f, 0.5f), 8, 100, 2f, 0.4f);
            G.Fx.Debris(at, new Color(0.5f, 0.45f, 0.4f), 3, 100);
        }
        QueueFree();
    }

    public override void _Draw()
    {
        switch (Kind)
        {
            case "lava":
                DrawCircle(Vector2.Zero, Radius + 3, new Color(1f, 0.4f, 0.05f, 0.3f));
                DrawCircle(Vector2.Zero, Radius, new Color(1f, 0.55f, 0.1f));
                DrawCircle(new Vector2(-1, -1), Radius * 0.5f, new Color(1f, 0.9f, 0.4f));
                break;
            case "spit":
                DrawCircle(Vector2.Zero, Radius, new Color(0.5f, 0.9f, 0.3f, 0.9f));
                break;
            case "fire":
            {
                float a = Math.Clamp(Life / 0.85f, 0, 1);
                float r = Radius * (1.6f - a * 0.6f);
                DrawCircle(Vector2.Zero, r + 3, new Color(1f, 0.35f, 0.05f, 0.25f * a));
                DrawCircle(Vector2.Zero, r, new Color(1f, 0.55f, 0.1f, 0.7f * a));
                DrawCircle(Vector2.Zero, r * 0.5f, new Color(1f, 0.9f, 0.5f, 0.9f * a));
                break;
            }
            case "ice":
            case "crystal":
            {
                var d = Vel.LengthSquared() > 1 ? Vel.Normalized() : Vector2.Right;
                var n = new Vector2(-d.Y, d.X);
                var col = KindColor;
                DrawColoredPolygon(new[] { d * Radius * 2f, n * Radius * 0.7f, -d * Radius * 1.2f, -n * Radius * 0.7f }, col);
                DrawLine(-d * Radius, d * Radius * 1.8f, new Color(1, 1, 1, 0.8f), 1f);
                DrawCircle(Vector2.Zero, Radius * 2, new Color(col, 0.12f));
                break;
            }
            default:
                DrawColoredPolygon(new[] { new Vector2(-Radius, -1), new Vector2(-1, -Radius), new Vector2(Radius, -1), new Vector2(1, Radius) }, new Color(0.55f, 0.5f, 0.45f));
                break;
        }
    }
}

/// <summary>Burning patch left by lava monsters and their globs.</summary>
public partial class LavaPuddle : Node2D
{
    private float _life = 3.5f, _tick, _t;
    public const float HalfW = 16f;
    public Enemy Source;
    public float LifeLeft => _life;

    public override void _Ready() { ZIndex = 3; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _life -= dt; _t += dt; _tick -= dt;
        if (_life <= 0) { QueueFree(); return; }
        var p = G.Player;
        if (p != null && _tick <= 0)
        {
            var d = p.GlobalPosition - GlobalPosition;
            if (Math.Abs(d.X) < HalfW + 4 && d.Y > -22 && d.Y < 6) { p.Hurt(Tune.Magma.PuddleDamage * G.DepthDmg, GlobalPosition + new Vector2(0, 10), 120, Source); _tick = 0.5f; }
        }
        if (G.Chance(0.1f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-HalfW, HalfW), -2), new Color(1f, 0.6f, 0.15f, 0.8f), 1, 30, 1.8f, 0.4f, -60);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = Math.Clamp(_life / 0.6f, 0, 1);
        float w = HalfW * (0.9f + 0.1f * MathF.Sin(_t * 6));
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, 0.3f));
        DrawCircle(Vector2.Zero, w + 6, new Color(1f, 0.4f, 0.05f, 0.25f * a));
        DrawCircle(Vector2.Zero, w, new Color(1f, 0.45f, 0.08f, 0.9f * a));
        DrawCircle(new Vector2(MathF.Sin(_t * 3) * 5, 0), w * 0.45f, new Color(1f, 0.85f, 0.35f, a));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>Ground-hugging shockwave from golem slams: jump over it.</summary>
public partial class Shockwave : Node2D
{
    public float Dir = 1, Speed = 260f, Damage = 14f, Life = 1.4f, Size = 1f;
    public Enemy Source;
    private bool _hitPlayer;
    private float _t;
    public float T => _t;

    public override void _Ready() { ZIndex = 3; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; Life -= dt;
        var cave = G.Cave;
        var next = GlobalPosition + new Vector2(Dir * Speed * dt, 0);
        if (Life <= 0 || cave.IsSolid(next + new Vector2(Dir * 4, -10 * Size)) || cave.IsWater(next + new Vector2(0, -4)))
        { Fizzle(); return; }
        if (!cave.FindFloor(next + new Vector2(0, -18), 44, out var fl)) { Fizzle(); return; }
        GlobalPosition = fl;
        var p = G.Player;
        if (!_hitPlayer && p != null && !p.Dead)
        {
            var d = p.GlobalPosition - GlobalPosition;
            if (Math.Abs(d.X) < 12 * Size && d.Y > -22 * Size - 8 && d.Y < 6) { p.Hurt(Damage, GlobalPosition + new Vector2(-Dir * 10, 10), 260, Source); _hitPlayer = true; }
        }
        if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition, new Color(0.6f, 0.55f, 0.5f, 0.8f), 1, 80, 2f, 0.35f, 400);
        QueueRedraw();
    }

    private void Fizzle()
    {
        G.Fx.Burst(GlobalPosition, new Color(0.6f, 0.55f, 0.5f, 0.8f), 5, 80, 2f, 0.35f, 400);
        QueueFree();
    }

    public override void _Draw()
    {
        var c = new Color(0.62f, 0.56f, 0.5f);
        for (int k = 0; k < 3; k++)
        {
            float x = -Dir * k * 7 * Size;
            float h = (16 - k * 4) * Size * (0.8f + 0.2f * MathF.Sin(_t * 30 + k));
            DrawColoredPolygon(new[] { new Vector2(x - 5 * Size, 2), new Vector2(x + Dir * 2, -h), new Vector2(x + 5 * Size, 2) }, c.Darkened(k * 0.15f));
        }
    }
}

/// <summary>A stalactite that shakes loose from the ceiling (boss attack) with a telegraph.</summary>
public partial class FallingRock : Node2D
{
    public float Damage = 14f;
    public Enemy Source;
    private float _warn = 0.8f, _vy;
    private Vector2 _floor;
    private bool _hasFloor;
    /// <summary>For the 3D stage: the telegraph time left and where it will land.</summary>
    public float Warn => _warn;
    public Vector2? Target => _hasFloor ? _floor : null;

    public override void _Ready()
    {
        ZIndex = 3;
        _hasFloor = G.Cave.FindFloor(GlobalPosition, 800, out _floor);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_warn > 0)
        {
            _warn -= dt;
            if (G.Chance(0.3f)) G.Fx.Burst(GlobalPosition, new Color(0.6f, 0.55f, 0.5f, 0.7f), 1, 20, 1.5f, 0.5f, 300);
            QueueRedraw();
            return;
        }
        _vy = Math.Min(_vy + 1400 * dt, 900);
        var np = GlobalPosition + new Vector2(0, _vy * dt);
        var p = G.Player;
        if (p != null && !p.Dead && Math.Abs(p.GlobalPosition.X - np.X) < 12 && Math.Abs(p.GlobalPosition.Y - np.Y) < 20)
        { p.Hurt(Damage, np - new Vector2(0, 20), 150, Source); Shatter(np); return; }
        if (G.Cave.IsSolid(np + new Vector2(0, 10))) { Shatter(np); return; }
        GlobalPosition = np;
        QueueRedraw();
    }

    private void Shatter(Vector2 at)
    {
        G.Sfx.Play("rock", at, -4);
        G.Fx.Burst(at, new Color(0.6f, 0.55f, 0.5f), 12, 150, 2.5f, 0.5f);
        QueueFree();
    }

    public override void _Draw()
    {
        if (_warn > 0 && _hasFloor)
        {
            var local = ToLocal(_floor);
            float a = 0.25f + 0.25f * MathF.Sin(_warn * 30);
            DrawSetTransform(local, 0, new Vector2(1, 0.3f));
            DrawCircle(Vector2.Zero, 12, new Color(0, 0, 0, a + 0.2f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        float shake = _warn > 0 ? MathF.Sin(_warn * 60) * 1.5f : 0;
        DrawColoredPolygon(new[] { new Vector2(-7 + shake, -10), new Vector2(7 + shake, -10), new Vector2(shake, 14) }, new Color(0.5f, 0.45f, 0.42f));
    }
}

/// <summary>The swordsman's Crescent Wave: a slicing arc that flies ahead of a swing and cuts
/// through every enemy in its path once.</summary>
public partial class SwordWave : Node2D
{
    public Vector2 Dir;
    public float Damage, Range = 150f, Speed = 480f;
    private float _traveled;
    public float Traveled => _traveled;
    private readonly HashSet<Enemy> _hit = new();

    public override void _Ready() { ZIndex = 2; Rotation = Dir.Angle(); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var from = GlobalPosition;
        var to = from + Dir * Speed * dt;
        _traveled += Speed * dt;
        if (G.Cave.IsSolid(to) || _traveled > Range)
        {
            G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.95f, 1f, 0.8f), 6, 90, 1.8f, 0.3f);
            QueueFree();
            return;
        }
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || _hit.Contains(e)) continue;
            if (Geometry2D.GetClosestPointToSegment(e.GlobalPosition, from, to).DistanceTo(e.GlobalPosition) > e.HitRadius + 10) continue;
            _hit.Add(e);
            float dealt = e.Hurt(Damage, Dir * 120f, e.GlobalPosition - Dir * e.HitRadius);
            if (dealt > 0)
            {
                G.Player?.OnDealtDamage(dealt);
                if (!e.Dead) e.Freeze(Tune.Feel.HitStopThrown);
                G.Fx.Spark(e.GlobalPosition, Dir, false, new Color(0.8f, 0.95f, 1f));
            }
        }
        GlobalPosition = to;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = 1f - _traveled / Range;
        // a crescent facing the way it flies (drawn in local space: +x is forward)
        const int n = 14;
        var outer = new Vector2[n];
        var inner = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)(n - 1) * 2 - 1;      // -1..1 across the crescent
            float ang = t * 1.1f;
            outer[k] = new Vector2(MathF.Cos(ang) * 12, MathF.Sin(ang) * 14);
            inner[k] = new Vector2(MathF.Cos(ang) * 12 - 6 * (1 - t * t), MathF.Sin(ang) * 12);
        }
        var poly = new Vector2[n * 2];
        for (int k = 0; k < n; k++) { poly[k] = outer[k]; poly[2 * n - 1 - k] = inner[k]; }
        DrawColoredPolygon(poly, new Color(0.75f, 0.92f, 1f, 0.55f * a));
        DrawPolyline(outer, new Color(1, 1, 1, 0.9f * a), 1.6f);
    }
}
