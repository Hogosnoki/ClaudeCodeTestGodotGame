using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Cavern Colossus, guardian of every cave's far chamber. Cycles through a leaping slam
/// (shockwaves), a roar that shakes stalactites loose over the arena, and a head-down charge
/// that stuns it against the wall (take the opening!). Below half health it gets faster and
/// calls bats to its aid.
/// </summary>
public partial class CavernColossus : Enemy
{
    private enum S { Intro, Walk, LeapCrouch, Leap, Land, Roar, ChargeWindup, Charge, Stunned }
    private S _s = S.Intro;
    private float _t, _next = 1.2f;
    private int _lastAttack = -1;
    private Room _room;
    private bool _phase2;
    /// <summary>For the 3D model: enraged (the core burns red).</summary>
    public bool Phase2 => _phase2;

    public CavernColossus() { MaxHp = Tune.Boss.Hp; BodyRadius = 30; ContactDamage = Tune.Boss.Contact; XpValue = Tune.Boss.Xp; KnockResist = 1f; IsBoss = true; }

    public void Init(Room room) => _room = room;

    protected override void Setup()
    {
        DisplayName = "Cavern Colossus";
        Awake = true;
        UseSprite("colossus");
    }

    protected override Color BloodColor => new(0.65f, 0.6f, 0.55f);

    public override float Hurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (_s == S.Stunned) dmg *= 1.5f;
        if (_s == S.Intro) return 0;
        float d = base.Hurt(dmg, knock, hitPos);
        if (!_phase2 && Hp < MaxHp * Tune.Boss.EnrageAt && !Dead)
        {
            _phase2 = true;
            G.Sfx.Play("roar", GlobalPosition, 2, 0, 0.85f);
            G.Fx.AddShake(10);
            G.Fx.Text(GlobalPosition + new Vector2(0, -50), "ENRAGED", new Color(1f, 0.4f, 0.2f), 16, 1.5f);
        }
        return d;
    }

    private float Speed => _phase2 ? Tune.Boss.EnragedSpeedMult : 1f;

    protected override void Think(float dt)
    {
        _t += dt;
        // Leash: never let the colossus leave (or fall out of) its arena.
        if (_room != null && (InWater || Math.Abs(GlobalPosition.X - _room.Center.X) > _room.RxPx + 30 || GlobalPosition.Y > _room.Floor.Y + 60))
        {
            G.Fx.Burst(GlobalPosition, new Color(0.6f, 0.55f, 0.5f), 20, 200, 3f, 0.5f);
            GlobalPosition = _room.Floor + new Vector2(0, -BodyRadius - 20);
            Velocity = Vector2.Zero;
            ToWalk();
        }
        var v = Velocity;
        bool floor = IsOnFloor();
        switch (_s)
        {
            case S.Intro:
                ApplyGravity(dt);
                if (floor && _t > 0.2f)
                {
                    G.Sfx.Play("roar", GlobalPosition, 3);
                    G.Fx.AddShake(12);
                    Go(S.Roar);
                    _t = 0.5f; // shortened first roar
                }
                return;
            case S.Walk:
            {
                int dirP = Math.Sign(ToP.X) == 0 ? (int)Face : Math.Sign(ToP.X);
                int walk = Intent == BackOff ? -dirP : dirP;
                Face = walk;
                v.X = Mathf.MoveToward(v.X, walk * Tune.Boss.WalkSpeed * Speed, 400 * dt);
                _next -= dt;
                if (Intent >= LeapSlam && CanAct(Intent)) { StartAttack(Intent - LeapSlam); Consume(); }
                break;
            }
            case S.LeapCrouch:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.55f / Speed)
                {
                    float air = 0.85f;
                    float tx = Mathf.Clamp(P.GlobalPosition.X, _room.Center.X - _room.RxPx + 50, _room.Center.X + _room.RxPx - 50);
                    v = new Vector2((tx - GlobalPosition.X) / air / MoveScale, -0.5f * Grav * air); // lands on target despite MoveScale
                    G.Sfx.Play("jump", GlobalPosition, 0, 0, 0.4f);
                    Go(S.Leap);
                }
                break;
            case S.Leap:
                if (floor && _t > 0.15f)
                {
                    G.Sfx.Play("slam", GlobalPosition, 3);
                    G.Fx.AddShake(12);
                    var foot = GlobalPosition + new Vector2(0, BodyRadius);
                    G.Fx.Burst(foot, new Color(0.6f, 0.55f, 0.5f), 30, 260, 3.5f, 0.6f);
                    for (int s = -1; s <= 1; s += 2)
                        G.Spawn(new Shockwave { Position = foot + new Vector2(s * 34, 0), Dir = s, Damage = Tune.Boss.ShockwaveDamage * DmgK, Size = 1.6f, Speed = 300 * Speed, Life = 2f, Source = this });
                    if (DistP < 55) P.Hurt(Tune.Boss.SlamDamage * DmgK, GlobalPosition, 350, this);
                    v.X = 0;
                    Go(S.Land);
                }
                break;
            case S.Land:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.7f / Speed) ToWalk();
                break;
            case S.Roar:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.9f)
                {
                    int n = _phase2 ? 10 : 7;
                    for (int k = 0; k < n; k++)
                    {
                        float x = _room.Center.X + G.Range(-_room.RxPx + 30, _room.RxPx - 30);
                        if (k == 0) x = P.GlobalPosition.X;
                        if (G.Cave.FindCeiling(new Vector2(x, _room.Floor.Y - 30), _room.RyPx * 2.5f, out var ce))
                            G.Spawn(new FallingRock { Position = ce + new Vector2(0, 14), Damage = Tune.Boss.RockDamage * DmgK, Source = this });
                    }
                    if (_phase2)
                        for (int k = 0; k < 2; k++)
                        {
                            var bat = new Bat { Position = _room.Center + new Vector2(G.Range(-120, 120), -_room.RyPx * 0.5f) };
                            G.Spawn(bat);
                        }
                    ToWalk();
                }
                break;
            case S.ChargeWindup:
                v.X = Mathf.MoveToward(v.X, -Face * 30, 600 * dt);
                if (_t > 0.65f / Speed) { Go(S.Charge); G.Sfx.Play("roar", GlobalPosition, -4, 0, 1.4f); }
                break;
            case S.Charge:
                v.X = Face * Tune.Boss.ChargeSpeed * Speed;
                if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition + new Vector2(-Face * 20, BodyRadius), new Color(0.6f, 0.55f, 0.5f, 0.8f), 1, 80, 3f, 0.4f, 200);
                if (IsOnWall() || _t > 2.2f)
                {
                    G.Sfx.Play("slam", GlobalPosition, 2);
                    G.Fx.AddShake(14);
                    for (int k = 0; k < 3; k++)
                    {
                        float x = GlobalPosition.X - Face * G.Range(40, 200);
                        if (G.Cave.FindCeiling(new Vector2(x, GlobalPosition.Y - 20), 400, out var ce))
                            G.Spawn(new FallingRock { Position = ce + new Vector2(0, 14), Damage = Tune.Boss.RockDamage * 0.85f * DmgK, Source = this });
                    }
                    v.X = -Face * 120;
                    v.Y = -200;
                    Go(S.Stunned);
                }
                break;
            case S.Stunned:
                v.X = Mathf.MoveToward(v.X, 0, 500 * dt);
                if (_t > 1.6f) ToWalk();
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
        ContactActive = _s != S.Stunned;
    }

    private void Go(S s) { _s = s; _t = 0; }

    private void ToWalk()
    {
        Go(S.Walk);
        _next = G.Range(1.1f, 2.0f) / Speed;
    }

    // ---- brain interface: while walking it picks when (and which) attack to start
    private const int Advance = 0, BackOff = 1, LeapSlam = 2, RoarAttack = 3, ChargeAttack = 4;
    private static readonly string[] Moves = { "advance", "back off", "leap slam", "roar", "charge" };
    private int _teacherPick = -1;
    protected override string BrainName => "boss";
    protected override string[] Actions => Moves;
    protected override bool Busy => _s != S.Walk;
    protected override bool Striking => _s is S.Charge or S.Leap;
    public override bool Attacking => _s is S.LeapCrouch or S.Leap or S.ChargeWindup or S.Charge;
    protected override bool IsAttack(int a) => a >= LeapSlam;
    protected override float AttackReady => _next <= 0 ? 1 : 0;
    protected override bool CanAct(int a) => a < LeapSlam || _next <= 0;

    /// <summary>The scripted boss walks for a while, then picks a random attack (rarely the same twice).</summary>
    protected override int Teacher()
    {
        if (_s != S.Walk || _next > 0) return Advance;
        if (_teacherPick < 0)
        {
            int a;
            do a = G.RangeI(0, 2); while (a == _lastAttack && G.Chance(0.7f));
            _teacherPick = a;
        }
        return LeapSlam + _teacherPick;
    }

    private void StartAttack(int a)
    {
        _lastAttack = a;
        _teacherPick = -1;
        Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
        switch (a)
        {
            case 0: Go(S.LeapCrouch); break;
            case 1: Go(S.Roar); G.Sfx.Play("roar", GlobalPosition, 0); G.Fx.AddShake(6); break;
            default: Go(S.ChargeWindup); break;
        }
    }

    protected override void Animate()
    {
        switch (_s)
        {
            case S.Intro: Anim.Loop(IsOnFloor() ? "idle" : "leap"); break;
            case S.Walk: Anim.Loop("walk", 1.2f * Speed); break;
            case S.LeapCrouch: Anim.Loop("crouch", 8f / (0.55f / Speed * 24f)); break;
            case S.Leap: Anim.Loop("leap"); break;
            case S.Land: Anim.Loop("land", 8f / (0.7f / Speed * 24f)); break;
            case S.Roar: Anim.Loop("roar", 20f / (0.9f * 24f)); break;
            case S.ChargeWindup: Anim.Loop("charge_windup", 10f / (0.65f / Speed * 24f)); break;
            case S.Charge: Anim.Loop("charge", 1.4f); break;
            case S.Stunned: Anim.Loop("stunned"); break;
        }
        // the one-shot clips above are driven as "loops" of non-looping clips: restart them on state entry
        if (_s != _animState) { _animState = _s; Anim.Sprite.Frame = 0; Anim.Sprite.Play(); }
        Anim.AllowTurns = _s == S.Walk;
        Anim.Modulate = _phase2 ? new Color(1f, 0.78f, 0.7f) : Colors.White;
    }

    private S _animState = S.Intro;

    public override void _Draw()
    {
        var core = _phase2 ? new Color(1f, 0.35f, 0.15f) : new Color(0.4f, 0.9f, 1f);
        float pulse = 0.6f + 0.4f * MathF.Sin(T * (_phase2 ? 9 : 4));
        DrawCircle(Vector2.Zero, 60, new Color(core, 0.05f * pulse));
        DrawHealthBar(); // (bosses have their own bar; this shows the brain's label while training)
    }
}
