#nullable enable
using System.Collections.Generic;
using ProcGen.Engine.Overrides;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A named divergence from a map's base configuration -- e.g. a "winter" or "night" take on
    /// the same map, generated from the same layers/seeds by default but free to override any of
    /// them. A variation stores only what changes: per-layer seed/noise/tile-list overrides
    /// (<see cref="LayerVariation"/>, one entry per overridden layer -- a layer with no entry
    /// here generates exactly as the base map generates it) plus its own hand-painted tile diff
    /// (<see cref="Overrides"/>), layered on top of the base map's own overrides the same way the
    /// base map's overrides layer on top of procedural generation. Turning a variation into
    /// something <see cref="Generation.MapGenerator"/> can actually consume is
    /// <see cref="Generation.VariationResolution.Resolve"/>'s job -- nothing here talks to the
    /// generator directly, and the generator itself has no idea variations exist.
    /// </summary>
    public sealed class MapVariation
    {
        /// <summary>This variation's identifier within its map -- only needs to be unique among that map's own variations, the same scoping as an entrance/exit id.</summary>
        public string Id { get; set; } = "";

        public List<LayerVariation> LayerOverrides { get; set; } = new List<LayerVariation>();

        /// <summary>This variation's own hand-painted diff, layered on top of the base map's overrides. Same record shape as the base map's own diff -- see <see cref="OverrideStore"/>.</summary>
        public List<TileOverride> Overrides { get; set; } = new List<TileOverride>();
    }
}
