using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elder Dragon, waiting at the bottom of the world in an arena over a lake of lava. It
/// stalks the floor, sweeps a cone of fire across it, takes to the air and dives on you with a
/// ground-shaking landing, lashes its tail at anyone close, and roars down a rain of fire. Below
/// half health it gets faster and calls fire bats. Leaving the floor for the lava only sends it
/// back into the air.
/// </summary>
public partial class Dragon : Enemy
{
    private enum S { Intro, Walk, BreathWindup, Breath, TakeOff, Hover, Dive, Land, TailWindup, Tail, Roar }
    private S _s = S.Intro;
    private float _t, _next = 1.5f, _fireT;
    private int _lastAttack = -1;
    private Room _room;
    private bool _phase2;
    private float _diveX;
    private Vector2 _breathDir;
    /// <summary>For the 3D model: enraged, and where the fire is going.</summary>
    public bool Phase2 => _phase2;
    public Vector2 BreathDir => _breathDir;
    public bool Breathing => _s == S.Breath;

    public Dragon()
    {
        MaxHp = Tune.Dragon.Hp; BodyRadius = 30; ContactDamage = Tune.Dragon.Contact; XpValue = Tune.Dragon.Xp;
        KnockResist = 1f; IsBoss = true; IsGuardian = true;
    }

    public void Init(Room room) => _room = room;

    protected override bool UsesGravity => _s is not (S.TakeOff or S.Hover or S.Dive);

    protected override void Setup()
    {
        DisplayName = "Elder Dragon";
        Title = "THE ELDER DRAGON";
        Awake = true;
        UseSprite("dragon");
    }

    protected override Color BloodColor => new(1f, 0.45f, 0.1f);
    private float Speed => _phase2 ? Tune.Dragon.EnragedSpeedMult : 1f;
    private Vector2 Mouth => GlobalPosition + new Vector2(Face * 60, -26);

    public override float Hurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (_s == S.Intro) return 0;
        float d = base.Hurt(dmg, knock, hitPos);
        if (!_phase2 && Hp < MaxHp * Tune.Dragon.EnrageAt && !Dead)
        {
            _phase2 = true;
            G.Sfx.Play("roar", GlobalPosition, 4, 0, 0.7f);
            G.Fx.AddShake(14);
            G.Fx.ScreenFlash(new Color(1f, 0.4f, 0.1f), 0.35f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -70), "ENRAGED", new Color(1f, 0.4f, 0.2f), 18, 1.6f);
        }
        return d;
    }

    private void Go(S s)
    {
        _s = s; _t = 0;
        ManualMove = s is S.TakeOff or S.Hover or S.Dive;
    }

    private void ToWalk()
    {
        Go(S.Walk);
        _next = G.Range(1.2f, 2.2f) / Speed;
    }

    private float HoverY => _room.Center.Y - _room.RyPx * 0.55f;

    protected override void Think(float dt)
    {
        _t += dt;
        var v = Velocity;
        bool floor = IsOnFloor();
        // lava or a pit: back into the air
        if (!ManualMove && _s != S.Intro && (G.Cave.IsLava(GlobalPosition + new Vector2(0, BodyRadius)) || GlobalPosition.Y > _room.Floor.Y + 60))
        {
            G.Fx.Burst(GlobalPosition + new Vector2(0, BodyRadius), new Color(1f, 0.6f, 0.2f), 20, 200, 3f, 0.5f, -100);
            Go(S.TakeOff);
        }
        switch (_s)
        {
            case S.Intro:
                ApplyGravity(dt);
                if (floor && _t > 0.2f)
                {
                    G.Sfx.Play("roar", GlobalPosition, 4, 0, 0.8f);
                    G.Fx.AddShake(16);
                    G.Fx.Shockwave(GlobalPosition + new Vector2(0, BodyRadius), 120, new Color(1f, 0.7f, 0.4f, 0.8f));
                    Go(S.Roar);
                    _t = 0.4f;
                }
                return;
            case S.Walk:
            {
                int dirP = Math.Sign(ToP.X) == 0 ? (int)Face : Math.Sign(ToP.X);
                int walk = Intent == BackOff ? -dirP : Math.Abs(ToP.X) < 50 ? 0 : dirP;
                if (walk != 0) Face = walk; else Face = dirP;
                v.X = Mathf.MoveToward(v.X, walk * Tune.Dragon.WalkSpeed * Speed, 400 * dt);
                // don't walk into the lava
                if (walk != 0 && !G.Cave.IsSolid(GlobalPosition + new Vector2(walk * 40, BodyRadius + 12)) && !G.Cave.IsSolid(GlobalPosition + new Vector2(walk * 40, BodyRadius + 40))) v.X = 0;
                if (floor && IsOnWall() && walk != 0) v.Y = -300;
                _next -= dt;
                if (Intent >= Breath && CanAct(Intent)) { StartAttack(Intent - Breath); Consume(); }
                break;
            }
            case S.BreathWindup:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (G.Chance(0.5f)) G.Fx.Ember(Mouth, new Color(1f, 0.7f, 0.2f));
                if (_t > 0.75f / Speed)
                {
                    Go(S.Breath);
                    _breathDir = (P.GlobalPosition - Mouth).Normalized();
                    if (_breathDir.X * Face < 0.2f) _breathDir = new Vector2(Face, 0.3f).Normalized();
                    G.Sfx.Play("lava", GlobalPosition, 2, 0, 0.6f);
                }
                break;
            case S.Breath:
            {
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                // the cone sweeps after the player
                var want = (P.GlobalPosition - Mouth).Normalized();
                if (want.X * Face > 0) _breathDir = _breathDir.Slerp(want, Math.Min(1, dt * 1.6f * Speed));
                _fireT -= dt;
                while (_fireT <= 0)
                {
                    _fireT += 0.045f;
                    G.Spawn(new EnemyProjectile
                    {
                        Position = Mouth, Vel = _breathDir.Rotated(G.Range(-0.13f, 0.13f)) * G.Range(250, 320), Grav = -40,
                        Damage = Tune.Dragon.FireDamage * DmgK, Kind = "fire", Radius = 6, Life = 0.85f, Source = this,
                    });
                }
                if (G.Chance(0.2f)) G.Sfx.Play("lava", Mouth, -10, 0.2f, 0.7f);
                if (_t > 1.7f) ToWalk();
                break;
            }
            case S.TakeOff:
            {
                var target = new Vector2(GlobalPosition.X, HoverY);
                GlobalPosition = GlobalPosition.MoveToward(target, 260 * Speed * dt);
                if (G.Chance(0.3f)) G.Fx.Dust(GlobalPosition + new Vector2(G.Range(-30, 30), BodyRadius + 10), 1);
                if (_t < 0.05f) { G.Sfx.Play("airdash", GlobalPosition, 0, 0, 0.4f); G.Fx.Shockwave(GlobalPosition + new Vector2(0, BodyRadius), 70, new Color(1, 1, 1, 0.5f)); }
                if (GlobalPosition.DistanceTo(target) < 6 || _t > 1.6f) Go(S.Hover);
                break;
            }
            case S.Hover:
            {
                float tx = Mathf.Clamp(P.GlobalPosition.X, _room.Center.X - _room.RxPx + 40, _room.Center.X + _room.RxPx - 40);
                var target = new Vector2(tx, HoverY + MathF.Sin(T * 4) * 8);
                GlobalPosition = GlobalPosition.MoveToward(target, 240 * Speed * dt);
                Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
                if (_t > (_phase2 ? 0.8f : 1.1f))
                {
                    _diveX = tx;
                    Go(S.Dive);
                    G.Sfx.Play("roar", GlobalPosition, -2, 0, 1.3f);
                }
                break;
            }
            case S.Dive:
            {
                var np = GlobalPosition + new Vector2(Mathf.MoveToward(GlobalPosition.X, _diveX, 200 * dt) - GlobalPosition.X, 560 * Speed * dt);
                bool hit = G.Cave.IsSolid(np + new Vector2(0, BodyRadius + 2)) || G.Cave.IsLava(np + new Vector2(0, BodyRadius));
                if (G.Chance(0.6f)) G.Fx.Trail(GlobalPosition + new Vector2(G.Range(-20, 20), -20), new Color(1f, 0.6f, 0.2f, 0.5f));
                if (hit || _t > 2.5f)
                {
                    Slam();
                    Go(S.Land);
                    Velocity = Vector2.Zero;
                }
                else GlobalPosition = np;
                break;
            }
            case S.Land:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.8f / Speed) ToWalk();
                break;
            case S.TailWindup:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.6f / Speed)
                {
                    Go(S.Tail);
                    G.Sfx.Play("swing_heavy", GlobalPosition, 4, 0, 0.4f);
                    G.Fx.Swoosh(GlobalPosition + new Vector2(0, 6), -Face, 90, new Color(1f, 0.8f, 0.6f, 0.8f));
                    G.Fx.Swoosh(GlobalPosition + new Vector2(0, 6), Face, 70, new Color(1f, 0.8f, 0.6f, 0.6f));
                    var rel = ToP;
                    if (Math.Abs(rel.X) < 100 && Math.Abs(rel.Y) < 55) P.Hurt(Tune.Dragon.TailDamage * DmgK, GlobalPosition, 420, this);
                    var foot = GlobalPosition + new Vector2(0, BodyRadius);
                    for (int s = -1; s <= 1; s += 2)
                        G.Spawn(new Shockwave { Position = foot + new Vector2(s * 60, 0), Dir = s, Damage = Tune.Dragon.ShockwaveDamage * DmgK * 0.8f, Size = 1.2f, Speed = 280 * Speed, Life = 1.2f, Source = this });
                }
                break;
            case S.Tail:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.6f) ToWalk();
                break;
            case S.Roar:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 1.0f)
                {
                    RainFire();
                    ToWalk();
                }
                break;
        }
        if (!ManualMove)
        {
            Velocity = v;
            ApplyGravity(dt);
        }
    }

    private void Slam()
    {
        G.Sfx.Play("slam", GlobalPosition, 5);
        G.Fx.AddShake(18);
        var foot = GlobalPosition + new Vector2(0, BodyRadius);
        G.Fx.Explosion(foot, new Color(1f, 0.55f, 0.2f), 1.4f);
        G.Fx.Shockwave(foot, 160, new Color(1f, 0.8f, 0.5f, 0.9f));
        for (int s = -1; s <= 1; s += 2)
            G.Spawn(new Shockwave { Position = foot + new Vector2(s * 50, 0), Dir = s, Damage = Tune.Dragon.ShockwaveDamage * DmgK, Size = 1.8f, Speed = 320 * Speed, Life = 2f, Source = this });
        if (DistP < 70) P.Hurt(Tune.Dragon.DiveDamage * DmgK, GlobalPosition, 380, this);
        G.Main.Rumble(0.6f, 0.9f, 0.4f);
    }

    private void RainFire()
    {
        int n = _phase2 ? 11 : 7;
        for (int k = 0; k < n; k++)
        {
            float x = k == 0 ? P.GlobalPosition.X : _room.Center.X + G.Range(-_room.RxPx + 30, _room.RxPx - 30);
            var at = new Vector2(x, _room.Center.Y - _room.RyPx * 0.9f);
            if (G.Cave.FindCeiling(new Vector2(x, _room.Floor.Y - 60), _room.RyPx * 2.5f, out var ce)) at = ce + new Vector2(0, 16);
            G.Spawn(new EnemyProjectile { Position = at, Vel = new Vector2(G.Range(-20, 20), G.Range(20, 80)), Grav = 520, Damage = Tune.Dragon.RainDamage * DmgK, Kind = "lava", Radius = 6, Life = 4f, Source = this });
        }
        if (_phase2)
            for (int k = 0; k < 2; k++)
            {
                var bat = Biomes.Var(new Bat(), "Fire ", "ff9050");
                bat.Position = _room.Center + new Vector2(G.Range(-160, 160), -_room.RyPx * 0.5f);
                bat.Engage();
                G.Spawn(bat);
            }
    }

    // ---- brain interface: while walking it picks when (and which) attack to start
    private const int Advance = 0, BackOff = 1, Breath = 2, DiveAttack = 3, TailAttack = 4, RoarAttack = 5;
    private static readonly string[] Moves = { "advance", "back off", "breath", "dive", "tail", "roar" };
    private int _teacherPick = -1;
    protected override string BrainName => "dragon";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != S.Walk;
    protected override bool Striking => _s is S.Dive;
    public override bool Attacking => _s is S.BreathWindup or S.Breath or S.Dive or S.TailWindup or S.Tail;
    protected override bool IsAttack(int a) => a >= Breath;
    protected override float AttackReady => _next <= 0 ? 1 : 0;
    protected override bool CanAct(int a) => a < Breath || (_next <= 0 && (a != TailAttack || DistP < 140));

    protected override int Teacher()
    {
        if (_s != S.Walk || _next > 0) return Advance;
        if (_teacherPick < 0)
        {
            int a;
            if (DistP < 110 && G.Chance(0.6f)) a = 2;
            else
                do a = G.Chance(_phase2 ? 0.3f : 0.18f) ? 3 : G.RangeI(0, 1); while (a == _lastAttack && G.Chance(0.7f));
            _teacherPick = a;
        }
        return Breath + _teacherPick;
    }

    private void StartAttack(int a)
    {
        _lastAttack = a;
        _teacherPick = -1;
        Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
        switch (a)
        {
            case 0: Go(S.BreathWindup); G.Sfx.Play("roar", GlobalPosition, -6, 0, 1.2f); break;
            case 1: Go(S.TakeOff); break;
            case 2: Go(S.TailWindup); break;
            default: Go(S.Roar); G.Sfx.Play("roar", GlobalPosition, 3, 0, 0.75f); G.Fx.AddShake(8); break;
        }
    }

    protected override void Animate()
    {
        switch (_s)
        {
            case S.Intro: Anim.Loop(IsOnFloor() ? "idle" : "fly"); break;
            case S.Walk: Anim.Loop(Math.Abs(Velocity.X) > 5 ? "walk" : "idle", 1.1f * Speed); break;
            case S.BreathWindup: Anim.Loop("breath_windup", 10f / (0.75f / Speed * 24f)); break;
            case S.Breath: Anim.Loop("breath"); break;
            case S.TakeOff: case S.Hover: Anim.Loop("fly", 1.3f); break;
            case S.Dive: Anim.Loop("dive"); break;
            case S.Land: Anim.Loop("idle"); break;
            case S.TailWindup: Anim.Loop("tail_windup", 8f / (0.6f / Speed * 24f)); break;
            case S.Tail: Anim.Loop("tail"); break;
            case S.Roar: Anim.Loop("roar"); break;
        }
        if (_s != _animState) { _animState = _s; Anim.Sprite.Frame = 0; Anim.Sprite.Play(); }
        Anim.AllowTurns = _s is S.Walk or S.Hover;
        Anim.Modulate = _phase2 ? new Color(1f, 0.8f, 0.72f) : Colors.White;
    }

    private S _animState = S.Intro;

    public override void _Draw()
    {
        float pulse = 0.6f + 0.4f * MathF.Sin(T * (_phase2 ? 8 : 3));
        DrawCircle(Vector2.Zero, 80, new Color(1f, 0.45f, 0.1f, 0.05f * pulse));
        DrawHealthBar();
    }
}
