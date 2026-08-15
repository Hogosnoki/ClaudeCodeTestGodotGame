namespace ProcGen.Engine.Overrides
{
    /// <summary>One hand-painted (layer, position, tile) diff record.</summary>
    public readonly struct TileOverride
    {
        public string LayerId { get; }
        public int X { get; }
        public int Y { get; }
        public string TileId { get; }

        public TileOverride(string layerId, int x, int y, string tileId)
        {
            LayerId = layerId;
            X = x;
            Y = y;
            TileId = tileId;
        }
    }
}
