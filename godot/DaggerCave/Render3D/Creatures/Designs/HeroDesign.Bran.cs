using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman, Bran: a broad, weathered warrior. Short dark tousled hair and a stubbled, stern
/// face; a slate-blue cowl at the throat and a torn slate-blue cloak behind; a great fur mantle over
/// one shoulder; a sleeveless dark tunic crossed by brown leather straps and buckles; bare, heavy
/// arms in wrapped leather bracers and fingerless gloves; a wide belt with a ragged tabard hanging
/// from it; dark trousers, leather knee guards and tall boots with fur cuffs; a longsword with a
/// plain crossguard and a wrapped grip. It sits on the same skeleton as the other heroes (so every
/// pose and stroke is shared), just thicker.
/// </summary>
public sealed partial class HeroDesign
{
    private void SculptBran(Sculptor s)
    {
        var skin = C(0.62f, 0.44f, 0.34f);
        var stubble = C(0.36f, 0.265f, 0.22f);
        var hair = C(0.13f, 0.085f, 0.055f);
        var slate = C(0.06f, 0.095f, 0.2f);
        var slateDk = C(0.03f, 0.05f, 0.13f);
        var tunic = C(0.05f, 0.05f, 0.058f);
        var trousers = C(0.045f, 0.045f, 0.055f);
        var leather = C(0.36f, 0.19f, 0.085f);
        var leatherDk = C(0.19f, 0.1f, 0.05f);
        var boot = C(0.2f, 0.11f, 0.055f);
        var fur = C(0.62f, 0.57f, 0.47f);
        var steel = C(0.5f, 0.52f, 0.56f);

        int hipsB = s["hips"], spineB = s["spine"], chestB = s["chest"], neckB = s["neck"], headB = s["head"];
        int c0 = s["cape0"], c1 = s["cape1"], c2 = s["cape2"], c3 = s["cape3"];
        int s0 = s["scarf0"], s1 = s["scarf1"], s2 = s["scarf2"];

        // ---- torso: a deep chest and heavy shoulders, the tunic over it
        s.Egg(hipsB, new(0, 0.03f, 0), new(0.13f, 0.1f, 0.15f), trousers, Mat.Leather, 0.04f);
        s.Limb(spineB, new(0, 0.05f, 0), new(0, 0.27f, 0), 0.112f, 0.148f, tunic, Mat.Leather, 0.04f);
        s.Egg(chestB, new(0.012f, 0.37f, 0), new(0.14f, 0.165f, 0.22f), tunic, Mat.Leather, 0.05f);
        s.Limb(chestB, new(-0.01f, 0.45f, -0.17f), new(-0.01f, 0.45f, 0.17f), 0.08f, 0.08f, tunic, Mat.Leather, 0.04f);
        // a panel of brown leather down the tunic's left side (the far side), lacing at the seam
        s.Egg(chestB, new(0.03f, 0.33f, -0.145f), new(0.115f, 0.15f, 0.07f), leather, Mat.Leather, 0.02f);
        s.Limb(chestB, new(0.11f, 0.46f, -0.095f), new(0.125f, 0.2f, -0.1f), 0.008f, 0.008f, leatherDk, Mat.Leather, 0.004f);
        // the diagonal strap: over the left shoulder, down across the chest to the right hip, with its buckle
        s.Limb(chestB, new(0.1f, 0.475f, -0.13f), new(0.158f, 0.22f, 0.06f), 0.033f, 0.033f, leather, Mat.Leather, 0.008f);
        s.Limb(spineB, new(0.158f, 0.22f, 0.06f), new(0.13f, 0.04f, 0.16f), 0.033f, 0.033f, leather, Mat.Leather, 0.008f);
        s.Block(chestB, new(0.163f, 0.345f, -0.03f), new(0.009f, 0.032f, 0.026f), 0.004f, steel, Mat.Metal, 0.004f);
        // a second strap behind, crossing the other way, with a ring where they meet
        s.Limb(chestB, new(-0.13f, 0.47f, 0.14f), new(-0.15f, 0.2f, -0.09f), 0.02f, 0.02f, leather, Mat.Leather, 0.008f);
        s.Limb(chestB, new(0.09f, 0.47f, 0.14f), new(-0.05f, 0.2f, 0.19f), 0.016f, 0.016f, leatherDk, Mat.Leather, 0.006f);
        // the belt, a lower strap, and two buckles
        s.Egg(hipsB, new(0.004f, 0.095f, 0), new(0.15f, 0.036f, 0.168f), leather, Mat.Leather, 0.012f);
        s.Egg(hipsB, new(0.004f, 0.035f, 0), new(0.147f, 0.02f, 0.166f), leatherDk, Mat.Leather, 0.01f);
        s.Block(hipsB, new(0.152f, 0.095f, 0.0f), new(0.012f, 0.034f, 0.042f), 0.004f, steel, Mat.Metal, 0.004f);
        s.Block(hipsB, new(0.15f, 0.036f, 0.07f), new(0.009f, 0.017f, 0.022f), 0.003f, steel, Mat.Metal, 0.004f);
        // a satchel of leather at the right hip
        s.Egg(hipsB, new(-0.02f, 0.03f, 0.172f), new(0.06f, 0.06f, 0.036f), leatherDk, Mat.Leather, 0.02f);

        // ---- neck, cowl, and head
        s.Limb(neckB, new(0.01f, 0.46f, 0), new(0.02f, 0.61f, 0), 0.064f, 0.056f, skin, Mat.Skin, 0.03f);
        // a slate cowl wound round the throat and thrown over the left shoulder
        s.Egg(neckB, new(0.02f, 0.575f, 0), new(0.115f, 0.075f, 0.145f), slate, Mat.Leather, 0.03f);
        s.Egg(neckB, new(-0.03f, 0.615f, 0), new(0.1f, 0.085f, 0.125f), slate, Mat.Leather, 0.03f);
        s.Limb(chestB, new(0.06f, 0.52f, -0.02f), new(0.115f, 0.38f, -0.13f), 0.06f, 0.04f, slate, Mat.Leather, 0.03f);
        s.Limb(chestB, new(0.11f, 0.4f, -0.12f), new(0.08f, 0.3f, -0.18f), 0.036f, 0.02f, slateDk, Mat.Leather, 0.02f);

        // the head: a square jaw dark with stubble, a heavy brow over deep-set eyes, a straight, strong nose
        s.Egg(headB, new(0.02f, 0.715f, 0), new(0.1f, 0.112f, 0.102f), skin, Mat.Skin, 0.02f);
        s.Egg(headB, new(0.045f, 0.652f, 0), new(0.082f, 0.066f, 0.09f), stubble, Mat.Skin, 0.025f);
        s.Block(headB, new(0.08f, 0.635f, 0), new(0.032f, 0.032f, 0.072f), 0.022f, stubble, Mat.Skin, 0.02f);
        s.Ball(headB, new(0.11f, 0.63f, 0), 0.03f, stubble, Mat.Skin, 0.02f);
        s.Limb(headB, new(0.065f, 0.645f, -0.082f), new(0.05f, 0.7f, -0.098f), 0.024f, 0.016f, stubble, Mat.Skin, 0.02f);
        s.Limb(headB, new(0.065f, 0.645f, 0.082f), new(0.05f, 0.7f, 0.098f), 0.024f, 0.016f, stubble, Mat.Skin, 0.02f);
        // cheekbones, and a brow ridge that shades the eyes
        s.Ball(headB, new(0.07f, 0.705f, -0.08f), 0.024f, skin, Mat.Skin, 0.02f);
        s.Ball(headB, new(0.07f, 0.705f, 0.08f), 0.024f, skin, Mat.Skin, 0.02f);
        s.Limb(headB, new(0.09f, 0.755f, -0.075f), new(0.09f, 0.755f, 0.075f), 0.026f, 0.026f, skin, Mat.Skin, 0.02f);
        s.CarveBall(headB, new(0.12f, 0.728f, 0.044f), 0.02f, 0.01f);
        s.CarveBall(headB, new(0.12f, 0.728f, -0.044f), 0.02f, 0.01f);
        s.Limb(headB, new(0.1f, 0.735f, 0), new(0.128f, 0.692f, 0), 0.02f, 0.018f, skin, Mat.Skin, 0.014f);
        s.Ball(headB, new(0.132f, 0.686f, 0), 0.02f, skin, Mat.Skin, 0.012f);
        s.Ball(headB, new(0.12f, 0.683f, 0.016f), 0.011f, skin, Mat.Skin, 0.008f);
        s.Ball(headB, new(0.12f, 0.683f, -0.016f), 0.011f, skin, Mat.Skin, 0.008f);
        s.Ball(headB, new(-0.005f, 0.71f, 0.1f), 0.025f, skin, Mat.Skin, 0.012f);
        s.Ball(headB, new(-0.005f, 0.71f, -0.1f), 0.025f, skin, Mat.Skin, 0.012f);
        s.CarveLimb(headB, new(0.112f, 0.65f, -0.03f), new(0.112f, 0.649f, 0.03f), 0.004f, 0.004f, 0.003f);
        s.Eye(headB, new(0.1f, 0.728f, 0.044f), 0.0115f, C(0.1f, 0.075f, 0.06f), 0.1f);
        s.Eye(headB, new(0.1f, 0.728f, -0.044f), 0.0115f, C(0.1f, 0.075f, 0.06f), 0.1f);
        // dark, slanted brows
        s.Limb(headB, new(0.104f, 0.765f, 0.012f), new(0.092f, 0.772f, 0.07f), 0.011f, 0.008f, hair, Mat.Leather, 0.006f);
        s.Limb(headB, new(0.104f, 0.765f, -0.012f), new(0.092f, 0.772f, -0.07f), 0.011f, 0.008f, hair, Mat.Leather, 0.006f);
        // tousled dark hair: a full cap with a fringe swept across the brow, and short tufts swept back
        s.Egg(headB, new(0.075f, 0.815f, 0.0f), new(0.05f, 0.03f, 0.1f), hair, Mat.Leather, 0.02f, new(0, 0, -12), 0.006f);
        s.Egg(headB, new(0.085f, 0.8f, 0.045f), new(0.03f, 0.03f, 0.04f), hair, Mat.Leather, 0.015f, bump: 0.005f);
        s.Egg(headB, new(0.08f, 0.8f, -0.05f), new(0.03f, 0.035f, 0.04f), hair, Mat.Leather, 0.015f, bump: 0.005f);
        s.Egg(headB, new(0.0f, 0.74f, 0.092f), new(0.05f, 0.06f, 0.022f), hair, Mat.Leather, 0.015f, bump: 0.005f);
        s.Egg(headB, new(0.0f, 0.74f, -0.092f), new(0.05f, 0.06f, 0.022f), hair, Mat.Leather, 0.015f, bump: 0.005f);
        s.Egg(headB, new(-0.008f, 0.805f, 0), new(0.116f, 0.075f, 0.116f), hair, Mat.Leather, 0.03f, bump: 0.006f);
        s.Egg(headB, new(-0.06f, 0.755f, 0), new(0.07f, 0.095f, 0.104f), hair, Mat.Leather, 0.03f, bump: 0.006f);
        void Tuft(Vector3 a, Vector3 b, float r, Vector3 bend, float curl) => s.Horn(headB, a, b, r, hair, Mat.Leather, bend, curl, 6, 4);
        // (clumps of hair: short, thick, swept)
        s.Egg(headB, new(0.06f, 0.845f, 0.04f), new(0.06f, 0.03f, 0.045f), hair, Mat.Leather, 0.02f, new(0, 0, -15), 0.005f);
        s.Egg(headB, new(0.055f, 0.85f, -0.04f), new(0.055f, 0.03f, 0.045f), hair, Mat.Leather, 0.02f, new(0, 0, -20), 0.005f);
        s.Egg(headB, new(0.0f, 0.865f, 0.0f), new(0.08f, 0.035f, 0.08f), hair, Mat.Leather, 0.02f, bump: 0.006f);
        s.Egg(headB, new(-0.06f, 0.855f, 0.03f), new(0.05f, 0.035f, 0.05f), hair, Mat.Leather, 0.02f, bump: 0.006f);
        s.Egg(headB, new(-0.06f, 0.85f, -0.04f), new(0.05f, 0.035f, 0.05f), hair, Mat.Leather, 0.02f, bump: 0.006f);
        Tuft(new(-0.08f, 0.8f, 0.0f), new(-0.15f, 0.78f, 0.02f), 0.034f, Vector3.Down, 0.02f);
        Tuft(new(-0.07f, 0.74f, 0.03f), new(-0.13f, 0.7f, 0.045f), 0.028f, Vector3.Down, 0.012f);

        // ---- the scarf's tail, trailing behind (a short slate rag)
        s.Ball(s0, new(-0.07f, 0.6f, 0), 0.05f, slate, Mat.Leather, 0.02f);
        s.Limb(s0, new(-0.07f, 0.595f, 0.005f), new(-0.15f, 0.55f, 0.025f), 0.044f, 0.036f, slate, Mat.Leather, 0.015f);
        s.Limb(s1, new(-0.15f, 0.55f, 0.025f), new(-0.21f, 0.44f, 0.035f), 0.036f, 0.02f, slateDk, Mat.Leather, 0.012f);
        s.Limb(s2, new(-0.21f, 0.44f, 0.035f), new(-0.23f, 0.34f, 0.04f), 0.02f, 0.006f, slateDk, Mat.Leather, 0.01f);

        // ---- the fur mantle over the left shoulder (the far one), spilling down its front and over the back
        int clavL = s["clav_l"];
        s.Egg(clavL, new(0.0f, 0.5f, -0.2f), new(0.16f, 0.09f, 0.17f), fur, Mat.Fur, 0.04f, bump: 0.012f);
        s.Egg(clavL, new(0.02f, 0.44f, -0.25f), new(0.12f, 0.09f, 0.1f), fur, Mat.Fur, 0.04f, bump: 0.012f);
        s.Egg(clavL, new(-0.04f, 0.48f, -0.16f), new(0.1f, 0.06f, 0.14f), fur, Mat.Fur, 0.04f, bump: 0.012f);
        s.Egg(chestB, new(-0.05f, 0.535f, -0.03f), new(0.12f, 0.06f, 0.19f), fur, Mat.Fur, 0.04f, bump: 0.01f);
        s.Egg(clavL, new(0.06f, 0.5f, -0.28f), new(0.08f, 0.08f, 0.07f), fur, Mat.Fur, 0.04f, bump: 0.014f);
        s.Egg(clavL, new(-0.08f, 0.5f, -0.27f), new(0.08f, 0.08f, 0.07f), fur, Mat.Fur, 0.04f, bump: 0.014f);
        s.Egg(clavL, new(-0.02f, 0.56f, -0.14f), new(0.09f, 0.05f, 0.09f), fur, Mat.Fur, 0.04f, bump: 0.014f);
        // its ragged lower edge
        for (int k = 0; k < 6; k++)
        {
            float z = -0.1f - k * 0.03f;
            float x = 0.09f - Math.Abs(k - 2.5f) * 0.03f;
            s.Horn(clavL, new(x, 0.47f, z), new(x + 0.015f, 0.385f - (k % 2) * 0.03f, z - 0.012f), 0.026f, fur, Mat.Fur, Vector3.Forward, 0.015f, 6, 5);
        }
        for (int k = 0; k < 4; k++)
            s.Horn(clavL, new(-0.06f, 0.46f - k * 0.0f, -0.12f - k * 0.04f), new(-0.08f, 0.38f - (k % 2) * 0.03f, -0.13f - k * 0.045f), 0.024f, fur, Mat.Fur, Vector3.Forward, 0.015f, 6, 5);

        // ---- arms: bare, heavy, in wrapped leather bracers and fingerless gloves
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], hd = s["hand" + sfx];
            s.Egg(ua, new(0, 0.43f, 0.19f * z), new(0.09f, 0.09f, 0.085f), skin, Mat.Skin, 0.03f);
            s.Limb(ua, new(0, 0.44f, 0.19f * z), new(0, 0.2f, 0.2f * z), 0.078f, 0.064f, skin, Mat.Skin, 0.03f);
            s.Egg(ua, new(0.035f, 0.33f, 0.2f * z), new(0.055f, 0.085f, 0.055f), skin, Mat.Skin, 0.03f);
            s.Ball(fa, new(0, 0.19f, 0.2f * z), 0.06f, skin, Mat.Skin, 0.02f);
            s.Limb(fa, new(0, 0.18f, 0.2f * z), new(0, -0.03f, 0.2f * z), 0.066f, 0.052f, skin, Mat.Skin, 0.02f);
            // the bracer: most of the forearm, flaring at the wrist, strapped at the top
            s.Limb(fa, new(0, 0.12f, 0.2f * z), new(0, -0.045f, 0.2f * z), 0.068f, 0.064f, leather, Mat.Leather, 0.012f);
            s.Egg(fa, new(0, 0.115f, 0.2f * z), new(0.074f, 0.014f, 0.074f), leatherDk, Mat.Leather, 0.008f);
            s.Egg(fa, new(0, 0.03f, 0.2f * z), new(0.072f, 0.012f, 0.072f), leatherDk, Mat.Leather, 0.008f);
            for (int w = 0; w < 4; w++) s.Egg(fa, new(0, 0.085f - w * 0.035f, 0.2f * z), new(0.07f, 0.008f, 0.07f), leatherDk, Mat.Leather, 0.006f);
            s.Block(fa, new(0.07f, 0.115f, 0.2f * z), new(0.008f, 0.02f, 0.018f), 0.003f, steel, Mat.Metal, 0.003f);
            // a fingerless glove: dark leather over the palm, the fingertips bare
            s.Limb(hd, new(0.005f, -0.05f, 0.2f * z), new(0.018f, -0.11f, 0.2f * z), 0.044f, 0.038f, leatherDk, Mat.Leather, 0.012f);
            s.Ball(hd, new(0.02f, -0.137f, 0.2f * z), 0.03f, skin, Mat.Skin, 0.01f);
        }

        // ---- legs: dark trousers, leather knee guards, tall boots with fur cuffs
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(th, new(0, 0.0f, 0.095f * z), new(0.012f, -0.39f, 0.095f * z), 0.076f, 0.06f, trousers, Mat.Leather, 0.03f);
            s.Ball(sh, new(0.02f, -0.39f, 0.095f * z), 0.058f, trousers, Mat.Leather, 0.015f);
            // the knee guard: a leather cup with a strap around the leg
            s.Egg(sh, new(0.045f, -0.385f, 0.095f * z), new(0.048f, 0.065f, 0.07f), leather, Mat.Leather, 0.012f);
            s.Egg(sh, new(0.0f, -0.385f, 0.095f * z), new(0.066f, 0.012f, 0.078f), leatherDk, Mat.Leather, 0.008f);
            s.Limb(sh, new(0.01f, -0.39f, 0.095f * z), new(-0.01f, -0.73f, 0.095f * z), 0.056f, 0.046f, trousers, Mat.Leather, 0.015f);
            // the boot, from below the knee to the ground, a torn fur cuff at its top
            s.Limb(sh, new(0.002f, -0.5f, 0.095f * z), new(-0.01f, -0.765f, 0.095f * z), 0.07f, 0.058f, boot, Mat.Leather, 0.012f);
            s.Egg(sh, new(0.0f, -0.5f, 0.095f * z), new(0.08f, 0.034f, 0.082f), fur, Mat.Fur, 0.016f, bump: 0.008f);
            s.Egg(sh, new(0.0f, -0.6f, 0.095f * z), new(0.074f, 0.011f, 0.076f), leatherDk, Mat.Leather, 0.006f);
            s.Egg(sh, new(-0.004f, -0.68f, 0.095f * z), new(0.066f, 0.011f, 0.068f), leatherDk, Mat.Leather, 0.006f);
            s.Block(sh, new(0.066f, -0.6f, 0.095f * z), new(0.006f, 0.014f, 0.016f), 0.003f, steel, Mat.Metal, 0.003f);
            s.Limb(ft, new(-0.035f, -0.775f, 0.095f * z), new(0.125f, -0.785f, 0.095f * z), 0.052f, 0.042f, boot, Mat.Leather, 0.012f);
            s.Limb(ft, new(-0.04f, -0.8f, 0.095f * z), new(0.135f, -0.803f, 0.095f * z), 0.02f, 0.016f, leatherDk, Mat.Leather, 0.008f);
        }

        // ---- the torn tabard hanging from the belt, its hem in ragged points
        {
            (float y, float w, float x)[] rowSpec = { (0.09f, 0.1f, 0.155f), (-0.06f, 0.115f, 0.175f), (-0.22f, 0.125f, 0.185f), (-0.38f, 0.125f, 0.19f), (-0.5f, 0.12f, 0.19f) };
            const int cols = 9;
            var grid = new Vector3[rowSpec.Length][];
            for (int r = 0; r < rowSpec.Length; r++)
            {
                var (y, w, x) = rowSpec[r];
                float fall = r / (float)(rowSpec.Length - 1);
                grid[r] = new Vector3[cols];
                for (int c = 0; c < cols; c++)
                {
                    float a = -1f + 2f * c / (cols - 1);
                    float fold = 0.014f * fall * MathF.Sin(a * MathF.PI * 2f + 0.4f);
                    // a torn, uneven hem: long and short tongues of cloth
                    float hem = r == rowSpec.Length - 1 ? 0.075f * MathF.Sin(a * MathF.PI * 3.3f + 0.7f) - 0.03f * MathF.Abs(a) : 0f;
                    grid[r][c] = new Vector3(x + 0.02f * a * a + fold, y + hem, w * a);
                }
            }
            s.Cloth(new[] { hipsB, hipsB, hipsB, hipsB, hipsB }, grid, slate, new Vector3(1, 0, 0), Mat.Leather, 0.006f, 3);
        }

        // ---- the torn cloak down the back: narrower than a hero's, frayed at the hem
        {
            (float y, float w, float x, float wrap)[] rowSpec =
            {
                (0.5f, 0.13f, -0.1f, 0.05f),
                (0.26f, 0.17f, -0.148f, 0.04f),
                (-0.02f, 0.18f, -0.158f, 0.035f),
                (-0.28f, 0.17f, -0.165f, 0.03f),
                (-0.5f, 0.16f, -0.172f, 0.028f),
            };
            const int cols = 9;
            var grid = new Vector3[rowSpec.Length][];
            for (int r = 0; r < rowSpec.Length; r++)
            {
                var (y, w, x, wrap) = rowSpec[r];
                float fall = r / (float)(rowSpec.Length - 1);
                grid[r] = new Vector3[cols];
                for (int c = 0; c < cols; c++)
                {
                    float a = -1f + 2f * c / (cols - 1);
                    float fold = 0.018f * fall * MathF.Sin(a * MathF.PI * 2f + 0.9f);
                    float hem = r == rowSpec.Length - 1 ? 0.09f * MathF.Sin(a * MathF.PI * 3.6f + 1.3f) + 0.03f * MathF.Sin(a * 11f) - 0.04f * Math.Abs(a) : 0f;
                    grid[r][c] = new Vector3(x + wrap * a * a - fold, y + hem, w * a);
                }
            }
            s.Cloth(new[] { c0, c1, c2, c3, c3 }, grid, slate, new Vector3(-1, 0, 0), Mat.Leather, 0.006f, 3);
        }

        // ---- the longsword, gripped in the right fist
        int handR = s["hand_r"];
        var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
        var sw = new MeshBuilder();
        sw.Append(PropMeshes.Longsword(1.05f, 0.036f, C(0.72f, 0.74f, 0.78f), steel, leatherDk), grip);
        s.Rigid(handR, sw, Mat.Metal);
    }
}
