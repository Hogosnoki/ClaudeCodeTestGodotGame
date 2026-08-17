#nullable enable
using System;
using System.Collections.Generic;
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
    ///
    /// Every row operation below goes through <see cref="MapEditorToolScene.EditableTiles"/>
    /// rather than a captured LayerDef -- when a variation is selected this lazily forks a copy
    /// of the base layer's tile list into that variation's <see cref="LayerVariation.Tiles"/> on
    /// first edit, so the same row-building/mutation code edits either the base layer or the
    /// current variation's own palette without needing two parallel code paths. Rows are always
    /// *displayed* from <see cref="MapEditorToolScene.EffectiveTiles"/> (the resolved base-or-
    /// variation list) so switching layers/variations never has to guess which list to show.
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildTilesSubTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Tiles" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

            var tilesHeaderRow = new HBoxContainer();
            tilesHeaderRow.AddChild(new Label { Text = "Tiles (selected layer)", SizeFlagsHorizontal = SizeFlags.ExpandFill });
            _tilesResetButton = new Button { Text = "Reset to base", Visible = false, TooltipText = "Discard this variation's tile-list override -- inherit the base map's tiles again" };
            _tilesResetButton.Pressed += OnTilesResetPressed;
            tilesHeaderRow.AddChild(_tilesResetButton);
            root.AddChild(tilesHeaderRow);
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

        private void RebuildTileRows(List<TileDef> tiles)
        {
            foreach (Node child in _tilesContainer.GetChildren())
            {
                child.QueueFree();
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                int index = i;
                var tile = tiles[i];
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
                    idEdit.TextSubmitted += _ => OnTileIdSubmitted(index, tile.Id, idEdit);
                    idEdit.FocusExited += () => OnTileIdSubmitted(index, tile.Id, idEdit);
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
                            RebuildTileRows(EffectiveTiles());
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

                // Always writes through EditableTiles() (by position, not the captured `tile`
                // reference) so the first edit against a variation forks the list at exactly
                // this moment, whichever control triggers it.
                spin.ValueChanged += v =>
                {
                    EditableTiles()[index].Range = v;
                    _tilesResetButton.Visible = _selectedVariationId != null;
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
                    var editable = EditableTiles();
                    var moved = editable[index];
                    editable.RemoveAt(index);
                    editable.Insert(index - 1, moved);
                    _tilesResetButton.Visible = _selectedVariationId != null;
                    RebuildTileRows(EffectiveTiles());
                    Regenerate();
                };
                row.AddChild(up);

                var down = new Button { Text = "v", Disabled = index == tiles.Count - 1, TooltipText = "Move down (changes range boundaries)" };
                down.Pressed += () =>
                {
                    var editable = EditableTiles();
                    var moved = editable[index];
                    editable.RemoveAt(index);
                    editable.Insert(index + 1, moved);
                    _tilesResetButton.Visible = _selectedVariationId != null;
                    RebuildTileRows(EffectiveTiles());
                    Regenerate();
                };
                row.AddChild(down);

                var remove = new Button { Text = "x", Disabled = tiles.Count <= 1 };
                remove.TooltipText = tiles.Count <= 1
                    ? "A layer needs at least one tile"
                    : $"Remove '{tile.Id}' from this layer";
                remove.Pressed += () => OnRemoveTile(index);
                row.AddChild(remove);

                _tilesContainer.AddChild(row);
            }
        }

        /// <summary>
        /// Commits a tile-id edit. Bound to both TextSubmitted (Enter) and FocusExited (click
        /// away) so either commits the rename; the "does the tile at this index still have the id
        /// we captured when the row was built" guard makes this idempotent if both fire for the
        /// same edit (a rename replaces the TileDef instance, so re-checking by captured id
        /// rather than by stale object reference is what catches the second, now-stale signal).
        /// Renaming the base map's tile cascades through writes_over/BlockedTransitions/overrides
        /// via <see cref="RenameOperations.RenameTile"/>; renaming a tile a variation introduced
        /// itself only cascades through that variation's own overrides, via
        /// <see cref="RenameOperations.RenameVariationTile"/> -- see that method's doc comment.
        /// </summary>
        private void OnTileIdSubmitted(int index, string capturedId, LineEdit idEdit)
        {
            if (_suppressSignals) return;
            var tiles = EditableTiles();
            if (index < 0 || index >= tiles.Count || tiles[index].Id != capturedId) return;

            string oldId = capturedId;
            string newId = idEdit.Text.Trim();
            if (newId == oldId) return;

            if (newId == TileDef.NoOverrideId)
            {
                _addTileHintLabel.Text = $"'{TileDef.NoOverrideId}' is reserved -- use 'Add Blank Range' to add a blank slot instead of renaming one into it.";
                idEdit.Text = oldId;
                return;
            }

            bool renamed = _selectedVariationId == null
                ? RenameOperations.RenameTile(_definition, _overrides, CurrentLayer().Id, oldId, newId)
                : RenameOperations.RenameVariationTile(CurrentVariation()!, CurrentLayer().Id, oldId, newId);
            if (!renamed)
            {
                _addTileHintLabel.Text = string.IsNullOrEmpty(newId)
                    ? "Tile id can't be empty."
                    : $"Layer '{CurrentLayer().Id}' already has a tile called '{newId}'.";
                idEdit.Text = oldId;
                return;
            }

            // The tile's display color and any imported art are keyed by id string -- carry both
            // over to the new id so a rename doesn't look like it reset them. oldId is only
            // removed from these *global* (not per-variation) dictionaries if nothing else in
            // the map -- a base layer, or another variation's own tile-list override -- still
            // uses it; otherwise this rename (e.g. a variation's own copy of "land" becoming
            // "snow") would steal the color/art out from under whoever else still has "land".
            bool oldIdStillInUse = TileIdInUseAnywhere(oldId);
            if (_tileColors.TryGetValue(oldId, out var color))
            {
                if (!oldIdStillInUse) _tileColors.Remove(oldId);
                _tileColors[newId] = color;
            }
            if (_tileTextures.TryGetValue(oldId, out var texture))
            {
                _tileTextures[newId] = texture;
                string oldArtPath = $"{TileArtDirectory}/{oldId}.png";
                if (FileAccess.FileExists(oldArtPath))
                {
                    if (oldIdStillInUse)
                    {
                        DirAccess.CopyAbsolute(oldArtPath, $"{TileArtDirectory}/{newId}.png");
                    }
                    else
                    {
                        _tileTextures.Remove(oldId);
                        DirAccess.RenameAbsolute(oldArtPath, $"{TileArtDirectory}/{newId}.png");
                    }
                }
            }

            _addTileHintLabel.Text = "";
            _tilesResetButton.Visible = _selectedVariationId != null;
            RebuildTileRows(EffectiveTiles());
            _overlay.SetTileColors(_tileColors);
            _overlay.SetTileTextures(_tileTextures);
            RefreshMovementPanel();
            Regenerate();
        }

        private void OnAddTilePressed()
        {
            string id = _newTileIdEdit.Text.Trim();
            var tiles = EditableTiles();

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
            if (tiles.Exists(t => t.Id == id))
            {
                _addTileHintLabel.Text = $"Layer '{CurrentLayer().Id}' already has a tile called '{id}'.";
                return;
            }

            tiles.Add(new TileDef(id, 1.0));
            GetTileColor(id); // assigns this new id a default color if it doesn't have one yet
            _newTileIdEdit.Text = "";
            _addTileHintLabel.Text = "";
            _tilesResetButton.Visible = _selectedVariationId != null;

            RebuildTileRows(EffectiveTiles());
            _overlay.SetTileColors(_tileColors);
            RefreshMovementPanel();
            Regenerate();
        }

        private void OnAddBlankRangePressed()
        {
            var tiles = EditableTiles();
            if (tiles.Exists(t => t.Id == TileDef.NoOverrideId))
            {
                _addTileHintLabel.Text = "This layer already has a blank range -- adjust its weight instead of adding another.";
                return;
            }

            tiles.Add(new TileDef(TileDef.NoOverrideId, 1.0));
            _addTileHintLabel.Text = "";
            _tilesResetButton.Visible = _selectedVariationId != null;
            RebuildTileRows(EffectiveTiles());
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
            RebuildTileRows(EffectiveTiles());
            Regenerate();
        }

        /// <summary>
        /// Loads any already-imported art for the current map's tile ids (base layers plus every
        /// variation's own tile-list overrides) from TileArtDirectory -- called on map switch/
        /// load so art imported in an earlier session (or for another map sharing this Godot
        /// project) reappears without re-importing. Unlike tile colors, art isn't reset to
        /// defaults on load -- see class doc comment on why it's stored this way.
        /// </summary>
        private void HydrateTileTexturesFromDisk()
        {
            foreach (var layer in _definition.Layers)
            {
                HydrateTileTexturesFromDisk(layer.Tiles);
            }
            foreach (var variation in _definition.Variations)
            {
                foreach (var layerVariation in variation.LayerOverrides)
                {
                    if (layerVariation.Tiles != null) HydrateTileTexturesFromDisk(layerVariation.Tiles);
                }
            }
            _overlay.SetTileTextures(_tileTextures);
        }

        private void HydrateTileTexturesFromDisk(List<TileDef> tiles)
        {
            foreach (var tile in tiles)
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

        /// <summary>Whether any tile list anywhere in the current map -- any base layer, or any variation's own tile-list override -- still contains this exact id.</summary>
        private bool TileIdInUseAnywhere(string tileId)
        {
            foreach (var layer in _definition.Layers)
            {
                if (layer.Tiles.Exists(t => t.Id == tileId)) return true;
            }
            foreach (var variation in _definition.Variations)
            {
                foreach (var layerVariation in variation.LayerOverrides)
                {
                    if (layerVariation.Tiles != null && layerVariation.Tiles.Exists(t => t.Id == tileId)) return true;
                }
            }
            return false;
        }

        private void OnRemoveTile(int index)
        {
            var tiles = EditableTiles();
            if (tiles.Count <= 1) return; // CompiledLayer requires at least one tile
            if (index < 0 || index >= tiles.Count) return;

            tiles.RemoveAt(index);
            _tilesResetButton.Visible = _selectedVariationId != null;
            RebuildTileRows(EffectiveTiles());
            RefreshMovementPanel();
            Regenerate();
        }
    }
}
