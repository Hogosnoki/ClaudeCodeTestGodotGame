using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The heroes' anatomy kit: arms and legs built from the masses a body is made of (the deltoid capping the shoulder, biceps and
/// triceps, the forearm swelling below the elbow and slimming to a wrist wider than it is deep; the quadriceps and hamstrings,
/// the teardrop above the knee, the kneecap, the two heads of the calf over a slim ankle), not from one tapering tube each with
/// a ball at the joint. <c>def</c> is how much of that shows through what covers it: 1 bare skin, about 0.6 tight leather,
/// about 0.3 loose cloth (which also bunches at the crook of the joint). Rest pose: the arms hang straight down, the legs stand
/// straight, +X forward, the side's sign in <c>z</c>.
/// </summary>
public sealed partial class HeroDesign
{
    /// <summary>
    /// An upper arm and forearm, shoulder (y 0.45) to wrist (y -0.03), at side <paramref name="az"/> (upper arm) and
    /// <paramref name="fz"/> (forearm). <paramref name="upper"/> covers the upper arm and <paramref name="lower"/> the forearm.
    /// </summary>
    private static void ArmForm(Sculptor s, int ua, int fa, float z, float az, float fz, float g, float def,
        Color upper, Mat upperMat, Color lower, Mat lowerMat, float bump = 0f, bool cap = true)
    {
        float m = 0.55f + 0.45f * def;          // how far the muscle masses stand proud
        float bl = 0.03f - 0.012f * def;         // and how sharply they're drawn (loose cloth melts them together)
        // the shoulder: the deltoid draped over it, a cap taller than it is wide, melting into the arm (never a ball)
        if (cap) s.Egg(ua, new(0.004f, 0.415f, az + 0.008f * z), new Vector3(0.06f, 0.078f, 0.05f) * g, upper, upperMat, 0.04f, bump: bump);
        // the shaft, slimming to the elbow so the joint reads
        s.Limb(ua, new(0, 0.43f, az), new(0, 0.215f, az + (fz - az) * 0.5f), 0.046f * g, 0.035f * g, upper, upperMat, 0.026f, bump);
        // biceps in front, triceps behind (the longer, fuller mass)
        s.Egg(ua, new(0.016f + 0.004f * def, 0.335f, az), new Vector3(0.026f + 0.01f * m, 0.068f, 0.032f + 0.004f * m) * g, upper, upperMat, bl, bump: bump);
        s.Egg(ua, new(-0.02f - 0.004f * def, 0.355f, az), new Vector3(0.028f + 0.009f * m, 0.08f, 0.034f + 0.005f * m) * g, upper, upperMat, bl, bump: bump);
        // the elbow: the bony point behind, nothing round
        s.Egg(fa, new(-0.024f, 0.198f, fz), new Vector3(0.019f, 0.024f, 0.022f) * g, lower, lowerMat, 0.016f, bump: bump);
        // the forearm: full below the elbow (the flexors and the long muscle over the top), slimming to the wrist
        s.Limb(fa, new(0, 0.185f, fz), new(0.003f, -0.028f, fz), 0.039f * g, 0.026f * g, lower, lowerMat, 0.022f, bump);
        s.Egg(fa, new(0.006f, 0.124f, fz), new Vector3(0.03f + 0.01f * m, 0.07f, 0.034f + 0.009f * m) * g, lower, lowerMat, bl + 0.004f, bump: bump);
        // the wrist: wider across than front to back
        s.Egg(fa, new(0.004f, -0.022f, fz), new Vector3(0.021f, 0.02f, 0.029f) * g, lower, lowerMat, 0.012f, bump: bump);
        // loose cloth gathers in a fold at the crook of the elbow
        if (def < 0.45f)
            s.Ring(fa, new(0.006f, 0.19f, fz), 0.041f * g, 0.043f * g, 0.009f, upper, upperMat, 0.012f, tiltDeg: 14f, bump: bump);
    }

    /// <summary>
    /// A thigh and shin, hip (y 0) to ankle (y -0.75), at side <paramref name="lz"/>, in <paramref name="col"/> (trousers, or bare
    /// skin, or a suit); the knee in <paramref name="kneeCol"/> (a guard, or the same).
    /// </summary>
    private static void LegForm(Sculptor s, int th, int sh, float z, float lz, float g, float def, Color col, Mat mat,
        Color kneeCol, Mat kneeMat, float bump = 0f)
    {
        float m = 0.55f + 0.45f * def;
        float bl = 0.034f - 0.012f * def;
        // the thigh: full at the top, slimming to the knee
        s.Limb(th, new(0, 0.0f, lz), new(0.012f, -0.372f, lz), 0.07f * g, 0.047f * g, col, mat, 0.032f, bump);
        // the quadriceps in front, the hamstrings behind, the inner thigh toward the other leg
        s.Egg(th, new(0.028f, -0.17f, lz), new Vector3(0.036f + 0.012f * m, 0.13f, 0.044f + 0.01f * m) * g, col, mat, bl, bump: bump);
        s.Egg(th, new(-0.024f, -0.14f, lz), new Vector3(0.034f + 0.01f * m, 0.12f, 0.042f + 0.008f * m) * g, col, mat, bl, bump: bump);
        s.Egg(th, new(0.002f, -0.085f, lz - 0.02f * z), new Vector3(0.046f, 0.1f, 0.036f) * g, col, mat, 0.034f, bump: bump);
        // the teardrop above the knee, on the inside
        s.Egg(th, new(0.024f, -0.322f, lz - 0.016f * z), new Vector3(0.022f + 0.008f * m, 0.042f, 0.024f + 0.006f * m) * g, col, mat, bl, bump: bump);
        // the knee: its cap in front, flat at the sides (no ball)
        s.Egg(sh, new(0.036f, -0.392f, lz), new Vector3(0.022f, 0.028f, 0.027f) * g, kneeCol, kneeMat, 0.014f, bump: bump);
        // the shin, slimming to a narrow ankle
        s.Limb(sh, new(0.01f, -0.4f, lz), new(-0.008f, -0.728f, lz), 0.044f * g, 0.028f * g, col, mat, 0.022f, bump);
        // the calf: its outer head high, the inner a little lower
        s.Egg(sh, new(-0.024f, -0.5f, lz + 0.004f * z), new Vector3(0.03f + 0.012f * m, 0.092f, 0.036f + 0.007f * m) * g, col, mat, bl, bump: bump);
        s.Egg(sh, new(-0.02f, -0.53f, lz - 0.012f * z), new Vector3(0.026f + 0.01f * m, 0.07f, 0.03f + 0.006f * m) * g, col, mat, bl, bump: bump);
        // loose trousers bunch behind the knee
        if (def < 0.45f)
            s.Ring(sh, new(-0.004f, -0.395f, lz), 0.05f * g, 0.05f * g, 0.01f, col, mat, 0.014f, tiltDeg: -12f, bump: bump);
    }

    /// <summary>
    /// A boot from mid-shin down: the shaft over the calf, a turned-down cuff, the ankle, the instep, a rounded toe box, a heel block and
    /// a sole standing a little proud all round (the sole's underside at the floor, y -0.821).
    /// </summary>
    private static void BootForm(Sculptor s, int sh, int ft, float lz, float g, Color col, Color dk, Color cuff, Mat cuffMat,
        float top = -0.52f, float bump = 0.0012f)
    {
        // the shaft, following the calf down to the ankle
        s.Limb(sh, new(0.002f, top, lz), new(-0.008f, -0.742f, lz), 0.052f * g, 0.043f * g, col, Mat.Leather, 0.014f, bump);
        s.Egg(sh, new(-0.016f, top - 0.04f, lz), new Vector3(0.05f, 0.06f, 0.05f) * g, col, Mat.Leather, 0.02f, bump: bump);
        // the cuff: a turned-down band, standing off the shaft
        s.Ring(sh, new(0.002f, top + 0.006f, lz), 0.058f * g, 0.058f * g, 0.012f, cuff, cuffMat, 0.006f, bump: bump);
        // the ankle and the instep
        s.Egg(ft, new(0.0f, -0.758f, lz), new Vector3(0.05f, 0.04f, 0.047f) * g, col, Mat.Leather, 0.016f, bump: bump);
        s.Egg(ft, new(0.052f, -0.78f, lz), new Vector3(0.074f, 0.034f, 0.044f) * g, col, Mat.Leather, 0.018f, bump: bump);
        // the toe box: rounded, a little upturned
        s.Egg(ft, new(0.112f, -0.792f, lz), new Vector3(0.042f, 0.026f, 0.041f) * g, col, Mat.Leather, 0.014f, bump: bump);
        // the heel: a block under the back of the foot
        s.Block(ft, new(-0.04f, -0.806f, lz), new Vector3(0.03f, 0.014f, 0.034f) * g, 0.006f, dk, Mat.Leather, 0.008f);
        // the sole, a welt just proud of the upper (it follows the foot: it isn't a plank under it)
        s.Block(ft, new(0.042f, -0.813f, lz), new Vector3(0.09f, 0.0075f, 0.04f) * g, 0.012f, dk, Mat.Leather, 0.016f);
    }

    /// <summary>
    /// A shoulder guard of overlapping plates (leather or steel) over the deltoid: a domed top plate and two lames below it, each
    /// flaring out a little further, so the shoulder reads as armour laid on it, not as a ball joint.
    /// </summary>
    private static void PauldronForm(Sculptor s, int ua, float z, float az, float g, Color col, Color edge, Mat mat, float bump = 0.001f)
    {
        s.Egg(ua, new(0.002f, 0.468f, az + 0.008f * z), new Vector3(0.078f, 0.042f, 0.074f) * g, col, mat, 0.016f, new(0, 0, -14f * z), bump);
        s.Egg(ua, new(0.0f, 0.43f, az + 0.02f * z), new Vector3(0.074f, 0.026f, 0.07f) * g, col, mat, 0.008f, new(0, 0, -22f * z), bump);
        s.Egg(ua, new(-0.002f, 0.402f, az + 0.028f * z), new Vector3(0.068f, 0.022f, 0.064f) * g, edge, mat, 0.008f, new(0, 0, -28f * z), bump);
    }
}
