using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A named point on a map where a player leaves toward <see cref="DestinationMapId"/>, to
    /// arrive at that map's entrance identified by <see cref="DestinationEntranceId"/>. Both
    /// destination fields are plain strings naming a map/entrance elsewhere in the *same* project
    /// -- a project file is one whole game, so every valid destination is always available
    /// in-memory alongside this map, with no cross-file lookup involved. They can still go
    /// dangling (e.g. the destination map or entrance was since deleted); see
    /// <see cref="ProcGen.Engine.Validation.ProjectValidation.FindDanglingExits"/> for the
    /// diagnostic that catches that without treating it as a hard error.
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
