using System;
using System.Collections.Generic;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Serialization;
using ProcGen.Engine.Validation;
using Xunit;

namespace ProcGen.Engine.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void RoundTrip_PreservesEveryFieldOfASingleMapProject()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "starter_island";
            def.Entrances.Add(new EntrancePoint("north_gate", 10, -4));
            def.Exits.Add(new ExitPoint("south_path", 12, 30, "forest", "west_gate"));

            var overrides = new OverrideStore();
            overrides.Set("ground", 3, 4, "sand");
            overrides.Set("ground_cover", -2, 7, "dirt");

            string json = ProjectFileSerializer.Serialize(new[] { (def, overrides) });
            var loaded = ProjectFileSerializer.Deserialize(json);

            Assert.Single(loaded);
            var (loadedMap, loadedOverrides) = loaded[0];

            Assert.Equal(def.MapId, loadedMap.MapId);
            Assert.Equal(def.WorldSeed, loadedMap.WorldSeed);

            Assert.Equal(def.Layers.Count, loadedMap.Layers.Count);
            for (int i = 0; i < def.Layers.Count; i++)
            {
                var expected = def.Layers[i];
                var actual = loadedMap.Layers[i];
                Assert.Equal(expected.Id, actual.Id);
                Assert.Equal(expected.Seed.X, actual.Seed.X);
                Assert.Equal(expected.Seed.Y, actual.Seed.Y);
                Assert.Equal(expected.Seed.T, actual.Seed.T);
                Assert.Equal(expected.Noise.Octaves, actual.Noise.Octaves);
                Assert.Equal(expected.Noise.Frequency, actual.Noise.Frequency);
                Assert.Equal(expected.Noise.Persistence, actual.Noise.Persistence);
                Assert.Equal(expected.Noise.Lacunarity, actual.Noise.Lacunarity);

                Assert.Equal(expected.Tiles.Count, actual.Tiles.Count);
                for (int t = 0; t < expected.Tiles.Count; t++)
                {
                    Assert.Equal(expected.Tiles[t].Id, actual.Tiles[t].Id);
                    Assert.Equal(expected.Tiles[t].Range, actual.Tiles[t].Range);
                }

                Assert.Equal(expected.WritesOver.Count, actual.WritesOver.Count);
                for (int w = 0; w < expected.WritesOver.Count; w++)
                {
                    Assert.Equal(expected.WritesOver[w].LayerId, actual.WritesOver[w].LayerId);
                    Assert.Equal(expected.WritesOver[w].TileId, actual.WritesOver[w].TileId);
                }
            }

            Assert.Single(loadedMap.Entrances);
            Assert.Equal("north_gate", loadedMap.Entrances[0].Id);
            Assert.Equal(10, loadedMap.Entrances[0].X);
            Assert.Equal(-4, loadedMap.Entrances[0].Y);

            Assert.Single(loadedMap.Exits);
            Assert.Equal("south_path", loadedMap.Exits[0].Id);
            Assert.Equal(12, loadedMap.Exits[0].X);
            Assert.Equal(30, loadedMap.Exits[0].Y);
            Assert.Equal("forest", loadedMap.Exits[0].DestinationMapId);
            Assert.Equal("west_gate", loadedMap.Exits[0].DestinationEntranceId);

            Assert.Equal(overrides.Count, loadedOverrides.Count);
            Assert.True(loadedOverrides.TryGet("ground", 3, 4, out var t1));
            Assert.Equal("sand", t1);
            Assert.True(loadedOverrides.TryGet("ground_cover", -2, 7, out var t2));
            Assert.Equal("dirt", t2);
        }

        [Fact]
        public void RoundTrip_PreservesMultipleMapsInOneProject_InOrder()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "overworld";
            mapA.Exits.Add(new ExitPoint("to_forest", 5, 5, "forest", "entry"));

            var mapB = Milestone1Fixture.BuildDefinition();
            mapB.MapId = "forest";
            mapB.Entrances.Add(new EntrancePoint("entry", 0, 0));

            var loaded = ProjectFileSerializer.Deserialize(
                ProjectFileSerializer.Serialize(new[]
                {
                    (mapA, new OverrideStore()),
                    (mapB, new OverrideStore()),
                }));

            Assert.Equal(2, loaded.Count);
            Assert.Equal("overworld", loaded[0].Map.MapId);
            Assert.Equal("forest", loaded[1].Map.MapId);
            Assert.Equal("to_forest", loaded[0].Map.Exits[0].Id);
            Assert.Equal("entry", loaded[1].Map.Entrances[0].Id);
        }

        [Fact]
        public void RoundTrip_ProducesByteIdenticalGenerationOutput()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "determinism_check";
            var overrides = new OverrideStore();
            overrides.Set("ground", 5, 5, "land");

            var region = new RegionSpec(originX: -10, originY: -10, width: 60, height: 60, transformation: 0.0);
            var before = MapGenerator.GenerateRegion(def, region, overrides);

            string json = ProjectFileSerializer.Serialize(new[] { (def, overrides) });
            var (loadedMap, loadedOverrides) = ProjectFileSerializer.Deserialize(json)[0];
            var after = MapGenerator.GenerateRegion(loadedMap, region, loadedOverrides);

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Assert.Equal(before.GetFinalTile(x, y), after.GetFinalTile(x, y));
                }
            }
        }

        [Fact]
        public void Deserialize_DuplicateMapIds_Throws()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "same_id";
            var mapB = Milestone1Fixture.BuildDefinition();
            mapB.MapId = "same_id";

            string json = ProjectFileSerializer.Serialize(new[]
            {
                (mapA, new OverrideStore()),
                (mapB, new OverrideStore()),
            });

            var ex = Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize(json));
            Assert.Contains("same_id", ex.Message);
        }

        [Fact]
        public void Deserialize_MissingMapId_Throws()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "";
            string json = ProjectFileSerializer.Serialize(new[] { (def, new OverrideStore()) });

            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize(json));
        }

        [Fact]
        public void Deserialize_DuplicateEntranceIds_Throws()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "m";
            def.Entrances.Add(new EntrancePoint("gate", 0, 0));
            def.Entrances.Add(new EntrancePoint("gate", 5, 5));

            string json = ProjectFileSerializer.Serialize(new[] { (def, new OverrideStore()) });

            var ex = Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize(json));
            Assert.Contains("gate", ex.Message);
        }

        [Fact]
        public void Deserialize_DuplicateExitIds_Throws()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "m";
            def.Exits.Add(new ExitPoint("out", 0, 0, "other", "in"));
            def.Exits.Add(new ExitPoint("out", 5, 5, "other2", "in2"));

            string json = ProjectFileSerializer.Serialize(new[] { (def, new OverrideStore()) });

            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize(json));
        }

        [Fact]
        public void Deserialize_EmptyOrMalformedJson_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize(""));
            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize("not json"));
            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize("{}"));
            Assert.Throws<FormatException>(() => ProjectFileSerializer.Deserialize("{\"maps\": []}"));
        }

        [Fact]
        public void Serialize_OmitsNothingEditorOnly_JsonHasNoColorFields()
        {
            var def = Milestone1Fixture.BuildDefinition();
            def.MapId = "m";
            string json = ProjectFileSerializer.Serialize(new[] { (def, new OverrideStore()) });

            Assert.DoesNotContain("color", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FindDanglingExits_ResolvedExit_ReportsNothing()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "a";
            mapA.Exits.Add(new ExitPoint("out", 0, 0, "b", "in"));
            var mapB = Milestone1Fixture.BuildDefinition();
            mapB.MapId = "b";
            mapB.Entrances.Add(new EntrancePoint("in", 0, 0));

            var problems = ProjectValidation.FindDanglingExits(new List<MapDefinition> { mapA, mapB });

            Assert.Empty(problems);
        }

        [Fact]
        public void FindDanglingExits_UnknownDestinationMap_IsReported()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "a";
            mapA.Exits.Add(new ExitPoint("out", 0, 0, "nonexistent", "in"));

            var problems = ProjectValidation.FindDanglingExits(new List<MapDefinition> { mapA });

            var problem = Assert.Single(problems);
            Assert.Equal("a", problem.SourceMapId);
            Assert.Equal("out", problem.ExitId);
            Assert.Equal(DanglingExitReason.MapNotFound, problem.Reason);
        }

        [Fact]
        public void FindDanglingExits_UnknownDestinationEntrance_IsReported()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "a";
            mapA.Exits.Add(new ExitPoint("out", 0, 0, "b", "nonexistent"));
            var mapB = Milestone1Fixture.BuildDefinition();
            mapB.MapId = "b";
            mapB.Entrances.Add(new EntrancePoint("in", 0, 0));

            var problems = ProjectValidation.FindDanglingExits(new List<MapDefinition> { mapA, mapB });

            var problem = Assert.Single(problems);
            Assert.Equal(DanglingExitReason.EntranceNotFound, problem.Reason);
        }

        [Fact]
        public void FindDanglingExits_SelfReferencingSameMap_IsAllowed()
        {
            var mapA = Milestone1Fixture.BuildDefinition();
            mapA.MapId = "a";
            mapA.Entrances.Add(new EntrancePoint("loop_in", 1, 1));
            mapA.Exits.Add(new ExitPoint("loop_out", 0, 0, "a", "loop_in"));

            var problems = ProjectValidation.FindDanglingExits(new List<MapDefinition> { mapA });

            Assert.Empty(problems);
        }
    }
}
