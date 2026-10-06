using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman, Bran, sculpted from the concept art: a broad, weathered warrior (short dark hair falling over a heavy
/// brow, stubble on a square jaw, a slate cowl at the neck, a shaggy fur mantle on his left shoulder, a sleeveless dark
/// tunic under crossing leather straps and a belt, leather bracers, fingerless gloves, a torn navy tabard, baggy trousers
/// tucked into fur-cuffed boots, a ragged slate cloak behind) and his longsword. Built from the same signed-distance
/// primitives as the other heroes, on the same rig (so every pose and stroke is shared), at a finer grid, with the
/// texture painted over the finished skin (stubble, brow shadow, scuffed leather, wear on the cloth, fur tips).
/// </summary>
public sealed partial class HeroDesign
{
    // the art's palette (cleaned a little)
    private static readonly Color BrSkin = new(0.55f, 0.37f, 0.27f);
    private static readonly Color BrHair = new(0.095f, 0.07f, 0.055f);
    private static readonly Color BrNavy = new(0.12f, 0.16f, 0.27f);
    private static readonly Color BrSlate = new(0.27f, 0.33f, 0.45f);
    private static readonly Color BrTunic = new(0.105f, 0.095f, 0.11f);
    private static readonly Color BrTunicBrown = new(0.2f, 0.135f, 0.095f);
    private static readonly Color BrLeather = new(0.3f, 0.185f, 0.105f);
    private static readonly Color BrLeatherDk = new(0.16f, 0.1f, 0.065f);
    private static readonly Color BrTrouser = new(0.11f, 0.105f, 0.115f);
    private static readonly Color BrFur = new(0.8f, 0.76f, 0.68f);
    private static readonly Color BrBoot = new(0.2f, 0.135f, 0.095f);
    private static readonly Color BrGlove = new(0.15f, 0.1f, 0.075f);
    private static readonly Color BrMetal = new(0.58f, 0.6f, 0.63f);

    /// <summary>A ring round a vertical axis (a belt, a collar, a cuff, a strap); <paramref name="tilt"/> (metres of rise across its width) lifts one side.</summary>
    private static void Ring(Sculptor s, int bone, Vector3 c, float rx, float rz, float r, Color col, Mat mat, float blend = 0.006f, float bump = 0f, float tilt = 0f)
        => s.Ring(bone, c, rx, rz, r, col, mat, blend, tilt == 0f ? 0f : Mathf.RadToDeg(MathF.Atan2(tilt, rz)), bump);

    private const Mat HairMat = Mat.Fur;
    private static Color BrHairCol(float n1, float n2)
    {
        float streak = Sm(-0.3f, 0.5f, n2);
        return BrHair.Lerp(new Color(0.22f, 0.15f, 0.1f), streak * 0.55f).Darkened(Mathf.Clamp(-n1, 0f, 1f) * 0.15f);
    }

    /// <summary>His skin at a point: the stubble on the jaw, ruddy cheeks, shadow about the eyes, the arms weathered.</summary>
    private static Color BrFace(Vector3 p, Color c, float n1, float n2)
    {
        c = c.Lerp(BrSkin, 0.8f);
        if (p.Y > 0.58f && p.Y < 0.9f && p.X > -0.02f)
        {
            float lowFace = Sm(0.715f, 0.67f, p.Y);
            float aroundMouth = Sm(0.075f, 0.035f, MathF.Sqrt(MathF.Pow((p.Y - 0.668f) * 1.4f, 2) + p.Z * p.Z * 0.7f));
            float jaw = Mathf.Max(lowFace, aroundMouth) * Sm(0.0f, 0.05f, p.X);
            float cheek = Sm(0.085f, 0.06f, MathF.Abs(p.Z)) * Sm(0.69f, 0.715f, p.Y) * Sm(0.74f, 0.72f, p.Y);
            float speck = Sm(-0.15f, 0.35f, n2);
            c = c.Lerp(new Color(0.16f, 0.12f, 0.1f), jaw * 0.5f * (0.6f + 0.4f * speck));
            c = c.Lerp(new Color(0.5f, 0.26f, 0.19f), cheek * 0.3f);
            float sx = Sm(0.045f, 0.012f, MathF.Min(MathF.Sqrt(MathF.Pow(p.X - 0.108f, 2) + MathF.Pow(p.Y - 0.762f, 2) + MathF.Pow(MathF.Abs(p.Z) - 0.033f, 2)), 0.1f));
            c = c.Darkened(sx * 0.3f);
        }
        c = c.Darkened(Mathf.Clamp(-n1, 0f, 1f) * 0.12f).Lightened(Mathf.Clamp(n1, 0f, 1f) * 0.05f);
        if (p.Y < 0.5f) c = c.Lerp(new Color(0.46f, 0.31f, 0.23f), 0.3f);   // the arms, a little sun-worn
        return c;
    }

    /// <summary>A thin tube laid along a path of points on the skin (a brow, a lid, a lip), bound to a bone, of the given radius at each point.</summary>
    private static void Strand(Sculptor s, int bone, Vector3[] path, float[] radii, Color col, Mat mat)
    {
        var mb = new MeshBuilder();
        mb.Tube(path, radii, 7, col, capStart: true);
        var binds = new (int, int, float)[mb.Count];
        for (int k = 0; k < binds.Length; k++) binds[k] = (bone, bone, 1f);
        s.Skinned(mb, binds, mat);
    }

    private static float Sm(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

    private static Vector3 Lerp3(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    /// <summary>How far forward the torso's front reaches at height y and sideways z (the chest and waist eggs).</summary>
    private static float TorsoFront(float y, float z)
    {
        static float Egg(float cx, float cy, float rx, float ry, float rz, float y, float z)
        {
            float k = 1f - MathF.Pow((y - cy) / ry, 2) - MathF.Pow(z / rz, 2);
            return k > 0 ? cx + rx * MathF.Sqrt(k) : float.NegativeInfinity;
        }
        float f = Math.Max(Egg(0.012f, 0.35f, 0.14f, 0.13f, 0.205f, y, z), Egg(0.005f, 0.17f, 0.125f, 0.115f, 0.158f, y, z));
        return float.IsNegativeInfinity(f) ? 0.1f : f;
    }

    private void SculptBran(Sculptor s)
    {
        var rng = new Random(77);
        int hipsB = s["hips"], spineB = s["spine"], chestB = s["chest"], neckB = s["neck"], headB = s["head"];
        int c0 = s["cape0"], c1 = s["cape1"], c2 = s["cape2"], c3 = s["cape3"];
        int s0 = s["scarf0"], s1 = s["scarf1"], s2 = s["scarf2"];

        // ================================================================ torso
        s.Egg(hipsB, new(0, 0.03f, 0), new(0.125f, 0.1f, 0.155f), BrTrouser, Mat.Cloth, 0.04f);
        s.Egg(spineB, new(0.005f, 0.17f, 0), new(0.12f, 0.115f, 0.15f), BrTunic, Mat.Cloth, 0.05f);
        s.Egg(chestB, new(0.012f, 0.35f, 0), new(0.135f, 0.13f, 0.185f), BrTunic, Mat.Cloth, 0.05f);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            // pectorals, the latissimus' flare and the broad back
            s.Egg(chestB, new(0.052f, 0.375f, 0.088f * z), new(0.08f, 0.06f, 0.082f), BrTunic, Mat.Cloth, 0.03f);
            s.Egg(chestB, new(-0.02f, 0.33f, 0.135f * z), new(0.075f, 0.11f, 0.06f), BrTunic, Mat.Cloth, 0.04f);
            s.Egg(chestB, new(-0.045f, 0.405f, 0.09f * z), new(0.095f, 0.085f, 0.105f), BrTunic, Mat.Cloth, 0.04f);
        }
        // the neckline, and the thick neck rising out of it
        s.Egg(chestB, new(0.1f, 0.455f, 0), new(0.045f, 0.055f, 0.07f), BrSkin, Mat.Skin, 0.03f, bump: 0.0012f);
        s.Limb(neckB, new(0.012f, 0.47f, 0), new(0.028f, 0.65f, 0), 0.066f, 0.056f, BrSkin, Mat.Skin, 0.035f, 0.0012f);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            // trapezius, sloping to the shoulders
            s.Limb(chestB, new(-0.01f, 0.495f, 0.045f * z), new(-0.005f, 0.46f, 0.165f * z), 0.05f, 0.045f, BrTunic, Mat.Cloth, 0.03f);
        }

        // ================================================================ head (a man's: about a seventh of his height)
        var skin = BrSkin;
        s.Egg(headB, new(-0.004f, 0.78f, 0), new(0.098f, 0.1f, 0.08f), skin, Mat.Skin, 0.03f);                  // cranium
        s.Egg(headB, new(0.04f, 0.735f, 0), new(0.076f, 0.092f, 0.06f), skin, Mat.Skin, 0.03f);                  // face
        s.Block(headB, new(0.05f, 0.676f, 0), new(0.044f, 0.03f, 0.04f), 0.023f, skin, Mat.Skin, 0.025f);          // square jaw
        s.Ball(headB, new(0.098f, 0.658f, 0), 0.025f, skin, Mat.Skin, 0.02f);                                      // chin
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Limb(headB, new(0.0f, 0.715f, 0.058f * z), new(0.05f, 0.662f, 0.04f * z), 0.025f, 0.026f, skin, Mat.Skin, 0.025f); // jaw line
            s.Egg(headB, new(0.072f, 0.725f, 0.042f * z), new(0.027f, 0.022f, 0.02f), skin, Mat.Skin, 0.02f);                    // cheekbone
            s.Egg(headB, new(0.0f, 0.752f, 0.079f * z), new(0.017f, 0.033f, 0.01f), skin, Mat.Skin, 0.012f, new(0, 0, 8f * z)); // ear
            // the eye socket, with the lids over and under the eye
            s.CarveBall(headB, new(0.1f, 0.762f, 0.03f * z), 0.0155f, 0.012f);
            s.Limb(headB, new(0.098f, 0.775f, 0.016f * z), new(0.093f, 0.773f, 0.046f * z), 0.0075f, 0.0065f, skin, Mat.Skin, 0.006f);
            s.Limb(headB, new(0.099f, 0.749f, 0.018f * z), new(0.095f, 0.75f, 0.043f * z), 0.006f, 0.0055f, skin, Mat.Skin, 0.006f);
        }
        // the brow ridge and the nose
        s.Limb(headB, new(0.09f, 0.784f, -0.05f), new(0.097f, 0.787f, 0), 0.0095f, 0.0105f, skin, Mat.Skin, 0.014f);
        s.Limb(headB, new(0.097f, 0.787f, 0), new(0.09f, 0.784f, 0.05f), 0.0105f, 0.0095f, skin, Mat.Skin, 0.014f);
        s.Limb(headB, new(0.098f, 0.77f, 0), new(0.118f, 0.724f, 0), 0.0095f, 0.011f, skin, Mat.Skin, 0.02f);    // nose bridge
        s.Ball(headB, new(0.126f, 0.717f, 0), 0.011f, skin, Mat.Skin, 0.012f);                                // tip
        s.Egg(headB, new(0.114f, 0.709f, 0.01f), new(0.01f, 0.009f, 0.009f), skin, Mat.Skin, 0.01f);            // nostril wings
        s.Egg(headB, new(0.114f, 0.709f, -0.01f), new(0.01f, 0.009f, 0.009f), skin, Mat.Skin, 0.01f);
        // the eyes, dark
        var iris = C(0.09f, 0.065f, 0.05f);
        s.Eye(headB, new(0.098f, 0.762f, 0.03f), 0.0096f, iris, 0.15f);
        s.Eye(headB, new(0.098f, 0.762f, -0.03f), 0.0096f, iris, 0.15f);

        // the finest features are finer than the grid's voxels, so they are laid on the skin as explicit strands, each point at the height
        // of the skin where it stands: the brows (drawn in toward the nose: calm, a little grim), the lids' lines, the lips and the mouth
        float X(float y, float z, float lift) => s.SurfaceX(y, z) + lift;
        Vector3[] Along(float z0, float z1, int n, Func<float, float> yAt, float lift)
        {
            var pts = new Vector3[n];
            for (int j = 0; j < n; j++)
            {
                float t = j / (float)(n - 1), z = z0 + (z1 - z0) * t, y = yAt(t);
                pts[j] = new Vector3(X(y, z, lift), y, z);
            }
            return pts;
        }
        var lashes = BrHair.Darkened(0.2f);
        var lipUp = new Color(0.42f, 0.25f, 0.21f);
        var lipLow = new Color(0.5f, 0.31f, 0.26f);
        var mouthLine = new Color(0.12f, 0.06f, 0.05f);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            // brows: thick at the nose, arching, thinning out toward the temple
            Strand(s, headB, Along(0.005f * z, 0.062f * z, 7, t => 0.7835f + 0.006f * MathF.Sin(t * 2.6f) - 0.004f * t * t, -0.0035f),
                new[] { 0.0092f, 0.0092f, 0.0088f, 0.008f, 0.0068f, 0.005f, 0.0025f }, BrHair, HairMat);
            // the upper lid's line, and the lower lid's
            Strand(s, headB, Along(0.012f * z, 0.049f * z, 6, t => 0.7745f - 0.004f * MathF.Sin(t * 3.14f) * 0 - 0.004f * t, 0.0004f),
                new[] { 0.0028f, 0.0034f, 0.0034f, 0.003f, 0.0024f, 0.0012f }, lashes, Mat.Leather);
        }
        // lips: the upper, with its bow, the fuller lower; the dark line between; the nostrils
        Strand(s, headB, Along(-0.023f, 0.023f, 9, t => 0.6785f + 0.0012f * MathF.Cos((t - 0.5f) * 6.28f) - 0.004f * MathF.Pow(2f * t - 1f, 2f), -0.0025f),
            new[] { 0.0035f, 0.0048f, 0.0055f, 0.0058f, 0.0062f, 0.0058f, 0.0055f, 0.0048f, 0.0035f }, lipUp, Mat.Skin);
        Strand(s, headB, Along(-0.018f, 0.018f, 9, t => 0.6655f - 0.002f * MathF.Pow(2f * t - 1f, 2f), -0.0025f),
            new[] { 0.0035f, 0.0052f, 0.006f, 0.0066f, 0.0068f, 0.0066f, 0.006f, 0.0052f, 0.0035f }, lipLow, Mat.Skin);
        Strand(s, headB, Along(-0.0235f, 0.0235f, 9, t => 0.6722f, -0.0005f),
            new[] { 0.0016f, 0.0024f, 0.0028f, 0.003f, 0.003f, 0.003f, 0.0028f, 0.0024f, 0.0016f }, mouthLine, Mat.Leather);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            Strand(s, headB, Along(0.004f * z, 0.012f * z, 4, t => 0.7045f, 0.0f), new[] { 0.0022f, 0.0035f, 0.0035f, 0.0015f }, mouthLine, Mat.Leather);
        }

        // hair: a dark, tousled cap, the fringe falling over the brow, the sides over the temples
        s.Egg(headB, new(-0.012f, 0.812f, 0), new(0.11f, 0.085f, 0.092f), BrHair, HairMat, 0.022f, bump: 0.003f);
        s.Egg(headB, new(-0.068f, 0.765f, 0), new(0.052f, 0.092f, 0.084f), BrHair, HairMat, 0.022f, bump: 0.003f);
        for (int j = 0; j < 9; j++)
        {
            float t = j / 8f;
            float z = -0.072f + 0.144f * t;
            float rr = (float)rng.NextDouble();
            var root = new Vector3(0.0f + 0.04f * (1f - MathF.Abs(2f * t - 1f)), 0.875f, z);
            var tip = new Vector3(0.09f + 0.025f * rr - 0.035f * MathF.Abs(2f * t - 1f), 0.82f + 0.035f * (float)rng.NextDouble(), z * 1.05f + (rr - 0.5f) * 0.02f);
            s.Limb(headB, root, tip, 0.022f, 0.008f, BrHair, HairMat, 0.016f, 0.002f);
        }
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Limb(headB, new(0.02f, 0.8f, 0.077f * z), new(0.045f, 0.73f, 0.07f * z), 0.018f, 0.009f, BrHair, HairMat, 0.018f, 0.002f); // sideburns
            // a few spikes over the crown and the nape
            s.Limb(headB, new(-0.04f, 0.885f, 0.04f * z), new(-0.085f, 0.905f, 0.06f * z), 0.019f, 0.006f, BrHair, HairMat, 0.008f, 0.002f);
            s.Limb(headB, new(-0.085f, 0.75f, 0.05f * z), new(-0.12f, 0.69f, 0.045f * z), 0.023f, 0.009f, BrHair, HairMat, 0.01f, 0.002f);
        }
        // a little stubble's body along the chin and lip (the rest is paint)
        s.Limb(headB, new(0.094f, 0.64f, -0.018f), new(0.094f, 0.64f, 0.018f), 0.011f, 0.011f, skin, Mat.Skin, 0.012f, 0.0014f);

        // ================================================================ the cowl, and the scarf's tail
        Ring(s, neckB, new(0.014f, 0.52f, 0), 0.1f, 0.115f, 0.036f, BrNavy, Mat.Cloth, 0.012f, 0.0025f);
        Ring(s, neckB, new(0.022f, 0.565f, 0), 0.082f, 0.092f, 0.03f, BrNavy, Mat.Cloth, 0.012f, 0.0025f);
        s.Egg(neckB, new(-0.07f, 0.51f, 0), new(0.075f, 0.065f, 0.13f), BrNavy, Mat.Cloth, 0.03f, bump: 0.002f);
        // a fold of it laid across the chest
        s.Limb(neckB, new(0.095f, 0.535f, 0.075f), new(0.16f, 0.43f, -0.02f), 0.032f, 0.026f, BrNavy, Mat.Cloth, 0.012f, 0.002f);
        s.Limb(neckB, new(0.14f, 0.43f, -0.03f), new(0.115f, 0.34f, -0.12f), 0.026f, 0.014f, BrNavy, Mat.Cloth, 0.01f, 0.002f);
        s.Ball(s0, new(-0.075f, 0.57f, 0.02f), 0.04f, BrNavy, Mat.Cloth, 0.02f);
        s.Limb(s0, new(-0.075f, 0.565f, 0.025f), new(-0.2f, 0.545f, 0.045f), 0.04f, 0.034f, BrNavy, Mat.Cloth, 0.015f);
        s.Limb(s1, new(-0.2f, 0.545f, 0.045f), new(-0.36f, 0.52f, 0.055f), 0.034f, 0.02f, BrNavy, Mat.Cloth, 0.012f);
        s.Limb(s2, new(-0.36f, 0.52f, 0.055f), new(-0.45f, 0.495f, 0.06f), 0.02f, 0.008f, BrNavy, Mat.Cloth, 0.01f);

        // ================================================================ arms
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], hd = s["hand" + sfx], cl = s["clav" + sfx];
            // the shoulder cap, the upper arm (deltoid, biceps, triceps), the elbow and the forearm's swell, bare
            s.Egg(ua, new(0, 0.435f, 0.225f * z), new(0.098f, 0.095f, 0.085f), BrSkin, Mat.Skin, 0.035f, bump: 0.0012f);
            s.Limb(ua, new(0, 0.43f, 0.218f * z), new(0, 0.225f, 0.215f * z), 0.074f, 0.054f, BrSkin, Mat.Skin, 0.03f, 0.0012f);
            s.Egg(ua, new(0.034f, 0.36f, 0.218f * z), new(0.044f, 0.078f, 0.052f), BrSkin, Mat.Skin, 0.03f);        // biceps
            s.Egg(ua, new(-0.038f, 0.35f, 0.218f * z), new(0.042f, 0.088f, 0.052f), BrSkin, Mat.Skin, 0.03f);       // triceps
            s.Ball(fa, new(0, 0.19f, 0.215f * z), 0.053f, BrSkin, Mat.Skin, 0.02f);
            s.Limb(fa, new(0, 0.18f, 0.215f * z), new(0, -0.025f, 0.213f * z), 0.058f, 0.043f, BrSkin, Mat.Skin, 0.02f, 0.001f);
            s.Egg(fa, new(0.012f, 0.13f, 0.215f * z), new(0.048f, 0.07f, 0.056f), BrSkin, Mat.Skin, 0.025f);        // the forearm's swell
            // the leather bracer: flared at the elbow end, strapped twice and buckled
            s.Limb(fa, new(0, 0.12f, 0.215f * z), new(0, -0.048f, 0.215f * z), 0.066f, 0.057f, BrLeather, Mat.Leather, 0.01f, 0.0016f);
            Ring(s, fa, new(0, 0.122f, 0.215f * z), 0.07f, 0.07f, 0.0085f, BrLeatherDk, Mat.Leather, 0.004f, 0.0008f);
            Ring(s, fa, new(0, 0.085f, 0.215f * z), 0.068f, 0.068f, 0.0095f, BrLeatherDk, Mat.Leather, 0.004f, 0.0008f);
            Ring(s, fa, new(0, 0.03f, 0.215f * z), 0.0625f, 0.0625f, 0.0095f, BrLeatherDk, Mat.Leather, 0.004f, 0.0008f);
            Ring(s, fa, new(0, -0.046f, 0.215f * z), 0.059f, 0.059f, 0.01f, BrLeatherDk, Mat.Leather, 0.004f, 0.0008f);
            s.Block(fa, new(0.068f, 0.085f, 0.215f * z), new(0.006f, 0.014f, 0.016f), 0.003f, BrMetal, Mat.Metal, 0.003f);
            s.Block(fa, new(0.063f, 0.03f, 0.215f * z), new(0.006f, 0.014f, 0.016f), 0.003f, BrMetal, Mat.Metal, 0.003f);
            // the fingerless glove's fist (it grips along the vertical: the sword goes through it)
            s.Egg(hd, new(0.014f, -0.075f, 0.215f * z), new(0.05f, 0.05f, 0.043f), BrGlove, Mat.Leather, 0.02f, bump: 0.001f);
            for (int f = 0; f < 4; f++)
                s.Egg(hd, new(0.042f, -0.047f - f * 0.0185f, 0.215f * z), new(0.024f, 0.0115f, 0.04f), f % 2 == 0 ? BrGlove : BrLeatherDk, Mat.Leather, 0.008f);
            // bare knuckles under the glove's edge, and the thumb laid over the fingers
            s.Egg(hd, new(0.052f, -0.044f, 0.215f * z), new(0.012f, 0.01f, 0.036f), BrSkin, Mat.Skin, 0.006f);
            s.Limb(hd, new(0.02f, -0.045f, 0.236f * z), new(0.05f, -0.062f, 0.226f * z), 0.0165f, 0.013f, BrGlove, Mat.Leather, 0.01f);
            s.Ball(hd, new(0.052f, -0.063f, 0.224f * z), 0.0135f, BrSkin, Mat.Skin, 0.006f);
            // the wrist: the glove's cuff
            s.Limb(hd, new(0.006f, -0.036f, 0.215f * z), new(0.012f, -0.06f, 0.215f * z), 0.05f, 0.046f, BrGlove, Mat.Leather, 0.012f);
        }

        // ================================================================ the fur mantle on his left shoulder
        {
            int ua = s["uarm_l"];
            var fur = BrFur;
            var furDk = BrFur.Darkened(0.3f);
            var furMid = BrFur.Darkened(0.15f);
            // the mass of it, a pelt thrown over the shoulder and down the arm
            s.Egg(ua, new(0.0f, 0.495f, -0.222f), new(0.125f, 0.05f, 0.13f), furMid, Mat.Fur, 0.04f, bump: 0.006f);
            s.Egg(ua, new(-0.01f, 0.455f, -0.246f), new(0.105f, 0.075f, 0.082f), furDk, Mat.Fur, 0.04f, bump: 0.006f);
            // shaggy locks in layers: long ones at the rim, shorter and paler above
            for (int j = 0; j < 18; j++)
            {
                float a2 = j / 18f * Mathf.Tau + 0.15f * (j % 2);
                var dir = new Vector3(MathF.Cos(a2), 0, MathF.Sin(a2));
                bool outer = dir.Z < -0.15f;
                float len = (outer ? 0.07f : 0.045f) + 0.03f * (float)rng.NextDouble();
                var root = new Vector3(0, 0.49f, -0.222f) + dir * 0.09f;
                var tip = root + dir * (len * 0.55f) + new Vector3(0, -(outer ? 0.1f : 0.06f) - 0.025f * (float)rng.NextDouble(), 0);
                s.Limb(ua, root, tip, 0.032f, 0.01f, j % 3 == 0 ? furDk : j % 3 == 1 ? furMid : fur, Mat.Fur, 0.016f, 0.004f);
            }
            for (int j = 0; j < 12; j++)
            {
                float a2 = j / 12f * Mathf.Tau + 0.3f;
                var dir = new Vector3(MathF.Cos(a2), 0, MathF.Sin(a2));
                var root = new Vector3(0, 0.45f, -0.246f) + dir * 0.075f;
                var tip = root + dir * 0.03f + new Vector3(0, -0.07f - 0.04f * (float)rng.NextDouble(), 0);
                s.Limb(ua, root, tip, 0.027f, 0.008f, j % 2 == 0 ? furDk : furMid, Mat.Fur, 0.014f, 0.004f);
            }
            for (int j = 0; j < 7; j++)
            {
                float t = j / 6f;
                var root = new Vector3(-0.05f + 0.11f * t, 0.515f, -0.12f - 0.08f * t);
                s.Limb(ua, root, root + new Vector3(0.02f, 0.045f, -0.02f), 0.026f, 0.008f, fur, Mat.Fur, 0.014f, 0.004f);
            }
        }

        // ================================================================ the straps, the belt and the buckles
        {
            // the main strap: from his left shoulder, across the chest and down to the right hip
            var path = new Vector3[10];
            for (int j = 0; j < path.Length; j++)
            {
                float t = j / (float)(path.Length - 1);
                float y = Mathf.Lerp(0.475f, 0.1f, t), z = Mathf.Lerp(-0.125f, 0.115f, t);
                path[j] = new Vector3(TorsoFront(y, z) + 0.006f, y, z);
            }
            for (int j = 0; j < path.Length - 1; j++)
                s.Limb(j < 3 ? neckB : chestB, path[j], path[j + 1], 0.0155f, 0.0155f, BrLeather, Mat.Leather, 0.003f, 0.0008f);
            // a second, thinner strap crossing the other way over the belly
            var p2 = new Vector3[7];
            for (int j = 0; j < p2.Length; j++)
            {
                float t = j / (float)(p2.Length - 1);
                float y = Mathf.Lerp(0.3f, 0.095f, t), z = Mathf.Lerp(0.14f, -0.1f, t);
                p2[j] = new Vector3(TorsoFront(y, z) + 0.006f, y, z);
            }
            for (int j = 0; j < p2.Length - 1; j++)
                s.Limb(chestB, p2[j], p2[j + 1], 0.011f, 0.011f, BrLeatherDk, Mat.Leather, 0.003f, 0.0008f);
            // the buckle on the main strap, high on his chest
            s.Block(chestB, new(TorsoFront(0.405f, -0.062f) + 0.016f, 0.405f, -0.062f), new(0.006f, 0.02f, 0.017f), 0.004f, BrMetal, Mat.Metal, 0.003f, new(0, 0, 0));
            s.Block(chestB, new(TorsoFront(0.405f, -0.062f) + 0.021f, 0.405f, -0.062f), new(0.0035f, 0.012f, 0.009f), 0.002f, BrLeatherDk, Mat.Leather, 0.002f);
            // the belt, with a broad buckle, and a lower belt slung askew
            Ring(s, spineB, new(0.006f, 0.118f, 0), 0.113f, 0.145f, 0.022f, BrLeatherDk, Mat.Leather, 0.006f, 0.001f);
            Ring(s, hipsB, new(0.012f, 0.082f, 0), 0.128f, 0.158f, 0.013f, BrLeather, Mat.Leather, 0.006f, 0.001f, 0.022f);
            s.Block(spineB, new(0.126f, 0.118f, 0), new(0.01f, 0.026f, 0.034f), 0.005f, BrMetal, Mat.Metal, 0.004f);
            s.Block(spineB, new(0.134f, 0.118f, 0), new(0.0035f, 0.017f, 0.021f), 0.003f, BrLeatherDk, Mat.Leather, 0.002f);
            // a pouch at the right hip, and the lantern's loop is a part of the rig
            s.Egg(hipsB, new(0.02f, 0.06f, 0.16f), new(0.05f, 0.06f, 0.035f), BrLeather, Mat.Leather, 0.012f, bump: 0.001f);
            s.Egg(hipsB, new(0.02f, 0.092f, 0.168f), new(0.054f, 0.026f, 0.04f), BrLeatherDk, Mat.Leather, 0.01f);
        }

        // ================================================================ legs
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            // baggy dark trousers
            s.Limb(th, new(0, 0.0f, 0.095f * z), new(0.012f, -0.39f, 0.095f * z), 0.08f, 0.062f, BrTrouser, Mat.Cloth, 0.03f, 0.0018f);
            s.Egg(th, new(0.03f, -0.1f, 0.09f * z), new(0.07f, 0.12f, 0.07f), BrTrouser, Mat.Cloth, 0.04f, bump: 0.002f);
            s.Limb(sh, new(0.01f, -0.39f, 0.095f * z), new(-0.01f, -0.6f, 0.095f * z), 0.062f, 0.054f, BrTrouser, Mat.Cloth, 0.02f, 0.0015f);
            // a knee guard of dark leather, strapped
            s.Egg(sh, new(0.048f, -0.388f, 0.095f * z), new(0.038f, 0.052f, 0.058f), BrLeatherDk, Mat.Leather, 0.012f, bump: 0.0012f);
            Ring(s, sh, new(0.014f, -0.345f, 0.095f * z), 0.067f, 0.067f, 0.007f, BrLeather, Mat.Leather, 0.004f);
            Ring(s, sh, new(0.01f, -0.43f, 0.095f * z), 0.064f, 0.064f, 0.007f, BrLeather, Mat.Leather, 0.004f);
            // the boots: leather to below the knee, a ragged fur cuff, straps and buckles, a toecap and a heel
            s.Limb(sh, new(0.0f, -0.5f, 0.095f * z), new(-0.012f, -0.775f, 0.095f * z), 0.066f, 0.058f, BrBoot, Mat.Leather, 0.012f, 0.0016f);
            Ring(s, sh, new(0.0f, -0.495f, 0.095f * z), 0.074f, 0.074f, 0.02f, BrFur, Mat.Fur, 0.01f, 0.004f);
            Ring(s, sh, new(0.002f, -0.525f, 0.095f * z), 0.07f, 0.07f, 0.014f, BrFur.Darkened(0.3f), Mat.Fur, 0.01f, 0.004f);
            Ring(s, sh, new(-0.004f, -0.6f, 0.095f * z), 0.07f, 0.07f, 0.0075f, BrLeatherDk, Mat.Leather, 0.004f);
            Ring(s, sh, new(-0.007f, -0.67f, 0.095f * z), 0.066f, 0.066f, 0.0075f, BrLeatherDk, Mat.Leather, 0.004f);
            s.Block(sh, new(0.062f, -0.6f, 0.095f * z), new(0.005f, 0.013f, 0.015f), 0.003f, BrMetal, Mat.Metal, 0.003f);
            s.Block(sh, new(0.058f, -0.67f, 0.095f * z), new(0.005f, 0.013f, 0.015f), 0.003f, BrMetal, Mat.Metal, 0.003f);
            s.Limb(ft, new(-0.03f, -0.775f, 0.095f * z), new(0.11f, -0.785f, 0.095f * z), 0.056f, 0.046f, BrBoot, Mat.Leather, 0.012f, 0.0012f);
            s.Egg(ft, new(0.098f, -0.778f, 0.095f * z), new(0.048f, 0.036f, 0.05f), BrBoot, Mat.Leather, 0.012f, bump: 0.0012f);
            s.Egg(ft, new(-0.045f, -0.79f, 0.095f * z), new(0.04f, 0.028f, 0.05f), BrLeatherDk, Mat.Leather, 0.01f);
            s.Limb(ft, new(-0.04f, -0.803f, 0.095f * z), new(0.135f, -0.803f, 0.095f * z), 0.0105f, 0.0105f, BrLeatherDk.Darkened(0.3f), Mat.Leather, 0.005f);
        }

        // ================================================================ the cloth: cloak behind, tabard in front
        {
            (float y, float w, float x, float wrap)[] rowSpec =
            {
                (0.52f, 0.17f, -0.1f, 0.09f),
                (0.26f, 0.215f, -0.158f, 0.075f),
                (-0.06f, 0.23f, -0.18f, 0.06f),
                (-0.32f, 0.235f, -0.192f, 0.05f),
                (-0.58f, 0.235f, -0.2f, 0.045f),
            };
            const int cols = 11;
            var grid = new Vector3[rowSpec.Length][];
            for (int r = 0; r < rowSpec.Length; r++)
            {
                var (y, w, x, wrap) = rowSpec[r];
                float fall = r / (float)(rowSpec.Length - 1);
                grid[r] = new Vector3[cols];
                for (int c = 0; c < cols; c++)
                {
                    float a = -1f + 2f * c / (cols - 1);
                    float fold = 0.02f * fall * MathF.Sin(a * MathF.PI * 2.5f + 0.6f);
                    // the hem is torn: ragged, one long strip hanging lower than the rest
                    float hem = r == rowSpec.Length - 1 ? 0.07f * MathF.Sin(a * 9.3f + 1.1f) - 0.05f * (c % 2) + (c == 3 ? -0.09f : 0f) : 0f;
                    grid[r][c] = new Vector3(x + wrap * a * a - fold, y + hem, w * a);
                }
            }
            s.Cloth(new[] { c0, c1, c2, c3, c3 }, grid, BrSlate.Darkened(0.35f), new Vector3(-1, 0, 0), Mat.Cloth, 0.006f, 3);
        }
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx];
            // each half of the tabard follows its own thigh (so a stride swings it open, as a split front does), torn at the hem
            (float y, float x)[] rows = { (0.1f, 0.14f), (-0.07f, 0.138f), (-0.28f, 0.142f), (-0.5f, 0.143f) };
            const int cols = 6;
            var grid = new Vector3[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                grid[r] = new Vector3[cols];
                float fall = r / (float)(rows.Length - 1);
                for (int c = 0; c < cols; c++)
                {
                    float t = c / (float)(cols - 1);
                    // (from the middle, c = 0, out to the side)
                    float zz = (0.004f + 0.135f * t) * z;
                    float xx = rows[r].x - 0.06f * t * t - 0.012f * MathF.Sin(t * 5f + k * 2f) * fall;
                    float hem = r == rows.Length - 1 ? -0.07f * ((c + k) % 3) / 2f + 0.03f * MathF.Sin(c * 2.3f + k) : 0f;
                    grid[r][c] = new Vector3(xx, rows[r].y + hem, zz);
                }
            }
            // the cloth runs across a row, so the grid is transposed: rows hang, columns spread
            s.Cloth(new[] { hipsB, hipsB, th, th }, grid, k == 0 ? BrNavy : BrTunicBrown.Lerp(BrNavy, 0.55f), new Vector3(1, 0, 0), Mat.Cloth, 0.006f, 3);
            // a brown flap over the hip, held by the lower belt
            int hp = hipsB;
            s.Egg(hp, new(0.02f, -0.04f, 0.165f * z), new(0.032f, 0.1f, 0.05f), BrTunicBrown, Mat.Cloth, 0.012f, bump: 0.0015f);
        }

        // ================================================================ the paint
        var noise = s.Noise;
        static float Lum(Color c) => 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
        s.Paints.Add((p, c, mat) =>
        {
            float n1 = noise.Fbm(p.X * 22f, p.Y * 22f, p.Z * 22f, 3);
            float n2 = noise.Fbm(p.X * 70f + 3f, p.Y * 70f, p.Z * 70f, 2);
            // (on the head, skin and hair run into each other: how much of this spot is hair is read off how dark the blend is, and
            // jittered, so the hairline is a ragged, organic edge and not a staircase of voxels)
            if ((mat == Mat.Skin || mat == Mat.Fur) && p.Y > 0.6f && p.Y < 1.0f && p.X > -0.2f)
            {
                float t = Mathf.Clamp((Lum(BrSkin) - Lum(c)) / (Lum(BrSkin) - Lum(BrHair)), 0f, 1f);
                float hairy = Sm(0.4f, 0.62f, t + n2 * 0.14f);
                return BrFace(p, BrSkin, n1, n2).Lerp(BrHairCol(n1, n2), hairy);
            }
            switch (mat)
            {
                case Mat.Skin:
                    return BrFace(p, c, n1, n2);
                case Mat.Fur:
                {
                    // the fur is pale, grey toward the roots, with dirty streaks
                    float streak = Sm(-0.3f, 0.5f, n2);
                    c = c.Lerp(BrFur, 0.5f);
                    var dirt = new Color(0.5f, 0.45f, 0.38f);
                    return c.Lerp(dirt, Mathf.Clamp(0.25f - n1 * 0.4f, 0f, 0.8f) + streak * 0.2f);
                }
                case Mat.Leather:
                {
                    // scuffed and sweat-dark, lighter where it has worn through
                    float wear = Sm(0.1f, 0.6f, n2);
                    c = c.Darkened(Mathf.Clamp(-n1, 0f, 1f) * 0.3f).Lightened(wear * 0.1f);
                    if (p.Y < -0.7f) c = c.Darkened(0.12f * Sm(-0.7f, -0.8f, p.Y)); // mud on the boots
                    return c;
                }
                case Mat.Cloth:
                {
                    // the tunic: brown panel low on his left, worn black elsewhere; every cloth mottled with wear and dirt
                    if (c.R < 0.14f && c.G < 0.14f && c.B < 0.14f && p.Y > 0.06f && p.Y < 0.5f && p.X > -0.02f)
                    {
                        float brown = Sm(0.02f, -0.06f, p.Z + 0.01f * n1) * Sm(0.38f, 0.3f, p.Y + 0.02f * n1);
                        c = c.Lerp(BrTunicBrown, brown * 0.85f);
                    }
                    float fray = Sm(0.1f, 0.7f, n2);
                    c = c.Darkened(Mathf.Clamp(-n1, 0f, 1f) * 0.28f).Lightened(fray * 0.06f);
                    if (p.Y < -0.3f) c = c.Lerp(new Color(0.22f, 0.2f, 0.17f), 0.25f * Sm(-0.3f, -0.55f, p.Y)); // dust at the hem
                    return c;
                }
                case Mat.Metal:
                    return c.Darkened(Mathf.Clamp(-n1, 0f, 1f) * 0.3f);
                default:
                    return c;
            }
        });

        // ================================================================ the longsword, gripped in the right fist
        int handR = s["hand_r"];
        var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
        var sw = new MeshBuilder();
        sw.Append(PropMeshes.Longsword(1.05f, 0.036f, C(0.72f, 0.74f, 0.78f), C(0.42f, 0.44f, 0.47f), C(0.1f, 0.06f, 0.04f)), grip);
        s.Rigid(handR, sw, Mat.Metal);
    }
}
