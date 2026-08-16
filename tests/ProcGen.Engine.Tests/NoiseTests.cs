using ProcGen.Engine.Noise;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class NoiseTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(5, -3, 2)]
        [InlineData(-7, 11, -4)]
        public void Sample_AtExactLatticePoint_IsExactlyZero(int x, int y, int z)
        {
            // At an exact integer lattice coordinate, the offset vector to that corner is (0,0,0),
            // so the gradient dot product -- and therefore the interpolated result -- must be
            // exactly zero. This is a direct structural check on the gradient-noise construction.
            double v = LatticeNoise3D.Sample(x, y, z, seed: 42);
            Assert.Equal(0.0, v);
        }

        [Fact]
        public void Sample_IsDeterministic_ForSameInputs()
        {
            double a = LatticeNoise3D.Sample(3.25, -1.75, 0.6, seed: 7);
            double b = LatticeNoise3D.Sample(3.25, -1.75, 0.6, seed: 7);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Sample_DiffersAcrossFractionalPositions()
        {
            // Fine positional control: two nearby fractional X coordinates must not collapse to
            // the same value between the same pair of lattice points.
            // y/z are given small fractional offsets (rather than landing exactly on integer
            // lattice points) to avoid the well-known axis-aligned-line degeneracy of gradient
            // noise, where a line through two lattice points can evaluate to zero along its whole
            // length if both endpoints' selected gradients happen to have no component along that
            // axis -- a property of the construction itself, not a defect (see the exact-lattice
            // test above, which pins down that same zero-at-lattice-points behavior deliberately).
            double a = LatticeNoise3D.Sample(3.1, 5.37, 0.41, seed: 1);
            double b = LatticeNoise3D.Sample(3.9, 5.37, 0.41, seed: 1);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Sample_IsContinuous_AcrossFractionalXY()
        {
            // Small steps in a fractional coordinate should produce small changes in output --
            // verifies interpolation is actually blending between lattice values, not jumping.
            double prev = LatticeNoise3D.Sample(2.0, 2.37, 0.41, seed: 99);
            for (int i = 1; i <= 20; i++)
            {
                double x = 2.0 + i * 0.05;
                double cur = LatticeNoise3D.Sample(x, 2.37, 0.41, seed: 99);
                Assert.True(System.Math.Abs(cur - prev) < 0.5, $"Discontinuous jump at x={x}: {prev} -> {cur}");
                prev = cur;
            }
        }

        [Fact]
        public void Sample_IsContinuous_AlongTransformationAxis()
        {
            // The Transformation axis interpolates exactly like X/Y: small steps in T should
            // produce small, gradual changes rather than jumps.
            double prev = LatticeNoise3D.Sample(10.5, -3.25, 0.0, seed: 5);
            for (int i = 1; i <= 20; i++)
            {
                double t = i * 0.1;
                double cur = LatticeNoise3D.Sample(10.5, -3.25, t, seed: 5);
                Assert.True(System.Math.Abs(cur - prev) < 0.5, $"Discontinuous jump at t={t}: {prev} -> {cur}");
                prev = cur;
            }
        }

        [Fact]
        public void ToUnitInterval_StaysWithinHalfOpenRange()
        {
            for (double raw = -1.5; raw <= 1.5; raw += 0.1)
            {
                double v = LatticeNoise3D.ToUnitInterval(raw);
                Assert.True(v >= 0.0 && v < 1.0, $"raw={raw} -> {v} out of [0,1)");
            }
        }

        [Fact]
        public void SampleFbm_IsDeterministic()
        {
            double a = LatticeNoise3D.SampleFbm(12.3, -4.5, 0.2, 1000.0, 2000.0, 0.0, seed: 3, octaves: 4, frequency: 0.1, persistence: 0.5, lacunarity: 2.0);
            double b = LatticeNoise3D.SampleFbm(12.3, -4.5, 0.2, 1000.0, 2000.0, 0.0, seed: 3, octaves: 4, frequency: 0.1, persistence: 0.5, lacunarity: 2.0);
            Assert.Equal(a, b);
        }

        [Fact]
        public void SampleFbm_OriginAtZeroCoordinate_IsInvariantToFrequency()
        {
            // At the sampled coordinate (0,0,0), x*freq/y*freq/z*freq is always exactly 0
            // regardless of frequency, so the resulting lattice position is exactly
            // (originX, originY, originZ) -- origin must not itself be scaled by frequency, so
            // varying frequency here must not change the output at all. This is the direct fix
            // for the hypersensitivity bug: a real SeedPosition's origin is meant to be a fixed
            // decorrelation offset, not something that gets re-multiplied by every frequency
            // (or, via lacunarity, per-octave frequency) tweak.
            double originX = 1000.37, originY = 2000.81, originZ = 0.0;
            double first = LatticeNoise3D.SampleFbm(0.0, 0.0, 0.0, originX, originY, originZ, seed: 9, octaves: 1, frequency: 0.01, persistence: 0.5, lacunarity: 2.0);
            foreach (double frequency in new[] { 0.05, 0.1, 0.5, 2.0 })
            {
                double v = LatticeNoise3D.SampleFbm(0.0, 0.0, 0.0, originX, originY, originZ, seed: 9, octaves: 1, frequency: frequency, persistence: 0.5, lacunarity: 2.0);
                Assert.Equal(first, v);
            }
        }

        [Fact]
        public void SampleFbm_SmallFrequencyNudge_WithLargeOrigin_ChangesOutputOnlyGradually()
        {
            // Regression test for the hypersensitivity bug: with a large, realistic SeedPosition-
            // style origin, a small relative nudge to frequency (or lacunarity, which multiplies
            // frequency per octave) must not blow the sampled point past a neighboring lattice
            // cell -- that would show up as a large, discontinuous-looking output change instead
            // of a gradual one.
            double origin = 1000.37;
            double baseFrequency = 0.05;
            double a = LatticeNoise3D.SampleFbm(10.0, 4.0, 0.0, origin, 2000.81, 0.0, seed: 11, octaves: 3, frequency: baseFrequency, persistence: 0.5, lacunarity: 2.0);
            double b = LatticeNoise3D.SampleFbm(10.0, 4.0, 0.0, origin, 2000.81, 0.0, seed: 11, octaves: 3, frequency: baseFrequency * 1.01, persistence: 0.5, lacunarity: 2.0);
            Assert.True(System.Math.Abs(a - b) < 0.2, $"A 1% frequency nudge changed raw output by {System.Math.Abs(a - b)}, expected a small change.");
        }
    }
}
