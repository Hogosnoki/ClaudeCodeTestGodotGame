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

        /// <summary>
        /// The transitive "writes over" closure of tileId: tileId itself, plus every tile on any
        /// layer carrying a <see cref="WritesOverRule"/> whose TileId names something already in
        /// the set, repeated until no more are added. This is what lets "land" mean "land, or
        /// whatever a higher layer painted over it" -- e.g. ground_cover's dirt/grass/tallgrass,
        /// since ground_cover's WritesOverRule("ground", "land") means any of its own tiles can
        /// appear wherever "land" would otherwise have been the final result.
        /// </summary>
        public static HashSet<string> ExpandWritesOverFamily(string tileId, IEnumerable<LayerDef> layers)
        {
            var family = new HashSet<string> { tileId };
            var layerList = new List<LayerDef>(layers);
            bool addedAny;
            do
            {
                addedAny = false;
                foreach (var layer in layerList)
                {
                    if (!layer.WritesOver.Exists(w => family.Contains(w.TileId)))
                        continue;
                    foreach (var tile in layer.Tiles)
                    {
                        if (family.Add(tile.Id))
                            addedAny = true;
                    }
                }
            } while (addedAny);
            return family;
        }

        /// <summary>
        /// Same as <see cref="MakeSolid"/>, but the blocked target is tileId's whole
        /// <see cref="ExpandWritesOverFamily"/> rather than just the literal id -- every tile
        /// outside the family is blocked from entering any tile inside it, movement stays open
        /// between family members (the "same type" exemption extended to the whole family), and
        /// leaving is still never blocked.
        /// </summary>
        public static void MakeSolidFamily(List<TileTransitionRule> rules, string tileId, IEnumerable<string> allTileIds, IEnumerable<LayerDef> layers)
        {
            var family = ExpandWritesOverFamily(tileId, layers);
            var existing = new HashSet<(string From, string To)>(rules.Select(r => (r.FromTileId, r.ToTileId)));
            foreach (var target in family)
            {
                foreach (var otherTileId in allTileIds)
                {
                    if (family.Contains(otherTileId))
                        continue;
                    var pair = (otherTileId, target);
                    if (existing.Add(pair))
                        rules.Add(new TileTransitionRule(otherTileId, target));
                }
            }
        }

        /// <summary>Same as <see cref="ClearBlocksInto"/>, but clears incoming blocks for every tile in tileId's <see cref="ExpandWritesOverFamily"/>, not just the literal id.</summary>
        public static void ClearBlocksIntoFamily(List<TileTransitionRule> rules, string tileId, IEnumerable<LayerDef> layers)
        {
            var family = ExpandWritesOverFamily(tileId, layers);
            rules.RemoveAll(r => family.Contains(r.ToTileId));
        }
    }
}
