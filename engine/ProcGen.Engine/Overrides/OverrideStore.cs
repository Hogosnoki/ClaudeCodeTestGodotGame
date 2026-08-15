using System.Collections.Generic;

namespace ProcGen.Engine.Overrides
{
    /// <summary>
    /// Sparse diff of manually hand-painted tiles: one (layer, position, tile) record per
    /// changed cell. Never holds a full map copy -- a hand-edit costs exactly one entry.
    /// This is the same override set that must round-trip through the save/export format so the
    /// runtime engine reproduces hand-edits exactly, not just the procedural base.
    /// </summary>
    public sealed class OverrideStore
    {
        private readonly Dictionary<TileOverrideKey, string> _overrides = new Dictionary<TileOverrideKey, string>();

        public void Set(string layerId, int x, int y, string tileId)
        {
            _overrides[new TileOverrideKey(layerId, x, y)] = tileId;
        }

        public bool Clear(string layerId, int x, int y)
        {
            return _overrides.Remove(new TileOverrideKey(layerId, x, y));
        }

        public bool TryGet(string layerId, int x, int y, out string? tileId)
        {
            return _overrides.TryGetValue(new TileOverrideKey(layerId, x, y), out tileId);
        }

        public int Count => _overrides.Count;

        /// <summary>Enumerates every override as a flat diff record, e.g. for saving to disk.</summary>
        public IEnumerable<TileOverride> Enumerate()
        {
            foreach (var kvp in _overrides)
            {
                yield return new TileOverride(kvp.Key.LayerId, kvp.Key.X, kvp.Key.Y, kvp.Value);
            }
        }

        public static OverrideStore FromRecords(IEnumerable<TileOverride> records)
        {
            var store = new OverrideStore();
            foreach (var r in records)
            {
                store.Set(r.LayerId, r.X, r.Y, r.TileId);
            }
            return store;
        }
    }
}
