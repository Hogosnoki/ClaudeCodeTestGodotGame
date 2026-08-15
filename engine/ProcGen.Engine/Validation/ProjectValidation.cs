using System.Collections.Generic;
using System.Linq;
using ProcGen.Engine.Model;

namespace ProcGen.Engine.Validation
{
    /// <summary>
    /// Cross-map checks for a whole project. Unlike the hard validation
    /// <see cref="ProcGen.Engine.Serialization.ProjectFileSerializer"/> does on load (duplicate map
    /// ids, duplicate entrance/exit ids), dangling-exit detection is a diagnostic, not an error: a
    /// map under active construction may reference a destination that doesn't exist yet, and that
    /// shouldn't block saving or loading -- it should just be visible.
    /// </summary>
    public static class ProjectValidation
    {
        public static List<DanglingExit> FindDanglingExits(IReadOnlyList<MapDefinition> maps)
        {
            var byId = maps.ToDictionary(m => m.MapId);
            var problems = new List<DanglingExit>();

            foreach (var map in maps)
            {
                foreach (var exit in map.Exits)
                {
                    if (!byId.TryGetValue(exit.DestinationMapId, out var destMap))
                    {
                        problems.Add(new DanglingExit(map.MapId, exit.Id, exit.DestinationMapId, exit.DestinationEntranceId, DanglingExitReason.MapNotFound));
                    }
                    else if (!destMap.Entrances.Exists(e => e.Id == exit.DestinationEntranceId))
                    {
                        problems.Add(new DanglingExit(map.MapId, exit.Id, exit.DestinationMapId, exit.DestinationEntranceId, DanglingExitReason.EntranceNotFound));
                    }
                }
            }
            return problems;
        }
    }
}
