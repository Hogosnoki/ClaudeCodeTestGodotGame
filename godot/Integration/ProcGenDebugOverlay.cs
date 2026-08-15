#nullable enable
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Overrides;

namespace ProcGenGame.Integration
{
    /// <summary>
    /// Visual debugger for the generation engine: draws a flat-colored square per cell (keyed by
    /// tile id) with no TileSet/art required, so a map is inspectable before any art exists.
    /// Supports switching between "final composite" and any single layer's raw resolution
    /// (mirrors the spec's "toggleable layer visibility for debugging"), and outlines cells that
    /// carry a manual override so hand-edits are visually distinguishable from procedural output.
    /// Like <see cref="ProcGenTileMapView"/>, this is integration glue: it renders whatever
    /// <see cref="MapResult"/> it's given and never re-derives generation logic itself.
    /// </summary>
    public partial class ProcGenDebugOverlay : Node2D
    {
        [Export] public int CellPixelSize { get; set; } = 16;
        [Export] public bool ShowGridLines { get; set; } = true;
        [Export] public bool ShowOverrideMarkers { get; set; } = true;

        private static readonly Color GridColor = new Color(0, 0, 0, 0.25f);
        private static readonly Color OverrideMarkerColor = new Color(1, 1, 1, 0.9f);
        private static readonly Color UnknownTileColor = new Color(1, 0, 1, 1); // loud magenta: "you forgot to map this tile id"

        private MapResult? _result;
        private RegionSpec _region;
        private OverrideStore? _overrides;

        /// <summary>Which layer's raw resolution to display. Null means "final composite" (the normal in-game view).</summary>
        public string? LayerId { get; private set; }

        private Dictionary<string, Color> _tileColors = new();

        /// <summary>Assigns a display color per engine tile id. Unmapped tile ids render as loud magenta so gaps are obvious.</summary>
        public void SetTileColors(Dictionary<string, Color> colors)
        {
            _tileColors = colors;
            QueueRedraw();
        }

        /// <summary>Switches which layer is displayed. Pass null to show the final composited result.</summary>
        public void ShowLayer(string? layerId)
        {
            LayerId = layerId;
            QueueRedraw();
        }

        /// <summary>Feeds a fresh generation result (and the override store it was generated with) to draw.</summary>
        public void Render(MapResult result, RegionSpec region, OverrideStore overrides)
        {
            _result = result;
            _region = region;
            _overrides = overrides;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_result == null) return;

            for (int x = 0; x < _region.Width; x++)
            {
                for (int y = 0; y < _region.Height; y++)
                {
                    DrawCell(x, y);
                }
            }

            if (ShowGridLines)
            {
                DrawGridLines();
            }
        }

        private void DrawCell(int x, int y)
        {
            string? tile = LayerId == null ? _result!.GetFinalTile(x, y) : _result!.GetLayerTile(LayerId, x, y);
            var rect = new Rect2(x * CellPixelSize, y * CellPixelSize, CellPixelSize, CellPixelSize);

            if (tile == null)
            {
                return; // nothing resolved here on the selected layer -- leave transparent
            }

            Color color = _tileColors.TryGetValue(tile, out var mapped) ? mapped : UnknownTileColor;
            DrawRect(rect, color, filled: true);

            if (ShowOverrideMarkers && _overrides != null && IsOverridden(x, y))
            {
                DrawRect(rect, OverrideMarkerColor, filled: false, width: 2f);
            }
        }

        private bool IsOverridden(int localX, int localY)
        {
            // When showing a single layer, check that layer's own override. When showing the
            // final composite, check whichever layer actually produced the visible tile --
            // GetFinalLayerId is exactly what makes that possible without re-deriving the
            // engine's resolution order here.
            string? layerToCheck = LayerId ?? _result!.GetFinalLayerId(localX, localY);
            if (layerToCheck == null) return false;

            int worldX = _region.OriginX + localX;
            int worldY = _region.OriginY + localY;
            return _overrides!.TryGet(layerToCheck, worldX, worldY, out _);
        }

        private void DrawGridLines()
        {
            float w = _region.Width * CellPixelSize;
            float h = _region.Height * CellPixelSize;

            for (int x = 0; x <= _region.Width; x++)
            {
                float px = x * CellPixelSize;
                DrawLine(new Vector2(px, 0), new Vector2(px, h), GridColor);
            }
            for (int y = 0; y <= _region.Height; y++)
            {
                float py = y * CellPixelSize;
                DrawLine(new Vector2(0, py), new Vector2(w, py), GridColor);
            }
        }

        /// <summary>Converts a local mouse/click position (in this node's local coordinates) to a region-local cell, or null if outside the rendered region.</summary>
        public Vector2I? LocalPositionToCell(Vector2 localPosition)
        {
            int cx = Mathf.FloorToInt(localPosition.X / CellPixelSize);
            int cy = Mathf.FloorToInt(localPosition.Y / CellPixelSize);
            if (_result == null || cx < 0 || cy < 0 || cx >= _region.Width || cy >= _region.Height)
            {
                return null;
            }
            return new Vector2I(cx, cy);
        }
    }
}
