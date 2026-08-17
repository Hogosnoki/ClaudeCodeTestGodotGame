#nullable enable
using Godot;
using ProcGen.Engine.Validation;

namespace ProcGenGame
{
    /// <summary>
    /// The top-level "Game" tab: project-wide controls that apply to every map at once --
    /// Save/Load (see <see cref="MapEditorToolScene.OnSavePressed"/>/<see cref="MapEditorToolScene.OnLoadPressed"/>)
    /// and a summary of every map in the project plus any exit that doesn't resolve to a real
    /// map/entrance. Refreshed lazily (only when this tab becomes active) via
    /// <see cref="MapEditorToolScene.OnTopTabChanged"/>.
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildGameTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Game" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

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

            var savedProjectsRow = new HBoxContainer();
            savedProjectsRow.AddChild(new Label { Text = "Existing projects", SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var refreshSavedProjectsButton = new Button { Text = "Refresh" };
            refreshSavedProjectsButton.Pressed += RefreshSavedProjectsList;
            savedProjectsRow.AddChild(refreshSavedProjectsButton);
            root.AddChild(savedProjectsRow);
            _savedProjectsList = new ItemList { CustomMinimumSize = new Vector2(0, 80) };
            _savedProjectsList.ItemSelected += index => _saveFileNameEdit.Text = _savedProjectsList.GetItemText((int)index);
            root.AddChild(_savedProjectsList);
            root.AddChild(new Label
            {
                Text = "Click a name to fill it into the field above, then Save (overwrite) or Load.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Maps overview"));
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
    }
}
