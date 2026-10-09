using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A cloud of sulphurous gas (the Sulphur Springs): it hangs where it was let out, drifting up a little and stopping against the roof,
/// and poisons whoever stands in it. It is fuel: a fire that touches it (a bolt, a fireball, a burning creature, another blast) lights it,
/// and after a heartbeat it goes up in a ball of fire that hurts everything in it and lights the clouds beside it in turn.
/// </summary>
public partial class GasCloud : Node2D
{
    public static readonly List<GasCloud> All = new();
    public float Radius = Tune.Sulphur.CloudRadius, Life = Tune.Sulphur.CloudLife;
    /// <summary>Who let it out (it harms no one of that kind: the gas's own are at home in it).</summary>
    public Enemy Source;
    private float _t, _tick, _fuse = -1f, _sway;
    public float T => _t;
    /// <summary>For the 3D view: 0..1 how far into its heartbeat before the blast (-1 when not lit).</summary>
    public float Fuse => _fuse < 0 ? -1f : 1f - Math.Clamp(_fuse / Tune.Sulphur.ChainDelay, 0, 1);
    public bool Lit => _fuse >= 0f;
    /// <summary>The size it has now: it swells as it comes out and thins as it fades.</summary>
    public float Size => Radius * Math.Min(1f, _t * 2.5f) * Math.Clamp((Life - _t) / 2f, 0.55f, 1f);
    /// <summary>For the 3D view: 0..1 how solid it is.</summary>
    public float Density => Math.Clamp((Life - _t) / 1.5f, 0, 1) * Math.Min(1f, _t * 3f);

    public override void _Ready()
    {
        ZIndex = 3;
        _sway = G.Range(0, 10);
        All.Add(this);
    }

    public override void _ExitTree() => All.Remove(this);

    /// <summary>A fire at <paramref name="at"/> (reaching <paramref name="r"/> px): every cloud it touches is lit, and every gasbag in it goes up. Returns how many things caught.</summary>
    public static int IgniteAt(Vector2 at, float r)
    {
        int n = 0;
        foreach (var c in All.ToArray())
        {
            if (!IsInstanceValid(c) || c.Lit || c.Density < 0.15f) continue;
            if (c.GlobalPosition.DistanceTo(at) < c.Size + r) { c.Light(0.02f); n++; }
        }
        foreach (var e in G.Enemies.ToArray())
            if (e is Gasbag gb && IsInstanceValid(gb) && !gb.Dead && gb.GlobalPosition.DistanceTo(at) < r + gb.HitRadius + 6f) { gb.Pop(true); n++; }
        return n;
    }

    /// <summary>Lit: it goes up after <paramref name="delay"/> seconds.</summary>
    public void Light(float delay)
    {
        if (Lit) return;
        _fuse = delay;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        if (Lit)
        {
            _fuse -= dt;
            if (_fuse <= 0f) { Explode(); return; }
        }
        if (_t > Life) { QueueFree(); return; }
        var cave = G.Cave;
        // (it drifts up and a little to the side, and lies along the roof when it meets it)
        var np = GlobalPosition + new Vector2(MathF.Sin(_t * 0.7f + _sway) * 5f, -Tune.Sulphur.CloudRise) * dt;
        if (!cave.IsSolid(np + new Vector2(0, -Size * 0.5f))) GlobalPosition = np;
        var p = G.Player;
        if (!Lit && p != null && !p.Dead && _tick <= 0 && Density > 0.4f && p.GlobalPosition.DistanceTo(GlobalPosition) < Size + 4f)
        {
            _tick = Tune.Sulphur.TickEvery;
            p.GivePoison(p.Stats.MaxHp * Tune.Sulphur.PoisonShare + Tune.Sulphur.PoisonFlat * G.DepthDmg, Tune.Sulphur.PoisonSeconds);
        }
        if (G.Chance(0.12f)) G.Fx.Burst(GlobalPosition + G.RandDir() * Size * 0.7f, new Color(0.85f, 0.85f, 0.3f, 0.5f), 1, 10, 2f, 0.8f, -8);
    }

    /// <summary>The blast: a ball of fire the size of the cloud (a little over), hurting heroes and creatures in it and lighting the clouds beside it.</summary>
    private void Explode()
    {
        float r = Size * Tune.Sulphur.BlastRadiusMult + 6f;
        var at = GlobalPosition;
        G.Sfx.Play("lava", at, 2, 0.1f, 0.55f);
        G.Sfx.Play("rock", at, -4, 0.1f, 0.5f);
        G.Fx.Flash(at, r * 1.2f, new Color(1f, 0.7f, 0.25f), 0.3f);
        G.Fx.Burst(at, new Color(1f, 0.55f, 0.12f), 22, 200, 3f, 0.7f, -50f);
        G.Fx.Burst(at, new Color(0.35f, 0.3f, 0.2f, 0.6f), 8, 60, 6f, 1.1f, -20f);
        G.Fx.Shockwave(at, r, new Color(1f, 0.6f, 0.2f), 0.25f);
        G.Fx.AddShake(3.5f);
        var p = G.Player;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(at) < r + 6f)
        {
            p.Hurt(p.Stats.MaxHp * Tune.Sulphur.BlastShare + Tune.Sulphur.BlastFlat * G.DepthDmg, at, 220);
            if (!p.Dead) p.GiveBurn(p.Stats.MaxHp * Tune.Sulphur.BlastBurn + 2f * G.DepthDmg, Tune.Status.BurnSeconds);
        }
        foreach (var e in G.Enemies.ToArray())
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit || e is Gasbag || e.GlobalPosition.DistanceTo(at) > r + e.HitRadius) continue;
            var away = (e.GlobalPosition - at).Normalized();
            float dealt = e.Hurt(Math.Max(12f, e.MaxHp * 0.3f), away * 220f, at, DamageKind.Fire);
            if (dealt > 0 && !e.Dead && e.Element != Element.Fire) e.Ignite(Math.Max(2f, e.MaxHp * 0.04f), 3f);
        }
        foreach (var c in All.ToArray())
            if (c != this && IsInstanceValid(c) && !c.Lit && c.GlobalPosition.DistanceTo(at) < r + c.Size * 0.8f) c.Light(Tune.Sulphur.ChainDelay);
        foreach (var e in G.Enemies.ToArray())
            if (e is Gasbag gb && IsInstanceValid(gb) && !gb.Dead && gb.GlobalPosition.DistanceTo(at) < r + gb.HitRadius) gb.Pop(true);
        QueueFree();
    }
}

/// <summary>
/// A fissure in the floor of the Sulphur Springs: it rests, hisses and bubbles yellow for a moment (the warning), then blows gas for a
/// few seconds, a puff every half second, each puff a <see cref="GasCloud"/> that hangs where it lands.
/// </summary>
public partial class GasVent : Node2D
{
    private float _t, _phase, _puff;
    private bool _hissed;
    public static float CycleLength => Tune.Sulphur.VentIdle + Tune.Sulphur.VentWarn + Tune.Sulphur.VentBlow;
    /// <summary>For the 3D view: seconds into its rest - hiss - blow cycle.</summary>
    public float Cycle => (_t + _phase) % CycleLength;
    public bool Blowing => Cycle >= Tune.Sulphur.VentIdle + Tune.Sulphur.VentWarn;
    public bool Warning => Cycle >= Tune.Sulphur.VentIdle && !Blowing;

    public override void _Ready() { ZIndex = 2; _phase = G.Range(0, CycleLength); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _puff -= dt;
        var p = G.Player;
        if (p == null || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 760 * 760) return;
        if (Warning)
        {
            if (!_hissed) { _hissed = true; G.Sfx.Play("spider", GlobalPosition, -4, 0.2f, 0.45f); }
            if (G.Chance(0.35f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-6, 6), -2), new Color(0.9f, 0.9f, 0.3f, 0.6f), 1, 30, 1.8f, 0.6f, -20);
        }
        else if (Blowing)
        {
            if (_puff <= 0 && GasCloud.All.Count < 40)
            {
                _puff = Tune.Sulphur.PuffEvery;
                G.Spawn(new GasCloud { Position = GlobalPosition + new Vector2(G.Range(-8, 8), -16), Radius = Tune.Sulphur.CloudRadius * G.Range(0.8f, 1.1f), Life = Tune.Sulphur.CloudLife * G.Range(0.85f, 1.15f) });
                G.Sfx.Play("dodge", GlobalPosition, -8, 0.2f, 0.4f);
            }
        }
        else _hissed = false;
    }
}

/// <summary>A pool of acid left by an acid newt's glob: it burns whoever stands in it, and dries up.</summary>
public partial class AcidPuddle : Node2D
{
    public const float HalfW = 17f;
    public float Life = Tune.Sulphur.Newt.PuddleLife;
    public Enemy Source;
    private float _t, _tick;
    public float T => _t;
    public float LifeLeft => Life - _t;

    public override void _Ready() { ZIndex = 3; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        if (_t > Life) { QueueFree(); return; }
        var p = G.Player;
        if (p != null && !p.Dead && _tick <= 0)
        {
            var d = p.GlobalPosition - GlobalPosition;
            if (Math.Abs(d.X) < HalfW + 4 && d.Y > -22 && d.Y < 6)
            {
                _tick = 0.5f;
                p.Hurt(Tune.Sulphur.Newt.PuddleDamage * G.DepthDmg, GlobalPosition + new Vector2(0, 10), 100, Source);
            }
        }
        if (G.Chance(0.08f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-HalfW, HalfW), -2), new Color(0.75f, 0.9f, 0.2f, 0.7f), 1, 24, 1.6f, 0.5f, -50);
    }
}
