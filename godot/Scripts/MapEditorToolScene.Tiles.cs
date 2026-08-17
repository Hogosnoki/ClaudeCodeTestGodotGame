#nullable enable
using System;
using Godot;
using ProcGen.Engine.Editing;
using ProcGen.Engine.Model;

namespace ProcGenGame
{
    /// <summary>
    /// The "Tiles" sub-tab under Maps: the selected layer's weighted tile palette (color/id/
    /// imported art/range/reorder/remove per row), split out of what used to be a single combined
    /// "Map" panel alongside Generation. Painting/panning on the map stays active while this
    /// sub-tab is showing (see <see cref="MapEditorToolScene.CanPaintOrDraw"/>).
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildTilesSubTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Tiles" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

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

            _addBlankRangeButton = new Button
            {
                Text = "Add Blank Range",
                TooltipText = "Adds a weighted-range slot that produces no tile -- lets the layer(s) below show through instead.",
                Visible = false,
            };
            _addBlankRangeButton.Pressed += OnAddBlankRangePressed;
            root.AddChild(_addBlankRangeButton);
            root.AddChild(new Label
            {
                Text = "Only offered for layers other than the bottom one -- the bottom layer has nothing beneath it to reveal.",
                Modulate = new Color(1, 1, 1, 0.5f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }

        private void RebuildTileRows(LayerDef layer)
        {
            foreach (Node child in _tilesContainer.GetChildren())
            {
                child.QueueFree();
            }

            for (int i = 0; i < layer.Tiles.Count; i++)
            {
                int index = i;
                var tile = layer.Tiles[i];
                var row = new HBoxContainer();

                bool isBlank = tile.Id == TileDef.NoOverrideId;
                if (isBlank)
                {
                    // No color and no rename for the blank sentinel -- it has nothing to draw,
                    // and renaming it away would silently turn it into an ordinary opaque tile.
                    row.AddChild(new Label
                    {
                        Text = "(blank -- shows layer below)",
                        CustomMinimumSize = new Vector2(98, 0),
                        Modulate = new Color(1, 1, 1, 0.7f),
                    });
                }
                else
                {
                    var colorButton = new ColorPickerButton { Color = GetTileColor(tile.Id), CustomMinimumSize = new Vector2(28, 0) };
                    colorButton.ColorChanged += c =>
                    {
                        _tileColors[tile.Id] = c;
                        _overlay.SetTileColors(_tileColors);
                    };
                    row.AddChild(colorButton);

                    var idEdit = new LineEdit { Text = tile.Id, CustomMinimumSize = new Vector2(70, 0) };
                    idEdit.TextSubmitted += _ => OnTileIdSubmitted(layer, tile, idEdit);
                    idEdit.FocusExited += () => OnTileIdSubmitted(layer, tile, idEdit);
                    row.AddChild(idEdit);

                    bool hasArt = _tileTextures.ContainsKey(tile.Id);
                    var importButton = new Button { Text = hasArt ? "Img*" : "Img", TooltipText = hasArt ? "Replace this tile's imported image" : "Import an image for this tile" };
                    importButton.Pressed += () =>
                    {
                        _pendingImportTileId = tile.Id;
                        _importImageDialog.PopupCentered();
                    };
                    row.AddChild(importButton);

                    if (hasArt)
                    {
                        var clearArt = new Button { Text = "x Img", TooltipText = "Remove the imported image -- falls back to the color swatch" };
                        clearArt.Pressed += () =>
                        {
                            _tileTextures.Remove(tile.Id);
                            _overlay.SetTileTextures(_tileTextures);
                            RebuildTileRows(layer);
                        };
                        row.AddChild(clearArt);
                    }
                }

                var minus = new Button { Text = "-" };
                // Fixed width for the same reason as the Transformation field above: predictable
                // total row size inside a ScrollContainer, regardless of expand-fill clipping.
                // Step is deliberately finer than the -/+ buttons' fixed 0.1 nudge below -- Step
                // only governs how much precision typing a value directly preserves/rounds to.
                var spin = new SpinBox { Step = 0.001, MinValue = 0, MaxValue = 1000, CustomMinimumSize = new Vector2(80, 0) };
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

                // Reordering changes the cumulative weighted-range boundaries (CompiledLayer sums
                // ranges in list order), so it can reshuffle which tiles land where -- same caveat
                // as editing a range, just via position instead of value.
                var up = new Button { Text = "^", Disabled = index == 0, TooltipText = "Move up (changes range boundaries)" };
                up.Pressed += () =>
                {
                    layer.Tiles.RemoveAt(index);
                    layer.Tiles.Insert(index - 1, tile);
                    RebuildTileRows(layer);
                    Regenerate();
                };
                row.AddChild(up);

                var down = new Button { Text = "v", Disabled = index == layer.Tiles.Count - 1, TooltipText = "Move down (changes range boundaries)" };
                down.Pressed += () =>
                {
                    layer.Tiles.RemoveAt(index);
                    layer.Tiles.Insert(index + 1, tile);
                    RebuildTileRows(layer);
                    Regenerate();
                };
                row.AddChild(down);

                var remove = new Button { Text = "x", Disabled = layer.Tiles.Count <= 1 };
                remove.TooltipText = layer.Tiles.Count <= 1
                    ? "A layer needs at least one tile"
                    : $"Remove '{tile.Id}' from this layer";
                remove.Pressed += () => OnRemoveTile(layer, tile);
                row.AddChild(remove);

                _tilesContainer.AddChild(row);
            }
        }

        /// <summary>
        /// Commits a tile-id edit. Bound to both TextSubmitted (Enter) and FocusExited (click
        /// away) so either commits the rename; the `layer.Tiles.Contains(tile)` guard makes this
        /// idempotent if both fire for the same edit (RenameOperations.RenameTile replaces the
        /// TileDef instance rather than mutating it in place, so a stale `tile` reference is no
        /// longer present in the list after the first successful call).
        /// </summary>
        private void OnTileIdSubmitted(LayerDef layer, TileDef tile, LineEdit idEdit)
        {
            if (_suppressSignals || !layer.Tiles.Contains(tile)) return;

            string oldId = tile.Id;
            string newId = idEdit.Text.Trim();
            if (newId == oldId) return;

            if (newId == TileDef.NoOverrideId)
            {
                _addTileHintLabel.Text = $"'{TileDef.NoOverrideId}' is reserved -- use 'Add Blank Range' to add a blank slot instead of renaming one into it.";
                idEdit.Text = oldId;
                return;
            }

            if (!RenameOperations.RenameTile(_definition, _overrides, layer.Id, oldId, newId))
            {
                _addTileHintLabel.Text = string.IsNullOrEmpty(newId)
                    ? "Tile id can't be empty."
                    : $"Layer '{layer.Id}' already has a tile called '{newId}'.";
                idEdit.Text = oldId;
                return;
            }

            // The tile's display color and any imported art are keyed by id string -- carry both
            // over to the new id so a rename doesn't look like it reset them.
            if (_tileColors.TryGetValue(oldId, out var color))
            {
                _tileColors.Remove(oldId);
                _tileColors[newId] = color;
            }
            if (_tileTextures.TryGetValue(oldId, out var texture))
            {
                _tileTextures.Remove(oldId);
                _tileTextures[newId] = texture;
                string oldArtPath = $"{TileArtDirectory}/{oldId}.png";
                if (FileAccess.FileExists(oldArtPath))
                {
                    DirAccess.RenameAbsolute(oldArtPath, $"{TileArtDirectory}/{newId}.png");
                }
            }

            _addTileHintLabel.Text = "";
            RebuildTileRows(layer);
            _overlay.SetTileColors(_tileColors);
            _overlay.SetTileTextures(_tileTextures);
            RefreshMovementPanel();
            Regenerate();
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
            if (id == TileDef.NoOverrideId)
            {
                _addTileHintLabel.Text = $"'{TileDef.NoOverrideId}' is reserved -- use 'Add Blank Range' below instead.";
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

        private void OnAddBlankRangePressed()
        {
            var layer = CurrentLayer();
            if (layer.Tiles.Exists(t => t.Id == TileDef.NoOverrideId))
            {
                _addTileHintLabel.Text = "This layer already has a blank range -- adjust its weight instead of adding another.";
                return;
            }

            layer.Tiles.Add(new TileDef(TileDef.NoOverrideId, 1.0));
            _addTileHintLabel.Text = "";
            RebuildTileRows(layer);
            Regenerate();
        }

        /// <summary>
        /// Loads the picked image, copies it into TileArtDirectory keyed by tile id (always
        /// re-encoded as PNG, regardless of source format, so lookups only ever need to check one
        /// fixed extension), and hands the resulting texture to the overlay -- the same pool the
        /// procedurally-generated cells and manually-painted overrides both render through, since
        /// both just resolve to a tile id.
        /// </summary>
        private void OnImportImageFileSelected(string path)
        {
            if (_pendingImportTileId == null) return;
            string tileId = _pendingImportTileId;
            _pendingImportTileId = null;

            var image = new Image();
            var loadErr = image.Load(path);
            if (loadErr != Error.Ok)
            {
                _addTileHintLabel.Text = $"Could not load '{path}': {loadErr}";
                return;
            }

            DirAccess.MakeDirRecursiveAbsolute(TileArtDirectory);
            string destPath = $"{TileArtDirectory}/{tileId}.png";
            var saveErr = image.SavePng(destPath);
            if (saveErr != Error.Ok)
            {
                _addTileHintLabel.Text = $"Could not save imported image to '{destPath}': {saveErr}";
                return;
            }

            _tileTextures[tileId] = ImageTexture.CreateFromImage(image);
            _overlay.SetTileTextures(_tileTextures);
            _addTileHintLabel.Text = "";
            RebuildTileRows(CurrentLayer());
            Regenerate();
        }

        /// <summary>
        /// Loads any already-imported art for the current map's tile ids from TileArtDirectory --
        /// called on map switch/load so art imported in an earlier session (or for another map
        /// sharing this Godot project) reappears without re-importing. Unlike tile colors, art
        /// isn't reset to defaults on load -- see class doc comment on why it's stored this way.
        /// </summary>
        private void HydrateTileTexturesFromDisk()
        {
            foreach (var layer in _definition.Layers)
            {
                foreach (var tile in layer.Tiles)
                {
                    if (tile.Id == TileDef.NoOverrideId || _tileTextures.ContainsKey(tile.Id)) continue;
                    string path = $"{TileArtDirectory}/{tile.Id}.png";
                    if (!FileAccess.FileExists(path)) continue;

                    var image = new Image();
                    if (image.Load(path) == Error.Ok)
                    {
                        _tileTextures[tile.Id] = ImageTexture.CreateFromImage(image);
                    }
                }
            }
            _overlay.SetTileTextures(_tileTextures);
        }

        private void OnRemoveTile(LayerDef layer, TileDef tile)
        {
            if (layer.Tiles.Count <= 1) return; // CompiledLayer requires at least one tile
            layer.Tiles.Remove(tile);
            RebuildTileRows(layer);
            RefreshMovementPanel();
            Regenerate();
        }
    }
}
