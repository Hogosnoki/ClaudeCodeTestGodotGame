using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The heroes' motion. Legs are solved, not keyframed: every foot stays planted while it carries the
/// body (it travels back under the hips exactly as far as the hero covers, at any speed) and then
/// swings through an arc to land ahead, so a run never skates, and a sprint gets its long flight
/// phases. What a hero's body does around that (lean, arm carriage, stride, knee lift, the way it
/// cuts and casts) comes from a style of its own, so the five don't move alike.
///
/// Swings follow the gameplay swing exactly: the wind-up, the sweep and the follow-through last as long
/// as the swing's own timers say, so the blade crosses the aim while the hit lands, and the light
/// trailing it is read off the blade itself (<see cref="SampleBlade"/>).
/// </summary>
public sealed partial class HeroDesign
{
    // ================================================================== the pose

    private struct Body
    {
        public float Lean, Twist, HipTwist, Bank, HeadPitch, HeadYaw, Roll;
        /// <summary>The whole body pitched forward from the feet (degrees): the pelvis and everything above it tip with it, and the legs are
        /// turned back against it so the feet keep their places. Lean bends the spine on top of it.</summary>
        public float Pitch;
        public Vector3 Root;
        public float SR, AR, ER, WR, WRy;
        public float SL, AL, EL, WL, WLy;
        /// <summary>Thigh and knee bends, and the foot's pitch in the world (toe up positive), right leg then left.</summary>
        public float HR, KR, FR, HRx, HL, KL, FL, HLx;
    }

    private static Body Lerp(Body a, Body b, float t)
    {
        if (t <= 0f) return a;
        if (t >= 1f) return b;
        Body r;
        r.Lean = Mathf.Lerp(a.Lean, b.Lean, t); r.Twist = Mathf.Lerp(a.Twist, b.Twist, t); r.HipTwist = Mathf.Lerp(a.HipTwist, b.HipTwist, t);
        r.Bank = Mathf.Lerp(a.Bank, b.Bank, t);
        r.HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t); r.HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t); r.Roll = Mathf.Lerp(a.Roll, b.Roll, t); r.Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t);
        r.Root = a.Root.Lerp(b.Root, t);
        r.SR = Mathf.Lerp(a.SR, b.SR, t); r.AR = Mathf.Lerp(a.AR, b.AR, t); r.ER = Mathf.Lerp(a.ER, b.ER, t); r.WR = Mathf.Lerp(a.WR, b.WR, t); r.WRy = Mathf.Lerp(a.WRy, b.WRy, t);
        r.SL = Mathf.Lerp(a.SL, b.SL, t); r.AL = Mathf.Lerp(a.AL, b.AL, t); r.EL = Mathf.Lerp(a.EL, b.EL, t); r.WL = Mathf.Lerp(a.WL, b.WL, t); r.WLy = Mathf.Lerp(a.WLy, b.WLy, t);
        r.HR = Mathf.Lerp(a.HR, b.HR, t); r.KR = Mathf.Lerp(a.KR, b.KR, t); r.FR = Mathf.Lerp(a.FR, b.FR, t); r.HRx = Mathf.Lerp(a.HRx, b.HRx, t);
        r.HL = Mathf.Lerp(a.HL, b.HL, t); r.KL = Mathf.Lerp(a.KL, b.KL, t); r.FL = Mathf.Lerp(a.FL, b.FL, t); r.HLx = Mathf.Lerp(a.HLx, b.HLx, t);
        return r;
    }

    /// <summary>How much the joints' ranges are exaggerated (the heroes are small on screen).</summary>
    private const float Ex = 1.22f;

    // ================================================================== styles

    /// <summary>How one hero runs.</summary>
    private struct Style
    {
        /// <summary>Metres a planted foot travels back under the hips (the same at every speed).</summary>
        public float Stance;
        /// <summary>Metres from one footfall to the next of the same foot, at a sprint.</summary>
        public float Stride;
        /// <summary>Hip height above the floor in a run (standing is 0.8), metres.</summary>
        public float Hip;
        /// <summary>Metres of up and down.</summary>
        public float Bounce;
        /// <summary>How high the swinging ankle lifts off the ground, metres.</summary>
        public float Clear;
        /// <summary>Forward lean of the torso at a sprint, degrees.</summary>
        public float Lean;
        /// <summary>Pelvis yaw and the opposing turn of the shoulders, degrees.</summary>
        public float HipTwist, Counter;
        /// <summary>The free arm's swing (each way), its middle, and its elbow (middle, swing).</summary>
        public float FreeAmp, FreeMid, FreeElbow, FreeElbowAmp;
        /// <summary>The arm that carries the weapon: the same, and the weapon's angle from hanging (degrees, forward positive).</summary>
        public float HoldAmp, HoldMid, HoldElbow, HoldElbowAmp, HoldAbs;
        /// <summary>Which way the head goes: forward of the torso's lean (negative) or up.</summary>
        public float HeadUp;
        /// <summary>How hard the feet land (the hips dip further).</summary>
        public float Weight;
    }

    private static Style StyleFor(HeroKind kind) => kind switch
    {
        // the Swordsman (after Marth): tall and light, long gliding strides, the free arm pumping, the sword arm swept back
        // nearly straight with the blade trailing low behind him
        HeroKind.Swordsman => new Style
        {
            Stance = 0.78f, Stride = 4.7f, Hip = 0.79f, Bounce = 0.04f, Clear = 0.3f, Lean = 18f, HipTwist = 9f, Counter = 13f,
            FreeAmp = 48f, FreeMid = 10f, FreeElbow = 84f, FreeElbowAmp = 18f,
            HoldAmp = 6f, HoldMid = -46f, HoldElbow = 12f, HoldElbowAmp = 4f, HoldAbs = -62f, HeadUp = -2f, Weight = 0.008f,
        },
        // the Warden: short, heavy strides, shield tucked in front, every footfall felt
        HeroKind.Warden => new Style
        {
            Stance = 0.66f, Stride = 3.3f, Hip = 0.78f, Bounce = 0.03f, Clear = 0.18f, Lean = 14f, HipTwist = 6f, Counter = 10f,
            FreeAmp = 5f, FreeMid = 42f, FreeElbow = 82f, FreeElbowAmp = 4f,
            HoldAmp = 32f, HoldMid = 14f, HoldElbow = 90f, HoldElbowAmp = 10f, HoldAbs = 62f, HeadUp = 3f, Weight = 0.03f,
        },
        // the Vitalist: low and gliding, quick short steps, the staff held close, the free hand trailing
        HeroKind.Vitalist => new Style
        {
            Stance = 0.68f, Stride = 3.6f, Hip = 0.76f, Bounce = 0.03f, Clear = 0.18f, Lean = 18f, HipTwist = 8f, Counter = 14f,
            FreeAmp = 46f, FreeMid = -2f, FreeElbow = 68f, FreeElbowAmp = 14f,
            HoldAmp = 12f, HoldMid = 26f, HoldElbow = 72f, HoldElbowAmp = 6f, HoldAbs = 40f, HeadUp = -2f, Weight = 0.006f,
        },
        // the Elementalist: upright and springing, long floating lopes, the free hand flowing, the staff upright
        HeroKind.Elementalist => new Style
        {
            Stance = 0.72f, Stride = 4.2f, Hip = 0.8f, Bounce = 0.055f, Clear = 0.26f, Lean = 14f, HipTwist = 7f, Counter = 13f,
            FreeAmp = 42f, FreeMid = 8f, FreeElbow = 62f, FreeElbowAmp = 22f,
            HoldAmp = 10f, HoldMid = 20f, HoldElbow = 76f, HoldElbowAmp = 5f, HoldAbs = 46f, HeadUp = 6f, Weight = 0.004f,
        },
        // the Aegis: calm and upright, measured steps, the staff carried close and the free hand open
        HeroKind.Aegis => new Style
        {
            Stance = 0.7f, Stride = 3.9f, Hip = 0.78f, Bounce = 0.04f, Clear = 0.2f, Lean = 15f, HipTwist = 7f, Counter = 12f,
            FreeAmp = 40f, FreeMid = 4f, FreeElbow = 66f, FreeElbowAmp = 16f,
            HoldAmp = 11f, HoldMid = 22f, HoldElbow = 74f, HoldElbowAmp = 5f, HoldAbs = 44f, HeadUp = 2f, Weight = 0.007f,
        },
        // the Automaton: wide-set, stiff and heavy, short clanking steps, every footfall a thud, the cleaver carried low
        HeroKind.Automaton => new Style
        {
            Stance = 0.82f, Stride = 3.0f, Hip = 0.8f, Bounce = 0.02f, Clear = 0.14f, Lean = 10f, HipTwist = 3f, Counter = 5f,
            FreeAmp = 22f, FreeMid = 10f, FreeElbow = 70f, FreeElbowAmp = 6f,
            HoldAmp = 16f, HoldMid = 14f, HoldElbow = 78f, HoldElbowAmp = 6f, HoldAbs = 100f, HeadUp = 2f, Weight = 0.035f,
        },
        // the Shape Shifter: light, quick, never quite settled, the staff swung like a blade
        HeroKind.ShapeShifter => new Style
        {
            Stance = 0.74f, Stride = 4.3f, Hip = 0.77f, Bounce = 0.045f, Clear = 0.26f, Lean = 16f, HipTwist = 8f, Counter = 14f,
            FreeAmp = 46f, FreeMid = 6f, FreeElbow = 84f, FreeElbowAmp = 16f,
            HoldAmp = 22f, HoldMid = 18f, HoldElbow = 84f, HoldElbowAmp = 10f, HoldAbs = 110f, HeadUp = 0f, Weight = 0.01f,
        },
        // the Rogue (after Sheik): low and pitched hard forward, the longest strides, heels flicking up, both arms swept back
        // behind with the daggers trailing, the head up and eyes ahead
        _ => new Style
        {
            Stance = 0.86f, Stride = 5.0f, Hip = 0.7f, Bounce = 0.04f, Clear = 0.3f, Lean = 38f, HipTwist = 10f, Counter = 9f,
            FreeAmp = 8f, FreeMid = -52f, FreeElbow = 22f, FreeElbowAmp = 6f,
            HoldAmp = 8f, HoldMid = -52f, HoldElbow = 22f, HoldElbowAmp = 6f, HoldAbs = -40f, HeadUp = -14f, Weight = 0.008f,
        },
    };

    private readonly Style _st;
    /// <summary>Test aid: log the pose numbers of frames posed without moving on (the model sheet).</summary>
    public static bool DebugLog;

    /// <summary>Attack clips cross-fade quickly (their own wind-up blends in from the pose before it).</summary>
    public override float BlendSeconds(string clip) => clip.StartsWith("slash_") || clip == "heave" ? 0.03f : 0.09f;

    // ================================================================== the leg solver

    // (rest-space lengths: hip to knee, knee to ankle, ankle to the sole, heel and toe either side of the ankle)
    // (after the legs are lengthened: see Proportion)
    private static readonly float L1 = new Vector2(0.01f, 0.39f * LegK).Length(), L2 = new Vector2(0.02f, 0.75f * LegK - 0.39f * LegK).Length();
    private const float AnkleH = 0.071f, HeelBack = 0.066f, ToeFwd = 0.161f;
    /// <summary>The hip joint's height above the floor when the root is at rest.</summary>
    private const float HipRest = 0.81f + SoleDrop;
    /// <summary>The hip heights below were set for the old, shorter legs: this scales them to the new.</summary>
    private const float HipK = HipRest / 0.81f;

    /// <summary>
    /// Two-bone solve in the side plane: the thigh's bend (forward positive) and the knee's bend for an
    /// ankle at (<paramref name="ax"/> forward, <paramref name="ay"/> up) from the hip joint. A target
    /// out of reach is clamped to a straight leg (the foot lifts a little).
    /// </summary>
    private static void LegIK(float ax, float ay, out float hip, out float knee)
    {
        float d = MathF.Sqrt(ax * ax + ay * ay);
        d = Math.Clamp(d, Math.Abs(L1 - L2) + 0.02f, (L1 + L2) * 0.998f);
        float interior = MathF.Acos(Math.Clamp((L1 * L1 + L2 * L2 - d * d) / (2f * L1 * L2), -1f, 1f));
        knee = 180f - Mathf.RadToDeg(interior);
        float toward = MathF.Atan2(ax, -ay);
        float alpha = MathF.Acos(Math.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f));
        hip = Mathf.RadToDeg(toward + alpha);
    }

    /// <summary>Where the ankle sits (x forward, y up from the ground line) with the sole touching the ground at
    /// <paramref name="contactX"/>, the contact a share <paramref name="roll"/> of the way from heel to toe and the foot pitched.</summary>
    private static Vector2 AnkleFrom(float contactX, float groundY, float roll, float pitchDeg)
    {
        float psi = Mathf.DegToRad(pitchDeg), cs = MathF.Cos(psi), sn = MathF.Sin(psi);
        float rx = Mathf.Lerp(-HeelBack, ToeFwd, roll), ry = -AnkleH;
        return new Vector2(contactX - (rx * cs - ry * sn), groundY - (rx * sn + ry * cs));
    }

    /// <summary>One leg's pose at its own phase <paramref name="q"/> (0 = the heel touches down).</summary>
    private void Leg(float q, float sf, float travel, float hipH, out float hip, out float knee, out float foot)
    {
        var st = _st;
        float reach = 0.47f;
        float ground = -hipH;
        if (q < sf)
        {
            // carrying the body: the contact point rolls heel to toe while it moves back under the hips
            float u = q / sf;
            float cx = travel * (reach - u);
            float roll = W3.SmoothStep(0.1f, 0.9f, u);
            float pitch = Key(u, (0f, 13f), (0.3f, 1f), (0.62f, -3f), (1f, -40f));
            var ank = AnkleFrom(cx, ground, roll, pitch);
            LegIK(ank.X, ank.Y, out hip, out knee);
            foot = pitch;
            return;
        }
        float v = (q - sf) / (1f - sf);
        // the swing: off the toe, heel kicked up, knee driven through, the foot reaching ahead and pawing back
        var a0 = AnkleFrom(travel * (reach - 1f), 0f, 1f, -40f);
        var a1 = AnkleFrom(travel * reach, 0f, 0f, 13f);
        float e = W3.Smooth01(v);
        const float paw = 0.09f;
        float x = Mathf.Lerp(a0.X, a1.X + paw, e) - paw * W3.SmoothStep(0.8f, 1f, v);
        float lift = st.Clear * MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(v, 0.7f)), 1.25f);
        float y = Mathf.Lerp(a0.Y, a1.Y, v) + lift;
        LegIK(x, ground + y, out hip, out knee);
        foot = Key(v, (0f, -40f), (0.3f, -34f), (0.65f, -14f), (1f, 13f));
    }

    // ================================================================== base movements

    /// <summary>
    /// Both feet planted flat on the floor, the right <paramref name="right"/> metres ahead of the hips (behind if negative) and
    /// the left <paramref name="left"/>, the hips <paramref name="hipH"/> high: the stance is solved, so however the weight
    /// shifts over them (Root.X) the feet neither float nor sink nor slide.
    /// </summary>
    private static void Stance(ref Body o, float right, float left, float hipH, float heel = 0f)
    {
        // (with the heels up, <paramref name="heel"/> degrees, the weight rides on the balls of the feet)
        float roll = heel > 0f ? 0.88f : 0.45f;
        var ar = AnkleFrom(right - o.Root.X, -hipH, roll, -heel);
        LegIK(ar.X, ar.Y, out o.HR, out o.KR);
        var al = AnkleFrom(left - o.Root.X, -hipH, roll, -heel);
        LegIK(al.X, al.Y, out o.HL, out o.KL);
        o.FR = o.FL = -heel;
        o.Root.Y = hipH - HipRest;
    }

    /// <summary>
    /// Standing ready: never at attention, always in a stance, the feet set wide and staggered, knees soft, the chest up,
    /// the chin lifted and the weapon held where it can strike from. The breath lifts the chest and squares the shoulders,
    /// and the weight drifts slowly between the feet, so a hero standing still is never a statue.
    /// </summary>
    private Body Idle(float time)
    {
        float b = MathF.Sin(time * 1.7f), w = MathF.Sin(time * 0.53f + 0.8f);
        // (an automaton doesn't breathe: a slow hum through the frame)
        float br = _automaton ? 0.35f : _swordsman ? 1.5f : 1f;
        var o = new Body { HRx = 7, HLx = 8 };
        float right, left, hipH;
        if (_warden)
        {
            // squared up behind the shield, shield foot forward, the sword cocked back over the shoulder ready to come down
            o.Lean = 7; o.HeadPitch = -4; o.Twist = -6;
            o.SL = 52; o.EL = 86; o.AL = 6; o.WL = -(o.SL + o.EL);
            o.SR = 6; o.AR = 24; o.ER = 106; o.WR = 176 - (o.SR + o.ER);
            right = -0.21f; left = 0.2f; hipH = 0.735f; o.HRx = 9; o.HLx = 10;
        }
        else if (_rogue)
        {
            // after Sheik: a ninja's crouch, very low and wide, front foot forward, pitched toward the prey with the eyes up;
            // the front hand raised open by the face, dagger up, the rear hand low behind the hip with its dagger trailing
            o.Lean = 20; o.HeadPitch = -16; o.Twist = 12;
            o.SL = 74; o.AL = 12; o.EL = 76; o.WL = -10;
            o.SR = -30; o.AR = 22; o.ER = 30; o.WR = -45;
            right = -0.25f; left = 0.24f; hipH = 0.64f; o.HRx = 10; o.HLx = 10;
        }
        else if (_caster)
        {
            // tall, chest out and chin up, the staff planted at the side and the free hand holding its power ready
            o.Lean = 1; o.HeadPitch = -7;
            o.SR = 22; o.ER = 72; o.AR = 12;
            if (_elementalist) { o.SL = 48; o.EL = 86; o.AL = 20; o.WL = -26; }
            else if (_vitalist) { o.SL = 28; o.EL = 44; o.AL = 22; o.WL = -22; }
            else { o.SL = 20; o.EL = 52; o.AL = 24; o.WL = -12; }
            right = 0.16f; left = -0.15f; hipH = 0.775f;
        }
        else if (_automaton)
        {
            // broad and planted, arms held off its great frame, the cleaver low and forward
            o.Lean = 4; o.HeadPitch = -3;
            o.SR = 20; o.AR = 20; o.ER = 48; o.WR = 112 - (o.SR + o.ER);
            o.SL = 12; o.AL = 22; o.EL = 42;
            right = 0.2f; left = -0.21f; hipH = 0.75f; o.HRx = 11; o.HLx = 11;
        }
        else if (_shifter)
        {
            // loose and low, never quite still, the staff held out like a blade and the free hand clawed
            o.Lean = 8; o.HeadPitch = -8; o.Twist = 4;
            o.SR = 36; o.AR = 14; o.ER = 58; o.WR = 128 - (o.SR + o.ER);
            o.SL = 30; o.AL = 16; o.EL = 72; o.WL = -16;
            right = 0.19f; left = -0.18f; hipH = 0.745f;
        }
        else
        {
            // the Swordsman, after Marth: a fencer's poise, tall and side-on, chest proud and chin up, the sword held low and
            // forward with its point toward the floor ahead, the off hand drawn back by the hip, light on his feet
            o.Lean = -1; o.HeadPitch = -7; o.Twist = 10;
            o.SR = 26; o.AR = 16; o.ER = 26; o.WR = 46 - (o.SR + o.ER);
            o.SL = -18; o.AL = 20; o.EL = 26; o.WL = 10;
            right = 0.2f; left = -0.2f; hipH = 0.77f; o.HRx = 6; o.HLx = 8;
        }
        // the breath: the chest lifts and the shoulders square on the in-breath; the weight drifts between the feet
        o.Lean -= 1.4f * b * br; o.HeadPitch += 0.8f * b * br;
        o.SR += 1.5f * b * br; o.SL -= 1.5f * b * br; o.AR += 1.2f * b * br; o.AL += 1.2f * b * br;
        if (!_caster) o.WR -= 1.5f * b * br;
        o.Root.X = 0.014f * w;
        o.HipTwist = -o.Twist * 0.5f;
        o.HeadYaw = -o.Twist * 0.5f;
        // (the Rogue never settles: a springy bounce on the balls of the feet, the knees pumping)
        if (_rogue)
        {
            float spring = MathF.Abs(MathF.Sin(time * 4.2f));
            hipH += 0.018f * spring - 0.009f; o.Lean -= 2f * spring; o.SL += 3f * spring;
        }
        Stance(ref o, right, left, hipH * HipK + 0.004f * b * br, _rogue ? 14f : 0f);
        return o;
    }

    /// <summary>
    /// Sitting on a log by the camp fire: thighs level, shins down, forearms on the knees and
    /// leaning in toward the warmth, breathing slowly (the hips drop a thigh's length so the
    /// feet stay on the ground).
    /// </summary>
    private Body Sit(float time)
    {
        float b = MathF.Sin(time * 1.3f);
        var o = new Body
        {
            Lean = 14 + b * 1.5f, HeadPitch = 8 - b * 1.2f, Root = new Vector3(-0.02f, -0.36f, 0),
            SR = 38 + b, ER = 62, WR = 20, AR = 12,
            SL = 34 - b, EL = 66, AL = 12,
            HR = 84, KR = 90, HRx = 9, HL = 80, KL = 86, HLx = 9,
        };
        if (_warden) { o.SL = 22; o.EL = 70; o.AL = 14; o.WL = -(o.SL + o.EL) + 20; }
        if (_caster) { o.SR = 30 + b; o.ER = 48; }
        return o;
    }

    /// <summary>Metres from one footfall to the next of the same foot at this speed (short at a jog, the style's stride at a sprint).</summary>
    private float StrideAt(float speed) => Mathf.Lerp(_st.Stance / 0.52f, _st.Stride, W3.SmoothStep(1f, 9f, speed));

    /// <summary>
    /// A run at this phase and speed: planted, solved legs; pelvis and shoulders turning against each
    /// other; the arms pumping opposite the legs in the style's carriage; the torso pitched into it.
    /// </summary>
    private Body Run(float phase, float speed, CreaturePose p, in AnimInput a)
    {
        var st = _st;
        float vn = W3.SmoothStep(1f, 9f, speed);
        float stride = StrideAt(speed);
        float sf = Math.Clamp(st.Stance / stride, 0.17f, 0.52f);
        float flight = Math.Clamp((0.5f - sf) / 0.26f, 0f, 1f);
        float tau = Mathf.Tau;
        // the body's height: compressed while a foot carries it, rising through the flight between
        float mid = MathF.Cos(2f * tau * (phase - sf * 0.5f));
        float hipH = Mathf.Lerp(0.775f, st.Hip, Math.Min(1f, 0.35f + vn)) * HipK - st.Bounce * Ex * flight * mid + 0.02f * (1f - flight) * mid - st.Weight * flight * (0.5f + 0.5f * mid);
        var o = new Body { Root = new Vector3(0, hipH - HipRest, 0), HRx = 4f, HLx = 4f };
        // (the foot goes back exactly as far as the body goes in the time it's down)
        float travel = sf * stride;
        Leg(phase, sf, travel, hipH, out o.HR, out o.KR, out o.FR);
        Leg(Mathf.PosMod(phase + 0.5f, 1f), sf, travel, hipH, out o.HL, out o.KL, out o.FL);
        // the arms swing against the legs: the right forward as the left foot lands (phase 0.5)
        float sw = -MathF.Cos(tau * (phase + 0.03f));
        o.SR = st.HoldMid + st.HoldAmp * Ex * sw;
        o.ER = st.HoldElbow + st.HoldElbowAmp * sw;
        // (the Swordsman's trailing blade is carried out from the body, clear of his legs and cape)
        o.AR = _swordsman ? 20f : _rogue ? 16f : 10f;
        o.SL = st.FreeMid - st.FreeAmp * Ex * sw;
        o.EL = st.FreeElbow - st.FreeElbowAmp * sw;
        o.AL = _rogue ? 16f : 10f;
        // the torso: pitched into it, riding the stride, shoulders turning against the hips
        float lean = Mathf.Lerp(7f, st.Lean * (0.85f + 0.15f * Ex), vn);
        // the whole body tips forward from the feet, as a runner's does: the weight carried ahead of the planted foot so each push
        // against the ground drives it on, not up (a little at a jog, more at a sprint, most of all for the low sprinters)
        o.Pitch = Mathf.Lerp(5f, Math.Min(19f, 14f + 0.15f * st.Lean), vn);
        float turn = MathF.Cos(tau * (phase - 0.95f));
        o.Lean = lean + 2.2f * flight * -mid;
        o.HipTwist = st.HipTwist * Ex * turn * (0.4f + 0.6f * vn);
        o.Twist = -st.Counter * Ex * turn * (0.4f + 0.6f * vn);
        // (the head stays up, eyes on what's ahead, however far the body pitches into it)
        o.HeadPitch = -lean * 0.55f - o.Pitch * 0.6f + st.HeadUp + 1.5f * mid;
        o.HeadYaw = -o.Twist * 0.55f;
        // the shield arm is carried, not swung
        if (_warden)
        {
            o.SL = st.FreeMid + st.FreeAmp * -sw; o.EL = st.FreeElbow; o.AL = 4f; o.WL = -(o.SL + o.EL);
            o.HRx = 3f; o.HLx = 3f;
        }
        // the weapon hand hangs its weapon at its angle, with a little lag behind the arm
        if (!_caster)
        {
            o.WR = p.Spring(10, st.HoldAbs - (o.SR + o.ER), a.Dt, 110f, 13f);
            if (_rogue) o.WL = p.Spring(11, st.HoldAbs - (o.SL + o.EL), a.Dt, 110f, 13f);
        }
        return o;
    }

    /// <summary>
    /// In the air. Rising, one knee is driven high and the other leg trails long, the free arm flung up with the leap and
    /// the weapon drawn back ready; at the top the body gathers, arms out; falling, the legs reach down for the ground
    /// and the arms spread wide for balance, the head up and the eyes on where it will land.
    /// </summary>
    private Body Air(float vy, float time)
    {
        var rise = new Body { Lean = 5, HeadPitch = -10, SR = 62, ER = 74, WR = 30, AR = 20, SL = 128, EL = 22, AL = 16, HR = 92, KR = 104, FR = -16, HL = -24, KL = 34, FL = -42, HRx = 5, HLx = 4 };
        var apex = new Body { Lean = 10, HeadPitch = -6, SR = 70, ER = 64, WR = 26, AR = 30, SL = 82, EL = 40, AL = 34, HR = 74, KR = 104, FR = -14, HL = 30, KL = 88, FL = -26, HRx = 7, HLx = 7 };
        var fall = new Body { Lean = 3, HeadPitch = -3, SR = 46, ER = 34, WR = 24, AR = 56, SL = 50, EL = 26, AL = 62, HR = 34, KR = 30, FR = 12, HL = -4, KL = 36, FL = 2, HRx = 10, HLx = 10 };
        if (_swordsman)
        {
            // (after Marth: both knees drawn up neatly, the sword arm swept back with the blade trailing, the free hand leading)
            rise = new Body { Lean = 6, HeadPitch = -8, SR = -30, ER = 14, WR = -20, AR = 22, SL = 62, EL = 34, AL = 20, HR = 72, KR = 112, FR = -20, HL = 44, KL = 98, FL = -30, HRx = 6, HLx = 6 };
            apex = new Body { Lean = 10, HeadPitch = -6, SR = -24, ER = 16, WR = -18, AR = 28, SL = 56, EL = 40, AL = 28, HR = 70, KR = 108, FR = -14, HL = 52, KL = 100, FL = -24, HRx = 7, HLx = 7 };
            fall = new Body { Lean = 2, HeadPitch = -4, SR = -22, ER = 12, WR = -16, AR = 36, SL = 46, EL = 24, AL = 40, HR = 26, KR = 22, FR = 10, HL = -6, KL = 30, FL = 0, HRx = 9, HLx = 9 };
        }
        Body o = vy > 0 ? Lerp(apex, rise, W3.SmoothStep(0.5f, 5f, vy)) : Lerp(apex, fall, W3.SmoothStep(-0.5f, -6f, vy));
        // (a long fall: the arms and legs work a little against it, not frozen)
        float drift = MathF.Sin(time * 5f) * W3.SmoothStep(-4f, -9f, vy);
        o.AR += 6f * drift; o.AL -= 6f * drift; o.HR += 4f * drift; o.HL -= 4f * drift;
        if (_warden) { o.SL = 48; o.EL = 82; o.AL = 6; o.WL = -(o.SL + o.EL); }
        if (_caster) { o.SR = 46; o.ER = 62; o.AR = 18; }
        if (_rogue)
        {
            // (after Sheik: balled up tight going up, both arms swept back; falling, the legs gathered under, the arms back and wide)
            float up = W3.SmoothStep(-2f, 3f, vy);
            o.Lean = Mathf.Lerp(8f, 22f, up); o.HeadPitch = -14f;
            o.HR = Mathf.Lerp(46f, 92f, up); o.KR = Mathf.Lerp(64f, 128f, up); o.HL = Mathf.Lerp(22f, 76f, up); o.KL = Mathf.Lerp(58f, 120f, up);
            o.FR = -26f; o.FL = -30f;
            o.SR = -48f; o.ER = 30f; o.AR = Mathf.Lerp(34f, 20f, up); o.WR = -30f;
            o.SL = -44f; o.EL = 34f; o.AL = Mathf.Lerp(34f, 20f, up); o.WL = -30f;
        }
        if (_automaton) { o.AR *= 0.6f; o.AL *= 0.6f; o.SL = Math.Min(o.SL, 70f); }
        return o;
    }

    private Body Swim(float t, bool moving, float time)
    {
        if (moving)
        {
            float st = t * Mathf.Tau;
            return new Body
            {
                Roll = -72, HeadPitch = -45, Lean = 0,
                SR = 160 - 70 * Sin01(st), ER = 30 * Sin01(st + 1.5f), AR = 30, WR = 10,
                SL = 160 - 70 * Sin01(st), EL = 30 * Sin01(st + 1.5f), AL = 30,
                HR = 8 * MathF.Sin(st * 2f), KR = 18 + 10 * MathF.Sin(st * 2f + 1f),
                HL = -8 * MathF.Sin(st * 2f), KL = 18 - 10 * MathF.Sin(st * 2f + 1f), HRx = 4, HLx = 4,
            };
        }
        float w = time * 3.2f;
        return new Body
        {
            Roll = -8, Lean = 4, HeadPitch = 0,
            SR = 38 + 18 * MathF.Sin(w), AR = 38, ER = 40, WR = 30,
            SL = 38 + 18 * MathF.Sin(w + 1.3f), AL = 38, EL = 40,
            HR = 25 + 22 * MathF.Sin(w * 0.8f), KR = 55 + 25 * MathF.Sin(w * 0.8f + 1f),
            HL = 25 - 22 * MathF.Sin(w * 0.8f), KL = 55 - 25 * MathF.Sin(w * 0.8f + 1f), HRx = 8, HLx = 8,
        };
    }

    // ================================================================== the blade

    /// <summary>Where the current swing stands, from the swing's own timers (or, without them, the clip).</summary>
    private struct SwingClock
    {
        public bool Timed;
        /// <summary>0..1 through the whole swing, and where the sweep begins and ends in it.</summary>
        public float T, W, U;
    }

    private static SwingClock ClockFor(Player player, float clipT, bool finisher)
    {
        if (player != null && player.IsSwinging)
        {
            float wind = player.SwingWindupSeconds, act = player.SwingActiveSeconds, follow = player.SwingFollowSeconds;
            float total = Math.Max(0.01f, wind + act + follow);
            return new SwingClock { Timed = true, T = Math.Clamp(player.SwingElapsed / total, 0f, 1f), W = wind / total, U = (wind + act) / total };
        }
        float frames = finisher ? 9f : 7f;
        float w = (finisher ? 3f : 2f) / frames;
        return new SwingClock { T = clipT, W = w, U = w + 2f / frames };
    }

    /// <summary>How one hero cuts. Angles are the blade's, in degrees from the aim (up positive).</summary>
    private struct CutStyle
    {
        public float CockA, OverA, CockB, OverB, CockC, OverC;
        /// <summary>The elbow: folded at the top of the wind-up, and as far as it opens through the cut.</summary>
        public float BendCock, BendEnd;
        /// <summary>How far the torso winds back and comes through, degrees (the pelvis goes the other way).</summary>
        public float Wind, Through;
        /// <summary>Lean at the wind-up and through the cut (a downward cut dives, a rising one arches).</summary>
        public float LeanWind, LeanThrough;
        /// <summary>How deep the legs sink and lunge (1 = a full lunge), how far the free arm counters.</summary>
        public float Lunge, Counter;
    }

    private CutStyle CutFor() => _kind switch
    {
        // after Marth: fast, wide crescents with a long, nearly straight arm, the body turning through them, upright and poised
        HeroKind.Swordsman => new CutStyle { CockA = 124f, OverA = -34f, CockB = -84f, OverB = 80f, CockC = 170f, OverC = -42f, BendCock = 54f, BendEnd = 2f, Wind = 34f, Through = 44f, LeanWind = -10f, LeanThrough = 18f, Lunge = 1f, Counter = 0.55f },
        // wide, committed cuts from the shoulder with the whole body behind them
        HeroKind.ShapeShifter => new CutStyle { CockA = 116f, OverA = -34f, CockB = -80f, OverB = 76f, CockC = 162f, OverC = -42f, BendCock = 78f, BendEnd = 6f, Wind = 36f, Through = 40f, LeanWind = -14f, LeanThrough = 26f, Lunge = 1f, Counter = 0.55f },
        // great hacking chops from high over the shoulder, the whole frame swung behind them, the feet barely moving
        HeroKind.Automaton => new CutStyle { CockA = 128f, OverA = -34f, CockB = -74f, OverB = 72f, CockC = 172f, OverC = -42f, BendCock = 82f, BendEnd = 18f, Wind = 28f, Through = 34f, LeanWind = -9f, LeanThrough = 28f, Lunge = 0.6f, Counter = 0.25f },
        // short, hard chops behind the shield, feet planted, the weight thrown into the blow
        HeroKind.Warden => new CutStyle { CockA = 96f, OverA = -38f, CockB = -58f, OverB = 60f, CockC = 134f, OverC = -50f, BendCock = 86f, BendEnd = 24f, Wind = 22f, Through = 27f, LeanWind = -8f, LeanThrough = 20f, Lunge = 0.8f, Counter = 0.15f },
        // quick, close cuts from a crouch, one dagger then the other, the body whipping behind them
        // after Sheik: short, snapping strikes from the crouch that shoot out to full reach and spring straight back, the whole low body whipping behind them
        _ => new CutStyle { CockA = 62f, OverA = -38f, CockB = -54f, OverB = 66f, CockC = 96f, OverC = -50f, BendCock = 112f, BendEnd = 8f, Wind = 30f, Through = 44f, LeanWind = 10f, LeanThrough = 30f, Lunge = 0.75f, Counter = 0.35f },
    };

    private static float SweepEase(float k)
    {
        // gathers speed, is fastest late, then eases into the follow-through
        float s = W3.Smooth01(k);
        return Mathf.Lerp(s, 1f - (1f - k) * (1f - k), 0.35f);
    }

    /// <summary>
    /// A sword (or dagger) cut: the blade's angle over the swing with the arm, torso and stance around
    /// it. The wind-up, sweep and follow-through are the gameplay swing's own beats. Each beat is
    /// committed: the wind-up coils the whole body and rocks the weight onto the back foot, the cut
    /// drives it forward into a lunge behind the blade, the blade carries on past its mark, and the
    /// finish is held a moment before the body recovers.
    /// </summary>
    private Body Cut(Body basePose, string clip, in SwingClock ck, float aim, bool grounded, float runAmount)
    {
        var cs = CutFor();
        char letter = clip[6];
        bool fin = letter == 'c', second = letter == 'b';
        // (the Rogue's second stroke is the left hand's)
        bool handR = !(_rogue && second);
        float cock = aim + (fin ? cs.CockC : second ? cs.CockB : cs.CockA);
        float over = aim + (fin ? cs.OverC : second ? cs.OverB : cs.OverA);
        float sign = cock > over ? 1f : -1f;      // +1: a downward cut
        float t = ck.T, w = ck.W, u = ck.U;
        float carry0 = handR ? basePose.SR + basePose.ER + basePose.WR - 90f : basePose.SL + basePose.EL + basePose.WL - 90f;
        float bend0 = handR ? basePose.ER : basePose.EL;
        // (a finisher is thrown harder: everything goes further)
        float big = fin ? 1.15f : 1f;
        // how far the blade carries on past its mark, degrees
        float carry = (fin ? 9f : 5f) * sign;
        float legW = grounded && Math.Abs(aim) < 65f ? cs.Lunge * (1f - runAmount * 0.9f) : 0f;
        // the body around the blade: wind (0..1, coiling), sweep (0..1, coming through)
        Body Around(float blade, float bend, float sweep, float wind, bool winding)
        {
            var o = basePose;
            float theta = 90f + blade;
            float wrist = -bend * 0.35f;
            float upper = theta - bend - wrist;
            // (held against the torso's pitch: whatever the chest does, the blade keeps its line in the world)
            float pitch = winding ? Mathf.Lerp(basePose.Lean, (sign > 0f ? cs.LeanWind : -cs.LeanWind) * big, wind) : Mathf.Lerp((sign > 0f ? cs.LeanWind : -cs.LeanWind) * big, (sign > 0f ? cs.LeanThrough : -cs.LeanThrough) * Ex * big, sweep);
            upper += (pitch - basePose.Lean) * 0.9f;
            if (handR) { o.SR = upper; o.ER = bend; o.WR = wrist; o.AR = 12f; o.WRy = 0f; }
            else { o.SL = upper; o.EL = bend; o.WL = wrist; o.AL = 12f; o.WLy = 0f; }
            // the torso coils against the cut and comes through it; the pelvis leads the way back round
            float wnd = cs.Wind * Ex * sign * big, thr = cs.Through * Ex * sign * big;
            o.Twist = winding ? Mathf.Lerp(basePose.Twist, -wnd, wind) : Mathf.Lerp(-wnd, thr, sweep);
            o.HipTwist = winding ? -o.Twist * 0.4f : Mathf.Lerp(wnd * 0.4f, -thr * 0.6f, Math.Min(1f, sweep * 1.4f));
            float leanWind = (sign > 0f ? cs.LeanWind : -cs.LeanWind) * big, leanThrough = (sign > 0f ? cs.LeanThrough : -cs.LeanThrough) * Ex * big;
            o.Lean = winding ? Mathf.Lerp(basePose.Lean, leanWind, wind) : Mathf.Lerp(leanWind, leanThrough, sweep);
            // the eyes stay on the mark the whole way: the head holds against the body's turn and pitch
            o.HeadPitch = -aim * 0.3f - o.Lean * 0.25f + (sign > 0f ? -4f * sweep : 3f * sweep);
            o.HeadYaw = -o.Twist * 0.6f;
            // the other arm answers the blade: forward as it goes back, flung back as it comes through
            float free = Math.Clamp(0.4f * blade, -50f, 50f) * cs.Counter * 2.2f;
            if (_warden)
            {
                // (the shield leads: up and forward on the wind-up, driven out with the blow)
                o.SL = 50f + 26f * sweep * (second ? 1.4f : 1f) + 6f * wind; o.EL = 74f - 10f * sweep; o.AL = 4f; o.WL = -(o.SL + o.EL);
            }
            else if (_rogue)
            {
                // (the idle hand tucks back and guards)
                // (after Sheik: the other hand snaps back to guard the face as the strike shoots out)
                float guard = Mathf.Lerp(58f, 40f, sweep) + free * 0.2f * (handR ? 1f : -1f);
                if (handR) { o.SL = guard; o.EL = 104f; o.AL = 12f; o.WL = -10f; }
                else { o.SR = guard; o.ER = 104f; o.AR = 12f; o.WR = -10f; }
            }
            else if (_swordsman)
            {
                // (Marth's off hand: reaching toward the mark in the wind-up, then swept back and out behind, long and open, as the blade comes through)
                o.SL = winding ? Mathf.Lerp(basePose.SL, 36f, wind) : Mathf.Lerp(36f, -56f, sweep);
                o.EL = winding ? Mathf.Lerp(basePose.EL, 20f, wind) : 18f; o.AL = 22f + 18f * sweep; o.WL = 14f;
            }
            else { o.SL = free; o.AL = 24f + 14f * sweep; o.EL = 36f + 10f * sweep; }
            // the feet: the weight rocks back onto the rear foot in the coil, then the leading foot (opposite the cutting hand)
            // steps in and the body drives forward over it as the blade comes through, the rear heel lifting off
            if (legW > 0f)
            {
                bool leadLeft = handR;
                float coil = winding ? W3.Smooth01(wind) : 1f - sweep;
                float drive = winding ? 0f : sweep;
                float leadH = Mathf.Lerp(Mathf.Lerp(14f, 28f, coil), 60f * big, drive), leadK = Mathf.Lerp(Mathf.Lerp(18f, 46f, coil), 58f * big, drive);
                float rearH = Mathf.Lerp(Mathf.Lerp(-10f, -24f, coil), -40f * big, drive), rearK = Mathf.Lerp(Mathf.Lerp(12f, 38f, coil), 22f, drive);
                float dip = -0.055f * coil - 0.11f * drive * big;
                float rearToe = -30f * drive;
                if (leadLeft) { o.HL = Mathf.Lerp(o.HL, leadH, legW); o.KL = Mathf.Lerp(o.KL, leadK, legW); o.HR = Mathf.Lerp(o.HR, rearH, legW); o.KR = Mathf.Lerp(o.KR, rearK, legW); o.FL = Mathf.Lerp(o.FL, 0f, legW); o.FR = Mathf.Lerp(o.FR, rearToe, legW); }
                else { o.HR = Mathf.Lerp(o.HR, leadH, legW); o.KR = Mathf.Lerp(o.KR, leadK, legW); o.HL = Mathf.Lerp(o.HL, rearH, legW); o.KL = Mathf.Lerp(o.KL, rearK, legW); o.FR = Mathf.Lerp(o.FR, 0f, legW); o.FL = Mathf.Lerp(o.FL, rearToe, legW); }
                o.Root.Y += dip * legW * Ex;
                o.Root.X += (-0.03f * coil + 0.05f * drive * big) * legW;
            }
            return o;
        }
        Body At(float tt)
        {
            if (tt < w)
            {
                float k = tt / w;
                float e = W3.Smooth01(k);
                // (a dip against the cut first, so it starts from a coil, and the blade drawn on past the cock: loaded)
                float blade = Mathf.Lerp(carry0, cock, e) + sign * 13f * MathF.Sin(MathF.PI * Math.Min(1f, k * 1.4f)) * (1f - k) + sign * 8f * W3.SmoothStep(0.6f, 1f, k);
                return Around(blade, Mathf.Lerp(bend0, cs.BendCock, e), 0f, e, true);
            }
            float ks = Math.Clamp((tt - w) / (u - w), 0f, 1f);
            float es = SweepEase(ks);
            return Around(Mathf.Lerp(cock + sign * 8f, over, es), Mathf.Lerp(cs.BendCock, cs.BendEnd, es), es, 1f - es, false);
        }
        if (t <= u) return At(t);
        // the follow-through: the blade carries on past its mark and is held there a beat (the cut fully committed),
        // then the body recovers into whatever it was doing
        float fk = Math.Clamp((t - u) / Math.Max(0.01f, 1f - u), 0f, 1f);
        float past = Key(fk, (0f, 0f), (0.22f, 1f), (0.5f, 0.8f), (1f, 0.6f));
        var end = Around(over - carry * past, cs.BendEnd, 1f, 0f, false);
        end.Twist += sign * 6f * past * big;
        end.Lean += (sign > 0f ? 4f : -3f) * past;
        float rk = W3.SmoothStep(0.38f, 1f, fk);
        return Lerp(end, basePose, rk);
    }

    /// <summary>
    /// The Swordsman's heaving swing: both hands on the hilt, the sword hauled up over the head and
    /// far behind, the body arched back and rising onto the toes (the long wind-up), then brought over
    /// and down in one great arc as the whole weight drops into a deep lunge behind it, and held there.
    /// </summary>
    private static Body Heave(Body basePose, float t, bool grounded)
    {
        const float w = 7f / 16f, u = 9f / 16f;
        float blade, lean, twist;
        if (t < w)
        {
            float k = W3.Smooth01(t / w);
            blade = Mathf.Lerp(basePose.SR - 90f + basePose.ER * 0.5f, 176f, k);
            lean = Mathf.Lerp(basePose.Lean, -26f, k);
            twist = -34f * k;
        }
        else if (t < u)
        {
            float k = (t - w) / (u - w);
            k = 1f - (1f - k) * (1f - k);
            blade = Mathf.Lerp(176f, -24f, k);
            lean = Mathf.Lerp(-26f, 50f, k);
            twist = Mathf.Lerp(-34f, 34f, k);
        }
        else
        {
            // (held at the bottom, then drawn back up a little as the body rises out of it)
            float k = W3.SmoothStep(0.4f, 1f, (t - u) / (1f - u));
            blade = Mathf.Lerp(-24f, -14f, k);
            lean = Mathf.Lerp(50f, 18f, k);
            twist = Mathf.Lerp(34f, 8f, k);
        }
        var o = basePose;
        float bend = t < w ? Mathf.Lerp(40f, 80f, t / w) : t < u ? Mathf.Lerp(80f, 6f, (t - w) / (u - w)) : 10f;
        // (the blade's line held against the dive of the chest, so it drives down to the floor and not through it)
        o.SR = 90f + blade - bend * 0.35f + Math.Max(0f, lean) * 0.6f; o.ER = bend; o.WR = -bend * 0.55f; o.AR = 14f; o.WRy = 0;
        // the off hand joins the sword hand on the hilt
        o.SL = o.SR - 10f; o.EL = bend + 12f; o.AL = 26f; o.WL = o.WR;
        o.Lean = lean; o.Twist = twist; o.HipTwist = -twist * 0.5f;
        // (the eyes on the mark: up past the blade at the top, then down the line of the blow)
        o.HeadPitch = t < w ? -6f * (t / w) - lean * 0.3f : 4f - lean * 0.2f;
        o.HeadYaw = -twist * 0.4f;
        if (grounded)
        {
            float rise = t < w ? W3.SmoothStep(0.2f, 1f, t / w) : t < u ? 1f - W3.SmoothStep(0f, 0.5f, (t - w) / (u - w)) : 0f;
            float low = t < w ? 0.2f * (t / w) : t < u ? Mathf.Lerp(0.2f, 1f, (t - w) / (u - w)) : Mathf.Lerp(1f, 0.6f, W3.SmoothStep(0.4f, 1f, (t - u) / (1f - u)));
            o.HR = Mathf.Lerp(o.HR, 58f, low); o.KR = Mathf.Lerp(o.KR, 76f, low);
            o.HL = Mathf.Lerp(o.HL, -36f, low); o.KL = Mathf.Lerp(o.KL, 24f, low);
            o.FR = Mathf.Lerp(o.FR, 0f, low); o.FL = Mathf.Lerp(o.FL, -30f, low);
            o.Root.Y = Mathf.Lerp(o.Root.Y, -0.18f, low) + 0.025f * rise;
            o.FR -= 14f * rise; o.FL -= 14f * rise;
            o.Root.X += -0.035f * rise + 0.06f * low;
        }
        return o;
    }

    private static float AimAngle(string dir) => dir switch { "up" => 90f, "upfwd" => 45f, "downfwd" => -45f, "down" => -90f, _ => 0f };

    // ================================================================== the casters' gestures

    /// <summary>The angle to aim at (degrees up from forward) for the player's last cast, clamped.</summary>
    private static float CastAim(Player player, int facing, float lo, float hi)
    {
        if (player == null) return 0f;
        return Math.Clamp(Mathf.RadToDeg(MathF.Atan2(-player.CastDir.Y, MathF.Max(player.CastDir.X * facing, -0.2f))), lo, hi);
    }

    /// <summary>The Elementalist's firebolt: the free hand draws back and throws a palm forward at the aim, the staff levelled at it.</summary>
    private Body BoltCast(Body o, float t, float aim, ref float staff)
    {
        float draw = Key(t, (0f, 0.3f), (0.28f, 1f), (0.42f, 0f));
        float thrust = Key(t, (0.26f, 0f), (0.42f, 1f), (0.8f, 1f), (1f, 0f));
        // the free arm: elbow folded back by the shoulder, then straight out along the aim
        o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 20f, draw), 90f + aim * 0.85f, thrust);
        o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 112f, draw), 6f, thrust);
        o.AL = Mathf.Lerp(o.AL, 18f, thrust);
        o.WL = Mathf.Lerp(-10f, -34f, thrust);
        // the staff hand snaps the orb up behind the shoulder, then flicks it out at arm's length, level with the mark
        o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 150f, draw), 92f + aim * 0.7f, thrust);
        o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 50f, draw), 16f, thrust);
        staff = Mathf.Lerp(Mathf.Lerp(staff, 128f, draw), aim + 6f, thrust);
        o.Twist += 16f * draw - 22f * thrust; o.HipTwist -= 7f * draw - 10f * thrust;
        o.Lean += -6f * draw + 12f * thrust - aim * 0.05f * thrust;
        o.HeadPitch += -aim * 0.2f * thrust;
        o.HR += 18f * thrust; o.KR += 20f * thrust; o.HL -= 14f * thrust;
        o.Root.Y -= 0.06f * thrust;
        o.Root.X += -0.02f * draw + 0.045f * thrust;
        o.HeadYaw = -o.Twist * 0.5f;
        return o;
    }

    /// <summary>The Elementalist's updraft: a deep crouch, both hands dragging low, then up onto the toes and the arms and staff sweeping up.</summary>
    private Body UpdraftCast(Body o, float t, ref float staff)
    {
        float crouch = Key(t, (0f, 0f), (0.32f, 1f), (0.46f, 0.9f), (0.64f, 0f));
        float rise = Key(t, (0.3f, 0f), (0.56f, 1f), (0.86f, 1f), (1f, 0f));
        o.Root.Y -= 0.2f * crouch; o.Root.Y += 0.03f * rise;
        o.HR += 62f * crouch; o.KR += 86f * crouch; o.HL += 52f * crouch; o.KL += 80f * crouch;
        o.FR -= 10f * rise; o.FL -= 10f * rise;
        o.Lean += 20f * crouch - 16f * rise;
        o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, -25f, crouch), 160f, rise); o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 30f, crouch), 20f, rise);
        o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, -30f, crouch), 150f, rise); o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 26f, crouch), 14f, rise); o.AL = Mathf.Lerp(o.AL, 34f, rise);
        o.HeadPitch += -24f * rise;
        staff = Mathf.Lerp(Mathf.Lerp(staff, 64f, crouch), 92f, rise);
        return o;
    }

    /// <summary>The Elementalist's blizzard: the staff raised and stirred in circles over the head, then thrust down at the spot.</summary>
    private Body StormCast(Body o, float t, float aim, ref float staff)
    {
        float up = Key(t, (0f, 0f), (0.2f, 1f), (0.72f, 1f), (0.86f, 0f));
        float stir = Math.Clamp((t - 0.15f) / 0.55f, 0f, 1f);
        float ang = stir * Mathf.Tau * 2f;
        float plunge = Key(t, (0.68f, 0f), (0.82f, 1f), (1f, 0f));
        // the arms held high with the staff whirling in a circle above the head
        o.SR = Mathf.Lerp(o.SR, 150f + 14f * MathF.Sin(ang), up); o.ER = Mathf.Lerp(o.ER, 36f + 14f * MathF.Cos(ang), up); o.AR = Mathf.Lerp(o.AR, 14f, up);
        o.SL = Mathf.Lerp(o.SL, 120f - 10f * MathF.Sin(ang), up); o.EL = Mathf.Lerp(o.EL, 24f, up); o.AL = Mathf.Lerp(o.AL, 50f + 16f * MathF.Cos(ang), up);
        staff = Mathf.Lerp(staff, 92f + 62f * MathF.Sin(ang - 0.6f) * (1f - plunge), up);
        staff = Mathf.Lerp(staff, aim - 20f, plunge);
        o.SR = Mathf.Lerp(o.SR, 60f + aim * 0.5f, plunge); o.ER = Mathf.Lerp(o.ER, 20f, plunge);
        o.Lean += -10f * up + 30f * plunge; o.HeadPitch += -20f * up * (1f - plunge) + 8f * plunge;
        o.Twist += 10f * MathF.Sin(ang) * up;
        o.Root.Y += 0.03f * up - 0.08f * plunge;
        o.HR += 34f * plunge; o.KR += 38f * plunge; o.HL -= 18f * plunge;
        return o;
    }

    /// <summary>The Elementalist's snap: the free hand held out, then a sharp flick of the fingers, the shoulder dropping as it lands.</summary>
    private Body SnapCast(Body o, float t, float aim, ref float staff)
    {
        float reach = Key(t, (0f, 0f), (0.3f, 1f), (0.88f, 1f), (1f, 0f));
        float flick = Key(t, (0.42f, 0f), (0.5f, 1f), (0.66f, 0.25f), (0.8f, 0f));
        o.SL = Mathf.Lerp(o.SL, 84f + aim * 0.8f + 8f * flick, reach); o.EL = Mathf.Lerp(o.EL, 14f + 46f * flick, reach); o.AL = Mathf.Lerp(o.AL, 22f, reach);
        o.WL = -30f * flick;
        o.SR = Mathf.Lerp(o.SR, 40f, reach * 0.6f); o.ER = Mathf.Lerp(o.ER, 80f, reach * 0.6f);
        o.Twist += -14f * reach + 10f * flick; o.HipTwist -= -5f * reach;
        o.Lean += 6f * reach - 8f * flick;
        o.HeadPitch += -aim * 0.2f * reach + 6f * flick;
        o.Root.Y -= 0.03f * reach + 0.03f * flick;
        o.HR += 10f * reach; o.KR += 12f * reach;
        staff = Mathf.Lerp(staff, 70f, reach);
        return o;
    }

    // ================================================================== every frame

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T;
        float speed = MathF.Abs(a.Vel.X);

        // ---- base: where the body is and how it moves
        ref float phase = ref p.Vars[0];
        float runAmount = a.OnFloor ? W3.SmoothStep(0.5f, 3.2f, speed) : 0f;
        if (a.FixedGait) phase = a.Gait;
        else if (a.OnFloor && speed > 0.05f)
        {
            // (moving against the way it faces, it backpedals: the stride runs in reverse)
            float dir = a.Vel.X * a.Facing >= 0f ? 1f : -1f;
            phase = Mathf.PosMod(phase + dir * speed * a.Dt / StrideAt(speed), 1f);
        }
        Body o;
        // (the Automaton never swims: it walks the bottom and sinks upright, feet first)
        if (a.InWater && !_automaton && !c.StartsWith("dodge")) o = Swim(t, c == "swim", a.Time);
        else if (!a.OnFloor && c != "wall_slide") o = Air(a.Vel.Y, a.Time);
        else o = runAmount > 0.01f ? Lerp(Idle(a.Time), Run(phase, speed, p, a), runAmount) : Idle(a.Time);

        // ---- the clip on top
        var player = a.Owner as Player;
        // a caster's staff: its angle in the body's frame (degrees from forward, up positive)
        // (running, it's carried angled forward so its foot doesn't trail through the ground)
        float staff = Mathf.Lerp(78f, _st.HoldAbs, runAmount);
        if (c.StartsWith("slash_"))
        {
            float aim = AimAngle(c[8..]);
            if (player != null && player.SwingAim != Vector2.Zero)
                aim = Mathf.RadToDeg(MathF.Atan2(-player.SwingAim.Y, MathF.Max(player.SwingAim.X * a.Facing, -0.25f)));
            o = Cut(o, c, ClockFor(player, t, c[6] == 'c'), aim, a.OnFloor, runAmount);
        }
        else switch (c)
        {
            case "rest":
                // (the rest pose, for close-ups on the model sheet)
                o = new Body();
                break;
            case "sit":
                // (at the camp: T blends from sitting, 0, to standing ready, 1)
                o = Lerp(Sit(a.Time), Idle(a.Time), W3.Smooth01(t));
                if (_caster) staff = Mathf.Lerp(92f, 78f, t);
                break;
            case "jump_start":
                {
                    // the take-off: a deep coil, arms swung back, eyes up at where it's going; then everything thrown upward at once, onto the toes
                    float coil = Key(t, (0, 0), (0.45f, 1f), (0.8f, 0.2f), (1, 0));
                    float fling = Key(t, (0.4f, 0f), (1f, 1f));
                    o.Root.Y += -0.2f * coil + 0.05f * fling;
                    o.KR += 80 * coil; o.KL += 80 * coil; o.HR += 50 * coil; o.HL += 50 * coil;
                    o.FR -= 30 * fling; o.FL -= 30 * fling;
                    o.SR += -64 * coil + 80 * fling; o.SL += -58 * coil + 100 * fling; o.ER += 10 * coil; o.EL -= 10 * fling;
                    o.Lean += 24 * coil - 6 * fling; o.HeadPitch += -16 * coil - 6 * fling;
                    break;
                }
            case "land":
                {
                    // the hero's landing: the fall soaked up in a deep crouch, the free hand reaching for the ground, the weapon held
                    // out wide behind for balance, the head up and the eyes already on what's next; then rising tall out of it
                    float k = Key(t, (0, 1), (0.3f, 0.88f), (1, 0));
                    k = k * k * (3f - 2f * k);
                    // (the Swordsman comes down light, like a fencer: a shallower give, the same poise)
                    if (_swordsman) k *= 0.6f;
                    o.Root.Y -= 0.24f * k; o.Root.X -= 0.02f * k; o.Lean += 28 * k;
                    o.HR += 56 * k; o.KR += 94 * k; o.HL += 40 * k; o.KL += 84 * k;
                    o.HeadPitch -= 18 * k;
                    if (!_warden) { o.SL = Mathf.Lerp(o.SL, 34, k); o.EL = Mathf.Lerp(o.EL, 12, k); o.AL += 26 * k; }
                    if (!_caster) { o.SR = Mathf.Lerp(o.SR, -12, k); o.AR += 34 * k; o.ER = Mathf.Lerp(o.ER, 28, k); if (!_rogue) o.WR = Mathf.Lerp(o.WR, -60, k); }
                    // (the chest lifting a touch past upright as it stands: done, and ready)
                    o.Lean -= 5f * MathF.Sin(MathF.PI * W3.SmoothStep(0.5f, 1f, t));
                    break;
                }
            case "run_start":
                {
                    // the first steps out of a stand: driven off the back foot, the whole body tipped far forward to put the weight ahead of
                    // the feet (the steeper lean of a body accelerating), the arms pumping hard, the head up
                    float k = Key(t, (0, 1), (1, 0));
                    o.Pitch += 14 * k; o.Lean += 8 * k; o.SR -= 22 * k; o.SL += 18 * k; o.Root.Y -= 0.03f * k; o.HeadPitch -= 16 * k;
                    break;
                }
            case "run_stop":
                {
                    // braking: the whole body tipped back behind the leading foot, which digs in ahead
                    float k = Key(t, (0, 1), (0.6f, 0.8f), (1, 0));
                    o.Pitch = Mathf.Lerp(o.Pitch, -10f, k); o.Lean -= 16 * k; o.Root.Y -= 0.08f * k; o.HeadPitch += 4 * k;
                    o.HR = Mathf.Lerp(o.HR, 48, k); o.KR = Mathf.Lerp(o.KR, 26, k);
                    o.HL = Mathf.Lerp(o.HL, 2, k); o.KL = Mathf.Lerp(o.KL, 44, k);
                    o.FR = Mathf.Lerp(o.FR, 18, k); o.FL = Mathf.Lerp(o.FL, 0, k);
                    if (!_warden) { o.SL += 36 * k; o.AL += 22 * k; }
                    o.SR += 18 * k;
                    break;
                }
            case "turn_r2l":
            case "turn_l2r":
                o.Root.Y -= 0.04f * MathF.Sin(t * MathF.PI);
                break;
            case "dodge" when _swordsman && !((player != null && player.IsDodging) || a.Vel.Length() > 18f):
                {
                    // the Swordsman's double jump, after Marth: a tight forward somersault, knees to the chest, the sword arm
                    // held out long so the blade wheels round with him, opening out at the end into the rise
                    float tuck = MathF.Sin(Math.Clamp(t * 1.1f, 0f, 1f) * MathF.PI);
                    o.Roll = -360f * W3.Smooth01(Math.Min(1f, t * 1.1f));
                    o.Lean = 20f * tuck; o.HeadPitch = 26f * tuck;
                    o.HR = Mathf.Lerp(o.HR, 118f, tuck); o.KR = Mathf.Lerp(o.KR, 138f, tuck); o.HL = Mathf.Lerp(o.HL, 112f, tuck); o.KL = Mathf.Lerp(o.KL, 136f, tuck);
                    o.FR = Mathf.Lerp(o.FR, -24f, tuck); o.FL = Mathf.Lerp(o.FL, -24f, tuck);
                    o.SR = Mathf.Lerp(o.SR, 70f, tuck); o.ER = Mathf.Lerp(o.ER, 10f, tuck); o.WR = Mathf.Lerp(o.WR, 0f, tuck); o.AR = Mathf.Lerp(o.AR, 30f, tuck);
                    o.SL = Mathf.Lerp(o.SL, 60f, tuck); o.EL = Mathf.Lerp(o.EL, 110f, tuck); o.AL = Mathf.Lerp(o.AL, 10f, tuck);
                    break;
                }
            case "dodge" when _swordsman:
                {
                    // the dash-slash: no roll, no tuck: he drops into a long lean, head up and eyes on the mark, the sword arm drawn back and cocked ready to strike, the off arm thrown forward, legs in a running lunge
                    float k = W3.SmoothStep(0f, 0.18f, t);
                    o.Roll = 0f; o.Pitch = Mathf.Lerp(o.Pitch, 0f, k);
                    o.Lean = Mathf.Lerp(o.Lean, 62f, k); o.HeadPitch = Mathf.Lerp(o.HeadPitch, -32f, k); o.Twist = Mathf.Lerp(o.Twist, -14f, k);
                    o.Root.Y = Mathf.Lerp(o.Root.Y, -0.07f, k);
                    o.SR = Mathf.Lerp(o.SR, -66f, k); o.ER = Mathf.Lerp(o.ER, 72f, k); o.WR = Mathf.Lerp(o.WR, 24f, k); o.AR = Mathf.Lerp(o.AR, 10f, k);
                    o.SL = Mathf.Lerp(o.SL, 58f, k); o.EL = Mathf.Lerp(o.EL, 30f, k); o.AL = 12f;
                    o.HR = Mathf.Lerp(o.HR, 54f, k); o.KR = Mathf.Lerp(o.KR, 40f, k); o.FR = Mathf.Lerp(o.FR, -22f, k);
                    o.HL = Mathf.Lerp(o.HL, -46f, k); o.KL = Mathf.Lerp(o.KL, 24f, k); o.FL = Mathf.Lerp(o.FL, -12f, k);
                    break;
                }
            case "dodge":
                {
                    float tuck = MathF.Sin(Math.Clamp(t * 1.15f, 0, 1) * MathF.PI);
                    o.Roll = -360f * W3.Smooth01(t); o.Pitch = 0f;
                    o.Root.Y = -0.3f * tuck;
                    o.HR = 20 + 105 * tuck; o.KR = 15 + 130 * tuck; o.HL = 20 + 105 * tuck; o.KL = 15 + 130 * tuck;
                    o.FR = -20 * tuck; o.FL = -20 * tuck;
                    o.SR = 20 + 56 * tuck; o.ER = 40 + 76 * tuck; o.SL = 20 + 56 * tuck; o.EL = 40 + 76 * tuck;
                    o.Lean = 34 * tuck; o.HeadPitch = 34 * tuck;
                    break;
                }
            case "airdash":
                o = new Body { Lean = 66, HeadPitch = -42, SR = -34, ER = 30, WR = 60, AR = 12, SL = -50, EL = 20, AL = 12, HR = -20, KR = 40, FR = -30, HL = -36, KL = 18, FL = -26, HRx = 4, HLx = 4 };
                if (_warden) { o.SL = 60; o.EL = 70; o.WL = -(o.SL + o.EL); }
                break;
            case "throw" when _rogue:
                {
                    // after Sheik's needles: crouched with both arms crossed over the chest, then both flung out at once, fanned,
                    // the body springing forward and up behind them; the hands hang there a moment, then back to guard
                    float load = Key(t, (0, 0), (0.32f, 1f), (0.44f, 0f));
                    float fling = Key(t, (0.32f, 0f), (0.46f, 1f), (0.75f, 0.85f), (1f, 0f));
                    o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 44f, load), 98f, fling); o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 124f, load), 6f, fling);
                    o.AL = Mathf.Lerp(Mathf.Lerp(o.AL, -16f, load), 30f, fling); o.WL = Mathf.Lerp(o.WL, -6f, fling);
                    o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 50f, load), 74f, fling); o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 124f, load), 8f, fling);
                    o.AR = Mathf.Lerp(Mathf.Lerp(o.AR, -16f, load), 34f, fling); o.WR = Mathf.Lerp(o.WR, -6f, fling);
                    o.Lean += 10f * load - 6f * fling; o.HeadPitch -= 6f * fling;
                    o.Root.Y += -0.04f * load + 0.02f * fling; o.Root.X += -0.015f * load + 0.03f * fling;
                    o.KR += 16f * load; o.KL += 16f * load; o.HR += 10f * load; o.HL += 10f * load;
                    break;
                }
            case "throw":
                {
                    // the off hand flings the dagger (the sword stays ready)
                    float k = t;
                    o.SL = Key(k, (0, 20), (0.35f, -72), (0.55f, 125), (1, 70));
                    o.EL = Key(k, (0, 30), (0.35f, 92), (0.55f, 4), (1, 25));
                    o.AL = 18;
                    o.Twist = Key(k, (0, 0), (0.35f, 30), (0.55f, -28), (1, -10));
                    o.HipTwist = -o.Twist * 0.45f;
                    o.HeadYaw = -o.Twist * 0.5f;
                    o.Lean += Key(k, (0, 0), (0.35f, -12), (0.55f, 16), (1, 5));
                    // (the weight goes back on the draw and forward behind the throw)
                    o.Root.X += Key(k, (0, 0), (0.35f, -0.025f), (0.55f, 0.03f), (1, 0.01f));
                    if (_rogue) { o.HR += 16 * Key(k, (0, 0), (0.55f, 1), (1, 0)); o.KR += 20 * Key(k, (0, 0), (0.55f, 1), (1, 0)); o.Root.Y -= 0.04f * Key(k, (0, 0), (0.55f, 1), (1, 0)); }
                    break;
                }
            case "bash":
                {
                    // the Warden's Guarded Charge: driven low behind the shield, sword held back
                    float k = Key(t, (0, 0.3f), (0.25f, 1f), (1, 1f));
                    o.Lean = Mathf.Lerp(o.Lean, 32, k); o.Twist = Mathf.Lerp(o.Twist, 14, k); o.HeadPitch = Mathf.Lerp(o.HeadPitch, -14, k);
                    o.Root.Y = Mathf.Lerp(o.Root.Y, -0.1f, k);
                    o.SR = Mathf.Lerp(o.SR, -38, k); o.ER = Mathf.Lerp(o.ER, 58, k); o.WR = Mathf.Lerp(o.WR, 25, k); o.AR = 14;
                    o.HR = Mathf.Lerp(o.HR, 54, k); o.KR = Mathf.Lerp(o.KR, 44, k); o.HL = Mathf.Lerp(o.HL, -34, k); o.KL = Mathf.Lerp(o.KL, 14, k);
                    o.FR = Mathf.Lerp(o.FR, 0, k); o.FL = Mathf.Lerp(o.FL, -25, k);
                    break;
                }
            case "cast":
                if (_elementalist) o = BoltCast(o, t, CastAim(player, a.Facing, -70f, 80f), ref staff);
                else
                {
                    // the drain bolt: staff drawn back, then thrust crystal-first along the aim
                    float aim = CastAim(player, a.Facing, -70f, 80f);
                    float draw = Key(t, (0, 0.4f), (0.3f, 1f), (0.45f, 0f));
                    float thrust = Key(t, (0.3f, 0f), (0.45f, 1f), (0.8f, 1f), (1f, 0f));
                    o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 30, draw), 78 + aim * 0.75f, thrust);
                    o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 92, draw), 12, thrust);
                    o.Twist += -16 * draw + 18 * thrust; o.HipTwist -= -6 * draw + 7 * thrust;
                    o.Lean += -6 * draw + 12 * thrust - aim * 0.05f * thrust;
                    o.HeadPitch += -aim * 0.2f * thrust;
                    o.SL = Mathf.Lerp(o.SL, 66 + aim * 0.5f, thrust); o.EL = Mathf.Lerp(o.EL, 16, thrust); o.AL = Mathf.Lerp(o.AL, 20, thrust);
                    o.HR += 18 * thrust; o.KR += 20 * thrust; o.HL -= 14 * thrust; o.Root.Y -= 0.05f * thrust;
                    o.Root.X += -0.02f * draw + 0.04f * thrust; o.HeadYaw = -o.Twist * 0.5f;
                    staff = Mathf.Lerp(Mathf.Lerp(staff, 112, draw), aim + 18, thrust);
                }
                break;
            case "rupture":
                if (_elementalist) { o = SnapCast(o, t, CastAim(player, a.Facing, -60f, 70f), ref staff); break; }
                {
                    // the staff raised high behind, the free hand thrust out at the creature, open...
                    // then clenched and torn back to the chest as it bursts
                    float aim = CastAim(player, a.Facing, -60f, 70f);
                    float reach = Key(t, (0, 0), (0.3f, 1f), (0.46f, 1f), (0.56f, 0f));
                    float yank = Key(t, (0.46f, 0f), (0.56f, 1f), (0.8f, 1f), (1f, 0f));
                    o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 86 + aim * 0.8f, reach), 28, yank);
                    o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 4, reach), 124, yank);
                    o.AL = Mathf.Lerp(o.AL, 24, Math.Max(reach, yank));
                    o.WL = Mathf.Lerp(-18f * reach, 44f, yank);
                    o.SR = Mathf.Lerp(o.SR, 150, Math.Max(reach, yank)); o.ER = Mathf.Lerp(o.ER, 30, Math.Max(reach, yank)); o.AR = Mathf.Lerp(o.AR, 30, Math.Max(reach, yank));
                    o.Lean += 16 * reach - 14 * yank - aim * 0.05f * reach;
                    o.Twist += -20 * reach + 24 * yank; o.HipTwist -= -8 * reach + 9 * yank;
                    o.HeadPitch += -aim * 0.25f * reach;
                    o.HR += 22 * reach; o.KR += 20 * reach; o.HL -= 14 * reach;
                    staff = Mathf.Lerp(staff, 104, Math.Max(reach, yank));
                }
                break;
            case "heave":
                o = player != null && player.AxeKind > 0 ? AxeHeave(o, player.AxeStage, a.OnFloor, player.AxeKind, a.Time) : Heave(o, t, a.OnFloor);
                break;
            case "shove":
                {
                    // the Warden's shield bash: a short step and the shield punched straight out
                    float k = Key(t, (0, 0), (0.22f, 1f), (0.55f, 1f), (1f, 0f));
                    o.SL = Mathf.Lerp(45, 100, k); o.EL = Mathf.Lerp(75, 6, k); o.AL = 8;
                    o.WL = -(o.SL + o.EL) + 90f * k;
                    o.WLy = a.Facing > 0 ? -30 : 30;
                    o.SR = Mathf.Lerp(o.SR, -28, k); o.ER = Mathf.Lerp(o.ER, 74, k);
                    o.Lean = Mathf.Lerp(o.Lean, 30, k); o.Twist = Mathf.Lerp(o.Twist, -22, k); o.HeadPitch = Mathf.Lerp(o.HeadPitch, -6, k);
                    o.HR = Mathf.Lerp(o.HR, 46, k); o.KR = Mathf.Lerp(o.KR, 38, k); o.HL = Mathf.Lerp(o.HL, -30, k); o.KL = Mathf.Lerp(o.KL, 12, k);
                    o.FR = Mathf.Lerp(o.FR, 0, k); o.FL = Mathf.Lerp(o.FL, -28, k);
                    o.Root.Y = Mathf.Lerp(o.Root.Y, -0.08f, k);
                    break;
                }
            case "hex":
                if (_elementalist) { o = UpdraftCast(o, t, ref staff); break; }
                {
                    // raised overhead in both hands, then driven down into the ground
                    float up = Key(t, (0, 0), (0.4f, 1f), (0.52f, 0f));
                    float slam = Key(t, (0.4f, 0f), (0.52f, 1f), (0.8f, 1f), (1f, 0f));
                    // (the fist stays high enough in the slam for the heel to strike the floor, not sink into it)
                    o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 156, up), 70, slam); o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 22, up), 42, slam);
                    o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 154, up), 60, slam); o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 28, up), 40, slam); o.AL = 18;
                    o.Lean += -10 * up + 28 * slam; o.HeadPitch += -14 * up + 12 * slam;
                    o.Root.Y -= 0.15f * slam;
                    o.HR += 40 * slam; o.KR += 62 * slam; o.HL += 34 * slam; o.KL += 56 * slam;
                    staff = Mathf.Lerp(Mathf.Lerp(staff, 92, up), 86, slam);
                }
                break;
            case "heal":
                if (_elementalist) { o = StormCast(o, t, CastAim(player, a.Facing, -60f, 60f), ref staff); break; }
                {
                    // the staff lifted high, the free hand opened, the face turned upward
                    float k = Key(t, (0, 0), (0.25f, 1f), (0.75f, 1f), (1f, 0f));
                    o.SR = Mathf.Lerp(o.SR, 168, k); o.ER = Mathf.Lerp(o.ER, 6, k); o.AR = Mathf.Lerp(o.AR, 14, k);
                    o.SL = Mathf.Lerp(o.SL, 110, k); o.EL = Mathf.Lerp(o.EL, 10, k); o.AL = Mathf.Lerp(o.AL, 56, k);
                    o.HeadPitch += -26 * k; o.Lean += -8 * k;
                    o.Root.Y += 0.03f * k;
                    o.KR -= 6 * k; o.KL -= 6 * k;
                    staff = Mathf.Lerp(staff, 90, k);
                }
                break;
            case "hurt":
                {
                    // rocked hard by the blow, but the weapon stays up and the body comes straight back at it, head first
                    float k = Key(t, (0, 0), (0.14f, 1), (0.5f, 0.4f), (1, 0));
                    float head = Key(t, (0, 0), (0.12f, 1), (0.36f, 0.15f), (1, 0));
                    o.Lean -= 30 * k; o.HeadPitch -= 30 * head; o.Twist += 16 * k; o.HipTwist -= 7 * k;
                    o.SR += 12 * k; o.AR += 16 * k;
                    // (the Warden's shield stays between him and it)
                    if (!_warden) { o.SL += 44 * k; o.AL += 34 * k; }
                    o.Root += new Vector3(-0.07f * k, -0.06f * k, 0);
                    o.KR += 32 * k; o.KL += 26 * k;
                    break;
                }
            case "death":
                {
                    float k1 = W3.SmoothStep(0f, 0.35f, t), k2 = W3.SmoothStep(0.3f, 0.85f, t);
                    o = Idle(0);
                    o.Lean = -15 * k1; o.KR = 16 + 60 * k1; o.KL = 10 + 70 * k1; o.HR = 12 + 30 * k1; o.HL = -8 + 30 * k1;
                    o.Roll = 84 * k2;
                    o.Root = new Vector3(-0.1f * k2, -0.05f * k1 - 0.62f * k2, 0);
                    o.SR = 24 + 80 * k2; o.AR = 30 * k2; o.ER = 34 - 20 * k2; o.SL = -2 + 70 * k2; o.AL = 30 * k2;
                    o.HeadPitch = -30 * k2; o.KR = Mathf.Lerp(o.KR, 25, k2); o.KL = Mathf.Lerp(o.KL, 55, k2);
                    break;
                }
            case "wall_slide":
                o = new Body
                {
                    Lean = -8, HeadPitch = -10, Root = new Vector3(-0.02f, -0.04f, 0),
                    SR = 60, ER = 40, WR = 40, AR = 12,
                    SL = 140, EL = 25, AL = 10,
                    HR = 55, KR = 75, FR = -10, HL = 15, KL = 25, HRx = 6, HLx = 6,
                };
                if (_warden) { o.SL = 70; o.EL = 60; o.WL = -(o.SL + o.EL); }
                break;
        }

        // ---- ducking: hips down, knees folded, the body hunched forward (the feet stay where they were)
        if (player != null && player.Crouching && !c.StartsWith("slash_") && c != "death" && !c.StartsWith("dodge"))
        {
            o.Pitch = 0f; o.Root.Y -= 0.27f;
            o.HR = 78; o.KR = 112; o.HL = 66; o.KL = 104; o.FR = 8; o.FL = 8;
            o.Lean += 20; o.HeadPitch += 10;
            o.SR = Math.Max(o.SR, 30); o.SL = Math.Max(o.SL, 20);
        }

        // ---- the Automaton braced: sunk on its knees, leaning in, the free forearm up across the body like a shield
        if (_automaton && player != null && player.Bracing && !c.StartsWith("slash_") && c != "death")
        {
            o.Pitch = 0f; o.Root.Y -= 0.1f;
            o.HR = 52; o.KR = 74; o.HL = 26; o.KL = 50; o.FR = 6; o.FL = 6;
            o.Lean += 12;
            o.SL = 78; o.EL = 96; o.AL = 0;
            o.SR = Math.Max(o.SR, 36);
        }

        // ---- a caster's hand holds the staff at its angle, whatever the arm is doing
        if (_caster && c != "death") o.WR = staff + o.Lean - o.SR - o.ER;

        // ---- the elemental Warden behind his Earth Shield: down on one knee, the shield up before him
        if (_warden && player != null && player.EarthShieldUp && c != "death")
        {
            o.Pitch = 0f; o.Root.Y -= 0.3f;
            o.HR = 92; o.KR = 96; o.FR = 0;
            o.HL = 8; o.KL = 120; o.FL = 34;
            o.Lean += 14; o.HeadPitch += 6;
            o.SR = Math.Max(o.SR, 30);
        }

        // ---- the Swordsman's Blazing Charge: bent low into it, legs driving, the sword held up before him like a ram
        if (_swordsman && player != null && player.BlazeOn && c != "death")
        {
            o.Roll = 0f; o.Pitch = 0f;
            o.Lean = 38; o.HeadPitch = -20; o.Twist = -6;
            o.Root.Y = -0.06f;
            o.SR = 55; o.ER = 70; o.WR = -20; o.AR = 6;
            o.SL = 40; o.EL = 60; o.AL = 10;
            o.HR = 54; o.KR = 40; o.FR = -22;
            o.HL = -46; o.KL = 24; o.FL = -12;
        }

        // ---- the warden's shield, raised toward where she guards
        if (_warden && player != null && player.ShieldRaised && !c.StartsWith("dodge") && c != "death" && c != "shove")
        {
            var d = player.ShieldDir;
            float ang = Mathf.RadToDeg(MathF.Atan2(-d.Y, d.X * a.Facing));
            o.SL = 90 + ang - 25; o.EL = 45; o.AL = 10;
            o.WL = ang - (o.SL + o.EL);
            o.WLy = a.Facing > 0 ? -30 : 30;
        }

        // ---- cloak and scarf trail behind the motion
        float fwd = a.Vel.X * a.Facing;
        float trail = -Math.Clamp(fwd * 5.5f, -30f, 70f) - Math.Clamp(-a.Vel.Y * 4f, 0f, 45f);
        if (a.InWater) trail *= 0.5f;
        float flutter = MathF.Sin(a.Time * 9f) * Math.Min(1f, (speed + MathF.Abs(a.Vel.Y)) / 6f) * 6f;
        // (each footfall kicks the cloak: it bounces with the stride)
        float bounce = runAmount * 8f * MathF.Cos(2f * Mathf.Tau * phase);
        float cp0 = p.Spring(0, trail * 0.35f + o.Lean * 0.3f + bounce * 0.5f, a.Dt, 70f, 9f);
        float cp1 = p.Spring(1, trail * 0.35f + flutter + bounce, a.Dt, 60f, 8f);
        float cp2 = p.Spring(2, trail * 0.3f - flutter + bounce * 0.8f, a.Dt, 55f, 7f);
        float cp3 = p.Spring(3, trail * 0.2f + flutter * 1.5f + bounce * 0.6f, a.Dt, 50f, 6f);
        float sf0 = p.Spring(4, trail * 0.4f - 6f + flutter, a.Dt, 45f, 6f);
        float sf1 = p.Spring(5, trail * 0.3f - flutter * 1.8f, a.Dt, 40f, 5f);
        float sf2 = p.Spring(6, trail * 0.2f + flutter * 2.5f, a.Dt, 35f, 4f);

        if (DebugLog && a.Dt == 0f) GD.Print($"[hero] {c} phase {phase:0.00} SR {o.SR:0} ER {o.ER:0} WR {o.WR:0} lean {o.Lean:0} HR {o.HR:0} KR {o.KR:0} HL {o.HL:0} KL {o.KL:0}");
        Apply(p, o);
        // (the elemental Warden's steel shield shrunk away: his mode's shield is held in its place)
        if (shieldBone >= 0 && player != null && player.Elemental) p.Scale[shieldBone] = Vector3.One * 0.001f;
        p.Bend(cape0, cp0); p.Bend(cape1, cp1); p.Bend(cape2, cp2); p.Bend(cape3, cp3);
        p.Bend(scarf0, sf0 * 0.6f); p.Add(scarf0, 0, sf1 * 0.3f, 0); p.Bend(scarf1, sf1); p.Bend(scarf2, sf2);
    }

    private void Apply(CreaturePose p, Body o)
    {
        // (the pelvis turns one way and the chest the other: the chest's turn is Twist in all, whatever the pelvis does)
        p.Set(hips, o.Bank * 0.3f, o.HipTwist + o.Twist * 0.25f, o.Roll - o.Pitch);
        p.Move(hips, o.Root);
        p.Set(spine, o.Bank * 0.35f, o.Twist * 0.35f - o.HipTwist * 0.45f, -o.Lean * 0.45f);
        p.Set(chest, o.Bank * 0.35f, o.Twist * 0.4f - o.HipTwist * 0.55f, -o.Lean * 0.55f);
        p.Set(neck, 0, o.HeadYaw * 0.4f, o.Lean * 0.3f - o.HeadPitch * 0.4f);
        p.Set(head, 0, o.HeadYaw * 0.6f, o.Lean * 0.45f - o.HeadPitch * 0.6f);
        // arms: swing about the side axis first, then out from the body
        p.Rot[uarm[0]] = CreaturePose.Q(-o.AR, 0, 0) * CreaturePose.Q(0, 0, o.SR);
        p.Set(farm[0], 0, 0, o.ER);
        p.Set(hand[0], 0, o.WRy, o.WR);
        p.Rot[uarm[1]] = CreaturePose.Q(o.AL, 0, 0) * CreaturePose.Q(0, 0, o.SL);
        p.Set(farm[1], 0, 0, o.EL);
        p.Set(hand[1], 0, o.WLy, o.WL);
        // legs: hip, knee, and a foot held at its pitch in the world
        // (the thighs turned back against the body's pitch: HR and HL are the legs' angles in the world, so the solved feet stay planted)
        p.Rot[thigh[0]] = CreaturePose.Q(-o.HRx, 0, 0) * CreaturePose.Q(0, 0, o.HR + o.Pitch);
        p.Set(shin[0], 0, 0, -o.KR);
        p.Set(foot[0], 0, 0, o.FR - (o.HR - o.KR));
        p.Rot[thigh[1]] = CreaturePose.Q(o.HLx, 0, 0) * CreaturePose.Q(0, 0, o.HL + o.Pitch);
        p.Set(shin[1], 0, 0, -o.KL);
        p.Set(foot[1], 0, 0, o.FL - (o.HL - o.KL));
    }

    // ================================================================== the blade's trail

    /// <summary>Blade length by hero (metres): the sword, the shortsword, a dagger.</summary>
    private float BladeLength => _rogue ? 0.3f : _warden ? 0.74f : _automaton ? 0.72f : 1.05f;

    /// <summary>
    /// Reads where the blade actually is (the guard and the point, in the world) off the hand's bone and
    /// hands it to the hero, who keeps a short history of it: the light that follows a cut is that
    /// history, so it lies exactly along the blade.
    /// </summary>
    private void SampleBlade(CreatureModel m, in AnimInput a)
    {
        if (a.Owner is not Player player || _caster) return;
        int which = 0;
        // (the Rogue's second stroke is the left hand's)
        if (_rogue && a.Clip is { Length: > 6 } c && c.StartsWith("slash_") && c[6] == 'b') which = 1;
        if (BladeWorld(m, which, out var guard, out var tip)) player.SampleBlade(guard, tip);
    }

    /// <summary>The blade's guard and point in the world, read off a hand's bone (rest space: the grip is (0.016, -0.08, 0) from the hand joint, the blade runs down from it).</summary>
    public bool BladeWorld(CreatureModel m, int which, out Vector3 guard, out Vector3 tip)
    {
        guard = tip = Vector3.Zero;
        int bone = hand[which];
        if (bone < 0 || _caster) return false;
        var pose = m.Skel.GetBoneGlobalPose(bone);
        var xf = m.Skel.GlobalTransform;
        guard = xf * (pose * new Vector3(0.016f, -0.08f, 0f));
        tip = xf * (pose * new Vector3(0.016f, -0.08f - BladeLength, 0f));
        return true;
    }
}
