using System.Collections.Generic;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// The full procedural definition of a map: an ordered list of layers (index = priority,
    /// higher index = higher priority, per the spec's "painter's algorithm gated by writes_over"
    /// rule) plus a world seed shared by all layers' hashing.
    /// Layer identity/decorrelation comes from each layer's own <see cref="SeedPosition"/>, not
    /// from a separate per-layer integer -- see SeedPosition's doc comment.
    /// </summary>
    public sealed class MapDefinition
    {
        public int WorldSeed { get; set; }
        public List<LayerDef> Layers { get; } = new List<LayerDef>();
    }
}
