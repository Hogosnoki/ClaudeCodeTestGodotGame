using System.Collections.Generic;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// The full procedural definition of a map: an ordered list of layers (index = priority,
    /// higher index = higher priority, per the spec's "painter's algorithm gated by writes_over"
    /// rule) plus a world seed shared by all layers' hashing.
    /// Layer identity/decorrelation comes from each layer's own <see cref="SeedPosition"/>, not
    /// from a separate per-layer integer -- see SeedPosition's doc comment.
    /// This whole type, plus the override diff painted on top of it, is the entire "configuration"
    /// half of "configuration + algorithm reproduces the map exactly" -- see
    /// <see cref="ProcGen.Engine.Serialization.MapFileSerializer"/> for the save/load format.
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>
        /// This map's identifier -- how other maps' <see cref="ExitPoint"/>s refer to it, and how
        /// the game looks it up once loaded into memory. Deliberately separate from the file name
        /// a map is saved under: the file name is only how the game engine locates the file on
        /// disk, while the map id is how the game refers to it afterward, so the two are free to
        /// differ (e.g. renaming a file doesn't break every exit that points at the map inside it).
        /// </summary>
        public string MapId { get; set; } = "";

        public int WorldSeed { get; set; }

        // These three carry a setter (not just a getter) purely so System.Text.Json can assign a
        // freshly-deserialized list straight in -- it does not populate an existing read-only
        // collection property the way constructor parameters do. Nothing needs to ever reassign
        // them wholesale; the normal way to build one up is still `def.Layers.Add(...)`.
        public List<LayerDef> Layers { get; set; } = new List<LayerDef>();

        /// <summary>
        /// Where a player arrives when entering this map through some other exit (on this map or
        /// any other) whose DestinationEntranceId names one of these. An id only needs to be
        /// unique within this map -- a reference to one is always the pair (this map's MapId,
        /// entrance id), and the MapId half of that pair is what's globally unique.
        /// </summary>
        public List<EntrancePoint> Entrances { get; set; } = new List<EntrancePoint>();

        /// <summary>A point the player leaves through, naming a destination map id and one of that destination's entrance ids.</summary>
        public List<ExitPoint> Exits { get; set; } = new List<ExitPoint>();
    }
}
