using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public struct PlayerInput
{
    public Vector2 Move;        // -1..1 each axis; up is negative Y
    public Vector2 Aim;         // normalized aim direction (zero = use facing)
    public bool Jump, JumpHeld, Attack, Ability, Dodge, Potion, Interact;
    public bool GuardHeld;      // dodge button held (the warden's shield)
    public Vector2 GuardAim;    // right stick, else the mouse (keyboard + mouse), else zero = facing
    /// <summary>The right stick is pushed: the warden's shield goes up that way, no button needed.</summary>
    public bool StickGuard;
}

/// <summary>
/// The hero. This file is the body every hero shares: ground movement with coyote time, jump
/// buffering and variable jump height, free swimming with a breath meter, hazards, health,
/// potions and experience, and the buffering of presses. What each hero fights with lives in
/// the other Player.*.cs files: the blade (Swordsman and Warden), the Swordsman's dodge and
/// charged strike, the Warden's shield and shield dash, and the Vitalist's bolt, hex and heal.
/// Every tunable that upgrades touch lives in <see cref="PlayerStats"/>.
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

    public PlayerStats Stats = new(G.Hero);
    public HeroKind Hero => Stats.Hero;
    private bool IsWarden => Stats.Hero == HeroKind.Warden;
    private bool IsSwordsman => Stats.Hero == HeroKind.Swordsman;
    private bool IsVitalist => Stats.Hero == HeroKind.Vitalist;

    public float Hp = Tune.Hero.StartHp;
    public float Breath = Tune.Hero.BreathSeconds;
    public int Level = 1;
    public int Xp;
    /// <summary>Milestone picks waiting to be offered (every few levels).</summary>
    public int PendingMilestones;
    /// <summary>Potions carried (drink with Q / Y).</summary>
    public int Potions = 1;
    /// <summary>Set each frame while stuck in a web.</summary>
    public float WebbedT;
    private float _hotLeft, _hotRate, _lavaTick, _xpFrac, _slideDust;
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

    // timers / state
    private float _coyote, _jumpBuffer, _invuln, _iframes, _drownTick;
    private float _airDashT, _wallJumpLock, _hurtFlash, _bubbleT, _animT;
    private Vector2 _airDashDir;
    private int _airJumps, _airDashes;
    private bool _jumpCutDone, _wasOnFloor;
    private float _lastFallSpeed, _freeze, _stuckInRock;

    // presses waiting to fire (see BufferPresses)
    private float _attackBuf, _abilityBuf, _dodgeBuf;
    private Vector2 _attackAim, _abilityAim;
    private PlayerInput _dodgeInput;

    public bool Invulnerable => _invuln > 0 || _iframes > 0;
    /// <summary>Dodging, dashing, invulnerable, or behind a raised shield (an input for the enemy brains).</summary>
    public bool Guarding => IsDodging || IsShieldDashing || Invulnerable || ShieldRaised;
    /// <summary>The hero's ability (charged strike, shield dash, heal) is ready to use.</summary>
    public bool SecondaryReady => Stats.Hero switch
    {
        HeroKind.Warden => _dashCd <= 0,
        HeroKind.Vitalist => _healCd <= 0 && Alimus >= HealCost,
        _ => _chargeCd <= 0 || Charged > 0,
    };
    public int XpToNext => (int)(Tune.Hero.XpBase + Tune.Hero.XpLinear * Level + Tune.Hero.XpQuadratic * Level * Level);

    public override void _Ready()
    {
        CollisionLayer = G.LayerPlayer;
        CollisionMask = G.LayerTerrain;
        FloorMaxAngle = Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees);
        FloorSnapLength = 7f;
        SafeMargin = 0.5f;
        AddChild(new CollisionShape2D { Shape = new CapsuleShape2D { Radius = 6.5f, Height = 26f } });
        ZIndex = 1;
        Anim = SpriteAnimator.Create(Stats.Hero switch { HeroKind.Warden => "warden", HeroKind.Vitalist => "vitalist", _ => "swordsman" });
        Anim.FootOffset = 13f;
        AddChild(Anim);
        Hp = Stats.MaxHp;
        ShieldHp = Stats.ShieldMax;
        Breath = Stats.BreathMax;
        Alimus = Math.Min(Stats.AlimusMax, Tune.Vitalist.AlimusStart);
    }

    public override void _EnterTree() { if (!G.Players.Contains(this)) G.Players.Add(this); }
    public override void _ExitTree() => G.Players.Remove(this);

    public void SyncCharges()
    {
        if (_dodgeCd.Length != Stats.DodgeCharges) Array.Resize(ref _dodgeCd, Stats.DodgeCharges);
    }

    /// <summary>Heals (pickups, potions, the vitalist). The Warden's shield takes a share too.</summary>
    public void Heal(float amount)
    {
        if (Dead || amount <= 0) return;
        float before = Hp;
        Hp = Math.Min(Stats.MaxHp, Hp + amount);
        if (Hp - before >= 1f) G.Fx?.Text(GlobalPosition + new Vector2(0, -22), "+" + Mathf.RoundToInt(Hp - before), new Color(0.4f, 1f, 0.5f), 10);
        if (IsWarden) MendShield(amount * Tune.Warden.HealToShield);
    }

    /// <summary>A gulp of air (the vents' bubbles).</summary>
    public void AddBreath(float seconds) => Breath = Math.Min(Stats.BreathMax, Breath + seconds);

    public void AddXp(int amount)
    {
        _xpFrac += amount * Meta.XpMult;
        int whole = (int)_xpFrac;
        _xpFrac -= whole;
        Xp += whole;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            Progression.AutoLevel(this);
            if (Level % Meta.MilestoneEvery == 0) PendingMilestones++;
        }
    }

    /// <summary>Drinks a potion: part of the heal at once, the rest over the next seconds.</summary>
    public bool DrinkPotion()
    {
        if (Dead || Potions <= 0 || Hp >= Stats.MaxHp - 0.5f) return false;
        Potions--;
        Heal(Stats.MaxHp * Meta.PotionHealNow);
        _hotRate = Stats.MaxHp * Meta.PotionHealOverTime / Meta.PotionHotSeconds;
        _hotLeft = Meta.PotionHotSeconds;
        G.Sfx.Play("heal", GlobalPosition, 0, 0, 0.8f);
        G.Sfx.Play("bubble", GlobalPosition, -4, 0, 0.6f);
        G.Fx.Flash(GlobalPosition, 26, new Color(1f, 0.45f, 0.6f));
        G.Fx.Ring(GlobalPosition, 22, new Color(1f, 0.55f, 0.7f));
        for (int k = 0; k < 10; k++) G.Fx.Ember(GlobalPosition + G.RandDir() * 10, new Color(1f, 0.5f, 0.65f));
        G.Fx.ScreenFlash(new Color(1f, 0.35f, 0.5f), 0.25f);
        Anim.Flash(0.6f);
        return true;
    }

    /// <summary>True while a potion's heal over time is still running.</summary>
    public bool Mending => _hotLeft > 0;

    private PlayerInput ReadInput()
    {
        if (InputOverride != null) return InputOverride();
        if (G.Main.MenuOpen) return default;
        var inp = new PlayerInput
        {
            Move = new Vector2(Input.GetAxis("move_left", "move_right"), Input.GetAxis("move_up", "move_down")),
            Jump = Input.IsActionJustPressed("jump"),
            JumpHeld = Input.IsActionPressed("jump"),
            Dodge = Input.IsActionJustPressed("dodge"),
            GuardHeld = Input.IsActionPressed("dodge"),
            Potion = Input.IsActionJustPressed("potion"),
        };
        // up works too, but only a deliberate push (running past a door on a tilted stick shouldn't take you down)
        inp.Interact = Input.IsActionJustPressed("interact") || (Input.IsActionJustPressed("move_up") && Math.Abs(inp.Move.X) < 0.5f);
        bool mouseAttack = Input.IsActionJustPressed("attack");
        bool kbAttack = Input.IsActionJustPressed("attack_alt");
        bool mouseAbility = Input.IsActionJustPressed("ability");
        bool kbAbility = Input.IsActionJustPressed("ability_alt");
        inp.Attack = mouseAttack || kbAttack;
        inp.Ability = mouseAbility || kbAbility;
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        bool stickOn = stick.Length() > 0.35f;
        // the right stick raises the Warden's shield by itself, pointing where it's pushed
        inp.StickGuard = stickOn;
        // the shield points wherever a swing would go: right stick, else the left stick on a
        // controller (else your facing), or the mouse
        if (stickOn) inp.GuardAim = stick.Normalized();
        else if (G.Main.UsingPad) inp.GuardAim = inp.Move.Length() > 0.3f ? inp.Move.Normalized() : Vector2.Zero;
        else inp.GuardAim = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        if (stickOn) inp.Aim = stick.Normalized();
        else if (kbAttack || kbAbility || G.Main.UsingPad) inp.Aim = inp.Move.Length() > 0.2f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        else inp.Aim = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        return inp;
    }

    /// <summary>
    /// Presses are remembered for a moment (Tune.Hero.PressBuffer) and fire as soon as they're
    /// allowed. They're read even during a hit-stop, so a combo pressed on the impact follows the
    /// instant the freeze ends instead of being lost.
    /// </summary>
    private void BufferPresses(in PlayerInput inp)
    {
        var aim = inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : new Vector2(Facing, 0);
        if (inp.Attack) { _attackBuf = Tune.Hero.PressBuffer; _attackAim = aim; }
        if (inp.Ability) { _abilityBuf = Tune.Hero.PressBuffer; _abilityAim = aim; }
        if (inp.Dodge) { _dodgeBuf = Tune.Hero.PressBuffer; _dodgeInput = inp; }
        if (inp.Jump) _jumpBuffer = Tune.Hero.JumpBuffer;
        if (inp.Potion) DrinkPotion();
        if (inp.Interact) TryInteract();
    }

    /// <summary>Walks into an exit tunnel the hero stands at (exits no longer take you by surprise).</summary>
    private void TryInteract()
    {
        if (Dead || G.Main.MenuOpen) return;
        foreach (var n in G.World.GetChildren())
            if (n is Portal portal && portal.Reaches(GlobalPosition)) { portal.Enter(); return; }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _animT += dt;
        var cave = G.Cave;
        if (Dead) { Anim.TimeMult = 1; Anim.Position = Vector2.Zero; DeadPhysics(cave, dt); return; }
        var inp = ReadInput();
        BufferPresses(inp);
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

        TickTimers(dt);
        switch (Stats.Hero)
        {
            case HeroKind.Warden: UpdateShield(inp, dt); break;
            case HeroKind.Vitalist: TickVitalist(dt); break;
            default: TickSwordsman(dt); break;
        }

        bool wasInWater = InWater;
        InWater = cave.IsWater(GlobalPosition + new Vector2(0, 2));
        HeadUnder = cave.IsWater(GlobalPosition + new Vector2(0, -9));
        if (InWater != wasInWater && Math.Abs(Velocity.Y) > 80)
        {
            G.Sfx.Play("splash", GlobalPosition, -4);
            G.Fx.Splash(new Vector2(GlobalPosition.X, cave.WaterY), Math.Clamp(Math.Abs(Velocity.Y) / 500f, 0.2f, 1f), new Color(0.65f, 0.88f, 1f, 0.9f));
            if (InWater) G.Fx.Bubbles(GlobalPosition, 8);
        }
        Hazards(cave, dt);
        // hitting the water soaks up most of the speed you carried in
        if (InWater && !wasInWater) Velocity *= Tune.Hero.WaterEntryDamp;
        UpdateBreath(dt);
        Unstick(cave, dt);

        if (_dashT <= 0)
        {
            if (inp.Move.X > 0.2f) Facing = 1; else if (inp.Move.X < -0.2f) Facing = -1;
        }

        var v = Velocity;
        bool onFloor = IsOnFloor();
        _wallSliding = false;
        _jumpedFromGround = false;
        bool surfaceFloat = !onFloor && !InWater && GlobalPosition.Y > cave.WaterY - 10 && cave.IsWater(GlobalPosition + new Vector2(0, 14));
        if (onFloor || surfaceFloat) { _coyote = Tune.Hero.CoyoteTime; _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; }

        // the dodge button: the Swordsman rolls, the Vitalist hexes (the Warden's raises her shield)
        if (_dodgeBuf > 0 && DodgeButton(_dodgeInput)) _dodgeBuf = 0;

        if (_dodgeT > 0) v = DodgeMotion(v, dt);
        else if (_dashT > 0) v = DashMotion(v, dt);
        else if (_airDashT > 0)
        {
            v = _airDashDir * Tune.Hero.AirDashSpeed;
            if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.55f, 0.9f, 1f), 0.2f);
            if (_airDashT - dt <= 0) v *= 0.5f;
        }
        else if (InWater) v = Swim(inp, v, dt, cave);
        else v = Platform(inp, v, dt, onFloor);

        if (_airDashT > 0) _airDashT -= dt;

        _lastFallSpeed = v.Y;
        Velocity = v;
        MoveAndSlide();
        bool nowFloor = IsOnFloor();
        if (nowFloor && !_wasOnFloor && _lastFallSpeed > 260)
        {
            G.Sfx.Play("land", GlobalPosition, -6);
            G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 3 + (int)(_lastFallSpeed / 120f), 1.2f);
            if (_lastFallSpeed > 450) G.Fx.Shockwave(GlobalPosition + new Vector2(0, 13), 26, new Color(1, 1, 1, 0.35f), 0.25f);
            Anim.Once("land", 1);
        }
        _wasOnFloor = nowFloor;

        // the attack button, then the ability button (each fires once it's allowed)
        if (_attackBuf > 0 && Primary(_attackAim)) _attackBuf = 0;
        if (_abilityBuf > 0 && Ability(_abilityAim)) _abilityBuf = 0;
        if (_swingT >= 0) UpdateSwing(dt);

        UpdateAnimation(nowFloor);
        QueueRedraw();
    }

    /// <summary>The attack button: a swing, or the Vitalist's drain bolt. True once it fires.</summary>
    private bool Primary(Vector2 aim) => Stats.Hero switch
    {
        HeroKind.Vitalist => CastBolt(aim),
        HeroKind.Warden => _dashT <= 0 && TrySwing(aim),
        _ => TrySwing(aim),
    };

    /// <summary>The ability button: charged strike, shield dash, or heal. True once it fires.</summary>
    private bool Ability(Vector2 aim) => Stats.Hero switch
    {
        HeroKind.Warden => TryShieldDash(aim),
        HeroKind.Vitalist => TryHeal(),
        _ => TryCharge(),
    };

    /// <summary>The dodge button's press: a roll or a hex (the Warden's shield reads the button itself).</summary>
    private bool DodgeButton(in PlayerInput inp) => Stats.Hero switch
    {
        HeroKind.Swordsman => TryDodge(inp),
        HeroKind.Vitalist => TryHex(),
        _ => true,
    };

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

    /// <summary>Lava: it burns (a big share of your health) and throws you back up out of it.</summary>
    private void Hazards(CaveData cave, float dt)
    {
        if (!cave.IsLava(GlobalPosition + new Vector2(0, 8))) return;
        if (G.Chance(0.5f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), 8), new Color(1f, 0.6f, 0.2f));
        if (_lavaTick > 0) return;
        _lavaTick = 0.7f;
        G.Sfx.Play("lava", GlobalPosition, 0, 0.1f, 0.8f);
        G.Fx.Splash(new Vector2(GlobalPosition.X, cave.WaterY), 0.8f, new Color(1f, 0.55f, 0.15f));
        G.Fx.Smoke(GlobalPosition, 4, new Color(0.25f, 0.2f, 0.2f, 0.5f));
        TakeRawDamage(Math.Min(Stats.MaxHp * 0.16f + 4, 30 * G.DepthDmg) * (1f - Stats.DamageReduction), "burn");
        Velocity = new Vector2(Velocity.X * 0.5f, -BaseJumpV * 1.05f);
        _coyote = 0;
        _invuln = Math.Max(_invuln, 0.3f);
    }

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
        _waveCd -= dt; WebbedT -= dt; _lavaTick -= dt;
        _attackBuf -= dt; _abilityBuf -= dt; _dodgeBuf -= dt;
        _chargeCd -= dt; _dashCd -= dt; _boltCd -= dt; _hexCd -= dt; _healCd -= dt;
        if (_hotLeft > 0)
        {
            _hotLeft -= dt;
            Heal(_hotRate * dt);
            if (G.Chance(0.15f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-7, 7), G.Range(-10, 10)), new Color(1f, 0.55f, 0.7f));
        }
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
        if (WebbedT > 0) target *= 0.45f;
        float accel = onFloor ? Tune.Hero.GroundAccel : (_wallJumpLock > 0 ? 350f : Tune.Hero.AirAccel);
        // frozen ground: slow to get going and slower to stop
        if (onFloor && G.Biome != null && G.Biome.Slippery)
        {
            accel *= Math.Abs(target) > Math.Abs(v.X) && Math.Sign(target) == Math.Sign(v.X) ? 0.35f : 0.12f;
            _slideDust -= dt;
            if (Math.Abs(v.X - target) > 60 && _slideDust <= 0) { _slideDust = 0.05f; G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 1, 0.5f, new Color(0.85f, 0.95f, 1f, 0.6f)); }
        }
        else if (onFloor && Math.Abs(v.X) > 150 && Math.Sign(target) != Math.Sign(v.X) && G.Chance(0.3f)) G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 1, 0.6f);
        v.X = Mathf.MoveToward(v.X, target, accel * dt);
        if (_lungeT > 0) v.X = _lungeDir * Math.Max(Math.Abs(v.X) * Math.Sign(v.X) * _lungeDir, LungeSpeed); // sword lunge
        v.Y = Math.Min(v.Y + Gravity * dt * (v.Y > 0 ? Tune.Hero.FallGravityMult : 1f), MaxFall);

        float jumpV = BaseJumpV * MathF.Sqrt(Stats.JumpMult) * (WebbedT > 0 ? 0.75f : 1f);
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
                G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 3);
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

    public void OnDealtDamage(float dealt)
    {
        if (Stats.LifeSteal > 0) Hp = Math.Min(Stats.MaxHp, Hp + dealt * Stats.LifeSteal);
        if (IsVitalist) GainAlimus(dealt * Stats.AlimusGain);
    }

    public void OnKill()
    {
        Kills++;
        if (Stats.HealOnKill > 0) Heal(Stats.HealOnKill);
    }

    /// <summary>True when the last Hurt was stopped by the shield (the attacker's strike is spent).</summary>
    public bool LastHitBlocked { get; private set; }

    /// <summary>Returns the damage taken. <paramref name="source"/> is credited with it (enemy learning).</summary>
    public float Hurt(float dmg, Vector2 from, float knock = 230f, Enemy source = null)
    {
        LastHitBlocked = false;
        if (Dead || Invulnerable) return 0;
        bool melee = source != null && GodotObject.IsInstanceValid(source) && source.GlobalPosition.DistanceTo(GlobalPosition) < 70;
        var block = TryBlock(from, dmg, melee ? source : null);
        if (block.Blocked)
        {
            // the shield took it: at most the share it lets through, with no flinch
            LastHitBlocked = true;
            return ApplyChip(block.Through, source);
        }
        dmg *= 1f - Stats.DamageReduction;
        if (source != null && GodotObject.IsInstanceValid(source))
        {
            source.CreditDamage(dmg);
            // mutual bounce: an enemy that struck you in melee rebounds a little too
            if (melee) source.Recoil(source.GlobalPosition.X - GlobalPosition.X);
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
        _dodgeT = 0; _airDashT = 0; _dashT = 0;
        return dmg;
    }

    /// <summary>Freezes just the hero for a hit-stop (holding the pose from this very frame).</summary>
    public void Freeze(float seconds)
    {
        _freeze = Math.Max(_freeze, seconds);
        if (Anim != null) Anim.TimeMult = 0;
    }
    /// <summary>Seconds of hit-stop left.</summary>
    public float FreezeLeft => _freeze;

    private int _lastStandDepth = -1;

    private void TakeRawDamage(float dmg, string kind)
    {
        Hp -= dmg;
        if (Hp <= 0 && Stats.LastStand && _lastStandDepth != G.Depth)
        {
            // once per depth, a killing blow leaves you standing behind a whole shield
            _lastStandDepth = G.Depth;
            Hp = 1;
            _invuln = Tune.Warden.LastStandInvuln;
            RefillShield();
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "LAST STAND", new Color(1f, 0.85f, 0.4f), 13, 1.5f);
            G.Fx.Ring(GlobalPosition, 24, new Color(1f, 0.85f, 0.4f));
            G.Sfx.Play("roar", GlobalPosition, -8, 0, 1.8f);
        }
        bool chip = kind == "chip";
        _hurtFlash = chip ? 0.06f : 0.15f;
        G.Fx.Text(GlobalPosition + new Vector2(0, -24), Mathf.RoundToInt(Math.Max(1f, dmg)).ToString(), chip ? new Color(1f, 0.7f, 0.55f) : new Color(1f, 0.35f, 0.3f), chip ? 10 : 12);
        if (!chip) G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 8, 120, 2.2f, 0.45f);
        G.Fx.AddShake(kind == "drown" ? 2 : chip ? 1.5f : 6);
        if (!chip) G.Sfx.Play(kind == "drown" ? "bubble" : "hurt", GlobalPosition, -2);
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

    /// <summary>
    /// The body is a sprite (see <see cref="Anim"/>, which drives the 3D model); this draws the
    /// 2D debug view's extras: the blade smear and the Warden's guard.
    /// </summary>
    public override void _Draw()
    {
        if (Dead) return;
        if (IsWarden) DrawGuard();
        DrawSmear();
    }
}
