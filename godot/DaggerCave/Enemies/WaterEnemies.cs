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

    public Fish() { MaxHp = 10; BodyRadius = 6; ContactDamage = 7; XpValue = 2; }

    protected override void Setup()
    {
        DisplayName = "Cave Fish";
        _home = GlobalPosition;
        _wanderA = G.Range(0, Mathf.Tau);
        _col = G.Chance(0.5f) ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.35f, 0.75f, 0.8f);
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
                if (Awake && playerIn && DistP < 320)
                {
                    if (_dartT > 0) { /* keep dashing */ }
                    else if (_dartCd <= 0)
                    {
                        _dartT = 0.4f; _dartCd = G.Range(0.9f, 1.5f) * (Elite ? 0.6f : 1f);
                        v = ToP.Normalized() * (Elite ? 330 : 280);
                    }
                    else v = v.MoveToward(ToP.Normalized() * 30 + new Vector2(0, MathF.Sin(T * 5) * 20), 400 * dt);
                }
                else
                {
                    // Shore ambush: leap at a player standing near the water.
                    var rel = ToP;
                    if (Awake && !playerIn && _leapCd <= 0 && Math.Abs(rel.X) < 190 && P.GlobalPosition.Y > cave.WaterY - 170 && GlobalPosition.Y < cave.WaterY + 90 && rel.Y < 0)
                    {
                        _leapCd = G.Range(3f, 5f);
                        float t = 0.75f;
                        var target = P.GlobalPosition;
                        v = new Vector2((target.X - GlobalPosition.X) / t, (target.Y - GlobalPosition.Y) / t - 0.5f * Grav * t);
                        v.Y = Math.Max(v.Y, -720);
                        G.Sfx.Play("splash", GlobalPosition, -8, 0.2f, 1.3f);
                    }
                    else
                    {
                        _wanderA += G.Range(-2, 2) * dt;
                        var wander = Vector2.Right.Rotated(_wanderA) * 50;
                        if (GlobalPosition.DistanceTo(_home) > 120) wander = (_home - GlobalPosition).Normalized() * 60;
                        v = v.MoveToward(wander, 200 * dt);
                        if (!Awake || playerIn == false)
                        {
                            // drift toward the surface under the player to set up a leap
                            if (Awake && Math.Abs(ToP.X) < 260) v = v.MoveToward(new Vector2(Math.Sign(ToP.X) * 60, -40), 250 * dt);
                        }
                    }
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

    public override void _Draw()
    {
        var c = Tint(_col);
        float wig = MathF.Sin(T * (_state == 0 ? 12 : 25)) * (_state == 2 ? 0.5f : 0.25f);
        float rot = _state == 0 ? 0 : (_state == 2 ? MathF.Sin(T * 20) * 0.6f : Velocity.Angle() * Face);
        if (_state == 1) rot = Mathf.Clamp(Velocity.Y / 600f, -1, 1) * -Face * -1;
        Begin(rot);
        DrawColoredPolygon(new[] { new Vector2(-6, 0), new Vector2(-12, -5 + wig * 8), new Vector2(-12, 5 + wig * 8) }, c.Darkened(0.2f));
        DrawSetTransform(Vector2.Zero, rot, new Vector2(Face * Size * 1.4f, Size * 0.85f));
        DrawCircle(Vector2.Zero, 6, c);
        Begin(rot);
        DrawColoredPolygon(new[] { new Vector2(-3, -4), new Vector2(2, -9), new Vector2(3, -4) }, c.Darkened(0.25f));
        DrawCircle(new Vector2(5, -1.5f), 1.6f, Colors.White);
        DrawCircle(new Vector2(5.4f, -1.5f), 0.9f, Colors.Black);
        DrawLine(new Vector2(8, 1.5f), new Vector2(6, 2.5f), new Color(0.2f, 0.05f, 0.05f), 1f);
        End();
        DrawHealthBar();
    }
}

/// <summary>Stationary spiny urchin on the sea floor; periodically bristles its spikes outward.</summary>
public partial class Urchin : Enemy
{
    private float _cycle;
    private bool _hitThisPulse;

    protected override bool UsesGravity => false;

    public Urchin() { MaxHp = 30; BodyRadius = 10; ContactDamage = 9; XpValue = 5; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Urchin";
        _cycle = G.Range(0, 2.5f);
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

    protected override void Think(float dt)
    {
        float prev = _cycle;
        _cycle += dt * (Elite ? 1.3f : 1f);
        if (_cycle > 3f) { _cycle -= 3f; _hitThisPulse = false; }
        if (prev < 2.1f && _cycle >= 2.1f && DistP < 400) G.Sfx.Play("spike", GlobalPosition, -6);
        float reach = (BodyRadius + SpikeLen()) * Size;
        if (!_hitThisPulse && SpikeLen() > 12 && DistP < reach + 6) { P.Hurt(12 * G.DepthDmg, GlobalPosition); _hitThisPulse = true; }
    }

    public override void _Draw()
    {
        float sl = SpikeLen();
        float tremble = _cycle > 1.6f && _cycle < 2.1f ? MathF.Sin(T * 70) * 0.8f : 0;
        var body = Tint(new Color(0.3f, 0.12f, 0.38f));
        var spike = Tint(new Color(0.55f, 0.35f, 0.65f));
        Begin();
        for (int k = 0; k < 18; k++)
        {
            float a = k * Mathf.Tau / 18 + T * 0.2f;
            var d = Vector2.Right.Rotated(a);
            float l = sl * (k % 2 == 0 ? 1f : 0.75f);
            DrawLine(d * 8, d * (10 + l) + new Vector2(tremble, 0), spike, 1.4f);
        }
        DrawCircle(Vector2.Zero, 10, body);
        for (int k = 0; k < 5; k++) DrawCircle(Vector2.Right.Rotated(k * 1.3f + 0.4f) * 5, 1.3f, new Color(0.9f, 0.5f, 1f, 0.6f + 0.4f * MathF.Sin(T * 3 + k)));
        End();
        DrawHealthBar();
    }
}

/// <summary>
/// Lurks in a burrow in an underwater wall with only its eyes showing (immune), then lunges out
/// along a line at a nearby swimmer, bites, and slowly retracts -- vulnerable while extended.
/// </summary>
public partial class Eel : Enemy
{
    public Vector2 WallNormal = Vector2.Up;
    private Vector2 _home, _target, _head;
    private int _state; // 0 hidden, 1 lunge, 2 hold, 3 retract
    private float _stateT, _cd = 1f;

    protected override bool UsesGravity => false;
    public override bool CanBeHit => _state != 0;
    public override float HitRadius => 8 * Size;

    public Eel() { MaxHp = 24; BodyRadius = 7; ContactDamage = 10; XpValue = 6; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Eel";
        _home = GlobalPosition;
        _head = _home;
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
        ContactActive = false;
        CollisionMask = 0;
    }

    protected override void Think(float dt)
    {
        _stateT += dt; _cd -= dt;
        float maxLen = 170 * (Elite ? 1.4f : 1f);
        switch (_state)
        {
            case 0:
                ContactActive = false;
                if (Awake && _cd <= 0 && P.InWater && P.GlobalPosition.DistanceTo(_home) < maxLen + 20 && G.Cave.LineClear(_home, P.GlobalPosition))
                {
                    _state = 1; _stateT = 0;
                    var d = P.GlobalPosition + P.Velocity * 0.15f - _home;
                    _target = _home + d.Normalized() * Math.Min(d.Length() + 20, maxLen);
                    G.Sfx.Play("eel", _home, -2);
                }
                break;
            case 1:
                ContactActive = true;
                _head = _head.MoveToward(_target, 460 * dt);
                if (_head.DistanceTo(_target) < 2 || _stateT > 0.6f) { _state = 2; _stateT = 0; }
                break;
            case 2:
                if (_stateT > 0.45f) { _state = 3; _stateT = 0; }
                break;
            default:
                _head = _head.MoveToward(_home, 150 * dt);
                if (_head.DistanceTo(_home) < 2) { _state = 0; _stateT = 0; _cd = Elite ? 1.0f : 1.8f; }
                break;
        }
        GlobalPosition = _head;
        if ((_head - _home).X != 0) Face = Math.Sign((_head - _home).X);
    }

    public override void _Draw()
    {
        var homeL = _home - GlobalPosition;
        var body = Tint(new Color(0.25f, 0.35f, 0.22f));
        var belly = Tint(new Color(0.7f, 0.75f, 0.35f));
        // burrow
        DrawCircle(homeL, 9 * Size, new Color(0.03f, 0.03f, 0.05f, 0.9f));
        var seg = -homeL;
        float len = seg.Length();
        if (len > 3)
        {
            var dir = seg / len;
            var perp = new Vector2(-dir.Y, dir.X);
            int n = Math.Max(3, (int)(len / 6));
            var pts = new Vector2[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                pts[k] = homeL + seg * t + perp * MathF.Sin(t * 9 - T * 14) * 5 * MathF.Sin(t * MathF.PI) * Size;
            }
            DrawPolyline(pts, body, 7 * Size);
            DrawPolyline(pts, belly, 2 * Size);
            if (_state is 1 or 2 && G.Chance(0.3f))
                DrawLine(pts[n / 2], pts[n / 2] + G.RandDir() * 8, new Color(0.7f, 0.9f, 1f), 1f);
        }
        var fwd = len > 3 ? seg / len : WallNormal;
        DrawSetTransform(Vector2.Zero, fwd.Angle(), new Vector2(Size, Size));
        float jaw = _state == 1 || _state == 2 ? 0.5f : 0.1f;
        DrawColoredPolygon(new[] { new Vector2(-5, -5), new Vector2(9, -1 - jaw * 4), new Vector2(9, 0), new Vector2(-5, 0) }, body);
        DrawColoredPolygon(new[] { new Vector2(-5, 0), new Vector2(9, 1 + jaw * 4), new Vector2(-5, 5) }, body.Darkened(0.2f));
        DrawCircle(new Vector2(2, -3), 1.4f, new Color(1f, 0.9f, 0.3f));
        End();
        DrawHealthBar();
    }
}
