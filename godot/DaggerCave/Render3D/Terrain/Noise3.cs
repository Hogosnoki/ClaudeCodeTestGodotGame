using System;
using System.Runtime.CompilerServices;

namespace DaggerCave;

/// <summary>
/// Improved (Perlin 2002) gradient noise in 3D, plus fractal and ridged sums. Used to sculpt
/// rock surfaces and creature skin; deterministic for a given seed.
/// </summary>
public sealed class Noise3
{
    private readonly int[] _p = new int[512];

    public Noise3(int seed)
    {
        var perm = new int[256];
        for (int i = 0; i < 256; i++) perm[i] = i;
        var rng = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (perm[i], perm[j]) = (perm[j], perm[i]);
        }
        for (int i = 0; i < 512; i++) _p[i] = perm[i & 255];
    }

    public static readonly Noise3 Shared = new(1337);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Grad(int h, float x, float y, float z)
    {
        h &= 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float t, float a, float b) => a + t * (b - a);

    /// <summary>Gradient noise, roughly in [-1, 1].</summary>
    public float Sample(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float xf = x - xi, yf = y - yi, zf = z - zi;
        int X = xi & 255, Y = yi & 255, Z = zi & 255;
        float u = Fade(xf), v = Fade(yf), w = Fade(zf);
        var p = _p;
        int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z;
        int B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;
        return Lerp(w,
            Lerp(v, Lerp(u, Grad(p[AA], xf, yf, zf), Grad(p[BA], xf - 1, yf, zf)),
                    Lerp(u, Grad(p[AB], xf, yf - 1, zf), Grad(p[BB], xf - 1, yf - 1, zf))),
            Lerp(v, Lerp(u, Grad(p[AA + 1], xf, yf, zf - 1), Grad(p[BA + 1], xf - 1, yf, zf - 1)),
                    Lerp(u, Grad(p[AB + 1], xf, yf - 1, zf - 1), Grad(p[BB + 1], xf - 1, yf - 1, zf - 1))));
    }

    /// <summary>Fractal sum, normalised to roughly [-1, 1].</summary>
    public float Fbm(float x, float y, float z, int octaves, float lacunarity = 2.03f, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Sample(x, y, z) * amp;
            norm += amp;
            amp *= gain;
            x *= lacunarity; y *= lacunarity; z *= lacunarity;
            // decorrelate octaves
            x += 17.13f; y += 5.71f; z += 11.39f;
        }
        return sum / norm;
    }

    /// <summary>Ridged fractal: sharp creases where the noise crosses zero (cracks, veins, ridges). Roughly [0, 1].</summary>
    public float Ridged(float x, float y, float z, int octaves, float lacunarity = 2.1f, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            float n = 1f - MathF.Abs(Sample(x, y, z));
            sum += n * n * amp;
            norm += amp;
            amp *= gain;
            x *= lacunarity; y *= lacunarity; z *= lacunarity;
            x += 31.7f; y += 13.3f; z += 7.9f;
        }
        return sum / norm;
    }
}
