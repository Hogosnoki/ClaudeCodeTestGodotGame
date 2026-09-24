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
    }

    public Bat() { MaxHp = 12; BodyRadius = 7; ContactDamage = 7; XpValue = 3; }

    protected override void Think(float dt)
    {
        _stateT += dt;
        var cave = G.Cave;
        switch (_state)
        {
            case 0:
                Velocity = Vector2.Zero;
                ContactActive = false;
                if (DistP < 240 && SeesP || HurtFlash > 0) { _state = 1; _stateT = 0; G.Sfx.Play("bat", GlobalPosition, -4); ContactActive = true; }
                return;
            case 1:
            {
                var to = ToP + new Vector2(0, -6);
                var dir = to.Normalized();
                var perp = new Vector2(-dir.Y, dir.X);
                var desired = dir * (170f + (Elite ? 40 : 0)) + perp * MathF.Sin(T * 6 + _wob) * 110f;
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

    public override void _Draw()
    {
        var body = Tint(new Color(0.28f, 0.2f, 0.32f));
        var wing = Tint(new Color(0.2f, 0.14f, 0.25f));
        if (_state == 0)
        {
            Begin(0, 1, 1);
            DrawColoredPolygon(new[] { new Vector2(-5, -6), new Vector2(5, -6), new Vector2(4, 5), new Vector2(0, 8), new Vector2(-4, 5) }, wing);
            DrawCircle(new Vector2(0, 4), 3, body);
            DrawLine(new Vector2(-2, -6), new Vector2(-2, -9), body, 1.5f);
            DrawLine(new Vector2(2, -6), new Vector2(2, -9), body, 1.5f);
            End();
            return;
        }
        float flap = MathF.Sin(T * 22);
        Begin();
        var lw = new[] { new Vector2(-2, -1), new Vector2(-9, -6 - flap * 7), new Vector2(-15, -2 - flap * 9), new Vector2(-11, 2 - flap * 3), new Vector2(-6, 1) };
        var rw = new Vector2[lw.Length];
        for (int k = 0; k < lw.Length; k++) rw[k] = new Vector2(-lw[k].X, lw[k].Y);
        DrawColoredPolygon(lw, wing);
        DrawColoredPolygon(rw, wing);
        DrawCircle(Vector2.Zero, 5, body);
        DrawColoredPolygon(new[] { new Vector2(-4, -3), new Vector2(-3, -9), new Vector2(-1, -4) }, body);
        DrawColoredPolygon(new[] { new Vector2(4, -3), new Vector2(3, -9), new Vector2(1, -4) }, body);
        DrawCircle(new Vector2(2, -1), 1.2f, new Color(1f, 0.25f, 0.2f));
        DrawCircle(new Vector2(-1, -1), 1.2f, new Color(1f, 0.25f, 0.2f));
        End();
        DrawHealthBar();
    }
}

/// <summary>Hops toward the player in arcs and lashes its tongue at close range. Swims too.</summary>
public partial class Frog : Enemy
{
    private float _hopCd = 1f, _tongueCd = 1.5f, _tongueT = -1, _croakT;
    private Vector2 _tongueDir;
    private const float TongueLen = 90f, TongueTime = 0.38f;

    public Frog() { MaxHp = 20; BodyRadius = 9; ContactDamage = 6; XpValue = 4; }

    protected override void Setup() { DisplayName = "Frog"; _croakT = G.Range(1, 5); }

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
        if (_croakT <= 0) { _croakT = G.Range(3, 7); if (DistP < 600) G.Sfx.Play("frog", GlobalPosition, -8); }

        if (_tongueT >= 0)
        {
            _tongueT += dt;
            v.X = Mathf.MoveToward(v.X, 0, 800 * dt);
            float ext = TongueExtent();
            var tip = GlobalPosition + new Vector2(0, -2) + _tongueDir * ext;
            if (!P.Dead && tip.DistanceTo(P.GlobalPosition) < 12) P.Hurt(9f * G.DepthDmg * (Elite ? 1.5f : 1f), GlobalPosition);
            if (_tongueT > TongueTime) _tongueT = -1;
        }
        else if (floor)
        {
            v.X = Mathf.MoveToward(v.X, 0, 900 * dt);
            if (Awake && DistP < 360)
            {
                Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
                if (DistP < TongueLen * Size && _tongueCd <= 0 && SeesP)
                {
                    _tongueT = 0; _tongueCd = 2.2f; _tongueDir = (ToP + new Vector2(0, -4)).Normalized();
                    G.Sfx.Play("tongue", GlobalPosition, -4);
                }
                else if (_hopCd <= 0)
                {
                    _hopCd = G.Range(1.0f, 1.6f);
                    float dir = Math.Sign(ToP.X);
                    v = new Vector2(dir * G.Range(140, 200), -G.Range(330, 400));
                    G.Sfx.Play("jump", GlobalPosition, -14, 0.1f, 0.7f);
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

    public override void _Draw()
    {
        var skin = Tint(new Color(0.32f, 0.62f, 0.28f));
        var belly = Tint(new Color(0.75f, 0.8f, 0.45f));
        if (_tongueT >= 0)
        {
            var tip = new Vector2(0, -2) + _tongueDir * TongueExtent();
            DrawLine(new Vector2(0, -2), tip, new Color(0.95f, 0.45f, 0.55f), 2.5f);
            DrawCircle(tip, 3, new Color(0.95f, 0.45f, 0.55f));
        }
        bool air = !IsOnFloor() && !InWater;
        float croak = _croakT < 0.3f ? 1 : 0;
        Begin();
        if (air)
        {
            DrawLine(new Vector2(-4, 4), new Vector2(-11, 10), skin, 2.5f);
            DrawLine(new Vector2(4, 4), new Vector2(9, 9), skin, 2.5f);
        }
        else
        {
            DrawColoredPolygon(new[] { new Vector2(-9, 7), new Vector2(-4, 1), new Vector2(-1, 7) }, skin);
            DrawColoredPolygon(new[] { new Vector2(9, 7), new Vector2(4, 1), new Vector2(3, 7) }, skin);
        }
        DrawSetTransform(Vector2.Zero, 0, new Vector2(Face * Size * 1.25f, Size * 0.85f));
        DrawCircle(Vector2.Zero, 8, skin);
        DrawCircle(new Vector2(2, 3), 5 + croak * 2, belly);
        Begin();
        DrawCircle(new Vector2(3, -7), 3, skin);
        DrawCircle(new Vector2(-2, -7), 3, skin);
        DrawCircle(new Vector2(3.5f, -7.5f), 1.6f, new Color(0.1f, 0.1f, 0.1f));
        DrawCircle(new Vector2(-1.5f, -7.5f), 1.6f, new Color(0.1f, 0.1f, 0.1f));
        End();
        DrawHealthBar();
    }
}

/// <summary>Runs at you and clubs you -- or, as a slinger, keeps its distance and lobs rocks.</summary>
public partial class Goblin : Enemy
{
    public bool Slinger;
    private int _state; // 0 chase, 1 windup, 2 recover
    private float _stateT, _throwCd = 1.5f, _gruntT;

    public Goblin() { MaxHp = 26; BodyRadius = 9; ContactDamage = 4; XpValue = 5; }

    protected override void Setup() { DisplayName = Slinger ? "Goblin Slinger" : "Goblin"; _gruntT = G.Range(2, 6); }

    protected override void Think(float dt)
    {
        _stateT += dt; _throwCd -= dt; _gruntT -= dt;
        var v = Velocity;
        if (InWater)
        {
            v = v.MoveToward(new Vector2(Math.Sign(ToP.X) * 50, -60), 300 * dt);
            Velocity = v;
            return;
        }
        bool floor = IsOnFloor();
        float speed = (Slinger ? 95f : 125f) * (Elite ? 1.15f : 1f);
        if (_gruntT <= 0) { _gruntT = G.Range(3, 8); if (Awake && DistP < 500) G.Sfx.Play("goblin", GlobalPosition, -8, 0.2f); }

        if (!Awake || DistP > 460) { v.X = Mathf.MoveToward(v.X, 0, 600 * dt); Velocity = v; ApplyGravity(dt); return; }

        float dx = ToP.X;
        if (_state == 0)
        {
            float want;
            if (Slinger)
            {
                float d = Math.Abs(dx);
                want = d < 130 ? -Math.Sign(dx) : d > 230 ? Math.Sign(dx) : 0;
                Face = Math.Sign(dx) == 0 ? Face : Math.Sign(dx);
                if (_throwCd <= 0 && DistP < 340 && SeesP) { ThrowRock(); _throwCd = Elite ? 1.2f : 2.2f; }
            }
            else
            {
                want = Math.Abs(dx) > 8 ? Math.Sign(dx) : 0;
                if (want != 0) Face = want;
                if (DistP < 34 * Size && Math.Abs(ToP.Y) < 30) { _state = 1; _stateT = 0; want = 0; G.Sfx.Play("goblin", GlobalPosition, -4, 0.2f, 1.2f); }
            }
            v.X = Mathf.MoveToward(v.X, want * speed, 900 * dt);
            if (floor && (IsOnWall() || (ToP.Y < -50 && Math.Abs(dx) < 90)) && want != 0) v.Y = -390;
            if (floor && want != 0 && !G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 30)) && !G.Cave.IsSolid(GlobalPosition + new Vector2(want * 16, 60)) && ToP.Y < 40)
                v.Y = -330; // hop gaps rather than falling in
        }
        else if (_state == 1)
        {
            v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
            if (_stateT > 0.38f)
            {
                _state = 2; _stateT = 0;
                G.Sfx.Play("swing_heavy", GlobalPosition, -4, 0.1f, 0.7f);
                var rel = ToP;
                if (Math.Abs(rel.X) < 40 * Size && Math.Sign(rel.X) != -Face && Math.Abs(rel.Y) < 30 * Size)
                    P.Hurt(12f * G.DepthDmg * (Elite ? 1.4f : 1f), GlobalPosition);
            }
        }
        else
        {
            v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
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
            G.Spawn(new EnemyProjectile { Position = from, Vel = vel.Rotated((k - (n - 1) / 2f) * 0.12f), Grav = g, Damage = 8 * G.DepthDmg, Kind = "rock", Radius = 4 });
        G.Sfx.Play("throw", GlobalPosition, -8, 0.1f, 0.6f);
        _state = 2; _stateT = 0.2f;
    }

    public override void _Draw()
    {
        var skin = Tint(Slinger ? new Color(0.45f, 0.55f, 0.3f) : new Color(0.38f, 0.5f, 0.24f));
        var cloth = Tint(new Color(0.42f, 0.28f, 0.18f));
        bool moving = Math.Abs(Velocity.X) > 15;
        float run = moving ? MathF.Sin(T * 14) : 0;
        Begin();
        DrawLine(new Vector2(0, 4), new Vector2(3 + run * 4, 10), skin, 2.5f);
        DrawLine(new Vector2(0, 4), new Vector2(-3 - run * 4, 10), skin, 2.5f);
        DrawColoredPolygon(new[] { new Vector2(-5, -3), new Vector2(5, -3), new Vector2(6, 6), new Vector2(-6, 6) }, cloth);
        DrawCircle(new Vector2(1, -8), 5.5f, skin);
        DrawColoredPolygon(new[] { new Vector2(-3, -9), new Vector2(-11, -13), new Vector2(-3, -6) }, skin);
        DrawColoredPolygon(new[] { new Vector2(5, -10), new Vector2(11, -14), new Vector2(5, -7) }, skin);
        DrawCircle(new Vector2(3.5f, -8.5f), 1.3f, new Color(1f, 0.9f, 0.2f));
        DrawLine(new Vector2(2, -5), new Vector2(5, -5), new Color(0.15f, 0.1f, 0.05f), 1f);
        // weapon
        float armA = _state == 1 ? -2.4f + MathF.Sin(_stateT * 40) * 0.1f : _state == 2 && !Slinger ? 0.9f : -0.4f;
        var sh = new Vector2(2, -2);
        var hand = sh + Vector2.Right.Rotated(armA) * 7;
        DrawLine(sh, hand, skin, 2.2f);
        if (!Slinger)
        {
            var tip = hand + Vector2.Right.Rotated(armA - 0.3f) * 12;
            DrawLine(hand, tip, new Color(0.45f, 0.3f, 0.15f), 3.5f);
            DrawCircle(tip, 3.2f, new Color(0.5f, 0.35f, 0.18f));
        }
        else DrawCircle(hand, 2.5f, new Color(0.55f, 0.5f, 0.45f));
        End();
        DrawHealthBar();
    }
}

/// <summary>Creeps along the ceiling, drops on a silk thread when you pass beneath, then climbs back. Lands and pounces if its thread is cut.</summary>
public partial class Spider : Enemy
{
    private int _state; // 0 ceiling, 1 drop, 2 hang, 3 climb, 4 ground
    private float _stateT, _anchorY, _pounceCd;

    protected override bool UsesGravity => _state == 4;

    public Spider() { MaxHp = 18; BodyRadius = 8; ContactDamage = 9; XpValue = 4; }

    protected override void Setup()
    {
        DisplayName = "Spider";
        _anchorY = GlobalPosition.Y - 8;
        MotionMode = MotionModeEnum.Floating;
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
                if (Awake && Math.Abs(dx) > 6 && ToP.Y > 0 && DistP < 400)
                {
                    float nx = GlobalPosition.X + Math.Sign(dx) * 70 * dt;
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
                var np = GlobalPosition + new Vector2(0, 330 * dt);
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
                    v.X = Mathf.MoveToward(v.X, Face * 150, 900 * dt);
                    if (_pounceCd <= 0 && DistP < 110 && SeesP)
                    {
                        _pounceCd = 1.4f;
                        v = new Vector2(Math.Sign(ToP.X) * 230, -300);
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

    public override void _Draw()
    {
        if (_state is 1 or 2 or 3 || (_state == 0 && GlobalPosition.Y - _anchorY > 9))
            DrawLine(new Vector2(0, _anchorY - GlobalPosition.Y), Vector2.Zero, new Color(0.9f, 0.9f, 0.95f, 0.6f), 1f);
        var body = Tint(new Color(0.12f, 0.1f, 0.12f));
        bool upside = _state == 0;
        Begin(0, 1, upside ? -1 : 1);
        float walk = MathF.Sin(T * 16);
        for (int k = 0; k < 4; k++)
        {
            float a = -0.9f + k * 0.45f;
            float wig = (k % 2 == 0 ? walk : -walk) * 0.2f;
            var knee = new Vector2(MathF.Cos(a + wig) * 8, -4 + MathF.Sin(a) * 3);
            DrawPolyline(new[] { Vector2.Zero, knee, knee + new Vector2(knee.X * 0.3f, 8) }, body, 1.3f);
            DrawPolyline(new[] { Vector2.Zero, new Vector2(-knee.X, knee.Y), new Vector2(-knee.X * 1.3f, knee.Y + 8) }, body, 1.3f);
        }
        DrawCircle(new Vector2(-3, -1), 6, body);
        DrawCircle(new Vector2(4, 0), 4, body);
        DrawColoredPolygon(new[] { new Vector2(-4, -4), new Vector2(-2, -1), new Vector2(-4, 2), new Vector2(-6, -1) }, new Color(0.85f, 0.1f, 0.1f));
        DrawCircle(new Vector2(6, -1), 1f, new Color(1f, 0.2f, 0.2f));
        DrawCircle(new Vector2(5, 1), 1f, new Color(1f, 0.2f, 0.2f));
        End();
        DrawHealthBar();
    }
}

/// <summary>Slow molten blob: leaves burning puddles, lobs lava globs, and hates water.</summary>
public partial class LavaMonster : Enemy
{
    private float _lobCd = 2f, _puddleT, _windup = -1;

    public LavaMonster() { MaxHp = 40; BodyRadius = 12; ContactDamage = 10; XpValue = 8; KnockResist = 0.3f; }

    protected override void Setup() => DisplayName = "Magma Brute";
    protected override Color BloodColor => new(1f, 0.5f, 0.1f);

    protected override void Think(float dt)
    {
        _lobCd -= dt; _puddleT -= dt;
        var v = Velocity;
        if (InWater)
        {
            // It boils away in water.
            Hp -= 15 * dt;
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
            if (_windup > 0.55f) { Lob(); _windup = -1; }
        }
        else if (Awake && DistP < 420)
        {
            Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
            v.X = Mathf.MoveToward(v.X, Face * 45, 300 * dt);
            if (IsOnFloor() && IsOnWall()) v.Y = -300;
            if (_lobCd <= 0 && DistP < 340 && SeesP) { _windup = 0; _lobCd = Elite ? 2f : 3.2f; G.Sfx.Play("lava", GlobalPosition, -6); }
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
            G.Spawn(new EnemyProjectile { Position = from, Vel = baseV * G.Range(0.8f, 1.15f) + new Vector2((k - (n - 1) / 2f) * 45, 0), Grav = g, Damage = 9 * G.DepthDmg, Kind = "lava", Radius = 5 });
    }

    public override void _Draw()
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(T * 4);
        float glow = _windup >= 0 ? 1 : pulse * 0.6f;
        var crust = Tint(new Color(0.3f, 0.1f, 0.06f));
        var hot = new Color(1f, 0.45f + glow * 0.3f, 0.1f);
        DrawCircle(Vector2.Zero, (BodyRadius + 10) * Size, new Color(1f, 0.4f, 0.05f, 0.1f + glow * 0.08f));
        float wob = MathF.Sin(T * 3) * 1.2f;
        Begin();
        var pts = new Vector2[12];
        for (int k = 0; k < 12; k++)
        {
            float a = k * Mathf.Tau / 12;
            float r = 12 + MathF.Sin(a * 3 + T * 2) * 1.5f;
            pts[k] = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.9f + (MathF.Sin(a) > 0 ? 2 : wob));
        }
        DrawColoredPolygon(pts, Tint(hot));
        DrawColoredPolygon(new[] { new Vector2(-9, -6), new Vector2(-3, -10), new Vector2(0, -4), new Vector2(-6, -1) }, crust);
        DrawColoredPolygon(new[] { new Vector2(2, 2), new Vector2(8, -2), new Vector2(10, 5), new Vector2(4, 8) }, crust);
        DrawColoredPolygon(new[] { new Vector2(-8, 4), new Vector2(-3, 3), new Vector2(-4, 9) }, crust);
        DrawLine(new Vector2(2, -5), new Vector2(8, -4), new Color(1f, 0.95f, 0.5f), 2f);
        DrawLine(new Vector2(-4, -5), new Vector2(0, -4), new Color(1f, 0.95f, 0.5f), 2f);
        End();
        DrawHealthBar();
    }
}

/// <summary>Stone golem: slow, heavy, nearly immovable; telegraphs a ground slam that sends shockwaves both ways.</summary>
public partial class Golem : Enemy
{
    private float _slamCd = 1.5f, _windup = -1, _recover;

    public Golem() { MaxHp = 90; BodyRadius = 15; ContactDamage = 12; XpValue = 14; KnockResist = 0.85f; }

    protected override void Setup() => DisplayName = "Golem";
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
            if (_windup > 0.8f) { Slam(); _windup = -1; _recover = 1.0f; }
        }
        else if (_recover > 0) v.X = Mathf.MoveToward(v.X, 0, 800 * dt);
        else if (Awake && DistP < 460)
        {
            Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
            v.X = Mathf.MoveToward(v.X, Face * 40, 300 * dt);
            if (IsOnFloor() && IsOnWall()) v.Y = -330;
            if (_slamCd <= 0 && DistP < 190 && Math.Abs(ToP.Y) < 70 && IsOnFloor()) { _windup = 0; G.Sfx.Play("goblin", GlobalPosition, -2, 0.1f, 0.4f); }
        }
        else v.X = Mathf.MoveToward(v.X, 0, 600 * dt);
        Velocity = v;
        ApplyGravity(dt);
    }

    private void Slam()
    {
        _slamCd = Elite ? 2.2f : 3.2f;
        G.Sfx.Play("slam", GlobalPosition);
        G.Fx.AddShake(8);
        var foot = GlobalPosition + new Vector2(0, BodyRadius * Size);
        G.Fx.Burst(foot, new Color(0.6f, 0.55f, 0.5f), 20, 200, 3f, 0.5f);
        for (int s = -1; s <= 1; s += 2)
            G.Spawn(new Shockwave { Position = foot + new Vector2(s * 16 * Size, 0), Dir = s, Damage = 14 * G.DepthDmg * (Elite ? 1.3f : 1), Size = Elite ? 1.5f : 1f, Speed = Elite ? 300 : 250 });
        var rel = ToP;
        if (Math.Abs(rel.X) < 30 * Size && Math.Abs(rel.Y) < 30 * Size) P.Hurt(18 * G.DepthDmg, GlobalPosition, 320);
    }

    public override void _Draw()
    {
        var stone = Tint(new Color(0.45f, 0.44f, 0.43f));
        var dark = Tint(new Color(0.3f, 0.29f, 0.3f));
        float step = Math.Abs(Velocity.X) > 5 ? MathF.Sin(T * 6) : 0;
        float raise = _windup >= 0 ? Math.Min(1, _windup / 0.5f) : 0;
        float shake = _windup >= 0.5f ? MathF.Sin(T * 60) * 1f : 0;
        Begin();
        DrawRect(new Rect2(-10 + shake, 6 + step, 7, 9), dark);
        DrawRect(new Rect2(3 + shake, 6 - step, 7, 9), dark);
        DrawColoredPolygon(new[] { new Vector2(-13 + shake, -8), new Vector2(12 + shake, -10), new Vector2(14 + shake, 8), new Vector2(-12 + shake, 9) }, stone);
        DrawRect(new Rect2(-6 + shake, -18, 13, 10), stone);
        var eye = new Color(0.4f, 0.95f, 1f, 0.8f + raise * 0.2f);
        DrawRect(new Rect2(1 + shake, -15, 4, 2.5f), eye);
        DrawLine(new Vector2(-6, -1), new Vector2(4, 3), dark, 1.2f);
        // arms
        float armA = Mathf.Lerp(1.3f, -1.9f, raise);
        var sh = new Vector2(10 + shake, -5);
        var fist = sh + Vector2.Right.Rotated(armA) * 13;
        DrawLine(sh, fist, stone, 6);
        DrawCircle(fist, 5, dark);
        var sh2 = new Vector2(-11 + shake, -5);
        var fist2 = sh2 + Vector2.Right.Rotated(Mathf.Pi - armA) * 13;
        DrawLine(sh2, fist2, stone, 6);
        DrawCircle(fist2, 5, dark);
        End();
        DrawHealthBar();
    }
}
