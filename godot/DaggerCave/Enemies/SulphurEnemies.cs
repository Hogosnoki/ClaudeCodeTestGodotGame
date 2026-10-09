using System;
using Godot;

namespace DaggerCave;

// ============================================================================ the Sulphur Springs' creatures

/// <summary>
/// A bladder of heavy yellow gas the size of a barrel, drifting down off the roof. It comes to hang just over your head, swells and lets a
/// cloud go under itself, and drifts off again. It does no harm of its own; the harm is the gas. Struck down, it bursts into a cloud; lit
/// (a fire bolt, a fireball, another blast), it goes up in a ball of fire. Kill it from afar, or with something cold.
/// </summary>
public partial class Gasbag : Enemy
{
    public override Element Element => Element.Gas;
    private int _s; // 0 drift, 1 swelling, 2 recover
    private float _st, _cd, _wob;
    private bool _fire;
    public override void NetState(NetIO io) => io.Sync(ref _s);

    /// <summary>For the 3D model: 0..1 how far it has swollen before it lets its cloud go.</summary>
    public float Swell => _s == 1 ? Math.Clamp(_st / Tune.Sulphur.Gasbag.VentWindup, 0f, 1f) : 0f;
    /// <summary>For the 3D model: it has just let its cloud go (shrinks back).</summary>
    public float Spent => _s == 2 ? Math.Clamp(1f - _st / 0.6f, 0f, 1f) : 0f;

    protected override bool UsesGravity => false;

    public Gasbag() { MaxHp = Tune.Sulphur.Gasbag.Hp; BodyRadius = 11; Size = 2f; ContactDamage = 0; XpValue = Tune.Sulphur.Gasbag.Xp; KnockResist = 0.2f; }

    protected override void Setup()
    {
        DisplayName = "Gasbag";
        _wob = G.Range(0, 10);
        _cd = G.Range(1.5f, Tune.Sulphur.Gasbag.VentCooldown);
        MotionMode = MotionModeEnum.Floating;
        UseSprite("gasbag");
    }

    protected override Color BloodColor => new(0.8f, 0.85f, 0.2f);
    public override string HitSound => "hit_wood";

    /// <summary>It goes: with a fire (a ball of flame) or without (a cloud).</summary>
    public void Pop(bool fire)
    {
        if (Dead || Puppet) return;
        _fire = fire;
        Hp = 0f;
        Die();
    }

    protected override void Die()
    {
        if (!Dead && !Puppet)
        {
            var c = new GasCloud { Position = GlobalPosition, Radius = Tune.Sulphur.CloudRadius * Tune.Sulphur.Gasbag.PopScale * Math.Max(1f, Size * 0.8f), Life = Tune.Sulphur.CloudLife * 0.8f, Source = this };
            G.Spawn(c);
            if (_fire) c.Light(0.02f);
            G.Sfx.Play("dodge", GlobalPosition, -2, 0.2f, 0.45f);
        }
        base.Die();
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        var to = ToP;
        var v = Velocity;
        var bob = new Vector2(MathF.Sin(T * 1.1f + _wob) * 14, MathF.Sin(T * 1.9f + _wob) * 9);
        float speed = Tune.Sulphur.Gasbag.Speed * (Elite ? 1.2f : 1f);
        if (_s == 0)
        {
            Vector2 want;
            if (!Awake || DistP > Aggro(380)) want = bob * 0.5f;
            else
            {
                // hang a little over the hero's head, near enough that what it lets go settles on them
                var hover = to + new Vector2(0, -40 * Size);
                want = hover.Length() < 10f ? bob : hover.Normalized() * Math.Min(speed, hover.Length() * 2.5f) + bob * 0.4f;
            }
            v = v.MoveToward(want, 110 * dt);
            if (Awake && _cd <= 0 && DistP < 120 && Math.Abs(to.Y) < 90 && SeesP)
            {
                _s = 1; _st = 0;
                G.Sfx.Play("gasp", GlobalPosition, -6, 0.1f, 0.5f);
            }
        }
        else if (_s == 1)
        {
            v = v.MoveToward(Vector2.Zero, 160 * dt);
            if (G.Chance(0.3f)) G.Fx.Burst(GlobalPosition + G.RandDir() * 9 * Size, new Color(0.9f, 0.9f, 0.3f, 0.6f), 1, 14, 1.8f, 0.5f, -10);
            if (_st > Tune.Sulphur.Gasbag.VentWindup)
            {
                _s = 2; _st = 0; _cd = Tune.Sulphur.Gasbag.VentCooldown * G.Range(0.8f, 1.3f) * (Elite ? 0.7f : 1f);
                G.Spawn(new GasCloud { Position = GlobalPosition + new Vector2(0, 26 * Size), Radius = Tune.Sulphur.CloudRadius * Tune.Sulphur.Gasbag.CloudScale * (Elite ? 1.3f : 1f), Source = this });
                G.Sfx.Play("dodge", GlobalPosition, -2, 0.2f, 0.35f);
                v = new Vector2(v.X, -70f);
            }
        }
        else
        {
            v = v.MoveToward(new Vector2(v.X * 0.5f, -25f), 120 * dt);
            if (_st > 0.6f) _s = 0;
        }
        // (it keeps off the water and out of the rock)
        if (GlobalPosition.Y > G.Cave.WaterY - 18 && v.Y > -40) v.Y = -40;
        if (Math.Abs(to.X) > 6) Face = Math.Sign(to.X);
        Velocity = v;
    }

    protected override bool Busy => _s != 0;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0.2f; } }
    protected override void Animate() => Anim.Loop(_s == 1 ? "swell" : "float");
    protected override Vector2 DeathDrift => new Vector2(0, -10);

    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// A pale, blind worm as thick as a leg that lies hid in a hole in the floor, with only a ring of a mouth to be seen. Come near and it
/// strikes out along a line at you, bites, breathes a puff of gas where it struck, and draws back into the hole (vulnerable while out).
/// </summary>
public partial class BrimstoneWorm : Eel
{
    protected override string SetName => "worm";
    protected override float LungeLen => Tune.Sulphur.Worm.Reach;
    protected override float LungeSpd => 420f;
    protected override Vector2 HomeOffset => new(0, 7);
    protected override Vector2 SightFrom => Home + new Vector2(0, -10);
    protected override bool WantsTarget => !P.InWater && Math.Abs(P.GlobalPosition.X - Home.X) < Tune.Sulphur.Worm.Trigger;

    public BrimstoneWorm()
    {
        MaxHp = Tune.Sulphur.Worm.Hp; BodyRadius = 9; Size = 1.25f; ContactDamage = Tune.Sulphur.Worm.Bite; XpValue = Tune.Sulphur.Worm.Xp; KnockResist = 1f;
    }

    protected override void Setup() { base.Setup(); DisplayName = "Brimstone Worm"; }
    protected override Color BloodColor => new(0.85f, 0.8f, 0.3f);

    protected override void OnRetract()
    {
        // (a puff of gas where it struck)
        if (GasCloud.All.Count < 22) G.Spawn(new GasCloud { Position = GlobalPosition + new Vector2(0, -8), Radius = Tune.Sulphur.CloudRadius * 0.7f, Life = Tune.Sulphur.CloudLife * 0.45f, Source = this });
    }
}

/// <summary>
/// A flat-headed salamander the yellow of the crust it lives on. It keeps its distance and spits acid in an arc; where the glob lands
/// the floor burns for a few seconds. It is quick to turn from anyone who closes with it.
/// </summary>
public partial class AcidNewt : Walker
{
    private int _s; // 0 walk, 1 windup, 2 recover
    private float _st, _cd = 1.8f;

    public AcidNewt() { MaxHp = Tune.Sulphur.Newt.Hp; BodyRadius = 10; Size = 1.3f; ContactDamage = Tune.Sulphur.Newt.Contact; XpValue = Tune.Sulphur.Newt.Xp; }

    protected override void Setup() { DisplayName = "Acid Newt"; UseSprite("newt"); }
    protected override Color BloodColor => new(0.78f, 0.9f, 0.2f);

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake || DistP > Aggro(420)) { Brake(ref v, dt, 600); break; }
                int dir = DirP;
                Face = dir;
                float d = DistP;
                // it likes 110-200 px between you and it, and backs off if you close
                float want = d > 190 ? dir : d < 100 ? -dir : 0;
                v = Stride(v, want, Tune.Sulphur.Newt.Speed * (Elite ? 1.2f : 1f), dt, 300);
                if (_cd <= 0 && d < 250 && d > 60 && Math.Abs(ToP.Y) < 120 && SeesP)
                {
                    _s = 1; _st = 0;
                    Anim.Once("spit_windup", 3, 8f / (Tune.Sulphur.Newt.SpitWindup * 24f));
                    G.Sfx.Play("spider", GlobalPosition, -6, 0.2f, 0.7f);
                }
                break;
            case 1:
                Brake(ref v, dt, 1100);
                Face = DirP;
                if (_st > Tune.Sulphur.Newt.SpitWindup)
                {
                    _s = 2; _st = 0; _cd = Tune.Sulphur.Newt.SpitCooldown * G.Range(0.85f, 1.2f) * (Elite ? 0.7f : 1f);
                    Anim.Once("spit", 3);
                    Spit();
                }
                break;
            default:
                Brake(ref v, dt);
                if (_st > 0.5f) _s = 0;
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    /// <summary>A glob lobbed at the hero (a fan of three, for an elite).</summary>
    private void Spit()
    {
        G.Sfx.Play("splash", GlobalPosition, -3, 0.1f, 1.5f);
        var from = GlobalPosition + new Vector2(Face * 12f * Size, -4f * Size);
        var to = P.GlobalPosition + new Vector2(0, -4);
        const float grav = 520f;
        float t = Math.Clamp(from.DistanceTo(to) / 260f, 0.4f, 1.3f);
        var vel = new Vector2((to.X - from.X) / t, (to.Y - from.Y) / t - 0.5f * grav * t);
        int n = Elite ? 3 : 1;
        for (int k = 0; k < n; k++)
            G.Spawn(new EnemyProjectile { Position = from, Vel = vel.Rotated((k - (n - 1) / 2f) * 0.16f), Grav = grav, Damage = Tune.Sulphur.Newt.SpitDamage * DmgK, Kind = "acid", Radius = 5, Life = 2.6f, Source = this });
        G.Fx.Burst(from, new Color(0.78f, 0.92f, 0.2f), 6, 80, 1.8f, 0.3f);
    }

    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 8 ? "walk" : "idle", Math.Clamp(avx / 50f, 0.7f, 1.5f));
        Anim.AllowTurns = _s == 0;
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// The guardian of the springs: a colossus the colour of old brimstone. It fights as the Cavern Colossus does (leaping slams, the roar that
/// shakes the roof down, the charge), and from time to time breathes out: a ring of great clouds across its chamber that hang there. Light
/// them (they are fuel) and the whole chamber goes up around it.
/// </summary>
public partial class VentColossus : CavernColossus
{
    private float _breath = 6f;

    protected override void Setup() { base.Setup(); DisplayName = "Vent Colossus"; }

    protected override void Think(float dt)
    {
        base.Think(dt);
        _breath -= dt;
        if (_breath > 0f || Busy || Dead || Room == null) return;
        _breath = Tune.Sulphur.Colossus.ExhaleCooldown * G.Range(0.8f, 1.2f) * (Phase2 ? 0.7f : 1f);
        Exhale();
    }

    /// <summary>A ring of clouds across the chamber, low enough that they sit where the hero fights.</summary>
    private void Exhale()
    {
        G.Sfx.Play("roar", GlobalPosition, -2, 0.1f, 0.6f);
        G.Fx.AddShake(4);
        G.Fx.Text(GlobalPosition + new Vector2(0, -60), "THE AIR TURNS", new Color(0.95f, 0.9f, 0.3f), 13, 1.2f);
        int n = (int)Tune.Sulphur.Colossus.Clouds + (Phase2 ? 2 : 0);
        for (int k = 0; k < n; k++)
        {
            float x = Room.Center.X + (-0.8f + 1.6f * (k + G.Range(0.2f, 0.8f)) / n) * Room.RxPx;
            if (!G.Cave.FindFloor(new Vector2(x, Room.Floor.Y - 60), 140f, out var fl)) continue;
            G.Spawn(new GasCloud { Position = fl + new Vector2(0, -34), Radius = Tune.Sulphur.CloudRadius * 1.15f, Life = Tune.Sulphur.CloudLife * 1.2f, Source = this });
        }
    }
}
