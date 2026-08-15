using System.Collections.Generic;
using ProcGen.Engine.Model;

namespace ProcGen.Engine.Tests
{
    /// <summary>
    /// Shared 2-layer setup matching the milestone-1 scenario from the spec:
    /// ground (deep_water/shallow_water/sand/land) + ground_cover (dirt/grass/tallgrass,
    /// writes_over ground.land).
    /// </summary>
    internal static class Milestone1Fixture
    {
        public static MapDefinition BuildDefinition()
        {
            var ground = new LayerDef(
                "ground",
                new List<TileDef>
                {
                    new TileDef("deep_water", 0.7),
                    new TileDef("shallow_water", 0.3),
                    new TileDef("sand", 0.2),
                    new TileDef("land", 2.0),
                },
                new List<WritesOverRule>(),
                new SeedPosition(1000.37, 2000.81, 0.0),
                new NoiseParams { Octaves = 3, Frequency = 0.05, Persistence = 0.5, Lacunarity = 2.0 });

            var groundCover = new LayerDef(
                "ground_cover",
                new List<TileDef>
                {
                    new TileDef("dirt", 0.4),
                    new TileDef("grass", 0.8),
                    new TileDef("tallgrass", 0.6),
                },
                new List<WritesOverRule> { new WritesOverRule("ground", "land") },
                new SeedPosition(-500.62, 7500.19, 0.0),
                new NoiseParams { Octaves = 2, Frequency = 0.08, Persistence = 0.5, Lacunarity = 2.0 });

            var def = new MapDefinition { WorldSeed = 12345 };
            def.Layers.Add(ground);
            def.Layers.Add(groundCover);
            return def;
        }
    }
}
