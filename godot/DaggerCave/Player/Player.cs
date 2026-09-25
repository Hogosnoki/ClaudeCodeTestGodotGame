using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public struct PlayerInput
{
    public Vector2 Move;        // -1..1 each axis; up is negative Y
    public Vector2 Aim;         // normalized aim direction (zero = use facing)
    public bool Jump, JumpHeld, Attack, Throw, Dodge;
    public bool GuardHeld;      // dodge button held (the warden's shield)
    public Vector2 GuardAim;    // right stick, else the mouse (keyboard + mouse), else zero = facing
}

/// <summary>
/// The dagger wielder. Ground movement with coyote time / jump buffering / variable jump height,
/// free swimming with a breath meter below the water line, a dodge, a 360-degree dagger swing
/// with optional combo chain, and a thrown dagger that locks your weapon for a couple of seconds
/// per charge. Every tunable that upgrades touch lives in <see cref="PlayerStats"/>.
/// </summary>
public partial class Player : CharacterBody2D
{
    // Tunables live in Core/Tuning.cs (Tune.Hero). Gravity and jump speed are both scaled by
    // Floatiness (g*k, v*sqrt(k)) so jump height is unchanged while the arc gets floatier.
    private static float Gravity => Tune.Hero.Gravity * Tune.Hero.Floatiness;
    private static float MaxFall => Tune.Hero.MaxFallSpeed;
    private static float RunSpeed => Tune.Hero.RunSpeed;
    private static float BaseJumpV => Tune.Hero.JumpVelocity * MathF.Sqrt(Tune.Hero.Floatiness);
    private static float SwimSpeedBase => Tune.Hero.SwimSpeed;
    private static float ComboWindow => Tune.Hero.ComboWindow;
    // weapon: the swordsman's medium sword or the warden's shortsword
    private bool IsWarden => Stats.Hero == HeroKind.Warden;
    private float SwingCooldownBase => IsWarden ? Tune.Warden.SwingCooldown : Tune.Swordsman.SwingCooldown;
    private float SwingActive => IsWarden ? Tune.Warden.SwingTime : Tune.Swordsman.SwingTime;
    private float SwingWindup => IsWarden ? Tune.Warden.SwingWindup : Tune.Swordsman.SwingWindup;
    // this swing's beats (scaled by attack speed): wind-up, then the sweep (the hitbox), then follow-through
    private float _windup, _active;
    private bool _released;
    private float BaseReach => IsWarden ? Tune.Warden.Reach : Tune.Swordsman.Reach;
    private float BaseDamage => IsWarden ? Tune.Warden.Damage : Tune.Swordsman.Damage;
    private float BaseKnock => IsWarden ? Tune.Warden.Knockback : Tune.Swordsman.Knockback;
    private float LungeSpeed => IsWarden ? Tune.Warden.Lunge : Tune.Swordsman.Lunge;
    private static float ThrowDamage => Tune.Hero.ThrowDamage;
    private static float DodgeSpeed => Tune.Hero.DodgeSpeed;
    private static float DodgeTime => Tune.Hero.DodgeTime;
    private static float DodgeCdBase => Tune.Hero.DodgeCooldown;

    public PlayerStats Stats = new(G.Hero);
    public float Hp = Tune.Hero.StartHp;
    public float Breath = Tune.Hero.BreathSeconds;
    public int Level = 1;
    public int Xp;
    public int PendingLevelUps;
    public int Kills;
    public bool Dead;
    public bool InWater, HeadUnder;
    public float Facing = 1;

    public Func<PlayerInput> InputOverride;
    public SpriteAnimator Anim;

    // animation bookkeeping
    private bool _wallSliding, _jumpedFromGround;
    private string _lastBase = "idle";
    private float _lastAbsVx;

    // Timers / state
    private float _coyote, _jumpBuffer, _invuln, _iframes, _swingCd, _swingT = -1, _swingSinceLast = 9, _drownTick;
    private float[] _throwCd = new float[1];
    private float[] _dodgeCd = new float[1];
    private float _dodgeT, _airDashT, _wallJumpLock, _hurtFlash, _bubbleT, _animT;
    private Vector2 _dodgeDir, _airDashDir, _swingDir;
    private int _comboStep, _airJumps, _airDashes;
    private bool _jumpCutDone, _wasOnFloor, _swingHitSomething, _chainLive, _finisher;
    private readonly HashSet<Enemy> _swingHits = new();
    private float _swingArc, _swingReach, _swingDmg;
    private float _lastFallSpeed;

    public bool IsDodging => _dodgeT > 0;
    public bool IsSwinging => _swingT >= 0;
    /// <summary>The secondary (thrown dagger / barrier) is ready to use.</summary>
    public bool SecondaryReady
    {
        get
        {
            if (IsWarden) return _barrierCd <= 0;
            foreach (var c in _throwCd) if (c <= 0) return true;
            return false;
        }
    }
    /// <summary>Dodging, invulnerable, or behind a raised shield (an input for the enemy brains).</summary>
    public bool Guarding => IsDodging || Invulnerable || ShieldRaised;

    // warden
    public float ShieldHp { get; private set; } = Tune.Warden.ShieldHp;
    public bool ShieldRaised { get; private set; }
    public bool ShieldBroken => _shieldBrokenT > 0;
    public float ShieldBrokenLeft => _shieldBrokenT;
    public Vector2 ShieldDir { get; private set; } = Vector2.Right;
    public float ShieldArc => Mathf.DegToRad(Tune.Warden.ShieldArcDegrees) * Stats.ShieldArcMult;
    public float BarrierHp { get; private set; }
    public float BarrierCooldownFrac => Math.Clamp(_barrierCd / Math.Max(0.01f, Stats.BarrierCooldown), 0, 1);
    private float _shieldBrokenT, _shieldRegenWait, _shieldUpT, _shieldFlash, _barrierT, _barrierCd;
    private float _freeze, _lungeT, _lungeDir, _waveCd;
    private bool _barrierStruck;
    public float[] ThrowCooldowns => _throwCd;
    public float[] DodgeCooldowns => _dodgeCd;
    public float SwingCooldownFrac => Math.Clamp(_swingCd / (SwingCooldownBase / Stats.AttackSpeed), 0, 1);
    public int XpToNext => (int)(Tune.Hero.XpBase + Tune.Hero.XpLinear * Level + Tune.Hero.XpQuadratic * Level * Level);
    public bool Invulnerable => _invuln > 0 || _iframes > 0;

    public override void _Ready()
    {
        CollisionLayer = G.LayerPlayer;
        CollisionMask = G.LayerTerrain;
        FloorMaxAngle = Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees);
        FloorSnapLength = 7f;
        SafeMargin = 0.5f;
        AddChild(new CollisionShape2D { Shape = new CapsuleShape2D { Radius = 6.5f, Height = 26f } });
        ZIndex = 1;
        Anim = SpriteAnimator.Create(IsWarden ? "warden" : "swordsman");
        Anim.FootOffset = 13f;
        AddChild(Anim);
        Hp = Stats.MaxHp;
        ShieldHp = Stats.ShieldMax;
        Breath = Stats.BreathMax;
    }

    public void SyncCharges()
    {
        if (_throwCd.Length != Stats.ThrowCharges) Array.Resize(ref _throwCd, Stats.ThrowCharges);
        if (_dodgeCd.Length != Stats.DodgeCharges) Array.Resize(ref _dodgeCd, Stats.DodgeCharges);
    }

    public void Heal(float amount)
    {
        if (Dead || amount <= 0) return;
        float before = Hp;
        Hp = Math.Min(Stats.MaxHp, Hp + amount);
        if (Hp - before >= 1f) G.Fx?.Text(GlobalPosition + new Vector2(0, -22), "+" + Mathf.RoundToInt(Hp - before), new Color(0.4f, 1f, 0.5f), 10);
    }

    /// <summary>Test harness: a fresh, full shield.</summary>
    public void RefillShield() { ShieldHp = Stats.ShieldMax; _shieldBrokenT = 0; }

    /// <summary>A gulp of air (the vents' bubbles).</summary>
    public void AddBreath(float seconds) => Breath = Math.Min(Stats.BreathMax, Breath + seconds);

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToNext) { Xp -= XpToNext; Level++; PendingLevelUps++; }
    }

    private PlayerInput ReadInput()
    {
        if (InputOverride != null) return InputOverride();
        var inp = new PlayerInput
        {
            Move = new Vector2(Input.GetAxis("move_left", "move_right"), Input.GetAxis("move_up", "move_down")),
            Jump = Input.IsActionJustPressed("jump"),
            JumpHeld = Input.IsActionPressed("jump"),
            Dodge = Input.IsActionJustPressed("dodge"),
            GuardHeld = Input.IsActionPressed("dodge"),
        };
        bool mouseAttack = Input.IsActionJustPressed("attack");
        bool kbAttack = Input.IsActionJustPressed("attack_alt");
        bool mouseThrow = Input.IsActionJustPressed("throw");
        bool kbThrow = Input.IsActionJustPressed("throw_alt");
        inp.Attack = mouseAttack || kbAttack;
        inp.Throw = mouseThrow || kbThrow;
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        // the shield points wherever a swing would go: right stick, else the left stick on a
        // controller (else your facing), or the mouse
        if (stick.Length() > 0.35f) inp.GuardAim = stick.Normalized();
        else if (G.Main.UsingPad) inp.GuardAim = inp.Move.Length() > 0.3f ? inp.Move.Normalized() : Vector2.Zero;
        else inp.GuardAim = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        if (stick.Length() > 0.35f) inp.Aim = stick.Normalized();
        else if (kbAttack || kbThrow) inp.Aim = inp.Move.Length() > 0.2f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        else inp.Aim = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        return inp;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _animT += dt;
        var cave = G.Cave;
        if (Dead) { Anim.TimeMult = 1; Anim.Position = Vector2.Zero; DeadPhysics(cave, dt); return; }
        // hit-stop: only the hero (and whoever it traded blows with) freezes, the world carries on
        if (_freeze > 0)
        {
            _freeze -= dt;
            Anim.TimeMult = 0;
            Anim.Position = new Vector2(G.Range(-1.2f, 1.2f), G.Range(-0.6f, 0.6f));
            QueueRedraw();
            return;
        }
        Anim.TimeMult = 1;
        Anim.Position = Vector2.Zero;
        var inp = ReadInput();

        TickTimers(dt);
        if (IsWarden) UpdateShield(inp, dt);

        bool wasInWater = InWater;
        InWater = cave.IsWater(GlobalPosition + new Vector2(0, 2));
        HeadUnder = cave.IsWater(GlobalPosition + new Vector2(0, -9));
        if (InWater != wasInWater && Math.Abs(Velocity.Y) > 80)
        {
            G.Sfx.Play("splash", GlobalPosition, -4);
            G.Fx.Directional(new Vector2(GlobalPosition.X, cave.WaterY), Vector2.Up, 0.7f, new Color(0.6f, 0.85f, 1f, 0.9f), 14, 220, 2.5f, 0.6f, 600, 0);
        }
        // hitting the water soaks up most of the speed you carried in
        if (InWater && !wasInWater) Velocity *= Tune.Hero.WaterEntryDamp;
        UpdateBreath(dt);
        Unstick(cave, dt);

        if (inp.Move.X > 0.2f) Facing = 1; else if (inp.Move.X < -0.2f) Facing = -1;

        var v = Velocity;
        bool onFloor = IsOnFloor();
        _wallSliding = false;
        _jumpedFromGround = false;
        bool surfaceFloat = !onFloor && !InWater && GlobalPosition.Y > cave.WaterY - 10 && cave.IsWater(GlobalPosition + new Vector2(0, 14));
        if (onFloor || surfaceFloat) { _coyote = Tune.Hero.CoyoteTime; _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; }
        if (inp.Jump) _jumpBuffer = Tune.Hero.JumpBuffer;

        if (!IsWarden && inp.Dodge && _dodgeT <= 0 && _airDashT <= 0) TryDodge(inp);

        if (_dodgeT > 0)
        {
            v = _dodgeDir * DodgeSpeed;
            if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.45f, 0.8f, 1f), 0.2f);
            if (_dodgeT - dt <= 0) v *= 0.45f;
        }
        else if (_airDashT > 0)
        {
            v = _airDashDir * Tune.Hero.AirDashSpeed;
            if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.55f, 0.9f, 1f), 0.2f);
            if (_airDashT - dt <= 0) v *= 0.5f;
        }
        else if (InWater) v = Swim(inp, v, dt, cave);
        else v = Platform(inp, v, dt, onFloor);

        if (_dodgeT > 0) _dodgeT -= dt;
        if (_airDashT > 0) _airDashT -= dt;

        _lastFallSpeed = v.Y;
        Velocity = v;
        MoveAndSlide();
        bool nowFloor = IsOnFloor();
        if (nowFloor && !_wasOnFloor && _lastFallSpeed > 260)
        {
            G.Sfx.Play("land", GlobalPosition, -6);
            G.Fx.Burst(GlobalPosition + new Vector2(0, 13), new Color(0.6f, 0.55f, 0.5f, 0.7f), 6, 60, 2f, 0.35f, 50);
            Anim.Once("land", 1);
        }
        _wasOnFloor = nowFloor;

        // Attacks (the warden can swing from behind the shield, but can't combo there)
        if (inp.Attack && _swingCd <= 0 && _dodgeT <= 0) StartSwing(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
        if (inp.Throw && _dodgeT <= 0)
        {
            if (IsWarden) TryBarrier();
            else TryThrow(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
        }
        if (_swingT >= 0) UpdateSwing(dt);

        UpdateAnimation(nowFloor);
        QueueRedraw();
    }

    /// <summary>Chooses the looping clip for the current movement state and triggers transitions.</summary>
    private void UpdateAnimation(bool onFloor)
    {
        var vel = Velocity;
        float avx = Math.Abs(vel.X);
        string clip;
        float speed = 1f;
        float rot = 0f;
        if (InWater && _dodgeT <= 0)
        {
            if (vel.Length() > 40)
            {
                clip = "swim";
                speed = Math.Clamp(vel.Length() / 130f, 0.6f, 1.6f);
                float ang = MathF.Atan2(vel.Y, Math.Max(avx, 1f));
                rot = Mathf.Clamp(ang, -0.9f, 0.9f) * Facing;
            }
            else clip = "swim_idle";
        }
        else if (_wallSliding) clip = "wall_slide";
        else if (!onFloor)
        {
            if (_jumpedFromGround) Anim.Once("jump_start", 1);
            clip = vel.Y < -130 ? "jump_rise" : vel.Y < 140 ? "jump_apex" : "fall";
        }
        else if (avx > 25)
        {
            clip = "run";
            speed = Math.Clamp(avx / (RunSpeed * 0.95f), 0.5f, 1.5f);
            if (_lastBase == "idle") Anim.Once("run_start", 1);
        }
        else
        {
            clip = "idle";
            if (_lastBase == "run" && _lastAbsVx > 140) Anim.Once("run_stop", 1);
        }
        _lastBase = clip;
        _lastAbsVx = avx;
        Anim.Loop(clip, speed);
        Anim.Face((int)Facing);
        Anim.Motion(InWater ? vel * 0.3f : vel);
        // Ease the sprite's tilt toward the swim direction.
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, rot, 0.25f);

        // i-frame shimmer and post-hit blink
        float a = _invuln > 0 && (int)(_animT * 20) % 2 == 0 ? 0.35f : 1f;
        Anim.Modulate = _iframes > 0 ? new Color(0.75f, 0.95f, 1f, a) : new Color(1, 1, 1, a);
    }

    private float _stuckInRock;

    /// <summary>A fallen body still obeys the world: it drops on land, and drifts down slowly in water.</summary>
    private void DeadPhysics(CaveData cave, float dt)
    {
        var v = Velocity;
        if (cave.IsWater(GlobalPosition))
        {
            v *= 1f / (1f + Tune.Hero.WaterDrag * 1.5f * dt);
            v = v.MoveToward(new Vector2(0, 28), 400f * dt);
        }
        else
        {
            v.X = Mathf.MoveToward(v.X, 0, (IsOnFloor() ? 900f : 200f) * dt);
            v.Y = Math.Min(v.Y + Gravity * dt, MaxFall);
        }
        Velocity = v;
        MoveAndSlide();
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, 0, 0.2f);
        QueueRedraw();
    }

    /// <summary>Safety net: if the player ever ends up embedded in rock, nudge them out to open space.</summary>
    private void Unstick(CaveData cave, float dt)
    {
        if (!cave.IsSolid(GlobalPosition)) { _stuckInRock = 0; return; }
        _stuckInRock += dt;
        if (_stuckInRock < 0.25f) return;
        for (float r = 8; r < 400; r += 8)
            for (int k = 0; k < 16; k++)
            {
                var p = GlobalPosition + Vector2.Right.Rotated(k * Mathf.Tau / 16) * r;
                if (!cave.IsSolid(p) && !cave.IsSolid(p + new Vector2(0, -12)) && !cave.IsSolid(p + new Vector2(0, 12)))
                {
                    GlobalPosition = p; Velocity = Vector2.Zero; _stuckInRock = 0;
                    return;
                }
            }
    }

    private void TickTimers(float dt)
    {
        _coyote -= dt; _jumpBuffer -= dt; _invuln -= dt; _iframes -= dt; _swingCd -= dt; _swingSinceLast += dt;
        _wallJumpLock -= dt; _hurtFlash -= dt; _lungeT -= dt;
        _barrierCd -= dt; _waveCd -= dt;
        if (_barrierT > 0)
        {
            _barrierT -= dt;
            if (_barrierT <= 0)
            {
                // Restoring Ward: if the barrier was struck but outlasted the attack, what's left heals you
                if (Stats.RestoringWard && _barrierStruck && BarrierHp > 0) Heal(BarrierHp);
                BarrierHp = 0;
            }
        }
        for (int k = 0; k < _throwCd.Length; k++) if (_throwCd[k] > 0) _throwCd[k] -= dt;
        for (int k = 0; k < _dodgeCd.Length; k++) if (_dodgeCd[k] > 0) _dodgeCd[k] -= dt;
    }

    private void UpdateBreath(float dt)
    {
        if (HeadUnder)
        {
            Breath -= dt;
            _bubbleT -= dt;
            if (_bubbleT <= 0) { _bubbleT = G.Range(0.4f, 1.0f); G.Fx.Bubbles(GlobalPosition + new Vector2(Facing * 3, -12), 2); }
            if (Breath <= 0)
            {
                Breath = 0;
                _drownTick -= dt;
                if (_drownTick <= 0) { _drownTick = 0.5f; TakeRawDamage(Tune.Hero.DrownDamageFlat + Stats.MaxHp * Tune.Hero.DrownDamageFrac, "drown"); }
            }
        }
        else
        {
            if (Breath < Stats.BreathMax * 0.35f && Breath < Stats.BreathMax - 0.1f && _drownTick != -99) { G.Sfx.Play("gasp", GlobalPosition, -6); _drownTick = -99; }
            Breath = Math.Min(Stats.BreathMax, Breath + dt * Stats.BreathMax * 0.6f);
            if (Breath >= Stats.BreathMax) _drownTick = 0;
        }
    }

    private Vector2 Platform(PlayerInput inp, Vector2 v, float dt, bool onFloor)
    {
        float target = inp.Move.X * RunSpeed * Stats.MoveSpeed * (ShieldRaised && !Stats.Stalwart ? Tune.Warden.ShieldMoveMult : 1f);
        float accel = onFloor ? Tune.Hero.GroundAccel : (_wallJumpLock > 0 ? 350f : Tune.Hero.AirAccel);
        v.X = Mathf.MoveToward(v.X, target, accel * dt);
        if (_lungeT > 0) v.X = _lungeDir * Math.Max(Math.Abs(v.X) * Math.Sign(v.X) * _lungeDir, LungeSpeed); // sword lunge
        v.Y = Math.Min(v.Y + Gravity * dt * (v.Y > 0 ? Tune.Hero.FallGravityMult : 1f), MaxFall);

        float jumpV = BaseJumpV * MathF.Sqrt(Stats.JumpMult);
        int wallSide = WallSide();
        bool onWall = !onFloor && wallSide != 0;

        if (Stats.WallJump && onWall && v.Y > 0 && Math.Sign(inp.Move.X) == wallSide)
        {
            v.Y = Math.Min(v.Y, Tune.Hero.WallSlideSpeed);
            _wallSliding = true;
            Facing = wallSide;
            if (G.Chance(0.2f)) G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 6), new Color(0.6f, 0.55f, 0.5f, 0.6f), 1, 20, 1.5f, 0.3f, 30);
        }

        if (_jumpBuffer > 0)
        {
            if (_coyote > 0)
            {
                v.Y = -jumpV; _coyote = 0; _jumpBuffer = 0; _jumpCutDone = false;
                _jumpedFromGround = true;
                G.Sfx.Play("jump", GlobalPosition, -8);
                G.Fx.Burst(GlobalPosition + new Vector2(0, 13), new Color(0.6f, 0.55f, 0.5f, 0.6f), 5, 50, 1.8f, 0.3f, 40);
            }
            else if (Stats.WallJump && onWall)
            {
                v = new Vector2(-wallSide * Tune.Hero.WallJumpPush, -jumpV * Tune.Hero.WallJumpMult);
                Facing = -wallSide; _wallJumpLock = 0.16f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 1.2f);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("jump_start", 2);
                G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 0), new Color(0.7f, 0.65f, 0.6f, 0.8f), 6, 80, 2f, 0.3f, 100);
            }
            else if (Stats.DoubleJump && _airJumps > 0)
            {
                _airJumps--; v.Y = -jumpV * Tune.Hero.DoubleJumpMult; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -5, 0.05f, 1.4f);
                Anim.Once("dodge", 2, 1.6f);
                G.Fx.Ring(GlobalPosition + new Vector2(0, 12), 10, new Color(0.7f, 0.9f, 1f, 0.8f));
            }
            else if (Stats.AirDash && _airDashes > 0)
            {
                _airDashes--; _jumpBuffer = 0;
                var d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
                _airDashDir = d; _airDashT = Tune.Hero.AirDashTime;
                if (d.X != 0) Facing = Math.Sign(d.X);
                G.Sfx.Play("airdash", GlobalPosition, -4);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("airdash", 3);
                G.Fx.Ring(GlobalPosition, 12, new Color(0.6f, 0.9f, 1f, 0.8f));
            }
        }
        if (!inp.JumpHeld && v.Y < -120 && !_jumpCutDone) { v.Y *= Tune.Hero.JumpCutMult; _jumpCutDone = true; }
        return v;
    }

    /// <summary>-1 / +1 if there is rock right beside the player (for wall slide / wall jump), else 0.</summary>
    private int WallSide()
    {
        var c = G.Cave; var p = GlobalPosition;
        for (int s = -1; s <= 1; s += 2)
            if (c.IsSolid(p + new Vector2(s * 10, -4)) && c.IsSolid(p + new Vector2(s * 10, 6))) return s;
        return 0;
    }

    private Vector2 Swim(PlayerInput inp, Vector2 v, float dt, CaveData cave)
    {
        var dir = inp.Move;
        if (inp.JumpHeld) dir.Y = -1;
        float spd = SwimSpeedBase * Stats.SwimSpeed;
        if (dir.LengthSquared() > 0.04f)
        {
            v = v.MoveToward(dir.Normalized() * spd, Tune.Hero.SwimAccel * dt);
            if (G.Chance(0.05f)) G.Fx.Bubbles(GlobalPosition, 1);
        }
        else v = v.MoveToward(new Vector2(0, 22), 320f * dt);
        v *= 1f / (1f + Tune.Hero.WaterDrag * dt);

        bool nearSurface = GlobalPosition.Y < cave.WaterY + 20;
        if (_jumpBuffer > 0 && nearSurface)
        {
            v.Y = -BaseJumpV * MathF.Sqrt(Stats.JumpMult) * Tune.Hero.SurfaceLeapMult;
            _jumpBuffer = 0; _jumpCutDone = true;
            G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 0.8f);
        }
        return v;
    }

    private void TryDodge(PlayerInput inp)
    {
        int idx = -1;
        for (int k = 0; k < _dodgeCd.Length; k++) if (_dodgeCd[k] <= 0) { idx = k; break; }
        if (idx < 0) return;
        _dodgeCd[idx] = DodgeCdBase * Stats.DodgeCdMult;
        Vector2 d;
        if (InWater) d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        else d = new Vector2(Math.Abs(inp.Move.X) > 0.2f ? Math.Sign(inp.Move.X) : Facing, 0);
        _dodgeDir = d; _dodgeT = DodgeTime;
        if (Math.Abs(d.X) > 0.2f) Facing = Math.Sign(d.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("dodge", 3, 6f / (DodgeTime * 24f));
        if (Stats.DodgeIFrames) _iframes = DodgeTime + 0.12f;
        G.Sfx.Play("dodge", GlobalPosition, -3);
    }

    private void StartSwing(Vector2 aim)
    {
        aim = aim.Normalized();
        // Combo: a strike that lands refunds the swing cooldown, up to ComboResets times in a row.
        // A swing made without a refund (or after a pause) starts a new chain.
        _comboStep = _chainLive && _swingSinceLast < SwingCooldownBase / Stats.AttackSpeed + ComboWindow ? _comboStep + 1 : 0;
        _chainLive = false;
        _swingSinceLast = 0;
        _swingDir = aim;
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        // Finisher: the last strike of a full chain of three or more hits much harder
        bool finisher = _finisher = Stats.ThirdCombo && _comboStep >= 2 && _comboStep == Stats.ComboResets;
        _swingArc = Mathf.DegToRad(finisher ? Tune.Hero.FinisherArcDegrees : Tune.Hero.SwingArcDegrees);
        _swingReach = BaseReach * Stats.DaggerReach * (finisher ? Tune.Hero.FinisherReachMult : 1f);
        _swingDmg = BaseDamage * Stats.DamageMult * (finisher ? Tune.Hero.FinisherDamageMult : 1f);
        _swingT = 0;
        _released = false;
        float speed = Math.Max(1f, Stats.AttackSpeed);
        _windup = SwingWindup / speed * (finisher ? 1.5f : 1f);
        _active = SwingActive / speed;
        _swingCd = SwingCooldownBase / Stats.AttackSpeed;
        _swingHits.Clear();
        _swingHitSomething = false;
        // coil for the wind-up
        Anim.Punch(new Vector2(1.08f, 0.9f));
        // body animation: combo letter + the nearest of five aim directions in front of the player
        var local = new Vector2(aim.X * Facing, aim.Y);
        float la = MathF.Atan2(local.Y, Math.Max(local.X, -0.2f));
        string dir = la < -1.18f ? "up" : la < -0.39f ? "upfwd" : la < 0.39f ? "fwd" : la < 1.18f ? "downfwd" : "down";
        string letter = finisher ? "c" : _comboStep % 2 == 0 ? "a" : "b";
        Anim.Face((int)Facing, instant: true);
        // clip frames: wind-up (2, or 3 for the finisher), woosh (2), follow-through (the rest).
        // Play it so the wind-up frames last exactly the wind-up time; the woosh then lands with the hitbox.
        float windFrames = finisher ? 3 : 2;
        Anim.Once($"slash_{letter}_{dir}", 3, windFrames / 24f / _windup);
    }

    /// <summary>The moment the blade comes around: sound, lunge, crescent wave, stretch.</summary>
    private void ReleaseSwing()
    {
        _released = true;
        var aim = _swingDir;
        G.Sfx.Play(_finisher ? "swing_heavy" : "swing", GlobalPosition, -2, 0.12f, 1f + _comboStep * 0.08f);
        // the sword carries you forward a little (horizontal strikes, on your feet)
        if (LungeSpeed > 0 && !InWater && Math.Abs(aim.X) > 0.35f) { _lungeT = 0.12f; _lungeDir = Math.Sign(aim.X); }
        Anim.Punch(new Vector2(1.22f, 0.86f));
        if (Stats.CrescentWave && _waveCd <= 0)
        {
            _waveCd = Tune.Swordsman.WaveCooldown;
            G.Spawn(new SwordWave
            {
                Position = GlobalPosition + new Vector2(0, -3) + aim * (_swingReach * 0.6f),
                Dir = aim,
                Damage = _swingDmg * Tune.Swordsman.WaveDamage,
                Range = Tune.Swordsman.WaveRange,
            });
        }
    }

    /// <summary>Time into the sweep (negative during the wind-up).</summary>
    private float SweepT => _swingT - _windup;

    private void UpdateSwing(float dt)
    {
        _swingT += dt;
        if (!_released && SweepT >= 0) ReleaseSwing();
        if (SweepT >= 0 && SweepT <= _active)
        {
            var origin = GlobalPosition + new Vector2(0, -3);
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || _swingHits.Contains(e)) continue;
                var to = e.GlobalPosition - origin;
                float dist = to.Length();
                if (dist - e.HitRadius > _swingReach + 4) continue;
                float tol = dist > 1 ? MathF.Atan2(e.HitRadius, dist) : MathF.PI;
                if (dist > 12 && Math.Abs(_swingDir.AngleTo(to)) > _swingArc * 0.5f + tol) continue;
                if (!G.Cave.LineClear(origin, e.GlobalPosition - to.Normalized() * Math.Min(dist, e.HitRadius))) continue;
                _swingHits.Add(e);
                OnSwingHit(e, to);
            }
            foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            {
                var to = pr.GlobalPosition - origin;
                if (to.Length() > _swingReach + 8 || Math.Abs(_swingDir.AngleTo(to)) > _swingArc * 0.6f) continue;
                pr.Deflect();
            }
        }
        // follow-through: the rest of the clip (3-4 frames at the clip's speed), a held beat
        if (SweepT > _active + _windup * 1.6f) _swingT = -1;
    }

    private void OnSwingHit(Enemy e, Vector2 to)
    {
        var dir = (_swingDir + to.Normalized()).Normalized();
        var kbTable = Tune.Hero.KnockbackByLevel;
        float kb = BaseKnock + kbTable[Math.Clamp(Stats.KnockbackLevel, 0, kbTable.Length - 1)];
        bool finisher = _finisher;
        if (finisher) kb += Tune.Hero.FinisherExtraKnockback;
        var hitPos = e.GlobalPosition - to.Normalized() * e.HitRadius;
        float dmg = _swingDmg * G.Range(0.9f, 1.1f);
        if (Stats.Execute && e.Hp < e.MaxHp * Tune.Swordsman.ExecuteBelow) dmg *= 1f + Tune.Swordsman.ExecuteBonus;
        float dealt = e.Hurt(dmg, dir * kb, hitPos);
        if (dealt > 0 && Stats.BleedShare > 0 && !e.Dead) e.Bleed(dealt * Stats.BleedShare, Tune.Swordsman.BleedSeconds);
        if (dealt <= 0)
        {
            G.Sfx.Play("clink", GlobalPosition, -6);
            G.Fx.Spark(hitPos, -dir, false, new Color(1f, 0.9f, 0.6f));
            return;
        }
        OnDealtDamage(dealt);
        bool killed = e.Dead;
        float stop = finisher ? Tune.Feel.HitStopFinisher : killed ? Tune.Feel.HitStopKill : Tune.Feel.HitStopNormal;
        if (!killed) e.Freeze(stop);
        // impact: sparks, freeze-frame, a camera nudge in the direction of the blow, rumble
        G.Fx.Spark(hitPos, dir, finisher || killed, finisher ? new Color(1f, 0.85f, 0.4f) : Colors.White);
        G.Main.Kick(dir * (finisher ? Tune.Feel.KickFinisher : killed ? Tune.Feel.KickKill : Tune.Feel.KickNormal));
        G.Main.Rumble(finisher ? 0.6f : 0.35f, finisher ? 0.7f : 0.15f, finisher ? 0.16f : 0.08f);
        if (finisher || killed) G.Fx.AddShake(finisher ? Tune.Feel.ShakeFinisher : Tune.Feel.ShakeKill);
        if (finisher && G.Chance(1f)) Afterimage.Spawn(Anim, new Color(1f, 0.85f, 0.4f), 0.18f);
        if (!_swingHitSomething)
        {
            _swingHitSomething = true;
            // (no combo from behind a raised shield)
            if (_comboStep < Stats.ComboResets && !ShieldRaised) { _swingCd = 0.04f; _chainLive = true; }
            // mutual bounce: you rebound slightly from what you hit (sideways only)
            if (Math.Abs(to.X) > 2) Velocity = new Vector2(Velocity.X - Math.Sign(to.X) * Tune.Combat.StrikeRecoil, Velocity.Y);
            Freeze(stop);
            // Pogo: downward aerial strikes bounce the player up.
            if (Stats.Pogo && !IsOnFloor() && !InWater && _swingDir.Y > 0.55f)
            {
                Velocity = new Vector2(Velocity.X, -BaseJumpV * Tune.Hero.PogoBounceMult * MathF.Sqrt(Stats.JumpMult));
                _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; _jumpCutDone = true;
                G.Fx.Ring(e.GlobalPosition, 14, new Color(1f, 1f, 0.7f, 0.9f));
            }
        }
    }

    public void OnDealtDamage(float dealt)
    {
        if (Stats.LifeSteal > 0) Hp = Math.Min(Stats.MaxHp, Hp + dealt * Stats.LifeSteal);
    }

    public void OnKill()
    {
        Kills++;
        if (Stats.HealOnKill > 0) Heal(Stats.HealOnKill);
    }

    private void TryThrow(Vector2 aim)
    {
        int idx = -1;
        for (int k = 0; k < _throwCd.Length; k++) if (_throwCd[k] <= 0) { idx = k; break; }
        if (idx < 0) return;
        _throwCd[idx] = Stats.ThrowCooldown;
        aim = aim.Normalized();
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("throw", 3, 1.4f);
        G.Main.Rumble(0.2f, 0f, 0.08f);
        var d = new ThrownDagger
        {
            Dir = aim,
            Damage = ThrowDamage * Stats.DamageMult,
            BouncesLeft = Stats.Bounces,
            Pierce = Stats.Pierce,
        };
        d.GlobalPosition = GlobalPosition + new Vector2(0, -4) + aim * 8;
        G.Spawn(d);
        if (Stats.FanOfKnives)
            for (int s = -1; s <= 1; s += 2)
            {
                var side = aim.Rotated(s * 0.2f);
                var extra = new ThrownDagger { Dir = side, Damage = ThrowDamage * Stats.DamageMult * Tune.Swordsman.FanDamage, Pierce = Stats.Pierce };
                extra.GlobalPosition = GlobalPosition + new Vector2(0, -4) + side * 8;
                G.Spawn(extra);
            }
        G.Sfx.Play("throw", GlobalPosition, -2);
    }

    /// <summary>Returns the damage taken. <paramref name="source"/> is credited with it (enemy learning).</summary>
    public float Hurt(float dmg, Vector2 from, float knock = 230f, Enemy source = null)
    {
        LastHitBlocked = false;
        if (Dead || Invulnerable) return 0;
        bool melee = source != null && GodotObject.IsInstanceValid(source) && source.GlobalPosition.DistanceTo(GlobalPosition) < 70;
        if (TryBlock(from, dmg, out _, melee))
        {
            LastHitBlocked = true;
            if (melee) source.Recoil(source.GlobalPosition.X - GlobalPosition.X);
            return 0;
        }
        dmg *= 1f - Stats.DamageReduction;
        if (BarrierHp > 0)
        {
            // the barrier soaks what it can; with thorns, melee attackers get the hit back
            if (Stats.BarrierThorns && melee) source.Hurt(dmg, (source.GlobalPosition - GlobalPosition).Normalized() * 120, source.GlobalPosition);
            float soak = Math.Min(BarrierHp, dmg);
            _barrierStruck = true;
            BarrierHp -= soak; dmg -= soak;
            G.Fx.Ring(GlobalPosition, 16, new Color(0.55f, 0.85f, 1f, 0.9f));
            G.Sfx.Play("clink", GlobalPosition, -4, 0.1f, 0.8f);
            if (BarrierHp <= 0) { _barrierT = 0; G.Fx.Burst(GlobalPosition, new Color(0.55f, 0.85f, 1f), 14, 140, 2f, 0.4f); }
            if (dmg <= 0.01f)
            {
                _invuln = 0.25f;
                if (melee) source.Recoil(source.GlobalPosition.X - GlobalPosition.X);
                return 0;
            }
        }
        if (source != null && GodotObject.IsInstanceValid(source))
        {
            source.CreditDamage(dmg);
            // mutual bounce: an enemy that struck you in melee rebounds a little too
            if (source.GlobalPosition.DistanceTo(GlobalPosition) < 70) source.Recoil(source.GlobalPosition.X - GlobalPosition.X);
        }
        TakeRawDamage(dmg, "hit");
        _invuln = Stats.HurtInvuln;
        var away = (GlobalPosition - from).Normalized();
        // turn to face what hit you, then recoil
        if (Math.Abs(from.X - GlobalPosition.X) > 2) Facing = Math.Sign(from.X - GlobalPosition.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("hurt", 4);
        Anim.Flash(1f);
        Freeze(Tune.Feel.HitStopPlayerHurt);
        if (source != null && GodotObject.IsInstanceValid(source) && !source.Dead) source.Freeze(Tune.Feel.HitStopPlayerHurt);
        G.Main.Kick(away * Tune.Feel.KickPlayerHurt);
        G.Main.Rumble(0.6f, 0.8f, 0.25f);
        if (away.LengthSquared() < 0.01f) away = new Vector2(-Facing, 0);
        // horizontal only (plus a gentle push in water) so nothing can juggle you upward
        float kx = Math.Sign(away.X == 0 ? -Facing : away.X) * knock * Tune.Combat.HurtKnockbackMult * (Stats.Stalwart ? 0f : 1f);
        Velocity = new Vector2(kx, InWater ? Velocity.Y + away.Y * knock * 0.3f : Velocity.Y);
        _dodgeT = 0; _airDashT = 0;
        return dmg;
    }

    /// <summary>Freezes just the hero for a hit-stop.</summary>
    public void Freeze(float seconds) => _freeze = Math.Max(_freeze, seconds);

    // ---------------------------------------------------------------- warden: shield + barrier

    private void UpdateShield(PlayerInput inp, float dt)
    {
        // regeneration: at zero after a break for ShieldBreakTime, then back at the normal rate
        if (_shieldBrokenT > 0) _shieldBrokenT -= dt;
        else if (_shieldRegenWait > 0) _shieldRegenWait -= dt * (Stats.QuickMend ? 4f : 1f);
        else ShieldHp = Math.Min(Stats.ShieldMax, ShieldHp + Stats.ShieldRegen * dt);
        _shieldFlash -= dt; _blockGrace -= dt;

        bool want = inp.GuardHeld || inp.Dodge;
        bool was = ShieldRaised;
        ShieldRaised = want && !ShieldBroken && ShieldHp > 0;
        if (ShieldRaised && !was) _shieldUpT = 0;
        _shieldUpT += dt;
        // aim: right stick / mouse when given, otherwise the way you face
        var aim = inp.GuardAim.LengthSquared() > 0.01f ? inp.GuardAim.Normalized() : new Vector2(Facing, 0);
        ShieldDir = aim;
        if (ShieldRaised && Math.Abs(aim.X) > 0.2f) Facing = Math.Sign(aim.X);
    }

    /// <summary>True when the last Hurt was stopped by the shield (the attacker's strike is spent).</summary>
    public bool LastHitBlocked { get; private set; }
    private float _blockGrace;

    /// <summary>Blocks a hit arriving from <paramref name="from"/> if the raised shield covers it.</summary>
    public bool TryBlock(Vector2 from, float dmg, out bool perfect, bool melee = false)
    {
        perfect = false;
        if (!IsWarden || !ShieldRaised) return false;
        var to = from - GlobalPosition;
        // an attacker pressed right up against you is judged by which side it's on
        if (to.Length() < 14) to = new Vector2(to.X == 0 ? ShieldDir.X : Math.Sign(to.X), 0);
        if (to.LengthSquared() < 0.01f) to = ShieldDir;
        if (Math.Abs(ShieldDir.AngleTo(to)) > ShieldArc * 0.5f + 0.2f) return false;
        // melee blows landing on the shield in the same instant cost it once
        if (melee && _blockGrace > 0) return true;
        if (melee) _blockGrace = 0.2f;
        perfect = _shieldUpT <= Tune.Warden.PerfectWindow;
        float cost = dmg * (perfect && Stats.PerfectSoak ? Tune.Warden.PerfectSoakMult : 1f);
        ShieldHp -= cost;
        _shieldRegenWait = Tune.Warden.ShieldRegenDelay;
        _shieldFlash = perfect ? 0.25f : 0.12f;
        var at = GlobalPosition + ShieldDir * 16;
        G.Fx.Spark(at, ShieldDir, perfect, perfect ? new Color(1f, 1f, 0.8f) : new Color(0.55f, 0.8f, 1f));
        G.Sfx.Play("clink", at, perfect ? 0 : -3, 0.1f, perfect ? 1.3f : 0.9f);
        G.Main.Rumble(0.3f, 0.2f, 0.08f);
        Freeze(perfect ? 0.08f : 0.04f);
        if (perfect) G.Fx.Text(GlobalPosition + new Vector2(0, -26), "PERFECT", new Color(1f, 0.95f, 0.6f), 10, 0.6f);
        if (ShieldHp <= 0)
        {
            ShieldHp = 0;
            _shieldBrokenT = Stats.ShieldBreakTime;
            ShieldRaised = false;
            G.Sfx.Play("rock", at, 0, 0.1f, 1.4f);
            G.Fx.Burst(at, new Color(0.55f, 0.8f, 1f), 22, 180, 2.5f, 0.5f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -26), "SHIELD BROKEN", new Color(0.6f, 0.8f, 1f), 10, 1f);
        }
        return true;
    }

    /// <summary>Projectiles meet the shield first; a perfect block with Riposte Guard sends them back.</summary>
    public bool TryBlockProjectile(EnemyProjectile pr)
    {
        if (!TryBlock(pr.GlobalPosition - pr.Vel.Normalized() * 10, pr.Damage, out bool perfect)) return false;
        if (perfect && Stats.PerfectReflect) pr.Reflect(ShieldDir, Stats.DamageMult);
        else pr.Deflect();
        return true;
    }

    private void TryBarrier()
    {
        if (_barrierCd > 0) return;
        _barrierCd = Stats.BarrierCooldown;
        _barrierT = Stats.BarrierDuration;
        BarrierHp = Stats.BarrierAmount;
        _barrierStruck = false;
        G.Sfx.Play("levelup", GlobalPosition, -10, 0.05f, 1.6f);
        G.Fx.Ring(GlobalPosition, 18, new Color(0.55f, 0.85f, 1f, 0.9f));
        Anim.Flash(0.5f);
    }

    private int _lastStandDepth = -1;

    private void TakeRawDamage(float dmg, string kind)
    {
        Hp -= dmg;
        if (Hp <= 0 && Stats.LastStand && _lastStandDepth != G.Depth)
        {
            // once per depth, a killing blow leaves you standing with a fresh barrier
            _lastStandDepth = G.Depth;
            Hp = 1;
            _invuln = Tune.Warden.LastStandInvuln;
            BarrierHp = Stats.BarrierAmount; _barrierT = Stats.BarrierDuration;
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "LAST STAND", new Color(1f, 0.85f, 0.4f), 13, 1.5f);
            G.Fx.Ring(GlobalPosition, 24, new Color(1f, 0.85f, 0.4f));
            G.Sfx.Play("roar", GlobalPosition, -8, 0, 1.8f);
        }
        _hurtFlash = 0.15f;
        G.Fx.Text(GlobalPosition + new Vector2(0, -24), Mathf.RoundToInt(dmg).ToString(), new Color(1f, 0.35f, 0.3f), 12);
        G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 8, 120, 2.2f, 0.45f);
        G.Fx.AddShake(kind == "drown" ? 2 : 6);
        G.Sfx.Play(kind == "drown" ? "bubble" : "hurt", GlobalPosition, -2);
        if (Hp <= 0) Die();
    }

    private void Die()
    {
        if (Dead) return;
        Dead = true; Hp = 0;
        Anim.Once("death", 99);
        Anim.Modulate = Colors.White;
        G.Main.Rumble(1f, 1f, 0.6f);
        G.Sfx.Play("player_die", GlobalPosition);
        G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 30, 200, 3f, 0.9f);
        G.Main.OnPlayerDied();
    }

    // ---------------------------------------------------------------- drawing

    private void DrawGuard()
    {
        if (BarrierHp > 0)
        {
            float a = 0.18f + 0.1f * MathF.Sin(_animT * 6);
            if (_barrierT < 1f) a *= _barrierT; // fading out
            DrawCircle(new Vector2(0, -2), 19, new Color(0.45f, 0.75f, 1f, a * 0.5f));
            DrawArc(new Vector2(0, -2), 19, 0, Mathf.Tau, 32, new Color(0.6f, 0.9f, 1f, a * 2.5f), 1.2f);
        }
        if (!ShieldRaised) return;
        // a narrow arc of blue light in front of the shield hand
        float frac = Math.Clamp(ShieldHp / Math.Max(1f, Stats.ShieldMax), 0, 1);
        bool perfectWindow = _shieldUpT <= Tune.Warden.PerfectWindow;
        var col = perfectWindow || _shieldFlash > 0 ? new Color(0.85f, 0.95f, 1f) : new Color(0.35f, 0.65f, 1f);
        float ang = ShieldDir.Angle(), half = ShieldArc * 0.5f;
        var c = new Vector2(0, -3);
        const float r = 17;
        DrawArc(c, r, ang - half, ang + half, 16, new Color(col, 0.25f + 0.3f * frac), 6f);
        DrawArc(c, r + 1.5f, ang - half, ang + half, 16, new Color(col, 0.55f + 0.45f * frac), 1.6f);
    }

    /// <summary>
    /// The body is a sprite (see <see cref="Anim"/>); this draws the blade smear on top: a
    /// crescent that sweeps with the swing, brightest at its leading edge, fading behind it.
    /// </summary>
    public override void _Draw()
    {
        if (!Dead && IsWarden) DrawGuard();
        if (Dead || _swingT < 0 || SweepT < 0 || SweepT > _active + 0.15f) return;
        bool finisher = _finisher;
        float prog = Math.Clamp(SweepT / _active, 0, 1);
        prog = 1 - (1 - prog) * (1 - prog) * (1 - prog);
        float fade = 1 - Math.Clamp((SweepT - _active) / 0.15f, 0, 1);
        float dirSign = _comboStep % 2 == 0 ? 1 : -1;
        float baseA = _swingDir.Angle();
        float a0 = baseA - dirSign * _swingArc * 0.5f;
        float head = a0 + dirSign * _swingArc * prog;
        // the tail catches up with the head as the swing fades out
        float tail = a0 + dirSign * _swingArc * Math.Max(0, prog - 0.85f + (1 - fade) * 0.85f);
        if (Math.Abs(head - tail) < 0.02f) return;
        var o = new Vector2(0, -3);
        const int n = 18;
        // The smear covers the whole blade, from the hand to the tip, and fades both along the
        // swing (bright at the leading edge) and across it (bright at the tip, clear at the hand).
        float outer = _swingReach + 3;
        float blade = _swingReach * (IsWarden ? 0.62f : 0.8f) * (finisher ? 1.1f : 1f);
        var outerBand = new Vector2[n * 2];
        var innerBand = new Vector2[n * 2];
        var outerCols = new Color[n * 2];
        var innerCols = new Color[n * 2];
        var edge = new Vector2[n];
        var edgeCols = new Color[n];
        var tint = finisher ? new Color(1f, 0.82f, 0.35f) : new Color(0.8f, 0.95f, 1f);
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)(n - 1);            // 0 = tail, 1 = head
            float ang = Mathf.Lerp(tail, head, t);
            float w = blade * (0.35f + 0.65f * MathF.Sin(t * MathF.PI * 0.5f)); // the tail thins out
            var d = Vector2.Right.Rotated(ang);
            float alpha = t * t * fade;
            var pOuter = o + d * outer;
            var pMid = o + d * (outer - w * 0.4f);
            var pInner = o + d * (outer - w);
            outerBand[k] = pOuter; outerBand[2 * n - 1 - k] = pMid;
            innerBand[k] = pMid; innerBand[2 * n - 1 - k] = pInner;
            outerCols[k] = new Color(tint, 0.8f * alpha);
            outerCols[2 * n - 1 - k] = innerCols[k] = new Color(tint, 0.32f * alpha);
            innerCols[2 * n - 1 - k] = new Color(tint, 0f);
            edge[k] = o + d * (outer + 0.5f);
            edgeCols[k] = new Color(1, 1, 1, alpha);
        }
        DrawPolygon(innerBand, innerCols);
        DrawPolygon(outerBand, outerCols);
        DrawPolylineColors(edge, edgeCols, finisher ? 2.5f : 1.8f);
        if (finisher)
        {
            // an echo arc slightly inside the main one
            for (int k = 0; k < n; k++) edge[k] = o + (edge[k] - o) * 0.72f;
            DrawPolylineColors(edge, edgeCols, 1.2f);
        }
    }
}
