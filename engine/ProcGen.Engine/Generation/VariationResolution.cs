#nullable enable
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;

namespace ProcGen.Engine.Generation
{
    /// <summary>
    /// Turns a base <see cref="MapDefinition"/>/<see cref="OverrideStore"/> plus a chosen
    /// variation id into the effective (definition, overrides) pair
    /// <see cref="MapGenerator.GenerateRegion"/> actually consumes -- the generator itself knows
    /// nothing about variations; it only ever sees an ordinary-looking, already-resolved
    /// MapDefinition. Layer identity/order/writes_over and the map's entrances/exits/
    /// BlockedTransitions always come from the base map unchanged; only what a
    /// <see cref="LayerVariation"/> actually sets (seed, noise, or the tile list) diverges, and
    /// only for the layer it names -- "loading a variation begins at the lowest level" (the base
    /// map), with only the named divergences applied on top.
    /// </summary>
    public static class VariationResolution
    {
        /// <summary>
        /// Resolves variationId against baseDefinition.Variations. A null/empty id, or one that
        /// doesn't match any variation on this map, returns the base pair unchanged (by
        /// reference -- no copy made when there's nothing to vary).
        /// </summary>
        public static (MapDefinition Definition, OverrideStore Overrides) Resolve(
            MapDefinition baseDefinition, OverrideStore baseOverrides, string? variationId)
        {
            if (string.IsNullOrEmpty(variationId)) return (baseDefinition, baseOverrides);

            var variation = baseDefinition.Variations.Find(v => v.Id == variationId);
            if (variation == null) return (baseDefinition, baseOverrides);

            var effective = new MapDefinition
            {
                MapId = baseDefinition.MapId,
                WorldSeed = baseDefinition.WorldSeed,
                Entrances = baseDefinition.Entrances,
                Exits = baseDefinition.Exits,
                BlockedTransitions = baseDefinition.BlockedTransitions,
                Variations = baseDefinition.Variations,
            };
            foreach (var layer in baseDefinition.Layers)
            {
                var layerVariation = variation.LayerOverrides.Find(lv => lv.LayerId == layer.Id);
                effective.Layers.Add(layerVariation == null
                    ? layer
                    : new LayerDef(
                        layer.Id,
                        layerVariation.Tiles ?? layer.Tiles,
                        layer.WritesOver,
                        layerVariation.Seed ?? layer.Seed,
                        layerVariation.Noise ?? layer.Noise));
            }

            // Base overrides first, then this variation's own diff on top -- a variation can
            // repaint a cell the base map already hand-painted, but doesn't have to repeat
            // painted cells it agrees with.
            var effectiveOverrides = new OverrideStore();
            foreach (var record in baseOverrides.Enumerate())
            {
                effectiveOverrides.Set(record.LayerId, record.X, record.Y, record.TileId);
            }
            foreach (var record in variation.Overrides)
            {
                effectiveOverrides.Set(record.LayerId, record.X, record.Y, record.TileId);
            }

            return (effective, effectiveOverrides);
        }
    }
}
