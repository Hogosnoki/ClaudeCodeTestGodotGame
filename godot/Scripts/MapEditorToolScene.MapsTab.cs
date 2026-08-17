#nullable enable
using Godot;

namespace ProcGenGame
{
    /// <summary>
    /// The top-level "Maps" tab: controls shared across every per-map sub-tab -- the
    /// maps-in-project list and the selected layer picker, both needed regardless of whether
    /// Generation, Tiles, Rules, or Entrance-Exit is currently showing -- followed by the
    /// <see cref="MapEditorToolScene._mapsSubTabs"/> TabContainer itself. Each sub-tab's own
    /// content is built by its respective partial-class file (MapEditorToolScene.Generation.cs,
    /// .Tiles.cs, .Rules.cs, .EntranceExit.cs).
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildMapsTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Maps" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

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

            root.AddChild(Header("Layer"));
            _layerList = new ItemList { CustomMinimumSize = new Vector2(0, 70) };
            _layerList.ItemSelected += index => SelectLayer((int)index);
            root.AddChild(_layerList);

            var layerIdRow = new HBoxContainer();
            layerIdRow.AddChild(new Label { Text = "Id", CustomMinimumSize = new Vector2(80, 0) });
            _layerIdEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _layerIdEdit.TextSubmitted += _ => OnLayerIdSubmitted();
            _layerIdEdit.FocusExited += OnLayerIdSubmitted;
            layerIdRow.AddChild(_layerIdEdit);
            root.AddChild(layerIdRow);
            _layerIdHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_layerIdHintLabel);

            _compositeToggle = new CheckBox { Text = "Show final composite" };
            _compositeToggle.Toggled += on =>
            {
                _showFinalComposite = on;
                UpdateOverlayView();
                Regenerate();
            };
            root.AddChild(_compositeToggle);
            root.AddChild(new HSeparator());

            _mapsSubTabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            root.AddChild(_mapsSubTabs);

            BuildGenerationSubTab(_mapsSubTabs);
            BuildTilesSubTab(_mapsSubTabs);
            BuildRulesSubTab(_mapsSubTabs);
            BuildEntranceExitSubTab(_mapsSubTabs);

            _mapsSubTabs.TabChanged += OnMapsSubTabChanged;
        }
    }
}
