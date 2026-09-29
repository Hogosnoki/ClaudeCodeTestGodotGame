using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementals: the cave's own forces, each at home where its element is. They stand off and
/// attack from range (a wind-up you can read, then the blow), and each has a nature of its own:
/// the Earth Elemental hurls boulders and shrugs off knocks, the Frost Elemental fans out shards of
/// ice, the Nature Elemental looses leaf-blades and heals if left alone, the Fire Elemental is quick
/// and flings fireballs and burns whoever touches it, and the Water Elemental lives in the water
/// and spits droplets from it.
/// </summary>
public abstract partial class ElementalWalker : Walker
{
    protected float AtkCd = 1.8f, Wind = -1, Recover;
    protected abstract string Set { get; }
    protected abstract float WalkSpeed { get; }
    protected abstract float WindTime { get; }
    protected abstract float Cooldown { get; }
    /// <summary>How close it likes to be to attack (it backs off if you're much closer).</summary>
    protected abstract float Reach { get; }
    protected abstract void Unleash();
    protected virtual void Tick(float dt) { }
    protected virtual string WindSound => "goblin";

    protected override void Setup() => UseSprite(Set);

    protected override void Think(float dt)
    {
        AtkCd -= dt; Recover -= dt;
        Tick(dt);
        if (Paddle(dt)) return;
        var v = Velocity;
        if (Wind >= 0)
        {
            Wind += dt;
            Brake(ref v, dt, 900);
            if (Wind > WindTime)
            {
                Wind = -1; Recover = 0.6f; AtkCd = Cooldown * (Elite ? 0.7f : 1f);
                Anim.Once("slam", 3);
                Unleash();
            }
        }
        else if (Recover > 0) Brake(ref v, dt, 800);
        else if (Awake && DistP < Aggro(430))
        {
            int dir = DirP;
            Face = dir;
            float d = DistP;
            float want = d > Reach * 0.85f ? dir : d < Reach * 0.4f ? -dir : 0;
            v = Stride(v, want, WalkSpeed, dt, 300);
            if (AtkCd <= 0 && d < Reach * 1.15f && SeesP)
            {
                Wind = 0;
                Anim.Once("slam_windup", 3, 12f / (WindTime * 24f));
                G.Sfx.Play(WindSound, GlobalPosition, -4, 0.1f, 0.5f);
            }
        }
        else Brake(ref v, dt, 700);
        Velocity = v;
        ApplyGravity(dt);
    }

    protected override bool Busy => Wind >= 0 || Recover > 0 || InWater;
    public override bool Attacking => Wind >= 0;
    protected override float AttackReady => 1 - Math.Clamp(AtkCd / Cooldown, 0, 1);
    protected override void OnInterrupted() { Wind = -1; Recover = 0.4f; AtkCd = Math.Max(AtkCd, 1.2f); }

    /// <summary>A projectile aimed at the hero (lobbed, if <paramref name="grav"/> is set).</summary>
    protected EnemyProjectile Shot(string kind, float speed, float grav, float damage, float radius, float spread = 0f, float life = 3f)
    {
        var from = GlobalPosition + new Vector2(Face * 6f * Size, -8f * Size);
        var to = P.GlobalPosition + new Vector2(0, -4);
        Vector2 vel;
        if (grav > 0f)
        {
            // a lob that comes down on the hero
            float t = Math.Clamp(from.DistanceTo(to) / speed, 0.35f, 1.4f);
            vel = new Vector2((to.X - from.X) / t, (to.Y - from.Y) / t - 0.5f * grav * t);
        }
        else vel = (to - from).Normalized() * speed;
        var pr = new EnemyProjectile { Position = from, Vel = vel.Rotated(spread), Grav = grav, Damage = damage * DmgK, Kind = kind, Radius = radius, Life = life, Source = this };
        G.Spawn(pr);
        return pr;
    }

    protected override void Animate() => Anim.Loop(Math.Abs(Velocity.X) > 6 && IsOnFloor() ? "walk" : "idle", 1.15f);

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ earth

/// <summary>A construct of packed earth and stone: slow, heavy, hard to knock back; it tears a boulder from itself and hurls it.</summary>
public partial class EarthElemental : ElementalWalker
{
    public override Element Element => Element.Earth;
    public EarthElemental() { MaxHp = Tune.Elementals.Earth.Hp; BodyRadius = 14; ContactDamage = Tune.Elementals.Earth.Contact; XpValue = Tune.Elementals.Earth.Xp; KnockResist = 0.75f; }
    protected override void Setup() { DisplayName = "Earth Elemental"; base.Setup(); }
    protected override string Set => "elem_earth";
    protected override float WalkSpeed => Tune.Elementals.Earth.Speed;
    protected override float WindTime => 0.75f;
    protected override float Cooldown => Tune.Elementals.Earth.Cooldown;
    protected override float Reach => 230f;
    protected override string WindSound => "rock";
    protected override Color BloodColor => new(0.45f, 0.36f, 0.26f);

    protected override void Unleash()
    {
        G.Sfx.Play("throw", GlobalPosition, -2, 0.1f, 0.6f);
        G.Fx.Burst(GlobalPosition + new Vector2(Face * 8, -10) * Size, new Color(0.5f, 0.4f, 0.28f), 10, 120, 2.5f, 0.4f);
        Shot("rock", 300f, 620f, Tune.Elementals.Earth.Damage, 7f);
        if (Elite) Shot("rock", 300f, 620f, Tune.Elementals.Earth.Damage, 7f, 0.18f);
    }

    protected override void Die()
    {
        if (!Dead) { G.Fx.Burst(GlobalPosition, new Color(0.5f, 0.4f, 0.28f), 24, 220, 3.2f, 0.6f, 180f); G.Fx.AddShake(4); }
        base.Die();
    }
}

// ============================================================================ frost

/// <summary>A crystalline golem: it drives its fists into the ground and a fan of ice shards leaps out toward you.</summary>
public partial class FrostElemental : ElementalWalker
{
    public override Element Element => Element.Frost;
    public FrostElemental() { MaxHp = Tune.Elementals.Frost.Hp; BodyRadius = 15; ContactDamage = Tune.Elementals.Frost.Contact; XpValue = Tune.Elementals.Frost.Xp; KnockResist = 0.6f; }
    protected override void Setup() { DisplayName = "Frost Elemental"; base.Setup(); }
    protected override string Set => "elem_frost";
    protected override float WalkSpeed => Tune.Elementals.Frost.Speed;
    protected override float WindTime => 0.7f;
    protected override float Cooldown => Tune.Elementals.Frost.Cooldown;
    protected override float Reach => 220f;
    protected override string WindSound => "clink";
    protected override Color BloodColor => new(0.7f, 0.9f, 1f);

    protected override void Unleash()
    {
        G.Sfx.Play("spike", GlobalPosition, -2, 0.1f, 1.2f);
        G.Fx.Ring(GlobalPosition + new Vector2(Face * 10, 0) * Size, 28 * Size, new Color(0.75f, 0.94f, 1f, 0.8f), 0.3f);
        int n = Elite ? 7 : 5;
        for (int k = 0; k < n; k++) Shot("crystal", 250f, 0f, Tune.Elementals.Frost.Damage, 4f, (k - (n - 1) / 2f) * 0.16f, 2.4f);
    }

    protected override void Die()
    {
        if (!Dead) G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.95f, 1f), 26, 240, 2.6f, 0.6f, 240f);
        base.Die();
    }
}

// ============================================================================ nature

/// <summary>A tangle of roots in a rough shape of a man, leaves at its shoulders: it looses leaf-blades in a spread, and knits itself back together if left alone.</summary>
public partial class NatureElemental : ElementalWalker
{
    public override Element Element => Element.Nature;
    private float _sinceHit = 9f;

    public NatureElemental() { MaxHp = Tune.Elementals.Nature.Hp; BodyRadius = 13; ContactDamage = Tune.Elementals.Nature.Contact; XpValue = Tune.Elementals.Nature.Xp; }
    protected override void Setup() { DisplayName = "Nature Elemental"; base.Setup(); }
    protected override string Set => "elem_nature";
    protected override float WalkSpeed => Tune.Elementals.Nature.Speed;
    protected override float WindTime => 0.55f;
    protected override float Cooldown => Tune.Elementals.Nature.Cooldown;
    protected override float Reach => 210f;
    protected override Color BloodColor => new(0.35f, 0.65f, 0.2f);

    protected override void Tick(float dt)
    {
        _sinceHit += dt;
        // (left alone for a few seconds, it heals)
        if (_sinceHit > 3f && Hp < MaxHp && !Dead)
        {
            Hp = Math.Min(MaxHp, Hp + MaxHp * Tune.Elementals.Nature.RegenPerSec * dt);
            if (G.Chance(0.12f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), G.Range(-12, 8)) * Size, new Color(0.45f, 0.95f, 0.3f));
        }
    }

    protected override void OnHurt() => _sinceHit = 0f;

    protected override void Unleash()
    {
        G.Sfx.Play("throw", GlobalPosition, -3, 0.1f, 1.4f);
        G.Fx.Burst(GlobalPosition + new Vector2(Face * 8, -8) * Size, new Color(0.4f, 0.8f, 0.25f), 8, 110, 2f, 0.35f);
        int n = Elite ? 5 : 3;
        for (int k = 0; k < n; k++) Shot("leaf", 270f, 0f, Tune.Elementals.Nature.Damage, 4f, (k - (n - 1) / 2f) * 0.2f, 2.2f);
    }
}

// ============================================================================ fire

/// <summary>A gathering of small flames standing as one: quick on its feet, it flings fireballs, and it scorches whatever it touches.</summary>
public partial class FireElemental : ElementalWalker
{
    public override Element Element => Element.Fire;
    public FireElemental() { MaxHp = Tune.Elementals.Fire.Hp; BodyRadius = 12; ContactDamage = Tune.Elementals.Fire.Contact; XpValue = Tune.Elementals.Fire.Xp; }
    protected override void Setup() { DisplayName = "Fire Elemental"; base.Setup(); }
    protected override string Set => "elem_fire";
    protected override float WalkSpeed => Tune.Elementals.Fire.Speed;
    protected override float WindTime => 0.5f;
    protected override float Cooldown => Tune.Elementals.Fire.Cooldown;
    protected override float Reach => 180f;
    protected override string WindSound => "lava";
    protected override Color BloodColor => new(1f, 0.6f, 0.15f);

    protected override void Tick(float dt)
    {
        // it sheds sparks and flames as it goes
        if (G.Chance(0.35f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), G.Range(-14, 6)) * Size, G.Chance(0.5f) ? new Color(1f, 0.5f, 0.12f) : new Color(1f, 0.85f, 0.4f));
    }

    protected override void Unleash()
    {
        G.Sfx.Play("lava", GlobalPosition, -3, 0.1f, 1.6f);
        G.Fx.Flash(GlobalPosition + new Vector2(Face * 8, -8) * Size, 14, new Color(1f, 0.6f, 0.2f), 0.14f);
        int n = Elite ? 4 : 3;
        for (int k = 0; k < n; k++) Shot("fire", 280f, 260f, Tune.Elementals.Fire.Damage, 5f, (k - (n - 1) / 2f) * 0.13f, 1.6f);
    }

    protected override void Die()
    {
        if (!Dead) { G.Fx.Burst(GlobalPosition, new Color(1f, 0.55f, 0.15f), 26, 230, 2.6f, 0.6f, -80f); G.Fx.Flash(GlobalPosition, 26, new Color(1f, 0.6f, 0.2f), 0.2f); }
        base.Die();
    }
}

// ============================================================================ water

/// <summary>
/// A drifting mass of water with droplets circling it. It never leaves the water: it glides after
/// whoever is in reach, and from the water spits a spread of droplets at anyone it can see.
/// </summary>
public partial class WaterElemental : Enemy
{
    private float _atkCd = 1.5f, _wind = -1, _recover, _wanderA;
    private Vector2 _home;

    protected override bool UsesGravity => false;

    public WaterElemental() { MaxHp = Tune.Elementals.Water.Hp; BodyRadius = 12; ContactDamage = Tune.Elementals.Water.Contact; XpValue = Tune.Elementals.Water.Xp; }

    protected override void Setup()
    {
        DisplayName = "Water Elemental";
        _home = GlobalPosition;
        _wanderA = G.Range(0, Mathf.Tau);
        UseSprite("elem_water");
        MotionMode = MotionModeEnum.Floating;
    }

    protected override Color BloodColor => new(0.5f, 0.75f, 1f);

    protected override void Think(float dt)
    {
        _atkCd -= dt; _recover -= dt;
        var cave = G.Cave;
        var v = Velocity;
        // out of the water (thrown up by a blow), it sinks back
        if (!InWater && GlobalPosition.Y < cave.WaterY)
        {
            v = v.MoveToward(new Vector2(0, 220), 700 * dt);
            Velocity = v;
            return;
        }
        if (_wind >= 0)
        {
            _wind += dt;
            v = v.MoveToward(Vector2.Zero, 500 * dt);
            if (_wind > 0.55f)
            {
                _wind = -1; _recover = 0.5f; _atkCd = Tune.Elementals.Water.Cooldown * (Elite ? 0.7f : 1f);
                Anim.Once("slam", 3);
                Spit();
            }
        }
        else if (_recover > 0) v = v.MoveToward(Vector2.Zero, 400 * dt);
        else if (Awake && DistP < Aggro(380))
        {
            Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
            // glide toward the hero (keeping a little way off), staying under the surface
            var toward = ToP.Normalized();
            float d = DistP;
            var want = d > 150 ? toward * Tune.Elementals.Water.Speed : d < 80 ? -toward * 40 : Vector2.Zero;
            want += new Vector2(0, MathF.Sin(T * 3f) * 14);
            v = v.MoveToward(want, 260 * dt);
            if (_atkCd <= 0 && d < 260 && SeesP)
            {
                _wind = 0;
                Anim.Once("slam_windup", 3, 12f / (0.55f * 24f));
                G.Sfx.Play("splash", GlobalPosition, -4, 0.1f, 0.7f);
            }
        }
        else
        {
            _wanderA += G.Range(-1.5f, 1.5f) * dt;
            var wander = Vector2.Right.Rotated(_wanderA) * 30;
            if (GlobalPosition.DistanceTo(_home) > 140) wander = (_home - GlobalPosition).Normalized() * 45;
            v = v.MoveToward(wander, 160 * dt);
        }
        // stays under the surface
        if (GlobalPosition.Y < cave.WaterY + 10 && v.Y < 0) v.Y = 15;
        if (Math.Abs(v.X) > 4) Face = Math.Sign(v.X);
        Velocity = v;
    }

    private void Spit()
    {
        var from = GlobalPosition + new Vector2(Face * 6f, -6f) * Size;
        var dir = (P.GlobalPosition + new Vector2(0, -4) - from).Normalized();
        int n = Elite ? 5 : 3;
        for (int k = 0; k < n; k++)
        {
            var vel = dir.Rotated((k - (n - 1) / 2f) * 0.2f) * 250f;
            G.Spawn(new EnemyProjectile { Position = from, Vel = vel, Grav = 120, Damage = Tune.Elementals.Water.Damage * DmgK, Kind = "water", Radius = 4, Life = 2.2f, Source = this });
        }
        G.Sfx.Play("splash", GlobalPosition, -2, 0.1f, 1.3f);
        G.Fx.Bubbles(from, 4);
    }

    protected override bool Busy => _wind >= 0 || _recover > 0;
    public override bool Attacking => _wind >= 0;
    protected override void OnInterrupted() { _wind = -1; _recover = 0.4f; _atkCd = Math.Max(_atkCd, 1.2f); }
    protected override Vector2 DeathDrift => new Vector2(0, -20);
    protected override void Animate() => Anim.Loop(Velocity.Length() > 15 ? "walk" : "idle", Math.Clamp(Velocity.Length() / 60f, 0.8f, 1.6f));
    public override void _Draw() => DrawHealthBar();
}
