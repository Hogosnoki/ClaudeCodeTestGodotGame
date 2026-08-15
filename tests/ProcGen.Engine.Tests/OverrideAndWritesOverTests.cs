using ProcGen.Engine.Generation;
using ProcGen.Engine.Overrides;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class OverrideAndWritesOverTests
    {
        [Fact]
        public void ManualOverride_OnLowerLayer_MakesHigherLayerEligible_WhereItWasNotBefore()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 60, height: 60, transformation: 0.0);
            var overrides = new OverrideStore();

            var before = MapGenerator.GenerateRegion(def, region, overrides);

            // Find a cell where "ground" did NOT resolve to "land" procedurally, so
            // "ground_cover" was ineligible there (writes_over: [ground.land]).
            int fx = -1, fy = -1;
            for (int x = 0; x < region.Width && fx < 0; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    if (before.GetLayerTile("ground", x, y) != "land")
                    {
                        fx = x; fy = y; break;
                    }
                }
            }
            Assert.True(fx >= 0, "Test fixture didn't produce any non-land ground cell to test against.");
            Assert.Null(before.GetLayerTile("ground_cover", fx, fy));

            // Hand-paint that cell's "ground" layer to "land".
            overrides.Set("ground", region.OriginX + fx, region.OriginY + fy, "land");
            var after = MapGenerator.GenerateRegion(def, region, overrides);

            Assert.Equal("land", after.GetLayerTile("ground", fx, fy));
            // ground_cover must re-evaluate its writes_over filter against the *overridden*
            // ground result and become eligible -- i.e. it now produces some tile (possibly one
            // rolled by its own noise), rather than staying null.
            Assert.NotNull(after.GetLayerTile("ground_cover", fx, fy));
        }

        [Fact]
        public void ManualOverride_ThatRemovesLand_MakesHigherLayerIneligible()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 60, height: 60, transformation: 0.0);
            var overrides = new OverrideStore();
            var before = MapGenerator.GenerateRegion(def, region, overrides);

            int fx = -1, fy = -1;
            for (int x = 0; x < region.Width && fx < 0; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    if (before.GetLayerTile("ground", x, y) == "land" && before.GetLayerTile("ground_cover", x, y) != null)
                    {
                        fx = x; fy = y; break;
                    }
                }
            }
            Assert.True(fx >= 0, "Test fixture didn't produce a land cell with ground_cover output to test against.");

            overrides.Set("ground", region.OriginX + fx, region.OriginY + fy, "deep_water");
            var after = MapGenerator.GenerateRegion(def, region, overrides);

            Assert.Equal("deep_water", after.GetLayerTile("ground", fx, fy));
            Assert.Null(after.GetLayerTile("ground_cover", fx, fy));
            Assert.Equal("deep_water", after.GetFinalTile(fx, fy));
        }

        [Fact]
        public void ManualOverride_OnTopLayer_IsFinalStepAndWinsRegardlessOfFilter()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 30, height: 30, transformation: 0.0);
            var overrides = new OverrideStore();
            var before = MapGenerator.GenerateRegion(def, region, overrides);

            // Pick a cell where ground_cover is procedurally ineligible (ground != land).
            int fx = -1, fy = -1;
            for (int x = 0; x < region.Width && fx < 0; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    if (before.GetLayerTile("ground", x, y) != "land")
                    {
                        fx = x; fy = y; break;
                    }
                }
            }
            Assert.True(fx >= 0);

            // Hand-paint ground_cover directly, without touching ground at all.
            overrides.Set("ground_cover", region.OriginX + fx, region.OriginY + fy, "grass");
            var after = MapGenerator.GenerateRegion(def, region, overrides);

            Assert.Equal("grass", after.GetLayerTile("ground_cover", fx, fy));
            Assert.Equal("grass", after.GetFinalTile(fx, fy));
        }

        [Fact]
        public void GetFinalLayerId_IdentifiesWhichLayerProducedTheFinalTile()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 40, height: 40, transformation: 0.0);
            var overrides = new OverrideStore();
            var map = MapGenerator.GenerateRegion(def, region, overrides);

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    string? finalLayer = map.GetFinalLayerId(x, y);
                    Assert.NotNull(finalLayer);
                    Assert.Equal(map.GetLayerTile(finalLayer!, x, y), map.GetFinalTile(x, y));

                    bool groundCoverProduced = map.GetLayerTile("ground_cover", x, y) != null;
                    Assert.Equal(groundCoverProduced ? "ground_cover" : "ground", finalLayer);
                }
            }
        }

        [Fact]
        public void OverridePersists_AcrossRepeatedGeneration()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 20, height: 20, transformation: 0.0);
            var overrides = new OverrideStore();
            overrides.Set("ground", 3, 4, "sand");

            var a = MapGenerator.GenerateRegion(def, region, overrides);
            var b = MapGenerator.GenerateRegion(def, region, overrides);

            Assert.Equal("sand", a.GetLayerTile("ground", 3, 4));
            Assert.Equal("sand", b.GetLayerTile("ground", 3, 4));
        }
    }
}
