namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One entry in a layer's ordered weighted-range tile list. `Range` is a weight, not an
    /// absolute threshold -- see <see cref="ProcGen.Engine.Selection.TileSelector"/> for how the
    /// ordered list of ranges is turned into cumulative selection bounds.
    /// </summary>
    public sealed class TileDef
    {
        public string Id { get; }
        public double Range { get; }

        public TileDef(string id, double range)
        {
            Id = id;
            Range = range;
        }
    }
}
