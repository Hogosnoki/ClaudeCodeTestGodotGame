using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Crystals of ice bursting out of the floor (the Swordsman's Comet and Stalagmite Vault, <c>Player/Player.Elemental.cs</c>). A Comet's
/// burst fans out from where he lands, the crystals leaning away from the middle; a Stalagmite Vault's are laid one by one under him as
/// he leaps back over the ground, each striking what stands over it as it bursts up. They stand a moment and melt away. The caster's
/// game deals the blows; the others' copies (<see cref="Harmless"/>) only show them (see <see cref="NetSync.HeroVisual"/>).
/// </summary>
public partial class IceCrystals : HeroSpell
{
    public struct Spike { public Vector2 Foot; public float Lean, Length, Width, Born; }
    public readonly List<Spike> Spikes = new();
    /// <summary>A burst fanning out from here (Comet); otherwise a trail laid under the caster for <see cref="Lay"/> s (Stalagmite Vault).</summary>
    public bool Burst;
    public float Half = 70f, Height = 40f, Stand = 1f, Lay = 0.45f, Every = 16f, Damage, Frost, Toss;
    public int Count = 12, Seed;
    public bool Harmless;
    public const float Rise = 0.1f, Melt = 0.45f;
    private readonly HashSet<Enemy> _struck = new();
    private float _lastX = float.NaN;
    private Random _rng;
    /// <summary>For the tests: the creatures a crystal has struck.</summary>
    public int Struck => _struck.Count;

    public override void _Ready()
    {
        base._Ready();
        _rng = new Random(Seed);
        if (!Burst) return;
        var c = GlobalPosition;
        for (int k = 0; k < Count; k++)
        {
            // -1..1 across the burst: tall in the middle, leaning out and lower toward the edges
            float u = Count == 1 ? 0f : k / (Count - 1f) * 2f - 1f;
            float x = c.X + u * Half * (0.85f + 0.15f * R());
            if (!Ground.FloorNear(x, c.Y, 24f, 30f, out float fy)) continue;
            float len = Height * (1.15f - 0.55f * Math.Abs(u)) * (0.8f + 0.4f * R());
            Spikes.Add(new Spike { Foot = new Vector2(x, fy + 2f), Lean = u * 0.85f + (R() - 0.5f) * 0.3f, Length = len, Width = len * 0.24f, Born = Math.Abs(u) * 0.08f + R() * 0.04f });
        }
    }

    private float R() => (float)_rng.NextDouble();

    /// <summary>0..1: how far up out of the floor a crystal stands (it bursts up overshooting, stands, and shrinks away as it melts).</summary>
    public float SpikeScale(int i)
    {
        var s = Spikes[i];
        float t = T - s.Born;
        if (t <= 0f) return 0f;
        if (t < Rise) { float k = t / Rise; return 1.15f * MathF.Sin(k * MathF.PI * 0.5f); }
        if (t < Rise + Stand) return 1f + 0.15f * MathF.Exp(-(t - Rise) * 12f);
        return Math.Clamp(1f - (t - Rise - Stand) / Melt, 0f, 1f);
    }

    protected override void Tick(float dt)
    {
        // (a trail: a stalagmite under him every so far he passes, while the leap carries him)
        if (!Burst && T < Lay && Caster != null && IsInstanceValid(Caster) && !Caster.Dead && Ground.Below(Caster.GlobalPosition + new Vector2(0, 6), 90f, out var floor)
            && (float.IsNaN(_lastX) || Math.Abs(floor.X - _lastX) >= Every))
        {
            _lastX = floor.X;
            float len = Height * (0.8f + 0.4f * R());
            Spikes.Add(new Spike { Foot = floor + new Vector2(0, 2f), Lean = (R() - 0.5f) * 0.35f, Length = len, Width = len * 0.26f, Born = T });
            var col = Elements.ColorOf(HeroElement.Frost);
            G.Fx.Directional(floor, Vector2.Up, 0.35f, new Color(0.92f, 0.98f, 1f), 6, 220, 2.4f, 0.35f, 300, 0);
            G.Fx.Burst(floor, new Color(col, 0.8f), 4, 60, 2f, 0.3f, 0, 0);
            G.Sfx.Play("clink", floor, -6, 0.1f, 0.6f + 0.2f * R());
        }
        bool any = !Burst && T < Lay;
        for (int i = 0; i < Spikes.Count; i++)
        {
            var s = Spikes[i];
            float t = T - s.Born;
            if (t < Rise + Stand + Melt) any = true;
            // (a glint now and then on the standing ice; meltwater dripping as it goes)
            if (t > Rise && t < Rise + Stand && G.Chance(0.03f)) G.Fx.Glint(s.Foot + new Vector2(0, -s.Length * G.Range(0.2f, 0.8f)), Elements.ColorOf(HeroElement.Frost), 4f);
            if (t > Rise + Stand && t < Rise + Stand + Melt && G.Chance(0.08f)) G.Fx.Burst(s.Foot + new Vector2(G.Range(-3f, 3f), -s.Length * SpikeScale(i) * 0.5f), new Color(0.75f, 0.9f, 1f, 0.7f), 1, 20, 1.6f, 0.4f, 300, 0);
            // the crystal strikes what stands over it as it bursts up
            if (Harmless || Damage <= 0f || t < 0f || t > Rise + 0.1f) continue;
            var dir = new Vector2(MathF.Sin(s.Lean), -MathF.Cos(s.Lean));
            foreach (var e in Foes())
            {
                if (_struck.Contains(e)) continue;
                var rel = e.GlobalPosition - s.Foot;
                float along = rel.Dot(dir), off = Math.Abs(rel.Cross(dir));
                if (along < -e.HitRadius || along > s.Length + e.HitRadius || off > s.Width + e.HitRadius) continue;
                _struck.Add(e);
                Hit(e, Damage, dir * 60f, DamageKind.Frost);
                if (Frost > 0f) Build(e, HeroElement.Frost, Frost);
                if (Toss > 0f && !e.Dead) e.Shove(new Vector2(dir.X * Toss * 0.4f, -Toss));
                if (!e.Dead) e.HitStop(Tune.Feel.HitStopBolt);
            }
        }
        if (!any) QueueFree();
    }
}

/// <summary>
/// Breaking Quake's cracks (the Swordsman's earth and fire Hard Swing): from where the blade is driven in, great gaping cracks full of magma
/// race out along the floor either way, and close again moments later. What stands over them as they open is burned (a full fire build-up)
/// and thrown up; the caster's game deals that, once, as they burst. The others' copies only show them.
/// </summary>
public partial class MagmaCracks : HeroSpell
{
    public float Half = 120f, Spread = 0.28f, Seconds = 1.6f, Damage, Toss = 240f;
    public bool Harmless;
    public int Seed;
    /// <summary>The floor either way from the middle (left, then right), every few px, as far as it runs (a wall or a drop stops a crack).</summary>
    public readonly List<Vector2> Left = new(), Right = new();
    public int Struck { get; private set; }
    private bool _burst;
    private float _fx;

    public override void _Ready()
    {
        base._Ready();
        var c = GlobalPosition;
        float y = c.Y;
        for (float x = c.X; x >= c.X - Half; x -= 6f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; Left.Add(new Vector2(x, fy)); }
        y = c.Y;
        for (float x = c.X; x <= c.X + Half; x += 6f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; Right.Add(new Vector2(x, fy)); }
        if (Left.Count == 0) Left.Add(c);
        if (Right.Count == 0) Right.Add(c);
    }

    /// <summary>0..1: how far out the cracks have run.</summary>
    public float Reach => W3.Smooth01(T / Spread);
    /// <summary>0..1: how open they are (they gape at once, and close up over the last half second).</summary>
    public float Open => Math.Clamp((Seconds - T) / 0.5f, 0f, 1f) * Math.Min(1f, T / 0.06f);

    protected override void Tick(float dt)
    {
        if (T >= Seconds) { QueueFree(); return; }
        if (!_burst) { _burst = true; Erupt(); }
        // (magma spitting up along the cracks as they run and stand; heat and smoke)
        if ((_fx -= dt) > 0) return;
        _fx = 0.03f;
        float open = Open;
        foreach (var side in new[] { Left, Right })
        {
            int n = Math.Max(1, (int)(side.Count * Reach));
            var p = side[G.RangeI(0, n - 1)];
            G.Fx.Directional(p, Vector2.Up, 0.35f, new Color(1f, G.Range(0.35f, 0.65f), 0.1f, open), 2, G.Range(140, 260), G.Range(2.2f, 3.4f), 0.45f, 420, 0);
            if (G.Chance(0.2f)) G.Fx.Smoke(p + new Vector2(0, -6), 1, new Color(0.3f, 0.26f, 0.24f, 0.4f * open), 30f);
            // the tip of each crack as it runs: a spray of magma and rock
            if (T < Spread) { var tip = side[n - 1]; G.Fx.Debris(tip, new Color(0.45f, 0.3f, 0.22f), 2, 160); G.Fx.Burst(tip, new Color(1f, 0.5f, 0.12f), 3, 120, 2.6f, 0.35f, 300, 0); }
        }
        if (G.Chance(0.3f)) G.Fx.LightUp(GlobalPosition + new Vector2(0, -8), new Color(1f, 0.45f, 0.15f), 1.6f * open, Half * 1.8f, 0.2f);
    }

    private void Erupt()
    {
        var c = GlobalPosition;
        G.Fx.Shockwave(c, Half, new Color(1f, 0.45f, 0.12f, 0.7f), 0.35f);
        G.Fx.Directional(c, Vector2.Up, 0.5f, new Color(1f, 0.55f, 0.15f), 20, 360, 3.2f, 0.5f, 420, 0);
        G.Fx.Debris(c, new Color(0.42f, 0.3f, 0.22f), 18, 300);
        G.Fx.AddShake(6f);
        G.Main.Rumble(0.7f, 0.9f, 0.3f);
        G.Sfx.Play("slam", c, 0, 0.05f, 0.7f);
        G.Sfx.Play("lava", c, -2, 0.05f, 0.8f);
        GasCloud.IgniteAt(c, Half);
        Breakables.SpellBurst(c, Half * 0.5f);
        if (Harmless) return;
        float x0 = Left[^1].X, x1 = Right[^1].X;
        foreach (var e in Foes())
        {
            if (!Ground.Over(e, x0, x1, c.Y + 10f, 46f + e.HitRadius)) continue;
            Struck++;
            Hit(e, Damage, new Vector2(Math.Sign(e.GlobalPosition.X - c.X) * 60f, -60f), DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.Elements.Full);
            if (!e.Dead) { e.Shove(new Vector2(Math.Sign(e.GlobalPosition.X - c.X) * Toss * 0.4f, -Toss)); e.HitStop(Tune.Feel.HitStopBolt * 1.5f); }
        }
    }
}

/// <summary>Blazing Charge's sphere of fire about the Swordsman (nearly opaque, roaring), while his charge lasts. Every game makes its own
/// from his state (<see cref="Player.BlazeOn"/>).</summary>
public partial class BlazeShell : Node2D
{
    public Player Hero;
    public float Age { get; private set; }

    public override void _Ready() => ZIndex = 3;

    public override void _PhysicsProcess(double delta)
    {
        Age += (float)delta;
        if (Hero == null || !IsInstanceValid(Hero) || !Hero.BlazeOn) { QueueFree(); return; }
        GlobalPosition = Hero.GlobalPosition + new Vector2(0, -4);
    }
}

/// <summary>Steam Vault's steam scalds: creatures in the cloud take a little fire and frost (and a little harm) each moment it hangs there.</summary>
public partial class SteamScald : HeroSpell
{
    public float Radius = 46f, Seconds = 4f;
    private float _tick;
    public int Scalded { get; private set; }

    protected override void Tick(float dt)
    {
        if (T >= Seconds) { QueueFree(); return; }
        if ((_tick -= dt) > 0) return;
        _tick = Tune.ElementalSword.ScaldTick;
        float dmg = Caster != null && IsInstanceValid(Caster) ? Caster.Stats.DamageMult * Tune.ElementalSword.ScaldDamage : 1f;
        foreach (var e in Foes())
        {
            if (e.GlobalPosition.DistanceTo(GlobalPosition) > Radius + e.HitRadius) continue;
            Scalded++;
            Hit(e, dmg, Vector2.Zero, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.ElementalSword.ScaldBuild);
            Build(e, HeroElement.Frost, Tune.ElementalSword.ScaldBuild);
            G.Fx.Burst(e.GlobalPosition + new Vector2(0, -e.HitRadius), new Color(0.92f, 0.95f, 1f, 0.7f), 2, 40, 2.4f, 0.4f, -40, 0);
        }
    }
}
