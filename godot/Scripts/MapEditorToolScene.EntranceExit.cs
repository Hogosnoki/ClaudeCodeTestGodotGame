#nullable enable
using System;
using Godot;
using ProcGen.Engine.Model;

namespace ProcGenGame
{
    /// <summary>
    /// The "Entrance-Exit" sub-tab under Maps: this map's named entrance and exit points.
    /// Entrance/exit points render as colored markers on the map only while this sub-tab is
    /// active (see <see cref="RefreshMarkerOverlay"/>). Unlike tiles, they don't feed the
    /// generation algorithm at all -- they're pure gameplay metadata carried alongside the map --
    /// so editing them never calls Regenerate(), only RefreshMarkerOverlay().
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildEntranceExitSubTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Entrance-Exit" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

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
                Text = "A point the player leaves through, toward a destination map and one of that map's entrances -- pick both from the dropdowns below. Check the Game tab's overview for exits whose destination has since been deleted.",
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

        private void RefreshMarkerOverlay()
        {
            var entrances = _definition.Entrances.ConvertAll(e => (e.Id, e.X, e.Y));
            var exits = _definition.Exits.ConvertAll(e => (e.Id, e.X, e.Y));
            (bool IsEntrance, string Id)? armed = null;
            if (_armedEntrance != null) armed = (true, _armedEntrance.Id);
            else if (_armedExit != null) armed = (false, _armedExit.Id);

            _markerOverlay.Visible = IsMapsTabActive && IsEntranceExitSubTabActive;
            _markerOverlay.SetPoints(entrances, exits, armed);
        }
    }
}
