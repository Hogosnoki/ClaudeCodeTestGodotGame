#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using ProcGen.Engine.Editing;
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
    /// The side panel is a two-tier tab layout (see <c>MapEditorToolScene.*.cs</c> for the
    /// per-tab construction code, split out of this file since each tab's UI-building code is
    /// substantial): a top-level "Game" tab (project save/load, plus a project-wide overview
    /// listing every map and any exit whose destination doesn't resolve to a real map/entrance)
    /// and a "Maps" tab. The Maps tab holds controls that apply regardless of which of its four
    /// sub-tabs is showing -- the maps-in-project list and the selected layer picker -- above
    /// "Generation" (region/Transformation/seed/noise), "Tiles" (the selected layer's tile
    /// ranges), "Rules" (directional tile-transition/movement-blocking rules), and
    /// "Entrance-Exit" (this map's named entrance and exit points) sub-tabs. Painting/panning on
    /// the map is only active while Generation or Tiles is showing; entrance/exit points render
    /// as colored markers on the map only while Entrance-Exit is showing; the traversal overlay's
    /// red blocked-edge lines only show while Rules is showing (or unconditionally during Test
    /// mode -- see below). A "Place" button per entrance/exit point arms it so the next map click
    /// sets its position. There is no separate per-tile "walkable" flag anywhere in the tool --
    /// Rules' "Solid"/"Open" buttons are a convenience that bulk-add/remove ordinary
    /// <see cref="ProcGen.Engine.Model.TileTransitionRule"/>s via
    /// <see cref="ProcGen.Engine.Movement.TraversalEditing"/>, so that's the only mechanism.
    ///
    /// Each tile row also has an "Img" button that imports an image (any format Godot's
    /// <see cref="Image"/> can load) via a native file-browse dialog, copying it into
    /// <c>res://TileArt/&lt;tileId&gt;.png</c> and handing it to the overlay
    /// (<see cref="ProcGenDebugOverlay.SetTileTextures"/>), where it takes priority over that
    /// tile's flat color -- the same pool of art serves both procedurally-generated cells and
    /// manually-painted overrides, since both are just a tile id underneath. Unlike tile colors,
    /// imported art is not reset on Load (it's re-hydrated from TileArtDirectory instead) since
    /// it's meant to persist across sessions -- but it's not yet part of the saved project JSON
    /// itself, so a project shared with someone else needs its TileArt folder shared alongside it
    /// for now.
    ///
    /// A "Test" button (top-left, below "Return to Map Area") spawns a <see cref="TestPlayerController"/>
    /// -- a plain circle, keyboard-controlled (arrow keys), moving pixel-smoothly rather than
    /// snapping tile to tile -- at the center of the designated area, so the Rules sub-tab's
    /// traversal rules can be tried out live instead of just inspected as data. Movement is
    /// resolved one axis at a time against <see cref="CompiledTraversalRules.IsBlocked"/> using
    /// the actual generated tile at each cell (via <see cref="CanEnterCell"/>), the camera follows
    /// the player, and the TraversalOverlay's red blocked-edge lines stay visible regardless of
    /// which tab was open when Test was pressed, so a blocked step is visually explained on
    /// the spot. Escape returns to the editor, restoring the pre-test camera position/zoom;
    /// map-click painting/panning is disabled for the duration since movement is keyboard-only.
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

        // Top-tier and Maps-tab sub-tier tab indices (see class doc comment for what each holds).
        private const int GameTabIndex = 0;
        private const int MapsTabIndex = 1;
        private const int GenerationSubTabIndex = 0;
        private const int TilesSubTabIndex = 1;
        private const int RulesSubTabIndex = 2;
        private const int EntranceExitSubTabIndex = 3;

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
        private TraversalOverlay _traversalOverlay = null!;
        private Node2D _worldRoot = null!;

        // "Test" mode: a keyboard-controlled TestPlayerController spawned into _worldRoot so it
        // shares the same coordinate space as the terrain, driven from _Process while active.
        // _lastResult/_lastCompiledRules are the data the last Regenerate() produced -- cached here
        // (rather than recomputed) since movement collision checks happen every frame.
        private Button _testButton = null!;
        private TestPlayerController? _testPlayer;
        private bool _testMode;
        private Vector2 _preTestCameraPosition;
        private float _preTestZoom;
        private MapResult? _lastResult;
        private CompiledTraversalRules? _lastCompiledRules;

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
        private LineEdit _layerIdEdit = null!;
        private Label _layerIdHintLabel = null!;
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
        private Button _addBlankRangeButton = null!;

        // Tile art import (see class doc comment): an imported image, keyed by tile id, takes
        // priority over that tile's flat color in the overlay -- the same pool serves both
        // procedurally-generated cells and manually-painted overrides, since both just resolve to
        // a tile id that this dictionary is keyed by. Copied into TileArtDirectory so it survives
        // across sessions (unlike colors, which are pure in-memory editor state); not yet part of
        // the saved project JSON -- see class doc comment.
        private const string TileArtDirectory = "res://TileArt";
        private readonly Dictionary<string, Texture2D> _tileTextures = new Dictionary<string, Texture2D>();
        private FileDialog _importImageDialog = null!;
        private string? _pendingImportTileId;
        private Label _statusLabel = null!;
        private Label _coordsLabel = null!;
        private CheckBox _compositeToggle = null!;
        private CheckBox _drawModeToggle = null!;

        // Save/load, the maps-in-project list, and the top-level Game/Maps tabs (with Maps'
        // Generation/Tiles/Rules/Entrance-Exit sub-tabs -- see the per-tab partial-class files).
        // A project file *is* a game (see class doc comment) -- Save/Load act on every map in
        // _allMaps at once, not just the currently selected one.
        private const string MapsDirectory = "res://Maps";
        private LineEdit _mapIdEdit = null!;
        private LineEdit _saveFileNameEdit = null!;
        private Label _saveLoadStatusLabel = null!;
        private ItemList _savedProjectsList = null!;
        private ItemList _mapList = null!;
        private Label _mapListHintLabel = null!;
        private TabContainer _topTabs = null!; // Game (0) / Maps (1)
        private TabContainer _mapsSubTabs = null!; // Generation (0) / Tiles (1) / Rules (2) / Entrance-Exit (3)
        private VBoxContainer _entrancesContainer = null!;
        private LineEdit _newEntranceIdEdit = null!;
        private Label _entranceHintLabel = null!;
        private VBoxContainer _exitsContainer = null!;
        private LineEdit _newExitIdEdit = null!;
        private Label _exitHintLabel = null!;
        private VBoxContainer _solidTileRowsContainer = null!;
        private CheckBox _includeWritesOverFamilyToggle = null!;
        private VBoxContainer _transitionRulesContainer = null!;
        private OptionButton _newRuleFromDropdown = null!;
        private OptionButton _newRuleToDropdown = null!;
        private Label _movementHintLabel = null!;
        private VBoxContainer _projectOverviewMapsContainer = null!;
        private VBoxContainer _danglingExitsContainer = null!;

        // Variations (see MapVariation's doc comment): a named divergence from the current map's
        // base configuration, selected alongside the layer picker since it's shared context for
        // the Generation/Tiles sub-tabs below. Null means "editing the base map" -- the same
        // Generation/Tiles fields and handlers are reused either way (see CurrentVariation/
        // CurrentLayerVariation/EditableNoise/EditableTiles), just redirected to write into the
        // selected variation's LayerVariation instead of the base LayerDef when one is selected.
        private ItemList _variationList = null!;
        private LineEdit _variationIdEdit = null!;
        private Label _variationIdHintLabel = null!;
        private Label _variationListHintLabel = null!;
        private string? _selectedVariationId;
        private Button _seedResetButton = null!;
        private Button _noiseResetButton = null!;
        private Button _tilesResetButton = null!;

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
            RefreshVariationList();
            RebuildEntranceRows();
            RebuildExitRows();
            RefreshMovementPanel();
            RefreshSavedProjectsList();
            HydrateTileTexturesFromDisk();
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
                case InputEventKey { Pressed: true, Keycode: Key.Escape } when _testMode:
                    ExitTestMode();
                    break;
            }
        }

        public override void _Process(double delta)
        {
            if (!_testMode || _testPlayer == null) return;

            Vector2 inputDir = Vector2.Zero;
            if (Input.IsActionPressed("ui_left")) inputDir.X -= 1f;
            if (Input.IsActionPressed("ui_right")) inputDir.X += 1f;
            if (Input.IsActionPressed("ui_up")) inputDir.Y -= 1f;
            if (Input.IsActionPressed("ui_down")) inputDir.Y += 1f;

            _testPlayer.TryMove(inputDir, (float)delta, CellPixelSize, CanEnterCell);
            CheckForExitTransition();

            // Camera follow: keep the player centered in the viewport, same math HandleZoom uses
            // to keep a fixed world point under a fixed screen point. Harmless to redo after a
            // transition -- CheckForExitTransition already centered on the new position and
            // regenerated, so this and RegenerateIfViewportChanged below are no-ops in that case.
            _worldRoot.Position = GetMapViewportSize() / 2f - _testPlayer.Position * _zoom;
            RegenerateIfViewportChanged();
        }

        /// <summary>
        /// If the test player's current cell has an exit, jumps straight to the destination
        /// map's matching entrance -- the same live-project-in-memory lookup a real game would
        /// do, just triggered by standing on the cell instead of a dedicated "interact" input.
        /// A no-op if the exit's destination map/entrance doesn't resolve (e.g. a dangling exit;
        /// see the Game tab's project overview), so a bad link fails safe rather than crashing
        /// Test mode.
        /// </summary>
        private void CheckForExitTransition()
        {
            if (_testPlayer == null) return;

            int cellX = Mathf.FloorToInt(_testPlayer.Position.X / CellPixelSize);
            int cellY = Mathf.FloorToInt(_testPlayer.Position.Y / CellPixelSize);
            var exit = _definition.Exits.Find(e => e.X == cellX && e.Y == cellY);
            if (exit == null) return;

            int destMapIndex = _allMaps.FindIndex(m => m.Definition.MapId == exit.DestinationMapId);
            if (destMapIndex < 0) return;
            var destEntrance = _allMaps[destMapIndex].Definition.Entrances.Find(e => e.Id == exit.DestinationEntranceId);
            if (destEntrance == null) return;

            _currentMapIndex = destMapIndex;
            _selectedLayerIndex = 0;
            _selectedVariationId = null;

            // Center the camera on the new spawn point *before* regenerating, since
            // RegenerateWithViewport computes what to generate from the current camera position.
            _testPlayer.Position = new Vector2((destEntrance.X + 0.5f) * CellPixelSize, (destEntrance.Y + 0.5f) * CellPixelSize);
            _worldRoot.Position = GetMapViewportSize() / 2f - _testPlayer.Position * _zoom;
            Regenerate(); // not RegenerateIfViewportChanged -- the map changed even if the cell-quantized viewport rectangle didn't
        }

        // ---------- Tab state ----------
        // Replaces the old single `_panelMode` int -- painting/overlay visibility now depend on
        // both which top-level tab and (when on Maps) which sub-tab is active.

        private bool IsMapsTabActive => _topTabs.CurrentTab == MapsTabIndex;
        private bool IsGenerationSubTabActive => _mapsSubTabs.CurrentTab == GenerationSubTabIndex;
        private bool IsTilesSubTabActive => _mapsSubTabs.CurrentTab == TilesSubTabIndex;
        private bool IsRulesSubTabActive => _mapsSubTabs.CurrentTab == RulesSubTabIndex;
        private bool IsEntranceExitSubTabActive => _mapsSubTabs.CurrentTab == EntranceExitSubTabIndex;

        /// <summary>Whether the map viewport should paint/clear overrides on click rather than pan -- true only for the Maps tab's Generation and Tiles sub-tabs, the two that actually shape terrain.</summary>
        private bool CanPaintOrDraw => IsMapsTabActive && (IsGenerationSubTabActive || IsTilesSubTabActive);

        private void OnTopTabChanged(long index)
        {
            _armedEntrance = null;
            _armedExit = null;
            if (index == GameTabIndex) RefreshProjectOverview();
            RefreshMarkerOverlay();
            UpdateTraversalOverlayVisibility();
        }

        private void OnMapsSubTabChanged(long index)
        {
            _armedEntrance = null;
            _armedExit = null;
            RefreshMarkerOverlay();
            UpdateTraversalOverlayVisibility();
        }

        private void UpdateTraversalOverlayVisibility()
        {
            // Test mode keeps the blocked-edge overlay visible regardless of the active tab (see
            // class doc comment) -- outside Test mode it only makes sense while Rules is showing.
            _traversalOverlay.Visible = _testMode || (IsMapsTabActive && IsRulesSubTabActive);
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
            _traversalOverlay = new TraversalOverlay { CellPixelSize = CellPixelSize, Visible = false };
            _worldRoot.AddChild(_traversalOverlay);

            BuildMapHud();
            BuildSidePanel();

            _importImageDialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Title = "Import tile image",
                Size = new Vector2I(800, 600),
            };
            _importImageDialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.bmp,*.webp,*.tga ; Image files" };
            _importImageDialog.FileSelected += OnImportImageFileSelected;
            AddChild(_importImageDialog);
        }

        /// <summary>Floating controls over the map viewport itself -- deliberately not inside the scrollable side panel, so they're reachable no matter how lost the camera gets.</summary>
        private void BuildMapHud()
        {
            var returnButton = new Button { Text = "Return to Map Area" };
            returnButton.AnchorLeft = 0f; returnButton.AnchorTop = 0f; returnButton.AnchorRight = 0f; returnButton.AnchorBottom = 0f;
            returnButton.OffsetLeft = 12; returnButton.OffsetTop = 12;
            returnButton.Pressed += CenterOnDesignatedArea;
            AddChild(returnButton);

            _testButton = new Button { Text = "Test", TooltipText = "Spawn a keyboard-controlled test player to try out the Rules sub-tab's traversal rules. Arrow keys to move, Escape to return to the editor." };
            _testButton.AnchorLeft = 0f; _testButton.AnchorTop = 0f; _testButton.AnchorRight = 0f; _testButton.AnchorBottom = 0f;
            _testButton.OffsetLeft = 12; _testButton.OffsetTop = 48;
            _testButton.Pressed += EnterTestMode;
            AddChild(_testButton);

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

        /// <summary>Builds the panel shell (scroll container + status label) and the top-level Game/Maps tabs. Each tab's own content is built by <c>BuildGameTab</c>/<c>BuildMapsTab</c> in their respective partial-class files.</summary>
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

            _topTabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            root.AddChild(_topTabs);

            BuildGameTab(_topTabs);
            BuildMapsTab(_topTabs);

            _topTabs.TabChanged += OnTopTabChanged;
            // Start on Maps (Generation sub-tab), matching the old default of editing straight
            // into region/seed/noise -- "Game" is listed first per its project-wide role, but
            // isn't where map-making actually happens.
            _topTabs.CurrentTab = MapsTabIndex;
            RefreshMarkerOverlay();
            UpdateTraversalOverlayVisibility();
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

        private SpinBox AddDoubleField(VBoxContainer parent, string label, double min, double max, Action onChanged, double step = 0.01)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
            var spin = new SpinBox { Step = step, MinValue = min, MaxValue = max, SizeFlagsHorizontal = SizeFlags.ExpandFill };
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

            // No painting/panning/placing while testing -- movement is keyboard-only and the
            // camera follows the player, so a click here would just fight that. Zoom above still
            // works, since it doesn't touch player or camera-follow state.
            if (_testMode) return;

            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed && (_armedEntrance != null || _armedExit != null))
                {
                    PlaceArmedMarkerAtMouse();
                    return;
                }
                // Outside the Maps tab's Generation/Tiles sub-tabs there's nothing to paint --
                // dragging always pans, regardless of the Draw Mode toggle (which only makes
                // sense for terrain).
                if (!CanPaintOrDraw || !_drawMode)
                {
                    _isPanning = mouse.Pressed;
                    _lastPanMousePos = mouse.Position;
                    return;
                }
                if (mouse.Pressed) PaintAtMouse();
                return;
            }

            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed && _drawMode && CanPaintOrDraw)
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

        // ---------- Test mode ----------

        /// <summary>Spawns a keyboard-controlled TestPlayerController at the center of the designated area to try out the Rules sub-tab's traversal rules live. Escape (see _UnhandledInput) returns to the editor.</summary>
        private void EnterTestMode()
        {
            if (_testMode) return;
            _testMode = true;

            // A focused SpinBox/LineEdit would otherwise eat the arrow keys meant for movement.
            GetViewport().GuiReleaseFocus();

            _preTestCameraPosition = _worldRoot.Position;
            _preTestZoom = _zoom;

            var spawnWorldPixel = new Vector2(
                (_originX + _regionWidth / 2f) * CellPixelSize,
                (_originY + _regionHeight / 2f) * CellPixelSize);
            _testPlayer = new TestPlayerController { Position = spawnWorldPixel };
            _worldRoot.AddChild(_testPlayer);

            // Show blocked edges regardless of which tab was open, so a tester can see exactly
            // why a step was refused.
            UpdateTraversalOverlayVisibility();
            _testButton.Disabled = true;
            UpdateStatus();
        }

        private void ExitTestMode()
        {
            if (!_testMode) return;
            _testMode = false;

            _testPlayer?.QueueFree();
            _testPlayer = null;

            _zoom = _preTestZoom;
            _worldRoot.Scale = Vector2.One * _zoom;
            _worldRoot.Position = _preTestCameraPosition;

            UpdateTraversalOverlayVisibility();
            _testButton.Disabled = false;
            RegenerateIfViewportChanged();
            UpdateStatus();
        }

        /// <summary>Whether the test player may step from fromCellWorld onto the adjacent toCellWorld -- an unresolved (not-yet-generated, out-of-viewport) cell fails closed rather than letting the player walk into the unknown.</summary>
        private bool CanEnterCell(Vector2I fromCellWorld, Vector2I toCellWorld)
        {
            if (_lastResult == null || _lastCompiledRules == null || !_lastViewport.HasValue) return false;

            string? fromTile = TileAtWorldCell(fromCellWorld, _lastViewport.Value);
            string? toTile = TileAtWorldCell(toCellWorld, _lastViewport.Value);
            if (fromTile == null || toTile == null) return false;

            return !_lastCompiledRules.IsBlocked(fromTile, toTile);
        }

        private string? TileAtWorldCell(Vector2I worldCell, RegionSpec viewport)
        {
            int lx = worldCell.X - viewport.OriginX;
            int ly = worldCell.Y - viewport.OriginY;
            if (lx < 0 || ly < 0 || lx >= viewport.Width || ly >= viewport.Height) return null;
            return _lastResult!.GetFinalTile(lx, ly);
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
            if (_selectedLayerIndex < _layerList.ItemCount)
            {
                _layerList.Select(_selectedLayerIndex);
            }
        }

        private void SelectLayer(int index)
        {
            if (index < 0 || index >= _definition.Layers.Count) return;

            _selectedLayerIndex = index;
            _layerList.Select(index);

            var layer = _definition.Layers[index];
            _suppressSignals = true;
            _layerIdEdit.Text = layer.Id;
            _layerIdHintLabel.Text = "";
            _suppressSignals = false;

            _addBlankRangeButton.Visible = index > 0;
            RefreshGenerationAndTilesFields();
            UpdateOverlayView();
            Regenerate();
        }

        private LayerDef CurrentLayer() => _definition.Layers[_selectedLayerIndex];

        // ---------- Variations ----------
        // A variation is a named divergence from the current map's base configuration (see
        // MapVariation's doc comment) -- null _selectedVariationId means "editing the base map".
        // The Generation/Tiles sub-tabs are reused unchanged either way: their fields always show
        // the EFFECTIVE (resolved) seed/noise/tiles for the current layer+variation combination,
        // and their handlers always write through EditableNoise()/EditableTiles(), which lazily
        // creates the variation's LayerVariation entry (cloned from the base layer) on first
        // write rather than requiring a separate "start overriding" step.

        private MapVariation? CurrentVariation() =>
            _selectedVariationId == null ? null : _definition.Variations.Find(v => v.Id == _selectedVariationId);

        /// <summary>The current variation's override entry for the current layer, if any. createIfMissing lazily adds an empty one (inheriting everything) the first time something is actually edited -- never called just to display effective values.</summary>
        private LayerVariation? CurrentLayerVariation(bool createIfMissing)
        {
            var variation = CurrentVariation();
            if (variation == null) return null;
            string layerId = CurrentLayer().Id;
            var existing = variation.LayerOverrides.Find(lv => lv.LayerId == layerId);
            if (existing != null || !createIfMissing) return existing;

            var created = new LayerVariation { LayerId = layerId };
            variation.LayerOverrides.Add(created);
            return created;
        }

        private SeedPosition EffectiveSeed() => CurrentLayerVariation(false)?.Seed ?? CurrentLayer().Seed;
        private NoiseParams EffectiveNoise() => CurrentLayerVariation(false)?.Noise ?? CurrentLayer().Noise;
        private List<TileDef> EffectiveTiles() => CurrentLayerVariation(false)?.Tiles ?? CurrentLayer().Tiles;

        /// <summary>The NoiseParams instance to write into: the base layer's own, or (lazily forked from it) the current variation's override.</summary>
        private NoiseParams EditableNoise()
        {
            if (_selectedVariationId == null) return CurrentLayer().Noise;
            var lv = CurrentLayerVariation(createIfMissing: true)!;
            var baseNoise = CurrentLayer().Noise;
            lv.Noise ??= new NoiseParams { Octaves = baseNoise.Octaves, Frequency = baseNoise.Frequency, Persistence = baseNoise.Persistence, Lacunarity = baseNoise.Lacunarity };
            return lv.Noise;
        }

        /// <summary>The tile list to mutate: the base layer's own, or (lazily cloned from it) the current variation's override.</summary>
        private List<TileDef> EditableTiles()
        {
            if (_selectedVariationId == null) return CurrentLayer().Tiles;
            var lv = CurrentLayerVariation(createIfMissing: true)!;
            lv.Tiles ??= CurrentLayer().Tiles.ConvertAll(t => new TileDef(t.Id, t.Range));
            return lv.Tiles;
        }

        /// <summary>Refreshes the Generation sub-tab's seed/noise fields and the Tiles sub-tab's rows from the current layer+variation's effective values -- called on layer switch, variation switch, and after a Reset-to-base.</summary>
        private void RefreshGenerationAndTilesFields()
        {
            var seed = EffectiveSeed();
            var noise = EffectiveNoise();
            _suppressSignals = true;
            _seedXBox.Value = seed.X;
            _seedYBox.Value = seed.Y;
            _seedTBox.Value = seed.T;
            _octavesBox.Value = noise.Octaves;
            _frequencyBox.Value = noise.Frequency;
            _persistenceBox.Value = noise.Persistence;
            _lacunarityBox.Value = noise.Lacunarity;
            _suppressSignals = false;

            bool editingVariation = _selectedVariationId != null;
            var layerVariation = CurrentLayerVariation(false);
            _seedResetButton.Visible = editingVariation && layerVariation?.Seed != null;
            _noiseResetButton.Visible = editingVariation && layerVariation?.Noise != null;
            _tilesResetButton.Visible = editingVariation && layerVariation?.Tiles != null;

            RebuildTileRows(EffectiveTiles());
        }

        private void RefreshVariationList()
        {
            _variationList.Clear();
            _variationList.AddItem("(Base)");
            foreach (var variation in _definition.Variations)
            {
                _variationList.AddItem(variation.Id);
            }
            int selectIndex = _selectedVariationId == null
                ? 0
                : _definition.Variations.FindIndex(v => v.Id == _selectedVariationId) + 1;
            _variationList.Select(Math.Max(0, selectIndex));
        }

        private void SelectVariation(int index)
        {
            _selectedVariationId = index <= 0 || index > _definition.Variations.Count
                ? null
                : _definition.Variations[index - 1].Id;
            _variationList.Select(index);
            _suppressSignals = true;
            _variationIdEdit.Text = _selectedVariationId ?? "";
            _variationIdEdit.Editable = _selectedVariationId != null;
            _variationIdHintLabel.Text = "";
            _suppressSignals = false;
            RefreshGenerationAndTilesFields();
            Regenerate();
        }

        private void OnNewVariationPressed()
        {
            var variation = new MapVariation { Id = GenerateUniqueVariationId("variation") };
            _definition.Variations.Add(variation);
            _selectedVariationId = variation.Id;
            RefreshVariationList();
            SelectVariation(_definition.Variations.Count);
            _variationListHintLabel.Text = "";
        }

        private void OnDuplicateVariationPressed()
        {
            var source = CurrentVariation();
            var clone = new MapVariation
            {
                Id = GenerateUniqueVariationId(source?.Id ?? "variation"),
                LayerOverrides = source?.LayerOverrides.ConvertAll(lv => new LayerVariation
                {
                    LayerId = lv.LayerId,
                    Seed = lv.Seed,
                    Noise = lv.Noise == null ? null : new NoiseParams { Octaves = lv.Noise.Octaves, Frequency = lv.Noise.Frequency, Persistence = lv.Noise.Persistence, Lacunarity = lv.Noise.Lacunarity },
                    Tiles = lv.Tiles?.ConvertAll(t => new TileDef(t.Id, t.Range)),
                }) ?? new List<LayerVariation>(),
                Overrides = source == null ? new List<TileOverride>() : new List<TileOverride>(source.Overrides),
            };
            _definition.Variations.Add(clone);
            _selectedVariationId = clone.Id;
            RefreshVariationList();
            SelectVariation(_definition.Variations.Count);
            _variationListHintLabel.Text = "";
        }

        private void OnDeleteVariationPressed()
        {
            var variation = CurrentVariation();
            if (variation == null)
            {
                _variationListHintLabel.Text = "Select a variation to delete -- (Base) can't be removed.";
                return;
            }
            _definition.Variations.Remove(variation);
            _selectedVariationId = null;
            RefreshVariationList();
            SelectVariation(0);
            _variationListHintLabel.Text = "";
        }

        private void OnVariationIdSubmitted()
        {
            if (_suppressSignals) return;
            var variation = CurrentVariation();
            if (variation == null) return;

            string oldId = variation.Id;
            string newId = _variationIdEdit.Text.Trim();
            if (newId == oldId) return;
            if (string.IsNullOrEmpty(newId))
            {
                _variationIdHintLabel.Text = "Variation id can't be empty.";
                _variationIdEdit.Text = oldId;
                return;
            }
            if (_definition.Variations.Exists(v => v.Id == newId))
            {
                _variationIdHintLabel.Text = $"A variation called '{newId}' already exists.";
                _variationIdEdit.Text = oldId;
                return;
            }
            variation.Id = newId;
            _selectedVariationId = newId;
            _variationIdHintLabel.Text = "";
            RefreshVariationList();
        }

        private string GenerateUniqueVariationId(string baseId)
        {
            var existing = new HashSet<string>();
            foreach (var v in _definition.Variations) existing.Add(v.Id);

            if (!existing.Contains(baseId)) return baseId;
            int n = 2;
            while (existing.Contains($"{baseId}_{n}")) n++;
            return $"{baseId}_{n}";
        }

        private void OnLayerIdSubmitted()
        {
            if (_suppressSignals) return;
            string oldId = CurrentLayer().Id;
            string newId = _layerIdEdit.Text.Trim();
            if (newId == oldId)
            {
                _layerIdHintLabel.Text = "";
                return;
            }
            if (!RenameOperations.RenameLayer(_definition, _overrides, oldId, newId))
            {
                _layerIdHintLabel.Text = string.IsNullOrEmpty(newId)
                    ? "Layer id can't be empty."
                    : $"A layer called '{newId}' already exists.";
                _layerIdEdit.Text = oldId;
                return;
            }
            _layerIdHintLabel.Text = "";
            RefreshLayerList();
            // The overlay's "which layer am I viewing" selector holds the layer id as of the last
            // ShowLayer call -- without refreshing it here, it stays pointed at the old id, and
            // the next regenerate (from any cause) hands it a MapResult whose per-layer grid is
            // keyed by the new id, throwing a KeyNotFoundException when it tries to look itself up.
            UpdateOverlayView();
            Regenerate();
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
            _selectedVariationId = null;
            _armedEntrance = null;
            _armedExit = null;

            _suppressSignals = true;
            _mapIdEdit.Text = _definition.MapId;
            _suppressSignals = false;

            ResetRegionAndTransformFields();
            RefreshLayerList();
            RefreshVariationList();
            RebuildEntranceRows();
            RebuildExitRows();
            RefreshMovementPanel();
            HydrateTileTexturesFromDisk();
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
            foreach (var v in source.Variations)
            {
                clone.Variations.Add(new MapVariation
                {
                    Id = v.Id,
                    LayerOverrides = v.LayerOverrides.ConvertAll(lv => new LayerVariation
                    {
                        LayerId = lv.LayerId,
                        Seed = lv.Seed,
                        Noise = lv.Noise == null ? null : new NoiseParams { Octaves = lv.Noise.Octaves, Frequency = lv.Noise.Frequency, Persistence = lv.Noise.Persistence, Lacunarity = lv.Noise.Lacunarity },
                        Tiles = lv.Tiles?.ConvertAll(t => new TileDef(t.Id, t.Range)),
                    }),
                    Overrides = new List<TileOverride>(v.Overrides),
                });
            }
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
                RefreshSavedProjectsList();
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

        /// <summary>Lists every ".json" file directly inside MapsDirectory, alphabetically, so Load doesn't require typing a file name blind.</summary>
        private void RefreshSavedProjectsList()
        {
            _savedProjectsList.Clear();

            using var dir = DirAccess.Open(MapsDirectory);
            if (dir == null) return; // no saves made yet -- directory doesn't exist

            var names = new List<string>();
            dir.ListDirBegin();
            string entry = dir.GetNext();
            while (entry != "")
            {
                if (!dir.CurrentIsDir() && entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    names.Add(entry);
                entry = dir.GetNext();
            }
            dir.ListDirEnd();

            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names) _savedProjectsList.AddItem(name);
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
            var newSeed = new SeedPosition(_seedXBox.Value, _seedYBox.Value, _seedTBox.Value);
            if (_selectedVariationId == null)
            {
                CurrentLayer().Seed = newSeed;
            }
            else
            {
                CurrentLayerVariation(createIfMissing: true)!.Seed = newSeed;
                _seedResetButton.Visible = true;
            }
            Regenerate();
        }

        private void OnNoiseChanged()
        {
            var noise = EditableNoise();
            noise.Frequency = _frequencyBox.Value;
            noise.Persistence = _persistenceBox.Value;
            noise.Lacunarity = _lacunarityBox.Value;
            _noiseResetButton.Visible = _selectedVariationId != null;
            Regenerate();
        }

        private void OnSeedResetPressed()
        {
            var lv = CurrentLayerVariation(false);
            if (lv != null) lv.Seed = null;
            RefreshGenerationAndTilesFields();
            Regenerate();
        }

        private void OnNoiseResetPressed()
        {
            var lv = CurrentLayerVariation(false);
            if (lv != null) lv.Noise = null;
            RefreshGenerationAndTilesFields();
            Regenerate();
        }

        private void OnTilesResetPressed()
        {
            var lv = CurrentLayerVariation(false);
            if (lv != null) lv.Tiles = null;
            RefreshGenerationAndTilesFields();
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
            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;
            PaintOrClearAtCell(cell.Value, clear: false);
            Regenerate();
        }

        private void ClearOverrideAtMouse()
        {
            var cell = _overlay.LocalPositionToCell(_overlay.GetLocalMousePosition());
            if (cell == null) return;
            PaintOrClearAtCell(cell.Value, clear: true);
            Regenerate();
        }

        /// <summary>
        /// Cycles (or clears) a manual override at a cell. Editing the base map writes straight
        /// into <see cref="_overrides"/>, same as always. Editing a variation writes into that
        /// variation's own override diff instead -- never the base map's -- so a variation only
        /// ever stores what diverges (see MapVariation's doc comment); clearing a cell there just
        /// means "stop diverging here", not "erase whatever the base map painted".
        /// </summary>
        private void PaintOrClearAtCell(Vector2I cell, bool clear)
        {
            string layerId = CurrentLayer().Id;
            var variation = CurrentVariation();
            if (variation == null)
            {
                if (clear) _overrides.Clear(layerId, cell.X, cell.Y);
                else CycleOverride(_overrides, _overrides, layerId, EffectiveTiles(), cell);
                return;
            }

            var variationStore = OverrideStore.FromRecords(variation.Overrides);
            if (clear) variationStore.Clear(layerId, cell.X, cell.Y);
            else CycleOverride(variationStore, _overrides, layerId, EffectiveTiles(), cell);
            variation.Overrides = new List<TileOverride>(variationStore.Enumerate());
        }

        /// <summary>Advances a cell's override to the next tile in order (wrapping back to "no override"), starting from whatever's currently effectively visible: writeTo's own value if it has one at this cell, else readFallback's -- but the result is only ever written into writeTo.</summary>
        private static void CycleOverride(OverrideStore writeTo, OverrideStore readFallback, string layerId, List<TileDef> tiles, Vector2I cell)
        {
            string? current = writeTo.TryGet(layerId, cell.X, cell.Y, out var own)
                ? own
                : readFallback.TryGet(layerId, cell.X, cell.Y, out var fallback) ? fallback : null;
            int nextIndex = current == null ? 0 : tiles.FindIndex(t => t.Id == current) + 1;

            if (nextIndex >= tiles.Count) writeTo.Clear(layerId, cell.X, cell.Y);
            else writeTo.Set(layerId, cell.X, cell.Y, tiles[nextIndex].Id);
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
                // Resolving through VariationResolution is a no-op (returns the base pair
                // unchanged) whenever _selectedVariationId is null -- see its doc comment.
                var (effectiveDefinition, effectiveOverrides) = VariationResolution.Resolve(_definition, _overrides, _selectedVariationId);
                var result = MapGenerator.GenerateRegion(effectiveDefinition, viewport, effectiveOverrides);
                _lastResult = result;
                _lastCompiledRules = CompiledTraversalRules.Compile(effectiveDefinition);
                _overlay.Render(result, viewport, effectiveOverrides);
                _traversalOverlay.Render(result, viewport, _lastCompiledRules);
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
            if (_testMode && _testPlayer != null)
            {
                int cellX = Mathf.FloorToInt(_testPlayer.Position.X / CellPixelSize);
                int cellY = Mathf.FloorToInt(_testPlayer.Position.Y / CellPixelSize);
                string tile = TileAtWorldCell(new Vector2I(cellX, cellY), _lastViewport ?? default) ?? "?";
                string testMapLabel = string.IsNullOrEmpty(_definition.MapId) ? "(untitled map)" : _definition.MapId;
                _statusLabel.Text =
                    "Test mode -- arrow keys to move, Escape to return to the editor.\n" +
                    $"Map: {testMapLabel}\n" +
                    $"Player cell: ({cellX}, {cellY})   Standing on: {tile}\n" +
                    "Walking onto an exit jumps to its destination entrance.";
                return;
            }

            var layer = _definition.Layers[_selectedLayerIndex];
            string view = _showFinalComposite ? "final composite" : layer.Id;
            string mapLabel = string.IsNullOrEmpty(_definition.MapId) ? "(untitled map)" : _definition.MapId;
            string variationLabel = _selectedVariationId == null ? "(Base)" : _selectedVariationId;
            _statusLabel.Text =
                $"Map: {mapLabel} ({_currentMapIndex + 1}/{_allMaps.Count} in project)\n" +
                $"Variation: {variationLabel}\n" +
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
