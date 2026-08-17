using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;

namespace ProcGen.Engine.Editing
{
    /// <summary>
    /// Renaming a layer or tile id can't just mutate the id in place -- <see cref="LayerDef.Id"/>
    /// and <see cref="TileDef.Id"/> are deliberately immutable (see TileDef's doc comment) because
    /// writes_over rules, transition rules, and override records all reference layers/tiles by id
    /// string, and an in-place mutation would silently break every one of those references. These
    /// helpers instead replace the renamed LayerDef/TileDef with a new instance carrying the new id
    /// (same position in its list, same other data) and cascade the rename through every place that
    /// id string is referenced within the same map, so nothing is left dangling.
    /// </summary>
    public static class RenameOperations
    {
        /// <summary>Renames a layer id, cascading through every other layer's WritesOverRule.LayerId and every override record on that layer. Returns false (no-op) if oldLayerId doesn't exist or newLayerId is empty/already taken.</summary>
        public static bool RenameLayer(MapDefinition def, OverrideStore overrides, string oldLayerId, string newLayerId)
        {
            newLayerId = newLayerId?.Trim() ?? "";
            if (string.IsNullOrEmpty(newLayerId) || oldLayerId == newLayerId) return false;
            if (def.Layers.Exists(l => l.Id == newLayerId)) return false;
            int index = def.Layers.FindIndex(l => l.Id == oldLayerId);
            if (index < 0) return false;

            var old = def.Layers[index];
            def.Layers[index] = new LayerDef(newLayerId, old.Tiles, old.WritesOver, old.Seed, old.Noise);

            foreach (var layer in def.Layers)
            {
                for (int i = 0; i < layer.WritesOver.Count; i++)
                {
                    if (layer.WritesOver[i].LayerId == oldLayerId)
                        layer.WritesOver[i] = new WritesOverRule(newLayerId, layer.WritesOver[i].TileId);
                }
            }

            var renamed = new List<TileOverride>();
            foreach (var record in overrides.Enumerate())
            {
                renamed.Add(record.LayerId == oldLayerId
                    ? new TileOverride(newLayerId, record.X, record.Y, record.TileId)
                    : record);
            }
            overrides.ReplaceAll(renamed);

            // Variations reference layers by the same id string (LayerVariation.LayerId, and
            // each variation's own override records) -- cascade there too, or a rename would
            // silently orphan every variation's overrides for this layer.
            foreach (var variation in def.Variations)
            {
                foreach (var layerVariation in variation.LayerOverrides)
                {
                    if (layerVariation.LayerId == oldLayerId) layerVariation.LayerId = newLayerId;
                }
                for (int i = 0; i < variation.Overrides.Count; i++)
                {
                    var record = variation.Overrides[i];
                    if (record.LayerId == oldLayerId)
                        variation.Overrides[i] = new TileOverride(newLayerId, record.X, record.Y, record.TileId);
                }
            }
            return true;
        }

        /// <summary>Renames a tile id within one layer, cascading through every layer's WritesOverRule, every TileTransitionRule, and every override record that named it. Returns false (no-op) if layerId/oldTileId don't exist or newTileId is empty/already taken on that layer.</summary>
        public static bool RenameTile(MapDefinition def, OverrideStore overrides, string layerId, string oldTileId, string newTileId)
        {
            newTileId = newTileId?.Trim() ?? "";
            if (string.IsNullOrEmpty(newTileId) || oldTileId == newTileId) return false;
            var layer = def.Layers.Find(l => l.Id == layerId);
            if (layer == null) return false;
            if (layer.Tiles.Exists(t => t.Id == newTileId)) return false;
            int tileIndex = layer.Tiles.FindIndex(t => t.Id == oldTileId);
            if (tileIndex < 0) return false;

            layer.Tiles[tileIndex] = new TileDef(newTileId, layer.Tiles[tileIndex].Range);

            foreach (var l in def.Layers)
            {
                for (int i = 0; i < l.WritesOver.Count; i++)
                {
                    var rule = l.WritesOver[i];
                    if (rule.LayerId == layerId && rule.TileId == oldTileId)
                        l.WritesOver[i] = new WritesOverRule(layerId, newTileId);
                }
            }

            for (int i = 0; i < def.BlockedTransitions.Count; i++)
            {
                var rule = def.BlockedTransitions[i];
                string from = rule.FromTileId == oldTileId ? newTileId : rule.FromTileId;
                string to = rule.ToTileId == oldTileId ? newTileId : rule.ToTileId;
                if (from != rule.FromTileId || to != rule.ToTileId)
                    def.BlockedTransitions[i] = new TileTransitionRule(from, to);
            }

            var renamed = new List<TileOverride>();
            foreach (var record in overrides.Enumerate())
            {
                renamed.Add(record.LayerId == layerId && record.TileId == oldTileId
                    ? new TileOverride(record.LayerId, record.X, record.Y, newTileId)
                    : record);
            }
            overrides.ReplaceAll(renamed);

            // Cascade into every variation's own tile-list replacement (if it has one for this
            // layer) and its own override records, the same way as the base map above.
            foreach (var variation in def.Variations)
            {
                foreach (var layerVariation in variation.LayerOverrides)
                {
                    if (layerVariation.LayerId != layerId || layerVariation.Tiles == null) continue;
                    int variationTileIndex = layerVariation.Tiles.FindIndex(t => t.Id == oldTileId);
                    if (variationTileIndex >= 0)
                        layerVariation.Tiles[variationTileIndex] = new TileDef(newTileId, layerVariation.Tiles[variationTileIndex].Range);
                }
                for (int i = 0; i < variation.Overrides.Count; i++)
                {
                    var record = variation.Overrides[i];
                    if (record.LayerId == layerId && record.TileId == oldTileId)
                        variation.Overrides[i] = new TileOverride(record.LayerId, record.X, record.Y, newTileId);
                }
            }
            return true;
        }

        /// <summary>
        /// Renames a tile id within one variation's own tile-list replacement for a layer (see
        /// <see cref="LayerVariation.Tiles"/>) -- for a tile id a variation introduced itself
        /// (e.g. "ice" in a winter palette swap) rather than one shared with the base map, so
        /// there is no base-layer WritesOverRule/BlockedTransitions/base-override cascade to
        /// worry about, only this variation's own override records. Returns false (no-op) if the
        /// variation has no tile-list override for layerId, oldTileId isn't in it, or newTileId
        /// is empty/already taken within that same list.
        /// </summary>
        public static bool RenameVariationTile(MapVariation variation, string layerId, string oldTileId, string newTileId)
        {
            newTileId = newTileId?.Trim() ?? "";
            if (string.IsNullOrEmpty(newTileId) || oldTileId == newTileId) return false;
            var layerVariation = variation.LayerOverrides.Find(lv => lv.LayerId == layerId);
            if (layerVariation?.Tiles == null) return false;
            if (layerVariation.Tiles.Exists(t => t.Id == newTileId)) return false;
            int tileIndex = layerVariation.Tiles.FindIndex(t => t.Id == oldTileId);
            if (tileIndex < 0) return false;

            layerVariation.Tiles[tileIndex] = new TileDef(newTileId, layerVariation.Tiles[tileIndex].Range);

            for (int i = 0; i < variation.Overrides.Count; i++)
            {
                var record = variation.Overrides[i];
                if (record.LayerId == layerId && record.TileId == oldTileId)
                    variation.Overrides[i] = new TileOverride(record.LayerId, record.X, record.Y, newTileId);
            }
            return true;
        }
    }
}
