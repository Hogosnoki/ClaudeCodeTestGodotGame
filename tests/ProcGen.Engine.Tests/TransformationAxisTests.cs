using ProcGen.Engine.Generation;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Validation;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class TransformationAxisTests
    {
        [Theory]
        [InlineData(0.0, 0.1, true)]
        [InlineData(0.1, 0.2, true)]
        [InlineData(0.0, 0.0, true)]
        [InlineData(0.0, -0.1, true)]
        [InlineData(0.0, 0.15, false)]
        [InlineData(1.0, 2.0, false)]
        public void IsValidStep_EnforcesMaxStepOfPointOne(double from, double to, bool expectedValid)
        {
            Assert.Equal(expectedValid, TransformationAxis.IsValidStep(from, to));
        }

        [Fact]
        public void ClampStep_NeverExceedsMaxStep_AndPreservesDirection()
        {
            Assert.Equal(0.1, TransformationAxis.ClampStep(0.0, 5.0), precision: 10);
            Assert.Equal(-0.1, TransformationAxis.ClampStep(0.0, -5.0), precision: 10);
            Assert.Equal(0.05, TransformationAxis.ClampStep(0.0, 0.05), precision: 10);
        }

        [Fact]
        public void GenerateRegion_AcrossSmallTransformationSteps_MorphsGraduallyNotAbruptly()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region0 = new RegionSpec(originX: 0, originY: 0, width: 50, height: 50, transformation: 0.0);
            var region1 = new RegionSpec(originX: 0, originY: 0, width: 50, height: 50, transformation: 0.1);
            var region2 = new RegionSpec(originX: 0, originY: 0, width: 50, height: 50, transformation: 0.2);
            var farRegion = new RegionSpec(originX: 0, originY: 0, width: 50, height: 50, transformation: 8.0);

            var overrides = new OverrideStore();
            var mapT0 = MapGenerator.GenerateRegion(def, region0, overrides);
            var mapT1 = MapGenerator.GenerateRegion(def, region1, overrides);
            var mapT2 = MapGenerator.GenerateRegion(def, region2, overrides);
            var mapFar = MapGenerator.GenerateRegion(def, farRegion, overrides);

            double agree01 = AgreementFraction(mapT0, mapT1, region0.Width, region0.Height);
            double agree12 = AgreementFraction(mapT1, mapT2, region0.Width, region0.Height);
            double agreeFar = AgreementFraction(mapT0, mapFar, region0.Width, region0.Height);

            // A single 0.1 step should keep the overwhelming majority of tiles the same ("same
            // style, different specific arrangement"), and should keep noticeably more agreement
            // than jumping to a distant, unrelated Transformation value.
            Assert.True(agree01 > 0.85, $"T=0.0->0.1 agreement too low: {agree01}");
            Assert.True(agree12 > 0.85, $"T=0.1->0.2 agreement too low: {agree12}");
            Assert.True(agree01 > agreeFar, $"Small step ({agree01}) should agree more than a distant jump ({agreeFar})");

            // And it must not be literally identical -- Transformation is supposed to change the
            // specific arrangement, just gradually.
            Assert.True(agree01 < 1.0, "T=0.0 and T=0.1 produced an identical map; Transformation axis had no effect");
        }

        private static double AgreementFraction(MapResult a, MapResult b, int width, int height)
        {
            int same = 0;
            int total = width * height;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (a.GetFinalTile(x, y) == b.GetFinalTile(x, y)) same++;
                }
            }
            return (double)same / total;
        }
    }
}
