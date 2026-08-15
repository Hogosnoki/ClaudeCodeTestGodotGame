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

        internal MapResult(RegionSpec region, IReadOnlyList<string> layerIds, Dictionary<string, string?[,]> perLayer, string?[,] final)
        {
            Region = region;
            LayerIds = layerIds;
            _perLayer = perLayer;
            _final = final;
        }

        /// <summary>Resolved tile id for a specific layer at a region-local cell, or null if that layer produced nothing there.</summary>
        public string? GetLayerTile(string layerId, int localX, int localY) => _perLayer[layerId][localX, localY];

        /// <summary>Final composited tile id (topmost layer with output) at a region-local cell.</summary>
        public string? GetFinalTile(int localX, int localY) => _final[localX, localY];
    }
}
