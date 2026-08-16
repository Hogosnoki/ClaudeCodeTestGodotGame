using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One entry in a layer's ordered weighted-range tile list. `Range` is a weight, not an
    /// absolute threshold -- see <see cref="ProcGen.Engine.Selection.TileSelector"/> for how the
    /// ordered list of ranges is turned into cumulative selection bounds.
    /// `Range` is mutable so a tool can live-tune it (e.g. a property panel's +/- nudge); `Id` is
    /// not, since `writes_over` rules and override records reference tiles by id string --
    /// renaming a tile therefore means replacing this instance with a new one (same slot, new id)
    /// and cascading every reference to it, which is what
    /// <see cref="ProcGen.Engine.Editing.RenameOperations.RenameTile"/> does; nothing should ever
    /// mutate `Id` directly.
    /// </summary>
    public sealed class TileDef
    {
        /// <summary>
        /// Reserved id meaning "this weighted-range slot produces no tile" -- selecting it makes
        /// the layer resolve to null at that cell instead of one of its own tiles, letting
        /// whatever layer is below show through (see <see cref="Generation.MapGenerator"/>). Only
        /// meaningful on a layer that has something beneath it to reveal; the engine itself
        /// doesn't restrict which layers may use it -- that's a tool-level guardrail (see
        /// MapEditorToolScene's "Add Blank Range", offered only for non-bottom layers).
        /// </summary>
        public const string NoOverrideId = "no_override";

        public string Id { get; }
        public double Range { get; set; }

        [JsonConstructor]
        public TileDef(string id, double range)
        {
            Id = id;
            Range = range;
        }
    }
}
