using ProcGen.Engine.Editing;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class RenameOperationsTests
    {
        [Fact]
        public void RenameLayer_UpdatesId_AndCascadesWritesOverReferences()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameLayer(def, overrides, "ground", "terrain");

            Assert.True(ok);
            Assert.Equal("terrain", def.Layers[0].Id);
            Assert.Equal("terrain", def.Layers[1].WritesOver[0].LayerId);
            Assert.Equal("land", def.Layers[1].WritesOver[0].TileId);
        }

        [Fact]
        public void RenameLayer_CascadesOverrideStoreEntries()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            overrides.Set("ground", 3, 4, "sand");

            RenameOperations.RenameLayer(def, overrides, "ground", "terrain");

            Assert.False(overrides.TryGet("ground", 3, 4, out _));
            Assert.True(overrides.TryGet("terrain", 3, 4, out var tileId));
            Assert.Equal("sand", tileId);
        }

        [Fact]
        public void RenameLayer_RejectsDuplicateId()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameLayer(def, overrides, "ground", "ground_cover");

            Assert.False(ok);
            Assert.Equal("ground", def.Layers[0].Id);
        }

        [Fact]
        public void RenameLayer_RejectsEmptyId()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameLayer(def, overrides, "ground", "   ");

            Assert.False(ok);
            Assert.Equal("ground", def.Layers[0].Id);
        }

        [Fact]
        public void RenameLayer_UnknownOldId_IsNoOp()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameLayer(def, overrides, "nonexistent", "whatever");

            Assert.False(ok);
        }

        [Fact]
        public void RenameTile_UpdatesId_AndCascadesWritesOverRuleTileId()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameTile(def, overrides, "ground", "land", "dry_land");

            Assert.True(ok);
            Assert.Equal("dry_land", def.Layers[0].Tiles[3].Id);
            Assert.Equal("dry_land", def.Layers[1].WritesOver[0].TileId);
            Assert.Equal("ground", def.Layers[1].WritesOver[0].LayerId);
        }

        [Fact]
        public void RenameTile_PreservesRange()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            double originalRange = def.Layers[0].Tiles[3].Range;

            RenameOperations.RenameTile(def, overrides, "ground", "land", "dry_land");

            Assert.Equal(originalRange, def.Layers[0].Tiles[3].Range);
        }

        [Fact]
        public void RenameTile_CascadesBlockedTransitions()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            def.BlockedTransitions.Add(new TileTransitionRule("shallow_water", "land"));
            def.BlockedTransitions.Add(new TileTransitionRule("land", "deep_water"));

            RenameOperations.RenameTile(def, overrides, "ground", "land", "dry_land");

            Assert.Equal("dry_land", def.BlockedTransitions[0].ToTileId);
            Assert.Equal("dry_land", def.BlockedTransitions[1].FromTileId);
        }

        [Fact]
        public void RenameTile_CascadesOverrideStoreValue_OnlyForMatchingLayer()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            overrides.Set("ground", 3, 4, "land");
            overrides.Set("ground_cover", 5, 6, "grass"); // different layer, different tile -- must not be touched

            RenameOperations.RenameTile(def, overrides, "ground", "land", "dry_land");

            Assert.True(overrides.TryGet("ground", 3, 4, out var renamed));
            Assert.Equal("dry_land", renamed);
            Assert.True(overrides.TryGet("ground_cover", 5, 6, out var untouched));
            Assert.Equal("grass", untouched);
        }

        [Fact]
        public void RenameTile_RejectsDuplicateIdOnSameLayer()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameTile(def, overrides, "ground", "land", "sand");

            Assert.False(ok);
            Assert.Equal("land", def.Layers[0].Tiles[3].Id);
        }

        [Fact]
        public void RenameTile_RejectsEmptyId()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            bool ok = RenameOperations.RenameTile(def, overrides, "ground", "land", "");

            Assert.False(ok);
        }

        [Fact]
        public void RenameTile_UnknownLayerOrTile_IsNoOp()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            Assert.False(RenameOperations.RenameTile(def, overrides, "nonexistent_layer", "land", "x"));
            Assert.False(RenameOperations.RenameTile(def, overrides, "ground", "nonexistent_tile", "x"));
        }
    }
}
