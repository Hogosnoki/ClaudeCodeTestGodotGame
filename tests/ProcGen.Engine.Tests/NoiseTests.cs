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
            double a = LatticeNoise3D.SampleFbm(12.3, -4.5, 0.2, seed: 3, octaves: 4, frequency: 0.1, persistence: 0.5, lacunarity: 2.0);
            double b = LatticeNoise3D.SampleFbm(12.3, -4.5, 0.2, seed: 3, octaves: 4, frequency: 0.1, persistence: 0.5, lacunarity: 2.0);
            Assert.Equal(a, b);
        }
    }
}
