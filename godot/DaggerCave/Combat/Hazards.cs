using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Something the player's blade can strike that isn't a creature (webs, ice).</summary>
public interface IBreakable
{
    Vector2 HitCenter { get; }
    float HitSize { get; }
    void Strike(Vector2 from);
}

/// <summary>Registry of breakables for the swing to test against.</summary>
public static class Breakables
{
    public static readonly List<IBreakable> All = new();

    /// <summary>A spell sweeps from <paramref name="a"/> to <paramref name="b"/>: any ice sheet it crosses takes a touch, and a breakable
    /// platform (rock slab or ice) a blow. True if it touched one (a bolt stops there).</summary>
    public static bool Spell(Vector2 a, Vector2 b, float pad = 6f)
    {
        bool any = false;
        foreach (var br in All.ToArray())
        {
            if (br is IceSheet ice)
            {
                var at = Geometry2D.GetClosestPointToSegment(br.HitCenter, a, b);
                if (Math.Abs(at.X - br.HitCenter.X) <= ice.HitSize && Math.Abs(at.Y - br.HitCenter.Y) <= pad + 4f) any |= ice.Touch();
            }
            // the breakable platforms take a spell as they take a blow, so a caster is never shut in by one
            else if (br is RockLedge ledge && !ledge.Broken && Crosses(ledge.GlobalPosition, ledge.Half - 1f, ledge.ThickPx * 0.5f, a, b, pad * 0.5f)) { ledge.Strike(a); any = true; }
            else if (br is IcePlatform plat && !plat.Broken && Crosses(plat.GlobalPosition + new Vector2(0, 7), plat.HalfW, 7f, a, b, pad * 0.5f)) { plat.Strike(a); any = true; }
        }
        return any;
    }

    /// <summary>Does the segment a-b pass within <paramref name="pad"/> of the box (centre c, half sizes hx, hy)?</summary>
    private static bool Crosses(Vector2 c, float hx, float hy, Vector2 a, Vector2 b, float pad)
    {
        float len = a.DistanceTo(b);
        int n = Math.Max(2, (int)(len / 3f));
        for (int k = 0; k <= n; k++)
        {
            var q = a.Lerp(b, k / (float)n);
            if (Math.Abs(q.X - c.X) <= hx + pad && Math.Abs(q.Y - c.Y) <= hy + pad) return true;
        }
        return false;
    }

    /// <summary>A spell lands around <paramref name="at"/>: ice sheets within <paramref name="radius"/> take a touch.</summary>
    public static void SpellBurst(Vector2 at, float radius)
    {
        foreach (var br in All.ToArray())
        {
            if (br is IceSheet ice && Math.Abs(at.X - ice.HitCenter.X) <= ice.HitSize + radius && Math.Abs(at.Y - ice.HitCenter.Y) <= radius + 6f) ice.Touch();
            else if (br is RockLedge ledge && !ledge.Broken && Math.Abs(at.X - ledge.GlobalPosition.X) <= ledge.Half + radius && Math.Abs(at.Y - ledge.GlobalPosition.Y) <= ledge.ThickPx * 0.5f + radius) ledge.Strike(at);
            else if (br is IcePlatform plat && !plat.Broken && Math.Abs(at.X - plat.GlobalPosition.X) <= plat.HalfW + radius && Math.Abs(at.Y - plat.GlobalPosition.Y - 7f) <= 7f + radius) plat.Strike(at);
        }
    }

    /// <summary>Something flies from <paramref name="a"/> to <paramref name="b"/>: a web-hung chest's web in its way parts (only those: bolts and daggers leave ice and brush alone).</summary>
    public static void Shoot(Vector2 a, Vector2 b, float pad = 4f)
    {
        foreach (var br in All.ToArray())
        {
            if (br is not Chest c || !c.Hung || c.CutT >= 0) continue;
            var at = Geometry2D.GetClosestPointToSegment(br.HitCenter, a, b);
            if (at.DistanceTo(br.HitCenter) <= br.HitSize + pad) { c.Cut(); }
        }
    }
}

/// <summary>A choking cloud of spores: hurts a little every half second while you're inside.</summary>
public partial class SporeCloud : Node2D
{
    public float Radius = 36f, Life = 2.6f;
    public Enemy Source;
    private float _t, _tick;
    public float T => _t;

    public override void _Ready() { ZIndex = 3; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        if (_t > Life) { QueueFree(); return; }
        float r = Radius * Math.Min(1f, _t * 4f);
        // (a driven creature's cloud is the hero's: it chokes the creatures of the cave, not the hero)
        if (GodotObject.IsInstanceValid(Source) && Source.Master != null)
        {
            if (_tick <= 0)
            {
                _tick = 0.5f;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (!GodotObject.IsInstanceValid(e) || e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(GlobalPosition) >= r + 6 + e.HitRadius) continue;
                    Source.Master.FormBlow(e, Tune.Sporeling.CloudDamage, Vector2.Zero);
                    if (!e.Dead) e.Weaken(0.6f, 3f);
                }
            }
        }
        else
        {
            var p = G.Player;
            if (p != null && !p.Dead && _tick <= 0 && p.GlobalPosition.DistanceTo(GlobalPosition) < r + 6)
            {
                _tick = 0.5f;
                p.Hurt(Tune.Sporeling.CloudDamage * G.DepthDmg, GlobalPosition, 40, GodotObject.IsInstanceValid(Source) ? Source : null);
            }
        }
        if (G.Chance(0.3f)) G.Fx.Burst(GlobalPosition + G.RandDir() * r * 0.7f, new Color(0.75f, 0.55f, 0.95f, 0.6f), 1, 12, 2f, 0.8f, -10);
        GlobalPosition += new Vector2(0, -6 * dt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = Math.Clamp((Life - _t) / 0.6f, 0, 1) * Math.Min(1f, _t * 4f);
        float r = Radius * Math.Min(1f, _t * 4f);
        for (int k = 0; k < 6; k++)
        {
            var o = new Vector2(MathF.Cos(k * 1.1f + _t), MathF.Sin(k * 1.7f + _t * 1.3f)) * r * 0.45f;
            DrawCircle(o, r * 0.55f, new Color(0.62f, 0.42f, 0.85f, 0.12f * a));
        }
        DrawCircle(Vector2.Zero, r * 0.7f, new Color(0.8f, 0.6f, 1f, 0.1f * a));
    }
}

/// <summary>A fungus pod on the fungal cavern floor that bursts into a spore cloud when you come close.</summary>
public partial class SporePod : Node2D
{
    private float _cd, _t, _swell;
    /// <summary>For the 3D stage: how far it has swollen before it bursts, and whether it is ready to.</summary>
    public float Swell => _swell;
    public bool Primed => _cd <= 0;

    public override void _Ready() { ZIndex = 1; }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _cd -= dt;
        var p = G.Player;
        if (p == null || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 900 * 900) return;
        if (_swell > 0)
        {
            _swell += dt;
            if (_swell > 0.55f)
            {
                _swell = 0; _cd = 5f;
                G.Sfx.Play("dodge", GlobalPosition, -6, 0.2f, 0.5f);
                G.Spawn(new SporeCloud { Position = GlobalPosition + new Vector2(0, -14), Radius = 40, Life = 2.8f });
                G.Fx.Pop(GlobalPosition + new Vector2(0, -8), new Color(0.75f, 0.5f, 1f), 8);
            }
        }
        else if (_cd <= 0 && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition) < 56) _swell = 0.001f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s = 1f + (_swell > 0 ? _swell * 0.8f : 0.05f * MathF.Sin(_t * 2));
        float ready = _cd > 0 ? 0.5f : 1f;
        DrawSetTransform(Vector2.Zero, 0, new Vector2(s, s));
        DrawCircle(new Vector2(0, -6), 9, new Color(0.45f, 0.25f, 0.55f, ready));
        DrawCircle(new Vector2(-3, -9), 3, new Color(0.9f, 0.7f, 1f, 0.8f * ready));
        DrawCircle(new Vector2(4, -5), 2, new Color(0.9f, 0.7f, 1f, 0.8f * ready));
        DrawCircle(new Vector2(0, -6), 14, new Color(0.7f, 0.5f, 1f, 0.08f * ready));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>A sticky web strung across a nest passage: slows you to a crawl until you cut it.</summary>
public partial class WebPatch : Node2D, IBreakable
{
    public float Radius = 30f;
    private float _t;
    private int _hp = 1;
    public Vector2 HitCenter => GlobalPosition;
    public float HitSize => Radius;

    public override void _Ready() { ZIndex = -1; Breakables.All.Add(this); }
    public override void _ExitTree() => Breakables.All.Remove(this);

    public void Strike(Vector2 from)
    {
        if (--_hp > 0) return;
        NetSync.WebGone(GlobalPosition);
        Pop();
    }

    /// <summary>The web tears (here, or a friend's blade tore it in their game).</summary>
    public void Pop()
    {
        if (IsQueuedForDeletion()) return;
        G.Sfx.Play("web", GlobalPosition, -2);
        G.Fx.Burst(GlobalPosition, new Color(0.9f, 0.9f, 0.95f, 0.8f), 14, 120, 1.4f, 0.6f, 80, 1);
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        var p = G.Player;
        if (p != null && !p.Dead && p.GlobalPosition.DistanceTo(GlobalPosition) < Radius)
        {
            if (p.WebbedT <= 0) G.Sfx.Play("web", GlobalPosition, -10, 0.2f, 1.2f);
            p.WebbedT = 0.15f;
        }
    }

    public override void _Draw()
    {
        var col = new Color(0.92f, 0.92f, 0.96f, 0.55f);
        for (int k = 0; k < 8; k++)
        {
            var d = Vector2.Right.Rotated(k * Mathf.Tau / 8 + 0.2f) * Radius;
            DrawLine(Vector2.Zero, d, col, 1f);
        }
        for (int ring = 1; ring <= 4; ring++)
        {
            float r = Radius * ring / 4.4f;
            var pts = new Vector2[9];
            for (int k = 0; k <= 8; k++) pts[k] = Vector2.Right.Rotated(k * Mathf.Tau / 8 + 0.2f) * r * (k % 2 == 0 ? 1f : 0.92f);
            DrawPolyline(pts, col, 1f);
        }
    }
}

/// <summary>A cluster of crystal spikes: touching them hurts and throws you up.</summary>
public partial class CrystalSpikes : Node2D
{
    public float HalfW = 14f;
    private float _t;
    private readonly float[] _h = new float[5];
    public float[] Heights => _h;

    public override void _Ready()
    {
        ZIndex = 1;
        for (int k = 0; k < _h.Length; k++) _h[k] = G.Range(8, 16);
    }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        var p = G.Player;
        if (p == null || p.Dead) return;
        var d = p.GlobalPosition - GlobalPosition;
        if (Math.Abs(d.X) < HalfW + 5 && d.Y > -26 && d.Y < 4)
        {
            if (p.Hurt(p.Stats.MaxHp * 0.08f + 3 * G.DepthDmg, GlobalPosition + new Vector2(0, 10), 120) > 0)
            {
                p.Velocity = new Vector2(p.Velocity.X, -330);
                G.Fx.Glint(p.GlobalPosition, new Color(0.7f, 0.9f, 1f));
            }
        }
        if (G.Chance(0.02f)) G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-HalfW, HalfW), -G.Range(4, 14)), new Color(0.75f, 0.9f, 1f));
    }

    public override void _Draw()
    {
        for (int k = 0; k < _h.Length; k++)
        {
            float x = -HalfW + k * (HalfW * 2 / (_h.Length - 1));
            float h = _h[k];
            var tip = new Vector2(x + (k - 2) * 1.2f, -h);
            DrawColoredPolygon(new[] { new Vector2(x - 3.5f, 2), tip, new Vector2(x + 3.5f, 2) }, new Color(0.55f, 0.75f, 1f, 0.9f));
            DrawLine(new Vector2(x - 1, 1), tip, new Color(0.9f, 0.97f, 1f, 0.8f), 1f);
        }
        DrawCircle(new Vector2(0, -6), HalfW + 6, new Color(0.6f, 0.8f, 1f, 0.05f + 0.03f * MathF.Sin(_t * 2)));
    }
}

/// <summary>A fissure in the magma cavern floor that glows, rumbles and then erupts a column of fire.</summary>
public partial class FireVent : Node2D
{
    private float _t, _phase;
    public const float Idle = 3.2f, Warn = 0.9f, Burn = 1.3f;
    /// <summary>For the 3D stage: where it is in its idle - warn - burn cycle (seconds).</summary>
    public float Cycle => (_t + _phase) % (Idle + Warn + Burn);
    private float _tick;

    public override void _Ready() { ZIndex = 2; _phase = G.Range(0, Idle + Warn + Burn); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt; _tick -= dt;
        var p = G.Player;
        if (p == null || p.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 1100 * 1100) return;
        float c = (_t + _phase) % (Idle + Warn + Burn);
        if (c > Idle && c < Idle + Warn)
        {
            if (G.Chance(0.4f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-6, 6), -2), new Color(1f, 0.6f, 0.2f));
            if (G.Chance(0.1f)) G.Fx.Dust(GlobalPosition, 1, 0.5f, new Color(0.4f, 0.3f, 0.25f, 0.5f));
        }
        else if (c >= Idle + Warn)
        {
            if (c - dt < Idle + Warn) G.Sfx.Play("lava", GlobalPosition, 0, 0.1f, 0.5f);
            for (int k = 0; k < 2; k++) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), -G.Range(0, 70)), new Color(1f, 0.5f, 0.1f));
            var d = p.GlobalPosition - GlobalPosition;
            if (!p.Dead && _tick <= 0 && Math.Abs(d.X) < 14 && d.Y > -84 && d.Y < 6)
            {
                _tick = 0.45f;
                p.Hurt(p.Stats.MaxHp * 0.07f + 4 * G.DepthDmg, GlobalPosition + new Vector2(0, 20), 160);
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float c = (_t + _phase) % (Idle + Warn + Burn);
        DrawColoredPolygon(new[] { new Vector2(-9, 1), new Vector2(-3, -2), new Vector2(3, -1.5f), new Vector2(9, 1) }, new Color(0.15f, 0.04f, 0.02f));
        float glow = c > Idle ? Math.Min(1, (c - Idle) / Warn) : 0.15f;
        DrawCircle(new Vector2(0, -2), 8, new Color(1f, 0.45f, 0.1f, 0.25f * glow));
        if (c >= Idle + Warn)
        {
            float k = (c - Idle - Warn) / Burn;
            float h = 80 * MathF.Sin(Math.Min(1, k * 3) * Mathf.Pi / 2) * (k > 0.8f ? (1 - k) * 5 : 1);
            for (int i = 0; i < 4; i++)
            {
                float w = 11 - i * 2.5f;
                var col = i switch { 0 => new Color(0.9f, 0.25f, 0.05f, 0.5f), 1 => new Color(1f, 0.5f, 0.1f, 0.7f), 2 => new Color(1f, 0.8f, 0.3f, 0.8f), _ => new Color(1f, 1f, 0.8f, 0.9f) };
                float wob = MathF.Sin(_t * 30 + i) * 1.5f;
                DrawColoredPolygon(new[] { new Vector2(-w + wob, 0), new Vector2(-w * 0.4f - wob, -h * (1 - i * 0.12f)), new Vector2(w * 0.4f + wob, -h * (1 - i * 0.12f)), new Vector2(w - wob, 0) }, col);
            }
        }
    }
}

/// <summary>
/// A frozen stretch of the water's surface (frost caverns). You can walk on it; it takes two
/// blows to break, from above to get into the water, or from below to get out.
/// </summary>
public partial class IceSheet : StaticBody2D, IBreakable
{
    public float HalfW = 24f;
    private int _hp = 2;
    private float _flash;
    private int _touches;
    private double _touchAt = -1;
    /// <summary>Spells that touch it (a bolt, a storm's strike, a drain) before it gives way.</summary>
    public const int TouchesToBreak = 3;
    public bool Cracked => _hp < 2 || _touches > 0;
    public float FlashT => _flash;
    public Vector2 HitCenter => GlobalPosition;
    public float HitSize => HalfW;

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        ZIndex = 2;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(HalfW * 2, 8) }, Position = new Vector2(0, 2) });
        Breakables.All.Add(this);
    }

    public override void _ExitTree() => Breakables.All.Remove(this);

    public void Strike(Vector2 from) => Hit(--_hp);

    /// <summary>A spell touches the ice: the third touch breaks it (the same blast can't count twice).</summary>
    public bool Touch()
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _touchAt < 0.12) return false;
        _touchAt = now;
        _touches++;
        Hit(TouchesToBreak - _touches);
        return true;
    }

    private void Hit(int left)
    {
        _flash = 0.15f;
        G.Sfx.Play("clink", GlobalPosition, -2, 0.1f, 0.6f);
        G.Fx.Glint(GlobalPosition + new Vector2(G.Range(-HalfW, HalfW) * 0.6f, 0), new Color(0.8f, 0.95f, 1f));
        if (left > 0) { QueueRedraw(); return; }
        G.Sfx.Play("rock", GlobalPosition, 0, 0.1f, 1.6f);
        G.Fx.Debris(GlobalPosition, new Color(0.8f, 0.93f, 1f), 12, 200);
        G.Fx.Splash(GlobalPosition, 0.6f, new Color(0.7f, 0.9f, 1f, 0.9f));
        QueueFree();
    }

    public override void _Process(double delta) { if (_flash > 0) { _flash -= (float)delta; QueueRedraw(); } }

    public override void _Draw()
    {
        var body = _flash > 0 ? new Color(1, 1, 1, 0.95f) : new Color(0.72f, 0.88f, 0.98f, 0.85f);
        DrawRect(new Rect2(-HalfW, -2, HalfW * 2, 8), body);
        DrawRect(new Rect2(-HalfW, -2, HalfW * 2, 2), new Color(0.95f, 1f, 1f, 0.9f));
        if (Cracked)
        {
            DrawLine(new Vector2(-6, -2), new Vector2(2, 3), new Color(0.35f, 0.55f, 0.7f), 1.2f);
            DrawLine(new Vector2(2, 3), new Vector2(9, 0), new Color(0.35f, 0.55f, 0.7f), 1.2f);
            DrawLine(new Vector2(2, 3), new Vector2(0, 6), new Color(0.35f, 0.55f, 0.7f), 1.2f);
        }
    }
}

/// <summary>
/// A frozen ledge (frost caverns): stand on it, but after a few landings or two blows it
/// shatters, and freezes back a while later.
/// </summary>
public partial class IcePlatform : StaticBody2D, IBreakable
{
    public float HalfW = 32f;
    private int _landings, _hits;
    private bool _broken, _playerOn;
    private float _respawn, _crack, _flash;
    public bool Broken => _broken;
    public int Cracks => _landings + _hits;
    public float FlashT => _flash;
    public float Shaking => _crack;
    private CollisionShape2D _shape;
    public Vector2 HitCenter => GlobalPosition + new Vector2(0, 6);
    public float HitSize => _broken ? 0 : HalfW;

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        ZIndex = 1;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(HalfW * 2, 14) }, Position = new Vector2(0, 7), OneWayCollision = true };
        AddChild(_shape);
        Breakables.All.Add(this);
    }

    public override void _ExitTree() => Breakables.All.Remove(this);

    public void Strike(Vector2 from)
    {
        if (_broken) return;
        _hits++;
        _flash = 0.12f;
        G.Sfx.Play("clink", GlobalPosition, -4, 0.1f, 0.7f);
        if (_hits >= 2) Shatter();
    }

    private void Shatter()
    {
        _broken = true;
        _respawn = 10f;
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        G.Sfx.Play("rock", GlobalPosition, -2, 0.1f, 1.7f);
        G.Fx.Debris(GlobalPosition + new Vector2(0, 6), new Color(0.8f, 0.93f, 1f), 14, 160);
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _flash -= dt;
        var p = G.Player;
        if (_broken)
        {
            _respawn -= dt;
            if (_respawn <= 0 && (p == null || p.GlobalPosition.DistanceTo(GlobalPosition) > HalfW + 30))
            {
                _broken = false; _hits = 0; _landings = 0; _crack = 0;
                _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
                G.Fx.Glint(GlobalPosition, new Color(0.85f, 0.95f, 1f), 12);
            }
            QueueRedraw();
            return;
        }
        bool on = p != null && !p.Dead && p.IsOnFloor() && Math.Abs(p.GlobalPosition.X - GlobalPosition.X) < HalfW + 4 && Math.Abs(p.GlobalPosition.Y + 13 - GlobalPosition.Y) < 6;
        if (on && !_playerOn)
        {
            _landings++;
            _crack = 0.3f;
            G.Sfx.Play("clink", GlobalPosition, -10, 0.2f, 0.5f);
            if (_landings >= 3) Shatter();
        }
        _playerOn = on;
        _crack -= dt;
        if (_flash > 0 || _crack > 0) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_broken) return;
        float shake = _crack > 0 ? G.Range(-1, 1) : 0;
        var body = _flash > 0 ? Colors.White : new Color(0.7f, 0.87f, 0.98f, 0.9f);
        DrawColoredPolygon(new[] { new Vector2(-HalfW + shake, 0), new Vector2(HalfW + shake, 0), new Vector2(HalfW - 6 + shake, 10), new Vector2(-HalfW + 6 + shake, 10) }, body);
        DrawLine(new Vector2(-HalfW + shake, 0.5f), new Vector2(HalfW + shake, 0.5f), new Color(1, 1, 1, 0.9f), 1.5f);
        for (int k = 0; k < _landings + _hits; k++)
            DrawLine(new Vector2(-HalfW * 0.6f + k * 14, 1), new Vector2(-HalfW * 0.6f + k * 14 + 5, 8), new Color(0.3f, 0.5f, 0.7f), 1.2f);
    }
}

/// <summary>
/// Grasping roots (the root-choked tunnels): a knot of roots on the floor that slows whoever
/// wades through it, and if it holds them long enough, catches their weapon for a moment. Two
/// cuts clear it.
/// </summary>
public partial class GraspingRoots : Node2D, IBreakable
{
    public float Radius = 26f;
    private float _t, _grip, _regrip;
    private int _cuts = Tune.Roots.CutsToClear;
    public Vector2 HitCenter => GlobalPosition + new Vector2(0, -8);
    public float HitSize => Radius;
    /// <summary>For the 3D view: 0..1, how tightly it is closing on someone (it writhes harder).</summary>
    public float Grip => Math.Clamp(_grip / Tune.Roots.GripToSnag, 0f, 1f);
    public float Age => _t;
    /// <summary>For the 3D view: cut once (it sags).</summary>
    public bool Wounded => _cuts < Tune.Roots.CutsToClear;

    public override void _Ready() { ZIndex = -1; Breakables.All.Add(this); }
    public override void _ExitTree() => Breakables.All.Remove(this);

    public void Strike(Vector2 from)
    {
        G.Sfx.Play("web", GlobalPosition, -4, 0.1f, 0.7f);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -8), new Color(0.45f, 0.33f, 0.2f, 0.9f), 10, 130, 1.8f, 0.5f, 300, 10);
        _grip = 0;
        if (--_cuts > 0) return;
        G.Fx.Burst(GlobalPosition, new Color(0.35f, 0.26f, 0.16f), 14, 170, 2.2f, 0.6f, 400, 10);
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _regrip -= dt;
        var p = G.Player;
        bool held = p != null && !p.Dead && Math.Abs(p.GlobalPosition.X - GlobalPosition.X) < Radius && p.GlobalPosition.Y > GlobalPosition.Y - 30 && p.GlobalPosition.Y < GlobalPosition.Y + 6;
        if (held)
        {
            if (p.WebbedT <= 0) G.Sfx.Play("web", GlobalPosition, -12, 0.2f, 0.6f);
            p.WebbedT = 0.15f;
            if (_regrip <= 0)
            {
                _grip += dt;
                if (_grip >= Tune.Roots.GripToSnag)
                {
                    p.Snag(Tune.Roots.SnagSeconds);
                    _grip = 0;
                    _regrip = Tune.Roots.Regrip;
                }
            }
        }
        else _grip = Math.Max(0, _grip - dt * 0.8f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var col = new Color(0.36f, 0.26f, 0.15f, 0.9f);
        for (int k = 0; k < 7; k++)
        {
            float x = -Radius + k * Radius / 3f;
            float h = 10 + 6 * MathF.Sin(_t * (2f + Grip * 6f) + k * 1.7f);
            DrawLine(new Vector2(x, 0), new Vector2(x + 3 * MathF.Sin(_t * 1.5f + k), -h), col, 2f);
        }
    }
}

/// <summary>
/// An unstable stretch of ceiling (the fossil graveyards): when someone walks beneath it, dust
/// sifts down and the rock groans, then a few stones break loose (each one telegraphed where it
/// will land). Then it settles for a while.
/// </summary>
public partial class CaveIn : Node2D
{
    /// <summary>How far down the floor under it is (px), set when placed.</summary>
    public float Drop = 200f;
    private float _t, _rumble = -1f, _rest;
    public float Age => _t;
    /// <summary>For the 3D view: 0..1 while it is about to give way.</summary>
    public float Rumbling => _rumble < 0 ? 0f : 1f - _rumble / Tune.CaveIn.Rumble;

    public override void _Ready() { ZIndex = 2; _rest = G.Range(0.5f, 2f); }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        var p = G.Player;
        if (_rumble >= 0)
        {
            _rumble -= dt;
            if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-Tune.CaveIn.Reach, Tune.CaveIn.Reach), 4), new Color(0.62f, 0.57f, 0.5f, 0.7f), 1, 20, 1.6f, 0.7f, 300);
            if (_rumble < 0) Collapse();
            return;
        }
        _rest -= dt;
        if (_rest > 0 || p == null || p.Dead) return;
        var to = p.GlobalPosition - GlobalPosition;
        if (Math.Abs(to.X) < Tune.CaveIn.Reach + 20 && to.Y > 0 && to.Y < Drop + 20)
        {
            _rumble = Tune.CaveIn.Rumble;
            G.Sfx.Play("rock", GlobalPosition, -6, 0.1f, 0.5f);
            G.Sfx.Play("roar", GlobalPosition, -18, 0.1f, 0.4f);
            G.Fx.AddShake(2.5f);
            G.Main.Rumble(0.2f, 0.4f, 0.5f);
        }
    }

    private void Collapse()
    {
        int n = G.RangeI(Tune.CaveIn.RocksMin, Tune.CaveIn.RocksMax);
        for (int k = 0; k < n; k++)
        {
            var at = GlobalPosition + new Vector2(G.Range(-Tune.CaveIn.Reach, Tune.CaveIn.Reach), 6);
            if (G.Cave.IsSolid(at)) continue;
            G.Spawn(new FallingRock { Position = at, Damage = Tune.CaveIn.Damage * G.DepthDmg });
        }
        G.Fx.AddShake(4f);
        G.Sfx.Play("slam", GlobalPosition, -8, 0.1f, 0.6f);
        _rest = G.Range(Tune.CaveIn.RestMin, Tune.CaveIn.RestMax);
    }

    public override void _Draw()
    {
        // cracks in the ceiling, widening as it groans
        var col = new Color(0.15f, 0.12f, 0.1f, 0.8f);
        float w = 1f + 2f * Rumbling;
        DrawLine(new Vector2(-Tune.CaveIn.Reach * 0.7f, 0), new Vector2(0, 5), col, w);
        DrawLine(new Vector2(0, 5), new Vector2(Tune.CaveIn.Reach * 0.6f, 1), col, w);
    }
}
