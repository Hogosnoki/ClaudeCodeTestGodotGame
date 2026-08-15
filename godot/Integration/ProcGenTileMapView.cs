#nullable enable
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Generation;

namespace ProcGenGame.Integration
{
    /// <summary>
    /// Thin rendering adapter: paints a <see cref="MapResult"/> onto a <see cref="TileMapLayer"/>
    /// using a tile-id -> atlas-coords mapping the consumer provides. This is integration
    /// glue, not engine logic -- it knows nothing about noise, layers, or overrides, only how to
    /// turn the engine's already-resolved output into TileMapLayer cells. Depends on Godot
    /// (unlike the engine) because that is exactly what a "thin consumer" is for.
    /// </summary>
    public partial class ProcGenTileMapView : Node2D
    {
        /// <summary>The TileMapLayer this view paints into. Assign in code or via the editor.</summary>
        [Export] public TileMapLayer? TargetTileMap { get; set; }

        /// <summary>TileSet source id to paint with (matches the source you set up in the TileSet resource).</summary>
        [Export] public int SourceId { get; set; } = 0;

        /// <summary>Maps each engine tile id (e.g. "land", "tallgrass") to its atlas coordinates in the TileSet.</summary>
        public Dictionary<string, Vector2I> TileAtlasCoords { get; set; } = new();

        /// <summary>
        /// Clears and repaints the target TileMapLayer from a generation result. World cell
        /// (region.OriginX + x, region.OriginY + y) is used as the TileMapLayer cell coordinate,
        /// so painted regions line up correctly even when generating a region that doesn't start
        /// at the origin.
        /// </summary>
        public void Render(MapResult result, RegionSpec region)
        {
            if (TargetTileMap == null)
            {
                GD.PushError($"{nameof(ProcGenTileMapView)}: {nameof(TargetTileMap)} is not assigned.");
                return;
            }

            TargetTileMap.Clear();

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    string? tile = result.GetFinalTile(x, y);
                    if (tile == null) continue;

                    if (!TileAtlasCoords.TryGetValue(tile, out var coords))
                    {
                        GD.PushWarning($"{nameof(ProcGenTileMapView)}: no atlas mapping for tile id '{tile}'.");
                        continue;
                    }

                    var cell = new Vector2I(region.OriginX + x, region.OriginY + y);
                    TargetTileMap.SetCell(cell, SourceId, coords);
                }
            }
        }
    }
}
