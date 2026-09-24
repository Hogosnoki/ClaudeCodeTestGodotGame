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

    public CavernColossus() { MaxHp = 650; BodyRadius = 30; ContactDamage = 16; XpValue = 120; KnockResist = 1f; IsBoss = true; }

    public void Init(Room room) => _room = room;

    protected override void Setup()
    {
        DisplayName = "Cavern Colossus";
        Awake = true;
    }

    protected override Color BloodColor => new(0.65f, 0.6f, 0.55f);

    public override float Hurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (_s == S.Stunned) dmg *= 1.5f;
        if (_s == S.Intro) return 0;
        float d = base.Hurt(dmg, knock, hitPos);
        if (!_phase2 && Hp < MaxHp * 0.5f && !Dead)
        {
            _phase2 = true;
            G.Sfx.Play("roar", GlobalPosition, 2, 0, 0.85f);
            G.Fx.AddShake(10);
            G.Fx.Text(GlobalPosition + new Vector2(0, -50), "ENRAGED", new Color(1f, 0.4f, 0.2f), 16, 1.5f);
        }
        return d;
    }

    private float Speed => _phase2 ? 1.35f : 1f;

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
                Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
                v.X = Mathf.MoveToward(v.X, Face * 55 * Speed, 400 * dt);
                _next -= dt;
                if (_next <= 0) PickAttack();
                break;
            case S.LeapCrouch:
                v.X = Mathf.MoveToward(v.X, 0, 1200 * dt);
                if (_t > 0.55f / Speed)
                {
                    float air = 0.85f;
                    float tx = Mathf.Clamp(P.GlobalPosition.X, _room.Center.X - _room.RxPx + 50, _room.Center.X + _room.RxPx - 50);
                    v = new Vector2((tx - GlobalPosition.X) / air, -0.5f * Grav * air);
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
                        G.Spawn(new Shockwave { Position = foot + new Vector2(s * 34, 0), Dir = s, Damage = 16 * G.DepthDmg, Size = 1.6f, Speed = 300 * Speed, Life = 2f });
                    if (DistP < 55) P.Hurt(22 * G.DepthDmg, GlobalPosition, 350);
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
                            G.Spawn(new FallingRock { Position = ce + new Vector2(0, 14), Damage = 14 * G.DepthDmg });
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
                v.X = Face * 400 * Speed;
                if (G.Chance(0.5f)) G.Fx.Burst(GlobalPosition + new Vector2(-Face * 20, BodyRadius), new Color(0.6f, 0.55f, 0.5f, 0.8f), 1, 80, 3f, 0.4f, 200);
                if (IsOnWall() || _t > 2.2f)
                {
                    G.Sfx.Play("slam", GlobalPosition, 2);
                    G.Fx.AddShake(14);
                    for (int k = 0; k < 3; k++)
                    {
                        float x = GlobalPosition.X - Face * G.Range(40, 200);
                        if (G.Cave.FindCeiling(new Vector2(x, GlobalPosition.Y - 20), 400, out var ce))
                            G.Spawn(new FallingRock { Position = ce + new Vector2(0, 14), Damage = 12 * G.DepthDmg });
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
        _next = G.Range(0.8f, 1.6f) / Speed;
    }

    private void PickAttack()
    {
        int a;
        do a = G.RangeI(0, 2); while (a == _lastAttack && G.Chance(0.7f));
        _lastAttack = a;
        Face = Math.Sign(ToP.X) == 0 ? Face : Math.Sign(ToP.X);
        switch (a)
        {
            case 0: Go(S.LeapCrouch); break;
            case 1: Go(S.Roar); G.Sfx.Play("roar", GlobalPosition, 0); G.Fx.AddShake(6); break;
            default: Go(S.ChargeWindup); break;
        }
    }

    public override void _Draw()
    {
        var plate = Tint(new Color(0.42f, 0.38f, 0.36f));
        var dark = Tint(new Color(0.24f, 0.21f, 0.22f));
        var core = _phase2 ? new Color(1f, 0.35f, 0.15f) : new Color(0.4f, 0.9f, 1f);
        float pulse = 0.6f + 0.4f * MathF.Sin(T * (_phase2 ? 9 : 4));
        float crouch = _s == S.LeapCrouch ? Math.Min(1, _t * 3) * 6 : 0;
        float headDown = _s is S.ChargeWindup or S.Charge ? 1 : 0;
        float stunWobble = _s == S.Stunned ? MathF.Sin(T * 10) * 0.08f : 0;
        float walk = _s == S.Walk || _s == S.Charge ? MathF.Sin(T * (_s == S.Charge ? 20 : 7)) : 0;

        DrawCircle(new Vector2(0, 0), 50, new Color(core, 0.06f * pulse));
        Begin(stunWobble);
        // legs
        for (int k = 0; k < 3; k++)
        {
            float x = -18 + k * 16;
            float lift = (k % 2 == 0 ? walk : -walk) * 4;
            DrawLine(new Vector2(x, 10 + crouch), new Vector2(x - 8, 30 - Math.Max(0, lift)), dark, 6);
        }
        // body
        var bodyPts = new[]
        {
            new Vector2(-34, 14 + crouch), new Vector2(-30, -10 + crouch), new Vector2(-14, -26 + crouch + headDown * 6),
            new Vector2(10, -28 + crouch + headDown * 8), new Vector2(30, -12 + crouch + headDown * 10), new Vector2(34, 14 + crouch),
        };
        DrawColoredPolygon(bodyPts, plate);
        DrawPolyline(new[] { bodyPts[0], bodyPts[1], bodyPts[2], bodyPts[3], bodyPts[4], bodyPts[5] }, dark, 2);
        DrawLine(new Vector2(-20, -18 + crouch), new Vector2(-8, 8 + crouch), dark, 2);
        DrawLine(new Vector2(6, -22 + crouch), new Vector2(14, 6 + crouch), dark, 2);
        // core
        DrawCircle(new Vector2(-2, 0 + crouch), 8, new Color(core, 0.35f));
        DrawCircle(new Vector2(-2, 0 + crouch), 5 * pulse + 1, core);
        // head
        var head = new Vector2(30, -8 + crouch + headDown * 14);
        DrawColoredPolygon(new[] { head + new Vector2(-8, -10), head + new Vector2(12, -6), head + new Vector2(14, 6), head + new Vector2(-6, 8) }, plate);
        DrawRect(new Rect2(head + new Vector2(4, -4), new Vector2(6, 3)), _s == S.Stunned ? new Color(1, 1, 0.4f) : core);
        // horns
        DrawColoredPolygon(new[] { head + new Vector2(-4, -9), head + new Vector2(6, -22), head + new Vector2(4, -8) }, dark);
        // claws
        float clawA = _s == S.Roar ? -1.2f : _s == S.LeapCrouch ? 0.8f : 0.3f + walk * 0.1f;
        var sh = new Vector2(20, 2 + crouch);
        var claw = sh + Vector2.Right.Rotated(clawA) * 20;
        DrawLine(sh, claw, dark, 7);
        DrawColoredPolygon(new[] { claw + new Vector2(-4, -6), claw + new Vector2(12, -4), claw + new Vector2(2, 2) }, plate);
        DrawColoredPolygon(new[] { claw + new Vector2(-4, 6), claw + new Vector2(12, 6), claw + new Vector2(2, 0) }, plate);
        End();
    }
}
