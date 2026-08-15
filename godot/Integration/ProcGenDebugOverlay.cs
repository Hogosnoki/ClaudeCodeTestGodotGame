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
    ///
    /// Cells are drawn at their absolute world position (world cell (wx, wy) always draws at
    /// pixel (wx * CellPixelSize, wy * CellPixelSize) in this node's own local space), not at a
    /// position relative to the rendered region's origin. That's what lets a consumer freely pan
    /// a camera around this node (e.g. by moving/scaling a parent Node2D) without needing to
    /// reposition the overlay itself every time a different region gets generated.
    /// </summary>
    public partial class ProcGenDebugOverlay : Node2D
    {
        [Export] public int CellPixelSize { get; set; } = 16;
        [Export] public bool ShowGridLines { get; set; } = true;
        [Export] public bool ShowOverrideMarkers { get; set; } = true;

        private static readonly Color GridColor = new Color(0, 0, 0, 0.25f);
        private static readonly Color OverrideMarkerColor = new Color(1, 1, 1, 0.9f);
        private static readonly Color UnknownTileColor = new Color(1, 0, 1, 1); // loud magenta: "you forgot to map this tile id"
        private static readonly Color DesignatedAreaBorderColor = new Color(1f, 0.95f, 0.3f, 0.9f);
        private const float DimAmount = 0.6f; // how far outside-area cells are darkened toward black, 0..1

        private MapResult? _result;
        private RegionSpec _region;
        private OverrideStore? _overrides;

        /// <summary>Which layer's raw resolution to display. Null means "final composite" (the normal in-game view).</summary>
        public string? LayerId { get; private set; }

        /// <summary>
        /// World-cell rectangle of the "real" map that will actually be used/exported. Cells
        /// rendered outside this area (still generated so the surrounding space is navigable) are
        /// dimmed and the area's boundary is outlined, so it's visually obvious where the
        /// designated map ends and the rest of the (effectively infinite) generation space begins.
        /// Null disables dimming entirely (draws everything at full brightness).
        /// </summary>
        public Rect2I? DesignatedArea { get; set; }

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

            DrawDesignatedAreaBorder();
        }

        private void DrawCell(int x, int y)
        {
            string? tile = LayerId == null ? _result!.GetFinalTile(x, y) : _result!.GetLayerTile(LayerId, x, y);
            if (tile == null)
            {
                return; // nothing resolved here on the selected layer -- leave transparent
            }

            int worldX = _region.OriginX + x;
            int worldY = _region.OriginY + y;
            var rect = new Rect2(worldX * CellPixelSize, worldY * CellPixelSize, CellPixelSize, CellPixelSize);

            Color color = _tileColors.TryGetValue(tile, out var mapped) ? mapped : UnknownTileColor;
            if (DesignatedArea.HasValue && !IsInsideDesignatedArea(worldX, worldY))
            {
                color = color.Darkened(DimAmount);
            }
            DrawRect(rect, color, filled: true);

            if (ShowOverrideMarkers && _overrides != null && IsOverridden(x, y))
            {
                DrawRect(rect, OverrideMarkerColor, filled: false, width: 2f);
            }
        }

        private bool IsInsideDesignatedArea(int worldX, int worldY)
        {
            var area = DesignatedArea!.Value;
            return worldX >= area.Position.X && worldX < area.Position.X + area.Size.X &&
                   worldY >= area.Position.Y && worldY < area.Position.Y + area.Size.Y;
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
            float left = _region.OriginX * CellPixelSize;
            float top = _region.OriginY * CellPixelSize;
            float right = (_region.OriginX + _region.Width) * CellPixelSize;
            float bottom = (_region.OriginY + _region.Height) * CellPixelSize;

            for (int x = 0; x <= _region.Width; x++)
            {
                float px = left + x * CellPixelSize;
                DrawLine(new Vector2(px, top), new Vector2(px, bottom), GridColor);
            }
            for (int y = 0; y <= _region.Height; y++)
            {
                float py = top + y * CellPixelSize;
                DrawLine(new Vector2(left, py), new Vector2(right, py), GridColor);
            }
        }

        private void DrawDesignatedAreaBorder()
        {
            if (!DesignatedArea.HasValue) return;
            var area = DesignatedArea.Value;
            var rect = new Rect2(
                area.Position.X * CellPixelSize, area.Position.Y * CellPixelSize,
                area.Size.X * CellPixelSize, area.Size.Y * CellPixelSize);
            DrawRect(rect, DesignatedAreaBorderColor, filled: false, width: 2f);
        }

        /// <summary>
        /// Converts a local position (in this node's local coordinates -- see the class doc
        /// comment on what "local" means here) to the absolute world cell it falls in, or null if
        /// that cell isn't part of the currently rendered region (so there's no data for it yet).
        /// </summary>
        public Vector2I? LocalPositionToCell(Vector2 localPosition)
        {
            if (_result == null) return null;

            int wx = Mathf.FloorToInt(localPosition.X / CellPixelSize);
            int wy = Mathf.FloorToInt(localPosition.Y / CellPixelSize);

            if (wx < _region.OriginX || wy < _region.OriginY ||
                wx >= _region.OriginX + _region.Width || wy >= _region.OriginY + _region.Height)
            {
                return null;
            }
            return new Vector2I(wx, wy);
        }
    }
}
