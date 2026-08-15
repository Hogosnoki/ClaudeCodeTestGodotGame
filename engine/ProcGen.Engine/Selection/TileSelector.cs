using System;

namespace ProcGen.Engine.Selection
{
    /// <summary>
    /// Weighted-range tile selection, per the spec: sum tile ranges, normalize the layer's noise
    /// value into [0, sum), and pick whichever tile's cumulative slot contains it. Implemented as
    /// a binary search over a precomputed cumulative-bounds table (see <see cref="CompiledLayer"/>)
    /// rather than the literal subtraction loop -- mathematically equivalent, O(log n) instead of
    /// O(n) per cell.
    /// </summary>
    public static class TileSelector
    {
        /// <param name="unitNoiseValue">Layer noise value normalized to [0, 1).</param>
        public static TileSelectionResult Select(CompiledLayer layer, double unitNoiseValue)
        {
            double normalized = unitNoiseValue * layer.RangeSum;
            if (normalized < 0.0) normalized = 0.0;
            if (normalized >= layer.RangeSum) normalized = Math.BitDecrement(layer.RangeSum);

            int index = Array.BinarySearch(layer.CumulativeUpper, normalized);
            if (index < 0)
            {
                // BinarySearch returns the bitwise complement of the first element greater than
                // the value when there's no exact match -- exactly the slot we want, since
                // CumulativeUpper[i] is an exclusive upper bound.
                index = ~index;
            }
            else
            {
                // Exact match against an upper bound belongs to the next slot up (bounds are
                // exclusive), except at the very last tile where it's clamped in.
                index += 1;
            }
            if (index >= layer.CumulativeUpper.Length) index = layer.CumulativeUpper.Length - 1;

            double lower = layer.LowerBoundOf(index);
            double upper = layer.CumulativeUpper[index];
            string tileId = layer.Def.Tiles[index].Id;

            return new TileSelectionResult(index, tileId, normalized, lower, upper, layer.RangeSum);
        }
    }
}
