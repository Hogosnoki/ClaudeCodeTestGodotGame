using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Darting fish: hesitates, then lunges at swimmers. If you stand near the shore it leaps out
/// after you and flops around on land (suffocating slowly) until it wriggles back into the water.
/// </summary>
public partial class Fish : Enemy
{
    private int _state; // 0 swim, 1 airborne, 2 flop
    private float _dartT, _dartCd, _leapCd = 2f, _flopCd, _wanderA;
    private Vector2 _home;
    private Color _col;

    protected override bool UsesGravity => _state != 0;

    public Fish() { MaxHp = Tune.Fish.Hp; BodyRadius = 6; ContactDamage = Tune.Fish.Contact; XpValue = Tune.Fish.Xp; }

    protected override void Setup()
    {
        DisplayName = "Cave Fish";
        _home = GlobalPosition;
        _wanderA = G.Range(0, Mathf.Tau);
        bool orange = G.Chance(0.5f);
        _col = orange ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.35f, 0.75f, 0.8f);
        UseSprite(orange ? "fish" : "fish2");
        if (!orange) ContactDamage *= Tune.Fish.BlueDamageMult; // the blue ones bite softer
        MotionMode = MotionModeEnum.Floating;
        _dartCd = G.Range(0.3f, 1.2f);
    }

    protected override void Think(float dt)
    {
        _dartCd -= dt; _dartT -= dt; _leapCd -= dt; _flopCd -= dt;
        var cave = G.Cave;
        bool water = InWater;
        var v = Velocity;
        if (_state == 0 && !water && GlobalPosition.Y < cave.WaterY) { _state = 1; MotionMode = MotionModeEnum.Grounded; }
        if (_state != 0 && water) { _state = 0; MotionMode = MotionModeEnum.Floating; G.Sfx.Play("splash", GlobalPosition, -12, 0.2f, 1.4f); }

        switch (_state)
        {
            case 0:
            {
                bool playerIn = P.InWater;
                if (_dartT > 0) { /* keep dashing */ }
                else if (Intent == Dart && _dartCd <= 0)
                {
                    _dartT = 0.45f; _dartCd = G.Range(1.2f, 2.0f) * (Elite ? 0.6f : 1f);
                    v = ToP.Normalized() * Tune.Fish.DartSpeed * (Elite ? 1.18f : 1f);
                    Consume();
                }
                else if (Intent == Leap && CanAct(Leap))
                {
                    // Shore ambush: leap at a player standing near the water.
                    _leapCd = G.Range(3f, 5f);
                    float t = 0.75f;
                    var target = P.GlobalPosition;
                    // aimed so it lands on you once MoveScale shrinks the arc
                    v = new Vector2((target.X - GlobalPosition.X) / MoveScale / t, (target.Y - GlobalPosition.Y) / MoveScale / t - 0.5f * Grav * t);
                    v.Y = Math.Max(v.Y, -720);
                    G.Sfx.Play("splash", GlobalPosition, -8, 0.2f, 1.3f);
                    Consume();
                }
                else if (Intent == Approach && playerIn)
                    v = v.MoveToward(ToP.Normalized() * 30 + new Vector2(0, MathF.Sin(T * 5) * 20), 400 * dt);
                else if (Intent == Flee)
                    v = v.MoveToward(-ToP.Normalized() * 90, 300 * dt);
                else
                {
                    _wanderA += G.Range(-2, 2) * dt;
                    var wander = Vector2.Right.Rotated(_wanderA) * 50;
                    if (GlobalPosition.DistanceTo(_home) > 120) wander = (_home - GlobalPosition).Normalized() * 60;
                    v = v.MoveToward(wander, 200 * dt);
                    // drift toward the surface under the player to set up a leap
                    if (Intent == Approach) v = v.MoveToward(new Vector2(Math.Sign(ToP.X) * 60, -40), 250 * dt);
                }
                // Stay under water unless leaping.
                if (GlobalPosition.Y < cave.WaterY + 8 && v.Y < 0 && v.Y > -300) v.Y = 20;
                if (v.X != 0) Face = Math.Sign(v.X);
                Velocity = v;
                break;
            }
            case 1:
                ApplyGravity(dt);
                if (IsOnFloor()) { _state = 2; G.Sfx.Play("flop", GlobalPosition, -4); }
                break;
            default:
            {
                Hp -= 1.2f * dt; // suffocating
                if (Hp <= 0) { Die(); return; }
                v.X = Mathf.MoveToward(v.X, 0, 300 * dt);
                if (IsOnFloor() && _flopCd <= 0)
                {
                    _flopCd = G.Range(0.35f, 0.6f);
                    // Flop toward the player if close, otherwise toward the water.
                    float dir = DistP < 160 ? Math.Sign(ToP.X) : (G.Chance(0.5f) ? 1 : -1);
                    v = new Vector2(dir * G.Range(60, 130), -G.Range(140, 230));
                    Face = dir;
                    G.Sfx.Play("flop", GlobalPosition, -10);
                }
                Velocity = v;
                ApplyGravity(dt);
                break;
            }
        }
    }

    // ---- brain interface
    private const int Drift = 0, Approach = 1, Flee = 2, Dart = 3, Leap = 4;
    private static readonly string[] Moves = { "drift", "approach", "flee", "dart", "leap" };
    protected override string BrainName => "fish";
    protected override string[] Actions => Moves;
    protected override bool Busy => _state != 0 || _dartT > 0;
    protected override bool Striking => _dartT > 0 || _state == 1;
    protected override bool IsAttack(int a) => a is Dart or Leap;
    protected override void OnInterrupted() => _dartT = 0;
    protected override float AttackReady => _dartCd <= 0 ? 1 : 0;

    protected override bool CanAct(int a) => a switch
    {
        Dart => _dartCd <= 0,
        Leap => _leapCd <= 0 && GlobalPosition.Y < G.Cave.WaterY + 90 && ToP.Y < 0,
        _ => true,
    };

    protected override int Teacher()
    {
        if (!Awake) return Drift;
        bool playerIn = P.InWater;
        if (playerIn && DistP < Aggro(Tune.Fish.AggroRange)) return _dartCd <= 0 ? Dart : Approach;
        var rel = ToP;
        if (!playerIn && _leapCd <= 0 && Math.Abs(rel.X) < 190 && P.GlobalPosition.Y > G.Cave.WaterY - 170 && GlobalPosition.Y < G.Cave.WaterY + 90 && rel.Y < 0) return Leap;
        return !playerIn && Math.Abs(rel.X) < 260 ? Approach : Drift;
    }

    protected override void Animate()
    {
        var v = Velocity;
        if (_state == 1 && Math.Abs(v.X) > 5) Face = Math.Sign(v.X);
        Anim.AllowTurns = _state == 0;
        Anim.Loop(_state == 0 ? (_dartT > 0 ? "dart" : "swim") : _state == 1 ? "leap" : "flop",
                  _state == 0 ? Math.Clamp(v.Length() / 80f, 0.6f, 2f) : 1f);
        float rot = 0;
        if (_state != 2 && v.Length() > 20) rot = Mathf.Clamp(MathF.Atan2(v.Y, Math.Max(Math.Abs(v.X), 1f)), _state == 1 ? -1.3f : -0.7f, _state == 1 ? 1.3f : 0.7f) * Face;
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, rot, 0.2f);
    }

    protected override Vector2 DeathDrift => InWater ? new Vector2(0, -30) : Vector2.Zero;

    public override void _Draw() => DrawHealthBar();
}

/// <summary>Stationary spiny urchin on the sea floor; periodically bristles its spikes outward.</summary>
public partial class Urchin : Enemy
{
    public override string HitSound => "hit_stone";
    private float _cycle;
    private bool _hitThisPulse;

    protected override bool UsesGravity => false;

    public Urchin() { MaxHp = Tune.Urchin.Hp; BodyRadius = 10; ContactDamage = Tune.Urchin.Contact; XpValue = Tune.Urchin.Xp; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Urchin";
        _cycle = G.Range(0, 2.5f);
        UseSprite("urchin");
        Anim.AllowTurns = false;
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
    }

    private float SpikeLen()
    {
        float c = _cycle;
        if (c < 1.6f) return 7;                       // resting
        if (c < 2.1f) return 7 - (c - 1.6f) * 8;      // retract (telegraph)
        if (c < 2.25f) return 3 + (c - 2.1f) / 0.15f * 17; // burst
        if (c < 2.6f) return 20;
        return 20 - (c - 2.6f) / 0.4f * 13;
    }

    // ---- brain interface: it can hold its spikes in (rest) or start a pulse early (bristle)
    private const int Rest = 0, Bristle = 1;
    private const float RestEnd = 1.59f, MinRest = 0.5f;
    private static readonly string[] Moves = { "rest", "bristle" };
    protected override string BrainName => "urchin";
    protected override string[] Actions => Moves;
    protected override bool Busy => _cycle > RestEnd;
    protected override bool CanAct(int a) => a != Bristle || _cycle >= MinRest;
    protected override float AttackReady => Math.Clamp(_cycle / RestEnd, 0, 1);
    protected override bool IsAttack(int a) => a == Bristle;
    public override bool Attacking => _cycle > 2.05f && _cycle < 2.7f;
    protected override void OnInterrupted() { if (_cycle > 1.6f && _cycle < 2.6f) _cycle = 2.6f; _hitThisPulse = true; }
    protected override int Teacher() => _cycle >= RestEnd - 0.05f ? Bristle : Rest;

    protected override void Think(float dt)
    {
        float prev = _cycle;
        bool go = Intent == Bristle;
        if (go && _cycle < RestEnd) { _cycle = RestEnd; prev = _cycle; Consume(); }
        _cycle += dt * (Elite ? 1.3f : 1f);
        if (!go && prev <= RestEnd && _cycle > RestEnd) _cycle = RestEnd; // hold the spikes in
        if (_cycle > 3f) { _cycle -= 3f; _hitThisPulse = false; }
        if (prev < 2.1f && _cycle >= 2.1f && DistP < 400) G.Sfx.Play("spike", GlobalPosition, -6);
        float reach = (BodyRadius + SpikeLen()) * Size;
        if (!_hitThisPulse && SpikeLen() > 12 && DistP < reach + 6) { P.Hurt(Tune.Urchin.SpikeDamage * DmgK, GlobalPosition, source: this); _hitThisPulse = true; }
    }

    protected override void Animate()
    {
        // the sprite's 72-frame pulse is the same 3 s cycle the damage uses
        if (!Anim.OnceActive) Anim.SetFrameManual("pulse", _cycle);
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// Lurks in a burrow in an underwater wall with only its eyes showing (immune), then lunges out
/// along a line at a nearby swimmer, bites, and slowly retracts -- vulnerable while extended.
/// </summary>
public partial class Eel : Enemy
{
    public Vector2 WallNormal = Vector2.Up;
    private Vector2 _home, _target, _head;
    /// <summary>For the 3D model: the burrow the body runs back into.</summary>
    public Vector2 Home => _home;
    private int _state; // 0 hidden, 1 lunge, 2 hold, 3 retract
    public override void NetState(NetIO io) { io.Sync(ref _state); io.Sync(ref _home); io.Sync(ref WallNormal); }
    private float _stateT, _cd = 1f;

    protected override bool UsesGravity => false;
    public override bool CanBeHit => _state != 0;
    public override float HitRadius => 8 * Size;

    public Eel() { MaxHp = Tune.Eel.Hp; BodyRadius = 7; ContactDamage = Tune.Eel.Contact; XpValue = Tune.Eel.Xp; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Eel";
        _home = GlobalPosition;
        _head = _home;
        UseSprite("eel");
        Anim.AllowTurns = false;
        Anim.ZIndex = 1;
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
        ContactActive = false;
        CollisionMask = 0;
    }

    protected override void Think(float dt)
    {
        _stateT += dt; _cd -= dt;
        float maxLen = Tune.Eel.LungeLength * (Elite ? 1.4f : 1f);
        switch (_state)
        {
            case 0:
                ContactActive = false;
                if (Awake && Intent == Lunge && _cd <= 0)
                {
                    Consume();
                    _state = 1; _stateT = 0;
                    var d = P.GlobalPosition + P.Velocity * 0.15f - _home;
                    _target = _home + d.Normalized() * Math.Min(d.Length() + 20, maxLen);
                    G.Sfx.Play("eel", _home, -2);
                    Anim.Once("bite", 3);
                }
                break;
            case 1:
                ContactActive = true;
                _head = _head.MoveToward(_target, Tune.Eel.LungeSpeed * dt);
                if (_head.DistanceTo(_target) < 2 || _stateT > 0.6f) { _state = 2; _stateT = 0; }
                break;
            case 2:
                if (_stateT > 0.45f) { _state = 3; _stateT = 0; }
                break;
            default:
                _head = _head.MoveToward(_home, 150 * dt);
                if (_head.DistanceTo(_home) < 2) { _state = 0; _stateT = 0; _cd = Tune.Eel.Cooldown * (Elite ? 0.55f : 1f); }
                break;
        }
        GlobalPosition = _head;
        if ((_head - _home).X != 0) Face = Math.Sign((_head - _home).X);
    }

    // ---- brain interface
    private const int Lurk = 0, Lunge = 1;
    private static readonly string[] Moves = { "lurk", "lunge" };
    protected override string BrainName => "eel";
    protected override string[] Actions => Moves;
    protected override bool Busy => _state != 0;
    protected override bool Striking => _state is 1 or 2;
    protected override bool IsAttack(int a) => a == Lunge;
    protected override void OnInterrupted() { if (_state is 1 or 2) { _state = 3; _stateT = 0; } }
    protected override bool CanAct(int a) => a != Lunge || _cd <= 0;
    protected override float AttackReady => _cd <= 0 ? 1 : 0;

    protected override int Teacher()
    {
        float maxLen = Tune.Eel.LungeLength * (Elite ? 1.4f : 1f);
        return _cd <= 0 && P.InWater && P.GlobalPosition.DistanceTo(_home) < maxLen + 20 && G.Cave.LineClear(_home, P.GlobalPosition) ? Lunge : Lurk;
    }

    protected override void Animate()
    {
        var dir = _head - _home;
        if (dir.Length() < 2) dir = WallNormal;
        if (Math.Abs(dir.X) > 0.01f) Face = Math.Sign(dir.X);
        Anim.Face((int)Face, instant: true);
        Anim.Rotation = Face > 0 ? dir.Angle() : Mathf.Wrap(dir.Angle() - Mathf.Pi, -Mathf.Pi, Mathf.Pi);
        Anim.Loop(_state is 1 or 2 ? "hold" : "lurk");
    }

    /// <summary>The sprite is the head; the body is a wavy tube drawn from the burrow to it.</summary>
    public override void _Draw()
    {
        var homeL = _home - GlobalPosition;
        DrawCircle(homeL, 9 * Size, new Color(0.03f, 0.03f, 0.05f, 0.9f));
        var seg = -homeL;
        float len = seg.Length();
        if (len > 3)
        {
            var dir = seg / len;
            var perp = new Vector2(-dir.Y, dir.X);
            int n = Math.Max(3, (int)(len / 5));
            var pts = new Vector2[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                pts[k] = homeL + seg * t * 0.92f + perp * MathF.Sin(t * 9 - T * 14) * 5 * MathF.Sin(t * MathF.PI) * Size;
            }
            var body = new Color(0.235f, 0.353f, 0.204f);
            DrawPolyline(pts, body.Darkened(0.6f), 8.6f * Size);
            DrawPolyline(pts, body, 7 * Size);
            DrawPolyline(pts, new Color(0.725f, 0.753f, 0.392f), 2.2f * Size);
            if (_state is 1 or 2 && G.Chance(0.4f))
                DrawLine(pts[n / 2], pts[n / 2] + G.RandDir() * 8, new Color(0.7f, 0.9f, 1f), 1f);
        }
        DrawHealthBar();
    }
}
