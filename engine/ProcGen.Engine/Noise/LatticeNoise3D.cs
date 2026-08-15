using System;

namespace ProcGen.Engine.Noise
{
    /// <summary>
    /// Integer-lattice gradient noise (Ken Perlin's "improved noise" construction), extended to
    /// three axes: X, Y, and Transformation. Transformation is interpolated exactly like X/Y --
    /// it is just a third lattice axis -- which is what makes moving through it gradually morph
    /// shapes instead of jumping between unrelated results.
    ///
    /// Determinism / cross-platform note:
    /// Gradient selection is driven entirely by an integer avalanche hash (unchecked 32-bit
    /// arithmetic, which .NET guarantees wraps identically on every supported platform/architecture).
    /// No trigonometric or transcendental functions (Sin/Cos/etc, whose libm implementations can
    /// differ subtly between platforms) are used anywhere in this class, and no fused-multiply-add
    /// is used (.NET does not auto-contract +/- and * into FMA; only an explicit
    /// Math.FusedMultiplyAdd call would). The only floating-point operations are +, -, *, / on
    /// `double`, which IEEE 754 specifies exactly and which .NET's JIT does not reorder across
    /// these platforms. That combination is what makes "same inputs -> bit-identical output"
    /// safe to promise across the tool and the game, and across OS/CPU targets.
    /// </summary>
    public static class LatticeNoise3D
    {
        // 12 standard cube-edge gradient directions (Perlin's reference set), padded to 16 entries
        // by repeating 4 of them so gradient selection can use a cheap "& 15" mask instead of a
        // modulo. This is the same padding trick used in Perlin's own reference implementation.
        private static readonly double[] GradX = { 1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0, 1, -1, 0, 0 };
        private static readonly double[] GradY = { 1, 1, -1, -1, 0, 0, 0, 0, 1, -1, 1, -1, 1, -1, -1, -1 };
        private static readonly double[] GradZ = { 0, 0, 0, 0, 1, 1, -1, -1, 1, 1, -1, -1, 0, 0, 1, -1 };

        private const uint GradientMask = 15;

        /// <summary>
        /// Deterministic 32-bit avalanche hash over three lattice-integer coordinates plus a seed.
        /// Pure integer ops only -- identical results on every platform .NET runs on.
        /// </summary>
        private static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h = Mix(h ^ (uint)x);
                h = Mix(h ^ (uint)y);
                h = Mix(h ^ (uint)z);
                return h;
            }
        }

        // MurmurHash3-style 32-bit finalizer: cheap, well-distributed, integer-only.
        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7feb352dU;
                h ^= h >> 15;
                h *= 0x846ca68bU;
                h ^= h >> 16;
                return h;
            }
        }

        private static void GradientAt(int ix, int iy, int iz, int seed, out double gx, out double gy, out double gz)
        {
            uint h = Hash(ix, iy, iz, seed) & GradientMask;
            gx = GradX[h];
            gy = GradY[h];
            gz = GradZ[h];
        }

        /// <summary>Quintic fade (6t^5 - 15t^4 + 10t^3): zero first AND second derivative at t=0/1.</summary>
        private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

        private static double Lerp(double a, double b, double t) => a + t * (b - a);

        private static double GradDot(int ix, int iy, int iz, double x, double y, double z, int seed)
        {
            GradientAt(ix, iy, iz, seed, out double gx, out double gy, out double gz);
            double dx = x - ix, dy = y - iy, dz = z - iz;
            return gx * dx + gy * dy + gz * dz;
        }

        /// <summary>
        /// Single-octave 3D gradient noise. Raw output range is approximately [-1, 1].
        /// </summary>
        public static double Sample(double x, double y, double z, int seed)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int z0 = (int)Math.Floor(z);
            int x1 = x0 + 1, y1 = y0 + 1, z1 = z0 + 1;

            double tx = Fade(x - x0);
            double ty = Fade(y - y0);
            double tz = Fade(z - z0);

            double n000 = GradDot(x0, y0, z0, x, y, z, seed);
            double n100 = GradDot(x1, y0, z0, x, y, z, seed);
            double n010 = GradDot(x0, y1, z0, x, y, z, seed);
            double n110 = GradDot(x1, y1, z0, x, y, z, seed);
            double n001 = GradDot(x0, y0, z1, x, y, z, seed);
            double n101 = GradDot(x1, y0, z1, x, y, z, seed);
            double n011 = GradDot(x0, y1, z1, x, y, z, seed);
            double n111 = GradDot(x1, y1, z1, x, y, z, seed);

            double nx00 = Lerp(n000, n100, tx);
            double nx10 = Lerp(n010, n110, tx);
            double nx01 = Lerp(n001, n101, tx);
            double nx11 = Lerp(n011, n111, tx);

            double nxy0 = Lerp(nx00, nx10, ty);
            double nxy1 = Lerp(nx01, nx11, ty);

            return Lerp(nxy0, nxy1, tz);
        }

        /// <summary>
        /// Fractal Brownian Motion: sums octaves of <see cref="Sample"/> at increasing frequency
        /// and decreasing amplitude, normalized by total amplitude so the result stays within
        /// approximately [-1, 1] regardless of octave count.
        /// </summary>
        public static double SampleFbm(double x, double y, double z, int seed, int octaves, double frequency, double persistence, double lacunarity)
        {
            if (octaves < 1) octaves = 1;

            double amplitude = 1.0;
            double freq = frequency;
            double sum = 0.0;
            double maxAmplitude = 0.0;

            for (int o = 0; o < octaves; o++)
            {
                // Distinct integer seed per octave so octaves sample uncorrelated lattices
                // instead of aliasing against each other.
                int octaveSeed = unchecked(seed + o * 1013904223);
                sum += Sample(x * freq, y * freq, z * freq, octaveSeed) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            return maxAmplitude > 0.0 ? sum / maxAmplitude : 0.0;
        }

        /// <summary>Maps raw noise output (~[-1,1]) into the half-open interval [0, 1).</summary>
        public static double ToUnitInterval(double raw)
        {
            double v = raw * 0.5 + 0.5;
            if (v < 0.0) v = 0.0;
            if (v >= 1.0) v = Math.BitDecrement(1.0);
            return v;
        }
    }
}
