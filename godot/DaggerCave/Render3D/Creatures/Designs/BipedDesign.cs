using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Shared skeleton, body parameters and motion for two-legged creatures (goblins, skeletons,
/// sporelings, brutes, golems, wraiths). A design sets its proportions, sculpts its own look on
/// the standard bones, and picks poses per clip from the building blocks here.
/// </summary>
public abstract class BipedDesign : CreatureDesign
{
    /// <summary>Proportions in metres (model space: +X forward, floor at FloorY).</summary>
    protected sealed class Spec
    {
        public float Floor = -0.56f;
        public float HipH = 0.46f;            // hip joint above the floor
        public float Spine = 0.1f, Chest = 0.14f, Neck = 0.1f, HeadUp = 0.06f;
        public float Hunch = 0f;              // how far the chest and head sit forward
        public float ShoulderW = 0.14f, HipW = 0.08f;
        public float UpperArm = 0.2f, ForeArm = 0.2f, Hand = 0.08f;
        public float Thigh = 0.23f, Shin = 0.21f, Foot = 0.12f;
    }

    protected Spec P = new();
    public override float FloorY => P.Floor;

    protected int hips, spine, chest, neck, head, jaw;
    protected readonly int[] uarm = new int[2], farm = new int[2], hand = new int[2], thigh = new int[2], shin = new int[2], foot = new int[2];
    // rest joint positions (model space), filled by BuildSkeleton
    protected Vector3 Hips, Spine, Chest, Neck, Head, JawP;
    protected readonly Vector3[] Shoulder = new Vector3[2], Elbow = new Vector3[2], Wrist = new Vector3[2], HipJ = new Vector3[2], Knee = new Vector3[2], Ankle = new Vector3[2], Toe = new Vector3[2];

    protected override void OnBonesBound()
    {
        hips = B("hips"); spine = B("spine"); chest = B("chest"); neck = B("neck"); head = B("head"); jaw = B("jaw");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            uarm[k] = B("uarm" + s); farm[k] = B("farm" + s); hand[k] = B("hand" + s);
            thigh[k] = B("thigh" + s); shin[k] = B("shin" + s); foot[k] = B("foot" + s);
        }
    }

    /// <summary>Creates the standard bones from <see cref="P"/> (arms hang, legs straight).</summary>
    protected void BuildSkeleton(Sculptor s, bool withJaw = true)
    {
        var p = P;
        Hips = new Vector3(0, p.Floor + p.HipH, 0);
        Spine = Hips + new Vector3(p.Hunch * 0.3f, p.Spine, 0);
        Chest = Spine + new Vector3(p.Hunch * 0.5f, p.Chest, 0);
        Neck = Chest + new Vector3(p.Hunch * 0.6f, p.Neck, 0);
        Head = Neck + new Vector3(p.Hunch * 0.3f, p.HeadUp, 0);
        JawP = Head + new Vector3(0.02f, -0.02f, 0);
        int hb = s.Bone("hips", -1, Hips);
        int sb = s.Bone("spine", hb, Spine);
        int cb = s.Bone("chest", sb, Chest);
        int nb = s.Bone("neck", cb, Neck);
        int hd = s.Bone("head", nb, Head);
        if (withJaw) s.Bone("jaw", hd, JawP);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            Shoulder[k] = Chest + new Vector3(p.Hunch * 0.2f, p.Chest * 0.55f, p.ShoulderW * z);
            Elbow[k] = Shoulder[k] + new Vector3(0, -p.UpperArm, 0.01f * z);
            Wrist[k] = Elbow[k] + new Vector3(0, -p.ForeArm, 0);
            int ua = s.Bone("uarm" + sfx, cb, Shoulder[k]);
            int fa = s.Bone("farm" + sfx, ua, Elbow[k]);
            s.Bone("hand" + sfx, fa, Wrist[k]);
            HipJ[k] = Hips + new Vector3(0, -0.02f, p.HipW * z);
            Knee[k] = HipJ[k] + new Vector3(0.01f, -p.Thigh, 0);
            Ankle[k] = Knee[k] + new Vector3(-0.01f, -p.Shin, 0);
            Toe[k] = new Vector3(Ankle[k].X + p.Foot, p.Floor + 0.02f, Ankle[k].Z);
            int th = s.Bone("thigh" + sfx, hb, HipJ[k]);
            int sh = s.Bone("shin" + sfx, th, Knee[k]);
            s.Bone("foot" + sfx, sh, Ankle[k]);
        }
    }

    // ================================================================== poses

    protected struct Body
    {
        public float Lean, Twist, Bank, HeadPitch, HeadYaw, HeadRoll, Jaw, Roll;
        public Vector3 Root;
        public float SR, AR, ER, WR, SL, AL, EL, WL;
        public float HR, KR, HRx, HL, KL, HLx;
    }

    protected static Body Mix(Body a, Body b, float t)
    {
        if (t <= 0f) return a;
        if (t >= 1f) return b;
        Body r;
        r.Lean = Mathf.Lerp(a.Lean, b.Lean, t); r.Twist = Mathf.Lerp(a.Twist, b.Twist, t); r.Bank = Mathf.Lerp(a.Bank, b.Bank, t);
        r.HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t); r.HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t); r.HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t);
        r.Jaw = Mathf.Lerp(a.Jaw, b.Jaw, t); r.Roll = Mathf.Lerp(a.Roll, b.Roll, t);
        r.Root = a.Root.Lerp(b.Root, t);
        r.SR = Mathf.Lerp(a.SR, b.SR, t); r.AR = Mathf.Lerp(a.AR, b.AR, t); r.ER = Mathf.Lerp(a.ER, b.ER, t); r.WR = Mathf.Lerp(a.WR, b.WR, t);
        r.SL = Mathf.Lerp(a.SL, b.SL, t); r.AL = Mathf.Lerp(a.AL, b.AL, t); r.EL = Mathf.Lerp(a.EL, b.EL, t); r.WL = Mathf.Lerp(a.WL, b.WL, t);
        r.HR = Mathf.Lerp(a.HR, b.HR, t); r.KR = Mathf.Lerp(a.KR, b.KR, t); r.HRx = Mathf.Lerp(a.HRx, b.HRx, t);
        r.HL = Mathf.Lerp(a.HL, b.HL, t); r.KL = Mathf.Lerp(a.KL, b.KL, t); r.HLx = Mathf.Lerp(a.HLx, b.HLx, t);
        return r;
    }

    /// <summary>Standing: breathing, a slight crouch, weight on one leg, arms loose.</summary>
    protected virtual Body Idle(float time)
    {
        float b = MathF.Sin(time * 2f);
        return new Body
        {
            Lean = 6 + b * 1.5f, HeadPitch = -4 + b * 2f, Jaw = 4 + 3 * MathF.Sin(time * 1.3f),
            Root = new Vector3(0, -0.02f + b * 0.004f, 0),
            SR = 8 + b * 3, ER = 20, AR = 10, SL = 4 - b * 3, EL = 18, AL = 10,
            HR = 12, KR = 18, HRx = 5, HL = -6, KL = 12, HLx = 5,
        };
    }

    /// <summary>A walk or run cycle (phase 0..1), blended in by amount.</summary>
    protected virtual Body Walk(float phase, float amount, bool run, float time)
    {
        float sn = MathF.Sin(phase * Mathf.Tau), cs = MathF.Cos(phase * Mathf.Tau);
        float stride = run ? 40f : 26f;
        var o = new Body
        {
            Lean = run ? 16 : 8, Twist = (run ? 10 : 6) * sn, HeadPitch = run ? -8 : -2, Jaw = 6,
            Root = new Vector3(0, (run ? -0.04f : -0.015f) + (run ? 0.035f : 0.018f) * MathF.Abs(sn), 0),
            SR = 8 - stride * 0.7f * sn, ER = run ? 60 : 25, AR = 10,
            SL = 8 + stride * 0.7f * sn, EL = run ? 60 : 25, AL = 10,
            HR = 8 + stride * sn, KR = 15 + (run ? 70 : 40) * Math.Max(0, cs) + 8 * Math.Max(0, -sn),
            HL = 8 - stride * sn, KL = 15 + (run ? 70 : 40) * Math.Max(0, -cs) + 8 * Math.Max(0, sn),
            HRx = 4, HLx = 4,
        };
        return Mix(Idle(time), o, amount);
    }

    /// <summary>In the air: legs tuck on the way up, reach down on the way down, arms up.</summary>
    protected virtual Body Air(float vy)
    {
        var up = new Body { Lean = 6, SR = 50, ER = 40, AR = 15, SL = 60, EL = 40, AL = 15, HR = 45, KR = 75, HL = 10, KL = 40, HRx = 6, HLx = 6, Jaw = 10 };
        var down = new Body { Lean = 0, SR = 100, ER = 25, AR = 30, SL = 110, EL = 25, AL = 30, HR = 20, KR = 30, HL = 0, KL = 20, HRx = 8, HLx = 8, Jaw = 20 };
        return Mix(up, down, W3.SmoothStep(3f, -4f, vy));
    }

    /// <summary>Recoil from a blow.</summary>
    protected static Body Hurt(Body o, float t)
    {
        float k = Key(t, (0, 0), (0.2f, 1), (1, 0.1f));
        o.Lean -= 24 * k; o.HeadPitch -= 25 * k; o.Twist += 12 * k; o.Jaw += 25 * k;
        o.SR += 35 * k; o.AR += 30 * k; o.SL += 45 * k; o.AL += 30 * k;
        o.Root += new Vector3(-0.04f * k, -0.03f * k, 0);
        o.KR += 20 * k; o.KL += 20 * k;
        return o;
    }

    /// <summary>Collapse: knees give, then the body topples backward to the floor.</summary>
    protected Body Die(float t, float lieHeight)
    {
        float k1 = W3.SmoothStep(0f, 0.3f, t), k2 = W3.SmoothStep(0.25f, 0.8f, t);
        var o = Idle(0);
        o.Lean = -12 * k1; o.KR += 60 * k1; o.KL += 70 * k1; o.HR += 30 * k1; o.HL += 30 * k1; o.Jaw = 30 * k1;
        o.Roll = 86 * k2;
        o.Root = new Vector3(-0.1f * k2, -0.05f * k1 - (Hips.Y - P.Floor - lieHeight) * k2, 0);
        o.SR += 70 * k2; o.AR += 30 * k2; o.SL += 60 * k2; o.AL += 30 * k2;
        o.HeadPitch = -30 * k2; o.HeadRoll = 20 * k2;
        o.KR = Mathf.Lerp(o.KR, 25, k2); o.KL = Mathf.Lerp(o.KL, 55, k2);
        return o;
    }

    /// <summary>A two-handed or one-handed strike from above: wind-up raises the arm, the strike drives it down and through.</summary>
    protected static Body Chop(Body o, float windup, float strike, bool rightOnly = true)
    {
        float up = windup * (1 - strike);
        o.SR = Mathf.Lerp(o.SR, 165, up); o.ER = Mathf.Lerp(o.ER, 70, up);
        o.Lean = Mathf.Lerp(o.Lean, -10, up);
        o.Twist = Mathf.Lerp(o.Twist, -20, up);
        if (strike > 0f)
        {
            o.SR = Mathf.Lerp(165, 20, strike); o.ER = Mathf.Lerp(70, 5, strike);
            o.Lean = Mathf.Lerp(-10, 22, strike); o.Twist = Mathf.Lerp(-20, 25, strike);
            o.HR = Mathf.Lerp(o.HR, 35, strike); o.KR = Mathf.Lerp(o.KR, 40, strike);
            o.HL = Mathf.Lerp(o.HL, -20, strike); o.KL = Mathf.Lerp(o.KL, 15, strike);
            o.Root.Y -= 0.05f * strike;
        }
        if (!rightOnly) { o.SL = o.SR; o.EL = o.ER; }
        return o;
    }

    protected void Apply(CreaturePose p, Body o)
    {
        p.Set(hips, 0, o.Twist * 0.25f, o.Roll);
        p.Move(hips, o.Root);
        p.Set(spine, o.Bank * 0.5f, o.Twist * 0.35f, -o.Lean * 0.45f);
        p.Set(chest, o.Bank * 0.5f, o.Twist * 0.4f, -o.Lean * 0.55f);
        p.Set(neck, 0, o.HeadYaw * 0.4f, o.Lean * 0.3f - o.HeadPitch * 0.4f);
        p.Set(head, o.HeadRoll, o.HeadYaw * 0.6f, o.Lean * 0.45f - o.HeadPitch * 0.6f);
        p.Set(jaw, 0, 0, -o.Jaw);
        p.Rot[uarm[0]] = CreaturePose.Q(-o.AR, 0, 0) * CreaturePose.Q(0, 0, o.SR);
        p.Set(farm[0], 0, 0, o.ER);
        p.Set(hand[0], 0, 0, o.WR);
        p.Rot[uarm[1]] = CreaturePose.Q(o.AL, 0, 0) * CreaturePose.Q(0, 0, o.SL);
        p.Set(farm[1], 0, 0, o.EL);
        p.Set(hand[1], 0, 0, o.WL);
        p.Rot[thigh[0]] = CreaturePose.Q(-o.HRx, 0, 0) * CreaturePose.Q(0, 0, o.HR);
        p.Set(shin[0], 0, 0, -o.KR);
        p.Set(foot[0], 0, 0, -(o.HR - o.KR) * 0.8f);
        p.Rot[thigh[1]] = CreaturePose.Q(o.HLx, 0, 0) * CreaturePose.Q(0, 0, o.HL);
        p.Set(shin[1], 0, 0, -o.KL);
        p.Set(foot[1], 0, 0, -(o.HL - o.KL) * 0.8f);
    }

    /// <summary>The gait phase, advanced by ground speed (metres per full stride pair).</summary>
    protected static float Gait(CreaturePose p, in AnimInput a, float strideLen)
    {
        ref float ph = ref p.Vars[0];
        if (a.OnFloor || a.InWater) ph = (ph + MathF.Abs(a.Vel.X) / strideLen * a.Dt) % 1f;
        return ph;
    }

    // ================================================================== sculpt helpers

    /// <summary>Standard limbs on the standard bones (skin, or whatever the colours say).</summary>
    protected void Limbs(Sculptor s, float armR, float legR, Color arm, Color leg, Mat armMat, Mat legMat, float handR = 0f, Color? handCol = null, float footR = 0f)
    {
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], hd = s["hand" + sfx];
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(ua, Shoulder[k], Elbow[k], armR * 1.15f, armR * 0.9f, arm, armMat, 0.02f);
            s.Limb(fa, Elbow[k], Wrist[k], armR * 0.9f, armR * 0.7f, arm, armMat, 0.015f);
            if (handR > 0) s.Limb(hd, Wrist[k], Wrist[k] + new Vector3(0.01f, -P.Hand, 0), handR, handR * 0.8f, handCol ?? arm, armMat, 0.012f);
            s.Limb(th, HipJ[k], Knee[k], legR * 1.2f, legR * 0.9f, leg, legMat, 0.02f);
            s.Limb(sh, Knee[k], Ankle[k], legR * 0.9f, legR * 0.7f, leg, legMat, 0.015f);
            s.Limb(ft, Ankle[k], Toe[k], footR > 0 ? footR : legR * 0.75f, (footR > 0 ? footR : legR * 0.75f) * 0.7f, leg, legMat, 0.015f);
        }
    }
}
