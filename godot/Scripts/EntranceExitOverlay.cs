#nullable enable
using System.Collections.Generic;
using Godot;

namespace ProcGenGame
{
    /// <summary>
    /// Draws colored markers for the currently-selected map's entrance/exit points on top of the
    /// generated terrain -- purely a visualization/placement aid for
    /// <see cref="MapEditorToolScene"/>; no engine data flows through here. Entrances draw green,
    /// exits draw orange, and whichever one (if any) is currently armed for click-to-place draws
    /// with a bright white outline. Positioned identically to <see cref="Integration.ProcGenDebugOverlay"/>
    /// (world cell (wx, wy) at pixel (wx * CellPixelSize, wy * CellPixelSize)) so it lines up with
    /// the terrain underneath without any extra coordinate translation.
    /// </summary>
    public partial class EntranceExitOverlay : Node2D
    {
        [Export] public int CellPixelSize { get; set; } = 20;

        private static readonly Color EntranceColor = new Color(0.25f, 0.85f, 0.35f, 0.9f);
        private static readonly Color ExitColor = new Color(0.95f, 0.55f, 0.15f, 0.9f);
        private static readonly Color ArmedOutlineColor = new Color(1f, 1f, 1f, 0.95f);

        private readonly List<(string Id, int X, int Y)> _entrances = new();
        private readonly List<(string Id, int X, int Y)> _exits = new();
        private (bool IsEntrance, string Id)? _armed;

        /// <summary>Replaces the full set of points to draw. Called whenever the selected map's entrances/exits change, or the armed-for-placement point changes.</summary>
        public void SetPoints(IEnumerable<(string Id, int X, int Y)> entrances, IEnumerable<(string Id, int X, int Y)> exits, (bool IsEntrance, string Id)? armed)
        {
            _entrances.Clear();
            _entrances.AddRange(entrances);
            _exits.Clear();
            _exits.AddRange(exits);
            _armed = armed;
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (var e in _entrances)
            {
                bool armed = _armed.HasValue && _armed.Value.IsEntrance && _armed.Value.Id == e.Id;
                DrawMarker(e.X, e.Y, e.Id, EntranceColor, armed);
            }
            foreach (var e in _exits)
            {
                bool armed = _armed.HasValue && !_armed.Value.IsEntrance && _armed.Value.Id == e.Id;
                DrawMarker(e.X, e.Y, e.Id, ExitColor, armed);
            }
        }

        private void DrawMarker(int worldX, int worldY, string id, Color color, bool armed)
        {
            var center = new Vector2((worldX + 0.5f) * CellPixelSize, (worldY + 0.5f) * CellPixelSize);
            float radius = CellPixelSize * 0.4f;

            DrawCircle(center, radius, color);
            if (armed)
            {
                DrawArc(center, radius + 3f, 0f, Mathf.Tau, 24, ArmedOutlineColor, width: 2.5f);
            }

            var font = ThemeDB.FallbackFont;
            DrawString(font, center + new Vector2(-radius, -radius - 4f), id, HorizontalAlignment.Left, -1, 12, Colors.White);
        }
    }
}
