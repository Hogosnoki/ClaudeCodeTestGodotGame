using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The dual-charge abilities that outlive the moment they're cast (<see cref="Tune.Dual"/>, <c>Player/Player.Duals.cs</c>): a fissure
/// running along the floor, a wall of fire, a pool of magma, ice bursting up under a creature, a meteor, a frozen orb, a tornado, a
/// wildfire and a whiteout. Each lives in its caster's game, which deals its blows; what it looks like is made of particles, which
/// reach the other games as the caster's own effects do (see <see cref="NetSync.Scope"/>).
/// </summary>
public abstract partial class HeroSpell : Node2D
{
    public Player Caster;
    /// <summary>The caster's damage multiplier when it was cast.</summary>
    public float Power = 1f;
    protected float T;

    public override void _Ready() => ZIndex = 2;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        T += dt;
        NetSync.Scope++;
        try { Tick(dt); }
        finally { NetSync.Scope--; }
        // (ending of itself, it dissipates in its element's way: a flame gutters out, ice sublimes, a gust dies, rubble trickles)
        if (!_gone && IsQueuedForDeletion() && T > 0.25f && SoundEl != HeroElement.None)
        {
            _gone = true;
            G.Sfx.Element(ElementSounds.Ev.Fizzle, SoundEl, GlobalPosition, GetType().Name, weight: 1.2f);
        }
    }
    private bool _gone;
    private double _hitSoundAt = -10;

    /// <summary>The spell's element, for its sounds (each kind of spell its own variant of it); none: it makes none of its own.</summary>
    protected HeroElement SoundEl => this switch
    {
        FireTrail or FireWall or MagmaPool or MagmaCracks or FlameBreath or FireRing => HeroElement.Fire,
        Meteor m => m.Frost ? HeroElement.Frost : HeroElement.Fire,
        IceCrystals or IceEruption or TundraField => HeroElement.Frost,
        Fissure => HeroElement.Earth,
        Tornado or StrongGust => HeroElement.Wind,
        SteamScald or Downpour => HeroElement.Water,
        _ => HeroElement.None,
    };

    protected abstract void Tick(float dt);

    /// <summary>A blow from the spell, credited to its caster.</summary>
    protected float Hit(Enemy e, float dmg, Vector2 knock, DamageKind kind)
    {
        if (e.Dead || !e.CanBeHit) return 0f;
        float dealt = e.Hurt(dmg, knock, e.GlobalPosition, kind);
        // (its own sound of striking, not every tick of a lasting one: a few times a second at most)
        double now = Time.GetTicksMsec() / 1000.0;
        if (dealt > 0 && SoundEl != HeroElement.None && now - _hitSoundAt > 0.22)
        {
            _hitSoundAt = now;
            G.Sfx.Element(ElementSounds.Ev.Hit, SoundEl, e.GlobalPosition, GetType().Name, volDb: dmg < 3f ? -6f : 0f);
        }
        if (dealt > 0 && Caster != null && IsInstanceValid(Caster)) { Caster.OnDealtDamage(dealt); Caster.AttributeHit(e, GlobalPosition); }
        return dealt;
    }

    protected void Build(Enemy e, HeroElement el, float amount)
    {
        if (!e.Dead) e.Build(el, amount, Power, Caster != null && IsInstanceValid(Caster) ? Caster : null);
    }

    /// <summary>The creatures that can be struck now.</summary>
    protected static IEnumerable<Enemy> Foes() => G.Enemies.ToArray().Where(e => !e.Dead && e.CanBeHit);
}

/// <summary>Finding the floor for the spells that run along it: cave rock, or a platform still standing.</summary>
public static class Ground
{
    public static bool Solid(Vector2 p)
    {
        if (G.Cave.IsSolid(p)) return true;
        foreach (var l in RockLedge.All)
            if (GodotObject.IsInstanceValid(l) && !l.Broken && Math.Abs(p.X - l.GlobalPosition.X) <= l.Half && Math.Abs(p.Y - l.GlobalPosition.Y) <= l.ThickPx * 0.5f) return true;
        foreach (var br in Breakables.All)
            if (br is IcePlatform plat && !plat.Broken && Math.Abs(p.X - plat.GlobalPosition.X) <= plat.HalfW && p.Y >= plat.GlobalPosition.Y && p.Y <= plat.GlobalPosition.Y + 14f) return true;
        return false;
    }

    /// <summary>The floor at <paramref name="x"/> near height <paramref name="y"/>: looking down from <paramref name="up"/> above to
    /// <paramref name="down"/> below. False if rock fills the top (a wall) or nothing is found (a drop).</summary>
    public static bool FloorNear(float x, float y, float up, float down, out float floorY)
    {
        floorY = y;
        if (Solid(new Vector2(x, y - up))) return false;
        for (float d = -up; d <= down; d += 3f)
            if (Solid(new Vector2(x, y + d))) { floorY = y + d; return true; }
        return false;
    }

    /// <summary>The floor under <paramref name="p"/> (within <paramref name="maxDown"/>), or false.</summary>
    public static bool Below(Vector2 p, float maxDown, out Vector2 floor)
    {
        for (float d = 0; d <= maxDown; d += 3f)
            if (Solid(p + new Vector2(0, d))) { floor = p + new Vector2(0, d); return true; }
        floor = p;
        return false;
    }

    /// <summary>A creature is standing (or low) over this stretch of floor.</summary>
    public static bool Over(Enemy e, float x0, float x1, float floorY, float height)
        => e.GlobalPosition.X + e.HitRadius >= x0 && e.GlobalPosition.X - e.HitRadius <= x1
           && e.GlobalPosition.Y - e.HitRadius <= floorY + 6f && e.GlobalPosition.Y + e.HitRadius >= floorY - height;
}

// ====================================================================== the Swordsman

/// <summary>Earthsplitter (earth+earth): a crack runs along the floor from where the blade struck, rock bursting up behind its head;
/// whatever it passes under is tossed up and stunned, and a rubble wall in its way comes down.</summary>
public partial class Fissure : HeroSpell
{
    public float Dir = 1f, Range = Tune.Dual.SplitRange, Speed = Tune.Dual.SplitSpeed, Damage, Toss = Tune.Dual.SplitToss;
    /// <summary>The Earthsplitter's (not a Fault Strike's): the ground quakes as it runs, rock flies higher, and what it strikes is held a beat.</summary>
    public bool Dramatic;
    private float _gone, _x, _floorY, _fx;
    private readonly HashSet<Enemy> _hit = new();
    public int Struck => _hit.Count;

    public override void _Ready()
    {
        base._Ready();
        _x = GlobalPosition.X;
        _floorY = GlobalPosition.Y;
    }

    protected override void Tick(float dt)
    {
        var col = Elements.ColorOf(HeroElement.Earth);
        float move = Speed * dt;
        while (move > 0)
        {
            float step = Math.Min(6f, move);
            move -= step;
            float nx = _x + Dir * step;
            if (!Ground.FloorNear(nx, _floorY, 22f, 30f, out float fy)) { End(col); return; }
            var a = new Vector2(_x, _floorY - 12f);
            _x = nx; _floorY = fy; _gone += step;
            var b = new Vector2(_x, _floorY - 12f);
            foreach (var rb in Rubble.All.ToArray())
                if (IsInstanceValid(rb) && !rb.Cleared && Breakables.SegHitsBox(a, b, rb.GlobalPosition, rb.Size * 0.5f)) rb.Smash();
            Breakables.Spell(a, b, 3f);
            foreach (var e in Foes())
            {
                if (_hit.Contains(e) || !Ground.Over(e, _x - 10f, _x + 10f, _floorY, 46f)) continue;
                _hit.Add(e);
                Hit(e, Damage, new Vector2(Dir * 60f, -40f), DamageKind.Physical);
                Build(e, HeroElement.Earth, Tune.Elements.Full);
                e.Shove(new Vector2(Dir * 70f, -Toss));
                G.Fx.Directional(e.GlobalPosition + new Vector2(0, e.HitRadius), Vector2.Up, 0.35f, col, 8, 260, 3f, 0.45f, 500, 0);
                if (Dramatic && !e.Dead) { e.HitStop(Tune.Dual.SplitHitStop); G.Fx.Debris(e.GlobalPosition + new Vector2(0, e.HitRadius), col, 10, 320); G.Fx.AddShake(3f); }
            }
            // rock bursting up behind the crack's head
            _fx -= step;
            if (_fx <= 0)
            {
                _fx = 9f;
                var at = new Vector2(_x, _floorY);
                G.Fx.Directional(at, Vector2.Up, 0.45f, G.Chance(0.5f) ? col : col.Darkened(0.35f), Dramatic ? 6 : 3, G.Range(160, Dramatic ? 420 : 280), Dramatic ? 3.6f : 2.8f, 0.45f, 520, 0);
                G.Fx.Dust(at, 2, 1.2f, new Color(0.55f, 0.46f, 0.36f, 0.6f));
                if (Dramatic) { G.Fx.Debris(at, col.Darkened(0.2f), 3, 300); G.Fx.AddShake(1.4f); }
            }
            if (_gone >= Range) { End(col); return; }
        }
    }

    private void End(Color col)
    {
        var at = new Vector2(_x, _floorY);
        G.Fx.Debris(at, col.Darkened(0.2f), 12, 240);
        G.Fx.Shockwave(at, 28, new Color(col, 0.6f), 0.3f);
        G.Sfx.Play("rock", at, -4, 0.1f, 0.7f);
        QueueFree();
    }
}

/// <summary>Inferno Cleave (fire+fire): a wall of fire stands where the blade struck. Creatures in it burn, and the shots that fly
/// into it burn up; gas in it goes up.</summary>
public partial class FireWall : HeroSpell
{
    public float Seconds = Tune.Dual.WallSeconds, Height = Tune.Dual.WallHeight, Half = Tune.Dual.WallHalf, TickDamage;
    private float _tick, _light;
    public int Burned { get; private set; }

    protected override void Tick(float dt)
    {
        if (T >= Seconds) { QueueFree(); return; }
        var c = GlobalPosition;
        float fade = Math.Clamp((Seconds - T) / 0.5f, 0f, 1f) * Math.Clamp(T / 0.15f, 0f, 1f);
        if ((_tick -= dt) <= 0)
        {
            _tick = Tune.Dual.WallTick;
            GasCloud.IgniteAt(c + new Vector2(0, -Height * 0.5f), Height * 0.5f);
            foreach (var e in Foes())
            {
                if (!Ground.Over(e, c.X - Half, c.X + Half, c.Y, Height)) continue;
                Hit(e, TickDamage, Vector2.Zero, DamageKind.Fire);
                Build(e, HeroElement.Fire, Tune.Dual.WallBuild);
            }
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
        {
            if (!IsInstanceValid(pr)) continue;
            var p = pr.GlobalPosition;
            if (Math.Abs(p.X - c.X) <= Half + pr.Radius && p.Y <= c.Y + 4f && p.Y >= c.Y - Height) { Burned++; pr.Deflect(); }
        }
        // the flames: tongues licking up the wall, sparks off the top
        for (int k = 0; k < 3; k++)
        {
            var at = c + new Vector2(G.Range(-Half, Half), -G.Range(0f, Height * 0.35f));
            var tone = G.Chance(0.5f) ? new Color(1f, 0.45f, 0.1f, 0.9f * fade) : new Color(1f, 0.78f, 0.3f, 0.9f * fade);
            G.Fx.Directional(at, Vector2.Up, 0.18f, tone, 1, G.Range(120, 220), G.Range(2.4f, 3.6f), G.Range(0.3f, 0.5f) * Height / 84f, -60, 0);
        }
        if (G.Chance(0.3f)) G.Fx.Ember(c + new Vector2(G.Range(-Half, Half), -Height * G.Range(0.6f, 1f)), new Color(1f, 0.7f, 0.3f));
        if ((_light -= dt) <= 0) { _light = 0.25f; G.Fx.LightUp(c + new Vector2(0, -Height * 0.5f), new Color(1f, 0.55f, 0.2f), 1.6f * fade, Height * 1.4f, 0.3f); }
    }
}

/// <summary>Magma Rend (earth+fire): the floor splits and magma wells up along it. Creatures in it are mired (slower in all they do)
/// and burn.</summary>
public partial class MagmaPool : HeroSpell
{
    public float Seconds = Tune.Dual.PoolSeconds, Half = Tune.Dual.PoolHalf, TickDamage;
    private float _x0, _x1, _tick;
    private readonly List<Vector2> _surface = new();
    public int Mired { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        // the pool spreads along the floor each way until a wall or a drop stops it
        var c = GlobalPosition;
        _x0 = _x1 = c.X;
        float y = c.Y;
        for (float x = c.X; x >= c.X - Half; x -= 6f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; _x0 = x; _surface.Add(new Vector2(x, fy)); }
        y = c.Y;
        for (float x = c.X + 6f; x <= c.X + Half; x += 6f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; _x1 = x; _surface.Add(new Vector2(x, fy)); }
        if (_surface.Count == 0) _surface.Add(c);
        var col = new Color(1f, 0.45f, 0.1f);
        foreach (var p in _surface) if (G.Chance(0.5f)) G.Fx.Directional(p, Vector2.Up, 0.5f, col, 2, 200, 3f, 0.5f, 400, 0);
    }

    protected override void Tick(float dt)
    {
        if (T >= Seconds) { QueueFree(); return; }
        float fade = Math.Clamp((Seconds - T) / 0.6f, 0f, 1f);
        if ((_tick -= dt) <= 0)
        {
            _tick = Tune.Dual.PoolTick;
            float y = GlobalPosition.Y;
            foreach (var e in Foes())
            {
                if (!Ground.Over(e, _x0, _x1, y + 10f, 30f + e.HitRadius)) continue;
                Mired++;
                e.Mire(Tune.Dual.PoolTick + 0.4f);
                Hit(e, TickDamage, Vector2.Zero, DamageKind.Fire);
                Build(e, HeroElement.Fire, Tune.Dual.PoolBuild);
            }
            GasCloud.IgniteAt(GlobalPosition, Half);
        }
        // the magma: a glowing crust along the floor, bubbles bursting, heat rising
        var p = _surface[G.RangeI(0, _surface.Count - 1)];
        G.Fx.Burst(p + new Vector2(G.Range(-3f, 3f), -1f), new Color(1f, G.Range(0.3f, 0.55f), 0.08f, 0.95f * fade), 1, 30, G.Range(3f, 4.5f), 0.4f, 60, 0);
        if (G.Chance(0.25f)) G.Fx.Directional(p, Vector2.Up, 0.3f, new Color(1f, 0.6f, 0.2f, fade), 2, 150, 2.2f, 0.45f, 380, 0);
        if (G.Chance(0.08f)) G.Fx.Smoke(p + new Vector2(0, -6), 1, new Color(0.35f, 0.3f, 0.28f, 0.4f * fade), 30f);
        if (G.Chance(0.12f)) G.Fx.LightUp(GlobalPosition + new Vector2(0, -8), new Color(1f, 0.45f, 0.15f), 1.2f * fade, Half * 1.6f, 0.3f);
    }
}

/// <summary>Frost Heave (earth+frost): the ground under a creature cracks with frost, then bursts up in a column of ice. Whatever is over
/// it takes a great blow of frost, is filled with it, and is thrown up.</summary>
public partial class IceEruption : HeroSpell
{
    public float Warn = Tune.Dual.HeaveWarn, Half = Tune.Dual.HeaveHalf, Height = Tune.Dual.HeaveHeight, Damage, Toss = Tune.Dual.HeaveToss;
    private bool _burst;
    private float _fx;
    public int Struck { get; private set; }

    protected override void Tick(float dt)
    {
        var c = GlobalPosition;
        var col = Elements.ColorOf(HeroElement.Frost);
        if (!_burst)
        {
            // the warning: frost spreading out over the floor, the ground shaking
            if ((_fx -= dt) <= 0)
            {
                _fx = 0.05f;
                G.Fx.Directional(c + new Vector2(G.Range(-Half, Half), 0), Vector2.Up, 0.6f, new Color(col, 0.8f), 1, 50, 2f, 0.35f, 0, 0);
            }
            if (T < Warn) return;
            _burst = true;
            Erupt(c, col);
            return;
        }
        // the ice stands a moment, glinting, then goes
        if (G.Chance(0.2f)) G.Fx.Glint(c + new Vector2(G.Range(-Half, Half) * 0.6f, -G.Range(0f, Height * 0.7f)), col, 5f);
        if (T > Warn + 1.1f)
        {
            G.Fx.Burst(c + new Vector2(0, -Height * 0.3f), new Color(0.9f, 0.97f, 1f), 14, 120, 2.2f, 0.4f, 300);
            QueueFree();
        }
    }

    private void Erupt(Vector2 c, Color col)
    {
        G.Fx.Ring(c, Half + 12, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Directional(c, Vector2.Up, 0.22f, new Color(0.92f, 0.98f, 1f), 26, 520, 3.4f, 0.5f, 300, 0);
        G.Fx.Directional(c, Vector2.Up, 0.7f, col, 14, 300, 2.6f, 0.45f, 420, 0);
        G.Fx.Shockwave(c, Half + 30, new Color(col, 0.6f), 0.3f);
        G.Fx.LightUp(c + new Vector2(0, -Height * 0.4f), col, 2.4f, Height * 1.5f, 0.45f);
        G.Fx.AddShake(4f);
        G.Sfx.Play("clink", c, 0, 0.05f, 0.45f);
        G.Sfx.Play("slam", c, -4, 0.05f, 0.9f);
        G.Main.Rumble(0.5f, 0.7f, 0.2f);
        Breakables.SpellBurst(c, Half);
        Breakables.Spell(c + new Vector2(0, -4), c + new Vector2(0, -Height), Half * 0.6f);
        foreach (var e in Foes())
        {
            if (!Ground.Over(e, c.X - Half, c.X + Half, c.Y, Height)) continue;
            Struck++;
            Hit(e, Damage, new Vector2(0, -80f), DamageKind.Frost);
            Build(e, HeroElement.Frost, Tune.Elements.Full);
            e.Shove(new Vector2(0, -Toss));
        }
    }
}

// ====================================================================== the Elementalist

/// <summary>Meteorite (fire+fire, the bolt): a burning rock falls out of the dark on the thickest knot of creatures. Every platform it passes
/// through breaks (they grow back), and where it meets the rock it bursts, setting everything about it alight. Cryometeor (frost+frost): a
/// rock of ice that falls the same way, but bursts in frost (frozen solid) and leaves the ground about it frozen. Cast standing, it falls
/// through any rock above the caster's feet (ceilings, overhangs, walls it clips) and bursts only on ground as low as the caster stands or
/// lower; cast in the air, on any ground it meets.</summary>
public partial class Meteor : HeroSpell
{
    public Vector2 Target;
    public float Speed = Tune.Dual.MeteorSpeed, Radius = Tune.Dual.MeteorRadius, Damage;
    /// <summary>A Cryometeor (ice, not fire).</summary>
    public bool Frost;
    /// <summary>The height of the ground its caster stood on (rock above it is passed through); null: cast in the air, any ground stops it.</summary>
    public float? GroundY;
    /// <summary>For the tests: where it burst.</summary>
    public Vector2 LandedAt { get; private set; }
    private Color Col => Frost ? Elements.ColorOf(HeroElement.Frost) : new Color(1f, 0.5f, 0.15f);
    private Vector2 _dir;
    private float _gone, _max, _fx;
    public int Smashed { get; private set; }
    public bool Landed { get; private set; }
    /// <summary>For the tests: every creature its burst struck.</summary>
    public int Struck { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        ZIndex = 4;
        _dir = (Target - GlobalPosition).Normalized();
        _max = GlobalPosition.DistanceTo(Target) * 1.8f + 120f;
        // (standing, far enough to come down to the caster's ground and a way below it)
        if (GroundY is float g && _dir.Y > 0.1f) _max = Math.Max(_max, (g - GlobalPosition.Y) / _dir.Y + 200f);
        G.Sfx.Play(Frost ? "clink" : "lava", Target, -6, 0.05f, 0.55f);
        G.Fx.Ring(Target, Radius * 0.5f, new Color(Col, 0.7f), 0.5f, FxLayer.RingStyle.Dashed);
    }

    protected override void Tick(float dt)
    {
        float move = Speed * dt;
        var col = Col;
        while (move > 0)
        {
            float step = Math.Min(8f, move);
            move -= step;
            var next = GlobalPosition + _dir * step;
            _gone += step;
            // it goes through whatever platform it meets, and on
            Smashed += Breakables.Smash(next, 14f);
            if ((G.Cave.IsSolid(next) && Lands(next)) || _gone >= _max) { Burst(GlobalPosition); return; }
            GlobalPosition = next;
            // (it bursts on the creatures it was sent for, even before it meets the rock)
            if (_gone > 40f && GlobalPosition.DistanceTo(Target) < 10f && Foes().Any(e => e.GlobalPosition.DistanceTo(GlobalPosition) < e.HitRadius + 12f)) { Burst(GlobalPosition); return; }
        }
        // the burning rock and its trail
        var at = GlobalPosition;
        G.Fx.Burst(at, Frost ? new Color(0.9f, 0.97f, 1f) : new Color(1f, 0.75f, 0.35f), 2, 40, 5.5f, 0.18f, 0, 0);
        G.Fx.Trail(at - _dir * 6f, new Color(col, 0.9f));
        G.Fx.Directional(at, -_dir, 0.4f, G.Chance(0.5f) ? col : col.Lightened(0.35f), 2, 140, 3f, 0.35f, Frost ? 60 : -40, 0);
        if (G.Chance(0.3f)) G.Fx.Smoke(at - _dir * 10f, 1, Frost ? new Color(0.85f, 0.92f, 1f, 0.4f) : new Color(0.3f, 0.27f, 0.25f, 0.5f), 20f);
        if ((_fx -= dt) <= 0) { _fx = 0.08f; G.Fx.LightUp(at, col, 2.2f, 140f, 0.12f); }
    }

    /// <summary>Rock here stops it: any, cast in the air; cast standing, only rock as low as the caster's ground or lower.</summary>
    public bool Lands(Vector2 p) => GroundY is not float g || p.Y >= g - 4f;

    private void Burst(Vector2 at)
    {
        Landed = true;
        LandedAt = at;
        var col = Col;
        if (Frost) { FrostBurst(at, col); return; }
        G.Fx.Explosion(at, col, 1.8f);
        G.Fx.Shockwave(at, Radius, new Color(1f, 0.6f, 0.25f, 0.7f), 0.4f);
        G.Fx.Ring(at, Radius, new Color(col, 0.9f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.Debris(at, new Color(0.35f, 0.25f, 0.2f), 22, 300);
        G.Fx.LightUp(at, col, 4f, Radius * 3f, 0.6f);
        G.Fx.AddShake(7f);
        G.Main.Rumble(0.8f, 0.9f, 0.3f);
        G.Sfx.Play("slam", at, 2, 0.05f, 0.6f);
        G.Sfx.Play("lava", at, 0, 0.05f, 0.5f);
        Smashed += Breakables.Smash(at, Radius * 0.6f);
        GasCloud.IgniteAt(at, Radius);
        foreach (var e in Foes())
        {
            if (e.GlobalPosition.DistanceTo(at) > Radius + e.HitRadius) continue;
            if (!G.Cave.LineClear(at + (e.GlobalPosition - at).Normalized() * 4f, e.GlobalPosition)) continue;
            Struck++;
            var away = (e.GlobalPosition - at).Normalized();
            Hit(e, Damage, away * 160f, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.Elements.Full);
        }
        // embers raining back down
        for (int k = 0; k < 10; k++) G.Fx.Ember(at + G.RandDir() * G.Range(0f, Radius * 0.7f), new Color(1f, G.Range(0.45f, 0.8f), 0.2f));
        QueueFree();
    }

    /// <summary>A Cryometeor lands: everything about it is frozen solid (two full build-ups), and the ground freezes over.</summary>
    private void FrostBurst(Vector2 at, Color col)
    {
        G.Fx.Burst(at, new Color(0.92f, 0.98f, 1f), 34, 280, 3f, 0.55f, 260);
        G.Fx.Shockwave(at, Radius, new Color(col, 0.7f), 0.4f);
        G.Fx.Ring(at, Radius, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.LightUp(at, col, 3f, Radius * 2.5f, 0.5f);
        G.Fx.AddShake(6f);
        G.Main.Rumble(0.7f, 0.8f, 0.25f);
        G.Sfx.Play("slam", at, 0, 0.05f, 0.7f);
        G.Sfx.Play("clink", at, 0, 0.05f, 0.4f);
        Smashed += Breakables.Smash(at, Radius * 0.6f);
        foreach (var e in Foes())
        {
            if (e.GlobalPosition.DistanceTo(at) > Radius + e.HitRadius) continue;
            if (!G.Cave.LineClear(at + (e.GlobalPosition - at).Normalized() * 4f, e.GlobalPosition)) continue;
            Struck++;
            Hit(e, Damage, (e.GlobalPosition - at).Normalized() * 100f, DamageKind.Frost);
            Build(e, HeroElement.Frost, Tune.Elements.Full);
            Build(e, HeroElement.Frost, Tune.Elements.Full);
        }
        if (Ground.Below(at + new Vector2(0, -16), 80, out var floor))
        {
            var field = new TundraField { Position = floor, Caster = Caster, Power = Power };
            G.Spawn(field);
        }
        QueueFree();
    }
}

/// <summary>Tornado (wind+wind, the fixed-range spell): a whirlwind runs along the floor, sweeping up the light creatures it meets and carrying them along
/// inside it, swallowing the shots thrown at it; when it blows out, what it carried is flung away.</summary>
public partial class Tornado : HeroSpell
{
    public float Dir = 1f, Speed = Tune.Dual.TornadoSpeed, Seconds = Tune.Dual.TornadoSeconds, Radius = Tune.Dual.TornadoRadius;
    private float _floorY, _tick, _carryT;
    private readonly HashSet<Enemy> _caught = new();
    public int Caught => _caught.Count;
    public int Swallowed { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        _floorY = GlobalPosition.Y;
    }

    private Vector2 Eye => new(GlobalPosition.X, _floorY - Radius * 1.1f);

    protected override void Tick(float dt)
    {
        var col = Elements.ColorOf(HeroElement.Wind);
        // along the floor (a wall turns it back; a drop, it crosses at the same height)
        float nx = GlobalPosition.X + Dir * Speed * dt;
        if (Ground.FloorNear(nx, _floorY, 22f, 40f, out float fy)) _floorY = fy;
        else if (G.Cave.IsSolid(new Vector2(nx, _floorY - 22f))) { Dir = -Dir; nx = GlobalPosition.X; }
        GlobalPosition = new Vector2(nx, _floorY);
        if (T >= Seconds) { BlowOut(col); return; }
        var eye = Eye;
        // it takes up the light creatures it meets, and holds them turning in it
        bool carry = (_carryT -= dt) <= 0;
        if (carry) _carryT = 0.05f;
        bool tick = (_tick -= dt) <= 0;
        if (tick) _tick = 0.5f;
        foreach (var e in Foes())
        {
            float d = e.GlobalPosition.DistanceTo(eye);
            if (!_caught.Contains(e))
            {
                if (d > Radius + e.HitRadius || e.IsBoss) continue;
                _caught.Add(e);
                G.Fx.Ring(e.GlobalPosition, e.HitRadius + 6, new Color(col, 0.8f), 0.25f, FxLayer.RingStyle.Dashed);
            }
            if (d > Radius * 3f) { _caught.Remove(e); continue; }
            float spin = T * 9f + e.GetInstanceId() % 7;
            var want = eye + new Vector2(MathF.Cos(spin) * Radius * 0.45f, -Radius * 0.3f + MathF.Sin(spin) * 6f);
            if (carry) e.Shove((want - e.GlobalPosition) * 7f + new Vector2(Dir * Speed, 0));
            if (tick) Hit(e, Tune.Dual.TornadoTick * Power, Vector2.Zero, DamageKind.Physical);
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            if (IsInstanceValid(pr) && pr.GlobalPosition.DistanceTo(eye) < Radius * 1.4f) { Swallowed++; G.Fx.Burst(pr.GlobalPosition, new Color(col, 0.8f), 5, 60, 1.4f, 0.25f, 0); pr.QueueFree(); }
        // the funnel: streaks whirling round a column that widens as it rises
        for (int k = 0; k < 7; k++)
        {
            float h = G.Range(0f, 1f);
            float w = Radius * (0.2f + 0.9f * h);
            float a = G.Range(0f, Mathf.Tau);
            var at = new Vector2(GlobalPosition.X + MathF.Cos(a) * w, _floorY - h * Radius * 2.6f);
            var tone = G.Chance(0.5f) ? new Color(col, 0.75f) : new Color(0.88f, 0.9f, 0.86f, 0.6f);
            G.Fx.Directional(at, new Vector2(-MathF.Sin(a) * Dir, -0.3f).Normalized(), 0.15f, tone, 1, 220, G.Range(1.4f, 2.2f), 0.22f, 0, 1);
        }
        if (G.Chance(0.3f)) G.Fx.Dust(new Vector2(GlobalPosition.X, _floorY), 1, 1f, new Color(0.6f, 0.58f, 0.52f, 0.5f));
    }

    private void BlowOut(Color col)
    {
        foreach (var e in _caught)
        {
            if (!IsInstanceValid(e) || e.Dead) continue;
            var away = new Vector2(Math.Sign(e.GlobalPosition.X - GlobalPosition.X + Dir * 0.1f), -1.1f).Normalized();
            e.Shove(away * Tune.Dual.TornadoFling);
            Hit(e, Tune.Dual.TornadoTick * 2f * Power, away * 60f, DamageKind.Physical);
        }
        G.Fx.Shockwave(Eye, Radius * 1.6f, new Color(col, 0.6f), 0.35f);
        G.Sfx.Play("dodge", Eye, -2, 0.05f, 0.6f);
        QueueFree();
    }
}

/// <summary>
/// Strong Gust (wind+wind, the attack): a little whirlwind that races along the floor, shoving every creature it meets back ahead of it
/// (a boss barely) and nicking them as it goes; a wall stops it, a drop it crosses at the same height.
/// </summary>
public partial class StrongGust : HeroSpell
{
    public float Dir = 1f, Speed = Tune.Dual.GustSpeed, Seconds = Tune.Dual.GustSeconds, Radius = Tune.Dual.GustRadius;
    private float _floorY, _shoveT;
    private readonly Dictionary<Enemy, float> _nick = new();
    /// <summary>For the tests: the creatures it has shoved.</summary>
    public int Pushed => _nick.Count;

    public override void _Ready()
    {
        base._Ready();
        _floorY = GlobalPosition.Y;
        G.Fx.Directional(GlobalPosition + new Vector2(0, -10), new Vector2(Dir, 0), 0.3f, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 10, 260, 2f, 0.3f, 0, 1);
        G.Sfx.Play("dodge", GlobalPosition, -2, 0.05f, 1.2f);
    }

    protected override void Tick(float dt)
    {
        var col = Elements.ColorOf(HeroElement.Wind);
        float nx = GlobalPosition.X + Dir * Speed * dt;
        if (Ground.FloorNear(nx, _floorY, 18f, 40f, out float fy)) _floorY = fy;
        else if (G.Cave.IsSolid(new Vector2(nx, _floorY - 16f))) { Fade(col); return; }
        GlobalPosition = new Vector2(nx, _floorY);
        if (T >= Seconds) { Fade(col); return; }
        var eye = new Vector2(GlobalPosition.X, _floorY - Radius);
        bool shove = (_shoveT -= dt) <= 0f;
        if (shove) _shoveT = 0.05f;
        foreach (var e in Foes())
        {
            if (e.GlobalPosition.DistanceTo(eye) > Radius + e.HitRadius + 6f) continue;
            // back it goes, ahead of the wind (kept just in front of it), and a nick every so often (a boss stands its ground)
            float ahead = (e.GlobalPosition.X - GlobalPosition.X) * Dir;
            if (shove) e.Shove(new Vector2(Dir * Tune.Dual.GustShove + (ahead < 0 ? Dir * Speed : 0f), -50f));
            if (!_nick.TryGetValue(e, out float last) || T - last >= Tune.Dual.GustEvery)
            {
                _nick[e] = T;
                Hit(e, Tune.Dual.GustTick * Power, new Vector2(Dir * 20f, 0), DamageKind.Physical);
            }
        }
        // the little funnel: streaks whirling round a column, widening as it rises
        for (int k = 0; k < 4; k++)
        {
            float h = G.Range(0f, 1f);
            float w = Radius * (0.25f + 0.8f * h);
            float a = G.Range(0f, Mathf.Tau);
            var at = new Vector2(GlobalPosition.X + MathF.Cos(a) * w, _floorY - h * Radius * 2.2f);
            var tone = G.Chance(0.5f) ? new Color(col, 0.8f) : new Color(0.9f, 0.92f, 0.88f, 0.6f);
            G.Fx.Directional(at, new Vector2(-MathF.Sin(a) * Dir, -0.3f).Normalized(), 0.15f, tone, 1, 240, G.Range(1.2f, 1.9f), 0.18f, 0, 1);
        }
        if (G.Chance(0.4f)) G.Fx.Dust(new Vector2(GlobalPosition.X, _floorY), 1, 1f, new Color(0.6f, 0.58f, 0.52f, 0.5f));
    }

    private void Fade(Color col)
    {
        G.Fx.Burst(new Vector2(GlobalPosition.X, _floorY - Radius), new Color(col, 0.7f), 10, 120, 1.8f, 0.3f, 0, 1);
        QueueFree();
    }
}

/// <summary>Fire Walk (fire, movement): for a while fire burns where the caster walks, a trail of it fading behind; creatures in it burn.</summary>
public partial class FireTrail : HeroSpell
{
    public float Seconds = Tune.ElementalStaff.WalkSeconds;
    private readonly List<(Vector2 at, float t)> _spots = new();
    private float _lay, _tick;
    public int Burned { get; private set; }

    protected override void Tick(float dt)
    {
        bool laying = T < Seconds && Caster != null && IsInstanceValid(Caster) && !Caster.Dead;
        // a patch of fire where the caster's feet are (on the ground)
        if (laying && (_lay -= dt) <= 0 && Ground.Below(Caster.GlobalPosition + new Vector2(0, 6), 14, out var floor))
        {
            _lay = 0.08f;
            if (_spots.Count == 0 || _spots[^1].at.DistanceTo(floor) > 8f) _spots.Add((floor, T));
        }
        float life = Tune.ElementalStaff.TrailLife;
        _spots.RemoveAll(p => T - p.t > life);
        if (!laying && _spots.Count == 0) { QueueFree(); return; }
        foreach (var (at, t) in _spots)
        {
            if (!G.Chance(0.3f)) continue;
            float fade = Math.Clamp((life - (T - t)) / 0.6f, 0f, 1f);
            G.Fx.Directional(at + new Vector2(G.Range(-5f, 5f), 0), Vector2.Up, 0.25f, new Color(1f, G.Range(0.4f, 0.7f), 0.12f, 0.9f * fade), 1, G.Range(60, 120), G.Range(2f, 3f), 0.3f, -40, 0);
        }
        if ((_tick -= dt) > 0) return;
        _tick = Tune.ElementalStaff.TrailTick;
        foreach (var e in Foes())
        {
            if (!_spots.Any(p => Ground.Over(e, p.at.X - 8f, p.at.X + 8f, p.at.Y, 30f))) continue;
            Burned++;
            Hit(e, Tune.ElementalStaff.TrailDamage * Power, Vector2.Zero, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.ElementalStaff.TrailBuild);
        }
    }
}

/// <summary>Tundra (frost, movement): the ground about where it was cast freezes over. Heroes on it slide quicker; creatures on it are chilled
/// and slowly filled with frost.</summary>
public partial class TundraField : HeroSpell
{
    public float Seconds = Tune.ElementalStaff.TundraSeconds, Half = Tune.ElementalStaff.TundraHalf;
    private float _x0, _x1, _tick;
    private readonly List<Vector2> _surface = new();
    public int Chilled { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        // the frost spreads along the floor each way until a wall or a drop stops it
        var c = GlobalPosition;
        _x0 = _x1 = c.X;
        float y = c.Y;
        for (float x = c.X; x >= c.X - Half; x -= 8f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; _x0 = x; _surface.Add(new Vector2(x, fy)); }
        y = c.Y;
        for (float x = c.X + 8f; x <= c.X + Half; x += 8f) { if (!Ground.FloorNear(x, y, 16f, 18f, out float fy)) break; y = fy; _x1 = x; _surface.Add(new Vector2(x, fy)); }
        if (_surface.Count == 0) _surface.Add(c);
        foreach (var p in _surface) G.Fx.Burst(p, new Color(0.9f, 0.97f, 1f, 0.9f), 2, 50, 2.4f, 0.4f, 0, 0);
        G.Sfx.Play("clink", c, -4, 0.05f, 0.6f);
    }

    protected override void Tick(float dt)
    {
        if (T >= Seconds) { QueueFree(); return; }
        float fade = Math.Clamp((Seconds - T) / 0.6f, 0f, 1f);
        var col = Elements.ColorOf(HeroElement.Frost);
        var p = _surface[G.RangeI(0, _surface.Count - 1)];
        G.Fx.Burst(p + new Vector2(G.Range(-4f, 4f), -1f), new Color(0.88f, 0.96f, 1f, 0.85f * fade), 1, 10, G.Range(2.5f, 4f), 0.5f, 0, 0);
        if (G.Chance(0.12f)) G.Fx.Glint(p + new Vector2(0, -2), col, 4f);
        if ((_tick -= dt) > 0) return;
        _tick = Tune.ElementalStaff.TundraTick;
        float y = GlobalPosition.Y;
        foreach (var e in Foes())
        {
            if (!Ground.Over(e, _x0, _x1, y + 10f, 30f + e.HitRadius)) continue;
            Chilled++;
            e.Chill(Tune.ElementalStaff.TundraSlow, Tune.ElementalStaff.TundraTick + 0.3f);
            Build(e, HeroElement.Frost, Tune.ElementalStaff.TundraBuild);
        }
        // (heroes on the ice slide quicker)
        foreach (var h in G.Players)
            if (h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.X >= _x0 - 6f && h.GlobalPosition.X <= _x1 + 6f && Math.Abs(h.GlobalPosition.Y + 12f - y) < 30f)
                h.GiveBuff(Player.BuffTailwind, Tune.ElementalStaff.TundraTick + 0.2f);
    }
}

/// <summary>Fire Barrage / Frost Barrage (fire+wind / frost+wind, the bolt): a volley of bolts from the staff, one after another.</summary>
public partial class Barrage : HeroSpell
{
    public HeroElement El = HeroElement.Fire;
    public Vector2 Aim;
    private float _next;
    public int Loosed { get; private set; }

    protected override void Tick(float dt)
    {
        if (Caster == null || !IsInstanceValid(Caster) || Caster.Dead || Loosed >= Tune.Dual.BarrageBolts) { QueueFree(); return; }
        if ((_next -= dt) > 0) return;
        _next = Tune.Dual.BarrageEvery;
        Loosed++;
        Caster.BarrageBolt(El, Aim);
    }
}

/// <summary>Flamebreath (fire+fire, movement): fire pours from the staff in a cone. Creatures in it burn; aimed down, it holds the caster up.</summary>
public partial class FlameBreath : HeroSpell
{
    public Vector2 Dir;
    private float _tick;
    public int Burned { get; private set; }

    protected override void Tick(float dt)
    {
        if (Caster == null || !IsInstanceValid(Caster) || Caster.Dead || T >= Tune.Dual.BreathSeconds) { QueueFree(); return; }
        var from = Caster.CastPoint;
        GlobalPosition = from;
        float half = Mathf.DegToRad(Tune.Dual.BreathDegrees) * 0.5f, range = Tune.Dual.BreathRange;
        for (int k = 0; k < 5; k++)
        {
            var d = Dir.Rotated(G.Range(-half, half));
            var tone = G.Chance(0.5f) ? new Color(1f, 0.45f, 0.1f, 0.9f) : new Color(1f, 0.8f, 0.3f, 0.9f);
            G.Fx.Directional(from + d * 6f, d, 0.05f, tone, 1, G.Range(260, 360), G.Range(2.4f, 4f), range / 320f, -50, 0);
        }
        // (breathed at the floor, the fire holds you up)
        if (Dir.Y > 0.5f && Caster.Velocity.Y > -60f) Caster.Velocity = new Vector2(Caster.Velocity.X, -60f);
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
        {
            if (!IsInstanceValid(pr)) continue;
            var to = pr.GlobalPosition - from;
            if (to.Length() <= range && Math.Abs(Dir.AngleTo(to)) <= half) pr.Deflect();
        }
        if ((_tick -= dt) > 0) return;
        _tick = Tune.Dual.BreathTick;
        GasCloud.IgniteAt(from + Dir * range * 0.6f, range * 0.4f);
        foreach (var e in Foes())
        {
            var to = e.GlobalPosition - from;
            if (to.Length() > range + e.HitRadius || (to.Length() > 12f && Math.Abs(Dir.AngleTo(to)) > half + 0.1f)) continue;
            if (!G.Cave.LineClear(from, e.GlobalPosition)) continue;
            Burned++;
            Hit(e, Tune.Dual.BreathDamage * Power, Dir * 20f, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.Dual.BreathBuild);
        }
    }
}

/// <summary>Fire Barrier (fire+wind, movement): a ring of fire about the caster, going where they go. Creatures that touch it burn and are
/// thrown back; shots that cross it burn up.</summary>
public partial class FireRing : HeroSpell
{
    private float _tick;
    public int Burned { get; private set; }

    protected override void Tick(float dt)
    {
        if (Caster == null || !IsInstanceValid(Caster) || Caster.Dead || T >= Tune.Dual.RingSeconds) { QueueFree(); return; }
        var c = Caster.GlobalPosition + new Vector2(0, -4);
        GlobalPosition = c;
        float r = Tune.Dual.RingRadius;
        for (int k = 0; k < 4; k++)
        {
            float a = G.Range(0f, Mathf.Tau) + T * 3f;
            var at = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            G.Fx.Directional(at, new Vector2(-MathF.Sin(a), MathF.Cos(a)), 0.3f, G.Chance(0.5f) ? new Color(1f, 0.45f, 0.1f, 0.9f) : new Color(1f, 0.75f, 0.3f, 0.9f), 1, 80, G.Range(2.2f, 3.2f), 0.25f, -60, 0);
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            if (IsInstanceValid(pr) && Math.Abs(pr.GlobalPosition.DistanceTo(c) - r) < 12f) pr.Deflect();
        if ((_tick -= dt) > 0) return;
        _tick = Tune.Dual.RingTick;
        foreach (var e in Foes())
        {
            float d = e.GlobalPosition.DistanceTo(c);
            if (d > r + e.HitRadius + 6f) continue;
            Burned++;
            var away = (e.GlobalPosition - c).Normalized();
            Hit(e, Tune.Dual.RingDamage * Power, away * 60f, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.Dual.RingBuild);
            e.Shove(away * Tune.Dual.RingPush + new Vector2(0, -60f));
        }
    }
}

/// <summary>Rain of Fire / Hailstorm (fire+fire / frost+frost, the fixed-range spell): fire (or hail) falls out of the dark over the spot.
/// Each fireball bursts where it lands; each hailstone strikes what it hits, and beats flying creatures down.</summary>
public partial class Downpour : HeroSpell
{
    public HeroElement El = HeroElement.Fire;
    private float _drop;
    private readonly List<(Vector2 at, Vector2 v)> _falling = new();
    public int Struck { get; private set; }

    protected override void Tick(float dt)
    {
        bool fire = El == HeroElement.Fire;
        var col = fire ? new Color(1f, 0.5f, 0.15f) : Elements.ColorOf(HeroElement.Frost);
        var c = GlobalPosition;
        if (T < Tune.Dual.RainSeconds && (_drop -= dt) <= 0)
        {
            _drop = Tune.Dual.RainEvery;
            // from as high over the spot as the roof allows
            float x = c.X + G.Range(-Tune.Dual.RainHalf, Tune.Dual.RainHalf);
            G.Cave.Raycast(new Vector2(x, c.Y), Vector2.Up, 180f, out var top, 6f);
            _falling.Add((top + new Vector2(0, 6), new Vector2(G.Range(-30f, 30f), fire ? 380f : 460f)));
        }
        if (T >= Tune.Dual.RainSeconds && _falling.Count == 0) { QueueFree(); return; }
        for (int k = _falling.Count - 1; k >= 0; k--)
        {
            var (at, v) = _falling[k];
            var next = at + v * dt;
            Enemy hit = Foes().FirstOrDefault(e => e.GlobalPosition.DistanceTo(next) < e.HitRadius + 4f);
            bool ground = G.Cave.IsSolid(next) || Ground.Solid(next) || next.Y > c.Y + 140f;
            if (hit == null && !ground)
            {
                _falling[k] = (next, v);
                if (fire) G.Fx.Burst(next, new Color(1f, 0.7f, 0.3f, 0.95f), 1, 10, 3.2f, 0.08f, 0, 0);
                else G.Fx.Burst(next, new Color(0.9f, 0.97f, 1f, 0.95f), 1, 8, 2.4f, 0.06f, 0, 0);
                continue;
            }
            _falling.RemoveAt(k);
            if (fire) FireLands(next, hit, col); else HailLands(next, hit, col);
        }
    }

    private void FireLands(Vector2 at, Enemy direct, Color col)
    {
        G.Fx.Burst(at, col, 6, 90, 2.4f, 0.3f, 120);
        G.Fx.Flash(at, 10, col, 0.08f);
        GasCloud.IgniteAt(at, Tune.Dual.RainBurst);
        foreach (var e in Foes())
        {
            if (e != direct && e.GlobalPosition.DistanceTo(at) > Tune.Dual.RainBurst + e.HitRadius) continue;
            Struck++;
            Hit(e, Tune.Dual.RainDamage * Power, (e.GlobalPosition - at).Normalized() * 30f, DamageKind.Fire);
            Build(e, HeroElement.Fire, Tune.Dual.RainBuild);
        }
    }

    private void HailLands(Vector2 at, Enemy direct, Color col)
    {
        G.Fx.Burst(at, new Color(0.9f, 0.97f, 1f), 4, 80, 1.8f, 0.25f, 300);
        if (direct == null) { Breakables.SpellBurst(at, 6f); return; }
        Struck++;
        Hit(direct, Tune.Dual.HailDamage * Power, new Vector2(0, 40f), DamageKind.Frost);
        Build(direct, HeroElement.Frost, Tune.Dual.HailBuild);
        // (the hail beats what flies down out of the air)
        direct.Shove(new Vector2(0, Tune.Dual.HailBeat));
    }
}

/// <summary>
/// Frost Tower (frost+frost, movement): a pillar of ice rising under the caster as they leap, to stand on and cast from higher ground.
/// It is solid to creatures and to its caster's feet (the other heroes pass through it); creatures near it go for it instead of a hero,
/// their blows and shots wearing it down (<see cref="Enemy.Lure"/>); it shatters when its health is gone or its time is up. It doesn't
/// attack. It lives in its caster's game (online, only the host's creatures can go for a host's tower).
/// </summary>
public partial class FrostTower : StaticBody2D
{
    public static readonly List<FrostTower> All = new();
    public Player Caster;
    public float Hp, MaxHp, Height = Tune.Dual.TowerHeight, Half = Tune.Dual.TowerHalf, Seconds = Tune.Dual.TowerSeconds, Rise = Tune.Dual.TowerRise;
    /// <summary>Something to stand on (a Frost Tower; an <see cref="IceDecoy"/> is walked through), and, above 0, how many blows it takes
    /// whatever their weight (its health otherwise).</summary>
    public bool Solid = true;
    public int Hits;
    private float _t, _flash;
    private bool _gone;
    private RectangleShape2D _rect;
    private CollisionShape2D _shape;
    /// <summary>For the tests: blows and shots it has taken.</summary>
    public int Struck { get; private set; }

    public bool Standing => !_gone && Hp > 0 && IsInsideTree();
    /// <summary>Just struck (for the 3D view's flash).</summary>
    public bool Flashing => _flash > 0;
    /// <summary>How tall it stands now (it rises in a moment).</summary>
    public float Risen => Height * Math.Clamp(_t / Rise, 0f, 1f);
    public float Top => GlobalPosition.Y - Risen;

    public override void _Ready()
    {
        CollisionLayer = Solid ? G.LayerTower : 0;
        CollisionMask = 0;
        ZIndex = 1;
        _rect = new RectangleShape2D { Size = new Vector2(Half * 2f, 2f) };
        _shape = new CollisionShape2D { Shape = _rect, Position = new Vector2(0, -1f) };
        AddChild(_shape);
        if (Hits > 0) MaxHp = Hits;
        else if (MaxHp <= 0) MaxHp = Tune.Dual.TowerHp;
        Hp = MaxHp;
        Rising();
    }

    /// <summary>The ice coming up out of the floor.</summary>
    protected virtual void Rising()
    {
        var col = Elements.ColorOf(HeroElement.Frost);
        G.Fx.Directional(GlobalPosition, Vector2.Up, 0.12f, new Color(0.92f, 0.98f, 1f), 26, 480, 3.4f, 0.45f, 300, 0);
        G.Fx.Ring(GlobalPosition, 26, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.AddShake(3f);
        G.Sfx.Play("clink", GlobalPosition, 0, 0.05f, 0.5f);
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() { All.Remove(this); Release(); }

    /// <summary>The nearest point of the pillar to <paramref name="from"/> (where a creature strikes it).</summary>
    public Vector2 NearestPoint(Vector2 from)
        => new(Math.Clamp(from.X, GlobalPosition.X - Half, GlobalPosition.X + Half), Math.Clamp(from.Y, Top, GlobalPosition.Y));

    /// <summary>The standing tower a point is inside, or null (a shot meeting one).</summary>
    public static FrostTower At(Vector2 p)
    {
        foreach (var t in All)
            if (t.Standing && Math.Abs(p.X - t.GlobalPosition.X) <= t.Half && p.Y >= t.Top && p.Y <= t.GlobalPosition.Y) return t;
        return null;
    }

    /// <summary>The tower that draws a creature going for <paramref name="hero"/>: the nearest standing one within reach, seen, and nearer than
    /// the hero (or the hero stands on it).</summary>
    public static FrostTower LureFor(Enemy e, Player hero)
    {
        FrostTower best = null;
        float bd = Tune.Dual.LureRange;
        var me = e.GlobalPosition;
        foreach (var t in All)
        {
            if (!t.Standing) continue;
            var at = t.NearestPoint(me);
            float d = at.DistanceTo(me);
            if (d >= bd || !G.Cave.LineClear(me, at)) continue;
            bool onIt = t.Solid && hero != null && hero == t.Caster && t.Holds(hero);
            if (!onIt && hero != null && hero.GlobalPosition.DistanceTo(me) < d) continue;
            bd = d; best = t;
        }
        return best;
    }

    /// <summary>The hero is up on it (or over it).</summary>
    public bool Holds(Player h) => Math.Abs(h.GlobalPosition.X - GlobalPosition.X) <= Half + 8f && h.GlobalPosition.Y + 13f <= Top + 4f;

    /// <summary>A creature's blow or shot.</summary>
    public void Damage(float dmg, Enemy by)
    {
        if (!Standing || dmg <= 0) return;
        Struck++;
        Hp -= Hits > 0 ? 1f : dmg;
        _flash = 0.12f;
        var at = new Vector2(GlobalPosition.X, Top + Risen * 0.4f);
        G.Fx.Burst(at, new Color(0.88f, 0.96f, 1f), 6, 90, 2f, 0.3f, 200);
        if (Hits <= 0) G.Fx.TickText(at + new Vector2(0, -10), Mathf.CeilToInt(dmg).ToString(), Elements.ColorOf(HeroElement.Frost));
        G.Sfx.Play("clink", at, -6, 0.1f, 1.2f);
        if (Hp <= 0) Shatter();
    }

    private void Shatter()
    {
        if (_gone) return;
        _gone = true;
        var c = GlobalPosition + new Vector2(0, -Risen * 0.5f);
        G.Fx.Burst(c, new Color(0.9f, 0.97f, 1f), 28, 180, 2.6f, 0.5f, 320);
        G.Fx.Debris(c, Elements.ColorOf(HeroElement.Frost), 10, 160);
        G.Sfx.Play("clink", c, -2, 0.05f, 0.5f);
        Shattered(c);
        Release();
        QueueFree();
    }

    /// <summary>It breaks (worn down or its time up).</summary>
    protected virtual void Shattered(Vector2 at) { }

    /// <summary>Its caster's feet no longer meet it.</summary>
    private void Release()
    {
        if (Solid && Caster != null && IsInstanceValid(Caster)) Caster.CollisionMask &= ~G.LayerTower;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gone) return;
        float dt = (float)delta;
        _t += dt;
        if (_flash > 0) _flash -= dt;
        if (_t >= Seconds) { Shatter(); return; }
        float h = Math.Max(2f, Risen);
        _rect.Size = new Vector2(Half * 2f, h);
        _shape.Position = new Vector2(0, -h * 0.5f);
        // its caster stands on it once above its top, and walks through it from beside (as the other heroes always do)
        if (Solid && Caster != null && IsInstanceValid(Caster) && !Caster.Dead)
        {
            if (Caster.GlobalPosition.Y + 13f <= Top + 2f) Caster.CollisionMask |= G.LayerTower;
            else if (Caster.GlobalPosition.Y + 13f > Top + 8f) Caster.CollisionMask &= ~G.LayerTower;
        }
        NetSync.Scope++;
        try { Shimmer(h); }
        finally { NetSync.Scope--; }
    }

    /// <summary>About the pillar (its body is the 3D view): a glint now and then, chips falling as it's worn down.</summary>
    private void Shimmer(float h)
    {
        var col = Elements.ColorOf(HeroElement.Frost);
        float worn = Math.Clamp(Hp / Math.Max(1f, MaxHp), 0f, 1f);
        if (G.Chance(0.06f)) G.Fx.Glint(new Vector2(GlobalPosition.X + G.Range(-Half, Half), GlobalPosition.Y - G.Range(0f, h)), col, 4f);
        // (cracked: chips falling off it)
        if (worn < 0.5f && G.Chance(0.1f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-Half, Half), -G.Range(0f, h)), new Color(0.85f, 0.95f, 1f), 1, 40, 1.6f, 0.4f, 300);
    }

    public override void _Draw()
    {
        // (the 2D view, for the debug overlay)
        float h = Risen;
        DrawRect(new Rect2(-Half, -h, Half * 2f, h), new Color(0.7f, 0.9f, 1f, 0.35f));
    }
}

/// <summary>
/// Glacial Vault's decoy: the Swordsman's likeness in ice, left standing where he sprang back from. Creatures go for it as they do a Frost
/// Tower (anyone walks through it: it is no wall); it takes <c>DecoyHits</c> blows (or <c>DecoySeconds</c>) and shatters, filling the
/// creatures beside it with frost. The 3D view wears the hero's own body, posed as he stood, in ice (see <see cref="IceDecoyView"/>).
/// </summary>
public partial class IceDecoy : FrostTower
{
    /// <summary>The hero's pose as he left it (each bone's pose, and the skeleton's place about the decoy's foot), for the 3D view.</summary>
    public Vector3[] PosePos;
    public Quaternion[] PoseRot;
    public Vector3[] PoseScale;
    public Transform3D PoseAt;
    public CreatureModel PoseOf;
    /// <summary>A Frozen Effigy: as it shatters, the creatures beside it are frozen solid (two full frost build-ups).</summary>
    public bool Great;

    public IceDecoy()
    {
        Solid = false;
        Hits = Tune.ElementalSword.DecoyHits;
        Height = Tune.ElementalSword.DecoyHeight;
        Half = Tune.ElementalSword.DecoyHalf;
        Seconds = Tune.ElementalSword.DecoySeconds;
        Rise = 0.1f;
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        // (the pose is taken now, before the leap moves him: he stands as he was)
        var m = Caster?.Anim?.Model3D;
        if (m == null || PosePos != null || !IsInstanceValid(m) || !m.IsInsideTree()) return;
        var sk = m.Skel;
        int n = sk.GetBoneCount();
        PosePos = new Vector3[n]; PoseRot = new Quaternion[n]; PoseScale = new Vector3[n];
        for (int k = 0; k < n; k++) { PosePos[k] = sk.GetBonePosePosition(k); PoseRot[k] = sk.GetBonePoseRotation(k); PoseScale[k] = sk.GetBonePoseScale(k); }
        var g = sk.GlobalTransform;
        PoseAt = new Transform3D(g.Basis, g.Origin - W3.P(GlobalPosition));
        PoseOf = m;
    }

    protected override void Rising()
    {
        var col = Elements.ColorOf(HeroElement.Frost);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -12), new Color(0.92f, 0.98f, 1f), 16, 110, 2.2f, 0.4f, 120);
        G.Fx.Ring(GlobalPosition, 18, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("clink", GlobalPosition, -2, 0.05f, 0.8f);
    }

    protected override void Shattered(Vector2 at)
    {
        var col = Elements.ColorOf(HeroElement.Frost);
        G.Fx.Ring(at, Tune.ElementalSword.DecoyRadius, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(at, new Color(0.95f, 0.99f, 1f), 20, 220, 2.4f, 0.45f, 300);
        if (Caster == null || !IsInstanceValid(Caster)) return;
        float power = Caster.Stats.DamageMult;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(at) > Tune.ElementalSword.DecoyRadius + e.HitRadius) continue;
            e.Build(HeroElement.Frost, Great ? Tune.Elements.Full : Tune.ElementalSword.DecoyBuild, power, Caster);
            if (Great && !e.Dead) e.Build(HeroElement.Frost, Tune.Elements.Full, power, Caster);
        }
    }
}
