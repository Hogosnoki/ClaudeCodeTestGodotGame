using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The liveliness layer, over every creature's own animation: breathing and fidgets and glances while it idles,
/// a lean into its stride, a start and a stop that overshoot like weight does, a rear-up when it notices someone, an
/// anticipation (a coil back, a quiver) before an attack, a lunge as it lands, a flinch when it is struck. It writes only
/// whole-body values (lean, stretch, lift, a glance, the glow), so it suits every skeleton; designs keep their own limb work.
/// </summary>
public partial class CreatureModel
{
    private enum Kind { Idle, Move, Wind, Strike, Settle, Hurt, None }

    private float _lifePhase = float.NaN, _lifeT, _kickT, _kickLean, _noticeT = -1f, _fidgetAt, _fidgetT = -1f, _kindT;
    private Kind _kind = Kind.Idle;
    private bool _wasAlert;

    private static Kind Classify(string clip)
    {
        if (string.IsNullOrEmpty(clip)) return Kind.Idle;
        switch (clip)
        {
            case "idle": case "float": case "hang": case "sit": case "roost": case "hold": case "wake": case "croak": return Kind.Idle;
            case "hurt": case "stunned": return Kind.Hurt;
            case "death": case "turn_r2l": case "turn_l2r": return Kind.None;
            case "recover": case "land": case "run_stop": case "drop": return Kind.Settle;
            case "aim": case "rear": case "crouch": case "curl": case "roar": return Kind.Wind;
            case "slam": case "bite": case "swipe": case "strike": case "sting": case "slash": case "pounce": case "dive": case "charge":
            case "tongue": case "tail": case "shove": case "lob": case "puff": case "breath": case "throw": case "burst": case "leap":
            case "heave": case "bash": case "dart": case "rupture": case "hex": case "cast": case "heal": case "flop":
                return Kind.Strike;
        }
        if (clip.EndsWith("windup")) return Kind.Wind;
        return Kind.Move;
    }

    private static float Smooth(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    private void ApplyLife(in AnimInput a)
    {
        var p = Pose;
        float ex = Design.LifeScale, dt = Math.Min(a.Dt, 0.1f);
        if (float.IsNaN(_lifePhase))
        {
            _lifePhase = (GetInstanceId() % 977) / 977f * Mathf.Tau;
            _fidgetAt = 2f + (GetInstanceId() % 53) / 10f;
        }
        _lifeT += dt;
        var kind = Classify(a.Clip);
        if (kind == Kind.None) { p.Lean = p.Spring(901, 0f, dt, 200f, 16f); return; }
        float speed = MathF.Abs(a.Vel.X) + 0.5f * MathF.Abs(a.Vel.Y);
        // idle-looking clips while it is in fact travelling (some designs just loop "idle")
        if (kind == Kind.Idle && speed > 0.25f && a.OnFloor) kind = Kind.Move;
        else if (kind == Kind.Move && speed < 0.1f && a.Clip != null && (a.Clip == "walk" || a.Clip == "run")) kind = Kind.Idle;
        float fs = a.Facing >= 0 ? 1f : -1f;

        // ---- state changes: a start rocks back before it goes, a stop rocks on forward
        if (kind != _kind)
        {
            if (_kind == Kind.Idle && kind == Kind.Move) { _kickT = 0.14f; _kickLean = -5f; }
            else if (_kind == Kind.Move && (kind == Kind.Idle || kind == Kind.Settle)) { _kickT = 0.16f; _kickLean = 6f; }
            _kind = kind; _kindT = 0f;
        }
        _kindT += dt;

        // ---- noticing someone: a rear-up, a pop of the glow
        bool alert = a.Owner is Enemy foe && foe.Alerted;
        if (alert && !_wasAlert && kind is Kind.Idle or Kind.Move) _noticeT = 0f;
        _wasAlert = alert;

        float lean = 0, sx = 1, sy = 1, rx = 0, ry = 0, yaw = 0;
        switch (kind)
        {
            case Kind.Idle:
            {
                float br = MathF.Sin(_lifeT * 2.6f + _lifePhase);
                sy += 0.028f * br; sx -= 0.014f * br; ry += 0.006f * br;
                lean += 1.6f * MathF.Sin(_lifeT * 0.9f + _lifePhase * 1.7f);
                // a slow look about, as if hearing things
                yaw += 14f * MathF.Sin(_lifeT * 0.55f + _lifePhase) * MathF.Sin(_lifeT * 0.23f + _lifePhase * 2.1f);
                if (_fidgetT < 0 && _lifeT > _fidgetAt) { _fidgetT = 0f; }
                break;
            }
            case Kind.Move:
            {
                float s = Math.Clamp(speed * 3f, 0f, 1f);
                lean += (4f + (alert ? 3f : 0f)) * s;
                sy += 0.012f * MathF.Sin(a.Time * 9f + _lifePhase) * s;
                yaw += 3f * MathF.Sin(a.Time * 4.5f + _lifePhase) * s;
                break;
            }
            case Kind.Wind:
            {
                float k = Smooth(a.T);
                lean += -11f * k;
                sy += 0.11f * k; sx -= 0.06f * k;
                ry += 0.02f * k; rx += -fs * 0.05f * k;
                // it gathers itself: a quiver that tightens
                lean += 1.4f * k * MathF.Sin(a.Time * 55f);
                p.Glow *= 1f + 0.5f * k;
                break;
            }
            case Kind.Strike:
            {
                float u = a.T;
                float f = Key(u, (0f, 0.2f), (0.2f, 1f), (0.6f, 0.9f), (1f, 0.25f));
                lean += 17f * f;
                sx += 0.16f * f; sy -= 0.10f * f;
                rx += fs * 0.11f * f;
                p.Glow *= 1f + 0.4f * f;
                break;
            }
            case Kind.Settle:
            {
                float k = 1f - Smooth(a.T);
                sy -= 0.07f * k; sx += 0.05f * k;
                lean += 5f * k;
                break;
            }
            case Kind.Hurt:
            {
                float f = Key(a.T, (0f, 0f), (0.18f, 1f), (1f, 0f));
                lean += -15f * f;
                sx -= 0.1f * f; sy += 0.08f * f;
                rx += -fs * 0.06f * f;
                yaw += 10f * f * MathF.Sin(a.T * 30f);
                break;
            }
        }
        // a fidget: a shrug and a little hop, now and then
        if (_fidgetT >= 0f)
        {
            _fidgetT += dt;
            float u = _fidgetT / 0.55f;
            if (u >= 1f || kind != Kind.Idle) { _fidgetT = -1f; _fidgetAt = _lifeT + 3.5f + 3.5f * (0.5f + 0.5f * MathF.Sin(_lifePhase * 13f + _lifeT)); }
            else
            {
                lean += -5f * MathF.Sin(u * MathF.PI * 2f);
                ry += 0.03f * MathF.Sin(u * MathF.PI);
                sy += 0.04f * MathF.Sin(u * MathF.PI);
            }
        }
        if (_kickT > 0f) { _kickT -= dt; lean += _kickLean * Math.Clamp(_kickT / 0.15f, 0f, 1f); }
        if (_noticeT >= 0f)
        {
            _noticeT += dt;
            float u = _noticeT / 0.55f;
            if (u >= 1f) _noticeT = -1f;
            else
            {
                float bump = MathF.Sin(u * MathF.PI);
                lean += -13f * bump;
                sy += 0.15f * bump; sx -= 0.05f * bump;
                ry += 0.045f * bump;
                p.Glow *= 1f + 1.2f * bump;
            }
        }

        // everything settles through springs, so each change overshoots a little, as weight does
        p.Lean = p.Spring(901, lean * ex, dt, 200f, 16f);
        float jx = p.Spring(902, (sx - 1f) * ex, dt, 220f, 17f), jy = p.Spring(903, (sy - 1f) * ex, dt, 220f, 17f);
        p.Stretch = new Vector3(1f + jx, 1f + jy, 1f + 0.5f * jx);
        float qx = p.Spring(904, rx * ex, dt, 200f, 16f), qy = p.Spring(905, ry * ex, dt, 200f, 16f);
        p.Root += new Vector3(qx, qy, 0f);
        p.Yaw += p.Spring(906, yaw * ex, dt, 80f, 12f);
    }

    private static float Key(float t, params (float t, float v)[] k) => CreatureDesign.Key(t, k);
}
