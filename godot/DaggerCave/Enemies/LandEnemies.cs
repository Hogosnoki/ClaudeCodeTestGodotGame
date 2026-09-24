using System;
using Godot;

namespace DaggerCave;

/// <summary>Roosts on ceilings; wakes and swoops at the player along a wobbling path, then retreats and comes again.</summary>
public partial class Bat : Enemy
{
    private int _state; // 0 roost, 1 swoop, 2 retreat
    private float _stateT, _wob;

    protected override bool UsesGravity => false;

    protected override void Setup()
    {
        DisplayName = "Bat";
        _wob = G.Range(0, 10);
        MotionMode = MotionModeEnum.Floating;
        UseSprite("bat");
    }

    public Bat() { MaxHp = Tune.Bat.Hp; BodyRadius = 7; ContactDamage = Tune.Bat.Contact; XpValue = Tune.Bat.Xp; }

    /// <summary>Entrance: already on the wing, swooping in from off-screen.</summary>
    public override void Engage()
    {
        base.Engage();
        _state = 1;
        ContactActive = true;
    }

    protected override void Think(float dt)
    {
        _stateT += dt;
        var cave = G.Cave;
        switch (_state)
        {
            case 0:
                Velocity = Vector2.Zero;
                ContactActive = false;
                if (DistP < Tune.Bat.WakeRange && SeesP || HurtFlash > 0) { _state = 1; _stateT = 0; G.Sfx.Play("bat", GlobalPosition, -4); ContactActive = true; Anim.Once("wake", 3); }
                return;
            case 1:
            {
                var to = ToP + new Vector2(0, -6);
                var dir = to.Normalized();
                var perp = new Vector2(-dir.Y, dir.X);
                var desired = dir * (Tune.Bat.FlySpeed + (Elite ? 40 : 0)) + perp * MathF.Sin(T * 6 + _wob) * Tune.Bat.WobbleSpeed;
                Velocity = Velocity.MoveToward(desired, 520f * dt);
                if (DistP < 18 || _stateT > 2.6f) { _state = 2; _stateT = 0; }
                break;
            }
            default:
            {
                var away = (-ToP).Normalized() + new Vector2(0, -0.8f);
                Velocity = Velocity.MoveToward(away.Normalized() * 150f, 500f * dt);
                if (_stateT > 0.9f) { _state = 1; _stateT = 0; if (G.Chance(0.4f)) G.Sfx.Play("bat", GlobalPosition, -8); }
                break;
            }
        }
        if (GlobalPosition.Y > cave.WaterY - 14) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y, -80));
        if (Velocity.X != 0) Face = Math.Sign(Velocity.X);
    }

    protected override void Animate()
    {
        Anim.Loop(_state == 0 ? "roost" : _state == 1 && DistP < 70 ? "dive" : "fly");
        Anim.AllowTurns = _state != 0;
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>Hops toward the player in arcs and lashes its tongue at close range. Swims too.</summary>
public partial class Frog : Enemy
{
    private float _hopCd = 1f, _tongueCd = 1.5f, _tongueT = -1, _croakT;
    private Vector2 _tongueDir;
    private static float TongueLen => Tune.Frog.TongueRange;
    private const float TongueTime = 0.38f;

    public Frog() { MaxHp = Tune.Frog.Hp; BodyRadius = 9; ContactDamage = Tune.Frog.Contact; XpValue = Tune.Frog.Xp; }

    private float _hopWind = -1, _hopDir;
    private bool _wasAir;

    protected override void Setup() { DisplayName = "Frog"; _croakT = G.Range(1, 5); UseSprite("frog"); }

    protected override void Think(float dt)
    {
        _hopCd -= dt; _tongueCd -= dt; _croakT -= dt;
        var v = Velocity;
        if (InWater)
        {
            var target = Awake ? ToP.Normalized() * 70f : new Vector2(0, -20);
            v = v.MoveToward(target, 200 * dt);
            if (GlobalPosition.Y < G.Cave.WaterY + 4 && v.Y < 0) v.Y = 0;
            Velocity = v;
            if (v.X != 0) Face = Math.Sign(v.X);
            return;
        }
        bool floor = IsOnFloor();
        if (_croakT <= 0) { _croakT = G.Range(3, 7); if (DistP < 600) G.Sfx.Play("frog", GlobalPosition, -8); Anim.Once("croak", 1); }
        if (_hopWind >= 0)
        {
            // crouch first, then spring
            _hopWind -= dt;
            v.X = Mathf.MoveToward(v.X, 0, 900 * dt);
            if (_hopWind < 0)
            {
                v = new Vector2(_hopDir * Tune.Frog.HopSpeedX * G.Range(0.82f, 1.18f), -Tune.Frog.HopSpeedY * G.Range(0.9f, 1.1f));
                G.Sfx.Play("jump", GlobalPosition, -14, 0.1f, 0.7f);
            }
            Velocity = v;
            ApplyGravity(dt);
            return;
        }

        if (_tongueT >= 0)
        {
            _tongueT += dt;
            v.X = Mathf.MoveToward(v.X, 0, 800 * dt);
            float ext = TongueExtent();
            var tip = GlobalPosition + new Vector2(0, -2) + _tongueDir * ext;
            if (!P.Dead && tip.DistanceTo(P.GlobalPosition) < 12) P.Hurt(Tune.Frog.TongueDamage * G.DepthDmg * (Elite ? 1.5f : 1f), GlobalPosition);
            if (_tongueT > TongueTime) _tongueT = -1;
        }
        else if (floor)
        {
            v.X = Mathf.MoveToward(v.X, 0, 900 * dt);
            if (Awake && DistP < Aggro(Tune.Frog.AggroRange))
            {
                Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
                if (DistP < TongueLen * Size && _tongueCd <= 0 && SeesP)
                {
                    _tongueT = 0; _tongueCd = Tune.Frog.TongueCooldown; _tongueDir = (ToP + new Vector2(0, -4)).Normalized();
                    G.Sfx.Play("tongue", GlobalPosition, -4);
                    Anim.Once("tongue", 3, 9f / (TongueTime * 24f));
                }
                else if (_hopCd <= 0)
                {
                    _hopCd = G.Range(Tune.Frog.HopCooldownMin, Tune.Frog.HopCooldownMax);
                    _hopDir = Math.Sign(ToP.X);
                    _hopWind = 4f / 24f;
                    Anim.Once("crouch", 2);
                }
            }
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private float TongueExtent()
    {
        float t = _tongueT / TongueTime;
        return (t < 0.4f ? t / 0.4f : 1 - (t - 0.4f) / 0.6f) * TongueLen * Size;
    }

    protected override void Animate()
    {
        bool air = !IsOnFloor() && !InWater;
        if (_wasAir && !air && !InWater) Anim.Once("land", 1);
        _wasAir = air;
        Anim.Loop(InWater ? "swim" : air ? (Velocity.Y < 0 ? "hop" : "fall") : "idle");
    }

    public override void _Draw()
    {
        if (_tongueT >= 0)
        {
            var mouth = new Vector2(Face * 7.6f * Size, 1.8f * Size);
            var tip = mouth + _tongueDir * TongueExtent();
            DrawLine(mouth, tip, new Color(0.35f, 0.08f, 0.12f), 3.6f);
            DrawLine(mouth, tip, new Color(0.95f, 0.45f, 0.55f), 2.4f);
            DrawCircle(tip, 3, new Color(0.95f, 0.45f, 0.55f));
        }
        DrawHealthBar();
    }
}

/// <summary>Runs at you and clubs you -- or, as a slinger, keeps its distance and lobs rocks.</summary>
public partial class Goblin : Enemy
{
    public bool Slinger;
    private int _state; // 0 chase, 1 windup, 2 recover
    private float _stateT, _throwCd = 1.5f, _gruntT;

    public Goblin() { MaxHp = Tune.Goblin.Hp; BodyRadius = 9; ContactDamage = Tune.Goblin.Contact; XpValue = Tune.Goblin.Xp; }

    private float _throwDelay = -1;
    private bool _wasAir;

    protected override void Setup() { DisplayName = Slinger ? "Goblin Slinger" : "Goblin"; _gruntT = G.Range(2, 6); UseSprite(Slinger ? "slinger" : "goblin"); }

    protected override void Think(float dt)
    {
        _stateT += dt; _throwCd -= dt; _gruntT -= dt;
        var v = Velocity;
        if (_throwDelay >= 0)
        {
            // the sling whirls, then lets go on the release frame
            _throwDelay -= dt;
            if (_throwDelay < 0) ThrowRock();
            v.X = Mathf.MoveToward(v.X, 0, 900 * dt);
            Velocity = v;
            ApplyGravity(dt);
            return;
        }
        if (InWater)
        {
            v = v.MoveToward(new Vector2(Math.Sign(ToP.X) * 50, -60), 300 * dt);
            Velocity = v;
            return;
        }
        bool floor = IsOnFloor();
        float speed = (Slinger ? Tune.Goblin.SlingerSpeed : Tune.Goblin.RunSpeed) * (Elite ? 1.15f : 1f);
        if (_gruntT <= 0) { _gruntT = G.Range(3, 8); if (Awake && DistP < 500) G.Sfx.Play("goblin", GlobalPosition, -8, 0.2f); }

        if (!Awake || DistP > Aggro(Tune.Goblin.AggroRange)) { v.X = Mathf.MoveToward(v.X, 0, 600 * dt); Velocity = v; ApplyGravity(dt); return; }

        float dx = ToP.X;
        if (_state == 0)
        {
            float want;
            if (Slinger)
            {
                float d = Math.Abs(dx);
                want = d < 130 ? -Math.Sign(dx) : d > 230 ? Math.Sign(dx) : 0;
                Face = Math.Sign(dx) == 0 ? Face : Math.Sign(dx);
                if (_throwCd <= 0 && DistP < 340 && SeesP) { _throwDelay = 0.36f; Anim.Once("throw", 3); _throwCd = Tune.Goblin.ThrowCooldown * (Elite ? 0.6f : 1f); }
            }
            else
            {
                want = Math.Abs(dx) > 8 ? Math.Sign(dx) : 0;
                if (want != 0) Face = want;
                if (DistP < 34 * Size && Math.Abs(ToP.Y) < 30) { _state = 1; _stateT = 0; want = 0; G.Sfx.Play("goblin", GlobalPosition, -4, 0.2f, 1.2f); Anim.Once("windup", 3, 6f / (Tune.Goblin.WindupTime * 24f)); }
            }
            v.X = Mathf.MoveToward(v.X, want * speed, 900 * dt);
            if (floor && (IsOnWall() || (ToP.Y < -50 && Math.Abs(dx) < 90)) && want != 0) v.Y = -390;
            if (floor && want != 0 && !G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 30)) && !G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 60)) && ToP.Y < 40)
                v.Y = -330; // hop gaps rather than falling in
        }
        else if (_state == 1)
        {
            v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
            if (_stateT > Tune.Goblin.WindupTime)
            {
                _state = 2; _stateT = 0;
                G.Sfx.Play("swing_heavy", GlobalPosition, -4, 0.1f, 0.7f);
                Anim.Once("strike", 3);
                var rel = ToP;
                if (Math.Abs(rel.X) < 40 * Size && Math.Sign(rel.X) != -Face && Math.Abs(rel.Y) < 30 * Size)
                    P.Hurt(Tune.Goblin.ClubDamage * G.DepthDmg * (Elite ? 1.4f : 1f), GlobalPosition);
            }
        }
        else
        {
            v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
            if (_stateT > 0.25f && _stateT - dt <= 0.25f) Anim.Once("recover", 2, 1.2f);
            if (_stateT > 0.5f) { _state = 0; _stateT = 0; }
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    private void ThrowRock()
    {
        var from = GlobalPosition + new Vector2(0, -10);
        var target = P.GlobalPosition + P.Velocity * 0.25f;
        float t = Math.Clamp(from.DistanceTo(target) / 280f, 0.45f, 1.1f);
        const float g = 600f;
        var vel = new Vector2((target.X - from.X) / t, (target.Y - from.Y) / t - 0.5f * g * t);
        int n = Elite ? 3 : 1;
        for (int k = 0; k < n; k++)
            G.Spawn(new EnemyProjectile { Position = from, Vel = vel.Rotated((k - (n - 1) / 2f) * 0.12f), Grav = g, Damage = Tune.Goblin.RockDamage * G.DepthDmg, Kind = "rock", Radius = 4 });
        G.Sfx.Play("throw", GlobalPosition, -8, 0.1f, 0.6f);
        _state = 2; _stateT = 0.2f;
    }

    protected override void Animate()
    {
        bool air = !IsOnFloor() && !InWater;
        if (air && !_wasAir && Velocity.Y < -100) Anim.Once("jump", 1);
        if (!air && _wasAir) Anim.Once("land", 1);
        _wasAir = air;
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(air ? "fall" : avx > 15 ? "run" : "idle", Math.Clamp(avx / 110f, 0.6f, 1.4f));
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>Creeps along the ceiling, drops on a silk thread when you pass beneath, then climbs back. Lands and pounces if its thread is cut.</summary>
public partial class Spider : Enemy
{
    private int _state; // 0 ceiling, 1 drop, 2 hang, 3 climb, 4 ground
    private float _stateT, _anchorY, _pounceCd;

    protected override bool UsesGravity => _state == 4;

    public Spider() { MaxHp = Tune.Spider.Hp; BodyRadius = 8; ContactDamage = Tune.Spider.Contact; XpValue = Tune.Spider.Xp; }

    protected override void Setup()
    {
        DisplayName = "Spider";
        _anchorY = GlobalPosition.Y - 8;
        MotionMode = MotionModeEnum.Floating;
        UseSprite("spider");
    }

    protected override void Think(float dt)
    {
        _stateT += dt; _pounceCd -= dt;
        var cave = G.Cave;
        ManualMove = _state != 4;
        switch (_state)
        {
            case 0:
            {
                float dx = ToP.X;
                if (Awake && Math.Abs(dx) > 6 && ToP.Y > 0 && DistP < Aggro(400))
                {
                    float nx = GlobalPosition.X + Math.Sign(dx) * Tune.Spider.CrawlSpeed * dt;
                    // stay on the ceiling contour
                    if (cave.FindCeiling(new Vector2(nx, GlobalPosition.Y + 12), 50, out var ce) && !cave.IsSolid(new Vector2(nx, ce.Y + 8)))
                    {
                        GlobalPosition = new Vector2(nx, ce.Y + 8);
                        _anchorY = ce.Y;
                        Face = Math.Sign(dx);
                    }
                }
                if (Awake && Math.Abs(dx) < 34 && ToP.Y > 20 && ToP.Y < 300 && SeesP)
                { _state = 1; _stateT = 0; G.Sfx.Play("spider", GlobalPosition, -4); }
                break;
            }
            case 1:
            {
                var np = GlobalPosition + new Vector2(0, Tune.Spider.DropSpeed * dt);
                bool floor = cave.IsSolid(np + new Vector2(0, BodyRadius * Size + 2)) || cave.IsWater(np);
                if (floor || np.Y > P.GlobalPosition.Y + 6) { _state = 2; _stateT = 0; }
                else GlobalPosition = np;
                break;
            }
            case 2:
                if (_stateT > 0.9f) { _state = 3; _stateT = 0; }
                break;
            case 3:
            {
                var np = GlobalPosition + new Vector2(0, -120 * dt);
                if (np.Y <= _anchorY + 8) { np.Y = _anchorY + 8; _state = 0; _stateT = 0; }
                GlobalPosition = np;
                break;
            }
            default:
            {
                var v = Velocity;
                if (IsOnFloor())
                {
                    Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
                    v.X = Mathf.MoveToward(v.X, Face * Tune.Spider.GroundSpeed, 900 * dt);
                    if (_pounceCd <= 0 && DistP < 110 && SeesP)
                    {
                        _pounceCd = Tune.Spider.PounceCooldown;
                        v = new Vector2(Math.Sign(ToP.X) * 230, -300);
                        Anim.Once("pounce", 3);
                        G.Sfx.Play("spider", GlobalPosition, -4);
                    }
                }
                if (InWater) v = v.MoveToward(new Vector2(0, -80), 400 * dt);
                Velocity = v;
                ApplyGravity(dt);
                break;
            }
        }
    }

    protected override void OnHurt()
    {
        if ((_state == 1 || _state == 2 || _state == 3) && G.Chance(0.45f))
        {
            _state = 4; ManualMove = false; Velocity = Vector2.Zero;
        }
    }

    private float _lastX;

    protected override void Animate()
    {
        bool moving = Math.Abs(GlobalPosition.X - _lastX) > 0.2f || Math.Abs(Velocity.X) > 10;
        _lastX = GlobalPosition.X;
        Anim.FlipV = _state == 0;
        Anim.Loop(_state switch
        {
            0 => moving ? "crawl" : "idle",
            1 => "drop",
            2 or 3 => "hang",
            _ => IsOnFloor() && Math.Abs(Velocity.X) > 10 ? "crawl" : "idle",
        }, 1.3f);
    }

    public override void _Draw()
    {
        if (_state is 1 or 2 or 3 || (_state == 0 && GlobalPosition.Y - _anchorY > 9))
            DrawLine(new Vector2(0, _anchorY - GlobalPosition.Y), Vector2.Zero, new Color(0.9f, 0.9f, 0.95f, 0.6f), 1f);
        DrawHealthBar();
    }
}

/// <summary>Slow molten blob: leaves burning puddles, lobs lava globs, and hates water.</summary>
public partial class LavaMonster : Enemy
{
    private float _lobCd = 2f, _puddleT, _windup = -1;

    public LavaMonster() { MaxHp = Tune.Magma.Hp; BodyRadius = 12; ContactDamage = Tune.Magma.Contact; XpValue = Tune.Magma.Xp; KnockResist = 0.3f; }

    protected override void Setup() { DisplayName = "Magma Brute"; UseSprite("magma"); }
    protected override Color BloodColor => new(1f, 0.5f, 0.1f);

    protected override void Think(float dt)
    {
        _lobCd -= dt; _puddleT -= dt;
        var v = Velocity;
        if (InWater)
        {
            // It boils away in water.
            Hp -= Tune.Magma.WaterDamagePerSec * dt;
            if (G.Chance(0.3f)) G.Fx.Burst(GlobalPosition + new Vector2(0, -10), new Color(0.85f, 0.85f, 0.85f, 0.5f), 1, 40, 4f, 0.8f, -100);
            if (G.Chance(0.02f)) G.Sfx.Play("lava", GlobalPosition, -10);
            v = v.MoveToward(new Vector2(0, -90), 400 * dt);
            Velocity = v;
            if (Hp <= 0) Die();
            return;
        }
        if (_windup >= 0)
        {
            _windup += dt;
            v.X = Mathf.MoveToward(v.X, 0, 600 * dt);
            if (_windup > 0.55f) { Lob(); _windup = -1; Anim.Once("lob", 3); }
        }
        else if (Awake && DistP < Aggro(420))
        {
            Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
            v.X = Mathf.MoveToward(v.X, Face * Tune.Magma.WalkSpeed, 300 * dt);
            if (IsOnFloor() && IsOnWall()) v.Y = -300;
            if (_lobCd <= 0 && DistP < 340 && SeesP) { _windup = 0; _lobCd = Tune.Magma.LobCooldown * (Elite ? 0.62f : 1f); G.Sfx.Play("lava", GlobalPosition, -6); Anim.Once("lob_windup", 3, 8f / (0.55f * 24f)); }
            if (_puddleT <= 0 && IsOnFloor() && Math.Abs(v.X) > 10)
            {
                _puddleT = 1.3f;
                G.Spawn(new LavaPuddle { Position = GlobalPosition + new Vector2(0, BodyRadius * Size) });
            }
        }
        else v.X = Mathf.MoveToward(v.X, 0, 300 * dt);
        Velocity = v;
        ApplyGravity(dt);
    }

    private void Lob()
    {
        var from = GlobalPosition + new Vector2(0, -12 * Size);
        int n = Elite ? 5 : 3;
        const float g = 700f;
        var target = P.GlobalPosition;
        float t = Math.Clamp(from.DistanceTo(target) / 260f, 0.5f, 1.2f);
        var baseV = new Vector2((target.X - from.X) / t, (target.Y - from.Y) / t - 0.5f * g * t);
        for (int k = 0; k < n; k++)
            G.Spawn(new EnemyProjectile { Position = from, Vel = baseV * G.Range(0.8f, 1.15f) + new Vector2((k - (n - 1) / 2f) * 45, 0), Grav = g, Damage = Tune.Magma.GlobDamage * G.DepthDmg, Kind = "lava", Radius = 5 });
    }

    protected override void Animate() => Anim.Loop(Math.Abs(Velocity.X) > 8 && IsOnFloor() ? "walk" : "idle", 1.4f);

    public override void _Draw()
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(T * 4);
        DrawCircle(Vector2.Zero, (BodyRadius + 12) * Size, new Color(1f, 0.4f, 0.05f, 0.06f + pulse * 0.05f));
        DrawHealthBar();
    }
}

/// <summary>Stone golem: slow, heavy, nearly immovable; telegraphs a ground slam that sends shockwaves both ways.</summary>
public partial class Golem : Enemy
{
    private float _slamCd = 1.5f, _windup = -1, _recover;

    public Golem() { MaxHp = Tune.Golem.Hp; BodyRadius = 15; ContactDamage = Tune.Golem.Contact; XpValue = Tune.Golem.Xp; KnockResist = 0.85f; }

    protected override void Setup() { DisplayName = "Golem"; UseSprite("golem"); }
    protected override Color BloodColor => new(0.6f, 0.58f, 0.55f);

    protected override void Think(float dt)
    {
        _slamCd -= dt; _recover -= dt;
        var v = Velocity;
        if (InWater) { v = v.MoveToward(new Vector2(Math.Sign(ToP.X) * 25, 60), 300 * dt); Velocity = v; return; }
        if (_windup >= 0)
        {
            _windup += dt;
            v.X = 0;
            if (_windup > Tune.Golem.SlamWindup) { Slam(); _windup = -1; _recover = 1.0f; Anim.Once("slam", 3); }
        }
        else if (_recover > 0)
        {
            v.X = Mathf.MoveToward(v.X, 0, 800 * dt);
            if (_recover < 0.75f && _recover + dt >= 0.75f) Anim.Once("recover", 2, 10f / (0.75f * 24f));
        }
        else if (Awake && DistP < Aggro(460))
        {
            Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
            v.X = Mathf.MoveToward(v.X, Face * Tune.Golem.WalkSpeed, 300 * dt);
            if (IsOnFloor() && IsOnWall()) v.Y = -330;
            if (_slamCd <= 0 && DistP < 190 && Math.Abs(ToP.Y) < 70 && IsOnFloor()) { _windup = 0; G.Sfx.Play("goblin", GlobalPosition, -2, 0.1f, 0.4f); Anim.Once("slam_windup", 3, 12f / (Tune.Golem.SlamWindup * 24f)); }
        }
        else v.X = Mathf.MoveToward(v.X, 0, 600 * dt);
        Velocity = v;
        ApplyGravity(dt);
    }

    private void Slam()
    {
        _slamCd = Tune.Golem.SlamCooldown * (Elite ? 0.7f : 1f);
        G.Sfx.Play("slam", GlobalPosition);
        G.Fx.AddShake(8);
        var foot = GlobalPosition + new Vector2(0, BodyRadius * Size);
        G.Fx.Burst(foot, new Color(0.6f, 0.55f, 0.5f), 20, 200, 3f, 0.5f);
        for (int s = -1; s <= 1; s += 2)
            G.Spawn(new Shockwave { Position = foot + new Vector2(s * 16 * Size, 0), Dir = s, Damage = Tune.Golem.ShockwaveDamage * G.DepthDmg * (Elite ? 1.3f : 1), Size = Elite ? 1.5f : 1f, Speed = Tune.Golem.ShockwaveSpeed * (Elite ? 1.2f : 1f) });
        var rel = ToP;
        if (Math.Abs(rel.X) < 30 * Size && Math.Abs(rel.Y) < 30 * Size) P.Hurt(Tune.Golem.SlamDamage * G.DepthDmg, GlobalPosition, 320);
    }

    protected override void Animate() => Anim.Loop(Math.Abs(Velocity.X) > 6 && IsOnFloor() ? "walk" : "idle", 1.2f);

    public override void _Draw() => DrawHealthBar();
}
