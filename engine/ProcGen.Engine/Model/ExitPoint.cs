using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A named point on a map where a player leaves toward <see cref="DestinationMapId"/>, to
    /// arrive at that map's entrance identified by <see cref="DestinationEntranceId"/>. Both
    /// destination fields are plain strings, not validated against the other map's file -- maps
    /// are saved/loaded independently, so a dangling reference (destination not found at runtime)
    /// is a game-side concern, not something this engine can check from one file alone.
    /// Id is immutable once created, like <see cref="EntrancePoint"/>.Id, and only needs to be
    /// unique within its parent map -- see <see cref="MapDefinition.Exits"/>.
    /// </summary>
    public sealed class ExitPoint
    {
        public string Id { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public string DestinationMapId { get; set; }
        public string DestinationEntranceId { get; set; }

        [JsonConstructor]
        public ExitPoint(string id, int x, int y, string destinationMapId, string destinationEntranceId)
        {
            Id = id;
            X = x;
            Y = y;
            DestinationMapId = destinationMapId;
            DestinationEntranceId = destinationEntranceId;
        }
    }
}
