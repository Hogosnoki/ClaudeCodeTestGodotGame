#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Generation;
using ProcGen.Engine.Model;
using ProcGen.Engine.Movement;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Serialization;
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
    /// A project file *is* a game: <see cref="_allMaps"/> holds every map belonging to it, and
    /// Save/Load act on all of them at once via <see cref="ProjectFileSerializer"/> -- the same
    /// reader/writer the game itself must use, so a saved file reproduces identically wherever
    /// it's loaded. There is deliberately no way to load a single map out of a different project
    /// file; an exit's destination is always resolved against maps already in memory. The "Maps
    /// in this project" list switches which map the rest of the panel edits (New/Duplicate/Delete
    /// included); <see cref="_definition"/>/<see cref="_overrides"/> below are computed
    /// properties, not fields, so every existing read site keeps working unchanged regardless of
    /// which map is currently selected.
    ///
    /// An "Edit: Map / Entrance-Exit / Movement / Project Overview" dropdown swaps the panel
    /// between the settings above, a panel for adding/positioning this map's named entrance and
    /// exit points (so those controls aren't cluttering the view all the time), a panel for
    /// editing directional tile-transition (movement-blocking) rules, and a project-wide summary
    /// that flags exits whose destination doesn't resolve to a real map/entrance. Entrance/exit
    /// points render as colored markers on the map while that panel is active; a "Place" button
    /// per point arms it so the next map click sets its position. There is no separate per-tile
    /// "walkable" flag anywhere in the tool -- the Movement panel's "Solid"/"Open" buttons are a
    /// convenience that bulk-add/remove ordinary <see cref="ProcGen.Engine.Model.TileTransitionRule"/>s
    /// via <see cref="ProcGen.Engine.Movement.TraversalEditing"/>, so that's the only mechanism.
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

        // Every map in the project, in project order; _currentMapIndex says which one the rest of
        // the panel is editing. _definition/_overrides below are computed, not stored -- every
        // existing read site (`_definition.Layers`, `_overrides.TryGet(...)`, etc.) keeps working
        // unchanged, since a property reads exactly like a field at the call site. The only places
        // that ever need to *reassign* which map is active go through _currentMapIndex instead.
        private List<(MapDefinition Definition, OverrideStore Overrides)> _allMaps = null!;
        private int _currentMapIndex;
        private MapDefinition _definition => _allMaps[_currentMapIndex].Definition;
        private OverrideStore _overrides => _allMaps[_currentMapIndex].Overrides;
        private ProcGenDebugOverlay _overlay = null!;
        private EntranceExitOverlay _markerOverlay = null!;
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
        private SpinBox _octavesBox = null!;
        private SpinBox _frequencyBox = null!;
        private SpinBox _persistenceBox = null!;
        private SpinBox _lacunarityBox = null!;
        private SpinBox _transformationBox = null!;
        private SpinBox _originXBox = null!;
        private SpinBox _originYBox = null!;
        private SpinBox _widthBox = null!;
        private SpinBox _heightBox = null!;
        private VBoxContainer _tilesContainer = null!;
        private LineEdit _newTileIdEdit = null!;
        private Label _addTileHintLabel = null!;
        private Label _statusLabel = null!;
        private Label _coordsLabel = null!;
        private CheckBox _compositeToggle = null!;
        private CheckBox _drawModeToggle = null!;

        // Save/load, the maps-in-project list, and the Map / Entrance-Exit / Project Overview
        // panel switch. A project file *is* a game (see class doc comment) -- Save/Load act on
        // every map in _allMaps at once, not just the currently selected one.
        private const string MapsDirectory = "res://Maps";
        private LineEdit _mapIdEdit = null!;
        private LineEdit _saveFileNameEdit = null!;
        private Label _saveLoadStatusLabel = null!;
        private ItemList _mapList = null!;
        private Label _mapListHintLabel = null!;
        private OptionButton _modeDropdown = null!;
        private int _panelMode; // 0 = Map, 1 = Entrance/Exit, 2 = Movement, 3 = Project Overview
        private VBoxContainer _mapModePanel = null!;
        private VBoxContainer _entranceExitModePanel = null!;
        private VBoxContainer _entrancesContainer = null!;
        private LineEdit _newEntranceIdEdit = null!;
        private Label _entranceHintLabel = null!;
        private VBoxContainer _exitsContainer = null!;
        private LineEdit _newExitIdEdit = null!;
        private Label _exitHintLabel = null!;
        private VBoxContainer _movementModePanel = null!;
        private VBoxContainer _solidTileRowsContainer = null!;
        private VBoxContainer _transitionRulesContainer = null!;
        private OptionButton _newRuleFromDropdown = null!;
        private OptionButton _newRuleToDropdown = null!;
        private Label _movementHintLabel = null!;
        private VBoxContainer _projectOverviewPanel = null!;
        private VBoxContainer _projectOverviewMapsContainer = null!;
        private VBoxContainer _danglingExitsContainer = null!;

        // Click-to-place: "arming" an entrance/exit for placement makes the next map click set
        // its position, instead of painting or panning. At most one of these is non-null.
        private EntrancePoint? _armedEntrance;
        private ExitPoint? _armedExit;

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

            _allMaps = new List<(MapDefinition, OverrideStore)> { (BuildDefinition(), new OverrideStore()) };
            _currentMapIndex = 0;

            BuildUi();
            _overlay.SetTileColors(_tileColors);

            RefreshMapList();
            RefreshLayerList();
            RebuildEntranceRows();
            RebuildExitRows();
            RefreshMovementPanel();
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
            _markerOverlay = new EntranceExitOverlay { CellPixelSize = CellPixelSize, Visible = false };
            _worldRoot.AddChild(_markerOverlay);

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

            root.AddChild(Header("Project"));
            _saveFileNameEdit = new LineEdit { PlaceholderText = "project file name, e.g. mygame.json" };
            root.AddChild(_saveFileNameEdit);

            var saveLoadRow = new HBoxContainer();
            var saveButton = new Button { Text = "Save" };
            saveButton.Pressed += OnSavePressed;
            saveLoadRow.AddChild(saveButton);
            var loadButton = new Button { Text = "Load" };
            loadButton.Pressed += OnLoadPressed;
            saveLoadRow.AddChild(loadButton);
            root.AddChild(saveLoadRow);

            _saveLoadStatusLabel = new Label
            {
                Text = $"Reads/writes every map at once from/to {MapsDirectory}/<file name> -- a project file is one whole game. There is no way to load a single map from a different project file.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            root.AddChild(_saveLoadStatusLabel);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Maps in this project"));
            _mapList = new ItemList { CustomMinimumSize = new Vector2(0, 90) };
            _mapList.ItemSelected += index => SelectMap((int)index);
            root.AddChild(_mapList);

            var mapActionsRow = new HBoxContainer();
            var newMapButton = new Button { Text = "New" };
            newMapButton.Pressed += OnNewMapPressed;
            mapActionsRow.AddChild(newMapButton);
            var duplicateMapButton = new Button { Text = "Duplicate" };
            duplicateMapButton.Pressed += OnDuplicateMapPressed;
            mapActionsRow.AddChild(duplicateMapButton);
            var deleteMapButton = new Button { Text = "Delete" };
            deleteMapButton.Pressed += OnDeleteMapPressed;
            mapActionsRow.AddChild(deleteMapButton);
            root.AddChild(mapActionsRow);

            _mapListHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_mapListHintLabel);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Map"));
            _mapIdEdit = new LineEdit { PlaceholderText = "map id (how exits refer to this map)", Text = _definition.MapId };
            _mapIdEdit.TextChanged += t => { _definition.MapId = t; RefreshMapList(); };
            root.AddChild(_mapIdEdit);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Edit"));
            _modeDropdown = new OptionButton();
            _modeDropdown.AddItem("Map");
            _modeDropdown.AddItem("Entrance / Exit");
            _modeDropdown.AddItem("Movement");
            _modeDropdown.AddItem("Project Overview");
            _modeDropdown.ItemSelected += index => SetPanelMode((int)index);
            root.AddChild(_modeDropdown);
            root.AddChild(new HSeparator());

            _mapModePanel = new VBoxContainer();
            _mapModePanel.AddThemeConstantOverride("separation", 6);
            root.AddChild(_mapModePanel);
            BuildMapModePanel(_mapModePanel);

            _entranceExitModePanel = new VBoxContainer { Visible = false };
            _entranceExitModePanel.AddThemeConstantOverride("separation", 6);
            root.AddChild(_entranceExitModePanel);
            BuildEntranceExitModePanel(_entranceExitModePanel);

            _movementModePanel = new VBoxContainer { Visible = false };
            _movementModePanel.AddThemeConstantOverride("separation", 6);
            root.AddChild(_movementModePanel);
            BuildMovementModePanel(_movementModePanel);

            _projectOverviewPanel = new VBoxContainer { Visible = false };
            _projectOverviewPanel.AddThemeConstantOverride("separation", 6);
            root.AddChild(_projectOverviewPanel);
            BuildProjectOverviewPanel(_projectOverviewPanel);
        }

        private void SetPanelMode(int mode)
        {
            _panelMode = mode;
            _mapModePanel.Visible = mode == 0;
            _entranceExitModePanel.Visible = mode == 1;
            _movementModePanel.Visible = mode == 2;
            _projectOverviewPanel.Visible = mode == 3;
            _armedEntrance = null;
            _armedExit = null;
            if (mode == 3) RefreshProjectOverview();
            RefreshMarkerOverlay();
        }

        private void BuildMapModePanel(VBoxContainer root)
        {
            root.AddChild(Header("Region (designated map area)"));
            _originXBox = AddIntField(root, "Origin X", _originX, -100000, 100000, v => { _originX = v; Regenerate(); });
            _originYBox = AddIntField(root, "Origin Y", _originY, -100000, 100000, v => { _originY = v; Regenerate(); });
            _widthBox = AddIntField(root, "Width", _regionWidth, 1, 200, v => { _regionWidth = v; Regenerate(); });
            _heightBox = AddIntField(root, "Height", _regionHeight, 1, 200, v => { _regionHeight = v; Regenerate(); });
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

            root.AddChild(Header("Noise (selected layer)"));
            _octavesBox = AddIntField(root, "Octaves", 1, 1, 8, v => { CurrentLayer().Noise.Octaves = v; Regenerate(); });
            _frequencyBox = AddDoubleField(root, "Frequency", 0.001, 10, OnNoiseChanged);
            _persistenceBox = AddDoubleField(root, "Persistence", 0, 1, OnNoiseChanged);
            _lacunarityBox = AddDoubleField(root, "Lacunarity", 0.1, 10, OnNoiseChanged);
            root.AddChild(new Label
            {
                Text = "Fewer octaves / lower frequency = smoother, more gradual terrain transitions (e.g. a wide sandy beach ring); more octaves = rougher, more detailed but steeper edges.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
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

        private void BuildEntranceExitModePanel(VBoxContainer root)
        {
            root.AddChild(Header("Entrances"));
            root.AddChild(new Label
            {
                Text = "Where a player arrives via some other exit (on this map or any other) targeting one of these ids. Ids only need to be unique on this map. Use the coordinate readout over the map to find where to place one, then type it in here.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _entrancesContainer = new VBoxContainer();
            root.AddChild(_entrancesContainer);

            var addEntranceRow = new HBoxContainer();
            _newEntranceIdEdit = new LineEdit { PlaceholderText = "new entrance id", CustomMinimumSize = new Vector2(200, 0) };
            addEntranceRow.AddChild(_newEntranceIdEdit);
            var addEntranceButton = new Button { Text = "Add Entrance" };
            addEntranceButton.Pressed += OnAddEntrancePressed;
            addEntranceRow.AddChild(addEntranceButton);
            root.AddChild(addEntranceRow);

            _entranceHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_entranceHintLabel);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Exits"));
            root.AddChild(new Label
            {
                Text = "A point the player leaves through, toward a destination map and one of that map's entrances -- pick both from the dropdowns below. Check Project Overview for exits whose destination has since been deleted.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _exitsContainer = new VBoxContainer();
            root.AddChild(_exitsContainer);

            var addExitRow = new HBoxContainer();
            _newExitIdEdit = new LineEdit { PlaceholderText = "new exit id", CustomMinimumSize = new Vector2(200, 0) };
            addExitRow.AddChild(_newExitIdEdit);
            var addExitButton = new Button { Text = "Add Exit" };
            addExitButton.Pressed += OnAddExitPressed;
            addExitRow.AddChild(addExitButton);
            root.AddChild(addExitRow);

            _exitHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_exitHintLabel);
        }

        private void BuildMovementModePanel(VBoxContainer root)
        {
            root.AddChild(new Label
            {
                Text = "Whether a character standing on one tile can step onto an adjacent one, checked purely by the final tile id each cell resolves to -- never by layer. There's no separate 'walkable' flag on a tile; every rule below, hand-added or bulk-generated by 'Solid', lives in the same list and is equally editable.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Make a tile solid"));
            root.AddChild(new Label
            {
                Text = "'Solid' blocks every other known tile from moving onto this one -- but never blocks this tile from moving onto anything else, so a character can never get stuck standing on a tile made solid after the fact, and two adjacent cells of the same tile always stay walkable between each other. 'Open' clears all of this tile's incoming blocks.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _solidTileRowsContainer = new VBoxContainer();
            root.AddChild(_solidTileRowsContainer);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Transition rules"));
            root.AddChild(new Label
            {
                Text = "Every current rule, one per row: a character standing on 'From' cannot step onto an adjacent 'To'. Blocking is one-directional -- add the reverse rule too if movement should be blocked both ways.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _transitionRulesContainer = new VBoxContainer();
            root.AddChild(_transitionRulesContainer);

            var addRuleRow = new HBoxContainer();
            _newRuleFromDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            addRuleRow.AddChild(_newRuleFromDropdown);
            addRuleRow.AddChild(new Label { Text = "->" });
            _newRuleToDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            addRuleRow.AddChild(_newRuleToDropdown);
            root.AddChild(addRuleRow);

            var addRuleButton = new Button { Text = "Add Rule" };
            addRuleButton.Pressed += OnAddTransitionRulePressed;
            root.AddChild(addRuleButton);

            _movementHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_movementHintLabel);
        }

        private void BuildProjectOverviewPanel(VBoxContainer root)
        {
            root.AddChild(Header("Maps"));
            _projectOverviewMapsContainer = new VBoxContainer();
            root.AddChild(_projectOverviewMapsContainer);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Dangling exits"));
            root.AddChild(new Label
            {
                Text = "Exits whose destination map or entrance doesn't exist in this project. Not an error by itself -- a map under construction may reference a destination you haven't built yet -- but worth checking before you consider the project finished.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _danglingExitsContainer = new VBoxContainer();
            root.AddChild(_danglingExitsContainer);

            var refreshButton = new Button { Text = "Refresh" };
            refreshButton.Pressed += RefreshProjectOverview;
            root.AddChild(refreshButton);
        }

        private void RefreshProjectOverview()
        {
            foreach (Node child in _projectOverviewMapsContainer.GetChildren())
            {
                child.QueueFree();
            }
            for (int i = 0; i < _allMaps.Count; i++)
            {
                var def = _allMaps[i].Definition;
                string marker = i == _currentMapIndex ? "-> " : "    ";
                string label = string.IsNullOrEmpty(def.MapId) ? "(untitled map)" : def.MapId;
                _projectOverviewMapsContainer.AddChild(new Label
                {
                    Text = $"{marker}{label}   [{def.Layers.Count} layer(s), {def.Entrances.Count} entrance(s), {def.Exits.Count} exit(s)]",
                });
            }

            foreach (Node child in _danglingExitsContainer.GetChildren())
            {
                child.QueueFree();
            }
            var problems = ProjectValidation.FindDanglingExits(_allMaps.ConvertAll(m => m.Definition));
            if (problems.Count == 0)
            {
                _danglingExitsContainer.AddChild(new Label { Text = "None.", Modulate = new Color(1, 1, 1, 0.6f) });
            }
            else
            {
                foreach (var p in problems)
                {
                    string reason = p.Reason == DanglingExitReason.MapNotFound
                        ? $"destination map '{p.DestinationMapId}' doesn't exist"
                        : $"map '{p.DestinationMapId}' has no entrance '{p.DestinationEntranceId}'";
                    _danglingExitsContainer.AddChild(new Label
                    {
                        Text = $"{p.SourceMapId} / exit '{p.ExitId}': {reason}",
                        Modulate = new Color(1f, 0.6f, 0.5f, 1f),
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    });
                }
            }
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
                if (mouse.Pressed && (_armedEntrance != null || _armedExit != null))
                {
                    PlaceArmedMarkerAtMouse();
                    return;
                }
                // In every non-Map edit mode there's nothing to paint -- dragging always pans,
                // regardless of the Draw Mode toggle (which only makes sense for terrain).
                if (_panelMode != 0 || !_drawMode)
                {
                    _isPanning = mouse.Pressed;
                    _lastPanMousePos = mouse.Position;
                    return;
                }
                if (mouse.Pressed) PaintAtMouse();
                return;
            }

            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed && _drawMode && _panelMode == 0)
            {
                ClearOverrideAtMouse();
            }
        }

        private void PlaceArmedMarkerAtMouse()
        {
            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;

            if (_armedEntrance != null)
            {
                _armedEntrance.X = cell.Value.X;
                _armedEntrance.Y = cell.Value.Y;
                _armedEntrance = null;
                RebuildEntranceRows();
            }
            else if (_armedExit != null)
            {
                _armedExit.X = cell.Value.X;
                _armedExit.Y = cell.Value.Y;
                _armedExit = null;
                RebuildExitRows();
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
            _octavesBox.Value = layer.Noise.Octaves;
            _frequencyBox.Value = layer.Noise.Frequency;
            _persistenceBox.Value = layer.Noise.Persistence;
            _lacunarityBox.Value = layer.Noise.Lacunarity;
            _suppressSignals = false;

            RebuildTileRows(layer);
            UpdateOverlayView();
            Regenerate();
        }

        private LayerDef CurrentLayer() => _definition.Layers[_selectedLayerIndex];

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
            RefreshMovementPanel();
            Regenerate();
        }

        private void OnRemoveTile(LayerDef layer, TileDef tile)
        {
            if (layer.Tiles.Count <= 1) return; // CompiledLayer requires at least one tile
            layer.Tiles.Remove(tile);
            RebuildTileRows(layer);
            RefreshMovementPanel();
            Regenerate();
        }

        // ---------- Entrances / Exits ----------
        // Unlike tiles, entrance/exit points don't feed the generation algorithm at all -- they're
        // pure gameplay metadata carried alongside the map -- so editing them never calls
        // Regenerate(). They do refresh the marker overlay, which is a pure visualization aid.

        private void RebuildEntranceRows()
        {
            foreach (Node child in _entrancesContainer.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var entrance in _definition.Entrances)
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = entrance.Id, CustomMinimumSize = new Vector2(80, 0) });

                var xBox = new SpinBox { Step = 1, MinValue = -1000000, MaxValue = 1000000, Rounded = true, CustomMinimumSize = new Vector2(65, 0) };
                xBox.Value = entrance.X;
                xBox.ValueChanged += v => { entrance.X = (int)Math.Round(v); RefreshMarkerOverlay(); };
                row.AddChild(xBox);

                var yBox = new SpinBox { Step = 1, MinValue = -1000000, MaxValue = 1000000, Rounded = true, CustomMinimumSize = new Vector2(65, 0) };
                yBox.Value = entrance.Y;
                yBox.ValueChanged += v => { entrance.Y = (int)Math.Round(v); RefreshMarkerOverlay(); };
                row.AddChild(yBox);

                var place = new Button { Text = "Place", TooltipText = "Click, then click the map to set this entrance's position" };
                place.Pressed += () =>
                {
                    _armedEntrance = entrance;
                    _armedExit = null;
                    RefreshMarkerOverlay();
                };
                row.AddChild(place);

                var remove = new Button { Text = "x", TooltipText = $"Remove entrance '{entrance.Id}'" };
                remove.Pressed += () =>
                {
                    if (_armedEntrance == entrance) _armedEntrance = null;
                    _definition.Entrances.Remove(entrance);
                    RebuildEntranceRows();
                };
                row.AddChild(remove);

                _entrancesContainer.AddChild(row);
            }
            RefreshMarkerOverlay();
        }

        private void OnAddEntrancePressed()
        {
            string id = _newEntranceIdEdit.Text.Trim();
            if (string.IsNullOrEmpty(id))
            {
                _entranceHintLabel.Text = "Enter an entrance id first.";
                return;
            }
            if (_definition.Entrances.Exists(e => e.Id == id))
            {
                _entranceHintLabel.Text = $"This map already has an entrance called '{id}'.";
                return;
            }

            _definition.Entrances.Add(new EntrancePoint(id, 0, 0));
            _newEntranceIdEdit.Text = "";
            _entranceHintLabel.Text = "";
            RebuildEntranceRows();
        }

        private void RebuildExitRows()
        {
            foreach (Node child in _exitsContainer.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var exit in _definition.Exits)
            {
                var card = new VBoxContainer();
                card.AddThemeConstantOverride("separation", 2);

                var headerRow = new HBoxContainer();
                headerRow.AddChild(new Label { Text = exit.Id, CustomMinimumSize = new Vector2(110, 0) });
                var place = new Button { Text = "Place", TooltipText = "Click, then click the map to set this exit's position" };
                place.Pressed += () =>
                {
                    _armedExit = exit;
                    _armedEntrance = null;
                    RefreshMarkerOverlay();
                };
                headerRow.AddChild(place);
                var remove = new Button { Text = "x", TooltipText = $"Remove exit '{exit.Id}'" };
                remove.Pressed += () =>
                {
                    if (_armedExit == exit) _armedExit = null;
                    _definition.Exits.Remove(exit);
                    RebuildExitRows();
                };
                headerRow.AddChild(remove);
                card.AddChild(headerRow);

                var posRow = new HBoxContainer();
                posRow.AddChild(new Label { Text = "X", CustomMinimumSize = new Vector2(20, 0) });
                var xBox = new SpinBox { Step = 1, MinValue = -1000000, MaxValue = 1000000, Rounded = true, CustomMinimumSize = new Vector2(70, 0) };
                xBox.Value = exit.X;
                xBox.ValueChanged += v => { exit.X = (int)Math.Round(v); RefreshMarkerOverlay(); };
                posRow.AddChild(xBox);
                posRow.AddChild(new Label { Text = "Y", CustomMinimumSize = new Vector2(20, 0) });
                var yBox = new SpinBox { Step = 1, MinValue = -1000000, MaxValue = 1000000, Rounded = true, CustomMinimumSize = new Vector2(70, 0) };
                yBox.Value = exit.Y;
                yBox.ValueChanged += v => { exit.Y = (int)Math.Round(v); RefreshMarkerOverlay(); };
                posRow.AddChild(yBox);
                card.AddChild(posRow);

                card.AddChild(new Label { Text = "Destination map", Modulate = new Color(1, 1, 1, 0.6f) });
                var destMapDropdown = new OptionButton();
                var destEntranceDropdown = new OptionButton();
                PopulateDestinationMapDropdown(destMapDropdown, exit.DestinationMapId);
                PopulateDestinationEntranceDropdown(destEntranceDropdown, exit.DestinationMapId, exit.DestinationEntranceId);
                destMapDropdown.ItemSelected += idx =>
                {
                    string selectedMapId = destMapDropdown.GetItemMetadata((int)idx).AsString();
                    exit.DestinationMapId = selectedMapId;
                    exit.DestinationEntranceId = "";
                    PopulateDestinationEntranceDropdown(destEntranceDropdown, selectedMapId, "");
                };
                card.AddChild(destMapDropdown);

                card.AddChild(new Label { Text = "Destination entrance", Modulate = new Color(1, 1, 1, 0.6f) });
                destEntranceDropdown.ItemSelected += idx =>
                {
                    exit.DestinationEntranceId = destEntranceDropdown.GetItemMetadata((int)idx).AsString();
                };
                card.AddChild(destEntranceDropdown);

                card.AddChild(new HSeparator());
                _exitsContainer.AddChild(card);
            }
            RefreshMarkerOverlay();
        }

        /// <summary>Fills a dropdown with every map in the project (plus a "not chosen" placeholder), selecting whichever matches currentValue -- or the placeholder, if currentValue is empty or names a map no longer in the project, without overwriting it.</summary>
        private void PopulateDestinationMapDropdown(OptionButton dropdown, string currentValue)
        {
            dropdown.Clear();
            dropdown.AddItem("(choose a map)");
            dropdown.SetItemMetadata(0, "");
            int selectIndex = 0;

            for (int i = 0; i < _allMaps.Count; i++)
            {
                string id = _allMaps[i].Definition.MapId;
                dropdown.AddItem(string.IsNullOrEmpty(id) ? "(untitled map)" : id);
                dropdown.SetItemMetadata(i + 1, id);
                if (!string.IsNullOrEmpty(currentValue) && id == currentValue) selectIndex = i + 1;
            }
            dropdown.Select(selectIndex);
        }

        private void PopulateDestinationEntranceDropdown(OptionButton dropdown, string destinationMapId, string currentValue)
        {
            dropdown.Clear();
            dropdown.AddItem("(choose an entrance)");
            dropdown.SetItemMetadata(0, "");
            int selectIndex = 0;

            var destMap = _allMaps.Find(m => m.Definition.MapId == destinationMapId).Definition;
            if (destMap != null)
            {
                for (int i = 0; i < destMap.Entrances.Count; i++)
                {
                    string id = destMap.Entrances[i].Id;
                    dropdown.AddItem(id);
                    dropdown.SetItemMetadata(i + 1, id);
                    if (!string.IsNullOrEmpty(currentValue) && id == currentValue) selectIndex = i + 1;
                }
            }
            dropdown.Select(selectIndex);
        }

        private void OnAddExitPressed()
        {
            string id = _newExitIdEdit.Text.Trim();
            if (string.IsNullOrEmpty(id))
            {
                _exitHintLabel.Text = "Enter an exit id first.";
                return;
            }
            if (_definition.Exits.Exists(e => e.Id == id))
            {
                _exitHintLabel.Text = $"This map already has an exit called '{id}'.";
                return;
            }

            _definition.Exits.Add(new ExitPoint(id, 0, 0, "", ""));
            _newExitIdEdit.Text = "";
            _exitHintLabel.Text = "";
            RebuildExitRows();
        }

        /// <summary>Every distinct tile id used by any layer of the current map, in first-seen order -- the universe "Solid" bulk-blocks against and the dropdown options for hand-added rules.</summary>
        private List<string> GetAllTileIds()
        {
            var seen = new HashSet<string>();
            var ids = new List<string>();
            foreach (var layer in _definition.Layers)
            {
                foreach (var tile in layer.Tiles)
                {
                    if (seen.Add(tile.Id)) ids.Add(tile.Id);
                }
            }
            return ids;
        }

        private void RefreshMovementPanel()
        {
            var tileIds = GetAllTileIds();

            foreach (Node child in _solidTileRowsContainer.GetChildren())
            {
                child.QueueFree();
            }
            foreach (var tileId in tileIds)
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = tileId, CustomMinimumSize = new Vector2(120, 0) });
                var solid = new Button { Text = "Solid", TooltipText = $"Block every other tile from moving onto '{tileId}'" };
                solid.Pressed += () =>
                {
                    TraversalEditing.MakeSolid(_definition.BlockedTransitions, tileId, GetAllTileIds());
                    RefreshMovementPanel();
                };
                row.AddChild(solid);
                var open = new Button { Text = "Open", TooltipText = $"Clear every rule blocking movement onto '{tileId}'" };
                open.Pressed += () =>
                {
                    TraversalEditing.ClearBlocksInto(_definition.BlockedTransitions, tileId);
                    RefreshMovementPanel();
                };
                row.AddChild(open);
                _solidTileRowsContainer.AddChild(row);
            }

            foreach (Node child in _transitionRulesContainer.GetChildren())
            {
                child.QueueFree();
            }
            for (int i = 0; i < _definition.BlockedTransitions.Count; i++)
            {
                int index = i;
                var rule = _definition.BlockedTransitions[i];
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = $"{rule.FromTileId} -> {rule.ToTileId}", SizeFlagsHorizontal = SizeFlags.ExpandFill });
                var remove = new Button { Text = "x", TooltipText = "Remove this rule" };
                remove.Pressed += () =>
                {
                    _definition.BlockedTransitions.RemoveAt(index);
                    RefreshMovementPanel();
                };
                row.AddChild(remove);
                _transitionRulesContainer.AddChild(row);
            }

            PopulateTileDropdown(_newRuleFromDropdown, tileIds, "");
            PopulateTileDropdown(_newRuleToDropdown, tileIds, "");
        }

        private static void PopulateTileDropdown(OptionButton dropdown, List<string> tileIds, string currentValue)
        {
            dropdown.Clear();
            dropdown.AddItem("(choose a tile)");
            dropdown.SetItemMetadata(0, "");
            int selectIndex = 0;

            for (int i = 0; i < tileIds.Count; i++)
            {
                dropdown.AddItem(tileIds[i]);
                dropdown.SetItemMetadata(i + 1, tileIds[i]);
                if (!string.IsNullOrEmpty(currentValue) && tileIds[i] == currentValue) selectIndex = i + 1;
            }
            dropdown.Select(selectIndex);
        }

        private void OnAddTransitionRulePressed()
        {
            string from = _newRuleFromDropdown.GetItemMetadata(_newRuleFromDropdown.Selected).AsString();
            string to = _newRuleToDropdown.GetItemMetadata(_newRuleToDropdown.Selected).AsString();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                _movementHintLabel.Text = "Choose both a From and a To tile first.";
                return;
            }
            if (from == to)
            {
                _movementHintLabel.Text = "A tile can't block movement into itself -- same-type movement always stays open.";
                return;
            }
            if (_definition.BlockedTransitions.Exists(r => r.FromTileId == from && r.ToTileId == to))
            {
                _movementHintLabel.Text = "This rule already exists.";
                return;
            }

            _definition.BlockedTransitions.Add(new TileTransitionRule(from, to));
            _movementHintLabel.Text = "";
            RefreshMovementPanel();
        }

        private void RefreshMarkerOverlay()
        {
            var entrances = _definition.Entrances.ConvertAll(e => (e.Id, e.X, e.Y));
            var exits = _definition.Exits.ConvertAll(e => (e.Id, e.X, e.Y));
            (bool IsEntrance, string Id)? armed = null;
            if (_armedEntrance != null) armed = (true, _armedEntrance.Id);
            else if (_armedExit != null) armed = (false, _armedExit.Id);

            _markerOverlay.Visible = _panelMode == 1;
            _markerOverlay.SetPoints(entrances, exits, armed);
        }

        // ---------- Maps in this project ----------

        private void RefreshMapList()
        {
            _mapList.Clear();
            for (int i = 0; i < _allMaps.Count; i++)
            {
                string id = _allMaps[i].Definition.MapId;
                _mapList.AddItem(string.IsNullOrEmpty(id) ? "(untitled map)" : id);
            }
            if (_currentMapIndex < _mapList.ItemCount)
            {
                _mapList.Select(_currentMapIndex);
            }
        }

        private void SelectMap(int index)
        {
            if (index < 0 || index >= _allMaps.Count || index == _currentMapIndex) return;
            _currentMapIndex = index;
            ActivateCurrentMap();
        }

        /// <summary>Refreshes every panel section to reflect whichever map _currentMapIndex now points at -- called after switching, creating, duplicating, or deleting a map.</summary>
        private void ActivateCurrentMap()
        {
            _selectedLayerIndex = 0;
            _armedEntrance = null;
            _armedExit = null;

            _suppressSignals = true;
            _mapIdEdit.Text = _definition.MapId;
            _suppressSignals = false;

            ResetRegionAndTransformFields();
            RefreshLayerList();
            RebuildEntranceRows();
            RebuildExitRows();
            RefreshMovementPanel();
            SelectLayer(0);
            CenterOnDesignatedArea();
            RefreshMapList();
        }

        private void OnNewMapPressed()
        {
            var newDef = BuildDefinition();
            newDef.MapId = GenerateUniqueMapId("new_map");
            _allMaps.Add((newDef, new OverrideStore()));
            _currentMapIndex = _allMaps.Count - 1;
            ActivateCurrentMap();
            _mapListHintLabel.Text = "";
        }

        private void OnDuplicateMapPressed()
        {
            var clone = CloneMapDefinition(_definition, GenerateUniqueMapId(_definition.MapId));
            var clonedOverrides = OverrideStore.FromRecords(new List<TileOverride>(_overrides.Enumerate()));
            _allMaps.Add((clone, clonedOverrides));
            _currentMapIndex = _allMaps.Count - 1;
            ActivateCurrentMap();
            _mapListHintLabel.Text = "";
        }

        private void OnDeleteMapPressed()
        {
            if (_allMaps.Count <= 1)
            {
                _mapListHintLabel.Text = "A project needs at least one map.";
                return;
            }
            _allMaps.RemoveAt(_currentMapIndex);
            _currentMapIndex = Math.Min(_currentMapIndex, _allMaps.Count - 1);
            ActivateCurrentMap();
            _mapListHintLabel.Text = "";
        }

        private string GenerateUniqueMapId(string baseId)
        {
            var existing = new HashSet<string>();
            foreach (var (definition, _) in _allMaps) existing.Add(definition.MapId);

            if (!existing.Contains(baseId)) return baseId;
            int n = 2;
            while (existing.Contains($"{baseId}_{n}")) n++;
            return $"{baseId}_{n}";
        }

        /// <summary>Deep-clones a map's layers/tiles/entrances/exits so editing the copy can never mutate the source. Overrides are cloned separately by the caller (OverrideStore has no owning object to clone from here).</summary>
        private static MapDefinition CloneMapDefinition(MapDefinition source, string newMapId)
        {
            var clone = new MapDefinition { MapId = newMapId, WorldSeed = source.WorldSeed };
            foreach (var layer in source.Layers)
            {
                var tiles = layer.Tiles.ConvertAll(t => new TileDef(t.Id, t.Range));
                var writesOver = new List<WritesOverRule>(layer.WritesOver);
                var noise = new NoiseParams
                {
                    Octaves = layer.Noise.Octaves,
                    Frequency = layer.Noise.Frequency,
                    Persistence = layer.Noise.Persistence,
                    Lacunarity = layer.Noise.Lacunarity,
                };
                clone.Layers.Add(new LayerDef(layer.Id, tiles, writesOver, layer.Seed, noise));
            }
            foreach (var e in source.Entrances) clone.Entrances.Add(new EntrancePoint(e.Id, e.X, e.Y));
            foreach (var e in source.Exits) clone.Exits.Add(new ExitPoint(e.Id, e.X, e.Y, e.DestinationMapId, e.DestinationEntranceId));
            foreach (var r in source.BlockedTransitions) clone.BlockedTransitions.Add(new TileTransitionRule(r.FromTileId, r.ToTileId));
            return clone;
        }

        // ---------- Save / Load (whole project -- every map in _allMaps, together) ----------

        private void OnSavePressed()
        {
            string fileName = NormalizeFileName(_saveFileNameEdit.Text);
            if (string.IsNullOrEmpty(fileName))
            {
                _saveLoadStatusLabel.Text = "Enter a file name before saving.";
                return;
            }

            var mapIds = _allMaps.ConvertAll(m => m.Definition.MapId.Trim());
            if (mapIds.Exists(string.IsNullOrEmpty))
            {
                _saveLoadStatusLabel.Text = "Every map needs a Map ID before saving.";
                return;
            }
            if (MapDefinitionValidation.TryFindDuplicateId(mapIds, out var dupMapId))
            {
                _saveLoadStatusLabel.Text = $"Duplicate map id '{dupMapId}' -- map ids must be unique within a project.";
                return;
            }
            foreach (var (definition, _) in _allMaps)
            {
                if (MapDefinitionValidation.TryFindDuplicateId(definition.Entrances.ConvertAll(e => e.Id), out var dupEntrance))
                {
                    _saveLoadStatusLabel.Text = $"Map '{definition.MapId}': duplicate entrance id '{dupEntrance}'.";
                    return;
                }
                if (MapDefinitionValidation.TryFindDuplicateId(definition.Exits.ConvertAll(e => e.Id), out var dupExit))
                {
                    _saveLoadStatusLabel.Text = $"Map '{definition.MapId}': duplicate exit id '{dupExit}'.";
                    return;
                }
            }

            string path = $"{MapsDirectory}/{fileName}";
            try
            {
                DirAccess.MakeDirRecursiveAbsolute(MapsDirectory);
                string json = ProjectFileSerializer.Serialize(_allMaps.ConvertAll(m => (m.Definition, m.Overrides)));

                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    _saveLoadStatusLabel.Text = $"Could not open '{path}' for writing ({FileAccess.GetOpenError()}).";
                    return;
                }
                file.StoreString(json);
                _saveLoadStatusLabel.Text = $"Saved {_allMaps.Count} map(s) to {path}";
            }
            catch (Exception ex)
            {
                _saveLoadStatusLabel.Text = $"Save failed: {ex.Message}";
            }
        }

        private void OnLoadPressed()
        {
            string fileName = NormalizeFileName(_saveFileNameEdit.Text);
            if (string.IsNullOrEmpty(fileName))
            {
                _saveLoadStatusLabel.Text = "Enter a file name to load.";
                return;
            }

            string path = $"{MapsDirectory}/{fileName}";
            if (!FileAccess.FileExists(path))
            {
                _saveLoadStatusLabel.Text = $"No file at {path}";
                return;
            }

            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    _saveLoadStatusLabel.Text = $"Could not open '{path}' for reading ({FileAccess.GetOpenError()}).";
                    return;
                }
                string json = file.GetAsText();
                var loaded = ProjectFileSerializer.Deserialize(json);

                _allMaps = loaded.ConvertAll(m => (m.Map, m.Overrides));
                _currentMapIndex = 0;
                ActivateCurrentMap();

                _saveLoadStatusLabel.Text = $"Loaded {_allMaps.Count} map(s) from {path}";
            }
            catch (Exception ex)
            {
                _saveLoadStatusLabel.Text = $"Load failed: {ex.Message}";
            }
        }

        private static string NormalizeFileName(string typed)
        {
            string trimmed = typed.Trim();
            if (trimmed.Length == 0) return "";
            return trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ".json";
        }

        /// <summary>Resets the designated-area/Transformation fields to their defaults after loading a different map, syncing the SpinBoxes without re-triggering their change handlers.</summary>
        private void ResetRegionAndTransformFields()
        {
            _originX = 0;
            _originY = 0;
            _regionWidth = 32;
            _regionHeight = 20;
            _transformation = 0.0;

            _suppressSignals = true;
            _originXBox.Value = _originX;
            _originYBox.Value = _originY;
            _widthBox.Value = _regionWidth;
            _heightBox.Value = _regionHeight;
            _transformationBox.Value = _transformation;
            _suppressSignals = false;
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

        private void OnNoiseChanged()
        {
            var noise = CurrentLayer().Noise;
            noise.Frequency = _frequencyBox.Value;
            noise.Persistence = _persistenceBox.Value;
            noise.Lacunarity = _lacunarityBox.Value;
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
            string mapLabel = string.IsNullOrEmpty(_definition.MapId) ? "(untitled map)" : _definition.MapId;
            _statusLabel.Text =
                $"Map: {mapLabel} ({_currentMapIndex + 1}/{_allMaps.Count} in project)\n" +
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

            var def = new MapDefinition { MapId = "new_map", WorldSeed = 12345 };
            def.Layers.Add(ground);
            def.Layers.Add(groundCover);
            return def;
        }
    }
}
