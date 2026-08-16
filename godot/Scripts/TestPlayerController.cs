#nullable enable
using System;
using Godot;

namespace ProcGenGame
{
    /// <summary>
    /// The "Test" mode player: a plain circle (no art yet -- this exists to exercise the Movement
    /// panel's traversal rules interactively, not to look like anything) that moves pixel-smoothly
    /// rather than snapping tile to tile. Purely a visualization + local-movement node, like
    /// <see cref="EntranceExitOverlay"/>/<see cref="TraversalOverlay"/> -- it doesn't know about
    /// <c>MapDefinition</c> or the engine at all; <see cref="MapEditorToolScene"/> owns generation
    /// data and hands this a `canEnter` callback per move.
    /// </summary>
    public partial class TestPlayerController : Node2D
    {
        [Export] public float Radius { get; set; } = 8f;
        [Export] public float Speed { get; set; } = 140f; // pixels/sec

        private static readonly Color BodyColor = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

        public override void _Draw()
        {
            DrawCircle(Vector2.Zero, Radius, BodyColor);
            DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 24, OutlineColor, width: 2f);
        }

        /// <summary>
        /// Moves by inputDir*Speed*delta, resolved one axis at a time (X then Y) so the player
        /// slides along a blocked edge instead of stopping dead on both axes when approaching it
        /// diagonally -- the usual axis-separated approach for grid-aware continuous movement.
        /// canEnter(fromCellWorld, toCellWorld) decides whether a step across a cell boundary is
        /// allowed; it's never consulted for a move that stays within the current cell.
        /// </summary>
        public void TryMove(Vector2 inputDir, float delta, int cellPixelSize, Func<Vector2I, Vector2I, bool> canEnter)
        {
            if (inputDir == Vector2.Zero) return;
            Vector2 move = inputDir.Normalized() * Speed * delta;

            if (move.X != 0f) Position = ResolveAxis(Position, new Vector2(move.X, 0f), cellPixelSize, canEnter);
            if (move.Y != 0f) Position = ResolveAxis(Position, new Vector2(0f, move.Y), cellPixelSize, canEnter);
        }

        private static Vector2 ResolveAxis(Vector2 pos, Vector2 delta, int cellPixelSize, Func<Vector2I, Vector2I, bool> canEnter)
        {
            Vector2 next = pos + delta;
            Vector2I fromCell = ToCell(pos, cellPixelSize);
            Vector2I toCell = ToCell(next, cellPixelSize);
            if (fromCell == toCell || canEnter(fromCell, toCell)) return next;
            return pos;
        }

        private static Vector2I ToCell(Vector2 pixelPos, int cellPixelSize) =>
            new Vector2I(Mathf.FloorToInt(pixelPos.X / cellPixelSize), Mathf.FloorToInt(pixelPos.Y / cellPixelSize));
    }
}
