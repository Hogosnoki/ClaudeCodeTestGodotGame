using ProcGen.Engine.Generation;
using ProcGen.Engine.Overrides;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class DeterminismTests
    {
        [Fact]
        public void GenerateRegion_TwiceWithSameInputs_IsBitIdentical()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 100, originY: -50, width: 40, height: 40, transformation: 0.0);

            var overrides = new OverrideStore();
            overrides.Set("ground", 110, -45, "land");
            overrides.Set("ground_cover", 110, -45, "tallgrass");

            var first = MapGenerator.GenerateRegion(def, region, overrides);
            var second = MapGenerator.GenerateRegion(def, region, overrides);

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Assert.Equal(first.GetLayerTile("ground", x, y), second.GetLayerTile("ground", x, y));
                    Assert.Equal(first.GetLayerTile("ground_cover", x, y), second.GetLayerTile("ground_cover", x, y));
                    Assert.Equal(first.GetFinalTile(x, y), second.GetFinalTile(x, y));
                }
            }
        }

        [Fact]
        public void GenerateRegion_SameInputsFromFreshOverrideStore_IsBitIdentical()
        {
            // Rebuilds the override store from its serialized-record form, the way a save/load
            // round-trip would, and confirms that produces the same result as the live store --
            // this is the actual "reload from disk" path the runtime engine takes.
            var def = Milestone1Fixture.BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 30, height: 30, transformation: 0.1);

            var overrides = new OverrideStore();
            overrides.Set("ground", 5, 5, "land");

            var reloaded = OverrideStore.FromRecords(overrides.Enumerate());

            var a = MapGenerator.GenerateRegion(def, region, overrides);
            var b = MapGenerator.GenerateRegion(def, region, reloaded);

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Assert.Equal(a.GetFinalTile(x, y), b.GetFinalTile(x, y));
                }
            }
        }
    }
}
