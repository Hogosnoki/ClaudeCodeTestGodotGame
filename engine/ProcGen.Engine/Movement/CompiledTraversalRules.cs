using System.Collections.Generic;
using ProcGen.Engine.Model;

namespace ProcGen.Engine.Movement
{
    /// <summary>
    /// Precomputed O(1) lookup over a <see cref="MapDefinition"/>'s <see cref="MapDefinition.BlockedTransitions"/>,
    /// built once (not once per movement check) so a game can call <see cref="IsBlocked"/> every step
    /// without re-scanning the rule list. Rules are directional and keyed purely on final tile ids --
    /// see <see cref="TileTransitionRule"/> for why layers never enter into it.
    /// </summary>
    public sealed class CompiledTraversalRules
    {
        private readonly HashSet<(string From, string To)> _blocked;

        private CompiledTraversalRules(HashSet<(string From, string To)> blocked)
        {
            _blocked = blocked;
        }

        public static CompiledTraversalRules Compile(MapDefinition def)
        {
            var blocked = new HashSet<(string From, string To)>();
            foreach (var rule in def.BlockedTransitions)
                blocked.Add((rule.FromTileId, rule.ToTileId));
            return new CompiledTraversalRules(blocked);
        }

        /// <summary>True if a character standing on a cell resolved to fromTileId is blocked from moving onto an adjacent cell resolved to toTileId.</summary>
        public bool IsBlocked(string fromTileId, string toTileId) => _blocked.Contains((fromTileId, toTileId));
    }
}
