using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One entry in a layer's ordered weighted-range tile list. `Range` is a weight, not an
    /// absolute threshold -- see <see cref="ProcGen.Engine.Selection.TileSelector"/> for how the
    /// ordered list of ranges is turned into cumulative selection bounds.
    /// `Range` is mutable so a tool can live-tune it (e.g. a property panel's +/- nudge); `Id` is
    /// not, since `writes_over` rules and override records reference tiles by id string and
    /// changing it out from under them would silently break those references.
    /// </summary>
    public sealed class TileDef
    {
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
