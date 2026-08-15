using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Validation;

namespace ProcGenGame
{
    /// <summary>
    /// Minimal C# test scene for milestone 1. Calls straight into the engine module -- no editor
    /// UI, no game-specific glue -- and prints the same acceptance checks that the automated test
    /// suite (tests/ProcGen.Engine.Tests) verifies, so the milestone can also be observed running
    /// live inside the real Godot runtime.
    /// </summary>
    public partial class Milestone1TestScene : Node
    {
        public override void _Ready()
        {
            GD.Print("=== ProcGen Engine — Milestone 1 ===");

            var def = BuildDefinition();
            var region = new RegionSpec(originX: 0, originY: 0, width: 24, height: 16, transformation: 0.0);
            var overrides = new OverrideStore();

            GD.Print("\n-- Region at Transformation = 0.0 --");
            var mapT0 = MapGenerator.GenerateRegion(def, region, overrides);
            PrintGrid(mapT0, region);

            GD.Print("\n-- Transformation axis: 0.0 -> 0.1 -> 0.2 (gradual morph check) --");
            var regionT1 = new RegionSpec(region.OriginX, region.OriginY, region.Width, region.Height, 0.1);
            var regionT2 = new RegionSpec(region.OriginX, region.OriginY, region.Width, region.Height, 0.2);
            GD.Print($"Step 0.0 -> 0.1 valid under <=0.1 rule: {TransformationAxis.IsValidStep(0.0, 0.1)}");
            GD.Print($"Step 0.0 -> 0.5 valid under <=0.1 rule: {TransformationAxis.IsValidStep(0.0, 0.5)} (clamped to {TransformationAxis.ClampStep(0.0, 0.5)})");
            var mapT1 = MapGenerator.GenerateRegion(def, regionT1, overrides);
            var mapT2 = MapGenerator.GenerateRegion(def, regionT2, overrides);
            double agree01 = AgreementFraction(mapT0, mapT1, region);
            double agree12 = AgreementFraction(mapT1, mapT2, region);
            GD.Print($"Agreement T0.0 vs T0.1: {agree01:P1} (same style, gradually different arrangement)");
            GD.Print($"Agreement T0.1 vs T0.2: {agree12:P1}");

            GD.Print("\n-- Manual override + writes_over re-evaluation --");
            var (ox, oy) = FindNonLandCell(mapT0, region);
            GD.Print($"Cell ({ox},{oy}) procedurally: ground={mapT0.GetLayerTile("ground", ox, oy)}, ground_cover={mapT0.GetLayerTile("ground_cover", ox, oy) ?? "(none)"}");
            overrides.Set("ground", region.OriginX + ox, region.OriginY + oy, "land");
            var mapOverridden = MapGenerator.GenerateRegion(def, region, overrides);
            GD.Print($"After hand-painting ground=({ox},{oy}) -> land: ground_cover={mapOverridden.GetLayerTile("ground_cover", ox, oy) ?? "(none)"} (re-evaluated writes_over against the override)");

            GD.Print("\n-- Determinism: regenerate same region + seeds + overrides twice --");
            var repeatA = MapGenerator.GenerateRegion(def, region, overrides);
            var repeatB = MapGenerator.GenerateRegion(def, region, overrides);
            bool identical = GridsAreIdentical(repeatA, repeatB, region);
            GD.Print(identical ? "PASS: regeneration is bit-identical." : "FAIL: regeneration differs between runs.");

            GD.Print("\n=== Milestone 1 checks complete ===");
            GetTree().Quit(identical ? 0 : 1);
        }

        private static MapDefinition BuildDefinition()
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

        private static readonly Dictionary<string, string> TileGlyphs = new Dictionary<string, string>
        {
            ["deep_water"] = "~", ["shallow_water"] = "-", ["sand"] = ".", ["land"] = ",",
            ["dirt"] = "d", ["grass"] = "g", ["tallgrass"] = "T",
        };

        private static void PrintGrid(MapResult map, RegionSpec region)
        {
            for (int y = 0; y < region.Height; y++)
            {
                var row = new System.Text.StringBuilder();
                for (int x = 0; x < region.Width; x++)
                {
                    string tile = map.GetFinalTile(x, y) ?? "?";
                    row.Append(TileGlyphs.TryGetValue(tile, out var glyph) ? glyph : "?");
                }
                GD.Print(row.ToString());
            }
        }

        private static double AgreementFraction(MapResult a, MapResult b, RegionSpec region)
        {
            int same = 0, total = region.Width * region.Height;
            for (int x = 0; x < region.Width; x++)
                for (int y = 0; y < region.Height; y++)
                    if (a.GetFinalTile(x, y) == b.GetFinalTile(x, y)) same++;
            return (double)same / total;
        }

        private static bool GridsAreIdentical(MapResult a, MapResult b, RegionSpec region)
        {
            foreach (var layerId in a.LayerIds)
            {
                for (int x = 0; x < region.Width; x++)
                    for (int y = 0; y < region.Height; y++)
                        if (a.GetLayerTile(layerId, x, y) != b.GetLayerTile(layerId, x, y))
                            return false;
            }
            return true;
        }

        private static (int X, int Y) FindNonLandCell(MapResult map, RegionSpec region)
        {
            for (int x = 0; x < region.Width; x++)
                for (int y = 0; y < region.Height; y++)
                    if (map.GetLayerTile("ground", x, y) != "land")
                        return (x, y);
            return (0, 0);
        }
    }
}
