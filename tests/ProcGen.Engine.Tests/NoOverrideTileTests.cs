using System.Collections.Generic;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class NoOverrideTileTests
    {
        private static LayerDef AlwaysBlankLayer(string id, List<WritesOverRule> writesOver, double seedOffset)
        {
            return new LayerDef(
                id,
                new List<TileDef> { new TileDef(TileDef.NoOverrideId, 1.0) },
                writesOver,
                new SeedPosition(seedOffset, seedOffset, 0.0),
                new NoiseParams { Octaves = 1, Frequency = 0.05 });
        }

        [Fact]
        public void ProceduralSelection_ResolvesToNull_LettingLowerLayerShowThrough()
        {
            var ground = Milestone1Fixture.BuildDefinition().Layers[0]; // deep_water/shallow_water/sand/land, no filter
            var alwaysBlankCover = AlwaysBlankLayer("cover", new List<WritesOverRule>(), 777.0);

            var def = new MapDefinition { WorldSeed = 12345 };
            def.Layers.Add(ground);
            def.Layers.Add(alwaysBlankCover);

            var region = new RegionSpec(originX: 0, originY: 0, width: 20, height: 20, transformation: 0.0);
            var result = MapGenerator.GenerateRegion(def, region, new OverrideStore());

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Assert.Null(result.GetLayerTile("cover", x, y));
                    Assert.Equal(result.GetLayerTile("ground", x, y), result.GetFinalTile(x, y));
                    Assert.Equal("ground", result.GetFinalLayerId(x, y));
                }
            }
        }

        [Fact]
        public void ManualOverride_OfNoOverrideSentinel_AlsoResolvesToNull()
        {
            var ground = Milestone1Fixture.BuildDefinition().Layers[0];
            // This cover layer would normally always paint "grass" -- the point of this test is
            // that a manual override of the sentinel id still forces it blank regardless.
            var cover = new LayerDef("cover", new List<TileDef> { new TileDef("grass", 1.0) },
                new List<WritesOverRule>(), new SeedPosition(1.0, 1.0, 0.0), new NoiseParams());

            var def = new MapDefinition { WorldSeed = 1 };
            def.Layers.Add(ground);
            def.Layers.Add(cover);

            var region = new RegionSpec(originX: 0, originY: 0, width: 5, height: 5, transformation: 0.0);
            var overrides = new OverrideStore();
            overrides.Set("cover", 2, 2, TileDef.NoOverrideId);

            var result = MapGenerator.GenerateRegion(def, region, overrides);

            Assert.Null(result.GetLayerTile("cover", 2, 2));
            Assert.Equal(result.GetLayerTile("ground", 2, 2), result.GetFinalTile(2, 2));
            // Elsewhere, uninfluenced by the override, cover still procedurally produces "grass".
            Assert.Equal("grass", result.GetLayerTile("cover", 0, 0));
        }

        [Fact]
        public void OnAStandaloneLayerWithNothingBeneath_ResultsInNullFinalTile()
        {
            // The engine itself doesn't forbid a base layer from using the sentinel -- that
            // restriction (only offer it for non-bottom layers) lives in the editor tool, not
            // here. A base layer that's always blank simply produces a mapful of "no tile".
            var def = new MapDefinition { WorldSeed = 1 };
            def.Layers.Add(AlwaysBlankLayer("only_layer", new List<WritesOverRule>(), 0.0));

            var region = new RegionSpec(originX: 0, originY: 0, width: 5, height: 5, transformation: 0.0);
            var result = MapGenerator.GenerateRegion(def, region, new OverrideStore());

            Assert.Null(result.GetFinalTile(2, 2));
            Assert.Null(result.GetFinalLayerId(2, 2));
        }

        [Fact]
        public void HigherLayer_WritesOverRule_TreatsBlankResultAsIneligible()
        {
            // base (always "land") -> middle (always blank, writes_over base.land) -> top
            // (writes_over middle.grass). Middle never actually produces "grass" (it's always
            // blank), so top must never become eligible.
            var baseLayer = new LayerDef("base", new List<TileDef> { new TileDef("land", 1.0) },
                new List<WritesOverRule>(), new SeedPosition(0, 0, 0), new NoiseParams());
            var middle = AlwaysBlankLayer("middle", new List<WritesOverRule> { new WritesOverRule("base", "land") }, 500.0);
            var top = new LayerDef("top", new List<TileDef> { new TileDef("flower", 1.0) },
                new List<WritesOverRule> { new WritesOverRule("middle", "grass") }, new SeedPosition(900, 900, 0), new NoiseParams());

            var def = new MapDefinition { WorldSeed = 1 };
            def.Layers.Add(baseLayer);
            def.Layers.Add(middle);
            def.Layers.Add(top);

            var region = new RegionSpec(originX: 0, originY: 0, width: 10, height: 10, transformation: 0.0);
            var result = MapGenerator.GenerateRegion(def, region, new OverrideStore());

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Assert.Null(result.GetLayerTile("middle", x, y));
                    Assert.Null(result.GetLayerTile("top", x, y));
                    Assert.Equal("land", result.GetFinalTile(x, y));
                }
            }
        }

        [Fact]
        public void MixedWithRealTiles_SomeCellsBlank_SomeCellsShowTheRealTile()
        {
            var ground = Milestone1Fixture.BuildDefinition().Layers[0];
            var cover = new LayerDef("cover",
                new List<TileDef> { new TileDef("grass", 1.0), new TileDef(TileDef.NoOverrideId, 1.0) },
                new List<WritesOverRule>(), new SeedPosition(42.0, 84.0, 0.0),
                new NoiseParams { Octaves = 2, Frequency = 0.08 });

            var def = new MapDefinition { WorldSeed = 999 };
            def.Layers.Add(ground);
            def.Layers.Add(cover);

            var region = new RegionSpec(originX: 0, originY: 0, width: 40, height: 40, transformation: 0.0);
            var result = MapGenerator.GenerateRegion(def, region, new OverrideStore());

            bool sawBlank = false, sawGrass = false;
            for (int x = 0; x < region.Width && !(sawBlank && sawGrass); x++)
            {
                for (int y = 0; y < region.Height && !(sawBlank && sawGrass); y++)
                {
                    string? coverTile = result.GetLayerTile("cover", x, y);
                    if (coverTile == null) sawBlank = true;
                    else if (coverTile == "grass") sawGrass = true;
                }
            }

            Assert.True(sawBlank, "Expected at least one cell where the no_override slot was selected.");
            Assert.True(sawGrass, "Expected at least one cell where grass was selected.");
        }
    }
}
