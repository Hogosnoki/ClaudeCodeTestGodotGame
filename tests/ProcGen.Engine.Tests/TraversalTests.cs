using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Movement;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Serialization;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class TraversalTests
    {
        [Fact]
        public void CompiledTraversalRules_UnlistedPair_IsNotBlocked()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var compiled = CompiledTraversalRules.Compile(def);

            Assert.False(compiled.IsBlocked("land", "sand"));
        }

        [Fact]
        public void CompiledTraversalRules_ListedPair_IsBlockedOnlyInThatDirection()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.BlockedTransitions.Add(new TileTransitionRule("shallow_water", "grass"));
            var compiled = CompiledTraversalRules.Compile(def);

            Assert.True(compiled.IsBlocked("shallow_water", "grass"));
            Assert.False(compiled.IsBlocked("grass", "shallow_water"));
        }

        [Fact]
        public void MakeSolid_BlocksEveryOtherTileIntoTarget_ButNotOutOfItAndNotSelf()
        {
            var rules = new List<TileTransitionRule>();
            var allTiles = new[] { "deep_water", "shallow_water", "sand", "land" };

            TraversalEditing.MakeSolid(rules, "land", allTiles);

            Assert.Equal(3, rules.Count);
            Assert.Contains(rules, r => r.FromTileId == "deep_water" && r.ToTileId == "land");
            Assert.Contains(rules, r => r.FromTileId == "shallow_water" && r.ToTileId == "land");
            Assert.Contains(rules, r => r.FromTileId == "sand" && r.ToTileId == "land");
            Assert.DoesNotContain(rules, r => r.FromTileId == "land");

            var compiled = CompiledTraversalRules.Compile(new MapDefinition { BlockedTransitions = rules });
            Assert.True(compiled.IsBlocked("sand", "land"));
            Assert.False(compiled.IsBlocked("land", "sand"));
            Assert.False(compiled.IsBlocked("land", "land"));
        }

        [Fact]
        public void MakeSolid_CalledTwice_DoesNotDuplicateRules()
        {
            var rules = new List<TileTransitionRule>();
            var allTiles = new[] { "sand", "land" };

            TraversalEditing.MakeSolid(rules, "land", allTiles);
            TraversalEditing.MakeSolid(rules, "land", allTiles);

            Assert.Single(rules);
        }

        [Fact]
        public void MakeSolid_PreservesManuallyAddedRuleInTheOppositeDirection()
        {
            var rules = new List<TileTransitionRule> { new TileTransitionRule("land", "sand") };
            var allTiles = new[] { "sand", "land" };

            TraversalEditing.MakeSolid(rules, "land", allTiles);

            Assert.Equal(2, rules.Count);
            Assert.Contains(rules, r => r.FromTileId == "land" && r.ToTileId == "sand");
            Assert.Contains(rules, r => r.FromTileId == "sand" && r.ToTileId == "land");
        }

        [Fact]
        public void ClearBlocksInto_RemovesOnlyRulesTargetingGivenTile_LeavesOthersUntouched()
        {
            var rules = new List<TileTransitionRule>
            {
                new TileTransitionRule("sand", "land"),
                new TileTransitionRule("deep_water", "land"),
                new TileTransitionRule("shallow_water", "grass"),
            };

            TraversalEditing.ClearBlocksInto(rules, "land");

            Assert.Single(rules);
            Assert.Equal("shallow_water", rules[0].FromTileId);
            Assert.Equal("grass", rules[0].ToTileId);
        }

        [Fact]
        public void ExpandWritesOverFamily_IncludesTilesOfLayersThatWriteOverIt()
        {
            var def = Milestone1Fixture.BuildDefinition();

            var family = TraversalEditing.ExpandWritesOverFamily("land", def.Layers);

            Assert.Equal(new HashSet<string> { "land", "dirt", "grass", "tallgrass" }, family);
        }

        [Fact]
        public void ExpandWritesOverFamily_TileNothingWritesOver_IsJustItself()
        {
            var def = Milestone1Fixture.BuildDefinition();

            var family = TraversalEditing.ExpandWritesOverFamily("sand", def.Layers);

            Assert.Equal(new HashSet<string> { "sand" }, family);
        }

        [Fact]
        public void ExpandWritesOverFamily_IsTransitiveAcrossMultipleLayers()
        {
            var bottom = new LayerDef("bottom", new List<TileDef> { new TileDef("land", 1.0) },
                new List<WritesOverRule>(), new SeedPosition(0, 0, 0), new NoiseParams());
            var middle = new LayerDef("middle", new List<TileDef> { new TileDef("grass", 1.0) },
                new List<WritesOverRule> { new WritesOverRule("bottom", "land") }, new SeedPosition(1, 1, 0), new NoiseParams());
            var top = new LayerDef("top", new List<TileDef> { new TileDef("flower", 1.0) },
                new List<WritesOverRule> { new WritesOverRule("middle", "grass") }, new SeedPosition(2, 2, 0), new NoiseParams());
            var layers = new List<LayerDef> { bottom, middle, top };

            var family = TraversalEditing.ExpandWritesOverFamily("land", layers);

            Assert.Equal(new HashSet<string> { "land", "grass", "flower" }, family);
        }

        [Fact]
        public void MakeSolidFamily_BlocksOutsideTilesFromEveryFamilyMember_ButNotAmongFamilyMembers()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var allTiles = new[] { "deep_water", "shallow_water", "sand", "land", "dirt", "grass", "tallgrass" };
            var rules = new List<TileTransitionRule>();

            TraversalEditing.MakeSolidFamily(rules, "land", allTiles, def.Layers);

            Assert.Equal(12, rules.Count); // 3 outside tiles x 4 family members
            foreach (var outside in new[] { "deep_water", "shallow_water", "sand" })
            {
                foreach (var family in new[] { "land", "dirt", "grass", "tallgrass" })
                {
                    Assert.Contains(rules, r => r.FromTileId == outside && r.ToTileId == family);
                }
            }
            // No blocking among family members, and leaving is never blocked.
            Assert.DoesNotContain(rules, r => r.FromTileId == "land" || r.FromTileId == "dirt" || r.FromTileId == "grass" || r.FromTileId == "tallgrass");
        }

        [Fact]
        public void ClearBlocksIntoFamily_RemovesRulesTargetingAnyFamilyMember()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var rules = new List<TileTransitionRule>
            {
                new TileTransitionRule("sand", "land"),
                new TileTransitionRule("sand", "grass"),
                new TileTransitionRule("shallow_water", "sand"),
            };

            TraversalEditing.ClearBlocksIntoFamily(rules, "land", def.Layers);

            Assert.Single(rules);
            Assert.Equal("shallow_water", rules[0].FromTileId);
            Assert.Equal("sand", rules[0].ToTileId);
        }

        [Fact]
        public void RoundTrip_PreservesBlockedTransitions()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "m";
            def.BlockedTransitions.Add(new TileTransitionRule("shallow_water", "grass"));
            def.BlockedTransitions.Add(new TileTransitionRule("deep_water", "land"));

            string json = ProjectFileSerializer.Serialize(new[] { (def, new OverrideStore()) });
            var (loadedMap, _) = ProjectFileSerializer.Deserialize(json)[0];

            Assert.Equal(2, loadedMap.BlockedTransitions.Count);
            Assert.Equal("shallow_water", loadedMap.BlockedTransitions[0].FromTileId);
            Assert.Equal("grass", loadedMap.BlockedTransitions[0].ToTileId);
            Assert.Equal("deep_water", loadedMap.BlockedTransitions[1].FromTileId);
            Assert.Equal("land", loadedMap.BlockedTransitions[1].ToTileId);
        }
    }
}
