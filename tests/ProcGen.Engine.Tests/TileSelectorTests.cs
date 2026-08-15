using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Selection;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class TileSelectorTests
    {
        private static LayerDef WaterAndLandLayer() => new LayerDef(
            "water_and_land",
            new List<TileDef>
            {
                new TileDef("deep_water", 0.7),
                new TileDef("shallow_water", 0.3),
                new TileDef("sand", 0.2),
                new TileDef("land", 2.0),
            },
            new List<WritesOverRule>(),
            new SeedPosition(0, 0, 0),
            new NoiseParams());

        // Note: the spec's own worked example states "0.7 + 0.3 + 0.2 + 2.0 = 2.2", which is an
        // arithmetic slip in the prose -- the actual sum is 3.2. The selection *mechanism* (sum
        // ranges, normalize into [0, sum), walk cumulative bounds) is what's implemented; these
        // tests use the correct sum, 3.2, and derive boundary positions from it.
        [Theory]
        [InlineData(0.0, "deep_water")]
        [InlineData(0.7 / 3.2 - 0.001, "deep_water")]
        [InlineData(0.7 / 3.2 + 0.001, "shallow_water")]
        [InlineData(1.0 / 3.2 + 0.001, "sand")]
        [InlineData(1.2 / 3.2 + 0.001, "land")]
        [InlineData(0.999999, "land")]
        public void Select_PicksTileByCumulativeWeightedRange(double unitValue, string expectedTileId)
        {
            var compiled = new CompiledLayer(WaterAndLandLayer());
            var result = TileSelector.Select(compiled, unitValue);
            Assert.Equal(expectedTileId, result.TileId);
        }

        [Fact]
        public void Select_RangeSumIsCorrectSumOfTileRanges()
        {
            var compiled = new CompiledLayer(WaterAndLandLayer());
            Assert.Equal(3.2, compiled.RangeSum, precision: 10);
        }

        [Fact]
        public void Select_ExposesContinuousValueAndBounds_ForBoundaryBlending()
        {
            var compiled = new CompiledLayer(WaterAndLandLayer());
            // Deep in the "land" slot (which spans unit range [0.375, 1.0)): far from either boundary.
            var deep = TileSelector.Select(compiled, 0.6);
            Assert.Equal("land", deep.TileId);
            Assert.True(deep.DistanceToNearestBoundary() > 0.1);

            // Just past the sand/land boundary: very close to it.
            var nearBoundary = TileSelector.Select(compiled, 1.2 / 3.2 + 0.0001);
            Assert.Equal("land", nearBoundary.TileId);
            Assert.True(nearBoundary.DistanceToNearestBoundary() < 0.001);
        }

        [Fact]
        public void Select_ProportionsAcrossManySamples_MatchWeights()
        {
            var compiled = new CompiledLayer(WaterAndLandLayer());
            var counts = new Dictionary<string, int>();
            const int samples = 100_000;
            for (int i = 0; i < samples; i++)
            {
                double u = (i + 0.5) / samples; // uniform sweep across [0,1)
                var r = TileSelector.Select(compiled, u);
                counts.TryGetValue(r.TileId, out var c);
                counts[r.TileId] = c + 1;
            }

            double sum = compiled.RangeSum;
            AssertProportion(counts, "deep_water", 0.7 / sum, samples);
            AssertProportion(counts, "shallow_water", 0.3 / sum, samples);
            AssertProportion(counts, "sand", 0.2 / sum, samples);
            AssertProportion(counts, "land", 2.0 / sum, samples);
        }

        private static void AssertProportion(Dictionary<string, int> counts, string tileId, double expectedFraction, int samples)
        {
            double actual = counts.TryGetValue(tileId, out var c) ? (double)c / samples : 0.0;
            Assert.True(System.Math.Abs(actual - expectedFraction) < 0.001,
                $"{tileId}: expected fraction {expectedFraction}, got {actual}");
        }
    }
}
