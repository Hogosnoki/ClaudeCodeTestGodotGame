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
    private const float Gravity = 1350f, MaxFall = 720f;
    private const float RunSpeed = 185f;
    private const float BaseJumpV = 470f;
    private const float SwimSpeedBase = 150f;
    private const float SwingCooldownBase = 0.36f, SwingActive = 0.11f, ComboWindow = 0.55f;
    private const float BaseReach = 30f;
    private const float BaseDamage = 10f, ThrowDamage = 16f;
    private const float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCdBase = 0.95f;

    public PlayerStats Stats = new();
    public float Hp = 100;
    public float Breath = 8f;
    public int Level = 1;
    public int Xp;
    public int PendingLevelUps;
    public int Kills;
    public bool Dead;
    public bool InWater, HeadUnder;
    public float Facing = 1;

    public Func<PlayerInput> InputOverride;

    // Timers / state
    private float _coyote, _jumpBuffer, _invuln, _iframes, _swingCd, _swingT = -1, _swingSinceLast = 9, _drownTick;
    private float[] _throwCd = new float[1];
    private float[] _dodgeCd = new float[1];
    private float _dodgeT, _airDashT, _wallJumpLock, _landSquash, _runPhase, _hurtFlash, _bubbleT, _animT;
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
        if (Dead) { Velocity = new Vector2(0, Math.Min(Velocity.Y + Gravity * dt, MaxFall)); MoveAndSlide(); QueueRedraw(); return; }
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
        bool surfaceFloat = !onFloor && !InWater && GlobalPosition.Y > cave.WaterY - 10 && cave.IsWater(GlobalPosition + new Vector2(0, 14));
        if (onFloor || surfaceFloat) { _coyote = 0.1f; _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; }
        if (inp.Jump) _jumpBuffer = 0.13f;

        if (inp.Dodge && _dodgeT <= 0 && _airDashT <= 0) TryDodge(inp);

        if (_dodgeT > 0)
        {
            v = _dodgeDir * DodgeSpeed;
            if (G.Chance(0.6f)) G.Fx.Ghost(GlobalPosition, Facing);
            if (_dodgeT - dt <= 0) v *= 0.45f;
        }
        else if (_airDashT > 0)
        {
            v = _airDashDir * 540f;
            if (G.Chance(0.7f)) G.Fx.Ghost(GlobalPosition, Facing);
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
            _landSquash = 0.12f;
        }
        _wasOnFloor = nowFloor;

        // Attacks
        if (inp.Attack && _swingCd <= 0 && DaggerInHand && _dodgeT <= 0) StartSwing(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
        if (inp.Throw && _dodgeT <= 0) TryThrow(inp.Aim.LengthSquared() > 0.01f ? inp.Aim : new Vector2(Facing, 0));
        if (_swingT >= 0) UpdateSwing(dt);

        if (Math.Abs(Velocity.X) > 20 && nowFloor) _runPhase += dt * Math.Abs(Velocity.X) * 0.075f;
        QueueRedraw();
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
        _wallJumpLock -= dt; _landSquash -= dt; _hurtFlash -= dt;
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
            if (G.Chance(0.2f)) G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 6), new Color(0.6f, 0.55f, 0.5f, 0.6f), 1, 20, 1.5f, 0.3f, 30);
        }

        if (_jumpBuffer > 0)
        {
            if (_coyote > 0)
            {
                v.Y = -jumpV; _coyote = 0; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -8);
            }
            else if (Stats.WallJump && onWall)
            {
                v = new Vector2(-wallSide * 250f, -jumpV * 0.92f);
                Facing = -wallSide; _wallJumpLock = 0.16f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 1.2f);
                G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 0), new Color(0.7f, 0.65f, 0.6f, 0.8f), 6, 80, 2f, 0.3f, 100);
            }
            else if (Stats.DoubleJump && _airJumps > 0)
            {
                _airJumps--; v.Y = -jumpV * 0.9f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -5, 0.05f, 1.4f);
                G.Fx.Ring(GlobalPosition + new Vector2(0, 12), 10, new Color(0.7f, 0.9f, 1f, 0.8f));
            }
            else if (Stats.AirDash && _airDashes > 0)
            {
                _airDashes--; _jumpBuffer = 0;
                var d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
                _airDashDir = d; _airDashT = 0.16f;
                if (d.X != 0) Facing = Math.Sign(d.X);
                G.Sfx.Play("airdash", GlobalPosition, -4);
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
        float dealt = e.Hurt(_swingDmg * G.Range(0.9f, 1.1f), dir * kb, e.GlobalPosition - to.Normalized() * e.HitRadius);
        if (dealt <= 0) { G.Sfx.Play("clink", GlobalPosition, -6); return; }
        OnDealtDamage(dealt);
        if (!_swingHitSomething)
        {
            _swingHitSomething = true;
            if (_comboResetPending) { _swingCd = 0.04f; _comboResetPending = false; }
            G.Main.HitStop(finisher ? 0.07f : 0.035f);
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
        _invuln = 0.85f;
        var away = (GlobalPosition - from).Normalized();
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
        G.Sfx.Play("player_die", GlobalPosition);
        G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 30, 200, 3f, 0.9f);
        G.Main.OnPlayerDied();
    }

    // ---------------------------------------------------------------- drawing

    private static readonly Color Cloak = new(0.16f, 0.36f, 0.42f);
    private static readonly Color CloakDark = new(0.09f, 0.21f, 0.26f);
    private static readonly Color Skin = new(0.93f, 0.78f, 0.62f);
    private static readonly Color Scarf = new(0.78f, 0.2f, 0.18f);
    private static readonly Color Steel = new(0.85f, 0.9f, 0.95f);

    public override void _Draw()
    {
        if (Dead) { DrawCircle(new Vector2(0, 8), 5, CloakDark); return; }
        bool flicker = _invuln > 0 && (int)(_animT * 20) % 2 == 0;
        float alpha = flicker ? 0.35f : 1f;
        var tint = _hurtFlash > 0 ? new Color(1, 0.5f, 0.5f) : Colors.White;
        if (_iframes > 0) tint = new Color(0.7f, 0.95f, 1f);
        Color C(Color c) => new(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha);

        var vel = Velocity;
        bool swimming = InWater && _dodgeT <= 0;
        float rot = 0;
        if (swimming && vel.Length() > 30) rot = Mathf.Clamp(Mathf.Wrap(Facing > 0 ? vel.Angle() : vel.Angle() - Mathf.Pi, -Mathf.Pi, Mathf.Pi), -1.2f, 1.2f) * 0.8f;
        if (_dodgeT > 0 && !InWater) rot = (1 - _dodgeT / DodgeTime) * Mathf.Tau * Facing;
        float squash = _landSquash > 0 ? 1 + _landSquash * 1.5f : 1;
        DrawSetTransform(Vector2.Zero, rot, new Vector2(Facing * squash, 1 / squash));

        bool grounded = IsOnFloor();
        float run = MathF.Sin(_runPhase);
        float run2 = MathF.Cos(_runPhase);
        // scarf
        float sway = MathF.Sin(_animT * 7) * 2f;
        var sb = new Vector2(-2, -8);
        float trail = Math.Clamp(Math.Abs(vel.X) / 180f, 0.2f, 1.2f);
        DrawPolyline(new[] { sb, sb + new Vector2(-6 * trail, 2 + sway * 0.5f), sb + new Vector2(-11 * trail, 1 + sway), sb + new Vector2(-15 * trail, 3 + sway * 1.5f) }, C(Scarf), 2.5f);

        // legs
        Vector2 hip = new(0, 5);
        Vector2 f1, f2;
        if (swimming) { float k = MathF.Sin(_animT * 10); f1 = new Vector2(-5 + k * 2, 12); f2 = new Vector2(-5 - k * 2, 11); }
        else if (!grounded) { f1 = new Vector2(3, 10); f2 = new Vector2(-3, 12); }
        else if (Math.Abs(vel.X) > 20) { f1 = new Vector2(run * 5, 13 - Math.Max(0, run2) * 3); f2 = new Vector2(-run * 5, 13 - Math.Max(0, -run2) * 3); }
        else { f1 = new Vector2(2.5f, 13); f2 = new Vector2(-2.5f, 13); }
        DrawLine(hip, f2, C(CloakDark), 3f);
        DrawLine(hip, f1, C(CloakDark), 3f);

        // body / cloak
        float flare = Math.Clamp(-vel.X * Facing / 200f, -0.5f, 1f) * 3 + MathF.Sin(_animT * 5) * 0.8f;
        DrawColoredPolygon(new[] { new Vector2(-4, -6), new Vector2(4, -6), new Vector2(5, 6), new Vector2(-6 - flare, 8) }, C(Cloak));
        DrawLine(new Vector2(-4, 1), new Vector2(4.5f, 1), C(new Color(0.45f, 0.3f, 0.18f)), 1.5f); // belt

        // head + hood
        var head = new Vector2(0.5f, -10.5f);
        DrawCircle(head, 5.5f, C(Cloak));
        DrawCircle(head + new Vector2(1.5f, 0.8f), 3.6f, C(Skin));
        DrawColoredPolygon(new[] { head + new Vector2(-5.5f, 0), head + new Vector2(-2, -6), head + new Vector2(-9, -3) }, C(CloakDark));
        DrawCircle(head + new Vector2(3, 0.2f), 0.9f, new Color(0.1f, 0.1f, 0.15f, alpha));

        // arm + dagger
        bool inHand = DaggerInHand;
        var shoulder = new Vector2(1, -3);
        if (_swingT >= 0 && _swingT < 0.2f)
        {
            float prog = Math.Clamp(_swingT / SwingActive, 0, 1);
            float dirSign = _comboStep % 2 == 0 ? 1 : -1;
            // world-space blade angle, converted into the flipped/rotated local space
            float wa = _swingDir.Angle() + dirSign * _swingArc * (prog - 0.5f);
            var lv = Vector2.Right.Rotated(wa).Rotated(-rot);
            lv.X *= Facing;
            float a = lv.Angle();
            var hand = shoulder + Vector2.Right.Rotated(a) * 7;
            DrawLine(shoulder, hand, C(Skin), 2.5f);
            DrawDagger(hand, a, alpha);
        }
        else
        {
            float bob = grounded && Math.Abs(vel.X) > 20 ? run * 2 : 0;
            var hand = shoulder + new Vector2(4 + bob * 0.5f, 5);
            DrawLine(shoulder, hand, C(Skin), 2.5f);
            if (inHand) DrawDagger(hand, Mathf.DegToRad(swimming ? 10 : 60 + bob * 5), alpha);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        // slash arc (world orientation)
        if (_swingT >= 0 && _swingT < 0.2f)
        {
            float prog = Math.Clamp(_swingT / SwingActive, 0, 1);
            float fade = 1 - Math.Clamp((_swingT - SwingActive) / 0.09f, 0, 1);
            bool finisher = Stats.ThirdCombo && _comboStep == 2;
            float dirSign = _comboStep % 2 == 0 ? 1 : -1;
            float baseA = _swingDir.Angle();
            float a0 = baseA - dirSign * _swingArc * 0.5f;
            float a1 = baseA - dirSign * _swingArc * 0.5f + dirSign * _swingArc * prog;
            int n = 14;
            var pts = new Vector2[n * 2];
            var o = new Vector2(0, -3);
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)(n - 1);
                float ang = Mathf.Lerp(a0, a1, t);
                float thick = MathF.Sin(t * MathF.PI * 0.5f + 0.2f);
                pts[k] = o + Vector2.Right.Rotated(ang) * (_swingReach + 2);
                pts[2 * n - 1 - k] = o + Vector2.Right.Rotated(ang) * (_swingReach + 2 - 11 * thick);
            }
            var col = finisher ? new Color(1f, 0.85f, 0.4f, 0.75f * fade) : new Color(0.85f, 0.97f, 1f, 0.6f * fade);
            if (Math.Abs(a1 - a0) > 0.05f) DrawColoredPolygon(pts, col);
        }
    }

    private void DrawDagger(Vector2 hand, float angle, float alpha)
    {
        var d = Vector2.Right.Rotated(angle);
        var perp = new Vector2(-d.Y, d.X);
        float len = 9f * Stats.DaggerReach;
        DrawLine(hand - d * 2, hand + d * 1, new Color(0.35f, 0.22f, 0.12f, alpha), 2.5f);
        DrawLine(hand + d + perp * 2.5f, hand + d - perp * 2.5f, new Color(0.7f, 0.6f, 0.3f, alpha), 1.5f);
        DrawColoredPolygon(new[] { hand + d * 1.5f + perp * 1.3f, hand + d * (1.5f + len), hand + d * 1.5f - perp * 1.3f }, new Color(Steel, alpha));
    }
}
