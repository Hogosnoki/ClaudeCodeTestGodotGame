using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One allow-list entry: "this layer may generate over the tile identified by TileId,
    /// resolved on the layer identified by LayerId". A <see cref="LayerDef"/> carries a list of
    /// these (not a single dependency), and any lower layer may be referenced -- not only the
    /// immediately preceding one. A layer is eligible to generate at a cell if ANY of its rules
    /// matches that cell's currently-resolved output on the referenced layer.
    /// </summary>
    public readonly struct WritesOverRule
    {
        public string LayerId { get; }
        public string TileId { get; }

        [JsonConstructor]
        public WritesOverRule(string layerId, string tileId)
        {
            LayerId = layerId;
            TileId = tileId;
        }
    }
}
