using System.Collections.Generic;
using System.Linq;
using ProcGen.Engine.Model;

namespace ProcGen.Engine.Movement
{
    /// <summary>
    /// Bulk-editing helpers over a <see cref="MapDefinition.BlockedTransitions"/> list. There is no
    /// separate per-tile "walkable" flag -- "Solid" is purely a convenience that bulk-adds ordinary
    /// <see cref="TileTransitionRule"/>s, so the result is edited and inspected through the exact
    /// same list as any manually-added rule.
    /// </summary>
    public static class TraversalEditing
    {
        /// <summary>
        /// Blocks every other known tile from moving onto tileId, without blocking tileId from
        /// moving onto itself (same-type traversal always stays open) and without blocking tileId
        /// from moving onto anything else (leaving is never blocked, so a character can never get
        /// stuck standing on a tile made solid after the fact). Already-present rules are left as-is
        /// rather than duplicated.
        /// </summary>
        public static void MakeSolid(List<TileTransitionRule> rules, string tileId, IEnumerable<string> allTileIds)
        {
            var existing = new HashSet<(string From, string To)>(rules.Select(r => (r.FromTileId, r.ToTileId)));
            foreach (var otherTileId in allTileIds)
            {
                if (otherTileId == tileId)
                    continue;
                var pair = (otherTileId, tileId);
                if (existing.Add(pair))
                    rules.Add(new TileTransitionRule(otherTileId, tileId));
            }
        }

        /// <summary>Removes every rule that blocks movement onto tileId, from any source tile.</summary>
        public static void ClearBlocksInto(List<TileTransitionRule> rules, string tileId)
        {
            rules.RemoveAll(r => r.ToTileId == tileId);
        }
    }
}
