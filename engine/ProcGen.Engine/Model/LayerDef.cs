using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One generation layer. Layer count and tile count per layer are both unbounded by design --
    /// nothing in the engine assumes a fixed number of either.
    /// An empty <see cref="WritesOver"/> list means the layer is a base layer: it is eligible to
    /// generate everywhere (no filter to satisfy).
    /// </summary>
    public sealed class LayerDef
    {
        public string Id { get; }
        public List<TileDef> Tiles { get; }
        public List<WritesOverRule> WritesOver { get; }
        public SeedPosition Seed { get; set; }
        public NoiseParams Noise { get; set; }

        [JsonConstructor]
        public LayerDef(string id, List<TileDef> tiles, List<WritesOverRule> writesOver, SeedPosition seed, NoiseParams noise)
        {
            Id = id;
            Tiles = tiles;
            WritesOver = writesOver;
            Seed = seed;
            Noise = noise;
        }
    }
}
