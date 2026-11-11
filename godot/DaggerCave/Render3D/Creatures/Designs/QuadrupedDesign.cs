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

    /// <summary>
    /// A second attack's wind-up on four legs, over the clip's pose. Lunge and Pounce: the chest drops between the forelegs and the
    /// haunches fold under, the tail straight up, the head low and fixed on you; it lets go long, forelegs reaching. Slam: it
    /// rears onto its hind legs, forepaws lifted high, head thrown back and jaws wide; down come the paws. Spit: it braces, draws
    /// its head back and up with the throat full, then whips the head forward, jaws wide. The rest: braces and bristles.
    /// </summary>
    public override void Windup(CreaturePose p, in WindupPose w)
    {
        float c = w.Coil, s = w.Snap, q = w.Tremble;
        float pitch = 0, arch = 0, neckUp = 0, headUp = 0, jawOpen = 0, tailUp = 0, f = 0, fb = 0, h = 0, hb = 0;
        var root = Vector3.Zero;
        switch (w.Kind)
        {
            case Move2.Lunge: case Move2.Pounce: case Move2.Dive: case Move2.Kamikaze:
                // (play bow: front down, rump up; the hind legs loaded)
                pitch = -13f * c + 10f * s; arch = -8f * c + 12f * s; neckUp = 16f * c - 8f * s; headUp = 8f * c; jawOpen = 6f * c + 30f * s;
                tailUp = 46f * c - 30f * s; f = 22f * c + 46f * s; fb = 54f * c - 10f * s; h = -6f * c - 44f * s; hb = 34f * c - 10f * s;
                root = new Vector3(-0.05f * c + 0.05f * s, -0.05f * c, 0);
                tailUp += 6f * q;
                break;
            case Move2.Slam:
                // (reared up on the hind legs, forepaws high)
                pitch = 50f * c - 6f * s; arch = 10f * c - 14f * s; neckUp = -30f * c + 16f * s; headUp = 10f * c; jawOpen = 30f * c + 6f * s;
                tailUp = -18f * c; f = 78f * c + 10f * s; fb = 46f * c - 30f * s; h = -44f * c; hb = 16f * c + 22f * s;
                root = new Vector3(-0.03f * c, 0.02f * c - 0.03f * s, 0);
                f += 5f * q;
                break;
            case Move2.Spit: case Move2.Volley:
                pitch = 7f * c - 7f * s; neckUp = 30f * c - 26f * s; headUp = 20f * c - 14f * s; jawOpen = 5f * c + 40f * s;
                tailUp = 20f * c; f = -8f * c; h = -6f * c; hb = 14f * c; root = new Vector3(-0.035f * c + 0.03f * s, 0, 0);
                headUp += 3f * q;
                break;
            case Move2.Burst: case Move2.Spines: case Move2.Spin: case Move2.Shatter:
                arch = 16f * c - 12f * s; neckUp = -18f * c + 22f * s; jawOpen = 26f * s; tailUp = 30f * c;
                fb = 20f * c; hb = 20f * c; root = new Vector3(0, -0.03f * c, 0);
                break;
            default: return;
        }
        p.Add(hips, 0, 0, pitch);
        p.Move(hips, root);
        p.Add(spine, 0, 0, arch * 0.5f);
        p.Add(chest, 0, 0, arch * 0.5f);
        p.Add(neck, 0, 0, neckUp - pitch * 0.5f);
        p.Add(head, 0, 0, headUp - pitch * 0.3f);
        p.Add(jaw, 0, 0, -jawOpen);
        for (int k = 0; k < tail.Length; k++) p.Add(tail[k], 0, 0, tailUp * (k == 0 ? 1 : 0.35f));
        for (int k = 0; k < 2; k++)
        {
            p.Add(fUp[k], 0, 0, f - pitch); p.Add(fLo[k], 0, 0, -fb); p.Add(fPaw[k], 0, 0, -(f - fb) * 0.7f + fb * 0.6f);
            p.Add(hUp[k], 0, 0, h - pitch + hb * 0.4f); p.Add(hLo[k], 0, 0, hb * 0.8f); p.Add(hPaw[k], 0, 0, -(h + hb * 1.2f) * 0.8f);
        }
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

    /// <summary>
    /// Legs built from their masses, not one tube a bone: in front the shoulder blade over the joint, the triceps' bulk behind the
    /// upper arm, the elbow's point, the forearm full below the elbow and slimming to the wrist, and a broad, flat paw with its
    /// toe pads along the front; behind, the haunch's great round mass, the thigh, the muscle behind the lower thigh, the hock's
    /// point and a long flat paw. <paramref name="fr"/> and <paramref name="hr"/> are the front and hind legs' thickness at the
    /// elbow and the knee.
    /// </summary>
    protected void MuscledLegs(Sculptor s, float fr, float hr, Color col, Mat mat, Color pawCol, Color padCol, float bump = 0f, int toes = 5)
    {
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int fu = s["fup" + sfx], fl = s["flo" + sfx], fp = s["fpaw" + sfx], hu = s["hup" + sfx], hl = s["hlo" + sfx], hp = s["hpaw" + sfx];
            // ---- the foreleg
            var fUpMid = (FSh[k] + FEl[k]) * 0.5f;
            s.Egg(fu, FSh[k] + new Vector3(0.01f, 0.08f * (fr / 0.1f), -0.01f * z), new Vector3(1.45f, 1.75f, 1.05f) * fr, col, mat, 0.05f, bump: bump);
            s.Limb(fu, FSh[k], FEl[k], fr * 1.3f, fr * 0.95f, col, mat, 0.04f, bump);
            s.Egg(fu, fUpMid + new Vector3(-0.42f * fr, 0, 0), new Vector3(0.95f, 1.35f, 0.9f) * fr, col, mat, 0.04f, bump: bump);
            s.Egg(fl, FEl[k] + new Vector3(-0.5f * fr, 0.04f * fr, 0), new Vector3(0.55f, 0.62f, 0.6f) * fr, col, mat, 0.025f, bump: bump);
            s.Limb(fl, FEl[k], FWr[k], fr * 0.98f, fr * 0.68f, col, mat, 0.03f, bump);
            s.Egg(fl, FEl[k] + (FWr[k] - FEl[k]) * 0.3f + new Vector3(0.22f * fr, 0, 0), new Vector3(0.78f, 1.25f, 0.8f) * fr, col, mat, 0.035f, bump: bump);
            Paw(s, fp, FWr[k], FToe[k], fr * 0.95f, z, pawCol, padCol, mat, toes);
            // ---- the hind leg
            s.Egg(hu, HHp[k] + new Vector3(-0.02f, -0.02f, 0.01f * z), new Vector3(2.0f, 2.05f, 1.35f) * hr, col, mat, 0.06f, bump: bump);
            s.Limb(hu, HHp[k], HKn[k], hr * 1.5f, hr * 1.0f, col, mat, 0.045f, bump);
            s.Egg(hl, HKn[k] + (HHk[k] - HKn[k]) * 0.32f + new Vector3(-0.38f * hr, 0, 0), new Vector3(0.82f, 1.2f, 0.8f) * hr, col, mat, 0.035f, bump: bump);
            s.Limb(hl, HKn[k], HHk[k], hr * 0.92f, hr * 0.6f, col, mat, 0.03f, bump);
            s.Egg(hp, HHk[k] + new Vector3(-0.36f * hr, 0.05f * hr, 0), new Vector3(0.45f, 0.52f, 0.5f) * hr, col, mat, 0.02f, bump: bump);
            Paw(s, hp, HHk[k], HToe[k], hr * 0.82f, z, pawCol, padCol, mat, toes);
        }
    }

    /// <summary>A broad, flat paw from the wrist (or hock) to the toes, wider than deep, with toe pads along its front edge.</summary>
    private static void Paw(Sculptor s, int bone, Vector3 wrist, Vector3 toe, float r, float z, Color col, Color pad, Mat mat, int toes)
    {
        var mid = (wrist + toe) * 0.5f;
        float len = (toe - wrist).Length();
        s.Limb(bone, wrist, mid + new Vector3(0, -0.15f * r, 0), r * 0.75f, r * 0.85f, col, mat, 0.025f);
        s.Egg(bone, new Vector3(mid.X + 0.1f * len, toe.Y + 0.42f * r, mid.Z), new Vector3(len * 0.55f, 0.48f * r, 1.12f * r), col, mat, 0.03f);
        for (int t = 0; t < toes; t++)
        {
            float f = toes == 1 ? 0f : t / (float)(toes - 1) - 0.5f;
            var at = new Vector3(toe.X - 0.12f * r * MathF.Abs(f) * 2f, toe.Y + 0.3f * r, toe.Z + f * 1.9f * r);
            s.Egg(bone, at, new Vector3(0.36f, 0.3f, 0.3f) * r, col, mat, 0.012f);
        }
        s.Egg(bone, new Vector3(mid.X + 0.12f * len, toe.Y + 0.1f * r, mid.Z), new Vector3(len * 0.32f, 0.12f * r, 0.7f * r), pad, Mat.Skin, 0.012f);
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
