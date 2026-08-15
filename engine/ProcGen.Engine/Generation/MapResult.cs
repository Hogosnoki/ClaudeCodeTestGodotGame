// File-scoped so this compiles cleanly even when copied into a host project that hasn't
// opted into <Nullable>enable</Nullable> project-wide.
#nullable enable
using System.Collections.Generic;

namespace ProcGen.Engine.Generation
{
    /// <summary>
    /// Resolved output of a generation pass over a <see cref="RegionSpec"/>: every layer's
    /// resolved tile per cell (null where that layer produced nothing at that cell), plus the
    /// final composited tile (topmost layer with a non-null resolved value).
    /// Indices are region-local: [0, Width) x [0, Height), offset from RegionSpec.OriginX/Y.
    /// </summary>
    public sealed class MapResult
    {
        public RegionSpec Region { get; }
        public IReadOnlyList<string> LayerIds { get; }

        private readonly Dictionary<string, string?[,]> _perLayer;
        private readonly string?[,] _final;
        private readonly string?[,] _finalLayerId;

        internal MapResult(RegionSpec region, IReadOnlyList<string> layerIds, Dictionary<string, string?[,]> perLayer, string?[,] final, string?[,] finalLayerId)
        {
            Region = region;
            LayerIds = layerIds;
            _perLayer = perLayer;
            _final = final;
            _finalLayerId = finalLayerId;
        }

        /// <summary>Resolved tile id for a specific layer at a region-local cell, or null if that layer produced nothing there.</summary>
        public string? GetLayerTile(string layerId, int localX, int localY) => _perLayer[layerId][localX, localY];

        /// <summary>Final composited tile id (topmost layer with output) at a region-local cell.</summary>
        public string? GetFinalTile(int localX, int localY) => _final[localX, localY];

        /// <summary>
        /// Id of the layer that produced the final composited tile at a region-local cell (the
        /// topmost layer with non-null output there). Useful for debug visualization -- e.g. to
        /// look up whether that specific layer has a manual override at this cell.
        /// </summary>
        public string? GetFinalLayerId(int localX, int localY) => _finalLayerId[localX, localY];
    }
}
