// File-scoped so this compiles cleanly even when copied into a host project that hasn't
// opted into <Nullable>enable</Nullable> project-wide.
#nullable enable
using System;
using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Noise;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Selection;

namespace ProcGen.Engine.Generation
{
    /// <summary>
    /// The layer-resolution pass: for every cell in a region, evaluates layers bottom-to-top,
    /// each one checking its `writes_over` filter against the already-resolved output of any
    /// lower layer it references (not just its immediate predecessor), applying manual overrides
    /// as the final per-layer step. This is the single place both the tool and the game must call
    /// through -- it is the only source of truth for what a map looks like.
    /// </summary>
    public static class MapGenerator
    {
        public static MapResult GenerateRegion(MapDefinition definition, RegionSpec region, OverrideStore overrides)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Layers.Count == 0) throw new ArgumentException("MapDefinition has no layers.", nameof(definition));
            if (region.Width <= 0 || region.Height <= 0) throw new ArgumentException("Region must have positive width/height.", nameof(region));
            overrides ??= new OverrideStore();

            var layers = definition.Layers;
            var compiled = new CompiledLayer[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                compiled[i] = new CompiledLayer(layers[i]);
            }

            var layerIds = new List<string>(layers.Count);
            var perLayerGrids = new Dictionary<string, string?[,]>(layers.Count);
            foreach (var layer in layers)
            {
                layerIds.Add(layer.Id);
                perLayerGrids[layer.Id] = new string?[region.Width, region.Height];
            }
            var finalGrid = new string?[region.Width, region.Height];
            var finalLayerGrid = new string?[region.Width, region.Height];

            // Reused across cells to avoid an allocation per cell; cleared each iteration.
            var resolvedThisCell = new Dictionary<string, string?>(layers.Count);

            for (int lx = 0; lx < region.Width; lx++)
            {
                int worldX = region.OriginX + lx;
                for (int ly = 0; ly < region.Height; ly++)
                {
                    int worldY = region.OriginY + ly;
                    resolvedThisCell.Clear();

                    for (int i = 0; i < layers.Count; i++)
                    {
                        var layer = layers[i];
                        bool eligible = IsEligible(layer, resolvedThisCell);

                        string? tileId = null;
                        if (eligible)
                        {
                            // layer.Seed is a fixed lattice-space translation (decorrelates layers
                            // from each other) -- passed to SampleFbm separately from the sampled
                            // position so it's applied AFTER frequency scaling, not before. See
                            // SampleFbm's doc comment for why that ordering matters.
                            var noise = layer.Noise;
                            double raw = LatticeNoise3D.SampleFbm(
                                worldX, worldY, region.Transformation,
                                layer.Seed.X, layer.Seed.Y, layer.Seed.T,
                                definition.WorldSeed, noise.Octaves, noise.Frequency, noise.Persistence, noise.Lacunarity);
                            double unit = LatticeNoise3D.ToUnitInterval(raw);
                            var selection = TileSelector.Select(compiled[i], unit);
                            tileId = selection.TileId;
                        }

                        // Manual overrides are the final step and apply regardless of whether the
                        // procedural filter matched -- a hand-painted tile is authoritative.
                        if (overrides.TryGet(layer.Id, worldX, worldY, out var overrideTileId))
                        {
                            tileId = overrideTileId;
                        }

                        // The no_override sentinel means "this layer produces nothing here" --
                        // resolve it to null (same as an ineligible/filtered-out layer) so lower
                        // layers show through, whether it came from the procedural roll or a
                        // manual override painting it directly.
                        if (tileId == TileDef.NoOverrideId)
                        {
                            tileId = null;
                        }

                        resolvedThisCell[layer.Id] = tileId;
                        perLayerGrids[layer.Id][lx, ly] = tileId;
                    }

                    var (finalTile, finalLayer) = ComputeFinalTile(layers, resolvedThisCell);
                    finalGrid[lx, ly] = finalTile;
                    finalLayerGrid[lx, ly] = finalLayer;
                }
            }

            return new MapResult(region, layerIds, perLayerGrids, finalGrid, finalLayerGrid);
        }

        /// <summary>
        /// A layer is eligible to generate at this cell if it has no filter (base layer), or if
        /// ANY of its writes_over rules matches the referenced lower layer's already-resolved
        /// (post-override) tile at this cell.
        /// </summary>
        private static bool IsEligible(LayerDef layer, Dictionary<string, string?> resolvedThisCell)
        {
            if (layer.WritesOver.Count == 0) return true;

            foreach (var rule in layer.WritesOver)
            {
                if (resolvedThisCell.TryGetValue(rule.LayerId, out var resolvedTile) &&
                    resolvedTile == rule.TileId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns the topmost layer's tile and that layer's id, for whichever layer actually produced output at this cell.</summary>
        private static (string? TileId, string? LayerId) ComputeFinalTile(List<LayerDef> layers, Dictionary<string, string?> resolvedThisCell)
        {
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var tile = resolvedThisCell[layers[i].Id];
                if (tile != null) return (tile, layers[i].Id);
            }
            return (null, null);
        }
    }
}
