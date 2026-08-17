using System.Collections.Generic;
using ProcGen.Engine.Editing;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class VariationTests
    {
        [Fact]
        public void Resolve_UnknownOrEmptyVariationId_ReturnsBasePairByReference()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();

            var (defForEmpty, overridesForEmpty) = VariationResolution.Resolve(def, overrides, "");
            var (defForNull, overridesForNull) = VariationResolution.Resolve(def, overrides, null);
            var (defForUnknown, overridesForUnknown) = VariationResolution.Resolve(def, overrides, "nonexistent");

            Assert.Same(def, defForEmpty);
            Assert.Same(overrides, overridesForEmpty);
            Assert.Same(def, defForNull);
            Assert.Same(overrides, overridesForNull);
            Assert.Same(def, defForUnknown);
            Assert.Same(overrides, overridesForUnknown);
        }

        [Fact]
        public void Resolve_LayerWithNoVariationEntry_InheritsBaseSeedNoiseAndTiles()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            def.Variations.Add(new MapVariation { Id = "winter" }); // no LayerOverrides at all

            var (effective, _) = VariationResolution.Resolve(def, overrides, "winter");

            var baseGround = def.Layers[0];
            var effectiveGround = effective.Layers[0];
            Assert.Equal(baseGround.Seed.X, effectiveGround.Seed.X);
            Assert.Equal(baseGround.Noise.Frequency, effectiveGround.Noise.Frequency);
            Assert.Same(baseGround.Tiles, effectiveGround.Tiles);
        }

        [Fact]
        public void Resolve_OverriddenSeed_ReplacesOnlyThatLayersSeed()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            var newSeed = new SeedPosition(9999.0, 8888.0, 0.5);
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Seed = newSeed },
                },
            });

            var (effective, _) = VariationResolution.Resolve(def, overrides, "winter");

            Assert.Equal(newSeed.X, effective.Layers[0].Seed.X);
            Assert.Equal(newSeed.Y, effective.Layers[0].Seed.Y);
            // The other layer (ground_cover) is untouched.
            Assert.Equal(def.Layers[1].Seed.X, effective.Layers[1].Seed.X);
        }

        [Fact]
        public void Resolve_OverriddenNoise_ReplacesOnlyThatLayersNoise()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            var newNoise = new NoiseParams { Octaves = 5, Frequency = 0.2, Persistence = 0.7, Lacunarity = 3.0 };
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Noise = newNoise },
                },
            });

            var (effective, _) = VariationResolution.Resolve(def, overrides, "winter");

            Assert.Equal(5, effective.Layers[0].Noise.Octaves);
            Assert.Equal(def.Layers[0].Seed.X, effective.Layers[0].Seed.X); // seed still inherited
        }

        [Fact]
        public void Resolve_OverriddenTiles_ReplacesWholeTileList()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            var winterTiles = new List<TileDef> { new TileDef("ice", 1.0), new TileDef("snow", 1.0) };
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Tiles = winterTiles },
                },
            });

            var (effective, _) = VariationResolution.Resolve(def, overrides, "winter");

            Assert.Same(winterTiles, effective.Layers[0].Tiles);
            Assert.Equal(2, effective.Layers[0].Tiles.Count);
        }

        [Fact]
        public void Resolve_VariationOverrides_LayerOnTopOfBaseOverrides()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var baseOverrides = new OverrideStore();
            baseOverrides.Set("ground", 1, 1, "sand");
            baseOverrides.Set("ground", 2, 2, "land");
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                Overrides = new List<TileOverride> { new TileOverride("ground", 2, 2, "deep_water") },
            });

            var (_, effectiveOverrides) = VariationResolution.Resolve(def, baseOverrides, "winter");

            Assert.True(effectiveOverrides.TryGet("ground", 1, 1, out var untouched));
            Assert.Equal("sand", untouched); // base override not repainted by the variation still applies
            Assert.True(effectiveOverrides.TryGet("ground", 2, 2, out var overridden));
            Assert.Equal("deep_water", overridden); // variation's own diff wins where it repaints
        }

        [Fact]
        public void Resolve_DoesNotMutateBaseDefinitionOrBaseOverrides()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var baseOverrides = new OverrideStore();
            baseOverrides.Set("ground", 1, 1, "sand");
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation> { new LayerVariation { LayerId = "ground", Seed = new SeedPosition(1, 2, 3) } },
                Overrides = new List<TileOverride> { new TileOverride("ground", 5, 5, "deep_water") },
            });

            VariationResolution.Resolve(def, baseOverrides, "winter");

            Assert.NotEqual(1.0, def.Layers[0].Seed.X); // base layer's own seed untouched
            Assert.False(baseOverrides.TryGet("ground", 5, 5, out _)); // base overrides untouched
        }

        [Fact]
        public void GenerateRegion_AcceptsResolvedVariationDefinitionAndOverrides_WithoutErrors()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var baseOverrides = new OverrideStore();
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Seed = new SeedPosition(42.0, 7.0, 0.0) },
                },
            });
            var (effectiveDef, effectiveOverrides) = VariationResolution.Resolve(def, baseOverrides, "winter");

            var region = new RegionSpec(0, 0, 8, 8, 0.0);
            var result = MapGenerator.GenerateRegion(effectiveDef, region, effectiveOverrides);

            Assert.NotNull(result.GetFinalTile(0, 0));
        }

        [Fact]
        public void RenameLayer_CascadesIntoVariationLayerIdAndOverrides()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation> { new LayerVariation { LayerId = "ground", Seed = new SeedPosition(1, 2, 3) } },
                Overrides = new List<TileOverride> { new TileOverride("ground", 3, 4, "sand") },
            });

            RenameOperations.RenameLayer(def, overrides, "ground", "terrain");

            Assert.Equal("terrain", def.Variations[0].LayerOverrides[0].LayerId);
            Assert.Equal("terrain", def.Variations[0].Overrides[0].LayerId);
        }

        [Fact]
        public void RenameTile_CascadesIntoVariationTileListAndOverrides()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var overrides = new OverrideStore();
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Tiles = new List<TileDef> { new TileDef("land", 3.0) } },
                },
                Overrides = new List<TileOverride> { new TileOverride("ground", 3, 4, "land") },
            });

            RenameOperations.RenameTile(def, overrides, "ground", "land", "dry_land");

            Assert.Equal("dry_land", def.Variations[0].LayerOverrides[0].Tiles![0].Id);
            Assert.Equal(3.0, def.Variations[0].LayerOverrides[0].Tiles![0].Range); // range preserved
            Assert.Equal("dry_land", def.Variations[0].Overrides[0].TileId);
        }

        [Fact]
        public void RenameVariationTile_RenamesWithinVariationOwnedTileList_AndCascadesItsOwnOverrides()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var variation = new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Tiles = new List<TileDef> { new TileDef("ice", 1.0), new TileDef("snow", 1.0) } },
                },
                Overrides = new List<TileOverride> { new TileOverride("ground", 2, 2, "ice") },
            };
            def.Variations.Add(variation);

            bool ok = RenameOperations.RenameVariationTile(variation, "ground", "ice", "thin_ice");

            Assert.True(ok);
            Assert.Equal("thin_ice", variation.LayerOverrides[0].Tiles![0].Id);
            Assert.Equal("thin_ice", variation.Overrides[0].TileId);
            // The base map's own "land"/"deep_water"/etc tiles are completely untouched.
            Assert.DoesNotContain(def.Layers[0].Tiles, t => t.Id == "thin_ice");
        }

        [Fact]
        public void RenameVariationTile_RejectsDuplicateOrMissingLayerOverride()
        {
            var def = Milestone1Fixture.BuildDefinition();
            var variation = new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Tiles = new List<TileDef> { new TileDef("ice", 1.0), new TileDef("snow", 1.0) } },
                },
            };
            def.Variations.Add(variation);

            Assert.False(RenameOperations.RenameVariationTile(variation, "ground", "ice", "snow")); // duplicate within the list
            Assert.False(RenameOperations.RenameVariationTile(variation, "ground_cover", "dirt", "mud")); // no tile-list override for this layer
        }

        [Fact]
        public void ProjectFileSerializer_RoundTripsVariations()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "overworld";
            def.Variations.Add(new MapVariation
            {
                Id = "winter",
                LayerOverrides = new List<LayerVariation>
                {
                    new LayerVariation { LayerId = "ground", Seed = new SeedPosition(9, 8, 7), Noise = new NoiseParams { Octaves = 4 } },
                },
                Overrides = new List<TileOverride> { new TileOverride("ground", 1, 1, "deep_water") },
            });
            var overrides = new OverrideStore();

            string json = ProcGen.Engine.Serialization.ProjectFileSerializer.Serialize(
                new List<(MapDefinition, OverrideStore)> { (def, overrides) });
            var loaded = ProcGen.Engine.Serialization.ProjectFileSerializer.Deserialize(json);

            var loadedDef = loaded[0].Map;
            Assert.Single(loadedDef.Variations);
            Assert.Equal("winter", loadedDef.Variations[0].Id);
            Assert.Equal(9, loadedDef.Variations[0].LayerOverrides[0].Seed!.Value.X);
            Assert.Equal(4, loadedDef.Variations[0].LayerOverrides[0].Noise!.Octaves);
            Assert.Equal("deep_water", loadedDef.Variations[0].Overrides[0].TileId);
        }

        [Fact]
        public void ProjectFileSerializer_RejectsDuplicateVariationIdsOnSameMap()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "overworld";
            def.Variations.Add(new MapVariation { Id = "winter" });
            def.Variations.Add(new MapVariation { Id = "winter" });
            var overrides = new OverrideStore();
            string json = ProcGen.Engine.Serialization.ProjectFileSerializer.Serialize(
                new List<(MapDefinition, OverrideStore)> { (def, overrides) });

            Assert.Throws<System.FormatException>(() => ProcGen.Engine.Serialization.ProjectFileSerializer.Deserialize(json));
        }
    }
}
