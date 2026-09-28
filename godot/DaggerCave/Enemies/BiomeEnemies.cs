using System;
using Godot;

namespace DaggerCave;

/// <summary>Shared footwork for creatures that walk: chase, climb low walls, hop gaps, paddle out of water.</summary>
public abstract partial class Walker : Enemy
{
    protected int DirP => Math.Sign(ToP.X) == 0 ? (int)Face : Math.Sign(ToP.X);

    /// <summary>Accelerates toward want * speed; jumps walls and gaps that lie toward the player.</summary>
    protected Vector2 Stride(Vector2 v, float want, float speed, float dt, float jump = 330f)
    {
        v.X = Mathf.MoveToward(v.X, want * speed, 900 * dt);
        if (!IsOnFloor() || want == 0) return v;
        if (IsOnWall()) v.Y = -jump;
        else if (!G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 30)) && !G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 60)) && ToP.Y < 40)
            v.Y = -jump * 0.85f;
        return v;
    }

    /// <summary>Paddles toward the surface and the player; true while in water.</summary>
    protected bool Paddle(float dt)
    {
        if (!InWater) return false;
        Velocity = Velocity.MoveToward(new Vector2(DirP * 50, -70), 300 * dt);
        return true;
    }

    protected void Brake(ref Vector2 v, float dt, float rate = 1200f) => v.X = Mathf.MoveToward(v.X, 0, rate * dt);
}

// ============================================================================ rat

/// <summary>Scurries in packs; crouches for a quarter second, then lunges with a bite.</summary>
public partial class Rat : Walker
{
    private int _s; // 0 run, 1 windup, 2 lunge, 3 recover
    private float _st, _cd = 0.5f;

    public Rat() { MaxHp = Tune.Rat.Hp; BodyRadius = 6; ContactDamage = Tune.Rat.BiteDamage; XpValue = Tune.Rat.Xp; }

    protected override void Setup() { DisplayName = "Rat"; UseSprite("rat"); }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Approach => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Rat.RunSpeed * (Elite ? 1.1f : 1f), dt, 300);
                if (Intent == Bite && CanAct(Bite))
                {
                    _s = 1; _st = 0; Face = DirP;
                    Anim.Once("windup", 3, 5f / (Tune.Rat.Windup * 24f));
                    G.Sfx.Play("spider", GlobalPosition, -8, 0.2f, 1.6f);
                    Consume();
                }
                break;
            case 1:
                Brake(ref v, dt, 1400);
                if (_st > Tune.Rat.Windup)
                {
                    _s = 2; _st = 0; _cd = Tune.Rat.Cooldown;
                    v = new Vector2(Face * Tune.Rat.LungeSpeed, -170);
                    Anim.Once("bite", 3);
                }
                break;
            case 2:
                if ((_st > 0.12f && IsOnFloor()) || _st > 0.6f) { _s = 3; _st = 0; }
                break;
            default:
                Brake(ref v, dt);
                if (_st > 0.35f) _s = 0;
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private const int Idle = 0, Approach = 1, Retreat = 2, Bite = 3;
    private static readonly string[] Moves = { "idle", "approach", "retreat", "bite" };
    protected override string BrainName => "rat";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Rat.Cooldown, 0, 1);
    protected override bool CanAct(int a) => a != Bite || (_cd <= 0 && IsOnFloor());
    protected override bool IsAttack(int a) => a == Bite;
    public override bool Attacking => _s is 1 or 2;
    protected override void OnInterrupted() { _s = 3; _st = 0; }
    protected override bool Striking => _s == 2;

    protected override int Teacher()
    {
        if (DistP > Aggro(420)) return Idle;
        if (_cd <= 0 && DistP < 64 && Math.Abs(ToP.Y) < 28) return Bite;
        return Approach;
    }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 15 ? "run" : "idle", Math.Clamp(avx / 120f, 0.7f, 1.5f));
    }

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ bear

/// <summary>A hulking brute: rears up for a heavy swipe, or roars and charges, stunning itself on walls.</summary>
public partial class Bear : Walker
{
    private enum S { Walk, SwipeWindup, Swipe, ChargeWindup, Charge, Stunned, BiteWindup, Bite }
    private S _s;
    public override void NetState(NetIO io) => io.SyncByte(ref _s);
    private float _st, _chargeCd = 2.5f, _swipeCd, _biteCd;

    public Bear() { MaxHp = Tune.Bear.Hp; BodyRadius = 14; ContactDamage = Tune.Bear.Contact; XpValue = Tune.Bear.Xp; KnockResist = 0.6f; }
    /// <summary>For the 3D stage: stars circle a stunned bear's head.</summary>
    public bool Stunned => _s == S.Stunned;

    protected override void Setup() { DisplayName = "Bear"; UseSprite("bear"); }
    protected override Color BloodColor => new(0.6f, 0.1f, 0.1f);

    private void Go(S s) { _s = s; _st = 0; }

    protected override void Think(float dt)
    {
        _st += dt; _chargeCd -= dt; _swipeCd -= dt; _biteCd -= dt;
        if (InWater) { Tread(dt); return; }
        if (_s is S.BiteWindup or S.Bite) Go(S.Walk);
        var v = Velocity;
        switch (_s)
        {
            case S.Walk:
            {
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Advance => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Bear.WalkSpeed * (Elite ? 1.15f : 1f), dt, 340);
                if (Intent == Swipe && CanAct(Swipe))
                {
                    Face = DirP; Go(S.SwipeWindup);
                    Anim.Once("rear", 3, 8f / (Tune.Bear.SwipeWindup * 24f));
                    G.Sfx.Play("goblin", GlobalPosition, -2, 0.1f, 0.55f);
                    Consume();
                }
                else if (Intent == Charge && CanAct(Charge))
                {
                    Face = DirP; Go(S.ChargeWindup);
                    Anim.Once("roar", 3, 10f / (Tune.Bear.ChargeWindup * 24f));
                    G.Sfx.Play("roar", GlobalPosition, -6, 0.1f, 1.5f);
                    Consume();
                }
                break;
            }
            case S.SwipeWindup:
                Brake(ref v, dt);
                if (_st > Tune.Bear.SwipeWindup)
                {
                    Go(S.Swipe);
                    _swipeCd = 1.2f;
                    Anim.Once("swipe", 3);
                    G.Sfx.Play("swing_heavy", GlobalPosition, -2, 0.1f, 0.6f);
                    v.X = Face * 90;
                    var rel = ToP;
                    if (rel.X * Face > -8 && Math.Abs(rel.X) < 46 * Size && Math.Abs(rel.Y) < 34 * Size)
                        P.Hurt(Tune.Bear.SwipeDamage * DmgK * (Elite ? 1.3f : 1f), GlobalPosition, 260, this);
                    G.Fx.Swoosh(GlobalPosition + new Vector2(Face * 22 * Size, -6), Face, 30 * Size, new Color(1f, 0.9f, 0.8f, 0.8f));
                }
                break;
            case S.Swipe:
                Brake(ref v, dt, 600);
                if (_st > 0.55f) Go(S.Walk);
                break;
            case S.ChargeWindup:
                v.X = Mathf.MoveToward(v.X, -Face * 25, 600 * dt);
                if (_st > Tune.Bear.ChargeWindup) { Go(S.Charge); _chargeCd = Tune.Bear.ChargeCooldown; }
                break;
            case S.Charge:
                v.X = Face * Tune.Bear.ChargeSpeed * (Elite ? 1.1f : 1f);
                if (G.Chance(0.4f)) G.Fx.Dust(GlobalPosition + new Vector2(-Face * 12, BodyRadius * Size), 1);
                if (IsOnWall() || _st > 1.5f)
                {
                    bool wall = IsOnWall();
                    Go(wall ? S.Stunned : S.Walk);
                    if (wall)
                    {
                        G.Sfx.Play("slam", GlobalPosition, -2);
                        G.Fx.AddShake(6);
                        G.Fx.Burst(GlobalPosition + new Vector2(Face * 16, 0), new Color(0.6f, 0.55f, 0.5f), 12, 160, 2.5f, 0.4f);
                        v = new Vector2(-Face * 90, -140);
                        Anim.Once("hurt", 5);
                    }
                }
                break;
            case S.Stunned:
                Brake(ref v, dt, 500);
                if (_st > 1.2f) Go(S.Walk);
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    /// <summary>
    /// In water a bear can't rear up or charge: it treads water toward you, head up, and when
    /// close draws its head back and lunges with a bite.
    /// </summary>
    private void Tread(float dt)
    {
        var v = Velocity;
        var to = ToP;
        float d = to.Length();
        switch (_s)
        {
            case S.BiteWindup:
                // head drawn back, paddling in place
                v = v.MoveToward(Vector2.Zero, 400 * dt);
                if (_st > Tune.Bear.BiteWindup)
                {
                    Go(S.Bite);
                    _biteCd = Tune.Bear.BiteCooldown;
                    var dir = d > 1 ? to / d : new Vector2(Face, 0);
                    v = dir * 210f;
                    G.Sfx.Play("goblin", GlobalPosition, -1, 0.1f, 0.45f);
                    G.Sfx.Play("hit", GlobalPosition, -8, 0.1f, 1.4f);
                    G.Fx.Bubbles(GlobalPosition + new Vector2(Face * 18 * Size, -6), 6);
                    if (d < (Tune.Bear.BiteReach + 10) * Size && to.X * Face > -10)
                        P.Hurt(Tune.Bear.BiteDamage * DmgK * (Elite ? 1.3f : 1f), GlobalPosition, 200, this);
                }
                break;
            case S.Bite:
                v = v.MoveToward(Vector2.Zero, 500 * dt);
                if (_st > 0.45f) Go(S.Walk);
                break;
            default:
            {
                // (a swipe or charge started on land ends when it goes in)
                if (_s != S.Walk) Go(S.Walk);
                float want = !Awake ? 0 : Intent switch { Advance or Swipe => 1, Retreat => -1, _ => 0 };
                var dir = d > 1 ? to / d : Vector2.Zero;
                // toward (or away from) you, heaviest at the surface: it swims down only after you
                var target = new Vector2(Math.Sign(dir.X) * Math.Max(0.3f, Math.Abs(dir.X)), dir.Y) * want * Tune.Bear.SwimSpeed * (Elite ? 1.15f : 1f);
                if (want <= 0 || dir.Y < 0.2f) target.Y -= 30f; // buoyant
                v = v.MoveToward(target, 260 * dt);
                if (Math.Abs(dir.X) > 0.1f && want != 0) Face = Math.Sign(dir.X) * want;
                if (Awake && Intent == Swipe && CanAct(Swipe) && d < (Tune.Bear.BiteReach + 14) * Size)
                {
                    Face = Math.Sign(to.X) == 0 ? Face : Math.Sign(to.X);
                    Go(S.BiteWindup);
                    // (its roar clip is the bite in water: see BearDesign)
                    Anim.Once("roar", 3, 10f / (Tune.Bear.BiteWindup * 2f * 24f));
                    G.Sfx.Play("goblin", GlobalPosition, -5, 0.1f, 0.5f);
                    Consume();
                }
                if (G.Chance(0.03f)) G.Fx.Bubbles(GlobalPosition + new Vector2(0, -BodyRadius * Size * 0.5f), 1);
                break;
            }
        }
        Velocity = v;
    }

    private const int Stand = 0, Advance = 1, Retreat = 2, Swipe = 3, Charge = 4;
    private static readonly string[] Moves = { "stand", "advance", "retreat", "swipe", "charge" };
    protected override string BrainName => "bear";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != S.Walk || (!IsOnFloor() && !InWater);
    protected override float AttackReady => InWater ? 1 - Math.Clamp(_biteCd / Tune.Bear.BiteCooldown, 0, 1) : 1 - Math.Clamp(_chargeCd / Tune.Bear.ChargeCooldown, 0, 1);
    protected override bool CanAct(int a) => a switch
    {
        // (in water the swipe is a bite, and there's no charging)
        Swipe => InWater ? _biteCd <= 0 : _swipeCd <= 0 && IsOnFloor(),
        Charge => _chargeCd <= 0 && IsOnFloor() && !InWater,
        _ => true,
    };
    protected override bool IsAttack(int a) => a >= Swipe;
    public override bool Attacking => _s is S.SwipeWindup or S.ChargeWindup or S.Charge or S.BiteWindup || (_s == S.Swipe && _st < 0.12f) || (_s == S.Bite && _st < 0.12f);
    // a charge broken off by a shield leaves it reeling, as if it had hit a wall
    protected override void OnInterrupted() => Go(_s == S.Charge ? S.Stunned : S.Walk);
    protected override bool Striking => _s == S.Charge;

    protected override int Teacher()
    {
        if (DistP > Aggro(460)) return Stand;
        if (InWater) return _biteCd <= 0 && DistP < (Tune.Bear.BiteReach + 14) * Size ? Swipe : Advance;
        if (_swipeCd <= 0 && DistP < 50 * Size && Math.Abs(ToP.Y) < 34) return Swipe;
        if (_chargeCd <= 0 && DistP > 110 && DistP < 320 && Math.Abs(ToP.Y) < 40 && SeesP) return Charge;
        return Advance;
    }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        // (in water, "walk" is its paddling: see BearDesign)
        if (InWater) Anim.Loop(Velocity.Length() > 12 ? "walk" : "idle", 0.8f);
        else Anim.Loop(_s == S.Charge ? "run" : _s == S.Stunned ? "idle" : avx > 10 ? "walk" : "idle", _s == S.Charge ? 1.4f : Math.Clamp(avx / 60f, 0.7f, 1.4f));
        Anim.AllowTurns = _s is S.Walk;
    }

    public override void _Draw()
    {
        if (_s == S.Stunned)
            for (int k = 0; k < 3; k++)
            {
                float a = T * 5 + k * Mathf.Tau / 3;
                DrawCircle(new Vector2(MathF.Cos(a) * 12, -HitRadius - 6 + MathF.Sin(a) * 3), 2, new Color(1f, 0.95f, 0.5f, 0.9f));
            }
        DrawHealthBar();
    }
}

// ============================================================================ scorpion

/// <summary>Scuttles close, arches its tail over its back, and stings forward.</summary>
public partial class Scorpion : Walker
{
    private int _s; // 0 walk, 1 windup, 2 recover
    private float _st, _cd = 0.8f;

    public Scorpion() { MaxHp = Tune.Scorpion.Hp; BodyRadius = 9; ContactDamage = Tune.Scorpion.Contact; XpValue = Tune.Scorpion.Xp; KnockResist = 0.2f; }

    protected override void Setup() { DisplayName = "Scorpion"; UseSprite("scorpion"); }
    protected override Color BloodColor => new(0.55f, 0.8f, 0.2f);

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Advance => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Scorpion.WalkSpeed * (Elite ? 1.15f : 1f), dt, 310);
                if (Intent == Sting && CanAct(Sting))
                {
                    _s = 1; _st = 0; Face = DirP;
                    Anim.Once("sting_windup", 3, 8f / (Tune.Scorpion.StingWindup * 24f));
                    G.Sfx.Play("spider", GlobalPosition, -6, 0.1f, 0.8f);
                    Consume();
                }
                break;
            case 1:
                Brake(ref v, dt);
                if (_st > Tune.Scorpion.StingWindup)
                {
                    _s = 2; _st = 0; _cd = Tune.Scorpion.StingCooldown * (Elite ? 0.7f : 1f);
                    Anim.Once("sting", 3);
                    G.Sfx.Play("spike", GlobalPosition, -4);
                    v.X = Face * 60;
                    var rel = ToP;
                    float reach = Tune.Scorpion.StingReach * Size;
                    if (rel.X * Face > -6 && Math.Abs(rel.X) < reach + 6 && rel.Y > -reach && rel.Y < 24 * Size)
                        P.Hurt(Tune.Scorpion.StingDamage * DmgK * (Elite ? 1.35f : 1f), GlobalPosition, 180, this);
                    G.Fx.Spark(GlobalPosition + new Vector2(Face * reach, -8 * Size), new Color(0.9f, 1f, 0.5f));
                }
                break;
            default:
                Brake(ref v, dt);
                if (_st > 0.45f) _s = 0;
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private const int Stand = 0, Advance = 1, Retreat = 2, Sting = 3;
    private static readonly string[] Moves = { "stand", "advance", "retreat", "sting" };
    protected override string BrainName => "scorpion";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Scorpion.StingCooldown, 0, 1);
    protected override bool CanAct(int a) => a != Sting || (_cd <= 0 && IsOnFloor());
    protected override bool IsAttack(int a) => a == Sting;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override int Teacher()
    {
        if (DistP > Aggro(420)) return Stand;
        if (_cd <= 0 && Math.Abs(ToP.X) < Tune.Scorpion.StingReach * Size && ToP.Y > -Tune.Scorpion.StingReach * Size && ToP.Y < 20) return Sting;
        return Math.Abs(ToP.X) > 18 ? Advance : Stand;
    }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 10 ? "walk" : "idle", Math.Clamp(avx / 70f, 0.7f, 1.5f));
    }

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ hornet

/// <summary>Circles overhead, takes aim with a buzz, then dives in a straight line.</summary>
public partial class Hornet : Enemy
{
    private int _s; // 0 fly, 1 aim, 2 dive, 3 pull out
    private float _st, _cd = 1.2f, _wob, _buzzT;
    private Vector2 _diveDir;

    protected override bool UsesGravity => false;

    public Hornet() { MaxHp = Tune.Hornet.Hp; BodyRadius = 7; ContactDamage = Tune.Hornet.Contact; XpValue = Tune.Hornet.Xp; }

    protected override void Setup()
    {
        DisplayName = "Hornet";
        _wob = G.Range(0, 10);
        MotionMode = MotionModeEnum.Floating;
        UseSprite("hornet");
    }

    protected override Color BloodColor => new(0.9f, 0.8f, 0.2f);

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt; _buzzT -= dt;
        var to = ToP;
        var dir = to.LengthSquared() > 1 ? to.Normalized() : Vector2.Up;
        // it hums as it hovers (you hear it coming)
        if (Awake && _s == 0 && _buzzT <= 0 && DistP < 500) { _buzzT = G.Range(0.8f, 1.5f); G.Sfx.Play("buzz", GlobalPosition, -13, 0.1f, 1f); }
        switch (_s)
        {
            case 0:
            {
                if (!Awake) { Velocity = Velocity.MoveToward(new Vector2(MathF.Sin(T * 2 + _wob) * 20, MathF.Sin(T * 3 + _wob) * 12), 300 * dt); break; }
                // hold a perch above and to the side of the player
                var perch = P.GlobalPosition + new Vector2(-Math.Sign(to.X == 0 ? 1 : to.X) * 70, -90) + new Vector2(MathF.Sin(T * 2.3f + _wob) * 30, MathF.Sin(T * 3.1f + _wob) * 16);
                var desired = Intent switch
                {
                    Approach => (perch - GlobalPosition).LimitLength(Tune.Hornet.FlySpeed * 1.4f),
                    Retreat => (-dir + new Vector2(0, -0.6f)).Normalized() * Tune.Hornet.FlySpeed,
                    _ => new Vector2(MathF.Sin(T * 3 + _wob) * 30, MathF.Sin(T * 5 + _wob) * 20),
                };
                Velocity = Velocity.MoveToward(desired, 500 * dt);
                if (Intent == Dive && CanAct(Dive))
                {
                    _s = 1; _st = 0;
                    Anim.Once("aim", 3, 6f / (Tune.Hornet.AimTime * 24f));
                    // taking aim: the buzz rises to an angry whine
                    G.Sfx.Play("buzz", GlobalPosition, -1, 0.05f, 1.3f);
                    Consume();
                }
                break;
            }
            case 1:
                Velocity = Velocity.MoveToward(-dir * 40, 800 * dt); // draws back
                _diveDir = (to + P.Velocity * 0.15f).Normalized();
                if (_st > Tune.Hornet.AimTime) { _s = 2; _st = 0; _cd = Tune.Hornet.DiveCooldown; }
                break;
            case 2:
                Velocity = _diveDir * Tune.Hornet.DiveSpeed * (Elite ? 1.15f : 1f);
                if (G.Chance(0.5f)) G.Fx.Trail(GlobalPosition, new Color(1f, 0.9f, 0.4f, 0.5f));
                if (_st > 0.75f || IsOnWall() || IsOnFloor() || IsOnCeiling()) { _s = 3; _st = 0; }
                break;
            default:
                Velocity = Velocity.MoveToward(new Vector2(0, -90), 600 * dt);
                if (_st > 0.5f) _s = 0;
                break;
        }
        if (GlobalPosition.Y > G.Cave.WaterY - 14) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y, -80));
        if (_s != 2 && Math.Abs(to.X) > 4) Face = Math.Sign(to.X);
        else if (_s == 2 && _diveDir.X != 0) Face = Math.Sign(_diveDir.X);
    }

    private const int Hover = 0, Approach = 1, Retreat = 2, Dive = 3;
    private static readonly string[] Moves = { "hover", "approach", "retreat", "dive" };
    protected override string BrainName => "hornet";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0;
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Hornet.DiveCooldown, 0, 1);
    protected override bool CanAct(int a) => a != Dive || _cd <= 0;
    protected override bool IsAttack(int a) => a == Dive;
    public override bool Attacking => _s is 1 or 2;
    protected override void OnInterrupted() { if (_s is 1 or 2) { _s = 3; _st = 0; } }
    protected override bool Striking => _s == 2;

    public override void Engage() { base.Engage(); _cd = 1.5f; }

    protected override int Teacher()
    {
        if (DistP > Aggro(380)) return Hover;
        if (_cd <= 0 && DistP < 200 && SeesP) return Dive;
        return Approach;
    }

    protected override void Animate()
    {
        Anim.Loop(_s == 2 ? "dive" : "fly", 1.2f);
        Anim.AllowTurns = _s == 0;
    }

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ skeleton

/// <summary>Plods forward and slashes with a rusty blade. Sometimes, felled, it pulls itself back together.</summary>
public partial class Skeleton : Walker
{
    private int _s; // 0 walk, 1 windup, 2 recover, 3 collapsed
    public override void NetState(NetIO io) => io.Sync(ref _s);
    private float _st, _cd = 0.6f;
    private bool _reassembled;

    public Skeleton() { MaxHp = Tune.Skeleton.Hp; BodyRadius = 9; ContactDamage = Tune.Skeleton.Contact; XpValue = Tune.Skeleton.Xp; }

    protected override void Setup() { DisplayName = "Skeleton"; UseSprite("skeleton"); }
    protected override Color BloodColor => new(0.85f, 0.82f, 0.72f);
    public override bool CanBeHit => _s != 3;

    protected override void Die()
    {
        if (!_reassembled && G.Chance(Elite ? 0.8f : Tune.Skeleton.ReassembleChance))
        {
            // it clatters apart... for now
            _reassembled = true;
            _s = 3; _st = 0;
            Hp = 1;
            ContactActive = false;
            Anim.CancelOnce();
            Anim.Once("death", 99);
            G.Sfx.Play("rock", GlobalPosition, -4, 0.1f, 1.4f);
            G.Fx.Burst(GlobalPosition, BloodColor, 10, 140, 2.2f, 0.5f);
            return;
        }
        base.Die();
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Advance => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Skeleton.WalkSpeed * (Elite ? 1.15f : 1f), dt, 320);
                if (Intent == Slash && CanAct(Slash))
                {
                    _s = 1; _st = 0; Face = DirP;
                    Anim.Once("windup", 3, 8f / (Tune.Skeleton.Windup * 24f));
                    G.Sfx.Play("clink", GlobalPosition, -10, 0.2f, 0.6f);
                    Consume();
                }
                break;
            case 1:
                Brake(ref v, dt);
                if (_st > Tune.Skeleton.Windup)
                {
                    _s = 2; _st = 0; _cd = 1.3f;
                    Anim.Once("slash", 3);
                    G.Sfx.Play("swing", GlobalPosition, -4, 0.1f, 0.8f);
                    v.X = Face * 70;
                    var rel = ToP;
                    if (rel.X * Face > -8 && Math.Abs(rel.X) < 40 * Size && Math.Abs(rel.Y) < 30 * Size)
                        P.Hurt(Tune.Skeleton.SlashDamage * DmgK * (Elite ? 1.4f : 1f), GlobalPosition, source: this);
                    G.Fx.Swoosh(GlobalPosition + new Vector2(Face * 18 * Size, -8), Face, 24 * Size, new Color(0.9f, 0.95f, 1f, 0.7f));
                }
                break;
            case 2:
                Brake(ref v, dt);
                if (_st > 0.2f && _st - dt <= 0.2f) Anim.Once("recover", 2);
                if (_st > Tune.Skeleton.Recover) _s = 0;
                break;
            default:
                Brake(ref v, dt, 800);
                if (_st > Tune.Skeleton.ReassembleTime)
                {
                    _s = 0;
                    Hp = MaxHp * Tune.Skeleton.ReassembleHp;
                    ContactActive = true;
                    Anim.CancelOnce();
                    Anim.Once("recover", 5);
                    G.Sfx.Play("rock", GlobalPosition, -6, 0.1f, 1.8f);
                    G.Fx.Ring(GlobalPosition, 24, new Color(0.7f, 0.9f, 1f, 0.7f));
                    G.Fx.Text(GlobalPosition + new Vector2(0, -26), "RISES AGAIN", new Color(0.8f, 0.9f, 1f), 10);
                }
                else if (G.Chance(0.1f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-10, 10), 6), new Color(0.6f, 0.8f, 1f, 0.6f), 1, 30, 1.6f, 0.5f, -80);
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private const int Stand = 0, Advance = 1, Retreat = 2, Slash = 3;
    private static readonly string[] Moves = { "stand", "advance", "retreat", "slash" };
    protected override string BrainName => "skeleton";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    protected override bool CanAct(int a) => a != Slash || (_cd <= 0 && IsOnFloor());
    protected override bool IsAttack(int a) => a == Slash;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override int Teacher()
    {
        if (DistP > Aggro(440)) return Stand;
        if (_cd <= 0 && DistP < 38 * Size && Math.Abs(ToP.Y) < 30) return Slash;
        return Math.Abs(ToP.X) > 10 ? Advance : Stand;
    }

    protected override void Animate()
    {
        if (_s == 3) return;
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 10 ? "walk" : "idle", Math.Clamp(avx / 60f, 0.7f, 1.4f));
    }

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ sporeling

/// <summary>A waddling fungus that puffs clouds of choking spores (and bursts into one when killed).</summary>
public partial class Sporeling : Walker
{
    private int _s; // 0 walk, 1 windup, 2 recover
    private float _st, _cd = 1.5f;

    public Sporeling() { MaxHp = Tune.Sporeling.Hp; BodyRadius = 9; ContactDamage = Tune.Sporeling.Contact; XpValue = Tune.Sporeling.Xp; }

    protected override void Setup() { DisplayName = "Sporeling"; UseSprite("sporeling"); }
    protected override Color BloodColor => new(0.7f, 0.45f, 0.9f);

    protected override void Die()
    {
        if (!Dead) G.Spawn(new SporeCloud { Position = GlobalPosition, Radius = Tune.Sporeling.CloudRadius * 0.8f, Life = Tune.Sporeling.CloudTime * 0.7f });
        base.Die();
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Advance => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Sporeling.WalkSpeed, dt, 300);
                if (Intent == Puff && CanAct(Puff))
                {
                    _s = 1; _st = 0; Face = DirP;
                    Anim.Once("puff_windup", 3, 8f / (Tune.Sporeling.PuffWindup * 24f));
                    Consume();
                }
                break;
            case 1:
                Brake(ref v, dt);
                if (_st > Tune.Sporeling.PuffWindup)
                {
                    _s = 2; _st = 0; _cd = Tune.Sporeling.PuffCooldown * (Elite ? 0.7f : 1f);
                    Anim.Once("puff", 3);
                    G.Sfx.Play("dodge", GlobalPosition, -4, 0.2f, 0.6f);
                    G.Spawn(new SporeCloud { Position = GlobalPosition + new Vector2(Face * 14, -6), Radius = Tune.Sporeling.CloudRadius * (Elite ? 1.5f : 1f), Life = Tune.Sporeling.CloudTime, Source = this });
                }
                break;
            default:
                Brake(ref v, dt);
                if (_st > 0.6f) _s = 0;
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private const int Stand = 0, Advance = 1, Retreat = 2, Puff = 3;
    private static readonly string[] Moves = { "stand", "advance", "retreat", "puff" };
    protected override string BrainName => "sporeling";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Sporeling.PuffCooldown, 0, 1);
    protected override bool CanAct(int a) => a != Puff || (_cd <= 0 && IsOnFloor());
    protected override bool IsAttack(int a) => a == Puff;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override int Teacher()
    {
        if (DistP > Aggro(380)) return Stand;
        if (_cd <= 0 && DistP < 70 && Math.Abs(ToP.Y) < 40) return Puff;
        return Advance;
    }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 8 ? "walk" : "idle", Math.Clamp(avx / 45f, 0.7f, 1.4f));
    }

    public override void _Draw() => DrawHealthBar();
}

// ============================================================================ frost wraith

/// <summary>
/// A haunting more than a hunter: it drifts at a distance, looms in close now and then with a
/// shriek (arms spread, jaw wide, eyes blazing, but doing no harm), and only rarely, after a long
/// and obvious wind-up, looses a single ice shard (an elite, a fan of three).
/// </summary>
public partial class FrostWraith : Enemy
{
    private int _s; // 0 drift, 1 casting, 2 looming
    public override void NetState(NetIO io) => io.Sync(ref _s);
    private float _st, _cd = Tune.Wraith.FirstCast, _wob, _loomCd;
    private Vector2 _loomAt;

    protected override bool UsesGravity => false;

    /// <summary>For the 3D model: it is looming over the player right now.</summary>
    public bool Looming => _s == 2;
    /// <summary>For the 3D model: 0..1, how far into the loom (rises quickly, falls away at the end).</summary>
    public float LoomAmount => _s != 2 ? 0f : W3.Smooth01(_st * 4f) * W3.Smooth01((Tune.Wraith.LoomTime - _st) * 4f);

    public FrostWraith() { MaxHp = Tune.Wraith.Hp; BodyRadius = 9; ContactDamage = Tune.Wraith.Contact; XpValue = Tune.Wraith.Xp; }

    protected override void Setup()
    {
        DisplayName = "Frost Wraith";
        _wob = G.Range(0, 10);
        _cd = Tune.Wraith.FirstCast * G.Range(0.8f, 1.4f);
        _loomCd = G.Range(2f, Tune.Wraith.LoomCooldown);
        MotionMode = MotionModeEnum.Floating;
        UseSprite("wraith");
    }

    protected override Color BloodColor => new(0.7f, 0.9f, 1f);

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt; _loomCd -= dt;
        var to = ToP;
        var dir = to.LengthSquared() > 1 ? to.Normalized() : Vector2.Up;
        if (_s == 0)
        {
            float d = to.Length();
            var bob = new Vector2(MathF.Sin(T * 1.3f + _wob) * 20, MathF.Sin(T * 2.1f + _wob) * 14);
            var desired = !Awake ? bob : Intent switch
            {
                Approach => dir * Tune.Wraith.FlySpeed + bob,
                Retreat => -dir * Tune.Wraith.FlySpeed + bob,
                // keep 150-200 px away, a little above: always there at the edge of the light
                _ => dir * (d - 175) * 0.7f + new Vector2(0, -26) + bob,
            };
            Velocity = Velocity.MoveToward(desired.LimitLength(Tune.Wraith.FlySpeed), 240 * dt);
            if (Awake && Intent == Cast && CanAct(Cast))
            {
                _s = 1; _st = 0;
                Anim.Once("cast_windup", 3, 10f / (Tune.Wraith.CastWindup * 24f));
                G.Sfx.Play("gasp", GlobalPosition, -4, 0.1f, 1.3f);
                Consume();
            }
            else if (Awake && _loomCd <= 0 && SeesP && d < 300 && d > 90)
            {
                // loom: rush in to hang just out of reach, shrieking, then fall back
                _s = 2; _st = 0;
                _loomAt = P.GlobalPosition + new Vector2(-Math.Sign(to.X == 0 ? 1 : to.X) * Tune.Wraith.LoomDistance, -30);
                G.Sfx.Play("gasp", GlobalPosition, 0, 0.05f, 0.55f);
                G.Sfx.Play("roar", GlobalPosition, -8, 0.05f, 2.4f);
                Anim.Flash(0.5f);
                Anim.FlashColor = new Color(0.7f, 0.95f, 1f);
            }
        }
        else if (_s == 1)
        {
            Velocity = Velocity.MoveToward(Vector2.Zero, 300 * dt);
            if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition + new Vector2(Face * 10, -8), new Color(0.75f, 0.95f, 1f, 0.8f), 1, 40, 1.8f, 0.5f, -20);
            if (_st > Tune.Wraith.CastWindup)
            {
                _s = 0; _cd = Tune.Wraith.CastCooldown * G.Range(0.8f, 1.3f) * (Elite ? 0.7f : 1f);
                Anim.Once("cast", 3);
                G.Sfx.Play("clink", GlobalPosition, -2, 0.1f, 0.7f);
                int n = Elite ? 3 : 1;
                var from = GlobalPosition + new Vector2(Face * 10, -8);
                var aim = (P.GlobalPosition - from).Normalized();
                for (int k = 0; k < n; k++)
                    G.Spawn(new EnemyProjectile { Position = from, Vel = aim.Rotated((k - (n - 1) / 2f) * 0.22f) * Tune.Wraith.ShardSpeed, Grav = 0, Damage = Tune.Wraith.ShardDamage * DmgK, Kind = "ice", Radius = 4, Life = 2.4f, Source = this });
                G.Fx.Flash(from, 22, new Color(0.8f, 0.95f, 1f));
            }
        }
        else
        {
            // looming: hang over the player, trailing frost, then drift back out
            var at = _loomAt - GlobalPosition;
            Velocity = Velocity.MoveToward(at.LimitLength(1f) * Math.Min(220f, at.Length() * 5f), 900 * dt);
            if (G.Chance(0.6f)) G.Fx.Burst(GlobalPosition + G.RandDir() * 8, new Color(0.7f, 0.92f, 1f, 0.7f), 1, 30, 2.2f, 0.7f, -30);
            if (_st > Tune.Wraith.LoomTime)
            {
                _s = 0;
                _loomCd = Tune.Wraith.LoomCooldown * G.Range(0.8f, 1.5f);
                Velocity = -dir * Tune.Wraith.FlySpeed * 1.6f;
            }
        }
        if (GlobalPosition.Y > G.Cave.WaterY - 14) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y, -60));
        if (Math.Abs(to.X) > 4) Face = Math.Sign(to.X);
    }

    private const int Drift = 0, Approach = 1, Retreat = 2, Cast = 3;
    private static readonly string[] Moves = { "drift", "approach", "retreat", "cast" };
    protected override string BrainName => "wraith";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0;
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Wraith.CastCooldown, 0, 1);
    protected override bool CanAct(int a) => a != Cast || _cd <= 0;
    protected override bool IsAttack(int a) => a == Cast;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s != 0) { _s = 0; _cd = Tune.Wraith.CastCooldown; } }

    protected override int Teacher()
    {
        if (DistP > Aggro(420)) return Drift;
        if (_cd <= 0 && DistP < 320 && DistP > 100 && SeesP) return Cast;
        return DistP < 120 ? Retreat : DistP > 240 ? Approach : Drift;
    }

    protected override void Animate() => Anim.Loop("float");

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 20, new Color(0.6f, 0.9f, 1f, 0.05f + 0.03f * MathF.Sin(T * 3)));
        DrawHealthBar();
    }
}

// ============================================================================ shardling

/// <summary>A crystal crawler that curls up and bursts in a spray of shards; shatters when killed.</summary>
public partial class Shardling : Walker
{
    private int _s; // 0 walk, 1 curl, 2 recover
    private float _st, _cd = 1.2f;

    public Shardling() { MaxHp = Tune.Shardling.Hp; BodyRadius = 8; ContactDamage = Tune.Shardling.Contact; XpValue = Tune.Shardling.Xp; }

    protected override void Setup() { DisplayName = "Shardling"; UseSprite("shardling"); }
    protected override Color BloodColor => new(0.6f, 0.85f, 1f);

    protected override void Die()
    {
        if (!Dead) Shards(4, 0.6f);
        base.Die();
    }

    private void Shards(int n, float dmgMult)
    {
        var from = GlobalPosition + new Vector2(0, -4);
        for (int k = 0; k < n; k++)
        {
            float a = -Mathf.Pi * (0.1f + 0.8f * k / Math.Max(1, n - 1));
            G.Spawn(new EnemyProjectile { Position = from, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * G.Range(200, 260), Grav = 420, Damage = Tune.Shardling.ShardDamage * DmgK * dmgMult, Kind = "crystal", Radius = 3.5f, Life = 1.6f, Source = this });
        }
        G.Fx.Glint(from, new Color(0.7f, 0.95f, 1f));
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                if (!Awake) { Brake(ref v, dt, 600); break; }
                float want = Intent switch { Advance => DirP, Retreat => -DirP, _ => 0 };
                if (want != 0) Face = want;
                v = Stride(v, want, Tune.Shardling.WalkSpeed, dt, 300);
                if (Intent == Burst && CanAct(Burst))
                {
                    _s = 1; _st = 0;
                    Anim.Once("curl", 3, 8f / (Tune.Shardling.CurlTime * 24f));
                    G.Sfx.Play("clink", GlobalPosition, -6, 0.1f, 1.4f);
                    Consume();
                }
                break;
            case 1:
                Brake(ref v, dt);
                if (G.Chance(0.3f)) G.Fx.Glint(GlobalPosition + G.RandDir() * 8, new Color(0.7f, 0.95f, 1f));
                if (_st > Tune.Shardling.CurlTime)
                {
                    _s = 2; _st = 0; _cd = Tune.Shardling.BurstCooldown * (Elite ? 0.7f : 1f);
                    Anim.Once("burst", 3);
                    G.Sfx.Play("spike", GlobalPosition, -2);
                    Shards(Elite ? 9 : 6, 1f);
                    G.Fx.Ring(GlobalPosition, 40 * Size, new Color(0.7f, 0.95f, 1f, 0.8f));
                    if (DistP < 40 * Size) P.Hurt(Tune.Shardling.BurstDamage * DmgK, GlobalPosition, 200, this);
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

    private const int Stand = 0, Advance = 1, Retreat = 2, Burst = 3;
    private static readonly string[] Moves = { "stand", "advance", "retreat", "burst" };
    protected override string BrainName => "shardling";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    protected override float AttackReady => 1 - Math.Clamp(_cd / Tune.Shardling.BurstCooldown, 0, 1);
    protected override bool CanAct(int a) => a != Burst || (_cd <= 0 && IsOnFloor());
    protected override bool IsAttack(int a) => a == Burst;
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override int Teacher()
    {
        if (DistP > Aggro(400)) return Stand;
        if (_cd <= 0 && DistP < 64) return Burst;
        return Advance;
    }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 8 ? "walk" : "idle", Math.Clamp(avx / 60f, 0.7f, 1.5f));
    }

    public override void _Draw() => DrawHealthBar();
}
