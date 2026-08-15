#nullable enable
using System;
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
    /// The map-making tool: a fixed side property panel (region, Transformation, the selected
    /// layer's seed, and the selected layer's tile ranges) driving live regeneration, plus a free
    /// pan/zoom camera over the (effectively infinite) generation space. This is the
    /// "expedite map making by selecting generation settings rather than drawing each tile"
    /// workflow -- click-to-paint overrides still work on top of it (in Draw Mode) for the cases
    /// that genuinely need a hand-painted exception, but tuning parameters is the primary way to
    /// shape a map here.
    ///
    /// Every field writes straight into the same <see cref="MapDefinition"/> /
    /// <see cref="OverrideStore"/> the engine consumes -- there is no separate "tool state" that
    /// could drift from what actually gets generated.
    ///
    /// Two rectangles matter here and are easy to conflate:
    ///   - The "designated area" (Region panel: Origin X/Y, Width/Height) -- the actual map that
    ///     would get exported/used. Content outside it renders dimmed, and it's the target the
    ///     "Return to Map Area" button recenters on. Editing these fields never moves the camera.
    ///   - The "viewport" -- whatever's currently visible on screen, computed fresh from the
    ///     camera's pan/zoom every time something needs regenerating. This is what actually gets
    ///     generated and rendered; the designated area is otherwise irrelevant to generation.
    /// </summary>
    public partial class MapEditorToolScene : Control
    {
        private const int CellPixelSize = 20;
        private const int SidePanelWidth = 400;

        private const float MinZoom = 0.2f;
        private const float MaxZoom = 3.0f;
        private const float ZoomStep = 1.15f;
        private const int ViewportMargin = 2; // extra cells generated past the visible edge, so panning doesn't show a bare edge for one frame
        private const int MaxViewportCells = 300; // safety cap on viewport width/height regardless of zoom/window size

        // Display color per tile id -- a rendering concern, so it lives here in the tool, not on
        // the engine's TileDef. Colors stand in for tile art until real art exists ("represented
        // by color blocks for now"); a tool built against real art would swap this for atlas
        // coordinates the same way ProcGenTileMapView does.
        private readonly Dictionary<string, Color> _tileColors = new Dictionary<string, Color>
        {
            ["deep_water"] = new Color(0.10f, 0.20f, 0.55f),
            ["shallow_water"] = new Color(0.25f, 0.45f, 0.85f),
            ["sand"] = new Color(0.85f, 0.75f, 0.45f),
            ["land"] = new Color(0.45f, 0.35f, 0.20f),
            ["dirt"] = new Color(0.40f, 0.28f, 0.15f),
            ["grass"] = new Color(0.30f, 0.65f, 0.25f),
            ["tallgrass"] = new Color(0.15f, 0.45f, 0.15f),
        };

        private MapDefinition _definition = null!;
        private OverrideStore _overrides = null!;
        private ProcGenDebugOverlay _overlay = null!;
        private Node2D _worldRoot = null!;

        // Designated area (see class doc comment).
        private int _originX;
        private int _originY;
        private int _regionWidth = 32;
        private int _regionHeight = 20;
        private double _transformation;

        // Camera: _worldRoot.Position is the screen-pixel location of world pixel (0,0);
        // _zoom scales world pixels to screen pixels on top of that.
        private float _zoom = 1.0f;
        private bool _isPanning;
        private Vector2 _lastPanMousePos;
        private RegionSpec? _lastViewport;

        private int _selectedLayerIndex;
        private bool _showFinalComposite;
        private bool _drawMode = true;

        private ItemList _layerList = null!;
        private SpinBox _seedXBox = null!;
        private SpinBox _seedYBox = null!;
        private SpinBox _seedTBox = null!;
        private SpinBox _transformationBox = null!;
        private VBoxContainer _tilesContainer = null!;
        private LineEdit _newTileIdEdit = null!;
        private Label _addTileHintLabel = null!;
        private Label _statusLabel = null!;
        private Label _coordsLabel = null!;
        private CheckBox _compositeToggle = null!;
        private CheckBox _drawModeToggle = null!;

        // Guards programmatic SpinBox.Value assignments (e.g. syncing seed fields on layer
        // switch) from re-entering the same handler that would just write the value straight back.
        private bool _suppressSignals;

        public override void _Ready()
        {
            // A plain Control defaults to MouseFilter.Stop, which would swallow every click over
            // its full-rect bounds -- including the empty map area -- before _UnhandledInput ever
            // sees it. Ignore lets clicks fall through to _UnhandledInput wherever no interactive
            // child (a panel button/field) claims them first.
            MouseFilter = MouseFilterEnum.Ignore;

            _definition = BuildDefinition();
            _overrides = new OverrideStore();

            BuildUi();
            _overlay.SetTileColors(_tileColors);

            RefreshLayerList();
            CenterOnDesignatedArea();
            SelectLayer(0);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton mouseButton:
                    HandleMouseButton(mouseButton);
                    break;
                case InputEventMouseMotion motion:
                    HandleMouseMotion(motion);
                    break;
            }
        }

        // ---------- UI construction ----------

        private void BuildUi()
        {
            // Clips map content to the viewport area so it can never render on top of the side
            // panel -- sibling draw order alone isn't reliable protection at every zoom level
            // (the panel's own StyleBox background is opaque, but child CanvasItems reparented or
            // redrawn out of order can still peek through at the panel's edge), and clipping is
            // cheap insurance regardless of the exact cause.
            var mapClip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            mapClip.AnchorLeft = 0f; mapClip.AnchorTop = 0f; mapClip.AnchorBottom = 1f;
            mapClip.AnchorRight = 1f; mapClip.OffsetRight = -SidePanelWidth;
            AddChild(mapClip);

            _worldRoot = new Node2D();
            mapClip.AddChild(_worldRoot);
            _overlay = new ProcGenDebugOverlay { CellPixelSize = CellPixelSize };
            _worldRoot.AddChild(_overlay);

            BuildMapHud();
            BuildSidePanel();
        }

        /// <summary>Floating controls over the map viewport itself -- deliberately not inside the scrollable side panel, so they're reachable no matter how lost the camera gets.</summary>
        private void BuildMapHud()
        {
            var returnButton = new Button { Text = "Return to Map Area" };
            returnButton.AnchorLeft = 0f; returnButton.AnchorTop = 0f; returnButton.AnchorRight = 0f; returnButton.AnchorBottom = 0f;
            returnButton.OffsetLeft = 12; returnButton.OffsetTop = 12;
            returnButton.Pressed += CenterOnDesignatedArea;
            AddChild(returnButton);

            _drawModeToggle = new CheckBox { Text = "Draw Mode (uncheck to pan by dragging)", ButtonPressed = true };
            _drawModeToggle.AnchorLeft = 1f; _drawModeToggle.AnchorRight = 1f; _drawModeToggle.AnchorTop = 0f; _drawModeToggle.AnchorBottom = 0f;
            _drawModeToggle.OffsetRight = -(SidePanelWidth + 12);
            _drawModeToggle.OffsetLeft = -(SidePanelWidth + 12 + 300);
            _drawModeToggle.OffsetTop = 12;
            _drawModeToggle.Toggled += on => { _drawMode = on; _isPanning = false; };
            AddChild(_drawModeToggle);

            _coordsLabel = new Label { Text = "Mouse: (-, -)   Zoom: 100%" };
            _coordsLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
            _coordsLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _coordsLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            _coordsLabel.AnchorLeft = 0f; _coordsLabel.AnchorRight = 0f; _coordsLabel.AnchorTop = 1f; _coordsLabel.AnchorBottom = 1f;
            _coordsLabel.OffsetLeft = 12; _coordsLabel.OffsetTop = -32; _coordsLabel.OffsetRight = 320; _coordsLabel.OffsetBottom = -8;
            AddChild(_coordsLabel);
        }

        private void BuildSidePanel()
        {
            var panel = new PanelContainer
            {
                AnchorLeft = 1f,
                AnchorRight = 1f,
                AnchorTop = 0f,
                AnchorBottom = 1f,
                OffsetLeft = -SidePanelWidth,
                OffsetRight = 0f,
                OffsetTop = 0f,
                OffsetBottom = 0f,
            };
            AddChild(panel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 10);
            // Extra right margin clears the ScrollContainer's vertical scrollbar, which overlays
            // rather than reserving layout space -- without this, the rightmost widget in a row
            // (the "+0.1"/tile "+" buttons) renders partly under the scrollbar thumb.
            margin.AddThemeConstantOverride("margin_right", 26);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            panel.AddChild(margin);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            margin.AddChild(scroll);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(root);

            _statusLabel = new Label();
            root.AddChild(_statusLabel);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Region (designated map area)"));
            AddIntField(root, "Origin X", _originX, -100000, 100000, v => { _originX = v; Regenerate(); });
            AddIntField(root, "Origin Y", _originY, -100000, 100000, v => { _originY = v; Regenerate(); });
            AddIntField(root, "Width", _regionWidth, 1, 200, v => { _regionWidth = v; Regenerate(); });
            AddIntField(root, "Height", _regionHeight, 1, 200, v => { _regionHeight = v; Regenerate(); });
            root.AddChild(new Label
            {
                Text = "Content outside this area renders dimmed. Editing these fields doesn't move the camera -- use the coordinate readout to find where to place it, then type it in here.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Transformation"));
            var tRow = new HBoxContainer();
            var minusT = new Button { Text = "-0.1" };
            minusT.Pressed += () => StepTransformation(-0.1);
            tRow.AddChild(minusT);
            // Fixed (not expand-fill) width: the ScrollContainer this sits in doesn't reliably
            // clip an expand-fill child to available width, so a flexible width here can push
            // the trailing "+0.1" button out past the visible panel. A fixed width keeps the
            // row's total size small and predictable regardless.
            _transformationBox = new SpinBox { Step = 0.01, MinValue = -100000, MaxValue = 100000, CustomMinimumSize = new Vector2(110, 0) };
            _transformationBox.Value = _transformation;
            _transformationBox.ValueChanged += OnTransformationBoxChanged;
            tRow.AddChild(_transformationBox);
            var plusT = new Button { Text = "+0.1" };
            plusT.Pressed += () => StepTransformation(0.1);
            tRow.AddChild(plusT);
            root.AddChild(tRow);
            root.AddChild(new Label
            {
                Text = "Larger steps are clamped to 0.1, to keep shapes continuous.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Layer"));
            _layerList = new ItemList { CustomMinimumSize = new Vector2(0, 70) };
            _layerList.ItemSelected += index => SelectLayer((int)index);
            root.AddChild(_layerList);

            _compositeToggle = new CheckBox { Text = "Show final composite" };
            _compositeToggle.Toggled += on =>
            {
                _showFinalComposite = on;
                UpdateOverlayView();
                Regenerate();
            };
            root.AddChild(_compositeToggle);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Seed (selected layer)"));
            _seedXBox = AddDoubleField(root, "X", -1000000, 1000000, OnSeedChanged);
            _seedYBox = AddDoubleField(root, "Y", -1000000, 1000000, OnSeedChanged);
            _seedTBox = AddDoubleField(root, "T", -1000000, 1000000, OnSeedChanged);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Tiles (selected layer)"));
            _tilesContainer = new VBoxContainer();
            root.AddChild(_tilesContainer);

            var addRow = new HBoxContainer();
            _newTileIdEdit = new LineEdit { PlaceholderText = "new tile id", CustomMinimumSize = new Vector2(200, 0) };
            addRow.AddChild(_newTileIdEdit);
            var addTileButton = new Button { Text = "Add Tile" };
            addTileButton.Pressed += OnAddTilePressed;
            addRow.AddChild(addTileButton);
            root.AddChild(addRow);

            _addTileHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_addTileHintLabel);
        }

        private static Label Header(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 16);
            return label;
        }

        private SpinBox AddIntField(VBoxContainer parent, string label, int value, int min, int max, Action<int> onChanged)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
            var spin = new SpinBox { Step = 1, MinValue = min, MaxValue = max, Rounded = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            spin.Value = value;
            spin.ValueChanged += v =>
            {
                if (_suppressSignals) return;
                onChanged((int)Math.Round(v));
            };
            row.AddChild(spin);
            parent.AddChild(row);
            return spin;
        }

        private SpinBox AddDoubleField(VBoxContainer parent, string label, double min, double max, Action onChanged)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
            var spin = new SpinBox { Step = 0.01, MinValue = min, MaxValue = max, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            spin.ValueChanged += _ =>
            {
                if (_suppressSignals) return;
                onChanged();
            };
            row.AddChild(spin);
            parent.AddChild(row);
            return spin;
        }

        // ---------- Camera: pan, zoom, recentre ----------

        private void HandleMouseButton(InputEventMouseButton mouse)
        {
            if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp)
            {
                HandleZoom(zoomIn: true);
                return;
            }
            if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown)
            {
                HandleZoom(zoomIn: false);
                return;
            }

            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (!_drawMode)
                {
                    _isPanning = mouse.Pressed;
                    _lastPanMousePos = mouse.Position;
                    return;
                }
                if (mouse.Pressed) PaintAtMouse();
                return;
            }

            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed && _drawMode)
            {
                ClearOverrideAtMouse();
            }
        }

        private void HandleMouseMotion(InputEventMouseMotion motion)
        {
            UpdateCoordsHud(motion.Position);

            if (_isPanning)
            {
                _worldRoot.Position += motion.Position - _lastPanMousePos;
                _lastPanMousePos = motion.Position;
                RegenerateIfViewportChanged();
            }
        }

        private void HandleZoom(bool zoomIn)
        {
            float newZoom = Mathf.Clamp(_zoom * (zoomIn ? ZoomStep : 1f / ZoomStep), MinZoom, MaxZoom);
            if (Mathf.IsEqualApprox(newZoom, _zoom)) return;

            // Keep the world point currently under the cursor fixed on screen while zooming,
            // rather than zooming toward the viewport corner -- standard "zoom to cursor" feel.
            Vector2 screenMousePos = GetViewport().GetMousePosition();
            Vector2 worldPixelUnderMouse = _overlay.GetLocalMousePosition();

            _zoom = newZoom;
            _worldRoot.Scale = Vector2.One * _zoom;
            _worldRoot.Position = screenMousePos - worldPixelUnderMouse * _zoom;

            UpdateCoordsHud(screenMousePos);
            RegenerateIfViewportChanged();
        }

        /// <summary>Resets zoom to 1x and pans so the designated area is centered in the map viewport -- the "return to map area" escape hatch.</summary>
        private void CenterOnDesignatedArea()
        {
            _zoom = 1.0f;
            _worldRoot.Scale = Vector2.One;

            Vector2 mapViewportSize = GetMapViewportSize();
            var designatedCenterWorldPixel = new Vector2(
                (_originX + _regionWidth / 2f) * CellPixelSize,
                (_originY + _regionHeight / 2f) * CellPixelSize);

            _worldRoot.Position = mapViewportSize / 2f - designatedCenterWorldPixel;
            Regenerate();
        }

        private Vector2 GetMapViewportSize()
        {
            Vector2 windowSize = GetViewport().GetVisibleRect().Size;
            return new Vector2(MathF.Max(1f, windowSize.X - SidePanelWidth), MathF.Max(1f, windowSize.Y));
        }

        private void UpdateCoordsHud(Vector2 screenPos)
        {
            // Coordinates over the side panel aren't meaningful map positions; the panel's own
            // controls claim their input before it reaches _UnhandledInput, so in practice this
            // only ever runs for positions actually over the map, but the guard keeps the label
            // from showing a misleading in-panel position if that ever isn't true.
            if (screenPos.X > GetMapViewportSize().X)
            {
                return;
            }

            Vector2 worldPixel = (screenPos - _worldRoot.Position) / _zoom;
            int wx = Mathf.FloorToInt(worldPixel.X / CellPixelSize);
            int wy = Mathf.FloorToInt(worldPixel.Y / CellPixelSize);
            _coordsLabel.Text = $"Mouse: ({wx}, {wy})   Zoom: {_zoom * 100f:0}%";
        }

        // ---------- State changes ----------

        private void RefreshLayerList()
        {
            _layerList.Clear();
            foreach (var layer in _definition.Layers)
            {
                _layerList.AddItem(layer.Id);
            }
        }

        private void SelectLayer(int index)
        {
            if (index < 0 || index >= _definition.Layers.Count) return;

            _selectedLayerIndex = index;
            _layerList.Select(index);

            var layer = _definition.Layers[index];
            _suppressSignals = true;
            _seedXBox.Value = layer.Seed.X;
            _seedYBox.Value = layer.Seed.Y;
            _seedTBox.Value = layer.Seed.T;
            _suppressSignals = false;

            RebuildTileRows(layer);
            UpdateOverlayView();
            Regenerate();
        }

        private void RebuildTileRows(LayerDef layer)
        {
            foreach (Node child in _tilesContainer.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var tile in layer.Tiles)
            {
                var row = new HBoxContainer();

                var colorButton = new ColorPickerButton { Color = GetTileColor(tile.Id), CustomMinimumSize = new Vector2(28, 0) };
                colorButton.ColorChanged += c =>
                {
                    _tileColors[tile.Id] = c;
                    _overlay.SetTileColors(_tileColors);
                };
                row.AddChild(colorButton);

                row.AddChild(new Label { Text = tile.Id, CustomMinimumSize = new Vector2(70, 0) });

                var minus = new Button { Text = "-" };
                // Fixed width for the same reason as the Transformation field above: predictable
                // total row size inside a ScrollContainer, regardless of expand-fill clipping.
                var spin = new SpinBox { Step = 0.1, MinValue = 0, MaxValue = 1000, CustomMinimumSize = new Vector2(70, 0) };
                spin.Value = tile.Range;
                var plus = new Button { Text = "+" };

                // The tile itself is the single source of truth; nudging just moves the SpinBox's
                // Value, which fires this same handler -- no separate "apply" step.
                spin.ValueChanged += v =>
                {
                    tile.Range = v;
                    Regenerate();
                };
                minus.Pressed += () => spin.Value = Math.Max(0, spin.Value - 0.1);
                plus.Pressed += () => spin.Value = spin.Value + 0.1;

                row.AddChild(minus);
                row.AddChild(spin);
                row.AddChild(plus);

                var remove = new Button { Text = "x", Disabled = layer.Tiles.Count <= 1 };
                remove.TooltipText = layer.Tiles.Count <= 1
                    ? "A layer needs at least one tile"
                    : $"Remove '{tile.Id}' from this layer";
                remove.Pressed += () => OnRemoveTile(layer, tile);
                row.AddChild(remove);

                _tilesContainer.AddChild(row);
            }
        }

        private void OnAddTilePressed()
        {
            string id = _newTileIdEdit.Text.Trim();
            var layer = _definition.Layers[_selectedLayerIndex];

            if (string.IsNullOrEmpty(id))
            {
                _addTileHintLabel.Text = "Enter a tile id first.";
                return;
            }
            if (layer.Tiles.Exists(t => t.Id == id))
            {
                _addTileHintLabel.Text = $"Layer '{layer.Id}' already has a tile called '{id}'.";
                return;
            }

            layer.Tiles.Add(new TileDef(id, 1.0));
            GetTileColor(id); // assigns this new id a default color if it doesn't have one yet
            _newTileIdEdit.Text = "";
            _addTileHintLabel.Text = "";

            RebuildTileRows(layer);
            _overlay.SetTileColors(_tileColors);
            Regenerate();
        }

        private void OnRemoveTile(LayerDef layer, TileDef tile)
        {
            if (layer.Tiles.Count <= 1) return; // CompiledLayer requires at least one tile
            layer.Tiles.Remove(tile);
            RebuildTileRows(layer);
            Regenerate();
        }

        private Color GetTileColor(string tileId)
        {
            if (!_tileColors.TryGetValue(tileId, out var color))
            {
                color = NextDefaultColor();
                _tileColors[tileId] = color;
            }
            return color;
        }

        /// <summary>Spreads hues around the color wheel (golden-ratio step) so tiles added one after another get visually distinct default colors.</summary>
        private Color NextDefaultColor()
        {
            float hue = (_tileColors.Count * 0.618034f) % 1.0f;
            return Color.FromHsv(hue, 0.55f, 0.85f);
        }

        private void UpdateOverlayView()
        {
            var layer = _definition.Layers[_selectedLayerIndex];
            _overlay.ShowLayer(_showFinalComposite ? null : layer.Id);
        }

        private void OnSeedChanged()
        {
            var layer = _definition.Layers[_selectedLayerIndex];
            layer.Seed = new SeedPosition(_seedXBox.Value, _seedYBox.Value, _seedTBox.Value);
            Regenerate();
        }

        private void OnTransformationBoxChanged(double newValue)
        {
            if (_suppressSignals) return;
            ApplyTransformation(TransformationAxis.ClampStep(_transformation, newValue));
        }

        private void StepTransformation(double delta)
        {
            double requested = _transformation + delta;
            ApplyTransformation(TransformationAxis.ClampStep(_transformation, requested));
        }

        private void ApplyTransformation(double value)
        {
            _transformation = value;
            _suppressSignals = true;
            _transformationBox.Value = value;
            _suppressSignals = false;
            Regenerate();
        }

        private void PaintAtMouse()
        {
            var layer = _definition.Layers[_selectedLayerIndex];
            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;

            int nextIndex = 0;
            if (_overrides.TryGet(layer.Id, cell.Value.X, cell.Value.Y, out var current))
            {
                nextIndex = layer.Tiles.FindIndex(t => t.Id == current) + 1;
            }

            if (nextIndex >= layer.Tiles.Count)
            {
                _overrides.Clear(layer.Id, cell.Value.X, cell.Value.Y); // cycled past the last tile: back to procedural
            }
            else
            {
                _overrides.Set(layer.Id, cell.Value.X, cell.Value.Y, layer.Tiles[nextIndex].Id);
            }
            Regenerate();
        }

        private void ClearOverrideAtMouse()
        {
            var layer = _definition.Layers[_selectedLayerIndex];
            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;

            _overrides.Clear(layer.Id, cell.Value.X, cell.Value.Y);
            Regenerate();
        }

        /// <summary>Regenerates the currently visible viewport. The workhorse for every discrete, user-initiated change (tile/seed/region edits, layer switches, zoom, painting).</summary>
        private void Regenerate() => RegenerateWithViewport(ComputeViewportRegion());

        /// <summary>
        /// Same as <see cref="Regenerate"/> but skips the (comparatively expensive) engine call
        /// entirely if the computed viewport is identical to the last one rendered. Used on the
        /// mouse-drag pan path, which can fire many motion events per second -- most of which
        /// move the mouse without the *cell-quantized* viewport actually changing.
        /// </summary>
        private void RegenerateIfViewportChanged()
        {
            var viewport = ComputeViewportRegion();
            if (_lastViewport.HasValue && RegionsEqual(_lastViewport.Value, viewport)) return;
            RegenerateWithViewport(viewport);
        }

        private void RegenerateWithViewport(RegionSpec viewport)
        {
            _lastViewport = viewport;
            _overlay.DesignatedArea = new Rect2I(_originX, _originY, _regionWidth, _regionHeight);
            try
            {
                var result = MapGenerator.GenerateRegion(_definition, viewport, _overrides);
                _overlay.Render(result, viewport, _overrides);
                UpdateStatus();
            }
            catch (ArgumentException ex)
            {
                // Reachable now that tile ranges/removal are editable, e.g. every tile on a layer
                // nudged to a range of 0 leaves nothing to select from. Report it instead of
                // crashing; the last successful render stays on screen.
                _statusLabel.Text = $"Cannot generate: {ex.Message}";
            }
        }

        /// <summary>Converts the camera's current pan/zoom into the world-cell rectangle actually visible in the map viewport, padded slightly and capped for safety.</summary>
        private RegionSpec ComputeViewportRegion()
        {
            Vector2 mapViewportSize = GetMapViewportSize();

            Vector2 topLeftWorldPixel = -_worldRoot.Position / _zoom;
            Vector2 bottomRightWorldPixel = (mapViewportSize - _worldRoot.Position) / _zoom;

            int originX = Mathf.FloorToInt(topLeftWorldPixel.X / CellPixelSize) - ViewportMargin;
            int originY = Mathf.FloorToInt(topLeftWorldPixel.Y / CellPixelSize) - ViewportMargin;
            int endX = Mathf.CeilToInt(bottomRightWorldPixel.X / CellPixelSize) + ViewportMargin;
            int endY = Mathf.CeilToInt(bottomRightWorldPixel.Y / CellPixelSize) + ViewportMargin;

            int width = Math.Clamp(endX - originX, 1, MaxViewportCells);
            int height = Math.Clamp(endY - originY, 1, MaxViewportCells);

            return new RegionSpec(originX, originY, width, height, _transformation);
        }

        private static bool RegionsEqual(RegionSpec a, RegionSpec b) =>
            a.OriginX == b.OriginX && a.OriginY == b.OriginY &&
            a.Width == b.Width && a.Height == b.Height &&
            Math.Abs(a.Transformation - b.Transformation) < 1e-9;

        private void UpdateStatus()
        {
            var layer = _definition.Layers[_selectedLayerIndex];
            string view = _showFinalComposite ? "final composite" : layer.Id;
            _statusLabel.Text =
                $"Editing layer: {layer.Id}\n" +
                $"Viewing: {view}\n" +
                $"Designated area: ({_originX}, {_originY}) {_regionWidth}x{_regionHeight}\n" +
                $"Transformation: {_transformation:0.00}\n" +
                $"Overrides: {_overrides.Count}\n\n" +
                (_drawMode
                    ? "Left-click map: cycle override on the selected layer\nRight-click map: clear override"
                    : "Drag map: pan the camera") +
                "\nMouse wheel: zoom";
        }

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
