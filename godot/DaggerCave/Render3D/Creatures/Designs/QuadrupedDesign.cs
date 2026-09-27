using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Shared skeleton and gaits for four-legged creatures (rats, bears, the colossus, the dragon):
/// a spine from pelvis to skull with a jaw, a tail chain, and four legs whose joints bend the way
/// a dog's or a bear's do (elbows back, knees forward, hocks back).
/// </summary>
public abstract class QuadrupedDesign : CreatureDesign
{
    protected sealed class Spec
    {
        public float Floor = -0.4f;
        public float HipH = 0.35f, ShoulderH = 0.36f; // joint heights above the floor
        public float BodyLen = 0.4f;                   // hip joint to shoulder joint
        public float NeckLen = 0.14f, HeadLen = 0.2f, NeckRise = 0.06f;
        public float TailLen = 0.5f; public int TailBones = 4;
        public float LegW = 0.1f;
        public float FUpper = 0.18f, FLower = 0.17f;   // front: shoulder->elbow->wrist (paw below)
        public float HUpper = 0.16f, HLower = 0.16f;   // hind: hip->knee->hock (paw below)
        public float Paw = 0.07f;
    }

    protected Spec Q = new();
    public override float FloorY => Q.Floor;

    protected int hips, spine, chest, neck, head, jaw;
    protected int[] tail;
    protected readonly int[] fUp = new int[2], fLo = new int[2], fPaw = new int[2], hUp = new int[2], hLo = new int[2], hPaw = new int[2];
    protected Vector3 Hips, Spine, Chest, Neck, Head;
    protected readonly Vector3[] FSh = new Vector3[2], FEl = new Vector3[2], FWr = new Vector3[2], FToe = new Vector3[2];
    protected readonly Vector3[] HHp = new Vector3[2], HKn = new Vector3[2], HHk = new Vector3[2], HToe = new Vector3[2];
    protected Vector3[] TailPts;

    protected override void OnBonesBound()
    {
        hips = B("hips"); spine = B("spine"); chest = B("chest"); neck = B("neck"); head = B("head"); jaw = B("jaw");
        tail = new int[Q.TailBones];
        for (int k = 0; k < Q.TailBones; k++) tail[k] = B("tail" + k);
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            fUp[k] = B("fup" + s); fLo[k] = B("flo" + s); fPaw[k] = B("fpaw" + s);
            hUp[k] = B("hup" + s); hLo[k] = B("hlo" + s); hPaw[k] = B("hpaw" + s);
        }
    }

    protected void BuildSkeleton(Sculptor s)
    {
        var q = Q;
        Hips = new Vector3(-q.BodyLen * 0.5f, q.Floor + q.HipH + 0.04f, 0);
        Chest = new Vector3(q.BodyLen * 0.5f, q.Floor + q.ShoulderH + 0.05f, 0);
        Spine = (Hips + Chest) * 0.5f + new Vector3(0, 0.02f, 0);
        Neck = Chest + new Vector3(0.04f, 0.02f, 0);
        Head = Neck + new Vector3(q.NeckLen, q.NeckRise, 0);
        int hb = s.Bone("hips", -1, Hips);
        int sb = s.Bone("spine", hb, Spine);
        int cb = s.Bone("chest", sb, Chest);
        int nb = s.Bone("neck", cb, Neck);
        int hd = s.Bone("head", nb, Head);
        s.Bone("jaw", hd, Head + new Vector3(0.02f, -0.03f, 0));
        TailPts = new Vector3[q.TailBones + 1];
        int parent = hb;
        for (int k = 0; k <= q.TailBones; k++)
        {
            float t = k / (float)q.TailBones;
            TailPts[k] = Hips + new Vector3(-0.05f - q.TailLen * t, 0.02f - 0.12f * t * t, 0);
            if (k < q.TailBones) parent = s.Bone("tail" + k, parent, TailPts[k]);
        }
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            FSh[k] = new Vector3(Chest.X - 0.02f, q.Floor + q.ShoulderH, q.LegW * z);
            FEl[k] = FSh[k] + new Vector3(-0.04f, -q.FUpper, 0);
            FWr[k] = FEl[k] + new Vector3(0.02f, -q.FLower, 0);
            FToe[k] = new Vector3(FWr[k].X + q.Paw, q.Floor + 0.02f, FWr[k].Z);
            int fu = s.Bone("fup" + sfx, cb, FSh[k]);
            int fl = s.Bone("flo" + sfx, fu, FEl[k]);
            s.Bone("fpaw" + sfx, fl, FWr[k]);
            HHp[k] = new Vector3(Hips.X, q.Floor + q.HipH, q.LegW * z * 0.95f);
            HKn[k] = HHp[k] + new Vector3(0.06f, -q.HUpper, 0);
            HHk[k] = HKn[k] + new Vector3(-0.07f, -q.HLower, 0);
            HToe[k] = new Vector3(HHk[k].X + q.Paw * 1.1f, q.Floor + 0.02f, HHk[k].Z);
            int hu = s.Bone("hup" + sfx, hb, HHp[k]);
            int hl = s.Bone("hlo" + sfx, hu, HKn[k]);
            s.Bone("hpaw" + sfx, hl, HHk[k]);
        }
    }

    // ================================================================== motion

    /// <summary>Leg angles for one frame: swing (+ forward) and bend for each leg; spine and head.</summary>
    protected struct Stance
    {
        public float FR, FRb, FL, FLb, HR, HRb, HL, HLb; // swing and bend per leg
        public float Pitch, Arch, NeckUp, HeadUp, Jaw, HeadYaw, TailUp, TailSway;
        public Vector3 Root;
        public float Roll;
    }

    /// <summary>A gait: gallop (true) bounds, walk steps one leg at a time.</summary>
    protected static Stance Gait(float ph, float amount, bool gallop, float stride)
    {
        float Leg(float off) => MathF.Sin((ph + off) * Mathf.Tau);
        float Lift(float off) => Math.Max(0, MathF.Cos((ph + off) * Mathf.Tau));
        float[] off = gallop ? new[] { 0.5f, 0.6f, 0f, 0.1f } : new[] { 0.25f, 0.75f, 0f, 0.5f }; // FR FL HR HL
        var s = new Stance
        {
            FR = stride * Leg(off[0]) * amount, FRb = 40 * Lift(off[0]) * amount,
            FL = stride * Leg(off[1]) * amount, FLb = 40 * Lift(off[1]) * amount,
            HR = stride * Leg(off[2]) * amount, HRb = 45 * Lift(off[2]) * amount,
            HL = stride * Leg(off[3]) * amount, HLb = 45 * Lift(off[3]) * amount,
            Arch = gallop ? 12 * MathF.Sin(ph * Mathf.Tau) * amount : 0,
            Root = new Vector3(0, (gallop ? 0.04f : 0.012f) * MathF.Abs(MathF.Sin(ph * Mathf.Tau * (gallop ? 1 : 2))) * amount, 0),
            NeckUp = gallop ? -6 * amount : 0, TailSway = 10 * MathF.Sin(ph * Mathf.Tau) * amount,
        };
        return s;
    }

    protected void Apply(CreaturePose p, Stance s)
    {
        p.Set(hips, 0, 0, s.Pitch + s.Roll);
        p.Move(hips, s.Root);
        p.Set(spine, 0, 0, s.Arch * 0.5f);
        p.Set(chest, 0, 0, s.Arch * 0.5f);
        p.Set(neck, 0, s.HeadYaw * 0.4f, s.NeckUp - s.Pitch * 0.5f);
        p.Set(head, 0, s.HeadYaw * 0.6f, s.HeadUp - s.Pitch * 0.3f);
        p.Set(jaw, 0, 0, -s.Jaw);
        for (int k = 0; k < tail.Length; k++)
            p.Set(tail[k], 0, s.TailSway * (0.5f + k * 0.3f), s.TailUp * (k == 0 ? 1 : 0.35f));
        // front legs: the upper swings, the lower folds back when lifted, the paw stays level
        FrontLeg(p, 0, s.FR, s.FRb);
        FrontLeg(p, 1, s.FL, s.FLb);
        HindLeg(p, 0, s.HR, s.HRb);
        HindLeg(p, 1, s.HL, s.HLb);
    }

    private void FrontLeg(CreaturePose p, int k, float swing, float bend)
    {
        p.Set(fUp[k], 0, 0, swing);
        p.Set(fLo[k], 0, 0, -bend);
        p.Set(fPaw[k], 0, 0, -(swing - bend) * 0.7f + bend * 0.6f);
    }

    private void HindLeg(CreaturePose p, int k, float swing, float bend)
    {
        p.Set(hUp[k], 0, 0, swing + bend * 0.4f);
        p.Set(hLo[k], 0, 0, bend * 0.8f);
        p.Set(hPaw[k], 0, 0, -(swing + bend * 1.2f) * 0.8f);
    }

    protected static Stance Blend(Stance a, Stance b, float t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        return new Stance
        {
            FR = Mathf.Lerp(a.FR, b.FR, t), FRb = Mathf.Lerp(a.FRb, b.FRb, t), FL = Mathf.Lerp(a.FL, b.FL, t), FLb = Mathf.Lerp(a.FLb, b.FLb, t),
            HR = Mathf.Lerp(a.HR, b.HR, t), HRb = Mathf.Lerp(a.HRb, b.HRb, t), HL = Mathf.Lerp(a.HL, b.HL, t), HLb = Mathf.Lerp(a.HLb, b.HLb, t),
            Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t), Arch = Mathf.Lerp(a.Arch, b.Arch, t), NeckUp = Mathf.Lerp(a.NeckUp, b.NeckUp, t),
            HeadUp = Mathf.Lerp(a.HeadUp, b.HeadUp, t), Jaw = Mathf.Lerp(a.Jaw, b.Jaw, t), HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t),
            TailUp = Mathf.Lerp(a.TailUp, b.TailUp, t), TailSway = Mathf.Lerp(a.TailSway, b.TailSway, t),
            Root = a.Root.Lerp(b.Root, t), Roll = Mathf.Lerp(a.Roll, b.Roll, t),
        };
    }

    protected static float Phase(CreaturePose p, in AnimInput a, float strideLen)
    {
        ref float ph = ref p.Vars[0];
        ph = (ph + MathF.Abs(a.Vel.X) / strideLen * a.Dt) % 1f;
        return ph;
    }

    /// <summary>Topples onto its side, legs stiffening (k 0..1).</summary>
    protected Stance Dead(Stance s, float k, float drop)
    {
        s.Roll = 0;
        s.Root = new Vector3(0, -drop * k, 0);
        s.FR = s.FL = 25 * k; s.FRb = s.FLb = 10 * k; s.HR = s.HL = -30 * k; s.HRb = s.HLb = 5 * k;
        s.NeckUp = -25 * k; s.HeadUp = -15 * k; s.Jaw = 25 * k; s.TailUp = -10 * k;
        return s;
    }

    // ================================================================== sculpt helpers

    /// <summary>Standard legs along the bones: the thickness at the top and the paw size.</summary>
    protected void Legs(Sculptor s, float fr, float hr, Color col, Mat mat, Color pawCol, float bump = 0f)
    {
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            int fu = s["fup" + sfx], fl = s["flo" + sfx], fp = s["fpaw" + sfx], hu = s["hup" + sfx], hl = s["hlo" + sfx], hp = s["hpaw" + sfx];
            s.Limb(fu, FSh[k] + new Vector3(0, 0.05f, -0.02f * (k == 0 ? 1 : -1)), FEl[k], fr * 1.35f, fr, col, mat, 0.03f, bump);
            s.Limb(fl, FEl[k], FWr[k], fr, fr * 0.75f, col, mat, 0.02f, bump);
            s.Limb(fp, FWr[k], FToe[k], fr * 0.8f, fr * 0.6f, pawCol, mat, 0.015f);
            s.Limb(hu, HHp[k] + new Vector3(0, 0.05f, 0), HKn[k], hr * 1.5f, hr, col, mat, 0.035f, bump);
            s.Limb(hl, HKn[k], HHk[k], hr * 0.9f, hr * 0.65f, col, mat, 0.02f, bump);
            s.Limb(hp, HHk[k], HToe[k], hr * 0.75f, hr * 0.6f, pawCol, mat, 0.015f);
        }
    }

    /// <summary>Hooked claws on every paw.</summary>
    protected void Claws(Sculptor s, int count, float len, float r, Color col)
    {
        for (int k = 0; k < 2; k++)
        {
            string sfx = k == 0 ? "_r" : "_l";
            foreach (var (bone, toe) in new[] { (s["fpaw" + sfx], FToe[k]), (s["hpaw" + sfx], HToe[k]) })
                for (int c = 0; c < count; c++)
                {
                    var at = toe + new Vector3(0, 0.005f, (c - (count - 1) * 0.5f) * r * 2.6f);
                    s.Horn(bone, at, at + new Vector3(len, -len * 0.5f, 0), r, col, Mat.Claw, Vector3.Up, len * 0.2f);
                }
        }
    }
}
