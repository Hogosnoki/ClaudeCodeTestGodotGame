using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A creature's second attack, in its body: every kind of attack is prefaced by its own wind-up, so the blow has weight before it
/// lands and can be told from any other at a glance. A wind-up is staged the way a thrown punch is: a little counter-move the wrong
/// way, the gather into the coiled pose, a held moment where it trembles and pulls a shade further, then the release, which
/// overshoots, and the recovery. This writes the whole body's part (lean, stretch, where the weight sits, the turn of the body);
/// <see cref="CreatureDesign.Windup"/> adds the limbs'.
/// <list type="bullet">
/// <item>Lunge: sinks low and draws back along the line of the charge, then throws itself flat along it.</item>
/// <item>Dive: climbs and tips its nose up, hangs, then tips over and falls on you.</item>
/// <item>Spit: rears its head back, swelling as it fills; snaps forward as it lets go.</item>
/// <item>Volley: rears and sweeps from side to side, sighting each shot of the fan.</item>
/// <item>Slam: rises to its full height and arches back, hanging there; comes down flat.</item>
/// <item>Burst: sinks and swells in three pulses, each bigger than the last; bursts outward.</item>
/// <item>Spines: shrinks in on itself, bristling; snaps out to full size.</item>
/// <item>Spin: winds its body round the other way, low; unwinds.</item>
/// <item>Pounce: crouches deep, haunches wiggling as it judges the leap; stretches long in the air.</item>
/// <item>Kamikaze: rocks as it lines up, burning hotter; Shatter: swells, shuddering, glowing brighter until it breaks.</item>
/// </list>
/// </summary>
public partial class CreatureModel
{
    private Move2 _windKind;
    private float _windSnapT = 9f;
    private bool _windWas;

    /// <summary>Look development: play this second attack on the model sheet (kind, phase, how far through the wind-up, seconds into the phase).</summary>
    public static (Move2 Kind, int Phase, float Frac, float PhaseT)? WindupTest;

    private void ApplyWindup(in AnimInput a)
    {
        var p = Pose;
        float dt = Math.Min(a.Dt, 0.1f);
        var foe = a.Owner as Enemy;
        var kind = foe?.Move2Kind ?? Move2.None;
        int phase = foe?.Move2Phase ?? 0;
        float frac = foe?.Move2WindFrac ?? 0f, phaseT = foe?.Move2PhaseT ?? 0f;
        if (WindupTest is { } test) (kind, phase, frac, phaseT) = test;
        float coil = 0f, tremble = 0f;
        if (phase == 1)
        {
            float u = frac;
            _windKind = kind;
            coil = Key(u, (0f, 0f), (0.1f, -0.12f), (0.55f, 0.92f), (0.86f, 1f), (1f, 1.1f));
            tremble = Smooth((u - 0.5f) * 2f) * MathF.Sin(a.Time * 58f);
            _windWas = true;
        }
        else if (phase >= 2 && _windWas) { _windWas = false; _windSnapT = 0f; }
        else if (phase == 0) _windWas = false;
        _windSnapT += dt;
        // the release: all there at once, held through the blow, let go as it recovers
        float snap = phase == 2 ? 1f : _windSnapT < 9f && phase == 3 ? 1f - Smooth(phaseT / 0.3f) : 0f;
        // (the coil lets go far faster than it gathers; the snap overshoots a little)
        float c = p.Spring(911, coil, dt, phase == 1 ? 260f : 900f, phase == 1 ? 22f : 44f);
        float s = p.Spring(912, snap, dt, 300f, 20f);
        float fs = a.Facing >= 0 ? 1f : -1f, ex = Math.Max(0.55f, Design.LifeScale);
        float t = tremble * Math.Clamp(c, 0f, 1f);

        float lean = 0, sx = 0, sy = 0, rx = 0, ry = 0, yaw = 0, glow = 0;
        switch (_windKind)
        {
            case Move2.Lunge:
                lean = -16f * c + 22f * s; sx = 0.08f * c + 0.14f * s; sy = -0.12f * c - 0.08f * s;
                rx = -0.06f * c + 0.05f * s; lean += 1.2f * t;
                break;
            case Move2.Dive:
            {
                // (nose up as it climbs; in the last of the wind-up it tips over)
                float tip = Smooth((phase == 1 ? frac : 1f) * 4f - 3f);
                lean = (-26f + 44f * tip) * c + 38f * s; sy = 0.1f * c + 0.12f * s; sx = -0.06f * c - 0.06f * s;
                ry = 0.07f * c * (1f - 0.5f * tip); rx = -0.03f * c;
                break;
            }
            case Move2.Spit:
                lean = -15f * c + 20f * s; sx = 0.07f * c - 0.05f * s; sy = 0.1f * c - 0.04f * s;
                rx = -0.045f * c + 0.05f * s; ry = 0.012f * c; glow = 0.5f * c;
                sx += 0.012f * t; sy += 0.012f * t;
                break;
            case Move2.Volley:
                lean = -12f * c + 16f * s; sy = 0.08f * c; sx = 0.03f * c;
                yaw = 22f * c * MathF.Sin(a.Time * 11f) ; rx = -0.03f * c + 0.03f * s; ry = 0.03f * c; glow = 0.4f * c;
                break;
            case Move2.Slam:
                lean = -20f * c + 14f * s; sy = 0.17f * c - 0.05f * s; sx = -0.08f * c + 0.06f * s;
                ry = 0.045f * c; rx = -0.03f * c; lean += 1.6f * t;
                break;
            case Move2.Burst:
            {
                // three swells, each bigger than the one before
                float u = phase == 1 ? frac : 1f;
                float pulse = (0.35f + 0.65f * u) * (0.5f - 0.5f * MathF.Cos(u * 3f * Mathf.Tau));
                float g = 0.1f * c + 0.13f * pulse * Math.Clamp(c, 0f, 1f);
                sx = g * 1.2f + 0.3f * s; sy = g * 0.7f + 0.2f * s; ry = -0.025f * c + 0.03f * s; lean = 6f * c - 8f * s; glow = 0.9f * pulse + 1.2f * s;
                break;
            }
            case Move2.Spines:
                sx = -0.15f * c + 0.24f * s; sy = -0.13f * c + 0.24f * s; ry = -0.015f * c;
                sx += 0.025f * t; sy -= 0.025f * t; glow = 0.6f * c + 0.8f * s;
                break;
            case Move2.Spin:
                yaw = -75f * c + 40f * s; lean = -7f * c; sy = -0.1f * c; sx = 0.07f * c + 0.12f * s; ry = -0.02f * c;
                yaw += 3f * t;
                break;
            case Move2.Pounce:
            {
                // (the haunches wiggle as it judges the leap)
                float wig = Math.Clamp(c, 0f, 1f) * Smooth(c * 2f - 0.8f) * MathF.Sin(a.Time * 26f);
                lean = -9f * c + 16f * s; sy = -0.24f * c + 0.18f * s; sx = 0.13f * c - 0.1f * s;
                ry = -0.03f * c; rx = -0.03f * c; yaw = 9f * wig;
                break;
            }
            case Move2.Kamikaze:
                lean = 10f * c * MathF.Sin(a.Time * 17f) + 30f * s; sy = 0.08f * c; ry = 0.03f * c; glow = 1.5f * c + 2f * s;
                break;
            case Move2.Shatter:
                sx = 0.2f * c + 0.03f * t; sy = 0.2f * c - 0.03f * t; glow = 2f * c * (0.6f + 0.4f * MathF.Abs(tremble));
                break;
        }
        p.Lean += lean * ex;
        p.Stretch = new Vector3(p.Stretch.X * (1f + sx * ex), p.Stretch.Y * (1f + sy * ex), p.Stretch.Z * (1f + 0.5f * (sx + Math.Max(0f, sy)) * ex));
        p.Root += new Vector3(fs * rx, ry, 0f);
        p.Yaw += yaw;
        p.Glow *= 1f + glow;
        Design.Windup(p, new WindupPose { Kind = _windKind, Coil = c, Snap = s, Tremble = t, Time = a.Time });
    }
}
