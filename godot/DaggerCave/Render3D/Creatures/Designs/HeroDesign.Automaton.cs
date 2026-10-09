using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Automaton, on the heroes' rig: a copper engine of a man, green with verdigris at the seams. A barrel chest banded in
/// brass with a furnace glowing behind a barred window and a pressure gauge over the heart, a pair of stacks rising behind
/// the head, great riveted pauldrons, a squared copper head under a brass dome with two porthole eyes lit amber and a
/// grille for a mouth, pistons along the forearms, ball joints at elbow and knee, heavy iron boots, and a cleaver as long
/// as its arm. The lantern at its hip lights the cave, as every hero's does.
/// </summary>
public sealed partial class HeroDesign
{
    private static readonly Color AuCopper = new(0.5f, 0.27f, 0.15f);
    private static readonly Color AuCopperDk = new(0.34f, 0.18f, 0.1f);
    private static readonly Color AuBrass = new(0.58f, 0.45f, 0.2f);
    private static readonly Color AuIron = new(0.15f, 0.15f, 0.17f);
    private static readonly Color AuSteel = new(0.55f, 0.56f, 0.6f);
    private static readonly Color AuVerdigris = new(0.27f, 0.47f, 0.4f);
    private static readonly Color AuFire = new(1f, 0.55f, 0.15f);

    private void SculptAutomaton(Sculptor s)
    {
        int hipsB = s["hips"], spineB = s["spine"], chestB = s["chest"], neckB = s["neck"], headB = s["head"];

        // ================================================================ trunk
        s.Block(hipsB, new(0, 0.02f, 0), new(0.11f, 0.08f, 0.15f), 0.04f, AuIron, Mat.Metal, 0.03f);
        s.Limb(spineB, new(0, 0.05f, 0), new(0, 0.26f, 0), 0.12f, 0.135f, AuCopperDk, Mat.Metal, 0.04f);
        // the barrel of a chest, banded in brass, the bands riveted
        s.Egg(chestB, new(0.01f, 0.335f, 0), new(0.165f, 0.17f, 0.215f), AuCopper, Mat.Metal, 0.05f, bump: 0.0015f);
        foreach (float y in new[] { 0.235f, 0.43f })
        {
            float rx = y < 0.3f ? 0.15f : 0.135f, rz = y < 0.3f ? 0.2f : 0.18f;
            s.Ring(chestB, new(0.01f, y, 0), rx, rz, 0.014f, AuBrass, Mat.Gold, 0.006f);
            for (int k = 0; k < 14; k++)
            {
                float a = k / 14f * Mathf.Tau;
                s.Ball(chestB, new(0.01f + (rx + 0.006f) * MathF.Cos(a), y, (rz + 0.006f) * MathF.Sin(a)), 0.009f, AuSteel, Mat.Metal, 0.003f);
            }
        }
        // verdigris creeping out from under the bands
        s.Egg(chestB, new(0.05f, 0.25f, 0.12f), new(0.08f, 0.025f, 0.07f), AuVerdigris, Mat.Metal, 0.02f);
        s.Egg(chestB, new(-0.06f, 0.42f, -0.13f), new(0.07f, 0.02f, 0.06f), AuVerdigris, Mat.Metal, 0.02f);
        // the furnace behind its barred window
        s.CarveBall(chestB, new(0.2f, 0.32f, 0), 0.07f, 0.01f);
        s.Egg(chestB, new(0.13f, 0.32f, 0), new(0.04f, 0.065f, 0.075f), AuFire, Mat.Ember, 0.02f).Emit = 0.5f;
        for (int k = -1; k <= 1; k++)
            s.Limb(chestB, new(0.16f, 0.385f, 0.032f * k), new(0.16f, 0.255f, 0.032f * k), 0.008f, 0.008f, AuIron, Mat.Metal, 0.004f);
        s.Ring(chestB, new(0.155f, 0.32f, 0), 0.01f, 0.075f, 0.012f, AuBrass, Mat.Gold, 0.004f);
        // a pressure gauge over the heart
        s.Ball(chestB, new(0.14f, 0.405f, 0.105f), 0.032f, AuBrass, Mat.Gold, 0.006f);
        s.Ball(chestB, new(0.165f, 0.405f, 0.11f), 0.022f, new Color(0.85f, 0.82f, 0.72f), Mat.Bone, 0.003f);
        // the stacks behind the head, capped in brass
        foreach (float z in new[] { 0.075f, -0.075f })
        {
            s.Limb(chestB, new(-0.13f, 0.38f, z), new(-0.15f, 0.7f, z * 1.15f), 0.03f, 0.026f, AuIron, Mat.Metal, 0.02f);
            s.Ring(chestB, new(-0.15f, 0.69f, z * 1.15f), 0.032f, 0.032f, 0.01f, AuBrass, Mat.Gold, 0.004f);
            s.CarveBall(chestB, new(-0.15f, 0.725f, z * 1.15f), 0.02f, 0.004f);
        }
        // a valve wheel on the back
        s.Ring(chestB, new(-0.17f, 0.32f, 0), 0.01f, 0.05f, 0.01f, AuBrass, Mat.Gold, 0.004f);
        s.Limb(chestB, new(-0.165f, 0.32f, 0), new(-0.19f, 0.32f, 0), 0.012f, 0.012f, AuIron, Mat.Metal, 0.006f);

        // ================================================================ head
        s.Limb(neckB, new(0.01f, 0.47f, 0), new(0.02f, 0.62f, 0), 0.05f, 0.046f, AuIron, Mat.Metal, 0.02f);
        // (a ribbed collar where neck meets chest)
        s.Ring(neckB, new(0.012f, 0.5f, 0), 0.07f, 0.075f, 0.014f, AuBrass, Mat.Gold, 0.008f);
        s.Block(headB, new(0.02f, 0.705f, 0), new(0.085f, 0.075f, 0.082f), 0.035f, AuCopper, Mat.Metal, 0.02f, bump: 0.001f);
        s.Egg(headB, new(0.012f, 0.775f, 0), new(0.088f, 0.05f, 0.088f), AuBrass, Mat.Gold, 0.015f);
        s.Ring(headB, new(0.016f, 0.765f, 0), 0.09f, 0.088f, 0.01f, AuIron, Mat.Metal, 0.005f);
        // a whistle on the crown
        s.Limb(headB, new(-0.01f, 0.81f, 0), new(-0.025f, 0.865f, 0), 0.012f, 0.01f, AuBrass, Mat.Gold, 0.006f);
        s.Ball(headB, new(-0.026f, 0.872f, 0), 0.016f, AuBrass, Mat.Gold, 0.004f);
        // two portholes for eyes, rimmed in brass, lit from inside
        foreach (float z in new[] { 0.036f, -0.036f })
        {
            s.Ball(headB, new(0.1f, 0.722f, z), 0.026f, AuBrass, Mat.Gold, 0.004f);
            s.Eye(headB, new(0.112f, 0.722f, z), 0.018f, AuFire, 1f);
        }
        // the mouth grille
        for (int k = 0; k < 3; k++)
            s.CarveLimb(headB, new(0.11f, 0.668f - k * 0.013f, -0.035f), new(0.11f, 0.668f - k * 0.013f, 0.035f), 0.004f, 0.004f, 0.002f);
        // bolts at the temples
        foreach (float z in new[] { 0.085f, -0.085f })
            s.Ball(headB, new(0.02f, 0.71f, z), 0.016f, AuSteel, Mat.Metal, 0.004f);

        // ================================================================ arms
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], hd = s["hand" + sfx];
            // a great riveted pauldron
            s.Egg(ua, new(0, 0.475f, 0.2f * z), new(0.095f, 0.075f, 0.085f), AuCopper, Mat.Metal, 0.02f);
            s.Ring(ua, new(0, 0.44f, 0.2f * z), 0.085f, 0.08f, 0.01f, AuBrass, Mat.Gold, 0.005f);
            for (int r = 0; r < 4; r++) s.Ball(ua, new(-0.04f + r * 0.027f, 0.52f, 0.2f * z + 0.05f * z), 0.009f, AuSteel, Mat.Metal, 0.003f);
            s.Limb(ua, new(0, 0.43f, 0.19f * z), new(0, 0.21f, 0.2f * z), 0.048f, 0.044f, AuIron, Mat.Metal, 0.02f);
            // the elbow, a brass ball joint
            s.Ball(fa, new(0, 0.19f, 0.2f * z), 0.05f, AuBrass, Mat.Gold, 0.012f);
            // a heavy copper forearm with a piston along its back
            s.Limb(fa, new(0, 0.17f, 0.2f * z), new(0, -0.02f, 0.2f * z), 0.055f, 0.064f, AuCopper, Mat.Metal, 0.015f);
            s.Ring(fa, new(0, 0.0f, 0.2f * z), 0.066f, 0.066f, 0.01f, AuBrass, Mat.Gold, 0.004f);
            s.Limb(fa, new(-0.06f, 0.16f, 0.2f * z), new(-0.06f, 0.01f, 0.2f * z), 0.012f, 0.012f, AuSteel, Mat.Metal, 0.004f);
            s.Limb(fa, new(-0.06f, 0.16f, 0.2f * z), new(-0.06f, 0.09f, 0.2f * z), 0.019f, 0.019f, AuIron, Mat.Metal, 0.004f);
            // an iron fist
            s.Block(hd, new(0.012f, -0.085f, 0.2f * z), new(0.042f, 0.045f, 0.04f), 0.02f, AuIron, Mat.Metal, 0.012f);
        }

        // ================================================================ legs
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(th, new(0, -0.01f, 0.1f * z), new(0.012f, -0.37f, 0.1f * z), 0.074f, 0.06f, AuCopper, Mat.Metal, 0.03f);
            s.Ring(th, new(0.005f, -0.18f, 0.1f * z), 0.072f, 0.072f, 0.01f, AuBrass, Mat.Gold, 0.004f);
            s.Ball(sh, new(0.02f, -0.39f, 0.1f * z), 0.056f, AuBrass, Mat.Gold, 0.012f);
            s.Limb(sh, new(0.01f, -0.41f, 0.1f * z), new(-0.01f, -0.73f, 0.1f * z), 0.056f, 0.05f, AuCopperDk, Mat.Metal, 0.015f);
            // a greave down the shin
            s.Egg(sh, new(0.04f, -0.54f, 0.1f * z), new(0.03f, 0.13f, 0.055f), AuCopper, Mat.Metal, 0.015f);
            // a heavy iron boot
            s.Block(ft, new(0.035f, -0.775f, 0.1f * z), new(0.085f, 0.04f, 0.055f), 0.02f, AuIron, Mat.Metal, 0.015f);
            s.Ring(ft, new(-0.01f, -0.735f, 0.1f * z), 0.058f, 0.058f, 0.01f, AuBrass, Mat.Gold, 0.004f);
        }

        // ================================================================ the cleaver, down the fist (along the sword's -Y)
        {
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
            var mb = new MeshBuilder();
            // a wrapped haft and a brass ferrule
            mb.Tube(new[] { new Vector3(0, 0.07f, 0), new Vector3(0, -0.13f, 0) }, new[] { 0.019f, 0.021f }, 8, new Color(0.14f, 0.09f, 0.06f));
            mb.Tube(new[] { new Vector3(0, -0.12f, 0), new Vector3(0, -0.16f, 0) }, new[] { 0.026f, 0.026f }, 8, AuBrass);
            // the blade: a broad slab, its spine in line with the haft and its belly out ahead, an edge ground bright
            mb.Box(new Transform3D(Basis.Identity, new Vector3(0.05f, -0.5f, 0)), new Vector3(0.09f, 0.34f, 0.011f), new Color(0.36f, 0.37f, 0.4f));
            mb.Box(new Transform3D(Basis.Identity, new Vector3(0.146f, -0.5f, 0)), new Vector3(0.008f, 0.335f, 0.006f), new Color(0.8f, 0.82f, 0.86f));
            // the squared tip, and the rivets that hold the haft's tang
            mb.Box(new Transform3D(Basis.Identity, new Vector3(0.05f, -0.845f, 0)), new Vector3(0.1f, 0.012f, 0.012f), new Color(0.3f, 0.3f, 0.33f));
            foreach (float y in new[] { -0.2f, -0.26f })
                DecorMeshes.AddSphere(mb, new Vector3(0.0f, y, 0.012f), 0.012f, AuBrass, 4);
            var cl = new MeshBuilder();
            cl.Append(mb, grip);
            s.Rigid(s["hand_r"], cl, Mat.Metal);
        }
        SculptLantern(s, hipsB);
    }
}
