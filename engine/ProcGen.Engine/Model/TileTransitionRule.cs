using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One directional movement-blocking rule: a character standing on a cell whose final tile is
    /// FromTileId cannot move onto an adjacent cell whose final tile is ToTileId. Blocking is
    /// evaluated purely on final tile ids (see MapResult.GetFinalTile), never on layers -- the two
    /// tiles named here can come from entirely different layers (e.g. "shallow_water" on a ground
    /// layer, "grass" on a ground_cover layer), since a rule only ever compares what a player
    /// actually sees standing at each cell.
    /// Symmetric blocking is just two rules, one each direction; that asymmetry is also what lets
    /// a one-way transition (down a ledge but not back up) exist as an ordinary rule rather than a
    /// special case. This is deliberately the *only* movement-blocking mechanism -- there is no
    /// separate per-tile "walkable" flag. "Solid" in the editor is a convenience that bulk-adds
    /// rules of this same type rather than a distinct concept; see
    /// <see cref="ProcGen.Engine.Movement.TraversalEditing.MakeSolid"/>.
    /// </summary>
    public readonly struct TileTransitionRule
    {
        public string FromTileId { get; }
        public string ToTileId { get; }

        [JsonConstructor]
        public TileTransitionRule(string fromTileId, string toTileId)
        {
            FromTileId = fromTileId;
            ToTileId = toTileId;
        }
    }
}
