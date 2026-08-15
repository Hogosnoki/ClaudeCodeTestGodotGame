using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A named point on a map where a player arrives after taking an exit (on this map or any
    /// other) whose DestinationEntranceId matches <see cref="Id"/>. Id is immutable once created,
    /// like <see cref="TileDef"/>.Id -- exits reference it by string, and changing it out from
    /// under them would silently break those references. It only needs to be unique within its
    /// parent map, not across the whole game -- see <see cref="MapDefinition.Entrances"/>.
    /// </summary>
    public sealed class EntrancePoint
    {
        public string Id { get; }
        public int X { get; set; }
        public int Y { get; set; }

        [JsonConstructor]
        public EntrancePoint(string id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
        }
    }
}
