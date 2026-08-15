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
    /// <see cref="ProcGen.Engine.Serialization.ProjectFileSerializer"/> for the save/load format.
    /// One <c>MapDefinition</c> is one map among possibly many in a single project file, which is
    /// itself one whole game -- see <see cref="ProcGen.Engine.Serialization.ProjectFile"/>.
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>
        /// This map's identifier within its project -- how other maps' <see cref="ExitPoint"/>s
        /// refer to it, and how the game looks it up once the project is loaded into memory. Only
        /// needs to be unique within the project file it lives in (enforced on load by
        /// <see cref="ProcGen.Engine.Serialization.ProjectFileSerializer"/>): a project *is* a
        /// game, there is no way for an exit to reference a map in a different project file, so
        /// there is never a reason for two maps in the same game to share an id.
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
        /// any other in the project) whose DestinationEntranceId names one of these. An id only
        /// needs to be unique within this map -- a reference to one is always the pair (this map's
        /// MapId, entrance id), and the MapId half of that pair is what's unique project-wide.
        /// </summary>
        public List<EntrancePoint> Entrances { get; set; } = new List<EntrancePoint>();

        /// <summary>A point the player leaves through, naming a destination map id and one of that destination's entrance ids.</summary>
        public List<ExitPoint> Exits { get; set; } = new List<ExitPoint>();

        /// <summary>
        /// Directional movement-blocking rules between adjacent cells, keyed by final tile id --
        /// evaluated by <see cref="ProcGen.Engine.Movement.CompiledTraversalRules"/>, never
        /// consulted by generation itself. This is the only movement-blocking mechanism; there is
        /// no separate per-tile "walkable" flag. See <see cref="TileTransitionRule"/>.
        /// </summary>
        public List<TileTransitionRule> BlockedTransitions { get; set; } = new List<TileTransitionRule>();
    }
}
