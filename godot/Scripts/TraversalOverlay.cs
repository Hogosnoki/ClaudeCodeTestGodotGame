#nullable enable
using Godot;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Movement;

namespace ProcGenGame
{
    /// <summary>
    /// Draws a thin red line on every adjacent-cell edge the current
    /// <see cref="CompiledTraversalRules"/> blocks in either direction -- pure visualization for
    /// <see cref="MapEditorToolScene"/>'s Movement panel, no engine data flows through here.
    /// Evaluated against each cell's actual final resolved tile id in the currently generated
    /// viewport (never the abstract rule list directly), so what's drawn always matches what
    /// "Solid"/"Open"/hand-added rules actually produce once terrain is generated -- including
    /// rules that reference a tile id no longer used anywhere on the map, which simply never
    /// draw anything. Positioned identically to <see cref="Integration.ProcGenDebugOverlay"/> and
    /// <see cref="EntranceExitOverlay"/> (world cell (wx, wy) at pixel
    /// (wx * CellPixelSize, wy * CellPixelSize)), so it lines up with the terrain underneath
    /// without any extra coordinate translation.
    /// </summary>
    public partial class TraversalOverlay : Node2D
    {
        [Export] public int CellPixelSize { get; set; } = 20;

        private static readonly Color BlockedEdgeColor = new Color(0.9f, 0.15f, 0.15f, 0.95f);
        private const float LineWidth = 3f;

        private MapResult? _result;
        private RegionSpec _region;
        private CompiledTraversalRules? _rules;

        /// <summary>Feeds a fresh generation result and its matching compiled rule set to draw against. Called every time the viewport regenerates or the rule set changes.</summary>
        public void Render(MapResult result, RegionSpec region, CompiledTraversalRules rules)
        {
            _result = result;
            _region = region;
            _rules = rules;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_result == null || _rules == null) return;

            for (int x = 0; x < _region.Width; x++)
            {
                for (int y = 0; y < _region.Height; y++)
                {
                    string? here = _result.GetFinalTile(x, y);
                    if (here == null) continue;

                    if (x + 1 < _region.Width)
                    {
                        string? right = _result.GetFinalTile(x + 1, y);
                        if (right != null && IsEdgeBlocked(here, right))
                        {
                            DrawWorldEdge(_region.OriginX + x + 1, _region.OriginY + y, vertical: true);
                        }
                    }
                    if (y + 1 < _region.Height)
                    {
                        string? down = _result.GetFinalTile(x, y + 1);
                        if (down != null && IsEdgeBlocked(here, down))
                        {
                            DrawWorldEdge(_region.OriginX + x, _region.OriginY + y + 1, vertical: false);
                        }
                    }
                }
            }
        }

        private bool IsEdgeBlocked(string a, string b) => _rules!.IsBlocked(a, b) || _rules.IsBlocked(b, a);

        /// <summary>
        /// Draws the line along world-cell boundary (worldX, worldY): vertical draws the seam
        /// between (worldX-1, worldY) and (worldX, worldY); horizontal draws the seam between
        /// (worldX, worldY-1) and (worldX, worldY).
        /// </summary>
        private void DrawWorldEdge(int worldX, int worldY, bool vertical)
        {
            float px = worldX * CellPixelSize;
            float py = worldY * CellPixelSize;
            var from = new Vector2(px, py);
            var to = vertical ? new Vector2(px, py + CellPixelSize) : new Vector2(px + CellPixelSize, py);
            DrawLine(from, to, BlockedEdgeColor, LineWidth);
        }
    }
}
