using System;
using ProcGen.Engine.Model;

namespace ProcGen.Engine.Selection
{
    /// <summary>
    /// Precomputed cumulative-weight table for one layer's tile list, built once per generation
    /// call (not once per cell) so selection is a binary search over a small sorted array rather
    /// than re-summing ranges at every position -- an efficient equivalent of the spec's
    /// "sum ranges, walk the list subtracting" description.
    /// </summary>
    public sealed class CompiledLayer
    {
        public LayerDef Def { get; }
        public double RangeSum { get; }

        // CumulativeUpper[i] = sum of tiles[0..i].Range -- the upper (exclusive) bound of tile i's
        // slot in normalized [0, RangeSum) selection space. Tile i's slot is
        // [CumulativeUpper[i-1], CumulativeUpper[i]) (with CumulativeUpper[-1] == 0).
        public double[] CumulativeUpper { get; }

        public CompiledLayer(LayerDef def)
        {
            if (def.Tiles == null || def.Tiles.Count == 0)
                throw new ArgumentException($"Layer '{def.Id}' has no tiles.", nameof(def));

            Def = def;
            CumulativeUpper = new double[def.Tiles.Count];
            double running = 0.0;
            for (int i = 0; i < def.Tiles.Count; i++)
            {
                double range = def.Tiles[i].Range;
                if (range < 0.0)
                    throw new ArgumentException($"Layer '{def.Id}' tile '{def.Tiles[i].Id}' has a negative range.", nameof(def));
                running += range;
                CumulativeUpper[i] = running;
            }
            RangeSum = running;

            if (RangeSum <= 0.0)
                throw new ArgumentException($"Layer '{def.Id}' has a zero total tile range.", nameof(def));
        }

        public double LowerBoundOf(int tileIndex) => tileIndex == 0 ? 0.0 : CumulativeUpper[tileIndex - 1];
    }
}
