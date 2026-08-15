#nullable enable
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Validation;
using ProcGenGame.Integration;

namespace ProcGenGame
{
    /// <summary>
    /// Interactive visual debugger for the milestone-1 scenario. Run this scene directly (open
    /// Scenes/VisualDebugDemo.tscn in the editor and press F6) to see the generated map as
    /// colored cells, switch which layer is displayed, hand-paint overrides by clicking, and step
    /// the Transformation axis while watching the <= 0.1 clamp rule apply live.
    ///
    /// Controls:
    ///   1 / 2 / 3   - view layer "ground" / "ground_cover" / final composite
    ///   [ / ]       - step Transformation by -0.1 / +0.1
    ///   P           - attempt a +1.0 Transformation jump (watch it clamp to +0.1)
    ///   Left click  - cycle the override at that cell through the viewed layer's tile list
    ///   Right click - clear the override at that cell
    ///   R           - clear all overrides
    /// </summary>
    public partial class VisualDebugDemoScene : Node2D
    {
        private const int CellPixelSize = 24;
        private static readonly RegionSpec BaseRegion = new RegionSpec(originX: 0, originY: 0, width: 32, height: 20, transformation: 0.0);

        private MapDefinition _definition = null!;
        private OverrideStore _overrides = null!;
        private double _transformation;
        private ProcGenDebugOverlay _overlay = null!;
        private Label _hud = null!;

        public override void _Ready()
        {
            _definition = BuildDefinition();
            _overrides = new OverrideStore();

            _overlay = GetNode<ProcGenDebugOverlay>("Overlay");
            _overlay.CellPixelSize = CellPixelSize;
            _overlay.SetTileColors(new Dictionary<string, Color>
            {
                ["deep_water"] = new Color(0.10f, 0.20f, 0.55f),
                ["shallow_water"] = new Color(0.25f, 0.45f, 0.85f),
                ["sand"] = new Color(0.85f, 0.75f, 0.45f),
                ["land"] = new Color(0.45f, 0.35f, 0.20f),
                ["dirt"] = new Color(0.40f, 0.28f, 0.15f),
                ["grass"] = new Color(0.30f, 0.65f, 0.25f),
                ["tallgrass"] = new Color(0.15f, 0.45f, 0.15f),
            });
            _overlay.ShowLayer(null); // final composite by default

            _hud = GetNode<Label>("HUD/Label");

            Regenerate();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false } key)
            {
                HandleKey(key.Keycode);
            }
            else if (@event is InputEventMouseButton { Pressed: true } mouse)
            {
                HandleClick(mouse);
            }
        }

        private void HandleKey(Key key)
        {
            switch (key)
            {
                case Key.Key1: _overlay.ShowLayer("ground"); break;
                case Key.Key2: _overlay.ShowLayer("ground_cover"); break;
                case Key.Key3: _overlay.ShowLayer(null); break;
                case Key.Bracketright: StepTransformation(0.1); break;
                case Key.Bracketleft: StepTransformation(-0.1); break;
                case Key.P: StepTransformation(1.0); break; // deliberately oversized, to show the clamp
                case Key.R:
                    _overrides = new OverrideStore();
                    Regenerate();
                    break;
                default: return;
            }
            Redraw();
        }

        private void StepTransformation(double requestedDelta)
        {
            double requested = _transformation + requestedDelta;
            double clamped = TransformationAxis.ClampStep(_transformation, requested);
            _transformation = clamped;
            Regenerate();
        }

        private void HandleClick(InputEventMouseButton mouse)
        {
            string? layerId = _overlay.LayerId;
            if (layerId == null)
            {
                UpdateHud("Select a single layer first (1 or 2) before hand-painting.");
                return;
            }

            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;

            int worldX = BaseRegion.OriginX + cell.Value.X;
            int worldY = BaseRegion.OriginY + cell.Value.Y;

            if (mouse.ButtonIndex == MouseButton.Right)
            {
                _overrides.Clear(layerId, worldX, worldY);
                Regenerate();
                return;
            }

            if (mouse.ButtonIndex != MouseButton.Left) return;

            var layer = _definition.Layers.Find(l => l.Id == layerId);
            if (layer == null) return;

            int nextIndex = 0;
            if (_overrides.TryGet(layerId, worldX, worldY, out var current))
            {
                nextIndex = layer.Tiles.FindIndex(t => t.Id == current) + 1;
            }

            if (nextIndex >= layer.Tiles.Count)
            {
                _overrides.Clear(layerId, worldX, worldY); // cycled past the last tile: back to procedural
            }
            else
            {
                _overrides.Set(layerId, worldX, worldY, layer.Tiles[nextIndex].Id);
            }

            Regenerate();
        }

        private void Regenerate()
        {
            var region = new RegionSpec(BaseRegion.OriginX, BaseRegion.OriginY, BaseRegion.Width, BaseRegion.Height, _transformation);
            var result = MapGenerator.GenerateRegion(_definition, region, _overrides);
            _overlay.Render(result, region, _overrides);
            Redraw();
        }

        private void Redraw()
        {
            string layerLabel = _overlay.LayerId ?? "final composite";
            UpdateHud($"Layer: {layerLabel}   Transformation: {_transformation:0.00}   Overrides: {_overrides.Count}\n" +
                      "[1/2/3] switch layer   [ ]/[ ] step T   [P] oversized T jump (watch it clamp)\n" +
                      "Left-click: cycle override   Right-click: clear override   [R] clear all overrides");
        }

        private void UpdateHud(string text) => _hud.Text = text;

        private static MapDefinition BuildDefinition()
        {
            var ground = new LayerDef(
                "ground",
                new List<TileDef>
                {
                    new TileDef("deep_water", 0.7),
                    new TileDef("shallow_water", 0.3),
                    new TileDef("sand", 0.2),
                    new TileDef("land", 2.0),
                },
                new List<WritesOverRule>(),
                new SeedPosition(1000.37, 2000.81, 0.0),
                new NoiseParams { Octaves = 3, Frequency = 0.05, Persistence = 0.5, Lacunarity = 2.0 });

            var groundCover = new LayerDef(
                "ground_cover",
                new List<TileDef>
                {
                    new TileDef("dirt", 0.4),
                    new TileDef("grass", 0.8),
                    new TileDef("tallgrass", 0.6),
                },
                new List<WritesOverRule> { new WritesOverRule("ground", "land") },
                new SeedPosition(-500.62, 7500.19, 0.0),
                new NoiseParams { Octaves = 2, Frequency = 0.08, Persistence = 0.5, Lacunarity = 2.0 });

            var def = new MapDefinition { WorldSeed = 12345 };
            def.Layers.Add(ground);
            def.Layers.Add(groundCover);
            return def;
        }
    }
}
