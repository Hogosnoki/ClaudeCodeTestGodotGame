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

            root.AddChild(Header("Variation"));
            root.AddChild(new Label
            {
                Text = "A named divergence from this map's base configuration -- e.g. a season or time of day. Only what you actually change here (seed, noise, tiles, or hand-painted cells) is stored; everything else keeps generating exactly as (Base) does.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            _variationList = new ItemList { CustomMinimumSize = new Vector2(0, 70) };
            _variationList.ItemSelected += index => SelectVariation((int)index);
            root.AddChild(_variationList);

            var variationActionsRow = new HBoxContainer();
            var newVariationButton = new Button { Text = "New" };
            newVariationButton.Pressed += OnNewVariationPressed;
            variationActionsRow.AddChild(newVariationButton);
            var duplicateVariationButton = new Button { Text = "Duplicate" };
            duplicateVariationButton.Pressed += OnDuplicateVariationPressed;
            variationActionsRow.AddChild(duplicateVariationButton);
            var deleteVariationButton = new Button { Text = "Delete" };
            deleteVariationButton.Pressed += OnDeleteVariationPressed;
            variationActionsRow.AddChild(deleteVariationButton);
            root.AddChild(variationActionsRow);

            var variationIdRow = new HBoxContainer();
            variationIdRow.AddChild(new Label { Text = "Id", CustomMinimumSize = new Vector2(80, 0) });
            _variationIdEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, Editable = false };
            _variationIdEdit.TextSubmitted += _ => OnVariationIdSubmitted();
            _variationIdEdit.FocusExited += OnVariationIdSubmitted;
            variationIdRow.AddChild(_variationIdEdit);
            root.AddChild(variationIdRow);
            _variationIdHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_variationIdHintLabel);
            _variationListHintLabel = new Label { Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_variationListHintLabel);
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
