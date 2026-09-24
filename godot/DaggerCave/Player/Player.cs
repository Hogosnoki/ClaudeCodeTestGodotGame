using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public struct PlayerInput
{
    public Vector2 Move;        // -1..1 each axis; up is negative Y
    public Vector2 Aim;         // normalized aim direction (zero = use facing)
    public bool Jump, JumpHeld, Attack, Throw, Dodge;
}

/// <summary>
/// The dagger wielder. Ground movement with coyote time / jump buffering / variable jump height,
/// free swimming with a breath meter below the water line, a dodge, a 360-degree dagger swing
/// with optional combo chain, and a thrown dagger that locks your weapon for a couple of seconds
/// per charge. Every tunable that upgrades touch lives in <see cref="PlayerStats"/>.
/// </summary>
public partial class Player : CharacterBody2D
{
    // --- Tunables (pixels, seconds) ---
    // Gravity and jump speed are both scaled by the same factor k (g*k, v*sqrt(k)) so the jump
    // height is unchanged but the arc is a touch floatier.
    private const float Gravity = 1350f * 0.88f, MaxFall = 675f;
    private const float RunSpeed = 170f;
    private static readonly float BaseJumpV = 470f * MathF.Sqrt(0.88f);
    private const float SwimSpeedBase = 150f;
    private const float SwingCooldownBase = 0.36f, SwingActive = 0.11f, ComboWindow = 0.55f;
    private const float BaseReach = 30f;
    private const float BaseDamage = 10f, ThrowDamage = 16f;
    private const float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCdBase = 0.95f;

    public PlayerStats Stats = new();
    public float Hp = 60;
    public float Breath = 8f;
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
    private bool _jumpCutDone, _wasOnFloor, _comboResetPending, _swingHitSomething;
    private readonly HashSet<Enemy> _swingHits = new();
    private float _swingArc, _swingReach, _swingDmg;
    private float _lastFallSpeed;

    public bool IsDodging => _dodgeT > 0;
    public bool DaggerInHand { get { foreach (var c in _throwCd) if (c <= 0) return true; return false; } }
    public float[] ThrowCooldowns => _throwCd;
    public float[] DodgeCooldowns => _dodgeCd;
    public float SwingCooldownFrac => Math.Clamp(_swingCd / (SwingCooldownBase / Stats.AttackSpeed), 0, 1);
    public int XpToNext => (int)(12 + 8 * Level + 1.6f * Level * Level);
    public bool Invulnerable => _invuln > 0 || _iframes > 0;

    public override void _Ready()
    {
        CollisionLayer = G.LayerPlayer;
        CollisionMask = G.LayerTerrain;
        FloorMaxAngle = Mathf.DegToRad(48);
        FloorSnapLength = 7f;
        SafeMargin = 0.5f;
        AddChild(new CollisionShape2D { Shape = new CapsuleShape2D { Radius = 6.5f, Height = 26f } });
        ZIndex = 1;
        Anim = SpriteAnimator.Create("player");
        AddChild(Anim);
        Hp = Stats.MaxHp;
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
        };
        bool mouseAttack = Input.IsActionJustPressed("attack");
        bool kbAttack = Input.IsActionJustPressed("attack_alt");
        bool mouseThrow = Input.IsActionJustPressed("throw");
        bool kbThrow = Input.IsActionJustPressed("throw_alt");
        inp.Attack = mouseAttack || kbAttack;
        inp.Throw = mouseThrow || kbThrow;
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        if (stick.Length() > 0.35f) inp.Aim = stick.Normalized();
        else if (kbAttack || kbThrow) inp.Aim = inp.Move.Length() > 0.2f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        else inp.Aim = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        return inp;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _animT += dt;
        if (Dead) { Velocity = new Vector2(0, Math.Min(Velocity.Y + Gravity * dt, MaxFall)); MoveAndSlide(); Anim.Rotation = 0; QueueRedraw(); return; }
        var cave = G.Cave;
        var inp = ReadInput();

        TickTimers(dt);

        bool wasInWater = InWater;
        InWater = cave.IsWater(GlobalPosition + new Vector2(0, 2));
        HeadUnder = cave.IsWater(GlobalPosition + new Vector2(0, -9));
        if (InWater != wasInWater && Math.Abs(Velocity.Y) > 80)
        {
            G.Sfx.Play("splash", GlobalPosition, -4);
            G.Fx.Directional(new Vector2(GlobalPosition.X, cave.WaterY), Vector2.Up, 0.7f, new Color(0.6f, 0.85f, 1f, 0.9f), 14, 220, 2.5f, 0.6f, 600, 0);
        }
        UpdateBreath(dt);
        Unstick(cave, dt);

        if (inp.Move.X > 0.2f) Facing = 1; else if (inp.Move.X < -0.2f) Facing = -1;

        var v = Velocity;
        bool onFloor = IsOnFloor();
        _wallSliding = false;
        _jumpedFromGround = false;
        bool surfaceFloat = !onFloor && !InWater && GlobalPosition.Y > cave.WaterY - 10 && cave.IsWater(GlobalPosition + new Vector2(0, 14));
        if (onFloor || surfaceFloat) { _coyote = 0.1f; _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; }
        if (inp.Jump) _jumpBuffer = 0.13f;

        if (inp.Dodge && _dodgeT <= 0 && _airDashT <= 0) TryDodge(inp);

        if (_dodgeT > 0)
        {
            v = _dodgeDir * DodgeSpeed;
            if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.45f, 0.8f, 1f), 0.2f);
            if (_dodgeT - dt <= 0) v *= 0.45f;
        }
        else if (_airDashT > 0)
        {
            v = _airDashDir * 540f;
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

        // Attacks
        if (inp.Attack && _swingCd <= 0 && DaggerInHand && _dodgeT <= 0) StartSwing(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
        if (inp.Throw && _dodgeT <= 0) TryThrow(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
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
        // Ease the sprite's tilt toward the swim direction.
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, rot, 0.25f);

        // i-frame shimmer and post-hit blink
        float a = _invuln > 0 && (int)(_animT * 20) % 2 == 0 ? 0.35f : 1f;
        Anim.Modulate = _iframes > 0 ? new Color(0.75f, 0.95f, 1f, a) : new Color(1, 1, 1, a);
    }

    private float _stuckInRock;

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
        _wallJumpLock -= dt; _hurtFlash -= dt;
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
                if (_drownTick <= 0) { _drownTick = 0.5f; TakeRawDamage(5f + Stats.MaxHp * 0.02f, "drown"); }
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
        float target = inp.Move.X * RunSpeed * Stats.MoveSpeed;
        float accel = onFloor ? 1900f : (_wallJumpLock > 0 ? 350f : 1200f);
        v.X = Mathf.MoveToward(v.X, target, accel * dt);
        v.Y = Math.Min(v.Y + Gravity * dt * (v.Y > 0 ? 1.2f : 1f), MaxFall);

        float jumpV = BaseJumpV * MathF.Sqrt(Stats.JumpMult);
        int wallSide = WallSide();
        bool onWall = !onFloor && wallSide != 0;

        if (Stats.WallJump && onWall && v.Y > 0 && Math.Sign(inp.Move.X) == wallSide)
        {
            v.Y = Math.Min(v.Y, 110f);
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
                v = new Vector2(-wallSide * 250f, -jumpV * 0.92f);
                Facing = -wallSide; _wallJumpLock = 0.16f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 1.2f);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("jump_start", 2);
                G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 0), new Color(0.7f, 0.65f, 0.6f, 0.8f), 6, 80, 2f, 0.3f, 100);
            }
            else if (Stats.DoubleJump && _airJumps > 0)
            {
                _airJumps--; v.Y = -jumpV * 0.9f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -5, 0.05f, 1.4f);
                Anim.Once("dodge", 2, 1.6f);
                G.Fx.Ring(GlobalPosition + new Vector2(0, 12), 10, new Color(0.7f, 0.9f, 1f, 0.8f));
            }
            else if (Stats.AirDash && _airDashes > 0)
            {
                _airDashes--; _jumpBuffer = 0;
                var d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
                _airDashDir = d; _airDashT = 0.16f;
                if (d.X != 0) Facing = Math.Sign(d.X);
                G.Sfx.Play("airdash", GlobalPosition, -4);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("airdash", 3);
                G.Fx.Ring(GlobalPosition, 12, new Color(0.6f, 0.9f, 1f, 0.8f));
            }
        }
        if (!inp.JumpHeld && v.Y < -120 && !_jumpCutDone) { v.Y *= 0.5f; _jumpCutDone = true; }
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
            v = v.MoveToward(dir.Normalized() * spd, 800f * dt);
            if (G.Chance(0.05f)) G.Fx.Bubbles(GlobalPosition, 1);
        }
        else v = v.MoveToward(new Vector2(0, 22), 320f * dt);

        bool nearSurface = GlobalPosition.Y < cave.WaterY + 20;
        if (_jumpBuffer > 0 && nearSurface)
        {
            v.Y = -BaseJumpV * MathF.Sqrt(Stats.JumpMult) * 0.95f;
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
        int maxSteps = Stats.ThirdCombo ? 3 : 2;
        _comboStep = _swingSinceLast < ComboWindow ? (_comboStep + 1) % maxSteps : 0;
        _swingSinceLast = 0;
        _swingDir = aim;
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        bool finisher = Stats.ThirdCombo && _comboStep == 2;
        _swingArc = Mathf.DegToRad(finisher ? 170 : 115);
        _swingReach = BaseReach * Stats.DaggerReach * (finisher ? 1.25f : 1f);
        _swingDmg = BaseDamage * Stats.DamageMult * (finisher ? 2f : 1f);
        _swingT = 0;
        _swingCd = SwingCooldownBase / Stats.AttackSpeed;
        _swingHits.Clear();
        _swingHitSomething = false;
        _comboResetPending = Stats.Combo && _comboStep < maxSteps - 1;
        // body animation: combo letter + the nearest of five aim directions in front of the player
        var local = new Vector2(aim.X * Facing, aim.Y);
        float la = MathF.Atan2(local.Y, Math.Max(local.X, -0.2f));
        string dir = la < -1.18f ? "up" : la < -0.39f ? "upfwd" : la < 0.39f ? "fwd" : la < 1.18f ? "downfwd" : "down";
        string letter = finisher ? "c" : _comboStep % 2 == 0 ? "a" : "b";
        Anim.Face((int)Facing, instant: true);
        Anim.Once($"slash_{letter}_{dir}", 3, Math.Max(1f, Stats.AttackSpeed) * (finisher ? 1f : 1.05f));
        G.Sfx.Play(finisher ? "swing_heavy" : "swing", GlobalPosition, -2, 0.12f, 1f + _comboStep * 0.08f);
    }

    private void UpdateSwing(float dt)
    {
        _swingT += dt;
        if (_swingT <= SwingActive)
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
        if (_swingT > 0.22f) _swingT = -1;
    }

    private void OnSwingHit(Enemy e, Vector2 to)
    {
        var dir = (_swingDir + to.Normalized()).Normalized();
        float kb = Stats.KnockbackLevel switch { 0 => 40f, 1 => 260f, 2 => 380f, _ => 480f };
        bool finisher = Stats.ThirdCombo && _comboStep == 2;
        if (finisher) kb += 120;
        var hitPos = e.GlobalPosition - to.Normalized() * e.HitRadius;
        float dealt = e.Hurt(_swingDmg * G.Range(0.9f, 1.1f), dir * kb, hitPos);
        if (dealt <= 0)
        {
            G.Sfx.Play("clink", GlobalPosition, -6);
            G.Fx.Spark(hitPos, -dir, false, new Color(1f, 0.9f, 0.6f));
            return;
        }
        OnDealtDamage(dealt);
        bool killed = e.Dead;
        // impact: sparks, freeze-frame, a camera nudge in the direction of the blow, rumble
        G.Fx.Spark(hitPos, dir, finisher || killed, finisher ? new Color(1f, 0.85f, 0.4f) : Colors.White);
        G.Main.Kick(dir * (finisher ? 6f : killed ? 4f : 2.5f));
        G.Main.Rumble(finisher ? 0.6f : 0.35f, finisher ? 0.7f : 0.15f, finisher ? 0.16f : 0.08f);
        if (finisher || killed) G.Fx.AddShake(finisher ? 5 : 3);
        if (finisher && G.Chance(1f)) Afterimage.Spawn(Anim, new Color(1f, 0.85f, 0.4f), 0.18f);
        if (!_swingHitSomething)
        {
            _swingHitSomething = true;
            if (_comboResetPending) { _swingCd = 0.04f; _comboResetPending = false; }
            G.Main.HitStop(finisher ? 0.15f : killed ? 0.12f : 0.08f);
            // Pogo: downward aerial strikes bounce the player up.
            if (Stats.Pogo && !IsOnFloor() && !InWater && _swingDir.Y > 0.55f)
            {
                Velocity = new Vector2(Velocity.X, -BaseJumpV * 0.95f * MathF.Sqrt(Stats.JumpMult));
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
        G.Sfx.Play("throw", GlobalPosition, -2);
    }

    public void Hurt(float dmg, Vector2 from, float knock = 230f)
    {
        if (Dead || Invulnerable) return;
        dmg *= 1f - Stats.DamageReduction;
        TakeRawDamage(dmg, "hit");
        _invuln = Stats.HurtInvuln;
        var away = (GlobalPosition - from).Normalized();
        // turn to face what hit you, then recoil
        if (Math.Abs(from.X - GlobalPosition.X) > 2) Facing = Math.Sign(from.X - GlobalPosition.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("hurt", 4);
        Anim.Flash(1f);
        G.Main.HitStop(0.1f);
        G.Main.Kick(away * 6f);
        G.Main.Rumble(0.6f, 0.8f, 0.25f);
        if (away.LengthSquared() < 0.01f) away = new Vector2(-Facing, 0);
        Velocity = new Vector2(Math.Sign(away.X == 0 ? -Facing : away.X) * knock, InWater ? away.Y * knock : -170f);
        _dodgeT = 0; _airDashT = 0;
    }

    private void TakeRawDamage(float dmg, string kind)
    {
        Hp -= dmg;
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

    /// <summary>
    /// The body is a sprite (see <see cref="Anim"/>); this draws the blade smear on top: a
    /// crescent that sweeps with the swing, brightest at its leading edge, fading behind it.
    /// </summary>
    public override void _Draw()
    {
        if (Dead || _swingT < 0 || _swingT > 0.26f) return;
        bool finisher = Stats.ThirdCombo && _comboStep == 2;
        float prog = Math.Clamp(_swingT / SwingActive, 0, 1);
        prog = 1 - (1 - prog) * (1 - prog) * (1 - prog);
        float fade = 1 - Math.Clamp((_swingT - SwingActive) / 0.15f, 0, 1);
        float dirSign = _comboStep % 2 == 0 ? 1 : -1;
        float baseA = _swingDir.Angle();
        float a0 = baseA - dirSign * _swingArc * 0.5f;
        float head = a0 + dirSign * _swingArc * prog;
        // the tail catches up with the head as the swing fades out
        float tail = a0 + dirSign * _swingArc * Math.Max(0, prog - 0.85f + (1 - fade) * 0.85f);
        if (Math.Abs(head - tail) < 0.02f) return;
        var o = new Vector2(0, -3);
        const int n = 18;
        float outer = _swingReach + 3, width = finisher ? 15 : 11;
        var pts = new Vector2[n * 2];
        var cols = new Color[n * 2];
        var edge = new Vector2[n];
        var edgeCols = new Color[n];
        var tint = finisher ? new Color(1f, 0.82f, 0.35f) : new Color(0.8f, 0.95f, 1f);
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)(n - 1);            // 0 = tail, 1 = head
            float ang = Mathf.Lerp(tail, head, t);
            float w = width * MathF.Sin(t * MathF.PI * 0.5f + 0.15f);
            var d = Vector2.Right.Rotated(ang);
            pts[k] = o + d * outer;
            pts[2 * n - 1 - k] = o + d * (outer - w);
            float alpha = t * t * fade;
            cols[k] = new Color(tint, 0.85f * alpha);
            cols[2 * n - 1 - k] = new Color(tint, 0.0f);
            edge[k] = o + d * (outer + 0.5f);
            edgeCols[k] = new Color(1, 1, 1, alpha);
        }
        DrawPolygon(pts, cols);
        DrawPolylineColors(edge, edgeCols, finisher ? 2.5f : 1.8f);
        if (finisher)
        {
            // an echo arc slightly inside the main one
            for (int k = 0; k < n; k++) edge[k] = o + (edge[k] - o) * 0.72f;
            DrawPolylineColors(edge, edgeCols, 1.2f);
        }
    }
}
