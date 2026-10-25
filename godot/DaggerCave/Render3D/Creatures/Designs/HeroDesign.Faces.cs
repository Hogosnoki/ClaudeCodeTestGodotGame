using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The heroes' faces. Each is built on the Swordsman's carefully placed anatomy (the cranium, the face's mass, the jaw and
/// chin, the cheekbones, the sockets and lids, the brow ridge, the nose's bridge, tip and wings, then the brows, lids'
/// lines, lips and mouth laid on the skin as fine strands), moved, scaled and reshaped by a <see cref="Face"/> of the
/// hero's own: a broad, square, bearded Warden; a gaunt old Elementalist with a hooked nose and a long grey beard; a calm,
/// soft-featured Aegis; a lean, sharp, war-painted Shape Shifter; a Rogue with high cheekbones and red eyes over the mask.
/// Every eye has a white, an iris and a pupil.
/// </summary>
public sealed partial class HeroDesign
{
    private enum Beard { None, Stubble, Short, Long }

    /// <summary>A face's shape (1 is the Swordsman's in every measure).</summary>
    private struct Face
    {
        /// <summary>Where it sits relative to the Swordsman's (metres), how big it is, and how wide (sideways only).</summary>
        public Vector3 Offset;
        public float Scale, Wide;
        public Color Skin, Hair, Iris, LipUp, LipLow;
        public float EyeGlow;
        /// <summary>0 a soft, rounded jaw, 1 a square one; the chin's size; how prominent the cheekbones are, and how far up (metres).</summary>
        public float Jaw, Chin, Cheek, CheekY;
        /// <summary>The brow ridge; the nose's length, the bump on its bridge (a break or a hook), its tip and its width.</summary>
        public float Ridge, NoseLen, NoseHook, NoseTip, NoseWide;
        /// <summary>The lips' fullness, the mouth's width, the corners' lift (up a smile, down stern).</summary>
        public float Lips, Mouth, Smile;
        /// <summary>The brows' inner ends raised (kind, wise) or drawn down (stern, intent), and their thickness; how deep the eyes are set.</summary>
        public float BrowTilt, BrowThick, Socket;
        /// <summary>Lines of age: the folds from the nose to the mouth, the creases on the forehead.</summary>
        public float Age;
        public bool Cranium, Ears;
        public Beard Beard;
        public Color BeardCol;
        /// <summary>Paint across the eyes and claw marks down a cheek (the Shape Shifter's).</summary>
        public bool WarPaint;
    }

    private static Face BaseFace(Color skin) => new()
    {
        Offset = new Vector3(0.012f, -0.034f, 0f), Scale = 0.95f, Wide = 1f,
        Skin = skin, Hair = BrHair, Iris = new Color(0.09f, 0.065f, 0.05f),
        // (the lips a little darker and redder than the skin, no more)
        LipUp = skin.Darkened(0.34f).Lerp(new Color(0.45f, 0.18f, 0.16f), 0.12f), LipLow = skin.Darkened(0.24f).Lerp(new Color(0.5f, 0.22f, 0.18f), 0.12f), EyeGlow = 0.15f,
        Jaw = 1f, Chin = 1f, Cheek = 1f, Ridge = 1f, NoseLen = 1f, NoseTip = 1f, NoseWide = 1f,
        Lips = 1f, Mouth = 1f, BrowThick = 1f, Socket = 1f, Cranium = true,
        BeardCol = BrHair,
    };

    /// <summary>The point the face scales about (the Swordsman's mid-face).</summary>
    private static readonly Vector3 FacePivot = new(0.05f, 0.72f, 0f);

    private static Vector3 FaceAt(in Face f, float x, float y, float z) => FacePivot + (new Vector3(x, y, z * f.Wide) - FacePivot) * f.Scale + f.Offset;

    /// <summary>A point of the hero's face back in the Swordsman's measure (for the paint).</summary>
    private static Vector3 FaceFrom(in Face f, Vector3 p)
    {
        var q = FacePivot + (p - f.Offset - FacePivot) / f.Scale;
        return new Vector3(q.X, q.Y, q.Z / f.Wide);
    }

    /// <summary>An eye: the white, the iris and the pupil, looking along <paramref name="look"/>.</summary>
    private static void SculptEye(Sculptor s, int bone, Vector3 c, float r, Color iris, float glow, Vector3 look)
    {
        // (the white kept warm and dim, and the iris large, so the eye reads as an eye at a distance and not a stare)
        s.Eye(bone, c, r, new Color(0.52f, 0.48f, 0.43f), 0f);
        s.Eye(bone, c + look * r * 0.52f, r * 0.7f, iris, glow);
        s.Eye(bone, c + look * r * 0.98f, r * 0.32f, new Color(0.015f, 0.012f, 0.012f), 0f);
    }

    /// <summary>
    /// Sculpts a face on the head. What's in front of it (a hood's carved opening, a helm) should already be sculpted, with
    /// its opening carved, since the face's fine strands are laid on whatever skin stands in front when they're placed;
    /// anything over the face (a mask) goes on after.
    /// </summary>
    private void SculptFace(Sculptor s, int headB, Face f)
    {
        Vector3 T(float x, float y, float z) => FaceAt(f, x, y, z);
        float sc = f.Scale;
        var skin = f.Skin;
        if (f.Cranium) s.Egg(headB, T(-0.004f, 0.78f, 0), new Vector3(0.098f, 0.1f, 0.08f * f.Wide) * sc, skin, Mat.Skin, 0.03f);
        s.Egg(headB, T(0.04f, 0.735f, 0), new Vector3(0.076f, 0.092f, 0.06f * f.Wide) * sc, skin, Mat.Skin, 0.03f);
        // the jaw: square for a hard face, rounded for a soft one
        if (f.Jaw >= 0.5f)
            s.Block(headB, T(0.05f, 0.676f, 0), new Vector3(0.044f, 0.03f, 0.04f * f.Wide * (0.8f + 0.25f * f.Jaw)) * sc, 0.023f * sc, skin, Mat.Skin, 0.025f);
        else
            s.Egg(headB, T(0.055f, 0.68f, 0), new Vector3(0.052f, 0.036f, 0.04f * f.Wide * (0.85f + 0.2f * f.Jaw)) * sc, skin, Mat.Skin, 0.025f);
        s.Ball(headB, T(0.098f, 0.658f, 0), 0.025f * sc * f.Chin, skin, Mat.Skin, 0.02f);
        float jawR = 0.025f * (0.78f + 0.25f * f.Jaw);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            s.Limb(headB, T(0.0f, 0.715f, 0.058f * z), T(0.05f, 0.662f, 0.04f * z * (0.75f + 0.3f * f.Jaw)), jawR * sc, jawR * 1.04f * sc, skin, Mat.Skin, 0.025f);
            s.Egg(headB, T(0.072f, 0.725f + f.CheekY, 0.042f * z), new Vector3(0.027f, 0.022f, 0.02f) * sc * f.Cheek, skin, Mat.Skin, 0.02f);
            if (f.Ears) s.Egg(headB, T(0.0f, 0.752f, 0.079f * z), new Vector3(0.017f, 0.033f, 0.01f) * sc, skin, Mat.Skin, 0.012f, new(0, 0, 8f * z));
            // the socket, with the lids over and under the eye
            s.CarveBall(headB, T(0.1f, 0.762f, 0.03f * z), 0.0155f * sc * f.Socket, 0.012f);
            s.Limb(headB, T(0.1f, 0.7695f, 0.017f * z), T(0.095f, 0.768f, 0.045f * z), 0.0072f * sc, 0.0062f * sc, skin, Mat.Skin, 0.005f);
            s.Limb(headB, T(0.1f, 0.7535f, 0.018f * z), T(0.096f, 0.7545f, 0.043f * z), 0.0055f * sc, 0.005f * sc, skin, Mat.Skin, 0.005f);
        }
        // the brow ridge and the nose
        float ridge = f.Ridge;
        s.Limb(headB, T(0.09f, 0.784f, -0.05f), T(0.097f + 0.004f * (ridge - 1f), 0.787f, 0), 0.0095f * sc * ridge, 0.0105f * sc * ridge, skin, Mat.Skin, 0.014f);
        s.Limb(headB, T(0.097f + 0.004f * (ridge - 1f), 0.787f, 0), T(0.09f, 0.784f, 0.05f), 0.0105f * sc * ridge, 0.0095f * sc * ridge, skin, Mat.Skin, 0.014f);
        float nl = f.NoseLen - 1f;
        var tip = T(0.126f + 0.012f * nl, 0.717f - 0.014f * nl, 0);
        s.Limb(headB, T(0.098f, 0.77f, 0), T(0.118f + 0.012f * nl, 0.724f - 0.014f * nl, 0), 0.0095f * sc * f.NoseWide, 0.011f * sc * f.NoseWide, skin, Mat.Skin, 0.02f);
        if (f.NoseHook > 0f) s.Ball(headB, T(0.111f + 0.006f * nl + 0.0035f * f.NoseHook, 0.749f - 0.006f * nl, 0), 0.0085f * sc * (0.8f + 0.25f * f.NoseHook), skin, Mat.Skin, 0.01f);
        s.Ball(headB, tip, 0.011f * sc * f.NoseTip, skin, Mat.Skin, 0.012f);
        s.Egg(headB, T(0.114f + 0.008f * nl, 0.709f - 0.012f * nl, 0.01f * f.NoseWide), new Vector3(0.01f, 0.009f, 0.009f) * sc * f.NoseWide, skin, Mat.Skin, 0.01f);
        s.Egg(headB, T(0.114f + 0.008f * nl, 0.709f - 0.012f * nl, -0.01f * f.NoseWide), new Vector3(0.01f, 0.009f, 0.009f) * sc * f.NoseWide, skin, Mat.Skin, 0.01f);
        // the eyes, turned in a touch toward what's ahead
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            SculptEye(s, headB, T(0.097f, 0.7615f, 0.03f * z), 0.0092f * sc, f.Iris, f.EyeGlow, new Vector3(1f, 0f, -0.12f * z).Normalized());
        }

        // the fine features, laid on the skin where it stands (brows, lids' lines, lips, mouth, nostrils, lines of age)
        Vector3[] Along(float z0, float z1, int n, Func<float, float> yAt, float lift)
        {
            var pts = new Vector3[n];
            for (int j = 0; j < n; j++)
            {
                float t = j / (float)(n - 1);
                var q = T(0f, yAt(t), z0 + (z1 - z0) * t);
                pts[j] = new Vector3(s.SurfaceX(q.Y, q.Z) + lift * sc, q.Y, q.Z);
            }
            return pts;
        }
        float[] Rad(float[] r, float m) { var o = new float[r.Length]; for (int j = 0; j < r.Length; j++) o[j] = r[j] * m * sc; return o; }
        var lashes = f.Hair.Darkened(0.35f);
        var mouthLine = new Color(0.12f, 0.06f, 0.05f);
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            float tilt = f.BrowTilt;
            Strand(s, headB, Along(0.005f * z, 0.062f * z, 7, t => 0.7835f + 0.006f * MathF.Sin(t * 2.6f) - 0.004f * t * t + 0.0055f * tilt * (1f - t) * (1f - t), -0.0035f),
                Rad(new[] { 0.0074f, 0.0076f, 0.0072f, 0.0066f, 0.0056f, 0.0042f, 0.0021f }, f.BrowThick), f.Hair, HairMat);
            Strand(s, headB, Along(0.012f * z, 0.049f * z, 6, t => 0.7705f - 0.003f * t, 0.0004f),
                Rad(new[] { 0.0028f, 0.0034f, 0.0034f, 0.003f, 0.0024f, 0.0012f }, 1f), lashes, Mat.Leather);
            Strand(s, headB, Along(0.014f * z, 0.044f * z, 5, t => 0.7525f - 0.0012f * MathF.Sin(t * 3.14f), 0.0003f),
                Rad(new[] { 0.0008f, 0.0012f, 0.0012f, 0.0011f, 0.0006f }, 1f), skin.Darkened(0.16f), Mat.Skin);
        }
        float mw = f.Mouth, smile = f.Smile;
        Strand(s, headB, Along(-0.023f * mw, 0.023f * mw, 9, t => 0.6785f + 0.0012f * MathF.Cos((t - 0.5f) * 6.28f) - 0.004f * MathF.Pow(2f * t - 1f, 2f) + 0.0035f * smile * MathF.Pow(2f * t - 1f, 4f), -0.0025f),
            Rad(new[] { 0.0035f, 0.0048f, 0.0055f, 0.0058f, 0.0062f, 0.0058f, 0.0055f, 0.0048f, 0.0035f }, f.Lips), f.LipUp, Mat.Skin);
        Strand(s, headB, Along(-0.018f * mw, 0.018f * mw, 9, t => 0.6655f - 0.002f * MathF.Pow(2f * t - 1f, 2f) - 0.0015f * (f.Lips - 1f) + 0.0025f * smile * MathF.Pow(2f * t - 1f, 4f), -0.0025f),
            Rad(new[] { 0.0035f, 0.0052f, 0.006f, 0.0066f, 0.0068f, 0.0066f, 0.006f, 0.0052f, 0.0035f }, f.Lips), f.LipLow, Mat.Skin);
        Strand(s, headB, Along(-0.0235f * mw, 0.0235f * mw, 9, t => 0.6722f + 0.004f * smile * MathF.Pow(2f * t - 1f, 4f), -0.0005f),
            Rad(new[] { 0.0012f, 0.0018f, 0.0021f, 0.0022f, 0.0022f, 0.0022f, 0.0021f, 0.0018f, 0.0012f }, 1f), mouthLine, Mat.Leather);
        // the philtrum, the groove from the nose to the lip
        Strand(s, headB, Along(0f, 0f, 3, t => 0.699f - 0.016f * t, 0.0002f), Rad(new[] { 0.0016f, 0.0018f, 0.0014f }, 1f), skin.Darkened(0.12f), Mat.Skin);
        float nl2 = f.NoseLen - 1f;
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            Strand(s, headB, Along(0.004f * z * f.NoseWide, 0.012f * z * f.NoseWide, 4, t => 0.7045f - 0.012f * nl2, 0.0f), Rad(new[] { 0.0022f, 0.0035f, 0.0035f, 0.0015f }, 1f), mouthLine, Mat.Leather);
            if (f.Age > 0f)
            {
                // the folds from the nostrils' wings to the corners of the mouth
                Strand(s, headB, Along(0.017f * z, 0.027f * z * mw, 5, t => 0.704f - 0.03f * t, 0.0002f), Rad(new[] { 0.0012f, 0.0022f, 0.0026f, 0.0022f, 0.0012f }, f.Age), skin.Darkened(0.22f), Mat.Skin);
            }
        }
        if (f.Age > 0.5f)
            for (int j = 0; j < 2; j++)
            {
                float y0 = 0.803f + 0.009f * j;
                Strand(s, headB, Along(-0.032f, 0.032f, 7, t => y0 + 0.002f * MathF.Sin(t * 3.14f), 0.0001f), Rad(new[] { 0.0006f, 0.0012f, 0.0015f, 0.0016f, 0.0015f, 0.0012f, 0.0006f }, f.Age), skin.Darkened(0.2f), Mat.Skin);
            }

        // the beard
        var bc = f.BeardCol;
        if (f.Beard == Beard.Stubble)
            s.Limb(headB, T(0.094f, 0.64f, -0.018f), T(0.094f, 0.64f, 0.018f), 0.011f * sc, 0.011f * sc, skin, Mat.Skin, 0.012f, 0.0014f);
        if (f.Beard >= Beard.Short)
        {
            // a close beard following the jaw to the chin, and a moustache over the lip, its ends falling past the corners
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                s.Limb(headB, T(0.012f, 0.716f, 0.06f * z), T(0.062f, 0.656f, 0.038f * z), (jawR + 0.006f) * sc, (jawR + 0.008f) * sc, bc, HairMat, 0.012f, 0.0025f);
                Strand(s, headB, Along(0.003f * z, 0.026f * z * mw, 6, t => 0.6885f - 0.003f * t - 0.012f * t * t, 0.002f), Rad(new[] { 0.0042f, 0.0052f, 0.0055f, 0.005f, 0.004f, 0.0025f }, 1f), bc, HairMat);
            }
            s.Ball(headB, T(0.096f, 0.648f, 0), 0.031f * sc * f.Chin, bc, HairMat, 0.014f, 0.0025f);
        }
        if (f.Beard == Beard.Long)
        {
            // and long, falling from the chin to the chest in a tapering point
            s.Limb(headB, T(0.09f, 0.645f, 0), T(0.112f, 0.56f, 0), 0.032f * sc, 0.02f * sc, bc, HairMat, 0.016f, 0.003f);
            s.Limb(headB, T(0.112f, 0.56f, 0), T(0.12f, 0.5f, 0.004f), 0.02f * sc, 0.006f * sc, bc, HairMat, 0.012f, 0.002f);
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                s.Limb(headB, T(0.05f, 0.66f, 0.04f * z), T(0.085f, 0.58f, 0.022f * z), 0.022f * sc, 0.012f * sc, bc, HairMat, 0.014f, 0.003f);
            }
        }

        // the skin's colour: a flush over the cheeks, shadow about the eyes and under the brow, the lips' warmth, the stubble's
        // shadow, and paint where it's worn
        var face = f;
        s.Paints.Add((p, c, mat) =>
        {
            if (mat != Mat.Skin || p.Y < 0.6f || p.X < -0.03f) return c;
            var q = FaceFrom(face, p);
            float az = MathF.Abs(q.Z);
            float cheek = Sm(0.03f, 0.0f, new Vector2(q.Y - 0.722f, az - 0.045f).Length() - 0.01f) * Sm(0.05f, 0.08f, q.X);
            c = c.Lerp(new Color(0.62f, 0.3f, 0.24f), cheek * 0.22f);
            float eye = Sm(0.024f, 0.008f, new Vector3(q.X - 0.1f, q.Y - 0.765f, az - 0.031f).Length());
            c = c.Darkened(eye * (0.08f + 0.08f * face.Socket));
            float mouth = Sm(0.03f, 0.008f, new Vector2(q.Y - 0.672f, az * 0.6f).Length()) * Sm(0.08f, 0.1f, q.X);
            c = c.Lerp(new Color(0.5f, 0.26f, 0.22f), mouth * 0.25f);
            if (face.Beard == Beard.Stubble || face.Beard >= Beard.Short)
            {
                float low = Sm(0.705f, 0.665f, q.Y) * Sm(0.0f, 0.05f, q.X);
                float lip = Sm(0.03f, 0.012f, new Vector2((q.Y - 0.69f) * 1.6f, az).Length());
                c = c.Lerp(face.BeardCol.Darkened(0.3f), Mathf.Max(low, lip) * 0.42f);
            }
            if (face.WarPaint)
            {
                // a band of charcoal across the eyes, temple to temple, and three claw marks down the left cheek
                float band = Sm(0.014f, 0.008f, MathF.Abs(q.Y - 0.763f)) * Sm(0.07f, 0.06f, az);
                float claws = 0f;
                if (q.Z < -0.02f)
                    for (int j = 0; j < 3; j++)
                    {
                        float lz = -0.03f - 0.011f * j + (q.Y - 0.735f) * 0.25f;
                        claws = Mathf.Max(claws, Sm(0.0032f, 0.0012f, MathF.Abs(q.Z - lz)) * Sm(0.745f, 0.738f, q.Y) * Sm(0.682f, 0.692f, q.Y));
                    }
                c = c.Lerp(new Color(0.06f, 0.06f, 0.07f), Mathf.Max(band * 0.85f, claws * 0.8f));
            }
            return c;
        });
    }
}
