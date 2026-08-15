namespace ProcGen.Engine.Selection
{
    /// <summary>
    /// Result of a weighted-range tile selection. Exposes the underlying continuous normalized
    /// value and the selected tile's bounds (not just the discrete tile id) so a renderer can
    /// compute proximity to the nearest range boundary and blend smoothly there, per the spec's
    /// "blend rather than hard-cut" requirement.
    /// </summary>
    public readonly struct TileSelectionResult
    {
        public int TileIndex { get; }
        public string TileId { get; }
        public double NormalizedValue { get; }
        public double LowerBound { get; }
        public double UpperBound { get; }
        public double RangeSum { get; }

        public TileSelectionResult(int tileIndex, string tileId, double normalizedValue, double lowerBound, double upperBound, double rangeSum)
        {
            TileIndex = tileIndex;
            TileId = tileId;
            NormalizedValue = normalizedValue;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            RangeSum = rangeSum;
        }

        /// <summary>
        /// Distance from the normalized value to the nearer of this tile's two range boundaries,
        /// in normalized selection-space units. Callers (e.g. a renderer) turn this into a blend
        /// weight against the neighboring tile as the value approaches 0.
        /// </summary>
        public double DistanceToNearestBoundary()
        {
            double toLower = NormalizedValue - LowerBound;
            double toUpper = UpperBound - NormalizedValue;
            return toLower < toUpper ? toLower : toUpper;
        }
    }
}
